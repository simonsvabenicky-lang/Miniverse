using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Miniverse.Hub;

namespace Miniverse.EditorTools
{
    /// <summary>
    /// Builds Home.unity from scratch, in code — same reasoning as Frontline's SceneBuilder:
    /// the scene is a build artifact, never hand-edited in the Editor, so it can't drift and
    /// can always be regenerated from source.
    ///
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Miniverse.EditorTools.HubSceneBuilder.Build
    /// </summary>
    public static class HubSceneBuilder
    {
        const string ScenePath = "Assets/_Hub/Scenes/Home.unity";

        [MenuItem("Miniverse/Rebuild Home Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Home has never had a camera -- its Canvas is ScreenSpaceOverlay, which doesn't
            // need one to draw. That's fine while a minigame's own camera is doing the actual
            // frame-clearing, but the moment a minigame's scene unloads and takes its camera
            // with it, there are zero cameras left in the whole loaded scene set. With nothing
            // left to clear the color buffer, the GPU just keeps showing the outgoing minigame's
            // last rendered frame indefinitely, with only Home's tiles/title drawn on top of it
            // (Canvas UI doesn't clear first, it just draws on whatever's already there) -- the
            // "old game frozen behind Home's tiles" overlap seen on-device after exiting FlowSort
            // or Frontline. Deliberately NOT tagged MainCamera: FlowSort's TapInputRouter (and
            // any future minigame) resolves world taps via Camera.main, and a second camera
            // wearing that tag would make that lookup ambiguous the moment a minigame is loaded
            // alongside Home. Depth -100 so a minigame's own (default depth 0) camera always
            // renders after this one and is what's actually visible while playing -- this camera
            // only matters in the gap where it's the sole camera left.
            var cameraGO = new GameObject("HomeBackgroundCamera", typeof(Camera));
            var homeCamera = cameraGO.GetComponent<Camera>();
            homeCamera.clearFlags = CameraClearFlags.SolidColor;
            homeCamera.backgroundColor = new Color(0.07f, 0.07f, 0.09f);
            homeCamera.cullingMask = 0;
            homeCamera.depth = -100f;

            // Project is Input System-only (activeInputHandler: 1, matching Frontline) so this
            // must be InputSystemUIInputModule, not the legacy StandaloneInputModule — the
            // legacy module reads UnityEngine.Input, which is disabled entirely in this mode
            // and would silently never fire (caught via Frontline's graduation: their
            // CanvasBuilder.cs hit this exact issue first). Added with no actions assigned,
            // InputSystemUIInputModule auto-generates default point/click bindings in OnEnable,
            // which is exactly what happens for a component added purely in code like this.
            var eventSystemGO = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); // portrait mobile
            scaler.matchWidthOrHeight = 0.5f;

            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGO.transform.SetParent(canvasGO.transform, false);
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -40f);
            titleRect.sizeDelta = new Vector2(0f, 120f);
            var title = titleGO.GetComponent<Text>();
            title.text = "PocketVerse";
            title.fontSize = 64;
            title.alignment = TextAnchor.MiddleCenter;
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.color = Color.white;

            var scrollGO = new GameObject("GameGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            scrollGO.transform.SetParent(canvasGO.transform, false);
            var gridRect = scrollGO.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.05f, 0.05f);
            gridRect.anchorMax = new Vector2(0.95f, 0.85f);
            gridRect.offsetMin = Vector2.zero;
            gridRect.offsetMax = Vector2.zero;
            var grid = scrollGO.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(320f, 320f);
            grid.spacing = new Vector2(24f, 24f);
            grid.childAlignment = TextAnchor.UpperCenter;

            var emptyLabelGO = new GameObject("EmptyStateLabel", typeof(RectTransform), typeof(Text));
            emptyLabelGO.transform.SetParent(canvasGO.transform, false);
            var emptyRect = emptyLabelGO.GetComponent<RectTransform>();
            emptyRect.anchorMin = new Vector2(0.1f, 0.4f);
            emptyRect.anchorMax = new Vector2(0.9f, 0.6f);
            emptyRect.offsetMin = Vector2.zero;
            emptyRect.offsetMax = Vector2.zero;
            var emptyLabel = emptyLabelGO.GetComponent<Text>();
            emptyLabel.text = "No games yet — graduate one into Assets/Games/ to see it here.";
            emptyLabel.alignment = TextAnchor.MiddleCenter;
            emptyLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            emptyLabel.color = new Color(0.7f, 0.7f, 0.7f);

            var bootstrapGO = new GameObject("HubBootstrap", typeof(HubLauncher), typeof(HomeScreenController));
            var homeController = bootstrapGO.GetComponent<HomeScreenController>();
            var so = new SerializedObject(homeController);
            so.FindProperty("_gridParent").objectReferenceValue = gridRect;
            so.FindProperty("_emptyStateLabel").objectReferenceValue = emptyLabel;
            so.ApplyModifiedPropertiesWithoutUndo();

            var launcher = bootstrapGO.GetComponent<HubLauncher>();
            var launcherSo = new SerializedObject(launcher);
            launcherSo.FindProperty("_homeUIRoot").objectReferenceValue = canvasGO;
            launcherSo.FindProperty("_homeEventSystem").objectReferenceValue = eventSystemGO;
            launcherSo.ApplyModifiedPropertiesWithoutUndo();

            System.IO.Directory.CreateDirectory("Assets/_Hub/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Miniverse] Home scene rebuilt at {ScenePath}");
        }
    }
}

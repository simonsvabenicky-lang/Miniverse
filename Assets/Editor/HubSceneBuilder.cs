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
            // 540x960 -- matches Frontline's own CanvasScaler reference exactly (see its
            // CanvasBuilder.Build()), not this scene's previous 1080x1920: every pixel size
            // HubCanvasBuilder borrows from Frontline's dimensions (topBarH, panel widths,
            // button sizes) is only proportioned correctly against the same reference the
            // numbers were measured against. Using the same reference across both also means a
            // 44px icon button reads as the same relative size in both apps, not just the same
            // sprite -- part of "shared across the platform", not just a coincidence.
            scaler.referenceResolution = new Vector2(540, 960);
            scaler.matchWidthOrHeight = 0.5f;

            var result = Miniverse.EditorTools.HubCanvasBuilder.Build(canvasGO.transform);

            var bootstrapGO = new GameObject("HubBootstrap", typeof(HubLauncher), typeof(HomeScreenController), typeof(HomeShellController));
            var homeController = bootstrapGO.GetComponent<HomeScreenController>();
            var so = new SerializedObject(homeController);
            so.FindProperty("_gridParent").objectReferenceValue = result.GridParent;
            so.FindProperty("_emptyStateLabel").objectReferenceValue = result.EmptyStateLabel;
            so.FindProperty("_cardFrame").objectReferenceValue = result.CardFrame;
            so.FindProperty("_titleFont").objectReferenceValue = result.TitleFont;
            var accentProp = so.FindProperty("_accentColors");
            accentProp.arraySize = result.AccentColors.Length;
            for (int i = 0; i < result.AccentColors.Length; i++)
                accentProp.GetArrayElementAtIndex(i).colorValue = result.AccentColors[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            var shellController = bootstrapGO.GetComponent<HomeShellController>();
            var shellSo = new SerializedObject(shellController);
            shellSo.FindProperty("_soundOnIcon").objectReferenceValue = result.SoundOnIcon;
            shellSo.FindProperty("_soundOffIcon").objectReferenceValue = result.SoundOffIcon;
            shellSo.ApplyModifiedPropertiesWithoutUndo();

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

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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

            var eventSystemGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

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
            title.text = "Miniverse";
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

            System.IO.Directory.CreateDirectory("Assets/_Hub/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Miniverse] Home scene rebuilt at {ScenePath}");
        }
    }
}

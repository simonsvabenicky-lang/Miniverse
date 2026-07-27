using FlowSort.Gameplay;
using FlowSort.Hub;
using FlowSort.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FlowSort.EditorTools
{
    /// <summary>
    /// Builds Main.unity from scratch, in code — same reasoning as Frontline/PocketVerse: the
    /// scene is a build artifact, never hand-edited in the Editor.
    ///
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod FlowSort.EditorTools.SceneBuilder.Build
    /// </summary>
    public static class SceneBuilder
    {
        // Named FlowSortMain, not Main -- Frontline's graduated scene is already
        // Assets/Games/Frontline/Scenes/Main.unity, and Unity's SceneManager.LoadScene(name)
        // resolves scenes by filename across the whole build; two scenes sharing "Main" would
        // be ambiguous. Flagged as a known risk in FlowSort's own HANDOFF before graduation.
        const string ScenePath = "Assets/Games/FlowSort/Scenes/FlowSortMain.unity";
        const string ArtRoot = "Assets/Games/FlowSort/Art/Kenney/";
        static readonly Color BackgroundColor = new Color32(0xFB, 0xF0, 0xDC, 0xFF); // warm cream, not blue

        [MenuItem("FlowSort/Rebuild Main Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            new GameObject("TapInputRouter", typeof(TapInputRouter));

            var art = BuildArtRegistry();
            var wallet = new GameObject("CurrencyWallet", typeof(CurrencyWallet)).GetComponent<CurrencyWallet>();

            var gridGO = new GameObject("PuzzleGrid", typeof(PuzzleGrid));
            var grid = gridGO.GetComponent<PuzzleGrid>();
            grid.GridRoot = new GameObject("GridRoot").transform;
            grid.GridRoot.SetParent(gridGO.transform, false);
            grid.BackdropRoot = new GameObject("BackdropRoot").transform;
            grid.BackdropRoot.SetParent(gridGO.transform, false);

            var queueGO = new GameObject("CritterQueue", typeof(CritterQueue));
            var queue = queueGO.GetComponent<CritterQueue>();
            queue.QueueRoot = new GameObject("QueueRoot").transform;
            queue.QueueRoot.SetParent(queueGO.transform, false);

            var lanes = new FiringLane[GameTuning.LaneCount];
            for (int i = 0; i < lanes.Length; i++)
            {
                var laneGO = new GameObject($"Lane_{i}", typeof(FiringLane));
                laneGO.transform.position = new Vector3(GameTuning.LaneX[i], GameTuning.LaneY, 0f);
                var lane = laneGO.GetComponent<FiringLane>();
                lane.Grid = grid;
                lanes[i] = lane;
            }

            var gmGO = new GameObject("GameManager", typeof(RevealGameManager));
            var gm = gmGO.GetComponent<RevealGameManager>();
            gm.Grid = grid;
            gm.Queue = queue;
            gm.Lanes = lanes;
            gm.Wallet = wallet;

            queue.GameManager = gm;
            foreach (var lane in lanes) lane.GameManager = gm;

            gmGO.AddComponent<FlowSortMiniGame>();

            BuildCanvas(art, wallet, gm);

            System.IO.Directory.CreateDirectory("Assets/Games/FlowSort/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            // Not touching EditorBuildSettings.scenes here — PocketVerse/BuildSceneSync.Sync()
            // is the source of truth for the hub's build scene list (rebuilds it from every
            // Assets/Games/*/Scenes/*.unity + Home on disk). Clobbering it here would silently
            // drop Home and every other graduated game from Build Settings, same bug fixed
            // during Frontline's graduation.

            Debug.Log($"[FlowSort] Main scene rebuilt at {ScenePath}");
        }

        static void BuildCamera()
        {
            var camGO = new GameObject("Main Camera", typeof(Camera));
            camGO.tag = "MainCamera";
            var cam = camGO.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BackgroundColor;
            camGO.transform.position = new Vector3(0f, 0f, -10f);
        }

        static ArtRegistry BuildArtRegistry()
        {
            var go = new GameObject("ArtRegistry", typeof(ArtRegistry));
            var art = go.GetComponent<ArtRegistry>();

            string[] colorFolders = { "blue", "green", "pink", "purple", "red", "yellow" };
            for (int i = 0; i < colorFolders.Length; i++)
                art.BlockSprites[i] = Load($"{ArtRoot}ShapeCharacters/{colorFolders[i]}_body_squircle.png");

            string[] faceFiles = { "face_a", "face_b", "face_c", "face_d" };
            for (int i = 0; i < faceFiles.Length; i++)
                art.FaceSprites[i] = Load($"{ArtRoot}ShapeCharacters/{faceFiles[i]}.png");

            art.KeyIcon = Load($"{ArtRoot}Icons/key.png");
            art.CoinIcon = Load($"{ArtRoot}Icons/coin.png");
            art.StarIcon = Load($"{ArtRoot}Icons/star.png");
            art.LockedIcon = Load($"{ArtRoot}Icons/locked.png");
            art.UnlockedIcon = Load($"{ArtRoot}Icons/unlocked.png");

            art.PanelSprite = Load($"{ArtRoot}UIPack/Grey/button_rectangle_flat.png");
            art.RoundButtonGrey = Load($"{ArtRoot}UIPack/Grey/button_round_flat.png");
            string[] powerupColors = { "Blue", "Green", "Red", "Yellow" };
            for (int i = 0; i < powerupColors.Length; i++)
                art.RoundButtonColored[i] = Load($"{ArtRoot}UIPack/{powerupColors[i]}/button_round_flat.png");

            return art;
        }

        static Sprite Load(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogError($"[FlowSort] Missing sprite at {path}");
            return sprite;
        }

        static void BuildCanvas(ArtRegistry art, CurrencyWallet wallet, RevealGameManager gm)
        {
            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(540f, 960f);
            scaler.matchWidthOrHeight = 0.5f;

            var exitGO = CreateImage(canvasGO.transform, "ExitButton", art.RoundButtonGrey,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(56f, 56f));
            var exitButton = exitGO.AddComponent<Button>();
            gm.ExitButton = exitButton;
            AddLabel(exitGO.transform, "Label", "X",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                TextAlignmentOptions.Center, 26, new Color32(0x5A, 0x4A, 0x3C, 0xFF));

            var levelText = AddLabel(canvasGO.transform, "LevelText", "Level 1",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(92f, -20f), new Vector2(220f, 56f),
                TextAlignmentOptions.TopLeft, 32, new Color32(0x5A, 0x4A, 0x3C, 0xFF));

            var keysPanel = CreateImage(canvasGO.transform, "KeysPanel", art.PanelSprite,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(150f, 56f));
            var coinImg = CreateImage(keysPanel.transform, "CoinIcon", art.CoinIcon,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(36f, 36f));
            var keysText = AddLabel(keysPanel.transform, "KeysText", "0",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(90f, 44f),
                TextAlignmentOptions.MidlineRight, 30, new Color32(0x5A, 0x4A, 0x3C, 0xFF));

            var hud = canvasGO.AddComponent<RevealHud>();
            hud.Wallet = wallet;
            hud.LevelText = levelText;
            hud.KeysText = keysText;
            gm.Hud = hud;

            BuildPowerupBar(canvasGO.transform, art, gm);
            BuildPetShop(canvasGO.transform, art, wallet);
        }

        static void BuildPowerupBar(Transform canvasParent, ArtRegistry art, RevealGameManager gm)
        {
            var barGO = new GameObject("PowerupBar", typeof(RectTransform));
            barGO.transform.SetParent(canvasParent, false);
            var barRect = barGO.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.5f, 0f);
            barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 150f);
            barRect.sizeDelta = new Vector2(500f, 110f);

            (string label, Sprite sprite) refill = ("Refill", art.RoundButtonColored[0]);
            (string label, Sprite sprite) shuffle = ("Shuffle", art.RoundButtonColored[1]);
            (string label, Sprite sprite) undo = ("Undo", art.RoundButtonColored[2]);
            (string label, Sprite sprite) hint = ("Hint", art.RoundButtonColored[3]);
            var defs = new[] { refill, shuffle, undo, hint };

            var buttons = new Button[4];
            for (int i = 0; i < defs.Length; i++)
            {
                float x = (i - 1.5f) * 118f;
                var btnGO = CreateImage(barGO.transform, $"Powerup_{defs[i].label}", defs[i].sprite,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 20f), new Vector2(96f, 96f));
                buttons[i] = btnGO.AddComponent<Button>();

                AddLabel(barGO.transform, $"Powerup_{defs[i].label}_Label", defs[i].label,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -42f), new Vector2(110f, 30f),
                    TextAlignmentOptions.Center, 18, new Color32(0x5A, 0x4A, 0x3C, 0xFF));
            }

            var powerupBar = barGO.AddComponent<PowerupBar>();
            powerupBar.GameManager = gm;
            powerupBar.RefillButton = buttons[0];
            powerupBar.ShuffleButton = buttons[1];
            powerupBar.UndoButton = buttons[2];
            powerupBar.HintButton = buttons[3];
        }

        static void BuildPetShop(Transform canvasParent, ArtRegistry art, CurrencyWallet wallet)
        {
            var shopGO = new GameObject("PetShop", typeof(RectTransform));
            shopGO.transform.SetParent(canvasParent, false);
            var shopRect = shopGO.GetComponent<RectTransform>();
            shopRect.anchorMin = new Vector2(0.5f, 0f);
            shopRect.anchorMax = new Vector2(0.5f, 0f);
            shopRect.pivot = new Vector2(0.5f, 0f);
            shopRect.anchoredPosition = new Vector2(0f, 10f);
            shopRect.sizeDelta = new Vector2(520f, 120f);

            var petIcons = new Image[6];
            var costLabels = new TMP_Text[6];
            var buttons = new Button[6];
            var lockOverlays = new GameObject[6];

            for (int i = 0; i < 6; i++)
            {
                float x = (i - 2.5f) * 84f;

                var slotGO = CreateImage(shopGO.transform, $"PetSlot_{i}", art.PanelSprite,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 40f), new Vector2(76f, 76f));
                buttons[i] = slotGO.AddComponent<Button>();

                var iconGO = CreateImage(slotGO.transform, "Icon", null,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(48f, 48f));
                petIcons[i] = iconGO.GetComponent<Image>();

                var lockGO = CreateImage(slotGO.transform, "LockOverlay", art.LockedIcon,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76f, 76f));
                lockGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
                lockOverlays[i] = lockGO;

                costLabels[i] = AddLabel(shopGO.transform, $"PetCost_{i}", "0",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -14f), new Vector2(80f, 30f),
                    TextAlignmentOptions.Center, 18, new Color32(0x5A, 0x4A, 0x3C, 0xFF));
            }

            var petShop = shopGO.AddComponent<PetShop>();
            petShop.Wallet = wallet;
            petShop.PetIcons = petIcons;
            petShop.CostLabels = costLabels;
            petShop.Buttons = buttons;
            petShop.LockOverlays = lockOverlays;
        }

        static GameObject CreateImage(Transform parent, string name, Sprite sprite,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            if (sprite != null) img.preserveAspect = true;
            return go;
        }

        static TMP_Text AddLabel(Transform parent, string name, string text,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta,
            TextAlignmentOptions alignment, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = color;
            return tmp;
        }
    }
}

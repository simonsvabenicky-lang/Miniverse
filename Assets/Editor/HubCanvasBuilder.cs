using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Miniverse.Hub;

namespace Miniverse.EditorTools
{
    /// <summary>
    /// Builds Home's real UI as part of the generated scene -- same "build artifact, styled with
    /// the Kenney UI Pack" contract as Frontline's own CanvasBuilder
    /// (Assets/Games/Frontline/Editor/CanvasBuilder.cs), which this deliberately mirrors helper
    /// method for helper method so the two screens read as one app's UI, not two different
    /// conventions bolted together. Structure only -- HomeShellController wires click behaviour
    /// and runtime-only content (counters, the Profile stat) at Awake, since a Button's onClick
    /// listener is a C# delegate and delegates don't survive EditorSceneManager.SaveScene.
    ///
    /// Replaces the skeleton app's flat black/white Canvas (a Title, a GridLayoutGroup, an empty
    /// label, nothing else) per Simon's "give the skeleton app real UI... have pocketverse feel
    /// like an app" ask: a real background, a persistent top bar (profile/settings/sound/lives/
    /// cash) and bottom tab bar (Home/Store), and Settings/Store/Profile overlay panels.
    /// </summary>
    public static class HubCanvasBuilder
    {
        const string UiRoot = "Assets/_Hub/Art/UI";

        public struct Result
        {
            public RectTransform GridParent;
            public TextMeshProUGUI EmptyStateLabel;
            public Sprite CardBackground;
            public Sprite[] BadgeSprites;
            public Sprite SoundOnIcon;
            public Sprite SoundOffIcon;
        }

        public static Result Build(Transform canvasRoot)
        {
            BuildBackground(canvasRoot);
            var (gridParent, emptyLabel) = BuildGameGridCanvas(canvasRoot);
            BuildSettingsPanel(canvasRoot);
            BuildStorePanel(canvasRoot);
            BuildProfilePanel(canvasRoot);
            BuildShell(canvasRoot);

            return new Result
            {
                GridParent = gridParent,
                EmptyStateLabel = emptyLabel,
                // button_rectangle_depth_gradient, not the flat input_rectangle panel: it has a
                // real baked-in gradient and drop-shadow band along the bottom (see Frontline's
                // UIImporter border comment), which is most of what makes a game tile read as a
                // raised card instead of a plain coloured rectangle -- input_rectangle alone
                // looked flat on-device even with HomeScreenController's own Shadow layer behind it.
                CardBackground = ColorSprite("Grey", "button_rectangle_depth_gradient"),
                // Grey deliberately excluded: it's light enough that the badge's white letter
                // read as nearly invisible against it on-device (confirmed first pass, Frontline's
                // tile got the Grey badge and its "F" all but disappeared). Blue/Green/Yellow are
                // all saturated enough for white text to read clearly on every one.
                BadgeSprites = new[]
                {
                    ColorSprite("Blue", "button_round_depth_gloss"),
                    ColorSprite("Green", "button_round_depth_gloss"),
                    ColorSprite("Yellow", "button_round_depth_gloss"),
                },
                SoundOnIcon = IconSprite("audioOn"),
                SoundOffIcon = IconSprite("audioOff"),
            };
        }

        /// <summary>Sibling index 0 -- a real gradient backdrop instead of the skeleton app's flat colour, per Simon's "real background not just black or white screen".</summary>
        static void BuildBackground(Transform canvasRoot)
        {
            var root = NewRect("Background", canvasRoot);
            Stretch(root);
            var img = root.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            root.gameObject.AddComponent<ProceduralHomeBackground>();
        }

        /// <summary>The default view: a small title/logo strip and the game grid. HomeScreenController fills the grid itself at runtime.</summary>
        static (RectTransform gridParent, TextMeshProUGUI emptyLabel) BuildGameGridCanvas(Transform canvasRoot)
        {
            var root = NewRect("GameGridCanvas", canvasRoot);
            Stretch(root);

            // TopBar (see BuildShell) is 108/960 = 0.1125 of the reference height, so its bottom
            // edge sits at y=0.8875 -- Title has to stay entirely below that or the two overlap.
            // First pass put Title at 0.87-0.95 without accounting for this and it visibly
            // collided with the top bar's pills on-device.
            var title = AnchorRect(root, "Title", new Vector2(0f, 0.79f), new Vector2(1f, 0.875f));
            var titleTmp = title.gameObject.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "PocketVerse";
            titleTmp.fontSize = 42;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = Color.white;
            titleTmp.raycastTarget = false;

            var grid = AnchorRect(root, "GameGrid", new Vector2(0.05f, 0.13f), new Vector2(0.95f, 0.78f));
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(320f, 320f);
            layout.spacing = new Vector2(24f, 24f);
            layout.childAlignment = TextAnchor.UpperCenter;

            var emptyLabel = AnchorRect(root, "EmptyStateLabel", new Vector2(0.1f, 0.4f), new Vector2(0.9f, 0.6f));
            var emptyTmp = emptyLabel.gameObject.AddComponent<TextMeshProUGUI>();
            emptyTmp.text = "No games yet — graduate one into Assets/Games/ to see it here.";
            emptyTmp.alignment = TextAlignmentOptions.Center;
            emptyTmp.color = new Color(0.85f, 0.85f, 0.9f);
            emptyTmp.raycastTarget = false;

            return (grid, emptyTmp);
        }

        /// <summary>Sound toggle + Back -- the only real setting so far (matches Frontline's own "MUSIC (none yet)" honesty about what's actually wired up).</summary>
        static void BuildSettingsPanel(Transform canvasRoot)
        {
            var root = NewRect("SettingsPanel", canvasRoot);
            Stretch(root);
            BuildDim(root);
            AddTitle(root, "SETTINGS", 173f, 44);

            var soundRow = CreatePanel(root, "SoundPanel", 0f, 340f, 420f, 72f);
            AddRowLabel(soundRow, "SOUND", 24, 0.7f);
            // The sound toggle lives in the top bar itself (see BuildShell) -- this row is a
            // second, larger place to see/flip the same state, not a separate setting.
            var noteRect = AnchorRect(soundRow, "Note", new Vector2(0.7f, 0f), new Vector2(0.96f, 1f));
            var noteTmp = noteRect.gameObject.AddComponent<TextMeshProUGUI>();
            noteTmp.text = "see top bar";
            noteTmp.fontSize = 14;
            noteTmp.alignment = TextAlignmentOptions.Right;
            noteTmp.color = new Color(0.2f, 0.2f, 0.22f, 0.5f);
            noteTmp.raycastTarget = false;

            CreateButton(root, "BackButton", "BACK", ButtonSprite("Grey"), 0f, 460f, 300f, 76f, 26);
            // Left active -- see BuildProfilePanel's comment on why HomeShellController hides
            // this at runtime instead.
        }

        /// <summary>Honest placeholder, not a fake shop -- nothing sells anything yet. Matches Frontline's own "COMING SOON" convention rather than pretending a store exists.</summary>
        static void BuildStorePanel(Transform canvasRoot)
        {
            var root = NewRect("StorePanel", canvasRoot);
            Stretch(root);
            BuildDim(root);
            AddTitle(root, "STORE", 173f, 44);

            var card = CreatePanel(root, "ComingSoonCard", 0f, 420f, 420f, 200f);
            var labelRect = AnchorRect(card, "Label", new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.9f));
            var labelTmp = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            labelTmp.text = "More coming soon";
            labelTmp.fontSize = 24;
            labelTmp.fontStyle = FontStyles.Bold;
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.color = new Color(0.15f, 0.15f, 0.18f, 0.8f);
            labelTmp.raycastTarget = false;

            CreateButton(root, "BackButton", "BACK", ButtonSprite("Grey"), 0f, 680f, 300f, 76f, 26);
            // Left active -- see BuildProfilePanel's comment on why.
        }

        /// <summary>Avatar + one real stat (games played, from HubStats) -- not a fake profile with invented numbers.</summary>
        static void BuildProfilePanel(Transform canvasRoot)
        {
            var root = NewRect("ProfilePanel", canvasRoot);
            Stretch(root);
            BuildDim(root);
            AddTitle(root, "PROFILE", 173f, 44);

            var avatarRt = PlaceTopCenter(NewRect("Avatar", root), 0f, 300f, 140f, 140f);
            var avatarImg = avatarRt.gameObject.AddComponent<Image>();
            avatarImg.sprite = ColorSprite("Grey", "button_round_depth_gloss");
            avatarImg.raycastTarget = false;
            var avatarIconRt = AnchorRect(avatarRt, "Icon", new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f));
            avatarIconRt.gameObject.AddComponent<Image>().raycastTarget = false;
            avatarIconRt.gameObject.AddComponent<ProceduralAvatarIcon>();

            var nameRect = PlaceTopCenter(NewRect("PlayerName", root), 0f, 470f, 400f, 50f);
            var nameTmp = nameRect.gameObject.AddComponent<TextMeshProUGUI>();
            nameTmp.text = "PocketVerse Player";
            nameTmp.fontSize = 26;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.color = Color.white;
            nameTmp.raycastTarget = false;

            // Filled at runtime (HomeShellController) from HubStats.GamesPlayed -- can't be
            // known at scene-build time, same reasoning as Frontline's Leaderboard rows.
            var statsRect = PlaceTopCenter(NewRect("GamesPlayedText", root), 0f, 530f, 400f, 40f);
            var statsTmp = statsRect.gameObject.AddComponent<TextMeshProUGUI>();
            statsTmp.text = "";
            statsTmp.fontSize = 18;
            statsTmp.alignment = TextAlignmentOptions.Center;
            statsTmp.color = new Color(1f, 1f, 1f, 0.75f);
            statsTmp.raycastTarget = false;

            CreateButton(root, "BackButton", "BACK", ButtonSprite("Grey"), 0f, 620f, 300f, 76f, 26);
            // Deliberately left active here (not SetActive(false)): GameObject.Find can't locate
            // an inactive object at all, even mid-path, and HomeShellController.Awake() finds
            // every panel by name via GameObject.Find($"Canvas/{name}") before it has any other
            // reference to hide them by. Confirmed on-device: baking these inactive here left
            // Find() silently returning null for all three panels, so tapping the settings gear
            // just hid the game grid and showed nothing -- Settings/Store/Profile never actually
            // opened. Same reasoning as Frontline's own GameUI: every CanvasBuilder screen starts
            // active, and RefreshCanvasVisibility (here, HomeShellController's ShowHome() call at
            // the end of Awake) is what actually hides the non-current ones, after they've
            // already been found.
        }

        /// <summary>A translucent full-screen scrim behind an overlay panel's content, so Settings/Store/Profile read as a sheet over Home rather than a second unrelated screen.</summary>
        static void BuildDim(Transform panelRoot)
        {
            var dim = NewRect("Dim", panelRoot);
            Stretch(dim);
            dim.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.72f);
        }

        /// <summary>
        /// Persistent chrome: top bar (profile / settings / sound / lives / cash) and a bottom
        /// tab bar (Home / Store). Built last -> highest sibling index -> always renders on top
        /// of whichever panel (GameGridCanvas/Settings/Store/Profile) is currently showing,
        /// exactly like Frontline's own Shell.
        /// </summary>
        static void BuildShell(Transform canvasRoot)
        {
            var root = NewRect("Shell", canvasRoot);
            Stretch(root);

            const float topBarH = 108f;
            var topBar = NewRect("TopBar", root);
            StretchTop(topBar, topBarH);

            var profileBtn = CreateIconButton(topBar, "ProfileButton", null, 0.08f, 0.5f, 44f);
            var profileIconRt = AnchorRect(profileBtn.transform, "Icon", new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.78f));
            profileIconRt.gameObject.AddComponent<Image>().raycastTarget = false;
            profileIconRt.gameObject.AddComponent<ProceduralAvatarIcon>();

            CreateIconButton(topBar, "SettingsGearButton", IconSprite("gear"), 0.20f, 0.5f, 44f);
            CreateIconButton(topBar, "SoundToggleButton", IconSprite("audioOn"), 0.32f, 0.5f, 44f);

            var livesPill = CreatePillPanel(topBar, "LivesPill", 0.58f, 0.5f, 130f, 52f);
            var heartIconRt = AnchorPoint(livesPill, "HeartIcon", 0.20f, 0.5f, 28f, 28f);
            heartIconRt.gameObject.AddComponent<Image>().raycastTarget = false;
            heartIconRt.gameObject.AddComponent<ProceduralHeartIcon>();
            var livesTmp = AnchorRect(livesPill, "Value", new Vector2(0.36f, 0f), new Vector2(0.95f, 1f))
                .gameObject.AddComponent<TextMeshProUGUI>();
            livesTmp.text = "0";
            livesTmp.fontSize = 20;
            livesTmp.fontStyle = FontStyles.Bold;
            livesTmp.alignment = TextAlignmentOptions.Left;
            livesTmp.color = new Color(0.15f, 0.15f, 0.18f);
            livesTmp.raycastTarget = false;

            var cashPill = CreatePillPanel(topBar, "CashPill", 0.85f, 0.5f, 120f, 52f);
            var coinIconRt = AnchorPoint(cashPill, "CoinIcon", 0.22f, 0.5f, 28f, 28f);
            var coinImg = coinIconRt.gameObject.AddComponent<Image>();
            coinImg.sprite = IconSprite("coin");
            coinImg.raycastTarget = false;
            var cashTmp = AnchorRect(cashPill, "Value", new Vector2(0.4f, 0f), new Vector2(0.95f, 1f))
                .gameObject.AddComponent<TextMeshProUGUI>();
            cashTmp.text = "0";
            cashTmp.fontSize = 20;
            cashTmp.fontStyle = FontStyles.Bold;
            cashTmp.alignment = TextAlignmentOptions.Left;
            cashTmp.color = new Color(0.15f, 0.15f, 0.18f);
            cashTmp.raycastTarget = false;

            const float tabBarH = 118f;
            var tabBar = NewRect("BottomTabBar", root);
            StretchBottom(tabBar, tabBarH);
            var tabBarImg = tabBar.gameObject.AddComponent<Image>();
            tabBarImg.color = new Color(0f, 0f, 0f, 0.35f);

            CreateTabButton(tabBar, "HomeTab", IconSprite("home"), "HOME", 0, 2);
            CreateTabButton(tabBar, "StoreTab", IconSprite("shoppingCart"), "STORE", 1, 2);
        }

        // ---- helpers (ported from Frontline's CanvasBuilder -- same visual language, same art root convention, repointed at Assets/_Hub/Art/UI) ----

        static Sprite ColorSprite(string color, string file) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{UiRoot}/{color}/{file}.png");
        static Sprite ExtraSprite(string file) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{UiRoot}/Extra/{file}.png");
        static Sprite IconSprite(string file) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{UiRoot}/Icons/{file}.png");
        static Sprite ButtonSprite(string color) => ColorSprite(color, "button_rectangle_depth_gloss");
        static Sprite SquareButtonSprite(string color) => ColorSprite(color, "button_square_depth_gloss");

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static RectTransform StretchTop(RectTransform rt, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        static RectTransform StretchBottom(RectTransform rt, float height)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        static RectTransform AnchorRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        static RectTransform AnchorPoint(Transform parent, string name, float ax, float ay, float w, float h)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        static RectTransform PlaceTopCenter(RectTransform rt, float x, float yFromTop, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -yFromTop);
            return rt;
        }

        static TextMeshProUGUI AddTitle(Transform parent, string text, float yFromTop, int fontSize)
        {
            var rt = NewRect("Title", parent);
            PlaceTopCenter(rt, 0f, yFromTop, 500f, 80f);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        static void AddRowLabel(Transform panel, string text, int fontSize, float rightEdge)
        {
            var rt = AnchorRect(panel, "Label", new Vector2(0.06f, 0f), new Vector2(rightEdge, 1f));
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = new Color(0.15f, 0.15f, 0.18f);
            tmp.raycastTarget = false;
        }

        static RectTransform CreatePanel(Transform parent, string name, float x, float yFromTop, float w, float h)
        {
            var rt = NewRect(name, parent);
            PlaceTopCenter(rt, x, yFromTop, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = ExtraSprite("input_rectangle");
            img.type = Image.Type.Sliced;
            return rt;
        }

        static RectTransform CreatePillPanel(Transform parent, string name, float ax, float ay, float w, float h)
        {
            var rt = AnchorPoint(parent, name, ax, ay, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = ExtraSprite("input_rectangle");
            img.type = Image.Type.Sliced;
            return rt;
        }

        static Button CreateButton(Transform parent, string name, string label, Sprite sprite,
                                   float x, float yFromTop, float w, float h, int fontSize)
        {
            var rt = NewRect(name, parent);
            PlaceTopCenter(rt, x, yFromTop, w, h);

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            var labelRt = NewRect("Label", rt);
            Stretch(labelRt);
            var tmp = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color32(40, 40, 45, 255);
            tmp.raycastTarget = false;

            return button;
        }

        /// <summary>A square icon-only button. Pass a null icon and add your own child (e.g. ProfileButton's ProceduralAvatarIcon) when no static sprite fits.</summary>
        static Button CreateIconButton(Transform parent, string name, Sprite icon, float ax, float ay, float size)
        {
            var rt = AnchorPoint(parent, name, ax, ay, size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = SquareButtonSprite("Grey");
            img.type = Image.Type.Sliced;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            if (icon != null)
            {
                var iconRt = AnchorPoint(rt, "Icon", 0.5f, 0.5f, size * 0.52f, size * 0.52f);
                var iconImg = iconRt.gameObject.AddComponent<Image>();
                iconImg.sprite = icon;
                iconImg.raycastTarget = false;
            }

            return button;
        }

        static void CreateTabButton(Transform parent, string name, Sprite icon, string label, int index, int count)
        {
            float t0 = index / (float)count, t1 = (index + 1) / (float)count;
            var rt = AnchorRect(parent, name, new Vector2(t0, 0f), new Vector2(t1, 1f));

            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.001f);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.16f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            if (icon != null)
            {
                var iconRt = AnchorPoint(rt, "Icon", 0.5f, 0.62f, 30f, 30f);
                var iconImg = iconRt.gameObject.AddComponent<Image>();
                iconImg.sprite = icon;
                iconImg.raycastTarget = false;
            }

            var labelRt = AnchorRect(rt, "Label", new Vector2(0f, 0.06f), new Vector2(1f, 0.34f));
            var tmp = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 15;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
        }
    }
}

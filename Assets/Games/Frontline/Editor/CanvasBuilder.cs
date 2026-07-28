using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;

namespace Frontline.EditorTools
{
    /// <summary>
    /// Builds the real (uGUI/TextMeshPro) UI, styled with the Kenney UI Pack, as part of the
    /// generated scene -- same "build artifact, not hand-assembled" contract as everything else
    /// SceneBuilder makes.
    ///
    /// Every screen (Menu, Paused, Settings, Shop, Upgrades, Ranks, the in-run pause button, the
    /// Death screen) is built here, structure only. Click behaviour and anything that changes at
    /// runtime (the sound toggle's sprite, the Ranks list, Death's score readout) is wired at
    /// Awake by GameUI/GameManager finding these objects by name -- a Button's onClick listener
    /// is a C# delegate, and delegates don't survive EditorSceneManager.SaveScene, so only the
    /// sprites/anchors/text serialize.
    ///
    /// The weapon codex is the one exception that's fully baked here: WeaponDef data is static
    /// compile-time data, so there's nothing to wire -- the cards are real, final content the
    /// moment the scene is generated, same as everything else SceneBuilder builds from source.
    /// </summary>
    public static class CanvasBuilder
    {
        const string UiRoot = "Assets/Games/Frontline/Art/UI"; // repointed at graduation (2026-07-27)

        public static void Build()
        {
            // Without this, nothing here is clickable: GraphicRaycaster only finds *what* was
            // hit, an EventSystem is what actually turns a click/tap into "send it a message".
            // This project has never had one -- there's been no Canvas, no Button, until now.
            // The project is Input System-only (activeInputHandler: 1, see InputReader.cs), so
            // it's InputSystemUIInputModule, not the legacy StandaloneInputModule -- the legacy
            // module reads UnityEngine.Input, which is disabled entirely in this mode and would
            // silently never fire. Added with no actions assigned: InputSystemUIInputModule
            // auto-generates its default point/click bindings (mouse + touch) in OnEnable
            // whenever it finds none, which only happens for a component added purely in code
            // like this -- see its AssignDefaultActions() doc comment.
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            GameObject canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // 540x960: the exact reference the IMGUI HUD has used all along (GameManager.UiScale),
            // so button positions ported from the old Rect() math land in the same place.
            scaler.referenceResolution = new Vector2(540, 960);
            scaler.matchWidthOrHeight = 0.5f;

            Transform root = canvasGo.transform;
            BuildMenuBackground(root);
            BuildMainMenu(root);
            BuildPaused(root);
            BuildSettings(root);
            BuildShop(root);
            BuildLeaderboard(root);
            BuildUpgrades(root);
            BuildPauseButton(root);
            BuildDeathScreen(root);
            // Built last -> highest sibling index -> renders on top of every tabbed screen's own
            // content, so the persistent chrome never gets buried under a scroll view or a card.
            BuildShell(root);
        }

        /// <summary>
        /// One shared backdrop behind every screen canvas (sibling index 0, so it renders behind
        /// all of them) instead of each screen dimming the live 3D battlefield behind it -- see
        /// ProceduralMenuBackground for why. GameUI toggles this object exactly like any other
        /// screen canvas (active whenever Screen != Screen_.Playing).
        /// </summary>
        static void BuildMenuBackground(Transform canvasRoot)
        {
            var root = NewRect("MenuBackground", canvasRoot);
            Stretch(root);
            var img = root.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            root.gameObject.AddComponent<ProceduralMenuBackground>();
        }

        /// <summary>
        /// Persistent chrome shown on every tabbed screen (Menu/Shop/Upgrades/Ranks): a top bar
        /// (settings gear, a "lives" badge, Supply currency) and a bottom tab bar to switch
        /// between them. Replaces the old layout -- a SETTINGS button and a SHOP/UPGRADE/RANKS
        /// row baked into the Main Menu only, plus a separate BACK button on each of the other
        /// three screens -- with the single always-visible shell every real reference in the
        /// genre uses, per Simon's "like a real proper good looking Ui" feedback on the first
        /// pass. GameUI shows/hides this exactly like MenuBackground and tints whichever tab
        /// matches the current Screen_.
        ///
        /// The "lives" badge is deliberately static, not wired to any save data: Frontline has
        /// no energy/ticket gate on playing at all (per-run lives are a completely separate,
        /// in-run health stat), and the badge says so outright -- a small, direct rebuttal to the
        /// ad-funnel games this genre is discovered through, which almost always meter attempts.
        /// </summary>
        static void BuildShell(Transform canvasRoot)
        {
            var root = NewRect("Shell", canvasRoot);
            Stretch(root);

            // ---- Top bar: library (back to PocketVerse) -- gear -- lives badge -- Supply currency ----
            const float topBarH = 96f;
            var topBar = NewRect("TopBar", root);
            StretchTop(topBar, topBarH);

            // Graduation-only affordance (2026-07-28): the only way off this screen back to the
            // hub's own Home used to be the hardware/gesture Back button, invisible unless you
            // already knew it was there. Plain "X" rather than an icon -- no home/back glyph
            // exists in this project's Kenney UI Pack subset, and "X" matches the button
            // FlowSort already uses for the exact same "return to PocketVerse" action elsewhere
            // in the app, so the two graduated games read as one consistent UI language instead
            // of two different conventions for the same thing.
            CreateIconTextButton(topBar, "LibraryButton", "X", 0.06f, 0.5f, 44f);

            CreateIconButton(topBar, "SettingsGearButton", Spr("Icons", "gear"), 0.175f, 0.5f, 44f);

            var livesPill = CreatePillPanel(topBar, "LivesPill", 0.42f, 0.5f, 196f, 52f);
            var heartIconRt = AnchorPoint(livesPill, "HeartIcon", 0.16f, 0.5f, 30f, 30f);
            heartIconRt.gameObject.AddComponent<Image>().raycastTarget = false;
            heartIconRt.gameObject.AddComponent<ProceduralHeartIcon>();
            var livesTmp = AnchorRect(livesPill, "Label", new Vector2(0.32f, 0f), new Vector2(0.97f, 1f))
                .gameObject.AddComponent<TextMeshProUGUI>();
            livesTmp.text = "UNLIMITED";
            livesTmp.fontSize = 15;
            livesTmp.fontStyle = FontStyles.Bold;
            livesTmp.alignment = TextAlignmentOptions.Left;
            livesTmp.enableWordWrapping = false;
            livesTmp.overflowMode = TextOverflowModes.Overflow;
            livesTmp.color = new Color(0.15f, 0.15f, 0.18f);
            livesTmp.raycastTarget = false;

            var supplyPill = CreatePillPanel(topBar, "SupplyPill", 0.75f, 0.5f, 130f, 52f);
            var coinIconRt = AnchorPoint(supplyPill, "CoinIcon", 0.20f, 0.5f, 28f, 28f);
            var coinImg = coinIconRt.gameObject.AddComponent<Image>();
            coinImg.sprite = Spr("Icons", "coinPlaceholder");
            coinImg.color = new Color(1f, 0.82f, 0.15f);
            coinImg.raycastTarget = false;
            var supplyTmp = AnchorRect(supplyPill, "Value", new Vector2(0.38f, 0f), new Vector2(0.95f, 1f))
                .gameObject.AddComponent<TextMeshProUGUI>();
            supplyTmp.text = "0";
            supplyTmp.fontSize = 20;
            supplyTmp.fontStyle = FontStyles.Bold;
            supplyTmp.alignment = TextAlignmentOptions.Left;
            supplyTmp.color = new Color(0.15f, 0.15f, 0.18f);
            supplyTmp.raycastTarget = false;

            // ---- Bottom tab bar: Home / Shop / Upgrade / Ranks ----
            const float tabBarH = 118f;
            var tabBar = NewRect("BottomTabBar", root);
            StretchBottom(tabBar, tabBarH);
            var tabBarImg = tabBar.gameObject.AddComponent<Image>();
            tabBarImg.color = new Color(0f, 0f, 0f, 0.35f);

            CreateTabButton(tabBar, "HomeTab", null, "HOME", 0, 4);
            CreateTabButton(tabBar, "ShopTab", Spr("Icons", "shoppingCart"), "SHOP", 1, 4);
            CreateTabButton(tabBar, "UpgradeTab", Spr("Icons", "target"), "UPGRADE", 2, 4);
            CreateTabButton(tabBar, "RanksTab", Spr("Icons", "trophy"), "RANKS", 3, 4);
        }

        static void BuildMainMenu(Transform canvasRoot)
        {
            var root = NewRect("MainMenuCanvas", canvasRoot);
            Stretch(root);

            AddTitle(root, "FRONTLINE", 153f, 56);

            // PLAY: the one button that has to look unmistakably like THE button -- greenish
            // blue, biggest, on its own. Shown only before any checkpoint exists.
            CreateButton(root, "PlayButton", "PLAY", ButtonSprite("Blue"), 0f, 326f, 300f, 91f, 34);

            // Once a checkpoint exists (a boss has been beaten -- see GameManager.OnBossKilled),
            // this pair replaces PlayButton instead: CONTINUE resumes from the checkpoint stage,
            // NEW GAME keeps today's Stage-1 behaviour so early stages stay farmable. Which pair
            // is actually visible is a runtime decision (GameUI.WireMainMenu) -- SaveData doesn't
            // exist at scene-build time.
            CreateButton(root, "ContinueButton", "CONTINUE", ButtonSprite("Blue"), 0f, 326f, 300f, 76f, 28);
            CreateButton(root, "NewGameButton", "NEW GAME", ButtonSprite("Grey"), 0f, 412f, 300f, 56f, 20);
        }

        static void BuildPaused(Transform canvasRoot)
        {
            var root = NewRect("PausedCanvas", canvasRoot);
            Stretch(root);
            AddTitle(root, "PAUSED", 192f, 48);

            float w = 300f, h = 72f, gap = 12f, y = 346f;
            CreateButton(root, "ResumeButton", "RESUME", ButtonSprite("Blue"), 0f, y, w, h, 28);
            y += h + gap;
            CreateButton(root, "RestartButton", "RESTART", ButtonSprite("Grey"), 0f, y, w, h, 28);
            y += h + gap;
            CreateButton(root, "PausedSettingsButton", "SETTINGS", ButtonSprite("Grey"), 0f, y, w, h, 28);
            y += h + gap;
            CreateButton(root, "PausedMenuButton", "MAIN MENU", ButtonSprite("Grey"), 0f, y, w, h, 28);
        }

        static void BuildSettings(Transform canvasRoot)
        {
            var root = NewRect("SettingsCanvas", canvasRoot);
            Stretch(root);
            AddTitle(root, "SETTINGS", 173f, 44);

            float w = 380f, h = 64f, gap = 10f, y = 326f;

            // Sound is real -- a checkbox you can actually flip, wired to Audio.Muted at
            // runtime (see GameUI.WireSettings). Its sprite starts unchecked here; GameUI sets
            // the true state as soon as the scene loads.
            var soundRow = CreatePanel(root, "SoundPanel", 0f, y, w, h);
            AddRowLabel(soundRow, "SOUND", 22, 0.7f);
            CreateToggle(soundRow, "SoundToggle", 0.85f, 40f);

            y += h + gap;
            // Honest placeholders, not fake toggles -- there is no music or vibration yet.
            var musicRow = CreatePanel(root, "MusicPanel", 0f, y, w, h);
            AddRowLabel(musicRow, "MUSIC   (none yet)", 18, 0.94f, dim: true);

            y += h + gap;
            var vibeRow = CreatePanel(root, "VibrationPanel", 0f, y, w, h);
            AddRowLabel(vibeRow, "VIBRATION   (todo)", 18, 0.94f, dim: true);

            y += h + 30f;
            CreateButton(root, "SettingsBackButton", "BACK", ButtonSprite("Grey"), 0f, y, w, h, 26);
        }

        /// <summary>
        /// The real economy hub: spend Supply to unlock/equip heroes and unlock/upgrade
        /// weapons. Structure (a row per hero and per weapon, names baked in) is built here; the
        /// locked/unlocked/equipped state, cost, and affordability tint are all runtime
        /// (GameUI.RefreshShop) since they depend on SaveData, which doesn't exist yet at
        /// scene-build time.
        ///
        /// Heroes + weapons together don't fit one screen, so this is the first CanvasBuilder
        /// screen to scroll -- everything else so far has fit in fixed Y offsets.
        /// </summary>
        static void BuildShop(Transform canvasRoot)
        {
            var root = NewRect("ShopCanvas", canvasRoot);
            Stretch(root);
            AddTitle(root, "SHOP", 132f, 34);

            // Supply is shown persistently in the top bar's pill now (see BuildShell) -- no need
            // for a second readout in here.

            const float rowW = 460f, rowH = 84f, gap = 10f, headerH = 30f;
            float contentH = headerH + Heroes.All.Length * (rowH + gap)
                            + headerH + Weapons.Pickups.Length * (rowH + gap) + 10f;

            RectTransform content = CreateScrollView(root, "ScrollView", 178f, 610f, rowW, contentH);

            float y = 0f;
            AddSectionHeader(content, "HEROES", y);
            y += headerH;
            foreach (HeroDef hero in Heroes.All)
            {
                BuildShopHeroRow(content, hero, y, rowW, rowH);
                y += rowH + gap;
            }

            AddSectionHeader(content, "WEAPONS", y);
            y += headerH;
            foreach (WeaponDef def in Weapons.Pickups)
            {
                BuildShopRow(content, def, y, rowW, rowH);
                y += rowH + gap;
            }
        }

        static void AddSectionHeader(Transform parent, string text, float yFromTop)
        {
            var rt = NewRect($"Header_{text}", parent);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 28f);
            rt.anchoredPosition = new Vector2(0f, -yFromTop);

            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 20;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = new Color(1f, 1f, 1f, 0.85f);
            tmp.raycastTarget = false;
        }

        /// <summary>
        /// A vertically scrolling list: ScrollRect + a masked Viewport + a Content rect sized to
        /// fit everything (computed by the caller, not a ContentSizeFitter -- every row here is
        /// placed at an explicit Y offset already, so the total height is just arithmetic).
        /// Returns Content; callers parent rows to it exactly like any other screen's root.
        /// </summary>
        static RectTransform CreateScrollView(Transform parent, string name, float yFromTop,
                                              float viewportH, float w, float contentH)
        {
            var root = NewRect(name, parent);
            PlaceTopCenter(root, 0f, yFromTop, w, viewportH);

            var scrollRect = root.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;

            var viewport = NewRect("Viewport", root);
            Stretch(viewport);
            // RectMask2D needs a Graphic on the same object to clip against; fully transparent,
            // so it clips without being visible itself.
            var viewportImg = viewport.gameObject.AddComponent<Image>();
            viewportImg.color = new Color(1f, 1f, 1f, 0.001f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, contentH);
            content.anchoredPosition = Vector2.zero;

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            return content;
        }

        static void BuildShopHeroRow(Transform parent, HeroDef hero, float yFromTop, float w, float h)
        {
            var row = CreatePanel(parent, $"ShopHero_{hero.Id}", 0f, yFromTop, w, h);

            var name = AnchorRect(row, "Name", new Vector2(0.05f, 0.5f), new Vector2(0.5f, 0.95f));
            var nameTmp = name.gameObject.AddComponent<TextMeshProUGUI>();
            nameTmp.text = hero.DisplayName;
            nameTmp.fontSize = 22;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.Left;
            nameTmp.color = new Color(0.15f, 0.15f, 0.18f);
            nameTmp.raycastTarget = false;

            var status = AnchorRect(row, "Status", new Vector2(0.05f, 0.06f), new Vector2(0.5f, 0.48f));
            var statusTmp = status.gameObject.AddComponent<TextMeshProUGUI>();
            statusTmp.text = hero.Trait;
            statusTmp.fontSize = 14;
            statusTmp.enableWordWrapping = false;
            statusTmp.overflowMode = TextOverflowModes.Overflow;
            statusTmp.alignment = TextAlignmentOptions.Left;
            statusTmp.color = new Color(0.3f, 0.3f, 0.34f);
            statusTmp.raycastTarget = false;

            var actionRect = AnchorRect(row, "Action", new Vector2(0.55f, 0.14f), new Vector2(0.94f, 0.86f));
            var img = actionRect.gameObject.AddComponent<Image>();
            img.sprite = ButtonSprite("Blue");
            img.type = Image.Type.Sliced;
            var button = actionRect.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            var affordance = actionRect.gameObject.AddComponent<ShopActionButton>();
            var so = new SerializedObject(affordance);
            so.FindProperty("_affordable").objectReferenceValue = ButtonSprite("Blue");
            so.FindProperty("_unaffordable").objectReferenceValue = ButtonSprite("Grey");
            so.ApplyModifiedProperties();

            var actionLabelRt = NewRect("Label", actionRect);
            Stretch(actionLabelRt);
            var actionTmp = actionLabelRt.gameObject.AddComponent<TextMeshProUGUI>();
            actionTmp.text = "";
            actionTmp.fontSize = 15;
            actionTmp.fontStyle = FontStyles.Bold;
            actionTmp.alignment = TextAlignmentOptions.Center;
            actionTmp.color = new Color32(40, 40, 45, 255);
            actionTmp.raycastTarget = false;
        }

        static void BuildShopRow(Transform parent, WeaponDef def, float yFromTop, float w, float h)
        {
            var row = CreatePanel(parent, $"ShopRow_{def.Mesh}", 0f, yFromTop, w, h);

            var name = AnchorRect(row, "Name", new Vector2(0.05f, 0.5f), new Vector2(0.5f, 0.95f));
            var nameTmp = name.gameObject.AddComponent<TextMeshProUGUI>();
            nameTmp.text = def.DisplayName;
            nameTmp.fontSize = 22;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.Left;
            nameTmp.color = new Color(0.15f, 0.15f, 0.18f);
            nameTmp.raycastTarget = false;

            var status = AnchorRect(row, "Status", new Vector2(0.05f, 0.06f), new Vector2(0.5f, 0.48f));
            var statusTmp = status.gameObject.AddComponent<TextMeshProUGUI>();
            statusTmp.text = "";
            statusTmp.fontSize = 15;
            statusTmp.enableWordWrapping = false;
            statusTmp.overflowMode = TextOverflowModes.Overflow;
            statusTmp.alignment = TextAlignmentOptions.Left;
            statusTmp.color = new Color(0.3f, 0.3f, 0.34f);
            statusTmp.raycastTarget = false;

            // The action button (BUY/UPGRADE/MAX) -- its own nested Kenney button, sprite and
            // label both refreshed at runtime since what it says and whether it's affordable
            // both depend on SaveData.
            var actionRect = AnchorRect(row, "Action", new Vector2(0.55f, 0.14f), new Vector2(0.94f, 0.86f));
            var img = actionRect.gameObject.AddComponent<Image>();
            img.sprite = ButtonSprite("Blue");
            img.type = Image.Type.Sliced;
            var button = actionRect.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            var affordance = actionRect.gameObject.AddComponent<ShopActionButton>();
            var so = new SerializedObject(affordance);
            so.FindProperty("_affordable").objectReferenceValue = ButtonSprite("Blue");
            so.FindProperty("_unaffordable").objectReferenceValue = ButtonSprite("Grey");
            so.ApplyModifiedProperties();

            var actionLabelRt = NewRect("Label", actionRect);
            Stretch(actionLabelRt);
            var actionTmp = actionLabelRt.gameObject.AddComponent<TextMeshProUGUI>();
            actionTmp.text = "";
            actionTmp.fontSize = 15;
            actionTmp.fontStyle = FontStyles.Bold;
            actionTmp.alignment = TextAlignmentOptions.Center;
            actionTmp.color = new Color32(40, 40, 45, 255);
            actionTmp.raycastTarget = false;
        }

        /// <summary>
        /// Real content, not a fake stub: local high scores, persisted by GameManager and
        /// displayed here. Eight fixed rows, populated at runtime (GameUI.RefreshLeaderboard)
        /// since the scores obviously can't be known at scene-build time.
        /// </summary>
        static void BuildLeaderboard(Transform canvasRoot)
        {
            var root = NewRect("LeaderboardCanvas", canvasRoot);
            Stretch(root);
            AddTitle(root, "RANKS", 150f, 40);

            var panel = CreatePanel(root, "RanksPanel", 0f, 230f, 420f, 560f);
            const int rows = 8;
            float rowH = 1f / rows;
            for (int i = 0; i < rows; i++)
            {
                var rowRect = AnchorRect(panel, $"Row{i}",
                    new Vector2(0.08f, 1f - (i + 1) * rowH), new Vector2(0.92f, 1f - i * rowH));
                var tmp = rowRect.gameObject.AddComponent<TextMeshProUGUI>();
                tmp.text = "";
                tmp.fontSize = 24;
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.color = i == 0 ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 1f, 1f, 0.85f);
                tmp.fontStyle = i == 0 ? FontStyles.Bold : FontStyles.Normal;
                tmp.raycastTarget = false;
            }
        }

        /// <summary>
        /// The weapon codex, fully real: WeaponDef is static compile-time data (see Weapons.cs),
        /// so unlike Ranks there's nothing to wire at runtime -- every card's numbers are baked
        /// in here, the same way SceneBuilder bakes in props and gun sounds.
        /// </summary>
        static void BuildUpgrades(Transform canvasRoot)
        {
            var root = NewRect("UpgradesCanvas", canvasRoot);
            Stretch(root);
            AddTitle(root, "WEAPONS", 142f, 36);

            float y = 222f, cardW = 460f, cardH = 88f, gap = 10f;
            foreach (WeaponDef def in Weapons.Pickups)
            {
                BuildWeaponCard(root, def, y, cardW, cardH);
                y += cardH + gap;
            }
        }

        static void BuildWeaponCard(Transform parent, WeaponDef def, float yFromTop, float w, float h)
        {
            var card = CreatePanel(parent, $"Card_{def.Mesh}", 0f, yFromTop, w, h);

            var name = AnchorRect(card, "Name", new Vector2(0.05f, 0.52f), new Vector2(0.6f, 0.92f));
            var nameTmp = name.gameObject.AddComponent<TextMeshProUGUI>();
            nameTmp.text = def.DisplayName;
            nameTmp.fontSize = 24;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.Left;
            nameTmp.color = new Color(0.15f, 0.15f, 0.18f);
            nameTmp.raycastTarget = false;

            if (def.Trait != null)
            {
                var trait = AnchorRect(card, "Trait", new Vector2(0.05f, 0.68f), new Vector2(0.78f, 0.92f));
                var traitTmp = trait.gameObject.AddComponent<TextMeshProUGUI>();
                traitTmp.text = def.Trait;
                traitTmp.fontSize = 15;
                traitTmp.alignment = TextAlignmentOptions.Right;
                traitTmp.color = new Color(0.15f, 0.35f, 0.65f);
                traitTmp.raycastTarget = false;
            }

            // Lock/level state -- runtime only (GameUI.RefreshUpgrades), this card is otherwise
            // fully baked at scene-build time since WeaponDef itself never changes.
            var badge = AnchorRect(card, "Badge", new Vector2(0.78f, 0.68f), new Vector2(0.95f, 0.92f));
            var badgeTmp = badge.gameObject.AddComponent<TextMeshProUGUI>();
            badgeTmp.text = "";
            badgeTmp.fontSize = 14;
            badgeTmp.fontStyle = FontStyles.Bold;
            badgeTmp.alignment = TextAlignmentOptions.Right;
            badgeTmp.raycastTarget = false;

            var stat = AnchorRect(card, "Stat", new Vector2(0.05f, 0.10f), new Vector2(0.58f, 0.50f));
            var statTmp = stat.gameObject.AddComponent<TextMeshProUGUI>();
            float dps = def.Dps;
            float rate = 1f / def.FireInterval;
            statTmp.text = $"DPS {dps:F0}   RATE {rate:F0}/s";
            statTmp.fontSize = 15;
            statTmp.alignment = TextAlignmentOptions.Left;
            statTmp.color = new Color(0.3f, 0.3f, 0.34f);
            statTmp.raycastTarget = false;

            // Range bar -- the load-bearing stat that turns the table into a real decision.
            // Plain coloured Images, no sprite needed: a null-sprite Image already renders as a
            // solid rect (see MainMenu's Dim), same trick, just a small nested one here.
            var track = AnchorRect(card, "RangeTrack", new Vector2(0.62f, 0.16f), new Vector2(0.95f, 0.30f));
            track.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.12f);

            float rangeT = Mathf.Clamp01(def.Range / 26f);   // 26 = sniper, the longest in the table
            var fill = AnchorRect(track, "RangeFill", Vector2.zero, new Vector2(rangeT, 1f));
            fill.gameObject.AddComponent<Image>().color = new Color(0.25f, 0.55f, 0.85f);
        }

        /// <summary>
        /// The in-run pause button, top-right. Only ever visible during Screen_.Playing (see
        /// GameUI.RefreshCanvasVisibility) -- the HUD owns the top-left, and a thumb reaching
        /// for pause shouldn't cross the lane the player is steering in.
        /// </summary>
        static void BuildPauseButton(Transform canvasRoot)
        {
            var root = NewRect("PauseButtonCanvas", canvasRoot);
            Stretch(root);

            var rt = NewRect("PauseButton", root);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(58f, 58f);
            rt.anchoredPosition = new Vector2(-14f, -14f);

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = SquareButtonSprite("Grey");
            img.type = Image.Type.Sliced;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            var label = NewRect("Label", rt);
            Stretch(label);
            var tmp = label.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = "II";
            tmp.fontSize = 24;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.2f, 0.2f, 0.22f);
            tmp.raycastTarget = false;
        }

        /// <summary>
        /// Score/Stage/Level text is baked empty here and filled by GameManager at the moment
        /// the run ends -- unlike the weapon codex, this genuinely can't be known until then.
        /// </summary>
        static void BuildDeathScreen(Transform canvasRoot)
        {
            var root = NewRect("DeathCanvas", canvasRoot);
            Stretch(root);

            float y = 269f;
            var title = AddTitle(root, "OVERRUN", y, 58);
            title.color = new Color(1f, 0.4f, 0.35f);

            AddCenteredLabel(root, "ScoreText", "", y + 90f, 26);
            AddCenteredLabel(root, "StageText", "", y + 126f, 26);
            var supplyText = AddCenteredLabel(root, "SupplyText", "", y + 162f, 24);
            supplyText.color = new Color(1f, 0.82f, 0.15f);   // gold -- reads as a reward, not a stat
            AddSupplyIcon(root, y + 162f, -90f, 24f);

            CreateButton(root, "RestartButton", "RESTART", ButtonSprite("Blue"), 0f, y + 215f, 260f, 76f, 30);
            CreateButton(root, "MenuButton", "MAIN MENU", ButtonSprite("Grey"), 0f, y + 305f, 260f, 66f, 26);
        }

        // ---- helpers ----

        static Sprite Spr(string folder, string file) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{UiRoot}/{folder}/{file}.png");

        static Sprite ButtonSprite(string color) => Spr(color, "button_rectangle_depth_gradient");
        static Sprite SquareButtonSprite(string color) => Spr(color, "button_square_depth_gradient");
        static Sprite ExtraSprite(string name) => Spr("Extra", name);

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Fills the parent completely -- for full-screen backgrounds.</summary>
        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Full parent width, fixed height, pinned to the top edge -- for the top bar.
        /// Width follows the parent fractionally rather than a fixed unit count, since the
        /// canvas's actual local width isn't reliably the 540 reference (CanvasScaler blends
        /// width/height against the device's real aspect, see Build()'s matchWidthOrHeight).</summary>
        static RectTransform StretchTop(RectTransform rt, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        /// <summary>Same as StretchTop but pinned to the bottom edge -- for the tab bar.</summary>
        static RectTransform StretchBottom(RectTransform rt, float height)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        /// <summary>Fractional sub-rect of whatever parent it's given -- works regardless of how
        /// that parent itself was positioned, since anchors are always relative to the immediate
        /// parent's own box.</summary>
        static RectTransform AnchorRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Fixed-size box centred on a fractional anchor point within the parent.</summary>
        static RectTransform AnchorPoint(Transform parent, string name, float ax, float ay, float w, float h)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        /// <summary>
        /// Top-centre anchored, positioned by distance-from-top -- deliberately mirrors the old
        /// IMGUI Rect(x, yFromTop, w, h) convention so every number ported from the old Draw*
        /// methods lands in the same place without re-deriving the layout.
        /// </summary>
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

        static TextMeshProUGUI AddCenteredLabel(Transform parent, string name, string text, float yFromTop, int fontSize)
        {
            var rt = NewRect(name, parent);
            PlaceTopCenter(rt, 0f, yFromTop, 540f, 40f);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>A settings-row label, anchored inside its panel by a fractional split point.</summary>
        static void AddRowLabel(Transform panel, string text, int fontSize, float rightEdge, bool dim = false)
        {
            var rt = AnchorRect(panel, "Label", new Vector2(0.06f, 0f), new Vector2(rightEdge, 1f));
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = dim ? new Color(0.2f, 0.2f, 0.22f, 0.55f) : new Color(0.15f, 0.15f, 0.18f);
            tmp.raycastTarget = false;
        }

        /// <summary>A label centred inside an arbitrary panel (Shop's "COMING SOON" card).</summary>
        static void AddPanelLabel(Transform panel, string name, string text, int fontSize, float centerY, bool dim = false)
        {
            var rt = AnchorRect(panel, name, new Vector2(0.05f, centerY - 0.1f), new Vector2(0.95f, centerY + 0.1f));
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = dim ? new Color(0.2f, 0.2f, 0.22f, 0.6f) : new Color(0.15f, 0.15f, 0.18f);
            tmp.raycastTarget = false;
        }

        static void AddIcon(Transform panel, string name, Sprite sprite, float ax, float ay, float size)
        {
            var rt = AnchorPoint(panel, name, ax, ay, size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.raycastTarget = false;
        }

        /// <summary>
        /// Placeholder for a currency icon -- no Kenney pack (checked: base Game Icons,
        /// RPG Expansion, Board Game Icons) actually ships a coin/gem, so this reuses the RPG
        /// Expansion's plain circle badge, gold-tinted to match SupplyText, sat just left of
        /// where the (centered, variable-width) Supply label sits. xOffset is a hand-guessed
        /// gap, not measured against real text width -- nudge it once this is actually visible.
        /// Swap Spr("Icons", "coinPlaceholder") for a real coin sprite in one line if one ever
        /// turns up that matches the art style.
        /// </summary>
        static void AddSupplyIcon(Transform parent, float yFromTop, float xOffset, float size)
        {
            var rt = NewRect("SupplyIcon", parent);
            PlaceTopCenter(rt, xOffset, yFromTop, size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Spr("Icons", "coinPlaceholder");
            img.type = Image.Type.Simple;
            img.color = new Color(1f, 0.82f, 0.15f);
            img.raycastTarget = false;
        }

        /// <summary>Panel background: the Kenney "input" container, 9-sliced so it stretches to
        /// any row/card size without warping its rounded corners.</summary>
        static RectTransform CreatePanel(Transform parent, string name, float x, float yFromTop, float w, float h)
        {
            var rt = NewRect(name, parent);
            PlaceTopCenter(rt, x, yFromTop, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = ExtraSprite("input_rectangle");
            img.type = Image.Type.Sliced;
            return rt;
        }

        /// <summary>
        /// A checkbox button. Both sprites are baked in via CheckboxToggle/SerializedObject --
        /// not loaded by path at runtime, which would mean AssetDatabase, which is Editor-only
        /// and silently returns null in an actual build. GameUI sets the real on/off state from
        /// Audio.Muted the moment the scene loads and again on every click (WireSettings).
        /// </summary>
        static void CreateToggle(Transform panel, string name, float ax, float size)
        {
            var rt = AnchorPoint(panel, name, ax, 0.5f, size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Spr("Grey", "check_square_grey");
            img.type = Image.Type.Simple;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;

            var toggle = rt.gameObject.AddComponent<CheckboxToggle>();
            var so = new SerializedObject(toggle);
            so.FindProperty("_off").objectReferenceValue = Spr("Grey", "check_square_grey");
            so.FindProperty("_on").objectReferenceValue = Spr("Grey", "check_square_color_checkmark");
            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// A Kenney-sprite Button: 9-sliced Image (so it resizes without warping the rounded
        /// corners/shadow) + a colour-tint press transition (no separate "pressed" sprite was
        /// imported, so ColorTint darkening on press is what sells the click) + a centred label.
        /// </summary>
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
            colors.fadeDuration = 0.05f;   // snappy, not a slow fade -- this is an auto-firer game
            button.colors = colors;

            var labelRt = NewRect("Label", rt);
            Stretch(labelRt);
            var tmp = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color32(40, 40, 45, 255);   // dark, not pure black -- softer against the pastel buttons
            tmp.raycastTarget = false;   // clicks go to the button's Image, not through the label

            return button;
        }

        /// <summary>A square icon-only button (the top bar's settings gear) -- fractionally
        /// positioned within its parent, unlike CreateButton's absolute x/yFromTop, so it stays
        /// put regardless of the canvas's actual local width on a given device.</summary>
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

            AddIcon(rt, "Icon", icon, 0.5f, 0.5f, size * 0.52f);
            return button;
        }

        /// <summary>Same square button as CreateIconButton, but a short text glyph instead of a
        /// sprite -- for cases like the top bar's "X" back-to-library button where no matching
        /// icon exists in this project's Kenney UI Pack subset.</summary>
        static Button CreateIconTextButton(Transform parent, string name, string text, float ax, float ay, float size)
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

            var labelRt = NewRect("Label", rt);
            Stretch(labelRt);
            var tmp = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size * 0.42f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color32(40, 40, 45, 255);
            tmp.raycastTarget = false;

            return button;
        }

        /// <summary>A rounded pill panel (top bar's lives/Supply badges) -- fractionally
        /// positioned, same reasoning as CreateIconButton.</summary>
        static RectTransform CreatePillPanel(Transform parent, string name, float ax, float ay, float w, float h)
        {
            var rt = AnchorPoint(parent, name, ax, ay, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = ExtraSprite("input_rectangle");
            img.type = Image.Type.Sliced;
            return rt;
        }

        /// <summary>One quarter-width slot of the bottom tab bar: an icon (optional -- Home has
        /// none, see BuildShell), a label below it, and a Button covering the whole cell so the
        /// label doesn't need to be the tap target. GameUI tints Icon/Label between the active
        /// and inactive colours based on the current Screen_.</summary>
        static void CreateTabButton(Transform parent, string name, Sprite icon, string label, int index, int count)
        {
            float t0 = index / (float)count, t1 = (index + 1) / (float)count;
            var rt = AnchorRect(parent, name, new Vector2(t0, 0f), new Vector2(t1, 1f));

            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.001f); // invisible, just a raycast target
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

            var labelRt = AnchorRect(rt, "Label", new Vector2(0f, 0.06f), new Vector2(1f, icon != null ? 0.34f : 0.7f));
            var tmp = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 15;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }
    }
}

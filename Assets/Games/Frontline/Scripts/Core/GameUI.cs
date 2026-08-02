using UnityEngine;
using UnityEngine.UI;

namespace Frontline
{
    public enum Screen_
    {
        Menu,
        Playing,
        Paused,
        Settings,
        Shop,
        Upgrades,
        Leaderboard,
        Dead,
    }

    /// <summary>
    /// The shell: menu, pause, settings, and the shop/upgrades/leaderboard tabs. Every screen is
    /// real uGUI now (CanvasBuilder, Kenney-styled) -- structure comes from the generated scene,
    /// click behaviour is wired here at Awake by finding each screen's buttons by name, since a
    /// Button's onClick listener is a C# delegate and delegates don't survive
    /// EditorSceneManager.SaveScene. Same "structure from the generator, behaviour by name at
    /// runtime" contract WeaponGate/Hurdle already use for their prefab's named children.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public static GameUI Instance { get; private set; }
        public Screen_ Screen { get; private set; } = Screen_.Menu;

        Screen_ _returnTo = Screen_.Menu;

        /// <summary>
        /// Survives the scene reload that a restart performs. Static because everything else in
        /// the scene, this component included, is destroyed and rebuilt by LoadScene.
        /// </summary>
        static bool _startImmediately;

        /// <summary>Has this scene instance already had a run in it? Then PLAY must reload.</summary>
        bool _dirty;

        GameObject _menuBackground;
        GameObject _shell;
        GameObject _menuCanvas;
        GameObject _pausedCanvas;
        GameObject _settingsCanvas;
        GameObject _shopCanvas;
        GameObject _upgradesCanvas;
        GameObject _leaderboardCanvas;
        GameObject _pauseButtonCanvas;
        GameObject _gameplayHud;

        CheckboxToggle _soundToggle;
        TMPro.TextMeshProUGUI _topBarSupplyText;

        // (tab, its Screen_, icon, label) -- walked by RefreshTabHighlight to tint whichever one
        // matches the current Screen_ and dim the rest, and by WireShell to hook up each click.
        (GameObject tab, Screen_ screen, Image icon, TMPro.TextMeshProUGUI label)[] _tabs;

        void Awake()
        {
            Instance = this;
            // AutoPilot has no thumb and can't press PLAY, so it starts the run itself.
            bool auto = AutoPilot.Enabled || _startImmediately;
            _startImmediately = false;
            Screen = auto ? Screen_.Playing : Screen_.Menu;
            _dirty = auto;
            ApplyTimeScale();

            _menuBackground = Find("MenuBackground");
            _gameplayHud = Find("GameplayHud");
            WireShell();
            WireMainMenu();
            WirePaused();
            WireSettings();
            WireShop();
            WireUpgrades();
            WireLeaderboard();
            WirePauseButton();
            RefreshTopBarSupply();
            RefreshCanvasVisibility();
        }

        /// <summary>The persistent top bar + bottom tab bar CanvasBuilder.BuildShell built --
        /// see its doc comment for why this replaced a SETTINGS button and a SHOP/UPGRADE/RANKS
        /// row baked into the Main Menu plus a separate BACK button on every other screen.</summary>
        void WireShell()
        {
            GameObject c = Find("Shell");
            _shell = c;
            if (c == null) return;
            UIWire.Click(c, "TopBar/LibraryButton", () => GameManager.ExitToHubOverride?.Invoke());
            UIWire.Click(c, "TopBar/SettingsGearButton", () => { _returnTo = Screen; Go(Screen_.Settings); });

            _topBarSupplyText = c.transform.Find("TopBar/SupplyPill/Value")?.GetComponent<TMPro.TextMeshProUGUI>();

            _tabs = new (GameObject, Screen_, Image, TMPro.TextMeshProUGUI)[]
            {
                (Find2(c, "BottomTabBar/HomeTab"), Screen_.Menu, null, null),
                (Find2(c, "BottomTabBar/ShopTab"), Screen_.Shop, null, null),
                (Find2(c, "BottomTabBar/UpgradeTab"), Screen_.Upgrades, null, null),
                (Find2(c, "BottomTabBar/RanksTab"), Screen_.Leaderboard, null, null),
            };
            for (int i = 0; i < _tabs.Length; i++)
            {
                var (tab, screen, _, _) = _tabs[i];
                if (tab == null) continue;
                Screen_ target = screen; // local copy -- captured per-iteration, not by the loop variable
                tab.GetComponent<Button>()?.onClick.AddListener(() => Go(target));
                _tabs[i] = (tab, screen, tab.transform.Find("Icon")?.GetComponent<Image>(),
                                         tab.transform.Find("Label")?.GetComponent<TMPro.TextMeshProUGUI>());
            }
        }

        static GameObject Find2(GameObject root, string path) => root.transform.Find(path)?.gameObject;

        void WireMainMenu()
        {
            GameObject c = Find("MainMenuCanvas");
            _menuCanvas = c;
            if (c == null) return;
            UIWire.Click(c, "PlayButton", () => StartRunAtStage(1));
            UIWire.Click(c, "ContinueButton", () => StartRunAtStage(SaveData.I.HighestCheckpointStage));
            UIWire.Click(c, "NewGameButton", () => StartRunAtStage(1));

            // PLAY alone before any checkpoint exists; CONTINUE + NEW GAME once one does -- no
            // UI clutter for a player who hasn't earned a checkpoint yet.
            bool hasCheckpoint = SaveData.I.HighestCheckpointStage > 0;
            SetActive(c.transform.Find("PlayButton")?.gameObject, !hasCheckpoint);
            SetActive(c.transform.Find("ContinueButton")?.gameObject, hasCheckpoint);
            SetActive(c.transform.Find("NewGameButton")?.gameObject, hasCheckpoint);

            var continueLabel = c.transform.Find("ContinueButton/Label")?.GetComponent<TMPro.TextMeshProUGUI>();
            if (continueLabel != null) continueLabel.text = $"CONTINUE\nSTAGE {SaveData.I.HighestCheckpointStage}";
        }

        /// <summary>
        /// Requests GameManager start at a given stage, then starts the run through the normal
        /// StartRun path. Two calls, not one, because a fresh (never-yet-played) scene's
        /// GameManager.Awake already ran before this click -- see StartRun's remarks on why the
        /// "first press" and "already dirty" cases differ. RequestStartStage covers the reload
        /// case (RestartRun -> a new Awake consumes it); calling JumpToStage directly covers the
        /// no-reload case. Both together are safe to call unconditionally -- JumpToStage is a
        /// plain assignment, not a one-shot event.
        /// </summary>
        void StartRunAtStage(int stage)
        {
            GameManager.RequestStartStage(stage);
            if (GameManager.Instance != null) GameManager.Instance.JumpToStage(stage);
            StartRun();
        }

        void WirePaused()
        {
            GameObject c = Find("PausedCanvas");
            _pausedCanvas = c;
            if (c == null) return;
            UIWire.Click(c, "ResumeButton", () => Go(Screen_.Playing));
            UIWire.Click(c, "RestartButton", RestartRun);
            UIWire.Click(c, "PausedSettingsButton", () => { _returnTo = Screen_.Paused; Go(Screen_.Settings); });
            UIWire.Click(c, "PausedMenuButton", () => Go(Screen_.Menu));
        }

        void WireSettings()
        {
            GameObject c = Find("SettingsCanvas");
            _settingsCanvas = c;
            if (c == null) return;
            UIWire.Click(c, "SettingsBackButton", () => Go(_returnTo));

            Transform toggle = c.transform.Find("SoundPanel/SoundToggle");
            if (toggle != null)
            {
                _soundToggle = toggle.GetComponent<CheckboxToggle>();
                Button button = toggle.GetComponent<Button>();
                if (button != null) button.onClick.AddListener(ToggleSound);
                RefreshSoundToggle();
            }
        }

        void ToggleSound()
        {
            Audio.Muted = !Audio.Muted;
            RefreshSoundToggle();
        }

        void RefreshSoundToggle() => _soundToggle?.SetOn(!Audio.Muted);

        const string ShopContentPath = "ScrollView/Viewport/Content";

        void WireShop()
        {
            GameObject c = Find("ShopCanvas");
            _shopCanvas = c;
            if (c == null) return;

            Transform content = c.transform.Find(ShopContentPath);
            if (content == null) { Debug.LogWarning("[Frontline] Shop ScrollView content not found."); return; }

            foreach (HeroDef hero in Heroes.All)
            {
                HeroDef captured = hero;
                Transform row = content.Find($"ShopHero_{hero.Id}");
                Button button = row != null ? row.Find("Action")?.GetComponent<Button>() : null;
                button?.onClick.AddListener(() => OnShopHeroRowClicked(captured));
            }
            foreach (WeaponDef def in Weapons.Pickups)
            {
                WeaponDef captured = def;
                Transform row = content.Find($"ShopRow_{def.Mesh}");
                Button button = row != null ? row.Find("Action")?.GetComponent<Button>() : null;
                button?.onClick.AddListener(() => OnShopWeaponRowClicked(captured));
            }
            RefreshShop();
        }

        void OnShopHeroRowClicked(HeroDef hero)
        {
            SaveData save = SaveData.I;
            if (!save.IsHeroUnlocked(hero.Id))
                save.UnlockHero(hero.Id, hero.UnlockCost);
            else
                save.EquipHero(hero.Id);
            RefreshShop();
        }

        void OnShopWeaponRowClicked(WeaponDef def)
        {
            SaveData save = SaveData.I;
            if (!save.IsWeaponUnlocked(def.Mesh))
                save.UnlockWeapon(def.Mesh, def.UnlockCost);
            else
            {
                int level = save.WeaponLevel(def.Mesh);
                if (level < Tuning.WeaponUpgradeMaxLevel)
                    save.UpgradeWeapon(def.Mesh, Tuning.WeaponUpgradeCost(level), Tuning.WeaponUpgradeMaxLevel);
            }
            RefreshShop();
        }

        /// <summary>Refreshes every row's lock/level/equipped status, cost, and afford-tint from
        /// SaveData. Called on Awake and after every purchase (Supply, or the equipped hero,
        /// just changed).</summary>
        void RefreshShop()
        {
            if (_shopCanvas == null) return;
            SaveData save = SaveData.I;
            RefreshTopBarSupply();

            Transform content = _shopCanvas.transform.Find(ShopContentPath);
            if (content == null) return;

            foreach (HeroDef hero in Heroes.All)
            {
                Transform row = content.Find($"ShopHero_{hero.Id}");
                if (row == null) continue;

                var actionLabel = row.Find("Action/Label")?.GetComponent<TMPro.TextMeshProUGUI>();
                var affordance = row.Find("Action")?.GetComponent<ShopActionButton>();

                if (!save.IsHeroUnlocked(hero.Id))
                {
                    if (actionLabel != null) actionLabel.text = $"UNLOCK\n{hero.UnlockCost}";
                    affordance?.SetAffordable(save.Supply >= hero.UnlockCost);
                }
                else if (save.EquippedHero == hero.Id)
                {
                    if (actionLabel != null) actionLabel.text = "EQUIPPED";
                    affordance?.SetAffordable(false);
                }
                else
                {
                    if (actionLabel != null) actionLabel.text = "EQUIP";
                    affordance?.SetAffordable(true);
                }
            }

            foreach (WeaponDef def in Weapons.Pickups)
            {
                Transform row = content.Find($"ShopRow_{def.Mesh}");
                if (row == null) continue;

                var statusTmp = row.Find("Status")?.GetComponent<TMPro.TextMeshProUGUI>();
                var actionLabel = row.Find("Action/Label")?.GetComponent<TMPro.TextMeshProUGUI>();
                var affordance = row.Find("Action")?.GetComponent<ShopActionButton>();

                if (!save.IsWeaponUnlocked(def.Mesh))
                {
                    if (statusTmp != null) statusTmp.text = "LOCKED";
                    if (actionLabel != null) actionLabel.text = $"UNLOCK\n{def.UnlockCost}";
                    affordance?.SetAffordable(save.Supply >= def.UnlockCost);
                }
                else
                {
                    int level = save.WeaponLevel(def.Mesh);
                    bool maxed = level >= Tuning.WeaponUpgradeMaxLevel;
                    if (statusTmp != null)
                        statusTmp.text = maxed ? $"MAX  Lv{level}" : $"Lv {level}/{Tuning.WeaponUpgradeMaxLevel}";

                    if (maxed)
                    {
                        if (actionLabel != null) actionLabel.text = "MAX";
                        affordance?.SetAffordable(false);
                    }
                    else
                    {
                        int cost = Tuning.WeaponUpgradeCost(level);
                        if (actionLabel != null) actionLabel.text = $"UPGRADE\n{cost}";
                        affordance?.SetAffordable(save.Supply >= cost);
                    }
                }
            }
        }

        void WireUpgrades()
        {
            GameObject c = Find("UpgradesCanvas");
            _upgradesCanvas = c;
            if (c == null) return;
            RefreshUpgrades();
        }

        /// <summary>Just the lock/level badge -- everything else on this card (name, stats,
        /// range bar) is static WeaponDef data baked in at scene-build time.</summary>
        void RefreshUpgrades()
        {
            if (_upgradesCanvas == null) return;
            SaveData save = SaveData.I;

            foreach (WeaponDef def in Weapons.Pickups)
            {
                Transform card = _upgradesCanvas.transform.Find($"Card_{def.Mesh}");
                var badge = card != null ? card.Find("Badge")?.GetComponent<TMPro.TextMeshProUGUI>() : null;
                if (badge == null) continue;

                if (!save.IsWeaponUnlocked(def.Mesh))
                {
                    badge.text = "LOCKED";
                    badge.color = new Color(0.7f, 0.25f, 0.2f);
                }
                else
                {
                    int level = save.WeaponLevel(def.Mesh);
                    badge.text = $"Lv {level}";
                    badge.color = new Color(0.15f, 0.5f, 0.2f);
                }
            }
        }

        void WireLeaderboard()
        {
            GameObject c = Find("LeaderboardCanvas");
            _leaderboardCanvas = c;
        }

        /// <summary>
        /// Fills the 8 pre-built rows from GameManager's local high-score list. Called every
        /// time the tab opens rather than once at Awake, since a run finishing (this scene
        /// reloading is the only way back to the menu) is exactly when the list changes.
        /// </summary>
        void RefreshLeaderboard()
        {
            if (_leaderboardCanvas == null) return;
            Transform panel = _leaderboardCanvas.transform.Find("RanksPanel");
            if (panel == null) return;

            var scores = GameManager.LoadHighScores();
            for (int i = 0; i < panel.childCount; i++)
            {
                var tmp = panel.GetChild(i).GetComponent<TMPro.TextMeshProUGUI>();
                if (tmp == null) continue;
                tmp.text = i < scores.Count
                    ? $"{i + 1}.   {scores[i]}"
                    : (i == 0 ? "No runs yet -- play one!" : "");
            }
        }

        void WirePauseButton()
        {
            GameObject c = Find("PauseButtonCanvas");
            _pauseButtonCanvas = c;
            if (c == null) return;
            UIWire.Click(c, "PauseButton", () => Go(Screen_.Paused));
        }

        static GameObject Find(string name)
        {
            GameObject go = GameObject.Find($"Canvas/{name}");
            if (go == null) Debug.LogWarning($"[Frontline] {name} not found -- was CanvasBuilder run?");
            return go;
        }

        public void Go(Screen_ screen)
        {
            Screen = screen;
            ApplyTimeScale();
            RefreshTopBarSupply();
            RefreshCanvasVisibility();
            if (screen == Screen_.Leaderboard) RefreshLeaderboard();
            if (screen == Screen_.Shop) RefreshShop();
            if (screen == Screen_.Upgrades) RefreshUpgrades();
        }

        /// <summary>Everything except Playing freezes the world. Menus are not a spectator sport.</summary>
        void ApplyTimeScale() => Time.timeScale = Screen == Screen_.Playing ? 1f : 0f;

        /// <summary>Exactly one screen Canvas active at a time, matching the current Screen_ --
        /// the Canvas draws on top of everything regardless of what OnGUI does, so it needs its
        /// own visibility flags rather than just "not drawn this frame".</summary>
        void RefreshCanvasVisibility()
        {
            bool tabbed = Screen == Screen_.Menu || Screen == Screen_.Shop
                       || Screen == Screen_.Upgrades || Screen == Screen_.Leaderboard;
            SetActive(_menuBackground, Screen != Screen_.Playing);
            SetActive(_shell, tabbed);
            SetActive(_menuCanvas, Screen == Screen_.Menu);
            SetActive(_pausedCanvas, Screen == Screen_.Paused);
            SetActive(_settingsCanvas, Screen == Screen_.Settings);
            SetActive(_shopCanvas, Screen == Screen_.Shop);
            SetActive(_upgradesCanvas, Screen == Screen_.Upgrades);
            SetActive(_leaderboardCanvas, Screen == Screen_.Leaderboard);
            SetActive(_pauseButtonCanvas, Screen == Screen_.Playing);
            SetActive(_gameplayHud, Screen == Screen_.Playing || Screen == Screen_.Paused);
            RefreshTabHighlight();
        }

        static void SetActive(GameObject go, bool active)
        {
            if (go != null) go.SetActive(active);
        }

        static readonly Color TabActive = new Color(1f, 0.85f, 0.35f);
        static readonly Color TabInactive = new Color(1f, 1f, 1f, 0.55f);

        void RefreshTabHighlight()
        {
            if (_tabs == null) return;
            foreach (var (tab, screen, icon, label) in _tabs)
            {
                if (tab == null) continue;
                Color c = screen == Screen ? TabActive : TabInactive;
                if (icon != null) icon.color = c;
                if (label != null) label.color = c;
            }
        }

        void RefreshTopBarSupply()
        {
            if (_topBarSupplyText != null) _topBarSupplyText.text = SaveData.I.Supply.ToString();
        }

        /// <summary>
        /// PLAY. On a fresh scene this just unpauses; only a scene that has already been played
        /// in needs reloading.
        ///
        /// This used to call Restart() unconditionally, which reloaded the scene -- and a reload
        /// destroys this component, so Awake ran again and put us straight back on the menu.
        /// Press PLAY, get the menu, forever. The button rendered perfectly and did nothing,
        /// which a screenshot cannot show you.
        /// </summary>
        public void StartRun()
        {
            if (_dirty) { RestartRun(); return; }
            _dirty = true;
            Go(Screen_.Playing);
        }

        /// <summary>
        /// Reload and drop straight into the run. Reload rather than hand-reset: SceneBuilder's
        /// scene is the source of truth, and a fresh one can't carry stale pools, corpses or a
        /// spent weapon into the next attempt.
        /// </summary>
        public void RestartRun()
        {
            _startImmediately = true;
            Time.timeScale = 1f;
            GameManager.Instance.Restart();
        }
    }
}

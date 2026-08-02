using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace Frontline
{
    /// <summary>
    /// Run state, pools, and the enemy registry.
    ///
    /// Note the prefabs are built from primitives in code rather than authored as .prefab
    /// assets. For the grey-box that's a feature, not a shortcut: it means the scene holds
    /// no asset references to get broken, so the whole project stays reproducible from
    /// source alone. When real art lands we swap BuildTemplate() for serialized prefab
    /// fields and nothing else changes.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        /// <summary>
        /// Assigned by SceneBuilder, not dragged in an Inspector. Null falls back to the
        /// code-built capsule, so the grey-box still runs if the art is ever missing.
        /// </summary>
        [SerializeField] Enemy _enemyPrefab;

        /// <summary>Also assigned by SceneBuilder. No gate prefab simply means no gates.</summary>
        [SerializeField] WeaponGate _gatePrefab;
        /// <summary>No hurdle prefab means occupied-but-not-a-gate lanes just stay empty.</summary>
        [SerializeField] Hurdle _hurdlePrefab;

        [SerializeField] Projectile _projectilePrefab;
        [SerializeField] Flash _flashPrefab;

        public bool RunActive { get; private set; } = true;
        public int Score { get; private set; }
        // Set for real in Awake (Heroes.Equipped().ExtraLives) -- this default only covers the
        // edge case of something reading Health before Awake has run.
        public int Health { get; private set; } = Tuning.PlayerStartHealth;
        public int Wave { get; private set; }

        public float RunTime { get; private set; }
        /// <summary>World difficulty stage, 1-based -- enemy toughness/speed/wave density.</summary>
        public int Stage { get; private set; } = 1;
        /// <summary>
        /// Your own growth, 1-based. Increments once per weapon GATE claimed (a Hurdle grants
        /// nothing). Separated from Stage: Stage is the world's clock, Level is what the player
        /// actually earned, and the HUD's "Lv" used to secretly mean Stage, which made no sense
        /// if the clock ever ran fast (a run showing "Lv 415" for a number nobody chose).
        /// </summary>
        public int Level { get; private set; } = 1;
        /// <summary>Current damage multiplier from Level. Also scales gate/hurdle HP.</summary>
        public float DamageMult => Tuning.LevelDamageMultAt(Level);
        public int EnemyCount => _enemies.Count;

        readonly List<Enemy> _enemies = new List<Enemy>(64);
        readonly List<WeaponGate> _gates = new List<WeaponGate>(4);
        readonly List<Hurdle> _hurdles = new List<Hurdle>(4);

        string _flashText;
        bool _flashDowngrade;
        float _flashTimer;
        string _stageFlashText;
        float _stageFlashTimer;
        int _levelFlashValue;
        int _levelFlashPercent;
        float _levelFlashTimer;
        float _fps = 60f;
        bool _noDie;

        GameObject _deathCanvas;
        GameObject _menuBackground;
        TextMeshProUGUI _deathScoreText;
        TextMeshProUGUI _deathStageText;
        TextMeshProUGUI _deathSupplyText;
        TextMeshProUGUI _hudScoreText;
        TextMeshProUGUI _hudLivesText;
        TextMeshProUGUI _hudStageText;
        TextMeshProUGUI _hudWeaponText;
        int _deathSupplyEarned;

        ObjectPool<Enemy> _enemyPool;
        ObjectPool<Projectile> _projectilePool;
        ObjectPool<WeaponGate> _gatePool;
        ObjectPool<Hurdle> _hurdlePool;
        ObjectPool<Flash> _flashPool;

        /// <summary>
        /// Survives the scene reload a Restart performs -- same pattern as GameUI's
        /// _startImmediately. Set by GameUI's CONTINUE button before StartRun/RestartRun, and
        /// also applied directly if this is a fresh, never-yet-played scene (StartRun doesn't
        /// reload in that case, so Awake never runs again to consume it -- see StartRun's own
        /// remarks on why the "first press" and "already dirty" paths differ).
        /// </summary>
        static int _pendingStartStage = 1;
        public static void RequestStartStage(int stage) => _pendingStartStage = Mathf.Max(1, stage);

        void Awake()
        {
            Instance = this;

            JumpToStage(_pendingStartStage);
            _pendingStartStage = 1;   // consumed -- a later NEW GAME/plain restart defaults to Stage 1

            // The hero's trade-off, applied for real -- see HeroDef.ExtraLives.
            Health = Tuning.PlayerStartHealth + Heroes.Equipped().ExtraLives;

            // -nodie: headless test harness only, see OnEnemyBreachedLine. Never set by a
            // player build.
            foreach (string a in System.Environment.GetCommandLineArgs())
                if (string.Equals(a, "-nodie", System.StringComparison.OrdinalIgnoreCase)) _noDie = true;

            // Mobile Unity targets 30fps unless told otherwise, and vSync on a phone would pin
            // us there regardless. This genre lives or dies on responsiveness -- shipping a 30fps
            // build and concluding "the steering feels sluggish" would be diagnosing the wrong
            // thing entirely.
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            Transform bin = new GameObject("~Pooled").transform;
            bin.SetParent(transform, false);

            Enemy enemyTemplate = _enemyPrefab != null
                ? _enemyPrefab
                : BuildTemplate<Enemy>("EnemyTemplate", PrimitiveType.Capsule,
                                       new Color(0.85f, 0.25f, 0.22f), Tuning.EnemyRadius * 2f);

            Projectile projTemplate = _projectilePrefab != null
                ? _projectilePrefab
                : BuildTemplate<Projectile>("ProjectileTemplate", PrimitiveType.Sphere,
                                            new Color(1f, 0.92f, 0.4f), Tuning.ProjectileRadius * 2f);

            _enemyPool = new ObjectPool<Enemy>(enemyTemplate, bin, warm: 64);
            _projectilePool = new ObjectPool<Projectile>(projTemplate, bin, warm: 48);

            // Only ever a handful alive, but pooled like everything else -- a mid-run
            // Instantiate of a gate is still a hitch, and hitches are what this genre is
            // judged on.
            if (_gatePrefab != null)
                _gatePool = new ObjectPool<WeaponGate>(_gatePrefab, bin, warm: 4);
            if (_hurdlePrefab != null)
                _hurdlePool = new ObjectPool<Hurdle>(_hurdlePrefab, bin, warm: 4);

            // Warmed high: the minigun alone fires ~22/sec, and every one of those spawns a
            // muzzle flash and (usually) an impact.
            if (_flashPrefab != null)
                _flashPool = new ObjectPool<Flash>(_flashPrefab, bin, warm: 32);

            WireDeathScreen();
            WireHud();
        }

        /// <summary>
        /// The real (Kenney-styled, uGUI) Death screen CanvasBuilder built into the scene.
        /// Score/Stage/Level can't be baked at scene-build time like the weapon codex can --
        /// they're only known once a run actually ends -- so this just finds the labels and
        /// buttons by name; Update() fills them in and flips visibility each time RunActive
        /// changes. Same "structure from the generator, behaviour by name at runtime" contract
        /// GameUI uses for every other screen.
        /// </summary>
        void WireDeathScreen()
        {
            GameObject canvas = GameObject.Find("Canvas/DeathCanvas");
            if (canvas == null)
            {
                Debug.LogWarning("[Frontline] DeathCanvas not found -- was CanvasBuilder run?");
                return;
            }
            _deathCanvas = canvas;
            _menuBackground = GameObject.Find("Canvas/MenuBackground");
            _deathScoreText = canvas.transform.Find("ScoreText")?.GetComponent<TextMeshProUGUI>();
            _deathStageText = canvas.transform.Find("StageText")?.GetComponent<TextMeshProUGUI>();
            _deathSupplyText = canvas.transform.Find("SupplyText")?.GetComponent<TextMeshProUGUI>();
            UIWire.Click(canvas, "RestartButton", () => { if (GameUI.Instance != null) GameUI.Instance.RestartRun(); });
            UIWire.Click(canvas, "MenuButton", () => { if (GameUI.Instance != null) GameUI.Instance.Go(Screen_.Menu); });
            _deathCanvas.SetActive(false);
        }

        /// <summary>
        /// The top-left run readout (score/lives/stage/weapon) -- a real card now
        /// (CanvasBuilder.BuildGameplayHud) instead of default-skin IMGUI text, to match the
        /// rest of the UI. GameUI owns showing/hiding the card (RefreshCanvasVisibility); this
        /// just finds the four rows once and keeps them filled in, same split of responsibility
        /// WireDeathScreen already uses.
        /// </summary>
        void WireHud()
        {
            // Not GameObject.Find("Canvas/GameplayHud/StatsPanel") -- GameUI's Awake can run
            // before this one and already hidden GameplayHud (default Screen_ is Menu, and the
            // HUD only shows for Playing/Paused), and GameObject.Find skips inactive objects.
            // "Canvas" itself is always active, so find that first and Transform.Find the rest
            // -- Transform.Find has no such restriction.
            GameObject canvas = GameObject.Find("Canvas");
            Transform panel = canvas != null ? canvas.transform.Find("GameplayHud/StatsPanel") : null;
            if (panel == null)
            {
                Debug.LogWarning("[Frontline] GameplayHud/StatsPanel not found -- was CanvasBuilder run?");
                return;
            }
            _hudScoreText = panel.Find("ScoreText")?.GetComponent<TextMeshProUGUI>();
            _hudLivesText = panel.Find("LivesText")?.GetComponent<TextMeshProUGUI>();
            _hudStageText = panel.Find("StageText")?.GetComponent<TextMeshProUGUI>();
            _hudWeaponText = panel.Find("WeaponText")?.GetComponent<TextMeshProUGUI>();
        }

        void UpdateHud()
        {
            if (_hudScoreText != null) _hudScoreText.text = $"SCORE {Score}";
            if (_hudLivesText != null) _hudLivesText.text = $"LIVES {Health}";
            if (_hudStageText != null) _hudStageText.text = $"STAGE {Stage}";
            if (_hudWeaponText == null) return;
            if (PlayerWeapon.Instance != null && PlayerWeapon.Instance.Current != null)
            {
                // The %DMG stays on screen permanently, not just in the level-up flash --
                // "are you sure a level 1 ak and level 2 ak have different stats?" is answered
                // better by a number you can check any time than by a flash you might miss.
                int pct = Mathf.RoundToInt((DamageMult - 1f) * 100f);
                _hudWeaponText.text = $"WEAPON {PlayerWeapon.Instance.Current.DisplayName}  Lv{Level} (+{pct}% DMG)";
            }
            else
            {
                _hudWeaponText.text = "";
            }
        }

        /// <summary>Makes an inactive, collider-free primitive to serve as a pool prefab.</summary>
        static T BuildTemplate<T>(string name, PrimitiveType shape, Color color, float diameter)
            where T : Component
        {
            GameObject go = GameObject.CreatePrimitive(shape);
            go.name = name;
            // We resolve hits ourselves by distance; colliders would only cost us.
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * diameter;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            T comp = go.AddComponent<T>();
            go.SetActive(false);
            return comp;
        }

        // ---- Spawning ----

        public void SpawnEnemy(Vector3 at) => _enemyPool.Get(at);
        public void ReleaseProjectile(Projectile p) => _projectilePool.Release(p);

        public void SpawnProjectile(Vector3 at, Vector3 direction, float damage, int pierce, float range)
            => _projectilePool.Get(at).Configure(direction, damage, pierce, range);

        public void SpawnGate(WeaponDef offer, float laneX)
        {
            if (_gatePool == null) return;
            WeaponGate gate = _gatePool.Get(new Vector3(laneX, 0f, Tuning.GateSpawnZ));
            gate.Configure(offer, laneX, Tuning.RowItemHalfWidth);
        }

        public void ReleaseGate(WeaponGate g) => _gatePool.Release(g);

        public void SpawnHurdle(float laneX)
        {
            if (_hurdlePool == null) return;
            Hurdle h = _hurdlePool.Get(new Vector3(laneX, 0f, Tuning.GateSpawnZ));
            h.Configure(laneX, Tuning.RowItemHalfWidth);
        }

        public void ReleaseHurdle(Hurdle h) => _hurdlePool.Release(h);

        public void SpawnFlash(Vector3 at, float size, float duration, Color color, float emission)
        {
            if (_flashPool == null) return;
            _flashPool.Get(at).Configure(size, duration, color, emission);
        }

        public void ReleaseFlash(Flash f) => _flashPool.Release(f);

        public void RegisterGate(WeaponGate g) => _gates.Add(g);
        public void UnregisterGate(WeaponGate g) => _gates.Remove(g);
        public void RegisterHurdle(Hurdle h) => _hurdles.Add(h);
        public void UnregisterHurdle(Hurdle h) => _hurdles.Remove(h);

        public void OnGateTaken(WeaponDef def)
        {
            _flashText = def.DisplayName;
            _flashDowngrade = def.Downgrade;
            _flashTimer = Tuning.PickupFlashDuration;
            if (Audio.Instance != null)
                Audio.Instance.Play(def.Downgrade ? Sfx.GateBad : Sfx.GateTaken);

            // Level is earned, not clocked: every claimed gate (never a hurdle -- see Hurdle,
            // it grants nothing) bumps it by one, and a big gold flash says so distinctly from
            // the small pickup-name flash, which only says *which* gun.
            Level++;
            _levelFlashValue = Level;
            // Spelled out in the flash itself, not just implied by a "Lv" number -- "are you
            // sure a level 1 ak and level 2 ak have different stats? different dps?". DamageMult
            // reads the new Level immediately, so this is the real number the next shot uses.
            _levelFlashPercent = Mathf.RoundToInt((DamageMult - 1f) * 100f);
            _levelFlashTimer = Tuning.LevelFlashDuration;
        }

        void Update()
        {
            // Not in OnGUI: that runs several times a frame (Layout, then Repaint), so a timer
            // ticked there drains at a rate that depends on the event count.
            if (_flashTimer > 0f) _flashTimer -= Time.deltaTime;
            if (_stageFlashTimer > 0f) _stageFlashTimer -= Time.deltaTime;
            if (_levelFlashTimer > 0f) _levelFlashTimer -= Time.deltaTime;

            // The stage clock. Only ticks while the run is live (Time.timeScale is 0 on menus,
            // so deltaTime is already 0 then, but guard anyway).
            if (RunActive)
            {
                RunTime += Time.deltaTime;
                int nowStage = Tuning.StageAt(RunTime);
                if (nowStage != Stage)
                {
                    Stage = nowStage;
                    OnStageAdvanced();
                }
            }

            // Smoothed so it's readable rather than a blur of noise. unscaledDeltaTime because
            // this measures the machine, not the game.
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.05f);

            UpdateDeathScreen();
        }

        /// <summary>Same visibility condition the old IMGUI DrawDeathScreen call used -- the
        /// screen belongs to the run, so it shouldn't show over the main menu or a stub tab.</summary>
        void UpdateDeathScreen()
        {
            if (_deathCanvas == null) return;

            bool inRun = GameUI.Instance == null
                         || GameUI.Instance.Screen == Screen_.Playing
                         || GameUI.Instance.Screen == Screen_.Paused;
            bool show = inRun && !RunActive;

            if (show == _deathCanvas.activeSelf) return;
            _deathCanvas.SetActive(show);
            // Only ever force the backdrop ON here, never off -- GameUI.Screen stays "Playing"
            // for the whole time Death is up, so GameUI.RefreshCanvasVisibility never turns the
            // backdrop on by itself. But leaving the death screen (MAIN MENU) flips Screen to
            // Menu and RefreshCanvasVisibility correctly turns the backdrop on for that; forcing
            // it off here on the same frame would stomp that and flash the live battlefield.
            if (show && _menuBackground != null) _menuBackground.SetActive(true);
            if (show)
            {
                if (_deathScoreText != null) _deathScoreText.text = $"SCORE  {Score}";
                if (_deathStageText != null) _deathStageText.text = $"STAGE {Stage}   LEVEL {Level}";
                if (_deathSupplyText != null) _deathSupplyText.text = $"+{_deathSupplyEarned} SUPPLY";
            }
        }

        /// <summary>
        /// Stage boundary: the world's dopamine beat. A big STAGE flash, a sound, and a
        /// guaranteed juicy row (2 gates, so a premium is on offer) right as the difficulty
        /// jumps -- so your chance to answer the new pressure arrives with it.
        /// </summary>
        void OnStageAdvanced()
        {
            _stageFlashText = $"STAGE {Stage}";
            _stageFlashTimer = Tuning.StageFlashDuration;
            if (Audio.Instance != null) Audio.Instance.Play(Sfx.GateTaken);
            SpawnGateRow(minGates: 2);

            // Every Nth stage, replace the flash with a sharper warning -- it fires the same
            // frame as the STAGE text above, so BOSS INCOMING is what actually gets seen; on a
            // boss stage that's the more useful thing to read.
            if (Stage % Tuning.BossStageInterval == 0)
            {
                SpawnBoss();
                _stageFlashText = "BOSS INCOMING";
            }
        }

        /// <summary>
        /// The same pooled Enemy as everything else in the horde, just configured much tougher
        /// and visibly bigger right after spawning -- see Enemy.ConfigureAsBoss. Spawned dead
        /// centre at GateSpawnZ (not the closer EnemySpawnZ regular enemies use) so its bulk is
        /// visible approaching from a distance, same "needs reading time" reasoning gates get.
        /// </summary>
        void SpawnBoss()
        {
            Enemy boss = _enemyPool.Get(new Vector3(0f, 0f, Tuning.GateSpawnZ));
            boss.ConfigureAsBoss();
        }

        /// <summary>
        /// A row across the 5 lanes: occupies 1-3 of them, always leaves the rest open. Occupied
        /// lanes are gates or hurdles; `minGates` of them are forced to be real weapon gates
        /// (the periodic spawner asks for 1, the stage-boundary reward for 2 so a premium is
        /// guaranteed). Never all 5 lanes -- an open lane is always available, which is what
        /// stops any single gate/hurdle from ever being a forced shot or a shield for an enemy
        /// marching behind it.
        /// </summary>
        public void SpawnGateRow(int minGates = 1)
        {
            int occupied = Mathf.Clamp(Random.Range(Tuning.RowMinOccupied, Tuning.RowMaxOccupied + 1),
                                       minGates, Tuning.RowMaxOccupied);

            var lanes = new List<int>(Tuning.RowLaneCount);
            for (int i = 0; i < Tuning.RowLaneCount; i++) lanes.Add(i);
            for (int i = lanes.Count - 1; i > 0; i--)   // Fisher-Yates
            {
                int j = Random.Range(0, i + 1);
                (lanes[i], lanes[j]) = (lanes[j], lanes[i]);
            }

            var gateLanes = new List<int>();
            var hurdleLanes = new List<int>();
            for (int i = 0; i < occupied; i++)
            {
                bool forcedGate = i < minGates;
                if (forcedGate || Random.value >= Tuning.RowHurdleChance) gateLanes.Add(lanes[i]);
                else hurdleLanes.Add(lanes[i]);
            }

            List<WeaponDef> offers = PickRowWeapons(gateLanes.Count);
            for (int i = 0; i < gateLanes.Count; i++)
                SpawnGate(offers[i], Tuning.RowLaneX(gateLanes[i]));

            foreach (int lane in hurdleLanes)
                SpawnHurdle(Tuning.RowLaneX(lane));
        }

        /// <summary>Solo gate leans cheap (a lone gate is easy to commit to, so it shouldn't
        /// always be the safe pick); 2+ gates guarantee the cheap/premium contrast that makes
        /// the row an actual decision.
        ///
        /// Only draws from Shop-unlocked weapons -- a gun you haven't bought yet can't show up
        /// at a gate. AK is always unlocked (see SaveData.NewSave) and always in Cheap, so
        /// `cheap` below can never come back empty; `premium` can, for a brand new save that
        /// hasn't bought Sniper or Minigun yet, so every premium branch below falls back to
        /// cheap rather than indexing an empty array.</summary>
        static List<WeaponDef> PickRowWeapons(int count)
        {
            var offers = new List<WeaponDef>(count);
            if (count <= 0) return offers;

            WeaponDef[] cheap = UnlockedPool(Weapons.Cheap);
            WeaponDef[] premium = UnlockedPool(Weapons.Premium);

            if (count == 1)
            {
                offers.Add(premium.Length > 0 && Random.value >= 0.7f
                    ? premium[Random.Range(0, premium.Length)]
                    : cheap[Random.Range(0, cheap.Length)]);
                return offers;
            }

            offers.Add(premium.Length > 0 ? premium[Random.Range(0, premium.Length)] : cheap[Random.Range(0, cheap.Length)]);
            offers.Add(cheap[Random.Range(0, cheap.Length)]);
            for (int i = 2; i < count; i++)
            {
                WeaponDef[] pool = premium.Length > 0 && Random.value < 0.5f ? premium : cheap;
                offers.Add(pool[Random.Range(0, pool.Length)]);
            }

            // Shuffle so the premium isn't always the first lane picked.
            for (int i = offers.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (offers[i], offers[j]) = (offers[j], offers[i]);
            }
            return offers;
        }

        static WeaponDef[] UnlockedPool(WeaponDef[] pool)
        {
            var list = new List<WeaponDef>(pool.Length);
            foreach (WeaponDef w in pool)
                if (SaveData.I.IsWeaponUnlocked(w.Mesh)) list.Add(w);
            return list.ToArray();
        }

        // ---- Enemy registry ----

        public void RegisterEnemy(Enemy e) => _enemies.Add(e);
        public void UnregisterEnemy(Enemy e) => _enemies.Remove(e);

        /// <summary>
        /// Closest enemy in front of the player. Exists for AutoPilot: a bot that steers at
        /// the nearest target is a rough stand-in for a human, and blind sine-sweeping was
        /// making every difficulty reading useless.
        /// </summary>
        public Enemy FindNearestEnemyAhead()
        {
            Enemy best = null;
            float bestZ = float.MaxValue;
            for (int i = 0; i < _enemies.Count; i++)
            {
                float z = _enemies[i].transform.position.z;
                if (z <= Tuning.PlayerZ || z >= bestZ) continue;
                bestZ = z;
                best = _enemies[i];
            }
            return best;
        }

        /// <summary>The boss, if one is currently alive. For AutoPilot only -- see AutoPilot's
        /// boss-priority branch, added because nearest-by-Z targeting alone let the boss slip
        /// past unshot behind closer regular enemies and gates.</summary>
        public Enemy FindBoss()
        {
            for (int i = 0; i < _enemies.Count; i++)
                if (_enemies[i].IsBoss) return _enemies[i];
            return null;
        }

        public Enemy FindEnemyOverlapping(Vector3 point, float radius)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                Enemy e = _enemies[i];
                float r = radius + e.Radius;
                Vector3 d = e.transform.position - point;
                // Ground plane only — vertical offset is cosmetic in a lane shooter.
                if (d.x * d.x + d.z * d.z <= r * r) return e;
            }
            return null;
        }

        /// <summary>Nearest unclaimed gate ahead of the player. For AutoPilot only.</summary>
        public WeaponGate FindNearestGate()
        {
            WeaponGate best = null;
            float bestZ = float.MaxValue;
            for (int i = 0; i < _gates.Count; i++)
            {
                WeaponGate g = _gates[i];
                if (g == null) continue;
                float z = g.transform.position.z;
                if (z <= Tuning.PlayerZ || z >= bestZ) continue;
                bestZ = z;
                best = g;
            }
            return best;
        }

        /// <summary>
        /// A gate the bullet has reached, if any. Gates are wide panels, so the test is a Z-plane
        /// crossing within the gate's half-width in X -- not a radius, or a bullet could slip
        /// between "inside the panel" frames at the gate's approach speed.
        /// </summary>
        public WeaponGate FindGateOverlapping(Vector3 point)
        {
            for (int i = 0; i < _gates.Count; i++)
            {
                WeaponGate g = _gates[i];
                if (g == null) continue;
                Vector3 gp = g.transform.position;
                if (Mathf.Abs(point.z - gp.z) <= Tuning.GateHitDepth
                    && Mathf.Abs(point.x - gp.x) <= g.HalfWidth)
                    return g;
            }
            return null;
        }

        /// <summary>
        /// True if a live weapon gate currently occupies this X's lane. Deliberately checks
        /// gates only, never hurdles.
        ///
        /// Root cause of "a bad gate you dont want and just behind it is a soldier... those
        /// soldiers will use the bad gate as a shield": a gate absorbs every bullet fired
        /// through it, so whichever object sits CLOSER to the player along a lane blocks
        /// bullets from reaching anything farther away in that same lane. A gate spawns at
        /// Z=42 and takes ~2.7s to reach the enemy spawn line at Z=30; any enemy spawned into
        /// that lane after that point spawns already behind the gate -- and because gate speed
        /// is fixed while enemy speed only ever matches or exceeds it, that gap never closes on
        /// its own, so the enemy stays shielded for the rest of the gate's ~10s lifetime. That
        /// reads as "the soldier used the gate as a shield and walked the width of the screen".
        /// The fix is upstream: never let an enemy spawn into a gate's lane at all (see
        /// EnemySpawner). Hurdles are exempt on purpose -- "those are supposed to be shields".
        /// </summary>
        public bool IsGateLane(float x)
        {
            for (int i = 0; i < _gates.Count; i++)
            {
                WeaponGate g = _gates[i];
                if (g == null) continue;
                if (Mathf.Abs(x - g.transform.position.x) <= g.HalfWidth) return true;
            }
            return false;
        }

        /// <summary>Same crossing test as FindGateOverlapping, for hurdles.</summary>
        public Hurdle FindHurdleOverlapping(Vector3 point)
        {
            for (int i = 0; i < _hurdles.Count; i++)
            {
                Hurdle h = _hurdles[i];
                if (h == null) continue;
                Vector3 hp = h.transform.position;
                if (Mathf.Abs(point.z - hp.z) <= Tuning.GateHitDepth
                    && Mathf.Abs(point.x - hp.x) <= h.HalfWidth)
                    return h;
            }
            return null;
        }

        // ---- Run events ----

        /// <summary>
        /// Scores the kill and takes the enemy out of the registry so it stops being shootable.
        /// Does NOT release it -- Enemy holds on to itself until its death animation finishes,
        /// then calls ReleaseEnemy.
        /// </summary>
        public void OnEnemyKilled(Enemy e)
        {
            Score += 10;
            if (e.IsBoss) OnBossKilled();
            UnregisterEnemy(e);
        }

        /// <summary>
        /// The checkpoint moment: beating a boss doesn't end the run (the player keeps playing
        /// the same endless run past it), but it permanently raises the stage a *future* PLAY
        /// can start from -- see SaveData.HighestCheckpointStage and GameUI's CONTINUE button.
        /// </summary>
        void OnBossKilled()
        {
            Score += Tuning.BossKillScoreBonus;

            if (Stage > SaveData.I.HighestCheckpointStage)
            {
                SaveData.I.HighestCheckpointStage = Stage;
                SaveData.I.Save();
            }
            SaveData.I.AddSupply(Tuning.BossKillSupplyBonus);
        }

        public void ReleaseEnemy(Enemy e) => _enemyPool.Release(e);

        public void OnEnemyBreachedLine(Enemy e)
        {
            _enemyPool.Release(e);
            if (!RunActive) return;

            // Headless test harness only (-nodie on the command line): lets a capture run survive
            // deep enough to verify the difficulty ramp and the frame rate at the enemy cap. A
            // player build never passes the flag, so it's inert in the wild.
            if (_noDie) return;

            // Clamped: several enemies can breach on the same frame, and the HUD read
            // "LIVES -11" before this guard existed.
            Health = Mathf.Max(0, Health - 1);
            if (Audio.Instance != null) Audio.Instance.Play(Sfx.Breach);
            if (Health == 0)
            {
                RunActive = false;
                RecordScore(Score);
                _deathSupplyEarned = Tuning.SupplyEarned(Score, Stage);
                SaveData.I.AddSupply(_deathSupplyEarned);
            }
        }

        // ---- Local high scores (Ranks tab) ----
        //
        // No server, no account -- this is a solo top-8 kept in PlayerPrefs on the device. It's
        // real content for the Ranks tab rather than a "COMING SOON" reskin, without inventing a
        // backend this project doesn't have yet.
        const string HighScoreKey = "Frontline.HighScores";
        const int MaxHighScores = 8;

        static void RecordScore(int score)
        {
            List<int> scores = LoadHighScores();
            scores.Add(score);
            scores.Sort((a, b) => b - a);
            if (scores.Count > MaxHighScores) scores.RemoveRange(MaxHighScores, scores.Count - MaxHighScores);
            PlayerPrefs.SetString(HighScoreKey, string.Join(",", scores));
            PlayerPrefs.Save();
        }

        /// <summary>Best-first. Empty if no run has ever ended yet.</summary>
        public static List<int> LoadHighScores()
        {
            var list = new List<int>();
            string raw = PlayerPrefs.GetString(HighScoreKey, "");
            if (string.IsNullOrEmpty(raw)) return list;
            foreach (string part in raw.Split(','))
                if (int.TryParse(part, out int v)) list.Add(v);
            return list;
        }

        public void OnWaveStarted(int wave) => Wave = wave;

        /// <summary>
        /// Standalone default: restarts by reloading the scene, everything rebuilds from
        /// SceneBuilder's work. Set by FrontlineMiniGame.Init when running under the Miniverse
        /// hub, where this scene is loaded additively over Home -- reloading "the active scene"
        /// there would unload Home too, so the hub build routes RESTART back through the hub
        /// instead. Null (this default) in a standalone Frontline build.
        /// </summary>
        public static System.Action RestartOverride;

        /// <summary>
        /// Set by FrontlineMiniGame.Init when running under the Miniverse hub -- reports the
        /// current score and hands control back to HubLauncher. Null (and never invoked) in a
        /// standalone Frontline build, since there's no hub to return to. GameUI's new
        /// "back to library" button on the tabbed screens calls this directly.
        /// </summary>
        public static System.Action ExitToHubOverride;

        /// <summary>Restarts by reloading the scene — everything rebuilds from SceneBuilder's work.</summary>
        public void Restart()
        {
            if (RestartOverride != null) { RestartOverride(); return; }
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// Jumps straight to a stage rather than replaying the escalation from Stage 1 -- sets
        /// RunTime to match so Update()'s own StageAt(RunTime) check agrees immediately and
        /// never fires OnStageAdvanced for every stage in between (which would spawn a pile of
        /// gate rows and, worse, extra bosses, all in one frame).
        /// </summary>
        public void JumpToStage(int stage)
        {
            Stage = Mathf.Max(1, stage);
            RunTime = (Stage - 1) * Tuning.StageDuration;
        }

        /// <summary>
        /// Everything below was authored against the 540x960 desktop window, which made it
        /// render at roughly half size on a real 1080x2340 phone. Scaling off screen height
        /// keeps it legible on any device.
        /// </summary>
        public static float UiScale => Screen.height / 960f;
        static int Scaled(float v) => Mathf.RoundToInt(v * UiScale);

        // ---- Grey-box HUD ----
        // The persistent score/lives/stage/weapon readout moved to a real canvas
        // (CanvasBuilder.BuildGameplayHud, filled in by UpdateHud) once the rest of the UI got
        // styled -- plain IMGUI text next to the shop's/menu's real cards looked unfinished.
        // Everything else here stays IMGUI on purpose: world-space labels that track a
        // moving Transform (gate/hurdle callouts, floating pickup numbers) and one-off flashes
        // are cheaper to draw straight from Update() than to shuffle through pooled uGUI
        // objects for, and the version/fps stamp is a throwaway dev readout, not player-facing UI.
        void OnGUI()
        {
            // The HUD belongs to the run. Drawing score and gate labels over the main menu or a
            // settings panel would look like a bug.
            bool inRun = GameUI.Instance == null
                         || GameUI.Instance.Screen == Screen_.Playing
                         || GameUI.Instance.Screen == Screen_.Paused;
            if (!inRun) return;

            UpdateHud();

            DrawGateLabels();
            DrawHurdleLabels();
            DrawPickupFlash();
            DrawStageFlash();
            DrawLevelUpFlash();
            DrawStageVignette();
            DrawVersion();
            GUI.color = Color.white;
        }

        /// <summary>
        /// Build number and frame rate, bottom-right.
        ///
        /// The version is how we tell which build is actually on the phone -- we burned a round
        /// trip testing a stale APK against a fix it didn't contain, with no way to tell by
        /// looking. The fps is here for the same reason: "does it hold 60?" was being answered
        /// with "it feels smooth, so probably", and a 30fps build feels like bad controls rather
        /// than like a frame rate. Now it's a number Simon can just read out.
        /// </summary>
        void DrawVersion()
        {
            var style = new GUIStyle(GUI.skin.label)
            { fontSize = Scaled(15), alignment = TextAnchor.LowerRight };
            // Amber under 55: visible without being alarming at a normal 58-60 wobble.
            GUI.color = _fps < 55f ? new Color(1f, 0.7f, 0.3f, 0.9f) : new Color(1f, 1f, 1f, 0.5f);
            // Sits above Unity's "Development Build" watermark, which owns the very bottom-right
            // corner on desktop. The Android release build has no watermark, but these
            // screenshots are how the game gets looked at.
            GUI.Label(new Rect(0f, Screen.height - 62f * UiScale, Screen.width - 10f * UiScale, 26f * UiScale),
                      $"v{Application.version}   {_fps:F0} fps", style);
            GUI.color = Color.white;
        }

        /// <summary>
        /// The weapon's name floating over each gate, in world space.
        ///
        /// Playtest: "they do not make me think". The offer was a small dark gun silhouette
        /// seen from behind at 42 degrees -- you can't weigh a choice you can't read. Projecting
        /// the name to screen with WorldToScreenPoint costs nothing and needs no TextMeshPro,
        /// which would drag in a package import and an essential-resources dialog for what is
        /// still throwaway debug UI.
        /// </summary>
        void DrawGateLabels()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = Scaled(24), alignment = TextAnchor.MiddleCenter };
            var hpStyle = new GUIStyle(GUI.skin.label)
            { fontSize = Scaled(44), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            foreach (WeaponGate g in _gates)
            {
                if (g == null || g.Offer == null) continue;

                Vector3 sp = cam.WorldToScreenPoint(g.transform.position + Vector3.up * (Tuning.GateHeight + 0.4f));
                if (sp.z <= 0f) continue;   // behind the camera; WorldToScreenPoint mirrors it

                float x = sp.x, topY = Screen.height - sp.y;
                // Narrow: far-off gates sit near the vanishing point where their two screen-X
                // positions nearly coincide, so wide labels overlapped into "BALANMINIGUN".
                float w = 130f * UiScale;

                // The number you're shooting down -- the load-bearing thing to read at a glance.
                GUI.color = new Color(1f, 1f, 1f, 0.95f);
                GUI.Label(new Rect(x - w * 0.5f, topY - 50f * UiScale, w, 48f * UiScale),
                          Mathf.CeilToInt(Mathf.Max(0f, g.Hp)).ToString(), hpStyle);

                // Gun name only -- the trait lives in the codex now, and dropping it stops the
                // two gates' labels colliding when they're far off.
                GUI.color = new Color(0.85f, 0.95f, 1f);
                GUI.Label(new Rect(x - w * 0.5f, topY, w, 28f * UiScale), g.Offer.DisplayName, nameStyle);
            }
            GUI.color = Color.white;
        }

        /// <summary>
        /// Just the HP number over each hurdle -- no name (it offers nothing to name), a duller
        /// colour than a gate's so the two read as different kinds of thing at a glance: one is
        /// an offer, one is just in the way.
        /// </summary>
        void DrawHurdleLabels()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            var hpStyle = new GUIStyle(GUI.skin.label)
            { fontSize = Scaled(36), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            foreach (Hurdle h in _hurdles)
            {
                if (h == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(h.transform.position + Vector3.up * 1.6f);
                if (sp.z <= 0f) continue;

                float w = 110f * UiScale;
                GUI.color = new Color(1f, 1f, 1f, 0.6f);
                GUI.Label(new Rect(sp.x - w * 0.5f, Screen.height - sp.y - 42f * UiScale, w, 40f * UiScale),
                          Mathf.CeilToInt(Mathf.Max(0f, h.Hp)).ToString(), hpStyle);
            }
            GUI.color = Color.white;
        }

        /// <summary>Big, central, unmissable. The old feedback was 14px of grey in a corner.</summary>
        void DrawPickupFlash()
        {
            if (_flashTimer <= 0f || string.IsNullOrEmpty(_flashText)) return;

            float t = _flashTimer / Tuning.PickupFlashDuration;   // 1 -> 0
            var style = new GUIStyle(GUI.skin.label) { fontSize = Scaled(54), alignment = TextAnchor.MiddleCenter };

            Color c = _flashDowngrade ? new Color(1f, 0.35f, 0.3f) : new Color(0.55f, 1f, 0.6f);
            c.a = Mathf.Clamp01(t * 2.2f);   // hold, then fade out at the end
            GUI.color = c;

            // Rises as it fades.
            float y = Screen.height * 0.42f - (1f - t) * 40f * UiScale;
            GUI.Label(new Rect(0f, y, Screen.width, 70f * UiScale), _flashText, style);
            GUI.color = Color.white;
        }

        /// <summary>
        /// The stage-up beat: big, centred, higher than the pickup flash so the two don't clash
        /// (a stage boundary spawns a gate, so a pickup flash may follow moments later). This is
        /// the "you levelled up" punch that makes stepped difficulty feel like progress.
        /// </summary>
        void DrawStageFlash()
        {
            if (_stageFlashTimer <= 0f || string.IsNullOrEmpty(_stageFlashText)) return;

            float t = _stageFlashTimer / Tuning.StageFlashDuration;   // 1 -> 0
            var style = new GUIStyle(GUI.skin.label)
            { fontSize = Scaled(72), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

            GUI.color = new Color(1f, 0.85f, 0.3f, Mathf.Clamp01(t * 2.5f));
            GUI.Label(new Rect(0f, Screen.height * 0.30f, Screen.width, 90f * UiScale),
                      _stageFlashText, style);
            GUI.color = Color.white;
        }

        /// <summary>
        /// "LEVEL 4!" in gold, on every gate claimed -- distinct from DrawPickupFlash (which
        /// only names the gun) and from DrawStageFlash (the world's clock, not the player's).
        /// "leveling up needs to be visible... a golden level 2! sign flies on the screen".
        /// Positioned lower than the stage flash and offset sideways so a stage boundary (which
        /// spawns a guaranteed gate) doesn't stack two centred flashes directly on top of each
        /// other if the player claims it immediately.
        /// </summary>
        void DrawLevelUpFlash()
        {
            if (_levelFlashTimer <= 0f) return;

            float t = _levelFlashTimer / Tuning.LevelFlashDuration;   // 1 -> 0
            var style = new GUIStyle(GUI.skin.label)
            { fontSize = Scaled(58), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

            // Punch: overshoot past 1x then settle, in the first third of its life.
            float grow = Mathf.Clamp01((1f - t) * 4f);
            float scale = Mathf.Lerp(1.4f, 1f, Mathf.SmoothStep(0f, 1f, grow));

            var matrix = GUI.matrix;
            float y = Screen.height * 0.58f;
            Vector2 pivot = new Vector2(Screen.width * 0.5f, y + 45f * UiScale);
            GUIUtility.ScaleAroundPivot(Vector2.one * scale, pivot);

            GUI.color = new Color(1f, 0.82f, 0.15f, Mathf.Clamp01(t * 2.5f));
            GUI.Label(new Rect(0f, y, Screen.width, 90f * UiScale),
                      $"LEVEL {_levelFlashValue}!  +{_levelFlashPercent}% DMG", style);

            GUI.matrix = matrix;
            GUI.color = Color.white;
        }

        /// <summary>
        /// A warm full-screen overlay that deepens with Stage -- the always-visible danger cue
        /// for "not sure what separates a stage, cant feel it playing cant see it and i dont
        /// look to the top left". A first attempt tinted the enemies' own materials directly,
        /// which is a stronger and more specific cue in principle, but broke their actual
        /// appearance twice (the characters turned out to render via per-submesh TEXTURED
        /// material slots, not the flat per-part _BaseColor every other custom material in this
        /// project uses -- a property block applied without a slot index hits every material on
        /// a renderer identically, smearing one sampled colour across textures it was never
        /// meant to touch). A screen-space overlay cannot misrender a character under any
        /// circumstance, so it's the safe version of the same idea: it can't be missed (covers
        /// the whole frame, the entire time), and it costs nothing to prove correct.
        ///
        /// Zero at Stage 1 (t=0, alpha=0 -- fully transparent, provably a no-op), ramping in
        /// over the next several stages rather than front-loading it in one jump.
        /// </summary>
        void DrawStageVignette()
        {
            float t = Mathf.Clamp01((Stage - 1) / 7f);
            float alpha = t * 0.22f;
            if (alpha <= 0f) return;

            GUI.color = new Color(0.85f, 0.08f, 0.05f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}

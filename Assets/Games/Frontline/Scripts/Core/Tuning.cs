using UnityEngine;   // Mathf, for the per-stage scaling helpers

namespace Frontline
{
    /// <summary>
    /// Every number that decides how the game feels, in one place.
    ///
    /// Deliberately a plain static class rather than a ScriptableObject: SO values get
    /// baked into a .asset file, so editing the C# default afterwards does nothing and
    /// the only way to retune is by hand in the Inspector. Static consts mean a change
    /// here actually lands on the next build. Costs us live tweaking during Play mode,
    /// which we weren't going to use anyway.
    /// </summary>
    public static class Tuning
    {
        // ---- Lane ----
        public const float LaneHalfWidth = 3.0f;   // player clamps to +/- this on X

        // The soldier sits this far back from the point the camera frames around, which pulls
        // him low in the frame (toward the viewer) -- Simon wanted ~20% up from the bottom, not
        // 40%. Because the camera anchors on CamAnchorZ and NOT on the soldier, moving him back
        // moves only him, not the whole view. Every weapon's range gets this added back on (see
        // AutoFirer) so bullets still reach the exact same world line they did before -- "guns
        // shoot to the line where they were shooting now".
        public const float PlayerPullback = 3.6f;
        public const float CamAnchorZ = 0f;         // the Z the camera is framed around
        public const float PlayerZ = -PlayerPullback;

        // ---- Player ----
        // 22, up from 14. This caps how fast the soldier can chase your finger, and on a phone
        // an ordinary thumb flick was outrunning it -- so he rubber-banded along behind the
        // touch instead of tracking it. Wants to stay comfortably above any speed a real drag
        // can ask for, or the steering stops feeling attached to your hand.
        public const float PlayerMoveSpeed = 22f;  // units/sec toward the touch target

        /// <summary>
        /// How much of the screen's width you drag across to cross the whole lane.
        ///
        /// This replaced a raw screen-pixels-to-world scale (0.022), which was silently
        /// resolution-dependent -- a pixel is not a physical unit. Tuned on a 540px desktop
        /// window it meant a 273px drag crossed the lane: half that window, ~72mm of mouse.
        /// The same constant on a 1080px phone made 273px a *quarter* of the screen and about
        /// 18mm of thumb. Four times too sensitive, and the reason steering felt like it "moves
        /// 10x more than what i input".
        ///
        /// As a fraction it means the same thing on every device. 0.5 = sweep half the screen,
        /// cross the lane. NB 0.5 also happens to reproduce the old desktop feel exactly at
        /// 540px, which is the feel that was reported as good.
        /// </summary>
        public const float LaneCrossDragFraction = 0.5f;
        public const float PlayerRadius = 0.4f;

        // ---- Weapon ----
        public const float FireInterval = 0.12f;   // seconds between shots
        public const float ProjectileSpeed = 34f;
        public const float ProjectileDamage = 10f;
        public const float ProjectileRadius = 0.12f;
        // Fallback only. The real muzzle comes from the equipped gun's own bounds -- see
        // PlayerWeapon.MuzzlePosition. These are used solely if no gun is equipped.
        public const float MuzzleForwardOffset = 0.6f;
        public const float MuzzleHeight = 1.0f;
        public const float MuzzleTipGap = 0.08f;   // just clear of the barrel, so it doesn't clip

        // The single most important number in the game right now.
        //
        // Bullets used to be capped by a 2.5s lifetime = 85 units of travel, which out-ranged
        // the whole 40-unit lane: enemies died on the pixel they spawned on and combat lived
        // at the vanishing point. Range is the design concept, lifetime was an implementation
        // detail leaking through, so it's expressed directly and Projectile derives the rest.
        //
        // 16 is chosen off the camera projection, not taste. Working it out for the rig below
        // (55 deg vertical FOV, 42 deg pitch, 8.5 up, 6 back): the visible lane runs Z=-2.8 to
        // Z=26.9, but perspective is brutally non-linear -- Z=16 sits only ~13% down the frame
        // while Z=0 sits at ~72%. So a 16-unit range makes the kill zone the *near half* of
        // the screen -- the half a player can actually read -- while the horde still marches
        // in from the horizon untouched. Retune this if the camera block below changes.
        public const float WeaponRange = 16f;

        // ---- Enemies ----
        // 4.5, up from 3.2. At the old speed an enemy spent 5.6s walking from spawn to the
        // range edge before anything could happen to it -- most of a run was a conveyor belt.
        public const float EnemySpeed = 4.5f;      // marching toward the player (-Z)
        // 25 (3 hits). Briefly tried 40 to stop enemies popping the moment they crossed the
        // range line -- but that fear was wrong. What actually spaces kills out across the
        // kill zone is X alignment: a bullet only covers ProjectileRadius + EnemyRadius of a
        // 6-unit lane, so the gate on damage is being lined up, not enemy HP. 40 just starved
        // throughput without buying anything.
        public const float EnemyHealth = 25f;
        public const float EnemyRadius = 0.45f;
        // How long a corpse stays on screen playing its death animation. Enemies used to blink
        // out of existence the frame their health hit zero -- and dying is the single most
        // frequent thing that happens in this game, ~80 times a run.
        public const float EnemyDeathDuration = 1.1f;
        // 30, down from 44. Worked out against the current camera (12.5 up, 9 back, 46 pitch,
        // 55 vertical FOV): the ground's visible far edge sits at world Z ~28.4, so 44 meant
        // enemies spent (44-28.4)/EnemySpeed =~3.5s marching in the dark before they were even
        // visible. "i wait too long for enemy soldiers to run in, have them nearly immediately
        // come into frame" -- 30 sits just past the visible edge, so they pop in almost at once.
        // Gates deliberately do NOT share this constant any more (see GateSpawnZ) -- they still
        // need the long runway for reading the row and choosing a lane.
        public const float EnemySpawnZ = 30f;
        // Contact with the soldier, not 6 units behind his back. Relative to PlayerZ so it
        // tracks the soldier wherever the pullback puts him -- an absolute value would leave
        // enemies dying in mid-air where the player used to stand.
        public const float EnemyBreachZ = PlayerZ + PlayerRadius + EnemyRadius;

        // ---- Waves ----
        // Tightened from 2.4s / +1 per wave. The old curve never caught up with the player's
        // kill rate, so LIVES never dropped once across any run -- the breach mechanic worked,
        // it was simply never reached. Pressure has to eventually exceed throughput or the
        // prototype can't answer whether it's fun.
        public const float WaveInterval = 1.8f;    // seconds between wave pushes
        public const int WaveSizeBase = 4;
        public const int WaveSizeGrowth = 2;       // +N enemies per wave
        public const int WaveSizeMax = 40;
        // NB: this also hard-caps spawn rate at 1/0.25 = 4 enemies/sec no matter how big
        // waves get, which is the real ceiling on difficulty. Revisit here first if the
        // late game turns out too easy.
        public const float WaveSpawnSpread = 0.25f; // stagger within a wave

        // ---- Audio ----
        public const int AudioVoices = 12;      // ring of sources; overlapping cues need room
        // 1.0, because per-clip levels are now normalised against measured RMS (see
        // Weapons.ShotVolume and Editor/AudioReport) rather than fudged with a global.
        public const float MasterVolume = 1f;
        public const float SfxPitchJitter = 0.09f;  // +/- per shot, so repeats don't read as a loop
        public const float ShotVolume = 0.3f;   // fallback only; guns carry their own level

        // ---- Why these pitches exist ----
        // Measured brightness (Editor/AudioReport, high-frequency energy / total):
        //   impactSoft_heavy  0.009   <- body fall: inaudible
        //   lowFrequency_expl 0.009   <- old sniper: confirmed inaudible
        //   impactPunch_heavy 0.016   <- kill: "barely"
        //   impactPunch_med   0.024   <- hit: "pretty quiet"
        //   explosionCrunch   0.113   <- shotgun: CONFIRMED audible
        //   laserRetro        0.316+  <- guns: clearly audible
        // Every cue Simon can't hear is 10-40x darker than every cue he can. Phone speakers
        // roll off below a few hundred Hz, so Kenney's impact foley -- which is all bass -- is
        // thrown away by the hardware no matter what volume it's given. Pitching a clip up
        // shifts its energy into the band the speaker can actually move; it's the only lever
        // that works without different source material.
        public const float HitVolume = 0.55f;
        public const float HitPitch = 1.9f;     // drags impactPunch_medium up into audible range

        // The kill *crack* is now deliberately minor -- the body landing is the cue that says
        // "he's down", which is what was actually being asked for. Two loud events a beat apart
        // for every one of ~80 kills a run would just be noise.
        public const float KillVolume = 0.55f;
        public const float KillPitch = 1.6f;

        // ---- Juice ----
        // Muzzle: brief enough to read as a flash rather than a bulb. At the minigun's 0.045s
        // cadence these overlap into a near-constant glow, which is exactly right.
        // Bigger and longer than physical honesty wants. The camera is 10 units back looking
        // down at 42 degrees, so a realistic flash is ~8px for 55ms -- technically present,
        // invisible in motion. Juice has to be sized for where the camera actually is.
        public const float MuzzleFlashSize = 0.62f;
        public const float MuzzleFlashDuration = 0.07f;
        public const float MuzzleFlashEmission = 8f;

        // Impact: bigger and slower than the muzzle, because it's the one that tells you the
        // shot connected. Bullets previously vanished into enemies with no acknowledgement at
        // all until the third one killed them.
        public const float HitFlashSize = 0.8f;
        public const float HitFlashDuration = 0.15f;
        public const float HitFlashEmission = 7f;

        // ---- Lanes (weapon gates / hurdles) ----
        // Discrete slots across the road's width. A "row" occupies some of these and leaves the
        // rest open, which is the fix for "a bad gate you dont want, and just behind it is a
        // soldier... those soldiers will use the bad gate as a shield" -- a gate absorbs every
        // bullet aimed through it (no pierce), so an enemy standing behind an unwanted gate used
        // to be unkillable until you either cleared or ate that gate. With open lanes always
        // available, you're never forced to shoot through anything you don't want: dodge into a
        // clear lane and the enemy behind it has no cover at all.
        public const int RowLaneCount = 5;
        public static float RowLaneWidth => (LaneHalfWidth * 2f) / RowLaneCount;
        // Under half a lane's width, so neighbouring occupied lanes don't visually touch.
        public static float RowItemHalfWidth => RowLaneWidth * 0.42f;
        public static float RowLaneX(int index) => -LaneHalfWidth + RowLaneWidth * (index + 0.5f);

        public const int RowMinOccupied = 1;
        public const int RowMaxOccupied = 3;         // never all 5 -- an open lane always exists
        public const float RowHurdleChance = 0.35f;  // per occupied lane not forced to be a gate

        public const float GateSpeed = 4.5f;        // matches EnemySpeed: gates ride in with the horde
        public const float GateSpinSpeed = 60f;     // deg/sec on the displayed gun
        public const float GateHeight = 2.4f;
        public const float GateInterval = 9f;
        public const float GateFirstDelay = 5f;
        // Kept far (unlike EnemySpawnZ, which was pulled in) -- a gate row is the one thing you
        // need real time to read: how many lanes are occupied, which gun, cheap or premium.
        public const float GateSpawnZ = 42f;
        public const float GateDespawnZ = -4f;      // well behind the player; it has already resolved
        // How thick the gate is to a bullet, in Z. Generous so a fast bullet can't tunnel past
        // the panel between frames (34 u/s * 1/60 = ~0.57 units/frame).
        public const float GateHitDepth = 0.8f;
        public const float PickupFlashDuration = 1.1f;  // big centre-screen weapon name on pickup
        public const float GatePopDuration = 0.18f;     // the taken gate's punch before it vanishes
        public const float GatePopScale = 1.5f;

        // ---- Hurdles ----
        // A shoot-through obstacle with no reward -- "you dont get a new gun but you can shoot
        // through it to get to soldiers behind it". Occupies a lane exactly like a gate (same
        // pooling/HP/claim mechanics via the ILaneBlocker seam) but grants nothing on clearing,
        // so its HP is kept low: it's a speed bump for that lane, not a resource decision.
        public const float HurdleBaseHp = 45f;

        // ---- Camera ----
        // Zoomed out from (8.5, 6). The close camera left no time to see a gate before it
        // arrived; the whole point of the mechanic is choosing, and you can't choose what you
        // can't see coming. Higher and further back shows more lane ahead -- room to read the
        // gates and plan -- moving toward the reference gate-runners where the approach is a
        // long readable runway. NB combat now sits higher up the frame; if it starts to feel
        // far again, pull EnemySpawnZ and the weapon ranges in rather than dollying back.
        public const float CamHeight = 12.5f;
        public const float CamBack = 9.0f;
        public const float CamPitch = 46f;         // degrees looking down; steeper to see further
        public const float CamFollowLerp = 6f;     // how hard it tracks player X
        public const float CamFollowXScale = 0.35f; // only partially follows -> feels anchored

        // ---- Run ----
        public const int PlayerStartHealth = 5;

        // ---- Difficulty ramp (Stages) ----
        // Stepped, not smooth: dopamine comes from contrast -- a breather, then a visible spike
        // -- so the WORLD escalates every StageDuration seconds. Stage 1 is the baseline; each
        // stage after multiplies from it.
        //
        // 55, up from 30 ("stages should be less common"). Stage governs enemy toughness/speed/
        // wave density ONLY now -- your own power moved to Level (below), so slowing this down
        // no longer also throttles how often you get stronger.
        public const float StageDuration = 55f;

        // FPS guard. The Kirin 980 held 62fps at ~20 skinned enemies, so 40 keeps headroom.
        // Difficulty past this comes from tankier/faster enemies and bigger gates, NOT more
        // bodies -- which is what keeps the frame budget flat however deep the run goes. This
        // cap lifts for free once the horde is instanced.
        public const int EnemyConcurrentCap = 40;

        public const float EnemyHealthPerStage = 0.35f;
        public const float EnemySpeedPerStage = 0.22f;
        public const float EnemySpeedCap = 7.5f;
        // Waves come faster and denser each stage, bounded by the concurrent cap above.
        public const float WaveIntervalMin = 0.7f;
        public const float WaveIntervalPerStage = 0.16f;
        public const int WaveSizePerStage = 3;      // extra enemies per wave per stage

        public const float StageFlashDuration = 1.6f;

        public static int StageAt(float runTime) => Mathf.FloorToInt(runTime / StageDuration) + 1;
        public static float EnemyHealthAt(int stage) => EnemyHealth * (1f + EnemyHealthPerStage * (stage - 1));
        public static float EnemySpeedAt(int stage) => Mathf.Min(EnemySpeed + EnemySpeedPerStage * (stage - 1), EnemySpeedCap);
        public static float WaveIntervalAt(int stage) => Mathf.Max(WaveIntervalMin, WaveInterval - WaveIntervalPerStage * (stage - 1));

        // ---- Weapon Level ----
        // Your own growth, separated from Stage. It used to be the same clock: DamageMultAt
        // read off Stage, so the HUD's "Lv" was really a disguised stage count -- if stages ever
        // came quickly it would read as an absurd "Lv 415" with no connection to anything the
        // player did. Level now increments by 1 every time a weapon GATE is claimed (a Hurdle
        // grants nothing), so it tracks "how many gates you actually took" -- a number that
        // naturally stays sane over a run, and is something the player earned rather than
        // something the clock handed them.
        // 0.22, up from 0.15. "are you sure a level 1 ak and level 2 ak have different stats?
        // different dps? that is the whole point" -- the multiplier was real (AutoFirer does
        // multiply def.Damage by this every shot) but a 15% step wasn't a strong enough signal
        // to feel shot-to-shot against enemies whose own HP doesn't move with Level at all
        // (only Stage scales enemy HP). 22% is a clearer jump, and it's now spelled out
        // directly on the level-up flash rather than asked to be taken on faith.
        public const float DamagePerLevel = 0.22f;
        public static float LevelDamageMultAt(int level) => 1f + DamagePerLevel * Mathf.Max(0, level - 1);
        // The big gold "LEVEL 4!" flash on a gate claim -- distinct from the small pickup-name
        // flash, which just says which gun. This one says you got stronger.
        public const float LevelFlashDuration = 1.3f;

        // ---- Economy (Supply) ----
        // Earned once, on death (see GameManager.OnEnemyBreachedLine), and spent in the Shop on
        // weapon/hero unlocks and upgrades (SaveData). Two components on purpose: score alone
        // would only reward killing, and a run that survives to a late stage without racking up
        // kills (e.g. dodging a rough stretch) should still be worth something -- surviving is
        // itself the harder half of a late run.
        public const float SupplyPerScore = 0.5f;
        public const int SupplyPerStage = 8;
        public static int SupplyEarned(int score, int stage) =>
            Mathf.RoundToInt(score * SupplyPerScore) + stage * SupplyPerStage;

        // ---- Weapon meta-upgrades (Shop) ----
        // Deliberately one shared curve for every weapon rather than a per-gun table: this is a
        // second, PERMANENT damage multiplier that stacks on top of the existing per-run Level
        // system (DamagePerLevel above), not a replacement for it -- so it stays simple and
        // uniform rather than needing its own balance pass per weapon.
        public const int WeaponUpgradeMaxLevel = 4;
        public const int WeaponUpgradeBaseCost = 60;
        public const int WeaponUpgradeCostPerLevel = 40;
        public const float WeaponUpgradeDamagePerLevel = 0.06f;
        public static int WeaponUpgradeCost(int currentLevel) => WeaponUpgradeBaseCost + WeaponUpgradeCostPerLevel * currentLevel;
        public static float WeaponUpgradeMultAt(int level) => 1f + WeaponUpgradeDamagePerLevel * level;

        // ---- Bosses & checkpoints ----
        // A boss is the same pooled Enemy, just configured much tougher and visibly bigger
        // (Enemy.ConfigureAsBoss) -- not a parallel enemy type, so it rides the existing
        // kill/breach/registry/pool machinery unchanged.
        public const int BossStageInterval = 5;      // every Nth stage boundary
        public const float BossHealthMult = 12f;     // a real wall, not just a fat regular enemy
        public const float BossSpeedMult = 0.6f;     // slower -- menacing, not a rusher
        public const float BossScale = 1.8f;
        public const int BossKillScoreBonus = 200;
        public const int BossKillSupplyBonus = 150;
    }
}

using System;
using FlowSort.Gameplay;
using FlowSort.Meta;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlowSort.Blocks
{
    /// <summary>
    /// Level lifecycle, scoring, and the win/fail rules.
    ///
    /// The challenge is an ammo economy, not a timer: each level hands you a fixed set of
    /// colour-matched towers. Shots that meet the wrong colour die on impact and are wasted, so
    /// spraying a red tower at a blue region burns your budget for nothing. Clear the picture to
    /// win; run every tower dry with blocks still standing and you lose.
    /// </summary>
    public class BlockBreakGame : MonoBehaviour
    {
        public BlockWall Wall;
        public BallSystem Balls;
        public TowerSlots Slots;
        public CurrencyWallet Wallet;
        public ImpactFX Fx;
        public BlockHud Hud;
        public PixelPicture[] Pictures;

        public int Level { get; private set; } = 1;
        public int Score { get; private set; }
        public bool IsGameOver { get; private set; }

        /// <summary>Mid-game exit; the hub wrapper binds this to MiniGameContext.ReturnToHub.</summary>
        public event Action OnExitRequested;

        /// <summary>Real game over; the hub wrapper binds this to ReportGameOver(score).</summary>
        public event Action<int> OnGameOver;

        /// <summary>
        /// Leave the game. Inside PocketVerse the hub wrapper subscribes and returns to the menu;
        /// standalone there is nothing subscribed and nowhere to go, so the build quits instead of
        /// the button silently doing nothing — which is exactly how it behaved on device.
        /// </summary>
        public void RequestExit()
        {
            if (OnExitRequested != null)
            {
                OnExitRequested.Invoke();
                return;
            }

            // Standalone, this goes back to FlowSort's own menu. Inside PocketVerse only the game
            // scene ships — the hub is the front end — so the menu is not in the build at all and
            // loading it by name would throw. Nothing subscribed AND no menu means there is
            // nowhere to go, which is a state worth saying out loud rather than crashing in.
            if (Application.CanStreamedLevelBeLoaded(GameSession.MenuScene))
            {
                SceneManager.LoadScene(GameSession.MenuScene);
                return;
            }

            Debug.LogWarning("[FlowSort] Exit requested with no hub subscriber and no menu scene.");
        }

        int pictureIndex;
        bool advancing;

        void OnEnable()
        {
            Wall.OnBlockDestroyed += HandleBlockDestroyed;
            Wall.OnWallCleared += HandleWallCleared;
            Slots.OnAllTowersLost += HandleAllTowersLost;
        }

        void OnDisable()
        {
            Wall.OnBlockDestroyed -= HandleBlockDestroyed;
            Wall.OnWallCleared -= HandleWallCleared;
            Slots.OnAllTowersLost -= HandleAllTowersLost;
        }

        void Start()
        {
            PlayerProfile.ApplySound();
            ApplyMode();
            StartLevel();
        }

        /// <summary>
        /// Sets the starting difficulty from whichever mode the menu launched.
        ///
        /// LEVELS starts at one and climbs — the progression the whole fill and line-ordering ramp
        /// was tuned around. ENDLESS skips the ramp entirely and opens at full difficulty, so it
        /// is a score run rather than a curve. DAILY is ENDLESS's midpoint on a seed derived from
        /// the date, which is what makes everyone's board the same one.
        /// </summary>
        void ApplyMode()
        {
            switch (GameSession.Mode)
            {
                case GameMode.Endless:
                    Level = BlockTuning.DifficultyRampLevels + 1;
                    pictureIndex = UnityEngine.Random.Range(0, Mathf.Max(1, Pictures?.Length ?? 1));
                    break;

                case GameMode.Daily:
                    UnityEngine.Random.InitState(GameSession.DailySeed);
                    Level = BlockTuning.FullFillLevel;
                    pictureIndex = GameSession.DailySeed;
                    break;

                default:
                    // Resume where the player got to. Sending them back to level 1 every time
                    // they close the app threw away the whole progression.
                    Level = Mathf.Max(1, PlayerProfile.BestLevel);
                    pictureIndex = Level - 1;
                    break;
            }
        }

        void StartLevel()
        {
            advancing = false;
            IsGameOver = false;

            if (Pictures != null && Pictures.Length > 0)
                Wall.Load(Pictures[Mathf.Abs(pictureIndex) % Pictures.Length],
                          BlockTuning.FillForLevel(Level));

            Balls.ClearAll();

            // The hand is sized from the board, so every colour on it can always be cleared —
            // see TowerSlots.Fill.
            Slots.Fill(Wall, Level);

            Hud?.SetLevel(Level);
            Hud?.SetScore(Score);
            Hud?.SetBelt(Slots.RidingCount, BlockTuning.MaxOnBelt);
            Hud?.HideGameOver();
            Hud?.ShowBanner(GameSession.Mode == GameMode.Levels ? $"LEVEL {Level}"
                                                                : GameSession.Mode.ToString().ToUpper());
        }

        void Update()
        {
            if (IsGameOver || Hud == null) return;
            Hud.SetBelt(Slots.RidingCount, BlockTuning.MaxOnBelt);
        }

        void HandleBlockDestroyed(int x, int y, byte color, Vector3 worldPos)
        {
            Score += BlockTuning.ScorePerBlock;
            Hud?.SetScore(Score);
            Fx?.BlockDestroyed(worldPos, BlockPalette.Get(color));
        }

        void HandleWallCleared()
        {
            if (advancing) return;
            advancing = true;

            Wallet?.Add(BlockTuning.LevelClearKeys);
            Fx?.LevelCleared(new Vector3(Layout.GridCenter.x, Layout.GridCenter.y, 0f));

            Hud?.ShowBanner("CLEARED", 0.5f);

            // Clear the board of towers before the next picture arrives. Towers mid-lap and
            // towers parked in squares used to survive the transition and open the next level
            // already deployed and already shooting.
            Balls.ClearAll();
            Slots.SweepAway();

            var sfx = Sfx.Instance;
            if (sfx != null) StartCoroutine(CoinChime(sfx));
            Level++;
            pictureIndex++;

            if (GameSession.Mode == GameMode.Levels) PlayerProfile.ReportLevel(Level);

            // Long enough for the sweep to finish and the CLEARED banner to land.
            Invoke(nameof(StartLevel), 1.5f);
        }

        /// <summary>
        /// Coins land a beat after the clear fanfare rather than on top of it, so the reward reads
        /// as a separate event instead of thickening the same chord.
        /// </summary>
        System.Collections.IEnumerator CoinChime(Sfx sfx)
        {
            yield return new WaitForSeconds(0.45f);
            sfx.Play(sfx.Coin, 0.55f);
        }

        void HandleAllTowersLost()
        {
            // Shots already in flight may still finish the picture, so give them a moment before
            // calling it — losing on a technicality while a winning shot is mid-air feels awful.
            if (advancing || IsGameOver) return;
            Invoke(nameof(ConfirmGameOver), 1.2f);
        }

        void ConfirmGameOver()
        {
            if (advancing || IsGameOver) return;
            if (Wall.RemainingBlocks <= 0) return;

            IsGameOver = true;
            Balls.ClearAll();

            PlayerProfile.ReportScore(GameSession.Mode, Score);

            var sfx = Sfx.Instance;
            sfx?.ResetStreak();
            sfx?.Play(sfx.Defeat, 0.7f);

            Hud?.ShowGameOver(Score);
            OnGameOver?.Invoke(Score);
        }

        /// <summary>
        /// Play again from the start of the mode — wired to the game-over panel's retry button.
        /// Costs a heart, same as launching from the menu; without that, retry would be a way to
        /// play for free forever and the whole hearts economy would be decoration.
        /// </summary>
        public void Restart()
        {
            if (!PlayerProfile.TrySpendHeart())
            {
                RequestExit();
                return;
            }

            CancelInvoke();
            Score = 0;
            ApplyMode();
            IsGameOver = false;
            StartLevel();
        }
    }
}

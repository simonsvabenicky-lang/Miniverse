using System;
using UnityEngine;

namespace FlowSort.Meta
{
    public enum GameMode { Levels = 0, Endless = 1, Daily = 2 }

    /// <summary>
    /// Everything that survives between sessions: hearts, coins, best scores and the sound
    /// setting. Static and PlayerPrefs-backed, so the menu and the game read the same numbers
    /// without either having to own the other or a manager object having to survive a scene load.
    ///
    /// Hearts regenerate on the wall clock rather than on play time, which is the point of them —
    /// so the count is derived on read from the stored value plus however long has passed, never
    /// ticked by an Update loop that only runs while the app is open.
    /// </summary>
    public static class PlayerProfile
    {
        public const int MaxHearts = 5;

        /// <summary>Real time to earn one heart back.</summary>
        public static readonly TimeSpan HeartRegen = TimeSpan.FromMinutes(10);

        const string HeartsKey = "fs_hearts";
        const string RegenKey = "fs_heart_due";      // UTC ticks the next heart lands
        const string CoinsKey = "fs_coins";
        const string SoundKey = "fs_sound";
        const string LevelKey = "fs_best_level";
        const string ScoreKey = "fs_best_score_";    // + mode index

        // --- Hearts ---

        public static int Hearts
        {
            get
            {
                Settle();
                return PlayerPrefs.GetInt(HeartsKey, MaxHearts);
            }
        }

        /// <summary>Time until the next heart arrives, or zero when hearts are full.</summary>
        public static TimeSpan TimeToNextHeart
        {
            get
            {
                Settle();
                if (PlayerPrefs.GetInt(HeartsKey, MaxHearts) >= MaxHearts) return TimeSpan.Zero;

                var due = new DateTime(long.Parse(PlayerPrefs.GetString(RegenKey, "0")), DateTimeKind.Utc);
                var left = due - DateTime.UtcNow;
                return left > TimeSpan.Zero ? left : TimeSpan.Zero;
            }
        }

        public static bool TrySpendHeart()
        {
            Settle();

            int hearts = PlayerPrefs.GetInt(HeartsKey, MaxHearts);
            if (hearts <= 0) return false;

            // Dropping below full starts the clock; it is already running otherwise.
            if (hearts == MaxHearts) SetDue(DateTime.UtcNow + HeartRegen);

            PlayerPrefs.SetInt(HeartsKey, hearts - 1);
            PlayerPrefs.Save();
            return true;
        }

        public static void AddHearts(int amount)
        {
            Settle();

            int hearts = Mathf.Clamp(PlayerPrefs.GetInt(HeartsKey, MaxHearts) + amount, 0, MaxHearts);
            PlayerPrefs.SetInt(HeartsKey, hearts);
            if (hearts >= MaxHearts) PlayerPrefs.DeleteKey(RegenKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Credits whatever hearts the wall clock owes since the last read. Rolls the due time
        /// forward one interval per heart rather than resetting it to now, so time spent with the
        /// app closed is never quietly discarded.
        /// </summary>
        static void Settle()
        {
            int hearts = PlayerPrefs.GetInt(HeartsKey, MaxHearts);
            if (hearts >= MaxHearts) return;

            string raw = PlayerPrefs.GetString(RegenKey, "");
            if (string.IsNullOrEmpty(raw) || !long.TryParse(raw, out long ticks))
            {
                SetDue(DateTime.UtcNow + HeartRegen);
                return;
            }

            var due = new DateTime(ticks, DateTimeKind.Utc);
            var now = DateTime.UtcNow;

            // A clock moved backwards would otherwise leave the player waiting indefinitely.
            if (due - now > HeartRegen)
            {
                SetDue(now + HeartRegen);
                return;
            }

            bool changed = false;
            while (now >= due && hearts < MaxHearts)
            {
                hearts++;
                due += HeartRegen;
                changed = true;
            }

            if (!changed) return;

            PlayerPrefs.SetInt(HeartsKey, hearts);
            if (hearts >= MaxHearts) PlayerPrefs.DeleteKey(RegenKey);
            else SetDue(due);
            PlayerPrefs.Save();
        }

        static void SetDue(DateTime utc) => PlayerPrefs.SetString(RegenKey, utc.Ticks.ToString());

        // --- Coins ---

        public static int Coins => PlayerPrefs.GetInt(CoinsKey, 0);

        public static void AddCoins(int amount)
        {
            PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, Coins + amount));
            PlayerPrefs.Save();
        }

        public static bool TrySpendCoins(int amount)
        {
            if (Coins < amount) return false;
            AddCoins(-amount);
            return true;
        }

        // --- Landing squares ---

        public const int MinLandingSlots = 4;
        public const int MaxLandingSlots = 6;

        /// <summary>
        /// How many landing squares the player owns. This is the game's real progression: the
        /// squares are a throughput cap, so buying one is the only upgrade that changes how much
        /// of a hand you can actually get through.
        /// </summary>
        public static int LandingSlots => Mathf.Clamp(
            PlayerPrefs.GetInt(SlotsKey, MinLandingSlots), MinLandingSlots, MaxLandingSlots);

        /// <summary>Coins for the next square, rising steeply — it is meant to be a goal.</summary>
        public static int NextSlotCost => 250 * (1 + LandingSlots - MinLandingSlots);

        public static bool TryBuyLandingSlot()
        {
            if (LandingSlots >= MaxLandingSlots) return false;
            if (!TrySpendCoins(NextSlotCost)) return false;

            PlayerPrefs.SetInt(SlotsKey, LandingSlots + 1);
            PlayerPrefs.Save();
            return true;
        }

        const string SlotsKey = "fs_slots";

        // --- Records ---

        public static int BestScore(GameMode mode) => PlayerPrefs.GetInt(ScoreKey + (int)mode, 0);

        public static void ReportScore(GameMode mode, int score)
        {
            if (score <= BestScore(mode)) return;
            PlayerPrefs.SetInt(ScoreKey + (int)mode, score);
            PlayerPrefs.Save();
        }

        public static int BestLevel => PlayerPrefs.GetInt(LevelKey, 1);

        public static void ReportLevel(int level)
        {
            if (level <= BestLevel) return;
            PlayerPrefs.SetInt(LevelKey, level);
            PlayerPrefs.Save();
        }

        // --- Settings ---

        public static bool SoundOn
        {
            get => PlayerPrefs.GetInt(SoundKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(SoundKey, value ? 1 : 0);
                PlayerPrefs.Save();
                AudioListener.volume = value ? 1f : 0f;
            }
        }

        /// <summary>Applies the stored sound setting. Called on load by both scenes.</summary>
        public static void ApplySound() => AudioListener.volume = SoundOn ? 1f : 0f;
    }

    /// <summary>
    /// What the next loaded game should play. Set by the menu, read by BlockBreakGame — a static
    /// rather than a DontDestroyOnLoad object, because two fields do not need a lifetime.
    /// </summary>
    public static class GameSession
    {
        public static GameMode Mode = GameMode.Levels;

        /// <summary>Scene the menu lives in; the game's exit button returns here.</summary>
        public const string MenuScene = "Menu";
        public const string GameScene = "Main";

        /// <summary>Stable per-day seed, so everyone's daily board is the same one.</summary>
        public static int DailySeed => (int)(DateTime.UtcNow.Date.Ticks / TimeSpan.TicksPerDay);
    }
}

using UnityEngine;

namespace Miniverse.Hub
{
    /// <summary>
    /// The hub-level meta-currency and lives counters shown in Home's top bar. Nothing spends or
    /// grants these yet -- no graduated game is wired to a shared economy -- so this deliberately
    /// starts with a fixed opening balance rather than pretending a shop or reward flow exists
    /// (see StorePanel's "coming soon" placeholder for the same honesty). Persisted via
    /// PlayerPrefs, same local-first spirit as AnalyticsService: real, saved numbers, not a
    /// hardcoded "0" that never changes.
    /// </summary>
    public static class HubEconomy
    {
        const string CashKey = "hub_cash";
        const string LivesKey = "hub_lives";
        const int StartingCash = 500;
        const int StartingLives = 5;

        public static event System.Action Changed;

        public static int Cash
        {
            get => PlayerPrefs.GetInt(CashKey, StartingCash);
            private set { PlayerPrefs.SetInt(CashKey, value); Changed?.Invoke(); }
        }

        public static int Lives
        {
            get => PlayerPrefs.GetInt(LivesKey, StartingLives);
            private set { PlayerPrefs.SetInt(LivesKey, value); Changed?.Invoke(); }
        }

        public static void AddCash(int amount) => Cash = Mathf.Max(0, Cash + amount);
        public static void AddLives(int amount) => Lives = Mathf.Max(0, Lives + amount);
    }
}

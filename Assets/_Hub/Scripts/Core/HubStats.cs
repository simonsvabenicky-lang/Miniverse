using UnityEngine;

namespace Miniverse.Hub
{
    /// <summary>
    /// One real, persisted stat for the Profile panel -- games launched, all-time. Deliberately
    /// not derived by re-parsing analytics.jsonl at runtime (LocalFileAnalyticsBackend already
    /// owns that file's format; a second reader here would just be a second place to keep in
    /// sync with it for one number). HubLauncher.LaunchGame increments this alongside its own
    /// AnalyticsService.LogGameLaunch call.
    /// </summary>
    public static class HubStats
    {
        const string GamesPlayedKey = "hub_games_played";

        public static int GamesPlayed => PlayerPrefs.GetInt(GamesPlayedKey, 0);

        public static void IncrementGamesPlayed() =>
            PlayerPrefs.SetInt(GamesPlayedKey, GamesPlayed + 1);
    }
}

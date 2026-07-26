using System.Collections.Generic;

namespace Miniverse.Hub.Analytics
{
    /// <summary>
    /// The one call site every game-lifecycle hook uses (see HubLauncher). Backends are
    /// additive, so wiring in a real dashboard later is "add one more IAnalyticsBackend to
    /// this list", never a change to HubLauncher or any minigame's code.
    /// </summary>
    public static class AnalyticsService
    {
        static readonly List<IAnalyticsBackend> Backends = new() { new LocalFileAnalyticsBackend() };

        public static void LogGameLaunch(string gameId) =>
            Log("game_launch", new Dictionary<string, object> { { "gameId", gameId } });

        public static void LogGameEnd(string gameId, float durationSeconds, int score) =>
            Log("game_end", new Dictionary<string, object>
            {
                { "gameId", gameId },
                { "durationSeconds", durationSeconds },
                { "score", score },
            });

        static void Log(string eventName, Dictionary<string, object> parameters)
        {
            foreach (var backend in Backends)
                backend.LogEvent(eventName, parameters);
        }
    }
}

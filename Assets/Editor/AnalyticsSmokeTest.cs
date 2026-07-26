using UnityEditor;
using UnityEngine;
using Miniverse.Hub.Analytics;

namespace Miniverse.EditorTools
{
    /// <summary>
    /// Fires a fake launch/end event pair so the analytics pipeline can be verified without
    /// a real graduated minigame to play through HubLauncher. Not shipped logic, just a
    /// dev-time check — delete once a real game exercises AnalyticsService for real.
    ///
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Miniverse.EditorTools.AnalyticsSmokeTest.Run
    /// </summary>
    public static class AnalyticsSmokeTest
    {
        [MenuItem("Miniverse/Analytics Smoke Test")]
        public static void Run()
        {
            AnalyticsService.LogGameLaunch("smoke_test_game");
            AnalyticsService.LogGameEnd("smoke_test_game", 12.5f, 340);
            Debug.Log($"[Miniverse] Analytics smoke test wrote to {Application.persistentDataPath}/analytics.jsonl");
        }
    }
}

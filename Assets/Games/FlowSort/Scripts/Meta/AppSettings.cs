using UnityEngine;

namespace FlowSort.Meta
{
    /// <summary>
    /// Runtime settings that belong to the app rather than to any one scene.
    ///
    /// Runs before the first scene loads, so it applies to the menu and the game alike without
    /// either having to know about it — and without a manager object that has to survive a load.
    /// </summary>
    public static class AppSettings
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            // Unity's mobile default is 30, and nothing here ever overrode it. Measured frame
            // times on device sat exactly on the 32ms cap with under 0.1ms of variance across
            // every frame — the GPU was finishing early and waiting, not struggling. Towers move
            // at 21 units a second, so the extra frames are worth having.
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            PlayerProfile.ApplySound();
        }
    }
}

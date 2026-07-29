using UnityEngine;

namespace Miniverse.Hub
{
    /// <summary>
    /// Home's sound toggle and button-click SFX. Muted is persisted (PlayerPrefs) so the choice
    /// survives an app restart, same as Frontline's own Audio.Muted -- the two are independent
    /// static classes rather than a shared one, since Home and a graduated game's own audio never
    /// run in the same scene lifecycle together and sharing state across the hub/game boundary
    /// isn't otherwise needed.
    /// </summary>
    public static class HubAudio
    {
        const string MutedKey = "hub_audio_muted";

        public static bool Muted
        {
            get => PlayerPrefs.GetInt(MutedKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
                AudioListener.volume = value ? 0f : 1f;
            }
        }

        /// <summary>Applies the persisted mute state to AudioListener -- call once when Home loads, since AudioListener.volume itself isn't persisted by Unity.</summary>
        public static void Apply() => AudioListener.volume = Muted ? 0f : 1f;
    }
}

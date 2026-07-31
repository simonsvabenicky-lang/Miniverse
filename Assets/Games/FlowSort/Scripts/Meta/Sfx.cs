using UnityEngine;

namespace FlowSort.Meta
{
    /// <summary>
    /// The one place sound comes out of, in both scenes.
    ///
    /// A small round-robin of AudioSources rather than PlayClipAtPoint, which allocates a
    /// GameObject per sound — at several blocks a second that is the single biggest avoidable
    /// cost in the frame. Clip references are plain public fields assigned by the scene builders,
    /// which is the one category of edit-time state that survives serialisation into a player.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        public static Sfx Instance { get; private set; }

        [Header("Game")]
        public AudioClip[] Notes = System.Array.Empty<AudioClip>();
        public AudioClip Shot;
        public AudioClip Deploy;
        public AudioClip Land;
        public AudioClip LevelComplete;
        public AudioClip Defeat;

        [Header("Shared")]
        public AudioClip Click;
        public AudioClip Confirm;
        public AudioClip Deny;
        public AudioClip Popup;
        public AudioClip Coin;
        public AudioClip Heart;

        const int VoiceCount = 8;

        AudioSource[] voices;
        int cursor;

        /// <summary>How far up the note ladder the current run of breaks has climbed.</summary>
        int streak;
        float streakExpiry;

        void Awake()
        {
            Instance = this;

            voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                var go = new GameObject($"Voice_{i}", typeof(AudioSource));
                go.transform.SetParent(transform, false);

                var src = go.GetComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                voices[i] = src;
            }

            PlayerProfile.ApplySound();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (streak > 0 && Time.time > streakExpiry) streak = 0;
        }

        public void Play(AudioClip clip, float volume = 0.6f, float pitch = 1f)
        {
            if (clip == null || voices == null) return;

            var src = voices[cursor];
            cursor = (cursor + 1) % voices.Length;

            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.Play();
        }

        /// <summary>
        /// A block broke. Each break in quick succession climbs one rung of the note ladder and
        /// the run resets after a short gap, so a good sweep of a colour sounds like an arpeggio
        /// going up rather than the same click twenty times. This is the whole reason the pack's
        /// match-three notes were worth importing over a single hit sound.
        /// </summary>
        public void Break()
        {
            if (Notes == null || Notes.Length == 0) return;

            Play(Notes[Mathf.Min(streak, Notes.Length - 1)], 0.45f);

            streak = Mathf.Min(streak + 1, Notes.Length - 1);
            streakExpiry = Time.time + 0.45f;
        }

        public void ResetStreak() => streak = 0;
    }
}

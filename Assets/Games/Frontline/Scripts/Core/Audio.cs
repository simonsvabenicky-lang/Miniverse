using System.Collections.Generic;
using UnityEngine;

namespace Frontline
{
    /// <summary>Which sound, not which file.</summary>
    public enum Sfx
    {
        Hit,
        Kill,
        /// <summary>The corpse landing, a beat after the killing shot. Not the same event.</summary>
        BodyFall,
        GateTaken,
        GateBad,
        Breach,
    }

    /// <summary>
    /// All sound in the game.
    ///
    /// ONE clip per cue, deliberately. The first version picked at random from 5 "variants" per
    /// cue to avoid repetition -- but Kenney's laserRetro_000..004 are five *different* sounds,
    /// not five takes of one, so the gun shuffled a mixtape 8 times a second. Reported as "like
    /// 3 different sounds mixed into one, tutututu then electro bass then the raygun". Variety
    /// now comes only from pitch jitter, which cannot change what the sound *is*.
    ///
    /// Guns are per-weapon: drop a file named after the gun (AK.wav, Shotgun.wav...) into
    /// Assets/Audio/Guns and SceneBuilder wires it automatically. That's the seam for someone
    /// with ears to fix this without touching code -- which matters, because Claude has none.
    /// </summary>
    public class Audio : MonoBehaviour
    {
        public static Audio Instance { get; private set; }

        /// <summary>Flipped by the settings screen. Simplest possible mute.</summary>
        public static bool Muted;

        [SerializeField] AudioClip _hit;
        [SerializeField] AudioClip _kill;
        [SerializeField] AudioClip _bodyFall;
        [SerializeField] AudioClip _gateTaken;
        [SerializeField] AudioClip _gateBad;
        [SerializeField] AudioClip _breach;

        [SerializeField] AudioClip _defaultShot;
        // Parallel arrays rather than a serialisable dict, which Unity has never supported.
        [SerializeField] string[] _gunNames;
        [SerializeField] AudioClip[] _gunClips;

        readonly Dictionary<string, AudioClip> _guns = new Dictionary<string, AudioClip>();
        AudioSource[] _sources;
        int _next;
        float[] _lastPlayed;

        void Awake()
        {
            Instance = this;

            for (int i = 0; i < _gunNames.Length && i < _gunClips.Length; i++)
                if (_gunClips[i] != null) _guns[_gunNames[i]] = _gunClips[i];

            // A ring of sources: overlapping cues need somewhere to overlap, and allocating an
            // AudioSource per shot at the minigun's 22/sec would not be free.
            _sources = new AudioSource[Tuning.AudioVoices];
            for (int i = 0; i < _sources.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;   // 2D: fixed camera, 6-unit lane, nothing to pan
                _sources[i] = src;
            }

            _lastPlayed = new float[System.Enum.GetValues(typeof(Sfx)).Length + 1];
            for (int i = 0; i < _lastPlayed.Length; i++) _lastPlayed[i] = -99f;
        }

        /// <summary>Shot sound for the equipped gun, falling back to the default.</summary>
        public void PlayShot(string gunMesh, float volume, float pitch = 1f)
        {
            AudioClip clip = gunMesh != null && _guns.TryGetValue(gunMesh, out AudioClip c) ? c : _defaultShot;
            // Index past the Sfx enum: the shot has its own throttle slot.
            if (!Throttle(_lastPlayed.Length - 1, 0.03f)) return;
            PlayClip(clip, volume, pitch);
        }

        public void Play(Sfx sfx, float volume = 1f, float pitch = 1f)
        {
            if (!Throttle((int)sfx, MinGap(sfx))) return;
            PlayClip(Clip(sfx), volume, pitch);
        }

        bool Throttle(int slot, float minGap)
        {
            if (Time.unscaledTime - _lastPlayed[slot] < minGap) return false;
            _lastPlayed[slot] = Time.unscaledTime;
            return true;
        }

        void PlayClip(AudioClip clip, float volume, float pitch = 1f)
        {
            if (clip == null || Muted) return;

            AudioSource src = _sources[_next];
            _next = (_next + 1) % _sources.Length;

            src.clip = clip;
            src.volume = Mathf.Clamp01(volume * Tuning.MasterVolume);
            src.pitch = pitch * Random.Range(1f - Tuning.SfxPitchJitter, 1f + Tuning.SfxPitchJitter);
            src.Play();
        }

        AudioClip Clip(Sfx sfx) => sfx switch
        {
            Sfx.Hit => _hit,
            Sfx.Kill => _kill,
            Sfx.BodyFall => _bodyFall,
            Sfx.GateTaken => _gateTaken,
            Sfx.GateBad => _gateBad,
            Sfx.Breach => _breach,
            _ => null,
        };

        static float MinGap(Sfx sfx) => sfx switch
        {
            Sfx.Hit => 0.04f,
            Sfx.Kill => 0.05f,
            Sfx.BodyFall => 0.05f,
            _ => 0f,
        };
    }
}

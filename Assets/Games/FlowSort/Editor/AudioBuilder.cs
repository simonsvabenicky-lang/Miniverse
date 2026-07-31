using FlowSort.Meta;
using UnityEditor;
using UnityEngine;

namespace FlowSort.EditorTools
{
    /// <summary>
    /// Creates the shared Sfx player and assigns its clips. Both scene builders call it, so the
    /// menu and the game get an identical sound setup from one list rather than two that drift.
    /// </summary>
    public static class AudioBuilder
    {
        

        /// <summary>Rungs of the ladder played on consecutive block breaks. See Sfx.Break.</summary>
        const int NoteCount = 8;

        /// <param name="music">Menu and game both get the bed; it restarts on the scene change.</param>
        public static Sfx Build(bool music = true)
        {
            var go = new GameObject("Sfx", typeof(Sfx));
            var sfx = go.GetComponent<Sfx>();

            if (music)
            {
                var bed = new GameObject("MusicBed", typeof(AudioSource), typeof(MusicBed));
                bed.transform.SetParent(go.transform, false);
            }

            var notes = new AudioClip[NoteCount];
            for (int i = 0; i < NoteCount; i++) notes[i] = Clip($"Note_{i + 1}");
            sfx.Notes = notes;

            sfx.Shot = Clip("Shot");
            sfx.Deploy = Clip("Deploy");
            sfx.Land = Clip("Land");
            sfx.LevelComplete = Clip("LevelComplete");
            sfx.Defeat = Clip("Defeat");

            sfx.Click = Clip("Click");
            sfx.Confirm = Clip("Confirm");
            sfx.Deny = Clip("Deny");
            sfx.Popup = Clip("Popup");
            sfx.Coin = Clip("Coin");
            sfx.Heart = Clip("Heart");

            return sfx;
        }

        static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{ProjectPaths.Audio}/{name}.wav");
            if (clip == null) Debug.LogError($"[FlowSort] Missing audio clip {ProjectPaths.Audio}/{name}.wav");
            return clip;
        }
    }
}

using UnityEngine;

namespace FlowSort.Meta
{
    /// <summary>
    /// A quiet looping music bed, synthesised at load rather than streamed from a file.
    ///
    /// The asset library has no music in it — only effects and long location ambiences — and a
    /// puzzle game does not want a blacksmith forge under it. Generating the loop keeps
    /// provenance unambiguous (it is original by construction, like the pictures) and costs a few
    /// hundred KB of RAM instead of a few MB of APK.
    ///
    /// It is deliberately plain: a four-chord progression, a triangle-ish arpeggio and a soft
    /// bass, mixed low. Background music that asks for attention is worse than none.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MusicBed : MonoBehaviour
    {
        /// <summary>
        /// Loud enough to actually be a backing track. At 0.16, against effects mixed at 0.45 and
        /// up, it was there but inaudible — which is indistinguishable from not being there.
        /// </summary>
        [Range(0f, 1f)] public float Volume = 0.5f;

        const int SampleRate = 22050;
        const float Bpm = 96f;

        /// <summary>Semitone offsets of the four chord roots, relative to A2. Am - F - C - G.</summary>
        static readonly int[] Roots = { 0, -4, -9, -2 };

        /// <summary>
        /// Two melody notes per bar, as semitone offsets from that bar's root. Chord tones and
        /// their neighbours, so it stays consonant whatever the progression does under it.
        /// </summary>
        static readonly int[] Melody = { 7, 12, 4, 7, 7, 4, 12, 7 };

        /// <summary>Minor, major, major, major over those roots.</summary>
        static readonly int[][] Chords =
        {
            new[] { 0, 3, 7 },
            new[] { 0, 4, 7 },
            new[] { 0, 4, 7 },
            new[] { 0, 4, 7 },
        };

        void Awake()
        {
            var source = GetComponent<AudioSource>();
            source.clip = Generate();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = Volume;
            source.Play();
        }

        AudioClip Generate()
        {
            float beat = 60f / Bpm;
            float barLength = beat * 4f;
            int barSamples = Mathf.RoundToInt(barLength * SampleRate);
            int total = barSamples * Roots.Length;

            var data = new float[total];

            for (int bar = 0; bar < Roots.Length; bar++)
            {
                int root = Roots[bar];
                var chord = Chords[bar];

                // Bass: the root, one note per half bar so it has a pulse instead of a drone.
                AddNote(data, bar * barSamples, barSamples / 2, Midi(root - 12), 0.42f, 0.5f, sine: true);
                AddNote(data, bar * barSamples + barSamples / 2, barSamples / 2,
                        Midi(root - 12), 0.34f, 0.5f, sine: true);

                // Arpeggio: eighth notes walking up and back down the triad.
                int stepSamples = Mathf.RoundToInt(beat * 0.5f * SampleRate);
                int steps = barSamples / stepSamples;

                for (int s = 0; s < steps; s++)
                {
                    int rung = s % (chord.Length * 2 - 2);
                    if (rung >= chord.Length) rung = chord.Length * 2 - 2 - rung;

                    int semitone = root + chord[rung] + (s >= steps / 2 ? 12 : 0);
                    AddNote(data, bar * barSamples + s * stepSamples, stepSamples,
                            Midi(semitone), 0.22f, 0.5f, sine: false);
                }

                // Melody: two held notes a bar, high enough to sit above the arpeggio. Without a
                // line to follow, the bed reads as texture rather than as a piece of music.
                int half = barSamples / 2;
                AddNote(data, bar * barSamples, half,
                        Midi(root + Melody[bar * 2] + 12), 0.3f, 1.1f, sine: true);
                AddNote(data, bar * barSamples + half, half,
                        Midi(root + Melody[bar * 2 + 1] + 12), 0.26f, 1.1f, sine: true);
            }

            Soften(data);

            var clip = AudioClip.Create("MusicBed", total, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A2 is the tonic; offsets are semitones from it.</summary>
        static float Midi(int semitonesFromA2) => 110f * Mathf.Pow(2f, semitonesFromA2 / 12f);

        /// <summary>
        /// Adds one note with a short attack and an exponential decay. The decay is what keeps
        /// the arpeggio from smearing into a drone at this tempo.
        /// </summary>
        static void AddNote(float[] data, int start, int length, float frequency,
                            float amplitude, float decay, bool sine)
        {
            float step = frequency / SampleRate;
            float phase = 0f;

            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (index >= data.Length) break;

                float t = i / (float)length;
                float attack = Mathf.Min(1f, t * 40f);
                float envelope = attack * Mathf.Exp(-t / decay);

                float wave = sine
                    ? Mathf.Sin(phase * Mathf.PI * 2f)
                    : Mathf.Abs(phase * 2f - 1f) * 2f - 1f;   // triangle

                data[index] += wave * envelope * amplitude;

                phase += step;
                if (phase >= 1f) phase -= 1f;
            }
        }

        /// <summary>
        /// A one-pole lowpass plus a loop-seam crossfade. The filter takes the edge off the
        /// triangle wave; the seam blend stops the loop point from clicking, which is the thing
        /// that would make a generated bed obviously generated.
        /// </summary>
        static void Soften(float[] data)
        {
            // Gentler than before: at 0.28 this was rolling off most of what made the arpeggio
            // and melody audible, on top of the volume being far too low.
            float previous = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                previous = Mathf.Lerp(previous, data[i], 0.55f);
                data[i] = Mathf.Clamp(previous, -0.95f, 0.95f);
            }

            int blend = Mathf.Min(2048, data.Length / 8);
            for (int i = 0; i < blend; i++)
            {
                float t = i / (float)blend;
                int tail = data.Length - blend + i;
                float mixed = Mathf.Lerp(data[tail], data[i], t);
                data[tail] = mixed;
            }
        }
    }
}

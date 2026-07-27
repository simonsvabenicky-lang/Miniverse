using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Frontline.EditorTools
{
    /// <summary>
    /// Measures every clip: length, peak and RMS.
    ///
    /// This exists because Claude cannot hear, and guessing at the mix by reading filenames has
    /// now been wrong twice ("cant hear a thump" two builds running, after raising the volume
    /// each time). Loudness and length are numbers, and numbers can be measured -- so measure
    /// them instead of arguing about them. It won't tell you whether a sound is *good*, but it
    /// will tell you why one is inaudible next to another.
    ///
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Frontline.EditorTools.AudioReport.Run
    /// </summary>
    public static class AudioReport
    {
        [MenuItem("Frontline/Report Audio Levels")]
        public static void Run()
        {
            var rows = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .ToArray();

            Debug.Log($"[Frontline] ---- AUDIO REPORT ({rows.Length} clips) ----");
            foreach (string path in rows)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;

                // GetData needs the samples decompressed and resident; an .ogg left on its
                // default import settings will hand back silence and quietly tell you nothing.
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                // preloadAudioData moved onto the per-platform sample settings in Unity 6.
                if (settings.loadType != AudioClipLoadType.DecompressOnLoad || !settings.preloadAudioData)
                {
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    settings.preloadAudioData = true;
                    importer.defaultSampleSettings = settings;
                    importer.SaveAndReimport();
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }

                var data = new float[clip.samples * clip.channels];
                if (!clip.GetData(data, 0))
                {
                    Debug.LogWarning($"[Frontline] {clip.name}: GetData failed");
                    continue;
                }

                float peak = 0f, sumSq = 0f, diffSq = 0f;
                for (int i = 0; i < data.Length; i++)
                {
                    float a = Mathf.Abs(data[i]);
                    if (a > peak) peak = a;
                    sumSq += data[i] * data[i];
                    // First-order difference = a crude high-pass. Its energy relative to the
                    // whole is a decent proxy for brightness without writing an FFT.
                    if (i > 0)
                    {
                        float d = data[i] - data[i - 1];
                        diffSq += d * d;
                    }
                }
                float rms = data.Length > 0 ? Mathf.Sqrt(sumSq / data.Length) : 0f;
                float diffRms = data.Length > 1 ? Mathf.Sqrt(diffSq / (data.Length - 1)) : 0f;

                // Why this matters more than loudness on this project: phone speakers roll off
                // hard below a few hundred Hz, so a bass-heavy clip is one the device physically
                // throws away however loud it measures. The sniper was peak 0.904 and inaudible.
                // Low bright = the phone will not play it, whatever you set the volume to.
                float bright = rms > 0f ? diffRms / rms : 0f;

                Debug.Log($"[Frontline] {clip.name,-30} len={clip.length,5:F2}s  peak={peak,5:F3}  " +
                          $"rms={rms,6:F4}  bright={bright,5:F3}");
            }
        }
    }
}

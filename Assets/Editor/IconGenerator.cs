using System.IO;
using UnityEditor;
using UnityEngine;

namespace Miniverse.EditorTools
{
    /// <summary>
    /// Draws a placeholder app icon in code — a small cluster of "planet" circles on a dark
    /// background, standing in for "many little game-worlds in one app" until real branding
    /// exists. Same reasoning as everything else in Assets/Editor: generated, not hand-drawn,
    /// so swapping the design later is a script edit, not an asset hunt.
    ///
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Miniverse.EditorTools.IconGenerator.Generate
    /// </summary>
    public static class IconGenerator
    {
        static readonly Color BackgroundColor = new Color32(0x22, 0x19, 0x33, 0xFF);
        static readonly Color CoreColor = new Color32(0x8A, 0x6D, 0xE9, 0xFF);
        static readonly Color CoreShadeColor = new Color32(0x5B, 0x43, 0xA8, 0xFF);

        static readonly (Vector2 offset, float radiusFrac, Color color)[] Satellites =
        {
            (new Vector2(-0.30f, 0.28f), 0.14f, new Color32(0xFF, 0x8A, 0x5B, 0xFF)), // coral
            (new Vector2(0.32f, 0.24f), 0.11f, new Color32(0x5B, 0xD6, 0xC4, 0xFF)),  // teal
            (new Vector2(0.28f, -0.30f), 0.10f, new Color32(0xFF, 0xD1, 0x5B, 0xFF)), // yellow
            (new Vector2(-0.26f, -0.30f), 0.08f, new Color32(0xFF, 0x6B, 0x9E, 0xFF)), // pink
        };

        const string SourceDir = "Assets/_Hub/Art/Icon";

        [MenuItem("Miniverse/Generate Placeholder App Icon")]
        public static void Generate()
        {
            Directory.CreateDirectory(SourceDir);

            SaveIcon(DrawLegacy(1024), $"{SourceDir}/AppIcon_Legacy.png");
            SaveIcon(DrawAdaptiveForeground(1024), $"{SourceDir}/AppIcon_AdaptiveFG.png");
            SaveIcon(DrawAdaptiveBackground(1024), $"{SourceDir}/AppIcon_AdaptiveBG.png");
            AssetDatabase.Refresh();

            ApplyToPlayerSettings();
            Debug.Log("[Miniverse] Placeholder app icon generated and assigned to Android Player Settings.");
        }

        static void SaveIcon(Texture2D tex, string path)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        // --- drawing ---

        static Texture2D DrawLegacy(int size)
        {
            var tex = NewTexture(size);
            float radius = size * 0.5f;
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float cornerRadius = size * 0.18f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (!InsideRoundedSquare(x, y, size, cornerRadius))
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }
                    tex.SetPixel(x, y, BackgroundColor);
                }
            }

            PaintCluster(tex, size, center, radius * 0.62f, opaqueBackground: true);
            tex.Apply();
            return tex;
        }

        static Texture2D DrawAdaptiveForeground(int size)
        {
            var tex = NewTexture(size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, Color.clear);

            // Android's adaptive-icon mask can crop up to ~33% off each edge, so foreground
            // content has to stay inside the inner "safe zone" or it gets clipped on-device.
            Vector2 center = new Vector2(size / 2f, size / 2f);
            PaintCluster(tex, size, center, size * 0.5f * 0.42f, opaqueBackground: false);
            tex.Apply();
            return tex;
        }

        static Texture2D DrawAdaptiveBackground(int size)
        {
            var tex = NewTexture(size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, BackgroundColor);
            tex.Apply();
            return tex;
        }

        static void PaintCluster(Texture2D tex, int size, Vector2 center, float coreRadius, bool opaqueBackground)
        {
            // Core "home world" — two-tone circle to read as a sphere rather than a flat disc.
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y);
                    float d = Vector2.Distance(p, center);
                    if (d > coreRadius) continue;

                    bool lowerRight = (x - center.x) + (center.y - y) < coreRadius * 0.25f;
                    tex.SetPixel(x, y, lowerRight ? CoreShadeColor : CoreColor);
                }
            }

            // Satellite "mini-game" circles orbiting the core.
            foreach (var (offset, radiusFrac, color) in Satellites)
            {
                Vector2 satCenter = center + offset * size * 0.5f;
                float satRadius = size * radiusFrac;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        if (Vector2.Distance(new Vector2(x, y), satCenter) <= satRadius)
                            tex.SetPixel(x, y, color);
                    }
                }
            }
        }

        static bool InsideRoundedSquare(int x, int y, int size, float cornerRadius)
        {
            float px = x, py = y;
            float minX = 0, minY = 0, maxX = size, maxY = size;

            float nearestX = Mathf.Clamp(px, minX + cornerRadius, maxX - cornerRadius);
            float nearestY = Mathf.Clamp(py, minY + cornerRadius, maxY - cornerRadius);

            if (px >= minX + cornerRadius && px <= maxX - cornerRadius) return py >= minY && py <= maxY;
            if (py >= minY + cornerRadius && py <= maxY - cornerRadius) return px >= minX && px <= maxX;

            return Vector2.Distance(new Vector2(px, py), new Vector2(nearestX, nearestY)) <= cornerRadius;
        }

        static Texture2D NewTexture(int size) => new Texture2D(size, size, TextureFormat.RGBA32, false);

        // --- Player Settings wiring ---

        static void ApplyToPlayerSettings()
        {
            // Sticking to the long-stable per-target-group icon API rather than the newer
            // per-kind adaptive-icon one (UnityEditor.PlatformIconKind's actual member names
            // aren't consistent across Unity versions/platform extension DLLs, and guessing
            // wrong just fails a headless build). This fills every required Android icon slot
            // with the same square artwork — correct for a legacy/round icon, and adaptive
            // will crop it to a circle, which the centered circle-cluster design already
            // accounts for. Good enough for a placeholder; revisit with true adaptive
            // foreground/background layers once real branding exists.
            var legacy = AssetDatabase.LoadAssetAtPath<Texture2D>($"{SourceDir}/AppIcon_Legacy.png");

            var group = BuildTargetGroup.Android;
            var sizes = PlayerSettings.GetIconSizesForTargetGroup(group);
            var icons = new Texture2D[sizes.Length];
            for (int i = 0; i < sizes.Length; i++) icons[i] = legacy;
            PlayerSettings.SetIconsForTargetGroup(group, icons);
        }
    }
}

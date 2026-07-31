using System.IO;
using FlowSort.Blocks;
using UnityEditor;
using UnityEngine;

namespace FlowSort.EditorTools
{
    /// <summary>
    /// Bakes the five rounded-square button faces into the single block sheet the wall samples.
    ///
    /// The sources are 357px UI art; blocks render at roughly 40px on a phone, so they are boxed
    /// down to BlockAtlas.TilePixels with a transparent gutter around each. Baking rather than
    /// sampling five textures is what keeps the whole wall on one material.
    ///
    /// Menu: FlowSort/Build Block Atlas. SceneBuilder runs this first so a scene rebuild always
    /// ships a sheet that matches the current palette.
    /// </summary>
    public static class BlockAtlasBuilder
    {
        public static string AtlasPath => ProjectPaths.BlockAtlas;
        

        /// <summary>Source file per palette index — index 0 is empty and has no sprite.</summary>
        static readonly string[] SourceNames =
        {
            null, "Block_Red", "Block_Orange", "Block_Green", "Block_Blue", "Block_White",
        };

        [MenuItem("FlowSort/Build Block Atlas")]
        public static Texture2D Build()
        {
            var atlas = new Texture2D(BlockAtlas.Width, BlockAtlas.Height, TextureFormat.RGBA32, false);

            var clear = new Color32[BlockAtlas.Width * BlockAtlas.Height];
            atlas.SetPixels32(clear);

            for (byte i = 1; i < BlockPalette.Count && i < SourceNames.Length; i++)
            {
                var src = LoadReadable($"{ProjectPaths.GuiBundle}/Blocks/{SourceNames[i]}.png");
                if (src == null) continue;

                var rect = BlockAtlas.TileRect(i);
                Blit(src, atlas, rect);
                Object.DestroyImmediate(src);
            }

            atlas.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(AtlasPath));
            File.WriteAllBytes(AtlasPath, atlas.EncodeToPNG());
            Object.DestroyImmediate(atlas);

            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);

            var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            Debug.Log($"[FlowSort] Block atlas baked: {BlockAtlas.Width}x{BlockAtlas.Height}, " +
                      $"{BlockPalette.Count - 1} tiles -> {AtlasPath}");
            return imported;
        }

        /// <summary>
        /// Reads a source PNG straight off disk rather than through the imported Texture2D, which
        /// avoids depending on whatever compression or Read/Write setting the importer applied.
        /// </summary>
        static Texture2D LoadReadable(string assetPath)
        {
            if (!File.Exists(assetPath))
            {
                Debug.LogError($"[FlowSort] Block source missing: {assetPath}");
                return null;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(File.ReadAllBytes(assetPath))) return tex;

            Debug.LogError($"[FlowSort] Could not decode {assetPath}");
            Object.DestroyImmediate(tex);
            return null;
        }

        /// <summary>Box-filtered downscale of the whole source into a destination pixel rect.</summary>
        static void Blit(Texture2D src, Texture2D dst, RectInt rect)
        {
            var srcPixels = src.GetPixels32();
            var outPixels = new Color32[rect.width * rect.height];

            float sx = src.width / (float)rect.width;
            float sy = src.height / (float)rect.height;
            int boxW = Mathf.Max(1, Mathf.FloorToInt(sx));
            int boxH = Mathf.Max(1, Mathf.FloorToInt(sy));

            for (int y = 0; y < rect.height; y++)
            {
                int y0 = Mathf.FloorToInt(y * sy);
                for (int x = 0; x < rect.width; x++)
                {
                    int x0 = Mathf.FloorToInt(x * sx);
                    int r = 0, g = 0, b = 0, a = 0, n = 0;

                    for (int by = 0; by < boxH; by++)
                    {
                        int py = Mathf.Min(y0 + by, src.height - 1);
                        for (int bx = 0; bx < boxW; bx++)
                        {
                            int px = Mathf.Min(x0 + bx, src.width - 1);
                            var c = srcPixels[py * src.width + px];
                            r += c.r; g += c.g; b += c.b; a += c.a;
                            n++;
                        }
                    }

                    outPixels[y * rect.width + x] =
                        new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), (byte)(a / n));
                }
            }

            dst.SetPixels32(rect.xMin, rect.yMin, rect.width, rect.height, outPixels);
        }
    }
}

using UnityEngine;

namespace FlowSort.Blocks
{
    /// <summary>
    /// Layout of the block sprite sheet, shared by the editor tool that bakes it
    /// (BlockAtlasBuilder) and the mesh builder that samples it (WallMesh).
    ///
    /// Every block is one of the Basic GUI Bundle's rounded-square button faces, scaled down and
    /// packed into a single texture. Because the colour IS the sub-image, the whole wall stays on
    /// one material and one texture fetch while keeping the sprites' baked highlight, inner
    /// shadow and hard black outline exactly as authored — none of which a tinted flat quad and a
    /// procedural bevel ever managed to fake.
    ///
    /// Cells are padded so mip levels cannot bleed one colour into its neighbour.
    /// </summary>
    public static class BlockAtlas
    {
        public const int Cols = 4;
        public const int Rows = 2;

        /// <summary>Cell pitch in pixels. One cell per palette colour.</summary>
        public const int Cell = 128;

        /// <summary>Transparent gutter inside each cell.</summary>
        public const int Pad = 8;

        public const int TilePixels = Cell - Pad * 2;

        public const int Width = Cols * Cell;
        public const int Height = Rows * Cell;

        /// <summary>Pixel rect of the drawn sprite for a palette index (1..BlockPalette.Count-1).</summary>
        public static RectInt TileRect(byte colorIndex)
        {
            int k = Mathf.Clamp(colorIndex - 1, 0, Cols * Rows - 1);
            return new RectInt((k % Cols) * Cell + Pad, (k / Cols) * Cell + Pad, TilePixels, TilePixels);
        }

        /// <summary>Normalised UV rect (xy = min, zw = max) for a palette index.</summary>
        public static Vector4 TileUv(byte colorIndex)
        {
            var r = TileRect(colorIndex);
            return new Vector4(
                r.xMin / (float)Width,
                r.yMin / (float)Height,
                r.xMax / (float)Width,
                r.yMax / (float)Height);
        }
    }
}

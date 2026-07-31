using UnityEngine;

namespace FlowSort.Blocks
{
    /// <summary>
    /// Block colours. Index 0 is reserved for "empty" so a PixelPicture cell byte maps directly
    /// to a palette index with no offset arithmetic.
    ///
    /// The five block colours are sampled straight from the Basic GUI Bundle rounded-square
    /// sprites the wall actually renders (see BlockAtlas), so a particle, a tower tint and the
    /// block it came from are the same colour by construction rather than by eye. There is no
    /// grey, black or brown entry: outlines come baked into the sprites, so nothing in the palette
    /// needs to be a dark ink.
    /// </summary>
    public static class BlockPalette
    {
        /// <summary>Palette entries including index 0 (empty). Block colours are 1..Count-1.</summary>
        public const int Count = 6;

        static readonly Color32[] Colors =
        {
            new Color32(0x00, 0x00, 0x00, 0x00), // 0 - empty
            new Color32(0xDC, 0x00, 0x3E, 0xFF), // 1 - red
            new Color32(0xE8, 0x6E, 0x00, 0xFF), // 2 - orange
            new Color32(0x2D, 0xA5, 0x00, 0xFF), // 3 - green
            new Color32(0x00, 0x7E, 0xE0, 0xFF), // 4 - blue
            new Color32(0xFF, 0xFF, 0xFF, 0xFF), // 5 - white
        };

        // --- Scene colours ---
        //
        // A deep desaturated indigo, bracketing #383C62. The violet it replaced was bright but it
        // competed with the blocks instead of carrying them: a saturated background leaves nothing
        // for a saturated block to pop against. This one is dark and low-chroma so all five block
        // colours read at full strength on it.

        public static readonly Color32 BackgroundBottom = new Color32(0x32, 0x36, 0x5A, 0xFF);
        public static readonly Color32 BackgroundTop = new Color32(0x40, 0x44, 0x6E, 0xFF);

        /// <summary>Recess behind the blocks, so gaps in the picture read as depth, not sky.</summary>
        public static readonly Color32 GridWell = new Color32(0x2B, 0x2E, 0x4E, 0xFF);

        /// <summary>Runway strip behind each queue line.</summary>
        public static readonly Color32 LineRunway = new Color32(0x45, 0x4A, 0x78, 0xFF);

        // --- Track (RacingKit) ---
        // Keyed by the FBX's own material names. Indigo tarmac, white markings, gold verge —
        // none of which collide with a block colour, so the belt never reads as a target.

        public static readonly Color32 TrackRoad = new Color32(0x3B, 0x2F, 0xA8, 0xFF);
        public static readonly Color32 TrackMarking = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        public static readonly Color32 TrackVerge = new Color32(0xFF, 0xC2, 0x2E, 0xFF);
        /// <summary>Kerb and gantry red. A true red rather than the pink it started as.</summary>
        public static readonly Color32 TrackBarrierA = new Color32(0xE8, 0x1F, 0x3C, 0xFF);
        public static readonly Color32 TrackBarrierB = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

        public static readonly Color32 SlotFrame = new Color32(0x2A, 0xDC, 0xE0, 0xFF);
        public static readonly Color32 TextInk = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        public static readonly Color32 TextAccent = new Color32(0xFF, 0xD3, 0x5C, 0xFF);

        public static Color32 Get(byte index) => Colors[index < Count ? index : 0];

        /// <summary>
        /// The same colour, ready to be written into a mesh's vertex colours.
        ///
        /// Unity converts colours set through Material/UI APIs into the project's working colour
        /// space, but vertex colours are handed to the shader raw. In a linear project that means
        /// an authored sRGB value is read as if it were already linear and comes out visibly
        /// washed out — a deep indigo track rendered as pale lavender until this was applied.
        /// </summary>
        public static Color32 ToVertex(Color32 c) =>
            QualitySettings.activeColorSpace == ColorSpace.Linear ? (Color32)((Color)c).linear : c;

        /// <summary>Block colour, colour-space corrected for direct use as a vertex colour.</summary>
        public static Color32 GetVertex(byte index) => ToVertex(Get(index));

        /// <summary>Palette indices that count as real block colours (excludes empty).</summary>
        public static byte RandomBlockIndex() => (byte)Random.Range(1, Count);
    }
}

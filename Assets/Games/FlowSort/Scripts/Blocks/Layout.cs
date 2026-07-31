using UnityEngine;

namespace FlowSort.Blocks
{
    /// <summary>
    /// Resolves world-space layout at runtime from the device's actual aspect ratio.
    ///
    /// A perspective camera's VERTICAL fov is fixed, so visible WIDTH shrinks on taller phones.
    /// The framed play area must always fit horizontally, so the fov is solved from the width we
    /// need and everything else is anchored to the resulting visible height: the frame hangs from
    /// the top under the HUD, the queue lines sit above the bottom edge, and the landing row
    /// floats in the gap between them.
    /// </summary>
    public static class Layout
    {
        public static bool Configured { get; private set; }

        public static float VisibleHalfHeight { get; private set; }

        /// <summary>Centre of the block grid in world space.</summary>
        public static Vector2 GridCenter { get; private set; }

        /// <summary>Half-extents of the conveyor's centre-line rectangle.</summary>
        public static Vector2 ConveyorHalf { get; private set; }

        /// <summary>
        /// Landing squares this run. Read once per layout rather than per query, so the row
        /// cannot change width halfway through a level if the profile is touched.
        /// </summary>
        public static int SlotCount { get; private set; } = Meta.PlayerProfile.MinLandingSlots;

        public static float SlotY { get; private set; }
        public static float LineBottomY { get; private set; }
        public static float FrameTopY { get; private set; }
        public static float FrameBottomY { get; private set; }

        const float SideMargin = 0.3f;

        /// <summary>Distance from the bottom edge to the last visible line row.</summary>
        const float SlotBottomPad = 4f;

        /// <summary>Distance from the grid edge out to the conveyor's centre line.</summary>
        public const float ConveyorInset = 2.2f;

        /// <summary>Width of the race track the towers ride, centred on the conveyor centre line.</summary>
        public const float TrackWidth = 3.6f;

        /// <summary>Band just outside the track where the crash barriers stand.</summary>
        public const float BarrierBand = 0.5f;

        /// <summary>
        /// Distance from the grid edge out to the outermost edge of the bezel. Everything here is
        /// pared back to what the track physically needs to carry a tower: the frame is what the
        /// board competes with for screen width, so every unit trimmed here is board.
        /// </summary>
        public const float FrameOuter = ConveyorInset + TrackWidth * 0.5f + BarrierBand + 0.7f;

        /// <summary>Half-extent of a landing well including its rim. Shared with FrameRenderer.</summary>
        public static float WellHalf => BlockTuning.SlotSpacing * 0.40f + 0.35f;

        public static float GridHalfWidth => BlockTuning.WallWidth * BlockTuning.TileSize * 0.5f;
        public static float GridHalfHeight => BlockTuning.WallHeight * BlockTuning.TileSize * 0.5f;

        public static void Configure(Camera cam)
        {
            SlotCount = Meta.PlayerProfile.LandingSlots;

            float aspect = cam != null && cam.aspect > 0.01f ? cam.aspect : 9f / 19.5f;

            // The frame — grid plus track, barriers and bezel on both sides — is what must fit.
            float requiredHalfWidth = GridHalfWidth + FrameOuter + SideMargin;
            float halfHeight = requiredHalfWidth / aspect;

            if (cam != null)
                cam.fieldOfView = 2f * Mathf.Atan(halfHeight / BlockTuning.CameraDistance) * Mathf.Rad2Deg;

            VisibleHalfHeight = halfHeight;

            // Clear space above the frame for the HUD. Proportional, because solving the fov from
            // width means the world's vertical extent changes with both aspect AND how wide the
            // frame grew — a fixed band that suited one of those stops suiting the other.
            float hudBand = Mathf.Max(6f, halfHeight * 0.20f);
            FrameTopY = halfHeight - hudBand;

            float frameHalfHeight = GridHalfHeight + FrameOuter;
            GridCenter = new Vector2(0f, FrameTopY - frameHalfHeight);
            FrameBottomY = FrameTopY - frameHalfHeight * 2f;

            ConveyorHalf = new Vector2(GridHalfWidth + ConveyorInset, GridHalfHeight + ConveyorInset);

            // The queue lines are anchored to the BOTTOM of the screen and the landing row floats
            // between them and the frame, rather than the landing row being anchored and the lines
            // hanging off below it — which pushed every queued tower past the bottom edge and made
            // the "see what's coming" planning the lines exist for impossible.
            LineBottomY = -halfHeight + SlotBottomPad;

            // Centred in the gap, but never so high that a landing well overlaps the frame's
            // lower edge — which is exactly what happened the first time the grid was resized.
            float wellHalf = WellHalf;
            float highest = FrameBottomY - wellHalf - 1f;
            float lowest = LineY(0) + BlockTuning.SlotSpacing * 0.5f + wellHalf + 0.5f;
            SlotY = Mathf.Min(highest, Mathf.Max(lowest, (FrameBottomY + LineY(0)) * 0.5f));

            Configured = true;
        }

        public static void EnsureConfigured()
        {
            if (Configured) return;
            Configure(Camera.main);
        }

        /// <summary>World position of a grid cell's centre.</summary>
        public static Vector3 CellToWorld(int x, int y) => new Vector3(
            GridCenter.x + (x + 0.5f) * BlockTuning.TileSize - GridHalfWidth,
            GridCenter.y + (y + 0.5f) * BlockTuning.TileSize - GridHalfHeight,
            0f);

        public static float SlotX(int index) =>
            (index - (SlotCount - 1) * 0.5f) * BlockTuning.SlotSpacing;

        /// <summary>X of a queue line. Lines sit under the middle of the landing row.</summary>
        public static float LineX(int line) =>
            (line - (BlockTuning.LineCount - 1) * 0.5f) * BlockTuning.SlotSpacing;

        /// <summary>Y of the Nth tower back in a line; index 0 is the front, nearest the slots.</summary>
        public static float LineY(int index) =>
            LineBottomY + (BlockTuning.VisibleLineDepth - 1 - Mathf.Min(index, BlockTuning.VisibleLineDepth - 1))
                        * BlockTuning.LineSpacingY;
    }

    /// <summary>Solves the camera fov and world layout before anything else's Awake runs.</summary>
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(Camera))]
    public class GameCamera : MonoBehaviour
    {
        void Awake() => Layout.Configure(GetComponent<Camera>());
    }
}

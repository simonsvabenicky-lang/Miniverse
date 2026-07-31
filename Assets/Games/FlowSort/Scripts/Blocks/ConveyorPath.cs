using UnityEngine;

namespace FlowSort.Blocks
{
    /// <summary>
    /// The closed track that rings the block grid: four straights joined by four quarter-circle
    /// corners. Towers ride it and fire straight INWARD along whichever straight they are on —
    /// the belt turning them 90 degrees around each corner is the entire aiming system, which is
    /// why towers have no sweep of their own.
    ///
    /// The corners are real arcs rather than a mitred rectangle, because the kit's corner tile is
    /// a quarter circle and a tower cutting straight across it looked like it had left the road.
    /// Nothing fires on an arc: the direction there is diagonal, and the grid ray only takes axis
    /// steps. A tower simply turns through the corner and resumes at the next straight.
    ///
    /// Positions are parameterised by arc length from the start of the bottom straight,
    /// travelling anticlockwise.
    /// </summary>
    public static class ConveyorPath
    {
        /// <summary>
        /// Matches the kit's corner tile, whose road arc has a radius of half the tile. Any larger
        /// and the corner piece would need a footprint the frame has no room for.
        /// </summary>
        public static float Radius => Layout.TrackWidth * 0.5f;

        public static float BottomRun => Mathf.Max(0f, Layout.ConveyorHalf.x * 2f - Radius * 2f);
        public static float SideRun => Mathf.Max(0f, Layout.ConveyorHalf.y * 2f - Radius * 2f);
        public static float ArcRun => Mathf.PI * 0.5f * Radius;

        public static float Perimeter => (BottomRun + SideRun + ArcRun * 2f) * 2f;

        public static float Wrap(float distance)
        {
            float p = Perimeter;
            if (p <= 0f) return 0f;
            distance %= p;
            return distance < 0f ? distance + p : distance;
        }

        /// <summary>
        /// Segment index 0..7 around the loop — even are straights (bottom, right, top, left),
        /// odd are the corners between them — plus how far into that segment we are.
        /// </summary>
        static int SegmentAt(float distance, out float into)
        {
            distance = Wrap(distance);

            float straight = BottomRun;
            float side = SideRun;
            float arc = ArcRun;

            var lengths = new[] { straight, arc, side, arc, straight, arc, side, arc };

            for (int i = 0; i < lengths.Length; i++)
            {
                if (distance < lengths[i] || i == lengths.Length - 1)
                {
                    into = distance;
                    return i;
                }
                distance -= lengths[i];
            }

            into = 0f;
            return 0;
        }

        public static Vector3 PositionAt(float distance)
        {
            int seg = SegmentAt(distance, out float t);

            Vector2 c = Layout.GridCenter;
            Vector2 h = Layout.ConveyorHalf;
            float r = Radius;

            switch (seg)
            {
                case 0: return new Vector3(c.x - h.x + r + t, c.y - h.y, 0f);
                case 1: return Arc(c.x + h.x - r, c.y - h.y + r, -90f, t);
                case 2: return new Vector3(c.x + h.x, c.y - h.y + r + t, 0f);
                case 3: return Arc(c.x + h.x - r, c.y + h.y - r, 0f, t);
                case 4: return new Vector3(c.x + h.x - r - t, c.y + h.y, 0f);
                case 5: return Arc(c.x - h.x + r, c.y + h.y - r, 90f, t);
                case 6: return new Vector3(c.x - h.x, c.y + h.y - r - t, 0f);
                default: return Arc(c.x - h.x + r, c.y - h.y + r, 180f, t);
            }
        }

        static Vector3 Arc(float cx, float cy, float startDegrees, float travelled)
        {
            float angle = (startDegrees + travelled / Mathf.Max(0.001f, Radius) * Mathf.Rad2Deg)
                          * Mathf.Deg2Rad;
            return new Vector3(cx + Mathf.Cos(angle) * Radius, cy + Mathf.Sin(angle) * Radius, 0f);
        }

        /// <summary>
        /// Inward firing direction, or false on a corner. Holding fire through the turn is what
        /// keeps every shot axis-aligned, which the grid ray depends on.
        /// </summary>
        public static bool TryFireDirection(float distance, out Vector2 direction)
        {
            int seg = SegmentAt(distance, out _);

            switch (seg)
            {
                case 0: direction = Vector2.up; return true;
                case 2: direction = Vector2.left; return true;
                case 4: direction = Vector2.down; return true;
                case 6: direction = Vector2.right; return true;
                default: direction = Vector2.zero; return false;
            }
        }

        /// <summary>
        /// Unit normal pointing away from the board. Anything that lines the track — kerbs,
        /// barriers — is placed by offsetting along this, so it follows the corners instead of
        /// stopping square where the arcs begin.
        /// </summary>
        public static Vector2 NormalAt(float distance)
        {
            int seg = SegmentAt(distance, out _);

            switch (seg)
            {
                case 0: return Vector2.down;
                case 2: return Vector2.right;
                case 4: return Vector2.up;
                case 6: return Vector2.left;
            }

            // On an arc the outward normal is simply the radius direction.
            Vector3 point = PositionAt(distance);
            Vector2 centre = ArcCentre(seg);
            return ((Vector2)point - centre).normalized;
        }

        static Vector2 ArcCentre(int segment)
        {
            Vector2 c = Layout.GridCenter;
            Vector2 h = Layout.ConveyorHalf;
            float r = Radius;

            switch (segment)
            {
                case 1: return new Vector2(c.x + h.x - r, c.y - h.y + r);
                case 3: return new Vector2(c.x + h.x - r, c.y + h.y - r);
                case 5: return new Vector2(c.x - h.x + r, c.y + h.y - r);
                default: return new Vector2(c.x - h.x + r, c.y - h.y + r);
            }
        }

        /// <summary>
        /// Continuous heading in degrees about Z, sweeping through the corners rather than
        /// snapping. Straights sit at 0/90/180/270, so a tower turns a quarter turn per corner in
        /// the direction it is actually travelling.
        /// </summary>
        public static float RotationAt(float distance)
        {
            int seg = SegmentAt(distance, out float t);
            float sweep = t / Mathf.Max(0.001f, ArcRun) * 90f;

            switch (seg)
            {
                case 0: return 0f;
                case 1: return sweep;
                case 2: return 90f;
                case 3: return 90f + sweep;
                case 4: return 180f;
                case 5: return 180f + sweep;
                case 6: return 270f;
                default: return 270f + sweep;
            }
        }

        /// <summary>
        /// Arc length of the point on the bottom straight nearest a given world X — so a tower
        /// launched from a slot joins the belt directly below that slot.
        /// </summary>
        public static float EntryDistanceForX(float worldX)
        {
            float left = Layout.GridCenter.x - Layout.ConveyorHalf.x + Radius;
            return Mathf.Clamp(worldX - left, 0f, BottomRun);
        }

        /// <summary>Where the towers join and leave the belt — the gantry marks this spot.</summary>
        public static Vector3 GatePosition => PositionAt(EntryDistanceForX(Layout.GridCenter.x));
    }
}

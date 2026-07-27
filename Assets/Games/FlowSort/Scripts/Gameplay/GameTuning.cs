using UnityEngine;

namespace FlowSort.Gameplay
{
    public static class GameTuning
    {
        // Grid
        public const int GridCols = 6;
        public const int GridRows = 7;
        public const int ActiveColorCount = 4; // colors in play per level, out of 6 total
        public const float KeyChancePerCell = 0.07f;
        public const float CellSize = 0.62f;
        public static readonly Vector2 GridOrigin = new Vector2(-1.55f, 4.0f); // top-left cell center

        public static Vector2 CellPosition(int col, int row) =>
            GridOrigin + new Vector2(col * CellSize, -row * CellSize);

        // Firing lanes
        public const int LaneCount = 3;
        public const float FireInterval = 0.45f;
        public static readonly float[] LaneX = { -1.3f, 0f, 1.3f };
        public const float LaneY = -1.0f;

        // Critter queue
        public const int QueueVisibleSlots = 6;
        public const int CritterAmmoMin = 1;
        public const int CritterAmmoMax = 3;
        public const float QueueY = -1.75f;
        public const float QueueSlotSpacing = 0.78f;

        // Progression / rewards
        public const int ChestBonusKeys = 5;

        // Powerups
        public const int RefillAmmoAmount = 2;
    }
}

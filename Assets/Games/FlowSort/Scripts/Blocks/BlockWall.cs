using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FlowSort.Blocks
{
    /// <summary>
    /// The wall's data model plus its descent. Owns no rendering (WallMesh does that) and no
    /// colliders at all — ball collision is analytic against this grid, see BallSystem and
    /// DESIGN.md §7. That is both faster and more robust than ~900 BoxColliders.
    ///
    /// The whole wall descends by moving this GameObject's transform, so chunk meshes stay in
    /// wall-local space and never need rebuilding just because the wall moved.
    /// </summary>
    [RequireComponent(typeof(WallMesh))]
    public class BlockWall : MonoBehaviour
    {
        public int Width { get; private set; }
        public int Height { get; private set; }

        /// <summary>Fires with cell coords, the destroyed colour, and the world position (for FX).</summary>
        public event Action<int, int, byte, Vector3> OnBlockDestroyed;
        public event Action OnWallCleared;

        byte[] cells;
        bool[] reserved;
        WallMesh mesh;

        public int RemainingBlocks { get; private set; }

        void Awake()
        {
            mesh = GetComponent<WallMesh>();
            Layout.EnsureConfigured();
        }

        /// <summary>
        /// Loads a picture, keeping only <paramref name="fill"/> of its blocks.
        ///
        /// Early levels use a thinned board so they play short, and the same picture fills in as
        /// the player progresses. Thinning also opens the interior up, so a first level is
        /// forgiving in the way that matters: almost every colour is reachable from somewhere.
        /// </summary>
        public void Load(PixelPicture picture, float fill = 1f)
        {
            Width = picture.Width;
            Height = picture.Height;
            cells = new byte[Width * Height];
            reserved = new bool[Width * Height];
            Array.Copy(picture.Cells, cells, Mathf.Min(picture.Cells.Length, cells.Length));

            if (fill < 0.999f)
                BlockRules.Thin(cells, picture.Optional, fill, n => Random.Range(0, n));

            RemainingBlocks = 0;
            for (int i = 0; i < cells.Length; i++) if (cells[i] != 0) RemainingBlocks++;

            ApplyTransform();

            if (mesh == null) mesh = GetComponent<WallMesh>();
            mesh.Rebuild(this);
        }

        // --- Queries ---

        public byte At(int x, int y)
        {
            if (cells == null || x < 0 || x >= Width || y < 0 || y >= Height) return 0;
            return cells[y * Width + x];
        }

        public bool IsSolid(int x, int y) => At(x, y) != 0;

        /// <summary>Whether any block of this colour is still standing anywhere on the board.</summary>
        public bool HasColor(byte color)
        {
            if (cells == null) return false;
            for (int i = 0; i < cells.Length; i++) if (cells[i] == color) return true;
            return false;
        }

        /// <summary>
        /// Walks inward from a tower along its fire direction and reports the FIRST block it would
        /// meet. A tower only fires when that block matches its colour, so no ammo is ever spent on
        /// a shot that cannot connect — a tower with 20 rounds facing only 3 matching blocks comes
        /// back with 17.
        ///
        /// A cell already claimed by a shot in flight is reported, not skipped past. Skipping used
        /// to let a tower keep drilling into the same column while its earlier shots were still in
        /// the air, which is how towers emptied themselves in a single lap; now the tower simply
        /// holds fire until it has moved on.
        /// </summary>
        public bool FirstBlockAlong(Vector3 worldOrigin, Vector2 dir,
                                    out int hitX, out int hitY, out byte color, out bool claimed)
        {
            hitX = hitY = -1;
            color = 0;
            claimed = false;
            if (cells == null) return false;

            Vector3 local = transform.InverseTransformPoint(worldOrigin);
            int cx = Mathf.FloorToInt(local.x / BlockTuning.TileSize);
            int cy = Mathf.FloorToInt(local.y / BlockTuning.TileSize);

            return BlockRules.FirstBlockAlong(cells, reserved, Width, Height, cx, cy,
                                              Mathf.RoundToInt(dir.x), Mathf.RoundToInt(dir.y),
                                              out hitX, out hitY, out color, out claimed);
        }

        /// <summary>Forwards the shootable-colour set to the renderer. See WallMesh.SetLiveColors.</summary>
        public void SetLiveColors(bool[] live)
        {
            if (mesh == null) mesh = GetComponent<WallMesh>();
            mesh.SetLiveColors(live);
        }

        public void Reserve(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return;
            reserved[y * Width + x] = true;
        }

        public void Release(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return;
            reserved[y * Width + x] = false;
        }

        /// <summary>World-space Y of the wall's row-0 bottom edge, accounting for descent.</summary>
        public Vector3 CellToWorld(int x, int y) => transform.TransformPoint(
            new Vector3(BlockTuning.CellLocalX(x), BlockTuning.CellLocalY(y), 0f));

        /// <summary>
        /// Blocks still standing, per palette index. This is what the tower generator sizes the
        /// level's ammo budget against, so every colour on the board is guaranteed enough rounds
        /// to be cleared — see TowerSlots.Fill.
        /// </summary>
        public void CountByColor(int[] into) => BlockRules.CountByColor(cells, into);

        /// <summary>
        /// Blocks per colour that a tower could actually hit RIGHT NOW — the outermost block on
        /// every row and column, from all four edges the belt runs along.
        ///
        /// This is the difficulty signal the level uses to order the queue lines: a colour that
        /// is buried behind others is worth nothing until something else digs it out, and where
        /// its towers sit in the lines is what decides whether that is a puzzle or a dead end.
        /// </summary>
        public void CountExposedByColor(int[] into) =>
            BlockRules.CountExposedByColor(cells, Width, Height, into);

        /// <summary>
        /// Cell containing a world point, or false if outside the grid. O(1) — this is what
        /// replaces per-block colliders.
        /// </summary>
        public bool WorldToCell(Vector3 world, out int cx, out int cy)
        {
            // The wall's pivot is its bottom-LEFT corner, so local coords already run 0..Width.
            // The old half-width term here was left over from a centred pivot and shifted every
            // hit ~14 columns to the right — shots visibly ate blocks nowhere near the tower.
            Vector3 local = transform.InverseTransformPoint(world);
            cx = Mathf.FloorToInt(local.x / BlockTuning.TileSize);
            cy = Mathf.FloorToInt(local.y / BlockTuning.TileSize);
            return cx >= 0 && cx < Width && cy >= 0 && cy < Height;
        }

        // --- Mutation ---

        public bool Destroy(int x, int y)
        {
            if (!IsSolid(x, y)) return false;

            byte color = cells[y * Width + x];
            cells[y * Width + x] = 0;
            RemainingBlocks--;

            mesh.MarkDirty(y);
            OnBlockDestroyed?.Invoke(x, y, color, CellToWorld(x, y));

            if (RemainingBlocks <= 0) OnWallCleared?.Invoke();
            return true;
        }

        // The grid is static now: the conveyor supplies the pressure, not a descending wall.
        void ApplyTransform()
        {
            transform.position = new Vector3(
                Layout.GridCenter.x - Layout.GridHalfWidth,
                Layout.GridCenter.y - Layout.GridHalfHeight,
                0f);
        }
    }
}

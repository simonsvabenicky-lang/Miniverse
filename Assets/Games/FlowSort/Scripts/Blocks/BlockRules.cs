namespace FlowSort.Blocks
{
    /// <summary>
    /// The board rules, as pure functions over a cell array.
    ///
    /// These live apart from BlockWall so the balance simulator can run the REAL rules rather
    /// than a second copy of them that quietly drifts. A model of the game that disagrees with
    /// the game is worse than no model, because it answers balance questions confidently and
    /// wrongly.
    /// </summary>
    public static class BlockRules
    {
        /// <summary>
        /// Walks from a starting cell along a unit step and reports the first block met.
        ///
        /// A cell already claimed by a shot in flight is reported, not skipped: skipping let a
        /// tower drill a whole column while its own shots were still travelling, which is how
        /// towers used to empty themselves in a single lap.
        /// </summary>
        public static bool FirstBlockAlong(byte[] cells, bool[] reserved, int width, int height,
                                           int cx, int cy, int sx, int sy,
                                           out int hitX, out int hitY, out byte color, out bool claimed)
        {
            hitX = hitY = -1;
            color = 0;
            claimed = false;

            if (cells == null || (sx == 0 && sy == 0)) return false;

            bool entered = false;

            for (int step = 0; step < width + height + 8; step++)
            {
                bool inside = cx >= 0 && cx < width && cy >= 0 && cy < height;

                if (inside)
                {
                    entered = true;
                    int i = cy * width + cx;
                    if (cells[i] != 0)
                    {
                        hitX = cx;
                        hitY = cy;
                        color = cells[i];
                        claimed = reserved != null && reserved[i];
                        return true;
                    }
                }
                else if (entered)
                {
                    return false; // passed all the way through
                }

                cx += sx;
                cy += sy;
            }

            return false;
        }

        /// <summary>
        /// Thins a board down to <paramref name="fill"/> of its blocks, taking cells flagged
        /// optional first and only cutting into the rest once those are gone. Early levels ship
        /// less of the picture; the subject survives at every fill a normal ramp reaches.
        /// </summary>
        public static void Thin(byte[] cells, bool[] optional, float fill, System.Func<int, int> nextInt)
        {
            int total = 0;
            for (int i = 0; i < cells.Length; i++) if (cells[i] != 0) total++;

            int toDrop = total - UnityEngine.Mathf.RoundToInt(total * UnityEngine.Mathf.Clamp01(fill));
            if (toDrop <= 0) return;

            toDrop = DropPass(cells, optional, toDrop, true, nextInt);
            if (toDrop > 0) DropPass(cells, optional, toDrop, false, nextInt);
        }

        /// <summary>
        /// One sweep that removes exactly the requested count, deciding each candidate against how
        /// many are left to drop versus how many candidates remain — no shuffled index list.
        /// </summary>
        static int DropPass(byte[] cells, bool[] optional, int toDrop, bool optionalOnly,
                            System.Func<int, int> nextInt)
        {
            int candidates = 0;
            for (int i = 0; i < cells.Length; i++)
                if (cells[i] != 0 && (!optionalOnly || IsOptional(optional, i))) candidates++;

            for (int i = 0; i < cells.Length && toDrop > 0; i++)
            {
                if (cells[i] == 0) continue;
                if (optionalOnly && !IsOptional(optional, i)) continue;

                if (nextInt(candidates) < toDrop)
                {
                    cells[i] = 0;
                    toDrop--;
                }

                candidates--;
            }

            return toDrop;
        }

        static bool IsOptional(bool[] optional, int index) =>
            optional != null && index < optional.Length && optional[index];

        /// <summary>Blocks per colour still standing.</summary>
        public static void CountByColor(byte[] cells, int[] into)
        {
            System.Array.Clear(into, 0, into.Length);
            if (cells == null) return;

            for (int i = 0; i < cells.Length; i++)
            {
                byte c = cells[i];
                if (c != 0 && c < into.Length) into[c]++;
            }
        }

        /// <summary>
        /// Blocks per colour that a tower could hit right now: the outermost block of every row
        /// and column, from all four edges the belt runs along.
        /// </summary>
        public static void CountExposedByColor(byte[] cells, int width, int height, int[] into)
        {
            System.Array.Clear(into, 0, into.Length);
            if (cells == null) return;

            for (int x = 0; x < width; x++)
            {
                Tally(FirstAlongColumn(cells, width, height, x, true), into);
                Tally(FirstAlongColumn(cells, width, height, x, false), into);
            }

            for (int y = 0; y < height; y++)
            {
                Tally(FirstAlongRow(cells, width, height, y, true), into);
                Tally(FirstAlongRow(cells, width, height, y, false), into);
            }
        }

        static void Tally(byte color, int[] into)
        {
            if (color != 0 && color < into.Length) into[color]++;
        }

        static byte FirstAlongColumn(byte[] cells, int width, int height, int x, bool fromBottom)
        {
            for (int i = 0; i < height; i++)
            {
                int y = fromBottom ? i : height - 1 - i;
                byte c = cells[y * width + x];
                if (c != 0) return c;
            }
            return 0;
        }

        static byte FirstAlongRow(byte[] cells, int width, int height, int y, bool fromLeft)
        {
            for (int i = 0; i < width; i++)
            {
                int x = fromLeft ? i : width - 1 - i;
                byte c = cells[y * width + x];
                if (c != 0) return c;
            }
            return 0;
        }
    }
}

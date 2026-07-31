using UnityEngine;

namespace FlowSort.Blocks
{
    /// <summary>
    /// One level's block layout. Cells are palette indices (0 = empty), stored row-major with
    /// y = 0 as the BOTTOM row, matching the wall's own coordinate system so no flipping is
    /// needed at load time.
    ///
    /// Authored by Editor/PictureAuthor.cs from code rather than traced from any reference
    /// image — see DESIGN.md §4.
    /// </summary>
    [CreateAssetMenu(menuName = "FlowSort/Pixel Picture", fileName = "Picture")]
    public class PixelPicture : ScriptableObject
    {
        public int Width = BlockTuning.WallWidth;
        public int Height = BlockTuning.WallHeight;
        public byte[] Cells;

        /// <summary>
        /// True for the loose satellite clusters around the subject, false for the subject itself.
        ///
        /// Early levels play short by shipping less of the picture, and this is what tells them
        /// what to leave out. Thinning at random instead turned level 1 into confetti — the whole
        /// point of the mode is that a picture emerges, and it can't emerge from a board that
        /// never contained one.
        /// </summary>
        public bool[] Optional;

        public byte At(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return 0;
            return Cells[y * Width + x];
        }

        public void Set(int x, int y, byte value)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return;
            Cells[y * Width + x] = value;
        }

        public void Allocate(int width, int height)
        {
            Width = width;
            Height = height;
            Cells = new byte[width * height];
            Optional = new bool[width * height];
        }

        public bool IsOptional(int index) =>
            Optional != null && index < Optional.Length && Optional[index];

        public int SolidCount
        {
            get
            {
                if (Cells == null) return 0;
                int n = 0;
                for (int i = 0; i < Cells.Length; i++) if (Cells[i] != 0) n++;
                return n;
            }
        }
    }
}

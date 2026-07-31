using FlowSort.Blocks;
using UnityEditor;
using UnityEngine;

namespace FlowSort.EditorTools
{
    /// <summary>
    /// Generates the level pictures as PixelPicture assets.
    ///
    /// These are composed from primitives in code rather than traced or sampled from any
    /// reference image — see DESIGN.md §4. That keeps provenance unambiguous and makes adding a
    /// new level a few lines rather than an art task.
    ///
    /// Pictures are SPARSE: the subject is drawn on an empty board, then split along a coarse
    /// lattice and eroded so it reads as clusters of blocks with gaps between them, with loose
    /// satellite clusters scattered around it. That is the look, and it also drives the mechanic —
    /// gaps are what let a shot reach deep into the board instead of every colour but the outer
    /// skin being unreachable.
    ///
    /// Menu: FlowSort/Generate Pictures
    /// </summary>
    public static class PictureAuthor
    {
        
        const int W = BlockTuning.WallWidth;
        const int H = BlockTuning.WallHeight;

        // Palette indices, mirroring BlockPalette. No ink: the sprites carry their own outline.
        const byte Empty = 0, Red = 1, Orange = 2, Green = 3, Blue = 4, White = 5;

        [MenuItem("FlowSort/Generate Pictures")]
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder(ProjectPaths.Pictures))
                AssetDatabase.CreateFolder(ProjectPaths.Root, "Pictures");

            Save("Picture_01_Rocket", BuildRocket());
            Save("Picture_02_Robot", BuildRobot());
            Save("Picture_03_Ghost", BuildGhost());
            Save("Picture_04_Bolt", BuildBolt());
            Save("Picture_05_Heart", BuildHeart());
            Save("Picture_06_Star", BuildStar());
            Save("Picture_07_Cat", BuildCat());
            Save("Picture_08_Mushroom", BuildMushroom());
            Save("Picture_09_Crown", BuildCrown());
            Save("Picture_10_Fish", BuildFish());
            Save("Picture_11_Cactus", BuildCactus());
            Save("Picture_12_Skull", BuildSkull());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FlowSort] Generated 12 pictures ({W}x{H}) into {ProjectPaths.Pictures}");
        }

        /// <summary>Regenerate and immediately dump previews — the loop used while tuning sparsity.</summary>
        [MenuItem("FlowSort/Generate Pictures + Previews")]
        public static void GenerateAndPreview()
        {
            Generate();
            ExportPreviews();
        }

        /// <summary>
        /// Writes each picture out as a scaled-up PNG so the designs can actually be eyeballed
        /// without launching the game. Previews are throwaway — not part of the build.
        /// </summary>
        [MenuItem("FlowSort/Export Picture Previews")]
        public static void ExportPreviews()
        {
            const int scale = 8;
            string dir = System.Environment.GetEnvironmentVariable("FLOWSORT_PREVIEW_DIR");
            if (string.IsNullOrEmpty(dir)) dir = "D:/FlowSort/_previews";
            System.IO.Directory.CreateDirectory(dir);

            foreach (var guid in AssetDatabase.FindAssets("t:PixelPicture"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var pic = AssetDatabase.LoadAssetAtPath<PixelPicture>(path);
                if (pic == null || pic.Cells == null) continue;

                var tex = new Texture2D(pic.Width * scale, pic.Height * scale, TextureFormat.RGBA32, false);
                for (int y = 0; y < pic.Height * scale; y++)
                for (int x = 0; x < pic.Width * scale; x++)
                {
                    byte cell = pic.At(x / scale, y / scale);
                    Color c = cell == 0 ? (Color)BlockPalette.GridWell : (Color)BlockPalette.Get(cell);

                    // Draw the grout so clusters and gaps are legible in the preview.
                    bool grout = x % scale == 0 || y % scale == 0;
                    tex.SetPixel(x, y, grout ? c * 0.55f : c);
                }
                tex.Apply();

                string outPath = $"{dir}/{System.IO.Path.GetFileNameWithoutExtension(path)}.png";
                System.IO.File.WriteAllBytes(outPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Debug.Log($"[FlowSort] preview -> {outPath} ({pic.SolidCount} blocks)");
            }
        }

        static void Save(string name, Raster r)
        {
            string path = $"{ProjectPaths.Pictures}/{name}.asset";
            var pic = AssetDatabase.LoadAssetAtPath<PixelPicture>(path);
            bool isNew = pic == null;
            if (isNew) pic = ScriptableObject.CreateInstance<PixelPicture>();

            pic.Allocate(W, H);
            System.Array.Copy(r.Cells, pic.Cells, r.Cells.Length);
            System.Array.Copy(r.Optional, pic.Optional, r.Optional.Length);

            if (isNew) AssetDatabase.CreateAsset(pic, path);
            else EditorUtility.SetDirty(pic);

            int optional = 0;
            for (int i = 0; i < pic.Cells.Length; i++)
                if (pic.Cells[i] != 0 && pic.Optional[i]) optional++;

            Debug.Log($"[FlowSort]   {name}: {pic.SolidCount} blocks " +
                      $"({optional} optional, subject holds at fill >= " +
                      $"{1f - optional / (float)Mathf.Max(1, pic.SolidCount):F2})");
        }

        // --- Picture definitions (original designs) ---

        static Raster BuildRocket()
        {
            var r = new Raster(seed: 1101);

            // Chunky on purpose: at this grid a 6-wide design feature survives as three and a half
            // cells, and the fracture pass then cuts it into unreadable crumbs.

            // Exhaust plume, widening downward.
            r.Poly(Orange, 10f, 7f, 18f, 7f, 21f, 0f, 7f, 0f);
            r.Poly(Red, 11.5f, 5f, 16.5f, 5f, 19f, 0f, 9f, 0f);

            // Fins.
            r.Poly(Red, 9f, 6f, 9f, 15f, 3f, 6f);

            // Fuselage + nose cone.
            r.Rect(9, 6, 18, 24, White);
            r.Poly(Red, 8.5f, 24f, 19.5f, 24f, 14f, 31f);

            // Porthole.
            r.Disc(14f, 18f, 3.6f, Blue);

            r.MirrorX();

            r.PatternFill(0, 5, Green, Blue, Orange);
            r.Erode(0.10f);
            return r;
        }

        // Symmetric subjects draw left-half detail only and mirror, which avoids the sub-cell
        // rounding that otherwise makes paired features (eyes especially) land unevenly.
        static Raster BuildRobot()
        {
            var r = new Raster(seed: 2202);

            // Antenna.
            r.Rect(13, 25, 14, 29, White);
            r.Disc(14f, 30.5f, 1.8f, Red);

            // Side vent (left only — mirrored below).
            r.Rect(3, 14, 5, 20, Orange);

            // Head.
            r.Rect(6, 7, 21, 25, White);

            // Left eye (mirrored below).
            r.Disc(10f, 19.5f, 2.6f, Blue);

            // Grille mouth — left half only.
            r.Rect(10, 11, 14, 14, Red);

            // Neck + shoulders.
            r.Rect(11, 4, 16, 7, Orange);
            r.Rect(7, 1, 20, 4, Green);

            r.MirrorX();

            // Four distinct colours, not one of them twice. Repeating a colour in the cycle makes
            // it half the ground, which swamps the level's own difficulty ramp — the picture ends
            // up mattering far more than the level number.
            r.PatternFill(1, 4, Blue, Orange, Red, Green);
            r.Erode(0.10f);
            return r;
        }

        static Raster BuildGhost()
        {
            var r = new Raster(seed: 3303);

            // Dome head into a straight body.
            r.Disc(14f, 22f, 8f, White);
            r.Rect(6, 9, 21, 22, White);

            // Scalloped hem — punch arcs back out of the bottom edge.
            r.Disc(8f, 8.5f, 2.6f, Empty);
            r.Disc(14f, 8.5f, 2.6f, Empty);
            r.Disc(20f, 8.5f, 2.6f, Empty);
            r.Rect(0, 0, 27, 6, Empty);

            // Left eye (mirrored below).
            r.Disc(10.5f, 23f, 2.4f, Blue);

            r.MirrorX();

            // Mouth sits on the centre line, so it stays symmetric either side of the mirror.
            r.Disc(14f, 17f, 2.4f, Red);

            // Checker, not rings. Concentric rings sort the ground into colour BANDS, which puts
            // one colour entirely inside the others: it cannot be reached until everything
            // outside it is gone, and the simulated win rate at full fill collapsed to 3%.
            r.PatternFill(1, 3, Green, Orange, Blue);
            r.Erode(0.10f);
            return r;
        }

        static Raster BuildBolt()
        {
            var r = new Raster(seed: 4404);

            // Zigzag bolt, drawn as one polygon.
            r.Poly(Orange,
                17f, 31f,
                8f, 17.5f,
                13f, 17.5f,
                9.5f, 1f,
                21f, 15f,
                15f, 15f);

            // Highlight down the leading edge.
            r.Poly(White,
                16f, 28f,
                11f, 18.5f,
                13.5f, 18.5f,
                12f, 8f,
                17f, 15.5f,
                14.5f, 15.5f);

            r.PatternFill(0, 4, Blue, Green, Red, White);
            r.Erode(0.10f);
            return r;
        }

        static Raster BuildHeart()
        {
            var r = new Raster(seed: 5505);

            r.Disc(9.5f, 22f, 5.6f, Red);
            r.Poly(Red, 3.5f, 22f, 24.5f, 22f, 14f, 4f);
            r.Disc(14f, 20f, 3.2f, White);

            r.MirrorX();

            r.PatternFill(1, 4, Blue, Green, Orange, White);
            r.Erode(0.03f);
            return r;
        }

        static Raster BuildStar()
        {
            var r = new Raster(seed: 6606);

            r.Poly(Orange,
                14f, 29f, 11.1f, 21.1f, 2.6f, 20.7f, 9.2f, 15.5f,
                7.0f, 7.3f, 14f, 12f, 21.0f, 7.3f, 18.8f, 15.5f,
                25.4f, 20.7f, 16.9f, 21.1f);

            r.Disc(14f, 19f, 3.4f, White);

            r.PatternFill(2, 4, Blue, Red, Green, White);
            r.Erode(0.03f);
            return r;
        }

        static Raster BuildCat()
        {
            var r = new Raster(seed: 7707);

            // Ear (left only — mirrored).
            r.Poly(Orange, 5f, 20f, 6f, 30f, 13f, 23f);

            r.Rect(5, 7, 22, 24, Orange);

            // Eye and cheek stripe, left half only.
            r.Disc(10f, 18f, 2.4f, Green);
            r.Rect(2, 12, 6, 13, White);

            r.MirrorX();

            // Nose and mouth sit on the centre line.
            r.Disc(14f, 13.5f, 2f, Red);
            r.Rect(13, 9, 14, 12, Red);

            r.PatternFill(0, 5, Blue, White, Green, Red);
            r.Erode(0.04f);
            return r;
        }

        static Raster BuildMushroom()
        {
            var r = new Raster(seed: 8808);

            r.Rect(10, 3, 17, 19, White);
            r.Disc(14f, 19f, 10f, Red);
            r.Rect(4, 17, 23, 20, Red);

            r.Disc(8.5f, 21f, 2.4f, White);
            r.Disc(19.5f, 22f, 2.8f, White);
            r.Disc(14f, 25f, 2.2f, White);

            r.PatternFill(1, 4, Blue, Green, Orange, Red);
            r.Erode(0.04f);
            return r;
        }

        static Raster BuildCrown()
        {
            var r = new Raster(seed: 9909);

            r.Poly(Orange,
                4f, 8f, 24f, 8f, 24f, 25f, 19f, 15f,
                14f, 24f, 9f, 15f, 4f, 25f);

            r.Rect(4, 4, 23, 9, Orange);

            r.Disc(9f, 6.5f, 1.8f, Red);
            r.Disc(14f, 6.5f, 1.8f, Green);
            r.Disc(19f, 6.5f, 1.8f, Blue);

            r.PatternFill(0, 4, White, Blue, Green, Red);
            r.Erode(0.04f);
            return r;
        }

        static Raster BuildFish()
        {
            var r = new Raster(seed: 1010);

            r.Disc(16f, 17f, 8.5f, Blue);

            // Tail and top fin.
            r.Poly(Orange, 9f, 17f, 2f, 26f, 2f, 8f);
            r.Poly(Orange, 13f, 23f, 21f, 23f, 17f, 30f);

            r.Disc(20f, 20f, 2.4f, White);
            r.Disc(20.5f, 20f, 1.1f, Red);

            // Three-colour ground on purpose: it leaves blue and orange out entirely, so the
            // body and fins read against it instead of merging into it the way they first did.
            r.PatternFill(2, 4, Green, White, Red);
            r.Erode(0.04f);
            return r;
        }

        static Raster BuildCactus()
        {
            var r = new Raster(seed: 1111);

            r.Rect(11, 2, 16, 28, Green);

            // Arm (left only — mirrored).
            r.Rect(4, 14, 10, 18, Green);
            r.Rect(4, 14, 7, 23, Green);

            r.MirrorX();

            // Pot.
            r.Rect(7, 0, 20, 5, Orange);
            r.Rect(6, 4, 21, 6, Red);

            // Flower.
            r.Disc(14f, 30f, 2.4f, White);

            r.PatternFill(1, 5, Blue, White, Red, Orange);
            r.Erode(0.04f);
            return r;
        }

        static Raster BuildSkull()
        {
            var r = new Raster(seed: 1212);

            r.Rect(6, 10, 21, 27, White);
            r.Disc(14f, 22f, 8.5f, White);

            // Jaw.
            r.Rect(9, 4, 18, 11, White);
            r.Rect(11, 2, 16, 5, White);

            // Eye socket, left only.
            r.Disc(10f, 21f, 3f, Blue);

            r.MirrorX();

            // Nose and teeth on the centre line.
            r.Poly(Red, 14f, 18f, 11.5f, 13f, 16.5f, 13f);
            r.Rect(10, 6, 11, 9, Blue);
            r.Rect(13, 6, 14, 9, Blue);
            r.Rect(16, 6, 17, 9, Blue);

            r.PatternFill(0, 4, Green, Orange, Blue, Red);
            r.Erode(0.04f);
            return r;
        }

        // --- Tiny raster helper ---

        class Raster
        {
            public readonly byte[] Cells = new byte[W * H];

            /// <summary>Marks the scatter clusters, so early levels can ship without them.</summary>
            public readonly bool[] Optional = new bool[W * H];

            readonly System.Random rng;

            /// <summary>
            /// Subjects are drawn in a fixed design space and scaled onto whatever grid the game
            /// is actually using, so BlockTuning.WallWidth/Height can change — to make blocks
            /// bigger on screen, say — without every coordinate in every picture being redrawn.
            /// </summary>
            const int DesignW = 28, DesignH = 32;

            static float SX => W / (float)DesignW;
            static float SY => H / (float)DesignH;
            static float SR => (SX + SY) * 0.5f;

            public Raster(int seed) => rng = new System.Random(seed);

            float Next01() => (float)rng.NextDouble();

            public void Set(int x, int y, byte c)
            {
                if (x < 0 || x >= W || y < 0 || y >= H) return;
                Cells[y * W + x] = c;
            }

            public byte At(int x, int y)
            {
                if (x < 0 || x >= W || y < 0 || y >= H) return Empty;
                return Cells[y * W + x];
            }

            public void Rect(int x0, int y0, int x1, int y1, byte c)
            {
                int cx0 = Mathf.RoundToInt(x0 * SX), cx1 = Mathf.RoundToInt(x1 * SX);
                int cy0 = Mathf.RoundToInt(y0 * SY), cy1 = Mathf.RoundToInt(y1 * SY);

                for (int y = cy0; y <= cy1; y++)
                for (int x = cx0; x <= cx1; x++)
                    Set(x, y, c);
            }

            public void Disc(float designX, float designY, float designRadius, byte c)
            {
                float cx = designX * SX, cy = designY * SY, radius = designRadius * SR;

                int minX = Mathf.FloorToInt(cx - radius), maxX = Mathf.CeilToInt(cx + radius);
                int minY = Mathf.FloorToInt(cy - radius), maxY = Mathf.CeilToInt(cy + radius);
                float r2 = radius * radius;

                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= r2) Set(x, y, c);
                }
            }

            /// <summary>
            /// Copies the left half onto the right, mirrored about the grid's centre line.
            /// Guarantees perfect symmetry for subjects that need it.
            /// </summary>
            public void MirrorX()
            {
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W / 2; x++)
                    Cells[y * W + (W - 1 - x)] = Cells[y * W + x];
            }

            /// <summary>Even-odd fill of an arbitrary polygon given as flat x,y pairs.</summary>
            public void Poly(byte c, params float[] xy)
            {
                int n = xy.Length / 2;
                if (n < 3) return;

                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    bool inside = false;

                    for (int i = 0, j = n - 1; i < n; j = i++)
                    {
                        float xi = xy[i * 2] * SX, yi = xy[i * 2 + 1] * SY;
                        float xj = xy[j * 2] * SX, yj = xy[j * 2 + 1] * SY;

                        if (yi > py != yj > py &&
                            px < (xj - xi) * (py - yi) / (yj - yi) + xi)
                            inside = !inside;
                    }

                    if (inside) Set(x, y, c);
                }
            }

            /// <summary>
            /// Random single-cell bites. On a filled board these are the pockets that let a shot
            /// reach past the outer skin, so they are a mechanic as much as a texture.
            /// </summary>
            public void Erode(float chance)
            {
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (At(x, y) != Empty && Next01() < chance) Set(x, y, Empty);
            }

            /// <summary>
            /// Fills everything the subject did not claim with a regular pattern, so the board is
            /// mostly solid rather than a silhouette floating in space. Pattern cells are marked
            /// optional, which means early levels ship the subject against a sparse ground and
            /// later ones fill the whole frame in.
            ///
            /// <paramref name="kind"/> 0 = diagonal bands, 1 = checker, 2 = concentric rings.
            /// </summary>
            public void PatternFill(int kind, int band, params byte[] colors)
            {
                if (colors == null || colors.Length == 0) return;

                int cx = W / 2, cy = H / 2;
                int step = Mathf.Max(1, Mathf.RoundToInt(band * SR));

                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (At(x, y) != Empty) continue;

                    int index = kind switch
                    {
                        0 => (x + y) / step,
                        1 => x / step + y / step,
                        _ => Mathf.Max(Mathf.Abs(x - cx), Mathf.Abs(y - cy)) / step,
                    };

                    Set(x, y, colors[((index % colors.Length) + colors.Length) % colors.Length]);
                    Optional[y * W + x] = true;
                }
            }

        }
    }
}

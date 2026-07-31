using System.Collections.Generic;
using UnityEngine;

namespace FlowSort.Blocks
{
    /// <summary>
    /// Renders the whole wall in ChunkCount draw calls by baking each horizontal band of rows
    /// into one mesh, instead of giving hundreds of blocks a GameObject each (see DESIGN.md §6).
    ///
    /// A block is a single quad whose UVs select its colour's tile out of the block atlas, so the
    /// rich sprite face — baked highlight, inner shadow, hard black outline — comes through
    /// unmodified while every chunk still shares one material and one texture. Vertex colour is
    /// left white and reserved as a per-block shade multiplier.
    ///
    /// Only chunks containing a destroyed block rebuild, at most once per frame.
    /// </summary>
    public class WallMesh : MonoBehaviour
    {
        public Material BlockMaterial;

        readonly List<Vector3> verts = new List<Vector3>(2048);
        readonly List<Vector3> normals = new List<Vector3>(2048);
        readonly List<Color32> colors = new List<Color32>(2048);
        readonly List<Vector2> uvs = new List<Vector2>(2048);
        readonly List<int> tris = new List<int>(3072);

        Mesh[] chunkMeshes;
        bool[] chunkDirty;
        BlockWall wall;
        int chunkCount;

        static readonly Vector3 FrontNormal = new Vector3(0f, 0f, -1f);
        static readonly Color32 NoShade = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        /// <summary>
        /// Enough to separate live colours from dead ones, not so much that the picture stops
        /// being legible — a tower is riding most of the time, so this is the board's normal look.
        /// </summary>
        static readonly Color32 Dimmed = new Color32(0x96, 0x96, 0xA0, 0xFF);

        /// <summary>
        /// Which colours a tower on the belt can currently shoot. Blocks of any other colour are
        /// pushed back with vertex shade, so at a glance you can see what the towers you have
        /// running are actually able to eat — which is the question the whole game asks and which
        /// a board of five equally bright colours gave no help answering.
        ///
        /// Null or all-false means nothing is riding, and everything renders at full strength.
        /// </summary>
        bool[] liveColors;

        public void Rebuild(BlockWall source)
        {
            wall = source;
            chunkCount = Mathf.CeilToInt(wall.Height / (float)BlockTuning.ChunkRows);

            EnsureChunks();
            for (int c = 0; c < chunkCount; c++) BuildChunk(c);
        }

        /// <summary>
        /// Sets which colours are live. Rebuilds every chunk, so it is only called when the set
        /// actually changes — a tower deploying, landing or running dry — never per frame.
        /// </summary>
        public void SetLiveColors(bool[] live)
        {
            liveColors = live;
            if (chunkDirty == null) return;
            for (int c = 0; c < chunkDirty.Length; c++) chunkDirty[c] = true;
        }

        Color32 ShadeFor(byte color)
        {
            if (liveColors == null || color >= liveColors.Length) return NoShade;

            bool anyLive = false;
            for (int i = 0; i < liveColors.Length; i++) if (liveColors[i]) { anyLive = true; break; }

            if (!anyLive) return NoShade;
            return liveColors[color] ? NoShade : Dimmed;
        }

        public void MarkDirty(int row)
        {
            if (chunkDirty == null) return;
            int c = row / BlockTuning.ChunkRows;
            if (c >= 0 && c < chunkDirty.Length) chunkDirty[c] = true;
        }

        void LateUpdate()
        {
            if (chunkDirty == null) return;
            for (int c = 0; c < chunkCount; c++)
            {
                if (!chunkDirty[c]) continue;
                chunkDirty[c] = false;
                BuildChunk(c);
            }
        }

        void EnsureChunks()
        {
            // Chunk children are created once and reused; Load() on a new picture reuses them
            // rather than destroying and respawning renderers.
            if (chunkMeshes != null && chunkMeshes.Length == chunkCount) return;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("Chunk_")) DestroyImmediate(child.gameObject);
            }

            chunkMeshes = new Mesh[chunkCount];
            chunkDirty = new bool[chunkCount];

            for (int c = 0; c < chunkCount; c++)
            {
                var go = new GameObject($"Chunk_{c}", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);

                var m = new Mesh { name = $"WallChunk_{c}" };
                m.MarkDynamic();
                chunkMeshes[c] = m;
                go.GetComponent<MeshFilter>().sharedMesh = m;

                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = BlockMaterial;
                // Hundreds of shadow-casting blocks would dominate the frame budget and add
                // nothing: the wall is lit head-on. Only towers cast shadows (DESIGN.md §12).
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        void BuildChunk(int chunk)
        {
            verts.Clear();
            normals.Clear();
            colors.Clear();
            uvs.Clear();
            tris.Clear();

            int rowStart = chunk * BlockTuning.ChunkRows;
            int rowEnd = Mathf.Min(rowStart + BlockTuning.ChunkRows, wall.Height);
            float h = BlockTuning.TileSize * (1f - BlockTuning.BlockGap) * 0.5f;

            for (int y = rowStart; y < rowEnd; y++)
            {
                for (int x = 0; x < wall.Width; x++)
                {
                    byte color = wall.At(x, y);
                    if (color == 0) continue;

                    float cx = BlockTuning.CellLocalX(x);
                    float cy = BlockTuning.CellLocalY(y);
                    Vector4 uv = BlockAtlas.TileUv(color);

                    AddQuad(
                        new Vector3(cx - h, cy - h, 0f),
                        new Vector3(cx + h, cy - h, 0f),
                        new Vector3(cx + h, cy + h, 0f),
                        new Vector3(cx - h, cy + h, 0f),
                        uv, ShadeFor(color));
                }
            }

            var mesh = chunkMeshes[chunk];
            mesh.Clear();
            if (verts.Count == 0) return;

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0, true);
        }

        void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector4 uv, Color32 shade)
        {
            int i = verts.Count;

            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            normals.Add(FrontNormal); normals.Add(FrontNormal);
            normals.Add(FrontNormal); normals.Add(FrontNormal);
            colors.Add(shade); colors.Add(shade); colors.Add(shade); colors.Add(shade);

            uvs.Add(new Vector2(uv.x, uv.y));
            uvs.Add(new Vector2(uv.z, uv.y));
            uvs.Add(new Vector2(uv.z, uv.w));
            uvs.Add(new Vector2(uv.x, uv.w));

            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }
    }
}

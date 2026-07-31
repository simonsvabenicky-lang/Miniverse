using System.Collections.Generic;
using UnityEngine;

namespace FlowSort.Blocks
{
    /// <summary>
    /// Draws the static furniture: the backdrop, the gold bezel around the board, the recess the
    /// blocks sit in, the landing wells and the queue-line runways. One mesh, one draw call, built
    /// in Awake because runtime-created meshes cannot be serialised into the scene.
    ///
    /// The belt itself is NOT drawn here any more — ConveyorTrack lays real RacingKit road tiles
    /// in the band this used to fill with a flat rectangle.
    ///
    /// The bezel is layered light-on-dark rather than a single flat rect, which is what gives the
    /// frame the raised, moulded look rather than reading as a painted outline.
    /// </summary>
    public class FrameRenderer : MonoBehaviour
    {
        public Material Material;

        /// <summary>Textured material for the landing squares — a GUI-bundle sprite, not a flat rect.</summary>
        public Material SlotMaterial;

        readonly List<Vector3> verts = new List<Vector3>(256);
        readonly List<Color32> colors = new List<Color32>(256);
        readonly List<Vector2> uvs = new List<Vector2>(256);
        readonly List<int> tris = new List<int>(384);

        void Awake()
        {
            Layout.EnsureConfigured();

            Vector2 c = Layout.GridCenter;
            Vector2 h = Layout.ConveyorHalf;

            float halfW = Layout.VisibleHalfHeight * 2f;
            float top = Layout.VisibleHalfHeight + 6f;
            float bottom = -Layout.VisibleHalfHeight - 6f;

            // Everything except the far backdrop sits just BEHIND the gameplay plane, not far
            // back. Under a perspective camera, geometry at z=10 projects noticeably smaller and
            // pulled toward screen centre than the towers and blocks at z=0 — which made the slot
            // wells drift away from the towers standing in them, worse toward the screen edges.
            AddRect(-halfW, bottom, halfW, top, 14f,
                    BlockPalette.BackgroundBottom, BlockPalette.BackgroundTop);

            // No bezel. A square gold frame around a track with rounded corners left a wedge of
            // dead space at each one and fought the shape it was supposed to contain — the track
            // is the boundary now.

            // Recess behind the blocks, so the gaps in a sparse picture read as depth.
            AddRect(c.x - Layout.GridHalfWidth - 0.5f, c.y - Layout.GridHalfHeight - 0.5f,
                    c.x + Layout.GridHalfWidth + 0.5f, c.y + Layout.GridHalfHeight + 0.5f, 0.8f,
                    BlockPalette.GridWell);

            // Landing wells — same near plane as the towers that stand in them.
            // Faint runway behind each queue line, so the lines read as ordered ranks.
            float lr = BlockTuning.SlotSpacing * 0.38f;
            float lineTop = Layout.LineY(0) + lr;
            float lineBottom = Layout.LineY(BlockTuning.VisibleLineDepth - 1) - lr;

            for (int line = 0; line < BlockTuning.LineCount; line++)
            {
                float x = Layout.LineX(line);
                AddRect(x - lr, lineBottom, x + lr, lineTop, 2.3f, BlockPalette.LineRunway);
            }

            Build("FrameMesh", Material);
            BuildSlots();
        }

        /// <summary>
        /// The five landing squares, drawn as sprite quads on their own mesh. They need a second
        /// draw call because they are the one piece of furniture that is textured — everything
        /// else here is flat vertex colour on one shared material.
        /// </summary>
        void BuildSlots()
        {
            if (SlotMaterial == null) return;

            verts.Clear();
            colors.Clear();
            uvs.Clear();
            tris.Clear();

            float r = Layout.WellHalf;
            for (int i = 0; i < Layout.SlotCount; i++)
            {
                float x = Layout.SlotX(i);
                AddRect(x - r, Layout.SlotY - r, x + r, Layout.SlotY + r, 2.2f, Color.white);
            }

            Build("SlotMesh", SlotMaterial);
        }

        void AddRect(float x0, float y0, float x1, float y1, float z, Color32 flat)
            => AddRect(x0, y0, x1, y1, z, flat, flat);

        void AddRect(float x0, float y0, float x1, float y1, float z, Color32 bottomColor, Color32 topColor)
        {
            if (x1 <= x0 || y1 <= y0) return;

            // Colour-space corrected here, once, rather than at every call site.
            bottomColor = BlockPalette.ToVertex(bottomColor);
            topColor = BlockPalette.ToVertex(topColor);

            int i = verts.Count;

            verts.Add(new Vector3(x0, y0, z));
            verts.Add(new Vector3(x1, y0, z));
            verts.Add(new Vector3(x1, y1, z));
            verts.Add(new Vector3(x0, y1, z));

            colors.Add(bottomColor); colors.Add(bottomColor);
            colors.Add(topColor); colors.Add(topColor);

            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));

            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        void Build(string name, Material material)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0, false);

            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }
}

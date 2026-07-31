using System.Collections.Generic;
using UnityEngine;

namespace FlowSort.Blocks
{
    /// <summary>
    /// Builds the belt out of Kenney RacingKit road tiles: a closed race track ringing the picture,
    /// with quarter-turn corners and crash barriers along the outside.
    ///
    /// Every tile is welded into ONE mesh with per-submesh vertex colours, so the whole track —
    /// tarmac, lane markings, verges and every barrier — is a single draw call and needs none of
    /// the FBX's own materials at runtime. That also means the kit's grey asphalt never actually
    /// ships: colours come from BlockPalette, keyed off the source material names.
    ///
    /// The kit is authored top-down (road in the XZ plane), so every piece is pitched -90 about X
    /// to stand up and face the camera, then spun about Z to point along its edge.
    /// </summary>
    public class ConveyorTrack : MonoBehaviour
    {
        public Material TrackMaterial;

        [Header("RacingKit models (CC0)")]
        public GameObject StraightModel;
        public GameObject CornerModel;
        public GameObject KerbModel;
        public GameObject GateModel;
        public GameObject BarrierModelA;
        public GameObject BarrierModelB;

        /// <summary>
        /// Slightly behind the gameplay plane so towers riding the belt always render in front of
        /// the tarmac, but well in front of the bezel.
        /// </summary>
        const float TrackZ = 0.35f;

        /// <summary>Stands a top-down-authored piece up to face the camera.</summary>
        static readonly Quaternion StandUp = Quaternion.Euler(-90f, 0f, 0f);

        readonly List<Vector3> verts = new List<Vector3>(8192);
        readonly List<Vector3> normals = new List<Vector3>(8192);
        readonly List<Color32> colors = new List<Color32>(8192);
        readonly List<int> tris = new List<int>(12288);

        void Awake()
        {
            Layout.EnsureConfigured();
            Build();
        }

        void Build()
        {
            var straight = Piece.From(StraightModel);
            if (straight == null)
            {
                Debug.LogError("[FlowSort] ConveyorTrack has no straight road model.");
                return;
            }

            var corner = Piece.From(CornerModel) ?? straight;

            Vector2 c = Layout.GridCenter;
            Vector2 h = Layout.ConveyorHalf;
            float tw = Layout.TrackWidth;

            // The corner tiles sit on the quarter circles the path actually turns through, and
            // the straights fill what is left of each edge.
            float runX = ConveyorPath.BottomRun;
            float runY = ConveyorPath.SideRun;

            PlaceCorner(corner, new Vector2(c.x - h.x, c.y - h.y), new Vector2(1f, 1f));
            PlaceCorner(corner, new Vector2(c.x + h.x, c.y - h.y), new Vector2(-1f, 1f));
            PlaceCorner(corner, new Vector2(c.x + h.x, c.y + h.y), new Vector2(-1f, -1f));
            PlaceCorner(corner, new Vector2(c.x - h.x, c.y + h.y), new Vector2(1f, -1f));

            PlaceRun(straight, new Vector2(c.x, c.y - h.y), runX, tw, true);
            PlaceRun(straight, new Vector2(c.x, c.y + h.y), runX, tw, true);
            PlaceRun(straight, new Vector2(c.x - h.x, c.y), runY, tw, false);
            PlaceRun(straight, new Vector2(c.x + h.x, c.y), runY, tw, false);

            PlaceBarriers();
            AddDashes();
            PlaceGate();

            Emit();
        }

        // --- Placement ---

        /// <summary>
        /// A quarter-turn tile, spun so its road hugs the corner the two adjoining edges meet at.
        /// The spin is measured from the mesh rather than hard-coded: which way the kit's corner
        /// piece turns is not something that can be guessed, and getting it wrong leaves four
        /// broken junctions that are only visible on device.
        /// </summary>
        void PlaceCorner(Piece piece, Vector2 center, Vector2 hug)
        {
            float want = Mathf.Atan2(hug.y, hug.x) * Mathf.Rad2Deg;
            float have = Mathf.Atan2(piece.RoadCentroidDir.y, piece.RoadCentroidDir.x) * Mathf.Rad2Deg;
            float spin = Mathf.Round(Mathf.DeltaAngle(have, want) / 90f) * 90f;

            Add(piece, center, Layout.TrackWidth, Layout.TrackWidth, spin);
        }

        /// <summary>Tiles a straight edge run, stretching the last fraction of a tile across all of them.</summary>
        void PlaceRun(Piece piece, Vector2 center, float length, float width, bool horizontal)
        {
            if (length <= 0.01f) return;

            int count = Mathf.Max(1, Mathf.RoundToInt(length / width));
            float tile = length / count;

            float heading = horizontal ? 0f : 90f;

            for (int i = 0; i < count; i++)
            {
                float offset = -length * 0.5f + tile * (i + 0.5f);
                Vector2 p = horizontal
                    ? new Vector2(center.x + offset, center.y)
                    : new Vector2(center.x, center.y + offset);

                Add(piece, p, tile, width, heading);
            }
        }

        /// <summary>
        /// Crash barriers all the way round, walked by arc length rather than laid out as four
        /// square runs — which is what left a hard right angle sitting outside a curved corner.
        /// Each segment is placed on the path's outward normal and turned to its heading, so the
        /// line bends with the track.
        /// </summary>
        void PlaceBarriers()
        {
            var a = Piece.From(BarrierModelA);
            var b = Piece.From(BarrierModelB);
            if (a == null && b == null) return;
            a ??= b;
            b ??= a;

            float across = Layout.BarrierBand;
            float along = across * (a.Size.x / Mathf.Max(0.01f, a.Size.z));

            float offset = Layout.TrackWidth * 0.5f + across * 0.5f;
            if (ConveyorPath.Perimeter <= 0f || along <= 0.01f) return;

            // Spaced along the OFFSET line, not the centre line. Barriers sit outside the track,
            // where a corner arc is measurably longer than the arc the path itself follows —
            // spacing them by the centre line left a gap at every corner and half the tiles
            // missing around it.
            var rail = SampleOffsetLine(offset, out float total);
            if (total <= 0.01f) return;

            int count = Mathf.Max(4, Mathf.RoundToInt(total / along));
            float step = total / count;

            int cursor = 0;
            float walked = 0f;

            for (int i = 0; i < count; i++)
            {
                float target = step * (i + 0.5f);

                while (cursor < rail.Count - 2 && walked + rail[cursor].Length < target)
                {
                    walked += rail[cursor].Length;
                    cursor++;
                }

                var node = rail[cursor];
                float t = node.Length > 0.0001f ? (target - walked) / node.Length : 0f;

                var centre = Vector2.Lerp(node.Point, rail[cursor + 1].Point, t);
                float heading = Mathf.LerpAngle(node.Heading, rail[cursor + 1].Heading, t);

                var piece = (i & 1) == 0 ? a : b;
                var tint = BlockPalette.ToVertex(
                    (i & 1) == 0 ? BlockPalette.TrackBarrierA : BlockPalette.TrackBarrierB);

                // Slightly long so neighbours meet around the outside of a curve.
                Add(piece, centre, step * 1.06f, across, heading, tint, TrackZ - 0.02f);
            }
        }

        struct RailNode
        {
            public Vector2 Point;
            public float Heading;
            public float Length;   // to the next node
        }

        /// <summary>
        /// Walks the path once and returns the line offset outward from it, with the distance
        /// between consecutive samples. That is what lets anything lining the track be spaced by
        /// its own length rather than the path's.
        /// </summary>
        List<RailNode> SampleOffsetLine(float offset, out float total)
        {
            const int Samples = 480;

            var nodes = new List<RailNode>(Samples + 1);
            float perimeter = ConveyorPath.Perimeter;

            for (int i = 0; i <= Samples; i++)
            {
                float d = perimeter * i / Samples;
                Vector3 p = ConveyorPath.PositionAt(d);
                Vector2 n = ConveyorPath.NormalAt(d);

                nodes.Add(new RailNode
                {
                    Point = new Vector2(p.x + n.x * offset, p.y + n.y * offset),
                    Heading = ConveyorPath.RotationAt(d) + (i == Samples ? 360f : 0f),
                });
            }

            total = 0f;
            for (int i = 0; i < nodes.Count - 1; i++)
            {
                var node = nodes[i];
                node.Length = Vector2.Distance(node.Point, nodes[i + 1].Point);
                nodes[i] = node;
                total += node.Length;
            }

            return nodes;
        }

        /// <summary>
        /// The kit's overhead gantry, spanning the road where towers join and leave the belt.
        ///
        /// Laid flat like everything else: the board is seen from above, so a gantry towers drive
        /// THROUGH is one that spans across their direction of travel with a leg either side of
        /// the road. Standing it upright instead put it alongside the track rather than over it.
        /// </summary>
        void PlaceGate()
        {
            var gate = Piece.From(GateModel);
            if (gate == null) return;

            float d = ConveyorPath.EntryDistanceForX(Layout.GridCenter.x);
            Vector3 point = ConveyorPath.PositionAt(d);

            // Turned a quarter from the direction of travel, so its span crosses the road.
            float heading = ConveyorPath.RotationAt(d) + 90f;
            // Exactly the road, no more: any longer and it pokes into the picture behind it.
            float span = Layout.TrackWidth;
            float depth = span * (gate.Size.z / Mathf.Max(0.01f, gate.Size.x));

            Add(gate, new Vector2(point.x, point.y), span, Mathf.Max(depth, 0.9f), heading,
                null, TrackZ - 0.12f);
        }

        /// <summary>
        /// Gold centre-line dashes around the loop. The kit's tiles only carry edge lines, which
        /// left the belt reading as one flat band with no sense of a direction of travel; dashes
        /// are what turn it into a track you can see towers running along.
        ///
        /// Laid out along the same arc-length parameterisation the towers ride, so a dash is
        /// always exactly under their path. Corners are skipped — a straight dash on the turn
        /// would cut across the arc.
        /// </summary>
        void AddDashes()
        {
            const float pitch = 3.4f;
            const float length = 1.9f;
            const float width = 0.42f;

            float perimeter = ConveyorPath.Perimeter;
            if (perimeter <= 0f) return;

            var color = BlockPalette.ToVertex(BlockPalette.TrackVerge);
            float z = TrackZ - 0.05f;

            // Each dash is built from several short segments sampled along the path, so it bends
            // with the road instead of cutting a straight chord across a corner — which read as
            // the line kinking sideways as it approached one.
            const int Parts = 4;

            for (float d = 0f; d < perimeter; d += pitch)
            {
                for (int i = 0; i < Parts; i++)
                {
                    float from = d - length * 0.5f + length * i / Parts;
                    float to = d - length * 0.5f + length * (i + 1) / Parts;

                    Vector3 a = ConveyorPath.PositionAt(from);
                    Vector3 b = ConveyorPath.PositionAt(to);

                    Vector3 along = b - a;
                    if (along.sqrMagnitude < 1e-5f) continue;

                    Vector3 side = new Vector3(-along.y, along.x, 0f).normalized * (width * 0.5f);
                    AddQuad(a - side, b - side, b + side, a + side, z, color);
                }
            }
        }

        void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float z, Color32 color)
        {
            int i = verts.Count;

            verts.Add(new Vector3(a.x, a.y, z));
            verts.Add(new Vector3(b.x, b.y, z));
            verts.Add(new Vector3(c.x, c.y, z));
            verts.Add(new Vector3(d.x, d.y, z));

            for (int k = 0; k < 4; k++)
            {
                normals.Add(Vector3.back);
                colors.Add(color);
            }

            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        // --- Mesh assembly ---

        /// <summary>
        /// Appends one piece, stood up and spun, then stretched to an exact world width and height.
        ///
        /// Scale is applied in WORLD space after the rotation rather than as a local scale, so the
        /// caller can just say "this tile is 4.8 wide and 4.6 tall" without having to know which
        /// local axis that ended up being.
        /// </summary>
        /// <summary>
        /// Appends one piece, stretched to <paramref name="along"/> by <paramref name="across"/>
        /// in its OWN axes and then turned to a heading.
        ///
        /// Scaling has to happen before the rotation. Sizing against the rotated bounding box
        /// instead only works while every heading is a multiple of 90 degrees — the moment
        /// barriers started following the corner arcs, each one got stretched to fill its diagonal
        /// bounding box and the track sprouted shards at every corner.
        /// </summary>
        void Add(Piece piece, Vector2 center, float along, float across, float headingDegrees,
                 Color32? overrideColor = null, float? z = null, bool standUp = true,
                 float thickness = -1f)
        {
            // Pieces whose long axis is local Z need a quarter turn, and then their local X is
            // what runs across the track rather than along it.
            bool alongIsX = piece.RoadAlongLocalX;
            float spin = headingDegrees + (alongIsX ? 0f : 90f);

            var rot = standUp
                ? Quaternion.Euler(0f, 0f, spin) * StandUp
                : Quaternion.Euler(0f, 0f, spin);

            float depth = thickness > 0f ? thickness : Layout.TrackWidth * 0.2f;
            Vector3 size = piece.Size;

            var scale = standUp
                ? new Vector3(
                    (alongIsX ? along : across) / Mathf.Max(0.001f, size.x),
                    depth / Mathf.Max(0.001f, size.y),
                    (alongIsX ? across : along) / Mathf.Max(0.001f, size.z))
                : new Vector3(
                    along / Mathf.Max(0.001f, size.x),
                    across / Mathf.Max(0.001f, size.y),
                    depth / Mathf.Max(0.001f, size.z));

            Vector3 rotatedCenter = rot * Vector3.Scale(piece.Center, scale);
            var origin = new Vector3(center.x - rotatedCenter.x, center.y - rotatedCenter.y,
                                     (z ?? TrackZ) - rotatedCenter.z);

            var m = Matrix4x4.TRS(origin, rot, scale);

            int baseIndex = verts.Count;
            var srcVerts = piece.Vertices;
            var srcNormals = piece.Normals;

            for (int i = 0; i < srcVerts.Length; i++)
            {
                verts.Add(m.MultiplyPoint3x4(srcVerts[i]));
                normals.Add(i < srcNormals.Length
                    ? (rot * srcNormals[i]).normalized
                    : Vector3.back);
                colors.Add(Color.white);
            }

            for (int sub = 0; sub < piece.SubmeshTriangles.Length; sub++)
            {
                var color = overrideColor ?? piece.SubmeshColors[sub];
                var indices = piece.SubmeshTriangles[sub];

                for (int i = 0; i < indices.Length; i++)
                {
                    int v = baseIndex + indices[i];
                    colors[v] = color;
                    tris.Add(v);
                }
            }
        }

        void Emit()
        {
            if (verts.Count == 0) return;

            var mesh = new Mesh { name = "ConveyorTrack" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0, true);

            var go = new GameObject("TrackMesh", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = TrackMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            Debug.Log($"[FlowSort] Track built: {verts.Count} verts, {tris.Count / 3} tris, 1 draw call.");
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>
        /// A source model flattened into the arrays the combiner needs, plus the two facts about
        /// its geometry that placement depends on: which local axis its road runs along, and which
        /// way its road leans (for corner pieces).
        /// </summary>
        class Piece
        {
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public int[][] SubmeshTriangles;
            public Color32[] SubmeshColors;
            public Vector3 Center;
            public Vector3 Size;

            public bool RoadAlongLocalX;

            /// <summary>Road centroid offset from the piece centre, in the view plane (x, y).</summary>
            public Vector2 RoadCentroidDir;

            public static Piece From(GameObject source)
            {
                if (source == null) return null;

                var filter = source.GetComponentInChildren<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) return null;

                var mesh = filter.sharedMesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                var mats = renderer != null ? renderer.sharedMaterials : null;

                var piece = new Piece
                {
                    Vertices = mesh.vertices,
                    Normals = mesh.normals,
                    Center = mesh.bounds.center,
                    Size = mesh.bounds.size,
                    SubmeshTriangles = new int[mesh.subMeshCount][],
                    SubmeshColors = new Color32[mesh.subMeshCount],
                };

                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    piece.SubmeshTriangles[i] = mesh.GetTriangles(i);
                    string name = mats != null && i < mats.Length && mats[i] != null ? mats[i].name : "";
                    piece.SubmeshColors[i] = ColorFor(name);
                }

                piece.MeasureRoad(mesh, mats);
                return piece;
            }

            /// <summary>
            /// The kit's material name decides the colour. Converted for direct use as a vertex
            /// colour here, once per submesh, rather than per triangle index.
            /// </summary>
            static Color32 ColorFor(string materialName)
            {
                string n = materialName.ToLowerInvariant();
                if (n.Contains("grass")) return BlockPalette.ToVertex(BlockPalette.TrackVerge);
                if (n.Contains("grey") || n.Contains("gray") || n.Contains("white"))
                    return BlockPalette.ToVertex(BlockPalette.TrackMarking);
                if (n.Contains("red")) return BlockPalette.ToVertex(BlockPalette.TrackBarrierA);
                return BlockPalette.ToVertex(BlockPalette.TrackRoad);
            }

            /// <summary>
            /// Works out the road's run and lean from the tarmac submesh's own vertices. Both are
            /// properties of the kit's authoring that cannot be read off the bounds — the tile is
            /// square either way — and both are wrong in a way that only shows up on device.
            /// </summary>
            void MeasureRoad(Mesh mesh, Material[] mats)
            {
                int roadSub = -1;
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    string n = mats != null && i < mats.Length && mats[i] != null
                        ? mats[i].name.ToLowerInvariant() : "";
                    if (n.Contains("road")) { roadSub = i; break; }
                }

                // Single-material pieces (the barriers) have no road to measure.
                if (roadSub < 0)
                {
                    RoadAlongLocalX = Size.x >= Size.z;
                    RoadCentroidDir = new Vector2(1f, 0f);
                    return;
                }

                var indices = SubmeshTriangles[roadSub];
                var sum = Vector3.zero;
                var min = new Vector3(float.MaxValue, 0f, float.MaxValue);
                var max = new Vector3(float.MinValue, 0f, float.MinValue);

                for (int i = 0; i < indices.Length; i++)
                {
                    var v = Vertices[indices[i]];
                    sum += v;
                    min.x = Mathf.Min(min.x, v.x); max.x = Mathf.Max(max.x, v.x);
                    min.z = Mathf.Min(min.z, v.z); max.z = Mathf.Max(max.z, v.z);
                }

                if (indices.Length == 0)
                {
                    RoadAlongLocalX = true;
                    RoadCentroidDir = new Vector2(1f, 0f);
                    return;
                }

                RoadAlongLocalX = max.x - min.x >= max.z - min.z;

                // Local z becomes world y once the piece is stood up, so the lean is (x, z).
                Vector3 centroid = sum / indices.Length;
                var dir = new Vector2(centroid.x - Center.x, centroid.z - Center.z);
                RoadCentroidDir = dir.sqrMagnitude < 1e-6f ? new Vector2(1f, 1f) : dir.normalized;
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace FlowSort.Blocks
{
    /// <summary>
    /// Simulates and draws every ball. No Rigidbodies and no colliders anywhere (DESIGN.md §7):
    /// the wall is a uniform grid, so the cell under a ball is a floor-divide away. That is
    /// cheaper than ~900 BoxColliders and, with substepping, cannot tunnel.
    ///
    /// Shots travel dead straight and never bounce — the conveyor turning a tower 90 degrees at
    /// each corner is the entire aiming system. A shot eats through blocks of its OWN colour and
    /// dies the instant it meets any other, which is what makes wasted ammo the core cost.
    ///
    /// All balls draw in one call as a single rebuilt mesh of velocity-stretched quads, with a
    /// vertex-alpha gradient standing in for a per-ball TrailRenderer.
    /// </summary>
    public class BallSystem : MonoBehaviour
    {
        public BlockWall Wall;
        public Material BallMaterial;

        struct Ball
        {
            public Vector3 Pos;
            public Vector2 Vel;
            public byte Color;
            public int TargetX;
            public int TargetY;
            public bool Alive;
        }

        Ball[] balls;
        int spawnCursor;

        Mesh mesh;
        readonly List<Vector3> verts = new List<Vector3>(BlockTuning.MaxBalls * 4);
        readonly List<Color32> colors = new List<Color32>(BlockTuning.MaxBalls * 4);
        readonly List<Vector2> uvs = new List<Vector2>(BlockTuning.MaxBalls * 4);
        readonly List<int> tris = new List<int>(BlockTuning.MaxBalls * 6);

        public int ActiveCount { get; private set; }

        void Awake()
        {
            balls = new Ball[BlockTuning.MaxBalls];

            mesh = new Mesh { name = "Balls" };
            mesh.MarkDynamic();

            var go = new GameObject("BallMesh", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = BallMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        public bool Spawn(Vector3 position, Vector2 velocity, byte colorIndex, int targetX, int targetY)
        {
            for (int i = 0; i < balls.Length; i++)
            {
                int idx = (spawnCursor + i) % balls.Length;
                if (balls[idx].Alive) continue;

                balls[idx] = new Ball
                {
                    Pos = position,
                    Vel = velocity,
                    Color = colorIndex,
                    TargetX = targetX,
                    TargetY = targetY,
                    Alive = true
                };
                spawnCursor = (idx + 1) % balls.Length;
                return true;
            }
            return false; // pool exhausted — drop the shot rather than allocate mid-frame
        }

        public void ClearAll()
        {
            if (balls == null) return;
            for (int i = 0; i < balls.Length; i++) balls[i].Alive = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            ActiveCount = 0;

            for (int i = 0; i < balls.Length; i++)
            {
                if (!balls[i].Alive) continue;
                Simulate(ref balls[i], dt);
                if (balls[i].Alive) ActiveCount++;
            }

            BuildMesh();
        }

        void Simulate(ref Ball b, float dt)
        {
            float speed = b.Vel.magnitude;
            if (speed < 0.001f) { b.Alive = false; return; }

            // Guarantee no single step crosses more than a quarter tile.
            int steps = Mathf.Max(1, Mathf.CeilToInt(speed * dt / BlockTuning.MaxStepDistance));
            float subDt = dt / steps;

            for (int s = 0; s < steps && b.Alive; s++)
            {
                Vector3 next = b.Pos + new Vector3(b.Vel.x, b.Vel.y, 0f) * subDt;

                // One shot destroys exactly ONE block and is gone — no piercing. The tower only
                // ever fires when the first block ahead already matches, so a shot that reaches
                // any solid cell has connected with its intended target.
                if (Wall != null && Wall.WorldToCell(next, out int cx, out int cy) && Wall.IsSolid(cx, cy))
                {
                    Wall.Release(b.TargetX, b.TargetY);
                    Wall.Destroy(cx, cy);
                    b.Alive = false;
                    break;
                }

                b.Pos = next;

                if (OutsidePlayfield(b.Pos))
                {
                    // Target was cleared by someone else mid-flight; free the claim.
                    Wall.Release(b.TargetX, b.TargetY);
                    b.Alive = false;
                    break;
                }
            }
        }

        static Color32[] trailColors;

        /// <summary>A block colour lifted toward white, ready to write straight into the mesh.</summary>
        static Color32 TrailColor(byte colorIndex)
        {
            if (trailColors == null)
            {
                trailColors = new Color32[BlockPalette.Count];
                for (byte i = 0; i < BlockPalette.Count; i++)
                {
                    Color tint = BlockPalette.Get(i);
                    trailColors[i] = BlockPalette.ToVertex(new Color32(
                        (byte)Mathf.Min(255f, tint.r * 255f + 70f),
                        (byte)Mathf.Min(255f, tint.g * 255f + 70f),
                        (byte)Mathf.Min(255f, tint.b * 255f + 70f), 255));
                }
            }

            return trailColors[colorIndex < BlockPalette.Count ? colorIndex : 0];
        }

        static bool OutsidePlayfield(Vector3 p)
        {
            Vector2 c = Layout.GridCenter;
            Vector2 h = Layout.ConveyorHalf;
            return p.x < c.x - h.x || p.x > c.x + h.x || p.y < c.y - h.y || p.y > c.y + h.y;
        }

        void BuildMesh()
        {
            verts.Clear();
            colors.Clear();
            uvs.Clear();
            tris.Clear();

            float r = BlockTuning.BallRadius;

            for (int i = 0; i < balls.Length; i++)
            {
                if (!balls[i].Alive) continue;

                Vector2 v = balls[i].Vel;
                float speed = v.magnitude;
                Vector2 dir = speed > 0.001f ? v / speed : Vector2.up;
                Vector2 perp = new Vector2(-dir.y, dir.x) * r;

                Vector3 head = balls[i].Pos + new Vector3(dir.x, dir.y, 0f) * r;
                Vector3 tail = balls[i].Pos - new Vector3(dir.x, dir.y, 0f) * BlockTuning.BallTrailLength;

                int baseIndex = verts.Count;

                verts.Add(head + new Vector3(perp.x, perp.y, 0f));
                verts.Add(head - new Vector3(perp.x, perp.y, 0f));
                verts.Add(tail - new Vector3(perp.x, perp.y, 0f));
                verts.Add(tail + new Vector3(perp.x, perp.y, 0f));

                // Tinted to the shot's own colour so the player can read at a glance which
                // blocks a stream is able to eat. Precomputed per palette index — this runs for
                // every live shot every frame.
                var headColor = TrailColor(balls[i].Color);
                var tailColor = new Color32(headColor.r, headColor.g, headColor.b, 0);

                colors.Add(headColor); colors.Add(headColor);
                colors.Add(tailColor); colors.Add(tailColor);

                uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(0f, 0f));

                tris.Add(baseIndex); tris.Add(baseIndex + 1); tris.Add(baseIndex + 2);
                tris.Add(baseIndex); tris.Add(baseIndex + 2); tris.Add(baseIndex + 3);
            }

            mesh.Clear();
            if (verts.Count == 0) return;

            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0, false);
        }
    }
}

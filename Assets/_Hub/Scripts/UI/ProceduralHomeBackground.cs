using UnityEngine;
using UnityEngine.UI;

namespace Miniverse.Hub
{
    /// <summary>
    /// Home's full-screen backdrop -- replaces the flat black/white the skeleton app shipped
    /// with. Same "generate, don't hand-place" contract as Frontline's ProceduralMenuBackground
    /// (Assets/Games/Frontline/Scripts/Core/ProceduralMenuBackground.cs): a Texture2D baked once
    /// at Awake, not an authored asset. Cool blue-to-violet gradient (a hub/menu screen, not
    /// Frontline's warm battlefield-sunset one) with a soft glow behind where the game grid sits
    /// and small scattered dot/star doodles for texture, per Simon's "tiny decorative background
    /// doodles" ask.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class ProceduralHomeBackground : MonoBehaviour
    {
        static readonly Color Top = new Color(0.30f, 0.42f, 0.78f);
        static readonly Color Mid = new Color(0.20f, 0.24f, 0.52f);
        static readonly Color Bottom = new Color(0.10f, 0.10f, 0.24f);

        const int W = 180, H = 320;

        void Awake()
        {
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var rng = new System.Random(7); // fixed seed -- same backdrop every launch
            var dots = new (float x, float y, float r, float a)[26];
            for (int i = 0; i < dots.Length; i++)
                dots[i] = ((float)rng.NextDouble(), (float)rng.NextDouble(),
                           0.01f + (float)rng.NextDouble() * 0.012f, 0.10f + (float)rng.NextDouble() * 0.12f);

            var blobs = new (float x, float y, float r, float a)[6];
            for (int i = 0; i < blobs.Length; i++)
                blobs[i] = ((float)rng.NextDouble(), (float)rng.NextDouble(),
                            0.14f + (float)rng.NextDouble() * 0.18f, 0.04f + (float)rng.NextDouble() * 0.05f);

            for (int y = 0; y < H; y++)
            {
                float v = y / (float)(H - 1); // 0 = bottom, 1 = top
                Color baseCol = v > 0.55f
                    ? Color.Lerp(Mid, Top, (v - 0.55f) / 0.45f)
                    : Color.Lerp(Bottom, Mid, v / 0.55f);

                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)(W - 1);

                    // Soft glow behind the grid's usual position (upper-middle of the screen).
                    float gx = (u - 0.5f) * 2f;
                    float gy = (v - 0.62f) * 2f;
                    float glow = Mathf.Clamp01(1f - Mathf.Sqrt(gx * gx + gy * gy));
                    glow = glow * glow * 0.14f;

                    Color c = baseCol + new Color(glow * 0.5f, glow * 0.55f, glow * 0.7f);

                    foreach (var b in blobs)
                    {
                        float dx = u - b.x, dy = v - b.y;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float falloff = Mathf.Clamp01(1f - d / b.r);
                        c += Color.white * (falloff * falloff * b.a);
                    }

                    foreach (var dot in dots)
                    {
                        float dx = u - dot.x, dy = v - dot.y;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d < dot.r) c += Color.white * dot.a * (1f - d / dot.r);
                    }

                    tex.SetPixel(x, y, new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b)));
                }
            }
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, W, H), new Vector2(0.5f, 0.5f));
            GetComponent<Image>().sprite = sprite;
        }
    }
}

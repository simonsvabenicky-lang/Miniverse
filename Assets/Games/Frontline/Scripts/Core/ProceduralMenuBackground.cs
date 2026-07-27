using UnityEngine;
using UnityEngine.UI;

namespace Frontline
{
    /// <summary>
    /// Every non-gameplay screen used to show the live, paused battlefield (soldier + lanes)
    /// dimmed behind it -- reads as a menu bolted onto gameplay rather than an actual menu, since
    /// nothing in the reference genre (Archero, Survivor.io, Last War) does that. This paints a
    /// full-screen backdrop instead: a warm sunset-over-the-front gradient (colourful, not the
    /// flat near-black first pass landed on -- see Simon's "should be colorful, fits our vibe"
    /// feedback), a few soft drifting cloud blobs for texture the way a reference level-select
    /// screen used a bubble pattern, and a soft glow behind the title/button column. Baked into a
    /// Texture2D once at Awake, not authored -- same "generate, don't hand-place" contract as
    /// everything else CanvasBuilder builds. GameUI shows/hides the object this sits on
    /// (Screen != Screen_.Playing) exactly like every other screen canvas.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class ProceduralMenuBackground : MonoBehaviour
    {
        static readonly Color Top = new Color(1.00f, 0.72f, 0.30f);
        static readonly Color Mid = new Color(0.95f, 0.40f, 0.18f);
        static readonly Color Bottom = new Color(0.50f, 0.13f, 0.14f);

        const int W = 160, H = 288;

        void Awake()
        {
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            // Fixed seed: the backdrop should look the same every launch, not reshuffle.
            var rng = new System.Random(1);
            var blobs = new (float x, float y, float r, float a)[9];
            for (int i = 0; i < blobs.Length; i++)
                blobs[i] = ((float)rng.NextDouble(), (float)rng.NextDouble(),
                            0.10f + (float)rng.NextDouble() * 0.16f, 0.05f + (float)rng.NextDouble() * 0.05f);

            for (int y = 0; y < H; y++)
            {
                float v = y / (float)(H - 1); // 0 = bottom, 1 = top
                Color baseCol = v > 0.5f
                    ? Color.Lerp(Mid, Top, (v - 0.5f) * 2f)
                    : Color.Lerp(Bottom, Mid, v * 2f);

                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)(W - 1);

                    float vx = (u - 0.5f) * 2f;
                    float vy = (v - 0.5f) * 2f;
                    float vDist = Mathf.Sqrt(vx * vx + vy * vy * 0.7f);
                    float vignette = 1f - Mathf.SmoothStep(0.5f, 1.3f, vDist) * 0.35f;

                    // A soft glow sat behind where the title/button column always lands
                    // (upper-centre) -- just enough to keep the backdrop from reading as flat.
                    float gx = (u - 0.5f) * 2f;
                    float gy = (v - 0.62f) * 2.4f;
                    float glow = Mathf.Clamp01(1f - Mathf.Sqrt(gx * gx + gy * gy));
                    glow = glow * glow * 0.16f;

                    Color c = baseCol * vignette + new Color(glow, glow * 0.9f, glow * 0.6f);

                    // Soft warm "cloud" blobs, like the reference level-select screen's bubble
                    // pattern -- breaks up the gradient without competing with UI on top of it.
                    foreach (var b in blobs)
                    {
                        float dx = u - b.x, dy = v - b.y;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float falloff = Mathf.Clamp01(1f - d / b.r);
                        c += Color.white * (falloff * falloff * b.a);
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

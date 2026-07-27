using UnityEngine;
using UnityEngine.UI;

namespace Frontline
{
    /// <summary>
    /// The top bar's "lives" badge needed a heart, and nothing in the imported Kenney Icons set
    /// (gear/shoppingCart/trophy/target/medal1, see Assets/Art/UI/Icons) is one -- so this bakes
    /// a small heart-shaped Texture2D at Awake the same way ProceduralMenuBackground bakes the
    /// backdrop, rather than hand-authoring a sprite that would break the "generate, don't
    /// hand-place" contract every other CanvasBuilder asset follows.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class ProceduralHeartIcon : MonoBehaviour
    {
        const int S = 48;
        const int Super = 3; // 3x3 supersampling -- a raw per-pixel test on a curved silhouette this small is visibly jagged

        static readonly Vector2 C1 = new Vector2(-0.34f, 0.28f);
        static readonly Vector2 C2 = new Vector2(0.34f, 0.28f);
        const float R = 0.44f;
        static readonly Vector2 Apex = new Vector2(0f, -0.9f);
        static readonly Vector2 TriL = new Vector2(-0.80f, 0.30f);
        static readonly Vector2 TriR = new Vector2(0.80f, 0.30f);

        void Awake()
        {
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < Super; sy++)
                    {
                        for (int sx = 0; sx < Super; sx++)
                        {
                            float px = ((x + (sx + 0.5f) / Super) / S - 0.5f) * 2.3f;
                            float py = ((y + (sy + 0.5f) / Super) / S - 0.5f) * 2.3f;
                            if (InsideHeart(px, py)) hits++;
                        }
                    }
                    float a = hits / (float)(Super * Super);
                    tex.SetPixel(x, y, new Color(0.95f, 0.22f, 0.28f, a));
                }
            }
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, S, S), new Vector2(0.5f, 0.5f));
            GetComponent<Image>().sprite = sprite;
        }

        static bool InsideHeart(float px, float py)
        {
            var p = new Vector2(px, py);
            return Vector2.Distance(p, C1) <= R || Vector2.Distance(p, C2) <= R || PointInTriangle(p, Apex, TriL, TriR);
        }

        static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p - a, b - a);
            float d2 = Cross(p - b, c - b);
            float d3 = Cross(p - c, a - c);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}

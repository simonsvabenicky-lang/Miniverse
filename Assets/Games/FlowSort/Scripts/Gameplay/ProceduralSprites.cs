using UnityEngine;

namespace FlowSort.Gameplay
{
    /// <summary>
    /// Flat-shape sprites drawn in code at runtime — this genre doesn't need premade art, and
    /// procedural placeholders are cheap to iterate on (same reasoning as the hub's placeholder
    /// icon). Cached per-shape since every orb/fork/bin of a kind reuses the same texture.
    /// </summary>
    public static class ProceduralSprites
    {
        static Sprite circle;
        static Sprite roundedRect;
        static Sprite triangle;

        public static Sprite Circle() => circle ??= BuildCircle(64);
        public static Sprite RoundedRect() => roundedRect ??= BuildRoundedRect(96, 64, 14);
        public static Sprite Triangle() => triangle ??= BuildTriangle(64);

        static Sprite BuildCircle(int size)
        {
            var tex = NewTex(size, size);
            Vector2 c = new Vector2(size / 2f, size / 2f);
            float r = size / 2f - 1f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) <= r ? Color.white : Color.clear);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite BuildRoundedRect(int w, int h, float corner)
        {
            var tex = NewTex(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, InsideRounded(x + 0.5f, y + 0.5f, w, h, corner) ? Color.white : Color.clear);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static bool InsideRounded(float x, float y, int w, int h, float r)
        {
            float nx = Mathf.Clamp(x, r, w - r);
            float ny = Mathf.Clamp(y, r, h - r);
            if (x >= r && x <= w - r) return y >= 0 && y <= h;
            if (y >= r && y <= h - r) return x >= 0 && x <= w;
            return Vector2.Distance(new Vector2(x, y), new Vector2(nx, ny)) <= r;
        }

        static Sprite BuildTriangle(int size)
        {
            var tex = NewTex(size, size);
            Vector2 a = new Vector2(2, 2);
            Vector2 b = new Vector2(2, size - 2);
            Vector2 c = new Vector2(size - 2, size / 2f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    tex.SetPixel(x, y, PointInTriangle(p, a, b, c) ? Color.white : Color.clear);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
            bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(hasNeg && hasPos);
        }

        static float Sign(Vector2 p1, Vector2 p2, Vector2 p3) =>
            (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);

        static Texture2D NewTex(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
    }
}

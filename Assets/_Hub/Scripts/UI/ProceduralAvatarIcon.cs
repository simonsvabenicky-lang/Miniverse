using UnityEngine;
using UnityEngine.UI;

namespace Miniverse.Hub
{
    /// <summary>
    /// The top bar's profile button icon. Same reasoning as Frontline's ProceduralHeartIcon: no
    /// person/profile silhouette exists in this project's imported Kenney sprite subset (checked
    /// kenney_game-icons, the expansion, and the base UI pack -- the closest match was a robot
    /// icon, which reads wrong for "profile"), so this bakes a simple flat silhouette instead of
    /// hand-authoring one. A circle head + shoulder arc is legible at icon size and unambiguous.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class ProceduralAvatarIcon : MonoBehaviour
    {
        const int S = 48;
        const int Super = 3;

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
                            if (InsideAvatar(px, py)) hits++;
                        }
                    }
                    float a = hits / (float)(Super * Super);
                    // Light, not dark: the Basic GUI Bundle icon-button backgrounds (see
                    // HubCanvasBuilder.BuildShell's ProfileButton, BuildProfilePanel's avatar
                    // circle) are medium-dark slate with a bold black outline -- the pack's own
                    // icons all use a light fill on that same style of background. An earlier pass
                    // used dark navy here for Kenney's light-grey buttons; that combination would
                    // now read as a near-invisible dark-on-dark blob.
                    tex.SetPixel(x, y, new Color(0.88f, 0.90f, 0.95f, a));
                }
            }
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, S, S), new Vector2(0.5f, 0.5f));
            GetComponent<Image>().sprite = sprite;
        }

        static bool InsideAvatar(float px, float py)
        {
            // Head: a circle sat slightly above centre.
            if (Vector2.Distance(new Vector2(px, py), new Vector2(0f, -0.32f)) <= 0.34f) return true;

            // Shoulders: the top of a wide circle, clipped by a horizontal line so only the
            // "shoulder arc" shows rather than a full second head.
            bool inShoulderCircle = Vector2.Distance(new Vector2(px, py), new Vector2(0f, 1.05f)) <= 0.78f;
            return inShoulderCircle && py < 0.30f;
        }
    }
}

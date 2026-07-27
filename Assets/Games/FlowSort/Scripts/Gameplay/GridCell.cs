using UnityEngine;

namespace FlowSort.Gameplay
{
    /// <summary>
    /// One tile in the puzzle grid. Always instantiated at runtime by PuzzleGrid — grids
    /// regenerate every level, so nothing here is ever edit-time-authored or saved into the
    /// scene, which sidesteps the serialization pitfalls a MonoBehaviour configured only via a
    /// method call can run into (see Bin's history in HANDOFF.md).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class GridCell : MonoBehaviour
    {
        public PieceColor Color { get; private set; }
        public bool HasKey { get; private set; }
        public bool Cleared { get; private set; }

        SpriteRenderer sr;

        public void Setup(PieceColor color, bool hasKey)
        {
            Color = color;
            HasKey = hasKey;
            Cleared = false;

            sr = GetComponent<SpriteRenderer>();
            sr.sprite = ArtRegistry.Instance.Block(color);
            sr.sortingOrder = 10;
            ScaleToFit(sr, GameTuning.CellSize * 0.92f);

            if (hasKey)
            {
                var keyGo = new GameObject("Key", typeof(SpriteRenderer));
                keyGo.transform.SetParent(transform, false);
                keyGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
                var keySr = keyGo.GetComponent<SpriteRenderer>();
                keySr.sprite = ArtRegistry.Instance.KeyIcon;
                keySr.color = new Color(1f, 0.84f, 0.2f, 1f);
                keySr.sortingOrder = 11;
                ScaleToFit(keySr, GameTuning.CellSize * 0.55f);
            }

            gameObject.SetActive(true);
        }

        public void Clear()
        {
            Cleared = true;
            gameObject.SetActive(false);
        }

        public void Restore()
        {
            Cleared = false;
            gameObject.SetActive(true);
        }

        static void ScaleToFit(SpriteRenderer renderer, float worldSize)
        {
            float srcSize = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
            float scale = worldSize / srcSize;
            renderer.transform.localScale = Vector3.one * scale;
        }
    }
}

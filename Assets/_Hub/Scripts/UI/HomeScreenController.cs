using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Miniverse.Hub
{
    /// <summary>
    /// Populates the Home screen's game grid from GameCatalog at runtime. Tiles are built in
    /// code rather than instantiated from a prefab asset, matching Frontline's
    /// generate-don't-hand-author convention — one less asset type to keep in sync by hand.
    ///
    /// Third pass at the card design: Simon is now hand-authoring each game's icon art with
    /// the name already baked into the image (key-art style, see Frontline's icon), so this
    /// no longer draws any text of its own — no name strip, no letter-mark fallback. The
    /// accent-coloured frame (_cardFrame, still green/blue/orange per game) stays as the
    /// background box; the icon art sits inset within it so the frame reads as a lining
    /// border all the way around, same as a photo in a mat. A game with no icon yet just
    /// shows the bare accent box — honest about what's real, no synthesized substitute.
    /// </summary>
    public class HomeScreenController : MonoBehaviour
    {
        [SerializeField] RectTransform _gridParent;
        [SerializeField] TextMeshProUGUI _emptyStateLabel;
        [SerializeField] Sprite _cardFrame;
        [SerializeField] Color[] _accentColors;

        void Start()
        {
            Rebuild();
        }

        public void Rebuild()
        {
            foreach (Transform child in _gridParent)
                Destroy(child.gameObject);

            var games = GameCatalog.All;
            if (_emptyStateLabel != null) _emptyStateLabel.gameObject.SetActive(games.Count == 0);

            foreach (var def in games)
                BuildTile(def);
        }

        void BuildTile(MiniGameDef def)
        {
            Color accent = PickAccent(def);

            var tile = new GameObject($"Tile_{def.gameId}", typeof(RectTransform));
            tile.transform.SetParent(_gridParent, false);

            // Drop shadow: a second copy of the same frame sprite, dark and offset behind the
            // card -- one flat panel alone reads as a coloured rectangle, not a raised card.
            var shadowGo = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
            shadowGo.transform.SetParent(tile.transform, false);
            var shadowRect = (RectTransform)shadowGo.transform;
            shadowRect.anchorMin = Vector2.zero;
            shadowRect.anchorMax = Vector2.one;
            shadowRect.offsetMin = new Vector2(5f, -10f);
            shadowRect.offsetMax = new Vector2(5f, -10f);
            var shadowImg = shadowGo.GetComponent<Image>();
            shadowImg.sprite = _cardFrame;
            shadowImg.type = Image.Type.Sliced;
            shadowImg.color = new Color(0f, 0f, 0f, 0.30f);
            shadowImg.raycastTarget = false;

            var cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(Button));
            cardGo.transform.SetParent(tile.transform, false);
            var cardRect = (RectTransform)cardGo.transform;
            cardRect.anchorMin = Vector2.zero;
            cardRect.anchorMax = Vector2.one;
            cardRect.offsetMin = Vector2.zero;
            cardRect.offsetMax = Vector2.zero;
            var cardImg = cardGo.GetComponent<Image>();
            cardImg.sprite = _cardFrame;
            cardImg.type = Image.Type.Sliced;
            cardImg.color = accent;

            var button = cardGo.GetComponent<Button>();
            button.targetGraphic = cardImg;
            var colors = button.colors;
            colors.pressedColor = new Color(accent.r * 0.85f, accent.g * 0.85f, accent.b * 0.85f, 1f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            button.onClick.AddListener(() => HubLauncher.Instance.LaunchGame(def));

            BuildArt(cardRect, def);
        }

        /// <summary>The game's icon art, inset within the accent-coloured frame so the frame shows as a lining border all the way around. No text drawn here -- the icon art itself already carries the game's name.</summary>
        void BuildArt(Transform cardRect, MiniGameDef def)
        {
            if (def.icon == null) return;

            const float margin = 0.10f;
            var artRect = Anchor(cardRect, "Art", new Vector2(margin, margin), new Vector2(1f - margin, 1f - margin));
            var img = artRect.gameObject.AddComponent<Image>();
            img.sprite = def.icon;
            // Stretch to fill the inset rect exactly -- preserveAspect would letterbox/
            // pillarbox and leave a gap of bare accent colour between the art and the frame
            // whenever the icon's aspect doesn't match the tile's. Simon's icons are being
            // authored to the tile's own aspect, so a uniform stretch is the correct fit.
            img.preserveAspect = false;
            img.raycastTarget = false;
        }

        Color PickAccent(MiniGameDef def)
        {
            if (_accentColors == null || _accentColors.Length == 0) return Color.grey;
            int index = Mathf.Abs(def.gameId.GetHashCode()) % _accentColors.Length;
            return _accentColors[index];
        }

        static RectTransform Anchor(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }
    }
}

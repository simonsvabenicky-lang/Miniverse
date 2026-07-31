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
    /// Second pass at the card design, per Simon's own correction: "now its a massive rectangle
    /// and a small logo in the middle thats the opposite of wehat we want". The game's own
    /// icon/art now fills almost the entire tile (not a small 84x84 badge floating in a mostly
    /// empty card), with the name integrated as a strip across the top instead of a caption
    /// below. No per-game icon exists for every game yet (MiniGameDef.icon is unset on some
    /// graduated games), so those still fall back to a big bold letter mark rather than a fake
    /// icon — honest about what's real, same as Frontline's "(none yet)" settings rows. Swap in
    /// def.icon automatically the moment a game ships one.
    /// </summary>
    public class HomeScreenController : MonoBehaviour
    {
        [SerializeField] RectTransform _gridParent;
        [SerializeField] TextMeshProUGUI _emptyStateLabel;
        [SerializeField] Sprite _cardFrame;
        [SerializeField] Color[] _accentColors;
        [SerializeField] TMP_FontAsset _titleFont;

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

            BuildNameStrip(cardRect, def);
            BuildArt(cardRect, def);
        }

        /// <summary>Top strip: the game's name, bold, with a short underline accent -- same visual language as a real store listing's title, not a caption squeezed under a badge.</summary>
        void BuildNameStrip(Transform cardRect, MiniGameDef def)
        {
            var nameRect = Anchor(cardRect, "Name", new Vector2(0.07f, 0.80f), new Vector2(0.93f, 0.95f));
            var nameTmp = nameRect.gameObject.AddComponent<TextMeshProUGUI>();
            nameTmp.text = def.displayName;
            nameTmp.font = _titleFont;
            nameTmp.fontSize = 26;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.BottomLeft;
            nameTmp.enableAutoSizing = true;
            nameTmp.fontSizeMin = 14;
            nameTmp.fontSizeMax = 26;
            nameTmp.color = Color.white;
            nameTmp.raycastTarget = false;

            var underline = Anchor(cardRect, "Underline", new Vector2(0.07f, 0.76f), new Vector2(0.42f, 0.785f));
            underline.gameObject.AddComponent<Image>().color = Color.white;
            underline.GetComponent<Image>().raycastTarget = false;
        }

        /// <summary>The whole rest of the card, edge to edge -- def.icon if the game has shipped one, otherwise a big fallback letter mark. Either way this is the dominant thing on the tile, not a small centred badge.</summary>
        void BuildArt(Transform cardRect, MiniGameDef def)
        {
            var artRect = Anchor(cardRect, "Art", new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.72f));

            if (def.icon != null)
            {
                var img = artRect.gameObject.AddComponent<Image>();
                img.sprite = def.icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
                return;
            }

            var letterTmp = artRect.gameObject.AddComponent<TextMeshProUGUI>();
            letterTmp.text = string.IsNullOrEmpty(def.displayName) ? "?" : def.displayName.Substring(0, 1).ToUpperInvariant();
            letterTmp.font = _titleFont;
            letterTmp.fontSize = 160;
            letterTmp.enableAutoSizing = true;
            letterTmp.fontSizeMin = 40;
            letterTmp.fontSizeMax = 220;
            letterTmp.fontStyle = FontStyles.Bold;
            letterTmp.alignment = TextAlignmentOptions.Center;
            letterTmp.color = new Color(1f, 1f, 1f, 0.92f);
            letterTmp.raycastTarget = false;
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

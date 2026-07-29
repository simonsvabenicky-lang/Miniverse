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
    /// Each tile is a real card now (panel + drop shadow + a coloured badge + name), not the
    /// flat solid-colour square the skeleton app shipped with — see HubCanvasBuilder's doc
    /// comment for the rest of the "make it feel like an app" pass this was part of. No
    /// per-game icon exists yet (MiniGameDef.icon is unset on every graduated game so far), so
    /// the badge is a coloured circle with the game's initial rather than a fake icon —
    /// honest about what's real, same as Frontline's "(none yet)" settings rows. Swap in
    /// def.icon automatically the moment a game actually ships one.
    /// </summary>
    public class HomeScreenController : MonoBehaviour
    {
        [SerializeField] RectTransform _gridParent;
        [SerializeField] TextMeshProUGUI _emptyStateLabel;
        [SerializeField] Sprite _cardBackground;
        [SerializeField] Sprite[] _badgeSprites; // one round-gloss sprite per palette colour (Blue/Green/Yellow/Grey)

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
            var tile = new GameObject($"Tile_{def.gameId}", typeof(RectTransform));
            tile.transform.SetParent(_gridParent, false);

            // Drop shadow: a second copy of the same panel sprite, dark and offset behind the
            // card -- one flat panel alone reads as a coloured rectangle, not a raised card.
            var shadowGo = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
            shadowGo.transform.SetParent(tile.transform, false);
            var shadowRect = (RectTransform)shadowGo.transform;
            shadowRect.anchorMin = Vector2.zero;
            shadowRect.anchorMax = Vector2.one;
            shadowRect.offsetMin = new Vector2(5f, -10f);
            shadowRect.offsetMax = new Vector2(5f, -10f);
            var shadowImg = shadowGo.GetComponent<Image>();
            shadowImg.sprite = _cardBackground;
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
            cardImg.sprite = _cardBackground;
            cardImg.type = Image.Type.Sliced;
            cardImg.color = new Color(0.97f, 0.97f, 1f);

            var button = cardGo.GetComponent<Button>();
            button.targetGraphic = cardImg;
            var colors = button.colors;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.9f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            button.onClick.AddListener(() => HubLauncher.Instance.LaunchGame(def));

            BuildBadge(cardRect, def);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(cardRect, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = new Vector2(0.06f, 0.04f);
            labelRect.anchorMax = new Vector2(0.94f, 0.34f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.text = def.displayName;
            labelTmp.fontSize = 22;
            labelTmp.fontStyle = FontStyles.Bold;
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.enableAutoSizing = true;
            labelTmp.fontSizeMin = 12;
            labelTmp.fontSizeMax = 22;
            labelTmp.color = new Color(0.15f, 0.15f, 0.2f);
            labelTmp.raycastTarget = false;
        }

        void BuildBadge(Transform parent, MiniGameDef def)
        {
            var badgeGo = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            badgeGo.transform.SetParent(parent, false);
            var badgeRect = (RectTransform)badgeGo.transform;
            badgeRect.anchorMin = new Vector2(0.5f, 0.68f);
            badgeRect.anchorMax = new Vector2(0.5f, 0.68f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.sizeDelta = new Vector2(84f, 84f);

            var badgeImg = badgeGo.GetComponent<Image>();
            badgeImg.raycastTarget = false;

            if (def.icon != null)
            {
                badgeImg.sprite = def.icon;
                return;
            }

            if (_badgeSprites != null && _badgeSprites.Length > 0)
            {
                int index = Mathf.Abs(def.gameId.GetHashCode()) % _badgeSprites.Length;
                badgeImg.sprite = _badgeSprites[index];
            }

            var letterGo = new GameObject("Letter", typeof(RectTransform));
            letterGo.transform.SetParent(badgeRect, false);
            var letterRect = (RectTransform)letterGo.transform;
            letterRect.anchorMin = Vector2.zero;
            letterRect.anchorMax = Vector2.one;
            letterRect.offsetMin = Vector2.zero;
            letterRect.offsetMax = Vector2.zero;
            var letterTmp = letterGo.AddComponent<TextMeshProUGUI>();
            letterTmp.text = string.IsNullOrEmpty(def.displayName) ? "?" : def.displayName.Substring(0, 1).ToUpperInvariant();
            letterTmp.fontSize = 36;
            letterTmp.fontStyle = FontStyles.Bold;
            letterTmp.alignment = TextAlignmentOptions.Center;
            letterTmp.color = Color.white;
            letterTmp.raycastTarget = false;
        }
    }
}

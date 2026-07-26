using UnityEngine;
using UnityEngine.UI;

namespace Miniverse.Hub
{
    /// <summary>
    /// Populates the Home screen's game grid from GameCatalog at runtime. Tiles are built in
    /// code rather than instantiated from a prefab asset, matching Frontline's
    /// generate-don't-hand-author convention — one less asset type to keep in sync by hand.
    /// </summary>
    public class HomeScreenController : MonoBehaviour
    {
        [SerializeField] RectTransform _gridParent;
        [SerializeField] Text _emptyStateLabel;

        void Start()
        {
            Rebuild();
        }

        public void Rebuild()
        {
            foreach (Transform child in _gridParent)
                Destroy(child.gameObject);

            var games = GameCatalog.All;
            _emptyStateLabel.gameObject.SetActive(games.Count == 0);

            foreach (var def in games)
                BuildTile(def);
        }

        void BuildTile(MiniGameDef def)
        {
            var tile = new GameObject($"Tile_{def.gameId}", typeof(RectTransform), typeof(Image), typeof(Button));
            tile.transform.SetParent(_gridParent, false);
            tile.GetComponent<Image>().color = new Color(0.16f, 0.16f, 0.2f);

            var labelGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGO.transform.SetParent(tile.transform, false);
            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelGO.GetComponent<Text>();
            label.text = def.displayName;
            label.alignment = TextAnchor.MiddleCenter;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.color = Color.white;

            tile.GetComponent<Button>().onClick.AddListener(() => HubLauncher.Instance.LaunchGame(def));
        }
    }
}

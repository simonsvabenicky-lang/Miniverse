using UnityEngine;

namespace Miniverse.Hub
{
    /// <summary>
    /// One entry in the hub's game catalog. Each graduated minigame gets exactly one of
    /// these, saved under Assets/Games/&lt;Name&gt;/Resources/GameCatalog/. Unity merges
    /// same-relative-path Resources folders project-wide, so GameCatalog.All finds every
    /// game's def via Resources.LoadAll("GameCatalog") without any shared file needing an
    /// edit — that's the point: grafting in a new game never touches a file another game's
    /// graduation also touched.
    /// </summary>
    [CreateAssetMenu(fileName = "GameDef", menuName = "Miniverse/MiniGame Definition")]
    public class MiniGameDef : ScriptableObject
    {
        [Tooltip("Stable identifier, e.g. \"frontline\". Never rename once shipped — it's used as the save-data key.")]
        public string gameId;

        public string displayName;

        [Tooltip("Scene file name (no path/extension) that gets loaded additively when the player picks this game.")]
        public string sceneName;

        public Sprite icon;

        public string category = "Action";

        [Tooltip("Unticked games are hidden from the Home grid (e.g. mid-graduation, not yet playable).")]
        public bool enabled = true;
    }
}

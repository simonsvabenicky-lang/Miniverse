using Miniverse.Hub;
using FlowSort.Blocks;
using FlowSort.Meta;
using UnityEngine;

namespace FlowSort.Hub
{
    /// <summary>
    /// PocketVerse wrapper for FlowSort.
    ///
    /// Lives outside Assets/ in the FlowSort project (Miniverse.Hub/IMiniGame don't exist here,
    /// so this can't compile standalone) — drop into PocketVerse/Assets/Games/FlowSort/Scripts/
    /// at graduation and attach to the same GameObject as BlockBreakGame.
    ///
    /// GRADUATION NOTES
    ///
    /// FlowSort now has TWO scenes: Menu (its own front end) and Main (the game). Inside the hub
    /// only Main should ship — PocketVerse has its own front end, and two menus stacked on each
    /// other is worse than either. Port Main only, and set GameSession.Mode before it loads.
    ///
    /// Because the hub subscribes to OnExitRequested, BlockBreakGame routes exit through the hub
    /// rather than loading its own Menu scene, so nothing in the game needs a hub-specific branch.
    /// Standalone it goes back to its own menu; here it returns to the hub. Both paths are live.
    /// </summary>
    public class FlowSortMiniGame : MonoBehaviour, IMiniGame
    {
        public BlockBreakGame Manager;

        /// <summary>Which mode the hub tile launches. Levels is the progression one.</summary>
        public GameMode Mode = GameMode.Levels;

        MiniGameContext context;

        void Awake()
        {
            if (Manager == null) Manager = GetComponent<BlockBreakGame>();

            // Read by BlockBreakGame.Start, so it has to be set before the game initialises.
            GameSession.Mode = Mode;
        }

        public void Init(MiniGameContext ctx)
        {
            context = ctx;
            Manager.OnExitRequested += HandleExitRequested;
            Manager.OnGameOver += HandleGameOver;
        }

        public void StartGame() { }

        public void PauseGame() => Time.timeScale = 0f;

        public void ResumeGame() => Time.timeScale = 1f;

        /// <summary>
        /// The score the run actually earned. BlockBreakGame owns it now — the old wrapper
        /// derived one from level and coin count, which double-counted a persistent wallet.
        /// </summary>
        public int GetScore() => Manager.Score;

        /// <summary>
        /// Progress is written through PlayerProfile as it happens (level reached, best scores,
        /// coins, hearts), so there is nothing to flush here. PlayerPrefs is saved on each write.
        /// </summary>
        public void SaveProgress() { }

        void HandleExitRequested()
        {
            Time.timeScale = 1f;
            context.ReturnToHub();
        }

        void HandleGameOver(int score) => context.ReportGameOver(score);

        void OnDestroy()
        {
            if (Manager == null) return;
            Manager.OnExitRequested -= HandleExitRequested;
            Manager.OnGameOver -= HandleGameOver;
        }
    }
}

using UnityEngine;

namespace Miniverse.Hub
{
    /// <summary>
    /// Lifecycle every minigame implements so the hub can host it without knowing anything
    /// about its internals. One component in the minigame's scene implements this and is
    /// what HubLauncher looks up after an additive scene load.
    /// </summary>
    public interface IMiniGame
    {
        /// <summary>
        /// Called once, right after the minigame's scene finishes loading additively, before
        /// the player can interact with anything. Wire up cross-game state here (e.g. read
        /// PlayerProgress) rather than in Awake/Start, since those run at scene-load time and
        /// can't be sequenced against the hub.
        /// </summary>
        void Init(MiniGameContext context);

        void StartGame();

        void PauseGame();

        void ResumeGame();

        /// <summary>
        /// The minigame's own signal that a run is over. It should call
        /// MiniGameContext.ReportGameOver from here rather than the hub polling for it.
        /// </summary>
        int GetScore();

        void SaveProgress();
    }

    /// <summary>
    /// Handed to a minigame in Init so it can call back into the hub without a direct
    /// reference to HubLauncher — keeps the dependency one-directional (hub knows about
    /// games, games don't need to know about the hub's launcher type).
    /// </summary>
    public class MiniGameContext
    {
        public string GameId { get; }

        public MiniGameContext(string gameId)
        {
            GameId = gameId;
        }

        public void ReportGameOver(int finalScore)
        {
            HubLauncher.Instance.OnMiniGameOver(finalScore);
        }

        public void ReturnToHub()
        {
            HubLauncher.Instance.ReturnToHub();
        }
    }
}

using Miniverse.Hub;
using FlowSort.Gameplay;
using UnityEngine;

namespace FlowSort.Hub
{
    // Lives outside Assets/ in the FlowSort project (Miniverse.Hub/IMiniGame don't exist here,
    // so this can't compile standalone) — drop into PocketVerse/Assets/Games/<Name>/Scripts/
    // at graduation and attach to the same GameObject as RevealGameManager.
    public class FlowSortMiniGame : MonoBehaviour, IMiniGame
    {
        public RevealGameManager Manager;
        MiniGameContext context;

        void Awake()
        {
            if (Manager == null) Manager = GetComponent<RevealGameManager>();
        }

        public void Init(MiniGameContext ctx)
        {
            context = ctx;
            Manager.OnExitRequested += HandleExitRequested;
        }

        public void StartGame() { }

        public void PauseGame() => Time.timeScale = 0f;

        public void ResumeGame() => Time.timeScale = 1f;

        // No fail state yet — score is keys collected plus a per-level bonus, good enough
        // until a real scoring design exists.
        public int GetScore() => (Manager.Level - 1) * 100 + Manager.Wallet.Keys;

        public void SaveProgress() { }

        void HandleExitRequested() => context.ReturnToHub();

        void OnDestroy()
        {
            if (Manager != null) Manager.OnExitRequested -= HandleExitRequested;
        }
    }
}

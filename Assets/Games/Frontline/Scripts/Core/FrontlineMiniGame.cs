using UnityEngine;
using UnityEngine.InputSystem;
using Miniverse.Hub;

namespace Frontline
{
    /// <summary>
    /// The IMiniGame adapter Miniverse's HubLauncher finds after loading Frontline's scene
    /// additively over Home. This is the only hub-aware file in the whole game -- Frontline
    /// keeps owning its full internal lifecycle (its own Main Menu/Shop/Upgrade/Ranks tab bar,
    /// Pause, Death screen, SaveData/Supply economy) exactly as it does standalone, so grafting
    /// a future Frontline update back in is still just "copy the folder over," not a rewrite.
    /// Added once, at graduation time (2026-07-27) -- see Frontline's
    /// frontline-miniverse-integration memory and Miniverse's HANDOFF.md for the plan this
    /// implements.
    /// </summary>
    public class FrontlineMiniGame : MonoBehaviour, IMiniGame
    {
        MiniGameContext _context;
        bool _reportedGameOver;

        public void Init(MiniGameContext context)
        {
            _context = context;

            // GameManager.Restart() normally does SceneManager.LoadScene(buildIndex) in Single
            // mode, which would unload Home too -- Frontline runs as an *additive* scene under
            // the hub, not the only scene in the game anymore. Routing RESTART back through the
            // hub (one extra tap: player lands on Frontline's own Main Menu, not literally
            // Home) is the safe option until HubLauncher grows a reload-this-game-in-place call;
            // an in-place additive unload/reload here risks silently breaking Home instead.
            GameManager.RestartOverride = () => _context.ReturnToHub();
            GameManager.ExitToHubOverride = RequestExit;
        }

        // Frontline shows its own Main Menu first (PLAY/CONTINUE, Shop/Upgrade/Ranks tabs)
        // rather than dropping straight into a run -- deliberate for this first graduation,
        // see the coordination message to the Miniverse session for the reasoning.
        public void StartGame() => _reportedGameOver = false;

        public void PauseGame()
        {
            if (GameUI.Instance != null && GameUI.Instance.Screen == Screen_.Playing)
                GameUI.Instance.Go(Screen_.Paused);
        }

        public void ResumeGame()
        {
            if (GameUI.Instance != null && GameUI.Instance.Screen == Screen_.Paused)
                GameUI.Instance.Go(Screen_.Playing);
        }

        public int GetScore() => GameManager.Instance != null ? GameManager.Instance.Score : 0;

        public void SaveProgress() => SaveData.I.Save();

        /// <summary>
        /// The one place that actually leaves Frontline for the hub -- reports the current score
        /// (so analytics/save data see a real session end) and guards against firing twice.
        /// Reachable two ways: the hardware/gesture Back button (mapped to Keyboard.escapeKey in
        /// the new Input System) while on Frontline's own Main Menu, and GameUI's explicit
        /// "back to library" button on any tabbed screen (Menu/Shop/Upgrades/Ranks) via
        /// GameManager.ExitToHubOverride.
        /// </summary>
        void RequestExit()
        {
            if (_reportedGameOver) return;
            _reportedGameOver = true;
            _context.ReportGameOver(GetScore());
        }

        void Update()
        {
            if (_reportedGameOver) return;

            bool backPressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            if (backPressed && GameUI.Instance != null && GameUI.Instance.Screen == Screen_.Menu)
                RequestExit();
        }
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;
using Miniverse.Hub.Analytics;

namespace Miniverse.Hub
{
    /// <summary>
    /// Lives on a persistent object in the Home scene (never unloaded). Owns loading a
    /// minigame's scene additively on top of Home, finding its IMiniGame, and tearing it
    /// back down when the player exits — Home itself never gets unloaded, so hub UI state
    /// (currency, selection) survives a play session without extra save/restore plumbing.
    /// </summary>
    public class HubLauncher : MonoBehaviour
    {
        public static HubLauncher Instance { get; private set; }

        [SerializeField] GameObject _homeUIRoot;

        Scene _activeGameScene;
        IMiniGame _activeGame;
        string _activeGameId;
        float _gameStartTime;
        int _pendingScore;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void LaunchGame(MiniGameDef def)
        {
            if (_activeGame != null)
            {
                Debug.LogWarning($"[Hub] LaunchGame({def.gameId}) ignored — {_activeGameId} is already active.");
                return;
            }

            _activeGameId = def.gameId;
            SceneManager.LoadScene(def.sceneName, LoadSceneMode.Additive);
            SceneManager.sceneLoaded += OnGameSceneLoaded;
        }

        void OnGameSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Additive) return;
            SceneManager.sceneLoaded -= OnGameSceneLoaded;

            _activeGameScene = scene;
            _activeGame = FindMiniGameIn(scene);
            if (_activeGame == null)
            {
                Debug.LogError($"[Hub] Scene '{scene.name}' has no component implementing IMiniGame — cannot launch.");
                UnloadActiveGameScene();
                return;
            }

            // Additive loading means Home's own Canvas is still in the hierarchy and still
            // rendering — without this, Home's title/grid visibly bleed through on top of (or
            // interleaved with) the minigame's own UI and 3D scene, sort order between the two
            // Canvases being otherwise undefined. Caught on-device during Frontline's
            // graduation: PLAY worked, but "PocketVerse" and the empty tile box were still
            // floating over live gameplay.
            if (_homeUIRoot != null) _homeUIRoot.SetActive(false);

            _activeGame.Init(new MiniGameContext(_activeGameId));
            _activeGame.StartGame();

            _gameStartTime = Time.realtimeSinceStartup;
            _pendingScore = 0;
            AnalyticsService.LogGameLaunch(_activeGameId);
        }

        static IMiniGame FindMiniGameIn(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<IMiniGame>(includeInactive: true);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Called by MiniGameContext.ReportGameOver — the minigame decides when it's over, the hub just records the result.</summary>
        public void OnMiniGameOver(int finalScore)
        {
            Debug.Log($"[Hub] {_activeGameId} finished with score {finalScore}");
            _pendingScore = finalScore;
            _activeGame?.SaveProgress();
            ReturnToHub();
        }

        /// <summary>
        /// Also the exit path when a player backs out mid-game with no score reported (a
        /// menu/quit button, say) — UnloadActiveGameScene logs game_end either way, using
        /// whatever _pendingScore was last set to (0 if OnMiniGameOver never ran), so
        /// session-length analytics don't silently miss abandoned sessions.
        /// </summary>
        public void ReturnToHub()
        {
            if (_activeGame == null) return;
            UnloadActiveGameScene();
        }

        void UnloadActiveGameScene()
        {
            if (_activeGameScene.IsValid())
                SceneManager.UnloadSceneAsync(_activeGameScene);

            if (_activeGame != null)
            {
                float duration = Time.realtimeSinceStartup - _gameStartTime;
                AnalyticsService.LogGameEnd(_activeGameId, duration, _pendingScore);
            }

            if (_homeUIRoot != null) _homeUIRoot.SetActive(true);

            _activeGame = null;
            _activeGameId = null;
        }
    }
}

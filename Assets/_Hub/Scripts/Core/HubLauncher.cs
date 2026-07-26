using UnityEngine;
using UnityEngine.SceneManagement;

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

        Scene _activeGameScene;
        IMiniGame _activeGame;
        string _activeGameId;

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

            _activeGame.Init(new MiniGameContext(_activeGameId));
            _activeGame.StartGame();
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
            _activeGame?.SaveProgress();
            ReturnToHub();
        }

        public void ReturnToHub()
        {
            if (_activeGame == null) return;
            UnloadActiveGameScene();
        }

        void UnloadActiveGameScene()
        {
            if (_activeGameScene.IsValid())
                SceneManager.UnloadSceneAsync(_activeGameScene);
            _activeGame = null;
            _activeGameId = null;
        }
    }
}

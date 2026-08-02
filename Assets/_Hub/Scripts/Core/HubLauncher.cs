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
        [SerializeField] GameObject _homeEventSystem;

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

            // Deactivate Home's own UI *before* the additive load, not after: Unity runs the
            // freshly-loaded scene's Awake() calls synchronously inside LoadScene, before
            // sceneLoaded fires -- so deactivating here only in the sceneLoaded callback left
            // Home's own "Canvas/Shell" active at the exact moment the minigame's own Awake
            // (e.g. Frontline's GameUI.WireShell, GameObject.Find("Canvas/Shell")) ran. Both
            // Home and every minigame's own generated scene use "Canvas" as their root Canvas
            // name and Home's own top bar is named "Shell" too (HubCanvasBuilder.BuildShell),
            // so a same-named GameObject.Find while both are active is ambiguous and can bind
            // to Home's Shell instead of the minigame's own -- exactly what broke Frontline's
            // in-game HUD staying visible during Playing after its 2026-08-02 re-sync (its
            // _shell field pointed at Home's Shell, so RefreshCanvasVisibility was toggling the
            // wrong object). Deactivating first means GameObject.Find only ever sees one match.
            if (_homeUIRoot != null) _homeUIRoot.SetActive(false);

            // Home's EventSystem is a separate root object, not a child of _homeUIRoot, so
            // hiding the Canvas above doesn't touch it — it stays enabled and fights the
            // minigame's own additively-loaded EventSystem for UI input. Found on-device during
            // FlowSort's graduation: the grid taps (raw Pointer + Physics2D, no EventSystem
            // involved) worked fine, but the real uGUI exit Button never fired at all with two
            // EventSystems live at once.
            if (_homeEventSystem != null) _homeEventSystem.SetActive(false);

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

            _gameStartTime = Time.realtimeSinceStartup;
            _pendingScore = 0;
            AnalyticsService.LogGameLaunch(_activeGameId);
            HubStats.IncrementGamesPlayed();
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
            if (_activeGame != null)
            {
                float duration = Time.realtimeSinceStartup - _gameStartTime;
                AnalyticsService.LogGameEnd(_activeGameId, duration, _pendingScore);
            }

            _activeGame = null;
            _activeGameId = null;

            // Home only reactivates once the old scene has actually finished unloading, not the
            // instant UnloadSceneAsync is *requested* -- reactivating immediately left Home's
            // Canvas active while the outgoing minigame's own GameObjects were still mid-teardown
            // for however many frames that takes, which is exactly the several-seconds-long
            // overlap (minigame's last frame + Home's tiles both visible) seen on-device after
            // exiting FlowSort. Waiting for .completed removes the window entirely -- worst case
            // is a brief blank frame between the two, not a double-exposure of both.
            if (_activeGameScene.IsValid())
            {
                var op = SceneManager.UnloadSceneAsync(_activeGameScene);
                if (op != null) op.completed += _ => ReactivateHome();
                else ReactivateHome();
            }
            else
            {
                ReactivateHome();
            }
        }

        void ReactivateHome()
        {
            if (_homeUIRoot != null) _homeUIRoot.SetActive(true);
            if (_homeEventSystem != null) _homeEventSystem.SetActive(true);
        }
    }
}

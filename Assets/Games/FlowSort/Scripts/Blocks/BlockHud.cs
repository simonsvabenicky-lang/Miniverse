using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlowSort.Blocks
{
    /// <summary>
    /// Screen-space HUD. All references are assigned by SceneBuilder as plain public fields and
    /// all button listeners are wired at RUNTIME in Start() — never via editor-time AddListener,
    /// which does not survive scene serialisation (the exit button shipped dead once for exactly
    /// that reason; see HANDOFF.md).
    /// </summary>
    public class BlockHud : MonoBehaviour
    {
        public BlockBreakGame Game;

        public TMP_Text ScoreText;
        public TMP_Text LevelText;
        public TMP_Text BeltText;
        public Button ExitButton;

        [Header("Level banner")]
        public GameObject BannerRoot;
        public CanvasGroup BannerGroup;
        public TMP_Text BannerText;

        public GameObject GameOverPanel;
        public TMP_Text GameOverScoreText;
        public Button RetryButton;
        public Button MenuButton;

        [Header("Pause")]
        public GameObject PausePanel;
        public Button ResumeButton;
        public Button QuitButton;

        float scorePunch;
        Vector3 scoreBaseScale = Vector3.one;

        void Start()
        {
            if (ScoreText != null) scoreBaseScale = ScoreText.transform.localScale;

            if (ExitButton != null) ExitButton.onClick.AddListener(OnBackPressed);
            if (ResumeButton != null) ResumeButton.onClick.AddListener(() => SetPaused(false));
            if (QuitButton != null) QuitButton.onClick.AddListener(OnExitPressed);
            if (RetryButton != null) RetryButton.onClick.AddListener(OnRetryPressed);
            // The game-over panel covers the screen and eats the exit button's taps, so it needs
            // its own way back to the menu or the only route out is retrying until hearts run dry.
            if (MenuButton != null) MenuButton.onClick.AddListener(OnExitPressed);

            // Only if nothing has asked for it yet. Start() order between this and BlockBreakGame
            // is undefined, and the game shows the first level's banner from its own Start —
            // hiding unconditionally here swallowed it every time.
            if (bannerAge < 0f && BannerRoot != null) BannerRoot.SetActive(false);
            SetPaused(false);
            HideGameOver();
        }

        /// <summary>
        /// BACK pauses rather than leaving outright. Dropping a half-played level on one tap is
        /// the wrong default anywhere, and more so inside a hub where the button sits next to
        /// everything else you might reach for.
        /// </summary>
        void OnBackPressed()
        {
            if (PausePanel == null)
            {
                OnExitPressed();
                return;
            }

            SetPaused(true);
        }

        public void SetPaused(bool paused)
        {
            if (PausePanel != null) PausePanel.SetActive(paused);
            Time.timeScale = paused ? 0f : 1f;
        }

        void OnExitPressed()
        {
            Time.timeScale = 1f;
            if (Game != null) Game.RequestExit();
        }

        void OnRetryPressed()
        {
            Time.timeScale = 1f;
            if (Game != null) Game.Restart();
        }

        public void SetScore(int score)
        {
            if (ScoreText == null) return;
            ScoreText.text = score.ToString();
            scorePunch = 0.15f;
        }

        public void SetLevel(int level)
        {
            if (LevelText != null) LevelText.text = $"LEVEL {level}";
        }

        /// <summary>
        /// Towers on the belt against the cap. Without this on screen a refused tap looks like a
        /// broken button rather than a rule.
        ///
        /// Bare numbers, no caption: the landing squares are drawn on screen and you can see how
        /// full they are, so the only count that needs saying is the one with no visual.
        /// </summary>
        public void SetBelt(int riding, int capacity)
        {
            if (BeltText == null) return;
            BeltText.text = $"{riding}/{capacity}";
            BeltText.color = riding >= capacity ? BlockPalette.Get(1) : BlockPalette.TextInk;
        }

        /// <summary>
        /// A card that punches in, holds, and fades. Clearing a board used to roll straight into
        /// the next one with nothing to mark it, so the win never landed.
        /// </summary>
        public void ShowBanner(string text, float hold = 0.75f)
        {
            if (BannerRoot == null || BannerText == null) return;

            BannerText.text = text;
            BannerRoot.SetActive(true);
            bannerAge = 0f;
            bannerHold = hold;
        }

        float bannerAge = -1f;
        float bannerHold;

        void UpdateBanner()
        {
            if (bannerAge < 0f || BannerRoot == null) return;

            bannerAge += Time.deltaTime;

            const float rise = 0.18f;
            const float fade = 0.3f;

            float scale;
            float alpha;

            if (bannerAge < rise)
            {
                float t = bannerAge / rise;
                scale = Mathf.LerpUnclamped(0.6f, 1f, 1f - (1f - t) * (1f - t) * (1f - t));
                alpha = t;
            }
            else if (bannerAge < rise + bannerHold)
            {
                scale = 1f;
                alpha = 1f;
            }
            else
            {
                float t = Mathf.Clamp01((bannerAge - rise - bannerHold) / fade);
                scale = 1f + t * 0.12f;
                alpha = 1f - t;

                if (t >= 1f)
                {
                    BannerRoot.SetActive(false);
                    bannerAge = -1f;
                    return;
                }
            }

            BannerRoot.transform.localScale = Vector3.one * scale;
            if (BannerGroup != null) BannerGroup.alpha = alpha;
        }

        public void ShowGameOver(int score)
        {
            if (GameOverScoreText != null) GameOverScoreText.text = score.ToString();
            if (GameOverPanel != null) GameOverPanel.SetActive(true);
        }

        public void HideGameOver()
        {
            if (GameOverPanel != null) GameOverPanel.SetActive(false);
        }

        void Update()
        {
            UpdateBanner();

            if (scorePunch <= 0f || ScoreText == null) return;

            scorePunch -= Time.deltaTime;
            float t = Mathf.Clamp01(scorePunch / 0.15f);
            ScoreText.transform.localScale = scoreBaseScale * (1f + 0.15f * t);
        }
    }
}

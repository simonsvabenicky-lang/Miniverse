using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FlowSort.Meta
{
    /// <summary>
    /// The home screen: hearts and coins along the top, three bottom tabs, and the three modes to
    /// play. Built by Editor/MenuBuilder.cs — this only carries references and behaviour.
    ///
    /// Every button listener is wired at RUNTIME in Start(), never via editor-time AddListener,
    /// which does not survive scene serialisation. That is what shipped the game's exit button
    /// dead once already; see HANDOFF.md.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [Header("Top bar")]
        public TMP_Text HeartCountText;
        public TMP_Text HeartTimerText;
        public TMP_Text CoinText;

        [Header("Tabs")]
        public Button[] TabButtons = Array.Empty<Button>();
        public Image[] TabImages = Array.Empty<Image>();
        public GameObject[] TabPanels = Array.Empty<GameObject>();
        public Sprite TabOnSprite;
        public Sprite TabOffSprite;

        [Header("Play")]
        public Button EndlessButton;
        public Button DailyButton;
        public Button LevelsButton;
        public TMP_Text PlayNoticeText;

        [Header("Shop")]
        public Button BuyHeartButton;
        public Button BuyRefillButton;
        public Button BuySlotButton;
        public TMP_Text SlotCaptionText;
        public TMP_Text ShopNoticeText;

        [Header("Stats")]
        public TMP_Text StatsText;

        [Header("Settings")]
        public Button SettingsButton;
        public Button SettingsCloseButton;
        public Button SoundButton;
        public GameObject SettingsPanel;
        public Image SoundIcon;
        public Sprite AudioOnSprite;
        public Sprite AudioOffSprite;

        public const int HeartCost = 40;
        public const int RefillCost = 150;

        float noticeTimer;
        float refreshTimer;

        void Start()
        {
            PlayerProfile.ApplySound();

            for (int i = 0; i < TabButtons.Length; i++)
            {
                int index = i;
                if (TabButtons[i] != null) TabButtons[i].onClick.AddListener(() => SelectTab(index));
            }

            if (EndlessButton != null) EndlessButton.onClick.AddListener(() => Launch(GameMode.Endless));
            if (DailyButton != null) DailyButton.onClick.AddListener(() => Launch(GameMode.Daily));
            if (LevelsButton != null) LevelsButton.onClick.AddListener(() => Launch(GameMode.Levels));

            if (BuyHeartButton != null) BuyHeartButton.onClick.AddListener(BuyHeart);
            if (BuyRefillButton != null) BuyRefillButton.onClick.AddListener(BuyRefill);
            if (BuySlotButton != null) BuySlotButton.onClick.AddListener(BuySlot);

            if (SettingsButton != null) SettingsButton.onClick.AddListener(() => ShowSettings(true));
            if (SettingsCloseButton != null) SettingsCloseButton.onClick.AddListener(() => ShowSettings(false));
            if (SoundButton != null) SoundButton.onClick.AddListener(ToggleSound);

            ShowSettings(false);
            SelectTab(1);
            Refresh();
        }

        void Update()
        {
            // The heart timer is the only thing on screen that changes on its own.
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = 0.5f;
                RefreshHearts();
            }

            if (noticeTimer <= 0f) return;

            noticeTimer -= Time.unscaledDeltaTime;
            if (noticeTimer > 0f) return;

            SetNotice(PlayNoticeText, "");
            SetNotice(ShopNoticeText, "");
        }

        // --- Tabs ---

        void SelectTab(int index)
        {
            Click();

            for (int i = 0; i < TabPanels.Length; i++)
                if (TabPanels[i] != null) TabPanels[i].SetActive(i == index);

            for (int i = 0; i < TabImages.Length; i++)
                if (TabImages[i] != null)
                    TabImages[i].sprite = i == index ? TabOnSprite : TabOffSprite;

            if (index == 2) RefreshStats();
        }

        // --- Play ---

        void Launch(GameMode mode)
        {
            if (!PlayerProfile.TrySpendHeart())
            {
                Deny();
                Notice(PlayNoticeText, "OUT OF HEARTS - WAIT OR BUY ONE");
                return;
            }

            var sfx = Sfx.Instance;
            sfx?.Play(sfx.Confirm, 0.6f);

            GameSession.Mode = mode;
            SceneManager.LoadScene(GameSession.GameScene);
        }

        static void Click()
        {
            var sfx = Sfx.Instance;
            sfx?.Play(sfx.Click, 0.5f);
        }

        static void Deny()
        {
            var sfx = Sfx.Instance;
            sfx?.Play(sfx.Deny, 0.5f);
        }

        // --- Shop ---

        void BuyHeart() => Buy(HeartCost, 1, "+1 HEART");

        void BuyRefill() => Buy(RefillCost, PlayerProfile.MaxHearts, "HEARTS REFILLED");

        /// <summary>
        /// The one upgrade that changes how the game plays: a landing square is throughput, so
        /// every extra one is more of your hand you can actually get through.
        /// </summary>
        void BuySlot()
        {
            if (PlayerProfile.LandingSlots >= PlayerProfile.MaxLandingSlots)
            {
                Deny();
                Notice(ShopNoticeText, "ALL SQUARES UNLOCKED");
                return;
            }

            if (!PlayerProfile.TryBuyLandingSlot())
            {
                Deny();
                Notice(ShopNoticeText, "NOT ENOUGH COINS");
                return;
            }

            var sfx = Sfx.Instance;
            sfx?.Play(sfx.Confirm, 0.6f);

            Notice(ShopNoticeText, $"{PlayerProfile.LandingSlots} SQUARES");
            Refresh();
        }

        void Buy(int cost, int hearts, string success)
        {
            if (PlayerProfile.Hearts >= PlayerProfile.MaxHearts)
            {
                Deny();
                Notice(ShopNoticeText, "HEARTS ALREADY FULL");
                return;
            }

            if (!PlayerProfile.TrySpendCoins(cost))
            {
                Deny();
                Notice(ShopNoticeText, "NOT ENOUGH COINS");
                return;
            }

            PlayerProfile.AddHearts(hearts);

            var sfx = Sfx.Instance;
            sfx?.Play(sfx.Heart, 0.6f);

            Notice(ShopNoticeText, success);
            Refresh();
        }

        // --- Settings ---

        void ShowSettings(bool show)
        {
            bool wasOpen = SettingsPanel != null && SettingsPanel.activeSelf;
            if (SettingsPanel != null) SettingsPanel.SetActive(show);
            RefreshSoundIcon();

            // Silent on the initial hide during Start; only a real open or close makes a noise.
            if (show == wasOpen) return;

            var sfx = Sfx.Instance;
            sfx?.Play(sfx.Popup, 0.5f);
        }

        void ToggleSound()
        {
            PlayerProfile.SoundOn = !PlayerProfile.SoundOn;
            RefreshSoundIcon();

            // Played after the toggle, so turning sound ON is audible and turning it off is not.
            Click();
        }

        void RefreshSoundIcon()
        {
            if (SoundIcon == null) return;
            SoundIcon.sprite = PlayerProfile.SoundOn ? AudioOnSprite : AudioOffSprite;
        }

        // --- Display ---

        void Refresh()
        {
            RefreshHearts();
            RefreshStats();
            if (CoinText != null) CoinText.text = PlayerProfile.Coins.ToString();

            if (SlotCaptionText == null) return;

            SlotCaptionText.text = PlayerProfile.LandingSlots >= PlayerProfile.MaxLandingSlots
                ? $"{PlayerProfile.LandingSlots} squares - all unlocked"
                : $"{PlayerProfile.NextSlotCost} coins - you have {PlayerProfile.LandingSlots}";
        }

        void RefreshHearts()
        {
            int hearts = PlayerProfile.Hearts;
            if (HeartCountText != null) HeartCountText.text = hearts.ToString();
            if (CoinText != null) CoinText.text = PlayerProfile.Coins.ToString();

            if (HeartTimerText == null) return;

            if (hearts >= PlayerProfile.MaxHearts)
            {
                HeartTimerText.text = "FULL";
                return;
            }

            var left = PlayerProfile.TimeToNextHeart;
            HeartTimerText.text = $"{(int)left.TotalMinutes:00}:{left.Seconds:00}";
        }

        void RefreshStats()
        {
            if (StatsText == null) return;

            StatsText.text =
                $"BEST LEVEL\n<size=64>{PlayerProfile.BestLevel}</size>\n\n" +
                $"BEST ENDLESS\n<size=64>{PlayerProfile.BestScore(GameMode.Endless)}</size>\n\n" +
                $"BEST DAILY\n<size=64>{PlayerProfile.BestScore(GameMode.Daily)}</size>";
        }

        void Notice(TMP_Text target, string message)
        {
            SetNotice(target, message);
            noticeTimer = 2.2f;
        }

        static void SetNotice(TMP_Text target, string message)
        {
            if (target != null) target.text = message;
        }
    }
}

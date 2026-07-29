using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Miniverse.Hub
{
    /// <summary>
    /// Home's persistent chrome (top bar + bottom tab bar) and the three overlay panels it opens
    /// -- Settings, Store, Profile. Same "structure from HubCanvasBuilder, behaviour wired here at
    /// runtime by finding children by name" split Frontline's GameUI uses, for the same reason: a
    /// Button's onClick listener is a C# delegate and delegates don't survive
    /// EditorSceneManager.SaveScene.
    /// </summary>
    public class HomeShellController : MonoBehaviour
    {
        [SerializeField] Sprite _soundOnIcon;
        [SerializeField] Sprite _soundOffIcon;

        GameObject _gameGridCanvas;
        GameObject _settingsPanel;
        GameObject _storePanel;
        GameObject _profilePanel;

        TextMeshProUGUI _livesValue;
        TextMeshProUGUI _cashValue;
        Image _soundIcon;
        TextMeshProUGUI _profileStatsText;

        void Awake()
        {
            GameObject shell = Find("Shell");
            _gameGridCanvas = Find("GameGridCanvas");
            _settingsPanel = Find("SettingsPanel");
            _storePanel = Find("StorePanel");
            _profilePanel = Find("ProfilePanel");

            if (shell != null)
            {
                HubUIWire.Click(shell, "TopBar/ProfileButton", () => ShowOnly(_profilePanel));
                HubUIWire.Click(shell, "TopBar/SettingsGearButton", () => ShowOnly(_settingsPanel));
                HubUIWire.Click(shell, "TopBar/SoundToggleButton", ToggleSound);
                HubUIWire.Click(shell, "BottomTabBar/HomeTab", ShowHome);
                HubUIWire.Click(shell, "BottomTabBar/StoreTab", () => ShowOnly(_storePanel));

                _livesValue = shell.transform.Find("TopBar/LivesPill/Value")?.GetComponent<TextMeshProUGUI>();
                _cashValue = shell.transform.Find("TopBar/CashPill/Value")?.GetComponent<TextMeshProUGUI>();
                _soundIcon = shell.transform.Find("TopBar/SoundToggleButton/Icon")?.GetComponent<Image>();
            }

            if (_settingsPanel != null) HubUIWire.Click(_settingsPanel, "BackButton", ShowHome);
            if (_storePanel != null) HubUIWire.Click(_storePanel, "BackButton", ShowHome);
            if (_profilePanel != null)
            {
                HubUIWire.Click(_profilePanel, "BackButton", ShowHome);
                _profileStatsText = _profilePanel.transform.Find("GamesPlayedText")?.GetComponent<TextMeshProUGUI>();
            }

            HubAudio.Apply();
            RefreshSoundIcon();
            RefreshCounters();
            HubEconomy.Changed += RefreshCounters;

            // HubCanvasBuilder leaves Settings/Store/Profile active in the saved scene on
            // purpose -- GameObject.Find can't locate an inactive object, even mid-path, so the
            // Find() calls above would fail for all three if they started hidden. This is what
            // actually hides them, now that they've been found -- same order of operations as
            // Frontline's GameUI (find everything active, then RefreshCanvasVisibility hides the
            // non-current screens at the end of Awake).
            ShowHome();
        }

        void OnDestroy() => HubEconomy.Changed -= RefreshCounters;

        void ShowHome() => ShowOnly(_gameGridCanvas);

        void ShowOnly(GameObject target)
        {
            SetActive(_gameGridCanvas, target == _gameGridCanvas);
            SetActive(_settingsPanel, target == _settingsPanel);
            SetActive(_storePanel, target == _storePanel);
            SetActive(_profilePanel, target == _profilePanel);
            if (target == _profilePanel && _profileStatsText != null)
                _profileStatsText.text = $"Games played: {HubStats.GamesPlayed}";
        }

        void ToggleSound()
        {
            HubAudio.Muted = !HubAudio.Muted;
            RefreshSoundIcon();
        }

        void RefreshSoundIcon()
        {
            if (_soundIcon != null) _soundIcon.sprite = HubAudio.Muted ? _soundOffIcon : _soundOnIcon;
        }

        void RefreshCounters()
        {
            if (_livesValue != null) _livesValue.text = HubEconomy.Lives.ToString();
            if (_cashValue != null) _cashValue.text = HubEconomy.Cash.ToString();
        }

        static void SetActive(GameObject go, bool active) { if (go != null) go.SetActive(active); }

        static GameObject Find(string name)
        {
            GameObject go = GameObject.Find($"Canvas/{name}");
            if (go == null) Debug.LogWarning($"[Miniverse] {name} not found -- was HubCanvasBuilder run?");
            return go;
        }
    }
}

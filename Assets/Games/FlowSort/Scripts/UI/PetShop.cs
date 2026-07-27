using FlowSort.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlowSort.UI
{
    /// <summary>
    /// Bottom shelf of six collectible critters, one per PieceColor, unlocked by spending keys.
    /// Unlock state persists across sessions via PlayerPrefs.
    /// </summary>
    public class PetShop : MonoBehaviour
    {
        public CurrencyWallet Wallet;
        public Image[] PetIcons = new Image[6];
        public TMP_Text[] CostLabels = new TMP_Text[6];
        public Button[] Buttons = new Button[6];
        public GameObject[] LockOverlays = new GameObject[6];

        static readonly int[] Costs = { 20, 30, 45, 60, 80, 100 };
        readonly bool[] unlocked = new bool[6];

        void Start()
        {
            for (int i = 0; i < 6; i++)
            {
                unlocked[i] = PlayerPrefs.GetInt(PrefKey(i), 0) == 1;
                PetIcons[i].sprite = ArtRegistry.Instance.Block((PieceColor)i);
                RefreshSlot(i);

                int captured = i;
                Buttons[i].onClick.AddListener(() => TryUnlock(captured));
            }
        }

        void TryUnlock(int i)
        {
            if (unlocked[i]) return;
            if (!Wallet.TrySpend(Costs[i])) return;

            unlocked[i] = true;
            PlayerPrefs.SetInt(PrefKey(i), 1);
            RefreshSlot(i);
        }

        void RefreshSlot(int i)
        {
            CostLabels[i].text = unlocked[i] ? "Owned" : Costs[i].ToString();
            LockOverlays[i].SetActive(!unlocked[i]);
        }

        static string PrefKey(int i) => $"flowsort_pet_{i}";
    }
}

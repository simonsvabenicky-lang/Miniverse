using FlowSort.Gameplay;
using TMPro;
using UnityEngine;

namespace FlowSort.UI
{
    public class RevealHud : MonoBehaviour
    {
        public CurrencyWallet Wallet;
        public TMP_Text LevelText;
        public TMP_Text KeysText;

        void Start()
        {
            Wallet.OnChanged += HandleKeysChanged;
            HandleKeysChanged(Wallet.Keys);
        }

        public void SetLevel(int level) => LevelText.text = $"Level {level}";

        void HandleKeysChanged(int keys) => KeysText.text = keys.ToString();
    }
}

using System;
using FlowSort.Meta;
using UnityEngine;

namespace FlowSort.Gameplay
{
    /// <summary>
    /// Scene-side view of the player's coins.
    ///
    /// It owns no storage of its own — PlayerProfile does, so the menu's coin counter and the
    /// game's payouts are the same number rather than two that drift apart. This survives as a
    /// component because the PocketVerse hub wrapper reads Manager.Wallet.Keys to report a score.
    /// </summary>
    public class CurrencyWallet : MonoBehaviour
    {
        public int Keys => PlayerProfile.Coins;

        public event Action<int> OnChanged;

        public void Add(int amount)
        {
            PlayerProfile.AddCoins(amount);
            OnChanged?.Invoke(Keys);
        }

        public bool TrySpend(int amount)
        {
            if (!PlayerProfile.TrySpendCoins(amount)) return false;
            OnChanged?.Invoke(Keys);
            return true;
        }
    }
}

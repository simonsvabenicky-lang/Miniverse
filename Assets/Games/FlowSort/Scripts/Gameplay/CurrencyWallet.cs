using System;
using UnityEngine;

namespace FlowSort.Gameplay
{
    public class CurrencyWallet : MonoBehaviour
    {
        const string PrefsKey = "flowsort_keys";

        public int Keys { get; private set; }
        public event Action<int> OnChanged;

        void Awake()
        {
            Keys = PlayerPrefs.GetInt(PrefsKey, 0);
        }

        public void Add(int amount)
        {
            Keys += amount;
            PlayerPrefs.SetInt(PrefsKey, Keys);
            OnChanged?.Invoke(Keys);
        }

        public bool TrySpend(int amount)
        {
            if (Keys < amount) return false;
            Keys -= amount;
            PlayerPrefs.SetInt(PrefsKey, Keys);
            OnChanged?.Invoke(Keys);
            return true;
        }
    }
}

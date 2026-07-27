using System.Collections.Generic;
using UnityEngine;

namespace Frontline
{
    /// <summary>
    /// Everything that survives between runs, as one JSON blob in PlayerPrefs -- currency
    /// (Supply), weapon/hero unlocks and levels, the boss checkpoint. One key rather than a
    /// separate ad-hoc PlayerPrefs entry per feature (the way the high-score list already works
    /// in GameManager), because this is about to back three separate systems at once, not one.
    /// </summary>
    [System.Serializable]
    public class SaveData
    {
        public int Supply;
        public List<string> UnlockedWeapons = new List<string>();
        public List<int> WeaponLevels = new List<int>();
        public List<string> UnlockedHeroes = new List<string>();
        public List<int> HeroLevels = new List<int>();
        public string EquippedHero = "Soldier";
        public int HighestCheckpointStage;

        const string Key = "Frontline.Save";
        static SaveData _instance;

        public static SaveData I => _instance ??= Load();

        static SaveData Load()
        {
            string raw = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(raw)) return NewSave();
            return JsonUtility.FromJson<SaveData>(raw) ?? NewSave();
        }

        /// <summary>A brand new install already owns its starting gun and soldier -- an empty
        /// unlock list would mean a fresh player can't even fire a shot.</summary>
        static SaveData NewSave()
        {
            var data = new SaveData();
            data.UnlockedWeapons.Add(Weapons.Starting.Mesh);
            data.WeaponLevels.Add(0);
            data.UnlockedHeroes.Add("Soldier");
            data.HeroLevels.Add(0);
            return data;
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public void AddSupply(int amount)
        {
            Supply += amount;
            Save();
        }

        public bool IsWeaponUnlocked(string mesh) => UnlockedWeapons.Contains(mesh);

        public int WeaponLevel(string mesh)
        {
            int i = UnlockedWeapons.IndexOf(mesh);
            return i >= 0 ? WeaponLevels[i] : 0;
        }

        public bool UnlockWeapon(string mesh, int cost)
        {
            if (IsWeaponUnlocked(mesh) || Supply < cost) return false;
            Supply -= cost;
            UnlockedWeapons.Add(mesh);
            WeaponLevels.Add(0);
            Save();
            return true;
        }

        public bool UpgradeWeapon(string mesh, int cost, int maxLevel)
        {
            int i = UnlockedWeapons.IndexOf(mesh);
            if (i < 0 || WeaponLevels[i] >= maxLevel || Supply < cost) return false;
            Supply -= cost;
            WeaponLevels[i]++;
            Save();
            return true;
        }

        public bool IsHeroUnlocked(string id) => UnlockedHeroes.Contains(id);

        public int HeroLevel(string id)
        {
            int i = UnlockedHeroes.IndexOf(id);
            return i >= 0 ? HeroLevels[i] : 0;
        }

        public bool UnlockHero(string id, int cost)
        {
            if (IsHeroUnlocked(id) || Supply < cost) return false;
            Supply -= cost;
            UnlockedHeroes.Add(id);
            HeroLevels.Add(0);
            Save();
            return true;
        }

        /// <summary>No-op (but still true) if already equipped -- callers can fire this on every
        /// tap of a hero row without worrying about redundant saves.</summary>
        public bool EquipHero(string id)
        {
            if (!IsHeroUnlocked(id)) return false;
            if (EquippedHero != id) { EquippedHero = id; Save(); }
            return true;
        }
    }
}

namespace Frontline
{
    /// <summary>
    /// One playable hero. Mirrors WeaponDef's shape -- a trade-off, not a strict upgrade, same
    /// philosophy as the weapon table: a hero worth unlocking has to cost you something, or the
    /// choice isn't real.
    /// </summary>
    public class HeroDef
    {
        /// <summary>Matches SaveData.EquippedHero / UnlockedHeroes entries.</summary>
        public readonly string Id;
        /// <summary>ArtImporter's prefab name -- "Player" for the starting hero, "Player_X" for
        /// everyone else (see ArtImporter.BuildCharacter).</summary>
        public readonly string PrefabName;
        public readonly string DisplayName;
        public readonly string Trait;
        public readonly int UnlockCost;

        /// <summary>Flat bonus to Tuning.PlayerStartHealth.</summary>
        public readonly int ExtraLives;
        /// <summary>Multiplies Tuning.PlayerMoveSpeed -- the cost side of ExtraLives: tankier
        /// but harder to dodge with.</summary>
        public readonly float MoveSpeedMult;

        public HeroDef(string id, string prefabName, string displayName, string trait,
                       int unlockCost, int extraLives = 0, float moveSpeedMult = 1f)
        {
            Id = id;
            PrefabName = prefabName;
            DisplayName = displayName;
            Trait = trait;
            UnlockCost = unlockCost;
            ExtraLives = extraLives;
            MoveSpeedMult = moveSpeedMult;
        }
    }

    public static class Heroes
    {
        public static readonly HeroDef Soldier = new("Soldier", "Player", "SOLDIER", "BALANCED", 0);

        // +1 life is a real cushion against an early mistake; -15% move speed is a real cost
        // every single dodge for the whole run -- felt on nearly every gate row, not just once.
        public static readonly HeroDef Hazmat = new("Hazmat", "Player_Hazmat", "HAZMAT", "TANKY, SLOW",
                                                     300, extraLives: 1, moveSpeedMult: 0.85f);

        public static readonly HeroDef[] All = { Soldier, Hazmat };
        public static readonly HeroDef Starting = Soldier;

        public static HeroDef ById(string id)
        {
            foreach (HeroDef h in All)
                if (h.Id == id) return h;
            return Starting;
        }

        /// <summary>Whichever hero SaveData currently has equipped, falling back to Soldier if
        /// the save somehow names one that doesn't exist (a stale save after a hero was
        /// removed, say) -- never returns null.</summary>
        public static HeroDef Equipped() => ById(SaveData.I.EquippedHero);
    }
}

namespace Frontline
{
    /// <summary>
    /// One weapon. Mesh must match a child object name in the character FBX -- the whole rack
    /// is already parented to the soldier's hand, so equipping is SetActive, not attachment.
    /// </summary>
    public class WeaponDef
    {
        public readonly string Mesh;
        public readonly string DisplayName;
        public readonly float FireInterval;
        public readonly float Damage;
        public readonly int Pellets;
        public readonly float SpreadDegrees;   // total cone, split across pellets

        /// <summary>
        /// Drives the gate's colour. Stated explicitly rather than derived from DPS: by raw DPS
        /// the Shotgun (80) scores below the AK (83) and would be painted as a downgrade, when
        /// it's a sidegrade that buys crowd coverage. The player reads this colour at a glance
        /// while dodging, so it has to be honest about intent, not arithmetic.
        /// </summary>
        public readonly bool Downgrade;

        /// <summary>
        /// Extra enemies a bullet passes through after the first.
        ///
        /// This is what makes the Sniper exist. At 75 damage against 25 HP enemies it was
        /// throwing away two thirds of every shot into a corpse -- a strictly worse AK, which
        /// is exactly how it played ("sniper is garbage i never pick it"). Pierce sends the
        /// wasted damage into whoever is standing behind, so the same number becomes a reason
        /// to take it: it clears a column the way the shotgun clears a row.
        /// </summary>
        public readonly int Pierce;

        /// <summary>One word for the gate label. What this gun is *for*, at a glance.</summary>
        public readonly string Trait;

        /// <summary>
        /// Gate HP: how much damage you pour into the gate to claim this gun.
        ///
        /// This is the cost in Simon's shoot-through-gate design. Grinding a gate down is time
        /// you're NOT shooting the horde, so a high number is a real gamble -- commit to the
        /// premium gate and the enemies pile up while you break through it. Cheap guns are a
        /// quick claim, premium guns make you bleed for them.
        /// </summary>
        public readonly float GateCost;

        /// <summary>
        /// Supply cost to unlock this gun in the Shop, permanently, for every future run. A
        /// separate currency and a separate cost from GateCost on purpose -- GateCost is spent
        /// mid-run (gate HP) to borrow the gun for this run only; UnlockCost is spent between
        /// runs to make it available at gates at all. Zero means "already owned" -- only the
        /// starting weapon (see SaveData.NewSave) ships that way.
        /// </summary>
        public readonly int UnlockCost;

        /// <summary>
        /// How far up the lane this gun kills. The axis that makes the table a real choice.
        ///
        /// Every gun used to share Tuning.WeaponRange, so they differed only in numbers --
        /// "minigun is obv the op weapon", "AK and SMG i just do it randomly". Damage-per-second
        /// alone can only ever produce a ranking, and a ranking isn't a decision.
        ///
        /// Range is a real cost because enemies walk at you: a short-ranged gun means they get
        /// close before they die, which is the same thing as risk. So the minigun can keep the
        /// highest DPS in the game and still be a genuine gamble, and the sniper can kill less
        /// per second while being the safe pick. That's a choice; a bigger number isn't.
        /// </summary>
        public readonly float Range;

        /// <summary>
        /// Per-gun mix level, normalised against each clip's *measured* RMS (see
        /// Editor/AudioReport). The source files differ by 5.6x in inherent loudness -- the AK's
        /// laser is rms 0.42, the minigun's 0.076 -- so a single shared volume made some guns
        /// deafening and others silent regardless of the number.
        ///
        /// Rate is also priced in: a gun firing 22x/second must sit far lower per shot than one
        /// firing twice, or it stops being a gun and becomes a wall.
        /// </summary>
        public readonly float ShotVolume;

        /// <summary>
        /// Playback pitch. Pitching a mid-heavy clip *down* is how a gun gets weight on a phone.
        ///
        /// The sniper was lowFrequency_explosion: peak 0.904, as hot as anything in the project,
        /// and still reported as barely audible at maximum volume. Phone speakers physically
        /// cannot reproduce bass -- they roll off hard below a few hundred Hz -- so a sound whose
        /// energy is all low end is a sound the device throws away. Heavy has to be built from
        /// frequencies the speaker can actually move.
        /// </summary>
        public readonly float ShotPitch;

        public WeaponDef(string mesh, string displayName, float fireInterval, float damage,
                         int pellets = 1, float spreadDegrees = 0f, bool downgrade = false,
                         int pierce = 0, string trait = null, float shotVolume = 0.3f,
                         float shotPitch = 1f, float range = 16f, float gateCost = 100f,
                         int unlockCost = 0)
        {
            ShotPitch = shotPitch;
            Range = range;
            GateCost = gateCost;
            UnlockCost = unlockCost;
            Mesh = mesh;
            DisplayName = displayName;
            FireInterval = fireInterval;
            Damage = damage;
            Pellets = pellets;
            SpreadDegrees = spreadDegrees;
            Downgrade = downgrade;
            Pierce = pierce;
            Trait = trait;
            ShotVolume = shotVolume;
        }

        public float Dps => Damage * Pellets / FireInterval;
    }

    /// <summary>
    /// The weapon table.
    ///
    /// These are deliberately *not* a straight power ladder. If every gate were an upgrade the
    /// choice would be fake -- you'd take whichever number was bigger and never think again.
    /// Instead they trade along different axes at broadly similar DPS: the Shotgun buys crowd
    /// coverage with single-target damage, the Sniper the reverse, and the Pistol is an outright
    /// downgrade so that picking the wrong gate can actually cost you. Enemy health is 25, so
    /// the interesting number per weapon is how many hits that takes.
    /// </summary>
    public static class Weapons
    {
        // shotVolume values are 1/measured-rms, scaled by fire rate. Clip rms in brackets.
        // Guns sit low because the kill cue caps at 1.0 and has to cut through constant gunfire.
        //
        // No gun here is strictly better than another: each buys power with range, or range with
        // power. Ranges are set against the camera projection -- 16 puts kills ~13% down the
        // screen, 9 puts them almost on top of the player, 26 is near the horizon.

        // Deliberately NOT offered at gates -- see Pickups.
        public static readonly WeaponDef Pistol = new("Pistol", "PISTOL", 0.28f, 12f,
                                                      downgrade: true, shotVolume: 0.38f,
                                                      range: 16f);                           // rms 0.209

        // The baseline. Everything else is a deviation from this.
        public static readonly WeaponDef AK = new("AK", "AK", 0.12f, 10f,
                                                  trait: "BALANCED", shotVolume: 0.1f,
                                                  range: 16f, gateCost: 100f);               // rms 0.423, loudest file

        // Twice the AK's rate for two thirds the damage, and it gives up a third of the range to
        // get it. Fast, but they arrive. Cheapest gate -- a quick claim.
        public static readonly WeaponDef SMG = new("SMG", "SMG", 0.07f, 7f, spreadDegrees: 5f,
                                                   trait: "FAST", shotVolume: 0.32f,
                                                   range: 11f, gateCost: 70f, unlockCost: 150);  // rms 0.082, 14/sec

        // Short-range crowd clearer. Range 13 (up from 9 -- "shotgun is maybe too nerfed"): 9
        // was near-suicidal, and its real DPS is already low because pellets miss at 20 deg of
        // spread, so it was paying the range cost twice. Still the shortest reach in the table,
        // just no longer a death sentence. Damage 10/pellet so a centre hit actually clears the
        // row it's built to clear.
        public static readonly WeaponDef Shotgun = new("Shotgun", "SHOTGUN", 0.50f, 10f,
                                                       pellets: 5, spreadDegrees: 20f, trait: "SPREAD",
                                                       shotVolume: 0.95f, range: 13f, gateCost: 130f,
                                                       unlockCost: 220);
        // Pierce 4 = punches through five in a line. Damage stays high so it one-shots each.
        // A real rifle at last: qubodup's "Sniper Rifle Shot" (freesound 182051), CC0, lifted
        // from public-domain US Government footage. Measured peak 1.000, bright 0.187 -- well
        // clear of the ~0.11 line below which this phone's speaker simply doesn't reproduce
        // sound. Unpitched, because it's an actual recording rather than a laser in disguise.
        //
        // NB it's 1.95s against a 0.6s fire interval, so ~3 tails overlap. The tail is the echo
        // that makes it read as a rifle (and the "whoosh" that was asked for), but it's the
        // first suspect if the sniper ever turns to mush.
        // 0.9: still a bang, just not one that buries everything else.
        // Range 26 reaches nearly to the horizon -- the safe pick. It kills least per second and
        // kills furthest away, which is the whole trade.
        // Premium: high gate cost. Grinding 240 HP down is real time not spent on the horde.
        public static readonly WeaponDef Sniper = new("Sniper", "SNIPER", 0.60f, 75f,
                                                      pierce: 4, trait: "PIERCE",
                                                      shotVolume: 0.9f, shotPitch: 1f,
                                                      range: 26f, gateCost: 240f, unlockCost: 380);

        // Keeps the highest DPS in the game -- that part was never the problem. Range 10 is the
        // price: "minigun is obv the op weapon" was true because raw power cost nothing. Now it
        // shreds anything that gets close, and everything gets close. Most expensive gate.
        public static readonly WeaponDef Minigun = new("ShortCannon", "MINIGUN", 0.045f, 6f,
                                                       spreadDegrees: 8f, trait: "RAPID",
                                                       shotVolume: 0.26f, range: 10f, gateCost: 280f,
                                                       unlockCost: 450);

        /// <summary>What the soldier starts a run holding. Must match ArtImporter's built gun.</summary>
        public static readonly WeaponDef Starting = AK;

        /// <summary>
        /// Shoot-through gates always pair a cheap gun against a premium one, so the choice is
        /// always "quick claim, modest gun" vs "bleed for it, better gun" -- Simon's exact
        /// framing. Splitting the pools guarantees that tension instead of leaving it to a random
        /// two-of-five draw that might offer SMG vs AK (no real stakes).
        /// </summary>
        public static readonly WeaponDef[] Cheap = { SMG, Shotgun, AK };
        public static readonly WeaponDef[] Premium = { Sniper, Minigun };

        /// <summary>All gate-offerable guns, for the weapon codex.</summary>
        public static readonly WeaponDef[] Pickups = { AK, SMG, Shotgun, Sniper, Minigun };
    }
}

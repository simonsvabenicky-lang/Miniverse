using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FlowSort.Blocks
{
    /// <summary>
    /// Owns the two off-belt zones and the traffic between them.
    ///
    ///   LINES     - where towers start. Only the FRONT of each line can be sent; the rest are
    ///               drawn dimmed so you can see what is coming and plan colours ahead.
    ///   CONVEYOR  - transient. A tower rides one lap, firing only where its colour is exposed.
    ///   LANDING   - the row of squares a tower drops into if it comes back with ammo left.
    ///               It can be sent again from there.
    ///
    /// The fail state lives here: a tower returning from its lap needs a free landing square.
    /// Send towers whose colour is scarce or buried and they come back nearly full, clog the
    /// squares, and once there is nowhere to land, the level is lost.
    /// </summary>
    public class TowerSlots : MonoBehaviour
    {
        public BlockArt Art;
        public BallSystem Balls;
        public BlockWall Wall;
        public ImpactFX Fx;
        public Camera TapCamera;

        public event Action OnAllTowersLost;

        readonly List<ConveyorTower>[] lines = new List<ConveyorTower>[BlockTuning.LineCount];
        /// <summary>Sized in Awake, because the count is bought with coins and varies per player.</summary>
        ConveyorTower[] landing;

        public int LandingUsed
        {
            get
            {
                int n = 0;
                foreach (var t in landing) if (t != null) n++;
                return n;
            }
        }

        public int TowersRemaining
        {
            get
            {
                int n = LandingUsed;
                foreach (var line in lines) if (line != null) n += line.Count;
                return n;
            }
        }

        public bool AnyTowerUsable => TowersRemaining > 0;

        readonly List<ConveyorTower> riding = new List<ConveyorTower>(BlockTuning.MaxOnBelt);
        readonly bool[] liveColors = new bool[BlockPalette.Count];

        /// <summary>Towers currently riding the belt. Capped at BlockTuning.MaxOnBelt.</summary>
        public int RidingCount => riding.Count;

        /// <summary>
        /// Tells the wall which colours are shootable right now, so everything else can be pushed
        /// back visually. Called only when the set changes, never per frame — it rebuilds meshes.
        /// </summary>
        void RefreshLiveColors()
        {
            for (int i = 0; i < liveColors.Length; i++) liveColors[i] = false;
            foreach (var tower in riding)
                if (tower != null) liveColors[tower.ColorIndex] = true;

            Wall?.SetLiveColors(liveColors);
        }

        void Awake()
        {
            Layout.EnsureConfigured();
            landing = new ConveyorTower[Layout.SlotCount];
            for (int i = 0; i < lines.Length; i++) lines[i] = new List<ConveyorTower>();
        }

        /// <summary>
        /// Deals the level's towers.
        ///
        /// The hand is generated FROM the board rather than rolled blind: every colour on the
        /// grid gets towers totalling WinAmmoMargin rounds per block of that colour, so there is
        /// always strictly more ammo of each colour than there is wall to shoot with it. A level
        /// is therefore always winnable on paper, and losing is only ever the result of clogging
        /// the landing squares with towers you sent at colours that were buried or already gone.
        ///
        /// Towers are small (AmmoMin..AmmoMax) and numerous by design: many cheap towers make the
        /// choice of which colour to send next the interesting decision, where a handful of fat
        /// ones just made it a formality.
        /// </summary>
        public void Fill(BlockWall wall, int level)
        {
            ClearAll();

            var counts = new int[BlockPalette.Count];
            wall.CountByColor(counts);

            var exposure = new int[BlockPalette.Count];
            wall.CountExposedByColor(exposure);

            var hand = DealHand(counts, exposure, level);

            for (int i = 0; i < hand.Count; i++)
            {
                int line = i % BlockTuning.LineCount;
                int index = lines[line].Count;
                lines[line].Add(BuildTower(hand[i].Color, hand[i].Ammo, LinePosition(line, index)));
            }

            RefreshLineVisuals();
        }

        public readonly struct Deal
        {
            public readonly byte Color;
            public readonly int Ammo;
            public Deal(byte color, int ammo) { Color = color; Ammo = ammo; }
        }

        /// <summary>
        /// The whole hand, generated and ordered. Public and driven by plain counts so the
        /// balance simulator can deal exactly the hands the game deals rather than an
        /// approximation of them.
        /// </summary>
        public static List<Deal> DealHand(int[] counts, int[] exposure, int level)
        {
            var hand = BuildHand(counts, BlockTuning.WinAmmoMargin(level));
            Shuffle(hand);
            OrderByDifficulty(hand, exposure, level);
            SpreadFrontColors(hand);
            return hand;
        }

        static List<Deal> BuildHand(int[] counts, float margin)
        {
            var hand = new List<Deal>(64);

            for (byte color = 1; color < BlockPalette.Count; color++)
            {
                if (counts[color] <= 0) continue;

                int budget = Mathf.CeilToInt(counts[color] * margin);
                while (budget > 0)
                {
                    int ammo = RollAmmo();

                    // Don't let the last tower of a colour be a near-empty stub: fold a small
                    // remainder into it instead of dealing a 10-round tower to clear 2 blocks.
                    if (budget - ammo < BlockTuning.AmmoMin)
                        ammo = Mathf.Min(BlockTuning.AmmoMax, Mathf.Max(BlockTuning.AmmoMin, budget));

                    hand.Add(new Deal(color, ammo));
                    budget -= ammo;
                }
            }

            return hand;
        }

        /// <summary>
        /// Flat across the range.
        ///
        /// This used to be weighted toward small towers, which bought a board many more of them —
        /// but the landing row is a throughput cap, not a storage problem: a tower can only leave
        /// a line when a square frees up, so a hand far larger than the squares can cycle leaves
        /// most of it permanently unreachable. Flat keeps the hand near what five squares can
        /// actually turn over. See BalanceSim.
        /// </summary>
        static int RollAmmo()
        {
            int steps = (BlockTuning.AmmoMax - BlockTuning.AmmoMin) / BlockTuning.AmmoStep;
            return BlockTuning.AmmoMin + UnityEngine.Random.Range(0, steps + 1) * BlockTuning.AmmoStep;
        }

        /// <summary>
        /// Decides where in the lines each tower sits — which is where a level's difficulty
        /// actually comes from.
        ///
        /// A colour is only worth sending if it is currently on the exposed face of the picture.
        /// On level 1 the towers whose colours you can shoot right now are at the FRONT of the
        /// lines, so every send does something. As levels climb, that inverts: the colours you
        /// need are buried behind towers for colours that are already gone or still walled in, so
        /// clearing the way costs you landing squares and the squares are what run out.
        ///
        /// Around the midpoint the two pulls cancel and the jitter dominates, which is a genuinely
        /// neutral shuffle rather than a seam between two regimes.
        /// </summary>
        static void OrderByDifficulty(List<Deal> hand, int[] exposure, int level)
        {
            int peak = 1;
            foreach (int n in exposure) peak = Mathf.Max(peak, n);

            // Capped below 1: a fully inverted order buries every usable colour behind every dead
            // one, and the level stops being hard and starts being a coin flip.
            float hostility = Mathf.Clamp(
                (BlockTuning.DifficultyLevel(level) - 1) / (float)BlockTuning.DifficultyRampLevels,
                0f, BlockTuning.MaxHostility);

            var keys = new float[hand.Count];
            for (int i = 0; i < hand.Count; i++)
            {
                float useful = exposure[hand[i].Color] / (float)peak;
                float jitter = UnityEngine.Random.Range(-0.25f, 0.25f);

                // -useful sorts the immediately usable towers to the front; +useful buries them.
                keys[i] = Mathf.Lerp(-useful, useful, hostility) + jitter;
            }

            // Insertion sort: the hand is a couple of dozen entries and this keeps keys and deals
            // in step without allocating a comparer or a key-value pair per tower.
            for (int i = 1; i < hand.Count; i++)
            {
                var deal = hand[i];
                float key = keys[i];
                int j = i - 1;

                while (j >= 0 && keys[j] > key)
                {
                    hand[j + 1] = hand[j];
                    keys[j + 1] = keys[j];
                    j--;
                }

                hand[j + 1] = deal;
                keys[j + 1] = key;
            }
        }

        /// <summary>
        /// Forces the three line fronts to be different colours.
        ///
        /// Ordering purely by exposure means the single most shootable colour wins all three front
        /// places, and a level opens with three identical options — no choice at all, which is the
        /// entire reason there is more than one line. Each duplicate is swapped for the earliest
        /// tower further back that brings a new colour, so the ordering is otherwise untouched.
        /// </summary>
        static void SpreadFrontColors(List<Deal> hand)
        {
            int fronts = Mathf.Min(BlockTuning.LineCount, hand.Count);

            for (int i = 0; i < fronts; i++)
            {
                if (!ClashesWithFront(hand, i, i)) continue;

                for (int k = i + 1; k < hand.Count; k++)
                {
                    if (ClashesWithFront(hand, k, i)) continue;
                    (hand[i], hand[k]) = (hand[k], hand[i]);
                    break;
                }
            }
        }

        static bool ClashesWithFront(List<Deal> hand, int candidate, int frontCount)
        {
            for (int j = 0; j < frontCount; j++)
                if (hand[j].Color == hand[candidate].Color) return true;
            return false;
        }

        /// <summary>
        /// Interleaves the colours so no line is a single-colour block. Without this the hand
        /// comes out grouped by colour and the front of every line offers the same choice.
        /// </summary>
        static void Shuffle(List<Deal> hand)
        {
            for (int i = hand.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (hand[i], hand[j]) = (hand[j], hand[i]);
            }
        }

        static Vector3 LinePosition(int line, int index) => new Vector3(
            Layout.LineX(line), Layout.LineY(index), 0f);

        ConveyorTower BuildTower(byte color, int ammo, Vector3 rest)
        {
            var tower = TowerFactory.Create(Art, color, ammo, Balls, Wall, Fx, transform, rest);
            tower.OnSpent += HandleSpent;
            tower.OnReturnedToSlot += HandleReturned;
            return tower;
        }

        /// <summary>Only the front tower of each line is live; the rest are dimmed previews.</summary>
        void RefreshLineVisuals()
        {
            for (int line = 0; line < lines.Length; line++)
            {
                for (int i = 0; i < lines[line].Count; i++)
                {
                    var tower = lines[line][i];
                    if (tower == null) continue;

                    // Towers deeper than the visible run are held offstage until they advance.
                    bool visible = i < BlockTuning.VisibleLineDepth;
                    tower.gameObject.SetActive(visible);
                    if (!visible) continue;

                    tower.SetRestPosition(LinePosition(line, i));
                    tower.SetDimmed(i > 0);
                }
            }
        }

        void HandleReturned(ConveyorTower tower)
        {
            riding.Remove(tower);
            RefreshLiveColors();

            // Its colour is gone from the board, so it can never do anything again. Retiring it
            // rather than parking it is what keeps the landing row from silently filling with
            // dead weight — with a 1.2 ammo margin roughly a sixth of every colour's rounds are
            // surplus, and those towers used to occupy squares permanently.
            if (Wall != null && !Wall.HasColor(tower.ColorIndex))
            {
                tower.Retire();
                return;
            }

            int free = FirstFreeLanding();

            if (free < 0)
            {
                // Nowhere to put it — this is the loss.
                OnAllTowersLost?.Invoke();
                return;
            }

            landing[free] = tower;
            tower.SlotIndex = free;
            tower.SetRestPosition(LandingPosition(free));
            tower.SetDimmed(false);
            Meta.Sfx.Instance?.Play(Meta.Sfx.Instance.Land, 0.5f);
        }

        void HandleSpent(ConveyorTower tower)
        {
            // A tower only ever runs dry mid-lap, so this is always a belt slot freeing up.
            riding.Remove(tower);
            RefreshLiveColors();

            for (int i = 0; i < landing.Length; i++)
                if (landing[i] == tower) landing[i] = null;

            foreach (var line in lines) line.Remove(tower);
            RefreshLineVisuals();
        }

        int FirstFreeLanding()
        {
            for (int i = 0; i < landing.Length; i++)
                if (landing[i] == null) return i;
            return -1;
        }

        static Vector3 LandingPosition(int slot) => new Vector3(Layout.SlotX(slot), Layout.SlotY, 0f);

        /// <summary>
        /// Retires every tower still in play, with a small stagger so the board empties as a
        /// gesture rather than blinking out. Used between levels: without it, towers mid-lap and
        /// towers parked in squares carried straight into the next picture and started shooting
        /// it before it had been dealt a hand.
        /// </summary>
        public void SweepAway()
        {
            foreach (var tower in transform.GetComponentsInChildren<ConveyorTower>())
            {
                if (tower == null) continue;
                tower.RetireAfter(UnityEngine.Random.Range(0f, 0.35f));
            }

            riding.Clear();
            for (int i = 0; i < landing.Length; i++) landing[i] = null;
            foreach (var line in lines) line?.Clear();

            RefreshLiveColors();
        }

        public void ClearAll()
        {
            riding.Clear();
            RefreshLiveColors();

            // Every tower under this object, not just the ones the lines and landing row still
            // know about — a tower mid-lap belongs to neither, and those were the ones surviving
            // into the next level.
            foreach (var tower in transform.GetComponentsInChildren<ConveyorTower>())
                if (tower != null) Destroy(tower.gameObject);

            foreach (var line in lines)
            {
                if (line == null) continue;
                foreach (var t in line) if (t != null) Destroy(t.gameObject);
                line.Clear();
            }

            for (int i = 0; i < landing.Length; i++)
            {
                if (landing[i] != null) Destroy(landing[i].gameObject);
                landing[i] = null;
            }
        }

        void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;

            var cam = TapCamera != null ? TapCamera : Camera.main;
            if (cam == null) return;

            Vector2 screen = pointer.position.ReadValue();
            if (!Physics.Raycast(cam.ScreenPointToRay(screen), out var hit, 500f)) return;

            var tapped = hit.collider.GetComponentInParent<ConveyorTower>();
            if (tapped == null || tapped.Current != ConveyorTower.State.InSlot) return;

            TrySend(tapped);
        }

        void TrySend(ConveyorTower tower)
        {
            // The belt is full — the tap is refused outright rather than queued, so the cap is
            // something you plan around instead of something that silently reorders your sends.
            if (RidingCount >= BlockTuning.MaxOnBelt)
            {
                tower.RejectSend();
                Meta.Sfx.Instance?.Play(Meta.Sfx.Instance.Deny, 0.5f);
                return;
            }

            // From a landing square: always allowed, and it frees the square.
            for (int i = 0; i < landing.Length; i++)
            {
                if (landing[i] != tower) continue;

                landing[i] = null;
                Launch(tower);
                return;
            }

            // From a line: only the front tower can go.
            foreach (var line in lines)
            {
                int index = line.IndexOf(tower);
                if (index < 0) continue;
                if (index != 0) return; // tapped a queued preview, not the front

                line.RemoveAt(0);
                Launch(tower);
                RefreshLineVisuals();
                return;
            }
        }

        void Launch(ConveyorTower tower)
        {
            riding.Add(tower);
            tower.Deploy();
            RefreshLiveColors();
            Meta.Sfx.Instance?.Play(Meta.Sfx.Instance.Deploy, 0.55f);
        }
    }
}

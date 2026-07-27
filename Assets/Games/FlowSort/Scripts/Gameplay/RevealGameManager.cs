using System;
using System.Collections.Generic;
using FlowSort.UI;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace FlowSort.Gameplay
{
    /// <summary>
    /// Owns level lifecycle: builds a grid from a random subset of colors, refills the critter
    /// queue, and reacts to keys/completion. Standalone for now, same as Junction Sort was —
    /// not implementing IMiniGame until this is proven fun (see HANDOFF.md).
    /// </summary>
    public class RevealGameManager : MonoBehaviour
    {
        public PuzzleGrid Grid;
        public CritterQueue Queue;
        public FiringLane[] Lanes;
        public CurrencyWallet Wallet;
        public RevealHud Hud;

        // Assigned by SceneBuilder, wired at runtime in Start() -- a Button's onClick listener
        // is a C# delegate, and delegates added via AddListener() from an editor-time script
        // don't survive scene serialization (only entries added through the Editor's own
        // Inspector become persistent calls). Wiring it here instead, same pattern PowerupBar
        // already uses for its own buttons.
        public Button ExitButton;

        public int Level { get; private set; } = 1;

        /// <summary>
        /// Fired when the player taps the exit button. Nothing subscribes to this standalone —
        /// it exists so a future hub wrapper can do `gm.OnExitRequested += hubContext.ReturnToHub`
        /// without any other plumbing (this mechanic has no lose state, so an exit button is the
        /// only way a session ends short of quitting the app entirely).
        /// </summary>
        public event Action OnExitRequested;

        public void RequestExit() => OnExitRequested?.Invoke();

        List<PieceColor> activeColors;
        static readonly PieceColor[] AllColors =
            { PieceColor.Blue, PieceColor.Green, PieceColor.Pink, PieceColor.Purple, PieceColor.Red, PieceColor.Yellow };

        void OnEnable()
        {
            Grid.OnCellCleared += HandleCellCleared;
            Grid.OnGridComplete += HandleGridComplete;
        }

        void OnDisable()
        {
            Grid.OnCellCleared -= HandleCellCleared;
            Grid.OnGridComplete -= HandleGridComplete;
        }

        void Start()
        {
            if (ExitButton != null) ExitButton.onClick.AddListener(RequestExit);
            StartLevel();
        }

        void StartLevel()
        {
            activeColors = PickActiveColors();
            Grid.BuildLevel(activeColors);
            Queue.Refill();
            Hud.SetLevel(Level);
        }

        List<PieceColor> PickActiveColors()
        {
            var pool = new List<PieceColor>(AllColors);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            return pool.GetRange(0, GameTuning.ActiveColorCount);
        }

        public PieceColor PickWeightedActiveColor() => activeColors[Random.Range(0, activeColors.Count)];

        public bool TryAssignToLane(Critter critter)
        {
            foreach (var lane in Lanes)
            {
                if (lane.IsFree)
                {
                    lane.Assign(critter);
                    return true;
                }
            }
            return false;
        }

        void HandleCellCleared(PieceColor color, bool wasKey)
        {
            if (wasKey) Wallet.Add(1);
        }

        void HandleGridComplete()
        {
            Wallet.Add(GameTuning.ChestBonusKeys);
            Level++;
            Invoke(nameof(StartLevel), 1.2f);
        }

        public void UsePowerupRefill()
        {
            foreach (var lane in Lanes)
            {
                // Lane critters are accessed indirectly since FiringLane doesn't expose the
                // current critter publicly — ask the lane to add ammo to whatever it holds.
                lane.RefillCurrent(GameTuning.RefillAmmoAmount);
            }
        }

        public void UsePowerupShuffle() => Queue.ShuffleAll();

        public void UsePowerupUndo() => Grid.UndoLast();

        public void UsePowerupHint()
        {
            PieceColor? best = null;
            int bestRemaining = int.MaxValue;
            foreach (var c in activeColors)
            {
                int remaining = Grid.RemainingOfColor(c);
                if (remaining > 0 && remaining < bestRemaining)
                {
                    bestRemaining = remaining;
                    best = c;
                }
            }
            if (best.HasValue) Grid.PulseColor(best.Value);
        }
    }
}

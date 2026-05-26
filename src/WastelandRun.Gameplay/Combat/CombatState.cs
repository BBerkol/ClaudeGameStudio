// WastelandRun.Gameplay — Combat/CombatState.cs
// Mutable POCO that holds the entire combat-scope state for a single encounter.
// Engine-free; owned by CombatLoop and never persisted directly (the run-layer DTO
// snapshots a subset on save per ADR-0004).

using System;
using WastelandRun.Cards;
using WastelandRun.Enemies;
using WastelandRun.Vehicle;

namespace WastelandRun.Gameplay.Combat
{
    /// <summary>
    /// All mutable state for one combat encounter. Updated synchronously by
    /// <see cref="CombatLoop"/> step methods. Read by the HUD / view layer.
    ///
    /// Lifetime: one instance per encounter. Recreated by CombatLoop.Setup.
    /// </summary>
    public sealed class CombatState
    {
        public Vehicle.Vehicle Player { get; }
        public Vehicle.Vehicle Enemy  { get; }

        public DeckStateManager Deck { get; }
        public IEnemyBrain      Brain { get; }
        public CombatRules      Rules { get; }

        /// <summary>Deterministic combat RNG — per-combat instance seeded via ADR-0003.</summary>
        public Random Rng { get; }

        /// <summary>1-based turn index. Incremented at the start of each PlayerTurn (R2).</summary>
        public int TurnIndex { get; internal set; }

        /// <summary>Energy available for the current PlayerTurn. Refreshed at Setup / start of each turn.</summary>
        public int Energy { get; internal set; }

        /// <summary>Player position on the chase rail. Mutated by ShiftPositionEffect.</summary>
        public PositionState PlayerPosition { get; internal set; }

        /// <summary>
        /// The intent telegraphed for the next EnemyTurn — picked at end of the previous
        /// turn (R4). Null only at the moment of Setup-before-first-pick.
        /// </summary>
        public PickedIntent NextEnemyIntent { get; internal set; }

        /// <summary>
        /// CombatOutcome.InProgress until either Frame goes Offline. Set by CombatLoop
        /// after each damage resolution; once non-InProgress, all step methods early-out.
        /// </summary>
        public CombatOutcome Outcome { get; internal set; } = CombatOutcome.InProgress;

        public bool IsEnded => Outcome != CombatOutcome.InProgress;

        public CombatState(
            Vehicle.Vehicle player,
            Vehicle.Vehicle enemy,
            DeckStateManager deck,
            IEnemyBrain brain,
            CombatRules rules,
            Random rng,
            PositionState startingPosition)
        {
            Player         = player ?? throw new ArgumentNullException(nameof(player));
            Enemy          = enemy  ?? throw new ArgumentNullException(nameof(enemy));
            Deck           = deck   ?? throw new ArgumentNullException(nameof(deck));
            Brain          = brain  ?? throw new ArgumentNullException(nameof(brain));
            Rules          = rules  ?? throw new ArgumentNullException(nameof(rules));
            Rng            = rng    ?? throw new ArgumentNullException(nameof(rng));
            PlayerPosition = startingPosition;
        }

        /// <summary>
        /// Maps the card-system PositionState to the enemy-side CombatPosition.
        /// Player.Ahead means enemy is Behind, and vice versa.
        /// </summary>
        public CombatPosition EnemyPosition =>
            PlayerPosition == PositionState.Ahead ? CombatPosition.Behind : CombatPosition.Ahead;
    }
}

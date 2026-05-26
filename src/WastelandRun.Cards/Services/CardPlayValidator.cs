using System;
using System.Collections.Generic;
using WastelandRun.Vehicle;

namespace WastelandRun.Cards
{
    /// <summary>
    /// Validates card play preconditions at the logic layer. Engine-free per ADR-0006.
    /// Returns typed results — never throws on a failed precondition check.
    ///
    /// Usage example:
    ///   var validator = new CardPlayValidator();
    ///   var result = validator.CheckPositionRequirement(card, playerPosition);
    ///   if (result != CardPlayResult.Ok) BlockPlay(result);
    ///
    ///   var targets = validator.ResolveValidTargets(card, allSlots);
    /// </summary>
    public sealed class CardPlayValidator
    {
        /// <summary>
        /// Checks whether <paramref name="card"/> can be played from
        /// <paramref name="currentPosition"/>.
        /// BonusIfAhead / BonusIfBehind never block play — they only modify damage output.
        /// </summary>
        public CardPlayResult CheckPositionRequirement(ICardData card, PositionState currentPosition)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));

            return card.PositionRequirement switch
            {
                PositionRequirement.RequiresAhead  when currentPosition != PositionState.Ahead  => CardPlayResult.PositionRequirementNotMet,
                PositionRequirement.RequiresBehind when currentPosition != PositionState.Behind => CardPlayResult.PositionRequirementNotMet,
                _ => CardPlayResult.Ok
            };
        }

        /// <summary>
        /// Resolves the set of valid target slots for a card with
        /// <see cref="CardTargetType.EnemySubsystem"/>.
        /// An empty <see cref="ICardData.ValidSubsystemTargets"/> means all slots are valid (EC11).
        /// Returns <paramref name="allSlots"/> directly when the card targets all slots or has
        /// no target — callers must not mutate the returned list.
        /// </summary>
        public IReadOnlyList<SlotType> ResolveValidTargets(ICardData card, IReadOnlyList<SlotType> allSlots)
        {
            if (card == null)     throw new ArgumentNullException(nameof(card));
            if (allSlots == null) throw new ArgumentNullException(nameof(allSlots));

            if (card.TargetType != CardTargetType.EnemySubsystem)
                return allSlots;

            // EC11: empty ValidSubsystemTargets means all 4 slots are valid
            if (card.ValidSubsystemTargets == null || card.ValidSubsystemTargets.Count == 0)
                return allSlots;

            return card.ValidSubsystemTargets;
        }
    }

    /// <summary>Result of a card play precondition check.</summary>
    public enum CardPlayResult
    {
        /// <summary>Play is allowed.</summary>
        Ok,

        /// <summary>
        /// Blocked: the card requires a specific chase-rail position that the player
        /// does not currently occupy.
        /// </summary>
        PositionRequirementNotMet,

        /// <summary>Blocked: card's EnergyCost exceeds the current energy pool. R3 step 1.</summary>
        InsufficientEnergy,

        /// <summary>
        /// Blocked: card targets a specific subsystem and the supplied slot is not in
        /// <see cref="ICardData.ValidSubsystemTargets"/>. R3 step 3.
        /// </summary>
        InvalidTarget,

        /// <summary>Blocked: the supplied card instance is not in the player's hand.</summary>
        NotInHand
    }

    /// <summary>
    /// Chase-rail position of the player vehicle relative to the enemy.
    /// Ahead = player is in front; Behind = player is behind.
    /// </summary>
    public enum PositionState
    {
        Ahead,
        Behind
    }
}

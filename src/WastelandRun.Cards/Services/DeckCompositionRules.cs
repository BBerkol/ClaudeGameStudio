using System;
using System.Collections.Generic;

namespace WastelandRun.Cards
{
    /// <summary>
    /// Enforces deck size floor and ceiling constraints for Chopshop purge and card-add
    /// operations. Stateless — callers pass the current deck list by reference; this class
    /// mutates it. Engine-free per ADR-0006.
    ///
    /// Usage example:
    ///   var rules = new DeckCompositionRules();
    ///   var result = rules.TryPurge(deck, cardToRemove);
    ///   if (result != DeckGrowResult.Success) ShowInlineMessage(result);
    /// </summary>
    public sealed class DeckCompositionRules
    {
        /// <summary>Minimum deck size enforced by the Chopshop purge gate (EC7).</summary>
        public const int DeckMinSize = 10;

        /// <summary>Maximum deck size: a defensive safety ceiling, unreachable in EA content (ADR-0006).</summary>
        public const int MaxDeckSize = 60;

        /// <summary>
        /// Attempts to remove <paramref name="card"/> from <paramref name="deck"/>.
        /// Returns <see cref="DeckGrowResult.MinimumSizeReached"/> without mutating the
        /// deck if its current count is at or below <see cref="DeckMinSize"/>.
        /// IsStarterCard does NOT gate purge eligibility (EC6).
        /// </summary>
        /// <exception cref="ArgumentNullException">If deck or card is null.</exception>
        /// <exception cref="ArgumentException">If card is not present in the deck.</exception>
        public DeckGrowResult TryPurge(List<ICardData> deck, ICardData card)
        {
            if (deck == null)  throw new ArgumentNullException(nameof(deck));
            if (card == null)  throw new ArgumentNullException(nameof(card));

            if (deck.Count <= DeckMinSize)
                return DeckGrowResult.MinimumSizeReached;

            if (!deck.Remove(card))
                throw new ArgumentException($"TryPurge: card '{card.CardId}' is not present in the deck.", nameof(card));

            return DeckGrowResult.Success;
        }

        /// <summary>
        /// Attempts to add <paramref name="card"/> to <paramref name="deck"/>.
        /// Returns <see cref="DeckGrowResult.CapacityExceeded"/> without mutating the
        /// deck if its current count is at or above <see cref="MaxDeckSize"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">If deck or card is null.</exception>
        public DeckGrowResult TryGrow(List<ICardData> deck, ICardData card)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            if (card == null) throw new ArgumentNullException(nameof(card));

            if (deck.Count >= MaxDeckSize)
                return DeckGrowResult.CapacityExceeded;

            deck.Add(card);
            return DeckGrowResult.Success;
        }
    }
}

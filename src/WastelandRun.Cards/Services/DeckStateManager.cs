using System;
using System.Collections.Generic;

namespace WastelandRun.Cards
{
    /// <summary>
    /// Pure POCO state machine that owns card location across the four zones:
    /// deck, hand, discard, and exhausted. Card "state" is implicit — determined
    /// by which list a card appears in, not by a stored enum field.
    ///
    /// Stateless between construction and <see cref="Load"/>. All zone mutations
    /// are synchronous and engine-free per ADR-0006.
    /// </summary>
    public sealed class DeckStateManager
    {
        private readonly List<ICardData> _deck      = new();
        private readonly List<ICardData> _hand      = new();
        private readonly List<ICardData> _discard   = new();
        private readonly List<ICardData> _exhausted = new();

        public IReadOnlyList<ICardData> Deck      => _deck;
        public IReadOnlyList<ICardData> Hand      => _hand;
        public IReadOnlyList<ICardData> Discard   => _discard;
        public IReadOnlyList<ICardData> Exhausted => _exhausted;

        /// <summary>
        /// Populates zones from persisted run state. Clears all zones first.
        /// Hand is always empty at load time — cards in hand are not persisted mid-run.
        /// </summary>
        public void Load(
            IReadOnlyList<ICardData> deck,
            IReadOnlyList<ICardData> discard,
            IReadOnlyList<ICardData> exhausted)
        {
            if (deck == null)      throw new ArgumentNullException(nameof(deck));
            if (discard == null)   throw new ArgumentNullException(nameof(discard));
            if (exhausted == null) throw new ArgumentNullException(nameof(exhausted));
            _deck.Clear();
            _hand.Clear();
            _discard.Clear();
            _exhausted.Clear();
            _deck.AddRange(deck);
            _discard.AddRange(discard);
            _exhausted.AddRange(exhausted);
        }

        /// <summary>
        /// Draws one card from the top of the deck into the hand.
        /// If the deck is empty, reshuffles the discard first using the provided RNG.
        /// Returns null if both deck and discard are empty (AC-5).
        /// </summary>
        public ICardData? DrawOne(Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (_deck.Count == 0)
            {
                if (_discard.Count == 0) return null;
                ReshuffleDiscard(rng);
            }
            var card = _deck[0];
            _deck.RemoveAt(0);
            _hand.Add(card);
            return card;
        }

        /// <summary>
        /// Plays a card from the hand. Cards with the Exhaust keyword move to
        /// exhausted; all others move to discard. Retain has no effect here —
        /// it only prevents discard at end-of-turn.
        /// </summary>
        public void PlayCard(ICardData card)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            if (!_hand.Remove(card))
                throw new InvalidOperationException($"PlayCard: card '{card.CardId}' is not in hand.");
            if ((card.Keywords & CardKeyword.Exhaust) != 0)
                _exhausted.Add(card);
            else
                _discard.Add(card);
        }

        /// <summary>
        /// Ends the current turn. Cards without the Retain keyword move from
        /// hand to discard. Retain cards remain in hand for the next turn.
        /// </summary>
        public void EndTurn()
        {
            for (int i = _hand.Count - 1; i >= 0; i--)
            {
                if ((_hand[i].Keywords & CardKeyword.Retain) == 0)
                {
                    _discard.Add(_hand[i]);
                    _hand.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Moves all Innate-keyword cards from deck and discard into hand at combat start.
        /// Cards already in hand are skipped. Exhausted zone is not swept — Innate cards
        /// that were exhausted this run do not return.
        /// </summary>
        public void DrawInnateCards()
        {
            var alreadyInHand = new HashSet<ICardData>(_hand);
            MoveInnateToHand(_deck, alreadyInHand);
            MoveInnateToHand(_discard, alreadyInHand);
        }

        /// <summary>
        /// Removes all cards whose <see cref="ICardData.SourceSlotId"/> matches the
        /// given slot id from deck, hand, and discard. Exhausted zone is not swept —
        /// exhausted granted cards complete their exile naturally.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if sourceSlotId is null or empty.</exception>
        public void RemoveGrantedCards(string sourceSlotId)
        {
            if (string.IsNullOrEmpty(sourceSlotId))
                throw new ArgumentException("sourceSlotId must be non-null and non-empty.", nameof(sourceSlotId));
            RemoveBySourceSlot(_deck, sourceSlotId);
            RemoveBySourceSlot(_hand, sourceSlotId);
            RemoveBySourceSlot(_discard, sourceSlotId);
        }

        // Fisher-Yates in-place shuffle of _deck after absorbing _discard.
        private void ReshuffleDiscard(Random rng)
        {
            _deck.AddRange(_discard);
            _discard.Clear();
            for (int i = _deck.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = _deck[i];
                _deck[i] = _deck[j];
                _deck[j] = tmp;
            }
        }

        private void MoveInnateToHand(List<ICardData> source, HashSet<ICardData> alreadyInHand)
        {
            for (int i = source.Count - 1; i >= 0; i--)
            {
                var card = source[i];
                if ((card.Keywords & CardKeyword.Innate) != 0 && !alreadyInHand.Contains(card))
                {
                    source.RemoveAt(i);
                    _hand.Add(card);
                    alreadyInHand.Add(card);
                }
            }
        }

        private static void RemoveBySourceSlot(List<ICardData> list, string sourceSlotId)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].SourceSlotId == sourceSlotId)
                    list.RemoveAt(i);
            }
        }
    }
}

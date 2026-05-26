using System;
using System.Collections.Generic;

namespace WastelandRun.Cards
{
    /// <summary>
    /// Translates between the engine-free <see cref="CardSystemDTO"/> serialization model
    /// and the runtime <see cref="DeckStateManager"/>. Used by the save/load orchestrator
    /// defined in ADR-0004.
    ///
    /// <para>Assembly is engine-free — all logging uses <see cref="Console"/> rather than
    /// <c>UnityEngine.Debug</c>.</para>
    /// </summary>
    public sealed class CardSystemSaveAdapter
    {
        /// <summary>
        /// Serializes the current state of <paramref name="deck"/> into a
        /// <see cref="CardSystemDTO"/> suitable for JSON persistence.
        /// Hand cards are intentionally excluded — the hand is ephemeral and must be
        /// empty at save time (ADR-0004: saves occur only between combats).
        /// </summary>
        /// <param name="deck">The runtime deck manager to snapshot. Must not be null.
        /// Must have an empty hand — throws if any cards remain in hand.</param>
        /// <returns>A new <see cref="CardSystemDTO"/> reflecting the current zone state.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="deck"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <paramref name="deck"/> has cards in hand. Save must only be called
        /// between combats when the hand has been fully resolved.
        /// </exception>
        public CardSystemDTO Save(DeckStateManager deck)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            if (deck.Hand.Count > 0)
                throw new InvalidOperationException(
                    $"CardSystemSaveAdapter.Save called with {deck.Hand.Count} card(s) in hand. " +
                    "Save must only be called between combats when the hand is empty (ADR-0004).");

            var deckIds      = ExtractIds(deck.Deck);
            var discardIds   = ExtractIds(deck.Discard);
            var exhaustedIds = ExtractIds(deck.Exhausted);

            var copyCounts = new Dictionary<string, int>();
            CountIds(deckIds,      copyCounts);
            CountIds(discardIds,   copyCounts);
            CountIds(exhaustedIds, copyCounts);

            return new CardSystemDTO
            {
                Deck           = deckIds,
                Discard        = discardIds,
                Exhausted      = exhaustedIds,
                CardCopyCounts = copyCounts,
            };
        }

        /// <summary>
        /// Populates <paramref name="target"/> from a persisted <see cref="CardSystemDTO"/>.
        /// Unknown card IDs (not found in <paramref name="catalog"/>) are logged and skipped;
        /// the resulting zones may be shorter than the DTO lists if IDs were lost.
        /// A <see cref="CardSystemDTO.CardCopyCounts"/> mismatch is detected and logged;
        /// zone lists are always authoritative — stored counts are never used for resolution.
        /// <c>SourceSlotId</c> is runtime-only and will be null on all loaded cards; the
        /// vehicle system re-stamps it from the current part loadout (ADR-0006 §Amendment 2026-05-18).
        /// </summary>
        /// <param name="dto">The deserialized DTO. Must not be null. Zone lists must not be null.</param>
        /// <param name="catalog">The card catalog used to resolve IDs to data objects. Must not be null.</param>
        /// <param name="target">The deck manager to populate. Must not be null.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dto"/>, <paramref name="catalog"/>, or <paramref name="target"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if any zone list on <paramref name="dto"/> is null. Null lists indicate a corrupt
        /// or pre-schema save file — the orchestrator must migrate before calling Load.
        /// </exception>
        public void Load(CardSystemDTO dto, ICardCatalog catalog, DeckStateManager target)
        {
            if (dto     == null) throw new ArgumentNullException(nameof(dto));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (target  == null) throw new ArgumentNullException(nameof(target));

            if (dto.Deck           == null) throw new ArgumentException("dto.Deck is null — corrupt or pre-schema save file.",           nameof(dto));
            if (dto.Discard        == null) throw new ArgumentException("dto.Discard is null — corrupt or pre-schema save file.",        nameof(dto));
            if (dto.Exhausted      == null) throw new ArgumentException("dto.Exhausted is null — corrupt or pre-schema save file.",      nameof(dto));
            if (dto.CardCopyCounts == null) throw new ArgumentException("dto.CardCopyCounts is null — corrupt or pre-schema save file.", nameof(dto));

            ValidateCopyCounts(dto);

            var resolvedDeck      = ResolveIds(dto.Deck,      catalog);
            var resolvedDiscard   = ResolveIds(dto.Discard,   catalog);
            var resolvedExhausted = ResolveIds(dto.Exhausted, catalog);

            target.Load(resolvedDeck, resolvedDiscard, resolvedExhausted);
        }

        // ── Private helpers ────────────────────────────────────────────────────────

        private static List<string> ExtractIds(IReadOnlyList<ICardData> zone)
        {
            var ids = new List<string>(zone.Count);
            foreach (var card in zone)
                ids.Add(card.CardId);
            return ids;
        }

        private static void CountIds(IReadOnlyList<string> ids, Dictionary<string, int> counts)
        {
            foreach (var id in ids)
            {
                counts.TryGetValue(id, out var existing);
                counts[id] = existing + 1;
            }
        }

        /// <summary>
        /// Rebuilds copy counts from the three DTO zone lists and compares to
        /// <see cref="CardSystemDTO.CardCopyCounts"/>. Logs a warning to
        /// <see cref="Console.Error"/> if a mismatch is detected. Zone lists are
        /// always used for card resolution — stored counts are never used directly.
        /// </summary>
        private static void ValidateCopyCounts(CardSystemDTO dto)
        {
            var rebuilt = new Dictionary<string, int>();
            CountIds(dto.Deck,      rebuilt);
            CountIds(dto.Discard,   rebuilt);
            CountIds(dto.Exhausted, rebuilt);

            bool mismatch = rebuilt.Count != dto.CardCopyCounts.Count;
            if (!mismatch)
            {
                foreach (var kvp in rebuilt)
                {
                    if (!dto.CardCopyCounts.TryGetValue(kvp.Key, out var stored) || stored != kvp.Value)
                    {
                        mismatch = true;
                        break;
                    }
                }
            }

            if (mismatch)
                Console.Error.WriteLine(
                    "[CardSystemSaveAdapter] CardCopyCounts mismatch — loading from zone lists; stored counts ignored.");
        }

        private static List<ICardData> ResolveIds(IReadOnlyList<string> ids, ICardCatalog catalog)
        {
            var resolved = new List<ICardData>(ids.Count);
            foreach (var cardId in ids)
            {
                try
                {
                    resolved.Add(catalog.GetById(cardId));
                }
                catch (KeyNotFoundException)
                {
                    Console.WriteLine(
                        $"[CardSystemSaveAdapter] Unknown CardId '{cardId}' — skipped on load.");
                }
            }
            return resolved;
        }
    }
}

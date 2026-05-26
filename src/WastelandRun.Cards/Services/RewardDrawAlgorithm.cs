using System;
using System.Collections.Generic;
using System.Linq;
using WastelandRun.Vehicle;

namespace WastelandRun.Cards
{
    /// <summary>
    /// Deterministic card reward draw pipeline. Composes rarity weights, pity counter,
    /// primary-family bias (Mastery 1–3 Slot 1), copy-limit filtering, and without-replacement
    /// into a single pure function per ADR-0006 §"Reward Draw Algorithm".
    ///
    /// Stateless after construction — callers may share one instance across all reward draws.
    /// The caller constructs and owns the System.Random instance per ADR-0003; this class
    /// never constructs one internally.
    /// </summary>
    public sealed class RewardDrawAlgorithm : ICardRewardGenerator
    {
        // Copy limits per GDD §"Card Limits" (TR-card-016)
        private const int PityThreshold      = 8;
        private const int BiasMaxMastery     = 3;
        private const int InnateCap          = 3;  // TR-card-010: max Innate-keyword cards per deck
        private const int CommonCopyLimit    = 3;
        private const int UncommonCopyLimit  = 3;
        private const int RareCopyLimit      = 1;
        // Legendary is never eligible in EA reward draws (ICardCatalog may include them; we exclude them here)
        private const int LegendaryCopyLimit = 0;

        private readonly ICardCatalog    _catalog;
        private readonly IMasteryWeights _masteryWeights;
        private readonly TokenResolver   _tokenResolver;

        /// <summary>
        /// Constructs a RewardDrawAlgorithm. All dependencies are injected — no global state.
        /// </summary>
        public RewardDrawAlgorithm(
            ICardCatalog    catalog,
            IMasteryWeights masteryWeights,
            TokenResolver   tokenResolver)
        {
            _catalog        = catalog        ?? throw new ArgumentNullException(nameof(catalog));
            _masteryWeights = masteryWeights ?? throw new ArgumentNullException(nameof(masteryWeights));
            _tokenResolver  = tokenResolver  ?? throw new ArgumentNullException(nameof(tokenResolver));
        }

        /// <inheritdoc />
        public CardDraft[] Generate(
            ChassisType           chassis,
            int                   mastery,
            int                   rarePityCounter,
            int                   drawCount,
            IReadOnlyList<string> currentDeckCardIds,
            System.Random         rng)
        {
            if (rng == null)                throw new ArgumentNullException(nameof(rng));
            if (currentDeckCardIds == null) throw new ArgumentNullException(nameof(currentDeckCardIds));
            if (drawCount <= 0)             return Array.Empty<CardDraft>();

            var copyCountById   = BuildCopyCountMap(currentDeckCardIds);
            var primaryFamilies = _masteryWeights.GetPrimaryFamilies(chassis);
            var (commonW, uncommonW, rareW) = _masteryWeights.GetWeights(chassis, mastery);
            int deckInnateCount = CountDeckInnateCards(chassis, currentDeckCardIds);

            var results     = new List<CardDraft>(drawCount);
            var seenInOffer = new HashSet<string>(drawCount);

            for (int slotIndex = 0; slotIndex < drawCount; slotIndex++)
            {
                // Step 1: pity fires when counter >= threshold and no Rare drafted yet this offer
                bool pityFires = rarePityCounter >= PityThreshold
                    && !results.Any(d => d.Rarity == CardRarity.Rare);

                // Step 2: build candidate pool — chassis-filtered, copy-limited, Innate-capped, without-replacement
                var pool = BuildEligiblePool(chassis, copyCountById, seenInOffer, deckInnateCount);

                if (pool.Count == 0)
                    return Array.Empty<CardDraft>();  // full chassis pool exhausted

                ICardData chosen;

                if (pityFires)
                {
                    // Step 5: pity-forced Rare — if Rare pool empty return [] (Scrap compensation)
                    var rarePool = FilterByRarity(pool, CardRarity.Rare);
                    if (rarePool.Count == 0)
                        return Array.Empty<CardDraft>();
                    chosen = rarePool[rng.Next(rarePool.Count)];
                }
                else
                {
                    // Step 3: primary-family bias on Slot 1 at Mastery 1–3
                    IReadOnlyList<ICardData> activePool = pool;
                    if (slotIndex == 0 && mastery <= BiasMaxMastery && primaryFamilies.Count > 0)
                    {
                        var biasPool = pool.Where(c => primaryFamilies.Contains(c.Family)).ToList();
                        if (biasPool.Count > 0)
                            activePool = biasPool;
                        // bias pool exhausted — fall through to full pool, no error
                    }

                    // Step 4/7: weighted draw with implicit tier-degradation (empty tiers get weight 0)
                    chosen = PickWeighted(activePool, commonW, uncommonW, rareW, rng);
                    if (chosen == null)
                        return Array.Empty<CardDraft>();  // all rarity tiers exhausted in active pool
                }

                // Step 8: project to CardDraft; pre-bake RulesText via TokenResolver (TR-card-003)
                seenInOffer.Add(chosen.CardId);
                results.Add(BuildDraft(chosen, chassis, mastery, slotIndex));
            }

            return results.ToArray();
        }

        // ─── Pool construction ────────────────────────────────────────────────────

        private List<ICardData> BuildEligiblePool(
            ChassisType             chassis,
            Dictionary<string, int> copyCountById,
            HashSet<string>         seenInOffer,
            int                     deckInnateCount)
        {
            var all      = _catalog.GetByChassis(chassis);
            var eligible = new List<ICardData>(all.Count);
            foreach (var card in all)
            {
                if (seenInOffer.Contains(card.CardId)) continue;
                int limit = CopyLimitFor(card.Rarity);
                if (limit == 0) continue;  // Legendary — never eligible
                copyCountById.TryGetValue(card.CardId, out int owned);
                if (owned >= limit) continue;
                // TR-card-010: Innate cap — exclude Innate cards when deck already holds InnateCap copies
                if (deckInnateCount >= InnateCap && (card.Keywords & CardKeyword.Innate) != 0) continue;
                eligible.Add(card);
            }
            return eligible;
        }

        /// <summary>
        /// Counts total Innate-keyword cards (including duplicates) in the current deck.
        /// Uses the chassis catalog as the keyword source — deck cards from other chassis are ignored.
        /// </summary>
        private int CountDeckInnateCards(ChassisType chassis, IReadOnlyList<string> deckCardIds)
        {
            var chassisCards = _catalog.GetByChassis(chassis);
            var innateIds    = new HashSet<string>(chassisCards.Count);
            foreach (var card in chassisCards)
                if ((card.Keywords & CardKeyword.Innate) != 0)
                    innateIds.Add(card.CardId);

            int count = 0;
            foreach (var id in deckCardIds)
                if (innateIds.Contains(id))
                    count++;
            return count;
        }

        private static int CopyLimitFor(CardRarity rarity) => rarity switch
        {
            CardRarity.Common    => CommonCopyLimit,
            CardRarity.Uncommon  => UncommonCopyLimit,
            CardRarity.Rare      => RareCopyLimit,
            CardRarity.Legendary => LegendaryCopyLimit,
            _                    => 0
        };

        private static Dictionary<string, int> BuildCopyCountMap(IReadOnlyList<string> deckCardIds)
        {
            var map = new Dictionary<string, int>(deckCardIds.Count);
            foreach (var id in deckCardIds)
            {
                map.TryGetValue(id, out int c);
                map[id] = c + 1;
            }
            return map;
        }

        private static List<ICardData> FilterByRarity(List<ICardData> pool, CardRarity rarity)
        {
            var filtered = new List<ICardData>();
            foreach (var c in pool)
                if (c.Rarity == rarity) filtered.Add(c);
            return filtered;
        }

        // ─── Weighted draw with tier-degradation ──────────────────────────────────

        /// <summary>
        /// Picks one card from <paramref name="pool"/> using rarity weights.
        /// Tiers with no eligible cards get effective weight 0, providing implicit tier-degradation.
        /// Consumes exactly two rng.Next() calls per pick (one for rarity roll, one for card within tier).
        /// Returns null when all tiers are empty.
        /// </summary>
        private static ICardData PickWeighted(
            IReadOnlyList<ICardData> pool,
            int commonW, int uncommonW, int rareW,
            System.Random rng)
        {
            var rares    = FilterByRarityReadOnly(pool, CardRarity.Rare);
            var uncommons = FilterByRarityReadOnly(pool, CardRarity.Uncommon);
            var commons  = FilterByRarityReadOnly(pool, CardRarity.Common);

            // Zero out empty tiers — implicit tier-degradation without a loop
            int eRare    = rares.Count    > 0 ? rareW    : 0;
            int eUncommon = uncommons.Count > 0 ? uncommonW : 0;
            int eCommon  = commons.Count  > 0 ? commonW  : 0;
            int total    = eRare + eUncommon + eCommon;

            if (total == 0) return null;

            int roll = rng.Next(total);

            IReadOnlyList<ICardData> tier;
            if (roll < eRare)                     tier = rares;
            else if (roll < eRare + eUncommon)    tier = uncommons;
            else                                  tier = commons;

            return tier[rng.Next(tier.Count)];
        }

        private static List<ICardData> FilterByRarityReadOnly(IReadOnlyList<ICardData> pool, CardRarity rarity)
        {
            var filtered = new List<ICardData>();
            foreach (var c in pool)
                if (c.Rarity == rarity) filtered.Add(c);
            return filtered;
        }

        // ─── CardDraft projection ─────────────────────────────────────────────────

        private CardDraft BuildDraft(ICardData card, ChassisType chassis, int mastery, int slotIndex)
        {
            return new CardDraft
            {
                CardId        = card.CardId,
                DisplayName   = card.DisplayName,
                RulesText     = _tokenResolver.Resolve(card),
                Family        = card.Family,
                Rarity        = card.Rarity,
                EnergyCost    = card.EnergyCost,
                CardArtKey    = card.CardArtKey,
                KeywordBadges = EnumerateKeywords(card.Keywords),
                MerchantPrice = null,
                SelectionHash = ComputeDeterministicHash(card.CardId, (int)chassis, mastery, slotIndex)
            };
        }

        // StringComparer.Ordinal.GetHashCode bypasses .NET 5+ hash randomization — stable across sessions.
        private static int ComputeDeterministicHash(string cardId, int chassis, int mastery, int slotIndex)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + StringComparer.Ordinal.GetHashCode(cardId);
                h = h * 31 + chassis;
                h = h * 31 + mastery;
                h = h * 31 + slotIndex;
                return h;
            }
        }

        private static string[] EnumerateKeywords(CardKeyword keywords)
        {
            if (keywords == CardKeyword.None) return Array.Empty<string>();
            var names = new List<string>(4);
            if ((keywords & CardKeyword.Exhaust)  != 0) names.Add(nameof(CardKeyword.Exhaust));
            if ((keywords & CardKeyword.Retain)   != 0) names.Add(nameof(CardKeyword.Retain));
            if ((keywords & CardKeyword.Innate)   != 0) names.Add(nameof(CardKeyword.Innate));
            if ((keywords & CardKeyword.Ethereal) != 0) names.Add(nameof(CardKeyword.Ethereal));
            return names.ToArray();
        }
    }
}

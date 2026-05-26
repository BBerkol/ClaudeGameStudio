using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WastelandRun.Cards;
using WastelandRun.ScrapEconomy;
using WastelandRun.Vehicle;

// EditMode test — engine-free. WastelandRun.Cards and WastelandRun.ScrapEconomy both have
// noEngineReferences: true. No UnityEngine imports needed or allowed.
namespace WastelandRun.Tests.Unit.CardSystem
{
    [TestFixture]
    public class PityInnateCapTest
    {
        // ─── Helpers ──────────────────────────────────────────────────────────────

        private static RewardDrawAlgorithm Make(
            ICardCatalog    catalog,
            IMasteryWeights weights  = null,
            TokenResolver   resolver = null)
            => new RewardDrawAlgorithm(
                catalog,
                weights  ?? new RewardUniformWeightsStub(),
                resolver ?? new TokenResolver());

        private static System.Random Rng(int seed = 42) => new System.Random(seed);

        private static readonly string[] EmptyDeck = Array.Empty<string>();

        // ─────────────────────────────────────────────────────────────────────────
        // AC-1: Pity happy path
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_Generate_PityCounter8_NonEmptyRarePool_Slot1IsRare()
        {
            var algo = Make(new RewardFullPoolCatalogStub(), new RewardGddWeightsStub());
            var rng  = Rng(42);

            for (int i = 0; i < 50; i++)
            {
                var r = algo.Generate(ChassisType.Scout, 1, rarePityCounter: 8, 1, EmptyDeck, rng);
                Assert.AreEqual(1, r.Length, $"Trial {i}: pity must produce 1 draft");
                Assert.AreEqual(CardRarity.Rare, r[0].Rarity,
                    $"Trial {i}: rarePityCounter=8 must force a Rare draw");
            }
        }

        [Test]
        public void test_Generate_PityCounter7_BelowThreshold_NoRareGuarantee()
        {
            // At counter=7 (one short of threshold), standard weighted draw runs.
            // At Mastery 1 GDD weights (1% Rare), 200 draws should yield ~2 Rares, never 200.
            const int draws = 200;
            var algo = Make(new RewardFullPoolCatalogStub(), new RewardGddWeightsStub());
            var rng  = Rng(42);

            int rareCount = 0;
            for (int i = 0; i < draws; i++)
            {
                var r = algo.Generate(ChassisType.Scout, 1, rarePityCounter: 7, 1, EmptyDeck, rng);
                if (r.Length > 0 && r[0].Rarity == CardRarity.Rare) rareCount++;
            }

            // Pity at counter=7 must NOT force every draw to Rare.
            // At 1% rate, getting ≥50 Rares in 200 draws is probability ~10^-47 (impossible in practice).
            Assert.Less(rareCount, 50,
                $"rarePityCounter=7 must not guarantee Rare — pity threshold is 8. Got {rareCount}/{draws} Rares.");
        }

        [Test]
        public void test_Generate_PityCounter9_AboveThreshold_StillForcesRare()
        {
            // Pity threshold is ≥8, so counter=9 must also fire.
            var algo = Make(new RewardFullPoolCatalogStub(), new RewardGddWeightsStub());
            var rng  = Rng(42);

            for (int i = 0; i < 20; i++)
            {
                var r = algo.Generate(ChassisType.Scout, 1, rarePityCounter: 9, 1, EmptyDeck, rng);
                Assert.AreEqual(1, r.Length, $"Trial {i}: pity must produce 1 draft");
                Assert.AreEqual(CardRarity.Rare, r[0].Rarity,
                    $"Trial {i}: rarePityCounter=9 (≥8 threshold) must still force a Rare draw");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-2: Pity + empty Rare pool → Scrap compensation
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_Generate_PityCounter8_RarePoolEmpty_ReturnsScrapCompensation()
        {
            var catalog = new RewardSmallPoolCatalogStub();  // 1 Common, 1 Uncommon, 1 Rare
            var algo    = Make(catalog);
            var deck    = new List<string> { "scout_rare_001" };  // cap the only Rare

            var result = algo.Generate(ChassisType.Scout, 1, rarePityCounter: 8, 1, deck, Rng(42));

            Assert.IsNotNull(result, "Must return empty array — never null");
            Assert.AreEqual(0, result.Length,
                "Pity fires but Rare pool is empty — must return [] (Scrap compensation signal)");
        }

        [Test]
        public void test_Generate_PityCounter8_RarePoolEmpty_RepeatCallsYieldSameCompensation()
        {
            // After Scrap compensation, the next call with rarePityCounter=8 must also return [].
            // The algorithm is stateless; caller is responsible for maintaining the counter.
            var catalog = new RewardSmallPoolCatalogStub();
            var algo    = Make(catalog);
            var deck    = new List<string> { "scout_rare_001" };
            var rng     = Rng(42);

            for (int i = 0; i < 3; i++)
            {
                var result = algo.Generate(ChassisType.Scout, 1, rarePityCounter: 8, 1, deck, rng);
                Assert.AreEqual(0, result.Length,
                    $"Call {i + 1}: pity + empty Rare pool must consistently return [] (Scrap compensation)");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-3: Pity F2 fallback independence
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_Generate_PityEmptyRare_ReturnsEmptyNotDegradedUncommon()
        {
            // When pity fires on an empty Rare pool, result must be [] (Scrap compensation),
            // NOT a degraded Uncommon. F2 tier-degradation (Rare→Uncommon→Common) is exclusive
            // to the non-pity weighted draw path — pity + empty Rare pool is a hard stop.
            var catalog = new RewardSmallPoolCatalogStub();
            var algo    = Make(catalog, new RewardGddWeightsStub());
            var deck    = new List<string> { "scout_rare_001" };

            var result = algo.Generate(ChassisType.Scout, 1, rarePityCounter: 8, 1, deck, Rng(42));

            Assert.AreEqual(0, result.Length,
                "Pity + empty Rare pool must return [] — NOT a degraded Uncommon or Common. " +
                "F2 tier-degradation applies only to the standard weighted draw path, not pity.");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-4: Pity counter authority — read-only int parameter
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_Generate_PityCounter_AlgorithmDoesNotMutateCallerCounter()
        {
            // rarePityCounter is an int (value type) — passed by copy, never by ref.
            // This test documents the contract: counter authority belongs exclusively to
            // LootStateDTO (Loot & Reward system). The algorithm cannot mutate the caller's counter.
            var algo   = Make(new RewardFullPoolCatalogStub(), new RewardGddWeightsStub());
            int counter = 8;

            algo.Generate(ChassisType.Scout, 1, counter, 1, EmptyDeck, Rng(42));

            Assert.AreEqual(8, counter,
                "rarePityCounter is a read-only int parameter — " +
                "RewardDrawAlgorithm.Generate() must not mutate the caller's pity counter. " +
                "Counter authority belongs exclusively to LootStateDTO.");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-5: Pity reset signal — via CardDraft.Rarity, not algorithm action
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_Generate_PityReset_IsSignaledByRarePresentInResult()
        {
            // Pity counter reset protocol: the algorithm signals "Rare drawn" by including
            // a CardDraft with Rarity == Rare in the result. LootStateDTO observes this
            // and resets rarePityCounter to 0. The algorithm never directly resets any counter.
            var algo   = Make(new RewardFullPoolCatalogStub(), new RewardGddWeightsStub());
            var result = algo.Generate(ChassisType.Scout, 1, rarePityCounter: 8, 1, EmptyDeck, Rng(42));

            Assert.AreEqual(1, result.Length, "Pity must produce a draft");
            Assert.AreEqual(CardRarity.Rare, result[0].Rarity,
                "Pity reset signal: Rare is present in CardDraft[]. " +
                "LootStateDTO observes CardDraft.Rarity == Rare and resets rarePityCounter — not the algorithm.");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-6: Merchant Rare does NOT reset pity counter
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_Generate_MerchantPurchaseContext_AlgorithmAgnosticToMerchantSource()
        {
            // rarePityCounter tracks combat-reward drought only — Merchant purchases are outside
            // the algorithm's knowledge. If the caller passes counter=8 after a Merchant Rare
            // purchase without resetting, pity fires (algorithm cannot know the Merchant context).
            // Correct Merchant counter management is LootStateDTO's responsibility, not this algorithm's.
            var algo = Make(new RewardFullPoolCatalogStub(), new RewardGddWeightsStub());

            // Caller correctly does NOT reset counter after a Merchant Rare purchase —
            // only combat-reward Rare drafts reset the pity counter.
            int counterAfterMerchantPurchase = 8;

            var result = algo.Generate(
                ChassisType.Scout, 1, counterAfterMerchantPurchase, 1, EmptyDeck, Rng(42));

            Assert.AreEqual(CardRarity.Rare, result[0].Rarity,
                "Algorithm fires pity when counter=8 regardless of Merchant context — " +
                "the algorithm is agnostic to Rare source. " +
                "Merchant Rare purchase counter management is LootStateDTO's sole responsibility.");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-7: Innate cap at 3 — Innate cards excluded from offer
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_Generate_InnateCap3_DeckHas3InnateEntries_InnateExcludedFromOffer()
        {
            // Pool: 2 Innate Common + 5 non-Innate Common + 2 Uncommon + 1 Rare (all Scout)
            // Deck: 3 Innate card entries → InnateCap (3) is hit → Innate cards excluded from pool
            var catalog = new PityInnatePoolCatalogStub();
            var algo    = Make(catalog, new RewardGddWeightsStub());
            var rng     = Rng(42);

            // 3 Innate entries: 2 copies of innate_001 + 1 copy of innate_002 = count 3
            var deck = new List<string>
            {
                "scout_innate_001",
                "scout_innate_001",
                "scout_innate_002",
            };

            for (int i = 0; i < 100; i++)
            {
                var result = algo.Generate(ChassisType.Scout, 1, 0, 1, deck, rng);
                foreach (var draft in result)
                    Assert.IsFalse(draft.CardId.StartsWith("scout_innate_"),
                        $"Trial {i}: Innate card '{draft.CardId}' must not appear when InnateCap=3 is reached");
            }
        }

        [Test]
        public void test_Generate_InnateCap3_TwoSlotOffer_NeitherSlotContainsInnate()
        {
            var catalog = new PityInnatePoolCatalogStub();
            var algo    = Make(catalog, new RewardUniformWeightsStub());
            var rng     = Rng(42);

            var deck = new List<string> { "scout_innate_001", "scout_innate_002", "scout_innate_001" };

            for (int i = 0; i < 50; i++)
            {
                var result = algo.Generate(ChassisType.Scout, 1, 0, 2, deck, rng);
                foreach (var draft in result)
                    Assert.IsFalse(draft.CardId.StartsWith("scout_innate_"),
                        $"Trial {i}, draft '{draft.CardId}': no Innate card may appear when deck has 3 Innate (InnateCap=3)");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-8: Innate cap at 2 — Innate cards appear normally
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_Generate_InnateCap2_InnateCardsEligibleForOffer()
        {
            // Deck has 2 Innate entries (below InnateCap=3) — Innate cards must remain in the pool.
            // Uniform weights maximise the chance of drawing an Innate card over 200 draws.
            var catalog = new PityInnatePoolCatalogStub();
            var algo    = Make(catalog, new RewardUniformWeightsStub());
            var rng     = Rng(42);

            var deck = new List<string> { "scout_innate_001", "scout_innate_002" };  // 2 Innate = below cap

            int innateCount = 0;
            for (int i = 0; i < 200; i++)
            {
                var r = algo.Generate(ChassisType.Scout, 1, 0, 1, deck, rng);
                if (r.Length > 0 && r[0].CardId.StartsWith("scout_innate_"))
                    innateCount++;
            }

            Assert.Greater(innateCount, 0,
                $"With 2 Innate in deck (below InnateCap=3), Innate cards must be eligible for offers. " +
                $"Got {innateCount}/200 draws with an Innate card — expected at least 1.");
        }

        [Test]
        public void test_Generate_InnateCap0_InnateCardsFullyEligible()
        {
            // Empty deck = no Innate in deck → all Innate cards are eligible.
            var catalog = new PityInnatePoolCatalogStub();
            var algo    = Make(catalog, new RewardUniformWeightsStub());
            var rng     = Rng(42);

            int innateCount = 0;
            for (int i = 0; i < 200; i++)
            {
                var r = algo.Generate(ChassisType.Scout, 1, 0, 1, EmptyDeck, rng);
                if (r.Length > 0 && r[0].CardId.StartsWith("scout_innate_"))
                    innateCount++;
            }

            Assert.Greater(innateCount, 0,
                $"With 0 Innate in deck, all Innate cards must be eligible. " +
                $"Got {innateCount}/200 draws with an Innate card.");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-9: Free purge valve — determinism
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_FreeValveComputer_SameInputs_AlwaysReturnsSameValue()
        {
            var computer = new FreeValveComputer();

            bool first  = computer.Compute(runSeed: 1, nodeIndex: 3);
            bool second = computer.Compute(runSeed: 1, nodeIndex: 3);
            bool third  = computer.Compute(runSeed: 1, nodeIndex: 3);

            Assert.AreEqual(first, second, "Same runSeed+nodeIndex must produce same result (call 1 vs 2)");
            Assert.AreEqual(first, third,  "Same runSeed+nodeIndex must produce same result (call 1 vs 3)");
        }

        [Test]
        public void test_FreeValveComputer_DifferentInputs_YieldMixedResults()
        {
            // Different (runSeed, nodeIndex) pairs must not all produce the same value —
            // the distribution must be mixed, not constant.
            var computer = new FreeValveComputer();

            int trueCount = 0;
            for (int nodeIndex = 0; nodeIndex < 50; nodeIndex++)
                if (computer.Compute(runSeed: 777, nodeIndex: nodeIndex)) trueCount++;

            Assert.Greater(trueCount,       0, "Some nodeIndexes must yield valve=true");
            Assert.Greater(50 - trueCount,  0, "Some nodeIndexes must yield valve=false");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-10 (overflow safety): Free purge valve — unchecked arithmetic
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_FreeValveComputer_Int32MaxValueRunSeed_NoOverflowException()
        {
            var computer = new FreeValveComputer();

            Assert.DoesNotThrow(
                () => computer.Compute(runSeed: int.MaxValue, nodeIndex: 1),
                "runSeed=Int32.MaxValue ^ nodeIndex=1 must not throw — XOR cannot overflow by definition");
        }

        [Test]
        public void test_FreeValveComputer_BothMaxInt32_NoOverflowException()
        {
            var computer = new FreeValveComputer();

            Assert.DoesNotThrow(
                () => computer.Compute(runSeed: int.MaxValue, nodeIndex: int.MaxValue),
                "runSeed=Int32.MaxValue ^ nodeIndex=Int32.MaxValue must not throw — XOR produces 0, cannot overflow");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-11: Free purge valve — in-session re-entry (cache)
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_FreeValveComputer_SameNodeReenteredSameSession_ReturnsCachedValue()
        {
            // Entering the same Chopshop node twice within a session must return the same
            // IsFreeValveApplied value. The value is computed once and cached — not re-rolled.
            var computer = new FreeValveComputer();

            bool firstEntry  = computer.Compute(runSeed: 42, nodeIndex: 7);
            bool secondEntry = computer.Compute(runSeed: 42, nodeIndex: 7);

            Assert.AreEqual(firstEntry, secondEntry,
                "Re-entering the same Chopshop node (same runSeed + nodeIndex) within a session " +
                "must return the cached valve value — the valve is not re-rolled on re-entry.");
        }

        [Test]
        public void test_FreeValveComputer_DifferentNodes_EachCachedIndependently()
        {
            // Each (runSeed, nodeIndex) pair has its own independent cached value.
            var computer = new FreeValveComputer();

            bool node3 = computer.Compute(runSeed: 42, nodeIndex: 3);
            bool node4 = computer.Compute(runSeed: 42, nodeIndex: 4);

            // Re-entry must return the original cached value for each node independently.
            Assert.AreEqual(node3, computer.Compute(runSeed: 42, nodeIndex: 3),
                "node 3 re-entry must return cached value");
            Assert.AreEqual(node4, computer.Compute(runSeed: 42, nodeIndex: 4),
                "node 4 re-entry must return cached value");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-12: Free purge valve — distribution ~33%
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_FreeValveComputer_10kEntries_ValveDistributionIn30To36Percent()
        {
            // 10,000 simulated Chopshop entries via unique (runSeed=0, nodeIndex=i) pairs.
            // Unique pairs avoid cache hits — each call exercises the RNG path once.
            // Must produce the same count every run (deterministic seed enumeration).
            const int entries = 10_000;
            var computer = new FreeValveComputer();

            int trueCount = 0;
            for (int i = 0; i < entries; i++)
                if (computer.Compute(runSeed: 0, nodeIndex: i))
                    trueCount++;

            double rate = (double)trueCount / entries;
            Assert.GreaterOrEqual(rate, 0.30, $"Valve true rate {rate:P2} below 30% minimum (expected ~33%)");
            Assert.LessOrEqual(rate,   0.36, $"Valve true rate {rate:P2} above 36% maximum (expected ~33%)");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-13 (deferred): Cross-story dependency — persistence round-trip
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        [Ignore("DEFERRED: requires Story 008 (IsFreeValveApplied save DTO) — full round-trip " +
                "compute→persist→reload→assert cannot be validated until IsFreeValveApplied " +
                "ownership is resolved in the save schema.")]
        public void test_FreeValveComputer_PersistAndReload_ReturnsSameValueAfterReload()
        {
            // Placeholder: Story 006 + Story 008 together must satisfy the persistence round-trip
            // before the card-system epic DoD is signed off.
            Assert.Fail("Implement after Story 008 is Complete.");
        }
    }

    // ==========================================================================
    // Stubs specific to Story 006 — Innate cap tests
    // ==========================================================================

    /// <summary>
    /// Innate cap test pool: 2 Innate Common + 5 non-Innate Common + 2 Uncommon + 1 Rare (all Scout).
    /// Innate card IDs: "scout_innate_001", "scout_innate_002".
    /// </summary>
    internal sealed class PityInnatePoolCatalogStub : ICardCatalog
    {
        private readonly List<ICardData> _cards = Build();

        private static List<ICardData> Build()
        {
            var pool = new List<ICardData>();

            pool.Add(new RewardMockCardData
            {
                CardId   = "scout_innate_001",
                Rarity   = CardRarity.Common,
                Keywords = CardKeyword.Innate,
                Family   = CardFamily.Precision
            });
            pool.Add(new RewardMockCardData
            {
                CardId   = "scout_innate_002",
                Rarity   = CardRarity.Common,
                Keywords = CardKeyword.Innate,
                Family   = CardFamily.Precision
            });

            for (int i = 1; i <= 5; i++)
                pool.Add(new RewardMockCardData
                {
                    CardId   = $"scout_common_{i:D3}",
                    Rarity   = CardRarity.Common,
                    Keywords = CardKeyword.None,
                    Family   = CardFamily.Assault
                });

            for (int i = 1; i <= 2; i++)
                pool.Add(new RewardMockCardData
                {
                    CardId   = $"scout_uncommon_{i:D3}",
                    Rarity   = CardRarity.Uncommon,
                    Keywords = CardKeyword.None,
                    Family   = CardFamily.Control
                });

            pool.Add(new RewardMockCardData
            {
                CardId   = "scout_rare_001",
                Rarity   = CardRarity.Rare,
                Keywords = CardKeyword.None,
                Family   = CardFamily.Repair
            });

            return pool;
        }

        public ICardData GetById(string cardId) => _cards.First(c => c.CardId == cardId);
        public IReadOnlyList<ICardData> GetByChassis(ChassisType _) => _cards;
        public IReadOnlyList<ICardData> GetByChassisAndRarity(ChassisType _, CardRarity rarity)
            => _cards.Where(c => c.Rarity == rarity).ToList();
    }
}

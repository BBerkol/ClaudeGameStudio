using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WastelandRun.Cards;
using WastelandRun.Vehicle;

// EditMode test — all types are engine-free (WastelandRun.Cards has noEngineReferences: true).
// No UnityEngine imports needed or allowed.
namespace WastelandRun.Tests.Unit.CardSystem
{
    [TestFixture]
    public class RewardDrawAlgorithmTest
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
        // AC-1: No UnityEngine.Random in draw path
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_RewardDrawAlgorithm_Assembly_ContainsNoUnityEngineReference()
        {
            var assembly   = typeof(RewardDrawAlgorithm).Assembly;
            var referenced = assembly.GetReferencedAssemblies();
            foreach (var name in referenced)
                Assert.IsFalse(
                    name.Name!.StartsWith("UnityEngine", StringComparison.Ordinal),
                    $"WastelandRun.Cards must not reference UnityEngine — found: {name.Name}");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-2: Rarity draw weight simulation
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_Generate_Mastery1_100kDraws_RareRateIn007To013()
        {
            const int draws = 100_000;
            var algo    = Make(new RewardFullPoolCatalogStub(), new RewardGddWeightsStub());
            var catalog = new RewardFullPoolCatalogStub();
            // EmptyDeck means copy limits never fire — every Generate() call consumes exactly
            // 2 rng.Next() calls, keeping RNG cadence uniform across all draws.
            Assert.GreaterOrEqual(
                catalog.GetByChassis(ChassisType.Scout).Count(c => c.Rarity == CardRarity.Rare), 2,
                "Stub must have ≥2 Rare cards to ensure cadence is uniform across 100k draws");
            var rng  = Rng(42);

            int rareCount = 0;
            for (int i = 0; i < draws; i++)
            {
                var r = algo.Generate(ChassisType.Scout, 1, 0, 1, EmptyDeck, rng);
                if (r.Length > 0 && r[0].Rarity == CardRarity.Rare) rareCount++;
            }

            double rate = (double)rareCount / draws;
            Assert.GreaterOrEqual(rate, 0.007, $"Rare rate {rate:P3} below minimum 0.7%");
            Assert.LessOrEqual(rate, 0.013, $"Rare rate {rate:P3} above maximum 1.3%");
        }

        [Test]
        public void test_Generate_Mastery5_10kDraws_RareRateIn044To056()
        {
            const int draws = 10_000;
            var algo    = Make(new RewardFullPoolCatalogStub(), new RewardGddWeightsStub());
            var catalog = new RewardFullPoolCatalogStub();
            Assert.GreaterOrEqual(
                catalog.GetByChassis(ChassisType.Scout).Count(c => c.Rarity == CardRarity.Rare), 2,
                "Stub must have ≥2 Rare cards to ensure uniform RNG cadence");
            var rng  = Rng(42);

            int rareCount = 0;
            for (int i = 0; i < draws; i++)
            {
                var r = algo.Generate(ChassisType.Scout, 5, 0, 1, EmptyDeck, rng);
                if (r.Length > 0 && r[0].Rarity == CardRarity.Rare) rareCount++;
            }

            double rate = (double)rareCount / draws;
            Assert.GreaterOrEqual(rate, 0.044, $"Mastery 5 Rare rate {rate:P3} below 4.4%");
            Assert.LessOrEqual(rate, 0.056, $"Mastery 5 Rare rate {rate:P3} above 5.6%");
        }

        [Test]
        public void test_Generate_Mastery8_10kDraws_RareRateIn094To106()
        {
            const int draws = 10_000;
            var algo    = Make(new RewardFullPoolCatalogStub(), new RewardGddWeightsStub());
            var catalog = new RewardFullPoolCatalogStub();
            Assert.GreaterOrEqual(
                catalog.GetByChassis(ChassisType.Scout).Count(c => c.Rarity == CardRarity.Rare), 2,
                "Stub must have ≥2 Rare cards to ensure uniform RNG cadence");
            var rng  = Rng(42);

            int rareCount = 0;
            for (int i = 0; i < draws; i++)
            {
                var r = algo.Generate(ChassisType.Scout, 8, 0, 1, EmptyDeck, rng);
                if (r.Length > 0 && r[0].Rarity == CardRarity.Rare) rareCount++;
            }

            double rate = (double)rareCount / draws;
            Assert.GreaterOrEqual(rate, 0.094, $"Mastery 8 Rare rate {rate:P3} below 9.4%");
            Assert.LessOrEqual(rate, 0.106, $"Mastery 8 Rare rate {rate:P3} above 10.6%");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-3: Pool exhaustion fallback
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_Generate_RaresAtCopyLimit_DegradesToUncommonOrCommon_NoRareReturned()
        {
            var catalog = new RewardFullPoolCatalogStub();
            var algo    = Make(catalog, new RewardGddWeightsStub());
            var rng     = Rng(42);
            // Deck holds 1 copy of every Rare (copy limit = 1)
            var deck = catalog.GetByChassis(ChassisType.Scout)
                              .Where(c => c.Rarity == CardRarity.Rare)
                              .Select(c => c.CardId)
                              .ToList();

            for (int i = 0; i < 500; i++)
            {
                var r = algo.Generate(ChassisType.Scout, 1, 0, 1, deck, rng);
                if (r.Length > 0)
                    Assert.AreNotEqual(CardRarity.Rare, r[0].Rarity,
                        $"Trial {i}: Rare must not be returned when all Rares are at copy limit");
            }
        }

        [Test]
        public void test_Generate_AllTiersExhausted_ReturnsEmptyArrayNotNull()
        {
            var catalog = new RewardSmallPoolCatalogStub();  // 1 Common, 1 Uncommon, 1 Rare
            var algo    = Make(catalog);
            Assert.AreEqual(3, catalog.GetByChassis(ChassisType.Scout).Count,
                "RewardSmallPoolCatalogStub must contain exactly 3 cards (1 per rarity tier) for this exhaustion test to be valid");
            var deck    = new List<string>();
            deck.AddRange(Enumerable.Repeat("scout_common_001",   3));
            deck.AddRange(Enumerable.Repeat("scout_uncommon_001", 3));
            deck.Add("scout_rare_001");

            var result = algo.Generate(ChassisType.Scout, 1, 0, 1, deck, Rng(42));

            Assert.IsNotNull(result, "Must return empty array, never null");
            Assert.AreEqual(0, result.Length, "Fully exhausted pool must return empty array");
        }

        [Test]
        public void test_Generate_DrawCount2_Slot2PoolExhaustedAfterSlot1_ReturnsEmptyNotPartial()
        {
            // Pool has exactly 1 eligible card — Slot 2 finds nothing after Slot 1 draws it
            var catalog = new RewardSmallPoolCatalogStub();
            var algo    = Make(catalog);
            // Cap Uncommon and Rare — only 1 Common remains eligible
            var deck = new List<string>();
            deck.AddRange(Enumerable.Repeat("scout_uncommon_001", 3));
            deck.Add("scout_rare_001");

            var result = algo.Generate(ChassisType.Scout, 1, 0, 2, deck, Rng(42));

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length,
                "When Slot 2 pool empties after Slot 1 draw, must return [] — never a partial array");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Pity counter handling (algorithm Step 1 + Step 5)
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_Generate_PityCounter8_Slot1_AlwaysDrawsRare()
        {
            var algo = Make(new RewardFullPoolCatalogStub(), new RewardGddWeightsStub());
            var rng  = Rng(42);

            for (int i = 0; i < 50; i++)
            {
                var r = algo.Generate(ChassisType.Scout, 1, rarePityCounter: 8, 1, EmptyDeck, rng);
                Assert.AreEqual(1, r.Length, $"Trial {i}: pity should produce 1 draft");
                Assert.AreEqual(CardRarity.Rare, r[0].Rarity,
                    $"Trial {i}: rarePityCounter=8 must force a Rare draw");
            }
        }

        [Test]
        public void test_Generate_PityCounter8_RarePoolEmpty_ReturnsEmptyArray()
        {
            var catalog = new RewardSmallPoolCatalogStub();
            var algo    = Make(catalog);
            // Cap the only Rare
            var deck = new List<string> { "scout_rare_001" };

            var result = algo.Generate(ChassisType.Scout, 1, rarePityCounter: 8, 1, deck, Rng(42));

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length,
                "Pity fires but Rare pool is empty — must return [] (Scrap compensation signal)");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-4: Primary-family bias
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_Generate_Mastery1Slot1_PrimaryFamilyBias_AtLeast98PercentPrimaryFamily()
        {
            const int draws = 10_000;
            // Catalog: 4 Precision + 4 Maneuver + 4 Assault (all Common)
            // Bias: primaryFamilies = [Precision, Maneuver]
            var algo = Make(new RewardBiasTestCatalogStub(), new RewardBiasWeightsStub());
            var rng  = Rng(42);

            int primaryCount = 0;
            for (int i = 0; i < draws; i++)
            {
                var r = algo.Generate(ChassisType.Scout, 1, 0, 1, EmptyDeck, rng);
                if (r.Length > 0 &&
                    (r[0].Family == CardFamily.Precision || r[0].Family == CardFamily.Maneuver))
                    primaryCount++;
            }

            double rate = (double)primaryCount / draws;
            Assert.GreaterOrEqual(rate, 0.98,
                $"Slot 1 primary-family rate {rate:P2} must be ≥ 98% at Mastery 1–3");
        }

        [Test]
        public void test_Generate_Mastery4_NoBias_NonPrimaryFamilyAppears()
        {
            // At Mastery 4+ bias is disabled — Assault family must appear in draws
            const int draws = 2_000;
            var algo = Make(new RewardBiasTestCatalogStub(), new RewardBiasWeightsStub());
            var rng  = Rng(42);

            int assaultCount = 0;
            for (int i = 0; i < draws; i++)
            {
                var r = algo.Generate(ChassisType.Scout, 4, 0, 1, EmptyDeck, rng);
                if (r.Length > 0 && r[0].Family == CardFamily.Assault) assaultCount++;
            }

            Assert.Greater(assaultCount, 0,
                "Mastery 4 must draw from the full pool — Assault family must appear");
        }

        [Test]
        public void test_Generate_Mastery1_PrimaryFamilyPoolFullyExhausted_FallsBackNoException()
        {
            var catalog = new RewardBiasTestCatalogStub();
            var algo    = Make(catalog, new RewardBiasWeightsStub());
            // Exhaust all Precision and Maneuver cards (copy limit 3 each, all Common)
            var deck = new List<string>();
            foreach (var card in catalog.GetByChassis(ChassisType.Scout)
                                        .Where(c => c.Family == CardFamily.Precision
                                                 || c.Family == CardFamily.Maneuver))
                deck.AddRange(Enumerable.Repeat(card.CardId, 3));

            CardDraft[] result = null;
            Assert.DoesNotThrow(
                () => result = algo.Generate(ChassisType.Scout, 1, 0, 1, deck, Rng(42)),
                "Exhausted primary-family pool must fall back to full pool without throwing");
            Assert.IsNotNull(result);
            // Assault family cards remain — should draw from them
            if (result.Length > 0)
                Assert.AreEqual(CardFamily.Assault, result[0].Family,
                    "Fallback draw must come from remaining Assault pool");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-5: Without-replacement in two-card offer
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_Generate_DrawCount2_Slot1AndSlot2_NeverReturnSameCardId()
        {
            const int trials = 1_000;
            var algo = Make(new RewardFullPoolCatalogStub());
            var rng  = Rng(42);

            for (int i = 0; i < trials; i++)
            {
                var r = algo.Generate(ChassisType.Scout, 1, 0, 2, EmptyDeck, rng);
                if (r.Length == 2)
                    Assert.AreNotEqual(r[0].CardId, r[1].CardId,
                        $"Trial {i}: Slot 1 and Slot 2 must never return the same CardId");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-6: CardDraft.RulesText is pre-templated (TR-card-003)
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_Generate_RulesText_TokensFullyResolved_NoPlaceholdersRemain()
        {
            var card = new RewardMockCardData
            {
                CardId              = "scout_damage_001",
                DescriptionTemplate = "Deal {damage} and restore {armor} armor.",
                Effects             = new ICardEffect[]
                {
                    new DamageEffect(4, 0, false, Array.Empty<ICardEffectCondition>()),
                    new RestoreArmorEffect(2)
                }
            };
            var algo   = Make(new RewardSingleCardCatalogStub(card));
            var result = algo.Generate(ChassisType.Scout, 1, 0, 1, EmptyDeck, Rng(42));

            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("Deal 4 and restore 2 armor.", result[0].RulesText);
            Assert.IsFalse(result[0].RulesText.Contains('{'),
                "RulesText must contain no { — all tokens must be resolved by TokenResolver");
        }

        [Test]
        public void test_Generate_RulesText_UnmatchedToken_ResolvesToQuestionMark_NotNull()
        {
            var card = new RewardMockCardData
            {
                CardId              = "scout_weird_001",
                DescriptionTemplate = "Does {unknown} thing.",
                Effects             = Array.Empty<ICardEffect>()
            };
            var algo   = Make(new RewardSingleCardCatalogStub(card));
            var result = algo.Generate(ChassisType.Scout, 1, 0, 1, EmptyDeck, Rng(42));

            Assert.AreEqual(1, result.Length);
            Assert.IsNotNull(result[0].RulesText);
            Assert.AreEqual("Does ? thing.", result[0].RulesText,
                "Unknown token must resolve to '?' — not null, not the raw token");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-7: Determinism regression
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_Generate_SameSeedSameInputs_ProducesIdenticalCardDraftOutput()
        {
            var algo = Make(new RewardFullPoolCatalogStub(), new RewardGddWeightsStub());
            var deck = new[] { "scout_common_001" };

            CardDraft[] Run()
            {
                var rng = Rng(42);
                return algo.Generate(ChassisType.Scout, 2, 0, 2, deck, rng);
            }

            var run1 = Run();
            var run2 = Run();

            Assert.AreEqual(run1.Length, run2.Length, "Output length must be identical across runs");
            for (int i = 0; i < run1.Length; i++)
            {
                Assert.AreEqual(run1[i].CardId,        run2[i].CardId,        $"[{i}].CardId must match");
                Assert.AreEqual(run1[i].Rarity,        run2[i].Rarity,        $"[{i}].Rarity must match");
                Assert.AreEqual(run1[i].RulesText,     run2[i].RulesText,     $"[{i}].RulesText must match");
                Assert.AreEqual(run1[i].SelectionHash, run2[i].SelectionHash, $"[{i}].SelectionHash must match");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Edge cases
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_Generate_DrawCountZero_ReturnsEmptyArrayImmediately()
        {
            var algo   = Make(new RewardFullPoolCatalogStub());
            var result = algo.Generate(ChassisType.Scout, 1, 0, 0, EmptyDeck, Rng(42));

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [Test]
        public void test_Generate_LegendaryCards_NeverSelectedInRewardDraw()
        {
            const int draws = 5_000;
            var algo = Make(new RewardLegendaryIncludedCatalogStub());
            var rng  = Rng(42);

            int legendaryCount = 0;
            for (int i = 0; i < draws; i++)
            {
                var r = algo.Generate(ChassisType.Scout, 1, 0, 1, EmptyDeck, rng);
                legendaryCount += r.Count(d => d.Rarity == CardRarity.Legendary);
            }

            Assert.AreEqual(0, legendaryCount,
                "Legendary cards must never appear in reward draws (excluded by LegendaryCopyLimit = 0)");
        }

        [Test]
        public void test_Generate_DrawCount2_FullPool_ReturnsTwoDrafts()
        {
            var algo   = Make(new RewardFullPoolCatalogStub());
            var result = algo.Generate(ChassisType.Scout, 1, 0, 2, EmptyDeck, Rng(42));

            Assert.AreEqual(2, result.Length,
                "With a full uncapped pool, drawCount=2 must return exactly 2 drafts");
        }
    }

    // ==========================================================================
    // Stubs — self-contained, no shared mutable state
    // ==========================================================================

    /// <summary>ICardData test double. Named Reward* to avoid collision with MockCardData in assembly-contracts_test.cs.</summary>
    internal sealed class RewardMockCardData : ICardData
    {
        public string CardId { get; init; } = "scout_mock_001";
        public string DisplayName { get; init; } = "Mock Card";
        public string DescriptionTemplate { get; set; } = "";
        public string FlavorText { get; init; } = "";
        public string CardArtKey { get; init; } = "mock_art";
        public ChassisType ChassisPool { get; init; } = ChassisType.Scout;
        public CardFamily Family { get; init; } = CardFamily.Precision;
        public CardRarity Rarity { get; init; } = CardRarity.Common;
        public bool IsStarterCard { get; init; } = false;
        public int EnergyCost { get; init; } = 1;
        public int MerchantPrice { get; init; } = 0;
        public CardTargetType TargetType { get; init; } = CardTargetType.EnemySubsystem;
        public IReadOnlyList<SlotType> ValidSubsystemTargets { get; init; } = Array.Empty<SlotType>();
        public PositionRequirement PositionRequirement { get; init; } = PositionRequirement.None;
        public CardKeyword Keywords { get; init; } = CardKeyword.None;
        public IReadOnlyList<ICardEffect> Effects { get; set; } = Array.Empty<ICardEffect>();
        public int BaseDamage { get; init; } = 0;
        public string? SourceSlotId { get; init; } = null;
    }

    // ── Weights stubs ──────────────────────────────────────────────────────────

    internal sealed class RewardUniformWeightsStub : IMasteryWeights
    {
        public (int Common, int Uncommon, int Rare) GetWeights(ChassisType chassis, int mastery)
            => (33, 33, 34);
        public IReadOnlyList<CardFamily> GetPrimaryFamilies(ChassisType chassis)
            => Array.Empty<CardFamily>();
    }

    /// <summary>GDD-accurate rarity weights by mastery tier.</summary>
    internal sealed class RewardGddWeightsStub : IMasteryWeights
    {
        public (int Common, int Uncommon, int Rare) GetWeights(ChassisType chassis, int mastery)
            => mastery switch
            {
                <= 3 => (90, 9, 1),
                <= 6 => (70, 25, 5),
                _    => (60, 30, 10)
            };
        public IReadOnlyList<CardFamily> GetPrimaryFamilies(ChassisType chassis)
            => Array.Empty<CardFamily>();
    }

    /// <summary>Bias weights: primary families = [Precision, Maneuver] for Scout.</summary>
    internal sealed class RewardBiasWeightsStub : IMasteryWeights
    {
        public (int Common, int Uncommon, int Rare) GetWeights(ChassisType chassis, int mastery)
            => (90, 9, 1);
        public IReadOnlyList<CardFamily> GetPrimaryFamilies(ChassisType chassis)
            => new[] { CardFamily.Precision, CardFamily.Maneuver };
    }

    // ── Catalog stubs ──────────────────────────────────────────────────────────

    /// <summary>10 Common + 5 Uncommon + 2 Rare, all Scout.</summary>
    internal sealed class RewardFullPoolCatalogStub : ICardCatalog
    {
        private readonly List<ICardData> _cards = Build();

        private static List<ICardData> Build()
        {
            var pool = new List<ICardData>();
            for (int i = 1; i <= 10; i++)
                pool.Add(new RewardMockCardData { CardId = $"scout_common_{i:D3}",   Rarity = CardRarity.Common });
            for (int i = 1; i <= 5; i++)
                pool.Add(new RewardMockCardData { CardId = $"scout_uncommon_{i:D3}", Rarity = CardRarity.Uncommon });
            for (int i = 1; i <= 2; i++)
                pool.Add(new RewardMockCardData { CardId = $"scout_rare_{i:D3}",     Rarity = CardRarity.Rare });
            return pool;
        }

        public ICardData GetById(string cardId) => _cards.First(c => c.CardId == cardId);
        public IReadOnlyList<ICardData> GetByChassis(ChassisType _) => _cards;
        public IReadOnlyList<ICardData> GetByChassisAndRarity(ChassisType _, CardRarity rarity)
            => _cards.Where(c => c.Rarity == rarity).ToList();
    }

    /// <summary>1 Common + 1 Uncommon + 1 Rare, all Scout.</summary>
    internal sealed class RewardSmallPoolCatalogStub : ICardCatalog
    {
        private readonly List<ICardData> _cards = new List<ICardData>
        {
            new RewardMockCardData { CardId = "scout_common_001",   Rarity = CardRarity.Common },
            new RewardMockCardData { CardId = "scout_uncommon_001", Rarity = CardRarity.Uncommon },
            new RewardMockCardData { CardId = "scout_rare_001",     Rarity = CardRarity.Rare }
        };

        public ICardData GetById(string cardId) => _cards.First(c => c.CardId == cardId);
        public IReadOnlyList<ICardData> GetByChassis(ChassisType _) => _cards;
        public IReadOnlyList<ICardData> GetByChassisAndRarity(ChassisType _, CardRarity rarity)
            => _cards.Where(c => c.Rarity == rarity).ToList();
    }

    /// <summary>4 Precision + 4 Maneuver + 4 Assault (all Common, Scout) for bias testing.</summary>
    internal sealed class RewardBiasTestCatalogStub : ICardCatalog
    {
        private readonly List<ICardData> _cards = Build();

        private static List<ICardData> Build()
        {
            var pool = new List<ICardData>();
            for (int i = 1; i <= 4; i++)
                pool.Add(new RewardMockCardData { CardId = $"scout_precision_{i:D3}", Rarity = CardRarity.Common, Family = CardFamily.Precision });
            for (int i = 1; i <= 4; i++)
                pool.Add(new RewardMockCardData { CardId = $"scout_maneuver_{i:D3}",  Rarity = CardRarity.Common, Family = CardFamily.Maneuver });
            for (int i = 1; i <= 4; i++)
                pool.Add(new RewardMockCardData { CardId = $"scout_assault_{i:D3}",   Rarity = CardRarity.Common, Family = CardFamily.Assault });
            return pool;
        }

        public ICardData GetById(string cardId) => _cards.First(c => c.CardId == cardId);
        public IReadOnlyList<ICardData> GetByChassis(ChassisType _) => _cards;
        public IReadOnlyList<ICardData> GetByChassisAndRarity(ChassisType _, CardRarity rarity)
            => _cards.Where(c => c.Rarity == rarity).ToList();
    }

    /// <summary>One card — for token resolution tests.</summary>
    internal sealed class RewardSingleCardCatalogStub : ICardCatalog
    {
        private readonly ICardData _card;
        public RewardSingleCardCatalogStub(ICardData card) => _card = card;

        public ICardData GetById(string cardId) => _card;
        public IReadOnlyList<ICardData> GetByChassis(ChassisType _) => new[] { _card };
        public IReadOnlyList<ICardData> GetByChassisAndRarity(ChassisType _, CardRarity rarity)
            => _card.Rarity == rarity ? new[] { _card } : Array.Empty<ICardData>();
    }

    /// <summary>Pool including a Legendary — verifies it is never selected.</summary>
    internal sealed class RewardLegendaryIncludedCatalogStub : ICardCatalog
    {
        private readonly List<ICardData> _cards = new List<ICardData>
        {
            new RewardMockCardData { CardId = "scout_common_001",    Rarity = CardRarity.Common },
            new RewardMockCardData { CardId = "scout_uncommon_001",  Rarity = CardRarity.Uncommon },
            new RewardMockCardData { CardId = "scout_rare_001",      Rarity = CardRarity.Rare },
            new RewardMockCardData { CardId = "scout_legendary_001", Rarity = CardRarity.Legendary }
        };

        public ICardData GetById(string cardId) => _cards.First(c => c.CardId == cardId);
        public IReadOnlyList<ICardData> GetByChassis(ChassisType _) => _cards;
        public IReadOnlyList<ICardData> GetByChassisAndRarity(ChassisType _, CardRarity rarity)
            => _cards.Where(c => c.Rarity == rarity).ToList();
    }
}

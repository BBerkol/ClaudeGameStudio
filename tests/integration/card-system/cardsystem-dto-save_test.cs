using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using WastelandRun.Cards;
using WastelandRun.Vehicle;

// EditMode integration test — engine-free.
// Covers Story 008: CardSystemDTO serialization contract and CardSystemSaveAdapter load behaviour.
// CardSystemSaveAdapter is referenced here before it is written (test-first per coding-standards.md).
// These tests will compile once CardSystemSaveAdapter is added to WastelandRun.Cards.Services.
namespace WastelandRun.Cards.Tests
{
    /// <summary>
    /// Integration tests for CardSystemDTO JSON serialization and CardSystemSaveAdapter load logic.
    /// Each test is fully independent — no shared mutable state across test methods.
    /// </summary>
    [TestFixture]
    public class CardSystemDtoSaveTests
    {
        // ─── Test helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Minimal ICardData implementation for test use. All non-essential members
        /// return safe defaults. CardId is set at construction.
        /// </summary>
        private sealed class TestCard : ICardData
        {
            public string CardId { get; }
            public string DisplayName { get; } = "Test Card";
            public string DescriptionTemplate { get; } = "";
            public string FlavorText { get; } = "";
            public string CardArtKey { get; } = "";
            public ChassisType ChassisPool { get; } = ChassisType.Scout;
            public CardFamily Family { get; } = CardFamily.Precision;
            public CardRarity Rarity { get; } = CardRarity.Common;
            public bool IsStarterCard { get; } = false;
            public int EnergyCost { get; } = 1;
            public int MerchantPrice { get; } = 0;
            public CardTargetType TargetType { get; } = CardTargetType.EnemySubsystem;
            public IReadOnlyList<SlotType> ValidSubsystemTargets { get; } = Array.Empty<SlotType>();
            public PositionRequirement PositionRequirement { get; } = PositionRequirement.None;
            public CardKeyword Keywords => CardKeyword.None;
            public IReadOnlyList<ICardEffect> Effects { get; } = Array.Empty<ICardEffect>();
            public int BaseDamage { get; } = 0;
            public string? SourceSlotId { get; set; } = null;

            public TestCard(string id) => CardId = id;
        }

        /// <summary>
        /// Minimal ICardCatalog implementation backed by an in-memory dictionary.
        /// GetById throws KeyNotFoundException for unknown IDs, matching the contract
        /// the real catalog will enforce; the adapter must handle this gracefully.
        /// </summary>
        private sealed class TestCatalog : ICardCatalog
        {
            private readonly Dictionary<string, ICardData> _cards;

            public TestCatalog(IEnumerable<ICardData> cards)
                => _cards = cards.ToDictionary(c => c.CardId, c => c);

            public ICardData GetById(string cardId)
            {
                if (_cards.TryGetValue(cardId, out var card)) return card;
                throw new KeyNotFoundException($"TestCatalog: no card with id '{cardId}'");
            }

            public IReadOnlyList<ICardData> GetByChassis(ChassisType chassis)
                => _cards.Values.Where(c => c.ChassisPool == chassis).ToList();

            public IReadOnlyList<ICardData> GetByChassisAndRarity(ChassisType chassis, CardRarity rarity)
                => _cards.Values.Where(c => c.ChassisPool == chassis && c.Rarity == rarity).ToList();
        }

        // ─── AC-1 / AC-8: Constant values — SystemId and SchemaVersion ─────────────

        /// <summary>
        /// CardSystemDTO.SystemId must equal "card-system" and SchemaVersion must equal 1.
        /// These constants key the DTO into the save envelope routing (ADR-0004).
        /// A typo or increment here breaks save file compatibility for all shipped players.
        /// </summary>
        [Test]
        public void test_CardSystemDTO_Constants_SystemIdAndSchemaVersion_CorrectValues()
        {
            Assert.AreEqual("card-system", CardSystemDTO.SystemId,
                "SystemId must equal 'card-system' for save-system routing (ADR-0004)");
            Assert.AreEqual(1, CardSystemDTO.SchemaVersion,
                "SchemaVersion must equal 1 — increment only on breaking schema changes");
        }

        // ─── AC-1: Exact field count — 4 public instance properties, consts excluded ──

        /// <summary>
        /// CardSystemDTO must expose exactly 4 public instance properties: Deck, Discard, Exhausted,
        /// CardCopyCounts. Const fields (SystemId, SchemaVersion) and the synthesised EqualityContract
        /// from the sealed record must not be counted.
        /// </summary>
        [Test]
        public void test_CardSystemDTO_FourPublicInstanceProperties_ExcludesConsts()
        {
            // Arrange
            var properties = typeof(CardSystemDTO)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != "EqualityContract") // sealed record synthesises this as protected — guard for edge cases
                .ToArray();

            var names = properties.Select(p => p.Name).ToHashSet();

            // Assert — exact count
            Assert.AreEqual(4, properties.Length,
                $"CardSystemDTO must expose exactly 4 public instance properties, found {properties.Length}: " +
                string.Join(", ", properties.Select(p => p.Name)));

            // Assert — named fields present
            Assert.IsTrue(names.Contains("Deck"),           "Expected property 'Deck'");
            Assert.IsTrue(names.Contains("Discard"),        "Expected property 'Discard'");
            Assert.IsTrue(names.Contains("Exhausted"),      "Expected property 'Exhausted'");
            Assert.IsTrue(names.Contains("CardCopyCounts"), "Expected property 'CardCopyCounts'");

            // Assert — const fields are NOT surfaced as instance properties
            // (SystemId and SchemaVersion are const — GetProperties never returns them)
            Assert.IsFalse(names.Contains("SystemId"),      "'SystemId' is a const field — must not appear in GetProperties");
            Assert.IsFalse(names.Contains("SchemaVersion"), "'SchemaVersion' is a const field — must not appear in GetProperties");
        }

        // ─── AC-2: Round-trip — all fields byte-identical after JSON serialize/deserialize ─

        /// <summary>
        /// Serializing a fully-populated CardSystemDTO to JSON and back must produce a record
        /// where all four fields are value-identical to the original, including list order and
        /// dictionary contents. Underscore characters in CardId strings must not be altered.
        /// Verifies that Unity's bundled Newtonsoft.Json 13+ handles init-only properties on
        /// sealed records without requiring [JsonConstructor].
        /// </summary>
        [Test]
        public void test_CardSystemDTO_RoundTrip_NewtonsoftJson_AllFieldsByteIdentical()
        {
            // Arrange
            var original = new CardSystemDTO
            {
                Deck = new List<string>
                {
                    "scout_precision_001",
                    "scout_precision_001"
                },
                Discard = new List<string>
                {
                    "scout_assault_003",
                    "scout_maneuver_002",
                    "scout_repair_001",
                    "scout_control_004",
                    "scout_precision_007"
                },
                Exhausted = new List<string>
                {
                    "scout_rare_001",
                    "scout_rare_002"
                },
                CardCopyCounts = new Dictionary<string, int>
                {
                    { "scout_precision_001", 2 }
                }
            };

            // Act
            var json         = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<CardSystemDTO>(json);

            // Assert — non-null result
            Assert.IsNotNull(deserialized, "Deserialized DTO must not be null");

            // Assert — Deck: order and content identical
            Assert.AreEqual(original.Deck.Count, deserialized!.Deck.Count,
                "Deck list length must survive round-trip");
            for (int i = 0; i < original.Deck.Count; i++)
                Assert.AreEqual(original.Deck[i], deserialized.Deck[i],
                    $"Deck[{i}] must be byte-identical (underscore preservation check)");

            // Assert — Discard: order and content identical
            Assert.AreEqual(original.Discard.Count, deserialized.Discard.Count,
                "Discard list length must survive round-trip");
            for (int i = 0; i < original.Discard.Count; i++)
                Assert.AreEqual(original.Discard[i], deserialized.Discard[i],
                    $"Discard[{i}] must be byte-identical");

            // Assert — Exhausted: order and content identical
            Assert.AreEqual(original.Exhausted.Count, deserialized.Exhausted.Count,
                "Exhausted list length must survive round-trip");
            for (int i = 0; i < original.Exhausted.Count; i++)
                Assert.AreEqual(original.Exhausted[i], deserialized.Exhausted[i],
                    $"Exhausted[{i}] must be byte-identical");

            // Assert — CardCopyCounts: dictionary contents identical
            Assert.AreEqual(original.CardCopyCounts.Count, deserialized.CardCopyCounts.Count,
                "CardCopyCounts entry count must survive round-trip");
            foreach (var kvp in original.CardCopyCounts)
            {
                Assert.IsTrue(deserialized.CardCopyCounts.ContainsKey(kvp.Key),
                    $"CardCopyCounts must contain key '{kvp.Key}' after round-trip");
                Assert.AreEqual(kvp.Value, deserialized.CardCopyCounts[kvp.Key],
                    $"CardCopyCounts['{kvp.Key}'] value must be identical after round-trip");
            }
        }

        // ─── AC-3: CardCopyCounts drift auto-corrected — no exception thrown ─────────

        /// <summary>
        /// When CardCopyCounts disagrees with the actual Deck list (drift), Load must:
        ///   1. Not throw any exception.
        ///   2. Auto-correct the in-memory deck to reflect the actual list contents.
        /// Also covers the edge case where a CardId present in Deck is entirely absent
        /// from CardCopyCounts — the adapter must derive the correct count from Deck itself.
        /// </summary>
        [Test]
        public void test_CardSystemSaveAdapter_Load_CardCopyCountsDrift_AutoCorrectedNoException()
        {
            // Arrange — DTO claims count=1 but Deck has 2 copies; CardCopyCounts is intentionally wrong
            var card = new TestCard("scout_precision_001");
            var catalog = new TestCatalog(new[] { card });

            var dto = new CardSystemDTO
            {
                Deck           = new List<string> { "scout_precision_001", "scout_precision_001" },
                Discard        = new List<string>(),
                Exhausted      = new List<string>(),
                // Drift: stored count says 1, but Deck has 2 entries
                CardCopyCounts = new Dictionary<string, int> { { "scout_precision_001", 1 } }
            };

            var adapter     = new CardSystemSaveAdapter();
            var deckManager = new DeckStateManager();

            // Act — must not throw despite count mismatch
            Assert.DoesNotThrow(() => adapter.Load(dto, catalog, deckManager),
                "Load must not throw when CardCopyCounts drifts from actual Deck contents");

            // Assert — deck state reflects the actual Deck list (2 copies), not the wrong stored count
            Assert.AreEqual(2, deckManager.Deck.Count,
                "After auto-correct, Deck must contain 2 copies matching the DTO Deck list");
            Assert.IsTrue(deckManager.Deck.All(c => c.CardId == "scout_precision_001"),
                "Both deck entries must have CardId 'scout_precision_001'");

            // Assert — empty zones are untouched
            Assert.AreEqual(0, deckManager.Discard.Count);
            Assert.AreEqual(0, deckManager.Exhausted.Count);
        }

        // ─── AC-4a: 5/3/2 zone distribution survives load ──────────────────────────

        /// <summary>
        /// A DTO with 5 Deck, 3 Discard, and 2 Exhausted cards must produce a DeckStateManager
        /// with exactly those counts in the corresponding zones after Load.
        /// </summary>
        [Test]
        public void test_CardSystemSaveAdapter_Load_FiveThreeTwo_DistributionSurvivesLoad()
        {
            // Arrange — 10 distinct cards covering all three zones
            var allCards = new[]
            {
                new TestCard("a"), new TestCard("b"), new TestCard("c"),
                new TestCard("d"), new TestCard("e"),  // Deck: 5
                new TestCard("f"), new TestCard("g"), new TestCard("h"),  // Discard: 3
                new TestCard("i"), new TestCard("j")   // Exhausted: 2
            };

            var catalog = new TestCatalog(allCards);

            var dto = new CardSystemDTO
            {
                Deck      = new List<string> { "a", "b", "c", "d", "e" },
                Discard   = new List<string> { "f", "g", "h" },
                Exhausted = new List<string> { "i", "j" },
                CardCopyCounts = new Dictionary<string, int>
                {
                    { "a", 1 }, { "b", 1 }, { "c", 1 }, { "d", 1 }, { "e", 1 }
                }
            };

            var adapter     = new CardSystemSaveAdapter();
            var deckManager = new DeckStateManager();

            // Act
            Assert.DoesNotThrow(() => adapter.Load(dto, catalog, deckManager));

            // Assert — zone counts match DTO
            Assert.AreEqual(5, deckManager.Deck.Count,
                "Deck must contain exactly 5 cards");
            Assert.AreEqual(3, deckManager.Discard.Count,
                "Discard must contain exactly 3 cards");
            Assert.AreEqual(2, deckManager.Exhausted.Count,
                "Exhausted must contain exactly 2 cards");

            // Assert — Hand is always empty at load time (ADR-0004)
            Assert.AreEqual(0, deckManager.Hand.Count,
                "Hand must be empty immediately after Load");
        }

        // ─── AC-4b: All three lists empty → all counts 0, no error ─────────────────

        /// <summary>
        /// Edge case: a DTO with all three zone lists empty must load without error and
        /// produce a DeckStateManager with all zone counts at zero.
        /// </summary>
        [Test]
        public void test_CardSystemSaveAdapter_Load_AllEmptyLists_AllCountsZeroNoError()
        {
            // Arrange — empty DTO; catalog has no cards but is not queried
            var catalog = new TestCatalog(Array.Empty<ICardData>());

            var dto = new CardSystemDTO
            {
                Deck           = new List<string>(),
                Discard        = new List<string>(),
                Exhausted      = new List<string>(),
                CardCopyCounts = new Dictionary<string, int>()
            };

            var adapter     = new CardSystemSaveAdapter();
            var deckManager = new DeckStateManager();

            // Act
            Assert.DoesNotThrow(() => adapter.Load(dto, catalog, deckManager),
                "Load must not throw for an all-empty DTO");

            // Assert — all zones are zero
            Assert.AreEqual(0, deckManager.Deck.Count,      "Deck must be empty");
            Assert.AreEqual(0, deckManager.Discard.Count,   "Discard must be empty");
            Assert.AreEqual(0, deckManager.Exhausted.Count, "Exhausted must be empty");
            Assert.AreEqual(0, deckManager.Hand.Count,      "Hand must be empty");
        }

        // ─── Save→Load round-trip: all zones preserved end-to-end ────────────────

        /// <summary>
        /// Full adapter round-trip: build a DeckStateManager with known zone contents,
        /// call Save() to capture a DTO, then Load() into a fresh manager and assert
        /// zone contents are identical. Exercises ExtractIds and CountIds paths.
        /// </summary>
        [Test]
        public void test_CardSystemSaveAdapter_SaveThenLoad_RoundTrip_AllZonesIdentical()
        {
            // Arrange — 4 distinct cards across three zones (hand is empty, as required by Save)
            var cardA = new TestCard("a");
            var cardB = new TestCard("b");
            var cardC = new TestCard("c");
            var cardD = new TestCard("d");

            var catalog      = new TestCatalog(new ICardData[] { cardA, cardB, cardC, cardD });
            var sourceDeck   = new DeckStateManager();
            sourceDeck.Load(
                new[] { cardA, cardB },       // Deck: 2
                new[] { cardC },              // Discard: 1
                new[] { cardD });             // Exhausted: 1

            var adapter = new CardSystemSaveAdapter();

            // Act — Save then Load into a fresh manager
            var dto         = adapter.Save(sourceDeck);
            var targetDeck  = new DeckStateManager();
            Assert.DoesNotThrow(() => adapter.Load(dto, catalog, targetDeck));

            // Assert — zone counts match original
            Assert.AreEqual(2, targetDeck.Deck.Count,      "Deck must contain 2 cards after round-trip");
            Assert.AreEqual(1, targetDeck.Discard.Count,   "Discard must contain 1 card after round-trip");
            Assert.AreEqual(1, targetDeck.Exhausted.Count, "Exhausted must contain 1 card after round-trip");
            Assert.AreEqual(0, targetDeck.Hand.Count,      "Hand must be empty after Load");

            // Assert — CardIds are preserved
            Assert.IsTrue(targetDeck.Deck.Any(c => c.CardId == "a"),      "CardId 'a' must survive round-trip");
            Assert.IsTrue(targetDeck.Deck.Any(c => c.CardId == "b"),      "CardId 'b' must survive round-trip");
            Assert.IsTrue(targetDeck.Discard.Any(c => c.CardId == "c"),   "CardId 'c' must survive round-trip");
            Assert.IsTrue(targetDeck.Exhausted.Any(c => c.CardId == "d"), "CardId 'd' must survive round-trip");

            // Assert — CardCopyCounts computed correctly
            Assert.AreEqual(1, dto.CardCopyCounts["a"], "CardCopyCounts['a'] must be 1");
            Assert.AreEqual(1, dto.CardCopyCounts["b"], "CardCopyCounts['b'] must be 1");
        }

        // ─── Load: null zone list → ArgumentException ────────────────────────────

        /// <summary>
        /// A DTO with a null zone list (corrupt or pre-schema save file) must throw
        /// ArgumentException rather than NullReferenceException, so the orchestrator
        /// can distinguish a schema migration failure from a code bug.
        /// </summary>
        [Test]
        public void test_CardSystemSaveAdapter_Load_NullDeckList_ThrowsArgumentException()
        {
            // Arrange — Deck is null (simulates missing field in corrupt JSON)
            var catalog = new TestCatalog(Array.Empty<ICardData>());
            var dto = new CardSystemDTO
            {
                Deck           = null,
                Discard        = new List<string>(),
                Exhausted      = new List<string>(),
                CardCopyCounts = new Dictionary<string, int>()
            };

            var adapter     = new CardSystemSaveAdapter();
            var deckManager = new DeckStateManager();

            // Act + Assert — must throw ArgumentException, not NullReferenceException
            Assert.Throws<ArgumentException>(() => adapter.Load(dto, catalog, deckManager),
                "Load must throw ArgumentException when dto.Deck is null");
        }
    }
}

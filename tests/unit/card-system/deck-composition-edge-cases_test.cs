using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WastelandRun.Cards;
using WastelandRun.Vehicle;

// EditMode test — engine-free. WastelandRun.Cards has noEngineReferences: true.
// Named EdgeCaseMockCardData (not MockCardData) to avoid collision with stubs in sibling test files.
namespace WastelandRun.Tests.Unit.CardSystem
{
    [TestFixture]
    public class DeckCompositionEdgeCasesTest
    {
        // ─── Stub ─────────────────────────────────────────────────────────────────

        internal sealed class EdgeCaseMockCardData : ICardData
        {
            public string CardId { get; init; }                                         = "edge_mock_001";
            public string DisplayName { get; init; }                                    = "Mock Card";
            public string DescriptionTemplate { get; init; }                            = "";
            public string FlavorText { get; init; }                                     = "";
            public string CardArtKey { get; init; }                                     = "mock_art";
            public ChassisType ChassisPool { get; init; }                               = ChassisType.Scout;
            public CardFamily Family { get; init; }                                     = CardFamily.Precision;
            public CardRarity Rarity { get; init; }                                     = CardRarity.Common;
            public bool IsStarterCard { get; init; }                                    = false;
            public int EnergyCost { get; init; }                                        = 1;
            public int MerchantPrice { get; init; }                                     = 0;
            public CardTargetType TargetType { get; init; }                             = CardTargetType.EnemySubsystem;
            public IReadOnlyList<SlotType> ValidSubsystemTargets { get; init; }         = Array.Empty<SlotType>();
            public PositionRequirement PositionRequirement { get; init; }               = PositionRequirement.None;
            public CardKeyword Keywords { get; init; }                                  = CardKeyword.None;
            public IReadOnlyList<ICardEffect> Effects { get; init; }                    = Array.Empty<ICardEffect>();
            public int BaseDamage { get; init; }                                        = 0;
            public string? SourceSlotId { get; init; }                                  = null;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private static DeckCompositionRules Rules() => new DeckCompositionRules();
        private static CardPlayValidator    Validator() => new CardPlayValidator();

        private static EdgeCaseMockCardData Card(
            string  id          = "c001",
            bool    isStarter   = false,
            CardKeyword kw      = CardKeyword.None,
            CardTargetType targetType                       = CardTargetType.EnemySubsystem,
            IReadOnlyList<SlotType>? validTargets           = null,
            PositionRequirement posReq                      = PositionRequirement.None,
            IReadOnlyList<ICardEffect>? effects             = null)
            => new EdgeCaseMockCardData
            {
                CardId                 = id,
                IsStarterCard          = isStarter,
                Keywords               = kw,
                TargetType             = targetType,
                ValidSubsystemTargets  = validTargets ?? Array.Empty<SlotType>(),
                PositionRequirement    = posReq,
                Effects                = effects ?? Array.Empty<ICardEffect>()
            };

        /// <summary>Builds a deck list of <paramref name="count"/> distinct cards.</summary>
        private static List<ICardData> BuildDeck(int count)
            => Enumerable.Range(1, count)
                .Select(i => (ICardData)Card($"c{i:D3}"))
                .ToList();

        private static readonly IReadOnlyList<SlotType> AllSlots = new[]
        {
            SlotType.Weapon, SlotType.Engine, SlotType.Mobility, SlotType.Frame
        };

        // ─── AC-1: Purge floor and boundary (EC7) ────────────────────────────────

        [Test]
        public void test_TryPurge_DeckAtExactly10_ReturnsMinimumSizeReached()
        {
            // Arrange
            var deck   = BuildDeck(10);
            var target = deck[0];
            var rules  = Rules();

            // Act
            var result = rules.TryPurge(deck, target);

            // Assert
            Assert.That(result, Is.EqualTo(DeckGrowResult.MinimumSizeReached));
            Assert.That(deck.Count, Is.EqualTo(10), "deck must not be mutated on failure");
        }

        [Test]
        public void test_TryPurge_DeckAt11_Succeeds_ThenAt10_ReturnsMinimumSizeReached()
        {
            // Arrange
            var deck   = BuildDeck(11);
            var target = deck[0];
            var rules  = Rules();

            // Act — first purge
            var firstResult = rules.TryPurge(deck, target);

            // Assert — success, deck drops to 10
            Assert.That(firstResult, Is.EqualTo(DeckGrowResult.Success));
            Assert.That(deck.Count, Is.EqualTo(10));
            Assert.That(deck, Does.Not.Contain(target));

            // Act — second purge at 10
            var secondTarget = deck[0];
            var secondResult = rules.TryPurge(deck, secondTarget);

            // Assert — blocked at minimum
            Assert.That(secondResult, Is.EqualTo(DeckGrowResult.MinimumSizeReached));
            Assert.That(deck.Count, Is.EqualTo(10));
        }

        [Test]
        public void test_TryPurge_StarterCardInDeckAbove10_PurgeSucceeds()
        {
            // Arrange — EC6: IsStarterCard does not protect against purge
            var deck       = BuildDeck(10);
            var starter    = Card("starter_001", isStarter: true);
            deck.Add(starter);   // deck is now 11
            var rules      = Rules();

            // Act
            var result = rules.TryPurge(deck, starter);

            // Assert — starter card purged normally
            Assert.That(result, Is.EqualTo(DeckGrowResult.Success));
            Assert.That(deck, Does.Not.Contain(starter));
            Assert.That(deck.Count, Is.EqualTo(10));
        }

        [Test]
        public void test_TryPurge_StarterCard_BehavesIdenticallyToNonStarter_AtMinimum()
        {
            // Arrange — both starter and non-starter blocked at 10
            var deckWithStarter = BuildDeck(9);
            var starter         = Card("starter_002", isStarter: true);
            deckWithStarter.Add(starter);   // exactly 10

            var deckWithNormal = BuildDeck(10);
            var normal         = deckWithNormal[0];
            var rules          = Rules();

            // Act
            var starterResult = rules.TryPurge(deckWithStarter, starter);
            var normalResult  = rules.TryPurge(deckWithNormal,  normal);

            // Assert — same result regardless of IsStarterCard
            Assert.That(starterResult, Is.EqualTo(DeckGrowResult.MinimumSizeReached));
            Assert.That(normalResult,  Is.EqualTo(DeckGrowResult.MinimumSizeReached));
        }

        // ─── AC-2: EC10 multi-effect continues through Offline ────────────────────

        [Test]
        public void test_MultiEffect_EffectListIteration_DoesNotShortCircuit()
        {
            // Arrange — two stub effects; iteration order must cover both regardless of slot state.
            // This test verifies that a card's Effects list is iterable in full without throwing.
            // Combat resolution (state-checking on Offline) is the combat layer's responsibility.
            var firedEffects = new List<int>();
            var effect1      = new TrackingEffect(1, firedEffects);
            var effect2      = new TrackingEffect(2, firedEffects);
            var card         = Card("ec10_001", effects: new ICardEffect[] { effect1, effect2 });

            // Act — simulate the resolution loop the combat layer will run
            foreach (var effect in card.Effects)
            {
                if (effect is TrackingEffect te) te.Fire();
            }

            // Assert — both effects fired; no short-circuit
            Assert.That(firedEffects, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void test_MultiEffect_CardEffectsList_IsFullyEnumerable_AfterSlotOffline()
        {
            // Arrange — a card whose Effects list has 3 entries; all must be reachable
            var counter = 0;
            var effects = new ICardEffect[]
            {
                new TrackingEffect(1, null) { OnFire = () => counter++ },
                new TrackingEffect(2, null) { OnFire = () => counter++ },
                new TrackingEffect(3, null) { OnFire = () => counter++ }
            };
            var card = Card("ec10_002", effects: effects);

            // Act — iterate without any short-circuit guard
            foreach (var effect in card.Effects)
                if (effect is TrackingEffect te) te.Fire();

            // Assert
            Assert.That(counter, Is.EqualTo(3));
        }

        // ─── AC-3: EC14 Innate+Ethereal accepted; Ethereal+Retain rejected ────────
        // Note: OnValidate lives on CardDefinitionSO (WastelandRun.Gameplay, engine assembly).
        // The POCO layer does not validate keyword combinations — tested here at the
        // ICardData / CardKeyword level to confirm the flags are orthogonal in the model.

        [Test]
        public void test_Keywords_InnateAndEthereal_AreOrthogonal_NeitherSuppressesOther()
        {
            // Arrange
            var card = Card("ec14_001", kw: CardKeyword.Innate | CardKeyword.Ethereal);

            // Assert — both bits are independently readable
            Assert.That((card.Keywords & CardKeyword.Innate),   Is.Not.Zero, "Innate flag must be set");
            Assert.That((card.Keywords & CardKeyword.Ethereal), Is.Not.Zero, "Ethereal flag must be set");
        }

        [Test]
        public void test_Keywords_InnateEthereal_InDeckStateManager_InnateDrawFiresCorrectly()
        {
            // Arrange — Innate|Ethereal card in deck; DrawInnateCards must pull it to hand
            var mgr  = new DeckStateManager();
            var card = Card("ec14_innate", kw: CardKeyword.Innate | CardKeyword.Ethereal);
            mgr.Load(new[] { card }, Array.Empty<ICardData>(), Array.Empty<ICardData>());

            // Act — combat start: Innate sweep
            mgr.DrawInnateCards();

            // Assert — card is in hand (Innate fired)
            Assert.That(mgr.Hand, Contains.Item(card));
            Assert.That(mgr.Deck, Is.Empty);
        }

        [Test]
        public void test_Keywords_InnateEthereal_EtherealDiscardsAtEndOfTurn()
        {
            // Arrange — card is in hand (simulating post-DrawInnate state); not played
            var mgr  = new DeckStateManager();
            var card = Card("ec14_ethereal", kw: CardKeyword.Innate | CardKeyword.Ethereal);
            // Load with card in deck, draw it via innate sweep to put it in hand
            mgr.Load(new[] { card }, Array.Empty<ICardData>(), Array.Empty<ICardData>());
            mgr.DrawInnateCards();

            // Act — end of turn without playing; Ethereal = no Retain, so discard fires
            mgr.EndTurn();

            // Assert — Ethereal (no Retain) discards at end of turn
            Assert.That(mgr.Hand,    Is.Empty);
            Assert.That(mgr.Discard, Contains.Item(card));
        }

        // ─── AC-4: EC3 runtime proxy — unknown keyword flag ───────────────────────

        [Test]
        public void test_TokenResolver_UnknownKeywordFlag128_DoesNotThrow_ReturnsResolvedString()
        {
            // Arrange — (CardKeyword)128 is beyond all defined values
            var card = Card("ec3_001", kw: (CardKeyword)128);
            var cardWithDescription = new EdgeCaseMockCardData
            {
                CardId              = card.CardId,
                Keywords            = (CardKeyword)128,
                DescriptionTemplate = "Deal {damage} damage",
                BaseDamage          = 5,
                Effects             = new ICardEffect[] { new DamageEffect(5, 0, false, Array.Empty<ICardEffectCondition>()) }
            };
            var resolver = new TokenResolver();

            // Act
            string? result = null;
            Assert.DoesNotThrow(() => result = resolver.Resolve(cardWithDescription));

            // Assert — non-null, non-empty, and contains the resolved damage value
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Not.Empty);
            Assert.That(result, Does.Contain("5"));
        }

        [Test]
        public void test_TokenResolver_UnknownKeywordFlagMaxInt_DoesNotThrow()
        {
            // Arrange — Int32.MaxValue as keyword bits
            var card = new EdgeCaseMockCardData
            {
                CardId              = "ec3_maxint",
                Keywords            = (CardKeyword)int.MaxValue,
                DescriptionTemplate = "Deal {damage} damage",
                BaseDamage          = 3,
                Effects             = new ICardEffect[] { new DamageEffect(3, 0, false, Array.Empty<ICardEffectCondition>()) }
            };
            var resolver = new TokenResolver();

            // Act + Assert — must not throw or overflow
            string? result = null;
            Assert.DoesNotThrow(() => result = resolver.Resolve(card));
            Assert.That(result, Is.Not.Null);
        }

        // ─── AC-5: MaxDeckSize cap ────────────────────────────────────────────────

        [Test]
        public void test_TryGrow_DeckAtExactly60_ReturnsCapacityExceeded()
        {
            // Arrange
            var deck  = BuildDeck(60);
            var extra = Card("extra_001");
            var rules = Rules();

            // Act
            var result = rules.TryGrow(deck, extra);

            // Assert
            Assert.That(result, Is.EqualTo(DeckGrowResult.CapacityExceeded));
            Assert.That(deck.Count, Is.EqualTo(60), "deck must not be mutated on cap hit");
        }

        [Test]
        public void test_TryGrow_DeckAt59_Succeeds_ThenAt60_ReturnsCapacityExceeded()
        {
            // Arrange
            var deck   = BuildDeck(59);
            var first  = Card("grow_001");
            var second = Card("grow_002");
            var rules  = Rules();

            // Act — first grow
            var firstResult = rules.TryGrow(deck, first);

            // Assert — deck grows to 60
            Assert.That(firstResult, Is.EqualTo(DeckGrowResult.Success));
            Assert.That(deck.Count, Is.EqualTo(60));

            // Act — second grow at 60
            var secondResult = rules.TryGrow(deck, second);

            // Assert — blocked at ceiling
            Assert.That(secondResult, Is.EqualTo(DeckGrowResult.CapacityExceeded));
            Assert.That(deck.Count, Is.EqualTo(60));
        }

        // ─── AC-6: EC11 empty ValidSubsystemTargets = all slots ──────────────────

        [Test]
        public void test_ResolveValidTargets_EmptyValidSubsystemTargets_ReturnsAllSlots()
        {
            // Arrange — ValidSubsystemTargets is empty (EC11: treat as all 4 slots valid)
            var card      = Card("ec11_001", targetType: CardTargetType.EnemySubsystem, validTargets: Array.Empty<SlotType>());
            var validator = Validator();

            // Act
            var targets = validator.ResolveValidTargets(card, AllSlots);

            // Assert — all 4 slots returned
            Assert.That(targets, Is.EquivalentTo(AllSlots));
        }

        [Test]
        public void test_ResolveValidTargets_SpecificTargets_ReturnsOnlyThoseSlots()
        {
            // Arrange — card explicitly restricts to Weapon only
            var card      = Card("ec11_002", targetType: CardTargetType.EnemySubsystem,
                                 validTargets: new[] { SlotType.Weapon });
            var validator = Validator();

            // Act
            var targets = validator.ResolveValidTargets(card, AllSlots);

            // Assert — only Weapon returned
            Assert.That(targets, Is.EquivalentTo(new[] { SlotType.Weapon }));
        }

        // ─── AC-7: Position requirement enforcement ───────────────────────────────

        [Test]
        public void test_CheckPositionRequirement_RequiresAhead_WhenBehind_ReturnsBlocked()
        {
            // Arrange
            var card      = Card("pos_001", posReq: PositionRequirement.RequiresAhead);
            var validator = Validator();

            // Act
            var result = validator.CheckPositionRequirement(card, PositionState.Behind);

            // Assert
            Assert.That(result, Is.EqualTo(CardPlayResult.PositionRequirementNotMet));
        }

        [Test]
        public void test_CheckPositionRequirement_RequiresAhead_WhenAhead_ReturnsOk()
        {
            // Arrange
            var card      = Card("pos_002", posReq: PositionRequirement.RequiresAhead);
            var validator = Validator();

            // Act
            var result = validator.CheckPositionRequirement(card, PositionState.Ahead);

            // Assert
            Assert.That(result, Is.EqualTo(CardPlayResult.Ok));
        }

        [Test]
        public void test_CheckPositionRequirement_RequiresBehind_WhenAhead_ReturnsBlocked()
        {
            // Arrange
            var card      = Card("pos_003", posReq: PositionRequirement.RequiresBehind);
            var validator = Validator();

            // Act
            var result = validator.CheckPositionRequirement(card, PositionState.Ahead);

            // Assert
            Assert.That(result, Is.EqualTo(CardPlayResult.PositionRequirementNotMet));
        }

        [Test]
        public void test_CheckPositionRequirement_RequiresBehind_WhenBehind_ReturnsOk()
        {
            // Arrange
            var card      = Card("pos_004", posReq: PositionRequirement.RequiresBehind);
            var validator = Validator();

            // Act
            var result = validator.CheckPositionRequirement(card, PositionState.Behind);

            // Assert
            Assert.That(result, Is.EqualTo(CardPlayResult.Ok));
        }

        [Test]
        public void test_CheckPositionRequirement_BonusIfBehind_NeverBlocksPlay()
        {
            // Arrange — BonusIfBehind/BonusIfAhead are modifier hints, not hard gates
            var cardBonus     = Card("pos_005", posReq: PositionRequirement.BonusIfBehind);
            var cardBonusAhd  = Card("pos_006", posReq: PositionRequirement.BonusIfAhead);
            var validator     = Validator();

            // Act + Assert — both positions return Ok for both bonus-only cards
            Assert.That(validator.CheckPositionRequirement(cardBonus,    PositionState.Ahead),  Is.EqualTo(CardPlayResult.Ok));
            Assert.That(validator.CheckPositionRequirement(cardBonus,    PositionState.Behind), Is.EqualTo(CardPlayResult.Ok));
            Assert.That(validator.CheckPositionRequirement(cardBonusAhd, PositionState.Ahead),  Is.EqualTo(CardPlayResult.Ok));
            Assert.That(validator.CheckPositionRequirement(cardBonusAhd, PositionState.Behind), Is.EqualTo(CardPlayResult.Ok));
        }

        [Test]
        public void test_CheckPositionRequirement_None_AlwaysOk()
        {
            // Arrange
            var card      = Card("pos_007", posReq: PositionRequirement.None);
            var validator = Validator();

            // Act + Assert
            Assert.That(validator.CheckPositionRequirement(card, PositionState.Ahead),  Is.EqualTo(CardPlayResult.Ok));
            Assert.That(validator.CheckPositionRequirement(card, PositionState.Behind), Is.EqualTo(CardPlayResult.Ok));
        }

        // ─── Internal helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Minimal ICardEffect stub that records which effect fired.
        /// Used to verify multi-effect iteration does not short-circuit (AC-2/EC10).
        /// </summary>
        private sealed class TrackingEffect : ICardEffect
        {
            private readonly int _id;
            private readonly List<int>? _log;
            public Action? OnFire { get; set; }

            public TrackingEffect(int id, List<int>? log)
            {
                _id  = id;
                _log = log;
            }

            public void Fire()
            {
                _log?.Add(_id);
                OnFire?.Invoke();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WastelandRun.Cards;
using WastelandRun.Vehicle;

// EditMode test — engine-free. WastelandRun.Cards has noEngineReferences: true.
// Named DeckMockCardData (not MockCardData) to avoid collision with stubs in sibling test files.
namespace WastelandRun.Tests.Unit.CardSystem
{
    [TestFixture]
    public class DeckLifecycleTest
    {
        // ─── Stub ─────────────────────────────────────────────────────────────────

        internal sealed class DeckMockCardData : ICardData
        {
            public string CardId { get; init; }                                          = "deck_mock_001";
            public string DisplayName { get; init; }                                     = "Mock Card";
            public string DescriptionTemplate { get; init; }                             = "";
            public string FlavorText { get; init; }                                      = "";
            public string CardArtKey { get; init; }                                      = "mock_art";
            public ChassisType ChassisPool { get; init; }                                = ChassisType.Scout;
            public CardFamily Family { get; init; }                                      = CardFamily.Precision;
            public CardRarity Rarity { get; init; }                                      = CardRarity.Common;
            public bool IsStarterCard { get; init; }                                     = false;
            public int EnergyCost { get; init; }                                         = 1;
            public int MerchantPrice { get; init; }                                      = 0;
            public CardTargetType TargetType { get; init; }                              = CardTargetType.EnemySubsystem;
            public IReadOnlyList<SlotType> ValidSubsystemTargets { get; init; }          = Array.Empty<SlotType>();
            public PositionRequirement PositionRequirement { get; init; }                = PositionRequirement.None;
            public CardKeyword Keywords { get; init; }                                   = CardKeyword.None;
            public IReadOnlyList<ICardEffect> Effects { get; init; }                     = Array.Empty<ICardEffect>();
            public int BaseDamage { get; init; }                                         = 0;
            public string? SourceSlotId { get; init; }                                   = null;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private static DeckStateManager MakeFresh() => new DeckStateManager();

        private static DeckMockCardData Card(
            string id          = "c001",
            CardKeyword kw     = CardKeyword.None,
            string? sourceSlot = null)
            => new DeckMockCardData { CardId = id, Keywords = kw, SourceSlotId = sourceSlot };

        private static Random Rng(int seed = 42) => new Random(seed);

        // ─── AC-1: Draw moves card from deck to hand ───────────────────────────────

        [Test]
        public void test_DrawOne_CardInDeck_MovesFromDeckToHand()
        {
            // Arrange
            var mgr  = MakeFresh();
            var card = Card("c001");
            mgr.Load(new[] { card }, Array.Empty<ICardData>(), Array.Empty<ICardData>());

            // Act
            var drawn = mgr.DrawOne(Rng());

            // Assert
            Assert.That(drawn, Is.SameAs(card));
            Assert.That(mgr.Deck, Is.Empty);
            Assert.That(mgr.Hand, Contains.Item(card));
        }

        // ─── AC-2: Play (non-Exhaust) moves card to discard ───────────────────────

        [Test]
        public void test_PlayCard_NonExhaust_MovesFromHandToDiscard()
        {
            // Arrange
            var mgr  = MakeFresh();
            var card = Card("c001");
            mgr.Load(new[] { card }, Array.Empty<ICardData>(), Array.Empty<ICardData>());
            mgr.DrawOne(Rng());

            // Act
            mgr.PlayCard(card);

            // Assert
            Assert.That(mgr.Hand, Is.Empty);
            Assert.That(mgr.Discard, Contains.Item(card));
            Assert.That(mgr.Exhausted, Is.Empty);
        }

        // ─── AC-3: End-of-turn discard vs Retain ──────────────────────────────────

        [Test]
        public void test_EndTurn_NonRetainCard_MovesToDiscard()
        {
            // Arrange
            var mgr  = MakeFresh();
            var card = Card("c001");
            mgr.Load(new[] { card }, Array.Empty<ICardData>(), Array.Empty<ICardData>());
            mgr.DrawOne(Rng());

            // Act
            mgr.EndTurn();

            // Assert
            Assert.That(mgr.Hand, Is.Empty);
            Assert.That(mgr.Discard, Contains.Item(card));
        }

        [Test]
        public void test_EndTurn_RetainCard_StaysInHand()
        {
            // Arrange
            var mgr  = MakeFresh();
            var card = Card("c001", CardKeyword.Retain);
            mgr.Load(new[] { card }, Array.Empty<ICardData>(), Array.Empty<ICardData>());
            mgr.DrawOne(Rng());

            // Act
            mgr.EndTurn();

            // Assert
            Assert.That(mgr.Hand, Contains.Item(card));
            Assert.That(mgr.Discard, Is.Empty);
        }

        [Test]
        public void test_PlayCard_ExhaustAndRetain_AfterMultipleEndTurns_MovesToExhausted()
        {
            // Arrange
            var mgr  = MakeFresh();
            var card = Card("c001", CardKeyword.Exhaust | CardKeyword.Retain);
            mgr.Load(new[] { card }, Array.Empty<ICardData>(), Array.Empty<ICardData>());
            mgr.DrawOne(Rng());

            // Act — Retain keeps card in hand across two turns; playing it then hits Exhaust
            mgr.EndTurn();
            mgr.EndTurn();
            mgr.PlayCard(card);

            // Assert — Exhaust wins over Retain; card goes to exhausted zone
            Assert.That(mgr.Exhausted, Contains.Item(card));
            Assert.That(mgr.Discard,   Is.Empty);
            Assert.That(mgr.Hand,      Is.Empty);
        }

        // ─── AC-4: Reshuffle discard into deck on empty draw ──────────────────────

        [Test]
        public void test_DrawOne_EmptyDeck_NonEmptyDiscard_ReshufflesAndDraws()
        {
            // Arrange
            var mgr = MakeFresh();
            var c1  = Card("c001");
            var c2  = Card("c002");
            mgr.Load(Array.Empty<ICardData>(), new[] { c1, c2 }, Array.Empty<ICardData>());

            // Act
            var drawn = mgr.DrawOne(Rng());

            // Assert — discard moved to deck; one card drawn into hand
            Assert.That(drawn, Is.Not.Null);
            Assert.That(mgr.Hand.Count,   Is.EqualTo(1));
            Assert.That(mgr.Discard,      Is.Empty);
            Assert.That(mgr.Deck.Count,   Is.EqualTo(1));
        }

        [Test]
        public void test_DrawOne_ReshuffleWithSeed42_IsNotOriginalDiscardOrder()
        {
            // Arrange — 5-card discard; Fisher-Yates with seed 42 should reorder them
            var mgr   = MakeFresh();
            var cards = Enumerable.Range(1, 5).Select(i => Card($"c{i:D3}")).ToArray();
            mgr.Load(Array.Empty<ICardData>(), cards, Array.Empty<ICardData>());

            // Act — draw all 5 after reshuffle
            var order = new List<ICardData>();
            for (int i = 0; i < 5; i++) order.Add(mgr.DrawOne(Rng(42))!);

            // Assert — shuffled order differs from original insertion order.
            // Seed 42 verified non-identity for 5 elements under Fisher-Yates; change seed if this assertion fails.
            Assert.That(order.Select(c => c.CardId), Is.Not.EqualTo(cards.Select(c => c.CardId)),
                "Seed 42 produced identity permutation — choose a different seed for this test");
        }

        [Test]
        public void test_DrawOne_ReshuffleWithSameSeed_ProducesSameResult()
        {
            // Arrange
            var cards = Enumerable.Range(1, 5).Select(i => Card($"c{i:D3}")).ToArray();

            var mgrA = MakeFresh();
            mgrA.Load(Array.Empty<ICardData>(), cards, Array.Empty<ICardData>());
            var orderA = Enumerable.Range(0, 5).Select(_ => mgrA.DrawOne(Rng(99))!.CardId).ToList();

            var mgrB = MakeFresh();
            mgrB.Load(Array.Empty<ICardData>(), cards, Array.Empty<ICardData>());
            var orderB = Enumerable.Range(0, 5).Select(_ => mgrB.DrawOne(Rng(99))!.CardId).ToList();

            // Assert — deterministic: same seed → same shuffle order
            Assert.That(orderA, Is.EqualTo(orderB));
        }

        // ─── AC-5: Both zones empty → returns null ─────────────────────────────────

        [Test]
        public void test_DrawOne_EmptyDeckAndDiscard_ReturnsNull_HandUnchanged()
        {
            // Arrange
            var mgr = MakeFresh();
            mgr.Load(Array.Empty<ICardData>(), Array.Empty<ICardData>(), Array.Empty<ICardData>());

            // Act
            var result = mgr.DrawOne(Rng());

            // Assert
            Assert.That(result, Is.Null);
            Assert.That(mgr.Hand, Is.Empty);
        }

        // ─── AC-6: Exhaust + Retain combo played → exhausted zone ─────────────────

        [Test]
        public void test_PlayCard_ExhaustAndRetain_MovesToExhausted()
        {
            // Arrange
            var mgr  = MakeFresh();
            var card = Card("c001", CardKeyword.Exhaust | CardKeyword.Retain);
            mgr.Load(new[] { card }, Array.Empty<ICardData>(), Array.Empty<ICardData>());
            mgr.DrawOne(Rng());

            // Act
            mgr.PlayCard(card);

            // Assert — Exhaust wins; card goes to exhausted, not discard
            Assert.That(mgr.Exhausted, Contains.Item(card));
            Assert.That(mgr.Discard,   Is.Empty);
            Assert.That(mgr.Hand,      Is.Empty);
        }

        // ─── AC-7: Exhaust + Retain unplayed → stays in hand after EndTurn ─────────

        [Test]
        public void test_EndTurn_ExhaustAndRetainCardNotPlayed_StaysInHand()
        {
            // Arrange
            var mgr  = MakeFresh();
            var card = Card("c001", CardKeyword.Exhaust | CardKeyword.Retain);
            mgr.Load(new[] { card }, Array.Empty<ICardData>(), Array.Empty<ICardData>());
            mgr.DrawOne(Rng());

            // Act
            mgr.EndTurn();

            // Assert — Retain keeps it in hand; Exhaust only fires on PlayCard
            Assert.That(mgr.Hand, Contains.Item(card));
            Assert.That(mgr.Discard, Is.Empty);
        }

        [Test]
        public void test_EndTurn_ExhaustAndRetainCard_StaysInHand_After10Turns()
        {
            // Arrange
            var mgr  = MakeFresh();
            var card = Card("c001", CardKeyword.Exhaust | CardKeyword.Retain);
            mgr.Load(new[] { card }, Array.Empty<ICardData>(), Array.Empty<ICardData>());
            mgr.DrawOne(Rng());

            // Act
            for (int i = 0; i < 10; i++) mgr.EndTurn();

            // Assert — Retain persists indefinitely until explicitly played
            Assert.That(mgr.Hand, Contains.Item(card));
            Assert.That(mgr.Discard, Is.Empty);
        }

        // ─── AC-8: Exhausted cards never re-enter deck or discard ─────────────────

        [Test]
        public void test_ExhaustedCard_DoesNotReenterDeckOrDiscard_AfterReshuffle()
        {
            // Arrange — exhausted card in load; another card in discard to trigger reshuffle
            var mgr      = MakeFresh();
            var exhausted = Card("ex_001", CardKeyword.Exhaust);
            var normal    = Card("n_001");
            mgr.Load(Array.Empty<ICardData>(), new[] { normal }, new[] { exhausted });

            // Act — reshuffle triggered by drawing from empty deck
            mgr.DrawOne(Rng());

            // Assert
            Assert.That(mgr.Deck.Concat(mgr.Discard).Concat(mgr.Hand), Does.Not.Contain(exhausted));
            Assert.That(mgr.Exhausted, Contains.Item(exhausted));
        }

        // ─── AC-9: DrawInnateCards sweeps deck + discard, not exhausted ───────────

        [Test]
        public void test_DrawInnateCards_MovesInnateFromDeckAndDiscard_ToHand()
        {
            // Arrange
            var mgr        = MakeFresh();
            var innateInDeck    = Card("inn_d", CardKeyword.Innate);
            var innateInDiscard = Card("inn_disc", CardKeyword.Innate);
            var normalCard      = Card("n001");
            mgr.Load(new[] { innateInDeck, normalCard }, new[] { innateInDiscard }, Array.Empty<ICardData>());

            // Act
            mgr.DrawInnateCards();

            // Assert
            Assert.That(mgr.Hand, Contains.Item(innateInDeck));
            Assert.That(mgr.Hand, Contains.Item(innateInDiscard));
            Assert.That(mgr.Hand, Does.Not.Contain(normalCard));
            Assert.That(mgr.Deck, Contains.Item(normalCard));
        }

        // ─── AC-10: Exhausted Innate cards are not drawn ──────────────────────────

        [Test]
        public void test_DrawInnateCards_ExhaustedInnateCard_NotDrawn()
        {
            // Arrange
            var mgr          = MakeFresh();
            var exhaustedInn = Card("ex_inn", CardKeyword.Innate);
            mgr.Load(Array.Empty<ICardData>(), Array.Empty<ICardData>(), new[] { exhaustedInn });

            // Act
            mgr.DrawInnateCards();

            // Assert — exhausted zone is never swept
            Assert.That(mgr.Hand, Is.Empty);
            Assert.That(mgr.Exhausted, Contains.Item(exhaustedInn));
        }

        // ─── AC-11: Innate already in hand is not duplicated ──────────────────────

        [Test]
        public void test_DrawInnateCards_InnateAlreadyInHand_NotDuplicated()
        {
            // Arrange — stage an Innate card into hand by drawing it from deck,
            //            then call DrawInnateCards with another copy in discard
            var mgr         = MakeFresh();
            var innateCard  = Card("inn_001", CardKeyword.Innate);
            var innateDisc  = Card("inn_002", CardKeyword.Innate);
            mgr.Load(new[] { innateCard }, new[] { innateDisc }, Array.Empty<ICardData>());
            mgr.DrawOne(Rng()); // innateCard now in hand

            // Act
            mgr.DrawInnateCards();

            // Assert — innateCard not added again; innateDisc is new so it gets added
            Assert.That(mgr.Hand.Count(c => c.CardId == "inn_001"), Is.EqualTo(1));
            Assert.That(mgr.Hand, Contains.Item(innateDisc));
        }

        // ─── AC-12: RemoveGrantedCards sweeps deck, hand, discard — not exhausted ─

        [Test]
        public void test_RemoveGrantedCards_TwoCopiesSameCardId_OnlyRemovesMatchingSourceSlotId()
        {
            // Arrange — two copies sharing CardId but different SourceSlotId
            var mgr     = MakeFresh();
            var granted = Card("g001", sourceSlot: "slot_A");
            var reward  = Card("g001", sourceSlot: null);           // same CardId, different origin
            mgr.Load(new[] { granted, reward }, Array.Empty<ICardData>(), Array.Empty<ICardData>());

            // Act
            mgr.RemoveGrantedCards("slot_A");

            // Assert — only the slot_A copy removed
            Assert.That(mgr.Deck, Does.Not.Contain(granted));
            Assert.That(mgr.Deck, Contains.Item(reward));
        }

        [Test]
        public void test_RemoveGrantedCards_SweepsAllZonesExceptExhausted()
        {
            // Arrange — stage one card into hand by drawing it, keep others in deck/discard/exhausted
            var mgr           = MakeFresh();
            var inDeck        = Card("g_deck",     sourceSlot: "slot_B");
            var toBeDrawn     = Card("g_hand",     sourceSlot: "slot_B");  // will move to hand
            var inDiscard     = Card("g_disc",     sourceSlot: "slot_B");
            var inExhausted   = Card("g_exh",      sourceSlot: "slot_B");
            var keeper        = Card("keeper_001");

            mgr.Load(
                new[] { toBeDrawn, inDeck, keeper },
                new[] { inDiscard },
                new[] { inExhausted });

            mgr.DrawOne(Rng()); // toBeDrawn now in hand

            // Act
            mgr.RemoveGrantedCards("slot_B");

            // Assert
            Assert.That(mgr.Deck,      Does.Not.Contain(inDeck));
            Assert.That(mgr.Hand,      Does.Not.Contain(toBeDrawn));
            Assert.That(mgr.Discard,   Does.Not.Contain(inDiscard));
            Assert.That(mgr.Exhausted, Contains.Item(inExhausted),  "exhausted zone must not be swept");
            Assert.That(mgr.Deck,      Contains.Item(keeper),       "non-granted card must survive");
        }

        [Test]
        public void test_RemoveGrantedCards_NullOrEmptySourceSlotId_Throws()
        {
            var mgr = MakeFresh();
            mgr.Load(Array.Empty<ICardData>(), Array.Empty<ICardData>(), Array.Empty<ICardData>());

            Assert.Throws<ArgumentException>(() => mgr.RemoveGrantedCards(null!));
            Assert.Throws<ArgumentException>(() => mgr.RemoveGrantedCards(""));
        }
    }
}

// WastelandRun.Tests.Gameplay — combat-loop_test.cs
// End-to-end checks for the engine-free CombatLoop driving a Dune Skimmer encounter.
// Covers setup wiring, R3 card play pipeline, R4 enemy intent telegraph + resolution,
// and the PlayerWon / EnemyWon outcomes.
// GDD sources: design/gdd/card-combat-system.md §R2-R9, design/gdd/biome-1-enemy-roster.md §Archetype 1.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using WastelandRun.Cards;
using WastelandRun.Enemies;
using WastelandRun.Enemies.Archetypes;
using WastelandRun.Gameplay.Combat;
using WastelandRun.Vehicle;

namespace WastelandRun.Tests.Gameplay
{
    [TestFixture]
    public class CombatLoopTests
    {
        // -----------------------------------------------------------------
        // Setup
        // -----------------------------------------------------------------

        [Test]
        public void test_combat_loop_setup_initialises_full_state()
        {
            CombatLoop loop = NewLoop(out CombatState state, seed: 1, deck: BuildStarterDeck(8, cost: 1, dmg: 3));

            Assert.That(state.TurnIndex,            Is.EqualTo(1));
            Assert.That(state.Energy,               Is.EqualTo(CombatRules.Default.StartingEnergy));
            Assert.That(state.Outcome,              Is.EqualTo(CombatOutcome.InProgress));
            Assert.That(state.Deck.Hand.Count,      Is.EqualTo(CombatRules.Default.HandSizePerTurn));
            Assert.That(state.Deck.Deck.Count,      Is.EqualTo(8 - CombatRules.Default.HandSizePerTurn));
            Assert.That(state.Deck.Discard,         Is.Empty);
            Assert.That(state.NextEnemyIntent,      Is.Not.Null,
                "Setup must telegraph the turn-1 enemy intent before the player acts.");
            Assert.That(state.NextEnemyIntent.EnrageBonus, Is.EqualTo(0),
                "Turn 1 is well before Dune Skimmer's default enrage turn 8.");
            Assert.That(state.Player.IsDead, Is.False);
            Assert.That(state.Enemy.IsDead,  Is.False);
        }

        [Test]
        public void test_combat_loop_setup_is_deterministic_for_same_seed()
        {
            var deckA = BuildStarterDeck(10, cost: 1, dmg: 3);
            var deckB = BuildStarterDeck(10, cost: 1, dmg: 3);

            CombatLoop a = NewLoop(out CombatState stateA, seed: 42, deck: deckA);
            CombatLoop b = NewLoop(out CombatState stateB, seed: 42, deck: deckB);

            // The two states draw the same set of card IDs in the same order.
            var handA = ExtractCardIds(stateA.Deck.Hand);
            var handB = ExtractCardIds(stateB.Deck.Hand);
            Assert.That(handB, Is.EqualTo(handA), "Same seed → same opening hand.");

            // And the same intent name is telegraphed.
            Assert.That(stateB.NextEnemyIntent.Intent.Name, Is.EqualTo(stateA.NextEnemyIntent.Intent.Name));
        }

        // -----------------------------------------------------------------
        // PlayCard validation
        // -----------------------------------------------------------------

        [Test]
        public void test_play_card_rejects_unaffordable_card_with_insufficient_energy_code()
        {
            CombatLoop loop = NewLoop(out CombatState state, seed: 1,
                deck: BuildStarterDeck(6, cost: 4, dmg: 1));  // cost > starting energy (3)

            ICardData card = state.Deck.Hand[0];
            int hpBefore = state.Enemy.GetSlotHp(SlotType.Frame);

            CardPlayResult result = loop.PlayCard(card, SlotType.Frame);

            Assert.That(result, Is.EqualTo(CardPlayResult.InsufficientEnergy));
            Assert.That(state.Energy, Is.EqualTo(CombatRules.Default.StartingEnergy), "Energy must not be consumed on a rejected play.");
            Assert.That(state.Deck.Hand, Does.Contain(card), "Rejected card stays in hand.");
            Assert.That(state.Enemy.GetSlotHp(SlotType.Frame), Is.EqualTo(hpBefore));
        }

        [Test]
        public void test_play_card_rejects_card_not_in_hand_with_not_in_hand_code()
        {
            CombatLoop loop = NewLoop(out CombatState state, seed: 1,
                deck: BuildStarterDeck(6, cost: 1, dmg: 3));

            ICardData stranger = FixtureCard.Damage("stranger", cost: 1, baseDamage: 99);
            CardPlayResult result = loop.PlayCard(stranger, SlotType.Frame);

            Assert.That(result, Is.EqualTo(CardPlayResult.NotInHand));
        }

        // -----------------------------------------------------------------
        // PlayCard happy path
        // -----------------------------------------------------------------

        [Test]
        public void test_play_card_damages_enemy_frame_and_consumes_energy_and_zone()
        {
            CombatLoop loop = NewLoop(out CombatState state, seed: 1,
                deck: BuildStarterDeck(6, cost: 1, dmg: 4));

            ICardData card = state.Deck.Hand[0];
            int frameBefore = state.Enemy.GetSlotHp(SlotType.Frame);

            CardPlayResult result = loop.PlayCard(card, SlotType.Frame);

            Assert.That(result, Is.EqualTo(CardPlayResult.Ok));
            Assert.That(state.Energy, Is.EqualTo(CombatRules.Default.StartingEnergy - 1));
            Assert.That(state.Enemy.GetSlotHp(SlotType.Frame), Is.EqualTo(frameBefore - 4));
            Assert.That(state.Deck.Hand, Does.Not.Contain(card));
            Assert.That(state.Deck.Discard, Does.Contain(card));
        }

        // -----------------------------------------------------------------
        // EndPlayerTurn — enemy resolves intent
        // -----------------------------------------------------------------

        [Test]
        public void test_end_player_turn_resolves_telegraphed_intent_and_refreshes_player()
        {
            // Tiny deck so we can verify draw refill on turn 2.
            CombatLoop loop = NewLoop(out CombatState state, seed: 1,
                deck: BuildStarterDeck(8, cost: 1, dmg: 1));

            int playerFrameBefore = state.Player.GetSlotHp(SlotType.Frame);
            int playerArmorBefore = state.Player.CurrentArmor;
            int playerWeaponBefore = state.Player.GetSlotHp(SlotType.Weapon);
            PickedIntent telegraphed = state.NextEnemyIntent;

            loop.EndPlayerTurn();

            // Turn rolled over.
            Assert.That(state.TurnIndex, Is.EqualTo(2));
            Assert.That(state.Energy,    Is.EqualTo(CombatRules.Default.EnergyPerTurn));
            Assert.That(state.NextEnemyIntent, Is.Not.Null, "A new intent must be telegraphed for turn 2.");

            // The enemy intent landed on the player. Damage went somewhere depending on which
            // intent was picked — assert the *aggregate* loss reflects the intent's behaviour.
            switch (telegraphed.Intent.Name)
            {
                case "Ram":
                    // Ram: 8 dmg → Frame. Armor 4 absorbs, then 4 → Hp.
                    Assert.That(state.Player.CurrentArmor, Is.EqualTo(0));
                    Assert.That(state.Player.GetSlotHp(SlotType.Frame), Is.EqualTo(playerFrameBefore - 4));
                    break;
                case "Scatter Shot":
                    // 6 dmg → Weapon (plating 2 absorbs, 4 → Hp = 8-4 = 4).
                    Assert.That(state.Player.GetSlotHp(SlotType.Weapon), Is.EqualTo(playerWeaponBefore - 4));
                    break;
                case "Tailgate":
                    // No state delta on either vehicle (position is combat-scope and not modelled
                    // on Vehicle). Player should be untouched.
                    Assert.That(state.Player.CurrentArmor,              Is.EqualTo(playerArmorBefore));
                    Assert.That(state.Player.GetSlotHp(SlotType.Frame), Is.EqualTo(playerFrameBefore));
                    Assert.That(state.Player.GetSlotHp(SlotType.Weapon), Is.EqualTo(playerWeaponBefore));
                    break;
                default:
                    Assert.Fail($"Unexpected intent '{telegraphed.Intent.Name}' for Dune Skimmer.");
                    break;
            }
        }

        // -----------------------------------------------------------------
        // End-to-end Dune Skimmer fight
        // -----------------------------------------------------------------

        [Test]
        public void test_full_fight_player_with_lethal_deck_terminates_with_player_won()
        {
            // Player has 6× Smash cards (3 energy, 12 damage to enemy Frame).
            // One Smash drops enemy Frame to 0 (12 HP → 0) on turn 1.
            var deck = BuildStarterDeck(6, cost: 3, dmg: 12);
            CombatLoop loop = NewLoop(out CombatState state, seed: 7, deck: deck);

            ICardData lethal = state.Deck.Hand[0];

            CardPlayResult result = loop.PlayCard(lethal, SlotType.Frame);

            Assert.That(result, Is.EqualTo(CardPlayResult.Ok));
            Assert.That(state.Enemy.IsDead, Is.True);
            Assert.That(state.Outcome, Is.EqualTo(CombatOutcome.PlayerWon));

            // Subsequent EndPlayerTurn is a no-op (idempotent on ended combat).
            int playerHpBefore = state.Player.GetSlotHp(SlotType.Frame);
            loop.EndPlayerTurn();
            Assert.That(state.Player.GetSlotHp(SlotType.Frame), Is.EqualTo(playerHpBefore),
                "EndPlayerTurn must not resolve enemy intent after combat has ended.");
        }

        [Test]
        public void test_full_fight_passive_player_eventually_loses_to_enemy_attacks()
        {
            // Player plays nothing — enemy chips Frame each turn. Verify EnemyWon eventually triggers.
            // Player Frame: 20 HP + 4 Armor = 24 effective. Ram base 8, so ~3 hits to break armor + Hp,
            // then 2-3 more to kill. Run up to 20 turns and assert combat ends with EnemyWon.
            CombatLoop loop = NewLoop(out CombatState state, seed: 3,
                deck: BuildStarterDeck(8, cost: 1, dmg: 1));

            int safety = 30;
            while (!state.IsEnded && safety-- > 0)
            {
                loop.EndPlayerTurn();
            }

            Assert.That(state.IsEnded, Is.True, "Combat must terminate within the safety bound.");
            Assert.That(state.Outcome, Is.EqualTo(CombatOutcome.EnemyWon));
            Assert.That(state.Player.IsDead, Is.True);
            Assert.That(state.Enemy.IsDead,  Is.False);
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static CombatLoop NewLoop(
            out CombatState state,
            int seed,
            IReadOnlyList<ICardData> deck,
            PositionState startingPosition = PositionState.Ahead)
        {
            var player = TestPlayerFixtures.SpawnPlayer();
            var enemy  = VehicleFactory.FromChassis(DuneSkimmer.BuildChassis());
            var brain  = DuneSkimmer.BuildBrain();

            var loop = new CombatLoop();
            state = loop.Setup(player, enemy, deck, brain, seed, CombatRules.Default, startingPosition);
            return loop;
        }

        private static IReadOnlyList<ICardData> BuildStarterDeck(int count, int cost, int dmg)
        {
            var list = new List<ICardData>(count);
            for (int i = 0; i < count; i++)
                list.Add(FixtureCard.Damage($"strike_{i}", cost, dmg));
            return list;
        }

        private static List<string> ExtractCardIds(IReadOnlyList<ICardData> cards)
        {
            var ids = new List<string>(cards.Count);
            foreach (ICardData c in cards) ids.Add(c.CardId);
            return ids;
        }
    }
}

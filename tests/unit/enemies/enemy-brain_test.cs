// WastelandRun.Tests.Enemies — enemy-brain_test.cs
// Verifies the cross-cutting AI primitives in isolation, independent of any one
// archetype: EnrageConfig math, RetargetPolicy modes, WeightModifier factories,
// IntentPool weighted selection (with a degenerate single-spec pool for determinism).

using System;
using System.Collections.Generic;
using NUnit.Framework;
using WastelandRun.Enemies;
using WastelandRun.Vehicle;

namespace WastelandRun.Tests.Enemies
{
    [TestFixture]
    public class EnemyBrainTests
    {
        // -------------------------------------------------------------------
        // EnrageConfig
        // -------------------------------------------------------------------

        [Test]
        public void test_enrage_bonus_is_zero_before_enrage_turn()
        {
            var enrage = new EnrageConfig(enrageTurn: 8, baseBonus: 2, escalation: 1);
            Assert.That(enrage.ComputeBonus(1), Is.EqualTo(0));
            Assert.That(enrage.ComputeBonus(7), Is.EqualTo(0));
        }

        [Test]
        public void test_enrage_bonus_equals_base_on_activation_turn()
        {
            // card-combat-system.md R7: activation turn applies BaseBonus, no escalation yet.
            var enrage = new EnrageConfig(enrageTurn: 5, baseBonus: 3, escalation: 2);
            Assert.That(enrage.ComputeBonus(5), Is.EqualTo(3));
        }

        [Test]
        public void test_enrage_bonus_scales_linearly_after_activation()
        {
            // card-combat-system.md R7: BaseBonus + (turn - EnrageTurn) * Escalation.
            // Defaults base=+2 escalation=+1 ⇒ turn 8→2, 9→3, 10→4.
            var enrage = new EnrageConfig(enrageTurn: 8, baseBonus: 2, escalation: 1);
            Assert.That(enrage.ComputeBonus(8),  Is.EqualTo(2), "Activation turn — base only.");
            Assert.That(enrage.ComputeBonus(9),  Is.EqualTo(3));
            Assert.That(enrage.ComputeBonus(10), Is.EqualTo(4));
            Assert.That(enrage.ComputeBonus(15), Is.EqualTo(9));
        }

        [Test]
        public void test_enrage_bonus_with_custom_escalation()
        {
            // IronShepherd archetype: base=3, escalation=3 ⇒ turn 6→3, 7→6, 8→9.
            var enrage = new EnrageConfig(enrageTurn: 6, baseBonus: 3, escalation: 3);
            Assert.That(enrage.ComputeBonus(6), Is.EqualTo(3));
            Assert.That(enrage.ComputeBonus(7), Is.EqualTo(6));
            Assert.That(enrage.ComputeBonus(8), Is.EqualTo(9));
        }

        // -------------------------------------------------------------------
        // RetargetPolicy
        // -------------------------------------------------------------------

        [Test]
        public void test_fixed_slot_retarget_always_returns_configured_slot()
        {
            var policy = RetargetPolicy.Fixed(SlotType.Frame);
            var player = TestPlayerFixtures.SpawnPlayer();

            Assert.That(policy.ResolveTarget(player, declared: SlotType.Weapon), Is.EqualTo(SlotType.Frame));
            Assert.That(policy.ResolveTarget(player, declared: SlotType.Engine), Is.EqualTo(SlotType.Frame));
        }

        [Test]
        public void test_priority_list_picks_first_alive_slot_in_order()
        {
            var policy = RetargetPolicy.PriorityListOf(
                SlotType.Engine, SlotType.Frame, SlotType.Weapon, SlotType.Mobility);

            var player = TestPlayerFixtures.SpawnPlayer();
            Assert.That(policy.ResolveTarget(player, SlotType.Mobility), Is.EqualTo(SlotType.Engine),
                "All alive → first in priority list.");

            // Kill Engine.
            player.ApplyDamage(SlotType.Engine, 100, DamageSource.Card);
            Assert.That(policy.ResolveTarget(player, SlotType.Mobility), Is.EqualTo(SlotType.Frame),
                "Engine offline → fall to Frame.");
        }

        // -------------------------------------------------------------------
        // WeightModifiers
        // -------------------------------------------------------------------

        [Test]
        public void test_target_slot_degraded_modifier_only_fires_on_degraded_state()
        {
            WeightModifier mod = WeightModifiers.TargetSlotDegraded(SlotType.Frame, 2.5f);
            var self   = TestPlayerFixtures.SpawnPlayer();
            var target = TestPlayerFixtures.SpawnPlayer();

            // Functional → x1.
            Assert.That(mod(TestContexts.Brain(self, target)), Is.EqualTo(1f));

            // Push Frame into Degraded (target Frame = 20 HP, 4 armor, threshold 50%).
            // Need Hp > 0 and Hp ≤ 10. Damage 4 armor + 11 HP = 15.
            target.ApplyDamage(SlotType.Frame, 15, DamageSource.Card);
            Assert.That(target.GetDamageState(SlotType.Frame), Is.EqualTo(DamageState.Degraded));
            Assert.That(mod(TestContexts.Brain(self, target)), Is.EqualTo(2.5f));

            // Push to Offline → modifier should NOT fire (only on Degraded state).
            target.ApplyDamage(SlotType.Frame, 100, DamageSource.Card);
            Assert.That(target.GetDamageState(SlotType.Frame), Is.EqualTo(DamageState.Offline));
            Assert.That(mod(TestContexts.Brain(self, target)), Is.EqualTo(1f));
        }

        [Test]
        public void test_zero_if_target_slot_offline_filters_intent()
        {
            WeightModifier mod = WeightModifiers.ZeroIfTargetSlotOffline(SlotType.Engine);
            var self   = TestPlayerFixtures.SpawnPlayer();
            var target = TestPlayerFixtures.SpawnPlayer();

            Assert.That(mod(TestContexts.Brain(self, target)), Is.EqualTo(1f));
            target.ApplyDamage(SlotType.Engine, 100, DamageSource.Card);
            Assert.That(mod(TestContexts.Brain(self, target)), Is.EqualTo(0f));
        }

        [Test]
        public void test_zero_if_all_target_slots_offline_short_circuits_pool()
        {
            WeightModifier mod = WeightModifiers.ZeroIfAllTargetSlotsOffline();
            var self   = TestPlayerFixtures.SpawnPlayer();
            var target = TestPlayerFixtures.SpawnPlayer();

            Assert.That(mod(TestContexts.Brain(self, target)), Is.EqualTo(1f));

            foreach (SlotType s in new[] { SlotType.Weapon, SlotType.Engine, SlotType.Mobility, SlotType.Frame })
                target.ApplyDamage(s, 100, DamageSource.Card);

            Assert.That(mod(TestContexts.Brain(self, target)), Is.EqualTo(0f),
                "All target slots offline → intent pool should fully short-circuit.");
        }

        // -------------------------------------------------------------------
        // EnemyBrain.ChooseIntent
        // -------------------------------------------------------------------

        [Test]
        public void test_brain_returns_null_when_every_spec_has_zero_weight()
        {
            // Single spec with a modifier that zeros it out unconditionally.
            var spec = new IntentSpec(
                name: "AlwaysOff",
                baseWeight: 50,
                declaredTargetSlot: SlotType.Frame,
                builder: () => new DamageIntent("AlwaysOff", 10),
                modifiers: new WeightModifier[] { _ => 0f });

            var brain = new EnemyBrain(
                new IntentPool(new[] { spec }),
                RetargetPolicy.Fixed(SlotType.Frame));

            var self   = TestPlayerFixtures.SpawnPlayer();
            var target = TestPlayerFixtures.SpawnPlayer();

            PickedIntent picked = brain.ChooseIntent(
                TestContexts.Brain(self, target), new Random(1));

            Assert.That(picked, Is.Null);
        }

        [Test]
        public void test_brain_always_picks_the_only_available_spec()
        {
            // Degenerate pool: one spec. Brain must pick it regardless of RNG seed.
            var spec = new IntentSpec(
                name: "Only",
                baseWeight: 100,
                declaredTargetSlot: SlotType.Engine,
                builder: () => new DamageIntent("Only", 5));

            var brain = new EnemyBrain(
                new IntentPool(new[] { spec }),
                RetargetPolicy.Fixed(SlotType.Engine),
                new EnrageConfig(enrageTurn: 8, baseBonus: 2, escalation: 1));

            var self   = TestPlayerFixtures.SpawnPlayer();
            var target = TestPlayerFixtures.SpawnPlayer();

            for (int seed = 0; seed < 5; seed++)
            {
                PickedIntent picked = brain.ChooseIntent(
                    TestContexts.Brain(self, target, turnIndex: 9), new Random(seed));

                Assert.That(picked,                    Is.Not.Null,                $"seed {seed}");
                Assert.That(picked.SourceSpec,         Is.SameAs(spec),            $"seed {seed}");
                Assert.That(picked.ResolvedTargetSlot, Is.EqualTo(SlotType.Engine), $"seed {seed}");
                Assert.That(picked.EnrageBonus,        Is.EqualTo(3),               "Turn 9, enrageTurn 8, base +2 escalation +1 → 2 + (9-8)*1 = 3.");
            }
        }
    }
}

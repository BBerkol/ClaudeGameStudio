// WastelandRun.Tests.Enemies — iron-shepherd_test.cs
// Verifies the Biome 1 Elite archetype: PriorityList retarget, Engine-degraded
// Ram bias, Corroded application via Armor Rend, Reinforce armor-full gate,
// Enrage timing (turn 6, +3/turn), pool swap to Enraged behaviour.
// GDD source: design/gdd/biome-1-enemy-roster.md §Archetype 2.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using WastelandRun.Enemies;
using WastelandRun.Enemies.Archetypes;
using WastelandRun.Vehicle;

namespace WastelandRun.Tests.Enemies
{
    [TestFixture]
    public class IronShepherdTests
    {
        [Test]
        public void test_iron_shepherd_chassis_matches_gdd_stat_block()
        {
            var shepherd = VehicleFactory.FromChassis(IronShepherd.BuildChassis());

            Assert.That(shepherd.Chassis,                         Is.EqualTo(ChassisType.Shepherd));
            Assert.That(shepherd.GetSlotMaxHp(SlotType.Weapon),   Is.EqualTo(10));
            Assert.That(shepherd.GetSlotMaxHp(SlotType.Engine),   Is.EqualTo(14));
            Assert.That(shepherd.GetSlotMaxHp(SlotType.Mobility), Is.EqualTo(14));
            Assert.That(shepherd.GetSlotMaxHp(SlotType.Frame),    Is.EqualTo(28));
            Assert.That(shepherd.MaxArmor,                        Is.EqualTo(10),
                "Frame plating contributes 10 armor.");
            Assert.That(shepherd.CurrentArmor,                    Is.EqualTo(10),
                "Spawn should fill armor to max.");
        }

        [Test]
        public void test_brain_priority_list_falls_through_when_engine_offline()
        {
            EnemyBrain brain = IronShepherd.BuildBrain();
            var shepherd = VehicleFactory.FromChassis(IronShepherd.BuildChassis());
            var player   = TestPlayerFixtures.SpawnPlayer();

            // Disable player's Engine.
            player.ApplyDamage(SlotType.Engine, 100, DamageSource.Card);
            Assert.That(player.GetDamageState(SlotType.Engine), Is.EqualTo(DamageState.Offline));

            // PriorityList(Engine, Frame, Weapon, Mobility) — Engine offline → Frame.
            SlotType resolved = brain.Retarget.ResolveTarget(player, declared: SlotType.Engine);
            Assert.That(resolved, Is.EqualTo(SlotType.Frame));
        }

        [Test]
        public void test_brain_priority_list_falls_to_declared_when_all_priority_offline()
        {
            EnemyBrain brain = IronShepherd.BuildBrain();
            var player   = TestPlayerFixtures.SpawnPlayer();

            // Disable every slot.
            foreach (SlotType s in new[] { SlotType.Engine, SlotType.Frame, SlotType.Weapon, SlotType.Mobility })
                player.ApplyDamage(s, 100, DamageSource.Card);

            // All priority slots are Offline → fall through to the declared slot.
            SlotType resolved = brain.Retarget.ResolveTarget(player, declared: SlotType.Mobility);
            Assert.That(resolved, Is.EqualTo(SlotType.Mobility));
        }

        [Test]
        public void test_armor_rend_applies_corroded_to_engine_slot()
        {
            // Pull Armor Rend spec out of the pool and resolve it against the player.
            EnemyBrain brain = IronShepherd.BuildBrain();
            IntentSpec rendSpec = FindByName(brain.ActivePool.Specs, "Armor Rend");
            IEnemyIntent rend  = rendSpec.Builder();

            var shepherd = VehicleFactory.FromChassis(IronShepherd.BuildChassis());
            var player   = TestPlayerFixtures.SpawnPlayer();

            Assert.That(player.GetSlotStatuses(SlotType.Engine), Is.Empty);

            rend.Resolve(TestContexts.Resolve(shepherd, player, SlotType.Engine));

            IReadOnlyList<StatusInstance> engineStatuses = player.GetSlotStatuses(SlotType.Engine);
            Assert.That(engineStatuses.Count, Is.EqualTo(1));
            Assert.That(engineStatuses[0].Type,              Is.EqualTo(StatusType.Corroded));
            Assert.That(engineStatuses[0].RemainingDuration, Is.EqualTo(3));
            Assert.That(engineStatuses[0].Stacks,            Is.EqualTo(1));
        }

        [Test]
        public void test_reinforce_weight_zeros_when_shepherd_armor_already_full()
        {
            EnemyBrain brain = IronShepherd.BuildBrain();
            IntentSpec reinforce = FindByName(brain.ActivePool.Specs, "Reinforce");

            var shepherd = VehicleFactory.FromChassis(IronShepherd.BuildChassis());
            var player   = TestPlayerFixtures.SpawnPlayer();

            // Spawn fills armor to MaxArmor; Reinforce should be gated to 0.
            float fullArmorWeight = reinforce.ComputeEffectiveWeight(
                TestContexts.Brain(shepherd, player));
            Assert.That(fullArmorWeight, Is.EqualTo(0f),
                "Reinforce should drop when self armor is at max.");

            // Drain shepherd armor (hit Frame so armor absorbs first).
            shepherd.ApplyDamage(SlotType.Frame, 5, DamageSource.Card);
            Assert.That(shepherd.CurrentArmor, Is.LessThan(shepherd.MaxArmor));

            float drainedWeight = reinforce.ComputeEffectiveWeight(
                TestContexts.Brain(shepherd, player));
            Assert.That(drainedWeight, Is.EqualTo(20f),
                "Reinforce base weight should be active once armor is missing.");
        }

        [Test]
        public void test_enrage_bonus_kicks_in_at_turn_six_base_three_escalation_three()
        {
            EnemyBrain brain = IronShepherd.BuildBrain();

            Assert.That(brain.Enrage.EnrageTurn, Is.EqualTo(6));
            Assert.That(brain.Enrage.BaseBonus,  Is.EqualTo(3));
            Assert.That(brain.Enrage.Escalation, Is.EqualTo(3));

            Assert.That(brain.Enrage.ComputeBonus(turnIndex: 5), Is.EqualTo(0),
                "Pre-enrage turn yields no bonus.");
            Assert.That(brain.Enrage.ComputeBonus(turnIndex: 6), Is.EqualTo(3),
                "Activation turn — base +3.");
            Assert.That(brain.Enrage.ComputeBonus(turnIndex: 8), Is.EqualTo(9),
                "Turn 8 — base 3 + 2 turns × +3 = 9.");
        }

        [Test]
        public void test_enrage_pool_swap_removes_reinforce_and_armor_rend()
        {
            EnemyBrain brain = IronShepherd.BuildBrain();

            // Pre-swap: 4 intents, Reinforce + Armor Rend present.
            Assert.That(brain.ActivePool.Specs.Count, Is.EqualTo(4));

            brain.ActivePool = IronShepherd.BuildEnragePool();

            var names = NamesOf(brain.ActivePool.Specs);
            Assert.That(brain.ActivePool.Specs.Count, Is.EqualTo(2));
            Assert.That(names, Does.Contain("Ram (Enraged)"));
            Assert.That(names, Does.Contain("Double Ram"));
            Assert.That(names, Does.Not.Contain("Reinforce"));
            Assert.That(names, Does.Not.Contain("Armor Rend"));
        }

        [Test]
        public void test_ram_with_enrage_bonus_consumes_armor_then_hp()
        {
            // Verify damage routes through the F-VP2 pipeline with enrage bonus added.
            var shepherd = VehicleFactory.FromChassis(IronShepherd.BuildChassis());
            var player   = TestPlayerFixtures.SpawnPlayer(frameArmor: 4);

            var ram = new DamageIntent("Ram", baseDamage: 10);
            // Turn 8 → +9 enrage bonus for Shepherd (turn 6 + 3/turn).
            int enrageBonus = IronShepherd.BuildBrain().Enrage.ComputeBonus(turnIndex: 8);
            Assert.That(enrageBonus, Is.EqualTo(9));

            ram.Resolve(TestContexts.Resolve(shepherd, player, SlotType.Frame, enrageBonus: enrageBonus));

            // 10 + 9 = 19 damage. 4 absorbed by armor, 15 → Hp.
            // Player Frame had 20 HP → 20 - 15 = 5 HP remaining.
            Assert.That(player.CurrentArmor,              Is.EqualTo(0));
            Assert.That(player.GetSlotHp(SlotType.Frame), Is.EqualTo(5));
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private static IntentSpec FindByName(IReadOnlyList<IntentSpec> specs, string name)
        {
            foreach (IntentSpec s in specs)
                if (s.Name == name) return s;
            throw new AssertionException($"No intent spec named '{name}' in pool.");
        }

        private static List<string> NamesOf(IReadOnlyList<IntentSpec> specs)
        {
            var names = new List<string>(specs.Count);
            foreach (IntentSpec s in specs) names.Add(s.Name);
            return names;
        }
    }
}

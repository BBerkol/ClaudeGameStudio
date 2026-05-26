// WastelandRun.Tests.Enemies — dune-skimmer_test.cs
// Verifies the Biome 1 Raider archetype: chassis stats, intent pool shape,
// position-gated Tailgate, Frame-degraded Ram bias, end-to-end damage flow.
// GDD source: design/gdd/biome-1-enemy-roster.md §Archetype 1.

using System;
using NUnit.Framework;
using WastelandRun.Enemies;
using WastelandRun.Enemies.Archetypes;
using WastelandRun.Vehicle;

namespace WastelandRun.Tests.Enemies
{
    [TestFixture]
    public class DuneSkimmerTests
    {
        [Test]
        public void test_dune_skimmer_chassis_matches_gdd_stat_block()
        {
            // Arrange + Act
            var skimmer = VehicleFactory.FromChassis(DuneSkimmer.BuildChassis());

            // Assert — slot HP per biome-1-enemy-roster.md §Archetype 1 / Stat Block
            Assert.That(skimmer.Chassis,                          Is.EqualTo(ChassisType.Skimmer));
            Assert.That(skimmer.GetSlotMaxHp(SlotType.Weapon),    Is.EqualTo(6));
            Assert.That(skimmer.GetSlotMaxHp(SlotType.Engine),    Is.EqualTo(6));
            Assert.That(skimmer.GetSlotMaxHp(SlotType.Mobility),  Is.EqualTo(8));
            Assert.That(skimmer.GetSlotMaxHp(SlotType.Frame),     Is.EqualTo(12));
            Assert.That(skimmer.MaxArmor,                         Is.EqualTo(0),
                "Skimmer is a raider — no armor contribution.");
            Assert.That(skimmer.IsDead,                           Is.False);
        }

        [Test]
        public void test_dune_skimmer_brain_has_three_intents_with_gdd_weights()
        {
            EnemyBrain brain = DuneSkimmer.BuildBrain();
            var specs = brain.ActivePool.Specs;

            Assert.That(specs.Count, Is.EqualTo(3));
            Assert.That(FindByName(specs, "Ram").BaseWeight,          Is.EqualTo(50));
            Assert.That(FindByName(specs, "Scatter Shot").BaseWeight, Is.EqualTo(20));
            Assert.That(FindByName(specs, "Tailgate").BaseWeight,     Is.EqualTo(30));
        }

        [Test]
        public void test_tailgate_weight_zeros_when_enemy_already_ahead()
        {
            EnemyBrain brain = DuneSkimmer.BuildBrain();
            IntentSpec tailgate = FindByName(brain.ActivePool.Specs, "Tailgate");

            var skimmer = VehicleFactory.FromChassis(DuneSkimmer.BuildChassis());
            var player  = TestPlayerFixtures.SpawnPlayer();

            float aheadWeight  = tailgate.ComputeEffectiveWeight(
                TestContexts.Brain(skimmer, player, CombatPosition.Ahead));
            float behindWeight = tailgate.ComputeEffectiveWeight(
                TestContexts.Brain(skimmer, player, CombatPosition.Behind));

            Assert.That(aheadWeight,  Is.EqualTo(0f),
                "Tailgate is pointless when the skimmer is already ahead.");
            Assert.That(behindWeight, Is.GreaterThan(0f),
                "Tailgate must be available from the Behind position.");
        }

        [Test]
        public void test_ram_bias_doubles_when_player_frame_is_degraded()
        {
            EnemyBrain brain = DuneSkimmer.BuildBrain();
            IntentSpec ram = FindByName(brain.ActivePool.Specs, "Ram");

            var skimmer = VehicleFactory.FromChassis(DuneSkimmer.BuildChassis());
            var player  = TestPlayerFixtures.SpawnPlayer();

            // Functional baseline.
            float baseline = ram.ComputeEffectiveWeight(TestContexts.Brain(skimmer, player));
            Assert.That(baseline, Is.EqualTo(50f));

            // Push player's Frame into Degraded (Hp must be > 0 and <= 50% of MaxHp = 10).
            // Frame has 4 armor + 20 HP. Hit with 4 armor + 11 HP = 15 damage card.
            player.ApplyDamage(SlotType.Frame, 15, DamageSource.Card);
            Assert.That(player.GetDamageState(SlotType.Frame), Is.EqualTo(DamageState.Degraded),
                "Test setup: player frame should now be Degraded.");

            // Same modifier applies × 2.0.
            float boosted = ram.ComputeEffectiveWeight(TestContexts.Brain(skimmer, player));
            Assert.That(boosted, Is.EqualTo(100f),
                "Ram should double when target Frame is Degraded.");
        }

        [Test]
        public void test_ram_intent_resolves_eight_damage_through_armor_then_hp()
        {
            // F-VP2 pipeline: Frame slot armor consumed first, remainder to Hp.
            var skimmer = VehicleFactory.FromChassis(DuneSkimmer.BuildChassis());
            var player  = TestPlayerFixtures.SpawnPlayer(frameArmor: 4);

            int armorBefore = player.CurrentArmor;
            int hpBefore    = player.GetSlotHp(SlotType.Frame);

            var ram = new DamageIntent("Ram", baseDamage: 8);
            ram.Resolve(TestContexts.Resolve(skimmer, player, SlotType.Frame));

            // 8 dmg → 4 absorbed by armor → 4 to Hp.
            Assert.That(player.CurrentArmor,             Is.EqualTo(armorBefore - 4));
            Assert.That(player.GetSlotHp(SlotType.Frame), Is.EqualTo(hpBefore - 4));
        }

        [Test]
        public void test_skimmer_brain_picks_an_intent_with_deterministic_seed()
        {
            // Smoke test the brain end-to-end with seeded RNG.
            // We don't assert which intent — only that one was picked and the resolved
            // slot is sane (the FixedSlot retarget always returns Frame).
            EnemyBrain brain = DuneSkimmer.BuildBrain();
            var skimmer  = VehicleFactory.FromChassis(DuneSkimmer.BuildChassis());
            var player   = TestPlayerFixtures.SpawnPlayer();

            PickedIntent picked = brain.ChooseIntent(
                TestContexts.Brain(skimmer, player, CombatPosition.Ahead, turnIndex: 1),
                new Random(42));

            Assert.That(picked,                     Is.Not.Null);
            Assert.That(picked.Intent,              Is.Not.Null);
            Assert.That(picked.ResolvedTargetSlot,  Is.EqualTo(SlotType.Frame),
                "FixedSlot(Frame) retarget should always pick Frame.");
            Assert.That(picked.EnrageBonus,         Is.EqualTo(0),
                "Turn 1 < EnrageTurn 8 → no bonus yet.");
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private static IntentSpec FindByName(System.Collections.Generic.IReadOnlyList<IntentSpec> specs, string name)
        {
            foreach (IntentSpec s in specs)
                if (s.Name == name) return s;
            throw new AssertionException($"No intent spec named '{name}' in pool.");
        }
    }
}

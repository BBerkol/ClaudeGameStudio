// WastelandRun.Tests.Enemies — dredge_test.cs
// Verifies the Biome 1 Boss archetype: phase pools, Phase 2 trigger at 60% Frame HP,
// strip-disables-intent (self slot offline ⇒ corresponding intent gated to 0),
// Composite Shred dealing damage to both Weapon AND Frame in one resolution,
// Javelin Hook applying damage + Stunned together.
// GDD source: design/gdd/biome-1-enemy-roster.md §Archetype 3.

using System.Collections.Generic;
using NUnit.Framework;
using WastelandRun.Enemies;
using WastelandRun.Enemies.Archetypes;
using WastelandRun.Vehicle;

namespace WastelandRun.Tests.Enemies
{
    [TestFixture]
    public class DredgeTests
    {
        [Test]
        public void test_dredge_chassis_matches_collapsed_four_slot_gdd_mapping()
        {
            var dredge = VehicleFactory.FromChassis(Dredge.BuildChassis());

            Assert.That(dredge.Chassis,                         Is.EqualTo(ChassisType.Dredge));
            Assert.That(dredge.GetSlotMaxHp(SlotType.Weapon),   Is.EqualTo(18));
            Assert.That(dredge.GetSlotMaxHp(SlotType.Engine),   Is.EqualTo(22));
            Assert.That(dredge.GetSlotMaxHp(SlotType.Mobility), Is.EqualTo(22));
            Assert.That(dredge.GetSlotMaxHp(SlotType.Frame),    Is.EqualTo(28));

            // Armor pool: Frame contributes 14 (chest + back folded), Engine contributes 5.
            Assert.That(dredge.MaxArmor,     Is.EqualTo(19));
            Assert.That(dredge.CurrentArmor, Is.EqualTo(19));
        }

        [Test]
        public void test_phase_1_pool_contains_only_ram_sweep_and_taunt()
        {
            IntentPool phase1 = Dredge.BuildPhase1Pool();
            var names = NamesOf(phase1.Specs);

            Assert.That(phase1.Specs.Count, Is.EqualTo(3));
            Assert.That(names, Does.Contain("Ram"));
            Assert.That(names, Does.Contain("Sweep"));
            Assert.That(names, Does.Contain("Taunt"));

            Assert.That(names, Does.Not.Contain("Shred"),
                "Shred is Phase 2 only.");
            Assert.That(names, Does.Not.Contain("Javelin Hook"));
        }

        [Test]
        public void test_phase_2_pool_contains_shred_javelin_flail_and_bulldoze()
        {
            IntentPool phase2 = Dredge.BuildPhase2Pool();
            var names = NamesOf(phase2.Specs);

            Assert.That(phase2.Specs.Count, Is.EqualTo(4));
            Assert.That(names, Does.Contain("Shred"));
            Assert.That(names, Does.Contain("Javelin Hook"));
            Assert.That(names, Does.Contain("Spike Flail"));
            Assert.That(names, Does.Contain("Bulldoze"));
        }

        [Test]
        public void test_should_enter_phase_2_triggers_at_or_below_sixty_percent_frame_hp()
        {
            var dredge = VehicleFactory.FromChassis(Dredge.BuildChassis());
            // Frame MaxHp = 28; 60% = 16.8 → trigger at Hp <= 16.8 i.e. ≤ 16 (integers).
            int maxHp = dredge.GetSlotMaxHp(SlotType.Frame);

            Assert.That(Dredge.ShouldEnterPhase2(dredge), Is.False,
                "Full HP, plenty of armor — Phase 1.");

            // Burn through all armor (19) then chip Frame HP down to exactly the threshold.
            // Damage > armor will push into Hp. 28 + 19 = 47 to reach Hp 0; we want Hp = 17 (>60%).
            // Frame currently has 19 armor and 28 HP. Strip armor + 11 HP = 30 damage → Hp = 17.
            dredge.ApplyDamage(SlotType.Frame, 30, DamageSource.Card);
            Assert.That(dredge.GetSlotHp(SlotType.Frame), Is.EqualTo(17));
            Assert.That((float)dredge.GetSlotHp(SlotType.Frame) / maxHp, Is.GreaterThan(0.60f));
            Assert.That(Dredge.ShouldEnterPhase2(dredge), Is.False,
                "Hp 17/28 = 60.7% — above threshold, still Phase 1.");

            // One more hit to push Hp = 16 (≤ 60%).
            dredge.ApplyDamage(SlotType.Frame, 1, DamageSource.Card);
            Assert.That(dredge.GetSlotHp(SlotType.Frame), Is.EqualTo(16));
            Assert.That(Dredge.ShouldEnterPhase2(dredge), Is.True,
                "Hp 16/28 = 57.1% — at/below threshold, enter Phase 2.");
        }

        [Test]
        public void test_phase_2_strip_disables_shred_when_dredge_weapon_offline()
        {
            var dredge = VehicleFactory.FromChassis(Dredge.BuildChassis());
            var player = TestPlayerFixtures.SpawnPlayer();

            IntentPool phase2 = Dredge.BuildPhase2Pool();
            IntentSpec shred = FindByName(phase2.Specs, "Shred");

            // Weapon intact: Shred has positive weight (assumes player in front for RequiresAhead).
            float weightHealthy = shred.ComputeEffectiveWeight(
                TestContexts.Brain(dredge, player, CombatPosition.Ahead));
            Assert.That(weightHealthy, Is.GreaterThan(0f));

            // Strip the Dredge's own Weapon offline.
            dredge.ApplyDamage(SlotType.Weapon, 100, DamageSource.Card);
            Assert.That(dredge.GetDamageState(SlotType.Weapon), Is.EqualTo(DamageState.Offline));

            float weightStripped = shred.ComputeEffectiveWeight(
                TestContexts.Brain(dredge, player, CombatPosition.Ahead));
            Assert.That(weightStripped, Is.EqualTo(0f),
                "Weapon offline → Shred is no longer available.");
        }

        [Test]
        public void test_phase_2_spike_flail_requires_behind_position()
        {
            var dredge = VehicleFactory.FromChassis(Dredge.BuildChassis());
            var player = TestPlayerFixtures.SpawnPlayer();

            IntentSpec flail = FindByName(Dredge.BuildPhase2Pool().Specs, "Spike Flail");

            float fromAhead  = flail.ComputeEffectiveWeight(
                TestContexts.Brain(dredge, player, CombatPosition.Ahead));
            float fromBehind = flail.ComputeEffectiveWeight(
                TestContexts.Brain(dredge, player, CombatPosition.Behind));

            Assert.That(fromAhead,  Is.EqualTo(0f),
                "Spike Flail requires Behind — Ahead gates it out.");
            Assert.That(fromBehind, Is.EqualTo(25f),
                "From Behind, Flail uses its full base weight.");
        }

        [Test]
        public void test_shred_composite_damages_weapon_and_frame_in_one_resolution()
        {
            var dredge = VehicleFactory.FromChassis(Dredge.BuildChassis());
            var player = TestPlayerFixtures.SpawnPlayer(frameArmor: 4);

            int weaponHpBefore = player.GetSlotHp(SlotType.Weapon);
            int frameHpBefore  = player.GetSlotHp(SlotType.Frame);
            int armorBefore    = player.CurrentArmor;

            IntentSpec shredSpec = FindByName(Dredge.BuildPhase2Pool().Specs, "Shred");
            IEnemyIntent shred   = shredSpec.Builder();

            // Resolved target = Weapon (the declared slot for Shred). FixedSlotDamageIntent
            // inside the composite hits Frame regardless.
            shred.Resolve(TestContexts.Resolve(dredge, player, SlotType.Weapon));

            // Weapon: 12 dmg straight to Hp (non-Frame, no armor in the way).
            Assert.That(player.GetSlotHp(SlotType.Weapon),
                Is.EqualTo(weaponHpBefore - 12).Or.EqualTo(0),
                "Weapon takes the first hit of Shred.");

            // Frame: 12 dmg → 4 absorbed by armor, 8 → Hp.
            Assert.That(player.CurrentArmor,              Is.EqualTo(armorBefore - 4));
            Assert.That(player.GetSlotHp(SlotType.Frame), Is.EqualTo(frameHpBefore - 8));
        }

        [Test]
        public void test_taunt_applies_marked_to_player_frame()
        {
            var dredge = VehicleFactory.FromChassis(Dredge.BuildChassis());
            var player = TestPlayerFixtures.SpawnPlayer();

            IntentSpec tauntSpec = FindByName(Dredge.BuildPhase1Pool().Specs, "Taunt");
            tauntSpec.Builder().Resolve(TestContexts.Resolve(dredge, player, SlotType.Frame));

            IReadOnlyList<StatusInstance> frameStatuses = player.GetSlotStatuses(SlotType.Frame);
            Assert.That(frameStatuses.Count, Is.EqualTo(1));
            Assert.That(frameStatuses[0].Type, Is.EqualTo(StatusType.Marked));
        }

        [Test]
        public void test_javelin_hook_damages_frame_and_applies_stunned()
        {
            var dredge = VehicleFactory.FromChassis(Dredge.BuildChassis());
            var player = TestPlayerFixtures.SpawnPlayer(frameArmor: 4);

            int armorBefore   = player.CurrentArmor;
            int frameHpBefore = player.GetSlotHp(SlotType.Frame);

            IntentSpec javelinSpec = FindByName(Dredge.BuildPhase2Pool().Specs, "Javelin Hook");
            javelinSpec.Builder().Resolve(TestContexts.Resolve(dredge, player, SlotType.Frame));

            // 8 dmg → 4 absorbed by armor, 4 → Hp.
            Assert.That(player.CurrentArmor,              Is.EqualTo(armorBefore - 4));
            Assert.That(player.GetSlotHp(SlotType.Frame), Is.EqualTo(frameHpBefore - 4));

            // StatusIntent with overrideTargetSlot=null falls back to ctx.ResolvedTargetSlot
            // (here: Frame) — Stunned lands on the Frame slot, not vehicle-level.
            IReadOnlyList<StatusInstance> frameStatuses = player.GetSlotStatuses(SlotType.Frame);
            Assert.That(frameStatuses.Count,   Is.EqualTo(1));
            Assert.That(frameStatuses[0].Type, Is.EqualTo(StatusType.Stunned));
            Assert.That(player.GetStatuses(),  Is.Empty,
                "Stunned is not vehicle-level in current intent wiring.");
        }

        [Test]
        public void test_dredge_enrage_uses_turn_eight_base_four_with_default_escalation()
        {
            // GDD biome-1-enemy-roster.md §Archetype 3: EnrageBaseBonusOverride = 4.
            // Escalation uses card-combat-system.md R7 default of +1/turn.
            EnemyBrain brain = Dredge.BuildBrain();

            Assert.That(brain.Enrage.EnrageTurn, Is.EqualTo(8));
            Assert.That(brain.Enrage.BaseBonus,  Is.EqualTo(4));
            Assert.That(brain.Enrage.Escalation, Is.EqualTo(1));

            Assert.That(brain.Enrage.ComputeBonus(turnIndex: 7),  Is.EqualTo(0));
            Assert.That(brain.Enrage.ComputeBonus(turnIndex: 8),  Is.EqualTo(4), "Activation turn — base.");
            Assert.That(brain.Enrage.ComputeBonus(turnIndex: 10), Is.EqualTo(6), "Base 4 + 2 turns × +1 = 6.");
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

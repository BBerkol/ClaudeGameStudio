// WastelandRun.Tests.Enemies — test-support.cs
// Shared fixture helpers: builds a minimal player vehicle so enemy intents have
// a real IVehicleView/IVehicleMutator to mutate during damage-flow tests.

using System.Collections.Generic;
using WastelandRun.Enemies;
using WastelandRun.Vehicle;

namespace WastelandRun.Tests.Enemies
{
    /// <summary>
    /// Lightweight player chassis used as a damage target across enemy tests.
    /// Scout chassis, Frame ArmorContribution = 4 so the F-VP2 pipeline has armor
    /// to consume before Hp damage starts (matching V&amp;P GDD pipeline shape).
    /// </summary>
    public static class TestPlayerFixtures
    {
        public const string PartIdWeapon   = "test_player_weapon";
        public const string PartIdEngine   = "test_player_engine";
        public const string PartIdMobility = "test_player_mobility";
        public const string PartIdFrame    = "test_player_frame";

        public static IChassisData BuildScoutChassis(int frameArmor = 4)
        {
            var weapon   = new FixturePart(PartIdWeapon,   SlotType.Weapon,   ChassisType.Scout, maxPlating: 2);
            var engine   = new FixturePart(PartIdEngine,   SlotType.Engine,   ChassisType.Scout, maxPlating: 2);
            var mobility = new FixturePart(PartIdMobility, SlotType.Mobility, ChassisType.Scout, maxPlating: 2);
            var frame    = new FixturePart(PartIdFrame,    SlotType.Frame,    ChassisType.Scout,
                                            maxPlating: 0, armorContribution: frameArmor);

            var slotMaxHp = new Dictionary<SlotType, int>
            {
                { SlotType.Weapon,    8 },
                { SlotType.Engine,    8 },
                { SlotType.Mobility, 10 },
                { SlotType.Frame,    20 },
            };

            var starterParts = new Dictionary<SlotType, IPartData>
            {
                { SlotType.Weapon,   weapon   },
                { SlotType.Engine,   engine   },
                { SlotType.Mobility, mobility },
                { SlotType.Frame,    frame    },
            };

            return new FixtureChassis(ChassisType.Scout, slotMaxHp, starterParts);
        }

        public static WastelandRun.Vehicle.Vehicle SpawnPlayer(int frameArmor = 4) =>
            VehicleFactory.FromChassis(BuildScoutChassis(frameArmor));
    }

    /// <summary>
    /// Builds an <see cref="EnemyBrainContext"/> with sensible defaults for tests
    /// that only care about a subset of the inputs.
    /// </summary>
    public static class TestContexts
    {
        public static EnemyBrainContext Brain(
            IVehicleView self,
            IVehicleView target,
            CombatPosition position = CombatPosition.Ahead,
            int turnIndex = 1) =>
            new EnemyBrainContext(self, target, position, turnIndex);

        public static EnemyResolveContext Resolve(
            IVehicleMutator self,
            IVehicleMutator target,
            SlotType slot,
            int enrageBonus = 0,
            int turnIndex = 1,
            CombatPosition position = CombatPosition.Ahead) =>
            new EnemyResolveContext(self, target, slot, enrageBonus, turnIndex, position);
    }
}

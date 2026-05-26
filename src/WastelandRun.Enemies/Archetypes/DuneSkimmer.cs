// WastelandRun.Enemies — Archetypes/DuneSkimmer.cs
// Biome 1 Raider — DifficultyScore 0.090. Pool: Ram, Scatter Shot, Tailgate.
// GDD source: design/gdd/biome-1-enemy-roster.md §Archetype 1.

using System.Collections.Generic;
using WastelandRun.Vehicle;

namespace WastelandRun.Enemies.Archetypes
{
    /// <summary>
    /// Dune Skimmer fixture — small single-rider raider bike, the player's first opponent.
    /// 4 slots (matches existing data model): Weapon, Engine, Mobility, Frame.
    /// Stat block per biome-1-enemy-roster.md §Archetype 1 / Stat Block.
    /// </summary>
    public static class DuneSkimmer
    {
        public const string PartIdWeapon   = "skimmer_speargun";
        public const string PartIdEngine   = "skimmer_engine";
        public const string PartIdMobility = "skimmer_wheels";
        public const string PartIdFrame    = "skimmer_tubeframe";

        public static IChassisData BuildChassis()
        {
            var weapon   = new FixturePart(PartIdWeapon,   SlotType.Weapon,   ChassisType.Skimmer, maxPlating: 1);
            var engine   = new FixturePart(PartIdEngine,   SlotType.Engine,   ChassisType.Skimmer, maxPlating: 1);
            var mobility = new FixturePart(PartIdMobility, SlotType.Mobility, ChassisType.Skimmer, maxPlating: 1);
            var frame    = new FixturePart(PartIdFrame,    SlotType.Frame,    ChassisType.Skimmer, maxPlating: 0, armorContribution: 0);

            // MaxHpOverride[hull_0]=12 folds in the legacy MaxArmorContribution[Frame]=4
            // per the GDD ADR-0007 mapping note (line 96-97).
            var slotMaxHp = new Dictionary<SlotType, int>
            {
                { SlotType.Weapon,   6  },
                { SlotType.Engine,   6  },
                { SlotType.Mobility, 8  },
                { SlotType.Frame,    12 },
            };

            var starterParts = new Dictionary<SlotType, IPartData>
            {
                { SlotType.Weapon,   weapon   },
                { SlotType.Engine,   engine   },
                { SlotType.Mobility, mobility },
                { SlotType.Frame,    frame    },
            };

            return new FixtureChassis(ChassisType.Skimmer, slotMaxHp, starterParts);
        }

        /// <summary>
        /// Default Biome 1 brain: FixedSlot Frame retarget, no Enrage override (turn 8, +2/turn).
        /// </summary>
        public static EnemyBrain BuildBrain()
        {
            var ram = new IntentSpec(
                name: "Ram",
                baseWeight: 50,
                declaredTargetSlot: SlotType.Frame,
                builder: () => new DamageIntent("Ram", baseDamage: 8),
                modifiers: new WeightModifier[]
                {
                    WeightModifiers.TargetSlotDegraded(SlotType.Frame, 2.0f),
                    WeightModifiers.ZeroIfAllTargetSlotsOffline(),
                });

            var scatterShot = new IntentSpec(
                name: "Scatter Shot",
                baseWeight: 20,
                declaredTargetSlot: SlotType.Weapon,
                builder: () => new DamageIntent("Scatter Shot", baseDamage: 6),
                modifiers: new WeightModifier[]
                {
                    WeightModifiers.ZeroIfTargetSlotOffline(SlotType.Weapon),
                    WeightModifiers.ZeroIfAllTargetSlotsOffline(),
                });

            var tailgate = new IntentSpec(
                name: "Tailgate",
                baseWeight: 30,
                declaredTargetSlot: SlotType.Frame,  // declared slot doesn't matter — no damage
                builder: () => new UtilityIntent("Tailgate"),
                modifiers: new WeightModifier[]
                {
                    WeightModifiers.ZeroIfEnemyAhead(),
                    WeightModifiers.ZeroIfAllTargetSlotsOffline(),
                });

            var pool = new IntentPool(new[] { ram, scatterShot, tailgate });
            var retarget = RetargetPolicy.Fixed(SlotType.Frame);
            return new EnemyBrain(pool, retarget); // default Enrage (turn 8, +2/turn)
        }
    }
}

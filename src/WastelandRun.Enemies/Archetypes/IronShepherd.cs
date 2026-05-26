// WastelandRun.Enemies — Archetypes/IronShepherd.cs
// Biome 1 Elite — DifficultyScore 0.257. Pool: Ram, Armor Rend, Flank, Reinforce.
// Enrage turn 6 (override, base +3 / escalation +3 per turn).
// GDD: design/gdd/biome-1-enemy-roster.md §Archetype 2.

using System.Collections.Generic;
using WastelandRun.Vehicle;

namespace WastelandRun.Enemies.Archetypes
{
    /// <summary>
    /// Iron Shepherd fixture — mid-biome Elite armored buggy. PriorityList retarget
    /// teaches Engine-first targeting. 5 → 4 slot mapping: the GDD's 5th "reinforced"
    /// slot is folded into Frame MaxHp per the 2026-05-26 collapse decision.
    /// Stat block per biome-1-enemy-roster.md §Archetype 2 / Stat Block.
    /// </summary>
    public static class IronShepherd
    {
        public const string PartIdWeapon   = "shepherd_rotary_cannon";
        public const string PartIdEngine   = "shepherd_twin_stacks";
        public const string PartIdMobility = "shepherd_steel_wheels";
        public const string PartIdFrame    = "shepherd_full_plating";

        public static IChassisData BuildChassis()
        {
            var weapon   = new FixturePart(PartIdWeapon,   SlotType.Weapon,   ChassisType.Shepherd, maxPlating: 2);
            // Engine maxPlating reflects the legacy Armor budget that's now mid-band defence.
            var engine   = new FixturePart(PartIdEngine,   SlotType.Engine,   ChassisType.Shepherd, maxPlating: 2);
            var mobility = new FixturePart(PartIdMobility, SlotType.Mobility, ChassisType.Shepherd, maxPlating: 2);
            // Frame gets the elite plating's armor contribution.
            var frame    = new FixturePart(PartIdFrame,    SlotType.Frame,    ChassisType.Shepherd, maxPlating: 0, armorContribution: 10);

            // MaxHpOverride[hull_0]=28 + 5-slot reinforced slot folded into Frame.
            var slotMaxHp = new Dictionary<SlotType, int>
            {
                { SlotType.Weapon,   10 },
                { SlotType.Engine,   14 },
                { SlotType.Mobility, 14 },
                { SlotType.Frame,    28 },
            };

            var starterParts = new Dictionary<SlotType, IPartData>
            {
                { SlotType.Weapon,   weapon   },
                { SlotType.Engine,   engine   },
                { SlotType.Mobility, mobility },
                { SlotType.Frame,    frame    },
            };

            return new FixtureChassis(ChassisType.Shepherd, slotMaxHp, starterParts);
        }

        public static EnemyBrain BuildBrain()
        {
            // Base pool — turns 1..5.
            var ram = new IntentSpec(
                name: "Ram",
                baseWeight: 40,
                declaredTargetSlot: SlotType.Engine,
                builder: () => new DamageIntent("Ram", baseDamage: 10),
                modifiers: new WeightModifier[]
                {
                    WeightModifiers.TargetSlotDegraded(SlotType.Engine, 2.5f),
                    WeightModifiers.ZeroIfAllTargetSlotsOffline(),
                });

            var armorRend = new IntentSpec(
                name: "Armor Rend",
                baseWeight: 30,
                declaredTargetSlot: SlotType.Engine,
                builder: () => new StatusIntent("Armor Rend", StatusType.Corroded, duration: 3, stacks: 1, overrideTargetSlot: SlotType.Engine),
                modifiers: new WeightModifier[]
                {
                    WeightModifiers.ZeroIfTargetSlotOffline(SlotType.Engine),
                    WeightModifiers.ZeroIfAllTargetSlotsOffline(),
                });

            var flank = new IntentSpec(
                name: "Flank",
                baseWeight: 20,
                declaredTargetSlot: SlotType.Mobility,
                builder: () => new DamageIntent("Flank", baseDamage: 12),
                positionReq: PositionRequirement.RequiresBehind,
                modifiers: new WeightModifier[]
                {
                    WeightModifiers.ZeroIfAllTargetSlotsOffline(),
                });

            var reinforce = new IntentSpec(
                name: "Reinforce",
                baseWeight: 20,
                declaredTargetSlot: SlotType.Frame,
                builder: () => new DefendIntent("Reinforce", armorGain: 5),
                modifiers: new WeightModifier[]
                {
                    WeightModifiers.ZeroIfSelfArmorFull(),
                });

            var basePool = new IntentPool(new[] { ram, armorRend, flank, reinforce });

            // Retarget: Engine-first cascade per the GDD.
            var retarget = RetargetPolicy.PriorityListOf(
                SlotType.Engine, SlotType.Frame, SlotType.Weapon, SlotType.Mobility);

            // Enrage override — turn 6: base +3, escalation +3/turn (6→3, 7→6, 8→9).
            var enrage = new EnrageConfig(enrageTurn: 6, baseBonus: 3, escalation: 3);

            return new EnemyBrain(basePool, retarget, enrage);
        }

        /// <summary>
        /// Enrage pool — pool swap at turn 6. Removes Reinforce + Armor Rend;
        /// adds Double Ram. Caller does the swap (e.g., combat loop checks turn at end-of-turn).
        /// </summary>
        public static IntentPool BuildEnragePool()
        {
            var ram = new IntentSpec(
                name: "Ram (Enraged)",
                baseWeight: 60,
                declaredTargetSlot: SlotType.Engine,
                builder: () => new DamageIntent("Ram", baseDamage: 10),
                modifiers: new WeightModifier[]
                {
                    WeightModifiers.TargetSlotDegraded(SlotType.Engine, 2.0f),
                    WeightModifiers.ZeroIfAllTargetSlotsOffline(),
                });

            var doubleRam = new IntentSpec(
                name: "Double Ram",
                baseWeight: 40,
                declaredTargetSlot: SlotType.Frame,
                builder: () => new DamageIntent("Double Ram", baseDamage: 8),
                modifiers: new WeightModifier[]
                {
                    WeightModifiers.ZeroIfAllTargetSlotsOffline(),
                });

            return new IntentPool(new[] { ram, doubleRam });
        }
    }
}

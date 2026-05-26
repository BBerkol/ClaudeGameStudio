// WastelandRun.Enemies — Archetypes/Dredge.cs
// Biome 1 Boss — DifficultyScore 0.423. Two-phase: Phase 1 (HP>60%), Phase 2 (HP≤60%).
// 10-slot GDD layout collapsed to 4 slots per the 2026-05-26 mapping decision:
//   Weapon (minigun)   ← weapon_0
//   Engine (twin blisters) ← engine_0, armor contribution folded in
//   Mobility (rear flail + wheels) ← mobility_0
//   Frame (reinforced cab) ← hull_0 + armor_chest + armor_back HP folded in (no separate armor pool)
// Strip-disables-intent rules respect collapsed slots: Weapon offline → Shred disabled, etc.
// GDD: design/gdd/biome-1-enemy-roster.md §Archetype 3.

using System.Collections.Generic;
using WastelandRun.Vehicle;

namespace WastelandRun.Enemies.Archetypes
{
    public static class Dredge
    {
        public const string PartIdWeapon   = "dredge_minigun";
        public const string PartIdEngine   = "dredge_twin_blisters";
        public const string PartIdMobility = "dredge_flail_wheels";
        public const string PartIdFrame    = "dredge_reinforced_cab";

        /// <summary>Phase transition trigger — Phase 2 activates when Hull HP ≤ this fraction of MaxHp.</summary>
        public const float Phase2HpFraction = 0.60f;

        public static IChassisData BuildChassis()
        {
            var weapon   = new FixturePart(PartIdWeapon,   SlotType.Weapon,   ChassisType.Dredge, maxPlating: 3);
            var engine   = new FixturePart(PartIdEngine,   SlotType.Engine,   ChassisType.Dredge, maxPlating: 3, armorContribution: 5);
            // Mobility holds the spike-flail and the wheels; legacy MaxArmorContribution[Mobility]=5 folds to maxPlating.
            var mobility = new FixturePart(PartIdMobility, SlotType.Mobility, ChassisType.Dredge, maxPlating: 3);
            // Frame inherits the boss's two armor slots (chest + back) as ArmorContribution.
            var frame    = new FixturePart(PartIdFrame,    SlotType.Frame,    ChassisType.Dredge, maxPlating: 0, armorContribution: 14);

            // MaxHpOverride[hull_0]=28 (GDD line 263) — armor budget lives in the Frame ArmorContribution
            // rather than folded into HP (Dredge needs the armor pool to demonstrate Phase 1 chip-strip).
            var slotMaxHp = new Dictionary<SlotType, int>
            {
                { SlotType.Weapon,   18 },
                { SlotType.Engine,   22 },
                { SlotType.Mobility, 22 },
                { SlotType.Frame,    28 },
            };

            var starterParts = new Dictionary<SlotType, IPartData>
            {
                { SlotType.Weapon,   weapon   },
                { SlotType.Engine,   engine   },
                { SlotType.Mobility, mobility },
                { SlotType.Frame,    frame    },
            };

            return new FixtureChassis(ChassisType.Dredge, slotMaxHp, starterParts);
        }

        /// <summary>
        /// Brain seeded with the Phase 1 pool. Caller (combat loop / test) swaps
        /// <c>brain.ActivePool = BuildPhase2Pool()</c> when <see cref="ShouldEnterPhase2"/> returns true.
        /// </summary>
        public static EnemyBrain BuildBrain()
        {
            var pool = BuildPhase1Pool();
            var retarget = RetargetPolicy.PriorityListOf(
                SlotType.Frame, SlotType.Weapon, SlotType.Engine, SlotType.Mobility);
            // GDD: EnrageBaseBonusOverride = 4. Escalation uses GDD R7 default of +1/turn.
            var enrage = new EnrageConfig(enrageTurn: 8, baseBonus: 4, escalation: 1);
            return new EnemyBrain(pool, retarget, enrage);
        }

        /// <summary>
        /// Phase 1: Ram, Sweep, Taunt. Bulldoze/Shred/Javelin/Flail are Phase 2 only.
        /// Taunt applies Marked (next Frame-targeted hit deals +3) per GDD line 280-282.
        /// </summary>
        public static IntentPool BuildPhase1Pool()
        {
            var ram = new IntentSpec(
                name: "Ram",
                baseWeight: 55,
                declaredTargetSlot: SlotType.Frame,
                builder: () => new DamageIntent("Ram", baseDamage: 10),
                modifiers: new WeightModifier[]
                {
                    WeightModifiers.TargetSlotDegraded(SlotType.Frame, 1.5f),
                    WeightModifiers.ZeroIfSelfSlotOffline(SlotType.Engine),
                    WeightModifiers.ZeroIfAllTargetSlotsOffline(),
                });

            var sweep = new IntentSpec(
                name: "Sweep",
                baseWeight: 25,
                declaredTargetSlot: SlotType.Weapon,
                builder: () => new DamageIntent("Sweep", baseDamage: 7),
                modifiers: new WeightModifier[]
                {
                    WeightModifiers.ZeroIfSelfSlotOffline(SlotType.Weapon),
                    WeightModifiers.ZeroIfAllTargetSlotsOffline(),
                });

            var taunt = new IntentSpec(
                name: "Taunt",
                baseWeight: 20,
                declaredTargetSlot: SlotType.Frame,
                builder: () => new StatusIntent("Taunt", StatusType.Marked, duration: 1, stacks: 1, overrideTargetSlot: SlotType.Frame));

            return new IntentPool(new[] { ram, sweep, taunt });
        }

        /// <summary>
        /// Phase 2: Shred (Weapon + Frame), Javelin Hook (Frame damage + Stunned),
        /// Spike Flail (Frame, requires-behind), Bulldoze (Frame fallback).
        /// Each strip-gates intent via ZeroIfSelfSlotOffline — Weapon offline ⇒ no Shred, etc.
        /// </summary>
        public static IntentPool BuildPhase2Pool()
        {
            var shred = new IntentSpec(
                name: "Shred",
                baseWeight: 35,
                declaredTargetSlot: SlotType.Weapon,  // primary slot for retarget; composite handles the second hit
                builder: () => new CompositeIntent("Shred",
                    new DamageIntent("Shred:Subsystem", baseDamage: 12),
                    new FixedSlotDamageIntent("Shred:Frame", SlotType.Frame, baseDamage: 12, addEnrageBonus: false)),
                positionReq: PositionRequirement.RequiresAhead,
                modifiers: new WeightModifier[]
                {
                    WeightModifiers.ZeroIfSelfSlotOffline(SlotType.Weapon),
                    WeightModifiers.ZeroIfAllTargetSlotsOffline(),
                });

            var javelin = new IntentSpec(
                name: "Javelin Hook",
                baseWeight: 25,
                declaredTargetSlot: SlotType.Frame,
                builder: () => new CompositeIntent("Javelin Hook",
                    new DamageIntent("Javelin Hook:Damage", baseDamage: 8),
                    new StatusIntent("Javelin Hook:Stunned", StatusType.Stunned, duration: 1, stacks: 1, overrideTargetSlot: null)),
                positionReq: PositionRequirement.RequiresAhead,
                modifiers: new WeightModifier[]
                {
                    WeightModifiers.ZeroIfSelfSlotOffline(SlotType.Engine),
                    WeightModifiers.ZeroIfAllTargetSlotsOffline(),
                });

            var flail = new IntentSpec(
                name: "Spike Flail",
                baseWeight: 25,
                declaredTargetSlot: SlotType.Frame,
                builder: () => new DamageIntent("Spike Flail", baseDamage: 10),
                positionReq: PositionRequirement.RequiresBehind,
                modifiers: new WeightModifier[]
                {
                    WeightModifiers.ZeroIfSelfSlotOffline(SlotType.Mobility),
                    WeightModifiers.ZeroIfAllTargetSlotsOffline(),
                });

            var bulldoze = new IntentSpec(
                name: "Bulldoze",
                baseWeight: 15,
                declaredTargetSlot: SlotType.Frame,
                builder: () => new DamageIntent("Bulldoze", baseDamage: 14),
                modifiers: new WeightModifier[]
                {
                    WeightModifiers.ZeroIfSelfSlotOffline(SlotType.Frame),
                    WeightModifiers.ZeroIfAllTargetSlotsOffline(),
                });

            return new IntentPool(new[] { shred, javelin, flail, bulldoze });
        }

        /// <summary>
        /// Phase trigger: Phase 2 begins when the Dredge's Frame HP drops to ≤ 60% of MaxHp.
        /// Combat-loop check: call at end of each turn after damage resolution.
        /// </summary>
        public static bool ShouldEnterPhase2(IVehicleView dredge)
        {
            int maxHp = dredge.GetSlotMaxHp(SlotType.Frame);
            if (maxHp <= 0) return false; // empty frame — vehicle is broken anyway
            int currentHp = dredge.GetSlotHp(SlotType.Frame);
            return ((float)currentHp / maxHp) <= Phase2HpFraction;
        }
    }
}

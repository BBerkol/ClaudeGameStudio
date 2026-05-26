// WastelandRun.Enemies — FixturePart.cs
// Minimal IPartData implementation used by hardcoded enemy chassis fixtures.
// Mirrors PartDefinitionSO at the data layer without requiring a ScriptableObject.

using System.Collections.Generic;
using WastelandRun.Vehicle;

namespace WastelandRun.Enemies
{
    /// <summary>
    /// Hardcoded part fixture for enemy archetypes. Carries the same shape as a
    /// PartDefinitionSO at the data layer — IPartData — but constructed in code.
    /// Enemy parts do not grant cards (the player does not equip them), so
    /// GrantedCardIds defaults to empty.
    /// </summary>
    public sealed class FixturePart : IPartData
    {
        public string PartId { get; }
        public SlotType SlotType { get; }
        public IReadOnlyList<ChassisType> CompatibleChassis { get; }
        public PartRarity Rarity { get; }
        public IReadOnlyList<string> GrantedCardIds { get; }
        public IReadOnlyList<StatModifier> StatModifiers { get; }
        public int MaxPlating { get; }
        public int ArmorContribution { get; }

        public FixturePart(
            string partId,
            SlotType slotType,
            ChassisType compatibleChassis,
            int maxPlating = 0,
            int armorContribution = 0,
            PartRarity rarity = PartRarity.Common,
            IReadOnlyList<StatModifier> statModifiers = null,
            IReadOnlyList<string> grantedCardIds = null)
        {
            PartId            = partId;
            SlotType          = slotType;
            CompatibleChassis = new[] { compatibleChassis };
            Rarity            = rarity;
            GrantedCardIds    = grantedCardIds ?? System.Array.Empty<string>();
            StatModifiers     = statModifiers  ?? System.Array.Empty<StatModifier>();
            MaxPlating        = maxPlating;
            ArmorContribution = armorContribution;
        }
    }

    /// <summary>
    /// Hardcoded chassis fixture for enemy archetypes. Mirrors ChassisDefinitionSO
    /// at the data layer — IChassisData — but constructed in code. Slot count is
    /// fixed at 4 (ADR-0005 legacy data model); per the user's choice on 2026-05-26,
    /// 5-slot and 10-slot enemies (Shepherd, Dredge) are mapped down to 4 slots
    /// with extras folded into existing slot HP. See biome-1-enemy-roster.md
    /// "ADR-0007 mapping" header for the original layout.
    /// </summary>
    public sealed class FixtureChassis : IChassisData
    {
        public ChassisType Chassis { get; }
        public IReadOnlyDictionary<SlotType, int> SlotMaxHp { get; }
        public float DegradedThresholdPct { get; }
        public IReadOnlyDictionary<SlotType, IPartData> StarterParts { get; }
        public IReadOnlyList<string> StarterDeckCardIds { get; }
        public int MaxEnergyBase { get; }
        public int MaxHandSizeBase { get; }

        public FixtureChassis(
            ChassisType chassis,
            IReadOnlyDictionary<SlotType, int> slotMaxHp,
            IReadOnlyDictionary<SlotType, IPartData> starterParts,
            float degradedThresholdPct = 0.5f,
            int maxEnergyBase = 3,
            int maxHandSizeBase = 5,
            IReadOnlyList<string> starterDeckCardIds = null)
        {
            Chassis              = chassis;
            SlotMaxHp            = slotMaxHp;
            StarterParts         = starterParts;
            DegradedThresholdPct = degradedThresholdPct;
            MaxEnergyBase        = maxEnergyBase;
            MaxHandSizeBase      = maxHandSizeBase;
            StarterDeckCardIds   = starterDeckCardIds ?? System.Array.Empty<string>();
        }
    }
}

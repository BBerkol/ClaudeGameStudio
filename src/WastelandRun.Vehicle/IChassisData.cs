// WastelandRun.Vehicle — IChassisData.cs
// POCO-native read interface for chassis definition data.
// Implemented by ChassisDefinitionSO (WastelandRun.Gameplay) and test fakes.
// Authority: ADR-0005 §Key Interfaces / IChassisData

using System.Collections.Generic;

namespace WastelandRun.Vehicle
{
    /// <summary>
    /// Read-only projection of a chassis definition, decoupled from ScriptableObject.
    /// All runtime code depends on this interface — never on ChassisDefinitionSO directly.
    /// ADR-0005 §Key Interfaces.
    ///
    /// Usage example:
    ///   IChassisData chassis = catalog.GetChassis(ChassisType.Scout);
    ///   Vehicle v = VehicleFactory.FromChassis(chassis, catalog);
    /// </summary>
    public interface IChassisData
    {
        /// <summary>Which chassis variant this definition describes.</summary>
        ChassisType Chassis { get; }

        /// <summary>
        /// Maximum Hp for each slot on this chassis.
        /// Must contain exactly 4 entries — one per <see cref="SlotType"/> (R11).
        /// </summary>
        IReadOnlyDictionary<SlotType, int> SlotMaxHp { get; }

        /// <summary>
        /// Threshold below which a slot is considered Degraded rather than Functional.
        /// Expressed as a fraction in [0..1] (e.g. 0.5 = 50% of MaxHp).
        /// DamageState.Degraded when: 0 &lt; Hp &lt;= DegradedThresholdPct * MaxHp.
        /// </summary>
        float DegradedThresholdPct { get; }

        /// <summary>
        /// Parts pre-installed when a Vehicle is created from this chassis.
        /// Must contain exactly 4 entries — one per <see cref="SlotType"/>.
        /// All parts must have CompatibleChassis containing this Chassis (R11).
        /// Σ StarterParts.GrantedCardIds.Count must be &lt;= 4 (R11).
        /// </summary>
        IReadOnlyDictionary<SlotType, IPartData> StarterParts { get; }

        /// <summary>
        /// Card IDs forming the starter deck granted at chassis selection.
        /// Must contain exactly 10 entries (R11).
        /// </summary>
        IReadOnlyList<string> StarterDeckCardIds { get; }

        /// <summary>Base maximum energy per turn before part modifiers.</summary>
        int MaxEnergyBase { get; }

        /// <summary>Base maximum hand size before part modifiers.</summary>
        int MaxHandSizeBase { get; }
    }
}

// WastelandRun.Vehicle — IPartData.cs
// POCO-native read interface for part definition data.
// Implemented by PartDefinitionSO (WastelandRun.Gameplay) and test fakes.
// Authority: ADR-0005 §Key Interfaces / IPartData

using System.Collections.Generic;

namespace WastelandRun.Vehicle
{
    /// <summary>
    /// Read-only projection of a part definition, decoupled from ScriptableObject.
    /// All runtime code depends on this interface — never on PartDefinitionSO directly.
    /// ADR-0005 §Key Interfaces / ADR-0002 noEngineReferences discipline.
    ///
    /// Usage example:
    ///   IPartData part = catalog.GetById("scout-saw-blade");
    ///   mutator.InstallPart(SlotType.Weapon, part);
    /// </summary>
    public interface IPartData
    {
        /// <summary>Unique string key for this part. Used as the DTO serialization key.</summary>
        string PartId { get; }

        /// <summary>Which vehicle slot this part occupies.</summary>
        SlotType SlotType { get; }

        /// <summary>Chassis types that accept this part. Must not be empty (R11 validator).</summary>
        IReadOnlyList<ChassisType> CompatibleChassis { get; }

        /// <summary>Rarity tier. Controls the number of granted cards (R11).</summary>
        PartRarity Rarity { get; }

        /// <summary>
        /// Card definition IDs granted to the deck when this part is installed.
        /// Removed from all deck zones (deck / hand / discard) on RemovePart.
        /// Count must match rarity: Common=1, Uncommon=2, Rare=3, Legendary=3 (R11).
        /// </summary>
        IReadOnlyList<string> GrantedCardIds { get; }

        /// <summary>Stat modifiers this part contributes while installed.</summary>
        IReadOnlyList<StatModifier> StatModifiers { get; }

        /// <summary>
        /// Maximum plating stacks this part can hold.
        /// Must be 0 for Frame slot parts (R11).
        /// Valid range: [0..5] (R11).
        /// </summary>
        int MaxPlating { get; }

        /// <summary>
        /// How much this part contributes to the vehicle's MaxArmor.
        /// Must be &gt;= 0 (R11). MaxArmor = Σ InstalledPart.ArmorContribution.
        /// </summary>
        int ArmorContribution { get; }
    }
}

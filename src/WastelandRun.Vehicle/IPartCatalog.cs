// WastelandRun.Vehicle — IPartCatalog.cs
// Stateless part lookup interface.
// Implemented by AddressablesPartCatalog (WastelandRun.Gameplay) and test fakes.
// Authority: ADR-0005 R9, R10 / §Key Interfaces

using System.Collections.Generic;

namespace WastelandRun.Vehicle
{
    /// <summary>
    /// Stateless read interface for part lookups.
    /// The catalog owns no internal RNG — caller-owned System.Random is used by
    /// Loot and Map systems for seeded selection (ADR-0003 determinism discipline).
    ///
    /// Runtime implementation: AddressablesPartCatalog loads all PartDefinitionSO
    /// via Addressables label "part-definitions" at scene init, caches by PartId.
    /// ADR-0005 R9 / ADR-0003.
    ///
    /// Usage example:
    ///   IReadOnlyList&lt;IPartData&gt; candidates =
    ///       catalog.GetParts(SlotType.Weapon, PartRarity.Common, ChassisType.Scout);
    ///   IPartData chosen = candidates[rng.Next(candidates.Count)]; // caller owns rng
    /// </summary>
    public interface IPartCatalog
    {
        /// <summary>
        /// Returns all parts matching the given (slot, rarity, chassis) filter.
        /// Stateless — no RNG, no side effects. ADR-0003: seeded selection is
        /// the caller's responsibility (Loot / Map pass their System.Random).
        ///
        /// Returns an empty list (not null) if:
        ///   - No parts match the filter.
        ///   - Chassis is HeavyTruck and no HeavyTruck parts are authored (MVP stub — ADR-0005 R14).
        ///   - The catalog was initialized on an empty Addressables group.
        /// ADR-0005 R9.
        /// </summary>
        IReadOnlyList<IPartData> GetParts(SlotType slot, PartRarity rarity, ChassisType chassis);

        /// <summary>
        /// Returns the part with the given PartId, or null if not found.
        /// Used by save-load to reconstitute IPartData from persisted string keys (ADR-0004).
        /// ADR-0005 §Key Interfaces.
        /// </summary>
        IPartData GetById(string partId);

        /// <summary>
        /// Projects the stat changes of installing <paramref name="candidate"/> into
        /// <paramref name="slot"/> on <paramref name="vehicle"/> without mutating state.
        /// Pure read helper for the compare-panel UI.
        ///
        /// Calculates:
        ///   - ProjectedMaxArmor = Σ ArmorContribution (existing minus displaced, plus candidate).
        ///   - ProjectedStats = composed modifiers for all StatType values after the swap.
        ///   - CardsAdded / CardsRemoved for deck-preview display.
        ///
        /// Throws <see cref="PartIncompatibleException"/> if the candidate is not compatible
        /// with the vehicle chassis or target slot (mirrors InstallPart validation).
        /// ADR-0005 R10.
        /// </summary>
        VehicleStatePreview PreviewInstall(IVehicleView vehicle, SlotType slot, IPartData candidate);
    }
}

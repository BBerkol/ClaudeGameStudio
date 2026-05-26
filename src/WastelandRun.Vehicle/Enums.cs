// WastelandRun.Vehicle — Enums.cs
// All shared enumeration types for the Vehicle sub-model.
// Lives in the noEngineReferences assembly; no UnityEngine.* dependencies.
// Authority: ADR-0005 §Decision/Key Interfaces

namespace WastelandRun.Vehicle
{
    /// <summary>
    /// The four typed data slots on every vehicle.
    /// Slot count is a compile-time invariant; there is no runtime add/remove.
    /// ADR-0005 R2 — exactly 4 slots (Weapon, Engine, Mobility, Frame).
    /// NOTE: "Armor" is NOT a slot — it is a vehicle-level derived stat. See <see cref="Vehicle.MaxArmor"/>.
    /// </summary>
    public enum SlotType
    {
        Weapon,
        Engine,
        Mobility,
        Frame
    }

    /// <summary>
    /// Damage state of a single slot, derived from current Hp — never stored directly.
    /// ADR-0005 R4 / V&amp;P GDD R3:
    ///   Empty      = no part installed (InstalledPartId == null)
    ///   Functional = Hp &gt; DegradedThreshold% of MaxHp
    ///   Degraded   = 0 &lt; Hp &lt;= DegradedThreshold%
    ///   Offline    = Hp == 0
    /// </summary>
    public enum DamageState
    {
        Empty,
        Functional,
        Degraded,
        Offline
    }

    /// <summary>
    /// Source of a damage application, used by the Frame damage pipeline (F-VP2).
    /// DOT / Status damage bypasses vehicle Armor; Card and Environment damage does not.
    /// ADR-0005 §ADR-0001 Amendments row 4 / V&amp;P GDD line 178.
    /// </summary>
    public enum DamageSource
    {
        Card,
        Status,
        Environment
    }

    /// <summary>
    /// Operation type for a stat modifier entry.
    /// Composition order: (base + ΣAdd) × ΠMultiply, Override short-circuits.
    /// ADR-0005 R5.
    /// </summary>
    public enum StatOperation
    {
        Add,
        Multiply,
        Override
    }

    /// <summary>
    /// Rarity tier of a part. Controls the number of granted cards per ADR-0005 R11:
    ///   Common=1, Uncommon=2, Rare=3, Legendary=3.
    /// </summary>
    public enum PartRarity
    {
        Common,
        Uncommon,
        Rare,
        Legendary
    }

    /// <summary>
    /// Chassis variants available in the game.
    /// Player chassis: Scout, Assault, HeavyTruck (HeavyTruck reserved post-EA, ADR-0005 R14).
    /// Enemy chassis: Skimmer (Biome 1 raider), Shepherd (Biome 1 elite), Dredge (Biome 1 boss).
    /// Enemy values added 2026-05-26 to support biome-1-enemy-roster.md fixture build.
    /// </summary>
    public enum ChassisType
    {
        Scout,
        Assault,
        HeavyTruck,
        Skimmer,
        Shepherd,
        Dredge
    }

    /// <summary>
    /// Canonical stat identifiers shared across all assemblies.
    /// Single source of truth — downstream systems import from WastelandRun.Vehicle.
    /// Extending this enum requires an ADR amendment (ADR-0005 §Decision).
    ///
    /// Usage examples:
    ///   StatComposer.Compose(baseEnergy, vehicle.GetModifiers(StatType.MaxEnergyBase))
    ///   StatModifier(StatType.CardBaseDamageOut, StatOperation.Multiply, 1.25f)
    /// </summary>
    public enum StatType
    {
        // --- Card Combat (ADR-0002 / card-combat-system.md) ---

        /// <summary>Base energy available per turn before modifiers.</summary>
        MaxEnergyBase,

        /// <summary>Maximum number of cards that can be held in hand.</summary>
        MaxHandSize,

        /// <summary>Multiplier applied to all outgoing card damage values.</summary>
        CardBaseDamageOut,

        /// <summary>Multiplier applied to all repair-effect card values.</summary>
        CardBaseRepair,

        /// <summary>Number of cards drawn at the start of each turn.</summary>
        DrawPerTurn,

        // --- Scrap Economy (scrap-economy.md — pending ADR-0008) ---

        /// <summary>Buy-price modifier at merchants and the chopshop.</summary>
        ShopPriceMultiplier,

        /// <summary>Sell-price modifier when scrapping parts for Scrap currency.</summary>
        ScrapYieldMultiplier,

        // --- Vehicle-derived (informational) ---

        /// <summary>
        /// Exposed read-only via <see cref="IVehicleView.MaxArmor"/>.
        /// Not a direct stat-modifier target; listed here for registry completeness.
        /// </summary>
        ArmorMax
    }

    /// <summary>
    /// Status effect type identifiers. Placeholder for ADR-0007 (Status Effects).
    /// These four values match the ADR-0005 §Key Interfaces stub; ADR-0007 will
    /// amend this enum with full semantics and tick behaviour.
    ///
    /// Do NOT add new values here without an ADR amendment.
    /// </summary>
    public enum StatusType
    {
        Burning,
        Corroded,
        Overcharged,
        Stalled,
        Marked,
        Stunned
    }
}

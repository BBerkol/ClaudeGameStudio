// WastelandRun.Vehicle — Exceptions.cs
// Domain exceptions for the Vehicle sub-model.
// Authority: ADR-0005 §Implementation Guidelines / Exception locations

using System;

namespace WastelandRun.Vehicle
{
    /// <summary>
    /// Thrown by <see cref="IVehicleMutator.InstallPart"/> at runtime when:
    ///   (a) the part's CompatibleChassis list does not include the vehicle's chassis, OR
    ///   (b) the part's SlotType does not match the target slot.
    ///
    /// This is a recoverable exception — callers (Loot, Economy) decide whether to
    /// discard the drop or surface a UI error. ADR-0005 §Exception locations.
    ///
    /// Example:
    ///   try { mutator.InstallPart(SlotType.Weapon, weaponPart); }
    ///   catch (PartIncompatibleException ex) { /* surface to player */ }
    /// </summary>
    public sealed class PartIncompatibleException : Exception
    {
        /// <summary>The slot the install was attempted on.</summary>
        public SlotType Slot { get; }

        /// <summary>The part that failed the compatibility check.</summary>
        public string PartId { get; }

        /// <summary>The chassis of the vehicle that rejected the part.</summary>
        public ChassisType VehicleChassis { get; }

        public PartIncompatibleException(
            SlotType slot,
            string partId,
            ChassisType vehicleChassis,
            string message)
            : base(message)
        {
            Slot           = slot;
            PartId         = partId;
            VehicleChassis = vehicleChassis;
        }
    }

    /// <summary>
    /// Thrown when two or more <see cref="StatModifier"/> entries with
    /// <see cref="StatOperation.Override"/> target the same <see cref="StatType"/>.
    ///
    /// This is an editor-time authoring error and should never occur in a shipped build
    /// (the EditorBuildPreprocessor is the authoritative gate — AC-VP29).
    ///
    /// At runtime, <see cref="StatComposer.Compose"/> throws this as a
    /// defense-in-depth check in case the editor gate was bypassed.
    ///
    /// Example:
    ///   // In OnValidate or StatComposer:
    ///   throw new MultipleOverrideException(StatType.MaxEnergyBase, partId);
    /// </summary>
    public sealed class MultipleOverrideException : Exception
    {
        /// <summary>The stat that has more than one Override modifier.</summary>
        public StatType Stat { get; }

        /// <summary>Optional context identifier (e.g., PartId) for diagnostics.</summary>
        public string SourceId { get; }

        public MultipleOverrideException(StatType stat, string sourceId = null)
            : base($"Multiple Override modifiers found for stat '{stat}' in '{sourceId ?? "unknown"}'. " +
                   "Only one Override per stat is allowed (ADR-0005 R5 / AC-VP29).")
        {
            Stat     = stat;
            SourceId = sourceId;
        }
    }
}

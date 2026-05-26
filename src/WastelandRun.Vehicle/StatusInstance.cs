// WastelandRun.Vehicle — StatusInstance.cs
// Placeholder status instance type. Full semantics owned by ADR-0007 (Status Effects).
// This stub satisfies the IVehicleView.GetStatuses() and IVehicleMutator.ApplyStatus()
// contracts at the data layer without pre-committing ADR-0007's tick/stack design.
//
// ADR-0007 will amend this type. Do NOT add game logic here without an ADR amendment.

namespace WastelandRun.Vehicle
{
    /// <summary>
    /// Represents a single active status effect instance on a vehicle or slot.
    /// Shape is a placeholder per ADR-0005; ADR-0007 (Status Effects) owns the
    /// final design including tick behaviour, stack semantics, and expiry rules.
    ///
    /// Stored in <see cref="Vehicle.ActiveStatuses"/> (vehicle-scoped) and
    /// <see cref="SlotState.Statuses"/> (slot-scoped).
    ///
    /// Usage example:
    ///   IReadOnlyList&lt;StatusInstance&gt; statuses = vehicleView.GetStatuses();
    ///   bool isBurning = statuses.Any(s => s.Type == StatusType.Burning);
    /// </summary>
    public sealed class StatusInstance
    {
        /// <summary>Which status effect this instance represents.</summary>
        public StatusType Type { get; }

        /// <summary>
        /// Remaining duration in turns. 0 means the status expires at end of
        /// the current turn. Semantics finalized by ADR-0007.
        /// </summary>
        public int RemainingDuration { get; internal set; }

        /// <summary>
        /// Current stack count. Semantics (cap, per-tick effect) finalized by ADR-0007.
        /// </summary>
        public int Stacks { get; internal set; }

        /// <summary>
        /// The slot this instance is scoped to, or null if vehicle-level.
        /// ADR-0005 §Key Interfaces / IVehicleView.GetSlotStatuses note.
        /// </summary>
        public SlotType? TargetSlot { get; }

        internal StatusInstance(StatusType type, int duration, int stacks, SlotType? targetSlot)
        {
            Type              = type;
            RemainingDuration = duration;
            Stacks            = stacks;
            TargetSlot        = targetSlot;
        }
    }
}

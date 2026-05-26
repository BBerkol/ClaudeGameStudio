// WastelandRun.Vehicle — SlotState.cs
// Mutable state for one vehicle slot. All mutation is package-internal;
// external readers go through IVehicleView. Mutation goes through Vehicle (IVehicleMutator).
// Authority: ADR-0005 §Key Interfaces / SlotState

using System;
using System.Collections.Generic;

namespace WastelandRun.Vehicle
{
    /// <summary>
    /// Runtime state for a single vehicle slot.
    ///
    /// All fields are mutated exclusively by <see cref="Vehicle"/> (the IVehicleMutator
    /// implementation). External code reads through <see cref="IVehicleView"/> slot methods.
    ///
    /// DamageState is a derived property — never stored. It is recomputed from Hp and
    /// the DegradedThreshold supplied by the chassis. ADR-0005 R4.
    ///
    /// Thread safety: single-threaded by construction (ADR-0002 / ADR-0005).
    /// </summary>
    public sealed class SlotState
    {
        // Supplied once at construction; chassis provides the threshold.
        private readonly float _degradedThresholdPct;

        // Cached at install time to allow MaxArmor recalculation and stat composition
        // without holding a catalog reference in Vehicle. Cleared on RemovePart.
        // ADR-0005 §Implementation Guidelines / Stat composition caching.
        internal int CachedArmorContribution { get; set; }
        internal IReadOnlyList<StatModifier> CachedStatModifiers { get; set; } =
            Array.Empty<StatModifier>();

        // --- Public identity ---

        /// <summary>Which slot this state belongs to.</summary>
        public SlotType Slot { get; }

        // --- Part installation ---

        /// <summary>
        /// PartId of the currently installed part. Null when no part is installed
        /// (DamageState == Empty).
        /// </summary>
        public string PartId { get; internal set; }

        // --- Hp ---

        /// <summary>
        /// Current hit points of the installed part. 0 when Empty or Offline.
        /// Never negative.
        /// </summary>
        public int Hp { get; internal set; }

        /// <summary>
        /// Maximum hit points, set from the chassis definition when a part is installed.
        /// 0 when no part is installed.
        /// </summary>
        public int MaxHp { get; internal set; }

        // --- Plating ---

        /// <summary>
        /// Current plating stacks. Always 0 for Frame slot.
        /// Clamped at MaxPlating on AddPlating.
        /// </summary>
        public int PlatingStacks { get; internal set; }

        /// <summary>
        /// Maximum plating stacks allowed. 0 for Frame slot (R11).
        /// Set from IPartData.MaxPlating on install.
        /// </summary>
        public int MaxPlating { get; internal set; }

        // --- Status effects (slot-scoped) ---

        // Internal list; exposed through IVehicleView.GetSlotStatuses as IReadOnlyList.
        internal List<StatusInstance> Statuses { get; } = new List<StatusInstance>();

        // --- Derived state ---

        /// <summary>
        /// Damage state derived from current Hp.
        /// Computation per ADR-0005 R4 / V&amp;P GDD R3:
        ///   Empty      = PartId == null
        ///   Offline    = Hp == 0  (part present but destroyed)
        ///   Degraded   = Hp &gt; 0 and Hp &lt;= DegradedThreshold% of MaxHp
        ///   Functional = Hp &gt; DegradedThreshold% of MaxHp
        /// </summary>
        public DamageState DamageState
        {
            get
            {
                if (PartId == null)  return DamageState.Empty;
                if (Hp == 0)        return DamageState.Offline;
                if (Hp <= _degradedThresholdPct * MaxHp) return DamageState.Degraded;
                return DamageState.Functional;
            }
        }

        // --- Construction ---

        /// <summary>
        /// Creates an empty slot. Called by Vehicle constructor.
        /// </summary>
        /// <param name="slot">Which slot this state represents.</param>
        /// <param name="degradedThresholdPct">
        /// Fraction of MaxHp below which DamageState becomes Degraded. [0..1].
        /// Supplied by the chassis definition.
        /// </param>
        internal SlotState(SlotType slot, float degradedThresholdPct)
        {
            Slot                  = slot;
            _degradedThresholdPct = degradedThresholdPct;
        }
    }
}

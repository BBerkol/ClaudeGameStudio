// WastelandRun.Vehicle — IVehicleView.cs
// Read-only interface for vehicle state. All downstream readers depend on this.
// Mutation is exclusively through IVehicleMutator (separate interface, whitelisted callers).
// Casting IVehicleView to IVehicleMutator is forbidden — use explicit DI of IVehicleMutator.
// Authority: ADR-0005 §Key Interfaces / ADR-0001 (locked contract)

using System;
using System.Collections.Generic;

namespace WastelandRun.Vehicle
{
    /// <summary>
    /// Read-only view of a vehicle's runtime state.
    /// All systems that only observe vehicle state depend on this interface.
    ///
    /// Mutation is whitelisted and exclusively available through <see cref="IVehicleMutator"/>.
    /// The write-whitelist (Combat, Status Effects, Economy, Loot, Enemy AI) is enforced
    /// by code review — casting this interface to the concrete type to gain mutation access
    /// is a blocking code-review violation.
    ///
    /// Event fire order is specified in ADR-0005 §Implementation Guidelines / Events.
    /// HUD and visual subscribers may rely on the documented ordering.
    ///
    /// Usage example:
    ///   void OnCombatTurn(IVehicleView player, IVehicleView enemy)
    ///   {
    ///       if (player.IsDead) EndCombat(winner: enemy);
    ///       var state = player.GetDamageState(SlotType.Weapon);
    ///   }
    /// </summary>
    public interface IVehicleView
    {
        // --- Identity ---

        /// <summary>Which chassis variant this vehicle uses.</summary>
        ChassisType Chassis { get; }

        // --- Armor ---

        /// <summary>
        /// Derived vehicle-level armor maximum. Not a slot.
        /// MaxArmor = Σ InstalledPart.ArmorContribution (recalculated on install/scrap/Offline).
        /// ADR-0005 R3.
        /// </summary>
        int MaxArmor { get; }

        /// <summary>
        /// Current armor value. Clamped [0, MaxArmor].
        /// Does NOT reset between turns; resets per-combat start (V&amp;P GDD line 34).
        /// </summary>
        int CurrentArmor { get; }

        // --- Life status ---

        /// <summary>
        /// True when the Frame slot's DamageState reaches Offline.
        /// There is no separate Hull Hp pool — Frame destruction ends the vehicle.
        /// V&amp;P GDD / ADR-0005 §Key Interfaces IVehicleView.
        /// </summary>
        bool IsDead { get; }

        // --- Slot reads ---

        /// <summary>
        /// Derived damage state for the given slot.
        /// Never stored directly; computed from current Hp vs thresholds.
        /// ADR-0005 R4 / V&amp;P GDD R3.
        /// </summary>
        DamageState GetDamageState(SlotType slot);

        /// <summary>
        /// The PartId of the installed part, or null if the slot is empty (DamageState.Empty).
        /// ADR-0005 R2 / §Key Interfaces.
        /// </summary>
        string GetPartId(SlotType slot);

        /// <summary>Current Hp of the part in the given slot. 0 when Offline or Empty.</summary>
        int GetSlotHp(SlotType slot);

        /// <summary>MaxHp of the part in the given slot. 0 when Empty.</summary>
        int GetSlotMaxHp(SlotType slot);

        /// <summary>
        /// Current plating stacks on the given slot.
        /// Always 0 for Frame slot (Frame uses Armor instead of Plating).
        /// ADR-0005 R8.
        /// </summary>
        int GetPlating(SlotType slot);

        // --- Status reads ---

        /// <summary>
        /// Vehicle-level active statuses (not scoped to a slot).
        /// V&amp;P GDD line 35: ActiveStatuses is vehicle-scoped.
        /// ADR-0007 will amend StatusInstance shape.
        /// </summary>
        IReadOnlyList<StatusInstance> GetStatuses();

        /// <summary>
        /// Slot-scoped active statuses (e.g., Ignite on Weapon).
        /// Per-slot statuses live on SlotState; surfaced here for callers who
        /// need slot-granular status inspection.
        /// ADR-0007 will amend StatusInstance shape.
        /// </summary>
        IReadOnlyList<StatusInstance> GetSlotStatuses(SlotType slot);

        // --- Stat composition ---

        /// <summary>
        /// Returns the composed stat modifier for the given stat across all installed parts.
        /// Composition: (1.0 + ΣAdd) × ΠMultiply, or the Override value if present.
        /// The caller supplies the base value and multiplies or adds this result themselves,
        /// depending on the stat's semantic (see V&amp;P GDD R8 / line 187).
        ///
        /// Only the modifier is returned, not the final stat value — the base is consumer-defined.
        /// Reads are not cached; called infrequently (combat-turn boundaries only).
        /// ADR-0005 R5.
        ///
        /// Usage example:
        ///   float energy = chassis.MaxEnergyBase * view.GetStatModifier(StatType.MaxEnergyBase);
        /// </summary>
        float GetStatModifier(StatType stat);

        // --- Events ---
        // All events use C# Action delegates. UnityEvent is forbidden in combat systems
        // (technical-preferences: too slow, swallows exceptions). ADR-0005 §Implementation Guidelines.
        // Event fire order: see ADR-0005 §Implementation Guidelines / Events — fire order.

        /// <summary>
        /// Fires when a slot's DamageState changes.
        /// Signature: (slot, fromState, toState) — V&amp;P GDD line 189 / ADR-0005 ADR-0001 Amendments.
        /// Fires once per ApplyDamage call even if multiple thresholds are crossed
        /// (reports final state, not intermediate).
        /// </summary>
        event Action<SlotType, DamageState, DamageState> OnSlotDamageStateChanged;

        /// <summary>
        /// Fires after a part is installed (new granted cards already added to deck pile).
        /// Signature: (slot, newPartId).
        /// </summary>
        event Action<SlotType, string> OnPartInstalled;

        /// <summary>
        /// Fires after a part is removed (granted cards already cleared from all deck zones).
        /// Signature: (slot, removedPartId).
        /// </summary>
        event Action<SlotType, string> OnPartRemoved;

        /// <summary>Fires when MaxArmor changes (install/scrap/Offline of an ArmorContribution part).</summary>
        event Action<int> OnMaxArmorChanged;

        /// <summary>Fires when CurrentArmor changes (damage, repair, or clamp after MaxArmor drop).</summary>
        event Action<int> OnCurrentArmorChanged;

        /// <summary>
        /// Fires when a status is applied.
        /// Signature: (targetSlot-or-null-mapped-to-SlotType, statusType).
        /// Vehicle-level statuses fire with the Frame slot as a sentinel; ADR-0007 will clarify.
        /// V&amp;P GDD line 192.
        /// </summary>
        event Action<SlotType, StatusType> OnStatusApplied;

        /// <summary>
        /// Fires when a status expires naturally or is removed.
        /// V&amp;P GDD line 193.
        /// </summary>
        event Action<SlotType, StatusType> OnStatusExpired;
    }
}

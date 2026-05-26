// WastelandRun.Vehicle — IVehicleMutator.cs
// Whitelisted write interface for vehicle state.
// Callers outside the whitelist are a blocking code-review violation.
// Authority: ADR-0005 §Key Interfaces / ADR-0001 write-whitelist

namespace WastelandRun.Vehicle
{
    /// <summary>
    /// Whitelisted write interface for vehicle state mutation.
    ///
    /// CALLERS (from docs/registry/architecture.yaml write-whitelist):
    ///   - Combat         : ApplyDamage, AddPlating, AddArmor, ApplyStatus, Repair, TickStatuses
    ///   - Status Effects : ApplyDamage (DOT), ApplyStatus, RemoveStatus, TickStatuses
    ///   - Economy        : InstallPart, RemovePart, Repair
    ///   - Loot           : InstallPart
    ///   - Enemy AI       : ApplyStatus (via its own IVehicleMutator ref to the target vehicle)
    ///
    /// Any call-site outside this whitelist is a BLOCKING code review comment.
    ///
    /// Threading: Single-threaded by construction (ADR-0002); all methods are
    /// synchronous on the calling thread. No locks required or provided.
    ///
    /// Usage example:
    ///   // In CombatLoop (whitelisted):
    ///   _playerMutator.ApplyDamage(SlotType.Weapon, 3, DamageSource.Card);
    ///   _playerMutator.Repair(SlotType.Engine, 5, canReviveOffline: false);
    /// </summary>
    public interface IVehicleMutator
    {
        // --- Damage pipeline (F-VP2) ---

        /// <summary>
        /// Applies damage to a slot following the F-VP2 pipeline:
        ///   1. Consume plating: plating_consumed = min(amount, PlatingStacks).
        ///   2. after_plating = amount - plating_consumed.
        ///   3a. Frame slot: subtract from CurrentArmor first (skipped if source == Status),
        ///       then remainder → Hp.
        ///   3b. Non-Frame slot: after_plating → Hp directly.
        ///   4. Clamp Hp >= 0. Check DamageState transition; fire OnSlotDamageStateChanged if crossed.
        ///   5. If transitioned to Offline: remove granted cards from all zones; if Frame → IsDead = true.
        ///
        /// DOT (source == DamageSource.Status) bypasses CurrentArmor on Frame (AC-VP40).
        /// ADR-0005 R8 / §Implementation Guidelines / Damage pipeline.
        /// </summary>
        void ApplyDamage(SlotType slot, int amount, DamageSource source);

        /// <summary>
        /// Restores Hp to a slot.
        /// If <paramref name="canReviveOffline"/> is false, does nothing when the slot is Offline.
        /// If <paramref name="canReviveOffline"/> is true, revives the slot (Hp set to restored amount,
        /// clamped at MaxHp; DamageState transitions out of Offline).
        /// Fires OnSlotDamageStateChanged if the DamageState threshold is crossed.
        /// ADR-0005 §Key Interfaces.
        /// </summary>
        void Repair(SlotType slot, int hpRestored, bool canReviveOffline);

        /// <summary>
        /// Adds plating stacks to a non-Frame slot (Frame slot uses Armor instead).
        /// Clamped at SlotState.MaxPlating. Silently ignored for Frame slots.
        /// ADR-0005 §Key Interfaces.
        /// </summary>
        void AddPlating(SlotType slot, int stacks);

        /// <summary>
        /// Adds to CurrentArmor. Clamped at MaxArmor.
        /// Fires OnCurrentArmorChanged.
        /// ADR-0005 §Key Interfaces.
        /// </summary>
        void AddArmor(int amount);

        // --- Status pipeline ---
        // StatusInstance shape is owned by ADR-0007. These signatures satisfy the
        // V&P GDD R9 line 206 contract; ADR-0007 will amend with tick/stack semantics.

        /// <summary>
        /// Applies a status effect to the vehicle or a specific slot.
        /// Null <paramref name="targetSlot"/> = vehicle-level status.
        /// If the status already exists: ADR-0007 defines stack accumulation vs. refresh.
        /// Fires OnStatusApplied.
        /// ADR-0005 R9 / V&amp;P GDD line 206.
        /// </summary>
        void ApplyStatus(StatusType type, int duration, int stacks, SlotType? targetSlot);

        /// <summary>
        /// Forcibly removes a status before it expires naturally.
        /// Used by Status Effects on slot Offline transitions.
        /// Fires OnStatusExpired.
        /// ADR-0005 §Key Interfaces.
        /// </summary>
        void RemoveStatus(StatusType type, SlotType? targetSlot);

        /// <summary>
        /// End-of-turn bookkeeping: decrements all status durations, expiring those at 0.
        /// DOT ticks internally route through ApplyDamage with DamageSource.Status
        /// (preserving the armor-bypass rule). Fires OnStatusExpired for expired statuses.
        /// ADR-0005 §Key Interfaces.
        /// </summary>
        void TickStatuses();

        // --- Part lifecycle ---

        /// <summary>
        /// Installs a part into the given slot.
        /// Invariants enforced (throws PartIncompatibleException on violation — recoverable):
        ///   - part.CompatibleChassis must contain the vehicle's chassis.
        ///   - part.SlotType must match slot.
        /// Side effects on success (in order per ADR-0005 §Events fire order):
        ///   - Auto-scraps existing part (fires OnPartRemoved, removes old granted cards).
        ///   - Resets Hp = MaxHp, PlatingStacks = 0.
        ///   - Recalculates MaxArmor; clamps CurrentArmor.
        ///   - Adds new granted cards to the deck pile.
        ///   - Fires OnPartInstalled, OnSlotDamageStateChanged(Empty→Functional),
        ///     OnMaxArmorChanged (if changed), OnCurrentArmorChanged (if clamped).
        /// ADR-0005 R6.
        /// </summary>
        void InstallPart(SlotType slot, IPartData part);

        /// <summary>
        /// Permanently removes a part from the slot. Scrap is permanent for this run.
        /// Side effects (in order):
        ///   - Removes granted cards from all deck zones (deck / hand / discard).
        ///   - Sets Hp = 0, PlatingStacks = 0.
        ///   - Recalculates MaxArmor; clamps CurrentArmor.
        ///   - Fires OnPartRemoved, then OnSlotDamageStateChanged(prevState→Empty),
        ///     OnMaxArmorChanged (if changed), OnCurrentArmorChanged (if clamped).
        /// ADR-0005 R7.
        /// </summary>
        void RemovePart(SlotType slot);
    }
}

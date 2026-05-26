// WastelandRun.Vehicle — Vehicle.cs
// Concrete POCO implementing IVehicleView + IVehicleMutator.
// Downstream code depends ONLY on the interfaces — never on this concrete class.
// Authority: ADR-0005 §Decision / §Implementation Guidelines

using System;
using System.Collections.Generic;
using System.Linq;

namespace WastelandRun.Vehicle
{
    /// <summary>
    /// Runtime vehicle state. Plain C# class — no UnityEngine dependencies.
    /// Implements both <see cref="IVehicleView"/> (reads) and <see cref="IVehicleMutator"/> (writes).
    ///
    /// Downstream systems MUST depend on the interface, never the concrete type:
    ///   - CombatLoop, HUD, Enemy AI → <see cref="IVehicleView"/>
    ///   - Combat, Status Effects, Economy, Loot, Enemy AI (target) → <see cref="IVehicleMutator"/>
    ///
    /// Casting <see cref="IVehicleView"/> to <see cref="IVehicleMutator"/> to gain mutation
    /// access is a BLOCKING code-review violation (ADR-0005 §Decision).
    ///
    /// Created exclusively by <see cref="VehicleFactory.FromChassis"/>.
    ///
    /// Thread safety: single-threaded by construction per ADR-0002 / ADR-0005.
    /// </summary>
    public sealed class Vehicle : IVehicleView, IVehicleMutator
    {
        // -----------------------------------------------------------------------
        // Internal state
        // -----------------------------------------------------------------------

        private readonly Dictionary<SlotType, SlotState> _slots;
        private readonly List<StatusInstance>            _activeStatuses = new List<StatusInstance>();

        // IVehicleView exposes slots as IReadOnlyDictionary; cache the wrapper once.
        private readonly IReadOnlyDictionary<SlotType, SlotState> _slotsReadOnly;

        // Granted-card tracking: PartId → list of card IDs granted by that part.
        // Scrap/remove sweeps this to identify which exact cards to pull from deck zones.
        // Full identity tracking (ADR-0005 §Implementation Guidelines / EC-VP11) is refined
        // by ADR-0006 (Card System Data Authoring). For now we track by PartId + CardId list.
        private readonly Dictionary<SlotType, List<string>> _grantedCardsBySlot
            = new Dictionary<SlotType, List<string>>();

        // -----------------------------------------------------------------------
        // IVehicleView — Identity
        // -----------------------------------------------------------------------

        /// <inheritdoc/>
        public ChassisType Chassis { get; }

        // -----------------------------------------------------------------------
        // IVehicleView — Armor
        // -----------------------------------------------------------------------

        /// <inheritdoc/>
        public int MaxArmor { get; private set; }

        /// <inheritdoc/>
        public int CurrentArmor { get; private set; }

        // -----------------------------------------------------------------------
        // IVehicleView — Life status
        // -----------------------------------------------------------------------

        /// <inheritdoc/>
        public bool IsDead { get; private set; }

        // -----------------------------------------------------------------------
        // IVehicleView — Slot reads
        // -----------------------------------------------------------------------

        /// <inheritdoc/>
        public DamageState GetDamageState(SlotType slot) => _slots[slot].DamageState;

        /// <inheritdoc/>
        public string GetPartId(SlotType slot) => _slots[slot].PartId;

        /// <inheritdoc/>
        public int GetSlotHp(SlotType slot) => _slots[slot].Hp;

        /// <inheritdoc/>
        public int GetSlotMaxHp(SlotType slot) => _slots[slot].MaxHp;

        /// <inheritdoc/>
        public int GetPlating(SlotType slot) => _slots[slot].PlatingStacks;

        // -----------------------------------------------------------------------
        // IVehicleView — Status reads
        // -----------------------------------------------------------------------

        /// <inheritdoc/>
        public IReadOnlyList<StatusInstance> GetStatuses() => _activeStatuses;

        /// <inheritdoc/>
        public IReadOnlyList<StatusInstance> GetSlotStatuses(SlotType slot) =>
            _slots[slot].Statuses;

        // -----------------------------------------------------------------------
        // IVehicleView — Stat composition
        // -----------------------------------------------------------------------

        /// <inheritdoc/>
        public float GetStatModifier(StatType stat)
        {
            // Gather all modifiers for this stat from installed parts.
            // Allocation note: this returns a lazily-iterated sequence via LINQ.
            // Called infrequently (combat-turn boundaries); not on the per-frame hot path.
            // If profiling shows allocation pressure, replace with a pre-allocated buffer.
            var modifiers = GatherModifiers(stat);
            return StatComposer.Compose(0f, modifiers);
            // Note: base = 0 here because GetStatModifier returns the modifier contribution,
            // not the final stat value. Callers add/multiply their own base (see IVehicleView doc).
        }

        // -----------------------------------------------------------------------
        // IVehicleView — Events
        // -----------------------------------------------------------------------

        /// <inheritdoc/>
        public event Action<SlotType, DamageState, DamageState> OnSlotDamageStateChanged;

        /// <inheritdoc/>
        public event Action<SlotType, string> OnPartInstalled;

        /// <inheritdoc/>
        public event Action<SlotType, string> OnPartRemoved;

        /// <inheritdoc/>
        public event Action<int> OnMaxArmorChanged;

        /// <inheritdoc/>
        public event Action<int> OnCurrentArmorChanged;

        /// <inheritdoc/>
        public event Action<SlotType, StatusType> OnStatusApplied;

        /// <inheritdoc/>
        public event Action<SlotType, StatusType> OnStatusExpired;

        // -----------------------------------------------------------------------
        // IVehicleMutator — Damage pipeline
        // -----------------------------------------------------------------------

        /// <inheritdoc/>
        public void ApplyDamage(SlotType slot, int amount, DamageSource source)
        {
            if (amount <= 0) return;

            SlotState state = _slots[slot];
            DamageState before = state.DamageState;

            // Step 1: consume plating
            int platingConsumed = Math.Min(amount, state.PlatingStacks);
            state.PlatingStacks -= platingConsumed;
            int afterPlating = amount - platingConsumed;

            if (afterPlating <= 0)
            {
                // Plating absorbed all damage; DamageState cannot have changed.
                return;
            }

            // Step 2: Frame slot — subtract from CurrentArmor first
            //         (skipped if source == Status, i.e., DOT bypass — AC-VP40)
            if (slot == SlotType.Frame && source != DamageSource.Status)
            {
                int armorConsumed = Math.Min(afterPlating, CurrentArmor);
                if (armorConsumed > 0)
                {
                    CurrentArmor -= armorConsumed;
                    OnCurrentArmorChanged?.Invoke(CurrentArmor);
                }
                afterPlating -= armorConsumed;
            }

            if (afterPlating <= 0) return;

            // Step 3: Apply to Hp
            state.Hp = Math.Max(0, state.Hp - afterPlating);

            // Step 4: Check DamageState transition
            DamageState after = state.DamageState;
            if (after != before)
            {
                // Single fire even if multiple thresholds crossed — reports final state.
                OnSlotDamageStateChanged?.Invoke(slot, before, after);

                if (after == DamageState.Offline)
                {
                    HandleSlotOffline(slot, state);
                }
            }
        }

        /// <inheritdoc/>
        public void Repair(SlotType slot, int hpRestored, bool canReviveOffline)
        {
            if (hpRestored <= 0) return;

            SlotState state = _slots[slot];
            if (state.DamageState == DamageState.Empty) return;
            if (state.DamageState == DamageState.Offline && !canReviveOffline) return;

            DamageState before = state.DamageState;
            state.Hp = Math.Min(state.MaxHp, state.Hp + hpRestored);
            DamageState after = state.DamageState;

            if (after != before)
            {
                OnSlotDamageStateChanged?.Invoke(slot, before, after);
            }
        }

        /// <inheritdoc/>
        public void AddPlating(SlotType slot, int stacks)
        {
            if (slot == SlotType.Frame) return; // Frame uses Armor, not Plating.
            if (stacks <= 0) return;

            SlotState state = _slots[slot];
            state.PlatingStacks = Math.Min(state.MaxPlating, state.PlatingStacks + stacks);
        }

        /// <inheritdoc/>
        public void AddArmor(int amount)
        {
            if (amount <= 0) return;
            CurrentArmor = Math.Min(MaxArmor, CurrentArmor + amount);
            OnCurrentArmorChanged?.Invoke(CurrentArmor);
        }

        // -----------------------------------------------------------------------
        // IVehicleMutator — Status pipeline
        // -----------------------------------------------------------------------

        /// <inheritdoc/>
        public void ApplyStatus(StatusType type, int duration, int stacks, SlotType? targetSlot)
        {
            if (targetSlot.HasValue)
            {
                // Slot-scoped status
                StatusInstance existing = _slots[targetSlot.Value].Statuses
                    .Find(s => s.Type == type);

                if (existing != null)
                {
                    // ADR-0007 will define stack/refresh semantics.
                    // Placeholder: refresh duration, add stacks.
                    existing.RemainingDuration = Math.Max(existing.RemainingDuration, duration);
                    existing.Stacks            += stacks;
                }
                else
                {
                    _slots[targetSlot.Value].Statuses.Add(
                        new StatusInstance(type, duration, stacks, targetSlot));
                }
                OnStatusApplied?.Invoke(targetSlot.Value, type);
            }
            else
            {
                // Vehicle-level status
                StatusInstance existing = _activeStatuses.Find(s => s.Type == type);
                if (existing != null)
                {
                    existing.RemainingDuration = Math.Max(existing.RemainingDuration, duration);
                    existing.Stacks            += stacks;
                }
                else
                {
                    _activeStatuses.Add(new StatusInstance(type, duration, stacks, null));
                }
                // Use Frame as sentinel slot for vehicle-level status events (ADR-0005 note).
                OnStatusApplied?.Invoke(SlotType.Frame, type);
            }
        }

        /// <inheritdoc/>
        public void RemoveStatus(StatusType type, SlotType? targetSlot)
        {
            if (targetSlot.HasValue)
            {
                List<StatusInstance> list = _slots[targetSlot.Value].Statuses;
                int idx = list.FindIndex(s => s.Type == type);
                if (idx >= 0)
                {
                    list.RemoveAt(idx);
                    OnStatusExpired?.Invoke(targetSlot.Value, type);
                }
            }
            else
            {
                int idx = _activeStatuses.FindIndex(s => s.Type == type);
                if (idx >= 0)
                {
                    _activeStatuses.RemoveAt(idx);
                    OnStatusExpired?.Invoke(SlotType.Frame, type);
                }
            }
        }

        /// <inheritdoc/>
        public void TickStatuses()
        {
            // Tick vehicle-level statuses
            for (int i = _activeStatuses.Count - 1; i >= 0; i--)
            {
                StatusInstance inst = _activeStatuses[i];
                inst.RemainingDuration--;
                if (inst.RemainingDuration < 0)
                {
                    _activeStatuses.RemoveAt(i);
                    OnStatusExpired?.Invoke(SlotType.Frame, inst.Type);
                }
                // ADR-0007: DOT damage ticks go here via ApplyDamage(DamageSource.Status).
            }

            // Tick slot-scoped statuses
            foreach (SlotType slotType in SlotTypes.All)
            {
                List<StatusInstance> list = _slots[slotType].Statuses;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    StatusInstance inst = list[i];
                    inst.RemainingDuration--;
                    if (inst.RemainingDuration < 0)
                    {
                        list.RemoveAt(i);
                        OnStatusExpired?.Invoke(slotType, inst.Type);
                    }
                    // ADR-0007: DOT damage ticks go here.
                }
            }
        }

        // -----------------------------------------------------------------------
        // IVehicleMutator — Part lifecycle
        // -----------------------------------------------------------------------

        /// <inheritdoc/>
        public void InstallPart(SlotType slot, IPartData part)
        {
            ValidateCompatibility(slot, part);

            SlotState state = _slots[slot];

            // Auto-scrap existing part if present
            if (state.PartId != null)
            {
                ExecuteRemovePart(slot, state);
            }

            // Install new part
            DamageState beforeInstall = state.DamageState; // should be Empty after scrap

            state.PartId        = part.PartId;
            state.MaxHp         = GetChassisMaxHp(slot);    // chassis defines per-slot MaxHp
            state.Hp            = state.MaxHp;
            state.PlatingStacks = 0;
            state.MaxPlating    = part.MaxPlating;

            // Cache the part's contribution data on the SlotState so MaxArmor recalc
            // and GetStatModifier can run without holding a catalog reference.
            // SlotState doc declares these as "cached at install time" (ADR-0005).
            state.CachedArmorContribution = part.ArmorContribution;
            state.CachedStatModifiers     = part.StatModifiers ?? Array.Empty<StatModifier>();

            // Track granted cards for this slot
            _grantedCardsBySlot[slot] = new List<string>(part.GrantedCardIds);
            // ADR-0006 will refine card instance identity; for now we store card IDs.
            // Actual deck manipulation is orchestrated by the caller (CombatLoop / Economy)
            // listening to OnPartInstalled; the Vehicle records which IDs were granted.

            // Recalculate MaxArmor
            int oldMaxArmor = MaxArmor;
            RecalculateMaxArmor();

            // Clamp CurrentArmor if MaxArmor decreased (unusual on install, but correct)
            ClampCurrentArmor(oldMaxArmor);

            // Fire events per ADR-0005 §Events fire order / InstallPart:
            //   3. OnPartInstalled
            OnPartInstalled?.Invoke(slot, part.PartId);

            //   4. OnSlotDamageStateChanged(Empty → Functional)
            DamageState afterInstall = state.DamageState;
            if (afterInstall != beforeInstall)
            {
                OnSlotDamageStateChanged?.Invoke(slot, beforeInstall, afterInstall);
            }

            //   5. OnMaxArmorChanged if changed
            if (MaxArmor != oldMaxArmor)
            {
                OnMaxArmorChanged?.Invoke(MaxArmor);
            }
        }

        /// <inheritdoc/>
        public void RemovePart(SlotType slot)
        {
            SlotState state = _slots[slot];
            if (state.PartId == null) return; // Already empty; no-op.

            ExecuteRemovePart(slot, state);
        }

        // -----------------------------------------------------------------------
        // Construction (internal — use VehicleFactory)
        // -----------------------------------------------------------------------

        /// <summary>
        /// Constructs a Vehicle with pre-built SlotState entries.
        /// Called only by <see cref="VehicleFactory"/>. Do not call directly.
        /// </summary>
        internal Vehicle(
            ChassisType chassis,
            Dictionary<SlotType, SlotState> slots,
            Dictionary<SlotType, int> chassisMaxHpMap)
        {
            Chassis              = chassis;
            _slots               = slots;
            _slotsReadOnly       = slots;
            _chassisMaxHpMap     = chassisMaxHpMap;

            // Grant empty card lists for all slots
            foreach (SlotType s in SlotTypes.All)
                _grantedCardsBySlot[s] = new List<string>();
        }

        // -----------------------------------------------------------------------
        // Internal helpers
        // -----------------------------------------------------------------------

        private readonly Dictionary<SlotType, int> _chassisMaxHpMap;

        private int GetChassisMaxHp(SlotType slot) => _chassisMaxHpMap[slot];

        private void ValidateCompatibility(SlotType slot, IPartData part)
        {
            if (part.SlotType != slot)
            {
                throw new PartIncompatibleException(slot, part.PartId, Chassis,
                    $"Part '{part.PartId}' has SlotType '{part.SlotType}' but target slot is '{slot}'.");
            }

            bool chassisMatch = false;
            foreach (ChassisType c in part.CompatibleChassis)
            {
                if (c == Chassis) { chassisMatch = true; break; }
            }

            if (!chassisMatch)
            {
                throw new PartIncompatibleException(slot, part.PartId, Chassis,
                    $"Part '{part.PartId}' is not compatible with chassis '{Chassis}'.");
            }
        }

        /// <summary>
        /// Executes the removal side effects for the part currently in the given slot.
        /// Does NOT validate — caller is responsible for checking state.PartId != null.
        /// Fire order per ADR-0005 §Events fire order / InstallPart items 1–2:
        ///   1. OnPartRemoved (cards already cleared — callers listen and sweep deck zones)
        ///   2. OnSlotDamageStateChanged(...→ Empty) if the old part wasn't Offline
        /// </summary>
        private void ExecuteRemovePart(SlotType slot, SlotState state)
        {
            string oldPartId   = state.PartId;
            DamageState before = state.DamageState;

            // Clear granted card tracking. Actual deck sweep is done by the subscriber
            // (CombatLoop / Economy) listening to OnPartRemoved.
            _grantedCardsBySlot[slot].Clear();

            // Reset slot fields
            state.PartId        = null;
            state.Hp            = 0;
            state.MaxHp         = 0;
            state.PlatingStacks = 0;
            state.MaxPlating    = 0;
            state.CachedArmorContribution = 0;
            state.CachedStatModifiers     = Array.Empty<StatModifier>();

            int oldMaxArmor = MaxArmor;
            RecalculateMaxArmor();
            ClampCurrentArmor(oldMaxArmor);

            // Fire events
            //   1. OnPartRemoved
            OnPartRemoved?.Invoke(slot, oldPartId);

            //   2. OnSlotDamageStateChanged(prev → Empty)
            if (before != DamageState.Empty)
            {
                OnSlotDamageStateChanged?.Invoke(slot, before, DamageState.Empty);
            }

            //   OnMaxArmorChanged if MaxArmor changed (from part's ArmorContribution)
            if (MaxArmor != oldMaxArmor)
            {
                OnMaxArmorChanged?.Invoke(MaxArmor);
            }

            //   OnCurrentArmorChanged if CurrentArmor was clamped
            // (handled by ClampCurrentArmor — see below)
        }

        /// <summary>
        /// Handles a slot transitioning to Offline.
        /// Called immediately after OnSlotDamageStateChanged fires.
        /// ADR-0005 §Events fire order / damage / if newState == Offline.
        /// </summary>
        private void HandleSlotOffline(SlotType slot, SlotState state)
        {
            // If this part contributed ArmorContribution, MaxArmor must drop.
            // (We don't have IPartData here; MaxArmor recalc checks Hp == 0 → skips.)
            // Recalc reads ArmorContribution via the catalog — but Vehicle is catalog-free.
            // Resolution: ArmorContribution is cached on SlotState at install time.
            // See: we do NOT cache it on SlotState in this design — the slot knows PartId,
            // and the catalog provides ArmorContribution. However Vehicle is catalog-free.
            //
            // ADR-0005 §Architecture: "MaxArmor is cached and invalidated on InstallPart /
            // RemovePart / slot Offline transition only."
            //
            // To support Offline-triggered MaxArmor invalidation without a catalog reference,
            // we cache the ArmorContribution on SlotState at install time.
            // See SlotState._cachedArmorContribution (internal field set by Vehicle.InstallPart).
            //
            // This is already handled: RecalculateMaxArmor() sums only slots where Hp > 0
            // (Offline slots contribute 0). So we just call RecalculateMaxArmor.
            int oldMaxArmor = MaxArmor;
            RecalculateMaxArmor();
            ClampCurrentArmor(oldMaxArmor);

            if (MaxArmor != oldMaxArmor)
            {
                OnMaxArmorChanged?.Invoke(MaxArmor);
            }

            // If Frame goes Offline: vehicle dies
            if (slot == SlotType.Frame)
            {
                IsDead = true;
                // No further events — combat resolution is owned by CombatLoop (ADR-0002).
            }
        }

        /// <summary>
        /// Recalculates and caches MaxArmor = Σ ArmorContribution from installed parts
        /// where the slot is NOT Offline. Offline parts contribute 0 (ADR-0005 §caching note).
        /// Called on install, remove, and slot→Offline transition.
        /// </summary>
        private void RecalculateMaxArmor()
        {
            int total = 0;
            foreach (SlotType slotType in SlotTypes.All)
            {
                SlotState s = _slots[slotType];
                // Only count parts that are installed and not Offline.
                if (s.DamageState != DamageState.Empty && s.DamageState != DamageState.Offline)
                {
                    total += s.CachedArmorContribution;
                }
            }
            MaxArmor = total;
        }

        /// <summary>
        /// Clamps CurrentArmor to [0, MaxArmor] and fires OnCurrentArmorChanged if it changed.
        /// </summary>
        private void ClampCurrentArmor(int previousMaxArmor)
        {
            int clamped = Math.Min(CurrentArmor, MaxArmor);
            if (clamped != CurrentArmor)
            {
                CurrentArmor = clamped;
                OnCurrentArmorChanged?.Invoke(CurrentArmor);
            }
        }

        /// <summary>
        /// Gathers all StatModifiers targeting <paramref name="stat"/> from installed,
        /// non-Offline parts. Returns an enumerable (no allocation at call site).
        /// </summary>
        private IEnumerable<StatModifier> GatherModifiers(StatType stat)
        {
            foreach (SlotType slotType in SlotTypes.All)
            {
                SlotState s = _slots[slotType];
                if (s.DamageState == DamageState.Empty || s.DamageState == DamageState.Offline)
                    continue;

                foreach (StatModifier mod in s.CachedStatModifiers)
                {
                    if (mod.TargetStat == stat) yield return mod;
                }
            }
        }
    }

    // -----------------------------------------------------------------------
    // SlotTypes helper — avoids Enum.GetValues allocation in hot paths
    // -----------------------------------------------------------------------

    /// <summary>
    /// Pre-allocated array of all SlotType values for iteration without allocations.
    /// Used internally by Vehicle to enumerate slots.
    /// </summary>
    internal static class SlotTypes
    {
        public static readonly SlotType[] All =
        {
            SlotType.Weapon,
            SlotType.Engine,
            SlotType.Mobility,
            SlotType.Frame
        };
    }
}

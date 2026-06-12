# Story 004: Part Install/Remove + Event Fire Order + Granted-Card Soft Disable

> **Epic**: Vehicle POCO + Part Catalog
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-05-25

## Context

**GDD**: `design/gdd/vehicle-and-part-architecture.md` + `design/gdd/vehicle-and-part-mechanics.md`
**Requirement**: `TR-vehicle-008`, `TR-vehicle-009`, `TR-vehicle-010`, `TR-vehicle-011`, `TR-vehicle-012`, `TR-vehicle-024`

**ADR Governing Implementation**: ADR-0005 (amended by ADR-0007 for granted-card lifecycle Decision 16)
**ADR Decision Summary**: `InstallPart` writes `InstalledPart`, resets slot HP to `MaxHp`, recalculates any per-slot stat aggregates, adds GrantedCards to the deck (each tagged with `SourceSlotId`), and fires `OnPartInstalled` last. `RemovePart` nulls `InstalledPart`, removes GrantedCards **by exact instance identity** (TR-024), fires `OnPartRemoved`. Event fire order is **state-change → event** (callers see post-mutation state in handlers). **Decision 16 (Granted-Card Soft Disable)**: When a slot transitions to `Offline`, its granted cards remain in the deck/hand/discard but are flagged `IsDisabled = true` (visual grey-out in zone). Repair restores them. Hard removal only happens on scrap **or** when an external source ends (e.g., Dredge Javelin chain cut) — both atomic across deck + hand + discard.

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: GrantedCard instance identity uses `CardInstanceId` (Guid or ulong, ADR-0005). `SourceSlotId` is nullable on `CardData` for cards not granted by parts (starter deck).

**Control Manifest Rules (this layer)**:
- Required: GrantedCards removed by exact instance ID (TR-024), never by archetype — source: ADR-0005
- Required: Decision 16 soft-disable on Offline; cards stay in zones, IsDisabled=true — source: ADR-0007
- Required: Event fire order is state-change → event — source: ADR-0005
- Forbidden: Removing cards by archetype/name match — source: ADR-0005

---

## Acceptance Criteria

- [ ] **AC-1** *(TR-008)*: `InstallPart(slotId, partId, partDef)` writes `InstalledPart = partId`, sets `CurrentHp = MaxHp`, generates a new `CardInstanceId` per entry in `partDef.GrantedCards`, appends each to the deck tagged with `SourceSlotId = slotId`, then fires `OnPartInstalled(slotId, partId)` last. Throws `InvalidPartInstallException` if the slot is non-empty, the part's `CompatibleChassis` doesn't include this chassis, or the part's `PositionRequirement` doesn't match the slot's `Anchor.Position`.
- [ ] **AC-2** *(TR-009)*: `RemovePart(slotId, reason)` reads existing `InstalledPart`, nulls it, removes every card whose `SourceSlotId == slotId` from deck + hand + discard atomically, fires `OnPartRemoved(slotId, partId, reason)` last. `RemovalReason` enum: `Scrapped`, `Replaced`, `ExternalSourceEnded`.
- [ ] **AC-3** *(TR-010)*: No `Vehicle.MaxArmor` field exists. (Carried from story 002 — verified here for the install path.) Installing a part with `ArmorContribution > 0` adds a new `SlotInstance` of `SlotKind.Armor` with `RedirectsTo = slotId` only if the chassis layout already declares an Armor slot for that position. **Install does NOT mutate `IFrameLayout`.**
- [ ] **AC-4** *(TR-011)*: For Armor `SlotInstance`s, `CurrentHp` resets to its `Definition.MaxHp` at combat start (`Vehicle.ResetCombatEphemeral()` method); it persists across turns within a combat but is NOT serialised between combats. Method on `IVehicleMutator`.
- [ ] **AC-5** *(TR-012)*: `SlotInstance.CombatsSurvived` increments +1 per combat-won call (`Vehicle.RecordCombatVictory()`) for every slot that holds an `InstalledPart`. Resets to 0 on `InstallPart` or `RemovePart` for that slot. Read by Scrap Economy.
- [ ] **AC-6** *(TR-024)*: Granted cards carry the part's `CardInstanceId` and `SourceSlotId`. Removal targets exact `CardInstanceId` matches — not card name, not archetype, not display data. If the user has duplicate copies of the same card from two different parts and one part is removed, only the matching copies vanish.
- [ ] **AC-7** *(Decision 16)*: When `OnSlotDamageStateChanged(slotId, *, Offline)` fires (from damage), every card in deck + hand + discard whose `SourceSlotId == slotId` is set `IsDisabled = true`. When the slot transitions Offline → Functional (via Repair), those same cards flip `IsDisabled = false`. NO removal occurs.
- [ ] **AC-8** *(Decision 16)*: Hard removal of granted cards happens **only** when `RemovalReason == Scrapped` OR `RemovalReason == ExternalSourceEnded`. Removal is atomic across deck + hand + discard (no partial state visible to subscribers). `OnPartRemoved` fires after removal.
- [ ] **AC-9**: Event fire order across one `InstallPart` call: (1) write InstalledPart + reset HP; (2) append cards to deck with `SourceSlotId`; (3) fire `OnPartInstalled` once. Handlers reading `vehicle.GetSlot(slotId).InstalledPart` see the new part.
- [ ] **AC-10**: `CardData` (referenced from Card Combat assembly, but documented here): adds a nullable `string? SourceSlotId` field stamped at runtime when the card is granted. Starter-deck cards have `SourceSlotId == null` and are never auto-disabled or auto-removed.
- [ ] **AC-11**: All deck/hand/discard mutations from this story go through `ICardZoneMutator` (interface owned by Card Combat assembly). This story defines the interface contract Vehicle depends on; the implementation is in a later card-combat story.

---

## Implementation Notes

- Atomicity for hard-removal: take a snapshot of card IDs in {deck ∪ hand ∪ discard} where `SourceSlotId == slotId`, then call `ICardZoneMutator.RemoveByIds(snapshot)` in one batch. Single observable transition.
- Soft-disable uses `ICardZoneMutator.SetDisabledByIds(snapshot, true|false)`.
- `Vehicle` subscribes to its OWN `OnSlotDamageStateChanged` event for Decision 16 wiring — internal subscription set up in constructor. This keeps the policy collocated with the data.
- Repair-driven re-enable: when `OnSlotDamageStateChanged(*, Offline, Functional)` fires (from story 003's RepairSlot path), flip `IsDisabled = false` for the slot's granted cards.
- DO NOT re-grant cards on revive. The same `CardInstanceId`s flip back on.

---

## Out of Scope

- Visual grey-out / card disable UI — view layer, separate story.
- Dredge Javelin chain-cut card effect implementation — Card Combat assembly's external-source-ended trigger calls `RemovePart(slotId, ExternalSourceEnded)`.
- Repair cost — Scrap Economy assembly.
- `ICardZoneMutator` implementation (we only define the interface contract here).

---

## QA Test Cases

**Logic story — automated test specs (live under `tests/unit/vehicle/part_install_remove_test.cs`):**

- **AC-1**: install fires events last with new state visible
  - Given: empty slot "weapon_front", part with 2 GrantedCards
  - When: `InstallPart("weapon_front", partId, partDef)` and OnPartInstalled handler reads `GetSlot("weapon_front").InstalledPart`
  - Then: handler sees the new part; deck has 2 new cards with `SourceSlotId = "weapon_front"`
  - Edge cases: install on non-empty slot → InvalidPartInstallException; incompatible chassis → InvalidPartInstallException; position mismatch → InvalidPartInstallException

- **AC-2 / AC-6**: remove targets exact instances
  - Given: two parts installed in different slots, each granting one card with the SAME card name but different `CardInstanceId`s
  - When: `RemovePart("slot_a", Scrapped)`
  - Then: only the card with `SourceSlotId == "slot_a"` is removed; the other copy remains
  - Edge cases: card had moved from deck → hand → discard during play → still removed atomically

- **AC-4**: combat-ephemeral Armor reset
  - Given: vehicle with an Armor slot at 5/10 HP (took damage)
  - When: `ResetCombatEphemeral()` is called
  - Then: Armor slot HP = 10
  - Edge cases: structural slots are NOT reset; PlatingStacks reset to 0

- **AC-5**: CombatsSurvived
  - Given: weapon slot with installed part, CombatsSurvived = 2
  - When: `RecordCombatVictory()`
  - Then: CombatsSurvived = 3
  - Edge cases: subsequent `InstallPart` on same slot resets to 0; `RemovePart` resets to 0

- **AC-7**: soft-disable on Offline
  - Given: part installed in "weapon_front" granting 3 cards (2 in deck, 1 in hand)
  - When: damage takes slot to 0 HP → DamageState.Offline → OnSlotDamageStateChanged fires
  - Then: all 3 cards have `IsDisabled = true`; no card is removed
  - Edge cases: card was discarded mid-turn → still flips disabled; repair to Functional → IsDisabled flips back to false on all 3

- **AC-8**: hard removal atomicity
  - Given: 4 cards from one slot distributed across deck (2), hand (1), discard (1)
  - When: `RemovePart(slotId, Scrapped)`
  - Then: in a single ICardZoneMutator call, all 4 are removed; no subscriber sees a partial state (e.g., deck cleared but hand still holding)
  - Edge cases: `Replaced` removal reason — hard remove (same atomic guarantee); `ExternalSourceEnded` — hard remove

- **AC-9**: event ordering
  - Given: install handler that asserts on `GetSlot(slotId).InstalledPart != null`
  - When: install fires
  - Then: assertion passes (state was set before event)
  - Edge cases: handler that reads deck count sees the new cards

- **AC-10**: starter cards never auto-affected
  - Given: starter deck of 10 cards with `SourceSlotId == null`
  - When: any slot goes Offline OR any RemovePart fires
  - Then: starter cards unaffected (neither disabled nor removed)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/vehicle/part_install_remove_test.cs` — must exist and pass all AC cases
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Stories 001, 002, 003 (damage path drives the Offline transitions that Decision 16 reacts to)
- Unlocks: Card Combat granted-card UI, Scrap Economy install/scrap flows

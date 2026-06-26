# Capture — Slice 9a `VehicleStateDto` (foundation for Rest, Chopshop, Ambush, mid-combat-quit)

**Date:** 2026-06-26
**Scope:** Fourth concrete RunState DTO (`VehicleStateDto`) + snapshot-on-demand adapter (`VehicleStateSerializable`) + a single `Vehicle.RestoreFromSnapshot` rehydrate primitive. Pure save-plumbing slice; zero UI surface, zero design risk, zero gameplay change. Foundation for Slice 9b (Rest encounter) and every future slice whose mechanical effect mutates persistent vehicle state (Chopshop, Event[Ambush] chain, mid-combat-quit-and-resume).
**Companion docs:** ADR-0004 §Decision 1 (distributed schema registry) — Slice 9a registers the fourth `SYSTEM_ID` const (`"run.vehicle_state"`). No ADR amendment expected; this is a vanilla DTO addition under the established pattern.
**Sister slice:** Slice 9b (Rest encounter) ships immediately after 9a green. Slice 9b consumes this DTO transparently — the Rest handler calls `Vehicle.RepairSlot(slotId, amount)`, the per-tick `EnqueueRunStateWrite` snapshots through `VehicleStateSerializable`, and resume restores the post-repair state.

## Final-game picture this slice serves

The save-and-resume promise extends past run-shape (`run.session_core`: NodeMap + RunSeed + RunDeck) to the **vehicle's combat state**. After Slice 9a, a player who takes Combat damage on beacon 3, walks to beacon 4 (a Rest), repairs their Frame, then quit-to-desktop and relaunches — finds the Frame still repaired, the damage on weapon_0 still applied, the armor_0 buffer still at its post-combat depleted value. Today, every reload silently resets the vehicle to chassis-fresh state. That's the tolerated gap that Slice 8 (run-shape only) left open; Slice 9a closes it.

The slice is foundational, not feature-shaped. Three near-future slices need it:
- **Slice 9b (Rest):** Rest's only mechanical output is part-state transitions (Offline → Nominal). Without persistence, those transitions are invisible across reload — a real bug, not polish.
- **Slice 10+ (Chopshop):** Same shape as Rest, with Scrap cost. Same persistence dependency.
- **Slice ~10+ (Event[Ambush]):** Event nodes apply combat damage outside a Combat beacon. Same persistence dependency.
- **Mid-combat quit-and-resume** (deferred, not this slice): would also lean on `VehicleStateDto`. Today combat is single-screen-synchronous; a mid-combat quit is rare-but-not-impossible. The snapshot cadence question is deferred to whenever combat-pause ships.

By landing the save category as its own clean slice, Slice 9b becomes a one-consumer feature on top of a known-stable foundation (TD's recommended 9a/9b split per the 2026-06-26 verdict pasted below).

## What is being added (new code)

**Files added (new):**

- `Assets/Scripts/Save/Dtos/VehicleStateDto.cs` — `IRunStateSerializable`. `SYSTEM_ID = "run.vehicle_state"`, `SCHEMA_VERSION = 1`. Payload: `List<SlotSnapshotDto> Slots`. Self-describing entry per Slice 8b Amendment Q7. `Type DtoType => typeof(VehicleStateDto)` per Slice 8c Amendment Addendum.
- `Assets/Scripts/Save/Dtos/SlotSnapshotDto.cs` — nested DTO (NOT an `IRunStateSerializable`). One per slot. Payload locked to the per-slot persistent state surface enumerated below.
- `Assets/Scripts/Save/Adapters/VehicleStateSerializable.cs` — snapshot-on-demand adapter mirroring `RunDeckSerializable`. Holds `Func<RunState>` live source; `ToDto()` projects `RunState.PlayerVehicle` through `VehicleStateDto.From`. `FromDto` captures `LastLoaded` for whatever resume gate joins it (Slice 9a does NOT extend the `run.session_core` group — see Decision Q3 below).
- `Assets/Tests/EditMode/Save/VehicleStateDto_round_trip_test.cs` — build → DTO → JSON → DTO → reconstruct, deep equality on every slot field. Covers Scout (small-frame, 5 slots + armor_0) and at least one Dredge-shaped fixture (exposable slots if cheap; otherwise just multi-slot non-armor coverage).
- `Assets/Tests/EditMode/Save/VehicleStateDto_wire_format_test.cs` — locks the canonical JSON literal for a known fixture. Ordinal sort at every depth.
- `Assets/Tests/EditMode/Save/VehicleStateSerializable_test.cs` — adapter contract: SystemId/SchemaVersion/DtoType forwarding, ctor null-source throw, snapshot-on-demand fresh DTOs, live-source mutation reflection, `LastLoaded` capture, wrong-type FromDto throw, null-source ToDto throw.
- `Assets/Tests/EditMode/Combat/Vehicle_RestoreFromSnapshot_test.cs` — locks the rehydrate primitive: build vehicle, install parts, take damage, snapshot, build new vehicle via Layout, restore from snapshot, assert per-slot state equality. Covers Hp + MaxHp + ArmorContribution + InstalledPartId + InstalledPartDisplayName + CorrodedStacks + armor_0 buffer.

**Files modified:**

- `Assets/Scripts/Combat/Vehicle.cs` — adds one public method:
  - `public void RestoreFromSnapshot(IReadOnlyList<SlotSnapshotDto> snapshots)` — walks `_byId`, finds each persisted slot by `SlotId`, calls a new `internal void SlotInstance.RestoreFromSnapshot(...)` per slot, **does NOT call `RecomputeArmorPool`** (the snapshot already carries the correct `armor_0.MaxHp` and `Hp`; a recompute would reset Hp to MaxHp, undoing depleted buffer state). Doc-comment makes the no-recompute discipline explicit.
- `Assets/Scripts/Combat/SlotInstance.cs` — adds one `internal` method:
  - `internal void RestoreFromSnapshot(int hp, int maxHp, int armorContribution, string partId, string partName, int corrodedStacks, bool hasPart)` — single-shot setter for the persistent state surface, bypasses install-path validation. `internal` keeps the back-door narrow; only `Vehicle.RestoreFromSnapshot` calls it. The existing `SetHp` / `SetMaxHp` internals remain; the new method is the rehydrate-only entry.
- `Assets/Scripts/CombatView/SaveBootstrap.cs` — registers `VehicleStateSerializable` alongside the existing three. Reads its `LastLoaded` after `LoadRunState()` returns. Passes the loaded `VehicleStateDto` to `host.Initialize(...)` (signature gains a fourth parameter).
- `Assets/Scripts/CombatView/RunSceneHost.cs` — `Initialize` signature gains `VehicleStateDto loadedVehicleState`. `BeginRunFromLoaded` signature unchanged at the Slice 9a level (Q3: vehicle restore happens **after** `BeginRunFromLoaded` constructs the fresh-shape vehicle from the chassis SO; see "Rehydrate sequencing" below).
- `Assets/Scripts/Combat/AssemblyInfo.cs` — `[assembly: InternalsVisibleTo("WastelandRun.Save")]` if not already granted, so `SlotSnapshotDto.From` / `VehicleStateDto.From` can read internal-only fields. (Verify before commit; if InternalsVisibleTo already covers Save, no change needed. If Combat instead exposes a public projection method, that's the cleaner path — TD will adjudicate.)

## Authored values being destroyed

**Nothing authored is destroyed.** Slice 9a is purely additive at every surface:

| Where | Current | Slice 9a change |
|---|---|---|
| `Vehicle.cs` | No rehydrate method | Adds `public RestoreFromSnapshot(...)` — single new entry point, no existing method touched. |
| `SlotInstance.cs` | `internal SetHp` + `internal SetMaxHp` | Adds `internal RestoreFromSnapshot(...)` — same scope (internal), same access pattern (Vehicle-only caller). No existing setter signature touched. |
| `RunSceneHost.Initialize` | 4-arg `(LoadResult, NodeMapDto, RunSeedDto, RunDeckDto)` | 5-arg `(LoadResult, NodeMapDto, RunSeedDto, RunDeckDto, VehicleStateDto)` — sole call site (`SaveBootstrap.Start`) updated in same commit. EditMode tests updated. |
| `SaveBootstrap.cs` | Registers three adapters | Registers four. One extra `Register(new VehicleStateSerializable(() => host.RunState))` line. |
| Test wire-format literals | Three locked literals (NodeMap/RunSeed/RunDeck) | Fourth locked literal added; existing three untouched. |
| `armor_0` semantics | `Hp` depletes during combat, `MaxHp` derived via `RecomputeArmorPool` | Unchanged at runtime. Snapshot persists both values verbatim; restore writes both verbatim and does NOT call `RecomputeArmorPool` (key invariant — see Q4). |

No GDD edits. No SO edits. No prefab edits. No designer-tuned values touched. No scene edits. The capture-before-destroy hook should green-light this slice on path inspection alone — no protected paths carry destructive changes.

## Per-slot persistent state surface (the DTO payload)

Enumerated from `SlotInstance.cs` (read 2026-06-26). The DTO persists the **runtime-mutable** fields only; layout-time fields (`SlotId`, `Kind`, `Position`, `IsStructural`, `HudAnchor`, `ExposureMultiplier`, `RedirectsTo`, `_degradedThresholdPct`) are reconstructed via `IFrameLayout` at vehicle build time and never mutate per-run.

| Field | Type | Why persist |
|---|---|---|
| `SlotId` | `string` | Join key — matches snapshot to fresh-build SlotInstance by id, not by index. Survives Layout add/remove ordering changes (defensive against Layout SO edits between save and load). |
| `Hp` | `int` | The only field that mutates frequently during combat. Critical to persist. |
| `MaxHp` | `int` | Constant per part-install. Persisted for paranoia (catches drift if part data changes between saves; recovered DTO carries the snapshot-time max). For `armor_0`, this is the sum-of-parts-at-snapshot value. |
| `ArmorContribution` | `int` | Constant per part-install. Persisted so the same-shape post-restore vehicle would yield the same recomputed armor pool if `RecomputeArmorPool` were called (it isn't, by design — but the value is required for the Slice 9b Rest case where a `RepairSlot` afterwards correctly recomputes). |
| `InstalledPartId` | `string?` | Identity of the installed part. Nullable (int-only install paths leave it null). Persisted so HUD tooltips after restore look correct. |
| `InstalledPartDisplayName` | `string?` | Same. Persisted alongside the Id. |
| `CorrodedStacks` | `int` | Per-slot status counter. Public mutable. Persisted to lock corrosion progress. |
| `HasPart` | `bool` | Distinguishes "empty slot" from "slot with 0-Hp part." `armor_0` always has `HasPart=true` even though it's a buffer. |

**NOT persisted (recovered at rehydrate time):**
- `DamageState` — pure function of `Hp` + `MaxHp` + `DegradedThreshold`; recomputes on read.
- `DegradedThreshold` — pure function of `MaxHp` + per-layout threshold pct; recomputes on read.
- `_byKind` index on `Vehicle` — reconstructed from `_byId` walk in the constructor.
- `Vehicle._badges` (status strip) — see Q5 below; deferred to a separate slice if needed.

## Rehydrate sequencing (the load-path order question)

The load path runs as:

1. `SaveBootstrap.Start` calls `LoadRunState()` — all four adapters' `FromDto` populate their `LastLoaded`.
2. `SaveBootstrap` reads `nodeMapAdapter.LastLoaded`, `runSeedAdapter.LastLoaded`, `runDeckAdapter.LastLoaded`, `vehicleStateAdapter.LastLoaded`.
3. `SaveBootstrap` calls `host.Initialize(result, nodeMap, runSeed, runDeck, vehicleState)`.
4. `host.Initialize` evaluates the `run.session_core` resume gate (NodeMap + RunSeed + RunDeck all present + non-skipped).
   - If gate FAILS → fresh run via `BeginNewRun`; **vehicle state DTO is discarded** (no scenario where vehicle persists across a run-shape regenerate; the chassis-fresh vehicle from `BeginNewRun` is correct).
   - If gate PASSES → `BeginRunFromLoaded` rehydrates RunController + RunState including a **chassis-fresh** PlayerVehicle.
5. After `BeginRunFromLoaded` returns, if `vehicleState != null`: `host.RunState.PlayerVehicle.RestoreFromSnapshot(vehicleState.Slots)`.
6. Host fires `OnBeaconChanged` (existing path).

**Why the post-construction restore, not constructor-injection of the snapshot:**
- Vehicle construction goes through `IFrameLayout` → `BuildVehicle(...)` → empty `SlotInstance`s + install loop. The install loop is the canonical fresh-build path. Injecting snapshot state into the constructor would create a bimodal "snapshot mode" vs "fresh mode" inside `BuildVehicle` — ADR-0011 forbidden pattern #3 (bimodal paths). The post-construction restore preserves a single fresh-build shape with an additive restore phase.
- Restore-after-construction is also what happens when Rest fires in Slice 9b: the vehicle is already built, the Rest verb mutates a slot, the next snapshot write picks up the new state. Slice 9a's restore primitive is the same shape as Slice 9b's mutate primitive — both operate on an already-constructed Vehicle.

**Why the vehicle DTO is NOT in `run.session_core`:**
- The membership criterion landed in Slice 8d (ADR-0004): "a member joins `run.session_core` if its absence-with-others-present creates a silently-broken determinism or progression invariant."
- VehicleState absence does NOT silently break determinism: a missing vehicle DTO means "restore to chassis-fresh state," which is a recoverable cosmetic loss (player sees a healed vehicle when they expected damaged), not a corrupted run. The run-shape (graph + seed + deck) is intact.
- The asymmetric exhaustion policy per ADR-0004 Decision 7 applies: VehicleState is non-blocking; if it fails to load, the player gets a fresh-shape vehicle and the run continues. This is *gameplay-suboptimal but mechanically consistent*, the right side of the asymmetric policy.

## Test matrix

| # | Test fixture | Count | What it locks |
|---|---|---|---|
| 1 | `VehicleStateDto_round_trip_test` | ~7 | `From(Vehicle)` → `ToVehicleState()` snapshot for Scout chassis. Multi-slot with damage applied (weapon_0 at 5/10 Hp, mobility_0 at 0/10 Offline, armor_0 at 12/20 partially depleted). Deep field equality post round-trip. Corroded-stacks variant. armor_0 invariant: `MaxHp != Σ ArmorContribution` post-restore is fine (no recompute fires). Wrong-type FromDto throws. From(null) throws. |
| 2 | `VehicleStateDto_wire_format_test` | 3 | Locked canonical JSON literal for a known fixture (Scout, fresh-build, no damage). Ordinal sort at every depth. Round-trip the literal back. |
| 3 | `VehicleStateSerializable_test` | ~8 | SystemId/SchemaVersion/DtoType forward from DTO consts. Ctor null-source throws. Snapshot-on-demand projects fresh DTO per call. Live-source mutation (apply damage between calls) reflected in next ToDto. ToDto with null source throws InvalidOperationException. FromDto captures LastLoaded. Wrong-type FromDto throws. |
| 4 | `Vehicle_RestoreFromSnapshot_test` | ~6 | Build vehicle → install parts → take damage → snapshot. Build new vehicle via same Layout → restore from snapshot → assert per-slot Hp + MaxHp + ArmorContribution + PartId + PartName + CorrodedStacks + HasPart equality. Verify `armor_0.Hp` matches snapshot exactly (no recompute reset). Verify `DamageState` correctly recomputes post-restore (Offline slot reads Offline). |
| 5 | `RunSceneHost_Resume_Vehicle_Test` (extend existing) | ~3 | End-to-end: plant envelope with all four DTOs → SaveBootstrap.Start → host.Initialize restores vehicle alongside the run-shape group. Verify damaged Scout fixture (weapon_0 at 5 Hp) survives reload. Fresh-run fallback when vehicle DTO is missing but session_core present (chassis-fresh vehicle, no exception). Fresh-run fallback when session_core regenerates (vehicle DTO discarded). |

Baseline today: **638 tests / 637 pass / 1 skip (pre-existing Combat Ignored)**. Slice 9a target: **~665 / 664 pass / 1 skip**.

## Decisions for ratification (Slice 9a)

- **Q1 — Save category membership.** `VehicleStateDto` ships as a **standalone (group-of-one) RunState category**, NOT joined to `run.session_core`. Rationale per the membership criterion: absence does not silently break determinism (chassis-fresh fallback is mechanically consistent). The asymmetric exhaustion policy applies (non-blocking, fresh-fallback on load failure).
- **Q2 — Vehicle vs SlotInstance public surface.** Single new `public Vehicle.RestoreFromSnapshot(IReadOnlyList<SlotSnapshotDto>)` entry point. SlotInstance gains a single `internal RestoreFromSnapshot(...)` that only Vehicle calls. No public Hp/MaxHp setters added at SlotInstance level — keeps the back-door narrow.
- **Q3 — Rehydrate sequencing.** Vehicle restore happens **after** `BeginRunFromLoaded` constructs the chassis-fresh vehicle. No constructor-injected snapshot path; no bimodal `BuildVehicle`. Snapshot restore is an additive phase on a fully-constructed Vehicle.
- **Q4 — `RecomputeArmorPool` skip during restore.** `RestoreFromSnapshot` does NOT call `RecomputeArmorPool`. The snapshot carries the correct `armor_0.MaxHp` already; recomputing would reset `armor_0.Hp` to `MaxHp`, undoing depleted buffer state. The next damage event or part-install (via Rest/Chopshop) will correctly trigger a recompute; restore itself is recompute-free.
- **Q5 — Status badges (`Vehicle._badges`).** **Deferred.** Status badges are encounter-scoped today (clear between Combat encounters); persistence is not required for cross-beacon resume. Slice 9a explicitly does NOT persist `_badges`. If a future encounter ships cross-encounter badges (e.g. a multi-node corrosion effect), that slice carries the `_badges` DTO. Captured here so the gap is explicit, not silent.
- **Q6 — Enemy vehicles.** **Not persisted.** Enemy vehicles are constructed per-Combat from the beacon's archetype and discarded post-Combat. They never live across a resume boundary. `VehicleStateDto` is exclusively for `RunState.PlayerVehicle`.
- **Q7 — InternalsVisibleTo.** Combat assembly grants `[assembly: InternalsVisibleTo("WastelandRun.Save")]` (verify if not already granted from a prior slice). This keeps `SlotInstance.RestoreFromSnapshot` `internal` while letting `VehicleStateDto.From(Vehicle)` read SlotInstance state directly. Cleaner than exposing public Hp/MaxHp getters on the per-slot level (those already exist as `get; private set;` — read is fine without InternalsVisibleTo; the visibility grant is for the *write* side at restore time, scoped via Vehicle.RestoreFromSnapshot).

## Defers (ADR-0011-clean)

| Item | Reason it lights up later, not now |
|---|---|
| Status-badge persistence (`_badges`) | No cross-encounter badge ships today; persisting would be speculative scaffolding. |
| Mid-combat quit-and-resume | Combat today is single-screen-synchronous; mid-combat quit is rare. Snapshot cadence + active-CombatLoop persistence is a separate slice if it ships. |
| Enemy vehicle persistence | Not required (per-Combat construct, discard post-Combat). |
| Vehicle-DTO entry into `run.session_core` | The membership criterion explicitly excludes recoverable-as-chassis-fresh state. Not a deferral — a permanent design decision. |
| Schema migration for `VehicleStateDto` v1 → v2 | No prior version exists. First-version schemas don't migrate. Slice 8b load path handles unknown-system / mismatched-schema partial-skip if a future v2 ships. |

## Risk-flagged unknowns (TD review surface)

1. **InternalsVisibleTo audit.** Need to verify whether Combat already grants InternalsVisibleTo to Save (from a prior slice — possible but unconfirmed). If yes, no AssemblyInfo edit. If no, one-line grant added with a rationale comment.
2. **Vehicle build path through Layout.** Need to confirm `BeginRunFromLoaded` already constructs the player vehicle via `IFrameLayout` (Scout's `SmallFrameLayout.Instance`) — Slice 8d/8c left the construction in a callable shape. If construction is currently coupled to `BeginNewRun`-only logic, a small extraction is needed first.
3. **armor_0 initialization order.** `Vehicle.FillArmorPool()` is the build-time armor pool init. Restore must NOT call FillArmorPool — that would re-derive armor_0.MaxHp from current part state and overwrite the snapshot's value. The post-construction sequence is: `BuildVehicle → InstallPart×N → FillArmorPool` (fresh-build), then `RestoreFromSnapshot` (overwrites every slot's mutable state including armor_0).
4. **CorrodedStacks public mutability.** SlotInstance exposes `CorrodedStacks` as `public ... { get; set; }` — directly settable from any assembly. This is the existing public surface; restore can write through it directly. No new escape hatch.

## Technical Director Review (verbatim, 2026-06-26)

### Verdict on Slice 9 shape (Q3 of the brief)

> "**Save integration: NOT confirmed. The brief is wrong here.**
>
> `run.vehicle_state` does not exist today. There is no vehicle DTO and no vehicle adapter. This is the slice's scope cliff:
>
> - **Option 3a (smallest scope, real bug):** Ship Rest without persisting Vehicle state. Document the known gap: quit-after-Rest loses the repair on reload. Acceptable only if you also pin a follow-up slice (Slice 9.5 or Slice 10) for the `run.vehicle_state` adapter, and the gap is documented in the capture file under a `## Known Limits` heading. I do not recommend this for a slice whose entire mechanical point is mutating Vehicle state.
> - **Option 3b (right scope, larger slice):** Add `VehicleStateDto` + `VehicleStateSerializable` + adapter wiring as part of Slice 9. ADR-0004 distributed schema registry pattern, mirror `RunDeckDto`'s shape. Per-slot snapshot: `{ SlotId, PartId, Hp, MaxHp, DamageState, ArmorContribution }`. RecomputeArmorPool fires after restore. This is ~250 lines of save plumbing on top of your existing Rest estimate — call it ~450–550 total for the slice.
> - **Option 3c (split the slice):** Ship `run.vehicle_state` as Slice 9a first (no Rest, just vehicle persistence — testable via a fake mid-run damage scenario), then Rest as Slice 9b. Two clean commits, each individually testable.
>
> **I recommend 3c.** Two reasons: (a) `run.vehicle_state` is foundational — Chopshop, Event[Ambush chain], and combat-damage-persistence-mid-run all need it next; landing it ahead of Rest makes Rest a one-line consumer of the save category, not a co-author of it. (b) Slice 9a (save plumbing) has zero UI surface and zero design risk; Slice 9b (Rest) becomes a pure feature slice with the save question already answered. The risk profile of each half is lower than the combined slice."

### Slice 9a's amendments to TD's 3b shape

Two amendments to TD's 3b enumeration applied in this capture's design:

1. **`DamageState` not persisted.** TD listed it in the per-slot field set. We exclude it — `DamageState` is a pure function of `Hp` + `DegradedThreshold` and recomputes correctly on read post-restore. Persisting it would create drift potential (snapshot vs runtime disagreement). Confirmed by reading `SlotInstance.cs:114-123`.
2. **`RecomputeArmorPool` does NOT fire after restore.** TD said "RecomputeArmorPool fires after restore." We invert this for Q4 reasons enumerated above: the snapshot carries the correct `armor_0` state, and a recompute would reset `Hp` to `MaxHp` and undo depleted buffer state. The next install/repair event will correctly recompute; restore-time recompute is incorrect.

Both amendments are TD's expected review surface and will be re-asked of TD before commit if the user wants a re-pass.

### Verdict on the `IScrapEconomy.TryRepair(freeRepair: true)` trap (Q4 of the brief — applies to Slice 9b, captured here for the record)

> "**Q4 — `freeRepair` flag: do not add it.**
>
> This is the trap. The GDD says `TryRepair(subsystem, source, freeRepair: true)`. The verb doesn't exist, and **the only call site shipping this slice passes `true`**. If you ship the parameter now:
> - `freeRepair=false` is a dead branch (no caller exercises it) — ADR-0011 forbidden pattern #5.
> - The semantic of `false` is unspecified by any shipping consumer — when Chopshop ships in a future slice and *does* want a paid repair, the contract for the false branch (does it call `TrySpend` first? what cost formula?) will be argued from scratch anyway.
> - This recreates the ADR-0012 `InstallPart(armorContribution=0)` default-param-overload trap that's pinned in my memory — source-compatible signature, no real consumer for the non-default branch, semantics fuzzy.
>
> **Recommended shape for slice 9b:** Rest calls `Vehicle.RepairSlot(slotId, hugeAmount)` *directly* via the encounter controller — it does not go through `IScrapEconomy` at all. Rationale:
> - Rest does not transact Scrap (free). The Scrap Economy interface is for *currency operations*; a free, full-restore action is not a currency operation. Routing it through `IScrapEconomy` for the sake of GDD-doc alignment is forcing a verb through the wrong system.
> - The audit log the GDD wants ("Rest[node:N] repaired slot X") can be raised by the Rest encounter controller itself — it doesn't need to live in `IScrapEconomy`'s ledger.
> - When Chopshop ships, **that** is the slice where `IScrapEconomy.TryRepair(slotId, source, cost)` lands — with `cost` as a real `int` parameter (not a `bool freeRepair`), exercising both `TrySpend` and `Vehicle.RepairSlot` together. No dead branch, no compat shim, one consumer per shape."

Pinned to memory as `feedback_gdd_verb_signature_not_load_bearing.md` for forward reference.

### Verdict on capture scope (Q5 of the brief)

> "Per CLAUDE.md the capture trigger is 'system refactor / new system ≥50 lines.' Slice 9b alone is ~250 lines; with Slice 9a save plumbing, 450–550. Both halves touch protected paths (`Run/`, `Save/`, `CombatView/`). The capture is mandatory under the hook, and the structure (handler + overlay + UIDocument + UXML + USS + tests) needs the pre-edit reconnaissance the capture protocol enforces — see memory `feedback_capture_before_destroy_view_layer.md`."

Slice 9a is the first half of the 9a/9b split. Slice 9b will carry its own capture file referencing this one.

## Success criteria for "Slice 9a got this right"

- Damaged vehicle survives quit-and-resume — Hp, MaxHp, ArmorContribution, PartId, PartName, CorrodedStacks all match snapshot.
- `armor_0.Hp` post-restore matches snapshot exactly (NOT reset to MaxHp).
- Fresh-shape vehicle when only `run.session_core` survives load (no exception, no soft-lock; vehicle is chassis-fresh, run continues).
- `run.session_core` resume gate is **unchanged** — Slice 9a does not modify the three-DTO atomic group. Vehicle DTO is independent.
- `IScrapEconomy` interface surface **unchanged**. (Confirmation that we did not add the `TryRepair(freeRepair: true)` trap; this is Slice 9b's surface to honor, but recorded here so a Slice 9b drift is caught against the 9a baseline.)
- `BuildVehicle` / `BeginRunFromLoaded` shape **unchanged** — restore is a post-construction additive phase, no bimodal injection.
- Test suite: ~665 / 664 pass / 1 pre-existing skip.

---

**Next step after capture approval:** Implement Slice 9a in the order: DTO + nested SlotSnapshotDto → SlotInstance.RestoreFromSnapshot + Vehicle.RestoreFromSnapshot → VehicleStateSerializable adapter → SaveBootstrap registration + RunSceneHost.Initialize signature → tests (round-trip → wire-format → adapter → vehicle restore → host integration). Each layer green before the next. Single commit at end with `feat(save): slice 9a — VehicleStateDto + per-slot snapshot adapter` per the conventional-commits standard.

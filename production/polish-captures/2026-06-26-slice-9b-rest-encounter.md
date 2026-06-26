# Capture — Slice 9b: Rest encounter

**Date:** 2026-06-26
**Sister slice:** 9a (`run.vehicle_state` save plumbing, merged `40bb4ff`)
**Scope:** First non-Combat beacon handler. Wires the Rest encounter end-to-end
— biome distribution table widening (ADR-0015 data-only) + `RunSession.ResolveRest`
verb + UI Toolkit picker overlay + per-commit snapshot trigger. Haven terminus
is **out of scope by construction** — already auto-resolves in `CommitNextBeacon`
(RunController.cs:247-251).

## Why now

Slice 9a's promise is only half-paid until a player-driven mechanical effect
mutates persistent vehicle state across a beacon. Rest is the smallest such
effect: pure HP restoration, single-tick resolution, no Scrap economy
dependency. It's also the first encounter that exercises the
`Vehicle.RepairSlot` → `EnqueueRunStateSnapshot` → `VehicleStateDto.ApplyTo`
pipeline end-to-end. Chopshop (later) will reuse this exact shape with
Scrap cost added — so building Rest right de-risks every subsequent
non-Combat handler.

## Final-game picture

The map currently spawns Combat + Haven terminus only (per ADR-0015's first
application). After 9b, Biome 1 spawns Combat + Rest + Haven. A player who
takes weapon damage at beacon 2 can detour west to a Rest at beacon 3, pick
weapon_0 from the damaged-slot list, restore it to MaxHp, then commit forward
to the next Combat with the weapon intact. Quit-and-resume mid-detour
rehydrates to the same posture. The Rest handler's verb signature is
load-bearing for Slice 10 (Chopshop) — Chopshop will copy the same
single-tick resolution shape and prepend a Scrap deduction.

## Files touched

### New
- `Assets/Scripts/Run/RestPickerViewModel.cs` — model facing the UI picker.
  Exposes `IReadOnlyList<RestRepairCandidate>` (slotId, displayName, damageDelta)
  + `void Commit(string slotId)`. Pure POCO — no UI dependency.
- `Assets/UI/RunHUD/RestPicker.uxml` + `RestPicker.uss` + Assets/Scripts/UI/RestPickerController.cs
  — UI Toolkit overlay per ADR-0014. Bind/OnDestroy lifecycle (subscribes to
  `RunSession.OnBeaconArrived` external publisher).
- `Assets/Tests/EditMode/Run/RunSession_ResolveRest_test.cs` — verb contract.
- `Assets/Tests/EditMode/Run/RestPickerViewModel_test.cs` — empty-list edge,
  damaged-slot enumeration, commit semantics.
- `Assets/Tests/EditMode/CombatView/RunSceneHost_RestResume_test.cs` — quit
  mid-pick → resume opens picker, not partially-applied repair (TD R1).

### Modified
- `Assets/Resources/BiomeDistributions/Biome1Distribution.asset` — append
  `WeightedBeaconType(Rest, 20)` to `_nonTerminalBeaconTypes`. **Pure SO edit.**
  No code branch — the generator is already polymorphic via
  `WeightedBeaconType` (ADR-0015 pattern).
- `Assets/Scripts/Run/RunSession.cs` — new public `void ResolveRest(string slotId)`
  sibling to EnterCombat/ExitCombat. Throws on wrong-beacon, already-resolved,
  unknown slotId, undamaged slotId.
- `Assets/Scripts/CombatView/RunSceneHost.cs` — fires
  `EnqueueRunStateSnapshot()` post-ResolveRest (mirrors the post-combat
  cadence).
- `Assets/Scripts/CombatView/RunSceneOverlayHost.cs` (or sibling) — wires the
  RestPicker visibility to `current.Type == Rest && !current.IsResolved`.

### NOT touched (called out explicitly)
- `Vehicle.cs` / `SlotInstance.cs` — `RepairSlot` already exists at Vehicle.cs:485,
  already calls `RecomputeArmorPool()` at Vehicle.cs:503. **Zero combat
  primitive changes.**
- `RunController.cs` — pure state machine, no encounter logic added.
- `IScrapEconomy.TryRepair(subsystem, …, freeRepair: true)` — **deliberately
  not implemented**, see "Decisions" Q1.
- Haven handler — `CommitNextBeacon` already auto-handles terminal arrival.
- Existing CardRewardPicker (UGUI) — ADR-0014 P3 debt, not touched.

## Decisions

### Q1 — Verb shape: `Vehicle.RepairSlot` direct, NOT `IScrapEconomy.TryRepair(freeRepair: true)`

The GDD (node-encounter.md C.2.6) prescribes
`IScrapEconomy.TryRepair(subsystem, nodeContext, freeRepair: true)`. We are
**not implementing this**. The verb signature is the trap named in user
memory `feedback_gdd_verb_signature_not_load_bearing.md`: single-consumer
interface design + extends a verb (`TryRepair`) that doesn't exist + uses
retired vocabulary (`subsystem` was killed by ADR-0010).

Instead: `RunSession.ResolveRest(slotId)` calls
`Vehicle.RepairSlot(slotId, slot.MaxHp - slot.Hp)` directly. When Chopshop
ships and a second concrete consumer materializes, **then** factor a verb
seam — at that point we'll know what the seam needs to carry (Scrap cost
parameter at minimum). GDD retrofit is doc-only when that lands.

**TD confirmed.**

### Q2 — Verb location: `RunSession.ResolveRest`, NOT `RunController.ResolveRest` or `IBeaconHandler` dispatcher

Sibling to `EnterCombat`/`ExitCombat` on RunSession — RunSession already owns
encounter resolution; RunController stays a pure state machine.

Asymmetric vs the Combat Enter/Exit pair: Rest has no mid-state, so no
`EnterRest()` / `ExitRest()` synthetic split. **Vestigial scaffolding is
ADR-0011 #2.** A single `ResolveRest(slotId)` is the right shape — it's the
"ExitCombat" moment without any prior "EnterCombat" because the entire
mechanical effect is a single tick.

Dispatcher pattern (`IBeaconHandler` enum-dispatched) rejected: ADR-0011 #4
vestigial-enum risk for two consumers (Combat + Rest). Re-evaluate at
Chopshop / Merchant if the third+ handler shape genuinely diverges.

**TD confirmed.**

### Q3 — No new RNG salt

Rest is fully player-driven. No deterministic roll fires. No
`DeriveRestSeed` added — ADR-0011 #6 dead-code trap if added preemptively.

**TD confirmed.**

### Q4 — Picker UI: UI Toolkit per ADR-0014, permanent overlay on `Run.prefab`

UI Toolkit (UXML + USS + C# controller) per ADR-0014. The picker is **not**
a scene-additive sub-scene — scene-split is deferred per
`production/td-verdicts/2026-06-17-scene-split-verdict.md` (revisit before
biome 2). For Slice 9b: permanent overlay on `Run.prefab`, SetActive-gated by
the visibility wire.

**Subscription lifecycle: Bind/OnDestroy**, NOT OnEnable/OnDisable. The
RestPickerController subscribes to `RunSession.OnBeaconArrived` (external
publisher) — per memory `feedback_subscription_lifecycle_pairing` +
`feedback_uitoolkit_subscription_lifecycle`, external-publisher events
under SetActive cycles must be Bind/OnDestroy or they silently break when
the overlay toggles. Slice 7a CardRewardPicker bug was exactly this.

Empty damaged-slot list (player hits Rest at full HP) — **in scope**: picker
renders "Nothing to repair" with a dismiss button that calls
`ResolveRest(slotId: null)` → `current.MarkResolved()` directly. Prevents
soft-lock. ~5 lines.

**TD confirmed.**

### Q5 — Persistence cadence: snapshot on commit, NOT mid-pick

`EnqueueRunStateSnapshot()` fires inside `RunSceneHost` only after
`ResolveRest` returns — same shape as post-combat snapshot. Mid-pick state
(picker open, no slot chosen) is **not** persisted. Quitting mid-pick
resumes to: beacon `Arrived` + `!IsResolved` + picker re-opens via the
visibility wire. Locked by `RunSceneHost_RestResume_test`.

**TD confirmed (R1).**

## Traps avoided / verified by reading code before authoring

| Trap | Status | Verified by |
|---|---|---|
| GDD `TryRepair(freeRepair: true)` verb-signature | **Avoided** — call `Vehicle.RepairSlot` direct | Memory `feedback_gdd_verb_signature_not_load_bearing` + Q1 above |
| `RepairSlot` doesn't clamp to MaxHp | **Verified** — Vehicle.cs:497-498 `if (next > MaxHp) next = MaxHp` | Read pre-implementation per TD R2 |
| `RepairSlot` doesn't recompute armor on Frame state transition | **Verified** — Vehicle.cs:503 calls `RecomputeArmorPool()` unconditionally | Read pre-implementation per TD R3 |
| `EnterRest()`/`ExitRest()` symmetric pair | **Avoided** — single `ResolveRest(slotId)` | Q2 above |
| Dispatcher pattern for 2 handlers | **Avoided** — sibling method | Q2 above |
| UI subscription lifecycle (OnEnable/OnDisable under SetActive cycles) | **Avoided** — Bind/OnDestroy | Q4 above + Slice 7a memory |
| Mid-pick snapshot | **Avoided** — snapshot post-commit only | Q5 above + TD R1 |
| Code branch for new beacon type in distribution | **Avoided** — pure SO edit per ADR-0015 | Existing generator is polymorphic |

## Test surface

| # | File | Tests | What it locks |
|---|---|---|---|
| 1 | `RunSession_ResolveRest_test.cs` | ~6 | Verb contract: throws on non-Rest beacon, throws on already-resolved, throws on unknown slotId, throws on undamaged slotId. Success path: slot Hp → MaxHp, beacon resolved, DamageState becomes Functional. Empty-slot dismiss path: `ResolveRest(null)` allowed iff no damaged slots, marks resolved, no Vehicle mutation. |
| 2 | `RestPickerViewModel_test.cs` | ~4 | Damaged-slot enumeration source-of-truth = Vehicle (TD R4). Empty-list semantics. Commit forwards to RunSession. Filter excludes undamaged slots. |
| 3 | `RunSceneHost_RestResume_test.cs` | ~3 | Resume mid-pick: planted envelope at unresolved Rest beacon → picker re-opens on load. Resume post-Rest: snapshot reflects repaired Hp. (TD R1.) |
| 4 | `RunSceneHost_Resume_Test.cs` extension | ~1 | Distribution-widening sanity: Biome1Distribution emits Rest beacons (count > 0 across N runs at fixed seeds). Catches accidental SO regression. |

Total new tests: ~14. All deterministic, all unit-isolated, no integration
runtime required.

## Defers (called out, not abandoned)

| Item | Why deferred | Re-surfaces when |
|---|---|---|
| Rest celebratory animation + audio | Polish, not mechanical. Risks slice scope creep. | Polish pass on encounter feedback (post-MVP). |
| Haven victory screen UI | Already mechanically resolved — `Status = Victory` fires. UI presentation is a separate slice. | Haven-presentation polish slice. |
| GDD `freeRepair` retrofit on Scrap Economy GDD | Per `feedback_prefer_code_over_doc_churn` — defer until 2nd consumer. | Chopshop slice writes the seam, then doc-only PR. |
| UGUI → UI Toolkit port of CardRewardPicker | ADR-0014 P3 debt, independent surface. | ADR-0014 P3 closeout slice. |
| `IScrapEconomy.TryRepair` verb seam | Single consumer today. | Chopshop ships 2nd consumer. |
| Cross-beacon Rest semantics ("rest costs a turn") | Per GDD: "the detour costs them a turn of storm advance" — storm system not in code yet. | Storm system slice. |
| Multiple-slot Rest (repair-all) | GDD: "Restores one subsystem fully." Single-pick is spec. | N/A — not deferred, intentional. |
| Per-biome Rest weight tuning | Slice ships GDD-target 20% in Biome 1; tuning is a balance pass. | Balance-pass slice. |

## Known Limits at slice close

None destructive. All defers above are forward additions, not gaps.

## Technical Director Review

> **TD-FEASIBILITY: APPROVE WITH CONCERNS**
>
> Slice shape is sound. You've correctly dodged the verb-signature trap and
> the dispatcher trap. Steps 1-5 are the right scope. Concerns below are
> tactical, not structural.
>
> **Verdicts on Q1-Q5**
>
> **Q1 — Verb location: A confirmed.** RunSession owns encounter resolution;
> RunController stays a pure state machine. Asymmetry vs. EnterCombat/ExitCombat
> is fine — Combat needs a mid-state because the combat loop runs elsewhere
> (Combat.prefab scene); Rest resolves in a single tick. Don't synthesize a
> fake `EnterRest()` for symmetry's sake — that's vestigial scaffolding
> (ADR-0011 #2). Naming nit: `ResolveRestRepair(slotId)` reads well; consider
> `ResolveRest(slotId)` to match the future Chopshop verb shape
> (`ResolveChopshop(...)`) without the verb-doubling.
>
> **Q2 — Picker lifetime: (a) confirmed.** Scene-split is deferred per your
> own verdict (2026-06-17). Permanent overlay on Run.prefab, SetActive-gated,
> sibling to the future-ported CardRewardPicker UI Toolkit version.
> **Concern:** mind the subscription lifecycle pairing memory — if the Rest
> picker subscribes to a RunSession event (e.g. `BeaconArrived`) to know
> when to show, that's an external-publisher subscription → **Bind/OnDestroy**,
> NOT OnEnable/OnDisable, even though it's SetActive-toggled. Slice 7a
> CardRewardPicker bug was exactly this. Bake this into the brief for
> unity-ui-specialist.
>
> **Q3 — No new seed: confirmed.** Rest is fully player-driven. No salt
> needed. Don't add one preemptively — adding an unused `DeriveRestSeed`
> is the same trap as adding an unused verb.
>
> **Q4 — Scope cap: confirmed, with one addition.** Defer animations,
> celebratory feedback, audio. **Do not defer the "no damaged slots" edge
> case** — if a player hits Rest at full HP, the picker must not soft-lock
> the run. Cheapest handling: picker shows "Nothing to repair" + dismiss
> button that calls MarkResolved directly. That's ~5 lines and prevents a
> stuck-state bug report. Treat as in-scope.
>
> **Q5 — GDD retrofit deferred: confirmed.** Per `feedback_prefer_code_over_doc_churn`
> and `feedback_gdd_verb_signature_not_load_bearing`. Queue the retrofit
> alongside Chopshop's second-consumer materialization. Add one line to the
> slice 9b capture noting the GDD's `TryRepair(freeRepair: true)` is
> intentionally not implemented and points at the GDD verb-trap memory.
>
> **Risks you haven't flagged**
>
> **R1 — Save cadence on partial completion.** If the player opens the Rest
> picker and quits before picking, what does resume look like? Beacon is
> `Arrived` but not `Resolved`, picker re-opens on load. Confirm the picker
> can rehydrate from `_state.Status + current beacon kind == Rest +
> !current.IsResolved`. This is the right shape but needs an explicit test.
> **Don't snapshot mid-pick.**
>
> **R2 — Vehicle.RepairSlot return value semantics.** It returns RepairResult.
> Slice 9b should assert what that result means at Rest scope (full restore
> vs. clamped). Read the existing call site (Weld card) before authoring —
> confirm the `amount = maxHp - currentHp` calculation actually returns to
> Nominal, not Degraded, for an Offline slot. If RepairSlot has
> subsystem-state-transition logic beyond HP math, the Rest handler may
> need to call a different primitive. **Verify before writing the verb.**
>
> **R3 — ADR-0012 sum-of-parts armor.** If the player repairs a Frame part,
> armor_0.MaxHp recomputes. Confirm RecomputeArmorPool fires on RepairSlot
> for armor-contributing parts, OR call it explicitly post-repair in
> ResolveRest. Memory `project_sum_of_parts_armor` says "on
> install/uninstall/state transitions" — Offline→Nominal IS a state
> transition, so it should fire, but verify the code path, don't assume.
>
> **R4 — Picker damaged-slot enumeration source of truth.** Picker needs
> the damaged-slot list. Pull from Vehicle directly (`vehicle.GetDamagedSlots()`
> or equivalent), not from a cached projection. If the projection doesn't
> exist, add it on Vehicle, not on the picker.
>
> **Split recommendation: Don't split.** 9a's split worked because save
> plumbing and Rest handler had independent test surfaces. Here the verb
> + picker + snapshot are one cohesive transaction — splitting picker
> from verb just adds an integration moment. Ship as 9b single slice.
>
> **Success criteria.** We'll know this was right if: (1) Chopshop lands
> in a later slice without refactoring `ResolveRest`, (2) the GDD retrofit
> when it happens is doc-only with no code churn, (3) resume-mid-Rest
> round-trips cleanly in an EditMode test.

### TD risks resolved before authoring

- **R2** verified: `Vehicle.RepairSlot` (Vehicle.cs:485-505) clamps to MaxHp
  via `if (next > inst.MaxHp) next = inst.MaxHp` (line 498). `RepairResult`
  carries `(healed, beforeState, afterState)`. Full-restore call is
  `RepairSlot(slotId, slot.MaxHp - slot.Hp)`. No second primitive needed.
- **R3** verified: `Vehicle.RepairSlot` calls `RecomputeArmorPool()`
  unconditionally on line 503, exactly per ADR-0012 contract. Offline→Functional
  Frame repair correctly bumps `armor_0.MaxHp`. No explicit post-repair recompute
  in `ResolveRest`.
- **R4** noted: `Vehicle.GetDamagedSlots()` projection does not yet exist;
  add it on Vehicle (filter `_slotInstances` where `slot.Hp < slot.MaxHp`),
  not in the picker. Single new public surface, ~3 lines.

## Implementation order

1. `Vehicle.GetDamagedSlots()` projection (TD R4) — ~3 lines + 1 test.
2. `RunSession.ResolveRest(slotId)` verb (Q2) — ~25 lines + 6 tests.
3. `RestPickerViewModel` POCO (no UI dep) — ~30 lines + 4 tests.
4. `Biome1Distribution.asset` widening (Q-data) — SO edit.
5. `RestPickerController` (UXML+USS+C#) per ADR-0014 (Q4) — ~80 lines + UI integration test.
6. `RunSceneHost.EnqueueRunStateSnapshot()` post-`ResolveRest` (Q5).
7. `RunSceneHost_RestResume_test.cs` end-to-end + distribution sanity.
8. Full EditMode suite green.
9. Single commit: `feat(run): slice 9b — Rest encounter + UI Toolkit picker`.

## Acceptance criteria

- All EditMode tests pass (existing 669 + ~14 new = ~683).
- Rest beacon spawns in Biome 1 fixed-seed graphs (sanity test).
- Player can navigate to a Rest beacon and fully repair one damaged slot via picker.
- Empty damaged-slot Rest dismisses without soft-lock.
- Resume mid-pick re-opens picker; resume post-Rest reflects repaired Hp.
- Zero new entries to `IScrapEconomy.TryRepair` (verb seam stays deferred).
- Zero changes to `Vehicle.RepairSlot` / `SlotInstance` (combat primitive untouched).
- No `EnterRest`/`ExitRest` pair (single `ResolveRest`).
- No `IBeaconHandler` dispatcher.
- No `DeriveRestSeed`.

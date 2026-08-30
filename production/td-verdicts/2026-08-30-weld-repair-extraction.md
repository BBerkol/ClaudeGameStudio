# TD Verdict — extract the weld drain out of the Chopshop controller

**Date:** 2026-08-30
**Files:** new `Assets/Scripts/Run/WeldRepairModel.cs`;
`Assets/Scripts/CombatView/ChopshopWorkbenchController.cs`
**Verdict:** CONCERNS — extraction is right; three surface changes are conditions.

---

## Why this came up

Repair is moving hosts: off the Chopshop entry choice rail and into the new
fullscreen garage
(`production/polish-captures/2026-08-29-chopshop-bay-garage.md` §2a). Before
moving it, we wanted a test proving repair still works.

**There is none to extend — the weld budget logic has ZERO coverage.** Verified:
nothing in the suite references `StartBudget` or `_repairBudget`.
`Vehicle.RepairSlot` itself IS covered by `RepairCardTests`, but the drain
accounting, the depletion lock, the per-visit reset and the "fully-healed slot
does not consume budget" rule are untested.

It also cannot be tested where it lives: `TickRepair` interleaves the arithmetic
with `Mouse.current.leftButton.isPressed`, `Time.deltaTime`, hover state and UI
writes. Reflection-driven tests were rejected twice earlier in this session as
harness-shaped.

So the extraction is not overhead on top of the test — it is what makes both the
test and the host move possible. Logic that does not live in the host cannot
break when the host changes.

## Rulings

**1. Home is `WastelandRun.Run`, not `Combat`.** Both are `noEngineReferences`,
and Run already references Combat so `Vehicle` / `SlotInstance` / `SlotKind` are
in scope. The weld budget is a **run-scoped, per-beacon-visit economy resource**,
not a combat-loop concept; ADR-0002 charters Combat as the encounter model.
Decisive tiebreaker: the only plausible future persistence is a `RunState` DTO
field under ADR-0004, and Combat has no run-save story.

**2. Shrink the proposed surface.**
- `ShouldExit` splits into **`BudgetDepleted`** + **`NothingRepairable`**. The
  rule is legitimately model state, but one flag collapses two causes the garage
  will want to distinguish (different copy and SFX at 1.0). The view ORs them.
- **Drop `BudgetSpent`.** It is `HpHealed > 0` — `RepairSlot(slot, 1)` and
  `_repairBudget--` move 1:1. Two fields carrying one number is parallel storage
  in miniature.
- **Delete `ResetAccumulator()`.** A two-call protocol the view must remember is
  the bimodal-path smell ADR-0011 targets. Fold input into the tick:
  `Tick(vehicle, hoveredSlotId, inputHeld, deltaSeconds)`, and let the model
  clear its own accumulator when `!inputHeld || hoveredSlotId == null ||
  Budget <= 0`. The view keeps `Mouse.current` and derives `canDrain` only for
  the sparks.
- **`HasRepairableDamage` MOVES, it is not copied.** `ShowDialogueEntry`'s
  Repair-button gate calls it too — two callers today, one owner.

**3. `BeginVisit` is safe; the caller is the whole rule.** Sole call site stays
`HandleBeaconActivated`. Move the existing "never OnEnable" comment onto
`BeginVisit`'s xmldoc so the constraint sits at the definition. Hold `_weld` as a
`readonly` field initialised at construction and **never re-`new`'d in
`OnEnable`** — that reproduces today's cross-`SetActive` persistence exactly. The
regression risk is a NEW caller, not the method existing.

**4. Mechanical move + new tests is acceptable, with one substitute for the
missing characterisation baseline.** Same commit. For each of the four untested
rules — per-visit reset, depletion lock, fully-healed slot does not charge
budget, multi-tick stall burst — **mutate the extracted arithmetic and confirm
the test goes red before landing.** A test never seen failing is not coverage.

**5. ADR compliance is conditional on zero leftovers.** Delete from the
controller: `StartBudget`, `SecondsPerTick`, `_repairBudget`, `_drainAcc`, and
the private `HasRepairableDamage`. `SetBudgetBarFill` reads
`_weld.BudgetFraction` — a shadow int for the bar is parallel storage. No
default-param compat overload on `Tick` (source-compatible ≠
semantics-preserving). No `Mathf` in the model; clamp in the view.

## Three-lens self-audit

**Codebase health.** The ADR-0011 risk is real but lives entirely in the
leftovers above — grep the controller for all five symbols post-move and report
zero. Subscription lifecycle untouched (the model holds none). Teardown:
`ExitRepairMode` / `OnDestroy` never touch the model, so there is no
mid-callback race.

**Optimization.** The per-frame path must stay allocation-free.
`WeldTickOutcome` by value allocates nothing. Keep `GetSlotById` per tick, NOT
`GetDamagedSlots`. **Keep the `HpHealed > 0` gate before the
`HasRepairableDamage` call — it is the only thing keeping the allocating
`GetDamagedSlots` off the idle frame. Mechanical moves lose gates like this;
name it in review.**

**1.0 survival.** The signature survives the garage host move. ~~Named risk: the
budget is not persisted, so quitting and reloading mid-visit re-fires
`HandleBeaconActivated` and refunds it — a pre-existing save-scum exploit.~~
**RETRACTED — see the correction below.** Landing in `Run` still makes the
eventual fix cheaper, for the reasons in ruling 1.

---

## CORRECTION 2026-08-30 — the named exploit does not exist

A second TD pass, commissioned to design the fix, traced the write path and
**falsified the claim above**. Verified independently before accepting:

- `EnqueueRunStateWrite` snapshots the registry **synchronously at the call
  site** (`SaveSystem.Write.cs:186`), so disk holds the state at that instant.
- All **nine** `EnqueueRunStateSnapshot` callers live in `RunSceneHost`: run
  start (`:733`), resume (`:878`), both advance branches (`:982`, `:987`), and
  the five resolution seams (`:1239`, `:1251`, `:1264`, `:1277`, `:1302`) —
  which fire on RESOLVE, i.e. on leaving.
- There is no quit or pause autosave (`RunSceneHost.cs:277`).

**So nothing writes between arriving at a beacon and leaving it.** Quitting
mid-visit restores the ARRIVAL snapshot: the welded Hp is gone along with the
spent budget. A rollback, not a refund — the player loses their time, gains
nothing.

**Process note, which is why this correction is inline rather than a new file.**
The original claim was phrased as a "named risk" in a verdict whose every other
finding was correct and valuable. It was relayed to the director as a live
exploit in a shipping build, written into `WeldRepairModel`'s xmldoc, and
approved for a fix — before anyone traced the write path. One design cycle
spent. `feedback_verify_reviewer_claims` exists for exactly this, and applies to
agents whose other output is good.

**The tripwire is the real deliverable.** This becomes a genuine exploit the
instant ANY enqueue lands between arrival and departure — most likely the Phase
2.5 garage wanting part installs to survive a crash. At that point the budget
must be persisted **keyed by beacon index** AND `BeginVisit` must take that
index and refill only on a change — **in the same commit**. Persisting the
number alone leaves the next Chopshop starting spent; restoring alone is
overwritten one call later by the unconditional `BeginVisit` on activation.

---

## Technical Director Review

Verdict recorded above (`technical-director`, 2026-08-30). **CONCERNS** — the
extraction is endorsed; the three surface changes in ruling 2 and the
zero-leftovers condition in ruling 5 are binding, not advisory.

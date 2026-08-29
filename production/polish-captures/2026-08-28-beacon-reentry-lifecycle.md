# Capture — Beacon re-entry lifecycle (Chopshop + Rest)

**Date:** 2026-08-28
**System:** `ChopshopWorkbenchController` / `RestSceneController` activation lifecycle
**Severity:** Player-facing. Every 2nd+ visit shows stale UI.
**Status:** IMPLEMENTED + VERIFIED 2026-08-29 — see §8.

---

## 1. The defect

`BeaconActivator` in PrefabRoot mode calls `SetActive(true/false)` on the **same
persistent GameObject** per beacon type — it never instantiates per visit. Unity
runs `Start()` exactly once per component lifetime.

`ShowDialogueEntry()` is reached only from `Start()`:

| Controller | `Start()` | `ShowDialogueEntry()` |
|---|---|---|
| `ChopshopWorkbenchController` | `:326` | `:341` |
| `RestSceneController` | `:156` | `:171` |

Chopshop's other call sites (`:374`, `:478`, `:613`) are all repair-mode EXIT
paths — right-click cancel, back button, budget depleted. None fires on
re-entry.

**Result: on visit 2+, `OnEnable` fires but `Start()` does not.** Vehicle pose,
bar rebind, resource strip and repair enable/label all show stale or
UXML-default values.

### Why it became urgent yesterday

Biome 1 had ~2 Chopshops per map and a run visited **0.49** of them — a second
visit was nearly unreachable. Guaranteed spaced seeding (`bee7ba6`) plus the
icon fix (`fb0bb5d`) means players now routinely visit **2-3 per run**.

**A dormant bug became a common one, and nothing in that slice touched these
controllers.** It surfaced only from scouting the ground the NEXT slice stands
on — the Gate-1 pass mandated by `feedback-three-review-gates`.

---

## 2. Scope correction — TWO controllers, not three

The prior memory claimed Chopshop + Rest + **Merchant**. **Wrong.**
`MerchantSceneController` has no `Start()` anywhere; it queries every element in
`OnEnable` (`:172`), resets entry state (`_isShopOpen = false`, `:255`) and calls
`Bind()` (`:259`). It already refreshes per visit and **must not be touched**.

Memory `project_beacon_revisit_stale_entry` corrected 2026-08-27.

---

## 3. The fix — and the shape that was REJECTED

**Rejected:** move the guard + `ShowDialogueEntry()` into `OnEnable`. That was
the memory's prescription and my first proposal. `OnEnable` also fires on domain
reload and on any incidental `SetActive` toggle unrelated to a genuine beacon
visit.

**Accepted: subscribe to `BeaconActivator.OnBeaconActivated`** (`:105`) — the
event that fires only through `ActivateFor` → `SwapToPrefabRoot`, carries
identity dedup via `_lastActivatedBeacon`, and is **already consumed by
`RunSceneHost` for this exact purpose** (`:366` / `:372`, OnEnable/OnDisable
paired). Mirror the existing pattern; do not invent one.

Three properties verified directly in source:

- **Ordering safe by construction.** The event fires at `BeaconActivator.cs:436`
  strictly after `targetRoot.SetActive(true)` returns, so the controller's own
  `OnEnable` has already re-queried its `VisualElement` handles. No
  UIDocument-reclone hazard.
- **`Session` non-null identically on visit 1 and N.** `BeaconSceneBootstrap`
  sits on the same root and resolves `Host` in its `Awake`, inside that same
  synchronous `SetActive`. No first-vs-Nth branch → no ADR-0011 bimodal path.
- **`OnEnable`/`OnDisable` is the correct pairing**, not Bind/OnDestroy. The
  publisher outlives the subscriber's active period. Bind/OnDestroy would leak
  the subscription across the deactivated window and fire `ShowDialogueEntry`
  into a controller whose pointers `OnDisable` already nulled (`:312-323`) —
  absorbed silently by its `if (_root == null) return;`.

---

## 4. What is destroyed

### 4a. `Start()` deleted outright in both controllers

`ChopshopWorkbenchController.cs:326-342`, `RestSceneController.cs:156-172`. An
empty `Start()` would be ADR-0011 vestigial surface.

### 4b. The resume-into-resolved guard — DELETED, not ported

```csharp
if (session?.Controller?.Current != null && session.Controller.Current.IsResolved)
    return;
```

The `unity-specialist` plan said move this "verbatim." **The TD caught that it
is dead under the new cadence**, and I verified both links:

- `ActivateFor` returns `ClearAll()` when `current.IsResolved`
  (`BeaconActivator.cs:215-219`) — the event never fires for a resolved beacon.
- `_session` is constructed from `_controller` (`RunSceneHost.cs:663`, `:810`),
  so `Session.Controller.Current` and `_host.CurrentBeacon` are the same object.

Porting it forward would trade an empty method for a **dead branch reading as a
live invariant** — precisely the pattern deleting `Start()` avoids.

**The knowledge survives as a comment** on the subscription noting that resolved
beacons are filtered upstream at `BeaconActivator.cs:215`.

### 4c. `_repairBudget = StartBudget` MOVED from `OnEnable` to the handler

Currently the last statement of Chopshop's `OnEnable` (`:294`), commented
"OnEnable fires once per BeaconActivator SetActive(true) — that's the 'visit
begins' event."

**That comment becomes false.** `BeaconActivator.cs:398-406` fires
`OnBeaconActivated` for a *different* beacon on an *already-active* root with
**no `SetActive` cycle** — verified by both later passes reading the branch. The
refresh would run and the budget would not reset.

Unreachable in shipped content today (`BiomeWebGenerator.cs:1456`, `:1676-1682`
ban Chopshop-adjacent-Chopshop), but that is a map-design constraint external to
the controller, not a guarantee the controller may rely on. Its own contract
(`:151-156`) is "fresh visit = fresh budget."

Moving it gives "visit begins" **one cadence and one writer**. Safe: no frame
boundary exists between `SetActive(true)` and the event, and nothing else reads
`_repairBudget` in that window (`EnterRepairMode`'s guard at `:508` needs a
button click, impossible mid-call-stack).

---

## 5. Nothing else changes

`BeaconActivator.cs`, `BeaconSceneBootstrap.cs`, `RunSceneHost.cs` and
`MerchantSceneController.cs` are untouched. No new public surface, no SO change,
no asset edit, no save-format impact.

---

## 6. The regression test — and the trap it avoids

**No behaviour test exists for either controller.** Only
`MerchantSceneRootPrefabAuthoring_Test.cs`, a prefab-shape test.

### The proposed test was WRONG, and would have been instance seven

The `unity-specialist` first proposed `SetActive(false)` → mutate →
`SetActive(true)` → assert refreshed. I verified all four
`OnBeaconActivated?.Invoke` sites live inside `BeaconActivator` (`:299`, `:373`,
`:404`, `:436`) — **a raw GameObject `SetActive` cycle never fires the event.**

| | Result |
|---|---|
| Pre-fix | RED — `Start()` doesn't re-run |
| Post-fix | **RED** — a raw `SetActive` never fires `OnBeaconActivated` |

**It fails on both sides.** It drives a path production never takes. That is the
2026-08-27 pattern restated — a test that names the behaviour and reports on its
own harness artifact. Six tests passed while broken yesterday; this would have
been a seventh, failing while correct.

### Corrected shape

Drive `RunSceneHost.AdvanceToNextBeacon(toIndex, PlayerChoice)` — the same verb
a map click uses, which fires the real `OnBeaconChanged` the activator
subscribes to. `ActivateFor` is private (`:204`) and `OnBeaconChanged` is a
plain C# event (no external `Invoke`), so reflection would be the only
alternative — harness-shaped, rejected.

**Real generation deliberately rejected.** `BiomeWebGenerator.cs:1462-1467`
hard-codes index 1 as Combat (LeftFunnel), so reaching any second beacon means
winning a fight — dragging `CombatLoop`, archetypes and combat resolution into a
UI-refresh test. Instead: hand-built `NodeMap` `Start(0) → Chopshop(1) →
Chopshop(2) → Haven(3)` through the real resume path
(`Initialize` → `BeginRunFromLoaded`), reusing `RunSceneHost_Resume_Test`'s
fixture builders verbatim. `NodeMapDto.ToNodeMap()` yields reference-distinct
`BeaconData` per node, satisfying the `_lastActivatedBeacon` identity gate
(`:295`, `:400`) with no randomness.

### FALSE-PASS TRAP — verified

`ChopshopScreen.uxml:81-83` authors `<ui:Label name="scrap-value" text="0" />`.

**With Scrap = 0, a never-refreshed label reads "0" and the test PASSES while
broken.** The test must use distinct nonzero values: **17** before visit 1,
**+5 → 22** before visit 2.

### Required failure signature on unmodified HEAD

```
Expected: "22"  But was: "0"
```

A plain value mismatch — no exception, no missing-component error. **If HEAD
fails with anything else, the test is broken and gets fixed before any
controller is touched.** Per `feedback-prove-test-fails-on-the-bug`, a
regression lock is worthless until seen failing *for the right reason*.

Assert via `controller.GetComponent<UIDocument>().rootVisualElement
.Q<Label>("scrap-value")` — the idiom
`MerchantSceneRootPrefabAuthoring_Test.cs:84-101` already uses. **No new public
surface.**

Rest gets the same test only if the driver generalises without a second
harness. If not, Rest ships untested and that is accepted — Chopshop is where
slice 4 mounts and where the regression pressure lives.

---

## 7. Sequence

1. This capture, approved.
2. Write the PlayMode test. **Run on unmodified HEAD. Confirm
   `Expected: "22" But was: "0"`.** Do not proceed otherwise.
3. Apply the fix in both controllers.
4. Test green + EditMode suite green (baseline **1163 / 1161 / 0 / 2**).
5. Standalone commit. **Then** slice 4.

Slice 4 depends on this: its garage read surface mounts into Chopshop's
`#modal-container` and needs per-visit refresh, because installed parts change
between Chopshops. Landing it first means inventing a second refresh cadence or
inheriting the broken one — an ADR-0011 bimodal path created by ordering alone.

---

## Technical Director Review

> `technical-director`, 2026-08-28. Scoped to sequencing, `Start()` deletion and
> the test gate — the Unity mechanics were settled by a prior `unity-specialist`
> pass and my own source verification, and TD was explicitly told not to
> re-litigate them.

**1. Sequencing — APPROVE, fix now, standalone commit.**

The "last cheap moment" framing is right but underspecified. The argument is
three-part: (a) **the seam doesn't exist yet** — slice 4 needs per-visit refresh
and would either invent a second cadence or inherit the broken one, an ADR-0011
bimodal path created *by* the ordering choice; (b) **QA substrate** — slice 4's
verification is "open Chopshop, does it show the right parts," and landing it on
a substrate where visit 2+ is stale means every slice-4 bug report is filtered
through a known-broken lens; (c) **blast-radius asymmetry** — the fix is two
files, net-negative lines, zero new types; slice 4 is a new UI surface. Bundling
makes capture, review and rollback straddle both.

Counter-argument considered and rejected: "one bug, one revisit, players
tolerate it." At 2-3 Chopshops/run post-`bee7ba6` this is near-certain per run,
and stale *repair* state means the weld-budget bar can misrepresent — a
resource-facing display error, same class as the 2026-08-27 reward exploit.

**2. Deleting `Start()` — APPROVE, but the body contains dead code. Do not port
verbatim.**

An empty `Start()` is textbook ADR-0011 vestigial surface. Delete outright.

The real finding is what moves. The resume-into-resolved guard was live under
`Start()` (which fires regardless of beacon state) but is **unreachable** under
`OnBeaconActivated`: `ActivateFor` gates at `:215`, and `current` is
`_host.CurrentBeacon` = `_controller.Current` — the same object the guard reads
through `Session.Controller.Current`. Porting it moves a guard that *cannot* be
true into the new handler, where it reads as a live invariant and is never
exercised by any test. That is the ADR-0011 pattern you are deleting `Start()`
to avoid — trading an empty method for a dead branch.

Replace the guard's *intent* with a comment pointing at
`BeaconActivator.cs:215`. Knowledge survives, dead branch does not.

**Adjacent, recommended:** move `_repairBudget = StartBudget` from `OnEnable`
into the handler. After this fix that statement's comment is false on one path
(`:398-406` fires for a different beacon on an already-active root with no
`SetActive` cycle). Two lines, removes a reasoning burden permanently.

**3. Test gate — CONCERNS. The gate is warranted, and the proposed shape would
not have caught the bug.**

Traced against both versions: pre-fix RED because `Start()` doesn't re-run;
post-fix **also RED** because a raw `SetActive` cycle never fires
`OnBeaconActivated`. It fails on both sides — it doesn't discriminate defect
from fix, because it drives a path production never takes. That is the
2026-08-27 failure mode restated: six green tests over broken behaviour and one
red test over correct behaviour are the same bug — the harness isn't wired to
the production trigger.

"Unity lifecycle isn't unit-testable without a Player Loop" is true and
irrelevant — you have one. `RunPrefabAuthoring_Test.cs` already instantiates
`Run.prefab` in a PlayMode assembly that already references `Run`, `Save` and
`UI`. The harness cost is largely paid, and the regression class is about to
acquire a second consumer (slice 4's garage) and eventually a third. Guarding it
once is cheap; discovering it three more times is not.

**Seen-failing is mandatory, not ceremonial.** Because pre-fix and post-fix fail
through *different mechanisms*, "it went red" is insufficient — confirm it went
red *for the stale-display reason*. If it reports a null ref, a missing
subscription or a harness error, the test is broken, not the code.

### TD three-lens self-audit

- **Codebase health.** Grepped for ADR-0011 drift and found one — the dead
  resume guard, named above. No bridges, no parallel storage, no bimodal path
  introduced: the fix *removes* a cadence rather than adding one, and Merchant
  already uses the target shape, so this converges three controllers on one
  pattern rather than forking them. Two controllers will hold identical
  `Awake`-resolve + subscribe + handler triplets; under the pre-emptive-share
  rule that is worth noting, but I explicitly do **not** recommend a shared base
  yet — the shared part is ~6 lines of Unity boilerplate and the bodies differ.
  Revisit at a fourth beacon controller.
- **Optimization.** No delta. The event fires once per beacon activation — the
  correct cadence, nowhere near per-frame. `ShowDialogueEntry` rebuilds a handful
  of `Label.text` values; at ~3 invocations per run it is not worth measuring.
  Method group, not lambda — no closure capture.
- **1.0 survival.** `Action<BeaconData>` is the shape that ships; slice 4's
  garage hooks the same event without a signature change. One risk named: the
  handler currently ignores its `BeaconData` argument (it re-reads through
  `Session`). Keep the parameter anyway — declaring a parameterless handler and
  wrapping would be an ADR-0011 adapter, and that argument is the seam a future
  consumer will want.

---

## 8. Outcome — implemented and verified 2026-08-29

Test: `Assets/Tests/PlayMode/CombatView/ChopshopBeaconRevisit_Test.cs`
(+ `WastelandRun.Run.Authoring` added to the PlayMode asmdef references —
`BiomeDistributionSO` / `BeaconSceneBindingSO` live in that assembly).

**Seen-failing on unmodified HEAD, twice, with the required signature:**

```
Expected: "22"  But was:  "0"
```

Both times the failure landed on the FINAL assertion, not the harness gate —
so visit 1 painted, all three mount/unmount/re-mount assertions passed, and
there was no null ref, missing subscription or harness error. The test
discriminates the defect from its own scaffolding.

**Final state:** PlayMode 3/3 passed. EditMode **1163 / 1161 / 0 failed /
2 skipped**, 0 `error CS` — dead on the pre-slice baseline.

### 8a. Harness correction the three planning passes did not predict

The first version of the test seeded the wallet AFTER `Initialize` mounted
beacon 1, then asserted post-`yield`. That passed on HEAD and FAILED after the
fix — at the harness gate, `Expected: "17" But was: "0"`.

Not a re-clone race, and not a defect in the fix. **The cadence moved earlier in
the frame.** `Start()` ran at end-of-frame, so a value written any time during
that frame beat the paint. `OnBeaconActivated` fires *synchronously inside*
`SetActive(true)`, so the handler correctly painted the wallet as it stood — 0.

Corrected shape: park the resume cursor on **Start(0)** (which `ActivateFor`
sends to `ClearAll`, mounting nothing), seed the wallet there, then reach each
Chopshop by `AdvanceToNextBeacon`. Closer to how a player actually arrives, and
it removes the frame-timing coupling entirely. Node 0 carries `IsResolved=true`
— what a real save holds (`BiomeWebGenerator.cs:1461`), and required because
`CommitNextBeacon:701` throws on an unresolved current beacon.

**Because the test changed shape after its first red, it was re-proven against
stashed controllers rather than trusting the earlier failure.** A regression
lock is validated by the version of it that ships, not by an ancestor.

**The generalisable point:** moving a refresh from `Start()` to a synchronous
activation event moves it EARLIER within the frame, not just to a different
trigger. Any consumer that wrote model state between the mount and end-of-frame
was relying on `Start()`'s lateness. Production is unaffected — `Advance`
commits fuel/storm before `OnBeaconChanged?.Invoke()`, and `BeginRunFromLoaded`
fires it after the controller and session are fully built — but slice 4's garage
mounts into this same event and inherits the same constraint.

### 8b. Deltas from the plan

- **Awake** also LogWarns when no `BeaconActivator` resolves. The plan named the
  `FindAnyObjectByType` fallback but not the diagnostic; a silent null here
  reproduces the exact bug being fixed, with no signal.
- **The subscription sits ABOVE `OnEnable`'s degraded-UXML early-returns** so
  the OnEnable/OnDisable pairing is unconditional. `ShowDialogueEntry` already
  guards on `_root == null`, so a degraded tree fails quietly rather than
  leaving a half-paired subscription.
- **Three stale comments corrected** in `ChopshopWorkbenchController` that
  asserted the OnEnable budget cadence as fact (`_repairBudget` field comment,
  the note inside `ShowDialogueEntry`, and the class-level subscription
  paragraph). Both classes gained a "Per-visit entry refresh" paragraph stating
  there is no `Start()` and that adding one would fork the cadence.

### 8c. Unrelated finding, logged not fixed

`RunSceneHost.cs:1238` still documents `OnCombatModelCommitted` as firing
"BEFORE the beacon is latched resolved" — the inverted ordering the 2026-08-27
resolution-seam slice corrected across 19 other doc blocks. One survivor. Out of
scope here; own commit.

---

## 9. Process note

Three agent passes on a ~30-line fix, which is more than this size normally
warrants. Recorded because each pass changed the outcome rather than confirming
the last:

1. **`unity-specialist`** — rejected my `OnEnable` proposal for the
   `OnBeaconActivated` subscription, and corrected the scope from three
   controllers to two.
2. **`technical-director`** — caught that the specialist's "port the guard
   verbatim" instruction moved dead code, and that its proposed test would fail
   on both sides.
3. **`unity-specialist` (harness)** — found that `ActivateFor` is private,
   rejected real generation for the LeftFunnel-Combat coupling, and caught the
   `text="0"` false-pass trap.

Every load-bearing claim from all three was verified in source before being
acted on, per `feedback-verify-reviewer-claims`. Two claims were corrected by
that verification.

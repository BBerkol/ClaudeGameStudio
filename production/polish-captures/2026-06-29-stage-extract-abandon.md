# Capture — PlayerVehicleStage extract ABANDONED (Option Y)

**Date:** 2026-06-29
**System:** `PlayerVehicleStage` composite (steps 1+2) + Rest beacon bar canvas
**Status:** Pending user approval
**Supersedes:** `production/polish-captures/2026-06-28-player-vehicle-stage-extract.md`
(the original stage-extract plan; steps 3–5 will not ship)

## Why this capture exists

The PlayerVehicleStage extract landed steps 1+2 on 2026-06-28 (commit `0aef61c`):
the `PlayerVehicleStage.cs` MonoBehaviour and its host-side registration plumbing
(`RunSceneHost._stages` + `RegisterStage` + `UnregisterStage`). Steps 3–5
(authoring a `PlayerVehicleStage.prefab` asset, parenting the player vehicle +
WorldSpace bar canvas under it, mounting that composite under Combat and every
beacon root) were never authored.

A 10-pass iterative TD review on the path forward surfaced load-bearing findings
every pass and converged on a disk-verified verdict:

- `PlayerVehicleStage` is mounted on **zero** prefabs. `Combat.prefab` has no
  hits for the component; no `PlayerVehicleStage.prefab` exists; no beacon-root
  prefab instances it.
- `RunSceneHost._stages` is added-to and removed-from but never iterated — the
  xmldoc references an `EnsureWorldCamerasOnStages` method that does not exist.
- Combat's WorldSpace bar canvas worldCamera wire ships through
  `CombatHud.EnsureWorldCameras` (SerializeField-list-bound, Combat-only), NOT
  through the stage. The stage code has been load-bearing on nothing since it
  landed.
- The Rest beacon, which was the original motivation for hoisting the stage,
  currently ships with **no bar canvas at all** under `RestRoot.prefab` — that
  is a real latent Rest Pass 1 bug, but the fix does not require the stage
  composite.

The 1.0-canonical-shape rule (`feedback_demo_forward_over_infrastructure`) and
ADR-0011 (no dead-code/transitional scaffolding at done state) both fire on
keeping ~170 lines of dormant stage plumbing. **Option Y**: delete the stage,
author a `PlayerBarStackCanvas` directly under `RestRoot.prefab` (mirroring how
Combat authors its bar canvas under `LaneAxis`), wire `Camera.main` inline in
`RestPickerController.Awake` with the same idempotent pattern as
`CombatHud.AssignWorldCamera`.

## What's being destroyed

### Code deletions

- **`Assets/Scripts/CombatView/PlayerVehicleStage.cs`** — entire file (89 lines)
  + `.meta`. Sealed `MonoBehaviour` with `Awake`→`RegisterStage` handshake,
  `OnDestroy`→`UnregisterStage`, idempotent `ApplyCamera(Camera)` setter,
  `[SerializeField] Canvas _playerBarStackCanvas` + `PlayerBarStackCanvas`
  property. Mounted on zero prefabs.

- **`Assets/Scripts/CombatView/RunSceneHost.cs`**
  - Lines 99–108: `_stages` field + 9-line xmldoc.
  - Lines 546–548: section banner comment
    `// PlayerVehicleStage registration (2026-06-28 stage-extract)`.
  - Lines 550–575: `RegisterStage(PlayerVehicleStage)` method (19-line xmldoc
    + 6-line body).
  - Lines 577–586: `UnregisterStage(PlayerVehicleStage)` method (4-line xmldoc
    + 5-line body).
  - `using System.Collections.Generic;` at line 2 — **PRESERVED** (still
    consumed by `IReadOnlyList<BeaconData> Beacons` at line 115).
  - `EnsureEventSystem` private method at lines 588–605 — **PRESERVED**.
    Independently load-bearing for non-Combat beacons (Rest / Haven /
    Merchant / Event / Chopshop) where no `CombatHud` is present to create
    the EventSystem itself. Pass 7 found this; Pass 10 confirmed.

- **`Assets/Editor/CombatPrefabAuthor.cs`**
  - Lines 7740–7744: stale comment block "Bar stack canvas + backdrop are NOT
    authored here — designer adds them via prefab-edit" inside
    `AuthorRestRootPrefab`. Comment is wrong post-Option-Y; replaced with a
    short 4-line description matching the new author path.

### Code preservation (called out so reviewer doesn't expect them to go)

- `CombatHud.EnsureWorldCameras` (lines 500–523) + `AssignWorldCamera` (lines
  540–549) — load-bearing in Combat, untouched.
- `CombatHud.EnsureEventSystem` callsite (line 353) + method (lines 1610–1626)
  — defense-in-depth, untouched (Pass 7 verdict).
- `AuthorBarStackCanvas` helper + `BarStackSide` enum
  (`CombatPrefabAuthor.cs` lines 7035, 7049–7125) — untouched. First param is
  named `laneAxis` but is used only as a generic parent transform at line 7057
  (`SetParent` call). Pass 10 dropped the cosmetic param rename — the Rest
  call site adds a single-line comment clarifying the param is "parent
  transform, not Combat-specific LaneAxis."
- `VehicleBarStack.prefab` — exists standalone under `Assets/Prefabs/CombatView/`,
  consumed by both Combat and the new Rest bar canvas.

### Code additions

- **`Assets/Editor/CombatPrefabAuthor.cs`**, `AuthorRestRootPrefab`, after the
  PlayerVehicle parenting at line 7774:
  ```csharp
  // PlayerBarStackCanvas — WorldSpace canvas hosting the player's
  // VehicleBarStack widgets during Rest welding repair drain. Parented
  // directly under RestRoot (no intermediate stage composite). Mirrors
  // Combat's authoring path; RestPickerController wires worldCamera at
  // runtime via the CombatHud.AssignWorldCamera idiom. The first arg to
  // AuthorBarStackCanvas is the parent transform — historically named
  // 'laneAxis' for Combat's call site, but the helper is parent-agnostic.
  GameObject vehicleBarStackPrefab = LoadPrefab("VehicleBarStack");
  AuthorBarStackCanvas(root, "PlayerBarStackCanvas", Vector3.zero,
      vehicleBarStackPrefab, BarStackSide.Player);
  ```

- **`Assets/Scripts/CombatView/RestPickerController.cs`**, `Awake`, after the
  existing `_playerBarStack` auto-resolve at line 165 and before the line 167
  diagnostics:
  ```csharp
  // Mirror CombatHud.AssignWorldCamera — the authored WorldSpace canvas has
  // worldCamera = null at prefab edit time; bind Camera.main at runtime so
  // bar widgets render under the run camera. Idempotent: only writes when
  // worldCamera is currently null.
  if (_playerBarStack != null)
  {
      Camera mainCam = Camera.main;
      Canvas barCanvas = _playerBarStack.transform.parent != null
          ? _playerBarStack.transform.parent.GetComponent<Canvas>() : null;
      if (mainCam != null && barCanvas != null && barCanvas.worldCamera == null)
          barCanvas.worldCamera = mainCam;
  }
  ```

## Final-game picture the change serves

In 1.0, every beacon that hosts the player vehicle as a visible stage element
needs a WorldSpace bar canvas that tracks HP / Armor / Fuel through that
beacon's drain or repair logic. Combat already ships this. Rest needs it
during the welding repair drain (current Rest Pass 1 ships *no* bar canvas at
all — visible regression). Haven / Merchant / Event / Chopshop / EliteCombat
will each need it as their UI lands.

The original stage-extract bet was that a shared `PlayerVehicleStage.prefab`
composite would amortize the bar-canvas authoring across all six beacons. In
practice:

- Only one beacon root (Combat) currently authors the bar canvas.
- The composite's value proposition ("re-instantiable atomic vehicle +
  bars") was never paid for — the stage prefab was never created.
- The runtime camera-wire mechanism (`PlayerVehicleStage.ApplyCamera`) is a
  duplicate of `CombatHud.AssignWorldCamera` reached by a different path
  (host-driven push vs. component-local pull). One idiom is enough.

Option Y converges on the simpler shape: each beacon root authors its bar
canvas directly under its root, each beacon's primary controller
(`CombatHud` / `RestPickerController` / future Haven/Merchant/etc.) wires
`Camera.main` inline at Awake using the same 8-line idempotent pattern.
ADR-0011 clean (no parallel storage, no bimodal paths, no stub returns), no
infrastructure that ships ahead of a second concrete consumer
(`feedback_gdd_verb_signature_not_load_bearing`).

If a 7th beacon ever needs the canvas, lifting the 8-line idiom into a
shared helper is one commit. Until then: zero abstraction, zero dead code.

## Technical Director Review

**Pass 10 verdict — GO with one refinement (2026-06-29):**

Disk-verified all 6 edit surfaces with exact line numbers. Zero hidden
gotchas:

- `using System.Collections.Generic` preserved on `RunSceneHost` (still
  consumed by `Beacons` property).
- `EnsureEventSystem` on `RunSceneHost` preserved (non-Combat beacons need
  it; Pass 7 finding).
- `BarStackSide` enum survives untouched (used by Combat call sites and
  the new Rest call site).
- `VehicleBarStack.prefab` survives as standalone (Loaded by both call
  sites).
- Zero EditMode test references to `PlayerVehicleStage` or `_stages`.
- Zero save-system references (no DTO mentions the stage).
- Zero menu-item references (the `AuthorPlayerVehicleStagePrefab` menu
  item was never written).

Refinement Pass 10 accepted: skip the `AuthorBarStackCanvas` first-param
rename from `laneAxis` to `parent` (cosmetic only, would churn 3 call
sites). The Rest call site adds a one-line comment instead.

Aligns with:

- `feedback_demo_forward_over_infrastructure` (no scaffolding ahead of
  consumers).
- ADR-0011 (no dead code / transitional comments at done state).
- `feedback_aggressive_dead_code_cleanup` (delete dormant code rather than
  retain "in case").
- `feedback_gdd_verb_signature_not_load_bearing` (defer abstraction until
  2+ concrete consumers).
- `project_no_bridges_at_done` (the stage was on track to become a bridge
  to a never-arrived composite).

**Cleared for execution.** Phases 2–5 in `project_stage_extract_in_flight`
memory; that memory will be flipped to "abandoned" after the commit lands.

## Boot Sequence Correction (appended 2026-06-29 post-playtest)

The same playtest that verified the Rest bar canvas rendered also surfaced
a Workstream F (2026-06-17) topology-pivot regression: clicking a Combat
node from mid-map produced no combat and reset the run cursor to the
start, silently, with no exceptions.

### Root cause

`CombatController.Start()` at line 253 of
`Assets/Scripts/CombatView/CombatController.cs` was calling
`_host.BeginNewRun()` unconditionally. This made sense pre-Workstream-F
when `Combat.prefab` was the scene root and `Start()` ran once at scene
load as the canonical bootstrap. Post-Workstream-F:

- `RunScene` is the canonical entry.
- `SaveBootstrap` (line 151 of `SaveBootstrap.cs`) calls
  `RunSceneHost.Initialize(...)` at scene load.
- `Initialize` falls through to `BeginNewRun(null)` at line 265 of
  `RunSceneHost.cs` when no save exists.
- Bootstrap is therefore complete *before* the player ever sees the map.

Post-Option-B (2026-06-28), `Combat.prefab` activates inside an additive
`CombatScene` only after a player clicks a Combat beacon. At that point,
`CombatController.Start()` fires for the first time — and the bootstrap
`BeginNewRun` call inside it re-rolls the entire run, resetting the
cursor to the start. The user lands back on the map with no idea what
happened.

`BeginNewRun` is multi-owned by design (`Initialize` + `RestartRun`); the
`Start` call is dead weight at best, a re-bootstrap landmine at worst —
the playtest just proved which.

### Edits

- **`Assets/Scripts/CombatView/CombatController.cs`**
  - Line 253: deleted `_host.BeginNewRun();`.
  - Lines 248–252 (Start() comment block): rewrote to reflect bootstrap
    ownership by `RunSceneHost.Initialize` and explain the regression
    being prevented.
  - Lines 11–26 (class-doc summary): rewrote — the prior claim that
    "Start only calls RunSceneHost.BeginNewRun" was both stale *and*
    self-contradicting (the same comment said "the controller no longer
    drives scene boot"). Updated to state plainly that `Start` is now
    pure view-attach.

### Why this rides with the stage-extract slice

Slice 9b precedent: when a playtest closing the slice surfaces a latent
bug, the fix lands with the slice that exposed it. Splitting forces a
second capture cycle for a one-line delete with zero authored-content
impact. The fix is also strictly causal to the regression Workstream F
introduced — leaving it open compounds the "Workstream F isn't really
done" debt.

### Validation criterion

Mid-map Combat node click lands in `CombatScene` with the map cursor
preserved at the clicked beacon; no second `BeginNewRun` log line appears
between `Initialize` and `BeginCombatForCurrentBeacon`.

### Boot-ownership contract (locked here)

`RunSceneHost.Initialize` is the sole fresh-run bootstrap entry.
`CombatController.Start` is a pure view-attach (resolves the `CombatHud`
child, validates the host wire-up). `RestartRun` is the only other
caller of `BeginNewRun`. No third caller may be introduced without
revisiting this contract.

## Rest Picker Stuck-State Fix (appended 2026-06-29 post-playtest)

Same playtest surfaced a second stuck state: entering Rest with damage,
picking Repair, and healing every damaged slot before exhausting the
budget left the player frozen in Repair mode with Continue never
revealing.

### Root cause

`RestPickerController.TickRepair` (line 488 pre-fix) only exited Repair
mode when `_repairBudget <= 0`. The mid-drain "hovered slot fully healed"
break at lines 469–477 stops the inner drain loop but doesn't exit
Repair mode — so the player can heal everything, the budget remains, and
the controller waits forever for the budget to exhaust against no
damaged target.

The Show-time empty-vehicle path at lines 358–366 correctly hides the
rail when entering Rest at full HP (the user's original framing of the
bug). The actual gap was the *mid-Repair* "all healed" state.

### Edit

- **`Assets/Scripts/CombatView/RestPickerController.cs`**, `TickRepair`:
  added a `budgetDecrementedThisFrame` flag and extended the exit
  condition to `_repairBudget <= 0 || (budgetDecrementedThisFrame &&
  vehicle.GetDamagedSlots().Count == 0)`. The flag gate preserves the
  alloc-free per-frame hot path the F1 TD fix (2026-06-28) established
  — `GetDamagedSlots()` only fires on the ticks where a repair actually
  completed (handful per second of held-drain), not every frame.

### Validation criterion

Enter Rest with one damaged slot. Hold LMB over it. When the slot
reaches MaxHp before the budget exhausts, Repair mode ends and Continue
reveals automatically.

## Bug 3 fix — async scene load races synchronous OnCombatReady

### Symptom

After Bug 2 fix unmasked it: clicking a Combat beacon correctly does not
reset the cursor any more, but combat is broken on entry — cards rendered
but un-interactable, empty Stats/Log panels, "Turn 3" displayed (expected
Turn 1), AMBUSH banner shown, blue full-screen flash during transition.

### Root cause (disk-verified)

`RunSceneOverlayHost.HandleBeaconClicked` (lines 158–171) fires two
synchronous host calls in sequence:

```csharp
_host.AdvanceToNextBeacon(toIndex, HostAdvanceReason.PlayerChoice);
// synchronous; fires OnBeaconChanged → BeaconActivator.HandleBeaconChanged
// → ActivateFor → SwapToScene → SceneManager.LoadSceneAsync (ASYNC)
// → returns immediately, scene loads next frame(s)

if (arrived.Type == BeaconType.Combat || ...)
    _host.BeginCombatForCurrentBeacon();
// synchronous; calls _session.EnterCombat() (advances model into combat,
// shuffles deck, draws hand, runs ambush) then fires OnCombatReady
```

`OnCombatReady` therefore fires while `CombatScene` is still loading.
`CombatController` lives inside that scene's `Combat.prefab` and only
subscribes to `OnCombatReady` in its `Awake` — which has not run yet.
The synchronous fire misses the controller. `_loop` stays null →
card-drag handlers and End Turn no-op against the live model state.

`CombatHud` is on a sibling canvas under `Run.prefab` (Workstream F
2026-06-17 scene-root split) so it survives, renders the live deck/hand
state, and shows the AMBUSH banner — but without the controller, the
view is decorative only.

This is a topology-pivot regression: pre-Workstream-F + Option B,
`Combat.prefab` was always in-scene at session start so
`CombatController.Awake` had run before any click. Post-pivot, Combat is
behind an async scene load — the synchronous-event contract that
`BeginCombatForCurrentBeacon` assumed is broken.

### Fix — Option A (TD verdict 2026-06-29)

Add one new event on `BeaconActivator`:
`event Action<BeaconData> OnBeaconActivated`. It fires once the target is
mounted and `Awake`-d, regardless of activation mode:

- **PrefabRoot mode** (Rest, Haven, Merchant, Event, Chopshop): fires
  synchronously inside `SwapToPrefabRoot` after `targetRoot.SetActive(true)`.
- **AdditiveScene mode** (Combat, EliteCombat): fires from the
  `loadOp.completed` callback after `SetActiveScene` so the scene's
  components have run their lifecycle.

`RunSceneOverlayHost.HandleBeaconClicked` shrinks to just
`AdvanceToNextBeacon` — no more `BeginCombatForCurrentBeacon` from the
click handler.

`RunSceneHost.OnEnable` auto-resolves the sibling `BeaconActivator` and
subscribes to `OnBeaconActivated`. The handler:

```csharp
private void HandleBeaconActivated(BeaconData beacon)
{
    if (beacon == null) return;
    if (beacon.Type != BeaconType.Combat &&
        beacon.Type != BeaconType.EliteCombat) return;
    if (_session == null || _session.IsInCombat) return;
    BeginCombatForCurrentBeacon();
}
```

The `IsInCombat` guard makes the path resume-safe — a mid-combat
save-resume rehydrates the session into combat already, and the
activation event firing again must not re-enter `EnterCombat`.

### Why not B/C/D (TD analysis)

- **B (pending-loop cache on host)** — ADR-0011 #2 + #6: parallel
  storage + stub-shape getter only non-null in race window.
- **C (host two-step internal pending)** — same shape, state relocated,
  plus bimodal-path #3 (Combat-typed advances secretly async).
- **D (coroutine yield in click handler)** — ADR-0011 #5 compat overload
  via transitional coroutine; UI handler should not know about scene-load
  lifecycles.

Fix A is not a bridge — it fills the activation contract Option B
implied but never finished. The asymmetry (PrefabRoot synchronous,
AdditiveScene async) was already there; making both flow through a
single uniform event removes the asymmetry rather than encoding it as a
branch.

### Edits

- **`Assets/Scripts/CombatView/BeaconActivator.cs`**: new `public event
  Action<BeaconData> OnBeaconActivated` + fire sites — synchronous in
  `SwapToPrefabRoot` after `SetActive(true)`, async in `SwapToScene`'s
  `loadOp.completed` callback after `SetActiveScene`.
- **`Assets/Scripts/CombatView/RunSceneOverlayHost.cs`**, `HandleBeaconClicked`:
  delete the combat-typed `BeginCombatForCurrentBeacon` block (lines 165–170).
- **`Assets/Scripts/CombatView/RunSceneHost.cs`**: new `OnEnable`/`OnDisable`
  pair auto-resolving the sibling `BeaconActivator` via
  `GetComponentInChildren(includeInactive: true)`; new private
  `HandleBeaconActivated(BeaconData)` handler with the guard above.

### Bug 4 probe (separate slice)

TD flagged "Turn 3" displayed on a fresh combat as a likely second bug —
stale `CombatHud` state from a prior combat not cleared on re-entry, or
ambush sequence advancing turn count more than expected. Fix A will not
address it.

Probe: add a one-line `Debug.Log` inside
`RunSceneHost.BeginCombatForCurrentBeacon` immediately after
`_session.EnterCombat()` returns, logging `loop.TurnCount`. After Fix A
ships and combat is interactable again, observe the value:

- If `TurnCount == 1` at the log line but HUD shows `Turn 3` → stale HUD
  subscriber state (file Bug 4 against `CombatHud`).
- If `TurnCount == 3` at the log line → model-side bug in
  `RunSession.EnterCombat` / ambush sequence (file Bug 4 against
  `RunSession`).

Either way, file Bug 4 as a follow-up slice; do not bundle.

### Validation criterion

- Click Combat beacon from mid-map. Cursor advances, additive
  CombatScene loads, combat is fully interactable: card drag onto enemy
  slot resolves, End Turn fires the enemy turn, log lines stream.
- Click Rest beacon from mid-map. RestRoot prefab activates
  synchronously, Repair drain runs, Continue advances.
- Save mid-combat, reload, resume: scene reloads, session rehydrates;
  `OnBeaconActivated` fires but the `IsInCombat` guard blocks a second
  `EnterCombat`. Combat remains interactable post-resume.
- New EditMode test (TD ask #5): assert `OnCombatReady` fires *after* a
  fake activator's activated event, never before.

---

## Bug 6 — BeaconActivator idempotent return suppresses OnBeaconActivated for same-scene/root beacon transitions (2026-06-29)

### Symptom

After Fix A landed, the first combat ran clean. The second combat hop
silently no-op'd — clicking a fresh Combat beacon advanced the map
cursor (a `BeaconData` flipped to current), but combat did not start,
no `OnCombatReady` fired, the player was stranded on the map. Clicking
a subsequent beacon then threw:

```
InvalidOperationException: CommitNextBeacon called while current beacon
(index=13, type=Combat) is unresolved
```

User report: "combat did not trigger and i hopped on to that node" +
the throw above on the next click. Same pattern for Rest → Rest: first
Rest opened the picker, second Rest activated the prefab root visually
but the picker showed the previous session's state (a downstream Bug 8
symptom, separately filed).

### Root cause — disk-verified

All `BeaconType.Combat` beacons resolve to the same
`BeaconSceneEntry.ScenePath` (`CombatScene.unity`) via
`BeaconSceneBindingSO`. All `BeaconType.Rest` beacons resolve to the
same `PrefabRootEntry.Root` (`RestRoot` GameObject under the activator).

`BeaconActivator.SwapToScene` early-returned at the top:

```csharp
if (targetPath == _loadedScenePath && _activePrefabRoot == null)
    return;
```

`BeaconActivator.SwapToPrefabRoot` had the mirror guard:

```csharp
if (_activePrefabRoot == targetRoot && string.IsNullOrEmpty(_loadedScenePath))
    return;
```

Both guards keyed on *scene/root load state*, not on *which beacon is
currently activated*. Same scene loaded → silent return BEFORE
`OnBeaconActivated` fires. `RunSceneHost.HandleBeaconActivated` never
runs → `BeginCombatForCurrentBeacon` never runs → cursor sits on an
unresolved Combat beacon → next click trips
`CommitNextBeacon`'s "unresolved" invariant.

Fix A delivered the activation event but the activation **contract** —
"every beacon-bound activation must fire `OnBeaconActivated` exactly
once" — wasn't enforced when the scene/root happened to already be
loaded. The contract was implicitly "every *load* fires the event",
which collapses when consecutive beacons reuse the same target.

### Fix — beacon-identity activation gate

New private field `BeaconData _lastActivatedBeacon` on
`BeaconActivator`. Both swap methods now distinguish on beacon
identity, not scene-load state:

- **Same beacon reference** (`ReferenceEquals(_lastActivatedBeacon, beacon)`)
  → true idempotent silent return. Covers the bootstrap-then-immediate-
  OnBeaconChanged double-call and any defensive re-entries.
- **Different beacon, scene/root already loaded** → fire
  `OnBeaconActivated(beacon)`, update `_lastActivatedBeacon`, skip the
  redundant `LoadSceneAsync` / `SetActive(true)`.
- **Different beacon, fresh load** → existing path. Update
  `_lastActivatedBeacon` at the fire site (inside
  `loadOp.completed` for AdditiveScene; synchronously after
  `SetActive(true)` for PrefabRoot).

`SwapToScene` signature widened to `SwapToScene(BeaconData beacon,
string targetPath)`; `SwapToPrefabRoot(BeaconType type)` widened to
`SwapToPrefabRoot(BeaconData beacon)` (type recovered as
`beacon.Type` inside). Stale captured-via-host `beaconForEvent`
local removed — `beacon` parameter is the canonical source.

`ClearAll` resets `_lastActivatedBeacon = null`. When the cursor lands
on `BeaconType.Start` or a resolved beacon, nothing is "currently
activated"; the next `ActivateFor` must fire fresh even if the next
scene/root happens to match the just-unloaded one (defensive — `Start`
fires once at `BeginNewRun`, but the symmetry matters for the next
post-resolve advance).

### Why this is canonical, not a bridge (ADR-0011 audit)

The contract was always "every activated beacon fires
`OnBeaconActivated`". The first cut keyed on load state because no two
beacons shared a target at the time. Once Combat scenes were unified
and Rest got a single prefab root (the Option B topology), load-state
keying became insufficient. Beacon-identity keying is the *correct*
implementation of the same contract, not a parallel system.

- **No parallel storage**: `_lastActivatedBeacon` is the activation
  tracker; `_loadedScenePath` / `_activePrefabRoot` remain the
  scene/prefab tracker. Different concerns; no overlap.
- **No bimodal path**: both same-target and fresh-target cases fire
  `OnBeaconActivated` exactly once. The two-branch structure is a
  short-circuit, not a duplicate path.
- **No legacy/transitional naming**: `_lastActivatedBeacon` is a
  permanent field of the activator's data shape.

### Edits

- **`Assets/Scripts/CombatView/BeaconActivator.cs`**:
  - New field `private BeaconData _lastActivatedBeacon` with rationale
    comment alongside other state fields.
  - `SwapToScene` signature changed to `(BeaconData beacon, string
    targetPath)`; idempotent guard rewritten to fire
    `OnBeaconActivated` when beacon differs from
    `_lastActivatedBeacon`; `loadOp.completed` callback updates
    `_lastActivatedBeacon` before firing the event.
  - `SwapToPrefabRoot` signature changed to `(BeaconData beacon)`;
    same idempotent-guard rewrite; synchronous fire site at bottom
    updates `_lastActivatedBeacon` before firing.
  - `ActivateFor` updated to pass `current` through to both swap
    methods.
  - `ClearAll` resets `_lastActivatedBeacon = null`.

### Validation criterion

- Combat A → Combat B: clicking the second Combat beacon while
  `CombatScene` is still loaded fires `OnBeaconActivated` for the new
  beacon, `RunSceneHost.HandleBeaconActivated` runs,
  `BeginCombatForCurrentBeacon` calls `EnterCombat`, the new combat is
  interactable. No `CommitNextBeacon` throw on subsequent clicks.
- Rest A → Rest B: same pattern. (Picker re-show is Bug 8, separately
  filed — Bug 6 verifies the event fires, not the picker behavior.)
- Bootstrap → first beacon advance to the same scene: no double-fire
  (same beacon reference both times → second call short-circuits at
  `ReferenceEquals`).

---

## Bug 10 — `RunSceneOverlayHost.HandleBeaconChanged` predicate too narrow

**Date filed:** 2026-06-29 (exposed by Bug 6 verification playtest)

### Symptom

Second Combat click (Combat A → Combat B, both sharing CombatScene
additive load) drives `EnterCombat` correctly (probe log fires,
`IsInCombat=true`, subsequent Rest click throws `Cannot Advance while
a combat is in flight` — confirming model side is in combat) but the
view shows the map and the HUD remains hidden. Combat is unplayable.

First combat A works because Combat A's scene LoadSceneAsync defers
`OnBeaconActivated` to a later frame, giving
`RunSceneOverlayHost.HandleBeaconChanged` time to run first and
"correctly" show the map, then `BeaconActivator` fires
`OnBeaconActivated` next frame → `BeginCombatForCurrentBeacon` →
`HandleCombatReady` → `_mapView.Hide()` + `RaiseOverlayHidden()` →
HUD `SetActive(true)`. The map-then-hide flicker is invisible because
they're in adjacent frames.

Combat A → Combat B breaks because Bug 6's idempotent branch
(scene already loaded, just re-fire `OnBeaconActivated` for the new
beacon) is **synchronous** — fires inside the same call stack as
`OnBeaconChanged`. If `BeaconActivator.HandleBeaconChanged` is invoked
*before* `RunSceneOverlayHost.HandleBeaconChanged` on
`RunSceneHost.OnBeaconChanged`'s multicast, the entire
BeginCombat → HandleCombatReady → Hide map + Hide HUD chain runs to
completion synchronously, *then* `RunSceneOverlayHost.HandleBeaconChanged`
runs *last* and **re-shows the map + re-raises OverlayShown** → HUD
goes `SetActive(false)` again. The map-mutex predicate only fires for
unresolved Rest; unresolved Combat falls through to "show map."

### Root cause

The predicate `restPickerModal = current.Type == Rest && !IsResolved`
is too narrow. It encodes "Rest is the only beacon type where the map
must hide because something else is the current presentation." But
Combat and EliteCombat have exactly the same property — they take
over the current presentation, and the map must hide under them.

The previous code "worked" because the async scene-load path masked
the bug: `OnBeaconActivated` fired on a later frame, so
`HandleCombatReady` ran after `HandleBeaconChanged` finished, and
the second `_mapView.Hide()` won the race. The fix for Bug 6 made the
re-entrant case synchronous, exposing the predicate gap.

### Technical Director Review

**Verdict:** APPROVE Option (B) — fix the predicate, not the timing.

Rejected Option (A) (defer fire via `yield return null` coroutine):

> "Option (A) is a bridge in disguise. It papers over the symptom by
> codifying an implicit ordering contract ('subscribers must observe
> async-fire timing even when no async work happens') instead of
> fixing the predicate gap. ADR-0011 violation: parallel timing path
> between the sync (idempotent) and async (load) branches that
> callers can't see and can't validate. Future Bug 8 (Rest re-show
> on PrefabRoot SetActive cycle) would compound the same trick."

Approved Option (B):

> "OverlayHost already owns the 'is the map the current presentation?'
> decision — the Rest-unresolved branch proves it. The bug is the
> predicate is too narrow, full stop. The canonical predicate is
> `beacon ∈ {Start} ∪ {IsResolved}` (equivalently
> `!beacon.IsActiveEncounter`). ADR-0015-shaped narrowing at the
> predicate, not branching at the subscriber. Subscriber order
> becomes irrelevant — shuffling subscriptions should change nothing
> observable. That's the validation criterion. Bug 8 (next slice)
> extends as a predicate-sibling, not a new mechanism."

Reinforcing data: `BeaconActivator.cs:211` already uses the exact
same predicate (`current.Type == BeaconType.Start ||
current.IsResolved`) when deciding "is the current beacon presentable
as 'just hanging out on the map'." Mirroring that here unifies the
shared definition.

### Fix shape

Change the predicate in `RunSceneOverlayHost.HandleBeaconChanged`
from:

```csharp
bool restPickerModal =
    current != null &&
    current.Type == BeaconType.Rest &&
    !current.IsResolved;
```

to a positive "is the map the current presentation?" check that
mirrors `BeaconActivator.ActivateFor`:

```csharp
bool mapIsCurrent =
    current == null ||
    current.Type == BeaconType.Start ||
    current.IsResolved;
```

`if (mapIsCurrent) Show; else Hide`. Same shape, broader coverage.
The `current == null` arm preserves the bootstrap behaviour (no
beacon yet → safe to show map).

### ADR-0011 audit

- **No bridges**: the predicate is the canonical definition shared
  with `BeaconActivator.ActivateFor`. No parallel storage, no
  bimodal paths, no stub.
- **No timing contract**: removing the subscriber-order dependency
  is the whole point — subscriber order becomes irrelevant.
- **No vestigial naming**: `restPickerModal` → `mapIsCurrent`
  reflects the actual decision being made (map presentation, not
  Rest specifics).

### Edits

- **`Assets/Scripts/CombatView/RunSceneOverlayHost.cs`**:
  - `HandleBeaconChanged` predicate rewritten as above.
  - Branch direction flipped (positive `if (mapIsCurrent) Show` /
    `else Hide`) so the predicate name matches the dominant arm.
  - Comment block updated: the doc above the predicate no longer
    talks only about Rest; it explains the general rule and the
    subscriber-order resilience guarantee.

### Validation criterion

- Combat A → Combat B sync path: map hides, HUD stays visible, new
  combat is interactable.
- Rest A → Rest B: picker shows, map hides. (Rest re-show on
  PrefabRoot SetActive cycle is Bug 8.)
- Shuffling subscriber order of `RunSceneHost.OnBeaconChanged`
  should change *nothing* observable — both subscribers can now
  fire in either order and reach the same end state.

---

## Bug 6b — Bug 6 idempotent branch fires prematurely during in-flight load (boot regression)

**Date filed:** 2026-06-29 (exposed by playtest of save with Combat cursor)

### Symptom

Boot from a save with `cursor = Combat beacon, unresolved` lands in
CombatScene with no HUD, no card hand, no End Turn, no interaction.
Combat is "running" model-side (`_session.IsInCombat == true`) but the
view layer never wired up to the loop.

### Root cause

Bug 6's idempotent branch in `BeaconActivator.SwapToScene` uses
`targetPath == _loadedScenePath && _activePrefabRoot == null` as its
"scene is already loaded" gate. That's the wrong check:
`_loadedScenePath == targetPath` means *we started loading to that
path*, not *the scene is live*. During the in-flight window
(`_swapInFlight == true`), `CombatController` and `CombatHud` do not
yet exist — they're in the still-loading additive scene.

The boot path triggers this hole:

1. `SaveBootstrap.Start` → `_host.Initialize` →
   `BeginRunFromLoaded` → fires `OnBeaconChanged`.
2. `BeaconActivator.HandleBeaconChanged` → `SwapToScene` for the
   Combat target → falls through to the full async path → sets
   `_loadedScenePath`, `_swapInFlight=true`, starts
   `LoadSceneAsync`.
3. Initialize returns. SaveBootstrap calls
   `activator.LoadCurrentBeaconAsync()` (the "close the OnEnable
   subscription race" safety net, line 167).
4. Re-enters `SwapToScene` → Bug 6 idempotent branch:
   `targetPath == _loadedScenePath && _activePrefabRoot == null`
   → TRUE → fires `OnBeaconActivated` **before scene is live**.
5. `RunSceneHost.HandleBeaconActivated` →
   `BeginCombatForCurrentBeacon` → `_session.EnterCombat()` →
   fires `OnCombatReady`. Only subscriber is OverlayHost
   (CombatController and CombatHud don't exist yet).
   `OverlayHost.HandleCombatReady` runs → `RaiseOverlayHidden`
   → no CombatHud subscribed → no effect.
6. `_session._inFlight` is set. Combat is "running" model-side
   with no view bound.
7. Async load eventually completes → `loadOp.completed` fires
   `OnBeaconActivated` again → `RunSceneHost.HandleBeaconActivated`
   checks `IsInCombat == true` → returns early.
   `CombatController` subscribed after Awake but `OnCombatReady`
   already fired and won't fire again.

Pre-Bug-6 code had a silent `return` in that idempotent branch —
the boot's double-fire was a no-op, and the canonical fire site was
`loadOp.completed`, guaranteed to run after the scene was live.
Bug 6 turned that silent return into "fire OnBeaconActivated for the
new beacon," which is the right shape *only when the scene is
actually live*.

### Fix shape

Two gates added to `BeaconActivator.SwapToScene`:

1. The Bug 6 idempotent branch gains `!_swapInFlight`:
   ```csharp
   if (targetPath == _loadedScenePath && _activePrefabRoot == null && !_swapInFlight)
   ```
   While a swap is in flight, the scheduled `loadOp.completed` is
   the canonical fire site. Don't pre-fire.

2. The existing `if (_swapInFlight)` reentrant guard split into two
   arms: same-target → silent no-op (the SaveBootstrap belt-and-
   suspenders boot pattern); different-target → keep the warning
   (true reentry across different scenes).

### ADR-0011 audit

- **No bridges**: `_swapInFlight` is the canonical "is the swap
  live" flag; it was already authoritative for the reentry guard.
  Adding it to the idempotent gate unifies the definition.
- **No timing contract**: the fix removes a timing-dependent race
  (whether the explicit `LoadCurrentBeaconAsync` happens before or
  after `loadOp.completed`), replacing it with a state-machine gate
  (`_swapInFlight == false ⇔ scene is live`).
- **No vestigial naming**: the comment on the idempotent branch now
  explains the gate intent; the in-flight branch's split arms are
  named by purpose, not by call site.

### Edits

- **`Assets/Scripts/CombatView/BeaconActivator.cs`**:
  - Bug 6 idempotent branch gate broadened from
    `(targetPath == _loadedScenePath && _activePrefabRoot == null)`
    to `(... && !_swapInFlight)`. Comment block above the branch
    rewritten to explain why the in-flight window must not pre-fire.
  - `if (_swapInFlight)` reentrant guard split: same-target →
    silent return (boot double-call no-op); different-target →
    existing warning, now annotated with the in-flight target path
    for diagnosability.

### Validation criterion

- **Boot from saved Combat cursor**: scene loads, CombatHud
  visible, card hand built, End Turn bound, combat interactable.
- **Boot from saved Rest cursor**: PrefabRoot SetActive toggle,
  picker shows. (Sync path, no in-flight window; should already
  work, this fix is a no-op for prefab-root mode.)
- **Combat A → Combat B during run** (Bug 6's intended case):
  A's load completes (`_swapInFlight=false`) → B click → idempotent
  branch fires `OnBeaconActivated` for B. Unchanged.
- **No boot console noise**: same-target in-flight branch is
  silent; the warning only fires for actual reentry across
  different scenes.

---

## Bug 11 — Right-click cancels Rest Repair mode (2026-06-29)

### What surfaced

Playtest: player picked Repair, entered Weld mode, then had no
way out short of draining the full 20-HP budget or healing every
damaged slot. Continue button was visible (post Rest-Continue-
always-visible fix), but clicking it from Weld mode would fall
through to `OnContinueClicked` and `ResolveRest` mid-drain. The
player asked for the standard "right-click cancel current action"
affordance.

### Root cause

`RestPickerController.Update()` only called `TickRepair()` while
`_inRepair`. No input listener for the right mouse button — Weld
mode had only two exit conditions (budget exhausted, all slots
healed) plus the Continue-mid-drain fallback added on the same day.
The repair-mode UX was missing the inverse of "click Repair button":
"right-click anywhere → back to picker."

### Fix shape

In `RestPickerController.Update()`, before `TickRepair()`:

```csharp
if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
{
    ExitRepairMode();
    ShowPicker();
    return;
}
```

- `ExitRepairMode` tears down hover subs, restores cursor, stops
  sparks, hides budget bar.
- `ShowPicker` rebuilds the rail + Continue from the same canonical
  show path the SetActive-cycle entry uses — no second show shape,
  no parallel "return to rail" path. ADR-0011 single-path-clean.
- `Mouse.current` null-guard mirrors the pattern in `TickRepair`
  (player without a mouse device shouldn't NRE).
- `wasPressedThisFrame` (not `isPressed`) — single trigger per click,
  no held-button repeated retries.

### Files

- **`Assets/Scripts/CombatView/RestPickerController.cs`** — `Update`
  body extended with the right-click branch before `TickRepair`.

### Validation criterion

- Enter Rest → click Repair → cursor swaps to weld → right-click →
  cursor restores, budget bar hides, rail re-shows with Continue
  visible. Hover budget bar gone, repair drain stopped, hovered
  slot cleared.
- Right-click outside Weld mode → ignored (the guard above the
  branch is `if (!_inRepair) return`, so right-click before
  entering Repair is a no-op).
- Continue from rail post-cancel → `ResolveRest` fires cleanly,
  rest resolves, map re-shows.

---

## Bug 12 — Combat HUD missing on second combat (subscriber-order race)

### What surfaced

Playtest after Bug 10 + Bug 6 / 6b landed: first combat boot path
healthy, HUD wired, run interactable. After resolving combat 1
(reward pick → map shown), player clicked combat 2. CombatScene
loaded, vehicles + main HP bars + per-subsystem bars visible, but
no cards, no End Turn button, no energy orb, no pile chips — the
entire `Combat_HUD` screen-space canvas was inactive over a
visually-correct world-space layer.

### Root cause — `RaiseOverlayShown` fires on the wrong branch

Bug 10 broadened `RunSceneOverlayHost.HandleBeaconChanged`'s
predicate from "Rest unresolved only hides the map" to "any
non-Start unresolved beacon hides the map" — so a combat-beacon
click now takes the `mapView.Hide()` branch from this handler.
Same handler still called `RunOverlayEvents.RaiseOverlayShown()`
unconditionally after the map branch.

`OnBeaconChanged` has two subscribers — `RunSceneOverlayHost` and
`BeaconActivator`. Post Bug 6, `BeaconActivator.HandleBeaconChanged`
fires `OnBeaconActivated` *synchronously* on the
same-scene-different-beacon path (Combat A → Combat B reuse
CombatScene.unity), which chains synchronously into
`RunSceneHost.HandleBeaconActivated` → `BeginCombatForCurrentBeacon`
→ `OnCombatReady` → `RunSceneOverlayHost.HandleCombatReady` →
`RaiseOverlayHidden` (HUD shows).

If `BeaconActivator`'s `OnBeaconChanged` subscriber runs first, the
entire `OnCombatReady → RaiseOverlayHidden` chain completes inside
that synchronous call. Then the multicast continues to
`RunSceneOverlayHost.HandleBeaconChanged`, which finally runs and
calls the unconditional `RaiseOverlayShown` — re-hiding the HUD.

Bug 10's verdict claimed the predicate fix was "not subscriber-order
sensitive" — which was true for the map's visibility state, but
**not** for the HUD-hide event. The event-raise side of the handler
slipped past the original audit.

### ADR audit

- **ADR-0011 #3 (bimodal paths)**: Same handler driving two semantic
  states (map shown / map hidden) but firing the same overlay event
  on both is the same shape Bug 10 fixed for the map — applied to
  the event raise. The fix removes one of the two paths.
- **ADR-0011 #4 (vestigial enums)**: N/A.
- **Subscriber-order sensitivity**: this is the same anti-pattern
  Bug 10 was supposed to retire. Bug 12 is "Bug 10 didn't go far
  enough" — closing the same audit on the matching event-raise.

### Fix shape

Move `RunOverlayEvents.RaiseOverlayShown()` *inside* the `mapIsCurrent`
branch. The `!mapIsCurrent` branch raises nothing — the next surface
(Combat scene's `CombatHud` via `RaiseOverlayHidden` on `OnCombatReady`,
or beacon-root prefab via `BeaconActivator` unloading the previous
scene) owns its own visibility transition.

```csharp
if (mapIsCurrent)
{
    _mapView.Bind(...);
    _mapView.Show();
    RunOverlayEvents.RaiseOverlayShown();
}
else
{
    _mapView.Hide();
    // No raise — handled by the next surface's own state-change driver.
}
```

### Validation criterion

- **Combat 1 → reward → combat 2 click**: shuffling the two
  `OnBeaconChanged` subscribers' order changes nothing observable.
  HUD active, cards in hand, End Turn responsive, energy/pile chips
  visible.
- **Boot into Combat (saved cursor)**: HUD active on first frame
  after scene load completes. Unchanged.
- **Boot into Rest (saved cursor)**: PrefabRoot SetActive, picker
  shows, HUD inactive (no CombatScene loaded, so nothing to hide).
  Unchanged.
- **Combat → Rest → Combat sequence**: HUD active across the full
  loop; previous CombatScene unloads cleanly when RestRoot
  activates; new CombatScene loads with HUD live.

### Files

- **`Assets/Scripts/CombatView/RunSceneOverlayHost.cs`**:
  - `HandleBeaconChanged`: `RaiseOverlayShown` moved inside the
    `mapIsCurrent` branch. `!mapIsCurrent` branch annotated with
    why no event is raised.

---

## Polish 1 — Rest rail shape constant across visits + disabled-button styling

### What surfaced

Two cosmetic issues, one structural and one CSS, surfaced together on the
post-Bug-12 playtest of the Rest beacon:

1. **Empty-vehicle Rest hid the entire rail.** On the first Rest visit of a
   fresh run (no damage yet), `ShowPicker` added `is-hidden` to
   `_buttonRail`, leaving only the Continue button. Second Rest visit (after
   taking damage in combat) showed the full rail. The shape of Rest changed
   between visits — confusing player feedback ("am I seeing the wrong UI?
   is there a leftover state?").
2. **Forge + Upgrade looked active despite `SetEnabled(false)`.** The
   `.wr-button:hover` rule in `controls.uss` paints the orange-accent
   hover state regardless of the disabled flag. UI Toolkit's
   `.wr-button:disabled` only muted the text color. The mouse cursor lit
   the disabled buttons orange on hover — implying clickability.

### Fix shape

**Rail constant across visits, Repair gated on damage count:**

```csharp
Vehicle vehicle = Session?.Controller.State.PlayerVehicle;
bool hasDamage = vehicle != null && vehicle.GetDamagedSlots().Count > 0;
_forgeButton.SetEnabled(false);
_upgradeButton.SetEnabled(false);
_repairButton.SetEnabled(hasDamage);

_buttonRail.RemoveFromClassList("is-hidden");
// (Empty-vehicle is-hidden branch deleted — rail shape is invariant.)
```

The empty-vehicle path's intent (don't offer a no-op Repair button) is now
expressed via `SetEnabled` on Repair only. All three buttons render in all
three Rest visits; only their enabled state changes.

**USS `:disabled` styling — half-transparent, no hover accent:**

```css
.wr-rest-action:disabled {
    opacity: 0.5;
}

.wr-rest-action:disabled:hover {
    background-color: var(--wr-color-bg-elevated);
    color: var(--wr-color-text-muted);
}
```

The `:disabled:hover` rule pins the disabled background/color, neutralising
the `.wr-button:hover` orange tint inherited from `controls.uss`.

### ADR audit

- **ADR-0011 #3 (bimodal paths)**: empty-vehicle Rest vs damaged-vehicle Rest
  were two visually distinct shapes of the same encounter. Collapsing to one
  shape (rail always present, Repair-enabledness varies) retires the bimodal
  surface.
- **ADR-0014 (UI Toolkit primary)**: `:disabled` pseudo-class is the
  canonical UI Toolkit affordance for inert controls; no `pointer-events`
  hacks or `SetEnabled` + class-list dual-write.

### Validation criterion

- **Fresh-run first Rest** (no damage): all three buttons visible. Repair
  disabled (half-transparent, no orange hover). Continue button revealed.
  Clicking Repair → no-op (disabled). Clicking Continue → resolves rest,
  forward edge re-shows.
- **Second Rest after damage**: all three buttons visible. Repair enabled
  (full opacity, orange hover). Forge + Upgrade still disabled. Clicking
  Repair → enters Weld mode (existing behaviour). Right-click cancels
  (Bug 11 path unchanged).
- **Hover disabled buttons**: no orange accent on Forge/Upgrade in either
  visit; no orange accent on Repair when vehicle is undamaged.

### Files

- **`Assets/Scripts/CombatView/RestPickerController.cs`** — `ShowPicker`:
  - Empty-vehicle `is-hidden` branch removed.
  - `_repairButton.SetEnabled(true)` → `_repairButton.SetEnabled(hasDamage)`.
  - Comment block updated to describe constant-rail intent.
- **`Assets/UI/RestPicker.uss`** — added two rules:
  - `.wr-rest-action:disabled { opacity: 0.5; }`
  - `.wr-rest-action:disabled:hover` override pinning background +
    text-muted color.

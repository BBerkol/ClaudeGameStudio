# TD Verdict — Out-of-Fuel Game-Over Screen

**Date:** 2026-07-06
**Slice:** V3 Fuel-as-Clock Slice E polish (final)
**Verdict:** ACCEPT WITH AMENDMENTS (A1 + A2 below)

> **Amendment 2026-07-06 (end of session):** User pivoted the trigger design.
> Stranded is no longer an instant game-over — instead it kicks off an
> automatic storm-advance countdown. The storm ticks forward strip-by-strip
> until it engulfs the parked player's beacon; only then does the game-over
> screen appear. The primitives in this verdict (`IsStrandedForFuel`
> predicate, `OnRunStranded` event) stay valid but their semantics shift:
> the event now signals "begin auto-storm-advance mode", not "show
> game-over". A new event will fire on actual storm engulfment to trigger
> the screen. See memory `project_out_of_fuel_gameover_v2.md` and the
> RESUME block in `production/session-state/active.md` for the pivot
> details and next-session plan.

## Files touched

- `Assets/Scripts/Run/RunSession.cs` — new `IsStrandedForFuel()` public read-only predicate
- `Assets/Scripts/CombatView/RunSceneHost.cs` — new `OnRunStranded` event; check fires from `AdvanceToNextBeacon` AND `BeginRunFromLoaded` (per A1)
- `Assets/UI/GameOverView.uxml` — new
- `Assets/UI/GameOverView.uss` — new
- `Assets/Scripts/UI/GameOverViewController.cs` — new MonoBehaviour + Bind + Show/Hide + OnRetryRequested event
- `Assets/Scripts/CombatView/RunSceneOverlayHost.cs` — new `_gameOverView` field, `OnRunStranded` subscription, HandleStranded → hide map + show game-over view
- Run.prefab authoring — new UIDocument on the persistent host GameObject (baked by CombatPrefabAuthor.AuthorRun after this slice)

## Proposal

Ship the deferred fuel-empty failsafe called out in `FuelState.cs:59` xmldoc.
Trigger definition: **no forward edge from the current beacon is
affordable** (Haven arrivals always affordable; non-Haven arrivals
affordable iff `Fuel.Current >= PreviewSpend(baseCost, chassisMult).FuelDrained`).
Same predicate `RunSceneOverlayHost.BuildBeaconAffordability` uses per-chip
for the red "unaffordable" tint; the new `IsStrandedForFuel` is that
predicate reduced across the whole frontier.

## Technical Director Review

### Verdict: ACCEPT WITH AMENDMENTS

Shape is 90% right. Two amendments required for clean landing.

### What's right

- **Predicate on RunSession** — correct home. `_beaconFuelCosts` +
  `_chassisFuelBurnMultiplier` + `PreviewBeaconArrival` all live there.
  Placing it on RunController would force a bridge back to fuel state.
- **Reduction of existing per-chip logic** — `IsStrandedForFuel` =
  AND-across-chips of the same predicate
  `RunSceneOverlayHost.BuildBeaconAffordability` already computes. One
  arithmetic sibling, two surfaces. Clean.
- **Event ordering (OnBeaconChanged → OnRunStranded)** — correct. Map
  paints latest state, then game-over supersedes. Reversing it opens a
  one-frame click-routing gap.
- **Retry via `RestartRun`** — reuses the F5 / CombatOutcomeOverlay path.
  One restart flow, no parallel.
- **ADR-0011 clean** — additive event + additive predicate + additive UI.
  Not a bridge.
- **ADR-0014 clean** — UI Toolkit on new UIDocument. `SetEnabled(false)`
  on RETURN TO MENU mirrors Rest Forge/Upgrade fade — established
  pattern, not a stub.
- **Run.prefab drift, not Combat.prefab drift** — pre-author-bake
  sentinel is flagged on Combat.prefab only; this slice touches Run.prefab.

### Amendment A1 — REQUIRED

**Call `IsStrandedForFuel()` from `BeginRunFromLoaded` too, not just
`AdvanceToNextBeacon`.**

Rationale: save-resume rehydrates the exact `RunState` that was on disk.
If the player quit while stranded, booting back to the map and having to
click a chip to discover they're stuck is a **latent failure state
surfacing as UX** — the affordability tint is not sufficient signal on a
cold boot; player context is gone. The predicate is pure arithmetic on
hydrated state; calling it post-hydration is a two-line addition with
zero new mutation surface. Same `OnRunStranded` event — one event, two
entry points, symmetric. Missing this on resume is the classic "silent
gate" the whole slice exists to close.

### Amendment A2 — REQUIRED

**Reject a new `RunStatus.Stranded` enum value. Keep `Status = Ongoing`
and treat stranded as an event, not a status.**

Rationale: adding `Stranded` forces every `RunStatus` consumer (save
DTOs, `CombatOutcomeOverlay` dispatch, `RunController` guards) to grow
a new arm for a state that has exactly one behavior: show the game-over
screen. That's a bimodal path per ADR-0011 for a presentation-only
distinction. The current shape (Ongoing + event) treats stranded as
**an event, not a status** — correct. This verdict documents that call
explicitly so a future refactor doesn't "clean it up" by adding the
enum. RunController.State.Status stays a *terminal-state* signal
(Ongoing / Victory / Defeat); "player has no legal move" is a
presentation concern that fires an event and lets the overlay layer
handle it.

### Q3 — event surface

`Action` with no payload is fine. `RunSceneOverlayHost` already has
`_session` reference and reads `state.NodeMap.Beacons` for the
nodes-cleared count. Passing an int through the event is redundant
coupling.

## Success criteria

- Click stranded chip → game-over screen appears frame N+1 after
  `Advance` commits.
- Save while stranded → reload → screen appears within one frame of
  `BeginRunFromLoaded` completing (A1).
- Retry button → fresh `RunSession` via `RestartRun`, no residual overlay.
- No new `RunStatus` enum values (A2).
- No Combat.prefab regen — sentinel remains flagged for the outstanding
  Combat-scope drift; this slice does not touch it.
- `Mastery Progression` bar renders with title text only, no fill, no
  binding — future wiring tracked by memory
  `project_mastery_xp_wiring_pending`.

## Final-game picture

Fuel-as-Clock is the pacing spine of the run. The stranded moment is
the natural failure state of that subsystem. Without this screen the
player just clicks a beacon and nothing happens (silent gate at
`HandleBeaconClicked`). Adding the game-over screen closes the loop:
fuel matters visibly on the run HUD, unaffordable chips tint red,
stranded triggers the screen. Retry funnels through the same
`RestartRun` path as F5 debug and the CombatOutcomeOverlay defeat
button — one restart flow.

---

## 2026-07-24 V2 Amendment — Storm-Engulfment Trigger Wiring

**Amended verdict:** ACCEPT WITH AMENDMENTS (A3 + A4 + A5 below).

The 2026-07-06 verdict shipped model-side (predicate + trigger events) but
never landed the view (controller / uss / authoring / subscription). The V2
pivot renamed the game-over trigger from `OnRunStranded` to `OnStormEngulfed`
and repurposed `OnRunStranded` as `OnAutoStormBegan` (starts the ticker,
does NOT show the screen). This amendment resolves the six open wiring
questions and closes the slice.

### Q1 — View controller location: `Assets/Scripts/UI/`

**Same folder as `RunCompleteViewController.cs`.** The controller is a pure
Bind/Show/Hide + `OnRetryRequested` UIDocument owner — identical shape to
`RunCompleteViewController`. Keep it in `WastelandRun.UI` asmdef, wired
upward by `RunSceneOverlayHost` in `WastelandRun.CombatView` (the same
one-way arrow that binds `RunCompleteView`). Moving it into
`Assets/Scripts/CombatView/` would drag `WastelandRun.CombatView`
dependencies into a UIDocument controller for no reason — ADR-0014 UI-asmdef
one-way arrow stays intact.

### Q2 — Subscription owner: `RunSceneOverlayHost`

**Keep it on `RunSceneOverlayHost`.** The 2026-07-06 verdict is still right
post-pivot. `RunSceneOverlayHost` already owns the MapView / RunCompleteView
overlay orchestration and its Hide/Show semantics; `HandleStormEngulfed`
belongs in the same file because it must hide MapView (Q3) and hides
RunCompleteView-adjacent state.

`StormMapVisualHost` is the wrong precedent — that sibling exists because
the storm cursor is a **paint-during-play** concern (per-tick lerps against
`MapViewController`) that would pollute `RunSceneOverlayHost`'s beacon /
overlay orchestration. Game-over is a one-shot terminal-view swap, not a
per-tick paint — it belongs with the overlay orchestrator, not with the
map-visual pipe.

Sibling-not-nested per ADR-0016: `_gameOverView` lives as a serialized
sibling field on `RunSceneOverlayHost` alongside `_mapView` and
`_runCompleteView`. Uniform authoring shape.

### Q3 — UI competition: MapView.HideStormFront() before showing game-over

**Clean scrim.** `HandleStormEngulfed` calls `_mapView.HideStormFront()`
before `_mapView.Hide()` + `_gameOverView.Show()`. Rationale: the storm arc
is a lerp element painted at the player's beacon by `StormMapVisualHost`;
leaving it on-screen under a semi-transparent scrim reads as a bug
("something is still animating under the dead screen"), not as doom. The
OUT OF FUEL title + dark scrim is the doom read — the arc becomes visual
noise the moment the trigger fires. A lingering red silhouette *could* work
in a heavily-art-directed version (Slice-F polish), but the current storm
arc is a placeholder element without the framing to survive under a scrim.

`RaiseStormStopped` is NOT invoked here (the storm reached its target — it
didn't cancel). `MapView.HideStormFront` is the correct primitive; it's
what `StormMapVisualHost.HandleStormStopped` already calls. Symmetrical.

### Q4 — RETRY wire target: `RunSceneHost.RestartRun()`

**Confirmed still correct.** Grep verifies three call sites converge on
`RunSceneHost.RestartRun()`: F5 debug (`RunSceneHost.cs:758`),
`CombatOutcomeOverlay` defeat button (via
`RunSceneOverlayHost.HandleRestartRequested` → `_host.RestartRun()`),
`CombatHud._restartHandler`. GameOver retry becomes the fourth caller of
the same primitive. One restart flow, no parallel path.

### Q5 — Timing: defer Show by one frame (coroutine + `yield return null`)

**AMEND A3 — Defer.** `StormEngulfmentController.RaiseStormEngulfed()`
fires from inside the storm coroutine, immediately after
`_mapView.PlayStormAdvance` has been called on the terminal tick.
`PlayStormAdvance` is a Play/Tween-shaped call — the final lerp frame has
not yet painted when `OnStormEngulfed` invokes. Showing the game-over
scrim in the same synchronous step risks stealing the final frame of the
doom animation, breaking the "arc lands → doom" causal read.

Handler shape:
```
private void HandleStormEngulfed()
{
    if (_gameOverView == null || _host == null) return;
    StartCoroutine(ShowGameOverNextFrame());
}
private System.Collections.IEnumerator ShowGameOverNextFrame()
{
    yield return null;                       // let storm arc final frame paint
    if (this == null) yield break;           // teardown race guard
    _mapView?.HideStormFront();
    _mapView?.Hide();
    _gameOverView.Bind(BuildSummary());
    _gameOverView.Show();
    RunOverlayEvents.RaiseOverlayShown();
}
```

Per `feedback_uidocument_negative_exec_order` this coroutine-defer is the
established idiom for "wait one frame past a paint-adjacent event." Auto-
stops on GameObject destroy (Alt+F4 / RestartRun teardown race).

### Q6 — Panel Settings: dedicated `GameOverPanelSettings.asset` — AMEND A4

**AMEND A4 — dedicated PanelSettings, sort order 100.** Grep shows MapView
+ RunCompleteView both share `Assets/UI/PanelSettings.asset`
(`m_SortingOrder: 0`). Adding GameOverView on the same PanelSettings would
tie its paint order to hierarchy-add-order — brittle. `RunHUDPanelSettings`
was drafted 2026-07-06 (untracked) but isn't relevant here (that one is for
run-scoped HUD, not terminal overlay).

**Author a dedicated `Assets/UI/GameOverPanelSettings.asset` with
sortingOrder = 100.** Mirrors `HUD` sibling canvas convention
(`project_combat_scene_architecture`: sortingOrder 10/60/110 for
Combat_HUD / Popups / Debug). Terminal overlay = highest. Cost: one asset
file. Benefit: game-over paints deterministically over anything the map,
storm arc, run-complete, or reward-picker might have left onscreen — even
if a future slice adds a mid-storm popup.

The `AuthorRun` GameOverView mount block loads
`Assets/UI/GameOverPanelSettings.asset`; missing-asset error mirrors the
existing `PanelSettings.asset` error line.

### Amendment A5 — REQUIRED: doc surface cleanup

The 2026-07-06 verdict's "Files touched" list references
`OnRunStranded`. Under V2, the semantics of that event shifted to
`OnAutoStormBegan` (already renamed in `RunSceneHost.cs:294`). Per
ADR-0011 no-vestigial-fossils: this amendment supersedes those lines.
Reader guidance: the pre-amendment section describes the 2026-07-06 shape
which the V2 pivot replaced — treat as historical context, not as active
contract. Active contract = this amendment.

### Self-audit

**Lens 1 — Codebase health**
- ADR-0011 clean: no bridges, no bimodal paths. `OnStormEngulfed` is the
  V2 trigger event; the `OnRunStranded → OnAutoStormBegan` rename already
  landed cleanly (2026-07-24 storm-map-visual-host verdict). Nothing
  vestigial.
- Subscription lifecycle: `RunSceneOverlayHost` uses `OnEnable/OnDisable`
  today for `_host` events. This is CORRECT for Run.prefab-root siblings
  (never SetActive-toggled during a run — only on RestartRun teardown).
  Add `_host.OnStormEngulfed += HandleStormEngulfed` to the same block,
  paired detach in OnDisable. Per `feedback_subscription_lifecycle_pairing`:
  matched pair.
- Teardown races: coroutine auto-stops on GameObject destroy;
  `if (this == null)` guard after `yield return null` per the established
  idiom (`RunSceneOverlayHost.HandleBeaconClicked.onComplete` pattern).
- Single-responsibility: `RunSceneOverlayHost` charter is "run-scene UI
  orchestration" — game-over is in-charter. Not bolt-on.

**Lens 2 — Optimization**
- Event cadence: `OnStormEngulfed` fires exactly once per run (terminal).
  Zero per-frame cost. Coroutine yields exactly one frame, then completes.
- Allocation: `StartCoroutine` + one IEnumerator boxing = one allocation
  per run. Trivial.
- No LINQ, no Label.text churn in hot path (`nodes-cleared` label written
  once in Bind). Clean.

**Lens 3 — 1.0 shape survival**
- `OnStormEngulfed` event surface: `Action` no-payload. Same shape as
  `OnRunComplete`. Survives 1.0 — future consumers (mastery XP grant,
  telemetry, analytics ping) can query `_host.State.RunSeed` +
  `_host.State.NodeMap` directly, same pattern as `RunCompleteView`.
- Coroutine defer: canonical UITK idiom, ships 1.0.
- Placeholder Mastery Progression bar: memory-tracked
  (`project_mastery_xp_wiring_pending`). When mastery lands, wiring is a
  `Bind(summary, masteryProgress)` overload — no UXML rip-out.
- RETURN TO MENU `SetEnabled(false)` fade: mirrors Rest Forge/Upgrade,
  survives 1.0 (main menu wires the click handler + `SetEnabled(true)` in
  its slice; no UXML change).
- Dedicated PanelSettings survives 1.0: sortingOrder = 100 is a stable
  overlay tier for terminal screens (defeat / victory / main-menu-return),
  reusable by future terminal overlays.

### Files touched (V2 amendment — active contract)

- `Assets/UI/GameOverView.uxml` — commit as-is (untracked, drafted 2026-07-06)
- `Assets/UI/GameOverView.uss` — new
- `Assets/UI/GameOverPanelSettings.asset` — new (A4, sortingOrder 100)
- `Assets/Scripts/UI/GameOverViewController.cs` — new, mirror
  `RunCompleteViewController` shape (Bind + Show + Hide +
  `OnRetryRequested` event, `_menuButton.SetEnabled(false)` in OnEnable)
- `Assets/Scripts/CombatView/RunSceneOverlayHost.cs` — add `_gameOverView`
  serialized field, `_host.OnStormEngulfed += HandleStormEngulfed` in
  OnEnable, paired detach in OnDisable, `HandleStormEngulfed` coroutine-
  defer handler (A3), `_gameOverView.OnRetryRequested += HandleRestartRequested`
  reuses existing handler (Q4)
- `Assets/Editor/CombatPrefabAuthor.cs` — new `GameOverView` mount block in
  `AuthorRun` after `RunCompleteView` block, loads
  `GameOverPanelSettings.asset` (A4), wires
  `overlayHost._gameOverView` via SerializedObject
- Run.prefab, RunScene.unity — re-authored consequence (no
  Combat.prefab regen; sentinel unaffected)
- `feedback_component_authoring_same_commit` respected: new
  `GameOverViewController` ships with its `AuthorRun` mount block in the
  same commit.

### Success criteria (V2 amendment)

- Stranded run + storm ticker reaches player beacon → next frame:
  `MapView.HideStormFront` called, `MapView.Hide` called,
  `GameOverView.Show` called.
- RETRY button → `RunSceneHost.RestartRun()` fires; fresh session, all
  overlays hidden, no residual game-over UIDocument.
- RETURN TO MENU button visible-but-disabled (SetEnabled(false)).
- `nodes-cleared` label populated from `state.NodeMap.Beacons` count of
  `IsResolved` beacons (mirror RunCompleteView summary shape).
- `Mastery Progression` bar renders title-only, empty fill (placeholder;
  see memory `project_mastery_xp_wiring_pending`).
- GameOverView paints above map + storm-front element + any residual
  run-complete/reward-picker (dedicated PanelSettings sortingOrder = 100).
- Zero LogError from missing PanelSettings asset (A4 authoring path
  creates/loads the asset).

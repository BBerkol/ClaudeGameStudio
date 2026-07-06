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

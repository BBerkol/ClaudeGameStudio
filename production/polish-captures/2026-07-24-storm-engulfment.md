# Storm Engulfment System — Capture

**Date:** 2026-07-24
**System:** Out-of-fuel game-over V2 — auto-storm advance ticker
**Trigger:** New system ≥50 lines (CLAUDE.md Capture-Before-Destroy §2).

## Summary of the change

Pivot from V1 "stranded → instant game-over" (spec'd in `RunSceneHost.OnRunStranded`
xmldoc but never wired to a subscriber) to V2 "stranded → storm auto-advances →
engulfs player → game-over" per memory `project_out_of_fuel_gameover_v2`.

## Authored values / surfaces at risk (destructive audit)

| Surface | Current value | Destructive? | Plan |
|---|---|---|---|
| `RunSceneHost.OnRunStranded` event name + xmldoc | Named + documented, zero subscribers in the codebase | Rename (not delete) | Rename to `OnAutoStormBegan`; update xmldoc; update both fire sites; update `RunSession.cs:217` xmldoc reference |
| `RunSceneHost` fire-site call at line 569 (post-hydration) | `OnRunStranded?.Invoke()` | Rename call site | `OnAutoStormBegan?.Invoke()` |
| `RunSceneHost` fire-site call at line 661 (post-advance) | `OnRunStranded?.Invoke()` | Rename call site | `OnAutoStormBegan?.Invoke()` |
| `GameOverView.uxml` header comment references "GameOverViewController" | Comment-only reference to non-existent class | Non-destructive | Leave — controller will land as sibling slice |
| `RunSceneOverlayHost` subscriber list | Never subscribed to `OnRunStranded` | Non-destructive | No change this commit — no subscriber to swap |
| `Run.prefab` component list | Root: RunSceneHost + SaveBootstrap + RunSceneOverlayHost + MapView child + RunCompleteView child | Additive | Add sibling `StormEngulfmentController` on root via `CombatPrefabAuthor.AuthorRun` |
| `Assets/Resources/Run/` contents | `Biomes/` subdir only | Additive | Create `StormEngulfmentTuning.asset` (SO instance, `_secondsPerBeacon = 1.5f`) |

No prefab wipes. No scene reauthors. No SO field renames on existing assets. All
edits are additive (new files, new event fields) plus one rename (`OnRunStranded`
→ `OnAutoStormBegan`) with zero external subscribers — literally the safest
rename shape.

## Files created (new)

- `Assets/Scripts/Run/StormAdvanceTick.cs` — `readonly struct` payload
- `Assets/Scripts/Run/StormCursorTicker.cs` — pure POCO walker (test seam)
- `Assets/Scripts/Run/Authoring/StormEngulfmentSO.cs` — feel-knob SO
- `Assets/Scripts/CombatView/StormEngulfmentController.cs` — MonoBehaviour host + coroutine
- `Assets/Resources/Run/StormEngulfmentTuning.asset` — SO instance (author-created)
- `Assets/Tests/EditMode/Run/StormCursorTicker_Test.cs` — POCO ticker test

## Files modified

- `Assets/Scripts/CombatView/RunSceneHost.cs` — event rename + two new sibling events + two `Raise*` methods
- `Assets/Scripts/Run/RunSession.cs` — xmldoc reference update (line 217)
- `Assets/Editor/CombatPrefabAuthor.cs` — add StormEngulfmentController + SO reference in `AuthorRun`

## Technical Director Review

> **Verdict:** APPROVE — Shape "Auto-Advance Ticker on Stranded"

**Verdict summary:** This IS trivial. Do not build a new POCO on `RunState`. The
storm's "which beacon has it reached" is a **presentational cursor**, not
run-state. It only exists during the stranded animation and dies with the
game-over. Put it on the auto-tick controller, not `RunState`.

**1. Data shape.** `FuelState.StormCounter` today is a distance-to-next-strip
counter (0..30), not a beacon index. The new "storm has reached beacon N"
concept has no lifetime beyond the stranded coroutine. Add `int _stormCursorIndex`
as a private field on a new `StormEngulfmentController` MonoBehaviour. Do NOT
add `RunState.StormCursor`. Do NOT touch `FuelStateDto`. Zero save-shape churn.

**2. Tick source.** Dedicated coroutine on new component, triggered by
`OnAutoStormBegan`. Do not piggyback `BeaconTravelTick` — that surface's payload
semantics ("player is traveling") don't match "storm is auto-advancing while
player is parked." Reusing it would be a bimodal-path smell (ADR-0011 #3).

**3. Engulfment condition.** `_stormCursorIndex == _controller.CurrentIndex`.
Beacon-index equality, not lane check. Storm is conceptually a wave across the
whole map; it engulfs when it reaches the player's beacon, regardless of lane.
Lane awareness is presentation-only (Slice F strip rendering) and can layer on
later without changing this predicate.

**4. Determinism / ADR-0003.** No RNG needed. Storm walks the forward-edge
graph in a fixed traversal (first edge each step). If designers later want
branching-lane picks, gate that behind an SO flag and salt with
`RunSeed ^ 0x53544D` ('STM') per ADR-0003.

**5. ADR-0011.** Clean, no bridges.
- No new enum values, no `RunStatus.Stranded`.
- `OnRunStranded` → `OnAutoStormBegan` is a subscriber swap of zero because no
  subscribers exist today. Single canonical cut per project meta-rule.
- Add `OnStormEngulfed` sibling event — actual game-over trigger.
- Add `OnStormAdvanced(StormAdvanceTick)` sibling event — Slice F strip animator hook.
- **Generator SO surface freeze is not implicated** — no `BiomeDistributionSO` /
  `BiomeGenerationInputs` / `BiomeWebGenerator` touched.

**6. Three-Lens Self-Audit.**

*Lens 1 — Codebase Health:* Confirmed clean. Field is presentational, coroutine
owns it (single-responsibility). Subscription lifecycle: OnEnable/OnDisable on
the controller (RequireComponent(RunSceneHost) sibling; both live+die with
Run.prefab). Teardown race: coroutine auto-stops on GameObject destroy;
fire sites guard `if (this == null || _host == null) yield break`.

*Lens 2 — Optimization:* Coroutine tick at 1.5s cadence (not per-frame). Zero
allocation in tick body — increment an int, compare, invoke event. Cache one
`WaitForSeconds` in a field.

*Lens 3 — 1.0 Shape Survival:* `OnStormEngulfed` event survives to 1.0 as the
canonical trigger. `_stormCursorIndex` field survives — Slice F storm strip
renderer will subscribe to `OnStormAdvanced(StormAdvanceTick)` to lerp between
`FromIndex`/`ToIndex`. Add `StormAdvanceTick` struct now so Slice F ships
without signature churn. `_secondsPerBeacon` tunable on `StormEngulfmentSO`
from day one (data-flag lagging-dep pattern).

## Recommended commit shape (adapted from TD)

TD's "subscriber swap" step is omitted since no subscriber exists to swap. The
overlay wiring lands in a later slice (build `GameOverViewController` +
subscribe to `OnStormEngulfed`).

Single commit, ADR-0011 clean:
1. Rename `OnRunStranded` → `OnAutoStormBegan` on `RunSceneHost` (both fire sites, xmldoc, RunSession.cs xmldoc ref).
2. Add `OnStormEngulfed` (Action) + `OnStormAdvanced(StormAdvanceTick)` events on `RunSceneHost`; add public `RaiseStormEngulfed()` + `RaiseStormAdvanced(StormAdvanceTick)` for the controller to invoke.
3. New `StormEngulfmentController` MonoBehaviour on RunScene root (auto-authored via `CombatPrefabAuthor.AuthorRun`).
4. New `StormEngulfmentSO` at `Resources/Run/StormEngulfmentTuning.asset` (`_secondsPerBeacon = 1.5f`).
5. New `readonly struct StormAdvanceTick` in `WastelandRun.Run` namespace.
6. New `StormCursorTicker` POCO for testable tick logic (POCO seam over MonoBehaviour test hook).
7. EditMode test: `StormCursorTicker_Test` — assert engulfment fires when cursor == target index; assert forward-edge walk is deterministic (edge-0).

## Open decisions (punted to user)

- Tick rate default (1.5s is a guess — playtest queued as task #9).
- Click behavior during doom — MVP: existing affordability-array returns false → BeaconNodeElement styling handles it as unaffordable. No new gate.
- Camera / SFX — MVP: none.
- Game-over UI wiring (`GameOverViewController` on `OnStormEngulfed`) — separate slice.

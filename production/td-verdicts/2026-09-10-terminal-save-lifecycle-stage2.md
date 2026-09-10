# TD Verdict — Terminal Save Lifecycle, Stage 2 (model + host)

**Date:** 2026-09-10
**Slice:** clear-on-terminal run lifecycle, Stage 2
**Companion capture:** `production/polish-captures/2026-09-09-terminal-save-lifecycle.md`
**Stage 1:** landed Unity `80b05ac` — EditMode 1272/1271/0/1, mutation-tested

## Why this file exists

The consultation for this slice happened on **2026-09-09** and is recorded in
full in the companion capture. The session ran past midnight, so the
`td-review-required` hook — which keys on *today's* date — no longer sees it.
This file re-states the verdict under today's date rather than back-dating the
original. **No new consultation is claimed here; nothing below is invented.**
Every ruling is transcribed from the 2026-09-09 technical-director APPROVE and
the `unity-specialist` adversarial pass, both quoted at length in the capture.

## Files this verdict covers

Stage 2 touches, and the TD ruled on, each of these:

- `Assets/Scripts/Run/RunStatus.cs` — add `Engulfed`; rewrite the stale xmldoc
- **`Assets/Scripts/Run/RunController.cs`** — add `internal bool TryLatchEngulfed()`
- `Assets/Scripts/Run/RunSession.cs` — `OnRunTerminated` event, 5 fire sites,
  `ResolveEvent` invariant, stranded-storm guard
- `Assets/Scripts/Run/NodeMap.cs` — `IsTerminalCleared`
- `Assets/Scripts/CombatView/LoadedRunSnapshot.cs` — `ResumeMap`, `CanResume`
- `Assets/Scripts/CombatView/RunSceneHost.cs` — terminal handler, both
  subscribe sites, `AdvanceToNextBeacon` branch, `NotifyEventResolved` guard,
  `Initialize` gate

## ADRs at risk of drift, and the ruling on each

- **ADR-0011 (no bridges at done).** Payload is `Action<RunStatus>`, not a new
  `RunOutcome` enum — a second terminal vocabulary beside `RunStatus` would be
  forbidden pattern #8 (duplicate enums). `BeaconOutcome.RunTerminated` stays
  unconsumed rather than becoming a second source of truth beside `Status`
  (#2, parallel storage). No stub `AwardMasteryXp()` (#6) and no
  `// TODO: award XP` (#7).
- **ADR-0004 (save architecture).** `RunStatus` stays unpersisted. The load-path
  guard derives from the map (`NodeMap.IsTerminalCleared`) instead, which is why
  it is not parallel storage. MasteryState is untouched by the clear.
- **ADR-0002 (engine-free model).** The model cannot call `SaveSystem`; the
  terminal crosses to the host via an event, matching the five existing
  `On*ModelCommitted` bridges.
- **2026-07-06 TD Amendment A2** — *"storm engulfment is a presentation event,
  not a terminal status"* (`RunSession.cs:374-376`). **This slice reverses it.**
  Under clear-on-terminal, engulfment *is* terminal. Called out explicitly so
  it does not drift silently.

## The final-game picture this serves

A run ends when it ends. Death is permanent, victory is final, and neither can
be undone by quitting at the right moment. `OnRunTerminated(RunStatus)` is the
seam mastery XP, run-history telemetry, achievements and the victory stats
panel all attach to — the seam ships in this slice, zero of its subscribers do.

## TD Verdict

**ACCEPT** — verdict of record: `TD-ARCHITECTURE: APPROVE`, second pass,
2026-09-09. *"Implementation may proceed."* Stage 1 has since landed green.

Rulings carried forward, verbatim in substance:

1. **`TryLatchEngulfed` on `RunController`, `internal`, returns `false` when
   already terminal.** Engulfment is the one terminal with no model-side
   once-only guard — the only thing preventing repeat fires today is
   `StormAdvanceVisualPacer._engulfed` at `:253`, a **view-side** latch. A view
   holding the sole guard is the pattern the storm-cursor pivot removed, and a
   duplicate fire becomes a duplicate XP award the day that subscriber lands.
   `internal` for the same reason as `StartRun` / `MarkCombatBeaconCleared`: a
   scene script must not be able to call it.
2. **`RunStatus.Engulfed` is a TD call and needs no game-designer sign-off.**
   Containment verified: `RunStatus` has zero consumers outside
   `RunController`/`RunState`, and no `switch` over it exists anywhere, so no
   exhaustiveness assumption breaks. It *preserves* the open design question
   rather than answering it.
3. **Tracked deferral.** Owner `game-designer`; blocks the mastery-XP slice, not
   this one: *does a storm-engulf loss award mastery XP identically to a combat
   defeat?*
4. **Five fire sites, one event, one host handler** — including
   `RunSession.cs:596` (`AdvanceStormFromEvent`), which the original scoping
   missed.
5. **The H6 guard belongs at `RunSceneHost.NotifyEventResolved`**, not at
   `EventHandler.Resolve` or `ctx.Resolved`. Verified: `EventModalHost.
   HandleBeaconOutcome` (`:243-256`) clears `_resolutionInFlight`, stamps
   `_lastResolvedBeacon` and hides the dialogue *before* calling the host, so an
   earlier interception strands the modal open under the game-over overlay.
   View teardown must complete; model resolution must not.
6. **Subscribe at BOTH `RunSceneHost.cs:724` and `:871`.** Subscribing only in
   `BeginNewRun` means every *resumed* run fails to clear — named as the
   signature bug of this slice.
7. **`CanResume` composes `HasCompleteSessionCore`**, never re-derives it, and
   `IsTerminalCleared` is `IsRunComplete && Current.IsResolved` — the conjunct
   exists because `IsRunComplete` is POSITIONAL (true on arrival at the boss,
   pre-fight). A crash mid-boss-fight must still resume.
8. **Step 5 is seam only.** No XP code; the once-per-run guarantee is what makes
   duplicate awards structurally impossible.

**Merge gate (not waived):** EditMode green with counts, Editor closed, plus
`grep -cE 'error CS' TestResults/editmode.log`. Baseline after Stage 1 is
**1272 / 1271 / 0 / 1**. Test 8 must be proven to red with the guard removed.

## Three-lens self-audit

Carried from the 2026-09-09 verdict, which included it. **Health:** no bridge,
parallel storage, compat overload or vestigial surface added; the third
`(live, tmp, bak)` composition was extracted rather than added to; a symmetric
`ClearMasteryState` and a blanket five-verb `RequireRunOngoing` were both
refused for having zero and one real callers. **Optimization:** every new signal
is once-per-run, nothing per-frame; ~11 wasted `ToDto()` projections on the boss
path are an accepted, named trade; the retry-stall was mitigated in-slice rather
than deferred. **1.0 survival:** `OnRunTerminated(RunStatus)` extends by adding
enum values rather than changing a signature; `IsTerminalCleared` is the general
"run is over" predicate the codebase was missing; nothing here is scaffolding
the next slice rips out.

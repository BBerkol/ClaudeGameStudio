# TD Verdict — Phase 3 Save Integrity (Defects B + A)

**Date:** 2026-09-09
**Slice:** `production/remediation-plan-2026-09-08.md` §3
**Files at issue:** `Assets/Scripts/Save/SaveProjectionException.cs` (new),
`Assets/Scripts/Save/SaveSystem.Write.cs`,
`Assets/Scripts/CombatView/RunSceneHost.cs`,
`Assets/Scripts/CombatView/BeaconPresentation.cs` (new, deferred),
`Assets/Scripts/UI/RunCompleteViewController.cs` (deferred)
**Baseline:** EditMode 1263 / 1262 / 0 failed / 1 skipped

## TD Verdict

**AMEND.** Three blocking amendments, five corrections. The slice is correctly
scoped and both defects are real, but §3.2 as briefed **ships a new bug worse
than the one it fixes**, and its central premise was unverified.

## A1 (BLOCKING) — `IsRunComplete` is positional. The briefed fix ships a fake victory.

**Verified independently before accepting:**

```csharp
// NodeMap.cs:101
public bool IsRunComplete => _beacons[CurrentIndex].Type == TerminalType;
```

True the instant the cursor **lands on** the terminal beacon — *before the boss
fight*. The codebase already knows this: `RunSceneHost.AdvanceToNextBeacon`
`:969-983` branches on it and for a combat-shaped terminal fires only
`OnBeaconChanged`, with the comment *"Boss terminal — combat fires first,
OnRunComplete deferred to NotifyRewardClaimed (post-victory picker)."*

**Therefore a snapshot with `IsRunComplete == true` and the boss un-fought is a
routine, expected save state.** The briefed
`if (_controller.IsRunComplete) OnRunComplete?.Invoke();` would show the
**victory screen for a fight the player never had**, and — once the
run-stats/XP panel lands per the 2026-09-09 user decision — award XP for it.

The same term poisons `IsMapCurrent`: on that resume the map hides *and*
`BeaconActivator.ClearAll()` fires, producing a black screen with no boss and
no map.

**Correct predicate is terminal-AND-resolved**, which is exactly equivalent to
`RunStatus.Victory` because the latch rides atomically with `MarkResolved` on
both terminal shapes — verified at `RunController.cs:395-403` (combat terminal;
its xmldoc forbids splitting them) and `:716-720` (non-combat terminal).
`is_resolved` **is** persisted (`NodeMapDto.cs:249-250`), so this reconstructs
across resume **without** persisting `RunStatus` — which is *why* "do not
persist RunStatus" is right: the value is genuinely derivable, so storing it
would be ADR-0011 #2 parallel storage.

Amendment: add `IsRunOver => NodeMap.IsRunComplete && NodeMap.Current.IsResolved`
on `RunController`, surfaced on `RunSceneHost`. **Do not delete or repurpose
`IsRunComplete`** — `AdvanceToNextBeacon:962` and `NotifyRewardClaimed:1073`
legitimately want the positional meaning. Two names for two different questions
is not a bridge; one name for both is the bug. Name the parameter `isRunOver`
so the fake-victory bug becomes unwriteable at future call sites.

## A2 (BLOCKING) — the fan-out may fire into an empty delegate. Verify first.

`SaveBootstrap` is `[DefaultExecutionOrder(-100)]` (`:55`); `Awake` → `LoadAndInitialize`
→ `_host.Initialize` → `BeginRunFromLoaded` → the fan-out at `:876-878`.
`RunSceneOverlayHost` subscribes `OnRunComplete` in **`OnEnable`** (`:66`) and is
its **only** subscriber. `SaveBootstrap.cs:210-217` already documents this exact
race for a sibling and works around it by calling
`activator.LoadCurrentBeaconAsync()` explicitly at `:223`; three more components
carry cover-live-session guards for the same reason
(`StormAdvanceVisualPacer.cs:137`, `StormMapVisualHost.cs:91`,
`MapViewController.cs:1078`).

If `OnEnable` has not run by the Awake-time fire, the added invoke reaches zero
subscribers, the victory panel never paints, and merge gate #5 fails while
pointing at the panel-clone race — the wrong diagnosis. **Probe before writing
§3.2.** If the race is real, use the established cover-live-session idiom in
`RunSceneOverlayHost.OnEnable` instead of the fan-out. Ship one or the other,
never both (ADR-0011 #3).

## A3 (BLOCKING) — there are FOUR copies of the predicate, not three.

**Verified.** The fourth is `StormAdvanceVisualPacer.cs:269-276`, verbatim.
`RunSceneHost.cs:1048-1052` names it as a consumer, so the briefed three-item
list and that comment disagree; the union is four. Extracting three would leave
the fourth diverged and the `:1048` comment false (ADR-0011 #7). Route all four
through `BeaconPresentation.IsMapCurrent` and update that comment to point at
the helper rather than re-listing consumers.

## A4 — fold in the disk half of silent save loss.

**Verified:** `RunConsumer` (`SaveSystem.Write.cs:302-314`) calls
`ProcessPending()` at `:312` with **no try/catch**. `WriteWithRetry` catches
`IOException` only while `attempt < RetryDelaysMs.Length`. So a post-budget
`IOException`, a `JsonSerializationException`, or an `UnauthorizedAccessException`
escapes → the `while` exits → the consumer Task faults unobserved → **no further
save for the rest of the session, silently**. There is no quit-time flush
(`RunSceneHost.cs:277` documents that as deliberate), so the consumer is the only
path to disk.

Same defect class as B, same funnel, and it is the half that actually fires on
player machines. ~6 lines. Keep the `OperationCanceledException` catch separate
and outside, or `ResetForTests` teardown starts logging spurious failures.

## A5 — the Haven-hole justification is false.

The brief defends the `BeaconActivator` change as closing a "Haven-style-terminal
hole where `IsResolved` is false." No such hole exists:
`RunController.CommitNextBeacon:716-719` calls `arrived.MarkResolved()` for
non-combat terminals on arrival. The change is still correct as pure
de-duplication, but must not ship defended by a nonexistent premise.

The brief's rejection of the `SaveBootstrap.cs:222` call-site guard is correct
and its reasoning should be kept.

## A6 — name the residual.

On a resumed completed run, `RunState.Status` is `Ongoing` while the run is
factually `Victory`. Dormant today; not dormant once Defect C ships (`Status` is
where `Defeat` lives) or XP lands. Do not fix by persisting. Document at the
fan-out site until C decides the shape.

## A7 / A8

**A7.** Wrap `SnapshotMasteryRegistry` (`:291-300`) identically with
`SaveCategory.MasteryState` — otherwise the *blocking-policy* category is the one
without the telemetry the struct exists to serve. Duplicate the try/catch; do
**not** unify the two interfaces (ADR-0004 Decision 2 declares them disjoint and
CI-enforces it).

**A8.** When porting `GameOverViewController.TryCacheElements` (`:94-111`), do
not port its latent bug — the `if (_nodesClearedLabel != null) return;` guard
means a `SetActive` re-clone orphans cached handles and the guard blocks
re-query (memory `feedback_uidocument_setactive_reclone`). Null the caches in
`OnDisable` in the ported version and back-port that line in the same commit.

## Answers to the five questions

**Q2 — `BeaconPresentation` location:** `Assets/Scripts/CombatView/`. All four
consumers live there, zero new asmdef edges. Explicitly **not**
`Assets/Scripts/Run/` — "is the map the current presentation surface" is a
view-policy question the engine-free model has no concept of, and `isRunOver` is
a view-supplied term.

**Q3 — per-entry wrap:** **Yes, per-entry inside `SnapshotRunRegistry`** — the
plan's literal wording is the wrong one. Wrapping the whole call loses
`SystemId`, and with **11 adapters and 16 identical-shaped `InvalidOperationException`
throw sites** across `Assets/Scripts/Save/Adapters/`, "a projection failed"
without the key is nearly as useless as the current swallow.

**Q5 — `SaveWriteFailed` on `SaveSystem`:** yes, static event matching the
existing telemetry, cleared in `ResetForTests`. **`SaveSystem` must be the sole
raise site** — `RunSceneHost` catches only to keep the run alive and to log.
Note `WastelandRun.Save.asmdef` is `noEngineReferences: true`, so
`SaveProjectionException.cs` must be pure BCL — no `UnityEngine` using.
Consider a `SaveFailureStage` field (`Projection`/`Serialize`/`Disk`) now, since
A4 makes the third case real immediately.

## Q1 — scope. The recommendation.

**Ship B alone in Phase 3; re-scope A + C as one terminal-lifecycle slice.**

§3.2 and §3.3 build **contradictory 1.0 shapes**, and the plan already declares
the winner at its own line 376: *"Clear-on-terminal is the 1.0 shape for both."*

- **Shape 1 — completed runs are resumable.** The run ends when the player
  *dismisses* the victory screen. §3.2 as briefed is canonical 1.0 code.
- **Shape 2 — clear-on-terminal.** The run ends at boss death and the save is
  invalidated. `BeginRunFromLoaded` can then never see a completed run, and the
  fan-out branch, the `isRunOver` predicate term, the `BeaconActivator` limb and
  the `RunCompleteViewController` boot-race hardening are **all dead code the day
  C lands.** The residual crash window is handled at the load path, which is
  ADR-0004 Decision 4's existing policy at the single decision point the ADR
  names (`adr-0004:293`).

The 2026-09-09 user decision — fullscreen image + run-stats + **XP panel** —
leans hard toward Shape 2: that is a *claim-once* screen. If XP is on it,
resuming onto it is an award vector; if XP is awarded at boss death, there is
nothing to resume to.

**This is the user's call — it is a design question about when a run ends, not a
technical one.**

## Merge-gate deltas

1. `Resume_OnUnfoughtBossBeacon_DoesNotFireRunComplete` — the A1 regression lock.
   Plant an envelope with cursor on terminal, `is_resolved: false`. **This is the
   test that fails on the brief as written.**
2. Merge gate #6's revert-proof must also revert to the `IsRunComplete` form and
   show that test going red.
3. `BeaconPresentation` gets a direct unit test over the four-way truth table.
4. Fix the plan's stale `1244` baseline at `:396` → **1263 / 1262 / 0 / 1**.

## Pre-flight finding (verified after the verdict)

TD flagged `ChopshopRepairMode_Test` as thin on binding. Checking it found
**worse**: two **PlayMode** tests instantiate `RunSceneHost` with **zero**
binding —

| File | `AddComponent<RunSceneHost>` | bind |
|---|---|---|
| `ChopshopBeaconRevisit_Test.cs` | 1 | **0** |
| `ChopshopRepairMode_Test.cs` | 1 | **0** |

Both call `SaveSystem.ResetForTests()` and never `Bind`, so both rely on the
swallow and **both turn red when the catch is deleted**. Phase 2.1 only covered
EditMode. These must be bound before B lands.

## Disposition

- **B + A4 + A7 proceed now**, with the per-entry wrap, the `SaveFailureStage`
  field, `SaveSystem` as sole raise site, and the two PlayMode harnesses bound
  first.
- **A (§3.2) is HELD** pending the user's Shape 1 / Shape 2 decision. A1, A2 and
  A3 are recorded so the work is correct whenever it proceeds.
- **C (§3.3) unchanged** — still untraced, now explicitly entangled with A.

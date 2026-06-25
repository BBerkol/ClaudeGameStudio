# TD Verdict — Slice 8b-3 RunSceneHost Bootstrap + Trigger Wiring

**Date:** 2026-06-25
**Gate:** TD-ARCHITECTURE: **CONCERNS → APPROVE** (precondition cleared 2026-06-25)
**Scope:** Final wiring slice of the ADR-0004 Save & Persistence orchestrator. Slice 8b-1 (write path) and 8b-2 (load + recovery chain) shipped all-green (569 EditMode tests). 8b-3 wires the Unity-side bootstrap that resolves `Application.persistentDataPath` / `Application.temporaryCachePath`, starts the background consumer, registers run-scoped DTOs, and triggers `EnqueueRunStateWrite` at semantic boundaries.

## Five structural questions

**Q1 — Bootstrap site & timing.** Save bind + StartBackgroundConsumer fires from `RunSceneHost.Awake()` vs a separate sibling `SaveBootstrap` MonoBehaviour?

**Q2 — Trigger predicate surface.** Which call sites fire `SaveSystem.EnqueueRunStateWrite()`? Candidates:
- (a) `RunSession.ExitCombat`
- (b) `RunSceneHost.AdvanceToNextBeacon`
- (c) `RunSceneHost.NotifyRewardClaimed`
- (d) `RunSceneHost.BeginNewRun`

**Q3 — Load → resume integration.** How does `LoadResult.Loaded` route back into `RunController.StartRun(player, seed, map)` when only `NodeMapDto` is on the wire and `RunSeed` is not yet DTO'd?

**Q4 — MasteryState exhaustion dialog at this slice.** Wire the dialog now (defensive, no DTOs yet) vs defer with the FIRST MasteryState DTO slice?

**Q5 — Write trigger granularity vs queue coalescing.** Spray-and-trust-coalescing vs semantic-boundary-only?

## Verdicts

**Q1 — Separate `SaveBootstrap` MonoBehaviour, sibling of RunSceneHost on the same scene root, with `[DefaultExecutionOrder(-100)]`. It owns Bind + StartBackgroundConsumer + Register* + LoadRunState, then hands the LoadResult to RunSceneHost via a public `Initialize(LoadResult)` call from its own `Start()`.**

- Why: Composition smell-test fails for putting Save on RunSceneHost. Save is *process-lifetime* (Bind is single-shot, survives RestartRun); RunSceneHost is *run-lifetime* (RestartRun re-fires BeginNewRun). Folding them couples two different cadences and ADR-0011 #1 (adapter layer) starts looking inevitable the moment RestartRun must NOT rebind storage.
- Rejected: RunSceneHost.Awake doing both — fails the cadence test and forces an `_alreadyBound` guard which is exactly the kind of stateful flag ADR-0011 #5 calls out.

**Q2 — Canonical fire site is (b) `AdvanceToNextBeacon`, plus (d) `BeginNewRun` for the initial save. NOT ExitCombat, NOT NotifyRewardClaimed.**

- Why: ADR-0004 GDD R6 says "last beacon-resolve boundary" — the boundary is the *edge consumption*, not the reward claim. A crash between victory and departure correctly resumes the player at the just-resolved beacon. Verified in code: `RunController.ResolveCombat` calls `current.MarkResolved()` BEFORE `AdvanceToNextBeacon`/`CommitNextBeacon` fires; `CommitNextBeacon` even *guards* on `current.IsResolved` (RunController.cs:218). So Advance sees the fully-mutated NodeMap including the just-flipped IsResolved bit. Initial save on BeginNewRun guarantees a 1-step-in crash resumes the right seed/map rather than re-rolling.
- Rejected: Spraying all four sites — see Q5; coalescing is not free and three of the four collapse to the same logical boundary anyway.

**Q3 — Lock 8b-3 as NodeMap-only persistence; resume is NOT shippable this slice. Bootstrap treats `LoadResult.Loaded` as "log it, call BeginNewRun fresh anyway, surface a one-line warning." A `ResumeRun(LoadResult)` method does NOT exist yet.**

- Why: Re-deriving RunSeed from `MapSeed ^ 0x4D41` is an ADR-0011 #1 violation dressed as math — it's a covert bridge that says "the seed lives in two places and we're inferring one from the other." The clean path is: RunSeed DTO lands in the next slice, *then* `ResumeRun(LoadResult)` lands alongside it as a single canonical method. Shipping resume now means shipping it twice.
- Rejected: Extending BeginNewRun with a bimodal "loaded vs fresh" branch — ADR-0011 #3 (bimodal paths) and the eventual RunSeed slice would have to undo it.

**Q4 — Defer. No dialog code in 8b-3.**

- Why: `LoadMasteryState()` returns Empty by construction at this slice — wiring a dialog branch whose trigger predicate is provably false is ADR-0011 #6 (stub return / dead branch). The coupling cost (first MasteryState DTO slice reopens RunSceneHost AND adds dialog UI) is the *correct* coupling — those things genuinely belong together.
- Rejected: Defensive wire-up now — looks like hygiene, reads as dead code to the next person who opens the scene.

**Q5 — Coalescing is real but not a license. Treat enqueue calls as semantically meaningful boundary markers, not "free."**

- Why: ADR-0004's coalescing window is "at-most-one-pending per category" — on a desktop machine with a drained consumer, two enqueue calls 50ms apart absolutely produce two disk writes (the first is mid-flush when the second arrives). Liberal-spray would work on a debug build with a slow disk and bite on a fast SSD. Anchoring enqueue to the semantic boundary (AdvanceToNextBeacon = "the cursor moved, this is now the truth") makes the policy self-documenting.
- Rejected: Spray-and-trust-coalescing — race against drain timing, plus it makes the code lie about what a "save point" means.

## Precondition (cleared 2026-06-25)

TD originally gated this verdict CONCERNS pending confirmation that `NodeMapDto` round-trips `IsResolved` and that `IsResolved` is mutated in the resolve path, not the advance path.

**Cleared:**
- `NodeDto.IsResolved` is on the wire format (`Assets/Scripts/Save/Dtos/NodeMapDto.cs:243`).
- `NodeMapDto.From(NodeMap)` copies `b.IsResolved` for every beacon.
- `NodeMapDto.ToNodeMap()` reconstructs `BeaconData(isResolved: n.IsResolved)`.
- `BeaconData.MarkResolved()` is called by `RunController.ResolveCombat` on combat victory (`Assets/Scripts/Run/RunController.cs:115`) — strictly *before* any Advance path.
- `RunController.CommitNextBeacon` *guards* on `current.IsResolved` being true (`Assets/Scripts/Run/RunController.cs:218`), so the model itself enforces the resolve-before-advance ordering.

Verdict flips to **APPROVE**.

## Bottom-line implementation shape

1. `Assets/Scripts/Run/Save/SaveBootstrap.cs` (NEW, MonoBehaviour, `[DefaultExecutionOrder(-100)]`) — Bind, StartBackgroundConsumer, Register NodeMap serializable adapter, LoadRunState, hand LoadResult to RunSceneHost via `Initialize(LoadResult)`.
2. `Assets/Scripts/Run/Save/NodeMapSerializable.cs` (NEW) — `IRunStateSerializable` adapter that reads/writes the live NodeMap via the existing `NodeMapDto.From` / `ToNodeMap`. Registered by SaveBootstrap, not by NodeMap itself (composition smell-test).
3. `Assets/Scripts/CombatView/RunSceneHost.cs` — add `Initialize(LoadResult)` method (logs + calls BeginNewRun); add `SaveSystem.EnqueueRunStateWrite()` call at end of `BeginNewRun` and end of `AdvanceToNextBeacon`. No bimodal branches.
4. EditMode tests (`Assets/Tests/EditMode/Run/Save/`):
   - `SaveBootstrap_BindsAndLoads_BeforeRunSceneHostInitialize`
   - `RunSceneHost_BeginNewRun_EnqueuesWrite`
   - `RunSceneHost_AdvanceToNextBeacon_EnqueuesWrite`
   - `NodeMapSerializable_RoundTrip_PreservesIsResolved` (precondition test)
5. Capture file `production/polish-captures/2026-06-25-slice-8b-3-bootstrap-wiring.md` — RunSceneHost has authored serialised fields; adding `Initialize` and two enqueue sites is non-destructive but capture documents the wire-up surface for the next slice.

No new `ResumeRun` method. No dialog UXML. No MasteryState wiring. Slice 8b-3 ships the **wire**, the **initial-save**, and the **advance-save** — resume lands when RunSeed lands.

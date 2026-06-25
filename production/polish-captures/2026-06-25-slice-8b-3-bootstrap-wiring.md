# Capture — Slice 8b-3 Save Orchestrator Bootstrap + Trigger Wiring

**Date:** 2026-06-25
**Scope:** Wire the ADR-0004 Save & Persistence orchestrator into the Unity runtime: bootstrap (`Bind` + `StartBackgroundConsumer` + `Register` + `LoadRunState`), `NodeMap` `IRunStateSerializable` adapter, and `EnqueueRunStateWrite` triggers at semantic boundaries. Third sub-slice of the Slice 8b chain (8b-1 write [DONE 555/0/1] / 8b-2 load [DONE 569/0/1] / **8b-3 triggers [this slice]**).
**Companion docs:** `production/td-verdicts/2026-06-25-slice-8b-3-bootstrap-brief.md` (full TD review, APPROVE verdict after precondition cleared).

## Final-game picture this slice serves

This slice closes the loop on the ADR-0004 crash-and-resume promise: the write path (8b-1) snapshots run state on enqueue; the load path (8b-2) walks the recovery chain and returns a structured `LoadResult`; this slice **wires Unity to both**. After this slice, a player parking on a beacon, then quit-to-desktop, then relaunch, finds a `.sav` file on disk with the correct `NodeMap` (current beacon, resolved flags, path history). Resume itself does NOT ship in this slice — that lands when `RunSeed` becomes a DTO so the load path can rehydrate the full `RunState` shape rather than only the `NodeMap`. For 8b-3, `LoadResult.Loaded` is logged as a one-line warning and the host falls through to `BeginNewRun` fresh.

## What is being added (new code)

**Files added (new):**
- `Assets/Scripts/Run/Save/SaveBootstrap.cs` — `MonoBehaviour` with `[DefaultExecutionOrder(-100)]`. Resolves `Application.persistentDataPath` + `Application.temporaryCachePath` in `Awake`, constructs `DiskSaveStorage`, calls `SaveSystem.Bind`, `StartBackgroundConsumer`, registers a `NodeMapSerializable` against the live `RunSceneHost`, calls `SaveSystem.LoadRunState()`, hands the `LoadResult` to `RunSceneHost.Initialize(LoadResult)` from its own `Start()`.
- `Assets/Scripts/Run/Save/NodeMapSerializable.cs` — `IRunStateSerializable` adapter. Reads from a `RunController` (the host's controller reference) for `ToDto()` via `NodeMapDto.From(controller.NodeMap)`. `FromDto(NodeMapDto)` would route into a future resume path but in this slice is implemented as a guarded throw because resume is deferred (see Q3 in the TD brief). `SystemId` = `NodeMapDto.SYSTEM_ID`, `SchemaVersion` = `NodeMapDto.SCHEMA_VERSION` (registry consts are single source of truth).
- `Assets/Tests/EditMode/Run/Save/SaveBootstrap_test.cs` — bootstrap order tests (Bind before Register before Load before Initialize).
- `Assets/Tests/EditMode/Run/Save/RunSceneHost_EnqueuesWrite_test.cs` — `BeginNewRun` and `AdvanceToNextBeacon` enqueue tests; verified via `DrainPendingForTests` + `InMemorySaveStorage` filesystem inspection.
- `Assets/Tests/EditMode/Run/Save/NodeMapSerializable_RoundTrip_test.cs` — precondition test from the TD verdict: round-trip preserves `IsResolved` on every beacon.

**Files modified:**
- `Assets/Scripts/CombatView/RunSceneHost.cs`
  - Add public method `Initialize(LoadResult result)` — bootstrap entry point. Logs `LoadResult.Outcome` + `Rung`; calls `BeginNewRun(null)` regardless (resume deferred — TD Q3).
  - Add `SaveSystem.EnqueueRunStateWrite()` call at end of `BeginNewRun` (initial-save site — TD Q2 part d).
  - Add `SaveSystem.EnqueueRunStateWrite()` call at end of `AdvanceToNextBeacon`, after the `OnBeaconChanged` / `OnRunComplete` event fire (advance-save site — TD Q2 part b).
- `production/td-verdicts/` — new file `2026-06-25-slice-8b-3-bootstrap-brief.md` (TD verdict above).

Code summary by component:

- **`SaveBootstrap` MonoBehaviour** — sibling of `RunSceneHost` (NOT a child component on the same GameObject). `[DefaultExecutionOrder(-100)]` so `Awake` and `Start` both fire before any other scene script. `Awake` calls `SaveSystem.Bind(new DiskSaveStorage(Application.persistentDataPath, Application.temporaryCachePath))` and `SaveSystem.StartBackgroundConsumer()`. `Start` resolves the `RunSceneHost` reference via `GetComponentInChildren` or `[SerializeField]` (TBD per the user's preference for explicit serialised wiring vs scene-graph walks; defaulting to serialised reference per `feedback_designer_friendly_default`), registers the `NodeMapSerializable` adapter against the host's controller, calls `SaveSystem.LoadRunState()`, and calls `host.Initialize(result)`.
- **`NodeMapSerializable` adapter** — holds a `RunController` reference. `ToDto()` returns `NodeMapDto.From(_controller.State.NodeMap)`. `FromDto(object)` for this slice throws `NotSupportedException("Resume not shippable until RunSeed DTO lands — see TD verdict 2026-06-25 Q3.")` — explicit per TD's no-bimodal-branches rule; the FromDto path lands alongside `ResumeRun` in the next slice.
- **`RunSceneHost.Initialize(LoadResult)`** — public entry called by `SaveBootstrap.Start()`. Body: log one line via `Debug.Log` (e.g. `[Save] LoadResult: Outcome={result.Outcome} Rung={result.Rung}`), then call `BeginNewRun(null)`. No bimodal branch on Outcome — resume is deferred.
- **`RunSceneHost.BeginNewRun` enqueue site** — single call to `SaveSystem.EnqueueRunStateWrite()` immediately after `OnBeaconChanged?.Invoke()` returns. Wraps in a `try/catch (InvalidOperationException)` for the unbound case (EditMode test harnesses that don't bind Save), with a `Debug.LogWarning` fall-through. This keeps the test surface from coupling every RunSceneHost test to SaveSystem state.
- **`RunSceneHost.AdvanceToNextBeacon` enqueue site** — single call to `SaveSystem.EnqueueRunStateWrite()` at the end of the method, after both `OnRunComplete?.Invoke()` and `OnBeaconChanged?.Invoke()` early-returns. Same try/catch shape as BeginNewRun.

## What is being destroyed (authored values + locked fixtures)

**Nothing authored is destroyed.** Slice 8b-3 is additive at every surface:

- `RunSceneHost.cs` gains one new public method (`Initialize`) and two new lines (the two `EnqueueRunStateWrite` calls). All five existing serialised fields (`_playerVehicleAsset`, `_biomeDistribution`, `_combatBeaconArchetypes`, `_runSeed`, `_encounterSelection`) are untouched. All seven existing public methods (`BeginNewRun`, `BeginCombatForCurrentBeacon`, `AdvanceToNextBeacon`, `RestartRun`, `NotifyRewardClaimed`, `EndCombat`, `ResolveBinder`) keep their signatures and return shapes. All four existing events (`OnBeaconChanged`, `OnCombatReady`, `OnRunComplete`, `OnRewardClaimed`) keep their order and firing sites.
- The `Run.prefab` scene authoring (baked via `CombatPrefabAuthor.AuthorRun`) gains one new sibling component (`SaveBootstrap`). The authoring script will need a one-line addition to attach `SaveBootstrap` to the prefab root; the existing `_playerVehicleAsset` / `_biomeDistribution` / `_combatBeaconArchetypes` references on `RunSceneHost` are untouched.
- `NodeMapDto.cs` is untouched. The `IRunStateSerializable` adapter (`NodeMapSerializable`) is a separate file that delegates to `NodeMapDto.From` / `ToNodeMap`.
- No previously-banked tests are modified or deleted. The 569-test baseline holds unchanged.

The capture-before-destroy hook should green-light this slice on path inspection alone — no protected paths (prefabs / scenes / SOs / GDDs / ADRs) carry destructive changes.

## Validation plan

EditMode green attestation: 569 baseline holds + N new tests green.

Test matrix:

| # | Test | What it locks |
|---|------|---------------|
| 1 | `SaveBootstrap_Awake_binds_storage` | `SaveSystem.Bind` is called with non-null `ISaveStorage` after `SaveBootstrap.Awake` |
| 2 | `SaveBootstrap_Awake_starts_consumer` | `SaveSystem.StartBackgroundConsumer` fires after Bind (no double-call, no rebind) |
| 3 | `SaveBootstrap_Start_registers_NodeMap_serializable` | `NodeMapSerializable` is the registered IRunStateSerializable for `SYSTEM_ID="run.node_map"` |
| 4 | `SaveBootstrap_Start_calls_RunSceneHost_Initialize_with_LoadResult` | `Initialize` receives the LoadResult from `LoadRunState`; mock host asserts |
| 5 | `RunSceneHost_BeginNewRun_enqueues_write` | After `BeginNewRun(seed)`, `DrainPendingForTests` produces one .sav file on the bound `InMemorySaveStorage` containing the freshly-generated NodeMap bytes |
| 6 | `RunSceneHost_AdvanceToNextBeacon_enqueues_write` | After `Advance`, drain produces a .sav file with `current_index` matching the new cursor |
| 7 | `RunSceneHost_BeginNewRun_without_bound_storage_logs_warning_does_not_throw` | EditMode harnesses that skip SaveSystem.Bind keep working — try/catch ensures BeginNewRun stays usable in isolation |
| 8 | `NodeMapSerializable_round_trip_preserves_IsResolved` | TD precondition test: mark a beacon resolved on a live NodeMap → DTO → ToNodeMap → assert `IsResolved` still true on the reconstructed beacon |
| 9 | `NodeMapSerializable_FromDto_throws_until_resume_lands` | Explicit ADR-0011-clean error surface — `NotSupportedException` with a clear message pointing at the next slice |

## Decisions ratified for user ratification 2026-06-25

- **Q1** — Separate `SaveBootstrap` MonoBehaviour with `[DefaultExecutionOrder(-100)]`, sibling to `RunSceneHost`.
- **Q2** — Enqueue at end of `BeginNewRun` and end of `AdvanceToNextBeacon`. NOT `ExitCombat`, NOT `NotifyRewardClaimed`.
- **Q3** — Resume deferred to a later slice. `LoadResult.Loaded` is logged, then `BeginNewRun` fresh.
- **Q4** — MasteryState exhaustion dialog deferred. No dialog UXML or branch in 8b-3.
- **Q5** — Coalescing exists but two-site policy is the canonical minimum. No spray.

## Technical Director Review

See `production/td-verdicts/2026-06-25-slice-8b-3-bootstrap-brief.md` — TD-ARCHITECTURE CONCERNS gated on the `IsResolved` round-trip precondition; cleared 2026-06-25 on direct code inspection (`NodeMapDto.cs:243`, `RunController.cs:115`, `RunController.cs:218`). Verdict flipped to **APPROVE**.

Approval gate: 569 baseline holds + 9 new EditMode tests green and no compile errors on the Save asmdef + Run asmdef + test asmdef.

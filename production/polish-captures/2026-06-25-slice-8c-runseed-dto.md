# Capture — Slice 8c `RunSeedDto` + `run.seed_map` Resume-Atomic Group

**Date:** 2026-06-25
**Scope:** Second concrete RunState DTO (`RunSeedDto`), its snapshot-on-demand adapter (`RunSeedSerializable`), the `run.seed_map` resume-atomic-group gate in `RunSceneHost.Initialize`, and a mid-slice architectural amendment lifting `DtoType` as the third static-surface element on `IRunStateSerializable` / `IMasteryStateSerializable`. Fifth and final sub-slice of the ADR-0004 chain (8 envelope / 8a NodeMapDto / 8b-1 write / 8b-2 load / 8b-3 bootstrap+triggers / **8c RunSeedDto + resume gate [this slice]**).
**Companion docs:** ADR-0004 Slice 8c Amendment + Slice 8c Amendment Addendum (both already landed in `docs/architecture/adr-0004-save-persistence-architecture.md`). No standalone TD-verdict file — TD review delivered inline below under "Technical Director Review."

## Final-game picture this slice serves

This slice closes the ADR-0004 crash-and-resume promise for the run-loop layer. After this slice, a player parking on a beacon, quit-to-desktop, and relaunch finds a `.sav` containing both `RunSeed` and `NodeMap`. `RunSeed` reloads exact — so every per-step derivation (combat seed, reward seed, card-offer seed) reproduces the no-crash counterfactual on the next draw. The two DTOs are declared a **resume-atomic group**: both restored or neither (resume only fires when both DTOs survive load; either missing → both regenerate fresh). The both-or-neither gate lives in one place — `RunSceneHost.Initialize` — keeping the controller single-shape (no `ResumeRun` overload, no bimodal path).

## What is being added (new code)

**Files added (new):**
- `Assets/Scripts/Save/Dtos/RunSeedDto.cs` — `IRunStateSerializable` DTO carrying a single `int seed`. `SYSTEM_ID = "run.run_seed"`, `SCHEMA_VERSION = 1`. Self-describing entry per Slice 8b Amendment Q7 (`schema_version` field lifts from const).
- `Assets/Scripts/Save/Adapters/RunSeedSerializable.cs` — snapshot-on-demand adapter mirroring `NodeMapSerializable`. Holds `Func<RunState>` live source; `ToDto()` throws `InvalidOperationException` if source returns null (write-path wiring guard). `FromDto(object)` captures the loaded `RunSeedDto` into `LastLoaded` for the resume gate to consult.
- `Assets/Tests/EditMode/Save/RunSeedDto_round_trip_test.cs` — 6 tests covering `From(int)` projection, ToDto self-reference, FromDto field copy, FromDto wrong-type throw, and SchemaRegistry const surface.
- `Assets/Tests/EditMode/Save/RunSeedDto_wire_format_test.cs` — 3 tests locking the canonical JSON shape `{"schema_version":1,"seed":305419896}` (ordinal property sort).
- `Assets/Tests/EditMode/Save/RunSeedSerializable_test.cs` — 7 tests covering ctor null-source throw, SystemId/SchemaVersion forwarding, snapshot-on-demand projection (mutate source → next ToDto sees new value), FromDto `LastLoaded` capture, FromDto wrong-type throw.
- `Assets/Tests/EditMode/CombatView/RunSceneHost_Resume_Test.cs` — 4 integration tests with end-to-end `SaveBootstrap` flow. Plants envelopes with both DTOs via `PlantResumeFixture`, asserts `RunState.RunSeed == 0xCAFEF00D` and derived combat seeds match the no-crash counterfactual.

**Files modified:**
- `Assets/Scripts/CombatView/RunSceneHost.cs` — `Initialize(LoadResult)` signature changed to `Initialize(LoadResult, NodeMapDto, RunSeedDto)`. Added `BeginRunFromLoaded(int seed, NodeMap loadedMap)` sibling to `BeginNewRun`. The branching is `if (loadedNodeMap != null && loadedRunSeed != null) BeginRunFromLoaded(...); else BeginNewRun(null);` — one decision point, both-or-neither gate, no bimodal-path branching inside the controller. `BeginRunFromLoaded` skips `BiomeWebGenerator` and uses `loadedMap` directly; still constructs `RunController` / `SceneEncounterBuilder` / `RunSession`, fires `OnBeaconChanged`, calls `EnqueueRunStateWrite` for the first-resume save.
- `Assets/Scripts/CombatView/SaveBootstrap.cs` — registers `RunSeedSerializable` alongside the existing `NodeMapSerializable`. Reads both adapters' `LastLoaded` after `LoadRunState()` returns and passes them to `host.Initialize(result, nodeMap, runSeed)`.
- `Assets/Scripts/Save/Adapters/NodeMapSerializable.cs` — added `public NodeMapDto LastLoaded { get; private set; }` (captured by `FromDto`) so the resume gate can read it symmetrically with `RunSeedSerializable.LastLoaded`. Documented the Slice 8c contract in the type docstring.
- `Assets/Scripts/Run/AssemblyInfo.cs` — added `[assembly: InternalsVisibleTo("WastelandRun.Save.Tests")]` so the adapter tests can build a real `RunController` via the internal `StartRun` entry point.
- `Assets/Tests/EditMode/CombatView/SaveBootstrap_Test.cs` — docstring update on `LoadAndInitialize_Calls_Host_Initialize_Which_Starts_Run` to reflect the new `(LoadResult, NodeMapDto, RunSeedDto)` signature.

**Mid-slice architectural amendment — `DtoType` lifted as third static-surface element:**

During integration testing, the 4 `RunSceneHost_Resume_Test` cases failed because `SaveSystem.Load.cs` discovered entry DTO types via `runHandler.ToDto().GetType()` — but the Slice 8c adapters legitimately guard `ToDto()` against a null live source (write-path correctness), and load fires before any `RunController` exists. The fix lifts `Type DtoType { get; }` onto both interfaces:

- `Assets/Scripts/Save/IRunStateSerializable.cs` — new interface member with strengthened doc-comment ("must equal the runtime type returned by `ToDto()` when `ToDto()` succeeds").
- `Assets/Scripts/Save/IMasteryStateSerializable.cs` — same.
- `Assets/Scripts/Save/Dtos/NodeMapDto.cs` + `RunSeedDto.cs` — `[JsonIgnore] public Type DtoType => typeof(NodeMapDto);` / `typeof(RunSeedDto);`.
- `Assets/Scripts/Save/Adapters/NodeMapSerializable.cs` + `RunSeedSerializable.cs` — `public Type DtoType => typeof(NodeMapDto);` / `typeof(RunSeedDto);`.
- `Assets/Tests/EditMode/Save/StubRunSerializable.cs` + `Fixtures/EnvelopeFactory.cs` (StubMastery) — interface compliance.
- `Assets/Scripts/Save/SaveSystem.Load.cs:266-282` — load now reads `runHandler.DtoType` instead of `runHandler.ToDto().GetType()`. Doc-comment updated.

This is the Slice 8c **Amendment Addendum** in ADR-0004 (Decision 1 closing block). One paragraph; no new Decision number. CI enforcement via `SchemaRegistry_Unique_test.DtoTypeMatchesToDtoRuntimeType` (added in follow-up commit alongside this capture).

## What is being destroyed (authored values + locked fixtures)

**Nothing authored is destroyed.** Slice 8c is purely additive at every surface:

- `RunSceneHost.cs` keeps every existing serialised field, every public method's behavior, every event firing site. `Initialize` gained two parameters (`NodeMapDto`, `RunSeedDto`); the sole call site (`SaveBootstrap.Start()`) was updated in the same commit, and the EditMode test that exercised the old signature was updated to pass the new one.
- `NodeMapDto.cs` is unchanged except for the new `[JsonIgnore] DtoType` property. Wire format (`schema_version`, `biome_id`, `terminal_type`, `map_seed`, `allow_bidirectional`, `current_index`, `path_history`, `nodes`, `edges`) is bit-identical with Slice 8a. Wire-format tests (`NodeMapDto_wire_format_test`) still pass on the locked JSON literal.
- `NodeMapSerializable.cs` keeps the same `ToDto` / `FromDto` contract; the `LastLoaded` capture is additive (previously unread by production code; now read by `RunSceneHost.Initialize`).
- `SaveSystem.Load.cs` swaps one line of type discovery (`probeDto.GetType()` → `handler.DtoType`) inside a clearly-scoped try/catch. No envelope-shape change; no recovery-chain change; no schema-mismatch handling change.
- The `Run.prefab` authoring is untouched. `SaveBootstrap` was already a sibling component on the Run scene root from Slice 8b-3; this slice just adds one more `Register(...)` call inside its `Start()`.
- No GDD edits. No SO edits. No designer-tuned values touched.

The capture-before-destroy hook should green-light this slice on path inspection alone — no protected paths (prefabs / scenes / SOs / GDDs) carry destructive changes.

## Validation plan

EditMode green attestation: 605 baseline preserved + 20 new tests green + 1 follow-up CI assertion. Final tally: **607 total / 606 pass / 0 fail / 1 pre-existing skip**.

Test matrix:

| # | Test fixture | Count | What it locks |
|---|---|---|---|
| 1 | `RunSeedDto_round_trip_test` | 6 | `From(int)` → `ToDto` self-reference → `FromDto` copy preserves the seed across a round-trip. Schema const surface (`SYSTEM_ID="run.run_seed"`, `SCHEMA_VERSION=1`) on the DTO class. Wrong-type `FromDto` throws `InvalidCastException` with the offending type name. |
| 2 | `RunSeedDto_wire_format_test` | 3 | On-disk JSON is `{"schema_version":1,"seed":305419896}` (canonical resolver ordinal sort). Once this wire shape ships, locked for the life of the save. |
| 3 | `RunSeedSerializable_test` | 7 | Ctor null-source throws `ArgumentNullException`. SystemId/SchemaVersion/DtoType forward from `RunSeedDto` consts (single source of truth). Snapshot-on-demand: mutating the live source between `ToDto()` calls produces fresh values (no caching). `ToDto` with null source throws the wiring-trap message. `FromDto` captures `LastLoaded` for the resume gate. Wrong-type `FromDto` throws. |
| 4 | `RunSceneHost_Resume_Test` | 4 | End-to-end: plant envelope with both DTOs → `SaveBootstrap.Start()` registers adapters and calls `LoadRunState` → adapters' `FromDto` populates `LastLoaded` → `host.Initialize` reads both, picks `BeginRunFromLoaded`, restores `RunSeed == 0xCAFEF00D`. Fresh-run fallback when one DTO is missing (both-or-neither). Resumed `RunSeed` produces the expected derived combat seeds (ADR-0003 contract holds across save). |
| 5 | `SchemaRegistry_Unique_test.DtoTypeMatchesToDtoRuntimeType` (follow-up commit) | 1 | Reflection scan: every type implementing `IRunStateSerializable` / `IMasteryStateSerializable` with a parameterless ctor has `handler.DtoType == handler.ToDto().GetType()`. Catches drift the moment it lands. |

## Decisions ratified for user ratification 2026-06-25

- **Q1 — Resume gate location.** Inside `RunSceneHost.Initialize`, not inside `SaveSystem.Load` or `RunController`. Rationale: bootstrap is the single decision point for the runtime state shape; embedding the gate in load would require leaking RunState-semantic into the save layer.
- **Q2 — Single-shape controller.** No `RunController.ResumeRun(int seed, NodeMap map)` overload. Instead, `RunSceneHost.BeginRunFromLoaded` (sibling to `BeginNewRun`) is the host-side branch that wires up an already-rehydrated map. The controller has one constructor + one `StartRun` shape. ADR-0011 no-bimodal-path.
- **Q3 — Resume-atomic group.** `run.node_map` + `run.run_seed` declared as the `run.seed_map` resume-atomic group in ADR-0004 Decision 4 (Slice 8c Amendment). Both DTOs survive load → resume; either missing or in `SkippedSystemIds` → both regenerate. The skip-cascade is enforced at the gate (both-or-neither check) — not yet hoisted into `SaveSystem.Load` (deferred to slice ~8f when group N=2 lands).
- **Q4 — `DtoType` interface member.** Lifted mid-slice (architectural amendment) to decouple wire-type discovery from snapshot production. Endorsed by TD review (verdict inline below). ADR-0004 carries the Amendment Addendum.
- **Q5 — Mastery-side resume.** Deferred (no mastery DTOs land in this slice). The mastery-side `LastLoaded` capture pattern is reserved for whatever slice ships `MasteryStateDto`.

## Defers (ADR-0011-clean)

| Item | Reason it lights up later, not now |
|---|---|
| `NodeMapSerializable.LastLoaded` read by NodeMap-only resume consumer | Slice 8c integration already reads `LastLoaded` via the `run.seed_map` resume-atomic group. A NodeMap-only consumer would be a fresh code path; none exists. The TD risk-flag was to date-box this if it sits past one more slice — Slice 8d will reassess. |
| Skip-cascade for resume-atomic groups inside `SaveSystem.Load` | At N=1 group (this slice), the gate in `RunSceneHost.Initialize` is sufficient. When `run.run_deck` + `run.combat_state` becomes group N=2, a shared helper or declarative group-declaration in the ADR becomes valuable. Flag for slice ~8f. |
| `SaveBootstrap` mastery-side wiring | Deferred until the first mastery DTO ships (`MasteryStateDto` candidate slice 8d or 8e). The current bootstrap only registers RunState adapters. |
| Per-DTO `LastLoaded` API on `IRunStateSerializable` | Per-adapter convention for now (snapshot-on-demand pattern owns the capture). Promoting to the interface would force every implementer to track post-load state, which most don't need. Wait until N≥3 adapters carry it before lifting. |

## Technical Director Review

TD consulted 2026-06-25 after slice landed (commit `49f5252`). Verdict delivered inline:

**[TD-ARCHITECTURE]: APPROVE.** EditMode attestation: 606 pass / 0 fail / 1 pre-existing skip honored. APPROVE conditional on the test status holding through the follow-up CI assertion (`SchemaRegistry_DtoType_test`) — confirmed 2026-06-25 at 607 total / 606 pass / 0 fail / 1 skip.

- **Q1 (DtoType lift):** ENDORSE. Decouples write-time projection from type discovery cleanly. Considered alternatives rejected (probe-DTO static factory, `bool TryToDto(out object)`, polymorphic `IRunStateSerializable<TDto>`) — each either added a parallel registration mechanism, weakened the write-path contract, or introduced generic-dispatch bloat. No ADR-0011 risk: single net-new interface member, not an overload (no #5), not a mode toggle (no #3), every implementation returns a real Type (no #6).
- **Q2 (ADR amendment needed):** AMENDMENT NEEDED — one paragraph under Decision 1 (or 2), no new Decision number. Decision 1 currently locks two static surface elements (`SystemId`, `SchemaVersion`); `DtoType` is a third, and load *depends* on it being resolvable without live state. The dependency is architectural, not implementation-incidental. **Landed as the Slice 8c Amendment Addendum** in `adr-0004-save-persistence-architecture.md`.
- **Q3 (Slice 8d direction):** Top pick is **RunDeck DTO** — smallest still-meaningful next slice, third DTO in a row stress-tests the established pattern, no new resume-atomic group required, respects "build canonical 1.0 shape, no scaffolding." Close second: **MasteryStateDto** if you want to harden the asymmetric-exhaustion code path before more RunState DTOs pile up. Defer-eyeball option flagged as **stale memory** (predates Slices 7+8 — the closed Slice 6 loop is now buried under three layers of save).
- **Risk flags (tracked, not blocking):** NodeMap-only `LastLoaded` consumer (date-box if past one more slice); skip-cascade not hoisted into `SaveSystem.Load` at N=1 (fine; flag for N=2); SaveBootstrap mastery-side gap (handled when mastery DTO lands); no CI enforcement on `DtoType == ToDto().GetType()` (**resolved in follow-up commit alongside this capture**).

Approval gate: 606 baseline + 20 new EditMode tests + 1 follow-up CI assertion green, no compile errors on the Save asmdef + Run asmdef + CombatView asmdef + Save.Tests asmdef + CombatView.Tests asmdef. **Met.**

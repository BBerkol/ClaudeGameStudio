# Capture — Slice 8b-1 Save Orchestrator Write Path

**Date:** 2026-06-24
**Scope:** Land the real `SaveSystem` write orchestrator: registration API, `ISaveStorage` injection seam, single-consumer background `Task` draining `ConcurrentQueue<WriteIntent>`, temp-then-rename atomic write, 5×exponential retry budget. First sub-slice of the Slice 8b chain (8b-1 write / 8b-2 load / 8b-3 triggers).
**Companion docs:** `production/td-verdicts/2026-06-24-slice-8b-orchestrator-brief.md` (full TD review, CONCERNS verdict). ADR-0004 Slice 8b Amendment is already inlined in `docs/architecture/adr-0004-save-persistence-architecture.md` (Summary, Decision 3, Decision 5, Key Interfaces, Validation Criteria, Risks).

## Final-game picture this slice serves

By Slice 8b-3, a player crashing mid-run (Task Manager, OOM, power loss) can relaunch and resume from the last beacon-resolve boundary with byte-identical RNG state per ADR-0003. 8b-1 ships the write half: an enqueue-and-forget API that systems call after each beacon resolution, that the consumer Task fans into atomic per-category disk writes without touching the render thread. 8b-2 ships the load half; 8b-3 wires the call sites at `RunSceneHost`. Without 8b-1 the load path has nothing to load and the trigger path has nothing to call.

## What is being added (new code)

**Files added (new):**
- `Assets/Scripts/Save/ISaveStorage.cs`
- `Assets/Scripts/Save/DiskSaveStorage.cs`
- `Assets/Scripts/Save/SaveCategory.cs`
- `Assets/Scripts/Save/WriteIntent.cs`
- `Assets/Scripts/Save/SaveSystem.Write.cs` (partial class — orchestrator state + queue + consumer Task + atomic write + retry)
- `Assets/Tests/EditMode/Save/InMemorySaveStorage.cs` (test-only ISaveStorage impl)
- `Assets/Tests/EditMode/Save/SaveSystem_RegisterRunStateSerializable_test.cs`
- `Assets/Tests/EditMode/Save/SaveSystem_EnqueueRunStateWrite_test.cs`
- `Assets/Tests/EditMode/Save/SaveSystem_QueueCoalescing_test.cs`
- `Assets/Tests/EditMode/Save/SaveSystem_AtomicWrite_test.cs`
- `Assets/Tests/EditMode/Save/SaveSystem_RetryBudget_test.cs`

**Files modified:**
- `Assets/Scripts/Save/SaveSystem.cs` (`static class` → `static partial class`; add `Bind` / `Register*` / `ResetForTests` to the file, write-path logic in the partial)
- `Assets/Scripts/Save/Dtos/NodeMapDto.cs` (`[JsonIgnore]` → `[JsonProperty("schema_version")]` on `SchemaVersion`)
- `Assets/Tests/EditMode/Save/NodeMapDto_wire_format_test.cs` (locked literal + sort-assertion index update)

Code summary by file:

- **`ISaveStorage` interface** — filesystem-ops injection seam per Slice 8b Amendment Q5. `LiveDir` / `TempDir` / `OpenWrite` / `OpenRead` / `Move(src, dst, overwrite)` / `Exists` / `Delete`. ADR-0011 exception #4 (polymorphism via interface for real product/test divergence) — documented inline.
- **`DiskSaveStorage`** — production implementation. Wraps `System.IO.File.*` and takes `liveDir` + `tempDir` as plain string ctor parameters. **Does NOT reference `UnityEngine.Application`** — Save asmdef stays `noEngineReferences: true` per ADR-0002 POCO-purity lineage (ratified by 2026-06-24 TD round on Path A vs Path B). Unity-path resolution lives at the bootstrap site (CombatView — `RunSceneHost.Awake()` in 8b-3, or a tiny `SaveBootstrap` if cleaner). CI grep gate scopes `Application.persistentDataPath` / `Application.temporaryCachePath` references to that single bootstrap file.
- **`InMemorySaveStorage`** — test implementation. Backs `LiveDir` / `TempDir` with `Dictionary<string, byte[]>`. `Move` is a key-rename. Used by every 8b-1 test that doesn't need real bytes.
- **`WriteIntent`** record — `(SaveCategory category, IReadOnlyList<IRunStateSerializable> sources, long sequence)`. The sequence number lets the consumer apply coalescing (drop queued intents with lower seq for the same category).
- **`SaveCategory`** enum — `RunState`, `MasteryState`. Used to route to the correct envelope file path (`runstate.sav` / `masterystate.sav`).
- **`SaveSystem.RegisterRunStateSerializable(IRunStateSerializable)`** — key read from the instance's `SystemId` property (no explicit key arg, no hardcoded NodeMap binding). Duplicate `SystemId` throws `InvalidOperationException` immediately. Matches Slice 8b Amendment Q2 (registration via instance property).
- **`SaveSystem.EnqueueRunStateWrite()`** — collects all registered run-state serializables, snapshots them via `ToDto()` on the calling thread (to avoid background-thread reads of game state), enqueues a `WriteIntent`. Returns void; non-blocking.
- **Single-consumer background `Task`** — long-lived consumer started on first enqueue (lazy). Drains `ConcurrentQueue<WriteIntent>`, applies coalescing rule per Decision 3, serializes the **composite envelope** per Decision 5 Slice 8b Amendment (payload = map of `SystemId` → DTO-with-inline-`schema_version`), writes through `ISaveStorage` to `temp/<filename>.tmp`, validates by re-reading, rotates `.bak` (delete old `.bak`, move `.sav` → `.bak`), final `Move(tmp, .sav, overwrite: true)`. 5×exponential retry on `IOException` / sharing violation: 250 / 500 / 1000 / 2000 / 4000 ms (~7.75 s total).
- **`SaveSystem` constructor** — takes `ISaveStorage`. Default factory in Unity init constructs `DiskSaveStorage`. Tests pass `InMemorySaveStorage` directly. No `SetSavesRoot` toggle, no bimodal path.

## What is being destroyed (authored values + locked fixtures)

The Save namespace is mostly greenfield, but **two authored fixtures from Slice 8a get a controlled drift** because the per-DTO `schema_version` lifts to serialized:

### 1. `Assets/Scripts/Save/Dtos/NodeMapDto.cs` — `[JsonIgnore]` on `SchemaVersion` removed

Current state (NodeMapDto.cs:96-97):
```csharp
[JsonIgnore]
public int SchemaVersion => SCHEMA_VERSION;
```

Slice 8b-1 state:
```csharp
[JsonProperty("schema_version")]
public int SchemaVersion => SCHEMA_VERSION;
```

`SystemId` keeps its `[JsonIgnore]` — the map key in the composite payload **is** the SystemId, repeating it inside the entry would be ADR-0011 #2 (parallel storage). `SchemaVersion` becomes self-describing on each entry per Slice 8b Amendment Q7. This is the *only* DTO-side change in 8b-1.

### 2. `Assets/Tests/EditMode/Save/NodeMapDto_wire_format_test.cs` — locked canonical literal gains `schema_version`

Current locked literal (NodeMapDto_wire_format_test.cs:42-59):
```
{"allow_bidirectional":true,"biome_id":"biome.wire_fixture","current_index":1,
 "edges":[...],"map_seed":305419896,"nodes":[...],"path_history":[0],
 "terminal_type":"Haven"}
```

Slice 8b-1 locked literal (insert ordinally between `path_history` and `terminal_type`):
```
{"allow_bidirectional":true,"biome_id":"biome.wire_fixture","current_index":1,
 "edges":[...],"map_seed":305419896,"nodes":[...],"path_history":[0],
 "schema_version":1,
 "terminal_type":"Haven"}
```

The literal is the contract; the change is documented as ADR-0004 Slice 8b Amendment (Decision 5 Q7) per the test's own docstring (lines 28-32: "If you change the spec and this test fails, the spec change requires an ADR-0004 amendment and a SchemaVersion bump on NodeMapDto, NOT a fixture refresh. The locked literal is the contract."). **The amendment is the authority; the literal moves with it.** No `SchemaVersion` bump on the DTO — the wire shape is gaining a field that was previously implicit, not changing semantic; existing pre-8b saves do not exist (zero call sites outside Save namespace), so there is no install-base compatibility surface to preserve.

### 3. `Assets/Tests/EditMode/Save/NodeMapDto_wire_format_test.cs` — ordinal-sort assertion gains one index

Current assertion (lines 142-157) probes 8 field positions (`p0`..`p7`). Slice 8b-1 adds `p_schema` between `path_history` (p6) and `terminal_type` (p7); assertion chain extended.

### 4. `Assets/Tests/EditMode/Save/ComputeEnvelopeChecksum_fixture_test.cs` — **NOT destroyed**

The checksum fixture uses `system: "fixture-test"` and `payload: null`. The Slice 8b Amendment changes the **semantic** of `system` (was DTO `SystemId`, now category name) but not its **type** (still a string). The fixture's arbitrary string value is unaffected; `payload: null` is still a valid composite payload (no entries). SHA-256 hex stays `a70b47635e0f42b9f61888da6dc9424bd4a90eacdc9a50ff9c361f0ddd2c2914`.

### 5. `Assets/Scripts/Save/SaveSystem.cs` static helpers — **NOT destroyed**

`ComputeEnvelopeChecksum(Envelope)`, `SerializeCanonical(object)`, `NowTimestamp()`, `Iso8601UtcMsFormat` constant. All survive untouched. The orchestrator additions are new instance methods on a partial class (or a separate file in the same namespace) routing through `ISaveStorage`. The helpers stay static because they are pure functions over inputs.

### 6. `Assets/Scripts/Save/Envelope.cs` — **NOT destroyed structurally**

POCO field shape unchanged. `system` and `schema_version` field NAMES survive; only the semantic of `system` changes (per Decision 5 amendment). XML doc comments will be updated to match the amendment.

## Verbatim values being moved

```
NodeMapDto.SchemaVersion           [JsonIgnore]          → [JsonProperty("schema_version")]
NodeMapDto wire literal (line 58)  "terminal_type":"Haven"  → "schema_version":1,"terminal_type":"Haven"
NodeMapDto sort-assert (line 156)  p6 < p7 (path_history < terminal_type)  → p6 < p_schema < p7
```

Every other authored value in Slice 8a stays byte-identical.

## Test surface (Slice 8b-1 EditMode adds)

All under `Assets/Tests/EditMode/Save/`:

- **`SaveSystem_RegisterRunStateSerializable_test.cs`** — register one, register a duplicate-SystemId, register null. Confirms `SystemId` is read from the instance property, not an explicit arg.
- **`SaveSystem_EnqueueRunStateWrite_test.cs`** — single enqueue produces one `.sav` containing the registered DTO under its `SystemId` key; envelope `system` field is `"run_state"`; envelope checksum re-verifies on read-back.
- **`SaveSystem_QueueCoalescing_test.cs`** — N rapid enqueues for the same category produce ≤ N writes (typically 1–2 due to coalescing); the final `.sav` reflects the last-enqueued DTO state. Verifies the "ordering + coalescing" rationale from Decision 3 amendment.
- **`SaveSystem_AtomicWrite_test.cs`** — verifies the temp → validate → rotate-bak → move sequence. Reads bytes from `InMemorySaveStorage` at every step; confirms `.sav` is never observed mid-write.
- **`SaveSystem_RetryBudget_test.cs`** — `InMemorySaveStorage` configured to throw `IOException` on N consecutive `Move` calls; confirms 5×exponential retry sequence (timing not asserted — we assert call counts only, not wall-clock).
- **`NodeMapDto_wire_format_test.cs`** — updated literal + new ordinal-sort index, per "destroyed values" above.

## Slice 8b-1 close criteria

- All 542 baseline EditMode tests green (excluding the one pre-existing ignore).
- New tests above green.
- `ISaveStorage` injection seam present; Save asmdef stays `noEngineReferences: true`; CI grep gate (`Application.persistentDataPath` / `Application.temporaryCachePath` only in the single CombatView bootstrap file — 8b-1 has no such references at all, since the bootstrap lands in 8b-3) green.
- NodeMapDto wire-format literal matches the new Slice 8b-1 shape.
- Zero call sites of `EnqueueRunStateWrite` / `RegisterRunStateSerializable` outside the Save namespace itself (those land at 8b-3).
- ADR-0004 Slice 8b Amendment landed (already done — verified in `docs/architecture/adr-0004-save-persistence-architecture.md`).

## Out of scope for 8b-1

- `LoadRunState` and recovery chain → **8b-2**.
- `RunSceneHost.Awake()` registration + write-trigger wiring + `Application.quitting` sync path + `RegisterCommitPredicate` / `RegisterHandlerActivePredicate` → **8b-3**.
- MasteryState orchestration → arrives when the first MasteryState DTO lands; 8b-1 plumbs the `SaveCategory.MasteryState` path symmetrically but no MasteryState DTO is registered yet.
- Newtonsoft `link.xml` — already in place at Slice 8a (`Assets/Scripts/Save/link.xml` preserves `WastelandRun.Save.Dtos`); no change for 8b-1.
- Dev tooling (`InjectFault`, `DebugWriteCount`, `SimulateSlowWrite`, `/dev/corrupt-save`, `/dev/fresh-install-sim`) — deferred per ADR-0004 "Follow-on infrastructure tasks"; not blocking 8b-1.

## Technical Director Review

See `production/td-verdicts/2026-06-24-slice-8b-orchestrator-brief.md` for the full Q1–Q7 verdict. Verdict: **CONCERNS** (substantive but not REJECT). The three CONCERNS — single-consumer Task, `ISaveStorage` injection, composite payload per category — are now codified in ADR-0004 Slice 8b Amendment (committed inline). Pre-flight items closed: amendment ✓, capture file ✓ (this doc). Implementation may proceed once user approves this capture.

## User approval gate

Per `CLAUDE.md` Collaborative Protocol: I am asking for explicit approval of this capture before authoring any code in `Assets/Scripts/Save/`. Approve to proceed with 8b-1 code, or push back on:

- Destroy surface (Slice 8a fixture literal update)
- Test surface (the 5 new EditMode tests above)
- Scope boundary (lift schema_version to serialized = 8b-1; LoadRunState / RunSceneHost triggers = 8b-2 / 8b-3)

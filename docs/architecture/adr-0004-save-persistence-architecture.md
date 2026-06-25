# ADR-0004: Save & Persistence Architecture

## Status

Accepted

## Date

2026-04-24

## Last Verified

2026-06-25 (Slice 8c Amendment Addendum: `DtoType` lifted as third static-surface element on `IRunStateSerializable` / `IMasteryStateSerializable` — see Decision 1)

2026-06-25 (Slice 8c Amendment: Resume Atomicity grouping + `RunSeedDto` lands as the second RunState DTO, alongside `NodeMapDto`)

2026-06-24 (Slice 8b Amendment: single-consumer Task + `ConcurrentQueue` + `ISaveStorage` injection seam + composite payload per category with per-DTO partial-skip)

## Decision Makers

- User (creative/design lead) — approved 2026-04-24
- technical-director (architectural review) — approved 2026-04-24
- unity-specialist (engine-idiom review) — approved 2026-04-24

## Summary

Save is a **passive orchestrator**. Each gameplay system owns its own Data Transfer Object (DTO) and `Serialize()/Deserialize()` pair; Save composes them into a **per-category composite envelope** (one `runstate.sav` containing all RunState DTOs keyed by `SystemId`; one `masterystate.sav` for MasteryState — Slice 8b Amendment 2026-06-24), writes atomically via temp-then-rename through an `ISaveStorage` injection seam on a **single long-lived consumer Task draining a `ConcurrentQueue<WriteIntent>`** (Slice 8b Amendment 2026-06-24), and provides a **per-category independent recovery chain** (live → orphaned temp → N=1 backup) with **per-DTO partial-skip on schema mismatch *inside* the envelope** (Slice 8b Amendment 2026-06-24). The **schema registry is distributed** — `const string SystemId` + `const int SchemaVersion` live on each DTO class, not in a central registry file. Partial-load failure policy is category-asymmetric: exhausted RunState → non-blocking "start new run"; exhausted MasteryState → blocking dialog requiring explicit player consent before reset. `RunSeed` is persisted in the RunState envelope so deterministic replay (per ADR-0003) survives quit/resume.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.13f1) |
| **Domain** | Core / Scripting / I/O |
| **Engine APIs Used** | `Application.persistentDataPath`, `Application.temporaryCachePath`, `Application.quitting`, `FileStream` with `FileShare.None`, `File.Move(src, dst, overwrite: true)` (3-arg overload, Mono 6.4+ / .NET Standard 2.1), `Directory.CreateDirectory` |
| **Third-Party Packages** | `com.unity.nuget.newtonsoft-json` (official Unity Newtonsoft package) — required for Dictionary serialization (rejected `UnityEngine.JsonUtility` on that ground) and IL2CPP-preservable reflection (rejected `System.Text.Json` which requires `[JsonSerializable]` per DTO under IL2CPP) |
| **IL2CPP Considerations** | `Assets/Scripts/Save/link.xml` must preserve the `WastelandRun.Save.Dtos` namespace. Without `link.xml` IL2CPP stripping breaks Newtonsoft reflection-based deserialization at runtime. Created at Save implementation time. |
| **Knowledge Risk** | **LOW** for basic file I/O (`FileStream`, `File.Move`, atomic rename patterns) — stable across Unity 6.x. **MEDIUM** for `FileStream.Flush(flushToDisk: true)` reliability under Mono (see OQ4 — historically inconsistent; requires code-review verification; P/Invoke fallback `FlushFileBuffers` identified if needed). **MEDIUM** for `File.Move(..., overwrite: true)` 3-arg overload runtime resolution on Unity 6.3 Mono (see OQ5 — .NET Standard 2.1 exposes this but runtime-verify; `MoveFileEx` with `MOVEFILE_REPLACE_EXISTING` is the P/Invoke fallback). **LOW** for Newtonsoft + link.xml IL2CPP preservation — pattern is well-documented post-cutoff; preservation rules have not changed. |
| **Unity 6.3 Breaking-Change Flags Relevant to This ADR** | None of Unity 6.3's breaking changes affect Save directly. URP Render Graph, SerializeField fields-only, FindObjectsOfType deprecation, USS stricter parser, Box2D v3, New Input System — none touch file I/O, JSON serialization, or `Application.quitting`. Save operates entirely within POCO space + core .NET BCL. |
| **Forward Guards** | When Save surfaces user-facing UI (save indicator, error toast, blocking dialogs), it enters UI Toolkit territory — USS parser strictness applies to any stylesheets written at that point. When dev tooling is authored (`InjectFault`, `DebugWriteCount` console commands), the New Input System is the default input path. |

## ADR Dependencies

| Depends On | Why |
|------------|-----|
| **ADR-0002** (Card Combat: POCO State Model) | POCOs are naturally JSON-serializable. The Card Combat model is assembled from plain C# classes with no MonoBehaviour/ScriptableObject entanglement (ADR-0002 decision #1 + #2), which means their DTOs are trivial projections over existing state. No reflection-over-Unity-types is needed. |
| **ADR-0003** (Deterministic RNG Discipline) | ADR-0003's validation criterion requires `RunSeed` survive quit/resume to preserve deterministic replay. This ADR locks `RunSeed: int` into the top-level RunState envelope as the single authoritative source. Scoped seeds (`RunSeed ^ stepIndex`) are NOT persisted — they are derived on demand per ADR-0003's Rule 3. |

**Enables (downstream ADRs that will reference this):**
- Future per-system DTO ADRs (if any system needs non-trivial schema decisions beyond the envelope contract).
- Future migration runtime ADR (first post-EA content patch that changes a DTO — see "EA-mode vs permanent semantic" in GDD R5; the AC inversion trigger is documented there).

## Context

### The Problem

Wasteland Run has two categories of durable state:

1. **RunState** — complete in-progress run snapshot (vehicle loadout, deck state, Scrap balance, node map position, per-node runtime flags, RunSeed, Rare pity counter, loot cooldowns, encounter handler-active flag).
2. **MasteryState** — between-run persistence (chassis mastery XP, mastery level, unlocked content flags per chassis).

The save system must:

- Preserve mid-run state faithfully across quit/resume, crash, and hard-kill (Task Manager, OOM, driver crash, power loss).
- Never stall the render loop on disk I/O (60fps target, 16.6ms frame budget per tech-prefs).
- Survive antivirus sharing violations (Windows Defender real-time scan can hold a file handle 500–4000ms).
- Detect corruption (bit-rot, interrupted writes, malicious edits) via checksum.
- Recover from interrupted writes (orphaned `.tmp` files in the OS temp tree).
- Not corrupt Steam Cloud by syncing incomplete writes.
- Allow schema evolution post-ship without breaking existing player saves.
- Reset mastery only with explicit player consent — mastery is the cross-run emotional contract.

The GDD (`design/gdd/save-persistence.md`) is **Designed** status, second revision 2026-04-21, and encodes all of these requirements in R1–R8 plus 13 edge cases. Two director conditions from the 2026-04-24 gate check (TD-C2) required explicit ADR coverage of **schema-registry location** and **partial-load failure policy** before advancing. This ADR resolves both.

### Why This ADR Now

- Save & Persistence is the third of three Foundation-layer ADRs scheduled for Week 1 of Technical Setup per the 2026-04-24 gate-check task stack (Card Combat → Loot RNG → Save).
- Eight systems across the 10-GDD MVP set declare hard dependencies on Save's DTO contract (Card System, Card Combat, Vehicle & Part, Scrap Economy, Node Map, Node Encounter, Loot & Reward, Meta Progression). Their DTO schemas cannot be authored until the envelope contract, schema-versioning semantic, and checksum helper are locked in.
- Seven stories in the current epic backlog reference `SaveSystem.ComputeEnvelopeChecksum` and `SaveSystem.NowTimestamp` as dependencies — they are blocked until this ADR ships.
- The handler save-block retrofit (2026-04-23) and the Node Map `IsCommitInProgress` facade both depend on Save consulting an external predicate before writes; that consultation pattern is an architectural decision, not a trivial wiring detail.
- Deferring this ADR would force systems to either (a) invent their own ad-hoc schema constants and drift, or (b) block on the whole stack waiting for Save. Both are worse than deciding now.

## Decision

### Decision 1: Distributed Schema Registry

**Each DTO class declares its own stable identity constants:**

```csharp
public sealed class NodeMapDto : IRunStateSerializable
{
    public const string SYSTEM_ID      = "run.node_map";
    public const int    SCHEMA_VERSION = 1;

    [JsonIgnore] public string SystemId      => SYSTEM_ID;
    [JsonIgnore] public int    SchemaVersion => SCHEMA_VERSION;
    // ... payload fields ...
}
```

**Rules:**

- `SYSTEM_ID` is a stable string that never changes post-ship — it keys the DTO into the envelope contract for all time.
- `SCHEMA_VERSION` starts at `1` and increments by `1` on every breaking payload change (field added/removed/renamed/re-typed).
- **No central registry file.** There is no `docs/architecture/schema-registry.yaml` or equivalent — the DTO class IS the single source of truth for its schema identity.
- **Uniqueness is enforced by a reflection unit test** (`SchemaRegistry_Unique_test`): at CI time, scan the gameplay assembly for all types with both constants, assert every `SYSTEM_ID` appears exactly once. Duplicate `SYSTEM_ID` fails the build with both offending types named.

**Naming conventions (locked by Slice 8a Amendment 2026-06-24):**

- **Const fields** are UPPER_SNAKE_CASE — `SYSTEM_ID` / `SCHEMA_VERSION` — matching the project-wide constants convention from `.claude/docs/technical-preferences.md`. PascalCase here would collide with the matching instance properties on `IRunStateSerializable` / `IMasteryStateSerializable`, forcing rename gymnastics. The reflection scan in `SchemaRegistry_Unique_test` looks for these exact UPPER_SNAKE_CASE names.
- **Instance properties** that satisfy the interface read from the const — `public string SystemId => SYSTEM_ID;`. Single source of truth: the registry scan and the orchestrator's runtime dispatch agree because they both ultimately resolve to the same const.
- **`SystemId` value** is the dotted-snake hierarchical convention `"category.identifier"`:
  - **First segment** is the asymmetric-exhaustion category: `run` (RunState) or `mastery` (MasteryState). The recovery chain in Decision 4 treats these categories with different exhaustion policies; encoding the category in the wire value makes corrupted-save diagnostics human-readable without cross-referencing code.
  - **Second segment** is snake_case identifier per system.
  - Examples: `run.node_map`, `run.player_vehicle`, `run.run_deck`, `mastery.unlocks`.
  - **Flat snake-case** (`node-map`, `nodemap`) was rejected because it loses the category signal at the wire layer. **PascalCase coupled to the C# type name** (`NodeMapDto`) was rejected because a rename either breaks the `SystemId` (forbidden pattern #4 from ADR-0011) or keeps a stale string (forbidden pattern #7).

**Rationale:** A central registry file becomes a drift vector — every DTO change requires editing two places and eventually diverges. Distributed constants + CI-enforced uniqueness gives the same drift-resistance at zero coordination cost. This follows the same principle as ADR-0003's "each seeded system owns its stepIndex semantic" — single source of truth, CI-enforced.

The naming conventions above were locked at Slice 8a (first real DTO landing) when the const-vs-property collision surfaced. Slice 8 (envelope + interfaces, no DTOs) deferred the question because zero implementations existed; Slice 8a forced the resolution by being the first concrete DTO.

**Slice 8c Amendment Addendum (2026-06-25) — `DtoType` is the third static-surface element.**

The interface contract surfaces **three** static elements that the load path consults before any live source exists: `SystemId`, `SchemaVersion`, and `DtoType`. `DtoType` is the runtime `System.Type` of the DTO this handler produces / consumes (e.g. `typeof(NodeMapDto)`).

```csharp
public interface IRunStateSerializable
{
    string SystemId      { get; }
    int    SchemaVersion { get; }
    Type   DtoType       { get; }  // Slice 8c Amendment Addendum
    object ToDto();
    void   FromDto(object dto);
}
```

**Rules:**

- `DtoType` must equal the runtime type returned by `ToDto()` when `ToDto()` succeeds — never a base type, never an interface, never `null`.
- `DtoType` must be resolvable before any live source exists. Adapters that project DTOs off a live `Func<TLiveSource>` (e.g. `NodeMapSerializable`, `RunSeedSerializable`) guard `ToDto()` against a null source and throw on misuse; the load path uses `DtoType` instead of `ToDto().GetType()` so type discovery does not trip the wiring guard.
- A new CI test (`SchemaRegistry_DtoType_test` — sibling of `SchemaRegistry_Unique_test`) asserts the equality `handler.DtoType == handler.ToDto().GetType()` on every registered serializable whose `ToDto` is safe to invoke without setup (DTOs that act as their own handle). Adapters with ctor-injected live sources are covered by integration tests (`RunSceneHost_Resume_Test`).

**Rationale:** Slice 8b-3 introduced snapshot-on-demand adapters that legitimately guard `ToDto()` against null live sources (write-path correctness — fail-fast on orchestrator wiring traps). Slice 8c shipped the load path that needed entry-type discovery before any controller exists. Discovering type via `ToDto().GetType()` couples write-time projection to type discovery, then breaks when the write-path guard fires during load. Lifting `DtoType` decouples the two responsibilities cleanly — load reads a pure static surface, write keeps its guard. Considered alternatives (probe-DTO static factory, `bool TryToDto(out object)`, polymorphic `IRunStateSerializable<TDto>`) were rejected: each added either a parallel registration mechanism, a weakened write contract, or generic-dispatch bloat — without removing the original method. `DtoType` is a single net-new interface member; no overload pair (no ADR-0011 #5), no bimodal path (no #3), no stub return (no #6).

### Decision 2: Mutually Exclusive Serialization Interfaces

**`IRunStateSerializable` and `IMasteryStateSerializable` are declared disjoint.** A class may not implement both.

```csharp
public interface IRunStateSerializable
{
    string SystemId      { get; }
    int    SchemaVersion { get; }
    Type   DtoType       { get; } // Slice 8c Amendment Addendum — see Decision 1
    object ToDto();               // returns system-specific DTO
    void   FromDto(object dto);   // consumes same shape
}

public interface IMasteryStateSerializable
{
    // same shape, different interface
}
```

**Enforcement:** C# has no language-level compile-time enforcement for interface disjointness. A **reflection-based unit test** (`InterfaceExclusion_test`) scans the gameplay assembly at CI time and fails the build if any type implements both interfaces, naming the offender.

**Rationale:** GDD R1 says a system's state is either run-scoped or cross-run-persistent — never both. Dividing this contract at the interface level (rather than by convention) makes misuse a compile- or CI-time failure, not a runtime corruption pattern discovered in production.

### Decision 3: Atomic Write — Single-Consumer Background Task + Temp-in-`temporaryCachePath` + `File.Move` with `overwrite: true`

**The write sequence:**

1. Main thread enqueues a `WriteIntent` on a `ConcurrentQueue<WriteIntent>`. (Slice 8b Amendment 2026-06-24: was "thread-safe queue" generically; locked to `ConcurrentQueue<WriteIntent>` with one long-lived consumer Task.)
2. The **single long-lived consumer Task** dequeues, applies coalescing (drop queued writes for the same `SystemId` if a newer one is already pending — see Coalescing below), calls each system's `ToDto()` under a single `try`/`catch`. Any exception mid-assembly → abort write, preserve live `.sav`, log `SaveAssemblyFailedException`.
3. Consumer serializes the envelope to JSON bytes via Newtonsoft (canonical serialization: sorted property order, UTF-8, no indentation, no trailing whitespace).
4. Write bytes to `ISaveStorage.TempDir/[filename].tmp` via `ISaveStorage.OpenWrite` (`FileMode.Create`, `FileShare.None` semantics for `DiskSaveStorage`). Call `Flush(flushToDisk: true)` before closing. (Slice 8b Amendment 2026-06-24: path access wrapped behind `ISaveStorage`; no direct `Application.temporaryCachePath` reference outside `DiskSaveStorage`.)
5. Validate `.tmp` checksum by re-reading and recomputing.
6. Rotate `.bak` (delete `.bak`, rename `.sav` → `.bak`) via `ISaveStorage.Move` / `ISaveStorage.Delete`.
7. `ISaveStorage.Move(tmpPath, targetPath, overwrite: true)` — the atomic rename.
8. Consumer reports completion on a main-thread-pumped callback.

**Single-consumer rationale (Slice 8b Amendment 2026-06-24):**

The background writer is locked to **one long-lived consumer Task**, not per-write `Task.Run`. Three reasons make this load-bearing for ADR-0004's contract:

1. **Ordering.** ADR-0004's "last-write-wins per category" is only structurally true with one consumer. With `Task.Run` per write, the .NET ThreadPool can schedule an older write *after* a newer one, silently corrupting "newest state wins" — a class of race that wouldn't fail a unit test but would produce wrong saves under load.
2. **Coalescing.** A single consumer can inspect the queue and drop a queued write if a newer write for the same `SystemId` is already pending. This gives free debounce for the periodic-idle-flush trigger (GDD R2) — repeated rapid enqueues collapse to one disk write — without each call site having to coordinate.
3. **Bounded retry-park.** The 5×exponential retry budget can pin a thread for up to ~7.75s on a wedged file lock (antivirus hold, Defender real-time scan). With per-write `Task.Run`, N queued writes during a stuck handle = N parked ThreadPool threads. With one consumer, one thread parks; the queue absorbs further enqueues at zero thread cost.

**Cost:** one always-on background thread. Acceptable — it spends 99.9% of its life on a queue wait. Documented in Performance Implications.

**Two exceptions to background execution:**

- `Application.quitting` write is **synchronous on the main thread** — Unity's quit callback cannot await a background task.
- Launch-time recovery is **synchronous before the main menu displays** — load must complete before the player can interact.

**Critical architectural choice: temp files live in `temporaryCachePath`, NOT `saves/`.**

- `temporaryCachePath` is a distinct OS tree (`%LocalAppData%/Temp/[CompanyName]/[ProductName]/` on Windows) separate from `persistentDataPath/saves/`.
- Steam Cloud Auto-Cloud uses an **include-path allowlist** pointing at `saves/` — it never touches `temporaryCachePath`. Incomplete `.tmp` writes can never corrupt the cloud sync.

**Sharing-violation retry: 5 attempts with exponential backoff 250ms, 500ms, 1000ms, 2000ms, 4000ms (~7.75s total) on the background thread.** Sized to clear Windows Defender real-time scan hold windows (500–4000ms typical). Since retries are off-main-thread, no frame stall occurs regardless of retry latency.

**Rationale:** Temp-then-rename is the only crash-safe write pattern on Windows/NTFS, ext4, and APFS. Placing temp files in a Steam-Cloud-invisible tree eliminates an entire class of corruption (partial upload of incomplete write) without requiring Steam Cloud exclusion configuration. Background execution protects the 16.6ms frame budget. The 7.75s retry budget was raised from an earlier 300ms default after the GDD second-revision review flagged it insufficient for real-world Defender holds.

### Decision 4: Per-Category Independent Recovery Chain — Live → Orphaned Temp → N=1 Backup

**On every launch, before the main menu displays, Save validates candidates in order for each category independently:**

1. `*.sav` — validate envelope + checksum → use if valid, exit.
2. `*.tmp` in `temporaryCachePath` — validate → if valid, `File.Move` promote to `*.sav` and use (orphaned temp from interrupted write).
3. `*.sav.bak` → use if valid. Log `SaveBackupRecovery` **only on this branch**.
4. **No valid file** — category-asymmetric policy:
   - **RunState**: non-blocking. Main menu shows "New Run" only. Log `RunStateFullLoss`. A corrupted run is equivalent to "start a new run" at EA scope — documented non-blocking per N=1 risk acknowledgment in GDD R4.
   - **MasteryState**: **blocking dialog** — "Mastery progress could not be loaded — this indicates save file corruption across all backups. Please report this to support. Continue with a fresh mastery state, or quit to attempt manual recovery?" Log `MasteryStateFullLoss` on either choice. **Mastery is NEVER silently reset.**

**Per-category independence:** The RunState chain and the MasteryState chain run separately. Corrupted RunState does not block MasteryState recovery, and vice versa.

**Exit-on-first-valid:** If `.sav` validates, `.bak` is never consulted or promoted. `SaveBackupRecovery` telemetry is logged only when a backup was actually used — this makes the signal meaningful for post-ship corruption monitoring.

**N=1 backup (one rolling backup) is an explicit EA-scope risk acceptance** — GDD R4 documents this. In the narrow failure window where both `.sav` and `.bak` corrupt without a valid `.tmp`, RunState is lost. This is non-blocking because the player starts a new run (which is what a lost run means at EA). If post-EA telemetry shows meaningful `RunStateFullLoss` incidence, N can be raised to 2 via the tuning knob without breaking save compatibility.

**Partial-load failure policy (resolves TD-C2):**

- **Envelope-level failure** (malformed JSON, missing required envelope fields, envelope_version mismatch) → skip to next candidate silently.
- **Checksum failure** → skip to next candidate silently. Log `CorruptionDetected` with expected-vs-actual hex.
- **Schema-version mismatch** (higher → player downgraded; lower → EA-mode only: also incompatible until migration runtime lands) → skip to next candidate silently.
- **All envelope- or checksum-level failures are silent chain progressions.** No user is shown "save 1 of 3 failed" — they see the final category-level outcome only.
- **Exhausted chain** → category-asymmetric as above (non-blocking for RunState, blocking for MasteryState).

### Decision 5: Envelope Contract — Composite Payload per Category + Version + Checksum

**(Slice 8b Amendment 2026-06-24) The envelope wraps one *category* (RunState or MasteryState), not one DTO. Its payload is a composite map keyed by `SystemId`, with each entry self-describing its schema version inline.**

```json
{
  "envelope_version": 1,
  "system":           "run_state",
  "schema_version":   1,
  "written_at":       "2026-04-24T14:32:01.123Z",
  "checksum":         "a3f5c8...",
  "payload": {
    "run.node_map": { "schema_version": 1, "...node map fields...": "..." },
    "run.deck":     { "schema_version": 1, "...deck fields...":     "..." },
    "run.scrap":    { "schema_version": 1, "...scrap fields...":    "..." }
  }
}
```

**Field semantics:**

| Field | Type | Semantic |
|-------|------|----------|
| `envelope_version` | int | Reserved for envelope-level schema changes (e.g., adding a new field like `locale`). `=1` for all EA releases. Higher envelope_version than current code → treated as incompatible, skip candidate. |
| `system` | string | **(Slice 8b Amendment 2026-06-24)** Category name: `"run_state"` or `"mastery_state"`. NOT a DTO's `SystemId`. The single-DTO-per-envelope shape was rejected — see "Composite payload rationale" below. |
| `schema_version` | int | **(Slice 8b Amendment 2026-06-24)** Envelope spec version. Distinct from the per-DTO `schema_version` values inside `payload`. `=1` for all EA releases. |
| `written_at` | string | ISO 8601 UTC, millisecond precision (`yyyy-MM-ddTHH:mm:ss.fffZ`). Owned by `SaveSystem.NowTimestamp()`. |
| `checksum` | string | SHA-256 hex over the canonical serialization of the envelope **excluding the `checksum` field itself**. Owned by `SaveSystem.ComputeEnvelopeChecksum()`. Covers the whole composite payload — one integrity signal per category. |
| `payload` | object | **(Slice 8b Amendment 2026-06-24)** Composite map: key = DTO `SystemId` (e.g., `"run.node_map"`), value = DTO with an inline `schema_version` field. Save composes DTOs by reading each registered serializable's `SystemId` and inserting `ToDto()` under that key. |

**Per-DTO wire encoding (Slice 8b Amendment 2026-06-24):**

- `SystemId` stays `[JsonIgnore]` on DTOs — the map key in `payload` *is* the SystemId; repeating it inside would create the drift vector ADR-0011 #2 forbids.
- `SchemaVersion` lifts to a serialized property on each DTO (drop `[JsonIgnore]` from the interface property's implementation; serialized as `schema_version` snake_case). This is the self-describing per-entry version the load path compares against the current `SCHEMA_VERSION` const.

**Per-DTO schema-mismatch behavior (Slice 8b Amendment 2026-06-24):**

When loading, each entry in `payload` is matched against the corresponding registered serializable's `SCHEMA_VERSION`:

- **Match** → handler `FromDto` is invoked; entry rehydrates.
- **Mismatch (either direction)** → that entry is silently skipped; sibling entries continue loading. The category load returns success with the surviving entries hydrated and the skipped systems left in their default-constructed state. Skipped entries are logged as `SchemaMismatchSkipped` with `(SystemId, expected, actual)` for telemetry.
- **Entry absent** (registered system has no entry in payload — new DTO added in a later patch) → handler not called; system stays at default-constructed state.
- **Entry present but unregistered** (DTO removed from code but still in file — system retired post-patch) → entry silently ignored.

This is **partial-skip at the entry layer, not the file layer**. The envelope itself remains valid; checksum holds; recovery chain does not advance to `.bak`. This preserves "live wins if checksum-valid" while letting individual systems evolve their schemas without forcing whole-category corruption fallback.

**Resume Atomicity — DTOs may be declared a resume-atomic group (Slice 8c Amendment 2026-06-25):**

Per-DTO partial-skip applies independently *unless* DTOs are declared a **resume-atomic group**. Resume-atomic DTOs **load together or regenerate together**: if any member is absent or schema-skipped, the resume path falls back to a fresh run for all members in the group. The envelope itself remains valid (the load chain does not advance to `.bak`); other resume-atomic groups and standalone DTOs in the same envelope still rehydrate normally.

Resume-atomic groups are declared in this ADR. **First and only group at Slice 8c:**

| Group | Members | Reason |
|-------|---------|--------|
| `run.seed_map` | `run.run_seed`, `run.node_map` | `NodeMap.MapSeed = RunSeed ^ 0x4D41` per ADR-0003. NodeMap rehydrated against a regenerated RunSeed would carry a `MapSeed` whose provenance no longer matches its own structure — every per-step derivation (`RunSeed ^ stepIndex ^ <salt>` per ADR-0003 Rule 3 / RunController) downstream would silently diverge from the no-crash counterfactual. Players would not notice individual rolls, but a run that crashes through a pity-counter boundary would silently desync. Both-or-neither is the only policy that preserves the **ADR-0003 `RunSeed ^ stepIndex` contract across save boundaries**. |

**How resume-atomicity is enforced:** the bootstrap site that hands `LoadResult` to the run-orchestrator (Slice 8b-3 / 8c: `SaveBootstrap.LoadAndInitialize` → `RunSceneHost.Initialize`) is the single decision point. The orchestrator inspects `LoadResult.Outcome` + `LoadResult.SkippedSystemIds` against the declared groups; if any group is not whole, the orchestrator regenerates the entire group from scratch instead of rehydrating partial state. There is no per-DTO "rehydrate-or-regenerate" toggle on individual `IRunStateSerializable` implementations — provenance is the bootstrap's concern, not the controller's (ADR-0011 #3 avoidance).

**Future groups must be declared in this ADR.** When the next coupled pair surfaces (likely `run.run_deck` + `run.combat_state` once those DTOs ship — a deck shuffle is meaningless without the matching combat seed it was drawn against), append a row to the table above. No `[ResumeAtomic]` attribute, no central manifest file — drift-resistance is preserved the same way Decision 1's schema registry stays drift-resistant (one ADR row, CI-light enforcement via the bootstrap decision site).

**Cross-reference ADR-0003:** the `RunSeed ^ stepIndex` contract now explicitly **spans save boundaries**. The resume-atomic group above is the implementation mechanism — without it, a crash-recovered run would produce different combat shuffles, reward rolls, and card offers than its no-crash counterfactual, which is the exact class of silent determinism break ADR-0003 forbids. The `RunSeedDto_Resume_Preserves_Derived_Seeds` test (Slice 8c) locks the property end-to-end: load a known RunSeed, advance one step, assert `DeriveCombatSeed(stepIndex)` matches the pre-save value.

**Composite payload rationale (Slice 8b Amendment 2026-06-24):**

Decision 4 ("per-category" recovery chain) was correct; Decision 3's original wording was silent on per-DTO file layout. The per-DTO file layout (one `.sav` per DTO) was rejected because:

1. **Atomicity.** One `File.Move` advances the entire run state as a unit. With N files, a crash between writes ships inconsistent state across DTOs (e.g., NodeMap advanced but RunDeck not — undefined runtime).
2. **Recovery semantics.** ADR-0004's "live → orphaned tmp → bak" chain is one decision per category. N files = N independent chains = N×M (DTOs × rungs) corruption matrix, with no defined cross-DTO ordering on partial recovery.
3. **Checksum coverage.** Envelope checksum covers the whole category as a unit. Per-DTO files mean per-DTO checksums and no cross-DTO integrity signal — a tampered file could pass its own checksum while contradicting siblings.
4. **Steam Cloud.** One file ≈ one sync unit. N files multiply conflict-resolution surface.

**Cost:** a single DTO schema bump rewrites the whole envelope on next save. Acceptable — the partial-skip rule above means schema bumps are not breaking events for sibling systems, and the envelope is small (~7–14 KB total).

**Two and only two helpers Save provides:**

- `SaveSystem.ComputeEnvelopeChecksum(envelopeDto) → string` — canonical serialization + SHA-256 hex. Systems never compute their own checksums.
- `SaveSystem.NowTimestamp() → string` — ISO 8601 UTC ms format. Systems never format their own timestamps.

**Canonical serialization specification:**

- Newtonsoft.Json with `DefaultContractResolver`.
- Sorted property order.
- UTF-8 encoding.
- No indentation.
- No trailing whitespace.

**SHA-256 instance caching:** A single `IncrementalHash` instance is thread-local and reused across writes to avoid per-write GC allocation.

**Rationale:** Centralizing checksum and timestamp computation removes two drift vectors. If a system computed its own checksum, a bug in one system would produce files that load in one place and fail in another — a nightmare to diagnose. Canonical serialization guarantees the same bytes on every platform, so cross-machine saves (Steam Cloud) validate correctly.

### Decision 6: Newtonsoft.Json + `link.xml` for IL2CPP Preservation

**Library choice: `com.unity.nuget.newtonsoft-json`** (the official Unity-supported Newtonsoft package).

**IL2CPP preservation: `Assets/Scripts/Save/link.xml`** declaring the `WastelandRun.Save.Dtos` namespace preserved. IL2CPP strips unreferenced types aggressively; reflection-based deserialization (Newtonsoft's default mode) would fail at runtime for any DTO the static analyzer didn't see referenced. The `link.xml` entry is a single-file fix that covers all DTOs in that namespace — new DTOs added later inherit the preservation automatically.

**Why not `UnityEngine.JsonUtility`:** It does not support `Dictionary<K,V>`. Save's dependent systems require dictionary serialization (`NodeFlagsDTO` keys node-index → flags map; `LootStateDTO.PartDropCooldown` keys `SlotType` → int). Working around this with list-of-pairs is a public-API distortion of every dependent DTO.

**Why not `System.Text.Json`:** Under IL2CPP, `System.Text.Json` requires `[JsonSerializable]` source generators on every DTO. Each new DTO would require a generator registration; forgetting one breaks deserialization silently at runtime. Newtonsoft's `link.xml` approach centralizes preservation in one file.

**Why not a binary format (Protobuf, MessagePack, FlatBuffers):** Save file total budget is ~7–14 KB per F3. Binary encoding saves maybe 2–3 KB — below noise. Meanwhile binary formats lose human-diffable debugging (invaluable during schema migrations), add IL2CPP codegen toolchain complexity, and require a separate editor tool to inspect saves. JSON wins on developer ergonomics at this scale.

### Decision 7: `RunSeed` Persisted as `RunSeedDto` in the RunState Envelope (Slice 8c Amendment 2026-06-25)

**`RunSeedDto` is the single authoritative carrier of `RunSeed`. It implements `IRunStateSerializable` with `SYSTEM_ID = "run.run_seed"` and `SCHEMA_VERSION = 1`. Its on-wire payload is a single int (`seed`).** Resume-atomically grouped with `run.node_map` (see Decision 4 "Resume Atomicity").

- Established once at run-start by `RunSceneHost.BeginNewRun` (which already seeds `RunController.StartRun`).
- Persisted in every RunState write — `RunSeedDto` projects from the live `RunState.RunSeed` via the `RunSeedSerializable` adapter (mirrors the `NodeMapSerializable` snapshot-on-demand pattern, see Slice 8b-3).
- Loaded on resume as part of the resume-atomic group; all subsequent seeded-call derivations (`RunSeed ^ stepIndex ^ <salt>` per ADR-0003 Rule 3, applied by `RunController.DeriveCombatSeed` / `DeriveRewardSeed` / `DeriveCardOfferSeed`) recompute from this single field. `NodeMap.MapSeed` is derived from `RunSeed ^ 0x4D41` at run-start and persisted as part of `NodeMapDto.map_seed` for on-disk completeness — but on resume, the two must agree (this is structurally guaranteed by the resume-atomic group).
- **Scoped seeds are NEVER persisted as standalone DTOs** — only the root `RunSeed`. This is a direct application of ADR-0003 Rule 3: scoped seeds are derived on demand at each seeded entry point. Persisting them would create two truths and permit drift. (`NodeMap.MapSeed` is persisted *because the NodeMap structure depends on it*, not as an independent derivation source.)

**Slice 8 history:** Decision 7's original Slice 8 wording placed `run_seed: int` at the top-level envelope (sibling to `payload`). The Slice 8b Amendment moved DTOs into a composite payload map keyed by `SystemId`. RunSeed was deferred at Slice 8b-3 (TD verdict Q3) so that "land an int in a DTO" wouldn't bundle into the bootstrap+triggers slice. Slice 8c lands it as `RunSeedDto` under the composite-payload contract — same data, single DTO shape, consistent with every other RunState system.

**Rationale:** ADR-0003's Validation Criterion for deterministic replay requires that a run can be suspended and resumed without changing any future RNG draw. Persisting only the root seed + the step indices already carried in gameplay state (current node, pity counters, traversal list) is sufficient to reconstruct every future draw exactly. This is the smallest serialization surface that preserves determinism. The resume-atomic grouping with `run.node_map` (Decision 4) is what makes "preserves determinism" hold across the save boundary — without it, RunSeed alone could resume but the NodeMap it was paired with might not, leading to seed/map provenance drift.

---

### Key Interfaces (Locked by This ADR)

```csharp
// Serialization contracts — systems implement exactly one
public interface IRunStateSerializable
{
    string SystemId { get; }
    int    SchemaVersion { get; }
    object ToDto();
    void   FromDto(object dto);
}

public interface IMasteryStateSerializable { /* same shape, distinct interface */ }

// Save public API — all other Save operations are internal
public static class SaveSystem
{
    // Write orchestration
    public static void EnqueueRunStateWrite();      // non-blocking
    public static void EnqueueMasteryStateWrite();  // non-blocking

    // Load (launch-time synchronous)
    public static RunStateLoadResult     LoadRunState();
    public static MasteryStateLoadResult LoadMasteryState();

    // Helpers (only these two — systems may not roll their own)
    public static string ComputeEnvelopeChecksum(Envelope envelope);
    public static string NowTimestamp();

    // Status queries (for UI)
    public static bool HasValidRunState { get; }

    // Node Map commit-coordination (retrofit 2026-04-23)
    public static void RegisterCommitPredicate(Func<bool> isCommitInProgress);
    public static void RegisterHandlerActivePredicate(Func<bool> isHandlerActive);
}

// Envelope DTO (Save-owned)
public sealed class Envelope
{
    public int     envelope_version;
    public string  system;            // (Slice 8b Amendment 2026-06-24) category name: "run_state" or "mastery_state" — NOT a DTO SystemId
    public int     schema_version;    // envelope spec version, distinct from per-DTO schema versions
    public string  written_at;
    public string  checksum;
    public object  payload;           // (Slice 8b Amendment 2026-06-24) composite map: SystemId → DTO-with-inline-schema_version
}

// (Slice 8b Amendment 2026-06-24) ISaveStorage — injection seam for filesystem ops.
// ADR-0011 exception #4 (polymorphism via interface, real product/test divergence).
// Production: DiskSaveStorage takes liveDir/tempDir as ctor strings — Save asmdef
// stays `noEngineReferences: true` (ADR-0002 POCO-purity lineage).
// Tests: InMemorySaveStorage (RAM-backed) or DiskSaveStorage with Path.GetTempPath()
// directories for write-path tests that need real bytes.
// No SetSavesRoot(string) toggle — bimodal-path bridge, forbidden by ADR-0011 #3.
internal interface ISaveStorage
{
    string LiveDir { get; }           // bootstrap injects: Application.persistentDataPath/saves
    string TempDir { get; }           // bootstrap injects: Application.temporaryCachePath
    Stream OpenWrite(string path);
    Stream OpenRead(string path);
    void Move(string src, string dst, bool overwrite);
    bool Exists(string path);
    void Delete(string path);
}
```

Note on commit and handler predicates: Save consults these before every write. If either returns `true`, the write is deferred until both return `false` (retrofit contract from GDD R2 / NE handler save-block / `INodeMapSerializable.IsCommitInProgress`). This keeps Save free of schema knowledge while honoring the per-system write gates.

**Note on `ISaveStorage` (Slice 8b Amendment 2026-06-24, ratified by 2026-06-24 TD round):** `SaveSystem` takes `ISaveStorage` via `SaveSystem.Bind(ISaveStorage)` called once at game-init. The Save assembly stays `noEngineReferences: true` per ADR-0002 POCO-purity lineage — `DiskSaveStorage` does NOT call `UnityEngine.Application`. Instead, a Unity-aware bootstrap in the view layer (CombatView / Slice 8b-3 `RunSceneHost`) resolves `Application.persistentDataPath` + `Application.temporaryCachePath` and passes them to `DiskSaveStorage`'s ctor:

```csharp
// In CombatView (RunSceneHost or a tiny SaveBootstrap), one place in the codebase:
var storage = new DiskSaveStorage(
    liveDir: Path.Combine(Application.persistentDataPath, "saves"),
    tempDir: Application.temporaryCachePath);
SaveSystem.Bind(storage);
```

Tests construct `SaveSystem` directly with `InMemorySaveStorage` or a `Path.GetTempPath() + Guid` directory under `DiskSaveStorage`. This is **injection, not toggle** — there is no runtime decision to "use real or fake storage"; the binding is set at game-init once and never inspected. The CI grep gate (Validation Criteria + Risks row) constrains `Application.persistentDataPath` / `Application.temporaryCachePath` references to the bootstrap site only.

## Alternatives Considered

### Alternative 1: Central Schema Registry File (`docs/architecture/schema-registry.yaml`)

**Rejected.** Every DTO schema change would require editing both the DTO class and the registry file, creating a guaranteed drift vector. CI-enforced uniqueness on distributed `SystemId` constants gives identical coordination safety at zero maintenance cost.

**Where it would be worth reconsidering:** If the project ever adds runtime code that must enumerate all schemas without loading the gameplay assembly (e.g., an external save-inspector tool or a server-side migration service). That's not in EA scope and would be an ADR amendment, not a replacement.

### Alternative 2: Unity `PlayerPrefs` for MasteryState

**Rejected.** `PlayerPrefs` has no atomic write guarantees, no checksum, no schema versioning, and stores data in platform-inconsistent locations (Windows registry on PC, `NSUserDefaults` on macOS, XML file on Linux). On Windows, `PlayerPrefs` is a registry key under `HKCU\Software\[CompanyName]\[ProductName]` — opaque to Steam Cloud, fragile under registry corruption, and impossible to diff or hand-recover. MasteryState is the cross-run emotional contract; putting it in an unreliable storage tier is a deal-breaker.

### Alternative 3: Binary Format (Protocol Buffers, MessagePack, FlatBuffers)

**Rejected at EA scope.** The ~7–14 KB total save budget makes the size savings negligible (2–3 KB at most). Binary formats cost: (a) IL2CPP codegen toolchain complexity, (b) loss of human-diffable debugging during schema evolution, (c) a separate editor tool required to inspect saves. The Newtonsoft + JSON + canonical-serialization approach gives deterministic bytes for cross-machine Steam Cloud validation without any of these costs. Reconsider only if save size ever exceeds ~50 KB.

### Alternative 4: Monolithic RunState DTO (Save Owns the Schema)

**Rejected.** If Save owned `RunStateDTO` as a single flat class, every gameplay system adding persistent state would require a Save-side code change. This violates the "each system owns its state" pattern used throughout the project, creates Save-ownership of gameplay concepts (Save would have a `deck_card_ids` field — but Deck belongs to Card System), and makes schema evolution require Save's schema version to tick for every dependent system's change. The per-system DTO + passive-orchestrator pattern lets each system evolve independently.

### Alternative 5: Composite Save File (Single `save.sav` Containing All DTOs)

**Rejected in favor of per-category files (`runstate.sav` + `masterystate.sav`).** A single file would force a corrupted MasteryState checksum to invalidate the entire save, including RunState. Separation preserves GDD R1's independence-of-categories guarantee: corrupting one does not lose the other. The storage cost (two envelopes instead of one) is negligible.

### Alternative 6: Synchronous Writes (No Background Task)

**Rejected.** Save writes take tens of milliseconds typical, hundreds of milliseconds worst-case (antivirus sharing violations can hold handles 500–4000ms before the first retry even starts). At 16.6ms frame budget, a synchronous write on the main thread would cause a multi-frame stall visible to the player on every node transition. The background-task pattern is the only way to honor the frame budget while retaining crash-safe atomic writes.

## Consequences

### Positive

- **Partial-load failure policy explicit and resolved (TD-C2 director condition).** RunState and MasteryState have distinct, documented policies; mastery never silently resets.
- **Schema-registry location explicit and resolved (TD-C2 director condition).** `SystemId` + `SchemaVersion` live on DTOs, CI-enforced unique.
- **Each system owns its DTO schema, can evolve independently.** Save never becomes a bottleneck on feature work.
- **Atomic writes are crash-safe across NTFS, ext4, APFS.** Temp-then-rename is the universal pattern.
- **Steam Cloud corruption is structurally impossible.** Temp files live outside the cloud-synced tree; only validated complete files reach `saves/`.
- **RNG determinism survives quit/resume.** ADR-0003's validation criterion holds for saved runs.
- **Background writes preserve 60fps target.** No I/O blocks the main thread except on clean quit and launch-time recovery (both acceptable).
- **Single source of truth for checksum + timestamp** — no format drift possible.
- **JSON debuggability.** Hand-readable files help diagnose schema migration bugs post-ship.
- **`link.xml` IL2CPP preservation is a one-line-per-namespace fix.** New DTOs added to `WastelandRun.Save.Dtos` inherit preservation automatically.

### Negative

- **C# has no language-level enforcement for interface disjointness.** Mutual exclusion relies on the `InterfaceExclusion_test` CI check. If the CI is ever disabled, silent violations become possible. Mitigation: the test is cheap, runs on every PR, and its failure message names the offending type directly.
- **Distributed schema constants require CI discipline.** `SchemaRegistry_Unique_test` must never be skipped. Same mitigation.
- **Newtonsoft.Json adds a package dependency** (roughly 600 KB to the build). Acceptable trade for dictionary support + IL2CPP reflection preservation.
- **`link.xml` must be maintained.** If the DTO namespace is ever reorganized (e.g., sub-namespaces per category), `link.xml` needs updating. Mitigation: the project convention is all DTOs in `WastelandRun.Save.Dtos` — this rule is documented in the Save implementation guide and enforced by code review.
- **Two exceptions to background-task discipline** (`Application.quitting` synchronous, launch-time recovery synchronous). These are unavoidable given Unity's lifecycle model but must be guarded against regression — adding more "just this once sync" writes must require ADR amendment.
- **Mono `File.Move(..., overwrite: true)` and `FileStream.Flush(flushToDisk: true)` have runtime-verification risk** (OQ4, OQ5). P/Invoke fallbacks are identified but not yet proven. Code review at Save implementation gate is the enforcement point.

### Neutral

- **No migration runtime at EA.** First content patch that breaks a DTO must introduce the migration chain (GDD R5a) and invert the EA-mode "lower = incompatible" AC. This is a scheduled, documented future ADR, not a gap.
- **Visual/Audio and UI contracts are deferred** (save indicator, error toast, blocking dialog styling) to HUD UX spec and Main Menu UX spec. Save's UI API surface is the boolean `HasValidRunState` and the blocking-dialog event — not any widget.

## Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| `File.Move(..., overwrite: true)` 3-arg overload fails to resolve on Unity 6.3 Mono runtime | LOW | HIGH (atomic write broken) | Code review at Save implementation gate must verify runtime resolution. P/Invoke `MoveFileEx` with `MOVEFILE_REPLACE_EXISTING` is the identified fallback. Document in Save implementation story AC. |
| `FileStream.Flush(flushToDisk: true)` inconsistent on Mono — write may return before kernel buffers flush | LOW | MEDIUM (window for hard-kill data loss) | Code review at implementation gate. P/Invoke `FlushFileBuffers` (Windows) or `fsync` (Unix) as platform-specific fallback. Integration test writes then hard-kills; recovery chain must see the written bytes. |
| `link.xml` preservation missed on a new DTO namespace | MEDIUM | HIGH (runtime deserialization null on IL2CPP builds) | All DTOs must land in `WastelandRun.Save.Dtos`. Code review convention. Consider a reflection test that round-trips every `IRunStateSerializable` / `IMasteryStateSerializable` implementor through Newtonsoft on a headless test run — it would detect preservation gaps before ship. |
| Schema `SystemId` collision (two systems register the same string) | LOW | MEDIUM (load routes to wrong handler) | `SchemaRegistry_Unique_test` CI check. Duplicate detection names both types in the failure message. |
| Interface exclusion violated (class implements both `IRunStateSerializable` and `IMasteryStateSerializable`) | LOW | MEDIUM (data misrouted between categories) | `InterfaceExclusion_test` CI check. |
| Newtonsoft serialization non-deterministic across machines (unlikely — canonical serializer is well-specified, but cultural formatting or property order drift could bite) | LOW | HIGH (Steam Cloud sync sees checksum failures) | Canonical serialization spec documented. `ComputeEnvelopeChecksum_fixture_test` seeds a known envelope and asserts an exact SHA-256 hex. Any environmental drift fails this test. |
| Antivirus sharing violation retry budget (7.75s) still insufficient for slow Defender scans on low-spec disks | LOW | LOW (non-blocking warning; live file preserved) | Telemetry on `SaveWriteFailedException` post-ship. Raise budget in tuning knob if real-world incidence >0.1%. |
| Steam Cloud last-write-wins inversion on multi-machine play | MEDIUM | MEDIUM (progress appears to rewind) | GDD OQ3 documented as accepted limitation. Store page copy will note multi-machine caveat. Merge strategy deferred to post-EA. |
| `CompanyName` / `ProductName` changed post-EA — silently relocates `persistentDataPath`, orphans all saves | LOW | CATASTROPHIC (every player loses all saves on patch) | CI check (GDD EC10) compares `ProjectSettings.asset` to the baseline in tech-prefs "Project Identity" section (to be added before EA ship). Mismatch = build fails. |
| Developer adds a write helper that doesn't go through `SaveSystem.EnqueueRunStateWrite` — bypasses background task | LOW | MEDIUM (main-thread stall regression) | Code review convention: all file writes in `Assets/Scripts/Save/` namespace. `File.Write*` grep over the rest of the codebase as part of CI. |
| **(Slice 8b Amendment 2026-06-24)** Single-consumer Task wedged on a stuck file lock past the 7.75s retry budget — queue grows unbounded under sustained enqueues | LOW | MEDIUM (memory growth + delayed writes) | Coalescing rule (Decision 3) already drops queued duplicates per `SystemId`, so growth is bounded by the count of distinct categories × pending writes. Add soft-limit telemetry: `SaveSystem.QueueDepth` gauge; warn at depth >32. If a real wedge surfaces post-EA, escalate to fault tooling. |
| **(Slice 8b Amendment 2026-06-24)** `Application.persistentDataPath` reference leaks outside the CombatView bootstrap site via a copy-paste or a "just this once" call site | MEDIUM | HIGH (CI batchmode test seam breaks; "works in editor, fails in tests" drift) | CI grep gate on `Application.persistentDataPath` and `Application.temporaryCachePath` — only the single bootstrap file in CombatView may reference them. Save asmdef stays `noEngineReferences: true` (compile-time backstop — Save code cannot even attempt these calls). Test failure names the offending file. Same mitigation pattern as ADR-0011 #1 forbidden-pattern grep. |

## Performance Implications

**Allocations per write:** One `StringBuilder` (Newtonsoft internal), one output byte array, plus per-DTO allocation done by each system's `ToDto()`. SHA-256 uses the thread-local cached `IncrementalHash` — zero per-write allocation on the hashing step.

**Main-thread cost per write:** Enqueue operation only — a single `ConcurrentQueue.Enqueue` call. Effectively zero (<1µs).

**Background-thread cost per write:** Dominated by Newtonsoft serialization (~0.5ms for a typical 4 KB RunState envelope) and I/O (typically 2–20ms on SSD; 50–200ms on HDD; up to 4000ms on an antivirus hold).

**Load-time cost at launch:** One synchronous chain-walk per category. Typical: ~10ms (one file read + deserialize + checksum verify). Worst case: three file reads + three verifications = ~30ms. Acceptable within the main menu display budget.

**Storage:** ~7–14 KB persistent on disk per category pair (live + backup), plus up to 7 KB transient `.tmp` during writes. Negligible.

**Frame budget impact:** **Zero**. Writes are background-thread, and the enqueue is free. The only main-thread writes are `Application.quitting` (player is already quitting, stall is invisible) and launch-time recovery (happens before the main menu interactive state).

**Telemetry-relevant metrics** (for post-ship performance monitoring):

- `SaveSystem.WriteLatency` histogram (background-thread wall-clock time)
- `SaveSystem.SerializationLatency` histogram (Newtonsoft wall-clock time)
- `SaveSystem.SharingViolationRetries` counter (distribution of retry counts)
- `SaveSystem.CorruptionDetected` event (per-file corruption events)
- `SaveSystem.BackupRecovery` event (how often `.bak` actually saves us)

## Migration Plan

**For existing POCO code (commit `015b904` Card Combat):** No migration required. Card Combat state is already POCO and is already the exemplar this ADR generalizes. When Card Combat introduces persistence (post-combat snapshot for crash recovery per GDD R13), it will add a `CombatSnapshotDTO` implementing `IRunStateSerializable` following this ADR's rules.

**For new systems (all other MVP GDDs):**

- Declare DTO with `const string SYSTEM_ID` + `const int SCHEMA_VERSION` (UPPER_SNAKE_CASE; Slice 8a amendment); the instance properties on the interface read from the consts.
- Implement either `IRunStateSerializable` or `IMasteryStateSerializable` — never both.
- Use `SaveSystem.ComputeEnvelopeChecksum` and `SaveSystem.NowTimestamp` helpers; never roll your own.
- DTO class goes in `WastelandRun.Save.Dtos` namespace for `link.xml` preservation.
- Register `ToDto` / `FromDto` with `SaveSystem` at system boot.

**For engine/tooling infrastructure:**

- Add `com.unity.nuget.newtonsoft-json` to `Packages/manifest.json` at Save implementation gate.
- Create `Assets/Scripts/Save/link.xml` preserving `WastelandRun.Save.Dtos`.
- Add `Project Identity` section to `.claude/docs/technical-preferences.md` locking `CompanyName` + `ProductName`.
- Add CI check: `ProjectSettings.asset` baseline vs tech-prefs.
- Configure Steamworks partner portal Auto-Cloud with a single include-path root: `%LocalAppDataLow%/[CompanyName]/[ProductName]/saves/`.

**For testing infrastructure:**

- Add `SaveSystem.DebugWriteCount`, `SaveSystem.InjectFault(FaultType)`, `SaveSystem.SimulateSlowWrite(delayMs)` dev tooling alongside Save implementation.
- Add `/dev/corrupt-save`, `/dev/fresh-install-sim` console commands.
- Wire sequence-number log events per GDD "Dev Tooling Dependencies".

## Validation Criteria

This ADR is considered successfully implemented when:

- [ ] `SaveSystem` public API matches the Key Interfaces locked above.
- [ ] `IRunStateSerializable` and `IMasteryStateSerializable` exist as distinct interfaces; `InterfaceExclusion_test` is in CI and green.
- [ ] `SchemaRegistry_Unique_test` scans all DTOs and passes; CI green.
- [ ] Every DTO class declares `const string SYSTEM_ID` + `const int SCHEMA_VERSION` (UPPER_SNAKE_CASE; Slice 8a amendment) with PascalCase instance-property accessors satisfying the marker interface.
- [ ] Envelope schema matches Decision 5 exactly; `ComputeEnvelopeChecksum_fixture_test` passes.
- [ ] All writes (except `Application.quitting` and launch-time recovery) execute on background `Task`; code review confirms no main-thread `File.Write*` calls in `Assets/Scripts/Save/` except in the two documented exceptions.
- [ ] `.tmp` files written to `temporaryCachePath`; `.sav` and `.bak` in `persistentDataPath/saves/`.
- [ ] `File.Move(..., overwrite: true)` 3-arg overload resolves at runtime on target Mono (OQ5 closed).
- [ ] `FileStream.Flush(flushToDisk: true)` verified to actually flush on Mono (OQ4 closed) or P/Invoke fallback is in place.
- [ ] `Assets/Scripts/Save/link.xml` exists and preserves `WastelandRun.Save.Dtos`.
- [ ] Newtonsoft.Json package installed; build succeeds under IL2CPP.
- [ ] Recovery chain works per Decision 4; all Crash Recovery ACs in GDD pass.
- [ ] RunState envelope contains `run_seed: int`; resuming a run reproduces the same Loot RNG sequence as an uninterrupted run given identical player choices.
- [ ] Sharing-violation retry budget: 5 attempts, 250/500/1000/2000/4000ms spacing, all on background thread; frame time monitor shows no spike during retries.
- [ ] `Project Identity` section added to `.claude/docs/technical-preferences.md` locking `CompanyName` + `ProductName`; CI check in place.
- [ ] Integration tests for EC2 (sharing violation), EC3 (disk full), EC9 (orphaned temp), EC13 (run-end crash window) pass using `InjectFault` tooling.
- [ ] Formula verification ACs pass (20-node run = 20 RunState writes in the no-flush-no-quit case; 14 combat nodes = 14 MasteryState writes).
- [ ] **(Slice 8b Amendment 2026-06-24)** `ISaveStorage` injection seam present; Save asmdef stays `noEngineReferences: true` (ADR-0002 POCO-purity lineage); zero `Application.persistentDataPath` / `Application.temporaryCachePath` references outside the CombatView bootstrap site (typically `RunSceneHost.Awake()`). CI grep gate scopes "outside one bootstrap file."
- [ ] **(Slice 8b Amendment 2026-06-24)** Single-consumer ordering test: N concurrent enqueues for the same `SystemId` produce one disk write (coalesced) whose payload reflects the last enqueued state.
- [ ] **(Slice 8b Amendment 2026-06-24)** Composite-payload partial-skip test: an envelope with one entry at a wrong `schema_version` rehydrates sibling entries fully and leaves the mismatched entry at default-constructed state; load result reports skipped `SystemId`.
- [ ] **(Slice 8b Amendment 2026-06-24)** Recovery-chain test exercises all three rungs (live → orphaned tmp → bak) using `InMemorySaveStorage`; trigger tests for 8b-3 hit no filesystem at all.
- [ ] **(Slice 8c Amendment 2026-06-25)** `RunSeedDto` ships in `WastelandRun.Save.Dtos` with `SYSTEM_ID = "run.run_seed"` + `SCHEMA_VERSION = 1`; round-trip test covers wire shape + canonical JSON; `SchemaRegistry_Unique_test` passes with the new ID.
- [ ] **(Slice 8c Amendment 2026-06-25)** `RunSeedSerializable` adapter projects fresh DTOs off `Func<RunState>` per call (mirrors `NodeMapSerializable`); adapter test covers SystemId/SchemaVersion forwarding, fresh-projection invariant, live-source closure mutation, null-source throw, FromDto `LastLoaded` capture.
- [ ] **(Slice 8c Amendment 2026-06-25)** `RunSceneHost.Initialize(LoadResult)` enforces the `run.seed_map` resume-atomic group: both `run.run_seed` and `run.node_map` loaded ∧ neither in `SkippedSystemIds` → rehydrate via internal `BeginRunFromLoaded(seed, map)`; otherwise → fresh `BeginNewRun(null)`. Tests cover happy resume, mixed-skip both-or-neither fallback, and the determinism proof (`RunSeed_Resume_Preserves_Derived_Seeds`: same `DeriveCombatSeed(stepIndex)` post-resume as pre-save).

## GDD Requirements Addressed

| GDD Requirement | Section | How This ADR Addresses It |
|-----------------|---------|---------------------------|
| R1 — Save Scope Separation (RunState vs MasteryState) | Save & Persistence — Core Rules | Decision 2: mutually exclusive interfaces `IRunStateSerializable` / `IMasteryStateSerializable`, CI-enforced disjoint. |
| R2 — Write Triggers (node resolution, periodic idle flush, clean quit; handler save-block gate; mid-combat in-memory-only) | Save & Persistence — Core Rules | Decision 3: background `Task` orchestrates; `Application.quitting` synchronous exception. Decision 4 retrofit predicates (`RegisterCommitPredicate`, `RegisterHandlerActivePredicate`) honor the GDD R2 gates without Save owning schema knowledge. |
| R3 — Atomic Write Pattern | Save & Persistence — Core Rules | Decision 3: temp-in-`temporaryCachePath` + `File.Move(..., overwrite: true)` + sharing-violation retry + disk-full handling. |
| R4 — N=1 Rolling Backup | Save & Persistence — Core Rules | Decision 4: per-category chain includes live → temp → one backup; EA-scope risk acceptance documented. |
| R5 — Schema Versioning (EA: per-system tag, no migration chain) | Save & Persistence — Core Rules | Decision 1: `SchemaVersion` constant per DTO; Decision 4: EA-mode "any mismatch = incompatible" policy; future migration runtime flagged as separate ADR (GDD R5a trigger). |
| R6 — Crash Recovery | Save & Persistence — Core Rules | Decision 4: per-category independent chain; exit-on-first-valid; asymmetric exhaustion policy (non-blocking for RunState, blocking dialog for MasteryState). |
| R7 — Save File Location | Save & Persistence — Core Rules | Decision 3: `persistentDataPath/saves/` for live + backup; `temporaryCachePath` for `.tmp` (Steam Cloud include-path never reaches it). |
| R8 — DTO Schemas (Scrap Economy, Loot & Reward, Node Encounter retrofits) | Save & Persistence — Core Rules | Decision 1: each DTO owns its `SystemId` + `SchemaVersion`; Decision 5: Save composes them into envelope without schema knowledge. |
| Interactions with Other Systems (8 systems declared) | Save & Persistence — Interactions | Decision 2 interfaces + Decision 5 envelope contract are the sole coupling surface; each system owns its DTO per GDD ownership table. |
| `INodeMapSerializable.IsCommitInProgress` facade (retrofit 2026-04-23) | Save & Persistence — Interactions | Decision 3 + `RegisterCommitPredicate` API: Save consults external predicate before every write; write deferred until predicate returns false. |
| Node Encounter Handler Save-Block (retrofit 2026-04-23) | Save & Persistence — Interactions | `RegisterHandlerActivePredicate` mirrors commit predicate pattern; auto-save fires on `HandlerActive == false` transition. |
| F1 — RunState Write Frequency per Run | Save & Persistence — Formulas | Decision 3 background execution keeps write count within formula bounds (16–33 typical, 53 pathological); periodic-flush cap honored in write orchestrator. |
| F3 — On-Disk Storage Budget | Save & Persistence — Formulas | Decision 6 JSON format sized correctly for 7–14 KB total budget; binary-format alternative explicitly rejected because size savings are negligible. |
| EC1–EC13 (all edge cases) | Save & Persistence — Edge Cases | Atomic write + per-category chain + exhaustion asymmetry cover all 13 edge cases. Specific mappings: EC2 (Decision 3 retry); EC3 (background-thread catch); EC4 (Decision 4 asymmetric exhaustion); EC5 (Decision 4 envelope_version); EC9 (Decision 4 step 2 orphaned-temp promotion); EC10 (tech-prefs `Project Identity` + CI check); EC11 (Save returns keys faithfully — loading systems own fallback); EC12 (Decision 3 background writes + GDD periodic flush); EC13 (run-end marker detection in RunState envelope payload). |
| OQ4 (`FlushFileBuffers` on Mono) | Save & Persistence — Open Questions | Risk table + Validation Criteria require code-review closure at implementation gate; P/Invoke fallback identified. |
| OQ5 (`File.Move` 3-arg overload on Unity 6.3 Mono) | Save & Persistence — Open Questions | Same as OQ4: code-review closure + `MoveFileEx` P/Invoke fallback. |
| ADR-0003 validation criterion (RunSeed survives quit/resume) | ADR-0003 Validation Criteria | Decision 7: `RunSeed` is persisted in RunState envelope; scoped seeds re-derived on each seeded call per ADR-0003 Rule 3. |
| TD-C2 director condition (schema-registry location explicit) | 2026-04-24 gate-check report | Decision 1: distributed constants on DTOs, CI-enforced unique — no central registry file. |
| TD-C2 director condition (partial-load failure policy explicit) | 2026-04-24 gate-check report | Decision 4: per-category asymmetric chain (non-blocking for RunState, blocking dialog for MasteryState), envelope/checksum failures silent chain-progressions, exhaustion policy explicit. |

## Related

- **GDD**: `design/gdd/save-persistence.md` (Designed, 2nd revision 2026-04-21)
- **Upstream ADRs**: ADR-0002 (POCO model), ADR-0003 (Deterministic RNG discipline)
- **Downstream ADRs**: Future migration-runtime ADR (triggered by first content patch that breaks a DTO per GDD R5a); possible per-system DTO ADRs if any system needs non-trivial schema decisions beyond the envelope contract
- **Test framework**: NUnit via Unity Test Framework
- **Follow-on infrastructure tasks** (tracked separately, not part of this ADR):
  - Add `Project Identity` section to `.claude/docs/technical-preferences.md`
  - CI check: `ProjectSettings.asset` baseline vs tech-prefs
  - Create `Assets/Scripts/Save/link.xml` at Save implementation time
  - Configure Steamworks Auto-Cloud include-path `saves/`
  - Implement dev tooling: `DebugWriteCount`, `InjectFault`, `SimulateSlowWrite`, `/dev/corrupt-save`, `/dev/fresh-install-sim`
- **Gate-check report**: `production/gate-checks/2026-04-24-systems-design-to-technical-setup.md` (TD-C2 condition)
- **Session state**: `production/session-state/active.md`

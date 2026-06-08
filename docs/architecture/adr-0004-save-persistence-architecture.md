# ADR-0004: Save & Persistence Architecture

## Status

Accepted

## Date

2026-04-24

## Last Verified

2026-04-24

## Decision Makers

- User (creative/design lead) — approved 2026-04-24
- technical-director (architectural review) — approved 2026-04-24
- unity-specialist (engine-idiom review) — approved 2026-04-24

## Summary

Save is a **passive orchestrator**. Each gameplay system owns its own Data Transfer Object (DTO) and `Serialize()/Deserialize()` pair; Save composes them into a versioned envelope, writes atomically via temp-then-rename on a background `Task`, and provides a **per-category independent recovery chain** (live → orphaned temp → N=1 backup). The **schema registry is distributed** — `const string SystemId` + `const int SchemaVersion` live on each DTO class, not in a central registry file. Partial-load failure policy is category-asymmetric: exhausted RunState → non-blocking "start new run"; exhausted MasteryState → blocking dialog requiring explicit player consent before reset. `RunSeed` is persisted in the RunState envelope so deterministic replay (per ADR-0003) survives quit/resume.

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
public sealed class LootStateDTO
{
    public const string SystemId      = "loot-reward";
    public const int    SchemaVersion = 1;
    // ... payload fields ...
}
```

**Rules:**

- `SystemId` is a stable string that never changes post-ship — it keys the DTO into the envelope contract for all time.
- `SchemaVersion` starts at `1` and increments by `1` on every breaking payload change (field added/removed/renamed/re-typed).
- **No central registry file.** There is no `docs/architecture/schema-registry.yaml` or equivalent — the DTO class IS the single source of truth for its schema identity.
- **Uniqueness is enforced by a reflection unit test** (`SchemaRegistry_Unique_test`): at CI time, scan the gameplay assembly for all types with both constants, assert every `SystemId` appears exactly once. Duplicate `SystemId` fails the build with both offending types named.

**Rationale:** A central registry file becomes a drift vector — every DTO change requires editing two places and eventually diverges. Distributed constants + CI-enforced uniqueness gives the same drift-resistance at zero coordination cost. This follows the same principle as ADR-0003's "each seeded system owns its stepIndex semantic" — single source of truth, CI-enforced.

### Decision 2: Mutually Exclusive Serialization Interfaces

**`IRunStateSerializable` and `IMasteryStateSerializable` are declared disjoint.** A class may not implement both.

```csharp
public interface IRunStateSerializable
{
    string SystemId { get; }
    int    SchemaVersion { get; }
    object ToDto();             // returns system-specific DTO
    void   FromDto(object dto); // consumes same shape
}

public interface IMasteryStateSerializable
{
    // same shape, different interface
}
```

**Enforcement:** C# has no language-level compile-time enforcement for interface disjointness. A **reflection-based unit test** (`InterfaceExclusion_test`) scans the gameplay assembly at CI time and fails the build if any type implements both interfaces, naming the offender.

**Rationale:** GDD R1 says a system's state is either run-scoped or cross-run-persistent — never both. Dividing this contract at the interface level (rather than by convention) makes misuse a compile- or CI-time failure, not a runtime corruption pattern discovered in production.

### Decision 3: Atomic Write — Background Task + Temp-in-`temporaryCachePath` + `File.Move` with `overwrite: true`

**The write sequence:**

1. Main thread enqueues write intent on a thread-safe queue.
2. Background writer thread dequeues, calls each system's `ToDto()` under a single `try`/`catch`. Any exception mid-assembly → abort write, preserve live `.sav`, log `SaveAssemblyFailedException`.
3. Background writer serializes the envelope to JSON bytes via Newtonsoft (canonical serialization: sorted property order, UTF-8, no indentation, no trailing whitespace).
4. Write bytes to `Application.temporaryCachePath/[filename].tmp` via `FileStream` with `FileMode.Create`, `FileShare.None`. Call `Flush(flushToDisk: true)` before closing.
5. Validate `.tmp` checksum by re-reading and recomputing.
6. Rotate `.bak` (delete `.bak`, rename `.sav` → `.bak`).
7. `File.Move(tmpPath, targetPath, overwrite: true)` — the atomic rename.
8. Background writer reports completion on a main-thread-pumped callback.

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

### Decision 5: Envelope Contract — Version + Checksum + System-Owned Payload

**Every DTO is wrapped in this envelope before serialization:**

```json
{
  "envelope_version": 1,
  "system":           "loot-reward",
  "schema_version":   1,
  "written_at":       "2026-04-24T14:32:01.123Z",
  "checksum":         "a3f5c8...",
  "payload":          { ... system-owned DTO ... }
}
```

**Field semantics:**

| Field | Type | Semantic |
|-------|------|----------|
| `envelope_version` | int | Reserved for envelope-level schema changes (e.g., adding a new field like `locale`). `=1` for all EA releases. Higher envelope_version than current code → treated as incompatible, skip candidate. |
| `system` | string | Matches the DTO's `SystemId` constant. Used for load-time routing to the correct `FromDto` handler. |
| `schema_version` | int | Matches the DTO's `SchemaVersion` constant. Schema mismatch handling per GDD R5. |
| `written_at` | string | ISO 8601 UTC, millisecond precision (`yyyy-MM-ddTHH:mm:ss.fffZ`). Owned by `SaveSystem.NowTimestamp()`. |
| `checksum` | string | SHA-256 hex over the canonical serialization of the envelope **excluding the `checksum` field itself**. Owned by `SaveSystem.ComputeEnvelopeChecksum()`. |
| `payload` | object | System-owned DTO. Save has no schema knowledge of its internals. |

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

### Decision 7: `RunSeed` Persisted in RunState Envelope

**`RunState.payload.run_seed: int` is the single authoritative RunSeed.**

- Established once at run-start by whatever subsystem creates the new run (Node Map generation per GDD C.3).
- Persisted in every RunState write.
- Loaded on resume; all subsequent seeded-call derivations (`RunSeed ^ nodeIndex` for Loot, `RunSeed ^ mapStepIndex` for Node Map regeneration if that ever occurs) recompute from this single field.
- **Scoped seeds are NEVER persisted** — only the root `RunSeed`. This is a direct application of ADR-0003 Rule 3: scoped seeds are derived on demand at each seeded entry point. Persisting them would create two truths and permit drift.

**Rationale:** ADR-0003's Validation Criterion for deterministic replay requires that a run can be suspended and resumed without changing any future RNG draw. Persisting only the root seed + the step indices already carried in gameplay state (current node, pity counters, traversal list) is sufficient to reconstruct every future draw exactly. This is the smallest serialization surface that preserves determinism.

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
    public string  system;
    public int     schema_version;
    public string  written_at;
    public string  checksum;
    public object  payload;
}
```

Note on commit and handler predicates: Save consults these before every write. If either returns `true`, the write is deferred until both return `false` (retrofit contract from GDD R2 / NE handler save-block / `INodeMapSerializable.IsCommitInProgress`). This keeps Save free of schema knowledge while honoring the per-system write gates.

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

- Declare DTO with `const string SystemId` + `const int SchemaVersion`.
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
- [ ] Every DTO class declares `const string SystemId` + `const int SchemaVersion`.
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

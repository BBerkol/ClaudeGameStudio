# TD Verdict — Slice 8b ADR-0004 Save Orchestrator Brief

**Date:** 2026-06-24
**Scope:** Pre-code verdict for Slice 8b (orchestrator + atomic write + load + recovery chain). Continuation of the Slice 8 / 8a contract+DTO chain.
**Files in scope:**
- `Assets/Scripts/Save/SaveSystem.cs` (orchestrator surface — currently helpers only)
- `Assets/Scripts/Save/Envelope.cs`
- `Assets/Scripts/Save/Dtos/NodeMapDto.cs` (first real DTO; consumer of registration)
- `Assets/Scripts/CombatView/RunSceneHost.cs` (registration + trigger site for 8b-3)
- `docs/architecture/adr-0004-save-persistence-architecture.md` (amendment target)

## Context

Slice 8 (`99235a5`) shipped helpers + interfaces; Slice 8a (`fd8107d`) shipped `NodeMapDto` as the first real DTO. Slice 8b's STATUS-block scope is "orchestrator (`EnqueueRunStateWrite` + `LoadRunState`) + atomic temp-then-rename write on background `Task` + per-category recovery chain (live → orphaned temp → N=1 backup)."

Zero call sites outside Save namespace today. RunSceneHost owns NodeMap but does not invoke any save API.

## Technical Director Review

TD-ARCHITECTURE: CONCERNS

Brief is solid and ADR-aligned. CONCERNS (not REJECT) because Q5 needs an amendment and Q7 needs ADR-0004 to be explicit before code lands.

### Q1 — Structural split: ACCEPT, three sub-slices

Split it. The proposed decomposition is the right cut. Each sub-slice has an independent failure surface AND test surface.

- **8b-1**: Registration + write path (queue, background Task, temp-then-rename, validate, retry). Tests: write-then-read-bytes round-trip, retry budget, atomic visibility.
- **8b-2**: `LoadRunState` recovery chain (live → orphaned tmp → bak). Tests: each rung promoted, schema-mismatch silent skip, all-fail returns null.
- **8b-3**: RunSceneHost triggers + `Application.quitting` sync + predicates. Tests: trigger fires on beacon resolve, predicate gates write, quitting path is sync.

Land 8b-1 first. It's the only sub-slice that can't be tested with a fake; 8b-2 and 8b-3 can both stub the writer.

### Q2 — Registration: AMEND, explicit-key registry from instance

`SaveSystem.RegisterRunStateSerializable(IRunStateSerializable)` — no explicit key arg; key is read from the instance's `SystemId` property (already on the interface). Duplicate `SystemId` throws.

- No hard-coded NodeMap binding (ADR-0011 bridge smell — correctly flagged).
- No bimodal "registered + hardcoded fallback."
- Symmetric with `RegisterMasteryStateSerializable` later.

Registration site for Slice 8b: `RunSceneHost.Awake()` registers the NodeMap serializable. Same lifecycle that owns the model — no service-locator gymnastics.

### Q3 — Background Task: AMEND, single long-lived consumer

Single `ConcurrentQueue<WriteIntent>` + one long-lived consumer `Task`. Reasons:

1. **Ordering**: ADR-0004's "last write wins per category" is trivially true with one consumer; with `Task.Run` per write you get races where an older write lands after a newer one.
2. **Coalescing**: One consumer can drop a queued write if a newer one for the same `SystemId` is already pending — free debounce for periodic-idle flush.
3. **Retry budget**: 5×exponential up to 4000ms means a stuck file lock could pin a thread for ~7.75s. With per-write `Task.Run`, ten queued writes during a stuck handle = 10 threads parked. One consumer = one thread parked, queue keeps absorbing.

Cost: one always-on thread. Acceptable — it spends 99.9% of its life on a `BlockingCollection` wait or `SemaphoreSlim`.

**ADR-0004 amendment required**: "background Task" → "single-consumer background Task with ConcurrentQueue".

### Q4 — File paths: ACCEPT with clarification

- `Application.persistentDataPath/saves/runstate.sav` + `runstate.sav.bak`
- `Application.temporaryCachePath/runstate.tmp`

Per-category (one file per RunState / MasteryState), composite payload inside. See Q7.

### Q5 — Test seam: AMEND, inject `ISaveStorage`

`SetSavesRoot(string)` is a bimodal toggle in disguise — ADR-0011 violation. `Application.persistentDataPath` is also untestable on CI batchmode without a writable HOME.

Introduce `ISaveStorage` from day one:

```csharp
internal interface ISaveStorage {
    string LiveDir { get; }    // persistentDataPath/saves
    string TempDir { get; }    // temporaryCachePath
    Stream OpenWrite(string path);
    Stream OpenRead(string path);
    void Move(string src, string dst, bool overwrite);
    bool Exists(string path);
    void Delete(string path);
}
```

Production: `UnitySaveStorage` wraps `File.*` + Unity paths. Tests: `InMemorySaveStorage` or `TempDirSaveStorage(Path.GetTempPath() + Guid)`. `SaveSystem` constructor takes `ISaveStorage`; default-constructed via internal factory in Unity init.

Not a bridge — injection seam (ADR-0011 exception: polymorphism via interface for real product/test divergence). Document in slice as "injection, not toggle."

### Q6 — Predicates: in 8b-3 only

Land with the trigger wiring, not before. They're meaningless without a write-trigger consumer. `RegisterCommitPredicate` gates "is it safe to flush right now"; `RegisterHandlerActivePredicate` gates "is a handler mid-decision" — both belong with RunSceneHost integration.

### Q7 — One file per category, composite payload: AMEND ADR-0004

Per-category, composite. Decision 4 ("per-category" recovery chain) is correct; Decision 3 is silent on layout.

Composite envelope:
```
runstate.sav (one envelope)
  payload: { "run.node_map": {...}, "run.deck": {...}, "run.scrap": {...} }
```

Reasons:
1. **Atomicity**: One `File.Move` = the whole run state advances atomically. With N files you can crash between writes and ship inconsistent state.
2. **Recovery semantics**: ADR's "live → tmp → bak" chain is one decision per category. N files = N independent chains = N×M corruption matrix.
3. **Checksum**: Envelope checksum covers the whole category. Per-DTO files mean per-DTO checksums and no cross-DTO integrity signal.
4. **Steam Cloud**: One file ≈ one sync unit. Cleaner conflict resolution.

Cost: one DTO schema bump rewrites the whole envelope. Acceptable — per-DTO `SCHEMA_VERSION` constants drive partial-skip on schema mismatch *inside* the envelope load, not at the file layer.

**ADR-0004 amendment required**: spell out composite-payload layout + per-DTO schema-mismatch handling inside the envelope.

## Pre-flight before 8b-1

1. ADR-0004 amendment doc covering Q3 (single-consumer) + Q5 (`ISaveStorage`) + Q7 (composite payload). One amendment file, three sections.
2. Capture file for Slice 8b-1 per capture-before-destroy (Save namespace is new code, low destroy risk, but the amendment itself "destroys" the previous ADR contract — capture it).
3. 8b-1 closes with EditMode green attestation including new write-path tests AND existing 542 baseline holding (excluding the one pre-existing ignore).

**Success metric:** 8b-1 lands with `ISaveStorage` injected and zero `Application.persistentDataPath` references outside `UnitySaveStorage`; 8b-2 lands with a recovery-chain test that exercises all three rungs using `InMemorySaveStorage`; 8b-3 lands with a trigger test that doesn't touch the filesystem at all.

## Decisions surfaced to user

Before 8b-1 code:
- Confirm the 3 sub-slice split (8b-1 write / 8b-2 load / 8b-3 triggers).
- Confirm ADR-0004 amendment scope (Q3 single-consumer + Q5 ISaveStorage + Q7 composite payload).
- Confirm `RunSceneHost.Awake()` as the NodeMap registration site.

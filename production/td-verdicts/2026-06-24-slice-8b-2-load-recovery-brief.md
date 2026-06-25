# TD Verdict — Slice 8b-2 ADR-0004 Load Recovery Chain Brief

**Date:** 2026-06-24
**Scope:** Pre-code verdict for Slice 8b-2 — `LoadRunState` / `LoadMasteryState` recovery chain (live `.sav` → orphaned `.tmp` → N=1 `.bak`), partial-skip on per-DTO schema mismatch, asymmetric exhaustion outcome surfacing.
**Files in scope:**
- `Assets/Scripts/Save/SaveSystem.Load.cs` (NEW partial)
- `Assets/Scripts/Save/SaveSystem.Write.cs` (no destructive edits expected)
- `Assets/Scripts/Save/Envelope.cs` (locked)
- `Assets/Scripts/Save/Dtos/NodeMapDto.cs` (consumer of `FromDto` dispatch — locked)
- `Assets/Tests/EditMode/Save/` (new load + recovery-chain tests; reuse `InMemorySaveStorage` + `StubRunSerializable` + `StubDto`)

## Technical Director Review

TD-ARCHITECTURE: CONCERNS

Brief is tight and ADR-aligned. The recommendation set is mostly correct; CONCERNS (not ACCEPT) because three items need an explicit decision before code: Q1 surface shape (telemetry side-channel vs structured result — pick one and lock it), Q6 (failed-tmp cleanup needs to be tied to the corruption logger so we don't silently drop forensic data), and one missing item the brief did not flag — schema-version stamping on `.bak` rotation (see Q10 "Anything missing").

### Q1 — Return shape: AMEND, your lean (c) — structured `LoadResult`, no static event

Adopt the struct (c). Reasoning:

- Static events for telemetry are a bridge smell — ADR-0011 #1 (adapter layer in disguise). Two callers asking "did load succeed?" via two different surfaces (return value + event subscription) is bimodal (#3). Same `LoadResult` informs view-layer and telemetry sink.
- `IReadOnlyList<SchemaMismatch> Mismatches` lets tests assert the exact partial-skip set without scraping logs.
- `string SourceRung` is overspecified as `string` — use `enum SourceRung { Live, OrphanedTemp, Backup }` so consumers can switch exhaustively.
- Surface shape:
  ```csharp
  public readonly struct LoadResult {
      public LoadOutcome Outcome { get; }                // Loaded | LoadedWithPartialSkip | Empty | Exhausted
      public SourceRung  Rung    { get; }                // Live | OrphanedTemp | Backup | None
      public IReadOnlyList<string>         SkippedSystemIds { get; }   // unregistered-on-load
      public IReadOnlyList<SchemaMismatch> Mismatches       { get; }   // expected/actual per system
  }
  ```
- `LoadedFromBackup` collapses into `Outcome == Loaded && Rung == Backup`. Splitting it into the outcome enum loses the orthogonality (a backup load can also be `LoadedWithPartialSkip`).

### Q2 — First-launch / empty-state: ACCEPT, your lean (b) — distinct `Empty`

`Empty` and `Exhausted` carry different intents (normal first-launch vs corruption) and different downstream consequences. Folding them is ADR-0011 #5 (compat overload) by another name. Decision rule for the load path:

- All three candidates (`.sav`, `.tmp`, `.bak`) absent → `Empty`. No telemetry log. View shows "New Run".
- One or more candidates present, all validation-failed → `Exhausted`. Logs `RunStateFullLoss` / `MasteryStateFullLoss` per asymmetric exhaustion. RunState non-blocking, Mastery surfaces the outcome for the 8b-3 dialog.

### Q3 — Sync vs async load: ACCEPT, your lean (a) — sync

Sync `LoadRunState() / LoadMasteryState()`. Reasoning:

- Envelope is ~7–14 KB per ADR-0004 budget (§Performance). Read + parse + checksum is sub-millisecond on disk.
- Load happens once at scene/game init; the loading screen owns the frame budget. An async surface would force callers to thread `Task` plumbing through view-layer bootstrap for no real benefit.
- Test surface stays clean — no `await`, no `IRunStateSerializable` rewrite for `FromDtoAsync`.
- Background consumer Task is **write-only**. Do not touch it from the load path.

### Q4 — Separate `LoadRunState()` / `LoadMasteryState()` methods: ACCEPT, your lean

Two methods. Asymmetric exhaustion has caller-visible semantic divergence (RunState `Exhausted` → silently start new run; Mastery `Exhausted` → caller must surface a dialog before continuing). A single `Load(SaveCategory)` forces every caller to switch on the category to decide policy, which is the bridge pattern.

The shared internal: `private static LoadResult LoadCategory(SaveCategory category)`. The public surface is two thin wrappers. ADR-0011 clean: not bimodal, not parallel storage — distinct caller contracts.

### Q5 — Orphaned-tmp filtering: ACCEPT, your lean (b) — same validation as `.sav`

Treat `.tmp` exactly like `.sav` for the purpose of rung validity: envelope parses + `envelope_version` accepted + `schema_version` accepted + checksum validates → rung wins. Only THEN is the promotion `Move(.tmp, .sav, overwrite: true)` performed.

This preserves the invariant that anything reachable as a load source has the same integrity guarantees as the live file. It is also what the write path already produces on its happy path (Step 4 of `DoWrite` validates the .tmp before promoting), so the load path mirrors the write path's truth table.

Note on `envelope_version`: any value greater than the code's `EnvelopeVersion` constant fails the rung silently (ADR-0004 Decision 5 forward-compatibility — older client meets newer file, falls back to next candidate). Equal values pass.

### Q6 — Failed-validation `.tmp` cleanup: AMEND — delete, log first

Delete the failed `.tmp` AFTER recording one `SaveTempCorrupted(category, reason)` telemetry log. Reasoning:

- ADR-0011 hygiene argues delete (these are crash artifacts, not authored data).
- BUT zero logging means a recurring crash pattern in EA gets silently swept every boot — we lose the corruption signal entirely. One log line per failed `.tmp` makes the telemetry meaningful without retaining the byte payload.
- Apply the same rule to `.sav` and `.bak` rungs: log-then-skip. Do NOT delete `.sav` or `.bak` on validation failure — those are authored disk locations the user (or Steam Cloud) may need for manual recovery. `.tmp` is the only ephemeral location.

### Q7 — Recovery-chain test surface: ACCEPT — `InMemorySaveStorage` is the right seam

Confirm `InMemorySaveStorage` is the test surface. Direct planting (Q9) keeps tests terse. Minimum matrix to land in 8b-2:

| Test | Live | Tmp | Bak | Expected `LoadResult` |
|------|------|-----|-----|----------------------|
| `live_valid_wins` | valid | absent | absent | `Loaded` / `Live` |
| `live_valid_ignores_tmp_and_bak` | valid | valid | valid | `Loaded` / `Live` (tmp NOT promoted) |
| `live_corrupt_promotes_tmp` | corrupt-checksum | valid | absent | `Loaded` / `OrphanedTemp` + .sav now holds tmp bytes |
| `live_absent_promotes_tmp` | absent | valid | absent | `Loaded` / `OrphanedTemp` |
| `live_corrupt_tmp_corrupt_falls_to_bak` | corrupt | corrupt | valid | `Loaded` / `Backup` |
| `all_absent_returns_empty` | absent | absent | absent | `Empty` / `None` |
| `all_corrupt_returns_exhausted_runstate` | corrupt | corrupt | corrupt | `Exhausted` (RunState — non-blocking) |
| `all_corrupt_returns_exhausted_mastery` | corrupt | corrupt | corrupt | `Exhausted` (Mastery — caller surfaces dialog in 8b-3) |
| `schema_mismatch_partial_skip` | valid envelope w/ one DTO at bad schema_version | absent | absent | `LoadedWithPartialSkip` + `Mismatches` has the bad SystemId; envelope `.bak` NOT consulted |
| `unregistered_entry_silent_skip` | valid envelope w/ entry whose SystemId is not registered | absent | absent | `Loaded` + `SkippedSystemIds` has the unregistered id; no error |
| `corrupt_tmp_is_deleted_after_failed_load` | corrupt | corrupt | valid | After load, `.tmp` no longer in storage |
| `envelope_version_too_high_skips_rung` | future envelope_version | absent | valid | `Loaded` / `Backup` |

10 named cases + 2 platform-correctness tests = manageable. The four cross-cutting axes (rung × corruption mode × category × partial-skip presence) are covered.

### Q8 — `.bak` rotation interaction after backup load: AMEND — telemetry, not suppression

Your lean is correct: do NOT add a "recovered" flag that suppresses the next rotation. That flag is ADR-0011 #6 (stub return — surface that exists only to mute a downstream side-effect) and bimodal-codepath in disguise.

The N=1 guarantee post-backup-load:
- Recovered state lives in memory.
- Next successful write produces fresh `.sav` (good).
- Rotation pushes the now-stale corrupted `.sav` into `.bak`.
- The OLD `.bak` (which we loaded from, the last known-good) is gone.

Yes, we hold a known-bad blob as the backup until the *next* write rotates again — that gives us one window of N=0-effective backup. Telemetry-worthy: emit `SaveBackupRecoveryRotationWindow(category)` exactly once at the moment the backup-load completes, on the same log channel as `SaveBackupRecovery`. Post-EA telemetry will tell us if this window meaningfully overlaps player crashes; if it does, raise N=2 (ADR-0004 already documents this as a tuning knob).

Do not suppress, do not delay rotation, do not write a "verified clean .bak" flag. Just observe.

### Q9 — Test seam: ACCEPT — plant directly via `InMemorySaveStorage`

Plant directly. Corruption injection through the write path requires hijacking serialization mid-flight, which is invasive and bridge-shaped. The load path's job is to accept arbitrary bytes at the storage interface; tests should drive arbitrary bytes at the storage interface.

Add one helper to `InMemorySaveStorage` for ergonomics:
```csharp
public void PlantForTests(string path, byte[] bytes) => _files[path] = (byte[])bytes.Clone();
```
Test fixture helpers compose well-formed and intentionally-corrupted envelopes via shared factories under `Assets/Tests/EditMode/Save/Fixtures/`. The well-formed factory should ride on `SerializeCanonical` + `ComputeEnvelopeChecksum` so a Q1 envelope-format change cannot silently drift the load tests' "valid" baseline.

### Q10 — What this brief is missing

Three things to surface to the user before code:

1. **`schema_version` envelope-vs-entry split.** Slice 8b-1's write path puts a `schema_version` on the envelope (currently `= EnvelopeVersion`, i.e. `1`) AND a `schema_version` on each composite-payload entry (the DTO's `SchemaVersion`). The brief's per-DTO partial-skip rule reads the entry-level value. Make the verdict explicit: **8b-2 ignores the envelope-level `schema_version` for partial-skip dispatch.** It exists for ADR-0004 Decision 5 future use (envelope spec evolution); load currently treats it like `envelope_version`. This needs a docstring note on the Load partial so a future reader does not re-derive the rule from the field name.

2. **Atomicity of payload deserialization on partial-skip.** Today's `DoWrite` builds payload as `SortedDictionary<string, object>`. On read, Newtonsoft will hand us a `JObject` or `Dictionary<string, JToken>` and we need to iterate it. Decision rule for 8b-2: iterate payload entries, for each entry look up `SystemId` in `_runRegistry`/`_masteryRegistry`, read the entry's `schema_version`, compare to `serializable.SchemaVersion`, on match call `FromDto(payload[id].ToObject(serializable.ToDto().GetType()))`. **Do NOT deserialize the entire payload upfront** — a bad entry would corrupt the whole load. Entry-by-entry deserialization is the partial-skip surface.

3. **Empty registry on load.** If `LoadRunState()` runs before any `RegisterRunStateSerializable(...)` call (test or production misordering), every entry in the envelope payload becomes an unregistered-skip. The current Q1 surface treats this as `Loaded` with all-skipped — silent, which is wrong. Add: **load with zero matching registered systems on a non-empty payload returns `Exhausted`, not `Loaded`**. Or, equivalently, throw `InvalidOperationException("LoadRunState called before any IRunStateSerializable was registered")`. My lean: throw — silent partial-skip of EVERYTHING is the kind of failure that hides for a week. ADR-0011 #6 (stub return).

## Pre-flight before 8b-2 code

1. Lock the `LoadResult` / `LoadOutcome` / `SourceRung` / `SchemaMismatch` types in code (immutable, no setters, value-equality) — these are the diagnostic surface every downstream telemetry consumer reads, so churn here is expensive.
2. Add `PlantForTests` helper to `InMemorySaveStorage` and a shared `Assets/Tests/EditMode/Save/Fixtures/EnvelopeFactory.cs` that produces canonical valid + intentionally-corrupted byte payloads. Tests assert against `LoadResult`; the fixture rides on `SerializeCanonical` + `ComputeEnvelopeChecksum` so write-format drift fails the load tests.
3. Decide Q10 #3 (empty registry on load): silent-skip-everything vs throw. My lean: throw.
4. ADR-0004 docstring note: envelope-level `schema_version` is reserved for envelope-spec evolution; entry-level `schema_version` is the partial-skip dispatch axis.
5. 8b-2 closes with EditMode green attestation: all 12 enumerated Q7 tests + the 568 baseline from 8b-1 holding. The "TD-ARCHITECTURE APPROVE requires green tests" memory rule applies — semantic green, not just compilation green.

## Decisions surfaced to user

Before 8b-2 code lands:

- **D1 — Confirm `LoadResult` struct over telemetry side-channel** (Q1: structured result is the single diagnostic surface; no static events).
- **D2 — Confirm `Empty` is a distinct outcome from `Exhausted`** (Q2).
- **D3 — Confirm sync `LoadRunState()` / `LoadMasteryState()`** (Q3 + Q4).
- **D4 — Confirm "delete failed `.tmp` after logging"** (Q6). `.sav` and `.bak` failed-validation are log-only, never deleted.
- **D5 — Confirm "no .bak suppression after backup-load"** (Q8). Emit `SaveBackupRecoveryRotationWindow` telemetry one-shot. Do not gate next rotation.
- **D6 — Pick policy for empty-registry-on-non-empty-payload** (Q10 #3). My lean: throw `InvalidOperationException`. Alternative: treat as `Exhausted`. Don't pick silent-skip.

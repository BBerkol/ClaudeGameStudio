# Capture — Slice 8b-2 Save Orchestrator Load Recovery Chain

**Date:** 2026-06-24
**Scope:** Land the `LoadRunState` / `LoadMasteryState` recovery chain: live `.sav` → orphaned `.tmp` → N=1 `.bak` rung walk, per-DTO partial-skip on entry-level schema mismatch, asymmetric exhaustion outcome surfacing, structured `LoadResult` diagnostic. Second sub-slice of the Slice 8b chain (8b-1 write [DONE 555/0/1] / **8b-2 load [this slice]** / 8b-3 triggers).
**Companion docs:** `production/td-verdicts/2026-06-24-slice-8b-2-load-recovery-brief.md` (full TD review, CONCERNS verdict; D1-D6 approved by user 2026-06-24).

## Final-game picture this slice serves

The crash-and-resume promise from ADR-0004 GDD R6 lives or dies on this slice. 8b-1 wrote a `.sav` to disk; this slice walks the recovery chain on next launch and returns either the recovered state (with diagnostic telemetry on which rung won), an empty result for first-launch, or the asymmetric-exhaustion outcome that 8b-3 will surface to the view layer. Per-DTO partial-skip lets the EA-window schema bumps (e.g., RunDeck v1 → v2 in a patch) survive without invalidating a player's whole run.

## What is being added (new code)

**Files added (new):**
- `Assets/Scripts/Save/SaveSystem.Load.cs` (partial — public `LoadRunState` / `LoadMasteryState` + shared internal `LoadCategory` + rung walk + envelope validation + entry-by-entry partial dispatch)
- `Assets/Scripts/Save/LoadResult.cs` (`readonly struct LoadResult` + `enum LoadOutcome { Loaded, LoadedWithPartialSkip, Empty, Exhausted }` + `enum SourceRung { Live, OrphanedTemp, Backup, None }` + `readonly struct SchemaMismatch`)
- `Assets/Tests/EditMode/Save/Fixtures/EnvelopeFactory.cs` (rides on `SerializeCanonical` + `ComputeEnvelopeChecksum` so write-format drift fails load tests; emits well-formed envelopes + intentionally-corrupted variants)
- `Assets/Tests/EditMode/Save/SaveSystem_LoadRecoveryChain_test.cs` (8 rung-selection + envelope-version + tmp-cleanup tests)
- `Assets/Tests/EditMode/Save/SaveSystem_LoadOutcomes_test.cs` (4 tests: empty, exhausted RunState, exhausted Mastery, empty-registry throw)
- `Assets/Tests/EditMode/Save/SaveSystem_LoadPartialSkip_test.cs` (2 tests: schema-mismatch partial-skip, unregistered-entry silent-skip)

**Files modified:**
- `Assets/Tests/EditMode/Save/InMemorySaveStorage.cs` (add `PlantForTests(string path, byte[] bytes)` helper for direct corruption injection per TD verdict Q9)

Code summary by component:

- **`LoadResult` value type** — readonly struct, value-equality, no setters. `Outcome` + `Rung` + `SkippedSystemIds` (entries present in payload whose `SystemId` is not registered) + `Mismatches` (entries whose entry-level `schema_version` ≠ registered `SchemaVersion`). Single diagnostic surface per TD D1 — no static-event side-channel (ADR-0011 #1 bridge avoided).
- **`SourceRung` enum** — `Live` / `OrphanedTemp` / `Backup` / `None`. Exhaustive switch surface for consumers.
- **`LoadOutcome` enum** — `Loaded` / `LoadedWithPartialSkip` / `Empty` / `Exhausted`. `Empty` vs `Exhausted` is a deliberate split per TD D2 (normal first-launch vs corruption-across-all-rungs).
- **`SaveSystem.LoadRunState()` / `SaveSystem.LoadMasteryState()`** — sync per TD D3. Each routes to shared internal `LoadCategory(SaveCategory)`. Two public methods (not a single `Load(SaveCategory)`) per TD D4 — asymmetric exhaustion has caller-visible semantic divergence.
- **`LoadCategory(SaveCategory)`** — walks rungs in ADR-0004 Decision 4 order: `.sav` → `.tmp` → `.bak`. Each candidate runs the validation gauntlet (envelope parses → `envelope_version` accepted → checksum validates → at least one entry dispatches). Orphaned-tmp promotion via `Move(tmp, sav, overwrite: true)` only after validation passes (TD Q5). Failed `.tmp` is deleted after a `SaveTempCorrupted` log per TD D4; failed `.sav` / `.bak` are log-only (never deleted — authored locations).
- **Entry-by-entry deserialization** per TD Q10 #2 — payload is read as `Dictionary<string, JToken>` and each entry deserializes independently against the matching registered serializable. A bad entry corrupts only itself; siblings hydrate.
- **Empty-registry guard** — `LoadCategory` with zero registered serializables for the category but a non-empty payload throws `InvalidOperationException` per TD D6 (silent-skip-everything is ADR-0011 #6 stub-return).
- **`SaveBackupRecoveryRotationWindow(category)` telemetry** — emitted exactly once at the moment a backup-rung load completes, per TD D5. No suppression flag on next rotation (ADR-0011 #6 avoided); the N=0-effective window is observed via telemetry, not gated.
- **`InMemorySaveStorage.PlantForTests(path, bytes)`** — single-line helper. Defensive `byte[].Clone()` so caller mutations don't bleed into stored state.
- **`EnvelopeFactory`** — `BuildValid(SaveCategory, IEnumerable<(string systemId, int schemaVersion, object dto)>)` + `BuildWithCorruptChecksum(...)` + `BuildWithEnvelopeVersion(int)` + `BuildWithEntryAtSchemaVersion(string systemId, int wrongVersion)` + raw `BuildRandomBytes(int len)` for non-parsable garbage. Rides on `SaveSystem.SerializeCanonical` + `ComputeEnvelopeChecksum` so the write-format-drift detector test still fails if either drifts.

## What is being destroyed (authored values + locked fixtures)

**Nothing authored is destroyed.** Slice 8b-2 is additive — `SaveSystem.Load.cs` is a new partial, `LoadResult.cs` is a new file, the test layer adds files, and `InMemorySaveStorage` gets a single additive helper method. The existing 555-test baseline holds unchanged.

The only edit on a previously-banked file is the `InMemorySaveStorage.PlantForTests` additive method — no behavior change to existing storage operations, no signature changes on existing methods. Test stubs are not authored content; the capture-before-destroy rule does not gate this edit.

## Validation plan

EditMode green attestation: 555 baseline holds + 14 new tests green = **569 expected**.

12-row recovery-chain matrix from the TD verdict Q7 table, plus the 2 partial-skip cases, planted directly via `InMemorySaveStorage.PlantForTests`:

| # | Test | Live | Tmp | Bak | Expected |
|---|------|------|-----|-----|----------|
| 1 | `Live_valid_wins` | valid | absent | absent | `Loaded` / `Live` |
| 2 | `Live_valid_ignores_tmp_and_bak` | valid | valid | valid | `Loaded` / `Live` (tmp NOT promoted) |
| 3 | `Live_corrupt_promotes_tmp` | corrupt-checksum | valid | absent | `Loaded` / `OrphanedTemp` + `.sav` now holds tmp bytes |
| 4 | `Live_absent_promotes_tmp` | absent | valid | absent | `Loaded` / `OrphanedTemp` |
| 5 | `Live_corrupt_tmp_corrupt_falls_to_bak` | corrupt | corrupt | valid | `Loaded` / `Backup` |
| 6 | `Corrupt_tmp_is_deleted_after_failed_load` | corrupt | corrupt | valid | After load, `.tmp` no longer in storage |
| 7 | `Envelope_version_too_high_skips_rung` | future envelope_version | absent | valid | `Loaded` / `Backup` |
| 8 | `Backup_load_emits_rotation_window_telemetry_one_shot` | corrupt | absent | valid | `SaveBackupRecoveryRotationWindow` emitted exactly once |
| 9 | `All_absent_returns_empty` | absent | absent | absent | `Empty` / `None` |
| 10 | `All_corrupt_returns_exhausted_runstate` | corrupt | corrupt | corrupt | `Exhausted` |
| 11 | `All_corrupt_returns_exhausted_mastery` | corrupt | corrupt | corrupt | `Exhausted` (Mastery surfaces dialog in 8b-3) |
| 12 | `Empty_registry_on_non_empty_payload_throws` | valid envelope | absent | absent | `InvalidOperationException` (no registrations) |
| 13 | `Schema_mismatch_partial_skip` | valid envelope, one entry at bad `schema_version` | absent | absent | `LoadedWithPartialSkip` + `Mismatches` carries the bad `SystemId`; `.bak` NOT consulted |
| 14 | `Unregistered_entry_silent_skip` | valid envelope, one entry whose `SystemId` is not registered | absent | absent | `Loaded` + `SkippedSystemIds` carries the unregistered id; no error |

## Decisions ratified by user 2026-06-24

- **D1** — Structured `LoadResult` struct; no static-event telemetry side-channel.
- **D2** — `Empty` distinct from `Exhausted`.
- **D3** — Sync `LoadRunState()` / `LoadMasteryState()`.
- **D4** — Failed `.tmp` cleanup (log `SaveTempCorrupted`, then delete). `.sav` / `.bak` failed-validation are log-only.
- **D5** — No `.bak` rotation suppression after backup-load; emit one-shot `SaveBackupRecoveryRotationWindow` telemetry.
- **D6** — Empty registry + non-empty payload throws `InvalidOperationException`.

## Technical Director Review

See `production/td-verdicts/2026-06-24-slice-8b-2-load-recovery-brief.md` — TD-ARCHITECTURE CONCERNS, all six decisions (D1-D6) ratified by user; Q1-Q10 covered. Approval gate: 569 EditMode tests green and no compile errors on the Save asmdef + test asmdef.

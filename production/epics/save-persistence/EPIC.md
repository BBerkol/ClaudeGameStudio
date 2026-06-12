# Epic: Save & Persistence

> **Layer**: Foundation
> **GDD**: design/gdd/save-persistence.md
> **Architecture Module**: `WastelandRun.Save` — SaveManager passive orchestrator, atomic write pipeline, per-system DTO registry, N=1 backup chain, recovery policy
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories save-persistence`

## Overview

Implements the `SaveManager` as a passive orchestrator over per-system DTOs. Each gameplay system owns its own serialization contract: DTOs declare `public const string SystemId` and `public const int SchemaVersion` and are registered with `SaveManager`, which orchestrates when and where to write without owning any schema knowledge. Writes are atomic via a temp-then-rename pipeline (assemble DTOs → serialize → write `.tmp` → validate SHA-256 checksum → rotate `.bak` → `File.Move`) on a background `Task`; only `Application.quitting` and launch-time recovery run synchronously. The save envelope carries `envelope_version`, `system`, `schema_version`, `written_at` (ISO 8601 UTC), `checksum`, and `payload`. N=1 rolling backup provides one `.sav.bak` per category. The recovery chain is independent per category: live `.sav` → orphaned `.tmp` in `temporaryCachePath` → `.sav.bak` → full-loss handling. Exhaustion policy is asymmetric: RunState full loss shows New Run only (non-blocking); MasteryState full loss shows a blocking dialog. A `HandlerActive` save-block gate defers all writes while a Node Encounter handler is in flight; `INodeMapSerializable.IsCommitInProgress` defers periodic flushes during Node Map commit pipeline. This epic delivers the non-negotiable infrastructure contract — every other system's work is only as safe as this pipeline.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0004: Save & Persistence Architecture | Passive-orchestrator pattern; distributed schema registry (`SystemId` + `SchemaVersion` constants per DTO); atomic temp-then-rename with SHA-256; N=1 backup; per-category independent recovery; asymmetric exhaustion policy; Newtonsoft.Json + `link.xml`; background `Task` with `Application.quitting` drain | MEDIUM |
| ADR-0008: Addressables Runtime Asset Loading | Relevant for TR-save-025: broken Addressables key on load — Save returns stored keys faithfully; loading systems apply their own fallback | HIGH (pending Acceptance) |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-save-001 | RunState and MasteryState mutually exclusive via reflection-gated unit test; no type implements both interfaces | ADR-0004 ✅ |
| TR-save-002 | RunState captures vehicle loadout, deck, Scrap, map position, per-node flags, run-scoped counters | ADR-0004 ✅ |
| TR-save-003 | MasteryState persists chassis mastery XP, level, unlocked content flags across runs | ADR-0004 ✅ |
| TR-save-004 | RunState written after node resolution, every 30s idle on map (capped 30 flushes per dwell), on clean quit | ADR-0004 ✅ |
| TR-save-005 | MasteryState written once per combat after reward screen closes; non-combat nodes do not trigger write | ADR-0004 ✅ |
| TR-save-006 | All writes execute on background Task; only Application.quitting and launch-time recovery run synchronously | ADR-0004 ✅ |
| TR-save-007 | Atomic write sequence: assemble DTOs, serialize, write .tmp, validate checksum, rotate .bak, File.Move | ADR-0004 ✅ |
| TR-save-008 | Serialization uses Newtonsoft.Json with `link.xml` preservation; SHA-256 checksum over canonical envelope | ADR-0004 ✅ |
| TR-save-009 | Temp files live in `temporaryCachePath` not `saves/`; Steam Cloud include-path allowlist never reaches temp | ADR-0004 ✅ |
| TR-save-010 | Windows sharing violations retry 5 times exponential backoff (250ms to 4s = 7.75s total) | ADR-0004 ✅ |
| TR-save-011 | N=1 rolling backup: live `.sav`, single `.sav.bak`; rotation deletes old `.bak`, renames `.sav`, moves `.tmp` | ADR-0004 ✅ |
| TR-save-012 | Envelope contains envelope_version (1), system, schema_version, written_at (ISO 8601 UTC), checksum, payload | ADR-0004 ✅ |
| TR-save-013 | EA policy: any schema_version mismatch (higher or lower) is incompatible; higher envelope_version rejects | ADR-0004 ✅ |
| TR-save-014 | Recovery chain (priority): .sav, orphaned .tmp in temporaryCachePath, .sav.bak, full loss handling | ADR-0004 ✅ |
| TR-save-015 | RunState full loss shows New Run only; MasteryState full loss shows blocking dialog with recovery options | ADR-0004 ✅ |
| TR-save-016 | Mid-combat crash resumes at combat start (vehicle, deck, Scrap intact); soft-undo is intentional design | ADR-0004 ✅ |
| TR-save-017 | Handler save-block gates all writes when `NodeEncounter.HandlerActive` true; auto-save on transition to false | ADR-0004 ✅ |
| TR-save-018 | `INodeMapSerializable.IsCommitInProgress` defers all writes (including periodic flush) until false | ADR-0004 ✅ |
| TR-save-019 | Run completion: write MasteryState, delete runstate.sav/.bak (order critical); crash triggers run-end detection | ADR-0004 ✅ |
| TR-save-020 | Save location: `persistentDataPath/saves/` for live+backup; CI verifies ProjectSettings immutable post-EA | ADR-0004 ✅ |
| TR-save-021 | F1: RunState writes = NodeCount + PeriodicFlushes + QuitWrites; typical 16–33 per run | ADR-0004 ✅ |
| TR-save-022 | F3: Storage budget 3–6KB runstate.sav + 0.5–1KB masterystate.sav + N=1 backups + transient .tmp | ADR-0004 ✅ |
| TR-save-023 | ScrapStateDTO, LootStateDTO, NodeEncounterStateDTO own schema_version; each system defines its own DTO | ADR-0004 ✅ |
| TR-save-024 | Disk full during write: IOException caught on background thread, logs error, preserves .sav/.bak | ADR-0004 ✅ |
| TR-save-025 | Broken Addressables key on load: Save returns stored keys faithfully; loading systems apply fallback | ADR-0008 ⚠️ partial — behavior fully specified in GDD; ADR-0008 covers Addressables error-handling contract but is Proposed. Story can proceed against GDD spec; verify against ADR-0008 once Accepted. |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/save-persistence.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- Atomic write pipeline verified: crash-kill during `.tmp` write leaves `.sav`+`.bak` intact (integration test)
- Recovery chain integration test: corrupt `.sav` → falls through to `.bak` → loads successfully
- RunState/MasteryState mutual-exclusivity confirmed via reflection-gated unit test (TR-save-001)
- Background `Task` write path: no `UnityEngine.MainThreadDispatcher` calls within write pipeline
- `Application.quitting` drain completes within timeout bound (no hang on clean quit)
- `File.Move(src, dst, overwrite:true)` atomicity verified on Windows (OQ5 from ADR-0004)
- `FlushFileBuffers` P/Invoke behavior confirmed on Unity 6.3 Mono runtime (OQ4 from ADR-0004)
- `HandlerActive` save-block: automated test confirms no write fires while flag is true
- `link.xml` preservation: IL2CPP build does not strip Newtonsoft.Json DTO types

## Open Architecture Questions (from ADR-0004)

These must be resolved at first save-code commit — not pre-implementation blockers, but verification checkpoints:

- **OQ4**: `FlushFileBuffers` behaviour on Unity 6.3 Mono runtime — verify; P/Invoke fallback identified in ADR
- **OQ5**: `File.Move(src, dst, overwrite:true)` atomicity on Unity 6.3 Mono — verify; fallback is `File.Replace` on Windows, copy+delete elsewhere

## Next Step

Run `/create-stories save-persistence` to break this epic into implementable stories.

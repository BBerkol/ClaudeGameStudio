# Slice 8a — `NodeMapDto` landing (first real DTO under ADR-0004)

**Date:** 2026-06-24
**Author:** Claude (Opus 4.7)
**Slice:** 8a — first real DTO under `WastelandRun.Save.Dtos`
**Predecessor:** Slice 8 (`99235a5` — ADR-0004 envelope + disjoint interfaces + canonical helpers)
**Trigger:** New system ≥50 lines (new asmdef sub-namespace `WastelandRun.Save.Dtos`, new public type `NodeMapDto`, new reconstruction ctor on `NodeMap`, new link.xml).

## What this slice does

Lands the first real DTO under the ADR-0004 contract surface that Slice 8 just closed. `NodeMapDto` serializes/deserializes the run's beacon graph state. The two reflection-based CI gates (`SchemaRegistry_Unique_test`, `InterfaceExclusion_test`) — which passed trivially at Slice 8 with zero DTOs in scope — flip from "no candidates" to "one candidate" at this slice. That is the load-bearing visible win: ADR-0004's distributed schema registry guard becomes active.

## Files at risk

| File | Operation | Notes |
|---|---|---|
| `Assets/Scripts/Save/Dtos/NodeMapDto.cs` | **NEW** | First DTO. Implements `IRunStateSerializable`. Carries full graph payload per TD verdict (rejects regenerate-from-seed). |
| `Assets/Scripts/Save/Dtos/link.xml` | **NEW** | IL2CPP preservation for `WastelandRun.Save.Dtos` namespace only. |
| `Assets/Scripts/Run/NodeMap.cs` | **MODIFY** | (a) Add new public reconstruction ctor that enforces the same ADR-0011 guards as `FromBiomeGraph`. (b) Update the outdated Slice 7b docstring (lines 14-18 + 76-77) that says the graph is "regenerated on load" — TD verdict reverses this. |
| `Assets/Tests/EditMode/Save/NodeMapDto_round_trip_test.cs` | **NEW** | Round-trip + reconstruction equivalence + envelope-checksum verify. |
| `Assets/Tests/EditMode/Save/NodeMapDto_wire_format_test.cs` | **NEW** | Locked JSON-literal fixture asserting on-disk shape. Once a save exists in the wild, this is contract. |
| `docs/architecture/adr-0004-save-persistence-architecture.md` | **MODIFY** | Amendment locking the SystemId naming convention (`"category.identifier"` dotted-snake — `run.node_map` is the first). All future DTOs inherit. |

## What is being destroyed (Capture-before-destroy register)

**Nothing authored is being destroyed.** Greenfield DTO landing. The one modified-existing edit is on `NodeMap.cs`:

1. **Docstring lines 14-18** (current text reads "Per ADR-0004 the persistence shape is `BiomeId + RunSeed + CurrentIndex + PathHistory[]` regenerated on load — the graph itself is never persisted (determinism guarantees the rebuild).") will be replaced with the new TD-approved persistence stance: the full graph payload is carried in `NodeMapDto`. Reason: covert version-coupling risk (forbidden pattern #4/#7 if generator semantics drift, which ADR-0015 explicitly anticipates).
2. **Docstring lines 76-77** (same regenerate-from-seed assumption on `PathHistory`) — replaced with the same updated stance.

No designer-authored values are touched. No SO assets are modified. No prefabs touched.

## Defers (ADR-0011-clean)

| Item | Reason it lights up later, not now |
|---|---|
| `NodeMapDto.FromDto(object dto)` strict-type validation message wording | Slice 8b orchestrator dispatches by SystemId before calling — the defense-in-depth message is added but the orchestrator-side test for it lives in 8b. |
| Full `RunStateDto` composite (NodeMapDto + RunDeckDto + VehicleDto + ScrapEconomyDto + RunCoreDto) | Each DTO is its own slice (8a, 8a-2, 8a-3, …). Coupling them here would build a composite before the parts exist (ADR-0011 #6 transitional artifact). |
| `SaveSystem.LoadFromDisk` / `EnqueueRunStateWrite` orchestrator | Slice 8b. The contract surface (Envelope + ComputeEnvelopeChecksum) already exists; what 8b adds is the dispatch + I/O. |
| Per-category recovery chain (live → orphaned temp → N=1 backup) | Slice 8b — depends on file I/O. |
| `MasteryStateDto` placeholder | Slice 8c — `IMasteryStateSerializable` lights up the moment the first mastery-state DTO exists. |
| `Save.Dtos` namespace existing before any DTO | Closed at this slice — first appearance is `NodeMapDto.cs`. Sub-namespace creation IS the explicit signal that DTOs are landing (avoids forbidden pattern #6). |

## ADR-0011 trap watches

- **#4 (vestigial enum):** TD flagged this as the reason to carry the full graph payload (not regenerate from seed). The save's correctness must not depend on `BiomeWebGenerator` / `BiomeDistributionSO` / `BiomeGraph` semantics staying stable — that coupling silently mutates loaded runs the moment ADR-0015 lands a new biome distribution.
- **#5 (compat overload):** `NodeMap` already has `FromBiomeGraph(BiomeGraph, int runSeed, BeaconType, bool, string)`. The new reconstruction ctor MUST be a different signature shape (no parameter overlap rabbit hole). Plan: `new NodeMap(string biomeId, IReadOnlyList<BeaconData> beacons, IReadOnlyList<NormalizedPosition> positions, IReadOnlyList<(int, int)> edges, BeaconType terminalType, int currentIndex, IReadOnlyList<int> pathHistory, int mapSeed, bool allowBidirectional)`. Different parameter shape (no `BiomeGraph` argument), one public ctor signature on `NodeMap`.
- **#6 (transitional artifact):** `Save.Dtos` sub-namespace lands WITH the DTO file, not before. `link.xml` lands at this slice — not earlier as a placeholder. No "reserved for future DTOs" comments anywhere.
- **#3 (bimodal path):** Reconstruction ctor enforces the same ADR-0011 BiomeId guard (throw on null/empty). The validation is duplicated between `FromBiomeGraph` callers and the reconstruction ctor — that's parallel storage (#2)? **No** — both call paths go through the new ctor, so the guard exists once. `FromBiomeGraph` internally invokes the new ctor.

## TD verdict resolutions

- **DTO shape:** Full graph payload (nodes + positions + edges + AllowBidirectional + TerminalType + MapSeed + per-beacon EnemyArchetype + per-beacon IsResolved + CurrentIndex + PathHistory + BiomeId). Reject "regenerable from RunSeed." BeaconType serializes as string name.
- **Placement:** Option B — `NodeMapDto` implements `IRunStateSerializable`. `NodeMap` gains one new public reconstruction ctor; existing `FromBiomeGraph` is refactored to internally call it. The reconstruction ctor enforces the same ADR-0011 guards.
- **Tests:** 6 EditMode tests green at completion: 4 existing (Slice 8) flip from trivial to non-trivial; 2 new (`NodeMapDto_round_trip_test` + `NodeMapDto_wire_format_test`).
- **link.xml:** Preserve `WastelandRun.Save.Dtos` namespace only. Don't preserve `WastelandRun.Save` (Envelope is reachable through normal IL2CPP code paths; preserving it would be belt-and-suspenders ≈ ADR-0011 #6).
- **SystemId:** `"run.node_map"` dotted-snake hierarchical. Category prefix (`run` / `mastery`) encodes the asymmetric-exhaustion split in the wire value. Locked as ADR-0004 amendment at this slice.
- **Sub-namespace timing:** Lands with the DTO file.
- **Pre-flight (B1 chosen):** Const fields are source of truth; instance properties on `IRunStateSerializable` return the const. Final const naming landed as **UPPER_SNAKE_CASE** per `.claude/docs/technical-preferences.md` constants convention (`public const string SYSTEM_ID = "run.node_map"; public string SystemId => SYSTEM_ID;`) rather than the `SystemId_Const` working sketch — chosen to avoid PascalCase collision with the interface property and to match the existing project-wide const style. `SchemaRegistry_Unique_test` updated to scan for the UPPER_SNAKE_CASE names.
- **Net new on SaveSystem:** `public static string SerializeCanonical(object payload)` helper exposed so the wire-format lockdown test asserts the actual on-disk shape produced by SaveSystem's private canonical resolver (a duplicated test-local resolver would silently drift from prod). One-line wrapper over the existing `_canonicalSettings` instance; Slice 8b's `EnqueueRunStateWrite` will also consume it for the write path.

## Test commitments (BLOCKING)

All six must be green before this slice can close:

1. `ComputeEnvelopeChecksum_fixture_test` (Slice 8 — must remain green; locked hex shouldn't be touched).
2. `NowTimestamp_format_test` (Slice 8 — must remain green).
3. `SchemaRegistry_Unique_test` (now flips from trivial to non-trivial — `NodeMapDto` is the first candidate).
4. `InterfaceExclusion_test` (now flips from trivial to non-trivial — `NodeMapDto` is the first candidate; must NOT also implement `IMasteryStateSerializable`).
5. **NEW** `NodeMapDto_round_trip_test` — ToDto+FromDto reconstructs equivalent NodeMap (deep equality on BiomeId, CurrentIndex, PathHistory, AllowBidirectional, all beacons, all positions, all edges, all per-beacon EnemyArchetype + IsResolved).
6. **NEW** `NodeMapDto_wire_format_test` — locked JSON-literal fixture asserts on-disk shape under `CanonicalContractResolver` (ordinal-sorted field names, BeaconType as string name not ordinal, EnemyArchetype as nullable, position fields as floats).

## Technical Director Review

Verdict is APPROVE with two non-blocking concerns called out in (1) and (3). Slice it as proposed, but resolve the graph-payload question before authoring and add one test that the proposed set misses.

**1. DTO shape — carry the full graph payload. Do NOT regenerate from seed.**

`NodeMapDto` carries `BiomeId`, `CurrentIndex`, `PathHistory[]`, **and the full graph** (nodes: `{index, position.x, position.y, beaconType}[]`; edges: `{from, to}[]`, plus `AllowBidirectional` flag).

Rationale (ADR-0011 lens):
- "Regenerable from `RunSeed ^ stepIndex`" is a covert version-coupling. The save's correctness depends on `BiomeWebGenerator` + `BiomeDistributionSO` + `BiomeGraph` semantics never drifting across the save's lifetime. Any future generator tweak (and ADR-0015 explicitly anticipates more biome distributions landing as vertical slices) silently mutates loaded runs. That is forbidden pattern #4 (vestigial enum) and #7 (transitional comment) waiting to happen.
- ADR-0004's distributed schema registry was designed precisely so each system's on-disk shape is self-contained and versioned.
- Disk cost is negligible. Biome 1 has 20-40 nodes; full payload is well under 4 KB serialized.
- The deterministic-RNG concern (ADR-0003) is the opposite direction: `RunSeed` is persisted so *future* generation is reproducible, not as a license to recompute *past* generation.

Non-blocking: confirm BeaconType serializes as string name, not ordinal (ordinal = forbidden pattern #4 the moment we add an enum value).

**2. ToDto/FromDto placement — Option B.**

`NodeMapDto` implements `IRunStateSerializable`. Static `NodeMapDto.From(NodeMap)` builder + instance `NodeMapDto.ToNodeMap()` reconstructor. `NodeMap` gains one new public ctor that enforces the same ADR-0011 guards.

Option A rejected: `NodeMap.FromDto` would mutate `BiomeId` post-construction, bypassing the ctor guard (vestigial guard = #4) or duplicating validation (#2).
Option C rejected: separate serializer adapter is premature at one-DTO scale (#6).

`FromDto(object dto)` on the DTO rejects if `dto` is not a `NodeMapDto` — throw `InvalidCastException` with SystemId in message. Orchestrator will route by SystemId before calling; defense-in-depth is cheap.

**3. Test set — your list (i)-(iv) plus one addition.**

Add: **payload-shape lockdown test**. Serialize a fixture `NodeMapDto` to JSON and assert the on-disk shape (field names, ordering under `CanonicalContractResolver`, nested structure) matches a locked string fixture. Analogous to `ComputeEnvelopeChecksum_fixture_test`. Once a real save exists in the wild, wire format is contract; locking it at slice 8a means a future refactor that accidentally renames `path_history` → `pathHistory` fails CI rather than silently invalidating saves.

Non-blocking concern: Slice 8a APPROVE-on-completion requires explicit EditMode-green attestation across all six tests, not "compiles + the new test passes." The trivially-passing tests can break in non-obvious ways once they have real types in scope.

**4. link.xml granularity — preserve `WastelandRun.Save.Dtos` only.**

Don't preserve `WastelandRun.Save` itself. `Envelope` is constructed by `SaveSystem` directly; preserving it would be belt-and-suspenders (#6 mindset). DTOs are constructed via `Activator.CreateInstance` / Newtonsoft reflection during `DeserializeObject<NodeMapDto>` — field-level stripping is the real risk.

**5. SystemId naming — `"run.node_map"` dotted-snake hierarchical.**

First segment is asymmetric-exhaustion category (`run` / `mastery`); second is snake_case identifier. Encodes the recovery-path split in the wire value (corrupted-save diagnostics human-readable). Flat snake loses the signal; PascalCase couples to C# type name (rename breaks SystemId = #4 OR keeps stale string = #7). Future DTOs: `run.player_vehicle`, `run.run_deck`, `run.run_state`, `mastery.unlocks`. Lock as ADR-0004 amendment at this slice.

**6. Sub-namespace timing — confirmed, lands with the DTO file.**

`WastelandRun.Save.Dtos` first appears in `NodeMapDto.cs`. No empty folder, no placeholder, no "reserved" comment. ADR-0011 compliant.

**Cross-cutting concern (resolved B1):**

The marker interfaces expose `string SystemId` / `int SchemaVersion` as instance members. The `SchemaRegistry_Unique_test` scans for `const` fields. Resolution: const fields ARE source of truth; instance properties return the const. Final shipping form is UPPER_SNAKE_CASE consts (`SYSTEM_ID` / `SCHEMA_VERSION`) per project constants convention — avoids PascalCase collision with the instance properties and matches existing project style. Registry test updated to scan UPPER_SNAKE_CASE names.

**Slicing recommendation:** 8a = NodeMapDto + ToDto/FromDto + 6 tests green. 8b = orchestrator + atomic write + LoadFromDisk + recovery chain. Resolve interface-vs-const gap (B1) before authoring NodeMapDto.

Relevant absolute paths:
- `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\Assets\Scripts\Save\Envelope.cs`
- `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\Assets\Scripts\Save\IRunStateSerializable.cs`
- `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\Assets\Scripts\Save\IMasteryStateSerializable.cs`
- `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\Assets\Scripts\Save\SaveSystem.cs`
- `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\Assets\Scripts\Run\NodeMap.cs`
- `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\Assets\Tests\EditMode\Save\` (new: `NodeMapDto_round_trip_test.cs`, `NodeMapDto_wire_format_test.cs`)
- New file: `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\Assets\Scripts\Save\Dtos\NodeMapDto.cs`

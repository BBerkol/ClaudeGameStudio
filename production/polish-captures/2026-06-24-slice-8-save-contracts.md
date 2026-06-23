# 2026-06-24 — Slice 8: ADR-0004 first landing (envelope + contracts)

## What's being created (greenfield — nothing destroyed)

This is the first landing of ADR-0004 Save & Persistence. No prior `Assets/Scripts/Save/` exists. No Newtonsoft package installed. No `link.xml`. No DTO classes. **No authored values are being destroyed by this slice.** The capture file exists to satisfy the capture-before-destroy hook on "new system ≥50 lines" and to pin the TD verdict for the next iteration to audit against.

**Surface added:**

| File | Purpose |
|---|---|
| `Assets/Scripts/Save/WastelandRun.Save.asmdef` | New assembly, POCO + Newtonsoft only, no engine deps. |
| `Assets/Scripts/Save/Envelope.cs` | Decision-5 envelope POCO, snake_case fields matching the JSON shape locked by ADR-0004. |
| `Assets/Scripts/Save/IRunStateSerializable.cs` | Per-system serialization contract (Decision 2). |
| `Assets/Scripts/Save/IMasteryStateSerializable.cs` | Disjoint sibling contract (Decision 2). |
| `Assets/Scripts/Save/SaveSystem.cs` | Static helpers — `ComputeEnvelopeChecksum`, `NowTimestamp`. **No** `Enqueue*`/`Load*`/predicates. |
| `Packages/manifest.json` | Adds `com.unity.nuget.newtonsoft-json`. |
| `Assets/Tests/EditMode/Save/WastelandRun.Save.Tests.asmdef` | New test assembly referencing `WastelandRun.Save` + Newtonsoft + NUnit. |
| `Assets/Tests/EditMode/Save/SchemaRegistry_Unique_test.cs` | Reflection scan; passes at zero implementors. |
| `Assets/Tests/EditMode/Save/InterfaceExclusion_test.cs` | Reflection scan; passes at zero implementors. |
| `Assets/Tests/EditMode/Save/ComputeEnvelopeChecksum_fixture_test.cs` | Known envelope → known SHA-256 hex. Locks the canonical serialization spec. |
| `Assets/Tests/EditMode/Save/NowTimestamp_format_test.cs` | Regex `^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$`. |

## Out of scope (deferred — and why each defer is ADR-0011-clean)

| Deferred | Lands in | Why deferring is not a bridge |
|---|---|---|
| First DTO (`NodeMapDto` — owns the 7b fields) | Slice 8a | Zero interface implementors is a valid state, not a "TODO" state. The interface contract is complete; it just has no inhabitants yet. |
| `Assets/Scripts/Save/link.xml` | Slice 8a (with first DTO) | Preserves `WastelandRun.Save.Dtos`. That namespace does not exist in Slice 8. Authoring an empty `link.xml` for a non-existent namespace is the ADR-0011 forbidden pattern #6 (transitional artifact). |
| `WastelandRun.Save.Dtos` sub-namespace | Slice 8a | Same — creation is the trigger for `link.xml`, both land together. |
| `SaveSystem.EnqueueRunStateWrite` / `LoadRunState` | Slice 8b | The orchestrator API does not exist. Run does NOT add a Save asmdef reference yet — there are zero call sites to "wire early," so there is no temptation for a stub. |
| Background `Task` writer, atomic temp-then-rename, retry budget | Slice 8b | Pure orchestrator concern. |
| Recovery chain (live → orphaned-temp → backup), `.bak` rotation, asymmetric exhaustion | Slice 8c | Pure orchestrator concern. |
| `RegisterCommitPredicate` / `RegisterHandlerActivePredicate` | Slice 8b | Write-time consultations; no writer exists yet. |

## ADR-0011 trap watch (this slice)

The two easiest forbidden patterns to slip into:

- **#5 (compat overload)** — defining `SaveSystem.EnqueueRunStateWrite` with an empty body so call sites can be wired early. **Guard:** the method does not land in Slice 8. The Run assembly does not reference the Save asmdef. There are zero call sites because there is no API to call.
- **#6 (transitional artifact)** — authoring `link.xml` "ready for DTOs" or creating `WastelandRun.Save.Dtos` namespace empty. **Guard:** the namespace and the `link.xml` ship in the same commit as their first inhabitant (Slice 8a).

Structural enforcement: the namespace in Slice 8 is `WastelandRun.Save`, not `WastelandRun.Save.Dtos`. Creating the sub-namespace is the explicit signal that the first DTO is landing.

## RunStateDto framing correction

Earlier notes (incl. the Slice 7b commit message) framed the 7b fields as "RunStateDto shape pre-work." TD corrected the framing: at done state, RunState is reconstituted from a **composite** of per-system DTOs (`NodeMapDto`, `RunDeckDto`, `VehicleDto`, `ScrapEconomyDto`, and a tiny `RunCoreDto` carrying `RunSeed` + `RunStatus` + `PendingCardOffer`). A flat `RunStateDto` that copies the 7b fields directly is exactly ADR-0004 Alternative 4 ("Monolithic RunState DTO — Save Owns the Schema"), which was **rejected** in the ADR. The 7b fields belong to `NodeMapDto`, not a top-level run dto. The `RunState.cs` docstring referring to "ADR-0004 RunStateDto" is mildly misleading — flag for a future tidy.

## Success metric (TD-locked)

Slice 8a (first real DTO) must ship without changing **any** signature in `SaveSystem`, `Envelope`, `IRunStateSerializable`, or `IMasteryStateSerializable`. If any of those four needs to shift to accommodate the first real DTO, the Slice 8 contract was wrong.

## Test posture

All four tests are **BLOCKING** at this slice scope. No ADVISORY tests — every contract is automatable at the POCO level.

- `SchemaRegistry_Unique_test` — reflection scan of the gameplay assembly for types declaring both `const string SystemId` AND `const int SchemaVersion`; asserts every `SystemId` appears exactly once. Trivially passes today; lights up the moment a duplicate is introduced.
- `InterfaceExclusion_test` — reflection scan; asserts no type implements both `IRunStateSerializable` and `IMasteryStateSerializable`. Trivially passes today.
- `ComputeEnvelopeChecksum_fixture_test` — seeds a known envelope (excluding checksum field), asserts the SHA-256 hex of its canonical serialization matches a hardcoded expected hex. **This is the single most important regression test in the entire Save system** — any environmental drift in canonical serialization (cultural formatting, property order, encoding) fails it immediately and Steam Cloud cross-machine validation would break in production.
- `NowTimestamp_format_test` — regex match plus a `DateTime.TryParseExact` round-trip.

## Technical Director Review

**Verdict: TD-SLICE-8-CUT — APPROVE — Option 1 (Envelope + Contracts, no DTO, no I/O).**

Slice 8 lands ADR-0004's schema-shaped contract surface and nothing else.

- **Why not Option 2 (Envelope + RunStateDto round-trip):** forces a `RunStateDto` shape decision before any system DTO precedent exists. Per ADR-0004 Decision 1 each system owns its DTO; the four 7b fields (`BiomeId`, `RunSeed`, `CurrentIndex`, `PathHistory[]`) are NodeMap's slice, not the run's. Shipping a `RunStateDto` that copies them literally fuses NodeMapDto's schema into RunState's envelope — Alternative 4 trap.
- **Why not Option 3 (full vertical):** months of work, multiple cross-cutting concerns (background `Task`, OQ4/OQ5 P/Invoke verification, IL2CPP smoke test, retry budget). Cannot be one slice.

**What lands in Slice 8:** asmdef `WastelandRun.Save`; `Envelope.cs` matching Decision 5; `IRunStateSerializable.cs` + `IMasteryStateSerializable.cs`; `SaveSystem.cs` static with only `ComputeEnvelopeChecksum(Envelope)` and `NowTimestamp()`; Newtonsoft package install (helper needs canonical serialization); four BLOCKING EditMode tests.

**Newtonsoft scope:** install `com.unity.nuget.newtonsoft-json` in Slice 8. **No `link.xml` yet** — it preserves the DTO namespace, which has zero types in Slice 8. Adding `link.xml` now is a forbidden-pattern slip. Lands with first DTO in Slice 8a.

**Categorical fit:** everything in the slice is "save infrastructure as contract surface." Excluded: nothing touches `RunState`, `RunController`, `RunSession`, `NodeMap`, or any view layer. The Run namespace docstring on `PendingCardOffer` keeps referring to ADR-0004 as a forward reference — no behavioral coupling.

**RunStateDto shape (when it lands later):** composite envelope, not flat DTO. `RunState` reconstituted from `NodeMapDto` + `RunDeckDto` + `VehicleDto` + `ScrapEconomyDto` + tiny `RunCoreDto`. The 7b fields belong to `NodeMapDto`.

**Test posture:** all four tests BLOCKING (CI must pass). No ADVISORY at this scope — pure POCO contract, all evidence automatable.

**Success metric:** Slice 8a ships without changing any signature in `SaveSystem`, `Envelope`, `IRunStateSerializable`, or `IMasteryStateSerializable`. Signature shift in any of those four = Slice 8 contract was wrong.

**Files referenced:** `docs/architecture/adr-0004-save-persistence-architecture.md`, `Assets/Scripts/Run/RunState.cs`, `Packages/manifest.json`.

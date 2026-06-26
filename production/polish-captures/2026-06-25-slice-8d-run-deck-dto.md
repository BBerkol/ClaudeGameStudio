# Polish Capture: Slice 8d `RunDeckDto` + Polymorphic `CardEffectDto` Converter

**Date:** 2026-06-25
**System:** ADR-0004 RunState DTO chain — third concrete RunState DTO; introduces the polymorphic `CardEffect` persistence machinery + the `run.session_core` resume-atomic group rename.
**Affected paths:**

Unity-side (new files):
- `Assets/Scripts/Save/Dtos/RunDeckDto.cs`
- `Assets/Scripts/Save/Dtos/CardDefinitionDto.cs`
- `Assets/Scripts/Save/Dtos/CardEffectDto.cs` (abstract base + 7 concrete subtypes — `WeaponAttackEffectDto`, `PlateEffectDto`, `RepairEffectDto`, `RepositionFlipEffectDto`, `RepositionToEffectDto`, `DrawEffectDto`, `BuffEffectDto`)
- `Assets/Scripts/Save/Dtos/CardEffectConverter.cs` (custom `JsonConverter<CardEffectDto>` dispatching on `effect_type` const)
- `Assets/Scripts/Save/Adapters/RunDeckSerializable.cs`
- `Assets/Tests/EditMode/Save/RunDeckDto_round_trip_test.cs`
- `Assets/Tests/EditMode/Save/RunDeckDto_wire_format_test.cs`
- `Assets/Tests/EditMode/Save/CardEffectConverter_test.cs` (per-effect-type round-trip + unknown-discriminator partial-skip)
- `Assets/Tests/EditMode/Save/RunDeckSerializable_test.cs`
- `Assets/Tests/EditMode/CombatView/RunSceneHost_Resume_Test.cs` (existing — extended for deck rehydrate)

Unity-side (modified):
- `Assets/Scripts/Run/RunController.cs` — `StartRun(player, runSeed, map, deck)` signature change (caller-supplied deck)
- `Assets/Scripts/CombatView/RunSceneHost.cs` — `Initialize(LoadResult, NodeMapDto, RunSeedDto, RunDeckDto)` signature change; `BeginRunFromLoaded(int seed, NodeMap loadedMap, RunDeck loadedDeck)` signature change; `BeginNewRun` constructs `new RunDeck(RunDeck.Milestone1Starter())` at the call site
- `Assets/Scripts/CombatView/SaveBootstrap.cs` — register `RunDeckSerializable`; read `LastLoaded` on three adapters; pass three DTOs to `Initialize`
- `Assets/Scripts/Save/Dtos/RunSeedDto.cs` + `Assets/Scripts/Save/Adapters/RunSeedSerializable.cs` + `Assets/Scripts/Save/Dtos/NodeMapDto.cs` + `Assets/Scripts/Save/Adapters/NodeMapSerializable.cs` — group name xmldoc rename `run.seed_map` → `run.session_core` (doc-only; no wire-value change)
- `Assets/Tests/EditMode/Save/*Test.cs` + `Assets/Tests/EditMode/CombatView/RunSceneHost_Resume_Test.cs` — group name xmldoc/comment rename in existing tests

Framework-side (modified):
- `docs/architecture/adr-0004-save-persistence-architecture.md` — Slice 8d Amendment Addendum: rename `run.seed_map` → `run.session_core`, document membership criterion (*a member joins `run.session_core` if its absence-with-others-present creates a silently-broken determinism or progression invariant*), add `run.run_deck` to membership, update Slice 8c Migration Plan checklist row #285 + Validation Criteria row #578 to use new name.

## Proposed change

Land `RunDeckDto` (third RunState DTO under ADR-0004) carrying the player's run-scoped card collection. Introduces the polymorphic `CardEffect` persistence machinery — concrete `*EffectDto` mirrors per effect type + `effect_type` discriminator const + custom `JsonConverter` dispatching on the const. Expands the existing two-DTO resume-atomic group (`NodeMap` + `RunSeed`) to a three-DTO group with a renamed identity: `run.session_core`. Renames are doc-only (group identity is not a wire value); membership criterion documented in ADR-0004.

## Final-game picture this serves

The save-and-resume promise is that a player parking on a beacon, quitting to desktop, and relaunching finds their run intact — the exact node graph, the exact RunSeed, **and** the exact deck contents they had accumulated. Without `RunDeckDto`, a beacon-3 resume would silently roll the deck back to the M1 starter — the Combat beacons resolved en route would still be marked Resolved (NodeMapDto carries `IsResolved`), but the cards earned from each victory would be gone. This is mechanically broken in a way the player would (rightly) report as a bug: "my run loaded but I lost all my cards."

The slice also lands the polymorphism converter pattern that subsequent slices (8e PendingCardOfferDto, eventually 8g PlayerVehicleDto with installed parts) will reuse. Polymorphism is the highest remaining architectural risk in the save chain; isolating it under RunDeck — where the surface is "list of CardDefinition, no nullable, no lifecycle coupling" — keeps the converter risk separable from the additional concerns of CardOffer's nullable-latched lifecycle.

The `run.session_core` rename codifies a membership criterion that gates future additions to the atomic group, so the group doesn't accrete into `run.seed_map_deck_offer_vehicle_…` by slice 8g. The criterion is structural: a member joins iff its absence creates a silently-broken determinism or progression invariant. RunDeck qualifies (beacon-3 run with starter contents); Scrap and RunStatus do not (independently recoverable with surfaceable warnings).

## Authored values being destroyed

**Nothing authored is destroyed.** Slice 8d is purely additive at every surface:

| Where | Current | Replacement plan |
|---|---|---|
| `RunController.StartRun` | 3-arg `(player, runSeed, map)` constructs deck internally via `new RunDeck(RunDeck.Milestone1Starter())` | 4-arg `(player, runSeed, map, deck)` caller-supplied; both `BeginNewRun` and `BeginRunFromLoaded` construct the deck at the call site. Single shape (TD verdict Q5 = Option a). |
| `RunSceneHost.Initialize` | 3-arg `(LoadResult, NodeMapDto, RunSeedDto)` | 4-arg `(LoadResult, NodeMapDto, RunSeedDto, RunDeckDto)` |
| `RunSceneHost.BeginRunFromLoaded` | 2-arg `(int seed, NodeMap loadedMap)` | 3-arg `(int seed, NodeMap loadedMap, RunDeck loadedDeck)` |
| Group identity (doc-only, 16 xmldoc/comment refs across 9 files + 2 ADR rows) | `run.seed_map` | `run.session_core` — no `.sav` wire-value change (the group name is not a serialised field; the per-DTO `SYSTEM_ID` consts `"run.node_map"` and `"run.run_seed"` are unchanged) |
| `RunSceneHost.BeginNewRun` deck construction | Implicit (inside `RunController.StartRun`) | Explicit — `new RunDeck(RunDeck.Milestone1Starter())` at the host's call site |
| `Milestone1Starter()` | Static factory on `RunDeck` returning 13-card list | Unchanged. **Hard rule:** stays a static factory, never a default constructor behaviour (TD verdict Q5 ADR-0011-#3 watch). |

No GDD edits. No SO edits. No prefab edits. No designer-tuned values touched. No scene edits.

## What's added (in detail)

### `WastelandRun.Save.Dtos.RunDeckDto`

- `const string SYSTEM_ID = "run.run_deck"` (dotted-snake per Slice 8a Amendment).
- `const int SCHEMA_VERSION = 1`.
- Payload: `List<CardDefinitionDto> Cards` (`[JsonProperty("cards")]`).
- Instance properties `SystemId`, `SchemaVersion`, `[JsonIgnore] Type DtoType => typeof(RunDeckDto)` per Slice 8c Amendment Addendum.
- `static RunDeckDto From(RunDeck deck)` — projects each `CardDefinition` through `CardDefinitionDto.From`.
- `RunDeck ToRunDeck()` — reconstructs `RunDeck` via its existing `IEnumerable<CardDefinition>` ctor.
- `object ToDto() => this` + `void FromDto(object)` matching the established adapter contract.

### `WastelandRun.Save.Dtos.CardDefinitionDto`

- Payload: `[JsonProperty("name")] string Name`; `[JsonProperty("energy_cost")] int EnergyCost`; `[JsonProperty("description")] string Description`; `[JsonProperty("effects")] List<CardEffectDto> Effects`.
- All fields required at deserialisation (`[JsonProperty(Required = Required.Always)]`).
- `static CardDefinitionDto From(CardDefinition)` + `CardDefinition ToCardDefinition()` (constructs concrete `CardEffect` subclasses via the converter).
- **Not** an `IRunStateSerializable` — it is a nested DTO, owned by `RunDeckDto`.

### `WastelandRun.Save.Dtos.CardEffectDto` (abstract) + 7 concrete subtypes

- Abstract base carries `[JsonProperty("effect_type", Required = Required.Always)] string EffectType { get; }` (read-only on the base; each concrete subtype returns its own const).
- Each concrete subtype declares a `const string EFFECT_TYPE_ID` constant. **TD-amended Q3:** the const is hand-authored on each Dto class, NOT derived via `nameof(WeaponAttackEffect)` or reflection over the runtime type. The const is the wire contract; the runtime class is the consumer. Refactor-rename of the runtime type must not silently invalidate saves.

Effect IDs (locked here for ADR-0004 reference):
| Concrete Dto | `EFFECT_TYPE_ID` |
|---|---|
| `WeaponAttackEffectDto` | `"weapon_attack"` |
| `PlateEffectDto` | `"plate"` |
| `RepairEffectDto` | `"repair"` |
| `RepositionFlipEffectDto` | `"reposition_flip"` |
| `RepositionToEffectDto` | `"reposition_to"` |
| `DrawEffectDto` | `"draw"` |
| `BuffEffectDto` | `"buff"` |

Each concrete subtype carries its fields as `[JsonProperty(Required = Required.Always)]` matching the runtime ctor-throws-on-null discipline.

### `WastelandRun.Save.Dtos.CardEffectConverter`

- Custom `JsonConverter<CardEffectDto>` dispatching on the `effect_type` discriminator string.
- `ReadJson`: read JObject, switch on `effect_type` string → construct concrete `*EffectDto`, populate from JObject.
- `WriteJson`: serialise the concrete subtype directly (Newtonsoft's default contract picks up the const `EffectType` property).
- **TD-amended Q4:** unknown `effect_type` throws `JsonSerializationException`. The Slice 8b load path catches DTO-level deserialisation throws and partial-skips the entry (per the established per-DTO partial-skip contract). DTO-scope partial skip, NOT envelope-scope failure. Capture explicitly records this policy.
- **No reflection** over assembly types in the dispatch. The switch arms are hand-coded — adding a new effect = add the const + the Dto + one switch arm + bump `RunDeckDto.SCHEMA_VERSION`. Reflection over types is exactly how `TypeNameHandling` re-enters through the back door (TD-flagged ADR-0011 watch).

### `WastelandRun.Save.Adapters.RunDeckSerializable`

- Mirrors `NodeMapSerializable` and `RunSeedSerializable`: `Func<RunDeck>` live source, `ToDto()` projects fresh via `RunDeckDto.From(_liveSource())`, `FromDto(object)` captures `LastLoaded` for the resume gate.
- Same null-source-throws guard on `ToDto`.
- `SystemId` / `SchemaVersion` / `DtoType` forward from `RunDeckDto` consts (single source of truth).

### `SaveBootstrap.Bind`

- Adds a third adapter registration: `_runDeckAdapter = new RunDeckSerializable(() => _host != null ? _host.State?.Deck : null);` + `SaveSystem.RegisterRunStateSerializable(_runDeckAdapter);`.
- `LoadAndInitialize` reads `_runDeckAdapter.LastLoaded` and passes to `_host.Initialize(result, loadedNodeMap, loadedRunSeed, loadedRunDeck)`.

### `RunSceneHost.Initialize` resume gate

- All-three-or-none: `if (loadedNodeMap != null && loadedRunSeed != null && loadedRunDeck != null) BeginRunFromLoaded(loadedRunSeed.Seed, loadedNodeMap.ToNodeMap(), loadedRunDeck.ToRunDeck()); else BeginNewRun(null);`
- One decision point; no nested conditionals; no bimodal-path branching inside the controller.

### `RunController.StartRun`

- New signature: `internal void StartRun(Vehicle playerVehicle, int runSeed, NodeMap map, RunDeck deck)`.
- Throws on null deck (matches existing null-throws on player + map).
- No internal `new RunDeck(...)` construction. Caller-supplied.

### `RunDeck.Milestone1Starter` — locked-in shape

- Stays a static factory method returning `List<CardDefinition>`. Never a default constructor behaviour. TD verdict Q5 ADR-0011-#3 watch — if `new RunDeck()` ever returns starter-populated, the bimodal path re-enters at the ctor.

## Validation plan

EditMode baseline preserved + new tests green. Final tally target: **~635 total / 634 pass / 0 fail / 1 pre-existing skip** (607 baseline + ~28 new tests, exact count TBC at green attestation).

Test matrix:

| # | Test fixture | Approx count | What it locks |
|---|---|---|---|
| 1 | `RunDeckDto_round_trip_test` | 6 | `From(RunDeck)` → `ToRunDeck()` deep-equal across name/cost/description + effect list per card. Schema const surface. Wrong-type `FromDto` throws. Null-source `From` throws. |
| 2 | `RunDeckDto_wire_format_test` | 3 | Locked canonical JSON literal for a 2-card deck covering 2 distinct effect types (e.g., BulletBarrage WeaponAttack + Weld Plate). Ordinal property sort. Spec-not-snapshot stance per Slice 8 precedent. |
| 3 | `CardEffectConverter_test` | 8 (1 round-trip per effect type + 1 unknown-discriminator) | Round-trip per effect type preserves all fields. Unknown `effect_type` throws `JsonSerializationException` at deserialisation. |
| 4 | `RunDeckSerializable_test` | 7 | Ctor null-source throws. SystemId/SchemaVersion/DtoType forwarding. Snapshot-on-demand projection (mutate live deck → next ToDto sees new card). FromDto captures `LastLoaded`. Wrong-type FromDto throws. ToDto with null source throws wiring-trap. |
| 5 | `RunSceneHost_Resume_Test` (extension) | +3 | End-to-end resume with all three DTOs planted: deck contents survive round-trip. Mixed-skip (deck-only missing) → all three regenerate. Determinism: resumed deck produces same card-offer derivations on next victory. |
| 6 | `SchemaRegistry_Unique_test` | (existing — auto) | `RunDeckDto.SYSTEM_ID = "run.run_deck"` registers, no collision with `"run.node_map"` / `"run.run_seed"`. Auto-lights at green. |
| 7 | `SchemaRegistry_DtoType_test` | (existing — auto) | `RunDeckSerializable.DtoType == RunDeckSerializable.ToDto().GetType()` (`typeof(RunDeckDto)`). Auto-lights at green. |

## Defers (ADR-0011-clean)

| Item | Reason it lights up later, not now |
|---|---|
| `PendingCardOfferDto` (slice 8e) | TD verdict Q1 = isolate polymorphism risk under RunDeck. CardOffer's nullable-latched lifecycle + OfferSeed + dupe-filter replay deserve their own slice once `CardDefinitionDto` machinery is proven. |
| `ScrapDto` + `RunStatusDto` (slice 8f, bundled) | TD verdict Q7 — trivial int/enum DTOs; bundle to avoid burning slice ceremony on 10-line files. Independent recovery (NOT in `run.session_core`); scrap-missing = 0 with surfaceable warning, RunStatus-missing = Ongoing with safe default. |
| `PlayerVehicleDto` (slice 8g) | TD verdict Q7 — large surface: installed parts + per-slot state (Online/Offline/Scrapped) + ADR-0012 Sum-of-Parts armor replay-on-load. Joins `run.session_core` (beacon-3 with starter vehicle = silently broken). |
| MasteryState DTOs | Separate chain. Asymmetric-exhaustion policy (blocking dialog) lands with the first mastery DTO. |
| 1.0 / post-EA effect discriminator deprecation policy | Out of scope for 8d. EA-mode discipline: bump SCHEMA_VERSION on new effect type. 1.0 migration policy (rename / retire / deprecate effect types) is TBD — flagged here so it doesn't get re-litigated mid-1.0. |
| `[System.Serializable]` + parameterless ctor on runtime `CardEffect` subclasses | Pre-dates this work; required by Unity `[SerializeReference]` for designer authoring. Not used by the Newtonsoft path — the converter constructs concrete `*EffectDto` instances, NOT runtime `CardEffect` types. Confirm in implementation that the converter has zero reflection over runtime `CardEffect.*` types. |
| Skip-cascade hoisted into `SaveSystem.Load` | At three-member group, the gate in `RunSceneHost.Initialize` is still sufficient. When the group reaches N=4 (slice 8g `PlayerVehicleDto`) a shared helper or declarative group-declaration in the ADR becomes valuable. Flag for ~8g. |

## Technical Director Review

TD consulted 2026-06-25 pre-implementation. Verdict delivered inline:

**[TD-ARCHITECTURE]: APPROVE WITH AMENDMENTS.**

Verdict on slice scope: APPROVE Slice 8d as `RunDeckDto` alone, with two structural amendments to the proposed shape (Q3 sub-shape, Q6 group name).

**Q1 — Slice scope: RunDeck alone vs RunDeck + PendingCardOffer.** APPROVE Option A (RunDeck alone). The polymorphic-CardEffect converter is the architectural risk in this slice. Landing it under RunDeck — where the surface is "list of CardDefinition, no nullable, no resume coupling beyond the deck itself" — isolates the converter from `CardOffer`'s additional concerns (`OfferSeed`, dupe-filter semantics, nullable-latched-on-victory lifecycle, ADR-0013 pity-precedence). Slice 8e gets to reuse `CardDefinitionDto` + the effect converter as proven plumbing.

**Q2 — Polymorphic CardEffect persistence shape.** APPROVE Option B (explicit discriminator + custom JsonConverter). Discriminator value is a snake_case string keyed off a const `EFFECT_TYPE_ID` on each Dto class — NOT derived from `nameof(WeaponAttackEffect)` or reflection over the runtime type. Refactor-rename of the runtime type must not silently invalidate saves. The constant is the wire contract; the runtime class is the consumer. Converter lives in `WastelandRun.Save` alongside the canonical-JSON resolver. **ADR-0011 watch:** converter must NOT carry "unknown discriminator → default effect" or "missing discriminator → infer from fields" fallback branches — both are #3 (bimodal) traps. Option C (catalog lookup) correctly rejected — ADR-0013 explicitly flags upgraded copies and M2 procedural modification, which would force a parallel "modified-card-instance" persistence path. That's #3 (bimodal storage) the moment M2 lands.

**Q3 — Concrete-DTO-per-effect vs single-DTO-with-all-fields-nullable.** APPROVE Option A (7 concrete DTO classes + abstract base). Non-negotiable under ADR-0011-#3. A single CardEffectDto with `int? Damage`, `int? ArmorGain`, `int? RepairAmount`, `LanePosition? Target`, `int? Count`, `BuffTag? Tag`, `string? LaunchSlotId` is a bimodal path with seven modes. Concrete subtypes mirror the runtime hierarchy, each Dto's field set is non-nullable and required, and a malformed save fails at deserialisation rather than at "Damage is null on a WeaponAttack" deep in combat resolution. Abstract base carries only `effect_type` (`JsonRequired.Always`). Each concrete `*EffectDto` declares fields as `JsonRequired.Always`. Single switch in converter, keyed off `EFFECT_TYPE_ID` strings — no reflection over assembly types.

**Q4 — Schema versioning when new effect types land.** APPROVE proposal with one amendment. Bump `RunDeckDto.SCHEMA_VERSION` on every new effect type AND throw on unknown discriminator. Partial-skip at the DTO layer is the right policy in EA mode — the save loses its deck, the player gets a starter deck and a surfaced "save partially corrupted" notice. **Amendment:** capture explicitly records that `RunDeckDto` extends the ADR-0004 "any mismatch = incompatible" policy to "any unknown `effect_type` discriminator triggers DTO-level skip" — policy clarification, not new behaviour. Codifying "unknown discriminator = DTO-scope partial skip, not envelope-scope failure" prevents future-agent misreading. **Forward note (non-blocking):** post-EA / 1.0 wants a discriminator-deprecation policy. Out of scope for 8d.

**Q5 — Resume integration: RunController signature.** APPROVE Option (a) — caller-supplied deck, single signature. Direct application of ADR-0011: (b) is #5 (compat overload), (c) is #3 (bimodal default-param path — we've been burned by exactly this in ADR-0012's `InstallPart` default-param overload semantic trap, which is a live entry in agent memory). Option (a) is the only shape that survives the "no bridges at done" rule. **ADR-0011 watch:** ensure `RunDeck.Milestone1Starter()` stays a factory method, NOT a parameterless ctor or constructor default. If `new RunDeck()` ever returns a starter-populated deck, the bimodal path re-enters at the constructor layer.

**Q6 — Resume-atomic group membership.** AMEND to Shape A, but reject the proposed rename. Use **`run.session_core`**. Group membership: APPROVE adding RunDeck to the atomic group. A beacon-3 run with starter contents is mechanically broken in a way the player cannot recover from and cannot be warned about meaningfully. Both-or-neither is the correct policy. **On the rename:** `run.seed_map_deck` accretes — it'll be `run.seed_map_deck_offer_vehicle` by slice 8g. `run.session_state` is too broad (Scrap and RunStatus are also session state, but independently recoverable). `run.session_core` — semantically "the load-bearing structural state without which the run cannot resume coherently" — and document the membership criterion in ADR-0004 Amendment: *a member joins `run.session_core` if its absence-with-others-present creates a silently-broken determinism or progression invariant.* That criterion gates future additions and makes the group name self-documenting. **ADR-0004 documentation impact:** documented-group rename requires an ADR-0004 amendment addendum (matches the precedent set in slice 8c's amendment). The capture should call out that the amendment is part of the slice 8d deliverable, not a follow-up. **Backward compatibility:** the group identity is NOT a wire value (per-DTO `SYSTEM_ID` consts `"run.node_map"` / `"run.run_seed"` are unchanged). Doc-only rename across 16 xmldoc/comment refs across 9 Unity files + 2 ADR rows. Zero save-compat impact.

**Q7 — Categorical fit and slicing order.** APPROVE `RunDeckDto` as slice 8d. The chain has already been stress-tested across three DTOs (NodeMap, RunSeed, envelope/orchestrator). The remaining risk surface is "does polymorphism land cleanly." `RunDeckDto` is the polymorphism slice. Recommended order: (8d this slice) → 8e `PendingCardOfferDto` (reuses CardDefinitionDto + effect converter; joins `run.session_core`) → 8f `ScrapDto` + `RunStatusDto` bundled (trivial int/enum, independent recovery) → 8g `PlayerVehicleDto` (large surface, joins `run.session_core`) → MasteryState chain separately. Don't slice ScrapDto first to "warm up" — sets a precedent that single-int DTOs need full slice ceremony.

**ADR-0011 trap watches across the slice.**
- **#1 (adapter):** `RunDeckSerializable` snapshot-on-demand wrapper is the same adapter pattern approved in 8c. Re-approved.
- **#3 (bimodal):** highest risk. Watch the converter (no field-presence inference fallback), the RunController signature (no default-param `deck = null`), and the single-vs-concrete-Dto choice (Q3). All three addressed.
- **#5 (compat overload):** Q5 Option (b) explicitly rejected.
- **#6 (transitional artifact):** `[System.Serializable]` + parameterless ctor on `CardEffect` subclasses pre-dates this work (`[SerializeReference]` requirement, not Newtonsoft). Confirm parameterless ctors are NOT used by the Newtonsoft path — the JsonConverter constructs concrete DTOs directly, not via reflection over runtime `CardEffect` types.
- **#7 (transitional comment):** standard discipline — no "TODO: refactor when M2 lands" comments in the converter switch. If M2 needs a new effect type, the discipline is "add the const + the Dto + the switch arm, bump SCHEMA_VERSION."

**Net verdict.** Slice 8d as `RunDeckDto` — APPROVED with four amendments:
1. Q3: concrete-DTO-per-effect with `EFFECT_TYPE_ID` const on each, not derived from runtime type names.
2. Q4: codify "unknown discriminator = DTO-scope partial skip, not envelope-scope failure" explicitly in the capture.
3. Q6: rename to `run.session_core` (not `run.seed_map_deck`), and ship the ADR-0004 amendment addendum as part of this slice. Document the membership criterion.
4. Q5: confirm `RunDeck.Milestone1Starter()` stays a static factory, never default ctor behaviour.

Land slice 8d before any other RunState DTO. The polymorphism risk is the highest remaining architectural unknown in the save chain; isolating it under RunDeck and reusing the converter for slice 8e is the right sequencing.

This verdict assumes EditMode tests covering the converter (round-trip per effect type, unknown-discriminator partial-skip, schema-mismatch partial-skip) ship green with the slice. The capture attests to that explicitly before slice closure — gate-check requires green tests, and "compiles clean" is not the same as "semantically green" for a polymorphism converter.

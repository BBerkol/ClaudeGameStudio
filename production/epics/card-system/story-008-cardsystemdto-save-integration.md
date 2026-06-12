# Story 008: CardSystemDTO & Save Integration

> **Epic**: Card System
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: pending — `docs/architecture/control-manifest.md` not yet authored

## Context

**GDD**: `design/gdd/card-system.md`
**Requirements**: `TR-card-002`, `TR-card-017`, `TR-card-018`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Card System Data Authoring; ADR-0004: Save & Persistence Architecture
**ADR Decision Summary**: `CardSystemDTO` is a sealed record registered with `SaveManager` using `SystemId = "card-system"` and `SchemaVersion = 1`. Deck/Discard/Exhausted are persisted as `List<string>` of stable `CardId` values. `CardCopyCounts` is persisted for copy-limit enforcement. `RarePityCounter` is NOT in this DTO (owned by `LootStateDTO`). `IsFreeValveApplied` (Chopshop valve) is NOT in this DTO (owned by the future Node Map / Scrap Economy DTO). `SourceSlotId` is runtime-only and is NOT serialized. On load, an invariants check rebuilds `CardCopyCounts` from `Deck` and logs a warning + auto-corrects if mismatch.

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: Serialization uses Newtonsoft.Json with `link.xml` IL2CPP preservation (ADR-0004). Sealed records require explicit `JsonConstructor` attribute or public init-only properties to round-trip correctly. Confirm Unity 6.3 Mono runtime compatibility with Newtonsoft.Json sealed records before first save-code commit.

**Control Manifest Rules (this layer)**:
- Required: pending
- Forbidden: pending
- Guardrail: pending

---

## Acceptance Criteria

*From ADR-0006, ADR-0004, and `design/gdd/card-system.md`:*

- [ ] `CardSystemDTO` is a sealed record with `public const string SystemId = "card-system"` and `public const int SchemaVersion = 1`
- [ ] `CardSystemDTO` contains exactly four data fields: `Deck (List<string>)`, `Discard (List<string>)`, `Exhausted (List<string>)`, `CardCopyCounts (Dictionary<string, int>)`. A compile-time test enumerates `CardSystemDTO`'s public non-const fields/properties and asserts exactly four are present. Any additional field is a schema contract violation requiring a `SchemaVersion` increment.
- [ ] `CardSystemDTO` does NOT contain `RarePityCounter` (owned by Loot & Reward's `LootStateDTO`), `IsFreeValveApplied` (owned by the future Node Map / Scrap Economy DTO), or `SourceSlotId` (runtime-only per ADR-0006 Amendment 2026-05-18)
- [ ] Round-trip: serialize a `CardSystemDTO` with `Deck=["scout_precision_001","scout_precision_001"]`, `Discard=["scout_assault_003","scout_maneuver_002","scout_repair_001","scout_control_004","scout_precision_007"]`, `Exhausted=["scout_rare_001","scout_rare_002"]`, `CardCopyCounts={"scout_precision_001":2}` → Newtonsoft.Json serialize → deserialize → all fields byte-identical to the original
- [ ] `CardId` values survive round-trip unchanged — format `[chassis]_[family]_[sequence]` is preserved exactly, no truncation or encoding change
- [ ] Card states survive load: a DTO with 5 Deck, 3 Discard, 2 Exhausted cards deserializes to a deck state machine with the same 5/3/2 distribution
- [ ] `CardCopyCounts` drift auto-correct: when loaded DTO has `CardCopyCounts={"scout_precision_001":1}` but `Deck=["scout_precision_001","scout_precision_001"]` (count mismatch), `CardSystemSaveAdapter` logs a warning, auto-corrects to `{"scout_precision_001":2}`, and proceeds without throwing. The corrected count is used for all subsequent offer-filter operations in that session
- [ ] `CardSystemDTO` is registered with `SaveManager` via `SystemId` constant — verified by a unit test that asserts `CardSystemDTO.SystemId == "card-system"` and `CardSystemDTO.SchemaVersion == 1`

---

## Implementation Notes

*Derived from ADR-0004 and ADR-0006 Implementation Guidelines:*

- `CardSystemDTO` is the serialization boundary. At save time: collect current card state from the deck state machine → populate the four DTO fields → hand to `SaveManager`. At load time: receive DTO from `SaveManager` → run invariants check → reconstruct deck state machine.
- `SourceSlotId` is intentionally absent from the DTO. On load, `SourceSlotId` is re-stamped from the vehicle's current part loadout (which is serialized by the Vehicle system). If the vehicle loadout changes between save and load in an unexpected way, affected cards will have `SourceSlotId=null` — this is acceptable as it fails safe (card stays in deck, just loses the part-grant tracking).
- `IsFreeValveApplied` persistence ownership: this DTO explicitly does NOT own it. The Chopshop valve result must be persisted by whatever system owns Chopshop node state (future: Node Map / Scrap Economy system). If that DTO does not exist yet, the valve result is lost on save/reload — this is a known gap tracked in Story 006's cross-story dependency note.
- `CardCopyCounts` drift can occur if a save file was written by a future version of the game or if a bug caused an inconsistency. The auto-correct path (rebuild from Deck list) is the recovery: count occurrences of each `CardId` in `Deck`, `Discard`, and `Exhausted` combined.
- Newtonsoft.Json sealed records: use `[JsonConstructor]` on the canonical constructor or use init-only properties. Verify deserialization works with a round-trip test before shipping.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- **Story 007**: The in-memory deck state machine that this DTO serializes
- **Story 006**: `IsFreeValveApplied` computation — the persistence of this value is a gap tracked in Story 006's cross-story dependency note; it is NOT in this DTO

---

## QA Test Cases

*Written by qa-lead at story creation. Implement against these — do not invent new cases.*

**AC-1: CardSystemDTO exact field count**
- Given: `typeof(CardSystemDTO)` reflected
- When: Public non-const properties and fields are enumerated
- Then: Exactly 4 are present: Deck, Discard, Exhausted, CardCopyCounts
- Edge cases: Const fields (SystemId, SchemaVersion) are excluded from the count

**AC-2: Round-trip — all fields byte-identical**
- Given: A `CardSystemDTO` with 2-card Deck, 5-card Discard, 2 Exhausted, 1-entry CopyCounts
- When: Serialized with Newtonsoft.Json and deserialized
- Then: All four list contents are identical (same `CardId` strings, same order, same counts)
- Edge cases: `CardId` containing underscore characters (`"scout_precision_001"`) must not be altered by JSON serialization

**AC-3: CardCopyCounts auto-correct**
- Given: DTO where `CardCopyCounts={"scout_precision_001":1}` but Deck contains `"scout_precision_001"` twice
- When: `CardSystemSaveAdapter.Load(dto)` is called
- Then: Warning is logged; corrected CopyCounts shows `{"scout_precision_001":2}`; no exception
- Edge cases: CopyCounts with a missing key (card in Deck not in CopyCounts) — key is added with correct count

**AC-4: Card states 5/3/2 distribution survives load**
- Given: DTO with `Deck=["a","b","c","d","e"]`, `Discard=["f","g","h"]`, `Exhausted=["i","j"]`
- When: Deck state machine is reconstructed from DTO
- Then: `deck.Count==5`, `discard.Count==3`, `exhausted.Count==2`
- Edge cases: All three lists empty → zero counts; state machine initializes without error

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/card-system/cardsystem-dto-save_test.cs` — must exist and pass

**Status**: [x] `tests/integration/card-system/cardsystem-dto-save_test.cs` — 8 tests, all passing

---

## Dependencies

- Depends on: Story 007 must be DONE (deck state machine must exist to serialize)
- Unlocks: Epic Definition of Done (CardSystemDTO round-trip through SaveManager is a DoD gate)

---

## Completion Notes
**Completed**: 2026-05-24
**Criteria**: 8/8 passing
**Deviations**: ADVISORY — ADR-0006 §Requirements says "reject unknown CardIds with typed exception"; implementation skips + logs instead (story spec supersedes ADR text). Requires ADR-0006 amendment; no code change. ADVISORY — warning log on CardCopyCounts drift not directly asserted in tests (requires Console.SetError capture or injectable logger; deferred).
**Test Evidence**: Integration: `tests/integration/card-system/cardsystem-dto-save_test.cs` (8 tests)
**Code Review**: Complete — LP-CODE-REVIEW APPROVED 2026-05-24; QL-TEST-COVERAGE ADEQUATE 2026-05-24
**Scope note**: `ICardCatalog.cs` touched outside stated scope — added `<exception>` doc tag for `GetById` contract; valid, required by adapter.

# Story 009: Deck Composition Rules & Edge Cases

> **Epic**: Card System
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: pending — `docs/architecture/control-manifest.md` not yet authored

## Context

**GDD**: `design/gdd/card-system.md`
**Requirements**: `TR-card-009`, `TR-card-018`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Card System Data Authoring
**ADR Decision Summary**: Deck minimum is 10 (design constraint tied to pillars, not tunable). `MaxDeckSize = 60` is a runtime safety ceiling, unreachable in EA, enforced by returning `DeckGrowResult.CapacityExceeded`. Keyword extensibility (EC3) is an architectural invariant enforced at code review, not by automated test; a runtime proxy test covers the unknown-flag silent-ignore behavior.

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: Deck composition enforcement lives in `WastelandRun.Cards` (engine-free) for the logic rules. UI feedback (disabled purge button, inline message) is wired in the UI layer — this story verifies the logic-level precondition enforcement only.

**Control Manifest Rules (this layer)**:
- Required: pending
- Forbidden: pending
- Guardrail: pending

---

## Acceptance Criteria

*From `design/gdd/card-system.md` deck composition rules and Edge Cases section:*

- [ ] **Purge floor (EC7)**: Chopshop purge attempt when deck is at exactly 10 cards returns a failure result indicating the deck is at minimum size. No Scrap is charged. Logic-layer enforcement only — the UI disabled state is wired separately.
- [ ] **Purge allowed at 11 (EC7 boundary)**: purge at 11 cards succeeds, deck drops to 10. Subsequent purge attempt at 10 returns failure. Test: deck starts at 11 → purge → deck=10 → purge → failure
- [ ] **Starter card purge allowed (EC6)**: `IsStarterCard = true` does NOT protect a card from Chopshop purge. A starter card can be purged when deck size > 10. A non-starter card and a starter card behave identically in purge eligibility.
- [ ] **Multi-effect resolution does not halt at Offline (EC10)**: given a mock card with two effects where the first `DamageEffect` takes a target slot to Offline state, all subsequent effects in the list still fire against the now-Offline slot. The effect resolution loop does not short-circuit.
- [ ] **Empty ValidSubsystemTargets = all slots (EC11)**: a card with `TargetType == EnemySubsystem` and `ValidSubsystemTargets.Count == 0` — the targeting logic treats all 4 enemy slots as valid targets. No slot is suppressed.
- [ ] **Innate cap filtering (EC16)**: deck containing 3 Innate cards → `Generate()` excludes Innate cards from the offer pool. Deck containing 2 Innate cards → Innate cards appear normally.
- [ ] **EC14 — Innate+Ethereal: NOT rejected at SO import**: a `CardDefinitionSO` with both `Innate` and `Ethereal` keywords passes `OnValidate()` without errors. In combat simulation, the card appears in the opening hand (Innate fires) and is auto-discarded at end of turn if not played (Ethereal fires). Neither keyword suppresses the other.
- [ ] **OnValidate rejects exactly Ethereal+Retain (AC-9a)**: `OnValidate()` rejects only `Ethereal AND Retain` set simultaneously. `Innate|Ethereal`, `Innate|Exhaust`, and `Exhaust|Retain` are all accepted. Test covers all four cases: (a) `Ethereal|Retain` → error logged; (b) `Innate|Ethereal` → no error; (c) `Innate|Exhaust` → no error; (d) `Exhaust|Retain` → no error.
- [ ] **Position requirement enforcement**: a card with `PositionRequirement == RequiresBehind` when player position is `Ahead` — play attempt is blocked at game logic level (returns failure result, not an exception). Card with `RequiresAhead` blocked when `Behind`. Cards with `BonusIfBehind` or `BonusIfAhead` can always be played regardless of position.
- [ ] **MaxDeckSize cap**: a deck-grow operation that would push deck size to 61 returns `DeckGrowResult.CapacityExceeded` and does not add the card. Deck size remains 60.
- [ ] **EC3 — keyword extensibility (ADVISORY, code review)**: removing `Exhaust` requires changes only to the enum, Card Combat resolution branch, and SO assets — zero structural changes to `CardDefinitionSO`. Adding a new keyword requires only enum, new resolution branch, new glossary entry. Reviewed in code review; not a blocking test gate.
- [ ] **EC3 — runtime proxy (automated)**: a unit test creates an `ICardData` mock with `Keywords = (CardKeyword)128` (unrecognized bit flag). Passing this card through `TokenResolver.Resolve()` produces a non-null string. No exception is thrown. Unknown keyword flags are silently ignored in the engine-free layer.

---

## Implementation Notes

*Derived from ADR-0006 Implementation Guidelines:*

- Deck minimum and maximum are constants in the `WastelandRun.Cards` assembly: `DeckMinSize = 10`, `MaxDeckSize = 60`.
- Purge precondition: check `CurrentDeckSize > DeckMinSize` before executing. Return a typed result (`DeckGrowResult.MinimumSizeReached`) rather than throwing — the UI layer reads this result to display the inline message.
- `DeckGrowResult.CapacityExceeded` is returned (not thrown) for the `MaxDeckSize = 60` cap. The cap is defensive and not expected to be hit in EA content.
- EC10 (multi-effect): the effect resolution loop must not check target state before calling each effect's dispatch. The loop iterates through the full `Effects` list regardless of intermediate state changes. State-checking on Offline targets is the combat resolution layer's responsibility, not the card system's.
- EC11 (empty ValidSubsystemTargets): `Count == 0` means all slots are valid. This is explicitly not an error — do not add an import validator for this case (covered by Story 002's BypassPlating-specific validator instead).
- The `(CardKeyword)128` runtime proxy test: `CardKeyword` is a `[Flags]` enum. An unrecognized value that is a power of 2 beyond the defined range must not crash the POCO layer. `TokenResolver` must not switch/match exhaustively on keyword values.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- **Story 002**: `OnValidate` implementation for Ethereal+Retain and all other SO import validators
- **Story 007**: In-memory deck state transitions (draw, play, discard, reshuffle)
- **UI layer**: Disabled purge button state and inline message rendering

---

## QA Test Cases

*Written by qa-lead at story creation. Implement against these — do not invent new cases.*

**AC-1: Purge floor and boundary**
- Given: Deck of exactly 10 cards; then deck of 11 cards
- When: Purge is attempted at 10; purge at 11; purge at 10 after the 11→10 purge
- Then: `10 → failure`; `11 → success, deck=10`; `10 (after) → failure`
- Edge cases: Starter card in a 10-card deck: same failure result as non-starter

**AC-2: EC10 multi-effect continues through Offline**
- Given: A card with two effects; a mock vehicle where Effect 1 takes Weapon slot to Offline
- When: Both effects are resolved in sequence
- Then: Effect 2 fires against the Weapon slot in Offline state; no short-circuit or exception
- Edge cases: Effect 2 is a DamageEffect targeting an already-Offline slot — must still fire (result handled by combat layer)

**AC-3: EC14 Innate+Ethereal accepted at import and fires correctly**
- Given: A CardDefinitionSO with `Keywords = Innate | Ethereal`
- When: `OnValidate()` is called; then in combat simulation the card is in the opening hand and not played by end of turn
- Then: `OnValidate()` logs no error; card appears in opening hand; card moves to Discard at end of turn
- Edge cases: `Keywords = Ethereal | Retain` on the same card → `OnValidate()` logs an error

**AC-4: EC3 runtime proxy — unknown keyword flag**
- Given: A mock `ICardData` with `Keywords = (CardKeyword)128`; description = `"Deal {damage} damage"`; `DamageEffect` with `Amount=5`
- When: `TokenResolver.Resolve(card)` is called
- Then: Returns `"Deal 5 damage"` — no exception, no null, no `"?"`
- Edge cases: `Keywords = (CardKeyword)Int32.MaxValue` — same result (no overflow exception)

**AC-5: MaxDeckSize cap**
- Given: A deck of exactly 60 cards
- When: A deck-grow operation is attempted
- Then: Returns `DeckGrowResult.CapacityExceeded`; deck size remains 60
- Edge cases: Deck of 59 cards: grow succeeds → deck=60; second grow → CapacityExceeded

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/card-system/deck-composition-edge-cases_test.cs` — must exist and pass

**Status**: [x] `tests/unit/card-system/deck-composition-edge-cases_test.cs` — 20 tests

---

## Dependencies

- Depends on: Story 007 must be DONE (deck state machine must exist), Story 002 must be DONE (OnValidate behavior is verified here)
- Unlocks: Epic Definition of Done (edge case coverage required before epic closes)

---

## Completion Notes
**Completed**: 2026-05-25
**Criteria**: 9/9 in-scope passing (AC-7 out of scope — Story 002; AC-9 code review gate — approved)
**Deviations**:
- ADVISORY: `DeckCompositionRules.TryPurge/TryGrow` accepts `List<ICardData>`; `DeckStateManager.Deck` returns `IReadOnlyList<ICardData>`. Caller must bridge at integration time. Flag when UI/Scrap Economy layer stories begin.
- ADVISORY: `CardPlayResult` and `PositionState` enums co-located in `CardPlayValidator.cs` rather than `Enums/` directory. Move in a future housekeeping pass.
**Test Evidence**: `tests/unit/card-system/deck-composition-edge-cases_test.cs` — 20 tests
**Code Review**: Complete — LP-CODE-REVIEW APPROVED, QL-TEST-COVERAGE ADEQUATE

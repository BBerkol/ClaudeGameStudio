# Story 007: Card States & Deck Lifecycle

> **Epic**: Card System
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: pending — `docs/architecture/control-manifest.md` not yet authored

## Context

**GDD**: `design/gdd/card-system.md`
**Requirements**: `TR-card-017`, `TR-card-024`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Card System Data Authoring; ADR-0003: Deterministic RNG Discipline
**ADR Decision Summary**: Four card states (In Deck, In Hand, In Discard, Exhausted) are managed as a pure POCO state machine in `WastelandRun.Cards`. Reshuffle consumes the caller-owned `System.Random` (ADR-0003). `SourceSlotId` on `ICardData` is runtime-stamped when a part installs a card; granted-card removal targets exact instances by `SourceSlotId`, not arbitrary copies.

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: Deck state machine lives in `WastelandRun.Cards` (engine-free). Card states are not MonoBehaviour-backed. Reshuffle must not construct `new System.Random()` internally — the caller-owned instance must be passed in.

**Control Manifest Rules (this layer)**:
- Required: pending
- Forbidden: `UnityEngine.Random` in reshuffle path
- Guardrail: pending

---

## Acceptance Criteria

*From `design/gdd/card-system.md` Card States, Keywords, and Edge Cases sections:*

- [ ] A card drawn from the deck moves to In Hand: deck count decreases by 1, hand count increases by 1
- [ ] A played non-Exhaust card moves from In Hand to In Discard: hand count decreases by 1, discard count increases by 1
- [ ] At end of turn, non-Retain cards in hand move to In Discard: after end-of-turn processing, hand contains only Retain-keyword cards
- [ ] When a draw is attempted with an empty deck and non-empty discard: the discard reshuffles into the deck instantaneously; the draw proceeds from the new deck. The reshuffle result is NOT the same order as the discard (shuffled). Given the same caller-owned `System.Random(seed: 42)` and the same discard `[A, B, C, D, E]`, two reshuffles produce the same output (deterministic per ADR-0003)
- [ ] Draw attempt with empty deck AND empty discard produces no card and no error (EC5). Hand count is unchanged. No exception is thrown.
- [ ] **Exhaust+Retain played (EC1 played case)**: a card with both `Exhaust` and `Retain` keywords, when played, enters Exhausted state immediately. Retain does NOT fire on play.
- [ ] **Exhaust+Retain NOT played (EC1 unplayed case)**: a card with both `Exhaust` and `Retain` keywords, NOT played at end of Turn 1, stays in hand (Retain fires). NOT played at end of Turn 2, stays in hand again (Retain fires again — hold is indefinite, not one-turn-only). When played on any future turn, enters Exhausted state.
- [ ] Exhausted cards do not re-enter the deck or discard pile for the remainder of the run
- [ ] An Innate card appears in the opening hand at combat 1 start AND combat 2 start in the same run (while the card is In Deck or In Discard)
- [ ] An Innate card played and Exhausted in combat 1 does NOT appear in the opening hand at combat 2 — Exhausted cards cannot be Innate-drawn (EC13)
- [ ] An Innate card already In Hand at combat start (Retained from prior combat) does NOT produce a second copy via Innate draw — Innate fires only for cards In Deck or In Discard at combat start (EC15)
- [ ] Granted cards are tracked by `SourceSlotId` on `ICardData`: removal of a part's granted cards targets exact instances where `SourceSlotId == removedSlotId`, not arbitrary copies with the same `CardId`. A deck containing two copies of `"scout_precision_001"` where one has `SourceSlotId="Weapon"` and one has `SourceSlotId=null` — removing the Weapon part removes only the `SourceSlotId="Weapon"` copy (TR-card-024)

---

## Implementation Notes

*Derived from ADR-0006 Implementation Guidelines:*

- The four states are locations, not properties: a card is "In Deck" if it is in the deck list, "In Discard" if in the discard list, etc. State is derived from location, not stored.
- Reshuffle: move all cards from discard list to deck list, then Fisher-Yates shuffle using the caller-provided `System.Random`. The caller passes `rng` through; the deck state machine does not construct its own.
- Exhaust+Retain hold duration is indefinite. "Retain" means "do not discard at end of turn" — this fires every turn-end for as long as the card is in hand and not played. There is no one-turn cap.
- `SourceSlotId` is null for starter cards and Merchant-purchased cards. Part-granted cards have `SourceSlotId` set to the slot ID of the installing part. When removing a part, filter removal candidates by `SourceSlotId == slotId`, not by `CardId` alone. A player may have 2 copies of the same card where only one was granted by a part.
- Innate draw at combat start: iterate the deck and discard lists; any card with `(Keywords & CardKeyword.Innate) != 0` is moved to hand as an additional draw (not replacing a normal draw slot). Cards already In Hand are skipped.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- **Story 008**: `CardSystemDTO` serialization — this story implements the in-memory state machine only
- **Story 009**: Deck composition rules (minimum size, purge, MaxDeckSize cap)

---

## QA Test Cases

*Written by qa-lead at story creation. Implement against these — do not invent new cases.*

**AC-1: Draw, play, end-of-turn transitions**
- Given: Deck=[A,B,C], Hand=[], Discard=[]
- When: Draw A → Play A (non-Exhaust) → End of turn (with B in hand, no Retain)
- Then: After draw: Deck=[B,C], Hand=[A]. After play: Deck=[B,C], Hand=[], Discard=[A]. After end-of-turn with B in hand: Discard=[A,B]
- Edge cases: Retain card B stays in hand at end of turn; Discard=[A] only

**AC-2: Reshuffle is randomized and deterministic**
- Given: Deck=[], Discard=[A,B,C,D,E] in that order; `System.Random(seed=42)`
- When: A draw is attempted
- Then: Discard reshuffles into deck; the resulting deck order is NOT [A,B,C,D,E]. Running the same test again with seed 42 produces the same shuffled order.
- Edge cases: Deck=[], Discard=[] → no card produced, no error

**AC-3: Exhaust+Retain hold is indefinite**
- Given: A card X with `Exhaust|Retain` in hand
- When: End of Turn 1 (not played), End of Turn 2 (not played), Play in Turn 3
- Then: After T1 end: X still In Hand. After T2 end: X still In Hand. After T3 play: X enters Exhausted state.
- Edge cases: X not played for 10 turns — remains in hand all 10 turns; enters Exhausted only when played

**AC-4: Innate draw respects EC13 and EC15**
- Given: Run with one Innate card I
- When: Combat 1 start: I is drawn normally (In Deck). I is played and Exhausted in Combat 1. Combat 2 start.
- Then: Combat 2 opening hand does NOT contain I (Exhausted)
- Edge cases: If I is Retained into Combat 2 (Retain+Innate), I is already In Hand — Innate draw skipped, no second copy

**AC-5: SourceSlotId-based removal**
- Given: Deck contains two copies of "scout_precision_001": one with `SourceSlotId="Weapon"`, one with `SourceSlotId=null`
- When: Weapon part is removed (remove all cards where `SourceSlotId="Weapon"`)
- Then: Deck contains exactly 1 copy of "scout_precision_001" with `SourceSlotId=null`; the null-source copy is not removed
- Edge cases: Both copies in Discard — removal still targets by SourceSlotId, location doesn't matter

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/card-system/deck-lifecycle_test.cs` — must exist and pass

**Status**: [x] `tests/unit/card-system/deck-lifecycle_test.cs` — 19 tests, all passing

---

## Dependencies

- Depends on: Story 001 must be DONE (`ICardData` and `CardKeyword` must exist)
- Unlocks: Story 008 (CardSystemDTO serializes the state managed here)

---

## Completion Notes
**Completed**: 2026-05-24
**Criteria**: 11/11 passing
**Deviations**: ADVISORY — story header references TR-card-024 for the SourceSlotId criterion; correct reference is TR-vehicle-024 ("Granted cards tracked by part identity"). Implementation is correct; TR reference in story header only.
**Test Evidence**: Logic: `tests/unit/card-system/deck-lifecycle_test.cs` (19 tests)
**Code Review**: Complete — LP-CODE-REVIEW APPROVED 2026-05-24; QL-TEST-COVERAGE ADEQUATE 2026-05-24
**Advisory items (deferred)**: O(1) draw optimization (`RemoveAt(0)` → tail-draw); `IDeckStateManager` interface; test function names omit system segment

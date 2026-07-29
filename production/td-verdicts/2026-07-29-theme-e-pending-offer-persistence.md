# TD Verdict — Theme E: Pending*Offer save DTOs

**Date:** 2026-07-29
**Slice:** TD-Audit Theme E execution (follow-on to
`2026-07-29-whole-game-health-opt-audit.md` §Theme E)
**Verdict:** APPROVE (with two amendments to null-offer wire shape)

## Files at risk

- NEW: `PendingCardOfferDto.cs`
- NEW: `PendingEventOfferDto.cs`
- NEW: `PendingCardOfferSerializable.cs`
- NEW: `PendingEventOfferSerializable.cs`
- EDIT: `RunController.cs` — add sibling method `RestorePendingOffers`
- EDIT: `SaveBootstrap.cs` — register two new adapters
- EDIT: `RunSceneHost.cs` — extend `Initialize` + `BeginRunFromLoaded`
- NEW: 6 EditMode test files under `Assets/Tests/EditMode/Save/`
  - `PendingCardOfferDto_round_trip_test.cs`
  - `PendingCardOfferDto_wire_format_test.cs`
  - `PendingCardOfferSerializable_test.cs`
  - `PendingEventOfferDto_round_trip_test.cs`
  - `PendingEventOfferDto_wire_format_test.cs`
  - `PendingEventOfferSerializable_test.cs`

## ADRs at risk

- **ADR-0004 §Decision 1** — two new `SYSTEM_ID` values must stay unique
  (`SchemaRegistry_Unique_test` catches at CI). Chosen:
  `run.pending_card_offer` + `run.pending_event_offer`.
- **ADR-0004 §Decision 7** — offers are group-of-one (asymmetric exhaustion:
  offer corruption resets to null, does NOT force session_core regen).
- **ADR-0013** — closes the implicit `OfferSeed` persistence gap (recorded
  not-discarded per the ADR's rationale).
- **ADR-0011** — `RunController.RestorePendingOffers` ships as a sibling
  method to `StartRun`, NOT a default-param overload (which would be the
  `feedback_gdd_verb_signature_not_load_bearing` trap).

## Final-game picture

A player who alt-tabs off / crashes on the reward-picker or event-convert
modal resumes back into the same modal. Non-cosmetic — reward picks are
load-bearing progression per ADR-0013.

## TD Verdict

**APPROVE — five decisions blessed, two Decision 2 amendments folded in.**

### Decision 1 — Category assignment: APPROVE
Two group-of-one categories is the correct read of ADR-0004 §Decision 7.
Offer corruption resolving to null is legible (player skips one reward) and
independent of `run.session_core` blast radius.

### Decision 2 — Null-offer wire shape: APPROVE with callout
Nullable-inner-fields avoids routine skipped-list noise in the recovery log.
Two disciplines:

- `PendingCardOfferDto.Choices` should be `null` (never `new List<>()`) for
  no-offer. Pick one sentinel, document it, enforce it in `From`. Recommend
  `null` — matches `OfferSeed` null and avoids "which sentinel means
  empty?" ambiguity.
- `PendingEventOfferDto`: the four nullable fields must be **all-null OR
  all-non-null**. `FromDto` should enforce this and treat mixed-null as
  corrupt (recover to null-offer), else the DTO invents a partial-offer
  state that no producer emits.

### Decision 3 — Setter surface: APPROVE
`internal void RestorePendingOffers(CardOffer, PendingEventOffer)` on
`RunController.cs` — sibling to `StartRun`, ADR-0011 clean. Do NOT reach
for a default-param `StartRun(..., pendingCard=null, pendingEvent=null)`
overload.

### Decision 4 — Test coverage: APPROVE
6 files matches existing DTO family cadence. Each round-trip test must
cover null-offer AND populated AND (for event DTO) mixed-null-is-corrupt
per Decision 2 amendment.

### Decision 5 — CardEffect subtype coverage: APPROVE
Add a one-line comment at `PendingCardOfferDto.SCHEMA_VERSION` const
cross-referencing `RunDeckDto.SCHEMA_VERSION` so a future subtype author
sees both bump-sites without grep.

## Three-lens self-audit

**Codebase Health.** ADR-0011 clean (sibling method, no bridges, no bimodal
paths). Subscription lifecycle N/A (save-layer, no VisualElements).
`RestorePendingOffers` on `RunController.cs` is right owner — controller
already owns `StartRun`. No teardown races (synchronous restore before
scene wiring completes).

**Optimization.** Save cadence is user-driven, not per-frame — allocation
profile is nil. `List<CardDefinitionDto>` allocation only on populated
offers. No hot-path concern.

**1.0-Shape Survival.** DTO shape survives 1.0. `PendingEventOfferDto`'s
four scalar fields are the canonical event-offer contract; adding a fifth
(e.g., event flavor id) would need `SCHEMA_VERSION=2`, which is exactly
the ADR-0004 pattern. `RestorePendingOffers` grows cleanly if a third
pending-offer type lands.

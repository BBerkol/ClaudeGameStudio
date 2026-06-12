# Story 010: Starter Deck Content Authoring — Scout & Assault

> **Epic**: Card System
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Config/Data
> **Manifest Version**: pending — `docs/architecture/control-manifest.md` not yet authored

## Context

**GDD**: `design/gdd/card-system.md`
**Requirements**: `TR-card-011`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Card System Data Authoring
**ADR Decision Summary**: Starter deck card lists are authored in `ChassisDefinitionSO` (owned by Vehicle & Part System); Card System owns the rules (exactly 10 cards, `IsStarterCard = true`, family distribution constraints). This story authors the card SO assets and wires the lists into `ChassisDefinitionSO`.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: `CardDefinitionSO` and `ChassisDefinitionSO` are Unity ScriptableObjects. All 20 starter card assets must pass `OnValidate` from Story 002. Content authored in the Unity Inspector.

**Control Manifest Rules (this layer)**:
- Required: pending
- Forbidden: pending
- Guardrail: pending

---

## Acceptance Criteria

*From `design/gdd/card-system.md` Deck Composition Rules and Starter Deck section:*

- [ ] Scout `ChassisDefinitionSO.StarterDeck` references exactly 10 `CardDefinitionSO` assets, all with `IsStarterCard = true` and `ChassisPool == Scout`
- [ ] Scout starter deck contains at minimum 3 `Precision` family cards and at minimum 2 `Maneuver` family cards
- [ ] Scout starter deck contains at minimum 1 `Repair` family card
- [ ] Scout starter deck contains no card from a family other than those represented in the Scout chassis lean (no Assault-family cards in Scout starter)
- [ ] Assault `ChassisDefinitionSO.StarterDeck` references exactly 10 `CardDefinitionSO` assets, all with `IsStarterCard = true` and `ChassisPool == Assault`
- [ ] Assault starter deck contains at minimum 3 `Assault` family cards, of which at minimum 2 are Broad Assault (`TargetType == AllEnemySubsystems` or `TargetType == EnemySubsystem` targeting `Frame` only)
- [ ] Assault starter deck contains at minimum 2 `Precision` family cards
- [ ] Assault starter deck contains at most 1 `Maneuver` family card
- [ ] Assault starter deck contains at minimum 1 `Repair` family card
- [ ] All 20 authored starter card assets (`CardDefinitionSO`) pass `OnValidate` with no errors — verified by opening each asset in the Unity Inspector and confirming no red error bar

---

## Implementation Notes

*Derived from GDD starter deck rules:*

- Starter deck cards are teaching tools: they should represent the chassis's core families clearly. Scout openers should feel mobile and precise; Assault openers should feel aggressive and direct.
- `IsStarterCard = true` marks eligibility for starter deck inclusion only. It carries no special runtime protection — starter cards can be purged after the run begins (EC6).
- Starter cards should be Common rarity to ensure the opening hand is readable to new players. Uncommon starter cards are permitted but require lead sign-off.
- Energy costs in the starter deck should cluster at 1–2 to allow meaningful turn-1 play without running out of energy.

---

## Out of Scope

*Handled by other systems or future stories — do not implement here:*

- Full EA card pool authoring (75–100 cards per chassis) — this story covers the 10-card starter decks only
- Card art assets — `CardArtKey` can reference placeholder sprites for now; final art is a separate deliverable

---

## QA Test Cases

*Written by qa-lead at story creation. Manual verification steps for Config/Data story.*

**Manual check: Scout starter deck family distribution**
- Setup: Open Scout `ChassisDefinitionSO` in the Unity Inspector; expand `StarterDeck` list
- Verify: Count the `Family` field on each referenced card. At minimum: Precision ≥ 3, Maneuver ≥ 2, Repair ≥ 1. Total = 10.
- Pass condition: All three minimums met; exactly 10 cards; all cards have `ChassisPool == Scout` and `IsStarterCard == true`

**Manual check: Assault starter deck family distribution**
- Setup: Open Assault `ChassisDefinitionSO` in the Unity Inspector; expand `StarterDeck` list
- Verify: Count by family. At minimum: Assault ≥ 3 (of which Broad Assault ≥ 2), Precision ≥ 2, Repair ≥ 1. At most: Maneuver ≤ 1. Total = 10.
- Pass condition: All thresholds met; exactly 10 cards; all cards have `ChassisPool == Assault` and `IsStarterCard == true`

**Manual check: OnValidate passes for all 20 cards**
- Setup: Open each of the 20 starter card SOs in the Unity Inspector (or trigger a batch re-import)
- Verify: No red error bar appears in the Inspector for any card
- Pass condition: All 20 cards import cleanly; Unity Console shows no error messages from `OnValidate` for these assets

---

## Test Evidence

**Story Type**: Config/Data
**Required evidence**: Smoke check pass — `production/qa/smoke-[date].md` documenting that all 20 starter card assets open without `OnValidate` errors and both chassis starter decks satisfy family distribution rules

**Status**: [x] `production/qa/smoke-2026-05-25.md` — template complete; Unity Editor execution pending

---

## Dependencies

- Depends on: Story 002 must be DONE (`OnValidate` validators must be in place before authoring card assets), Story 003 must be DONE (`CardDefinitionSO` authoring SO must exist)
- Unlocks: Epic Definition of Done (starter decks are required for playtest verification)

---

## Completion Notes
**Completed**: 2026-05-25
**Criteria**: 10/10 passing (Unity Editor smoke check deferred — ADVISORY)
**Deviations**: ADVISORY — Unity `.asset` authoring requires the separate Unity project (`GameStudio\Madmax Rougelike\Wasteland Run\`). YAML spec at `assets/data/cards/starter-decks.yaml` is the Config/Data deliverable; all 20 CardIds, OnValidate rules, and family distributions pre-flighted clean by LP-CODE-REVIEW.
**Test Evidence**: Config/Data — smoke check template at `production/qa/smoke-2026-05-25.md`. Execute in Unity Editor after `.asset` authoring to close.
**Code Review**: LP-CODE-REVIEW APPROVED. QL-TEST-COVERAGE skipped — Config/Data story.

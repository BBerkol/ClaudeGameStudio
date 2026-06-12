# QA Plan — Card System Epic
**Sprint**: Card System Epic
**Scope**: Stories 001–010 (10 stories: 9 Complete, 1 Blocked)
**Date**: 2026-05-25
**QA Lead**: qa-lead
**Stage**: Pre-Production
**Engine**: Unity 6.3 LTS

---

## 1. Story Classification Table

| # | Story | Type | Status | Test File | Test Count | Gate Level |
|---|-------|------|--------|-----------|------------|------------|
| 001 | Assembly Core Contracts | Logic | Complete | `tests/unit/card-system/assembly-contracts_test.cs` | 37 (1 [Ignore]) | BLOCKING |
| 002 | CardEffectSO Hierarchy & Validators | Logic | Complete | `tests/unit/card-system/cardeffectso-validators_test.cs` | 44 | BLOCKING |
| 003 | CardDefinitionSO & TokenResolver | Logic | Complete | `tests/unit/card-system/token-resolver_test.cs` | 18 | BLOCKING |
| 004 | ChassisMastery & Addressables Catalog | Integration | BLOCKED (Ready) | None — not implemented | — | BLOCKING |
| 005 | RewardDrawAlgorithm — Pool & Determinism | Logic | Complete | `tests/unit/card-system/reward-draw-algorithm_test.cs` | 18 | BLOCKING |
| 006 | Pity, Innate Cap & Purge Valve | Logic | Complete | `tests/unit/card-system/pity-innate-cap_test.cs` | 20 (1 [Ignore]) | BLOCKING |
| 007 | Card States & Deck Lifecycle | Logic | Complete | `tests/unit/card-system/deck-lifecycle_test.cs` | 19 | BLOCKING |
| 008 | CardSystemDTO & Save Integration | Integration | Complete | `tests/integration/card-system/cardsystem-dto-save_test.cs` | 8 | BLOCKING |
| 009 | Deck Composition Rules & Edge Cases | Logic | Complete | `tests/unit/card-system/deck-composition-edge-cases_test.cs` | 20 | BLOCKING |
| 010 | Starter Deck Content Authoring | Config/Data | Complete | `production/qa/smoke-2026-05-25.md` | — (Inspector smoke check) | ADVISORY |

**Totals (active stories)**: 9 stories in scope | 176 automated tests across 8 test files | 2 [Ignore] markers

---

## 2. Automated Test Requirements

All Logic and Integration stories require a passing automated test file before the
story is eligible for sprint sign-off. Story 004 is excluded from this cycle (see
Section 5).

### Test Files — Expected Paths and Counts

| File | Expected Count | Actual Present | Notes |
|------|----------------|----------------|-------|
| `tests/unit/card-system/assembly-contracts_test.cs` | 37 | Yes | 1 [Ignore]: AC-5c Legendary exclusion — resolution in OI-1 |
| `tests/unit/card-system/cardeffectso-validators_test.cs` | 44 | Yes | Clean |
| `tests/unit/card-system/token-resolver_test.cs` | 18 | Yes | Inspector evidence also required — see Section 3 |
| `tests/unit/card-system/reward-draw-algorithm_test.cs` | 18 | Yes | Clean |
| `tests/unit/card-system/pity-innate-cap_test.cs` | 20 | Yes | 1 [Ignore]: AC-13 persistence round-trip — resolution in OI-2 |
| `tests/unit/card-system/deck-lifecycle_test.cs` | 19 | Yes | Clean |
| `tests/integration/card-system/cardsystem-dto-save_test.cs` | 8 | Yes | Clean |
| `tests/unit/card-system/deck-composition-edge-cases_test.cs` | 20 | Yes | Clean |

**Total expected active tests**: 184 declared | 182 active (2 [Ignore])

### [Ignore] Markers

Two tests are suppressed with `[Ignore]` and must not be treated as passing evidence:

| Marker | Story | AC | Reason on Record | Resolution Path |
|--------|-------|----|------------------|-----------------|
| `[Ignore]` on AC-5c Legendary exclusion | 001 | AC-5c | Story 005 is now Complete; the logic that gates this test is implemented | Un-ignore; confirm test passes — see OI-1 |
| `[Ignore]` on AC-13 free purge valve round-trip | 006 | AC-13 | Awaiting LP confirmation that `IsFreeValveApplied` is in DTO scope | Un-ignore after LP confirms DTO field — see OI-2 |

Neither `[Ignore]` is a sprint blocker for the current cycle. Both are ADVISORY open items.

---

## 3. Manual QA Scope

Manual QA for this sprint is limited to two items. Story 004 requires no manual QA
this cycle.

### 3.1 Story 003 — Inspector Smoke-Check Evidence Confirmation

**What is needed**: Confirm that evidence file
`production/qa/evidence/story-003-inspector-smokecheck.md` exists and contains a
completed sign-off.

**Tester actions**:
1. Open `production/qa/evidence/story-003-inspector-smokecheck.md`.
2. Confirm the file is present and contains a tester name, date, and Pass verdict.
3. Confirm no Console errors were observed during the Inspector check.
4. If the file is absent or unsigned, flag to QA Lead before sprint sign-off.

**Acceptance**: File present with completed sign-off. No flag = evidence confirmed.

**Gate level**: ADVISORY — missing evidence does not block sprint sign-off but must
be resolved before the card-system story set is submitted to a milestone gate check.

---

### 3.2 Story 010 — Smoke Check Execution in Unity Editor

**What is needed**: Execute the smoke check template at
`production/qa/smoke-2026-05-25.md` inside the live Unity project and sign off the
result.

**Tester actions**:
1. Open Unity project at `GameStudio\Madmax Rougelike\Wasteland Run\`.
2. Confirm all pre-conditions in the template are met before proceeding.
3. Execute Check 1 (Scout StarterDeck), Check 2 (Assault StarterDeck), and
   Check 3 (OnValidate for all 20 cards) as documented in the template.
4. Fill in the Actual column and Pass? column for each row in the template.
5. Record the Overall Verdict and sign the template (tester name + date).

**Acceptance**: All 3 checks PASS; template signed off in the file.

**Gate level**: ADVISORY — a FAIL does not block the current sprint sign-off but
constitutes an open bug against Story 010 and must be triaged before any milestone
build.

---

## 4. Out of Scope

The following items are explicitly excluded from this QA cycle:

| Item | Reason |
|------|--------|
| Story 004 — ChassisMastery & Addressables Catalog | BLOCKED (Ready): story not implemented; ADR-0008 is Proposed, not Accepted. No code to test. |
| IL2CPP smoke test for Story 004 | Deferred — depends on Story 004 implementation. |
| Addressables catalog integration test | Deferred — no catalog asset exists until Story 004 is implemented. |

Story 004 must have an integration test file at
`tests/integration/card-system/chassismastery-addressables_test.cs` (or equivalent
agreed path) before it can be considered for sprint sign-off. The BLOCKING gate
applies when the story is implemented.

---

## 5. Entry Criteria

QA execution must not begin until all of the following are true:

- [ ] Smoke check template exists at `production/qa/smoke-2026-05-25.md` — **CONFIRMED**
- [ ] All 8 test files listed in Section 2 are present on disk — **CONFIRMED**
- [ ] Unity project builds without compiler errors on the current branch
- [ ] Unity Test Runner reports all active (non-[Ignore]) tests passing — 182 of 184 expected
- [ ] Story 004 confirmed excluded from this cycle by producer or QA Lead

---

## 6. Exit Criteria

The QA cycle for the Card System Epic sprint is complete when all of the following are satisfied:

**Automated (BLOCKING — must all pass)**
- [ ] All 182 active automated tests pass in Unity Test Runner (no newly failing tests)
- [ ] No test file listed in Section 2 is missing from disk
- [ ] Neither [Ignore] marker has been silently removed without an accompanying passing test

**Manual (ADVISORY — document outcome either way)**
- [ ] Story 003 Inspector smoke-check evidence file confirmed at
      `production/qa/evidence/story-003-inspector-smokecheck.md` with completed sign-off,
      OR the absence is logged as an open item with triage date
- [ ] Story 010 smoke check template executed, all 3 checks filled in, and Overall Verdict
      recorded — PASS or FAIL with notes

**Open Items**
- [ ] All 4 open items in Section 7 have a recorded resolution decision (accept / resolve / defer),
      even if not yet actioned

**Sign-off**
- [ ] QA Lead signs the plan below
- [ ] Sprint review can proceed once automated gate passes and manual items are documented

---

## 7. Open Items Register

| ID | Story | Item | Severity | Owner | Recommended Resolution |
|----|-------|------|----------|-------|------------------------|
| OI-1 | 001 | `[Ignore]` on AC-5c Legendary exclusion test | Advisory | Lead Programmer | Story 005 is Complete; the blocking condition no longer applies. Remove `[Ignore]`, confirm test passes, commit. If test fails, treat as a Story 001 regression and open a bug report. |
| OI-2 | 006 | `[Ignore]` on AC-13 free purge valve persistence round-trip | Advisory | Lead Programmer | LP to confirm whether `IsFreeValveApplied` is in the `CardSystemDTO` scope. If yes: remove `[Ignore]`, implement and pass the test. If no: document the gap in the DTO design doc and accept as a known coverage gap. |
| OI-3 | 003 | Inspector smoke-check evidence file not confirmed present | Advisory | QA Tester | Tester to locate or produce `production/qa/evidence/story-003-inspector-smokecheck.md` and confirm it contains a completed sign-off. If absent, execute the Inspector check and create the file. |
| OI-4 | 010 | Smoke check template not yet executed in Unity Editor | Advisory | QA Tester | Execute all 3 checks per Section 3.2. Record verdict in `production/qa/smoke-2026-05-25.md`. If FAIL, open a bug report against Story 010 (S2 at minimum if card count or family distribution is wrong; S3 if isolated OnValidate error). |

---

## 8. Sign-Off

| Field | Value |
|-------|-------|
| **QA Lead** | |
| **Date** | |
| **Automated gate verdict** | |
| **Manual QA verdict** | |
| **Sprint sign-off verdict** | |

**Notes**:

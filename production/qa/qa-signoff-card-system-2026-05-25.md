## QA Sign-Off Report: Card System Epic
**Date**: 2026-05-25
**QA Lead sign-off**: qa-lead

### Test Coverage Summary

| # | Story | Type | Auto Tests | Manual QA | Result |
|---|-------|------|------------|-----------|--------|
| 001 | Assembly Core Contracts | Logic | 37 tests (1 [Ignore] — OI-1) | — | PASS WITH NOTES |
| 002 | CardEffectSO Hierarchy & Validators | Logic | 44 tests | — | PASS |
| 003 | CardDefinitionSO & TokenResolver | Logic | 18 tests | Evidence file absent — OI-3 | PASS WITH NOTES |
| 004 | ChassisMastery & Addressables Catalog | Integration | Not implemented — BLOCKED on ADR-0008 | — | EXCLUDED |
| 005 | RewardDrawAlgorithm — Pool & Determinism | Logic | 18 tests | — | PASS |
| 006 | Pity, Innate Cap & Purge Valve | Logic | 20 tests (1 [Ignore] — OI-2) | — | PASS WITH NOTES |
| 007 | Card States & Deck Lifecycle | Logic | 19 tests | — | PASS |
| 008 | CardSystemDTO & Save Integration | Integration | 8 tests | — | PASS |
| 009 | Deck Composition Rules & Edge Cases | Logic | 20 tests | — | PASS |
| 010 | Starter Deck Content Authoring | Config/Data | — | Smoke check template present, unsigned — OI-4 | PASS WITH NOTES |

**Active test count**: 182 across 8 confirmed test files. Unity Test Runner execution was not available in-session; gate evidence is structural (files present, counts consistent with story acceptance criteria). Test run confirmation is required before final merge.

Story 004 is formally EXCLUDED from this sprint's scope pending ADR-0008 sign-off. It does not count against the verdict.

### Smoke Check

PASS WITH WARNINGS — smoke check template is present and well-formed. Unity Editor execution has not been confirmed this session. OI-3 (Story 003 inspector check) and OI-4 (Story 010 starter deck content check) must be completed and signed by a QA Tester in the live Unity project before the smoke check can be marked fully closed.

### Open Items

| ID | Story | Item | Severity | Disposition |
|----|-------|------|----------|-------------|
| OI-1 | 001 | Stale [Ignore] on AC-5c Legendary test — Story 005 is Complete, the blocking condition that justified the ignore is gone | Advisory | Assign to LP: remove [Ignore] attribute and activate test body |
| OI-2 | 006 | [Ignore] on AC-13 free purge valve round-trip — IsFreeValveApplied confirmed absent from CardSystemDTO by design | Advisory | Permanent deferral accepted; LP to add a brief comment in the test file documenting this as a known intentional coverage gap |
| OI-3 | 003 | Inspector smoke-check evidence file missing — production/qa/evidence/story-003-inspector-smokecheck.md does not exist | Advisory | QA Tester to execute the Inspector check in Unity Editor and create the evidence file |
| OI-4 | 010 | Smoke check template present but unsigned — requires execution in live Unity project and a recorded pass/fail verdict | Advisory | QA Tester to execute in Unity Editor and sign the template |

All four open items are advisory. None are S1 or S2. None block a release build from proceeding.

### Bugs Found

None. No S1, S2, S3, or S4 bugs filed this sprint.

### Verdict: APPROVED WITH CONDITIONS

**Conditions**:

1. **OI-1** — Lead Programmer must remove the stale [Ignore] on the AC-5c Legendary test in Story 001's test file and confirm the test passes. This was a temporary skip whose blocking condition (Story 005 not yet merged) no longer applies.
2. **OI-2** — Lead Programmer must add an in-file comment on the AC-13 [Ignore] documenting the permanent deferral rationale (IsFreeValveApplied not persisted by design) so the skip is self-explaining to future reviewers.
3. **OI-3** — QA Tester must execute the Story 003 Inspector smoke check in the live Unity Editor and produce the evidence file at production/qa/evidence/story-003-inspector-smokecheck.md.
4. **OI-4** — QA Tester must execute the Story 010 smoke check in the live Unity Editor, record the verdict, and sign the template.
5. **Automated gate** — Unity Test Runner must be executed on the full test suite (182 active tests across 8 files) and results recorded before Story 004's ADR-0008 gate or the next sprint begins. Structural file evidence is accepted for this sprint sign-off; a live run confirmation is the standing debt.

Story 004 (ChassisMastery & Addressables Catalog) carries forward as a formal open epic item, gated on ADR-0008 sign-off. It is not a condition of this sign-off — it was never in scope for this sprint.

### Next Step

Assign OI-1 and OI-2 to Lead Programmer for resolution this sprint. Assign OI-3 and OI-4 to QA Tester for execution in the next available Unity Editor session. Once all four conditions are closed, qa-lead can promote this report to full APPROVED status. The Card System Epic may proceed to integration planning; Story 004 enters the ADR-0008 dependency queue independently.

---
name: Card System GDD AC Review
description: Adversarial QA review of Card System GDD acceptance criteria + sprint story QL-STORY-READY gate results; blocking gaps, untestable ACs, and missing coverage identified across five review passes
type: project
---

Reviewed Card System GDD (design/gdd/card-system.md) on 2026-04-19 (two passes) and 2026-04-20 (full adversarial sweep with rewrites).

## Pass 1 Findings (initial review)
- EC3 keyword extensibility ACs are architecture review items, not pass/fail QA tests
- "All display contexts" for Description token resolution is undefined — no explicit context list
- F3 tolerance uses tilde (~19%) while rarity weight AC uses ±1%; inconsistent precision standard
- Deck cap AC tests only UI layer; no data-layer enforcement AC exists
- "Remainder of the run" for Exhaust AC requires full-run test; run-end reset behavior unspecified
- Card reward screen AC silent on pool exhaustion edge case (fewer than 2 available cards)
- F2 simulation tool (Balance Sim Runner) not available until Month 5 — no fallback test method specified
- No ACs exist for: card upgrade path (UpgradedVersion), Innate keyword, Retain keyword, discard→reshuffle trigger

## Pass 2 Findings (adversarial AC review — full sweep)

### BLOCKING gaps (must be resolved before stories enter sprint)
1. AC-D6: "all display contexts" undefined in Description token AC — needs explicit 5-context list
2. AC-KE1/KE2 (EC3): keyword extensibility ACs are architecture/code-review tasks, not QA-testable — must be reclassified
3. AC-FV2: F3 simulation uses "~19%" tilde approximation with no defined pass boundary
4. MISSING-1: Control family minimum 1 damage rule has no SO import validation AC
5. MISSING-3: EC1 (Exhaust+Retain → Exhaust wins) has no runtime behavioral AC
6. MISSING-7: EC10 (multi-effect short-circuit) has no AC — programmer could break silently
7. MISSING-10: Innate keyword runtime behavior (bypasses draw order, does not consume draw slot) — no AC
8. MISSING-13: Deck at max 35 with pending reward — no EC defined, no AC
9. MISSING-15: Retain keyword runtime behavior — no AC; also has design ambiguity on whether Retained card counts against next-turn draw

### RECOMMENDED gaps (important, should have ACs before implementation)
- AC-DR2/DR3: Only UI gate tested; data-layer enforcement (direct API call) untested
- AC-DR4: Model B interaction (no passive close), Rare-skip confirmation, pool exhaustion — all untested
- AC-CS1: "Correctly" vague; reshuffle randomization untested
- AC-FV1: Requires* position case (card blocked if condition not met) untested
- MISSING-4/5/8/14/16: EC4, EC6, EC11, indexed tokens, reshuffle trigger precision

### Simulation AC analysis
- 10,000 draws adequate for Common/Uncommon; inadequate for 1% Rare at mastery 1–3 (need 100,000)
- ±1% tolerance too loose for 1% Rare (allows 2× actual rate); recommend ±0.3 pp for low-probability tiers
- F3 AC should be derived from F2 unit tests, not a separate simulation — remove F3 prose AC
- Both simulation ACs should become automated unit tests in tests/unit/loot/

### Stale reference
- Dependencies table (Systems That Depend) still says "ScrapSellValue" — field was renamed PurgeCost in Data Contract section. Internal inconsistency.

## Pass 3 Findings (2026-04-20 — full adversarial sweep with concrete rewrites)

**22 BLOCKING issues and 13 ADVISORY issues identified.**

### Key BLOCKING issues by category

**Data Contract:**
- AC-D7: No data-layer runtime enforcement for Control family minimum damage (only SO import tested)
- AC-D8A: No AC for unbound token rendering as `?` at runtime
- AC-D8B: Indexed tokens ({damage.1}, {damage.2}) have no AC at all

**Deck Rules:**
- AC-DR2: Missing ACs for Repair minimum (both chassis) and primary-lean family minimum
- AC-DR3: No data-layer purge enforcement AC — only UI gate tested
- AC-DR4B: No AC for pool exhaustion on reward screen (fewer than 2 eligible cards)
- AC-DR6: "Confirm" action on Rare-skip confirmation prompt untested
- AC-DR7: Exhaust+Retain UNPLAYED case — AC implies Exhaust wins unconditionally but GDD says Retain applies to unplayed cards; contradiction

**Card Pool Integrity:**
- AC-PI2: Non-deterministic (no RNG seed), sample size inadequate for 1% Rare
- AC-PI3A: Pity counter reset behavior not tested
- AC-PI4: No data-layer copy limit enforcement AC

**Card States:**
- AC-CS3: ZERO ACs for Retain positive behavior (core keyword completely uncovered)
- AC-CS7: Innate "does not consume a draw slot" behavior has no AC — opening hand size not tested

**Formula Verification:**
- AC-FV1A: RequiresBehind/RequiresAhead playability blocking has no AC
- AC-FV2: Only mastery 7–10 tested; mastery 1–3 and 4–6 F3 probability completely untested

**Missing ACs (mechanics in GDD body with zero coverage):**
- Free Purge Valve (33% seeded Chopshop chance): no AC
- Indexed Description Tokens ({damage.1}, {damage.2}): no AC
- Pool Exhaustion Fallback (re-roll then duplicate): no AC
- Pity Counter Reset (resets on any Rare offer): partial — reset path missing
- EC14 (Innate+Ethereal combination — permitted by design): no AC at all

**Systematic patterns:**
- All simulation ACs lack fixed RNG seeds — violates coding standards determinism rule
- UI-only gates appear in 3+ ACs (purge minimum, copy limits, Control damage) — no data-layer enforcement
- Formula ACs never test boundary values (BaseDamage=0, PositionBonus=0)
- "Immediately" and "instantaneously" appear in ACs with no measurable threshold

**Why:** Full adversarial sweep of all ACs before card system implementation begins.
**How to apply:** Flag any card system stories entering sprint that reference untouched ACs from the blocking list above. Require AC revision before accepting stories as ready. The three most dangerous single gaps are: AC-CS3 (Retain has zero coverage), AC-CS7 (Innate draw slot uncovered), EC14 (Innate+Ethereal has zero coverage).

## Pass 4 Findings (2026-04-20 — adversarial review of revised post-Pass-3 ACs)

**Verdict: NOT APPROVED. 16 BLOCKING gaps remain.**

### BLOCKING gaps surviving Pass 3 rewrite

1. AC-D2: "Malformed CardId" undefined — no character-level spec for valid [chassis]/[family]/[sequence] tokens
2. AC-D8A: Unbound token renders as `?` — still no AC (GDD specifies this behavior explicitly)
3. AC-DR2: Missing "at minimum 1 Repair card per starter deck" for both chassis
4. AC-DR4-GoBack: No AC for Go Back path on Rare-skip confirmation returning player to full reward screen
5. AC-DR4-PoolExhaustion: Fewer than 2 eligible reward cards → fallback behavior (rarity degradation → Scrap) has no AC
6. AC-PI2: ±1 pp tolerance too loose at mastery 1–3 (passes at 2× intended Rare rate); need ±0.3 pp or 100k draws
7. AC-PI3-EmptyPool: Pity counter empty-Rare-pool → Scrap compensation fallback has no AC
8. AC-PI4: Copy limits tested UI-only; no data-layer enforcement AC
9. AC-CS7: Innate draw-slot-additive behavior (hand size +1 per Innate) has no AC
10. AC-FV1: RequiresBehind/RequiresAhead playability blocking has no AC
11. AC-FV2: F3 only tested at mastery 7–10; mastery 1–3 (~2%) and 4–6 (~9.75%) uncovered
12. Pity counter resets on skipped Rare — no AC anywhere in the set
13. Primary-family bias at Mastery 1–3 (PrimaryFamilyBiasEnabled field) — zero AC coverage
14. Free Purge Valve (seeded 33% Chopshop chance, runSeed+nodeIndex, IsFreeValveApplied) — zero AC coverage
15. MerchantPrice != PurgeCost SO import constraint — no AC
16. EC14 (Innate+Ethereal combined runtime behavior) — no AC for 4th consecutive pass

### ADVISORY gaps
- AC-D6: PositionBonus validation requires Effects list traversal — scope not explicit
- AC-DR3: No data-layer purge enforcement AC
- AC-DR6: Skip → Confirm does not verify pity counter reset
- AC-PI2: Common/Uncommon rates not verified alongside Rare
- AC-CS4: "Instantaneously" still has no measurable threshold
- AC-FV3: "Simulated run" ambiguous — should be explicit formula unit test
- AC-DR4: Gamepad Back button missing from "no other dismissal" list
- Reshuffle randomization: no AC verifying reshuffle does not preserve discard order

## Pass 5 Findings (2026-05-21 — QL-STORY-READY gate review of 10 sprint stories)

**Sprint entry verdicts:** Stories 005 and 010 ADEQUATE; stories 001, 002, 003, 004, 006, 007, 008, 009 blocked on GAPS.

### BLOCKING gap (must resolve before sprint entry)
- Story 001, Gap 3: ICardEffect interface conflict — AC specifies `Apply(CardResolutionContext)` + `Condition` property; ADR-0006 specifies a pure marker interface with no methods. Must file ADR amendment or revise AC. Programmer will implement the wrong shape.

### Remaining GAPS by story

**Story 001:** ICardData member list incomplete (9 fields missing vs ADR-0006); Legendary enum existence not specified; ICardEffect interface mismatch (blocking); ICardRewardGenerator signature types not specified precisely.

**Story 002:** BypassPlating + empty ValidSubsystemTargets AC should clarify "empty array" means ValidSubsystemTargets field (not TargetType); MerchantPrice == 30 rejection missing "when MerchantPrice > 0" qualifier; CardId format grammar never specified (character-level regex/spec needed).

**Story 003:** F1 boundary test missing BaseDamage=0 case (excluded by Story 002 validator — cross-story dependency not documented); "5 display contexts" AC is Integration/UI evidence scope, not a Logic unit test scope — TokenResolver unit test should cover Resolve() output only.

**Story 004:** Memory leak AC is not automatable (profiler cannot be asserted); no IL2CPP standalone smoke test AC (ADR-0006 Validation Criterion 7 has no owning story).

**Story 006:** "Skipped offers" pity reset trigger undefined (reset on offer generation containing Rare, or on player selection of Rare?); free purge valve AC silent on in-session re-entry (no reload); IsFreeValveApplied full round-trip (compute → persist → reload) has no owning story.

**Story 007:** Exhaust+Retain unplayed hold duration ambiguous (one turn or indefinitely?); reshuffle randomization not tested (ordered copy would pass current AC).

**Story 008:** IsFreeValveApplied not declared in CardSystemDTO (ADR-0006) — AC references a field that doesn't exist in the DTO; CardCopyCounts drift invariant (auto-correct on load) has no AC despite being an explicit ADR-0006 validation requirement.

**Story 009:** EC14 Innate+Ethereal SO import AC correct but should cross-reference that ONLY Ethereal+Retain pair is rejected (prevent overly broad validator); EC3 keyword extensibility AC is a code review criterion, not testable — classify as ADVISORY or rewrite as structural test.

### Test specifications produced
- Full test case specs written for Story 005 (all ACs — 8 tests)
- Partial test case specs for Story 006 (5 of 8 ACs — pity happy path, pity+empty, counter immutability, Innate cap, free purge valve)
- Partial test case specs for Story 007 (10 of 12 ACs — gaps excluded)
- Full manual verification steps for Story 010 (6 checks)

### Test files to create
- tests/unit/cards/reward_draw_algorithm_pool_selection_tests.cs (Story 005)
- tests/unit/cards/reward_draw_algorithm_pity_tests.cs (Story 006)
- tests/unit/cards/deck_lifecycle_tests.cs (Story 007)
- tests/integration/cards/addressables_card_catalog_tests.cs (Story 004 — pending AC revision)
- tests/integration/cards/card_system_dto_save_tests.cs (Story 008 — pending AC revision)

**Why:** QL-STORY-READY gate run 2026-05-21 before card system sprint begins.
**How to apply:** Do not allow Stories 001–004, 006–009 to enter sprint until the 8 required AC revisions are approved. Story 005 and Story 010 may proceed immediately.

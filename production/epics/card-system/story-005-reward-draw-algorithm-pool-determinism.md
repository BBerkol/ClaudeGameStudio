# Story 005: RewardDrawAlgorithm — Pool Selection & Determinism

> **Epic**: Card System
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: pending — `docs/architecture/control-manifest.md` not yet authored

## Context

**GDD**: `design/gdd/card-system.md`
**Requirements**: `TR-card-003`, `TR-card-012`, `TR-card-013`, `TR-card-015`, `TR-card-016`, `TR-card-021`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Card System Data Authoring; ADR-0003: Deterministic RNG Discipline
**ADR Decision Summary**: `RewardDrawAlgorithm` composes rarity weights, primary-family bias (Slot 1 at Mastery 1–3), and copy-limit fallback into a single deterministic draw that consumes a caller-owned `System.Random` per ADR-0003. `UnityEngine.Random` is forbidden in the draw path. The algorithm is a pure function: same seed + same state → identical output every time.

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: `RewardDrawAlgorithm` lives in `WastelandRun.Cards` (engine-free). `System.Random` must be passed by the caller — the algorithm must never construct `new System.Random()` internally. ADR-0003 strictly forbids `UnityEngine.Random` in any seeded system.

**Control Manifest Rules (this layer)**:
- Required: pending
- Forbidden: `UnityEngine.Random` in draw path
- Guardrail: pending

---

## Acceptance Criteria

*From `design/gdd/card-system.md` sections F2, F3, and deck composition rules:*

- [ ] `RewardDrawAlgorithm` consumes a caller-provided `System.Random` — no `UnityEngine.Random` calls anywhere in the draw path (verified by CI grep or code review)
- [ ] Rarity draw weight simulation — all tests use `new System.Random(seed: 42)` and must produce identical results on every run:
  - Mastery 1–3: 100,000 draws, Rare rate in range **0.7%–1.3%** (tighter tolerance required because 1% rate makes ±1pp equivalent to ±100% relative error)
  - Mastery 4–6: 10,000 draws, Rare rate **4.4%–5.6%**
  - Mastery 7–10: 10,000 draws, Rare rate **9.4%–10.6%**
- [ ] Pool exhaustion fallback: if the Rare pool is empty (all Rares at copy limit), draw degrades to Uncommon; if Uncommon also empty, degrades to Common; if all three tiers fully exhausted, returns Scrap compensation result (not a card offer)
- [ ] Primary-family bias (Mastery 1–3): in 10,000 simulated Scout reward offers with seed 42, Slot 1 contains a `Precision` or `Maneuver` card in **≥98%** of offers where the primary-family pool is non-empty
- [ ] Primary-family bias disabled at Mastery 4+: both slots draw from full chassis pool using F2 weights only — no slot-force applied
- [ ] Degenerate pool fallback: when primary-family pool is entirely at copy limit, both slots draw from full chassis pool with no error and no partial result
- [ ] Copy limits enforced before weight normalization: cards at copy limit (3 copies Common/Uncommon, 1 copy Rare) are excluded from the draw pool; weight normalization is applied to the remaining eligible cards only
- [ ] Two-card offer: Slot 1 and Slot 2 never produce the same card (without-replacement constraint holds through all fallback paths)
- [ ] `ICardRewardGenerator.Generate()` with `drawCount=2` returns exactly 2 drafts when an eligible pool exists; returns empty array (length 0, never null) when the chassis-filtered pool is entirely exhausted
- [ ] Determinism regression: 10,000-sample seed replay produces byte-identical output — same seed + same `currentDeckCardIds` + same `mastery` → identical `CardDraft[]` every run
- [ ] `CardDraft.RulesText` is pre-templated: `ICardRewardGenerator.Generate()` calls `TokenResolver.Resolve()` on each card before populating `CardDraft.RulesText` — the returned `RulesText` contains no `{param}` patterns; the presentation layer renders it verbatim with no further substitution (TR-card-003)

---

## Implementation Notes

*Derived from ADR-0006 and ADR-0003 Implementation Guidelines:*

- The algorithm receives `System.Random rng` from the caller and advances it in sequence: first draw for Slot 1, then draw for Slot 2. The caller is responsible for not reusing the same `rng` instance across independent reward draws.
- F2 weight normalization: `P(rarity) = W(rarity) / (W(Common) + W(Uncommon) + W(Rare))`. Weights are loaded from `ChassisMasteryDefinitionSO` at runtime — they are never hardcoded in the algorithm.
- Pool exhaustion fallback is tier-sequential: Rare → Uncommon → Common → Scrap. The fallback does NOT loop back to Rare. The Scrap compensation result must be distinguishable from a card result in the return type (use a discriminated union or a nullable pattern per ADR-0006).
- Primary-family bias: Slot 1 draw filters the pool to `PrimaryFamily` cards first, applies F2 weight normalization within that subset, then draws. If the subset is empty, fall back to full pool for Slot 1 (not a hard error). Slot 2 always uses the full pool minus Slot 1's drawn card.
- `RewardDrawAlgorithm` must NOT write to `rarePityCounter` — it is a read-only `int` parameter. Counter authority belongs to `LootStateDTO` in the Loot & Reward system.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- **Story 006**: Pity counter logic, Innate cap filtering, and free purge valve seeding — these are separate algorithm concerns
- **Story 004**: Loading the card catalog that feeds the draw pool
- **Story 003**: `TokenResolver.Resolve()` implementation — this story consumes it; Story 003 must be DONE

---

## QA Test Cases

*Written by qa-lead at story creation. Implement against these — do not invent new cases.*

**AC-1: No UnityEngine.Random in draw path**
- Given: `RewardDrawAlgorithm` source files
- When: CI grep runs for `UnityEngine.Random` in `WastelandRun.Cards`
- Then: Zero matches
- Edge cases: Transitive calls through helper methods must also be checked

**AC-2: Rarity draw weight simulation — Mastery 1–3**
- Given: 100,000 calls to `Generate()` with seed 42, `drawCount=1`, full uncapped pool, mastery tier 1
- When: Rare card count is tallied
- Then: Rare count / 100,000 is in range 0.007–0.013
- Edge cases: Same test run twice must produce the identical Rare count (determinism check)

**AC-3: Pool exhaustion fallback**
- Given: A pool where all Rares are at copy limit; Uncommon pool has 2 eligible cards; Common pool has 5 eligible cards
- When: `Generate()` draws with `mastery=1` (would normally draw Rare 1% of time)
- Then: All draws return Uncommon or Common cards; no Rare is returned; no exception
- Edge cases: All three tiers exhausted → returns Scrap compensation result (length 0 or dedicated result type)

**AC-4: Primary-family bias — ≥98% Slot 1 is primary family**
- Given: 10,000 simulated Scout Mastery 1–3 offers, seed 42, non-empty Precision+Maneuver pool
- When: Slot 1 family is recorded for each offer
- Then: (Precision + Maneuver count) / 10,000 ≥ 0.98
- Edge cases: When primary-family pool is fully exhausted, both slots draw from full pool — verify no crash

**AC-5: Without-replacement in two-card offer**
- Given: 1,000 calls to `Generate(drawCount=2)` with seed 42
- When: Each returned `CardDraft[2]` is inspected
- Then: `draft[0].CardId != draft[1].CardId` in all 1,000 cases
- Edge cases: Pool with exactly 1 eligible card → `Generate(drawCount=2)` returns length 1 or Scrap compensation (not a crash)

**AC-6: CardDraft.RulesText pre-templated (TR-card-003)**
- Given: A mock card with `DescriptionTemplate = "Deal {damage} and restore {armor} armor"` and effects `DamageEffect(Amount=4)`, `RestoreArmorEffect(Amount=2)`
- When: `ICardRewardGenerator.Generate()` returns a `CardDraft[]`
- Then: `CardDraft.RulesText` equals `"Deal 4 and restore 2 armor"` — no `{param}` patterns remain
- Edge cases: Card with no matching effect for a token → token resolves to `"?"` in `RulesText` (not a crash, not null)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/card-system/reward-draw-algorithm_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003 must be DONE (`TokenResolver.Resolve()` must exist for `RulesText` pre-baking); Story 004 must be DONE (ChassisMasteryDefinitionSO and catalog must be loadable)
- Unlocks: Story 006 (pity + Innate cap logic builds on this algorithm)

## Completion Notes
**Completed**: 2026-05-22
**Criteria**: 11/11 passing (all automatable ACs covered)
**Deviations**: ADVISORY — `SelectionHash` omits `runSeed` from ADR-0006 Risks table formula; hash is deterministic (`ComputeDeterministicHash` + `StringComparer.Ordinal`) but same card+position across different runs produces the same hash (telemetry collision risk). Recommend adding `runSeed` in a follow-up. ADVISORY — `TokenResolver` injected as concrete type (not `ITokenResolver`). ADVISORY — duplicate `FilterByRarity`/`FilterByRarityReadOnly` overloads.
**Test Evidence**: Logic — `tests/unit/card-system/reward-draw-algorithm_test.cs` (18 tests)
**Code Review**: Complete — LP-CODE-REVIEW: APPROVED (4 blocking items resolved); QL-TEST-COVERAGE: ADEQUATE (3 blocking items resolved)

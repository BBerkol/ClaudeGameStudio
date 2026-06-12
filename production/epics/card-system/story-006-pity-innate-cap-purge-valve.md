# Story 006: RewardDrawAlgorithm — Pity System, Innate Cap & Purge Valve

> **Epic**: Card System
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: pending — `docs/architecture/control-manifest.md` not yet authored

## Context

**GDD**: `design/gdd/card-system.md`
**Requirements**: `TR-card-014`, `TR-card-010`, `TR-card-019`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Card System Data Authoring; ADR-0003: Deterministic RNG Discipline
**ADR Decision Summary**: Pity counter authority belongs exclusively to Loot & Reward's `LootStateDTO` — `RewardDrawAlgorithm.Generate` reads `rarePityCounter` as a read-only `int` and signals Rare presence via `CardDraft.Rarity`; it does NOT mutate any external counter. Innate cap (max 3 per deck) filters the offer pool before draw. Free purge valve uses a deterministic `System.Random(runSeed ^ nodeIndex)` and must read persisted state on reload, never re-roll.

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: Free purge valve determinism uses `runSeed ^ nodeIndex` (XOR) as the RNG seed per ADR-0003 Rule 3. XOR cannot overflow by definition — no `unchecked` wrapper is needed.

**Control Manifest Rules (this layer)**:
- Required: pending
- Forbidden: `UnityEngine.Random` in seeded systems
- Guardrail: pending

---

## Acceptance Criteria

*From `design/gdd/card-system.md` pity counter rules, keyword section, and deck composition rules:*

- [ ] **Pity happy path**: given 8 consecutive `Generate()` calls producing no Rare, the 9th call with `rarePityCounter=8` returns a `CardDraft[]` containing at least one card with `Rarity == CardRarity.Rare`. Test is deterministic using `new System.Random(seed: 42)`
- [ ] **Pity + empty Rare pool**: when all Rares are at copy limit (pool empty) and `rarePityCounter=8`, `Generate()` returns Scrap compensation (no Rare card); counter resets to 0 signal. On the next 8 Rare-free calls followed by a 9th with `rarePityCounter=8`, Scrap compensation fires again. Test is deterministic (seed 42)
- [ ] **Pity F2 fallback independence**: when pity fires on empty Rare pool, the result is Scrap compensation — NOT a degraded Uncommon draw. The F2 tier-degradation path (Rare→Uncommon→Common) is not triggered by a pity event. Verified by asserting the compensation result type is distinct from a card result
- [ ] **Pity counter authority**: `RewardDrawAlgorithm.Generate()` does NOT mutate `rarePityCounter` — it is a read-only `int` parameter. Verified by code review that no writes to any external pity field occur inside the algorithm
- [ ] **Pity reset signal**: the reset trigger is the presence of `Rarity == CardRarity.Rare` in the returned `CardDraft[]` — not player action. A unit test documents this explicitly: "Pity counter reset is Loot & Reward's responsibility; Algorithm signals Rare presence via `CardDraft.Rarity`"
- [ ] **Merchant Rare does NOT reset pity**: a test explicitly documents that `rarePityCounter` passed to `Generate()` is not affected by Merchant purchases — the counter is owned by Loot & Reward which tracks combat reward drought only
- [ ] **Innate cap at 3**: a deck containing 3 Innate-keyword cards — `Generate()` receives a `currentDeckCardIds` list that causes the Innate card count to be 3 — Innate cards are excluded from the returned `CardDraft[]`
- [ ] **Innate cap at 2**: a deck containing exactly 2 Innate cards — Innate cards appear normally in the returned drafts
- [ ] **Free purge valve — determinism**: `new System.Random(runSeed ^ nodeIndex)` produces a deterministic `IsFreeValveApplied` result. Given `runSeed=1, nodeIndex=3`, the computed value is identical on every call with the same inputs
- [ ] **Free purge valve — in-session re-entry**: entering the same Chopshop node twice in the same session (same `runSeed`, same `nodeIndex`, no save/reload between entries) produces the same `IsFreeValveApplied` value — the valve is not re-rolled on re-entry. The value is computed once and cached for the node visit duration
- [ ] **Free purge valve — distribution**: approximately 33% of 10,000 simulated Chopshop entries yield `IsFreeValveApplied = true` (pass range: 30%–36%). Test uses deterministic seed enumeration and must produce the same count every run
- [ ] **Cross-story dependency note**: the full round-trip test (compute `IsFreeValveApplied` → persist → reload → assert same value) is blocked until Story 008 resolves the `IsFreeValveApplied` ownership in the save DTO. Story 006 is Complete when all other ACs above pass. Stories 006 + 008 together must satisfy the persistence round-trip before the epic DoD is signed.

---

## Implementation Notes

*Derived from ADR-0006 and ADR-0003 Implementation Guidelines:*

- Pity fires when `rarePityCounter >= 8` (the 9th offer, counting from 0). The algorithm checks this condition at the start of `Generate()` before running F2 weights.
- When pity fires and the Rare pool is non-empty: set `useGuaranteedRare = true` and draw Slot 1 from the Rare pool only. Slot 2 runs standard F2.
- When pity fires and the Rare pool is empty: return Scrap compensation. Do not attempt to draw a card. The compensation result must signal the counter reset to Loot & Reward (either via a special return value type or a flag on the result).
- Innate cap filtering: before the pool draw, count all `ICardData` entries in `currentDeckCardIds` where `(card.Keywords & CardKeyword.Innate) != 0`. If count >= 3, remove all Innate-keyword cards from the draw pool before weight normalization.
- Free purge valve seed: `runSeed ^ nodeIndex` per ADR-0003 Rule 3. XOR cannot overflow — no `unchecked` wrapper is needed. The valve computation is a single `new System.Random(runSeed ^ nodeIndex).NextDouble() < 0.33` call per ADR-0003 Rule 4 (standalone utility entry point).
- The valve value must be cached on the Chopshop node's runtime state object. On re-entry within the same session, read from the cache, not from a new RNG call.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- **Story 005**: Core pool selection, copy-limit filtering, and deterministic draw
- **Story 008**: Persisting `IsFreeValveApplied` across save/reload (the round-trip test)

---

## QA Test Cases

*Written by qa-lead at story creation. Implement against these — do not invent new cases.*

**AC-1: Pity happy path**
- Given: A call to `Generate()` with `rarePityCounter=8`, seed 42, non-empty Rare pool
- When: Result is inspected
- Then: At least one `CardDraft` has `Rarity == Rare`
- Edge cases: `rarePityCounter=7` (one short) — no guarantee; `rarePityCounter=9` — still fires (≥8 threshold)

**AC-2: Pity + empty Rare pool → Scrap compensation**
- Given: `rarePityCounter=8`, all Rares at copy limit
- When: `Generate()` is called
- Then: Returns Scrap compensation result (not a card); counter reset signal is present
- Edge cases: After compensation, `rarePityCounter=8` again on the next call → same Scrap result

**AC-3: Innate cap filtering**
- Given: A `currentDeckCardIds` list that resolves to 3 Innate-keyword cards; card pool contains 2 Innate cards eligible for offer
- When: `Generate()` is called
- Then: Neither Innate card appears in the returned `CardDraft[]`; non-Innate cards appear normally
- Edge cases: 2 Innate in deck → Innate cards appear normally in offers

**AC-4: Free purge valve determinism**
- Given: `runSeed=1, nodeIndex=3`
- When: Valve is computed twice with same inputs
- Then: Both calls return the same `IsFreeValveApplied` value
- Edge cases: `runSeed=Int32.MaxValue, nodeIndex=1` — no OverflowException (XOR cannot overflow by definition)

**AC-5: Free purge valve in-session re-entry**
- Given: Chopshop node visited, valve computed, node exited, node re-entered without save/reload
- When: Valve value is read on second entry
- Then: Same value as first entry (read from cache, not re-rolled)
- Edge cases: Cache must survive a brief scene transition without a full reload

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/card-system/pity-innate-cap_test.cs` — must exist and pass

**Status**: [x] Created — `tests/unit/card-system/pity-innate-cap_test.cs` (20 runnable tests + 1 `[Ignore]` deferred for AC-13)

---

## Dependencies

- Depends on: Story 005 must be DONE (base draw algorithm must exist; pity and Innate cap extend it)
- Unlocks: Story 008 (the IsFreeValveApplied persistence round-trip depends on this story's computation being complete)

---

## Completion Notes

**Completed**: 2026-05-22
**Criteria**: 12/12 runnable passing (1 DEFERRED — AC-13 persistence round-trip, blocked on Story 008)
**Deviations**:
- ADVISORY: `CountDeckInnateCards` issues a second `GetByChassis` call separate from `BuildEligiblePool` (double catalog lookup per `Generate()`). Negligible at runtime (~14 draws/run). Future refactor candidate.
- ADVISORY: `FreeValveComputer._cache` lifetime must be run-scoped at composition root. Story 008 wiring concern.
- ADVISORY (resolved at close): Story implementation notes contained stale `runSeed + nodeIndex` / `unchecked` text. Corrected to `runSeed ^ nodeIndex` per ADR-0003 Rule 3 — XOR matches the implementation and tests.
- SCOPE NOTE: `FreeValveComputer` placed in new `WastelandRun.ScrapEconomy` assembly (not `WastelandRun.Cards`) per ADR-0006 TR-card-019 ownership boundary. Placeholder assembly until Scrap Economy ADR is formally authored.
**Test Evidence**: Logic — `tests/unit/card-system/pity-innate-cap_test.cs` (20 runnable + 1 `[Ignore]` deferred)
**Code Review**: CHANGES REQUIRED → resolved at close (story doc text correction + verified doc comment already present)
**Gate QL-TEST-COVERAGE**: ADEQUATE
**Gate LP-CODE-REVIEW**: CHANGES REQUIRED (both findings resolved — see deviations above)

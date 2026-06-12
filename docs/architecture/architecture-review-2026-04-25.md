# Architecture Review — 2026-04-25

> **Mode**: `/architecture-review full`
> **Engine**: Unity 6.3 LTS (pinned 2026-04-18)
> **GDDs Reviewed**: 10 MVP-tier
> **ADRs Reviewed**: 4 Accepted (ADR-0001, ADR-0002, ADR-0003, ADR-0004)
> **TRs Extracted**: 257 across 10 systems
> **Verdict**: **CONCERNS (PASSABLE)** for Technical Setup → Pre-Production gate

---

## Phase 1 — Inputs

### GDDs loaded (10 MVP-tier, all Approved)
- `design/gdd/card-system.md`
- `design/gdd/vehicle-and-part-system.md`
- `design/gdd/save-persistence.md`
- `design/gdd/status-effects.md`
- `design/gdd/card-combat-system.md`
- `design/gdd/scrap-economy.md`
- `design/gdd/node-map.md`
- `design/gdd/enemy-system.md`
- `design/gdd/loot-reward.md`
- `design/gdd/node-encounter.md`

Plus `design/gdd/game-concept.md` and `design/gdd/systems-index.md`.

### ADRs loaded (4 Accepted)
- ADR-0001: Visual Vehicle Part System (Accepted 2026-04-21)
- ADR-0002: Card Combat State & Event Architecture (Accepted 2026-04-24)
- ADR-0003: Loot RNG Determinism (Accepted 2026-04-24)
- ADR-0004: Save & Persistence Architecture (Accepted 2026-04-24)

### Engine reference
- `docs/engine-reference/unity/VERSION.md` — Unity 6.3 LTS, knowledge gap HIGH post-2022 LTS
- `docs/engine-reference/unity/breaking-changes.md` — SerializeField fields-only, URP Render Graph required, Input System default, Box2D v3
- `docs/engine-reference/unity/deprecated-apis.md` — legacy `Input`, UGUI deprecated (UI Toolkit preferred), `Resources.Load` → Addressables

### TR Registry baseline
- `docs/architecture/tr-registry.yaml` — empty on entry; populated this run (257 entries)

### Consistency-failures log
- `docs/consistency-failures.md` — does not exist; no recurring domain warnings to carry forward

---

## Phase 2 — Technical Requirements

257 TRs extracted across 10 systems (25 / 25 / 25 / 19 / 30 / 21 / 26 / 30 / 28 / 28 = 257). Full detail is persisted in `docs/architecture/tr-registry.yaml`. Summary per system:

| System | TRs | Dominant domains |
|--------|-----|------------------|
| Card System | 25 | Data structures (SO authoring), Persistence (deck state) |
| Vehicle & Part | 25 | Data structures (Vehicle POCO, slots), Communication (IVehicleView/Mutator) |
| Save & Persistence | 25 | Threading/timing (background writes), Platform (Steam Cloud, paths), Persistence (schema) |
| Status Effects | 19 | Data (StatusInstance), Timing (fire-before-tick), Communication (IStatusQuery) |
| Card Combat | 30 | Timing (pipeline order), Data (enums, state), Communication (events) |
| Scrap Economy | 21 | Data (formulas), Communication (IScrapEconomy facade), Persistence (log) |
| Node Map | 26 | Data (state machine), Timing (commit atomicity), Communication (route constraints) |
| Enemy | 30 | Data (EnemyDefinitionSO), Communication (CombatEndedPayload), Engine (silhouette classes) |
| Loot & Reward | 28 | Engine (weight modifier overlay), Threading (seeded determinism), Communication (delegation) |
| Node Encounter | 28 | Communication (handler orchestration), State persistence (frame state sampling) |

---

## Phase 3 — Traceability Matrix (system-level)

| System | Covered | Partial | Gap | Status |
|--------|---------|---------|-----|--------|
| Card System | 3 | 4 | 18 | ❌ GAP-dominated |
| Vehicle & Part | 4 | 5 | 16 | ⚠️ Partial (visual only) |
| **Save & Persistence** | **24** | 1 | 0 | ✅ **COVERED** |
| Status Effects | 0 | 5 | 14 | ❌ GAP |
| **Card Combat** | **11** | 6 | 13 | ⚠️ Partial (core covered) |
| Scrap Economy | 2 | 1 | 18 | ❌ GAP |
| Node Map | 4 | 3 | 19 | ⚠️ Partial (determinism + DTO only) |
| Enemy | 5 | 3 | 22 | ⚠️ Partial (Vehicle identity + RNG) |
| **Loot & Reward** | **12** | 2 | 14 | ⚠️ Partial (determinism covered) |
| Node Encounter | 1 | 2 | 25 | ❌ GAP |
| **Total** | **66 (26%)** | **32 (12%)** | **159 (62%)** | |

**Foundation-layer summary**: Save + Vehicle (visual) + Combat POCO + RNG are covered. Vehicle POCO sub-model + Card data authoring are Foundation gaps.

---

## Phase 3 — Coverage Gaps (no ADR exists)

### Foundation-layer gaps (must resolve before Pre-Production → Production)

- **Vehicle POCO Sub-Model**: TR-vehicle-001 (Vehicle POCO shape), TR-vehicle-006 (PartDefinitionSO contract), TR-vehicle-016 (IVehicleView formal contract), TR-vehicle-017 (IVehicleMutator allowed-callers list), TR-vehicle-019 (IPartCatalog), TR-vehicle-021 (stat composition Add+Multiply+Override), TR-vehicle-022 (ChassisDefinitionSO contract)
  - Suggested ADR: `/architecture-decision Vehicle POCO Sub-Model + Part Catalog`
  - Engine Risk: LOW (pure C#)

- **Card System Data Authoring**: TR-card-001 (CardDefinitionSO authority), TR-card-004 (CardEffectSO hierarchy), TR-card-009 (mastery-gated rarity weights), TR-card-012 (deck composition invariants), TR-card-016 (four card-location states persisted), TR-card-022 (deck state DTO shape), TR-card-024 (ICardRewardGenerator signature)
  - Suggested ADR: `/architecture-decision Card System Data Authoring (SO hierarchy + deck state)`
  - Engine Risk: LOW (ScriptableObjects + POCO)

### Core-layer gaps

- **Status Effects Subsystem**: 14 gap TRs — StatusInstance data shape, fire-before-tick resolution ordering, binary vs graduated merge rules, DOT bypass discriminator (DamageSource.Status), IStatusQuery interface, stack/duration caps
  - Suggested ADR: `/architecture-decision Status Effects Subsystem`
  - Engine Risk: LOW

- **Scrap Economy Transaction Facade**: 18 gap TRs — IScrapEconomy facade, Proposed→Validated→Committed→Logged state machine, TransactionInFlight reentrancy guard, rounding rules (Ceiling vs Floor), transaction log persistence
  - Suggested ADR: `/architecture-decision Scrap Economy Transaction Facade`
  - Engine Risk: LOW

- **Node Map Commit Pipeline**: 19 gap TRs — 10-step atomic commit pipeline, beacon state machine, route constraint gating (RC-M1/E1/W1/F1), storm advance coupling to combat reward close, commit-time sampling invariant, Haven terminal behaviour
  - Suggested ADR: `/architecture-decision Node Map Commit Pipeline`
  - Engine Risk: LOW

### Feature-layer gaps

- **Node Encounter Handler Orchestration**: 25 gap TRs — INodeEncounterHandler interface, seven handler types (Combat/EliteCombat/Merchant/Chopshop/Event/Rest/Haven), frame state sampling at commit, BeaconOutcome schema, event weight tilt on Frame state, EncounterType pass-through
  - Suggested ADR: `/architecture-decision Node Encounter Handler Orchestration`
  - Engine Risk: LOW

- **Enemy Data & Brain Contracts**: 22 gap TRs — EnemyDefinitionSO authoring, IEnemyPool.GetEnemyFor signature with biome-exclusive MVP rule, IEnemyBrain pure-C# contract, DifficultyScore formula ownership, CombatEndedPayload schema, RetargetPolicy enum, SilhouetteClass art contract
  - Suggested ADR: `/architecture-decision Enemy Data & Brain Contracts`
  - Engine Risk: LOW (no engine API usage in brains)

- **Loot & Reward Generation Pipeline**: 14 gap TRs — RewardTableSO three-axis keying, 10-step generation pipeline, weight modifier overlay (EffectiveWeight formula), fallback hierarchy (intended → half-floor → floor), BossSkip rule, PartDropCooldown management, SO import validators (AR-LR1/2/3/4/5)
  - Suggested ADR: `/architecture-decision Loot & Reward Generation Pipeline`
  - Engine Risk: LOW

---

## Phase 4 — Cross-ADR Conflict Detection

**No conflicts detected.** Verified pairwise:

| Pair | Conflict check | Verdict |
|------|----------------|---------|
| ADR-0001 ↔ ADR-0002 | Engine refs (ADR-0001 uses MonoBehaviour/SpriteRenderer; ADR-0002 is `noEngineReferences`) | ✅ Clean boundary — separate assemblies (`WastelandRun.Combat` POCO vs `WastelandRun.CombatView`) |
| ADR-0002 ↔ ADR-0003 | RNG ownership (ADR-0002 Deck.cs, ADR-0003 generalizes to all seeded systems) | ✅ Reinforcing, not contradictory |
| ADR-0002 ↔ ADR-0004 | DTO placement (ADR-0004 const fields on DTOs; ADR-0002 forbids engine refs in Combat) | ✅ DTOs host `const string`/`const int` which are engine-free |
| ADR-0003 ↔ ADR-0004 | RunSeed authority (ADR-0003 derives scoped seeds; ADR-0004 persists RunSeed in envelope) | ✅ Ownership explicit — RunSeed stored in RunState DTO |
| ADR-0001 ↔ ADR-0004 | Asset loading (ADR-0001 Addressables chassis-[name]; ADR-0004 Steam Cloud include `saves/`) | ✅ Disjoint — Addressables content not in save path |

### ADR Dependency Order (topological)

```
Foundation (parallel OK — no upstream deps):
  1. ADR-0001 — Visual Vehicle Part System
  2. ADR-0002 — Card Combat POCO + Events

Depends on Foundation:
  3. ADR-0003 — RNG Determinism   (requires ADR-0002)
  4. ADR-0004 — Save & Persistence (requires ADR-0002 + ADR-0003)
```

**No cycles. No unresolved-dependency blocks.** All 4 ADRs are Accepted.

---

## Phase 5 — Engine Compatibility Audit

**Engine**: Unity 6.3 LTS (pinned 2026-04-18)

| Item | Status |
|------|--------|
| ADRs with Engine Compatibility section | 4 / 4 ✅ |
| Deprecated API references | 0 ✅ |
| Stale engine version references | 0 ✅ |
| Post-cutoff API conflicts | 0 ✅ |
| Open Questions explicitly flagged | OQ4 (`FlushFileBuffers` on Mono), OQ5 (`File.Move(src, dst, overwrite:true)` on Unity 6.3 Mono) — both ADR-0004, both with P/Invoke fallbacks identified |

**Details:**
- ADR-0001 — URP Sprite Lit Shader Graph + MaterialPropertyBlock + `[PerRendererData]` + Addressables — all Unity 6.3 LTS-supported patterns
- ADR-0002 — Assembly-split with `noEngineReferences: true` — no Unity API surface to audit
- ADR-0003 — Forbids `UnityEngine.Random`, `Time.*`, `DateTime.Now`, `Guid.NewGuid`, `Random.Shared` — aligned with Unity 6.3 + deterministic-replay goals
- ADR-0004 — `File.Move(overwrite:true)`, `temporaryCachePath`, `persistentDataPath`, `Application.quitting`, Newtonsoft.Json + `link.xml` for IL2CPP — due diligence correctly reflected in OQ4/OQ5

**Engine specialist consultation**: deferred — no HIGH RISK audit findings warrant a specialist spike. Can be invoked ad-hoc if implementation surfaces Mono-specific behavior in OQ4/OQ5.

---

## Phase 5b — GDD Revision Flags

**None — all GDD assumptions are consistent with verified engine behaviour.**

No HIGH RISK engine findings that contradict GDD rules. ADR-0004 OQ4/OQ5 are implementation-time verifications, not design-level contradictions.

---

## Phase 6 — Architecture Document Coverage

Master architecture document (`docs/architecture/architecture.md`) does not yet exist. Phase 6 deferred until master doc is authored (next task in the Technical Setup queue).

---

## Phase 7 — Verdict

### **CONCERNS (PASSABLE)** for Technical Setup → Pre-Production

**PASS factors:**
- Foundation-layer coverage is strong: Save (ADR-0004 24/25 TRs), Vehicle visual rendering (ADR-0001), Combat POCO (ADR-0002 11/30 TRs = core), RNG discipline (ADR-0003).
- No cross-ADR conflicts.
- No engine compatibility issues.
- ADR dependency graph is acyclic; all 4 Accepted.
- TD-C2 director condition (schema-registry location + partial-load policy) addressed in ADR-0004.

**CONCERNS factors:**
- ~62% of TRs (159 / 257) have no ADR coverage.
- Gaps concentrated in Core layer (Status Effects, Scrap Economy, Node Map) and Feature layer (Enemy, Loot & Reward, Node Encounter).
- Two Foundation-layer gaps: Vehicle POCO sub-model (distinct from ADR-0001 visual layer) and Card Data Authoring.

**Not FAIL** because the Technical Setup → Pre-Production gate requires Foundation-layer ADRs only (ADR-0001/0002/0003/0004 satisfy this). Core/Feature ADRs are expected to be authored during Pre-Production.

### Required ADRs (priority order)

| # | Proposed ADR | Layer | TRs Covered |
|---|-------------|-------|-------------|
| 1 | Vehicle POCO Sub-Model + Part Catalog | Foundation | ~8 V&P TRs |
| 2 | Card System Data Authoring | Foundation | ~7 Card TRs |
| 3 | Status Effects Subsystem | Core | ~18 Status TRs |
| 4 | Scrap Economy Transaction Facade | Core | ~18 Scrap TRs |
| 5 | Node Map Commit Pipeline | Core | ~19 Map TRs |
| 6 | Node Encounter Handler Orchestration | Feature | ~25 Encounter TRs |
| 7 | Enemy Data & Brain Contracts | Feature | ~22 Enemy TRs |
| 8 | Loot & Reward Generation Pipeline | Feature | ~14 Loot TRs |

---

## Phase 8 — Outputs

Written by this review:
- `docs/architecture/architecture-review-2026-04-25.md` (this file)
- `docs/architecture/tr-registry.yaml` (populated — 257 TRs indexed)
- `production/session-state/active.md` (Next section updated)

### Recommended follow-ups

1. Run `/architecture-decision Vehicle POCO Sub-Model + Part Catalog` (Foundation priority 1).
2. Run `/architecture-decision Card System Data Authoring` (Foundation priority 2).
3. Then begin master architecture document (`docs/architecture/architecture.md`) — synthesis is possible now that the ADR skeleton is clear.
4. After priority-1/2 ADRs Accepted: re-run `/architecture-review` to confirm Foundation gaps close.
5. Core and Feature ADRs (priorities 3–8) scheduled during Pre-Production, before first story creation for those systems.

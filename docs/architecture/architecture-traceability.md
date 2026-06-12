# Architecture Traceability Index

<!-- Living document — updated by /architecture-review after each review run.
     Do not edit manually unless correcting an error. -->

## Document Status

- **Last Updated**: 2026-04-27
- **Engine**: Unity 6.3 LTS (pinned 2026-04-18)
- **GDDs Indexed**: 10 MVP-tier
- **ADRs Indexed**: 6 (6 Accepted)
- **Last Review**: [architecture-review-2026-04-25.md](architecture-review-2026-04-25.md)
- **TR Registry**: [tr-registry.yaml](tr-registry.yaml) — 257 stable IDs (drill-down source)

## Coverage Summary

| Status | Count | Percentage |
|--------|-------|-----------|
| ✅ Covered | 66 | 26% |
| ⚠️ Partial | 32 | 12% |
| ❌ Gap | 159 | 62% |
| **Total** | **257** | 100% |

Foundation-layer coverage is strong (Save 24/25, Vehicle visual layer, Combat POCO core, RNG discipline). Core and Feature layers have expected gaps — scheduled for Pre-Production ADR slate.

---

## Traceability Matrix (system-level)

Per-system roll-up. Full per-TR detail is in [tr-registry.yaml](tr-registry.yaml) (257 entries, one per TR, with requirement text + domain + status). This document summarizes the matrix; the registry is the drill-down authority.

| System | GDD | TRs | ✅ Covered | ⚠️ Partial | ❌ Gap | Covering ADRs | Status |
|--------|-----|-----|----|----|----|---------------|--------|
| Card System | `card-system.md` | 25 | 10 | 4 | 11 | ADR-0004 (deck DTO), ADR-0006 (SO authoring + deck state) | ✅ Foundation covered |
| Vehicle & Part | `vehicle-and-part-system.md` | 25 | 12 | 5 | 8 | ADR-0001 (visual), ADR-0005 (POCO + part catalog) | ✅ Foundation covered |
| Save & Persistence | `save-persistence.md` | 25 | 24 | 1 | 0 | ADR-0004 | ✅ Covered |
| Status Effects | `status-effects.md` | 19 | 0 | 5 | 14 | (partial via ADR-0002) | ❌ Core gap |
| Card Combat | `card-combat-system.md` | 30 | 11 | 6 | 13 | ADR-0002, ADR-0003 | ⚠️ Core covered |
| Scrap Economy | `scrap-economy.md` | 21 | 2 | 1 | 18 | ADR-0004 (DTO only) | ❌ Core gap |
| Node Map | `node-map.md` | 26 | 4 | 3 | 19 | ADR-0003, ADR-0004 (DTO + RNG) | ⚠️ Pipeline gap |
| Enemy | `enemy-system.md` | 30 | 5 | 3 | 22 | ADR-0001, ADR-0003 (visual + RNG) | ⚠️ Brain gap |
| Loot & Reward | `loot-reward.md` | 28 | 12 | 2 | 14 | ADR-0003, ADR-0004 | ⚠️ Pipeline gap |
| Node Encounter | `node-encounter.md` | 28 | 1 | 2 | 25 | ADR-0004 (DTO only) | ❌ Feature gap |
| **Total** | — | **257** | **66** | **32** | **159** | — | — |

---

## Known Gaps

Requirements with no ADR coverage, prioritised by layer (Foundation first). Each gap block lists the ADR that will close it.

### Foundation Layer Gaps (BLOCKING — must resolve before Pre-Production story authoring)

- [x] **Vehicle POCO Sub-Model + Part Catalog** — CLOSED 2026-04-25 by [ADR-0005](adr-0005-vehicle-poco-part-catalog.md)
  - Scope delivered: Vehicle POCO shape, PartDefinitionSO contract, IVehicleView/Mutator formal contracts, IPartCatalog, stat composition (Add+Multiply+Override single-winner), ChassisDefinitionSO, ADR-0001 amendments (4 data slots + 4-value DamageState + 3-arg event + DamageSource on ApplyDamage), VehicleStateDTO with distributed `SystemId`/`SchemaVersion` per ADR-0004
  - Status: Accepted 2026-04-27

- [x] **Card System Data Authoring** — CLOSED 2026-04-25 by [ADR-0006](adr-0006-card-system-data-authoring.md)
  - Scope delivered: CardDefinitionSO authority, sealed-subclass-per-asset CardEffectSO/EffectConditionSO hierarchy (no `[SerializeReference]`), mastery-gated rarity weights, deck composition invariants, four card-location states (Library/Deck/Hand/Discard), CardSystemDTO, ICardRewardGenerator signature with token resolution at draft-generation time, IL2CPP `link.xml` preservation, Addressables `card-effects` label discipline. RarePityCounter ownership delegated to LootStateDTO (Loot & Reward owns persistence).
  - Status: Accepted 2026-04-27

### Core Layer Gaps (must resolve before relevant system's stories are written — Pre-Production)

- [ ] **Status Effects Subsystem** — closes 14 gap TRs
  - Scope: StatusInstance data shape, fire-before-tick resolution ordering, binary vs graduated merge rules, DOT bypass discriminator (DamageSource.Status), IStatusQuery interface, stack/duration caps
  - Engine Risk: LOW
  - Action: `/architecture-decision Status Effects Subsystem`

- [ ] **Scrap Economy Transaction Facade** — closes 18 gap TRs
  - Scope: IScrapEconomy facade, Proposed→Validated→Committed→Logged state machine, TransactionInFlight reentrancy guard, rounding rules (Ceiling vs Floor), transaction log persistence
  - Engine Risk: LOW
  - Action: `/architecture-decision Scrap Economy Transaction Facade`

- [ ] **Node Map Commit Pipeline** — closes 19 gap TRs
  - Scope: 10-step atomic commit pipeline, beacon state machine, route constraint gating (RC-M1/E1/W1/F1), storm advance coupling to combat reward close, commit-time sampling invariant, Haven terminal behaviour
  - Engine Risk: LOW
  - Action: `/architecture-decision Node Map Commit Pipeline`

### Feature Layer Gaps (should resolve before feature sprint begins)

- [ ] **Node Encounter Handler Orchestration** — closes 25 gap TRs
  - Scope: INodeEncounterHandler interface, seven handler types (Combat/EliteCombat/Merchant/Chopshop/Event/Rest/Haven), frame state sampling at commit, BeaconOutcome schema, event weight tilt on Frame state, EncounterType pass-through
  - Engine Risk: LOW
  - Action: `/architecture-decision Node Encounter Handler Orchestration`

- [ ] **Enemy Data & Brain Contracts** — closes 22 gap TRs
  - Scope: EnemyDefinitionSO authoring, IEnemyPool.GetEnemyFor signature with biome-exclusive MVP rule, IEnemyBrain pure-C# contract, DifficultyScore formula ownership, CombatEndedPayload schema, RetargetPolicy enum, SilhouetteClass art contract
  - Engine Risk: LOW (no engine API usage in brains)
  - Action: `/architecture-decision Enemy Data & Brain Contracts`

- [ ] **Loot & Reward Generation Pipeline** — closes 14 gap TRs
  - Scope: RewardTableSO three-axis keying, 10-step generation pipeline, weight modifier overlay (EffectiveWeight formula), fallback hierarchy (intended → half-floor → floor), BossSkip rule, PartDropCooldown management, SO import validators (AR-LR1/2/3/4/5)
  - Engine Risk: LOW
  - Action: `/architecture-decision Loot & Reward Generation Pipeline`

### Presentation Layer Gaps (defer to implementation)

- [ ] **Screen Reader Integration Path** — pre-1.0 accessibility ADR (UAP vs custom vs post-1.0 defer)
  - Trigger: when accessibility screen-reader work is scheduled
  - Action: `/architecture-decision Screen Reader Integration Path`

---

## Cross-ADR Conflicts

**None detected as of 2026-04-25.**

| Conflict ID | ADR A | ADR B | Type | Status |
|-------------|-------|-------|------|--------|
| — | — | — | — | ✅ No conflicts |

Pairwise verification matrix is in `architecture-review-2026-04-25.md` Phase 4.

---

## ADR → GDD Coverage (Reverse Index)

| ADR | Title | GDD Requirements Addressed | Engine Risk |
|-----|-------|---------------------------|-------------|
| ADR-0001 | Visual Vehicle Part System | TR-vehicle-003, -004 (damage states, visual layer), TR-vehicle-013 (chassis emotional attachment), TR-vehicle-023 (3-chassis scaling), TR-enemy-015 (SilhouetteClass visual contract) | LOW (URP + Addressables, Unity 6.3 LTS verified) |
| ADR-0002 | Card Combat State & Event Architecture | 11 combat TRs: R1 (engine-free POCO), R11 (event bus), R15 (EncounterType), R16 (Position axis), R17 (pool-filter), R18 (synthetic RepositionIntent), F-CC1 (pipeline order), F-CC5 (PositionBonus preview), OQ-CC-NEW-2/3/4 (Fizzle + forbidden patterns) | LOW (assembly-split `noEngineReferences: true`) |
| ADR-0003 | Loot RNG Determinism | 12 loot + map TRs: C.1/C.3/C.5 (RNG ownership), AC-LR1–5 (reproducibility), AC-LR25 (scoped seed), AC-LR37–39 (seeded infra), F.1 (weight formula); generalizes to all seeded systems (Card Combat, Node Map) | LOW (forbidden-tokens grep CI-enforced) |
| ADR-0004 | Save & Persistence Architecture | 14 save TRs + retrofits: R1–R8 (envelope, schema, triggers, atomicity, recovery, DTOs, migration), F1 (passive orchestrator), F3 (write policy), OQ4 (FlushFileBuffers P/Invoke on Mono), OQ5 (File.Move overwrite on Unity 6.3 Mono), TD-C2 (schema-registry location + partial-load policy) | LOW, two OQs flagged with fallbacks |
| ADR-0005 | Vehicle POCO Sub-Model + Part Catalog | 8 V&P TRs (POCO shape, IVehicleView/Mutator contracts, IPartCatalog, stat composition, ChassisDefinitionSO, VehicleStateDTO); amends ADR-0001 (4 data slots, 4-value DamageState, 3-arg event, DamageSource arg) | LOW (POCO + standard SO + Addressables) |
| ADR-0006 | Card System Data Authoring | 7 Card TRs (CardDefinitionSO authority, sealed CardEffectSO hierarchy, rarity weights, deck composition invariants, four card-location states, CardSystemDTO, ICardRewardGenerator signature) | LOW (SO + IL2CPP `link.xml`) |

See each ADR file for the full per-TR mapping in its "GDD Requirements Addressed" section.

---

## Superseded Requirements

<!-- Requirements that existed in a GDD when an ADR was written, but the GDD
     has since changed. The ADR may need updating. -->

**None as of 2026-04-25.** The retrofit propagation pass (2026-04-23) preceded all four ADRs, so all ADRs were authored against post-retrofit GDDs.

| Req ID | GDD | Change | Affected ADR | Status |
|--------|-----|--------|-------------|--------|
| — | — | — | — | ✅ Clean |

---

## How to Use This Document

**When writing a new ADR**: Add it to the "ADR → GDD Coverage" table and update the per-system covered/partial/gap counts in the traceability matrix. The TR registry tracks the authoritative per-TR mapping — amend `tr-registry.yaml` only when a TR's requirement text changes (add `revised` date), status changes (active → deprecated → superseded-by), or a new TR is needed (append at end of that system's list with the next sequential ID).

**When approving a GDD change**: Run `/propagate-design-change [gdd-path]`. The skill scans the matrix for requirements from that GDD and flags any ADR assumptions that the change invalidates. Affected TRs get `revised` dates or `status: superseded-by` entries in the registry; affected ADRs get added to the "Superseded Requirements" table above.

**When running `/architecture-review`**: The skill recomputes the coverage table, updates the gap list, and re-validates cross-ADR consistency. This document is the output target for Phase 8.

**Gate check**: The Technical Setup → Pre-Production gate requires this document to exist and to have zero Foundation-layer gaps marked BLOCKING. Current state: **0 Foundation gaps remaining** — both Vehicle POCO sub-model (ADR-0005) and Card Data Authoring (ADR-0006) closed 2026-04-25; both flipped to Accepted 2026-04-27.

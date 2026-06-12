# Systems Index: Wasteland Run

> **Status**: Approved — all MVP GDD systems approved. Vehicle & Part System architecture-doc APPROVED 2026-05-19 (R4 revision pass). ADR-0007 → Accepted (architecture surface only). Mechanics-doc not yet authored (downstream; W2 implementation unblocked).
> **Created**: 2026-04-19
> **Last Updated**: 2026-05-19 (V&P architecture-doc **APPROVED** after R4 revision pass. R4 full re-review (unity-specialist, qa-lead, game-designer, systems-designer, creative-director) surfaced 4 blockers on 6 R3-touched sections; all 4 closed in-session. Key findings: B1 IsValidated session lifecycle gap → [Editor only] guard + LayoutNotValidatedThisSessionException + shipped-build skip; B2 [SerializeField] public on SOs → [field: SerializeField] properties; B3 AC-VPA31a telemetry field names → OffendingSubscriberType + OriginatingEventName named; B4 AC-VPA31b not falsifiable → full rewrite with independent setup + ghost-kill prevention. Doc grew ~2,460 → ~2,495 lines. ADR-0007 transitions Proposed → Accepted (architecture surface only). W2 (EnemyDefinitionSO + BrainRulesetSO) unblocked. See `design/gdd/reviews/vehicle-and-part-architecture-review-log.md` R4.)
> **Source Concept**: design/gdd/game-concept.md
> **TD-SYSTEM-BOUNDARY Review**: CONCERNS (accepted) 2026-04-19
> **PR-SCOPE Review**: OPTIMISTIC → adjustments accepted 2026-04-19
> **CD-SYSTEMS Review**: CONCERNS (accepted) 2026-04-19

---

## Overview

Wasteland Run is a 2D turn-based vehicular card roguelike. Its mechanical scope spans three interlocking domains: **combat** (card play against enemy vehicles with per-subsystem targeting), **vehicle management** (modular parts that shape both stats and deck identity), and **run structure** (seeded node map navigation across biomes with a persistent meta layer). All three domains must be designed before any can be implemented — they share state (the vehicle POCO), share vocabulary (card family taxonomy), and share the player's time within a single 30-60 minute session.

The game's five pillars drive system priority: Vehicle as Character and Chassis Identity demand a rich Vehicle & Part System before combat can carry emotional weight. Read to Win demands a deep Card Combat System with readable subsystem states. Scarcity with Agency demands a tight Scrap Economy. Route Reflects Vehicle State demands a Node Map whose content (biomes, encounter types) is shaped by what the vehicle can do.

Systems are designed Foundation → Core → Feature → Presentation. MVP systems are designed first; Vertical Slice systems follow once the Prototype hypothesis ("does card+subsystem combat feel good?") is validated.

---

## Systems Enumeration

| # | System Name | Category | Priority | Status | Design Doc | Depends On |
|---|-------------|----------|----------|--------|------------|------------|
| 1 | Card System | Cards | MVP | Approved | design/gdd/card-system.md | — |
| 2 | Vehicle & Part System | Vehicle | MVP | **BOTH DOCS APPROVED** — arch-doc APPROVED (2026-05-19, R4); mechanics-doc **APPROVED (2026-05-21, R8)**. ADR-0007 Accepted. W9.1 (2026-05-21): 16 precision fixes — C.1 clamp direction corrected (upward to `Ceiling(100/MaxHp)`); MaxHp=1 OnValidate rejection added; AC-VPM-H pass condition corrected (N=50); ScrapRefundRate floor example 0.25→0.20 + Rare case added; entities.yaml `min: 0.20` added; audio active-Critical count owner specified in part-state model; Light-tier suppression clarified (audio channel only; visual always fires); `CriticalLoopExitCrossfadeMs` tuning knob added (configurable, TBD W3g); "focused" definition added to C.3 (traversal focus, not confirmed selection); multi-stat Offline preview specified (block-level "(Destroyed)" label, absent stats in "New from candidate:"); Pillar 5 cross-doc gate sentence added to Dependencies; AC-VPM-A event source named (`IVehicleEventBus`) + forced-pass input rejection assertions added; AC-VPM-G injectable `IFrameClock` contract specified; AC-VPM-B affirmative assertion added; `ForcedPassBannerDurationSecs` added to Tuning Knobs (1.5s, inputs discarded, Esc exempt). CD verdict: "Spec precision gaps, not experience-layer concerns. Revision iteration cost exceeds marginal value. APPROVED." | design/gdd/vehicle-and-part-architecture.md (Approved) + vehicle-and-part-mechanics.md (Approved — R8, W9.1 applied) | — |
| 3 | Save & Persistence System | Persistence | MVP | Approved (2026-04-21) | design/gdd/save-persistence.md | — |
| 4 | Status Effect System | Combat | MVP | Approved (2026-04-21) | design/gdd/status-effects.md | Card System |
| 5 | Card Combat System | Combat | MVP | Approved (2026-04-21) | design/gdd/card-combat-system.md | Card System, Vehicle & Part System, Status Effect System |
| 6 | Scrap Economy System | Economy | MVP | Approved (2026-04-22) | design/gdd/scrap-economy.md | Vehicle & Part System, Card System |
| 7 | Node Map System | Map | MVP | Approved (2026-04-21) | design/gdd/node-map.md | Save & Persistence System |
| 8 | Enemy System | Combat | MVP | Approved (2026-04-23) | design/gdd/enemy-system.md | Card Combat System, Status Effect System, Vehicle & Part System |
| 9 | Loot & Reward System | Economy | MVP | Approved (2026-04-23) | design/gdd/loot-reward.md | Card System, Vehicle & Part System, Scrap Economy System, Enemy System, Node Map System |
| 10 | Node Encounter System | Map | MVP | Approved (2026-04-23) | design/gdd/node-encounter.md | Node Map System, Loot & Reward System, Scrap Economy System |
| 11 | Chassis Identity System (inferred) | Vehicle | Vertical Slice | Not Started | — | Vehicle & Part System, Card System |
| 12 | Biome System (inferred) | Map | Vertical Slice | Not Started | — | Node Map System, Enemy System, Node Encounter System |
| 13 | Meta Progression System | Progression | Vertical Slice | Not Started | — | Chassis Identity System, Loot & Reward System, Save & Persistence System |
| 14 | Combat HUD (UX Spec) | UI | MVP | NEEDS REVISION — /ux-review 2026-05-25 (blocking: Visual Budget ✅ fixed; advisory: OQ-CH4 gamepad pause, escalate pre-alpha) | design/ux/combat-hud.md | Card Combat System, Status Effect System, Vehicle & Part System |
| 15 | Part Inspect UI (UX Spec) | UI | MVP | Not Started | — | Vehicle & Part System, Scrap Economy System |
| 16 | Post-Combat Flow UI (UX Spec) | UI | MVP | Not Started | — | Loot & Reward System, Card System, Vehicle & Part System, Scrap Economy System |
| 17 | Map UI (UX Spec) | UI | MVP | Not Started | — | Node Map System, Biome System |
| 18 | Meta UI (UX Spec) | UI | Vertical Slice | Not Started | — | Meta Progression System, Vehicle & Part System |

---

## Categories

| Category | Description |
|----------|-------------|
| **Cards** | Card definitions, families, and deck rules — the vocabulary everything else references |
| **Combat** | Turn-based card play, subsystem state machine, enemy behavior |
| **Vehicle** | Chassis types, part slots, damage states, chassis identity differentiation |
| **Economy** | Currency flow, loot generation, drop tables, repair and install decisions |
| **Map** | Node map generation, biome structure, per-node encounter rules |
| **Progression** | Mastery tracks, XP, between-run unlock economy |
| **Persistence** | Run state and mastery state serialization across sessions |
| **UI** | Player-facing displays — all authored via `/ux-design`, not `/design-system` |

---

## Priority Tiers

| Tier | Definition | Target | Design Urgency |
|------|------------|--------|----------------|
| **MVP** | Required for the Prototype hypothesis: "does card+subsystem combat feel good in a 30-min run?" | Month 1–5 | Design FIRST |
| **Vertical Slice** | Required for a complete two-chassis experience with meta layer. Adds differentiation, biome identity, mastery. | Month 3–7 | Design SECOND |
| **Full Vision** | Post-EA content: Heavy Truck chassis, Biome 4, legendary sets | Post-launch | Design as needed |

---

## Dependency Map

### Layer 1 — Foundation (no dependencies)

1. **Card System** — All card effects, families, costs, and rarity tiers are referenced by every other combat system. Nothing can be designed until card structure is defined.
2. **Vehicle & Part System** — Chassis types, slot taxonomy, part stats, and the Vehicle POCO definition underpin combat stats, deck composition, economy pricing, and 3 UI systems. ⚠️ *Hard-blocked on Visual Part ADR — see High-Risk Systems.*
3. **Save & Persistence System** — Defines the persistence contract (run state vs. mastery state, passive serializer pattern, per-system DTO ownership) that all other systems must implement against.

### Layer 2 — Core (depends on Foundation only)

4. **Status Effect System** — Depends on: Card System. All card-triggered debuffs/buffs (especially Control family), stacking rules, tick timing, resolution order. Card Combat depends on this — not the reverse. *(Dependency corrected from initial draft per TD-SYSTEM-BOUNDARY C2.)*
5. **Card Combat System** — Depends on: Card System, Vehicle & Part System, Status Effect System. Turn loop orchestrator: hand/energy/draw/discard, card play pipeline, subsystem targeting. *Must be designed as three sub-models: Combat Loop / Subsystem State / Position State. See GDD scoping notes.*
6. **Scrap Economy System** — Depends on: Vehicle & Part System, Card System. Primary currency flow, part install/scrap/repair cost rules, merchant pricing logic.
7. **Node Map System** — Depends on: Save & Persistence System. Seeded procedural map generation, biome layout, depth lanes, branching paths, Haven endpoint. **Also owns route constraint rules**: vehicle state gates route availability (e.g., offline Mobility subsystem locks elite encounter nodes). This is the mechanical delivery of Pillar 5 (Route Reflects Vehicle State) — the map is not a neutral backdrop.

### Layer 3 — Feature (depends on Core)

8. **Enemy System** — Depends on: Card Combat System, Status Effect System, Vehicle & Part System. Enemy archetypes per biome, subsystem loadouts, AI targeting priority behavior, boss encounter rules.
9. **Loot & Reward System** — Depends on: Card System, Vehicle & Part System, Scrap Economy System. Drop-table generation only (deterministic, seeded). Reward selection/presentation UI lives in Post-Combat Flow UI. *(Scope clarified per TD-SYSTEM-BOUNDARY C5.)*
10. **Node Encounter System** — Depends on: Node Map System, Loot & Reward System, Scrap Economy System. Per-node rules for all 6 node types: Unknown, Merchant, Treasure, Chopshop, Normal Combat, Elite Combat. Includes Haven node (triggers end-of-run screen — resolved as a node type, not a separate system).
11. **Chassis Identity System** *(VS)* — Depends on: Vehicle & Part System, Card System. Per-chassis card reward pool biases, part drop biases, and the data-driven modifier contract for chassis-specific passive mechanics.
12. **Biome System** *(VS)* — Depends on: Node Map System, Enemy System, Node Encounter System. 4 biomes: enemy family assignments, difficulty scaling curves, node distribution weights, accent palette rules.
13. **Meta Progression System** *(VS)* — Depends on: Chassis Identity System, Loot & Reward System, Save & Persistence System. Chassis mastery tracks (10 levels × 2 chassis for EA), XP per run, unlock economy, legendary set availability triggers.

### Layer 4 — Presentation (UI/UX specs — authored via `/ux-design`)

14. **Combat HUD** — Depends on: Card Combat System, Status Effect System, Vehicle & Part System. Subsystem state display, card hand, energy counter, position indicator, enemy info panel.
15. **Part Inspect UI** — Depends on: Vehicle & Part System, Scrap Economy System. Used post-combat (part found) and between nodes (current loadout review). Install/scrap decision surface.
16. **Post-Combat Flow UI** — Depends on: Loot & Reward System, Card System, Vehicle & Part System, Scrap Economy System. XP/Scrap gain display, 2-card reward offer (pick 1 or skip — see Card System GDD), part find → Part Inspect handoff, repair prompt.
17. **Map UI** — Depends on: Node Map System, Biome System. Node map display, route selection, depth progress, biome transition indicator.
18. **Meta UI** *(VS)* — Depends on: Meta Progression System, Vehicle & Part System. Mastery track screen, chassis select screen, part inspect (full loadout view), haven ending screen.

---

## Recommended Design Order

| Order | System | Priority | Layer | Format | Est. Effort |
|-------|--------|----------|-------|--------|-------------|
| 1 | Card System | MVP | Foundation | GDD | M |
| 2 | Save & Persistence System | MVP | Foundation | GDD | M |
| 3 | Status Effect System | MVP | Core | GDD | M |
| 4 | Vehicle & Part System | MVP | Foundation | GDD | L |
| 5 | Card Combat System | MVP | Core | GDD | L |
| 6 | Scrap Economy System | MVP | Core | GDD | M |
| 7 | Node Map System | MVP | Core | GDD | M |
| 8 | Enemy System | MVP | Feature | GDD | L |
| 9 | Loot & Reward System | MVP | Feature | GDD | M |
| 10 | Node Encounter System | MVP | Feature | GDD | M |
| 11 | Combat HUD | MVP | Presentation | UX Spec | M |
| 12 | Part Inspect UI | MVP | Presentation | UX Spec | S |
| 13 | Post-Combat Flow UI | MVP | Presentation | UX Spec | M |
| 14 | Map UI | MVP | Presentation | UX Spec | M |
| — | *Prototype milestone target: Month 1–2* | | | | |
| 15 | Chassis Identity System | VS | Core | GDD | M |
| 16 | Biome System | VS | Feature | GDD | M |
| 17 | Meta Progression System | VS | Feature | GDD | M |
| 18 | Meta UI | VS | Presentation | UX Spec | S |
| — | *Vertical Slice milestone target: Month 3–5* | | | | |

> **Effort key**: S = 1 session, M = 2–3 sessions, L = 4+ sessions.
> GDD systems use `/design-system`. UX Spec systems use `/ux-design`.
> Vehicle & Part System (#4) is listed at position 4 but **cannot be finalized until the Visual Part ADR lands** — begin the GDD skeleton and early sections, hold finalization.

---

## Circular Dependencies

None found. Dependency graph is a clean DAG.

> **Note**: The previous draft had Status Effect System listed as depending on Card Combat System, which would have created implicit circular coupling (Combat → Status Effects → Combat). Resolved: Status Effect System depends on Card System only. Card Combat System depends on Status Effect System.

---

## High-Risk Systems

| System | Risk Type | Risk Description | Mitigation |
|--------|-----------|-----------------|------------|
| **Vehicle & Part System** | Scope + Technical | GDD approved 2026-04-21. Architecture, interfaces, state machine, and emotional-attachment mechanic (P4) locked. Residual risk: shader `[PerRendererData]` behavior must be validated in Month 1 prototype per ADR-0001. | ADR-0001 Accepted; GDD Approved. Residual validation: Month 1 shader prototype per ADR Validation Criteria #2. |
| **Card Combat System** | Design + Technical | God Object risk if Combat Loop, Subsystem State, and Position State are not separated into distinct sub-models from the start. Every other system will reach into Combat internals. | GDD must define three named sub-models with explicit interfaces. Subsystem State belongs on the Vehicle POCO (not inside Combat). See GDD scoping notes C1 and C3. |
| **Enemy System** | Scope | 3 biomes × 5–8 enemies = 15–24 enemy designs, each needing art, AI behavior, subsystem loadouts, and balance. Estimated 6–10 weeks of content work at full quality. | Begin enemy framework GDD early (Month 2). Prototype with 3–5 enemy types in 1 biome. Full enemy rosters are a content bottleneck — prioritize framework over content volume. |
| **Card System** | Scope | ~150–200 cards × (design + balance + art + VFX + SFX + test). At 2 hours per card fully-finished, that is 300–400 hours of content work. | Balance Sim Runner must exist by Month 5 (see Tooling Deliverables). Build the card effect framework and test with 20 cards in Prototype before committing to full card count. |
| **Save & Persistence System** | Technical | Save versioning: a single schema change without a version bump can corrupt save data at launch. Mastery data must survive run failures and app crashes. | Save GDD must specify per-system schema versioning (not monolithic), atomic writes, and N=3 auto-backup from day one. This is non-negotiable infrastructure — do not shortcut. |

---

## GDD Scoping Notes (TD-SYSTEM-BOUNDARY)

These constraints must be reflected in each GDD's Overview and Detailed Rules sections. They are not optional — they prevent architectural debt from forming during GDD authoring.

**C1 — Card Combat System: three sub-models required**
Card Combat GDD must define and name three distinct sub-models:
- `CombatLoop` — turn sequencing, hand/energy/draw/discard, card play pipeline. Orchestrator only; owns no state directly.
- `SubsystemState` — per-vehicle POCO: part → Functional/Degraded/Offline transitions, plating stacks, damage resolution. Belongs on the Vehicle, not inside Combat.
- `PositionState` — Behind/Ahead state, swap rules and triggers. Small enough to stay in Combat but must be a named component, not inline logic.

**C2 — Status Effect System dependency corrected**
Status Effects are a mechanism that Combat applies and queries. Status Effect GDD dependencies: Card System only. Card Combat GDD dependencies: Card System + Vehicle & Part System + Status Effect System. This ordering is reflected in the design order above.

**C3 — Vehicle POCO ownership: IVehicleView / IVehicleMutator**
Vehicle & Part System GDD must define the Vehicle runtime model access pattern explicitly:
- `IVehicleView` — read-only interface. All systems except mutators use this.
- `IVehicleMutator` — write interface. Exclusive list: Combat (damage application), Economy (install/scrap/repair), Loot (add found part). No other system may mutate vehicle state.
This is the single most load-bearing interface in the game. Name it in the GDD before any implementation begins.

**C4 — Save & Persistence: passive serializer pattern**
Save & Persistence GDD must define Save as a passive serializer:
- Save owns no schema knowledge of individual systems.
- Each system defines its own serialization contract (DTOs owned by that system, not by Save).
- Save orchestrates when to write and where; each system's `Serialize()/Deserialize()` pair handles what.
- Per-system schema versioning (not a monolithic save version number).

**C5 — Loot & Reward System: drop-table generation only**
Loot & Reward GDD scope: deterministic drop-table generation only (`GenerateRewards(context, seed)` → returns a list). Reward presentation and player selection live in Post-Combat Flow UI. Do not let reward logic reach into UI state (player-has-seen-rewards, animation-playing, etc.).

**C6 — Chassis Identity System: data-driven for EA**
Chassis Identity GDD must explicitly commit to: chassis mechanics expressed as card pool biases + passive stat modifiers via a declarative data contract. Bespoke per-chassis turn-phase hooks (`IChassisAbility`) are out of scope for EA. If this constraint changes post-EA, it requires an ADR.

**B1 — Node Map System: route constraint ownership (Pillar 5)**
Node Map System GDD must include an explicit section on route constraint rules: the mechanic by which vehicle state (subsystem damage, loadout gaps) gates node availability. This is not optional content — it is the primary delivery mechanism for Pillar 5 (Route Reflects Vehicle State). Without named constraints, the map becomes a neutral backdrop and Pillar 5 fails by default. At minimum: one named constraint per subsystem type (Mobility offline → blocked route type, Weapon offline → discouraged elite route, etc.).

**B2 — Vehicle & Part System: three chassis architecture, two chassis content**
The game's elevator pitch names three chassis (Scout, Assault, Heavy Truck). For EA, only Scout and Assault are in scope — Heavy Truck is explicitly post-launch. Vehicle & Part System GDD must design the chassis *architecture* to support all three (slot taxonomy, data contract, mastery track hooks) while authoring only Scout and Assault for EA. Heavy Truck slots in post-launch without requiring architectural changes.

**B3 — Chassis Identity at VS tier: intentional MVP trade-off**
Pillar 2 (Chassis Identity) is a core promise of the game. The deliberate choice to defer Chassis Identity System to VS tier is a trade-off: Prototype validates Pillar 3 (Read to Win) and Pillar 1 (Vehicle as Character) with one chassis (Scout only). Chassis differentiation testing begins at VS when Assault joins. This means Pillar 2's design test ("same strategy shouldn't work on all three chassis") cannot be validated in Prototype — this is accepted, documented, and must be explicitly revisited at the VS milestone.

**P4 — Vehicle & Part System: emotional attachment as mandatory design focus**
Pillar 1 says "part loss should feel like losing a limb, not swapping a tool." Vehicle & Part System GDD must include a mandatory design section answering: *what concretely creates emotional attachment to a specific part?* Options include: visible damage states on the vehicle silhouette (already in art bible), part acquisition history (how many combats has this part survived?), part-specific card synergies that feel like personality, or cosmetic wear that accumulates on well-used parts. The mechanic chosen must be named and specified — "parts feel important" is not a design specification.

**P5 — Enemy intent display: shared ownership between Enemy System and Combat HUD**
Pillar 3 (Read to Win) requires players to read enemy intent before acting. Enemy System GDD owns the **intent data model** — what the enemy intends to do next turn (attack target, magnitude, subsystem targeted, special behavior). Combat HUD UX Spec owns the **intent display** — how that intent is shown to the player (icon, color, position). Both GDDs must explicitly reference this handoff. Intent display is not optional — without it, the "read to win" pillar has no information channel.

---

## Production Notes (PR-SCOPE)

**P1 — Balance Sim Runner (VS Tooling Deliverable)**
A headless card combat simulation runner must exist by end of Vertical Slice (Month 5). Without it, balancing 150–200 cards by hand requires ~8 weeks of pure grind. This is infrastructure, not a game feature — allocate sprint capacity explicitly.

**P2 — Haven Ending Scope**
Haven ending is not a separate system. It is the Haven node type within Node Encounter System — a node that triggers an end-of-run screen (philosophical loop text: "everything happens in a pattern") followed by new run initialization. No narrative build-out; no cutscene system required. UI: simple overlay screen, handled under Meta UI.

**P3 — Month 7 Biome Decision Gate**
Pre-committed trigger: if Biome 2 is not content-complete by Month 7, Biome 3 is cut to post-launch. This decision is made at Month 7 regardless of optimism — it is a structural protection, not a panic call. Pre-announce Biome 3 as a free update on the store page.

**P4 — Months 9–10: Balance and Polish Only**
Months 9–10 are explicitly locked as balance + polish window. No new content enters production after Month 8. If content work bleeds into this window, content cuts — balance time does not compress.

**P5 — Visual Part ADR: Hard Deadline Month 3**
The Visual Part ADR must land before Month 3. If the ADR concludes that procedural damage overlays cost >4 weeks, descope to 20–30 part assets with static damage states. This decision gates all vehicle art production and the Vehicle & Part GDD finalization.

**P6 — Month 8 Closed Beta Milestone**
A closed beta with real player feedback is the single most effective burnout mitigation available to a solo developer during the content grind phase (Months 6–10). Target: build is feature-complete enough for external testers by Month 8. This provides a real external deadline mid-grind and playtest data while there is still time to act on it.

---

## Tooling Deliverables

| Tool | Tier | Purpose | Target |
|------|------|---------|--------|
| **Balance Sim Runner** | Vertical Slice | Headless card combat simulation — run thousands of matches to validate card balance and encounter difficulty without manual playtesting | Month 5 (end of VS) |
| **Visual Part ADR** | Before Month 3 | Formal architecture decision: procedural damage overlays vs. static damage states. Gates all vehicle art production. | Month 2 |

---

## Progress Tracker

| Metric | Count |
|--------|-------|
| Total GDD systems identified | 13 |
| Total UX Spec systems identified | 5 |
| Design docs started | 10 |
| Design docs reviewed | 10 |
| Design docs approved | 10 |
| MVP GDD systems designed | 10 / 10 |
| VS GDD systems designed | 0 / 3 |
| MVP UX Specs designed | 0 / 4 |
| VS UX Specs designed | 0 / 1 |

---

## Next Steps

- [x] Write Visual Part ADR — **ADR-0001 written 2026-04-19**, **Accepted 2026-04-21** (`docs/architecture/adr-0001-visual-vehicle-part-system.md`). Unblocks Vehicle & Part GDD finalization and vehicle art production.
- [x] Run `/design-system card-system` — **Approved 2026-04-19**
- [x] Run `/design-system save-persistence` — **Designed 2026-04-20**
- [x] Run `/design-review design/gdd/save-persistence.md` — **MAJOR REVISION NEEDED 2026-04-21**. Revised same day with scope reduction (N=1 backup, deferred migrations) + 24 blocking/recommended fixes.
- [x] Re-run `/design-review design/gdd/save-persistence.md` — **APPROVED WITH CARRY-OVERS 2026-04-21** (second review). 6 GDD-body must-fixes applied; 15 items graduate to Save ADR + implementation-gate ACs. See `design/gdd/reviews/save-persistence-review-log.md`.
- [x] Run `/design-review design/gdd/status-effects.md` — **APPROVED 2026-04-21** (light-touch review; all 8 sections present, cross-GDD consistency verified, 4 OQs carried forward).
- [x] Accept ADR-0001 (Visual Vehicle Part System) — **Accepted 2026-04-21**.
- [x] Run `/design-system vehicle-part` — **Approved 2026-04-21** (light-touch). 35 ACs, 5 OQs carried forward. Closes Status Effects OQ-SE2. See `design/gdd/reviews/vehicle-and-part-system-review-log.md`.
- [x] Run `/design-system card-combat` — **Approved 2026-04-21** (light-touch). 32 automated ACs + 2 playtest ACs. Closes OQ-SE1 (Enrage), OQ-SE3 (Redirected RNG), OQ-SE4 (Offline status), OQ-VP3 (Frame=Empty), OQ-VP4 (mid-resolution mutation), OQ-VP5 (stat floor). See `design/gdd/reviews/card-combat-system-review-log.md`.
- [x] Run `/design-system node-map` — **Approved 2026-04-21** (full review). 55 ACs across graph, storm, fuel, constraints, edge cases, save/load, UI, performance, pillar alignment. CD-GDD-ALIGN CONCERNS revised (4 fixes: Section B forfeit framing, Truck compensation contract upgraded to mandatory in F.2, AC-NM50 tightened, AC-NM55 added for Scrap/Fuel isolation). Retrofits flagged for V&P (chassis fuel multiplier + `SpendFuel`), Save (`INodeMapSerializable` + `IsCommitInProgress`), Card Combat (non-interaction with `Fuel`/`StormFrontX`), Loot & Reward (TruckRewardMultiplier ≥ 1.25×). 5 forward OQs surfaced.
- [x] Run `/design-system scrap-economy` — **Approved 2026-04-22** (full review). 11 sections A–K written; CD-GDD-ALIGN CONCERNS → REVISED 2026-04-22 (4 fixes: Section B re-voiced in wasteland register; D.6 Scrap Refund gained tenure decay mechanic (`TenureMultiplier`, `TenureDecayRate`, `TenureMinMultiplier`) for Pillar 1 defense — veteran parts refund progressively less; Overview / H.4 / G.1d softened "single most predictive number" + "admission" framing to preserve Pillar 3; I.5 Convert sub-screen + H.5/I.7 run summary given named three-domain legibility ("Build Economy" / "Route Economy" / "Combat Economy")). CD re-review: APPROVE, no new issues. 16 new registry constants registered (GlobalPurgeCost, StartingScrap.{Scout,Assault,Truck}, PityScrapAward, FreeValveProbability, InstallBaseCost.{Common,Uncommon,Rare}, BaseMerchantCost.{Common,Uncommon,Rare}, ScrapPerFuelRate, FuelPerScrapRate, RarityRepairRate.{Common,Uncommon,Rare}). Retrofits queued: Card System (remove `PurgeCost` field on `CardDefinitionSO`; collapse F5 Purge formula), V&P (expose `InstalledPart.StatModifiers` read-only + **NEW** `int CombatsSurvived { get; }` for tenure refund — High priority, blocks AC-SE7b/7c), Save & Persistence (register `ScrapStateDTO` + `FreeValveConsumedThisVisit` visit-scoped field), Card Combat (F.3 build-time invariant amendment). Closes OQ-NM5 (fuel acquisition legibility). 6 forward OQs surfaced (OQ-SE1…OQ-SE6).
- [x] Run `/design-system enemy` — **Approved 2026-04-23** (full review). 11 sections A–K written (1821 lines); CD-GDD-ALIGN → **APPROVE** no concerns (5 dimensions passed: Pillar 3 Fidelity, Pillar 2 Mirror Kicker, Tone/Register, Enrage as Creative Beat, NoOpIntent First-Class). 57 ACs (AC-ES1–AC-ES57) incl. Patch Rider 9-turn D.7 gold-standard determinism regression (AC-ES29) and ΣBaseSlotHP==BaseHullHP validator (AC-ES46). OQ-CC1 (enemy retargeting policy) CLOSED by C.6 + D.6 per-archetype RetargetPolicy modes (FixedSlot / PriorityList / ContextualRule). 15 Enemy-System-owned registry constants registered + EnrageTelegraphLeadTurns cross-referenced from Card Combat. 4 new OQs (OQ-ES1…OQ-ES4 gamepad binding, DisplayName field, empty-Armor render, tooltip anchor) + 3 telemetry-gated (OQ-ES5…OQ-ES7). Retrofits queued: Card Combat (EnrageIntentCandidates contract), V&P (SilhouetteClass art-contract field + ArchetypeFamily), Status Effects (NoOpIntent render path acknowledgment), Node Map (Biome-exclusive MVP archetype gating), Combat HUD UX (7 hard UI contract MUSTs — primary-element hard constraint top priority).
- [x] Run `/design-system loot-reward` — **Approved 2026-04-23** (full review). 11 sections A–K written (1290 lines); CD-GDD-ALIGN → **APPROVE-WITH-NOTES** (5 dimensions: Pillar 4 Scarcity-with-Agency Pass, Fantasy Coherence Pass-with-notes, Tone/Register Pass-with-notes, Mechanical Integrity Pass, Creative Risk Pass-with-notes). 55 ACs (AC-LR1–AC-LR55) across 13 surfaces: 38 unit + 11 integration + 3 playtest + 3 smoke. Pure-function contract `GenerateRewards(context, seed) → RewardOffer[]` locked with reentrancy + canonical-order + 10k-sample rarity distribution ACs. Banded-linear DSBonus locked at DS_THRESHOLD=0.40, DS_FLOOR_BONUS=4, DS_CEILING_BONUS=12 (Patch Rider D.7 gold-standard trace). Boss-skip rule (BossFlatScrap=30), per-slot cooldown (PartDropCooldownNodes=3), Fuel beacon-gating (Treasure/Event only). 14 L&R-owned registry constants registered (BiomeBaseScrap[1..3]=15/28/42, EliteScrapBonus=18, BossFlatScrap=30, DS_THRESHOLD=0.40, DS_FLOOR_BONUS=4, DS_CEILING_BONUS=12, PartDropCooldownNodes=3, PartRarityWeight.Common/Uncommon/Rare=60/30/10, FuelGrantRange.Min/Max=1/3) + 6 cross-refs added (TruckRewardMultiplier, PityScrapAward, 4× DifficultyScoreWeight). CD creative risks ranked HIGH→MEDIUM: R1 pity-fire sentiment inversion, R2 visual/audio drift toward celebratory during implementation, R3 Boss-skip absence perceived as bug. 8 forward OQs (OQ-LR1…OQ-LR8): 4 design-resolvable, 4 telemetry-gated. Retrofits queued: Card System (`ICardRewardGenerator`, CardDraft tooltip schema), V&P (`IPartCatalog.GetParts` + `InstallPart`, compare-panel), Scrap Economy (`TruckRewardMultiplier` + `PityScrapAward` referenced_by), Fuel System (`GrantFuel` + beacon gate), Save & Persistence (`LootStateDTO` serializer), Node Encounter (`GenerateRewards` call site + override payload), Post-Combat Flow UI (panel spec + SFX bus + 10 hard MUSTs carry-forward).
- [x] Run `/design-system node-encounter` — **Approved 2026-04-23** (full review). 11 sections A–K written; CD-GDD-ALIGN → **APPROVE** with 3 recommendations absorbed (Biome-3 dry-spiral telemetry-watch row added to G.6; Rest-register drift-risk added as 6th flag in H.7; OQ-NE12 tagged below as accessibility-gate milestone). 57 ACs (AC-NE1–AC-NE57) across 9 groups: Handler Dispatch & Lifecycle (8), Event Payload Determinism (7), HostileTiltDelta Tell (5), Per-Handler Rules (8), Per-Biome Distribution (4), Edge Cases (11), Observer & UI Event Contract (5), Haven Presentation (3), Exclusivity Contracts & Drift-Risk (6). Shared-contract-then-per-type structure: `INodeEncounterHandler.Begin(beacon, runSeed, frameState, economy, callback)` dispatches by BeaconType to 7 handlers {Combat, EliteCombat, Merchant, Chopshop, Event, Rest, Haven}; Event payload CDF sampling with integer weights {35/20/30/15} + commit-time asymmetric HostileTiltDelta {−5/+15/−10/0}. BeaconType reconciliation locked (Boss = EliteCombat + isBoss flag; Treasure = Event + TreasurePayload). Shell-only UI boundary with C# event observer (`OnHandlerBegin`/`OnHandlerEnd`) + 3-channel HostileTiltDelta tell (rust-shimmer + sub-200Hz audio +3dB + gamepad haptic motorLevel 0.15). Haven two exclusivity contracts (warm-orange palette ≥0.6 + sustained melodic harmonic). 14 NE-owned registry constants registered (EventBaseWeight.{Treasure/Ambush/Windfall/Convert}=35/20/30/15, EventTiltDelta.{Treasure/Ambush/Windfall/Convert}=−5/+15/−10/0, WindfallScrapGrant=12, EventConvertFavorableRate=3, MerchantOfferCount=3, ChopshopPartOfferCount=3, HandlerTimeout.NonCombat=30s / .Combat=600s) + HostileTiltDelta re-authored from scalar-0.15 to asymmetric 4-axis vector with NE now canonical source; 10 cross-refs added (ScrapPerFuelRate, FuelPerScrapRate, EliteHPScalar, EliteDamageScalar, BiomeBaseScrap.{1,2,3}, EliteScrapBonus, BossFlatScrap, FuelGrantRange.Min/Max). 12 forward OQs (OQ-NE1…OQ-NE12): 5 playtest/telemetry-gated, 2 cross-GDD retrofits pending, 2 post-MVP forward commitments, 2 asset-production specifics, 1 accessibility follow-up. Retrofits queued to 7 GDDs: Scrap Economy (zero-cost `TryRepair(…, freeRepair: true)` for Rest), Node Map (commit-time Frame sampling reaffirmation), Save & Persistence (HandlerActive save-block + auto-save at Idle transitions), L&R (salvage fallback `BiomeBaseScrap` + BeaconType axis renaming), V&P (`TryGrantFuel` FuelCap overflow semantics), Card Combat (CombatResult schema for handler-callback), Post-Combat Flow UI (handler-state observer subscription contract). **MVP core design phase CLOSED — 10/10.**

  > **Accessibility Gate — OQ-NE12 (Pre-1.0 milestone)**
  > The HostileTiltDelta 3-channel tell (rust-shimmer / sub-200Hz audio / gamepad haptic) leaves one accepted gap: keyboard-mouse players with both colorblindness AND low-frequency hearing loss receive only the visual shimmer channel (no haptic without a controller). OQ-NE12 requires a **formal accessibility review by `accessibility-specialist` before 1.0 launch** to ratify or remediate this gap (e.g., optional subtitle-style warning, UI-scale pulse, or rumble-on-keyboard fallback). This is the single accessibility commitment that must close before certification.
- [x] **Cross-GDD retrofit propagation pass — COMPLETE 2026-04-23.** All 7 target GDDs updated to absorb the accumulated MVP-phase retrofit queue (Node Encounter + prior-phase leftovers). **Status Effects**: NoOpIntent render path clause added to Interactions (Enemy System row). **Scrap Economy**: `IScrapEconomy.TryRepair` gained `bool freeRepair` parameter; D.3 free-repair path documented; F.5c rewritten for NE approval. **Loot & Reward**: F.3 Back-Reference Updates rewritten (NE moved from soft to hard retrofit with BeaconType axis reconciliation); EC-LR1 gained salvage-fallback structural invariant clause for Merchant/Chopshop/Event-Treasure consumers. **Node Map**: C3.5 handoff signature upgraded to 5-arg `Begin(beacon, runSeed, frameState, economy, callback)` with commit-time sampling invariant; F-NM5 HostileTiltApplication re-authored as asymmetric 4-axis vector; G.3 tuning-knob row renormalized to registry canonical. **Card System**: `PurgeCost` field removed from `CardDefinitionSO` data contract; F5 rewritten against `GlobalPurgeCost=30`; `ICardRewardGenerator` + `CardDraft` tooltip schema added as L&R interface contract; tuning-knob + AC + UI rows purged. **Card Combat**: R13 added (CombatResult schema + run-layer non-interaction invariant + build-time enforcement); R14 added (`IEnemyBrain.EnrageIntentCandidates` contract); Interactions row rewritten for Node Map + Node Encounter. **Vehicle & Part**: R10-R13 added — Fuel container contract (`IFuelContainer`/`IFuelMutator` with overflow semantics), InstalledPart read access + `CombatsSurvived` tenure counter, `IPartCatalog` + `PreviewInstall`, `SilhouetteClass` + `ArchetypeFamily` art contracts. **Save & Persistence**: Interactions table rewritten; `INodeMapSerializable` facade + handler save-block contracts added; R2 Write Triggers gated on `HandlerActive` / `IsCommitInProgress`; R8 DTO Schemas added (`ScrapStateDTO` with `FreeValveConsumedThisVisit`, `LootStateDTO`, `NodeEncounterStateDTO`). No new OQs introduced; retrofit queue cleared.
- [ ] Run `/propagate-design-change` (or fold into Card Combat implementation) — propagate part-granted cards into Card System GDD (per-instance provenance tracking, EC-VP11). Forward Dependency #3 from Card Combat review log. **Remaining item** — deferred to implementation phase.
- [~] ~~Run `/prototype combat`~~ — **skipped 2026-04-22** (γ decision). Walking skeleton through Slice 5c + 50 passing tests deemed sufficient validation of the core card+subsystem hypothesis. Feel validation deferred to first playable vertical slice. Accepted risk: if feel fails at VS, downstream GDDs may need rework.
- [ ] Author Save ADR (`docs/architecture/save-system-adr.md`) before first save-code commit. Owns: IL2CPP stripping config, SynchronizationContext/dispatcher choice, periodic-flush timer mechanism, dirty-flag semantics, concurrent-write queue topology, Newtonsoft allocation budget, launch-recovery latency budget, `Application.quitting` timeout bound, ThreadLocal disposal, `File.Move`/`Flush(true)` IL2CPP verification.
- [ ] Run `/design-review design/gdd/[system].md` after each GDD is authored
- [~] ~~Run `/prototype combat` after Card Combat GDD is approved~~ — **skipped 2026-04-22** (see above).
- [ ] Run `/gate-check pre-production` when all MVP GDDs are approved
- [ ] Add Balance Sim Runner to VS sprint backlog before Month 3

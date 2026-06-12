# Wasteland Run — Master Architecture Document

> **Status**: Living — updated after each `/architecture-review` or ADR acceptance
> **Engine**: Unity 6.3 LTS (pinned 2026-04-18)
> **Language**: C# (.NET Standard 2.1 via Unity)
> **Last Updated**: 2026-04-25
> **Source Review**: [architecture-review-2026-04-25.md](architecture-review-2026-04-25.md)
> **Traceability**: [architecture-traceability.md](architecture-traceability.md)
> **TR Registry**: [tr-registry.yaml](tr-registry.yaml) (257 indexed TRs)

---

## 1. Project Context

Wasteland Run is a 2D turn-based vehicular card roguelike: 30–60 minute runs across a seeded node map, with card combat against enemy vehicles via per-subsystem targeting. The architecture is shaped by five pillars that map directly to system boundaries:

| Pillar | Architectural Pressure |
|--------|------------------------|
| Vehicle as Character & Chassis Identity | Vehicle state is a **shared POCO** (read by combat, economy, map, save, UI); chassis identity is a data-authored differentiator, not a code fork |
| Read to Win | Combat **state is the source of truth**; the HUD subscribes, never owns. No hidden modifiers. |
| Scarcity with Agency | The economy is a **facade with transactional atomicity**; callers cannot sidestep state changes. |
| Route Reflects Vehicle State | The Node Map **reads vehicle state at commit time** to gate routes — vehicle is upstream of map, not the reverse. |
| Deterministic, Replayable Runs | `RunSeed` is the single identity of a run; all seeded systems derive scoped seeds by `RunSeed ^ stepIndex`. |

---

## 2. Architectural Principles

The 4 Accepted ADRs, taken together, form these load-bearing rules. Every new ADR should be consistent with them or explicitly supersede them.

### 2.1 POCO residency (from ADR-0002)
Combat state (`CombatLoop`, `SubsystemState`, `PositionState`, `Deck`, `Hand`, `Discard`, `StatusInstance`, etc.) lives in an **engine-free** assembly (`WastelandRun.Combat`, `noEngineReferences: true`). It has no `UnityEngine.*` surface. The view layer (`WastelandRun.CombatView`) subscribes to C# `Action` events.

**Why**: testable without Unity Editor, swappable view layer, deterministic simulation, and fast `dotnet test` loop.

### 2.2 Deterministic RNG discipline (from ADR-0003)
All seeded systems (combat, node map, loot generation, enemy pool, event beacons) derive their `System.Random` instance from `RunSeed ^ stepIndex`, pass the live RNG **by reference** into pure functions, and never use `UnityEngine.Random`, `Time.*`, `DateTime.Now`, `Guid.NewGuid`, or `Random.Shared`.

A CI grep enforces the forbidden-token list; the list is codified in the ADR and in the control manifest (pending).

**Why**: run reproducibility (bug reports, replays, balance testing), cross-platform determinism, seed-driven playtest reproducibility.

### 2.3 Event-driven communication (from ADR-0002 + ADR-0004)
Cross-system communication uses C# `Action`/`event` delegates. `UnityEvent` is forbidden in combat — it's slow, swallows exceptions, and hides invocation order in the Inspector. Save writes are triggered off event surfaces (combat end, merchant commit, node commit), not polled.

### 2.4 Passive orchestration + distributed schema (from ADR-0004)
The `SaveManager` is a passive orchestrator: each gameplay system owns its DTO shape and declares `public const string SystemId` and `public const int SchemaVersion` on the DTO itself. Migration is per-system. Writes are atomic (temp-then-rename) on a background `Task`, recovery is per-category independent, and the RunState vs MasteryState exhaustion policies are asymmetric (run non-blocking, mastery blocks with dialog).

### 2.5 Visual/POCO boundary (from ADR-0001)
Vehicle visuals (chassis, parts, damage states, armor overlay) live in `WastelandRun.CombatView` and use URP Sprite Lit Shader Graph + `MaterialPropertyBlock` + Addressables. The POCO Vehicle model never references `UnityEngine.*`; the view reads it via `IVehicleView` (read-only contract) and mutations go through `IVehicleMutator` (whitelisted callers only).

### 2.6 Transactional atomicity where it matters (emerging — pending Scrap Economy ADR)
Scrap transactions, Node Map commits, and save envelopes are **atomic** at their respective granularities. A partial commit is never observable to downstream systems.

---

## 3. Layered System Map

Layered per `design/gdd/systems-index.md`. ADR coverage shown per system; see [architecture-traceability.md](architecture-traceability.md) for per-TR detail.

### Layer 1 — Foundation (no dependencies)

| System | GDD | Covering ADR(s) | Status |
|--------|-----|-----------------|--------|
| Card System | `card-system.md` | ADR-0004 (deck DTO) | ⚠️ Needs "Card System Data Authoring" ADR (SO hierarchy, deck state DTO, ICardRewardGenerator) |
| Vehicle & Part System | `vehicle-and-part-system.md` | ADR-0001 (visual only) | ⚠️ Needs "Vehicle POCO Sub-Model + Part Catalog" ADR (POCO shape, IVehicleView/Mutator contracts, stat composition, IPartCatalog) |
| Save & Persistence | `save-persistence.md` | **ADR-0004** | ✅ Covered (24/25 TRs) |

### Layer 2 — Core (depends on Foundation)

| System | GDD | Covering ADR(s) | Status |
|--------|-----|-----------------|--------|
| Status Effect System | `status-effects.md` | partial via ADR-0002 (fire-before-tick timing) | ❌ Needs "Status Effects Subsystem" ADR |
| Card Combat System | `card-combat-system.md` | **ADR-0002**, ADR-0003 | ⚠️ Core covered; encounter orchestration gap |
| Scrap Economy System | `scrap-economy.md` | ADR-0004 (ScrapStateDTO) | ❌ Needs "Scrap Economy Transaction Facade" ADR |
| Node Map System | `node-map.md` | ADR-0003, ADR-0004 | ⚠️ Needs "Node Map Commit Pipeline" ADR |

### Layer 3 — Feature (depends on Core)

| System | GDD | Covering ADR(s) | Status |
|--------|-----|-----------------|--------|
| Enemy System | `enemy-system.md` | ADR-0001 (silhouette), ADR-0003 (pool RNG) | ⚠️ Needs "Enemy Data & Brain Contracts" ADR |
| Loot & Reward System | `loot-reward.md` | **ADR-0003**, ADR-0004 | ⚠️ Needs "Loot & Reward Generation Pipeline" ADR |
| Node Encounter System | `node-encounter.md` | ADR-0004 (NodeEncounterStateDTO) | ❌ Needs "Node Encounter Handler Orchestration" ADR |
| Chassis Identity (VS) | — | — | Not started — Vertical Slice scope |
| Biome (VS) | — | — | Not started — Vertical Slice scope |
| Meta Progression (VS) | — | ADR-0004 (MasteryState) | DTO shell only — Vertical Slice scope |

### Layer 4 — Presentation (UX specs, not GDDs)

| System | UX Spec | Covering ADR(s) | Status |
|--------|---------|-----------------|--------|
| Combat HUD | `design/ux/hud.md` (pending) | subscribes to ADR-0002 events | Defers to UX spec |
| Part Inspect UI | TBD | subscribes to Vehicle POCO | Defers |
| Post-Combat Flow UI | `design/ux/post-combat-flow.md` | subscribes to Loot + Save events | Defers |
| Map UI | TBD | subscribes to Node Map state | Defers |
| Meta UI (VS) | TBD | subscribes to Mastery state | Defers — VS scope |

---

## 4. Assembly Split & Module Boundaries

```
┌───────────────────────────────────────────────────────────────────────┐
│  Unity Editor / Runtime                                               │
│                                                                       │
│  ┌─────────────────────┐      ┌──────────────────────────────────┐    │
│  │ WastelandRun.View   │ ───► │ WastelandRun.Combat (POCO)       │    │
│  │ (MonoBehaviour,     │ sub- │                                  │    │
│  │  URP, Addressables, │ scribes │ noEngineReferences: true     │    │
│  │  UI Toolkit)        │      │ System.Random per step           │    │
│  │                     │      │ CombatLoop / SubsystemState /    │    │
│  │ IVehicleView        │◄──── │   PositionState / Deck / Hand    │    │
│  │ (read-only)         │ reads│ C# Action events (no UnityEvent) │    │
│  └─────────────────────┘      └──────────────────────────────────┘    │
│           │                                  │                        │
│           │                                  │ IVehicleMutator        │
│           │                                  │ (whitelisted callers)  │
│           ▼                                  ▼                        │
│  ┌─────────────────────┐      ┌──────────────────────────────────┐    │
│  │ WastelandRun.Save   │      │ WastelandRun.Gameplay            │    │
│  │                     │      │                                  │    │
│  │ SaveManager         │ ───► │ Node Map, Scrap Economy, Loot,   │    │
│  │ (passive orch.)     │ reads│ Enemy, Encounter, Status Effects │    │
│  │ Newtonsoft.Json     │ DTOs │                                  │    │
│  │ atomic writes       │      │ each system owns its DTO         │    │
│  │ File.Move overwrite │      │ const SystemId + SchemaVersion   │    │
│  └─────────────────────┘      └──────────────────────────────────┘    │
└───────────────────────────────────────────────────────────────────────┘
```

**Key boundaries:**
- `WastelandRun.Combat` (POCO) — tested with `WastelandRun.Combat.Tests.asmdef`, 136 EditMode tests green as of 2026-04-24.
- `WastelandRun.CombatView` — Unity-side renderer; subscribes to Combat events; owns URP materials, MaterialPropertyBlock, Addressables load.
- `WastelandRun.Gameplay` — node map, economy, loot, encounter handlers; plain C# where possible, MonoBehaviour only at integration seams.
- `WastelandRun.Save` — the passive orchestrator. Does not know about specific systems; reads `const SystemId` + `const int SchemaVersion` off the DTOs that gameplay systems register.

---

## 5. Cross-System Data Flow

### 5.1 Core combat loop (per turn)
```
Player card play
  → Combat POCO: Hand.Remove → Deck.Draw → Energy.Spend
  → Combat POCO: ResolveAttack (shared helper; called by both player cards & enemy intents)
  → Combat POCO: SubsystemState.ApplyDamage (Frame splash via floor(dmg/2) on non-Frame hits)
  → Combat POCO: StatusEffect.FireBeforeTick (turn-start resolution)
  → Combat POCO: emits CombatStateChanged event
  → View: subscribes, re-renders HUD / vehicle visuals / status icons
  → View: plays VFX + SFX + haptics (asymmetric HostileTiltDelta vector)
```

### 5.2 Combat end → loot → save
```
Combat POCO emits CombatEndedPayload
  → Loot & Reward (pure function, seeded with RunSeed ^ stepIndex)
  → RewardOffer emitted
  → Post-Combat Flow UI presents offer, awaits selection
  → Card System commits draft to Deck or applies pack purge
  → Scrap Economy commits scrap payout transaction
  → Node Map advances storm front, reveals next nodes
  → Save: triggered by event (not polled), atomic temp-then-rename on background Task
```

### 5.3 Node commit (route selection)
```
Player selects beacon
  → Node Map: sample vehicle state at commit time (Frame state, subsystem health, position)
  → Node Map: validate route constraints (RC-M1/E1/W1/F1)
  → Node Map: derive BeaconOutcome via seeded roll
  → Node Encounter: dispatch to appropriate handler (Combat / Merchant / Chopshop / Event / Rest / Haven / EliteCombat)
  → Save: auto-save is blocked while HandlerActive == true; resumes at handler close
```

### 5.4 Run boundary
```
Run start
  → Save loads run envelope (RunState DTO + per-system DTOs)
  → RunSeed restored to Combat POCO + Node Map + Loot
  → Recovery chain per category: live → orphaned temp → N=1 backup (independent per category)

Run end (Haven reached, or death)
  → Loot & Reward computes mastery XP delta
  → Save: final atomic write to both RunState and MasteryState
  → MasteryState write failure → blocking dialog (asymmetric policy)
```

---

## 6. Engine-Level Architectural Commitments

| Area | Commitment | Source |
|------|-----------|--------|
| Render Pipeline | URP (Universal RP) — 2D Sprite Lit Shader Graph for parts, MaterialPropertyBlock for per-renderer state, `[PerRendererData]` discipline | `.claude/docs/technical-preferences.md`, ADR-0001 |
| Render Graph | URP Compatibility Mode is **removed in 6.3** — all custom passes use Render Graph API (`RecordRenderGraph`) | `engine-reference/unity/breaking-changes.md` |
| Input | New Input System package (legacy `Input` deprecated) — KBM primary, gamepad partial; no hover-only interactions | tech-prefs |
| UI | UI Toolkit (UXML/USS) primary for runtime UI; Canvas/UGUI only when Toolkit gaps exist. USS parser stricter in 6.3 — audit required. | `engine-reference/unity/breaking-changes.md` |
| Physics | Box2D v3 under `UnityEngine.LowLevelPhysics2D` — 2D only, no 3D physics needed | tech-prefs |
| Serialization | Newtonsoft.Json (`com.unity.nuget.newtonsoft-json`) with `link.xml` IL2CPP preservation; no `BinaryFormatter` | ADR-0004 |
| Determinism | `System.Random` only; `UnityEngine.Random`, `Time.*`, `DateTime.Now`, `Guid.NewGuid`, `Random.Shared` forbidden | ADR-0003 |
| Asset Loading | Addressables for chassis/parts (`chassis-[name]`, `part-[id]`); no `Resources.Load` | ADR-0001, deprecated-apis |
| Events | C# `Action` / `event` keyword; `UnityEvent` forbidden in combat | tech-prefs, ADR-0002 |
| Serialized Fields | `[SerializeField]` on fields only (cannot apply to properties/methods in 6.3); use `[field: SerializeField]` for auto-properties | `engine-reference/unity/breaking-changes.md` |
| Reflection | `Object.FindObjectsByType<T>(FindObjectsSortMode.None)`; `FindObjectsOfType` deprecated | deprecated-apis |
| Threading | Save writes on background `Task` with atomic temp-then-rename; `Application.quitting` drain with timeout | ADR-0004 |
| Testing | Unity Test Framework (NUnit) EditMode for Combat POCO; PlayMode added when first runtime system needs integration coverage | `docs/test-strategy.md` |

---

## 7. Open Questions & Pending ADR Slate

### Open Questions (tracked in individual ADRs, resolution deferred)
- **OQ4 (ADR-0004)**: `FlushFileBuffers` behaviour on Unity 6.3 Mono runtime — verify at first save-code commit; P/Invoke fallback identified.
- **OQ5 (ADR-0004)**: `File.Move(src, dst, overwrite:true)` atomicity on Unity 6.3 Mono — verify at first save-code commit; fallback is `File.Replace` on Windows, copy+delete elsewhere.
- **Screen Reader Integration** — UAP vs custom vs post-1.0 defer; to be decided when accessibility screen-reader work is scheduled.
- **Combat HUD OQ-CH1** — Ambush urgency tint hex + pulse curve (color-only, non-blocking; geometry carries the read).

### Pending ADR Slate (priority order from review)

| # | ADR | Layer | TRs closed |
|---|-----|-------|------------|
| 1 | Vehicle POCO Sub-Model + Part Catalog | Foundation | ~8 V&P |
| 2 | Card System Data Authoring | Foundation | ~7 Card |
| 3 | Status Effects Subsystem | Core | ~18 Status |
| 4 | Scrap Economy Transaction Facade | Core | ~18 Scrap |
| 5 | Node Map Commit Pipeline | Core | ~19 Map |
| 6 | Node Encounter Handler Orchestration | Feature | ~25 Encounter |
| 7 | Enemy Data & Brain Contracts | Feature | ~22 Enemy |
| 8 | Loot & Reward Generation Pipeline | Feature | ~14 Loot |

Foundation ADRs (1, 2) should land **before first story** for their systems. Core + Feature ADRs are scheduled for Pre-Production, gated per-system (ADR before stories for that system).

---

## 8. Sources

### Accepted ADRs
- [ADR-0001 — Visual Vehicle Part System](adr-0001-visual-vehicle-part-system.md) (Accepted 2026-04-21)
- [ADR-0002 — Card Combat State & Event Architecture](adr-0002-card-combat-state-event.md) (Accepted 2026-04-24)
- [ADR-0003 — Loot RNG Determinism](adr-0003-loot-rng-determinism.md) (Accepted 2026-04-24)
- [ADR-0004 — Save & Persistence Architecture](adr-0004-save-persistence-architecture.md) (Accepted 2026-04-24)

### Review & Traceability
- [Architecture Review 2026-04-25](architecture-review-2026-04-25.md) — verdict CONCERNS (PASSABLE) for Technical Setup → Pre-Production
- [Architecture Traceability Index](architecture-traceability.md)
- [TR Registry (257 TRs)](tr-registry.yaml)

### Engine Reference
- [Unity 6.3 LTS Version Pin](../engine-reference/unity/VERSION.md)
- [Breaking Changes](../engine-reference/unity/breaking-changes.md)
- [Deprecated APIs](../engine-reference/unity/deprecated-apis.md)

### Project Standards
- [Technical Preferences](../../.claude/docs/technical-preferences.md)
- [Coding Standards](../../.claude/docs/coding-standards.md)
- [Coordination Rules](../../.claude/docs/coordination-rules.md)
- [Test Strategy](../test-strategy.md)

### Design
- [Systems Index](../../design/gdd/systems-index.md)
- [Game Concept](../../design/gdd/game-concept.md)
- [Game Pillars](../../design/gdd/game-pillars.md)

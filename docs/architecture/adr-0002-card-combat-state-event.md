# ADR-0002: Card Combat — POCO state model with engine-free assembly, deterministic seeding, and exception-based validation

## Status

Accepted

## Date

2026-04-24

## Last Verified

2026-04-24

## Decision Makers

- User (creative/design lead)
- technical-director (architectural review, pending)
- unity-specialist (engine-idiom review, pending)

## Summary

Card Combat is implemented as a plain C# model in an assembly with `noEngineReferences: true`, presented by a separate view assembly that depends on it one-way. All RNG is `System.Random` with an explicit seed (never `UnityEngine.Random`), events flow via `System.Action`/`event` (never `UnityEvent`), and the model validates inputs by throwing typed exceptions while returning result types for successful resolution.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.13f1) |
| **Domain** | Core / Scripting |
| **Knowledge Risk** | LOW — standard C# + Unity Assembly Definitions, no post-cutoff APIs |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/breaking-changes.md`, `.claude/docs/technical-preferences.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | None — audited clean under TD-C1 spike (2026-04-24) |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None (foundational) |
| **Enables** | ADR-0003 Loot RNG (same `System.Random` + seed pattern), ADR-0004 Save & Persistence (POCO tree is trivially serializable) |
| **Blocks** | Combat HUD UX spec implementation; any Production epic story that binds UI/VFX to combat state |
| **Ordering Note** | Must be Accepted before ADR-0003 and ADR-0004 are authored — both lean on decisions made here (determinism contract, POCO serialization shape). |

## Context

### Problem Statement

Card Combat is the single most tested and most code-heavy system in the project (136 EditMode tests, ~2,800 LOC as of `015b904`). The assumptions baked into its implementation — where state lives, how events flow, how tests reach the model, how randomness is controlled — are load-bearing for every downstream system: save/load must persist this state, combat HUD must read this state, loot rolls must share this RNG discipline, analytics must observe these events.

These decisions were made implicitly during the console-prototype → Unity backfill. They must be captured now, while the code is fresh and the test suite proves they hold, before downstream ADRs (Loot, Save) lean on contracts that were never explicitly written down.

### Current State

As of commit `015b904`:

- Two Unity Assembly Definitions exist:
  - `Assets/Scripts/Combat/WastelandRun.Combat.asmdef` with `"noEngineReferences": true`
  - `Assets/Scripts/CombatView/WastelandRun.CombatView.asmdef` referencing `WastelandRun.Combat`
- The combat model is ~23 plain C# classes (`CombatLoop`, `Vehicle`, `Slot`, `Deck`, `EnemyIntent`, `CardDefinition`, etc.) with zero `UnityEngine.*` imports.
- The view layer (`CombatController.cs`) is a single `MonoBehaviour` drawing IMGUI over model state, read via property access each `OnGUI` frame.
- `System.Random` is used for deck shuffling with a seed threaded through `CombatLoop.Setup(..., seed: int?)`.
- Validation is mixed: `InvalidCardPlayException` / `InvalidCombatantException` for illegal inputs; `CardPlayResult` / `AttackResult` / `DamageResult` / `RepairResult` for successful resolution.
- 136 EditMode tests execute in the POCO assembly without any scene or play-mode bootstrap.

### Constraints

- **Tech-prefs forbidden patterns (enforced)**: no `UnityEvent` in combat, no combat state on `MonoBehaviours`, no `UnityEngine.Random` in seeded systems, no hardcoded gameplay values.
- **Unity 6.3 breaking changes**: `[SerializeField]` fields-only, `FindObjectsByType` replaces `FindObjectsOfType`, URP Compat Mode removed — all irrelevant to a POCO combat model (confirmed by TD-C1 spike).
- **Runtime budget**: 60 FPS target / 16.6 ms frame. A turn resolve is sub-millisecond (no allocations in hot path, simple list ops).
- **Determinism**: Run seeds must reproduce identical outcomes for debug, test, and future replay capture.

### Requirements

- State model must be fully testable in EditMode without a scene, play-mode runner, or Unity bootstrap.
- Same run seed must produce the same card draws and the same brain RNG decisions across sessions.
- The view layer must never be able to write to combat state through a back door — physical separation required.
- Card resolution must be deterministic (given seed + inputs, result is identical).
- Tests must run in <5 seconds wall-clock for the full suite so TDD stays fluid.

## Decision

Card Combat is implemented as a plain C# state model in an engine-free assembly, consumed by a separate view assembly via one-way reference. Five specific rules govern the boundary.

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  WastelandRun.CombatView.asmdef                                 │
│  (references WastelandRun.Combat + UnityEngine)                 │
│                                                                 │
│  ┌──────────────────────────────────┐                           │
│  │ CombatController : MonoBehaviour │ ─── reads state ──┐       │
│  │  (IMGUI today, UI Toolkit later) │                   │       │
│  └──────────────────────────────────┘                   │       │
└──────────────────────────────────────────────────────────┼──────┘
                                                          │
                          one-way reference               │
                          (asmdef enforced)               │
                                                          ▼
┌─────────────────────────────────────────────────────────────────┐
│  WastelandRun.Combat.asmdef   ("noEngineReferences": true)      │
│                                                                 │
│  ┌──────────────┐    ┌───────────┐    ┌────────────────┐        │
│  │ CombatLoop   │───▶│  Vehicle  │───▶│     Slot       │        │
│  │ (orchestrator│    │ (combatant│    │ (subsystem     │        │
│  │  deck, rng,  │    │  state,   │    │  hp, plating,  │        │
│  │  phase)      │    │  damage)  │    │  damage state) │        │
│  └──────┬───────┘    └───────────┘    └────────────────┘        │
│         │                                                       │
│         ├──▶ Deck / Hand / Discard  (System.Random seeded)      │
│         ├──▶ IEnemyBrain            (pure interface)            │
│         └──▶ EnemyIntent            (value struct)              │
└─────────────────────────────────────────────────────────────────┘
                         ▲
                         │ references (EditMode only)
                         │
┌────────────────────────┴────────────────────────────────────────┐
│  Tests/EditMode/Combat/  (NUnit, 136 tests, runs headless)      │
└─────────────────────────────────────────────────────────────────┘
```

### Key Interfaces

```csharp
// --- Entry point ---------------------------------------------------------
public static CombatLoop Setup(
    Vehicle player,
    Vehicle enemy,
    IEnemyBrain enemyBrain = null,
    int maxEnergy = DefaultMaxEnergy,
    EncounterType encounter = EncounterType.Standard,
    IEnumerable<CardDefinition> startingDeck = null,
    int handSize = DefaultHandSize,
    int? seed = null);             // explicit seed — reproducible runs

// --- Player action contract ---------------------------------------------
public CardPlayResult PlayCard(CardDefinition card, SlotType targetSlot);
//   throws InvalidCardPlayException when:
//     - phase is not PlayerTurn
//     - card not in hand (deck mode)
//     - required subsystem offline (Symmetric Fizzle player side)
//     - energy cost > current energy
//   returns CardPlayResult carrying full resolution payload (Attack/Plate/
//     Reposition/Repair union) — never returns null, never returns bool.

// --- Phase transitions --------------------------------------------------
public void EndPlayerTurn();       // PlayerTurn → PlayerResolve → EnemyTurn
public void EndEnemyTurn();        // EnemyTurn → PlayerTurn (or Ended)

// --- Read-only state for view/analytics/save ---------------------------
public CombatPhase Phase { get; }
public int TurnCount { get; }
public int CurrentEnergy { get; }
public EnemyIntent? CurrentEnemyIntent { get; }
public IReadOnlyList<CardDefinition> Hand { get; }
public IReadOnlyList<CardDefinition> Discard { get; }
public int DeckCount { get; }

// --- Enemy brain contract ---------------------------------------------
public interface IEnemyBrain {
    EnemyIntent PickIntent(CombatLoop combat);
}
```

### Implementation Guidelines

1. **Keep the POCO assembly engine-free.** Do not add `using UnityEngine;` to any file under `Assets/Scripts/Combat/`. The asmdef enforces this at compile time (`noEngineReferences: true`); adding an engine reference will break the build, which is the desired failure mode.

2. **State lives on the model, not the view.** When adding new combat data (status effects, temporary buffs, per-turn counters), extend `Vehicle` / `Slot` / `CombatLoop` — not the `MonoBehaviour` view. The view is a read-only presenter.

3. **Every RNG call threads through an injected `System.Random`.** New systems that need randomness take the `CombatLoop`'s `_rng` (or their own seeded `System.Random`). Never call `UnityEngine.Random.*` from combat code. Never read `Time.time` / `DateTime.Now` for non-determinism sources.

4. **Events are C# `event` / `Action` delegates.** When push-based notification is added (e.g., for VFX hooks, analytics, sound triggers), expose them as `public event Action<DamageResult> DamageApplied;` — never `UnityEvent`. UnityEvent swallows exceptions and makes combat debugging unreliable (per tech-prefs forbidden patterns).

5. **Validation asymmetry: throw on illegal, return payload on legal.** Inputs that can only come from a bug (null card, wrong phase, card not in hand) throw typed exceptions. Inputs that can come from a legitimate player action but didn't resolve in full (attack that hit armor only, repair that didn't change state) return a result type with enough payload to drive UI narration.

6. **Symmetric Fizzle is asymmetric by design (OQ-CC-NEW-3/4).**
   - Player side: `RequireSubsystemOnlineFor` runs before energy is consumed → throws → energy and hand preserved so the player can re-plan the turn.
   - Enemy side: fizzle is checked at `ResolveEnemyIntent` → damage = 0, turn is consumed (the brain's intent was already locked in during enemy-turn transition).
   - `CardKind.Repair` is never gated — it is the explicit escape from a fizzle-lock state for both sides.

7. **Tests live in `Assets/Tests/EditMode/Combat/` and reference `WastelandRun.Combat` only.** No play-mode tests for combat. The POCO model compiles and runs in EditMode; any test that needs a scene is by definition testing the view, not the combat rules.

## Alternatives Considered

### Alternative 1: MonoBehaviour-owned combat state

- **Description**: `CombatLoop`, `Vehicle`, `Slot` all inherit from `MonoBehaviour`. State is stored on GameObjects in the scene. Tests must use `PlayMode` and spin up a scene.
- **Pros**: Tuning visible in inspector; Unity-idiomatic for designers browsing the hierarchy.
- **Cons**: Untestable in EditMode (requires scene bootstrap per test — ~seconds per test instead of milliseconds). Violates forbidden pattern "combat state stored on MonoBehaviours" from `technical-preferences.md`. Couples combat rules to Unity lifecycle (`Awake`/`OnDestroy` timing surprises). Save/load becomes harder — MonoBehaviours don't serialize cleanly across scenes.
- **Estimated Effort**: Comparable authoring effort; significantly higher test + debug cost.
- **Rejection Reason**: Directly violates a committed forbidden pattern and the 136-test suite becomes infeasible (minutes instead of seconds to run).

### Alternative 2: Entities/DOTS ECS combat

- **Description**: Combat state as `IComponentData` structs on entities; turn resolution via `ISystem`. Enemy brain becomes a system reading position components.
- **Pros**: Future-proof for large-scale combat; Burst-compilable; fits Unity 6 official direction.
- **Cons**: The scale doesn't justify it — this is a turn-based card game with exactly 2 combatants resolving in sub-millisecond time, not a mass-combat sim. Entities 1.3+ API is post-cutoff HIGH risk per `docs/engine-reference/unity/VERSION.md` — LLM assistance is unreliable. Authoring cost is ~3–5× POCO for no measurable benefit. Loses the "compiles against zero engine references" test story.
- **Estimated Effort**: 3–5× the POCO effort.
- **Rejection Reason**: Matching architectural complexity to actual scale. Reserve DOTS for a system that actually benefits (particle field, swarm AI, node-map pathfinding at scale if it ever needs it).

### Alternative 3: ScriptableObject-backed state

- **Description**: `Vehicle` and `Slot` as `ScriptableObject` instances; designers tune in asset inspector.
- **Pros**: Designer-friendly asset workflow for fixed vehicles/parts.
- **Cons**: ScriptableObjects are designed for **shared config**, not **per-combat instance data**. Using SO for runtime state leaks state between combats (asset modifications persist), or requires expensive `Instantiate` dance on every combat start. Fights Unity's asset lifecycle; undo/save bugs common.
- **Estimated Effort**: Higher than POCO once instantiation discipline is accounted for.
- **Rejection Reason**: Wrong tool — SO is for *definitions* (CardDefinition-style), not for *instance state* (Vehicle/Slot in active combat). Our `CardDefinition` may itself become an SO-backed asset later; `Vehicle`/`Slot` will not.

## Consequences

### Positive

- **Tests run in milliseconds, not seconds.** The 136-test suite executes in <1s wall-clock (vs. minutes for play-mode equivalent). Preserves fluid TDD.
- **Compile-time guardrail** against the forbidden pattern "combat state on MonoBehaviours." You physically cannot write `MonoBehaviour : CombatLoop` — the `noEngineReferences: true` flag is a hard wall.
- **Deterministic replay** is trivial: capture seed + player actions → re-run produces identical outcome. Enables bug repro and future replay-share features without extra architecture.
- **Save/load will be straightforward.** POCO trees serialize cleanly via `System.Text.Json` / `MessagePack` — ADR-0004 will lean on this.
- **Engine-upgrade cost is bounded.** A future Unity 7 migration touches the view layer only; the combat model is pure C# and migrates unchanged.
- **LLM assistance is high-fidelity.** POCO + `System.Random` are deeply in training data; no post-cutoff engine APIs means generated code compiles first try.

### Negative

- **Designers cannot tune combat values in the Unity inspector.** Tuning data will live in `CardDefinition` SOs or ScriptableObject-backed definitions (separate ADR if/when that becomes a bottleneck) — not on the `CombatLoop` object itself.
- **No automatic scene integration.** The view layer must wire state-to-UI binding manually. This is deliberate, but it's code the view layer now owns.
- **Event plumbing is explicit.** Adding a new push notification (e.g., "fire VFX when armor plated") means authoring an `event` and wiring a subscriber in the view — no drag-in-inspector binding via UnityEvent.
- **Contributors must understand the asmdef split.** A developer unfamiliar with Unity's assembly boundaries may try to add `using UnityEngine;` to the combat namespace and be confused when it fails to compile. This is surfaced in the control manifest.

### Neutral

- The IMGUI shell (`CombatController.OnGUI`) is placeholder — it will be replaced by UI Toolkit per a separate UI ADR. That migration does not touch this ADR's decisions.
- `CardDefinition` is currently a plain C# class constructed in code (`StarterDecks.MakeBasic()`). A later ADR may promote it to a ScriptableObject asset for designer-authored decks; that would not change this ADR.

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| Developer adds `using UnityEngine;` to combat namespace, bypassing asmdef intent via a second asmdef | LOW | MED | Control manifest entry: "Combat/ asmdef MUST have noEngineReferences: true" + code-review checklist item |
| Hidden non-determinism sneaks in (someone uses `Time.time`, `DateTime.Now`, `Guid.NewGuid()`) | MED | HIGH (breaks replay, balance tests) | Add a unit test that runs the same seed twice and asserts identical outcome; lint-level grep in CI for forbidden tokens in Combat/ |
| Future "just use UnityEvent for this one thing" pressure from an unfamiliar contributor | MED | MED | Control manifest FORBIDDEN entry + forbidden-patterns test (grep for `UnityEvent` in Combat/ .cs files) |
| State grows beyond what POCO comfortably serializes (cycles, large arrays) | LOW | MED | ADR-0004 will define the serialization contract; revisit here if POCO shape causes save-file bloat |
| EditMode test speed regresses as combat grows (status effects, 5 enemies per combat) | LOW | LOW | Monitor suite wall-clock in CI; hard ceiling of 10s for the combat suite — architect around if approached |

## Performance Implications

| Metric | Before (prototype console) | Expected After (Unity POCO) | Budget |
|--------|----------------------------|-----------------------------|--------|
| Turn resolve CPU | ~0.1 ms | ~0.1 ms | 16.6 ms / frame |
| EditMode test suite | N/A | <1 s for 136 tests | <10 s |
| Memory (per combat) | ~5 KB | ~8 KB (w/ deck state) | 2 GB ceiling |
| GC allocations per card play | negligible | ~1 KB (result struct + log list add) | No per-frame allocations outside card plays |

No per-frame work in the model — combat advances only on player input + end-of-turn. View polls state each `OnGUI` frame (acceptable for IMGUI debug shell; UI Toolkit migration will move to event subscription).

## Migration Plan

Not applicable — this ADR captures decisions already in the commit (`015b904`). The 136-test suite is the validation artifact.

**Rollback plan**: If a fundamental flaw is discovered (e.g., the POCO model can't express a required mechanic), mark this ADR `Superseded` and write a replacement. The two-assembly split means the combat model can be rewritten without touching view code — an entire `WastelandRun.Combat` rewrite is a bounded refactor, not a game-wide earthquake.

## Validation Criteria

- [x] `WastelandRun.Combat.asmdef` has `"noEngineReferences": true` (verified in file).
- [x] 136 EditMode tests pass against the POCO model (verified in commit `015b904`).
- [x] Seeded run reproduces identical card sequence (asserted by existing deck tests).
- [x] Symmetric Fizzle tests (11) + self-repair tests (14) confirm validation asymmetry (verified in commit).
- [x] TD-C1 engine-validation spike found zero Unity 6.3 breaking-change hits in the combat namespace (2026-04-24).
- [ ] Code-review checklist entry added to control manifest (pending — follow-on task).
- [ ] Forbidden-pattern CI grep covers `UnityEvent` / `UnityEngine.Random` in `Assets/Scripts/Combat/` (pending — follow-on task).

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|---------------------------|
| `design/gdd/card-combat-system.md` | Card Combat | R1 — CombatLoop orchestrates turns | `CombatLoop` owns Phase state machine and transitions (`Setup → PlayerTurn → PlayerResolve → EnemyTurn → ...`) |
| `design/gdd/card-combat-system.md` | Card Combat | R11 — Frame required to enter combat | `Setup()` calls `RequireFrameInstalled` on both combatants; throws `InvalidCombatantException` if missing |
| `design/gdd/card-combat-system.md` | Card Combat | R15 — EncounterType (Standard/Ambush) axis with Ambush pre-turn-1 setup | `EncounterType` enum + `SeedPositions()` + `ResolveAmbushSetup()` in `Start()` |
| `design/gdd/card-combat-system.md` | Card Combat | R16 — Position axis (Ahead/Behind) on Vehicle | `Position` enum + `Vehicle.Pos` + `SetPosition` / `Opposite` helpers |
| `design/gdd/card-combat-system.md` | Card Combat | R17 — Enemy intent position-requirement filtering | `PositionRequirement` on `EnemyIntent` + `PoolBrain` filtering |
| `design/gdd/card-combat-system.md` | Card Combat | R18 — Reposition synthetic intent when pool fully filtered | `IntentPool` synthesizes `RepositionIntent` fallback; `IntentKind.Reposition` resolved distinctly |
| `design/gdd/card-combat-system.md` | Card Combat | F-CC1 — Damage pipeline (slot vs Frame with splash) | `CombatLoop.ResolveAttack` + `Vehicle.ApplyDamage` + `AttackResult` with `Primary` / `Splash` payload |
| `design/gdd/card-combat-system.md` | Card Combat | F-CC5 — PositionBonus preview + resolution | `PositionBonus` struct + `EnemyIntent.PreviewDamage(enemyPos, playerPos)` |
| `design/gdd/card-combat-system.md` | Card Combat | OQ-CC-NEW-2 — Per-combat armor reset | `CombatLoop.Start()` calls `Player.ResetArmor()` / `Enemy.ResetArmor()` |
| `design/gdd/card-combat-system.md` | Card Combat | OQ-CC-NEW-3/4 — Symmetric Fizzle matrix | `RequireSubsystemOnlineFor` (player throws) + `IsSubsystemOffline` guard in `ResolveEnemyIntent` (enemy fizzles); Repair ungated |
| `.claude/docs/technical-preferences.md` | Forbidden patterns | No `UnityEvent` in combat; no combat state on MonoBehaviours; no `UnityEngine.Random` in seeded systems | Assembly split + `noEngineReferences: true` makes all three violations compile errors or physically impossible in the combat namespace |

## Related

- **Follows from**: Console prototype archives (`prototypes/combat/` v3/v4, `prototypes/combat-position-ambush/` v5) — validated findings backfilled into Unity at commit `015b904`.
- **Will be consumed by**:
  - ADR-0003 Loot RNG (same `System.Random` + explicit-seed pattern)
  - ADR-0004 Save & Persistence (POCO tree serialization shape)
  - Combat HUD UX spec (binding contract: read-only properties on `CombatLoop`)
- **Implementation**: `Assets/Scripts/Combat/` (POCO model), `Assets/Scripts/CombatView/CombatController.cs` (view), `Assets/Tests/EditMode/Combat/` (test suite)
- **Engine audit**: `docs/engine-reference/unity/VERSION.md` + TD-C1 spike notes (in `production/session-state/active.md`, 2026-04-24)

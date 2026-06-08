# Technical Preferences

<!-- Populated by /setup-engine. Updated as the user makes decisions throughout development. -->
<!-- All agents reference this file for project-specific standards and conventions. -->

## Engine & Language

- **Engine**: Unity 6.3 LTS
- **Language**: C#
- **Rendering**: Universal Render Pipeline (URP) — appropriate for 2D stylized game (RUST ICON visual direction)
- **Physics**: Unity 2D Physics (Box2D wrapper) — 2D card/vehicular game, no 3D physics needed

## Input & Platform

<!-- Written by /setup-engine. Read by /ux-design, /ux-review, /test-setup, /team-ui, and /dev-story -->
<!-- to scope interaction specs, test helpers, and implementation to the correct input methods. -->

- **Target Platforms**: PC (Steam)
- **Input Methods**: Keyboard/Mouse, Gamepad
- **Primary Input**: Keyboard/Mouse
- **Gamepad Support**: Partial (recommended but not primary — should not gate any feature)
- **Touch Support**: None
- **Platform Notes**: Standard PC UI conventions. All core interactions must be keyboard/mouse accessible. Gamepad support is additive — no hover-only interactions.

## Naming Conventions

- **Classes**: PascalCase (e.g., `CombatManager`, `ChassisDefinition`)
- **Public fields/properties**: PascalCase (e.g., `MoveSpeed`, `CurrentHullHp`)
- **Private fields**: _camelCase with underscore prefix (e.g., `_currentEnergy`, `_installedParts`)
- **Methods**: PascalCase (e.g., `TakeDamage()`, `ResolveCardEffect()`)
- **Events**: C# `Action` delegates or `event` keyword — **no `UnityEvent` in combat systems** (too slow, swallows exceptions)
- **Files**: PascalCase matching class name (e.g., `CombatManager.cs`, `ChassisDefinition.cs`)
- **Scenes/Prefabs**: PascalCase matching root node (e.g., `CombatScene.unity`, `ScoutChassis.prefab`)
- **Constants**: UPPER_SNAKE_CASE for true constants; PascalCase for readonly fields

## Performance Budgets

- **Target Framerate**: 60fps
- **Frame Budget**: 16.6ms
- **Draw Calls**: 200 (2D card game — UI-heavy; monitor closely once card VFX are added)
- **Memory Ceiling**: 2GB

## Testing

- **Framework**: NUnit via Unity Test Framework (built-in to Unity)
- **Minimum Coverage**: [TO BE CONFIGURED — set at architecture phase]
- **Required Tests**: Balance formulas, card effect resolution, subsystem state machine, loot table generation

## Forbidden Patterns

<!-- Add patterns that should never appear in this project's codebase -->
- **UnityEvent in combat systems** — use C# events or Actions; UnityEvent is slow and swallows exceptions, making combat state debugging unreliable
- **Combat state stored on MonoBehaviours** — keep combat state as plain C# model (POCO) separate from MonoBehaviours; view layer subscribes to state changes
- **UnityEngine.Random for seeded systems** — use `System.Random` with explicit seed for deterministic run generation (node map, loot tables); `UnityEngine.Random` is global state and breaks reproducibility
- **Hardcoded gameplay values** — all tunable values must be in ScriptableObjects or data files; no magic numbers in gameplay code

## Allowed Libraries / Addons

<!-- Add approved third-party dependencies here. Only add when actively integrating, not speculatively. -->
- **Unity Addressables** (accepted 2026-05-25) — runtime asset reference
  and lazy-load mechanism. Approved scope: chassis art bundle loading
  per ADR-0007 §3.3 (`AssetReferenceT<ChassisArtBundle>`) and forward
  use in ADR-0001 vehicle-part art pipeline. Tracking ADR: **ADR-0008
  (Accepted)** — memory budget (41 MB EA cap), build pipeline (local groups,
  auto catalog rebuild), runtime discipline (async-only, release-on-teardown,
  IL2CPP smoke test). Routing: `unity-addressables-specialist`.

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
- **ADR-0001** (Proposed) — Visual vehicle part system scope (gates vehicle art production — see game-concept.md risks)
- **ADR-0002** (Accepted) — Card Combat: POCO state model with engine-free assembly, deterministic seeding, exception-based validation (captures commit `015b904` architecture)
- **ADR-0003** (Accepted) — Deterministic RNG discipline: per-call `System.Random` from `RunSeed ^ stepIndex`, live RNG passed by reference, forbidden non-determinism tokens enforced by CI grep (generalizes ADR-0002 pattern to all seeded run systems)
- **ADR-0004** (Accepted) — Save & Persistence: passive orchestrator over per-system DTOs, distributed schema registry (`SystemId` + `SchemaVersion` constants on each DTO, CI-enforced unique), atomic temp-then-rename writes on background `Task`, per-category independent recovery chain (live → orphaned temp → N=1 backup), asymmetric exhaustion policy (RunState non-blocking, MasteryState blocking dialog), Newtonsoft.Json + `link.xml` IL2CPP preservation, `RunSeed` persisted per ADR-0003 (resolves TD-C2)
- **ADR-0008** (Accepted 2026-05-25) — Unity Addressables approved for chassis art lazy-load + vfx-combat overlay textures; memory budget 41 MB EA; local-only groups; async-only API rule; release-on-teardown unload contract; IL2CPP smoke test required before first shipping build
- **ADR-0010** (Accepted 2026-05-31) — Slot System Single Vocabulary: retires `LegacySlotKind` / `LegacyKindBridge` / `IsLegacyMode` / Vehicle-level armor pool; single `string slotId` runtime identifier; six-phase Unity-side execution; CI grep gate at Phase 5; supersedes ADR-0007 + ADR-0009 (incl. 2026-05-30 Amendment classifying bridge permanent)
- **ADR-0011** (Accepted 2026-05-31) — Project-wide no-bridges meta-rule: 8 forbidden patterns at done state (adapter layers, parallel storage, bimodal paths, vestigial enums, compat overloads, stub returns, transitional comments, duplicate enums); 5 explicit exceptions (one-shot migrators, schema version fields, editor authoring, polymorphism, CI grep gates); ADR-0010 is its first concrete application
- **ADR-0012** (Accepted 2026-06-02) — Part Data Authoring + Sum-of-Parts Armor: introduces `PartDefinitionSO` (PartId, SlotKind, MaxHp, ArmorContribution, sprite ref); `armor_0.MaxHp = Σ installed part.ArmorContribution`, recomputed on install/uninstall/state transitions; `InstallPart` gets SO overload for player + default-param int overload (`armorContribution=0`) for enemies/tests; deletes `VehicleDefinitionSO._armorHp` and rebuilds it as `List<PartSlot>`; closes ADR-0010 Amendment A; enemy Part SO catalog deferred (non-goal)

## Engine Specialists

<!-- Written by /setup-engine when engine is configured. -->
<!-- Read by /code-review, /architecture-decision, /architecture-review, and team skills -->
<!-- to know which specialist to spawn for engine-specific validation. -->

- **Primary**: unity-specialist
- **Language/Code Specialist**: unity-specialist (C# review — primary covers it)
- **Shader Specialist**: unity-shader-specialist (Shader Graph, HLSL, URP/HDRP materials, VFX Graph)
- **UI Specialist**: unity-ui-specialist (UI Toolkit UXML/USS, UGUI Canvas, runtime UI performance)
- **Additional Specialists**: unity-dots-specialist (ECS, Jobs system, Burst compiler — if needed for card simulation), unity-addressables-specialist (asset loading, memory management, content catalogs)
- **Routing Notes**: Invoke primary for architecture and general C# code review. Invoke DOTS specialist only if ECS/Jobs/Burst are adopted. Invoke shader specialist for all rendering and visual effects work. Invoke UI specialist for all interface implementation (combat HUD, map UI, card layout). Invoke Addressables specialist when asset management systems are built.

### File Extension Routing

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (.cs files) | unity-specialist |
| Shader / material files (.shader, .shadergraph, .mat) | unity-shader-specialist |
| UI / screen files (.uxml, .uss, Canvas prefabs) | unity-ui-specialist |
| Scene / prefab / level files (.unity, .prefab) | unity-specialist |
| Native extension / plugin files (.dll, native plugins) | unity-specialist |
| General architecture review | unity-specialist |

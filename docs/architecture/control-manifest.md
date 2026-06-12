# Control Manifest

> **Engine**: Unity 6.3 LTS
> **Last Updated**: 2026-05-25
> **Manifest Version**: 2026-05-25
> **ADRs Covered**: ADR-0001, ADR-0002, ADR-0003, ADR-0004, ADR-0005, ADR-0006, ADR-0007
> **Status**: Active — regenerate with `/create-control-manifest update` when ADRs change

`Manifest Version` is the date this manifest was generated. Story files embed
this date when created. `/story-readiness` compares a story's embedded version
to this field to detect stories written against stale rules. Always matches
`Last Updated` — they are the same date, serving different consumers.

This manifest is a programmer's quick-reference extracted from all Accepted ADRs,
technical preferences, and engine reference docs. For the reasoning behind each
rule, see the referenced ADR.

---

## Foundation Layer Rules

*Applies to: scene management, event architecture, save/load, engine initialisation, assembly architecture, deterministic RNG*

### Required Patterns

- **`WastelandRun.Combat.asmdef` compiles with `noEngineReferences: true`** — source: ADR-0002
- **`WastelandRun.Vehicle.asmdef` compiles with `noEngineReferences: true`** — source: ADR-0005
- **`WastelandRun.Cards.asmdef` compiles with `noEngineReferences: true`** — source: ADR-0006
- **Engine-free vehicle contract types (`SlotDefinition`, `SlotPosition`, `AnchorPoint`, `IFrameLayout`) live in `WastelandRun.Vehicle`; `FrameLayoutSO` (the Unity-bearing authoring asset) lives in `WastelandRun.Gameplay`** — source: ADR-0007 (Decisions 2 + 3)
- **Per-call `System.Random` construction — never a singleton or shared instance** — source: ADR-0003
- **Exactly one `System.Random` per seeded entry point; seed = `RunSeed ^ stepIndex`** — source: ADR-0003
- **RNG passed by reference down the call graph** — source: ADR-0003
- **`seeded-directories.yaml` documents all directories under deterministic RNG discipline** — source: ADR-0003
- **Each save DTO declares `const string SystemId` and `const int SchemaVersion`** — source: ADR-0004
- **Atomic file write: write to temp file in `Application.temporaryCachePath` → `File.Move(destFileName, overwrite: true)`** — source: ADR-0004
- **All file writes run on a background `Task`** — the only carve-outs are (a) the synchronous final flush during `Application.quitting` and (b) launch-time read-side temp-file scan/recovery. Normal gameplay writes must never block the main thread — source: ADR-0004
- **`RunSeed` persisted as-is; scoped seeds re-derived at runtime from `RunSeed ^ stepIndex`, never persisted directly** — source: ADR-0004

### Forbidden Approaches

- **Never `UnityEngine.Random` in any seeded system** — global state, non-reproducible — source: ADR-0003
- **Never `Time.time`, `Time.frameCount`, `Time.realtimeSinceStartup` in seeded systems** — non-deterministic across frames — source: ADR-0003
- **Never `DateTime.Now`, `DateTime.UtcNow`, `Environment.TickCount` in seeded systems** — clock-dependent — source: ADR-0003
- **Never `Guid.NewGuid()`, `Random.Shared`, or static mutable fields in seeded systems** — source: ADR-0003
- **Never reference `FrameLayoutSO` from `WastelandRun.Vehicle`** — it is a `ScriptableObject`; importing it breaks `noEngineReferences: true` — source: ADR-0007
- **Never reference `CardDefinitionSO` from any engine-free assembly (`WastelandRun.Vehicle`, `WastelandRun.Combat`, `WastelandRun.Cards`)** — `CardDefinitionSO` is a `ScriptableObject` and belongs in `WastelandRun.Gameplay` only — source: ADR-0006
- **Never apply both `IRunStateSerializable` and `IMasteryStateSerializable` to the same DTO** — mutually exclusive interfaces — source: ADR-0004
- **Never call `SaveSystem` methods beyond `ComputeEnvelopeChecksum` and `NowTimestamp`** — source: ADR-0004
- **CI forbidden-token grep must fail the build** if any of the ADR-0003 forbidden tokens appear in covered directories — source: ADR-0003

### Performance Guardrails

- **Per-category recovery chain**: live save → orphaned temp → N=1 backup; categories are independent (failure in one category does not abort another) — source: ADR-0004
- **Exhaustion policy asymmetry**: `RunState` exhausted = non-blocking new run; `MasteryState` exhausted = blocking dialog — source: ADR-0004
- **IL2CPP preservation**: `Newtonsoft.Json` + `link.xml` required for every SO type and interface-referenced type shipped in a build; add entries for new types at authoring time, not at build time — source: ADR-0004 / ADR-0005 / ADR-0006

---

## Core Layer Rules

*Applies to: core gameplay loop, vehicle system, combat state, damage pipeline, card resolution*

### Required Patterns

- **Combat state as plain C# POCO — never stored on MonoBehaviours** — source: ADR-0002
- **Use `C# event` / `Action` delegates in combat systems** — source: ADR-0002
- **`PlayCard` throws typed exceptions on invalid state** — source: ADR-0002
- **`IEnemyBrain` interface for all enemy AI decision-making** — source: ADR-0002
- **`IVehicleView` (read-only) / `IVehicleMutator` (write) interface split.** Whitelisted callers per `docs/registry/architecture.yaml` and ADR-0005:
  - *Combat*: `ApplyDamage`, `AddPlating`, `AddArmor`, `ApplyStatus`, `Repair`, `TickStatuses`
  - *Status Effects*: `ApplyDamage` (DOT), `ApplyStatus`, `RemoveStatus`, `TickStatuses`
  - *Economy*: `InstallPart`, `RemovePart`, `Repair`
  - *Loot*: `InstallPart`
  - *Enemy AI*: `ApplyStatus` (via its own injected `IVehicleMutator` reference)

  source: ADR-0005 (Key Interfaces `IVehicleMutator` comment block)
- **`StatComposer` formula: `(base + Σ Add) × Π Multiply`; Override short-circuits all others; multiple Override entries for the same stat = error** — source: ADR-0005
- **`IPartCatalog.GetParts` is stateless; seeded selection stays in callers (Loot / Map)** — source: ADR-0005
- **`Vehicle.IsDead` is a backing field with private setter** — written exactly once per combat at Step 4(f) of the damage pipeline; never a derived getter — source: ADR-0007 (Decision 11 invariant #3)
- **`ApplyDamage` is reentrant; every invocation (including recursive Armor redirects) captures its own `wasCritical = vehicle.CriticalState` snapshot at Step 0**, before any mutation and before any further recursion — source: ADR-0007 (Decision 15)
- **`OnCriticalStateChanged` fires at-most-once per top-level `ApplyDamage` entrypoint**, suppressed across recursive Armor redirect invocations via the shared `DamageContext.CriticalEventFiredThisCall` flag — source: ADR-0007 (Decision 15)
- **V&P GDD F-VP2 "Canonical Event Order Table" is the single source of truth for the `ApplyDamage` and `Repair` event sequences** — this ADR mirrors the invariant list; the GDD owns the row-by-row ordering — source: ADR-0007 (Decision 13)
- **CI check must compare ADR-0007 Decision 11 invariants against V&P F-VP2 "Ordering contract (locked)" block** and fail if they diverge (see `tools/ci/check-adr0007-vp-lockstep.sh`) — source: ADR-0007 (Decision 13)
- **Enemy AI `WeakestInstance` ties broken by lexicographic `SlotId`** (designer-authored, stable, deterministic — no RNG needed for the tie-break). `RandomInstance` targeting uses a caller-owned `System.Random` from `RunSeed ^ stepIndex` per ADR-0003 — source: ADR-0007 (Decision 8) / ADR-0003
- **`Repair()` emits `OnSlotHpChanged` on every Hp delta** (positive or negative). Emits `OnSlotDamageStateChanged` when the repair crosses a `DamageState` boundary — source: ADR-0007 (Decision 12)

### Forbidden Approaches

- **Never `UnityEvent` in combat systems** — slow, swallows exceptions, makes combat state debugging unreliable — source: ADR-0002 / tech-prefs
- **Never store combat state on MonoBehaviours** — combat state belongs in POCO; view layer subscribes to events — source: ADR-0002 / tech-prefs
- **Never `Dictionary<SlotType, SlotState>`** — replaced by `IReadOnlyList<SlotInstance>` with `_bySlotId` (O(1)) and `_byKind` (cached list) lookup dictionaries — source: ADR-0007
- **Never `SlotType` enum** — replaced by `SlotKind { Weapon, Engine, Mobility, Hull, Armor }` — source: ADR-0007 (Decision 1)
- **Never `SlotKind.Frame`** — the structural hull value is `SlotKind.Hull`; `Frame` was renamed to free the word for layout authoring — source: ADR-0007 (Decision 1)
- **Never `vehicle.MaxArmor`, `vehicle.CurrentArmor`, `vehicle.AddArmor`, `OnMaxArmorChanged`, or `OnCurrentArmorChanged` as vehicle-level surfaces** — Armor is per-slot HP only; for UI projection iterate `Slots.Where(s => s.Kind == Armor).Sum(s => s.Hp)` — source: ADR-0007 (Decisions 4 + 11 invariant #6)
- **Never `Slots[Frame].DamageState == Offline` as the death check** — replaced by `StructuralHp == 0` (sum over structural slots) — source: ADR-0007 (Decision 4)
- **Never mutate a card SO asset to record a `PositionRequirement` override** — override is stamped on the runtime `CardData` instance at card-grant time; SO assets are read-only templates — source: ADR-0007 (Decision 7)
- **Never call `Repair()` after `IsDead` has been written** — death is a once-per-combat terminal state; post-death repair is undefined behavior — source: ADR-0007 (Decision 12)

### Performance Guardrails

- **Slot lookup hot-path**: `GetSlot(slotId)` O(1) via `_bySlotId` dictionary; `GetSlotsByKind` returns a cached read-only list — no allocation per call — source: ADR-0007 (Decision 4)
- **Armor exposure redirect rounding**: `redirected_amount = floor(amount × ExposureMultiplier)`; apply `Math.Min(..., int.MaxValue)` before int cast — source: ADR-0007 (Decision 6)
- **Max slot count: ≤ 10** (Dredge is the largest authored layout); `GetSlotsByKind` O(N) over bounded N is acceptable at turn-scope call frequency — source: ADR-0007 (Decision 9)

---

## Feature Layer Rules

*Applies to: card system authoring pipeline, reward draw, pity counter*

### Required Patterns

- **`ICardData.SourceSlotId` is nullable** — null for starter and reward cards; runtime-stamped for part-derived granted cards — source: ADR-0006 (amended by ADR-0007)
- **`RewardDrawAlgorithm` is deterministic** — follows ADR-0003 caller-owned `System.Random` pattern — source: ADR-0006
- **`TokenResolver` is stateless** — resolves `{token}` substitutions at display time; never pre-bake into SO data — source: ADR-0006
- **`CardCopyCounts` is derived but persisted** — auto-corrected on load if drift is detected — source: ADR-0006
- **Pity counter authority: Card System reads only; Loot & Reward subsystems write** — source: ADR-0006
- **`CardSystemDTO` constants: `SystemId = "card-system"`, `SchemaVersion = 1`** — source: ADR-0006
- **`PositionRequirement` derived at card-grant time** via `DerivePositionRequirement(part, cardTemplate)`; stamped on the runtime `CardData` instance in-deck — the SO template is never mutated — source: ADR-0007 (Decisions 5 + 7)

### Forbidden Approaches

- **Never pre-bake `{token}` substitutions into card SO assets** — token values are runtime state — source: ADR-0006
- **Never mutate card SO assets at runtime** — SOs are read-only templates; instance state lives on the runtime `CardData` record — source: ADR-0006
- **Never write pity counters from the Card System** — write authority belongs to Loot & Reward subsystems — source: ADR-0006

### Performance Guardrails

- **`link.xml` must preserve**: `CardDefinitionSO`, all 8 `CardEffectSO` subclasses, all 3 `EffectConditionSO` subclasses, `EffectConditionProjection` — source: ADR-0006

---

## Presentation Layer Rules

*Applies to: rendering, audio, UI, VFX, shaders, vehicle visual system*

### Required Patterns

- **`MaterialPropertyBlock` for all per-renderer damage overlay property writes** — `[PerRendererData]` attribute required on all custom shader properties (`_DamageAmount`, `_DamageOverlay`) — source: ADR-0001
- **Damage overlay textures must NOT be atlas-packed** — `MaterialPropertyBlock.SetTexture()` uses absolute UVs; atlas packing silently corrupts overlay rendering — source: ADR-0001
- **Addressables load pattern**: `LoadAssetsAsync` inside `try { ... } catch { ... } finally { if (handle.IsValid()) Addressables.Release(handle); }` — source: ADR-0001 (Unity 6.2+ behaviour change)
- **`OnSlotDamageStateChanged` event signature: 3 args — `(string slotId, DamageState from, DamageState to)`** — source: ADR-0001 as amended by ADR-0007 (ADR-0001 originally declared `Action<SlotType, DamageState, DamageState>`; ADR-0007 replaced `SlotType` with `string slotId`)

### Forbidden Approaches

- **Never write material properties directly on a shared material** — use `MaterialPropertyBlock` only; direct writes affect all renderers sharing the material — source: ADR-0001
- **Never atlas-pack overlay textures** (`spr_dmg_overlay_degraded.png`, `spr_dmg_overlay_offline.png`) — set Atlas Packing = Disabled in the `vfx-combat` Addressables group — source: ADR-0001
- **Never `Resources.Load` for chassis art** — use Addressables groups (`chassis-[name]`, `vfx-combat`) — source: ADR-0001 / deprecated-apis

### Performance Guardrails

- **MPB writes at most once per slot per frame** — dirty-flag pattern: set dirty on `OnSlotDamageStateChanged`, flush in `LateUpdate`; never write MPB properties directly in the event handler — source: ADR-0001
- **Vehicle slot renderers draw call budget**: 10 slot renderers (5 slots × 2 vehicles) each issue one non-batched draw call = 10 draws = 5% of the 200-call budget — source: ADR-0001

---

## Global Rules (All Layers)

### Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Classes | PascalCase | `CombatManager`, `ChassisDefinition` |
| Public fields / properties | PascalCase | `MoveSpeed`, `CurrentHullHp` |
| Private fields | `_camelCase` | `_currentEnergy`, `_installedParts` |
| Methods | PascalCase | `TakeDamage()`, `ResolveCardEffect()` |
| Events / delegates | `C# Action` or `event` keyword | `OnSlotHpChanged` |
| Files | PascalCase matching class name | `CombatManager.cs` |
| Scenes / Prefabs | PascalCase matching root node | `CombatScene.unity` |
| True constants | UPPER_SNAKE_CASE | `MAX_SLOT_COUNT` |
| Readonly fields | PascalCase | `DefaultExposureMultiplier` |

Source: `technical-preferences.md`

### Performance Budgets

| Target | Value |
|--------|-------|
| Framerate | 60fps |
| Frame budget | 16.6ms |
| Draw calls | 200 max |
| Memory ceiling | 2GB |

Source: `technical-preferences.md`

### Approved Libraries / Addons

- **Unity Addressables** — runtime asset reference and lazy-load for chassis art bundles (ADR-0001 visual pipeline, ADR-0007 `FrameLayoutSO` catalog); implementation owned by `unity-addressables-specialist`. Full architecture rationale in ADR-0008 (Proposed — pending Acceptance).

### Forbidden APIs (Unity 6.3 LTS)

These APIs are deprecated or behaviour-breaking in Unity 6.3 LTS and must not be used:

| Deprecated API | Replacement | Source |
|----------------|-------------|--------|
| `Input` class (legacy) | `UnityEngine.InputSystem` | `engine-reference/unity/deprecated-apis.md` |
| `Canvas` / UGUI `Text` | `UIDocument` (UI Toolkit) + `TextMeshPro` / `Label` | `engine-reference/unity/deprecated-apis.md` |
| `ComponentSystem` / `GameObjectEntity` | `ISystem` (DOTS / Entities 1.3+) | `engine-reference/unity/deprecated-apis.md` |
| `CommandBuffer.DrawMesh` | RenderGraph API (`RecordRenderGraph`) | `engine-reference/unity/VERSION.md` flag #1 |
| `Physics2D.RaycastAll` | `RaycastNonAlloc` | `engine-reference/unity/deprecated-apis.md` |
| `Resources.Load` | `Addressables` | `engine-reference/unity/deprecated-apis.md` |
| `WWW` | `UnityWebRequest` | `engine-reference/unity/deprecated-apis.md` |
| `Application.LoadLevel` | `SceneManager.LoadScene` | `engine-reference/unity/deprecated-apis.md` |

To extend this list when Unity 6.4 LTS lands: update `docs/engine-reference/unity/deprecated-apis.md` and regenerate this manifest.

### Cross-Cutting Constraints

- **All tunable gameplay values in ScriptableObjects or data files** — no magic numbers in gameplay code — source: `technical-preferences.md`
- **`[SerializeField]` is fields-only in Unity 6.3** — never apply to properties or methods; use `[field: SerializeField]` for auto-implemented properties in SOs — source: `engine-reference/unity/VERSION.md` flag #2 / ADR-0005
- **Casting `IVehicleView` to `IVehicleMutator` is forbidden** — use explicit dependency injection of `IVehicleMutator` where mutation is allowed — source: ADR-0005

---

## ADR Amendment Map

This table records where later ADRs amend earlier ones. When a rule in a bare ADR seems to contradict a rule in this manifest, the amendment takes precedence.

| Earlier ADR | Amended by | What Changed |
|-------------|------------|--------------|
| ADR-0001: `OnSlotDamageStateChanged` signature `Action<SlotType, DamageState, DamageState>` | ADR-0007 (Decisions 1 + 7) | `SlotType` first arg replaced by `string slotId` |
| ADR-0001: vehicle-level `MaxArmor = Σ part.ArmorContribution` | ADR-0007 (Decisions 4 + 6) | Vehicle-level Armor stat removed; Armor is per-slot HP only |
| ADR-0005: `Vehicle.Slots` as `IReadOnlyDictionary<SlotType, SlotState>` | ADR-0007 (Decision 4) | Replaced by `IReadOnlyList<SlotInstance>` + `_bySlotId` / `_byKind` lookup dictionaries |
| ADR-0005: `SlotType { Weapon, Engine, Mobility, Frame }` | ADR-0007 (Decision 1) | Replaced by `SlotKind { Weapon, Engine, Mobility, Hull, Armor }` |
| ADR-0006: `PositionRequirement.RequiresAhead` / `RequiresBehind` | ADR-0007 (Decision 7) | Renamed to `EnemyAhead` / `EnemyBehind`; override stamped on runtime `CardData`, not on SO |
| ADR-0006: `ICardData.SourceSlotId` not mentioned | ADR-0007 (Decision 16) | `CardData.SourceSlotId` nullable; null for starter/reward cards; stamped at grant time for part-derived cards |

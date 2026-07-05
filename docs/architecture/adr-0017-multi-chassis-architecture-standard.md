# ADR-0017: Multi-Chassis Architecture Standard

## Status

Accepted (2026-07-05)

## Date

2026-07-05

## Last Verified

2026-07-05

## Decision Makers

- User (bertanberkol@gmail.com) — design authority
- `technical-director` agent — architectural review (Round 3 CONCERNS + Round 4 GO WITH FINAL AMENDMENTS)

## Summary

Wasteland Run 1.0 ships 3 player chassis with a full parts progression axis
(weapons/engines/mobility/hull/bodywork all drop as loot, install/swap on
RunMap, sell at Chopshop). This ADR codifies the four architectural
commitments that unblock Phase 2.5: `VehicleDefinitionSO` owns its
`IFrameLayout` via a serialized `FrameLayoutSO` field (retires the
`SmallFrameLayout.Instance` hardcode), `VehicleBodyworkAnchors`
per-vehicle-prefab component follows the Slice 2.6 `VehicleHudAnchors`
precedent, `SlotKind.Bodywork` extends the enum for visual-only decorative
slots that participate in sum-of-parts armor, and the `PartDefinitionSO`
catalog is chassis-agnostic (one SO per part, same install on all 3
chassis with identical stats).

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core / Scripting / UI (data model + serialization + prefab authoring) |
| **Knowledge Risk** | LOW — pure C# data model + Unity `[SerializeField]` + ScriptableObject; no engine API surface post-cutoff |
| **References Consulted** | ADR-0007, ADR-0011, ADR-0012, ADR-0013, ADR-0015, ADR-0016; Slice 2.6 `VehicleHudAnchors` precedent; `FrameLayoutSO.cs` OnValidate rules |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | ScriptableObject reference retention across builds (IL2CPP); enum extension compilation across all `switch(SlotKind)` sites; `VehicleBodyworkAnchors` OnValidate stability under Prefab Mode edits |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0007 (Frame-driven variable-slot system — `IFrameLayout`, `SlotDefinition`, `FrameLayoutSO`); ADR-0011 (no bridges at done); ADR-0012 **amendment** (universal sum-of-parts, `InstallPart(armorContribution=0)` default-param removal, enemy narrowing) — must land in the same or prior commit as this ADR flipping to Accepted; ADR-0013 (sibling reward-source composition — precedent for `IPartRewardSource`); ADR-0015 (configuration narrowing — enemy Frame-armor stays authoring-side, not runtime branch); ADR-0016 (category coherence — component sibling to VehicleVisual, not nested) |
| **Enables** | Phase 2.5 parts axis (2.5A data-model, 2.5B chassis refactor, 2.5C part drops, 2.5D `IPartRewardSource`, 2.5E Chopshop sell + Inventory, 2.5F fullscreen UIs, 2.5G chassis 2+3 authoring, 2.5H Chopshop buy stock, 2.5I Codex + Mastery) |
| **Blocks** | All of Phase 2.5. No parts-axis work can begin until ADR-0017 + ADR-0012 amendment are Accepted. |
| **Ordering Note** | ADR-0017 flips from Proposed → Accepted only when ADR-0012 amendment is Accepted. Both amendments land as one atomic Phase 2.5A commit; ADR statuses flip together. |

## Context

### Problem Statement

Prior scope shipped 1.0 with one player chassis (Scout) and a hardcoded
frame layout. 2026-07-05 the user scoped IN a full parts progression axis
+ 3 player chassis + Codex + Chopshop verb split. Under this scope, four
structural assumptions break simultaneously:

1. `VehicleDefinitionSO.BuildVehicle` hardcodes `SmallFrameLayout.Instance`
   at line 42 — every vehicle spawns against the same frame regardless
   of the SO's identity. Cannot ship chassis 2 + 3 without a per-vehicle
   frame layout selector.
2. `PartDefinitionSO` has no field for weapon mount direction (front/back/
   universal), even though ADR-0007's xmldoc pre-declared `MountDirection`
   as a planned field. This is an ADR-0011 vestigial-doc drift.
3. No `SlotKind` for cosmetic/bodywork parts. Bodywork parts (door,
   bumper, canister, visor, light) contribute to sum-of-parts armor but
   have no slot category.
4. No component owns per-vehicle bodywork sprite mount points. `VehicleVisual`
   currently holds 4 fixed slot renderers; extending it to 9+ would violate
   composition coherence (`feedback_composition_smell_test`).

### Current State

- **`VehicleDefinitionSO`** (`Assets/Scripts/CombatView/Data/VehicleDefinitionSO.cs`):
  Owns `_displayName` + `List<PartSlot> _parts`. `BuildVehicle` line 42
  calls `new Vehicle(name, SmallFrameLayout.Instance)` — hardcoded.
- **`FrameLayoutSO`** (`Assets/Scripts/CombatView/Data/FrameLayoutSO.cs`):
  Full ScriptableObject implementing `IFrameLayout` — exists but is not
  referenced by `VehicleDefinitionSO`. Has robust `OnValidate` covering
  R_FL.1 rules 1–6 (unique SlotIds, structural coverage, Armor redirection
  well-formedness, ExposureMultiplier bounds, HP bounds).
- **`PartDefinitionSO`**: Has `PartId`, `SlotKind`, `MaxHp`,
  `ArmorContribution`, sprite ref. **No `MountDirection` field** despite
  ADR-0007 xmldoc pre-declaring it.
- **`SlotKind`** (`Assets/Scripts/Combat/SlotKind.cs`): 6 values —
  `Weapon`, `Engine`, `Mobility`, `Hull`, `Armor`, `Exposable`.
  `CategoryLabel` extension switch **throws `ArgumentOutOfRangeException`
  on unknown** — safety net, not silent swallow.
- **`SlotPosition`** (`Assets/Scripts/Combat/SlotPosition.cs`): Exists.
  `Any` / `Front` / `Back`. Applied to Scout weapon_0 = Front,
  weapon_1 = Back. Correct type for `PartDefinitionSO.MountDirection`.
- **`LanePosition`** (`Assets/Scripts/Combat/LanePosition.cs`): Exists.
  `Ahead` / `Behind`. Already threaded through combat as the whole-vehicle
  chase-rail state. Correct type for card `PlayableAt?`.
- **`VehicleHudAnchors`** (Slice 2.6 Phase 1c, 2026-06-30): Per-vehicle
  prefab component holding hand-placed `RectTransform` HUD anchors.
  Precedent for `VehicleBodyworkAnchors` (both are "per-vehicle-prefab
  hand-placed mount points, resolved by SlotId").
- **Enemy archetypes** (`Dredge`/`Hauler`/`Tiny`/`Small` in
  `Assets/Scripts/Combat/Archetypes/`): Use code-defined `IFrameLayout`
  singletons, not `FrameLayoutSO` assets. **Out of scope for this ADR** —
  enemies stay code-defined per ADR-0015 authoring convention until
  content demand justifies migration.

### Constraints

- ADR-0011: no bridges at done. `SmallFrameLayout.Instance` retirement must
  delete the singleton in the same commit as the field lands — no "kept
  for tests" compat overload.
- ADR-0012: sum-of-parts armor rule is universal across all slot kinds
  that contribute (including Bodywork). `armor_0.MaxHp = Σ non-Offline
  ArmorContribution`.
- ADR-0004: distributed schema registry — every new save DTO ships a
  `SystemId` + `SchemaVersion` constant.
- IL2CPP: ScriptableObject references must be preserved via `link.xml`
  (existing project practice per ADR-0004).
- Content authoring sanity: ~140 parts × 3 chassis = 420 SOs is
  unsustainable. Parts are chassis-agnostic (one SO per part).

### Requirements

- Every `VehicleDefinitionSO` builds its runtime `Vehicle` with a chassis-
  specific frame layout, not a global hardcode.
- Every `PartDefinitionSO` declares its mount direction (`SlotPosition.Any`
  / `Front` / `Back`) — enforced at authoring time.
- Bodywork parts install into `SlotKind.Bodywork` slots, contribute to
  sum-of-parts armor, are **not routed by DamagePipeline** (cosmetic-only
  in combat per user 2026-07-05), never take state transitions in combat.
- Per-vehicle Bodywork sprite mount points live on the vehicle prefab
  (not on `VehicleVisual`, not nested inside it) and resolve by SlotId.
- One `PartDefinitionSO` installs identically on all 3 chassis (no chassis-
  tagged variants).

## Decision

### Architecture

```
VehicleDefinitionSO (asset)               VehicleHudAnchors (existing)
  ├─ _displayName                              │
  ├─ _frameLayout : FrameLayoutSO ◄─┐      Vehicle Prefab (Scout / Chassis2 / Chassis3)
  └─ _parts : List<PartSlot>        │        ├─ VehicleVisual (sprite composition)
       │                             │        ├─ VehicleHudAnchors (RectTransforms)
       │  BuildVehicle(nameOverride) │        └─ VehicleBodyworkAnchors (SRs)  ◄─ NEW
       ▼                             │              │
   Vehicle POCO                      │              └─ resolves SlotId → SpriteRenderer
       ├─ IFrameLayout ─────────────┘                  (hand-placed, upsert-if-missing)
       ├─ SlotInstances[]  (Weapon/Engine/Mobility/Hull/Armor/Exposable/Bodywork)
       └─ armor_0 buffer = Σ ArmorContribution (excludes Offline)

PartDefinitionSO (asset, chassis-agnostic)
  ├─ PartId
  ├─ SlotKind
  ├─ MountDirection : SlotPosition        ◄─ NEW (reuses existing enum)
  ├─ MaxHp
  ├─ ArmorContribution
  ├─ Sprite ref  (world-space; VehicleBodyworkAnchors mounts it)
  └─ StatModifiers : List<StatModifier>    ◄─ NEW (Phase 2.5A greenfield)

Card SO
  └─ PlayableAt : LanePosition?           ◄─ NEW (reuses existing enum)
                                             null = universal
                                             Behind = requires player behind (front-mounted weapon reaches ahead)
                                             Ahead  = requires player ahead  (back-mounted weapon reaches behind)
```

### Key Interfaces

```csharp
// Assets/Scripts/CombatView/Data/VehicleDefinitionSO.cs — REFACTORED
public sealed class VehicleDefinitionSO : ScriptableObject
{
    [SerializeField] private string _displayName;
    [SerializeField] private FrameLayoutSO _frameLayout;   // NEW — replaces SmallFrameLayout.Instance
    [SerializeField] private List<PartSlot> _parts;

    public Vehicle BuildVehicle(string nameOverride = null)
    {
        // SmallFrameLayout.Instance hardcode DELETED in the same commit.
        Vehicle v = new Vehicle(_displayName, _frameLayout);
        // ... install parts, FillArmorPool ...
    }
}

// Assets/Scripts/Combat/PartDefinitionSO.cs — EXTENDED
public sealed class PartDefinitionSO : ScriptableObject
{
    // ... existing fields ...
    [SerializeField] private SlotPosition _mountDirection = SlotPosition.Any;  // NEW
    [SerializeField] private List<StatModifier> _statModifiers;                // NEW
    public SlotPosition MountDirection => _mountDirection;
    public IReadOnlyList<StatModifier> StatModifiers => _statModifiers;
}

// Assets/Scripts/Combat/SlotKind.cs — EXTENDED
public enum SlotKind
{
    Weapon, Engine, Mobility, Hull, Armor, Exposable,
    Bodywork,   // NEW — cosmetic-only in combat, contributes to sum-of-parts armor via ArmorContribution
}

// Assets/Scripts/CombatView/VehicleBodyworkAnchors.cs — NEW COMPONENT
[DisallowMultipleComponent]
public sealed class VehicleBodyworkAnchors : MonoBehaviour
{
    [Serializable]
    public struct Entry
    {
        public string SlotId;
        public SpriteRenderer Renderer;   // world-space, hand-placed in Prefab Mode
    }

    [SerializeField] private List<Entry> _entries;

    public SpriteRenderer ResolveRenderer(string slotId) { /* linear scan, one per part */ }

    #if UNITY_EDITOR
    /// <summary>Auto-author bootstrap. Upsert-if-missing, never overwrite existing entries.</summary>
    public void EditorUpsert(string slotId, SpriteRenderer renderer) { /* ... */ }
    private void OnValidate() { /* dedupe SlotId, null-check renderer, warn missing */ }
    #endif
}

// Assets/Scripts/Combat/StatModifier.cs — NEW POCO (Phase 2.5A)
[Serializable]
public struct StatModifier { public StatKind Kind; public float Value; }

public enum StatKind { /* Phase 2.5A initial vocabulary — TBD in slice brief */ }
```

### Implementation Guidelines

Every `Phase 2.5A` piece ships in **one atomic commit** — non-atomic split
re-opens the `InstallPart(armorContribution=0)` default-param semantic
trap between commits (per `feedback_gdd_verb_signature_not_load_bearing`).
The atomic commit contains:

1. `SlotKind.Bodywork` enum extension.
2. Exhaustive-switch audit fixes at every site listed below.
3. `FrameLayoutSO` field on `VehicleDefinitionSO` + `SmallFrameLayout.Instance`
   singleton deletion (tests migrate to `FrameLayoutSO` assets, no compat
   overload).
4. `MountDirection : SlotPosition` field on `PartDefinitionSO`.
5. `StatModifier` struct + `StatKind` enum (initial vocabulary from
   Phase 2.5A slice brief).
6. ADR-0012 amendment: universal sum-of-parts rule + `InstallPart`
   default-param removal + enemy narrowing statement (ADR-0015).

**Exhaustive-switch audit checklist for `SlotKind.Bodywork`** (per Round 4 A5):

| Site | File | Bodywork treatment |
|---|---|---|
| `CategoryLabel` extension | `SlotKind.cs` | Add `"Bodywork"` case. Safety net already throws on unknown — no silent swallow risk, but must extend for correctness. |
| Save DTO serialization | `Save/Dtos/SlotSnapshotDto.cs`, `VehicleStateDto.cs`, `SaveSchemaRegistry` | Bodywork slots serialize normally; state never transitions in combat, so snapshot is authored-time invariant. Verify round-trip. |
| Damage routing | `DamagePipeline.Apply` | Bodywork is **skipped** — not a damage target in combat per user 2026-07-05. Add explicit `case SlotKind.Bodywork: return;` (or equivalent gate) so damage cannot leak. |
| Sum-of-parts recompute | `Vehicle.RecomputeArmorPool` | Bodywork slots are **included** in the ArmorContribution sum (they contribute at build time and don't state-transition in combat, so the sum stays static). Filter is "non-Offline"; Bodywork stays non-Offline. |
| HUD widgets | `VehicleBarStack`, HUD subscribers | Bodywork slots do **not** render bars. Legit skip — must be intentional (explicit filter), not accidental default. |
| Prefab authoring | `CombatPrefabAuthor` + chassis authors | Author Bodywork sprite mounts via `VehicleBodyworkAnchors.EditorUpsert(slotId, renderer)`. Missing entries = missing visual, not runtime crash. |

**`VehicleBodyworkAnchors` discipline** (per Round 4 A7):

- **Component sibling to `VehicleVisual`**, not nested inside it. Sprite
  hierarchy for rendering order can nest independently; the *component*
  stays sibling per ADR-0016 category coherence.
- **`EditorUpsert` is upsert-if-missing, never overwrite.** Slice 2.6
  auto-author regression: re-authoring wiped designer hand-tunes. Do not
  repeat.
- Anchor type is **`SpriteRenderer`** (world-space), not `RectTransform`.
  Bodywork sprites render alongside chassis art in world-space, not the
  HUD canvas. Sidesteps the `feedback_nested_sizedelta_override_pin` trap.

**`SmallFrameLayout.Instance` retirement** (per Round 4 A6):

- Singleton deleted in the same commit as the SO field lands.
- Tests migrate to `FrameLayoutSO` asset references (or in-memory
  `FrameLayoutSO.CreateInstance<FrameLayoutSO>()` fakes per test).
- No "kept for tests" compat overload — per
  `feedback_demo_forward_over_infrastructure` (2026-06-01 user retraction
  of demo-forward), tests migrate to canonical APIs, not transitional
  bridges.

**Bodywork combat behavior** (per user 2026-07-05):

- Bodywork parts are **cosmetic-only in combat**. `DamagePipeline` never
  routes to Bodywork slots.
- Bodywork slots do not state-transition in combat (no Offline, no
  Degraded).
- `armor_0.MaxHp` is computed at build time from all `ArmorContribution`
  including Bodywork; because Bodywork never state-transitions in combat,
  Bodywork's contribution is effectively a static bonus.
- Bodywork parts install/swap on **RunMap between beacons** or at
  **Chopshop**. Never in combat.

## Alternatives Considered

### Alternative 1: Chassis-tagged Bodywork (per-chassis part variants)

- **Description**: Each Bodywork PartDefinitionSO carries a `ChassisId`
  and only installs on matching chassis. Same visual identity ships as 3
  separate SOs.
- **Pros**: Per-chassis art tuning is explicit; no chassis-agnostic sprite
  compromise.
- **Cons**: 140 parts × 3 chassis = 420 SOs. Content bloat, Codex complexity,
  save-schema PartId space explosion.
- **Estimated Effort**: 3× authoring cost.
- **Rejection Reason**: Chassis-agnostic parts (Alternative 0, the chosen
  path) accepts a compromise on per-chassis art tuning in exchange for
  sustainable authoring at 1.0 scope. TD Round 2 initially recommended
  chassis-tagged; user reversed 2026-07-05 based on authoring-cost math.

### Alternative 2: Scout-only at 1.0 (defer 3-chassis to post-1.0)

- **Description**: Ship 1.0 with only Scout chassis + parts axis. Retire
  the 3-chassis commitment.
- **Pros**: Cuts Phase 2.5G (chassis 2+3 authoring, 2–4 weeks). Simplifies
  Codex chassis carousel to a single view.
- **Cons**: Violates game-concept.md 3-chassis pillar. Removes core
  replayability driver (different chassis = different builds).
- **Rejection Reason**: User accepted the full 11.5–16 week Phase 2.5
  timeline including chassis 2+3 (2026-07-05). Non-negotiable scope.

### Alternative 3: Extend `VehicleVisual` with 5 new Bodywork SR fields

- **Description**: Add `_door`, `_bumper`, `_canister`, `_visor`, `_light`
  as fields on `VehicleVisual`. Reuse existing component.
- **Pros**: One fewer component. No new authoring surface.
- **Cons**: Violates `feedback_composition_smell_test` — `VehicleVisual`'s
  category is "core chassis composition" (Weapon/Engine/Mobility/Hull);
  Bodywork is a distinct category. Would leak into ADR-0016 category
  coherence violation. Also brittle for chassis 2 + 3 if Bodywork slot
  counts differ.
- **Rejection Reason**: Slice 2.6 already proved the sibling-component
  pattern works (`VehicleHudAnchors`). Mirror it.

## Consequences

### Positive

- Every `VehicleDefinitionSO` becomes chassis-independent; adding chassis
  4+ post-1.0 requires only a new SO + a new `FrameLayoutSO` + a new
  vehicle prefab. No code changes.
- Chassis-agnostic parts catalog stays at ~140 SOs regardless of chassis
  count.
- ADR-0007's vestigial `MountDirection` xmldoc drift closes.
- `SmallFrameLayout.Instance` singleton retirement removes the last
  known "one chassis" hardcode.
- `VehicleBodyworkAnchors` establishes a general "per-vehicle-prefab
  hand-placed mount point" pattern that future features (per-vehicle VFX
  emitters, per-vehicle audio sources) can reuse.

### Negative

- Phase 2.5A must be an atomic commit — non-trivial coordination for a
  6-piece change. Fallback: if the commit is too large to review, split
  is possible only by shipping ADR-0012 amendment first, letting it soak,
  then landing the rest — but the semantic trap window widens.
- `SlotKind.Bodywork` enum extension requires touching every exhaustive
  switch site. Audit checklist in this ADR mitigates but does not
  eliminate the discovery risk.
- Chassis-agnostic parts accept a per-chassis art tuning compromise —
  same Heavy MG sprite on all 3 chassis (possibly with per-chassis mount
  offset via `VehicleBodyworkAnchors`, but same asset).

### Neutral

- Enemies stay on code-defined `IFrameLayout` singletons for the
  foreseeable future (ADR-0015 authoring narrowing). Migration to
  `FrameLayoutSO` assets can happen later without breaking this ADR.
- Bodywork parts install/swap on RunMap between beacons — same interaction
  surface as any other part. No new UI verb.

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| SlotKind.Bodywork silently swallowed at an audit-missed switch site | LOW | HIGH (save corruption, invisible parts) | Audit checklist in this ADR + `CategoryLabel` throw-on-unknown safety net catches it at first use |
| Phase 2.5A atomic commit too large to review | MEDIUM | MEDIUM (review latency) | TD verdict + capture file pre-review reduce review scope; split escape hatch = ADR-0012 amendment first, rest second |
| Designer hand-tunes on `VehicleBodyworkAnchors` wiped by re-authoring | MEDIUM | MEDIUM (polish rework) | `EditorUpsert` upsert-if-missing discipline (Slice 2.6 lesson explicitly folded in) |
| `FrameLayoutSO` per-chassis asset reference lost across IL2CPP builds | LOW | HIGH (spawn null-ref) | Existing `link.xml` covers ScriptableObject preservation per ADR-0004; verify on first IL2CPP smoke |
| Chassis-agnostic sprite doesn't fit chassis 2 or 3 silhouette | MEDIUM | MEDIUM (visual polish) | Per-chassis mount offset via `VehicleBodyworkAnchors` compensates; if silhouette mismatch persists, reversible via Alternative 1 for the offending part (case-by-case, not global) |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU (BuildVehicle) | ~0.1ms | ~0.1ms | 1ms (SO field lookup is compile-time serialized) |
| Memory (per vehicle) | ~4KB | ~5KB | 20KB (5 Bodywork SpriteRenderers + Anchors component) |
| Load Time (per chassis) | n/a (Scout only) | +50ms per chassis (Addressables lazy-load per ADR-0008) | 200ms cold-load budget |

Performance is not a load-bearing concern for this ADR — the changes are
data-model shape, not hot-path.

## Migration Plan

1. **Phase 2.5A — atomic commit** (per Implementation Guidelines above).
   Six pieces ship as one commit. ADR-0017 flips Proposed → Accepted
   alongside ADR-0012 amendment in this commit.
2. **Phase 2.5B — Scout chassis refactor + `VehicleBodyworkAnchors`
   authoring.** Capture-before-destroy required for `VehicleVisual`
   composition change and for the 5 Bodywork SR fields' first-time author.
   Scout prefab gets a `VehicleBodyworkAnchors` component with the 5
   default Bodywork slots defined by 2.5A slice brief.
3. **Phase 2.5C–2.5E — parts drops + Chopshop sell + fullscreen UIs** —
   consume the ADR-0017 shape.
4. **Phase 2.5G — chassis 2 + 3 authoring.** Each new chassis gets:
   (a) new `FrameLayoutSO` asset, (b) new vehicle prefab with
   `VehicleVisual` + `VehicleHudAnchors` + `VehicleBodyworkAnchors`,
   (c) new `VehicleDefinitionSO` pointing at its `FrameLayoutSO`.
   Zero code changes required.
5. **Phase 2.5H–2.5I — Chopshop buy + Codex.** Codex chassis carousel
   loops through the 3 `VehicleDefinitionSO` assets; discovery-gated
   part display filters against `PartDiscoveryState` DTO.

**Rollback plan**: If chassis 2 or 3 prove infeasible mid-2.5G, ADR-0017
still holds — the `FrameLayoutSO` field just points at `SmallFrameLayout`
for every chassis SO. No architecture rollback; only scope rollback via
Alternative 2. `SmallFrameLayout` code-defined singleton stays deleted;
tests use `FrameLayoutSO` asset fakes as designed.

## Validation Criteria

- [ ] `SmallFrameLayout.Instance` symbol does not appear anywhere in the
      codebase after Phase 2.5A (CI grep gate).
- [ ] Every `switch(SlotKind)` site has an explicit `case SlotKind.Bodywork`
      arm (CI grep gate + compile-time exhaustiveness where language
      supports).
- [ ] `PartDefinitionSO.MountDirection` field exists as `SlotPosition`
      (grep confirms no new enum introduced).
- [ ] Card `PlayableAt` types as `LanePosition?` (grep confirms no new
      enum introduced).
- [ ] `VehicleBodyworkAnchors` component exists on all 3 chassis prefabs
      by end of Phase 2.5G.
- [ ] Damage applied to Bodywork slot in unit test = 0 HP change,
      0 armor drain, 0 events raised.
- [ ] Round-trip save/load of a vehicle with Bodywork parts installed
      preserves `PartId`, `MaxHp`, `ArmorContribution` per slot.
- [ ] IL2CPP smoke build spawns all 3 chassis with their `FrameLayoutSO`
      references intact.

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/game-concept.md` | Vehicles | 3 player chassis at 1.0 | `FrameLayoutSO` per-chassis field decouples `VehicleDefinitionSO` from Scout hardcode; Phase 2.5G authors chassis 2 + 3 with zero code changes |
| `design/gdd/vehicle-frames.md` | Frame layouts | `small_frame`, `tiny_frame`, `hauler_frame`, `dredge_frame` topology | Each frame ships as a `FrameLayoutSO` asset; player chassis migrate off code-defined singletons |
| Parts progression axis (pending GDD — captured in `project_parts_axis_in_1_0.md`) | Parts economy | Chassis-agnostic parts, install/swap on RunMap, sell at Chopshop | ADR-0017 makes this the only shape: one PartDefinitionSO per part, `MountDirection` typed via existing `SlotPosition`, sum-of-parts armor universal via ADR-0012 amendment |
| Codex (pending GDD) | Meta-progression | Discovery-gated Codex with chassis carousel across 3 vehicles | Chassis-agnostic parts + per-chassis `FrameLayoutSO` + `PartDiscoveryStateDto` (Phase 2.5C) satisfy the display shape |

## Related

- **Depends on**: ADR-0007 (frame-driven variable-slot system), ADR-0011
  (no bridges), ADR-0012 (sum-of-parts armor — amendment required in
  same commit), ADR-0013 (sibling reward-source composition — precedent
  for `IPartRewardSource`), ADR-0015 (configuration narrowing — enemy
  frame-armor stays authoring-side), ADR-0016 (category coherence —
  component sibling to `VehicleVisual`).
- **Precedent for `VehicleBodyworkAnchors`**: Slice 2.6 Phase 1c
  `VehicleHudAnchors` (2026-06-30, see `project_hud_anchors_slice_26.md`).
- **Closes**: ADR-0007 xmldoc pre-declared `MountDirection` field —
  ADR-0011 vestigial-doc drift.
- **Code files touched (Phase 2.5A)**:
  `Assets/Scripts/CombatView/Data/VehicleDefinitionSO.cs`,
  `Assets/Scripts/Combat/PartDefinitionSO.cs`,
  `Assets/Scripts/Combat/SlotKind.cs`,
  `Assets/Scripts/Combat/Archetypes/SmallFrameLayout.cs` (DELETE),
  `Assets/Scripts/CombatView/VehicleBodyworkAnchors.cs` (NEW),
  `Assets/Scripts/Combat/StatModifier.cs` (NEW),
  `Assets/Scripts/Combat/StatKind.cs` (NEW),
  `Assets/Scripts/Combat/DamagePipeline.cs` (Bodywork skip),
  `Assets/Scripts/Combat/Vehicle.cs` (RecomputeArmorPool filter),
  `Assets/Scripts/CombatView/VehicleBarStack.cs` (Bodywork skip),
  `Assets/Scripts/Save/Dtos/SlotSnapshotDto.cs` + `VehicleStateDto.cs`
  (Bodywork round-trip).

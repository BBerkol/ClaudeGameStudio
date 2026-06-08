# ADR-0001: Visual Vehicle Part System

## Status
Accepted (Amended 2026-04-25 by ADR-0005 — see "ADR-0005 Amendments" subsection below)

## Date
2026-04-19

## Acceptance Date
2026-04-21

## ADR-0005 Amendments

ADR-0005 (Vehicle POCO Sub-Model + Part Catalog) amends four interface details that this ADR locked pre-retrofit. The V&P GDD (post-retrofit 2026-04-23) is authoritative; this ADR's data-shape declarations have been updated inline below to reflect the amended contract. The visual-layer decisions in this ADR (URP Sprite Lit Shader Graph, MaterialPropertyBlock pattern, Addressables groups, sprite budget) remain in force unchanged — only the interface surface shifted.

| Pre-retrofit (this ADR, original) | Amended (V&P GDD + ADR-0005) |
|---|---|
| 5 visible slots: Weapon, Armor, Engine, Mobility, Frame | 4 data slots: Weapon, Engine, Mobility, Frame. Armor is a vehicle-level derived stat (`MaxArmor = Σ part.ArmorContribution`) rendered as a chassis-level MPB overlay, not a slot sprite. |
| `DamageState { Functional, Degraded, Offline }` (3 values) | `DamageState { Empty, Functional, Degraded, Offline }` (4 values; `Empty` means `InstalledPart == null`) |
| `event Action<SlotType, DamageState>` (2-arg) | `event Action<SlotType, DamageState, DamageState>` (3-arg: slot, from, to) |
| `void ApplyDamage(SlotType slot, int amount)` (2-arg) | `void ApplyDamage(SlotType slot, int amount, DamageSource source)` (3-arg; `DamageSource ∈ {Card, Status, Environment}` per V&P GDD F-VP2 DOT bypass) |

The Decision and Key Interfaces sections below reflect the amended contract. See ADR-0005 § "ADR-0001 Amendments" for the full rationale.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Rendering / Sprites |
| **Knowledge Risk** | MEDIUM — post-LLM-cutoff (May 2025); verified against `docs/engine-reference/unity/` |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/breaking-changes.md` |
| **Post-Cutoff APIs Used** | Addressables `LoadAssetAsync` with exception handling (behavior changed Unity 6.2+) |
| **Verification Required** | Confirm `[PerRendererData]` MPB override works in URP 2D Sprite Lit Shader Graph; verify draw call count in Frame Debugger; confirm Addressables exception handling on load failure in Unity 6.3 |

### Unity 6.3 Change Applicability for this ADR

| Change | Applies? | Verified |
|--------|----------|---------|
| URP Render Graph (Compatibility Mode removed) | **No** — this ADR uses material shaders only, not custom `ScriptableRenderPass`. Render Graph change does not affect `SpriteRenderer` + Material Property Block. | Yes |
| `[SerializeField]` fields-only | **Yes** — VehicleView implementation must use `[field: SerializeField]` for any serialized auto-properties | Yes |
| `FindObjectsByType` replacement | **No** — not used in this system | Yes |
| Addressables exception on failure (6.2+) | **Yes** — all `LoadAssetAsync` calls require try/catch + `handle.IsValid()` guard | Yes |
| SRP Batcher / MPB interaction in URP 2D | **Yes** — MPB disables SRP Batcher for affected renderers; accepted at 10-renderer scale | Yes |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None — first ADR in the project |
| **Enables** | Vehicle & Part System GDD finalization; vehicle art production pipeline; `chassis-[name]` Addressables group definitions |
| **Blocks** | Vehicle & Part System GDD (cannot be finalized until this ADR is Accepted); all vehicle sprite art production |
| **Ordering Note** | Must be Accepted before Month 3. Art pipeline and GDD authoring for Vehicle & Part System are both gated on this decision. |

## Context

### Problem Statement

The vehicle in Wasteland Run is the player's primary emotional anchor (Pillar 1: Vehicle as Character). Part damage must be visually legible and feel consequential across 5 slot types per chassis. At the same time, the art pipeline must be sustainable for a solo developer — the naive full-sprite approach (one sprite per part per damage state) requires 120–180 sprites for EA scope alone and does not scale to a third chassis. A damage representation strategy is needed that balances visual expressiveness against solo dev art production capacity, while respecting the project's POCO combat model (combat state must not live on MonoBehaviours).

### Constraints
- Solo developer: art production budget targets ~40–60 visible part asset slots for EA
- Unity 6.3 LTS, URP 2D — shader work uses Shader Graph (not handwritten HLSL); no HDRP
- 200 draw call budget at 60fps (from `technical-preferences.md`) — vehicle rendering must not dominate
- RUST ICON visual identity: damage overlays must be pixel-art-consistent, point filter mode throughout
- Master canvas: 256×128px shared chassis canvas (Scout ~128×48px, Assault ~192×80px within canvas — per art bible Section 5.5)
- Part inspect and combat HUD must show damage state without text labels (color-coded per art bible Section 4)
- Vehicle state is owned by a plain C# POCO — no MonoBehaviour allowed to hold canonical state

### Requirements
- 5 visible chassis slots must each show distinct states: Functional, Degraded, Offline
- Damage state readable from combat mid-field view and Part Inspect UI
- Asset count ≤ ~60 base part sprites for Scout + Assault EA scope
- Runtime loading async (Addressables) — no blocking loads during combat
- Damage state transitions trigger visual updates at state-change time only — not per-frame polling
- Vehicle POCO remains plain C# — VehicleView MonoBehaviour reads via interface only

## Decision

**4 visible slots per chassis + chassis-level Armor overlay + URP Sprite Lit Shader Graph with Material Property Block damage overlays.** *(Amended 2026-04-25 by ADR-0005: was "5 visible slots" pre-retrofit; Armor is no longer a slot.)*

Each chassis exposes exactly **4 visible slots**: Weapon, Engine, Mobility, Frame. Vehicle-level Armor (`MaxArmor = Σ part.ArmorContribution`) renders as a chassis-level MPB overlay layered above the chassis silhouette — *not* a per-slot sprite renderer. Each of the 4 visible slots has:

1. **Base sprite** — artist-authored per chassis, per part variant. Point filter mode. Loaded async via Addressables from the `chassis-[name]` group.
2. **Shared damage overlay textures** — two standalone textures: `spr_dmg_overlay_degraded.png` and `spr_dmg_overlay_offline.png`. Shared across all chassis and all parts. Loaded once at combat start from the `vfx-combat` group; held at combat scope and released on combat scene teardown. **Must not be atlas-packed** — MaterialPropertyBlock texture assignment uses absolute UVs, not sprite atlas UVs.
3. **URP Sprite Lit Damage shader** — Shader Graph variant of the URP 2D Sprite Lit base. Adds two custom properties: `_DamageAmount` (float, `[PerRendererData]`) and `_DamageOverlay` (Texture2D, `[PerRendererData]`). The `[PerRendererData]` attribute is required for MaterialPropertyBlock to override these values per-renderer. Damage overlay composites atop the base sprite via alpha-blended multiply in the fragment graph.
4. **MaterialPropertyBlock** — one per slot renderer. `_DamageAmount` maps to DamageState: Functional = 0.0, Degraded = 0.5, Offline = 1.0. Updated on state-change events only via dirty flag — at most one MPB write per slot per frame regardless of event count within the frame.

### Architecture Diagram

```
Vehicle POCO (plain C#, no MonoBehaviour)
  ├── SlotState[4] { Weapon, Engine, Mobility, Frame }      // ADR-0005: 4 data slots
  │     ├── IPartData (interface; concrete = PartDefinitionSO authored asset)
  │     ├── DamageState  { Empty | Functional | Degraded | Offline }   // ADR-0005: +Empty
  │     └── PlatingStacks (int)
  └── MaxArmor / CurrentArmor (vehicle-level derived stat — ADR-0005)

VehicleView (MonoBehaviour — READ ONLY via IVehicleView)
  ├── PartSlotRenderer[4]                                   // 4 slot renderers
  │     ├── SpriteRenderer        (base sprite, loaded async from chassis-[name] group)
  │     ├── MaterialPropertyBlock
  │     │     ├── _DamageAmount   (float — set on OnSlotDamageStateChanged event)
  │     │     └── _DamageOverlay  (Texture2D — set once at combat start from vfx-combat group)
  │     └── dirty flag            (batches MPB writes; flushes in LateUpdate, not on event)
  └── ChassisArmorOverlay         (chassis-level Armor MPB; intensity = CurrentArmor / MaxArmor — ADR-0005)

Addressables Groups:
  chassis-scout:   spr_chassis_scout_[slot]_[variant].png  (≤30 base sprites)
  chassis-assault: spr_chassis_assault_[slot]_[variant].png (≤30 base sprites)
  vfx-combat:      spr_dmg_overlay_degraded.png             (standalone, no atlas)
                   spr_dmg_overlay_offline.png              (standalone, no atlas)
```

### Key Interfaces

```csharp
// AMENDED 2026-04-25 by ADR-0005 — see ADR-0005 § "Key Interfaces" for the full surface.
// The shapes below show only what THIS ADR locks (visual layer reads + write-whitelist contract).
public enum SlotType     { Weapon, Engine, Mobility, Frame }                  // ADR-0005: 4 slots (was 5; Armor moved to vehicle-level)
public enum DamageState  { Empty, Functional, Degraded, Offline }             // ADR-0005: +Empty (was 3 values)
public enum DamageSource { Card, Status, Environment }                        // ADR-0005: required by F-VP2 DOT bypass

// Read-only access — all systems except mutators use this
public interface IVehicleView {
    ChassisType Chassis { get; }
    int MaxArmor { get; }                                                     // ADR-0005: vehicle-level derived stat
    int CurrentArmor { get; }                                                 // ADR-0005
    IPartData GetInstalledPart(SlotType slot);                                // ADR-0005: interface-typed (concrete = PartDefinitionSO)
    DamageState GetDamageState(SlotType slot);
    int GetPlatingStacks(SlotType slot);
    event Action<SlotType, DamageState, DamageState> OnSlotDamageStateChanged; // ADR-0005: 3-arg (slot, from, to)
    // Additional events (OnPartInstalled / OnPartRemoved / OnMaxArmorChanged / OnCurrentArmorChanged / OnStatusApplied / OnStatusExpired) declared in ADR-0005.
}

// Write access — exclusive to the consumer whitelist registered in `interfaces.vehicle_state_access`
// (Combat, Status Effects, Economy, Loot, Enemy AI). See ADR-0005 § "Key Interfaces" for full mutator surface.
public interface IVehicleMutator {
    void ApplyDamage(SlotType slot, int amount, DamageSource source);          // ADR-0005: +DamageSource (DOT bypasses Armor on Frame)
    void InstallPart(SlotType slot, IPartData part);                           // ADR-0005: interface-typed
    void RemovePart(SlotType slot);
    void Repair(SlotType slot, int platingRestored);
    // AddArmor / AddPlating / ApplyStatus / RemoveStatus / TickStatuses declared in ADR-0005.
}
```

### Addressables Loading Pattern

```csharp
// Base sprite loading (per slot, on part install or combat start)
AsyncOperationHandle<Sprite> handle = default;
try {
    handle = Addressables.LoadAssetAsync<Sprite>(partDef.SpriteKey);
    _slotSprite = await handle.Task;
    _spriteRenderer.sprite = _slotSprite;
} catch (Exception e) {
    Debug.LogError($"[PartSlotRenderer] Failed to load sprite '{partDef.SpriteKey}': {e.Message}");
    _spriteRenderer.sprite = _fallbackSprite; // "missing part" placeholder silhouette
} finally {
    // handle.IsValid() guard prevents double-exception if load threw before handle was assigned
    if (handle.IsValid())
        Addressables.Release(handle);
}

// Overlay textures (loaded once at combat start, held at combat scope)
// Released by CombatSceneController.OnCombatTeardown() — NOT by individual slot renderers
```

## Alternatives Considered

### Alternative A: Full Sprite Set Per Damage State
- **Description**: Each installed part has authored sprites for Functional, Degraded, and Offline states. SpriteRenderer.sprite is swapped on state change. 40–60 visible parts × 3 states = 120–180 sprites.
- **Pros**: No custom shader; no MaterialPropertyBlock; simple SpriteRenderer.sprite swap; artist has full control over each damage state appearance; SRP Batcher unaffected
- **Cons**: 3× art production volume vs. Option B — unsustainable for a solo developer at EA scope. Heavy Truck post-launch would add another 40+ sprite triples. Does not scale.
- **Rejection Reason**: Art budget infeasible. 120–180 sprites at EA scope with a solo developer.

### Alternative C: 5 Key Slots + Separate SpriteRenderer Layers
- **Description**: Same 5-slot structure as Option B, but damage overlays are separate SpriteRenderer GameObjects layered on top via Order in Layer. No custom shader; damage overlay sprites drawn as additional renderers.
- **Pros**: No custom shader; artist-friendly overlay sprites; standard Unity 2D pipeline
- **Cons**: Each slot now has 2 SpriteRenderers (base + overlay) = 10 renderers per vehicle × 2 vehicles = 20 sprite renderers for vehicles alone, none SRP-batched due to different materials. Draw call pressure increases with card VFX active. Additional GameObjects increase scene complexity.
- **Rejection Reason**: Draw call overhead avoidable with MPB approach. MPB property updates are cheaper than additional GameObjects per slot.

### Alternative D: 2D Animation SpriteLibrary + SpriteResolver Swap
- **Description**: Use Unity's 2D Animation package. Define damage states as SpriteLibrary categories (e.g., `Category: Weapon, Label: Functional | Degraded | Offline`). SpriteResolver swaps the sprite at runtime.
- **Pros**: No custom shader; artist-friendly pipeline; no MaterialPropertyBlock complexity
- **Cons**: Damage state still requires one authored sprite per state per part — same asset count problem as Option A (120–180 sprites for EA). Adds the 2D Animation package dependency and a more complex asset pipeline. Provides no content savings over Option A, with more toolchain complexity.
- **Rejection Reason**: Does not solve the art production scope problem. Option B's shared overlay approach is the only way to decouple damage presentation from part-specific art.

## Consequences

### Positive
- 40–60 base sprites for EA (Scout + Assault) — sustainable solo art budget
- Shared overlay textures reused across all chassis and all parts — no per-part damage art
- Single SpriteRenderer per visible slot — 10 non-batched draw calls at most, well within 200 draw call budget
- Clean POCO/View separation — IVehicleView and IVehicleMutator enforce the access boundary established in systems index (Scoping Note C3)
- Scalable to Heavy Truck post-launch: same shader, same overlay set, same Addressables group pattern — add new `chassis-heavytruck` group only
- Reversible at low cost: IVehicleView insulates all gameplay logic from the rendering implementation. Replacing MPB + custom shader with any other rendering approach (e.g., Option A) requires changes to VehicleView and the shader only — no changes to Vehicle POCO, combat systems, economy, or loot. Blast radius is view layer + shader + Addressables group definitions.

### Negative
- **One custom Shader Graph asset required** — URP Sprite Lit Damage variant. Shader Graph assets must be maintained through URP version changes. However: this shader is a pure material shader (no custom render passes, no Render Graph involvement) — the URP Compatibility Mode removal in 6.3 does not affect it. Shader maintenance risk is LOW.
- **MaterialPropertyBlock disables SRP Batcher** for the 10 vehicle slot renderers. These renderers each issue an individual draw call rather than being batched. At 200 draw call budget, 10 draws is 5% — accepted.
- **Overlay textures must be standalone** (not atlas-packed) — they are referenced via MaterialPropertyBlock `SetTexture()`, which uses absolute UVs. If atlas-packed, UV remapping does not apply and the overlay will render incorrectly. Addressables group configuration for `vfx-combat` must disable atlas packing for overlay textures.
- **Addressables load/unload strategy is asymmetric**: base sprite handles are loaded and released per slot (on part install/uninstall). Overlay texture handles are loaded once at combat start and held for the full combat duration by `CombatSceneController`. This asymmetry must be documented and enforced — if an individual slot renderer releases an overlay handle, other renderers will lose their texture binding.
- **Test #1 (visual distinction) cannot be automated** — visual quality of Functional/Degraded/Offline states is a screenshot review + lead sign-off, per `coding-standards.md`. It is not a unit test.

### Risks

| Risk | Severity | Mitigation |
|------|----------|-----------|
| `[PerRendererData]` omitted from shader property block | HIGH | Prototype shader in Month 1; verify MPB override works before art production begins |
| Overlay textures atlas-packed in Addressables | HIGH | Set Atlas Packing = Disabled in `vfx-combat` Addressables group settings; document in art pipeline |
| Overlay handle released by slot renderer (should be combat-scoped) | MEDIUM | `CombatSceneController` owns overlay handle lifetime; slot renderers receive texture reference, do not hold the handle |
| URP 2D Renderer MPB batching edge cases | LOW | Verify draw call count in Frame Debugger after first implementation; document actual draw count |
| Addressables failure silently passes on null (pre-6.2 habit) | LOW | Enforce try/catch + `handle.IsValid()` guard in `finally` via code review; add to `Forbidden Patterns` in technical-preferences.md |
| Shader Graph custom properties not exposed to MPB | MEDIUM | Confirmed `[PerRendererData]` attribute is required; prototype before committing pipeline |

## GDD Requirements Addressed

> **Note**: The Vehicle & Part System GDD does not yet exist — this ADR blocks its finalization. The table below will be updated with specific section references when the GDD is authored. This ADR establishes the interface contracts that the GDD will reference, not the reverse.

| GDD System | Requirement (expected) | How This ADR Addresses It |
|------------|----------------------|--------------------------|
| `design/gdd/vehicle-and-part-system.md` | Visual representation of per-slot damage states (Functional/Degraded/Offline) | MPB `_DamageAmount` float maps directly to the three DamageState enum values |
| `design/gdd/vehicle-and-part-system.md` | IVehicleView / IVehicleMutator interface boundary (systems index C3) | Key Interfaces section defines these contracts exactly — the GDD will reference this ADR for implementation |
| `design/gdd/vehicle-and-part-system.md` | Pillar 1 emotional attachment — visible part identity on chassis | Base sprites are per-part, artist-authored; each installed part has a distinct visual presence at all damage levels |
| `design/gdd/vehicle-and-part-system.md` | Chassis architecture scales to 3 chassis (2 for EA) | Addressables group pattern (`chassis-[name]`) adds Heavy Truck without architectural change |

## Performance Implications

> All estimates below are analytical. Actual values **must be validated** via Unity Profiler on target hardware after first implementation (Month 1–2) and recorded as test evidence.

- **CPU**: MPB dirty-flag pattern batches writes to LateUpdate — at most 5 slot updates per vehicle per frame × 2 vehicles = 10 MPB flushes maximum per frame. Estimated <0.1ms. Addressables loads are async and off the hot path.
- **Memory**: ≤60 base sprites × ~8KB avg (128×48px DXT5/BC3) ≈ 480KB sprites. 2 overlay textures × ~8KB ≈ 16KB. Total vehicle rendering: <500KB. Well within 2GB ceiling.
- **Draw Calls**: 5 slot renderers × 2 vehicles = 10 SpriteRenderers, each issuing one non-batched draw call = 10 draw calls. This is 5% of the 200 draw call budget.
- **Load Time**: Addressables async loads on chassis select screen (base sprites) and on combat scene start (overlay textures). No blocking operations on the main thread during gameplay.

## Migration Plan

Greenfield — no existing code to migrate. Shader Graph asset and Addressables groups are created from scratch in Month 1.

## Validation Criteria

| # | Test | Type | Pass Condition |
|---|------|------|---------------|
| 1 | Functional/Degraded/Offline states visually distinct at combat mid-field zoom | Visual — screenshot + lead sign-off | Reviewer can identify damage state from screenshot without reading labels |
| 2 | MPB `_DamageAmount` updated at most once per frame per slot regardless of event count (dirty flag working) | Automated (Unity Profiler trace) | Frame Debugger shows ≤10 SetPass calls for vehicle renderers |
| 3 | 60fps maintained with both vehicle sprites + 4 card VFX active | Performance — Unity Profiler | Frame time ≤16.6ms; vehicle rendering <2ms |
| 4 | Addressables load failure triggers fallback "missing part" silhouette without exception propagation | Automated / manual | Test with invalid Addressable key; fallback sprite displays, no unhandled exception logged |
| 5 | Point filter mode confirmed on all vehicle and overlay sprites | Visual | No bilinear blending visible at game camera zoom level |

## Related Decisions
- `design/gdd/systems-index.md` — Vehicle & Part System GDD scoping notes C1, C3 (Vehicle POCO sub-models, IVehicleView/IVehicleMutator)
- `design/art/art-bible.md` — Section 5.5 (master canvas system, per-chassis slot dimensions), Section 8.3 (DXT5/BC3 texture compression), Section 8.5 (Addressables chassis group structure), Section 8.7 (Point filter mode requirement)

# ADR-0012: Part Data Authoring + Sum-of-Parts Armor

## Status

Accepted (2026-06-02)

## Date

2026-06-02

## Last Verified

2026-06-02

## Decision Makers

User (BertanBerkol), Claude (technical-director-equivalent session)

## Summary

ADR-0010 retired the legacy vehicle-level armor pool and consolidated armor on
the `armor_0` slot, but left part authoring stubbed (`SlotSpec.ArmorContribution`
serialized but ignored, `VehicleDefinitionSO._armorHp` a hand-tuned literal).
This ADR introduces `PartDefinitionSO` as the authoring shape and re-establishes
**ADR-0001's sum-of-parts armor rule**: `armor_0.MaxHp = Σ (installed part).ArmorContribution`.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Scripting / Data Authoring |
| **Knowledge Risk** | LOW |
| **References Consulted** | ADR-0001, ADR-0008, ADR-0010, ADR-0011 |
| **Post-Cutoff APIs Used** | `AssetReferenceT<Sprite>` (Addressables, per ADR-0008) |
| **Verification Required** | EditMode test suite green; armor recompute invariant holds after install/uninstall/death/repair |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (sum-of-parts rule), ADR-0008 (Addressables sprite refs), ADR-0010 (single slot vocabulary), ADR-0011 (no-bridges) |
| **Enables** | Vehicle & Part System GDD finalization; player install/uninstall UX work |
| **Blocks** | None at runtime — production code currently runs on the int-only `InstallPart` overload |
| **Ordering Note** | Closes ADR-0010 Amendment A (Part-SO rebuild hook left open in Phase 5 of the Phase Plan) |

## Context

### Problem Statement

Today, every vehicle is authored with a single `_armorHp` literal on
`VehicleDefinitionSO`. Per-slot `ArmorContribution` exists on `SlotSpec` but is
**ignored at build time** (ADR-0010 § Current State documents this as a stub
return that violates ADR-0011). Enemy archetypes hardcode `armor_0` HP in
code-authored constants (e.g. Dredge `FrameArmorHp`). The Part SO itself does
not exist — `Assets/Resources/VehicleParts/` is empty.

This means **parts have no mechanical identity**: swapping a Reinforced Door
for a Light Door cannot change the vehicle's armor capacity, because armor is
authored at the vehicle level, not aggregated from parts. The intended
loop — collect parts, install for measurable stat differences — collapses to a
cosmetic change.

### Current State

- `Vehicle.MaxArmor` / `CurrentArmor` are computed properties that already
  aggregate over `SlotKind.Armor` slot instances (post-ADR-0010). Aggregation
  layer is correct; the broken layer is **derivation of `armor_0.MaxHp`**.
- `Vehicle.InstallPart(string slotId, int maxHp)` is the only install entry
  point. Enemies and tests use it directly; player vehicles use it via
  `VehicleDefinitionSO.BuildVehicle()` which feeds the literal `_armorHp` into
  `armor_0`.
- No `PartDefinitionSO`. No part catalog. No place to hang
  `ArmorContribution`, sprite refs, or the IPartData contract that ADR-0001's
  visual layer expects.
- ADR-0010 Phase 5 line in the slot-vocabulary plan explicitly defers
  `SlotSpec.ArmorContribution` deletion to "VehicleDefinitionSO Part-SO rebuild
  having landed (Amendment A revised)." This ADR is that rebuild.

### Constraints

- **No bridges (ADR-0011):** sum-of-parts must be the only path; no fallback
  to a hand-authored literal alongside it.
- **Demo-forward but canonical (memory `feedback_demo_forward_over_infrastructure`):**
  build the 1.0 shape directly. No transitional dual-write.
- **Enemy art absent:** no PSB rigs, no Addressables groups for enemy parts.
  Forcing enemies onto `PartDefinitionSO` now would require ~16 placeholder SOs
  with no visual payoff.
- **ADR-0001 visual-layer contract:** `IPartData` is the read interface the
  view layer expects. `PartDefinitionSO` must implement it (or expose an
  adapter the renderer reads).

### Requirements

- Player parts are authored as ScriptableObjects with stable PartId.
- `armor_0.MaxHp` is **derived** from installed parts; never authored directly.
- Recompute is deterministic and runs on every state transition that changes a
  part's contribution (install, uninstall, death/Offline, repair/Functional).
- Enemy code-authoring path stays open without requiring SOs.
- CI grep gate (per ADR-0010 Phase 5) goes green for `_armorHp` and
  `SlotSpec.ArmorContribution` deletions.

## Decision

### 1. PartDefinitionSO

```csharp
[CreateAssetMenu(menuName = "Wasteland Run/Part Definition")]
public sealed class PartDefinitionSO : ScriptableObject, IPartData
{
    [SerializeField] private string _partId;             // stable; CI-enforced unique
    [SerializeField] private string _displayName;
    [SerializeField] private SlotKind _slotKind;
    [SerializeField, Min(1)] private int _maxHp;
    [SerializeField, Min(0)] private int _armorContribution;
    [SerializeField] private AssetReferenceT<Sprite> _sprite;   // per ADR-0008

    public string PartId            => _partId;
    public string DisplayName       => _displayName;
    public SlotKind SlotKind        => _slotKind;
    public int MaxHp                => _maxHp;
    public int ArmorContribution    => _armorContribution;
    public AssetReferenceT<Sprite> Sprite => _sprite;
}
```

`ArmorContribution` is permitted on **every** `SlotKind` (Weapon, Engine,
Mobility, Hull, Armor, Exposable) — a Reinforced Door (Hull part) and a Heavy
Engine (Engine part) both add to the pool. The value MAY be zero for parts
that contribute nothing (most Exposables).

### 2. Vehicle.InstallPart — two overloads

```csharp
// Player path — authored via PartDefinitionSO asset
public void InstallPart(string slotId, PartDefinitionSO partDef);

// Code-author path — enemies, tests, editor tools. armorContribution is
// optional; omit it for slots that don't contribute (the common case).
public void InstallPart(string slotId, int maxHp, int armorContribution = 0);
```

These are not a primary/compat pair — both are first-class, permanent APIs
serving distinct use cases (asset-authoring vs programmatic authoring),
analogous to `List<T>.Add(item)` vs `List<T>.AddRange(items)`. The default
parameter (`armorContribution = 0`) preserves the tweakable shorthand for
zero-contribution slots that enemies and tests want.

The current single-arg `InstallPart(string slotId, int maxHp)` becomes the
default-parameter case of the int overload — no separate signature, no
redirect, no deprecation. Existing call sites that pass two args keep
compiling; sites that want to set armor pass a third int.

### 3. Sum-of-parts recompute

```csharp
private void RecomputeArmor()
{
    int sum = 0;
    foreach (var slot in _slots.Values)
        if (slot.State != DamageState.Offline)
            sum += slot.ArmorContribution;
    _slots["armor_0"].SetMaxHp(sum);   // clamps CurrentHp downward
}
```

Recompute is called from `InstallPart`, `UninstallPart`, and from `SlotInstance`
state-transition hooks (`Functional → Offline`, `Offline → Functional`).
Offline parts contribute zero — losing your reinforced engine **reduces** the
armor pool, matching the player-fantasy line "the meaning of parts fades
otherwise" from the design discussion.

`armor_0` is the aggregator slot. Its `ArmorContribution` field on the SO is
ignored (the slot exists as the visible buffer; it does not "armor itself").
This is the one whitelisted oddity, documented at the recompute site.

### 4. VehicleDefinitionSO rebuild

```csharp
[Serializable] public struct PartSlot { public string SlotId; public PartDefinitionSO Part; }

[SerializeField] private List<PartSlot> _parts;   // canonical list
// DELETED: _machineGun, _flamethrower, _engine, _wheels, _frame (SlotSpec),
// DELETED: _armorHp
```

`BuildVehicle()` iterates `_parts` and calls the SO overload. The hardcoded
five-slot mapping (`_machineGun → weapon_0`, etc.) is replaced by the
explicit `SlotId` on each entry — a player-vehicle SO can now author any slot
the `FrameLayoutSO` exposes.

### 5. Enemy archetypes

Enemy archetypes (`Dredge`, `DuneSkimmer`, `IronShepherd`) stay on the int
overload. Most calls keep their current `(slotId, maxHp)` shape and let
`armorContribution` default to 0. The archetype decides which slot carries
armor — typically `hull_0` — and writes the third arg only there:

```csharp
v.InstallPart("weapon_0",  10);          // no armor contribution
v.InstallPart("engine_0",  24);          // no armor contribution
v.InstallPart("mobility_0", 10);         // no armor contribution
v.InstallPart("hull_0",    55, 15);      // hull carries the armor (was FrameArmorHp)
v.InstallPart("armor_0",    0);          // buffer slot, contributes nothing itself
```

`FrameArmorHp` and equivalents stay as tweakable constants in each archetype
file; they just feed into the third arg of one `InstallPart` call instead of
into a separate `_armorHp` field. Total enemy churn per archetype: ~1 line
edited (the armor-bearing slot) + 1 line for `armor_0`. **No PartDefinitionSO
for enemies in this slice.**

### 6. Non-Goals (explicit)

- Enemy Part SO catalog (deferred — no enemy art, no payoff)
- Player install/uninstall UX
- Inventory system, part drops, scrap economy hooks
- Granted-card lifecycle changes (already covered by `project_granted_card_lifecycle`)
- Cross-slot bonuses or set effects (out of scope)

## Consequences

### Positive

- Parts have mechanical identity. Swapping a part demonstrably changes
  capacity, fulfilling the ADR-0001 player-fantasy pillar.
- Closes ADR-0010 Amendment A. Phase 5 CI grep gate goes green for
  `_armorHp` and `SlotSpec.ArmorContribution`.
- Single source of truth for armor capacity (the part list). No hand-tuned
  literal that can drift from the parts behind it.
- Unblocks Vehicle & Part System GDD finalization (was waiting on the
  authoring shape).

### Negative

- ~12-file touch on production + tests (counted in pre-draft survey):
  `Vehicle.cs`, `VehicleDefinitionSO.cs`, `CombatDataInitializer.cs`,
  `CombatPrefabAuthor.cs`, 3 enemy archetypes, plus tests
  `DamagePipeline_R_ARM_Tests.cs`, `DredgeArchetypeTests.cs`, and 2-3 layout
  tests + the `Vehicle_Scout.asset` regen.
- Enemy archetypes asymmetric to player vehicles (int-overload vs SO
  overload). Accepted explicitly via Non-Goal #1.
- Live armor recompute on Offline transitions changes player-visible armor
  capacity mid-fight. Designed behaviour, but UX must communicate
  "armor cap dropped" clearly (out of scope here; surface to ux-designer).

### Neutral

- `MaxArmor` / `CurrentArmor` API unchanged (computed properties already
  aggregate over `SlotKind.Armor` slots; only `armor_0.MaxHp` derivation
  changes).
- `armor_0` slot remains in `FrameLayoutSO`. Layouts unchanged.

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|--------------|--------|-------------|---------------------------|
| `design/gdd/vehicle-and-part-system.md` | Vehicle & Parts | Parts have mechanical identity (Pillar 1 emotional attachment) | `ArmorContribution` per-part makes part choice mechanically meaningful, not cosmetic |
| `design/gdd/vehicle-and-part-system.md` | Vehicle & Parts | Damage states (Functional/Degraded/Offline) drive visuals AND stats | Recompute hook on Offline transitions removes the part's armor contribution live |
| `design/notes/armor-model-stress-test.md` | Combat / Armor | Armor capacity reflects vehicle composition | `armor_0.MaxHp = Σ part.ArmorContribution` |
| `design/gdd/enemy-system.md` | Enemies | Enemy stat authoring stays code-side until art lands | Enemy archetypes use `(slotId, maxHp, armorContribution)` overload; no SO catalog required |

## Related

- **Supersedes** ADR-0010 § "Single storage of armor state" authoring shape (the
  aggregation rule and `armor_0` slot model stay; the `_armorHp` literal goes).
- **Implements** ADR-0001 § sum-of-parts armor model.
- **Closes** ADR-0010 Amendment A (Part-SO rebuild hook in Phase 5 plan).
- **Depends on** ADR-0008 (Addressables `AssetReferenceT<Sprite>` for part art).
- **Constrained by** ADR-0011 (no bridges — no parallel `_armorHp` literal
  surviving alongside sum-of-parts).

# Vehicle & Part System

> **Status**: In Design
> **Author**: Bertan Berkol + Claude Code agents
> **Last Updated**: 2026-05-18 (frame-driven variable slot revision)
> **Implements Pillar**: Pillar 1 (Vehicle as Character) — primary; Pillar 2 (Chassis Identity) — secondary
> **Review Mode**: Lean (light-touch)
> **Gated By**: ADR-0007 (Proposed 2026-05-18) — frame-driven variable slot system; supersedes the 4-slot data contract and vehicle-level Armor model of ADR-0001 (visual layer of ADR-0001 still in force)

## Overview

The Vehicle & Part System is the single most load-bearing system in Wasteland Run. It owns the Vehicle POCO, the **frame-driven variable slot architecture** (slot list authored per `FrameLayoutSO`, with 5 `SlotKind` values: Weapon, Engine, Mobility, Hull, Armor), the three-state damage model (Functional, Degraded, Offline), and the `IVehicleView` / `IVehicleMutator` interface contracts that every other system reads or writes through. Death is generalized to `StructuralHp == 0` across all slots flagged `IsStructural` (default: `Hull` slots). `Armor` is a first-class destroyable slot kind that absorbs damage at 1× until destroyed, then exposes its `RedirectsTo` target for `ExposureMultiplier`-scaled damage; the old vehicle-level `MaxArmor`/`CurrentArmor` stat is removed. No combat, economy, loot, status effect, or persistence logic touches vehicle state directly — they route through this system's interfaces. The Vehicle POCO is plain C# with no MonoBehaviour dependencies; the VehicleView MonoBehaviour reads it via `IVehicleView` and renders per ADR-0001 (visual contracts unchanged).

The system also owns the emotional weight of the vehicle. Pillar 1 (Vehicle as Character) asks that part loss feel like losing a limb, not swapping a tool — a requirement not met by data structures alone. It is met by per-part acquisition history, visible damage states on the chassis silhouette, part-specific deck identity, and the install/scrap decision that concludes every combat. These design decisions live here. For EA, the architecture supports all three player chassis (Scout = Small Frame 5 slots, Assault = Medium Frame 5 slots, Heavy Truck = Heavy Frame 7 slots) through `ChassisDefinitionSO` + `FrameLayoutSO`; only Scout is authored for EA. Assault and Heavy Truck slot in post-launch as new SO assets without code changes. Enemy frames (`tiny_frame`, `hauler_frame`, `dredge_frame`) are authored alongside their first consumers in `biome-1-enemy-roster.md`.

## Player Fantasy

You've been running this Engine since Biome 1. It's Degraded now — two orange cracks down the block, and three of the cards in your deck only exist because this Engine is bolted to your chassis. You've thought about scrapping it at every Chopshop since. You haven't. There's a Rare Engine in the loot pile after this combat. Better on paper. Installing it means losing three cards you built the last hour of play around. You will agonize over it.

When a part goes Offline in combat, it is not a stat drop. It is a hole in the silhouette of your car, and a deck capability you planned your next three turns around. For the seconds before you adapt, maybe this run is over. That moment is the price of admission to Pillar 1. The Vehicle & Part System exists to make sure it costs something.

*Design anchor: emotional attachment is created by three game-visible mechanisms — (1) visible damage states on the chassis silhouette (already specified by ADR-0001), (2) part-granted cards that enter and leave the deck on install/scrap, (3) part scarcity (the next part of the same slot type may not appear for several combats). A `CombatsSurvived` counter per installed part is tracked internally (R11) to support Scrap Economy's tenure-based refund formula (D.6) and Save & Persistence's `InstalledPartDTO` round-trip — this is bookkeeping, not a player-surfaced stat; no HUD element or tooltip displays it. Complexity at the player-facing layer is intentionally kept at Slay the Spire-level readability.*

## Detailed Design

### Core Rules

**R1 — Vehicle POCO Composition**

The Vehicle POCO is a plain C# model. Fields:

- `Chassis: ChassisType` — enum (Scout, Assault, HeavyTruck). Present for player vehicles only; identifies which `ChassisDefinitionSO` was selected. NOT the source of slot structure (that's the layout).
- `FrameLayoutId: string` — references the `FrameLayoutSO` asset that defines this vehicle's slot list. Set at construction; immutable for the life of the vehicle. Both player and enemy vehicles carry this.
- `Slots: List<SlotInstance>` — variable-length; one entry per `SlotDefinition` in the referenced `FrameLayoutSO`. Ordered identically to the `FrameLayoutSO.Slots` list. Index is stable for the run (slots are never added or removed at runtime — only their content changes).
- `StructuralHp: int` — read-only computed property. Equals the sum of `Hp` across all `SlotInstance` entries where `SlotInstance.IsStructural == true`. Not stored — recomputed from `Slots` on demand. Drives the death condition.
- `ActiveStatuses: List<StatusInstance>` — owned here per Status Effects GDD.

**Removed from old R1**: `MaxArmor`, `CurrentArmor` — vehicle-level Armor pool is gone. Armor is now per-slot HP on `SlotInstance` entries with `Kind == Armor` (see R1.1 and R_ARM). HUD surfaces wanting an aggregate "armor" number iterate `Slots.Where(s => s.Kind == Armor).Sum(s => s.Hp)` as a presentation-layer projection.

No MonoBehaviour inheritance. No UnityEvents. No `UnityEngine.Random`. Position state is NOT on the vehicle (owned by Combat per systems-index C1). Scrap currency is NOT on the vehicle (owned by Scrap Economy GDD).

**Death condition (R_DEATH):**

```
IsDead = (StructuralHp == 0)
```

`StructuralHp` is the sum of current `Hp` across all slots where `IsStructural == true`. At default layout configuration, only `Kind == Hull` slots are structural. Common case of one Hull slot: `IsDead` is equivalent to the legacy `Slots[Frame].DamageState == Offline` rule. Layouts with zero structural slots are rejected by `FrameLayoutSO.OnValidate` (a designer error would otherwise produce a vehicle born dead at `Hp == 0`).

---

**R2 — Slot Kinds (`SlotKind`)**

`SlotKind` is the taxonomy of what a slot does. It replaces the old `SlotType` enum. Note the enum name is `SlotKind` — not `SlotType` — to avoid collision with `FrameLayoutSO` (the asset type name).

| SlotKind | Role | `IsStructural` Default | Death-Critical |
|---|---|---|---|
| `Weapon` | Primary offense; authors attack card variants | `false` | No |
| `Engine` | Deck cycling / energy; authors Maneuver cards | `false` | No |
| `Mobility` | Position manipulation; gates route constraints | `false` | No |
| `Hull` | Structural integrity; Offline = vehicle death under default layout | `true` | Yes (default) |
| `Armor` | Destroyable plate protecting a vulnerable area; exposes a redirect target when Hp == 0 | `false` | No |

**Rename note:** `SlotType.Frame` is renamed to `SlotKind.Hull`. All references to `Frame` in prior sections, formulas, edge cases, and interfaces use `Hull` going forward. The word "Frame" now refers exclusively to `FrameLayoutSO` (the asset type).

**`IsStructural` override:** A designer may set `IsStructural = true` on any `SlotKind` via `SlotDefinition.IsStructuralOverride` (see R_FL). For example, a future layout could make an Engine slot structural. Default assignments above apply when no override is set.

**Plating rule:** `PlatingStacks` is used by `Weapon`, `Engine`, `Mobility`, and `Hull` slots. `Armor` slots do NOT use `PlatingStacks` — their damage behavior is governed by the Armor exposure mechanic (see R_ARM). Writing `AddPlating` to an `Armor` slot is a no-op.

**Vehicle-level Armor layer removed:** The old `MaxArmor` / `CurrentArmor` vehicle-level fields are gone. Armor absorption is now slot-local: each `Kind == Armor` slot acts as its own shield for its `RedirectsTo` target. See R_ARM for the full mechanics.

Slot count is **per-layout**, not per-chassis — see R_FL. Every run starts with all slots filled by the chassis's starter parts. Mid-run, slots may be `Empty` (after scrap, before re-install).

---

**R3 — SlotInstance Runtime Model**

`SlotInstance` is the runtime state for a single slot. It replaces the old `SlotState`. The full runtime field set (including R11 tenure tracking) lives below; static authoring data lives on `SlotDefinition` within `FrameLayoutSO` (see R_FL).

```
SlotInstance:
  SlotId              (string — unique within this vehicle, authored on FrameLayoutSO.SlotDefinition)
  Kind                (SlotKind enum — from SlotDefinition; immutable at runtime)
  Position            (SlotPosition enum — from SlotDefinition; immutable at runtime)
  IsStructural        (bool — from SlotDefinition; immutable at runtime)
  InstalledPart       (IPartData? — null if Empty; engine-free interface per ADR-0005/ADR-0007.
                       The PartDefinitionSO authoring asset is projected to IPartData at install time
                       by IPartCatalog. The engine-free WastelandRun.Vehicle assembly never references
                       PartDefinitionSO directly.)
  Hp                  (int, current — 0 when Offline, MaxHp when first installed)
  MaxHp               (int — from SlotDefinition.MaxHpOverride if set, else chassis-default for Kind)
  DamageState         (enum: Empty | Functional | Degraded | Offline — derived from Hp, never stored)
  PlatingStacks       (int, 0..MaxPlating — absorbs damage before Hp; only used when Kind != Armor)
  CombatsSurvived     (int — incremented on combat-win while installed; reset on InstallPart/RemovePart.
                       Owned by V&P (this system writes it); Scrap Economy D.6 reads it for tenure refund;
                       Save & Persistence round-trips it via InstalledPartDTO. See R11.)
  HudAnchor           (AnchorPoint — copied at construction from SlotDefinition.HudAnchor.
                       Engine-free struct (X, Y floats) in WastelandRun.Vehicle. UI subscribers
                       read this directly off SlotInstance without a layout round-trip.)
```

**Armor-specific fields (only used when `Kind == Armor`):**

```
  ExposureMultiplier  (float — from SlotDefinition; default 3.0 for Armor slots)
  RedirectsToSlotId   (string — from SlotDefinition; the SlotId that receives amplified damage when Armor Hp == 0)
```

These two fields are populated at construction from `SlotDefinition`. They are read-only at runtime. Non-Armor slots carry `ExposureMultiplier = 1.0` and `RedirectsToSlotId = null` (unused).

**`DamageState` derivation:** `Empty` if `InstalledPart == null`; `Offline` if `Hp == 0 && InstalledPart != null`; `Degraded` if `0 < Hp <= DegradedThreshold`; `Functional` otherwise.

**`DegradedThreshold`**: per-slot value derived from `LayoutDef.DegradedThresholdPct × slot.MaxHp / 100` (see F-VP1). `DegradedThresholdPct` is declared on the `IFrameLayout` engine-free interface (R_FL.1) with a default of `50`; applied uniformly to all slots in the layout. The legacy `ChassisDefinitionSO.DegradedThreshold%` field is deprecated (see Tuning Knobs). (Closes Review 4 Blocker #3 — owner is the interface, not the chassis SO.)

**Lookup:** Callers look up a `SlotInstance` by `SlotId`:

```csharp
SlotInstance GetSlot(string slotId)   // on Vehicle POCO or IVehicleView
```

The old `Slots[SlotType]` dictionary is replaced by this lookup. `IVehicleView` exposes `IReadOnlyList<SlotInstance> Slots` for enumeration and `GetSlot(slotId)` for direct access.

**Death condition**: `IsDead = (StructuralHp == 0)`, where `StructuralHp` is the sum of `Hp` across all `SlotInstance` entries with `IsStructural == true`. See R_DEATH on R1. For the common single-Hull layout, this collapses to the legacy `Slots[Frame].DamageState == Offline` rule.

---

**R4 — PartDefinitionSO Contract**

`PartDefinitionSO` is the **authoring** surface (a `ScriptableObject` in `WastelandRun.Gameplay`). The **runtime** contract consumed by the engine-free `WastelandRun.Vehicle` assembly is the `IPartData` interface (per ADR-0005, extended by ADR-0007). `IPartCatalog` projects each `PartDefinitionSO` to an `IPartData` instance at load time; runtime code (Vehicle, SlotInstance, IVehicleMutator, IVehicleView events) speaks only `IPartData` and `string PartId`. The fields below are the SO's authoring shape; they are mirrored on `IPartData` at runtime.

```
PartDefinitionSO:
  PartId            (string, unique)
  DisplayName       (string)
  Kind              (SlotKind enum)           — fits exactly one slot kind (renamed from SlotType)
  MountDirection    (MountDirection enum)     — Any | Front | Back; ignored for non-Weapon parts.
                                                Drives install gating (R5 step 5) and card
                                                PositionRequirement derivation.
  CompatibleChassis (ChassisType[])           — chassis allow-list (player vehicles only;
                                                enemy parts skip chassis gating)
  Rarity            (enum: Common|Uncommon|Rare|Legendary)
  GrantedCards      (CardDefinitionSO[])      — see R7
  StatModifiers     (StatModifierSO[])        — see R8
  SpriteKey         (Addressables key)        — per ADR-0001
  MaxPlating        (int, 0-3)                — plating cap this part contributes (non-Armor slots only)
  ArmorContribution (int, ≥0) [DEPRECATED]    — pre-revision contribution to vehicle-level MaxArmor.
                                                MaxArmor is removed under ADR-0007. Field carried
                                                forward at import for legacy SOs but flagged as
                                                deprecated; no new parts should set this field.
                                                Remove after balance pass.
```

SO import validation:
- `CompatibleChassis` must contain at least one chassis (player parts only)
- `GrantedCards.Count` must match R7's rarity formula (no override)
- `Kind` must match a valid `SlotKind` value
- `MountDirection` is only meaningful when `Kind == Weapon`; warn (not error) if set on non-Weapon parts
- `ArmorContribution` import flags **deprecated**; non-zero values produce a warning, not an error

---

**R5 — Install Rules**

Install is invoked via `IVehicleMutator.InstallPart(string slotId, IPartData part)`. The slot is identified by `SlotId` string, not `SlotKind`. The `part` parameter is the engine-free `IPartData` interface (callers obtain it from `IPartCatalog`, which resolves the underlying `PartDefinitionSO`). Exclusive callers: Scrap Economy (post-combat install, Merchant), Loot (post-combat auto-install on empty slot).

On install:

1. Resolve `slotId` → `SlotInstance`. If not found: throw `SlotNotFoundException`.
2. If the target slot has an existing part: scrap it first (apply R6 inline — granted cards removed, `RemovePart` side effects run).
3. Validate `part.CompatibleChassis` contains `vehicle.Chassis` (player vehicles only; enemy vehicles skip this check — enemy parts are layout-bound, not chassis-gated). Reject with `PartIncompatibleException` if not.
4. Validate `part.Kind == slot.Kind`; reject with `PartIncompatibleException` if mismatch.
5. **Weapon mount check:** if `slot.Kind == Weapon`, validate `part.MountDirection` matches `slot.Position` per the gating rule:
   - `part.MountDirection == Any`: always valid.
   - `part.MountDirection == Front`: valid only if `slot.Position == Front || slot.Position == Any`.
   - `part.MountDirection == Back`: valid only if `slot.Position == Back || slot.Position == Any`.
   - Reject with `MountDirectionMismatchException` if invalid.
6. Set `slot.InstalledPart = part`; `slot.Hp = slot.MaxHp`; `slot.PlatingStacks = 0`; `slot.CombatsSurvived = 0`.
7. Add `part.GrantedCards` to the player's deck (shuffled into the deck pile).
8. Fire `OnPartInstalled(slotId, part.PartId)` event. (Event payload is `string PartId`, not the `IPartData` object — subscribers that need full part data look it up via `IPartCatalog.GetPart(partId)` or read the live `GetSlot(slotId).InstalledPart` reference. Keeps the event signature engine-free and save-friendly.)

Install always resets HP to `MaxHp`. Scrap cost (Scrap Economy GDD) is the economic counterweight to free repair via reinstall; deck disruption is the design-side counterweight.

**Card position requirement derived from MountDirection (step 5 side effect):** When a weapon is installed, the cards it grants inherit their position requirement from `part.MountDirection`:

| `MountDirection` | Card `PositionRequirement` |
|---|---|
| `Front` | `EnemyAhead` |
| `Back` | `EnemyBehind` |
| `Any` | None (no position requirement) |

This derivation is baked at install time into the granted card instances. It is not re-evaluated at draw or play time.

**EA note:** All starter weapons have `MountDirection = Any`. Position-locked weapons arrive in the post-EA arsenal pass. No position requirements exist on cards for EA.

---

**R6 — Scrap Rules**

Scrap is invoked via `IVehicleMutator.RemovePart(slotId)`. The slot is identified by `SlotId` string. Exclusive caller: Scrap Economy (Chopshop scrap action, pre-install replacement via R5 step 2).

On scrap:

1. Resolve `slotId` → `SlotInstance`. If not found: throw `SlotNotFoundException`.
2. Remove `part.GrantedCards` from the player's deck, hand, and discard pile (all zones).
3. Set `slot.InstalledPart = null`; `slot.Hp = 0`; `slot.PlatingStacks = 0`; `slot.DamageState` re-derives as `Empty`.
4. Award Scrap currency per Scrap Economy GDD formula (Vehicle & Part does not own the amount).
5. Fire `OnPartRemoved(slotId, part.PartId)` event. (Event payload is `string PartId` — same engine-free contract as `OnPartInstalled` step 8 in R5.)

Scrap is permanent for the run. There is no undo.

**Chopshop-only enforcement:** Chopshop is the only context allowed to invoke `RemovePart` as a player-initiated action. Mid-combat part replacement routes through `InstallPart` step 2, which scraps internally.

---

**R_FL — Frame Layout System**

### R_FL.1 — FrameLayoutSO Contract

`FrameLayoutSO` is a ScriptableObject (engine-bearing, lives in `WastelandRun.Gameplay`) that declares a vehicle's slot list. It is the only source of structural truth for how many slots a vehicle has, what kinds they are, and their physical positions. Runtime callers in `WastelandRun.Vehicle` (engine-free per ADR-0005's `noEngineReferences: true`) consume the layout exclusively through `IFrameLayout` — they never reference `FrameLayoutSO` directly. The split mirrors `PartDefinitionSO → IPartData` (ADR-0005).

```
IFrameLayout (engine-free interface in WastelandRun.Vehicle; implemented by FrameLayoutSO):
  LayoutId              (string — unique across all layouts)
  IsPlayerUnlockable    (bool)
  Slots                 (IReadOnlyList<SlotDefinition> — engine-free struct, see below)
  DegradedThresholdPct  (int, 1..99 — applied uniformly to ALL slots in this layout via F-VP1.
                          Lives on the interface so the engine-free WastelandRun.Vehicle
                          assembly can compute DegradedThreshold without referencing
                          FrameLayoutSO. All 6 EA layouts implement this as 50.
                          Closes Review 4 Blocker #3.)

FrameLayoutSO (ScriptableObject in WastelandRun.Gameplay; implements IFrameLayout):
  LayoutId              (string, unique across all layouts — designer-authored, e.g. "small_frame")
  DisplayName           (string)
  IsPlayerUnlockable    (bool — true = appears in player chassis selection; false = enemy-only)
  Slots                 (List<SlotDefinition> — ordered; order is stable at runtime)
  DegradedThresholdPct  (int, 1..99 — default 50; serialized [SerializeField] that implements
                          IFrameLayout.DegradedThresholdPct. Per-layout authoring surface for
                          future balance overrides; all 6 EA layouts ship with 50.)
```

Each `SlotDefinition` within `Slots` (engine-free `[System.Serializable]` struct in `WastelandRun.Vehicle`):

```
SlotDefinition:
  SlotId                (string, unique within this layout — e.g. "weapon_back", "hull_0")
  Kind                  (SlotKind enum)
  Position              (SlotPosition: Any | Front | Back)
  MaxHpOverride         (int? — null = use chassis-default HP for this Kind;
                          non-null = overrides chassis default for this specific slot.
                          For Armor slots: MUST be set (≥ 1) — there is no chassis default
                          for the Armor Kind. OnValidate rejects null/<1 on Armor slots.)
  IsStructuralOverride  (bool? — null = use Kind default per R2; non-null = explicit override)
  HudAnchor             (AnchorPoint — engine-free 2D anchor struct in WastelandRun.Vehicle,
                          declared as `[System.Serializable] struct AnchorPoint { float X, Y; }`.
                          Replaces UnityEngine.Vector2 so SlotDefinition compiles under
                          noEngineReferences:true. Normalized [0..1] × [0..1] chassis-local UV;
                          (0.5, 0.5) = center. Consumed by Combat HUD via IVehicleView.Slots
                          iteration. Replaces the hardcoded 4-slot HUD positions from ADR-0001.
                          Mirrors ADR-0007 SlotDefinition.HudAnchor.)
  ExposureMultiplier    (float — only meaningful when Kind == Armor; ignored otherwise; default 1.0)
  RedirectsToSlotId     (string? — only meaningful when Kind == Armor; must resolve to a valid SlotId
                          within the same layout; null for non-Armor slots)
```

**SO import validation (`OnValidate` — the six rules below are hard import errors and reject the asset; see AC-VP48a–f for per-rule test coverage):**

1. **Unique SlotIds.** All `SlotId` values must be non-empty and unique within the layout. (Closes AC-VP48a.) `LayoutId` is also unique across all `FrameLayoutSO` assets (CI-enforced, no two layouts share an ID).
2. **At least one structural slot.** At least one slot must resolve `IsStructural == true` (either by Kind default or `IsStructuralOverride`). Layouts with zero structural slots are rejected. (Closes OQ-VP3 + AC-VP48b.)
3. **Armor `RedirectsToSlotId` is well-formed.** For any slot where `Kind == Armor`:
   a. `RedirectsToSlotId` must be non-null and non-empty.
   b. The referenced `SlotId` must exist in the same layout.
   c. The target `SlotId` must not equal the Armor slot's own `SlotId` (no self-redirect).
   d. The target slot's `Kind` must **not** be `Armor` (no Armor → Armor redirect chains — see EC-VP21 for the rationale; chained Armor would either recurse infinitely or produce non-deterministic amplification order, both fatal under ADR-0003 determinism). (Closes AC-VP48c.)
   e. The target slot must have `IsStructural == true` (mirrors ADR-0007 Decision 2 OnValidate rule 3 — Armor exists to protect a structural slot).
4. **`ExposureMultiplier` is finite and positive.** For any slot where `Kind == Armor`: `ExposureMultiplier > 0`, `!float.IsNaN`, `!float.IsInfinity`. Value `< 1.0` inverts the design intent (makes damage weaker when exposed) — flag as a warning at import, not an error, to allow designer testing. NaN/Inf is a hard error. (Closes AC-VP48d.)
5. **`HudAnchor` is finite and in unit range.** `HudAnchor.X` and `HudAnchor.Y` must each be `IsFinite` (no NaN, no ±Inf) AND in `[0, 1]`. The `AnchorPoint.IsInUnitRect` helper performs this check. Out-of-range or non-finite values are a hard import error. (Closes AC-VP48e.)
6. **HP boundaries prevent a zero-width Functional band.** For the layout: `DegradedThresholdPct ∈ [1, 99]` (rejects `0` and `100` — both produce a degenerate band, see F-VP1 boundary discussion). For each `Kind == Armor` slot: `MaxHpOverride` must be non-null AND `≥ 1` (Armor has no chassis default; missing or zero override would spawn the slot Offline). For non-Armor slots with non-null `MaxHpOverride`: value must be `≥ 1` (the chassis default fallback is already constrained `≥ 1` by `ChassisDefinitionSO.OnValidate`). (Closes Review 2 blocker #7 + AC-VP48f + Review 3 blockers B4 + B5.)

**Non-error advisories:**
- `Position` on a non-Weapon slot is informational (used by UI art placement); it has no gameplay effect except on Weapon slots where MountDirection gating applies.
- `ExposureMultiplier > 5.0` produces a warning (high amplification + overflow risk, see SafeAmplify in F-VP2), not an error.

**Vehicle construction:** When a vehicle is instantiated, `Slots: List<SlotInstance>` is built by iterating `IFrameLayout.Slots` in order, constructing one `SlotInstance` per `SlotDefinition`, copying `SlotId`, `Kind`, `Position`, `IsStructural`, `HudAnchor`, `ExposureMultiplier`, and `RedirectsToSlotId` from the definition. `InstalledPart` starts as `null`. `Hp` starts as `MaxHp` (which resolves `MaxHpOverride ?? chassis-default`). `PlatingStacks = 0`, `CombatsSurvived = 0`. `HudAnchor` is copied at construction so UI subscribers can read it from `IVehicleView.Slots` without a layout round-trip.

---

### R_FL.2 — Player Frame Layouts

All player layouts have `IsPlayerUnlockable = true`.

**Small Frame** (`LayoutId: "small_frame"`) — Scout chassis, EA

| SlotId | Kind | Position | MaxHpOverride | IsStructural |
|---|---|---|---|---|
| `weapon_front` | Weapon | Front | 10 | false |
| `weapon_back` | Weapon | Back | 10 | false |
| `engine_0` | Engine | Any | 12 | false |
| `mobility_0` | Mobility | Any | 12 | false |
| `hull_0` | Hull | Any | 16 | true |

Total slot count: 5. Total structural HP: 16.

Design intent: Glass-cannon. Two weapon mounts (front + back) allow simultaneous offense from both positions, but the Hull is the thinnest of all player frames.

---

**Medium Frame** (`LayoutId: "medium_frame"`) — Assault chassis, post-EA

| SlotId | Kind | Position | MaxHpOverride | IsStructural |
|---|---|---|---|---|
| `weapon_0` | Weapon | Any | — (chassis default) | false |
| `weapon_1` | Weapon | Any | — | false |
| `engine_0` | Engine | Any | — | false |
| `mobility_0` | Mobility | Any | — | false |
| `hull_0` | Hull | Any | 24 | true |

Total slot count: 5. Total structural HP: 24.

Design intent: Assault / tank. Dual Any-position weapons. Higher Hull HP than Small Frame. Post-EA content; architecture only for EA.

---

**Heavy Frame** (`LayoutId: "heavy_frame"`) — Heavy Truck chassis, post-EA

| SlotId | Kind | Position | MaxHpOverride | IsStructural |
|---|---|---|---|---|
| `weapon_0` | Weapon | Any | — | false |
| `weapon_1` | Weapon | Any | — | false |
| `weapon_2` | Weapon | Any | — | false |
| `engine_0` | Engine | Any | — | false |
| `engine_1` | Engine | Any | — | false |
| `mobility_0` | Mobility | Any | — | false |
| `hull_0` | Hull | Any | — | true |

Total slot count: 7. Total structural HP: chassis-default for Hull Kind (TBD post-EA balance pass).

Design intent: Sustained fire, heavy HP. Three weapons + two engines. Post-EA content.

---

### R_FL.3 — Enemy Frame Layouts

All enemy layouts have `IsPlayerUnlockable = false`. Enemy vehicles do not use `ChassisType` gating on part installs — enemy parts are layout-bound by SO authoring.

**Tiny Frame** (`LayoutId: "tiny_frame"`) — Dune Skimmer, reusable scout layout

| SlotId | Kind | Position | MaxHpOverride | IsStructural |
|---|---|---|---|---|
| `weapon_0` | Weapon | Any | — | false |
| `engine_0` | Engine | Any | — | false |
| `mobility_0` | Mobility | Any | — | false |
| `hull_0` | Hull | Any | — | true |

Total slot count: 4.

---

**Hauler Frame** (`LayoutId: "hauler_frame"`) — Iron Shepherd, reusable elite layout

| SlotId | Kind | Position | MaxHpOverride | IsStructural |
|---|---|---|---|---|
| `weapon_0` | Weapon | Any | — | false |
| `weapon_1` | Weapon | Any | — | false |
| `engine_0` | Engine | Any | — | false |
| `mobility_0` | Mobility | Any | — | false |
| `hull_0` | Hull | Any | — | true |

Total slot count: 5.

---

**Dredge Frame** (`LayoutId: "dredge_frame"`) — Dredge boss, bespoke layout

| SlotId | Kind | Position | MaxHpOverride | ExposureMultiplier | RedirectsToSlotId | IsStructural |
|---|---|---|---|---|---|---|
| `weapon_minigun` | Weapon | Front | — | — | — | false |
| `weapon_javelin` | Weapon | Front | — | — | — | false |
| `weapon_ram` | Weapon | Front | — | — | — | false |
| `weapon_flail` | Weapon | Back | — | — | — | false |
| `engine_0` | Engine | Any | — | — | — | false |
| `engine_1` | Engine | Any | — | — | — | false |
| `mobility_0` | Mobility | Any | — | — | — | false |
| `hull_0` | Hull | Any | — | — | — | true |
| `armor_chest` | Armor | Front | **BALANCE PASS REQUIRED** | 3.0 | `hull_0` | false |
| `armor_back` | Armor | Back | **BALANCE PASS REQUIRED** | 3.0 | `hull_0` | false |

Total slot count: 10. Total structural HP: `hull_0` MaxHp (TBD balance pass).

Armor HP for `armor_chest` and `armor_back` must be set on `MaxHpOverride` (the Armor `SlotKind` has no chassis default — see R_FL.1 validator). The Dredge `dredge_frame` SO **will not pass `OnValidate`** until both Armor slots receive non-null `MaxHpOverride >= 1`. This is a hard import-time gate, not an advisory; the balance pass must lock these two numbers before the Dredge SO can ship. Tuning Knobs section provides design guidance (~2-3 focused unupgraded-Scout hits to break one plate).

---

**R_ARM — Armor Slot Damage Behavior**

### R_ARM.1 — Overview

An `Armor` slot is a destructible plate sitting in front of a vulnerable target. When the Armor plate has HP remaining, it absorbs incoming damage normally. When the Armor plate has been destroyed (`Hp == 0`), the wound is exposed: subsequent hits to the same Armor slot location deal amplified damage routed to the `RedirectsTo` target slot instead.

The player's drag-to-crosshair targeting UX does not change based on Armor state. The player always targets the same visual location. The system resolves the actual damage target and multiplier from the slot's current state.

### R_ARM.2 — Damage Behavior

The full Armor slot damage resolution, invoked when `ApplyDamage(slotId, amount, source)` targets a slot where `Kind == Armor`:

```
[Armor slot — intact: Hp > 0]
  damage lands on Armor Hp normally (1x multiplier, no redirect)
  armor_consumed = min(amount, slot.Hp)
  slot.Hp = slot.Hp - armor_consumed
  overflow = amount - armor_consumed
  if overflow > 0:
    redirect target = GetSlot(slot.RedirectsToSlotId)
    redirected_amount = SafeAmplify(overflow, slot.ExposureMultiplier)
    ApplyDamage(redirect target, redirected_amount, source)
    // overflow after full Armor destruction in same hit is also amplified (breakthrough policy)

[Armor slot — exposed: Hp == 0 && InstalledPart != null]
  damage is fully redirected to RedirectsTo slot, amplified
  redirected_amount = SafeAmplify(amount, slot.ExposureMultiplier)
  ApplyDamage(GetSlot(slot.RedirectsToSlotId), redirected_amount, source)
  // the Armor slot itself takes no further HP damage (already at 0)

[Armor slot — empty: InstalledPart == null]
  no-op (identical to EC-VP1 rule for empty slots)

// Overflow-safe amplification helper (resolves Review 2 blocker #6 + Review 3 B6):
SafeAmplify(int amount, float multiplier) → int:
  // Defensive guards on `multiplier` (closes Review 3 B6).
  // OnValidate (R_FL.1 rule 4) already rejects NaN/Inf at import time, so reaching
  // this branch implies a runtime corruption (deserialization error, debug command,
  // or status effect math bug). Fall back to the safe identity: deliver `amount`
  // unamplified rather than wrap to int.MinValue (the C# `(int)NaN` cast result).
  if float.IsNaN(multiplier) or float.IsInfinity(multiplier):
    log.Warn($"SafeAmplify received non-finite multiplier ({multiplier}) — falling back to amount={amount}")
    return amount

  // Multiplier ≤ 0 is also defended (OnValidate enforces > 0; a runtime debug write
  // could violate it). Negative multiplication would heal the redirect target.
  if multiplier ≤ 0f:
    log.Warn($"SafeAmplify received non-positive multiplier ({multiplier}) — falling back to amount={amount}")
    return amount

  amplified = floor((double) amount × (double) multiplier)   // double arithmetic, then floor
  // NaN/Inf in `amplified` is already impossible given the guards above + amount ≥ 0.
  return (int) min(amplified, (double) int.MaxValue)         // clamp before narrowing cast
  // Without the clamp, (int)(amount × multiplier) wraps to int.MinValue at
  // large inputs (e.g. amount=1_000_000_000 × 3.0 = 3e9 > int.MaxValue 2.147e9).
  // Wrap would cause the redirect target to be "healed" instead of damaged.
```

**Variable table:**

| Symbol | Type | Range | Description |
|---|---|---|---|
| `amount` | int | ≥ 0 | Incoming damage after all Card Combat modifiers (Corrode, position bonus, etc.) |
| `slot.Hp` | int | 0..MaxHp | Current HP of the Armor plate |
| `slot.ExposureMultiplier` | float | > 0.0 (design intent ≥ 1.0) | Multiplier applied to damage redirected to the underlying target when Armor is exposed |
| `redirected_amount` | int | 0..int.MaxValue | `SafeAmplify(amount, ExposureMultiplier)` — `(int) min(floor(amount × ExposureMultiplier), int.MaxValue)`. Damage delivered to `RedirectsTo` slot. |
| `overflow` | int | 0..amount | Damage remaining after Armor plate is destroyed in a single hit |

**Output range:** `redirected_amount` is floored and clamped at `int.MaxValue` before casting to int (overflow-safe; closes Review 2 blocker #6). If `ExposureMultiplier = 3.0` and `amount = 5`, the redirected amount is 15. Practical-range design intent has no ceiling — the clamp exists solely to prevent C# integer wrap at pathological inputs (e.g., debug commands or future damage-stat overflow). The Armor exposure state is intended to be a meaningful threat at normal play values.

### R_ARM.3 — Worked Examples

**Example 1 — Hit lands on intact Armor (doesn't destroy it):**

```
armor_chest: Hp = 12, MaxHp = 20, ExposureMultiplier = 3.0, RedirectsTo = hull_0
Incoming: amount = 8

armor_consumed = min(8, 12) = 8
slot.Hp = 12 - 8 = 4  (Armor still intact, Hp = 4)
overflow = 0
Result: hull_0 takes no damage. armor_chest DamageState — at Hp=4 / MaxHp=20 with
DegradedThresholdPct=50, threshold = 10; Hp=4 ≤ 10 → Degraded.
```

---

**Example 2 — Hit destroys Armor plate; overflow redirects to Hull:**

```
armor_chest: Hp = 4, ExposureMultiplier = 3.0, RedirectsTo = hull_0
hull_0: Hp = 60 (Dredge Hull)
Incoming: amount = 10

armor_consumed = min(10, 4) = 4
slot.Hp = 0  (Armor plate destroyed — DamageState transitions to Offline)
overflow = 10 - 4 = 6
redirected = floor(6 × 3.0) = 18
ApplyDamage(hull_0, 18, source)
Result: armor_chest = Offline (granted cards on armor_chest soft-disabled if any per ADR-0007 Decision 16; not removed from zones); hull_0.Hp drops by 18.
```

---

**Example 3 — Hit lands on already-exposed Armor (Hp == 0):**

```
armor_chest: Hp = 0 (exposed), InstalledPart = ArmorPlate SO, ExposureMultiplier = 3.0, RedirectsTo = hull_0
hull_0: Hp = 42
Incoming: amount = 7

Full redirect (no Armor HP remaining):
redirected_amount = floor(7 × 3.0) = 21
ApplyDamage(hull_0, 21, source)
Result: hull_0.Hp drops by 21 (from 42 to 21, assuming no Plating).
```

---

### R_ARM.4 — DOT Bypass on Armor Slots

Damage-over-time effects (Burning, and any future DOT) applied to an Armor slot bypass the exposure mechanic entirely: the DOT tick's damage lands directly on the Armor slot's own `Hp`, regardless of whether `Hp == 0`. This means DOT on an exposed Armor slot ticks on a slot that is already Offline — a no-op. DOT is an internal threat, not an external hit, and does not trigger redirection.

This is consistent with the existing DOT bypass rule (Plating/Armor bypass for DOT, F-VP2 ordering contract).

### R_ARM.5 — Visual Contract

Armor slots have distinct visual states mapped to `DamageState`:

| DamageState | Visual |
|---|---|
| `Functional` | Intact armor sprite |
| `Degraded` | Cracked armor sprite (damage cracks visible) |
| `Offline` | Exposed wound sprite (the underlying vulnerable structure showing through) |
| `Empty` | No sprite rendered for this slot location |

Player UX: the player always drags to the same target crosshair on the vehicle regardless of Armor state. The system routes damage based on the slot's current `DamageState`. The HUD displays the Armor slot's own `Hp` bar alongside the `RedirectsTo` slot's HP bar so the player can read the exposure state.

---

**R7 — Part-Granted Cards (Rarity-Scaled)**

Each PartDefinitionSO authors a fixed-length `GrantedCards` list. Length is enforced at SO import by rarity:

| Rarity | GrantedCards count |
|---|---|
| Common | 1 |
| Uncommon | 2 |
| Rare | 3 |
| Legendary | 3 |

Granted cards enter the deck on install (R5). They are **hard-removed** only on (a) scrap (R6) and (b) external-source termination (e.g., Dredge Javelin chain cut, where the originating system signals termination of the grant). On slot **Offline** the granted cards are **soft-disabled**, not removed: they remain in deck/hand/discard zones and are rendered unplayable via the source-slot playability gate (per Decision 16 in ADR-0007 — identical pattern to the existing `EnemyAhead`/`EnemyBehind` positional gate). Repairing the slot above Offline restores playability without re-entering the deck (no re-draw, no shuffle perturbation). See "States and Transitions" and the dedicated **Hard Removal Pathway** subsection for the full lifecycle table. Closes R6 Blocker 1 (game-designer / systems-designer / ux-designer / ui-programmer four-specialist convergence).

Granted cards are **tracked by part identity** — hard-removal targets the exact instances added by that part, not arbitrary cards of the same SO. If the player owns two copies of the same card from two sources (part grant + card reward), scrapping the part hard-removes only its instance. Soft-disable does not consult identity beyond the source-slot match (all cards whose `SourceSlotId` references an Offline slot are dimmed in-zone).

---

**R8 — Stat Modifiers**

Each PartDefinitionSO can contribute stat modifiers via `StatModifiers: StatModifierSO[]`. Stat values that modifiers can affect are defined by downstream systems (Card Combat: base damage, starting energy, max hand size; Scrap Economy: price modifiers):

```
StatModifierSO:
  TargetStat  (enum — defined by consumers)
  Operation   (enum: Add | Multiply | Override)
  Value       (float)
```

This GDD defines the modifier contract. Which stats exist and how they compose is owned by the consuming system. Vehicle & Part provides the data; Card Combat / Scrap Economy read it via `IVehicleView.GetStatModifier(stat)`.

---

**R9 — Interfaces (C3)**

> **Interface declarations live in ADR-0007 §Key Interfaces (single source of truth — pattern locked 2026-05-18, R6 strategy switch).** This GDD references members by name only and does not restate signatures. Enum declarations (`SlotKind`, `SlotPosition`, `MountDirection`, `DamageState`, `ChassisType`, `DamageSource`), the `IVehicleView` and `IVehicleMutator` surfaces, all event signatures (`OnSlotHpChanged`, `OnSlotDamageStateChanged`, `OnArmorExposed`, `OnGrantedCardRemoved`, `OnVehicleDied`, `OnCriticalStateChanged`, `OnPlatingChanged`, `OnStatusStackChanged`, `OnPartInstalled`, `OnPartRemoved`, `OnStatusApplied`, `OnStatusExpired`), and the `IGrantedCardData` / `AnchorPoint` POCOs are all canonically declared there. CI lock-step grep (`tools/ci/check-adr0007-vp-lockstep.sh`, ADR Decision 13) blocks any GDD edit that reintroduces a verbatim restatement.

The interfaces live in `WastelandRun.Vehicle.asmdef` (`noEngineReferences: true`); the engine-free assembly placement is enforced by ADR-0007 plus AC-VP46/AC-VP47/AC-VP52 CI grep gates. The runtime contract speaks `IPartData` (ADR-0005), `IGrantedCardData` (ADR-0007 Key Interfaces), and `string PartId` / `string CardId` (events + save).

All non-mutator systems access vehicle state exclusively through `IVehicleView`. Mutator access is restricted to the callers listed in the write-access table below; this is a V&P-owned policy, not part of the ADR interface contract.

**Cross-references the GDD uses by name** (none of these add new contract — they cite ADR-declared members):

- `IVehicleView.Slots`, `.GetSlot(slotId)`, `.GetSlotsByKind(kind)`, `.StructuralHp`, `.IsDead`, `.CriticalState`, `.ActiveStatuses`, `.GetStatModifier(stat)` — read paths used by F-VP1, F-VP2, F-VP3, R_ARM, R_FL.
- `IVehicleMutator.ApplyDamage`, `.Repair`, `.AddPlating`, `.InstallPart`, `.RemovePart`, `.ApplyStatus`, `.RemoveStatus`, `.TickStatuses`, `.HardRemoveCards` — mutator entrypoints used by F-VP2, R5, R6, R7, and the new "Hard Removal Pathway" section. `HardRemoveCards` is the Decision 16 surface for scrap and external-source termination.
- Events used by F-VP2 Step 4 atomic ordering: `OnSlotHpChanged`, `OnSlotDamageStateChanged`, `OnArmorExposed`, `OnGrantedCardRemoved`, `OnVehicleDied`, `OnCriticalStateChanged` — see F-VP2 Canonical Event Order Table for sequencing.

**`IsDead` semantics (V&P-owned subscriber contract, complements ADR-0007 Decision 11):** `IsDead` is a backing field with a private setter, not a derived getter. Subscribers reading `IsDead` from any handler other than `OnVehicleDied` will observe the pre-write value (`false`) until `OnVehicleDied` fires. This guarantee is depended on by EC-VP13 (mid-resolution structural Offline) and AC-VP44 (single-Hull defeat ordering). ADR-0007 Decision 11 invariant rules govern the write; this GDD owns the subscriber-side rule.

**`CriticalState` subscriber contract:** `CriticalState` transitions fire `OnCriticalStateChanged` AFTER `OnVehicleDied` per F-VP2 Step 4(h) so death-frame subscribers can suppress critical-state UI via an `IsDead` check inside the handler. Subscriber list: HUD vignette/red-screen overlay, audio heartbeat cue, accessibility low-vision pulse.

**Removed surface (historical — no replacement in ADR-0007):**
- `int MaxArmor`, `int CurrentArmor`, `event OnMaxArmorChanged`, `event OnCurrentArmorChanged` — vehicle-level Armor pool eliminated.
- `void AddArmor(int amount)` — `RestoreArmorEffectSO` is deprecated; existing cards using this effect require a Card Combat GDD design review.
- `void RecalculateMaxArmor()` — structural HP is recomputed inline within `ApplyDamage` Step 4.

**Write access (V&P-owned policy — reviewer-enforced, not in ADR):**

- `ApplyDamage` — Card Combat, Status Effects tick pipeline, Armor exposure redirect (internal recursive call per F-VP2 Step 2)
- `Repair`, `AddPlating` — Card Combat (via `RepairSubsystemEffectSO` / `RestorePlatingEffectSO`)
- `InstallPart`, `RemovePart` — Scrap Economy
- `HardRemoveCards` — Scrap Economy (scrap path, `sourceSlotId` = the slot being scrapped) and external-source termination callers (Status Effects, boss scripts, with `sourceSlotId = null` for non-slot cohorts). No other callers.
- `ApplyStatus`, `RemoveStatus` — Card Combat, Status Effects, Enemy AI (all route through `ApplyStatusEffectSO` / `RemoveStatusEffectSO`)
- `TickStatuses` — Card Combat only (call site: once per combat step, between damage resolution and end-of-step hooks; exact placement spec'd by Card Combat GDD)

Reviewers MUST fail any code change that introduces a new caller into the surfaces above without a corresponding update to this list.

---

**R10 — Fuel Container Contract (retrofit 2026-04-23 — Node Map + NE integration)**

Fuel is a run-layer resource owned by Node Map's commit pipeline (commit step 4 = `SpendFuel`) and granted by NE's Event/Treasure handlers. V&P owns the per-vehicle fuel container and the chassis-level fuel multiplier.

```csharp
public interface IFuelContainer  // exposed on IVehicleView
{
    int   CurrentFuel   { get; }   // 0 <= CurrentFuel <= FuelCap
    int   FuelCap       { get; }   // from ChassisDefinitionSO, post-multiplier
    float FuelMultiplier{ get; }   // chassis fuel-cost multiplier (Scout 0.8, Assault 1.0, Truck 1.3 — matches registry vehicle.constants.FuelBurnMultiplier; BLOCKER-2 arbitration 2026-04-24)
}

public interface IFuelMutator   // exposed on IVehicleMutator
{
    // Returns actual-spent amount. Caller must have validated affordability.
    int SpendFuel(int amount);

    // Grants fuel, capping at FuelCap. Returns actual-granted amount
    // (may be less than `amount` if cap is hit — excess is silently dropped).
    // Used by NE Windfall/Treasure handlers and Rest handler (per NE Section D/E).
    int TryGrantFuel(int amount);
}
```

**Overflow semantics (TryGrantFuel, C3 — 2026-04-24):** If `CurrentFuel + amount > FuelCap`, the excess is silently dropped — no toast, no error, no pending-grant queue. `TryGrantFuel(15)` on a vehicle with `CurrentFuel = 18, FuelCap = 20` grants `2` (returns `2`); the `13` excess is lost. The return value is always `actualGranted = min(amount, FuelCap - CurrentFuel)` and is guaranteed to satisfy `0 <= return <= amount`. V&P does **not** auto-convert overflow into Scrap or any other resource — that is a caller concern. Callers that want overflow-to-Scrap behavior must call `IScrapEconomy.TryConvertFuelToScrap(amount - actualGranted)` themselves (see Scrap Economy `IScrapEconomy.TryConvert*` for the caller-agnostic conversion surface; Events are the expected canonical caller per Node Encounter GDD). This matches NE's `FuelGrantRange` contract and the Rest handler's no-ceiling-bump promise. Consumer systems that care about lost fuel must check `return < amount`.

**SpendFuel authoritative:** Node Map's commit pipeline step 4 calls `SpendFuel(commitCost * FuelMultiplier)`. The multiplier is applied caller-side (Node Map F-NM1 `EdgeFuelCost` formula) before the call reaches V&P; V&P's `SpendFuel` is multiplier-blind. `FuelMultiplier` is exposed on `IFuelContainer` for Node Map's pre-commit affordability check and UI preview only.

---

**R11 — InstalledPart Read Access + Tenure Tracking (retrofit 2026-04-23 — Scrap Economy)**

Scrap Economy's tenure-refund mechanic (Scrap Economy D.6 / F.5b) requires reading `InstalledPart` metadata and the part's combat tenure. The runtime data model is `SlotInstance` (see R3); its full field list is defined there. `CombatsSurvived` is part of the `SlotInstance` field set and is read-only externally.

**`CombatsSurvived` ownership (single writer — closes Review 3 B10):**

V&P is the **single authoritative writer** of `CombatsSurvived`. No other system mutates the field. The field is exposed through `IVehicleView.GetSlot(slotId).CombatsSurvived` (read-only) and round-tripped by Save & Persistence's `InstalledPartDTO`. Scrap Economy GDD D.6 is a **reader only** — its tenure-refund formula consumes the value but never sets it. Any prior wording in Scrap Economy D.6 that implied Combat Resolution writes `CombatsSurvived` is superseded by this rule.

**Write trigger.** V&P subscribes to Combat Resolution's `OnCombatEnded(CombatResult result)` event (Card Combat GDD owns the event declaration). On every fired event where `result.DidPlayerWin == true`, V&P iterates `vehicle.Slots` and, for each slot where `InstalledPart != null` AND `DamageState != Offline`, increments `slot.CombatsSurvived` by 1. Offline slots do NOT accrue tenure — a part that ended combat at 0 HP did not "survive" in any narrative sense, and tenure-refund should not reward holding a dead part across combats. Empty slots are skipped. The increment happens once per `OnCombatEnded` firing, before any post-combat installs/scraps (Loot auto-install and Chopshop are downstream consumers of the bumped value).

**`CombatsSurvived` semantics:**
- Incremented by `+1` per qualifying slot on every `OnCombatEnded(result)` where `result.DidPlayerWin == true` AND `slot.InstalledPart != null` AND `slot.DamageState != Offline`.
- Reset to `0` on `InstallPart` (fresh install = fresh counter) and on `RemovePart` (counter goes with the part).
- Persisted in Save & Persistence's `InstalledPartDTO` (Save GDD retrofit queued).
- Consumed by Scrap Economy `TryScrapPart` (read-only) to compute tenure refund: `RefundScrap = BaseSalvageValue + (CombatsSurvived * TenureBonus)` per Scrap Economy D.6.

**`StatModifiers` accessor:** Exposed as a read-only projection to avoid double-hop through `InstalledPart`. Matches `IVehicleView.GetStatModifier(stat)` composition (R8).

---

**R12 — Part Catalog + Compare Panel (retrofit 2026-04-23 — Loot & Reward)**

L&R's post-combat part-drop flow needs to (a) enumerate available parts for a given `(slot, rarity, chassis)` filter, and (b) preview an install against the player's current vehicle. V&P owns the catalog and the preview.

```csharp
public interface IPartCatalog                                       // lives in WastelandRun.Vehicle (engine-free); see boundary note
{
    // Returns IPartData projections of all parts matching the filter. L&R uses this
    // for both reward generation (D.5) and Chopshop inventory seeding (Scrap Economy).
    // Engine-free: PartDefinitionSO assets are projected to IPartData inside the
    // Addressables-backed implementation (which lives in WastelandRun.Gameplay).
    IReadOnlyList<IPartData> GetParts(SlotKind kind, PartRarity rarity, ChassisType chassis);

    // Single-part lookup by string PartId. Used by event subscribers that received an
    // OnPartInstalled/Removed event with a string payload and want full IPartData.
    // Returns null if no part with the given PartId exists in the catalog.
    IPartData GetPart(string partId);

    // Installs are routed through IVehicleMutator.InstallPart; this is a pure-read helper.
    // Returns a preview of the post-install vehicle state without mutating.
    VehicleStatePreview PreviewInstall(IVehicleView vehicle, string slotId, IPartData candidate);
}

public sealed class VehicleStatePreview
{
    public IReadOnlyDictionary<StatType, float> PredictedStatModifiers;
    public string[] CardIdsAddedToDeck;     // candidate.GrantedCards projected to CardId strings (engine-free)
    public string[] CardIdsRemovedFromDeck; // if slot has existing part, those granted cards by CardId
    // Removed: PredictedMaxArmor, PredictedCurrentArmor — vehicle-level Armor layer eliminated.
    // PredictedStructuralHp may be added in a future pass if L&R compare-panel needs it.
}
```

`IPartCatalog` interface lives in `WastelandRun.Vehicle.asmdef` (engine-free, `noEngineReferences: true`). Its **implementation** (`AddressablesPartCatalog`) lives in `WastelandRun.Gameplay`, where the `PartDefinitionSO → IPartData` projection happens at load time per ADR-0005. This split keeps the interface side of the contract clean of `UnityEngine.ScriptableObject` references (closes Review 2 blocker #3 R12 leak).

L&R consumes `GetParts` for its `PartRarityWeight` sampling (L&R Section D.5). The compare-panel (L&R UI / Post-Combat Flow UI) consumes `PreviewInstall` to render the before/after delta. Event subscribers (HUD, analytics) use `GetPart(partId)` to resolve event payloads when they need full part data.

**Empty-return contract (C2, 2026-04-24).** `GetParts(slot, rarity, chassis)` is defined to return an empty `IReadOnlyList<IPartData>` (never `null`) when no assets match the filter — for example, a `(Weapon, Rare, Scout)` query when no Scout-compatible Rare weapons have been authored yet. An empty return is a valid outcome, not an error: L&R's half-biome-base Scrap fallback (L&R EC-LR7) handles the gap. V&P does not log, warn, or throw on empty returns. Callers MUST handle the empty case; no implicit whitelist fallback exists.

---

**R13 — Silhouette & Archetype Art Contract (retrofit 2026-04-24 — canonical owners cross-referenced)**

Enemy System and L&R own the canonical enum declarations for silhouette-readability (Pillar 3) and archetype-family grouping, per the BLOCKER-3 arbitration (2026-04-24). V&P consumes — never redefines — these types. The three-axis split is:

| Enum | Canonical owner | Axis | Canonical set | V&P usage |
|---|---|---|---|---|
| `SilhouetteClass` | **Enemy System C.2** | art / size | `{Small, Medium, Large, Boss}` | Imported on `ChassisDefinitionSO.SilhouetteClass` for player-chassis silhouette-grid compliance |
| `VisualFamily` | **Enemy System C.2** | art / family | `{Raider, Scavenger, Elite, Boss}` | Not on `ChassisDefinitionSO`. Enemy-only field on `EnemyDefinitionSO`; player chassis use `ChassisType` (`Scout \| Assault \| HeavyTruck`) for player-facing identity |
| `ArchetypeFamily` | **Loot & Reward C.3.1** | gameplay / loot | `{Raider, Patcher, PitPacker, Elite, Boss}` | Not on `ChassisDefinitionSO`. Enemy-only field on `EnemyDefinitionSO`; keyed by L&R's reward-table lookup `(BiomeIndex, BeaconType, ArchetypeFamily)` |

**Fields on `ChassisDefinitionSO` (player vehicles):**
- `SilhouetteClass: enum` — imported from Enemy System C.2. Drives art-direction silhouette-grid compliance (see Visual/Audio Requirements). No gameplay effect.

V&P does not declare `SilhouetteClass`, `VisualFamily`, or `ArchetypeFamily`. Any change to the canonical value sets requires a GDD amendment in the owning document (Enemy System for `SilhouetteClass` / `VisualFamily`; L&R for `ArchetypeFamily`); C# `using` references propagate the change to V&P automatically. Enemy System and L&R are the sole amendment surfaces.

Art-contract invariants for player chassis (Pillar 3 — Read to Win):
- Every `SilhouetteClass` must be distinguishable from every other at 25% screen-width distance (playtest gate, Visual/Audio).
- Boss-tier player chassis (post-MVP) would use the `Boss` silhouette class.

### States and Transitions

**State machine per slot:**

| State | Condition | Deck Contribution | Stat Contribution | Targetable |
|---|---|---|---|---|
| `Empty` | `InstalledPart == null` | None | None | No |
| `Functional` | `Hp > DegradedThreshold` | Granted cards in deck | Yes | Yes |
| `Degraded` | `0 < Hp <= DegradedThreshold` | Granted cards in deck | Yes | Yes |
| `Offline` | `Hp == 0 && InstalledPart != null` | **Granted cards soft-disabled** (remain in deck/hand/discard; unplayable via source-slot playability gate per ADR-0007 Decision 16) | None | Yes (Repair only) |

---

**Transition table (per slot):**

| From | To | Trigger | Side Effects |
|---|---|---|---|
| `Empty` | `Functional` | `InstallPart(slotId, part)` (R5) | Granted cards added to deck; `Hp = MaxHp`; `PlatingStacks = 0`; `OnPartInstalled` fires |
| `Functional` | `Degraded` | `ApplyDamage` drops `Hp` to `<= DegradedThreshold` | `OnSlotDamageStateChanged(slotId, Functional, Degraded)` fires; visual MPB update per ADR-0001 |
| `Degraded` | `Functional` | `Repair` raises `Hp` above `DegradedThreshold` | `OnSlotDamageStateChanged(slotId, Degraded, Functional)` fires |
| `Functional` | `Offline` | `ApplyDamage` drops `Hp` to `0` (big hit skips Degraded) | Granted cards transition to **soft-disabled** state (remain in deck/hand/discard; unplayable via source-slot playability gate per ADR-0007 Decision 16 — `OnGrantedCardRemoved` does NOT fire on Offline); if `IsStructural`: `StructuralHp` recomputed → vehicle death check; `OnSlotDamageStateChanged(slotId, Functional, Offline)` fires (HUD greys cards via this event); if `Kind == Armor`: `OnArmorExposed(slotId, RedirectsToSlotId)` fires |
| `Degraded` | `Offline` | `ApplyDamage` drops `Hp` to `0` | Same as Functional→Offline |
| `Offline` | `Degraded` | `Repair` with `canReviveOffline = true` raises `Hp` above `0` but `<= DegradedThreshold` | Granted-card **soft-disable lifted** — cards remain in whatever zone they were in before Offline (no re-draw, no discard re-add, no shuffle perturbation per ADR-0007 Decision 16); HUD un-greys via `OnSlotDamageStateChanged(slotId, Offline, Degraded)` |
| `Offline` | `Functional` | Not reachable in one step. Requires Offline→Degraded revival first, then further Repair to cross threshold. | — |
| Any | `Empty` | `RemovePart(slotId)` (R6) | Granted cards removed from all zones; `Hp = 0`; `PlatingStacks = 0`; `OnPartRemoved` fires |

---

**Resolution order on a state-crossing damage event**

When a single `ApplyDamage` call crosses a state boundary (e.g., Functional → Offline via one hit):

1. Per F-VP2: route by `slot.Kind`. Armor slots run the exposure pipeline (R_ARM.2). Non-Armor slots subtract `PlatingStacks` first, then apply remainder to `Hp`.
2. `Hp = max(0, Hp - damage_after_shield)`.
3. Recompute `DamageState` from the new `Hp`.
4. Fire `OnSlotHpChanged(slotId, slot.Hp, slot.MaxHp)` — every Hp change, regardless of state change.
5. If the state changed: apply the side effects for the TERMINAL state only (skip intermediate). A Functional → Offline hit fires one `OnSlotDamageStateChanged(slotId, Functional, Offline)` event, not two. Granted cards on this slot transition to soft-disabled once (per ADR-0007 Decision 16 — no `OnGrantedCardRemoved` on Offline; HUD-greying is driven solely by the single `OnSlotDamageStateChanged` event).
6. If the new state is `Offline` and `Kind == Armor`: fire `OnArmorExposed(slotId, RedirectsToSlotId)` for HUD binding.
7. If the new state is `Offline` and the slot is `IsStructural`: recompute `vehicle.StructuralHp`; if it equals 0 AND `vehicle.IsDead` was previously `false`, set the `IsDead` backing field to `true`, then fire `OnVehicleDied()` immediately after the write. Card Combat's defeat-sequence hook subscribes to `OnVehicleDied`.

See EC-VP20 for the canonical step-numbered table and subscriber rules.

---

**Plating / Armor absorption rule**

Plating stacks absorb incoming damage 1-for-1 before HP takes any, on `Weapon`, `Engine`, `Mobility`, and `Hull` slots. There is no "armor reduction" multiplier elsewhere. Corroded's `Stacks` bonus (Status Effects F1) is added to the incoming damage **before** absorption — Corrode makes the attack bigger, then Plating (or Armor exposure routing, for Armor slots) eats what it can, then the remainder hits `Hp`.

**Damage path fork by target slot Kind:** When the target is an `Armor` slot, the **Armor exposure mechanic** (R_ARM) replaces Plating in the absorption step. The Armor slot has no `PlatingStacks`; its own `Hp` is the shield, and once destroyed, the slot routes amplified damage to its `RedirectsTo` target. When the target is `Hull` or any non-Armor slot (Weapon, Engine, Mobility), `PlatingStacks` on that slot absorbs as before; no Armor slot is involved. The vehicle-level `MaxArmor` / `CurrentArmor` pool is removed — Armor is now a per-slot concept only. This fork is specified precisely in F-VP2.

**DOT bypass rule:** Damage-over-time effects (Burning and any future DOT) **bypass both Plating and the Armor exposure mechanic**. DOT ticks land directly on the slot they are attached to — this is an internal threat, not an external hit. DOT on an `Armor` slot ticks the Armor slot's own `Hp` (no redirect even if `Hp == 0`). See Status Effects GDD for DOT ordering.

*Forward dependency on Card Combat GDD: the full damage resolution order (card effects, position bonuses, Plating/Armor exposure, status bonuses, final application) must be specified there. This GDD locks only the Plating-before-Hp rule, the Kind-based damage fork, the Armor exposure routing, and the Corrode-before-absorption ordering. (T-4 of the armor-model stress-test requires Card Combat to write the Armor and non-Armor paths explicitly in F-CC1 under the new model.)*

---

**Vehicle death event**

When any `IsStructural` slot transitions to `Offline`:

1. All other slot transitions in the same resolution frame still fire normally.
2. `vehicle.StructuralHp` is recomputed inline (sum of `Hp` across `IsStructural` slots).
3. If `StructuralHp == 0`, `IsDead` becomes true.
4. Card Combat observes `IsDead` at a defined end-of-damage-step hook and initiates the defeat sequence.

For the common single-Hull layout, this collapses to "Hull goes Offline → vehicle dies." Multi-structural layouts (a future design choice) require all structural slots to reach `Hp == 0` for death. This GDD does NOT define the defeat sequence — no animation timing, no UI transition, no save-state handling. Those belong to Card Combat and Combat HUD GDDs respectively.

### Emotional Attachment Mechanic (P4)

Per systems-index Scoping Note P4, this GDD names exactly what creates emotional attachment to a specific installed part. Three mechanisms, all game-visible:

**M1 — Visible damage on the chassis silhouette**
The part a player chooses to protect is visibly degrading on the vehicle silhouette throughout combat. Per ADR-0001, the three damage states (Functional / Degraded / Offline) are rendered on the part's sprite via MaterialPropertyBlock. The player watches their part break. Memory is visual, not tracked. *Pillar 1 hook: the hole in the silhouette IS the emotional event.*

**M2 — Part-granted cards (R7)**
A part's identity is its cards. Scrap an Engine, lose the Maneuver cards it brought. Let an Engine go Offline in combat, lose those cards until you revive it. The deck is the record of what's installed; deck loss is felt immediately in the next hand drawn. Players remember parts by the cards they enable, not by flavor text. *Pillar 1 hook: loss is immediate and mechanical, not sentimental.*

**M3 — Part scarcity**
Parts do not drop on a predictable schedule. The Loot & Reward GDD owns the specific drop rates, but this GDD asserts the design constraint: **the next replacement for a given slot type must not be guaranteed within any fixed node count.** A run may go 5+ combats without a new Engine. Loss aversion does the remaining emotional work. *Pillar 1 hook: if replacement is easy, attachment is cheap.*

**Design test (for Pillar 1 gate review):**

The mechanic passes Pillar 1 if a reviewer can answer "yes" to both:
1. Does losing a Rare/Legendary part to Offline produce a visible deck change the player must adapt to on the next turn? (Tests M2.)
2. Can a player go 3+ combats with a damaged but unreplaced part because the replacement didn't drop? (Tests M3.)

The mechanic fails if either "no" — and if it fails, the three mechanisms above must be revisited before authoring parts or enemies.

**Anti-features (explicitly out of scope):**

- No per-part acquisition timestamps
- No player-surfaced "combats survived" display per part (the `InstalledPart.CombatsSurvived` counter defined in R11 is an internal data field consumed by Scrap Economy D.6 tenure-refund and Save serialization; it is never rendered in-game per Slay-the-Spire-level readability)
- No repair-history tracking
- No part-specific flavor/lore text in EA (deferred to post-launch)
- No per-part nickname / personalization (post-EA consideration)

These were considered and rejected in favor of Slay the Spire-level readability. The vehicle is a record of choices *made visible through current state* — not a log of past events.

### Chassis Architecture (B2)

This sub-section closes systems-index Scoping Note B2: *three-chassis architecture, two-chassis content for EA*.

---

This sub-section specifies the chassis architecture under the Frame-Driven Variable Slot System. `ChassisDefinitionSO` no longer owns slot layout — layout is owned by `FrameLayoutSO` (see R_FL). `ChassisDefinitionSO` retains chassis identity, defaults, and player-vehicle-specific fields.

---

**B2.1 — ChassisDefinitionSO Contract (revised)**

```
ChassisDefinitionSO:
  ChassisType           (enum: Scout | Assault | HeavyTruck)
  DisplayName           (string)
  FrameLayoutId         (string — references a FrameLayoutSO asset by LayoutId)
  DefaultSlotMaxHp      (Dictionary<SlotKind, int>)
                         — per-Kind default HP, used for any SlotDefinition in the layout
                           where MaxHpOverride is null. Required entries: Hull, Weapon,
                           Engine, Mobility. Armor Kind default is optional (Armor slots
                           typically use MaxHpOverride per their bespoke balance).
  DegradedThresholdPct  (int, 1..100 — default 50; applied to all slots in the layout)
  StarterParts          (Dictionary<SlotId, PartDefinitionSO>)
                         — keyed by SlotId matching the layout. One entry per slot
                           that has a starter part. Armor slots do NOT have starter
                           parts (the Armor slot IS the plate; see authoring note below).
  StarterDeck           (CardDefinitionSO[] — exactly 10 cards per Card System GDD)
  PrimaryFamilies       (CardFamily[] — 1..2)
  MaxEnergyBase         (int, default 3)
  MaxHandSizeBase       (int, default 5)
  SpriteBundle          (AssetReferenceT<ChassisArtBundle>)
  SilhouetteClass       (SilhouetteClass — imported from Enemy System C.2; no gameplay effect)
```

**`StarterParts` authoring note for Armor slots:** Armor slots represent a structural component of the vehicle (the physical armor plate), not an interchangeable part. Enemy Armor slots are pre-filled by the enemy's authoring entry for that SlotId — the designer authors a specific `PartDefinitionSO` of `Kind = Armor` for the plate. Player vehicles in EA have no Armor slots (Small Frame has none). The Armor `PartDefinitionSO` is authored normally: `Kind = Armor`, `GrantedCards = []` (Common, forced at import), `ArmorContribution` is unused (Armor slot HP IS the protection).

**`ArmorContribution` deprecation:** Under the new system, there is no vehicle-level `MaxArmor` / `CurrentArmor` pool. The old `ArmorContribution` field on `PartDefinitionSO` is deprecated (see R4). Parts that previously carried `ArmorContribution > 0` will be re-evaluated in the balance pass — their protection role may be expressed through Armor slots added to future layouts instead.

**SO import validation (revised):**

- `FrameLayoutId` must resolve to an existing `FrameLayoutSO` asset.
- `DefaultSlotMaxHp` must contain entries for `Hull`, `Weapon`, `Engine`, and `Mobility` at minimum.
- `StarterParts` keys must be valid `SlotId` values from the referenced layout.
- Each `StarterParts[slotId].Kind` must match the `SlotDefinition.Kind` for that `SlotId`.
- `StarterParts[slotId].CompatibleChassis` must include this chassis (player chassis only).
- `StarterDeck.Length == 10`.
- `PrimaryFamilies.Length` in `[1..2]`.
- Sum of `StarterParts[*].GrantedCards.Count` MUST NOT exceed 4.

---

**B2.2 — Scout Chassis (EA) — Small Frame**

Scout references `FrameLayoutId = "small_frame"`. Layout defined in R_FL.2.

**`DefaultSlotMaxHp`:**

| SlotKind | DefaultMaxHp |
|---|---|
| Weapon | 10 |
| Engine | 12 |
| Mobility | 12 |
| Hull | 16 |

Resolved per-slot HP at construction:

| SlotId | Kind | MaxHpOverride | Resolved MaxHp |
|---|---|---|---|
| `weapon_front` | Weapon | 10 | 10 |
| `weapon_back` | Weapon | 10 | 10 |
| `engine_0` | Engine | 12 | 12 |
| `mobility_0` | Mobility | 12 | 12 |
| `hull_0` | Hull | 16 | 16 |

Total structural HP: 16 (`hull_0` only). Total slot HP pool: 62.

- `DegradedThresholdPct`: 50 (default)
- `PrimaryFamilies`: `[Precision, Maneuver]`
- `MaxEnergyBase`: 3
- `MaxHandSizeBase`: 5

**Starter parts** (SlotId → part role):

| SlotId | Starter Part Role |
|---|---|
| `weapon_front` | Light Autocannon — Precision family |
| `weapon_back` | Smoke Launcher — Maneuver family (placeholder; exact card in Card System GDD) |
| `engine_0` | Tuned V6 — grants Maneuver cards |
| `mobility_0` | Off-Road Tires — enables evasion |
| `hull_0` | Alloy Frame — structural plate |

Design intent: Glass-cannon. Two weapons (front + back) enable dual-position offense. Hull HP = 16 means 2–3 unshielded hits threaten death. Fastest archetype; wins by tempo through Maneuver + Precision.

---

**B2.3 — Assault Chassis (Post-EA, Architecture-Only) — Medium Frame**

Assault references `FrameLayoutId = "medium_frame"` (post-EA asset). `ChassisType.Assault` is reserved in the enum. Assault chassis definition is architecture-only for EA; it slots in post-EA as one new `ChassisDefinitionSO` + one `FrameLayoutSO` ("medium_frame") + new starter parts and deck. No code changes required.

---

**B2.4 — Heavy Truck (Post-EA, Architecture-Only) — Heavy Frame**

`HeavyTruck` is reserved in the `ChassisType` enum. No `ChassisDefinitionSO` or `FrameLayoutSO` authored for EA. Post-EA addition requires: one `FrameLayoutSO` ("heavy_frame"), one `ChassisDefinitionSO`, new starter parts (7 slots), new starter deck. No code changes.

---

**B2.5 — Mastery Track Hook (Forward Dep, Deferred)**

Each chassis will eventually have a meta-progression mastery track (Meta Progression System GDD, not yet authored). `ChassisDefinitionSO` will grow a `MasteryUnlocks` field post-MVP. No hook added to the SO contract now — deferring prevents premature schema lock. Tracked as **OQ-VP1**.

---

**Note on HP values**: the numbers in B2.2 are explicit carries from the prior design (weapon 10 / engine 12 / mobility 12 / hull 16 for Scout). They bracket the Status Effects F2 ~20HP baseline assumption. Re-tuning after first Unity combat playtest is expected. Locked as tuning knobs in Section 7.

### Interactions with Other Systems

Every system that reads or writes vehicle state is listed below, with contract direction (who owns what).

---

**I1 — Card Combat System** *(downstream consumer + mutator)*

- **Reads**: `IVehicleView.Slots`, `.ActiveStatuses`, `.IsDead`, `.GetStatModifier(stat)` for damage calculation, targeting, and UI.
- **Writes** (via `IVehicleMutator`): `ApplyDamage`, `Repair`, `AddPlating`, `ApplyStatus`.
- **Observes events**: `OnSlotDamageStateChanged` (triggers VFX/SFX), `OnStatusApplied` / `OnStatusExpired` (HUD update), `IsDead` at end-of-damage-step hook (triggers defeat sequence).
- **Does not own**: slot taxonomy, damage state machine, plating-before-Hp order. These are locked here.

**I2 — Status Effects System** *(downstream consumer + mutator)*

- **Reads**: `.Slots` (for Corroded per-slot targeting), `.ActiveStatuses` (tick pipeline source).
- **Writes**: `ApplyDamage` (Burn); Corroded's bonus routes through Card Combat's damage pipeline. Status application itself is `ApplyStatus`.
- **Contract**: Vehicle POCO owns `ActiveStatuses: List<StatusInstance>`. Status Effects GDD owns tick order, merge rules, and per-type behavior. This GDD owns the storage.
- **Closed OQ**: Status Effects OQ-SE2 (`DamageSource` discriminator) closed by R9's enum.

**I3 — Scrap Economy** *(exclusive mutator for Install/Remove)*

- **Writes**: `InstallPart`, `RemovePart` — the ONLY system authorized to call these.
- **Reads**: `.Slots` to determine which slots are valid install targets, and the current installed part (for replace-scrap pricing).
- **Contract**: Vehicle & Part defines `InstallPart` / `RemovePart` semantics (R5, R6). Scrap Economy defines Chopshop flow, pricing, and the auto-scrap-on-replace UX.

**I4 — Loot & Reward** *(reads; mutates via Scrap Economy)*

- **Reads**: `.Slots` to determine slot-force bias (empty or Offline slots bias part drops, mirroring Card System pity logic).
- **Writes**: indirectly — post-combat part choices accepted by the player route through Scrap Economy's `InstallPart`.
- **Contract**: Loot does not mutate vehicle state directly. Part drops are offers; acceptance triggers `InstallPart`.

**I5 — Card System** *(reads; indirect deck mutation)*

- **Reads**: `.Slots` for deck-composition queries (what's granted by currently installed parts).
- **Writes**: deck mutation on install/scrap is performed by Vehicle & Part per R5 step 5 and R6 step 1 — Card System's Deck model is mutated via its own public API, called from this system.
- **Forward dep**: Card System GDD currently does NOT document part-granted cards as a deck source. Propagate via `/propagate-design-change` or during Card Combat GDD authoring.

**I6 — Save & Persistence** *(serializer)*

- **Reads**: Vehicle POCO is a leaf field on `RunState`. Serialized and restored as-is.
- **Writes**: on load, reconstructs the Vehicle POCO.
- **Contract**: Vehicle & Part guarantees the POCO is fully serializable (no non-serializable references, no event subscribers persisted). Save & Persistence guarantees round-trip fidelity.

**I7 — Visual Vehicle Part System (ADR-0001)** *(view-layer consumer)*

- **Reads** (via `IVehicleView`): `.Slots[*].DamageState`, `.Slots[*].InstalledPart.SpriteKey`, status flags for FX.
- **Writes**: none. View is read-only.
- **Contract**: ADR-0001 is authoritative for rendering. This GDD specifies only what state is exposed, not how it's drawn.

**I8 — Enemy AI** *(mutator for status application + damage)*

- **Writes**: `ApplyStatus` (enemy cards apply statuses to the player vehicle), `ApplyDamage` (enemy attacks).
- **Reads**: `.Slots`, `.ActiveStatuses` for targeting decisions.
- **Contract**: Enemy AI routes all effects through `IVehicleMutator` identically to player cards. No privileged access.

---

**Interaction rules:**

1. Every non-mutator system accesses vehicle state **only** through `IVehicleView`. No system casts to the concrete Vehicle POCO.
2. Mutator access is restricted to the systems listed in R9. Reviewers MUST fail code that introduces a new `IVehicleMutator` caller without a GDD update.
3. No system subscribes to or fires events not declared in `IVehicleView`. New events require an edit to R9.

## Formulas

The Vehicle & Part System authors a narrow set of formulas; most combat math lives in Card Combat. Only formulas this system owns are specified below.

---

**F-VP1 — DegradedThreshold**

```
DegradedThreshold(slot) = max(1, floor(frameLayout.DegradedThresholdPct × slot.MaxHp / 100))
```

**Variables:**
- `frameLayout.DegradedThresholdPct` — 1..100, per-layout tuning knob (default 50). Declared on `IFrameLayout` (R_FL.1), authored on `FrameLayoutSO.DegradedThresholdPct`. Applied uniformly to all slots in the layout. (R5 blocker #9 fix 2026-05-18: the formula was previously notated `ChassisDef.DegradedThresholdPct`, which contradicted R_FL.1's layout-owned declaration and the line-113 ownership statement. The legacy `ChassisDefinitionSO.DegradedThreshold%` field is deprecated and ignored when a layout value is set — see Tuning Knobs.)
- `slot.MaxHp` — resolved per-slot from `SlotDefinition.MaxHpOverride ?? ChassisDef.DefaultSlotMaxHp[slot.Kind]` (B2.1, R_FL.1).

**`max(1, ...)` clamp (closes Review 2 blocker #5):** the floor produces `0` whenever `pct × MaxHp < 100` — for example, `pct = 50, MaxHp = 1` yields `floor(0.5) = 0`. Without the clamp, the Degraded band is unreachable: `Hp > 0` means `Hp > DegradedThreshold` (Functional), and `Hp == 0` means Offline. The slot silently jumps Functional → Offline with no Degraded warning, and AC-VP22 (Functional→Degraded event firing) becomes untestable for low-MaxHp slots. The `max(1, ...)` clamp guarantees the Degraded band always covers at least `Hp == 1`. (Cost: at very low `MaxHp` the Degraded band is narrow, but the warning fires at least once before Offline.)

**Examples:**

| Chassis | SlotId | Kind | MaxHp | ThresholdPct | DegradedThreshold |
|---|---|---|---|---|---|
| Scout | `hull_0` | Hull | 16 | 50 | 8 |
| Scout | `weapon_front` | Weapon | 10 | 50 | 5 |
| Scout | `engine_0` | Engine | 12 | 50 | 6 |
| Hypothetical | `tiny_slot` | Weapon | 1 | 50 | **1** *(clamped from floor=0)* |
| Hypothetical | `small_slot` | Weapon | 3 | 30 | **1** *(clamped from floor=0)* |

**Design target:** At default 50%, players spend roughly as much time in Degraded as in Functional for a slowly-eroding slot, keeping the visual/audio warning informative. Lowering to 30% would make Degraded feel like a last-gasp state; raising to 70% would make Functional brief.

---

**F-VP2 — Damage Application (Kind-dependent)**

The full damage pipeline for `ApplyDamage(slotId, amount, source)`:

```
[Step 0 — resolve slot + capture pre-state]
  slot = GetSlot(slotId)
  if slot is Empty: no-op, return.
  wasCritical = vehicle.CriticalState                                                 // (Step-0 pre-snapshot) — captured BEFORE Step 2 shield absorption
                                                                                       // and BEFORE any recursion (Armor → redirect amplifies via ApplyDamage),
                                                                                       // so Step 4(h) compares against the state at THIS call's entry, not the
                                                                                       // post-recursion state. Each recursive ApplyDamage captures its own
                                                                                       // wasCritical for its own Step 4(h). Closes Review 4 Blocker #1.
  if source is DOT: skip Steps 1–2 routing; apply amount directly to slot.Hp (Step 3).

[Step 1 — Corrode applied by caller]
  Card Combat computes `amount` with Corrode bonus before calling ApplyDamage:
    amount += CorrodedStacks on slot  (Status Effects F1 — applied BEFORE this function)

[Step 2 — shield absorption by Kind]

  if slot.Kind == Armor:
    // Armor exposure mechanic — see R_ARM.2 for full pseudocode (incl. SafeAmplify definition)
    [Intact: slot.Hp > 0]
      armor_consumed = min(amount, slot.Hp)
      slot.Hp = slot.Hp - armor_consumed
      overflow = amount - armor_consumed
      if overflow > 0:
        redirect = GetSlot(slot.RedirectsToSlotId)
        ApplyDamage(redirect, SafeAmplify(overflow, slot.ExposureMultiplier), source) // recursive call captures its OWN wasCritical snapshot per Decision 15
      // FALL THROUGH to Step 3/4 on the armor slot itself — armor INTACT absorption is an Hp delta
      // and (per ADR-0007 Decision 14) MUST emit OnSlotHpChanged + OnSlotDamageStateChanged for any
      // band crossing (e.g., Functional → Degraded) on the armor slot. Closes R5 blocker #1.

    [Exposed: slot.Hp == 0 && InstalledPart != null]
      ApplyDamage(GetSlot(slot.RedirectsToSlotId), SafeAmplify(amount, slot.ExposureMultiplier), source)
      return  // Armor slot self took no Hp delta this hit → no Step 3/4 firing on the armor slot

  else:
    // Hull / Weapon / Engine / Mobility — PlatingStacks absorbs 1-for-1
    plating_consumed = min(amount, slot.PlatingStacks)
    damage_after_shield = amount - plating_consumed
    slot.PlatingStacks = slot.PlatingStacks - plating_consumed
    slot.Hp = max(0, slot.Hp - damage_after_shield)

[Step 3 — state recomputation]
  Recompute slot.DamageState from new slot.Hp.

[Step 4 — atomic event ordering — see Canonical Event Order Table below]
  // Idempotency: OnCriticalStateChanged fires at most once per top-level ApplyDamage entrypoint
  // even when recursion would otherwise produce multiple eligible (h) firings (ADR-0007 Decision 15).
  // The shared DamageContext tracks ctx.CriticalEventFiredThisCall across recursive worker calls.

  Fire OnSlotHpChanged(slotId, slot.Hp, slot.MaxHp).                                  // (a) every Hp change, regardless of state change

  If DamageState changed:
    Fire OnSlotDamageStateChanged(slotId, fromState, toState) — terminal state only.  // (b)

    If new state == Offline:
      If slot.Kind == Armor: fire OnArmorExposed(slotId, RedirectsToSlotId).          // (c)
      // (d) Granted-card removal on Offline is a NO-OP per ADR-0007 Decision 16.
      //     Deck/hand/discard composition is unchanged. Cards become unplayable via the
      //     source-slot-state playability gate (the OnSlotDamageStateChanged event above
      //     drives HUD greying via the same gate that drives positional-requirement greying).
      //     Repair revival (Decision 12) restores playability automatically.
      //     OnGrantedCardRemoved does NOT fire on Offline — see "Hard Removal Pathway"
      //     below for the two triggers (scrap, external-source termination) that DO fire it.
      If slot.IsStructural:
        Recompute vehicle.StructuralHp.                                               // (e) StructuralHp recompute
        wasDead = vehicle.IsDead                                                      // snapshot before mutation
        vehicle.IsDead = (StructuralHp == 0)                                          // (f) backing-field write
        if !wasDead && vehicle.IsDead:
          Fire OnVehicleDied().                                                       // (g) AFTER IsDead is set true

  // (h) — CriticalState transition fires last, after the death event resolves.
  // wasCritical was captured at Step 0 of THIS invocation; recursive invocations each
  // captured their own wasCritical for their own Step 4(h) (Decision 15 snapshot discipline).
  if vehicle.CriticalState != wasCritical AND !ctx.CriticalEventFiredThisCall:
    Fire OnCriticalStateChanged(vehicle.CriticalState).                               // (h) HUD vignette / audio heartbeat driver
    ctx.CriticalEventFiredThisCall = true

[Step 5 — defeat sequence handoff]
  // Card Combat owns the defeat sequence. Its end-of-damage-step hook subscribes to
  // OnVehicleDied and starts the defeat animation/score/loot pipeline AFTER the current
  // card finishes resolving. Subscribers MUST NOT read IsDead from inside Step 4
  // events other than OnVehicleDied itself — they are guaranteed to see the post-write
  // value only inside or after OnVehicleDied.
```

**Repair path emission (per ADR-0007 Decision 12 — closes R5 blocker #3):**

`IVehicleMutator.Repair(slotId, hpRestored, canReviveOffline)` is the symmetric counterpart to the damage pipeline and emits the same per-delta event contract. After the Hp mutation commits, the repair fires:

1. `OnSlotHpChanged(slotId, slot.Hp, slot.MaxHp)` — on every Hp delta, including Offline → Functional revival when `canReviveOffline == true`.
2. `OnSlotDamageStateChanged(slotId, fromState, toState)` — only when the repair crosses a band boundary upward (Offline → Functional/Degraded, Degraded → Functional).
3. `OnCriticalStateChanged(false)` — only when the repair removes the last structural slot from Degraded/Offline (i.e., `CriticalState` flips `true → false`). Uses the same `wasCritical` snapshot discipline as Decision 15 — the `Repair` entrypoint captures `wasCritical = vehicle.CriticalState` before the Hp write and compares after.

`IsDead` is NOT reversed by repair. A structural slot revived from Offline does not unset `IsDead`; death is a once-per-combat terminal state per the Ordering contract item 7. If a combat is post-death, `Repair` is undefined behavior at the call site (developer-build assertion recommended).

**Canonical Event Order Table (Step 4 atomic ordering — single source of truth):**

This table is the authoritative event-firing order. ADR-0007 Decision 11 mirrors the *invariants* (see Ordering contract below); ADR-0007 Decision 13 establishes CI lock-step gating against drift; `design/ux/combat-hud.md` references this table without duplicating it. Closes Review 4 Blocker #1 and R5 blocker #4.

| Step | Event / Action | Condition | Notes |
|---|---|---|---|
| Step 0 | `wasCritical = vehicle.CriticalState` (capture, no event) | Always | Per-invocation snapshot; each recursive `ApplyDamage` (incl. Armor → redirect breakthrough) captures its OWN snapshot per ADR-0007 Decision 15 |
| (a) | `OnSlotHpChanged(slotId, newHp, maxHp)` | Always (every Hp change, damage OR repair) | Repair path also fires this — ADR-0007 Decision 12 |
| (b) | `OnSlotDamageStateChanged(slotId, fromState, toState)` | Only if `DamageState` changed (damage OR repair-upward boundary cross) | Idempotent on identical-state writes (Degraded→Degraded does NOT fire). On **Armor INTACT** branch, fires on the armor slot itself when absorption crosses Degraded — ADR-0007 Decision 14, closes R5 blocker #1 |
| (c) | `OnArmorExposed(armorSlotId, redirectsToSlotId)` | Only if new state == Offline AND `slot.Kind == Armor` | Fires once per Armor Hp→0 transition |
| (d) | **No-op on Offline** — deck/hand/discard composition is UNCHANGED. Cards greyed via (a)+(b) source-slot-state playability gate. | Pre-Phase-1 spec removed cards on Offline; superseded by ADR-0007 Decision 16 (soft-disable lifecycle) | R5 blocker rewrite — see "Hard Removal Pathway" section below for the two triggers that DO mutate deck composition + fire `OnGrantedCardRemoved` |
| (d′) | `OnGrantedCardRemoved` — **does NOT fire on Offline** per Decision 16 | Fires only in the Hard Removal Pathway (scrap or external-source termination) — see that section for the canonical trigger contract | Signature: `Action<string?, IReadOnlyList<string>>` — `sourceSlotId` is nullable for external-source removals (Javelin cohort) |
| (e) | Recompute `vehicle.StructuralHp` (action, no event) | Only if new state == Offline AND `slot.IsStructural` | O(N) over structural slots; typically N=1 |
| (f) | `vehicle.IsDead = (StructuralHp == 0)` (backing-field write) | Only if (e) ran AND `!wasDead` | Atomic with (g); private setter |
| (g) | `OnVehicleDied()` | Only if (f) transitioned `false`→`true` | Exactly once per combat — terminal; not reversed by repair |
| (h) | `OnCriticalStateChanged(vehicle.CriticalState)` | Only if `vehicle.CriticalState != wasCritical` AND `!ctx.CriticalEventFiredThisCall` | At-most-once per top-level entrypoint per ADR-0007 Decision 15. Fires AFTER (g) so death-frame subscribers can suppress via `IsDead` check. Repair path also fires this on `true→false` transitions — Decision 12 |

**Subscriber rules:**
1. Subscribers reading `IsDead` from inside any handler other than `OnVehicleDied` see the *pre-write* value (`false`) — the backing-field write at (f) is sequenced after all other handlers return.
2. Step 4 is reentrant via Armor → redirect recursion. Inner recursive calls run their own complete Step 4 (with their own `wasCritical`) before the outer call resumes its Step 4. The shared `DamageContext.CriticalEventFiredThisCall` flag enforces at-most-once `OnCriticalStateChanged` per top-level entrypoint. Subscribers MUST NOT mutate vehicle state from inside Step 4 handlers — read-only inspection only.
3. (h) fires AFTER (g) by design so subscribers that want to suppress critical-state UI on the death frame check `IsDead` in their `OnCriticalStateChanged` handler.
4. Granted cards on a slot that goes Offline remain in their current zone (deck/hand/discard). They become unplayable via the source-slot-state playability gate (ADR-0007 Decision 16). Subscribers that want to grey them subscribe to `OnSlotDamageStateChanged` at (b) — there is no dedicated card-greying event because the slot-state event already drives it.

**Variables:**

| Symbol | Type | Range | Description |
|---|---|---|---|
| `amount` | int | ≥ 0 | Incoming damage pre-computed by Card Combat (post-Corrode, post-position-bonus) |
| `slot.PlatingStacks` | int | 0..MaxPlating | Non-Armor slots only; absorbs damage 1-for-1 |
| `slot.ExposureMultiplier` | float | > 0.0 | Armor slots only; amplifies overflow/redirect damage |
| `slot.Hp` | int | 0..MaxHp | Any slot |
| `redirected_amount` | int | ≥ 0 | `floor(amount × ExposureMultiplier)` — delivered to `RedirectsTo` slot |
| `vehicle.StructuralHp` | int | 0..sum of structural-slot MaxHp | Recomputed after any structural slot goes Offline |

**Output range:** `slot.Hp` floored at 0, capped at `MaxHp`. `redirected_amount` floored to int, unbounded above (exposure amplification is intentionally punishing).

**Ordering contract (locked):**

1. DOT bypasses all shields (Plating and Armor exposure mechanic) — DOT lands directly on the slot it lives on.
2. Corrode bonus is applied by Card Combat before calling `ApplyDamage` (amount is pre-computed with Corrode included).
3. Armor slots route through the exposure mechanic (R_ARM.2) before any Hp subtraction on the redirect target.
4. Hull and non-Hull non-Armor slots use `PlatingStacks` only. No vehicle-level Armor layer remains.
5. `DamageState` is re-derived after each `ApplyDamage` call (not batched across a multi-slot AoE turn).
6. `StructuralHp` is recomputed immediately when a structural slot transitions to Offline, in the same step as event firing.
7. `IsDead` is a backing field, not a derived getter (see R9 + F-VP4). It is set inside F-VP2 Step 4(f); `OnVehicleDied` fires in Step 4(g) AFTER the field is written. Subscribers reading `IsDead` from inside any other Step 4 event (e.g., `OnSlotDamageStateChanged`) are guaranteed to see the *pre-write* value (still `false`) until `OnVehicleDied` fires. (Closes Review 2 blocker #4.)
8. `OnSlotHpChanged` fires on **every** Hp change, even when `DamageState` does not transition (e.g., Functional → Functional with Hp drop). HUD bindings that need continuous Hp tracking subscribe here; bindings that only react to band changes use `OnSlotDamageStateChanged`. (Closes Review 2 blocker #2 — event was referenced by EC-VP20 but not declared.)

**Worked examples:**

*Scout `weapon_front` (Hp=10, PlatingStacks=0, incoming 7):*

```
Kind = Weapon (non-Armor)
plating_consumed = min(7, 0) = 0
damage_after_shield = 7
weapon_front.Hp = max(0, 10 - 7) = 3  → DamageState = Degraded (threshold = 5)
```

*Scout `hull_0` (Hp=16, PlatingStacks=2, incoming 10):*

```
Kind = Hull
plating_consumed = min(10, 2) = 2; PlatingStacks = 0
damage_after_shield = 8
hull_0.Hp = max(0, 16 - 8) = 8  → DamageState = Degraded (threshold = 8, exactly at threshold)
```

*Dredge `armor_chest` (Hp=8, ExposureMultiplier=3.0, RedirectsTo=hull_0, incoming 12):*

```
Kind = Armor, intact (Hp > 0)
armor_consumed = min(12, 8) = 8; armor_chest.Hp = 0 → Offline → OnArmorExposed fires
overflow = 12 - 8 = 4
redirected = floor(4 × 3.0) = 12
ApplyDamage(hull_0, 12, source)  → Hull pipeline runs (PlatingStacks absorbs first, then Hp)
```

*Dredge `armor_chest` already exposed (Hp=0, ExposureMultiplier=3.0, incoming 5):*

```
Kind = Armor, exposed (Hp == 0, InstalledPart != null)
redirected = floor(5 × 3.0) = 15
ApplyDamage(hull_0, 15, source)
```

*Scout `engine_0` (0 Plating, 13 incoming — Functional → Offline in one hit):*

```
Kind = Engine (non-Armor)
plating_consumed = 0
engine_0.Hp = max(0, 12 - 13) = 0; state = Offline
Step 4(a): OnSlotHpChanged(engine_0, 0, 12).
Step 4(b): OnSlotDamageStateChanged(engine_0, Functional, Offline).
Step 4(d): NO-OP — engine_0's granted cards stay in their zones; they become unplayable
           via source-slot gate. No OnGrantedCardRemoved firing.
Step 4(e–g): engine_0 is not IsStructural → no StructuralHp recompute, no death check.
Step 4(h): wasCritical unchanged (engine is non-structural) → no OnCriticalStateChanged.
```

*Dredge `armor_chest` INTACT absorption crossing Degraded (Decision 14):*

```
Initial: armor_chest.Hp = 10, MaxHp = 12, DegradedThresholdPct = 50 → DegradedThreshold = 6.
         vehicle.CriticalState = false. Incoming 5.
Kind = Armor, intact (Hp > 0)
armor_consumed = min(5, 10) = 5; armor_chest.Hp = 5; overflow = 0 → no recursion
FALL THROUGH to Step 3/4 on armor_chest itself.
Step 3: state recompute → Hp=5 < 6 → Degraded.
Step 4(a): OnSlotHpChanged(armor_chest, 5, 12).
Step 4(b): OnSlotDamageStateChanged(armor_chest, Functional, Degraded).
Step 4(c): no Offline → no OnArmorExposed.
Step 4(d/d′): no Offline AND no hard-removal trigger → no deck mutation, no OnGrantedCardRemoved.
Step 4(e–g): armor is non-structural → no StructuralHp recompute, no death check.
Step 4(h): wasCritical = false; armor non-structural → CriticalState_now = false → no OnCriticalStateChanged.
```

*Armor breakthrough → Hull recursion crosses critical (Decision 15 recursion + idempotency):*

```
Initial: vehicle.CriticalState = false. ctx.CriticalEventFiredThisCall = false.
         armor_chest.Hp = 4, MaxHp = 12.
         hull_0.Hp = 8, MaxHp = 20, DegradedThresholdPct = 50 → DegradedThreshold = 10.
         armor_chest.ExposureMultiplier = 3.0, RedirectsTo = "hull_0".

Top-level call: ApplyDamage("armor_chest", 10, Card)
  Step 0 (outer):  wasCritical_outer = vehicle.CriticalState = false.
  Step 2 (Armor intact branch):
    armor_consumed = min(10, 4) = 4; armor_chest.Hp = 0; overflow = 6.
    RECURSIVE CALL: ApplyDamage("hull_0", floor(6 × 3.0) = 18, Card)
      Step 0 (inner): wasCritical_inner = vehicle.CriticalState = false
                      (armor_chest.Hp=0 committed but armor non-structural — does not affect CriticalState).
      Step 2 (Hull non-Armor branch): plating_consumed = 0; hull_0.Hp = max(0, 8-18) = 0; state → Offline.
      Step 3: recompute → Offline.
      Step 4(a): OnSlotHpChanged(hull_0, 0, 20).
      Step 4(b): OnSlotDamageStateChanged(hull_0, Functional, Offline).
      Step 4(c): hull is not Armor → no OnArmorExposed.
      Step 4(d): NO-OP — hull_0's granted cards remain in deck (greyed via Step 4(b) source-slot gate).
      Step 4(e): hull_0.IsStructural → StructuralHp recompute → 0.
      Step 4(f): IsDead ← true.
      Step 4(g): OnVehicleDied().
      Step 4(h): CriticalState_now = true; wasCritical_inner = false; differ AND ctx.CriticalEventFiredThisCall = false.
                 → Fire OnCriticalStateChanged(true); ctx.CriticalEventFiredThisCall ← true.
      Recursive call returns.
    Step 2 outer call resumes (armor branch fall-through).
  Step 3 (outer): recompute armor_chest state → Hp=0 → Offline.
  Step 4(a): OnSlotHpChanged(armor_chest, 0, 12).
  Step 4(b): OnSlotDamageStateChanged(armor_chest, Functional, Offline).
  Step 4(c): armor → fire OnArmorExposed(armor_chest, hull_0).
  Step 4(d): no-op per Decision 16.
  Step 4(e–g): armor non-structural → IsDead already true from inner; no second OnVehicleDied.
  Step 4(h): CriticalState_now = true; wasCritical_outer = false; differ — BUT ctx.CriticalEventFiredThisCall = true
             → SUPPRESS. No second OnCriticalStateChanged.

Net firing: OnVehicleDied × 1; OnCriticalStateChanged(true) × 1; OnArmorExposed × 1;
            OnSlotDamageStateChanged × 2 (hull, then armor); OnSlotHpChanged × 2.
```

---

**Hard Removal Pathway (per ADR-0007 Decision 16 — V&P-bounded enumeration):**

Slot Offline is a **soft disable** of granted cards (the F-VP2 Step 4(d) no-op above). Cards become unplayable via the source-slot-state playability gate but remain in their current zone (deck/hand/discard). Hard removal — the only path that actually deletes cards from zones and fires `OnGrantedCardRemoved` — is reserved for the two triggers below.

**Mutator surface:**

```csharp
// On IVehicleMutator (canonical declaration in ADR-0007 §Key Interfaces):
void HardRemoveCards(string? sourceSlotId, IReadOnlyList<string> cardIds);
```

**Atomic sweep semantics:** A single `HardRemoveCards` invocation sweeps deck + hand + discard in one frame and removes every instance whose `CardId` matches any entry in `cardIds`. Non-matching cards in those zones are untouched. The currently-resolving card stack is excluded from the sweep per EC-VP12 — a card mid-resolution finishes resolving, then is not returned to any zone. Exactly one `OnGrantedCardRemoved(sourceSlotId, removedCardIds)` event fires after the sweep completes; `removedCardIds` is the subset of `cardIds` that actually existed in zones (callers MUST NOT assume input equals output — a card already played and exhausted is silently dropped from the output payload).

**`sourceSlotId` nullability:** `null` signals an external-source removal where the granting condition lives outside V&P (boss script, status-driven cohort). Subscribers MUST tolerate `null` and rely on the `cardIds` payload as the authoritative removal key. Non-null `sourceSlotId` signals a V&P scrap removal where the source slot is identified.

**Trigger 1 — Scrap (V&P R6 path):** Player removes a part via the scrap UI between combats (Chopshop). V&P R6 invokes `HardRemoveCards(slotId, slot.InstalledPart.GrantedCards.Select(c => c.CardId).ToList())` as part of part-removal. The non-null `sourceSlotId` is the slot being scrapped. This is the only player-initiated hard-removal path in EA. Scrap is not legal mid-combat (R6 caller restriction); mid-combat slot Offline routes through the soft-disable pathway exclusively.

**Trigger 2 — External-source termination (status-driven / boss-script cohort):** The card's granting condition signaled end-of-life from outside V&P. Canonical example: Dredge Javelin Hook adds a tether cohort to the player deck on hit; when the chain is cut (target killed, range exceeded, or any tether card resolves and exhausts the cohort), the originating system (Status Effects or boss script) invokes `HardRemoveCards(null, cohortCardIds)`. The grant path itself is owned by Status Effects / boss-script ADRs and is NOT specified here — this section specifies only the removal path.

**Worked example A — Engine destroyed mid-combat, then repaired (soft disable + revival):**

```
Setup: engine_main holds part with GrantedCards = [{CardId: "nitro_boost"}].
       "nitro_boost" is currently in discard pile.
       Deck: 4 cards. Hand: 3 cards. Discard: 5 cards (one is "nitro_boost").
       vehicle.CriticalState = false.

100-damage Hull hit overflows into engine_main via redirect.
engine_main.Hp → 0; state → Offline.
F-VP2 Step 4(a): OnSlotHpChanged(engine_main, 0, MaxHp).
F-VP2 Step 4(b): OnSlotDamageStateChanged(engine_main, Functional, Offline).
F-VP2 Step 4(d): NO-OP per Decision 16. "nitro_boost" stays in discard.
                  Hand-render code re-evaluates playability for cards-in-hand —
                  none have SourceSlotId == "engine_main", so no greying happens this frame.
                  Discard-pile inspector UI greys "nitro_boost" (its source-slot gate now fails).
                  No OnGrantedCardRemoved firing.

Three turns later, player plays Repair Kit → Repair(engine_main, 8, canReviveOffline=true).
Per Decision 12:
  OnSlotHpChanged(engine_main, 8, MaxHp).
  OnSlotDamageStateChanged(engine_main, Offline, Degraded).
Hand-render code re-evaluates: "nitro_boost" in discard flagged playable again.
Drawn next turn, plays normally. ZERO hand-shuffling occurred across both transitions.
```

**Worked example B — Dredge Javelin tether cut (external-source hard removal):**

```
Setup: Boss tether active. Deck contains 4 normal + 2 javelin tethers (tether_a, tether_b).
       Hand contains 3 cards including tether_a.
       Discard contains 2 javelin tethers (tether_c, tether_d from earlier hits).

Player plays tether_a from hand (resolving its effect = breaking free).
Card effect handler invokes:
  vehicle.HardRemoveCards(sourceSlotId: null,
                          cardIds: ["tether_a", "tether_b", "tether_c", "tether_d"]).

Atomic sweep matches by CardId across deck + hand + discard:
  Deck: tether_b removed.
  Hand: tether_a removed (was mid-resolution — per EC-VP12, the currently-resolving instance
        is excluded from the sweep; a different tether_a copy in hand would be removed).
  Discard: tether_c, tether_d removed.

One event fires: OnGrantedCardRemoved(null, ["tether_a", "tether_b", "tether_c", "tether_d"]).
                  (Subscribers must tolerate null first arg.)
HUD removes the four UI elements in one frame. No CriticalState change. No defeat implications.
```

**Cross-references:**
- Source-slot playability gate (the soft-disable mechanism): ADR-0007 Decision 16 — composed AND with the energy gate and the positional gate (`OverridePositionRequirement`, per ADR-0006 + ADR-0007 Decision 7).
- `CardData.SourceSlotId` runtime field: declared on `CardData` per ADR-0006 (amendment 2026-05-18 per ADR-0007 Decision 16). Baked at part-install time (non-null source-slot grants) or at status-driven grant time (null for external-source cohorts).
- Edge cases EC-VP12 (mid-resolution card exclusion from sweep) and EC-VP44 (granted-card lifecycle).
- Acceptance criteria AC-VP44 (soft disable on Offline), AC-VP44b (hard removal sweep), AC-VP44c (null sourceSlotId external-source path).

---

**F-VP3 — Stat Modifier Composition**

`IVehicleView.GetStatModifier(stat)` returns the composed value across all installed parts' `StatModifiers` matching `stat`:

```
base  = consumer_defined_base(stat)       // e.g., ChassisDefinitionSO.MaxEnergyBase for Energy
add   = Σ mod.Value where mod.Operation == Add      and mod.TargetStat == stat
mult  = Π mod.Value where mod.Operation == Multiply and mod.TargetStat == stat
ovr   = first mod.Value where mod.Operation == Override and mod.TargetStat == stat (null if none)

result = ovr ?? ((base + add) × mult)
```

**Rules:**
- Additive stacks with additive (sum).
- Multiplicative stacks with multiplicative (product).
- Override wins and skips composition; multiple Overrides for the same stat → error at SO import (validation rule).
- Order matters: `(base + add) × mult`, NOT `base + (add × mult)`.

**Example (Scout, Engine grants +1 MaxEnergy, Mobility grants ×1.25 MaxEnergy, no override):**

```
base = 3 (ChassisDefinitionSO.MaxEnergyBase)
add  = 1
mult = 1.25
result = (3 + 1) × 1.25 = 5.0 → consumer floors to int MaxEnergy = 5
```

**Note:** flooring / rounding is consumer-defined. Vehicle & Part returns `float`; Card Combat decides how to consume for int-typed stats.

---

**F-VP4 — Vehicle Death Check**

```
StructuralHp (read-only computed) = Σ slot.Hp for slot in Slots where slot.IsStructural == true

IsDead (backing field, private set):
  - initial value: false
  - written ONLY by F-VP2 Step 4(f) using the formula:
        IsDead = (StructuralHp == 0)
  - OnVehicleDied fires in F-VP2 Step 4(g) AFTER the field write, exactly once per
    false → true transition. The field never transitions true → false (no resurrection
    in EA; if added post-EA, an explicit ReviveVehicle path resets it).
```

`StructuralHp` is a read-only computed property on Vehicle POCO. The check is O(N) over structural slots only (typically 1 — the single Hull). Recomputed inline within F-VP2 Step 4 when any structural slot transitions to Offline. For the common single-Hull layout, equivalent to "Hull goes Offline → vehicle dies." For multi-structural layouts (future), all structural slots must reach `Hp == 0` for death.

**Why `IsDead` is a backing field, not a derived getter (Review 2 blocker #4):** A derived getter (`bool IsDead => StructuralHp == 0`) returns `true` the moment the last structural slot's `Hp` hits zero. But the canonical event ordering (EC-VP20, F-VP2 Step 4) fires `OnSlotDamageStateChanged(slotId, Functional, Offline)` *before* `OnVehicleDied`. A subscriber to `OnSlotDamageStateChanged` that reads `IsDead` would observe `true` and could initiate the defeat sequence twice — once from its own observation, again when `OnVehicleDied` later fires. Backing-field semantics make the read consistent: subscribers either see the pre-write value (`false`) until `OnVehicleDied` fires, or they observe the post-write value inside or after `OnVehicleDied`'s handler.

---

**Design commentary:**

- F-VP1 and F-VP2 are implemented verbatim. F-VP3 is a contract; individual stats are spec'd by their owning systems.
- Corrode-before-plating is the only cross-system ordering rule this GDD owns. It is reasserted here to prevent silent reordering during Card Combat authoring.

## Edge Cases

**EC-VP1 — Damage to an Empty slot.** Ignored. `ApplyDamage(Empty, *, *)` is a no-op; no event fires. Status Effects' Burn tick on an Empty slot similarly no-ops (Status Effects EC-S2 alignment).

**EC-VP2 — Repair on an Empty slot.** No-op. `Repair(Empty, *, *)` does not create a part. Only `InstallPart` populates an Empty slot.

**EC-VP3 — Repair with `canReviveOffline = false` on an Offline slot.** No-op. `Hp` remains at 0. Used by repair effects that can top up Functional / Degraded parts but cannot revive dead ones.

**EC-VP4 — Repair with `canReviveOffline = true` exceeding `MaxHp`.** Clamped to `MaxHp`. Granted cards return to the discard pile exactly once (re-entry, not duplication).

**EC-VP5 — Install on a chassis-incompatible part.** Throws `PartIncompatibleException` at R5 step 2. Caller (Scrap Economy) must prevent the UI from offering an incompatible install. Thrown exceptions are a contract violation, not a user-facing error path.

**EC-VP6 — Install on mismatched slot kind.** Throws `PartIncompatibleException` at R5 step 4 (`part.Kind != slot.Kind`). Same caller contract.

**EC-VP6b — Install with MountDirection mismatch.** Throws `MountDirectionMismatchException` at R5 step 5 (e.g., a `MountDirection = Back` weapon attempted on a `Position = Front` slot). Caller (Scrap Economy) must filter loot offers by mount-position compatibility.

**EC-VP6c — Install on an unknown SlotId.** Throws `SlotNotFoundException` at R5 step 1. Indicates either a stale `SlotId` from an outdated save or a designer authoring error in `StarterParts` keys.

**EC-VP7 — Install when slot is full.** Valid — R5 step 2 auto-scraps the existing part. This is the replace flow. Applies per-slot regardless of vehicle slot count.

**EC-VP8 — Plating absorbs more damage than incoming.** `plating_consumed = min(amount, PlatingStacks)` floors absorption. Excess plating is NOT consumed. A 2-damage hit against 5 plating consumes 2 plating, leaves 3.

**EC-VP9 — Plating on an Empty slot.** `AddPlating(Empty, *)` is a no-op. Plating requires an installed part (the part's `MaxPlating` defines the cap).

**EC-VP10 — Plating exceeding `MaxPlating`.** Clamped. `AddPlating(slot, 10)` on a part with `MaxPlating = 3` and current `PlatingStacks = 1` sets `PlatingStacks = 3`, not 11. Excess is silently dropped.

**EC-VP11 — Granted card already in deck from another source.** The deck can hold multiple copies. On scrap, only the instances added by the scrapped part are removed (R7's "tracked by part identity"). This requires per-card-instance provenance tracking in the Deck model — forward dep on Card System.

**EC-VP12 — Hard removal while one of the swept cards is mid-resolution.** Player-initiated scrap is Chopshop-only (not mid-combat) so the scrap path cannot intersect mid-resolution. The mid-resolution intersection IS reachable via the external-source termination path (Decision 16 / Hard Removal Pathway) — e.g., a Javelin tether card resolving its own removal effect via `HardRemoveCards`. Rule: the atomic sweep across deck + hand + discard EXCLUDES the currently-resolving card stack. A card in-flight finishes resolving, then is not returned to any zone. (Note: this EC was rewritten 2026-05-18 per ADR-0007 Decision 16 — `OnSlotDamageStateChanged → Offline` does NOT remove granted cards; mid-combat Offline is a soft disable, see EC-VP44.)

**EC-VP13 — Structural slot goes Offline mid-resolution.** If the Offline transition drops `StructuralHp` to 0, `IsDead` becomes true. The resolving card finishes its effect (including any remaining damage or status application on either side). Card Combat's end-of-damage-step hook observes `IsDead` and triggers defeat AFTER the current card resolves. No mid-card interrupt. In multi-structural layouts (none in EA), this also applies — `IsDead` only fires when the final structural slot reaches 0.

**EC-VP14 — Simultaneous state transitions (multiple slots go Degraded in one damage event).** AoE damage is applied slot-by-slot in `FrameLayoutSO.Slots` index order (the SlotDefinition list order on the layout). Each slot fires its own `OnSlotDamageStateChanged` event. Ordering is deterministic per layout for replay and save.

**EC-VP15 — Stat modifier references an unknown `TargetStat`.** Error at SO import. `TargetStat` must resolve to a registered stat enum (owning system defines). Unknown stats fail import; runtime cannot hit this path.

**EC-VP16 — Override + Override for the same stat across two installed parts.** Error at SO import if statically detectable (both parts in a starter chassis). For runtime installs, Scrap Economy must check and prevent the install, or the compose call fails at runtime. Exact handling tracked as **OQ-VP2**.

**EC-VP17 — Status applied to an Empty slot.** Rejected by `ApplyStatus`. Per-slot statuses (Corroded) require `InstalledPart != null`. Whole-vehicle statuses (Enrage, etc.) are allowed when at least one slot is non-Empty. The all-slots-Empty edge case is structurally impossible: `FrameLayoutSO` validation requires at least one `IsStructural == true` slot, and structural slots authored without a starter part construct with `Hp = MaxHp` and `InstalledPart = null` *only* if the chassis explicitly omits a starter (which is disallowed at chassis import). Closes prior OQ-VP3.

**EC-VP18 — Save round-trip across a state transition.** Save captures current `Hp`, not `DamageState`. On load, `DamageState` is re-derived. No "frozen in wrong state" bug possible.

**EC-VP19 — Deserialize a Vehicle with an Offline Frame.** Legal save state only if the combat is mid-defeat-sequence at save time. Save & Persistence GDD defines save points; if saves are disallowed during the defeat sequence (recommended), this EC cannot occur. If permitted, load restores the state and Combat resumes the defeat sequence.

**EC-VP20 — Event ordering within a single damage event.** Events are synchronous. Subscribers must tolerate mid-tick state changes — any handler that mutates state (e.g., triggers a card) runs to completion before the next damage tick.

**See F-VP2 Canonical Event Order Table (single source of truth, ADR-0007 Decision 13 lock-step).** Step letters, conditions, recursion semantics, and subscriber rules live there. ADR-0007 Decision 11 invariants and the F-VP2 table are CI-locked against drift; do not duplicate the table here. Prior R5 versions of this edge case restated the step list inline; that restatement was the root cause of R5 blocker #4 (prose-vs-table drift) and has been removed per the R6 strategy switch.

**EC-VP44 — Granted-card lifecycle on slot Offline (Decision 16 — soft disable vs hard removal split).** Granted cards have two distinct lifecycle pathways. Per ADR-0007 Decision 16:

*Soft disable (the default — slot Offline):* When the source slot transitions `Functional` or `Degraded` → `Offline` mid-combat, the slot's granted cards remain in whatever zone they currently occupy (deck, hand, discard). They become unplayable via the source-slot-state playability gate: a card is playable iff `vehicle.GetSlot(card.SourceSlotId).DamageState ∈ {Functional, Degraded}`. HUD greying is driven by the `OnSlotDamageStateChanged` event from F-VP2 Step 4(b) — no dedicated card-greying event exists. Repair revival of the source slot (`Repair(slotId, n, canReviveOffline=true)` per Decision 12) restores playability automatically via the same gate; the `OnSlotDamageStateChanged` event on the revival drives HUD un-greying. **`OnGrantedCardRemoved` does NOT fire on Offline. Deck composition is unchanged. Zero hand-shuffling occurs across either transition.**

*Hard removal (atomic sweep across all zones):* Two V&P-bounded triggers, and only these two, fire `OnGrantedCardRemoved`:

1. **Scrap (V&P R6 path):** Player scraps the part via the Chopshop UI between combats. V&P R6 invokes `HardRemoveCards(slotId, slot.InstalledPart.GrantedCards.Select(c => c.CardId).ToList())`. Non-null `sourceSlotId` = the scrapped slot. Not legal mid-combat.

2. **External-source termination:** The grant source signaled end-of-life from outside V&P (e.g., Dredge Javelin chain cut). The originating system (Status Effects / boss script) invokes `HardRemoveCards(null, cohortCardIds)` with `sourceSlotId = null`. The atomic sweep crosses deck + hand + discard and excludes the currently-resolving card stack per EC-VP12. Exactly one `OnGrantedCardRemoved(null, removedCardIds)` event fires after the sweep — subscribers MUST tolerate the `null` first argument.

*Empty-slot path:* A slot transitioning Offline with `InstalledPart == null` has no granted cards to manage — no playability gate evaluation, no `OnGrantedCardRemoved` firing. Trivial no-op.

*Concurrency with re-grant:* If a slot is repaired and re-installed with the same part in the same combat (unreachable in EA — install is post-combat-only per R5), the granted cards' `SourceSlotId` is freshly baked at the new install; the soft-disable gate evaluates against the new source-slot state. No deck mutation occurs across the cycle. Future cross-system re-grant via Status Effects must use the hard-removal pathway to clear the old cohort before granting a new one — soft disable alone does NOT free the old `SourceSlotId` reference.

(Closes R5 blocker for granted-card lifecycle. See F-VP2 "Hard Removal Pathway" subsection for the worked examples; AC-VP44 / AC-VP44b / AC-VP44c for acceptance criteria.)

## Dependencies

**Upstream (this GDD consumes):**

- **Card System GDD** (Approved) — `CardDefinitionSO`, `CardFamily` enum. Required by R7 (GrantedCards) and B2 (PrimaryFamilies, StarterDeck).
- **Status Effects GDD** (Approved) — `StatusInstance`, `StatusType`, tick pipeline. Vehicle POCO stores `ActiveStatuses`; Status Effects owns behavior. Closes OQ-SE2 via R9's `DamageSource` enum.
- **ADR-0001: Visual Vehicle Part System** (Accepted 2026-04-21; **partially superseded by ADR-0007 2026-05-18**) — visual layer contract (damage-state rendering, Addressables group layout, sprite stacking) remains authoritative. The fixed 4-slot data contract from ADR-0001 is superseded by ADR-0007's `FrameLayoutSO`-driven variable slot model. Interface signatures (`IVehicleView` / `IVehicleMutator`) are updated by ADR-0007 — see R9 of this GDD for the current surface.
- **ADR-0007: Frame-Driven Variable Slot System** (Proposed 2026-05-18) — slot taxonomy (`SlotKind`), `FrameLayoutSO` contract, Armor slot mechanic, `SlotPosition`/`MountDirection`, `StructuralHp` death model, `VehicleStateDTO` V1→V2 migration. Authoritative for slot data model and all interface signatures. This GDD's R_FL, R_ARM, R1.1, and the revised R1/R2/R5/R6/R9 implement ADR-0007.

**Downstream (systems that consume this GDD):**

- **Card Combat System GDD** (not authored) — reads `IVehicleView`, writes via `IVehicleMutator`. Consumes `GetStatModifier` for damage math, `Slots` for targeting, `IsDead` for defeat trigger. Owns full damage resolution order (this GDD locks only plating-before-Hp and Corrode-before-plating).
- **Scrap Economy GDD** (not authored) — exclusive caller of `InstallPart` / `RemovePart`. Owns Chopshop UI, pricing formulas, part-drop acceptance flow. Must enforce chassis/slot compatibility pre-call.
- **Loot & Reward GDD** (not authored) — reads `Slots` for slot-force bias; offers parts; accepted offers route through Scrap Economy.
- **Save & Persistence GDD** (Approved) — serializes Vehicle POCO as a leaf field on `RunState`. No schema coupling beyond "POCO is serializable."
- **Enemy AI** (no standalone GDD; behavior lives in Card Combat + Status Effects) — routes effects through `IVehicleMutator` identically to the player. No privileged path.
- **Combat HUD GDD** (not authored) — reads `IVehicleView` events for HUD updates. UI-only consumer.
- **Visual Vehicle Part System** (ADR-0001, view layer) — `VehicleView` MonoBehaviour subscribes to `IVehicleView` events, renders per ADR.

**Bidirectional acknowledgements (Design Doc Rules):**

| Acknowledgement | Status |
|---|---|
| Card System GDD mentions Vehicle & Part owns `ChassisDefinitionSO` | ✅ honored |
| Card System GDD mentions part-granted cards as a deck source | ❌ forward dep — propagate via `/propagate-design-change` or during Card Combat GDD |
| Card System GDD `PositionRequirement` (EnemyAhead / EnemyBehind) is derived from `PartDefinitionSO.MountDirection` at install (R5) | ❌ forward dep — Card System GDD must be updated to note this derivation rule |
| Status Effects GDD locates `ActiveStatuses` on Vehicle POCO | ✅ honored |
| Status Effects GDD OQ-SE2 closed by `DamageSource` enum (R9) | ✅ — Status Effects log to be updated when OQ resolved |
| ADR-0001 visual contract (sprite layers, damage tint) is respected verbatim | ✅ honored |
| ADR-0001 data contract (fixed 4-slot SlotType) is superseded by ADR-0007 | ⚠️ ADR-0001 to be marked Superseded (data section only) after ADR-0007 acceptance |
| ADR-0007 `FrameLayoutSO` contract and Armor slot mechanic are implemented by R_FL and R_ARM | ✅ honored |
| Save & Persistence treats Vehicle POCO as a leaf field; V1→V2 `VehicleStateDTO` migration owned by ADR-0007 | ✅ honored |

**Forward deps tracked (require propagation):**

1. **Card System GDD** — part-granted cards as a deck source; per-instance provenance tracking (EC-VP11).
2. **Meta Progression GDD** (not authored) — `MasteryUnlocks` field on `ChassisDefinitionSO` (**OQ-VP1**).
3. **Scrap Economy GDD** (not authored) — `InstallPart` / `RemovePart` exclusivity; Chopshop-only scrap action enforcement; Override-collision handling (**OQ-VP2**).

## Tuning Knobs

All tunable values surfaced below with safe ranges and the gameplay aspect they control.

| Knob | Owner | Default (EA) | Safe Range | Affects |
|---|---|---|---|---|
| `ChassisDefinitionSO.DefaultSlotMaxHp[Hull]` | ChassisSO (per chassis) | Scout 16 / Assault 24 | 10–40 | Death HP fallback for Hull slots without `MaxHpOverride`; primary combat-length dial |
| `ChassisDefinitionSO.DefaultSlotMaxHp[Weapon]` | ChassisSO | Scout 10 / Assault 14 | 6–20 | Weapon offline risk; pacing of capability loss |
| `ChassisDefinitionSO.DefaultSlotMaxHp[Engine]` | ChassisSO | Scout 12 / Assault 10 | 6–20 | Engine offline risk; deck cycling loss |
| `ChassisDefinitionSO.DefaultSlotMaxHp[Mobility]` | ChassisSO | Scout 12 / Assault 10 | 6–20 | Mobility offline risk; route-gate penalty |
| `ChassisDefinitionSO.DefaultSlotMaxHp[Armor]` | ChassisSO | (no default — must be set via `MaxHpOverride` per Armor slot) | n/a | Per-slot Armor HP is authored on the LayoutSO via `MaxHpOverride`; no chassis default — Armor is intentionally per-layout. `FrameLayoutSO.OnValidate` rejects any Armor slot with null or `< 1` `MaxHpOverride` (hard import gate, closes Review 2 blocker #7). |
| `ChassisDefinitionSO.DegradedThreshold%` | ChassisSO | 50 | 30–70 | (Deprecated — moved to `FrameLayoutSO.DegradedThresholdPct`; field retained for back-compat but ignored when LayoutSO sets a value) |
| `ChassisDefinitionSO.MaxEnergyBase` | ChassisSO | 3 | 2–5 | Base energy per turn before stat modifiers |
| `ChassisDefinitionSO.MaxHandSizeBase` | ChassisSO | 5 | 3–7 | Hand size cap |
| `FrameLayoutSO.Slots.Count` | LayoutSO (per layout) | Small 5 / Tiny 4 / Hauler 5 / Heavy 7 / Dredge 10 | 3–12 | Total vehicle complexity; card count ceiling via GrantedCards sum; combat decision surface |
| `FrameLayoutSO.DegradedThresholdPct` | LayoutSO (per layout) | 50 | 30–70 | Proportion of HP band spent in Degraded across ALL slots in this layout |
| `SlotDefinition.MaxHpOverride` | LayoutSO (per slot) | See R_FL.2 / R_FL.3 tables | 4–60 | Per-slot durability; use to differentiate slots within the same Kind on a layout |
| `SlotDefinition.ExposureMultiplier` | LayoutSO (per Armor slot) | 3.0 (Dredge Armor slots) | 1.5–5.0 | Damage amplification when Armor plate is destroyed and underlying slot is hit through the exposure |
| `SlotDefinition.Position` (Weapon slots) | LayoutSO (per Weapon slot) | Front / Back on Small Frame; Any on Tiny / Hauler | Any / Front / Back | Physical mount location; gates `MountDirection` matching on install |
| `SlotDefinition.HudAnchor` | LayoutSO (per slot) | per-slot `AnchorPoint` (engine-free POCO struct) in unit rect `[0,1]×[0,1]` (e.g. `(0.50, 0.80)` Hull centre-top) | `X`, `Y` ∈ `[0,1]` AND `IsFinite(X)` AND `IsFinite(Y)` | Combat HUD layout — where the slot's HP/state pip renders within the vehicle widget. `AnchorPoint` (not `UnityEngine.Vector2`) is required because `SlotDefinition` lives in the engine-free `WastelandRun.Vehicle` assembly per ADR-0005 / ADR-0007. View layer is responsible for converting to `Vector2` when binding to `RectTransform`. Replaces ADR-0001's hardcoded 4-slot HUD positions; required for variable-slot-count layouts (rec. #17). |
| `PartDefinitionSO.MountDirection` | PartSO (Weapon parts only) | Any (all EA starter weapons) | Any / Front / Back | Controls which physical slot positions accept this weapon; drives card position requirements (`EnemyAhead` / `EnemyBehind`) |
| `PartDefinitionSO.MaxPlating` | PartSO (per part) | 0–3 | 0–5 | Max plating stacks for that part; applies to non-Armor slots only |
| `PartDefinitionSO.GrantedCards.Count` | PartSO | Common 1 / Uncommon 2 / Rare 3 / Legendary 3 | 1–4 | Deck disruption magnitude on scrap/Offline |
| `PartDefinitionSO.StatModifiers[*].Value` | PartSO | per part | ±50% of stat base | Stat swings; balance vs. starter chassis feel |
| `PartDefinitionSO.ArmorContribution` | PartSO (per part) | DEPRECATED | n/a | Removed under ADR-0007. Field still parsed for save back-compat; non-zero values produce SO import warning. Schedule for full removal after Dredge balance pass. |

**Armor HP guidance (pending balance pass):**

- Armor plate HP on the Dredge boss (`armor_chest`, `armor_back`) should be tuned so that an unupgraded Scout needs approximately 2–3 focused hits to break one plate (M3 scarcity pressure). Once broken, the `ExposureMultiplier = 3.0` makes the underlying Hull vulnerable to 3× punishment — this is the "breakthrough" moment the Armor mechanic is designed around.
- Set `MaxHpOverride` on both Dredge Armor slots during the Dredge balance pass. Do not lock these values before first playtest.

**ExposureMultiplier safe range guidance:**

- 1.5: exposure feels like a moderate debuff. Still punishing, but not run-defining.
- 3.0 (default): breaking armor is a significant combat swing. Three times the normal damage lands on the underlying slot.
- 5.0: breaking armor is immediately crisis-level. Reserve for ultra-late boss design.

**Slot count guidance:**

- 3 slots: minimum viable vehicle (not recommended — too few capability axes for interesting decisions).
- 5 slots (Small / Medium / Hauler): standard player chassis; matches current Scout/Assault card economy.
- 7 slots (Heavy): pushes deck toward larger-hand strategies; engine count matters.
- 10 slots (Dredge boss): maximum designed case; targeting decisions become complex (by design for a boss).
- Above 12: uncharted; card economy and targeting UI have not been designed for this scale.

---

**Chassis HP profile guidance:**

- Keep Scout Hull < Assault Hull by ~50% to preserve the glass-cannon / tank identity (Pillar 2).
- Total **slot** HP pool ratio Scout : Assault should stay in `[0.70, 0.90]`. EA slot-HP values give 50:58 ≈ 0.86. Under ADR-0007, Assault's tank identity is no longer carried by an `ArmorContribution` pool — instead it must come from (a) higher Hull `MaxHp`, (b) higher per-slot `MaxHpOverride` values on its layout, or (c) addition of Armor slots to the Medium Frame layout (post-EA). Balance pass to choose one.
- Engine HP below Hull HP by ~25–50% keeps deck disruption credible mid-combat without trivializing Hull.

**DegradedThreshold guidance:**

- 30%: Degraded feels like a last-breath warning. Good for tension, bad for readability (short window to react).
- 50% (default): symmetric with Functional band.
- 70%: Degraded dominates the HP band. Warning fires early; risks fatigue ("always Degraded, meaningless").

**Granted card count guidance:**

- 1 / 2 / 3 / 3 is calibrated to make Rare and Legendary parts noticeably attachment-inducing while keeping Common parts low-stakes. Raising Legendary to 4 pushes into "scrapping this wrecks my deck" territory — consider only if playtests show Legendary loss is undersold.

**Out of scope for this GDD:**

- Damage values (owned by Card Combat).
- Plating application amounts (owned by specific card / effect SOs).
- Status Effect magnitudes (owned by Status Effects GDD).
- Drop rates (owned by Loot & Reward GDD).

## Visual/Audio Requirements

**Visual (see ADR-0001 for the full rendering contract):**

ADR-0001 is authoritative for chassis silhouette composition, per-slot sprite stacking order, MaterialPropertyBlock damage overlay (Functional / Degraded / Offline), Addressables group layout, and the 40–60 base-sprite EA budget.

This GDD adds NO new visual requirements beyond ADR-0001. It asserts only which state changes MUST produce a visual change:

- `OnSlotDamageStateChanged(slotId, Functional, Degraded)` → degraded overlay visible on part sprite.
- `OnSlotDamageStateChanged(slotId, *, Offline)` → offline overlay visible; sprite silhouette shows visible "hole" per ADR-0001.
- `OnSlotDamageStateChanged(slotId, Offline, Degraded)` → overlay returns to degraded (revival).
- `OnPartInstalled(slotId, part)` → new part sprite swapped in via `SpriteKey`.
- `OnPartRemoved(slotId, part)` → slot sprite cleared; Empty silhouette revealed.
- `OnStatusApplied(slotId, Corroded)` → per-slot corrosion VFX per Status Effects GDD.
- `OnArmorExposed(armorSlotId, redirectsToSlotId)` → Armor plate sprite shows "destroyed" state (sparks/cracks per R_ARM.5); HUD MUST emphasize the underlying `redirectsToSlotId` row to telegraph the now-vulnerable target. (Tightened from "MAY emphasize" to "MUST emphasize" per Review 4 Recommended R1 — the telegraph is the core UX payload of the exposure mechanic; optional treatment defeats the design intent.)
- `OnCriticalStateChanged(isCritical)` → **HUD MUST display a critical-state indicator within 100ms of the event firing on `isCritical == true`, and MUST clear that indicator within 100ms of the event firing on `isCritical == false`.** Specific visual treatment (vignette geometry, color, pulse cadence) and audio-layer driving are authored in `design/ux/combat-hud.md` and the Audio GDD respectively; this GDD locks only the latency contract and the firing semantics. Subscribers that need to suppress the indicator on death frames check `IsDead` inside the handler (per Step 4 Subscriber Rule 3). Stub-only per Review 4 Blocker #4 — combat-hud.md full refresh is a follow-up ticket against ADR-0007's variable-slot model.

**Audio cues (owned by Audio GDD; this GDD asserts what MUST have a cue):**

| Event | Cue Requirement | Priority |
|---|---|---|
| Slot Functional → Degraded | Short metallic strain / pop (~0.3s) | P0 — informs player of risk |
| Slot Any → Offline | Sharp break / mechanical failure (~0.5s) | P0 — informs player of capability loss + deck change |
| Slot Offline → Degraded (revival) | Reverse-pop / rebuild sting | P1 — rarer; confirms revival |
| Last structural slot → Offline (vehicle death) | Distinct death stinger (different from any other slot Offline) | P0 — must read as terminal |
| Armor slot → Offline (`OnArmorExposed`) | Plate-shatter cue — heavier, glassier than ordinary slot Offline; emphasizes "breakthrough" moment | P0 — must read as a combat-state swing for the Dredge fight |
| `InstallPart` at Chopshop | Bolt-tightening / install ratchet | P1 |
| `RemovePart` (scrap) | Metallic strip / tear | P1 |
| `AddPlating` | Layered armor clink | P2 |

**Audio mixing requirement:** the vehicle-death cue (last structural slot → Offline) must be distinguishable from any other slot Offline cue, AND the Armor-exposed cue must be distinguishable from both — even at 50% mix volume. Confusing a non-death Offline with vehicle death, or confusing an Armor-exposed with an ordinary Offline, is a P0 bug.

**Same-frame death-cue ordering arbiter (closes R6 Blocker 5 — audio-director three contradictions):** when `OnVehicleDied`, `OnArmorExposed`, `OnCriticalStateChanged(true)`, the plate-shatter cue, and/or the CriticalState entry stinger would all fire on the same F-VP2 frame (Steps 4(a) through 4(h)), the audio layer MUST arbitrate per the following rules — overriding the F-VP2 event-firing order with a separate **audio composition order**:

1. **`IsDead` suppression gate** — any cue whose subscriber observes `IsDead == true` at composition time is **suppressed at the audio layer**, even if its source event already fired. This includes: the CriticalState entry stinger (`OnCriticalStateChanged(true)` is no longer meaningful once the vehicle is dead), the plate-shatter cue when shatter and death land on the same frame (`OnArmorExposed` firing before `OnVehicleDied` per F-VP2 Step 4(g) ordering — but the player needs ONE terminal cue, not two), and any per-slot revival cue (`Offline → Degraded`) that lands in the same frame as `OnVehicleDied` (forbidden state — implementations MUST assert).
2. **Vehicle-death cue is supreme** — when `OnVehicleDied` fires, the vehicle-death stinger plays unducked at full mix priority. Concurrent P0/P1 cues that would have played on the same frame are ducked −12dB AND clipped to ≤0.2s (most are not even audible at that mix), with the exception of the `OnArmorExposed` plate-shatter cue, which is fully suppressed under rule 1 (the death stinger absorbs the narrative beat).
3. **Plate-shatter precedes death stinger when the death-causing hit travels through Armor** — if the breakthrough chain in a single `ApplyDamage` call routes Armor → Hull where the Hull recursion finishes the vehicle, the audio composer emits a 0.15s plate-shatter pre-roll → death stinger as a single composite cue (one "shatter-then-die" gesture), NOT two separate samples. F-VP2 still fires `OnArmorExposed` then `OnVehicleDied` as separate events; the composer treats them as one composite for mix purposes.
4. **CriticalState (true) suppression on death frames** — `OnCriticalStateChanged(true)` firing on the same frame as `OnVehicleDied` is suppressed unconditionally (rule 1 IsDead gate). `OnCriticalStateChanged(false)` recovery stinger has no death-frame interaction (false-direction transitions can only happen on a live vehicle).
5. **HUD caption layer mirrors audio** — captions for cues suppressed under rules 1–4 are also suppressed (per U8 caption coverage floor). The death caption ("Vehicle destroyed") plays once; no concurrent "Armor exposed" or "Critical state" caption stacks.

Implementation note: the same-frame arbiter is owned by the Audio System (or its delegate) at the *composition* layer, downstream of V&P's event emission. V&P does not change F-VP2 ordering to accommodate audio — the canonical event order is unchanged. The Audio GDD MUST publish the composition rule set; this V&P contract publishes the suppression *gates* (what may be suppressed, under what observable state) so subscribers can be implemented and tested.

**Explicit non-requirements (out of scope):**

- No animation timing specs (owned by Combat HUD / Card Combat).
- No music stingers or combat-ender stings (owned by Audio GDD).
- No VFX particle specs (owned by VFX/Shader specialists, gated by ADR-0001 and the Technical Art GDD).

## UI Requirements

This GDD specifies only vehicle-state-driven UI contracts. Full HUD layout, screen flows, and animation timing belong to the Combat HUD GDD.

**HUD event subscription lifecycle contract (closes R6 Blocker 6 — ui-programmer):** the Vehicle POCO lives in the engine-free `WastelandRun.Vehicle` assembly; the HUD is a `MonoBehaviour` (or stack of `MonoBehaviour`s) in `WastelandRun.Gameplay` or `WastelandRun.UI`. The Vehicle POCO has no awareness of the `MonoBehaviour` lifecycle (`OnEnable` / `OnDisable` / `OnDestroy`) and cannot cooperate with it. Without an explicit subscription contract, the 12 `IVehicleView` events (`OnSlotDamageStateChanged`, `OnSlotHpChanged`, `OnPlatingChanged`, `OnStatusStackChanged`, `OnVehicleDied`, `OnCriticalStateChanged`, `OnArmorExposed`, `OnGrantedCardRemoved`, `OnPartInstalled`, `OnPartRemoved`, `OnStatusApplied`, `OnStatusExpired`) become guaranteed memory leaks on scene reload — the Vehicle POCO retains delegate references to destroyed `MonoBehaviour` HUDs, and a fresh Vehicle on reload cannot recover.

**Owner doc**: `design/ux/combat-hud.md` (refresh ticket already exists at `production/sprint-tickets/combat-hud-refresh.md`). The refresh ticket MUST publish, before HUD implementation begins, a subscription contract conforming to this V&P-side requirement:

1. **Subscribe in `OnEnable`, unsubscribe in `OnDisable`.** Every HUD widget that subscribes to an `IVehicleView` event MUST register the subscription in `OnEnable` (or `Awake` if the widget is permanent) and unregister the same delegate in `OnDisable` (or `OnDestroy` if matching `Awake`). Subscription and unsubscription MUST use the same delegate reference (no lambda subscription without a cached field) — `Action` registers via `delegate -= handler` requires reference equality.
2. **Vehicle reference held weakly OR cleared on scene end.** If the HUD holds a direct `IVehicleView` reference across scene reload, the reference MUST be cleared in `OnDisable` (set to null) so the HUD does not retain a strong reference to the previous-combat Vehicle. The Combat Manager (or whichever system constructs the Vehicle POCO) is responsible for letting the previous Vehicle go out of scope.
3. **HUD lifecycle audit test.** A NUnit EditMode test loads a synthetic Combat scene, captures the live Vehicle POCO, exits the scene, forces GC, and asserts the Vehicle POCO is collectible (no live delegate roots remain). This test is BLOCKING-gate for the Combat HUD refresh ticket and is added to CI alongside it.
4. **V&P side: no subscriber bookkeeping.** Vehicle POCO does NOT keep subscriber inventories, does NOT iterate "destroy" subscriber lists on death, and does NOT re-broadcast events on reload. The contract is one-way: V&P emits, subscribers manage their own lifecycle.

V&P's responsibility ends at publishing this contract. Implementation lives in `design/ux/combat-hud.md` per the refresh ticket. The W2 architecting wave (EnemyDefinitionSO + BrainRulesetSO + ADR) does NOT depend on this implementation — only on the contract being published, which this section satisfies.

---

**U1 — Slot HUD display (in-combat)**

All slots of the player vehicle (count = `FrameLayoutSO.Slots.Count` for the vehicle's layout, typically 4–7) must be visible continuously during combat. The HUD iterates `IVehicleView.Slots` and renders one row per `SlotInstance`. Each slot row shows:

- Part sprite (from `PartDefinitionSO.SpriteKey`) or silhouette placeholder if `InstalledPart == null`
- Slot label (from `SlotInstance.Kind` — Weapon / Engine / Mobility / Hull / Armor — and `SlotInstance.Position` when Position != Any, e.g., "Weapon (Front)")
- HP bar or numeric `Hp / MaxHp`
- DamageState tint (per ADR-0001: green = Functional, amber = Degraded, red = Offline, empty = no part)
- PlatingStacks (slots where `Kind != Armor` only; shown as pips or numeric badge, only if > 0)
- Active per-slot statuses (Corroded) as badge icons

**Armor slot display:** Armor slots (`Kind == Armor`) render with the same row template as any other slot — there is no separate "Armor bar" overlay anymore. The visual contract for intact-vs-exposed and the OnArmorExposed cue are specified in R_ARM.5 (Visual Contract). HUDs MAY group Armor slots adjacent to their `RedirectsToSlotId` target for readability, but the underlying data model is uniform: one `SlotInstance` per slot row.

**Variable slot counts:** Layouts may have anywhere from 3 to 12 slots (current EA range: 4–10). HUD layout must accommodate this range — no hardcoded 4-slot assumption. Slot ordering for display follows `FrameLayoutSO.Slots` index order.

Empty slots display a silhouette placeholder, not hidden.

---

**U2 — Enemy slot HUD**

Mirror U1 for enemy vehicles. Reveal state: ALL slots of the enemy's `FrameLayoutSO` are visible from combat start (no fog-of-war on enemy slot HP — readability > mystique, Slay the Spire convention). This includes enemy Armor slots: per-slot HP is shown for Armor slots from the first turn, so the player can plan a focused break. Per symmetry rule: enemies use the same `IVehicleView.Slots` enumeration as the player — no special-case enemy data model. Layouts with high slot counts (e.g., Dredge boss `dredge_frame` = 10 slots) require the Combat HUD to scale or scroll the enemy slot list; exact layout strategy belongs to the Combat HUD GDD.

---

**U3 — Install dialog (post-combat / Loot node)**

When Loot offers a part, a dialog compares the offered part against the currently-installed part. It MUST surface:

- Offered part: name, rarity, `Kind` (and `MountDirection` for Weapon parts), granted cards (visible card previews), stat modifiers, `MaxPlating`.
- Current part: same fields for comparison. If the slot is Empty, the "current" side shows "Empty".
- **Deck delta**: explicit list of "cards added" (offered.GrantedCards) and "cards removed" (current.GrantedCards). This is the deck-disruption preview that M2 (Emotional Attachment) depends on.
- Actions: `Install` (triggers `InstallPart`, auto-scraps current) / `Decline` (no change).

---

**U4 — Scrap dialog (Chopshop)**

Chopshop scrap UI MUST surface:

- Current part full info per U3.
- **Deck delta**: "cards removed" preview.
- Scrap reward (from Scrap Economy GDD).
- Actions: `Scrap` (triggers `RemovePart`) / `Cancel`.

Double-confirmation NOT required — the deck delta preview is the confirmation surface.

---

**U5 — Damage feedback**

On `OnSlotDamageStateChanged`, the affected slot row MUST:

- Flash briefly (color per state transition)
- Update HP bar and tint without delay
- Play audio cue per Visual/Audio section

---

**U6 — Slot-state card-disable notification**

When a slot goes Offline, its granted cards are **NOT** removed from deck / hand / discard. Per Decision 16 (ADR-0007), Offline is a **soft disable**: the cards remain in their current zones and become unplayable via the source-slot playability gate (analogous to the existing `EnemyAhead`/`EnemyBehind` positional gate). A non-blocking HUD notification lists the affected cards for ~2 seconds at the moment of transition — phrased as "these N cards are disabled while [SlotName] is Offline", not "removed". This is the M2 readability hook: the player must see *which* cards just lost playability, and that they will be restored on repair.

**Hard-removal notification** is a distinct event: only fires on (a) scrap (R6) and (b) external-source termination (e.g., Dredge Javelin chain cut). When triggered, the HUD shows "these N cards left your deck" for ~2 seconds; the wording explicitly differs from the soft-disable notification so the player can distinguish recoverable disable from permanent removal. Both notifications observe the F-VP2 Step 4(h) frame ordering (after `OnCriticalStateChanged`) to avoid visual collision with the critical-state vignette.

On repair restoring an Offline slot to Functional or Degraded, a transient HUD cue ("[SlotName] back online — N cards restored") fires once per restoration; the cards themselves do not move between zones, only their dimmed/playable state changes. Closes R6 Blocker 1 priority (4-specialist convergence: game-designer / systems-designer / ux-designer / ui-programmer).

---

**U7 — Tooltip contracts**

Hovering any slot in combat reveals a tooltip with:

- Part full info (name, rarity, stat modifiers, granted cards)
- Current HP, PlatingStacks, active statuses

Tooltips MUST be reachable by keyboard navigation (accessibility — all features are keyboard/mouse accessible per technical-preferences.md).

---

**U8 — Accessibility floors**

- **DamageState non-color encoding** (closes R6 Blocker 8 — ux-designer): each of the four states MUST be communicated through at least two non-color channels in parallel — (a) a unique state-glyph in the slot HUD card (Empty: ⊘ outline; Functional: ◆ solid; Degraded: ⚠ chevron; Offline: ✕ slash), (b) a text state label rendered at ≥12pt under the glyph, (c) a slot-frame border weight (Empty: dashed 1px; Functional: solid 2px; Degraded: solid 2px with pulsing chevron overlay; Offline: hatched 3px). Numeric HP under the state label is the third channel. The four-state palette MUST pass the Color Universal Design (CUD) Protanopia/Deuteranopia/Tritanopia simulator with no two states becoming indistinguishable; `design/ux/accessibility-requirements.md` §4 (canonical owner) holds the actual hex values.
- **HP / Armor bars** MUST show numeric values, not just fill (toggleable in options per UX GDD; default ON for EA). Plating stack count MUST render as a discrete numeric badge (e.g., "×3"), not relying on icon multiplication alone.
- **Audio caption coverage floor** (closes R6 Blocker 8 — audio-director + accessibility cross-reference): every P0 and P1 audio cue in the V&P audio table (see Visual/Audio section, ~line 1605) MUST have a corresponding caption row spec'd in `design/ux/accessibility-requirements.md` §6 (captions). The seven P0/P1 cues currently in the V&P audio table — `OnSlotHpChanged` (per slot kind), `OnSlotDamageStateChanged` to Offline, `OnArmorExposed`, `OnVehicleDied`, `OnCriticalStateChanged(true)`, `OnCriticalStateChanged(false)` recovery, and Armor-plate shatter (Decision 6 step 1) — MUST each have: (a) caption text (≤32 chars, e.g., "Engine offline"), (b) caption priority band (matches audio cue priority — P0 captions interrupt P1; same-frame P0+P0 stacks per the same-frame ordering arbiter in Cluster D), (c) caption duration (≥1.5s minimum). Cues that fire on a dead vehicle (per Cluster D `IsDead` suppression rule) are suppressed at both audio AND caption layers — they do not stack a phantom caption. Per `design/accessibility-requirements.md` §4, no decision-critical channel is audio-only.

---

**Out of scope:**

- Exact screen positions, sizes, fonts — owned by Combat HUD GDD.
- Animation timings — owned by Combat HUD GDD.
- Chopshop full flow (beyond the scrap dialog) — owned by Scrap Economy GDD.
- Loot node flow (beyond the install dialog) — owned by Loot & Reward GDD.

## Acceptance Criteria

GIVEN / WHEN / THEN format. Every AC is testable by a QA tester against the Vehicle POCO and VehicleView.

**Vehicle POCO composition (R1–R2):**

- **AC-VP1** — GIVEN a new Scout vehicle constructed with `FrameLayoutId = "small_frame"`, WHEN the Vehicle POCO is constructed, THEN `Slots.Count == 5` (one per `SlotDefinition` in `small_frame`), each `SlotInstance` carries the SlotId, Kind, Position, and IsStructural from its `SlotDefinition`, and slot order matches `FrameLayoutSO.Slots` index order. No `MaxArmor` / `CurrentArmor` fields exist on the Vehicle POCO (removed under ADR-0007).
- **AC-VP2** — GIVEN a Vehicle POCO type, WHEN inspected via reflection in an EditMode test, THEN `typeof(Vehicle).BaseType == typeof(System.Object)` AND `typeof(Vehicle).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).All(f => !typeof(UnityEngine.Events.UnityEventBase).IsAssignableFrom(f.FieldType))` AND `typeof(Vehicle).GetEvents().All(e => !typeof(UnityEngine.Events.UnityEventBase).IsAssignableFrom(e.EventHandlerType))`. CI grep gate also rejects any `using UnityEngine.Events` token inside `WastelandRun.Vehicle.asmdef` files.
- **AC-VP3** — GIVEN a Vehicle POCO, WHEN `ActiveStatuses` is accessed, THEN it returns `List<StatusInstance>` (owned by Vehicle per Status Effects GDD).

**SlotInstance and DamageState (R1.1, R3, States & Transitions):**

- **AC-VP4** — GIVEN a Scout Hull slot (`hull_0`) with `MaxHp = 16`, WHEN `Hp = 16`, THEN `DamageState == Functional`.
- **AC-VP5** — GIVEN a Scout Hull slot with `MaxHp = 16` and layout `DegradedThresholdPct = 50`, WHEN `Hp = 8`, THEN `DamageState == Degraded` (F-VP1: threshold = 8).
- **AC-VP6** — GIVEN a Scout Hull slot with `MaxHp = 16` and `DegradedThresholdPct = 50`, WHEN `Hp = 9`, THEN `DamageState == Functional`.
- **AC-VP7** — GIVEN any slot, WHEN `Hp == 0 && InstalledPart != null`, THEN `DamageState == Offline`.
- **AC-VP8** — GIVEN any slot, WHEN `InstalledPart == null`, THEN `DamageState == Empty` regardless of `Hp`.
- **AC-VP9** — GIVEN a vehicle whose every `IsStructural == true` slot has `Hp == 0`, WHEN `IsDead` is read, THEN it returns `true`.
- **AC-VP10** — GIVEN a vehicle with at least one `IsStructural == true` slot at `Hp > 0`, WHEN `IsDead` is read, THEN it returns `false`, regardless of other slots' states.

**Install (R5):**

- **AC-VP11** — GIVEN an Empty engine slot at `slotId = "engine_0"`, WHEN `InstallPart("engine_0", validEnginePart)` is called, THEN `InstalledPart == validEnginePart`, `Hp == MaxHp`, `PlatingStacks == 0`, `CombatsSurvived == 0`, and `OnPartInstalled("engine_0", validEnginePart)` fires exactly once.
- **AC-VP12** — GIVEN an engine slot with an installed part granting cards [A, B], WHEN `InstallPart("engine_0", newPart)` with `newPart.GrantedCards == [C]` is called, THEN old cards [A, B] are removed from deck/hand/discard, new card [C] is added to the deck, and `Hp` resets to the slot's `MaxHp`.
- **AC-VP13** — GIVEN a Scout vehicle, WHEN `InstallPart("engine_0", partWhere CompatibleChassis = [Assault])` is called, THEN `PartIncompatibleException` is thrown.
- **AC-VP14** — GIVEN a weapon slot (`weapon_front`), WHEN `InstallPart("weapon_front", partWhere Kind = Engine)` is called, THEN `PartIncompatibleException` is thrown (Kind mismatch — EC-VP6).
- **AC-VP14b** — GIVEN the Back weapon slot (`weapon_back`, `Position = Back`), WHEN `InstallPart("weapon_back", partWhere Kind = Weapon, MountDirection = Front)` is called, THEN `MountDirectionMismatchException` is thrown (Front-only part rejected by Back-only slot — EC-VP6b).
- **AC-VP14c** — GIVEN any vehicle, WHEN `InstallPart("nonexistent_slot", validPart)` is called, THEN `SlotNotFoundException` is thrown (EC-VP6c).

**Scrap (R6):**

- **AC-VP15** — GIVEN an engine slot with installed part granting cards [A, B] and `Hp = 5`, WHEN `RemovePart("engine_0")` is called, THEN `InstalledPart == null`, `Hp == 0`, `PlatingStacks == 0`, `DamageState == Empty`, cards [A, B] are removed from all deck zones, and `OnPartRemoved("engine_0", part)` fires exactly once.

**Part-granted cards (R7):**

- **AC-VP16** — GIVEN a `PartDefinitionSO` of Rarity Common, WHEN imported, THEN validation accepts iff `GrantedCards.Count == 1`.
- **AC-VP17** — GIVEN Rarity Rare, THEN `GrantedCards.Count == 3` is required at import.
- **AC-VP18** — GIVEN Rarity Legendary, THEN `GrantedCards.Count == 3` is required at import.
- **AC-VP19** — GIVEN two installed parts granting duplicate card instances of the same `CardDefinitionSO`, WHEN one part is scrapped, THEN only its instance is removed from deck zones; the other remains.

**Damage application (F-VP2):**

- **AC-VP20** — GIVEN a Scout Hull slot with `Hp = 16, PlatingStacks = 3`, WHEN `ApplyDamage("hull_0", 5, Card)` is called, THEN `PlatingStacks == 0` and `Hp == 14`.
- **AC-VP21** — GIVEN a Scout engine slot with `Hp = 12, PlatingStacks = 0` and 2 granted cards in the deck (1 in hand, 1 in discard), WHEN `ApplyDamage("engine_0", 13, Card)` is called, THEN `Hp == 0`, `DamageState == Offline`, `OnSlotDamageStateChanged("engine_0", Functional, Offline)` fires exactly once (not twice), the granted cards **remain in their current zones** (deck/hand/discard counts unchanged), `OnGrantedCardRemoved` does **NOT** fire, and the cards' playability state becomes `false` via the source-slot gate. WHEN `Repair("engine_0", 6, canReviveOffline:true)` subsequently restores the slot to Degraded, the cards' playability returns to gate-determined (positional + energy still apply) without zone movement, and `OnGrantedCardRemoved` still does not fire. Closes R6 Blocker 1; aligns with Decision 16 (ADR-0007) soft-disable semantics.

- **AC-VP21b** (hard-removal twin) — GIVEN the same Scout engine slot with 2 granted cards across zones, WHEN the player scraps the part at a Workshop (V&P R6 boundary) OR the source is externally terminated (e.g., Dredge Javelin chain cut), THEN `HardRemoveCards(sourceSlotId, [cardIds])` is invoked, deck/hand/discard are atomically swept of those card instances, `OnGrantedCardRemoved` fires exactly once with the full card list, and the U6 "left your deck" notification renders (distinct phrasing from the U6 "disabled" notification).
- **AC-VP22** — GIVEN a slot in Functional at `Hp == DegradedThreshold + 1`, WHEN `ApplyDamage` drops `Hp` to the threshold, THEN `OnSlotDamageStateChanged(slotId, Functional, Degraded)` fires.

**Repair:**

- **AC-VP23** — GIVEN a slot `DamageState == Offline`, WHEN `Repair(slotId, 5, canReviveOffline=false)` is called, THEN `Hp == 0` (no-op), `DamageState == Offline`, and no event fires.
- **AC-VP24** — GIVEN a slot `DamageState == Offline, Hp = 0` AND its granted cards distributed across deck/hand/discard zones (soft-disabled per Decision 16), WHEN `Repair(slotId, 5, canReviveOffline=true)` is called, THEN `Hp == 5`, `DamageState == Degraded` (if `5 <= DegradedThreshold`), the granted cards **remain in their pre-repair zones** (deck/hand/discard counts unchanged — no movement to discard pile), `OnGrantedCardRemoved` does NOT fire, `OnSlotHpChanged(slotId, 5, MaxHp)` fires (per Decision 12), `OnSlotDamageStateChanged(slotId, Offline, Degraded)` fires, and the cards' playability state returns to gate-determined (positional + energy still apply). Aligns with Decision 16 (ADR-0007) soft-disable-lift semantics; supersedes the pre-R7 wording that erroneously asserted cards were added to discard on revival.
- **AC-VP25** — GIVEN `Repair` with amount exceeding `MaxHp`, WHEN called, THEN `Hp` is clamped to `MaxHp`.

**Plating:**

- **AC-VP26** — GIVEN a part with `MaxPlating = 3, PlatingStacks = 1`, WHEN `AddPlating(slotId, 10)` is called, THEN `PlatingStacks == 3` (clamped).
- **AC-VP27** — GIVEN a Scout Hull slot at `Hp = 16, PlatingStacks = 3` AND Corroded with 2 stacks active on the slot, WHEN Card Combat invokes `ApplyDamage("hull_0", 3, Card)`, THEN the post-state is `PlatingStacks == 0` AND `Hp == 14`. Math trace: Corrode amp adds 2 → effective `amount = 5`, plating subtracts 3 → `2` remaining to Hp, Hp goes `16 → 14`. The plating-then-Hp order is verified by asserting `PlatingStacks` hits 0 BEFORE Hp drops (equivalent: an alternate-order implementation that subtracted Hp first would yield `Hp == 13, PlatingStacks == 0` — that output MUST cause the test to fail).
- **AC-VP27b** — GIVEN an Armor slot (`Kind == Armor`), WHEN `AddPlating(armorSlotId, 1)` is called, THEN the call is a no-op and `PlatingStacks` remains 0 (Plating not applicable to Armor slots — R_ARM).

**Stat modifier composition (F-VP3):**

- **AC-VP28** — GIVEN base `MaxEnergy = 3`, an engine part grants `+1 Add`, a mobility part grants `×1.25 Multiply`, no Override, WHEN `GetStatModifier(MaxEnergy)` is called, THEN it returns 5.0.
- **AC-VP29** — GIVEN two parts with Override on the same stat, WHEN imported into a ChassisDefinitionSO together, THEN SO import validation fails (EC-VP16 static case).

**State transitions / events:**

- **AC-VP30** — GIVEN AoE damage hitting multiple slots simultaneously on the Small Frame layout, WHEN applied, THEN events fire in `FrameLayoutSO.Slots` index order (deterministic; identical across runs with identical layout assets), not in `SlotKind` enum order.
- **AC-VP31** — GIVEN a slot transition Functional → Offline in one hit, WHEN the damage resolves, THEN exactly one `OnSlotDamageStateChanged(slotId, Functional, Offline)` event fires (no intermediate Degraded event).

**Death:**

- **AC-VP32** — GIVEN the last `IsStructural == true` slot transitions to Offline mid-card-resolution, WHEN the card finishes resolving, THEN `IsDead == true`, `StructuralHp == 0`, and Card Combat's end-of-damage-step hook observes `IsDead` once per combat (not per subsequent damage tick).

**Save round-trip:**

- **AC-VP33** — GIVEN a Vehicle POCO with `FrameLayoutId`, installed parts, `Hp` values across all states, plating, `CombatsSurvived`, and active statuses, WHEN serialized and deserialized, THEN all field values match pre-serialization, `DamageState` re-derives identically from `Hp`, and `Slots.Count` matches `FrameLayoutSO.Slots.Count` for the loaded `FrameLayoutId`.
- **AC-VP33b** — GIVEN a V1 `VehicleStateDTO` save (pre-ADR-0007, no `LayoutId` field), WHEN loaded under V2, THEN the migration sets `LayoutId = "small_frame"` for Scout vehicles and `LayoutId = "medium_frame"` for Assault vehicles, the legacy 4-slot Frame entry is renamed to `hull_0`, legacy SlotMaxHp values are preserved as `MaxHp`, and a single load-time warning is logged (per ADR-0007 migration spec).

**Emotional Attachment (P4 design test):**

- **AC-VP34** — GIVEN a Rare engine part with `GrantedCards = [{CardId: "engine_burst_a"}, {CardId: "engine_burst_b"}, {CardId: "engine_burst_c"}]` installed on `engine_0` AND each granted card present once in the deck (deck size = 13: 10 starter + 3 grants), WHEN the engine slot goes Offline in combat (Hp → 0 via damage), THEN per Decision 16 (soft-disable): all three granted cards REMAIN in their current zones (deck count = 13, byCardId count unchanged) AND each granted card's `IsPlayable` evaluates to `false` via the source-slot playability gate (deck-inspection UI shows them greyed) AND `OnGrantedCardRemoved` does NOT fire. The original R2 wording ("removed granted cards… visibly changed") was stale per ADR-0007 Decision 16 — soft disable replaces removal-on-Offline. See AC-VP44 + AC-VP44d for the per-event firing assertions; see AC-VP44b for the scrap-path hard-removal test.
- **AC-VP35** — *(Relocated to "Design Test / Pillar Alignment" subsection below per R5 creative-director hybrid ruling — see AC-VP-DT1.)*

**Armor slot mechanic (R_ARM):**

- **AC-VP36** — GIVEN the Dredge boss layout, WHEN constructed, THEN `Slots` contains exactly 10 entries including `armor_chest` and `armor_back` with `Kind == Armor`, `ExposureMultiplier == 3.0`, and `RedirectsToSlotId == "hull_0"` (R_FL.3).
- **AC-VP37** — GIVEN an intact Armor slot (`armor_chest`, `Hp = 8`), WHEN `ApplyDamage("armor_chest", 5, Card)` is called, THEN `Hp == 3`, no damage propagates to `hull_0`, and `DamageState == Functional` (or Degraded depending on threshold).
- **AC-VP38** — GIVEN an Armor slot at `Hp = 3` with `ExposureMultiplier = 3.0` and `RedirectsToSlotId = "hull_0"`, WHEN `ApplyDamage("armor_chest", 10, Card)` is called, THEN `armor_chest.Hp == 0`, `DamageState == Offline`, `OnArmorExposed("armor_chest", "hull_0")` fires once, and `hull_0` receives `(10 - 3) × 3.0 = 21` damage (overflow × exposure multiplier).
- **AC-VP39** — GIVEN an Armor slot at `Hp = 0` (already exposed), WHEN `ApplyDamage("armor_chest", 4, Card)` is called, THEN `armor_chest` is unchanged (no further events), and `hull_0` receives `4 × 3.0 = 12` damage routed through the destroyed Armor slot.
- **AC-VP40** — GIVEN a Dredge vehicle with `armor_chest.Hp = 8` AND `hull_0.Hp = 30` AND a `Burning` status with 4 stacks active on `hull_0` (per Status Effects F2: tick = `2 × stacks = 8` per turn), WHEN the start-of-turn DOT tick resolves and applies `8` damage with `DamageSource == Status` to `hull_0`, THEN `armor_chest.Hp == 8` (unchanged), `hull_0.Hp == 22` (full tick), no `OnArmorExposed` fires, and no redirect-amplify path runs. (DOT bypasses Armor per R_ARM.4 — the redirect-on-exposure path is gated on `DamageSource == Card`.) An alternate-implementation regression that routed status DOTs through Armor would produce `hull_0.Hp == 30 - (8 - 8) × 3.0 = 30` (no Hull damage) — that output MUST cause the test to fail.
- **AC-VP41** — GIVEN an Empty Armor slot (`InstalledPart == null`, designer-authored Armor plate not installed), WHEN damage targets the underlying `RedirectsToSlotId`, THEN damage applies directly to the underlying slot with NO exposure multiplier (Empty Armor = no plate = no protection AND no amplification — R_ARM.2 Empty branch).
- **AC-VP42** — GIVEN any enemy vehicle whose layout contains Armor slots, WHEN inspected via `IVehicleView`, THEN those Armor slots appear in `Slots` with the same `SlotInstance` schema as the player vehicle (symmetric — no enemy-specific Armor type or aggregate field).

**R9 interface contract (closes Review 2 blockers #2, #3, #4):**

- **AC-VP43** — GIVEN any slot at `Hp = N`, WHEN `ApplyDamage(slotId, k, Card)` is called where `k > 0` AND `N - k >= 0`, THEN `OnSlotHpChanged(slotId, N - k, slot.MaxHp)` fires exactly once with the post-write `Hp` value. The event fires regardless of whether `DamageState` transitioned.
- **AC-VP44 — IsDead ordering + no `OnGrantedCardRemoved` on Offline.** GIVEN a single-Hull vehicle with `hull_0.Hp = 1` AND `hull_0.InstalledPart.GrantedCards = [{CardId: "hull_brace"}]` AND `"hull_brace"` present in deck/hand/discard zones, WHEN `ApplyDamage("hull_0", 5, Card)` is called, THEN the event order is: `OnSlotHpChanged("hull_0", 0, MaxHp)`, then `OnSlotDamageStateChanged("hull_0", Functional, Offline)`, then `OnVehicleDied()`. A subscriber to `OnSlotDamageStateChanged` that reads `vehicle.IsDead` from inside its handler observes `false` (backing-field write has not yet occurred); a subscriber to `OnVehicleDied` that reads `vehicle.IsDead` observes `true`. **`OnGrantedCardRemoved` does NOT fire during this sequence (per ADR-0007 Decision 16 — slot Offline is soft disable; deck/hand/discard counts for `"hull_brace"` are UNCHANGED before vs after the damage application).**
- **AC-VP44b — Hard removal sweep (scrap path).** GIVEN a part installed at `weapon_left` with `GrantedCards = [{CardId: "torch_swing"}]`, AND `"torch_swing"` distributed as: 1 copy in deck, 1 copy in hand, 1 copy in discard, AND a non-matching `"normal_card"` also present in all three zones, WHEN `IVehicleMutator.HardRemoveCards("weapon_left", ["torch_swing"])` is invoked, THEN all three `"torch_swing"` instances are removed atomically in one frame, all three `"normal_card"` instances remain untouched, and exactly one `OnGrantedCardRemoved("weapon_left", ["torch_swing"])` event fires after the sweep. The currently-resolving card stack is excluded per EC-VP12.
- **AC-VP44c — Null `sourceSlotId` external-source path.** GIVEN a tether cohort `["tether_a", "tether_b"]` with `SourceSlotId = null` distributed across deck (`tether_a`) and discard (`tether_b`), WHEN `IVehicleMutator.HardRemoveCards(null, ["tether_a", "tether_b"])` is invoked, THEN both instances are removed and exactly one `OnGrantedCardRemoved(null, ["tether_a", "tether_b"])` event fires; the subscriber receives the `null` first argument without `NullReferenceException`. The signature contract is `Action<string?, IReadOnlyList<string>>` on `IVehicleView.OnGrantedCardRemoved`; the V&P GDD reviewer MUST reject any commit that changes the first argument from nullable.
- **AC-VP44d — Soft-disable repair revival.** GIVEN the AC-VP44 setup (Hull Offline with `"hull_brace"` in zones) BUT a non-fatal hit (multi-structural layout where Hull Offline does not trigger death), WHEN the player subsequently calls `Repair("hull_0", n, canReviveOffline=true)` enough to cross Hull into Functional or Degraded, THEN per Decision 12: `OnSlotHpChanged("hull_0", newHp, MaxHp)` fires, then `OnSlotDamageStateChanged("hull_0", Offline, <newState>)` fires. A subscriber that re-evaluates source-slot-gate playability for `"hull_brace"` after the revival event observes `IsPlayable == true`; the card has not moved zones across either the damage or repair transitions. (Implements the Decision 16 soft-disable + Decision 12 repair-emission contract end-to-end.)
- **AC-VP45** — GIVEN `vehicle.IsDead == true` after the first defeat-triggering hit, WHEN any subsequent `ApplyDamage` call lands on any structural slot already at `Hp = 0`, THEN `OnVehicleDied` does NOT re-fire (the false → true transition guard prevents re-emission).
- **AC-VP46** — GIVEN any `IVehicleView` instance, WHEN `IPartCatalog.GetParts(SlotKind.Weapon, Rare, Scout)` is called and no matching assets exist, THEN the return value is an empty `IReadOnlyList<IPartData>` (not `null`), and the return type signature is engine-free (no `PartDefinitionSO` in the return type). Reviewers MUST fail any commit that reintroduces a `PartDefinitionSO` return on `IPartCatalog`.
- **AC-VP47** — GIVEN a part installation, WHEN `OnPartInstalled` fires, THEN the event payload type is `Action<string, string>` (slotId, partId) — not `Action<string, IPartData>` or `Action<string, PartDefinitionSO>`. Same constraint applies to `OnPartRemoved`.

**Boundary-value safety (closes Review 2 blockers #5, #6, #7 + Review 3 blockers B5, B7):**

The six R_FL.1 OnValidate rules each get their own AC. AC-VP48 was previously a single AC; it is now split into AC-VP48a–f, one per rule. (Closes Review 3 B8.)

- **AC-VP48a — Unique SlotIds.** GIVEN a `FrameLayoutSO` with two `SlotDefinition` entries sharing the same `SlotId` (including empty strings), WHEN the asset is imported, THEN `OnValidate` rejects the asset with a designer-readable error naming the duplicate SlotId and both slot indices.
- **AC-VP48b — At least one structural slot.** GIVEN a `FrameLayoutSO` whose every slot resolves `IsStructural == false` (e.g., Hull slot manually overridden to non-structural and no other override compensates), WHEN imported, THEN `OnValidate` rejects with a designer-readable error stating that the layout would spawn a vehicle dead at `StructuralHp == 0`.
- **AC-VP48c — Armor `RedirectsToSlotId` well-formed.** GIVEN an Armor slot whose `RedirectsToSlotId` is (1) null/empty, (2) equal to its own SlotId, (3) references a non-existent SlotId, or (4) references another Armor slot, WHEN imported, THEN `OnValidate` rejects with a distinct error for each case. Specifically: the Armor → Armor case must be rejected — a layout with `armor_a.RedirectsToSlotId = "armor_b"` AND `armor_b.RedirectsToSlotId = "hull_0"` fails import even though the eventual root is structural. (Closes Review 3 B7.)
- **AC-VP48d — `ExposureMultiplier` finite and positive.** GIVEN an Armor slot whose `ExposureMultiplier` is `≤ 0`, `NaN`, or `±Infinity`, WHEN imported, THEN `OnValidate` rejects. `ExposureMultiplier ∈ (0, 1.0)` produces a warning (not an error). `ExposureMultiplier > 5.0` also warns.
- **AC-VP48e — `HudAnchor` finite and in unit range.** GIVEN any slot whose `HudAnchor.X` or `HudAnchor.Y` is `NaN`, `±Infinity`, or outside `[0, 1]`, WHEN imported, THEN `OnValidate` rejects. The `AnchorPoint.IsInUnitRect` helper is the canonical check; tests use it directly.
- **AC-VP48f — HP boundaries reject zero-width Functional band.** GIVEN a `FrameLayoutSO` whose `DegradedThresholdPct` is `0`, `100`, negative, or `> 100`, WHEN imported, THEN `OnValidate` rejects. GIVEN an Armor slot whose `MaxHpOverride` is null or `< 1`, WHEN imported, THEN `OnValidate` rejects (the Dredge `dredge_frame` SO must fail import until both `armor_chest.MaxHpOverride` and `armor_back.MaxHpOverride` are `≥ 1`). GIVEN a non-Armor slot whose `MaxHpOverride` is non-null and `< 1`, WHEN imported, THEN `OnValidate` rejects. (Closes Review 3 B5; supersedes the original single-AC AC-VP48.)
- **AC-VP49** — GIVEN a Weapon slot with `MaxHp = 1` on a layout with `DegradedThresholdPct = 50`, WHEN `DegradedThreshold(slot)` is computed (F-VP1), THEN the result is `1` (the `max(1, floor(0.5))` clamp), not `0`. Slot at `Hp = 1` is Degraded; slot at `Hp = 0` is Offline. The Degraded band is reachable. (Authoring inputs that would create this degenerate edge are themselves rejected by AC-VP48f; AC-VP49 covers the floor-clamp inside F-VP1 for any survivor case after import.)
- **AC-VP50** — GIVEN an exposed Armor slot with `ExposureMultiplier = 3.0` and an incoming `amount = 1_000_000_000` (one billion, larger than `int.MaxValue / 3`), WHEN `ApplyDamage` resolves through `SafeAmplify`, THEN the `redirected_amount` delivered to the `RedirectsToSlotId` slot equals `int.MaxValue` (2,147,483,647), not a negative wrapped value. Guards against the C# narrowing-cast overflow path.
- **AC-VP50b — SafeAmplify NaN/Infinity fallback.** GIVEN an exposed Armor slot whose `ExposureMultiplier` field has been set to one of `{float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0f, -1f}` via the test fixture's reflection-based runtime corruption helper (`typeof(SlotInstance).GetField("_exposureMultiplier", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(slot, corruptValue)`) — this deliberately bypasses the `OnValidate` rejection that an authoring-time path would hit per AC-VP48d, simulating a deserialization bug or debug-command write that landed a corrupt value at runtime, WHEN `ApplyDamage` resolves through `SafeAmplify` with `amount = 10`, THEN for every value in the set: `redirected_amount` delivered to the `RedirectsToSlotId` slot equals `10` (the unamplified fallback) AND exactly one warning is logged via `WastelandRun.Diagnostics.LogWarn` containing the corrupt multiplier value AND no negative or `int.MinValue` value reaches the redirect target. Test is parameterized via NUnit `[TestCase]` across all five corrupt values. The injection helper is the canonical test-fixture path for forcing runtime corruption of any `OnValidate`-guarded field; reviewers MUST reject any test that uses `Reflection.Emit`, `unsafe` blocks, or `SerializationInfo` rehydration for this corruption — those would test the deserializer's behavior, not `SafeAmplify`'s. (Closes Review 3 B6 + R5 blocker #17.)

**Recommended-item additions (#15, #17):**

- **AC-VP51** — GIVEN a vehicle with at least one `IsStructural == true` slot in `Degraded` or `Offline` state, WHEN `vehicle.CriticalState` is read, THEN it returns `true` — regardless of how many other structural slots are still `Functional` (or whether *all* structural slots are Degraded/Offline). Conversely, given all structural slots are `Functional` (or `Empty`), `CriticalState` returns `false`. This matches R9's canonical definition. (The prior AC-VP51 wording required an additional "AND ≥1 structural still Functional" conjunct, which contradicted R9 and would have inverted CriticalState to `false` in the most critical case — every structural slot Degraded but none yet dead. The conjunct is removed. Closes Review 3 B8 + Review 2 recommended #15.)
- **AC-VP51b — OnCriticalStateChanged fires on transition.** GIVEN a vehicle whose `CriticalState` was `false` immediately before a damage application, WHEN a slot transitions to `Degraded` (or `Offline`) inside F-VP2 Step 4 such that the post-state `CriticalState` is `true`, THEN `OnCriticalStateChanged(true)` fires exactly once, after `OnVehicleDied` (if any) and after all per-slot events. Symmetric: when repair pushes the vehicle out of CriticalState, `OnCriticalStateChanged(false)` fires once. The event does NOT fire on damage applications that change `CriticalState`'s value by `0` (i.e., already-critical vehicle takes further damage). (Closes Review 3 B4.)
- **AC-VP52** — GIVEN a `FrameLayoutSO`, WHEN inspected via `IFrameLayout.Slots`, THEN every `SlotDefinition` exposes a `HudAnchor: AnchorPoint` field where `HudAnchor.IsInUnitRect == true`, and the Combat HUD iterates `IVehicleView.Slots` to read `HudAnchor` for placement (no hardcoded 4-slot positions remain). `HudAnchor` is also copied onto each `SlotInstance` at vehicle construction so UI subscribers can read it without a layout round-trip. The struct is engine-free (`AnchorPoint`, not `UnityEngine.Vector2`); CI must fail any commit that reintroduces `Vector2` as the `HudAnchor` type inside `WastelandRun.Vehicle.asmdef`. (Closes Review 2 recommended #17 + Review 3 B2.)

**R5 coverage gaps (Decisions 12 / 14 / 15 / 16 — closes R5 #1, #2, #3, #5, #6, #18.1, #18.2):**

- **AC-VP53 — Recursive `ApplyDamage` captures fresh `wasCritical` per invocation (Decision 15).** GIVEN a vehicle with `armor_chest.Hp = 2, ExposureMultiplier = 3.0, RedirectsToSlotId = "hull_0"` (Armor non-structural), `hull_0.Hp = 16, MaxHp = 16, DegradedThreshold = 8` (Functional), AND `vehicle.CriticalState == false` pre-damage, WHEN `ApplyDamage("armor_chest", 5, Card)` resolves (armor absorbs 2 → Offline, overflow = 3, redirected = `3 × 3.0 = 9` to hull_0, Hull `16 → 7` → Degraded), THEN the test fixture captures both `wasCritical` snapshots via a hook on `DamageContext` and asserts: (a) outer invocation's `wasCriticalOuter == false` (read at outer-call entry, pre-armor-damage), (b) recursive invocation's `wasCriticalInner == false` (read at RECURSIVE-call entry, AFTER armor went Offline but BEFORE hull damage; equals `false` because Armor is non-structural so the Armor → Offline transition did not flip vehicle CriticalState), (c) post-resolution `vehicle.CriticalState == true`, (d) `OnCriticalStateChanged(true)` fires exactly once via the top-level `DamageContext.CriticalEventFiredThisCall` idempotency check. A regression that propagated `wasCriticalOuter` into the recursive call instead of capturing fresh would produce identical results on this test (both snapshots are `false`) — the actual falsifier for the "propagate vs capture-fresh" bug is AC-VP59b. AC-VP53's job is to assert the two-snapshot capture pattern exists and is observable by the test harness. Closes R5 #18.1.
- **AC-VP54 — `IGrantedCardData` and engine-free Vehicle assembly placement (CI-gated).** GIVEN the project compiles successfully, WHEN CI scans the `assets/scripts/Vehicle/` directory (everything under the `WastelandRun.Vehicle.asmdef` scope), THEN: (a) `IGrantedCardData` interface declaration appears in exactly one `.cs` file under that assembly (`grep -lr "interface IGrantedCardData" assets/scripts/Vehicle/ | wc -l == 1`), (b) zero files in that assembly contain `using UnityEngine` or `using UnityEngine.*` tokens (`grep -rE "^using UnityEngine" assets/scripts/Vehicle/` returns empty), (c) the `WastelandRun.Vehicle.asmdef` JSON contains `"noEngineReferences": true`, (d) the same scan applied to `WastelandRun.Cards.asmdef` scope (per ADR-0006) returns the same engine-free invariant. CI fails the build if any of (a)–(d) is violated. The grep gates are spec'd in `tools/ci/check-engine-free-vehicle-cards.sh` (not yet implemented — Phase 4 / tooling sprint). Closes R5 #18.2.
- **AC-VP55 — Repair path emits `OnSlotHpChanged` + `OnSlotDamageStateChanged` on boundary cross (Decision 12).** GIVEN a structural slot at `Hp = 4, MaxHp = 16, DegradedThreshold = 8` (Degraded), `InstalledPart != null`, `DamageState == Degraded`, WHEN `Repair(slotId, 6, canReviveOffline=false)` is called, THEN the post-state is `Hp == 10, DamageState == Functional` AND events fire in this order: `OnSlotHpChanged(slotId, 10, 16)`, then `OnSlotDamageStateChanged(slotId, Degraded, Functional)`. WHEN the same slot at `Hp = 4` receives `Repair(slotId, 2, canReviveOffline=false)` (no boundary cross; Hp 4 → 6, still Degraded), THEN only `OnSlotHpChanged(slotId, 6, 16)` fires; `OnSlotDamageStateChanged` does NOT fire. The no-op repair case (`Hp == 0, canReviveOffline=false`) MUST NOT fire either event (per AC-VP23). Closes R5 #3.
- **AC-VP56 — `OnPlatingChanged` firing.** GIVEN a slot at `PlatingStacks = 1, MaxPlating = 3`, WHEN `AddPlating(slotId, 2)` is called, THEN `PlatingStacks == 3` AND `OnPlatingChanged(slotId, 3)` fires exactly once with the post-write stack count. WHEN a subsequent `ApplyDamage(slotId, 2, Card)` (no Corrode, plating subtracts 2) reduces `PlatingStacks` to `1`, THEN `OnPlatingChanged(slotId, 1)` fires exactly once. WHEN `AddPlating(slotId, 0)` is called (no-op), THEN `OnPlatingChanged` MUST NOT fire (delta = 0). A subscriber that reads `slot.PlatingStacks` from inside the handler observes the post-write value. Closes R5 #5.
- **AC-VP57 — `OnStatusStackChanged` firing.** GIVEN a slot with no active Burning status, WHEN Status Effects calls the Vehicle mutator to apply Burning at 3 stacks via the registered application path (e.g., `vehicle.ApplyStatus(slotId, StatusType.Burning, 3, durationTicks: 2)`), THEN `OnStatusStackChanged(slotId, StatusType.Burning, 3)` fires exactly once with the post-write stack count. WHEN a subsequent tick decrements to 2 stacks, THEN `OnStatusStackChanged(slotId, StatusType.Burning, 2)` fires once. WHEN a removal drops stacks to 0, THEN `OnStatusStackChanged(slotId, StatusType.Burning, 0)` fires once AND the status entry is removed from `vehicle.ActiveStatuses`. A no-delta refresh (re-apply 2 stacks to a slot already at 2 stacks; assume Status Effects rules say "max of current and new" and current wins) MUST NOT fire the event. Closes R5 #6.
- **AC-VP58 — Armor INTACT branch event emission (Decision 14).** GIVEN a Dredge `armor_chest` at `Hp = 12, MaxHp = 16, DegradedThresholdPct = 50` (DegradedThreshold = 8), `PlatingStacks = 0` (Plating is no-op on Armor per AC-VP27b), `DamageState == Functional`, AND `hull_0.Hp = 30`, WHEN `ApplyDamage("armor_chest", 5, Card)` is called (armor absorbs the full 5 without breakthrough; armor stays INTACT — Hp > 0), THEN the post-state is `armor_chest.Hp == 7, DamageState == Degraded` AND events fire in this order: `OnSlotHpChanged("armor_chest", 7, 16)`, `OnSlotDamageStateChanged("armor_chest", Functional, Degraded)`. NO `OnArmorExposed` fires (armor Hp > 0, no breakthrough). NO redirect to `hull_0` runs (no overflow). `hull_0.Hp == 30` (unchanged). The R5 #1 regression — Armor INTACT branch RETURNED early before F-VP2 Steps 3/4 — is detected by this AC: a regression yields zero events fired despite Hp dropping `12 → 7`. Closes R5 #1.
- **AC-VP59 — Armor breakthrough recursion crosses Critical (Decision 15 forward case).** GIVEN a Dredge layout with `armor_chest.Hp = 1, ExposureMultiplier = 3.0, RedirectsToSlotId = "hull_0"` (non-structural), `hull_0.Hp = 9, MaxHp = 16, DegradedThreshold = 8` (Functional, ONE above threshold), AND `vehicle.CriticalState == false` (no structural slot at or below threshold), WHEN `ApplyDamage("armor_chest", 2, Card)` is called (armor takes 1 → Offline, overflow = 1, redirected = `1 × 3.0 = 3` to `hull_0`, Hull `9 → 6` = Degraded, CROSSES threshold), THEN the captured-fresh `wasCritical` discipline produces: outer call captured `wasCriticalOuter = false`; recursive call captured `wasCriticalInner = false` (read at entry to the recursive invocation, AFTER armor Offline but BEFORE the 3-damage applied to Hull); post-resolution `CriticalState == true`; `OnCriticalStateChanged(true)` fires exactly once via the top-level `DamageContext.CriticalEventFiredThisCall` idempotency check. Closes R5 #2 forward case.
- **AC-VP59b — Already-Critical no re-fire (Decision 15 regression detector).** GIVEN the same Armor + Hull layout as AC-VP59 BUT `hull_0.Hp = 7` (already Degraded; vehicle `CriticalState == true` pre-damage), WHEN `ApplyDamage("armor_chest", 2, Card)` resolves (Hull `7 → 4`, still Degraded; CriticalState delta = 0), THEN `OnCriticalStateChanged` MUST NOT fire. The recursion captures `wasCriticalInner = true` (correctly reflecting Hull's Degraded state at entry to the recursive call) AND the per-invocation comparison `current.CriticalState == wasCritical` produces no transition. A regression that always captured the OUTERMOST `wasCritical = false` (e.g., from an entry-point snapshot taken before any damage) and propagated it downward would fire `OnCriticalStateChanged(true)` here — that output MUST cause the test to fail. Together AC-VP59 + AC-VP59b assert the captured-fresh-per-recursion invariant: the forward case proves fresh capture detects the transition; the regression case proves fresh capture suppresses spurious events.

**Design Test / Pillar Alignment:**

*Per R5 creative-director hybrid ruling: ACs that test qualitative balance hypotheses are relocated here rather than left in the unit-test AC section in untestable form. These are tested by telemetry-backed playtest evidence under `production/qa/evidence/`, NOT by CI.*

- **AC-VP-DT1 — M3 scarcity pillar (relocates former AC-VP35).** GIVEN a playtest cohort of 5 players each running 3 consecutive runs (15 runs total) with the default Loot drop tables and the Scout chassis, AND each run instrumented with telemetry recording: (a) the combat index of each non-Hull slot damage event (slot transitioned to Degraded or Offline), (b) the combat index of the first replacement-Part offer matching that slot's `Kind` after the damage event, WHEN aggregated across the 15 runs, THEN at least 60% of `(damage event, next matching replacement offer)` pairs MUST have ≥3 combats between them — this tests whether Loot drop frequency produces the "scarcity" subjective feel the M3 pillar targets. Failure indicates Loot drop rates need rebalancing (game-designer + economy-designer), NOT that the V&P system is broken. Evidence: telemetry CSV + a one-paragraph designer assessment under `production/qa/evidence/m3-scarcity-[date].md`. CI does NOT gate this AC; release-gate review (per `.claude/skills/gate-check/`) checks for presence and recency of the evidence file at major-milestone gates.

---

**Testing notes:**

- AC-VP34 is testable via EditMode unit test with a seeded loot and deck fixture; assertions are on zone counts and the soft-disable `IsPlayable` evaluation (per Decision 16).
- AC-VP35 is relocated to the **Design Test / Pillar Alignment** subsection (AC-VP-DT1) per R5 creative-director hybrid ruling. The original "playtest AC not automated" framing is preserved there with concrete cohort parameters.
- AC-VP43 through AC-VP52 + AC-VP53 through AC-VP59b are all automatable unit tests via the POCO plus event capture. The full automatable Review-3-and-R5-extended set is: AC-VP43, AC-VP44, AC-VP44b, AC-VP44c, AC-VP44d, AC-VP45, AC-VP46, AC-VP47, AC-VP48a–f, AC-VP49, AC-VP50, AC-VP50b, AC-VP51, AC-VP51b, AC-VP52, AC-VP53, AC-VP55, AC-VP56, AC-VP57, AC-VP58, AC-VP59, AC-VP59b.
- CI grep gates: AC-VP46 + AC-VP47 (no `PartDefinitionSO` token in `WastelandRun.Vehicle.asmdef` files); AC-VP52 (no `UnityEngine.Vector2` as `HudAnchor` type inside the engine-free assembly); AC-VP54 (no `using UnityEngine` tokens inside `WastelandRun.Vehicle.asmdef` OR `WastelandRun.Cards.asmdef` scope; `IGrantedCardData` declared exactly once under Vehicle assembly).
- AC-VP53's two-snapshot capture is observable via a `DamageContext` test hook the implementation MUST expose to EditMode tests (internal-with-`InternalsVisibleTo` is sufficient; reflection access is acceptable for the test fixture).
- AC-VP50b uses a reflection-based runtime corruption helper (spec'd in the AC body); reviewers MUST reject alternative injection methods (`Reflection.Emit`, `unsafe`, `SerializationInfo` rehydration) that would test the wrong code path.
- All other ACs are automatable unit tests via the POCO plus a fake Deck dependency.

## Open Questions

Surfaced during authoring; carried forward for resolution by named downstream GDDs.

**OQ-VP1 — Mastery track schema** *(forward dep on Meta Progression GDD)*. `ChassisDefinitionSO` will need a `MasteryUnlocks` field post-MVP. Deferred because authoring the field now would lock a schema without a GDD. Resolve when Meta Progression GDD begins.

**OQ-VP2 — Override collision at runtime install** *(forward dep on Scrap Economy GDD)*. Two installed parts can define `Override` operations on the same `TargetStat`. Static case (both parts in a starter chassis) is caught at SO import (AC-VP29). Runtime case: the player installs a part with an Override while another installed part already has one. Options:

- (a) Scrap Economy UI prevents the install (preferred — fail closed, visible to player).
- (b) Install succeeds; second Override silently wins (last-install-wins).
- (c) Install fails at runtime with an exception (bad UX — player sees a crash).

Decision deferred to Scrap Economy GDD. (a) is the strong recommendation.

**OQ-VP3 — All-slots-Empty edge case (CLOSED 2026-05-18 by ADR-0007).** Resolved by the `FrameLayoutSO` validator requirement that every layout contain at least one `IsStructural == true` slot AND by `ChassisDefinitionSO` import validation requiring `StarterParts` to populate every structural slot. A vehicle therefore cannot be constructed with all structural slots Empty. The runtime `IsDead = (StructuralHp == 0)` formula correctly handles the only legal Empty path (a structural part scrapped at Chopshop — see EC-VP17 for the gating rule that Scrap Economy must surface a "structural slot cannot be left Empty in combat" check).

**OQ-VP4 — Mid-resolution deck zone mutation semantics** *(forward dep on Card Combat GDD)*. EC-VP12 specifies that granted cards are removed from deck/hand/discard but NOT from the currently-resolving card stack. The exact "currently-resolving card stack" is a Card Combat concept — this GDD asserts the rule but does not define the stack. Requires Card Combat GDD to define the stack and the removal-exclusion check.

**OQ-VP5 — Stat round/floor convention** *(forward dep on Card Combat / Scrap Economy GDDs)*. F-VP3 returns `float`. Each consumer decides rounding for int-typed stats (e.g., `MaxEnergy`). A single shared helper is preferable to scattered conventions. Resolve when the first int-consuming stat is implemented.

---

**Closed in this GDD:**

- **Status Effects OQ-SE2** — `DamageSource` discriminator. Closed by R9's `enum DamageSource { Card, Status, Environment }`.
- **OQ-VP3 — All-slots-Empty edge case** — closed 2026-05-18 by ADR-0007 (FrameLayoutSO validator + ChassisDefinitionSO StarterParts requirement). See OQ-VP3 above.
- **Armor stress-test T-1** — combat-start Armor reset. Superseded by ADR-0007. Armor is now per-slot HP on `Kind == Armor` SlotInstances. There is no vehicle-level `CurrentArmor` to reset; per-slot Armor HP persists across combats just like Hull or Engine HP. Chopshop re-install exploit is prevented by Scrap Economy's normal scrap/repair pricing flow — not by a per-combat reset.
- **Armor stress-test T-4** — F-CC1 must fork on Hull vs non-Hull path. Superseded by ADR-0007. F-VP2 now forks on `Kind == Armor` vs everything else (Plating). Card Combat's F-CC1 should be rewritten to match this slot-Kind fork, not the old Frame-vs-non-Frame fork.
- **Armor stress-test T-6** — `RestoreArmorEffectSO` is distinct from `RestorePlatingEffectSO`. Superseded by ADR-0007. Armor restoration is now `Repair(armorSlotId, amount)` — same path as any other slot repair. No new effect type required; existing `RepairEffectSO` works as long as it accepts a `slotId` parameter (which it must under ADR-0007).

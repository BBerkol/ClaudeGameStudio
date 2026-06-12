# ADR-0010 Execution Plan — Atomic Slot Vocabulary Landing

**Status:** DRAFT awaiting user sign-off (2026-05-31).
**Supersedes (when landed):** `production/adr-0010-phase-plan.md` six-phase split.
**Charter:** project-wide no-bridges directive (`project_no_bridges_at_done`) +
ADR-0011 done-state rules.

## Why atomic

Under the user directive "no persisting bloated references, every step is for
end-product", every bridge surface across Phases 1–6 of the original phase plan
must land in a single atomic slice. A chunked landing would leave dormant
bimodal `Vehicle`/`Slot` internals on `main` between chunks — ADR-0011 forbids
dormant bridges as much as live ones. This document supersedes the six-phase
split and replaces it with **ordered execution stages A–L** that may be
committed individually on a feature branch but must merge to `main` as one
contiguous series with no intermediate bridge state ever publicly visible.

## Scope: IN

- Card data shape rewrite: `CardDefinition` → composition over polymorphic `CardEffect`.
- Intent data shape rewrite: `EnemyIntent` → composition over polymorphic `IntentEffect`.
- View layer: every `LegacySlotKind` reference replaced by `string slotId` reads via `Vehicle.GetSlotById` / `Vehicle.SlotInstances` / `Vehicle.StructuralSlot` (new accessor).
- Gameplay layer: every `LegacySlotKind` reference in `CombatLoop`, `DamagePipeline`, `IntentPool`, brain modifiers, `WeightModifier`, `CardPlayResult`, `EnemyTurnResult` flips to `string slotId`.
- `Vehicle.cs` simplification: drop bimodal plumbing; one ctor (`Vehicle(string, IFrameLayout)`); rename `EffectiveMaxArmor`→`MaxArmor`, `EffectiveCurrentArmor`→`CurrentArmor`.
- `Slot.cs` deleted entirely. `LegacySlotKind.cs` deleted. `SlotType.cs` deleted (`SlotKind` is canonical).
- `SlotDefinition.LegacyKindBridge` field deleted; `legacyKindBridge:` argument removed from all four `*FrameLayout.cs` files.
- All 27 test files migrated from `new Vehicle(name)` to `new Vehicle(name, layout)`; bridge-specific tests deleted.
- CI grep gate added: forbidden tokens `LegacySlotKind`, `LegacyKindBridge`, `IsLegacyMode`, `IsLayoutMode`, `_maxArmor`, `_currentArmor`, `EffectiveMaxArmor`, `EffectiveCurrentArmor`.
- `CombatPrefabAuthor` authoring path rebuilt for slotId-keyed hit zone structure.
- All four vehicle prefabs re-authored: PlayerVehicle, IronShepherd, DuneSkimmer, Dredge.
- Doc scrub: ADR-0009 superseded marker; "transitional"/"bridge"/"slice 2.7" comments removed from 10 bridge-defining files; `production/session-state/active.md` updated; ADR-0010 final amendment locking the as-shipped design.

## Scope: OUT (tracked separately)

- **`VehicleDefinitionSO` rebuild** — blocked on Part SO ADR (memory `project_part_so_blocker`). Stays in its legacy-named-fields shape. `CombatController.BuildScout()` static fallback covers gameplay when the asset is absent. **Flagged in code** with `// ADR-0011 exception: blocked on Part SO ADR (memory project_part_so_blocker)`. User commitment: tackle Part SO ADR as soon as a natural moment arises post-landing — reminder to fire on next session featuring vehicles/weapons/parts/rewards.

That is the only documented exception. Every other LegacySlotKind / bimodal surface lands.

---

## End-state shapes

### `CardDefinition` (composition over polymorphic `CardEffect`)

```csharp
public sealed class CardDefinition {
    public string Name { get; }
    public int    EnergyCost { get; }
    public string Description { get; }
    public IReadOnlyList<CardEffect> Effects { get; }

    public bool IsPlayable(CombatLoop loop)
        => Effects.All(e => e.CanResolve(loop));

    public void Resolve(CombatLoop loop) {
        foreach (var e in Effects) e.Apply(loop);
    }
}

public abstract class CardEffect {
    public abstract bool CanResolve(CombatLoop loop);
    public abstract void Apply(CombatLoop loop);
}

public sealed class WeaponAttackEffect : CardEffect {
    public string LaunchSlotId { get; }   // non-nullable
    public int    Damage       { get; }
    // CanResolve: loop.Player.GetSlotById(LaunchSlotId).DamageState != Offline
    // Apply: deal Damage to loop.Enemy via DamagePipeline; route from LaunchSlotId
}

public sealed class PlateEffect : CardEffect {
    public int ArmorGain { get; }
    // CanResolve: true
    // Apply: loop.Player.PlateArmor(ArmorGain)
}

public sealed class RepairEffect : CardEffect {
    public int RepairAmount { get; }
    // CanResolve: loop.Player has any slot with HasPart && Hp < MaxHp
    //             (target slotId picked at Apply time — first damaged non-structural slot)
    // Apply: loop.Player.RepairSlot(targetSlotId, RepairAmount)
}

public sealed class RepositionFlipEffect : CardEffect {
    // CanResolve: true
    // Apply: loop.Player.SetPosition(opposite of current)
}

public sealed class RepositionToEffect : CardEffect {
    public LanePosition Target { get; }   // non-nullable
    // CanResolve: loop.Player.Pos != Target  (fizzle gate)
    // Apply: loop.Player.SetPosition(Target)
}

public sealed class DrawEffect : CardEffect {
    public int Count { get; }
    // CanResolve: true
    // Apply: loop.Deck pulls Count cards into hand (over-cap allowed)
}

public sealed class BuffEffect : CardEffect {
    public BuffTag Tag { get; }
    // CanResolve: true
    // Apply: dispatch by Tag — FlameBarrier sets reduction%, etc.
}
```

- `CardKind` enum **deleted**. Effect type IS the discriminator.
- `LaunchSlotId` is set at card construction (StarterDecks today, weapon-install pipeline post-Part-SO).
- `RepositionFlipEffect` vs `RepositionToEffect` split deliberately to avoid `Target?` bimodal field.
- `RepairEffect` target picked at `Apply` time — fixed rule "first damaged non-structural" for Z''. Future targeting picker is a UI concern.

### `EnemyIntent` (composition over polymorphic `IntentEffect`)

```csharp
public readonly struct EnemyIntent {
    public IReadOnlyList<IntentEffect> Effects { get; }
    public string                Description { get; }
    public PositionRequirement?  Requires    { get; }   // intent-level gate; null=ungated
    public PositionBonus?        Bonus       { get; }   // intent-level conditional bonus; null=flat

    public bool CanResolve(CombatLoop loop)
        => (Requires == null || Requires.Value.IsSatisfied(loop))
           && Effects.All(e => e.CanResolve(loop));

    public int PreviewDamage(LanePosition enemyPos, LanePosition playerPos, int attackerDamageBonus = 0)
        => Effects.OfType<IntentAttackEffect>()
                  .Sum(a => a.Damage + (Bonus?.IsActiveAt(enemyPos, playerPos) == true ? Bonus.Value.Amount : 0))
           + attackerDamageBonus;
}

public abstract class IntentEffect {
    public abstract bool CanResolve(CombatLoop loop);
    public abstract void Apply(CombatLoop loop);
}

public sealed class IntentAttackEffect : IntentEffect {
    public string PoweredBySlotId { get; }   // enemy's own slot; non-nullable for attack effects
    public string TargetSlotId    { get; }   // player slot; non-nullable
    public int    Damage          { get; }
    // CanResolve: loop.Enemy.GetSlotById(PoweredBySlotId).DamageState != Offline
}

public sealed class IntentRepairEffect : IntentEffect {
    public string TargetSlotId { get; }      // enemy's own slot; non-nullable
    public int    Amount       { get; }
}

public sealed class IntentPlateEffect : IntentEffect {
    public int ArmorAmount { get; }
}

public sealed class IntentRepositionEffect : IntentEffect {
    public LanePosition Target { get; }      // non-nullable; enemy reposition is always directional
}

public sealed class IntentRamEffect : IntentEffect {
    public string TargetSlotId { get; }      // player structural slot; non-nullable
    public int    Damage       { get; }
    public int    SelfDamage   { get; }      // recoil to own structural slot
}

public sealed class IntentBuffEffect : IntentEffect {
    public int TauntStacks { get; }
}
```

- `IntentKind` enum **deleted**.
- `Requires?` / `Bonus?` survive as nullable optional-presence metadata (presence-check, not mode-switch — not bimodal per ADR-0011 reading).
- Today's archetype intents are all length-1 effect lists; future combo intents extend naturally.

### `Vehicle.cs` (final shape)

```csharp
public sealed class Vehicle : IVehicleMutator {
    public string        Name   { get; }
    public IFrameLayout  Layout { get; }       // non-nullable
    public LanePosition  Pos    { get; private set; }
    public IReadOnlyList<StatusBadge> Badges { get; }
    public int TauntStacks { get; private set; }
    public int OutgoingDamageBonus => TauntStacks * TauntDamagePerStack;
    public int NextAttackDamageReductionPercent { get; private set; }

    public Vehicle(string name, IFrameLayout layout) { /* only ctor */ }

    public SlotInstance GetSlotById(string slotId);
    public IReadOnlyList<SlotInstance> GetSlotsByKind(SlotKind kind);
    public IReadOnlyCollection<SlotInstance> SlotInstances { get; }
    public SlotInstance StructuralSlot { get; }      // NEW: layout guarantees exactly one
    public int StructuralHp { get; }                  // unchanged
    public int MaxArmor     { get; }                  // RENAMED from EffectiveMaxArmor (sums SlotKind.Armor MaxHp)
    public int CurrentArmor { get; }                  // RENAMED from EffectiveCurrentArmor (sums SlotKind.Armor Hp)
    public bool IsDead => StructuralHp <= 0;

    public void InstallPart(string slotId, int maxHp);
    public DamageResult ApplyDamage(string slotId, int amount, DamageSource source);
    public int  PlateArmor(int amount);
    public RepairResult RepairSlot(string slotId, int amount);
    public void SetPosition(LanePosition p);
    public void SetIncomingDamageReductionPercent(int percent);
    public void AddTaunt(int stacks);
    public void SetBadge(StatusBadge badge);
    public void ClearBadge(string id);
}
```

**Deleted from `Vehicle`:** `_slots`, `_legacyToSlotId`, `IsLayoutMode`, `GetSlot(LegacySlotKind)`, `GetSlotsOfType(SlotType)`, `GetSlotIdForLegacyKind`, `AllSlots`, legacy ctor, `MaxArmor` pool field (replaced by Armor-slot-sum getter of same name), `CurrentArmor` pool field (same), `EffectiveMaxArmor`/`EffectiveCurrentArmor` (folded into `MaxArmor`/`CurrentArmor` by rename), `ApplyDamage(LegacySlotKind, …)`, `InstallPart(LegacySlotKind, int, int)`, `RepairSlot(LegacySlotKind, int)`, `HasAnyOfflineNonFrameSlot`, `RecalculateMaxArmor`, `AddArmor` (unused after rename).

**`Slot.cs`** — file deleted. **`SlotType.cs`** — file deleted. **`LegacySlotKind.cs`** — file deleted.

---

## Execution stages

Each stage is an independently-reviewable commit on the slice branch. Tests
may go RED between stages; the requirement is GREEN at end of Stage L.
Production code never references LegacySlotKind from Stage E onward.

### Stage A — Card/Intent effect hierarchies land (additive)

- `Assets/Scripts/Combat/CardEffect.cs` (new): abstract base + 7 concrete effects.
- `Assets/Scripts/Combat/IntentEffect.cs` (new): abstract base + 6 concrete effects.
- New file `Assets/Scripts/Combat/BuffTag.cs` (replaces `BuffEffect` enum if it collides with the class name — rename TBD at code time).

**Gate:** compiles. No consumers yet.

### Stage B — `CardDefinition` + `EnemyIntent` rewritten as composition

- `Assets/Scripts/Combat/CardDefinition.cs`: rewrite as `Name + EnergyCost + Description + IReadOnlyList<CardEffect>`. Delete `Kind`, `Damage`, `ArmorGain`, `RepositionsPlayer`, `RepairAmount`, `RepositionTarget`, `RequiredWeapon`, `DrawCount`, `AppliedBuff` fields. Delete static factories (`Plate`, `Reposition`, `Handbrake`, `Overtake`, `Repair`, `Draw`, `Buff`).
- `Assets/Scripts/Combat/EnemyIntent.cs`: rewrite as `Effects + Description + Requires? + Bonus?`. Delete `Target`, `Damage`, `Kind`, `RepairAmount`, `PoweredBy`, `ArmorAmount`, `SelfDamage`, `TauntStacks` direct fields. Delete static factories.
- `Assets/Scripts/Combat/CardKind.cs` — DELETE.
- `Assets/Scripts/Combat/IntentKind.cs` — DELETE.

**Gate:** compile is RED — every consumer is broken. Move to Stage C immediately.

### Stage C — Producers rewritten

- `Assets/Scripts/Combat/StarterDecks.cs`: rewrite player starter deck using new `CardDefinition + Effects` shape. BulletBarrage = `new CardDefinition("BulletBarrage", energyCost, "...", new[]{ new WeaponAttackEffect("weapon_front", 12) })`. FlameBarrage = `"weapon_back"`. Plate, Handbrake, Overtake, FlameBarrier, etc. — each constructed with their effect list.
- `Assets/Scripts/Combat/Archetypes/DuneSkimmerArchetype.cs` (or equivalent): rewrite intent definitions using new `EnemyIntent + Effects` shape with slotId-bound `IntentAttackEffect`/`IntentRepairEffect`/etc.
- Same for `IronShepherdArchetype.cs`, `DredgeArchetype.cs`, and any other archetype intent table.
- `Assets/Scripts/Combat/Archetypes/ScriptedSequenceBrain.cs`, `AdaptiveSequenceBrain.cs`, `SelfRepairBrain.cs`: any intent construction call sites updated to new shape.

**One-shot SO migrator:** if `CardDefinitionSO` `.asset` files exist with serialized `LegacySlotKind RequiredWeapon` payload, run a one-shot editor script that translates and rewrites the assets. Delete migrator immediately after. (Stage C subtask, scoped at code time after grepping `.asset` files.)

**Gate:** producers compile. Consumers still RED.

### Stage D — Card/Intent consumers rewritten

- `Assets/Scripts/Combat/CombatLoop.cs` — `PlayCard` dispatches by iterating `card.Effects` and calling each effect's `Apply`. `EnemyTurn` reads `intent.Effects` and dispatches per type. `IsPlayable` delegates to `card.IsPlayable(this)`. All internal `LegacySlotKind` references removed.
- `Assets/Scripts/Combat/DamagePipeline.cs` — `Apply(Vehicle, LegacySlotKind, int, DamageSource)` overload DELETED. Only `Apply(Vehicle, string slotId, int, DamageSource)` survives.
- `Assets/Scripts/Combat/IntentPool.cs`: rewrite for new intent shape; weight selection unchanged but reads `intent.Effects`.
- `Assets/Scripts/Combat/WeightModifier.cs`: convert LegacySlotKind references to slotId or SlotKind as appropriate.
- `Assets/Scripts/Combat/CardPlayResult.cs`, `EnemyTurnResult.cs`: replace any `LegacySlotKind` field with `string slotId`.
- `Assets/Scripts/CombatView/CardWidget.cs`: `IsPlayable` calls `card.IsPlayable(loop)`; `BuildInfoText` walks `card.Effects` to render rule strings.
- `Assets/Scripts/CombatView/IntentWidget.cs`: walks `intent.Effects` for description / damage preview / slotId resolution.

**Gate:** compile GREEN for production code. Tests still RED (Stage J).

### Stage E — View layer slotId flip

- `Assets/Scripts/CombatView/VehiclePartHitZone.cs`: drop `[SerializeField] private LegacySlotKind _kind`. Add `[SerializeField] private string _slotId`. Update `Bind` signature, all callers.
- `Assets/Scripts/CombatView/VehicleVisual.cs`: delete 8 `[SerializeField] VehiclePartHitZone _hitZoneXxx` fields. Add lazy `Dictionary<string, List<VehiclePartHitZone>> _hitZonesBySlotId` built once via `GetComponentsInChildren<VehiclePartHitZone>` and grouped by `_slotId`. `GetHitZones(string slotId)` is the public read API. `GetHitZone(LegacySlotKind)` / `CollectHitZones(LegacySlotKind, …)` DELETED. SR fields (`_weaponSlot`, `_engineSlot`, etc.) survive — they are art anchors, not slot identity.
- `Assets/Scripts/CombatView/VehiclePartTint.cs`: iterate `target.SlotInstances`; for each slot look up SRs via a slotId→SR map derived from `VehicleVisual`. Drop all 7 hardcoded poll lines.
- `Assets/Scripts/CombatView/VehicleBarStack.cs`: delete `_subsystemPairs[]` SerializeField. Delete dual `_combatBarKinds`/`_combatBarSlotIds`/`_combatHitTargetKinds`/`_combatHitTargetSlotIds` lists — replace with single `_combatBarSlotIds` + `_combatHitTargetSlotIds`. `GetSlotAnchor(LegacySlotKind)` DELETED. Public read API is `GetSlotAnchorBySlotId(string)`. Internal iteration over `target.SlotInstances`.
- `Assets/Scripts/CombatView/MainBarWidget.cs`: read structural via `target.StructuralSlot.MaxHp`/`.Hp`. Read armor via `target.MaxArmor`/`target.CurrentArmor` (post-rename).
- `Assets/Scripts/CombatView/CombatHud.cs`: 4 `GetSlotAnchor(LegacySlotKind.Frame)` sites flip to `GetSlotAnchorBySlotId(target.StructuralSlot.SlotId)`. Drop `CombatHitTargetKinds` read; iterate `CombatHitTargetSlotIds` instead.
- `Assets/Scripts/CombatView/CombatController.cs`: ~14 LegacySlotKind sites flip to slotId.

**Gate:** view layer compiles. Production code has zero `LegacySlotKind` references.

### Stage F — Authoring rebuild

- `Assets/Editor/CombatPrefabAuthor.cs` (or equivalent): hit zone authoring now writes `_slotId` SerializeField per zone. Generation walks `SmallFrameLayout.Slots` / `HaulerFrameLayout.Slots` / `DredgeFrameLayout.Slots` / `TinyFrameLayout.Slots` to drive layout.
- ExecuteAlways asset guard (`!gameObject.scene.IsValid()` early-out) preserved per memory `feedback_executealways_asset_guard`.

**Gate:** authoring runs without errors; produces prefabs in slotId-keyed shape.

### Stage G — Prefab re-author (4 prefabs)

Per `feedback_pre_author_capture_protocol` and `feedback_reauthor_combat_after_vehicle`:

1. **Pre-author capture** — read prefab YAML for each of:
   - `PlayerVehicle.prefab`
   - `IronShepherd.prefab` (Vehicle_Shepherd)
   - `DuneSkimmer.prefab` (Vehicle_Skimmer)
   - `Dredge.prefab` (Vehicle_Dredge)
   Bake every designer override (transform, color, `m_IsActive`, child structure) into authoring source.
2. **Re-run authoring** for each vehicle.
3. **Re-author Combat prefab** after vehicle re-author (restores nested transform overrides).
4. **Eyes-on verification** per vehicle: Edit Mode + Play Mode render correct; hit zones map to correct slotIds; tints and bars drain on damage.

**Gate:** user-verified visual parity with pre-slice state.

### Stage H — Vehicle/Slot demolition

- `Assets/Scripts/Combat/Slot.cs` — FILE DELETED.
- `Assets/Scripts/Combat/Vehicle.cs` — simplify per end-state shape above:
  - Delete `_slots`, `_legacyToSlotId`, `IsLayoutMode`, `Layout` nullable→non-nullable.
  - Delete legacy ctor.
  - Delete `GetSlot(LegacySlotKind)`, `GetSlotsOfType(SlotType)`, `GetSlotIdForLegacyKind`, `AllSlots`, `ApplyDamage(LegacySlotKind, …)`, `InstallPart(LegacySlotKind, …)`, `RepairSlot(LegacySlotKind, …)`, `HasAnyOfflineNonFrameSlot`, `RecalculateMaxArmor`, `AddArmor`.
  - Delete `MaxArmor`/`CurrentArmor` pool fields.
  - Rename `EffectiveMaxArmor` → `MaxArmor`. Rename `EffectiveCurrentArmor` → `CurrentArmor`.
  - Add `StructuralSlot` accessor (singular — layout R_FL guarantees exactly one).
  - `IsDead` collapses to `StructuralHp <= 0`.
  - `PlateArmor` collapses to the layout-mode branch only.
  - `RepairSlot(string slotId, int amount)` becomes the only signature.

**Gate:** Vehicle/Slot compile; production code green; tests RED.

### Stage I — Enum + SlotDefinition cleanup

- `Assets/Scripts/Combat/LegacySlotKind.cs` — FILE DELETED (and `SlotKindExtensions` deleted along with it; `SlotKind.CategoryLabel` extension stays).
- `Assets/Scripts/Combat/SlotType.cs` — FILE DELETED. Any remaining caller uses `SlotKind` directly.
- `Assets/Scripts/Combat/SlotDefinition.cs`: delete `LegacyKindBridge` field, `HasLegacyKindBridge` getter, `LegacyKind` getter, and the corresponding ctor parameter.
- `Assets/Scripts/Combat/Archetypes/TinyFrameLayout.cs`, `SmallFrameLayout.cs`, `HaulerFrameLayout.cs`, `DredgeFrameLayout.cs`: remove all `legacyKindBridge:` named arguments.
- `Slot.ArmorContribution` already gone (lived on the deleted `Slot.cs`).

**Gate:** entire production codebase compiles with zero LegacySlotKind / SlotType / LegacyKindBridge references.

### Stage J — Test migration (27 files)

- Per the existing phase plan's count: 27 test files use legacy `new Vehicle(name)` ctor (44 sites). Rewrite each to `new Vehicle(name, layout)` with a layout chosen to make the test meaningful (most player-side tests use `SmallFrameLayout.Instance`; structural tests may use `TinyFrameLayout.Instance`).
- Audit `MaxArmor` assertions: under the rename, `MaxArmor` is the Armor-slot-sum (was the Vehicle pool). Tests that asserted on the legacy pool may need to install armor on `armor_0` instead.
- DELETE bridge-only tests:
  - `CombatLoopSlotIdSurfaceTests.cs:171` — `legacyEnemy` paths
  - `SmallFrameLayoutTests.cs:298` — `legacy` path
  - `DamagePipeline_R_ARM_Tests.cs:321` — `Legacy` path
  - Any other test exercising the `Slot._bridge` mode specifically.
- Card/intent tests rewritten for composition shape.

**Gate:** all tests GREEN.

### Stage K — Doc scrub

- `docs/architecture/adr-0010-slot-system-single-vocabulary.md`: final amendment locking the as-shipped design (effect composition for cards/intents; `StructuralSlot` accessor; Part SO ADR exception called out).
- `docs/architecture/adr-0009-*`: final amendment marking superseded by ADR-0010.
- Walk the 10 known bridge-defining files; scrub every "transitional" / "bridge" / "slice 2.7" / "ADR-0009 §Phase 3" comment.
- `production/session-state/active.md`: remove every "invalidated by bridge" warning.
- `production/adr-0010-phase-plan.md`: mark obsolete; this execution plan is the authoritative slice doc.

**Gate:** zero "bridge" / "transitional" / "slice 2.7" tokens grep clean across the modified file set.

### Stage L — CI grep gate + verification

- Add CI grep gate (e.g., `tools/ci/grep-gate.sh` or repo equivalent) with forbidden tokens:
  - `LegacySlotKind`
  - `LegacyKindBridge`
  - `IsLegacyMode`
  - `IsLayoutMode`
  - `_maxArmor`
  - `_currentArmor`
  - `EffectiveMaxArmor`
  - `EffectiveCurrentArmor`
  - `armorContribution` (case-insensitive — survives nowhere)
  - `SlotType` (deleted enum)
  Exception: `// ADR-0011 exception: blocked on Part SO ADR` comment lines may reference `VehicleDefinitionSO` legacy field names. The CI gate ignores `VehicleDefinitionSO.cs` for legacy-field-name tokens.
- Compile clean on Unity + headless test runner.
- All tests GREEN.
- Play test: 4 vehicles render correctly in Combat scene; full round trip (player turn → cards → intent → damage → death/victory) works for each archetype.

**Gate:** entire slice merges to `main` as one contiguous commit series.

---

## ADR-0011 exception register

Only one exception persists after this slice:

| File | Field(s) | Reason | Tracking |
|------|---------|--------|----------|
| `Assets/Scripts/CombatView/Data/VehicleDefinitionSO.cs` | `_machineGun`, `_flamethrower`, `_engine`, `_wheels`, `_frame` SerializeFields (legacy-named) | Slots derive Hp/ArmorContribution from installed parts, not from slot row. No PartDefinitionSO exists. | Memory `project_part_so_blocker`; user commitment to tackle Part SO ADR as next natural moment arises post-slice. Code-side TODO comment cites this. |

CI grep gate explicitly exempts this file for legacy SerializeField names. No other exceptions.

---

## Sign-off

When approved, this plan supersedes `production/adr-0010-phase-plan.md`.
Execution starts at Stage A on a feature branch. Each stage may be its own
commit on that branch. The branch merges to `main` only when Stage L verifies.
No intermediate bridge state ever lands on `main`.

**Pending user approval:** end-state shapes, scope, exception register.

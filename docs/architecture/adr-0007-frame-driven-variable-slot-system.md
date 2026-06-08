# ADR-0007: Frame-Driven Variable Slot System

## Status

**Superseded by ADR-0010** (2026-05-31) — the 4-slot visual model and
fixed-shape `SlotKind` vocabulary are retired in favor of variable-N
slotId-keyed slot lists. See `docs/architecture/adr-0010-slot-system-single-vocabulary.md`.

_Prior status: **Accepted (architecture surface only)** — V&P architecture-doc APPROVED 2026-05-19 (R4 revision pass; all 40 findings across R1–R4 closed). Full **Accepted across the W0 scope** additionally requires mechanics-doc (`design/gdd/vehicle-and-part-mechanics.md`) approval, since the W0 BLOCK-2 dominant-strategy fix only fully lands once `InstallCost` authors against `InstalledCount` per V&P architecture-doc §5.2._

_Prior Proposed history: Phase 1 structural amendment 2026-05-18 closed 13 of 21 V&P GDD R5 blockers; Decision 16 corrected the granted-card lifecycle spec error (slot Offline = soft disable). R6 Cluster B amendment 2026-05-19 fixed Key Interfaces compile/serialization bugs and added spec-layer recommended-item batch._

## Date

2026-05-18 (initial) · 2026-05-18 (R2 reconciliation) · 2026-05-18 (R5 Phase 1 amendment) · 2026-05-19 (R6 Cluster B amendment) · 2026-05-19 (status transition: Accepted — architecture surface only)

## Decision Makers

- User (creative/design lead) — locked four reconciliation decisions 2026-05-18 (see Revision History)
- technical-director (architectural authoring) — drafted 2026-05-18; reconciliation amendments 2026-05-18
- unity-specialist (engine-idiom review) — pending
- systems-designer (GDD revision lead) — pending consumer review

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-05-18 | technical-director | Initial draft (Proposed). |
| 2026-05-18 | user (locked) + technical-director (applied) | **Reconciliation amendments** — four contradictions with V&P GDD Review 2 closed: (1) Decision 6 overflow policy now AMPLIFIES overflow on the breaking hit (was single-hit/no-routing), aligning with GDD R_ARM.2 Example 2; (2) Decision 6 rounding changed `ceil` → `floor` for `redirected_amount`, aligning with GDD R_ARM.2 + F-VP2; (3) Decision 9 layout IDs rewritten without `_player`/`_enemy`/`_boss` suffixes (e.g. `small_frame_player` → `small_frame`), aligning with GDD R_FL.2 + tr-registry constants + locked design decision; (4) Decision 4 + Key Interfaces clarified — Vehicle POCO carries BOTH `IFrameLayout Layout` (runtime reference) and `string FrameLayoutId` (computed identifier delegating to `Layout.LayoutId`, used by saves and external tooling). |
| 2026-05-18 | technical-director (Phase 1 amendment per R5 handoff) | **R5 Phase 1 structural amendment** — addresses 13 of 21 V&P GDD Review 5 blockers within ADR alone per `production/r5-handoff-adr-0007-amendment-scope.md` (Phases 2–4 land in subsequent sessions). (1) Added **Decision 12** — repair path emits `OnSlotHpChanged` (closes R5 blocker #3). (2) Added **Decision 13** — V&P F-VP2 Canonical Event Order Table is single source of truth; CI lock-step gate spec'd (closes R5 blocker #4). (3) Added **Decision 14** — Armor INTACT branch emits Steps 3/4 on the armor slot (closes R5 blocker #1). (4) Added **Decision 15** — recursive `ApplyDamage` invocations each capture a fresh `wasCritical` snapshot; `OnCriticalStateChanged` is at-most-once per top-level entrypoint (closes R5 blocker #2). (5) Key Interfaces additions: `ActiveStatuses`, `GetStatModifier`, `OnPlatingChanged`, `OnStatusStackChanged` on `IVehicleView` (closes R5 blockers #5, #6, and the missing R9 properties); `ICardCatalog` engine-free dep declared (closes R5 blocker #7); `IGrantedCardData` explicit assembly-placement clause (closes R5 blocker #18 part 2). (6) `AnchorPoint` re-declared in Key Interfaces with `[System.Serializable]` annotation visible and Unity-6.3 fields-only-`[SerializeField]` note (closes R5 blockers #13, #14). Status remains **Proposed** — Acceptance is the gate after V&P GDD R6 returns APPROVED. |
| 2026-05-18 | user (locked) + technical-director (applied) | **Phase 1 extension — Decision 16 (Granted-Card Lifecycle)**. User caught a spec error in the just-landed Decision 11 invariant #5: slot Offline was specified as a hard purge of granted cards from the deck. Player-experience design owner clarified that part destruction must behave as a **soft disable** (cards stay in deck/hand/discard, become unplayable via a source-slot playability gate — identical pattern to the existing `EnemyAhead`/`EnemyBehind` positional gate). Hard removal is reserved for (a) scrap (V&P R6) and (b) external-source termination (Dredge Javelin chain cut). Changes: (1) rewrote Decision 11 invariant #5 to hard-removal-only semantics; (2) added new **Decision 16** spelling out soft-disable vs hard-removal, the source-slot playability gate composition with positional + energy gates, the `CardData.SourceSlotId` runtime-record amendment to ADR-0006, the `IVehicleMutator.HardRemoveCards(string?, IReadOnlyList<string>)` surface, two worked examples (Engine destroy-then-repair; Dredge Javelin tether-cut), and Phase 2 F-VP2 prune instructions; (3) updated `OnGrantedCardRemoved` event comment to nullable-`SourceSlotId` signature; (4) fixed Decision 14 + Decision 15 worked examples that referenced the deleted Offline-removal behavior; (5) added Decision 16 validation criteria. Phase 2 V&P GDD edits expand to include the F-VP2 prune + Hard Removal Pathway section + EC-VP44 rewrite, and Phase 2 user-input-needed list ("OnGrantedCardRemoved delivery scope" question) is now ANSWERED (hard removal sweeps deck + hand + discard atomically; nullable SourceSlotId for non-slot grants). |
| 2026-05-19 | user (approved) | **Status transition: Proposed → Accepted (architecture surface only).** V&P architecture-doc (`design/gdd/vehicle-and-part-architecture.md`) received APPROVED verdict on R4 revision pass (all 40 findings across R1–R4 closed). ADR-0007 architecture surface is now the stable contract for W2 implementation (`EnemyDefinitionSO` + `BrainRulesetSO`). Full Accepted across W0 scope pending mechanics-doc (`design/gdd/vehicle-and-part-mechanics.md`) approval. |
| 2026-05-19 | technical-director (R6 Cluster B amendment) | **R6 Cluster B amendment** — closes the 2 compile/serialization blockers + the spec-layer recommended-item batch surfaced by R6 adversarial review (verdict: MAJOR REVISION NEEDED). Changes: (1) `SlotDefinition` (Key Interfaces) converted from `{ get; }` property syntax to plain public fields — auto-property syntax generated a compiler-hidden backing field that silently failed Unity 6.3 fields-only `[SerializeField]` serialization (Engine Compatibility line 40 self-violation; unity-specialist BLOCK-1). (2) `AnchorPoint.IsFinite` and `AnchorPoint.IsInUnitRect` given expression-bodied bodies — previously declared `{ get; }` with no body, which does not compile in C# (unity-specialist BLOCK-2). (3) Renamed `CardPositionRequirement` → `PositionRequirement` to match card-system.md §1 canonical field name; declared enum placement as `WastelandRun.Vehicle` (engine-free) to prevent Vehicle ↔ Cards cycle (unity-specialist BOUND-3). (4) Formal `DamageContext` struct declared in Key Interfaces (previously referenced throughout Decision 11/15 but never declared); marked stack-allocated to close allocation-strategy gap (unity-specialist BOUND-3 / DET-3 / SER-1). (5) `ICardCatalog` impl placement explicit (`AddressablesCardCatalog` in `WastelandRun.Gameplay`; interface stays engine-free in `WastelandRun.Vehicle`) — closes BOUND-1. (6) `VehicleStatePreview` cross-referenced from V&P GDD §R12 with placement note (engine-free `WastelandRun.Vehicle`) — closes BOUND-2. (7) Architecture Diagram extended with `PartDefinitionSO.ArmorContribution` IL2CPP `link.xml` preservation note (SER-3). (8) `VehicleStateDTO.SchemaVersion` constant cross-referenced to Decision 10 line 435 authoritative declaration; AC-VP33b migration ambiguity (envelope-level vs slot-level) clarified in Save box (SER-1 / SER-2). Status remains **Proposed** pending V&P GDD R7 verdict (Clusters A/C/D still pending). |

## Summary

The 4-slot vehicle invariant locked by ADR-0001 (Weapon, Engine, Mobility, Frame, with vehicle-level Armor) is replaced with a **frame-driven variable slot system**. `FrameLayoutSO` becomes the layout authoring asset, declaring an ordered `List<SlotDefinition>` per frame. Each slot carries a designer-authored stable `SlotId` string, a `SlotKind` (`Weapon` | `Engine` | `Mobility` | `Hull` | `Armor`), a `Position` (`Any` | `Front` | `Back`), an `IsStructural` flag, and HUD anchor. The former `SlotType.Frame` is renamed to `SlotKind.Hull` to free `Frame` for layout terminology. Vehicle death is generalized to `StructuralHp == 0` (sum over structural slots) instead of a single Frame slot. A new `SlotKind.Armor` is promoted from "Layer 2 vehicle stat" to a first-class destroyable slot that absorbs at 1x then exposes its `RedirectsTo` slot for `ExposureMultiplier`-scaled damage. Weapon-card position requirements (`EnemyAhead` / `EnemyBehind`, renamed for clarity) are derived from the part's `MountDirection`, which is gated against the slot's `Position` at install time. Enemy/AI implicit targeting defaults to `WeakestInstance` with per-intent overrides. Player frames (Small/Medium/Heavy) and enemy frames (Tiny/Hauler/Dredge) are locked in this ADR. `SlotStateDTO` ticks from `SchemaVersion = 1` to `2`; V1 saves migrate by synthesizing SlotIds from kind names and assigning a default `LayoutId` from the V1 Chassis field.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.13f1) |
| **Domain** | Core / Scripting (POCO model + ScriptableObject authoring) |
| **Knowledge Risk** | LOW — `ScriptableObject` with `[SerializeField]` arrays + `OnValidate` are stable surface across Unity 6.x. No URP, Render Graph, Input System, Box2D v3, or UI Toolkit surfaces are touched. |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/breaking-changes.md`, `.claude/docs/technical-preferences.md`, ADR-0001, ADR-0002, ADR-0003, ADR-0004, ADR-0005, ADR-0006, `design/gdd/vehicle-and-part-system.md` (R1–R11), `design/gdd/biome-1-enemy-roster.md` |
| **Post-Cutoff APIs Used** | None new. Reuses Addressables loader pattern (per ADR-0001/0005) for `FrameLayoutSO` catalog if needed; otherwise frames are direct references on `ChassisDefinitionSO`/enemy roster entries. |
| **Unity 6.3 Breaking-Change Flags** | `[SerializeField]` is fields-only in 6.3 — all `SlotDefinition` and `FrameLayoutSO` serialized state must be fields (not properties); auto-properties exposed externally must use `[field: SerializeField]`. No other 6.3 breaking change applies. |
| **Verification Required** | (1) `FrameLayoutSO.OnValidate` rejects (a) duplicate SlotIds, (b) zero structural slots, (c) `Armor` slot whose `RedirectsTo` references a non-existent SlotId or a non-structural slot, (d) weapon slot with `Position` that contradicts a hard-locked starter part's `MountDirection`. (2) EditMode round-trip test: V1 `SlotStateDTO` save → load under V2 → re-save → re-load reproduces identical runtime state. (3) Standalone IL2CPP smoke test loads one `FrameLayoutSO`, constructs `Vehicle`, applies damage to every kind including `Armor` exposure branch. |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Supersedes** | **ADR-0001 (Visual Vehicle Part System)** — the 4-slot invariant, the vehicle-level Armor stat model, the `SlotType` enum's `Frame` member name, and the `IVehicleView.MaxArmor/CurrentArmor` surface are all replaced. The visual-layer decisions of ADR-0001 (URP Sprite Lit Shader Graph, MaterialPropertyBlock dirty-flag pattern, Addressables groups, point-filter sprite budget) **remain in force**; this ADR only changes the data contract those visuals consume. |
| **Depends On** | ADR-0002 (POCO state model — Vehicle stays plain C# in `WastelandRun.Vehicle` with `noEngineReferences: true`); ADR-0003 (deterministic RNG — enemy `RandomInstance` targeting uses caller-owned `System.Random` from `RunSeed ^ stepIndex`); ADR-0004 (distributed schema registry — `SlotStateDTO` ticks `SchemaVersion` and reuses temp-then-rename, per-category recovery, mastery-blocking exhaustion); ADR-0005 (assembly split, `IPartData`/`IPartCatalog` patterns, IL2CPP `link.xml` discipline — extended to add `FrameLayoutSO` to the preserved namespace) |
| **Amends** | **ADR-0006 (Card System)** — `PositionRequirement` enum values `RequiresAhead`/`RequiresBehind` are renamed to `EnemyAhead`/`EnemyBehind` (semantics unchanged: target is ahead/behind the firing vehicle). The amendment is a pure rename — `BonusIfAhead`/`BonusIfBehind`/`None` are unchanged. Card SO assets that referenced the old values must be migrated by a one-shot editor script (see Migration Plan step 7). |
| **Enables** | W2 enemy roster implementation (Tiny, Hauler, Dredge frames consumed by `biome-1-enemy-roster.md`); revised V&P GDD (R1–R11 require this ADR before they can be edited away from the 4-slot invariant); future post-EA position-locked weapons; future post-EA Armor-bearing player frames |
| **Blocks** | V&P GDD revision (cannot start until this ADR's data contract is locked); biome-1 enemy roster final revision (Dredge boss bespoke layout is only definable on this contract); HUD vehicle silhouette refactor (needs `SlotInstance` enumeration + `HudAnchor` field); save migration story (SlotStateDTO V1→V2 fixture + test) |
| **Ordering Note** | Must be Accepted before any code change to `WastelandRun.Vehicle` removes the 4-slot `Dictionary<SlotType, SlotState>` (ADR-0005 §Vehicle POCO), before any enemy roster authoring depends on the Dredge layout, and before any card SO is touched by the `EnemyAhead`/`EnemyBehind` rename script. |

## Context

### Problem Statement

ADR-0001 locked a **4 visible slots × 1 vehicle-level Armor stat** model based on the EA art budget and the Pillar 1 emotional-anchor goal. The V&P GDD (R1–R5) and ADR-0005 then hardened this into:

- `enum SlotType { Weapon, Engine, Mobility, Frame }` — fixed enumeration, indexed lookup `Dictionary<SlotType, SlotState>`.
- Vehicle-level `MaxArmor = Σ part.ArmorContribution` — Armor is a derived chassis stat, not a slot.
- Death condition: `Slots[Frame].DamageState == Offline` — Frame is the singular death-critical slot.

Three pressures from later design work have invalidated this invariant:

1. **Boss design** — the Dredge boss needs (a) more than one of each non-structural kind to make limb-stripping legible (3 Weapons, 2 Engines), (b) bespoke armor plating per body section, and (c) two distinct mount positions (front-mounted minigun/javelin/ram vs. back-mounted spiked flail). The fixed `Dictionary<SlotType, SlotState>` cannot represent two Engines on one vehicle, and Armor-as-stat cannot represent destroyable per-section plates.
2. **Weapon directionality** — the Combat Chase Rail (per `combat_scene_layout` memory) has player and enemy facing right, with left=back/right=front and a position swap animation. The card system already has `PositionRequirement.RequiresAhead/RequiresBehind` per ADR-0006, but these are authored per-card on an ad-hoc basis. To make weapon mount position a *gameplay surface* (you can't fire a back-mounted flail at a front target), the position must live on the slot the part is installed into, and weapon cards must inherit their requirement from the mount.
3. **Frame variety as the chassis progression knob** — chassis identity (Pillar 2) currently lives in starter parts, starter deck, and chassis-level art. Promoting *layout* to a first-class chassis differentiator gives Small (5 slots), Medium (5 slots, balanced), Heavy (7 slots, war machine) a structural identity beyond stats. `Dictionary<SlotType, SlotState>` cannot express "this chassis has 3 weapon slots, that one has 1."

ADR-0001 explicitly noted (§Consequences/Positive, line 197) that the IVehicleView boundary makes the rendering decision reversible. The data contract, however, was not designed for reversibility — it embedded the 4-slot assumption into every enum, every `Dictionary<SlotType, _>` lookup, every save DTO, every card position field, and every status target. This ADR pays the cost of generalization once, before EA ship locks the save schema and the content authoring volume makes the migration cost dominant.

### Current State

- `WastelandRun.Vehicle.asmdef` (ADR-0005) exists with `noEngineReferences: true`. `SlotType` enum has 4 values (Weapon, Engine, Mobility, Frame). `Vehicle.Slots` is `IReadOnlyDictionary<SlotType, SlotState>`. `MaxArmor` and `CurrentArmor` are vehicle-level ints. Death is `IsDead = Slots[Frame].DamageState == Offline`.
- `WastelandRun.Cards.asmdef` (ADR-0006) declares `PositionRequirement { None, RequiresAhead, RequiresBehind, BonusIfAhead, BonusIfBehind }`. Card SOs use these values directly. ~10 starter cards × 2 chassis are authored against this enum.
- `WastelandRun.Save.Dtos` (ADR-0004) has `SlotStateDTO` at `SchemaVersion = 1` keyed by `string SlotType` (e.g., `"Weapon"`).
- No `FrameLayoutSO` exists. No `SlotInstance` runtime type exists. No `MountDirection` field on `PartDefinitionSO`. No enemy roster code consumes the V&P contract yet — `biome-1-enemy-roster.md` is drafted but no SOs are authored.
- Combat tests (~136 EditMode) assume the 4-slot `Dictionary` shape and `Slots[Frame]` death check.
- `tr-registry.yaml` has ~20 active `TR-vehicle-NNN` requirements bound to the 4-slot contract.

### Constraints

- **Backward compatibility (saves)**: Any V1 save written by a player who plays an EA-pre-ADR-0007 build must load successfully under the V2 schema. Per ADR-0004 R5/EC4 the EA-mode policy treats lower `SchemaVersion` as incompatible unless the migration runtime lands; this ADR therefore bundles a one-shot V1→V2 read-side migration as part of the V2 implementation (no general migration runtime is added — see Migration Plan step 6).
- **Engine-free Vehicle assembly** (ADR-0005): `FrameLayoutSO` must live in `WastelandRun.Gameplay`, NOT in `WastelandRun.Vehicle`. The runtime `Vehicle` consumes only an `IFrameLayout` interface (engine-free); `FrameLayoutSO.ToRuntime()` projects to `IFrameLayout` at chassis instantiation (same ADR-0005 pattern as `PartDefinitionSO → IPartData`).
- **Determinism** (ADR-0003): Enemy AI `WeakestInstance` and `RandomInstance` targeting must be deterministic — ties broken by lexicographic SlotId (stable, designer-authored); RNG-based picks consume a caller-owned `System.Random` from `RunSeed ^ stepIndex`. No `UnityEngine.Random`, no `DateTime.Now`.
- **Tech-prefs forbidden patterns**: no UnityEvent, no MonoBehaviour-owned combat state, no hardcoded gameplay values (frame layouts ARE the data, but `Position`/`MountDirection`/`ExposureMultiplier` enums and floats must be authored in SO assets — no magic numbers in `Vehicle.cs`).
- **Performance budget** (60fps / 16.6ms): Hot-path slot lookup must not regress vs. the `Dictionary<SlotType, SlotState>` baseline. Variable slot count means `GetSlotsByKind` is `O(N)` over the slot list (N ≤ 10 in the worst case — Dredge), acceptable vs. a `Dictionary` lookup's `O(1)` because N is bounded and call sites are turn-scoped, not per-frame.
- **GDD invariants preserved**: `DamageState { Empty, Functional, Degraded, Offline }` (ADR-0005), `DegradedThreshold = 50% MaxHp` (V&P R3), `MaxPlating ∈ [0,3]` per slot (V&P R4), DOT bypasses Armor (V&P F-VP2) — all retained.

### Requirements

- A chassis can declare any ordered list of slots, with any mix of kinds, where the layout is the single source of truth.
- A slot identity is stable across the run (and across save roundtrip) via a designer-authored string SlotId — saves remain human-readable.
- A weapon's mount position is a hard install gate (Front weapon cannot install in Back slot) AND the source of the card's position requirement (no separate authoring step).
- A vehicle dies when its structural pool is exhausted, which generalizes single-Frame death to multi-Hull or zero-Hull-with-other-structural designs.
- Armor is a destroyable slot that absorbs at 1x and then **exposes** the underlying slot for `ExposureMultiplier`-scaled damage — directly modeling Mad Max-style armor plates being knocked off.
- Enemy/AI implicit-target defaults must be deterministic, designer-overridable per intent, and stable under save/replay.
- V1 saves load. The migration is one-shot at load time, not a general framework.

## Decision

### Decision 1: `SlotKind` Replaces `SlotType`; `Frame` Renamed to `Hull`

```csharp
namespace WastelandRun.Vehicle
{
    public enum SlotKind { Weapon, Engine, Mobility, Hull, Armor }   // was SlotType { Weapon, Engine, Mobility, Frame }
}
```

`SlotKind` is the gameplay category. `Hull` replaces `Frame` to free the word "Frame" for layout authoring (`FrameLayoutSO`). The two terms are now disjoint: a *frame* is a layout asset that declares zero or more *hull* slots (typically one for player frames, one for enemies, but the contract permits any non-zero structural pool).

Rename is mechanical across `WastelandRun.Vehicle`, `WastelandRun.Cards` (effect records referencing `SlotType`), `WastelandRun.Combat` (resolution branches), and `WastelandRun.Gameplay` (PartDefinitionSO.SlotType field, OnValidate). Old `SlotType` enum is deleted; no alias is retained (clean break — there are no shipped saves yet).

### Decision 2: `FrameLayoutSO` Is the Single Source of Truth for Slot Topology

```csharp
// SlotDefinition + SlotPosition + AnchorPoint live in the engine-free assembly
// (WastelandRun.Vehicle, noEngineReferences:true per ADR-0005). FrameLayoutSO
// is the engine-bearing authoring SO that holds them in a serialized array.

namespace WastelandRun.Vehicle
{
    public enum SlotPosition { Any, Front, Back }

    // Engine-free 2D anchor in normalized chassis-local UV space.
    // Replaces UnityEngine.Vector2 inside SlotDefinition so that the
    // engine-free Vehicle assembly compiles under noEngineReferences:true
    // (ADR-0005). FrameLayoutSO authoring is unaffected — the struct is
    // [System.Serializable] and serializes identically in Unity's inspector.
    [System.Serializable]
    public struct AnchorPoint
    {
        public float X;
        public float Y;
        public AnchorPoint(float x, float y) { X = x; Y = y; }
        public bool IsFinite => !float.IsNaN(X) && !float.IsNaN(Y)
                              && !float.IsInfinity(X) && !float.IsInfinity(Y);
        public bool IsInUnitRect => IsFinite && X >= 0f && X <= 1f && Y >= 0f && Y <= 1f;
    }

    [System.Serializable]
    public struct SlotDefinition
    {
        [SerializeField] private string         slotId;          // designer-authored, e.g. "weapon_back", "engine_0"
        [SerializeField] private SlotKind       kind;
        [SerializeField] private SlotPosition   position;        // Any | Front | Back
        [SerializeField] private bool           isStructural;    // default true for Hull, false otherwise (enforced by OnValidate suggestion, not lock)
        [SerializeField] private AnchorPoint    hudAnchor;       // chassis-local normalized [0,1]^2 anchor for HUD overlay; (0.5, 0.5) = center
        [SerializeField] private int            maxHpOverride;   // Armor-required override; -1 sentinel = "use chassis default" for non-Armor slots

        // Armor-only fields (ignored when kind != Armor; OnValidate warns if non-default on non-Armor)
        [SerializeField] private float          exposureMultiplier;   // e.g. 3.0
        [SerializeField] private string         redirectsTo;          // SlotId of the underlying structural slot

        public string         SlotId             => slotId;
        public SlotKind       Kind               => kind;
        public SlotPosition   Position           => position;
        public bool           IsStructural       => isStructural;
        public AnchorPoint    HudAnchor          => hudAnchor;
        public int            MaxHpOverride      => maxHpOverride;
        public float          ExposureMultiplier => exposureMultiplier;
        public string         RedirectsTo        => redirectsTo;
    }
}

namespace WastelandRun.Gameplay.Vehicle
{
    [CreateAssetMenu(menuName = "Wasteland/Vehicle/FrameLayout")]
    public sealed class FrameLayoutSO : ScriptableObject, IFrameLayout
    {
        [SerializeField] private string layoutId;             // e.g. "small_frame"
        [SerializeField] private string displayName;
        [SerializeField] private bool   isPlayerUnlockable;   // true for Small/Medium/Heavy; false for enemy frames
        [SerializeField] private SlotDefinition[] slots;       // designer-ordered; SlotDefinition is engine-free

        public string LayoutId => layoutId;
        public bool   IsPlayerUnlockable => isPlayerUnlockable;
        public IReadOnlyList<SlotDefinition> Slots => slots;

        private void OnValidate()
        {
            // Hard import errors (rejected by R_FL.1 — see V&P GDD R_FL.1 for the canonical rule list):
            // 1. SlotIds non-empty and unique within layout.
            // 2. At least one slot with IsStructural == true (otherwise vehicle is born dead).
            // 3. RedirectsToSlotId on Armor slots:
            //    a. Must reference a SlotId that exists in this layout.
            //    b. Target SlotId != self (no self-redirect).
            //    c. Target Kind != Armor (no Armor → Armor chains — see Decision 6 atomicity invariants).
            //    d. Target IsStructural == true.
            //    e. Armor slot itself must declare a non-empty RedirectsToSlotId.
            // 4. ExposureMultiplier > 0 AND IsFinite (NaN/Inf rejected; > 5.0 warns, does not error).
            // 5. HudAnchor.IsFinite AND HudAnchor.IsInUnitRect (NaN/Inf/out-of-range rejected at import).
            // 6. DegradedThresholdPct ∈ [1, 99] AND MaxHpOverride ≥ 1 on Armor slots
            //    (rejects the zero-width Functional band — see V&P F-VP1 + AC-VP48e/f).
        }
    }
}
```

`SlotDefinition` is a `struct` because it is value-type immutable layout data; instances live in the SO's `[SerializeField]` array.

**Designer default for `IsStructural`**: `true` if `Kind == Hull`, `false` otherwise. Implemented as an `OnValidate` auto-fix on first import (sets `isStructural = (kind == SlotKind.Hull)` if the slot is freshly created with default false-on-non-Hull and true-on-Hull). Designer can override per-slot.

`SlotId` is the **single stable handle** across all systems. Saves persist SlotIds. AI intents reference SlotIds for `SpecificSlotId` targeting. HUD elements bind to SlotIds.

### Decision 3: `IFrameLayout` Is the Engine-Free Runtime Projection

```csharp
namespace WastelandRun.Vehicle
{
    // Engine-free interface every layout asset implements.
    // Vehicle / SlotInstance / IPartCatalog consumers reach layout data
    // exclusively through this interface — they never reference FrameLayoutSO.
    // This is what makes WastelandRun.Vehicle compile under noEngineReferences:true.
    public interface IFrameLayout
    {
        string LayoutId         { get; }
        bool   IsPlayerUnlockable { get; }
        IReadOnlyList<SlotDefinition> Slots { get; }   // SlotDefinition struct lives in WastelandRun.Vehicle
    }
}
```

`SlotDefinition`, `SlotPosition`, and `AnchorPoint` live in `WastelandRun.Vehicle` (engine-free) so the runtime can read them without touching `UnityEngine.*`. `FrameLayoutSO` in `WastelandRun.Gameplay` adds `[SerializeField]` + `OnValidate` and implements `IFrameLayout` — same authoring/runtime split as `PartDefinitionSO → IPartData` (ADR-0005). The V&P GDD R_FL declares `IFrameLayout` as the engine-free consumer contract; `FrameLayoutSO` is the single authoring implementation.

### Decision 4: `SlotInstance` Replaces `SlotState`; `Vehicle.Slots` Is a Variable-Length Indexed List

```csharp
namespace WastelandRun.Vehicle
{
    public sealed class SlotInstance
    {
        public string         SlotId       { get; }   // mirrors SlotDefinition.SlotId
        public SlotKind       Kind         { get; }
        public SlotPosition   Position     { get; }
        public bool           IsStructural { get; }
        public IPartData      InstalledPart { get; internal set; }    // null = Empty
        public int            Hp           { get; internal set; }
        public int            MaxHp        { get; internal set; }     // derived from InstalledPart at install time
        public int            PlatingStacks{ get; internal set; }     // unchanged from ADR-0005 (non-Armor slots only)
        public DamageState    DamageState { get { /* derived from Hp + InstalledPart per ADR-0005 R3 */ } }

        // Armor-only mirror of SlotDefinition for fast access (avoid layout-lookup on damage hot path)
        public float          ExposureMultiplier { get; }
        public string         RedirectsTo        { get; }

        internal SlotInstance(SlotDefinition def) { /* copies definition into runtime state */ }
    }

    public sealed class Vehicle : IVehicleView, IVehicleMutator
    {
        public ChassisType  Chassis        { get; }
        public IFrameLayout Layout         { get; }                    // runtime layout reference (engine-free)
        public string       FrameLayoutId  => Layout.LayoutId;         // computed view; persisted by VehicleStateDTO
        public IReadOnlyList<SlotInstance> Slots { get; }              // ordered by FrameLayoutSO authoring order

        private readonly Dictionary<string, SlotInstance> _bySlotId;   // O(1) GetSlot
        private readonly Dictionary<SlotKind, List<SlotInstance>> _byKind;  // O(1) GetSlotsByKind

        public SlotInstance GetSlot(string slotId);                    // throws SlotNotFoundException if missing
        public IReadOnlyList<SlotInstance> GetSlotsByKind(SlotKind kind);

        public int StructuralHp => /* Σ slot.Hp where slot.IsStructural */;

        // IsDead is a BACKING FIELD with a private setter — NOT a derived getter.
        // It is written exactly once per combat inside ApplyDamage step (f) per V&P F-VP2,
        // strictly after OnSlotHpChanged/OnSlotDamageStateChanged/OnArmorExposed have fired
        // for the triggering hit and strictly before OnVehicleDied fires (step g).
        // (OnGrantedCardRemoved is NOT part of the damage-step event sequence per Decision 16
        // — slot Offline no longer mutates the deck; OnGrantedCardRemoved is hard-removal-only
        // and is invoked from out-of-band callers: scrap and external-source termination.)
        // Subscribers reading IsDead from inside any pre-(f) event handler are guaranteed to
        // observe the pre-write value (false). This sequencing resolves Review 2 blocker #4
        // (IsDead getter-vs-event race).
        public bool IsDead { get; private set; }                       // generalized death condition
    }
}
```

**FrameLayoutId dual-view contract** (revised 2026-05-18): Vehicle exposes BOTH `IFrameLayout Layout` (the runtime reference used for slot enumeration, validation, and hot-path access — engine-free, follows the ADR-0005 `PartDefinitionSO → IPartData` projection pattern) AND `string FrameLayoutId` (a computed view that delegates to `Layout.LayoutId`, used by save serialization, external tooling, AI debugging, and any code that should not depend on the runtime layout reference). Authoring assets (`ChassisDefinitionSO`, enemy roster SOs) hold direct SO references per Decision 9. Saves persist the string per Decision 10. The two views are not in tension — they are different consumers of the same identity at different layers. GDD R1.1 + R9 reference `FrameLayoutId: string`; this ADR's runtime interface adds the `IFrameLayout Layout` reference; both are correct and both are present on the POCO.

**`Vehicle.MaxArmor` and `Vehicle.CurrentArmor` are removed.** Armor is now per-slot HP. UI surfaces that want a "total armor" indicator iterate `Slots.Where(s => s.Kind == Armor).Sum(s => s.Hp)` — a UI projection, not a Vehicle invariant.

**The lookup dictionaries are built once at `Vehicle` construction** from the `IFrameLayout.Slots` list. `GetSlotsByKind` returns a cached read-only list, not a fresh allocation per call.

### Decision 5: `MountDirection` Gates Install + Derives Card `PositionRequirement`

```csharp
// WastelandRun.Vehicle (engine-free) — extended PartDefinition contract
public interface IPartData
{
    string        PartId            { get; }
    SlotKind      SlotKind          { get; }            // was SlotType
    SlotPosition  MountDirection    { get; }            // NEW — was implicitly "Any"
    // ... existing fields unchanged (CompatibleChassis, Rarity, GrantedCards, StatModifiers, SpriteKey, MaxPlating, ArmorContribution) ...
}

// Install gate (added to IVehicleMutator.InstallPart implementation in ADR-0005)
private static bool MountCompatible(SlotPosition slotPos, SlotPosition partMount)
{
    if (partMount == SlotPosition.Any) return true;                    // flexible part fits anywhere
    if (slotPos   == SlotPosition.Any) return true;                    // flexible slot accepts anything
    return slotPos == partMount;                                       // both directional — must match
}
```

`InstallPart` throws a new `MountIncompatibleException` (sibling to `PartIncompatibleException` from ADR-0005, exception-based validation per ADR-0002 pattern) when `MountCompatible` returns false.

**Card position requirement inheritance** lives at **card-grant time, not at card-resolution time**. When a part is installed and its `GrantedCards` are added to the deck (V&P R5 step 6), each granted card's `PositionRequirement` is overlaid with the part's `MountDirection`:

```csharp
PositionRequirement DerivePositionRequirement(IPartData part, ICardData cardTemplate)
{
    return part.MountDirection switch
    {
        SlotPosition.Front => PositionRequirement.EnemyAhead,
        SlotPosition.Back  => PositionRequirement.EnemyBehind,
        SlotPosition.Any   => cardTemplate.PositionRequirement   // fall through to card's authored requirement
    };
}
```

The derived `PositionRequirement` is stamped onto the runtime `ICardData` instance held in the deck (the SO asset is never mutated). This means **the position requirement is a runtime property of the card-in-deck, not a property of the card SO** — see Decision 7 for the consequence on Combat resolution and on save serialization.

For EA, every starter weapon SO has `MountDirection = Any` so positional gameplay stays optional. Position-locked weapons enter the arsenal post-EA.

### Decision 6: `SlotKind.Armor` Is a First-Class Destroyable Slot With Exposure Routing

When `IVehicleMutator.ApplyDamage(slotId, amount, source)` resolves against a slot whose `Kind == Armor`:

```
1. If slot.PlatingStacks > 0:
     consume plating (unchanged from ADR-0005 — Armor slots may carry plating from card effects)
2. If slot.Hp > 0 (Armor intact):
     armorConsumed = min(amount, slot.Hp)
     slot.Hp       = slot.Hp - armorConsumed                          // 1x absorption — never amplified
     overflow      = amount - armorConsumed
     if slot.Hp == 0 → fire OnSlotDamageStateChanged(slotId, Functional|Degraded, Offline)
                       fire OnArmorExposed(slotId, slot.RedirectsTo)  // see V&P R_ARM events
     if overflow > 0:                                                  // breakthrough on this hit
         redirectedAmount = floor(overflow * slot.ExposureMultiplier)
         redirectedSlot   = vehicle.GetSlot(slot.RedirectsTo)
         ApplyDamage(redirectedSlot.SlotId, redirectedAmount, source)  // recursive; the rest of this swing punches through
3. Else (slot.Hp == 0 — already exposed; InstalledPart != null):
     redirectedAmount = floor(amount * slot.ExposureMultiplier)
     redirectedSlot   = vehicle.GetSlot(slot.RedirectsTo)
     ApplyDamage(redirectedSlot.SlotId, redirectedAmount, source)      // recursive; Armor → Hull is the typical chain
```

**Breakthrough policy** (revised 2026-05-18 — was "Single-hit policy"): a hit that breaks the Armor plate AND has remaining overflow routes the overflow through the `ExposureMultiplier` to the redirect target on the SAME hit. Example: a 10-damage hit against a 4-HP Armor plate with `ExposureMultiplier = 3.0` and `RedirectsTo = hull_0` absorbs 4 on the plate (Armor → Offline), routes the 6 overflow through ×3.0 = `floor(18)` = 18 damage to `hull_0`, all from one card play. This is the "glass shield breakthrough" feel — investing into armor is real protection while it stands, but overkilling a low-HP plate is rewarded by punching through. A hit that *exactly* depletes Armor HP (e.g., 4 damage to a 4-HP plate) breaks the plate with zero overflow and routes no damage — the breakthrough only fires on overkill. The *next* hit after a breaking-zero-overflow hit is the first amplified-redirect hit (step 3 branch). This is GDD R_ARM.2 / F-VP2 normative; the ADR mirrors it.

**Rounding policy**: `redirected_amount = floor(... × ExposureMultiplier)`. Floor (not ceil, not banker's). Designer-favorable to the player; consistent across both the breakthrough branch (step 2 overflow) and the already-exposed branch (step 3). Divergence from `ceil` is only observable when `ExposureMultiplier` is non-integer (current default 3.0 produces identical results either way; tuning band per V&P G is 1.5–5.0). Integer-overflow clamp `Math.Min(..., int.MaxValue)` applies before the cast per V&P Review 2 blocker #6.

**Player UX**: card targeting drag-drop continues to address the visible slot (e.g., `armor_chest`). The system resolves the actual damage path internally. Players never select `hull_0` directly when an Armor slot covers it — the Armor must be downed first.

**HUD**: the Armor sprite renders prominently above the structural slot's silhouette. Visual transitions at `Hp == MaxHp` (intact), `0 < Hp < MaxHp` (cracking — driven by ADR-0001's MPB damage overlay system), `Hp == 0` (exposed — sprite swap to wound art, possibly with periodic VFX pulse to signal "hit here for bonus damage"). Visual specifics are out of scope for this ADR — they belong to the HUD spec.

**Armor slots also have `Position`** (Front/Back/Any) like other slots. This enables boss designs where the front armor plate gates front-mounted weapon damage and the back armor plate gates back-mounted weapon damage (cf. Dredge: `armor_chest` at Front, `armor_back` at Back).

**Armor is structural-by-default = false**. An Armor slot at 0 HP is "exposed", not "dead-vehicle-contribution". Vehicle death is governed by the structural pool (Hull slots typically), not by armor depletion.

### Decision 7: Card `PositionRequirement` Becomes Runtime State; ADR-0006 Amendment

`ICardData` from ADR-0006 declares `PositionRequirement PositionRequirement { get; }` and the project's runtime `CardData` record satisfies it. To support per-instance mount-derived requirements (Decision 5), the in-deck runtime card representation MUST carry its derived `PositionRequirement` separately from the SO template.

**Amendment to ADR-0006**:
- The `CardData` runtime record (the POCO held in `Deck`/`Hand`/`Discard`/`Exhausted` lists) gains `PositionRequirement OverridePositionRequirement` as an optional override. Combat resolution reads `card.OverridePositionRequirement ?? card.Template.PositionRequirement`.
- `PositionRequirement` enum values `RequiresAhead` and `RequiresBehind` are renamed to `EnemyAhead` and `EnemyBehind`. `None`, `BonusIfAhead`, `BonusIfBehind` are unchanged. A one-shot editor script (Migration step 7) rewrites all card SO `PositionRequirement` fields.
- `CardSystemDTO` (ADR-0006/0004) at `SchemaVersion = 1` does NOT need to bump — `Deck`/`Discard`/`Exhausted` persist CardIds only; the per-instance override is **re-derived on load** by re-running `DerivePositionRequirement` against the currently installed parts in the loaded `Vehicle`. This is correct because (a) cards are granted *by* installed parts, so as long as the part is still installed the derivation reproduces the same requirement, and (b) cards from a part that is no longer installed are removed from all zones at scrap time (V&P R6) so they cannot exist in a loaded deck without their granting part.

This avoids ticking the CardSystem schema and keeps the SO assets as the single source of truth for the *card template*; the *card instance* is a runtime projection.

### Decision 8: Enemy / AI Implicit Targeting

When an enemy intent or implicit-target card effect must pick among multiple slots of the same kind on the target vehicle, the **default rule is `WeakestInstance`**:

```
WeakestInstance(targetVehicle, kind):
    candidates = targetVehicle.GetSlotsByKind(kind).Where(s => s.DamageState != Empty)
    if candidates.IsEmpty: return null  (intent handler decides fallback — usually skip or retarget to Hull)
    return candidates.OrderBy(s => s.Hp).ThenBy(s => s.SlotId, StringComparer.Ordinal).First()
```

Ties broken by lexicographic SlotId (designer-authored, stable, deterministic — no RNG needed for the tie-break, satisfying ADR-0003 without consuming an RNG draw).

**`BrainRulesetSO` per-intent override** (enemy AI authoring asset, owned by the future Enemy AI ADR — surfaced here as a contract requirement):

```csharp
public enum ImplicitTargetMode { WeakestInstance, RandomInstance, SpecificSlotId }

[System.Serializable]
public struct ImplicitTargetSpec
{
    public ImplicitTargetMode Mode;
    public string             SpecificSlotId;   // ignored unless Mode == SpecificSlotId
    // RandomInstance: caller-owned System.Random per ADR-0003; brain receives it from CombatLoop
}
```

`RandomInstance` is reserved for chaotic boss behaviors and consumes the existing combat-scoped `System.Random` derived from `RunSeed ^ turnIndex` (the same RNG used by other combat-side stochastic effects per ADR-0003).

`SpecificSlotId` is bespoke targeting for boss intents (e.g., Dredge's Heavy Ram always targets the player's `hull_0`).

**Player-controlled card targeting is unchanged** — the existing drag-to-crosshair UI picks a specific slot instance directly (the player sees and chooses).

### Decision 9: Locked Frame Layouts (Authoring Assets)

| LayoutId | DisplayName | Player? | Slots |
|---|---|---|---|
| `small_frame` | Scout (Small Frame) | yes | `weapon_front`(Wpn,Front) · `weapon_back`(Wpn,Back) · `engine_0`(Eng,Any) · `mobility_0`(Mob,Any) · `hull_0`(Hull,Any) — **5 slots** |
| `medium_frame` | Assault (Medium Frame) | yes | `weapon_0`(Wpn,Any) · `weapon_1`(Wpn,Any) · `engine_0`(Eng,Any) · `mobility_0`(Mob,Any) · `hull_0`(Hull,Any) — **5 slots** |
| `heavy_frame` | Heavy Truck (Heavy Frame) | yes | `weapon_0`(Wpn,Any) · `weapon_1`(Wpn,Any) · `weapon_2`(Wpn,Any) · `engine_0`(Eng,Any) · `engine_1`(Eng,Any) · `mobility_0`(Mob,Any) · `hull_0`(Hull,Any) — **7 slots** |
| `tiny_frame` | Dune Skimmer | no | `weapon_0`(Wpn,Any) · `engine_0`(Eng,Any) · `mobility_0`(Mob,Any) · `hull_0`(Hull,Any) — **4 slots** |
| `hauler_frame` | Iron Shepherd | no | `weapon_0`(Wpn,Any) · `weapon_1`(Wpn,Any) · `engine_0`(Eng,Any) · `mobility_0`(Mob,Any) · `hull_0`(Hull,Any) — **5 slots** |
| `dredge_frame` | Dredge | no | `weapon_minigun`(Wpn,Front) · `weapon_javelin`(Wpn,Front) · `weapon_ram`(Wpn,Front) · `weapon_flail`(Wpn,Back) · `engine_0`(Eng,Any) · `engine_1`(Eng,Any) · `mobility_0`(Mob,Any) · `hull_0`(Hull,Any) · `armor_chest`(Armor,Front, ExposureMultiplier=3.0, RedirectsTo=`hull_0`) · `armor_back`(Armor,Back, ExposureMultiplier=3.0, RedirectsTo=`hull_0`) — **10 slots** |

**Naming convention** (revised 2026-05-18): LayoutIds are *unsuffixed* (`small_frame`, not `small_frame_player`). Player/enemy/boss disambiguation lives in the asset folder split (`Assets/Data/Vehicle/Frames/Player/` vs `Assets/Data/Vehicle/Frames/Enemy/`) and in the `IsPlayerUnlockable` boolean on `FrameLayoutSO`. The unsuffixed names match GDD R_FL.2, `tr-registry.yaml` FrameLayoutId constants, and the user-locked design decision recorded in `production/session-state/active.md`. The earlier suffixed proposal was withdrawn during reconciliation.

All `IsStructural` defaults apply (true for Hull, false for others). HudAnchor values are authored per layout against the chassis silhouette and validated in the HUD spec, not here.

Player layout assets live at `Assets/Data/Vehicle/Frames/Player/`. Enemy layout assets live at `Assets/Data/Vehicle/Frames/Enemy/`. Loaded via direct SO reference from `ChassisDefinitionSO` (player) and enemy roster SOs (enemy), not via Addressables label — frames are bound at chassis authoring time, not enumerated.

### Decision 10: `SlotStateDTO` Schema V1 → V2; One-Shot Migration

V2 shape:

```csharp
namespace WastelandRun.Save.Dtos
{
    [System.Serializable]
    public sealed record VehicleStateDTO
    {
        public const string SystemId      = "vehicle-state";
        public const int    SchemaVersion = 2;                                  // was 1

        public string LayoutId { get; init; }                                   // NEW — e.g. "small_frame"
        public IReadOnlyDictionary<string /*SlotId*/, SlotStateDTO> Slots { get; init; }   // keyed by SlotId, was keyed by SlotType.ToString()
        // ChassisType, ActiveStatuses, etc. unchanged
    }

    [System.Serializable]
    public sealed record SlotStateDTO
    {
        public string SlotId        { get; init; }    // NEW (was implicit in dictionary key as SlotType.ToString())
        public string PartId        { get; init; }    // null if Empty
        public int    Hp            { get; init; }
        public int    MaxHp         { get; init; }
        public int    PlatingStacks { get; init; }
        // No SlotKind/Position fields — those live on the layout, not in the save.
    }
}
```

**V1 → V2 migration** runs at load time inside `VehicleStateSaveAdapter.FromDto`:

```
if (envelope.schema_version == 1):
    legacy = deserialize as V1 (4 entries keyed by "Weapon"/"Engine"/"Mobility"/"Frame")
    layoutId = legacy.Chassis == "Scout"   ? "small_frame"
             : legacy.Chassis == "Assault" ? "medium_frame"
             : throw SaveMigrationException("Unknown V1 chassis: " + legacy.Chassis)
    slotIdMap = { "Weapon"→"weapon_front" (or first Wpn slot in layout),
                  "Engine"→"engine_0",
                  "Mobility"→"mobility_0",
                  "Frame"→"hull_0" }
    // Small frame has two weapon slots in V2 (front + back) but V1 only had one weapon — assign V1 weapon's PartId to "weapon_front" (designer convention: the V1 weapon was always conceptually front-mounted), leave "weapon_back" Empty
    v2 = build V2 DTO using layoutId + mapped SlotIds
    return v2
```

**`vehicle-state` SystemId is unchanged** — only SchemaVersion bumps. Per ADR-0004 EA-mode policy any non-2 SchemaVersion that isn't 1 is treated as incompatible (forward saves from future versions skip to next candidate; corrupted V1 saves with malformed shape skip per Decision 4 of ADR-0004). V1 is the only migration path the EA shipping build supports; post-EA migration runtime (ADR-0004 R5a) eventually generalizes this.

**Test gate**: a captured V1 save fixture (synthesized via a one-shot V1-writer test helper that we keep in the test project, never in shipping code) round-trips V1 → load-as-V2 → reserialize-as-V2 → load-as-V2 and asserts byte-identical V2 output across the two loads. This proves the V1 reader produces a consistent V2 normal form.

### Decision 11: Damage Pipeline (Consolidated)

**Canonical source:** The full damage pipeline lives in V&P GDD F-VP2 (Step 0 through Step 5, plus the "Canonical Event Order Table"). This ADR does NOT duplicate the pseudocode or the event ordering — V&P F-VP2 is the single source of truth. Implementations MUST match V&P F-VP2 verbatim. CI lock-step compares this section's invariant list against V&P F-VP2's "Ordering contract (locked)" block.

**Locked invariants (mirror V&P F-VP2 Ordering contract — do not edit one without the other):**

1. **Recursive routing.** `ApplyDamage` is reentrant: Armor slots route overflow/exposed damage by calling `ApplyDamage` on `RedirectsToSlotId` with `SafeAmplify(amount, ExposureMultiplier)`. Each invocation captures its own `wasCritical = vehicle.CriticalState` snapshot at **Step 0 (pre-Step-2)**, BEFORE any shield absorption or recursion can mutate state. (Closes Review 4 Blocker #1 — pre-R4 wasCritical placement at Step 4 raced the recursion.)
2. **Atomic event ordering (a → h).** See V&P F-VP2 "Canonical Event Order Table" for the authoritative step list. ADR does not duplicate.
3. **`IsDead` backing field.** `IsDead` is a backing field with a private setter, written in Step 4(f). `OnVehicleDied` fires in Step 4(g) AFTER the write. Subscribers reading `IsDead` from inside any other Step 4 event observe the pre-write value (`false`). Closes V&P Review 2 blocker #4. **Implementation note for `SlotInstance`/`Vehicle`:** declare `IsDead { get; private set; }` — public getter, private setter. No derived getter.
4. **`OnCriticalStateChanged` fires last.** Step 4(h) fires AFTER `OnVehicleDied` (g). Subscribers that need to suppress critical-state UI on death frames check `IsDead` inside the (h) handler.
5. **`OnGrantedCardRemoved` event is hard-removal-only.** Slot Offline does NOT remove granted cards from any zone (deck/hand/discard); cards become unplayable via the source-slot-state playability gate per Decision 16 (analogous to the positional gate). `OnGrantedCardRemoved` fires only for the **hard removal** triggers enumerated in Decision 16 (scrap-time at the V&P boundary per V&P R6; external-source-ended tethers such as Dredge Javelin where the originating system signals termination). Payload is `IReadOnlyList<string>` CardIds — engine-free per ADR-0005. F-VP2's prior "Step 4(d) deck removal on Offline" instruction is **deleted** by Decision 16; the Phase 2 V&P GDD edit prunes it.
6. **Vehicle-level `CurrentArmor` is deleted.** The pre-ADR-0007 V&P F-VP2 "armor between plating and Hull" step is GONE. Armor is per-slot HP only; protection only exists when an Armor slot redirects to a structural slot. The `MaxArmor / CurrentArmor / AddArmor / OnMaxArmorChanged / OnCurrentArmorChanged` surface from ADR-0001/0005 is REMOVED.
7. **Engine-free `IPartData.GrantedCards`.** Pseudocode in V&P F-VP2 Step 4(d) references `slot.InstalledPart.GrantedCards.Select(c => c.CardId)`. Under `noEngineReferences:true`, `IPartData.GrantedCards` MUST be typed `IReadOnlyList<IGrantedCardData>`, where `IGrantedCardData` is an engine-free struct/interface in `WastelandRun.Vehicle` carrying `CardId (string)` + baked `PositionRequirement`. No `CardDefinitionSO` reference crosses the engine boundary. Closes Review 4 Blocker #2. See V&P R4 "`IPartData.GrantedCards` engine-free type" paragraph.

### Decision 12: Repair Path Emits `OnSlotHpChanged` (and `OnSlotDamageStateChanged` on Boundary Cross)

Closes V&P GDD Review 5 blocker #3.

`IVehicleMutator.Repair(slotId, hpRestored, canReviveOffline)` MUST fire `OnSlotHpChanged(slotId, newHp, maxHp)` once the slot's `Hp` mutation is committed, mirroring the damage path's per-delta emission contract (Decision 11 invariant #1 / F-VP2 Step 3). The emission is required on **every** Hp delta — positive (repair, including Offline → Functional revival when `canReviveOffline == true`) and negative (damage) alike. Without this, HUD bar tweens, accessibility "subsystem back online" callouts, and any analytics that watch repair throughput must poll `slot.Hp` per frame to observe restoration. Polling is forbidden per the project's reactive-UI discipline.

**Emission order on a repair tick:**

1. `slot.Hp ← clamp(slot.Hp + hpRestored, 0, slot.MaxHp)`. If `canReviveOffline == true` and `slot.DamageState == Offline`, this read-then-clamp is preceded by a one-time "permission" check: the repair is allowed; otherwise repair is no-op on an Offline slot.
2. `OnSlotHpChanged(slotId, slot.Hp, slot.MaxHp)` fires.
3. If the repair crosses a `DamageState` boundary upward (Offline → Functional, Offline → Degraded, or Degraded → Functional after crossing `DegradedThresholdPct` upward), `OnSlotDamageStateChanged(slotId, fromState, toState)` fires immediately after step 2.
4. No `OnVehicleDied` reverse-fire. Revival of a structural slot from Offline does **not** unset `IsDead` — death is a once-per-combat terminal state per Decision 11 invariant #3. If the combat has not yet ended (i.e., `IsDead` was never written for this combat), revival simply continues normal combat. If `IsDead` was already written, the combat is over and the repair call is undefined behavior at this point (the combat loop should not be calling `Repair` post-death). Implementations MAY assert `!IsDead` in `Repair` in developer builds.
5. `OnCriticalStateChanged` fires per Step 4(h) ordering only if the repair caused `vehicle.CriticalState` to transition (e.g., the last Degraded structural slot was repaired above `DegradedThresholdPct`). Uses the same `wasCritical` snapshot discipline as Decision 15 — the repair entrypoint captures `wasCritical = vehicle.CriticalState` before step 1 and compares after step 3.

**Implementation note**: `Repair` already exists on `IVehicleMutator` (Key Interfaces section) but the ADR did not previously specify event emission. The Phase 2 V&P GDD edit will add a corresponding row in the F-VP2 Canonical Event Order Table; this ADR makes the contract authoritative now so Phase 2 is purely a pruning/pointer edit.

### Decision 13: V&P F-VP2 Canonical Event Order Table Is the Single Source of Truth; CI Lock-Step Gate

Closes V&P GDD Review 5 blocker #4. Generalizes Decision 11 invariant #2.

**Normative authority**: The **V&P GDD F-VP2 "Canonical Event Order Table"** is the single source of truth for atomic event sequencing across `ApplyDamage` and `Repair`. ADR-0007 mirrors the *invariants* in Decision 11's "Locked invariants" list; the GDD owns the *row-by-row ordering*. EC-VP20 prose in the V&P GDD that restates ordering rules competes with the table and is scheduled for pruning in Phase 2 of the R6 sequence (per `production/r5-handoff-adr-0007-amendment-scope.md`). After Phase 2, EC-VP20 should read: "See F-VP2 Canonical Event Order Table for the authoritative event sequencing. Subscriber rules are documented in the table footnotes."

**CI lock-step gate (new)**: A CI check MUST compare ADR-0007 Decision 11's "Locked invariants" numbered list against V&P F-VP2's "Ordering contract (locked)" numbered list. The check fails the build if either list adds, removes, or reorders an invariant without the matching edit landing in the other doc in the same commit. Implementation sketch (Unix shell, runs on the same CI runner that hosts the existing ADR-0004 baseline checks):

```bash
# tools/ci/check-adr0007-vp-lockstep.sh
set -euo pipefail
adr=docs/architecture/adr-0007-frame-driven-variable-slot-system.md
gdd=design/gdd/vehicle-and-part-system.md

# Extract numbered invariant headlines (the bold first sentence after "N. **")
adr_invariants=$(awk '/^\*\*Locked invariants/,/^### /' "$adr" \
                 | grep -E '^[0-9]+\. \*\*[^*]+\.\*\*' \
                 | sed -E 's/^([0-9]+)\. \*\*([^*]+)\.\*\*.*/\1|\2/')

gdd_invariants=$(awk '/Ordering contract \(locked\)/,/^####? /' "$gdd" \
                 | grep -E '^[0-9]+\. \*\*[^*]+\.\*\*' \
                 | sed -E 's/^([0-9]+)\. \*\*([^*]+)\.\*\*.*/\1|\2/')

if ! diff <(echo "$adr_invariants") <(echo "$gdd_invariants") > /tmp/lockstep.diff; then
    echo "ERROR: ADR-0007 Decision 11 invariants drifted from V&P F-VP2 Ordering contract" >&2
    cat /tmp/lockstep.diff >&2
    exit 1
fi
```

The gate covers event-ordering invariants only. It does NOT compare interface declarations — that is the separate "Optional CI Grep Gate" tracked in the handoff doc's §"Optional: CI Grep Gate" section and may be added in a later tooling sprint. This narrower gate is the minimum structural prevention for the R2-R5 drift pattern and is cheap to implement.

**Owner of changes**: Edits to the invariant list in either doc MUST be made in pairs in the same commit. PR description must reference both docs. Reviewers MUST reject single-doc edits to the invariant lists.

### Decision 14: Armor INTACT Branch Emits Steps 3/4 on the Armor Slot

Closes V&P GDD Review 5 blocker #1.

When `ApplyDamage` resolves against an Armor slot with `slot.Hp > 0` (Decision 6 step 2, the "Armor intact" branch — armor absorbs but does not break this hit), the armor slot itself MUST run the canonical Steps 3 and 4 (state-change events + once-per-combat events) for any Hp delta and any `DamageState` transition the absorption causes, exactly as a non-Armor slot does in Decision 11 invariant #2.

**The bug being fixed**: The pre-Phase-1 Decision 6 step 2 spec only described event firing when the armor *broke* (Hp → 0): `if slot.Hp == 0 → fire OnSlotDamageStateChanged → fire OnArmorExposed`. An Armor slot that took damage but stayed above zero silently transitioned `Functional → Degraded` (crossing `DegradedThresholdPct`) with no event firing. HUD subscribers watching for armor-bar damage tween, audio subscribers watching for "armor cracking" SFX, and accessibility subscribers watching for state-change announcements all missed the transition.

**Corrected emission on the Armor INTACT branch (after `slot.Hp ← slot.Hp - armorConsumed` and `slot.Hp > 0`):**

1. `OnSlotHpChanged(armorSlotId, slot.Hp, slot.MaxHp)` — fires on every absorption that produces a non-zero Hp delta.
2. `OnSlotDamageStateChanged(armorSlotId, fromState, toState)` — fires if the absorption crosses `DegradedThresholdPct` (Functional → Degraded). Does NOT fire if the armor stays Functional.
3. `OnCriticalStateChanged(isCritical)` — Armor slots are `IsStructural=false` by default (Decision 6 final paragraph), so an Armor slot's own state transition does NOT change `vehicle.CriticalState`. This event fires only when the *recursive* breakthrough call into a structural slot crosses critical; Decision 15 governs the recursive snapshot discipline that prevents double-firing.

**Worked example (armor absorbs but does not break)**:

Initial: `armor_chest.Hp = 10, MaxHp = 12, DegradedThresholdPct = 50` → DegradedThreshold = `floor(12 × 0.50) = 6`. `vehicle.CriticalState = false`. A 5-damage hit resolves: `armorConsumed = min(5, 10) = 5`, `slot.Hp ← 5`, `overflow = 0` (no breakthrough — Hp > 0). Transition: `Functional → Degraded` (5 < 6).

Events fired in F-VP2 step order, all on the armor slot itself, all in one atomic `ApplyDamage` invocation:

- Step 3: `OnSlotHpChanged("armor_chest", 5, 12)`.
- Step 4(a): `OnSlotDamageStateChanged("armor_chest", Functional, Degraded)`.
- Step 4(b–d): no Offline transition AND no hard-removal trigger fired → no `OnGrantedCardRemoved`. (Even on Offline, deck contents are untouched per Decision 16; cards greyed via Step 4(a) instead.)
- Step 4(e): no Armor break → no `OnArmorExposed`.
- Step 4(f): `IsDead` write — no-op (armor not structural; StructuralHp unchanged).
- Step 4(g): no `OnVehicleDied`.
- Step 4(h): `wasCritical == false`, `CriticalState_now == false` (armor not structural) → no `OnCriticalStateChanged`.

This is a structural correction to the canonical event-order spec, not a polish item. The Phase 2 V&P GDD edit will add a dedicated row to the F-VP2 Canonical Event Order Table for the Armor INTACT branch with the above emission list.

**Distinct from Decision 6 breakthrough branch**: When `overflow > 0` after the armor breaks (`slot.Hp == 0` AND there is leftover damage), Decision 6 step 2's recursive `ApplyDamage(redirectsTo, ...)` call runs *its own* full F-VP2 pipeline on the structural target, AND the armor slot's own Step 4 sequence still fires (with `OnArmorExposed`). The two are interleaved per Decision 15's recursion discipline — not collapsed.

### Decision 15: Recursive `ApplyDamage` Captures a Fresh `wasCritical` Per Invocation; `OnCriticalStateChanged` Is At-Most-Once Per Top-Level Entrypoint

Closes V&P GDD Review 5 blocker #2.

**Snapshot discipline**: Every invocation of `ApplyDamage(slotId, amount, source)` — including recursive invocations spawned by Decision 6's Armor → Hull breakthrough path and the already-exposed redirect path — MUST capture its own `wasCritical = vehicle.CriticalState` snapshot at Step 0 (before any mutation, before shield absorption, before any further recursion). The snapshot is used by Step 4(h) to determine whether `vehicle.CriticalState` actually transitioned during *this* invocation. The pre-Phase-1 Decision 11 invariant #1 mandated Step 0 capture for the outer call but did not explicitly require recursive re-capture; without re-capture, an Armor → Hull breakthrough where the Hull recursion crosses CriticalState had an ambiguous snapshot, producing event misfires.

**Idempotency discipline**: `OnCriticalStateChanged` MUST fire **at most once per top-level `ApplyDamage` entrypoint**, even when recursion would otherwise produce multiple eligible Step 4(h) firings. Idempotency is tracked by a per-top-call boolean `criticalEventFiredThisCall` that the public entrypoint resets and recursive invocations respect.

**Reference implementation pattern**:

```csharp
// Public entrypoint — resets the per-call context.
public void ApplyDamage(string slotId, int amount, DamageSource source) {
    var ctx = new DamageContext {
        WasCriticalAtTopEntry      = CriticalState,                     // top-level snapshot
        CriticalEventFiredThisCall = false,
        TopLevelEntrypointHpDelta  = 0,                                  // aggregate for analytics; optional
    };
    ApplyDamageInternal(slotId, amount, source, ctx);

    // Step 4(h) — fire OnCriticalStateChanged at most once if the top-level call
    // changed CriticalState and no recursive invocation already fired it.
    if (!ctx.CriticalEventFiredThisCall && CriticalState != ctx.WasCriticalAtTopEntry) {
        OnCriticalStateChanged?.Invoke(CriticalState);
        ctx.CriticalEventFiredThisCall = true;
    }
}

// Private recursive worker — receives the shared context.
private void ApplyDamageInternal(string slotId, int amount, DamageSource source, DamageContext ctx) {
    var wasCriticalLocal = CriticalState;       // Step 0 snapshot per invocation (Decision 15 first paragraph)
    // ... Steps 1–4(a–g) per F-VP2 ...
    // Step 4(h) — only the recursion that actually causes the transition fires the event.
    if (!ctx.CriticalEventFiredThisCall && CriticalState != wasCriticalLocal) {
        OnCriticalStateChanged?.Invoke(CriticalState);
        ctx.CriticalEventFiredThisCall = true;
    }
}
```

**Worked example (Armor breakthrough → Hull recursion crosses critical):**

Initial: `vehicle.CriticalState = false`. `armor_chest.Hp = 4, MaxHp = 12`. `hull_0.Hp = 8, MaxHp = 20, DegradedThresholdPct = 50` → DegradedThreshold = `floor(20 × 0.50) = 10`. `armor_chest.ExposureMultiplier = 3.0, RedirectsTo = "hull_0"`.

**Top-level call**: `ApplyDamage("armor_chest", 10, Card)`.
- Public entrypoint: `ctx.WasCriticalAtTopEntry = false`, `ctx.CriticalEventFiredThisCall = false`.
- Inner call: `ApplyDamageInternal("armor_chest", 10, Card, ctx)`.
  - `wasCriticalLocal_outer = false`.
  - Step 2 (Decision 6 Armor branch): absorbs 4 → `armor_chest.Hp = 0`; overflow = 6.
  - Recursive call: `ApplyDamageInternal("hull_0", floor(6 × 3.0) = 18, Card, ctx)`.
    - `wasCriticalLocal_inner = CriticalState` evaluated NOW. At this point `armor_chest.Hp = 0` is committed but armor is non-structural → `CriticalState_inner = false`. So `wasCriticalLocal_inner = false`.
    - Step 2 (inner Hull branch): `hull_0.Hp ← 0` (clamp). Slot Offline. `StructuralHp = 0`.
    - Step 4(a): `OnSlotDamageStateChanged("hull_0", Functional, Offline)`.
    - Step 4(b–d): no deck mutation on Offline per Decision 16 — `hull_0`'s granted cards remain in deck but become unplayable via the source-slot-state playability gate (the existing Step 4(a) `OnSlotDamageStateChanged` triggers HUD greying); `OnGrantedCardRemoved` does NOT fire (no hard-removal trigger present).
    - Step 4(e): n/a (Hull is not an Armor slot).
    - Step 4(f): `IsDead ← true`.
    - Step 4(g): `OnVehicleDied`.
    - Step 4(h): `CriticalState_now = true` (one structural slot Offline); `wasCriticalLocal_inner = false`. Differ → fire `OnCriticalStateChanged(true)`, set `ctx.CriticalEventFiredThisCall = true`. Inner call returns.
  - Outer call resumes after recursion. Step 3: `OnSlotHpChanged("armor_chest", 0, 12)`. Step 4(a): `OnSlotDamageStateChanged("armor_chest", Functional, Offline)`. Step 4(e): `OnArmorExposed("armor_chest", "hull_0")`. Step 4(f): no-op (armor not structural — `IsDead` already true from inner). Step 4(g): no second `OnVehicleDied`. Step 4(h): `CriticalState_now = true`, `wasCriticalLocal_outer = false`, differ — BUT `ctx.CriticalEventFiredThisCall == true` → suppress. Outer call returns.
- Public entrypoint Step 4(h) check: `CriticalState_now = true`, `ctx.WasCriticalAtTopEntry = false`, differ — BUT `ctx.CriticalEventFiredThisCall == true` → suppress. Top-level call returns.

Net firing across the full top-level invocation: `OnCriticalStateChanged(true)` exactly once (from the inner Hull invocation that actually caused the transition). `OnVehicleDied` exactly once. All other per-slot events fire normally.

**Relation to Decision 14**: Decision 14 fixes UNDER-emission on the INTACT path (events that should fire but didn't). Decision 15 fixes the snapshot correctness and OVER-emission risk on the recursive path (events fire correctly but with the wrong `wasCritical` snapshot, potentially double-firing `OnCriticalStateChanged`). The two are independent and both required.

**Test coverage** (referenced by AC additions scheduled for Phase 3):
- AC for the Armor → Hull breakthrough scenario above asserts exactly one `OnCriticalStateChanged(true)` firing.
- AC for a non-breaking Armor INTACT absorption asserts zero `OnCriticalStateChanged` firings even when armor crosses Degraded.
- AC for a non-recursive Hull hit that crosses critical asserts exactly one `OnCriticalStateChanged(true)` firing (regression coverage for the simple path).

### Decision 16: Granted-Card Lifecycle — Soft Disable on Offline, Hard Removal Only on Scrap or External-Source Termination

Closes the user-clarified lifecycle error in Decision 11 invariant #5 (caught between Phase 1 close and session end 2026-05-18). Supersedes the pre-Phase-1 F-VP2 Step 4(d) "deck removal on Offline" instruction.

**The mistake being fixed**: Pre-correction Decision 11 invariant #5 (and the V&P GDD F-VP2 step it mirrored) treated slot Offline as a hard purge of the slot's granted CardIds from the deck. Player-experience design owner clarified that part destruction must behave like a **temporary disablement** — identical pattern to the existing positional gate, where `Overtake` (EnemyAhead) is visibly greyed-out in hand while the player is already ahead and becomes playable again when the positional predicate flips. Repair revival of the source slot must therefore restore card playability with zero hand-shuffling churn. Hard removal is reserved for events that genuinely end card ownership: scrap (the part is gone) and external-source-ended tethers (the granting condition is over).

**Two distinct lifecycle states (locked):**

1. **Soft disable (no deck mutation).** Trigger: source slot transitions `Functional` or `Degraded` → `Offline`. Effect: the slot's granted cards remain in whatever zone they currently occupy (deck, hand, discard, exhaust). They become unplayable via the **source-slot-state playability gate** (defined below). HUD greys them via the existing `OnSlotDamageStateChanged` event from F-VP2 Step 4(a) — no new event needed. Repair revival (Offline → Functional/Degraded per Decision 12) restores playability automatically via the same gate; the revival's `OnSlotDamageStateChanged` triggers HUD un-greying. No `OnGrantedCardRemoved` firing.

2. **Hard removal (atomic sweep across all zones).** Triggers (V&P-bounded enumeration):
   - **Scrap.** Player removes a part via the scrap UI between combats. V&P R6 owns this surface; this ADR mirrors the contract.
   - **External-source termination.** The card's grant source signaled end-of-life from outside V&P. Canonical example: Dredge Javelin Hook adds tether cards to the player deck on hit; when the chain is cut (target killed, range exceeded, or any tether card is played and exhausts the cohort), the granting system (Status Effects or boss script) calls `IVehicleMutator`'s hard-removal surface, which sweeps the cohort from **deck + hand + discard atomically** in one frame.

   Effect: cards are deleted from all zones in a single atomic operation. `OnGrantedCardRemoved(slotId, cardIds)` fires once after the sweep completes. For external-source removals where there is no source slot (Javelin attaches a cohort label, not a slot), `slotId` may be `null` (interface contract: subscribers MUST tolerate `null` slotId and use the CardIds payload as the authoritative key).

**Source-slot-state playability gate** (composition rule with the existing positional gate):

A runtime in-deck card is playable iff **all** of the following are true:
- The energy gate passes (`player.Energy >= card.EnergyCost`).
- The positional gate passes (existing per Decision 7 / ADR-0006 `OverridePositionRequirement`).
- The source-slot gate passes (NEW): if the card carries a non-null `SourceSlotId`, then `vehicle.GetSlot(SourceSlotId).DamageState ∈ { Functional, Degraded }`. If `SourceSlotId` is null (non-slot grant source, e.g. Javelin tether), this gate is a no-op (always passes; hard removal is the only way to terminate non-slot grants).

The gate is a pure predicate evaluated at hand-render time and at play-attempt time. UI greys cards that fail any gate using a uniform "unplayable" treatment regardless of which gate failed (tooltip can distinguish: "Engine offline", "Wrong position", "Insufficient energy").

**Runtime card-record amendment** (delta against ADR-0006):

ADR-0006 Decision 6 specified that runtime in-deck `CardData` records derived from a `CardDefinitionSO` carry an `OverridePositionRequirement` projected from the source part's `MountDirection` (Decision 7 of this ADR sets the projection). Decision 16 extends the runtime record with one nullable field:

```csharp
public sealed record CardData {
    // ... existing ADR-0006 fields ...
    public string? SourceSlotId { get; init; }    // NEW per ADR-0007 Decision 16.
                                                  // Null for non-slot grants (Javelin cohort, neutral starter cards).
                                                  // Non-null for any card originating from IPartData.GrantedCards;
                                                  // baked at card-grant time (part install OR mid-combat slot-grant).
}
```

The Phase 2 V&P GDD edit will add a one-paragraph reference back to ADR-0006 noting the new field; the field itself is owned by Card Combat per ADR-0006, not by V&P. CI grep for `SourceSlotId` ensures it lands in both docs.

**F-VP2 changes required (Phase 2 prune)**:

- Step 4(d) in the F-VP2 pseudocode currently reads (pre-Phase-1): "Remove `slot.InstalledPart.GrantedCards.Select(c => c.CardId)` from `playerDeck` and fire `OnGrantedCardRemoved`." Phase 2 MUST replace this with: "No-op for Offline transitions. Deck composition is unchanged; cards become unplayable via the source-slot playability gate (ADR-0007 Decision 16). `OnGrantedCardRemoved` does NOT fire on Offline."
- F-VP2 Canonical Event Order Table row for Step 4(d): change column from `OnGrantedCardRemoved` to `(no event — soft disable per ADR-0007 Decision 16)`.
- A new F-VP2 section "Hard Removal Pathway" must enumerate the two triggers (scrap, external-source termination) and document the atomic-sweep event firing.

**Mutator surface (extension to `IVehicleMutator`)**:

```csharp
public interface IVehicleMutator {
    // ... existing surface ...

    // Hard removal — sweeps the named cards from player deck + hand + discard atomically.
    // Fires OnGrantedCardRemoved(sourceSlotId, removedCardIds) ONCE after the sweep.
    // sourceSlotId is null for external-source removals (Javelin cohort).
    // removedCardIds is the cohort of CardIds the caller wants gone in this operation.
    void HardRemoveCards(string? sourceSlotId, IReadOnlyList<string> cardIds);
}
```

Scrap callers (V&P R6) invoke `HardRemoveCards(slotId, slot.InstalledPart.GrantedCards.Select(c => c.CardId).ToList())` as part of part-removal. External-source callers (Status Effects, boss scripts) invoke with `sourceSlotId = null` and pass the cohort label's CardId set.

**Worked example A (Engine destroyed mid-combat, then repaired)**:

Setup: `engine_main` is Functional and holds part `engine_v8` whose `GrantedCards = [{ CardId: "nitro_boost" }]`. The card `nitro_boost` is currently in the player's discard pile (used earlier this combat). Player deck contains 4 cards; hand contains 3 cards; discard contains 5 cards (one of which is `nitro_boost`). `vehicle.CriticalState = false`.

A 100-damage Hull hit overflows into the engine via redirect. `engine_main.Hp → 0`. F-VP2 Step 4(a): `OnSlotDamageStateChanged("engine_main", Functional, Offline)`. Step 4(b–d): NO deck mutation. `nitro_boost` remains in discard. HUD subscriber (hand-render code) re-evaluates playability for cards currently in hand — none have `SourceSlotId == "engine_main"` so no greying happens this frame. The discard-pile inspector UI (when opened) greys `nitro_boost` because its source-slot gate now fails. `OnGrantedCardRemoved` does NOT fire.

Three turns later, player plays a `Repair Kit` card that calls `Repair("engine_main", 8, canReviveOffline: true)`. Per Decision 12: `OnSlotHpChanged` fires, then `OnSlotDamageStateChanged("engine_main", Offline, Degraded)` fires. HUD subscriber re-evaluates playability: `nitro_boost` in discard is now flagged playable again. If the player draws it next turn, it plays normally with no special handling. Zero hand-shuffling occurred across both transitions.

**Worked example B (Dredge Javelin Hook — external-source-ended hard removal)**:

Setup: Dredge boss script attaches a tether to the player. On the hit-resolution frame, the boss script calls `playerVehicle.HardRemoveCards(null, ...)` (no — this is the ADD path, not removal; description clarifies):

Correction — javelin ADD path goes through a separate "GrantCards" surface (not specified in this ADR — owned by Status Effects / boss-script ADR). What this Decision 16 specifies is the REMOVAL path: when the chain is cut.

Sequence:
1. Boss tether is active. Player deck contains 4 normal cards + 2 javelin tether cards (`tether_a`, `tether_b`). Hand contains 3 cards including `tether_a`. Discard contains 2 javelin tether cards (`tether_c`, `tether_d` — from earlier hits).
2. Player plays `tether_a` from hand (resolving its effect = breaking free of the tether). The card's effect handler calls `vehicle.HardRemoveCards(sourceSlotId: null, cardIds: ["tether_a", "tether_b", "tether_c", "tether_d"])`.
3. `HardRemoveCards` atomically removes all four from deck (1 hit), hand (1 hit — `tether_b` does not match because it's in deck, but if it were in hand it would go), and discard (2 hits). Actually: matches by CardId, removes wherever found, single atomic operation.
4. `OnGrantedCardRemoved(null, ["tether_a", "tether_b", "tether_c", "tether_d"])` fires once.
5. HUD subscriber removes the four card UI elements from all visible zones in one frame. No CriticalState change. No vehicle-death implications.

Subsequent Dredge javelin hits run the GRANT path (separate surface) to attach a fresh cohort with new CardIds — same termination semantics apply when the next chain is cut.

**Rationale (non-binding — for future-reviewer judgment)**:

The soft-disable rule preserves the player's run-progression intuition: damaging a part should not feel like a card-economy penalty on top of the stat penalty. The positional-gate pattern is already established for `EnemyAhead`/`EnemyBehind` cards (visible-but-unplayable, brightens when condition flips) and uses the same UI affordance, so this rule reuses an existing mental model. The hard-removal pathway exists for the cases where the card ownership genuinely ends — keeping the deck composition coherent with the player's actual deckbuilding choices and active boss-tether state. Splitting along these lines also avoids the cross-system race condition where simultaneous slot Offline + status-driven re-grant would create a remove-then-add churn that the player would see as a confusing hand-shuffle.

**Test coverage** (referenced by AC additions scheduled for Phase 3):
- AC for soft disable: slot Offline transition with granted card in hand → card flagged unplayable; deck/hand/discard counts unchanged; no `OnGrantedCardRemoved` firing.
- AC for repair restoration: Offline → Functional via `Repair(..., canReviveOffline: true)` → card flagged playable again; zero zone moves between the two transitions.
- AC for source-slot gate composition: card with `SourceSlotId="weapon_left"` AND `OverridePositionRequirement=EnemyAhead` AND `EnergyCost=2` → all three gates must pass for `IsPlayable` to return true; flipping any one gate independently flips `IsPlayable` to false.
- AC for hard removal sweep: `HardRemoveCards(slotId, [cardIds])` with cards distributed across deck/hand/discard → all matching CardIds removed in one frame; one `OnGrantedCardRemoved` firing with the full removed-CardId set; non-matching cards in those zones untouched.
- AC for `null` `sourceSlotId` external-source path: Javelin-style removal with `sourceSlotId=null` → subscribers receive `(null, cardIds)` payload and tolerate it (regression coverage for the nullable contract).

**Relation to Decisions 11–15**:

- Decision 11 invariant #5 was the original (wrong) hard-removal-on-Offline rule. Decision 16 supersedes it; the invariant text has been rewritten in this same Phase 1 amendment commit.
- Decision 12 (repair path) is the natural counterpart: Offline → Functional repair triggers the un-greying via the same `OnSlotDamageStateChanged` event Decision 16 leans on. The two decisions together close the lifecycle loop.
- Decisions 13–15 are independent and continue to apply unchanged.

### Key Interfaces (Locked by This ADR)

```csharp
// WastelandRun.Vehicle (noEngineReferences: true)

public enum SlotKind     { Weapon, Engine, Mobility, Hull, Armor }
public enum SlotPosition { Any, Front, Back }
public enum DamageState  { Empty, Functional, Degraded, Offline }       // unchanged from ADR-0005
public enum DamageSource { Card, Status, Environment }                  // unchanged from ADR-0005

public interface IFrameLayout {
    string LayoutId { get; }
    bool   IsPlayerUnlockable { get; }
    IReadOnlyList<SlotDefinition> Slots { get; }
    int    DegradedThresholdPct { get; }                                 // 1..99; applied uniformly to all slots in this layout per V&P F-VP1. Lives on the interface (Review 4 Blocker #3) so engine-free WastelandRun.Vehicle can compute DegradedThreshold without referencing FrameLayoutSO. All 6 EA layouts ship with 50.
}

[System.Serializable]                                                    // REQUIRED: Unity 6.3 enforces fields-only `[SerializeField]`. Without [System.Serializable], `HudAnchor` on SlotDefinition silently fails to serialize in the Unity inspector under 6.3 (engine-reference/unity/breaking-changes.md — `[SerializeField]` fields-only). Closes R5 blocker #14.
public struct AnchorPoint {                                              // engine-free 2D anchor (replaces Vector2 inside engine-free assembly)
    public float X;
    public float Y;
    public bool IsFinite     => !float.IsNaN(X) && !float.IsInfinity(X) && !float.IsNaN(Y) && !float.IsInfinity(Y);  // true when neither component is NaN/Inf; expression-bodied so no backing field is generated (would break Unity 6.3 fields-only serialization rule per line 40).
    public bool IsInUnitRect => IsFinite && X >= 0f && X <= 1f && Y >= 0f && Y <= 1f;                                // IsFinite AND both ∈ [0,1]; expression-bodied (no backing field).
}

[System.Serializable]                                                    // REQUIRED: SlotDefinition is serialized inside FrameLayoutSO.slots (Architecture Diagram line ~861). Unity 6.3 `[SerializeField]` is fields-only — auto-property syntax (`{ get; }`) generates a compiler-hidden backing field that does NOT serialize. All members below MUST be plain public fields. Closes R6 Blocker 2 (unity-specialist).
public struct SlotDefinition {
    public string         SlotId;
    public SlotKind       Kind;
    public SlotPosition   Position;
    public bool           IsStructural;
    public AnchorPoint    HudAnchor;                                     // engine-free; replaces Vector2 per ADR-0005 noEngineReferences
    public int            MaxHpOverride;                                 // Armor: required (≥1); non-Armor: -1 sentinel = "use chassis default"
    public float          ExposureMultiplier;
    public string         RedirectsTo;
}

public interface IVehicleView {                                          // REPLACES ADR-0001/0005 surface
    ChassisType  Chassis        { get; }
    IFrameLayout Layout         { get; }                                 // runtime layout reference
    string       FrameLayoutId  { get; }                                 // computed: Layout.LayoutId; persisted by VehicleStateDTO
    IReadOnlyList<SlotInstance> Slots { get; }
    SlotInstance GetSlot(string slotId);                                 // throws SlotNotFoundException
    IReadOnlyList<SlotInstance> GetSlotsByKind(SlotKind kind);
    int  StructuralHp { get; }
    bool IsDead       { get; }                                           // BACKING FIELD per F-VP2 step (f); not a derived getter
    bool CriticalState { get; }                                          // computed: ≥1 IsStructural slot Degraded OR Offline (per V&P R9)

    // R9 view-model properties (mirrored from V&P GDD R9 — were missing from this ADR pre-Phase-1; closes R5 missing-R9-members coverage)
    IReadOnlyList<StatusInstance> ActiveStatuses { get; }                // owned by Vehicle POCO per Status Effects GDD; consumed by HUD, Card Combat targeting, Enemy AI targeting (V&P I1/I2/I8)
    float GetStatModifier(StatType stat);                                // composed across installed parts per V&P F-VP3; consumed by Card Combat for damage math, by Scrap Economy for refund composition

    // Events (per-slot keyed by SlotId — was keyed by SlotType)
    event Action<string /*SlotId*/, DamageState, DamageState>          OnSlotDamageStateChanged;
    event Action<string /*SlotId*/, int /*newHp*/, int /*maxHp*/>      OnSlotHpChanged;          // per V&P Review 2 blocker #2; fires from BOTH damage path (Decision 11) AND repair path (Decision 12)
    event Action<string /*SlotId*/, int /*newStacks*/>                 OnPlatingChanged;         // fires when slot.PlatingStacks mutates (AddPlating, plating-consumption hit per Decision 6 step 1, status-driven plating strips). Closes R5 blocker #5. Subscriber: HUD plating-overlay sprite count, accessibility "shield stack: N" callout. No-op for slots where Kind == Armor (Armor cannot carry plating per Decision 6).
    event Action<string /*SlotId*/, StatusType, int /*newStacks*/>     OnStatusStackChanged;     // fires when an active status's stack count changes between Applied and Expired (e.g., Corroded ticks down from 3 → 2, or stacks merge from a new application). Distinct from OnStatusApplied (initial application, newStacks > 0 from zero) and OnStatusExpired (newStacks → 0). Closes R5 blocker #6. Subscriber: HUD status icon stack-count badge, accessibility tick announcements.
    event Action                                                       OnVehicleDied;            // fires AFTER IsDead backing field updates (per V&P Review 2 blocker #4); F-VP2 step (g)
    event Action<bool /*isCritical*/>                                  OnCriticalStateChanged;   // fires when CriticalState transitions during a damage step; F-VP2 step (h); enables HUD vignette / audio heartbeat. At-most-once per top-level ApplyDamage entrypoint per Decision 15.
    event Action<string /*ArmorSlotId*/, string /*RedirectsToSlotId*/> OnArmorExposed;           // fires when Armor slot transitions to Hp==0 (Decision 6 + 14)
    event Action<string? /*SourceSlotId*/, IReadOnlyList<string> /*CardIds*/> OnGrantedCardRemoved;  // fires ONLY for hard-removal triggers per Decision 16 (scrap-time per V&P R6; external-source-ended tethers e.g. Dredge Javelin). Does NOT fire on slot Offline — Decision 16 made slot Offline a soft-disable that leaves cards in-zone and unplayable via the source-slot playability gate. SourceSlotId is null for external-source removals where the grant did not originate from a slot (subscribers MUST tolerate null). Subscribers resolve CardIds via ICardCatalog (declared below).
    event Action<string /*SlotId*/, string /*PartId*/>                 OnPartInstalled;
    event Action<string /*SlotId*/, string /*PartId*/>                 OnPartRemoved;
    event Action<string /*SlotId*/, StatusType>                        OnStatusApplied;
    event Action<string /*SlotId*/, StatusType>                        OnStatusExpired;

    // MaxArmor / CurrentArmor / OnMaxArmorChanged / OnCurrentArmorChanged from ADR-0001/0005 are REMOVED.
}

public interface IVehicleMutator {                                       // REPLACES ADR-0001/0005 surface
    void ApplyDamage(string slotId, int amount, DamageSource source);    // SlotId-keyed (was SlotType)
    void Repair(string slotId, int hpRestored, bool canReviveOffline);
    void AddPlating(string slotId, int stacks);
    void InstallPart(string slotId, IPartData part);                     // throws MountIncompatibleException (Decision 5)
    void RemovePart(string slotId);
    void ApplyStatus(StatusType type, int duration, int stacks, string targetSlotId);
    void RemoveStatus(StatusType type, string targetSlotId);
    void TickStatuses();
    void HardRemoveCards(string? sourceSlotId, IReadOnlyList<string> cardIds);  // per Decision 16 — atomic sweep across deck + hand + discard; fires OnGrantedCardRemoved once. Callers: V&P R6 scrap path (sourceSlotId = the slot being scrapped) and external-source termination (sourceSlotId = null, e.g. Dredge Javelin chain cut).
    // AddArmor (ADR-0005 vehicle-level) is REMOVED. To restore Armor HP on an Armor slot, use Repair(armorSlotId, n).
}

public interface IPartData {                                             // EXTENDS ADR-0005
    string        PartId         { get; }
    SlotKind      SlotKind       { get; }                                // was SlotType (renamed)
    SlotPosition  MountDirection { get; }                                // NEW
    IReadOnlyList<IGrantedCardData> GrantedCards { get; }                // engine-free per Decision 11 invariant #7; see IGrantedCardData below
    // ... existing ADR-0005 fields unchanged ...
}

// ─── Engine-free card-projection types (placement: WastelandRun.Vehicle, noEngineReferences:true) ───
//
// IGrantedCardData and ICardCatalog live in the engine-free Vehicle assembly so that
// IPartData.GrantedCards and OnGrantedCardRemoved subscribers can resolve card identity
// without crossing the engine boundary. Authoring assets (CardDefinitionSO in
// WastelandRun.Cards) project to IGrantedCardData at load time via the same pattern as
// PartDefinitionSO → IPartData (ADR-0005). The Cards assembly may layer richer projection
// surfaces (e.g., an ICardRichData interface returning art/localized name) — those are
// NOT V&P's concern and do NOT belong in this engine-free assembly.

public interface IGrantedCardData {                                      // engine-free; placement: WastelandRun.Vehicle. Closes R5 blocker #18 (part 2): explicit assembly-placement clause.
    string              CardId              { get; }                     // stable identifier; matches CardDefinitionSO.CardId
    PositionRequirement PositionRequirement { get; }                     // baked at install time per Decision 5 (MountDirection → RequiresAhead/RequiresBehind; Any → card template default). Type owned by WastelandRun.Vehicle (see enum declaration below) so the engine-free Vehicle assembly does not cycle with WastelandRun.Cards. Closes R6 recommended-item BOUND-3 (unity-specialist).
}

// PositionRequirement enum — canonical owner: WastelandRun.Vehicle (engine-free, noEngineReferences:true).
// Was previously documented as "CardPositionRequirement" in pre-R6 ADR-0007 drafts; renamed to match
// card-system.md §1 canonical field name (`PositionRequirement` on CardDefinitionSO). Placement here
// (Vehicle assembly) rather than WastelandRun.Cards prevents a Vehicle → Cards reference cycle:
// IGrantedCardData lives in Vehicle, so its enum field must also live in Vehicle. CardDefinitionSO
// in WastelandRun.Cards references this enum via the one-way Cards → Vehicle assembly dependency.
public enum PositionRequirement { None, RequiresBehind, RequiresAhead, BonusIfBehind, BonusIfAhead }

public interface ICardCatalog {                                          // engine-free interface; placement: WastelandRun.Vehicle. Closes R5 blocker #7. Sole projection surface for CardId → IGrantedCardData resolution at the V&P boundary. Consumed by OnGrantedCardRemoved subscribers, by Save adapters needing card identity reconstitution, and by Enemy AI targeting code that references granted-card metadata.
    IGrantedCardData   GetGrantedCard(string cardId);                    // returns null if cardId is unknown (consumer decides whether to skip, log, or throw)
    IReadOnlyList<IGrantedCardData> Resolve(IReadOnlyList<string> cardIds);  // batch resolve, matching OnGrantedCardRemoved payload shape; null entries preserved at the index of unresolved CardIds
}
// ICardCatalog *implementation* placement: WastelandRun.Gameplay (Addressables-backed concrete class,
// projects CardDefinitionSO → IGrantedCardData at load time per the same pattern as PartDefinitionSO →
// IPartData in ADR-0005). The interface side stays engine-free; only the impl assembly may reference
// UnityEngine.ScriptableObject. Closes R6 recommended-item BOUND-1 (unity-specialist).

// ─── DamageContext — per-top-call shared state for the reentrant ApplyDamage pipeline ───
// Referenced extensively in Decision 15 (Reference Implementation Pattern, lines ~585-609) and Decision 11
// invariant #2. Declared here to close R6 recommended-item BOUND-3 / SER-1 (unity-specialist:
// "DamageContext referenced extensively but never formally declared"). Placement: WastelandRun.Vehicle
// (engine-free) because the Vehicle POCO owns ApplyDamage and creates the context. Allocation strategy:
// stack-allocated `struct` (no per-call heap allocation) — closes R6 unity-specialist DET-3.
public struct DamageContext {
    public bool WasCriticalAtTopEntry;                                   // CriticalState read at the public entrypoint, BEFORE any mutation. Used by Step 4(h) to detect whether vehicle.CriticalState actually transitioned during this top-level call.
    public bool CriticalEventFiredThisCall;                              // OnCriticalStateChanged idempotency latch per Decision 15 (at-most-once per top-level entrypoint). Reset to false by the public entrypoint; set true by any invocation that fires Step 4(h).
    public int  TopLevelEntrypointHpDelta;                               // optional aggregate (Decision 15 reference pattern); analytics-only, not consumed by the canonical event order. Implementations MAY omit if analytics is disabled.
}

// VehicleStatePreview — declared in V&P GDD §R12 around line 675 (returned by IPartCatalog.PreviewInstall).
// Placement: WastelandRun.Vehicle (engine-free). Fields-only `sealed class` with public mutable fields
// (matches V&P GDD authoring). Listed here for completeness so the Architecture Diagram below is
// consistent with the GDD's interface surface. Closes R6 recommended-item BOUND-2 (unity-specialist).
```

### Architecture Diagram

```
┌──────────────────────────────────────────────────────────────────────┐
│  WastelandRun.Gameplay.asmdef (+ UnityEngine)                        │
│    [CreateAssetMenu] FrameLayoutSO : IFrameLayout                    │
│      slots: SlotDefinition[] (designer-ordered, unique SlotIds)      │
│      OnValidate: SlotId uniqueness, structural-pool non-empty,       │
│                  Armor.RedirectsTo points to existing structural,    │
│                  ExposureMultiplier > 0                              │
│    PartDefinitionSO extended with SlotPosition MountDirection field  │
│    PartDefinitionSO.ArmorContribution: legacy field (per V&P R4      │
│      amendment) — retained for Armor-slot MaxHp derivation only;     │
│      MUST be IL2CPP-preserved alongside FrameLayoutSO below.         │
│    ChassisDefinitionSO holds FrameLayoutSO reference                 │
│    Enemy roster SOs hold FrameLayoutSO reference                     │
│    AddressablesCardCatalog : ICardCatalog (impl — projects           │
│      CardDefinitionSO → IGrantedCardData at load; interface is       │
│      engine-free, impl is here)                                      │
│                                                                      │
│    link.xml: preserve WastelandRun.Gameplay.Vehicle.FrameLayoutSO    │
│    link.xml: preserve PartDefinitionSO (covers ArmorContribution     │
│      field reflection per ADR-0005 namespace pattern; closes R6      │
│      unity-specialist SER-3 IL2CPP preservation gap)                 │
└────────────────────────┬─────────────────────────────────────────────┘
                         │ one-way reference (SO → POCO projection)
                         ▼
┌──────────────────────────────────────────────────────────────────────┐
│  WastelandRun.Vehicle.asmdef (noEngineReferences: true)              │
│    enums: SlotKind, SlotPosition, DamageState, DamageSource,         │
│           PositionRequirement                                        │
│    struct AnchorPoint (engine-free; field-only + computed helpers)   │
│    struct SlotDefinition (engine-free; field-only `[Serializable]`)  │
│    struct DamageContext (stack-allocated per top-level ApplyDamage)  │
│    interface IFrameLayout                                            │
│    interface IGrantedCardData, ICardCatalog                          │
│    sealed class VehicleStatePreview (returned by IPartCatalog)       │
│    class SlotInstance (runtime state per slot)                       │
│    class Vehicle : IVehicleView, IVehicleMutator                     │
│      - Slots: IReadOnlyList<SlotInstance> (variable length)          │
│      - _bySlotId: Dictionary<string, SlotInstance> (O(1) lookup)     │
│      - _byKind:   Dictionary<SlotKind, List<SlotInstance>>           │
│      - StructuralHp / IsDead                                         │
│      - ApplyDamage routing: Armor → exposure or absorb; else plating │
│        → Hp                                                          │
│    interface IPartData (+ MountDirection)                            │
│    exceptions: SlotNotFoundException, MountIncompatibleException     │
└────────────────────────┬─────────────────────────────────────────────┘
                         │ referenced by
                         ▼
┌──────────────────────────────────────────────────────────────────────┐
│  WastelandRun.Combat.asmdef (noEngineReferences: true)               │
│    Reads IVehicleView.GetSlot(slotId), GetSlotsByKind(kind),         │
│      StructuralHp, IsDead                                            │
│    Writes via IVehicleMutator.ApplyDamage(slotId, ...)               │
│    Card resolution branches on ICardEffect; SlotKind-typed effects   │
│      use GetSlotsByKind for targeting                                │
│    AI implicit-target picker: WeakestInstance default; per-intent    │
│      override per BrainRulesetSO                                     │
└────────────────────────┬─────────────────────────────────────────────┘
                         │
                         ▼
┌──────────────────────────────────────────────────────────────────────┐
│  WastelandRun.Save.Dtos (in WastelandRun.Save.asmdef)                │
│    VehicleStateDTO (SystemId="vehicle-state", SchemaVersion=2)       │
│      SchemaVersion is a `public const int` on VehicleStateDTO        │
│        (Decision 10 listing line 435 — authoritative declaration).   │
│        ADR-0004 Save Registry reads this constant via reflection;    │
│        NOT redeclared elsewhere. Closes R6 unity-specialist SER-1.   │
│      LayoutId: string                                                │
│      Slots: IReadOnlyDictionary<SlotId, SlotStateDTO>                │
│    VehicleStateSaveAdapter.FromDto: V1→V2 one-shot migration.        │
│      Migration is on VehicleStateDTO (the envelope), not on          │
│      SlotStateDTO (which has no version of its own — its shape is    │
│      defined by the envelope's SchemaVersion). Closes R6             │
│      unity-specialist SER-2 (AC-VP33b ambiguity).                    │
└──────────────────────────────────────────────────────────────────────┘
```

## Alternatives Considered

### Alternative 1: Extend `SlotType` Enum With New Values (Keep Dictionary Shape)

**Description**: Keep `SlotType` as a flat enum; add values like `Weapon2`, `Weapon3`, `Engine2`, `ArmorFront`, `ArmorBack`. `Vehicle.Slots` stays `IReadOnlyDictionary<SlotType, SlotState>`. Each chassis declares which enum values it uses.

**Pros**: Minimal disruption to `Dictionary<SlotType, _>` call sites. Save schema barely changes (still keyed by enum string). No new `SlotInstance` type — `SlotState` survives.

**Cons**: Enum explodes combinatorially as new layouts ship (every Heavy frame slot count or Dredge-style boss requires more enum members). Enum values are not designer-authored — they're code constants, so every new layout requires a programmer edit + recompile. The position concept (Front/Back) does not fit cleanly: `Weapon_Front_Mount_2` becomes a nonsense identifier when authored per chassis. Card effect records (ADR-0006) targeting `SlotType` become ambiguous: does `RestorePlatingEffect(stacks=2, TargetSlot=Weapon)` mean Weapon1 or Weapon2 on a Medium frame? Forces per-effect duplication or a new `SlotSelector` indirection — which is exactly the variable-slot abstraction this alternative was trying to avoid.

**Rejection Reason**: Solves the immediate "more slots" symptom without solving the underlying "slots are designer-authored, ordered, positional" requirement. The enum becomes a code-bound registry of designer data, violating the project's tech-prefs forbidden pattern "hardcoded gameplay values" by gating layout authoring on a recompile.

### Alternative 2: Flat List With Stringly-Typed `kind`

**Description**: `Vehicle.Slots` is a `List<Slot>` where `Slot` carries a `string Kind` (not an enum). `FrameLayoutSO` declares slots with arbitrary kind strings. Cards target by kind-string match.

**Pros**: Maximum flexibility — designers invent new kinds without code changes. Future kinds (e.g., "Shield", "Cargo", "Generator") cost zero code.

**Cons**: Loses exhaustive `switch` over `SlotKind` in Combat resolution (no compile-time enforcement that every kind has a handler). Loses the GDD's clean kind taxonomy — designer can typo `"weappon"` and the system silently treats it as a new kind. Loses static analysis: `if (slot.Kind == "Hull")` is unsearchable and unrenameable. Card effect serialization becomes brittle (a renamed kind silently breaks loaded saves). The five-kind taxonomy is *deliberately* small and stable per the GDD — flexibility we don't want is a cost we shouldn't pay.

**Rejection Reason**: The variability we need is in *layout* (how many slots, where, with what positions), not in *kind* (the gameplay categories). `SlotKind` as an enum + `SlotId` as a stable string gives us both: variable topology with stable categorical handling.

### Alternative 3: Composite Slots (Armor As Sub-Component of Hull, Not Sibling)

**Description**: Armor is not a separate `SlotKind` — it's an optional sub-component on a structural slot. `SlotInstance` gains `ArmorHp` / `MaxArmorHp` fields when authored to have armor. Damage flows ArmorHp → Hp inside a single slot.

**Pros**: One fewer `SlotKind`. Damage routing is simpler (no slot-to-slot redirect). Matches the V&P GDD's original mental model of armor as a vehicle-level layer, demoted to per-slot.

**Cons**: Armor as a sub-component can't have its own `Position` independent of the structural slot (the Dredge can't have a Front armor plate redirecting to a chassis-wide Hull). Armor sub-component can't be targeted by cards as a slot (e.g., "Restore Armor on `armor_chest`" requires armor be a first-class slot). HUD anchor for armor sprite is forced to coincide with its parent slot's anchor, foreclosing the "armor floats above the silhouette as a removable plate" visual design. Future post-EA designs (player chassis with detachable armor plates as a meta-progression unlock) are foreclosed.

**Rejection Reason**: The Dredge boss design (3 weapons front + 1 weapon back + 2 armor plates protecting one shared Hull) literally cannot be expressed in this model. Promoting Armor to a first-class slot costs one enum value and one redirect lookup; the design flexibility gained is the difference between "Dredge fits the system" and "Dredge needs a bespoke boss-only code path."

## Consequences

### Positive

- **Boss-class designs become expressible** without bespoke code paths. The Dredge layout is data, not a special case.
- **Chassis identity is now a structural variable**, not just a stat-and-art variable. Small/Medium/Heavy feel mechanically distinct because their slot counts differ.
- **SlotId-keyed events and APIs** are designer-readable in logs and saves (`"armor_chest exposed"` reads better than `"slot index 8 transitioned"`).
- **Mount-direction install gating** is a single new check; once in place, the entire positional-weapon design space opens without per-card authoring overhead.
- **`PositionRequirement` derivation from `MountDirection`** removes a class of authoring errors (designer forgetting to set the card's position requirement to match the weapon's mount).
- **Death condition generalizes cleanly** — `StructuralHp == 0` is one rule; future "two-Hull tank" or "Hull-less drone with structural Engine" designs are expressible.
- **Save schema is human-readable** — V2 saves persist `LayoutId` and SlotIds as strings, debuggable by hand.
- **Determinism preserved** — `WeakestInstance` tie-breaker uses lexicographic SlotId, no RNG draws spent on tie resolution.
- **ADR-0001 visual decisions reused intact** — URP Sprite Lit shader, MPB damage overlays, Addressables groups all apply to per-slot rendering whether N=4 or N=10.

### Negative

- **Existing 136 Combat tests must be migrated** from `Slots[Frame]` patterns to `GetSlot("hull_0")` or `GetSlotsByKind(Hull).First()` patterns. Mechanical, not conceptual, but high churn.
- **`SlotState` → `SlotInstance` rename** ripples through all call sites that touched the type name (smaller blast radius than the API change above, but still notable).
- **HUD work is materially larger** — slot anchors must be per-layout authored in `FrameLayoutSO.HudAnchor` rather than hardcoded against the 4-slot chassis silhouette. The HUD spec must be updated to consume `IVehicleView.Slots` enumeration with `HudAnchor` rather than fixed-position renderers per slot type.
- **Status effects re-keying**: `ApplyStatus(StatusType, duration, stacks, SlotType? targetSlot)` → `ApplyStatus(StatusType, duration, stacks, string targetSlotId)`. Status DOTs that previously targeted "the Weapon slot" must now target a specific SlotId; the Status Effects GDD/ADR must specify whether per-kind status effects pick a slot via `WeakestInstance` default or require explicit per-slot authoring. Surfaced as risk row R-SE below.
- **Card effects targeting `SlotType`** (`RestorePlatingEffect(stacks, SlotType TargetSlot)` per ADR-0006) must change to `SlotKind TargetSlotKind` + an `ImplicitTargetSpec` for "which instance of that kind." For EA scope where most cards address a single instance, the default `WeakestInstance` reproduces existing behavior — but the field shape changes. Card SO migration script (Migration step 7) must rewrite these fields too.
- **V&P GDD revision must come after this ADR** (cannot proceed in parallel). Adds one sequencing dependency to the design sprint.
- **Enemy roster `biome-1-enemy-roster.md` revision must come after V&P GDD** revision (which comes after this ADR). The roster file currently assumes the 4-slot model; the Dredge bespoke layout cannot be properly specified until this ADR ships.
- **`Vehicle.MaxArmor`/`CurrentArmor` removal** invalidates any UI binding that read them. The HUD's vehicle-Armor bar element (currently spec'd in the combat HUD backlog per memory) must either become "Armor slot HP sum, projected by HUD" or be redesigned around per-slot armor sprites — a HUD-layer call, surfaced as a HUD work item, not blocking this ADR.

### Neutral

- **`SlotKind.Armor` adds one new gameplay category** but no new authoring burden in EA (only the Dredge uses it; player frames don't have Armor slots in EA).
- **Future Armor-bearing player frames** become trivial to author (add Armor slots to the layout, balance the `ExposureMultiplier` and `RedirectsTo`) — but no EA scope is committed.
- **Performance is a wash** — N=10 worst case slot list iteration vs. fixed-4 Dictionary lookup is below noise on a 60fps budget where slot calls are turn-scoped (not per-frame).

## Risks

| ID | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| R-MIG | V1 save migration produces wrong V2 normal form for edge cases (e.g., V1 with `Frame` part null) | MEDIUM | HIGH (player loses a run on patch day) | Captured V1 fixture round-trip test (Migration step 6); test covers (a) full V1 save, (b) V1 with one slot Empty, (c) V1 with Frame at 1 HP (Degraded threshold edge), (d) V1 with active statuses on each slot. Ship gate: 100% of fixtures load without exception and produce identical normal form on re-save. |
| R-DRIFT | SlotId rename mid-layout silently breaks loaded saves (Designer renames `weapon_front`→`weapon_primary` in `small_frame`) | MEDIUM | MEDIUM (existing saves of in-progress runs throw SlotNotFoundException on load) | **Add a `SlotId stability** convention: SlotIds are append-only / never-renamed after the chassis ships. Document in `.claude/docs/technical-preferences.md` under "Forbidden Patterns" as a code-review rule. Enforce in CI by snapshotting `FrameLayoutSO.slots[*].slotId` per `LayoutId` in `docs/architecture/slot-id-baseline.yaml` and failing the build on any SlotId change in a shipped layout (similar to ADR-0004 `Project Identity` CI baseline). Post-EA layout edits that genuinely add slots are fine; renames require an ADR amendment + migration entry. |
| R-NOSTRUCT | Designer authors a `FrameLayoutSO` with zero structural slots — vehicle is born dead at `Hp == 0` | LOW | HIGH (chassis unplayable; ship-block bug) | `FrameLayoutSO.OnValidate` rejects zero-structural-slot layouts with a clear console error. Editor smoke test enumerates all `FrameLayoutSO` assets and asserts `Slots.Any(s => s.IsStructural)` for each. |
| R-ARMORLOOP | Armor.RedirectsTo references another Armor slot — infinite damage routing loop | LOW | HIGH (crash on first hit) | `FrameLayoutSO.OnValidate` rejects Armor whose `RedirectsTo` slot has `Kind == Armor`. Belt-and-suspenders: `ApplyDamage` Armor branch asserts the redirected slot is non-Armor; throws `InvalidLayoutException` if violated at runtime (developer-build only, not shipped). |
| R-MOUNT | Starter weapon SO authored with `MountDirection != Any` and a chassis whose corresponding slot's `Position` is incompatible — install fails on chassis-start, vehicle starts with empty weapon slot | MEDIUM | MEDIUM (silent gameplay bug — player has no weapon, doesn't realize why) | `ChassisDefinitionSO.OnValidate` simulates starter-part installation against the frame layout (each `StarterParts[i]` against the i-th compatible slot) and asserts no `MountIncompatibleException`. CI integration test loads each `ChassisDefinitionSO` and constructs a `Vehicle`, asserts all starter slots are non-Empty. |
| R-CARDDRIFT | After this ADR ships, designer renames a `SlotKind` enum value (e.g., `Hull` → `Chassis`) — all card SO targeting fields silently retarget | LOW | HIGH | Same convention as R-DRIFT: `SlotKind` enum is append-only / never-renamed post-EA. Documented in tech-prefs. Compile-time rename safety partially protects (designer editing the source typically uses Roslyn rename which updates references), but the SO asset `m_enumValue` integer survives any rename. Add an integer-stability baseline test for `SlotKind` enum order (same pattern as ADR-0004 baseline). |
| R-AI-DET | Enemy AI `WeakestInstance` tie-breaker non-deterministic if SlotIds tie (impossible by `OnValidate` uniqueness check, but defense in depth) | LOW | LOW | OnValidate uniqueness is the single-source guarantee. Documented as the invariant the tie-break relies on. |
| R-SE | Status Effects GDD/ADR currently assumes `SlotType?` targeting; renaming to `string` SlotId targeting may require Status effect re-authoring | MEDIUM | MEDIUM (Status ADR is downstream; this ADR pre-imposes a contract on it) | Status effects that target a kind (not a specific slot) use the same `ImplicitTargetSpec` default (`WeakestInstance` of that kind) — symmetric with AI implicit targeting (Decision 8). Status ADR (future) inherits this default, which preserves existing semantics for EA status authoring without re-authoring. |
| R-HUD | HUD combat overlay (Armor bar, slot anchors, vehicle silhouette renderer) is built against the 4-slot + vehicle-Armor model and must be refactored | HIGH | MEDIUM (HUD work, no gameplay break) | Surfaced to HUD spec / `combat_hud_backlog` (per memory). Work is mechanical: slot renderer iterates `IVehicleView.Slots` and reads `HudAnchor`; armor "bar" becomes per-Armor-slot sprite (Dredge only at EA). Not blocking ADR Acceptance; tracked as a HUD epic. |
| R-PERF | `GetSlotsByKind(kind)` allocates a fresh list per call in the hot damage path | LOW | LOW | Cached per-kind list in `Vehicle._byKind`, returned as `IReadOnlyList<SlotInstance>`. Cache is invalidated only on `InstallPart`/`RemovePart` (rare). Zero per-call allocation. |
| R-IL2CPP | `FrameLayoutSO` stripped by IL2CPP managed-stripping=High because runtime references it only through `IFrameLayout` | MEDIUM | HIGH (chassis fails to load on standalone build) | Add `link.xml` entry for `WastelandRun.Gameplay.Vehicle.FrameLayoutSO` (extends ADR-0005's namespace preservation pattern). Standalone IL2CPP smoke test loads one frame, constructs Vehicle, asserts `Slots.Count > 0`. |

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|---|---|---|
| vehicle-and-part-system.md | R1 — Vehicle POCO Composition (4-slot Dictionary, vehicle-level MaxArmor) | **Supersedes**: V&P GDD revision will rewrite R1 to "variable slot list per `FrameLayoutSO`; no vehicle-level Armor — Armor is a slot kind." This ADR provides the data contract the revised R1 will implement. |
| vehicle-and-part-system.md | R2 — Slot Taxonomy (4 fixed slots, Frame = death-critical) | **Supersedes**: V&P R2 revision will become "SlotKind taxonomy (5 kinds incl. Armor); structural pool defines death." This ADR locks the taxonomy. |
| vehicle-and-part-system.md | R3 — SlotState Runtime Model (derived DamageState) | **Preserved**: `SlotInstance` carries Hp/MaxHp/PlatingStacks; DamageState derivation unchanged. |
| vehicle-and-part-system.md | R4 — PartDefinitionSO Contract (StatModifiers, ArmorContribution) | **Amended**: `ArmorContribution` field becomes legacy (only relevant when computing per-Armor-slot MaxHp during install, if at all — Armor slot MaxHp may be authored directly on layout or derived from installed-part contribution; deferred to V&P revision). `MountDirection` added. |
| vehicle-and-part-system.md | R5 — Install Rules (RecalculateMaxArmor step) | **Amended**: RecalculateMaxArmor step removed (no vehicle-level Armor). MountCompatibility check added before slot mutation. |
| vehicle-and-part-system.md | R6 — Scrap Rules | **Preserved**: cards removed from all zones on scrap, unchanged. |
| vehicle-and-part-system.md | R9 — IVehicleMutator Surface | **Amended**: SlotId-keyed APIs (Decision 4); MountIncompatibleException added. |
| vehicle-and-part-system.md | F-VP2 — Damage Pipeline (plating → vehicle Armor → Hp for Frame) | **Replaced**: per-slot pipeline (Decision 11). Vehicle-level Armor step deleted; Armor slots use Decision 6 routing. |
| biome-1-enemy-roster.md | Dune Skimmer / Iron Shepherd / Dredge layouts | **Enables**: layouts locked in Decision 9; roster doc revision can now author enemy SOs against these `LayoutId`s. |
| card-system.md (via ADR-0006) | `PositionRequirement` enum semantics | **Amends**: rename to `EnemyAhead`/`EnemyBehind`; derivation from `MountDirection` (Decision 5 + 7). |
| save-persistence.md (via ADR-0004) | VehicleStateDTO schema | **Amends**: `SchemaVersion = 2`; V1→V2 migration (Decision 10). |

## Performance Implications

- **CPU per damage event**: `GetSlot(slotId)` is `Dictionary<string, SlotInstance>` O(1) hash lookup — equivalent to ADR-0005's `Dictionary<SlotType, SlotState>` baseline. `GetSlotsByKind` returns cached list (zero per-call cost). Armor exposure branch adds one extra `GetSlot` lookup + one recursive `ApplyDamage` — negligible.
- **CPU per turn**: Slot-list iteration for `StructuralHp` is O(N) with N ≤ 10 (Dredge) — under 100 ns per call on a typical CPU.
- **Memory per vehicle**: `SlotInstance` (~80 bytes) × N slots (4–10) + two dictionaries (~200 bytes overhead) + cached per-kind lists (~80 bytes × 5 kinds) — total ~1.5 KB per vehicle worst case. Two vehicles in combat = 3 KB. Negligible vs. 2 GB ceiling.
- **Allocations**: Zero per damage event. Allocation only on `InstallPart` (cache rebuild — single dictionary entry + list rebuild for the affected kind). Acceptable: install is post-combat, not in-frame.
- **Draw calls**: Variable slot count *raises* the cap from 10 (ADR-0001's 5-slot × 2 vehicle estimate) to 24 worst case (Dredge 10 slots + player 5 slots + 9 armor-overlay possible variants). All MPB-driven and outside SRP batching per ADR-0001. Still well under the 200 draw call budget at 12% utilization. HUD spec must validate against this in the Frame Debugger after first Dredge encounter is rendered.
- **Save size**: V2 envelope grows by `LayoutId` string (~25 bytes) + per-slot SlotId string (~12 bytes × N). For a Dredge encounter snapshot mid-combat (if ever serialized — currently mid-combat is in-memory only per V&P R1) the increment is ~150 bytes. RunState envelope grows ~50 bytes per save. Within ADR-0004's 7–14 KB total budget.
- **Load time**: One additional `FrameLayoutSO` read per chassis at run-start (referenced by `ChassisDefinitionSO`) — no Addressables roundtrip if frames are direct SO references, ~0 cost. With Addressables (if adopted) ~5 ms total cold load for all 6 frame assets behind the loading screen.

## Migration Plan

1. **Lock the data contract** (this ADR Accepted). User approval is the gate.
2. **Rename `SlotType → SlotKind` and `Frame → Hull`** across `WastelandRun.Vehicle`, `WastelandRun.Cards`, `WastelandRun.Combat`, `WastelandRun.Gameplay`. Mechanical Roslyn rename. *Verify*: compile passes; 136 Combat tests pass after rename only.
3. **Add `SlotPosition` enum, `SlotDefinition` struct, `IFrameLayout` interface** to `WastelandRun.Vehicle`. Compile passes; no runtime change yet.
4. **Add `FrameLayoutSO` to `WastelandRun.Gameplay.Vehicle`** with `OnValidate` and `[CreateAssetMenu]`. Add `link.xml` preservation entry. Author the 6 locked frame assets (Decision 9) at `Assets/Data/Vehicle/Frames/{Player,Enemy}/`. *Verify*: editor smoke test loads each, `OnValidate` passes for all, structural-pool non-empty assertion holds for each.
5. **Refactor `Vehicle` from `Dictionary<SlotType, SlotState>` to `IReadOnlyList<SlotInstance>` + lookup dictionaries**. Replace `IVehicleView`/`IVehicleMutator` signatures (Decision 4 + Key Interfaces). Add `SlotNotFoundException`, `MountIncompatibleException`. Remove `MaxArmor`/`CurrentArmor`/`AddArmor`/`OnMaxArmorChanged`/`OnCurrentArmorChanged`. Implement `StructuralHp`/`IsDead`. Implement Decision 11 damage pipeline (Armor branch). *Verify*: existing 136 Combat tests fail in known ways (Slots[Frame] patterns); rewrite each to use SlotId addressing. New tests cover: (a) Armor exposure branch (`ExposureMultiplier × damage` on Hull after Armor.Hp depleted), (b) MountIncompatible throws, (c) StructuralHp == 0 marks IsDead for layouts with 1 Hull, 2 Hull, and Hull-less-but-structural-Engine (defensive), (d) GetSlotsByKind returns cached list (same reference across calls when no mutation).
6. **Bump `VehicleStateDTO.SchemaVersion` to 2**. Add `LayoutId` and re-key `Slots` by SlotId. Implement V1→V2 reader in `VehicleStateSaveAdapter.FromDto`. *Verify*: V1 fixture round-trip test (Decision 10 test gate); test covers four V1 edge cases (full save, one slot Empty, Frame at Degraded threshold, statuses on each slot).
7. **Run ADR-0006 amendment script**: (a) `RequiresAhead` → `EnemyAhead`, `RequiresBehind` → `EnemyBehind` rename across all `CardDefinitionSO`/`PositionConditionSO` assets via editor script that reads each SO's serialized `m_PositionRequirement` int and writes the new int (or no-op if `BonusIf*`/`None`). (b) `RestorePlatingEffectSO.TargetSlot: SlotType` → `TargetSlotKind: SlotKind` + add `ImplicitTargetSpec` field defaulted to `WeakestInstance` (preserves existing card semantics where most cards address "the Weapon" and now address "the weakest Weapon instance"). *Verify*: every card SO loads with no `OnValidate` errors; combat resolution against a re-rolled deck reproduces pre-rename behavior on the existing 136 tests.
8. **Update `PartDefinitionSO`** with `MountDirection: SlotPosition` field. Default value `Any`. All existing starter weapon SOs receive `MountDirection = Any` (preserves EA behavior — no positional gameplay at EA). *Verify*: ChassisDefinitionSO.OnValidate simulates starter installation against each player frame; asserts no `MountIncompatibleException`.
9. **Update `ChassisDefinitionSO`** to reference a `FrameLayoutSO`. Author Scout → `small_frame`, Assault → `medium_frame`. Heavy Truck reserved (no SO at EA). *Verify*: chassis-select screen lists Scout and Assault; constructs `Vehicle` for each with full starter parts in correct slots.
10. **Update `tr-registry.yaml`**: mark `TR-vehicle-NNN` requirements bound to the 4-slot contract as superseded by ADR-0007 (`/architecture-review` Phase 8 will do this; this step records the link). Add new `TR-vehicle-NNN` entries for `FrameLayoutSO.OnValidate` rules, Armor exposure routing, MountDirection install gate.
11. **Update `.claude/docs/technical-preferences.md`** Forbidden Patterns to add: (a) "SlotId rename in shipped `FrameLayoutSO`" (per R-DRIFT mitigation), (b) "SlotKind enum reorder" (per R-CARDDRIFT mitigation). Add CI baseline files (`docs/architecture/slot-id-baseline.yaml`, `docs/architecture/slot-kind-baseline.yaml`) per ADR-0004's `Project Identity` precedent.
12. **Cascade to V&P GDD** (parallel systems-designer task) — rewrite R1, R2, R5, R9, F-VP2 against this ADR. Re-validate via `/design-review`.
13. **Cascade to biome-1 enemy roster** — author Tiny / Hauler / Dredge enemy SOs referencing `tiny_frame` / `hauler_frame` / `dredge_frame` layouts.
14. **Cascade to HUD spec** — refactor slot renderer to iterate `IVehicleView.Slots` and consume `HudAnchor`; redesign Armor visualization around per-Armor-slot sprites (Dredge first user). Schedule against the combat HUD backlog per memory.

## Validation Criteria

This ADR is considered successfully implemented when:

- [ ] `FrameLayoutSO` exists with `OnValidate` rejecting all 5 invalid cases (Decision 2).
- [ ] All 6 locked layouts authored and validated; `OnValidate` passes for each.
- [ ] `Vehicle` POCO refactored to variable slot list; `_bySlotId` / `_byKind` lookups O(1) / cached.
- [ ] `StructuralHp == 0 ⇔ IsDead` holds across unit tests covering 1-Hull, 2-Hull, and Hull-less-but-structural-Engine layouts.
- [ ] Armor exposure damage routing produces `floor(damage × ExposureMultiplier)` to RedirectsTo slot when Armor.Hp == 0 BEFORE the hit; absorbs 1:1 when Armor.Hp > 0; **a hit that breaks Armor with leftover overflow routes `floor(overflow × ExposureMultiplier)` to RedirectsTo on the SAME hit** (breakthrough policy — revised 2026-05-18); a hit that depletes Armor to exactly 0 with zero overflow does not route. Integer-overflow clamp `Math.Min(..., int.MaxValue)` applies before the cast.
- [ ] `MountIncompatibleException` thrown on install attempt of `Front` part into `Back` slot (and inverse); no exception on `Any`/`Any`, `Any`/`Front`, `Front`/`Front`.
- [ ] Cards granted by `MountDirection=Front` weapon carry `OverridePositionRequirement = EnemyAhead`; `Back` → `EnemyBehind`; `Any` → card template's authored requirement.
- [ ] V1 save fixture loads under V2 reader; re-saved V2 → re-loaded V2 produces byte-identical V2 across two consecutive load/save cycles.
- [ ] Existing 136 Combat EditMode tests pass after Migration step 5 + 7 (mechanical rewrite to SlotId addressing; no behavioral change for EA's `MountDirection = Any` starter weapons).
- [ ] New EditMode tests cover: (a) Armor exposure branch, (b) MountIncompatible install gate, (c) PositionRequirement derivation, (d) WeakestInstance tie-break by lex SlotId, (e) FrameLayoutSO.OnValidate failure cases.
- [ ] `WastelandRun.Vehicle.asmdef` compiles with zero `UnityEngine.*` imports (CI grep extends ADR-0005 audit).
- [ ] `link.xml` preserves `WastelandRun.Gameplay.Vehicle.FrameLayoutSO`; standalone IL2CPP smoke test loads one frame and constructs Vehicle successfully.
- [ ] `slot-id-baseline.yaml` and `slot-kind-baseline.yaml` CI checks active and passing.
- [ ] Card SO migration script run; every CardDefinitionSO loads with no OnValidate errors after the `EnemyAhead`/`EnemyBehind` rename.
- [ ] `ChassisDefinitionSO.OnValidate` simulates starter installation for Scout and Assault; both succeed without `MountIncompatibleException`.
- [ ] **Decision 12** — `Repair(slotId, hpRestored, canReviveOffline)` fires `OnSlotHpChanged` on every Hp delta (positive and negative) and `OnSlotDamageStateChanged` on any upward boundary cross (Offline → Functional/Degraded, Degraded → Functional). EditMode test covers: (a) Repair below threshold fires HpChanged only; (b) Repair across DegradedThresholdPct fires HpChanged then DamageStateChanged; (c) Repair of Offline slot with `canReviveOffline=true` fires HpChanged then DamageStateChanged but never reverses `IsDead`.
- [ ] **Decision 13** — `tools/ci/check-adr0007-vp-lockstep.sh` exists, runs in CI, and fails the build when ADR-0007 Decision 11 "Locked invariants" diverges from V&P F-VP2 "Ordering contract (locked)". Manual test: edit one invariant in either doc without the matching edit; CI fails. Revert; CI passes.
- [ ] **Decision 14** — Armor slot taking damage with `slot.Hp > 0` after absorption emits Steps 3/4 on the armor slot itself. EditMode test covers: (a) absorption that stays Functional fires HpChanged only; (b) absorption that crosses DegradedThresholdPct fires HpChanged then DamageStateChanged with no `OnArmorExposed`; (c) Armor non-structural-by-default verified — no `OnCriticalStateChanged` from an armor-only Degraded transition.
- [ ] **Decision 15** — `ApplyDamage` recursion captures fresh `wasCritical` per invocation; `OnCriticalStateChanged` fires at most once per top-level entrypoint. EditMode tests cover: (a) Armor breakthrough → Hull recursion crosses critical → exactly one `OnCriticalStateChanged(true)` firing (worked example from Decision 15); (b) non-breaking Armor INTACT absorption crossing Degraded → zero `OnCriticalStateChanged` firings (armor non-structural); (c) direct Hull hit crossing critical (no recursion) → exactly one `OnCriticalStateChanged(true)` firing (regression coverage); (d) double-recursion case (armor → hull where hull recursion itself routes through another armor — pathological but representable) → still at most one critical-state event.
- [ ] **Key Interfaces additions** — `IVehicleView.ActiveStatuses` and `GetStatModifier(StatType)` present and consumed by Card Combat damage path; `OnPlatingChanged` fires from `AddPlating`, Decision 6 plating-consumption step, and any status-driven plating strip; `OnStatusStackChanged` fires when Corroded ticks down or merges; `ICardCatalog` and `IGrantedCardData` declared in `WastelandRun.Vehicle` (engine-free) and consumed by `OnGrantedCardRemoved` subscribers without crossing the assembly boundary.
- [ ] **Decision 16 — soft disable on Offline** — A card with `SourceSlotId="X"` in any zone (deck/hand/discard) is flagged `IsPlayable=false` immediately after `vehicle.GetSlot("X").DamageState → Offline`, with zero deck/hand/discard zone moves and zero `OnGrantedCardRemoved` firings. EditMode test verifies pre- and post-transition card counts in each zone are identical.
- [ ] **Decision 16 — repair restoration** — After the slot-Offline test above, calling `Repair("X", n, canReviveOffline=true)` until `DamageState ∈ { Functional, Degraded }` flips the same card's `IsPlayable` to true with zero zone moves and zero additional events beyond Decision 12's `OnSlotHpChanged` + `OnSlotDamageStateChanged`.
- [ ] **Decision 16 — gate composition** — A test card with `SourceSlotId="weapon_left"`, `OverridePositionRequirement=EnemyAhead`, `EnergyCost=2` returns `IsPlayable=true` iff `weapon_left.DamageState ∈ {Functional, Degraded}` AND `combatState.PlayerPosition=Ahead` AND `player.Energy ≥ 2`. Truth table flips each gate independently and verifies the AND composition.
- [ ] **Decision 16 — hard removal sweep** — `vehicle.HardRemoveCards("slot_a", ["card_x", "card_y"])` with `card_x` in hand and `card_y` split across deck (one copy) + discard (one copy) results in: zero `card_x`/`card_y` instances anywhere post-call; one `OnGrantedCardRemoved("slot_a", ["card_x", "card_y"])` firing; non-matching cards in all three zones untouched.
- [ ] **Decision 16 — null SourceSlotId external-source path** — `vehicle.HardRemoveCards(null, ["tether_a", "tether_b"])` fires `OnGrantedCardRemoved(null, ["tether_a", "tether_b"])` exactly once; subscribers receive the null first argument without NRE. Regression coverage for nullable-`SourceSlotId` contract on both the mutator surface and the event.
- [ ] **Decision 16 — `CardData.SourceSlotId` projection at part install time** — When `InstallPart("weapon_left", partWithGrantedCard)` runs, the resulting runtime `CardData` instance carries `SourceSlotId = "weapon_left"`. When the same card is granted via a non-slot pathway (Javelin), the runtime `CardData` carries `SourceSlotId = null`. Verified via Card Combat construction test.

## Related Decisions

- **ADR-0001** (Visual Vehicle Part System) — **SUPERSEDED** by this ADR with respect to the 4-slot data contract and vehicle-level Armor; visual layer decisions (URP shader, MPB pattern, Addressables groups, sprite budget) remain in force.
- **ADR-0002** (Card Combat POCO state) — preserved; `Vehicle` continues as POCO in `noEngineReferences` assembly.
- **ADR-0003** (Deterministic RNG) — extended: `RandomInstance` AI targeting consumes caller-owned `System.Random`; tie-breaks use deterministic lex order, no RNG draws spent.
- **ADR-0004** (Save & Persistence) — extended: `VehicleStateDTO.SchemaVersion` ticks to 2; V1→V2 one-shot migration in adapter; SystemId unchanged.
- **ADR-0005** (Vehicle POCO + Part Catalog) — heavily amended: `Slots`, `IVehicleView`, `IVehicleMutator`, `IPartData` interfaces all change shape; `MaxArmor`/`CurrentArmor` removed; `MountDirection` added to `IPartData`. Assembly split discipline and `IPartCatalog` pattern preserved.
- **ADR-0006** (Card System) — amended: `PositionRequirement` enum rename; runtime card carries `OverridePositionRequirement` derived from installed part's `MountDirection`; card effect `SlotType` targeting fields become `SlotKind` + `ImplicitTargetSpec`.
- **`design/gdd/vehicle-and-part-system.md`** — primary GDD; R1, R2, R5, R9, F-VP2 revision blocked on this ADR.
- **`design/gdd/biome-1-enemy-roster.md`** — Dredge bespoke layout depends on this ADR; revision blocked on V&P GDD revision.
- **Future Enemy AI ADR** — will adopt `ImplicitTargetSpec` contract (Decision 8) and `BrainRulesetSO` per-intent override surface.
- **Future Status Effects ADR** — must adopt SlotId-keyed targeting (Decision 4) and `ImplicitTargetSpec` for kind-targeting statuses.
- **Future HUD spec / combat HUD backlog work** — must consume `IVehicleView.Slots` + `HudAnchor`; redesign Armor visualization per Armor slot.

---
status: Approved
gates: ADR-0007 Accepted
sister-doc: design/gdd/vehicle-and-part-mechanics.md
supersedes: design/gdd/vehicle-and-part-system.md (historical artifact — do not edit)
last-updated: 2026-05-19 (R4 revision pass)
---

# Vehicle & Part System — Architecture

> **Scope:** Contract surface for the frame-driven variable slot system —
> slot model, anchor model, chassis schema, formulas with declared boundary
> domains, frame-ordering convention, validation gates, and data-authoring
> rules. **ADR-0007 Accepted gates on this document only.**
>
> **Out of scope:** Damage states, soft-disable lifecycle, HUD UX,
> accessibility hooks, audio cues, economy hooks, granted-card lifecycle.
> See sister doc: `vehicle-and-part-mechanics.md`.

## 1. Overview

This document defines the **contract surface** of the frame-driven variable
slot system (ADR-0007): the canonical `SlotDefinition`, the `AnchorPoint`
POCO, the `ChassisDefinition` schema, the three system formulas (F-VP1
degraded threshold, F-VP2 installed-count clamp, F-VP3 `SafeAmplify`) with
declared input domains and boundary behavior, the same-frame ordering
convention that governs all multi-event collisions, the validation gates
that reject malformed data at load, and the data-authoring rules for
designers building chassis. A chassis declares a fixed array of slots, each
slot declares an `AnchorPoint` and an `IsPlayable` field, and parts install
into slots whose contracts they satisfy. **ADR-0007 Accepted gates on this
document only** — the experiential surface (damage states, soft-disable
lifecycle, HUD, accessibility, audio, economy, granted-card lifecycle)
lives in the sister mechanics doc and iterates independently. The contract
here is deterministic and engine-version-compatible (Unity 6.3 LTS,
`[SerializeField]` fields-only struct shape), and every formula declares
its input domain so that no invariant is "assumed" — it is either declared
or rejected.

## 2. Player Fantasy

The *players* of this document are programmers, designers, and QA — the
"fantasy" here is the **architectural promise** they should feel when they
work against this contract. For programmers: every invariant is declared,
every formula has a stated domain, and same-frame collisions resolve by a
rule that lives in one place. There are no implicit ordering assumptions
to reverse-engineer from runtime behavior. For designers: a new chassis
can be authored end-to-end in data, with the validator rejecting malformed
shapes at load — not at frame 30 of a combat encounter. For QA: every
acceptance criterion in §9 is testable from the published contract alone,
with no dependence on the experiential surface in the sister doc.
The experiential fantasy — the *feel* of a vehicle taking damage, of a
plate shattering, of a slot going dark — is owned by
`vehicle-and-part-mechanics.md` and is intentionally out of scope here.

## 3. Detailed Rules
*Single canonical `SlotDefinition`. `AnchorPoint` POCO. Chassis schema.
`IsPlayable` field declared on slot model. Validation gates.*

### 3.1 Slot Model — `SlotDefinition` (canonical)

**`SlotDefinition` is declared exactly once in this document.** This
declaration is the canonical contract. ADR-0007 §Key Interfaces references
this section by anchor; no other document restates the shape. CI lock-step
grep (`tools/ci/check-adr0007-vp-lockstep.sh`) rejects any verbatim
restatement elsewhere.

**Shape (POCO + Authoring DTO split — Phase A1 decision, locked
2026-05-19).** `SlotDefinition` is a pure POCO in the engine-free
`WastelandRun.Vehicle` assembly (`noEngineReferences: true` per ADR-0005
/ ADR-0007). It carries **no `[SerializeField]` and no Unity attributes**
so the slot/part contract can be unit-tested without spinning up the
engine. A separate Unity-side DTO, `SlotDefinitionAuthoring`, holds the
`[SerializeField]` fields the designer authors against, and projects to
the POCO at asset load via `ToPoco()`. Per Unity 6.3 LTS rules,
`[SerializeField]` is fields-only and does not apply to properties — the
DTO honors that rule; the POCO does not need to.

**Runtime POCO** (engine-free; `WastelandRun.Vehicle` assembly):

```csharp
// WastelandRun.Vehicle — noEngineReferences: true. Pure POCO.
public struct SlotDefinition
{
    public string SlotId;                  // unique within layout
    public SlotKind Kind;                  // see ADR-0007 enums
    public SlotPosition Position;          // Any | Front | Back
    public bool HasMaxHpOverride;          // discriminator for MaxHpOverride
    public int  MaxHpOverride;             // valid only when HasMaxHpOverride == true
    public bool HasStructuralOverride;     // discriminator for IsStructuralOverride
    public bool IsStructuralOverride;      // valid only when HasStructuralOverride == true
    public AnchorPoint HudAnchor;          // §3.2
    public float ExposureMultiplier;       // Armor only; default 1.0
    public string RedirectsToSlotId;       // Armor only; null otherwise
    public bool IsPlayable;                // §3.4
}
```

**Authoring DTO** (Unity-side; `WastelandRun.Vehicle.Unity` assembly):

```csharp
// WastelandRun.Vehicle.Unity — engine-bearing authoring shape.
[System.Serializable]
public struct SlotDefinitionAuthoring
{
    [SerializeField] public string SlotId;
    [SerializeField] public SlotKind Kind;
    [SerializeField] public SlotPosition Position;
    [SerializeField] public bool HasMaxHpOverride;
    [SerializeField] public int  MaxHpOverride;
    [SerializeField] public bool HasStructuralOverride;
    [SerializeField] public bool IsStructuralOverride;
    [SerializeField] public AnchorPoint HudAnchor;
    [SerializeField] public float ExposureMultiplier;
    [SerializeField] public string RedirectsToSlotId;
    [SerializeField] public bool IsPlayable;

    public SlotDefinition ToPoco() => new SlotDefinition {
        SlotId                 = SlotId,
        Kind                   = Kind,
        Position               = Position,
        HasMaxHpOverride       = HasMaxHpOverride,
        MaxHpOverride          = MaxHpOverride,
        HasStructuralOverride  = HasStructuralOverride,
        IsStructuralOverride   = IsStructuralOverride,
        HudAnchor              = HudAnchor,
        ExposureMultiplier     = ExposureMultiplier,
        RedirectsToSlotId      = RedirectsToSlotId,
        IsPlayable             = IsPlayable,
    };
}
```

**Why the discriminated `Has*` pattern, not `int?` / `bool?`:** Unity
does not serialize `Nullable<T>`. The discriminator pair (`HasX: bool`
+ `X: T`) is the idiomatic Unity 6.3 substitute and round-trips through
the YAML serializer without a custom property drawer. The `Has*` field
is the source of truth: when `false`, the paired override field is
**unread** by the construction pipeline (§3.3) and the chassis/Kind
default applies.

**Why the POCO/Authoring split, not a single `[SerializeField]`-bearing
struct:** keeping `SlotDefinition` engine-free preserves ADR-0005's
contract that `WastelandRun.Vehicle` is unit-testable without Unity. The
Authoring DTO absorbs every engine attribute; nothing inside the POCO
references `UnityEngine`. The `ToPoco()` projection is the single
boundary point, called once at asset load (§3.3), and is itself
unit-testable from the Unity-side assembly.

**Field semantics (POCO):**

| Field | Domain | Meaning |
|---|---|---|
| `SlotId` | non-empty string, unique in layout | Stable id for events, save, lookup |
| `Kind` | `SlotKind` enum | Determines compatible parts and default `IsStructural` |
| `Position` | `Any` \| `Front` \| `Back` | Weapon mount gating (informational on non-Weapon slots) |
| `HasMaxHpOverride` | `bool` | When `true`, `MaxHpOverride` carries the per-slot HP value; when `false`, chassis default for `Kind` applies |
| `MaxHpOverride` | `int ≥ 1` when `HasMaxHpOverride == true`; ignored otherwise | Per-slot HP override |
| `HasStructuralOverride` | `bool` | When `true`, `IsStructuralOverride` carries the structural flag; when `false`, the Kind default applies |
| `IsStructuralOverride` | `bool` when `HasStructuralOverride == true`; ignored otherwise | Override the Kind-default structural flag |
| `HudAnchor` | `AnchorPoint`, both coords finite ∈ [0,1] | UV in chassis-local unit rect; see §3.2 |
| `ExposureMultiplier` | finite `> 0` (warning if `< 1.0` or `> 5.0`) | Damage amplification when Armor is destroyed; ignored on non-Armor |
| `RedirectsToSlotId` | valid `SlotId` in same layout, not self, non-Armor target | Where amplified damage flows when Armor breaks; `null` on non-Armor |
| `IsPlayable` | `bool` | Slot can host an `InstalledPart` and contribute to combat. See §3.4 for full semantics |

**Mutability rule:** the POCO is mutable by C# rules (public fields),
but designer-authored data is **read-only at runtime** — the Authoring
DTO publishes the value at load via `ToPoco()`, and the runtime
construction in §3.3 copies field-by-field into `SlotInstance`. The
POCO's mutability exists for serialization round-trips (save/load,
ADR-0004) and for the projection assignment, not to support runtime
mutation of the definition.

**SlotInstance (runtime state) is declared in ADR-0007 §Key Interfaces** —
the architecture-doc/ADR boundary is: this doc owns the **definition** of
a slot (authoring-time, immutable-by-convention), ADR-0007 owns the
**instance** of a slot (runtime, mutable). The two are linked by the
construction rule in §3.3.

**Validator rules** are listed in §3.5 (single home for all gates).
**Boundary domains for the formulas that consume these fields** are
declared in §5 (single home for all formula contracts).

### 3.2 Anchor Model — `AnchorPoint`

`AnchorPoint` is the engine-free 2D coordinate used by `SlotDefinition.HudAnchor`
(and any future spatial reference on the slot model). It exists because
`SlotDefinition` lives in the `noEngineReferences: true` `WastelandRun.Vehicle`
assembly and cannot reference `UnityEngine.Vector2`. **Declared exactly once
in this document**; ADR-0007 §Key Interfaces references this section by anchor.

```csharp
// WastelandRun.Vehicle — noEngineReferences: true. Pure POCO.
// [System.Serializable] is in System, not UnityEngine, so it is
// safe inside an engine-free assembly. [SerializeField] would
// pull UnityEngine and break noEngineReferences — it is
// intentionally absent. Unity serializes public fields of
// [System.Serializable] structs automatically when the struct
// is embedded in a Unity-side DTO (e.g. SlotDefinitionAuthoring.HudAnchor),
// so no Unity attribute is needed here.
[System.Serializable]
public struct AnchorPoint
{
    public float X;
    public float Y;

    public bool IsInUnitRect()
        => float.IsFinite(X) && float.IsFinite(Y)
        && X >= 0f && X <= 1f
        && Y >= 0f && Y <= 1f;
}
```

**Coordinate space:** chassis-local unit rect. `X` and `Y` are normalized
to `[0, 1]`. `(0.0, 0.0)` is the bottom-left of the chassis sprite bounds;
`(1.0, 1.0)` is the top-right; `(0.5, 0.5)` is the center. The view layer
(Combat HUD, `IVehicleView` consumers) converts to `UnityEngine.Vector2`
in screen or local space by multiplying against the chassis widget's
`RectTransform.rect.size`.

**Validation idiom — `float.IsFinite` is the contract.** Any code that
ingests an `AnchorPoint` value (loader, validator, runtime construction)
MUST check finiteness with `float.IsFinite(X) && float.IsFinite(Y)`. The
older NaN-only check (`!float.IsNaN`) is insufficient — it permits `±∞`,
which propagates through subsequent math without an obvious failure point.
`IsInUnitRect()` is the single helper that bundles finiteness + range; all
validators in §3.5 use this helper rather than re-implementing the checks.

**Why a POCO struct, not `Vector2`:** `Vector2` is in `UnityEngine` and
would force `WastelandRun.Vehicle.asmdef` to drop `noEngineReferences:
true`, which is the assembly boundary that lets the slot/part contract be
unit-tested without spinning up the engine. `AnchorPoint` is intentionally
the minimum surface needed (two floats + one helper) — it is not a
general-purpose vector type and does NOT define arithmetic, normalization,
or dot product. View-layer code that needs `Vector2` semantics constructs
`new UnityEngine.Vector2(anchor.X, anchor.Y)` at the boundary.

### 3.3 Chassis Schema — `ChassisDefinition`

The chassis schema is split across two authoring assets:

- **`ChassisDefinitionSO`** — chassis identity, defaults, and
  player-vehicle-specific fields.
- **`FrameLayoutSO`** — the slot layout (a `List<SlotDefinition>`)
  referenced by chassis via `FrameLayoutId`.

This split separates **what a vehicle is** (chassis) from **how it's
shaped** (layout). Two chassis can share a layout (e.g., a future
"Scout Mk II" reusing `small_frame`); enemy vehicles use layouts without
a chassis at all (no `ChassisType` gating).

**`ChassisDefinitionSO` (authoring shape):**

```csharp
[CreateAssetMenu]
public sealed class ChassisDefinitionSO : ScriptableObject
{
    // [field: SerializeField] is the Unity 6.3 LTS pattern for serialized public read-only properties
    // (technical-preferences.md §Naming Conventions). SlotDefinitionAuthoring is the intentional
    // exception: it is a DTO struct in the Unity-side assembly where [SerializeField] on fields is
    // required by the POCO/DTO split design (§3.1).
    [field: SerializeField] public ChassisType ChassisType      { get; private set; }  // Scout | Assault | HeavyTruck
    [field: SerializeField] public string DisplayName           { get; private set; }
    [field: SerializeField] public string FrameLayoutId         { get; private set; }  // → FrameLayoutSO.LayoutId
    [field: SerializeField] public DefaultSlotMaxHpEntry[] DefaultSlotMaxHp { get; private set; }  // (SlotKind, int) entries
    [field: SerializeField] public StarterPartEntry[] StarterParts          { get; private set; }  // SlotId → PartDefinitionSO
    [field: SerializeField] public CardDefinitionSO[] StarterDeck           { get; private set; }  // exactly 10 cards
    [field: SerializeField] public CardFamily[] PrimaryFamilies             { get; private set; }  // 1..2 entries
    [field: SerializeField] public int MaxEnergyBase    { get; private set; } = 3;
    [field: SerializeField] public int MaxHandSizeBase  { get; private set; } = 5;
    [field: SerializeField] public AssetReferenceT<ChassisArtBundle> SpriteBundle { get; private set; }  // ADR-0008 (Proposed) — Addressables
    [field: SerializeField] public SilhouetteClass Silhouette   { get; private set; }  // art only, no gameplay effect
}
```

`DefaultSlotMaxHp` is array-backed (Unity does not serialize generic
`Dictionary<,>` directly). Runtime loaders project the entries into an
`IReadOnlyDictionary<SlotKind, int>` keyed by `SlotKind`.

**`AssetReferenceT<ChassisArtBundle>` provenance.** `AssetReferenceT<T>`
is part of Unity Addressables, approved 2026-05-19
(`technical-preferences.md` — Allowed Libraries). Full architecture
rationale (loader contract, memory budget rules, failure modes) lives in
**ADR-0008 (Proposed) — Addressables runtime asset loading**, opened for
technical-director sign-off. ADR-0008 reaching Accepted is required
before any codepath that actually loads from the catalog ships; the
contract in this section (the field exists, holds a typed asset
reference, resolves to a `ChassisArtBundle` at load) is independent of
that sign-off and does not block this doc's approval.

**Load mode is async — `SpriteBundle` is NOT touched by §3.3 construction.**
`LoadAssetAsync` is the resolution path. `AsyncOperationHandle<T>.WaitForCompletion()`
exists in Unity 6.x but blocks the rendering thread for the full load
duration — functionally a synchronous load — and MUST NOT be called in
the construction critical path. This is a **frame-budget requirement**,
not an API absence. The §3.3 vehicle construction pipeline below MUST NOT
dereference `SpriteBundle` — it is resolved asynchronously by the view
layer (Combat HUD / chassis-art prefab spawning) outside the construction
critical path. Mechanics-doc and the HUD UX spec own the async sequencing;
this doc only declares that no construction step blocks on the bundle.

**`FrameLayoutSO` (authoring shape):**

```csharp
[CreateAssetMenu]
public sealed class FrameLayoutSO : ScriptableObject
{
    [field: SerializeField] public string LayoutId                      { get; private set; }  // unique across all layouts
    [field: SerializeField] public bool IsPlayerUnlockable              { get; private set; }
    [field: SerializeField] public int DegradedThresholdPct             { get; private set; } = 50;  // [1, 99] (F-VP1)
    [field: SerializeField] public int CriticalThresholdPct             { get; private set; } = 20;  // [1, DegradedThresholdPct-1] (§5.4)
    [field: SerializeField] public SlotDefinitionAuthoring[] Slots      { get; private set; }  // ordered; order stable at runtime

    // Runtime-only flag — public field (not property) because AssetPostprocessor (external
    // code in the Unity-side assembly) must write it directly; [System.NonSerialized] cannot
    // be applied to auto-property backing fields via [field:] syntax.
    // Initialises to false on every domain reload (including Play Mode entry with default
    // Domain Reload settings). Set to true by AssetPostprocessor only when all §3.5
    // Error-severity gates pass at import time. Read by the §3.3 construction guard (editor only).
    // SCOPE: defends against mid-session reimport of a previously-valid-but-now-invalidated
    // asset within the same editor session (no domain reload between import and construction).
    // Does NOT protect against fresh Play Mode entry or fresh editor launches — those paths rely
    // on the build-time IPreprocessBuildWithReport gate (§3.5).
    // In shipped builds (!Application.isEditor): this flag is NOT checked at construction time;
    // the IPreprocessBuildWithReport gate is the sole correctness guarantee for shipped chassis data.
    [System.NonSerialized] public bool IsValidated;
}
```

**Authoring→POCO projection.** `FrameLayoutSO.Slots` holds the
`SlotDefinitionAuthoring` DTO declared in §3.1 (the Unity-serialisable
shape). The runtime never reads the authoring shape directly — the
loader projects each entry through `ToPoco()` exactly once at asset load
and exposes a `IReadOnlyList<SlotDefinition>` (POCO) to the rest of the
engine-free `WastelandRun.Vehicle` assembly. After this projection
point, the authoring DTO is invisible to the construction pipeline below.

**Runtime construction (the chassis→instance pipeline):**

When a vehicle is built:

1. Resolve `chassis.FrameLayoutId` → `FrameLayoutSO`. Reject with
   `LayoutNotFoundException` if unresolved. **[Editor only] Reject with
   `LayoutNotValidatedThisSessionException` if `FrameLayoutSO.IsValidated == false`**
   — the postprocessor sets this flag at successful import and clears it
   on any Error-severity gate failure (§3.5). This guard defends against
   mid-session reimport of a previously-valid-but-now-invalidated asset
   within the same editor session. It does NOT protect against fresh Play
   Mode entry (domain reload resets all `[System.NonSerialized]` fields
   to `false`) or fresh editor launches — those paths rely on the
   build-time `IPreprocessBuildWithReport` gate (§3.5). **In shipped
   builds (`!Application.isEditor`), this guard is skipped entirely**
   (it is conditionally compiled or gated on `Application.isEditor` in
   the Unity-side assembly; the `IPreprocessBuildWithReport` gate is
   the correctness guarantee for shipped data). `LayoutNotValidatedThisSessionException`
   is distinct from `VehicleConstructionException` (data-corrupt mid-construction
   failures) — a session-flag miss is never misread as corrupt chassis
   data. **If thrown in the editor:** reimport the `FrameLayoutSO` asset
   from the Unity Project window (right-click → Reimport, or Assets >
   Reimport All); the postprocessor will run and set `IsValidated = true`
   if all §3.5 gates pass. Project `FrameLayoutSO.Slots`
   (`SlotDefinitionAuthoring[]`) into the POCO list
   `slotDefs: IReadOnlyList<SlotDefinition>` via `ToPoco()` per entry.
   The pipeline below reads only `slotDefs`.
2. Construct `Vehicle.Slots: List<SlotInstance>` by iterating
   `slotDefs` in order, one `SlotInstance` per `SlotDefinition`.
3. Copy `SlotId`, `Kind`, `Position`, `HudAnchor`, `ExposureMultiplier`,
   `RedirectsToSlotId`, `IsPlayable` from the `SlotDefinition`.
4. Resolve `IsStructural = slotDef.HasStructuralOverride ? slotDef.IsStructuralOverride : <Kind default>`.
5. Resolve `MaxHp = slotDef.HasMaxHpOverride ? slotDef.MaxHpOverride : chassis.DefaultSlotMaxHp[Kind]`.
   If `HasMaxHpOverride == false` **and** the chassis lacks a default
   for `Kind`, the validator (§3.5) has already rejected the asset at
   load — this branch is never reached at runtime.
6. `Hp = MaxHp`. `InstalledPart = null`. Other runtime fields initialize
   to their default values (see ADR-0007 §Key Interfaces for the full
   `SlotInstance` shape).

This pipeline is deterministic: given the same `ChassisDefinitionSO` and
`FrameLayoutSO`, it produces the same `Vehicle.Slots` shape every run.
Save/load relies on this determinism via ADR-0004.

**Boundary contract for the chassis/layout fields:**

| Field | Domain | Enforced By |
|---|---|---|
| `FrameLayoutId` | non-empty; must resolve to a `FrameLayoutSO` | §3.5 |
| `DefaultSlotMaxHp[Hull, Weapon, Engine, Mobility]` | each value `> 0` | §3.5 (non-negotiable) |
| `StarterDeck.Length` | `== 10` | §3.5 |
| `PrimaryFamilies.Length` | `∈ [1, 2]` | §3.5 |
| `MaxEnergyBase` | `> 0` | §3.5 |
| `MaxHandSizeBase` | `> 0` | §3.5 |
| `DegradedThresholdPct` (on layout) | `∈ [1, 99]` | §3.5 + F-VP1 (§5.1) |
| `CriticalThresholdPct` (on layout) | `∈ [1, DegradedThresholdPct - 1]` | §3.5 + CriticalState predicate (§5.4) |

**Chassis-specific instance values (Scout `Hull = 16`, etc.) are NOT
declared in this document.** They are tuning data and live in the
mechanics doc, keyed off this contract. This separation is the entire
reason for the split — the contract can pass review without the tuning,
and the tuning can change without disturbing the contract.

### 3.4 Playability Field — `IsPlayable` semantics

`IsPlayable` is the explicit gate field that distinguishes
**player-managed slots** (subject to install/scrap, contributing their
installed part's `GrantedCards` to the deck) from **authored-immutable
slots** (which exist in the layout for damage, structural, or visual
reasons but are not subject to player install/scrap actions).

**Domain:** `bool`. Authored on `SlotDefinition` (§3.1), copied at runtime
construction (§3.3) to `SlotInstance.IsPlayable`.

**Contract — when `IsPlayable == true`:**

- `IVehicleMutator.InstallPart(slotId, part)` may succeed on this slot.
- `IVehicleMutator.ScrapSlot(slotId)` may succeed on this slot **only
  when `slot.IsStructural == false`**. Structural slots — even when
  playable (e.g., Player Hull) — MUST throw `StructuralSlotException`
  on scrap. A player can never scrap their own death-gating slot. This
  guard is orthogonal to the `IsPlayable` check: the install workflow
  is unrestricted by `IsStructural`; only scrap is. (The orthogonality
  table below remains correct — Player Hull is
  `IsPlayable=true, IsStructural=true`: installable, but un-scrappable.)
- The slot's `InstalledPart.GrantedCards`, if any, contribute to the
  player's deck via the deck-build pipeline.
- The slot appears in player-facing install UI choices (mechanics doc
  owns the UI rules).

**Contract — when `IsPlayable == false`:**

- `IVehicleMutator.InstallPart(slotId, part)` MUST throw
  `SlotNotPlayableException`. The slot is authored-immutable; the
  layout's pre-filled part is fixed for the run.
- `IVehicleMutator.ScrapSlot(slotId)` MUST throw `SlotNotPlayableException`.
- The slot's `InstalledPart.GrantedCards`, if any, do NOT contribute to
  the player's deck (deck builder filters by `slot.IsPlayable`).
- The slot still takes damage normally, fires `OnSlotHpChanged` /
  `OnSlotDamageStateChanged` events normally, and contributes to
  structural-HP / death calculations per its `IsStructural` flag
  (which is orthogonal — see truth table below).
- The slot still consumes the `Armor → structural redirect` chain if
  `Kind == Armor` (per §3.1 `RedirectsToSlotId`).

**Orthogonality with `IsStructural`:** the four combinations are all valid:

| `IsPlayable` | `IsStructural` | Example |
|---|---|---|
| `true` | `true` | Player Hull slot (death-gating + player-managed) |
| `true` | `false` | Player Weapon / Engine / Mobility slot |
| `false` | `true` | Enemy Hull (pre-authored part; slot death ends the encounter) |
| `false` | `false` | Armor plate (authored-immutable; damage diverts via `RedirectsToSlotId`) |

**Default value:** `true`. Player chassis layouts (Scout in EA) author
all slots as `IsPlayable = true`. Enemy layouts and Armor slots author
`IsPlayable = false`. The default of `true` is deliberate — the more
restrictive value (`false`) is the one that requires explicit designer
intent, so authoring errors fail safe toward "player can interact."

**Expected consumers (cross-doc):**

- Mechanics doc AC-VP21 (install workflow) — references this field for
  the `IsPlayable == false` rejection clause.
- Mechanics doc AC-VP24 (scrap workflow) — references this field for the
  `IsPlayable == false` rejection clause **and** the
  `IsStructural == true` rejection clause (Player Hull is the canonical
  example of the latter).
- Mechanics doc AC-VP44d (deck builder) — references this field for the
  `GrantedCards` filtering rule.
- Mechanics doc `IsPlayable == false` UI affordance — defines the
  player-facing UI treatment for slots that fail the playability check
  (greyed install button, locked tooltip wording, scrap-attempt error
  toast). Architecture publishes the exception types
  (`SlotNotPlayableException`, `StructuralSlotException`) and contract
  semantics; the UI affordance that consumes them is downstream.
  `[REVERSE PENDING — design/gdd/vehicle-and-part-mechanics.md unwritten]`
  per §7.4. Closes R1 recommendation #14.
- §3.5 validator — `IsPlayable` field must be present and bool-typed on
  every `SlotDefinition`; null is not permitted.
- §5 formulas — `IsPlayable` does NOT appear as an input to F-VP1, F-VP2,
  or F-VP3. It gates workflow APIs, not formula evaluation.

**New exception types:** two are added to the `WastelandRun.Vehicle`
exception set alongside `SlotNotFoundException`, `PartIncompatibleException`,
and `MountDirectionMismatchException`:

- `SlotNotPlayableException` — thrown by `InstallPart` / `ScrapSlot`
  when `slot.IsPlayable == false`.
- `StructuralSlotException` — thrown by `ScrapSlot` when `slot.IsPlayable
  == true` but `slot.IsStructural == true`. Distinct from
  `SlotNotPlayableException` so the mechanics-doc UI affordance can
  surface a structural-specific message ("Hull cannot be scrapped — it
  is structural") rather than the generic "not playable" path.

Each exception payload includes the violating `SlotId` for diagnosis.

### 3.5 Validation Gates

This subsection is the **single home** for all schema-rejection rules.

**Two-layer enforcement (rec. #9, closed 2026-05-19).** Every gate
listed here is enforced at **two stages**, and both stages must agree
on the rule:

1. **`OnValidate` on the relevant SO** — **edit-time feedback only**.
   Fires when a designer edits the asset in the Inspector. Logs an
   error to the console and shows an inspector banner. **Does NOT
   block asset import**, **does NOT prevent the asset from being
   referenced by other code or scenes**, **does NOT fail a build**.
   Treat `OnValidate` as a UX affordance for the designer, not a
   correctness guarantee. Severity: per the rule tables below
   (Error/Warning).
2. **`AssetPostprocessor.OnPostprocessAllAssets`** — **the
   editor-session flag-clearing and observability layer**. Runs at
   import (including CI import). On any gate marked **Error**, the
   postprocessor (a) clears the asset's runtime `IsValidated` flag
   (`FrameLayoutSO.IsValidated = false` per §3.3), (b) emits a logged
   error and a `VehicleEvent.AssetValidationFailed` telemetry record,
   and (c) throws a named `*ValidationException` (per the exception
   types below). Warnings emit `VehicleEvent.AssetValidationWarning`
   telemetry but do not clear `IsValidated`. **Throw behavior in Unity
   6.x:** exceptions thrown from `OnPostprocessAllAssets` are caught
   internally by Unity's import pipeline — the throw does not abort the
   import or remove the asset from the project. The `IsValidated = false`
   flag is the actual runtime enforcement mechanism. **Scope of
   `IsValidated`:** the flag defends against mid-session reimport of a
   valid-but-now-invalidated asset within the same editor session (no
   domain reload between import and construction). Domain reload (e.g.,
   Play Mode entry with default settings) resets all
   `[System.NonSerialized]` fields to `false` — `IsValidated` is cleared
   on every domain reload, not just on validation failure. The §3.3 step 1
   guard reads this flag and refuses to construct against an unvalidated
   layout. **Build-time blocking** (preventing a shipped build from
   containing invalid chassis data) requires a separate
   `IPreprocessBuildWithReport` implementation that re-runs validation at
   build time and fails the build if any asset fails a gate. This is a
   planned hardening step tracked in the producer backlog; the current
   postprocessor provides the editor-session observability path only. This
   is the **rejects**-class behavior referenced by §9.0 and AC-VPA24a..i:
   the rejects-class contract is the *combination* of postprocessor
   flag-clearing + `IsValidated` runtime guard (editor-session scope) +
   build preprocessor (build-time scope).

Why both layers? `OnValidate` gives the designer a five-second
feedback loop while editing in the Inspector; `AssetPostprocessor` is
the import-time layer that clears the `IsValidated` flag and surfaces
errors in the console and telemetry. Implementing only `OnValidate`
produces green-in-Inspector / red-at-runtime drift; implementing only
`AssetPostprocessor` denies the designer an in-Inspector signal.

**No invariant is "assumed nonzero" or "assumed in range" — every
constraint is either declared here or accepted as a runtime contract
bug.**

The runtime formula code in §5 does NOT defensively re-check these
domains. It trusts the validator.

#### Gates on `SlotDefinition` (per entry)

| Rule | Severity |
|---|---|
| `SlotId` non-empty and unique within the layout | Error |
| `IsPlayable` field present and bool-typed (no `null`) | Error |
| `HudAnchor.IsInUnitRect()` (uses `float.IsFinite` per §3.2) | Error |
| `ExposureMultiplier` is finite (`!IsNaN`, `!IsInfinity`) — Armor slots | Error |
| `ExposureMultiplier > 0` — Armor slots | Error |
| `ExposureMultiplier < 1.0` — Armor slots (inverts design intent) | Warning |
| `ExposureMultiplier > 5.0` — Armor slots (overflow risk vs. F-VP3) | Warning |
| Armor: `HasMaxHpOverride == true` and `MaxHpOverride ≥ 1` | Error |
| Armor: `RedirectsToSlotId` non-null, non-empty, resolves in same layout | Error |
| Armor: `RedirectsToSlotId ≠` this `SlotId` (no self-redirect) | Error |
| Armor: target slot's `Kind ≠ Armor` (no Armor → Armor chains) | Error |
| Armor: target slot's `IsStructural == true` | Error |
| Non-Armor: if `HasMaxHpOverride == true`, `MaxHpOverride ≥ 1` | Error |
| `HasStructuralOverride == false` ⇒ `IsStructuralOverride` field ignored at construction (advisory only) | Warning |
| `HasMaxHpOverride == false` ⇒ `MaxHpOverride` field ignored at construction (advisory only) | Warning |

#### Gates on `FrameLayoutSO`

| Rule | Severity |
|---|---|
| `LayoutId` non-empty and unique across all `FrameLayoutSO` assets (CI grep-enforced) | Error |
| At least one slot with `IsStructural == true` (via Kind default or override) | Error |
| **`DegradedThresholdPct ∈ [1, 99]`** (rejects 0 and 100) | **Error (non-negotiable)** |
| **`CriticalThresholdPct ∈ [1, DegradedThresholdPct - 1]`** (strict-less) | **Error (non-negotiable)** |

#### Gates on `ChassisDefinitionSO`

| Rule | Severity |
|---|---|
| `FrameLayoutId` non-empty; resolves to existing `FrameLayoutSO` | Error |
| `DefaultSlotMaxHp` contains entries for `Hull`, `Weapon`, `Engine`, `Mobility` | Error |
| **`DefaultSlotMaxHp[*]` each value `> 0`** | **Error (non-negotiable)** |
| `StarterDeck.Length == 10` | Error |
| `PrimaryFamilies.Length ∈ [1, 2]` | Error |
| `MaxEnergyBase > 0` | Error |
| `MaxHandSizeBase > 0` | Error |
| `StarterParts[slotId].Kind` matches `SlotDefinition.Kind` in referenced layout | Error |
| `StarterParts[slotId].CompatibleChassis` includes this chassis (player only) | Error |
| `Σ StarterParts[*].GrantedCards.Count ≤ 4` | Error |

#### Non-negotiable gates — provenance

Three gates are marked **non-negotiable** because they are formula-input
domain constraints lifted to load-time validation. They have specific
provenance and are NOT subject to "loosen for designer convenience":

1. **`DefaultSlotMaxHp[*] > 0`** — closes R7 systems-designer B-cluster.
   `DefaultSlotMaxHp = 0` produces division-by-zero in F-VP1 and a silent
   degenerate Healthy band (per §5.4 canonical vocabulary). Must be
   rejected at load.
2. **`DegradedThresholdPct ∈ [1, 99]`** — closes R7 systems-designer
   B-cluster. Boundaries 0 and 100 produce a zero-width Healthy band;
   F-VP1 (§5.1) is undefined outside this range. Must be enforced at the
   validator, not assumed by callers.
3. **`CriticalThresholdPct ∈ [1, DegradedThresholdPct - 1]`** — closes
   R1 systems-designer blocker #5 (2026-05-19). §5.4's CriticalState
   predicate is undefined when `CriticalThresholdPct ≥ DegradedThresholdPct`
   — the Critical band becomes zero-width or inverted, and the predicate
   ordering rule (Destroyed → Critical → Degraded → Healthy) silently
   reclassifies HP values that designers expect to be Degraded. The
   strict-less constraint guarantees a non-empty Critical band whenever
   `MaxHp ≥ 2`; the small-`MaxHp` collapse case (both thresholds floor
   to 1) is handled by predicate ordering and is allowed. (Previously
   deferred with "added in next revision pass" language; that defer is
   now closed.)

#### Stale-discriminator warning text (closes R12, 2026-05-19)

The two Warning-severity "advisory only" rules on `SlotDefinition`
(`HasStructuralOverride == false ⇒ IsStructuralOverride field ignored`
and `HasMaxHpOverride == false ⇒ MaxHpOverride field ignored`) emit
fixed, scripted text so that designers, log search, and the
`VehicleEvent.AssetValidationWarning` telemetry payload all share a
single string. **Exact message text** (substitute `{SlotId}` and
`{Value}`):

- `MaxHpOverride` stale: `"[V&P][WARN] SlotDefinition '{SlotId}' has MaxHpOverride={Value} but HasMaxHpOverride=false. The override value is ignored at construction. Either set HasMaxHpOverride=true to use it, or clear the field to remove this warning."`
- `IsStructuralOverride` stale: `"[V&P][WARN] SlotDefinition '{SlotId}' has IsStructuralOverride={Value} but HasStructuralOverride=false. The override value is ignored at construction. Either set HasStructuralOverride=true to use it, or clear the field to remove this warning."`

Both messages are emitted by `OnValidate` and by the
`AssetPostprocessor` (the postprocessor additionally writes them to
the `VehicleEvent.AssetValidationWarning` payload's `Message` field).
Warning-severity rules do NOT clear `IsValidated` and do NOT throw —
they emit and continue.

#### Validation exception types

Validation failures throw exception types in
`WastelandRun.Vehicle.Validation`:

- `SlotValidationException` — gate on `SlotDefinition` failed
- `LayoutValidationException` — gate on `FrameLayoutSO` failed
- `ChassisValidationException` — gate on `ChassisDefinitionSO` failed

Each payload includes the asset's GUID, the failing field name, and the
offending value.

The `AssetPostprocessor.OnPostprocessAllAssets` import hook throws
these exceptions on **Error**-severity gate failures, failing the
import job (and, in CI, failing the build). `OnValidate` re-uses the
same gate logic but surfaces the result as inspector + console
errors only — it never throws past Unity's editor boundary because
that would crash the editor rather than the build. The
exception-throwing path lives in the postprocessor.

**Runtime exceptions** (`SlotNotFoundException`, `PartIncompatibleException`,
`MountDirectionMismatchException`, `SlotNotPlayableException`,
`StructuralSlotException` per §3.4) are distinct from validation
exceptions — runtime exceptions fire on contract violations at play
time, validation exceptions fire on schema violations at import time.

## 4. Frame-Ordering Convention
*Top-level convention. Resolves all same-frame collisions in one place;
per-event references point back here.*

### 4.1 Event Resolution Order (same-frame)

**The canonical ordering basis is the layout's `Slots` array index.** All
same-frame multi-slot state changes resolve in `FrameLayoutSO.Slots`
index order. This is deterministic per layout, replay-safe, and save-safe
(ADR-0003). Alternative bases (`Kind` priority, source-of-damage, screen
distance) are explicitly forbidden — they introduce hidden coupling that
breaks under refactor.

Within a single simulation tick, multi-slot damage resolves in three
phases:

#### Phase 1 — State mutation (no events fire)

1. Iterate `vehicle.Slots` in `FrameLayoutSO.Slots` index order.
2. For each slot present in the damage event's slot→amount map: apply
   the damage to `slot.Hp` (clamped to `[0, MaxHp]`).
3. If the slot is `Kind == Armor` and `slot.Hp` reached 0 in this step:
   - Compute `remaining = appliedDamage - hpBeforeApplication` (clamped `≥ 0`).
   - Resolve `RedirectsToSlotId` → target slot.
   - Apply `remaining × ExposureMultiplier` (via `SafeAmplify` §5.3) to
     `target.Hp` **immediately**, in this same tick. This redirect
     mutation does NOT spawn a new iteration; it modifies the target's
     Hp in place, and the target's own index-order iteration (if it has
     direct damage in the same event) reads the already-modified Hp.
4. Continue iterating remaining slots.

Phase 1 finishes with all state mutations committed. No events have
fired yet.

#### Phase 2 — Event publication (no state mutates)

Events fire **in the order their state changes were committed in
Phase 1**, one event per slot per channel:

- `OnSlotHpChanged(slotId, oldHp, newHp)` — for every slot whose Hp
  changed during Phase 1.
- `OnSlotDamageStateChanged(slotId, oldState, newState)` — for every
  slot whose damage state (per §5.4: `Healthy / Degraded / Critical /
  Destroyed`) changed.
- `OnArmorExposed(armorSlotId, redirectsToSlotId)` — for every Armor
  slot whose Hp reached 0 in this tick.

The Armor redirect's commit precedes the target slot's amplified-damage
commit in Phase 1, therefore `OnArmorExposed` fires BEFORE the target
slot's amplified `OnSlotDamageStateChanged`. This ordering is
load-bearing for the audio composition window (§4.4).

#### Phase 3 — Death check (single end-of-tick gate)

After all Phase 2 events have fired:

1. Recompute `vehicle.StructuralHp = Σ slot.Hp` over slots where
   `IsStructural == true`.
2. If `StructuralHp == 0` and was non-zero before this tick: fire
   `OnVehicleDied`.

The death check is a single gate, not per-slot. It is the only step in
this convention that does not iterate in layout-index order — it is
unconditional, runs exactly once per tick, and runs last.

#### Worked example — Plate→0 + Hull→0 same-frame

An AoE deals 8 damage to a Scout-like layout where:

- `Slots[0] = armor_plate` (Hp 4, `ExposureMultiplier = 3.0`,
  `RedirectsToSlotId = "hull_0"`, `IsStructural = false`)
- `Slots[1] = hull_0` (Hp 12, `IsStructural = true`)

The damage event's slot→amount map: `{ "armor_plate": 8 }` (Hull is not
directly targeted).

**Phase 1:**
1. `armor_plate`: apply 8 → `Hp = 4 → 0`. `remaining = 8 - 4 = 4`.
   Redirect `4 × 3.0 = 12` to `hull_0`. `hull_0.Hp = 12 → 0`.
2. `hull_0`: not in damage map; iteration is a no-op.

**Phase 2 (commit order):**
1. `OnSlotHpChanged("armor_plate", 4, 0)`
2. `OnSlotDamageStateChanged("armor_plate", Healthy, Destroyed)`
3. `OnArmorExposed("armor_plate", "hull_0")`
4. `OnSlotHpChanged("hull_0", 12, 0)`
5. `OnSlotDamageStateChanged("hull_0", Healthy, Destroyed)`

**Phase 3:**
6. Recompute `StructuralHp = 0`. Fire `OnVehicleDied`.

This sequence is deterministic for the given layout + damage. Replay
produces an identical event sequence. The audio composition window
(§4.4) and VFX layer order (§4.2) consume this sequence; they do not
re-order it.

### 4.2 VFX Layer Order (same-frame)

This subsection declares the **publish-surface convention** for VFX
spawn requests. The actual rendering — particle Z, additive blending,
post-FX — lives in the VFX system (separate GDD). This document
declares only the timing and ordering rules that VFX consumers must
respect.

#### Spawn timing

VFX subscribers spawn visual effects in response to Phase 2 events from
§4.1. They do NOT spawn during Phase 1 (state mutation) or Phase 3
(death check). **No VFX can observe a Phase 1 mid-iteration state.** If
a VFX needs to mark "the moment armor breaks," it subscribes to
`OnArmorExposed` (Phase 2, after redirect commit), not to a hypothetical
mid-Phase-1 hook.

#### Same-frame layering rule

When multiple Phase 2 events fire in the same tick, **VFX layer in
event-fire order**. Earlier events go on the bottom layer; later events
go on top. This is the same order as §4.1 Phase 2 commit order (which
is layout-index order from Phase 1).

The architecture doc does NOT suppress same-frame VFX. Every Phase 2
event publishes a VFX request. Suppression — if any — is a VFX system
policy applied AFTER the architecture publishes. This separation lets
the contract stay stable while the visual feel evolves.

#### Phase 2 events covered by this convention

§4.1 listed the canonical three (`OnSlotHpChanged`,
`OnSlotDamageStateChanged`, `OnArmorExposed`). The same Phase 1→2
convention applies to all other damage-channel events that fire during
a damage tick:

- `OnPlatingChanged(slotId, oldStacks, newStacks)` — plating stacks
  changed during damage application.
- `OnCriticalStateChanged(slotId, isCritical)` — see §5.4.
- `OnStatusStackChanged(slotId, statusId, oldStacks, newStacks)` — see
  `status-effects.md`.

For all of these, the publish-order rule (layout-index order, then
commit order within slot) and the no-Phase-1-spawn rule apply identically.

#### Worked example — OnArmorExposed + OnPlatingChanged same-frame

An AoE deals damage to a layout where:

- `Slots[0] = armor_plate` (Hp 3, redirects to `weapon_front`, exposure 3.0)
- `Slots[1] = weapon_front` (PlatingStacks = 2; mechanics doc owns the
  per-plate absorption rule)

The damage breaks the plate, redirects amplified damage to
`weapon_front`, which strips its plating and loses HP.

**Phase 2 commit order:**
1. `OnSlotHpChanged("armor_plate", ...)`
2. `OnSlotDamageStateChanged("armor_plate", Healthy, Destroyed)`
3. `OnArmorExposed("armor_plate", "weapon_front")`
4. `OnPlatingChanged("weapon_front", 2, 0)`
5. `OnSlotHpChanged("weapon_front", ...)`

**VFX layering (bottom → top), same-frame:**

| Layer | Source event | Effect (illustrative; VFX system owns specifics) |
|---|---|---|
| 1 | `OnSlotHpChanged("armor_plate")` | Plate hit-flash |
| 2 | `OnSlotDamageStateChanged("armor_plate", →Destroyed)` | Plate state-transition smoke |
| 3 | `OnArmorExposed("armor_plate", "weapon_front")` | Plate-shatter shards |
| 4 | `OnPlatingChanged("weapon_front", 2, 0)` | Plating-loss spark burst |
| 5 | `OnSlotHpChanged("weapon_front")` | Weapon hit-flash |

The VFX system is free to compose these layers visually (cross-fade,
stagger, color-key) — the architecture requires only that the **order
of arrival** be Phase-2 commit order. A VFX subscriber that receives
`OnPlatingChanged` in this frame cannot assume it fired before any
preceding event in the same frame; if relative ordering matters, the
subscriber consults the event's tick-sequence index.

#### Cross-references

- Phase 2 event sequence: §4.1
- Subscriber invocation order within a single event: §4.3
- Audio composition during this tick: §4.4 (arbiter lives in Audio System)
- Particle prefab selection, blending, post-FX: VFX system GDD (TBA)

### 4.3 Subscriber Invocation Order

When a single Phase 2 event has multiple subscribers, **invocation runs
in a declared phase order**. This rule is the contract; the
implementation mechanism is below.

#### Subscriber phases (per event)

For each event fired in Phase 2, subscribers invoke in this order:

| Phase | Purpose | Examples |
|---|---|---|
| **2a — State** | Update derived state that other subscribers depend on | Deck builder removes cards on `OnGrantedCardRemoved`; status registry decrements |
| **2b — UI/HUD** | Read state, update visuals | HUD crosses out card; HP bar interpolates |
| **2c — VFX** | Spawn visual effects per §4.2 layering | Spark bursts, shatter shards |
| **2d — Audio** | Queue audio cues (arbitrated in §4.4) | Plate-shatter pre-roll, hit-cue |

Within a phase, subscribers invoke in **registration order**.
Registration is deterministic because all core subscribers register at
well-known initialization points (combat-scene boot, HUD widget
construction, audio system init).

#### Registration API

The phase is captured at subscription time:

```csharp
public interface IVehicleEventBus
{
    void Subscribe<TEvent>(SubscriberPhase phase, Action<TEvent> handler);
    void Unsubscribe<TEvent>(Action<TEvent> handler);
}

public enum SubscriberPhase
{
    State,    // 2a
    UI,       // 2b
    VFX,      // 2c
    Audio,    // 2d
}
```

`IVehicleView`'s existing `event Action<...>` declarations remain the
public publish surface. The dispatcher behind those events routes
through the bus; legacy `view.OnSlotHpChanged += handler` registration
without a declared phase defaults to `UI` and logs a one-time warning
per call site (migration path, not long-term shape).

#### Mandatory rules

**Subscribers MUST NOT mutate vehicle state during invocation.** Phase 2
is observation-only. A subscriber that needs to trigger a follow-on
damage event or part install queues the operation through
`IVehicleMutator`; the operation fires in a subsequent tick, not inline.
This rule prevents mid-tick re-entrancy that would break ADR-0003
determinism.

**Subscribers MUST NOT throw out of the handler.** Exceptions are
caught, logged, and the dispatcher continues with the next subscriber.
A throwing handler does NOT abort publication for the remaining
subscribers. One broken consumer cannot corrupt the rest of the
system's view of state.

**Registration order is the within-phase tie-breaker.** When two
subscribers both register on Phase 2a (e.g., deck builder + status
registry both consuming `OnGrantedCardRemoved`), the one registered
first invokes first. Tests that depend on relative ordering MUST
register in the order they need; tests that do NOT depend on ordering
MUST be robust to either order.

#### Worked example — `OnGrantedCardRemoved`

When a part is scrapped, `OnGrantedCardRemoved(slotId, partId, cardIds[])`
fires once. Subscribers consume:

| Phase | Subscriber | Action |
|---|---|---|
| 2a | Deck builder | Removes `cardIds` from deck / hand / discard piles |
| 2a | Status registry | (if applicable) decrements status stacks granted by the part |
| 2b | Hand HUD | Crosses out removed cards in the hand display |
| 2b | Deck-count widget | Refreshes shown deck size |
| 2c | VFX | "Card dissolve" particle on each removed hand card |
| 2d | Audio | Card-loss cue (composed in §4.4) |

The Phase 2a state subscribers (deck builder, status registry) run
before Phase 2b reads state. The Hand HUD therefore always crosses out
cards that have already been removed from the deck collection — there
is no race window in which the HUD shows a card the deck no longer
contains.

#### Cross-references

- Phase 2 event sequence across slots: §4.1
- VFX layering across events same-frame: §4.2
- Audio composition window: §4.4
- Granted-card lifecycle (when cards are removed vs. soft-disabled): mechanics doc

### 4.4 Audio Composition Window (gate publish only; arbiter lives in Audio System)

This subsection declares the **publish-side contract** for audio cues.
The Audio System owns the composition arbiter — duck dB, pre-roll, mix,
suppression — and lives in a separate GDD. This document declares only
what V&P publishes and what timing guarantees the Audio System can rely
on when composing.

#### The boundary

| Owned here (V&P architecture) | Owned by Audio System |
|---|---|
| Which events publish audio cues | Which cue plays (library selection) |
| Payload metadata each event carries | Duck dB, pre-roll, mix-bus assignment |
| Phase 2 timing guarantees | Same-tick composition policy |
| Mutual-exclusion *hints* on the payload | Whether to suppress, cross-fade, or layer |
| Caption-key reference on the payload | Caption rendering and accessibility timing |

#### Audio-relevant Phase 2 events

The following events from §4.1 and §4.2 carry **audio composition
metadata** in their payload. The Audio System subscribes via Phase 2d
(§4.3) and reads the metadata to compose.

```csharp
public sealed record AudioCompositionHint(
    AudioSeverity Severity,            // Background | Foreground | Critical
    string MutualExclusionGroup,       // null if none; cues sharing a group are arbitrated
    int TickSequenceIndex,             // monotonic within the tick, for deterministic order
    string CaptionKey                  // localization key for accessibility captions
);

public enum AudioSeverity
{
    Background,   // ambient (e.g., engine hum)
    Foreground,   // standard SFX (hit, plating loss)
    Critical,     // narrative-level (vehicle death, armor shatter)
}
```

Each audio-relevant event includes a `Hint` field of type
`AudioCompositionHint`. The events:

| Event | Severity | MutualExclusionGroup | CaptionKey |
|---|---|---|---|
| `OnSlotHpChanged` | Foreground | `"slot.hit"` | `caption.slot.hit` |
| `OnSlotDamageStateChanged` (→ Degraded) | Foreground | `"slot.degraded"` | `caption.slot.degraded` |
| `OnSlotDamageStateChanged` (→ Critical) | Critical | `"slot.critical"` | `caption.slot.critical` |
| `OnSlotDamageStateChanged` (→ Destroyed) | Critical | `"slot.destroyed"` | `caption.slot.destroyed` |
| `OnArmorExposed` | Critical | `"armor.shatter"` | `caption.armor.shatter` |
| `OnPlatingChanged` (decrease) | Foreground | `"plating.loss"` | `caption.plating.loss` |
| `OnVehicleDied` | Critical | `"vehicle.death"` | `caption.vehicle.death` |

The `OnSlotDamageStateChanged (→ Critical)` row closes R1 blocker #6 —
the player now hears the most dangerous transition (one hit from
destruction). The `(→ Destroyed)` row replaces the older `(→ Offline)`
naming to match §5.4's canonical 4-state vocabulary (`Healthy / Degraded
/ Critical / Destroyed`).

The `MutualExclusionGroup` strings are part of the V&P contract.
Renaming or removing a group is a breaking change and requires
coordinated update in the Audio System GDD.

#### Timing guarantees V&P provides

The Audio System can rely on these guarantees:

1. **All Phase 2 events for a single tick publish within the same
   frame.** The Audio System receives the full set before composition
   begins; it does not need to wait for "more events" from this tick.
2. **`TickSequenceIndex` is monotonic and dense** within a tick
   (`0, 1, 2, ..., N`). The Audio System uses this for deterministic
   ordering when multiple cues land in the same composition window.
3. **No mid-tick state mutation.** Per §4.3, subscribers cannot trigger
   follow-on damage events inline. The Audio System never sees a
   "second wave" of events from the same originating damage call.
4. **Replay determinism.** Given the same `RunSeed` and the same input
   sequence, the same set of events publishes with identical `Hint`
   payloads. The Audio System can therefore replay deterministically
   (ADR-0003).

#### What V&P does NOT prescribe

The Audio System decides:

- Which cue from a same-tick exclusion group actually plays.
- Whether to apply ducking and at what dB (mechanics doc lands specific
  values; Audio System enforces them).
- Pre-roll timing (e.g., plate-shatter pre-roll duration; mechanics doc
  lands the duration).
- Cross-fade vs. hard-cut decisions.
- Cross-group composition (whether independent groups play
  simultaneously or one ducks the other).

If the V&P contract changes (new event added, severity reclassified,
exclusion group renamed), the Audio System reacts to the published
metadata; it does not re-implement V&P logic.

#### Cross-references

- Phase 2 publication order: §4.1
- Subscriber invocation order (Phase 2d for audio): §4.3
- Concrete audio cue specs (duck dB, pre-roll durations, caption text
  content): mechanics doc + Audio System GDD
- Localization key registry: localization system (TBA)

## 5. Formulas
*F-VP1/F-VP2/F-VP3 with declared input domains, clamping rules, and
boundary behavior. Variable definitions, ranges, example calculations.*

### 5.1 F-VP1 — Degraded-state threshold

**Formula:**

```
DegradedThreshold(slot) = max(1, floor(layout.DegradedThresholdPct × slot.MaxHp / 100))
```

**Inputs and their domains** (enforced at validation per §3.5; the
runtime formula does NOT defensively re-check):

| Variable | Type | Domain | Source |
|---|---|---|---|
| `layout.DegradedThresholdPct` | int | `[1, 99]` | `FrameLayoutSO.DegradedThresholdPct` |
| `slot.MaxHp` | int | `> 0` | `SlotDefinition.MaxHpOverride ?? chassis.DefaultSlotMaxHp[Kind]` |

Values outside these domains indicate a contract violation upstream and
are never reached at runtime.

**Output:** `DegradedThreshold ∈ [1, slot.MaxHp − 1]`. The bounds are tight:

- Lower bound `1`: guaranteed by the `max(1, …)` clamp.
- Upper bound `slot.MaxHp − 1`: guaranteed by `DegradedThresholdPct ≤ 99`,
  which makes `floor(0.99 × MaxHp) ≤ MaxHp − 1` for all `MaxHp ≥ 1`.

**Consumed by §5.4 — F-VP1 is not a state machine.** F-VP1 produces
`DegradedThreshold`, one of the two thresholds the canonical state
predicate in §5.4 consumes (the other is `CriticalThreshold`). §5.1
declares the threshold formula; §5.4 declares the state machine. **§5.4
is the canonical state model for this architecture; §5.1 owns only this
formula.**

Slots with `InstalledPart == null` are queried for damage state like any
other slot — **occupancy (`InstalledPart` nullness) is orthogonal to
damage state** and is checked directly by consumers (deck builder,
install UI), not via the state predicate. An empty slot at full HP is
`Healthy` per §5.4; "is the slot occupied" is a separate, independent
question.

#### The `max(1, …)` clamp — integer-floor edge case

For low `MaxHp` values, `floor(pct × MaxHp / 100)` can produce `0`:

- `pct = 50, MaxHp = 1` → `floor(0.5) = 0`
- `pct = 30, MaxHp = 3` → `floor(0.9) = 0`

Without the clamp the Degraded band would be unreachable: `Hp > 0` would
mean `Hp > 0 = DegradedThreshold` (so per §5.4 the slot would be
`Healthy`), `Hp == 0` is `Destroyed`, and the slot would silently jump
`Healthy → Destroyed` without ever passing through Degraded or Critical.
The clamp guarantees a Degraded band of at least `Hp == 1`. Cost: at
very low `MaxHp` the Degraded band is narrow, but the state warning
fires at least once before `Destroyed`.

**This clamp is NOT a boundary-domain guard** — it handles the
integer-floor identity for *legal* inputs. The boundary-domain guards
live at §3.5.

#### Boundary behavior at the domain edges

| `DegradedThresholdPct` | `MaxHp` | `DegradedThreshold` | Degraded band |
|---|---|---|---|
| 1 | 100 | `max(1, floor(1)) = 1` | `Hp == 1` only |
| 1 | 16 (Scout Hull) | `max(1, floor(0.16)) = 1` | `Hp == 1` only |
| 50 | 16 | `max(1, floor(8)) = 8` | `Hp ∈ [1, 8]` |
| 99 | 100 | `max(1, floor(99)) = 99` | `Hp ∈ [1, 99]` |
| 99 | 1 | `max(1, floor(0.99)) = 1` | `Hp == 1` only |
| **0** | any | **rejected at validation** | n/a |
| **100** | any | **rejected at validation** | n/a |
| any | **0** | **rejected at validation** | n/a |

#### Determinism

F-VP1 is pure integer arithmetic over inputs that are fixed at load.
Given the same `(DegradedThresholdPct, MaxHp)` it returns the same
threshold every call. No `System.Random`, no floating-point
intermediates — C# integer division performs the floor implicitly.

#### Worked examples

| Chassis | SlotId | MaxHp | ThresholdPct | DegradedThreshold | Notes |
|---|---|---|---|---|---|
| Scout | `hull_0` | 16 | 50 | 8 | typical |
| Scout | `weapon_front` | 10 | 50 | 5 | typical |
| Scout | `engine_0` | 12 | 50 | 6 | typical |
| Hypothetical | `tiny_slot` | 1 | 50 | 1 | clamped from `floor(0) = 0` |
| Hypothetical | `small_slot` | 3 | 30 | 1 | clamped from `floor(0) = 0` |

### 5.2 F-VP2 — Installed-count clamp

**Purpose.** Reports the number of installed parts on a vehicle as a clamped
integer so consumers can reason about install state without re-implementing
boundary handling.

**Formula.**

```
InstalledCount(vehicle) = clamp(
    count(slot in vehicle.Slots where slot.InstalledPart != null),
    0,
    vehicle.Slots.Count
)
```

**Inputs and domains.**

| Input | Type | Domain | Source |
|-------|------|--------|--------|
| `vehicle.Slots` | `IReadOnlyList<SlotInstance>` | length ≥ 1 | runtime vehicle model (§3.3) |
| `slot.InstalledPart` | `PartInstance?` | nullable; null = empty slot | runtime vehicle model |

**Output.**

| Output | Type | Domain |
|--------|------|--------|
| `InstalledCount` | `int` | `[0, vehicle.Slots.Count]` |

**Why the clamp at all?** Three reasons:

1. **Consumer reasoning.** Downstream formulas (e.g. mechanics-doc
   `InstallCost`, scaling buffs) get a guaranteed bounded integer and can use
   it as an index, divisor, or interpolation parameter without defensive
   bounds checks of their own.
2. **Forward compatibility.** A future "phantom-install" buff that injects
   logical installs without a backing slot (granted-card mechanics, set-bonus
   simulation) must not break the contract. The clamp ensures
   `InstalledCount ≤ totalSlots` even if a buff over-counts; the upper-bound
   clamp absorbs the bug rather than corrupting downstream math.
3. **Reconfiguration safety.** During hot-swap operations (mechanics-doc
   territory), an intermediate frame can briefly see `InstalledPart` pointing
   to a stale reference while `slot.IsPlayable` is false. The clamp protects
   downstream consumers from observing a transient over-count during this
   single-frame window.

**Upstream idempotency requirement (rec. #11, closed 2026-05-19).** The
F-VP2 clamp protects downstream consumers but must not become a band-aid
that hides upstream double-fire bugs. The architecture requires:

- `IVehicleMutator.UninstallPart(slotId)` MUST be **idempotent at the
  `OnPartUninstalled` boundary**. Calling `UninstallPart` on a slot whose
  `InstalledPart` is already `null` is a no-op: no event re-fires, no
  state changes, no telemetry. The mutator owns the dedup check.
- `IVehicleMutator.InstallPart(slotId, part)` MUST be **rejection-only on
  re-install** at the `OnPartInstalled` boundary. Calling `InstallPart`
  on a slot whose `InstalledPart` is already non-null throws
  `SlotAlreadyOccupiedException` (named per §9 verb glossary forthcoming
  in Phase B Day 4); it does not silently overwrite, and it does not
  re-fire `OnPartInstalled`. Hot-swap = explicit uninstall-then-install
  pair, never an implicit overwrite.
- Subscribers MAY assume each `OnPartInstalled` / `OnPartUninstalled`
  event corresponds to a distinct transition between "occupied" and
  "empty"; they MUST NOT need to defensively count past-fires or
  maintain per-slot dedup state.

When the F-VP2 upper-bound clamp engages (`counted > vehicle.Slots.Count`
— almost always a double-fire bug upstream), the bus emits a
`VehicleEvent.InstalledCountClamped` telemetry record carrying the
originating call-site, the observed count, the clamped value, and the
frame number. Debug builds also log a warning. Production builds clamp
silently to keep downstream math sane while the telemetry surfaces the
underlying defect to whoever owns the offending mutator path.

Mechanics-doc owns the actual fix (idempotent mutator implementation,
hot-swap call-pair contract, granted-card simulation semantics).
Architecture guarantees only the **clamp behavior** and the **telemetry
signal** — i.e., the F-VP2 clamp is the *floor* of correctness, not the
ceiling.

**Boundary behavior.**

| Scenario | Slots count | Installed parts | `InstalledCount` |
|----------|-------------|-----------------|------------------|
| Empty vehicle | 4 | 0 | 0 |
| Partial install | 4 | 2 | 2 |
| Full install | 4 | 4 | 4 |
| Stale over-count (defensive) | 4 | counted = 5 | 4 (clamped) |

**Determinism.** Pure integer arithmetic, O(n) over slot list, no RNG, no
floating-point. Same inputs → same output every call.

**IsPlayable filtering — explicit non-policy.** F-VP2 does **not** filter on
`IsPlayable`. The count includes installed parts in disabled slots. Consumers
that need "playable installed parts only" must add the filter inline. This
keeps F-VP2 a primitive that can be composed with §3.4 semantics, rather than
baking one specific policy into the contract.

**Consumers.**

- Mechanics doc `InstallCost` formula — uses `InstalledCount` to scale install
  cost so the second install is more expensive than the first.
  **Closes R7 economy-designer BLOCK-2 (architecture surface only).** Full
  closure pending mechanics-doc `InstallCost` authorship against
  `InstalledCount`. The prior monolith used flat install cost and made early
  installs strictly optimal; this section guarantees the primitive (F-VP2) is
  available — the mechanics doc owns the cost curve that consumes it.
  `[REVERSE PENDING — design/gdd/vehicle-and-part-mechanics.md unwritten]`
  per §7.4.
- Mechanics doc set-bonus checks (future).
- HUD install-counter readouts.

**Cross-references.**

- §3.3 — `SlotInstance` and `InstalledPart` field definitions.
- §3.4 — `IsPlayable` orthogonality (consumers may compose).
- §3.4 — `IVehicleMutator.UninstallPart` / `InstallPart` idempotency
  contract (referenced by **Upstream idempotency requirement** above).
- §3.5 — slot-count validation gate (`vehicle.Slots.Count ≥ 1`).
- §6.10 — sibling roguelike-friendly policy. The bus catches a contract
  violation and emits telemetry without weaponising it against the run
  (there: `VehicleReentrancyException` → `ReentrancyBlocked`; here:
  F-VP2 over-count → `InstalledCountClamped`). Same posture, different
  trigger surface.

### 5.3 F-VP3 — `SafeAmplify` (0-multiplier preservation)

**Purpose.** Applies a multiplier to a base value with explicit handling of
boundary inputs (zero, negative, NaN, infinity). The contract guarantees
that a `0` multiplier produces a `0` output — no silent rewrite to `1`,
`epsilon`, or "skip the multiplier" branches.

**Formula.**

```
SafeAmplify(baseValue, multiplier) =
    if   not float.IsFinite(baseValue)  → throw SafeAmplifyDomainException
    elif not float.IsFinite(multiplier) → throw SafeAmplifyDomainException
    elif multiplier == 0f               → 0f                     // exact zero, both +0 and -0
    elif multiplier <  0f               → throw SafeAmplifyDomainException
    else
        result := baseValue * multiplier
        if not float.IsFinite(result)   → throw SafeAmplifyDomainException
        else                            → result
```

**Post-multiply finite check (rec. #10, closed 2026-05-19).** Even when
both inputs are finite, the multiplication can produce a non-finite
result by overflow — `float.MaxValue × 2f → +Infinity`. The post-multiply
guard catches this case: any non-finite result throws
`SafeAmplifyDomainException("post-multiply overflow")`. This closes the
chain-Inf loophole where a buff stack like `SafeAmplify(SafeAmplify(huge,
big), bigger)` could silently produce `Inf` and propagate into
downstream damage math. The contract requires three things: inputs are
finite, the multiplier is non-negative, **and** the result is finite.
All three are enforced at the call boundary; none is implicit.

**Inputs and domains.**

| Input | Type | Domain | Source |
|-------|------|--------|--------|
| `baseValue` | `float` | `float.IsFinite(baseValue) == true`; any sign | caller (typically a damage/effect value) |
| `multiplier` | `float` | `float.IsFinite(multiplier) == true`; `≥ 0f` | buff/debuff stack composition |

**Output.**

| Output | Type | Domain |
|--------|------|--------|
| `result` | `float` | `float.IsFinite(result) == true` |

**Why the 0-multiplier rule.** The R7 systems-designer finding flagged a
common antipattern where amplification helpers silently substitute a small
positive floor (e.g. `max(multiplier, 0.01f)`) to avoid division-by-zero in
downstream code or to "preserve at least some damage." That substitution is
load-bearing in two failure modes:

1. **Immunity buffs.** A card that grants "this attack deals no damage"
   composes its 0× multiplier into a buff stack. A silent floor turns
   immunity into 1% damage, which is a balance-affecting bug that is
   invisible at the call site.
2. **Conditional gates.** Effects gated by `if (multiplier > 0)` rely on
   exact-zero arithmetic. A silent floor breaks the gate and fires an
   effect that should have been suppressed.

The contract states the rule once, here, so every consumer can rely on it:
**multiplier == 0 → output == 0, exactly, every time.**

**Why exception over clamp on negatives.** A negative multiplier is a
caller bug, not a recoverable state. Returning `0` would mask it; clamping
to `0` would mask it. An exception surfaces the bug at the throwing site
where the stack trace is meaningful. Buff composition (mechanics doc) is
responsible for never producing a negative multiplier in the first place;
this is a contract assertion, not a runtime fallback.

**Why exception on NaN/infinity.** Same reasoning: any non-finite input is
either a caller bug or upstream RNG corruption. Letting it propagate would
contaminate downstream state silently. Throwing here keeps the failure
local.

**Boundary behavior.**

| `baseValue` | `multiplier` | Result | Reason |
|---|---|---|---|
| `10f` | `2f` | `20f` | normal path |
| `10f` | `1f` | `10f` | identity |
| `10f` | `0f` | `0f` | **exact zero preservation** |
| `10f` | `-0f` | `0f` | `-0f == 0f` in IEEE 754; preserved as `0f` |
| `0f` | `5f` | `0f` | zero base × any finite multiplier |
| `10f` | `0.0001f` | `0.001f` | small multiplier passes through (no floor) |
| `-10f` | `2f` | `-20f` | negative base allowed (e.g. healing-as-negative-damage) |
| `10f` | `-1f` | **throw** | `SafeAmplifyDomainException("multiplier must be ≥ 0")` |
| `float.NaN` | `2f` | **throw** | `SafeAmplifyDomainException("baseValue must be finite")` |
| `10f` | `float.PositiveInfinity` | **throw** | `SafeAmplifyDomainException("multiplier must be finite")` |
| `float.MaxValue` | `2f` | **throw** | `SafeAmplifyDomainException("post-multiply overflow")` — both inputs finite, result is `+Infinity` |

**Determinism — scoped to IL2CPP x64 (rec. #8, closed 2026-05-19).**
Single-precision IEEE 754 multiplication is bit-exact on **IL2CPP x64**
— the verified target. No RNG; no intrinsics with x64-platform-dependent
rounding. Same inputs → same output every call on this platform.

**The claim is NOT extended to ARM64 without per-platform validation.**
Apple Silicon, mobile ARM64, and other targets may emit different
SIMD/FMA intrinsics whose rounding diverges from x64 by a ULP. The
broader cross-platform determinism contract lives in ADR-0003;
expanding F-VP3's determinism guarantee to a new platform requires a
per-platform validation pass (golden-master comparison across
`baseValue × multiplier` over a representative input grid) before any
code can rely on cross-platform reproducibility through this primitive.
Until such validation lands, replay/save-load round-trips that consume
F-VP3 outputs must restrict themselves to x64 builds.

**Exception family.** `SafeAmplifyDomainException` extends a shared
`VehicleContractException` base (defined alongside `SlotNotPlayableException`
in §3.4). All contract-violation exceptions in the V&P assembly share this
base so consumers can catch broadly at integration boundaries (combat-loop
top-level) without leaking unrelated framework exceptions.

**Composition with buff stacks.** Multiple buffs compose by multiplying
their multipliers in sequence:

```
final = SafeAmplify(SafeAmplify(SafeAmplify(base, m1), m2), m3)
```

Because the 0 case short-circuits to `0` and `0 × anything == 0`, a single
0× buff anywhere in the chain zeros the final value regardless of
composition order. Order independence for zero is a property of the
contract, not an accident of multiplication — consumers can rely on it.

**Consumers.**

- Mechanics doc damage pipeline — applies armor/exposure/buff multipliers
  in sequence.
- Mechanics doc heal pipeline — same primitive with `baseValue < 0`
  convention.
- Card effect resolution (combat scene) — buff/debuff stack composition.

**Cross-references.**

- §3.4 — `VehicleContractException` base class.
- §3.5 — validation gates do not need to check `SafeAmplify` inputs;
  contract is enforced at the call site, not at asset load.
- §4.3 — subscriber phase model; `SafeAmplify` is called inside Phase 1
  (state mutation), never in event handlers.

### 5.4 CriticalState — derivation rule

**Purpose.** Declares the predicate for the `Critical` damage state at the
slot level, so consumers (HUD, audio, mechanics-doc soft-disable lifecycle)
can derive Critical status from canonical state without re-implementing the
threshold rule. Closes R7 systems-designer **B5**: prior monolith referenced
`CriticalState` without defining it.

**Canonical state model.** §5.4 is the **single authoritative declaration
of slot damage states** for this architecture. Any other section that
references slot damage state must point here. §5.1 declares the F-VP1
threshold formula (one input to this predicate); it is not a state
machine. §4.1 (`OnSlotDamageStateChanged` events) and §4.4 (audio
composition) consume this vocabulary. The four state names —
`Healthy / Degraded / Critical / Destroyed` — are contract-stable;
renaming any of them is a breaking change requiring coordinated update
across §4.1 worked examples, §4.4 audio events, the mechanics doc, and
the save/load schema.

**State model — slot damage states.** A slot occupies exactly one of four
states, derived from `slot.CurrentHp` and `slot.MaxHp`:

| State | Predicate | Notes |
|---|---|---|
| `Healthy` | `CurrentHp > DegradedThreshold` | full functionality |
| `Degraded` | `CriticalThreshold < CurrentHp ≤ DegradedThreshold` | F-VP1 governs entry |
| `Critical` | `0 < CurrentHp ≤ CriticalThreshold` | this section governs entry |
| `Destroyed` | `CurrentHp == 0` | terminal; mechanics doc owns lifecycle |

`Healthy → Degraded → Critical → Destroyed` is the only legal progression
under monotonic damage. Healing can move the slot back up the chain (see
"Hysteresis" below).

**Formula — CriticalThreshold derivation.**

```
CriticalThreshold(slot) = max(1, floor(CriticalThresholdPct × MaxHp / 100))
```

This is structurally identical to F-VP1 with `CriticalThresholdPct`
substituted for `DegradedThresholdPct`. The `max(1, …)` floor guarantees
`CriticalThreshold ≥ 1`, so `CurrentHp == 0` is never reachable from a
Critical-state transition — Destroyed is its own state.

**Inputs and domains.**

| Input | Type | Domain | Source |
|---|---|---|---|
| `slot.CurrentHp` | `int` | `[0, slot.MaxHp]` | runtime slot state |
| `slot.MaxHp` | `int` | `> 0` (validated §3.5) | resolved per F-VP1 |
| `CriticalThresholdPct` | `int` | `[1, DegradedThresholdPct - 1]` | `FrameLayoutSO.CriticalThresholdPct` (§3.3) |
| `DegradedThresholdPct` | `int` | `[1, 99]` | `FrameLayoutSO.DegradedThresholdPct` (§3.3) |

**Output.**

| Output | Type | Domain |
|---|---|---|
| `CriticalThreshold` | `int` | `[1, DegradedThreshold - 1]` |
| `state` | `enum { Healthy, Degraded, Critical, Destroyed }` | exactly one |

**Predicate (state lookup).**

```
GetSlotDamageState(slot) =
    if   slot.CurrentHp == 0                            → Destroyed
    elif slot.CurrentHp ≤ CriticalThreshold(slot)       → Critical
    elif slot.CurrentHp ≤ DegradedThreshold(slot)       → Degraded
    else                                                → Healthy
```

The order matters: Destroyed is checked first (the only state with
`CurrentHp == 0`), then Critical, then Degraded, then Healthy as the
default. This ordering removes any ambiguity at threshold boundaries: a
slot with `CurrentHp` exactly at `CriticalThreshold` is **Critical** (the
`≤` is inclusive), and a slot with `CurrentHp` exactly at
`DegradedThreshold` but above `CriticalThreshold` is **Degraded**.

**Why a separate Critical state.** Two reasons:

1. **HUD differentiation.** Players need a perceptual signal that a slot is
   one hit away from destruction. Degraded is "noticed"; Critical is
   "alarming." Mechanics doc owns the experiential rules; architecture
   declares the state exists.
2. **Soft-disable hook surface.** Mechanics-doc soft-disable lifecycle
   (granted-card lifecycle, audio severity escalation) gates on Critical,
   not on raw HP. A single named state lets multiple subsystems subscribe
   to the same canonical transition.

**Hysteresis — explicit non-policy.** This section does **not** add
hysteresis to the threshold predicate. A slot at exactly `CriticalThreshold`
flips between Degraded and Critical on each 1-HP delta. The reasoning:

- F-VP1 already establishes that thresholds are derived from
  `floor(pct × MaxHp / 100)` and are stable for a given `MaxHp`.
- Hysteresis (e.g. "enter at threshold, exit at threshold + 1") couples
  state to history, which complicates save/load — the state would no longer
  be a pure function of `CurrentHp`.
- The mechanics doc may choose to add hysteresis at the *presentation*
  layer (debounce VFX/audio re-triggers) without changing the canonical
  state predicate.

If a future revision needs hysteretic state, it must be declared as a new
state model with explicit save/load semantics, not bolted onto this
predicate.

**Boundary behavior.**

| `MaxHp` | `CriticalPct` | `DegradedPct` | `CurrentHp` | State |
|---|---|---|---|---|
| 50 | 10 | 30 | 50 | Healthy |
| 50 | 10 | 30 | 16 | Healthy (`floor(30×50/100) = 15`; `16 > 15`) |
| 50 | 10 | 30 | 15 | Degraded |
| 50 | 10 | 30 | 6 | Degraded (`floor(10×50/100) = 5`; `6 > 5`) |
| 50 | 10 | 30 | 5 | Critical |
| 50 | 10 | 30 | 1 | Critical |
| 50 | 10 | 30 | 0 | Destroyed |
| 3 | 10 | 30 | 1 | Critical (`max(1, floor(0.3)) = 1`, both thresholds floor to 1; tie → Critical wins per ordering) |
| 3 | 10 | 30 | 2 | Healthy (`2 > 1 = DegradedThreshold`) |

**Validation gate (added to §3.5).** Asset-load validation must enforce:

```
CriticalThresholdPct ∈ [1, DegradedThresholdPct - 1]
```

A chassis or part with `CriticalThresholdPct ≥ DegradedThresholdPct` is a
**hard failure** — load is rejected, asset is not playable. The strict-less
relationship guarantees `CriticalThreshold ≤ DegradedThreshold - 1` whenever
`MaxHp ≥ 2`; the small-`MaxHp` collapse case (both thresholds floor to 1) is
handled by the predicate's ordering and is allowed.

This validation rule lives in §3.5's non-negotiable gate table (closed
2026-05-19 — R1 blocker #5).

**Determinism.** Pure integer arithmetic and integer comparison. No RNG, no
floating-point in the predicate path. Save/load round-trips the state
correctly because state is a pure function of persisted `CurrentHp`,
`MaxHp`, and asset-resolved thresholds.

**Phase classification.** State derivation runs inside Phase 1 (state
mutation) per §4.1, immediately after `CurrentHp` is committed. State
transition events (`OnSlotDamageStateChanged`) are published in Phase 2,
not Phase 1.

**Consumers.**

- Mechanics doc — soft-disable lifecycle gates on `state == Critical`.
- HUD (§4.3 phase 2b subscribers) — renders Critical visual state.
- Audio System (§4.4) — uses `state == Critical` as severity input.
- Save/load — persists `slot.CurrentHp` only; state is derived on load.

**Cross-references.**

- §3.5 — validation gate for `CriticalThresholdPct ∈ [1, DegradedThresholdPct - 1]`.
- §4.1 — phase classification of state derivation.
- §5.1 — F-VP1 governs Degraded threshold (sibling formula).

## 6. Edge Cases

Architecture-level edge cases only. Each case states the explicit behavior
the contract guarantees — no "handle gracefully," no implicit fallback.
Mechanics-doc edge cases (player intent, lifecycle interactions, HUD
behavior) are out of scope for this section.

### 6.1 Zero-slot chassis

A `ChassisDefinitionSO` whose resolved slot list is empty (zero entries).

**Behavior:** asset-load validation **rejects** the chassis per §3.5. The
chassis is not addressable and cannot be referenced by a vehicle. No runtime
code path observes a zero-slot vehicle. `vehicle.Slots.Count ≥ 1` is a
post-load invariant.

### 6.2 F-VP1 collapse — `MaxHp = 1`

Both `DegradedThreshold` and `CriticalThreshold` collapse to `1` when
`MaxHp = 1` (the `max(1, …)` floor in F-VP1 and §5.4).

**Behavior:** the slot has exactly two reachable states: `Critical` at
`CurrentHp == 1` and `Destroyed` at `CurrentHp == 0`. Healthy and Degraded
are unreachable. This is **allowed** — the predicate ordering in §5.4
resolves the tie deterministically (Destroyed→Critical→Degraded→Healthy
checked in that order; `1 ≤ 1` matches Critical). Mechanics-doc tuning is
responsible for deciding whether 1-HP slots are appropriate.

### 6.3 Asset hot-reload during combat

A `ChassisDefinitionSO` or `PartDefinitionSO` is modified in the Unity
Editor while a vehicle referencing it is alive in a running combat.

**Behavior:** the architecture contract does **not** propagate the change.
Vehicle runtime state was constructed at combat-start per §3.3 and is
immutable in shape (slot count, slot identity) for the lifetime of the
combat. Mutable runtime state (CurrentHp, InstalledPart references) is owned
by the vehicle model, not the SO. Hot-reload affects only assets loaded
after the reload — already-constructed vehicles continue with their
combat-start snapshot.

Editor-only convenience: if developer reloads the scene, the next
combat-start picks up the new asset values. No live patching.

### 6.4 Save/load — slot removed from chassis between sessions

A save file references `slot.SlotId = "weapon_aux"` but the loaded
`ChassisDefinitionSO` no longer declares that slot.

**Behavior:** save/load owns the recovery policy per ADR-0004; the V&P
contract publishes a **schema-drift hook** that the persistence layer
invokes during chassis re-construction. The default policy is:

- Save entries referencing missing `SlotId` are **dropped** from the
  reconstructed vehicle.
- A `VehicleSchemaDriftWarning` is emitted (non-throwing) so the
  persistence layer can surface a user-facing "save was migrated" toast.
- The reconstructed vehicle is internally consistent (no dangling
  references).

This is a contract guarantee, not a mechanics-doc rule.

**Scrap refund on slot drop — deferred to mechanics-doc.** Whether (and how
much) Scrap is refunded to the player when a slot is dropped during schema
drift is a balance value, not an architecture guarantee. The architecture
contract guarantees only that the schema-drop event fires and the
persistence layer is notified; any refund formula is downstream.
`[REVERSE PENDING — design/gdd/vehicle-and-part-mechanics.md unwritten]`
per §7.4. Closes R1 recommendation #15 (deferred per Phase B Day 5).

### 6.5 Save/load — slot added to chassis between sessions

A save file omits `slot.SlotId = "weapon_aux"` but the loaded
`ChassisDefinitionSO` now declares it.

**Behavior:** the added slot is constructed at `CurrentHp = MaxHp` (fully
healthy) with `InstalledPart = null` (empty). No granted-card lifecycle
fires; the slot enters combat-ready state as if it were always there.

### 6.6 Subscriber throws during Phase 2 event publication

A subscriber registered via `IVehicleEventBus.Subscribe<TEvent>` throws an
exception inside its handler during Phase 2 broadcast (§4.3).

**Behavior:** the event bus **does not abort** the remaining subscribers
for the current event. The thrown exception is **caught at the bus
boundary**, wrapped in a `VehicleSubscriberException` (preserving inner
stack trace), and logged via the project's logging facility. Subsequent
subscribers in the publication order execute normally.

The throwing subscriber's partial state is its own responsibility — the
contract does not roll back its mutations. State subscribers (phase 2a) are
expected to never throw under normal operation; throws there indicate a
contract violation that should be surfaced loudly.

### 6.7 `AnchorPoint` outside unit rect

A `SlotDefinition.HudAnchor` whose `IsInUnitRect()` returns false (NaN,
infinity, or coordinate outside `[0, 1]`).

**Behavior:** asset-load validation **rejects** the chassis per §3.5. No
runtime code observes an invalid anchor. HUD layout (§4.3 phase 2b) can
trust the unit-rect invariant without re-validation.

### 6.8 `RedirectsToSlotId` — missing target

A `SlotDefinition.RedirectsToSlotId` points to a `SlotId` that does not
exist on the same chassis.

**Behavior:** asset-load validation **rejects** the chassis per §3.5. The
redirect graph is verified before the asset is admitted; no runtime
observes a dangling redirect.

### 6.9 `RedirectsToSlotId` — cycle

A chain `slot_a → slot_b → slot_a` (or any longer cycle).

**Behavior:** asset-load validation **rejects** the chassis per §3.5. The
redirect resolver runs a cycle-detection pass over the redirect graph at
load time. Validation reports the cycle path in the rejection message.

### 6.10 Recursive event publication

A subscriber's handler synchronously triggers another `IVehicleMutator`
call that would publish events of its own.

**Behavior:** **not supported**. Mutators inside event handlers throw
`VehicleReentrancyException`. The contract requires mutations to be staged
and applied between full Phase 1→2→3 cycles, not during a Phase 2
broadcast. Mechanics doc owns the "stage damage for next tick" pattern;
architecture only forbids the re-entry.

The bus guards re-entry by setting an `IsPublishing` flag during Phase 2
that the mutator interface checks before accepting any call.

**Bus-side safe-state catch (Phase A3, locked 2026-05-19).** The event
bus catches `VehicleReentrancyException` thrown by any subscriber's
mutator call **at the publication boundary**. On catch the bus:

1. Emits a `VehicleEvent.ReentrancyBlocked` telemetry log entry with
   fields `OriginatingEventName : string` (the event being broadcast
   when reentrancy was detected), `OffendingSubscriberType : System.Type`
   (the `Type` of the subscriber that called the mutator), and
   `RejectedMutatorSignature : string` (the mutator method name that
   was rejected).
2. Completes the in-flight Phase 2 broadcast for the remaining
   subscribers — the violation is treated as one subscriber failing,
   not as a frame-wide abort. Per §4.3 subscriber isolation, other
   subscribers must still receive the event.
3. Lets the frame finish in safe state. Phase 3 (death check) runs
   normally on the state produced by Phase 1. **The run continues.**
   The reentrancy violation is a developer-facing bug, not a
   player-facing game-over.

Mechanics doc owns recovery patterns (e.g., the "stage damage for next
tick" idiom referenced above); architecture only guarantees the bus does
not propagate the throw past its own catch. This policy is
roguelike-friendly: a contract bug surfaces in logs and telemetry
without weaponising itself against player progress. AC-VPA31a/b/c
(§9.6) cover the safe-state behavior: 31a asserts the bus catches and
emits telemetry, 31b asserts Phase 3 completes after the catch, and
31c asserts the run continues without aborting.

### 6.11 Empty subscriber list for a published event

An event is published with zero subscribers registered.

**Behavior:** **no error**. Publication is a no-op for the event payload
construction cost only. Phases 1 and 3 still run. The audio composition
window (§4.4) still ticks with zero entries.

### 6.12 `SafeAmplify` chained with mixed-finite inputs

A buff chain composes `SafeAmplify(SafeAmplify(base, 0f), float.NaN)`.

**Behavior:** the inner call returns `0f`. The outer call observes
`baseValue = 0f` (finite) and `multiplier = NaN` (non-finite) and
**throws** `SafeAmplifyDomainException` per §5.3. The 0-multiplier
short-circuit does **not** suppress the outer throw — once the chain
produces a NaN multiplier, the bug must surface.

See also §5.3's **post-multiply finite check**, which catches the
analogous overflow case where two finite inputs multiply to `Inf` —
e.g., a chained buff stack `SafeAmplify(SafeAmplify(huge, big), bigger)`
that would otherwise propagate `+Infinity` into downstream damage math.
The §5.3 guard and §6.12's NaN-propagation case share a common contract:
any non-finite value in the chain — input or intermediate — throws.

### 6.13 Combat-start fails mid-construction

Vehicle construction per §3.3 encounters a validation error after some
slots have been allocated (e.g. fourth slot fails a §3.5 gate).

**Behavior:** construction is **all-or-nothing**. Partial state is
discarded; the constructor throws `VehicleConstructionException`. The
caller (combat scene boot) is responsible for the failure path; no
half-constructed vehicle is ever observable.

## 7. Dependencies

Bidirectional. Each entry names the system, the direction, the specific
contract touched, and the **reverse anchor** the other document must list
back. Entries with `[REVERSE PENDING]` indicate the other doc has not yet
been updated to mention this one — those reverse links must be added before
this doc can pass `/design-review`.

### 7.1 Architecture is consumed by

These systems depend on the V&P architecture contract.

| System | Contract touched | Reverse anchor |
|---|---|---|
| `design/gdd/vehicle-and-part-mechanics.md` (sister) | `SlotDefinition` (§3.1), `AnchorPoint` (§3.2), validation gates (§3.5), phase model (§4.1), `IVehicleEventBus` (§4.3), F-VP1/2/3 (§5.1–5.3), `CriticalState` predicate (§5.4) | sister doc "Dependencies" must list this doc as the contract surface it depends on `[REVERSE PENDING — mechanics doc not yet authored]` |
| `design/gdd/card-combat-system.md` | `IVehicleMutator` for damage application; `IVehicleEventBus` Phase 2 subscriptions for combat reactions; `SafeAmplify` for buff/debuff stack composition | card-combat doc "Dependencies" must list this doc with the same three anchors `[REVERSE PENDING]` |
| `design/gdd/save-persistence.md` | schema-drift hook (§6.4–6.5); `SlotId` as persistence key; `CurrentHp` as the only persisted slot state (per §5.4 — state is derived on load) | save-persistence doc "Dependencies" must list this doc with the schema-drift hook anchor `[REVERSE PENDING]` |
| `design/ux/combat-hud.md` | `SlotDefinition.HudAnchor` unit-rect invariant (§3.2, §6.7); phase 2b UI subscriber rules (§4.3); same-frame VFX ordering (§4.2) | HUD doc must list this doc with the HudAnchor + subscriber anchors `[REVERSE PENDING — HUD doc may need anchor section]` |
| Audio System (`design/audio/` — pending) | `AudioCompositionHint` payload (§4.4); phase 2d subscriber contract (§4.3); event metadata table (§4.4) | audio system doc must list this doc with the AudioCompositionHint anchor `[REVERSE PENDING — audio doc not yet authored]` |
| `design/gdd/scrap-economy.md` | `InstalledCount` formula (F-VP2 / §5.2) — used by mechanics-doc `InstallCost` which the economy doc consumes | scrap-economy doc "Dependencies" must list this doc transitively (via mechanics doc) `[REVERSE PENDING — verify chain when scrap-economy doc revised]` |

### 7.2 Architecture depends on

These documents provide constraints, precedents, or upstream contracts that
this doc relies on.

| System | Contract touched | Reverse anchor |
|---|---|---|
| `docs/architecture/adr-0007-frame-driven-variable-slot-system.md` | This doc **gates** ADR-0007's transition to `Accepted` state. ADR-0007 provides the architectural mandate (variable slot count, frame-driven mutation, engine-free contract surface) that this doc operationalizes. | ADR-0007 "Consequences" must list this doc as the binding contract surface `[REVERSE PENDING — ADR-0007 currently Proposed]` |
| `docs/architecture/adr-0002-card-combat-state-event.md` | POCO state model precedent; engine-free assembly pattern (`noEngineReferences: true`) | ADR-0002 already mentions vehicle subsystems will follow this pattern; verify on next ADR-0002 revision |
| `docs/architecture/adr-0003-loot-rng-determinism.md` | Determinism discipline. Architecture doc declares all formulas pure-integer or IEEE-754-exact; no `UnityEngine.Random`, no time-dependent state. F-VP1/2/3 and CriticalState predicate all comply. | ADR-0003 lists vehicle subsystems as in-scope for determinism; verify on next ADR-0003 revision |
| `docs/architecture/adr-0004-save-persistence-architecture.md` | Schema-drift hook contract (§6.4–6.5) is a producer for ADR-0004's recovery chain. Per-system DTOs pattern (ADR-0004) governs how vehicle state serializes; this doc declares the slot-level DTO shape implicitly via §3.1 and §5.4. | ADR-0004 must list V&P architecture as a `SystemId` participant `[REVERSE PENDING]` |
| `docs/architecture/adr-0005-vehicle-poco-part-catalog.md` | Part catalog POCO pattern; engine-free part definitions consumed by `SlotInstance.InstalledPart` (§3.3) | ADR-0005 should list this doc as the consumer of part catalog runtime instances `[REVERSE PENDING]` |
| `docs/engine-reference/unity/VERSION.md` | Unity 6.3 LTS `[SerializeField]` fields-only constraint; `Object.FindObjectsByType` deprecation; new Input System default. Architecture doc cites the fields-only rule in §3.1 mutable struct rationale. | version-pinned engine reference; no reverse-anchor required (engine reference is a downstream snapshot, not a peer doc) |
| `.claude/docs/technical-preferences.md` | Naming conventions (PascalCase public, `_camelCase` private); forbidden patterns (no `UnityEvent` in combat; no `UnityEngine.Random` for seeded systems; no hardcoded gameplay values); ADR log (this doc resolves ADR-0001/0007 gating). | preferences doc lists ADRs the project depends on; no per-doc reverse-anchor required |
| ADR-0006 (`docs/architecture/adr-0006-card-system-data-authoring.md`) | Data-authoring pattern precedent for ScriptableObject + runtime-resolved POCO pipeline (§3.3) | ADR-0006 should list this doc as a sibling consumer of the pattern `[REVERSE PENDING]` |
| `docs/architecture/adr-0008-addressables-runtime-asset-loading.md` | Addressables runtime-resolution policy. §3.3 binds the chassis art bundle via `AssetReferenceT<ChassisArtBundle>` and references the async-only `LoadAssetAsync` semantics; ADR-0008 carries the binding rationale, memory budget rules, and the catalog-loading contract that this doc consumes at runtime. | ADR-0008 must list this doc as the first consumer of the policy `[REVERSE PENDING — ADR-0008 currently Proposed; transitions to Accepted once technical-director signs off, at which point this reverse-link entry closes]` |

### 7.3 Explicit non-dependencies

To prevent drift, the following systems are **not** dependencies of this
doc. If a future change creates a dependency, this section must be updated.

- **Map / node system** — vehicle state outlasts combat but the architecture
  contract is combat-scoped; map UI does not call `IVehicleMutator`.
- **Loot tables / RNG seeding** — F-VP1/2/3 and CriticalState are
  deterministic without RNG; loot system is a peer of mechanics doc, not
  architecture.
- **Network replication** — single-player only; no replication boundary.
- **Localization** — `CaptionKey` strings on `AudioCompositionHint` are
  identifiers, not text; localization consumes them downstream of audio
  system, not via this doc.

### 7.4 Reverse-link tracking

**Policy (R1 reaffirmation).** Every unfilled reverse-link in this doc — in
§7.1, §7.2, or in any cross-doc anchor (§3.4 UI affordance, §5.2 InstallCost,
§6.4 schema-drift refund, etc.) — MUST carry an explicit
`[REVERSE PENDING — <reason>]` annotation at the link site. Unmarked
reverse-link gaps are treated as missing dependencies and block the final
`/design-review APPROVED` verdict; explicit `[REVERSE PENDING]` markers do
not block the initial review pass.

The review log notes pending reverse links so the producer can sweep them
when the consuming docs are next touched. The sweep itself is
producer-backlog work scheduled post-R2 (per R1 recommendation #17
advisory-status close); this doc does not gate on the sweep completing.

## 8. Tuning Knobs

Architecture-level knobs only. These control **contract shape** — the
boundaries within which valid assets are admitted and the policies the
contract surface follows. Gameplay-feel values (damage numbers, energy
costs, card pool sizes, chassis archetypes) are mechanics-doc territory.

Each knob lists its type, safe range, default, what it affects, and notes.
A separate subsection at the end enumerates **contract-fixed** values that
look like knobs but are not — changing them is a contract change, not a
tuning pass.

### 8.1 Threshold range knobs

These knobs bound the valid range of per-asset threshold percentages.
Per-asset values still live on the `ChassisDefinitionSO` / `PartDefinitionSO`
— this section sets the **outer envelope** that asset-load validation
enforces.

| Knob | Type | Safe Range | Default | Affects |
|---|---|---|---|---|
| `DegradedThresholdPct` valid range | `int` interval | `[1, 99]` | `[1, 99]` (hard-coded in §3.5 validator) | F-VP1; rejects assets with `pct ∈ {0, 100, negative, > 100}` |
| `CriticalThresholdPct` valid range | `int` interval | `[1, DegradedThresholdPct - 1]` | computed per-asset | §5.4 predicate; rejects assets where Critical ≥ Degraded |
| `DefaultSlotMaxHp` minimum | `int` | `> 0` | `1` (non-negotiable) | F-VP1, F-VP2; rejects `MaxHp ≤ 0` at load |

**Tuning guidance.** Narrowing the `DegradedThresholdPct` range (e.g. to
`[10, 90]`) is a balance-stabilizing move that prevents pathological asset
authoring. Widening it would require contract-level review.

### 8.2 Capacity knobs

These knobs bound runtime data structures.

| Knob | Type | Safe Range | Default | Affects |
|---|---|---|---|---|
| `MaxSlotsPerChassis` | `int` | `[1, 16]` (recommended); `[1, 32]` (hard limit) | `8` | F-VP2 upper bound; validation gate on chassis load; HUD layout assumptions |
| `MaxSubscribersPerEvent` | `int` | `[1, 64]` | `16` | `IVehicleEventBus` capacity; throws `VehicleSubscriberOverflowException` on excess |
| `AudioCompositionWindowTicks` | `int` | `[1, 4]` (single-digit ticks per Phase 2 window) | `1` | §4.4 composition window length; longer windows allow more audio batching but delay critical cues |

**Tuning guidance.** `MaxSlotsPerChassis = 8` matches the design target for
the largest archetype. The hard limit at `32` exists to bound runtime memory
and HUD layout; exceeding it is a contract change. `MaxSubscribersPerEvent
= 16` is generous for the current subscriber set (state model, HUD, VFX
controller, audio system, save snapshot — 5 to 8 active subscribers).

### 8.3 Validation policy knobs

These knobs control how strictly asset-load validation behaves. All defaults
are **strict** (throw on any violation); permissive modes exist only for
designer iteration loops.

| Knob | Type | Safe Range | Default | Affects |
|---|---|---|---|---|
| `AssetValidationMode` | enum `{ Strict, WarnOnly, Disabled }` | any | `Strict` (build) / `Strict` (editor) | §3.5 gate behavior; `WarnOnly` logs and admits; `Disabled` skips validation entirely |
| `RedirectCycleDetection` | `bool` | any | `true` | §6.9 cycle detection during chassis load |
| `SchemaDriftWarningPolicy` | enum `{ Throw, Warn, Silent }` | any | `Warn` | §6.4 schema-drift hook emission policy |

**Tuning guidance.** **Never ship with `AssetValidationMode = Disabled`** —
the build CI must assert `Strict` for release configs. The `WarnOnly` mode
exists for designer iteration when a chassis is mid-authoring and
intentionally fails a gate. `SchemaDriftWarningPolicy = Throw` is useful
for catching saves-from-future-version bugs in development; ship with
`Warn`.

### 8.4 Save/load policy knobs

These knobs control behavior at the persistence boundary (§6.4–6.5).

| Knob | Type | Safe Range | Default | Affects |
|---|---|---|---|---|
| `MissingSlotRecoveryPolicy` | enum `{ Drop, Abort }` | any | `Drop` | §6.4; `Drop` removes the orphan entry, `Abort` rejects the save load |
| `AddedSlotInitialState` | enum `{ HealthyEmpty, DegradedEmpty }` | any | `HealthyEmpty` | §6.5; controls how a newly-declared slot enters a loaded vehicle |
| `MaxDeckSize` runtime ceiling | `int` | `[16, 256]` | `64` (carry-forward from R6 close) | save/load DTO capacity; no design cap (per mechanics doc) — this knob is purely a deserialization buffer guard |

**Tuning guidance.** `MissingSlotRecoveryPolicy = Drop` is the user-friendly
default — it preserves the player's progress when chassis assets change
between patches. `Abort` is appropriate only if a save is suspected
corrupted. `AddedSlotInitialState = HealthyEmpty` means players who load an
old save into a new chassis version get the new slot for free; switching to
`DegradedEmpty` would penalize them and is not recommended.

### 8.5 Contract-fixed values (NOT knobs)

These look like tunable parameters but are **part of the contract**.
Changing them requires a contract revision, not a tuning pass. They are
listed here so designers don't mistake them for tuning surface.

| Value | Fixed at | Reason it is not a knob |
|---|---|---|
| `AnchorPoint` unit rect | `[0, 1] × [0, 1]` | HUD layout coordinate convention — changing breaks all anchor data |
| F-VP1 floor | `max(1, …)` | Guarantees `DegradedThreshold ≥ 1`; changing breaks §5.4 predicate ordering |
| F-VP2 clamp behavior | `[0, vehicle.Slots.Count]` | Caller contract — consumers rely on bounded integer (§5.2) |
| F-VP3 zero-preservation | `multiplier == 0 → output == 0` | Closes R7 systems-designer "silent rewrite" finding; reverting reintroduces the bug |
| Phase 1 / 2 / 3 ordering | state → events → death | §4.1 — same-frame ordering depends on this; reordering breaks every same-frame correctness proof |
| Subscriber phase order | 2a → 2b → 2c → 2d | §4.3 — state subscribers must run before observers |
| `VehicleContractException` base | `System.Exception` derivative | Integration-boundary catch policy (§3.4) |
| Asset-load atomicity | all-or-nothing | §6.13 — no half-constructed vehicles observable |

### 8.6 Knob change protocol

Any change to §8.1–8.4 values is a tuning change and can be made without
contract revision. Any change to §8.5 is a **contract revision** and
requires:

1. ADR update or new ADR entry.
2. `/design-review` re-pass on this doc.
3. Mechanics doc revision pass (most §8.5 changes affect downstream
   assumptions).
4. Migration plan for existing assets and saves.

## 9. Acceptance Criteria

Each AC is independently testable by a QA tester (or an automated test
harness) following an explicit setup → action → expected-result pattern.
ACs are scoped to the architecture contract surface; mechanics-doc ACs live
in the sister doc.

ACs are grouped by the R7 top-5 blocker they close, with additional general
coverage at the end. Each AC includes the §-reference to the rule it
verifies.

### 9.0 Validation verbs (glossary)

The verbs below are used across §9 ACs and across all §3.5 / §5.x /
§6.x rule statements. They have **single binding meanings** — readers
must not interpret "rejects" as "logs" or "throws" as "rejects".

- **rejects** — **load-time fail**. The asset never enters runtime
  state. Manifests as a named exception
  (`ChassisValidationException`, `SlotDefinitionValidationException`)
  thrown from the `AssetPostprocessor` import pass. The asset is
  unusable until the author fixes the source data.
- **throws** — **runtime fail**. The API call fails with a named
  exception. The caller MUST handle or propagate; the bus does not
  swallow the throw (except per §6.10 reentrancy safe-state catch and
  §4.3 / §6.6 subscriber isolation, both explicitly carved out).
- **logs** — **non-fatal observability**. Writes to the logger and/or
  emits a `VehicleEvent.*` telemetry record on the bus. The operation
  still completes. Production builds may downgrade log to
  telemetry-only; debug builds always log.
- **no-op** — **silent completion**. Returns without state change,
  without log, without telemetry. The caller cannot distinguish
  between "did the work" and "had nothing to do."

**Named-exception catalog.** All derive from `VehicleContractException`.

| Exception | Raised by | Reference |
|-----------|-----------|-----------|
| `ChassisValidationException` | asset postprocessor / validator | §3.5 |
| `SlotDefinitionValidationException` | asset postprocessor / validator | §3.5 |
| `SlotNotPlayableException` | `IVehicleMutator.InstallPart` / `ScrapSlot` on `IsPlayable == false` slot | §3.4 |
| `StructuralSlotException` | `IVehicleMutator.ScrapSlot` on `IsStructural == true` slot | §3.4 |
| `SlotAlreadyOccupiedException` | `IVehicleMutator.InstallPart` on already-occupied slot | §5.2 |
| `VehicleReentrancyException` | mutator call during Phase 2 broadcast | §6.10 |
| `LayoutNotValidatedThisSessionException` | construction rejected because `FrameLayoutSO.IsValidated == false` in this editor session — editor only; reimport the layout asset to resolve (see §3.3 step 1) | §3.3 step 1 |
| `VehicleConstructionException` | mid-construction validation failure (data-corrupt; distinct from session-flag miss) | §6.13 |
| `SafeAmplifyDomainException` | `SafeAmplify` on non-finite input or post-multiply non-finite result | §5.3, §6.12 |
| `VehicleSubscriberException` | bus wrapper around subscriber throws | §4.3, §6.6 |

### 9.1 SlotDefinition single canonical declaration (R7 #1)

**AC-VPA01 — SlotDefinition declared exactly once.** A CI grep over the
repo for `struct SlotDefinition` matches **exactly one** declaration, in
the V&P architecture-owned namespace. Source: §3.1. Tooling: ripgrep CI
check `rg "struct SlotDefinition" --type cs | wc -l == 1`.

**AC-VPA02a — `SlotDefinition` POCO carries zero Unity attributes.**
Reflection over the declared `WastelandRun.Vehicle.SlotDefinition` type
returns: type kind = `ValueType`, all instance members are fields (no
properties), and **zero fields carry any `UnityEngine.*` attribute**
(including `[SerializeField]`). The type lives in the engine-free
assembly (`noEngineReferences: true`). Source: §3.1 Phase A1 POCO + DTO
split.

**AC-VPA02b — `SlotDefinitionAuthoring` DTO carries `[SerializeField]`
on every authored field.** Reflection over the Unity-side
`SlotDefinitionAuthoring` type returns: type kind = `ValueType`
(§3.1 declares it as `struct`), all instance members are fields (no
properties), and **every authored field carries `[SerializeField]`**
(Unity 6.3 fields-only rule). A `ToPoco()` converter exists that
produces a `SlotDefinition` POCO. Source: §3.1.

**AC-VPA03a — `SlotDefinition` POCO includes `IsPlayable` field.**
Reflection over the engine-free `SlotDefinition` POCO returns a field
named `IsPlayable` of type `bool`. The field carries **no Unity
attribute** (consistent with AC-VPA02a). Source: §3.1, §3.4.

**AC-VPA03b — `SlotDefinitionAuthoring` DTO surfaces `IsPlayable`
with `[SerializeField]`.** Reflection over the Unity-side
`SlotDefinitionAuthoring` returns a field named `IsPlayable` of type
`bool` with `[SerializeField]`. The `ToPoco()` converter copies
this value verbatim into the POCO. Source: §3.1, §3.4.

**AC-VPA04 — AnchorPoint engine-free.** The assembly containing
`AnchorPoint` has `noEngineReferences: true` in its `.asmdef`. The
`AnchorPoint` type contains no reference to `UnityEngine.Vector2` or any
`UnityEngine.*` type. Source: §3.2.

### 9.2 Frame-ordering convention (R7 #2)

**AC-VPA05 — Phase 1/2/3 ordering observable.** Given a vehicle with two
slots — a plate slot at layout index 0 and a hull slot at layout index 1
— applying damage that drops both to 0 in the same frame produces the
following observable sequence: (1) Phase 1 completes with all `CurrentHp`
mutations committed and no events yet fired; (2) Phase 2 broadcasts all
events in layout-index order — plate slot events fire before hull slot
events; (3) Phase 3 (death check) runs after all Phase 2 callbacks
complete. No event fires before all Phase 1 mutations are committed.
Source: §4.1.

**AC-VPA06 — VFX layer order = Phase 2 commit order.** Subscribing a phase
2c (VFX) handler to `OnArmorExposed` and `OnPlatingChanged` for a
same-frame Plate→0 + Hull→0 event reveals callbacks in **layout-index
order**, not subscription order. Source: §4.2.

**AC-VPA07 — Subscriber phase order observable.** Subscribing one handler
at each of `SubscriberPhase.State / UI / VFX / Audio` for the same event
type and triggering one event reveals callbacks in `2a → 2b → 2c → 2d`
order. Source: §4.3.

**AC-VPA08 — Audio composition window publishes hints, not policy.**
- **Setup:** register one subscriber `S` at `SubscriberPhase.Audio` for
  `OnSlotDamaged`. Configure a single-slot vehicle with `CurrentHp = 5,
  MaxHp = 10`.
- **Action:** call `IVehicleMutator.ApplyDamage(slot, 3)`.
- **Expected:** `S` receives exactly one `AudioCompositionHint` payload.
  The payload's `Severity`, `MutualExclusionGroup`, `TickSequenceIndex`,
  and `CaptionKey` fields are populated (non-null, non-default). No
  `AudioSource.Play` call and no mixer routing is invoked from anywhere
  in the V&P architecture assembly (verified by a static-analysis grep:
  zero `UnityEngine.Audio*` references in the asmdef-bounded code).

Source: §4.4.

**AC-VPA09 — No-abort-on-throw at Phase 2 boundary.** Given three
subscribers to `OnSlotDamaged` where subscriber #2 throws an
`InvalidOperationException` with message `"S2 throw"`, subscribers #1
and #3 still execute. Exactly one `VehicleSubscriberException` is
logged, and the logged exception satisfies
`caughtException.InnerException is InvalidOperationException` **and**
`caughtException.InnerException.Message == "S2 throw"` — i.e., the
original throw is preserved as the inner exception, not silently
collapsed to a string. Source: §4.3, §6.6.

### 9.3 IsPlayable on slot model (R7 #3)

**AC-VPA10 — IsPlayable orthogonal to IsStructural.** Constructing each of
the four `(IsPlayable, IsStructural)` combinations succeeds at validation
and produces a slot whose `IsPlayable` and `IsStructural` properties match
the input. Source: §3.4 truth table.

**AC-VPA11 — SlotNotPlayableException on disabled-slot mutation.** Calling
`IVehicleMutator.InstallPart(slot)` on a slot with `IsPlayable == false`
throws `SlotNotPlayableException`. The exception derives from
`VehicleContractException`. Source: §3.4.

**AC-VPA12 — IsPlayable false does not break damage path.** A slot with
`IsPlayable == false` still accepts damage via `IVehicleMutator.ApplyDamage`
and updates `CurrentHp` per §4.1. Disabled means not-installable, not
not-targetable. Source: §3.4.

**AC-VPA13 — IsPlayable persisted across save/load.** Setting `IsPlayable`
on a slot, saving, and reloading produces a slot with the same
`IsPlayable` value. Source: §3.1, §6.4–6.5.

### 9.4 Formula boundary domains (R7 #4)

**AC-VPA14 — F-VP1 floor at small `MaxHp`.** For `MaxHp = 3,
DegradedThresholdPct = 30`, `DegradedThreshold == 1` (not 0). Source: §5.1.

**AC-VPA15 — F-VP1 boundary at `pct = 1`.** For `MaxHp = 100,
DegradedThresholdPct = 1`, `DegradedThreshold == 1`. Source: §5.1.

**AC-VPA16 — F-VP1 boundary at `pct = 99`.** For `MaxHp = 100,
DegradedThresholdPct = 99`, `DegradedThreshold == 99`. Source: §5.1.

**AC-VPA17a — F-VP2 clamp upper bound.** Constructing a synthetic vehicle
where `count(installed) == 5` but `vehicle.Slots.Count == 4` yields
`InstalledCount == 4`. The clamp emits one
`VehicleEvent.InstalledCountClamped` telemetry record carrying
`(observedCount = 5, clampedValue = 4)`. Source: §5.2.

**AC-VPA17b — F-VP2 zero-installed lower bound + uninstall idempotency.**
Constructing a vehicle with 4 slots and zero installed parts yields
`InstalledCount == 0`. Calling
`IVehicleMutator.UninstallPart(emptySlotId)` on any already-empty slot
is a **no-op** per the §5.2 idempotency contract: `InstalledCount`
remains `0` and no `OnPartUninstalled` event fires. Source: §5.2
upstream idempotency requirement.

**AC-VPA18 — F-VP2 does not filter on IsPlayable.** For a vehicle with 2
installed parts where one slot has `IsPlayable == false`, `InstalledCount
== 2`. Source: §5.2.

**AC-VPA19 — F-VP3 zero preservation.** `SafeAmplify(10f, 0f) == 0f`
exactly (bit-equal, not "approximately"). `SafeAmplify(10f, -0f) == 0f`.
Source: §5.3.

**AC-VPA20a — F-VP3 NaN base rejection.** `SafeAmplify(float.NaN, 2f)`
throws `SafeAmplifyDomainException`. The exception message identifies
the offending input as the **base value**. Source: §5.3.

**AC-VPA20b — F-VP3 NaN multiplier rejection.** `SafeAmplify(10f,
float.NaN)` throws `SafeAmplifyDomainException`. The exception message
identifies the offending input as the **multiplier**. Source: §5.3.

**AC-VPA20c — F-VP3 infinity rejection (input).** Both
`SafeAmplify(10f, float.PositiveInfinity)` and
`SafeAmplify(float.NegativeInfinity, 2f)` throw
`SafeAmplifyDomainException`. Source: §5.3.

**AC-VPA20d — F-VP3 post-multiply overflow rejection.**
`SafeAmplify(3.0e38f, 100f)` (finite × finite → `+Infinity` per IEEE
754 single-precision overflow) throws `SafeAmplifyDomainException`.
The post-multiply finite check fires; no `+Infinity` is observable to
the caller. This case is distinct from AC-VPA20a–c because both inputs
are finite — the overflow happens **inside** the multiplication.
Source: §5.3 post-multiply finite check, §6.12.

**AC-VPA21 — F-VP3 negative-multiplier rejection.** `SafeAmplify(10f,
-1f)` throws `SafeAmplifyDomainException` with message naming the negative
multiplier. Source: §5.3.

**AC-VPA22 — CriticalState predicate ordering.** For a slot with `MaxHp =
3, CriticalThresholdPct = 10, DegradedThresholdPct = 30`, both thresholds
collapse to 1. Setting `CurrentHp = 1` yields `state == Critical` (not
Degraded). Source: §5.4.

**AC-VPA22b — `MaxHp = 1` threshold collapse.** For a slot with
`MaxHp = 1, CriticalThresholdPct = 10, DegradedThresholdPct = 30`,
both `CriticalThreshold` (F-VP1 applied to Critical) and
`DegradedThreshold` floor to 1, exactly equal to `MaxHp`. The
predicate ordering rule (Destroyed → Critical → Degraded → Healthy)
resolves the collapse:
- `CurrentHp = 1` ⇒ `state == Critical` (Critical predicate fires
  first; Healthy band is empty by construction).
- `CurrentHp = 0` ⇒ `state == Destroyed`.
There is no value of `CurrentHp` in `[0, MaxHp]` that produces
`Healthy` at `MaxHp = 1` — the design accepts this as a boundary
behaviour, not a bug. Source: §3.5 small-`MaxHp` collapse provenance,
§5.4 predicate ordering, §6.2.

**AC-VPA23 — CriticalState save/load round-trip.** Setting `CurrentHp` on
a slot, saving, reloading, and querying `GetSlotDamageState(slot)` returns
the same state value. State is derived from persisted `CurrentHp`. Source:
§5.4.

### 9.5 Cross-document vocabulary & validation (R7 #5)

The R1 single-AC bundling (one AC covering nine gates) is split into
nine independently testable ACs (AC-VPA24a..i). Each gate fails or
passes on its own; a QA tester records a separate verdict per gate.
All exceptions are **rejects**-class per §9.0 (load-time fail).

**AC-VPA24a — MaxHp > 0 gate.** Loading a `ChassisDefinitionSO` with
`DefaultSlotMaxHp = 0` rejects the asset with
`ChassisValidationException("MaxHp > 0")`. Source: §3.5.

**AC-VPA24b — DegradedThresholdPct lower bound.** Loading a
`ChassisDefinitionSO` with `DegradedThresholdPct = 0` rejects with
`ChassisValidationException("pct ∈ [1,99]")`. Source: §3.5.

**AC-VPA24c — DegradedThresholdPct upper bound.** Loading a
`ChassisDefinitionSO` with `DegradedThresholdPct = 100` rejects with
`ChassisValidationException("pct ∈ [1,99]")` (same exception class
and message as 24b — the gate is "∈ [1,99]" not "< 100"). Source:
§3.5.

**AC-VPA24d — Critical < Degraded gate (Day 2 add).** Loading a
`ChassisDefinitionSO` with `CriticalThresholdPct >= DegradedThresholdPct`
rejects with
`ChassisValidationException("Critical < Degraded")`. The specific case
`CriticalThresholdPct = DegradedThresholdPct` (equal, not greater)
also rejects. Source: §3.5 (Day 2 third non-negotiable gate).

**AC-VPA24e — Non-empty slot list gate.** Loading a
`ChassisDefinitionSO` with `Slots.Count == 0` rejects with
`ChassisValidationException("non-empty slot list")`. Source: §3.5,
§6.1.

**AC-VPA24f — Anchor unit-rect gate.** Loading a chassis whose
`AnchorPoint` has `X = 1.5f, Y = 0.5f` (or any component outside
`[0, 1]`) rejects with
`ChassisValidationException("anchor in unit rect")`. Source: §3.5.

**AC-VPA24g — Anchor NaN rejection.** Loading a chassis whose
`AnchorPoint` has `X = float.NaN` (or `Y = float.NaN`, or any
`±Infinity` component) rejects with
`ChassisValidationException("anchor in unit rect")` — same exception
class as 24f. Source: §3.5.

**AC-VPA24h — Redirect target exists.** Loading a chassis where a
`SlotDefinition` has `RedirectsToSlotId = "missing_slot"` and no slot
with that id exists in the same chassis rejects with
`ChassisValidationException("redirect target exists")`. Source: §3.5,
§6.9.

**AC-VPA24i — Redirect graph acyclic.** Loading a chassis where two
slots redirect to each other (`A → B → A`) rejects with
`ChassisValidationException("redirect graph acyclic")`. The exception
message includes the cycle path as a slot-id sequence (e.g.,
`"cycle: A → B → A"`). Longer cycles (3+ nodes) also reject and the
message names the full cycle path. Source: §3.5, §6.9.

**AC-VPA25 — F-VP2 `InstalledCount` consumed by mechanics-doc
`InstallCost`.** A static cross-reference check confirms that
`design/gdd/vehicle-and-part-mechanics.md` `InstallCost` formula
references `InstalledCount` (F-VP2 / §5.2) as its scaling input.

**Status: deferred.** This AC becomes verifiable only when the
mechanics doc is authored. Until then it is non-blocking: a focused
re-review must skip AC-VPA25 rather than fail it. Tracking: review log
R1 recommendation **#12** (Day 5 close of BLOCK-2 language; full
verification when mechanics doc passes its own R1).

**AC-VPA26 — Reverse-link audit.** Each entry in §7.1 (consumers) and §7.2
(providers) has its reverse anchor in the named target doc — OR is
explicitly marked `[REVERSE PENDING]` with a tracking entry in the review
log. No silent omissions. Source: §7.4.

**Status: advisory.** The reverse-link sweep across sister docs is
producer-backlog work per §7.4 policy, not a gate on this doc's R2
APPROVED verdict. AC-VPA26 verifies the **policy compliance** (every
unfilled link is explicitly marked, not silently omitted), not the
**completion** of the sweep itself. Tracking: review log R1
recommendation **#17** (post-R2 sweep ticket).

### 9.6 Edge case coverage (§6)

**AC-VPA27 — Zero-slot chassis rejected.** A `ChassisDefinitionSO` with
zero slots fails load with `ChassisValidationException`. No partial state
admitted. Source: §6.1.

**AC-VPA28 — Asset hot-reload does not propagate.** Modifying a
`ChassisDefinitionSO` field at runtime while a vehicle referencing it is
alive does not change the live vehicle's slot count or threshold values.
Source: §6.3.

**AC-VPA29 — Save/load drop policy on missing slot.** Saving a vehicle
with slot `weapon_aux`, removing that slot from the chassis asset, and
reloading produces a vehicle without `weapon_aux` and emits a
`VehicleSchemaDriftWarning`. The reloaded vehicle is internally consistent
(no dangling references). Source: §6.4.

**AC-VPA30 — Save/load handles added slot.** Saving a vehicle without slot
`weapon_aux`, adding that slot to the chassis asset, and reloading
produces a vehicle with `weapon_aux` at `CurrentHp == MaxHp`,
`InstalledPart == null`. No granted-card lifecycle fires. Source: §6.5.

**AC-VPA31a — Recursive event publication blocked + telemetry.**
- **Setup:** a single-slot vehicle (`CurrentHp = 5, MaxHp = 10,
  IsStructural = true`). Three subscribers registered to `OnSlotDamaged`:
  subscriber #1 and #3 are well-behaved; subscriber #2 calls
  `IVehicleMutator.ApplyDamage(slot, 1)` synchronously during its Phase 2
  handler. Vehicle `StructuralHp > 0` at time of reentry (reentrant damage
  would not kill the vehicle if applied).
- **Action:** call `IVehicleMutator.ApplyDamage(slot, 3)` to trigger the
  Phase 2 broadcast.
- **Expected:** the bus detects the reentrancy at subscriber #2's mutator
  call. **Exactly one** `VehicleEvent.ReentrancyBlocked` telemetry record
  is emitted. The record's `OffendingSubscriberType` field equals
  `typeof(SubscriberTwo)` (the concrete `System.Type` of subscriber #2);
  `OriginatingEventName` field equals the registered name of the
  `OnSlotDamaged` event. Source: §6.10 Phase A3 (telemetry emission
  guarantee).

**AC-VPA31b — Reentrant damage has no effect; Phase 3 runs on
Phase-1-only state.**
- **Setup (independent — do not chain from AC-VPA31a):** a single-slot
  vehicle (`CurrentHp = 8, MaxHp = 10, IsStructural = true`). Three
  subscribers to `OnSlotDamaged`: #1 and #3 are well-behaved; #2 calls
  `IVehicleMutator.ApplyDamage(slot, 1)` synchronously during its Phase 2
  handler. Record `hpBeforeAction = slot.CurrentHp` (= 8) before the
  action.
- **Action:** call `IVehicleMutator.ApplyDamage(slot, 3)` (Phase 1
  commits `CurrentHp = 5`; Phase 2 fires, #2's reentrant call is caught
  by the bus and swallowed).
- **Expected (three assertions):**
  1. Subscriber #3 still executes — reentrancy catch does not abort the
     remaining Phase 2 subscriber sequence.
  2. After Phase 2 completes, `slot.CurrentHp == 5` — the reentrant
     `ApplyDamage(slot, 1)` was swallowed and had **zero effect** on slot
     Hp. `CurrentHp` reflects Phase 1 mutations only (not
     `hpBeforeAction - 3 - 1 = 4`).
  3. Phase 3 (death check) runs: `GetSlotDamageState(slot)` returns a
     state consistent with `CurrentHp = 5, MaxHp = 10` (Healthy or
     Degraded per layout thresholds — not Destroyed). `OnVehicleDied`
     is **not** fired (vehicle is not dead). Vehicle is in a fully
     consistent post-frame state.
Source: §6.10 Phase A3 (Phase 3 completion guarantee).

**AC-VPA31c — Run continues after reentrancy catch.** Given the same
setup as AC-VPA31a/b, no exception escapes the bus boundary and the
next simulation tick proceeds normally. The run is not terminated, no
"abort to main menu" or "kick to title" path is taken — the Phase A3
"finish frame in safe state, run continues" contract holds. Source:
§6.10 Phase A3 (run-continuation guarantee).

**AC-VPA32 — Empty subscriber list is a no-op.** Publishing
`OnSlotDamaged` with zero registered subscribers completes without error
and does not skip Phase 1 or Phase 3. Source: §6.11.

**AC-VPA33 — All-or-nothing vehicle construction.** Forcing a §3.5
validation failure on the fourth slot of a chassis during construction
throws `VehicleConstructionException` and produces no partially-allocated
vehicle observable from outside. Source: §6.13.

> **Note — §6.7/6.8/6.9 schema coverage cross-reference.** Edge cases
> §6.7 (HudAnchor out-of-rect), §6.8 (redirect target missing), and
> §6.9 (redirect cycle) do not have AC entries in §9.6 because their
> coverage lives in §9.5 as **AC-VPA24f, AC-VPA24g** (anchor + NaN
> rejection), **AC-VPA24h** (redirect target exists), and
> **AC-VPA24i** (redirect graph acyclic). The Phase B AC split lifted
> these from a single bundled AC into per-gate ACs; the §6 → §9
> coverage chain remains complete, just routed through §9.5 instead
> of §9.6.

### 9.7 Determinism

The R1 single-AC bundling (one AC covering four formulas) is split
into four per-formula ACs. Each formula's determinism is verified
independently so a single failing formula does not mask the others.

**AC-VPA34a — F-VP1 (DegradedThreshold) determinism.** Running
`F-VP1(MaxHp = 100, DegradedThresholdPct = 30)` twice yields bit-equal
`DegradedThreshold == 30` (integer). Pure integer arithmetic; no
platform sensitivity. Source: §5.1.

**AC-VPA34b — F-VP2 (InstalledCount) determinism.** Running
`F-VP2(vehicle)` twice on the same vehicle state yields bit-equal
`InstalledCount`. Pure integer arithmetic, O(n) iteration in fixed
order; no platform sensitivity. Source: §5.2.

**AC-VPA34c — F-VP3 (SafeAmplify) determinism.** Running
`SafeAmplify(2.5f, 1.25f)` twice yields bit-equal `Single` output
(IEEE 754 single-precision; bit pattern compared, not approximate
equality). **Platform scope: IL2CPP x64 only.** ARM64 is not yet
validated and AC-VPA34c is **ADVISORY** on ARM64 builds until the
platform-specific harness is added per §5.3. **Mono Editor Play Mode
is also excluded** from this AC — the Mono JIT does not guarantee the
same IEEE 754 rounding contract as IL2CPP x64, and the harness only
runs against IL2CPP builds; in-Editor Play Mode tests of this formula
are advisory, not gating. **CI enforcement:** the gating CI run must
target `StandaloneWindows64` with `IL2CPP` backend; a build-target
assertion step must fail the CI run if the backend is not IL2CPP x64.
Test results from a Mono-backend runner (e.g. `game-ci/unity-test-runner`
in Editor mode) for this AC are recorded as advisory only and do not
block merge. Source: §5.3.

**AC-VPA34d — §5.4 state-predicate determinism.** Running
`GetSlotDamageState(slot)` twice on a slot with `MaxHp = 100,
CurrentHp = 25, DegradedThresholdPct = 30, CriticalThresholdPct = 10`
yields the same enum value (`Degraded`) on both calls. Integer-only
predicate; no platform sensitivity. Source: §5.4.

**AC-VPA35 — No engine-time dependency in contract surface.** A static
analysis (CI grep) confirms that the V&P architecture assembly contains no
references to `UnityEngine.Time`, `System.DateTime`, `System.Random`,
`UnityEngine.Random`, or `Stopwatch`. **Scope bound:** the grep is
bounded by the production `.asmdef` glob —
`src/runtime/**/WastelandRun.Vehicle*.asmdef` and the assemblies it
transitively closes over. Test assemblies (`tests/**/*.asmdef`) and
editor-only assemblies (any asmdef with `"includePlatforms":
["Editor"]`) are explicitly excluded; test code is permitted to
reference `System.DateTime` and `Stopwatch` for harness timing. Source:
§3.1 engine-free assembly; ADR-0003 determinism discipline.

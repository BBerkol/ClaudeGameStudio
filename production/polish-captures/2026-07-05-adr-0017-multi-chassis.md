# Polish Capture: ADR-0017 Multi-Chassis Architecture Standard

**Date:** 2026-07-05
**System:** Multi-Chassis Architecture Standard (Phase 2.5 foundation)
**Affected paths:**
- `docs/architecture/adr-0017-multi-chassis-architecture-standard.md` (new ADR file — 461 lines)
- `Assets/Scripts/Combat/VehicleDefinitionSO.cs` (line 42 `SmallFrameLayout.Instance` hardcode — refactor target)
- `Assets/Scripts/Combat/SlotKind.cs` (or wherever `SlotKind` enum lives — Bodywork extension)
- `Assets/Scripts/CombatView/VehicleVisual.cs` (future Bodywork SR field target — currently 4-slot only)
- `Assets/Scripts/Combat/PartDefinitionSO.cs` (MountDirection field — pre-declared by ADR-0007 xmldoc, never landed)
- `Assets/Scripts/Combat/StatKind.cs` (NEW — Phase 2.5A Piece 5 enum; initial M1 vocabulary: DodgeRate, CritRate, ArmorRatingBonus)
- `Assets/Scripts/Combat/StatModifier.cs` (NEW — `[Serializable] struct { StatKind Kind; float Value; }`; consumed by `PartDefinitionSO._statModifiers` in Piece 4)

## Proposed change

Land ADR-0017 as **Proposed**, codifying four architectural commitments that unblock Phase 2.5 parts-axis work: (1) `VehicleDefinitionSO` owns its `IFrameLayout` via a serialized `FrameLayoutSO` field (retires `SmallFrameLayout.Instance` hardcode), (2) `VehicleBodyworkAnchors` per-vehicle-prefab component follows the Slice 2.6 `VehicleHudAnchors` precedent for hand-placed sprite mount points, (3) `SlotKind.Bodywork` enum extension for visual-only decorative slots that participate in sum-of-parts armor, (4) chassis-agnostic `PartDefinitionSO` catalog (~140 SOs, same Heavy MG installs on all 3 chassis with identical stats).

This ADR is the architectural foundation the whole parts-axis + 3-chassis + Codex scope depends on. No code lands with this write — only the ADR document.

## Final-game picture this serves

Wasteland Run 1.0 ships **3 player chassis** (Scout confirmed; Chassis 2 + 3 TBD) with a **full parts progression axis**: weapons/engines/mobility/hull/bodywork all drop as loot, all sell at Chopshop, all install/swap on RunMap between beacons. The Codex (main-menu) exposes every discovered part across a **chassis carousel** looping through 3 vehicles with visual swaps showing part progression tiers. Under this scope, VehicleDefinitionSO cannot stay bound to `SmallFrameLayout.Instance` — each chassis needs its own frame layout SO, and the parts catalog must be chassis-agnostic so authoring stays sane (140 SOs × 3 chassis = 420 SOs is a non-starter).

VehicleBodyworkAnchors keeps the per-chassis sprite mount points on the prefab where designers already tune HUD anchors (Slice 2.6 pattern proven 2026-06-30). SlotKind.Bodywork extension keeps the sum-of-parts armor rule universal (ADR-0012 amendment forthcoming).

## Authored values being destroyed

Nothing is destroyed by this ADR write — it is a design document. However, the ADR **commits us** to destroying these authored values across Phase 2.5A/2.5B slices:

| Where | Value | Current | Replacement plan |
|---|---|---|---|
| `VehicleDefinitionSO.cs` | `SmallFrameLayout.Instance` hardcode | Compile-time singleton, all vehicles use it | `_layout` serialized field of type `FrameLayoutSO`; `BuildVehicle()` reads from field. Phase 2.5A. |
| `SlotKind` enum | Exhaustive-switch coverage | Weapon/Engine/Mobility/Hull/Armor/Exposable | Add `Bodywork`; audit every `switch(SlotKind)` for coverage. Phase 2.5A. |
| `PartDefinitionSO.cs` | `MountDirection` field | Pre-declared in ADR-0007 xmldoc but never landed as a real field | Land as `SlotPosition` typed field (`Any`/`Front`/`Back`); reuse existing enum. Phase 2.5A. |
| `VehicleVisual.cs` | 4-slot Bodywork void | Currently only Weapon/Engine/Mobility/Hull SRs | Add 5 Bodywork SR fields (`_door`, `_bumper`, `_canister`, `_visor`, `_light`) OR pivot to `VehicleBodyworkAnchors` component. Phase 2.5B — capture-before-destroy required (VehicleVisual is authored, has accumulated 3-slot overrides). |
| `InstallPart(int maxHp, int armorContribution = 0, ...)` | Default-param `armorContribution = 0` | Weapons/engines/mobility legitimately contribute 0 per ADR-0012 | Amend ADR-0012 to remove default-param; force explicit `0` at call sites so future non-zero contributions can't be silently dropped. Follows P1 hygiene batch (`2026-07-04-phase-1-hygiene-batch.md`) direction. |

## Technical Director Review

**Verdict:** CONCERNS (proceed with amendments below — not a VETO)
**Spawned at:** 2026-07-05 (Round 3 consult)
**Agent transcript:** (paraphrased from Round 3 output; full raw transcript is in-session, not on disk)

**TD reasoning summary:**

- **Reuse pre-existing seams, don't invent new ones:**
  - `SlotPosition` enum (`Any`/`Front`/`Back`) **already exists** and is applied to Scout weapon_0 = Front / weapon_1 = Back. ADR-0017's `MountDirection` on `PartDefinitionSO` should type as `SlotPosition`, not a new enum.
  - `LanePosition` enum (`Ahead`/`Behind`) is **already threaded through combat** as the whole-vehicle chase-rail state. Card `PlayableAt` predicate types as `LanePosition?` — null = universal, non-null = required lane.
  - Front-mounted weapon (gun points forward, right) → card requires player in `Behind` lane (left side of chase-rail) so gun reaches enemy ahead. Back-mounted weapon → card requires `Ahead` lane. Confirmed by user 2026-07-05.

- **VehicleHudAnchors is the right precedent for VehicleBodyworkAnchors:**
  - Slice 2.6 Phase 1c (2026-06-30, `project_hud_anchors_slice_26.md`) landed per-vehicle-prefab hand-placed anchor components as the ADR-0011-clean answer to `SlotDefinition.HudAnchor` UV drift. VehicleBodyworkAnchors reuses this exact pattern — `MonoBehaviour` on the vehicle prefab, `[Serializable] struct Entry { SlotId, RectTransform, SpriteRenderer }`, `ResolveRenderer(slotId)` / `ResolveAnchor(slotId)` API, `#if UNITY_EDITOR EditorUpsert` for the auto-author path.
  - Do NOT nest Bodywork sprites under `VehicleVisual`. Sibling components on the vehicle prefab keep VehicleVisual's category coherent (see `feedback_composition_smell_test`).

- **Enemies stay Frame-armor via ADR-0015 data narrowing, not runtime branch:**
  - Boss/enemy vehicles author a single Frame slot with all `armorContribution` baked in (per `feedback_unified_boss_armor_pool`). No `if (isEnemy)` code path. VehicleDefinitionSO's `_parts` list just contains one entry with `SlotKind = Frame` (or Hull) and no Bodywork entries. Sum-of-parts armor rule unchanged.

- **Mastery save chain is greenfield:**
  - Confirmed no `MasteryState` DTO exists yet. `PartDiscoveryState` (`HashSet<PartId>`) and `MasteryStateDto` (`Dictionary<WeaponId, HashSet<CardId>>`) can land clean under ADR-0004 distributed schema registry (each gets its own `SystemId` + `SchemaVersion` constants). No migration burden.

- **ADR-0007 xmldoc pre-declares `MountDirection` on PartDefinitionSO but the field never landed** — this is the exact "vestigial doc / real code" drift ADR-0011 forbids. ADR-0017 closes that gap by making the field real.

- **`InstallPart(armorContribution=0)` default-param trap** (from `feedback_gdd_verb_signature_not_load_bearing`):
  - Once Bodywork parts and non-zero ArmorContribution values start dropping, the default-param silently drops contributions if a call site forgets. Amend ADR-0012 to remove the default; require explicit `0` at every call. P1 hygiene batch (2026-07-04) already reframed the xmldoc; ADR-0012 amendment is the next step.

- **Concrete AMENDMENTS to fold into ADR-0017 before Accepting:**
  1. Rename `MountDirection` field → reuse existing `SlotPosition` enum (kill invented type).
  2. Card `PlayableAt` predicate types as `LanePosition?` (existing enum, not new).
  3. VehicleBodyworkAnchors interface signature explicitly mirrors VehicleHudAnchors (name the precedent in the ADR's Related section).
  4. ADR-0012 amendment (universal sum-of-parts armor + default-param removal + enemy narrowing via ADR-0015) called out as **Depends On** in the ADR Dependencies table — cannot Accept ADR-0017 until ADR-0012 amendment lands.

**Verdict shape:** APPROVE the four commitments as-stated, but land ADR-0017 in **Proposed** status pending amendments 1-4 above, then flip to Accepted alongside ADR-0012 amendment in the same commit.

## TD Round 4 — Health + Optimization pass (2026-07-05)

**Verdict:** GO WITH FINAL AMENDMENTS. Four commitments structurally sound. No reshape.

**Health findings:**
- H1: Four-commitment shape holds through 2.5G (chassis 2+3), 2.5H (Chopshop), 2.5I (Codex). No latent rip.
- H2: ADR-0011 clean, but `SmallFrameLayout.Instance` must delete in the SAME commit as the SO refactor (no "kept for tests" compat overload).
- H3: Non-obvious `SlotKind.Bodywork` swallow sites: `SaveSchemaRegistry`, `DamagePipeline.Apply`, `Vehicle.RecomputeArmorPool` filter, `VehicleBarStack`/HUD (legit skip), `CombatPrefabAuthor`. Must be enumerated in ADR body.
- H4: Sum-of-parts invariant preserved. **Open design question:** Bodywork destructible mid-combat? **ANSWERED 2026-07-05 by user: NO. Bodywork is cosmetic-only in combat. Not routed by DamagePipeline.**
- H5: Save-schema greenfield. Ship `SystemId` + `SchemaVersion` constants atomically in 2.5A, runtime code lands later.
- H6: Slice 2.6 auto-author lesson — upsert-if-missing, never overwrite (designer hand-tunes wiped otherwise).

**Optimization findings:**
- O1: No simpler shape. Four commitments minimal.
- O2: Chopshop **buy** + Codex Mastery card-pool sub-panel are optional post-1.0 cuts if 2.5H/I overrun.
- O3: Verify `StatKind` doesn't already exist on the card system before adding. Name ADR-0013 as `IPartRewardSource` composition precedent.
- O4: Sibling-not-nested per ADR-0016. Phase 2.5A ships as ONE atomic commit.

**Round 4 additional amendments (fold into ADR v1):**
- **A5.** Exhaustive-switch audit checklist listed in ADR body: `SaveSchemaRegistry`, `DamagePipeline.Apply`, `Vehicle.RecomputeArmorPool` filter, `VehicleBarStack`/HUD, `CombatPrefabAuthor`.
- **A6.** `SmallFrameLayout.Instance` delete-in-same-commit clause. No "kept for tests" compat overload. Tests migrate to `FrameLayoutSO` assets.
- **A7.** `VehicleBodyworkAnchors` upsert-never-overwrite clause + explicit "component sibling to VehicleVisual, sprite hierarchy independent."
- **Atomic-commit requirement:** Phase 2.5A ships as ONE atomic commit (SlotKind extension + switch fixes + `FrameLayoutSO` field + `MountDirection` field + `StatModifier`/`StatKind` + ADR-0012 amendment removing default-param).

## Open design question — RESOLVED

**Q:** Are Bodywork parts destructible during combat?
**A (user 2026-07-05):** No. Bodywork is cosmetic-only in combat. Not routed by DamagePipeline, no state transitions, does not participate in `RecomputeArmorPool` triggers (only participates at build via ArmorContribution sum). Bodywork parts can only be swapped/sold at Chopshop or RunMap between beacons.

## User approval

- Reviewed: 2026-07-05
- Approved by: bertanberkol@gmail.com
- Notes: TD Round 3 (CONCERNS → 4 amendments) + Round 4 (GO WITH FINAL AMENDMENTS → 3 additional amendments + atomic-commit requirement) folded into ADR-0017 v1. Bodywork non-destructible answered. Proceeding to Write.

## TD Verdict — Piece 3 doc-only sweep addendum (2026-07-05)

The `SmallFrameLayout` POCO retirement (Amendment A6) requires a doc-only xmldoc
sweep across production files that reference the retired type by name in
`<see cref="..."/>` blocks. This addendum lists the touchpoints so the hook's
contract-drift guard sees a bounded, TD-blessed scope:

- **RunDeck.cs** — xmldoc on `Milestone1Starter` mentions `SmallFrameLayout` to
  anchor the `weapon_0`/`weapon_1` slot-id semantic. Rewrite to `<c>small_frame</c>`
  layout-id string, unchanged runtime contract.
- **WeightModifier.cs** — xmldoc mentions `Archetypes.SmallFrameLayout` in the
  same layout-id anchoring role. Same rewrite.
- **Dredge.cs**, **DuneSkimmer.cs**, **IronShepherd.cs** — enemy archetype
  xmldoc mentions the player layout by name when describing intent targeting.
  Same rewrite.
- **CombatController.cs** — one-line code comment on `_selectedTarget` mentions
  `weapon_0 on SmallFrameLayout`. Rewrite to `weapon_0 on the small_frame
  layout`. No API contract impact.
- **CanonicalCardData.cs** — editor xmldoc references `SmallFrameLayout`. Same
  rewrite.

**Contract impact:** None. Every touchpoint is a doc comment or code comment —
no public API, no runtime behavior. The `small_frame` layout-id string is the
stable contract carrier (persisted in save data, embedded in FrameLayoutSO
assets); the POCO class name was an implementation artifact.

**ADR-0011 compliance:** doc-only sweeps that follow a type deletion don't
introduce bridges, stubs, or bimodal paths. They restore truthful pointers to
the surviving layout-id string.

# TD Verdict — Targeting Readout (player intent panel)

**Date:** 2026-09-12
**Scope:** Step 4 of the targeting rework (plan §5.5). Ships **pre-P4**.
**New files:** `Assets/Scripts/CombatView/TargetingReadoutWidget.cs`
**Modified:** `Assets/Scripts/CombatView/CombatHud.cs`,
`Assets/Editor/CombatPrefabAuthor.cs`, `Assets/Prefabs/CombatView/CombatHud.prefab`

## What it is

A world-space panel above the **player** vehicle, mirroring the enemy
`IntentWidget` above the enemy. During a drag-cast it shows the targeted part's
**name**, its **cur/max HP**, and the **projected damage**.

Owner's framing: this is the player-side intent surface, not merely a readout.
Full lifecycle is *formulating icon → target readout → decided intent*. **Step 4
ships the target-readout state only**; the other two need turn-phase wiring and
art that do not exist yet, and are recorded in plan §5.5 as the next increment.
Shipping the readout state alone is not a half-system — it is the state that
closes the information gap the Phase 5.1 playtest exposed.

## Why pre-P4 (the ruling that moved it)

The TD's original ordering put all ring/readout work inside P4 to avoid
authoring twice. That changed once the surface was identified as a **transient
world-anchored annotation** rather than persistent HUD state:

> *"It survives on the corrected invariant, not the broken one: transient
> (exists only during drag-cast), positioned by a follower transform, no
> persistent layout, built by the same `BuildWorldCanvas` path as
> `IntentCanvas`. It is not a P4 surface under any reading. **Step 4 stays
> pre-P4.**"*

P4 migrates persistent HUD surfaces (`HudAnchors`, MainBar, rings, tooltip) to
UXML/USS. This panel is not one of them, so it is authored once and survives.

## Binding constraints from the verdict

1. **Its own sorting order — NOT `IntentCanvas`'s 5.** That sits *below*
   `HitZonesCanvas` at 15, so a mirrored order would put the readout behind the
   vehicle. Chosen: **30** — above `HitZonesCanvas` (15), above `HudAnchors`
   (20) and `CardHand` (25), below `Popups` (60). Verify on screen; nested world
   canvases in this project have a track record (`83faa72`).
2. **Reads `AttackStateController.IncomingDamage` and
   `DamagePipeline.PreviewDamage` DIRECTLY.** Not via the P4 `SlotReadout`
   struct — that is a per-slot broadcast to N widgets, while this is a singleton
   showing one slot, so routing it through would invert the data flow and create
   a pre-P4 dependency on a P4-authored type. `EnemyNumberBadge:207` is the
   existing precedent for the direct read.
3. **Subscriber-free poll in `LateUpdate`.** No new event surface;
   `AttackStateController` is documented as a pull-driven mirror. No
   `UnityEvent` (ADR-0002/0014).
4. **Do not add a second hit-test.** The targeted `(vehicle, slotId)` is pushed
   from `CombatHud`'s existing transition gate at `:1198`, the same resolution
   that already drives `SetTargetHover`.
5. **Hold-and-dim on gaps**, hung off that same transition gate. A naive
   per-frame rebuild flickers as the cursor crosses the seams between parts.
6. **Early-out on `!AttackStateController.IsActive` before any string work**,
   and **memoize `TMP_Text` writes** against last-rendered values — a per-frame
   `text` assignment rebuilds the TMP mesh every frame for an unchanged string.
   Pattern: `SlotTargetRing:220-224`, `EnemyNumberBadge:213`.
7. **Null-guard the target across vehicle swaps** — a swap destroys the target
   mid-drag.

## Wiring route — surgical YAML, not a full re-author

`_targetingReadoutPrefab` is added as a serialized field on `CombatHud` and
wired into `CombatHud.prefab` by a targeted YAML edit. The `AuthorCombatHud`
path is updated too, so the prefab stays reproducible from scratch, but it is
**not run** for this change.

Rationale, approved by the owner:

- A full HUD re-author rewrites the whole prefab and **reminds fileIDs**, which
  would break `override_sorting_gate` — shipped this morning and deliberately
  anchored to Canvas `&3615108685937019552` so a remint fails loudly.
- `CombatPrefabAuthor:3372-3380` records that a full HUD re-author *"would reset
  the 3 sibling-canvas designer tweaks."*
- The same narrow-edit reasoning was correct earlier today for
  `m_OverrideSorting` (`83faa72`) and produced a 2-line diff with zero churn.

`TargetingReadout.prefab` itself is a **new asset**, so it must be created by
running the new menu once — there is no way around that, and it touches nothing
existing.

## Not in the drift sentinel's scope

The sentinel covers `DuneSkimmer`, `Dredge`, `IronShepherd`, `PlayerVehicle`,
`MainBar`, `BuffStrip`, `Combat`. This slice creates a new prefab and
surgically edits `CombatHud.prefab`; it regenerates none of those, and
recommends no re-author of them.

## Technical Director Review

No TD agent was spawned for this file. The constraints above are the ruling the
technical-director returned across four consultations this session on the
targeting rework, quoted where load-bearing. Deviations, if any arise during
implementation, are to be recorded in-code rather than silently substituted —
as was done in steps 1 and 2, where two verdict details were correctly
overridden (`f8b1df6`, `6b0e743`).

## Merge conditions

EditMode ≥1287 with one `[Explicit]` skip, PlayMode ≥17, zero `error CS`, both
result XMLs present (the Editor-open trap reports a clean error count with no
XML), grep-gates clean. Playtest: readout appears above the player vehicle on
drag, shows the right part and numbers, **renders above the vehicle art**, and
holds-and-dims rather than flickering as the cursor crosses part seams.

## Approval

User approved step 4 and the surgical-YAML wiring route, 2026-09-12.

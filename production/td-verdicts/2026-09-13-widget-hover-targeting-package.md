# TD Verdict — Widget hover + targeting package (step 4b)

**Date:** 2026-09-13
**Scope:** Plan §5.5. Pulls step 5's *behaviour* forward into UGUI; P4 still
re-homes the tooltip widget to UXML later.
**Files:** `Assets/Scripts/CombatView/VehicleBarStack.cs`,
`Assets/Scripts/CombatView/VehiclePartHitZone.cs`,
`Assets/Scripts/CombatView/EnemyNumberBadge.cs`

## What the playtest established

Owner aimed at an enemy wheel. The hit zone's own silhouette highlighted and the
badge's number previewed, but the badge itself did not highlight, the ring could
not be targeted, and hovering a ring produced no tooltip.

Diagnosed to a single root cause, verified in code — **not** a z-order or
layering problem, which was the owner's hypothesis:

```
WireCombatHoverTarget(_runtimeMainBar, _structuralSlotId)   // :542
WireCombatHoverTarget(zone, slotId)                          // :926
_combatHitTargets.Add(zone)                                  // :929
```

Rings and badges appear in **neither** call. So they were never drag-cast
candidates (nothing out-competes them — they are not in the race), and their
`OnPointerEnter` fires `OnHover?.Invoke(...)` into an empty delegate. Both
symptoms, one cause: they were wired as display-only mirrors per Slice 2b TD
verdict Q3.

Separately, `EnemyNumberBadge.SetTargetHover` (`:236-242`) only swaps the number
— the badge has **no outline element at all**, so "highlight the widget while
targeting its art" was unachievable on the enemy side regardless of wiring.

## Decisions

1. **Rings and badges become hover + click targets.** Wired through
   `WireCombatHoverTarget` and added to `_combatHitTargets`. The structural zone
   stays bound LAST so smaller part zones continue to beat the chassis
   (`:583-599`). Ring and zone resolve to the same `slotId`, so whichever wins
   first-hit-wins yields the same target — no ambiguity to arbitrate.
2. **Widget hover shows the per-slot tooltip.** `HandleWidgetHover`'s
   `if (slotId != _structuralSlotId) return;` early-out is relaxed so every slot
   surfaces its own name / state / HP, not just the structural anchor.
3. **The hit zone stops being a tooltip source, in the same commit.** Two hover
   surfaces answering for one slot is the redundancy this whole rework exists to
   remove. This is step 5's gate behaviour arriving early.
4. **Badge backdrop tints while targeted.** Chosen over adding a real outline
   element: the badge already drives `_numberText.color` off preview state
   (`:216`), so a backdrop tint matches its own idiom, needs no new authored
   child, and requires **no prefab re-author or menu run**. The backdrop's
   *sprite* stays damage-state driven; only the unused colour channel is taken.
   If the tint reads weak on a 44px disc, an outline element is a later swap.

## Why this lands before P4 rather than inside it

The prior sequencing put ring-hover info inside P4 to avoid authoring twice.
What is actually moving here is a **trigger**, not a surface: `BuffTooltipWidget`
is untouched, and P4's job of re-homing that widget to UXML is unchanged. The
duplicated work is roughly ten lines of subscription, against an owner who is
playtesting this daily and for whom the current split is the thing that keeps
confusing the feel.

The commit-boundary rule still holds and is why (2) and (3) ship together:
per-part information must never be homeless for the length of a commit.

## Dead code

Removing the zone as a tooltip source strands its tooltip plumbing. ADR-0011
forbids dormant retention, so it goes in the same commit: `_tooltip`,
`_tooltipKey`, `_displayName`, `_info`, `_tooltipShown`, `_pointerInside`,
`_pointerEnterScreenPos`, `ShowTooltip`, `HideTooltip`, the per-frame
suppression `Update()`, and `Bind`'s tooltip parameters.

**Deliberately retained:** `Refresh(hp, maxHp, state)` and the `_currentHp` /
`_maxHp` / `_damageState` fields. They are no longer tooltip inputs but
`Refresh` also performs the live sprite re-pull (`:349-352`) that keeps the alpha
hit-test mask synced to damage-state sprite swaps — the defect fixed in
`f8b1df6`. Deleting them would silently re-break enemy targeting accuracy.

## The `damagedAlive` question, deliberately NOT answered here

`VehiclePartHitZone.OnPointerEnter` suppressed the tooltip when a slot was
damaged-but-alive, reasoning that the ring already showed the HP. That rule dies
with the zone tooltip. Whether an equivalent belongs on the **widget** is a
design call the owner will answer by playtest — and its original justification
inverts there, since hovering the widget means hovering the very thing the rule
deferred to. Not reinstated by default; flagged for the playtest.

## Technical Director Review

No TD agent spawned. The constraints are the ruling returned across four
consultations on this rework, the load-bearing ones being that the gate flip and
widget-hover info must land in the same commit, and that dead plumbing is
deleted rather than left dormant. Deviations arising in implementation are to be
recorded in-code, as in `f8b1df6`, `6b0e743` and `3cef945` where verdict details
were correctly overridden.

## Merge conditions

EditMode ≥1287 with one `[Explicit]` skip, PlayMode ≥17, zero `error CS`, both
result XMLs present, grep-gates clean. Playtest: ring/badge targetable, widget
hover shows the tooltip, hit-zone hover no longer does, badge tints while its
art is targeted, and a verdict on whether `damagedAlive` should return.

## Approval

User approved all four items, 2026-09-13.

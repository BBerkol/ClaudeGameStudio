# TD Verdict — SlotTargetRing Subsystem HUD Refactor

**Date:** 2026-07-01
**Requested by:** user (post-eyeball-pass, mid session that also shipped Tasks A/B and reverted Task C)
**Scope:** Replace `SubsystemBar` + `SubsystemMarker` + `MainBarWidget` per-slot pair with a single circular `SlotTargetRing` widget per slot. Applies to both combat and rest. Bundled question: green-light the deletion of the abandoned `SetPlayerBarsActive` helper in `RestPickerController.cs` (Task C revert).

## Technical Director Review

**Verdict — Accept the ring refactor, but AFTER forward-pressure ships.**

The ring is a cosmetic replacement of two widgets that already work; forward-pressure is model work that changes what the HUD must display (fuel + storm meters need shape decisions). Doing rings first means re-touching them once forward-pressure lands.

### Ordering call — AFTER forward-pressure

Forward-pressure introduces new HUD elements (fuel meter, storm indicator) that share visual grammar with slot rings. Ship rings first and we design their color/damage-state palette in isolation, then discover fuel meter needs to slot into the same language and re-tune both. Bundle the ring refactor as the *view* half of a "HUD-shape unification" pass right after forward-pressure model lands = one canonical visual pass, not two.

### Scope shape — Single canonical cut

Per ADR-0011. Minimum shippable unit = all slots on all vehicles (player + enemies + Dredge) + both modes (combat + rest) in one commit. Phased-by-side (enemies first) creates a bimodal bar/ring cohabitation the memory + ADR-0011 both forbid. Refactor is bounded (one widget kind, one bind loop), so canonical cut is tractable.

### MainBar decision — (c) with a nudge toward (b)

Armor-is-buffer memory (`project_armor_not_subsystem`) says armor is NOT a slot, so it must not become a slot ring — kills option (a). Between (b) and (c): if forward-pressure defines a "resource meter" grammar (fuel bar shape), armor buffer should adopt that grammar for consistency. Ship (c) shape now (armor pill above chassis, structural HP as ring on chassis slot), revisit unifying armor pill + fuel meter once forward-pressure ships. This is why ordering matters.

### Capture inventory — bake before deletion

1. `SubsystemBar` per-vehicle authored positions, scales, `_pixelsPerHp`, damage-state colors (green/yellow/red thresholds + hex values).
2. `SubsystemMarker` icon sprite references + per-slot positions.
3. `MainBarWidget` authored sizes, AP/HP split geometry, text formatting.
4. `HideRule` per-slot mapping (`HideOnFullUnlessAttackActive` / `DamagedOnly` / `AlwaysVisible`) — **load-bearing** for rings (visibility semantics carry forward).
5. Damage-state color thresholds — **load-bearing** (rings inherit the palette).

### GDD vs direct — Lightweight spec sheet

Not a full 8-section GDD. This is view-layer widget replacement, not a new mechanic. Spec needs: ring visual states table (side × mode × damage-state → outline treatment), hover behavior, HideRule mapping, tooltip payload, anchor placement math. Ship as `design/hud/slot-target-ring.md` (spec, not GDD) + capture doc + this verdict.

### Slice sequence

1. Ship forward-pressure model (fuel + storm) — HUD stays on bars.
2. Capture doc: bake SubsystemBar / SubsystemMarker / MainBarWidget authored values + HideRule map + palette.
3. Spec sheet: ring visual states + hover + tooltip contract + anchor math (centered vs offset).
4. Author `SlotTargetRing` MB + prefab. Enemy jagged + player smooth variants as prefab variants of one base.
5. Rewrite `VehicleBarStack` bind loop to drive rings (same anchor catalog, same HideRule inputs).
6. Author armor pill above chassis (MainBar-c path). Retire `MainBarWidget` prefab.
7. Re-author all vehicles via `CombatPrefabAuthor`. Delete `SubsystemBar` / `SubsystemMarker` / `MainBarWidget` prefabs + MBs. Update damage-popup + drag-cast + tooltip consumers to ring anchors.
8. Migrate tests to canonical ring APIs (no bridge shims).

### Task C helper deletion — GREEN-LIT

The `SetPlayerBarsActive` helper in `RestPickerController.cs` (~35 lines) is dead-on-arrival given the reverted design direction. Retaining it accumulates ADR-0011 debt (bimodal path: entry-hidden vs always-visible). **Delete now**, before rings land, so the ring bind loop doesn't inherit a phantom visibility toggle. **This deletion is authorized by TD.**

## Files at Risk (future ring-refactor scope)

- `Assets/Scripts/CombatView/VehicleHudAnchors.cs` — anchor catalog reuse target
- `Assets/Scripts/CombatView/VehicleBarStack.cs` — bind loop rewrite
- `Assets/Scripts/CombatView/SubsystemBar.cs` — deletion target
- `Assets/Scripts/CombatView/SubsystemMarker.cs` — deletion target
- `Assets/Scripts/CombatView/MainBarWidget.cs` — deletion target (partial — armor pill replacement)
- `Assets/Scripts/CombatView/RestPickerController.cs` — hover routing update
- `design/node-map.md` — forward-pressure spec (verify shape before ring palette locks)

## Files Touched by This Verdict (immediate)

- `Assets/Scripts/CombatView/RestPickerController.cs` — deletion of `SetPlayerBarsActive` helper (Task C revert cleanup)

## Risks

- Ring hover-preview math (projected damage in center) couples to attack resolution — audit `AttackStateController` wiring before scope-locking.
- Anchor catalog reuse assumes ring-centered placement matches designer intent for bar-offset anchors; expect a re-tune pass.
- Forward-pressure delay could push rings out further than a slice — accept that; polish-mode discipline over speculative view work.

## Decision Log

- Ordering: AFTER forward-pressure.
- Scope: single canonical cut, no bimodal cohabitation.
- MainBar: option (c) — armor pill above chassis, structural HP as ring on chassis slot.
- Spec depth: lightweight spec sheet, not full 8-section GDD.
- Task C revert: delete `SetPlayerBarsActive` helper immediately.

# Capture — Drag-Cast Threshold Retune (CombatHudPanel.prefab)

Date: 2026-09-16
Slice: P4a defect-fix (D1 drag thresholds + drag-only card play)
Unity repo file: `Assets/Prefabs/UI/CombatHudPanel.prefab`

## Authored values being destroyed

| Field | Prefab line | Current (authored) | New | Source of current value |
|---|---|---|---|---|
| `_castEngageLiftPx` | 74 | `120` | `400` | Baked from C# initializer at author time (AuthorCombatHudPanel never sets it) |
| `_castCommitLiftPx` | 75 | `200` | `480` | Same |

Both C# initializers at `CombatHudPanelController.cs:212,214` change in the same
commit (120→400, 200→480) so code defaults and prefab stay in lockstep; a new
EditMode drift test asserts they remain equal from now on.

## Why

Playtested 2026-09-15/16 (defect D1): lift is measured from the un-hovered arc
row, and a hovered card already sits +100px, so engage 120 = ~20px of real drag
— the crosshair armed almost immediately on click. User-authored target: the
crosshair arms when the card is pulled up to roughly the bottom line of the
vehicle HP bars (~490px above panel bottom at 1080p reference). Measurement is
simultaneously repointed to a drag-origin snapshot (TD constraint B2), making
the constants mean "pixels of upward drag", which is what their tooltips
already claim. 80px engage→commit forgiveness gap preserved (400→480).

Starting values are explicitly calibration-pending after the first feel
playtest (TD constraint B3); they remain a two-field Inspector tune.

## Method

Surgical 2-line YAML edit to the prefab. Explicitly NOT a re-run of
`Author CombatHudPanel Prefab` — a full re-author remints the asset and is not
idempotent. No other prefab values touched.

## Technical Director Review

Verdict: **TD-CHANGE-IMPACT: CONCERNS** — slice approved to land under 14
binding constraints. Full verdict + three-lens self-audit:
`production/td-verdicts/2026-09-16-p4a-defect-fix-slice.md`. The finding that
created this capture: "the brief's 'no prefab or scene edits' is false —
`CombatHudPanel.prefab` lines 74-75 carry `_castEngageLiftPx: 120` /
`_castCommitLiftPx: 200` baked in; changing the initializers alone produces an
identical build. Do not re-author to propagate; the two-line YAML edit is
surgical. Capture eligible: write the capture enumerating both authored values
being replaced."

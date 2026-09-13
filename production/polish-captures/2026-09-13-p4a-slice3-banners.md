# Capture — P4a slice 3: turn-phase banner + ambush banner

**Date:** 2026-09-13
**System:** Combat HUD — UGUI → UI Toolkit (ADR-0018 P4a)
**Deleted:** `TurnPhaseWidget.cs`, `AmbushBannerWidget.cs`,
`TurnPhaseBanner.prefab`, `AmbushBanner.prefab`, their author methods, and their
nested instances in `CombatHud.prefab`
**Extended:** `CombatHudPanel.uxml` + `.uss`, `CombatHudPanelController.cs`

Two widgets in one slice because they are the same shape (top-centre banner,
background + text) and the ambush banner's position is **derived from the turn
banner's height** — splitting them would leave that dependency spanning two
commits.

## Authored values being destroyed

### `TurnPhaseBanner.prefab`

| Property | Value |
|---|---|
| `sizeDelta` | **360 × 100** |
| `turnText` | fontSize **30**, **Bold** |
| `phaseText` | fontSize **18**, Normal |
| text colour (both lines) | **RGBA(0.97, 0.94, 0.88, 1)** warm off-white — **not** white |
| anchor / pivot | top-centre, pivot (0.5, 1) |
| top margin | `TurnPhaseTopMarginPx` = **24** |

The two text lines **split the banner in half** — `turnText` anchor-stretched to
the top half (`anchorMin.y` 0.5), `phaseText` to the bottom half
(`anchorMax.y` 0.5), each with a **16px** horizontal inset and its text
vertically centred inside its own half. This is not a stacked-and-centred pair:
a flex column with `justify-content: center` bunches the lines together in the
middle of the box, which reads as the same design pushed slightly out of true.
Reproduced in USS with `height: 50%` on each label.

Five background tints from `TurnPhaseWidget.cs`, each a per-phase colour cue the
player reads *before* the text:

| Phase | Colour |
|---|---|
| `PlayerTurn` | **RGBA(0.10, 0.20, 0.35, 0.92)** calm blue |
| `EnemyTurn` | **RGBA(0.40, 0.18, 0.10, 0.92)** warning orange |
| `PlayerResolve` / `Setup` | **RGBA(0.12, 0.12, 0.14, 0.92)** neutral grey |
| `Ended` + player won | **RGBA(0.12, 0.32, 0.18, 0.95)** victory green |
| `Ended` + player lost | **RGBA(0.40, 0.10, 0.10, 0.95)** defeat red |

Text per phase, verbatim: `PlayerTurn` → "TURN {n}" / "your turn" ·
`PlayerResolve` → "TURN {n}" / "resolving…" (note the ellipsis character) ·
`EnemyTurn` → "TURN {n}" / "enemy turn" · `Setup` → "READY" / "" ·
`Ended` → "VICTORY" or "DEFEAT" / "".

### `AmbushBanner.prefab`

| Property | Value |
|---|---|
| `sizeDelta` | **360 × 28** |
| background | **RGBA(0.45, 0.18, 0.10, 0.92)** |
| label | fontSize **14**, **Bold** |
| label colour | **RGBA(1.00, 0.88, 0.70, 1)** warm cream — **not** white, and a *different* cream from the turn banner's |
| text | **"[AMBUSH] — enemy struck before turn 1"** (em dash) |

The label text was baked onto the **prefab**, not assigned by
`AmbushBannerWidget` (which held only a `_label` reference). Its UI Toolkit
equivalent is therefore the UXML `text` attribute, not a controller write.
| gap below turn banner | `AmbushBannerGapBelowTurnPx` = **8** |

Visible **only** when `Loop.Encounter == EncounterType.Ambush`.

## The derived offset, made literal

The ambush banner's top margin was computed:

```
TurnPhaseTopMarginPx + _turnPhaseSizePx.y + AmbushBannerGapBelowTurnPx
= 24 + 100 + 8 = 132
```

In USS that becomes a literal `top: 132px`. **The derivation is preserved in a
comment beside it**, because the relationship is real: change the turn banner's
height and the ambush banner must move. A bare `132` with no note is how that
dependency gets silently broken by the next person to retune the banner.

Both pivots are (0.5, 1) — top-centre — so `anchoredPosition.y` was already the
box's top edge. These convert directly, unlike the centre-pivot widgets in
slices 1 and 2 which needed half-extents subtracted.

## Behaviour preserved

- **Ambush toggles its children, not its GameObject.** The original comment
  explains why and it still applies in USS terms: disabling the element itself
  would stop the update path, freezing visibility when the encounter type
  changes on a designer Reset. The USS equivalent toggles `display` on the
  element while the controller keeps polling.
- `AmbushBannerWidget._lastVisible` was seeded **true** to force the first
  write — the same establishing-write discipline the panel controller already
  uses, and the same bug that shipped a visible empty readout earlier in this
  rework. Preserved as a nullable in the controller.

## Dying with them

`_turnPhaseSizePx`, `_ambushBannerSizePx`, `TurnPhaseTopMarginPx`,
`AmbushBannerGapBelowTurnPx` — all four become literals in USS. Unlike slices 1
and 2, `_turnPhaseSizePx` **was** genuinely read (by the ambush offset
calculation), so this is a real relocation rather than the removal of a
write-only field.

## Technical Director Review

No TD agent spawned. Executes the pattern proven in slices 1–2 under ADR-0018
P4a with no new architectural decisions: one shared `CombatHudPanel` tree,
document sortingOrder −10, and no `CombatView` type named from
`WastelandRun.UI` — both widgets need only `CombatLoop` (`Phase`, `TurnCount`,
`Winner`, `Encounter`), all in `WastelandRun.Combat`.

Expect the nested-instance excision for both, third and fourth occurrence: each
prefab is instantiated inside `CombatHud.prefab`, so each needs its
`PrefabInstance` block, its `stripped` blocks, and its orphaned `m_Children`
entry removed.

## Merge conditions

EditMode ≥1287 with one `[Explicit]` skip, PlayMode ≥17, zero `error CS`, both
result XMLs present, grep-gates clean.

**Playtest:** banner reads "TURN n" with the phase line beneath · tints blue on
your turn, orange on the enemy's, grey while resolving · shows VICTORY green or
DEFEAT red at the end · "READY" during setup · ambush tag appears **only** on an
ambush encounter, directly below the turn banner with its 8px gap.

## Approval

User approved continuous execution of P4a on 2026-09-13 ("keep going until we
are done").

# Capture — P4a slice 4: pile chips (DECK / DISCARD)

**Date:** 2026-09-13
**System:** Combat HUD — UGUI → UI Toolkit (ADR-0018 P4a)
**Deleted:** `PileCountWidget.cs`, `PileChip.prefab`, `AuthorPileChip` +
`BuildPileChipLine`, both nested instances in `CombatHud.prefab`,
`CombatHud.BuildPileChips` / `SpawnPileChip`
**Extended:** `CombatHudPanel.uxml` + `.uss`, `CombatHudPanelController.cs`
**Rewritten:** `CombatHud.ChipCenterInBottomCenterSpace` — see "The coupling"

## Slice split — why the popup is NOT in here

The original plan bundled pile chips with `PilePopupWidget` and
`PilePopupRowWidget` as one ~564-line slice. Splitting them, because they are
not the same shape and only one of them is coupled to anything:

- the **chips** are screen-anchored readouts whose live `RectTransform` is read
  by the card-hand animation (see below) — migrating them forces a real change
  to code outside the widget;
- the **popup** is a self-contained modal whose only tie to the chips is two
  numeric constants, and it has no consumers at all.

The popup + row become slice 5. Crosshair moves to 6, card hand to 7.

## The coupling — the only non-mechanical part of this slice

`CombatHud.GetDeckChipPositionInHandSpace` / `GetDiscardChipPositionInHandSpace`
are injected into `HandSequencer` (`CombatHud:290-291`). They drive where a card
flies **to** on discard and **from** on draw. Both call
`ChipCenterInBottomCenterSpace(chip)`, which reads the chip's live
`RectTransform` — `chip.position`, `chip.rect`, `chip.pivot` — and converts
world → canvas-local → bottom-centre anchored space. Its comment advertises that
it "works for any anchor/pivot combination".

Once the chips are UI Toolkit elements there is no `RectTransform` to read, and
the card hand does not migrate until slice 7. So this has to change now.

**It is not a loss.** The chip's position is not a discovered value — it is
authored, and `SpawnPileChip` sets it from constants this same class owns:

```
deck    centre = (-canvasW/2 + PileChipEdgeMarginPx + chipW/2,  _pileChipYPx)
discard centre = ( canvasW/2 - PileChipEdgeMarginPx - chipW/2,  _pileChipYPx)
```

Derivation, for the deck chip: anchor (0,0) and pivot (0, 0.5) put the pivot at
canvas-local x = `-canvasW/2 + 24`; `pivotToCentre.x = chipW * (0.5 - 0)` shifts
it to the centre. `pivot.y` is 0.5, so `pivotToCentre.y` is 0 and the centre's
height above the bottom edge is exactly `anchoredPosition.y` = `_pileChipYPx`.
The discard chip mirrors it with anchor/pivot x = 1 and a negated margin.

So the generalised converter was machinery re-deriving, through three coordinate
spaces at runtime, a value the same class had just written from constants. It
goes, and the two accessors compute the centres directly. **`canvasW` still
comes from the live canvas** `RectTransform` — that genuinely varies with
resolution and is the one term that was never a constant.

This also resurrects a dead field. `_pileChipWidthPx` (`:119`) was written from
the prefab's `sizeDelta` at `:417` and **read nowhere** — the same write-only
shape as slices 1–3. It becomes load-bearing in the new formula instead of being
deleted.

## Authored values being destroyed — complete enumeration

Read from **`AuthorPileChip`**, not from the widget. Slice 3's lesson: the
widget's C# constants are not the whole palette, and in this case the widget
holds *no* colours at all — the author comment says so explicitly ("PileCountWidget
no longer keeps a second copy of them").

### `PileChip.prefab` geometry

| Property | Value |
|---|---|
| `sizeDelta` | **70 × 56** |
| Label (top half) | anchors y 0.5→1, offsets **(4, 0)** to **(−4, −2)** |
| Count (bottom half) | anchors y 0→0.5, offsets **(4, 2)** to **(−4, 0)** |
| Label font | **12**, **Bold**, centred |
| Count font | **22**, **Bold**, centred |

Both lines are 4px inset horizontally; the label clears the top edge by 2px and
the count clears the bottom edge by 2px. Halves are 28px, so each text row is
26px tall: `2 + 26 + 26 + 2 = 56`.

### Colours

| Element | Value |
|---|---|
| background | **RGBA(0.10, 0.10, 0.13, 0.85)** |
| label | **RGBA(0.75, 0.72, 0.65, 1)** dim stone |
| count | **RGBA(0.97, 0.94, 0.88, 1)** warm off-white |

The count colour is the **same** warm off-white as slice 3's turn banner. It gets
its own token rather than sharing one — the two widgets are independent and a
shared token would silently couple a retune of one to the other.

### Placement — `CombatHud`

| Constant | Value |
|---|---|
| `PileChipEdgeMarginPx` | **24** — gap from chip's outer edge to canvas edge |
| `_pileChipYPx` | **180** — chip **centre** above the canvas bottom edge |
| deck | bottom-**LEFT**, anchor + pivot x = 0 |
| discard | bottom-**RIGHT**, anchor + pivot x = 1 |

Edge-anchored per W7.30 so the chips track the real canvas corner on any aspect
ratio — in USS that is `left: 24px` / `right: 24px`, which is the same promise
without the anchor arithmetic. Centre-to-box: `bottom = 180 − 56/2 = 152`.

`PileChipEdgeMarginPx` is `internal const` and **survives** — `PilePopupWidget`
reads it (`PanelEdgeMargin`) so the popup's outer edge lines up with the chip's.
It dies in slice 5, not here.

### Dying with it

`_pileChipHeightPx` (write-only, `:120`/`:418`) and `PileChipGapPx` (`:121`,
declared and **never referenced at all**).

## Behaviour being preserved

### The roll animation — the real content of this widget

`PileCountWidget` polls its count every frame and ticks the displayed number one
integer at a time toward it, pulsing the scale on each tick, so the player sees
cards *moving* between piles rather than a number teleporting.

| Constant | Value | Why |
|---|---|---|
| `TickIntervalDefaultSec` | **0.05** | cadence for small deltas |
| `MaxRollDurationSec` | **0.55** | caps big jumps — a 50-card pile must not hold the eye for 2.5s |
| `PulsePeakScale` | **1.45** | |
| `PulseDurationSec` | **0.18** | `sin(πt)` envelope, 0 → peak → 0 |

Tick cadence is `min(TickIntervalDefaultSec, MaxRollDurationSec / distance)`.

**The pulse scales the COUNT only** — label and background hold still, so the
number jiggles rather than the whole chip throbbing.

### The scripted-roll override, and the bug it exists to dodge

`PlayDeltaAnimation(delta)` suspends polling for one roll. It exists because
EndTurn runs DiscardHand + DrawHand + reshuffle **synchronously in one frame**:
for the deck chip the steady-state count before and after can be identical
(4 → 4) even though five cards just changed piles. The scripted roll overshoots
to the high-water mark (4 → 9) and then polling rolls it back down (9 → 4),
giving the player a visible "cards moved here" beat.

Two guards inside it must survive the port verbatim:

- the pre-mutation value is derived as `live − delta`, **anchored on the live
  count**, not on `_displayedCount + delta`. Anchoring on the displayed count
  would let a large negative delta drive the scripted target negative, tick the
  chip into the `-1` "not yet initialised" sentinel, and re-snap every frame.
- `preMutation` is clamped at 0, and `_displayedCount` is clamped at 0 on every
  tick, for the same reason.

`_displayedCount == -1` means "first read after Bind" and snaps silently — the
deck starts populated and must not roll 0 → 9 on combat start.

### Empty-pile dim

`SetInteractable(latest > 0)` runs every frame, driven off the **live** count and
not the animated one, so a mid-roll transient zero never flickers the chip. The
dim is `CanvasGroup.alpha = 0.45` plus `Button.interactable = false`.

**One judgement recorded:** UGUI stacked a second dim on top — `Selectable`'s
default disabled `ColorTint` (×0.78 RGB, ×0.5 alpha) applied to the background
*in addition to* the CanvasGroup. That is Unity's default, not an authored
decision; nobody chose ×0.78. The port keeps the authored `opacity: 0.45` on the
whole chip and drops the Selectable tint. Same shape as slice 2 dropping the
`Awake` sprite-repair: the thing being removed was an artefact of the mechanism,
not of the design.

Hover and press feedback likewise came from `Selectable`'s default ColorTint
(×0.96 and ×0.78) rather than anything authored. Unlike the disabled dim these
have no replacement in the design, so they are reproduced explicitly in USS —
losing press feedback on a button the player clicks is a real regression, and
"it was only the Unity default" does not make the button feel any less dead.

### Click

`OnClick` is a plain C# `Action`, never a `UnityEvent` (ADR-0002). The chip
raises it only when interactable. `CombatHud` subscribes to open the matching
pile popup, unsubscribing first so a re-`BuildPileChips` on combat reset cannot
double-fire. The popup is still UGUI until slice 5, so the controller raises
`OnDeckChipClicked` / `OnDiscardChipClicked` and `CombatHud` keeps calling
`_pilePopup.Show(...)` — the same callback seam slice 1 used for End Turn.

`EnsureClickable()` disappears with the widget. Its comment claimed the
`AddComponent` branches were "the LIVE path on every instantiation" because the
prefab carried no Button or CanvasGroup — **that comment is stale**:
`AuthorPileChip` adds both and `SerializedObject`-wires them. Either way the
question is moot in UI Toolkit, where a Button is an element type rather than a
component that can be missing.

## Nested-instance excision — fifth and sixth occurrence

`_deckChip` and `_discardChip` are local refs (no `guid:`), so both are
instantiated inside `CombatHud.prefab`. Each needs its `PrefabInstance` block,
its `stripped` blocks, and its orphaned `m_Children` entry removed — done
highest-line-first against a scratchpad backup, with the structural YAML check
(every type line preceded by a document marker, no consecutive markers) run
before any test.

## Technical Director Review

No TD agent spawned. Executes the pattern proven in slices 1–3 under ADR-0018
P4a. `WastelandRun.UI` names no `CombatView` type: the controller needs
`CombatLoop.DeckCount` and `CombatLoop.Discard.Count`, both in
`WastelandRun.Combat`.

The one decision worth flagging is the converter rewrite, and it is a **net
simplification that removes a UGUI dependency from a code path that outlives
UGUI** — `HandSequencer` keeps taking two `Func<Vector2>` and does not know the
chips changed technology. If a later designer moves the chips off the corner
formula, the accessors are the single place that has to follow, which is where
that knowledge belonged already.

Recorded risk: the chips were on the `Combat_HUD` canvas at sortingOrder 10,
**below** HitZones (15) and HudAnchors (20). The UI Toolkit panel sits at
document sortingOrder −10 and slice 1's playtest confirmed UGUI at 15/20 still
receives its raycasts. So the chips keep rendering above the vehicles and below
the outcome overlay, unchanged.

## Merge conditions

EditMode ≥1285 (three wiring rows retire with the prefab) with one `[Explicit]`
skip, PlayMode ≥17, zero `error CS`, both result XMLs present, grep-gates clean.

**Playtest:**
- both chips sit at the bottom corners, 24px in, reading DECK / DISCARD over a
  live count
- **end a turn**: the discard count rolls up one integer at a time with the
  number pulsing, and the deck count rolls *down* — not a teleport
- **force a reshuffle** (play until the deck empties): the deck chip overshoots
  to the high-water mark and rolls back down; it must not flash a `-1` or a
  negative number at any point
- an empty pile dims to 45% and its chip does nothing when clicked
- click either chip → the matching pile popup opens (still the UGUI one)
- **cards still fly to and from the chips** — this is the coupling rewrite; a
  card animating to (0,0) bottom-centre instead of the corner means the new
  formula is wrong

## Approval

User approved continuous execution of P4a on 2026-09-13 ("keep going until we
are done").

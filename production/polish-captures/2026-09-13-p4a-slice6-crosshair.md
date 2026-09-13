# Capture — P4a slice 6: drag-cast crosshair

**Date:** 2026-09-13
**System:** Combat HUD — UGUI → UI Toolkit (ADR-0018 P4a)
**Deleted:** `CrosshairWidget.cs`, `Crosshair.prefab`, `AuthorCrosshair` +
`BuildCrosshairImage` + its menu item, the `_crosshairWidget` nested instance in
`CombatHud.prefab`, and the `CombatWidgetPrefabWiring_Test` row for it
**Extended:** `CombatHudPanel.uxml` + `.uss`, `CombatHudPanelController.cs`,
`AuthorCombatHudPanel.cs` (two new serialized `Sprite` refs)

**⚠ REQUIRES AN AUTHOR RUN:** `Tools > Wasteland Run > Author CombatHudPanel
Prefab` — the controller gains two serialized `Sprite` fields pulled from
`UI_Elements.psb`, and until the prefab is re-authored the reticle renders as
untextured boxes.

## Why this is a migration and not a registry entry

ADR-0018 retains UGUI for "transient, per-event annotations positioned by a
**live world transform**, with no persistent screen-space layout". The crosshair
is transient and per-event, but it is positioned by the **cursor** — screen
space, not a world transform. It fails the invariant, it is not one of the four
registry members, and the registry is a closed list. So it migrates. The ADR
answers this without needing a judgement call.

## Authored values being destroyed — complete enumeration

Read from `AuthorCrosshair`, plus the designer-baked fields on the widget.

### Geometry — `Crosshair.prefab`

| Element | Size | Centre offset | Rotation |
|---|---|---|---|
| root | **96 × 96** | (0, 0) | — |
| Center | **7.871 × 7.871** | (0, 0) | 0° |
| Quarter_TL | 30 × 37 | **(−11.4, +29.5)** | **0°** |
| Quarter_TR | 30 × 37 | **(+29.1, +11.2)** | **−90°** |
| Quarter_BL | 30 × 37 | **(−29.3, −12.1)** | **+90°** |
| Quarter_BR | 30 × 37 | **(+12.1, −29.6)** | **−180°** |

The centre dot's native PSB size is 6 × 6; **7.871** is a designer tune, not a
rounding of anything. The four quarters are a **designer-baked layout dated
2026-05-16**: each bracket sits on one cardinal *side* of the box, offset along
the perpendicular axis for a pinwheel feel — they are deliberately not on the
diagonals, which is why the names TL/TR/BL/BR no longer describe where they sit.

### Designer-baked runtime knobs (2026-05-17)

| Field | Value | Note |
|---|---|---|
| `LockMultiplier` | **0.65** | explicitly retuned from an initial 0.55 — "gentler pull-in" |
| `LockTransitionSec` | **0.5** | retuned from 0.18 — "slower, more deliberate settle, reads as a heavier lock-acquired beat" |
| `AttackTint` | **RGBA(1, 0, 0, 1)** | pure red; art is neutral grey so a pure red multiplies to a strong saturated red, "less salmony than the original 1/0.3/0.25" |
| `RepairTint` | **RGBA(0.298, 0.858, 0.129, 1)** | saturated lime, "lifts above the dusty wasteland palette without going neon" |

All four carry written rationale for having been changed from an earlier value.
They are the most clearly designer-owned numbers in the whole of P4a and are
ported exactly, to the full float precision of the tints.

### Sprites

`crosshaircenter` and `crosshairouterquarter`, **sub-sprites inside
`Assets/Resources/UI_Elements.psb`**.

This matters: USS `resource("UI_Elements")` addresses the PSB asset, **not** a
named sub-sprite inside it — the `resource()` function cannot reach one. So the
sprites arrive as two serialized `Sprite` fields on the controller, applied as
inline `style.backgroundImage`. That is the established house pattern, already
stated on `DraftCardElement`: "controller holds the per-family sprite refs as
serialized fields — chosen over `Resources.Load` so the UI asmdef stays
asset-pipeline-agnostic and the wiring is visible in the Inspector."

The tint is `-unity-background-image-tint-color`, which is the same multiply the
UGUI `Image.color` was doing against neutral-grey art.

## The animation, ported exactly

Two states. **Default** = quarters at their authored positions (resting/scanning).
**Locked** = each quarter slides along its **single dominant cardinal axis** —
whichever of x/y has the larger absolute value in its default offset — pulling
that component toward 0 by `× LockMultiplier`, leaving the other axis alone.

That produces a pinwheel pinch rather than a radial shrink: TL slides **down**,
TR slides **left**, BL slides **right**, BR slides **up**.

- `MoveTowards` on `_lockT` over `LockTransitionSec`, then an **ease-in-out
  cubic** applied to the result — symmetric, so acquire and release feel the
  same, and cheap (no `Mathf.Pow`).
- **Mid-animation reversals glide.** `SetAcquired` only moves the *target*;
  `_lockT` is never snapped to an endpoint. Flicking the cursor off a target and
  back on reverses from wherever the interpolation currently is. Preserved.
- `Hide()` **resets the lock state to 0**. Without it, hiding mid-acquire would
  leave `_lockT` partway through and the next cast would render quarters in the
  wrong place for a frame before any `SetAcquired` landed. Preserved.

### Keeping "designer re-tunes and the direction re-derives"

The widget deliberately snapshotted the authored positions at `Awake` and
re-derived the dominant axis from them, so re-tuning in Prefab Mode needed no
code change. That property is preserved, but it needs one conversion.

USS positions **box edges**; `anchoredPosition` was a **centre offset** with
centred anchors and y-**up**. Running the dominant-axis rule on raw USS margins
gives the wrong answer — for Quarter_TR the margins are (14.1, −29.7), so the
rule would pick **Y** as dominant, where the centre offsets (29.1, 11.2) pick
**X**. TR would slide up instead of left, and the pinwheel would break.

So the controller snapshots `resolvedStyle` after first layout and converts back
to centre-offset space:

```
centreX =   marginLeft + width  / 2
centreY = −(marginTop  + height / 2)
```

USS stays the source of truth, designer edits still flow through, and the
derivation runs in the space it was written for.

## Two things that change

**1. The tuning surface.** Corner positions move from "drag the four quarter
rects in Prefab Mode" to "edit four USS margins". Every slice so far moved
geometry into USS, but this is the first widget whose comments explicitly invite
a designer to drag-tune it, so it is called out rather than buried. The
re-derivation above is what keeps that invitation meaningful.

**2. Layer.** The crosshair was on `Combat_HUD` (sortingOrder **10**) — *below*
HitZones (15) and HudAnchors (20), so the reticle painted **under** the target
rings and hit zones. In the panel it paints above them, like every other
migrated widget. A cursor reticle belongs on top and this is almost certainly an
improvement, but it is a visible change and belongs in the playtest, not in a
footnote.

`OnValidate`'s live AttackTint preview in Prefab Mode has no equivalent and
goes. So does `EnsureRoot`'s raycast-disabling of all five sub-images — in UI
Toolkit that is `picking-mode="Ignore"` in the UXML, declared rather than
enforced at runtime. Its reason still holds and is written into the UXML: bars
sit underneath and must keep receiving pointer hits through the crosshair's
pixels.

## The coordinate conversion

`MoveTo` took raw screen pixels and projected them through the canvas rect with
`RectTransformUtility.ScreenPointToLocalPointInRectangle`. The UI Toolkit
equivalent is `RuntimePanelUtils.ScreenToPanel(panel, screenPos)`, which also
handles the y-flip (screen is y-up, panel is y-down).

**This is the project's first use of `RuntimePanelUtils`** — previously verified
absent anywhere under `Assets/Scripts/UI`, and the reason P4b was gated on a
measurement spike. Two things keep that gate intact rather than quietly opening
it: this is *screen*-anchored (one conversion per pointer move during a drag),
where P4b's concern is *world*-anchored (a conversion every frame, per anchor,
forever); and this call is driven by the drag pipeline, so it costs nothing
outside an active cast. P4b stays gated.

It is also **not a bridge** under ADR-0011. The drag originates in the UGUI card
hand today, but a screen→panel conversion is a conversion between two real
coordinate systems, not an adapter between an old shape and a new one — any
non-UI-Toolkit input source would need it. When the card hand migrates in slice
7 the pointer arrives in panel space and the call becomes unnecessary; if it
survives as dead weight at that point, deleting it is slice 7's job.

## Nested-instance excision — eighth occurrence

`_crosshairWidget` is a local ref (no `guid:`): `PrefabInstance` block,
`stripped` blocks, orphaned `m_Children` entry. Highest-line-first against a
scratchpad backup, structural YAML check before any test run.

## Technical Director Review

No TD agent spawned. Executes the pattern proven in slices 1–5 under ADR-0018
P4a. `WastelandRun.UI` names no `CombatView` type — the crosshair needs no
model state at all; `CombatHud` keeps deciding the tint from `CardDefinition`
and pushes it down, exactly as it did to the widget.

Two judgements recorded: the sub-sprite problem forcing serialized `Sprite`
refs (house pattern, not an invention), and the centre-offset conversion needed
to keep the dominant-axis re-derivation correct.

## Merge conditions

EditMode ≥1287 with one `[Explicit]` skip, PlayMode ≥17, zero `error CS`, both
result XMLs present, grep-gates clean.

**Playtest:**
- drag a non-Self card past the cast threshold → the card hides and the reticle
  appears at the cursor, **textured**, not as plain boxes
- it **tracks the cursor** with no lag or offset — an offset of exactly half the
  96px footprint means the box-vs-centre conversion is wrong
- Attack / Reposition casts read **pure red**; a Patch on a destroyed player
  subsystem reads **lime green**
- hover a valid target → the four brackets **pinch inward along their cardinal
  axes** (TL down, TR left, BL right, BR up), over about half a second
- flick off the target and back on mid-animation → it **reverses smoothly**,
  never snapping to an endpoint
- release, then start a new cast → the brackets start at **rest**, not partway
  pinched
- **the layer change:** the reticle now paints **over** the target rings and hit
  zones, where it used to pass under them
- rings and hit zones still receive hover/targeting through the reticle's pixels

## Approval

User approved continuous execution of P4a on 2026-09-13 ("keep going until we
are done").

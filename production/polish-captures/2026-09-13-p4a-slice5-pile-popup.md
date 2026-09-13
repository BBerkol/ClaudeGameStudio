# Capture — P4a slice 5: pile popup + row

**Date:** 2026-09-13
**System:** Combat HUD — UGUI → UI Toolkit (ADR-0018 P4a)
**Deleted:** `PilePopupWidget.cs`, `PilePopupRowWidget.cs`, `PilePopup.prefab`,
`PilePopupRow.prefab`, `AuthorPilePopup` + `AuthorPilePopupRow` and both their
menu items, the `_pilePopup` nested instance in `CombatHud.prefab`, and the
slice-4 `OnDeckChipClicked` / `OnDiscardChipClicked` callback seam (the chips
now open the popup directly — that seam existed only because the popup had not
migrated yet, and leaving it would be a bridge under ADR-0011)

**⚠ REQUIRES AN AUTHOR RUN:** `Tools > Wasteland Run > Author CombatHudPanel
Prefab`. The controller gained a serialized `_pileRowTemplate`, and until the
prefab is re-authored it is null — popups open with a title and **no rows**.
Re-authoring this prefab is safe: all its visual tuning lives in the USS.
**Added:** `Assets/UI/PilePopupRow.uxml`,
`Assets/Scripts/UI/PilePopupRowElement.cs`
**Extended:** `CombatHudPanel.uxml` + `.uss`, `CombatHudPanelController.cs`

## Decision: the popup joins the HUD panel rather than getting its own

The pile popup is a full-screen modal — scrim plus a floating panel — which
looks like the self-contained-overlay case that P3 gave its own panel
(`CombatOutcomeOverlay`, the reward pickers). It is not, and the difference is
depth.

The UGUI popup lived on the **Popups canvas at sortingOrder 60**, above
HitZones (15) and HudAnchors (20), and it must cover the chips, End Turn, the
orb and the banners — the very widgets slices 1–4 just moved into
`CombatHudPanel` at document sortingOrder **−10**. The outcome overlay sits at
**0** and must still cover the popup.

A sibling panel would have to claim a sortingOrder strictly between −10 and 0,
i.e. invent a depth slot to express "above the HUD, below the outcome overlay".
Inside one tree that relationship is just **sibling order**: a last-child
full-rect scrim paints over every earlier sibling, for free, with nothing to
keep in sync. The popup is opened from a chip in this HUD and covers exactly
this HUD; that is what "one panel, one tree" was for. The reward pickers are
genuinely different — they own the screen between combats and have no HUD
underneath them to out-sort.

**Recorded as a risk, to be settled by playtest, not asserted here:** whether
the scrim also blocks *input* to the UGUI canvases beneath it (HitZones 15,
HudAnchors 20). The HUD panel's root is `picking-mode="Ignore"` precisely so it
does **not** intercept those raycasts; the scrim opts back in. Slice 1
established that a −10 panel *renders* above them, which is a different claim
from blocking their raycasts. If the vehicle rings turn out to stay clickable
through the scrim, the fix is scoped to the scrim element and does not disturb
the rest of the slice.

## Authored values being destroyed — complete enumeration

Read from `AuthorPilePopup` and `AuthorPilePopupRow`.

### `PilePopup.prefab`

| Element | Property | Value |
|---|---|---|
| scrim | colour | **RGBA(0, 0, 0, 0.65)** — "modal" without losing the combat scene behind it |
| panel | size | **520 × 640**, centred |
| panel | colour | **RGBA(0.10, 0.10, 0.13, 0.97)** |
| title | height / offset | **44** tall, **12px** down from the panel top |
| title | font | **30**, **Bold**, centred |
| title | colour | **RGBA(0.97, 0.94, 0.88, 1)** |
| scroll area | padding | **12px** all round, **68px** off the top (44 title + 12 top pad + 12 gap) |
| content | layout | VerticalLayoutGroup, padding **4** all round, spacing **4** |
| scroll | sensitivity | **30**, vertical only, `MovementType.Clamped` |

Panel sizing rationale, preserved: 520 wide fits ~460-wide rows plus ~30px
padding either side; 640 tall shows ~14 rows at 40px before scrolling starts.

### `PilePopupRow.prefab`

| Element | Property | Value |
|---|---|---|
| row | size | **460 × 40** |
| row | stripe | **RGBA(1, 1, 1, 0.04)** — separates adjacent rows against the panel without reading as a button |
| energy chip | size / offset | **32 × 32**, **8px** left gutter |
| energy chip | colour | **RGBA(0.95, 0.78, 0.18, 1)** gold disc — matches CardWidget's energy pip |
| energy text | font / colour | **20**, **Bold**, **RGBA(0.12, 0.10, 0.04, 1)** dark on gold |
| name | margins | **48px** left (8 gutter + 32 chip + 8 gap), **64px** right (reserves the value column) |
| name | font / colour | **18**, Normal, left-aligned, **RGBA(0.97, 0.94, 0.88, 1)** |
| name | overflow | **NoWrap + Ellipsis** |
| value | column | right-anchored **56 × 36**, **8px** from the right edge |
| value | font | **22**, **Bold**, right-aligned |

### Value colours — `PilePopupRowWidget`

| Constant | Value | Kinds |
|---|---|---|
| `DamageColor` | **RGBA(1.00, 0.30, 0.25, 1)** | Attack |
| `BlueValue` | **RGBA(0.45, 0.75, 1.00, 1)** | Plate, Repair |
| `DimValue` | **RGBA(0.65, 0.62, 0.55, 1)** | Draw, Reposition, Buff, none |

These deliberately match the in-game card-face palette so the popup and the card
itself read the same value in the same colour — the player does not re-learn the
mapping when switching between hand and pile.

### The energy chip is a square

`AuthorPilePopupRow` leaves `Image.sprite` **null** so the "circular disc" reads
as a flat gold square in Prefab Mode, with a note that designers can drop a
circular sprite in later without re-authoring. In USS a disc is
`border-radius: 16px` on a 32px box, so the port ships the **round** chip the
comment always intended. This is the one deliberate visual change in the slice,
and it is the same reasoning as slice 2's energy orb: a border-radius cannot go
missing the way a sprite reference can.

## Behaviour being preserved

- **Sort is energy-ascending then name-ascending, for BOTH piles.** Not an
  accident: the draw pile must not leak turn order, and holding discard to the
  same order makes the popup a single reference the player scans the same way
  regardless of which chip opened it. Newest-first discard would be defensible
  in isolation and is explicitly rejected.
- **The caller's list is never mutated** — `Show` sorts a private copy.
- **Re-`Show` on an open popup rebuilds rows in place**, so clicking the other
  chip while open swaps the list rather than needing a close first.
- **Scroll resets to the top on every `Show`**, or a re-open strands the player
  where the previous, different list left them.
- **Click the scrim to close; clicks on the panel do not close.** UGUI needed
  `OnPointerClick` to walk the `pointerPress` transform chain up to `_panel`
  because an empty area of the panel could miss the panel's own raycast target.
  In UI Toolkit the panel is a picking element and events bubble, so the scrim's
  handler checks whether the event's propagation path contains the panel.
- **`transform.SetAsLastSibling()` on Show** so a widget built later in play
  cannot paint over the popup. Inside one tree this is the same call on the
  element, and it is also the mechanism that puts the scrim above the HUD.
- Rows are **rebuilt, never pooled** — piles are bounded (≤40 in practice) and
  a straight clear/rebuild keeps the code trivial.

`PilePopupWidget.Hide()` flipping the whole GameObject has no UI Toolkit
equivalent and becomes `display: none` on the popup root. The widget's comment
noted it had no per-frame polling to keep alive, which is why the whole-object
flip was safe there; `display: none` is equally safe and does not take a
MonoBehaviour with it.

## `PileChipEdgeMarginPx` stops being `internal` — it does NOT die

An earlier draft of this capture claimed the constant had no callers left and
could go. **Wrong** — `ChipCenterInBottomCenterSpace` still reads it to place
the card-hand fly targets (slice 4). What actually changes is its visibility.

`PilePopupWidget.PanelEdgeMargin` aliased `CombatHud.PileChipEdgeMarginPx` so
the popup's outer edge lined up with the chip's, and `internal` existed purely
to let it. That reader is gone and the popup reproduces the 24 as a USS literal,
so the constant goes back to `private`.

Cost of that, stated plainly: the value now lives in three places — the const,
`.wr-pilechip--left/right`, and `.wr-pilepopup__panel--left/right` — which have
to agree by inspection rather than by the compiler. The alternative is a
C#-side constant that USS cannot read anyway, so the coupling is inherent to
the migration rather than introduced by this choice; the comments on all three
say so.

`PanelBottomY = 220` was **documented as derived** (`180` chip baseline + `28`
half-height + `12` gap) but hardcoded, with a comment on `CombatHud._pileChipYPx`
asking whoever retunes the baseline to sync it by hand. In USS the derivation is
written out beside the literal, the same treatment slice 3's ambush margin got.

Note the actual anchoring behaviour, which the constant name hides: the panel is
**centred 520 × 640** in the prefab, and `AnchorPanelToSide` *re-anchors it to a
bottom corner* on every `Show` — left for the deck chip, right for the discard
chip — so the list appears to grow out of the chip that was clicked. Both the
centred authored position and the corner re-anchor are real; the corner one is
what the player ever sees.

## Nested-instance excision — seventh occurrence

`_pilePopup` is a local ref (no `guid:`), so `PilePopup.prefab` is instantiated
inside `CombatHud.prefab`: `PrefabInstance` block, `stripped` blocks, orphaned
`m_Children` entry. Highest-line-first against a scratchpad backup, with the
structural YAML check before any test run.

`CombatHud._warnedMissingPilePopup` / `WarnMissingPilePopup` go with it — the
warning existed for a stale prefab missing the `_pilePopup` wire, and there is
no wire left to miss.

## Technical Director Review

No TD agent spawned. Executes the pattern proven in slices 1–4 under ADR-0018
P4a. The one architectural decision — folding the popup into the existing panel
rather than giving it its own — is argued above and rests on depth, which is
checkable: the alternative requires inventing a sortingOrder between −10 and 0.

`WastelandRun.UI` names no `CombatView` type. The controller needs
`CombatLoop.DeckCards`, `CombatLoop.Discard`, `HandCardInstance.Definition` and
`CardDefinition`'s derived helpers (`PrimaryDamage`, `ArmorGain`,
`RepairAmount`, `DrawCount`, `HasXxxEffect`) — all in `WastelandRun.Combat`.
This is the first slice where the panel controller touches card *data* rather
than just scalar loop state, and the arrow still holds.

The row follows the house repeated-item pattern (`DraftCardElement`,
`BeaconNodeElement`): a custom `VisualElement` that clones a UXML template into
itself, so designers keep editing the row tree in UI Builder.

## Merge conditions

EditMode ≥1287 with one `[Explicit]` skip, PlayMode ≥17, zero `error CS`, both
result XMLs present, grep-gates clean.

**Playtest:**
- click the DECK chip → popup opens over the HUD, titled DECK, list growing out
  of the **bottom-left**; click DISCARD → same but **bottom-right**
- rows read energy / name / value, value **red** for Attack, **blue** for Plate
  and Repair, **dim** for MOVE / ×N / BUFF
- the energy chip is a **round gold disc** (it was a square before — intended)
- both lists sort by energy, then alphabetically **inside each cost band**
- click the scrim → closes; click the panel or scroll it → stays open
- open a long pile, scroll down, close, re-open → **starts at the top again**
- open the deck popup, then click the discard chip **without closing** → the
  list and title swap in place
- **the risk item:** with the popup open, try to click a vehicle ring or hit
  zone through the scrim. Nothing should respond.
- outcome overlay still covers the popup if combat ends while it is open

## Approval

User approved continuous execution of P4a on 2026-09-13 ("keep going until we
are done").

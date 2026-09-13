# Capture — P4a slice 7: the card hand

**Date:** 2026-09-13
**System:** Combat HUD — UGUI → UI Toolkit (ADR-0018 P4a)

The last and largest piece of P4a. Unlike slices 1–6 this is **a phase, not a
slice**, and it ships as four commits. This file is the capture for all four and
is updated as each lands.

## Why it is four commits

Every previous slice was one widget with one boundary. This one is not:

| | |
|---|---|
| `CardWidget.cs` | 869 lines |
| `HandSequencer` / `HandModelObserver` / `HandEventQueue` / `HandBeat` / `HandEvent` / `HandLayoutEngine` | 307 lines of orchestration around it |

and it carries three problems none of the earlier slices had:

1. **An assembly inversion.** `CardWidget` holds `CombatController` and
   `CombatHud` fields and calls them directly — both `WastelandRun.CombatView`
   types. `WastelandRun.UI` may not reference that assembly, so every one of
   those calls has to become an injected delegate before the visual migration
   can even start.
2. **Coroutines on a non-MonoBehaviour.** `AnimateToDiscard` / `AnimateFromDeck`
   are `IEnumerator`s that `HandSequencer` drives via `StartCoroutine`. A
   `VisualElement` cannot own a coroutine; they have to move to the controller
   and take the element as a parameter.
3. **A different input model.** `IBeginDragHandler` / `IDragHandler` /
   `IEndDragHandler` on the EventSystem become `PointerDown` / `PointerMove` /
   `PointerUp` with explicit pointer capture.

Doing all of that in one commit would mean one un-bisectable change containing a
behavioural rework, an assembly refactor and a visual migration. The split:

| Commit | Content | Risk |
|---|---|---|
| **7a** | delete provably-dead code | none — no callers |
| **7b** | invert the `_hud` / `_controller` couplings, **still UGUI** | behavioural, isolated, independently playtestable |
| **7c** | the UXML/USS element + drag rework | visual |
| **7d** | retire `Card.prefab`, its author method, test rows, dead Hand\* files | cleanup |

7b is the one that matters. Isolating it means that if drag-cast targeting
breaks, the commit that broke it contains *only* the indirection change — not
900 lines of new UI on top.

---

## 7a — dead code removal

### `CardWidget.RenderStatic` — 45 lines, ZERO callers

Its doc comment states:

> Used by `WastelandRun.UI.CardRewardPickerController`, which Instantiates
> Card.prefab purely for its visuals so the reward draft cards stay in lockstep
> with in-hand cards — any tweak to Card.prefab in Prefab Mode propagates
> automatically.

**That consumer does not exist.** ADR-0014 P3 replaced it with
`DraftCardElement`, a native UI Toolkit element that *mirrors*
`CardWidget.BuildValueText` / `BuildInfoText` rather than instantiating the
prefab — its own source comments say so. A repo-wide grep for `RenderStatic`
returns the declaration and its own comment, nothing else.

This is the same phantom-citation pattern caught repeatedly this session: a
comment that was true when written, describing a relationship that was later
severed, left behind as a confident-sounding claim. Checked rather than trusted,
because it would otherwise have shaped the whole slice — a second consumer of
`Card.prefab` outside the combat hand would have forced the migrated card to
keep a display-only render path alive for the reward picker.

`HiddenColor`, `GetFamilySprite`, `BuildInfoText` and `BuildValueText` are all
still reached by the live path and stay.

### `CardOffer` — the UGUI wrapper named in the same comment

`RenderStatic`'s comment also justified the `_controller == null` early-outs in
the pointer handlers by saying "a CardOffer wrapper on the same GameObject owns
click/hover". **No such class exists** — `grep "class CardOffer"` returns
nothing. Every surviving `CardOffer` reference in the repo is
`WastelandRun.Run.CardOffer`, the reward model type, which is unrelated.

The guards themselves stay: the widget pool holds vacant slots, so an unbound
widget is a real state. Only the false explanation goes, with the method.

### Verified still live

Every other public member has real consumers, counted rather than assumed:
`CurrentCard` (4), `HandIndex` (1), `AssignCard` (2), `ClearAssignment` (2),
`IsAnimating` (7), `AnimateToDiscard` (1), `AnimateFromDeck` (2). Nothing else
in the file is dead.

**No authored values are destroyed by 7a** — it removes code with no callers and
no designer-tuned constants. The capture-before-destroy enumeration that slices
1–6 needed begins at 7c, where `Card.prefab`'s authored geometry and the
widget's tuned constants actually move.

---

---

## 7b — the cast seam

Full verdict: `production/td-verdicts/2026-09-13-card-cast-seam.md`. TD returned
**AMEND** and corrected three premises in the brief; all five amendments taken.

`ICardCastService` declared in `WastelandRun.UI`, implemented by `CombatHud`
(CombatView already references UI, so the arrow points the right way and nothing
moves). `CardWidget` now holds `Func<CombatLoop>`, `Action<HandCardInstance>` and
`ICardCastService` instead of `CombatController` / `CombatHud`.

**`CardDefinition.RequiresTarget`** absorbed `CombatHud.TargetingRequiresTarget`,
whose whole body was `TargetMode != TargetMode.Self` — a one-line rule about card
data that was sitting one assembly up, behind a MonoBehaviour.

`CombatHud`'s targeting members renamed to match the interface so implementation
is **implicit**: `TargetingActive`→`IsCasting`, `TargetingBeginCast`→`BeginCast`,
`TargetingUpdateCast`→`UpdateCast`, `TargetingEndCast`→`EndCast`,
`TargetingCard`→`CastingCard`. Five explicit-interface forwarders would have been
ADR-0011 forbidden pattern #1 verbatim — *"methods whose sole purpose is to
translate one vocabulary into another"*.

### The defect the review caught

I described the `_controller` sites as "`.Loop` reads only". Three of them
(`CardWidget.cs:241, 459, 487`) were **Unity-null teardown guards**.
`CombatController.Loop => _loop` is a plain managed field read: on a *destroyed*
MonoBehaviour it neither throws nor returns null, and only Unity's overloaded
`==` detects destruction. A naive `() => _controller.Loop` closure would have
silently dropped that and left the card driving a dead loop through teardown —
no exception, no log. The guards now live inside the closures at the `Bind` call
site, which is the only place they still can.

### What 7b does NOT do

It does **not** make `CardWidget` assembly-portable. The widget still
hard-references `HandLayoutEngine.ComputeSlotTransform`, and
`HandLayoutEngine.ApplyZOrderForWidget(CardWidget, …)` takes a `CardWidget`
parameter — a genuine circular dependency — plus `HandSequencer` and `HandBeat`.
7b inverts the **behavioural** couplings; the **layout** couplings are 7c's
problem. Recorded so 7c is not planned against a false baseline.

### Do not delete this seam

`ICardCastService` is permanent. It differs from the slice-4 callback seam that
slice 5 deleted: that one existed only because slice 4 had landed and slice 5 had
not — it had an expiry date. This one has none. `CombatHud` still owns targeting
at 1.0 and the card element still lives in `WastelandRun.UI`. The aggressive
seam-deletion instinct applied correctly in slice 5 would be wrong here.

### Divergence from the verdict

The TD proposed `TargetingActive` → **`IsActive`**. Used **`IsCasting`** instead:
`CombatHud.IsActive` reads as "is the HUD active" on a MonoBehaviour that does a
dozen unrelated things. Same goal — implicit implementation, zero forwarders —
without the readability regression on the concrete type.

---

## Constants inventory — for 7c, recorded now

Listed here early so the values are captured before the commit that moves them,
and so 7b can be reviewed knowing what 7c will have to preserve.

| Constant | Value | Note |
|---|---|---|
| `PlayableTint` | **(1, 1, 1, 1)** | must be *exactly* white — `Image.color` multiplies the sprite, so anything less washes out the art |
| `UnplayableTint` | **(0.55, 0.55, 0.55, 1)** | ~55% multiply; desaturates without occluding artwork |
| `HiddenColor` | (1, 1, 1, 0) | |
| `HoverLiftPx` | **100** | tuned to *exactly cancel* `HandLayoutEngine.IdleDropOffsetY` so a hovered card lands on its natural arc position — **change both together** |
| `LerpSpeed` | 12 | |
| `SlideInStartX` | −1200 | off-screen-left entry |
| `_castEngageLiftPx` | **120** | ~half a card height — crosshair appears, card visual hides |
| `_castCommitLiftPx` | **200** | ~full card height |
| `CancelBopMinLiftPx` | 30 | floor on the settle so a cancel always reads as a downward "bop" |
| `CancelFadeStartAlpha` | 0.4 | |
| `CancelFadePerSecond` | 6 | ~99% alpha in ~0.7s |
| `ProjectedUpHex` | **#5DFB5D** | amplified damage — bright lime |
| `ProjectedDownHex` | **#FF5C5C** | reduced damage — warm red |

**The engage/commit gap is a hysteresis pair and the reason it exists is
written down:** without the gap, releasing in the band just above the engage
point auto-commits, so a player dragging a card *back toward the hand* to cancel
gets an unintended play instead. Both numbers move together or neither does.

### `Card.prefab` — read from `AuthorCard()`, not from the widget

**The card was resized by designer request.** Authored at 160 × 270, then
**scaled 1.3× to 208 × 351 on 2026-06-04** — the note records that 1.5× was
tried and rejected as too large. **Every child offset and font size below was
scaled 1.3× to match**, which is why they carry two decimal places. These are
not arbitrary-looking numbers to be "cleaned up"; they are a uniform scale
applied to a designer-tuned layout.

| Element | Anchors / pivot | Position | Size | Font |
|---|---|---|---|---|
| root | — | — | **208 × 351** | — |
| Cost | top-left, pivot (0,1) | **(13.49, −9.23)** | 28.05 × 26.61 | **36.4** bold, Left |
| Value | top-right, pivot (1,1) | **(−13.49, −9.23)** | 28.05 × 26.61 | **36.4** bold, Right |
| Name | 0.05–0.95 × 0.55–0.82 | (0, 0) | stretch | **20.8** bold, Center |
| Info | 0.05–0.95 × 0.18–0.45 | **(0, −48.75)** | (0, **−45.5**) | **18.2**, Bottom |

Palette, **designer-baked 2026-05-09**:

| Element | Colour | Note |
|---|---|---|
| Cost | **RGBA(0.7451, 0.3160, 1, 1)** | purple-magenta |
| Value | **RGBA(0.7451, 0.3160, 1, 1)** | *deliberately identical to Cost* — the two corner numbers must read as a matched pair |
| Name | **black** | |
| Info | **RGBA(0.18, 0.18, 0.22, 1)** | |
| background | **white** | must stay white: the Image is real card art and the tint is multiplicative |

The background tint being white is the same constraint as `PlayableTint` above,
stated twice in the original for the same reason — anything other than pure white
washes out the artwork.

### `HandLayoutEngine` — the arc

| Constant | Value |
|---|---|
| `HandCapacity` | **8** |
| `CardSpacingPx` | **180** |
| `CardArcHeightPx` | **35** |
| `CardArcRotationDeg` | **3** |
| `IdleDropOffsetY` | **−100** — parks the hand below its natural arc to free play-field space |

Centering uses the **live hand count**, not `HandCapacity`, so the arc and
rotation tighten around the actual cards as the hand shrinks.

### `HandBeat` — the discard/draw rhythm

| Constant | Value | Rationale as written |
|---|---|---|
| `CardAnimDurationSec` | **0.25** | |
| `HandBeatStaggerSec` | **0.05** | "the cascade-reading floor StS-style hand dumps land on" — one constant for BOTH end-turn cascades and in-turn bursts |

Plus an anti-stall safety timeout (2× anim duration, 1.5s floor) that logs and
continues draining rather than wedging the pipeline.

---

## 7c2 — the migration itself

### Slice plan divergence: 7d folds into 7c2, three commits not four

The plan above split `7c` (the element) from `7d` (retire `Card.prefab`, its
author method, test rows). **That boundary cannot hold, and the reason is
mechanical rather than a preference.** `CardWidget` hard-references
`HandLayoutEngine.ComputeSlotTransform` and `HandLayoutEngine.ApplyZOrderForWidget(CardWidget, …)`
— the circular dependency 7b explicitly recorded as 7c's problem. The moment
`HandLayoutEngine` moves to `WastelandRun.UI` and sheds its element operations,
`CardWidget.cs` no longer compiles. It cannot be left standing for one commit.

And leaving the authored `CardHand` subtree in `CombatHud.prefab` while the new
UI Toolkit hand renders would put **two hands on screen at once** — the
intermediate state would not be playable, which is the property the 7a/7b/7c1
split was bought to preserve. So 7c2 carries the whole cut: new element, pipeline
move, UGUI hand deleted, prefab subtree excised, author methods and test rows
gone. There is nothing left for a 7d.

### Files touched

New:

- `Assets/Scripts/UI/CardHandElement.cs` — one pooled hand slot as a `VisualElement`
- `Assets/Scripts/UI/CardHandView.cs` — the pool, the z-order single writer, the per-frame tick
- `Assets/UI/CardHandCard.uxml` — the card's inner slot tree (body + four labels)

Moved `WastelandRun.CombatView` → `WastelandRun.UI` (via `git mv`, history follows):

- `Assets/Scripts/UI/HandLayoutEngine.cs` — **sheds three methods**; see below
- `Assets/Scripts/UI/HandBeat.cs`
- `Assets/Scripts/UI/HandSequencer.cs`
- `Assets/Scripts/UI/HandEventQueue.cs`
- `Assets/Scripts/UI/HandModelObserver.cs`
- `Assets/Scripts/UI/HandEvent.cs`

Edited:

- `Assets/Scripts/UI/CombatHudPanelController.cs` — hosts the pipeline + coroutines
- `Assets/Scripts/CombatView/CombatHud.cs` — hand ownership leaves
- `Assets/Editor/CombatPrefabAuthor.cs` — `AuthorCard`, `BuildCardLine`, the
  `CardHand` build block, `PatchCardHandCanvasMenu`, `_cards` / `_cardPrefab` wiring
- `Assets/Editor/AuthorCombatHudPanel.cs` — wires `_handCardTemplate`
- `Assets/Prefabs/CombatView/CombatHud.prefab` — `CardHand` subtree excised
- `Assets/Prefabs/UI/CombatHudPanel.prefab` — `_handCardTemplate` ref added
- `Assets/UI/CombatHudPanel.uxml` / `.uss` — the hand layer
- `Assets/UI/Tokens/tokens.colors.uss`, `Assets/Scripts/UI/CardImageRefs.cs`,
  `Assets/Scripts/Combat/CombatLoop.cs`,
  `Assets/Tests/EditMode/Combat/CardPlayabilityPendingTargetTests.cs` — stale
  `CardWidget` citations retargeted (they become phantoms the moment the type dies)
- `Assets/Tests/EditMode/CombatView/CombatWidgetPrefabWiring_Test.cs` — 7 rows removed
- `Assets/Tests/EditMode/Combat/HandObserverQueueDeterminismTests.cs` → moved to
  `Assets/Tests/EditMode/UI/` (its subjects are `WastelandRun.UI` types now, and
  `WastelandRun.Combat.Tests` does not reference that assembly)

Deleted:

- `Assets/Scripts/CombatView/CardWidget.cs` — 833 lines, ported to `CardHandElement`
- `Assets/Prefabs/CombatView/Card.prefab` — its authored values are in this file
  and land in USS

### Authored values destroyed by 7c2

Every value in the **Constants inventory** and the **`Card.prefab`** /
**`HandLayoutEngine`** / **`HandBeat`** tables above is destroyed by this commit.
That inventory was written into this file at 7b time for exactly this moment and
is not repeated here. Where each one lands:

| Source | Destination |
|---|---|
| `Card.prefab` root 208 × 351 | `--wr-size-handcard-*` (already split in 7c1) |
| Cost / Value / Name / Info offsets, sizes, fonts | `.wr-handcard__*` in `CombatHudPanel.uss`, with the 1.3× bake note verbatim |
| Cost / Value / Name / Info colours | already tokens (`--wr-color-card-*`) — verified equal to the floats |
| background white | USS comment on `.wr-handcard`; the tint is multiplicative |
| `PlayableTint` / `UnplayableTint` | `.wr-handcard` / `.wr-handcard.is-unplayable` |
| `HiddenColor` | replaced by `visibility: hidden` — see below |
| `HoverLiftPx` **100** ↔ `IdleDropOffsetY` **−100** | **adjacent**, `CardHandElement` field next to the engine constant's citation, "change both together" intact |
| `LerpSpeed` 12, `SlideInStartX` −1200 | `CardHandElement` documented consts |
| `_castEngageLiftPx` **120** ↔ `_castCommitLiftPx` **200** | **adjacent** serialized fields on `CardHandView`, hysteresis rationale verbatim |
| `CancelBopMinLiftPx` 30, `CancelFadeStartAlpha` 0.4, `CancelFadePerSecond` 6 | `CardHandElement` consts |
| `ProjectedUpHex` / `ProjectedDownHex` | `.wr-handcard__value.is-up` / `.is-down` — **no rich text** |
| `HandCapacity` 8, `CardSpacingPx` 180, `CardArcHeightPx` 35, `CardArcRotationDeg` 3 | unchanged on `HandLayoutEngine` |
| `CardAnimDurationSec` 0.25, `HandBeatStaggerSec` 0.05, `ReflowSettleSec` 0.2, `PipelineAwaitTimeoutSec` 1.5 | unchanged on `HandBeat` |
| hand rect: `CardHandYPx` −80, `HandWidthPx` 1280, `HandHeightPx` 390 | `.wr-cardhand` layer geometry in USS |
| `CardHand` canvas `overrideSorting` / `sortingOrder 25` | **dies with UGUI.** In one tree, "above the vehicle bars" is being a later child; there is no sorting order to keep in sync and `PatchCardHandCanvasMenu` has nothing left to patch |

Three values in `CombatHud` also die, and they are worth naming because they were
a *re-derivation* rather than a source: `PileChipEdgeMarginPx` 24,
`PileChipWidthPx` 70 and `_pileChipYPx` 180 existed only so
`ChipCenterInBottomCenterSpace` could reconstruct, through three coordinate
spaces, a chip position that slice 4 had already authored in USS. The chips are
elements in this same tree now, so `CardHandView` reads `chip.worldBound.center`
— a layout fact — and the reconstruction goes away with its constants.

### The four traps, and where each is closed

1. **The second Y-flip.** `ComputeSlotTransform` emits y-**up** and
   CCW-positive rotation; `translate` / `rotate` are y-**down** and
   CW-positive. **Both flips are in one method**, `CardHandView.ApplyTransform`,
   with a comment saying why they are together: fixing one and missing the other
   mirrors the arc silently. Distinct from the pointer-space flip already on
   `ICardCastService`'s coordinate contract.
2. **`CatchUpToHand()` from the controller's `OnEnable`**, after the element
   re-query — migrated elements are destroyed and re-cloned across a `SetActive`
   cycle, losing every `_currentCard`. Symptom if missed: return from a reward
   overlay, hand is blank.
3. **`HandSequencer.Stop()` in `OnDisable`.** Unity kills coroutines on a
   disabled MonoBehaviour but `_coroutine` stays non-null and `EnsureRunning`
   early-returns on non-null — a mid-combat overlay would leave the hand
   permanently deaf.
4. **`visibility`, not `display:none`**, for the vacant slot and the
   cast-mode hide. `display:none` orphans pointer capture and zeroes
   `resolvedStyle`, and the hide happens *while the element holds capture*.

### Deliberately NOT changed

- **`Resources.LoadAll` + `CardImageRefs` stays** for the per-family body
  sprite. `DraftCardElement` takes its sprites as serialized controller refs and
  its comment explains why for the *picker*; the hand has always used
  `Resources`, `CardImageRefs` names it as a consumer, and swapping the asset
  path inside a migration commit is exactly the undeclared change the USS
  comments keep warning against. If the two should converge, that is its own
  slice.
- **`ICardCastService` is untouched.** The verdict names it as a check:
  unchanged since 7b.
- **The engage/commit pair keeps its serialized-field shape.** They were
  `[SerializeField]` on `CardWidget` so a designer could tune them per-prefab;
  they become `[SerializeField]` on `CardHandView`'s host controller rather than
  `const`, so that affordance survives the migration.

## Technical Director Review

**Full verdict: `production/td-verdicts/2026-09-13-card-hand-7c-design.md`**
(`technical-director` `a4cb6c9bf388e0051` + `unity-ui-specialist`
`a16f6be8268479967`, dispatched in parallel before any code was written;
committed `d6b0660`). Reproduced here because this is the capture the edit is
gated on.

**Verdict: AMEND.** Direction right — move the pipeline to `WastelandRun.UI`,
one panel, keep coroutines — but 7c must *fix* an invariant it planned to
preserve, and one verified defect would have silently destroyed a designer bake.

- **THE HAZARD.** `tokens.spacing.uss` claimed `--wr-size-card-*` 160×270
  "matches the live Card.prefab sizeDelta". `Card.prefab` is **208 × 351**. The
  hand card would have silently shrunk 23% with no compile error. Resolved in
  7c1 by splitting the tokens.
- **Invariant Z3 was already false** — `CardWidget.cs:555` raised the dragged
  card with a raw `SetAsLastSibling()`. 7c1 made the single-writer claim true for
  the first time; 7c2 keeps it true by putting every sibling-order write on
  `CardHandView`.
- **Q1 — pipeline in `WastelandRun.UI`.** Option (B) (leave it in CombatView) is
  disqualified by a shipped-bug mechanism: `CombatHud.HandleOverlayShown` calls
  `SetActive(false)` and the panel is its child, so every overlay cycle re-clones
  the `UIDocument` tree — `CombatHud._cards` would hold `VisualElement`
  references across that cycle and the hand would be dead after the first reward
  picker. `HandLayoutEngine` sheds three element operations before travelling.
- **Q2 — coroutines stay**, hosted on `CombatHudPanelController`. USS transitions
  cannot carry `DrawOne`: `AnimateFromDeck` recomputes its target every frame
  from live `Hand.Count` ("the hand makes room"), and a transition has a fixed
  endpoint. Do NOT narrow `MonoBehaviour runner` — change the instance, not the
  signature.
- **Q4 — unify the text, keep the elements separate.** `BuildInfoText` and the
  `BaseValueText` branch order moved to `CardDefinition` in 7c1. Do NOT unify
  `DraftCardElement` and the hand card: a unified element needs a mode flag
  gating positioning, drag and hover — **ADR-0011 forbidden pattern #3**, created
  by the unification.
- **Q5 — one tree**, hand layer between the chips and the crosshair, so "the
  crosshair paints over the hand during a cast" is free and `BringToFront` on the
  pile popup still works.
- **Q6 — split 7c.** 7c1 prepare (landed, `cd65416`), 7c2 migrate.
- **Acceptance condition carried forward:** every constant in the inventory lands
  in USS or as a documented controller field **with its rationale comment
  verbatim**, and the two coupled pairs land adjacent with the "move together"
  note intact.
- **Standing risk, not a solved item:** `CombatHudPanelController` is ~1016 lines
  and 7c2 adds the pool, the z-order writer and a coroutine host. Above ~1400
  lines, extract a `CardHandView` sub-object the way `PileChipView` was
  extracted. **Taken up-front** — `CardHandView` ships as its own file in this
  commit rather than as a later rescue.

**UI specialist, substantive points taken:** `left`/`top` on reflow and
`translate` per frame; `hierarchy.Insert` is the `SetSiblingIndex` equivalent;
capture on threshold not on down, with `PointerCaptureOutEvent` as the single
source of truth for "drag ended, possibly forcibly"; `_suppressNextClick`
disappears rather than ports (a plain `VisualElement` has no `Clickable`, so
there is no synthesized `ClickEvent` to suppress); `RuntimePanelUtils.PanelToScreen`
is the exact inverse of `ScreenToPanel` — never hand-roll the flip; guard
per-frame `Label.text` writes behind an equality check and move the sprite write
to `AssignCard`. One of its citations
(`CombatHudPanelController.cs:1081-1097 ResolveLayersIfPossible`) was
**fabricated** — the file is 1016 lines and has no such method; the advice it
supported is correct and is what the file already does.

### One specialist claim was FALSE, and it was in the "verified" pile

> `RuntimePanelUtils.PanelToScreen` is the exact inverse of `ScreenToPanel`.
> Never hand-roll the flip.

**There is no `PanelToScreen`.** The compiler rejected it outright — `CS0117:
'RuntimePanelUtils' does not contain a definition for 'PanelToScreen'` — on the
first build of this slice. This was not in the specialist's own list of five
unverified items; it was stated as established API, alongside advice that was
correct, which is exactly what made it cost a build.

Worth noting *why it mattered at all*: the conversion is needed because
`ICardCastService`'s contract is Unity screen space, and it is screen space
because `CombatHud.UpdateTargetingHover` hit-tests the vehicles' **world-space
UGUI** canvases with `RectTransformUtility.RectangleContainsScreenPoint`. The
crosshair half of that seam round-trips (screen → panel) and would be happier
with panel space; the UGUI half cannot be. So the hybrid stack is what forces the
conversion to exist, and it will still force it after P4b.

`CardHandElement.ToScreen` now MEASURES the mapping instead: two probes through
the engine's own `ScreenToPanel` give the scale and sign per axis, and inverting
those is exact for the axis-aligned affine transform `ScreenToPanel` is. Nothing
in it assumes which way y runs or what the panel scale is — it asks. That is
deliberately not the same thing as re-deriving the flip by hand.

**What is NOT established:** that UI Toolkit offers no inverse *anywhere*. Only
the one member was disproved, and by the compiler rather than by enumerating the
type. A throwaway reflection probe was written to settle it and then dropped —
the capture-before-destroy hook gates new editor scripts, and a diagnostic that
exists for one run does not warrant a TD entry to get past it. Left as a small
open item: if a first-class inverse exists, `ToScreen` collapses to one call.

**Still unverified by the specialist — flagged, not treated as fact:**
absolute-position reordering being layout-free in this build, `usageHints`
availability in 6.3, `IPointerEvent.originalMousePosition`'s coordinate space,
whether `display:none` releases pointer capture, `experimental.animation`
stability. 7c2 depends on **none** of them: it reorders by `hierarchy.Insert`,
sets no `usageHints`, reads `evt.position` (documented panel space) rather than
`originalMousePosition`, uses `visibility` rather than `display`, and animates
from the existing coroutines rather than `experimental.animation`.

## 7c2 — LANDED, Unity `ef7b678`

**41 files, 2,406 insertions / 3,520 deletions.** EditMode **1287 / 1286 passed /
0 failed / 1 `[Explicit]` skip**, PlayMode **17/17**, 0 `error CS`, grep-gates
clean — all at baseline.

### Two simplifications the plan did not anticipate

1. **`ApplyZOrderForWidget` has no safe single-element equivalent, so it is
   gone.** The only index-setting route in UI Toolkit is remove-then-insert,
   which detaches the element from the panel for an instant — and `RaiseToTop`
   runs mid-drag on the element holding pointer capture. `BringToFront` reorders
   in place, so the canonical order is expressed as repeated `BringToFront` from
   the rightmost card inward and every caller runs the whole pass. Eight slots, on
   hand mutation only. This also makes invariant Z3 *simpler* to check than the
   TD's proposed grep: it is one method, not one method per call shape.
2. **The `isBusy` predicate injected into `CombatHudPanelController.Bind` is
   gone.** It existed because `HandSequencer` was a CombatView type this assembly
   could not name. It can name it now, so `IsCommitAllowed` reads
   `_handSequencer.IsRunning` directly — the same reasoning that retired the pile
   chips' injected count getter in slice 4.

### Refinement to the verdict's acceptance grep

The TD proposed: *"`grep -n "SetSiblingIndex\|BringToFront\|SendToBack"
Assets/Scripts/UI/` shows writes from exactly one file."* **That predicate is too
broad and would have failed on landing.** `MapViewController` (storm layer, player
marker), `BeaconNodeElement` and `CombatHudPanelController` (pile popup) all call
`BringToFront` legitimately and predate this slice. The checkable claim is
narrower: *sibling-order writes against a `CardHandElement` come from
`CardHandView` alone*, which holds. Verified by inspection of all six remaining
hits.

### Known limit, carried unchanged rather than fixed

`CatchUpTo` does not skip animating slots, and `HandSequencer.Stop()` does not
reach the per-card coroutines `HandBeat` started on the runner. So a loop swap
landing mid-discard can have `AssignCard` fight an in-flight animation. **The UGUI
`CatchUpToHand` had the identical hole** — it is not a regression, and fixing it
inside a migration commit would smuggle a behaviour change into a diff whose
whole claim is visual identity. Noted for the follow-up pass.

## Approval

User approved continuous execution of P4a on 2026-09-13 ("keep going until we
are done"), and delegated slice sequencing ("i trust you go as you recommend").
The 7d-folds-into-7c2 divergence above is the one item that changes the shape of
the agreed plan rather than its content, and is surfaced rather than absorbed.

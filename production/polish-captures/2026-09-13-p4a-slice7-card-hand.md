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

## Approval

User approved continuous execution of P4a on 2026-09-13 ("keep going until we
are done"), and delegated slice sequencing ("i trust you go as you recommend").

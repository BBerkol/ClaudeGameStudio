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
| **7b** | replace the `_hud` / `_controller` couplings with injected delegates, **still UGUI** | behavioural, isolated, independently playtestable |
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

## Approval

User approved continuous execution of P4a on 2026-09-13 ("keep going until we
are done"), and delegated slice sequencing ("i trust you go as you recommend").

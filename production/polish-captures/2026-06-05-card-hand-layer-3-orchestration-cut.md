# Polish Capture: Card Hand Animation System — Layer 3 Orchestration Cut

**Date:** 2026-06-05
**System:** Card combat view layer — five-component re-cut of the hand animation orchestrator (HandModelObserver, HandEventQueue, HandSequencer, HandBeat routines, HandLayoutEngine)
**Supersedes (in part):** `2026-06-05-card-hand-animation-system.md` Layer 2 capture — the binding model + `_hand.Insert(0,…)` semantics + sequenced pipeline philosophy ship forward; the single-class drain implementation is replaced.

**Affected paths:**
- `Assets/Scripts/Combat/CombatLoop.cs` — adds `EndTurnCascadeBegan` + `EndTurnCascadeEnded` events fired around `EndPlayerTurn`/`EndEnemyTurn` boundary so view-side observer can disambiguate end-turn dump from reactive chained discards
- `Assets/Scripts/CombatView/CombatHud.cs` — loses pipeline ownership; retains widget pool, HUD anchor refs, `CatchUpToHand` recovery primitive
- `Assets/Scripts/CombatView/HandLayoutEngine.cs` — NEW. Pure-function arc/Z-order/slot-transform authority. Single writer of `SetSiblingIndex` for hand widgets
- `Assets/Scripts/CombatView/HandModelObserver.cs` — NEW. Only component that talks to `CombatLoop` events. Normalises into typed `HandEvent` records
- `Assets/Scripts/CombatView/HandEventQueue.cs` — NEW. Typed FIFO + cascade-mode flag
- `Assets/Scripts/CombatView/HandSequencer.cs` — NEW. Single coroutine drain loop, single mutator of widget bindings. Owns `_reflowSettleSeconds` SerializeField + all beat-timing constants
- `Assets/Scripts/CombatView/HandBeat.cs` — NEW. Four typed beat routines (`PlayCardBeat`, `DrawCardBeat`, `EndTurnDiscardCascadeBeat`, `EndTurnDrawCascadeBeat`)
- `Assets/Scripts/CombatView/CardWidget.cs` — body untouched except for the `ComputeSlotTransform` call retargeting from `_hud.ComputeSlotTransform` to `HandLayoutEngine.ComputeSlotTransform`
- `Assets/Editor/CombatPrefabAuthor.cs` — Phase 3a consolidation: the four duplicated layout constants (`HandCapacity`, `CardSpacingPx`, `CardArcHeightPx`, `CardArcRotationDeg`) and the inlined arc math in `AuthorCombatHud()` retarget to `HandLayoutEngine.{HandCapacity,ComputeSlotTransform}`. ADR-0011 editor-authoring exception still permits literals here; consolidation is preferred when the editor asmdef already references `WastelandRun.CombatView` (it does). HUD-anchor literals (`CardHandYPx`, `HandWidthPx`, `HandHeightPx`) stay local — they govern the screen-space canvas, not the per-card arc.
- `Assets/Tests/EditMode/Combat/HandPipeline_Determinism_Test.cs` — NEW EditMode test (fixed RunSeed + fixed play sequence → identical binding map across 3 runs)

## Proposed change

Replace Layer 2's single-class pipeline on `CombatHud` with a five-component view-side hand orchestration. **HandModelObserver** subscribes to `CombatLoop` events and translates them into a typed event stream (Discard, Draw, BeginEndTurnCascade, EndEndTurnCascade). **HandEventQueue** is a FIFO of those typed records and owns the cascade-mode flag. **HandSequencer** is a single coroutine that drains the queue and decides per event whether to launch a HandBeat sequentially or as part of a staggered burst — it is the only mutator of widget bindings after construction. **HandBeat routines** are four typed coroutines (`PlayCardBeat`, `DrawCardBeat`, `EndTurnDiscardCascadeBeat`, `EndTurnDrawCascadeBeat`) each owning one atomic animation arc with its own timeout. **HandLayoutEngine** is a pure function returning `(position, rotation, siblingIndex)` from `(modelIndex, handCount)` — the single source of truth for arc layout AND Z-order.

This replaces Layer 2's `PipelineEvent` struct + enum, the `OnCardDiscarded`/`OnCardDrawn` handlers on CombatHud, `EnsurePipelineRunning`, `ProcessEventPipeline`, `DrainDiscardBurst`, `DrainOneDraw`, `ReassignWidgetsToCards`, and the three scattered `SetSiblingIndex` call sites. The model gains two explicit phase-boundary events.

## Final-game picture this serves

Same Slay-the-Spire canonical baseline Layer 2 served — cards visibly *move* between deck, hand, discard, multi-draws read as countable discrete arrivals, end-of-turn is a clean discard-cascade-then-draw-cascade — but with seams clean enough to absorb the next wave of card-flow polish without re-cutting the orchestrator. The HandBeat routines become the single extension point for new animation arcs (cascading-effect chains, reactive discards from buffs, hand-size-changing relics, peek/scry animations): adding "scry-peek-then-bottom" is a new `ScryBeat` method, not a third boolean branch inside a 90-line drain coroutine. HandLayoutEngine as a pure function lets EditMode tests assert layout correctness without instantiating a HUD. End-turn cascade boundary named at the model layer means any future system (replay buffer, telemetry, animation skip-on-mash, undo) can hook the same signal. Per `feedback_demo_forward_over_infrastructure.md` — this is the shape we ship with, not scaffolding.

## Authored values being destroyed (or at risk during refactor)

Every numeric constant and behavior gate tuned across Layer 1 + Layer 2. Each must survive into the new code at the exact value listed.

### CombatHud.cs constants and fields

| Where | Value | Plan | Rationale |
|---|---|---|---|
| `CombatHud.cs:26` `HandCapacity` | `8` | **Preserve** → `HandLayoutEngine.HandCapacity` const | Canonical pool size |
| `CombatHud.cs:27` `CardSpacingPx` | `180f` | **Preserve** → `HandLayoutEngine` | Arc input |
| `CombatHud.cs:36` `CardArcHeightPx` | `35f` | **Preserve** → `HandLayoutEngine` | Parabolic Y peak |
| `CombatHud.cs:37` `CardArcRotationDeg` | `3f` | **Preserve** → `HandLayoutEngine` | Edge tilt |
| `CombatHud.cs:682` `CardAnimDurationSec` | `0.25f` | **Preserve** → `HandSequencer` const | Per-beat duration |
| `CombatHud.cs:687` `EndTurnBurstStaggerSec` | `0.05f` | **Preserve + Rename** → `HandSequencer.HandBeatStaggerSec` | Single stagger constant governs end-turn AND mid-turn multi-draw per user-confirmed Q2 |
| `CombatHud.cs:692` `PipelineAwaitTimeoutSec` | `1.5f` | **Preserve** → `HandSequencer.BeatTimeoutSec` | Per-beat watchdog floor |
| `CombatHud.cs:699` `_reflowSettleSeconds` SerializeField | `0.2f` | **Preserve** → SerializeField on `HandSequencer` MonoBehaviour | Designer-tunable beat; must stay serialized per Layer 2 TD condition 6 |
| `CombatHud.cs:703-709` `PipelineEventKind` + `PipelineEvent` (with `Index` field) | enum + struct | **DELETE.** Replaced by `HandEvent` discriminated record in `HandEventQueue.cs` with four kinds: `Discard`, `Draw`, `BeginEndTurnCascade`, `EndEndTurnCascade`. `Index` field dropped entirely — sequencer reads live position via HandLayoutEngine, never caches event-time index | Cached event-time index is a known correctness hazard inside multi-draw bursts (see Layer 2 `DrainOneDraw` comment at lines 836–846 — the live-IndexOf override was a partial mitigation). Cascade boundary signals supersede the need to cache it |
| `CombatHud.cs:710` `_eventQueue` field | `Queue<PipelineEvent>` | **Move** to `HandEventQueue` | Owned by the queue component |
| `CombatHud.cs:711` `_pipelineCoroutine` field | `Coroutine` | **Move** to `HandSequencer` | Sequencer owns its drain handle |
| `CombatHud.cs:714-723` `OnCardDiscarded` handler | direct enqueue | **DELETE.** Relocate to `HandModelObserver.OnCardDiscarded` | Observer is the sole model-event consumer |
| `CombatHud.cs:725-734` `OnCardDrawn` handler | direct enqueue | **DELETE.** Relocate to `HandModelObserver.OnCardDrawn` | Same |
| `CombatHud.cs:736-741` `EnsurePipelineRunning` | drain starter | **DELETE.** Replaced by `HandSequencer` internal lifecycle | Sequencer self-starts on first enqueue and self-terminates when queue drains |
| `CombatHud.cs:743-761` `ProcessEventPipeline` | drain coroutine | **DELETE.** Replaced by `HandSequencer.DrainLoop` reading cascade-mode flag, dispatching to typed `HandBeat.*` methods | Cleaner dispatch surface; eliminates Peek-then-switch branch |
| `CombatHud.cs:767-818` `DrainDiscardBurst` | burst coroutine | **DELETE.** Replaced by `HandBeat.PlayCardBeat` (single-card path) + `HandBeat.EndTurnDiscardCascadeBeat` (burst path) | Two beats with shared helpers, not one bimodal method |
| `CombatHud.cs:827-873` `DrainOneDraw` | draw coroutine | **DELETE.** Replaced by `HandBeat.DrawCardBeat` (single-card mid-turn or chained-effect path) + `HandBeat.EndTurnDrawCascadeBeat` (burst path) | Same split rationale |
| `CombatHud.cs:879-905` `ReassignWidgetsToCards` | reindex pass | **Move** to `HandLayoutEngine.ReassignWidgetsToCards(IReadOnlyList<CardWidget>, IReadOnlyList<CardDefinition>)`, pure function over passed collections | Layout authority consolidates |
| `CombatHud.cs:856,902,966` scattered `SetSiblingIndex(hand.Count - 1 - idx)` | Z-order writes (3 sites) | **DELETE call-sites, Centralize** as `HandLayoutEngine.ApplyZOrder(widgets, handCount)` invoked at end of `ReassignWidgetsToCards` and end of every `HandBeat` | One Z-order invariant, one writer |
| `CombatHud.cs:909-917` `IndexOfCard` | reference-equality walk | **Move** to `HandLayoutEngine` as static helper | Hand reflow authority |
| `CombatHud.cs:922-930` `FindWidgetByCard` | widget lookup | **Move** to `HandSequencer` scope | Beat utility, not layout |
| `CombatHud.cs:934-945` `FindFreeWidget` | vacant scan | **Move** to `HandSequencer` scope | Same |
| `CombatHud.cs:952-969` `CatchUpToHand` | opening snap (no animation) | **Preserve** on CombatHud, but delegate index/sibling math to `HandLayoutEngine.ReassignWidgetsToCards` | State-recovery primitive stays on HUD |
| `CombatHud.cs:977+` `OnDeckReshuffled` | reshuffle chip animation | **Preserve untouched** | Unrelated to hand pipeline |

### CardWidget.cs constants and behaviors

| Where | Value | Plan |
|---|---|---|
| `CardWidget.cs:38` `PlayableTint` | `Color(1,1,1,1)` | **Preserve verbatim** |
| `CardWidget.cs:39` `UnplayableTint` | `Color(0.55,0.55,0.55,1)` | **Preserve verbatim** |
| `CardWidget.cs:40` `HiddenColor` | `Color(1,1,1,0)` | **Preserve verbatim** |
| `CardWidget.cs:48` `HoverLiftPx` | `24f` | **Preserve verbatim** |
| `CardWidget.cs:49` `LerpSpeed` | `12f` | **Preserve verbatim** |
| `CardWidget.cs:54` `SlideInStartX` | `-1200f` | **Preserve verbatim** |
| `CardWidget.cs:280-329` `RenderStatic` (draft-card path) | static paint | **Preserve untouched** (used by `CardRewardPicker`, not in-hand pipeline) |
| `CardWidget.cs:339-377` `CurrentCard`/`HandIndex`/`AssignCard`/`ClearAssignment` identity API | binding contract | **Preserve verbatim** — HandLayoutEngine builds on this |
| `CardWidget.cs:379-491` `Update()` body | per-frame paint + lerp | **Preserve verbatim** in semantics; only change is `_hud.ComputeSlotTransform(...)` call retargets to `HandLayoutEngine.ComputeSlotTransform(...)` (no semantic change, just authority relocation) |
| `CardWidget.cs:834-847` `PaintForAnimation` | shared anim paint | **Preserve verbatim** |
| `CardWidget.cs:849-897` `AnimateToDiscard` | discard arc coroutine | **Preserve verbatim**, called by `HandBeat.*DiscardBeat` |
| `CardWidget.cs:899-…` `AnimateFromDeck` | draw arc coroutine | **Preserve verbatim**, called by `HandBeat.*DrawBeat` |

### HandLayoutEngine — arc layout formula (relocated from `CombatHud.ComputeSlotTransform`)

| Aspect | Current | Plan |
|---|---|---|
| `t` parameter | `(handCount == 1) ? 0f : (slotIndex / (float)(handCount - 1)) * 2f - 1f` | **Preserve verbatim** — handles 1-card edge case |
| `xPos` | `t * (CardSpacingPx * (handCount - 1) / 2f)` | **Preserve verbatim** — centered around 0 |
| `yArc` | `CardArcHeightPx * (1f - t * t)` | **Preserve verbatim** — parabolic |
| `rotZ` | `-t * CardArcRotationDeg` | **Preserve verbatim** |
| `siblingIndex` (NEW return value) | `handCount - 1 - modelIndex` | **NEW** — derived in the same call so Z-order can never drift from layout |

## Behavioral contracts that must survive

| Behavior | Current path | Plan |
|---|---|---|
| Drag-cast HideVisual handoff (`_hudVisualHidden`) | CardWidget HideVisual line 685 / ShowVisual line 705 | **Preserve** — widget-local, independent of pipeline |
| Drag-back cancel slide (`_slidingBackFromTop` + alpha walk) | CardWidget Update branch | **Preserve** — independent of pipeline |
| Hover lift on playable cards | CardWidget Update lines 417–419 | **Preserve** — widget-owned |
| `IsAnimating` gate | CardWidget Update line 385 | **Preserve** — beats still set it; Update still bails |
| `AnimateToDiscard` target = DiscardChip world pos | Beat passes chip position in | **Preserve** |
| `AnimateFromDeck` source = DeckChip world pos | Beat passes chip position in | **Preserve** |
| Vacant slot paint (HiddenColor + empty text + park) | CardWidget Update lines 395–409 | **Preserve** — widget owns vacant rendering |
| Card sprite swap per family | CardWidget Update line 426 | **Preserve** |
| Cost/Name/Info/Value text repaint | CardWidget Update lines 429–432 | **Preserve** |
| `PaintForAnimation` shared by FromDeck + ToDiscard | CardWidget:834 | **Preserve** |
| End-of-`AnimateToDiscard` hidden state | CardWidget:880-895 | **Preserve** |
| Leftmost-on-top descending Z-order | Currently scattered across 3 `SetSiblingIndex` sites | **Preserve, Move authority** to `HandLayoutEngine.ApplyZOrder` |
| `CatchUpToHand` opening-hand snap (no animation) | CombatHud:952 | **Preserve** as HUD-owned recovery |

## Model contract (CombatLoop)

| Contract | Current | Plan |
|---|---|---|
| `_hand.Insert(0, pulled)` in `DrawForEffect` line 676 | Layer 2 shipped | **Preserve** |
| `_hand.Insert(0, c)` in `DrawHand` line 699 | Layer 2 shipped | **Preserve** |
| `CardDiscarded` → `RemoveAt` → `Resolve` → `_discard.Add` ordering in `PlayCard` | Layer 1 shipped | **Preserve** |
| `DiscardHand` per-card event loop (line 728–731) | Layer 2 shipped | **Preserve verbatim** |
| `DrawHand` per-card event loop (line 694–701) | Layer 2 shipped | **Preserve verbatim** |
| `EndPlayerTurn` calls `DiscardHand()` directly | Current | **Modify**: wrap with `EndTurnCascadeBegan?.Invoke()` immediately after `RequirePhase` check, before `DiscardHand()` |
| New event `EndTurnCascadeBegan` (no args) | — | **Add** to CombatLoop |
| New event `EndTurnCascadeEnded` (no args) | — | **Add**, fired in `EndEnemyTurn` after the new-turn `DrawHand()` completes. The cascade boundary encompasses dump + refill: behavior contract requires both halves to run inside one logical beat, separated only by the sequencer's internal phase gate |

## Risks the implementation must mitigate

| Risk | Mitigation contract |
|---|---|
| **R1 — Beat timeout** | Every `HandBeat.*` coroutine wraps its `widget.IsAnimating` await with `BeatTimeoutSec = max(CardAnimDurationSec * 2 + totalBurstStagger, 1.5f)`; log warn + continue on stall. Per beat, not per-event-batch — a single stuck widget cannot jam the whole hand refill |
| **R2 — No-overlap invariant** | `HandSequencer.DrainLoop` only launches the next beat after the previous beat's coroutine returns plus `_reflowSettleSeconds`. End-turn cascade burst is the one documented exception: dispatched inside a single beat that launches all discarders with `HandBeatStaggerSec` between launches, then awaits the last one |
| **R3 — No-gap invariant** | After every beat, before the settle wait, call `HandLayoutEngine.ReassignWidgetsToCards` to compact remaining cards into contiguous arc slots. Asserted by EditMode test: final state has no widget at index N when index N-1 is vacant |
| **R4 — Cascade boundary leak** | If `EndTurnCascadeBegan` fires but `EndTurnCascadeEnded` never does (model exception during DiscardHand or DrawHand), sequencer auto-clears cascade mode on next PlayerTurn phase entry; log error. Cascade flag never persists across phases |
| **R5 — Mid-cascade reactive discard** | Inside a cascade, a chained effect that fires CardDiscarded gets folded into the burst stagger naturally; outside a cascade, the same event fires its own `PlayCardBeat`. The boundary is what disambiguates the two — Option B (Phase polling) cannot do this disambiguation reliably |
| **R6 — Drag-cast during pipeline drain** | Widget `IsAnimating` gate already protects this. Sequencer additionally checks `widget.IsBeingDragged` (new accessor) before launching `PlayCardBeat` — if user is still dragging, wait one frame |
| **R7 — Determinism** | EditMode test: fixed RunSeed + fixed play+draw sequence → identical widget-to-card binding map at every event boundary, across 3 successive runs. Required in same PR |
| **R8 — Polish loss** | This capture is the safety net. Every constant and behavior above must survive |

## Quality gates

| Gate | Requirement |
|---|---|
| **EditMode tests green** | All existing combat tests pass after refactor; new HandLayoutEngine pure-function tests + HandSequencer determinism test added in same PR |
| **No bridges** | Layer 2's `PipelineEvent` / `PipelineEventKind` / `OnCardDiscarded` / `OnCardDrawn` (on CombatHud) / `EnsurePipelineRunning` / `ProcessEventPipeline` / `DrainDiscardBurst` / `DrainOneDraw` / `ReassignWidgetsToCards` / `IndexOfCard` / `FindWidgetByCard` / `FindFreeWidget` DELETED, not gated, not commented (per ADR-0011) |
| **Single Z-order writer** | `HandLayoutEngine.ApplyZOrder` is the only call site of `SetSiblingIndex` for hand widgets. Grep for `SetSiblingIndex` in `Assets/Scripts/CombatView/` returns only that one line + non-hand UI |
| **`_reflowSettleSeconds` stays SerializeField** | Owned by `HandSequencer`, not promoted to const |
| **End-turn cascade boundary explicit** | `EndTurnCascadeBegan` / `EndTurnCascadeEnded` events on `CombatLoop`; no view-side `Phase` polling for cascade detection |
| **Determinism smoke test in same PR** | Carried forward from Layer 2 TD condition 4 |
| **Visual regression checklist passed** | Walk the behavior contract section in playmode; tick each |

## Technical Director Review

**Verdict:** APPROVE
**Spawned at:** 2026-06-05
**Recommendation called on end-turn boundary signal:** Option A — explicit `BeginEndTurnCascade()` / `EndEndTurnCascade()` on `CombatLoop` model. Reasoning: the cascade boundary is a *narrative property of the turn lifecycle*, not a derivable property of phase transitions. Option B (observer reads `CombatLoop.Phase`) requires the view to infer "the discard burst that just fired is the end-turn dump, not three reactive discards from a chained effect" by watching for `PlayerTurn → PlayerResolve` transitions — and that inference is brittle the moment any future card (a Reflex/Counter-spell-style card, a status that auto-discards on phase change) fires CardDiscarded during the resolve phase. Once the model emits two distinct kinds of discard waves through the same event, the view must distinguish them, and reading phase is an indirect proxy that will leak. Putting the boundary on the model is also cheaper architecturally than it looks: `BeginEndTurnCascade()` is two lines — fire `EndTurnCascadeBegan`, then call `DiscardHand()`; `EndEndTurnCascade()` is one line — fire `EndTurnCascadeEnded` after `DrawHand()`. The model is already the authority on when the dump happens. Naming that authority is honest.

**TD reasoning summary:**

- Layer 2 implemented roughly 70% of the right structure with the right ingredients but the wrong joints. The remaining 30% is the typed `HandEvent` record, observer phase-awareness via explicit cascade boundary, single-writer Z-order, and first-class timeout invariant. Whack-a-mole would not have solved this; clean cut does.
- Five components, single responsibility per component, no shared mutable state outside named owners. Total LOC across new files estimated ~400; deletion from `CombatHud.cs` reclaims ~200, net ~200 LOC growth for substantial clarity gain.
- HandLayoutEngine as a pure function lets EditMode tests assert layout correctness without instantiating a HUD — significant test-cost reduction for future iterations.
- ADR-0011 no-bridges fully honored: deletion list is explicit, no flags, no comments, no parallel paths during phases (Phase 3c has one transitional shim accessor, deleted at end of Phase 3d before commit).
- Performance: neutral. Sequencer yields when idle. Layout engine is value-type math, no allocations.
- Pillar alignment: serves canonical 1.0 shape per `feedback_demo_forward_over_infrastructure.md`; satisfies ADR-0011 no-bridges; preserves designer intent via this capture; enables future card-flow polish (scry, reactive discards, hand-size relics) as additive beats not bimodal-branch tax.

**TD-mandated conditions on approval (all 7 satisfied or owned by implementation):**

1. Polish capture file MUST be written before code touches — **this document satisfies that condition**.
2. Single Z-order writer enforced by grep gate before merge.
3. No bridges — Layer 2 pipeline code DELETED, not gated.
4. Determinism EditMode test in implementation PR.
5. `_reflowSettleSeconds` stays SerializeField on `HandSequencer`.
6. End-turn cascade boundary explicit on model (Option A decided).
7. Single `HandBeatStaggerSec` constant governs end-turn AND mid-turn multi-draw (per user-confirmed Q2 answer).

**TD-defined success criteria:**

1. Playing any card: only that card animates outward. Remaining cards visibly slide into new arc slots with no blank gap. Zero "place back into hand" residual animation on neighbor.
2. Playing a Draw card: discard plays out, hand reflows, then drawn cards fly in from deck chip *one at a time, left-side occupation*, each visible as a discrete arrival.
3. EndEnemyTurn refill of N cards: staggered burst arrives from deck chip, reads as a single coherent "drawing a hand" beat. End-of-turn dump and refill never visually interleave.
4. Drag-cancel still slides back with alpha fade. Hover lift still works. Drag-cast crosshair handoff still works.
5. Z-order: leftmost on top, descending right, never drifts across a 10-turn combat session.
6. Zero "widget content swap" visual events across a 10-turn combat session.
7. Zero blank-gap visual events across 20 mid-hand plays.
8. Frame time during pipeline animations stays under 16.6ms.
9. Determinism: same RunSeed + same play sequence → same final hand state across 3 runs.

## User approval

- Reviewed: 2026-06-05
- Approved by: bertanberkol@gmail.com
- Notes: Approved for full implementation (Layer 3a–3e). End-turn boundary signal Option A confirmed. Single `HandBeatStaggerSec` for end-turn AND mid-turn multi-draw confirmed. All TD conditions binding. Five session edits from 2026-06-05 PM (A–E) remain in-place during phases; cleanly deleted in Phase 3d.

---

## Implementation order (post-approval)

**Layer 3a — HandLayoutEngine extraction (pure function carve-out).**
Files: NEW `Assets/Scripts/CombatView/HandLayoutEngine.cs`; modify `CombatHud.cs` (delete `ComputeSlotTransform`, `IndexOfCard`, `ReassignWidgetsToCards`, scattered `SetSiblingIndex`). CombatHud and CardWidget call the engine. Constants `CardSpacingPx`/`CardArcHeightPx`/`CardArcRotationDeg`/`HandCapacity` move with them.
Leaves working: full Layer 2 pipeline still drives animation; only layout authority moved.
Validation: combat scene play 3 cards + end turn → visual identical to Layer 2 baseline. New `HandLayoutEngine` EditMode tests assert formula correctness on 1, 2, 5, 8-card inputs.

**Layer 3b — Model cascade boundary signals.**
Files: `CombatLoop.cs` only. Add `EndTurnCascadeBegan` + `EndTurnCascadeEnded` events. Fire `Began` at top of `EndPlayerTurn` before `DiscardHand`. Fire `Ended` at end of `EndEnemyTurn` after the new-turn `DrawHand`.
Leaves working: Layer 2 view pipeline ignores new events (no subscribers yet); discard burst still drives off existing `CardDiscarded` sequence.
Validation: EditMode test asserts `Began` fires exactly once per `EndPlayerTurn`, `Ended` fires exactly once per matching `EndEnemyTurn`, ordering holds across multi-turn sequences.

**Layer 3c — HandModelObserver + HandEventQueue + typed HandEvent.**
Files: NEW `HandModelObserver.cs`, NEW `HandEventQueue.cs`. Move subscription wiring from `CombatHud.OnCardDiscarded`/`OnCardDrawn` into observer; add subscriptions for `EndTurnCascadeBegan`/`EndTurnCascadeEnded`. Observer translates each into typed `HandEvent` and enqueues. Layer 2 `ProcessEventPipeline` temporarily reads from new queue's underlying buffer via shim accessor — **this is the only transitional accessor and it goes away in Phase 3d**.
Leaves working: animation drives off legacy pipeline reading new queue.
Validation: enqueue counts match event fire counts; legacy pipeline still animates.

**Layer 3d — HandSequencer + HandBeat routines (THE CUT).**
Files: NEW `HandSequencer.cs`, NEW `HandBeat.cs`. **Delete** from `CombatHud.cs`: `ProcessEventPipeline`, `DrainDiscardBurst`, `DrainOneDraw`, `EnsurePipelineRunning`, `OnCardDiscarded`, `OnCardDrawn`, `_eventQueue` field, `_pipelineCoroutine` field, `_reflowSettleSeconds` field, `FindWidgetByCard`, `FindFreeWidget`, `PipelineEvent`, `PipelineEventKind`. `_reflowSettleSeconds` re-appears as SerializeField on `HandSequencer`. Sequencer's `DrainLoop` reads `HandEventQueue`, branches on cascade-mode flag, dispatches to typed `HandBeat.*` methods. Z-order applied at end of each beat via `HandLayoutEngine.ApplyZOrder`. Shim accessor from 3c deleted.
Leaves working: everything per the behavior contract.
Validation: full visual checklist (play, draw, multi-draw, end-turn dump-then-refill, drag-cast, drag-cancel, hover); zero content-swap events across 10-turn session; zero blank-gap events across 20 mid-hand plays.

**Layer 3e — Determinism EditMode test.**
Files: NEW `Assets/Tests/EditMode/Combat/HandPipeline_Determinism_Test.cs`. Fixed `RunSeed = 12345`, fixed play sequence (play card 0, play card 1, end turn, end enemy turn, repeat 5 turns). Snapshot widget-to-card binding map at every event boundary; assert identical across 3 runs. Also asserts no-gap invariant (no widget at slot N with vacant N-1) and no-overlap invariant (no two widgets share `_handIndex`).
Validation: test green on first run, green on 3 successive runs.

---

## Deletion list (explicit, per ADR-0011)

- `CombatHud.cs:703-709` — `PipelineEventKind` enum + `PipelineEvent` struct (Index field included)
- `CombatHud.cs:710` — `_eventQueue` field
- `CombatHud.cs:711` — `_pipelineCoroutine` field
- `CombatHud.cs:714-723` — `OnCardDiscarded` handler (semantics relocated to HandModelObserver)
- `CombatHud.cs:725-734` — `OnCardDrawn` handler (same)
- `CombatHud.cs:736-741` — `EnsurePipelineRunning`
- `CombatHud.cs:743-761` — `ProcessEventPipeline` coroutine
- `CombatHud.cs:767-818` — `DrainDiscardBurst` coroutine
- `CombatHud.cs:827-873` — `DrainOneDraw` coroutine
- `CombatHud.cs:879-905` — `ReassignWidgetsToCards`
- `CombatHud.cs:909-917` — `IndexOfCard`
- `CombatHud.cs:922-930` — `FindWidgetByCard`
- `CombatHud.cs:934-945` — `FindFreeWidget`
- `CombatHud.cs:699` — `_reflowSettleSeconds` declaration (re-appears on `HandSequencer`, original removed)
- `CombatHud.cs:856,902,966` (and any other `SetSiblingIndex(hand.Count - 1 - …)` call-sites for hand widgets) — consolidated to `HandLayoutEngine.ApplyZOrder` single writer
- `CombatHud.cs:682,687,692` — `CardAnimDurationSec` / `EndTurnBurstStaggerSec` / `PipelineAwaitTimeoutSec` declarations (relocated to HandSequencer; `EndTurnBurstStaggerSec` renamed to `HandBeatStaggerSec`)

No comments saying "moved to X". No deprecation flags. No `[Obsolete]`. Hard cut per ADR-0011.

## What survives untouched from Layer 2

- `CombatLoop.cs:341-360` — PlayCard CardDiscarded-then-RemoveAt-then-Resolve ordering
- `CombatLoop.cs:670-680` — DrawForEffect `_hand.Insert(0, pulled)` + `CardDrawn(pulled, 0)`
- `CombatLoop.cs:691-702` — DrawHand `_hand.Insert(0, c)` + `CardDrawn(c, 0)`
- `CombatLoop.cs:720-734` — DiscardHand per-card event loop
- `CardWidget.cs:280-329` — `RenderStatic` draft-card path
- `CardWidget.cs:339-377` — `CurrentCard`/`HandIndex`/`AssignCard`/`ClearAssignment` identity API
- `CardWidget.cs:379-491` — `Update()` body in its entirety (vacant-slot paint, sprite swap, hover lift, slide-in snap, drag-bail, alpha walk, rotation lerp). Only change is `_hud.ComputeSlotTransform` retargeting to `HandLayoutEngine.ComputeSlotTransform` — no semantic change
- `CardWidget.cs:834-847` — `PaintForAnimation`
- `CardWidget.cs:849-897` — `AnimateToDiscard` body verbatim
- `CardWidget.cs:899-…` — `AnimateFromDeck` body verbatim
- All `CardWidget` constants (`PlayableTint`, `UnplayableTint`, `HiddenColor`, `HoverLiftPx`, `LerpSpeed`, `SlideInStartX`)
- `CombatHud.cs:952-969` — `CatchUpToHand` opening-hand recovery (delegates internally to HandLayoutEngine; public method stays on CombatHud)
- `CombatHud.cs:977+` — `OnDeckReshuffled` reshuffle chip animation (unrelated to hand pipeline)
- Widget pool ownership + HUD anchor refs (DiscardChip/DeckChip world position helpers) on CombatHud
- All Layer 2 tuned numeric values (`0.25` / `0.05` / `1.5` / `0.2` / `180` / `35` / `3` / `8` / `1200` / `24` / `12`)
- The five session edits applied 2026-06-05 PM (A: SetSiblingIndex wave, B: reassign-before-settle, C: deleted slotWillReoccupy snap, D: drop live-IndexOf override, E: diagnostic warning) — they get cleanly deleted during Phase 3d as part of the broader pipeline-code removal; they remain in-place for the interim so the current build is somewhat improved while phases execute

---

## Phase 3a — closed 2026-06-08

**Status:** GREEN. HandLayoutEngine extracted, smell-test eye-check passed.

**One bug surfaced during eye-check and fixed in same phase** (canonical, no bridge):

`CombatLoop.cs:343-365` (`PlayCard`) and `CombatLoop.cs:720-739` (`DiscardHand`) were firing `CardDiscarded` BEFORE removing the card from `_hand`. Unity's `StartCoroutine` pumps the view pipeline synchronously up to its first yield, so `HandLayoutEngine.ReassignWidgetsToCards(_controller.Loop.Hand)` was reading a pre-removal `_hand` and computing stale `HandIndex` values on surviving widgets. Symptom: 3-card hand → play middle → gap stays open, right widget drifts to out-of-bounds arc slot.

**Fix:** invert the order at both sites — `_hand.RemoveAt` FIRST, then `CardDiscarded?.Invoke`. Establishes a real model invariant: **by the time `CardDiscarded` fires, the card is no longer in `_hand`.** No flag, no bridge, no parallel storage. Comment rewritten to reflect Phase 3a's identity-binding view (`FindWidgetByCard` on `evt.Card`); the Index field still carries the played slot for any consumer that needs it. The "RemoveAt before Resolve" constraint (so draws append at correct indices) remains preserved.

This supersedes the "What survives untouched" claim that `CombatLoop.cs:341-360` would survive verbatim — the **ordering** survives in spirit (RemoveAt and Discard-event still bracket Resolve correctly), but the line ordering of those two statements was swapped. The PlayCard fix code is the canonical 1.0 shape and stays through 3b–3e.

## Carry-over items for downstream phases

**For Phase 3c (HandModelObserver + HandEventQueue):**

- **Duplicate-SO identity binding limitation.** Surfaced at eye-check as `[CombatHud] DrainDiscardBurst: no widget found for card 'Weld' (model idx 0) — event silently dropped`. Repro: two `Weld` references in hand simultaneously (one drawn mid-turn during a play-then-draw sequence). `FindWidgetByCard(CardDefinition)` returns the first widget whose `_currentCard == SO`; when the same SO is in the hand twice, the second discard event finds either the first (already-animating) widget or none (after the first cleared). Canonical fix is per-card runtime instance binding: the hand model carries instance IDs rather than raw SO references; events carry the instance ID; widgets bind to instances. This is a CombatLoop shape change and naturally belongs in 3c where the HandEvent record is being typed for the first time. **Do not patch this in 3a with a queue-shim or "find next free Weld widget" heuristic — both are bridges by ADR-0011.**
- The `Index` field on `PipelineEvent` is already on the deletion list for 3c → 3d transition (line 45 above). The instance ID replaces it as the typed event's identity payload.

**For Phase 3d (HandSequencer + HandBeat — THE CUT):**

- **End-turn discard "cascade reads off" — user feedback 2026-06-08.** Current 50ms `EndTurnBurstStaggerSec` produces a "1-then-(2+3) burst" pattern at end-of-turn rather than a smooth left-to-right cascade. The first card discards alone (synchronous coroutine pump after the first `CardDiscarded.Invoke`), then subsequent cards drain as a stagger-burst after the first finishes. User wants the end-turn dump to read as one coherent left-to-right cascade — same per-card spacing throughout, no asymmetric first card. The fix lives in `HandSequencer.DrainLoop` where the cascade-mode flag is read: when `BeginEndTurnCascade` is active, all queued Discard events are batched into a single `EndTurnDiscardCascadeBeat` with uniform `HandBeatStaggerSec` across the entire sequence (no per-card synchronous-pump leak). The model boundary events from 3b are the key — without them, the sequencer has no way to know "this is the dump" vs "this is a single play."
- TD-Q3 designed behavior (single `HandBeatStaggerSec` for both end-turn and mid-turn multi-draw) is still binding. The fix is implementation, not retuning the constant.

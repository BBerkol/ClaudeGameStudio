# P4a Defect-Fix Slice (D1/D2/D3/D6 hardening + drag-only card play)

Date: 2026-09-16

## TD Verdict

**TD-CHANGE-IMPACT: CONCERNS** — all three fixes approved to land, under 14
binding constraints. Scope: remediation of shipped P4a card-hand/targeting view
code (CardHandElement.cs, CardHandView.cs, HandBeat.cs, HandSequencer.cs,
CombatHudPanelController.cs, CombatController.cs, CombatHud.cs,
VehicleBarStack.cs, CombatHudPanel.prefab, CardHandElement_Test.cs). No new
systems. Includes deletion of the tap-to-play path (TryTapPlay + the
_requestPlay delegate chain) per the binding 2026-09-15 user design decision —
drag-out-and-release is the only card-play gesture.

## Context

D5 (softlock) was reproduced 2026-09-16 with Console evidence and decomposes into
D6(a) + D2 + D1. Console showed repeated BeginCast/EndCast pairs with zero
`Play rejected` warnings and zero popups → commits never reached the controller
(`CombatHud.EndCast` silently no-ops when `_targetingHoveredSlotId == null`).
"Couldn't press End Turn AGAIN" → first End Turn's `EndTurnCoroutine` wedged on
the unbounded `IsHandAnimating` waits (CombatController.cs:475/:491) while
`RequestEndTurn` no-ops on the live routine handle (:143).

Specialist brief (unity-ui-specialist, same date) additionally found the
highest-probability wedge mechanism: per-card tweens are fire-and-forget
(`HandBeat.cs:112/:184` discard the `Coroutine` handles), so `HandSequencer.Stop()`
cannot stop them; an orphaned tween finishing after a loop-swap reassignment
silently wipes the reassigned slot.

## TD findings that changed the slice shape

1. `CombatHudPanelController.OnDisable` **already** calls `_handSequencer.Stop()`
   (:572) — dropped from scope. (Verified in main session.)
2. The threshold retune **cannot ship C#-only**: `CombatHudPanel.prefab:74-75`
   bakes `_castEngageLiftPx: 120` / `_castCommitLiftPx: 200`; the author tool
   never sets these fields. Surgical 2-line YAML edit + capture required.
   (Verified in main session.)
3. The hit-target reorder alone does **not** fix the dead chassis middle — a
   chassis-overlapping badge would still beat the structural zone, converting
   "dead" into "silently wrong target". Badge rects must be measured before
   claiming D2 fixed.
4. No new timeout constants anywhere. The hand pipeline's single derived budget
   (HandBeat: duration + stagger + slack) gains a **remediator** (force-settle
   via a stoppable per-element animation handle); CombatController's waits get a
   diagnostic scream only, never a bypass. "Feel values are serialized;
   integrity budgets are derived constants."
5. `try/finally` coroutine pattern adopted **narrowly** as a standard: only for
   coroutines that raise flags other code blocks on; the `finally` calls a
   single named settle method (the coroutine's only terminal path); `try/catch`
   with `yield` does not compile — `try/finally` only; `finally` covers
   exception + Dispose paths but NOT a live wedged tween — the stoppable handle
   covers that half. Both halves ship together. Codification → coding
   standards (lead-programmer), not an ADR.

## Binding constraints (consolidated)

- **A1** Full terminal settle in `finally` via named helpers
  (`SettleAfterDiscard`/`SettleAfterDraw`); delete inline tails; no duplicated
  exit paths (ADR-0011).
- **A2** `try/finally` only — never `try/catch` around `yield`.
- **A3** Two PlayMode tests prove cleanup runs on (a) `StopCoroutine` mid-flight
  and (b) runner disabled mid-flight.
- **A4** HandBeat's existing timeout branches force-settle, not just warn;
  requires element-owned `_animCoroutine` + `AbortAnimation`; warnings name
  element index, card name, elapsed.
- **A5** No timeout/bypass at CombatController.cs:475/:491 — diagnostic
  LogError only, wait preserved.
- **A6** `_endTurnRoutine = null` in `finally` + defensive `OnDisable` reset.
- **A7** **D6 stays OPEN** after this ships — hardening, not root cause. Check
  playtest console for a preceding NullReferenceException.
- **B1** Prefab edit (CombatHudPanel.prefab:74-75) + this capture.
- **B2** Lift measured from a drag-origin snapshot (`_dragOriginY` set in
  BeginDrag), not `_basePosition` minus a hover term; `_basePosition.y`
  preservation comment at CardHandElement.cs:677-682 stays.
- **B3** Starting pair calibration-pending after first feel playtest; 80px
  engage→commit forgiveness gap preserved. (TD recap floated 220/300;
  specialist geometry derived ~400/~480 from the user's HP-bar-line target —
  implementing the measured pair as the start value.)
- **B4** HP-bar line is a tuning landmark named in the tooltip, never a code
  coupling to VehicleBarStack geometry.
- **B5** Reshape CardHandElement_Test.cs:252-253 to sentinel ctor values +
  round-trip; keep the engage<commit invariant; ADD a prefab-vs-initializer
  drift test.
- **C1** Three-tier `_combatHitTargets` order: all zones → all widgets →
  structural; rewrite the VehicleBarStack.cs:630-636 comment to name the i≠j
  case; the "resolve badge to owning slot" alternative is a no-op — dropped.
- **C2** Measure badge rects before claiming the chassis middle fixed; options
  are shrink-badge-hit-rect-to-glyph or badges leave `_combatHitTargets`
  (UX call → user, with measurements). If badges leave, the 2026-09-13 comment
  block at VehicleBarStack.cs:600-615 is rewritten, not annotated.
- **D1** Warn on the silent `EndCast` no-commit branch (CombatHud.cs:849),
  naming card + release position.

## Tap-removal ripple (bound under FIX 2)

`_requestPlay` chain removal spans CardHandElement.cs (:146,:298,:303,:708,
:726-735), CardHandView.Bind (:94,:99), CombatHudPanelController (:234,:405,
:407,:634-640 — the :405 rebuild guard's truth condition must be re-derived,
not clause-deleted), CombatHud.cs:1195-1198. Six stale tap comments
(CardHandElement.cs:563-568,:721-725; CombatHudPanelController.cs:627-631)
ship corrected in the same commit. No test covers tap-play — the comment sweep
is the only guard against a botched removal.

## Exit criteria

EditMode green with counts pasted (baseline 1299/0 + new rows), PlayMode 17/17
plus the two new lifecycle tests, then a playtest exercising (a) engage at the
new threshold, (b) wheel/engine/chassis-middle targeting, (c) two consecutive
End Turns. "Tests green" requires the `failed="0"` line in the same message.

## Three-lens self-audit (TD)

- **Health**: single derived timeout budget stays with the animation duration in
  `WastelandRun.UI` (no second definition in CombatView); ADR-0011 drift
  enumerated (six stale tap comments, duplicated-settle vector foreclosed,
  threshold parallel-storage guarded by the new drift test); subscription
  lifecycle verified clean (CombatHudPanelController.cs:554-572/:604-656).
- **Optimization**: `try/finally` adds zero per-frame allocation. Flagged, not
  fixed: `GetComponentInParent<Canvas>()` inside FindHoverInStack's loop
  (CombatHud.cs:961) — acceptable (drag-only, small N); hoist when next
  touching that function.
- **1.0 survival**: drag-only is the permanent gesture — `_dragOriginY` is the
  shape that survives hover retunes; `AbortAnimation`/`_animCoroutine` is
  canonical for every future hand beat (D7 bundling, exhaust/retain);
  serialized-field seam is the 1.0 home for the thresholds (no SO — no second
  consumer). If C2 forces badges out of hit targets, that ships as the 1.0
  arrangement with the 2026-09-13 comment rewritten.

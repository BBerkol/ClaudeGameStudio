---
name: VPM AC Review (R8 / W9 Revision)
description: Adversarial AC review of vehicle-and-part-mechanics.md W9 revision (R8); 4 blocking issues remain; AC-VPM-H clamp direction is inverted (most critical new finding)
type: project
---

Reviewed vehicle-and-part-mechanics.md W9 on 2026-05-21. Document status: R8 NEEDS REVISION.

## W9 Changes vs R7 Verdict

- AC-VPM-F added — click-through prevention for Remove confirm dialog. Clean for modal scope.
- AC-VPM-G added — light-tier stagger timestamps. Has two blocking gaps: timing model unspecified (fixed-offset vs. chained), clock injection seam not named.
- AC-VPM-H added — cross-field OnValidate guard. CRITICAL: clamp direction inverted. Spec says "reduce by 1" but math requires increase. Test assertion (<= 48) validates broken behavior.
- PVH-1/2/3 added — playtest hypotheses, non-blocking.
- VPM04 assertion body labelling: W9 added numbered assertion references in fallback set, resolving R7 Blocking-1.

## Blocking Issues (4)

1. **VPM-H — Clamp direction inverted**: Spec says "reduce CriticalThresholdPct by 1 until Floor(MaxHp × Pct/100) >= 1." For MaxHp=2, reducing from 49→48→...→1 never satisfies exit condition (Floor always 0). Fix requires INCREASING Pct to >= 50. Test assertion "clamped value <= 48" validates broken behavior. Also: MaxHp=1 degenerate case (Floor always 0 for any valid Pct) not handled — algorithm would loop forever.

2. **VPM-G — Timing model unspecified + clock seam missing**: AC says "card C at 40–60ms" which is only self-consistent if offsets are measured from trigger, not from preceding card. Chained model produces different assertions. Fix: explicitly state "all timestamps measured from ApplyDamage trigger moment." Separately, ±5ms precision is unachievable in NUnit edit-mode without a named IStaggerClock injectable interface — clock seam must be named in the AC.

3. **VPM-A — Event log source class not named**: "OnSlotRepaired BEFORE OnDrawPhaseBegin in event log" — event log undefined, source classes not named. Test cannot subscribe to events without knowing source type (CombatEventBus? ISlotStateNotifier? TurnManager?). Fix: name the source class for both events. Reiterate that UnityEvent is forbidden per technical preferences; these must be C# events or Actions subscribable in edit-mode NUnit.

4. **VPM-A — Forced-pass input block has no AC**: Carried from R6 Blocking-10, R7 Blocking-4, R8. NOT covered by AC-VPM-F (which covers modal dialog, not forced-pass window). Requires dedicated AC (suggest AC-VPM-I): during forced-pass window, card play / relic activation / manual turn-end must each be rejected. Fix: add explicit assertions for all three rejection paths.

## Recommended Issues (2)

1. **BLOCKED labels (VPM04/VPM05/VPM09)**: No prerequisite story ID, no unblock trigger condition, no gate-level declaration. Fix: each BLOCKED annotation must specify prerequisite task, unblock condition (e.g., "IDrawPile.DrawPileCount exists in model layer"), and gate level (implementation may proceed; Done gate blocked until assertion verifiable).

2. **VPM-B — Affirmative cost assertion missing**: Carried from R7 Advisory-4. Only negative assertion present ("card Y must NOT cost BaseCost + 2"). Broken implementation returning BaseCost for all cards passes. Fix: add "card Y must cost exactly BaseCost + 1."

## Advisory Issues (3)

1. **VPM-F — Cancel semantics underspecified**: "Cancel fires" does not distinguish event dispatch vs. visual state transition. A broken implementation that destroys dialog without invoking CancelCallback passes. Fix: "CancelCallback must be invoked exactly once before dialog is destroyed."

2. **VPM-E — CI category unresolvable**: "economy-invariants category" has no NUnit attribute or CI command specified. Fix: specify `[Category("economy-invariants")]` attribute and CI run command.

3. **VPM-C — Dead-vehicle play state has no named observable**: "No dead-vehicle play state" is not queryable without naming a state enum or scene property. Fix: specify e.g. `GameStateManager.CurrentState != GameState.Combat`.

## Clean ACs

VPM01, VPM02 (formula + refund), VPM03 (boundary + float-cast), VPM04 (assertion body now labelled), VPM05 (body), VPM06, VPM07, VPM08, VPM09 (element routing pending), VPM-B (formula), VPM-C (sequencing intent), VPM-D (all cases), VPM-E (OnValidate body), VPM-F (modal scope).

## Most Dangerous Gap

**AC-VPM-H clamp inversion**: Both the algorithm and the test assertion are wrong in the same direction. An implementation following the spec verbatim produces Critical state permanently empty for MaxHp=2, and the test still passes. This is a silent false-pass on a core damage state invariant.

**Why:** Full adversarial re-review of W9 revision for R8 verdict.
**How to apply:** Block any story touching cross-field OnValidate guard until VPM-H clamp direction is corrected in both algorithm and assertion. Block VPM-A forced-pass story at Done gate until input-block AC is added. Block VPM-G stories until timing model is specified and clock seam is named.

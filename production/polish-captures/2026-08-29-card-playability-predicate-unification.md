# Capture — Card playability: one rule, two doors

**Date:** 2026-08-29
**System:** `CardEffect` / `CardDefinition` / `CardWidget` playability gate
**Severity:** Live player-facing bug (bounded) + blocks the lane-gate feature
**Status:** IMPLEMENTED + VERIFIED 2026-08-29 — see §8.

---

## 0. Files touched

| File | Change |
|---|---|
| `Assets/Scripts/Combat/CardEffect.cs` | + `GetStructuralFailureReason(CombatLoop)` virtual + `CanResolvePendingTarget`; overrides on `WeaponAttackEffect` + `RepairEffect` |
| `Assets/Scripts/Combat/CardDefinition.cs` | + `IsPlayablePendingTarget(CombatLoop)`; xmldoc naming it the single source of truth for greying |
| `Assets/Scripts/CombatView/CardWidget.cs` | `IsPlayable` body (`:813-835`) collapses to phase + energy + delegation |
| `Assets/Tests/EditMode/Combat/CardPlayabilityPendingTargetTests.cs` | NEW — 9 tests |

Read-only during this change (cited, not edited): `Combat/CardPlayContext.cs`,
`Combat/CombatLoop.cs`, `CombatView/CombatController.cs`.

**ADRs at risk:** ADR-0011 (this REMOVES a #2 parallel-storage violation; adds
no bridge, no bimodal path, no compat overload — `IsPlayablePendingTarget` is a
sibling projection of the same rule, not an adapter over it). ADR-0002
(engine-free Combat assembly — the new API takes only `CombatLoop`, no Unity
types). No SO change, no save-format impact, no new asmdef.

## 1. The defect

`CardDefinition.IsPlayable` has **zero production call sites**. The only
consumers of the model gate (`GetFailureReason` / `CanResolve`) are
`CombatLoop.PlayCard` (`:377`, `:452`).

The view layer uses a **private reimplementation** at
`CardWidget.cs:813-835` that branches on derived predicates
(`HasAttackEffect` / `HasRepositionEffect` / `HasRepairEffect`) instead of
walking `Effects`. Its own comment says "mirrors" three times.

**They have drifted, in both directions.** The widget ADDS Phase + EnergyCost
checks the model gate lacks, and OMITS `PlateEffect`'s gate entirely.

> **Live consequence: Weld renders fully lit and draggable while the Engine is
> offline.** `PlateEffect.GetFailureReason` (`CardEffect.cs:106-111`) requires
> `IsEngineOnline`; the widget never checks it. Dragging throws
> `InvalidCardPlayException` at `CombatLoop.cs:380`.
> **3 of the 13 starter cards.**

**Bounded severity.** `CombatController.cs:368` catches it, and the effect gate
runs BEFORE the energy spend (`CombatLoop.cs:373-384`), so the card bounces
with a message — not lost, not a crash.

**The root cause is structural, not a typo.** No branch in the widget ⇒ falls
through to `return true`. **Every new `CardEffect` subtype is invisible to the
UI by default and reads as always-playable.** ADR-0011 #2 (parallel storage of
one rule).

## 2. Why the obvious fix is wrong

**REJECTED:** route the widget through one shared
`GetCardFailureReason(card, ctx)` (the `technical-director` proposal).

`CardPlayContext.Self => default`, so `TargetSlotId` is null. Then
`WeaponAttackEffect` returns `"no target selected"` and `RepairEffect` the same
— **every attack and repair card would grey out permanently.**

**That is why the duplicate exists.** Pre-target greyout (card resting in the
hand, before a drag has picked a target) is a genuinely different question from
commit-time validation. Not laziness. Do not "just unify them."

Caught by the `unity-specialist` pass; confirmed by reading
`CardPlayContext.cs`.

## 3. The change — one rule, two doors

1. `CardEffect.GetStructuralFailureReason(CombatLoop loop)` — virtual, default
   `=> GetFailureReason(loop, CardPlayContext.Self)`. Already correct for
   `PlateEffect`, `RepositionFlipEffect`, `RepositionToEffect`, `DrawEffect`,
   `BuffEffect` — none of them read `ctx`.
2. Override in **only** `WeaponAttackEffect` and `RepairEffect`: keep their
   structural checks (`IsSlotOnline`, `HasAnyOfflineNonFrameSlot`), skip the
   `TargetSlotId` check.
3. `CardDefinition.IsPlayablePendingTarget(loop)` — mirrors `IsPlayable` but
   calls the structural door.
4. `CardWidget.cs:813-835` body collapses to phase + energy + one delegating
   call.

**The property that makes this the right shape:** a new effect type gets correct
greying **by default**. The opt-out lives beside the effect's own
`GetFailureReason`, not 700 lines away in a view file.

## 4. What is destroyed

`CardWidget.IsPlayable`'s per-effect-type checklist (`:815-834`) — the
Reposition / Repair / Attack branches and their explanatory comment block.
Deleted, not left dormant (ADR-0011). The knowledge survives as the effects'
own `GetStructuralFailureReason` overrides.

**Deliberately NOT in scope:** phase + energy remain checked in the widget
alongside the delegating call. They are duplicated with `CombatLoop.PlayCard`
(`:357`, `:385`), but they are NOT the bug class here — they cannot silently
miss a new effect type. Folding them in means moving `PlayCard`'s ordering
into a shared method, which is a larger change with its own ordering risk.
**Logged as follow-up, not silently left.**

## 5. Not optimising the reason strings

`GetFailureReason` runs only from `PlayCard` — click-bounded, well under
1 KB/s. The per-frame path (`CardWidget.Update` → the widget's bool predicate,
~8 widgets × 60fps) allocates nothing. The structural split preserves that: the
widget's hot path still returns bool. **Do not pool or cache the strings**;
correctness is the defect, not GC.

## 6. Test — seen failing on the defect

`Assets/Tests/EditMode/Combat/CardPlayabilityPendingTargetTests.cs`.

Because the new API does not exist on HEAD, "seen failing" is performed per
`feedback_prove_test_fails_on_the_bug` by first implementing
`PlateEffect`'s structural door as the **old widget behaviour** (no engine
check), confirming red on the Weld case, then correcting it.

Required failure signature: Weld + engine offline reports playable-pending-target
`True` when it must be `False`.

## 7. Why now

Prerequisite for the direction-locked rare-weapon cards
(`project_parts_progression_intent`). A lane-gated card would render lit in the
wrong lane and throw on drop — the same bug class, newly authored. Fixing the
predicate first means the feature cannot ship broken.

---

## 8. Outcome — verified 2026-08-29

**EditMode 1172 / 1170 / 0 failed / 2 skipped** (baseline was 1163; +9 = the new
fixture). **PlayMode 3/3.** 0 `error CS`.

### 8a. Seen-failing — TWO deliberate probes, both surgical

The new API did not exist on HEAD, so "seen failing" was performed by injecting
the two defects the fixture is meant to catch and confirming exactly which tests
went red. 6 of 9 stayed green under both probes — the fixture discriminates,
it does not blanket-fail.

| Probe | Injected defect | Went red |
|---|---|---|
| **1** | `PlateEffect.GetStructuralFailureReason => null` — reproduces the old widget, which had no Plate branch | `Weld_WhenEngineOffline_IsNotPlayable` **(the live bug)** · `StructuralGate_NeverContradicts_FullGate_OnceTargetIsSupplied` |
| **2** | `WeaponAttackEffect` structural door delegates to the full gate with `CardPlayContext.Self` — reproduces the REJECTED unification | `Attack_WithNoTargetPicked_IsPlayable_BecauseTargetIsNotAStructuralGate` |

Probe 1 red-ing the generic drift test as well as the specific Weld test is the
useful signal: `StructuralGate_NeverContradicts_FullGate` catches the *class*
(structural door disagreeing with the rule it projects), not just this instance.

Probe 2 matters because it locks the wrong fix out of the codebase. Without that
test, a future reader "simplifying" the two overrides away would grey out every
attack and repair card and the suite would stay green.

### 8b. Delta from the plan

`CardWidget.IsPlayable` was **kept as a method** rather than deleted outright —
it still owns the phase + energy checks and delegates the per-effect rules. Its
comment block was rewritten from a list of mirrored rules into an instruction
not to reintroduce per-effect checks there, with the reason (a missing branch
reads as always-playable, so the failure is silent).

### 8c. Hook interaction worth remembering

`td-review-required.sh` blocked the `CardDefinition.cs` edit: a capture only
satisfies it if the body literally names the file. §0 (Files touched) was added
in response. **Name every touched file explicitly in future captures** — the
hook does substring matching, not intent matching.

---

## Technical Director Review

> Verdict carried forward from the 2026-08-29 four-agent pass on the lane-gate
> proposal, where this defect was found. TD ruled **CONCERNS** on the proposal
> and identified the parallel predicate as the blocking issue:

**"`CardDefinition.IsPlayable` has zero production call sites... That is
ADR-0011 forbidden pattern #2 (parallel storage of the same rule) shipping
today. Any new effect type is invisible to it by construction: a direction-locked
card would render fully lit and draggable in the wrong lane, then throw
`InvalidCardPlayException` at commit."**

TD's ruling on layering is adopted: **the model-side re-check at commit is
correct and stays** — `WastelandRun.Combat` is engine-free with public API and
direct test callers; it cannot trust a UGUI widget. The defect is two
*predicates*, not two *calls*.

**TD's proposed unification is NOT adopted** — it breaks pre-target greyout
(§2). This is recorded as a correction, not a disagreement about the goal:
TD identified the right problem and the right layering, and missed that
`CardPlayContext.Self` carries a null target.

TD's ordering ruling (condition-only effects declared first so first-failure-wins
is authored rather than accidental) is **deferred** — no condition-only effect
ships in this change. It becomes live with the first lane-gated card.

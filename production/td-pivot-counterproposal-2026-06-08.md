# Counterproposal — Layer 3 closeout as Milestone 0

**Date:** 2026-06-08
**Author:** technical-director (consultative)
**Scope:** One specific dissent against `td-build-order-2026-06-08.md` Milestone 0. The rest of the pivot (kill combat-demo backlog, build outward from canonical run-loop systems, no bridges) I endorse without reservation.

---

## The dissent

I put Layer 3 (card hand orchestration cut) closeout as Milestone 0 because you stated the "no dangling work" rule and the Phase 3a work is in flight. The pivot doc honors that.

But the failure-mode section I wrote in the inventory ("subsystem perfectionism in isolation," "polish refactors are concrete in a way new systems are not") applies to Layer 3 itself. Specifically:

**Layer 3 is exactly the work shape that just got you stuck.** It is:
- Bounded to one existing system (combat view).
- Demo-able in isolation (eye-check protocol on `production/session-state/active.md` lines 67–80).
- Justified as "ships forward to final game" (capture line 27 — "this is the shape we ship with, not scaffolding").
- Has zero dependency on a paper MVP system going from paper to built.

That is four-for-four on the pattern I named in the inventory. The single fact that distinguishes Layer 3 from the combat-demo backlog is that Layer 3 work is **already partially complete** — Phase 3a shipped, awaiting eye-check.

If the question is "do we sink 2–4 more focused days into finishing Layer 3 in canonical shape, OR do we ship Phase 3a as-is and pivot immediately to Milestone 1?" — I think there is a real argument for the second path that I owe you.

## What the trade-off actually is

### Option A — Finish Layer 3 (the recommendation in the build-order doc)
- **Pro:** Honors "no dangling work" rule. Eliminates one source of future re-touch on `CombatHud.cs`. Closes a capture cleanly. Determinism test lands.
- **Pro:** Layer 3 IS canonical. The 5-component shape is correct. Future card-flow polish (scry, reactive discards, peek/bottom) lands as `HandBeat.ScryBeat` instead of a third boolean in a 90-line drain.
- **Con:** 2–4 days of work on the system you've been stuck in. Same brain, same files, same context.
- **Con:** Phase 3a already shipped. The marginal value of Phases 3b–3e is *cleaner internals*, not visible behavior. The player sees zero difference.
- **Con:** Reinforces the muscle that says "finish this combat thing before moving outward." The pattern you flagged today.

### Option B — Ship Phase 3a as-is, pivot to Milestone 1 immediately
- **Pro:** Maximum momentum into the actual pivot. The polish-capture failure mode does not get one more practice rep.
- **Pro:** The current state (Phase 3a done, Layer 2 single-class pipeline still partially present in `CombatHud.cs`) is *functionally fine* — it works, tests pass, eye-check approved.
- **Con:** `CombatHud.cs` is left in a hybrid state. HandLayoutEngine is canonical, but the queue + drain + reassign code Layer 2 introduced is still alive. **This is a bridge by ADR-0011's definition.**
- **Con:** Future card-flow polish (Milestone 6 timeframe) will re-touch this same code, likely with stale context. The work *will* be done eventually.
- **Con:** Violates your stated rule. If "no dangling work" is binding, Option B is off the table.

## Where I land

**Option A is correct under your stated rules.** The "no dangling work" rule is not new — it's the same rule that produced the Capture-Before-Destroy + Technical Director Review hook on this project. Half-shipped polish that gets re-touched later is the failure mode that hook was built to prevent.

But I want you to *see* that I'm recommending Option A *because of your rule*, not because Layer 3 closeout is the highest-ROI next move on its own merits. If you ever loosen the rule — "I want maximum pivot velocity this week, leave Layer 3 hybrid, pick it up in Milestone 6" — I would not push back on that. Option B is a real option. The rule is what makes it not the right one today.

## What I'd do if I were you

Finish Phase 3b → 3e per the capture. Eye-check. Commit. Close the capture. **Then immediately open the Save ADR for Milestone 1 — do not let the same brain that was just inside `CombatHud.cs` decide what to look at next.** The risk after finishing Layer 3 is that the brain looks around the same room for the next thing to clean up — and there are 27 `BuildLegacy` / `WireLegacyRefs` / `BuildIconLegacy` matches across the view layer that would happily absorb the next two weeks. (Milestone 6 explicitly defers them; the brain may not.)

The mechanical action that protects the pivot is: **the commit message that closes Layer 3 must be immediately followed by opening the Save ADR file in your editor.** No browsing, no audit, no "let me look at one more thing in CombatHud." If you do that, Option A and Option B converge to the same outcome — clean Layer 3 closure, clean pivot to run-loop construction.

## Summary

- The build-order doc's Milestone 0 stands.
- But Layer 3 closeout *is* the same work shape that just got us stuck — only barely defensible because (a) it's already 80% done, and (b) your "no dangling work" rule binds it.
- The real protection against pivot-failure is what you do *immediately after* the Layer 3 commit lands. The build-order doc names Milestone 1's first prerequisite — the Save ADR (Q1). That should be the very next thing you open. Not a sweep, not an audit, not another combat-view item.

If I'm wrong about any of this, I'd rather hear it now.

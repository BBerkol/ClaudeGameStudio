# TD Verdict — ADR-0018 P4a slice 7b: card → cast-service seam

**Date:** 2026-09-13
**Topic:** Inverting `CardWidget`'s couplings to `CombatHud` / `CombatController`
so the card can become a UI Toolkit element in 7c
**Reviewer:** `technical-director` agent (agentId `a5baa970649fc910c`)

**Files touched:** `Assets/Scripts/UI/ICardCastService.cs` (new),
`Assets/Scripts/Combat/CardDefinition.cs`,
`Assets/Scripts/CombatView/CardWidget.cs`,
`Assets/Scripts/CombatView/CombatHud.cs`,
`Assets/Scripts/CombatView/AttackStateController.cs`,
`Assets/Scripts/CombatView/CombatController.cs`

## The problem

`CardWidget` holds `CombatController` and `CombatHud` fields and calls them
directly. Both are `WastelandRun.CombatView` types; `WastelandRun.UI` does not
reference that assembly. The couplings must invert before 7c can move the card.

## Technical Director Review

**Verdict: AMEND** — the interface is justified and is *not* an ADR-0011 adapter
layer, but three premises in my brief were wrong and one proposed member did not
belong on the interface.

### Three corrections to my brief, all verified against the files

1. **"`_controller.Loop` — reads only, 11 sites."** Wrong. There are 13
   `_controller` sites and three of them (`CardWidget.cs:241, 459, 487`) are
   **Unity-null teardown guards**. `CombatController.Loop => _loop`
   (`CombatController.cs:79`) is a managed field read — reading it on a
   *destroyed* MonoBehaviour neither throws nor returns null. Only
   `_controller == null`, via Unity's overloaded `==`, detects destruction. A
   naive `() => _controller.Loop` closure silently discards that and
   `CardWidget.Update` keeps driving a stale loop through scene teardown. **This
   was the one real latent defect in the proposal.**

2. **"ONE implementer and ONE consumer."** Wrong. The targeting session already
   has three readers beyond `CardWidget`: `AttackStateController.cs:96,98`
   (`TargetingActive`, `TargetingCard`), `CombatController.cs:161`
   (`CurrentDragCastCard`), and `CombatHud.cs:633` (its own ESC handler calls
   `TargetingEndCast`). Drag-cast is already a shared multi-reader session, which
   strengthens rather than weakens the case for a named type.

3. **"No existing code moves."** Only true if the interface member names match
   `CombatHud`'s. My proposal renamed them, and C# has no implicit implementation
   across a name change — so as written it forces five explicit-interface
   forwarders whose sole purpose is translating one vocabulary into another.
   That is **ADR-0011 forbidden pattern #1 verbatim.** Avoidable, and avoided.

### Q1 — Premature abstraction? No.

`feedback_gdd_verb_signature_not_load_bearing` ("defer interface design until 2+
consumers; call the lower primitive") targets inventing a verb layer over
primitives you *could* have called directly. It presupposes the primitive is
reachable. Here it is not — this is a dependency **inversion**, so the option set
is {interface, delegates}, not {interface, call the primitive}. The rule is
silent on that axis.

Verified precedent, same shape, both with a single implementer:
`Run/NodeEncounter/IEventPresenter.cs` ← `CombatView/EventModalHost.cs:47`, and
`Run/NodeEncounter/ICombatDispatcher.cs` ← `CombatView/RunSceneHost.cs:46`. This
is the third instance of an established pattern.

**Where the rule DOES bite:** `RequiresTarget`. Its whole body is
`TargetMode != TargetMode.Self` (`CombatHud.cs:959-963`), and `CardDefinition`
lives in `WastelandRun.Combat`, which `WastelandRun.UI` already references. That
member is a verb wrapper over a reachable primitive. It moves (Amendment 3).

### Q2 — The split is defensible, but the rule must be stated

Not "interface for 5, delegates for 2". The rule is:

> **Interface when the callee holds state across calls and call order is an
> invariant. Delegate when the call is stateless and order-free.**

Drag-cast qualifies: `_targetingCard` (`CombatHud.cs:934`) is session state,
`Begin → Update* → End` is a documented lifecycle (`CombatHud.cs:907-912`), and
`IsCasting` is a query *on* that state used by the card's own cancel path
(`CardWidget.cs:576`). `Loop` and `RequestPlay` have neither.

A struct-of-delegates would be strictly worse — it erases the lifecycle
invariant and the name, and allocates five closures per `Bind` instead of one
reference.

`Func<CombatLoop>` rather than a `CombatLoop` value is **required, not
stylistic**: `CombatHud.cs:644` (`CombatLoop current = _controller.Loop; if
(current == _subscribedLoop) return;`) proves the instance is replaced per
combat.

### Q3 — Not an adapter layer

ADR-0011 forbidden #1 is *"types whose sole purpose is to translate one
vocabulary into another"* — canonically `LegacyKindBridge` mapping `slotId` →
`LegacySlotKind`. `ICardCastService` introduces **no second vocabulary**: same
types, same semantics, same single implementation. It relocates a declaration
across an assembly boundary, and it is **permanent by design** — it does not
exist to be deleted later, which is the defining property of a bridge.

This differs from the slice-5 callback seam that was deleted: that one existed
only because slice 4 had landed and slice 5 had not. It had an expiry date. This
one has none — `CombatHud` still owns targeting at 1.0, and the card element is
still in `WastelandRun.UI`. **Do not delete this one on the same instinct.**

### Q4 — Naming

`Host` in this codebase means *a MonoBehaviour that contains a thing*
(`EventModalHost`, `RunSceneHost`). `CombatHud` does not host the card — the card
drives it. The convention is a role noun for what the callee does
(`-Presenter`, `-Dispatcher`, `-Source`). The codebase already supplies the
word: `CombatHud.cs:907` calls it the *"drag-to-cast targeting service"*.

**`ICardCastService`.**

### Q5 — Standalone commit: keep it

7b is compile-enforced and behaviour-preserving by inspection; 7c is a ~900-line
rewrite with new event plumbing and a new coordinate space. Separating those risk
classes is the bisect value. After 7b the game is fully playable on the UGUI
hand — a valid bisect point a squashed 7b+7c would not provide.

### Framing correction for the capture

7b does **not** make `CardWidget` assembly-portable. It still hard-references
`HandLayoutEngine.ComputeSlotTransform` (`CardWidget.cs:299, 803, 814`) and
`HandLayoutEngine.ApplyZOrderForWidget(this, …)`, which takes a `CardWidget`
parameter (`HandLayoutEngine.cs:116`) — a genuine circular dependency —
plus `HandSequencer` and `HandBeat`. 7b inverts the **behavioural** couplings;
the **layout** couplings are 7c's problem. Stated so 7c is not planned against a
false baseline.

## Amendments accepted

1. **No forwarders** — rename on `CombatHud` so the interface names are
   canonical and implementation is implicit.
2. **Preserve the Unity-null guard inside the closures**, not just the
   `.Loop` read.
3. **`RequiresTarget` moves to `CardDefinition`**; `CombatHud.TargetingRequiresTarget`
   is deleted; interface drops to four members.
4. **Hoist the `Func` to one invocation per `Update`** rather than five.
5. **Capture wording** corrected per the framing note above.

## Divergence from the verdict, and why

The TD proposed renaming `TargetingActive` → **`IsActive`**. Rejected in favour
of **`IsCasting`**. `CombatHud.IsActive` reads as "is the HUD active" on a
MonoBehaviour that does a dozen unrelated things; `CombatHud.IsCasting` is
unambiguous, and reads equally well through the interface. This preserves the
amendment's actual goal — implicit implementation, zero forwarders — while
fixing a readability regression the rename would have introduced on the concrete
type.

Final member set: `IsCasting`, `BeginCast`, `UpdateCast`, `EndCast`.
`TargetingCard` → `CastingCard`, and it stays **off** the interface: no UI-side
consumer needs it yet, and adding it speculatively is the thing this project
treats as the defect.

## Three-lens self-audit (from the verdict, condensed)

- **Health:** ADR-0011 checked by grep against the real ADR text, not assumed.
  Precedent verified against actual implementers. One latent defect found and
  named (the Unity-null closure). No surface growth on `CombatHud` — it gains an
  interface and loses a member.
- **Optimization:** two closures per widget at `Bind`, `BuildCardHand`-scoped —
  once per combat, ~5–10 widgets, zero per-frame allocation. Interface dispatch
  adds one indirection on seven *cold* pointer-event sites, not in `Update`.
  Explicitly **not** worth optimizing further; caching `CombatLoop` in a field
  was considered and rejected as faster-and-wrong (`CombatHud.cs:644`).
- **1.0 survival:** all four members survive. `EndCast`'s `bool` return is
  load-bearing at `CardWidget.cs:572-590` (fired → snap home; not fired → cancel
  bop). Nothing here is 7c scaffolding, which is exactly why the slice-5 delete
  instinct must not fire on it. The real 1.0 risk is the **coordinate space** —
  UGUI screen coords (bottom-left) vs UI Toolkit panel coords (top-left) — so the
  contract is written onto the seam where 7c's author will read it.

**We'll know this was right if:** after 7c, `git log -p` on `CombatHud.cs` shows
no forwarding methods added, `ICardCastService.cs` is unchanged from its 7b
form, and the crosshair lands on the cursor on the first playtest rather than
mirrored vertically.

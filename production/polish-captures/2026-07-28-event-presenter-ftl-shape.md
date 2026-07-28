# 2026-07-28 — Event Presenter FTL-Shape Refactor

## Change summary

The 2026-07-27 Node Encounter Event slice shipped with a Convert-only
`IEventOfferResolver`; the other three payload kinds (Windfall, Treasure,
Ambush) commit their outcome synchronously with no UI. User surfaced this as
"i see them but nothing fires. it fades out and fades back in to map" and
followed with the design pivot: **every event opens a dialogue modal, choice
drives outcome**, FTL-shaped (short beats, per-choice mechanical outcome),
with room to grow one AWD-style depth axis later (single good-faction
reputation feeding boss + gated events).

Refactor pivots the presenter contract from "Convert async gate" to
"universal FTL-shape presenter" and installs the seams needed for the
faction-rep + volatility-register axes without shipping those systems today.

## Authored values / shape being destroyed

- **`IEventOfferResolver.cs`** — deleted. Signature was
  `Present(PendingEventOffer offer, Action<int> onResolved)`, Convert-only.
- **`EventModalHost.HandleConvertChoicePicked`** — deleted. Convert-specific
  choice-index-to-input mapping (`choice 0 → offer.MaxInput, else → 0`)
  moves from host to `EventHandler.DispatchConvert`.
- **`EventModalHost.FindConvertPayloadFor(direction)`** — deleted. No
  longer needed because caller now passes the exact payload reference.
- **`EventHandler.DispatchWindfall`** — silent grant + immediate Resolve
  replaced with `presenter.Present(payload, null, choice => grant+Resolve)`.
- **`EventHandler.DispatchTreasure`** — same as Windfall.
- **`EventHandler.DispatchAmbush`** — silent combat dispatch replaced with
  `presenter.Present(payload, null, choice => combatDispatch → Resolve)`.
- **`DialogueSceneController.Bind` callback type** — `Action<int>` →
  `Action<IDialogueChoiceData>` per TD 1.0-survival note. Enables per-choice
  data-flag lagging deps (faction affinity, volatility register, gated-by-rep)
  without a second signature change. `IDialogueChoiceData` is the POCO seam
  from `IDialogueChoiceData.cs`; `DialogueSceneController` builds a
  projection at click time.

## Being added

- **`IEventPresenter.cs`** — new. Single method:
  `Present(IEventPayloadData payload, PendingEventOffer convertOffer, Action<DialogueChoiceSO> onChoicePicked)`.
  `convertOffer` nullable — non-null only for Convert kinds. POCO/engine-free
  per ADR-0002 (lives in `WastelandRun.Run.NodeEncounter`).
- **`DialogueChoiceSO._nextScene`** — optional `DialogueSceneSO` reference.
  Data-flag lagging dep for FTL two-page events. Ships wired to null on
  every existing choice; controller re-Binds when a picked choice has
  NextScene non-null, else fires terminal callback.
- **`DialogueSceneController` NextScene chain** — one-hop chaining inside
  the controller; hosts stay content-blind.

## Files touched

- **delete** `Assets/Scripts/Run/NodeEncounter/IEventOfferResolver.cs`
- **new** `Assets/Scripts/Run/NodeEncounter/IEventPresenter.cs`
- **new** `Assets/Scripts/Run/NodeEncounter/IDialogueChoiceData.cs` — POCO
  seam mirroring the `IEventPayloadData` / `EventPayloadDefinitionSO` split.
  Required because the POCO Run assembly (`noEngineReferences: true`) can't
  reference `DialogueChoiceSO`. Ships with one field (`Index`); future
  faction/register fields extend without touching the presenter interface.
- **edit** `Assets/Scripts/Run/NodeEncounter/EventHandler.cs`
  (ctor param + all 4 Dispatch* methods)
- **edit** `Assets/Scripts/CombatView/EventModalHost.cs`
  (implements IEventPresenter; drops FindConvertPayloadFor;
  drops HandleConvertChoicePicked)
- **edit** `Assets/Scripts/UI/DialogueSceneController.cs`
  (callback type change + NextScene chain)
- **edit** `Assets/Scripts/Run/Authoring/DialogueChoiceSO.cs`
  (add `_nextScene` field + Configure overload)
- **edit** `Assets/Tests/EditMode/Run/NodeEncounter/EventHandler_Dispatch_Test.cs`
  (FakeOfferResolver→FakePresenter with new signature; Windfall/Treasure/
  Ambush tests now expect presenter.Present + choice callback)
- **zero asset changes** — every payload already carries a `_dialogue` ref
  and every Scene_* has ≥1 authored Choice_*.

## Technical Director Review

**Verdict:** APPROVE. Ship as designed, with the `Action<DialogueChoiceSO>`
swap folded in and two small guards.

**(a) Bimodal parameter — NOT an ADR-0011 violation.** Optional-context-
carried-on-payload is a normal signature; the *storage* isn't parallel
(one interface, one impl, one call path). ADR-0011's bimodal-path pattern is
about runtime branches picking between two storage shapes or two adapter
layers — this is "sticker copy needs the wallet cap number, only Convert has
a wallet cap." Two methods would be worse: it forces `EventHandler` to
bimodally dispatch on kind at the call site, which IS an ADR-0011 smell.
**Guard:** name it `convertOffer` (not `offer`) so nullability intent reads
locally.

**(b) POCO cast — acceptable.** `EventModalHost` is the Unity-side seam by
definition; that's where SO awareness legitimately lives. Alternatives are
worse: an `id string` lookup rebuilds `FindConvertPayloadFor` under a new
name (the thing you're deleting), and a sibling seam for "give me the
dialogue for this payload" is one-method-per-payload-kind ceremony for zero
polymorphism benefit. `IEventPayloadData` staying dialogue-free preserves
`noEngineReferences: true`. **Guard:** `EventModalHost.Present` should
hard-fail (`throw`, not silent no-op) if the cast fails — a non-SO payload
reaching the modal is a bug, not a fallback path.

**(c) NextScene null field — data-flag lagging dep, correct call.** Matches
`feedback_data_flag_lagging_dependency` exactly: the field's *end-state
value* ships today (null = terminal), no code rips out when chain assets
land. Contrast: a TODO or `if (SupportsChains)` toggle would be
pre-abstraction. The controller null-check is a one-line branch, not
scaffolding.

**Self-audit:**

- **Health:** Confirmed no drift — no bridges, no bimodal storage,
  `FindConvertPayloadFor` deletion tightens the surface. Watch:
  `_resolvedGuard` re-entry on Ambush is load-bearing since choice callback
  fires *before* combat dispatch — verify existing guard covers the
  presenter→combat→outcome window, not just presenter→outcome.
- **Optimization:** Presenter call is per-encounter (single-digit per run),
  not per-frame. No allocation concern. No delta.
- **1.0 survival:** `IEventPresenter(payload, convertOffer, onChoicePicked)`
  is the shipping shape — multi-choice branching, NextScene chains,
  AWD-style faction/register axes all extend without signature growth. One
  risk: if future events need per-choice *outcome data* (not just index),
  you'll want `onChoicePicked` to carry a `DialogueChoiceSO` reference, not
  `int`. Cheap to change now, painful later. **Recommend: change callback
  to `Action<DialogueChoiceSO>` in this slice.** Index-based is fine for
  today but locks out per-choice payloads (grant amounts, combat variants,
  faction affinity) that FTL-shape events want within 2-3 slices.

**Go, with the `Action<DialogueChoiceSO>` swap folded in.**

## Guards applied per TD

1. Interface parameter named `convertOffer` (not `offer`) — nullability
   intent reads locally.
2. `EventModalHost.Present` throws `InvalidOperationException` on non-SO
   payload cast fail — not a silent auto-decline.
3. Choice callback type swapped from `Action<int>` to
   `Action<DialogueChoiceSO>` — 1.0-survival future-proof.
4. `_resolvedGuard` verified to only be set by terminal `Resolve()`, not
   by presenter callback intermediate steps — Ambush chain safe.

## Design note (user 2026-07-28)

User confirmed FTL as the baseline shape with one AWD-style depth axis
kept simple: a single good-faction reputation that pays off in the boss
fight and occasional gated events. Conversations stay short
(roguelike-appropriate), with light sociology cues where volatile choices
usually sting but rarely pay off in the right rare context.

Faction affinity + volatility register are NOT wired today (no consumer).
Natural seam when the good-faction slice starts: add a
`_factionAffinityDelta` field to `DialogueChoiceSO` (data-flag lagging dep),
same slot the callback swap enables.

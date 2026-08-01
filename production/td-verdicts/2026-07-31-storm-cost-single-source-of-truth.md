# TD Verdict — Storm Cost Single Source of Truth

**Date:** 2026-07-31
**Source:** Live bug — event choice commit did not decrement storm counter on
node map or event modal header.
**Supersedes (partial):** `production/td-verdicts/2026-07-29-storm-preview-arc-crescent.md`
Amendment 1's Route-D two-field decision (per-choice cost lived on
`EventPayloadDefinitionSO._perChoiceStormCost[]` mirror). Route D's mechanic
survives; its storage shape is retired.
**Files touched:** `IEventPayloadData.cs`, `IDialogueChoiceData.cs`,
`EventPayloadDefinitionSO.cs`, `EventHandler.cs`, `DialogueSceneController.cs`,
`NodeEncounterDataInitializer.cs`, `DialogueChoiceSO.cs`, `IStormAdvancer.cs`,
`RunSession.cs`, `EventHandler_Dispatch_Test.cs`

---

## Problem

Two parallel spellings of the per-choice storm delta:

1. `DialogueChoiceSO._stormCost` — authored on every biome-1 choice
   (values 0..3 across six shipped assets).
2. `EventPayloadDefinitionSO._perChoiceStormCost[]` — index-parallel array on
   the payload SO; **empty on all four shipped Event assets** (never populated
   in `NodeEncounterDataInitializer`).

`EventHandler.ReadPerChoiceStormCost` indexed the payload array with a
choice-based lookup, so an unpopulated payload silently returned zero and the
storm counter never decremented on commit. The intended safety net —
`AssertChoiceStormCostMatchesPayload` — was documented in the class docstring
but never implemented (forbidden pattern #6, stub returns).

ADR-0011 forbidden pattern #2 (parallel storage). Even if the mirror were
populated, drift between the two spellings would grant the wrong cost silently.

---

## TD Verdict

**ACCEPT — retire `PerChoiceStormCost` payload-side mirror; consolidate to
`DialogueChoiceSO.StormCost` via `IDialogueChoiceData.StormCost` (already
present, already read by presentation layer).**

This is a clean ADR-0011 drift resolution, not a new refactor. The
"display-only mirror kept in sync by designer discipline + validator that was
never implemented" is textbook forbidden-pattern #2 (parallel storage) and #6
(stub returns — the validator that promised sync). Fixing the symptom via a
Refresh Copy pass would leave the trap armed: the next authoring session that
touches only the choice-side field would silently regress.

**Accepted shape:**

- `IEventPayloadData.PerChoiceStormCost` removed. Docstring paragraph about
  Route D's payload-side table retired; the mechanic now reads through
  `IDialogueChoiceData.StormCost` (single source, engine-free).
- `IDialogueChoiceData` gains `int StormCost { get; }` — the interface member
  `EventHandler.ReadPerChoiceStormCost` reads. `DialogueSceneController.CommitTerminal`
  snapshots `terminalChoice.StormCost` alongside `RewardCount` and threads
  it into the `DialogueChoiceProjection` inner class (parallel to the P1-5
  RewardCount pattern). Ships engine-free — `int` on the interface, no
  Unity types leak.
- `EventPayloadDefinitionSO._perChoiceStormCost` field + property + OnValidate
  branch removed. Removes the unused `System.Collections.Generic` using.
- All four `ConfigureX(...)` builders drop the `perChoiceStormCost` parameter.
- `EventHandler.ReadPerChoiceStormCost(IEventPayloadData, IDialogueChoiceData)`
  collapses to `ReadPerChoiceStormCost(IDialogueChoiceData) => choice?.StormCost ?? 0`.
  4 call sites updated to drop the payload argument.
- `NodeEncounterDataInitializer` drops `perChoiceStormCost: new[] { ... }`
  from all four `ConfigureX` calls. Sweep validator block (never-implemented
  stub reading `payload.PerChoiceStormCost`) deleted — verified genuinely a
  stub with no sibling real assertions per Amendment 2.
- `DialogueChoiceSO._stormCost` tooltip rewritten: drop "Display-only mirror —
  the actual applied storm delta lives on the paired EventPayloadDefinitionSO"
  language and the reference to the nonexistent
  `AssertChoiceStormCostMatchesPayload` validator. Field is authoritative.
- `IStormAdvancer.cs` + `RunSession.cs` docstring cross-references to
  `EventPayloadDefinitionSO.PerChoiceStormCost` retargeted to
  `IDialogueChoiceData.StormCost`.
- `EventHandler_Dispatch_Test.cs` fixtures updated: `FakePayload` drops
  `PerChoiceStormCost`; per-choice storm cost tests drive via `FakeChoice.StormCost`.

**ADR alignment:** ADR-0011 no-bridges (eliminates parallel storage +
never-implemented validator stub). ADR-0002 engine-free POCO:
`IDialogueChoiceData.StormCost` is already `int` on the interface — no Unity
types leak into the Run POCO assembly. ADR-0015 narrowing preserved: payload
SO stays the vocabulary for Windfall/Convert/Ambush/Treasure mechanics; the
storm cost is a per-choice knob and moves to the choice contract where it
belongs.

**Risk:** Low. The `StormCost` field is already authored on every shipped
`DialogueChoiceSO` in `Assets/Resources/Run/Events/Biome1/Choice_*.asset`
(Ambush_Fight=2, Cache_Pry=3, Cache_Blast=1, Scavenger_Commit=2,
Scavenger_Skip=0, Wreck_Take=2). The payload-side mirror was never populated,
so no data migration is required — deleting the parallel storage IS the fix.

**Self-audit (three lenses):**

- *Health:* Removes an actively broken parallel-storage trap. Reduces
  authoring surface (one field to author per choice, not two).
- *Optimization:* Removes an index-parallel array allocation per Event asset
  and a per-choice list read on the commit hot path.
- *1.0 survival:* This trap would have re-fired every time a designer added a
  new Event/choice pair. Ship-quality unification, not exploratory.

**Amendments:**

1. Update `production/td-verdicts/2026-07-29-storm-preview-arc-crescent.md`
   with a superseder note so Route-D's two-field decision is retired without
   losing the historical context.
2. Before deleting the `NodeEncounterDataInitializer` sweep validator block,
   confirm it is genuinely a stub (no real assertion siblings). If any
   real per-payload assertion lives inside the same block, preserve those.

**Verdict:** Ship it.

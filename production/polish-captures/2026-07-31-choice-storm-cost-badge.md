# Choice Storm-Cost Badge — Capture Before Destroy

**Date**: 2026-07-31
**Slice**: Per-choice storm cost visible on dialogue choice buttons (pre-commit cost affordance)
**Files at risk**:
- `Assets/Scripts/Run/Authoring/DialogueChoiceSO.cs` (add serialized field + Configure param + xmldoc)
- `Assets/Editor/NodeEncounterDataInitializer.cs` (pass `stormCost` per choice + OnValidate cross-check)
- `Assets/Scripts/UI/DialogueSceneController.cs` (render cost pill in `BuildChoiceButton`)
- `Assets/UI/DialogueScene.uss` (add `.wr-dialogue-choice-cost*` classes)
- `Assets/Resources/Run/Events/Biome1/Choice_*.asset` (bake `_stormCost` field into 6 assets)

## Context

User feedback 2026-07-31: "if an option is costing storm counter we should show that. right now the buried cache has 2 choices work the hatch open and blow but it does not show the cost of it on the button."

Storm cost per choice is authored on `EventPayloadDefinitionSO._perChoiceStormCost[]` (Route D, TD verdict 2026-07-28) — mechanically applied by `EventHandler.Dispatch*` after each choice commits. But that value is **not surfaced** on the dialogue choice button pre-commit — designers have been baking `(Storm +N)` parenthetically into `_tooltip` (see `Choice_Cache_Pry.asset` line 16: `_tooltip: Patient. Bigger haul. (Storm +3)`).

The 2026-07-28 verdict `production/td-verdicts/2026-07-28-node-encounter-storm-cost-route-d.md` shipped the storm-counter widget in the dialogue header ("current/max") and explicitly noted "waiting adds risk that the mechanic ships without the surface the player uses to reason about their cost/reward decision." The counter shows *how many ticks you have* — it does NOT show *how many this choice costs*. The Buried Cache event is the first authored event with meaningfully divergent per-choice costs (3 vs 1) — the invisibility of the delta breaks the intended risk/reward decision surface.

## Values being surfaced (baked into `_perChoiceStormCost[]` today, mirrored to `DialogueChoiceSO._stormCost` in this slice)

| Choice asset | Storm cost |
|---|---|
| `Choice_Wreck_Take.asset` | 2 |
| `Choice_Scavenger_Commit.asset` | 2 |
| `Choice_Scavenger_Skip.asset` | 0 (no pill shown) |
| `Choice_Ambush_Fight.asset` | 2 |
| `Choice_Cache_Pry.asset` | 3 |
| `Choice_Cache_Blast.asset` | 1 |

Existing parenthetical "(Storm +N)" text is retained in `_tooltip` for hover, but the primary surface is now the on-button pill.

## What is NOT being touched

- `Assets/Scripts/Run/Authoring/EventPayloadDefinitionSO.cs` — mechanical source-of-truth stays intact. `_perChoiceStormCost[]` is still the value `EventHandler.Dispatch*` applies via `IStormAdvancer.AdvanceStormFromEvent`.
- `Assets/Scripts/Run/NodeEncounter/EventHandler.cs` — dispatch logic unchanged.
- `Assets/Scripts/Run/NodeEncounter/IStormAdvancer.cs` — seam unchanged.
- Storm counter widget (`storm-counter-*` in `DialogueScene.uxml` + `StormCounter.uss`) — visible pill in header stays as-is.
- `EventModalHost.cs` — no storm-cost plumbing added; presenter reads directly from `DialogueChoiceSO`.
- Storm map visuals (`StormFrontElement`, `StormPreviewBandElement`, `MapViewController`) — Slice 2 territory.

## Content-blindness (AC-EV13 grep gate)

`DialogueSceneController` continues to consume only `DialogueChoiceSO` / `DialogueSceneSO` / `IDialogueChoiceData`. `_stormCost` is a plain `int` on the presentation SO — no `IScrapEconomy` / `BeaconType` / `EventPayloadKind` / `IStormAdvancer` reference. Same shape as the existing `_rewardCount` int display mirror (see `DialogueChoiceSO.cs:71-98` pre-emption note). Gate stays green.

## Split-brain risk mitigation

The mechanical value lives on `EventPayloadDefinitionSO._perChoiceStormCost[]`; the display value now lives on the paired `DialogueChoiceSO._stormCost`. Designer discipline keeps them in sync (same accepted pattern as `_rewardCount` today). Extra safety net: `NodeEncounterDataInitializer.AssertKindCoverage()` gets a sibling `AssertChoiceStormCostMatchesPayload()` sweep — walks each biome-1 `EventPayloadDefinitionSO`, cross-references its `Dialogue.Choices[i]._stormCost` against `_perChoiceStormCost[i]`, `Debug.LogError`s on mismatch. Runs at the same menu entry point (Generate / Refresh); catches drift at editor time, not at play time.

## Technical Director Review

**Verdict:** APPROVE Slice 1 as spec'd. Option B (SO-side display mirror) is correct — matches the `_rewardCount` precedent already accepted in the same file. Option A (Bind param from host) splits button label and cost across two SOs and puts display in `WastelandRun.UI` scope sourced from `EventPayloadDefinitionSO` in `Run` scope — worse layering, not better.

Required addition: OnValidate cross-check on `NodeEncounterDataInitializer` (Generate/Refresh menu) to warn on `_stormCost` vs `_perChoiceStormCost[i]` mismatch. Closes the split-brain risk that's the only real Option A advantage.

**Three-Lens Self-Audit:**
- *Health*: ADR-0011 clean — `_stormCost` is a real int carrying end-state value (data-flag lagging pattern), NOT a bridge stub. Follows precedent (RewardCount). Grows `DialogueChoiceSO` field count by 1 within its own shape.
- *Optimization*: One extra Label + USS class per choice, once per `Bind`. Trivial. No per-frame hot-path change.
- *1.0 survival*: `_stormCost` int is the canonical 1.0 shape. When the choice-as-source-of-truth unification lands (noted in `_rewardCount` xmldoc), `_stormCost` becomes source-of-truth alongside `_rewardCount` — no signature churn. The OnValidate guard survives 1.0 as a designer-safety net.

**Bundle discipline:** SPLIT from Slice 2 (storm preview live-track). Different files, different lens, different capture. Ship Slice 1 first.

**Load-bearing paths:**
- `Assets/Scripts/Run/Authoring/DialogueChoiceSO.cs` — field + Configure param
- `Assets/Scripts/UI/DialogueSceneController.cs:402` — `BuildChoiceButton` renders the pill
- `Assets/Editor/NodeEncounterDataInitializer.cs` — bake + validator
- `Assets/UI/DialogueScene.uss` — cost-pill classes (mirror `.wr-dialogue-continue-reward*` but red)
- 6 choice `.asset` files — `_stormCost` field bake

**Not attested EditMode-green** — no `run-tests.ps1` in either repo (see prior session state note). Slice ships as UXML/USS + SO + controller wiring; existing `EventHandler_Dispatch_Test` coverage of `_perChoiceStormCost[]` is unchanged (mechanical path untouched).

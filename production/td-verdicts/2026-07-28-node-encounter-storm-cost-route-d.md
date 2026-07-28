# TD Verdict — Node Encounter Storm-Cost (Route D) + UI Punch-List Second Pass

**Date:** 2026-07-28
**Scope:** Second TD pass on the Node Encounter dialogue slice bundle. Reaffirms Route D shape for per-choice storm cost, sequences the remaining work into three executable blocks, triages the UI specialist punch list, and pre-empts one shape drift on `DialogueChoiceSO`.
**Files under review:** `IStormAdvancer.cs` (new), `RunSession.cs`, `EventHandler.cs`, `EventPayloadDefinitionSO.cs`, `IEventPayloadData.cs`, `EventModalHost.cs`, `RunSceneHost.cs`, `EventHandler_Dispatch_Test.cs`, `NodeEncounterDataInitializer.cs`, `DialogueSceneController.cs`, `DialogueScene.uxml`, `DialogueScene.uss`, `DialogueChoiceSO.cs`, `Biome1Distribution.asset`.

---

## Context

First pass verdict green-lit Route D:

> **Route D** — Per-choice storm cost lives on the host-side `EventPayloadDefinitionSO` as `PerChoiceStormCost[]` (parallel to a new `PerChoiceScrapReward[]`). The content-blind `DialogueSceneController` stays clean (no mechanical vocab per grep gate AC-EV13). A new one-verb seam `IStormAdvancer` is injected into `EventHandler`; each `Dispatch*` calls `stormAdvancer.AdvanceStormFromEvent(payload.PerChoiceStormCost[choice.Index])` after mechanical resolution. `RunSceneHost` implements `IStormAdvancer` and delegates to `RunSession.AdvanceStormFromEvent(int)`, which mirrors the existing `AutoAdvanceStrandedStorm()` walk (advance counter → walk cursor → fire `OnStormAdvanced` + `OnStormEngulfed`).

Between the two passes, the UI specialist landed a 9-item hygiene punch list on the dialogue modal, and re-reading `DialogueChoiceSO` surfaced one shape concern about the new outcome-reveal stinger. Second pass triages both and confirms the execution order.

---

## Technical Director Review

### Route D — reaffirmed

Nothing in the second read changes the Route D verdict. The seam holds because:

- **Content-blind rule preserved.** `DialogueSceneController` sees `IDialogueChoiceData` only. Storm cost lives on the mechanical payload sibling (`EventPayloadDefinitionSO.PerChoiceStormCost`), read by `EventHandler` (host-side). Grep gate AC-EV13 stays green — no `IScrapEconomy` / `BeaconType` / `EventPayloadKind` / `ConvertDirection` / `IStormAdvancer` refs inside `WastelandRun.UI`.
- **One verb per seam.** `IStormAdvancer.AdvanceStormFromEvent(int ticks)` is the minimum surface. Not `IStormControl` with three methods — one verb, one caller, one implementer. ADR-0011 §exception-4 fit (composition seam, not adapter).
- **POCO purity preserved.** `IStormAdvancer` lives in `WastelandRun.Run.NodeEncounter` (POCO namespace, no Unity types). `RunSession` (already POCO) implements it directly — no MonoBehaviour bridge. `RunSceneHost` re-exposes it view-side purely because `EventModalHost` builds `EventHandler` from view-side context; the delegation is `_session.AdvanceStormFromEvent(ticks)` verbatim.
- **Data-flag lagging dependency clean.** `PerChoiceStormCost[]` and `PerChoiceScrapReward[]` ship as real int[] fields carrying end-state values today. If a future encounter shape needs a fifth or sixth per-choice number (part-drop chance, injury roll), we add another field — no bridge rip-out. Memory `feedback_data_flag_lagging_dependency` satisfied.

**Verdict:** Ship Route D as spec'd. No pivot.

### Block execution order

The bundle splits cleanly along dependency lines. Execute in this order — do NOT interleave, each block validates before the next starts:

**Block A — Storm verb + payload wiring** (mechanical core, no UI):

1. Create `Assets/Scripts/Run/NodeEncounter/IStormAdvancer.cs` (single-verb POCO interface).
2. Edit `Assets/Scripts/Run/RunSession.cs` — add public `AdvanceStormFromEvent(int ticks)` mirroring `AutoAdvanceStrandedStorm()` walk; make `RunSession : IStormAdvancer`.
3. Edit `Assets/Scripts/Run/Authoring/EventPayloadDefinitionSO.cs` — add `private int[] _perChoiceStormCost` + `private int[] _perChoiceScrapReward`, matching Configure() optional params, expose `IReadOnlyList<int>` properties.
4. Edit `Assets/Scripts/Run/NodeEncounter/IEventPayloadData.cs` — expose the two new `IReadOnlyList<int>` properties.
5. Edit `Assets/Scripts/Run/NodeEncounter/EventHandler.cs` — 4th ctor param `IStormAdvancer stormAdvancer`; each `Dispatch*` reads `payload.PerChoiceStormCost[choice.Index]` (guard: skip when array empty or index out of range) and calls `stormAdvancer.AdvanceStormFromEvent(ticks)` AFTER mechanical resolution. Treasure/Windfall paths respect `PerChoiceScrapReward[choice.Index]` when non-empty; else fall back to the SO-level `ScrapAmount` (backward compatible with single-choice events).
6. Edit `Assets/Scripts/CombatView/EventModalHost.cs` line ~198 — pass `_host` as the fourth arg to `new EventHandler(...)`.
7. Edit `Assets/Scripts/CombatView/RunSceneHost.cs` — declare `IStormAdvancer` on the class, implement by delegating to `_session.AdvanceStormFromEvent(ticks)`.
8. Edit `Assets/Tests/EditMode/Run/NodeEncounter/EventHandler_Dispatch_Test.cs` — add `FakeStormAdvancer` inner recorder, update every `new EventHandler(...)` call to pass it, add per-Dispatch* assertions that storm cost applied post-mechanical.
9. Edit `Assets/Editor/NodeEncounterDataInitializer.cs` — bake per-choice storm costs into all four biome-1 events (see §Data table below).
10. Edit `Assets/Resources/Run/Biomes/Biome1Distribution.asset` — `_stormCounterStart: 8` → `12`.

**Block B — Presenter reset + storm counter Label** (UI hooks to Block A payload state):

1. Add `<Label name="storm-counter" class="storm-counter" />` to the fixed header in `DialogueScene.uxml` (sibling of title/description).
2. Add `.storm-counter` styling in `DialogueScene.uss` — small amber pill top-right of header, `Storm: 8/12` shape.
3. Add `SetStormCounter(int current, int max)` method to `DialogueSceneController` that finds `_stormCounter` Label and formats text (`"Storm: {current}/{max}"`); called by host after Bind and after each terminal choice commit.
4. In `DialogueSceneController.CommitTerminal` and the `Bind` entry — explicitly reset `_illustration.style.backgroundImage = null`, `_title.text = ""`, `_description.text = ""`, `_body.text = ""` BEFORE re-populating (prevents stale content flash on re-Bind under the SetActive re-clone lifecycle — `feedback_uidocument_setactive_reclone`).
5. `EventModalHost` calls `_presenter.SetStormCounter(session.StormState.CurrentValue, session.StormState.MaxValue)` after Bind and after each dispatch resolution (before next choice's presenter re-Bind, or after terminal commit).

**Block C — UI hygiene punch list** (independent polish, no Block A/B dependency):

1. **StyleBackground cache** — `DialogueSceneController` currently allocates `new StyleBackground(scene.Illustration)` every Bind. Cache in `Dictionary<Sprite, StyleBackground>` keyed by sprite ref; clear on `OnDestroy`. Zero-alloc steady state.
2. **`:focus` USS rule** on `.wr-dialogue-choice` — border-left-color amber, matches hover intensity. Keyboard-nav visual feedback (Steam controller + keyboard-only players).
3. **Content clear in CommitTerminal** — covered by Block B item 4.
4. **`tabIndex` on choice buttons** — set incrementing tabIndex in `RebuildChoices` so tab-order matches visual order (1/2/3/4 top to bottom). UI Toolkit's default tab-order is DOM order but explicit tabIndex is defensive against future re-layouts.
5. **`contentContainer` rename** — rename the scroll body's content container from Unity's internal `.unity-scroll-view__content-container` selector to a named class `.wr-dialogue-scroll-content`. Unity may rename internal selectors between minor versions; owning the name insulates us.

**Punted (not in this slice, backlog for the whole-game TD audit):**

6. **Lambda closure allocations** in choice-click handlers — `_choices[i].clicked += () => onPicked(choice);` allocates per-Bind. Micro-optimization, defer until we see profiler evidence.
7. **Slant contrast** — the 5° diagonal edge may read as a stair-step at low resolutions. Art-pass concern, not shipping.
8. **Overflow scissor** — the rotated `#panel-bg` extends past the panel bounds and could bleed into the illustration edge at extreme aspect ratios (ultrawide). Deferred until we get a real ultrawide bug report.
9. **`EnsureCached` race** — the negative execution order + coroutine one-frame-defer already patched this class of bug (memory `feedback_uidocument_negative_exec_order`). If it re-surfaces we address then.

### Newly-worried-about: DialogueChoiceSO badge slot pre-emption

Re-reading `DialogueChoiceSO.cs` after the outcome-reveal design landed, the `_rewardIcon` + `_rewardCount` pair is currently framed as "reward or cost" (positive = amber, negative = red). The `.wr-dialogue-outcome-reward--cost` modifier class exists in USS today.

**Concern:** when the Merchant/Chopshop slice lands and reuses `DialogueChoiceSO` for shop transactions, designers will want a THIRD register: "you unlocked X" (grey badge, not amber-gain / not red-cost). And when Stranded chance events land (memory `project_stranded_chance_event`), we'll want a FOURTH: "chance to gain, chance to lose" (question-mark register).

If we don't pre-empt this, `DialogueChoiceSO` will grow `_rewardIcon2`, `_rewardCount2`, `_isUnlock`, `_isChance` — the exact field-creep shape ADR-0011 warns against.

**Pre-emption:** add an xmldoc note on `_rewardIcon` / `_rewardCount` calling out the current dual register (gain/cost) and flagging that additional registers should NOT be new fields but a `RewardKind` enum sibling (Gain/Cost/Unlock/Chance/…). Designers see the note; next slice that needs a new register lands the enum instead of a new bool.

No code change this slice — just the xmldoc note. Pre-emptively communicates the shape so the drift is caught at author-time not review-time.

### Storm-counter shape — locked

- Presenter owns the widget. `DialogueSceneController.SetStormCounter(int current, int max)` is the ONLY entry point. Presenter-side formatting (`$"Storm: {current}/{max}"`) so USS + layout can iterate without touching host code.
- Host owns the values. `EventModalHost` reads `session.StormState` and calls the setter — no bindings, no reactive observables (single-frame widget, no need for observer pattern).
- Widget location: fixed header of the diagonal strip panel, sibling of title/description. Right-aligned. Small amber pill (12-14px). Matches the outcome-reward stinger visual language.
- Ships this slice — waiting adds risk that the mechanic ships without the surface the player uses to reason about their cost/reward decision.

### Data table — biome-1 event storm costs

Per user direction 2026-07-28 (this slice):

| Event | Choice | Scrap | Fuel | Storm cost |
|---|---|---|---|---|
| **Wreckage** | Salvage | +40 | 0 | 2 ticks |
| **Scavenger (Convert)** | Trade fuel→scrap at MaxInput | +ConvertRate·MaxInput | -MaxInput | 2 ticks |
| **Scavenger (Convert)** | Walk away | 0 | 0 | 0 ticks |
| **Cache (Treasure)** | Pry it open (patient) | +60 | 0 | 3 ticks |
| **Cache (Treasure)** | Blow it (explosives) | +24 | 0 | 1 tick |
| **Ambush** | Fight | (combat resolves) | 0 | 2 ticks |

Storm counter baseline: 8 → 12 (Biome1Distribution.asset).

**Ambush deferred multi-choice evade seam:** the 3-choice "fight / evade-fuel / evade-scrap" shape requires a "skip combat per choice" mechanical branch on `AmbushArchetype` that doesn't exist yet. Defer as a follow-up slice — not blocking this one. `PerChoiceStormCost = [2]` (single choice) ships today.

**Cache split rationale:** the two-choice Cache introduces the first real risk/reward per-choice decision in the game (patient/high-value vs impatient/lower-value). It's the poster child for why Route D is worth building — a single Cache SO with `PerChoiceStormCost = [3, 1]` + `PerChoiceScrapReward = [60, 24]` expresses the whole design without a new SO type.

---

## Three-lens self-audit

**Health.** Route D adds one interface (`IStormAdvancer` — one verb), one method on `RunSession` (`AdvanceStormFromEvent` — mirrors existing shape), one 4th ctor param on `EventHandler`, two fields on `EventPayloadDefinitionSO`. All additive. No bridges. No dormant retention. Test coverage extended by a `FakeStormAdvancer` recorder pattern that matches existing test fixture style.

**Optimization.** Storm cost application is one `StormState.AdvanceCounter(int)` call per choice — same code path as `AutoAdvanceStrandedStorm`, already profiled clean. UI hygiene items 1 (StyleBackground cache) and 5 (contentContainer rename) reduce allocations and coupling to Unity internals. No per-frame hot-path changes.

**1.0 survival.** The `PerChoice*[]` shape survives forever — any future encounter that needs a per-choice mechanical delta lands as a new int[] field on `EventPayloadDefinitionSO` without touching `IStormAdvancer` or the presenter. `IStormAdvancer` is the composition seam for "events cost storm ticks," which is a permanent game rule. Data-flag lagging dep pattern preserved: values ship as end-state, not stubs. Badge-slot pre-emption note on `DialogueChoiceSO` heads off the field-creep failure mode before the second consumer (Merchant) arrives.

**Verdict:** Proceed with Block A → Block B → Block C in order.

---

## Acceptance walkthrough (post-implementation)

- [ ] EditMode tests pass: `EventHandler_Dispatch_Test` — each Dispatch* records the correct storm tick per choice; multi-choice Cache records 3 ticks for choice 0, 1 tick for choice 1.
- [ ] PlayMode: hit Wreckage beacon → salvage → storm advances 2 ticks; counter widget in dialogue header updates from `Storm: 8/12` to `Storm: 6/12`.
- [ ] PlayMode: hit Cache → pry choice → +60 scrap, storm advances 3 ticks; hit second Cache (new run) → explosives choice → +24 scrap, storm advances 1 tick.
- [ ] PlayMode: hit Scavenger → walk away → no scrap delta, no storm advance.
- [ ] Re-bind between events: choice list, illustration, title, description, body all replace cleanly (no stale flash from prior encounter).
- [ ] Keyboard tab-nav through choice buttons follows 1→2→3→4 visual order; `:focus` state visible.
- [ ] Grep gate AC-EV13 stays green: no mechanical vocab in `WastelandRun.UI`.

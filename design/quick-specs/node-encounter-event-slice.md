# Quick Spec — Node Encounter: Event Handler Slice

> **Status**: Approved to author (v2 — TD-reshaped 2026-07-27 for full-scope target)
> **Author**: user + game-designer + technical-director
> **Type**: Vertical implementation slice — scope-narrows the Node Encounter GDD
> **Parent GDD**: `design/gdd/node-encounter.md` (1063 lines, In Design 2026-04-24) — source of truth for all mechanical rules; this spec does **not** re-design anything the parent locks
> **Enables (downstream)**: Stranded Chance Events GDD (parked 2026-07-27 — see `production/polish-captures/2026-07-27-stranded-chance-events-gdd-shape.md`)

## 1. Purpose

Ship the Node Encounter Event-handler as its own vertical slice, activating
`BeaconType.Event = 5` as playable content in biome 1. The slice introduces:

- **Handler contract** — `INodeEncounterHandler` + `BeaconOutcome` (Node Encounter §C.1) as real code, first of seven handlers to ship.
- **Two SO families** — `EventPayloadDefinitionSO` (mechanical vocabulary: Windfall / Convert / Ambush / Treasure) and `DialogueSceneSO` + `DialogueChoiceSO` (presentation vocabulary: illustration, panel copy, numbered choices).
- **Content-blind dialogue controller** — `DialogueSceneController` reused by any future dialogue-shaped node (Merchant flavor, Chopshop offers, Stranded chance events). Zero references to `IScrapEconomy` or `BeaconType`; binds a `DialogueSceneSO` and emits choice-index callbacks.
- **`HostileTiltDelta`** — Frame-subsystem-state-dependent weight tilt (Node Encounter §D.2). Buildable now: samples existing `SlotInstance.DamageState { Nominal, Degraded, Offline }` per Frame slot.
- **Modal presentation prefab** — `EventRoot.prefab` per Option B hybrid topology (2026-06-28 verdict). Wraps `DialogueSceneRoot.prefab` (the reusable dialogue infra).
- **Amber palette tokens** — new palette baked into `Assets/UI/tokens.colors.uss` (ember-orange Haven register; Darkest Dungeon 2 reference).
- **6 verbs on `IScrapEconomy`** — `TryConvertScrapToFuel`, `TryConvertFuelToScrap`, `TryGrantScrap`, `TryGrantFuel`, plus preview helpers `CanConvert` (rate check) + `PreviewGrant` (Newtonsoft/dryrun path used by Convert dialog before player commits).
- **`PendingEventOffer`** — parallel to `PendingCardOffer` on `RunState`; latches Convert offers so the modal can present + resolve deterministically. Not persisted this slice (matches `PendingCardOffer` today).
- **`LootContextTag`** — data-flag lagging-dependency field on `DialogueChoiceSO` (Treasure integration). Carrier field only; L&R doesn't read it yet.

Downstream, Stranded Chance Events becomes a ~200-line adapter GDD reusing all
of this infrastructure — the parked GDD is gated on this slice landing first.

Beyond the downstream unblock, the slice delivers standalone player value:
`BeaconType.Event` is currently live in the biome-1 generator but fires no
content when visited. This slice turns dead beacons into a full payload family
with a shipped dialogue-scene aesthetic.

## 2. Scope narrowing

### IN this slice

| Item | Source of truth | Notes |
|---|---|---|
| `INodeEncounterHandler` interface | Node Encounter §C.1 | Real code; used by `EventHandler` now, extended by future handlers |
| `BeaconOutcome` payload struct | Node Encounter §C.1 | Readonly struct per §C.1 schema; some fields zero-populated by EventHandler |
| `EventHandler.Begin(...)` impl | Node Encounter §C.2.5 | Full weight-table roll + `HostileTiltDelta` + payload dispatch |
| `HostileTiltDelta` | Node Encounter §D.2 | Samples Frame `SlotInstance.DamageState` → per-payload weight delta. Ships buildable now (no Frame-subsystem seam needed — data lives on `SlotInstance` already) |
| `EventPayloadDefinitionSO` | (new — mechanical vocabulary) | Windfall / Convert / Ambush / Treasure payload shapes |
| `DialogueSceneSO` + `DialogueChoiceSO` | (new — presentation vocabulary) | Illustration ref + panel copy + choice list. Content-blind — shareable across future dialogue nodes |
| `DialogueSceneController` (content-blind) | (new — reusable infra) | Zero refs to `IScrapEconomy` / `BeaconType`. Binds `DialogueSceneSO`, emits `OnChoiceSelected(int index)` |
| `EventModalHost` (thin adapter) | (new) | Translates `BeaconActivated` → `DialogueSceneSO` selection → dispatches `EventHandler.Begin` → routes choice-index back to payload verbs → emits `BeaconOutcome` |
| `IScrapEconomy.TryConvertScrapToFuel` + `TryConvertFuelToScrap` | Node Encounter §C.2.5 + Scrap Economy | Rate = `EventConvertFavorableRate = 3` on Event nodes |
| `IScrapEconomy.TryGrantScrap` + `TryGrantFuel` | Node Encounter §C.2.5 | Used by Windfall + Treasure payloads |
| `IScrapEconomy.CanConvert` + `PreviewGrant` | (new preview helpers) | Support Convert dialog's pre-commit affordability check |
| `RunState.PendingEventOffer` | (mirrors `PendingCardOffer`) | Latches Convert offer for player resolution; not persisted this slice |
| `RunController.ResolveEventOffer(int choiceIndex)` + `SkipEventOffer()` | (mirrors card-offer verbs on RunController) | Apply/dismiss the pending offer |
| `EventRoot.prefab` under `Assets/Prefabs/BeaconRoots/` | Option B hybrid (2026-06-28 verdict) | `Mode = PrefabRoot`; mirrors `RestRoot.prefab` pattern; hosts `DialogueSceneRoot` child |
| `DialogueSceneRoot.prefab` | (new — reusable UI infra) | UIDocument + `DialogueSceneController` component. Nested inside `EventRoot` this slice; future dialogue nodes instance directly |
| `DialogueScene.uxml` + `DialogueScene.uss` | (new — split-panel layout) | 60% illustration + 40% parchment, numbered choices, torn-frame decorative edges (DD2 aesthetic) |
| `tokens.colors.uss` amber palette bake | (new palette register) | Ember-orange Haven register; DesignTokens propagation |
| Ambush → Combat chain | Node Encounter §C.2.5 Ambush sub-rule | Dispatches to existing Combat handler with `CombatSetup.EncounterType = Ambush` |
| `BeaconSceneBinding.asset` entry for Event | ADR-0015 | `Mode = PrefabRoot`, ScenePath empty |
| `RunSceneHost` Event fan-out | Existing pattern (2026-07-26 boss bundle) | Activate `EventRoot`, dispatch `EventHandler.Begin`, wire callback |
| `LootContextTag` field on `DialogueChoiceSO` | Data-flag lagging dep (memory `feedback_data_flag_lagging_dependency`) | Carrier only; L&R doesn't consume yet. Enum + serialized field ships, no consumer branches |
| Biome-1 Event content | (new authored assets) | 4 event assets — one per payload variant |
| Determinism + distribution + tilt tests | Node Encounter §D.1 seed derivation + §D.2 tilt | See §6 |

### OUT this slice (deferred with reason)

| Deferred item | Why safe to defer | What ships instead |
|---|---|---|
| Merchant / Chopshop / Rest / Haven handlers | Each is its own vertical slice; parent GDD has full mechanics | Existing `RestRoot.prefab` continues; other beacon types stub as today |
| Per-biome distribution table application | Biome 1 generator already emits Event beacons at current weight | `Biome1Distribution.asset` weight controls Event frequency |
| Loot & Reward integration (Treasure payload) | L&R has its own GDD + slice ahead | Treasure grants **12 scrap flat** via `TryGrantScrap` (matches Windfall's Scrap-Windfall); `LootContextTag` carrier field ships for future consumer |
| `TryRepair(freeRepair:true)` retrofit | Rest handler slice will land this | Not needed by EventHandler |
| Multi-biome Event content (biomes 2 / 3) | Biomes 2 / 3 not built | Biome 1 alone |
| Save/restore of *pending* Event modal state | Player closing app during modal is edge case not gated on this slice | Ship modal as non-serialized; interrupted event re-rolls on reload (matches `PendingCardOffer` today) |
| Narrative-authored copy beyond four stubs | Narrative pass follows | Placeholder copy on all four events |
| Illustration art beyond four placeholders | Art pass follows; user will provide first-draft illustrations | Placeholder sprites (single ember-tinted silhouette per event) |

## 3. Contract surfaces (concrete file list)

### New files

**Runtime code (Run):**
- `Assets/Scripts/Run/NodeEncounter/INodeEncounterHandler.cs`
- `Assets/Scripts/Run/NodeEncounter/BeaconOutcome.cs`
- `Assets/Scripts/Run/NodeEncounter/EventHandler.cs`
- `Assets/Scripts/Run/NodeEncounter/HostileTiltDelta.cs`
- `Assets/Scripts/Run/PendingEventOffer.cs`

**Authoring / data (Run.Authoring):**
- `Assets/Scripts/Run/Authoring/EventPayloadDefinitionSO.cs`
- `Assets/Scripts/Run/Authoring/DialogueSceneSO.cs`
- `Assets/Scripts/Run/Authoring/DialogueChoiceSO.cs`
- `Assets/Scripts/Run/Authoring/LootContextTag.cs` (enum + serialized field carrier)

**Content assets:**
- `Assets/Resources/Run/Events/Biome1/Wreck.asset` (Windfall payload)
- `Assets/Resources/Run/Events/Biome1/Scavenger.asset` (Convert payload)
- `Assets/Resources/Run/Events/Biome1/Ambush.asset` (Ambush payload → chain)
- `Assets/Resources/Run/Events/Biome1/Cache.asset` (Treasure payload → stub Scrap grant)

**View / UI (reusable dialogue infra + Event-specific host):**
- `Assets/Prefabs/BeaconRoots/EventRoot.prefab` (parented under RunScene)
- `Assets/Prefabs/UI/DialogueSceneRoot.prefab` (reusable — nested inside EventRoot this slice, standalone for future dialogue nodes)
- `Assets/Scripts/UI/DialogueSceneController.cs` (content-blind — zero IScrapEconomy / BeaconType refs)
- `Assets/Scripts/CombatView/EventModalHost.cs` (Event-specific adapter over DialogueSceneController)
- `Assets/UI/DialogueScene.uxml`
- `Assets/UI/DialogueScene.uss`

**Tests:**
- `Assets/Tests/EditMode/Run/NodeEncounter/EventHandler_Roll_Test.cs` (determinism + Nominal distribution)
- `Assets/Tests/EditMode/Run/NodeEncounter/EventHandler_HostileTilt_Test.cs` (per-DamageState tilt correctness)
- `Assets/Tests/EditMode/Run/NodeEncounter/EventHandler_Ambush_Chain_Test.cs` (Ambush → Combat with EncounterType)
- `Assets/Tests/EditMode/Run/ScrapEconomy_Convert_Test.cs` (rate = 3, floor semantics, preview + skip)
- `Assets/Tests/EditMode/Run/RunController_EventOffer_Test.cs` (Resolve / Skip semantics mirror card offer)

**File count:** 16 new source + 4 event assets = **20 new files**.

### Modified files

- `Assets/Scripts/Run/IScrapEconomy.cs` — 6 verbs added (4 mutators + 2 preview helpers)
- `Assets/Scripts/Run/ScrapEconomy.cs` — implementations
- `Assets/Scripts/Run/RunState.cs` — add `PendingEventOffer` nullable field mirroring `PendingCardOffer`
- `Assets/Scripts/Run/RunController.cs` — add `ResolveEventOffer(int)` + `SkipEventOffer()` mirroring card offer verbs
- `Assets/Scripts/CombatView/RunSceneHost.cs` — Event fan-out (activate `EventRoot`, dispatch `EventHandler.Begin`, subscribe to `BeaconOutcome` callback)
- `Assets/Scripts/CombatView/BeaconActivator.cs` — register `EventRoot` in the prefab-root registry (dispatch stays purely on `BeaconLoadMode`, no `if (type == Event)` branch)
- `Assets/Data/BeaconScenes/BeaconSceneBinding.asset` — Event entry (`Mode = PrefabRoot`, ScenePath empty)
- `Assets/Editor/CombatPrefabAuthor.cs` — Event binding entry + EventRoot authoring path mirroring `AuthorRestRoot`
- `Assets/UI/tokens.colors.uss` — amber palette bake (`--ember-*` register)

**Modified file count:** 9.

## 4. Dependencies + deferrals

| Dependency | State today | Slice behavior |
|---|---|---|
| `BeaconType.Event = 5` | Live in enum, live in generator, no runtime handler | Slice adds the handler |
| `ScrapEconomy` (`Current`, `Add`, `TrySpend`) | Live | Slice extends with 6 verbs — additive, no bimodal |
| `SlotInstance.DamageState` | Live per-slot enum `{Nominal, Degraded, Offline}` | `HostileTiltDelta` reads directly — no new Frame seam required |
| Card Combat System (Ambush chain) | Live | Slice invokes existing `CombatSetup` with `EncounterType = Ambush` |
| `RunSceneHost` fan-out pattern | Live (Combat / EliteCombat / Boss + Rest) | Slice adds Event branch mirroring Rest pattern |
| `RestRoot.prefab` (PrefabRoot precedent) | Live | Slice mirrors its shape for `EventRoot.prefab` |
| `PendingCardOffer` on `RunState` | Live | Slice adds parallel `PendingEventOffer` |
| `tokens.colors.uss` (DesignTokens propagation) | Live | Slice adds `--ember-*` amber register |
| Loot & Reward `GenerateRewards(Event[node:N:Treasure])` | Not built | Treasure stubs to 12-scrap flat grant. `LootContextTag` carrier field on `DialogueChoiceSO` ships now (data-flag lagging dep); L&R reads it when it lands, zero refactor |
| Save layer for `PendingEventOffer` | Not persisted | Ship non-serialized; matches `PendingCardOffer` treatment today |

## 5. Biome-1 content roster

Four events, one per payload variant so all resolve paths ship exercised. All
flavor copy is placeholder — Narrative pass follows. User will provide first-draft
illustrations before ship.

| Event | Payload | Weights (from §D.2 Nominal) | Mechanics | Copy stub |
|---|---|---|---|---|
| **Wreck** | Windfall | 30 | 50/50 Scrap-Windfall (`+12 Scrap`) vs Fuel-Windfall (`+1..3 Fuel`) | "A wrecked hauler, still smoking. Scavenge what's left." |
| **Scavenger** | Convert | 15 | Seeded direction: Scrap→Fuel or Fuel→Scrap at rate 3 (`Floor(in / 3)`); skip is free | "A scavenger flags you down. He's got what you need, if you've got what he needs." |
| **Ambush** | Ambush | 20 | Chains to Combat, `EncounterType = Ambush`; storm advances only on chained Combat close | "The road behind you shifts. Headlights. Too fast." |
| **Cache** | Treasure | 35 | Grants **+12 Scrap** flat (L&R integration deferred; `LootContextTag = Cache_Biome1` carrier ships) | "A hidden cache under a rusted tarp." |

**Weight sum:** 30 + 15 + 20 + 35 = 100 ✓ (matches Node Encounter §D.2 Nominal invariant)

**HostileTiltDelta** — applied on top of Nominal weights per Node Encounter §C.2.5 + §D.2:

- **Trigger:** any Frame `SlotInstance.DamageState` in `Degraded` or `Offline` (binary — Degraded and Offline apply the same delta, NOT compounded)
- **Deltas (integer, sum-zero):** Ambush +15, Windfall −10, Treasure −5, Convert unchanged
- **Tilted table** (Frame Degraded or Offline): Treasure 30, Ambush 35, Windfall 20, Convert 15 (sums to 100)

No renormalization needed — deltas are pre-designed to sum to zero.

## 6. Acceptance criteria

Testable — each must be verifiable pass/fail by a manual or automated test.

**Playable (manual walkthrough):**
- **AC-EV1** — Play biome 1 → hit at least one Event beacon → `EventRoot` activates → dialogue modal presents with amber palette + split-panel layout → outcome applies via correct verb → HUD updates
- **AC-EV2** — Ambush payload chains into Combat with `CombatSetup.EncounterType == Ambush` (verify via log)
- **AC-EV3** — Windfall auto-grants with no player input; Scavenger presents Convert dialog with numbered choices + affordability preview; Cache auto-grants with confirmation reveal; Ambush transitions directly to combat load
- **AC-EV4** — Amber palette matches DD2-reference aesthetic (`--ember-primary`, `--ember-panel-bg`, `--ember-choice-hover` visible in modal)

**Automated (unit + determinism):**
- **AC-EV5** — `EventHandler_Roll_Test`: same `(runSeed, nodeIndex)` produces identical payload across 10,000 trials
- **AC-EV6** — `EventHandler_Roll_Test`: Nominal weight distribution over 100k trials matches `{Treasure: 35±1%, Ambush: 20±1%, Windfall: 30±1%, Convert: 15±1%}`
- **AC-EV7** — `EventHandler_HostileTilt_Test`: with all Frame slots Nominal, tilted table equals base (35/20/30/15); with ≥ 1 Frame slot in Degraded OR Offline, tilted table equals (Treasure 30, Ambush 35, Windfall 20, Convert 15) — binary flag, not compounded (per §C.2.5)
- **AC-EV8** — `ScrapEconomy_Convert_Test`: `TryConvertScrapToFuel(9, ctx)` at rate 3 grants exactly 3 fuel; `TryConvertScrapToFuel(2, ctx)` grants 0 fuel and returns false; `CanConvert` matches `TryConvertScrapToFuel` result without mutation
- **AC-EV9** — `ScrapEconomy_Convert_Test`: player skipping Convert offer fires zero verbs (scrap + fuel state unchanged)
- **AC-EV10** — `EventHandler_Ambush_Chain_Test`: Ambush payload builds no `BeaconOutcome` from EventHandler; chained Combat's outcome is the beacon's outcome; storm advances exactly once
- **AC-EV11** — `RunController_EventOffer_Test`: `ResolveEventOffer(idx)` applies chosen payload verb + clears `PendingEventOffer`; `SkipEventOffer()` clears with zero verbs fired

**Structural / ADR gates (grep):**
- **AC-EV12** — Zero `if (beaconType == Event)` branches in `BeaconActivator` — dispatch stays on `BeaconLoadMode` per ADR-0015
- **AC-EV13** — `DialogueSceneController` has zero refs to `IScrapEconomy` or `BeaconType` (content-blind — grep gate)
- **AC-EV14** — `BeaconSceneBinding.asset` entry for Event has `Mode = PrefabRoot` and empty `ScenePath` (validated by `BeaconSceneBindingSO.OnValidate`)
- **AC-EV15** — `LootContextTag` has zero consumer branches this slice (only field-declaration references; grep-verified)

## 7. Downstream unblocks

Concrete future work this slice enables:

- **Stranded Chance Events** — parked GDD becomes ~200-line adapter over shared `DialogueSceneSO` + `EventPayloadDefinitionSO` (see `production/polish-captures/2026-07-27-stranded-chance-events-gdd-shape.md`)
- **Node Encounter GDD** — moves from paper to partially-implemented (1 of 7 handlers shipped); remaining handlers inherit the `INodeEncounterHandler` + `BeaconOutcome` contract
- **`TryConvert*` + `TryGrant*` verbs** — unblock any future economic-tension mechanic
- **`DialogueSceneController`** — reusable by Merchant flavor dialogue, Chopshop offer confirmation, Stranded events, future story beats
- **Amber palette** — DesignTokens register available for other Haven / Rest / narrative surfaces
- **`LootContextTag`** — L&R integration seam pre-wired; Treasure upgrade becomes a data-driven swap once L&R Event context lands
- **Biome-1 content density** — Event beacons stop being dead pixels; biome 1 gains a full payload family

## 8. Non-goals

Explicit non-goals so scope doesn't drift during author:

- **NOT** building L&R integration for Treasure — stub 12-scrap grant, `LootContextTag` carrier ships now, upgrade later
- **NOT** shipping other biome-1 event content beyond the 4 payload archetypes
- **NOT** authoring event copy beyond the four placeholder stubs — Narrative pass follows
- **NOT** authoring event art / VFX beyond placeholder illustrations — Art follows once user provides first-draft
- **NOT** touching Rest / Haven / Merchant / Chopshop handlers — each has its own slice ahead
- **NOT** persisting `PendingEventOffer` this slice — matches `PendingCardOffer` non-persistence today
- **NOT** consuming `LootContextTag` from anywhere — carrier field only, data-flag lagging dep

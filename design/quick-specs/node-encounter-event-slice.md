# Quick Spec — Node Encounter: Event Handler Slice

> **Status**: Approved to author (2026-07-27)
> **Author**: user + game-designer + technical-director
> **Type**: Vertical implementation slice — scope-narrows the Node Encounter GDD
> **Parent GDD**: `design/gdd/node-encounter.md` (1063 lines, In Design 2026-04-24) — source of truth for all mechanical rules; this spec does **not** re-design anything the parent locks
> **Enables (downstream)**: Stranded Chance Events GDD (parked 2026-07-27 — see `production/polish-captures/2026-07-27-stranded-chance-events-gdd-shape.md`)

## 1. Purpose

Ship the Node Encounter Event-handler as its own vertical slice, activating
`BeaconType.Event = 6` as playable content in biome 1. The slice introduces the
shared payload vocabulary (`EventPayloadDefinitionSO`), the modal presentation
prefab (`EventRoot.prefab` per Option B hybrid topology), and the `TryConvert*` /
`TryGrant*` verbs on `IScrapEconomy`. Downstream, Stranded Chance Events becomes
a ~200-line adapter GDD reusing all of this infrastructure — the parked GDD is
gated on this slice landing first.

Beyond the downstream unblock, the slice delivers standalone player value:
`BeaconType.Event` is currently live in the biome-1 generator but fires no
content when visited. This slice turns dead beacons into a full payload family
(Windfall / Convert / Ambush / Treasure) — one of the seven Node Encounter
handlers moves from paper to shipped.

## 2. Scope narrowing

### IN this slice

| Item | Source of truth | Notes |
|---|---|---|
| `INodeEncounterHandler` interface | Node Encounter §C.1 | Real code; used only by `EventHandler` in this slice, extended by future handlers |
| `BeaconOutcome` payload struct | Node Encounter §C.1 | All fields per §C.1 schema; some (e.g. `PartOffered`) will be zero-populated by EventHandler |
| `EventHandler.Begin(...)` impl | Node Encounter §C.2.5 | Full weight-table roll + payload dispatch |
| `EventPayloadDefinitionSO` | (new — shared vocabulary) | Thin SO for Treasure / Ambush / Windfall / Convert payloads |
| `IScrapEconomy.TryConvertScrapToFuel` + `TryConvertFuelToScrap` | Node Encounter §C.2.5 + Scrap Economy | Rate = `EventConvertFavorableRate = 3` on Event nodes |
| `IScrapEconomy.TryGrantScrap` + `TryGrantFuel` | Node Encounter §C.2.5 | Used by Windfall payload |
| `EventRoot.prefab` under `Assets/Prefabs/BeaconRoots/` | Option B hybrid (2026-06-28 verdict) | `Mode = PrefabRoot`; mirrors `RestRoot.prefab` pattern |
| Event modal UI (UI Toolkit) | Node Encounter §C.2.5 payloads | Hosted inside `EventRoot.prefab`; per-payload subview |
| Ambush → Combat chain | Node Encounter §C.2.5 Ambush sub-rule | Dispatches to existing Combat handler with `CombatSetup.EncounterType = Ambush` |
| `BeaconSceneBinding.asset` entry for Event | ADR-0015 | `Mode = PrefabRoot`, ScenePath empty |
| `RunSceneHost` Event fan-out | Existing 3-way fan-out pattern (2026-07-26 boss bundle) | Activate `EventRoot`, dispatch `EventHandler.Begin`, wire callback |
| Biome-1 Event content | (new authored assets) | 4 event assets — one per payload variant so all resolve paths get real test data |
| Determinism test | Node Encounter §D.1 seed derivation | Same `(runSeed, nodeIndex)` → same payload roll |

### OUT this slice (deferred with reason)

| Deferred item | Why safe to defer | What ships instead |
|---|---|---|
| Merchant / Chopshop / Rest / Haven handlers | Each is its own vertical slice; Node Encounter GDD has full mechanics for each already | Existing `RestRoot.prefab` continues to work; other beacon types stub as they do today |
| Per-biome distribution table application | Biome 1 generator already emits Event beacons at the current 20% weight; deferral doesn't gate this slice | Existing `Biome1Distribution.asset` weight controls Event frequency |
| `HostileTiltDelta` Frame-tilt weight adjustment | Requires `FrameSubsystemState` sampling seam that Frame subsystem owns; not yet wired | Ship with **Nominal weights only** (Treasure 35 / Ambush 20 / Windfall 30 / Convert 15); tilt lands with Frame subsystem or a follow-up |
| Loot & Reward integration (Treasure payload) | L&R has its own GDD + slice ahead | Treasure temporarily grants **12 scrap flat** via `TryGrantScrap` (matches Windfall's Scrap-Windfall grant); replaced when L&R Event context lands |
| `TryRepair(freeRepair:true)` retrofit | Rest handler slice will land this | Not needed by EventHandler; deferred cleanly |
| Multi-biome Event content (biomes 2 / 3) | Biomes 2 / 3 not built | Biome 1 alone this slice |
| Save/restore of *pending* Event modal state | Player closing app during modal is edge case not gated on this slice | Ship modal as non-serialized; interrupted event re-rolls on reload (documented behavior) |

## 3. Contract surfaces (concrete file list)

### New files

**Runtime code:**
- `Assets/Scripts/Run/NodeEncounter/INodeEncounterHandler.cs`
- `Assets/Scripts/Run/NodeEncounter/BeaconOutcome.cs`
- `Assets/Scripts/Run/NodeEncounter/EventHandler.cs`

**Authoring / data:**
- `Assets/Scripts/Run/Authoring/EventPayloadDefinitionSO.cs`
- `Assets/Resources/Run/Events/Biome1/Wreck.asset` (Windfall payload)
- `Assets/Resources/Run/Events/Biome1/Scavenger.asset` (Convert payload)
- `Assets/Resources/Run/Events/Biome1/Ambush.asset` (Ambush payload → chain)
- `Assets/Resources/Run/Events/Biome1/Cache.asset` (Treasure payload → stub Scrap grant)

**View / UI:**
- `Assets/Prefabs/BeaconRoots/EventRoot.prefab` (parented under RunScene)
- `Assets/Scripts/UI/EventModalController.cs`
- `Assets/UI/EventModal.uxml`
- `Assets/UI/EventModal.uss`

**Tests:**
- `Assets/Tests/EditMode/Run/NodeEncounter/EventHandler_Roll_Test.cs` (determinism, weight distribution)
- `Assets/Tests/EditMode/Run/NodeEncounter/EventHandler_Ambush_Chain_Test.cs` (Ambush → Combat with EncounterType)
- `Assets/Tests/EditMode/Run/ScrapEconomy_Convert_Test.cs` (rate = 3, floor semantics, skip fires zero verbs)

### Modified files

- `Assets/Scripts/Run/IScrapEconomy.cs` — add 4 verbs (`TryConvertScrapToFuel`, `TryConvertFuelToScrap`, `TryGrantScrap`, `TryGrantFuel`)
- `Assets/Scripts/Run/ScrapEconomy.cs` — implementations
- `Assets/Scripts/CombatView/RunSceneHost.cs` — Event fan-out (activate `EventRoot`, dispatch `EventHandler.Begin`, subscribe to `BeaconOutcome` callback)
- `Assets/Scripts/CombatView/BeaconActivator.cs` — register `EventRoot` in the prefab-root registry
- `Assets/Data/BeaconScenes/BeaconSceneBinding.asset` — Event entry (`Mode = PrefabRoot`, ScenePath empty)
- `Assets/Editor/CombatPrefabAuthor.cs` — Event binding entry + EventRoot authoring (mirror RestRoot authoring path)

## 4. Dependencies + deferrals

| Dependency | State today | Slice behavior |
|---|---|---|
| `BeaconType.Event = 6` | Live in enum, live in generator, no runtime handler | Slice adds the handler |
| `ScrapEconomy` (`Current`, `Add`, `TrySpend`) | Live | Slice extends with `TryGrant*` + `TryConvert*` — additive, no bimodal |
| Card Combat System (Ambush chain) | Live | Slice invokes existing `CombatSetup` with `EncounterType = Ambush`; Card Combat already accepts the flag |
| `RunSceneHost` fan-out pattern | Live (Combat / EliteCombat / Boss + Rest) | Slice adds Event branch mirroring Rest pattern |
| `RestRoot.prefab` (PrefabRoot precedent) | Live | Slice mirrors its shape for `EventRoot.prefab` |
| `FrameSubsystemState` (for `HostileTiltDelta`) | Not sampled at commit yet | Slice ships Nominal weights only; tilt is a follow-up |
| Loot & Reward `GenerateRewards(Event[node:N:Treasure])` | Not built | Treasure stubs to 12-scrap flat grant; upgraded when L&R Event context lands |

## 5. Biome-1 content roster

Four events, one per payload variant so all resolve paths ship exercised at
launch. All flavor copy is placeholder — Narrative pass follows.

| Event | Payload | Weights (from §D.2 Nominal) | Mechanics | Copy stub |
|---|---|---|---|---|
| **Wreck** | Windfall | 30 | 50/50 Scrap-Windfall (`+12 Scrap`) vs Fuel-Windfall (`+1..3 Fuel`) | "A wrecked hauler, still smoking. Scavenge what's left." |
| **Scavenger** | Convert | 15 | Seeded direction: Scrap→Fuel or Fuel→Scrap at rate 3 (`Floor(in / 3)`); skip is free | "A scavenger flags you down. He's got what you need, if you've got what he needs." |
| **Ambush** | Ambush | 20 | Chains to Combat, `EncounterType = Ambush`; storm advances only on chained Combat close | "The road behind you shifts. Headlights. Too fast." |
| **Cache** | Treasure | 35 | Grants **+12 Scrap** flat (L&R integration deferred; upgrade to reward-offer later) | "A hidden cache under a rusted tarp." |

**Weight sum:** 30 + 15 + 20 + 35 = 100 ✓ (matches Node Encounter §D.2 Nominal invariant)

## 6. Acceptance criteria

Testable — each must be verifiable pass/fail by a manual or automated test.

**Playable (manual walkthrough):**
- **AC-EV1** — Play biome 1 → hit at least one Event beacon → `EventRoot` activates → modal presents payload-appropriate UI → outcome applies via correct verb → HUD updates
- **AC-EV2** — Ambush payload chains into Combat with `CombatSetup.EncounterType == Ambush` (verify via CombatSetup log)
- **AC-EV3** — Windfall auto-grants with no player input; Scavenger presents opt-in dialog; Cache auto-grants with confirmation reveal; Ambush transitions directly to combat load

**Automated (unit + determinism):**
- **AC-EV4** — `EventHandler_Roll_Test`: same `(runSeed, nodeIndex)` produces identical payload across 10,000 trials
- **AC-EV5** — `EventHandler_Roll_Test`: Nominal weight distribution over 100k trials matches `{Treasure: 35±1%, Ambush: 20±1%, Windfall: 30±1%, Convert: 15±1%}`
- **AC-EV6** — `ScrapEconomy_Convert_Test`: `TryConvertScrapToFuel(9, ctx)` at rate 3 grants exactly 3 fuel; `TryConvertScrapToFuel(2, ctx)` grants 0 fuel and returns false (insufficient for one unit)
- **AC-EV7** — `ScrapEconomy_Convert_Test`: player skipping Convert offer fires zero verbs (scrap + fuel state unchanged)
- **AC-EV8** — `EventHandler_Ambush_Chain_Test`: Ambush payload builds no `BeaconOutcome` from EventHandler; the chained Combat's outcome is the beacon's outcome; storm advances exactly once

**Structural / ADR-0011:**
- **AC-EV9** — Grep verifies zero `if (beaconType == Event)` branches in `BeaconActivator` — dispatch stays on `BeaconLoadMode` tag per ADR-0015
- **AC-EV10** — `BeaconSceneBinding.asset` entry for Event has `Mode = PrefabRoot` and empty `ScenePath` (validated by `BeaconSceneBindingSO.OnValidate`)

## 7. Downstream unblocks

Concrete future work this slice enables:

- **Stranded Chance Events** — parked GDD becomes ~200-line adapter over shared infrastructure (see `production/polish-captures/2026-07-27-stranded-chance-events-gdd-shape.md`)
- **Node Encounter GDD** — moves from paper to partially-implemented (1 of 7 handlers shipped); remaining handlers (Merchant / Chopshop / Rest / Haven) inherit the `INodeEncounterHandler` + `BeaconOutcome` contract
- **`TryConvert*` verbs** — unblock any future economic-tension mechanic (Rest with paid-repair option, Chopshop discount events, etc.)
- **Biome-1 content density** — Event beacons stop being dead pixels; biome 1 gains a full payload family of encounters

## 8. Non-goals

Explicit non-goals so scope doesn't drift during author:

- **NOT** implementing `HostileTiltDelta` — Frame state wiring is out; Nominal weights only
- **NOT** building L&R integration for Treasure — stub 12-scrap grant, upgrade later
- **NOT** shipping other biome-1 event content beyond the 4 payload archetypes
- **NOT** authoring event copy beyond the four placeholder stubs — Narrative pass follows
- **NOT** authoring event art / VFX beyond a shared UI Toolkit modal look — Art follows
- **NOT** touching Rest / Haven / Merchant / Chopshop handlers — each has its own slice ahead

# Node Encounter System

> **Status**: In Design
> **Author**: user + creative-director + level-designer + systems-designer + economy-designer + narrative-director + art-director + audio-director + ux-designer + qa-lead
> **Last Updated**: 2026-04-23
> **Implements Pillar**: Pillar 5 (Route Reflects Vehicle State) — primary; Pillar 3 (Read to Win) + Pillar 4 (Scarcity with Agency) — secondary

## Overview

The Node Encounter System is Wasteland Run's per-beacon rulebook and dispatcher. When the player commits to a beacon on the Node Map, Node Encounter looks up the beacon's type, runs the matching handler, resolves the encounter, and returns a `BeaconOutcome` payload to Node Map — which then advances the storm (Combat/EliteCombat only) and re-computes the reachable set. The system owns seven handler paths covering every beacon type in the game: `Combat`, `EliteCombat` (with an `isBoss` flag at biome-gate Strip-5), `Merchant`, `Chopshop`, `Event` (the "Unknown" node, with Treasure/Ambush/Windfall/Convert payloads resolved at reveal), `Rest`, and `Haven`. It also owns the per-biome beacon distribution tables that Node Map reads during graph generation — Node Encounter authors the content; Node Map arranges the geometry.

At the player's seat, Node Encounter is where Pillar 5 (Route Reflects Vehicle State) actually *bites*. The beacon icons on the map are promises — a Merchant will sell you a card, a Chopshop will let you repair, an Event will roll the dice against your current Frame damage, an Elite at Strip-5 will close out a biome on different terms than the Elites that preceded it. The handler routes what those promises turn into once you commit: Scrap-priced Installs that feel affordable or oppressive depending on your current purse; Convert verbs that let you trade one scarcity for another; Event outcomes weighted toward hostility when your Frame subsystem is Degraded or Offline (`HostileTiltDelta` — shorthand for the asymmetric 4-axis tilt vector whose headline magnitude is the Ambush-axis `+15` out of 100; see Formula D.2 for the full `{−5, +15, −10, 0}` per-payload specification). The system secondary-serves Pillar 3 (Read to Win) by keeping outcome surfaces legible before commit — Unknown doesn't mean arbitrary; it means a readable weighted distribution — and Pillar 4 (Scarcity with Agency) by routing Merchant/Chopshop/Event/Rest interactions through the established Scrap Economy verb vocabulary (`TryPurchase`, `TryConvert*`, `TryInstall`, `TryRepair`, `TryPurge`) without ever opening a new resource pool.

Node Encounter does not own combat mechanics (Card Combat System), reward selection (Loot & Reward), graph structure (Node Map), or resource verbs (Scrap Economy). It is the thin orchestration layer where all four meet at the per-node granularity — the place where a graph edge becomes an experience.

## Player Fantasy

**Framing: "The Road Answers Back"**

The wasteland does not care about the player, but it does *register* them. When the vehicle is clean and the Frame is whole, the map is neutral — beacons resolve according to their declared distributions and no more. When the Frame is Degraded or Offline, the map tilts — Unknowns lean toward hostile outcomes, Events weight their rolls darker, the Elite at Strip-5 feels heavier than the Elites that preceded it. This isn't the world punishing the player. It's the wasteland doing what the wasteland does to a vehicle in the state theirs is in. The road answers what the car says. Route choice becomes a conversation — one the player can learn to speak better the longer they drive.

The anchor moment: two Unknown beacons sit in front of the player. Their Frame is Offline. Committing now would land on a hostile-tilted distribution. They detour west to a Rest beacon first, repair one tick, and come back to the Unknown with their posture changed. When it resolves to Windfall, they don't feel lucky — they feel like they spoke the right sentence to the road and the road answered plainly. When instead the detour costs them a turn of storm advance and the Unknown still resolves to Ambush, they don't feel cheated — they feel like the answer was the answer, and next run they'll speak differently.

This system **rejects** the framing of "dynamic difficulty," "adaptive world," or "the game reacts to you." The road has no agenda. `HostileTiltDelta` (the asymmetric Event-weight tilt vector, headline Ambush-axis `+15` out of 100) is not punishment; it is physics — a cracked Frame is a cracked Frame, and the wasteland treats cracked Frames the way cracked Frames deserve to be treated. The player is not being judged. They are being *read*, the same way they read the enemy's intent in combat. Pillar 3 (Read to Win) gets its secondary lift here: the player's skill is reading their own vehicle state as an input to the map's response, then routing accordingly. Unknown doesn't mean arbitrary; it means a weighted distribution you can read, *if* you know your own car well enough to know what weight you're carrying into it.

This fantasy sits calibrated against the adjacent GDDs. **Loot & Reward's** "ledger" is the principle that the road keeps score honestly — Node Encounter's "answer" is the mechanism by which the ledger gets balanced per-commit. **Enemy System's** Foresight spine (the enemy reads you as you read it) lives inside the combat beat; Node Encounter extends the same logic to the route layer — the *map* reads you as you read it, and the only divergence from declared distributions is the one your own vehicle's damage authored. **Scrap Economy's** wasteland register (quiet, material, inevitable) is the voice. No celebratory language. No "rewards" framed as gifts. Beacons are not rolls, they are answers. Haven at the end is the only answer that is also a kindness — the ember-orange terminal the road gives back when the player has spoken the route correctly for the length of the run.

**Design tests:**
- If a playtester says "I got lucky" after a Windfall, the fantasy is failing — they should say "I earned the read." Windfall must feel like a plain answer to a well-routed approach, not a dice roll that went their way.
- If `HostileTiltDelta` feels like the game is targeting the player, the framing has leaked — it must read as an environmental constant, not an AI behavior.
- If the Haven beacon doesn't land warmer than every other beacon it follows, Pillar 5's terminal payoff fails. The ember-orange exclusivity of Haven is the contract.
- If a player can route the same way across every chassis and get comparable results, Pillar 5 is failing through Node Encounter. Chassis state (storm cadence, starting Scrap, FuelBurnMultiplier) must translate into felt route divergence, not just stat divergence.

## Detailed Design

### Core Rules

#### C.1 Handler Contract (shared across all 7 beacon types)

Every beacon type resolves through a handler that implements `INodeEncounterHandler`. Node Map commits the player to a beacon and hands off via a single entry point; the handler resolves the encounter, applies economy effects through Scrap Economy verbs, and returns a `BeaconOutcome` payload via callback. Node Map then advances the storm (only on Combat/EliteCombat close) and recomputes the reachable set.

**Interface signature:**
```
INodeEncounterHandler.Begin(
    beacon:     BeaconData,            // immutable snapshot: BeaconType, nodeIndex, biomeIndex, isBoss, EncounterType (Card Combat R15)
    runSeed:    int,                   // from run-level seed
    frameState: FrameSubsystemState,   // { Nominal, Degraded, Offline } sampled at commit
    economy:    IScrapEconomy,         // verb interface only — no field access
    callback:   Action<BeaconOutcome>
)
```

**EncounterType on BeaconData (Card Combat R15 retrofit 2026-04-24).** `EncounterType` on the snapshot is meaningful only when `BeaconType ∈ {Combat, EliteCombat}` (or when an Event payload resolves to Ambush per C.2.5). It defaults to `Standard` and is authored per-node by Node Map at graph-generation time. Non-combat handlers (Merchant, Chopshop, Rest, Haven, Event non-Ambush payloads) ignore the field. The Combat/EliteCombat handlers pass it through to `CombatSetup.EncounterType` unchanged at Card Combat dispatch — NE never mutates or overrides it.

**Resolve flow (every handler must follow this order):**
1. **Seed once.** `var rng = new System.Random(runSeed ^ beacon.NodeIndex)` — created once at `Begin` entry, stored as instance field, never recreated or re-seeded.
2. **Determine outcome** using the seeded rng and type-specific logic.
3. **Apply economy effects exclusively via named Scrap Economy verbs** — never read or write Scrap/Fuel counts directly.
4. **Build `BeaconOutcome`** reflecting committed state.
5. **Invoke `callback(outcome)` exactly once.**

**`BeaconOutcome` schema:**

| Field | Type | Description |
|-------|------|-------------|
| `BeaconType` | `BeaconType` enum | Mirrors the beacon that was resolved |
| `PayloadType` | `EncounterPayload` enum | `None, Combat, Treasure, Ambush, Windfall, Convert, Rest, Merchant, Chopshop, Haven` |
| `WasCombatRewardClosed` | `bool` | `true` only when Combat/EliteCombat reward screen is dismissed — drives storm advance |
| `ScrapDelta` | `int` | Net Scrap change (positive = gained, negative = spent) |
| `FuelDelta` | `int` | Net Fuel change |
| `CardsOffered` | `CardId[]` | Cards presented to the player (may be empty) |
| `PartOffered` | `PartId?` | Nullable — only Treasure/Chopshop payloads populate |
| `RunTerminated` | `bool` | `true` on player death (Combat only) |

`BeaconOutcome` is a plain C# record/struct. No MonoBehaviour. No Unity lifecycle.

**Handler invariants (all seven types):**
1. **Single rng instance.** The rng is created once in `Begin`, passed by reference through all internal methods. No per-call re-seeding.
2. **Exactly one callback.** Zero invocations stall Node Map; multiple invocations corrupt graph state. Enforced by an `_hasResolved` guard that throws in development builds on second invocation.
3. **Economy-before-callback.** All verb calls complete before `callback` is invoked. No "cleanup" verb calls after callback.
4. **Never touch Node Map state.** Handler receives a `BeaconData` snapshot; it returns an outcome. Graph mutation (storm, reachable set, visited) is Node Map's alone.
5. **Never directly access Scrap/Fuel values.** Only verb calls and verb return booleans.

#### C.2 Per-Handler Rules

**C.2.1 — Combat handler**
- Launches Card Combat System with `EnemyDefinition` selected by Enemy System for `beacon.biomeIndex`, `isBoss=false`.
- **Dispatches Card Combat with `CombatSetup.EncounterType = beacon.EncounterType`** (Card Combat R15). Default `Standard` unless Node Map authored `Ambush` on this node. NE never defaults or overrides — the value on `BeaconData` is authoritative.
- On combat end, waits for reward screen dismissal before building `BeaconOutcome`.
- Calls `GenerateRewards(context: Combat[biomeN, node:N], seed)` on L&R; applies offer via reward-screen flow.
- Sets `WasCombatRewardClosed = true` on reward-screen close.
- On player death: `RunTerminated = true`, skips reward generation.

**C.2.2 — EliteCombat handler**
- Identical to Combat except: picks elite-tier `EnemyDefinition` for the biome; if `beacon.isBoss` is true (Strip-5 biome gate only), picks biome-boss variant.
- **Dispatches Card Combat with `CombatSetup.EncounterType = beacon.EncounterType`** (Card Combat R15). Ambush-authored elite nodes are permitted (Biome 3 narrative flavor). **Boss nodes (`isBoss == true`) MUST carry `EncounterType.Standard`** — the bossfight readability contract (Pillar 3) requires player-first turn order; Ambush on a boss is an authoring error. The Node Map graph-gen validator rejects `EncounterType.Ambush` on any `isBoss == true` node.
- Reward context: `EliteCombat[biomeN, node:N]` or `Boss[biomeN, node:N]` when `isBoss`.
- Applies `EliteScrapBonus = +18` or `BossFlatScrap = +30` via L&R's reward context (NE does not grant directly).
- Sets `WasCombatRewardClosed = true` on reward-screen close.

**C.2.3 — Merchant handler**
- Calls `GenerateRewards(context: Merchant[node:N], seed)` on L&R; receives exactly **3 priced card offers**.
- Player actions and verb routing:

| Player action | Verb fired | Notes |
|---|---|---|
| Browse (no commit) | None | Read-only `CurrentScrap` display |
| Purchase a card | `TryPurchase(card, price, "Merchant[node:N]")` | Deducts registered `MerchantPrice(rarity)` |
| Convert then purchase | `TryConvertFuelToScrap(amount, "Merchant[node:N]")` then `TryPurchase(...)` | Two sequential verb calls; each logged |
| Skip and leave | None | No verb, no state mutation |

- Prices come from L&R-returned offers; NE never computes price locally.
- Convert-at-Merchant is permitted per Scrap Economy Rule 5 (caller-agnostic verbs). Fantasy fit: *leaving with less gas than you came with* — a material trade.

**C.2.4 — Chopshop handler**
- Calls `GenerateRewards(context: Chopshop[node:N], seed)` on L&R; receives **3 priced part offers**.
- Exposes the verbs: `TryInstall`, `TryRepair`, `TryPurge`, `TryScrapPart`, `TryConvertScrapToFuel`, `TryConvertFuelToScrap`.
- Offering caps: 3 parts to install; all damaged subsystems eligible for repair; all deck cards eligible for purge; all non-Frame installed parts eligible for scrap.
- Pricing: L&R owns part prices; Repair/Purge costs computed locally via registered formulas (`RepairCost(currentDamage, rarity)`, flat `GlobalPurgeCost`).
- Frame protection invariant: `TryScrapPart` must refuse Frame parts (enforced by Scrap Economy verb).
- **Design intent distinguishing Merchant vs. Chopshop:** Merchant is forward-looking (buy something new, pure intake). Chopshop is maintenance-facing (repair, purge bloat, install reactively). The player skipping a Merchant says "I don't need that card." The player skipping a Chopshop says "I can absorb more damage."

**C.2.5 — Event handler**

Event is the "Unknown" beacon. **Unknown does not mean arbitrary — it is a readable weighted distribution the player can learn.** The contract: every Event rolls exactly one of four payloads from a known weight table, tilted only by Frame state. Over a run the distribution is legible; over many runs its shape is locked in the player's model.

The handler rolls the payload at **commit time** (when the player selects the Unknown beacon on the map), using `rng = new System.Random(runSeed ^ nodeIndex)`. Frame state is sampled at the same moment. Rolling at commit (not arrival) is deterministic under save/reload and closes the reload-abuse window where a player could degrade Frame mid-travel to observe tilt.

**Base weight table (integer, out of 100):**

| Payload | Base Weight (Frame Nominal) | Tilted Weight (Frame Degraded or Offline) |
|---|---|---|
| Treasure | 35 | 30 |
| Ambush | 20 | 35 |
| Windfall | 30 | 20 |
| Convert | 15 | 15 |

Tilt deltas (integer, out of 100): Ambush +15, Windfall −10, Treasure −5, Convert unchanged. Weights sum to exactly 100 in both states. Degraded and Offline apply the same delta (not compounded) — Offline already carries distinct mechanical consequences in V&P's Frame state machine; stacking a larger Event tilt on top produces unfair death spirals rather than legible physics. See Formula D.2 for the canonical storage representation.

**Payload sub-rules:**

- **Treasure** — calls `GenerateRewards(context: Event[node:N:Treasure], seed)` on L&R. Returns **one** reward offer (card, part, or resource grant per L&R's pity/pool logic). Presented via standard reward screen. Not auto-claimed.
- **Ambush** — chains directly into a Combat encounter against a biome-appropriate enemy; Combat handler takes over **with `CombatSetup.EncounterType = Ambush`** (Card Combat R15 — the Event-Ambush payload is the canonical source of Ambush encounters in MVP; `Standard` would undermine the payload's narrative point). No separate reward from Ambush itself; the chained Combat resolves reward via its own path. `WasCombatRewardClosed` will be set by the chained Combat handler.
- **Windfall** — auto-granted, no player prompt. 50/50 seeded roll: Scrap Windfall (flat `WindfallScrapGrant = 12`) or Fuel Windfall (draw from `FuelGrantRange` 1–3). Grant applied via `TryGrantScrap`/`TryGrantFuel`. The roll does not inspect player state — no reading of current Scrap/Fuel to decide which to grant.
- **Convert** — offers a single one-direction favorable conversion at a seeded-determined direction (Scrap→Fuel or Fuel→Scrap). Rate: `EventConvertFavorableRate = 3` (vs. baseline 4 on both directions from Scrap Economy). `FuelOut = Floor(ScrapIn / 3)` or `ScrapOut = Floor(FuelIn / 3)`. Player opts in via `TryConvertScrapToFuel` or `TryConvertFuelToScrap` with `nodeContext: "Event[node:N:Convert]"`. Skip is free. Only one direction per Convert node — prevents round-trip arbitrage.

**C.2.6 — Rest handler**
- Free restoration. No Scrap spent.
- Player chooses which subsystem to repair (no auto-targeting of worst-off).
- Restores one subsystem fully (Offline→Nominal or Degraded→Nominal) via `TryRepair(subsystem, "Rest[node:N]", freeRepair: true)` — extends `TryRepair` with a zero-cost path. A light retrofit on Scrap Economy GDD is queued to document the zero-cost flag.
- This is the beat where the Player Fantasy anchor lands: clearing a Degraded Frame before an Unknown is *speaking the right sentence to the road*.

**C.2.7 — Haven handler**
- Run-end terminus. No verb calls.
- Handler emits `BeaconOutcome` with `PayloadType = Haven`, all deltas zero.
- Presentation layer (UI) takes over from here — final Scrap tally, node count, chassis summary. Meta-currency accrual (if added in a future GDD) is UI-owned, not NE-owned.
- Haven's exclusivity is a presentation contract (ember-orange, register-warm), enforced by UI — no mechanical gating needed at the handler.

#### C.3 Per-Biome Distribution Tables

Node Encounter authors the per-biome beacon distribution; Node Map reads these at graph-generation time. Haven is hard-placed at the Biome 3 terminus, not percentage-weighted.

| Beacon Type | Biome 1 (Onramp) | Biome 2 (Pressure) | Biome 3 (Resolution) |
|---|---|---|---|
| Combat | 30% | 35% | 30% |
| EliteCombat | 5% | 8% | 10% |
| Merchant | 15% | 10% | 8% |
| Chopshop | 10% | 12% | 10% |
| Event | 20% | 20% | 17% |
| Rest | 20% | 15% | 15% |
| Haven | 0% | 0% | 1 fixed (terminus) |

**Biome-intent framing:**
- **Biome 1 — Onramp.** Frames are mostly intact; `HostileTiltDelta` rarely fires. Merchant front-loaded so the player can shape the deck before pressure arrives. Rest density generous (supports Truck's pressure-1 cadence).
- **Biome 2 — Pressure / Commitment.** Combat density peaks; Rest falls; the road *starts answering back*. Events tilt more often because Frames are now damaged — the same 20% Event rate reads darker.
- **Biome 3 — Resolution.** Fewer Merchants (Scrap is scarce; any appearance is a meaningful decision). Event density drops slightly — resolution biome earns legibility over dice-roll energy. Strip-5 is boss (`isBoss = true`); Haven sits beyond it.

#### C.4 Structural Invariants (hold regardless of tuning)

1. Every biome contains ≥1 Chopshop accessible from the critical path — repair is always a real option, never route-locked out.
2. EliteCombat count per biome: minimum 1, maximum 3 (not counting Strip-5 boss).
3. Minimum 2 Rest beacons per biome reachable within a 2-strip window of the Strip-5 gate.
4. Minimum branching width of 3 parallel routes per strip mid-biome — below this, P5 chassis routing divergence collapses to aesthetics.
5. No two consecutive strips may be Combat-only across all paths.
6. Merchant never appears on Strip-4 or Strip-5 within a biome — buying on the gate step kills the "committed your Scrap before you needed it" tension.

#### C.5 Strip-Level Clustering Intent (soft rules for graph generation)

- **Merchant** front-loads Strips 1–2; shuttered by Strip-3.
- **Chopshop** clusters mid-biome, Strips 2–4.
- **Event** peaks Strips 2–3 (Frame state uncertain, tilt has room to mean something); thins before the gate.
- **Rest** concentrates at Strip-1 (onramp recovery) and Strip-4 (pre-gate window — *speak the right sentence before the boss*).
- **EliteCombat** never Strip-1; appears Strip-3 onward.
- **Combat** distributed; slight dip at Strip-1 (learning) and Strip-5 (boss absorbs that beat).

### States and Transitions

| State | Enter Condition | Exit Condition | Next State |
|---|---|---|---|
| `Idle` | Run initialized or prior encounter completed | Node Map calls `Begin()` | `Dispatched` |
| `Dispatched` | `Begin()` called; rng seeded | Handler type resolved | `HandlerActive` |
| `HandlerActive.Combat_AwaitingResult` | Combat/EliteCombat handler launched Card Combat | Card Combat returns `CombatResult` | `HandlerActive.Combat_AwaitingRewardClose` |
| `HandlerActive.Combat_AwaitingRewardClose` | `CombatResult` received; reward screen shown | Player dismisses reward screen | `Resolving` |
| `HandlerActive.Event_Rolled` | Event handler sampled weighted distribution | Payload effect applied (may chain into Combat for Ambush) | `Resolving` |
| `HandlerActive.Simple` | Merchant/Chopshop/Rest/Haven; synchronous sub-flow | Effect applied (or player skip) | `Resolving` |
| `Resolving` | Effect application complete; `BeaconOutcome` built | `callback(outcome)` invoked | `Returned` |
| `Returned` | `callback` invoked | Node Map processes outcome | `Idle` |

**Hard rule:** `WasCombatRewardClosed = true` can only be set during `HandlerActive.Combat_AwaitingRewardClose` exit. No other path touches it. This preserves the invariant that only Combat/EliteCombat reward closure advances the storm.

### Interactions with Other Systems

| System | Direction | Interface | Ownership |
|---|---|---|---|
| **Node Map** | Upstream → NE | `BeaconData` snapshot + `callback` delegate at `Begin` | Node Map owns graph state; NE owns handler dispatch |
| **Node Map** | NE → Downstream | `BeaconOutcome` return; Node Map reads `WasCombatRewardClosed` to advance storm and recomputes reachable set | Node Map acts on outcome |
| **Loot & Reward** | NE → L&R | `GenerateRewards(context, seed) → RewardOffer[]` pure function | L&R owns reward content, pricing, pity logic; NE consumes |
| **Scrap Economy** | NE → Scrap | Caller-agnostic verbs: `TryPurchase`, `TryConvertScrapToFuel`, `TryConvertFuelToScrap`, `TryInstall`, `TryRepair` (with zero-cost path for Rest), `TryPurge`, `TryScrapPart`, `TryGrantScrap`, `TryGrantFuel` | Scrap owns purse state; NE never reads/writes directly |
| **Enemy System** | NE → Enemy | `EnemyDefinition` selection by biome + elite/boss tier | Enemy owns stat curves and AI; NE invokes by type |
| **Card Combat** | NE → Combat Scene | Handoff on Combat/EliteCombat dispatch; `CombatResult` returned to NE | Combat owns combat simulation; NE orchestrates entry/exit |
| **V&P / Frame State** | V&P → NE | `FrameSubsystemState` read-only at `Begin` (commit time) | V&P owns subsystem state machine; NE samples, never mutates |
| **Save System** | Save → NE | Determinism contract: given same `runSeed`, `nodeIndex`, and `frameState` at commit, handler produces identical `BeaconOutcome` | Save owns serialization; NE guarantees reproducibility |

**Key interaction invariants:**
- NE never authors combat mechanics, reward contents, graph structure, or resource verb semantics — it is a thin orchestration layer.
- `HostileTiltDelta` is consulted ONLY by the Event handler. Combat/EliteCombat difficulty is biome-authored by Enemy System. Merchant/Chopshop/Rest/Haven never consult Frame state.
- Frame state sampled at **commit** (player selects beacon), not arrival. If Frame degrades during travel, the tilt does not retroactively apply.

## Formulas

All formulas use **integer arithmetic** for determinism. No floating-point sampling. Event weights are stored as integers out of 100.

### D.1 Seed Derivation

**Variables:**
- `runSeed` — int32; sourced at run start from `DateTime.UtcNow.Ticks.GetHashCode()` per Save System contract
- `nodeIndex` — int; unique per node in the graph (0 .. ~50 for a 3-biome run)

**Formula:**
```
seed = runSeed XOR nodeIndex
rng  = new System.Random(seed)
```

**Range:** `seed` is any int32. Identical `(runSeed, nodeIndex)` always produces identical rng sequence across platforms.

**Example:** `runSeed = 0x12345678`, `nodeIndex = 14` → `seed = 0x12345676`. Handler stores rng instance as field; all subsequent rolls derive from this single instance.

### D.2 Event Weight Table Transform

**Variables:**
- `W_base` — int[4] indexed by payload: `{Treasure: 35, Ambush: 20, Windfall: 30, Convert: 15}`
- `ΔW` — int[4] tilt deltas: `{Treasure: −5, Ambush: +15, Windfall: −10, Convert: 0}`
- `frameState` ∈ `{Nominal, Degraded, Offline}` sampled at commit

**Formula:**
```
W_effective[payload] = W_base[payload]                     if frameState == Nominal
W_effective[payload] = W_base[payload] + ΔW[payload]       if frameState ∈ {Degraded, Offline}
```

**Invariants:**
- `Sum(W_base) = 100` (35 + 20 + 30 + 15)
- `Sum(W_base + ΔW) = 100` (30 + 35 + 20 + 15)
- All entries of `W_effective ≥ 0` in both states

**Example:**

| frameState | Treasure | Ambush | Windfall | Convert |
|---|---|---|---|---|
| Nominal | 35 | 20 | 30 | 15 |
| Degraded | 30 | 35 | 20 | 15 |
| Offline | 30 | 35 | 20 | 15 |

Degraded and Offline produce the same row — tilt is not compounded at Offline.

### D.3 Weighted Payload Sampling (Cumulative CDF)

**Variables:**
- `rng` — the seeded `System.Random` instance from D.1
- `W` — int[4] weight vector, `Sum(W) = 100`
- `payloadOrder` — fixed enum order: `[Treasure, Ambush, Windfall, Convert]`

**Formula:**
```
roll       = rng.Next(100)    // integer in [0, 99]
cumulative = 0
for payload in payloadOrder:
    cumulative += W[payload]
    if roll < cumulative:
        return payload
return payloadOrder[3]   // fallback; unreachable when Sum(W) = 100
```

**Range:** `roll ∈ [0, 99]`. Output ∈ `payloadOrder`.

**Invariant:** `payloadOrder` MUST be fixed at enum-declaration order. Sorting or shuffling breaks determinism.

**Example (Frame = Nominal, W = {35, 20, 30, 15}; cumulative boundaries 35 / 55 / 85 / 100):**

| roll | Boundary crossed | Payload |
|---|---|---|
| 0 | 0 < 35 | Treasure |
| 34 | 34 < 35 | Treasure |
| 35 | 35 < 55 | Ambush |
| 54 | 54 < 55 | Ambush |
| 55 | 55 < 85 | Windfall |
| 84 | 84 < 85 | Windfall |
| 85 | 85 < 100 | Convert |
| 99 | 99 < 100 | Convert |

**Example (Frame = Offline, W = {30, 35, 20, 15}; cumulative boundaries 30 / 65 / 85 / 100):**
- `roll = 40` → Ambush. In Nominal with the same roll, the result would also be Ambush — but the probability mass shifted: the Ambush zone widened from 20 units (Nominal) to 35 units (Offline).

### D.4 EventConvert Rate Formulas

**Variables:**
- `ScrapPerFuelRate = 4` (Scrap Economy baseline)
- `FuelPerScrapRate = 4` (baseline)
- `EventConvertFavorableRate = 3`

**Formulas:**
```
Direction selection (seeded):
    isScrapToFuel = rng.Next(2) == 0       // 50/50 seeded

Scrap → Fuel (favorable):
    FuelOut  = Floor(ScrapIn / 3)

Fuel → Scrap (favorable):
    ScrapOut = Floor(FuelIn  / 3)

Baseline comparison (for player messaging):
    FuelOut_baseline  = Floor(ScrapIn / 4)
    ScrapOut_baseline = Floor(FuelIn  / 4)
```

**Range:** all integer arithmetic; no rounding ambiguity. Minimum useful input = 3; below, output is 0.

**One direction per Convert node** — prevents round-trip arbitrage.

**Example (Scrap→Fuel, player has 15 Scrap):**
- Favorable: `FuelOut = Floor(15 / 3) = 5 Fuel` (deducts 15 Scrap)
- Baseline would have yielded: `Floor(15 / 4) = 3 Fuel`
- Player gains 2 Fuel vs. baseline (67% more)

**Example (Fuel→Scrap, player has 7 Fuel):**
- Favorable: `ScrapOut = Floor(7 / 3) = 2 Scrap`
- Baseline: `Floor(7 / 4) = 1 Scrap`
- Player gains 1 Scrap vs. baseline (100% more)

**Implementation note:** actual verb routing still uses `TryConvertScrapToFuel` / `TryConvertFuelToScrap` with a rate override parameter scoped to this node context. No new verbs introduced.

### D.5 Windfall Seeded Coin Flip

**Variables:**
- `rng` — handler-local seeded instance
- `WindfallScrapGrant = 12`
- `FuelGrantRange.Min = 1`, `FuelGrantRange.Max = 3`

**Formula:**
```
isScrapWindfall = rng.Next(2) == 0       // 50% branch

if isScrapWindfall:
    grant = WindfallScrapGrant           // flat 12
    economy.TryGrantScrap(grant, "Event[node:N:Windfall]")
    outcome.ScrapDelta = +grant
else:
    grant = rng.Next(FuelGrantRange.Min, FuelGrantRange.Max + 1)   // [1, 3]
    economy.TryGrantFuel(grant, "Event[node:N:Windfall]")
    outcome.FuelDelta = +grant
```

**Range:**
- Scrap grant: exactly 12 when the Scrap branch fires
- Fuel grant: discrete uniform ∈ {1, 2, 3} when the Fuel branch fires
- No inspection of player state — the branch is seeded, not adaptive

**Example:**
- `rng.Next(2) = 0` → Scrap branch → +12 Scrap. `BeaconOutcome.ScrapDelta = +12`
- `rng.Next(2) = 1` → Fuel branch. `rng.Next(1, 4) = 2` → +2 Fuel. `BeaconOutcome.FuelDelta = +2`

**Invariant:** `rng.Next(FuelGrantRange.Min, FuelGrantRange.Max + 1)` uses `+1` because `System.Random.Next(min, max)` is upper-exclusive. Off-by-one here would produce {1, 2} only.

### D.6 Storm-Advance Predicate

**Variable:** `outcome.WasCombatRewardClosed` (bool on `BeaconOutcome`)

**Formula:**
```
advanceStorm = outcome.WasCombatRewardClosed
```

**Range:** `true` only for Combat / EliteCombat handlers after reward-screen dismissal; `false` for all other handlers.

**Ambush chaining:** an Event beacon that rolls Ambush does NOT advance the storm from its Event handler — it hands off to the Combat handler which sets the flag on its own reward-screen close. Storm advances exactly once, for the chained Combat.

**Example:**
- Event → Ambush → Combat (win) → reward-screen close → `WasCombatRewardClosed = true` → storm advances +1
- Event → Windfall → +12 Scrap → `WasCombatRewardClosed = false` → storm does NOT advance
- EliteCombat (boss) → win → reward-screen close → `WasCombatRewardClosed = true` → storm advances +1 (Node Map then applies biome-gate transition)

## Edge Cases

Each edge case specifies: **trigger** → **exact behavior** → **invariant preserved**.

### E.1 Player death during Event-Ambush chained combat

**Trigger:** Event beacon rolls Ambush → Combat handler takes over → player dies mid-combat.

**Behavior:** Combat handler builds `BeaconOutcome` with `RunTerminated = true`, `WasCombatRewardClosed = false` (no reward screen on death), zero deltas. Event handler short-circuits — does NOT build a separate outcome, does NOT invoke its own callback. The chained Combat's outcome IS the beacon's outcome. Node Map receives exactly one `BeaconOutcome` for the Event node and processes run termination.

**Invariant:** `callback` is invoked exactly once per `Begin` regardless of chaining depth. Event → Ambush → Combat is a single dispatch from Node Map's perspective.

### E.2 Save or reload during active handler dispatch

**Trigger:** Player attempts to save, or the game crashes, while the handler is in any `HandlerActive` sub-state.

**Behavior:** Save System refuses serialization while `State != Idle` — the save menu's "save" action is disabled, and auto-save does not fire. Auto-save fires exclusively when the handler returns to `Idle` (after `BeaconOutcome` is fully processed by Node Map). On crash mid-handler, the last auto-save is the pre-beacon-commit state; the player re-commits to the same beacon on reload. Because `(runSeed, nodeIndex, frameState)` are identical on replay, the deterministic contract produces the identical `BeaconOutcome` — no meta-gaming loophole.

**Invariant:** Player cannot quit mid-Merchant and reload to see different offers. The handler's rng sequence is a function of seed alone, and the save point is outside the handler's lifetime.

### E.3 Loot & Reward returns empty RewardOffer[]

**Trigger:** `GenerateRewards(context, seed)` returns a zero-length array for a Merchant, Chopshop, or Treasure context (pool exhausted, all filtered out, or upstream bug).

**Behavior:** NE applies a salvage fallback: grant `BiomeBaseScrap.BiomeN` for the current biome via `TryGrantScrap(amount, "Salvage[node:N]")`. `BeaconOutcome.ScrapDelta = +BiomeBaseScrap.BiomeN`, `CardsOffered = []`, `PartOffered = null`, `PayloadType` remains the original beacon's payload type. The UI shows a "salvage" state — brief, register-consistent, not a "failed" beacon.

**Scope:** Applies to Merchant, Chopshop, and Event-Treasure only. Combat/EliteCombat reward contexts are owned end-to-end by L&R and have their own fallback rules per L&R GDD.

**Invariant:** The road never gives nothing back at a transactional beacon. Fits "the road keeps its ledger honestly."

### E.4 Frame state changes during encounter resolution

**Trigger:** During an active handler (especially Event-Ambush combat), the player takes damage that degrades Frame from Nominal → Degraded, or Degraded → Offline.

**Behavior:** `HostileTiltDelta` is NOT re-applied. The Event payload was rolled at commit using the commit-time Frame snapshot; the payload is fixed from that moment onward. Mid-encounter Frame changes affect Combat mechanics (per Card Combat and V&P GDDs) but do NOT retroactively re-roll or re-weight the Event outcome.

**Invariant:** Frame state is sampled once at `Begin()`, stored on the handler, never re-read. This preserves the "commit-time determinism" contract (D.1) and closes reload-abuse windows.

### E.5 Invalid or unregistered BeaconType dispatched

**Trigger:** Node Map calls `Begin()` with a `BeaconType` value that has no registered handler (data bug, enum desync, unfinished content).

**Behavior:** The handler factory throws `NodeEncounterDispatchException` in development builds (fail loud; stops the session). In production builds, logs a fatal error, emits a telemetry event, builds a zero-delta `BeaconOutcome` with `PayloadType = None` and returns control to Node Map so the run can attempt to continue (avoids bricking a player's run). Auto-save state is preserved.

**Invariant:** Unknown beacon types fail visibly in dev; production degrades to minimum-harm while surfacing the issue for postmortem.

### E.6 EventConvert offered but player has insufficient input

**Trigger:** Event rolls Convert with direction Scrap→Fuel; player's Scrap is below the minimum useful input (3).

**Behavior:** Handler still presents the Convert offer with its rate. The `TryConvertScrapToFuel` verb returns false if the player attempts to convert more than their balance. UI shows the actual achievable amount ("Convert 0 Scrap → 0 Fuel") and the player can decline. Skip is free. No verb fires on decline.

**Invariant:** Node Encounter never hides the Convert offer based on player resources — visibility is deterministic, not adaptive. The verb layer enforces balance limits.

### E.7 Round-trip arbitrage attempt

**Trigger:** Player reaches a second EventConvert node in the same run and attempts to reverse the prior direction at favorable rates.

**Behavior:** Each EventConvert node offers exactly one direction, seeded-determined at commit. A player who converted Scrap→Fuel at node 12 may find node 23 also offers Scrap→Fuel (not a guaranteed reverse). Even if a subsequent node rolls the opposite direction, the baseline-comparison math prevents net-positive round-trips because:
- Scrap→Fuel at favorable: `Floor(S/3)` Fuel
- Fuel→Scrap at favorable: `Floor(F/3)` Scrap
- Round-trip of 9 Scrap: 9 → 3 Fuel → 1 Scrap (net −8 Scrap)

**Invariant:** The one-direction-per-node structural rule (D.4) combined with integer-floor arithmetic makes arbitrage mathematically net-negative.

### E.8 Windfall Fuel grant exceeds Fuel cap

**Trigger:** Windfall Fuel branch rolls; player's current Fuel + rolled grant would exceed `FuelCap` (from V&P GDD).

**Behavior:** `TryGrantFuel` tops up to `FuelCap`; excess is silently dropped. `BeaconOutcome.FuelDelta` reports the actual applied amount (rolled − overflow), not the nominal roll. Example: player at Fuel 9/10, roll = 3 → `FuelDelta = +1`, excess 2 lost.

**Invariant:** Fuel never exceeds `FuelCap`. The Windfall seeded roll does not inspect player state before rolling (D.5) — overflow handling is downstream of the verb, not a branching decision in the handler. Determinism preserved.

### E.9 Double-dispatch attempt

**Trigger:** Node Map calls `Begin()` on a handler that is already in a `HandlerActive` state (bug in Node Map, duplicate input, double-tap on beacon).

**Behavior:** The handler's `_hasResolved` guard is checked at `Begin` entry; if any non-`Idle` state is active, throws `NodeEncounterDispatchException` in dev builds and is silently no-op'd in production (logs warning). The existing handler continues its lifecycle.

**Invariant:** Exactly one active handler at any time. Exactly one callback per `Begin`.

### E.10 Haven reachability in dying state

**Trigger:** Can a player reach Haven while at 0 HP, with DoT ticking, or otherwise "should-be-dead"?

**Behavior:** Impossible by construction. Reaching Haven requires resolving the Strip-5 boss (Combat handler) in Biome 3. The Combat handler sets `RunTerminated = true` on player death before returning `BeaconOutcome`, and Node Map terminates the run without advancing to Haven. Between Strip-5 and Haven there are no intermediate encounters that could apply damage — storm advance ended with the boss close.

**Invariant:** Haven is reachable only by surviving the terminal Combat. No handler can chain from a terminated run into Haven.

### E.11 Handler never invokes callback

**Trigger:** Implementation bug — a handler enters `HandlerActive` but fails to reach `Resolving` (infinite loop, exception thrown and caught silently, async task never completes).

**Behavior:** In development builds, a watchdog timeout (configurable, default 30 seconds for non-Combat, 10 minutes for Combat) fires, logs the stall with full state, and throws. In production, the same watchdog catches the stall and force-invokes `callback` with a zero-delta `BeaconOutcome` plus `PayloadType = None` to avoid locking the player's run; telemetry event emitted. The auto-save from beacon-commit lets the player retry on a subsequent launch.

**Invariant:** Node Map cannot be left stuck in `HandlerActive` forever. The callback contract is enforced by the runtime, not just by handler code.

## Dependencies

### F.1 Upstream Dependencies (Node Encounter consumes)

| System | Interface NE Consumes | Consumed Behavior | Bidirectional Status |
|---|---|---|---|
| **Node Map** | `INodeEncounterHandler.Begin(beacon, runSeed, frameState, economy, callback)` | `BeaconData` snapshot at commit; graph-owned identity | ✅ Confirmed — Node Map GDD documents the handoff and the `INodeEncounterHandler` interface explicitly |
| **Loot & Reward** | `GenerateRewards(context, seed) → RewardOffer[]` pure function | Reward content, pricing, pity, rarity sampling for Combat / EliteCombat / Boss / Merchant / Chopshop / Event-Treasure contexts | ⚠️ Retrofit queued — L&R `BeaconType` axis naming (Boss → EliteCombat+isBoss; Treasure → Event+TreasurePayload) + salvage fallback contract on empty returns |
| **Scrap Economy** | Caller-agnostic verbs: `TryPurchase`, `TryConvertScrapToFuel`, `TryConvertFuelToScrap`, `TryInstall`, `TryRepair`, `TryPurge`, `TryScrapPart`, `TryGrantScrap`, `TryGrantFuel` | All resource mutation; NE never touches purse directly | ⚠️ Retrofit queued — document `TryRepair` zero-cost path (`freeRepair: true`) used by Rest handler |
| **Enemy System** | `EnemyDefinition` selection by `(biomeIndex, tier)` where tier ∈ {Combat, Elite, Boss} | Stat curves, AI behavior, intent distribution per enemy | ✅ Confirmed — Enemy GDD documents Node Map/Encounter as consumer of `EnemyDefinitionSO` by biome |
| **V&P / Frame Subsystem** | `FrameSubsystemState` read-only snapshot at `Begin()` | Tilt input for Event handler; NE never mutates V&P state | ⚠️ Retrofit queued — document `FuelCap` overflow semantics on `TryGrantFuel` (excess silently dropped, per E.8) |
| **Card Combat System** | Handoff on Combat/EliteCombat dispatch with `CombatSetup.EncounterType`; receives `CombatResult` back | Combat simulation (Standard + Ambush encounter types per Card Combat R15), turn resolution, player-death detection | ⚠️ Retrofit queued — confirm `CombatResult` schema includes fields NE's `BeaconOutcome` mapping requires (death flag, reward-offer pointer). **Card Combat R15 `CombatSetup.EncounterType` contract closed by this GDD's C.1 / C.2.1 / C.2.2 / C.2.5 Ambush-payload retrofits (2026-04-24 Position & Movement propagation).** |
| **Save System** | `runSeed` at run start; determinism contract for replay | Reproducible rng sequences per `(runSeed, nodeIndex)` | ⚠️ Retrofit queued — save blocked during any `HandlerActive` state; auto-save fires at `Idle` transitions only (per E.2) |
| **Card System** | Card pool for Merchant/Treasure offers (routed via L&R) | Card rarity pools, offer filtering | ✅ Confirmed indirectly — Merchant/Treasure card offers flow L&R → NE → Card System via standard reward-screen contract |

### F.2 Downstream Dependents (Systems that consume Node Encounter)

| System | What NE Provides | Consumed Behavior | Bidirectional Status |
|---|---|---|---|
| **Node Map** | `BeaconOutcome` payload via callback | `WasCombatRewardClosed` drives storm advance; other deltas/flags drive reachable-set recompute, run termination | ✅ Confirmed — same interface as upstream (single-return-path contract) |
| **UI / Combat HUD** | Handler dispatch state transitions (`Dispatched` → `HandlerActive` → `Resolving` → `Returned`) | UI renders Merchant screen, Chopshop UI, Event payload reveal, reward screen, Rest subsystem picker, Haven terminus presentation | ⚠️ Retrofit queued — UI layer needs a formal "handler state observer" contract; route to Combat HUD UX + Post-Combat Flow UI retrofits |
| **Card System** | Card offers added to deck via Merchant/Treasure purchases (through Scrap verbs) | Deck mutation on successful `TryPurchase` | ✅ Confirmed — mediated by Scrap Economy's verb contract |
| **Save System** | Handler state reporting (`Idle` vs any `HandlerActive` sub-state) | `Save.CanSave` predicate; auto-save trigger at `Idle` transitions | ⚠️ Retrofit queued — same entry as F.1 Save System row |

### F.3 Consolidated Retrofit Queue

Retrofits raised by Node Encounter's contract that require updates to adjacent GDDs:

1. **Scrap Economy GDD** — document `TryRepair(subsystem, nodeContext, freeRepair: true)` zero-cost path (enables free Rest repairs without bypassing the verb layer). Source: C.2.6.
2. **Node Map GDD** — confirm commit-time Frame sampling for Event beacons aligns with `Begin()` handoff semantics. Source: C.2.5.
3. **Save System GDD** — document that save/auto-save is blocked during any `HandlerActive` state; auto-save fires exclusively at `Idle` transitions. Source: E.2.
4. **Loot & Reward GDD** — (a) clarify `BeaconType` axis naming/scope (`Boss` = `EliteCombat + isBoss`; `Treasure` = `Event + TreasurePayload`); (b) document salvage fallback contract — what NE does when `GenerateRewards` returns empty for Merchant/Chopshop/Event-Treasure. Source: C preamble + E.3.
5. **V&P GDD** — confirm `FuelCap` overflow semantics for `TryGrantFuel` (excess silently dropped, not rejected). Source: E.8.
6. **Card Combat GDD** — confirm `CombatResult` schema exposes the fields NE maps into `BeaconOutcome` (`RunTerminated`, reward offer pointer, damage summary) **and that `CombatSetup` accepts `EncounterType` (Standard / Ambush) populated from `beacon.EncounterType` at dispatch (Card Combat R15 — closed 2026-04-24).** Source: F.1 row + C.2.1.
7. **UI / Post-Combat Flow UX** — formalize handler-state observer contract for Merchant/Chopshop/Rest/Haven screens. Source: F.2 row.
8. **Node Map GDD** — surface `EncounterType` on Combat / EliteCombat node preview UI (Pillar 3 pre-commit readability contract per Card Combat R15). Node Map owns per-node `EncounterType` authoring at graph-generation time; the preview MUST display an Ambush indicator on the node icon so the player reads the encounter type before commit. Source: C.1 BeaconData authoring rule + C.2.1 / C.2.2 dispatch bullets.

### F.4 Forward Commitments

Systems that will consume Node Encounter in the future but do not yet exist as GDDs (noted so their future design documents know to reference NE):

- **Meta-progression / Unlock System** (post-MVP) — will consume run-end data via Haven handler's `BeaconOutcome`. NE's Haven contract (zero verbs, presentation only) leaves the accrual logic to the meta-layer.
- **Telemetry / Analytics** — will consume handler dispatch events and `BeaconOutcome` payloads for player-behavior data. NE contract does not yet emit telemetry explicitly; deferred to Analytics GDD.
- **Modding / Content Pipeline** (post-MVP) — any custom beacon type would register through the handler factory. F.1 Node Map row implies this extensibility but it is not formalized.

## Tuning Knobs

Every tunable value in the Node Encounter System. Safe ranges are derived from the section's invariants; values outside a safe range either break an invariant or produce an unhealthy distribution shape.

### G.1 Event Payload Distribution — Base Weights

Drives the baseline feel of Unknown beacons at a Nominal Frame. The sum invariant (`Sum = 100`) must hold after any change.

| Knob | Current | Safe Range | Gameplay Aspect |
|---|---|---|---|
| `EventBaseWeight.Treasure` | 35 | 25–45 | Frequency of L&R-authored reward offers at Unknowns; the "hopeful default" of Unknown |
| `EventBaseWeight.Ambush` | 20 | 15–30 | Baseline combat-chain probability at Unknowns; tension on clean Frames |
| `EventBaseWeight.Windfall` | 30 | 20–40 | Baseline free-grant frequency; how often the road gifts plainly |
| `EventBaseWeight.Convert` | 15 | 10–25 | Baseline Convert-offer frequency; how often economy-flex appears |

**Tuning rule:** raising Ambush without lowering Treasure/Windfall shifts the register harsher; raising Treasure/Windfall without corresponding lift on Ambush makes Unknowns feel consequence-free. Tune in paired strokes.

### G.2 Event Payload Distribution — Tilt Deltas (HostileTiltDelta)

Applied when `frameState ∈ {Degraded, Offline}`. Sum invariant (`Sum(ΔW) = 0`) must hold; all resulting effective weights must remain ≥ 0.

| Knob | Current | Safe Range | Gameplay Aspect |
|---|---|---|---|
| `EventTiltDelta.Ambush` | +15 | +10 to +20 | How hard a cracked Frame "tilts" the road hostile |
| `EventTiltDelta.Windfall` | −10 | −5 to −15 | How much the free-payout odds shrink when Frame is damaged |
| `EventTiltDelta.Treasure` | −5 | −3 to −8 | How much the reward-offer odds shrink when Frame is damaged |
| `EventTiltDelta.Convert` | 0 | locked at 0 | Convert offers don't tilt; economy-flex is a function of purse, not Frame |

**Tuning rule:** the absolute magnitude of `EventTiltDelta.Ambush` is the primary dial for P5 bite ("how hard does a cracked Frame change the road?"). Below +10, players will not notice; above +20, legibility fails — players will feel targeted rather than read.

**Derived constant (reference):** `HostileTiltDelta = 0.15` (total magnitude of tilt) is documented in registry; computed from these deltas (`|Ambush|`), not independently tunable.

### G.3 Windfall Grants

Applied on Windfall payload selection. Seeded 50/50 branch; no player-state inspection.

| Knob | Current | Safe Range | Gameplay Aspect |
|---|---|---|---|
| `WindfallScrapGrant` | 12 | 8–20 | Value of Scrap-branch Windfall; must sit between `DS_FLOOR_BONUS`=4 and `BiomeBaseScrap.Biome1`=15 to read as "small find" |
| `FuelGrantRange.Min` | 1 | 1–2 | Lower bound on Fuel-branch Windfall (already registered via L&R) |
| `FuelGrantRange.Max` | 3 | 2–4 | Upper bound on Fuel-branch Windfall |

**Tuning rule:** Windfall must never outweigh a Combat reward for the same biome — if `WindfallScrapGrant` approaches `BiomeBaseScrap.Biome2=28`, the player will prefer Events over Combats, which inverts P5 intent.

### G.4 EventConvert Favorable Rate

Offered by Event-Convert payload. Baseline rate (Scrap Economy) is 4.

| Knob | Current | Safe Range | Gameplay Aspect |
|---|---|---|---|
| `EventConvertFavorableRate` | 3 | 2–3 (denominator) | How much better Event-Convert is than baseline. At 2 the rate becomes exploitable on large purses; at 3 the advantage is meaningful but bounded by integer-floor arithmetic |

**Hard constraint:** must be strictly less than baseline `ScrapPerFuelRate=4` to be "favorable." Must be ≥ 2 to prevent trivial arbitrage with small inputs.

### G.5 Merchant and Chopshop Offer Caps

Applied when handler calls `GenerateRewards`.

| Knob | Current | Safe Range | Gameplay Aspect |
|---|---|---|---|
| `MerchantOfferCount` | 3 | 2–4 | Cards presented per Merchant. At 2, scarcity feels cruel; at 4, scarcity identity erodes |
| `ChopshopPartOfferCount` | 3 | 2–4 | Parts presented per Chopshop. Matches Merchant cadence by design |

**Tuning rule:** if these ever diverge (e.g., Chopshop=4, Merchant=3), the two beacon types read asymmetrically — resist divergence unless the asymmetry is intentional and CD-approved.

### G.6 Per-Biome Beacon Distribution Percentages

The per-biome distribution table is 21 tunable values (7 beacons × 3 biomes). Each must satisfy the per-biome sum invariant: rows sum to 100% (with Haven hard-placed at Biome 3 terminus, not percentage-contributing).

| Beacon Type | Biome 1 (Onramp) | Biome 2 (Pressure) | Biome 3 (Resolution) |
|---|---|---|---|
| Combat | 30% (safe 25–35) | 35% (safe 30–40) | 30% (safe 25–35) |
| EliteCombat | 5% (safe 3–8) | 8% (safe 5–12) | 10% (safe 8–15) |
| Merchant | 15% (safe 10–20) | 10% (safe 8–15) | 8% (safe 5–12) |
| Chopshop | 10% (safe 8–15) | 12% (safe 10–15) | 10% (safe 8–15) |
| Event | 20% (safe 15–25) | 20% (safe 15–25) | 17% (safe 12–22) |
| Rest | 20% (safe 15–25) | 15% (safe 10–20) | 15% (safe 10–20) |
| Haven | 0 | 0 | 1 fixed node (not percentage) |

**Per-biome sum invariant:** row must sum to 100% (Haven row excluded). Changing one beacon's percentage requires compensating changes elsewhere in the same biome row.

**Tuning rule:** the Combat column drives pressure pacing (30 → 35 → 30 is the "inhale → peak → resolve" arc). Flattening this curve (e.g., 33/33/34) erases the biome identity established in Section B. The Rest column inversely tracks it — when Combat peaks, Rest should fall.

**First-telemetry-pass watch row — Biome 3.** CD-GDD-ALIGN review flagged Biome 3's low Merchant floor (8%) + low Event floor (17%) + flat Rest (15%) as the distribution row most likely to produce a dry-resource spiral (Scrap starvation at the moment Chopshop prices are highest). Tuners should watch this row first at the first-wave telemetry pass; do not assume Biome 2 is the pressure point. OQ-NE3 (Windfall scaling by biome) is the correct hedge if the spiral surfaces — inflating Windfall to 15 in Biome 3 restores supply without disrupting distribution shape.

### G.7 Structural Invariants (Soft Knobs)

Rules that constrain graph generation; technically tunable but treated as invariants because lowering them breaks P5 intent.

| Knob | Current | Safe Range | Gameplay Aspect |
|---|---|---|---|
| `MinChopshopPerBiomeOnCriticalPath` | 1 | 1 (lower bound locked) | Guarantees repair is never route-locked out |
| `EliteCountPerBiome.Min` | 1 | 1–2 | Minimum elite encounters before Strip-5 boss |
| `EliteCountPerBiome.Max` | 3 | 2–4 | Cap on elite density; above 4, biome becomes a gauntlet |
| `MinRestWithin2StripsOfGate` | 2 | 1–3 | Pre-gate recovery availability (anchor for Section B's Rest-before-Unknown moment) |
| `MinBranchingWidthPerStrip` | 3 | 3–5 | Parallel route count; below 3, P5 chassis divergence collapses |
| `NoConsecutiveCombatOnlyStrips` | true | locked at true | Pacing invariant; cannot be disabled without CD approval |
| `MerchantForbiddenStrips` | {4, 5} | locked at {4, 5} | Pre-gate Merchant disabled (commit-your-Scrap tension invariant) |
| `beacon.EncounterType` (per Combat / EliteCombat node) | `Standard` | {`Standard`, `Ambush`} | Card Combat R15 dispatch payload. Default `Standard`. `Ambush` authored per-node by Node Map at graph-generation / content-authoring time (NOT a live-tuning knob). Boss nodes (`isBoss == true`) locked to `Standard` — validator rejects otherwise. Event-Ambush payloads dispatch as `Ambush` regardless of node annotation (C.2.5). |

### G.8 Runtime Watchdogs (from E.11)

| Knob | Current | Safe Range | Gameplay Aspect |
|---|---|---|---|
| `HandlerTimeout.NonCombat` | 30 seconds | 15–60 seconds | Dev-build stall detection for Merchant/Chopshop/Event/Rest/Haven |
| `HandlerTimeout.Combat` | 10 minutes | 5–20 minutes | Dev-build stall detection for Combat/EliteCombat (must accommodate longest realistic combat) |

**Tuning rule:** production builds still watchdog-recover by force-invoking `callback` with zero deltas; these timeouts affect the detection window, not runtime behavior on success.

## Visual/Audio Requirements

Node Encounter owns the *reveal* and *arrival* presentation for every beacon. Card Combat owns combat-scene VFX/SFX; V&P owns vehicle VFX; L&R owns reward-card presentation. This section specifies only the NE-owned beats: beacon arrival framing, the Event reveal (Unknown → payload), the HostileTiltDelta visual/audio tell, per-handler establishing shots and audio beds, and the two exclusivity contracts that protect Haven's register.

### H.1 Overarching Register (visual + audio rules)

**Visual register (wasteland-industrial, `RUST_ICON` art direction):**
- Palette: bleached sky, sun-bleached steel, rust-orange accents, grime/oil-dark neutrals. No saturated primaries.
- Icon style: silhouette-forward, readable at 64×64 on the map. Material-metaphor over symbolism (a stripped cab reads as *merchant*, not a pictogram of a shop).
- Motion budget per reveal: ≤0.5s total; no camera moves on reveal; no particles that obscure silhouette.
- **Exclusivity contract #1 (palette):** warm-orange interior-light glow (value ≥ 0.6, warm-shifted) is reserved for Haven presentation. No other handler may use warm-orange fills or halos — rust-orange *accents* on weathered metal are allowed everywhere else.

**Audio register (dry, diegetic, material-first):**
- Default bed: ambient wind layer, low-mid dominant, no tonal pitch content. Minimal reverb (dry exterior).
- Diegetic sources only: engines, metal, wind, distant weather, mechanical tools. No synthesized "UI" stingers that break fiction.
- **Exclusivity contract #2 (melodic):** sustained melodic/harmonic content (tonal pitch held >1.5s, recognizable interval relationships) is reserved for Haven arrival. All other beacons use material-sound stings, rhythmic texture, or ambient wind only. Short (<1s) diegetic harmonics from engines/resonance are permitted — sustained *music* is not.

### H.2 Event Reveal Beat (Unknown → Payload)

The Event icon arrives on the map as an **Unknown** silhouette (a weathered hazard-diamond with a rust-worn "?" embossed on corroded metal, not a painted symbol). At commit-time, `Begin` fires the reveal beat; by the time the handler scene renders, the payload identity is final.

**Visual (icon-morph, ~0.4s total, no camera move):**
- Frame 0 (0.00s): Unknown silhouette, held.
- Frames 1–3 (0.00–0.20s): rust-corrosion dissolve mask sweeps diagonally across the icon from upper-left to lower-right; Unknown reads as *corroding away* rather than fading.
- Frames 4–6 (0.20–0.40s): payload silhouette resolves underneath — Treasure (cache crate with rope-tie), Ambush (silhouetted approaching headlamp-pair), Windfall (tipped fuel-can with spill shadow), Convert (ratchet+bolt paired icon).
- Hold on resolved silhouette ≥ 0.4s before handler transition.

**Audio (payload-specific material sting, 0.3–0.7s, fires at 0.0s with icon-dissolve start):**
- **Treasure**: metallic panel-lift scrape + single muted clank. Material: thin sheet-steel against concrete. No resonance tail.
- **Ambush**: low engine harmonic (approach-tell), fades *in* from −12dB to −3dB over 1.2s, starts at reveal and sustains until handler transition. This is the only sting that bridges into the handler bed. No stinger attack — it is a *texture arrival*, not a jumpscare.
- **Windfall**: hollow drum ring (single hit, like an empty jerrycan struck once), 0.3s decay. No pitch content.
- **Convert**: ratchet click-clunk (mechanical pair, 0.25s apart). Dry, material, rhythmic.

**Rationale:** the rust-corrosion dissolve reinforces "the road decays in front of you" (Pillar 5 / "The Road Answers Back") rather than "a UI element flipped." The material-sound stings per-payload teach the vocabulary diegetically — players learn *treasure = panel scrape* before they learn the word.

### H.3 HostileTiltDelta Tell (Frame Degraded/Offline)

When `FrameState ∈ {Degraded, Offline}` at the moment an Event beacon is selected (hover/focus, pre-commit), the tilt-modified weights are surfaced via a *subtle environmental* tell — not a communicative HUD warning.

**Visual tell (hover/selection only, not passive on map):**
- Rust-shimmer overlay on the Event icon: a low-amplitude animated noise sampled against a rust-normal map, ~2% luminance variation, 1.5s loop. Reads as *the icon is weathering faster* under the hover cursor.
- Icon is otherwise unchanged in silhouette, color, and position.

**Audio tell (hover/selection only):**
- Ambient wind bed receives +3dB boost below 200Hz while the Event beacon is selected. Reads as *the wind presses harder* on this specific beacon. Removed instantly on deselection.

**Explicitly rejected alternatives (documented so they stay rejected):**
- (a) Red tint / warning color on the Event icon — violates register; reads as UI telemetry not world.
- (c) Text label or numeric weight display — violates "Enemy Foresight" diegetic-read principle.
- (d) Particle flicker / animated hazard pulse — too loud; reads as *punishment telegraph* which Section B explicitly rejected ("HostileTiltDelta is physics, not punishment").

**Rationale:** the tell is *readable if the player learns to feel it*, *ignorable if they don't*. Matches Pillar 3 (Read to Win) and Section B's "earned the read" design test.

### H.4 Per-Handler Establishing Visual (arrival, not full scene)

The handler scene proper is owned downstream (Card Combat / L&R / V&P dressings). NE owns only the *arrival establishing shot* — what the player sees in the first 1–2 seconds before the handler's own UI mounts.

| Handler | Establishing visual | Notes |
|---|---|---|
| **Combat** | Silhouetted enemy vehicle slides in from right edge, dust trail; 0.75s pan-left as player vehicle rolls into frame on left | Register: kinetic, tension-building. Hands off to Card Combat's scene mount at 0.75s. |
| **EliteCombat** | Same as Combat + enemy silhouette held 0.3s longer with a second dust-plume layer behind it | Reads as "bigger silhouette, more dust" — no color shift, no music sting. Scale-up, not register-shift. |
| **Merchant** | Stripped cab stall beside the rail, hand-lettered signboard (illegible weathered text), **no vendor face, no NPC portrait** — a gloved hand is visible on the counter, that is all | Explicit anti-StS rule: no faces. Merchant is a *place*, not a person. Preserves wasteland-register solitude. |
| **Chopshop** | Concrete pad with tungsten work lamp (warm but *harsh*, not cozy), tool scatter, raised lift frame | Tungsten work lamp is cooler and more clinical than Haven's interior glow — different warm source, different register. |
| **Event** | No establishing shot — handler opens directly into the payload scene (Treasure panel-lift, Ambush combat mount, Windfall grant screen, Convert prompt) | Events *interrupt*, they don't *arrive*. The reveal beat in H.2 is the only NE-owned visual. |
| **Rest** | L-shaped windbreak shelter, cooler palette (blue-grey shadows, no warm sources), thermos or camp kit visible on a flat rock | Deliberately *not* warm. Rest is repair, not respite. Windbreak reads as *function*, not *home*. |
| **Haven** | Settlement silhouette at middle distance, **warm interior light visible through wall gaps** (value ≥ 0.6, warm-shifted), faint smoke-plume from a chimney | Only handler that breaks the cool-palette default. This is the payoff for Pillar 5's "Route Reflects Vehicle State" — the route finally answers with *arrival*, not another encounter. |

### H.5 Per-Handler Audio Bed (arrival, ≤2s before handoff)

| Handler | Arrival audio bed | Handoff point |
|---|---|---|
| **Combat** | Engine-brake bridge: player vehicle downshift (0.4s) → enemy engine approach (0.35s) | 0.75s — hands off to Card Combat audio |
| **EliteCombat** | Same bridge + additional low-register engine layer (bigger engine = more cylinders) | 0.75s — same handoff |
| **Merchant** | Cooling engine ticks (metal contracting), distant wind, no human voice; 1.0–1.5s bed before UI mount | 1.5s |
| **Chopshop** | Hammer-ring + pneumatic hiss, 3–4s rhythmic cycle (work *continues* while player shops) | 1.5s; bed continues under UI |
| **Event** | No NE-owned bed — handler audio starts immediately after the H.2 reveal sting | N/A |
| **Rest** | Wind-only bed, fabric-flap of tarp/windbreak; no human sound | 1.5s |
| **Haven** | Low sustained vocal-or-string harmonic (the *only* sustained melodic element in the game outside of credits) + campfire crackle layer. **The harmonic must remain at −9dB and NOT swell until player initiates arrival confirmation** — a pre-emptive swell would telegraph Haven as "the good ending" and break the earned-arrival contract | 2.0s; harmonic holds under Haven presentation UI |

### H.6 Exclusivity Contracts (enforced at art/audio review)

Two contracts protect Haven's register. Both must be enforced during asset review — any handler asset that violates them is rejected regardless of in-isolation quality.

**Contract 1 — Warm-orange palette (value ≥ 0.6, warm-shifted) is reserved for Haven.**
- Violated by: warm interior lights on Merchant stalls, warm campfire glow on Rest shelters, warm sun rays on any non-Haven establishing shot.
- Allowed: rust-orange accents on weathered metal, tungsten work-lamp harshness (Chopshop — cooler and less saturated), dust-haze ambers at low luminance (<0.5).

**Contract 2 — Sustained melodic/harmonic content (tonal pitch >1.5s with interval relationships) is reserved for Haven arrival.**
- Violated by: any handler audio bed with a sustained pitched tone, Merchant "shopkeeper theme" music, Rest "peaceful reflection" pad, Event "mystery" arpeggio.
- Allowed: short diegetic harmonics (engine resonance, metal-ring decay <1s), rhythmic non-pitched texture, Ambush approach-tell low harmonic (qualifies as sustained but is dread-coded, not melodic — single tone, no interval content).

### H.7 Drift-Risk Flags (surface during review)

Six drift risks that break register if not actively policed. The first five guard *against* genre-drift (StS, FTL, horror, arcade, music-telegraph); the sixth guards *against* over-correction into warmth that would erode the Haven exclusivity contract.

1. **Merchant portrait/NPC face** — drifts toward *Slay the Spire* shopkeeper framing. Kills the solo-wasteland register. Merchant must remain faceless/place-not-person.
2. **Event reveal burst (camera punch, flash, particle explosion)** — drifts toward *FTL* event-popup framing. Reveal is a *material corrosion*, not a *UI event*.
3. **Ambush approach-tell swell pitched too sharp** — drifts toward horror register. Must stay low-mid engine harmonic, not a shriek or high-pitched rise.
4. **Convert audio using coin-clink / treasure-chime samples** — drifts toward arcade-economy register. Must stay mechanical (ratchet, bolt, click-clunk).
5. **Haven pre-emptive harmonic swell (music rises on arrival before player confirms)** — breaks the register exclusivity and telegraphs Haven as *the good ending*. Swell must be player-initiated at the presentation confirmation beat, not auto-triggered on scene mount.
6. **Rest beat reads as cozy-respite** (warm fill light, campfire VFX with warm glow ≥0.5 luminance, mellow melodic pad, crickets/hearth-ambient). Drifts *out of* the wasteland register and *into* Haven's warm-orange exclusivity space. Rest is **repair, not respite** — blue-grey shadows, wind-only bed, function-not-home. A cold-palette campfire at low luminance <0.5 is allowed only if it does not warm the scene's dominant fill. If the Rest establishing shot reads as "somewhere you want to stay," it has drifted.

**Review rule:** any asset submission for NE-owned beats must be checked against these 6 risks before acceptance. A submission that triggers any of the six is rejected back to author regardless of technical quality.

## UI Requirements

### I.1 Ownership Boundary

Node Encounter is a dispatcher, not a screen-owner. NE's UI ownership is limited to what only NE can own:

1. Handler-state observer contract (I.2)
2. Event reveal UI flow — Unknown → Payload icon-morph wiring (I.3)
3. HostileTiltDelta hover affordance routing (I.4)
4. Haven presentation UI (I.5 — Haven has no owner elsewhere)
5. Input blocking during handler transitions (I.6)
6. Keyboard/gamepad parity rules for NE-owned verbs (I.7)

**Deferred to owning GDDs** (Section F retrofits confirm these boundaries):
- Card Combat UI → **Card Combat GDD**
- Merchant / Chopshop / Event-Treasure reward offer UI → **Loot & Reward GDD**
- Rest repair UI → **V&P GDD + Scrap Economy GDD** (zero-cost TryRepair path)
- Post-Combat Flow UI → **Post-Combat Flow GDD** (subscribes to NE's handler-state observer from I.2)

### I.2 Handler-State Observer Contract

`NodeEncounterSystem` exposes two C# events. Downstream UI (map UI, Post-Combat Flow UI, save-blocker UI, telemetry) subscribes to these and never polls.

```csharp
public event System.Action<NodeMapBeacon> OnHandlerBegin;
public event System.Action<BeaconOutcome> OnHandlerEnd;
```

**Firing rules:**
- `OnHandlerBegin` fires exactly once per beacon commit, at the `Idle → HandlerActive` transition (see Section C states table), before the handler's `Begin` is invoked.
- `OnHandlerEnd` fires exactly once, at the `Returned → Idle` transition, after `BeaconOutcome` is finalized and after any Section D.6 storm-advance predicate resolution.
- Both events fire on the main thread. No async/thread-pool subscribers.

**Subscriber lifecycle rules (enforced at code review):**
- Subscribers register in Unity `Awake()` / `OnEnable()` and unregister in `OnDestroy()` / `OnDisable()`. Never in `Start()` (race condition if NE's first event fires before all scenes load).
- No `Update()` or `FixedUpdate()` loop may call `NodeEncounterSystem.IsActive` or equivalent polling API. If such a loop is found at review, reject and require event subscription.
- `NodeEncounterSystem.IsActive` may exist as a read-only convenience for *one-shot queries* (e.g., save system checking at save-menu open), but must never drive frame-loop behavior.

**Rationale:** polling misses single-frame transitions, lags input-blocker release by a frame (letting double-click through), and couples UI's update loop to NE's internal state object. Event-driven is the only implementation that satisfies E.10's arrival-flag semantics.

### I.3 Event Reveal UI Flow (Unknown → Payload)

The Event icon lives in the **map UI layer** (UI Toolkit), not as an in-world GameObject. The Unknown → Payload morph from H.2 is driven from UI on commit.

**Commit sequence (single-frame input → multi-frame animation):**

| T+ | Event | Owner |
|---|---|---|
| 0.00s (input frame) | UI fires `OnBeaconCommitAccepted(beacon)`; confirmation sound plays; input-block engages (I.6) | Map UI |
| 0.00s | NE samples commit-time RNG (Section D), determines `PayloadType`, fires `OnHandlerBegin` | NE |
| 0.00–0.40s | H.2 rust-corrosion dissolve animation plays on the map icon; payload silhouette resolves | Map UI (NE-owned animation asset) |
| 0.40–0.80s | Hold on resolved silhouette; map fades out | Map UI |
| 0.80s | Handler scene mounts | Handler owner |

**Animation trigger rule:** the reveal animation **fires on commit input regardless of focus-ring visibility**. Keyboard/gamepad players must not be required to focus-then-confirm-then-focus-again before animation begins. One input capture = one commit = one animation.

**State rule:** the Event icon never changes visual state based on `FrameState` (no warning tint, no color shift). The only hover-driven visual on the icon is the H.3 rust-shimmer tell (see I.4).

### I.4 HostileTiltDelta Hover Affordance

When `FrameState ∈ {Degraded, Offline}` and the player hovers or focuses an Event beacon, three subscribers fire in parallel. NE routes the hover state; the subscribers own the output.

**API:**
```csharp
public event System.Action<NodeMapBeacon> OnTiltActive;
public event System.Action<NodeMapBeacon> OnTiltInactive;
```

`OnTiltActive(beacon)` fires when: `beacon.Type == Event && FrameState ∈ {Degraded, Offline} && beacon.IsHoveredOrFocused`. `OnTiltInactive` fires when any condition becomes false.

**Subscribers (three channels, all environmental, none communicative):**
1. **Visual** — rust-shimmer overlay on icon (H.3). Subscribed by map UI renderer.
2. **Audio** — wind bed +3dB below 200Hz (H.3). Subscribed by audio system.
3. **Haptic** — gamepad low-frequency rumble pulse, `motorLevel ≈ 0.15`, 1.5s loop matching shimmer period. Subscribed by input system. KBM-only players fall back to visual-only (accepted gap, documented here).

**Hover-vs-focus parity rule:** `IsHoveredOrFocused` is true for **pointer hover (mouse) OR focus-ring selection (keyboard/gamepad)**. The tell MUST fire in focus-only navigation, not just on pointer hover. If the tell requires a mouse hover event to fire, gamepad users get no signal — that is a failure condition, not an acceptable gap.

**Prohibitions (enforced at art/UI review, restating H.3 as I-layer rules):**
- No tooltip, no numeric weight display, no text label, no color tint on the Unknown icon itself.
- No change to icon silhouette, position, or passive map-state visual under any `FrameState`.
- If a UI reviewer sees any element of the Event icon change based on Frame state other than the hover-activated shimmer overlay, reject.

### I.5 Haven Presentation UI

Haven is a single-screen presentation with one affordance. No verbs, no choice, no sub-screens.

**Screen layout:**
- Full-screen Haven establishing visual (per H.4: settlement silhouette, warm interior light ≥0.6 through wall gaps, faint smoke-plume).
- One text line overlay (Haven acknowledgment text — owned by writer, not this GDD).
- One affordance: **Continue** button, keyboard/gamepad default-focused, mouse-clickable.

**Audio swell sequence (the H.5 contract on UI):** the sustained harmonic holds at −9dB from scene mount. It does **not** swell until Continue is pressed.

| T+ | Event |
|---|---|
| 0.00s (scene mount) | Visual renders; harmonic at −9dB; Continue button enabled after 0.5s hold |
| 0.50s | Continue input accepted |
| 0.50s | UI fires `OnHavenContinue`; audio system raises harmonic to −3dB over 1.2s |
| 0.50–1.70s | Audio swells; visual holds still |
| 1.70s | Screen fades to run-summary / end-screen |

**Dismiss rule:** Continue press **triggers the swell, not the dismiss**. The screen holds until the swell peaks, then fades. Dismissing on input-frame collapses the harmonic's payoff.

**No features that drift toward post-run summary here:** no Scrap tally, no node count, no chassis review, no run statistics. Those belong on the post-run summary screen that follows Haven. Haven's only job is to hold the player in the warm-orange register for a deliberate moment.

### I.6 Input Blocking During Handler Transitions

When `hasArrived = true` (E.10) and handler state is not `Idle`, all map beacon interaction is blocked.

**Mechanism:** map UI sets `PickingMode = Ignore` on beacon interactive elements (UI Toolkit). Restored on `OnHandlerEnd → Idle` transition.

**Why not a transparent input-blocker overlay:**
- Overlays are invisible to accessibility tooling and screen readers.
- Overlays create hit-testing surfaces that swallow input ambiguously.
- Overlays are visually indistinguishable from a bug to an outside observer.
- Disabling `PickingMode` is explicit, testable, and reversible.

**Implementation rule:** the map UI subscribes to `OnHandlerBegin` (disable) and `OnHandlerEnd` (re-enable). The block is a property of the map layer, not a phantom element.

### I.7 Keyboard/Gamepad Parity Rules

Per `technical-preferences.md`, all core interactions must be KBM-accessible; gamepad is additive but must be functional for all NE-owned verbs.

**Rules:**
1. **No hover-only interactions.** Any state that fires on mouse-hover must also fire on keyboard/gamepad focus-ring selection (I.4 hover/focus parity).
2. **Single-input commit.** Pointer click = one input = one commit. Gamepad confirm = one input = one commit. Never require focus-then-confirm-then-re-focus on gamepad while mouse gets one click (I.3).
3. **Confirmation sound on input-frame, before animation.** The commit sound plays the same frame input is captured, not at animation start. Tying the sound to animation start creates perceptible delay on slower machines and breaks the closed-loop feedback.
4. **Focus ring visible for keyboard/gamepad, invisible for mouse.** Standard Unity UI Toolkit focus behavior; no NE-specific override needed — but the H.2 reveal animation plays identically regardless of focus-ring visibility.
5. **Haven Continue defaults focus** to the single button. Gamepad `A` / keyboard `Enter` or `Space` commits.

### I.8 Drift-Risk Flags

Three high-likelihood UI implementation mistakes to catch at review:

1. **Polling observer.** A `Update()` loop that calls `NodeEncounterSystem.IsActive` to gate map input. Rejects I.2 event contract. Flag: search codebase for `IsActive` calls in per-frame loops; any match requires event refactor.
2. **Warning-color tint on Unknown icon under HostileTiltDelta.** "Just visual polish" framing. Violates I.4 / H.3 / B's "earned the read" contract. Flag: any UI change to the Event icon based on `FrameState` other than the hover-activated shimmer is rejected.
3. **Transparent input-blocker overlay** instead of `PickingMode = Ignore`. Violates I.6. Flag: any new full-screen transparent UI element added during handler transitions is rejected; use the map layer's interaction toggle.

## Acceptance Criteria

Every AC is pass/fail-verifiable. Tests span three environments: **unit** (pure deterministic math — formulas, weight math, seed derivation), **integration** (handler dispatch, observer events, state transitions), and **walkthrough/review-gate** (UI behavior, exclusivity contracts, art/audio asset review). Numbering prefix `AC-NE` follows project convention.

### J.1 Handler Dispatch & Lifecycle

- **AC-NE1** — Given a valid beacon of any BeaconType, `NE.Commit(beacon)` from `Idle` state transitions NE to `HandlerActive` and invokes exactly one `INodeEncounterHandler.Begin()` on the handler registered for that BeaconType.
- **AC-NE2** — `OnHandlerBegin(beacon)` fires exactly once per commit, synchronously, before the handler's `Begin()` call.
- **AC-NE3** — On handler callback, NE transitions `HandlerActive → Returned → Idle` in that order; `OnHandlerEnd(BeaconOutcome)` fires exactly once at the `Returned → Idle` transition, after `BeaconOutcome` is finalized.
- **AC-NE4** — While NE is in `HandlerActive` or `Returned`, additional `Commit` calls are rejected without invoking any handler (`hasArrived` flag, E.10).
- **AC-NE5** — `BeaconOutcome` returned by any handler populates all required fields: `BeaconType`, `PayloadType`, `WasCombatRewardClosed`, `ScrapDelta`, `FuelDelta`, `CardsOffered`, `PartOffered`, `RunTerminated`.
- **AC-NE6** — Storm advance fires iff `outcome.WasCombatRewardClosed == true` (D.6). For non-Combat/EliteCombat handlers, `WasCombatRewardClosed` is false by contract; storm does not advance.
- **AC-NE7** — All NE-owned C# events (`OnHandlerBegin`, `OnHandlerEnd`, `OnTiltActive`, `OnTiltInactive`) fire on the Unity main thread (verified by thread-ID assertion in tests).
- **AC-NE8** — `NE.IsActive` returns `true` iff state ∈ {`HandlerActive`, `Returned`}; `false` iff state == `Idle`.

### J.2 Event Payload Determinism

- **AC-NE9** — For a given `(runSeed, nodeIndex)` pair, committing an Event beacon produces the same `PayloadType` across 100 repeated test runs (D.1 determinism).
- **AC-NE10** — RNG seed derivation = `runSeed XOR nodeIndex` (D.1). Uses `new System.Random(seed)`. No call to `UnityEngine.Random` in NE code paths (enforced at review; grep test).
- **AC-NE11** — Given `FrameState ∈ {Intact, OutOfFuel}`, over 10,000 Event commits the observed PayloadType distribution matches base weights 35/20/30/15 (Treasure/Ambush/Windfall/Convert) within ±2%.
- **AC-NE12** — Given `FrameState ∈ {Degraded, Offline}`, over 10,000 Event commits the distribution matches tilted weights 30/35/20/15 (base + deltas: Treasure −5, Ambush +15, Windfall −10, Convert 0) within ±2%. Sum = 100.
- **AC-NE13** — Weights are stored as integers out of 100; the active weight array sums to exactly 100 under all FrameStates; tilted weights produce no negative values.
- **AC-NE14** — EventConvert favorable rate produces `Floor(input/3)` output; baseline (non-Event) conversion produces `Floor(input/4)`. Net round-trip (in on one node, out on another) is mathematically ≤ input for all `input ∈ [1..1000]` (arbitrage-negative — exhaustive test).
- **AC-NE15** — Windfall samples `rng.Next(2)`: 0 → Scrap grant 12, 1 → Fuel grant in `[1,3]`. Over 10,000 samples the Scrap/Fuel split is 50/50 within ±2%.

### J.3 HostileTiltDelta Tell

- **AC-NE16** — Frame sampling for HostileTiltDelta uses `FrameState` at commit-time (C.2.5, E.4). Frame state changes after commit do not retroactively alter the sampled payload.
- **AC-NE17** — Tilt applies only when `BeaconType == Event`. For Combat, EliteCombat, Merchant, Chopshop, Rest, Haven commits, FrameState has zero effect on dispatch math.
- **AC-NE18** — `OnTiltActive(beacon)` fires when: `beacon.Type == Event AND FrameState ∈ {Degraded, Offline} AND (IsHovered OR IsFocused)`. Fires on pointer hover OR keyboard/gamepad focus-ring — not only mouse hover.
- **AC-NE19** — `OnTiltInactive` fires when any of the three conditions becomes false. Shimmer overlay, sub-200Hz audio boost, and gamepad haptic all cease within 1 frame.
- **AC-NE20** — Gamepad rumble during `OnTiltActive` runs at `motorLevel ≈ 0.15` on the low-frequency motor; stops cleanly on `OnTiltInactive`. KBM-only players get visual-only (no error log, no "missing feedback" assertion).

### J.4 Per-Handler Rules

- **AC-NE21** — Merchant handler invokes `L&R.GenerateRewards(RewardContext.Merchant, seed)` requesting exactly 3 cards.
- **AC-NE22** — Chopshop handler invokes `L&R.GenerateRewards(RewardContext.Chopshop, seed)` requesting exactly 3 parts.
- **AC-NE23** — Rest handler calls `IScrapEconomy.TryRepair(subsystem, nodeContext, freeRepair: true)`. No Scrap is deducted (test asserts `scrapAfter == scrapBefore`).
- **AC-NE24** — Windfall payload auto-grants via `TryGrantScrap(12)` or `TryGrantFuel([1,3])` with no player choice; handler closes and fires `OnHandlerEnd` within 1.5s of `Begin`.
- **AC-NE25** — EventConvert payload permits exactly one direction of exchange per node commit (Scrap→Fuel OR Fuel→Scrap, not both). After commit, the reverse direction is locked for that node in the RNG stream.
- **AC-NE26** — Event Treasure payload invokes `L&R.GenerateRewards(RewardContext.Treasure, seed)`; no combat scene is mounted.
- **AC-NE27** — Event Ambush payload mounts Card Combat with `CombatSetup.EncounterType = Ambush` (Card Combat R15); `BeaconOutcome.WasCombatRewardClosed` is populated by the Card Combat callback, not by NE.
- **AC-NE27a** — Combat and EliteCombat handlers pass `beacon.EncounterType` through to `CombatSetup.EncounterType` unchanged. Test asserts dispatched `CombatSetup.EncounterType == beacon.EncounterType` for both `Standard` and `Ambush` authored nodes. Default `Standard` is verifiable by inspecting an authored combat node with no Ambush annotation.
- **AC-NE27b** — Boss nodes (`EliteCombat + isBoss == true`) assert `EncounterType == Standard` at dispatch. Node Map graph-gen validator rejects `EncounterType == Ambush` on any `isBoss == true` node (pre-dispatch guard). Pillar 3 bossfight readability contract.
- **AC-NE28** — Haven handler presents the Haven UI (I.5). No verb grants or deducts Scrap, Fuel, Cards, or Parts. `BeaconOutcome` returns `RunTerminated=true` and all deltas=0.

### J.5 Per-Biome Distribution

- **AC-NE29** — Per-biome beacon distribution across Strips 2–4 matches Section C tables within ±1 count per beacon type per strip (integer-rounding tolerance).
- **AC-NE30** — Strip 5 contains exactly 1 Haven beacon (fixed, not weighted). Strip 5 boss-gated Combat is `EliteCombat + isBoss=true`.
- **AC-NE31** — Haven appears only on Strip 5. Strips 1–4 contain zero Haven beacons across all three biomes (grep-verifiable on generated node graphs).
- **AC-NE32** — No two adjacent strips have back-to-back EliteCombat beacons on the same vertical lane (structural invariant from Section C).

### J.6 Edge Cases

- **AC-NE33** — When `NE.IsActive == true`, `SaveSystem.IsSaveAllowed` returns `false`; save attempts during `HandlerActive`/`Returned` are rejected with user-facing feedback (E.2).
- **AC-NE34** — Auto-save fires exactly at the `Returned → Idle` transition after each beacon resolution. No auto-save fires during handler execution.
- **AC-NE35** — If `L&R.GenerateRewards` returns empty `RewardOffer[]` for Merchant/Chopshop/Event-Treasure contexts, the handler degrades to a salvage grant of `BiomeBaseScrap.BiomeN` (E.3).
- **AC-NE36** — Granting Fuel in excess of `FuelCap` grants up to cap; excess is silently discarded with no error log or UI alert (E.8).
- **AC-NE37** — Invalid BeaconType in dev builds throws `InvalidBeaconTypeException` and halts (fail loud). In production builds, logs a warning and skips to storm advance with zero deltas (min-harm, E.5).
- **AC-NE38** — Non-Combat handler exceeding `HandlerTimeout.NonCombat` (default 30s) triggers watchdog: NE force-invokes `callback` with zero deltas and transitions `Returned → Idle` (E.11).
- **AC-NE39** — Combat handler exceeding `HandlerTimeout.Combat` (default 10 min) triggers watchdog similarly.
- **AC-NE40** — Haven beacon is unreachable when `Frame == Offline` by graph construction — dying-state player cannot pathfind to a Haven node (E.9).
- **AC-NE41** — If Card Combat callback returns `BeaconOutcome.RunTerminated == true` (player-death mid-Combat or mid-Ambush), NE transitions `Returned → Idle`; storm-advance does not fire; run-end flow is triggered by downstream (E.1).
- **AC-NE42** — EventConvert with zero-Scrap or zero-Fuel input does not allow commit at UI: the exchange button is disabled when input would produce zero output (E.6).
- **AC-NE43** — During player-death mid-handler, no auto-save fires (combined invariant from AC-NE34 + AC-NE41).

### J.7 Observer & UI Event Contract

- **AC-NE44** — `OnBeaconCommitAccepted(beacon)` fires on the same frame input is captured; confirmation sound plays before reveal animation starts (I.7 rule 3).
- **AC-NE45** — Reveal animation (H.2 rust-corrosion dissolve) plays on commit input regardless of focus-ring visibility; never requires focus-then-confirm-then-re-focus on keyboard/gamepad (I.3).
- **AC-NE46** — During `HandlerActive`, map UI beacon elements have `PickingMode = Ignore`. Restored to `Position` on `OnHandlerEnd` firing (I.6).
- **AC-NE47** — No `Update()` or `FixedUpdate()` loop in the codebase calls `NodeEncounterSystem.IsActive` for per-frame decision logic (grep-verifiable review gate, I.8 drift-risk #1).
- **AC-NE48** — Subscribers to `OnHandlerBegin`/`OnHandlerEnd` register in `Awake`/`OnEnable` and unregister in `OnDestroy`/`OnDisable`. Over 100 scene reload cycles, subscriber count returns to baseline (no leaks).

### J.8 Haven Presentation

- **AC-NE49** — Haven UI Continue button enables only after 0.5s scene-mount hold. Pre-0.5s input is silently dropped.
- **AC-NE50** — Harmonic holds at −9dB from scene mount until Continue input accepted. No pre-emptive swell (H.7 risk #5).
- **AC-NE51** — Haven Continue input triggers harmonic swell to −3dB over 1.2s; screen fade begins at 1.7s post-input, not on input-frame (I.5).

### J.9 Exclusivity Contracts & Drift-Risk

- **AC-NE52** — No handler other than Haven uses warm-orange palette (value ≥ 0.6, warm-shifted) in any establishing visual or UI overlay. Enforced at art review; automated test: sample pixel values on establishing-shot renders and assert no non-Haven shot exceeds the warm threshold in the inspection region (H.6 Contract 1).
- **AC-NE53** — No handler other than Haven uses sustained melodic/harmonic audio (pitched tone >1.5s with interval relationships). Enforced at audio review; spectral-analysis test flags non-Haven audio beds with sustained pitched content (H.6 Contract 2).
- **AC-NE54** — Merchant establishing visual contains no NPC face, no portrait (H.4 + H.7 risk #1). Only gloved hand and signboard visible. Art-review rejection rule.
- **AC-NE55** — Event reveal contains no camera punch, no flash, no particle explosion. Animation is icon-morph only, ≤0.5s total, no camera transform changes during reveal (H.2 + H.7 risk #2).
- **AC-NE56** — Ambush audio sting uses low-mid engine harmonic approach-tell (fundamental frequency < 250Hz). No sharp/shriek/high-pitched content (H.2 + H.7 risk #3). Spectral-analysis test.
- **AC-NE57** — Convert audio uses mechanical ratchet/bolt/click-clunk material. Sample library must not include coin-clink or treasure-chime audio assets (H.2 + H.7 risk #4). Asset-naming review gate.

**Coverage trace:** every locked decision in Sections C/D/E/F/G/H/I has at least one AC. Unit-testable: J.1–J.3, J.6 (most), J.7 (partial). Integration-testable: J.4–J.5, J.6 (remainder), J.7 (remainder), J.8. Walkthrough/review-gate: J.9.

## Open Questions

Forward-gated decisions this GDD deliberately leaves unresolved. Each OQ documents what is open, what blocks resolution, the provisional answer (where one exists), and the gate/trigger that should force resolution.

### K.1 Playtest/Telemetry-Gated Tuning

- **OQ-NE1 (Per-biome Event density at Biome 3).** Biome 3 currently has 17 Events vs Biomes 1&2 at 20 (Section C distribution tables). Is the step-down correct, or does late-game Event fatigue demand a sharper drop (e.g., 13)?
  - *Blocked by:* ≥1000-run telemetry sample reaching Biome 3.
  - *Provisional:* keep 17 for MVP.
  - *Gate:* first-wave playtest post-implementation.

- **OQ-NE2 (EventConvert favorable rate).** The 3:1 favorable vs 4:1 baseline ratio (Formula D.4) gives a 1.33× multiplier on Convert. Open: does Convert feel worth its outcome, or does it read as "the boring Event"?
  - *Blocked by:* qualitative player-sentiment data from open beta.
  - *Provisional:* 3:1 locked for MVP.
  - *Gate:* open beta → measure Convert commit rate vs Treasure/Windfall/Ambush parity.

- **OQ-NE3 (Windfall Scrap grant curve).** Currently flat 12 (Formula D.5). Should it scale with biome depth (e.g., 10/12/15 across Biomes 1/2/3) to track Scrap-economy inflation?
  - *Blocked by:* Scrap Economy GDD's inflation curve calibration.
  - *Provisional:* flat 12 for MVP.
  - *Gate:* 3+ full-run playtests reaching Biome 3.

- **OQ-NE4 (HostileTiltDelta magnitude).** Current asymmetric tilt Ambush +15 / Windfall −10 / Treasure −5 / Convert 0. Is the tilt strong enough that players *feel* the Frame-state physics — or too aggressive such that Degraded Frame reads as punitive rather than diegetic?
  - *Blocked by:* player-perception telemetry + qualitative feedback.
  - *Provisional:* locked per Section G.2.
  - *Gate:* first playtest wave. Section B's "earned the read" design test fails if players describe it as "punishment."

- **OQ-NE5 (Watchdog timeout defaults).** 30s non-Combat / 10min Combat. Is 30s too aggressive for Chopshop browsing, or 10min too generous for Elite combats?
  - *Blocked by:* session-length telemetry.
  - *Provisional:* keep defaults.
  - *Gate:* first playtest wave.

### K.2 Cross-GDD Retrofits Pending

- **OQ-NE6 (L&R GDD BeaconType axis naming).** L&R's 5-value reward-table lookup key `{Combat, Elite, Boss, Treasure, Event}` must be clarified as a *reward-context axis*, not a structural enum. Pre-existing retrofit queued from session-start structural decision.
  - *Blocked by:* L&R GDD retrofit merge.
  - *Provisional:* NE routes `Boss = EliteCombat + isBoss` and `Treasure = Event + TreasurePayload` per Section C.
  - *Gate:* L&R GDD retrofit lands.

- **OQ-NE7 (Post-Combat Flow GDD subscription contract).** The handler-state observer contract (I.2) requires Post-Combat Flow UI to subscribe to `OnHandlerBegin`/`OnHandlerEnd`. Post-Combat Flow GDD does not yet exist; its subscription is a forward commitment.
  - *Blocked by:* Post-Combat Flow GDD authoring.
  - *Provisional:* NE's event contract is self-contained — subscribers who don't register simply get no events (safe).
  - *Gate:* Post-Combat Flow GDD Section C must explicitly document subscription to `OnHandlerBegin`/`OnHandlerEnd` with the lifecycle rules from I.2.

### K.3 Post-MVP Forward Commitments

- **OQ-NE8 (Modding hooks on `INodeEncounterHandler`).** Should the handler interface be public for community handler registration, or kept internal for MVP?
  - *Blocked by:* mod-support scope decision (not yet made for Wasteland Run).
  - *Provisional:* internal for MVP; architect the interface so future public exposure requires no breaking refactor.
  - *Gate:* pre-EA release mod-support decision.

- **OQ-NE9 (Additional beacon types post-MVP).** Candidates include Story/Lore beacons (narrative encounters) and Scenario beacons (run modifiers). NE's dispatcher architecture must remain open to these.
  - *Blocked by:* narrative system design, post-MVP roadmap.
  - *Provisional:* NE's 7-BeaconType enum is MVP-locked but extensible. Adding a beacon type post-MVP requires: new handler class, registration, per-biome distribution entry, registry constant.
  - *Gate:* post-MVP roadmap.

### K.4 Asset-Production Specifics

- **OQ-NE10 (Haven harmonic source — vocal or string).** H.5 specifies "low sustained vocal-or-string harmonic." The choice affects Haven's emotional register (vocal = humanist/communal; string = melancholic/solitary).
  - *Blocked by:* audio-director asset exploration.
  - *Provisional:* no default — audio-director picks during asset production.
  - *Gate:* first Haven audio asset production pass.

- **OQ-NE11 (Chopshop tungsten work-lamp color temperature).** H.4 specifies "warm but harsh, not cozy" — distinguished from Haven's interior ≥0.6 warm-orange. Exact Kelvin value pending.
  - *Blocked by:* art-director lighting pass.
  - *Provisional:* qualitative spec only. Asset-review gate: does the lamp read as *work lamp* (clinical, functional) or *hearth* (warm, home)? Latter rejects.
  - *Gate:* first Chopshop establishing-visual asset review.

### K.5 Accessibility Follow-up

- **OQ-NE12 (Formal accessibility review of the HostileTiltDelta tell).** Shimmer + sub-200Hz audio + gamepad haptic is the three-channel tell (I.4). KBM-only players without color vision may perceive only the shimmer; KBM-only players with visual impairment may perceive nothing. Is this acceptable for MVP, or does it require formal accessibility-specialist review before ship?
  - *Blocked by:* accessibility-specialist availability.
  - *Provisional:* ship MVP with three-channel tell; document KBM-only gap as known limitation.
  - *Gate:* accessibility review pass before 1.0 release (post-MVP but pre-1.0). Queue as accessibility-review-gate milestone.

**Totals:** 12 OQs across 5 groups. 5 are playtest/telemetry-resolvable, 2 are cross-GDD retrofit-gated, 2 are post-MVP scope, 2 are asset-production details handled during art/audio production, 1 is accessibility-review-gated before 1.0.

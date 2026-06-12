# Node Map System

> **Status**: Approved (CONCERNS revised)
> **Author**: Bertan Berkol + Claude Code agents
> **Last Updated**: 2026-04-21
> **Implements Pillar**: Pillar 5 (Route Reflects Vehicle State) — primary; Pillar 4 (Scarcity with Agency — Fuel as routing pacing); Pillar 2 (Chassis Identity — chassis-differentiated fuel burn)
> **Pacing Verdict**: `design/notes/verdict-sandstorm-chase-pacing.md` (GO WITH CONDITIONS, 2026-04-21)
> **Creative Director Review (CD-GDD-ALIGN)**: CONCERNS 2026-04-21 → REVISED 2026-04-21 (4 fixes applied: Section B forfeit sentence, F.2 Truck compensation contract upgraded to mandatory, AC-NM50 tightened to Reachable-set-size test, AC-NM55 added for Scrap/Fuel data-path isolation)

## Overview

The Node Map System is the run-level navigation layer of Wasteland Run — the data and decision surface that sits between combat encounters. Structurally, it is a seeded procedural graph of nodes traversed forward from the run start toward the Haven endpoint, with branching depth lanes that allow lateral routing rather than strict forward lockstep. The graph is generated from a per-run seed (`System.Random`, deterministic), persisted as part of RunState, and lives as a plain C# model accessed by other systems through read-only queries. Layered on top of the graph are two dynamic pressures: an advancing sandstorm that consumes nodes from the run-start edge (discrete advances tied to player commit events, never real-time), and a Fuel budget that prices each node transition — chassis determines how much Fuel a move costs, so Scout ranges wide while Heavy Truck is forced toward the critical path. From the player's seat the map is an active decision layer every turn: read the storm, read Fuel, read what is reachable, commit. From the system's seat it is the delivery mechanism for three pillars at once — Pillar 5 (vehicle state gates route availability via named subsystem constraints), Pillar 4 (Fuel as the routing domain of the three-resource model), and Pillar 2 (chassis-differentiated fuel burn extends chassis identity past combat into routing). Without it, Wasteland Run collapses into a series of isolated encounters with no between-fight tension, no chassis-dependent horizon, and no entropy chasing the player forward — the run structure disappears.

## Player Fantasy

Before you ever look at the map, the vehicle under you has already narrowed which routes are yours to consider. A Scout and a Heavy Truck given the same seeded node graph do not see the same map. The Scout sees a web — detours, scavenge loops, storm-adjacent shortcuts, paths that fork three nodes ahead. The Truck sees a spine — a narrow column of committed forward nodes where fuel burn has already foreclosed almost everything else. The chassis votes before the player clicks anything. This is the map-layer fusion of Pillar 2 (Chassis Identity) and Pillar 5 (Route Reflects Vehicle State): the question at the map is never *"where should I go?"* — it is *"given what I am driving, what can I still afford?"*

On every commit, the player's job is to study a dying map. Two forward options: a scrap-rich detour at 3 Fuel, a storm-adjacent shortcut at 1 Fuel. The player weighs, trades, commits — and understands that the un-chosen path will be eaten next turn, not because a clock ran out but because their own advance pulled the entropy forward. The verbs of this fantasy are deliberative: *read, weigh, spend, commit, forfeit*. Never race, never outrun. Fuel is the currency that prices these routes and nothing else; a hard route choice never feels like a repair choice, and a tense combat turn never feels like a travel cost. This is Pillar 4's three-domain scarcity made visible at the map layer.

And the map pushes back. When a combat costs the player their long-range Fuel tank, the map re-reads itself — a node that was reachable three turns ago is now *too far for what this car is now*. Not locked, not gated: simply out of reach. The player feels the loss in the map, not the stat sheet. Every combat ripples outward into routing. Every part install changes the horizon. This is Pillar 1 (Vehicle as Character) at the map layer — the vehicle's current state is not an abstracted input to a menu; it is the shape of the road ahead.

The storm is never a timer. It is the wasteland reclaiming the road behind the vehicle — pressure the player can always read but never outrun. When the player commits to a node, the world takes another bite. A node you could have taken two turns ago is gone now — not because you were too slow, but because the road itself has stopped existing behind you. That is the deal.

## Detailed Design

### Core Rules

The Node Map System defines four categories of rules: **graph generation**, **storm model**, **fuel budget**, and **commit resolution**. Each is deterministic, seeded from `RunState.RunSeed` via `System.Random`, and exposes read-only state to other systems through the Node Map's public interface.

#### C1.1 Graph Generation

1. Each run generates a seeded **beacon web** (FTL-style free-placed directed graph) at run start via `System.Random(RunSeed)`. The graph is immutable after generation.
2. The run path consists of **three biomes** between run-start and Haven. Each biome contains **18–22 beacons**.
3. Each biome is laid out on the 16:9 map canvas across **5 vertical strips**. Strips 1–4 contain 4–6 beacons each (vertically jittered, not grid-aligned). Strip 5 contains **1–2 beacons** — the **gate funnel** into the next biome (all routes must pass through this chokepoint).
4. **Minimum beacon separation**: 80px on the canvas. Layout retries generation if the constraint is violated.
5. Each non-Haven beacon has **1–3 forward connections** (target average 2). Connections must fall within a **45° forward cone** from the source beacon. Lateral connections to beacons within the same vertical strip are permitted if they satisfy the forward-cone constraint.
6. **Forward-connection guarantee**: every non-Haven beacon has at least one valid forward path to Haven. The generator retries until satisfied.
7. **Beacon types**: `{Combat, EliteCombat, Merchant, Chopshop, Event, Rest, Haven}`. Per-biome distribution is owned by the **Node Encounter GDD** (Node Map reads the distribution table; does not author it).
8. Haven is always the single terminal beacon at the right edge of Biome 3, Strip 5. No routes extend past Haven.
9. **Combat / EliteCombat `EncounterType` authoring (Card Combat R15 retrofit 2026-04-24).** Each `Combat` and `EliteCombat` beacon carries `EncounterType ∈ {Standard, Ambush}`. Default: `Standard`. `Ambush` is authored per-node at graph-generation time on a small percentage of Combat nodes (target **<15%** across the run, with bias toward Biome 3 for narrative flavor). The value is stored on `BeaconData` and passed through to Node Encounter at commit (C3.5) and from NE to Card Combat unchanged (NE C.2.1 / C.2.2). Node Encounter's Event-Ambush payload (NE C.2.5) is a separate dispatch path and does not set `EncounterType` on the graph — Event beacons do not carry the field. **Graph-gen validator INVARIANT**: any beacon with `isBoss == true` MUST carry `EncounterType.Standard`. Ambush-authored boss nodes are rejected as authoring errors (Pillar 3 bossfight readability contract — a boss that strikes first before the player sees the turn order violates Read-to-Win). Tested by AC-NM45c.

#### C1.2 Storm Model

The storm has **two representations** — one authoritative, one cosmetic:

1. **`StormFrontX` (authoritative scalar)** — a single `float` representing the storm front's X-coordinate on the map canvas. Save-serialized. Drives all gameplay logic. Any beacon whose X-coordinate is less than `StormFrontX` is in the **Consumed** state.
2. **Visual noise band (cosmetic only)** — a seeded Perlin/simplex noise overlay rendered on top of `StormFrontX`, giving the storm irregular tendrils and storm-like visual texture. Seeded from `RunSeed` so replays look identical. **Never read by gameplay logic.** The noise band may visually extend past `StormFrontX`, but beacons are only Consumed when their true X < `StormFrontX`.

**Storm advance trigger**: The system tracks a `CombatCommitCounter` that increments each time a `Combat` or `EliteCombat` encounter's reward screen closes. When the counter reaches `ChassisStormCadence[chassis]`, `StormFrontX` advances by one pace (`StormPaceX`) and the counter resets to 0. `ChassisStormCadence` is chassis-differentiated: Scout 3, Assault 2, Truck 1. No other trigger advances the storm.

Non-combat beacons (Merchant, Chopshop, Event, Rest) do NOT increment the counter and do NOT advance the storm — these are "breath" beats by design (Pillar 2: chassis-differentiated cadence makes Scout feel exploratory and Truck feel chased; Pillar 3 legibility: pace is predictable per-commit-type).

**Initial state**: `StormFrontX = -StormStartOffset` at run start (storm begins behind the run-start beacon by a configurable safety margin).

#### C1.3 Fuel Budget

1. Fuel is a vehicle-level resource, read via `IVehicleView.CurrentFuel` and deducted via `IVehicleMutator.SpendFuel(int)`.
2. Every beacon commit deducts Fuel. Cost formula defined in Section D; conceptually: `FuelCostBase × ChassisMultiplier + LateralSurcharge`.
3. **Chassis multipliers** (owned by V&P chassis stat block — this GDD consumes, does not author): Scout `×0.8`, Assault `×1.0`, Heavy Truck `×1.3`.
4. **Lateral surcharge**: commits to a lateral-adjacent beacon (not a forward connection) add `FuelCostLateralSurcharge`. Reflects the "off the critical path" cost.
5. **Fuel sources**: beacon rewards (Combat, Elite, Event), Merchant purchase, Chopshop conversion. Distribution owned by Node Encounter GDD and Loot & Reward GDD.
6. **Non-leakage contract**: Fuel is never spent or granted inside combat (Pillar 4, Condition 2). Combat damage never modifies Fuel.
7. **Insufficient Fuel**: if a commit would require more Fuel than the vehicle has, the beacon is not selectable (greyed out in UI). If ALL reachable beacons are unaffordable, the run ends (see Edge Cases E-3).

#### C1.4 Commit Resolution Pipeline

When the player commits from Beacon A → Beacon B, the system runs the following pipeline **in order**. The pipeline is atomic: any failure before step 5 rolls back the entire commit.

1. **Validate** — `IsValidCommit(A, B)` predicate must return true (see C2). If false, reject commit; UI does nothing.
2. **Query cost** — compute `fuelCost = ComputeFuelCost(A, B, chassis)`.
3. **Check affordability** — if `IVehicleView.CurrentFuel < fuelCost`, reject commit.
4. **Deduct Fuel** — call `IVehicleMutator.SpendFuel(fuelCost)`. If mutator rejects, halt pipeline.
5. **Transition A → Visited** — A's beacon state becomes `Visited`.
6. **Transition B → Current** — player is now at B.
7. **Hand off to Node Encounter** — B's encounter type resolves (Combat, Merchant, Chopshop, Event, Rest, Haven). Node Map relinquishes control to the Node Encounter GDD's handler during encounter.
8. **On encounter resolve** — Node Encounter returns an outcome. If B is a `Combat` or `EliteCombat` beacon AND the outcome is "reward screen closed":
   1. `CombatCommitCounter += 1`.
   2. If `CombatCommitCounter >= ChassisStormCadence[chassis]`: advance `StormFrontX += StormPaceX` and reset `CombatCommitCounter = 0`.
9. **Consume sweep** — iterate all beacons; any beacon whose X < `StormFrontX` transitions to `Consumed`. If `StormFrontX >= PlayerBeaconX`, trigger **run-end** (storm-hit, see Edge Cases E-1).
10. **Recompute Reachable set** — compute all beacons B' where `IsValidCommit(Current, B') == true`, and flag them `Reachable` for UI rendering.

### States and Transitions

The Node Map System has two concurrent state machines: the per-beacon state machine and the storm state machine. Both are deterministic and save-serialized (except `Reachable`, which is always computed from `Current` + graph + Fuel).

#### C2.1 Beacon State Machine

Each beacon holds one of five states:

| State | Meaning | Player interaction |
|---|---|---|
| `Unvisited` | Default state for beacons not yet reached or computed as reachable | Not selectable; rendered dim |
| `Reachable` | Satisfies `IsValidCommit(Current, Beacon)` | Selectable; rendered highlighted with cost label |
| `Current` | Player is presently at this beacon | Not selectable (cannot commit to self); rendered with "you are here" marker |
| `Visited` | Player has committed through this beacon | Not selectable; rendered faded but intact |
| `Consumed` | Beacon X < `StormFrontX` | Not selectable; rendered as storm-swallowed (visually destroyed) |

**Transition rules** (one row per valid transition; all other transitions forbidden):

| From | To | Trigger |
|---|---|---|
| `Unvisited` | `Reachable` | Commit pipeline step 10 — beacon satisfies `IsValidCommit` after Current moved |
| `Reachable` | `Unvisited` | Commit pipeline step 10 — beacon no longer satisfies `IsValidCommit` after Current moved |
| `Reachable` | `Current` | Commit pipeline step 6 — player committed to this beacon |
| `Current` | `Visited` | Commit pipeline step 5 — player committed away from this beacon |
| `Unvisited` | `Consumed` | Commit pipeline step 9 — beacon X fell behind `StormFrontX` |
| `Reachable` | `Consumed` | Commit pipeline step 9 — beacon X fell behind `StormFrontX` |
| `Visited` | `Consumed` | Commit pipeline step 9 — a Visited beacon can still fall to the storm (purely cosmetic — already traversed) |

**Forbidden transitions**: `Current → Consumed` is prevented by step 9's run-end trigger (storm hitting the player ends the run before the state transition fires). `Consumed → *` is one-way — Consumed beacons never recover.

#### C2.2 Storm State Machine

The storm has three states:

| State | Condition | Behavior |
|---|---|---|
| `Dormant` | `StormFrontX < FirstBeaconX` | Storm is present visually but has not yet consumed any beacon. Still advances on trigger. |
| `Active` | `StormFrontX >= FirstBeaconX` AND `StormFrontX < PlayerBeaconX` | Storm has begun consuming beacons; player is still ahead. |
| `RunEnd` | `StormFrontX >= PlayerBeaconX` | Storm has caught the player. Run ends immediately (see E-1). |

Transitions are monotonic: `Dormant → Active → RunEnd`. No state ever regresses (storm never retreats).

#### C2.3 The `IsValidCommit(A, B)` Predicate

The predicate that gates all commits. Formally:

```
IsValidCommit(A, B) := true if and only if ALL of the following hold:
  1. A == Current beacon
  2. B is not A (no self-commits)
  3. B.State ∈ {Unvisited, Reachable} (cannot commit to Visited, Current, or Consumed)
  4. Edge(A, B) exists in the generated graph
  5. B.X > A.X (forward progress; strictly greater, no backward moves)
        — OR —
     B.X == A.X AND B is in the same vertical strip as A (lateral-adjacent exception)
  6. No active Route Constraint (see C4) blocks the edge (A, B)
  7. Player has sufficient Fuel: IVehicleView.CurrentFuel >= ComputeFuelCost(A, B, chassis)
```

Condition 5 encodes the locked routing rule (**forward + lateral-adjacent only**). Condition 6 is the extension point for B1 Route Constraint Rules, which layer on top of this predicate in C4.

#### C2.4 System Invariants

The following invariants must hold at all times outside the atomic commit pipeline. A save-load cycle, a fresh generation, or any settled state must satisfy all nine:

- **I-1**: Exactly one beacon has state `Current` at any time (except during an atomic commit pipeline mid-flight).
- **I-2**: `Current` beacon X-coordinate is always >= `StormFrontX` (outside the RunEnd state).
- **I-3**: Haven is always reachable via at least one forward path from `Current`, unless the player has become stranded by Fuel starvation (see E-3).
- **I-4**: A beacon in `Consumed` state never transitions back to any other state.
- **I-5**: `StormFrontX` is monotonically non-decreasing over the lifetime of a run. Save-loads preserve the exact value.
- **I-6**: The `Reachable` set is computed, not stored — it is reconstructed from `Current` + graph edges + Fuel on save-load.
- **I-7**: The generated graph is deterministic given `RunSeed` — two runs with the same seed produce byte-identical beacon positions, edges, and types.
- **I-8**: The visual noise band is deterministic given `RunSeed` but NEVER read by gameplay logic (cosmetic-only contract).
- **I-9**: Fuel deduction and beacon state transition are atomic within the commit pipeline — a save cannot be taken mid-pipeline (Save GDD rule: save points are between commits only).

### Interactions with Other Systems

Node Map is a consumer-first system — it reads state from Vehicle & Part, hands off control to Node Encounter, and publishes its own state (graph, current beacon, `StormFrontX`, reachable set) through read-only queries. It mutates exactly one thing outside its own scope: Fuel, via `IVehicleMutator.SpendFuel(int)`.

The table below codifies every cross-system boundary. Each row names the partner system, the direction of the contract (Node Map reads vs. writes), the interface, and who owns which side.

#### C3.1 Interaction Matrix

| Partner System | Direction | Interface | Owner | Purpose |
|---|---|---|---|---|
| **Save & Persistence** | Bidirectional | `INodeMapSerializable` DTO (graph, beacon states, `StormFrontX`, `CurrentBeaconId`, `RunSeed`) | Save GDD owns serializer pattern; Node Map owns DTO shape | Persist and restore run state across save/load |
| **Vehicle & Part** | Read-only | `IVehicleView.CurrentFuel`, `IVehicleView.ChassisId`, `IVehicleView.SubsystemStates[SlotType]` | V&P GDD | Read Fuel for commit affordability; read chassis for Fuel multiplier; read subsystem states for Route Constraints (C4) |
| **Vehicle & Part** | Write-only (single verb) | `IVehicleMutator.SpendFuel(int)` | V&P GDD | Deduct Fuel on commit (step 4 of pipeline) |
| **Node Encounter** | Hand-off | `IEncounterHandler.Begin(Beacon, OutcomeCallback)` | Node Encounter GDD | Transfer control to encounter handler on commit step 7; receive outcome on step 8 |
| **Node Encounter** | Read-only | `IBiomeEncounterTable` (beacon-type distribution) | Node Encounter GDD | Populate beacon types during graph generation |
| **Card Combat** | Read-only | Combat outcome enum `{VictoryRewardClosed, Defeat}` via Node Encounter | Card Combat GDD | Detect "reward screen closed" to trigger storm tick (step 8 of pipeline) |
| **Loot & Reward** | Read-only | `IRewardTable` for Combat / Elite beacons | Loot & Reward GDD | Pass to Node Encounter during encounter resolution; Fuel appears as a reward channel here |
| **Scrap Economy** | Separation contract | None — **explicit non-interaction** | Scrap Economy GDD | Fuel and Scrap are distinct domains (Pillar 4). Scrap Economy GDD must NOT share a pool with Fuel; Fuel acquisition sources must be distinct from Scrap sources. |
| **Game Pillars / Anti-Pillars** | Constraint | Runtime assert: storm never advances during combat | Node Map owns the enforcement | Implements "NOT a real-time map" anti-pillar and CD Condition 3 |

#### C3.2 Save / Load Contract

The Node Map participates in the Save GDD's passive-serializer pattern via a single DTO:

```csharp
[Serializable]
public sealed class NodeMapDto  // implements INodeMapSerializable
{
    public int RunSeed;
    public float StormFrontX;
    public int CombatCommitCounter;
    public string CurrentBeaconId;
    public BeaconStateDto[] Beacons;  // {Id, X, Y, Strip, Type, State, Edges[]}
    public int BiomeIndex;
}
```

Save GDD's serializer calls Node Map's `ToDto()` / `FromDto(dto)` on save/load. The `Reachable` set is NOT serialized — it is recomputed from `CurrentBeaconId` + graph + `IVehicleView.CurrentFuel` on load (per Invariant I-6).

**Save timing constraint**: saves are only valid between commits (Invariant I-9). Node Map exposes `IsCommitInProgress : bool`; Save GDD must check this before invoking `ToDto()`.

#### C3.3 Vehicle & Part Contract

Node Map is a **read-heavy consumer** of V&P. It never touches `CurrentArmor`, `CurrentHullHp`, or any combat-owned state.

- **Fuel read**: `IVehicleView.CurrentFuel` is read every frame the map is open (to recompute `Reachable` / grey-out affordability).
- **Chassis read**: `IVehicleView.ChassisId` is read once per commit (for Fuel multiplier).
- **Subsystem state read**: `IVehicleView.SubsystemStates[SlotType]` is read every time `IsValidCommit` runs (to evaluate C4 Route Constraints).
- **Fuel write**: `IVehicleMutator.SpendFuel(int)` is called exactly once per commit, at step 4. V&P owns the clamp/reject logic; Node Map does not re-check after mutator returns.

**Interface addition required in V&P GDD**: `SpendFuel(int amount)` must exist on `IVehicleMutator` and return a success bool. Confirmed pending in the Dependencies section.

#### C3.4 Combat Non-Interaction Contract

Per CD Condition 3 and the "NOT a real-time map" anti-pillar:

- **Storm does NOT advance during combat.** The storm-advance trigger fires exclusively from Node Encounter's "reward screen closed" callback — never from any combat event.
- **Combat does NOT modify `CurrentFuel`.** Per V&P GDD, combat damage/effects do not touch Fuel.
- **Combat does NOT read `StormFrontX`.** Combat simulation has no knowledge of storm state.

A `#if DEBUG` assert in the storm advance method checks `NodeMap.Phase == NodeMapPhase.OnMap` and throws if advance is called during an active encounter.

#### C3.5 Node Encounter Hand-Off Protocol

Steps 7–8 of the commit pipeline hand control to Node Encounter and receive it back:

1. Node Map calls `INodeEncounterHandler.Begin(beacon, runSeed, frameState, economy, outcomeCallback)`.
2. Node Encounter takes over the UI and loop until the encounter concludes.
3. On conclusion, Node Encounter invokes `outcomeCallback(EncounterOutcome)`.
4. Node Map resumes at step 8 with the returned outcome.

**Commit-time sampling invariant (retrofit 2026-04-23):** The four hand-off arguments beyond `beacon` are captured at the instant of step 7 — the commit moment — and are immutable for the duration of the encounter. Specifically:

- **`beacon.EncounterType`** (Card Combat R15 retrofit 2026-04-24): `Standard` or `Ambush` per C1.1 rule 9. Meaningful only on `Combat` / `EliteCombat` beacons; ignored by other handlers. NE passes this through to `CombatSetup.EncounterType` unchanged at Card Combat dispatch (NE C.2.1 / C.2.2). Node Map does NOT select or mutate the value at hand-off — it was fixed at graph-gen time and lives on the immutable `BeaconData` snapshot.
- **`runSeed`**: the canonical run seed from Run Meta; stable across the whole run and used by NE's per-node RNG derivation `new System.Random(runSeed XOR nodeIndex)` (NE Section D).
- **`frameState`**: a snapshot of `IVehicleView.SubsystemStates[Frame] ∈ {Online, Degraded, Offline}` read at commit time. NE's Event handler uses this snapshot to decide whether `HostileTiltDelta` applies (F-NM5 / NE Section C). **Frame state is NOT re-sampled during the encounter.** If Frame changes mid-encounter (e.g., a status effect degrades it), the tilt does not retroactively shift — the player's commit-moment Frame is what the wasteland judges.
- **`economy`**: an `IScrapEconomy` handle for NE to route facade verbs (TryPurchase, TryConvert*, TryInstall, TryRepair, etc.).
- **`outcomeCallback`**: the resume continuation.

This commit-time semantics is load-bearing for the Frame hidden-tilt fantasy (RC-F1): the player's Frame condition *when they committed to the Event beacon* is what biases the outcome, not their condition after the encounter starts. Save & Persistence's mid-encounter save-block (Save GDD) preserves this invariant across save/load by blocking saves while `HandlerActive` is true — see E-5 and NE Section F.

**Outcome shape** (Node Map consumes; Node Encounter authors):
```csharp
public sealed class EncounterOutcome
{
    public BeaconType BeaconType;           // what kind of encounter ran
    public bool WasCombatRewardClosed;      // true only for Combat/Elite after reward screen close
    public bool DidPlayerSurvive;           // false = defeat (run-end)
}
```

#### C3.6 Haven Terminal Contract

When the player commits to Haven:
1. Pipeline runs normally through step 7.
2. Node Encounter's Haven handler runs (details owned by Node Encounter GDD / future Haven GDD).
3. Step 8 does NOT advance the storm (Haven is not a combat beacon; the run is ending).
4. Step 9's Consume sweep does not apply — the run is won.
5. Node Map publishes a `RunCompleted` event and relinquishes control to the meta layer.

### Route Constraint Rules (B1 — mandatory per TD-SYSTEM-BOUNDARY)

Per the B1 scoping rule, this GDD specifies **one named Route Constraint per subsystem type**. Route Constraints extend the `IsValidCommit` predicate (C2.3 condition 6) and/or modify the cost/risk calculation for affected edges. They are the map-layer expression of Pillar 5 (Route Reflects Vehicle State) — the car's current condition reshapes the available road.

Each constraint is evaluated against `IVehicleView.SubsystemStates[SlotType]` every time the map recomputes the Reachable set or runs `IsValidCommit`. Constraints are additive — multiple can apply simultaneously.

#### C4.1 Constraint Inventory

Four constraints are defined, one per subsystem slot. Each is classified as **Hard** (blocks commits) or **Soft** (modifies cost, risk, or UI signaling without blocking).

##### RC-W1 — Weapon Offline → Elite Warning Overlay (**Soft**)

- **Trigger**: `SubsystemStates[Weapon] == Offline`.
- **Effect**: Any Reachable beacon of type `EliteCombat` displays a red "Weapon Offline — Elite Warning" overlay. Beacon remains selectable.
- **Not a hard block**: The player can still commit to an Elite with no Weapon — this is a deliberate Pillar 3 (Read to Win) moment. The map tells them the risk; the choice is theirs.
- **Rationale**: Weapon offline in combat is already punishing (via Card Combat's damage pipeline). A hard map-block would be double-taxation. A soft overlay preserves agency while making the cost legible.

##### RC-E1 — Engine Offline → Fuel Cost Surcharge (**Soft**)

- **Trigger**: `SubsystemStates[Engine] == Offline`.
- **Effect**: Every commit adds `+1` to the final Fuel cost (applied after chassis multiplier and lateral surcharge).
- **Not a hard block**: Commits still resolve normally provided Fuel is sufficient.
- **UI signal**: Cost label on each Reachable beacon shows `"base + 1 (Engine offline)"` in amber.
- **Rationale**: Engine is the vehicle's routing muscle. Degraded engine → more Fuel per move → the map horizon shrinks. This is Pillar 5 expressed as economic friction, not as lock-out.

##### RC-M1 — Mobility Offline → Lateral Moves Blocked (**Hard**)

- **Trigger**: `SubsystemStates[Mobility] == Offline`.
- **Effect**: `IsValidCommit(A, B)` returns `false` for any edge where `B.X == A.X` (the lateral-adjacent exception from C2.3 condition 5 is disabled). Only strictly-forward edges are selectable.
- **UI signal**: Lateral-adjacent beacons appear Unvisited with a greyed "Mobility Offline" marker. Cost label is hidden.
- **Rationale**: This is the one hard block in the inventory. Lateral routing is the Scout's signature freedom; when Mobility is dead, the car loses that freedom entirely and is forced to the spine. This is Pillar 5 at maximum intensity — the map shrinks visibly.

##### RC-F1 — Frame Degraded → Unknown-Beacon Hostile Tilt (**Soft**)

- **Trigger**: `SubsystemStates[Frame] == Degraded` OR `SubsystemStates[Frame] == Offline`.
- **Effect**: For any `Event` beacon that has not yet been resolved (content unknown to player), the encounter table's random roll tilts toward hostile outcomes by `+15%` weight. Applies only at commit time (not pre-commit) and is invisible to the player.
- **Not a hard block**: No beacon becomes unreachable; event beacons still appear Reachable normally.
- **UI signal**: None — this is a hidden risk tilt. Player fantasy: when your Frame is cracked, the wasteland bites harder at unknown encounters.
- **Rationale**: Frame is the vehicle's baseline integrity. A degraded Frame signals a wounded car; the world responds. This is the only constraint that is deliberately not shown to the player — it is felt through accumulated bad outcomes, not read.

#### C4.2 Constraint Evaluation Order

When multiple constraints apply simultaneously (e.g., Engine offline + Mobility offline after a bad combat), they evaluate in this order:

1. **Hard constraints first** — RC-M1 is checked at condition 6 of `IsValidCommit`. A hard block ends evaluation for that edge.
2. **Soft-cost constraints** — RC-E1 applies to the Fuel cost computation.
3. **Soft-signal constraints** — RC-W1 applies to the UI overlay pass.
4. **Hidden-roll constraints** — RC-F1 applies at encounter-resolution time (not at commit).

#### C4.3 Mapping to Subsystem Slots

| Slot Type | Constraint ID | Class | Affects | Player-visible? |
|---|---|---|---|---|
| `Weapon` | RC-W1 | Soft | Elite beacon UI overlay | Yes (red overlay) |
| `Engine` | RC-E1 | Soft | Fuel cost per commit | Yes (amber cost label) |
| `Mobility` | RC-M1 | Hard | `IsValidCommit` (lateral disabled) | Yes (grey beacon marker) |
| `Frame` | RC-F1 | Soft | Event beacon encounter roll | No (hidden tilt) |

All four slots have exactly one named constraint — B1 rule satisfied.

#### C4.4 Constraints as Tuning Surface

All numeric thresholds (`+1` Fuel surcharge, `+15%` hostile tilt) are tuning knobs and appear in Section G. The qualitative class of each constraint (hard vs. soft, which player fantasy it serves) is load-bearing and should NOT be reclassified without a full Pillar-5 review.

## Formulas

Node Map defines seven numeric formulas. Every tuning knob referenced here is enumerated in Section G with its safe range. Every formula states its variables, valid ranges, and at least one example.

### F-NM1 — ComputeFuelCost

The canonical Fuel cost formula for a commit from beacon A to beacon B.

```
ComputeFuelCost(A, B, chassis, subsystemStates) =
    round( FuelCostBase × ChassisMultiplier[chassis] )
  + ( IsLateral(A, B) ? FuelCostLateralSurcharge : 0 )
  + ( subsystemStates[Engine] == Offline ? EngineOfflineSurcharge : 0 )
```

**Variables**:
| Symbol | Source | Type | Range | Meaning |
|---|---|---|---|---|
| `FuelCostBase` | Tuning knob (G) | int | 1–5 | Base Fuel cost for one commit, pre-multiplier |
| `ChassisMultiplier[chassis]` | V&P GDD | float | 0.5–2.0 | Chassis fuel-burn modifier |
| `IsLateral(A, B)` | Computed | bool | — | `true` iff `B.X == A.X` (lateral-adjacent commit) |
| `FuelCostLateralSurcharge` | Tuning knob (G) | int | 0–3 | Extra Fuel added for lateral moves |
| `subsystemStates[Engine]` | V&P GDD | enum | — | `Offline` / `Degraded` / `Online` |
| `EngineOfflineSurcharge` | Tuning knob (G) | int | 0–2 | Extra Fuel when Engine is Offline (RC-E1) |

**Chassis multipliers** (owned by V&P GDD, reproduced for clarity):

| Chassis | Multiplier |
|---|---|
| Scout | 0.8 |
| Assault | 1.0 |
| Heavy Truck | 1.3 |

**Example 1** — Scout, forward commit, Engine online, `FuelCostBase=2`:
```
cost = round(2 × 0.8) + 0 + 0 = round(1.6) = 2
```

**Example 2** — Heavy Truck, lateral commit, Engine online, `FuelCostBase=2`, `FuelCostLateralSurcharge=1`:
```
cost = round(2 × 1.3) + 1 + 0 = round(2.6) + 1 = 3 + 1 = 4
```

**Example 3** — Assault, forward commit, Engine offline, `FuelCostBase=2`, `EngineOfflineSurcharge=1`:
```
cost = round(2 × 1.0) + 0 + 1 = 2 + 0 + 1 = 3
```

**Output range**: minimum 1 Fuel per commit, maximum 13 Fuel per commit (Heavy Truck + lateral + Engine offline + worst-case knobs). Expected typical range: 2–5 Fuel.

### F-NM2 — StormAdvance

The storm's authoritative advance step, gated by a chassis-differentiated counter.

```
OnCombatRewardClosed():
    CombatCommitCounter += 1
    if CombatCommitCounter >= ChassisStormCadence[chassis]:
        StormFrontX = StormFrontX + StormPaceX
        CombatCommitCounter = 0
```

**Variables**:
| Symbol | Source | Type | Range | Meaning |
|---|---|---|---|---|
| `CombatCommitCounter` | Node Map state | int | `[0, max(ChassisStormCadence)]` | Combats completed since last storm tick |
| `ChassisStormCadence[chassis]` | F-NM7 | int | 1–5 | Combats required to tick the storm once |
| `StormFrontX` | Node Map state | float | `[-StormStartOffset, ∞)` | Current X-coordinate of storm front |
| `StormPaceX` | Tuning knob (G) | float | `0.5 × StripWidth` to `1.5 × StripWidth` | Distance advanced per tick |

**Trigger condition**: called exactly once, at commit pipeline step 8, when the resolving beacon is `Combat` or `EliteCombat` and `WasCombatRewardClosed == true`.

**Example** — Scout chassis (`ChassisStormCadence = 3`), `StormPaceX = 128`:
- Combat #1: counter 0→1, storm stays at `-64`
- Combat #2: counter 1→2, storm stays at `-64`
- Combat #3: counter 2→3 → **tick** → storm advances to `64`, counter resets to 0
- Combat #4: counter 0→1, storm stays at `64`

**Example** — Truck chassis (`ChassisStormCadence = 1`): every combat ticks the storm immediately; counter resets on every tick.

### F-NM3 — IsBeaconConsumed

The predicate that determines whether a beacon has been swallowed by the storm.

```
IsBeaconConsumed(beacon) =
    beacon.X < StormFrontX
```

Strictly less-than. A beacon exactly ON the storm front (`beacon.X == StormFrontX`) is NOT consumed — this provides a one-frame safety margin for the player-at-the-storm case (where the player's beacon sits exactly at the storm front, step 9 triggers run-end by a different predicate — see F-NM4).

### F-NM4 — IsStormCaughtPlayer

The run-end predicate.

```
IsStormCaughtPlayer() =
    StormFrontX >= CurrentBeacon.X
```

Non-strict `>=`. If the storm advances to or past the player's beacon X-coordinate, the run ends immediately (E-1). This is distinct from F-NM3 (strict `<`) because the player-at-the-storm case must be a loss, not a safe edge case.

### F-NM5 — HostileTiltApplication (RC-F1)

When Frame is Degraded or Offline, Event beacon encounter rolls tilt toward hostile outcomes.

**Canonical formula (retrofit 2026-04-23):** Per Node Encounter GDD Section C, `HostileTiltDelta` is an asymmetric 4-axis integer vector applied directly to the Event beacon's four outcome weights (which are integer weights out of 100, not probabilities). Node Map publishes the commit-time Frame state at hand-off (C3.5); NE owns application.

```
ApplyHostileTilt(eventWeights, frameState):
    // eventWeights = {Treasure: 35, Ambush: 20, Windfall: 30, Convert: 15} (base, integer /100)
    if (frameState == Degraded OR frameState == Offline):
        eventWeights.Treasure += HostileTiltDelta.Treasure   // -5  → 30
        eventWeights.Ambush   += HostileTiltDelta.Ambush     // +15 → 35
        eventWeights.Windfall += HostileTiltDelta.Windfall   // -10 → 20
        eventWeights.Convert  += HostileTiltDelta.Convert    //  0  → 15
    // Sum of deltas = 0 → tilted weights still sum to 100; no normalization needed.
    return CdfSample(eventWeights, rng)
```

**Variables**:
| Symbol | Source | Type | Range | Meaning |
|---|---|---|---|---|
| `HostileTiltDelta` | Registry (canonical: `design/gdd/node-encounter.md`) | int4 vector | `{Treasure: -5, Ambush: +15, Windfall: -10, Convert: 0}` | Per-axis additive deltas (integer /100) applied to Event beacon outcome weights when Frame Degraded/Offline |
| `eventWeights` | Registry / NE GDD Section C | int4 | base `{35, 20, 30, 15}` | Per-outcome integer weights out of 100 |
| `frameState` | Node Map → NE hand-off (C3.5) | enum | `{Online, Degraded, Offline}` | Commit-time snapshot of Frame subsystem |

**Example** — Event beacon base weights `{Treasure: 35, Ambush: 20, Windfall: 30, Convert: 15}` (sum 100). With Frame Degraded and the canonical tilt vector applied:
- Tilted: `{Treasure: 30, Ambush: 35, Windfall: 20, Convert: 15}` (sum 100 — no normalization step).
- Ambush probability rises from 20% to 35% (+75% relative); Windfall falls from 30% to 20% (−33% relative); Treasure from 35% to 30%; Convert unchanged.

The dominant shift is Ambush (the hostile-to-player outcome); Windfall and Treasure are the "cost" — the world trades good outcomes for bad when the car is wounded. The legacy scalar shorthand `0.15` referenced elsewhere in this GDD equals the magnitude of the Ambush component in the canonical vector (`+15/100 = 0.15`) and is retained only for tuning-knob discussion in G. Authoritative source is the registry + NE GDD.

### F-NM6 — GraphDensityTarget

The generator's target average forward-connection count per beacon, used during graph generation retries.

```
TargetConnectionCount(beacon) =
    clamp( round( GraphDensityTarget + Random(-0.5, +0.5) ), 1, 3 )
```

**Variables**:
| Symbol | Source | Type | Range | Meaning |
|---|---|---|---|---|
| `GraphDensityTarget` | Tuning knob (G) | float | 1.5–2.5 | Average forward connections per beacon |

Output range: always 1, 2, or 3. Default `GraphDensityTarget = 2.0` yields a distribution heavily weighted toward 2 connections with natural jitter into 1 or 3.

### F-NM7 — ChassisStormCadence

Chassis-differentiated constant governing storm tick rate. Lookup table rather than a formula, but documented here as the authoritative source.

```
ChassisStormCadence[Scout]   = 3
ChassisStormCadence[Assault] = 2
ChassisStormCadence[Truck]   = 1
```

**Meaning**: number of combat encounters required before `StormFrontX` advances by one pace.

**Tuning range**: 1–5. Below 1 is undefined; above 5 makes the storm feel inert and breaks Pillar 5. The Scout/Assault/Truck spread **must preserve ordering** (Scout > Assault > Truck) to preserve the chassis fantasy — if the ordering inverts, this is a Pillar-2 break and needs a pillar review.

**Example playthrough comparison**, Biome 1 with 20 combat beacons fully traversed:
- Scout: ~6.6 storm ticks across the biome
- Assault: ~10 storm ticks across the biome
- Truck: ~20 storm ticks across the biome

This is the cadence difference that makes the chassis fantasy legible at the map layer.

## Edge Cases

Every edge case states the trigger condition and the explicit system response. No hand-waving.

### E-1 — Storm catches player

**Trigger**: `StormFrontX >= CurrentBeacon.X` after commit pipeline step 8 executes `StormAdvance` (F-NM2).
**Response**: Run ends immediately with `RunEndReason = StormCaughtPlayer`. The commit pipeline halts at step 9 (Consume sweep triggers run-end); step 10 (Recompute Reachable) is skipped. Node Map publishes `RunEnded(StormCaught)` event. Transition to defeat/run-summary scene.
**Explicit state**: `CurrentBeacon.State` does NOT transition to `Consumed` — it remains `Current` in the final saved-out state. The run-summary scene renders the beacon-at-storm-front as the death point.
**Preventable?**: Yes — player had full legibility of `StormFrontX` and `CombatCommitCounter` before every commit.

### E-2 — Player defeated in combat

**Trigger**: Node Encounter returns `EncounterOutcome.DidPlayerSurvive == false`.
**Response**: Run ends immediately. Commit pipeline skips steps 8, 9, 10 entirely — `CombatCommitCounter` does NOT increment, `StormFrontX` does NOT advance. `RunEndReason = CombatDefeat`. Node Map publishes `RunEnded(Defeat)` event.
**Interaction with E-1**: Combat defeat takes priority over storm advance. If a combat ends in defeat, the storm never ticks for that fight.

### E-3 — Fuel starvation (all Reachable beacons unaffordable)

**Trigger**: At step 10 of the commit pipeline, the Reachable set is empty because every candidate beacon's Fuel cost exceeds `IVehicleView.CurrentFuel`. Note: a beacon is still in the Reachable set if the *only* reason it's unreachable is Fuel — it's flagged `Reachable` but rendered greyed out. Starvation is when even those greyed beacons are the only options AND the player has no way to acquire Fuel.
**Response**: Run-end with `RunEndReason = FuelStarvation`. Pity-refills violate Pillar 4 (Scarcity with Agency) — the player's Fuel spending was their agency; granting free Fuel on failure erases the consequence. Flagged as OQ-NM1 if playtest data shows starvation is a frequent run-ender (>5% of runs) — then revisit.

### E-4 — Graph generation failure

**Trigger**: The generator cannot produce a valid graph satisfying all constraints (forward-connection guarantee, 80px minimum separation, strip-5 gate funnel, 1–3 connections per beacon) after 100 retries.
**Response**: Log `GraphGenerationFailed(seed)` to `production/session-logs/`, then fall back to a **curated deterministic baseline graph** (one per biome, authored by hand, shipped with the build). Run proceeds with the fallback. This path should never fire in practice; if it does more than 0.1% of runs, escalate as a constraint-calibration bug.
**Why curated fallback over retry-with-new-seed**: Determinism. The player's seed must still produce a reproducible run — switching seeds silently breaks seeded-run replay. The curated fallback is itself deterministic and chosen per-biome.

### E-5 — Save requested mid-pipeline

**Trigger**: Save GDD attempts `ToDto()` during steps 1–10 of the commit pipeline. `IsCommitInProgress == true`.
**Response**: Save GDD's serializer is contractually required to check `IsCommitInProgress` and defer the save until the pipeline completes. If Save GDD calls `ToDto()` anyway (contract violation), Node Map throws `InvalidOperationException("Cannot serialize NodeMap mid-commit")`. Invariant I-9 is load-bearing.
**Explicit state**: No partial-commit save ever reaches disk.

### E-6 — Chassis swap mid-run

**Trigger**: A mechanic (mid-run garage, chassis-swap card, whatever) changes `IVehicleView.ChassisId` after the run has started.
**Response**: **NOT permitted by this GDD.** Chassis is fixed at run start. If V&P or any other system proposes a chassis-swap mechanic, it is a **contract break** with Node Map — `ChassisStormCadence` and `ChassisMultiplier` lookups assume the chassis is immutable for the run's duration. Flag as a dependency constraint in Section F.
**If chassis swap is later added**: `CombatCommitCounter` must reset to 0 at swap time (so the incoming chassis gets its full cadence, not an inherited partial counter). Re-designing this is an OQ, not an in-scope edge case.

### E-7 — Mobility offline and only lateral-reachable beacons

**Trigger**: RC-M1 (Mobility offline → lateral blocked) is active, AND the `Current` beacon's forward edges all lead to beacons that are either `Consumed` or unaffordable. The only theoretically-adjacent beacons are lateral, which RC-M1 blocks.
**Response**: Functionally equivalent to E-3 (Fuel starvation / stranded). Run-end with `RunEndReason = Stranded`. Player is warned by UI before committing to a Mobility-offline trajectory (cost label hidden on lateral beacons, clearly flagged "Mobility Offline").
**Design intent**: This is the sharp edge of Pillar 5. Mobility dying on a bad turn can end the run. That's the point — the vehicle's state genuinely gates routing. Mitigation is Pillar 3 (the player could see it coming).

### E-8 — Consumed-beacon reappearance bug

**Trigger**: A beacon transitions `Consumed → Unvisited` or any non-Consumed state. This should never happen — Invariant I-4 forbids it.
**Response**: `#if DEBUG` assert-throws. In release builds, log a `Severity.Critical` error to `production/session-logs/` and force the beacon back to `Consumed`. Run continues but is flagged `runIntegrityCompromised = true` for the run-summary screen so the player can report a bug.
**Why not silent recovery**: Silent state corruption is a seeded-run replay breaker.

### E-9 — Visual noise band overlaps a Reachable beacon

**Trigger**: The cosmetic storm noise overlay (not `StormFrontX`) visually covers a beacon whose X > `StormFrontX`. The beacon is gameplay-Reachable but visually obscured.
**Response**: UI rendering MUST layer beacon markers above the noise band. If the noise band renders over a Reachable beacon, the beacon's visual is not occluded — the noise band has `z-index < beaconMarker.z-index`. Cost labels and state indicators also render above the band.
**Why**: Pillar 3 legibility. If the noise band ever hides a Reachable beacon, the player loses information they are owed.

### E-10 — Simultaneous RC-E1 and RC-M1 (Engine + Mobility both offline)

**Trigger**: Both subsystems go offline in the same combat.
**Response**: Both constraints apply (additive, per C4.2 order). Lateral commits are blocked (RC-M1 hard); forward commits cost `+1` Fuel (RC-E1 soft). Player may be stranded if the remaining forward commits cannot be afforded (collapses to E-3).
**Not a double-death**: The two constraints compose by design — this is Pillar 5 at peak intensity. If playtest data shows dual-offline triggers a stranded state in >10% of affected runs, flag as a balance issue (OQ-NM2), but don't weaken the constraints individually.

### E-11 — Haven reached with storm close behind

**Trigger**: Player commits to Haven. `StormFrontX` is within one pace of `CurrentBeacon.X` (storm was about to catch them).
**Response**: Haven victory still triggers (per C3.6). Storm does not advance on Haven commit (step 8 skipped for non-combat beacon). The "photo finish" is purely cosmetic — the player wins.
**Design intent**: Last-second-escape is a player fantasy worth preserving. Don't grief the close finish.

### E-12 — `CombatCommitCounter` corruption at save-load

**Trigger**: Save file has inconsistent state: `CombatCommitCounter > ChassisStormCadence[chassis]`.
**Response**: On load, clamp `CombatCommitCounter = min(CombatCommitCounter, ChassisStormCadence[chassis] - 1)`. Log a warning. The next combat will still correctly trigger the storm tick (because the next increment reaches cadence).
**Why clamp on load, not throw**: Saves can become corrupted (hand edit, crash mid-write). Prefer graceful recovery over rejected saves; log so it's visible in telemetry.

### E-13 — All beacons in the current biome Consumed, but gate-funnel beacon unreached

**Trigger**: Storm has advanced past the entire body of the biome, but the single Strip-5 gate-funnel beacon is still ahead of the storm. Player is stranded between storm and gate with all side-beacons consumed.
**Response**: If `Current` beacon still has a valid forward connection to the gate funnel, nothing special — player commits as normal. If not, player is stranded (collapses to E-3).
**Why flagged**: The generator guarantee (every non-Haven beacon has a forward path to Haven) ensures this is resolvable unless Mobility-offline blocks lateral moves AND the remaining forward path requires a lateral. This is the worst-case composition of E-7 + E-10 + storm pressure.

## Dependencies

Bidirectional listing of every system Node Map depends on or that depends on Node Map. Each row specifies direction, the interface, and any retrofit required in the other GDD. Dependencies flagged **RETROFIT** must be propagated before Node Map can be marked Implementation-Ready.

### F.1 Upstream Dependencies (Node Map reads/calls these)

#### Vehicle & Part (CRITICAL)

**What Node Map needs**:
- `IVehicleView.CurrentFuel` (int, read every frame map is open)
- `IVehicleView.ChassisId` (enum, read per commit)
- `IVehicleView.SubsystemStates[SlotType]` (read per `IsValidCommit` call)
- `IVehicleMutator.SpendFuel(int amount) : bool` (called once per commit)
- Chassis stat block defining `ChassisMultiplier` (Scout 0.8, Assault 1.0, Truck 1.3) as per F-NM1
- **Contract**: Chassis is immutable for the run's duration (see E-6)

**RETROFIT required in V&P GDD**:
- Add `SpendFuel(int)` verb to `IVehicleMutator` (currently only `ApplyDamage` and `AddArmor` exist per Slice-5c unity refactor)
- Expose `SubsystemStates[SlotType]` map on `IVehicleView` (Weapon/Engine/Mobility/Frame → Online/Degraded/Offline)
- Document chassis-immutability contract explicitly in V&P GDD dependencies section
- Add fuel-burn multiplier to chassis stat block (Scout 0.8, Assault 1.0, Truck 1.3)
- Add `CurrentFuel` to `Vehicle` POCO and DTO

#### Save & Persistence (BIDIRECTIONAL)

**What Node Map needs**:
- Registration as an `IRunStateSerializable` participant
- Serializer calls `ToDto()` / `FromDto(dto)` on save/load via the passive serializer pattern
- Respect for `IsCommitInProgress` contract (E-5)

**What Save needs from Node Map**:
- `NodeMapDto` shape (defined in C3.2): `{RunSeed, StormFrontX, CombatCommitCounter, CurrentBeaconId, Beacons[], BiomeIndex}`
- `IsCommitInProgress : bool` property

**RETROFIT required in Save GDD**:
- Add `INodeMapSerializable` (or generic `INodeMapStateProvider`) to the serializer registry
- Document the `IsCommitInProgress` pre-save check as part of the serializer contract

#### Game Pillars (CONSTRAINT)

**What Node Map needs**:
- Pillar 4 three-domain rewrite (already applied 2026-04-21)
- "NOT a real-time map" anti-pillar (already applied 2026-04-21)

**No retrofit** — both already applied before this GDD was authored.

### F.2 Downstream Dependencies (other systems consume Node Map)

#### Node Encounter (CRITICAL — not yet designed)

**What Node Encounter needs from Node Map**:
- Hand-off call `IEncounterHandler.Begin(Beacon, outcomeCallback)` at commit pipeline step 7
- Access to `Beacon.Type`, `Beacon.BiomeIndex` for encounter selection
- **`beacon.EncounterType` ∈ {Standard, Ambush}** — authored per-node at graph-gen time per C1.1 rule 9 (Card Combat R15). NE passes the value through to `CombatSetup.EncounterType` unchanged at Card Combat dispatch (NE C.2.1 / C.2.2); neither NE nor Node Map re-selects or overrides it.

**What Node Map needs from Node Encounter**:
- `IEncounterHandler` implementation per beacon type
- `IBiomeEncounterTable` (per-biome type distribution for graph generation)
- `EncounterOutcome` return shape: `{BeaconType, WasCombatRewardClosed, DidPlayerSurvive}`

**RETROFIT status**: Node Encounter GDD **designed and approved 2026-04-23**. Card Combat R15 `EncounterType` authoring contract **closed 2026-04-24**: Node Map owns per-node `EncounterType` authoring + the boss-node validator (AC-NM45c); NE owns dispatch pass-through (NE AC-NE27a / AC-NE27b); Card Combat owns turn-order and starting-position semantics (Card Combat R15 / S2 / F-CC5). Hand-off interface is `INodeEncounterHandler.Begin(beacon, runSeed, frameState, economy, outcomeCallback)` per C3.5.

#### Loot & Reward (DOWNSTREAM — not yet designed)

**What Loot & Reward needs from Node Map**:
- Beacon type + biome index for reward-table selection
- `ChassisId` passthrough for Truck-path value compensation (per CD Condition 4)

**What Node Map needs from Loot & Reward**:
- Reward tables that include `Fuel` as a reward channel (critical — Fuel must be acquirable from beacons per C1.3)
- **Truck-path value compensation contract (MANDATORY — CD Condition 4)**: Loot & Reward MUST honor a minimum reward-value tilt for Truck-chassis runs so that Truck's narrower route picture is *focused*, not *taxed*. Floor contract: **Combat and Elite beacon reward tables grant a `TruckRewardMultiplier` value uplift of at least 1.25× (Scrap and Fuel channels) when `ChassisId == Truck`**, with the exact multiplier owned by Loot & Reward tuning but never below this floor. Failure to honor this makes Truck ship focused-in-spec and punished-in-play. Tested by AC-NM54.

**RETROFIT status**: Loot & Reward GDD is **undesigned**. The Truck compensation contract above is a hard-required interface, not a soft expectation. The Loot & Reward GDD, when authored, MUST expose `TruckRewardMultiplier >= 1.25` and apply it on Combat/Elite beacon tables. Provisional value; Loot & Reward owns final tuning.

#### Scrap Economy (SEPARATION CONTRACT — not yet designed)

**Explicit non-interaction**: Fuel and Scrap are **distinct resource domains** per Pillar 4. Node Map never touches Scrap; Scrap Economy never touches Fuel.

**What Scrap Economy must honor**:
- Fuel acquisition sources must be distinct from Scrap sources (no shared loot table slot)
- Scrap spent ≠ Fuel earned; no conversion formula allowed without a Pillar-4 review
- Merchant / Chopshop may offer Fuel-for-Scrap trades, but this is a *conversion at a cost*, not a shared pool

**RETROFIT status**: Scrap Economy GDD is **undesigned**. Flag this non-interaction contract as a Pillar-4 load-bearing constraint when that GDD is authored.

#### Run Meta / Haven (DOWNSTREAM — future)

**What the meta layer needs from Node Map**:
- `RunCompleted` event on Haven commit
- `RunEnded(RunEndReason)` event on all defeat paths (StormCaughtPlayer, CombatDefeat, FuelStarvation, Stranded)

**No retrofit** — Haven GDD does not exist yet. Flag as future dependency.

### F.3 Non-Interaction Contracts (systems that must NOT interact)

#### Card Combat (BLOCKED INTERACTION)

Per CD Condition 3 and the "NOT a real-time map" anti-pillar:

- **Card Combat must NOT advance `StormFrontX`** — no combat event, card effect, status tick, or damage pipeline step may touch storm state.
- **Card Combat must NOT modify `CurrentFuel`** — no card effect, loot, or on-combat-end hook may add or deduct Fuel.
- **Card Combat must NOT read `StormFrontX`** — combat simulation has no knowledge of map state.

**Enforcement**: `#if DEBUG` assert in Node Map's storm-advance method that `Phase == OnMap`. Stops any combat-layer leak loudly in development.

**RETROFIT required in Card Combat GDD**: Add a "Non-Interaction with Node Map" row to the V&P Interactions table — specifically note that `DamagePipeline.Apply` never touches Fuel, and that no status effect (Burning, Shield, Corrode, etc.) interacts with `StormFrontX`. This is easy because it's a **statement of what NOT to do**, but it needs to be explicit so a future card designer doesn't accidentally author a "burn fuel on damage" card.

#### Status Effects (INDIRECT)

Status Effects do not interact with Node Map directly. However, status effects that transition subsystem states (e.g., a hypothetical "Disable Engine" status) DO affect Node Map indirectly via RC constraints (C4). This is permitted — it's the same contract the subsystem state machine already carries.

**No retrofit** — existing Status Effects GDD already treats subsystem state as a first-class effect target.

### F.4 Summary Table

| System | Direction | Priority | Retrofit Required? |
|---|---|---|---|
| Vehicle & Part | Upstream | CRITICAL | Yes — `SpendFuel`, `SubsystemStates`, `CurrentFuel`, chassis fuel multiplier |
| Save & Persistence | Bidirectional | HIGH | Yes — register `INodeMapSerializable`, `IsCommitInProgress` check |
| Game Pillars | Constraint | HIGH | Done (Pillar 4 + anti-pillar applied) |
| Node Encounter | Downstream | CRITICAL | Designed 2026-04-23; `EncounterType` authoring + boss-node validator owned here (Card Combat R15 closure 2026-04-24) |
| Loot & Reward | Downstream | HIGH (future) | Honor Truck value compensation (CD Condition 4) |
| Scrap Economy | Separation | HIGH (future) | Honor Fuel/Scrap domain separation |
| Run Meta / Haven | Downstream | FUTURE | Honor `RunCompleted` / `RunEnded` events |
| Card Combat | Non-interaction | CRITICAL | Yes — add "never touches Fuel / storm" statement |
| Status Effects | Indirect | LOW | None |

## Tuning Knobs

Every knob below is a runtime-configurable value (ScriptableObject or data table per CLAUDE.md "no hardcoded gameplay values"). Each row specifies a safe range, the default, the gameplay aspect it controls, and which pillar or formula it feeds. Knobs outside their safe range are not bugs in the system — they are **design statements** that require a pillar review.

### G.1 Fuel Economy Knobs

| Knob | Default | Safe Range | Affects | Pillar / Formula |
|---|---|---|---|---|
| `FuelCostBase` | 2 | 1–5 | Base Fuel cost per commit | Pillar 4 (routing pacing intensity) / F-NM1 |
| `FuelCostLateralSurcharge` | 1 | 0–3 | Extra Fuel for lateral-adjacent commits | Pillar 5 (critical-path tension) / F-NM1 |
| `EngineOfflineSurcharge` | 1 | 0–2 | Fuel penalty when Engine is Offline (RC-E1) | Pillar 5 / F-NM1 / RC-E1 |

**Notes**:
- Increasing `FuelCostBase` compresses Scout's horizon and tightens Truck's spine. A value above 3 makes even Scout struggle; below 1 makes Fuel feel free (Pillar 4 break).
- `FuelCostLateralSurcharge = 0` erases the "critical-path-vs-detour" design tension — lateral becomes identical to forward.
- `EngineOfflineSurcharge` above 2 stacks with `FuelCostLateralSurcharge` and `Truck ×1.3` to produce commit costs that can't be afforded — verify against E-3 starvation rates before going high.

### G.2 Storm Knobs

| Knob | Default | Safe Range | Affects | Pillar / Formula |
|---|---|---|---|---|
| `StormPaceX` | 1 strip width (~128px on 1920-wide canvas) | 0.5×strip to 1.5×strip | Distance storm advances per tick | Pillar 5 / F-NM2 |
| `StormStartOffset` | 0.5 strip width | 0 to 2 strip widths | Initial safety margin at run start | Pillar 3 (grace period) |
| `ChassisStormCadence[Scout]` | 3 | 2–5 | Combats before storm ticks (Scout) | Pillar 2 / F-NM7 |
| `ChassisStormCadence[Assault]` | 2 | 1–4 | Combats before storm ticks (Assault) | Pillar 2 / F-NM7 |
| `ChassisStormCadence[Truck]` | 1 | 1–2 | Combats before storm ticks (Truck) | Pillar 2 / F-NM7 |

**Notes**:
- `StormPaceX` above 1.5 strip widths means the storm can skip a whole strip of beacons on a single tick — legibility becomes brittle. Below 0.5 means the storm advances imperceptibly.
- `StormStartOffset = 0` means the storm is already touching the run-start beacon at t=0 — a harsh opener. Above 2 strip widths gives the player a free "first biome grace" that dulls the pillar.
- **Ordering constraint on `ChassisStormCadence`**: Scout > Assault > Truck **must be preserved**. Equal values across chassis is a Pillar-2 break. Inverted values (Truck > Scout) would flip the chassis fantasy entirely — this is allowed only as a deliberate "thematic rework" with creative-director sign-off.

### G.3 Risk-Tilt Knobs

| Knob | Default | Safe Range | Affects | Pillar / Formula |
|---|---|---|---|---|
| `HostileTiltDelta` (canonical) | `{Treasure: -5, Ambush: +15, Windfall: -10, Convert: 0}` (int /100 vector) | Per-axis ±20 (keep sum-of-deltas = 0) | Per-outcome additive weight deltas on Event beacons when Frame Degraded/Offline | Pillar 5 / F-NM5 / RC-F1 |
| `HostileTiltDelta` (legacy scalar) | 0.15 | 0.0–0.30 | Shorthand for Ambush component magnitude only — retained for narrative; **authoritative source: `design/registry/entities.yaml` / `design/gdd/node-encounter.md`** | — |

**Notes**:
- Below `0.05` the effect is imperceptible; the constraint loses its teeth.
- Above `0.30` a Degraded Frame turns almost every Event into hostile — feels like a death spiral rather than a risk tilt.
- This knob is the only one that tunes a hidden effect (RC-F1 is invisible to the player). Verify via playtest, not UI inspection.

### G.4 Graph Generation Knobs

| Knob | Default | Safe Range | Affects | Pillar / Formula |
|---|---|---|---|---|
| `GraphDensityTarget` | 2.0 | 1.5–2.5 | Average forward connections per beacon | Pillar 3 (legibility) / F-NM6 |
| `BeaconCountPerBiome` | 20 | 18–22 | Total beacons in one biome | Pacing / C1.1 |

**Notes**:
- `GraphDensityTarget = 1.5` makes the graph feel sparse — few decision points per beacon, more forced routing. `GraphDensityTarget = 2.5` makes every beacon a 3-way fork, risking paralysis.
- `BeaconCountPerBiome` below 18 makes a biome feel thin (Scout can trivially skip half of it). Above 22 breaks the 5-strip layout (too many beacons per strip violate the 80px separation constraint).

### G.5 Structural Constants (NOT tuning knobs — changing these breaks the design)

These are listed for completeness so they are not mistaken for tuning knobs. Changes to these require a full architecture review, not a balance pass.

| Constant | Value | Why it's not a knob |
|---|---|---|
| `BiomeCount` | 3 | Pacing-locked run structure. Different count = different GDD. |
| `StripsPerBiome` | 5 | Spatial layout architecture (per level-designer). |
| `MinBeaconSeparation` | 80px | Visual/UI legibility constraint, not a balance lever. |
| `ForwardConeAngle` | 45° | Graph-generator constraint; changes would alter map feel categorically. |
| `MaxConnectionsPerBeacon` | 3 | Input legibility cap — four forks at one node overwhelms Pillar 3. |
| `MaxGenerationRetries` | 100 | Implementation-detail fallback; should never fire in practice (E-4). |

### G.6 Knobs NOT Listed in This Section

Two classes of values referenced by Node Map are tuned elsewhere and must NOT be duplicated here:

- **`ChassisMultiplier`** (Scout 0.8, Assault 1.0, Truck 1.3) — owned by V&P GDD chassis stat block. Referenced by F-NM1. Node Map consumes the authoritative V&P value; do not redefine here.
- **Beacon type distributions** (e.g., "60% Combat, 15% Elite, 10% Merchant, …") — owned by Node Encounter GDD per-biome. Node Map reads these at generation time; does not author them.

## Visual/Audio Requirements

This is a player-facing system. Visual and audio are load-bearing for Pillars 3 (Read to Win) and 5 (Route Reflects Vehicle State). This section defines **requirements**, not asset specs — detailed asset lists live in `design/art/` and `design/audio/` once authored.

### H.1 Visual Requirements

#### H.1.1 Map Canvas

- **Aspect**: 16:9 canvas. Targets PC display (per technical-preferences.md).
- **Left edge**: run-start origin. **Right edge**: Haven (terminal beacon). Movement is rightward only (consistent with existing `map_layout` memory).
- **Parallax / texture**: wasteland backdrop reading from left (denser, more "alive") to right (sparser, distant haven glow). The storm occupies the left edge and advances into the canvas.
- **Camera**: static framing; no pan/zoom at commit-time. The canvas shows the whole biome at all times (Pillar 3 — all options legible before commit).

#### H.1.2 Beacon Visuals

Each of the seven beacon types must have a **distinct silhouette** readable at map-zoom distance without text. Beacon type silhouettes are the primary information channel — color is a secondary channel for state.

| Beacon Type | Silhouette | Color signature |
|---|---|---|
| `Combat` | Standard circle with crosshair glyph | Neutral (vehicle-color accent) |
| `EliteCombat` | Circle with spiked crown glyph | Red (threat accent) |
| `Merchant` | Circle with coin/crate glyph | Gold accent |
| `Chopshop` | Circle with wrench glyph | Steel/grey accent |
| `Event` | Circle with question-mark glyph | Purple/mystery accent |
| `Rest` | Circle with campfire glyph | Warm orange |
| `Haven` | Larger hexagon, distinct shape | Green/gold (endpoint glow) |

**Ambush overlay (Card Combat R15 Pillar 3 retrofit 2026-04-24).** `Combat` and `EliteCombat` beacons authored with `EncounterType.Ambush` (C1.1 rule 9) render a distinct ambush indicator atop the standard silhouette — an angular chevron / strike-mark shape in a warning tint, anchored at the upper-right of the silhouette circle. The overlay is a **silhouette-level channel, not color-only**, so it satisfies the colorblind-safe palette rule in I.5 without requiring color distinction. Standard-encounter beacons render the unadorned silhouette. The overlay is visible in all beacon states where the silhouette is visible (`Unvisited` dim, `Reachable` full, `Visited` faded) and absorbs into the storm-swallowed treatment at `Consumed` per H.1.3 / H.1.4. The overlay must read at map-zoom distance without text — the pre-commit readability contract requires the player to see the encounter type before they commit Fuel, not after the combat loads.

#### H.1.3 Beacon State Rendering

| State | Visual treatment |
|---|---|
| `Unvisited` | Dim silhouette at ~40% opacity; no cost label; not pulsing |
| `Reachable` | Full silhouette at 100% opacity; cost label visible; subtle pulse; hoverable |
| `Current` | Full silhouette + "you are here" marker (outline ring); **no pulse** (pulse reserved for selectables) |
| `Visited` | Faded silhouette at ~60% opacity; no cost label; no pulse; trail line to next Visited |
| `Consumed` | Storm-swallowed (see H.1.4); visually destroyed/overlapped by noise band |

#### H.1.4 Storm Visuals

**Two layers**, matching C1.2:

1. **Authoritative front line** (`StormFrontX`): a thin, hard vertical line or sharp gradient. **Not ornate** — this is the gameplay ground truth and must be unambiguously readable. Color: harsh orange-red or a wasteland-sand red.
2. **Cosmetic noise band**: Perlin/simplex noise tendrils rendered behind the front line, extending leftward into consumed territory. **May render right of the front line only for visual tendrils** — but per E-9, beacon markers MUST layer above this band. Color: darker tan/brown dust tones.

**Storm advance animation**: when `StormFrontX` advances (F-NM2), the front line slides rightward over `~0.4–0.6 seconds` with a kicked-up-dust effect at the front. Timing is tunable but must be slow enough to be READ as an event, not a jump cut.

**Consumed beacons**: at the moment of Consume (step 9), the beacon is visually swallowed — dust/particles engulf the silhouette, then it fades to <15% opacity and tints sandstorm-red. The geometry persists so the player can still see *where* the consumed beacon was (post-mortem legibility), but it is clearly gone.

#### H.1.5 Edges (connections)

- **Forward edges**: solid lines from source beacon to target, in vehicle-faction color.
- **Lateral edges**: dashed lines (per existing `map_layout` memory: "dashed connections"), slightly dimmer to visually distinguish them from forward commits.
- **Edges leading to Consumed beacons**: fade to ~20% opacity but don't disappear entirely (preserves map memory).
- **Edges blocked by RC-M1 (Mobility offline)**: lateral dashed lines render in grey with a clear "blocked" cross-hatch overlay.

#### H.1.6 Route-Constraint Visual Overlays

Per C4, each constraint has a visual signal:

- **RC-W1 (Weapon offline)**: red "⚠ WEAPON OFFLINE" banner over Elite beacons. Must be readable without hover.
- **RC-E1 (Engine offline)**: cost label format `"3 [base 2 + 1 engine]"` in amber; include a small engine-offline glyph next to the cost.
- **RC-M1 (Mobility offline)**: lateral beacons render as described in H.1.5; additionally, a persistent "MOBILITY OFFLINE — LATERAL LOCKED" banner at the bottom of the map HUD while active.
- **RC-F1 (Frame degraded)**: **NO visual signal** (hidden effect per C4.1). Do not accidentally expose it via animation or hint text.

#### H.1.7 Storm Cadence UI (CombatCommitCounter)

Required UI element per Pillar 3. Displays:

- Current chassis's `ChassisStormCadence` (e.g., "3")
- Current `CombatCommitCounter` value (e.g., "2/3")
- Placement: near the storm front or in a persistent corner of the map HUD
- Visual: an ammo-style pip indicator preferred over text-only (faster read)
- Tick animation: when a combat resolves and the counter fills a pip, the pip lights up. When the last pip fills, the full counter flashes, then drains while the storm advance animation plays.

#### H.1.8 Run-End Visuals

- **E-1 Storm caught player**: storm front overtakes the player beacon; the beacon silhouette is engulfed in dust/particles; screen slowly fades to sandstorm-red; transition to run-summary.
- **E-2 Combat defeat**: owned by Card Combat defeat visuals (not this GDD).
- **E-3 Fuel starvation**: engine sputter VFX on player beacon; vehicle silhouette stalls; transition to run-summary with "OUT OF FUEL" flavor.
- **E-7 Stranded (Mobility offline)**: similar to E-3 but with "NOWHERE TO GO" flavor.

#### H.1.9 Biome Transition (Gate Funnel)

When the player commits from the Strip-5 gate beacon of biome N into biome N+1:
- Brief (~1–2 second) biome-swap visual — environment re-tints/re-textures
- Map canvas scrolls/transitions to the new biome layout
- Biome name card ("BIOME 2: [NAME]") displayed as a non-interactive overlay for ~2 seconds

#### H.1.10 Haven Arrival

- Haven beacon has a perceptible glow/halo at all distances (beacon is visible from anywhere in Biome 3).
- On commit to Haven, the screen transitions from "map view" to a Haven cinematic/arrival handled by Run Meta GDD (not this GDD). Node Map's responsibility ends at the commit.

### H.2 Audio Requirements

#### H.2.1 Ambient

- **Storm ambient loop**: low wind + distant rumble + occasional dust-gust transients. Continuous while map is open. Volume scales with proximity (`1 - (StormFrontX_distance_to_PlayerBeacon / canvas_width)`) — louder when the storm is closer.
- **Biome ambient**: per-biome distinct music bed (desert, irradiated, dust-choked, etc. — authoritative list owned by Audio Director). Underlies the storm ambient.

#### H.2.2 Storm Events

- **Storm tick (authoritative advance, F-NM2)**: short, unmistakable "whoomph" stinger — must cut through the ambient so the player feels the event even without looking at the counter. Reserved specifically for the storm-advance moment.
- **Storm approaches player (warning)**: when `StormFrontX` is within 1 pace of player's beacon, an ominous low drone fades in and persists until the player commits (driving the "flee" urgency).
- **Consume SFX**: a sharp "beacon swallowed" sound that plays once per Consumed beacon during the sweep. If multiple beacons consume in one tick, cluster into a single spatial sweep.

#### H.2.3 Commit Audio

- **Beacon hover**: subtle UI tick per beacon type (Combat = metallic; Merchant = coin-jingle; Chopshop = wrench-clink; etc.). Used for blind-navigation legibility as much as polish.
- **Beacon commit**: a "engine-starts / vehicle moves" transition stinger. Single sound, universal across beacon types — the beacon type's specific sound plays on entry, not on commit.
- **Commit rejected (insufficient Fuel, blocked by RC-M1, etc.)**: soft dull thud + UI deny sound.

#### H.2.4 Route-Constraint Audio Signals

- **RC-W1 (Weapon offline hovering Elite)**: a brief, heart-sinking low chord when the player hovers an Elite beacon while Weapon is Offline.
- **RC-E1 (Engine offline cost shown)**: no per-hover stinger — the amber cost label is sufficient. Avoid audio overload.
- **RC-M1 (Mobility offline hovering a lateral)**: the rejected-commit sound, played as a "you can't do this" signal even during hover (not just on attempted commit).
- **RC-F1 (Frame degraded)**: **no audio signal** (hidden effect).

#### H.2.5 Run-End Audio

- **E-1 Storm caught player**: the ominous drone from H.2.2 crescendos into a full storm-wall sound (loud, cathartic, scary). Briefly quiet as the run-summary transition begins.
- **E-3/E-7 Stranded**: engine-sputter-out sound; quieter, more pathetic ending than E-1.

#### H.2.6 Chassis-Specific Audio Beds

Per Pillar 2, each chassis's map-phase audio can emphasize its storm relationship:
- **Scout**: lighter ambient, storm feels more distant (supported by actual distance — Scout sees more map)
- **Assault**: neutral baseline
- **Truck**: heavier ambient, storm rumble more pronounced (it IS chasing the Truck harder)

Not required for MVP — flag as polish-phase enhancement.

## UI Requirements

Section H defined visual and audio style. This section defines the **interaction surface**: what's on screen, how the player reads it, how they input commits, and how the map integrates with the rest of the game's UI. All elements must support keyboard/mouse as primary input with gamepad as additive per technical-preferences.md.

### I.1 Screen Layout

The map screen is a single full-screen view. Layout zones (Unity UI Toolkit — UXML/USS):

```
┌─────────────────────────────────────────────────────────────┐
│  [Chassis]  [Hull HP]  [Fuel]  [Subsystem states]           │ ← HUD top bar (vehicle panel)
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   [Storm layer]   [Biome canvas — beacons + edges]          │
│                                                             │
│                                                             │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│  [Storm cadence pips]   [Biome name]    [Pause/Menu]        │ ← HUD bottom bar (map-specific)
└─────────────────────────────────────────────────────────────┘
```

- **HUD top bar (vehicle panel)**: persistent readout of vehicle state. Reads from `IVehicleView`. Never occludes the map canvas.
- **Biome canvas**: per H.1.1. Fills the central region. This is the only clickable/hoverable zone.
- **HUD bottom bar (map-specific)**: storm cadence pip indicator (H.1.7), current biome name, pause/menu button.

### I.2 Persistent HUD Elements (top bar)

| Element | Data source | Update cadence |
|---|---|---|
| Chassis icon | `IVehicleView.ChassisId` | Set at run start; no update |
| Hull HP bar | `IVehicleView.CurrentHullHp` / `MaxHullHp` | On V&P state-change event |
| **Fuel readout** | `IVehicleView.CurrentFuel` | On V&P state-change event; animates on spend/gain |
| Subsystem state row (4 icons: Weapon/Engine/Mobility/Frame) | `IVehicleView.SubsystemStates` | On V&P state-change event; tints icons per state (Online/Degraded/Offline) |

The subsystem row is **clickable** — clicking or hovering a subsystem icon tooltips that subsystem's current state AND flags any active Route Constraint attached to that subsystem (e.g., "MOBILITY OFFLINE — lateral moves blocked"). This makes the map's constraint layer self-documenting.

### I.3 Map-Specific HUD Elements (bottom bar)

- **Storm cadence pips** (per H.1.7): `ChassisStormCadence[chassis]` pips, with `CombatCommitCounter` lit. Tooltip: "Storm advances every N combats (chassis: [name]). You've fought [X] since the last storm tick."
- **Biome name panel**: current biome name + biome index (e.g., "Biome 2 of 3: Rust Flats"). Tooltip lists completed vs. total beacons in biome for player memory support.
- **Pause/Menu button**: opens game menu (handled by global UI, not this GDD).

### I.4 Beacon Interaction Flows

#### I.4.1 Hover

- Mouse: moving cursor over a beacon triggers hover state for beacons in `Reachable` or `Unvisited` states.
- Gamepad: d-pad / analog navigates between reachable beacons (nearest-neighbor discovery from `Current`). Active beacon = hover equivalent.
- Keyboard: arrow keys navigate between reachable beacons (same nearest-neighbor). Tab cycles through all Reachable.

Hover shows:
1. Beacon-type icon expanded with a name label ("Combat Encounter", "Merchant", etc.)
2. Fuel cost to commit (in the cost-label format from H.1.6 — includes any constraint surcharges)
3. If RC-W1 active and beacon is Elite: the warning banner
4. If applicable: brief flavor text (1 line, 6–10 words; e.g., "Raider outpost. Scrap likely.")
5. **If beacon is `Combat` / `EliteCombat`**: `EncounterType` label (Card Combat R15). Standard encounters add nothing. Ambush encounters append `"Ambush — enemy strikes first"` to the name label and announce it via screen reader per I.5 (e.g., `"Combat encounter, ambush, reachable, 2 fuel"`). This is the pre-commit readability contract: the player MUST see the encounter type before spending Fuel.

#### I.4.2 Commit

- Mouse: click on a hovered Reachable beacon → commit initiates.
- Gamepad: A button (or platform-equivalent "confirm") on the currently-selected beacon.
- Keyboard: Enter / Spacebar on the currently-selected beacon.

On commit initiation:
1. UI enters a `Committing` sub-state (all other beacons become non-interactive).
2. Brief commit animation plays (vehicle icon slides along the edge from `Current` to target).
3. Commit pipeline (C1.4) executes.
4. If successful, UI transitions out of map view into Node Encounter's handler.
5. If rejected (which should only happen for bugs, since UI pre-filters), show rejected-commit feedback (H.2.3) and return to idle map state.

#### I.4.3 Cancel / Undo

- No undo of commits (by design — commit is binding).
- During hover, ESC / B button / right-click clears the current hover selection without committing.

### I.5 Accessibility Requirements

Per `.claude/docs/technical-preferences.md` — "All core interactions must be keyboard/mouse accessible" and the accessibility-specialist domain:

- **Color-blind support**: Beacon state (Unvisited/Reachable/Current/Visited/Consumed) MUST be distinguishable without color. Primary channels: opacity, pulse animation, silhouette outline. Color is additive.
- **Beacon type silhouettes** (H.1.2) must be unique shapes — do not rely on color to distinguish Combat from Merchant from Elite.
- **Text scaling**: cost labels and banners scale with the game's UI scale setting. Minimum legible size: 16pt at 1080p.
- **Screen reader support**: every beacon must have an accessible name combining its type, state, and cost (e.g., "Combat encounter, reachable, 2 fuel"). Storm cadence pips expose a text alternative ("2 of 3 combats until storm advances").
- **Keyboard-only playable**: no feature requires mouse or gamepad. Confirmed covered by I.4.1–I.4.3.
- **Motion sensitivity**: storm noise band tendrils and storm advance animation should respect a "Reduce Motion" accessibility setting (owned by global accessibility; fallback to a static front line when enabled).

### I.6 Save / Load Touchpoints

- Opening the pause menu from the map triggers a save request. Save GDD serializer checks `IsCommitInProgress` before writing (E-5).
- On game load, if the persisted state's `Phase == OnMap`, the map view is the entry screen. `FromDto` reconstructs the graph and state; `Reachable` set is recomputed (per I-6).
- Mid-encounter loads (when `Phase == InEncounter`) are owned by Node Encounter and the specific encounter handler, not this GDD.

### I.7 Transitions

| From | To | Trigger | Responsibility |
|---|---|---|---|
| Map view | Node Encounter handler | Commit pipeline step 7 | Node Map relinquishes; Node Encounter takes over |
| Node Encounter handler | Map view | Encounter outcome returned | Node Encounter relinquishes; Node Map resumes at step 8 |
| Map view | Run-summary | Any run-end path (E-1, E-2, E-3, E-7) | Node Map publishes event; Run Meta handles summary |
| Map view | Haven cinematic | Haven commit | Node Map publishes `RunCompleted`; Run Meta handles cinematic |
| Game load | Map view | Load with `Phase == OnMap` | Save GDD restores state; Node Map rebuilds UI |

### I.8 Input Summary Table

| Action | Mouse | Keyboard | Gamepad |
|---|---|---|---|
| Hover beacon | Cursor over beacon | Arrow keys / Tab | D-pad / Left stick |
| Commit | Click | Enter / Space | A (confirm) |
| Clear hover | Right-click | ESC | B (back) |
| Pause/Menu | Click button | ESC (when no hover) | Start |
| Subsystem tooltip | Hover icon | Tab to HUD | D-pad to HUD row |

## Acceptance Criteria

Each criterion is independently testable. Verification method in parentheses: **(Automated)** = unit/integration test; **(Manual)** = QA walkthrough with documented evidence; **(Playtest survey)** = player-reported, threshold stated.

### J.1 Graph Generation

- **AC-NM1** Given a fixed `RunSeed`, the generated beacon graph is bit-identical across two independent runs (same beacon count, positions, edges, biome assignment). *(Automated)*
- **AC-NM2** Every generated graph has exactly one beacon at `X=0` (Start) and exactly one beacon at `X=MapWidth` (Haven). *(Automated)*
- **AC-NM3** Every beacon other than Haven has at least one outgoing forward edge — no dead-ends. *(Automated)*
- **AC-NM4** Every beacon other than Start is reachable from Start via forward-or-lateral traversal. *(Automated)*
- **AC-NM5** Graph density stays within `GraphDensityTarget ± 0.1` across 1000 seeded generations. *(Automated)*
- **AC-NM6** Beacon-type distribution matches the published weights within ±5% across 1000 seeded generations. *(Automated)*
- **AC-NM7** Two adjacent beacons are never both Elite encounters. *(Automated)*
- **AC-NM8** Generation completes in <50ms on the target spec (2020 mid-range PC, single thread). *(Automated perf)*

### J.2 Storm Model

- **AC-NM9** `StormFrontX` begins each run at `-StormStartOffset` regardless of chassis. *(Automated)*
- **AC-NM10** `CombatCommitCounter` starts at 0 and increments by exactly 1 on each Combat reward-screen close. *(Automated)*
- **AC-NM11** The storm advances by exactly `StormPaceX` when and only when `CombatCommitCounter >= ChassisStormCadence[chassis]`; the counter resets to 0 on the same tick. *(Automated)*
- **AC-NM12** Non-combat node resolutions (Event, Shop, Repair) do NOT increment `CombatCommitCounter` and do NOT advance the storm. *(Automated)*
- **AC-NM13** The storm never advances mid-combat; no hook inside `CombatLoop` mutates `StormFrontX` or the counter. *(Automated — static analysis + runtime assertion)*
- **AC-NM14** Any beacon whose X is less than `StormFrontX` transitions to `Consumed` on the same frame a storm advance applies. *(Automated)*
- **AC-NM15** Playtest survey: Scout runs feel "breath-room to scout ahead"; Truck runs feel "actively chased"; Assault sits between. *(Playtest survey — ≥70% agreement)*

### J.3 Fuel Cost

- **AC-NM16** `ComputeFuelCost(A, B, chassis, state)` returns identical integers for identical inputs — pure function, no RNG, no hidden state. *(Automated)*
- **AC-NM17** Lateral edges incur `FuelCostLateralSurcharge` on top of base cost; forward edges do not. *(Automated)*
- **AC-NM18** When `SubsystemStates[Engine] == Offline`, every displayed fuel cost includes `EngineOfflineSurcharge`, and the HUD annotates "+1 Engine offline". *(Automated + Manual)*
- **AC-NM19** Attempting to commit an edge where `CurrentFuel < ComputeFuelCost` is rejected by `IsValidCommit` and produces a specific "insufficient fuel" UI cue. *(Automated + Manual)*
- **AC-NM20** Committing an edge decrements `CurrentFuel` by exactly the computed cost — no double-spend, no rounding drift. *(Automated)*

### J.4 Route Constraints

- **AC-NM21** RC-M1 (Mobility Offline) blocks lateral-adjacent commits as a hard constraint — `IsValidCommit` returns false for any lateral edge. *(Automated)*
- **AC-NM22** RC-M1 does NOT block forward edges. *(Automated)*
- **AC-NM23** RC-W1 (Weapon Offline) adds a red "Elite warning" overlay to any edge leading to an Elite beacon but does NOT block commit. *(Automated + Manual)*
- **AC-NM24** RC-E1 (Engine Offline) matches AC-NM18; the +1 Fuel surcharge applies to all edges, not only forward. *(Automated)*
- **AC-NM25** RC-F1 (Frame Degraded/Offline) applies a hidden `+HostileTiltDelta` weight shift to Event outcomes; no UI indicator beyond existing Frame damage readout. *(Automated)*
- **AC-NM26** When a subsystem transitions Online→Offline mid-run, all affected Route Constraint flags update before the next commit prompt. *(Automated)*
- **AC-NM27** When multiple constraints apply simultaneously, surcharges and overlays combine additively without overriding each other. *(Automated)*

### J.5 Edge Cases

- **AC-NM28** EC-NM1 Fuel starvation (cannot afford any outgoing edge) ends the run with cause `FuelStarvation`; no pity refill occurs. *(Automated)*
- **AC-NM29** EC-NM3 Storm consuming the player's current beacon triggers the "storm overtake" run-end state. *(Automated)*
- **AC-NM30** EC-NM6 Dual-RC stranded (Mobility Offline + Fuel-below-min simultaneously blocking every edge) is detected and logged as a distinct telemetry event for balance review. *(Automated)*
- **AC-NM31** EC-NM9 A commit interrupted by a save/restore completes exactly once on resume — no double-apply of fuel cost or counter increment. *(Automated)*
- **AC-NM32** EC-NM11 Reaching Haven immediately ends the run as success regardless of storm position or remaining fuel. *(Automated)*
- **AC-NM33** EC-NM12 Saving mid-commit is blocked by `IsCommitInProgress`; attempt surfaces a "wait until node resolves" prompt. *(Automated + Manual)*
- **AC-NM34** All 13 edge cases from Section E have at least one corresponding automated test. *(Automated coverage check)*

### J.6 Save/Load

- **AC-NM35** `NodeMapDto` round-trips losslessly through the Save GDD serializer — every field equal after write→read. *(Automated)*
- **AC-NM36** On load, current beacon, storm front, `CombatCommitCounter`, `RunSeed`, and beacon visit states all resume identically to their pre-save values. *(Automated)*
- **AC-NM37** A save written in the v1.0 Node Map schema loads without crash after any additive DTO change; breaking changes require a documented migration. *(Automated + QA regression)*

### J.7 UI / Visual

- **AC-NM38** All five chassis beacon silhouettes render at the documented sizes and use the colorblind-safe palette tokens. *(Manual)*
- **AC-NM39** The storm front and its look-ahead shadow are legible at 1080p and 1440p with UI scaling from 80–150%. *(Manual)*
- **AC-NM40** The Fuel, Storm-next, Subsystem, and Beacon counters never overlap or clip at any supported aspect ratio (16:9, 16:10, 21:9). *(Manual)*
- **AC-NM41** Route Constraint overlays (RC-W1 red, RC-E1 +1 Fuel icon) are announced by the screen reader with exact cost text. *(Manual accessibility)*
- **AC-NM42** Hovering an edge previews fuel cost, any surcharges, and destination beacon type before commit. *(Manual)*
- **AC-NM43** Committing a node plays the chassis-bound audio bed transition without gaps or double-triggers. *(Manual)*
- **AC-NM44** Storm-advance tick has a distinct visual (front-slide) and audio cue (sub-bass rumble) played exactly once per advance. *(Manual)*
- **AC-NM45** Playtest survey: new players correctly identify "which nodes are still reachable" within 10 seconds of the map screen appearing. *(Playtest survey — ≥80% of first-time players)*
- **AC-NM45a** Combat and EliteCombat beacons with `EncounterType.Ambush` render the ambush overlay (H.1.2) in `Unvisited`, `Reachable`, and `Visited` states. Standard-encounter Combat beacons never render the overlay. Card Combat R15 Pillar 3 pre-commit readability contract. *(Manual visual)*
- **AC-NM45b** Hovering an `EncounterType.Ambush` beacon displays `"Ambush — enemy strikes first"` in the name label AND announces it via the screen reader (e.g., `"Combat encounter, ambush, reachable, 2 fuel"`). *(Manual accessibility)*
- **AC-NM45c** Graph-generation validator rejects any beacon with `isBoss == true` AND `EncounterType == Ambush`. Unit test: authored test graph with a boss-Ambush combination fails validation at graph-gen time and never reaches runtime. Closes Card Combat R15 bossfight readability contract. *(Automated)*

### J.8 Performance

- **AC-NM46** Map screen holds ≥60fps on the target spec with the storm-front shader active. *(Automated perf)*
- **AC-NM47** No GC allocations occur per-frame while hovering edges or panning the map. *(Automated allocation profiler)*
- **AC-NM48** Graph generation + initial render completes inside the "Entering Wasteland" transition budget (<250ms). *(Automated perf)*
- **AC-NM49** Save serialization of the full `NodeMapDto` completes in <5ms. *(Automated perf)*

### J.9 Pillar Alignment

- **AC-NM50** Pillar 2 "Chassis Identity" — across 1000 seeded runs at mid-biome (beacon index 40–60% of map), Scout's average Reachable-beacon set size exceeds Truck's by **≥40%**, and Assault falls strictly between them. Verifies that the chassis meaningfully shapes the map picture rather than producing only superficial subgraph differences. *(Automated simulation harness + graph-diff)*
- **AC-NM51** Pillar 3 "Read to Win" — before any commit, the player can see: current storm front, next-tick storm position, edge fuel cost including surcharges, and destination beacon type. *(Manual)*
- **AC-NM52** Pillar 4 "Scarcity with Agency (three domains) — Fuel isolation from Combat" — Fuel is never earned from combat reward tables; Scrap and Energy systems never gate route choices. *(Automated — reward-table grep + static rule check)*
- **AC-NM53** Pillar 5 "Route Reflects Vehicle State" — every subsystem Online→Offline transition produces at least one visible change to the route picture (surcharge, overlay, or blocked edge). *(Automated + Manual)*
- **AC-NM54** Pillar 2 "Chassis Identity — Truck compensation contract (CD Condition 4)" — Combat and Elite beacon reward-table outputs for `ChassisId == Truck` yield at least **1.25×** the Scrap and Fuel expected-value of the same tables for `ChassisId == Scout`, measured across 1000 seeded beacon resolutions. Fails if Truck ever ships with equal or lesser reward-value per critical-path beacon. *(Automated reward-table simulation)*
- **AC-NM55** Pillar 4 "Scarcity with Agency (three domains) — Scrap/Fuel data-path isolation" — no shared pool, field, or conversion function exists between the `CurrentScrap` and `CurrentFuel` runtime values. Merchant and Chopshop conversions route through explicit named verbs (`ConvertScrapToFuel(int)` / `ConvertFuelToScrap(int)`) with both sides logging the transaction, never through a shared wallet. *(Automated — static analysis + reference check)*

## Open Questions

These were surfaced during authoring and intentionally left unresolved for later GDDs, telemetry review, or design iteration. Each is tagged with its trigger condition for re-opening.

### Node Map-Native OQs

- **OQ-NM1** — Should fuel starvation ever allow a pity refill? Current answer: NO (EC-NM1). Revisit if telemetry shows >5% of runs ending via `FuelStarvation` — the design intent is rare & earned, not punitive. *Owner: Node Map GDD (this doc). Trigger: post-beta telemetry.*
- **OQ-NM2** — Is dual-RC stranded (Mobility Offline + fuel-below-min) acceptable as a "you should have played better" moment, or does it need a mitigation path? Current answer: acceptable. Revisit if >10% of EC-NM6 triggers come from early-run lockouts rather than late-run attrition. *Owner: Node Map GDD. Trigger: telemetry.*
- **OQ-NM3** — If a chassis swap mechanic is ever added (currently out of scope), how does `ChassisStormCadence` behave mid-run? Does the counter reset? Does the storm pace jump? *Owner: forward. Trigger: chassis-swap feature proposal.*
- **OQ-NM4** — Does the Haven approach (final ~3 beacons) have a storm behavior change — slowdown, acceleration, eye-of-the-storm beat? Currently flat. Worth prototyping if the final stretch feels flat in playtest. *Owner: Node Map GDD. Trigger: playtest feedback.*
- **OQ-NM5** — Fuel acquisition sources inside nodes — node rewards, chop-shop, specific Event outcomes — need to be legible enough to plan 2–3 nodes ahead without becoming deterministic. Balance pass lives in Scrap Economy + Loot & Reward GDDs but the Node Map enforces the legibility contract. *Owner: Scrap Economy GDD + Loot & Reward GDD. Trigger: those GDDs start.*
- **OQ-NM6** — Fuel starting amount per chassis is currently a tuning knob; the initial calibration against average nodes-to-Haven is untested. *Owner: Balance. Trigger: first-playable vertical slice.*

### Forward Dependencies (carried to downstream GDDs)

- **OQ-NM-FWD1** — Enemy archetype distribution across biomes — Node Map declares beacon types but not which enemies spawn where. *Resolved by: Enemy System GDD.*
- **OQ-NM-FWD2** — Event outcome distributions under RC-F1 `HostileTiltDelta` — needs a concrete event table. *Resolved by: Node Encounter GDD.*
- **OQ-NM-FWD3** — Shop inventory generation at Shop beacons — chassis-weighted or flat? *Resolved by: Scrap Economy GDD.*

### Retrofits Flagged (from Section F Dependencies)

- **V&P GDD retrofit** — add `SpendFuel(int)` to `IVehicleMutator`; expose `SubsystemStates` and `CurrentFuel` on `IVehicleView`; add `FuelBurnMultiplier` per chassis (Scout 0.8 / Assault 1.0 / Truck 1.3); document chassis-immutability contract mid-run.
- **Save & Persistence retrofit** — register `INodeMapSerializable`; document `IsCommitInProgress` pre-save check.
- **Card Combat retrofit** — add non-interaction row: "Combat never touches `Fuel`, `StormFrontX`, or `CombatCommitCounter`."

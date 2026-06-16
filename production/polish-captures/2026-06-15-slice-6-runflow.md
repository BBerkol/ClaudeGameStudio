# Polish Capture: Slice 6 — Run Flow (Canonical Biome-Web Generator + Host Orchestration)

**Date:** 2026-06-15
**System:** Run Loop — Slice 6 REVISED (canonical biome-web generator, host orchestrator, UI Toolkit map view)

**Supersedes:** `production/polish-captures/2026-06-13-slice-6-runflow.md` (linear `BuildLinearMilestone1` plan — rejected by TD on ADR-0011 grounds)

**Affected paths:**
- `Assets/Scripts/CombatView/RunSceneHost.cs`
- `Assets/Scripts/CombatView/CombatController.cs`
- `Assets/Scripts/CombatView/CombatOutcomeOverlay.cs`
- `Assets/Scripts/CombatView/CardRewardPicker.cs`
- `Assets/Scripts/Run/NodeMap.cs` (delete `SingleCombat`, delete `BuildLinearMilestone1`, add `BuildFromBiomes`)
- `Assets/Scripts/Run/BiomeWebGenerator.cs` (new)
- `Assets/Scripts/Run/BiomeDistributionSO.cs` (new)
- `Assets/Scripts/Run/RunController.cs` (tighten 3-arg `StartRun` overload)
- `Assets/Scripts/UI/MapViewController.cs` (new)
- `Assets/Scripts/UI/RunCompleteViewController.cs` (new)
- `Assets/Scripts/UI/BeaconNodeElement.cs` (new)
- `Assets/Scripts/UI/Phase1Marker.cs` (delete)
- `Assets/UI/Screens/MapView.uxml` + `MapView.uss` (new)
- `Assets/UI/Screens/RunComplete.uxml` + `RunComplete.uss` (new)
- `Assets/Resources/Run/Biomes/Biome1Distribution.asset` (new)
- `Assets/Prefabs/CombatView/Combat.prefab` (binder-array swap, picker/overlay event refactor)
- `Assets/Tests/EditMode/Run/*SingleCombat*.cs` (delete the 6 `NodeMap.SingleCombat` tests)
- `Assets/Tests/EditMode/Run/BiomeWebGenerator_Test.cs` (new)
- `Assets/Tests/EditMode/Run/BiomeDistributionSO_Test.cs` (new)
- `docs/architecture/adr-0015-biome-distribution-as-configuration-narrowing.md` (new)

## Proposed change

Slice 5b shipped `RunState.PendingCardOffer` latching but the chosen card
discards on the next `ResetCombat` because `NodeMap.SingleCombat` terminates
at Haven post-victory. Slice 6 retires `SingleCombat` AND the prototype
`BuildLinearMilestone1` helper in the same commit. The 1.0-canonical
`BiomeWebGenerator` lands per `design/gdd/node-map.md` C1.1: seeded FTL-style
free-placed beacon web, 5 vertical strips, 18-22 beacons per biome, 45°
forward cone, 80px minimum separation, gate-funnel chokepoint at strip 5,
forward-path-to-Haven guarantee, retry-on-constraint-violation.

Beacon-type scope is narrowed by **configuration, not code** (ADR-0015 first
application). Biome 1's `BiomeDistributionSO` only references Combat + Haven
entries; Merchant, Chopshop, EliteCombat, Event, and Rest enum values stay
defined but are absent from the biome-1 distribution table. Adding them later
is a data edit, not a code-path unfork — same generator, same commit pipeline,
no bimodal branches and no stub returns.

Host becomes the orchestrator — overlay and picker emit events upward instead
of calling `_controller.RequestReset` directly. Per-beacon enemy archetype
binding moves from a single `_enemyArchetypePrefab` SerializeField to a
serialized array indexed by Combat-beacon order with a
`Resources/EnemyArchetypes/<id>.prefab` fallback.

## Final-game picture this serves

The 1.0 shipping shape per `design/gdd/node-map.md` is a seeded FTL-style
beacon web across three biomes, with storm pressure and Fuel costs creating
routing decisions between fights. Slice 6 lands the canonical graph shape and
host orchestration. Storm, Fuel, and the four remaining beacon-type handlers
(Merchant, Chopshop, Event, Rest) ship in subsequent vertical slices on top
of the same generator and the same commit pipeline. The graph data model
does not change between this slice and 1.0; only what's wired to it grows.
M2 swaps biome-1's distribution table for biomes-2-and-3 distribution tables
with zero generator code changes — the slice success criterion ADR-0015
codifies.

## Authored values being destroyed

| Where | Value | Current | Replacement plan |
|---|---|---|---|
| RunSceneHost.cs | `_enemyArchetypePrefab` field (single GameObject SerializeField) | One archetype binder prefab wired in Inspector (DuneSkimmer in dev) | Deleted. Replaced by `_combatBeaconArchetypes` array indexed by Combat-beacon order. Skimmer value bakes into `array[0]` via `CombatPrefabAuthor`. Resources fallback at `Resources/EnemyArchetypes/<EnemyArchetypeId>.prefab`. |
| RunSceneHost.cs | `BeginNewRun` archetype binder lookup (lines 120-132) | Single binder GetComponent + warning fallback to DuneSkimmer | Per-beacon lookup driven by current `BeaconData.EnemyArchetype`. Generator-emitted graph carries archetype on each Combat beacon (from `BiomeDistributionSO.CombatEntries[i].ArchetypeId` via distribution roll); host reads each as the beacon enters. |
| RunSceneHost.cs | `BeginNewRun` map source | `NodeMap.SingleCombat(seed, archetypeId)` | `NodeMap.BuildFromBiomes(seed, new[] { Biome1Distribution })` via Resources-loaded SO. |
| RunSceneHost.cs | Public surface | `BeginNewRun`, `BeginCombat`, `EndCombat` | Plus `AdvanceToNextBeacon(reason)`, `RestartRun()`, `BeginCombatForCurrentBeacon()` + `event Action OnBeaconChanged`, `event Action<CombatLoop> OnCombatReady`, `event Action OnRunComplete` (System.Action per ADR-0014; ports ADR-0002 forward). |
| CombatController.cs | `ResetCombat` Slice-5b auto-advance line (`_host.Session.Advance(AdvanceReason.Departure)`) | Hardcoded Start → Combat[1] auto-step at scene boot | Removed. Host owns advance; controller exposes `BindCombat(CombatLoop loop)` seam that host invokes when a Combat beacon becomes current. |
| CombatController.cs | `ResetCombat` ownership of `BeginNewRun` + `BeginCombat` | Controller drives both at scene start | Host orchestrates. Controller becomes a pure presenter against an injected `CombatLoop` — same boundary Slice 5a started; finished here. |
| NodeMap.cs | `SingleCombat(int, EnemyArchetypeId)` factory (lines 76-98) | Slice-5a bootstrap helper, hardcoded 3-beacon chain | Deleted. No surviving caller after the host switches to `BuildFromBiomes`. |
| NodeMap.cs | `BuildLinearMilestone1(int)` factory (lines 61-73) + `Milestone1CombatArchetypes` static array (lines 49-54) | Slice-2 prototype helper, hardcoded escalating Skimmer→Shepherd→Dredge linear chain | Deleted. ADR-0011 cleanup — prototype scaffold that contradicts the canonical FTL-web shape per node-map GDD C1.1. |
| Run EditMode tests | 6 `NodeMap_SingleCombat_Test` cases | Cover the SingleCombat factory shape | Deleted alongside `SingleCombat`. New `BiomeWebGenerator_Test` (~7 cases) + `BiomeDistributionSO_Test` (~3 cases) cover canonical graph shape. |
| Run EditMode tests | `NodeMap_BuildLinearMilestone1` test coverage (~2 cases from Slice 2) | Cover linear hardcoded chain | Deleted alongside the factory. Generator tests replace coverage. |
| CombatOutcomeOverlay.cs | `Bind(CombatController)` signature | Takes controller, calls `_controller.RequestReset()` on defeat + missing-listener fallback (lines 271, 279) | New signature `Bind(events)` — overlay emits `event Action OnContinueRequested` (already exists) + `event Action OnRestartRequested` (new). Host subscribes to both. Defeat path: overlay raises `OnRestartRequested` → host calls `RestartRun()`. |
| CombatOutcomeOverlay.cs | `_controller` field + direct `RequestReset` calls | Owns reset dispatch | Removed. Overlay does not know about controllers — fires events to whoever wired the listener. |
| CardRewardPicker.cs | `Bind(RunSceneHost, CombatController, CombatOutcomeOverlay)` signature | Takes host + controller + outcome (line 74) | New signature `Bind(RunSceneHost, CombatOutcomeOverlay)` — picker keeps the host for `Session.AcceptCardChoice` / `SkipCardChoice` calls (the offer math), but `CloseAndReset` fires `event Action OnPickResolved` instead of calling `_controller.RequestReset`. Host listens and calls `AdvanceToNextBeacon(AdvanceReason.Departure)`. |
| CardRewardPicker.cs | `CloseAndReset` direct `_controller.RequestReset` call (line 318) | Picker drives combat reset on close | Removed. Picker raises `OnPickResolved`; host owns "what comes next" (map view, next beacon, or run-complete). |
| Combat.prefab | `RunSceneHost._enemyArchetypePrefab` override | Wired to DuneSkimmer archetype prefab | Cleared. New `_combatBeaconArchetypes` array override replaces it (Skimmer / Shepherd / Dredge prefabs in slots 0/1/2). |
| Combat.prefab | `CardRewardPicker` SerializeField wiring | `_host`, `_controller`, `_outcome` populated via `CombatHud.Bind` at runtime (not serialized — wiring is code-side) | Wiring trims to `_host`, `_outcome`. `_controller` reference dropped. |
| Combat.prefab | `CombatOutcomeOverlay` runtime binding | `Bind(controller)` called from CombatHud | New `Bind()` called with no controller dep; host subscribes to overlay events. |
| Phase1Marker.cs | Internal marker class | Phase-1 sentinel for `WastelandRun.UI` asmdef sanity | Deleted — Slice 6 ships real UI controllers that prove the asmdef. |
| RunController.cs | Public 3-arg `StartRun(vehicle, seed, map)` overload | Public for test injection of custom maps | Tightened to `internal` + `InternalsVisibleTo("WastelandRun.Tests.EditMode")`. Scene host uses the production overload that builds a real biome-web `NodeMap` internally. |
| Run/BeaconType.cs | Enum values | 3 values: `{Start, Combat, Haven}` | Extended to 8 values: `{Start, Combat, EliteCombat, Merchant, Chopshop, Event, Rest, Haven}`. Phase E lands the enum extension as the FIRST step (precondition for `BiomeDistributionSO.DistributionEntry.Type` field), before generator or SO authoring. |
| Run/BeaconType.cs | Doc comment (lines 3-8) | Cites ADR-0011 to justify narrow enum: "Milestone 1 ships only Start, Combat, and Haven — Fuel, Storm, Event, and Elite beacons arrive in later milestones and extend this enum at that time (no placeholder values shipped per ADR-0011 no-bridges)." | Rewritten to cite ADR-0015: full canonical enum from day one; scope narrowing happens via `BiomeDistributionSO` data tables, not enum gaps. ADR-0015 inverts the prior reasoning — enum stays canonical, distribution table shrinks. |

## Technical Director Review

**Verdict:** APPROVE (TD overturned prior 2026-06-13 verdict on the linear plan)
**Spawned at:** 2026-06-15 (this session — after user caught the 1.0-shape contradiction)
**Agent transcript:** Full TD response captured in session conversation; key points reproduced below.

**TD reasoning summary:**

*Prior verdict reversed:*
- Previous TD session (2026-06-13) approved `BuildLinearMilestone1` adoption.
  That was wrong — TD solved "make `PendingCardOffer` survive" by reaching
  for the nearest available helper without auditing against ADR-0011 or the
  user's 2026-06-01 retraction ("build canonical 1.0 shape directly, no
  throwaway scaffolding"). User caught this on re-read morning 2026-06-15.
  Current TD session overturns: linear is scaffolding, not 1.0 shape.

*ADR-0011 distinction — why this slice is NOT a stub/bimodal violation:*
- Beacon-type enum stays complete (all 7 types defined).
- Biome-1 distribution table only references `{Combat, Haven}` entries.
- Generator reads the table — it doesn't know other types are "missing."
- Single code path through canonical generator; no `if (handler exists) else throw` branches.
- This is ADR-0011 exception #4 (polymorphism via data tables).
- Adding Merchant later is a data edit (new entry in distribution table +
  new handler hook), not a code-path unfork.
- ADR-0015 codifies this pattern as load-bearing across the project.

*Locked by TD authority (user pre-approved with "all TD recommendations go"):*
- Q1=a — Biome-1 distribution: `{Combat, Haven}` only. EliteCombat waits
  for storm cadence pressure to land (Elite without storm misreads player intent).
- Q2=a — Full canonical topology: 5 strips, 18-22 beacons, 45° forward cone,
  80px min-sep, gate-funnel at strip 5, forward-path-to-Haven guarantee.
  Tuning is cheap later; getting the shape right now is what M2 needs.
- Q3=yes — Write ADR-0015 codifying "biome distribution as configuration
  narrowing" as the ADR-0011-compliant scope-narrowing pattern.

*Sequencing locked:*
- Graph slice (this one) → Fuel slice → Storm slice → Merchant/Event/Rest/Chopshop slices.
- Each upgrades the prior to canonical operating state.
- Each is one focused vertical slice, not a multi-week super-slice.

*Auto-advance line in `CombatController.ResetCombat` (Slice-5b regression fix):*
- Surgical removal + host-driven `_controller.BindCombat(loop)` seam.
- Host calls `_session.EnterCombat()`, then `_controller.BindCombat(loop)`.
- Controller becomes a pure presenter; auto-advance disappears because the
  host now owns stepping the beacon machine.

*The Slice 5b `PendingCardOffer` regression fixes naturally:*
- Currently dies because `SingleCombat` terminates at Haven on combat #1.
- Once host steps a real multi-Combat graph, offer survives across beacon
  advances. No special "minimal canonical move" needed.

*Pre-approved structural questions (carried forward from 2026-06-13 capture):*
- Q-A: Per-beacon archetype binder — **SerializeField array + Resources fallback**.
  Designer-friendly + tolerant of missing wiring + matches existing
  `CombatPrefabAuthor` patterns.
- Q-B: Old `_enemyArchetypePrefab` field — **delete it** (ADR-0011 no-bridges).
  Bake the Skimmer value into `array[0]` via author script before deletion.
- Q-C: Defeat path — **stay on `CombatOutcomeOverlay` restart**. Defeat =
  immediate restart loop; victory = map/RunComplete progression. RunCompleteView
  is not a defeat screen.

*Execution plan (7 phases, TD-approved):*
1. **Phase A** — This capture file + ADR-0015. User approval gate.
2. **Phase B** — Foundation UI: `MapViewController.cs`, `RunCompleteViewController.cs`,
   `BeaconNodeElement.cs` + matching UXML/USS. **MapView renders branching
   graph + connections + Reachable highlighting**, not a linear row.
   UI Toolkit primary stack per ADR-0014. No host wiring yet.
3. **Phase C** — Host orchestration: `RunSceneHost` gains `AdvanceToNextBeacon`,
   `RestartRun`, `BeginCombatForCurrentBeacon`, archetype binder array +
   Resources fallback, host events (`System.Action`). Delete `_enemyArchetypePrefab`
   (bake Skimmer into `array[0]`).
4. **Phase D** — Event-up overlay+picker: refactor `CombatOutcomeOverlay` +
   `CardRewardPicker` to emit events upward. Defeat path stays on overlay (Q-C).
5. **Phase E** — Canonical biome-web generator. **`BeaconType` enum extends
   to 8 values FIRST** (`{Start, Combat, EliteCombat, Merchant, Chopshop, Event,
   Rest, Haven}`) as the precondition for SO + generator authoring; doc comment
   rewritten to cite ADR-0015. Then: `BiomeWebGenerator` (FTL-placement per
   node-map GDD C1.1), `BiomeDistributionSO` (with `OnValidate` rejecting null
   `EnemyArchetypeId` on Combat/EliteCombat entries), `Biome1Distribution.asset`
   (Combat-weighted + Haven terminal), new `NodeMap.BuildFromBiomes(seed,
   BiomeDistributionSO[])` entry point. Host switches to it. Refactor
   `CombatController` to `BindCombat(CombatLoop)` seam. Remove auto-advance.
   Generator tests (~7, content-agnostic): deterministic-per-seed, min-sep
   invariant, forward-cone invariant, forward-path-to-Haven guarantee,
   gate-funnel topology, distribution-weighted type assignment, boss-Standard
   invariant (NM45c). Per-biome content test (`Biome1Distribution_emits_only_
   Combat_and_Haven`) is a separate, content-specific assertion.
6. **Phase F** — End-of-run `RunCompleteView`: wire on Haven arrival.
   Show `RunSeed` + final deck composition + restart.
7. **Phase G** — Cleanup + gates: delete `NodeMap.SingleCombat` + 6 tests,
   delete `NodeMap.BuildLinearMilestone1` + ~2 tests, delete `Phase1Marker.cs`,
   tighten `RunController.StartRun` 3-arg overload to `internal`, add CI grep
   gates across `WastelandRun.Run`, `WastelandRun.Combat`, `WastelandRun.UI`
   assemblies: `SingleCombat`, `BuildLinearMilestone1`, `_enemyArchetypePrefab`,
   `RequestReset` from view layer, **plus stub-handler patterns: `throw new
   NotImplementedException`, `// TODO: handler`, `Debug.LogWarning("not
   implemented`**. These extra grep terms enforce ADR-0015's "future
   beacon-type handlers must ship canonical, not as stubs" discipline.

**Success criteria additions:**
- All M1.5 EditMode tests still green after Slice 6 ships (no test churn
  outside the deleted SingleCombat/BuildLinearMilestone1 cases and their
  replacements).
- Generator tests pass against `Biome1Distribution.asset` AND against a
  synthetic full-7-type test fixture — proving content-agnostic algorithm.
- `BeaconType` enum has 8 values; doc comment cites ADR-0015. No `// TODO`
  markers anywhere in `Run/` or `Combat/` referencing missing beacon types.

*ADR alignment:*
- ADR-0011: No bridges. `SingleCombat` deletes; `BuildLinearMilestone1` deletes;
  `_enemyArchetypePrefab` deletes outright (no "fallback to old field"); test
  3-arg `StartRun` overload tightens to `internal` rather than living on as a
  production-facing test seam. Beacon-type scope narrowing happens via data
  (distribution table), not code-path branches — ADR-0011 exception #4.
- ADR-0013: `RunState.PendingCardOffer` flow is unchanged — Slice 6 lets it
  survive beacon advances, doesn't restructure it.
- ADR-0014: All new UI lands on UI Toolkit stack. `WastelandRun.UI` asmdef
  one-way arrow into `WastelandRun.Combat` + `WastelandRun.Run` already in place.
- ADR-0002: `System.Action` events under new UI stack (ports forward; no `UnityEvent`).
- ADR-0015 (new, lands in this capture): Codifies biome-distribution-as-
  configuration-narrowing as the canonical scope-narrowing pattern.

*Success criteria (TD-defined):*
- M2 swaps biome 1's distribution table for biomes 2-3 with zero generator
  code changes.
- Fuel slice plugs into `ComputeFuelCost` without touching the graph.
- `PendingCardOffer` survives across all biome-1 combats without a special case.

## User approval

- Reviewed: 2026-06-15
- Approved by: (pending — surface this capture to user before any Phase B code)
- Notes: User explicitly accepted all TD recommendations on Q1 (`{Combat, Haven}`
  only), Q2 (full canonical topology), Q3 (write ADR-0015) via "all TD
  recommendations go." Awaiting approval on this capture's content + the
  paired ADR-0015 file before Phase B begins.

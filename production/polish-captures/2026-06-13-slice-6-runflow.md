# Polish Capture: Slice 6 — Run Flow (Node-Map UI + Host Orchestration)

> **Superseded by:** `production/polish-captures/2026-06-15-slice-6-runflow.md` —
> User caught on 2026-06-15 that this capture's `BuildLinearMilestone1` adoption
> contradicted ADR-0011 + the 2026-06-01 "no scaffolding / build canonical 1.0
> shape directly" retraction. TD reversed prior verdict; replacement capture
> ships canonical FTL biome-web generator with ADR-0011 exception #4 scope
> narrowing (full `BeaconType` enum + narrow `BiomeDistributionSO` data table)
> per the new ADR-0015. This capture is retained for audit trail of the
> reversal — do NOT execute from this plan.

**Date:** 2026-06-13
**System:** Run Loop — Slice 6 (node-map UI, host orchestrator, linear M1 map adoption — REJECTED)

**Affected paths:**
- `Assets/Scripts/CombatView/RunSceneHost.cs`
- `Assets/Scripts/CombatView/CombatController.cs`
- `Assets/Scripts/CombatView/CombatOutcomeOverlay.cs`
- `Assets/Scripts/CombatView/CardRewardPicker.cs`
- `Assets/Scripts/Run/NodeMap.cs` (delete `SingleCombat` factory)
- `Assets/Scripts/Run/RunController.cs` (tighten 3-arg `StartRun` overload)
- `Assets/Scripts/UI/MapViewController.cs` (new)
- `Assets/Scripts/UI/RunCompleteViewController.cs` (new)
- `Assets/Scripts/UI/BeaconNodeElement.cs` (new)
- `Assets/Scripts/UI/Phase1Marker.cs` (delete)
- `Assets/UI/Screens/MapView.uxml` + `MapView.uss` (new)
- `Assets/UI/Screens/RunComplete.uxml` + `RunComplete.uss` (new)
- `Assets/Prefabs/CombatView/Combat.prefab` (binder-array swap, picker/overlay event refactor)
- `Assets/Tests/EditMode/Run/*SingleCombat*.cs` (delete the 6 NodeMap.SingleCombat tests)

## Proposed change

Slice 5b shipped `RunState.PendingCardOffer` latching but the chosen card
discards on the next `ResetCombat` because `NodeMap.SingleCombat` terminates
at Haven post-victory. Slice 6 retires `SingleCombat`, swaps `RunSceneHost`
to `NodeMap.BuildLinearMilestone1` (Start → Combat[Skimmer] → Combat[Shepherd]
→ Combat[Dredge] → Haven), and lands the node-map UI on the new UI Toolkit
stack (ADR-0014) so the player commits between fights and `PendingCardOffer`
survives across beacon advances. Hosts becomes the orchestrator — overlay
and picker emit events upward instead of calling `_controller.RequestReset`
directly. Per-beacon enemy archetype binding moves from a single
`_enemyArchetypePrefab` SerializeField to a serialized array indexed by
Combat-beacon order with a `Resources/EnemyArchetypes/<id>.prefab` fallback.

## Final-game picture this serves

Milestone 1 is "complete one biome end-to-end." That requires the player to
make non-trivial choices between fights (which card to keep, which beacon
to walk toward) and to see a run summary when the boss drops or the run
ends. The architectural shape under M1 is the same shape M2 needs — host
owns beacon orchestration, map UI is a presenter against
`INodeMapView`/`INodeMapMutator` facades, end-of-run screen is a presenter
against `RunState`. M2 swaps `BuildLinearMilestone1` for the branching map
builder; the host, the map UI, and the run-complete UI do not change.
Slice 6 is the last shape locked before content production scales up
(biome 2 roster, branching beacons, storm injection).

## Authored values being destroyed

| Where | Value | Current | Replacement plan |
|---|---|---|---|
| RunSceneHost.cs | `_enemyArchetypePrefab` field (single GameObject SerializeField) | One archetype binder prefab wired in Inspector (DuneSkimmer in dev) | Deleted. Replaced by `_combatBeaconArchetypes` array indexed by Combat-beacon order. Skimmer value bakes into `array[0]` via `CombatPrefabAuthor`. Resources fallback at `Resources/EnemyArchetypes/<EnemyArchetypeId>.prefab`. |
| RunSceneHost.cs | `BeginNewRun` archetype binder lookup (lines 120-132) | Single binder GetComponent + warning fallback to DuneSkimmer | Per-beacon lookup driven by current `BeaconData.Archetype`. `NodeMap.BuildLinearMilestone1` carries archetypes on the beacons; host reads each as the beacon enters. |
| RunSceneHost.cs | `BeginNewRun` map source | `NodeMap.SingleCombat(seed, archetypeId)` | `NodeMap.BuildLinearMilestone1(seed)` |
| RunSceneHost.cs | Public surface | `BeginNewRun`, `BeginCombat`, `EndCombat` | Plus `AdvanceToNextBeacon`, `RestartRun`, `BeginCombatForCurrentBeacon` + `event Action OnBeaconChanged` / `OnCombatReady` / `OnRunComplete` (System.Action per ADR-0014 ports ADR-0002 forward). |
| CombatController.cs | `ResetCombat` Slice-5b auto-advance line (`_host.Session.Advance(AdvanceReason.Departure)`) | Hardcoded Start → Combat[1] auto-step at scene boot | Removed. Host owns advance; controller exposes `BindCombat(CombatLoop loop)` seam that host invokes when a Combat beacon becomes current. |
| CombatController.cs | `ResetCombat` ownership of `BeginNewRun` + `BeginCombat` | Controller drives both at scene start | Host orchestrates. Controller becomes a pure presenter against an injected `CombatLoop` — same boundary Slice 5a started; finished here. |
| NodeMap.cs | `SingleCombat(int, EnemyArchetypeId)` factory | One-shot Slice-5a bootstrap helper (lines 76-98) | Deleted in the same commit that switches host to `BuildLinearMilestone1`. ADR-0011 §4 polymorphism carve-out no longer applies — helper has no surviving caller. |
| Run EditMode tests | ~6 `NodeMap.SingleCombat` tests | Cover the SingleCombat factory shape | Deleted alongside `SingleCombat`. New tests on host orchestration (binder array resolution, beacon-event firing) replace coverage. |
| CombatOutcomeOverlay.cs | `Bind(CombatController)` signature | Takes controller, calls `_controller.RequestReset()` on defeat + missing-listener fallback (lines 271, 279) | New signature `Bind(events)` — overlay emits `event Action OnContinueRequested` (already exists) + `event Action OnRestartRequested` (new). Host subscribes to both. Defeat path: overlay raises `OnRestartRequested` → host calls `RestartRun()`. |
| CombatOutcomeOverlay.cs | `_controller` field + direct `RequestReset` calls | Owns reset dispatch | Removed. Overlay does not know about controllers — fires events to whoever wired the listener. |
| CardRewardPicker.cs | `Bind(RunSceneHost, CombatController, CombatOutcomeOverlay)` signature | Takes host + controller + outcome (line 74) | New signature `Bind(RunSceneHost, CombatOutcomeOverlay)` — picker keeps the host for `Session.AcceptCardChoice` / `SkipCardChoice` calls (the offer math), but `CloseAndReset` fires `event Action OnPickResolved` instead of calling `_controller.RequestReset`. Host listens and calls `AdvanceToNextBeacon(AdvanceReason.Departure)`. |
| CardRewardPicker.cs | `CloseAndReset` direct `_controller.RequestReset` call (line 318) | Picker drives combat reset on close | Removed. Picker raises `OnPickResolved`; host owns "what comes next" (map view, next beacon, or run-complete). |
| Combat.prefab | `RunSceneHost._enemyArchetypePrefab` override | Wired to DuneSkimmer archetype prefab | Cleared. New `_combatBeaconArchetypes` array override replaces it (Skimmer / Shepherd / Dredge prefabs in slots 0/1/2). |
| Combat.prefab | `CardRewardPicker` SerializeField wiring | `_host`, `_controller`, `_outcome` populated via `CombatHud.Bind` at runtime (not serialized — wiring is code-side) | Wiring trims to `_host`, `_outcome`. `_controller` reference dropped. |
| Combat.prefab | `CombatOutcomeOverlay` runtime binding | `Bind(controller)` called from CombatHud | New `Bind()` called with no controller dep; host subscribes to overlay events. |
| Phase1Marker.cs | Internal marker class | Phase-1 sentinel for `WastelandRun.UI` asmdef sanity | Deleted — Phase 2 (this slice) ships real UI controllers that prove the asmdef. |
| RunController.cs | Public 3-arg `StartRun(vehicle, seed, map)` overload | Public for test injection of custom maps | Tightened to `internal` + `InternalsVisibleTo("WastelandRun.Tests.EditMode")`. Scene host uses the production 2-arg overload that builds `BuildLinearMilestone1` internally. |

## Technical Director Review

**Verdict:** APPROVE with three structural adjustments + three pre-Phase-B open questions
**Spawned at:** 2026-06-13 (this session, immediately after user said "Go through it with TD please")
**Agent transcript:** TD verdict captured below — full structured response delivered as Q1-Q7 binding decisions, 7-phase execution plan, and 3 explicit open questions. Reproduced in summary form for traceability; the full transcript is in the session conversation.

**TD reasoning summary:**

*Disagreements with prior Q1-Q7 verdicts (TD overrides):*
- **Q5 REJECTED** — orchestrator must be `RunSceneHost`, not `CombatHud`. CombatHud is a HUD presenter; making it own run-loop orchestration concentrates two responsibilities on a view component and bakes a wrong dependency arrow. Host already owns `RunSession`; map UI / picker / overlay / run-complete view are all peers under the host. Severity: high — getting this wrong now propagates into M2 branching map work.
- **Q3 ADJUSTED** — `RunCompleteView` needs `RunSeed` + final deck composition, not just combats-won + cards-picked. Players asked at M1 playtest for "what was my run." Seed enables run sharing in M2; deck composition is the only artifact that reflects choices.
- **Q4 STRENGTHENED** — also tighten `RunController.StartRun(vehicle, seed, map)` to `internal` + `InternalsVisibleTo` for tests. Test-only injection point should not be a production API.

*Auto-advance line in CombatController.ResetCombat (the Slice-5b regression fix):*
- TD rejected all 3 prior options (keep as-is / move to host BeginCombat / delete). Correct shape is **surgical removal + host-driven `BindCombat` seam**. Host calls `_session.EnterCombat()`, then `_controller.BindCombat(loop)`. Controller becomes a pure presenter; auto-advance disappears because the host is now the one stepping the beacon machine.

*Pre-Phase-B open questions (TD blocked code until answered):*
- **Q-A:** Per-beacon archetype binder — TD called this the most under-specified piece. User selected: **SerializeField array + Resources fallback**. Designer-friendly + tolerant of missing wiring + matches existing `CombatPrefabAuthor` patterns.
- **Q-B:** Old `_enemyArchetypePrefab` field — User selected: **delete it** (ADR-0011 no-bridges). Bake the Skimmer value into `array[0]` via author script before deletion.
- **Q-C:** Defeat path — User selected: **stay on `CombatOutcomeOverlay` restart**. Defeat = immediate restart loop; victory = map/RunComplete progression. RunCompleteView is not a defeat screen.

*Execution plan (7 phases, TD-approved):*
1. **Phase A** — This capture file. User approval gate.
2. **Phase B** — Foundation files: `MapViewController.cs`, `RunCompleteViewController.cs`, `BeaconNodeElement.cs` + matching UXML/USS under `Assets/Scripts/UI` + `Assets/UI`. UI Toolkit primary stack per ADR-0014. No host wiring yet.
3. **Phase C** — Host orchestration: `RunSceneHost` gains `AdvanceToNextBeacon`, `RestartRun`, `BeginCombatForCurrentBeacon`, archetype binder array + Resources fallback, host events (`System.Action`). Delete `_enemyArchetypePrefab` field (bake Skimmer into `array[0]`).
4. **Phase D** — Event-up overlay+picker: refactor `CombatOutcomeOverlay` + `CardRewardPicker` to emit events upward. Defeat path stays on overlay (Q-C).
5. **Phase E** — Map source switch: `RunSceneHost` from `NodeMap.SingleCombat` to `BuildLinearMilestone1`. Refactor `CombatController` to `BindCombat` seam (host-driven). Remove auto-advance.
6. **Phase F** — End-of-run RunCompleteView: wire on victory after final combat. Show run seed, final deck composition, restart.
7. **Phase G** — Cleanup + gates: delete `NodeMap.SingleCombat` + 6 tests, delete `Phase1Marker.cs`, tighten `RunController.StartRun` 3-arg overload, add CI grep gates (`SingleCombat`, `_enemyArchetypePrefab`, `RequestReset` from view layer).

*ADR alignment:*
- ADR-0011: No bridges. Single archetype field deletes outright; no "fallback to old field." `SingleCombat` deletes; no "deprecated" comment retained. Test 3-arg `StartRun` overload tightens to `internal` rather than living on as a production-facing test seam.
- ADR-0013: `RunState.PendingCardOffer` flow is unchanged — Slice 6 lets it survive beacon advances, doesn't restructure it.
- ADR-0014: All new UI lands on UI Toolkit stack. `WastelandRun.UI` asmdef one-way arrow into `WastelandRun.Combat` + `WastelandRun.Run` already in place.
- ADR-0002: `System.Action` events under new UI stack (ports forward; no `UnityEvent`).

## User approval

- Reviewed: 2026-06-13
- Approved by: (pending — surface this capture to user before any Phase B code)
- Notes: User pre-approved TD recommendations on Q-A (SerializeField array + Resources fallback), Q-B (delete `_enemyArchetypePrefab`), Q-C (defeat stays on overlay) via structured question response. Awaiting approval on the capture itself before Phase B begins.

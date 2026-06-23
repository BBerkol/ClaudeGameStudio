# Polish Capture: Slice 6 Phase F + G Closeout — UI Wire-Up + Cleanup + Grep Gates

**Date:** 2026-06-17
**System:** Run Loop — Slice 6 closeout (Phase F UI wire-up + Phase G cleanup/gates + Phase E controller-to-presenter completion)

**Supersedes scope of:** `production/polish-captures/2026-06-15-slice-6-runflow.md` §Phase F + §Phase G (the original capture wrote Phase F as RunCompleteView-only; reality moved when MapViewController landed fully built in Phase B but unwired through Phase E sub-commit 3 — closing it as a combined Phase F+G slice avoids leaving the codebase in a half-wired transitional state per ADR-0011).

**Path-2 scope expansion (2026-06-17 mid-session):** During Phase F authoring I found a sibling debug-shell auto-step in `CombatController.PickFirstCombatBeaconFromCurrent` that mirrors `CombatHud.PickFirstForwardEdge` — same anti-pattern, two instances. The original 2026-06-15 Phase E capture promised "CombatController becomes a pure presenter — finished here," but sub-commit 3 punted on it. TD ruled (2026-06-17) that both auto-steps are one anti-pattern and "finished here" means Slice 6 — punting again would stack the same debt twice in one slice. Scope expanded to also retire `PickFirstCombatBeaconFromCurrent`, move `ResetCombat` body into `HandleCombatReady`, delete `CombatController.RequestReset`, and rewire `RunControlsWidget` direct to `host.RestartRun()`. Landed as a **2-commit split** (see "Split rationale" below) so bisect surface stays isolated.

**Affected paths (commit A — UI wire-up):**
- `Assets/Scripts/CombatView/RunSceneHost.cs` (no view refs — keeps model-side; asmdef arrow stays intact)
- `Assets/Scripts/CombatView/CombatHud.cs` (delete `PickFirstForwardEdge` + its reward-claimed wiring)
- `Assets/Scripts/UI/RunSceneOverlayHost.cs` (NEW — owns view-controller SerializeField refs + Show/Hide orchestration + event subscriptions)
- `Assets/Scripts/UI/Phase1Marker.cs` (delete)
- `Assets/Scripts/UI/Phase1Marker.cs.meta` (delete)
- `Assets/Scripts/Run/RunController.cs` (tighten 3-arg `StartRun` overload to `internal` + `InternalsVisibleTo`)
- `Assets/Scripts/Run/WastelandRun.Run.asmdef` (add `InternalsVisibleTo("WastelandRun.Run.Tests")` etc. — verify which test asmdefs need access)
- `Assets/Prefabs/CombatView/Combat.prefab` (add `RunSceneOverlayHost` GameObject + UIDocument children for `MapView.uxml` and `RunComplete.uxml`)
- `tools/ci/grep-gates.sh` or equivalent (NEW grep gate set — see Phase G section, *excluding* `RequestReset`)

**Affected paths (commit B — controller-to-presenter):**
- `Assets/Scripts/CombatView/CombatController.cs` (delete `PickFirstCombatBeaconFromCurrent` + debug-shell comment, delete `RequestReset` public method, refactor `ResetCombat` → body moves into `HandleCombatReady`)
- `Assets/Scripts/CombatView/RunControlsWidget.cs` (rewire reset button: `_controller.RequestReset()` → `_host.RestartRun()`; drop `_controller` ref if dead)
- `tools/ci/grep-gates.sh` (append `\bRequestReset\b` pattern after commit A lands clean)

## Proposed change — Phase F (UI wire-up)

`RunCompleteViewController` and `MapViewController` were fully built in Slice 6 Phase B with `Bind/Show/Hide` + event surfaces (`OnRestartRequested`, `OnBeaconClicked`) but never wired into the scene. `RunSceneHost.OnRunComplete` event has fired since sub-commit 3 with no listener. `CombatHud.PickFirstForwardEdge` was added as a debug-shell auto-step because there was no map UI between combats — the method's own comment ("debug shell has no map UI between combats yet") is exactly the transitional-state ADR-0011 forbids at done state.

**Phase F closes the loop:** introduce `RunSceneOverlayHost` MonoBehaviour in `WastelandRun.UI` namespace. It holds `[SerializeField]` refs to `MapViewController` + `RunCompleteViewController`, finds `RunSceneHost` via `GetComponentInParent` (or a separate `[SerializeField]` ref), and subscribes to host events:
- `OnBeaconChanged` → `MapView.Bind(host.Session.Controller)` + `MapView.Show()` + `CombatHud.Hide()` (when current beacon is non-combat OR pending player choice)
- `MapView.OnBeaconClicked` → `host.AdvanceToNextBeacon(toIndex, HostAdvanceReason.PlayerChoice)` followed by `host.BeginCombatForCurrentBeacon()` if the destination is a Combat beacon
- `OnCombatReady` → `MapView.Hide()` + `CombatHud.Show()`
- `OnRunComplete` → `MapView.Hide()` + `CombatHud.Hide()` + `RunCompleteView.Bind(summary)` + `RunCompleteView.Show()`
- `RunCompleteView.OnRestartRequested` → `host.RestartRun()` (host's `BeginNewRun(null)` re-rolls fresh seed)

`RunSummaryViewModel` is built by the overlay host from `host.Session.Controller.State.RunSeed` + `host.Session.Controller.State.Deck.Cards` (the `RunDeck` already exposes `IReadOnlyList<CardDefinition> Cards` — UI can consume without crossing asmdef arrows).

## Proposed change — Phase G (cleanup + grep gates)

**Deletions (already partially done by sub-commit 3 — remainder lands here):**
- `Phase1Marker.cs` + `.meta` (Slice 6 Phase B prototype sentinel — real controllers now exist and are wired)
- `CombatHud.PickFirstForwardEdge` method + the reward-claimed handler that calls it (`_rewardClaimedHandler` etc.) — overlay host owns the next-beacon decision now
- Any `// TODO retire` or `// debug shell` transitional comments left in `CombatHud` adjacent to the deleted method

**Tightenings:**
- `RunController.StartRun(Vehicle, int, NodeMap)` → `internal` access modifier (scene host uses the production overload that builds via `BiomeWebGenerator`; only EditMode tests need the 3-arg overload)
- `WastelandRun.Run.asmdef` adds `InternalsVisibleTo` to the test asmdefs that exercise the 3-arg `StartRun` (per `grep -r "StartRun(.*NodeMap" Tests/`)

**CI grep gates (add to `tools/ci/grep-gates.sh` or wherever the existing checks live):**

| Pattern | Where | Reason |
|---|---|---|
| `\bSingleCombat\b` | `Assets/Scripts`, `Assets/Tests` | Retired Slice 5a sugar — must not return |
| `\bBuildLinearMilestone1\b` | `Assets/Scripts`, `Assets/Tests` | Retired prototype helper — must not return |
| `\bMilestone1CombatArchetypes\b` | `Assets/Scripts`, `Assets/Tests` | Retired hardcoded array — must not return |
| `\b_enemyArchetypePrefab\b` | `Assets/Scripts/CombatView` | Pre-Slice-6 single-prefab field — replaced by `_combatBeaconArchetypes` array |
| `\bRequestReset\b` | `Assets/Scripts/CombatView` (view layer only — `RunSession.RequestReset` is allowed model-side if it exists) | Picker/overlay no longer drive reset directly per Slice 6 |
| `\bPickFirstForwardEdge\b` | `Assets/Scripts` | Debug-shell fallback retires this slice — must not return |
| `\bPhase1Marker\b` | `Assets/Scripts`, `Assets/Tests` | Slice 6 Phase B sentinel — retires this slice |
| `throw new NotImplementedException` | `Assets/Scripts/Run`, `Assets/Scripts/CombatView` | ADR-0015 stub-handler discipline |
| `// TODO: handler` | `Assets/Scripts/Run`, `Assets/Scripts/CombatView` | ADR-0015 stub-handler discipline |
| `Debug.LogWarning\(.*not implemented` | `Assets/Scripts/Run`, `Assets/Scripts/CombatView` | ADR-0015 stub-handler discipline |

Grep gates exit non-zero on any match. Run as a pre-commit hook + CI step.

## Final-game picture this serves

Slice 6 closeout lands the canonical run flow end-to-end: start → branching-graph map → click beacon → combat → reward → map → next beacon → … → terminal Haven → run-complete summary → restart. No transitional debug-shell auto-stepping in production code, no unwired view controllers sitting dormant. The pattern (host emits model events, UI overlay subscribes + orchestrates Show/Hide) is the same shape future biome-1 handlers (Merchant, Chopshop, Event, Rest) will plug into when they ship — each new beacon-type handler adds its own UI view that the overlay host wires the same way. The grep gates ratchet ADR-0015's "no stub handlers" discipline so future authors can't accidentally ship a `throw new NotImplementedException` placeholder.

M2 swaps biome-1 distribution for biomes-2-and-3 distribution tables without touching any of the wired UI — the overlay host's contract is graph-shape-agnostic, just like the generator.

## Authored values being destroyed

| Where | Value | Current | Replacement plan |
|---|---|---|---|
| `CombatHud.cs` (lines ~596-664) | `PickFirstForwardEdge` static helper + reward-claimed handler that calls it | Debug-shell auto-picks the first forward edge between combats because "no map UI between combats yet" | Method deleted outright. Reward-claimed wiring removed. Overlay host's `MapView.OnBeaconClicked` → `host.AdvanceToNextBeacon` chain replaces it. The transitional comment block goes with the method. |
| `Phase1Marker.cs` | Slice 6 Phase B sentinel | Phase-1 marker proving `WastelandRun.UI` asmdef exists | Deleted. Real controllers prove the asmdef. |
| `RunController.cs` line ~64 | `public NodeMap StartRun(Vehicle vehicle, int seed, NodeMap map)` overload | Public — used by EditMode tests AND `RunSceneHost` in production for custom-map injection | Tightened to `internal`. `RunSceneHost` uses the production overload (`StartRun(Vehicle, int)` that builds via `BiomeWebGenerator`). Tests reach via `InternalsVisibleTo`. |
| `CombatHud.cs` adjacent xmldoc | Transitional comment "// flow routes through MapViewController and the player chooses the edge themselves; this fallback exists because the debug shell has no map UI between combats yet." | ADR-0011 transitional comment — slated for removal once MapView is wired | Removed alongside the method it describes. |
| `CombatController.cs` (lines 251-265) | `PickFirstCombatBeaconFromCurrent` static helper + xmldoc | Debug-shell auto-picks first forward Combat beacon from current beacon at scene boot — same anti-pattern as `PickFirstForwardEdge`, two instances. xmldoc literally admits: "production map UI hands the player a real choice of next-beacon; this helper exists because the debug shell has no map view yet." | (Commit B) Method deleted outright. Scene boot now shows the map (player parked on Start beacon, picks a Combat beacon themselves). MapView's `OnBeaconClicked` → `host.AdvanceToNextBeacon` chain replaces auto-step at both transition points. |
| `CombatController.cs` (lines 200-241) | `ResetCombat()` driver | Currently calls `BeginNewRun` + auto-step + `BeginCombatForCurrentBeacon` + `_loop.Start()` + target seed + log init — drives the host instead of presenting against an injected loop. | (Commit B) Refactored to pure presenter. Method body splits: scene-init half (just `_host.BeginNewRun()`) stays in `Start()`; loop-init half (`_loop.Start()`, target seed, log init, telegraph) moves into `HandleCombatReady(CombatLoop loop)` — the existing `OnCombatReady` listener. `ResetCombat` deleted. |
| `CombatController.cs` line 123 | `public void RequestReset() => ResetCombat();` | Public surface for debug widget reset button — view-layer entry point that drove combat reset, the `RequestReset` grep gate's target. | (Commit B) Deleted. `RunControlsWidget` rewires its button directly to `_host.RestartRun()`. View layer no longer holds a reset entry point. |
| `RunControlsWidget.cs` line 159 | `btn.onClick.AddListener(() => _controller.RequestReset());` | Debug reset button delegates through controller. | (Commit B) Rewired to `_host.RestartRun()` (widget gains a host ref, drops its controller dependency if it becomes the only consumer). |

## Split rationale (path 2 — TD-approved 2026-06-17)

The original single-commit plan grew into a 2-commit split when the controller-to-presenter scope was added:

**Why 2 commits, not 1**: bisect isolation. Commit A is mechanical UI wire-up + grep gates *excluding* `RequestReset` — a tight, low-risk change that lands green main. Commit B is the controller refactor that moves ~15 lines from `ResetCombat` into `HandleCombatReady` and deletes the auto-step + `RequestReset` entry point. If commit B breaks a scene-boot assumption (e.g. an EditMode test depending on `_loop != null` synchronously after `CombatController.Start()`), the revert surface is one commit, not the whole closeout. TD's prior Q5 verdict was "single closeout commit" assuming the original Phase F+G scope — that verdict updates to 2-commit split for the path-2 expansion.

**Why not 3 commits**: Phase 1 marker delete + grep gates land at clean ratchet points; splitting them off would just churn. Commit A lands them.

**Both commits must be EditMode-green before merging.** Per TD attestation requirement: "compilation-green ≠ semantic-green."

**The bimodal-paths rationale**: `PickFirstForwardEdge` and `PickFirstCombatBeaconFromCurrent` are the same anti-pattern in two instances. Killing one and keeping the other = first-transition policy ("scene-boot auto-step") differs from subsequent-transition policy ("player clicks map") for no semantic reason — exactly the bimodal-paths smell ADR-0011 forbids. Both must retire in the same slice.

## Technical Director Review

**Verdict:** APPROVE (TD verdict captured 2026-06-17, full TD response in session log)

**TD reasoning summary:**

*Q1 — Phase F scope: RunCompleteView + MapView together.* The capture's Phase F was written before MapView landed fully built; reality moved. Shipping RunCompleteView alone leaves `PickFirstForwardEdge` alive — that block's own comment is the ADR-0011 transitional-state pair. Combine.

*Q2 — Wire-up ownership: NEW `RunSceneOverlayHost` in `WastelandRun.UI` (Option B fallback).* TD's initial preference was Option (a) `RunSceneHost` owns the SerializeField refs. **Asmdef arrow check (2026-06-17) flipped the verdict to Option (b):** `WastelandRun.CombatView.asmdef` references `Combat + Run + Run.Authoring + UnityEngine.UI + TextMeshPro + InputSystem` — no `WastelandRun.UI`. `WastelandRun.UI.asmdef` references `Combat + Run`. The canonical arrow is `UI → Combat+Run` per ADR-0014. Adding a UI ref to CombatView would invert the arrow. Fallback (b) — thin overlay host in `WastelandRun.UI` — preserves the arrow. One new file, well-scoped.

*Q3 — Visibility orchestration: per-event Show/Hide from overlay host.* Four transitions, no state-machine value. Document the four transitions in xmldoc — that's the spec.

*Q4 — ADR-0004 persistence: PUNT to Slice 7.* M1 is pre-EA. No save surface exists. Sub-commit 3 brief flagged this as a blocker; sub-commit 3 shipped without it; nothing broke. Bolting persistence onto a UI wire-up slice violates the "no half-systems that re-break" rule. Lock as dedicated Slice 7 with its own TD brief. Documented here so the open question doesn't get re-asked.

*Q5 — Single closeout commit (UPDATED 2026-06-17 to 2-commit split).* TD initial verdict was one commit for the original Phase F+G scope. Mid-session scope expansion (path 2 — also retire `PickFirstCombatBeaconFromCurrent` + `CombatController.RequestReset`) triggered re-verdict: 2-commit split for bisect isolation. Commit A = UI wire-up + grep gates excluding `RequestReset`. Commit B = controller-to-presenter + `RequestReset` grep gate. Both must be EditMode-green separately.

*Path-2 mid-session re-verdict (2026-06-17):* TQ1–TQ5 (full transcript in session log).
- TQ1: `PickFirstForwardEdge` + `PickFirstCombatBeaconFromCurrent` = same anti-pattern, two instances. Treating differently would be cosmetic.
- TQ2: 2026-06-15 Phase E capture said "Controller becomes a pure presenter — finished here." Honoring that closes the slice cleanly. Punting again would stack the same debt twice in one slice.
- TQ3: Path 2 (full scope) recommended over path 1 (narrow) and path 3 (split). Bimodal residue isn't worth one extra slice cycle.
- TQ4 attestation requirements: (1) grep `_loop` null-coalesce assumptions in EditMode tests after `CombatController.Start()`; (2) verify `HandleCombatReady` idempotent + handles non-combat beacons cleanly; (3) confirm `_selectedTarget` seeding lands after `_loop.Enemy` exists once moved.
- TQ5: 2-commit split (commit A: UI wire-up; commit B: controller refactor). Both EditMode-green attested before merge.

*Q6 — Risk flags:*
- ADR-0011: Delete `PickFirstForwardEdge` method outright, not just the call site. ✓ (in plan above)
- ADR-0014: Asmdef arrow verified — Option (b) preserves it. ✓
- ADR-0015: TerminalType handling correct (`OnRunComplete` fires on `Current.Type == TerminalType`, not `ExitIndex`). ✓
- Grep gate set: Added `PickFirstForwardEdge` symmetric with other dead-name gates. ✓
- `RunSummaryViewModel` source: `RunDeck.Cards` enumeration is UI-consumable without arrow violation. ✓ (confirmed `RunDeck` exposes `IReadOnlyList<CardDefinition> Cards`)

**Success criteria (TD-defined):**
1. Post-closeout commit: `grep -r PickFirstForwardEdge` = 0, `grep -r Phase1Marker` = 0, `grep -r SingleCombat` = 0, `grep -r BuildLinearMilestone1` = 0.
2. No `WastelandRun.CombatView → WastelandRun.UI` asmdef edge.
3. Test count holds at 486/0/1 (+ any new view-wire tests if added); no regressions.
4. Manual run-through: start → combat → win → map shows → click beacon → combat → win → map → click terminal → RunCompleteView shows seed + deck → restart works.

## User approval

- Reviewed: 2026-06-17
- Approved by: User 2026-06-17 ("approve") for path-1 scope; then ("path 2") for path-2 scope expansion after TD's mid-session re-verdict on the second auto-step + bimodal-paths rationale.
- Notes: User explicitly asked for TD verdict via "Lets get TD in for ext phase." Path-1 scope locked + approved first. Mid-session find of `PickFirstCombatBeaconFromCurrent` triggered re-spawn of TD; TD verdict expanded scope to path 2 (2-commit split) with TQ4 attestation requirements. Both verdicts captured above. User approval recorded for path 2 final shape.

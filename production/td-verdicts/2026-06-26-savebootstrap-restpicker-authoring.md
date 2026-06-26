# TD Verdict — SaveBootstrap + RestPicker Authoring Fix + PlayMode Validation Test

**Date:** 2026-06-26
**Topic:** Wire SaveBootstrap and RestPickerController into Run.prefab `AuthorRun()`; add PlayMode prefab-validation test
**Files touched:**
- `Assets/Scripts/Save/SaveSystem.Write.cs` (add `IsBound` read-only property)
- `Assets/Editor/CombatPrefabAuthor.cs` (additive extensions to `AuthorRun()`)
- `Assets/Prefabs/Run/Run.prefab` (regenerated via menu)
- `Assets/Tests/PlayMode/CombatView/WastelandRun.CombatView.PlayMode.Tests.asmdef` (new asmdef — PlayMode test infrastructure first-time addition)
- `Assets/Tests/PlayMode/CombatView/RunPrefabAuthoring_Test.cs` (new test)

## Symptom that triggered the consultation

In Play mode the user hit:
```
[Save] EnqueueRunStateWrite skipped — SaveSystem not bound. This is expected for EditMode harnesses that don't run SaveBootstrap.
WastelandRun.CombatView.RunSceneHost:EnqueueRunStateSnapshot () (RunSceneHost.cs:538)
WastelandRun.CombatView.RunSceneHost:BeginNewRun (RunSceneHost.cs:310)
WastelandRun.CombatView.CombatController:Start () (CombatController.cs:253)
```

## Root cause

`CombatPrefabAuthor.AuthorRun()` builds Run.prefab with `RunSceneHost + RunSceneOverlayHost + MapView + RunCompleteView` but never adds `SaveBootstrap`. Consequence: no run state has been persisted in Play mode since Slice 8b-3 shipped — the whole ADR-0004 plumbing (NodeMapDto, RunSeedDto, RunDeckDto, VehicleStateDto) was silently inoperative outside EditMode test fixtures.

Same authoring gap for Slice 9b (just merged commit `bc384b1`): `AuthorRun()` doesn't bake the new `RestPicker` GameObject + UIDocument + RestPickerController, nor wire `RunSceneOverlayHost._restPicker`. Tests passed because the EditMode fixture builds components in-memory and reflectively injects SerializeFields — the prefab path was never validated.

## TD Verdict

**APPROVE** on all three scope points, with one tightening (PlayMode test addition).

### 1. SaveBootstrap on Run.prefab root — APPROVE
Docstring states "sibling to RunSceneHost," and RunSceneHost lives on Run.prefab root. Authoring it into CombatScene.unity instead would split the run-loop lifecycle across two authoring surfaces and break the "Run.prefab owns the run-loop layer" categorical claim. Run.prefab is the prefab that gets instantiated per run-loop boot; SaveBootstrap's single-shot Bind survives RestartRun because the *prefab instance* survives RestartRun (RestartRun rehydrates state, doesn't reinstantiate the host). Stays on Run.prefab. Categorical fit holds.

### 2. RestPickerController as child of Run.prefab — APPROVE
Identical pattern to MapView / RunCompleteView (UIDocument + Controller child, wired to RunSceneOverlayHost field). ADR-0014 compliant. No reason to place elsewhere.

### 3. Ordering — one concern, resolved
`SaveBootstrap` already has `[DefaultExecutionOrder(-100)]` per its docstring — verified on the class (line 44). `RunSceneHost.BeginNewRun` is called from `CombatController.Start` (the stack trace confirms this), which runs at default order 0. So `SaveBootstrap.Awake` (order -100) → `SaveSystem.Bind` → `CombatController.Start` → `BeginNewRun` → `EnqueueRunStateSnapshot` works.

Verified `RunSceneOverlayHost.OnEnable` has zero `SaveSystem`-touching code — ordering is fine.

### 4. Test coverage gap — IN SCOPE, add it (binding addition)
This is the second time the prefab-vs-test-fixture divergence has bitten (Slice 7a `CardRewardPicker` subscription lifecycle was the same shape: tests reflectively injected serialized refs, prefab didn't have them wired). **Recommend a PlayMode prefab-validation test** that instantiates `Run.prefab` and asserts:
- `GetComponent<SaveBootstrap>() != null`
- `SaveSystem.IsBound` after one frame
- `GetComponentInChildren<RestPickerController>() != null`
- Defensively: `MapViewController` + `RunCompleteViewController` (locks existing wires from regressing)

One test, ~30 lines, catches the entire class of "AuthorRun() forgot to bake X" regressions going forward. **Add it in the same commit as the AuthorRun() fix** — otherwise it gets deferred and the next slice regresses the same way.

This requires a new asmdef (`WastelandRun.CombatView.PlayMode.Tests.asmdef`) since there is no existing PlayMode test infrastructure under `Assets/Tests/PlayMode/`. First-time addition — structural boundary change, but additive (no existing asmdef shrinks/splits).

### 5. Capture-before-destroy — N/A
`AuthorRun()` edit is additive (no destruction of authored values), so no capture file required by the hook. Confirmed.

## Proceed signal

**Proceed.**

## ADRs at risk of drift

- **ADR-0004 (Save & Persistence)**: was inoperative in Play mode — this fix restores its production path
- **ADR-0014 (UI Toolkit primary stack)**: RestPicker integration follows the same pattern as MapView/RunCompleteView, no drift
- **ADR-0011 (No bridges meta-rule)**: `SaveSystem.IsBound` is a read-only check used solely by the validation test — not a runtime branching token, no #5 violation

## Final-game picture this serves

Persistence working end-to-end in Play mode is table-stakes for the Early Access shipping target. The regression class ("AuthorRun() forgot to bake X") will keep recurring as new prefab-component wiring lands (next likely: future beacon-type controllers — Merchant/Event/EliteCombat — per ADR-0015). A single PlayMode validation test pinning the canonical Run.prefab component set converts those silent regressions into hard test failures.

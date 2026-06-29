# Capture — Option B topology pivot, author-side cut

**Date:** 2026-06-28
**System:** Beacon presentation authoring (`CombatPrefabAuthor.cs`)
**Trigger:** Execution of the 2026-06-28 TD verdict
(`production/td-verdicts/2026-06-28-option-b-topology-pivot.md`).
Authoring code must follow the topology cut — `BeaconSceneOrchestrator` is
retired; `BeaconActivator` ships as a scene-level GameObject in `RunScene`;
Rest beacon moves from `Rest.unity` (additive scene) to `RestRoot.prefab`
(SetActive-toggled prefab root parented under the activator).

## Files about to be touched

- `Assets/Editor/CombatPrefabAuthor.cs`
  - `AuthorRunPrefab` — remove `BeaconSceneOrchestrator.AddComponent` block
    and the `_beaconSceneOrchestrator` serialized-property write on
    `SaveBootstrap`.
  - `AuthorRunScene` — add scene-level `BeaconActivator` GameObject;
    instantiate `RestRoot.prefab` as a sibling under the activator;
    serialize-wire `BeaconActivator._host`, `._sceneBinding`,
    `._prefabRoots[0]`.
  - `AuthorRestScene` — replace with `AuthorRestRootPrefab` (writes
    `Assets/Prefabs/BeaconRoots/RestRoot.prefab` carrying
    `BeaconSceneBootstrap` + `PlayerVehicle` instance +
    `RestPicker UIDocument` + `RestPickerController`).
  - `AuthorBeaconSceneBinding` — write per-entry `Mode`; Rest →
    `BeaconLoadMode.PrefabRoot` with empty `ScenePath`; Combat /
    EliteCombat / Haven / Merchant / Event / Chopshop → `AdditiveScene`
    with existing paths.
  - Constants — drop `RestScenePath`, add `RestRootPrefabPath`.
  - `AuthorAllScenes` — drop the `AuthorRestScene()` call, add the
    `AuthorRestRootPrefab()` call (runs before `AuthorRunScene`).

## What's being destroyed

- `BeaconSceneOrchestrator.AddComponent<>` block + `SerializedObject` wiring
  of `_host` and `_sceneBinding`.
- `SaveBootstrap._beaconSceneOrchestrator` serialized-property write.
- `AuthorRestScene` method body (Rest.unity author path).
- `RestScenePath` constant.
- `AuthorAllScenes` line invoking `AuthorRestScene()`.

No designer-tuned values are being lost — every value being removed is a
code reference (component-add, serialized wire). Rest scene assets
(`Rest.unity`) carry no authored designer values today (was only authored
2026-06-27, never opened in Scene view for tuning per session log).

The capture-before-destroy intent here is "code cut bigger than 50 lines,
TD verdict required, snapshot the cut for traceability" — not "preserve
designer polish" (none accrued on `Rest.unity`).

## Technical Director Review

Reuses the verdict at
`production/td-verdicts/2026-06-28-option-b-topology-pivot.md` — Option B
hybrid topology APPROVED with three load-bearing conditions:

1. `BeaconLoadMode` lives on the SO, not in code — ADR-0015 compliance.
2. Resume-into-resolved-Rest bug ships FIRST in its own commit. *Status:
   shipped this session.*
3. EditMode test migration ships IN the topology commit, not after.

This author-side cut serves condition #1 (the binding SO author path is
where `Mode` becomes a real field) and condition #3 (the test migration in
follow-up requires the new author paths to exist).

Cleared for execution.

## Scope amendment (user decision, this session)

User narrowed Option B's PrefabRoot scope to **Rest only** for this
commit. Haven / Merchant / Event / Chopshop remain `AdditiveScene` mode
pointing at their existing stub scenes. This is a smaller cut than the
verdict's "all 5 light beacons" — but the cut is forward-compatible: the
`PrefabRoot` mode + the activator's `_prefabRoots` list already accept
the other four when their UI lands; converting Haven et al. is one SO
entry flip + one prefab author each (no further activator changes).

The Haven/Merchant/Event/Chopshop stub scenes therefore stay authored
in this commit (still in `AuthorAllScenes`); only `Rest.unity` is
deleted.

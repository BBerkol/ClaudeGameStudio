# Chopshop Workbench — Shape C Author Slice (2026-08-01)

## Summary

Chopshop Phase 2.5 lands the beacon as a `PrefabRoot`-mode scene with a
fresh `ChopshopWorkbenchController` following the Shape C verdict
(vehicle-centered workbench + right dialogue panel + modal overlays for
non-repair ops). Repair recovers welding logic from git commit
`b26fc77^` (pre-strip `RestPickerController.cs`); Forge / Upgrade /
Parts ship as disabled `(soon)` choice buttons.

Two ADR-0011 audits are folded into the slice:
1. `VehicleBarStack.BindForRest → BindForWorkbench` **hard rename** (not
   alias) — no external callers post-Rest-strip.
2. Add `RunSession.ResolveChopshop` + `OnChopshopModelCommitted` and
   `RunSceneHost.NotifyChopshopResolved` + `HandleChopshopModelCommitted`
   to close the resolve-verb symmetry (Rest / Event / Merchant already
   have per-beacon verbs).

Two-wire flip per `feedback_prefabroot_binding_flip`:
1. `BeaconSceneBinding.asset` Type 4 (Chopshop) — Mode `0 → 1`, ScenePath
   cleared.
2. `CombatPrefabAuthor.AuthorRunScene` `_prefabRoots` list gains a fourth
   entry (Chopshop → ChopshopRoot GameObject) in the same commit.

## User answers to blockers (Q1/Q2/Q3)

**Q1 (backdrop)**: use `Assets/Resources/Chopshop BG.png` (already
staged 2026-07-02 per `project_chopshop_bg_rest_backdrop`). Loaded via
USS `background-image: resource("Chopshop BG");`. No new art ask.

**Q2 (vehicle pose)**: **REUSE `VehicleRestPose`** — same component that
Rest uses (`_restLocalPosition = Vector3.zero`, `_restScale = 1`,
wheels/bounce/dust/blur frozen via `VehicleMotionState.Rest`). Do NOT
create a new `VehicleWorkbenchPose` component; user rejected the
"wheels-off on jackstands" fiction as unnecessary for EA. Chopshop
composes the vehicle beside (not behind) the dialogue via UXML
`vehicle-stage` slot layout, not via a new pose.

**Q3 (Back during repair)**: "we covered all of these when doing the
rest repair" — recover UX from `git show b26fc77^:.../RestPickerController.cs`
verbatim. Right-click cancels active repair; Back button is only
reachable when NOT hovering (implicit in the pre-strip cursor-swap
UX — repair mode captures the cursor).

## Files being authored / modified

| File | Change type | Purpose |
|---|---|---|
| `Assets/UI/ChopshopScreen.uxml` | NEW | Mirror `RestScreen.uxml` — dialogue right, illustration left, vehicle stage overlay, 5 choice buttons, peek-overlay + modal container |
| `Assets/UI/ChopshopScreen.uss` | NEW | Mirror `RestScreen.uss` tokens; `resource("Chopshop BG")` backdrop; workbench-modal classes for Forge/Upgrade/Parts placeholders |
| `Assets/Scripts/CombatView/ChopshopWorkbenchController.cs` | NEW | Fresh controller. Entry-choice routing; Repair inlines pre-strip welding logic verbatim (cursor swap, welding sparks, budget bar drain pump); Forge/Upgrade/Parts open placeholder modals; Leave calls `NotifyChopshopResolved` |
| `Assets/Scripts/CombatView/VehicleBarStack.cs` | RENAME | `BindForRest → BindForWorkbench` + `UnbindRest → UnbindWorkbench` + `_restBound → _workbenchBound` + `_restTargetGetter/_restVisual → _workbench*` + `UpdateRestBound → UpdateWorkbenchBound` + all doc-comment mentions |
| `Assets/Scripts/Run/RunSession.cs` | ADD | `ResolveChopshop()` verb + `OnChopshopModelCommitted` event (mirror `ResolveMerchant` shape) |
| `Assets/Scripts/CombatView/RunSceneHost.cs` | ADD | `NotifyChopshopResolved()` + `HandleChopshopModelCommitted` bridge (mirror Merchant plumbing) |
| `Assets/Editor/CombatPrefabAuthor.cs` | ADD | `ChopshopRootPrefabPath` const, `AuthorChopshopRootPrefab` menu, `AuthorRunScene` fourth `_prefabRoots` entry, `AuthorBeaconSceneBinding` roster flips Chopshop to `PrefabRoot, string.Empty` |
| `Assets/Data/BeaconScenes/BeaconSceneBinding.asset` | EDIT | Type 4 (Chopshop) — Mode `0 → 1`, `ScenePath: ""` |
| `Assets/Prefabs/BeaconRoots/ChopshopRoot.prefab` | NEW (via author menu) | BeaconSceneBootstrap + PlayerVehicle instance + VehicleRestPose child mount + ChopshopScreen UIDocument + ChopshopWorkbenchController wired via SerializedObject |

## Values being destroyed (VehicleBarStack rename)

All values below are **method/field names only** — no serialized data
lost. The pre-strip Rest binding was invoked from Rest scene only;
Rest scene is narrative-only post-strip and has zero callers of
`BindForRest`. Grep confirmed no external caller before rename.

- `public void BindForRest(Func<Vehicle> targetGetter, VehicleVisual visual)` → `BindForWorkbench(...)`
- `public void UnbindRest()` → `UnbindWorkbench()`
- `private bool _restBound` → `_workbenchBound`
- `private Func<Vehicle> _restTargetGetter` → `_workbenchTargetGetter`
- `private VehicleVisual _restVisual` → `_workbenchVisual`
- `private void UpdateRestBound()` → `UpdateWorkbenchBound()`
- Doc-comment mentions at lines 65, 146, 634, 636, 654, 756 — updated
  in-place to "workbench".

## ADR-0011 audit (no bridges at done)

- **Hard rename** (not alias): no `[Obsolete]` shim, no delegating
  overload, no adapter class. If a caller-forgotten site surfaces at
  compile time, it gets its own rename in the same edit. Grep verified
  zero call-sites before edit.
- **Per-beacon-type verb**: `ResolveChopshop` is its own method — not a
  parameterized `Resolve(BeaconType)` helper. Matches
  `ResolveRest`/`ResolveEvent`/`ResolveMerchant` precedent
  (`RunSession.cs:640-790`).
- **PrefabRoot binding flip**: single line change (`_mode: 0 → 1`,
  `_scenePath` cleared). Old `Assets/Scenes/Beacons/Chopshop.unity`
  stub becomes orphaned — safe-delete follow-up after Play-mode
  verification confirms the wire (mirrors Merchant 2026-07-30
  follow-up).
- **Reuse `VehicleRestPose`**: user rejected new-component path; naming
  now slightly leaky (a "Rest" pose used at Chopshop). Accepted trade
  — component behavior is content-blind (freeze motion + snap to
  origin); if the fiction diverges post-1.0 (jackstands actual
  animation), split at that time.

## Rollback map

- **Hard rollback (whole slice)**: `git revert <slice-commit>` reverts
  all 9 files atomically.
- **Soft rollback (keep code, hide beacon)**: flip
  `BeaconSceneBinding.asset` Type 4 Mode `1 → 0` (returns to empty
  stub scene, black screen — matches pre-slice behavior).
- **Bar-stack rename revert only**: `git checkout HEAD~ -- Assets/Scripts/CombatView/VehicleBarStack.cs`
  — safe because no caller in current tree references
  `BindForWorkbench`.
- **Resolve-verb revert**: `git checkout HEAD~ -- Assets/Scripts/Run/RunSession.cs Assets/Scripts/CombatView/RunSceneHost.cs`
  — safe because ChopshopWorkbenchController.Leave path re-hits
  compile error until re-added.

## Technical Director Review

Verdict logged in prior session `production/session-state/active.md`
handoff dated 2026-08-01 EOD. Selected **Shape C** (vehicle-centered
workbench + right dialogue panel + modal overlays for non-repair ops).
Rejected Shape A (bimodal composition — ADR-0011 smell — because entry
and repair states would ship as separate UXML files) and Shape B (16%
middle strip too cramped for future Merchant-scale Forge/Parts offer
grid).

Blockers Q1/Q2/Q3 raised in that verdict have all been answered by
user (see § "User answers to blockers"). Q2 overridden — reuse
`VehicleRestPose`, not new `VehicleWorkbenchPose`. Q1 satisfied by
existing `Chopshop BG.png` staged asset. Q3 satisfied by verbatim
recovery of pre-strip welding UX from git.

Locked constraints from `2026-07-29-rest-repair-strip-to-chopshop.md`
(all preserved by this slice):
1. Dialogue panel stays visible during repair ✓ (Shape C's core promise)
2. Vehicle composed BESIDE dialogue, not behind it ✓ (UXML `vehicle-stage`
   slot positioned left, dialogue panel right)
3. Cursor swap + welding sparks + budget bar byte-identical ✓ (verbatim
   recovery from `b26fc77^`)
4. Repair alongside Forge + Upgrade + Parts ✓ (5-button entry choice
   set)

Follow-ups deferred to future slices:
- Forge modal wiring (placeholder in this slice)
- Upgrade modal wiring (placeholder)
- Parts modal wiring (placeholder — blocked on `RunInventory` from
  Phase 2.5 parts-axis)
- Extract `WorkbenchModalPanel` helper (TD flag: before 2nd modal
  lands) — deferred since only Repair ships live this slice
- Delete orphaned `Assets/Scenes/Beacons/Chopshop.unity` stub after
  Play-mode verify

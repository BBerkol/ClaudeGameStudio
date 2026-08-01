# TD Verdict — Chopshop Workbench Shape C + Resolve-Verb Symmetry (2026-08-01)

## Scope

Chopshop Phase 2.5 lands the beacon as a `PrefabRoot`-mode scene with a
fresh `ChopshopWorkbenchController` and closes two ADR-0011 audits in
the same slice:

1. `VehicleBarStack.BindForRest → BindForWorkbench` **hard rename** (no
   alias). All identifier renames scoped to the file + one xmldoc
   `<c>BindForRest</c>` mention in `VehicleHudAnchors.cs`. Zero external
   callers pre-edit (grep-verified — Rest scene is narrative-only
   post-2026-07-29 strip).
2. Add `RunSession.ResolveChopshop()` verb + `OnChopshopModelCommitted`
   event, and matching `RunSceneHost.NotifyChopshopResolved()` +
   `HandleChopshopModelCommitted()` bridge. **Verbatim shape mirror**
   of the existing `ResolveRest` / `ResolveEvent` / `ResolveMerchant`
   trio.

## Files touched

- `Assets/Scripts/CombatView/VehicleBarStack.cs` — identifier rename +
  doc-comment refresh
- `Assets/Scripts/CombatView/VehicleHudAnchors.cs` — one xmldoc mention
- `Assets/Scripts/Run/RunSession.cs` — add event decl + verb (xmldoc
  changes)
- `Assets/Scripts/CombatView/RunSceneHost.cs` — add bridge method +
  subscribe line (xmldoc changes)
- `Assets/UI/ChopshopScreen.uxml` / `.uss` (new)
- `Assets/Scripts/CombatView/ChopshopWorkbenchController.cs` (new)
- `Assets/Editor/CombatPrefabAuthor.cs` — new author menu + roster
  entries
- `Assets/Data/BeaconScenes/BeaconSceneBinding.asset` — Chopshop
  Type 4 Mode `0 → 1`
- `Assets/Prefabs/BeaconRoots/ChopshopRoot.prefab` (new, via author
  menu)

## ADRs at risk

- **ADR-0011 (no-bridges-at-done)** — hard rename (not alias); per-
  beacon resolve verb (not parameterized helper). CLEAN.
- **ADR-0014 (UI Toolkit primary)** — Chopshop is UXML + USS + C#
  controller. Fresh controller, no UGUI. CLEAN.
- **ADR-0004 (save & persistence)** — new `OnChopshopModelCommitted`
  routes into existing `EnqueueRunStateSnapshot()` path via
  `RunSceneHost.HandleChopshopModelCommitted`. Snapshot BEFORE
  MarkResolved preserves the crash-window invariant already established
  for Rest / Event / Merchant. CLEAN.
- **ADR-0015 (biome distribution narrowing)** — Chopshop already
  present as `BeaconType.Chopshop = 4`; distribution asset unchanged
  this slice (Chopshop weight remains whatever `Biome1Distribution.asset`
  currently declares). CLEAN.

## Final-game picture

Chopshop is a full 1.0 beacon carrying Repair (live this slice) plus
Forge / Upgrade / Parts placeholders that wire live in follow-up slices
per `project_parts_axis_in_1_0`. Shape C's vehicle-centered layout
scales to the Parts-axis reward grid without a re-authoring pass
because the modal-container pattern is the same seam Forge / Upgrade
/ Parts will each pop.

## Alternatives considered (from prior TD consultation)

- **Shape A** (bimodal composition — separate UXML per state):
  rejected. Two UXML files per beacon smell ADR-0011 (bimodal path).
- **Shape B** (16% middle strip for actions): rejected. Middle strip
  is too cramped for future Parts-axis 12-slot offer grid; Chopshop
  and Merchant should share the same layout budget.

## Risk register

- **Two-wire PrefabRoot flip trap** — `BeaconSceneBinding.asset` Mode
  flip AND `BeaconActivator._prefabRoots` list entry must land in same
  commit per `feedback_prefabroot_binding_flip` (Merchant 2026-07-30
  black-screen incident). Both are enumerated in the capture file
  `2026-08-01-chopshop-workbench-shape.md` files-touched list.
- **VehicleRestPose leaky naming at Chopshop** — user opted to reuse
  the component (Q2 answer) rather than spin a new
  `VehicleWorkbenchPose`. Component behavior is content-blind (freeze
  motion + snap origin), so the leaky name is aesthetic only. Follow-
  up (post-1.0): rename `VehicleRestPose → VehicleParkedPose` if the
  fiction ever needs to diverge.
- **BindForWorkbench compile break** — if a code path I missed still
  calls `BindForRest`, compile fails cleanly. No runtime silent-break
  risk. Grep confirmed zero external callers pre-rename.

## TD Verdict

**APPROVE** — proceed as planned in the capture file. The
`RunSession.ResolveChopshop` + `OnChopshopModelCommitted` pair is a
mechanical extension of an already-vetted contract; the xmldoc changes
are additive (no existing contract surface mutates) and the fire order
(pre-MarkResolved) inherits the crash-window guarantee established for
the three sibling verbs. `RunSceneHost.HandleChopshopModelCommitted`
must mirror the Merchant bridge shape (subscribe in same block, call
`EnqueueRunStateSnapshot()`).

Follow-ups deferred (not this slice):
- Extract `WorkbenchModalPanel` helper before 2nd modal lands
  (Repair-only this slice — no 2nd modal in this commit).
- Forge / Upgrade / Parts wire (placeholders only — buttons visible
  but disabled with "(soon)" tooltip).
- Delete orphaned `Assets/Scenes/Beacons/Chopshop.unity` stub after
  Play-mode verify (mirrors Merchant 2026-07-30 follow-up).

Capture file: `production/polish-captures/2026-08-01-chopshop-workbench-shape.md`.

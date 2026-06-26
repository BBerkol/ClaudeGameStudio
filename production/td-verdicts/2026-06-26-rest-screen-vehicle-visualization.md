# Rest Screen — Vehicle Visualization

**Gate:** TD-ARCHITECTURE (Slice 9b post-ship designer change)
**Date:** 2026-06-26
**Context:** Designer wants the Rest picker to show the player vehicle full-screen with per-subsystem HP bars and click-to-repair on the parts themselves, replacing the current button-list overlay.

## TD Verdict

**TD-ARCHITECTURE: APPROVE (with constraints)**

### Recommendation

**Vehicle rendering: Option (1a) re-pose the existing PlayerVehicle in the scene.**
**Click targets: re-use the existing `VehiclePartHitZone` components on the vehicle.**
**HP bars: re-use the existing `VehicleBarStack` MainBar + per-slot `SubsystemBar` widgets on the player side, bound from the picker controller instead of CombatHud.**
**Stack: stay on UI Toolkit for the scrim + title + dismiss; the vehicle and its bars stay world-space + UGUI exactly as they are in combat. ADR-0014 hybrid axis already permits this.**

### Why this is the right path

1. **Single canonical vehicle.** `PlayerVehicle.prefab` already lives in `Combat.prefab` under `LaneAxis` as a persistent scene object. Run-mode runs on top of CombatScene (per `combat_scene_architecture` memory) — the vehicle GameObject is present and built (`RunSceneHost.BeginNewRun` instantiates it through `BuildVehicle`) when a Rest beacon arrives. Re-instantiating (1b) or render-texturing (1c) would duplicate or freeze a model that already exists. **(1a) has zero authoring cost: the prefab is the prefab.**

2. **Hit zones already exist as the right primitive.** `VehiclePartHitZone` was built (W7.27) to be designer-positioned, alpha-masked, silhouette-outlined click targets, one per `SlotId`, with `IPointerClickHandler` already wired and an `OnClicked` event already fired on left click. In combat they route through `VehicleBarStack` to select a target; for Rest we route through `RestPickerViewModel.Commit(slotId)`. **No new clickable-part system is needed.** Designer tunes part-click rects in Prefab Mode — same workflow as combat.

3. **HP bars are not on the vehicle prefab — they're on a sibling `VehicleBarStack` canvas under `LaneAxis`.** That's a critical fact the brief got 50% right: the bars *exist* in the scene (authored as part of Combat.prefab), but they only render when `BindForCombat(controller, targetGetter, visual, tooltip)` is called. CombatHud is the binder today. **For Rest we ship a sibling no-controller bind path** on `VehicleBarStack` — `BindForRest(Func<Vehicle> targetGetter, VehicleVisual visual)` — that builds the same widget set in `DamagedOnly` visibility mode, without the attack-state UI, drag-cast hover, or BuffStrip. Player-side stack only; enemy stack stays hidden.

   This is *not* an ADR-0011 bridge — Rest and Combat are different consumers of the same canonical bar stack, sibling shapes per ADR-0013 composition pattern. The combat-only state (controller, attack hover, BuffStrip) becomes nullable inputs at the bind seam, not bimodal logic.

4. **ADR-0014 stays clean.** Axis-aligned hybrid (world-space UGUI vehicle + UI Toolkit scrim/chrome) is the exact carve-out ADR-0014 §"UGUI for world-space popups only" already authorized. The Rest picker's chrome (title "REPAIR ONE SUBSYSTEM", dismiss button, future tooltip) stays in `RestPicker.uxml`. The vehicle + bars stay world-space UGUI. **No ADR-0014 amendment required**; this is the second world-space-UGUI consumer, exactly the kind of growth the ADR anticipated.

5. **Active-scene state is already in our favor.** `RunSceneOverlayHost.HandleBeaconChanged` already toggles `_restPicker.Show()` when `BeaconType.Rest && !IsResolved`. Combat HUD is already off (`RunOverlayEvents.RaiseOverlayShown()` was fired when the map appeared). The player vehicle is already parked at its chase-rail pose. **The picker just needs to: (a) push the UI Toolkit scrim over the scene, (b) re-pose the vehicle to screen-center for the duration of the pick, (c) bind the player-side `VehicleBarStack` in `Rest` mode, (d) subscribe to each `VehiclePartHitZone.OnClicked` to invoke `_vm.Commit(slotId)`.** On Hide, restore the chase-rail pose and clear subscriptions.

### Constraints

- **C1 — `VehicleBarStack.BindForRest` is a new sibling bind path, not a flag on `BindForCombat`.** Per ADR-0011 #3 (no bimodal paths), do not pass `controller: null` to the existing method and special-case it. Add a new public verb with its own narrow inputs. Reuse `TryBuildCombatWidgets` internals by extracting the widget-build into a controller-free private helper that both verbs call.
- **C2 — Re-poser is a new component on the vehicle**, e.g. `VehicleRestPose`: takes a target screen-space center (or world position) on `Show`, lerps the vehicle and shadow to it, restores on `Hide`. Keep the lerp data-driven (`RestPoseSO` or serialized fields on the component). Do **not** re-instantiate; do **not** move the LaneAxis (chase-rail follower math will drag the BarStack with it — that's actually what we want, but verify).
- **C3 — Designer can author Rest layout in Prefab Mode without code.** The picker reveals an existing vehicle at a designer-tuned position; the bars project against the existing chassis bounds; hit zones are already designer-tuned per slot. Tuning the rest pose, scrim opacity, title text, dismiss button position is all done in Prefab Mode + UXML/USS — no recompile.
- **C4 — Armor is excluded from candidates.** `RestPickerViewModel.Candidates` already filters via `Vehicle.GetDamagedSlots` — verify (and test) that `GetDamagedSlots` excludes `SlotKind.Armor` per `project_armor_not_subsystem` memory. If it doesn't today, fix it there, not in the picker.
- **C5 — Hit-zone click subscriptions are Bind/OnDestroy-paired per `feedback_subscription_lifecycle_pairing`.** Picker `Show` adds the subscriptions, `Hide` removes them. Crash-resume reopens the picker (beacon-state driven per Slice 9b), which triggers Show again — re-subscribes cleanly.
- **C6 — Empty-list path stays unchanged.** When `_vm.IsEmpty`, the vehicle still renders but no hit zone is interactable (call `SetInteractable(false)` on every zone). The "Nothing to repair" + Continue dismiss path in UXML remains the resolution.
- **C7 — No render-texture path.** Option (1c) was tempting for isolation but adds a camera + RT + extra blit + sync-with-state-changes burden for zero gameplay gain. Reject.

### What we sacrifice / risk

- **R1 — Player-side `VehicleBarStack` visibility mode needs a `Rest` flavor.** `HideOnDestroyed`/`HideOnFullOrDestroyed`/`HideOnFullUnlessAttackActive` don't cover "show every damaged bar prominently, full-HP slots hidden, no attack-drag preview." Add `RestPickShown` HideRule or reuse `HideOnFullOrDestroyed` (the player-side DamagedOnly mode already does what we want). **Likely no new rule — reuse DamagedOnly mode.**
- **R2 — Chase-rail pose restore on Hide.** If the player resumes mid-pick, vehicle must come back to its world position cleanly. `VehicleRestPose` stores the original transform on Show and restores on Hide; idempotent.
- **R3 — Hit-zone outline color collision.** Combat uses yellow outline on drag-cast hover. Rest could reuse it for "hover-to-repair" feedback — same affordance, different verb. Acceptable; designer can override per-mode if it reads wrong.

### Validation criteria — "we'll know this was right if"

- Designer can move the rest pose, scrim alpha, title typography, and dismiss-button placement entirely in the editor, no recompile.
- The player recognizes their vehicle (visual parity = same prefab, same parts, same paint).
- Picking a part from the rest screen produces the same `RunSession.ResolveRest(slotId)` call as today's button list (no model-side change).
- Adding a new slot kind in the future (e.g. biome 2's `Trailer` slot) automatically appears in both combat targeting and rest repair, because both paths read the same authored zones / slot list.

### Out of scope (do not pull in)

- Replacing the dismiss button with a "skip rest" affordance — keep the empty-list path identical.
- Animating damaged parts (smoke, sparks) on the rest screen — defer to a polish slice if desired.
- Tooltip on hover ("Engine — Repair 12 → 24 HP") — nice-to-have, design it but don't gate the slice on it.

### Implementation phase order (for lead-programmer)

1. **Phase 1 — `VehicleBarStack.BindForRest`** sibling verb + extract shared widget-build helper. Tests: `VehicleBarStack_BindForRest_buildsBarsFromLayout_test`. (Pure model-side; no scene wire yet.)
2. **Phase 2 — `VehicleRestPose`** component on PlayerVehicle.prefab (authored, default off). Tests: `VehicleRestPose_show_restoresOnHide_test`.
3. **Phase 3 — `RestPickerController` rewrite.** Drop button-row construction; wire hit-zone subscriptions in `Show`, unwire in `Hide`. Call `_playerBarStack.BindForRest()` + `_vehicleRestPose.Show()` on Show. Tests: EditMode harness that fakes RunSession + asserts a hit-zone OnClicked routes through to `RunSession.ResolveRest(slotId)`.
4. **Phase 4 — UXML/USS update**: remove `#candidate-list`, keep `#empty-state` + `#dismiss-button`, add a "REPAIR ONE SUBSYSTEM" subtitle. Scrim opacity becomes designer-tunable.
5. **Phase 5 — `CombatPrefabAuthor` author path:** wire the player-side `VehicleBarStack` ref into `RestPickerController`, wire `VehicleRestPose` ref, drop the now-unused candidate-list construction from any author paths.

This is approve-with-constraints because **the canonical pieces all exist**; we're composing them through a new sibling consumer, not building new infrastructure. Estimated ~250-400 LOC across the 5 phases (mostly the new `BindForRest` extraction and the rest-pose component). Approval contingent on:

- Phase 1 lands as a sibling bind verb, not a flag.
- Phase 3's subscription lifecycle is Bind/OnDestroy-paired (memory: `feedback_subscription_lifecycle_pairing`).
- Capture-before-destroy fires on the UXML rewrite — list rows are about to go, capture the pre-rewrite UXML + controller into `production/polish-captures/2026-06-26-rest-picker-vehicle-viz.md` first.
- EditMode tests stay green at every phase boundary (memory: `feedback_gate_check_requires_green_tests`).

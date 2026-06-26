# Rest Picker — Vehicle Visualization Rewrite

**Date:** 2026-06-26
**System:** Slice 9b Rest encounter picker — controller + UXML + USS
**Trigger:** Same-day post-ship designer ask: replace the candidate-list-of-buttons picker with a full-screen vehicle visualization where the player clicks parts on the rendered vehicle to repair. TD verdict obtained pre-slice — APPROVE with constraints; re-use the existing `PlayerVehicle.prefab` already in the scene, the existing `VehiclePartHitZone` click components, and the existing `VehicleBarStack` (via a new sibling `BindForRest` verb). Full verdict at `production/td-verdicts/2026-06-26-rest-screen-vehicle-visualization.md` and embedded below under `## Technical Director Review`.

---

## The Final-Game Picture This Slice Serves

The Rest encounter is the player's moment to look at their vehicle and decide what hurts most. A button list reads like an inventory menu; the rendered vehicle reads like *their* vehicle in *its* current state. Slice 9b shipped the model-side correctness (RestPickerViewModel + RunSession.ResolveRest) — this rewrite ships the affordance the model deserves.

Pattern extends: future Merchant, Event, Chopshop screens will follow the same "full-screen scrim over the persistent CombatScene, designer-tuned pose, hit-zones for interaction" shape established here.

---

## What's Being Destroyed (Capture Surface)

### `RestPicker.uxml` — `#candidate-list` element (line 22)

```xml
<ui:VisualElement name="candidate-list" class="wr-rest-list" />
```

The container that today hosts runtime-projected `Button` rows. Going away — vehicle hit-zones replace it.

### `RestPicker.uss` — three rules tied to the list path (lines 67-99)

```css
.wr-rest-list {
    width: 560px;
    flex-direction: column;
    align-items: stretch;
    justify-content: flex-start;
    margin-bottom: 32px;
}

.wr-rest-row {
    height: 56px;
    margin-bottom: 12px;
    padding-left: 24px;
    padding-right: 24px;
    font-size: 22px;
    -unity-font-style: bold;
    -unity-text-align: middle-left;
    background-color: var(--wr-rest-row-bg);
    color: var(--wr-rest-row-text);
    border-left-width: 0;
    border-right-width: 0;
    border-top-width: 0;
    border-bottom-width: 0;
    border-top-left-radius: var(--wr-radius-md);
    border-top-right-radius: var(--wr-radius-md);
    border-bottom-left-radius: var(--wr-radius-md);
    border-bottom-right-radius: var(--wr-radius-md);
}

.wr-rest-row:hover {
    background-color: var(--wr-rest-row-bg-hover);
}
```

Plus the three `:root` design tokens that only fed these rules:

```css
--wr-rest-row-bg:       rgba(36, 36, 38, 0.92);
--wr-rest-row-bg-hover: rgba(56, 56, 60, 0.95);
--wr-rest-row-text:     #EBEBE0;
```

### `RestPickerController.cs` — three pieces tied to the list path

**1. `_candidateList` field (line 44):**
```csharp
private VisualElement _candidateList;
```

**2. `OnEnable` Q-lookup + null-guard fragment (lines 124, 128-129):**
```csharp
_candidateList = docRoot.Q<VisualElement>("candidate-list");
// included in the multi-element null check
if (_root == null || _candidateList == null || ...)
```

**3. `RebuildCandidateList()` method (lines 151-178) — entire body:**
```csharp
private void RebuildCandidateList()
{
    _candidateList.Clear();

    if (_vm.IsEmpty)
    {
        _emptyState.AddToClassList("is-visible");
        _dismissButton.AddToClassList("is-visible");
        return;
    }

    _emptyState.RemoveFromClassList("is-visible");
    _dismissButton.RemoveFromClassList("is-visible");

    var candidates = _vm.Candidates;
    for (int i = 0; i < candidates.Count; i++)
    {
        var row = candidates[i];
        string label = $"{row.DisplayName}: {row.Hp}/{row.MaxHp}  (+{row.DamageDelta} HP)";
        string capturedSlotId = row.SlotId;
        Button btn = new Button(() => OnCandidatePicked(capturedSlotId))
        {
            text = label,
        };
        btn.AddToClassList("wr-rest-row");
        _candidateList.Add(btn);
    }
}
```

The button-construction path is replaced by hit-zone subscription in Phase 3. `OnCandidatePicked(string slotId)` is preserved verbatim — same `_vm.Commit(slotId) → Hide() → OnRestResolved?.Invoke()` shape — only the call site changes (from row button to hit-zone event).

### Not destroyed (preserved verbatim)

- `_root` / `_emptyState` / `_dismissButton` Q-lookups and null-guards
- `OnDismissClicked` empty-list path and its event subscription pair
- `Show` / `Hide` `gameObject.SetActive` discipline (today's change, still load-bearing)
- `Bind(Func<RunSession>)` session getter
- `OnRestResolved` outgoing event + `RunSceneOverlayHost.HandleRestResolved` → `RunSceneHost.NotifyRestResolved` chain
- `RestPickerViewModel.Candidates` + `IsEmpty` + `Commit` + `Dismiss` — model surface untouched
- UXML scrim, title, subtitle, empty-state, dismiss-button structure (subtitle text retunes in Phase 4 to "REPAIR ONE SUBSYSTEM" if not already)
- Scrim color tokens `--wr-rest-scrim`, `--wr-rest-title`, `--wr-rest-subtitle`, `--wr-rest-empty-text`, `--wr-rest-dismiss-bg`, `--wr-rest-dismiss-text`

---

## What Ships (Slice Contents — 5 Phases, Single Commit Each)

### Phase 1 — `VehicleBarStack.BindForRest` sibling verb

New public verb on `VehicleBarStack`:
```csharp
public void BindForRest(Func<Vehicle> targetGetter, VehicleVisual visual);
```
Builds the same bar widget set as `BindForCombat` but with no controller, no attack-state UI, no drag-cast hover, no BuffStrip. Player-side only (enemy stack stays unbound). Internal `TryBuildCombatWidgets` is refactored to a controller-free helper both verbs share. Visibility mode defaults to `DamagedOnly` so full-HP slots stay hidden.

EditMode test: `VehicleBarStack_BindForRest_buildsBarsFromLayout_test` — asserts bars build from FrameLayoutSO without combat state.

### Phase 2 — `VehicleRestPose` component on `PlayerVehicle.prefab`

New MonoBehaviour:
```csharp
public sealed class VehicleRestPose : MonoBehaviour
{
    [SerializeField] private Vector3 _restScreenCenter;
    [SerializeField] private float    _lerpSeconds;

    public void Show();  // captures original transform, lerps to _restScreenCenter
    public void Hide();  // restores captured transform
}
```
Idempotent on repeat `Show`/`Hide` so resume-mid-pick replay is safe. Authored values stay in Prefab Mode (designer-tunable).

EditMode test: `VehicleRestPose_show_restoresOnHide_test`.

### Phase 3 — `RestPickerController` rewrite

- Drop `_candidateList`, `RebuildCandidateList`, and the `OnEnable` Q-lookup for `#candidate-list`.
- Add SerializeFields: `_playerBarStack` (`VehicleBarStack`), `_vehicleRestPose` (`VehicleRestPose`), `_playerVehicleGetter` seam.
- `Show()` after SetActive + VM build: subscribe each `VehiclePartHitZone.OnClicked` → `OnCandidatePicked(slotId)`; call `_playerBarStack.BindForRest(...)`; call `_vehicleRestPose.Show()`.
- `Hide()`: unsubscribe hit-zone handlers; call `_vehicleRestPose.Hide()`; clear bar binding.
- Subscription lifecycle pairs Bind/OnDestroy (memory: `feedback_subscription_lifecycle_pairing`).
- Empty-list path: vehicle still renders, but `SetInteractable(false)` on every hit zone; #empty-state + #dismiss-button show via existing `.is-visible` flow.

EditMode test: harness fakes RunSession, fires a hit-zone OnClicked, asserts `RunSession.ResolveRest(slotId)` was called.

### Phase 4 — `RestPicker.uxml` / `.uss` update

- UXML: remove `#candidate-list` element; subtitle text retunes to "REPAIR ONE SUBSYSTEM" if not already.
- USS: remove `.wr-rest-list`, `.wr-rest-row`, `.wr-rest-row:hover` selectors and the three `--wr-rest-row-*` tokens. Add designer-tunable scrim opacity via a single token if it's not already separated.

### Phase 5 — `CombatPrefabAuthor` author path

In `AuthorRun()` extension after RestPicker child creation:
- `restSo.FindProperty("_playerBarStack").objectReferenceValue = playerBarStack;`
- `restSo.FindProperty("_vehicleRestPose").objectReferenceValue = vehicleRestPose;`
- `restSo.FindProperty("_playerVehicleGetter").objectReferenceValue = ...;` (or expose via Bind from RunSceneOverlayHost)

Update PlayMode `RunPrefabAuthoring_Test` if new components warrant pinning.

---

## Authored-Value Audit (Capture Discipline)

Pre-rewrite authored values being deleted:
- 3 USS selectors (`.wr-rest-list`, `.wr-rest-row`, `.wr-rest-row:hover`) — all spacing/typography defaults, no designer-tuned values yet
- 3 design tokens (`--wr-rest-row-bg`, `--wr-rest-row-bg-hover`, `--wr-rest-row-text`) — RGB authored values pasted above verbatim for re-resurrection if needed
- 1 UXML element (`#candidate-list`) — no authored attributes beyond name + class
- 1 controller method body (`RebuildCandidateList`) — no designer-tuned constants; label format string `"{name}: {hp}/{max} (+{n} HP)"` lives only here

No prefab YAML touched; no FrameLayoutSO touched; no enemy authoring touched. ADR-0011 clean — list-path code deleted outright, not bridged.

---

## Technical Director Review

The full TD verdict for this slice lives at `production/td-verdicts/2026-06-26-rest-screen-vehicle-visualization.md` and is reproduced here:

**TD-ARCHITECTURE: APPROVE (with constraints)**

### Recommendation

- Vehicle rendering: Option (1a) re-pose the existing PlayerVehicle in the scene.
- Click targets: re-use the existing `VehiclePartHitZone` components on the vehicle.
- HP bars: re-use the existing `VehicleBarStack` MainBar + per-slot `SubsystemBar` widgets on the player side, bound from the picker controller instead of CombatHud.
- Stack: stay on UI Toolkit for the scrim + title + dismiss; the vehicle and its bars stay world-space + UGUI exactly as they are in combat. ADR-0014 hybrid axis already permits this.

### Why this is the right path

1. **Single canonical vehicle.** `PlayerVehicle.prefab` already lives in `Combat.prefab` under `LaneAxis` as a persistent scene object. Run-mode runs on top of CombatScene — the vehicle GameObject is present and built when a Rest beacon arrives. **(1a) has zero authoring cost: the prefab is the prefab.**

2. **Hit zones already exist as the right primitive.** `VehiclePartHitZone` was built (W7.27) to be designer-positioned, alpha-masked, silhouette-outlined click targets, one per `SlotId`, with `IPointerClickHandler` already wired and an `OnClicked` event already fired on left click. **No new clickable-part system is needed.**

3. **HP bars are not on the vehicle prefab — they're on a sibling `VehicleBarStack` canvas under `LaneAxis`.** That's a critical fact: the bars exist in the scene but only render when `BindForCombat(...)` is called. For Rest we ship a sibling no-controller bind path on `VehicleBarStack` — `BindForRest(Func<Vehicle> targetGetter, VehicleVisual visual)` — that builds the same widget set in `DamagedOnly` mode, without the attack-state UI, drag-cast hover, or BuffStrip. Sibling shapes per ADR-0013 composition pattern, not an ADR-0011 bridge.

4. **ADR-0014 stays clean.** Axis-aligned hybrid (world-space UGUI vehicle + UI Toolkit scrim/chrome) is the exact carve-out already authorized. Second world-space-UGUI consumer, exactly the kind of growth the ADR anticipated.

5. **Active-scene state is already in our favor.** Combat HUD is already off when overlays are up; player vehicle is already parked. Picker just needs to: push the UI Toolkit scrim, re-pose vehicle to screen-center, bind `VehicleBarStack` in Rest mode, subscribe to each `VehiclePartHitZone.OnClicked` to invoke `_vm.Commit(slotId)`.

### Constraints

- **C1** — `VehicleBarStack.BindForRest` is a new sibling bind path, NOT a flag on `BindForCombat`. Per ADR-0011 #3 (no bimodal paths). Reuse `TryBuildCombatWidgets` internals by extracting widget-build into a controller-free helper both verbs call.
- **C2** — Re-poser is a new component on the vehicle (`VehicleRestPose`): takes a target screen-space center on `Show`, lerps the vehicle and shadow, restores on `Hide`. Data-driven (`RestPoseSO` or serialized fields). Do NOT re-instantiate; do NOT move the LaneAxis (chase-rail follower math will drag the BarStack — actually what we want, but verify).
- **C3** — Designer can author Rest layout in Prefab Mode without code. Picker reveals existing vehicle at designer-tuned position; bars project against existing chassis bounds; hit zones already designer-tuned per slot.
- **C4** — Armor excluded from candidates. `RestPickerViewModel.Candidates` already filters via `Vehicle.GetDamagedSlots` — verify (and test) that `GetDamagedSlots` excludes `SlotKind.Armor` per `project_armor_not_subsystem`. If not today, fix it there, not in the picker.
- **C5** — Hit-zone click subscriptions are Bind/OnDestroy-paired per `feedback_subscription_lifecycle_pairing`. Picker `Show` adds subscriptions, `Hide` removes them.
- **C6** — Empty-list path unchanged. When `_vm.IsEmpty`, vehicle renders but no hit zone interactable. "Nothing to repair" + Continue dismiss path in UXML remains.
- **C7** — No render-texture path. Option (1c) rejected: adds camera + RT + extra blit + sync burden for zero gameplay gain.

### Risks acknowledged

- **R1** — Player-side `VehicleBarStack` likely already has a `DamagedOnly` visibility mode that does what Rest wants. **Likely no new HideRule needed.**
- **R2** — Chase-rail pose restore on Hide: `VehicleRestPose` stores original transform on Show, restores on Hide; idempotent.
- **R3** — Hit-zone outline color collision with combat drag-cast hover. Acceptable; designer can override per-mode if it reads wrong.

### Validation criteria

- Designer can move the rest pose, scrim alpha, title typography, dismiss-button placement in the editor, no recompile.
- Player recognizes their vehicle (same prefab, same parts, same paint).
- Picking a part produces the same `RunSession.ResolveRest(slotId)` call as today's button list (no model-side change).
- Adding a new slot kind in the future automatically appears in both combat targeting and rest repair, because both paths read the same authored zones / slot list.

### Out of scope

- Replacing dismiss button with a "skip rest" affordance — keep empty-list path identical.
- Animating damaged parts (smoke, sparks) on rest screen — defer.
- Tooltip on hover — design it but don't gate the slice on it.

### Implementation phase order (matches commit cadence)

1. `VehicleBarStack.BindForRest` sibling verb + extract shared widget-build helper.
2. `VehicleRestPose` component on PlayerVehicle.prefab.
3. `RestPickerController` rewrite.
4. UXML/USS update.
5. `CombatPrefabAuthor` author path wire-up.

Approval contingent on: Phase 1 lands as sibling bind verb not a flag; Phase 3 subscription lifecycle Bind/OnDestroy-paired; capture-before-destroy fires on UXML rewrite (this file); EditMode tests stay green at every phase boundary.

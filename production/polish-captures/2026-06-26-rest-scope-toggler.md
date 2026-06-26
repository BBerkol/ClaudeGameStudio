# 2026-06-26 — RestSceneBackdrop → RestScopeToggler rename + scope-widening

Slice 9b second-pass closeout. First PlayMode trial after Phase 2a + 5a + 5b shipped surfaced five gaps in the rest-screen rendering. This capture enumerates the destroyed values + bakes in the TD verdict before any code lands. Approved-then-edit per `production/polish-captures/README.md` (Capture-Before-Destroy protocol enforced by hook).

## Trigger (PlayMode observation)

User landed on a full-HP Rest beacon mid-run vs The Dredge encounter. Picker drew over the scene with the empty-state "Nothing to repair. / CONTINUE" surfaced. Backdrop swap worked (chase rail / sky / mountains / ground hidden). Player vehicle re-posed to center via 0.5s lerp. But:

1. **Combat HUD top-left still visible** — player HP plate (20/20) + main bar (55/55) drawing at combat position.
2. **Enemy intent HUD top-right still visible** — "The Dredge / 10 ATTACK / 0/8 / 0/80".
3. **Enemy vehicle still drawing** in its combat lane position.
4. **Vehicle lerps in over 0.5s** — user wants snap, vehicle "already in middle from the start."
5. **Per-slot HP bars NOT visible on the rest-posed vehicle** — `BindForRest` sets `VisibilityMode.DamagedOnly` → at full HP every bar self-hides via `HideOnFullOrDestroyed`. User wants them on regardless so the read is "yes all good, nothing to repair" from the vehicle itself.
6. **Console warning**: `[HandBeat] DiscardBurst: no widget for card 'FlameBurst' (model idx 0) — event dropped, card will not animate to discard` — combat hand still running coroutines during Rest.

## Values being destroyed

### `Assets/Scripts/CombatView/RestSceneBackdrop.cs` (file rename)

The file moves to `RestScopeToggler.cs`. `.meta` follows via `git mv` so GUID `e2c5a9d7f1b34e8a96d2c5b7a3e1d8c4` is preserved — the Combat.prefab nested `m_Script` ref at line 292 (`m_EditorClassIdentifier: WastelandRun.CombatView::WastelandRun.CombatView.RestSceneBackdrop`) keeps resolving. Inside the file the namespace stays `WastelandRun.CombatView` but the class name + `m_EditorClassIdentifier` change. After re-import Unity rewrites the prefab line to the new identifier automatically on first asset import; no manual prefab edit needed.

The component's existing authored values (Combat.prefab YAML lines 283–296) are preserved:

| Field | Current value | Post-rename |
|---|---|---|
| `_combatVisualsRoot` | `{fileID: 2252930377151266023}` → SceneVisuals.prefab stripped instance | unchanged |
| `_backdropRenderer` | `{fileID: 5647122311691776511}` → RestVisuals/Backdrop SR (32×18 dark warm-grey quad, sortingOrder -90, enabled=false at author) | unchanged |

Three new SerializeFields land at the same level. Defaults are null — `AuthorCombatScene` wires scene-instance refs (cross-prefab, same pattern as Phase 5b):

| New field | Resolves at scene-author to |
|---|---|
| `_enemyVehicle` | `combatInstance.transform.Find("LaneAxis/EnemyVehicle")?.gameObject` |
| `_combatHudCanvas` | `combatInstance.transform.Find("HUD/Combat_HUD")?.gameObject` (sortingOrder 10 canvas — silencing this also halts HandBeat-driven coroutines whose runners are children of this canvas, per TD verdict) |
| `_enemyBarStackCanvas` | `combatInstance.transform.Find("LaneAxis/EnemyBarStackCanvas")?.gameObject` (confirmed direct sibling of LaneAxis at Combat.prefab line 15) |

Total: 5 SerializeFields (was 2). Each captures pre-rest active-state on first `Show()` call and restores on `Hide()` — same `_captured` gate that exists today.

### `Assets/Scripts/CombatView/VehicleRestPose.cs`

| Field | Current default | Post-fix |
|---|---|---|
| `_lerpDurationSec` | `0.50f` | `0f` |

The existing `Update()` body (line 184) already snaps cleanly at duration zero: `float t = _lerpDurationSec > 0f ? Mathf.Clamp01(elapsed / _lerpDurationSec) : 1f`. No API surface change. No optional `snap:bool` arg (would be the ADR-0011 #5 compat-overload trap). Designer can still tune to a positive value in Prefab Mode if a future call site wants the cinematic 0.5s settle.

The Range attribute on the field is `Range(0.05f, 2.0f)`. We loosen the lower bound to `0f` to allow the new default + designer-set snap.

### `Assets/Scripts/CombatView/SubsystemBar.cs`

Add one enum case to `HideRule`:

| Enum cases (current) | Post-fix |
|---|---|
| `HideOnDestroyed, HideOnFullOrDestroyed, AlwaysHidden, HideOnFullUnlessAttackActive` | `… , AlwaysVisible` |

`ShouldBeVisible()` (line 224 switch) gets a new branch returning `true` unconditionally. No other behavior change.

### `Assets/Scripts/CombatView/VehicleBarStack.cs`

`BindForRest` (line 244) currently does:

```csharp
_visibilityMode = VisibilityMode.DamagedOnly;
```

Post-fix: that line is **deleted**. The default `_visibilityMode = VisibilityMode.AttackStateGated` is what BindForRest gets (keeps parent SetActive of markers + bars both on, which is correct for the rest screen — user wants HP bars + sub bars visible).

Then `TryBuildRestWidgets` (called at end of BindForRest) walks the spawned bars and overrides each one's hide rule:

```csharp
for (int i = 0; i < _runtimeBars.Count; i++)
    if (_runtimeBars[i] != null) _runtimeBars[i].SetHideRule(SubsystemBar.HideRule.AlwaysVisible);
```

`ResolveHideRule()` (line 827) is untouched — it's used by `BindForCombat` and stays correct. The rest path overrides per-bar after the shared `BuildPerSlotBars` returns; that's where the design decision "rest shows everything" lives. The `_visibilityMode` enum stays at 2 cases — no `RestMode` enum value, per TD verdict (the parent toggle and the per-widget rule are separate axes; this slice changes the per-widget rule only).

### `Assets/Editor/CombatPrefabAuthor.cs`

Two edits inside `WireRestPickerCrossPrefab` (lines 7598–7639):

1. After the existing 4 transform `Find` calls, add 3 more lookups for `EnemyVehicle`, `Combat_HUD`, `EnemyBarStackCanvas`. Same fail-loud LogError pattern if any are missing.
2. After the existing `pickerSo` SerializedObject block, add a second SerializedObject block targeting the **toggler component on RestVisuals** (already wired by Phase 5a — looked up via `restVisualsTr.GetComponent<RestScopeToggler>()` after the rename). Three `objectReferenceValue` writes for the new refs; `ApplyModifiedPropertiesWithoutUndo`.

No new SerializedObject call site is needed for `VehicleRestPose._lerpDurationSec` — the field default change picks up automatically when `AuthorPlayerVehicle` re-runs (`BuildVehicleScaffold` AddComponent at line 4397 uses default initialization). Same for `SubsystemBar.HideRule.AlwaysVisible` — no field default change, just a new enum case that BindForRest invokes at runtime.

## Files NOT touched (intentionally)

- `RestPickerController.cs` — no signature change. The picker still calls `_restSceneBackdrop.Show()`; the field rename to `_restScopeToggler` (and tooltip update) is the only edit, but the call site stays identical.
- `Combat.prefab` — no destructive YAML edit. Unity rewrites the `m_EditorClassIdentifier` on first re-import after the rename. AuthorCombatScene re-runs the wire-up at scene-instance level (where the 3 new refs land); the prefab itself only carries the existing 2 refs to SceneVisuals + Backdrop SR.
- `Run.prefab` — no edit. Cross-prefab refs always live at scene-instance level per Workstream F.

## Re-author sequence after edits land

Same 5-step sequence as Phase 5b:

1. Author PlayerVehicle Prefab — picks up new `VehicleRestPose._lerpDurationSec = 0f` default
2. Author Enemy Archetype Prefabs — same default propagates to enemy scaffolds (inert, but symmetric)
3. Author Combat Prefab — RestScopeToggler component re-bakes via the renamed type; designer values on the 2 existing refs preserved via the `_combatVisualsRoot` / `_backdropRenderer` re-assign block (mirrors Phase 5a)
4. Author Run Prefab — no change expected
5. Author Combat Scene — `WireRestPickerCrossPrefab` writes all 4 RestPickerController refs (Phase 5b) **and** 3 new RestScopeToggler refs (this slice)

## Risks called out

- **Enemy bar stack canvas (PlayerBarStackCanvas/EnemyBarStackCanvas)**: confirmed both are direct sibling children of Combat.prefab (lines 15 + 93). EnemyBarStackCanvas is in the toggler's new ref list. PlayerBarStackCanvas stays on (we want the rest-screen bars to render on it).
- **Enemy VehicleRestPose**: per Phase 2a fix, every vehicle scaffold (including enemies) has `VehicleRestPose`. RestPickerController only ever calls `Show()` on the player's; the enemy's `Show()` is never invoked. SetActive(false) on the enemy vehicle during Show + restore on Hide is independent of the enemy's RestPose component (the component's `_shown` flag stays false throughout). TD flagged this as a manual-check item — worth a one-pass eyeball of the enemy in the next combat after a rest, but unlikely to misbehave.
- **HandBeat coroutine halt**: TD confirmed HandBeat is a static class invoked by HandSequencer/HandHud whose coroutines live on children of Combat_HUD canvas. SetActive(false) on Combat_HUD halts those coroutines automatically (Unity standard behavior). The console warning observed today is a one-shot drain of an event that fired before the canvas went inactive — not a steady leak. No separate fix.
- **Bind/Unbind pairing**: `BindForRest` / `UnbindRest` is a Bind-pair (lifetime = picker Show/Hide), already correct per memory `feedback_subscription_lifecycle_pairing`. Unchanged.

## Technical Director Review

(Verdict from 2026-06-26 TD consultation — see full prompt + response in conversation log.)

> **TD-CHANGE-IMPACT: APPROVE (Option A, with two corrections)**
>
> Proceed with Option A — extend `RestSceneBackdrop` into a single rest-scope toggler. Reject B (premature centralization, multiplies seams) and C (reparenting fights live combat-model ownership; backdrop-sibling subtree already exists but the rest-posed vehicle must remain a child of LaneAxis so its bar stack's chassis-UV-to-canvas projection stays valid — moving it would invalidate `_chassisRestMatrix`). Your instinct is correct.
>
> **Corrections:**
> 1. **Rename to `RestScopeToggler`** (or `RestSceneToggle`). Yes — once it owns 4+ refs spanning HUD/vehicle/visuals, "Backdrop" mis-claims its category. ADR-0011 #7 forbids transitional naming comments — do the rename, don't leave a "formerly RestSceneBackdrop" note.
> 2. **No `alwaysVisible:bool` flag on `BindForRest`**: that's the ADR-0011 #5 compat-overload trap. Use the per-bar `HideRule` knob — that's where the "should this bar be on" decision lives anyway. `BindForRest` overrides to `AlwaysVisible`.
>
> **Snap-not-lerp** — set `_lerpDurationSec = 0f` as the default. Existing `Update()` already snaps cleanly at duration zero. No `snap:bool` arg. No other caller, no need for an option.
>
> **`BindForRest`'s `DamagedOnly` was NOT a contract**: the comment on line 56 explicitly says "DamagedOnly — unused at present … Retained for future low-noise display modes." No other consumer depends on the post-rest visibility mode. Free to drop.
>
> **HandBeat leakage — free fix**: HandBeat is a static class invoked by HandSequencer/HandHud coroutines running on the Combat_HUD canvas subtree. Unity halts coroutines when their host GameObject goes inactive. SetActive(false) on Combat_HUD silences them automatically. The console warning was a one-shot drain of a pre-inactive event, not a steady leak.
>
> **Landmines flagged:**
> - `VehicleRestPose` on enemy vehicle: never invoked, harmless. Manual eyeball next combat after a rest just to be safe.
> - `_originalCombatVisualsActive` capture now four captures — same `_captured` gate on first Show only so Show→Hide→Show doesn't drift.
> - `EnemyBarStackCanvas` sibling check — confirmed in this capture (Combat.prefab line 15).
> - Subscription lifecycle pairing already correct.
>
> **Success metrics**: one commit closes all 5 gaps; toggler's SerializeField list reads as a coherent "what is hidden when rest is on" inventory; no new enum value or flag or bridge; HandBeat warning disappears on first PlayMode trial post-fix.

## Sign-off

Awaiting user approval before any code edits land.

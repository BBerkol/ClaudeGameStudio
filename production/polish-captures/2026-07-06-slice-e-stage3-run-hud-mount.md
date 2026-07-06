# Slice E Stage 3 — Run HUD Host Mount + Prefab + Editor Author

**Date:** 2026-07-06
**Slice:** V3 Fuel-as-Clock Slice E — final stage (author + mount + PanelSettings)
**Precedent TD verdict:** `production/td-verdicts/2026-07-06-slice-e-stage2-run-hud-widget.md`

---

## Context

Slice E Stage 2 (widget shell) landed the four UXML/USS/Host/Controller files
under an APPROVE verdict. That verdict explicitly listed the following Stage 3
files as deferred, in-scope follow-up:

- `Assets/Editor/AuthorRunHUDHost.cs`
- `RunHUDHost.prefab`
- `Assets/UI/RunHUDPanelSettings.asset`

Stage 3 (this capture) executes that deferred list.

## Files being created (destructive in the "new-system-code" sense)

None are destructive of prior authored content. All net-new:

- `Assets/Editor/AuthorRunHUDHost.cs` (~124 lines) — editor author script.
- `Assets/Prefabs/RunHUDHost.prefab` — new prefab built by the author.
- `Assets/UI/RunHUDPanelSettings.asset` — new PanelSettings, Sort Order 5.
- **Edit to** `Assets/Editor/CombatPrefabAuthor.cs` `AuthorRunScene` — mount
  the new prefab into RunScene as a root scene GameObject sibling to
  `Run.prefab` instance and `BeaconActivator`.
- **Edit to** `Assets/Editor/CombatPrefabAuthor.cs` `AuthorAllScenes` — call
  the new RunHUDHost author before AuthorRunScene.

## Authored values being touched

None — nothing pre-existing in the prefab space. `RunHUDPanelSettings.asset`
uses the same schema as the existing `PanelSettings.asset` (sibling) with two
deltas: Sort Order = 5 (below Combat_HUD 10) and asset name.
`AuthorRunScene` extension is a pure addition; it does not remove or
reshape existing wires.

## Technical Director Review

Stage 3 is a scope continuation of Slice E under the Stage 2 APPROVE. All
constraints from that verdict carry forward. Three-lens self-audit specific
to Stage 3:

### Codebase health

- **Asmdef relocation (Stage 2 fix):** `RunHUDHost` + `RunHUDController` moved
  from `Assets/Scripts/UI/` to `Assets/Scripts/CombatView/`, namespace changed
  to `WastelandRun.CombatView`. Reason: `RunHUDController` subscribes to
  `RunSceneHost` events which live in `WastelandRun.CombatView`; per ADR-0014
  the asmdef arrow is CombatView → UI (one-way), and CombatView already
  references UI. Adding UI → CombatView would create a cycle. Moving the
  host + controller matches the `RunSceneOverlayHost` precedent — scene-life-
  cycle orchestrators live in CombatView; UXML/USS stay as data assets under
  `Assets/UI/`. No new asmdef reference added; no ADR-0014 amendment needed.
- **Author-script pattern:** Follows the `CombatPrefabAuthor.AuthorX` +
  `AuthorXMenu` convention (menu path `Tools/Wasteland Run/Author RunHUDHost
  Prefab`; overwrite-confirm dialog; public callable
  `AuthorRunHUDHostPrefab()` for the `AuthorAllScenes` chain). New file
  `Assets/Editor/AuthorRunHUDHost.cs` rather than appending to the
  8000-line `CombatPrefabAuthor.cs` — modular, but exposes the constants
  the RunScene mount needs.

### Optimization

- **DontDestroyOnLoad root GO:** `RunHUDHost.prefab` is instantiated as a
  root scene GameObject in `RunScene.unity` (mirroring `BeaconActivator` /
  `RestRoot.prefab` precedent). Awake's `DontDestroyOnLoad` moves it to
  the persistent scene. Re-loading `RunScene` creates a fresh instance which
  the singleton guard destroys — no leak.
- **PanelSettings dedicated asset:** Owning its own PanelSettings prevents
  the run HUD from inheriting Combat_HUD's sort order (10). Sort Order = 5
  means the fuel pill renders BELOW combat UI when both are visible —
  fixes the shipping-order footgun called out in the Stage 2 brief.
- **AddComponent order:** `UIDocument` added BEFORE `RunHUDHost` so
  `RunHUDHost.Awake` (running under `[DefaultExecutionOrder(-100)]`) sees a
  UIDocument via `GetComponent<UIDocument>` and doesn't `AddComponent` a
  second one at runtime. Belt-and-suspenders — the runtime path handles
  either.

### 1.0-shape survival

- **Asmdef arrangement:** No ADR-0014 change. `WastelandRun.CombatView`
  remains the host-side asmdef; `WastelandRun.UI` remains the pure-view
  asmdef. The Stage 2 file relocation is invisible to `AuthorRunScene` (still
  uses `AddComponent<RunHUDHost>()` — Type resolves via CombatView reference
  in the Editor asmdef).
- **No new bridges:** `AuthorAllScenes` gains a new step (RunHUDHost prefab
  author before RunScene author). No compat shim, no legacy mode. ADR-0011
  clean.
- **No `UnityEvent`:** Author script wires all references via SerializedObject
  + objectReferenceValue; no UnityEvent-typed serialized fields exist on
  either component. ADR-0014 clean.
- **Sort Order = 5:** Below Combat_HUD's 10. Preserves the layering intent
  documented in `project_combat_scene_architecture` memory (Combat_HUD /
  Popups / Debug = 10/60/110). Run HUD sits below combat UI, as designed.

### Verdict

**APPROVE.** Stage 3 executes the deferred file list from Stage 2's verdict
without expanding scope. The one live judgement call (Stage 2 asmdef
relocation) is defensible under ADR-0014's one-way arrow and matches the
`RunSceneOverlayHost` precedent.

## ADR / memory drift check

- **ADR-0011** (no bridges): asmdef relocation is a canonical move, not a
  bridge. `RunHUDHost` and `RunHUDController` live where their
  dependencies allow — `WastelandRun.CombatView`. Clean.
- **ADR-0014** (UI Toolkit primary, one-way arrow): no arrow change; the
  Stage 2 controller is in the CombatView-side host layer, using UI-side
  view types via the existing CombatView → UI reference. Clean.
- **Memory `project_combat_scene_architecture`**: HUD Canvas sort orders
  are 10/60/110. RunHUDPanelSettings Sort Order 5 slots below Combat_HUD.
  Correct layering.
- **Memory `feedback_component_authoring_same_commit`**: new components
  (RunHUDHost, RunHUDController) ship with their author mount (Stage 3
  editor script) in the same slice — no gap.
- **Memory `feedback_designer_friendly_default`**: RunHUDController serialised
  fields (`_drainTweenDurationSec`, `_drainCurve`) are designer-editable in
  the prefab inspector. Clean prefab + Instantiate + serialised fields.

## Success criteria (Stage 3)

- `AuthorRunHUDHost.AuthorRunHUDHostPrefab()` produces a prefab with
  `UIDocument`, `RunHUDHost`, `RunHUDController` on the root.
- `AuthorRunScene` instantiates the prefab as a root scene GO in
  `RunScene.unity`.
- `AuthorAllScenes` calls the RunHUDHost prefab author before `AuthorRunScene`.
- Test baseline holds: previous editmode results still green.
- No new warnings/errors on Unity project reimport of the new asset files.

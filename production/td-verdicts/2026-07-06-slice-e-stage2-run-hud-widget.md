# Slice E Stage 2 — Run HUD Widget Shell (UXML + USS + Host + Controller)

**Slice:** V3 Fuel-as-Clock Slice E — Run-scope fuel HUD widget shell
**Date:** 2026-07-06
**Verdict:** APPROVE

---

## Context

Slice D landed the `BeaconTravelTick` seam and `MapViewController.OnBeaconTravelTick`
event. Stage 1 of Slice E extended `BeaconTravelTick` with `PreviewedFuelBefore`/
`PreviewedFuelAfter` fields, added `RunSceneHost.OnRunStarted`/`OnRunEnded` events,
and wired `RunSceneOverlayHost` to pass `fuelBefore` into
`MapViewController.PlayBeaconTravelAnimation`.

Stage 2 (this slice) builds the widget shell that consumes those wires:
- `RunHUD.uxml` — overlay document with fuel pill
- `RunHUD.uss` — styling for pill + drain-pulse modifier
- `RunHUDHost.cs` — persistent singleton MonoBehaviour owning the UIDocument
- `RunHUDController.cs` — MonoBehaviour controller on the same GameObject

Full V3 architecture rationale:
`production/td-verdicts/2026-07-04-v3-fuel-as-clock-architecture.md`

## TD Verdict

**APPROVE.** Three-lens self-audit below.

### Codebase health

- **No `FuelState` change event** — `FuelState` is a pure POCO with no observer
  interface. `HandleFuelChanged` from the original spec is removed. The controller
  snapshots `FuelState.Current` reactively on two stable signals: `OnRunStarted`
  (run just bootstrapped; snapshot fresh max+current) and `BeaconTravelTick.Arrive`
  (model has committed; snap final value). During the 3s travel window the tween
  animates between the Depart preview values — no need to poll in Update. Clean;
  avoids adding a change event to an ADR-0002 POCO.
- **Controller as MonoBehaviour** — co-mounted on the same GameObject as the Host.
  Serialised fields (`_drainTweenDurationSec`, `_drainCurve`) are designer-editable
  in the prefab inspector (Stage 3). POCO alternative would require a config-struct
  passthrough on every Bind — more boilerplate, no benefit at this scale.
- **`DontDestroyOnLoad` singleton** — `RunHUDHost` survives the Run → Combat →
  Run round-trip. `[DefaultExecutionOrder(-100)]` guarantees the Awake guard runs
  before `UIDocument.OnEnable` initialises the panel. Duplicate-instance guard
  destroys the later instance immediately — safe on scene reload.
- **Lazy `FindAnyObjectByType` resolve** — host is persistent, `RunSceneHost` lives
  in the run scene (loaded after host). `TryResolveAndSubscribe` is called in
  `Bind` (initial boot) and in `OnEnable` (scene-reload rescue). Idempotent
  subscribe (unsubscribe-then-subscribe) prevents duplicate handler registration
  on AddressableScene-reload paths.
- **Depart tween snaps `fuelBefore → fuelAfter`** using the preview values carried
  in the tick; `AnimationCurve` + `_drainTweenDurationSec` are the feel knobs.
  On `Arrive`, `SnapshotFuel` reads live `FuelState.Current` (authoritative — handles
  Haven clamp and any other model-side adjustment). Belt-and-suspenders.

### Optimization

- `EnableInClassList` scope is element-local — no full-panel layout recompute.
- Coroutine allocated once per beacon travel (one-shot 0.4s); negligible vs 3s
  travel animation already allocated in `MapViewController`.
- `FindAnyObjectByType` called at most twice per scene load (Bind + OnEnable
  rescue). Not in Update. Acceptable.
- USS drain-pulse uses `background-color` swap only — NEVER `width`, `padding`,
  `margin`, or any layout-affecting property. Layout is stable during animation.

### 1.0-shape survival

- `RunHUDHost` + `RunHUDController` are in `WastelandRun.UI` — correct asmdef
  arrow direction (UI → CombatView one-way, per ADR-0014).
- No `UnityEvent` anywhere — `Action<BeaconTravelTick>`, `Action OnRunStarted`,
  `Action OnRunEnded` (ADR-0014 / ADR-0002).
- `RunHUDController` subscribes to `MapViewController.OnBeaconTravelTick` under
  the `Bind`/`Unbind` (= `OnDestroy`) pattern per memory `uitoolkit_subscription_lifecycle`.
  `RunSceneHost` events subscribed same way. No `OnEnable`/`OnDisable` mispairing.
- `wr-fuel-pill--draining` modifier uses `--wr-color-accent-hover` (`#e89047`) —
  no `--wr-color-warning` token exists in the current token set. Token comment
  in USS records this so the visual-direction pass can retarget cleanly.
- Stage 3 (editor authoring, prefab, PanelSettings.asset, RunSceneHost mount) is
  deferred — correct. Widget shell compiles and is verifiable in isolation.

## Files touched

**New files (Stage 2):**
- `Assets/UI/RunHUD.uxml` — overlay document, fuel pill, mock label text.
- `Assets/UI/RunHUD.uss` — `.wr-run-hud-root`, `.wr-fuel-pill`, `.wr-fuel-pill__label`, `.wr-fuel-pill--draining`.
- `Assets/Scripts/UI/RunHUDHost.cs` — persistent singleton MonoBehaviour. `WastelandRun.UI`.
- `Assets/Scripts/UI/RunHUDController.cs` — MonoBehaviour controller co-mounted on Host. `WastelandRun.UI`.

**Not touched (Stage 3):**
- `Assets/Editor/AuthorRunHUDHost.cs`
- `RunHUDHost.prefab`
- `Assets/UI/RunHUDPanelSettings.asset`
- `Assets/Scripts/CombatView/RunSceneHost.cs`

## ADR / memory drift check

- **ADR-0002** (engine-free Run POCO): no POCO touched. `FuelState` read-only from view layer. Clean.
- **ADR-0011** (no bridges): no bridge or adapter layer. `RunHUDController` is a canonical new subscriber; `Bind`/`Unbind` are not transitional. Clean.
- **ADR-0014** (UI Toolkit primary, no UnityEvent): new screen-space overlay uses UI Toolkit. No `UnityEvent`. Clean.
- **Memory `uitoolkit_subscription_lifecycle`**: external-publisher events (`RunSceneHost`, `MapViewController`) wired in `Bind` + unwired in `Unbind` (called from `OnDestroy`). Not `OnEnable`/`OnDisable` — host is `DontDestroyOnLoad` and never SetActive-toggled. Correct pairing.
- **Memory `subscription_lifecycle_pairing`**: Bind↔Unbind(OnDestroy) for all three event sources. No mismatched pairs.
- **Memory `feedback_td_three_lens_self_audit`**: three-lens self-audit is this section.
- **Memory `overall_picture_thinking`**: fuel pill is the first persistent HUD widget. PanelSettings sort order (Stage 3: below Combat_HUD's 10) places it under combat UI. Correct layering intent documented.

## Success criteria

- Four files compile with zero errors (edit-mode batch run passes).
- Test baseline holds: 800 passed / 0 failed / 1 skipped.
- `RunHUDController.Bind` resolves `RunSceneHost` via `FindAnyObjectByType` on scene load.
- Drain tween animates fuel label from `PreviewedFuelBefore` → `PreviewedFuelAfter` over `_drainTweenDurationSec`.
- `Arrive` tick snaps label to live `FuelState.Current`.
- `wr-fuel-pill--draining` class applied only during tween; removed on complete or `StopDrainTween`.

## Estimated effort

~1 session (pure new files; no existing file modification).

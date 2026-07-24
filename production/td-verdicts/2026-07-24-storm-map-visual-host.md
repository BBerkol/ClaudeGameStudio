# TD Verdict — Storm Map Visual Host

**Date:** 2026-07-24
**System:** Storm engulfment map-view visualisation (out-of-fuel V2 polish)
**Files at risk:** `StormMapVisualHost.cs` (new), `MapViewController.cs`, `StormEngulfmentController.cs`, `StormAdvanceTick.cs`, `RunSceneHost.cs`, `StormCursorTicker.cs`, `StormCursorTicker_Test.cs`, `MapView.uss`, `CombatPrefabAuthor.cs`

## Context

Out-of-fuel V2 ticker shipped this morning (Unity commit `81088de`) fires three
events during the doom coroutine: `OnAutoStormBegan`, `OnStormAdvanced(tick)`,
`OnStormEngulfed`. The visual layer needs to consume these to paint a menacing
red front element on the map that lerps between beacons in phase with the
model cursor, so the user's design intent — "player sees their doom as the map
scene unfolds" — reads as a spectacle, not silence-then-black-screen.

## Proposed shape (as briefed)

1. New MonoBehaviour `StormMapVisualHost` on Run.prefab root (sibling to
   RunSceneHost + StormEngulfmentController) — subscribes to the three storm
   events and calls a small visual API on MapViewController.
2. New public methods on MapViewController: `ShowStormFrontAt(int)`,
   `PlayStormAdvance(int, int, float)`, `HideStormFront()`.
3. New public getter on StormEngulfmentController: `SecondsPerBeacon`.
4. New `.wr-storm-front` USS class in MapView.uss.
5. CombatPrefabAuthor.AuthorRun wires the host + 3 serialized refs.

## Technical Director Review

**Verdict:** APPROVE — Shape "sibling visual host + private lerp helper"

**Core approvals:**
- Coupling shape (Host under CombatView, subscribing to RunSceneHost, calling
  MapViewController) is correct — MapViewController stays engine-independent
  per its charter, subscription glue is the sibling MonoBehaviour's job.
- Bimodal-path smell (player travel vs storm advance) is a **legitimate axis**,
  not drift — semantically different trigger/element/lifecycle/owner. Route
  both through a *private* `LerpElementBetweenBeacons` helper for shared
  arithmetic. Slice F storm-strip counter becomes the third caller with zero
  reshape.

**AMEND 1 — extend `StormAdvanceTick` payload:** Add `float DurationSeconds`
to the readonly struct instead of adding a `SecondsPerBeacon` getter that
subscribers query via a second sibling reference. Recovery-chance events
(confirmed future subscribers) will also want cadence for their roll timing.
Extending a readonly struct is ADR-0011-clean; adding a second sibling wire
is permanent bimodal wiring. Tests updated once, done.

**AMEND 2 — add `OnStormStopped` event on RunSceneHost:** Symmetric with the
Began/Advanced/Engulfed trio. Fires from `StormEngulfmentController.StopTicker`
so `StormMapVisualHost` can call `MapViewController.HideStormFront()`
cleanly. The alternatives (recovery-event system reaching into presentation,
piggybacking OnBeaconChanged) both drift under ADR-0011.

**Three-lens self-audit:**

- **Health:** Subscription lifecycle correct — StormMapVisualHost is a locally
  mounted sibling on Run.prefab root, not a UIDocument child that gets
  SetActive-cycled. `OnEnable`/`OnDisable` is the right pair (per memory
  `feedback_subscription_lifecycle_pairing`). Alt+F4 mid-lerp → coroutine
  dies with the GameObject, cleanly. OnDisable unsubscribes → no delegate
  leak.
- **Optimization:** Tick cadence is 1.5s — coroutine is fine, no per-frame
  allocation concern. Lerp allocates one Time.deltaTime accumulator per frame
  during ~1.5s window — mirrors existing `PlayBeaconTravelAnimation`. Do NOT
  reach for UI Toolkit's `ValueAnimation` unless it becomes the house pattern.
- **1.0 survival:** `StormAdvanceTick.DurationSeconds` survives (recovery-
  chance events need it). `OnStormStopped` survives (canonical recovery
  signal). `.wr-storm-front` USS survives (real state, not stopgap).
  StormMapVisualHost survives as canonical storm-visual owner. Private lerp
  helper survives Slice F storm-strip addition without reshape.

**Unverified assumptions (I could not read the Unity project from my shell —
verify before commit):**
- `StormAdvanceTick` IS a `readonly struct` today ✓ (verified by user in this session — file was written this morning as `public readonly struct`).
- No existing `OnStormStopped` on RunSceneHost ✓ (grep confirmed).
- `EnsurePlayerMarker` pattern exists in MapViewController ✓ (verified in current file).

## Applied AMENDs (implementation notes)

- `StormAdvanceTick` gains `DurationSeconds` field; StormCursorTicker
  constructs with `0f` (it doesn't know cadence); StormEngulfmentController
  wraps at raise time with `_tuning.SecondsPerBeacon`. Tests updated to
  expect zero from ticker directly and non-zero via controller.
- `RunSceneHost` gains `OnStormStopped` event + `RaiseStormStopped()` method.
  `StormEngulfmentController.StopTicker()` invokes it when a coroutine was
  cancelled — silent stops (never-started) are not raised.
- MapViewController extracts `private IEnumerator LerpElementBetweenBeacons(
  VisualElement element, int fromIndex, int toIndex, float durationSeconds,
  Action<VisualElement, Vector2> setPosition)` — used by both
  `PlayStormAdvance` and (retroactively) the existing player-travel path if
  cleanup is trivial. If retrofit is non-trivial, ship as
  storm-only-caller-today with the helper in place; travel path retrofit
  becomes an opportunistic follow-up.
- `StormMapVisualHost` no longer needs the `_stormController` serialized ref
  — duration comes off the tick payload.

## ADR alignment

- ADR-0003 (RNG discipline) — no RNG introduced.
- ADR-0011 (no bridges) — no enum values, no parallel storage, no compat
  overloads, no stubs. Extending readonly struct is exception #1 (additive).
- ADR-0014 (UI Toolkit primary) — new visual is UI Toolkit VisualElement +
  USS class, correct stack.
- ADR-0015 (data-flag lagging-dep) — StormEngulfmentSO tuning value flows
  into the payload, so if the SO field is retuned, the visual updates
  automatically.

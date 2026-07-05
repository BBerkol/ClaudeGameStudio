# Slice D — Map Travel Animation Seam — QA Evidence

**Date:** 2026-07-05
**Slice:** V3 Fuel-as-Clock Slice D (Shape A++)
**TD verdict:** `production/td-verdicts/2026-07-05-slice-d-map-travel-animation-seam.md`

## Automated (EditMode) — GREEN

- Runner: `Unity.exe -batchmode -runTests -testPlatform EditMode`
  (no `-quit` per `project_unity_batchmode_no_wait` memory).
- Filter: `WastelandRun.Run.Tests.FuelState_PreviewSpend_Test|WastelandRun.Run.Tests.RunSession_PreviewBeaconArrival_Test|WastelandRun.Run.Tests.RunSession_Fuel_Test`
- Result XML: `slice-d-editmode.xml` (Unity project root)
- Summary: **20 passed / 0 failed / 0 skipped** — 103 ms total.

### Test rollup

| Fixture | Passed / Total |
|---|---|
| `FuelState_PreviewSpend_Test` (POCO purity + arithmetic symmetry) | 8 / 8 |
| `RunSession_PreviewBeaconArrival_Test` (RunSession seam + deferred-commit ordering) | 6 / 6 |
| `RunSession_Fuel_Test` (regression guard for Spend/Haven contracts) | 6 / 6 |

The deferred-commit contract (`PreviewBeaconArrival_ToCombat_MatchesSubsequentAdvance_Delta` +
`PreviewBeaconArrival_ToHaven_MatchesSubsequentAdvance_Delta`) is the model-side proof
of the click → animation → commit flow: preview and Advance produce identical fuel
deltas because both call sites route through the private `ComputeDrain`/`ComputeRefill`
helpers.

## PlayMode — MANUAL WALKTHROUGH

Coroutine timing (3 s total, three milestone ticks) and Alt+F4 mid-flight
resume behaviour are out of scope for EditMode. Playtest steps:

### Golden path — click → travel animation → commit

Travel duration is constant-speed × distance, clamped to 1.5–4 s (see
`MapViewController.MarkerSpeedNormalizedPerSecond` + `MinTravelDuration` +
`MaxTravelDuration`). Short hops feel deliberate at 1.5 s; typical mid-map
edges land near 2 s; cross-map traversal clamps at 4 s.

1. Open `RunScene`, start a new run (RunSceneHost seed doesn't matter — this
   is a UI-timing check, not a determinism check).
2. Click a reachable non-Haven beacon (Combat or Rest — anything with a
   non-zero fuel cost).
3. **Observe** (during the travel window):
   - **A bright square player marker (`.wr-player-marker` — accent-hover
     fill, contrasting border, ~16 px) appears on top of the source
     beacon and slides straight-line toward the destination beacon**,
     reaching it at t = 3 s. This is the visible "the vehicle is
     travelling" affordance — Slice E will replace the placeholder square
     with a themed sprite / vehicle silhouette. If you see no marker at
     all, the beacons-layer or player-marker element failed to build —
     open the UI Toolkit Debugger and check for `#player-marker` on
     `#beacons-layer` (should be the last child).
   - The map view stays visible; no combat scene load fires yet.
   - Fuel HUD widget shows **no drain yet** — the fuel bar reads the
     pre-click value throughout the 3 s (Slice E will animate this).
   - Storm counter widget shows **no strip yet**.
   - Additional clicks on ANY beacon are silently dropped
     (`MapViewController.IsTraveling` + `RunSceneOverlayHost.HandleBeaconClicked`
     re-entry gate).
4. **At Arrive tick** (end of window):
   - Fuel HUD drops by the expected amount (base cost × chassis multiplier,
     ceil, min 1). For Scout: Combat = 6, Rest = 4, Event = 3.
   - Storm counter strips by the beacon's base cost.
   - Combat scene loads OR next map bind fires, depending on beacon type.
   - Player marker hides; the destination beacon takes on the
     `.wr-beacon--current` "you are here" state.

### Golden path — Haven arrival

1. Route to a Haven beacon after draining fuel (need a low tank so refill
   is observable).
2. Click Haven.
3. **Observe** (during the travel window):
   - Same marker travel: bright square slides from source beacon toward
     Haven along the visible connection line.
   - Same purity guard: fuel unchanged, storm counter unchanged.
4. **At Arrive tick**:
   - Fuel HUD rises by `ceil(35 × 0.65) = 23` (clamped to tank max = 35).
   - Storm counter resets to 30 (`StormCounterStart`).

### Alt+F4 mid-flight — resume-safe

1. Click any non-Haven beacon.
2. **Within the travel window**, force-close the game (Alt+F4 or Task Manager
   kill).
3. Relaunch, resume via main menu.
4. **Expected:** Player is parked on the pre-click beacon; fuel + storm
   counter carry their pre-click values; the previously-clicked destination
   is still *available* for a fresh click. This proves the deferred-commit
   contract — the click did not touch the persisted `RunState`.

### Cancellation-during-travel — clicks dropped

1. Click any beacon.
2. During the travel window, click a *different* beacon.
3. **Expected:** Second click is silently ignored; original animation
   completes; commit proceeds to the first destination only.

## Sign-off

- **Automated evidence:** GREEN — 20/20 in `slice-d-editmode.xml`.
- **Manual PlayMode walkthrough:** PENDING — user (BertanBerkol) to execute
  above steps before Slice D lands.
- **Landing gate:** commit is authorised on user's confirmation of the four
  PlayMode steps above.

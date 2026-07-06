# Slice E — Run HUD Fuel Pill — QA Evidence

**Date:** 2026-07-06
**Slice:** V3 Fuel-as-Clock Slice E — run-scope fuel HUD widget
**TD verdicts:**
- `production/td-verdicts/2026-07-06-slice-e-stage2-run-hud-widget.md` (Stage 2)
- `production/polish-captures/2026-07-06-slice-e-stage3-run-hud-mount.md` (Stage 3 capture)

## Automated (EditMode) — GREEN

- Runner: `Unity.exe -batchmode -runTests -testPlatform EditMode` (no `-quit` per
  `project_unity_batchmode_no_wait` memory).
- Result XML: `slice-e-stage3-editmode.xml` (Unity project root)
- Summary: **800 passed / 0 failed / 1 skipped** — 801 total — 16.7 s wall time.
- Stage 1 `BeaconTravelTick_Test.cs` (5 tests) present + green.
- No compile errors — Stage 2 widget files, Stage 3 author script,
  `AuthorRunScene` extension, and `AuthorAllScenes` extension all
  compile clean.

## PlayMode — MANUAL WALKTHROUGH

Coroutine drain-tween timing (0.4 s default; feel knob on
`RunHUDController._drainTweenDurationSec`) and cross-scene DontDestroyOnLoad
persistence are out of scope for EditMode. Playtest steps below.

### First-boot golden path — fuel pill appears + snapshots

1. `Tools > Wasteland Run > Scenes > Author All Scenes` (rebuilds
   `RunHUDHost.prefab` + mounts it in `RunScene.unity`).
2. Open `RunScene`, press Play.
3. **Observe** at run start:
   - **Fuel pill** (small chip in top-left corner — `.wr-fuel-pill`) is
     visible over the map. Shows `35/35` (tank max at Scout start).
   - Pill is below Combat_HUD z-order (Sort Order 5 vs 10) — this only
     matters once combat starts; verified visually via UI Toolkit Debugger
     showing separate panel.
   - Pill does NOT eat pointer events over the map (`picking-mode="Ignore"`
     on `#run-hud-root` and `#fuel-pill`). Clicks on beacons still resolve.

### Golden path — drain tween on non-Haven click

1. From start beacon, click a reachable Combat beacon (base cost = 8;
   Scout burn 0.7× = 6 drain).
2. **Observe** during the 3 s travel window:
   - Player marker slides from source to destination beacon (Slice D
     seam).
   - At Depart tick (`t=0`), **fuel pill starts animating**:
     - `.wr-fuel-pill--draining` USS class toggles on → background flips
       to `--wr-color-accent-hover` (warm orange).
     - Label counts down from `35/35` → `29/35` over 0.4 s via the
       serialized `_drainCurve` (EaseInOut default). Value uses
       `Mathf.RoundToInt(Mathf.Lerp)` so integer readout stays clean.
   - After 0.4 s: label reads `29/35`, background reverts to
     `--wr-color-bg-elevated`.
   - During the remaining ~2.6 s of travel: label stays at `29/35`
     (tween complete; model still untouched — Slice D deferred-commit).
3. **At Arrive tick** (end of travel window):
   - `SnapshotFuel` fires. Label re-reads `35 - 6 = 29/35` from live
     `FuelState.Current`. No change visible (belt-and-suspenders over the
     tween's final frame).
   - Combat scene loads.

### Golden path — Haven refill

1. Route to Haven after draining below max (need low tank so refill
   is observable; drain to ≤ 12 by clicking Combats first).
2. Click Haven (base cost = 0; refill `ceil(35 × 0.65) = 23`).
3. **Observe** during travel:
   - Same drain tween: pill animates from `fuelBefore` UP to
     `min(fuelBefore + 23, 35)`. Since values are just tweened via
     `Lerp`, the animation direction is upward (no different code path).
   - `.wr-fuel-pill--draining` USS class toggles on same way — the class
     name is generic to "animating," not literally "draining."
   - Label snaps to clamped max on completion (e.g. tank at 12 → 35, not 35+23).
4. **At Arrive:**
   - `SnapshotFuel` reads live `FuelState.Current` (authoritative — handles
     the Haven clamp cleanly). Label reads `35/35`.

### Persistence across Run → Combat → Run round-trip

1. Click any Combat beacon; travel + drain-tween complete; Combat scene
   additively loads.
2. **Observe:**
   - Fuel pill remains visible over the Combat HUD area (Sort Order 5 <
     10, so combat UI overlays it — MAY appear partly obscured, this is
     intended layering).
   - Label reads the drained value (e.g. `29/35`); no change during combat.
3. Win combat, claim reward, return to run.
4. **Observe:**
   - **Same pill instance** (verify by watching for a flicker — none
     expected). DontDestroyOnLoad + singleton guard prevented a
     rebuild.
   - Label still reads `29/35`.

### Restart mid-run

1. Start a run, click a Combat beacon and travel.
2. Fail combat → run-complete overlay appears.
3. Click Restart.
4. **Expected:**
   - `RunSceneHost.OnRunEnded` fires → `HandleRunEnded` drops
     `_fuel` reference (pill label frozen at last value).
   - `RunSceneHost.OnRunStarted` fires → `HandleRunStarted` re-caches
     `FuelState` from the fresh session. Label snaps to `35/35`.

### Alt+F4 mid-travel — safe

1. Click a Combat beacon.
2. **Within the 3 s travel window**, force-close the game (Alt+F4).
3. Relaunch, resume via main menu.
4. **Expected (per Slice D contract):**
   - Player parked on pre-click beacon.
   - Fuel + storm carry pre-click values (deferred-commit).
   - Fuel pill (freshly rebuilt from persistent host prefab) reads the
     resumed live value on `HandleRunStarted`.

## Files touched — Slice E full inventory

**Stage 1 (plumbing):**
- `Assets/Scripts/UI/BeaconTravelTick.cs` — modified: added
  `PreviewedFuelBefore` + `PreviewedFuelAfter` + Depart-only ctor.
- `Assets/Scripts/UI/MapViewController.cs` — modified:
  `PlayBeaconTravelAnimation` gained `fuelBefore` param; Depart tick
  carries preview values.
- `Assets/Scripts/CombatView/RunSceneOverlayHost.cs` — modified: passes
  `fuelBefore` at click site.
- `Assets/Tests/EditMode/UI/BeaconTravelTick_Test.cs` — new: 5 tests
  covering both ctors.

**Stage 2 (widget shell):**
- `Assets/UI/RunHUD.uxml` — new.
- `Assets/UI/RunHUD.uss` — new.
- `Assets/Scripts/CombatView/RunHUDHost.cs` — new (WastelandRun.CombatView
  namespace; UI asmdef can't reference CombatView).
- `Assets/Scripts/CombatView/RunHUDController.cs` — new (same namespace).

**Stage 3 (author + mount + panel settings):**
- `Assets/UI/RunHUDPanelSettings.asset` — new; Sort Order 5.
- `Assets/Editor/AuthorRunHUDHost.cs` — new; `Tools/Wasteland Run/Author
  RunHUDHost Prefab` menu.
- `Assets/Editor/CombatPrefabAuthor.cs` — modified: `AuthorRunScene`
  instantiates `RunHUDHost.prefab` as root scene GO; `AuthorAllScenes`
  calls the new author before `AuthorRunScene`.
- `Assets/Prefabs/RunHUDHost.prefab` — new (built by the author).

**Precursor (already landed as commit `82fe2b0`):**
- `Assets/Scripts/CombatView/RunSceneHost.cs` — `OnRunStarted` +
  `OnRunEnded` events added Slice E-0.

## Sign-off

- **Automated evidence:** GREEN — 800/0/1 in `slice-e-stage3-editmode.xml`.
- **Manual PlayMode walkthrough:** PENDING — user (BertanBerkol) to run
  the six sections above before Slice E lands.
- **Landing gate:** commit is authorised on user's confirmation of the
  playtest steps.

# Storm Pause-Event Cinematic — Capture

**Date:** 2026-07-25
**System:** Storm-advance pause-event (vignette + lane sweep + swallowed-beacon overlay + input lock)
**Trigger:** New system ≥50 lines across view layer (CLAUDE.md Capture-Before-Destroy §2).

## Summary of the change

Layer a pause-event cinematic on top of the V3 persistent storm cursor
(2026-07-24 verdict): every Combat exit that advances the storm now fades a
full-map red vignette in, sweeps the storm-front arc LEFT→RIGHT across the
lane over `StormAdvanceTick.DurationSeconds`, crosses out beacons the arc
passes (`.wr-beacon--swallowed`), holds ~0.5s past the sweep so the player
reads the final state, then fades the vignette out and returns control.
Input is locked twice — at the view (`_acceptClicks`) AND at the overlay
host (`IsInputLocked` gate) — so a click during the window can't slip
through either path. Force-unlocked on `OnRunEnded` so a run ending
mid-sweep never leaks the lock into a restart.

Companion changes shipped same session, same commit unit:
- Arc concavity default `0.15 → 0.08 → 0.04` (softer LEFT bulge).
- `Biome1Distribution._stormCounterStart` `30 → 8` (V3 tightening).
- `RunHUD` sibling storm-pill widget (420ms red flash on each advance).
- UIDocument-clone-race fix (`ResolveLayersIfPossible` from OnEnable +
  lazily from `EnsureStormFront`) — fixed intermittent "storm arc
  doesn't show" bug when the host's OnEnable races UIDocument's async
  clone under `[DefaultExecutionOrder(-100)]`.

## Authored values / surfaces at risk (destructive audit)

| Surface | Current value | Destructive? | Plan |
|---|---|---|---|
| `MapViewController._acceptClicks` | Implicit `true` in original click-forwarding path | Additive — new gate field + property | Add `_acceptClicks:true` + `IsInputLocked => !_acceptClicks` read-only property; `ForwardBeaconClick` early-returns when false |
| `MapViewController._arcConcavityNormalized` | Serialized 0.15 (per earlier verdict) | Retune default only | Bumped 0.15 → 0.08 → 0.04 across two playtest passes; still `[Range(0f, 0.5f)]` — designer can retune without recompile |
| `MapViewController._stormVignette` element | Nonexistent (no vignette layer) | Additive | Lazy-created in `EnsureStormVignette`, parented to `_canvas.parent` (map root, NOT canvas — see below) |
| Vignette parent choice | N/A | Additive — deliberate scoping | Parented to root (parent of `wr-map-canvas`) so the tint covers beacon-layer negative-inset chip fringes that spill past canvas edges |
| `MapViewController._stormSwallowedThroughIndex` | Nonexistent | Additive; `int.MinValue` sentinel | Tracker survives Bind cycles via `RebuildBeacons` reapply; `FlipSwallowedForApex` clamps start ≥ 0 so first grace-distance crossing doesn't loop 2^31 times |
| `MapView.uss` USS pool | 349 lines pre-slice | Additive selectors only | +`.wr-beacon--swallowed` (opacity 0.35 + tinted icon rgb(80,80,80)); +`.wr-storm-vignette` (background-color rgba(180,30,30,0) with 0.35s ease-out `transition-property: background-color`); +`.wr-storm-vignette--active` (rgba(180,30,30,0.28)) |
| `StormMapVisualHost._postAdvanceHoldSeconds` | Nonexistent (no post-hold) | Additive SerializeField | Default 0.5s; inspector-tunable per lagging-dep pattern |
| `StormMapVisualHost._inputLockCoroutine` | Nonexistent | Additive lifecycle field | Started on `HandleStormAdvanced`; stopped on OnDisable/OnRunEnded/reentry |
| `StormMapVisualHost` OnRunEnded subscription | Not subscribed | Additive subscriber | Force-unlocks the map so a game-over mid-sweep doesn't leak the lock into restart |
| `RunSceneOverlayHost.HandleBeaconClicked` gate list | Gated on `_mapView.IsTraveling` only | Additive gate | Second gate on `_mapView.IsInputLocked` — belt-and-braces against future keyboard-driven click paths that bypass `ForwardBeaconClick` |
| `Biome1Distribution._stormCounterStart` | 30 | Value retune | 8 (V3 tightening) — under Combat cost 8 + start 8, `Spend(8)` wraps the counter in-call (see "known drift" below) |
| `RunHUD.uxml/uss/RunHUDController.cs` storm-pill | Nonexistent | Additive sibling widget | Under fuel pill; subscribes `_host.OnStormAdvanced`; 420ms red flash per advance |

No prefab wipes. No scene reauthors. No SO field renames on existing assets.
All view-layer additions live behind lazy-creation guards so existing
Bind cycles keep working when the storm system is idle.

## Files created (new)

- `Assets/Scripts/CombatView/StormAdvanceVisualPacer.cs` — injects wall-clock `DurationSeconds` on `StormAdvanceTick`
- `Assets/Scripts/CombatView/RunHUDController.cs` / `RunHUDHost.cs` — HUD host + storm/fuel pill controller
- `Assets/UI/RunHUD.uxml` / `.uss` / `RunHUDPanelSettings.asset` — HUD document tree
- `Assets/Editor/AuthorRunHUDHost.cs` — HUD host prefab authoring

(All appear as `??` untracked in Unity repo; see resume block for full list.)

## Files modified

- `Assets/Scripts/UI/MapViewController.cs` — `+249` lines; `_acceptClicks` gate, `IsInputLocked` property, `SetInputLocked(bool)` API, `_stormVignette` element + `EnsureStormVignette`, `_stormSwallowedThroughIndex` tracker with `int.MinValue` sentinel, `ShowStormFrontAt` calls `FlipSwallowedThrough`, `StormAdvanceCoroutine` calls `FlipSwallowedForApex` per frame, `RebuildBeacons` reapplies swallowed class after clear, `ResolveLayersIfPossible` OnEnable+lazy fallback for UIDocument-clone-race, arc concavity default 0.04.
- `Assets/UI/MapView.uss` — `+38` lines; three additive selectors (`.wr-beacon--swallowed`, `.wr-storm-vignette`, `.wr-storm-vignette--active`).
- `Assets/Scripts/CombatView/StormMapVisualHost.cs` — `_postAdvanceHoldSeconds:0.5f` inspector knob; `_inputLockCoroutine` lifecycle field; subscribes `OnRunEnded` for force-unlock; `HandleStormAdvanced` locks + schedules unlock at `duration + hold`; `OnDisable` unlocks belt-and-braces.
- `Assets/Scripts/CombatView/RunSceneOverlayHost.cs` — `+174` lines total across the slice; pause-event slice adds `IsInputLocked` gate to `HandleBeaconClicked`.
- `Assets/Resources/Run/Biomes/Biome1Distribution.asset` — `_stormCounterStart: 30 → 8`.

## Technical Director Review

> **Verdict:** APPROVE — Shape "Option B: Pause-Event Cinematic on Presentation Surface"
> (verbal verdict from `technical-director` subagent, 2026-07-25; reconstructed
> inline here per session-state resume note.)

**Option A rejected (Cursor-on-RunState).** Adding a `PauseEventPhase` field
to `RunState` or promoting the sweep to a modal `RunSession` sub-state would
have coupled a purely presentational hold to save shape and doubled the
model surface without gameplay benefit. The sweep has no lifetime beyond
one Combat exit; storing it on `RunState` would violate the same
"presentational cursor, not run-state" boundary the 2026-07-24 storm
verdict already drew.

**Option B accepted (Presentation-only lock + vignette).** All new fields
live on `MapViewController` and `StormMapVisualHost`. Zero save-shape
churn. Zero `RunState` / `FuelState` / DTO delta. The pause-event is a
view-layer beat over an existing model event (`OnStormAdvanced`) — the
same shape as the travel animation.

**Two-gate input lock (belt-and-braces).** The view-side `_acceptClicks`
filter in `ForwardBeaconClick` catches all beacon clicks routed through
`BeaconNodeElement`. The host-side `RunSceneOverlayHost.HandleBeaconClicked`
gate on `IsInputLocked` catches any future keyboard/gamepad path that
bypasses `ForwardBeaconClick`. Redundant by design — TD flagged the
single-gate variant as a Level-2 risk under near-term input-method
expansion (memory `feedback_uitoolkit_subscription_lifecycle` reminded us
that view + host lifecycles diverge under SetActive cycles).

**Force-unlock on OnRunEnded (Level-2 leak fix).** A run ending mid-sweep
(engulfment game-over, quit-to-menu) would leave `_acceptClicks=false`
persisted on the MapViewController instance. `OnDisable` catches
component teardown; `OnRunEnded` catches the more common case where the
run ends but the scene stays loaded.

**Vignette parent scope.** Parenting to `wr-map-root` (parent of
`wr-map-canvas`) instead of the canvas itself covers the beacon-layer
negative-inset chip fringes that spill past the canvas margin. Canvas-scope
would leave protruding chip fringes untinted during the cinematic — visible
"gap" reads as a bug even though the input lock is intact.

**Runaway-loop guard.** `FlipSwallowedForApex` clamps `start` to
`max(_stormSwallowedThroughIndex + 1, 0)` so the initial `int.MinValue`
sentinel doesn't produce a 2^31-iteration first-grace-crossing loop.
Discovered in-session; no shipped bug.

**ADR-0011.** Clean, no bridges. No enum values added, no bimodal paths.
`OnStormAdvanced` payload type unchanged. Both new gates use the same
`IsInputLocked` predicate — single source of truth, no drift.

**Three-Lens Self-Audit.**

*Lens 1 — Codebase Health:* Two lifecycle-pair fields (`_deferredInitialPaint`,
`_inputLockCoroutine`) both properly torn down in OnDisable. Subscription
pattern OnEnable/OnDisable correct for a Run.prefab-root MonoBehaviour
(never SetActive-toggled under a UIDocument parent).

*Lens 2 — Optimization:* Swallowed-flip is O(new beacons) per frame during
the sweep, zero on idle frames. Vignette transition is USS-driven
(GPU-side lerp); C# only toggles a class name per state change.

*Lens 3 — 1.0 Shape Survival:* All authored knobs (`_postAdvanceHoldSeconds`,
`_secondsPerBeacon`, `_arcConcavityNormalized`) are `[SerializeField]` +
`[Range]`-annotated so designers retune without recompile. Swallowed-node
overlay survives to 1.0 as-is — the crossed-out styling is the canonical
"lane no longer accessible" affordance.

## Recommended commit shape

Session-state resume block already covers the A/B/C split:

- **Commit A** — V3 storm cursor core (`StormState`, `StormStateDto`,
  `StormStateSerializable`, `RunSession` inline advance, `RunController`,
  `BiomeDistributionSO` wiring, `StormState_Test`, `StormStateDto_round_trip_test`).
- **Commit B** — Presentation layer covered by THIS capture:
  `StormAdvanceVisualPacer` + `StormMapVisualHost` + `MapViewController`
  storm methods + `StormFrontElement` + `RunHUD`(host/controller/uxml/uss)
  + arc concavity default + UIDocument-clone-race fix + storm-pill +
  pause-event cinematic + `RunSceneOverlayHost` gate.
- **Commit C** — SO retunes (`Biome1Distribution._stormCounterStart 30→8`,
  `StormEngulfmentTuning.asset` if the tuning-asset wire-up lands).

**DO NOT `git add -A`** — earlier-slice scaffolding is still mixed in
(`AuthorRunHUDHost.cs`, `BeaconCopy.cs`, `BeaconTravelTick_Test.cs`,
`Fuel Canister.png`, `Map BG.png`, `Map Node Icons.psb`,
`GameOverView.*`, `RunHUDPanelSettings.asset`). Walk each with the user
before staging.

## Open decisions (deferred to playtest)

- **Storm-pill visible countdown** — under V3 semantics, Combat cost 8 +
  `StormCounterStart` 8 makes `Spend(8)` wrap in-call so the label reads
  "8/8 → 8/8" invisibly. Camouflaged by the sweep itself. Separate
  animation slice if we want a visible countdown; NOT blocking.
- **Multi-strip cinematic duration** — a hypothetical 8-strip cross would
  produce `SecondsPerBeacon(1.5) × 8 = 12s`. Biome-1 today emits 1 strip
  per Combat exit; revisit only if a future SO ever emits multi-strip.
- **`StormEngulfmentTuning.asset` wire-up** — SO instance exists at
  `Assets/Resources/Run/`; confirm the `StormAdvanceVisualPacer._tuning`
  field on `Run.prefab` is wired. If null, `FallbackSecondsPerBeacon(1.5f)`
  is used silently — fine for playtest, tighten before commit.
- **`_secondsPerBeacon` / `_postAdvanceHoldSeconds` cadence** — both
  inspector-tunable; retune during tomorrow's PlayMode playtest if the
  cadence feels wrong.

## Addendum 2026-07-25 — V3→V3.1 spatial-cursor pivot + Shape B' reshape

Same-day iteration on top of the pause-event cinematic. Followed the TD
verdict at `production/td-verdicts/2026-07-25-storm-cursor-spatial-pivot.md`
after four-pass convergence.

### V3.1 spatial pivot — what changed

Storm advance decoupled from Combat-cost accounting. `RunSession` now emits
`OnStormAdvanced` on every Combat exit with a chassis-neutral `baseCost`,
so a Scout and a Truck see the same storm cadence regardless of
`ChassisFuelBurnMultiplier`. The visual sweep still animates from
`StormState.CursorX` across the map via `StormFrontElement`; the underlying
cursor is a normalized-X (0..1) float advanced by `StormAdvancePerStrip`
per Combat exit.

### Shape B' reshape — counter/state relocation

Post-pivot TD review flagged that `Counter` / `CounterStart` / wrap
arithmetic living on `FuelState` was a shape smell — fuel's job is
resource accounting; counter's job is storm cadence. Locked decisions
after four convergence passes:

1. **Counter/CounterStart/wrap arithmetic moved FuelState → StormState.**
   `StormState` now carries `(CursorX, Counter, CounterStart)` and the
   in-call wrap in `Advance(int cost)`. `FuelState` reverts to `(Max,
   Current)` — the shape it was before V3.
2. **Float-cursor code + `StormAdvanceTick` payload untouched.** Presentation
   layer sees zero delta; the pacer, vignette, sweep, and USS classes are
   all inert to the relocation.
3. **HUD reads live `Storm.Counter` on the existing Arrive path.**
   `RunHUDController` binds to the same `OnStormAdvanced` event; no new
   event, no new poll.
4. **Standalone-group-of-one on `FuelState` DTO with fresh-tank on
   mismatch.** No cross-DTO migrator — `FuelStateSerializable` treats
   pre-B' saves as corrupted and rebuilds a fresh tank at chassis-defined
   `MaxFuel`. Cheaper than a migrator; save-shape drift under ADR-0004 is
   an all-or-none group anyway (session_core cascade catches player-vehicle
   mismatch).
5. **`_stormCounterStart` bumped 8 → 24.** Under V3.1 `baseCost=8`, this
   makes the counter visibly tick `24 → 16 → 8 → 0 → wrap` across three
   Combat commits per strip — the pill actually animates instead of
   invisibly wrapping in-call.
6. **Stranded auto-loop as session-side verb.** `RunSession.AutoAdvanceStrandedStorm()`
   drives an out-of-fuel storm sweep. Pacer owns the wall-clock timer and
   the `_engulfed` gate; session emits `StormAdvanceTick` with
   `DurationSeconds:0`, pacer injects the visible window. Preserves the
   V2 engulfment game-over trigger (see `2026-07-24-storm-engulfment.md`).

### Test migration cost (this session)

11 EditMode test files migrated to Shape B' API. Pattern per file:

- `new FuelState(max, N, startingFuel)` → `new FuelState(max, startingFuel)`
- Add `private const int TestStormCounterStart = 24;`
- `new StormState(cursorX)` → `new StormState(cursorX, TestStormCounterStart)`

Files touched (all `Assets/Tests/EditMode/`):
`Run/RunSession_Test.cs`, `Run/RunController_HappyPath_Test.cs`,
`Run/RunSession_CardReward_Test.cs`, `Run/RunSession_ResolveRest_test.cs`,
`Run/RunSession_Reward_Test.cs`, `CombatView/SceneEncounterBuilder_Test.cs`,
`Save/RunDeckSerializable_test.cs`, `Save/RunSeedSerializable_test.cs`,
`Save/VehicleStateSerializable_test.cs`,
`CombatView/RunSceneHost_RestResume_test.cs`,
`CombatView/RunSceneHost_Resume_Test.cs`.

Verified via final grep — zero single-arg `StormState` and zero 3-arg
`FuelState` calls remain in `Assets/`.

### Commit shape (updated)

Original A/B/C from the main body still applies. Shape B' + V3.1 pivot
folds cleanly into:

- **Commit A** — Model core (updated scope): V3.1 pivot + Shape B' reshape
  (`StormState` gains Counter/CounterStart, `FuelState` reverts to 2-tuple,
  `RunSession.AutoAdvanceStrandedStorm()`, `StormStateDto` + serializable
  gain counter fields, `FuelStateDto` reverts). Plus all 11 test-file
  API migrations.
- **Commit B** — Presentation (unchanged from main body).
- **Commit C** — SO retunes (updated): `_stormCounterStart: 8 → 24`
  (Biome1Distribution).

---

## Addendum 2026-07-26 — Tick-by-1 reshape + HUD storm-pill lerp

**Playtest feedback (2026-07-26):** Shape B' shipped with `_stormCounterStart = 24` and per-commit tick equal to the Combat base cost (8). User surfaced two issues:

1. Storm-pill widget SNAPPED at Arrive instead of lerping smoothly during travel (out of phase with the fuel-pill which lerps).
2. `24` too high and non-uniform (Combat=8 vs Merchant=4 mean the counter drops in inconsistent chunks — hard to read cadence at a glance). User directive: "every storm step should be small steps forward. 8 is the target."

**TD verdict:** `production/td-verdicts/2026-07-26-storm-counter-tick-reshape.md` — AMEND with two riders:

1. Keep `stormCost` parameter with `default = 1` on `AdvanceCounter`/`PreviewAdvanceCounter` — preserves the future biome-3 per-beacon-type storm-weight table seam (ADR-0015 lagging-dep pattern). Session drops the arg (uniform tick-by-1 today); the parameter lets a future biome shape push weights via SO without another API break.
2. Rename `PreviewNextCounter` → `PeekNextCounter` on StormState. Avoids naming rhyme with `PreviewAdvanceCounter` (which returns strip-wrap count, not counter value). `Peek` reads as "landed counter value"; `Preview` reads as "strip-wrap flag."

**Changes shipped (this addendum):**

- `StormState.AdvanceCounter(int stormCost = 1)` — default arg, param renamed `baseCost → stormCost`.
- `StormState.PreviewAdvanceCounter(int stormCost = 1)` — same.
- New `StormState.PeekNextCounter(int stormCost = 1)` — pure read-only landed-counter preview for HUD lerp.
- `RunSession.Advance` + `PreviewBeaconArrival` — drop the `baseCost` arg at both call sites (default=1 kicks in).
- `Biome1Distribution.asset` — `_stormCounterStart: 24 → 8`.
- `BiomeDistributionSO._stormCounterStart` default: `24 → 8` + tooltip rewritten.
- `BiomeDistributionSO_FuelCosts_Test.cs` — default-assertion updated 30 → 8.
- `RunSession_Fuel_Test.cs` — 6 test bodies migrated: renamed `TicksStormCounterByOne` (was `DrainsStormCounterByChassisNeutralBaseCost`), `-8 → -1` in three tests, priming rewritten in three tests (`storm.AdvanceCounter(8); storm.AdvanceCounter(8);` → `storm.AdvanceCounter(TestStormCounterStart - 1);` lands counter at 1).
- `RunSession_PreviewBeaconArrival_Test.cs:180` — `counterBefore - 8` → `counterBefore - 1`.
- All test-file `TestStormCounterStart = 24` fixture consts left unchanged (opaque fixture value, still internally consistent).

**HUD lerp reshape (same commit — cures the user's #1 complaint):**

- `BeaconTravelTick` gains `PreviewedStormCounterBefore` / `PreviewedStormCounterAfter` (int). Populated on every tick like the fuel Before/After pair.
- `MapViewController.PlayBeaconTravelAnimation` signature adds `counterBefore` + `counterAfter` params. All three tick emissions (Depart, InFlight×N, Arrive) plumb them through.
- `RunSceneOverlayHost.HandleBeaconClicked` computes `counterAfter` at click site (Haven → `storm.CounterStart`; non-Haven → `storm.PeekNextCounter()`). Keeps MapViewController engine-code-free (no StormState dep).
- `RunHUDController.HandleBeaconTravelTick` — InFlight branch now lerps the storm-pill number in phase with the fuel-pill (shared `_drainCurve`); guarded via `_lastShownStormCounter`. Arrive snaps to live state.
- `BeaconTravelTick_Test.cs` — all 5 constructor calls migrated to new signature.

**Cinematic pipeline check (task #25 — no code bug):** Wiring audit of `RunSession.Advance → StormAdvanceVisualPacer → RunSceneHost.RaiseStormAdvanced → StormMapVisualHost.HandleStormAdvanced` shows all 4 hops correctly bound. AdvanceCounter wrap math is sound (`Counter=1 → cursor=0 → wrap → strips=1, cursor=CounterStart`). Under the OLD 24 model wraps were rare (every 3 commits) — user's "counter reset without cursor moving" report was likely a perception issue masked by infrequent wraps. Under tick-by-1 with CounterStart=8, wraps happen every 8 non-Haven commits — pipeline should be far more visible. Awaiting user playtest to confirm.

**Test-migration cost this addendum:** ~7 test assertions across 3 files (Fuel_Test, PreviewBeaconArrival_Test, BeaconTravelTick_Test, BiomeDistributionSO_FuelCosts_Test). Zero API breakage in production code — `stormCost` parameter surviving as default kept RunSession and StormState_Test.cs on their existing signatures.

---

## Addendum 2026-07-26 (second pass) — REVERSE tick-by-1, restore tick-by-fuel-cost

**Playtest feedback after tick-by-1 shipped:** "why is the storm counter not ticking with fuel but ticking by how many nodes have crossed? this is wrong."

Design intent restored: the storm counter drops by the destination beacon's fuel base cost (Combat=8, Merchant=4, Elite=12), NOT by uniform 1-per-commit. Preserves "harder encounters advance the storm faster" thematic read that tick-by-1 flattened.

**TD verdict:** `production/td-verdicts/2026-07-26-storm-counter-tick-reshape.md` — second-pass amendment ACCEPT. Full detail in the verdict; summary here.

**Changes shipped (this second-pass addendum):**

- `RunSession.Advance` (line 228): `storm.AdvanceCounter()` → `storm.AdvanceCounter(baseCost)`.
- `RunSession.PreviewBeaconArrival` (line 308): `storm.PreviewAdvanceCounter()` → `storm.PreviewAdvanceCounter(baseCost)`.
- `BeaconTravelPreview.StormCounterAfter` (new int field) — composed at `RunSession.PreviewBeaconArrival` where the beacon-fuel-cost table is visible.
- `RunSceneOverlayHost` — dropped direct `storm.PeekNextCounter()` call at click site; reads `preview.StormCounterAfter` instead.
- `RunSession_Fuel_Test.cs` — 6 assertions reverted: `TicksStormCounterByOne` → `DrainsStormCounterByBaseCost`; `-1` → `-8`; priming shortcut in FiresOnStormAdvanced tests uses `AdvanceCounter(TestStormCounterStart - 1)` to land counter at 1 (under CounterStart=8 with Combat=8 the very next commit wraps).
- `RunSession_PreviewBeaconArrival_Test.cs:180` — `-1` → `-8` (see third-pass addendum below for the final revert).

**What stayed from tick-by-1:**

- `StormState.AdvanceCounter/PreviewAdvanceCounter/PeekNextCounter(int stormCost = 1)` — parameter name + default arg unchanged (default is a safety fallback for future misuse; real callers pass explicit cost).
- CounterStart = 8 (SO default + Biome1 asset). Confirmed by user twice.
- HUD storm-pill InFlight lerp — model-agnostic, works with either tick model.

**Why the parameter stays:** ADR-0015 lagging-dep seam for biome-3 per-beacon-type storm-weight table. Reverting to tick-by-fuel doesn't invalidate the seam; it picks fuel-cost as the "storm weight" formula for biome 1.

---

## Addendum 2026-07-26 (third pass) — Countdown-timer widget shape

**Playtest feedback (2026-07-26, later same day):** After the tick-by-fuel-cost reversal shipped (second-pass addendum above, kept CounterStart=8), user surfaced the final directive: "the storm counter should look like a timer that is going down, once it reaches 0 the storm actioncycle should take affect. every time it hits 0 the its storms turn to move."

**Root cause of the mismatch:** post-reversal, `PeekNextCounter(baseCost=8)` under `Counter=8, CounterStart=8` returned 8 (the post-wrap reset value). The widget lerped 8→8 across the travel window (invisible countdown), then any subsequent `SnapshotStormCounter()` on Arrive re-read live=8 and produced no visual transition — the countdown-to-zero moment was never rendered.

**TD verdict:** `production/td-verdicts/2026-07-26-storm-counter-tick-reshape.md` — third-pass amendment ACCEPTED. Compose-site fix at RunSession + RunHUDController; zero primitive changes on StormState.

**Changes shipped (this third-pass addendum):**

- `RunSession.PreviewBeaconArrival` (non-Haven branch): compose `stormCounterAfter = stormStrips > 0 ? 0 : storm.PeekNextCounter(baseCost)`. On strip-firing commits the widget-visible endpoint is the pre-wrap zero-crossing (0), not the post-wrap reset (CounterStart). Partial-spend commits (Merchant=4 against Counter=8 → next=4) still peek the reduced value.
- `RunHUDController.HandleBeaconTravelTick.Arrive`: if `tick.StormAdvanceStrips > 0`, explicitly write `0` to the label and skip `SnapshotStormCounter()`. Guarantees the visible endpoint even if the last InFlight tick's curved progress hadn't rounded to zero yet. Non-strip commits (partial spend / Haven refill) snap immediately per prior contract.
- `RunHUDController.HandleStormAdvanced`: schedule `SnapshotStormCounter()` to fire at `tick.DurationSeconds` ms (min 1) — the widget visually rests at 0 while the storm cursor sweeps its arc, then snaps back to CounterStart in phase with the cinematic completion. Same schedule handle as the existing red-flash pulse.
- `BeaconTravelPreview.StormCounterAfter` xmldoc: updated to describe widget-visible endpoint semantics + strip-firing 0 behavior.
- `BeaconTravelTick.PreviewedStormCounterAfter` xmldoc: same update.
- `RunSession_PreviewBeaconArrival_Test.cs:181`: `-1` → `-8` (assertion targets LIVE `storm.Counter` post-Advance, which continues to decrement by fuel base cost per the second-pass reversal — this test uses fixture `TestStormCounterStart=24`, so Combat=8 lands the counter at 16 with no wrap).

**Visual sequence (final target shape):**

1. Depart (t=0): widget = 8.
2. InFlight (0<t<1): widget lerps 8→0 across the travel window (drainCurve feel-shaped).
3. onComplete → Advance: model commits (counter wraps live to 8), OnStormAdvanced fires with DurationSeconds=SecondsPerBeacon (pacer-injected). Red flash starts; reset snap SCHEDULED for +SecondsPerBeacon.
4. Arrive (t=1): widget written to 0; snap skipped.
5. Cinematic (+SecondsPerBeacon seconds): storm-front arc sweeps via StormMapVisualHost; widget holds at 0.
6. Cinematic end: scheduled snap fires → widget resets to 8 for next cycle.

**Why compose-site not primitive change:** `PeekNextCounter` is a pure math peek of the post-wrap value — its behavior matches the model. The widget's needs (show zero-crossing before wrap) are presentation-shape, not model-shape. Adding a `PeekPreWrapValue()` primitive would ship a widget-shaped primitive on the model — a bridge in the ADR-0011 sense. Composing the widget's visible endpoint at the session boundary keeps the model clean and lets the widget concern stay at the widget's own seam.

---

## Addendum 2026-07-26 (fourth pass) — Deferred-cinematic queue at pacer

**Playtest feedback (2026-07-26, immediately after third-pass landed):**

> "the storm is not advancing right now ... this storm advancement cycle should be remembered and then once the player is out of the node interaction, the game should pause, and the storm arc should move forward"

**Root cause:** `RunSession.Advance` fires `OnStormAdvanced` synchronously. `RunSceneHost.AdvanceToNextBeacon` calls `session.Advance(next)` then raises `OnBeaconChanged` in the same call — flipping the scene to node-interaction and hiding the map on Combat/Rest/etc. beacons. The storm-cursor arc sweep starts against a hidden map. The counter widget lerp still worked (fires from `OnBeaconTravelTick` during the pre-Advance travel animation) — only the cursor sweep on the map was invisible.

**TD verdict:** `production/td-verdicts/2026-07-26-storm-counter-tick-reshape.md` — fourth-pass amendment ACCEPTED. Queue-and-flush at the pacer, no session-side changes.

**Changes shipped (this fourth-pass addendum):**

- `StormAdvanceVisualPacer` (`CombatView`): added `Queue<StormAdvanceTick> _pendingModelTicks`, `bool _pendingEngulfment`, `bool _sweepInFlight`, `Coroutine _flushCoroutine`.
- Subscribed to `RunSceneHost.OnBeaconChanged` and `OnRewardClaimed`; both route to `HandleMapPossiblyReturned` → `TryFlushPending`.
- `HandleSessionStormAdvanced(modelTick)`: **enqueue** instead of raising immediately. `HandleSessionStormEngulfed()`: **flag** `_pendingEngulfment` instead of raising immediately.
- `IsMapCurrent()` predicate: `CurrentBeacon == null || Type == Start || IsResolved`.
- `FlushPendingSequential` coroutine: dequeues each tick, raises via `RaiseStormAdvanced(DurationSeconds=SecondsPerBeacon)`, waits, then advances. Re-guards on `IsMapCurrent()` inside the loop so a Haven→Combat chain suspends mid-flush and re-arms on next map return. After all ticks drain, if `_pendingEngulfment`, raises `RaiseStormEngulfed` last — preserves TD Amendment A3 game-over-lands-on-settled-paint ordering.
- Removed dead code: `_deferredEngulfCoroutine` field + `DeferredEngulfment` IEnumerator + OnDisable StopCoroutine block + `_lastAdvanceDuration` write. Fire path fully replaced; no bimodal path retained (ADR-0011 clean).

**Visual sequence (post-fourth-pass, end-to-end):**

1. Player clicks a beacon that will fire a strip. Map-view travel animation runs 3s.
2. Counter widget lerps 8→0 (Depart→InFlight→Arrive).
3. `onComplete` → `session.Advance` commits (model wraps counter live to 8), fires `OnStormAdvanced` synchronously. **Pacer enqueues**. Host fires `OnBeaconChanged` in the same frame → map hides, node overlay opens.
4. Player resolves node (Combat victory / Rest close). Host fires `OnRewardClaimed` (Combat) or `OnBeaconChanged` (Rest resolution) → map returns.
5. `HandleMapPossiblyReturned` → `TryFlushPending` → `IsMapCurrent()` true → coroutine starts.
6. `RaiseStormAdvanced(DurationSeconds=SecondsPerBeacon)`. `StormMapVisualHost` locks map input, plays the arc sweep, holds through the storm hold seconds.
7. Coroutine waits the duration; queue drains; engulfment raises last if flagged.
8. Counter widget snap already scheduled from third-pass shape; snaps back to CounterStart in phase with sweep end.

**Files touched by fourth-pass amendment:**

- `Wasteland Run/Assets/Scripts/CombatView/StormAdvanceVisualPacer.cs` — full queue implementation + dead-code cleanup.

**Test surface impact:** Zero. Session tests still assert `OnStormAdvanced` fires synchronously with a zero-duration tick from `RunSession.Advance`. Pacer is engine-side only (MonoBehaviour, no EditMode test today).

**Success criterion:** Click a Combat beacon (fires a strip under Combat=8 vs CounterStart=8). Resolve Combat. Verify storm arc sweep is visible on map return, verify counter widget snap is in phase with sweep end.

---

## Addendum 2026-07-26 (fifth pass) — Map enter/exit fade-from-black

**Playtest feedback (2026-07-26, later same day):**

> "also can you please add in a fade in from black while entering and exiting the run map."

**Shape (Show / Hide transition polish):** Full-screen black scrim on MapView, opacity driven by MapViewController.Show/Hide. On Show the scrim snaps to opaque black in the same frame the map is displayed, then lerps opacity 1→0 over `_fadeDurationSeconds` (0.3s default). On Hide the scrim lerps 0→1 (from-transparent to-opaque) then snaps the map content to `display: none` at fade-complete. Root UIDocument stays `display: Flex` across the fade so the scrim survives the map-content hide.

**Changes shipped (this fifth-pass addendum):**

- `Assets/UI/MapView.uxml`: added `#map-fade-scrim` VE as sibling of `wr-map-root` (renamed root to `#map-content` for the display toggle). `picking-mode="Ignore"` baseline.
- `Assets/UI/MapView.uss`: added `.wr-map-fade-scrim` — position absolute, full-screen, `background-color: rgb(0,0,0)`, `opacity: 0` baseline.
- `MapViewController.cs`:
  - Added `[SerializeField, Range(0f,1f)] private float _fadeDurationSeconds = 0.3f` feel-knob.
  - Cached `_mapContent`, `_fadeScrim` VisualElements + `_fadeCoroutine`, `_isVisible` state.
  - `ResolveLayersIfPossible` now Q's `#map-content` and `#map-fade-scrim` alongside existing layers.
  - `Show()`: sets root Flex, snaps scrim opacity=1, starts `FadeScrim(1→0)` coroutine. Idempotent (`_isVisible` gate) so back-to-back OnBeaconChanged/OnRewardClaimed don't re-fade.
  - `Hide()`: starts `FadeScrim(0→1)` coroutine with `_mapContent.display=None` in the onComplete callback. Snap-hide fallback when already hidden or when scrim resolution failed.
  - `StartFadeCoroutine` cancels any in-flight fade before starting a new one.
  - `FadeScrim` uses `Time.unscaledDeltaTime` so a paused run still transitions.
  - `IsInputLocked` now includes `|| _fadeCoroutine != null` so beacon clicks arriving during the fade window are gated by the same seam the storm cinematic uses. Belt-and-braces: RunSceneOverlayHost's click handler already gates on `IsInputLocked` (2026-07-25 pause-event verdict).
  - `OnDisable` stops the fade coroutine to prevent leaks on run restart.

**Visual sequence — map exit (map → Combat):**

1. Player clicks Combat beacon → 3s travel animation on map.
2. `onComplete` → `session.Advance` commits → OnStormAdvanced enqueued by pacer.
3. RunSceneHost fires OnBeaconChanged → RunSceneOverlayHost.HandleBeaconChanged sees `mapIsCurrent=false` → calls `_mapView.Hide()`.
4. Hide starts scrim 0→1 fade. `IsInputLocked` returns true throughout the fade.
5. In parallel: BeaconActivator triggers async Combat scene load.
6. Fade completes at +0.3s → `_mapContent.display=None`.
7. Combat scene appears (async load may finish before or after the fade — either way, Combat scene is what the player sees after the fade).

**Visual sequence — map enter (Combat → map):**

1. Player wins Combat → OnRewardClaimed fires.
2. RunSceneOverlayHost.HandleRewardClaimed → HandleBeaconChanged → `mapIsCurrent=true` (beacon resolved) → `_mapView.Show()`.
3. Show snaps scrim opacity=1 (black), sets `_mapContent.display=Flex`, starts scrim 1→0 fade.
4. In parallel: StormAdvanceVisualPacer sees OnRewardClaimed → TryFlushPending → starts storm cinematic (locks input via SetInputLocked, plays sweep).
5. Fade completes at +0.3s → scrim invisible. Storm cinematic continues under its own SecondsPerBeacon window.
6. Player sees: black → map reveal (with storm cinematic playing behind) → cinematic completes → settled state.

**Design intent:** Fade is short enough (~0.3s) to feel like a deliberate cut, not a stall. Uses UnscaledDeltaTime so a Time.timeScale=0 pause (out-of-fuel death sequence) still transitions cleanly to the game-over screen. `IsInputLocked` gate ensures no beacon clicks land mid-fade to keep the enter/exit invariant consistent with the storm cinematic's own input lock.

**Non-goals (deferred):** Fade on Combat scene enter (would require a scrim in the Combat scene). Fade on Rest scene enter (same). Fade on game-over screen appear (the from-black on map exit already covers the visual transition before game-over renders on top). Cross-fade between map and Combat (would need coordinated scrim across both scenes — significant surgery for marginal feel benefit).

**Files touched by fifth-pass addendum:**

- `Wasteland Run/Assets/UI/MapView.uxml` — scrim VE + root rename.
- `Wasteland Run/Assets/UI/MapView.uss` — `.wr-map-fade-scrim` styles.
- `Wasteland Run/Assets/Scripts/UI/MapViewController.cs` — fade coroutine + Show/Hide rewrite + IsInputLocked extension + OnDisable cleanup.

**Test surface impact:** Zero. No new public API surface on MapViewController; Show/Hide signatures unchanged; existing tests that call Show/Hide continue to work (fade is engine-side coroutine, EditMode tests without a MonoBehaviour host won't animate but won't fail either).

**Success criterion:** Bootstrap into a new run → map appears from black. Click a Combat beacon → map fades to black before Combat scene appears. Win Combat → map fades in from black. Same for Rest beacons. No visible pop or flash at any transition boundary.

### Post-playtest bugfix — exit fade lives at the click site, not in Hide()

Initial shape had `MapViewController.Hide()` running the 0→1 scrim fade in-place, invoked from `RunSceneOverlayHost.HandleBeaconChanged` at the moment `mapIsCurrent` flipped to false. Playtest surfaced: the fade played AFTER Combat became visible — the map's UIDocument panel stayed `display: Flex` during the 0.3s fade window, and if Combat's async scene load completed inside that window (typical for small Combat scenes), the black scrim faded in ON TOP of the just-appeared Combat.

**Root cause:** `RunSession.Advance` fires `OnBeaconChanged` synchronously, which triggers BOTH the map hide (fade start) AND the BeaconActivator async scene load in the same frame. The two race — if Combat loads faster than the fade completes, the fade lands over Combat.

**Fix:** Move the exit fade UPSTREAM of `Advance` — into the click site's onComplete callback.

- Added `MapViewController.BeginExitFade(Action onComplete)` — fades scrim 0→1 over `_fadeDurationSeconds`, invokes callback on complete. Starts from current opacity (so a partial fade doesn't snap).
- `MapViewController.Hide()` is now a snap-hide: sets root `display: None` immediately, cancels any in-flight enter-fade coroutine, resets scrim opacity to 0. No fade in Hide anymore.
- `RunSceneOverlayHost.HandleBeaconClicked` wraps the `AdvanceToNextBeacon` call in `BeginExitFade`:
  ```csharp
  _mapView.PlayBeaconTravelAnimation(..., onComplete: () => {
      _mapView.BeginExitFade(onComplete: () => {
          _host.AdvanceToNextBeacon(toIndex, HostAdvanceReason.PlayerChoice);
      });
  });
  ```

**New exit timeline:**

1. Click → 3s travel animation on map.
2. Travel done → `BeginExitFade` starts (map still visible under fading scrim).
3. Fade complete (+0.3s) → `AdvanceToNextBeacon` → `Advance` → `OnBeaconChanged`.
4. `HandleBeaconChanged` → `_mapView.Hide()` — snap root=None, scrim reset to 0.
5. BeaconActivator's async Combat load fires from same OnBeaconChanged handler chain. Combat scene loads and becomes visible unobstructed.

The fade now plays entirely within the map's visible frame — the moment it completes, the map disappears and Combat has room to render. No overlap.

**Non-fade paths unaffected:** `HandleStormEngulfed`, `HandleRunComplete`, and `HandleCombatReady` still call `_mapView.Hide()` directly (snap-hide). These paths never had a fade under the initial shape either (engulfment relies on the storm cinematic for its darkening; run-complete transitions to the summary screen).

**Files touched by post-playtest bugfix:**

- `Wasteland Run/Assets/Scripts/UI/MapViewController.cs` — Hide() → snap-hide; added BeginExitFade().
- `Wasteland Run/Assets/Scripts/CombatView/RunSceneOverlayHost.cs` — HandleBeaconClicked's onComplete wraps AdvanceToNextBeacon in BeginExitFade.

**Test-migration cost this third-pass addendum:** 1 assertion (`RunSession_PreviewBeaconArrival_Test.cs:181` `-1 → -8`). Zero production API changes. StormState surface unchanged. BeaconTravelTick payload unchanged (semantics of `PreviewedStormCounterAfter` shifted, but ctor + field types identical).

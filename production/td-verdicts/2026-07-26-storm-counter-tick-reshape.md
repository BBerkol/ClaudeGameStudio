# Storm Counter Tick Reshape — TD Verdict

**Date:** 2026-07-26
**Requester:** playtest-surfaced UX mismatch — user wants "small steps forward"
**Predecessor:** `2026-07-25-storm-cursor-spatial-pivot.md` (Shape B')
**Files at risk:** `StormState.cs`, `RunSession.cs`, `BiomeDistributionSO.cs`,
`Biome1Distribution.asset`, `BeaconTravelTick.cs`, `RunHUDController.cs`,
~11 EditMode tests.

## Context

Shape B' shipped same session. Playtest hit immediate mismatch: the counter
widget snaps by baseCost (Combat=8 → 24→16→8→0 in three commits with
CounterStart=24), not tick-by-tick. User directive:

> "the storm counter is going down but its not ticking down like the rest of
> the widgets ... 24 is very high. we need that down at 8 and like i said
> every storm step should be small steps forward."

Follow-up confirmed the wrap-on-zero pause-cinematic contract is correct:

> "when counter moves to 0 the the game should enter a storm progression pause
> cycle and move the storm forward."

## Proposal (as briefed)

Decouple storm counter from fuel base cost:

1. `StormState.AdvanceCounter()` parameterless, `Counter -= 1`, wraps to
   `CounterStart` on ≤0, returns 1 on wrap else 0.
2. `PreviewAdvanceCounter()` parameterless mirror; add `PreviewNextCounter()`
   returning the value that WOULD land (for HUD lerp payload).
3. `CounterStart 24 → 8` (SO default + `Biome1Distribution.asset` override).
4. Fuel drain unchanged (`FuelState.Spend(baseCost, chassisMultiplier)` intact).
5. `RunSession.Advance` + `PreviewAdvance` drop the baseCost arg from storm calls.
6. `BeaconTravelTick` grows `PreviewedStormCounterBefore/After : int` fields;
   `RunHUDController` lerps storm label during InFlight ticks like fuel.

## Technical Director Review

**Verdict: AMEND — accept the reshape, but keep the per-beacon-type storm
weight seam alive as data (not code) before we rip out the parameter.**

**Why not straight ACCEPT.** 1.0-survival lens answers itself. Biome 3
scenario ("Elite advances storm 2x") is a plausible near-term knob. Ripping
`baseCost` out of `AdvanceCounter` today, then re-threading it in ~3 months
when biome 3 lands, is exactly the transitional-shape churn
`demo_forward_over_infrastructure` says to avoid. User's directive is about
**defaults and feel** ("24 is high, get to 8, small steps"), not about the
parameter's existence.

**Three-lens audit.**

- **Health:** Parameterless overload is ADR-0011-clean *only* if we're certain
  no future caller needs the weight. We aren't. Also: `Preview` + `PreviewNext`
  split introduces a subtle naming trap — `PreviewAdvanceCounter()` returns the
  strip-wrap flag, `PreviewNextCounter()` returns the landed value. Two preview
  verbs with adjacent names and different return semantics is exactly the seam
  that gets miscalled six months from now. Rename to `PeekNextCounter()` to
  break the visual rhyme.
- **Optimization:** No delta. O(1) either way.
- **1.0 survival:** ADR-0015 (biome distribution as configuration narrowing) is
  the precedent. Per-beacon-type storm weight belongs on `BiomeDistributionSO`
  as a table, not baked into calls.

## Amendments (locked)

1. **Keep the parameter, default it to 1.**
   `AdvanceCounter(int stormCost = 1)` and `PreviewAdvanceCounter(int stormCost = 1)`.
   RunSession calls parameterless today; biome 3 adds a `StormCostPerBeaconType`
   lookup on `BiomeDistributionSO` and passes it through. Zero drift,
   ADR-0011-clean (default param ≠ bridge — it's a signature default).
2. **Rename `PreviewNextCounter` → `PeekNextCounter`.** Breaks the `Preview*`
   rhyme; makes "returns the landed value, not the wrap flag" obvious at call
   sites.
3. **CounterStart 24 → 8: confirmed.** Both SO default and asset.
4. **BeaconTravelTick payload additions: confirmed.** `PreviewedStormCounterBefore/After`
   as `int` fields on the `readonly struct` — ADR-0011 payload growth, safe.
5. **Save/persistence: confirmed no schema bump.** `StormStateDto` shape unchanged.
6. **Tests:** parameter survives with a default → most call sites don't need
   updating. Only assertions hardcoding the old `-8/-12/-4` arithmetic shift
   to `-1`.

## Success criterion

Biome 3 designer adds a `StormCost` column to a distribution SO and gets 2x
elite pressure without touching `StormState.cs` or `RunSession.cs`.

## Blocker

None. Ship with amendments. EditMode must be green before commit — no
compilation-green-as-proxy.

---

## Amendment 2026-07-26 (later same day) — REVERSE tick-by-1, restore tick-by-fuel-cost

**User feedback after tick-by-1 shipped:** "why is the storm counter not ticking
with fuel but ticking by how many nodes have crossed? this is wrong." Design
intent restored: storm counter drops by the destination beacon's FUEL BASE COST
(Combat=8, Merchant=4, Elite=12), NOT by uniform 1-per-commit. Preserves the
"harder encounters advance the storm faster" thematic read that tick-by-1
flattened.

### What stays

- `StormState.AdvanceCounter(int stormCost = 1)` — parameter name + default
  arg unchanged. The default is a SAFETY FALLBACK for future misuse; real
  callers pass explicit cost.
- `StormState.PreviewAdvanceCounter(int stormCost = 1)` — same.
- `StormState.PeekNextCounter(int stormCost = 1)` — same.
- CounterStart = 8 (SO default + Biome1 asset) — this is orthogonal to the
  tick-model debate. User confirmed 8 twice.
- HUD storm-pill InFlight lerp (`BeaconTravelTick.PreviewedStormCounter{Before,After}`,
  `RunHUDController` lerp branch) — MODEL-AGNOSTIC. Lerps Before → After
  regardless of whether After = Before-1 or Before-8.

### What changes

- `RunSession.Advance` line 228: `storm.AdvanceCounter(baseCost)` — pass real cost.
- `RunSession.PreviewBeaconArrival` line 308: `storm.PreviewAdvanceCounter(baseCost)`.
- `BeaconTravelPreview` gains **`StormCounterAfter` int field** — composed in
  `RunSession.PreviewBeaconArrival` where the beacon-fuel-cost table is
  visible. RunSceneOverlayHost reads `preview.StormCounterAfter` and drops
  the direct `storm.PeekNextCounter()` call at the click site.
- Test fixture assertions revert: `TicksStormCounterByOne` → back to
  `DrainsStormCounterByBaseCost` naming, `-1` → `-8` (Combat), Haven-reset
  test's "storm counter ticked by 1" sanity assertion → "ticked by 8".
- Priming shortcut in FiresOnStormAdvanced test: `AdvanceCounter(TestStormCounterStart - 1)`
  → priming that lands counter such that the next Combat commit (cost=8)
  crosses zero. With CounterStart=8 and Counter=8 at start, ONE Combat commit
  already wraps → no priming needed.

### Files touched by amendment

- `RunSession.cs` (2 call sites) — no xmldoc touch, no verdict re-fire
- `BeaconTravelPreview.cs` (new field) — triggered THIS amendment
- `RunSceneOverlayHost.cs` (drop PeekNextCounter call, read preview instead)
- `RunSession_Fuel_Test.cs` (~6 assertions revert)
- `RunSession_PreviewBeaconArrival_Test.cs:180` (`-1` → `-8`)
- Polish capture addendum (2026-07-25-storm-pause-event.md) — update the
  tick-by-1 addendum to reflect the reversal.

### Why the parameter stays (unchanged from main verdict)

The `stormCost` parameter with default=1 remains the biome-3 lagging-dep seam
per ADR-0015. Biome 3 designer authoring a per-beacon-type storm-weight table
still doesn't need to touch `StormState.cs` — only how the caller composes
the argument. Reverting to tick-by-fuel doesn't invalidate the seam; it just
picks fuel-cost as the "storm weight" formula for biome 1.

### Rehabilitated read

Under CounterStart=8 with tick-by-fuel:
- Combat (cost 8): 8 → 0/wrap → strips=1, counter → 8. Every Combat = 1 strip.
- Merchant (cost 4): 8 → 4 → 0/wrap → 2 Merchants per strip.
- Elite (cost 12): 8 - 12 = -4 → wrap → strips=1, counter=4. Every Elite = 1 strip + partial credit.

Widget lerp during travel window: label smoothly tweens 8→0 (Combat) or
8→4 (first Merchant) or 4→8-post-wrap (Elite), etc. HUD renders the whole
integer sweep across the 1.5-4s travel window in phase with the fuel-pill
number — cures the original "counter snaps at Arrive" complaint.

### Verdict on amendment

**ACCEPT.** User directive is unambiguous; tick-by-1 was my misread of "small
steps forward" (visible cadence, not literal unit ticks). Reversal is
mechanically cheap (~5 call sites) and restores the design's fuel-cost-as-
storm-weight relationship that the biome-3 hypothetical assumed all along.

---

## Amendment 2026-07-26 (third pass) — Countdown-timer widget shape

**User feedback after tick-by-fuel-cost reversal shipped:** "the storm counter
should look like a timer that is going down, once it reaches 0 the storm
actioncycle should take affect. every time it hits 0 the its storms turn to
move."

**Root cause of the mismatch:** post-reversal, `PeekNextCounter(baseCost=8)`
under `Counter=8, CounterStart=8` returns 8 (the post-wrap reset value). The
widget lerped 8→8 across the travel window (invisible), then any subsequent
`SnapshotStormCounter()` on Arrive re-read live=8 and produced no visual
transition. The countdown-to-zero moment was never rendered.

### What changes

- **`RunSession.PreviewBeaconArrival`** (non-Haven branch): compose
  `stormCounterAfter = stormStrips > 0 ? 0 : storm.PeekNextCounter(baseCost)`.
  On strip-firing commits the widget-visible endpoint becomes the pre-wrap
  zero-crossing (0), not the post-wrap reset (CounterStart). On partial-spend
  commits (Merchant=4 against Counter=8 → next=4) `PeekNextCounter` still
  produces the reduced value.
- **`RunHUDController.HandleBeaconTravelTick.Arrive`**: if
  `tick.StormAdvanceStrips > 0`, DO NOT call `SnapshotStormCounter()` — the
  widget stays at 0 through the storm-cursor cinematic. On non-strip commits
  (partial spend / Haven refill) snap immediately per prior contract.
- **`RunHUDController.HandleStormAdvanced`**: schedule the reset snap
  (`SnapshotStormCounter()`) to fire after `tick.DurationSeconds` — the widget
  visually rests at 0 while the storm cursor sweeps, then snaps back to
  CounterStart in phase with the cinematic completion. Same schedule handle as
  the existing red-flash pulse.

### Visual sequence

1. **Depart** (t=0): widget = 8 (`PreviewedStormCounterBefore`).
2. **InFlight** (0<t<1): widget lerps 8→0 across the travel window (drainCurve
   feel-shaped).
3. **onComplete → Advance**: model commits, `OnStormAdvanced` fires with
   `DurationSeconds = SecondsPerBeacon` (pacer-injected). Red flash starts;
   reset snap SCHEDULED for `+SecondsPerBeacon`.
4. **Arrive** (t=1): widget stays at 0 (strip-firing branch skips snap).
5. **cinematic** (`+SecondsPerBeacon` seconds): storm-front element sweeps
   its arc via `StormMapVisualHost`; widget holds at 0.
6. **cinematic end**: scheduled snap fires → `SnapshotStormCounter()` reads
   live=CounterStart → widget resets to 8 for next cycle.

### What stays

- All `StormState` primitives (`AdvanceCounter`, `PreviewAdvanceCounter`,
  `PeekNextCounter`, `ResetCounter`) unchanged — this is a compose-site fix
  on the two RunSession/RunHUDController call sites, not a primitive reshape.
- `stormCost` parameter default=1 on StormState methods — ADR-0015 lagging-dep
  seam preserved (biome-3 storm-weight table still lands here).
- `CounterStart=8` — unchanged.
- Fuel-pill lerp — unchanged. Fuel-cost-as-storm-weight relationship intact.

### Files touched by amendment

- `RunSession.cs` (compose-site tweak, ~3 lines under the non-Haven branch).
- `RunHUDController.cs` (Arrive branch skip + HandleStormAdvanced scheduled
  snap).
- `RunSession_PreviewBeaconArrival_Test.cs:181` — assertion revert continues
  (`-1` → `-8` still valid; assertion targets LIVE `storm.Counter` post-Advance
  which is model-side and unaffected by the widget-visible compose change).
- Polish capture 2026-07-25-storm-pause-event.md — final addendum reflecting
  the countdown-timer shape.

### Why compose-site not primitive change

`PeekNextCounter` is a pure math peek of the post-wrap value — its behavior
matches the model. The widget's needs (show zero-crossing before wrap) are
presentation-shape, not model-shape. Adding a `PeekPreWrapValue()` primitive
would ship a widget-shaped primitive on the model — a bridge in the ADR-0011
sense. Composing the widget's visible endpoint at the session boundary keeps
the model clean and lets the widget concern stay at the widget's own seam.

### Verdict on third-pass amendment

**ACCEPT.** User directive is now precise ("timer counting down to 0"); shape
is presentation-only (no model API changes, no test surface changes beyond
prior `-1`→`-8` revert). Ships with the same pivot bundle.

---

## Amendment 2026-07-26 (fourth pass) — Deferred-cinematic queue at pacer

**Requester:** user playtest immediately after third-pass amendment landed.

> "the storm is not advancing right now ... this storm advancement cycle
> should be remembered and then once the player is out of the node
> interaction, the game should pause, and the storm arc should move forward"

### Root cause

`RunSession.Advance` fires `OnStormAdvanced` **synchronously** inside the
non-Haven commit path. `RunSceneHost.AdvanceToNextBeacon` calls
`session.Advance(next)` and then raises `OnBeaconChanged` in the same call —
which for a Combat/Rest/etc. beacon flips the scene to the node-interaction
overlay and hides the map. The visual sweep starts against a hidden map. The
player never sees it. Counter-widget lerp still works (fires from
`OnBeaconTravelTick` during the 3s pre-Advance animation) — the storm-cursor
sweep is what goes invisible.

Diagnosis matches the countdown-timer amendment landing correctly (widget
lerps 8→0 on Depart→InFlight→Arrive), then the model advance firing into a
frame where nothing on the map is visible to receive it.

### Shape

Queue-and-flush pattern at `StormAdvanceVisualPacer`:

- `HandleSessionStormAdvanced(modelTick)` → **enqueue**, do not raise
  immediately.
- `HandleSessionStormEngulfed()` → **flag** `_pendingEngulfment`, do not
  raise immediately.
- Subscribe to `RunSceneHost.OnBeaconChanged` and `OnRewardClaimed`; both
  route to `HandleMapPossiblyReturned` which calls `TryFlushPending()`.
- `TryFlushPending` gates on: not already flushing, has pending work, and
  `IsMapCurrent()` (predicate: `CurrentBeacon == null || Type == Start ||
  IsResolved`).
- `FlushPendingSequential` coroutine dequeues each tick, raises via
  `RaiseStormAdvanced` with pacer-injected `DurationSeconds =
  SecondsPerBeacon`, waits the cinematic window, then advances. After all
  ticks drain, if engulfment is flagged, raise `RaiseStormEngulfed` last so
  the game-over overlay lands on top of settled paint (preserves TD
  Amendment A3 ordering).
- Re-guard on `IsMapCurrent()` inside the loop so a Haven-refill →
  Combat-click chain suspends mid-flush and re-arms on the next map return.

### Why here, not at the session

- Session owns model truth (persistent cursor, single-source rule
  `project_no_bridges_at_done`). It should NOT know about map visibility.
- `StormAdvanceVisualPacer` already owns the wall-clock cinematic window
  (existing `SecondsPerBeacon` injection). Adding "know when the map is
  visible so we can raise the sweep against it" is the same shape of
  concern — presentation-time gating around the model's synchronous event.
- Zero test-surface impact. Session tests still assert `OnStormAdvanced`
  fires synchronously with a zero-duration tick. Pacer is engine-side only.

### Verdict on fourth-pass amendment

**ACCEPT.** Correctly located (pacer, not session). ADR-0011 clean (no
bimodal path — the immediate-raise branch is deleted, deferred is the only
path). Success criterion: click a beacon that fires a strip, resolve the
node (Combat victory or Rest), verify storm arc sweeps AFTER map returns,
verify counter widget snaps 0→CounterStart in phase with the sweep end.

## Fifth-pass amendment (2026-07-26 same-day, second reversal): sticker=drain=timer

### Context

Same session, after the tick-by-1 shape was already in code. User surfaced:

> "i move to a node that costs 5 but the timer reduces 6"

Bug #37 triage flagged three options:

- **A. Chassis-neutral:** storm drops by raw `baseCost`; sticker sticks; fuel
  and timer diverge (Scout 8-cost node → fuel -6, timer -8). Storm is a
  chassis-invariant "world clock." User initially picked this.
- **B. Chassis-multiplied:** storm drops by the chassis-multiplied fuelDrained
  (Scout 8 → fuel -6, timer -6). Sticker cost, fuel-pill delta, storm-timer
  delta all match. Rebalance path is per-chassis cost tables (native match) or
  the multiplier itself, NOT a separate storm knob.
- **C. Sticker + discount UI:** show raw baseCost as sticker with a chassis-
  discount pill. Most information, most UI weight.

User reversed A → B same day with rationale:

> "the costs and the reductions should be the same it doesnt make sense fuel
> losing 5 for a 5 cost node but the storm timer reducing 6"

### Files at risk (scope of the second reversal)

- `RunSession.cs` — `Advance` passes `fuelDrained` (not `baseCost`) to
  `storm.AdvanceCounter`; `PreviewBeaconArrival` mirrors the same.
- `FuelState.cs` — xmldoc refresh only: the `<para>` note on `Spend` said
  "chassis-neutral base cost, no multiplier" (accurate under Shape A); needs
  to reflect "sticker=drain=timer, chassis-multiplied FuelDrained fed through."
- `StormState.cs` — xmldoc refresh only (POCO itself is chassis-agnostic).
- `BeaconTravelPreview.cs`, `BeaconTravelTick.cs` — xmldoc refresh only
  (structs unchanged; strip count still an integer zero-crossing count).
- `RunSession_Fuel_Test.cs`, `RunSession_PreviewBeaconArrival_Test.cs`,
  `StormState_Test.cs`, `BeaconTravelTick_Test.cs` — assertions + doc refresh.
- Memory: `project_storm_counter_chassis_neutral.md` deleted;
  `project_storm_counter_sticker_drain_timer.md` written in its place.

### Technical Director Review — fifth-pass amendment

**Verdict: ACCEPT.** User-owned design call; no ADR/contract shift needed.

- Sticker/fuel/timer alignment is a legibility win that outweighs the
  chassis-invariance argument at biome-1 scope. The multi-chassis rebalance
  path (per-chassis cost tables) is preserved and cleaner than a split
  storm-knob world would be.
- Doc refresh across `FuelState.cs`, `StormState.cs`, `BeaconTravelPreview.cs`,
  `BeaconTravelTick.cs` is required — the previous "chassis-neutral" wording
  is now actively misleading. No code churn on those types beyond xmldoc.
- Two `RunSession_Fuel_Test.cs` assertions previously relied on `baseCost=8`
  wrapping the counter; under drain=6 they must assert `Counter=2` (no wrap).
  StormState direct-integer tests unaffected.
- Memory swap is required (Option A memory locks the opposite decision).

Success criterion: Combat commit shows sticker N, fuel pill drops N, storm
counter drops N — three numbers, one delta.

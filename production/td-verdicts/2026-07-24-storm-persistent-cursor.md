# TD Verdict — Out-of-Fuel V3: Persistent Storm Cursor

**Date:** 2026-07-24
**System:** Storm engulfment (out-of-fuel game-over path)
**Author:** Technical Director
**Status:** APPROVED WITH AMENDMENTS
**Supersedes (partially):** `2026-07-06-out-of-fuel-gameover.md`, `2026-07-24-storm-engulfment-gameover-hook.md`, `2026-07-24-storm-map-visual-host.md`, `2026-07-24-storm-front-arc-reshape.md`

## Technical Director Review

**Verdict: ACCEPT WITH AMENDMENTS.** The pivot is correct. The current shape (V2)
carries `StormAdvanceStrips` through `BeaconTravelTick` with zero consumers on
the other end — that is textbook ADR-0011 drift: a payload field authored for a
future consumer that never landed, sitting alongside a *presentational* doom
coroutine that pretends to be the storm but restarts from zero on resume. The
user's mental model is the correct 1.0 shape and the current shape is scaffolding
around a misimplemented spec. Ship the persistent cursor now, before another
slice cements the coroutine-only path.

That said, the pivot has real coupling costs — six load-bearing pieces of state
and event surface exist under the V2 contract and every one has downstream
subscribers already wired. This verdict answers all six of your questions,
resolves the migration order, and calls out three amendments that keep the
transition ADR-0011-clean.

---

## Question 1 — Cursor home

**Recommendation: New `StormState` POCO under `WastelandRun.Run`, held on `RunState.Storm`.**

Rejected alternatives:

- **On `FuelState`.** Tempting because "storm advance is a fuel-consumption
  consequence" — but `FuelState` is the *tank*, and its invariants
  (`Current`, `Max`, refill+reset semantics) are internally consistent. The
  storm cursor is graph-indexed, not fuel-indexed, and requires
  `INodeMapView` to advance (Q5). Wiring `INodeMapView` into `FuelState`
  would grow its charter past "tank arithmetic" — Lens 1 single-responsibility
  fail. Additionally, `FuelStateDto` is a group-of-one non-blocking DTO
  (per its own xmldoc — "recoverable cosmetic loss"); a missing storm
  cursor is emphatically *not* recoverable-cosmetic (storm at index 0 on
  resume changes the entire tactical picture), so the exhaustion policies
  do not match.

- **Field on `RunState` directly (e.g., `StormBeaconIndex`).** Works
  functionally but bloats `RunState`'s scalar surface and forces every
  future storm concern (advance policy, retreat-on-Haven, spike-on-stranded
  scaling) to add another top-level field. `StormState` is a two-int POCO
  today (`CursorIndex` + `TargetIndex` — see Amendment A below); making it
  a class from day one gives future concerns a home.

- **`System.Random` requirement.** Confirmed: **no RNG needed.**
  `StormState.Advance(int strips)` is `_cursor += strips` — deterministic
  from `FuelState.Spend`'s already-deterministic strip count. ADR-0003
  compliance is satisfied without threading a `System.Random` through.

**Shape:**

```csharp
namespace WastelandRun.Run
{
    public sealed class StormState
    {
        public int CursorIndex { get; private set; }
        public int InitialCursorIndex { get; }

        public StormState(int initialCursorIndex)
        {
            if (initialCursorIndex < 0)
                throw new System.ArgumentOutOfRangeException(...);
            InitialCursorIndex = initialCursorIndex;
            CursorIndex = initialCursorIndex;
        }

        public void Advance(int strips) { /* clamp, mutate */ }
        public void RetreatToHavenSpawn(int havenBeaconIndex) { /* see Q4 */ }
        public void RestoreFromSnapshot(int cursorIndex) { /* save path */ }
    }
}
```

Live at `RunState.Storm` (internal-set, like `Fuel`); constructed inside
`RunController.StartRun` alongside `FuelState`.

**Save DTO:** new `StormStateDto` at `run.storm_state`, `SCHEMA_VERSION = 1`,
group-of-one **but joined to `run.session_core` resume-atomic group**
(not standalone like `FuelStateDto`). Rationale: a resume that rehydrates
`NodeMap` + `RunSeed` + `RunDeck` but silently drops storm cursor lands
the player at, say, beacon 6 with storm-at-0 — meaning the storm effectively
"resets" from the player's perspective on every crash-and-resume, which is
exactly the kind of silent-progression-invariant break that the
session_core group exists to prevent. ADR-0004 Slice 8d Amendment
membership test: "absence-with-others-present creates a silently-broken
determinism or progression invariant" — passes. Bump the session_core
group version accordingly.

---

## Question 2 — Increment call site + initial position

**Recommendation: `StormState.Advance(strips)` mutator, called inline from
`RunSession.AdvanceToNextBeacon` using the strip count already returned by
`Fuel.Spend`.**

The mutator lives on `StormState` (not inlined `_state.StormBeaconIndex +=`)
so that any future clamping / event / logging concern has a single choke
point. `RunSession.Advance` becomes:

```csharp
if (arrived.Type == BeaconType.Haven)
{
    fuel.RefillPartial(_havenFuelRefillPercent);
    fuel.ResetStormOnHaven();
    // See Q4 for the storm-retreat call here.
}
else
{
    int baseCost = _beaconFuelCosts[(int)arrived.Type];
    FuelSpendResult result = fuel.Spend(baseCost, _chassisFuelBurnMultiplier);
    _controller.State.Storm.Advance(result.StormAdvanceStrips);
}
```

**Initial cursor position.** Storm starts at a **new
`BiomeDistributionSO.InitialStormBeaconIndex` field** (int, default `-3` for
Biome 1, `OnValidate` clamp `>= -20 && <= 0`). Rationale:

- Starting at `0` (Start beacon) means the storm is *on top of the player*
  at Depart. First non-Haven step drains 1 strip → storm at 1, player at 1
  → **instant game-over**. This is wrong.
- A negative sentinel (`-N` = "N beacons off-map to the left") gives you a
  visual runway before the storm reaches beacon 0, plus a
  designer-configurable "grace distance." `MapViewController` treats
  negative indices as "left of the leftmost beacon" — see Amendment B for
  the visual clamp.
- Configuring per-biome (via SO) lets Biome 2 start the storm closer for
  difficulty ramp without a code change (ADR-0015 lagging-dep pattern).
- ADR-0011 clean: not a stub, not a bridge, not a bimodal path — a
  configuration knob at the data table.

Persist `InitialCursorIndex` in `StormStateDto` per FuelState-snapshot
precedent so a mid-run SO retune of the initial grace distance does not
retroactively teleport an in-flight storm.

---

## Question 3 — Doom coroutine interaction

**Recommendation: Option B, with the doom coroutine repurposed as a
visual-lerp driver that fires on *every* `Fuel.Spend` result whose
`StormAdvanceStrips > 0`, not just stranding.** Reworded: **delete the
private `StormCursorTicker` walk; keep the coroutine as a per-tick
`WaitForSeconds` pacer that reads model-side `StormState.CursorIndex` and
lerps the visual apex.**

Rejected alternatives:

- **Option A (synchronous game-over from `AdvanceToNextBeacon`).** Would
  work for the game-over trigger but drops the visual arc lerp entirely.
  Under the persistent-cursor model, the storm advances during **every**
  beacon commit that produces strips > 0 — not only during stranding — so
  the arc needs to animate between beacon positions on regular travel too,
  not just on doom. Option A leaves the visual as an instant snap on every
  advance, which reads as broken.

- **Option C (persistent cursor for state + keep stranded-doom coroutine
  as separate mechanic).** This is the ADR-0011 #3 bimodal-path trap. Two
  storm-advance systems ("normal creep from fuel spend" + "spike-forward
  on stranded") coexisting means every future storm-touching feature has
  to decide which system to hook. Reject.

**Amendment C — retire `StormCursorTicker` outright.** The pure POCO
walk-along-forward-edges primitive lives inside `Fuel.Spend`'s strip count
now (chassis-neutral integer). The `StormCursorTicker`'s job — walk
forward from index 0 toward the player at `SecondsPerBeacon` cadence — is
no longer needed; the model advance is discrete (Q5), the visual lerp is
between `StormState.CursorIndex - strips` and `StormState.CursorIndex` on
each advance event, and the ticker's `INodeMapView` dependency was a
symptom of the "coroutine owns cursor state" misimplementation. Delete
`StormCursorTicker.cs` and its EditMode test.

**Repurposed coroutine.** `StormEngulfmentController` gets renamed to
`StormAdvanceVisualPacer` (or similar — Amendment D discusses naming),
subscribes to a new `RunSceneHost.OnStormAdvanced(StormAdvanceTick)` fired
after every `_state.Storm.Advance(strips)` call. Each event triggers one
coroutine window of `SecondsPerBeacon × strips` seconds during which
`StormMapVisualHost` lerps `StormFrontElement.SetApex` from
old-cursor-position to new-cursor-position. No walk-along-edges logic in
this class anymore — it just paces the visual against the model.

**Game-over trigger.** Fires synchronously inside `RunSession.Advance`
after `StormState.Advance` runs, iff `Storm.CursorIndex >= Player.CurrentIndex`.
The `RaiseStormEngulfed` event fires from the host after the visual pacer
completes its last lerp so the player sees the arc arrive at their beacon
before the game-over overlay shows (`AMEND A3` from
`storm-engulfment-gameover-hook.md` preserved).

---

## Question 4 — Haven retreat

**Recommendation: Haven arrival retreats storm cursor to
`havenBeaconIndex - RetreatBeacons` where `RetreatBeacons` is a new
`BiomeDistributionSO` field (default 2 for Biome 1, OnValidate clamp
`[0, 5]`).**

Rationale (user's model has been ambiguous, so pick the shape that gives
the most designer control without over-committing):

- **Retreat is required for the mechanic to be interesting.** If Haven is
  a fuel top-up only and storm continues creeping unhindered, Haven is
  strictly a fuel-arithmetic decision — go to Haven only when you can't
  afford the next step. But if Haven *also* pushes the storm back, Haven
  becomes a strategic tempo tool — "I'll take the Haven detour to buy
  breathing room even though I could afford to skip it." That's a much
  richer decision space and aligns with your run-scoped economy design.

- **Retreat amount is a designer knob, not a magic number.** 2 beacons
  matches the current V3 spec (`StormCounterStart = 30`,
  Combat/Elite fuel-cost 8-12 → storm advances ~1 strip per 3 beacons →
  Haven refund of 2 = ~6 beacons of runway). Ship it as
  `BiomeDistributionSO.HavenStormRetreatBeacons` so playtest tunes it
  without a code change (ADR-0015 lagging-dep pattern).

- **Clamp: never retreats past `InitialCursorIndex`.** `StormState.RetreatToHavenSpawn`
  computes `Math.Max(InitialCursorIndex, havenBeaconIndex - retreatBeacons)`
  so the storm can't retreat all the way off-map into "grace distance"
  territory and undo the whole tension arc. Also never advances the
  cursor — if the player somehow reaches a Haven that's *behind* the
  storm cursor, the retreat is a no-op (never a bimodal
  advance-or-retreat mutator).

`RunSession.Advance`'s Haven branch grows:

```csharp
fuel.RefillPartial(_havenFuelRefillPercent);
fuel.ResetStormOnHaven();
_controller.State.Storm.RetreatToHavenSpawn(arrived.Index, _havenStormRetreatBeacons);
```

The `_havenStormRetreatBeacons` field is constructor-injected into
`RunSession` from the SO, same pattern as `_havenFuelRefillPercent`.

---

## Question 5 — Path arithmetic

**Recommendation: Storm cursor is a scalar beacon-index integer that
increments by strip count, WITHOUT walking graph edges. Interpretation A
(flat index++).**

Rejected: Interpretation B (walk forward-edges toward the player). Reasons:

- **The player's mental model is "the storm is coming," not "the storm is
  chasing me down my exact path."** In a branching graph, the player
  doesn't have a *specific* forward path — they have a *frontier*. If the
  storm walked "the road I'm on" it'd have to pick one edge, and every
  time the graph branches the choice would look arbitrary.

- **Beacon-index correspondence with player position.** `NodeMap`
  guarantees `Beacons[N].Index == N` and forward edges only go
  index-ascending (per the `AllowBidirectional` shape). So
  `Storm.CursorIndex >= Player.CurrentIndex` is a valid game-over
  predicate under Interpretation A too — the player's beacon index
  monotonically increases, and the storm's does the same.

- **Visual coherence.** `StormFrontElement.SetApex` takes a normalized-X
  in `[0..1]` over the map layer. If the storm cursor is a flat index,
  the visual apex is `_beaconPositions[cursorIndex].x` — the storm's
  X-coord is *whichever beacon happens to be at that flat index*. In a
  cluster-lane graph (2026-07-07), that may not correspond to any
  player-reachable beacon on the same lane. **This is the risk of
  Interpretation A** and Amendment B addresses it.

**Amendment B — Storm visual reads a computed "front X" from
`MapViewController`, not directly from `_beaconPositions[CursorIndex].x`.**
The controller exposes a `float StormFrontX(int cursorIndex)` helper that:

1. Returns 0f (left edge) for `cursorIndex < 0` (grace distance).
2. Returns `_beaconPositions[cursorIndex].x` for a valid index.
3. For cluster-lane graphs where multiple beacons share close X-coords,
   linearly interpolates between the two beacon X-coords straddling
   `cursorIndex` if the immediate neighbors have very different X.
   (Deferred impl — Biome 1 is single-lane so the simple form works.)

This keeps the storm visual coherent across future generator shapes
without recomputing model-side. Interpretation A is preserved for the
model — the storm ticks discretely on the integer index — but the visual
maps that integer to a continuous X for the arc apex.

---

## Question 6 — Game-over trigger + event retirement

**Recommendation: Retire `OnAutoStormBegan` entirely. Rename
`OnStormEngulfed` semantics to "storm cursor reached player." Add
`OnStormAdvanced` as the sole storm-progress signal for both visual and
recovery-chance systems.**

Concretely:

- **`OnAutoStormBegan` deletes.** Under the persistent-cursor model, there
  is no "auto storm began" moment — the storm has been on the map since
  run start. The V2 shape needed this because the storm was a coroutine
  that literally started on a trigger; the V3 shape has no such trigger.
  Analytics / audio hooks that wanted "player has entered doom range"
  belong on a new predicate (e.g., `RunSession.StormTicksUntilEngulfment`
  = `Player.CurrentIndex - Storm.CursorIndex`) — subscribers roll their
  own threshold. **ADR-0011 clean: no dormant event.**

- **`OnStormEngulfed` stays but re-semantics.** Fires from
  `RunSession.Advance` synchronously when `Storm.CursorIndex >=
  Player.CurrentIndex` after the advance. `RunSceneOverlayHost` already
  subscribes to it and shows the game-over overlay one frame later
  (preserves `AMEND A3` deferred-view-show).

- **`OnStormAdvanced(StormAdvanceTick)` becomes the single per-tick signal.**
  Fires from `RunSession.Advance` after every non-zero strip advance
  (regardless of whether the player is stranded). Consumers:
  1. `StormMapVisualHost` — lerps the arc.
  2. Recovery-chance systems (wandering merchant, help event faucets — future) —
     roll per-tick chances weighted by `(TargetIndex - ToIndex)` gap.
  3. Optional Slice F storm-strip renderer.

- **`OnStormStopped` deletes.** Under V2 it was the "storm coroutine got
  cancelled by recovery-chance" signal. Under V3 there is no coroutine to
  cancel — recovery events just credit fuel via `FuelState.CreditFuel`
  and the storm keeps advancing at its normal cadence. If a future
  design decision *does* need "spike the storm forward when stranded"
  (Option C from Q3 as a mechanic layer), it's a *separate*
  `StormState.SpikeForward(int strips)` mutator, not an event.

- **`RunSession.IsStrandedForFuel()` stays, but as a UI predicate only.**
  Used by the map view to red-tint unaffordable beacons (already
  implemented). No longer a trigger — the game-over path is
  cursor-vs-cursor now, and stranding is just the state where the storm
  will eventually reach you because you can't outrun it. This matches
  ADR-0011 clean: predicate ≠ event, no dormant surface.

---

## Amendments Summary

- **Amendment A — `StormState` POCO** on `RunState.Storm`, with
  `CursorIndex` + `InitialCursorIndex` fields and `Advance` /
  `RetreatToHavenSpawn` / `RestoreFromSnapshot` mutators. Save DTO joins
  `run.session_core` group.
- **Amendment B — `MapViewController.StormFrontX(int)`** helper for
  visual coherence in future cluster-lane / multi-lane graphs.
- **Amendment C — Delete `StormCursorTicker.cs`** and its test; delete the
  private walk logic in `StormEngulfmentController`. Rename the
  controller to `StormAdvanceVisualPacer` (or keep the name if you prefer
  minimal churn — the class shrinks to "listen for OnStormAdvanced, pace
  the visual lerp"). Its coroutine now runs on tick events, not on a
  standing timer.
- **Amendment D — SO surface growth.** `BiomeDistributionSO` gains
  `InitialStormBeaconIndex` (int, default -3) and
  `HavenStormRetreatBeacons` (int, default 2). Both with `OnValidate`
  clamps. Feed into `RunSession` constructor.

---

## Three-Lens Self-Audit

**Lens 1 — Codebase Health**

- **ADR-0011 drift the pivot RESOLVES:** V2's `StormAdvanceStrips` field
  on `BeaconTravelTick` with zero read-back consumers is exactly the
  "carrier for a future consumer that never arrived" trap. Pivot removes
  that (or repurposes it — Amendment E below). V2's `StormCursorTicker`
  as a "pure POCO test seam" for a walk that's now unnecessary is dead
  code post-pivot. Delete it, don't keep it "just in case."
- **ADR-0011 drift the pivot COULD INTRODUCE:** dual-cursor anti-pattern
  if `StormState.CursorIndex` and the visual `StormFrontElement._apexX`
  ever get out of sync during save-resume. Mitigated by making the
  visual read model on Bind, not cache — `MapViewController.Bind`
  reads `_host.State.Storm.CursorIndex` and calls
  `_stormFront.SetApex(StormFrontX(cursor))` unconditionally.
- **Subscription lifecycle:** `StormMapVisualHost` uses OnEnable/OnDisable
  pair for host-published events — correct per
  `feedback_subscription_lifecycle_pairing` since the host lives on
  Run.prefab root, not a SetActive-cycled child.
  `RunSceneOverlayHost.OnStormEngulfed` subscription: same, OnEnable/OnDisable
  pair, correct. The new `OnStormAdvanced` subscription on
  `StormMapVisualHost` inherits the same pair. **No subscription-lifecycle
  regression.**
- **Single-responsibility:** `StormState` owns storm arithmetic and is
  the single seam for future storm concerns. `FuelState` stays as tank.
  `RunSession.Advance` grows two lines (Storm.Advance + game-over check).
  All within charter.
- **Duplication vs premature abstraction:** the "compute storm strips
  from base cost" arithmetic already lives in `FuelState.ComputeDrain`
  neighbor. Not adding a helper — the model call site is single (`RunSession.Advance`).
- **Teardown races:** No coroutine holding cursor state anymore, so the
  Alt+F4-mid-doom race that V2 mitigated by "coroutine restarts from 0
  on resume" goes away. Storm cursor is persisted; visual re-hydrates
  from model on Bind. Cleaner.

**Lens 2 — Optimization**

- **Event cadence:** `OnStormAdvanced` fires at most once per
  `AdvanceToNextBeacon` call — that's per-user-click, not per-frame.
  Vastly cheaper than V2's `SecondsPerBeacon` coroutine ticking every
  1.5s regardless. Zero optimization concern.
- **Allocation per invocation:** `StormAdvanceTick` is already a
  `readonly struct` — no boxing. `StormState.Advance(int)` is a scalar
  mutation. `MapViewController.StormFrontX(int)` is an array index +
  optional lerp. No LINQ, no delegate allocations, no string operations.
- **Cache/style recomputation:** `StormFrontElement.SetApex` already
  uses `MarkDirtyRepaint()` per-call — that's the correct UI Toolkit
  path. No USS class toggles on the advance path. The visual lerp
  coroutine sets `_stormFront.SetApex(Mathf.Lerp(...))` per frame
  during the ~1.5s window — same cost as today.
- **Non-goal: don't over-optimize.** The storm visual lerp coroutine
  runs `SecondsPerBeacon` seconds per storm advance, which happens ~1
  per 3 beacons player advances. At most one active coroutine at a time.
  Cost is negligible.

**Lens 3 — 1.0-Shape Survival**

- **Will this signature / struct / event payload survive to 1.0?** Yes,
  intentionally. `StormState` as a POCO with `Advance` / `RetreatToHavenSpawn`
  / `RestoreFromSnapshot` is the canonical 1.0 shape. `StormAdvanceTick`
  as a readonly struct already carries FromIndex/ToIndex/TargetIndex/DurationSeconds
  and downstream recovery-chance systems can hook it without signature
  growth. **Payload extension seam is `readonly struct` — future fields
  add cleanly.**
- **Anticipating downstream subscribers.** Wandering merchant / help-someone-out
  faucets are called out as design intent. They'll credit fuel via
  `FuelState.CreditFuel(int)` and won't need to touch `StormState` at all —
  the affordability set expands, `IsStrandedForFuel` returns false,
  but the storm cursor keeps advancing at its normal cadence (no
  spike-forward-on-stranded mechanic in the 1.0 shape). **Recovery-chance
  systems land as pure ScrapEconomy/FuelState hooks — no storm coupling.**
- **Stopgap visuals surviving to 1.0.** The arc-front visual, the
  storm-front USS palette, and the storm-layer topology already survive
  from V2 — they're the same rendering path. The persistent-cursor
  pivot changes *when* the arc paints, not *how*. Zero stopgap-visual
  risk.
- **Cancellation / re-entry / edge behaviors.** Two edge cases to lock:
  1. **Save mid-hop (during travel coroutine).** The travel coroutine
     mutates model only at `onComplete`. If the player Alt+F4s mid-hop,
     resume lands on pre-hop state — storm cursor is pre-hop too. Correct.
  2. **Player reaches Haven with storm already past Haven's index.**
     `RetreatToHavenSpawn` clamps to `max(InitialCursorIndex,
     havenIndex - retreatBeacons)` and never advances. If storm is at
     index 8, Haven is at 6, retreat = 2 → storm clamps to `max(-3, 4) = 4`.
     Storm retreats even if Haven is behind it. **Design intent:
     yes** — this is the "Haven is a strategic tempo tool" shape.
     Confirm this is what you want. If not, change clamp to
     `havenIndex >= Storm.CursorIndex → no-op`.

**Confirmed, no delta** on Lens 2. **Deltas surfaced** on Lens 1
(delete `StormCursorTicker`, dual-cursor guard on `Bind`) and Lens 3
(Haven-past-storm clamp policy needs a design call — recommend `max`
clamp per rationale above).

---

## Amendment E — `BeaconTravelTick` storm fields

`BeaconTravelTick.PreviewedStormBefore` / `PreviewedStormAfter` /
`StormAdvanceStrips` were pre-populated in V2 for the never-shipped Slice F
storm-strip renderer. Under V3, they represent *storm cursor* before/after,
not *storm counter* before/after (different semantics). Two options:

- **E1 — Repurpose:** rename `PreviewedStormBefore` → `PreviewedStormCursorBefore`
  (etc.), compute from `PreviewBeaconArrival` + persistent cursor. Populate
  every tick per the "every tick carries preview values" contract. Consumer:
  `StormMapVisualHost` (already exists) uses the Arrive tick's after-value
  to pace its lerp against.
- **E2 — Delete:** if the visual pacer works entirely off `OnStormAdvanced`
  events (not travel ticks), drop the fields.

**Recommend E1.** The travel tick's contract ("every tick carries
fully-populated preview values so subscribers lerp locally") is the right
1.0 shape and per-frame stormBefore/stormAfter preview lets a future
"tank-and-storm HUD widget" animate its storm forecast during travel
without dipping into live model. E2 sacrifices a real 1.0 seam to avoid
a rename — bad trade.

Rename is a one-shot migration (ADR-0011 exception #1). Ship in the same
commit as `StormState` introduction.

---

## Migration Order

**Ship in one atomic commit** (or two commits if the diff is truly huge —
but they land in the same PR, no intermediate "half-storm" state on main).
Reason: every ADR-0011 rule against bridges applies here — the V2 `OnAutoStormBegan`
+ `StormCursorTicker` + private-cursor path cannot coexist with V3
persistent-cursor on main even for a day, because the semantics are
mutually exclusive (V2 "start on stranding," V3 "always on"). One commit,
one gate.

**Recommended file order for the diff:**

1. **`FuelState.cs`** — no change. `FuelSpendResult` stays. Preserves
   test surface (`FuelState_Test`, `FuelState_PreviewSpend_Test`,
   `FuelStateDto_round_trip_test` all pass unchanged).

2. **`StormState.cs`** (NEW) + **`StormState_Test.cs`** (NEW). POCO with
   full ctor+mutator surface and EditMode coverage. Test doubles for
   Advance/Retreat clamp/RestoreFromSnapshot round-trip.

3. **`StormStateDto.cs`** (NEW) + **`StormStateDto_round_trip_test.cs`**
   (NEW). Mirror FuelStateDto structure. Add to `run.session_core`
   resume-atomic group; bump group schema version.

4. **`RunState.cs`** — add `public StormState Storm { get; }` + ctor
   parameter.

5. **`RunController.StartRun`** — accept `StormState` parameter, thread
   into `RunState` ctor.

6. **`BiomeDistributionSO.cs`** — add `_initialStormBeaconIndex` +
   `_havenStormRetreatBeacons` fields with `OnValidate` clamps. Update
   `BiomeDistributionSO_FuelCosts_Test` (may need rename to
   `BiomeDistributionSO_StormFields_Test` if it grows too broad).

7. **`RunSession.cs`** — constructor gains `_havenStormRetreatBeacons`;
   `Advance` inlines `Storm.Advance(strips)` + game-over check +
   `OnStormEngulfed` fire. `IsStrandedForFuel` unchanged (UI predicate
   only). Update `RunSession_Fuel_Test`, `RunSession_PreviewBeaconArrival_Test`.

8. **`RunSceneHost.cs`** — construct `StormState` in `BeginNewRun` /
   `BeginRunFromLoaded`. Pass `_biomeDistribution.HavenStormRetreatBeacons`
   into `RunSession` ctor. Delete `OnAutoStormBegan`. Delete
   `OnStormStopped`. `OnStormEngulfed` becomes model-fired (still
   presenter-consumed). Add `OnStormAdvanced(StormAdvanceTick)` fire
   inside `AdvanceToNextBeacon` when strips > 0. Delete the
   `IsStrandedForFuel()` post-Advance / post-Hydration `OnAutoStormBegan`
   trigger blocks.

9. **`StormEngulfmentController.cs`** — rewrite as visual-lerp pacer.
   Delete the private `StormCursorTicker` construction; delete the
   coroutine walk-along-forward-edges. Subscribe to `OnStormAdvanced`,
   trigger a per-tick `WaitForSeconds(SecondsPerBeacon)` window during
   which `StormMapVisualHost` lerps the apex. Consider renaming to
   `StormAdvanceVisualPacer`.

10. **`StormMapVisualHost.cs`** — subscribe to `OnStormAdvanced` (fires
    per storm-advance tick, not per-frame), call
    `_mapView.PlayStormAdvance(from, to, duration)`. Drop
    `OnAutoStormBegan` + `OnStormStopped` subscriptions. Add a
    `HandleRunStarted` that reads `_host.State.Storm.CursorIndex` and
    calls `_mapView.ShowStormFrontAt(cursor)` — replaces the
    `OnAutoStormBegan → ShowStormFrontAt(0)` path.

11. **`MapViewController.cs`** — add `StormFrontX(int cursorIndex)` helper
    (Amendment B). `ShowStormFrontAt` and `PlayStormAdvance` route
    through it. `HideStormFront` deletes (storm is always visible in V3).

12. **`BeaconTravelTick.cs`** — Amendment E1 rename.
    `PreviewedStormBefore` → `PreviewedStormCursorBefore`, etc.
    `MapViewController.BeaconTravelCoroutine` populates from
    `_host.State.Storm.CursorIndex` and `preview.Spend.StormAdvanceStrips`.
    Update `BeaconTravelTick_Test`.

13. **DELETE `StormCursorTicker.cs`** + **DELETE
    `StormCursorTicker_Test.cs`** + **DELETE `StormAdvanceTick.cs` OR
    KEEP:** decision — `StormAdvanceTick` struct is still useful as the
    `OnStormAdvanced` payload. Keep it, but move its xmldoc: it's no
    longer "the pure POCO ticker's tick," it's now "the model's
    advance-event payload." Rewrite the xmldoc accordingly. **Don't
    delete a still-useful readonly struct just because its original
    author is gone.**

14. **Delete polish-capture obsoletion.** Add a new capture file at
    `production/polish-captures/2026-07-24-storm-persistent-cursor.md`
    that includes this verdict, lists the deleted files
    (`StormCursorTicker.cs`, `StormCursorTicker_Test.cs`,
    `OnAutoStormBegan` event, `OnStormStopped` event), and pastes the
    Amendment list. The prior three storm captures from 2026-07-24 stay
    on disk as history but call out at the top: "superseded by
    persistent-cursor pivot, see 2026-07-24-storm-persistent-cursor.md."

**What breaks in the interim if you ship in stages:**

Do not stage this. Every stage boundary would either (a) leave the
persistent cursor half-wired (`StormState` exists, but game-over trigger
still fires from stranding predicate → storm cursor and doom trigger
disagree), or (b) leave both the V2 doom coroutine and the V3 model-side
cursor firing (double game-over race, dual-cursor visual). Neither is
acceptable ADR-0011.

If the diff is too large for one commit, split as:

- **Commit 1**: model side only (steps 1-8 + tests). Ship without
  changing `StormEngulfmentController`. Model-side game-over fires
  through `OnStormEngulfed`; V2 doom coroutine is deleted (steps 8-9
  overlap). This lands the persistent cursor + game-over trigger.
  Visual is temporarily broken (no lerp animation, only instant apex
  jump) but the mechanic works.
- **Commit 2**: view side (steps 9-12). Restores the visual lerp against
  `OnStormAdvanced` and completes Amendment E1.

Same PR, two commits. Do not merge Commit 1 without Commit 2.

---

## Success Criteria

We'll know this pivot was right if:

1. Playtesting a full Biome 1 run shows the storm visually advancing on
   the map after non-Haven commits, retreating a couple beacons on Haven
   arrivals, and reaching the player on run 4-5's worth of poor choices
   — matching the user's mental model surfaced 2026-07-24.
2. Save-mid-run and resume rehydrates storm cursor at its exact prior
   index (no restart-at-0 regression).
3. `IsStrandedForFuel` still red-tints unaffordable chips on the map
   without triggering game-over on its own — game-over comes from
   cursor collision.
4. No new ADR-0011 grep-hits: no `StormCursorTicker` references, no
   `OnAutoStormBegan` references, no `OnStormStopped` references, no
   dormant carrier fields.
5. EditMode tests green including `StormState_Test`, `StormStateDto_round_trip_test`,
   and updated `RunSession_Fuel_Test` variants asserting storm advances
   with fuel spend.

If (1) reveals the storm advances too fast or too slow at Biome 1's
`StormCounterStart = 30` + `beaconFuelCosts` values, retune the SO — not
the code (ADR-0015 pattern). If (4) fails, treat as a merge blocker.

---

**Final call: your decision.** You have full context on the V2 → V3
migration cost (13 files touched, one dead class deleted, one event
retired, three amendments) and the alternative (leave V2 as-is with
`StormAdvanceStrips` as dormant payload, ship V3 later). My strong
recommendation is ship V3 now: the V2 shape has already accumulated
three verdicts of layered scaffolding around a misimplemented spec, and
every additional slice against V2 will make the eventual pivot more
expensive. The mechanic the user wants is architecturally cleaner than
what's on disk today — that is rare, and worth catching.

---

## Amendment F — User Haven simplification (2026-07-24)

User rejected the retreat-on-Haven mechanic proposed in Q4 with the
directive: *"once haven is reached storm does not matter storm resets
(without the player seeing, this is done on endscreen. rename it sure."*

Interpretation: Haven = biome exit = run-complete boundary. There is no
mid-biome Haven retreat mechanic because there is no mid-biome Haven
today (Haven is terminal). Storm "reset" happens invisibly at run-end
because the NEXT run constructs a fresh `StormState` from
`BiomeDistributionSO.InitialStormBeaconIndex` via `RunSceneHost.BeginNewRun`.

**Deltas to shipped shape:**

- **`StormState`** ships WITHOUT `RetreatToHavenSpawn` mutator. Two-int
  POCO stays two-int (`CursorIndex` + `InitialCursorIndex`), plus
  `Advance` + `RestoreFromSnapshot` mutators only.
- **`BiomeDistributionSO`** ships WITHOUT `HavenStormRetreatBeacons`
  field. Only `_initialStormBeaconIndex` gets added.
- **`RunSession.Advance`** Haven branch does NOT mutate storm — just
  `Fuel.RefillPartial` + `Fuel.ResetStormOnHaven` (existing) then
  arrival branch exits. Non-Haven branch adds `Storm.Advance(strips)` +
  game-over check.
- **`RunSession` ctor** does NOT gain `_havenStormRetreatBeacons`
  parameter.

If a future biome ships mid-biome Haven beacons, the retreat mechanic
lands as a same-shape amendment: add `HavenStormRetreatBeacons` SO
field, add `RetreatToHavenSpawn` StormState mutator, wire into Haven
branch. Zero bridges, one clean amendment slice.

---

## Migration Files (basename manifest for gate-check)

Files touched by this atomic migration, listed by basename so hook
gates and reviewers can trace the diff surface at a glance. Order
follows the Migration Order section above.

**New files:**

- `StormState.cs` — POCO under `WastelandRun.Run`
- `StormState_Test.cs` — EditMode coverage
- `StormStateDto.cs` — persistence shape
- `StormStateSerializable.cs` — orchestrator adapter
- `StormStateDto_round_trip_test.cs` — save round-trip coverage
- `StormAdvanceVisualPacer.cs` — renamed from StormEngulfmentController

**Modified files:**

- `FuelState.cs` — no change (spec-locked)
- `RunState.cs` — add `public StormState Storm { get; }` + ctor param
- `RunController.cs` — `StartRun` accepts `StormState`
- `RunSession.cs` — `Advance` inlines `Storm.Advance` + game-over check
- `BiomeDistributionSO.cs` — add `_initialStormBeaconIndex` (Amendment F)
- `RunSceneHost.cs` — construct `StormState`, delete `OnAutoStormBegan`
  + `OnStormStopped`, add `OnStormAdvanced` fire, `Initialize` accepts
  `StormStateDto`
- `SaveBootstrap.cs` — register `StormStateSerializable`
- `StormMapVisualHost.cs` — subscribe to `OnStormAdvanced` + `OnRunStarted`
- `MapViewController.cs` — add `StormFrontX(int)` helper (Amendment B),
  delete `HideStormFront`
- `BeaconTravelTick.cs` — Amendment E1 rename
- `BeaconTravelTick_Test.cs` — update for E1 rename

**Deleted files:**

- `StormCursorTicker.cs` (Amendment C)
- `StormCursorTicker_Test.cs` (Amendment C)

**Kept but repurposed:**

- `StormAdvanceTick.cs` — xmldoc rewrite only (was "ticker's tick",
  now "model advance-event payload")

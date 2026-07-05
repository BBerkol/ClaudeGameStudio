# TD Verdict — V3 Fuel-as-Clock Architecture (Round 2)

**Date:** 2026-07-04
**Verdict:** APPROVE with conditions on Q3 and Q4 (see below)
**Consulted by:** Bertan, post design-panel lock 2026-07-02, D1/D2/D4 already resolved
**Prior verdict:** `production/td-verdicts/2026-07-02-v3-fuel-as-clock-architecture.md` (superseded — Q4 corrected below because that verdict incorrectly cited a `RunStateDto` class that does not exist in the codebase)

This is architecture only. No code was written. Files below were **read** to ground the verdict; nothing was edited.

---

## Q1 — FuelState home: APPROVE Option B (new `FuelState` POCO on `RunState.Fuel`)

**Recommendation.** Add a `FuelState` POCO to `WastelandRun.Run` and hang it off `RunState` as a single field: `public FuelState Fuel { get; }`. Do NOT flatten into two ints (`FuelCurrent`, `StormCounter`) on `RunState`.

**Why.**
- **Precedent is inside RunState already.** `RunState.cs` composes `Deck` (RunDeck POCO), `NodeMap` (NodeMap POCO), and `Scrap` (ScrapEconomy behind `IScrapEconomy`). A POCO-per-subsystem is the current convention — flat scalar fields on RunState are the exception (`RunSeed`, `Status`, `PendingCardOffer`), used only where there is no invariant to protect. Fuel has invariants (`Current ≤ Max`, `Current ≥ 0`, `StormCounter ≥ 0`, chassis multiplier floors to 1 and rounds up); those invariants have to live somewhere. If they don't live in `FuelState.Spend(baseCost, chassisMultiplier)`, they end up scattered across every call site — the "hardcoded gameplay values" and "unenforceable invariant" pair of ADR-0011 pattern #1 (adapter layer) and pattern #4 (polymorphism-via-code-branches, i.e. `if baseCost==0 skip decrement` littered wherever fuel is touched).
- **Ergonomics claim rebuttal (Bertan-as-designer).** "One flat POCO ergonomics" is a real preference, but `RunState.Fuel.Current` and `RunState.FuelCurrent` differ by 5 characters of read-site cost. The write-site cost differs by an order of magnitude: `state.Fuel.Spend(cost, mult)` vs. every consumer re-deriving `state.FuelCurrent = Max(0, state.FuelCurrent - Ceil(Max(1, cost * mult)))` inline. The one-flat-POCO ergonomic wins for scalar-value fields (e.g. `Scrap` on RunState was rejected as its own POCO for exactly that reason before ScrapEconomy landed) — but fuel is not a scalar, it's a **subsystem with invariants**.
- **ADR-0011 discipline.** Two flat ints would eventually need helper methods on `RunState` itself (`SpendFuel`, `ResetStormCounter`, `RefillPartial`) — RunState grows a fuel-shaped bulge. That is the "duplicate enums" / "parallel storage" smell approaching. Naming the subsystem is cheaper than fighting the drift.
- **ADR-0002 compatibility.** `FuelState` is engine-free (`WastelandRun.Run` assembly, `noEngineReferences: true`). No `UnityEngine.` references, no MonoBehaviours. Same rule ScrapEconomy already respects.

**Shape.**
```csharp
namespace WastelandRun.Run
{
    public sealed class FuelState
    {
        public int Current { get; private set; }
        public int Max { get; }              // See Q2 for derivation timing
        public int StormCounter { get; private set; }
        public int StormCounterStart { get; }

        internal FuelState(int max, int stormCounterStart, int startingFuel);

        public FuelSpendResult Spend(int baseCost, float chassisMultiplier);
        public void RefillPartial(float pct);   // Haven — uses Max
        public void ResetStormOnHaven();
        public void CreditFuel(int amount);     // Faucets: combat drops, event, merchant conversion
    }

    public readonly struct FuelSpendResult
    {
        public int FuelDrained { get; }         // Post-multiplier, post-floor, post-round-up
        public int StormAdvanceStrips { get; }  // 0 in the common case; 1 when StormCounter crossed 0
    }
}
```

**Note on `FuelSpendResult`.** Returning the drained amount + the strip-advance signal makes the UI's job trivial (the ticker knows exactly how much to animate and whether to march the storm line). It also lets tests assert per-call determinism against a known chassis+cost combo without touching internal state.

**Rejected alternatives.**
- Option A (flat `int FuelCurrent`, `int StormCounter` on RunState): rejected — invariants scatter.
- "Fold into Vehicle": rejected — same reason as prior verdict. Vehicle is the combat domain; fuel is the run-map domain. Combining them re-opens the Run→Combat directional arrow (ADR-0002 combat-assembly-free rule) and puts routing state inside a combat model.

**Risk if the alternative shape ships:** Every consumer of fuel (RunSession.Advance, RewardSource.Generate, save layer, MapView ticker) re-implements the multiplier floor/round-up rule. First balance patch (e.g. Truck 1.5 → 1.6) requires touching every consumer instead of one file. Silent divergence between test doubles and prod code.

---

## Q2 — Tank max derivation: APPROVE snapshot at `StartRun`, with a **recompute verb** for chassis swap

**Recommendation.** `FuelState.Max` is set at `StartRun` time from `chassis.TankCapacity × biome.StartingFuelModifier` and is stamped into the POCO as a `get`-only int. **Snapshot, not live-derive.** If chassis swap ever lands (Chopshop confirmed to NOT swap — per V3 LOCKED SHAPE Chopshop is repair-only), the game exposes a `FuelState.RecomputeMax(int newChassisCapacity, float biomeMod)` verb that also proportionally scales `Current` (or leaves it alone — that is a design call at swap-time, not now).

**Why.**
- **The alternative (live-derive on every access) forces `BiomeDistributionSO` to be reachable from `RunState`.** Today `RunState` is engine-free. `BiomeDistributionSO` lives in `WastelandRun.Run.Authoring` (engine-bound). Reaching from RunState → SO to compute Max on every access inverts the assembly arrow and breaks ADR-0002's engine-free-Run rule. You would have to either (a) inject the SO reference into RunState (leaks engine-bound state), or (b) inject a `Func<float> biomeModProvider` (delegate closure holding the SO). Both are bimodal-shape smells (ADR-0011 pattern #3).
- **Live-derive also breaks resume determinism.** The V3 counter values on the SO are content authoring surface — the SO can be edited between saves and loads (post-EA balance patch is exactly the case the prior verdict claimed as a *benefit* of live-derive). But `FuelState.Max` on a resumed run should equal the Max the player had before quitting. Post-EA balance patch changing Truck 75→80 mid-run silently expands the tank of a loaded save, which is a **live-service correctness bug**, not a feature. The correct place to update `Max` on balance patch is the same place that updates any other rehydrated invariant: on rollout, wipe live-run saves (EA policy already declared in ADR-0004 Decision 4 — schema mismatch = incompatible).
- **Chassis swap is a non-goal today.** V3 LOCKED SHAPE has no chassis swap mid-run; Chopshop is repair-only. `RecomputeMax` is not needed for the current slice; it's a verb we add if/when the mechanic lands. YAGNI on shipping it in Slice A. Ship the snapshot; deliberately leave the recompute as an addable verb.
- **Prior verdict was wrong on this point.** The 2026-07-02 verdict recommended live-derive citing "post-EA balance patch takes immediate effect." That's not the correctness we want — mid-run balance changes to persisted invariants are exactly what the ADR-0004 schema-version bump exists to protect against.

**Shape.**
```csharp
internal FuelState(int max, int stormCounterStart, int startingFuel)
{
    Max = max;
    StormCounterStart = stormCounterStart;
    Current = Math.Min(startingFuel, max);
    StormCounter = stormCounterStart;
}
```
`Max` is `get`-only after construction. Values arrive from the outside (RunSceneHost) which reads the chassis capacity + biome modifier and passes them in.

**Risk if live-derive ships:** Post-EA Truck-tank patch silently changes the fuel ceiling of every in-flight run. Storm-timing tuning becomes "either the SO or the rehydrated max, whichever wins." Save/load determinism (ADR-0003 rule 1 — reproducible outputs from persisted inputs) breaks quietly. The player's run behaves differently on Tuesday than it did on Monday.

---

## Q3 — Save shape (ADR-0004): APPROVE new `FuelStateDto` + `FuelStateSerializable` (standalone group-of-one)

**Recommendation.** New sibling DTO: `Assets/Scripts/Save/Dtos/FuelStateDto.cs` with `SYSTEM_ID = "run.fuel_state"` and `SCHEMA_VERSION = 1`. New adapter: `Assets/Scripts/Save/Adapters/FuelStateSerializable.cs` reading a `Func<RunState>` liveSource, projecting `RunState.Fuel` → DTO. **Do NOT join `run.session_core`.** Standalone group-of-one, sibling of `VehicleStateSerializable`.

**Correcting the prior verdict.** The 2026-07-02 verdict claimed "additive fields on `RunStateDto`" as the correct shape. **There is no `RunStateDto` class in the codebase** (verified — grep `class RunStateDto` returns zero hits; the save layer is 4 sibling DTOs: `RunSeedDto`, `NodeMapDto`, `RunDeckDto`, `VehicleStateDto`, each with its own `SYSTEM_ID` and `SchemaVersion`). Adding "fields to `RunStateDto`" is not a possible operation. This verdict corrects that.

**Why sibling DTO.**
- **Precedent is definitive.** ADR-0004 Decision 1 (distributed schema registry) and every existing DTO (`RunSeedDto`, `NodeMapDto`, `RunDeckDto`, `VehicleStateDto`) ship this shape: one DTO per subsystem, one adapter per DTO, `SYSTEM_ID = "run.<subsystem>"`, `SCHEMA_VERSION` const, `IRunStateSerializable` implementation. Deviating from this pattern re-introduces ADR-0011 #3 (bimodal path — "some subsystems ship their own DTO, some ride on a shared one") which the composite-payload amendment specifically retired.
- **Why standalone, not `run.session_core`.** `run.session_core` (RunSeed + NodeMap + RunDeck) is atomic because every per-step derivation and card-collection state couples through `RunSeed ^ stepIndex ^ salt`. Fuel state does not consume RunSeed for any derivation — it consumes chassis TankCapacity + biome multiplier + beacon base cost, all of which are content-side (SO-resident). A missing/skipped FuelState is recoverable to sane-defaults (`Current = Max`, `StormCounter = StormCounterStart`) without touching the seed graph. That is precisely the standalone-group criterion in the ADR-0004 Slice 9a Q1 amendment: "a member joins `run.session_core` if its absence-with-others-present creates a silently-broken determinism or progression invariant." Fuel absence does not — the run continues with a fresh tank on load-fail. Same asymmetric exhaustion policy as VehicleState.
- **Values NOT persisted (biome-SO-resident):** `Max` is a snapshot per Q2 → **is** persisted (it captures the tank capacity + biome modifier as they were at StartRun, so a post-patch SO edit doesn't retroactively resize the tank). `StormCounterStart` is also snapshotted at StartRun and persisted (same reason — the counter value was chosen for this run's biome under this run's authoring). `BeaconFuelCosts`, `HavenFuelRefillPercent`, `BiomeStartingFuelModifier` are all live-read from the SO at consume time (not persisted, per ADR-0003 rule 3 "scoped seeds and content parameters live on their source").

**Shape.**
```csharp
namespace WastelandRun.Save.Dtos
{
    public sealed class FuelStateDto : IRunStateSerializable
    {
        public const string SYSTEM_ID = "run.fuel_state";
        public const int SCHEMA_VERSION = 1;

        [JsonProperty("current")]              public int Current;
        [JsonProperty("max")]                  public int Max;
        [JsonProperty("storm_counter")]        public int StormCounter;
        [JsonProperty("storm_counter_start")]  public int StormCounterStart;

        [JsonIgnore] public string SystemId => SYSTEM_ID;
        [JsonProperty("schema_version")] public int SchemaVersion => SCHEMA_VERSION;
        [JsonIgnore] public System.Type DtoType => typeof(FuelStateDto);

        public static FuelStateDto From(FuelState fuel);
        public void ApplyTo(FuelState fuel);   // Called by adapter's rehydrate branch
        public object ToDto();
        public void FromDto(object dto);
    }
}
```

Adapter follows `VehicleStateSerializable` shape exactly — `LastLoaded` field, `_liveSource` Func, defense-in-depth type-check in `FromDto`.

**Rehydrate path.** Same as VehicleStateSerializable — `RunSceneHost.BeginRunFromLoaded` receives `loadedFuelState` alongside `loadedVehicleState`, and if non-null applies via `fuelState.ApplyTo(newRun.Fuel)`. If null, `FuelState` gets constructed fresh-shape from chassis + biome mod, tank full, storm counter at start value.

**Q3 risk if a monolithic DTO shape is chosen:** ADR-0011 #3 (bimodal path) violation — some subsystems have their own DTO, some don't. Every future subsystem author has to make the "own DTO or ride the monolith?" call. Every save/load path has to route by DTO type or by field name — the composite-payload amendment specifically retired this ambiguity by mandating one DTO per subsystem.

---

## Q4 — `BiomeDistributionSO` extension (ADR-0015): APPROVE the 4 fields as-shaped, but with **one authoring guardrail**

**Recommendation.** Ship the 4 approved fields on `BiomeDistributionSO`:
- `int[] BeaconFuelCosts` — an 8-element array keyed by BeaconType (full enum, biome 1 fills all 8: Start=0 [inert but present so the array is enum-parallel], Haven=0, Rest=3, Merchant=4, Chopshop=4, Event=5, Combat=8, EliteCombat=12). Storing values for beacons a biome doesn't emit is content-shaping, not dead code — the ADR-0015 pattern is exactly "the enum is the code contract, the SO chooses what actually spawns."
- `int StormCounterStart` — single scalar, default 30. Biome-scoped.
- `float HavenFuelRefillPercent` — single scalar, default 0.65. Biome-scoped.
- `float BiomeStartingFuelModifier` — single scalar, default 1.0. Biome-scoped. **Prior verdict deferred this to biome 2; I disagree — see below.**

**Why land `BiomeStartingFuelModifier` in Slice B, not defer.** The prior verdict argued YAGNI because biome 1 starts full-tank. But: (a) the field's presence at 1.0 is a no-op cost of ~4 bytes of SO storage and 5 lines of authoring surface; (b) the alternative — landing it later in a biome-2 slice — requires editing both the SO shape *and* every existing Biome1Distribution.asset in a data migration, which is a bigger commit than shipping it inert now; (c) Q2 above stamps `Max` at StartRun **based on this modifier**, so the seam has to exist at construction time regardless. Deferring means we ship a snapshot rule that reads only from chassis, then later change the snapshot rule to also read from a biome modifier — a bimodal shape (ADR-0011 #3). Ship the seam once, land the value at 1.0 for biome 1.

**Authoring guardrail (the "one" condition).** `BeaconFuelCosts` should be modeled as an 8-element `int[]` sized to `BeaconType`'s enum length with `OnValidate` clamping to `Enum.GetValues(typeof(BeaconType)).Length`. The alternative shape — a `List<WeightedBeaconType>`-style keyed struct — is what level-designer's `beaconFuelCost[]` sketch reads like, and it invites two problems: (1) an entry can be missing entirely, in which case what's the default? undefined-cost-per-beacon is a genuine bug surface; (2) two entries can key the same BeaconType, and the second silently wins. Enum-sized `int[]` closes both. Use `OnValidate` to warn if a Combat entry is 0 (typo detection) or if the array length mismatches `Enum.GetValues(typeof(BeaconType)).Length`.

**Is this ADR-0015 clean?** Yes — this is the pattern's canonical shape. Full BeaconType enum in code (unchanged). Per-biome data table (the SO) narrows to actual gameplay values. No "some biomes have costs, others don't" bimodal path — every biome that ships an SO ships a full cost table, and defaults on the SO fill in sensible values (0 for beacon types the biome doesn't emit).

**Duplication concern (Bertan's flag: "would a static const class + per-biome override be cleaner?").** No, and here's why: the moment biome 2 changes any single cost (say Combat=8 → Combat=10 for the harder biome), the "constants + override table" shape becomes the bimodal path — "look at the base const OR the override, depending on the biome." The pure per-biome table is one lookup path. The redundancy (biome 1 and biome 2 both stating Combat=8) is cheap — 32 bytes per biome — and worth the clarity. This is exactly ADR-0015's stance ("full enum stays real, table controls what generator emits") ported to costs instead of spawn-weight.

**No new ADR.** ADR-0015 amendment paragraph documents the 4 new fields as an application of the pattern. Same commit as the SO edit.

**Q4 risk if the wrong shape ships:**
- If we ship a `List<WeightedFuelCost>`-style keyed struct: silent duplicates, missing-entry semantics, OnValidate churn. Level-designer stops noticing typos, some encounter costs zero fuel and the storm never ticks.
- If we defer `BiomeStartingFuelModifier`: bimodal snapshot rule (Max = chassis only → Max = chassis × modifier) in two adjacent slices. Pattern-adjacent ADR-0011 violation.

---

## Q5 — `CombatReward.Fuel` extension (ADR-0013): APPROVE as clean additive, with **draw-order lock enforced by test**

**Recommendation.** Extend `CombatReward` additively:

```csharp
public sealed record CombatReward(int Scrap, CardOffer Choices, int Fuel = 0)
{
    public int Scrap { get; } = /* non-negative validation */;
    public CardOffer Choices { get; } = Choices;
    public int Fuel { get; } = Fuel >= 0
        ? Fuel
        : throw new System.ArgumentOutOfRangeException(nameof(Fuel), Fuel,
            "CombatReward.Fuel must be non-negative.");

    public CombatReward(int scrap) : this(scrap, null, 0) { }
}
```

**Why default `= 0`, not nullable `int?`.** Same reasoning as `Choices` nullable-vs-sentinel-empty is the *reverse*: for `Choices`, null = "no offer was rolled, don't show the picker," which is a semantically distinct state from "empty offer." For `Fuel`, `0` is a legitimate roll outcome (some beacons never drop fuel — Haven=0, Chopshop=0). Nullable adds no info at consume time (`?? 0` is what every consumer would write) and forces every save/load and log line to handle the null case. Default 0 is right.

**Extend `IRewardSource.Generate` return shape or leave `IRewardSource` scrap-only?** Extend it. `IRewardSource.Generate` returns `CombatReward` today — the record already carries `Choices` alongside `Scrap`; adding `Fuel` on the same record is not a new pattern, it's the pattern it already uses. Do NOT create a sibling `IFuelRewardSource`. Fuel and scrap share the same axis (per-beacon, ADR-0003-seeded, biome-content-tunable); scrap and card-offers do NOT share an axis (card offers are per-boss-vs-per-combat different, deck-state coupled). ADR-0013 §Q3's rationale for splitting `ICardRewardSource` was that card pool state is disjoint from scrap. Fuel is not disjoint from scrap. One source seam covers both.

**Draw-order lock (D4 was accepted — this is how to enforce it).** The V3 spec says scrap decided **before** fuel inside `Generate`. That is not a code-level "wire order" thing; it's a **deterministic-seed-draw order** thing. Inside `FlatScrapRewardSource.Generate` (and every future table-roll implementation), the same `System.Random` instance derived from `rewardSeed` MUST be consumed for scrap draws before fuel draws. If two implementations swap the order, saved fuel drops become non-deterministic across builds (ADR-0003 violation).

**Enforce with a test, not just a comment.** Ship a targeted unit test in the same slice: `RewardSource_ScrapBeforeFuel_test.cs` — instantiate the reward source with a fixed known seed, run `Generate` twice against the same seeded beacon, assert (scrap, fuel) equals a hand-picked expected tuple. If someone reorders draws, the test flips.

**RewardSeedMix stays `0x5257`.** Same seed derivation salt; nothing new.

**Q5 risk if the draw-order rule ships as comment-only:** First table-roll implementation (Slice C or later) that iterates scrap after fuel silently invalidates every save's reproducibility from that beacon forward. Debugging burns a session. Cheap to prevent with one test.

---

## Q6 — Presentation seam (D1 3-second animated ticker): APPROVE Event-based defer via `MapViewController.OnBeaconAnimationComplete` → `RunSceneOverlayHost.HandleBeaconClicked` gate

**Recommendation.** The 3-second animated ticker lives on `MapViewController` (per D1). But — critically — **the current async gate for combat entry already exists**: `BeaconActivator.OnBeaconActivated` fires *after* the async scene load, and `RunSceneHost.HandleBeaconActivated` gates the `BeginCombatForCurrentBeacon` call on that event. This is the pattern to extend, not replace.

Two options presented, one recommendation.

**Option 6.a — Insert the 3s wait BEFORE `AdvanceToNextBeacon` fires (my recommendation).** Sequence:

1. Player clicks beacon → `MapViewController.OnBeaconClicked` fires with `toIndex`.
2. `RunSceneOverlayHost.HandleBeaconClicked` calls `MapViewController.PlayBeaconTravelAnimation(fromIndex, toIndex, spendPreview, stormPreview, onComplete)` — a new async verb on the controller. `spendPreview` is the pre-computed fuel drain (from FuelState previewing the spend against chassis + base cost) and `stormPreview` is the strip-advance count (0 or 1). The overlay host does NOT call `_host.AdvanceToNextBeacon` yet.
3. `MapViewController` runs a Unity coroutine (`StartCoroutine`) over 3 seconds, ticking fuel widget + destination pill + storm counter widget 1-by-1 in sync. When done, invokes `onComplete()`.
4. `onComplete()` calls `_host.AdvanceToNextBeacon(toIndex, HostAdvanceReason.PlayerChoice)`, which flushes model-side fuel spend + storm decrement + node advance in a single controller call.
5. `RunSceneHost.HandleBeaconActivated` (existing) fires after the async scene load and, for Combat beacons, calls `BeginCombatForCurrentBeacon`.

Total end-to-end for Combat: 3s ticker → model advance → scene load → EnterCombat. UI ticks feel synchronized with model changes.

**Why coroutine on the controller, not UniTask.** Grep confirms zero UniTask usage in the codebase and no `Cysharp.Threading.Tasks` in Packages/manifest.json. `SceneManager.LoadSceneAsync` inside `BeaconActivator` uses a `.completed` callback (event-based, not awaited). The existing seam is C# events + Unity coroutines. Adopting UniTask for one seam introduces a new package and a new async pattern in isolation — new-package-for-one-seam is `feedback_data_flag_lagging_dependency.md` inverted (build the dependency, then the seam) but without the payoff. Coroutine is the ADR-consistent choice.

**Why callback rather than a coroutine that the caller awaits.** `RunSceneOverlayHost` is a MonoBehaviour but it is not the one that owns the animation — the controller does. Two options for "who calls what": (a) controller returns an `IEnumerator` that the host `StartCoroutine`s (couples host to Unity coroutine plumbing), or (b) controller `StartCoroutine`s on itself and invokes an `Action onComplete` when done (loose seam, host is agnostic to how the animation is implemented). Option (b) — the callback — is cleaner and matches the existing shape (`OnBeaconClicked`, `OnCombatReady`, `OnBeaconActivated` are all `Action`-based).

**Why NOT a new C# event on `MapViewController`.** An event like `OnBeaconAnimationComplete` would broadcast a single-consumer signal to potentially multiple listeners — but only `RunSceneOverlayHost` needs it, and the animation is per-click (single fire-and-forget). Passing the callback as a parameter to `PlayBeaconTravelAnimation` (single-shot) is more honest about the shape than an event that persists across clicks.

**State-tick-instant, UI-tick-over-3s (D1 rule) is preserved.** Because model advance happens in step 4 AFTER the ticker completes, the model DOES tick instant — just delayed 3s from the click. If Bertan reads D1 as "model ticks INSTANT ON CLICK, UI ticks OVER 3s" (i.e. model ticks first, UI catches up), that's a different pattern — see Option 6.b — but it introduces a save/resume divergence risk (see risk callout below).

**Option 6.b — Advance the model instantly on click, run ticker over the same 3s.** Sequence: click → `_host.AdvanceToNextBeacon` fires immediately, spending fuel + decrementing storm counter + advancing node → then `MapViewController.PlayBeaconTravelAnimation` runs the UI ticker over 3s → `HandleBeaconActivated` from scene load gates the actual EnterCombat behind the ticker completion (need an additional gate variable in `RunSceneHost` because `HandleBeaconActivated` currently calls `BeginCombatForCurrentBeacon` synchronously).

**Rejected.** Two reasons.
1. **Save/resume divergence.** If the player quits mid-ticker (Bertan will hit this in playtesting), the model has already advanced but the ticker never finished — resume brings them into a fully-committed post-tick state with no UI signal that the tick happened. Under 6.a, quitting mid-ticker rolls back to the pre-click state (nothing was committed model-side), which is the resume behavior a player would expect for "I closed the game before the storm actually rolled forward."
2. **Two async gates for combat.** Combat entry has to wait on BOTH ticker completion AND scene load. Two independent async events converging on one action is a race-condition hotspot — either can arrive first, and the code has to track "did the other one land yet?" state per beacon. 6.a keeps combat entry gated on a single event (BeaconActivator.OnBeaconActivated, unchanged) because the ticker completes *before* the scene load kicks off.

**Q6 risk if 6.b or a UniTask pattern ships:** Save/resume divergence on mid-ticker quit; new package adoption for a single call-site; race conditions between ticker completion and async scene load. Bertan will surface a bug within 2 playtests.

---

## Recommended slice sequence

Slice-level task decomposition, ordered for minimum blast radius per slice + reversibility if any slice is rejected mid-flight.

**Slice A — FuelState POCO + tests (no wiring).**
Adds `FuelState` + `FuelSpendResult` + unit tests. Not wired into `RunState` yet. Pure engine-free POCO in `WastelandRun.Run`. Tests: chassis multiplier floor at 1, round-up, `Spend` returns strip-advance = 1 when counter crosses 0, `Spend` returns 0 when counter merely decrements, `RefillPartial` clamps to Max, `ResetStormOnHaven` re-sets counter to `StormCounterStart`, `CreditFuel` clamps to Max. Ship as isolated types.

**Slice B — `BiomeDistributionSO` extension + Biome1Distribution.asset re-serialize.**
4 new SerializeField fields with OnValidate guardrails. Bake Biome1Distribution.asset values against V3 LOCKED SHAPE (BeaconFuelCosts array, StormCounterStart=30, HavenFuelRefillPercent=0.65, BiomeStartingFuelModifier=1.0). ADR-0015 amendment paragraph in same commit. No consumer wiring yet; SO is data-only surface. Test: OnValidate catches missing/short array.

**Slice C — RunState wiring + `FuelStateDto` + `FuelStateSerializable` + reward extension.**
This is the "the seam lights up" slice. Concurrent adds:
1. `RunState.Fuel` field + `RunController.StartRun` param-thread for `FuelState` (constructed by caller from chassis + biome SO).
2. `FuelStateDto` + `FuelStateSerializable` under `WastelandRun.Save.Dtos` / `Adapters` + `SaveBootstrap` registration + `RunSceneHost.BeginRunFromLoaded` rehydrate branch (mirrors VehicleStateSerializable pattern).
3. `CombatReward.Fuel = 0` additive extension.
4. `FlatScrapRewardSource.Generate` sets `Fuel = 0` (unchanged M1 behavior); reserves the seam.
5. `RunSession.Advance` spends fuel via `state.Fuel.Spend(baseCost, chassisMultiplier)` at commit time (before `EnterCombat` fires) — this is the model-side tick.
6. `RunSession.ExitCombat` credits `reward.Fuel` to `state.Fuel.CreditFuel(reward.Fuel)`.
7. `RewardSource_ScrapBeforeFuel_test.cs` — draw-order determinism enforcement.

Tests: `RunSession_Advance_DecrementsFuel_test`, `RunSession_Advance_ResetsStormOnHaven_test`, `RunSession_Fuel_PersistsAcrossSaveLoad_test`, plus the ADR-0004 round-trip test extended to include FuelStateDto.

**Slice D — MapViewController animation seam + PlayBeaconTravelAnimation callback path.**
Only after C is landed and green.
1. Add `PlayBeaconTravelAnimation(fromIndex, toIndex, spendPreview, stormPreview, onComplete)` verb on `MapViewController` — Unity coroutine over 3s.
2. `RunSceneOverlayHost.HandleBeaconClicked` refactor to: pre-compute spend preview (`FuelState.PreviewSpend(baseCost, mult)` — read-only variant that returns the same `FuelSpendResult` without mutating; add to the POCO), call `MapViewController.PlayBeaconTravelAnimation` with a callback that invokes `_host.AdvanceToNextBeacon`.
3. UI widgets subscribe to a per-frame tick during the animation (existing UI Toolkit subscription lifecycle — feedback memory).

Manual walkthrough evidence in `production/qa/evidence/` — this is a visual/feel story per the coding standards table. No automated test for the 3s ticker (fits the "not-automated" list). Test at the model level that `PreviewSpend` returns the same result as `Spend` would.

**Slice E (optional / follow-up) — Faucet wiring.**
Combat drops 2-4 fuel (Elite 3-5), Event ~25% chance 2-4, Merchant 4 scrap → 1 fuel conversion, Haven partial refill 65%. Ships incrementally, one beacon type per commit. Each ships with a unit test asserting deterministic outcome from a fixed seed.

**Why this order.** Slice A is pure model with zero consumers — reversible in one revert if the shape is wrong. Slice B is data-only. Slice C wires them together at the RunState + Save layers with the model semantically correct even if the UI still doesn't tick visibly. Slice D is the polish pass. Slice E fills in the faucet economy after the core clock is proven.

**Estimate.** A: 1 focused session. B: 0.5 session. C: 2 sessions (the DTO + adapter + save recovery path + reward extension + draw-order test is real work). D: 1 session. E: 2-3 sessions across all faucets.

---

## ADR delta summary

- **ADR-0002:** add one line to "systems composed under RunState" list (`FuelState`).
- **ADR-0004:** no ADR change; `FuelStateDto` follows the established sibling-DTO pattern. `SchemaRegistry_Unique_test` will pick up the new `SYSTEM_ID` automatically.
- **ADR-0013:** amendment note documenting additive `CombatReward.Fuel = 0` + draw-order lock (scrap draws before fuel draws inside `IRewardSource.Generate` implementations, enforced by test).
- **ADR-0015:** amendment paragraph documenting 4 new `BiomeDistributionSO` fields + enum-parallel int[] shape as the canonical cost-table application of the narrowing pattern.

## Files read to ground this verdict

- `Assets/Scripts/Run/RunState.cs`
- `Assets/Scripts/Run/RunSession.cs`
- `Assets/Scripts/Run/RunController.cs`
- `Assets/Scripts/Run/CombatReward.cs`
- `Assets/Scripts/Run/IRewardSource.cs`
- `Assets/Scripts/Run/FlatScrapRewardSource.cs`
- `Assets/Scripts/Run/Authoring/BiomeDistributionSO.cs`
- `Assets/Scripts/Save/IRunStateSerializable.cs`
- `Assets/Scripts/Save/Dtos/RunSeedDto.cs`
- `Assets/Scripts/Save/Dtos/NodeMapDto.cs`
- `Assets/Scripts/Save/Dtos/VehicleStateDto.cs`
- `Assets/Scripts/Save/Adapters/VehicleStateSerializable.cs`
- `Assets/Scripts/CombatView/RunSceneHost.cs`
- `Assets/Scripts/CombatView/RunSceneOverlayHost.cs`
- `Assets/Scripts/CombatView/BeaconActivator.cs` (grep-verified for OnBeaconActivated event + async LoadSceneAsync pattern)
- `Assets/Scripts/UI/MapViewController.cs`

## Success criteria (we'll know this was right if)

- Slice A's `FuelState` tests are all green with zero UnityEngine references in the test files (POCO discipline preserved).
- Slice C's save round-trip test writes a run with `Fuel.Current=42, StormCounter=17`, corrupts everything else, and the fuel state comes back intact from the standalone group (asymmetric-exhaustion policy proven).
- Slice C's `RewardSource_ScrapBeforeFuel_test` fails if any developer swaps draw order inside a `Generate` implementation.
- Slice D's playtest shows destination pill + fuel widget + storm counter all reaching final values at t=3s with no visible desync, and Alt+F4 at t=1.5s reloads the pre-click state (model wasn't committed).
- Zero `TODO.*Fuel` and zero `// V3` comments remain in `WastelandRun.Run` at end of Slice E.

---

*Filed by technical-director, 2026-07-04. Verdict corrects prior 2026-07-02 verdict on Q4 (RunStateDto class does not exist; sibling-DTO pattern is the correct shape). Respects ADR-0002, ADR-0003, ADR-0004, ADR-0011, ADR-0013, ADR-0015. Consistent with feedback memories: `flag_major_decisions`, `demo-forward-over-infrastructure` (retracted 2026-06-01 — build canonical 1.0), `cross_check_adr_contract`, `count_real_consumers`, `data_flag_lagging_dependency`.*

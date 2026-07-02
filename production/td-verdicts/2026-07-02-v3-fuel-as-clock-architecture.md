# TD Verdict — V3 Fuel-as-Clock Architecture

**Date:** 2026-07-02
**Verdict:** CONCERNS (3 items — none blocking, all cheap to resolve now / expensive later)
**Consulted by:** design lock post panel-3 (game-designer + systems-designer + economy-designer + level-designer)
**Locked design shape:** `production/session-state/active.md` — V3 LOCKED SHAPE block

## Q1 — Fuel state location: APPROVE Option A (single `FuelState` POCO on `RunState`)

```csharp
public sealed class FuelState  // engine-free POCO per ADR-0002
{
    public int Current { get; private set; }
    public int Max { get; }            // chassis-derived at StartRun, immutable
    public int StormCounter { get; private set; }
    public int StormCounterStart { get; }

    public FuelSpend Spend(int baseCost, float chassisMultiplier);
    public void RefillPartial(float pct);
    public void ResetStormOnHaven();
}
```

`RunState.Fuel` (single field). `Vehicle` stays combat-domain — tank capacity is
derived from `Vehicle.Chassis` at `StartRun` and **snapshotted** into
`FuelState.Max`. Combat should not see fuel (CD Condition 2).

**Rejected alternatives:**
- Split `FuelState` + `StormClock`: two-writer invariant with no independent
  axis of evolution (ADR-0011 pattern #4 territory if the split has no reason).
- Fold into `Vehicle`: leaks routing state into combat assembly (CD Cond 2 fail).

**Watch-item:** if biome 2 introduces "storm ticks independently of fuel spend,"
revisit as a split.

**Migration cost:** ~150 LOC + tests.

## Q2 — `CombatReward.Fuel`: APPROVE as ADR-0013 clean additive

```csharp
public sealed record CombatReward(int Scrap, CardOffer Choices, int Fuel = 0);
```

Default `= 0`, NOT nullable. Zero is a legitimate value; nullable adds no info
and forces `?? 0` at every consumer.

**Roll site:** extend `IRewardSource.Generate` to return `(int Scrap, int Fuel)`.
Do NOT create `IFuelRewardSource`. Fuel policy shares the same axis as scrap
(biome/beacon-typed, archetype-independent). ADR-0013 §Q3 rejected folding
because card pool state is disjoint from scrap — fuel is not disjoint.

**Determinism (ADR-0003):** reuse `RewardSeedMix = 0x5257`. Single `System.Random`
instance per beacon commit; draw order LOCKED (scrap first, then fuel). Contract
documented in code + CI-checked with known-seed unit test.

**Migration cost:** ~200 LOC + tests. ADR-0013 gets amendment note.

## Q3 — BiomeDistributionSO extension: PARTIAL APPROVE

| Field | Verdict | Reason |
|---|---|---|
| `int[] BeaconFuelCosts` by BeaconType | APPROVE | Pure content-shaping; full 7-value enum, biome-1 fills all 7. |
| `int StormCounterStart` | APPROVE | Single scalar, biome-scoped, tunable. |
| `float HavenFuelRefillPercent` | APPROVE | Single scalar, biome-scoped, tunable. |
| `float BiomeStartingFuelModifier` | DEFER | YAGNI at biome 1 (always starts full tank). Land with biome 2 slice. |
| `float[] depthCostModifier` per strip | REJECT | (a) "strip" grammar retired by ADR-0015 Block 2 amendment; (b) shape re-introduces a concept the generator abandoned; (c) V3 LOCKED SHAPE has no home for it (not in `base × chassis` nor in storm-decrement formula). If depth-scaling ever needed, put it in beacon cost table. |

**No separate SO.** All 3 approved fields are same-cadence (frozen at biome start,
biome-authored). Split = ADR-0011 pattern #4.

**ADR pressure:** ADR-0015 amendment (not new ADR). One paragraph in same commit.

**Migration cost:** ~30 LOC + asset re-serialize.

## Q4 — Save shape: APPROVE Option (a) additive fields on `RunStateDto`

```csharp
public sealed class RunStateDto : IRunStateSerializable
{
    public const string SYSTEM_ID = "run.state";
    public const int SCHEMA_VERSION = <N+1>;

    // ... existing fields ...
    public int FuelCurrent;
    public int FuelMax;          // OR derive-live on FromDto — see Risk 2
    public int StormCounter;
}
```

**Rejected alternatives:**
- Sub-DTO `RunFuelDto`: 3 ints with no independent evolution axis = ceremony.
- New top-level DTO with own `SystemId`: creates partial-state invariant
  (save with intact vehicle but corrupt fuel = no meaningful recovery UX).

**Schema version bump:** standard `SCHEMA_VERSION++`. EA-mode saves predating V3
fuel fail check → partial-skip → "start new run" non-blocking prompt. Correct
EA behavior; no migration runtime pre-1.0.

**Not persisted (derived on resume):** `StormCounterStart`,
`HavenFuelRefillPercent`, beacon cost table — all live on biome SO, re-read on
resume. Aligns with ADR-0003 Rule 3 ("scoped seeds not persisted").

**Migration cost:** ~100 LOC + tests.

## Q5 — CD Condition 3 ("Storm does NOT advance during combat"): STRICT-COMPATIBLE with wording risk

**Architecture-side reading:** In state graph, `CommitBeacon(n+1)` mutates
`FuelState.Current` and `FuelState.StormCounter` BEFORE `EnterCombat` fires.
Nothing inside `CombatLoop` reads/mutates storm. Condition 3 is strict-satisfied.

**Wording risk:** Player commits to Combat beacon → storm strip advances visibly
→ combat starts. Perception may not match state-machine correctness ("storm
ticked because of the combat"). Sub-question for CD:

- Storm-tick animation plays at commit (before EnterCombat) or at combat-exit
  (after ExitCombat)? Both compatible with state-machine reading; different
  player read.

**Recommended action:** Do NOT re-adjudicate the mechanic with CD. DO ask CD for
a one-line ruling on animation timing. Small architectural echo (MapViewController
subscription order) — pin before code.

## Top 3 Risks

1. **Fuel roll seed ordering (Q2).** Draw order for scrap-then-fuel MUST be
   locked in code + CI-checked. Concrete: single method
   `RollCombatDrops(int rewardSeed) → (Scrap, Fuel)` with inline contract
   comment + targeted unit test asserting known seed → known (scrap, fuel).

2. **`FuelState.Max` snapshot vs. live-derive on resume (Q1/Q4).** If persisted,
   post-EA balance patch (Scout 20→22) doesn't affect existing saves. If
   live-derived from `Chassis` on FromDto, patch takes immediate effect.
   **Recommend live-derive** — same principle as `StormCounterStart`.

3. **`depthCostModifier` rejection is soft.** If level-designer pushes back,
   correct answer is per-position beacon cost table (not modifier). V3 LOCKED
   SHAPE doesn't include per-position variance — game-designer call if it
   recurs.

## Decision Points Requiring User or CD Input

| # | Question | Route | Blocking |
|---|---|---|---|
| D1 | Storm-tick animation timing: at commit or at combat-exit? | creative-director | Blocks MapViewController wiring; ~2h either way |
| D2 | Confirm `depthCostModifier` rejection sticks; escalate if level-designer pushes back | user + game-designer | Non-blocking for slice 1 |
| D3 | `FuelState.Max` snapshot vs. live-derive on EA balance patches | user | Blocks DTO shape; recommend live-derive |
| D4 | Draw-order lock (scrap then fuel) in `IRewardSource.Generate` — accept as ADR-0013 amendment | user | Blocks reward-source refactor; recommend accept |

## Files Referenced

- `production/session-state/active.md` (V3 LOCKED SHAPE)
- `design/notes/verdict-sandstorm-chase-pacing.md` (CD 5 conditions)
- `docs/architecture/adr-0002-card-combat-state-event.md`
- `docs/architecture/adr-0003-deterministic-rng-discipline.md`
- `docs/architecture/adr-0004-save-persistence-architecture.md`
- `docs/architecture/adr-0011-no-bridges-architectural-rule.md`
- `docs/architecture/adr-0013-run-card-collection-and-reward-composition.md`
- `docs/architecture/adr-0015-biome-distribution-as-configuration-narrowing.md`

## ADR Delta Summary (post-implementation)

- **ADR-0002:** add one line to "systems composed under RunState" list (`FuelState`).
- **ADR-0013:** amendment note documenting additive `CombatReward.Fuel` +
  `IRewardSource.Generate` return-shape bump to `(Scrap, Fuel)`.
- **ADR-0015:** amendment paragraph documenting fuel/storm tuning on
  `BiomeDistributionSO` (2026-07-02, V3 fuel-as-clock).
- **ADR-0004:** `RunStateDto SCHEMA_VERSION` bump (standard, no ADR change).

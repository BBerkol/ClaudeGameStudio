# Capture — Cluster-Lane → Uniform Poisson-disc Retreat (2026-07-07, PM)

## Context

Same-day retreat from the cluster-lane generator pivot that landed this
morning. Playtest showed the cluster-lane algorithm produces dense
blob-clusters (visible as a wall of icons in center-right of map) rather
than the airy hex-mesh the designer sketched. TD verdict: retreat to
uniform Poisson-disc + Delaunay, retire cluster-lane vocabulary.

Reference: this afternoon's TD verdict (delivered inline; postscript appended
to `production/td-verdicts/2026-07-07-cluster-lane-generator.md`).

## What is being destroyed

### `BiomeGenerationInputs.cs` — record properties retired

- `LaneCountRange : (int Min, int Max)` — cluster vocabulary
- `ClustersPerLaneRange : (int Min, int Max)` — cluster vocabulary
- `BeaconsPerClusterRange : (int Min, int Max)` — cluster vocabulary
- `ClusterRadiusX : float` — anisotropic ellipse geometry
- `ClusterRadiusY : float` — anisotropic ellipse geometry
- `LaneYSpread : float` — lane centerline placement
- `LaneWobbleAmplitude : float` — sine-wobble per-lane

### `BiomeWebGenerator.cs` — code deleted

- `TryPlaceLaneClusters(rng, inputs, out positions)` — full method
- Private constants tied to lane placement:
  - `ClusterXMin = 0.20f`
  - `ClusterXMax = 0.80f`
  - `ClusterXJitter = 0.04f`
  - `LaneWobbleFrequency = 2f * PI`
  - `LanePhaseGoldenRatio = PI * 0.618`
- `MaxEmergentBeaconCount = 64` cap → lowered to `40`
- `PoissonCandidatesPerInsertion = 60` → lowered to `30` (uniform is looser than cluster scatter)

### `BiomeDistributionSO.cs` — SerializeField fields retired

- `_laneCountRange = new Vector2Int(2, 3)` — cluster vocabulary
- `_clustersPerLaneRange = new Vector2Int(1, 3)` — cluster vocabulary
- `_beaconsPerClusterRange = new Vector2Int(3, 6)` — cluster vocabulary
- `_clusterRadiusX = 45f` — anisotropic ellipse geometry
- `_clusterRadiusY = 65f` — anisotropic ellipse geometry
- `_laneYSpread = 0.2f` — lane centerline placement
- `_laneWobbleAmplitude = 40f` — sine-wobble per-lane

### `BiomeGenerationInputsFactory.cs` — code deleted

- `laneCountRange` / `clustersPerLaneRange` / `beaconsPerClusterRange` tuple unpacks
- Direct field passes for `ClusterRadiusX/Y`, `LaneYSpread`, `LaneWobbleAmplitude`

### `BiomeWebGenerator_Test.cs` — 22 tests migrating

- `DefaultLaneCountRange`, `DefaultClustersPerLaneRange`, `DefaultBeaconsPerClusterRange`
- `DefaultClusterRadiusX`, `DefaultClusterRadiusY`, `DefaultLaneYSpread`, `DefaultLaneWobbleAmplitude`
- `BuildInputs` signature — `laneCountRange` / `clustersPerLaneRange` / `beaconsPerClusterRange` params
- `EmergentBeaconBounds` — cluster-product formula
- Test 6 `Generate_BeaconCountWithinEmergentBounds`
- Test 22 `ClusterLaneRanges_RoundTripThroughInputs`

### `Biome1Distribution.asset` YAML — fields removed

Current authored values being destroyed (from today's tuning iteration Fix 3
+ my LaneYSpread=0.3 patch this session):

```yaml
_laneCountRange: {x: 3, y: 3}
_clustersPerLaneRange: {x: 4, y: 4}
_beaconsPerClusterRange: {x: 4, y: 4}
_clusterRadiusX: 95
_clusterRadiusY: 140
_laneYSpread: 0.3
_laneWobbleAmplitude: 40
_reconnectRadius: 500
_maxEdgeLength: 500
_globalMinSeparation: 90
```

## What is being ADDED

### `BiomeGenerationInputs.cs`

- `TargetBeaconCount : int` — single fixed count, replaces the 3-tuple combinatorial roll

### `BiomeWebGenerator.cs`

- `TryPlaceUniformPoisson(rng, targetBeaconCount, globalMinSeparation, out positions)` — uniform Poisson-disc across `[XMin, XMax] × [YMin, YMax]`
- `MaxEmergentBeaconCount = 40` (down from 64; leaves headroom for landmark overlay at Biome 2)
- `PoissonCandidatesPerInsertion = 30` (down from 60)

### `BiomeDistributionSO.cs`

- `_targetBeaconCount : int = 30` — TD-recommended default matching the designer's sketch (~30 nodes)
- `OnValidate`: cap warning drops from 64 to `_targetBeaconCount > 40`; add `_targetBeaconCount >= 4` clamp

### `BiomeGenerationInputsFactory.cs`

- `TargetBeaconCount` unpack

### `BiomeWebGenerator_Test.cs`

- Shared `DefaultTargetBeaconCount = 30`
- `BuildInputs` signature: `targetBeaconCount = 30`
- New helper: emergent count check = beacon count in `[Start+Terminal=2, targetBeaconCount]`
- Test 6 rewritten: `Generate_BeaconCountEqualsTargetOnSuccess` (equals `targetBeaconCount` on generator success)
- Test 22 rewritten: `TargetBeaconCount_RoundTripsThroughInputs` (small target vs large target → different graph sizes)

### `Biome1Distribution.asset`

New fields:
```yaml
_targetBeaconCount: 30
```

Keeps (algorithm-agnostic):
- `_globalMinSeparation` — retain current 90 as a Poisson-disc default (may need retune post-landing based on 30-node density feel)
- `_reconnectRadius` — retain current 500
- `_maxEdgeLength` — retain current 500 (may set to 0 = disable after retreat; user preference)
- `_allowBidirectional: 1`

## KEPT untouched

Non-generator SO surface stays fully intact:
- `_displayName`, `_biomeId`, `_nonTerminalBeaconTypes`, `_terminalBeaconType`,
  `_combatArchetypes`, `_mapTheme`, `_beaconFuelCosts`, `_stormCounterStart`,
  `_havenFuelRefillPercent`, `_biomeStartingFuelModifier`

Non-placement generator surface stays intact:
- `BowyerWatsonEdges`, `PruneEdges`, `EnforceMaxEdgeLengthCull`,
  `EnforceReconnectRadius`, `ValidateEdgeSet`,
  `ForwardPathGuaranteeToTerminal`, `AssignBeaconTypes`, sampling helpers
- Salt constants (`TopologySalt`, `BeaconTypeSalt`, `ArchetypeSalt`), start/terminal placement regions

## Rollback

If uniform Poisson output isn't what the sketch showed, rollback is:
- Revert this slice
- Return to cluster-lane state at commit before this retreat
- Re-tune cluster-lane geometry — but TD already declined that path

Not a real risk: uniform Poisson is what the sketch depicts, and it's the
pre-pivot algorithm that shipped for two weeks before this morning's
cluster-lane pivot.

## Technical Director Review

**APPROVE the retreat to uniform Poisson-disc + Delaunay. Do NOT preserve
cluster-lane as a degenerate shape.**

(Full verdict delivered inline in this session; punch list mirrored above
in "What is being destroyed" / "What is being ADDED" sections. Field-level
survival matrix pinned in the SO section.)

Success criteria:
- Designer's next screenshot matches sketch: ~30 nodes, 2–4 connections each, no clumps.
- Grep of `LaneCount|ClustersPerLane|BeaconsPerCluster|ClusterRadius|LaneYSpread|LaneWobbleAmplitude`
  returns zero across the repo.
- Test suite green with target-count invariant.
- Generator SO surface freeze commitment: 5 slices minimum before another
  placement-algorithm change; any further shape change requires pinned
  sketch + written brief before TD engages.

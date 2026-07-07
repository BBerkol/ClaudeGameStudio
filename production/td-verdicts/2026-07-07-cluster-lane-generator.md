# TD Verdict — Cluster-Lane Biome Generator (2026-07-07)

## Context

Current `BiomeWebGenerator` uses Poisson-disc uniform placement + Delaunay
+ forward-bias pruning + max-edge cull. Isotropic density by construction
— cannot produce the "corridor → cluster → corridor" traversal shape the
designer wants.

## Design intent (locked in)

Multi-lane branching map — Slay-the-Spire style, clusters as junctions:

- **2–3 parallel lanes** per run
- **1–3 clusters per lane**
- **3–6 beacons per cluster**
- Lanes are band-like but organically wobbly, concentrated in middle-Y
- Lane switching is **distance-based** — Delaunay + max-edge cull handles
  it; no explicit lane restriction on edges
- Start = single shared entry (left); Terminal = single shared exit (right)

## Proposed algorithm

1. Roll `laneCount ∈ [_laneCountRange]`, per lane `clusterCount ∈
   [_clustersPerLaneRange]`, per cluster `beaconCount ∈
   [_beaconsPerClusterRange]`.
2. Distribute lane centerlines across `[0.5 - _laneYSpread, 0.5 +
   _laneYSpread]`. Each lane's Y at X = base + sine-wobble.
3. Place cluster centers evenly along X ∈ [0.1, 0.9] per lane, with
   X-jitter; Y from the wobbled lane.
4. Scatter beacons within each cluster.
5. Force Start (left, mid-Y) and Terminal (right, mid-Y).
6. Delaunay + forward-bias + max-edge cull (unchanged).
7. Verify forward-path Start→Terminal; retry seed on failure
   (`MaxRetries=100`).

## Technical Director Review

### Q1 — ADR-0015 fit: CONFIRMED, no drift

These are content-shape parameters (band count, cluster count, scatter
shape), not behavioral seams. Same category as `BeaconFuelCosts[]` and
`StormCounterStart`. No enum switches, no code branches per biome. Ship
it.

### Q2 — `_beaconCountRange` retirement: NOT clean as proposed. Fix required

Grep shows the SO field, the `BiomeGenerationInputs` record property,
the factory unpack, and **11 test call sites** all name
`BeaconCountRange`. Deleting the SO field but keeping the record
property = ADR-0011 parallel-storage bridge. Do the **full cut in-slice**:
SO field gone, record property gone, factory drops the tuple, tests
rewritten to assert the new emergent range. Same discipline as
ADR-0010's Phase 5 grep gate — one commit, all sites migrated. The
`Assert.GreaterOrEqual/LessOrEqual` invariant becomes derived bounds
`laneCount * clusterCount * beaconsPerCluster`.

### Q3 — Algorithm amendments

- **Anisotropic cluster scatter**: uniform disk gives isotropic blobs.
  Prefer X-radius < Y-radius (e.g., `_clusterRadiusX = 45,
  _clusterRadiusY = 65`) so clusters read as vertical decision-points
  along an X-progressing lane, not round splotches that break the
  "lane-band" gestalt.
- **Wobble determinism**: sine-of-X with no phase term makes all lanes
  wobble in phase. Add per-lane phase offset `laneIndex * PI * 0.618`
  (golden ratio, deterministic, no RNG needed). Zero cost, breaks
  visual pattern-repeat.
- **Poisson-reject inside cluster**: at r=55, 6 beacons in a disk is
  already tight. Bump `PoissonCandidatesPerInsertion` to 60 for cluster
  fill. Consider fallback: accept-nearest-non-colliding rather than
  restarting the outer attempt.
- **Delaunay + max-edge cull is still the right connector.** Do NOT
  touch the pruning/reconnect passes.

### Cleanup deltas

- `MinSeparationPx` public const and `MinSeparationSchedule` retire
  cleanly. `MinSeparationSchedule` retires wholesale — replace loop
  body, no length-1 relaxed schedule as a "just in case" bridge.
- `BiomeWebGenerator_Test.cs` line 193 reference to `MinSeparationPx`
  becomes single `_globalMinSeparation` mirror, or deleted from public
  surface entirely.

### Three-lens self-audit

- **Health** — ADR-0011 delta above. Cross-check test file constants
  are cleaned in same slice.
- **Optimization** — Delaunay is O(n²) worst-case (Bowyer-Watson).
  Emergent typical n=15–25 is fine; the 6–54 upper bound worth pinning
  as a hard cap in `ValidateInputs` (throw if lane × cluster × beacon >
  64) so a runaway roll can't hang generation for 100 retries × O(n²)
  triangulation.
- **1.0 Survival** — Parameter names survive to shipping and map
  directly to the designer's "wide plains vs narrow canyon" tuning
  knob. `_laneWobbleAmplitude` is the one field flagged as
  maybe-throwaway: if sine looks synthetic, next slice's answer might
  be Perlin-noise displacement instead. Not blocking — sine is cheap
  and reversible.

### Verdict

**APPROVE the algorithm shape and SO field extension. CONCERNS block
sign-off on the migration:**

1. `BeaconCountRange` must be cut from SO + record property + factory +
   tests in-slice (not just the SO).
2. `MinSeparationPx` / `MinSeparationSchedule` public surface cleaned
   up in-slice (no orphaned const).
3. Hard cap on lane × cluster × beacon product added to
   `ValidateInputs`.
4. Adopt anisotropic cluster radius + per-lane wobble phase before
   authoring `Biome1Distribution.asset` retune.

### Success criteria

- Designer authors Biome 2 with different `_laneCountRange` and gets a
  categorically-different traversal feel without touching code.
- Grep of `BeaconCountRange` returns zero after the slice lands.
- Test suite green with new emergent-count assertions.

## Files touched

- `Assets/Scripts/Run/BiomeWebGenerator.cs` — placement algorithm
  replaced, connectivity/pruning unchanged.
- `Assets/Scripts/Run/BiomeGenerationInputs.cs` — record property
  swap.
- `Assets/Scripts/Run/Authoring/BiomeDistributionSO.cs` — new fields,
  retired `_beaconCountRange`.
- `Assets/Scripts/Run/Authoring/BiomeGenerationInputsFactory.cs` —
  new-field unpack.
- `Assets/Tests/EditMode/Run/BiomeWebGenerator_Test.cs` — 11 call
  sites migrate; emergent-count invariant.
- `Assets/Resources/Run/Biomes/Biome1Distribution.asset` — YAML gains
  new fields, drops old.

## Post-implementation tuning delta (2026-07-07, same day)

First playtest after landing the slice hit
`InvalidOperationException: Biome generator exhausted MaxRetries=100` on
`RunSceneHost.RestartRun`. Root cause: two overlapping geometric mismatches
between the SO defaults and the placement math.

**Problem 1 — cluster ellipse too tight for max beacon count.** At
`_globalMinSeparation=50` px, each beacon needs ~2,165 px² of exclusive
area (hex packing). Ellipse area π × 45 × 65 ≈ 9,189 px² fits ~3 beacons
realistically, but `_beaconsPerClusterRange=(3, 6)` asked for up to 6.
`TryPlaceLaneClusters` failed placement, retries exhausted.

**Problem 2 — `_maxEdgeLength=400` culls cluster-to-cluster forward links.**
Cluster X positions span [0.20, 0.80] with 2–3 clusters per lane; spacing
576–1152 px. `MaxEdgeLength=400` dropped every cluster-to-cluster edge,
`ForwardPathGuaranteeToTerminal` failed on any seed where lane clusters
placed apart.

**SO delta applied:**

- `_maxEdgeLength: 400 → 0` (disable cull — Delaunay + forward-bias
  pruning is enough structural discipline; ceiling re-enables in Biome 2
  with sane spacing math)
- `_beaconsPerClusterRange: (3, 6) → (2, 4)` (fits at 50 min-sep)
- `_clusterRadiusX: 45 → 55`, `_clusterRadiusY: 65 → 85` (extra room so
  the max-4 case doesn't sit at capacity edge)

**Known follow-up:** SO code defaults in `BiomeDistributionSO.cs` still
carry the old designed values (`(3, 6)`, radii 45×65, MaxEdgeLength 0
already). If a fresh SO is created via `CreateAssetMenu`, the (3, 6) beacon
default will exhaust the generator. Sync the code defaults to the asset
values in a follow-up hygiene pass, or add an OnValidate warning when
worst-case-cluster-density exceeds the ellipse capacity.

## PIVOT REVERSED — same-day retreat (2026-07-07, PM)

The cluster-lane approach was reversed **the same day it landed.** Root
cause was not tuning — it was that the algorithm's output shape
(dense clumps + corridor connectors) categorically does not match the
designer's mental model. Designer's hand-drawn sketch is a uniform hex-mesh
of ~30 nodes with 2–4 connections each, no clumps — which is exactly what
the pre-pivot Poisson-disc + Delaunay generator produced for two weeks
before this morning's pivot.

**TD verdict on retreat (delivered inline this session):** APPROVE retreat
to uniform Poisson-disc + Delaunay. Do NOT preserve cluster-lane as a
degenerate shape. Full cut of cluster vocabulary per ADR-0011 (no bridges).

Retreat capture: [`production/polish-captures/2026-07-07-generator-retreat.md`](../polish-captures/2026-07-07-generator-retreat.md).

**Process fix locked in:** 5-slice generator SO surface freeze after the
retreat lands. Any further placement-algorithm change requires (a) a
pinned reference sketch from the designer, and (b) a written brief on why
current shape fails vs sketch, before TD engages.

**Landmark node pass (verdict `2026-07-07-landmark-node-shape.md`)
unchanged:** the overlay ships at Biome 2 slice 1 and is compatible with
either placement algorithm — landmark placement is post-Bowyer-Watson,
rejection-sampled against the existing graph.

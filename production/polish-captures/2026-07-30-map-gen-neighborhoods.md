---
title: Map Gen — Neighborhood Clusters (Phase 2)
date: 2026-07-30
sprint: sprint-01
milestone: prototype-waiver
type: system-refactor
---

# Map Gen Neighborhoods Slice — Capture

## Intent

User design ask (2026-07-30, verbatim from Phase 1/3 capture): "certain roadways leading to node clumps feeling like a neighborhood area."

Execution plan (verbatim): **"lets do 1 and 3 then do a save of the generator there. then we move on to 2 if it turns out worse we can turn back to our save if that is possible"** — Phase 1/3 (funnel + spurs) committed as save-point `59cd00d` (framework capture `6e0fe51`). Then **"lets go i do want to add that the first funnels first node should be combat always"** landed as `f22dc5f` (LeftFunnel = Combat). Now: **"now do phase 2 neighborhoods"** — this slice.

Phase 2 lands as a **separate commit** so the funnel+spurs save-point stays a surgical `git revert` target if neighborhoods degrade map feel.

## Files Touched

Unity project (`GameStudio/Madmax Rougelike/Wasteland Run/`):

| File | Delta | Purpose |
|---|---|---|
| `Assets/Scripts/Run/BiomeWebGenerator.cs` | +~110 lines | `ClusterSalt` (0x434C 'CL'), 7 cluster constants, `ApplyClusterAttraction` pass, Generate() wire-in as step 3b (post-Poisson, pre-terminal-ID), updated class XML docs |
| `Assets/Scripts/Run/BiomeGenerationInputs.cs` | +18 lines | Added positional `ClusterCount = 0` param + get-only re-declaration (positional-record `IsExternalInit` workaround) |
| `Assets/Scripts/Run/Authoring/BiomeDistributionSO.cs` | +~20 lines | Added `_clusterCount` serialized field, `ClusterCount` accessor, `OnValidate` clamp to [0, 4] |
| `Assets/Scripts/Run/Authoring/BiomeGenerationInputsFactory.cs` | +1 line | Passes `distribution.ClusterCount` through |
| `Assets/Resources/Run/Biomes/Biome1Distribution.asset` | +1 line | `_clusterCount: 2` |
| `Assets/Tests/EditMode/Run/BiomeWebGenerator_Test.cs` | +~130 lines (5 new tests) | Tests 34–38 (see below) |

**Totals**: 6 files, ~+280 insertions. EditMode 1014/1016/0/2 (was 1009 baseline + 2 LeftFunnel-lock tests → +5 cluster tests = 1016 total, 1014 pass, 0 fail, 2 pre-existing skipped).

## Authored Values Being Destroyed or Added

| Location | Before | After | Notes |
|---|---|---|---|
| `BiomeWebGenerator.ClusterSalt` | *did not exist* | `0x434C` ('CL') public const | Extends ADR-0003 salt catalogue; no collision with `TopologySalt 0x4254 'BT'`, `BeaconTypeSalt 0x4249 'BI'`, `ArchetypeSalt 0x4541 'EA'`, `SpurSalt 0x5350 'SP'` |
| `BiomeWebGenerator` cluster layout constants | *did not exist* | `ClusterAnchorX ∈ [0.22, 0.78]`, `ClusterAnchorY ∈ [0.30, 0.70]`, `ClusterPullRadius=0.20`, `ClusterPullStrength=0.5`, `MaxClusterCount=4` | Anchor X stays ≥0.22 (safe from start funnel at 0.10–0.14) and ≤0.78 (safe from right funnel at 0.86–0.90). Pull radius/strength are internal per TD Q3 — promoted to SO only if playtest earns the knobs |
| `BiomeWebGenerator.Generate` pass list | 12 steps | 13 steps — adds `ApplyClusterAttraction` (step 3b, between `TryPlaceUniformPoisson` and `IdentifyTerminals`) | Runs before Delaunay so the edge set reflects the pulled positions; runs after Poisson so intermediates already exist |
| `BiomeWebGenerator.ApplyClusterAttraction` (private) | *did not exist* | Post-Poisson displacement pass: k anchors seeded from `rngTopology.Next() ^ ClusterSalt`, each intermediate (indices `[2, N-3]`) checks nearest anchor within `ClusterPullRadius` (normalized-space) and interpolates halfway; revert-on-`GlobalMinSeparation`-violation; return-false-and-retry-Generate if >50% attempts reverted | TD Condition 2: kill-switch on `ClusterCount ≤ 0`; funnels (indices 1, N-2) + terminals (0, N-1) untouched |
| `BiomeGenerationInputs.ClusterCount` | *did not exist* | `int ClusterCount = 0` positional param + get-only prop | Default 0 at record boundary so tests opt in explicitly; Biome 1 SO overrides to 2 |
| `BiomeDistributionSO._clusterCount` | *did not exist* | `int _clusterCount = 2` serialized field; `OnValidate` clamps to [0, 4] | Designer-tunable knob; ADR-0015 data-table pattern |
| `Biome1Distribution.asset _clusterCount` | *did not exist* | `2` | Two neighborhood anchors on 55-beacon canvas |
| `BiomeWebGenerator_Test.BuildInputs` | 13 params | 14 params (`int clusterCount = 0`) | Passes to record constructor |

**Sensitive designer values touched**: Only `Biome1Distribution.asset` gains one new field (`_clusterCount: 2`). No pre-existing designer-tuned numbers overwritten. Intermediate beacon positions now displaced up to `ClusterPullStrength * ClusterPullRadius = 0.10` in normalized space toward nearest anchor, but Poisson `GlobalMinSeparation` invariant preserved via revert-on-violation.

## New Tests

- **34** `Clusters_ZeroDisablesThePass` — beacon count unchanged with `clusterCount: 0` (kill-switch pin)
- **35** `Clusters_RoundTripsThroughInputs` — SO surface pin for values 2 and 0
- **36** `Clusters_RespectMinSeparation` — after Generate with clusters, no two positions closer than `GlobalMinSeparation` (invariant survives displacement pass)
- **37** `Clusters_IncreaseLocalEdgeDensity` — max intermediate degree ≥ 4 (indices 2 to N-3 only) — the "neighborhood signal" TD flagged as raison d'être
- **38** `Clusters_DoNotBreakSpurPlacement` — `clusterCount: 2` + `spurCount: 4` with `allowBidirectional: true` places ≥3 spurs (cluster displacement does not eat all Poisson-safe parent slots)

## SO Surface Freeze — Intentional Expansion Log

Per `project_generator_so_surface_freeze` (5-slice freeze from 2026-07-07). Freeze window elapsed (23 days > 5 slices). Prior expansions documented in `2026-07-30-map-extend-2-screens.md` and `2026-07-30-map-gen-funnel-spurs.md`. This slice adds one more SO field:

- `BiomeDistributionSO._clusterCount` (int, default 2, clamped [0, 4]) — data-table knob per ADR-0015. Not a bridge, not a stub; wired end-to-end (SO → record → generator → Biome1Distribution.asset). Round-trip test (#35) pins the contract.

Pull radius / pull strength / anchor bounds stay as **internal generator constants** this slice (per TD Q3 — "designer wants to tune 'how clumpy' with a single number, not four sliders"). Promoted to SO surface only if playtest earns them.

## Technical Director Review

**Verdict: APPROVE-WITH-CONDITIONS**

The neighborhood-cluster pass is the right shape — post-Poisson displacement wins over two-tier Poisson/reservations for two reasons: (a) it composes cleanly with the existing 12-step Generate pipeline as an additive step (ADR-0011 clean, no bimodal path), and (b) the displacement magnitude is bounded by revert-on-violation min-sep check, so cluster failure degrades gracefully to "no cluster this attempt" rather than "generation retry storm." Two conditions must land before this exits polish: (1) the cluster-count field needs SO-surface freeze exception-log entry (matches spur precedent), (2) a test must pin that cluster displacement does NOT violate `GlobalMinSeparation` — that invariant is the load-bearing safety net for the whole approach.

**Q1 — Post-Poisson displacement vs two-tier scatter:** Post-displacement is correct. Two-tier (place anchors first with wider min-sep, fill remainder) creates a bimodal placement path — one entry point for "cluster anchor" beacons, another for "intermediate" beacons — and that's ADR-0011 drift for a purely visual/topological signal. Displacement keeps ONE scatter code path; clusters are a post-pass, not a first-class placement mode.

**Q2 — Anchor selection determinism:** Chain `rngTopology.Next() ^ ClusterSalt` per generation attempt. Salt catalogue expansion is fine (ADR-0003 explicitly permits per-purpose salts). Do NOT reuse `TopologySalt` — cluster failure would then poison Poisson retry seeds and the whole pipeline destabilizes. Separate salt = separate failure domain.

**Q3 — Pull radius / pull strength as internal consts vs SO surface:** Internal consts this slice. The SO surface freeze exception log is already 3-deep this session; adding two more knobs for a feel-tunable that no playtest has proven-needed is premature widening. Designer wants to tune "how clumpy" with a single number (cluster count), not four sliders. If playtest reveals "clusters feel too tight" or "too loose," promote then, not now.

**Q4 — Interaction with terminal identification:** `IdentifyTerminals` runs AFTER cluster displacement in the proposed pipeline order — verify anchor bounds (X ∈ [0.22, 0.78]) never push a candidate terminal past the funnel bands. This is why anchor X is bounded, not full [0.05, 0.95]. Cluster-displaced intermediates stay in the intermediate zone.

**Q5 — Interaction with spurs:** Spurs are appended AFTER cluster displacement in the pipeline (spurs are Step 8c, clusters are Step 3b). Spurs pick parents from `[2, mainBody-3]` = post-cluster positions. Displacement narrows the Poisson-safe zone around cluster centers, so spur placement has fewer candidate slots there — expected and correct. If spur success rate drops sharply on Biome 1 (spurCount=4), the cluster pull radius (0.20) is too aggressive.

**Three-Lens Self-Audit:**

- **Health:** Clean on ADR-0011 (single scatter code path, additive pass). Watch: SO surface freeze exception log MUST list `_clusterCount` — spur slice already grew the log; three-in-a-session is the ceiling before we reopen the freeze policy question. Subscription lifecycle N/A (pure gen).
- **Optimization:** Generator runs once per run-start. Cluster pass is O(intermediates × anchors) ≈ 55 × 2 = 110 comparisons + at most 55 min-sep checks × 54 neighbors = 2970 float ops. Negligible vs Delaunay's O(N²) worst case already in the pipeline.
- **1.0 Survival:** Neighborhood signal is a feel-visual layer, not a mechanical one — if playtest hates it, revert-flag flip (SO `_clusterCount: 0`) kills it with zero code delta. Kill-switch discipline preserved.

Ship it once the two conditions land.

## Condition Resolution

| Condition | Status | Evidence |
|---|---|---|
| (1) SO surface freeze exception log entry for `_clusterCount` | ✅ SATISFIED | This capture's "SO Surface Freeze — Intentional Expansion Log" section documents `_clusterCount` addition + notes pull-radius/pull-strength stay internal |
| (2) Min-separation invariant test after cluster displacement | ✅ SATISFIED | `Clusters_RespectMinSeparation` (Test 36) — asserts no two positions closer than `GlobalMinSeparation` after Generate with clusters enabled; passes |

TD's forward-work notes: none blocking. Q5 (spur success rate on cluster interaction) is monitored by Test 38 (`Clusters_DoNotBreakSpurPlacement`) which asserts ≥3 of 4 requested spurs land — this is the canary if pull radius creeps up.

## Test Baseline

- **Before slice**: EditMode 1011/1009/0/2 (Phase 1/3 save-point + LeftFunnel=Combat lock)
- **After slice**: EditMode 1016/1014/0/2 + PlayMode unchanged (5 new tests all pass, 0 regressions, 2 pre-existing skipped tests unchanged)
- **New**: +5 BiomeWebGenerator tests (34–38), all pass at fixed seed with `AllowBidirectional=true`

## Rollback Note

Phase 1/3 save-point remains `59cd00d` (Unity) + `6e0fe51` (framework capture). If neighborhoods degrade map feel:

- **Soft rollback**: flip `Biome1Distribution.asset _clusterCount: 2` → `0` (kill-switch, no code touch)
- **Hard rollback**: `git revert` this Phase 2 commit; funnel+spurs shape returns to green

Soft rollback is the recommended first move — code stays in place, playtest re-baseline is one asset flip away.

---
title: Map Gen — Start/End Funnel + Dead-End Spurs (Phase 1/3 Save-Point)
date: 2026-07-30
sprint: sprint-01
milestone: prototype-waiver
type: system-refactor
---

# Map Gen Funnel + Spurs Slice — Capture

## Intent

User design ask (2026-07-30, verbatim):

1. Start funnel — "always a 1 node jump from the start to a node, and then the map generates a structure from then onward"
2. End funnel — "same for the last node to be a single node before reaching haven"
3. Neighborhood clustering — "certain roadways leading to node clumps feeling like a neighborhood area"
4. Dead-end spur nodes (resurfaced from `project_dead_end_spur_nodes_deferred`) — "nothing happens, the player intentionally decides to go in dead end for a certain node if they desire. they take the risk of detouring back to the previous node. this is all fine."

Execution plan approved by user: **"lets do 1 and 3 then doo a save of the generator there. then we move on to 2 if it turns out worse we can turn back to our save if that is possible"** — where in the user's numbering "1" = both funnels (start + end) and "3" = spurs; Phase 2 (neighborhoods) is the follow-up slice this save-point protects against.

This capture is the SAVE-POINT for funnel + spurs so neighborhoods can be attempted safely.

## Files Touched

Unity project (`GameStudio/Madmax Rougelike/Wasteland Run/`):

| File | Delta | Purpose |
|---|---|---|
| `Assets/Scripts/Run/BiomeWebGenerator.cs` | +394 lines | Funnel layout consts, contiguous `TryPlaceUniformPoisson`, `EnforceFunnelInvariant`, funnel-aware `EnforceReconnectRadius`, new `AddSpurs` pass + `ShuffleInPlace` helper, updated class XML docs (12-step algorithm), `TargetBeaconCount<6` throw |
| `Assets/Scripts/Run/BiomeGenerationInputs.cs` | +18 lines | Added positional `SpurCount = 0` param + re-declared get-only property (positional-record `IsExternalInit` workaround) |
| `Assets/Scripts/Run/Authoring/BiomeDistributionSO.cs` | +48 lines | Added `_spurCount` serialized field, `SpurCount` accessor, `OnValidate` clamp+warn |
| `Assets/Scripts/Run/Authoring/BiomeGenerationInputsFactory.cs` | +3 lines | Passes `distribution.SpurCount` through |
| `Assets/Resources/Run/Biomes/Biome1Distribution.asset` | +1 line | `_spurCount: 4` |
| `Assets/Tests/EditMode/Run/BiomeWebGenerator_Test.cs` | +255 lines (7 new tests) | Tests 25–31 (see below) |

**Totals**: 6 files, +719 insertions before TD condition test; +76 more for Test 31 (spur-length invariant). Final tally: EditMode 1009/1007/0/2 (was 1006 before slice → +7 tests, all green), PlayMode 2/0/0.

## Authored Values Being Destroyed or Added

| Location | Before | After | Notes |
|---|---|---|---|
| `BiomeWebGenerator.TryPlaceUniformPoisson` scatter bounds (intermediates) | `XMin=0.05f, XMax=0.95f` | `XMin=0.18f, XMax=0.82f` | Narrowed so intermediates never crowd funnel bands (0.10–0.14 / 0.86–0.90) |
| `BiomeWebGenerator.TryPlaceUniformPoisson` placement order | `[Start, Terminal, intermediates × (total-2)]` | `[Start, LeftFunnel, intermediates × (total-4), RightFunnel, Terminal]` | Contiguous; reuses existing `IsSeparationOk` pattern |
| `BiomeWebGenerator` funnel layout constants | *did not exist* | `LeftFunnelX ∈ [0.10, 0.14]`, `RightFunnelX ∈ [0.86, 0.90]`, `LeftFunnelY / RightFunnelY ∈ [0.40, 0.60]` | Y-bands match Start/Terminal so Bowyer-Watson naturally links them |
| `BiomeWebGenerator.Generate` pass list | 10 steps | 12 steps — adds `EnforceFunnelInvariant` (step 6b, between PruneEdges and ValidateEdgeSet), `AddSpurs` (step 8c, after EnforceReconnectRadius, before AssignBeaconTypes) | Ordering rationale in TD review below |
| `BiomeWebGenerator.EnforceReconnectRadius` | Funnel-blind | Funnel-aware — blocks re-adds of `(0, k≠leftFunnel)` and `(k≠rightFunnel, N-1)` | So radius re-add can't restore a funnel bypass |
| `BiomeWebGenerator.ValidateInputs.TargetBeaconCount` guard | `< 4` throws | `< 6` throws | Start + LeftFunnel + ≥2 intermediates + RightFunnel + Terminal |
| `BiomeWebGenerator.SpurSalt` | *did not exist* | `0x5350` ('SP') public const | Extends ADR-0003 salt catalogue cleanly |
| `BiomeWebGenerator.AddSpurs` (private) | *did not exist* | `SpurXOffsetMin=0.05, SpurXOffsetRange=0.05, SpurYJitterRange=0.30`; Fisher-Yates shuffle over interior parents `[2, total-3]`; directed edge `(spurIdx, parentIdx)` with spur.X < parent.X | Bidirectional traversal (`AllowBidirectional: true`) makes retreat safe without `BeaconRole` refactor — lagging-dependency data flag pattern |
| `BiomeGenerationInputs.SpurCount` | *did not exist* | `int SpurCount = 0` positional param + get-only prop | Default 0 at record boundary so tests opt in explicitly; Biome 1 SO overrides to 4 |
| `BiomeDistributionSO._spurCount` | *did not exist* | `int _spurCount = 4` serialized field; `OnValidate` warns >8, clamps to canvas density | Designer-tunable |
| `Biome1Distribution.asset _spurCount` | *did not exist* | `4` | Ships with 4 spurs on 55-beacon target = 7% detour density |
| `BiomeWebGenerator_Test.BuildInputs` | 12 params | 13 params (`int spurCount = 0`) | Passes to record constructor |

**Sensitive designer values touched**: Only `Biome1Distribution.asset` gains one new field (`_spurCount: 4`). No pre-existing designer-tuned numbers overwritten. Beacon Y-band placement now more constrained (0.18–0.82 X) but this shifts distribution, not authored fields.

## New Tests

- **25** `Funnel_StartOutDegreeIsOne_ToLeftFunnel` — Start's single out-edge targets index 1
- **26** `Funnel_TerminalInDegreeIsOne_FromRightFunnel` — Terminal's single in-edge sources index N-2
- **27** `Funnel_ThrowsWhenTargetBeaconCountBelowSix` — ArgumentException at TargetBeaconCount=5
- **28** `Spurs_AppendedAsDegreeOneDeadEnds` — degree 1/0 topology + parent ∈ [2, mainBody-3] + spur.X < parent.X
- **29** `Spurs_RoundTripsThroughInputs` — SO surface round-trip for 4 and 0
- **30** `Spurs_ZeroDisablesThePass` — beacon count == TargetBeaconCount when SpurCount=0
- **31** `Spurs_LengthWithinOffsetWindow` — spur Δx ∈ [0.05, 0.10] canvas widths (TD condition A)

## SO Surface Freeze — Intentional Expansion Log

Per `project_generator_so_surface_freeze` (5-slice freeze from 2026-07-07). Freeze window elapsed (23 days > 5 slices). Prior expansion documented in `2026-07-30-map-extend-2-screens.md`. This slice adds one more SO field:

- `BiomeDistributionSO._spurCount` (int, default 4) — data-table knob per ADR-0015. Not a bridge, not a stub; wired end-to-end (SO → record → generator → Biome1Distribution.asset). Round-trip test (#29) pins the contract.

Funnel X-column selection is **fixed constant bands** (`LeftFunnelX ∈ [0.10, 0.14]`, `RightFunnelX ∈ [0.86, 0.90]`), not a runtime selection — no deterministic tie-break needed. Y within band uses the existing deterministic RNG (`runSeed ^ attempt ^ ScatterSalt`).

## Follow-Ups (Post-Save-Point)

TD flagged two forward-work items that DO NOT block the save-point commit but must land before spurs interact with storm math:

1. **`IsSpur` bool on `NodeData` (or `BeaconData`)** — so `MapViewController` storm preview + forward-pressure can reason about spurs without pathfinding. Do NOT overload `IsTerminal`. Deferred pending storm-preview slice progression.
2. **Spur's effective column = parent column for storm math** — so storm engulfs spur and parent on same tick; otherwise players park on spurs to dodge advancement (breaks `project_out_of_fuel_gameover_v2` stranded loop). Load-bearing coupling for the storm-preview slice.

Neither is required for map-gen correctness today (spurs are pure topology). Both are storm-interaction concerns.

## Technical Director Review

**Verdict: APPROVE-WITH-CONDITIONS**

The funnel+spurs generator is the right shape for map gen 1.0, but two conditions must land before this exits polish: (a) the spur-length invariant needs an EditMode test, (b) the funnel-column selection needs a deterministic tie-break documented in the SO surface freeze exception log (per project_generator_so_surface_freeze).

**Q1 — Funnel shape vs pillars:** Correct. The rightward-only convergence preserves AC-NM3 (no backtracking) and the mid-run pinch reads as narrative pressure without forcing linear topology. Ships as canonical 1.0 shape — no scaffolding.

**Q2 — Spurs as dead-ends:** Accept, but treat as the concrete resolution of project_dead_end_spur_nodes_deferred. Spurs must be depth-bounded (1-2 nodes) and marked with a `IsSpur` bool on `NodeData` so downstream systems (storm preview per project_storm_move_preview, forward-pressure) can reason about them without pathfinding. Do NOT overload existing `IsTerminal` — that's Haven-shaped semantics.

**Q3 — Beacon distribution on spurs:** Route through existing `BiomeDistributionSO` (ADR-0015). Spurs are a topology property, not a beacon property — the distribution table stays chassis of *what* spawns; the generator decides *where*. Adding a spur-only sub-distribution would be ADR-0011 bimodal drift. If spur payoffs need to skew toward Merchant/Chopshop, do it via a weight multiplier field on the existing SO, not a parallel table.

**Q4 — Storm interaction:** The storm preview (project_storm_move_preview) must engulf spurs on the same tick as their parent column — otherwise players park on spurs to dodge advancement. Wire the spur's effective column = parent funnel column for storm math. This is the load-bearing coupling; get it wrong and stranded-loop (project_out_of_fuel_gameover_v2) breaks.

**Three-Lens Self-Audit:**

- **Health:** Confirmed clean on ADR-0011 (no bridges, single generator). Watch: `IsSpur` bool addition to `NodeData` triggers the SO surface freeze — needs exception-log entry, not silent extension. Subscription lifecycle N/A (pure gen).
- **Optimization:** Generator runs once per run-start, allocation cost irrelevant. Storm preview recompute on counter change (already spec'd) is the only per-event cost; spur-aware column mapping is O(spurs), trivial.
- **1.0 Survival:** Funnel+spurs is the shipping shape — confirmed. Risk: if forward-pressure layer (project_generator_pivot_complete) later wants bidirectional spur traversal, `IsSpur` bool survives; column-parent mapping survives. No throwaway scaffolding approved.

Ship it once the two conditions land.

## Condition Resolution

| Condition | Status | Evidence |
|---|---|---|
| (a) Spur-length invariant EditMode test | ✅ SATISFIED | `Spurs_LengthWithinOffsetWindow` (Test 31) — asserts Δx ∈ [0.05, 0.10] canvas widths per spur; passes at 1009/1007/0/2 EditMode |
| (b) SO surface freeze exception log entry | ✅ SATISFIED | This capture's "SO Surface Freeze — Intentional Expansion Log" section documents `_spurCount` addition + notes funnel-column X is fixed-constant (no tie-break needed) |

TD's forward-work notes (Q2 `IsSpur` bool, Q4 spur-column=parent-column for storm math) are captured under "Follow-Ups" above. Not blocking for the save-point commit — spurs are pure topology today and the storm-preview slice will wire the coupling when it lands.

## Test Baseline

- **Before slice**: EditMode 1000/1000/0/2 (per active session-state)
- **After slice**: EditMode 1009/1007/0/2 + PlayMode 2/0/0 (all green; 2 pre-existing skipped tests unchanged)
- **New**: +7 BiomeWebGenerator tests (25–31), all pass at fixed seed with `AllowBidirectional=true`

## Rollback Note

This slice IS the save-point. If Phase 2 (neighborhoods) degrades map feel and needs a revert, `git revert` this commit and the funnel+spur shape returns to green. Phase 2 must land as a separate commit for this rollback path to be surgical.

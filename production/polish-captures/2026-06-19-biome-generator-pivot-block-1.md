# Polish Capture: Biome Generator Pivot — Block 1 (Strip Generator + Tests Retirement)

**Date:** 2026-06-19
**System:** Run Loop — Node Map biome generator (POCO, `WastelandRun.Run`)
**Pivot block:** 1 of 2. Block 2 (NodeMap + MapView + ADR-0015 amendment + save-load test) scheduled NEXT-NEXT session.

## What's being destroyed

The FTL-strip `BiomeWebGenerator` shipped in Slice 6 sub-commit 2 (2026-06-16) is being replaced wholesale with a Poisson-disc + Delaunay + forward-bias-pruning generator. The strip generator works end-to-end (it carried Slice 6 closeout); the pivot is a design call by the user 2026-06-17 (memory: `project_generator_pivot_next_session.md`) because the strip-X grid produces visual proximity / reachability mismatches that break the FTL fantasy and the storm/fuel pressure layer that will land later wants bidirectional traversal to be possible.

**TD-FEASIBILITY (2026-06-19): CONCERNS** — sequencing only, not algorithm. Full verdict at `## Technical Director Review` below.

### Affected paths (Block 1)

- `Assets/Scripts/Run/BiomeWebGenerator.cs` — **REWRITE in place** (net delta tracked at commit time; strip layout / forward-cone code dropped; Bowyer-Watson + Poisson-disc + forward-bias pruning added)
- `Assets/Scripts/Run/BiomeGenerationInputs.cs` — **ADD** `bool AllowBidirectional` field (positional record, end-of-list, default not allowed — explicit caller responsibility per ADR-0011)
- `Assets/Scripts/Run/Authoring/BiomeDistributionSO.cs` — **ADD** `_allowBidirectional = true` serialized field + public accessor (lagging-dependency flag per `feedback_data_flag_lagging_dependency.md`; routed nowhere yet — Block 2 wires it into `NodeMap.Advance`)
- `Assets/Resources/biome/Biome1Distribution.asset` — **ADD** the new field value (Unity auto-serializes on first load if SerializeField has a default; verify .asset YAML on the manifest)
- `Assets/Tests/EditMode/Run/BiomeWebGenerator_Test.cs` — **REWRITE in place** (sibling `_v2_` file forbidden — ADR-0011 #5 bimodal-suffix bridge)

### Strip-generator authored values being destroyed

Captured from `Assets/Scripts/Run/BiomeWebGenerator.cs` HEAD:

**Public constants (deleted or repurposed):**
- `TotalStrips = 5` — DELETED (no strips)
- `MinNonTerminalPerStrip = 4` — DELETED
- `MaxNonTerminalPerStrip = 6` — DELETED
- `MinTerminalCount = 1` — KEPT (still the gate-funnel count bound)
- `MaxTerminalCount = 2` — KEPT
- `MinConnectionsPerSource = 1` — DELETED (Delaunay decides connectivity)
- `MaxConnectionsPerSource = 3` — DELETED
- `MinBeaconCount = 18` — KEPT (designer-facing target)
- `MaxBeaconCount = 22` — KEPT
- `MinSeparationPx = 80` — KEPT (Poisson-disc disc radius)
- `MinSeparationNormalized = 80/1920` — KEPT (derived)
- `CanvasAspectX = 1920` / `CanvasAspectY = 1080` — KEPT
- `TopologySalt = 0x4254` ('BT') — KEPT
- `BeaconTypeSalt = 0x4249` ('BI') — KEPT
- `ArchetypeSalt = 0x4541` ('EA') — KEPT
- `MaxRetries = 100` — KEPT

**Private layout constants (deleted):**
- `StripX0..StripX5` (0.05, 0.20, 0.40, 0.60, 0.80, 0.95) — DELETED (no strip X coords)
- `YMin = 0.10` / `YMax = 0.90` — KEPT (Poisson-disc bounds box; remove top/bottom margin same way)
- `XJitterFraction = 0.04` — DELETED (Poisson-disc inherently jitters)
- `ConnWeightThreshold1 = 1` / `ConnWeightThreshold2 = 3` — DELETED (no per-source weighted picks)
- `MaxJitterAttempts = 200` — DELETED (Poisson-disc has its own retry envelope per TD Q5: inner-relax 80→72→64 px)
- `StripXValues[]` array — DELETED

**Private methods deleted:**
- `PickStripCounts(rng)` — strip-shaped
- `PlacePositions(rng, stripCounts, positions, stripStart)` — strip-shaped placement
- `WireEdges(rng, stripCounts, stripStart, positions, edges)` — strip→strip+1 wiring + per-source 1-3 conn-weight roll + start-strip floor-at-2 + lateral 30% same-strip rolls
- `IsForwardConeOk(src, dst)` — 45° forward-cone test (dx≤0 reject + |dy_canvas|≤|dx_canvas|)
- Start-beacon outgoing floor-at-2 clamp (2026-06-17 eyeball-polish addition) — DELETED (Delaunay neighbour count for the leftmost-X node is generally >2 already; if Block 2 finds the choice still feels thin, add as a Poisson-disc placement seed bias)

**Edges-as-`List<(int From, int To)>`:** KEPT — TD Q2 verdict mandates Shape A (directed forward edges in graph; `NodeMap.Advance` consults `AllowBidirectional` policy; Shape B parallel-storage forbidden by ADR-0011 #2).

**`BiomeGraph` return shape:** KEPT — `(beacons, positions, edges, entryIndex, exitIndex)`. No DTO changes.

**`BiomeGenerationInputs` shape:** EXTENDED — append `bool AllowBidirectional` to positional record (`NonTerminalBeaconWeights`, `TerminalBeaconType`, `CombatArchetypeWeights`, **`AllowBidirectional`**). TD Q3 mandates flag lands Block 1 even though Block 1 has no internal consumer — flag's *value* (true) represents end state per `feedback_data_flag_lagging_dependency.md`; Block 2 = pure consumer change.

### Strip-test assertions being destroyed

`BiomeWebGenerator_Test.cs` HEAD has 11 tests. Each itemised so the bisect record sits in this file per TD Q4 verdict (single-commit in-place rewrite, no sibling `_v2_` file).

| # | Test | Verdict | Why |
|---|------|---------|-----|
| 1 | `Generate_IsDeterministicPerSeed` | **KEEP** | Engine invariant per ADR-0003; assertion shape (beacon count + index/type/archetype + positions + edge set equality) survives unchanged |
| 2 | `Generate_DifferentSeedsProduceDifferentGraphs` | **KEEP** | Determinism contrast; assertion already structural (count OR edges OR positions differ) |
| 3 | `Generate_AllBeaconsRespectMinSeparation` | **KEEP** | Poisson-disc min-sep invariant; same canvas-aspect-corrected distance test |
| 4 | `Generate_AllEdgesRespectForwardCone` | **DROP** | Forward-cone is strip-only invariant; Delaunay edges are bounded by triangulation geometry, not by ±45° from source. Replaced by statistical forward-bias test (new) |
| 5 | `Generate_EveryNonTerminalHasForwardPathToAnyTerminal` | **KEEP** | TD Q6 mandate: this is the *graph* invariant (Δx>0 path exists); preserved even under bidirectional *traversal policy*. BFS helper `BfsCanReachAnyOfType` walks edges directionally — unchanged. |
| 5b | `Generate_ExitIndex_IsFirstTerminalBeacon` | **KEEP** | Anchor contract per 2026-06-16 TD verdict; ExitIndex semantics unchanged |
| 6 | `Generate_HasGateFunnelOf1Or2BeaconsAtStrip5` | **RENAME + KEEP** | Rename to `Generate_HasGateFunnelOf1Or2TerminalBeacons` (strip-5 wording goes); assertion (terminal count in 1..2) still holds — gate funnel is now "rightmost cluster" but bound is the same |
| 7 | `Generate_BeaconCountIsInRange18To22` | **KEEP** | Designer-facing target; same constants |
| 8 | `Generate_HasExactly5StripsPlusStart` | **DROP** | 6 distinct X values invariant is strip-only; Poisson-disc emits real-valued continuous X. Replaced by Delaunay planarity test (new) |
| 9 | `Generate_TypeAssignmentRespectsDistributionWeights` | **KEEP** | Weighted-sample contract per ADR-0003 BeaconTypeSalt; unchanged. (Bumps Combat threshold from 80% to 80% — keep margin) |
| 10 | `Generate_ThrowsWhenNonTerminalWeightsEmpty` | **KEEP** | Guard test, validation unchanged |
| 11 | `Generate_ThrowsWhenCombatTypeRequiresArchetypeButPoolEmpty` | **KEEP** | Guard test, validation unchanged |

**Test fixture helpers being destroyed:**
- `StripXForIndex(int)` — DELETED (no strip X)
- `GetStripIndexForX(float)` — DELETED (no strip X)

**Test fixture helpers KEPT:**
- `MakeTwoTypeInputs`, `MakeSingleTypeRestInputs`, `MakeSkewedInputs` — same shape; will need `AllowBidirectional: false` appended to positional record (or named-arg) once `BiomeGenerationInputs` is extended
- `MakeGenerator`
- `BfsCanReachAnyOfType` — directional BFS, used by KEEP test #5

### New tests being added (Block 1)

1. **`Generate_NoSelfLoopEdges`** — for every `(from, to)` in `Edges`, `from != to`. TD Q7 mandate. New invariant for Bowyer-Watson + pruning.
2. **`Generate_NoDuplicateEdges`** — `(from, to)` set distinct (HashSet equivalence). TD Q7 mandate.
3. **`Generate_AllEdgeIndicesAreValid`** — `0 <= from, to < Beacons.Count`. TD Q7 mandate (defensive — Bowyer-Watson super-triangle indices must not leak).
4. **`Generate_EdgeSetIsDelaunayPlanar`** — no two edges geometrically cross (segment-intersection test) except at shared endpoints. Replaces test #8.
5. **`Generate_BidirectionalWalkReachesEveryBeacon`** — when `AllowBidirectional: true` is passed in `BiomeGenerationInputs`, treating edges as undirected, BFS from Start visits every beacon. This is the **policy** test (per TD Q6) — distinct from the graph-invariant test #5.
6. **`Generate_StatisticalAngleBias`** — over 100 distinct seeds (`FixedSeed..FixedSeed+99`), the aggregate ratio of edges where `Δx / edgeLength ≥ 0.5` (within 60° of horizontal) is `≥ 0.70`. Replaces test #4. **Note:** because edges are forward-oriented at emit time, the trivial "Δx > 0" assertion would be tautological — angle distribution is the meaningful invariant.
7. **`AllowBidirectional_FlagThreadsThroughInputs`** — round-trip the flag through `BiomeGenerationInputs` ctor → graph build → assert it doesn't perturb determinism (two calls, same flag, identical graphs; flipping flag does NOT change `Edges` set — only Block 2's `NodeMap.Advance` consumes it). Locks the no-consumer-yet contract.

**Net test delta:** 11 → 13 tests (-4 dropped, +7 added, 2 renamed). Helpers shrink by 2 methods.

### Why each verdict is binding

- Drop test 4 (forward-cone): the 45° cone is a strip-layout artefact. Delaunay's planarity bound replaces it as the geometric invariant.
- Drop test 8 (6 distinct X): Poisson-disc emits continuous-valued X. Holding "exactly 6 distinct X" would constrain the generator to a grid — i.e., re-introduce strips.
- Keep test 5 (forward-path BFS): per TD Q6, this is the *graph invariant* — "exists Δx>0 path Start→terminal." Survives the storm/fuel landing in a later slice because traversal policy is a separate consumer concern.

## Proposed change

**Algorithm (TD Q1):** Bowyer-Watson incremental Delaunay. N≤22, perf is non-issue. Textbook reference: super-triangle initialise, insert points one at a time, remove triangles whose circumcircle contains the new point, retriangulate the cavity. ~120-150 LoC, pure `System.Math`. Reject k-NN approximations — planarity by construction beats planarity by test.

**Placement (TD Q5):** Two-level retry envelope.
- **Inner (placement-level):** Poisson-disc with min-sep schedule 80 → 72 → 64 px, max 3 inner attempts. Failure surfaces to outer.
- **Outer (existing):** `MaxRetries = 100` with `attempt` salt. Outer fires only on Delaunay degeneracy (e.g. colinear cluster) or forward-bias <70% over local sample.

**Edge pruning:** After Delaunay, walk all triangle edges. For each undirected edge `(a, b)`, choose direction such that `positions[from].X < positions[to].X`. If `positions[a].X == positions[b].X` (true tie — extremely unlikely with Poisson-disc but possible), drop the edge. Per-edge keep probability = `Δx / edgeLength` clamped to `[ForwardBiasKeepFloor, 1.0]` where `ForwardBiasKeepFloor = 0.3f` is a named constant. Geometrically this is `cos θ` of the edge's angle from horizontal, with the floor acting as the explicit "how vertical is too vertical" knob. Scale-invariant by construction — no normalisation by a fragile `max(Δx)` value. Final invariant: aggregate ratio of edges where `Δx/edgeLength ≥ 0.5` (within 60° of horizontal) is `≥ 0.70` over 100 seeds.

**Bidirectional shape (TD Q2):** Edges stay forward-directed in the emitted graph. `BiomeGenerationInputs.AllowBidirectional` rides along; Block 2's `NodeMap.Advance` will check `if (AllowBidirectional && IsForwardEdge(toIndex, CurrentIndex))` to accept backwards walks. Generator does not double-emit.

**Generator-output invariants (TD Q7):** No self-loops, no duplicate `(from, to)`, all indices in `[0, Beacons.Count)`. Three new tests.

**Start beacon:** index 0 by convention. Placed first as the initial Poisson-disc sample at `(uniform(rng, 0.05, 0.15), uniform(rng, 0.40, 0.60))` — deterministic RNG-driven biased seed (centre-Y bias keeps Start visually anchored, X bias guarantees leftmost-cluster role). All subsequent points are pure Poisson-disc fill against this seed.

**Terminal beacons:** identify post-placement as the 1-2 rightmost-X beacons (gate-funnel emerges from geometry, not from a strip count). `ExitIndex` = first rightmost (anchor stable per test #5b).

**Determinism contract:** unchanged — `runSeed ^ attempt ^ salt` for placement RNG; `runSeed ^ beaconIdx ^ {BeaconTypeSalt | ArchetypeSalt}` for type/archetype sampling. ADR-0003 fresh-per-call RNG preserved.

## ADR-0015 doc/code gap mitigation (TD red flag)

ADR-0015 currently reads "FTL-style strips" in `## Summary`. Block 1 lands code that contradicts the doc. **Mitigation per TD verdict:** Block 1 commit body must contain:

> ADR-0015 amendment (drop "strip" language, broaden to "free-placed clustered Delaunay") scheduled for Block 2 commit. Per **ADR-0011 exception #1 (one-shot migration documented in-flight)**, the doc/code gap from Block 1 → Block 2 is NOT a forbidden bridge. **Block 2 must land by 2026-06-26 or this commit is reverted.** See `production/polish-captures/2026-06-19-biome-generator-pivot-block-1.md` § Technical Director Review.

This is the bridge-killer language. Without the named exception + the hard revert date, the in-between week is a forbidden transitional state. "Scheduled" without a date is intent, not commitment.

## Block 2 schedule confirmation (TD pushback)

TD asks: "confirm Block 2 date in commit body or Block 1 is dead code." Per `memory/project_generator_pivot_next_session.md`, Block 2 is the **session after** Block 1 — same calendar week. **User confirms-or-denies in approval step below.**

## MapView semantics flag (TD red flag)

`MapView.uss .wr-beacon--current` "you are here" + `MapViewController.BuildConnectionViewModels` reachability filter both assume forward-only progression. **NOT touched in Block 1.** Block 2 will rename `IsReachable` → `IsAdjacentToCurrent` and switch the filter to `from == CurrentIndex || to == CurrentIndex`. Flagged in commit body for visibility.

## Technical Director Review

**TD-FEASIBILITY: CONCERNS** (sequencing, not algorithm)

**Q1 — Delaunay algorithm.** Bowyer-Watson incremental. N≤22 means perf is a non-issue, but BW is the textbook reference algorithm — easy to grep against, easy to write a planarity test against, and the "one super-triangle, insert, retriangulate cavity" loop is ~120-150 LoC of pure System.Math. Override: **no**, take the obvious default. RNG is not needed inside BW itself; keep it deterministic-by-input. Reject (c) RNG/k-NN — planarity-by-construction beats planarity-by-test every time.

**Q2 — Edge representation.** Shape A: **directed forward-only edges in the graph; `NodeMap.Advance` consults `AllowBidirectional` and accepts the reverse**. Reasons: (i) statistical forward-bias test stays trivial (Δx>0 over the literal edge list); (ii) save-load DTO stays half the size; (iii) "edges are forward, traversal policy is configurable" mirrors ADR-0013 composition — data is canonical, behavior layers on top. Shape B doubles the edge set to encode a traversal policy, which is the exact "parallel storage" anti-pattern ADR-0011 #2 forbids.

**Q3 — `AllowBidirectional` lands Block 1.** Per `feedback_data_flag_lagging_dependency.md`, the flag's *value* must represent the end state — true today, true when storm lands, only its *consumer* (`NodeMap.Advance`) changes. If the flag lands Block 2, then Block 1 ships a generator with no policy hook and Block 2 retrofits one. That's a bridge by another name. **Thread `AllowBidirectional` through `BiomeGenerationInputs` in Block 1, even though the only Block 1 consumer is... nothing yet.** The SO field is what makes Block 2 a pure consumer change.

**Q4 — Rewrite the existing test file in place.** Sibling `_v2_` file is exactly the kind of bimodal-suffix bridge ADR-0011 #1/#5 forbids. The capture file enumerates the dropped assertions; that is the bisect record. One commit, one file, ADR-0011-clean.

**Q5 — Retry envelope.** Two-level. **Inner**: Poisson-disc relaxes via a fixed schedule (min-sep 80 → 72 → 64 px, max 3 inner attempts) inside one outer attempt — placement failures are a placement concern, not a topology concern. **Outer**: existing `MaxRetries=100` with `attempt` salt only fires on Delaunay degeneracy or forward-bias <70%. Don't shrink N inside the inner loop; N is a designer-facing target, not a fallback knob.

**Q6 — Strict-forward path test STAYS.** You named the right invariant: "exists a Δx>0 path Start→terminal." That is the invariant that survives the storm/fuel landing in a later slice — once forward pressure is on, bidirectional becomes a scouting affordance, not the topology. Test the topology, not the current traversal policy. Add a separate "bidirectional walk reaches every node" test for the *policy*; keep the strict-forward BFS for the *graph*.

**Q7 — Block 1 explicit invariants on emitted edges.** Yes: (i) no self-loops `(i,i)`; (ii) no duplicate `(from,to)` pairs; (iii) every edge endpoint is a valid beacon index. These are generator-output invariants regardless of `Advance` policy. Soft-cap stays Block 2.

**Red flags TD checked:**

- **Test-deletion volume**: 40-50 dropped assertions is fine *if* the capture file enumerates each by name + why it died (strip-shape obsolete, not strip-shape regression). That's the ADR-0011 receipt. Non-issue with discipline. **(Capture above does this — 11 named tests + helpers, 4 dropped 7 added 2 renamed.)**
- **ADR-0015 doc/code gap**: Real concern. Block 1 lands code that contradicts ADR-0015's "FTL-strip" language. **Mitigation**: Block 1 commit message MUST state "ADR-0015 amendment scheduled Block 2, see `production/td-verdicts/...`." That's the bridge-killer — a documented in-flight amendment is not a bridge per ADR-0011 exception #1 (one-shot migration).
- **MapView `.wr-beacon--current` semantics**: Flag in commit body, not code. View doesn't change Block 1 (no `NodeMap`/`MapView` work this block). Pure note.

**TD pushback not asked for:** Block 1 ships a generator nothing consumes yet. That is correct sequencing — but make sure the Block 2 session is actually scheduled before Block 1 lands, or you're sitting on dead code. Confirm Block 2 date in the commit body.

**ADRs invoked:** 0002, 0003, 0011 (#1, #2, #5, exception #1), 0013, 0015.

### Technical Director Re-Verification (2026-06-19, after capture draft)

User requested a double-check. TD reviewed the capture against the original verdict and called three blockers, one wording fix, one cleanup. All five amended above before user approval:

1. **Edge-pruning formula (BLOCKER, NEEDS-CHANGE → applied).** Original draft used `Δx / max(Δx)` keep-probability. TD: that couples two concerns (keep-rate + angle-bias) and "the `max(Δx)` normalisation makes one freak-wide edge collapse all others — fragile." Corrected formula: `Δx / edgeLength` clamped to `[ForwardBiasKeepFloor, 1.0]` where `ForwardBiasKeepFloor = 0.3f` is a named constant. Geometrically `cos θ` from horizontal; scale-invariant; floor is the explicit knob.

2. **Tautological test (BLOCKER, NEEDS-CHANGE → applied).** Original draft kept "Δx > 0" wording for the statistical test. TD: "Forward-orient-on-emit guarantees Δx≥0 by construction, so 'Δx>0' is just 'not a true tie'... You made the test tautological." Renamed `Generate_StatisticalForwardBias` → `Generate_StatisticalAngleBias`; threshold is `Δx / edgeLength ≥ 0.5` (within 60° of horizontal) over 100 seeds at ≥70%. That is the actual forward-bias claim.

3. **ADR-0015 mitigation needs a hard date (BLOCKER, NEEDS-CHANGE → applied).** TD: "'scheduled' without a date is intent, not commitment." Added explicit "Block 2 must land by 2026-06-26 or this commit is reverted" + named "ADR-0011 exception #1 invoked" in the quoted commit-body block.

4. **Speculative LoC estimate (CLEANUP, OVERRIDE → applied).** TD: "Drop it from the capture. Speculative LoC in a capture file becomes a fake target — Bowyer-Watson + Poisson-disc could land at 550 or 700 honestly." Replaced "~740 → ~600 LoC" with "net delta tracked at commit time."

5. **Start-placement wording (UNPROMPTED PUSHBACK → applied).** Original draft said Start placement was "not RNG-driven" then sampled from a range. TD: "Reconcile the wording or you'll get a determinism review flag next pass." Rewritten: Start placed at `(uniform(rng, 0.05, 0.15), uniform(rng, 0.40, 0.60))` — deterministic RNG-driven biased seed.

**Other TD verdicts on items I asked about (AGREE, no change required):**
- Δx==0 drop on true tie (no Y tiebreak) — honest answer; effectively unreachable with Poisson-disc on floats anyway.
- Terminal beacons identified post-placement as 1-2 rightmost-X — mirrors Start's leftmost-by-placement symmetry. No placement-time biasing for terminals.
- Flag-threading test (`AllowBidirectional_FlagThreadsThroughInputs`) — kept. ADR-0011 receipt that the flag's value represents end state.
- KEEP list (tests #2, #5b, #6 renamed, #7, #9, guards A+B) — none load-bearing-strip in disguise.
- DROP list (4 + 8 only) — only strip-load-bearing assertions in the suite.

**Final verdict after amendments: green light to implement Block 1 once user approves the amended capture.**


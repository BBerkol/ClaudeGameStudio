# Polish Capture: Biome Generator Pivot — Block 3 (max-hop distance constraint — CLOSING BLOCK)

**Date:** 2026-06-22
**System:** Run Loop — `BiomeWebGenerator` post-pruning max-hop re-add pass, `BiomeDistributionSO.MaxHopDistance` field, `BiomeGenerationInputs.MaxHopDistance` plumbing.
**Pivot block:** 3 of 3 — **closing block**. Generator pivot ends here. After Block 3, generator code is frozen and Slice 7 (ADR-0004 save/load) becomes the priority.

## Trigger — 2026-06-22 eyeball pass

After pushing Block 2 to origin (Unity `2b5114f`), the user ran an eyeball pass on the closed Slice 6 + Block 1 + Block 2 run loop. Findings, condensed from the playtest report:

- **First map:** cursor at Start. Two visually-close beacons clickable (forward-adjacent). Two MORE visually-close beacons were NOT interactable — within "reachable distance" by eye but no graph edge to cursor. Player couldn't reach them.
- **Second map:** same configuration worked — Block 2 bidirectional traversal confirmed, all visually-close beacons reachable.
- User asked: "is the 1-to-3 system tangling us?" — verified NO. That mechanic was the 2026-06-17 strip-generator polish (`Start strip outgoing edges floored at 2 post-RNG-roll`); Block 1 wiped it. Current `BiomeWebGenerator.cs` has no per-beacon outgoing-edge minimum. Grepped to confirm.

## Root cause (verified before TD brief)

`BiomeWebGenerator.PruneEdges` operates on the Bowyer-Watson Delaunay edge set with forward-bias keep probability `clamp(Δx/edgeLength, ForwardBiasKeepFloor=0.3, 1.0)`. Two compounding asymmetry sources:

1. **Delaunay neighbors ≠ visually-K-nearest beacons.** Empty-circumcircle pairs can skip a close-looking node.
2. **Forward-bias roll is stochastic per edge.** Even when a Delaunay edge exists, the roll can drop it.

First map = unlucky on Start's edges. Second map = lucky. Generator-output variance, not a Block 2 bug. Block 2 (`AllowBidirectional` + frontier filter switch) is confirmed working — the user observed full forward+backward traversal on map 2.

## User design ask (verbatim, lightly compressed)

> "the distancing is really rough right now. to be visually readable there should a be maximum distance to hop from one node to another, any node outside that distance we cant jump to. ofcourse the map generation should be considering this and giving possible pathways inside the generated node clutter for an exit. although this generation can choose to make only one reachable path to end, where the clutter only connects on a certain line way. The player will have to traverse accordingly even in that scenario."

Translation: a max-hop distance `D` such that visual proximity correlates with graph adjacency. Generator must respect this while preserving the forward-path-to-terminal invariant. Single-route bottlenecks through dense clutter are explicitly OK.

## Files at risk

**Unity repo (no values destroyed — pure additions + one constant guard tweak):**
- `Assets/Scripts/Run/Authoring/BiomeDistributionSO.cs` — add `_maxHopDistance` field + tooltip + `MaxHopDistance` property. Default per asset. `Biome1Distribution.asset` re-baked.
- `Assets/Scripts/Run/BiomeGenerationInputs.cs` — add `MaxHopDistance` to the positional record (alongside `AllowBidirectional`).
- `Assets/Scripts/Run/BiomeGenerationInputsFactory.cs` — propagate the field from SO to POCO.
- `Assets/Scripts/Run/BiomeWebGenerator.cs` — new `EnforceMaxHopReAdd` pass between `PruneEdges` and `ValidateEdgeSet`. Reads `MaxHopDistance` off `BiomeGenerationInputs`. Restricted to Delaunay candidate set (per TD).
- `Assets/Resources/Biomes/Biome1Distribution.asset` — bake initial `_maxHopDistance` value (tuning eyeball — start at ~280 canvas-px, iterate).
- `Assets/Tests/EditMode/Run/BiomeWebGenerator_Test.cs` — new test cases.

**Framework repo:**
- `production/polish-captures/2026-06-22-biome-generator-pivot-block-3.md` (this file).
- ADR-0015 NOT amended (per TD Q6 — no principle change).

## What's NOT being destroyed

Block 3 is pure additive. No fields removed, no comments retired, no test cases dropped. The existing `ForwardBiasKeepFloor`, Poisson schedule, BFS forward-path invariant, `AllowBidirectional` flag, and edge orientation rules all stand unchanged. The new pass slots between `PruneEdges` and `ValidateEdgeSet` and only ADDS edges that the pruning roll dropped from the Delaunay-candidate-within-D set.

## Block 3 design (post-TD)

### 1. `BiomeDistributionSO.MaxHopDistance` field

```csharp
[SerializeField,
 Tooltip("Maximum visual hop distance in canvas pixels. Any two beacons whose " +
         "Delaunay edge length is ≤ this value MUST be graph-adjacent — the " +
         "generator's max-hop re-add pass re-introduces such edges if forward-bias " +
         "pruning dropped them. Forward-orientation rule still applies: backward-X " +
         "edges (Δx ≤ 0) are NOT re-added. Tune in conjunction with the Poisson " +
         "min-separation schedule — D ≥ k · PoissonMinRadius for some k ensures " +
         "visually-close beacon pairs are always Delaunay-neighbours.")]
private float _maxHopDistance = 280f;

public float MaxHopDistance => _maxHopDistance;
```

### 2. `BiomeGenerationInputs` record extension

```csharp
public sealed record BiomeGenerationInputs(
    BiomeId           BiomeId,
    /* existing fields */,
    bool              AllowBidirectional,
    float             MaxHopDistance);    // NEW
```

### 3. `BiomeWebGenerator.EnforceMaxHopReAdd` pass

Inserted between `PruneEdges` and `ValidateEdgeSet`. Restricted to the Delaunay candidate set (`triEdges`) — NOT a full O(n²) pairwise sweep, per TD Q3 concern.

```csharp
private static void EnforceMaxHopReAdd(
    NormalizedPosition[] positions,
    HashSet<(int, int)> triEdges,
    List<(int From, int To)> kept,
    float maxHopDistancePx)
{
    if (maxHopDistancePx <= 0f) return;  // disabled when zero

    var keptSet = new HashSet<(int, int)>(kept);
    foreach (var e in triEdges)  // Delaunay-restricted
    {
        int a = e.Item1, b = e.Item2;
        NormalizedPosition pa = positions[a], pb = positions[b];

        // Forward orientation; drop Δx ≤ 0 (per TD Q4 — strict-forward BFS stays).
        int from, to;
        NormalizedPosition pFrom, pTo;
        if (pa.X < pb.X)      { from = a; to = b; pFrom = pa; pTo = pb; }
        else if (pb.X < pa.X) { from = b; to = a; pFrom = pb; pTo = pa; }
        else continue;

        if (keptSet.Contains((from, to))) continue;  // already kept

        float dxPx = (pTo.X - pFrom.X) * CanvasAspectX;
        float dyPx = (pTo.Y - pFrom.Y) * CanvasAspectY;
        float lenPx = (float)Math.Sqrt(dxPx * dxPx + dyPx * dyPx);
        if (lenPx > maxHopDistancePx) continue;

        kept.Add((from, to));
        keptSet.Add((from, to));
    }
}
```

### 4. New test cases (`BiomeWebGenerator_Test.cs`)

- `MaxHopDistance_DroppedEdgeReAddedWhenWithinD` — fixture where forward-bias pruning would drop a known-close edge; assert it's in the final edge list.
- `MaxHopDistance_BackwardEdgeNotReAdded` — Δx ≤ 0 candidates excluded from the re-add even if `lenPx ≤ D`.
- `MaxHopDistance_NonDelaunayPairsNotAdded` — pairs that are visually close but NOT Delaunay-adjacent stay out (no O(n²) sweep).
- `MaxHopDistance_ZeroDisablesThePass` — `MaxHopDistance == 0f` leaves the kept list untouched (kill-switch).
- `MaxHopDistance_BiomeDistributionSOFlagThreadsThroughInputs` — analog to the Block 2 `AllowBidirectional` threading test.

Test count delta: **+5 tests**. Target EditMode: 501 total / 500 passed / 0 failed / 1 skip.

### 5. Initial `_maxHopDistance` value — 280 canvas-px (eyeball start)

Reasoning: Poisson min-sep schedule is 80→72→64 px. TD's `D ≥ k · PoissonMinRadius` rule with k ≈ 3.5–4 gives ~280–320 px. Conservative starting point that should rescue the user-observed bug on map 1 without flooding the graph with edges. Tune via eyeball after first run.

### 6. CI grep-gate addition

Block 3 doesn't introduce retired vocabulary, but it does introduce one symbol worth gate-protecting against drift: nothing required. No new gates.

## ADRs invoked

- **ADR-0002** (POCO contract) — `BiomeGenerationInputs` stays engine-free.
- **ADR-0003** (deterministic RNG) — no new RNG draws (max-hop pass is deterministic given positions + kept set + D).
- **ADR-0011** — **TD Q7 verified clean.** Numeric constraint on canonical generator pass, single SO surface, no parallel storage, no stub returns, no transitional code.
- **ADR-0015** — **TD Q6 verified no amendment.** Algorithm shape (Poisson + Bowyer-Watson + forward-bias prune + forward-path BFS) unchanged. SO growing one field = normal table evolution.

## Technical Director Review

(Full TD brief + verdict at end of capture.)

**TD-CHANGE-IMPACT: CONCERNS** (scoped — none block the design).

**Q1. Where does `D` live? → `BiomeDistributionSO`.**
> ADR-0015 is broader than "enum/archetype narrowing." Its principle is "biome-shaped configuration on the data table; engine-shaped invariants on the generator." `D` is biome-shaped: biome 2 (canyon-tight) and biome 4 (open-flats) will want different values. The fact that it's numeric, not categorical, is irrelevant — `ForwardBiasKeepFloor` is numeric too and it's already an inputs-style knob. Putting `D` on the SO costs nothing now and pre-bakes per-biome flexibility. Constant is the wrong call. POCO `BiomeGenerationInputs` is acceptable but loses the per-biome dial without gain.

**Q2. Constraint semantics → (a), "every pair within D MUST be connected."**
> Cursor-relative (b) forces the generator to know about cursor state, which violates the generator's "pure topology, no run-state" contract. (a) is a clean pairwise invariant: deterministic from beacon positions alone, symmetric under `AllowBidirectional`, and the player's mental model ("if I see two close beacons, they connect") holds from every cursor position, not just Start. Accept the dense-cluster consequence — the user explicitly OK'd single-route bottlenecks through clutter.

**Q3. Post-pruning re-add. With a caveat.**
> Post-pruning is ADR-0011-cleaner — each pass has one job, and `PruneEdges` keeps its forward-bias narrative untouched. CONCERN: post-pruning re-add must run on the **Delaunay candidate set**, not on the full `O(n²)` pairwise sweep. A naive pairwise sweep can re-introduce edges that were never Delaunay-valid (crossing existing edges, creating non-planar topology that the visual layer doesn't expect). Restrict the re-add to `(Delaunay edges) ∩ (lenPx ≤ D)`. Δx==0 ties and self-loops are not at risk — those were placement-stage filters, not pruning filters. If after restricting to the Delaunay set the user still sees visually-close beacons unreached, that's a **placement** problem (Poisson radius too loose vs. `D`), not a pruning problem. Surface that to the user as a tuning relationship: `D ≥ k · PoissonMinRadius` for some k.

**Q4. Strict-forward BFS stays. Max-hop edges adopt forward-only orientation.**
> Loosening `ForwardPathGuaranteeToTerminal` to walk bidirectionally is a much bigger structural change than max-hop — it changes the *meaning* of "guarantee" from "monotonic forward progression exists" to "some walk exists," which collapses the run-progression invariant. Don't touch it. The mitigation for the "visual proximity but no click" residue is the placement-side tuning from Q3 (Poisson radius vs `D`), not BFS loosening. The user said single-route bottlenecks are OK — that's the contract this preserves. If a max-hop pair is `Δx ≤ 0`, drop the backward case; the BFS invariant holds and the player's expectation breaks only in genuinely lateral arrangements, which Poisson tuning should make rare.

**Q5. Block 3 of the generator pivot — framing (i).**
> Same module, same SO surface, generator-internal, ships before Slice 7. The "in-flight pivot alive past scope" risk is real but cheap: declare Block 3 the **closing** block (max-hop is the last generator-feel knob the user surfaces before save/load becomes the priority). Framing (iii) creates a label without a substantive boundary — Block 3 and "Slice 6.5" would touch the same files. Framing (ii) muddies Slice 7's persistence focus; reject.

**Q6. ADR-0015 amendment → no.**
> Algorithm shape (Poisson + Bowyer-Watson + forward-bias prune + forward-path BFS) is unchanged; max-hop is an additional pairwise invariant on the same web. The principle (full enum + SO data table = scope narrowing) is orthogonal. If `D` lands on `BiomeDistributionSO`, the SO grows one field — that's normal table evolution, not an ADR-0015 principle change.

**Q7. No ADR-0011 risk.**
> Numeric constraint on a canonical generator pass, single SO surface, no parallel storage, no stub returns, no transitional code path. Clean.

**Net:** APPROVE the contract shape (SO + (a) + post-pruning re-add on Delaunay-restricted set + strict-forward BFS + Block 3). CONCERNS to surface in the proposal:
1. The Delaunay-restriction on re-add.
2. The placement/D tuning relationship (`D ≥ k · PoissonMinRadius`).
3. Explicit declaration that Block 3 closes the generator pivot.

---

## Deadline / commit shape

- **No hard deadline** — Block 3 is not under ADR-0011 exception #1 (Blocks 1+2 closed that gap by 2026-06-22). Block 3 is normal-priority polish, lands in normal cadence.
- **Single atomic commit** in Unity + capture-only commit in framework (no ADR amendment per Q6).
- Same push policy: Unity to BBerkol/Wasteland-Run, framework local-only until user reconfigures origin.

## We'll know this was right if

- Re-run user's eyeball pass on a fresh map: every visually-close beacon to Start is clickable.
- Block 1 invariants still hold: ≥70% forward-bias edges over 100 seeds; 18-22 beacons; no self-loops/duplicates; min-sep 80 px placement; deterministic.
- EditMode count +5, all green.
- Map gen still produces single-route-through-clutter configurations occasionally (the user explicitly wants this preserved — verify by hand on a few seeds).
- Slice 7 (save/load) lands cleanly with no Block-3-shaped artifact in the persistence surface (max-hop is a generator-internal field, doesn't enter `RunStateDto`).

**Block 3 = closing block of the generator pivot. No Block 4.**

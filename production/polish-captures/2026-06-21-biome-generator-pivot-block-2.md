# Polish Capture: Biome Generator Pivot — Block 2 (NodeMap policy + MapView semantics + ADR-0015 amendment)

**Date:** 2026-06-21
**System:** Run Loop — `NodeMap.Advance` policy, `MapViewController.BuildConnectionViewModels` filter, `BeaconViewModel.IsReachable` rename, ADR-0015 amendment
**Pivot block:** 2 of 2. Closes the doc/code gap opened by Block 1 (Unity `ce3cc5d`, 2026-06-20). **Hard deadline 2026-06-30** per ADR-0011 exception #1 — single atomic commit or Block 1 reverts.

## Framing correction (user 2026-06-21)

The 2026-06-17 brief, Block 1 capture, and pickup memo all carried the phrase "FTL-style strips." That phrase was wrong on two counts:

1. **FTL never used a strip system.** Subset Games' FTL: Faster Than Light places beacon nodes via free-placed clustering with directed forward-only edges — no vertical strips, no fixed-X columns.
2. **The pre-Block-1 generator was the strip system, not an FTL system.** The Slice 6 2026-06-16 generator placed beacons on five fixed-X strips with strip→strip+1 wiring + lateral same-strip rolls. Calling that "FTL-style" mislabeled both the algorithm and the reference.

The Block 1 generator (Poisson-disc placement + Bowyer-Watson Delaunay + forward-bias edge pruning) IS the actual FTL placement model. **Block 2's ADR-0015 amendment drops "strip" wording, KEEPS "FTL-style" as the canonical descriptor.** Production xmldoc + ADR-0015 + test fixtures all converge on the corrected framing in the same commit.

## What's being changed (Block 2 scope)

Four items, single commit, atomic:

### 1. NodeMap policy — `AllowBidirectional` consumer

- `NodeMap.FromBiomeGraph(graph, runSeed, terminalType, **bool allowBidirectional**)` — new 4th parameter.
- `NodeMap._allowBidirectional` private field; stored at construction.
- `NodeMap.Advance(toIndex, reason)` — composes `IsForwardEdge(CurrentIndex, toIndex) || (_allowBidirectional && IsForwardEdge(toIndex, CurrentIndex))`.
- Throw message updated to reflect either-direction edge query under policy.
- `IsForwardEdge` name UNCHANGED (TD Q2) — structural predicate, not policy-aware.

**Call sites updated (6 total):**
- `Assets/Scripts/CombatView/RunSceneHost.cs:220` — pass `inputs.AllowBidirectional`
- `Assets/Tests/EditMode/Run/RunSession_Test.cs:59`
- `Assets/Tests/EditMode/Run/RunSession_Reward_Test.cs:72`
- `Assets/Tests/EditMode/Run/RunSession_CardReward_Test.cs:64`
- `Assets/Tests/EditMode/Run/RunController_HappyPath_Test.cs:74`
- `Assets/Tests/EditMode/CombatView/SceneEncounterBuilder_Test.cs:57`

Test fixtures default to `allowBidirectional: false` (existing forward-only traversal under test); the new backward-edge test passes `true`.

### 2. MapView semantics rename + frontier filter switch

- `WastelandRun.UI.BeaconViewModel.IsReachable` → `IsAdjacentToCurrent` (struct rename; field + ctor param + xmldoc). Property name reflects the new bidirectional reality — under policy, an edge can be reached in either direction.
- `MapViewController.BuildBeaconViewModels` — reachable set now `forward ∪ backward` from current: `reachable = ForwardEdgesFrom(currentIndex) ∪ {from : edges where to == currentIndex}`.
- `MapViewController.BuildConnectionViewModels` — frontier filter switches from `from != currentIndex → continue` to `from != currentIndex && to != currentIndex → continue`. Renders edges where either endpoint is current.
- `MapView.uss .wr-beacon--current` UNCHANGED (already correct — describes the current beacon, not adjacency).

### 3. ADR-0015 amendment (in-commit)

`docs/architecture/adr-0015-biome-distribution-as-configuration-narrowing.md`:

- **Summary:** drop "FTL-style strips"; canonical algorithm is "FTL-style free-placed clustering with Bowyer-Watson Delaunay triangulation and forward-bias edge pruning per `BiomeWebGenerator`."
- **Current State (Context):** rewrite to acknowledge Slice 6 closed; Block 1 (Unity `ce3cc5d`) superseded the original strip placement; the principle (full enum + narrow SO data table) is unchanged.
- **Architecture diagram (Decision):** drop "5 strips" / "gate funnel at strip 5" lines; replace with "1-2 terminal beacons identified post-placement as the rightmost-X cluster."
- **Key Interfaces:** `BiomeWebGenerator.Generate` signature corrected from the speculative `(int biomeIndex, BiomeDistributionSO distribution, System.Random rng)` to the shipped `(BiomeGenerationInputs inputs, int runSeed)`.
- **Validation Criteria:** drop "5 strips" + "gate funnel" wording; align with Block 1 invariants (Bowyer-Watson Delaunay planarity, Poisson-disc min-sep, forward-path BFS guarantee, statistical angle bias ≥70% of edges within 60° of horizontal).
- **Last Verified:** bump to 2026-06-21.
- **One-line forward note** (added to either ADR-0015 Migration Plan or ADR-0004 Migration Plan): "Slice 7 ADR-0004 persistence work must include `AllowBidirectional` in the per-biome DTO surface so save-load round-trip preserves the run's traversal policy."
- **NOT touched:** Alternatives Considered (historical), Risks, GDD Requirements Addressed table, Decision lead-in principle paragraph — all still correct.

### 4. Replacement test for the pickup memo's item #4

**Original item #4 (pickup memo):** "save-load round-trip test verifying `AllowBidirectional` persists across `RunState` save → restore via the ADR-0004 schema."

**Why dropped (TD Q3):** ADR-0004 persistence was deferred to Slice 7. Zero Newtonsoft / `JsonConvert` / `RunStateDto` / `Snapshot` hits in production Unity code. Building a minimal save layer just for this flag would ship half a save system — exactly the half-shipped-system smell per `feedback_overall_picture_thinking.md` — and violate ADR-0011 #1 (transitional adapter that Slice 7 has to rip out).

**Substitute test:** `Assets/Tests/EditMode/Run/NodeMap_AllowBidirectional_Test.cs` (new file):
- `Advance_BackwardEdge_RejectedWhenFlagFalse` — hand-authored 5-beacon graph; `NodeMap.FromBiomeGraph(..., allowBidirectional: false)`; assert `Advance` on a reverse edge throws.
- `Advance_BackwardEdge_AcceptedWhenFlagTrue` — same graph; `allowBidirectional: true`; assert reverse `Advance` returns valid `BeaconTransition`.
- `Advance_ForwardEdge_AcceptedRegardlessOfFlag` — proves the flag doesn't break the forward-only path.
- `FromBiomeGraph_PropagatesFlagToAdvance` — round-trip the flag through factory → inputs → graph → NodeMap; assert behavior matches.

Together with the Block 1 `AllowBidirectional_FlagThreadsThroughInputs` test (which pins the SO → inputs threading), Block 2 closes the threading chain end-to-end: SO → factory → inputs → FromBiomeGraph parameter → NodeMap field → Advance composition.

**Net test delta this commit:** +4 tests (NodeMap_AllowBidirectional_Test), 0 dropped. EditMode target after Block 2: **496 / 495 passed / 0 failed / 1 pre-existing skip**.

### 5. Doc hygiene cleanup (in-commit, required per ADR-0011 #7)

All stale "strip" references in production xmldoc + test xmldoc/comments. "FTL-style" wording survives where the algorithm is actually FTL-style (which is everywhere — Block 1 generator IS FTL).

**Production code (REQUIRED — in this commit):**

| File | What changes |
|------|--------------|
| `Assets/Scripts/Run/BiomeGraph.cs` | Class summary: drop "5 vertical strips, 18-22 beacons, gate-funnel chokepoint at strip 5"; keep "FTL-style free-placed directed graph." Replace strip terminology with "leftmost-cluster Start" / "rightmost-cluster terminal." |
| `Assets/Scripts/Run/BiomeGraph.cs` Edges xmldoc | Drop "Always points strip i → strip i+1 or lateral within a strip"; reframe as "forward-directed edges (positions[from].X < positions[to].X) from Bowyer-Watson Delaunay triangulation." |
| `Assets/Scripts/Run/BiomeGraph.cs` EntryIndex xmldoc | Drop "always strip 0"; replace with "Start beacon, placed first in the leftmost-X cluster per BiomeWebGenerator placement order." |
| `Assets/Scripts/Run/BiomeGraph.cs` ExitIndex xmldoc | Drop "strip 5" references; reframe as "rightmost cluster" anchor; representative-anchor contract unchanged. |
| `Assets/Scripts/Run/NodeMap.cs` class summary | Drop "branching FTL-style graph" sentence's "strip 5" reference. "FTL-style" wording stays. |
| `Assets/Scripts/UI/MapViewModels.cs` BeaconViewModel xmldoc | Drop "Phase E feeds real FTL-grid coordinates" (FTL is not a grid; also Slice 6 closed — Phase E is shipped). Drop "Phase E replaces hardcoded values" — also stale. |

**Test code (REQUIRED):**

| File | What changes |
|------|--------------|
| `Assets/Tests/EditMode/Run/RunSession_Test.cs` xmldoc | Drop "FTL generator's randomised output" → "BiomeWebGenerator's randomised output." |
| `Assets/Tests/EditMode/Run/RunSession_Reward_Test.cs` xmldoc | (same shape) |
| `Assets/Tests/EditMode/Run/RunSession_CardReward_Test.cs` xmldoc | (same shape) |
| `Assets/Tests/EditMode/Run/RunController_HappyPath_Test.cs` xmldoc | (any FTL-grid/strip references) |
| `Assets/Tests/EditMode/CombatView/SceneEncounterBuilder_Test.cs` xmldoc | (any FTL-grid/strip references) |

## What's destroyed (capture-before-destroy receipts)

### NodeMap.FromBiomeGraph signature

Old: `public static NodeMap FromBiomeGraph(BiomeGraph graph, int runSeed, BeaconType terminalType)`
New: `public static NodeMap FromBiomeGraph(BiomeGraph graph, int runSeed, BeaconType terminalType, bool allowBidirectional)`

3-arg signature destroyed; no overload retained (ADR-0011 #5 forbids compat overloads).

### NodeMap.Advance forward-only contract

Old: throws `InvalidOperationException` if `IsForwardEdge(CurrentIndex, toIndex)` is false — single-direction graph traversal.
New: throws only if BOTH forward (`C → T`) AND (under bidirectional policy) reverse (`T → C`) edge queries are false.

The forward-only invariant is destroyed for any NodeMap constructed with `allowBidirectional: true`. Under `false`, behavior is byte-identical to the old contract.

### BeaconViewModel.IsReachable field name

Old: `public bool IsReachable { get; }` — semantically "player may advance here."
New: `public bool IsAdjacentToCurrent { get; }` — semantically "edge exists between this beacon and current, in either direction." Ctor param + xmldoc renamed in lockstep.

Old name does not survive (no compat property — ADR-0011 #5).

### MapViewController frontier filter

Old: `if (from != currentIndex) continue;` — single source = current.
New: `if (from != currentIndex && to != currentIndex) continue;` — either endpoint = current.

### ADR-0015 "FTL-style strips" wording

Surfaces destroyed:
- Summary (~line 24-30): "narrows biome 1 to `{Combat, Haven}` beacon types via `Biome1Distribution.asset` while the full 7-type beacon enum (Combat, EliteCombat, Merchant, Chopshop, Event, Rest, Haven) is real in code" — phrase "First application: the Slice 6 node-map biome-web generator" SURVIVES; "FTL-style strips" framing dropped.
- Context Current State (~line 79-87): references to `NodeMap.SingleCombat` and `BuildLinearMilestone1` (both retired in Slice 6).
- Decision Architecture diagram (~line 156): "MinBeacons, MaxBeacons (18-22 per GDD)" survives; "StripCount (5 per GDD)" + "GateFunnelMaxBeacons (1-2 per GDD)" destroyed (replaced by "rightmost-X cluster terminal count 1-2").
- Decision Key Interfaces (~line 196-218): `BiomeDistributionSO` shape diagram updated to drop `_stripCount` / `_gateFunnelMaxBeacons` (never existed in shipped SO anyway); `BiomeWebGenerator.Generate` speculative signature replaced with shipped one.
- Implementation Guidelines (~line 226-251): bullet "FTL placement constraints" reframed to "Poisson-disc min-sep + Bowyer-Watson Delaunay planarity + forward-path guarantee."
- Validation Criteria (~line 374-385): "5 strips," "gate-funnel topology" references destroyed; replaced with Block 1 invariants.
- GDD Requirements Addressed (~line 397): "C1.1 §1-6: FTL beacon-web generation with 3 biomes, 18-22 beacons each, 5 strips, 80px min-sep, 45° forward cone" — "5 strips" + "45° forward cone" destroyed; replaced with "free-placed clustering, Bowyer-Watson Delaunay, forward-bias edge pruning."

The ADR principle ("scope-narrowing via SO data tables, full enum + canonical generator from day one") survives unchanged.

## Why a single commit (TD Q6)

The deadline mitigation contract from Block 1 (`production/polish-captures/2026-06-19-biome-generator-pivot-block-1.md`) is built around atomic doc/code closure. Splitting Block 2 reopens the doc/code gap window between commits. Items 1-4 have no independent test surface — they have to land as one diff to be testable as a whole. Bisect-isolation argument from Slice 6 Phase F+G does not apply.

## Why a substitute for save-load (TD Q3)

ADR-0004 persistence work was deferred to Slice 7 (active.md 2026-06-17). No `RunStateDto`, no `Snapshot`, no Newtonsoft layer exists in production code today. The 2026-06-17 brief's item #4 assumed sub-commit 3 would land DTOs; the user's deferral reversed that, and the pickup memo carried the assumption forward without re-checking. Building an ad-hoc snapshot for `AllowBidirectional` alone is the half-system smell (per `feedback_overall_picture_thinking.md`) + ADR-0011 #1 (transitional adapter). The substitute test pins the threading invariant today (the actual end-state behavior claim); the persistence test is queued for Slice 7's canonical ADR-0004 DTO surface where it fits without any Block-2-shaped artifact to rip out.

## Sequencing — what to verify before commit

- EditMode batchmode test pass: 496 / 495 / 0 failed / 1 pre-existing skip (`DredgeLayoutTests.ApplyDamage_HullTarget_DrainsArmor0First`).
- `tools/ci/grep-gates.sh` (extend with `strip 5` + `5 vertical strips` + "FTL-grid" patterns under `Assets/Scripts` + `Assets/Tests/EditMode` — should return zero hits post-commit).
- ADR-0015 doc/code gap closed: no `FTL-style strips` / `5 strips` / `gate funnel at strip 5` strings in ADR-0015 OR in any `.cs` file.
- `IsReachable` references absent in `WastelandRun.UI` namespace.
- Unity batchmode flag: `-runTests` only, no `-quit` (per `memory/project_unity_batchmode_no_quit.md`).

## Technical Director Review

Verdict received 2026-06-21 during this session's pre-implementation brief. Pasted verbatim below.

---

# TD-CHANGE-IMPACT: CONCERNS

**Scope:** Workstream A Block 2 of the biome generator pivot — pre-implementation re-verdict before single-commit landing. Deadline 2026-06-30 (9 days). One blocker (Q3 needs scope rewrite from the pickup memo), two redirects on your leans (Q1, Q5), four APPROVE-aligned-with-your-lean (Q2, Q4, Q6, Q7). New issue surfaced as Q8.

Before I go deep: I'm marking CONCERNS rather than APPROVE because the pickup memo's item #4 ("save-load round-trip test") is unbuildable as written and you correctly flagged it. The verdict closes that gap; once item #4 is rewritten per Q3 below, the commit is APPROVE-shaped. **No part of this re-verdict argues for reverting Block 1.**

---

## Q1 — Flag plumb path: **OPTION A (Parameter on FromBiomeGraph). OVERRIDE your lean.**

**Verdict:** Add `bool allowBidirectional` as a parameter on `NodeMap.FromBiomeGraph` and store it as `_allowBidirectional` on NodeMap. Do NOT echo onto `BiomeGraph`.

**Why I'm overriding your read of Option B as "fewer call site touches" being a tiebreaker:**

You correctly named the smell ("policy stuffed onto structural output") and then under-weighted it. ADR-0011 #2 (parallel storage of the same concept) is exactly what Option B creates: the flag would live on both `BiomeGenerationInputs.AllowBidirectional` AND `BiomeGraph.AllowBidirectional` at done state. Whichever one you read becomes a source-of-truth contest. The fact that one is "copied from the other at generator time" doesn't fix it — that's a classic ADR-0011 #1 adapter (BiomeGraph becomes an adapter shape that translates inputs into graph-with-policy).

Beyond ADR-0011: `BiomeGraph` carries a `noEngineReferences: true` POCO contract per ADR-0002, and its sole purpose per the xmldoc is "structural output of the generator." `AllowBidirectional` is **caller policy**, not generator output — the generator's edges are forward-directed in BOTH flag states (you can see this in `BiomeWebGenerator.cs:36` invariant: "Every non-terminal beacon has a forward path... independent of `BiomeGenerationInputs.AllowBidirectional`"). The flag never reaches the generator's edge set. Stuffing it on BiomeGraph misrepresents what BiomeGraph IS.

The "five call sites to update" concern is a non-issue: those five sites are exactly the right places to face the policy choice. The fix is a compiler-enforced visit list, not a maintenance burden. RunSceneHost is the prod site, the four EditMode tests are fixtures — the test fixtures should make the policy choice **explicit** (most will pass `false` for backward-edge tests, the bidirectional ones pass `true`).

**Pattern reference:** ADR-0010's "Single Vocabulary" rule — `slotId` is one string, owned by one storage. The flag is the run policy's vocabulary; it stays on the run-side (RunSceneHost reads inputs.AllowBidirectional; passes to FromBiomeGraph).

**Deadline impact:** None. Same 9 days. Five call sites is a 30-minute touch.

## Q2 — `IsForwardEdge` rename: **APPROVE your lean — keep the name; compose in `Advance`.**

**Verdict:** Keep `IsForwardEdge(from, to)` as-is. Have `Advance(toIndex, reason)` compose: `IsForwardEdge(CurrentIndex, toIndex) || (_allowBidirectional && IsForwardEdge(toIndex, CurrentIndex))`. Do NOT introduce `IsTraversable` or `HasDirectedEdge`.

**Why your option-A is correct here (and the rename in Q5 is NOT inconsistent with this):**

`IsForwardEdge` is **structurally** accurate — it asks "does the directed edge (from → to) exist in the graph?" The generator always emits forward-directed edges (BiomeWebGenerator invariant), so the name describes the storage shape, not the traversal policy. Renaming it to `HasDirectedEdge` is a synonym swap; you'd then need a *third* method (`IsTraversable`) to capture the policy-aware question, which is two methods doing one job split awkwardly.

`Advance`'s job is exactly "apply the run policy on top of the structural query." That's where the OR belongs. Composition at the policy seam keeps `IsForwardEdge` as a pure structural predicate (testable in isolation, no policy state) and concentrates the policy logic in one place.

Contrast with Q5/MapView: `IsReachable → IsAdjacentToCurrent` IS the right rename because the name made a false claim about traversal — under bidirectional policy, an edge can be `IsAdjacentToCurrent` and still not "reachable" by the old single-frontier semantic. `IsForwardEdge` makes no false claim — the edge IS still forward-directed; the policy decides whether you can walk it backward.

**Deadline impact:** None.

## Q3 — Save-load test scope: **OPTION α + docs hook into Slice 7. APPROVE your lean. This is the blocker on the pickup memo.**

**Verdict:** Drop item #4 ("save-load round-trip") from Block 2 scope as written. Replace with the flag-threading invariant test you described. Add a one-line forward note in ADR-0015 (or ADR-0004's Migration Plan) that AllowBidirectional persistence joins ADR-0004's per-system DTO surface in Slice 7. The pickup memo is wrong on this item, and you caught it correctly.

**Why the pickup memo's item #4 was wrong (this is the doc/code drift between 2026-06-17 and 2026-06-21):**

The 2026-06-16 brief assumed `RunStateDTO` would land in sub-commit 3 with a SchemaVersion bump. That assumption was reversed when the user deferred persistence to Slice 7 — and the active.md 2026-06-17 entry recorded the deferral correctly. The pickup memo (presumably authored while shaping Block 2 from the brief without revisiting the active.md deferral) reverted to the pre-deferral assumption. **Today's state in the Unity project confirms the deferral held**: zero hits on Newtonsoft, JsonConvert, RunStateDTO, RunStateDto, Snapshot, SaveLoad, Persistence in production code. `RunState.cs` is in-memory POCO only — no DTO seam to test against.

**Why Option β is wrong (don't build a half save system):**

Building an ad-hoc `RunStateSnapshot` for AllowBidirectional alone is exactly the "half-shipped systems that re-break" pattern your memory `feedback_overall_picture_thinking.md` warns about. It would also violate ADR-0011 #1 (the snapshot would be an adapter that exists only because a future system isn't there yet) and ADR-0011 #7 (every TODO marker on "expand to full RunState in Slice 7" is a transitional comment). When Slice 7 lands the canonical persistence shape, this ad-hoc snapshot gets ripped out — that's the bridge.

**Why Option γ (deterministic-rebuild test) is also wrong, but more subtly:**

It looks ADR-0011-clean because it doesn't ship persistence code. But it tests a property that's already covered: ADR-0003's deterministic-RNG discipline + the existing Block 1 test surface already proves that `BiomeGenerationInputsFactory.From(SO)` is deterministic and `BiomeWebGenerator.Generate(inputs, seed)` is deterministic. A "rebuild-from-SO" test would re-test ADR-0003's guarantee. The actual end-state claim that needs testing is "flag value reaches Advance" — which IS the threading test.

**Option α is ADR-0011-clean because it correctly applies the lagging-dependency-flag pattern (`feedback_data_flag_lagging_dependency.md`) at the test layer too**: the flag's value is end-state today (true in Biome1Distribution.asset), the threading invariant is testable today, and the persistence test is a Slice 7 deliverable that fits cleanly into the canonical ADR-0004 per-system DTO + SchemaVersion surface when it lands. **No code shape is promised by Block 2 that Slice 7 then has to rip out.** That's the test of "is this a bridge."

**Deadline impact:** None.

## Q4 — ADR-0015 amendment surface: **APPROVE medium-scope.**

(Full text per TD verdict — verbatim above in this session log; amendment surface enumerated in Block 2 scope item #3.)

## Q5 — Stale doc refs outside ADR-0015: **APPROVE in-scope and required. ALL of them, including the test-file comments.**

ADR-0011 #7 forbids transitional comments at done state. Half-correct is worse than uncorrected — it hides the gap. All listed in Block 2 scope item #5.

## Q6 — Single commit vs split: **APPROVE single commit.**

Block 2's four items all touch the same test pass. Slice 6 Phase F+G split was justified by independent test surfaces; Block 2 has none. Splitting reopens the doc/code gap window.

## Q7 — Schedule + carryover risk: **APPROVE with watch flag.**

9 days comfortable. Watch flag: `InternalsVisibleTo` on test asmdef may shake on `FromBiomeGraph` signature change — verify on first compile. Likely covered per Phase F+G capture.

## Q8 — NEW: BeaconType enum cardinality drift check

ADR-0015 Validation Criteria item 2 requires `BeaconType` enum at 8 values. Quick grep-confirm before locking Block 2 scope. **Confirmed pre-commit: `Assets/Scripts/Run/BeaconType.cs` lines 22-29 ship all 8 values (Start, Combat, EliteCombat, Merchant, Chopshop, Event, Rest, Haven). Q8 clean.**

---

## Block 1 revert assessment

**No red flags suggesting Block 1 should be reverted.** Block 1 is structurally clean: generator-only refactor, ADR-0011-#1-exception-flagged in xmldoc, deterministic per ADR-0003, POCO per ADR-0002. The doc/code gap (FTL-style strips language) is the explicit one-shot-migration exception that the deadline mitigation closes. The pickup memo's item #4 mistake doesn't reflect on Block 1 — it reflects on the pickup memo's reading of the 2026-06-16 brief. Block 2 with Q3's correction lands cleanly.

## Deadline mitigation contract — does this re-verdict shift it?

The original deadline contract was: "Single commit by 2026-06-30, ADR-0011-clean." The four items in the pickup memo formed the contract's substance.

This re-verdict **keeps the contract intact with one substitution**: item #4 changes from "save-load round-trip test" to "flag-threading invariant test + Slice 7 docs hook." Items #1, #2, #3 stand. The "single commit, by 2026-06-30, ADR-0011-clean" frame survives.

## We'll know this verdict was right if

- Block 2 lands as a single commit by 2026-06-30
- EditMode test count goes up by 4 (NodeMap_AllowBidirectional_Test); all pre-existing tests stay green
- No `strip` / `strip 5` / `5 vertical strips` / `FTL-grid` strings remain in production code, test code, or ADR-0015 after the commit (note: "FTL-style" without strips is kept — it's the correct algorithm descriptor)
- The `AllowBidirectional` flag's value reaches `Advance` end-to-end and the threading test pins that invariant
- Slice 7 lands ADR-0004 with `AllowBidirectional` falling naturally into the per-system DTO surface — no Block-2-shaped artifact to rip out

---

**ADRs invoked across Block 2:** 0002 (POCO contract), 0003 (deterministic RNG), 0004 (deferred persistence — Slice 7 hook), 0011 (no bridges + exception #1, #2 parallel storage, #5 compat overloads, #7 transitional comments), 0014 (asmdef arrow, `InternalsVisibleTo`), 0015 (the ADR being amended).

**User framing correction (2026-06-21):** "FTL-style" wording stays. "Strip" wording goes. The Block 1 generator IS the FTL placement system; the prior strip generator was the impostor.

---

## Closeout cleanup additions (2026-06-22)

Mid-sweep on 2026-06-22 (closeout day) four additional in-spirit cleanups surfaced. Each is artifact directly created by Block 1 and supposed to be erased by Block 2; consolidating them into the atomic commit closes the doc/code gap completely. TD verdict captured in `production/td-verdicts/2026-06-22-block-2-cleanup-additions.md` — **APPROVE all four** with one caveat on xmldoc honesty (applied).

### A. `ConnectionViewModel.IsReachable → IsAdjacentToCurrent` rename

Block 2's spec called out `BeaconViewModel.IsReachable → IsAdjacentToCurrent`. The sibling struct `ConnectionViewModel` carried the same `IsReachable` field; leaving it would mean the BeaconVM rename completed but the parallel ConnectionVM rename did not — the "vestigial parallel storage" smell ADR-0011 #2 forbids.

- `Assets/Scripts/UI/MapViewModels.cs`: `ConnectionViewModel.IsReachable` (field + ctor param + xmldoc) → `IsAdjacentToCurrent`
- TD caveat applied: xmldoc rewritten to say "Computed at emit time; currently always `true` because the view only emits edges that have the current cursor as an endpoint" instead of misleading "True when this connection has the current cursor as one of its endpoints." Future reader sees the shape (typed flag kept for forward extensibility) without trusting the value as a runtime predicate.

### B. `BiomeWebGenerator.cs` ADR-0015-exception xmldoc disclaimer removal

Block 1's commit landed lines 57-61 disclaimer in `BiomeWebGenerator.cs` class doc: "ADR-0015: this Block 1 commit lands non-strip code while ADR-0015's summary still references 'FTL-style strips'... gap is not a forbidden bridge for the in-flight window." Block 2 IS the gap closure. Leaving the disclaimer in place after Block 2 lands would be the "transitional comment" anti-pattern ADR-0011 #7 forbids at done state.

- `Assets/Scripts/Run/BiomeWebGenerator.cs:7-62`: removed disclaimer paragraph; rewrote leading algorithm paragraph from "Algorithm (Block 1 of the 2026-06-19 generator pivot; supersedes the FTL-strip layout shipped 2026-06-16)" to "Algorithm — free-placed clustered Delaunay web with forward bias (ADR-0015)". Reader of a 2027 codebase sees the canonical algorithm description with no trace of the exception window.

### C. Test method rename + assertion message updates

The terminal-cluster vocabulary was "gate funnel" in the pre-Block-1 generator. ADR-0015's amendment dropped "gate funnel" / "strip 5" — but the test method name + assertion strings still carried the retired vocabulary.

- `Assets/Tests/EditMode/Run/BiomeWebGenerator_Test.cs`:
  - Method rename: `Generate_HasGateFunnelOf1Or2TerminalBeacons` → `Generate_TerminalClusterHas1Or2TerminalBeacons`
  - Assertion messages: "Gate-funnel must have at least 1..." → "Terminal cluster must have at least 1..."
  - Section header + xmldoc rewritten — dropped "Gate funnel" and "Strip-5 wording dropped" notes.

### D. `RunSceneHost.cs:34` Phase E breadcrumb removal

The xmldoc paragraph "Slice 6 Phase E retires the linear `NodeMap.SingleCombat` sugar and the host-local `_currentBeaconIndex` cursor..." was a stale breadcrumb referencing the old multi-phase Slice 6 plan that ADR-0015 collapsed. Information value to a fresh reader was zero (the sugar IS retired; the comment doesn't tell you anything the code doesn't).

- `Assets/Scripts/CombatView/RunSceneHost.cs`: dropped the "Slice 6 Phase E retires..." paragraph; rewrote the surrounding xmldoc to describe the current `BeginNewRun` flow (read SO → unpack inputs → generate graph → hand to `RunController.StartRun`) without phase-history breadcrumbs.

### E. Sweep targets verified clean (no edits required)

Verification greps across `Assets/` confirmed zero live references to: `FTL-grid` / `FTL grid` / `IsReachable` / `GateFunnel` / `gate.funnel` / `gate funnel` / `strip 5` / `5 vertical strips`. Four `Phase E` references remain in xmldoc historical breadcrumbs (`RunSceneHost_Test.cs:23`, `BeaconNodeElement.cs:105`, `RunController.cs:10`, `RunSceneHost.cs:15`) — the grep-gates doc-comment filter tolerates them; live code is clean.

### F. `tools/ci/grep-gates.sh` extension

Per TD: locked the closeout vocabulary in CI so it cannot leak back.

- `strip 5`, `5 vertical strips`, `FTL-grid` — retired FTL-strip vocabulary
- `GateFunnel`, `gate.funnel`, `gate funnel` — retired terminal-cluster name
- `IsReachable` (scoped to `Assets/Scripts/UI` + `Assets/Tests`) — renamed both on `BeaconViewModel` and `ConnectionViewModel`
- `Phase E` (scoped to `Assets/Scripts` + `Assets/Tests`) — retired multi-phase label; existing xmldoc refs filtered by the doc-comment filter

Local `bash tools/ci/grep-gates.sh` run: **all gates clean**.

### Files added to the Block 2 atomic commit (delta vs. original 4-item spec)

| File | Block 2 spec | + Closeout additions |
|------|--------------|----------------------|
| `Assets/Scripts/Run/NodeMap.cs` | Y | — |
| `Assets/Scripts/Run/BiomeWebGenerator.cs` | — | Y (xmldoc) |
| `Assets/Scripts/UI/MapViewModels.cs` | Y (BeaconVM) | Y (ConnectionVM) |
| `Assets/Scripts/UI/MapViewController.cs` | Y | — |
| `Assets/Scripts/CombatView/RunSceneHost.cs` | Y (pass flag) | Y (xmldoc) |
| `Assets/Tests/EditMode/Run/NodeMap_AllowBidirectional_Test.cs` | Y (new) | — |
| `Assets/Tests/EditMode/Run/BiomeWebGenerator_Test.cs` | — | Y (rename) |
| `Assets/UI/MapView.uxml` | — | Y (stale comment) |
| `Assets/Scripts/Run/Authoring/BiomeDistributionSO.cs` | — | Y (tooltips) |
| `tools/ci/grep-gates.sh` | — | Y (7 new gates) |
| `docs/architecture/adr-0015-...md` (framework repo) | Y | — |

Plus the 5 existing test call sites updated for the `NodeMap.FromBiomeGraph` 4-arg signature (already in spec).

# Capture — Guaranteed Spaced Chopshop Seeding (Biome 1)

**Date:** 2026-08-27
**System:** `BiomeWebGenerator` beacon-type assignment + `BiomeDistributionSO`
**Epic:** Phase 2.5 Parts Axis · prerequisite for garage re-cut slice 4
**Status:** AWAITING USER APPROVAL — no edits made

---

## 1. Why — measured, not estimated

Parts drop on **every** combat victory (`RunSession.ExitCombat` latches a
`PendingPartOffer` alongside the card offer, `RunSession.cs:801-802`). Equipping
is **Chopshop-only** — a locked user decision (memory
`project-garage-chopshop-only`). So the spend rhythm is entirely governed by how
often a run meets a Chopshop.

I BFS'd a real generated map out of a played `runstate.sav` rather than
reasoning from the weights:

| Metric | Measured |
|---|---|
| Map size | 59 nodes, 54 on a viable Start→terminal path |
| **Average run length** | **17.1 beacons** (20 000 random forward walks) |
| Path length range | 10 shortest, 32 longest |
| On-path types | Combat 25, Event 13, Merchant 6, Rest 6, **Chopshop 2** |
| **Chopshop x-positions** | **0.69 and 0.73** — one cluster at ~70% depth |
| **Expected Chopshops visited/run** | **0.49** |

**Roughly half of all runs contain zero Chopshops.** A system that drops ~8
parts per run gives the player nowhere to spend them in half of all runs, and
when it does, both opportunities sit within four percent of each other at the
back of the map.

### User design decision (LOCKED)

**2–3 Chopshops per run, SPREAD across the run.** Explicitly chosen over
"clustered later for one bigger spend." Intent: parts feel incremental, spent in
stages.

---

## 2. The finding that reshaped the implementation

My first proposal was "seed 3 Chopshops at even x-intervals." **The TD rejected
that as arithmetically insufficient, and I verified the rejection empirically
before accepting it.**

With 54 on-path nodes and a 17.1-beacon walk, the average on-path node is
visited ~32% of the time. Seeding three at arbitrary band positions gives:

| Selection | Expected visits/run (random walk) |
|---|---|
| Today (weight 5, no guarantee) | 0.49 |
| Arbitrary node per band | **0.76** |
| `argmax(ancestorCount)` per band | **0.91** |

Forced placement alone moves 0.49 → ~0.9. **Still under one.** It would have
shipped, passed generator tests, and missed the design goal.

### Why it still works: the player steers

Only **two** nodes are mandatory on every path — index 1 (LeftFunnel) and index
53 (RightFunnel). I confirmed this by cut-vertex analysis. Everything else is
optional, so no interior seeding can be *structurally* guaranteed.

But the map is fully visible and per-type styled (`MapBeaconStyleSO`), so a
player who wants to spend parts routes toward a shop. Modelling that properly:

| Model | Chopshops visited | Path length |
|---|---|---|
| Random walker | 0.91 of 3 | 17.1 |
| **Deliberate router** | **3 of 3** | **15.0** |

The routed path is **shorter than the random-walk baseline** (15 vs 17.1). The
three seeded nodes are mutually forward-chainable (32 → 8 → 33), so visiting all
three costs **no** extra fuel and **no** extra storm pressure. There is no
detour tax to design around.

A player who ignores parts gets ~1 Chopshop. A player who engages gets 3. That
is a player choice, which is the correct shape.

> **Process note, recorded deliberately.** My first steering simulation reported
> 1.00 of 3 and I relayed that number before catching the bug — it took the
> first available edge instead of routing toward a goal. The corrected model
> gives 3 of 3. The erroneous figure was surfaced to the user and corrected in
> the same session. Simulation code is not evidence until its negative result is
> explained.

### Why routability selection is the right mechanism

`ancestorCount[v]` = number of beacons with a forward path to `v`. Selecting
`argmax` per band maximises the fraction of the upstream frontier that can steer
to the shop **without backtracking**. Observed range on the sample map: 3 to 56
ancestors across 50 candidates — a wide, meaningful spread, not noise.

It is also **RNG-free**, which makes the test a clean assertion rather than a
statistical one, and adds no salt to the ADR-0003 catalogue.

---

## 3. What is destroyed

### 3a. Authored value destroyed: exactly ONE

`Biome1Distribution.asset`, `_nonTerminalBeaconTypes`:

```yaml
  - Type: 4      # Chopshop  ← DELETED
    Weight: 5
```

**Provenance matters here:** this weight was never playtested as a deliberate
tuning. It entered as a placeholder alongside the Phase 2.5 parts axis, and it
is the direct cause of the measured 0.49. Deleting it is not overriding a
designer judgement — it is removing an unexamined default.

**Deleted, not zeroed.** `BiomeGenerationInputsFactory.From` strips `Weight <= 0`
entries at the SO→POCO boundary (`BiomeGenerationInputsFactory.cs:36`), so a
`Weight: 0` row is dead data the runtime never sees — a vestigial authored row,
ADR-0011 #6 in asset form.

**Consequence:** Chopshop count becomes exactly deterministic at the authored
value. Keeping weight 5 on top of a guarantee would yield ~5.4 per run against a
target of 2–3 — variance stacked on a floor.

Surviving weights total **exactly 100**: Combat 45 / Event 25 / Merchant 15 /
Rest 15. Every future weight conversation becomes a percentage conversation.

### 3b. Authored value added: one

```yaml
  _guaranteedChopshopCount: 3
```

### 3c. Explicitly UNCHANGED (verified against the asset)

`_targetBeaconCount: 55`, `_spurCount: 4`, `_clusterCount: 2`,
`_stormCounterStart: 12`, `_stormAdvancePerStrip: 0.12`,
`_initialStormCursorX: -0.05`, all 9 `_beaconFuelCosts`, `_reconnectRadius: 450`,
`_maxEdgeLength: 550`, `_globalMinSeparation: 90`, `_canvasWidthPx: 3840`,
`_canvasHeightPx: 1080`, `_havenFuelRefillPercent: 0.65`,
`_biomeStartingFuelModifier: 1`, `_bossEncounter`, `_combatArchetypes`,
`_mapTheme`.

**No storm retune.** Mean fuel per hop moves 4.95 → 5.00 (three nodes forced to
cost 4 instead of weighted sampling). Under 1% on a 17-beacon walk. The storm
counter was settled at 12 / 0.12 by playtest on 2026-08-04 and stays.

---

## 4. Two hazards found during review

### 4a. Spur placement — CONFIRMED REAL

`AddSpurs` runs at `BiomeWebGenerator.cs:359`, appending spur indices at
`[oldLen, oldLen+k)`. `AssignBeaconTypes` runs **after**, at line 362, on the
grown array. Spur indices are not `0`, not `1`, not in `terminalSet` — so they
fall through to the weighted branch at `:1573`. **Spurs are typed from the
normal pool today**, and an unguarded seed would land a guaranteed Chopshop on a
dead-end side branch ~7% of the time with `_spurCount: 4`.

Guard, no signature change: `TryPlaceUniformPoisson` fills exactly
`inputs.TargetBeaconCount` slots (`:514`), so that value is the pre-spur count
and is already in scope. Candidate range `[2, TargetBeaconCount - 3]` excludes
Start, LeftFunnel, RightFunnel, Terminal and every spur.

### 4b. Adjacency ban is backward-only — LATENT BUG

Chopshop is already in `AdjacencyBannedTypes` (`:1451`), but `HasSameTypeNeighbour`
(`:1627`) only inspects lower-index *assigned* neighbours. A forced Chopshop at
index 40 would not prevent a weighted Chopshop at index 12 that neighbours it,
because the forced type bypasses reroll.

**LeftFunnel never exposed this** because Combat is not in `AdjacencyBannedTypes`.
Forcing a *banned* type is a new situation the code does not handle.

Fix is one line — pre-seed `bannedByType[BeaconType.Chopshop]` with the forced
indices before the assignment loop, making the ban bidirectional. **Omitting it
reintroduces exactly the clustering this change exists to fix.**

---

## 5. Proposed changeset

1. **`BiomeDistributionSO.cs`** — `_guaranteedChopshopCount = 3` + accessor +
   OnValidate clamp `[0, 6]`, modelled on the `_clusterCount` block (`:597-614`).
2. **`BiomeGenerationInputs.cs`** — append `int GuaranteedChopshopCount = 0`
   **last** in the positional record. **Default 0 is load-bearing** — it is the
   kill-switch pattern shared by `SpurCount` / `ClusterCount`, and it is what
   keeps every existing generator fixture green (see §6).
3. **`BiomeGenerationInputsFactory.cs`** — one appended argument.
4. **`BiomeWebGenerator.cs`** — `MaxGuaranteedChopshops = 6`; move the adjacency
   table build (`:1511-1517`) above the selector so it is shared; add
   `SelectGuaranteedChopshops` (predecessor table → per-candidate reverse BFS →
   per-band `argmax(ancestorCount)`, lowest-index tie-break, excluding candidates
   adjacent to an already-selected Chopshop); add the forced branch to the
   assignment loop; pre-seed `bannedByType[Chopshop]`.
5. **`Biome1Distribution.asset`** — the two-value delta in §3.
6. **Tests** — two new fixtures; fix two stale references (§6).

**Bands are DERIVED, not authored.** `XMin = 0.18`, `XMax = 0.82` (`:141-142`),
span 0.64, K=3 → centres **0.287 / 0.500 / 0.713**. At 17.1 beacons that is
~5.8 beacons between stops — the "every 5–6 beacons / every 2–3 combats" rhythm
requested. Authoring both a count and a positions array would encode the same
fact twice (ADR-0011 #2) and require OnValidate machinery to police a
redundancy we chose to create.

**Single `int`, not a range.** "2–3" reads as designer uncertainty about the
number, not a request for per-run variance. A rhythm wants consistency.
Reversibility cost accepted explicitly: `int` → `Vector2Int` on one field with a
capture, if playtest asks for it.

**Empty-band policy: skip, place fewer, do not fail the attempt** — mirroring
`AddSpurs` (`:1230-1234`). This is type assignment, not a graph invariant;
coupling it to the geometry retry loop could burn all `MaxRetries` on a
pathological canvas. New test #1 is the guard against silent degradation.

**No ADR.** This is ADR-0015 applied unchanged — a per-biome data table
narrowing content, no new architectural principle.

---

## 6. Blast radius

**Zero red tests** if `GuaranteedChopshopCount` defaults to 0 at the record
boundary — `BiomeWebGenerator_Test.BuildInputs` (`:56`) never passes it, so the
pass stays disabled for every existing fixture.

Verified green: `Generate_TypeAssignmentRespectsDistributionWeights` (`:436`),
`Generate_BeaconCountEqualsTargetOnSuccess` (`:415`), `RestBeacons_NoTwoAdjacent_*`
(`:1221`), `MerchantBeacons_NoTwoAdjacent_*` (`:1275`), all `Funnel_*` /
`Spurs_*` / `Clusters_*` / `ReconnectRadius_*` / `MaxEdgeLength_*`, all four
`BiomeDistributionSO_Test`, and every `RunSceneHost_*` / `SaveBootstrap_Test` /
`FuelState_Test` / `RunSession_*` (they touch `BeaconFuelCosts`, unchanged).

**Two stale references to fix:**
- `Biome1Distribution_AssetSanity_Test.cs:43-45` — the test passes, but its
  comment predicts *"Chopshop lands with the parts axis; target totals then:
  Combat 30 / Merchant 15 / Chopshop 10 / Event 25 / Rest 20."* That prediction
  is now wrong; leaving it invites someone to re-add the weight.
- `StormPacingTuner_Test.cs:544-550` — hand-mirrors the SO including
  `(BeaconType.Chopshop, 5)`. Already known to drift (memory
  `project-storm-pacing-harness-gaps`); leaving it makes it lie harder.

**Two new tests:**
1. `GuaranteedChopshops_ThreeSeeded_NoneOnSpur_AcrossFiveSeeds` — on the
   shipping asset, assert exactly 3 and every index `< TargetBeaconCount`.
2. `GuaranteedChopshops_ZeroDisablesThePass` — mirrors
   `Spurs_ZeroDisablesThePass` (`:1519`).

**`MaxRetries`: no risk.** The selector runs at `:362`, after every retry-gated
check. It cannot trigger a retry.

**Save compatibility: none needed.** `NodeMapDto` persists the full beacon graph
rather than regenerating from `RunSeed` (`NodeMapDto.cs:41`), whose own doc
comment cites this exact scenario. In-flight saves unaffected, `SCHEMA_VERSION`
stays 1.

**Per memory `feedback-prove-test-fails-on-the-bug`:** new test #1 must be
validated by reversion — set the count to 0, confirm it fails, restore.

---

## 7. Verification gate

- EditMode suite green. Baseline: **1154 / 1152 / 0 failed / 2 skipped**, 0
  `error CS`, at Unity `5483f76`.
- Per-test diff against that baseline; only the two new tests should appear.
- Re-run the map analysis on a freshly generated map and confirm 3 Chopshops,
  spread, none on a spur.

**Open item flagged by the TD, outside its read:** confirm `BeaconSceneBinding`
for Chopshop is in PrefabRoot mode (`_prefabRoots` entry **and** the Mode 0→1
flip — memory `feedback-prefabroot-binding-flip`). Going from ~2.4 expected to a
guaranteed 3 raises the exposure of a missing flip from "sometimes" to "every
run."

---

## Technical Director Review

> Verdict from the `technical-director` agent, 2026-08-27. Reproduced verbatim
> in the portions bearing on this change.

**`TD-BIOME-CHOPSHOP-SEEDING: CONCERNS`**

The proposed direction is sound but, as briefed, **it will not achieve the
design goal**. Forced placement of 3 Chopshops on a 55-node web yields roughly
0.9–1.0 expected Chopshops on a given walk — barely better than today's 0.49,
and still ~35–40% of runs at zero. The fix is one extra constraint in the
selection step, detailed under item 1. Everything else in the brief holds.

### 1. Forced placement vs. weight-plus-minimum-count

**VERDICT: Forced placement — but per-band selection MUST be
routability-constrained, not random. A plain "pick a random node in each band"
shape fails the design goal arithmetically.**

Weight-plus-minimum-count is the wrong tool. There is no post-hoc "top up to N"
hook: `AssignBeaconTypes` (`BiomeWebGenerator.cs:1489`) is single-pass, assigns
in index order, and the adjacency-ban bookkeeping (`bannedByType`, `:1523`) is
built incrementally as it goes. A minimum-count top-up pass would have to
re-enter that state after the fact — a second writer over "what type is beacon
i," which is precisely ADR-0011's bimodal-path smell. Forced placement is also
the existing precedent: LeftFunnel bypasses the weighted pool entirely at
`:1537-1556`.

But LeftFunnel is a **degenerate case that hides the real problem**.
`EnforceFunnelInvariant` (`:1025`) strips every Start out-edge except `(0 → 1)`,
so index 1 has reach-probability exactly 1.0. Every run passes through it. That
guarantee does not generalize to an interior node.

Your own measurement proves it: 54 on-path nodes, 17.1 average walk length → the
average on-path node is visited ~32% of the time. Seeding 3 Chopshops at random
band positions gives an expected **0.95 visited per run**. The change would
ship, look correct in the generator tests, and move the player-facing number
from 0.49 to ~0.95. Still under one.

The two structurally guaranteed slots are index 1 (LeftFunnel, locked Combat)
and index N-2 (RightFunnel — every forward path traverses it, since
`EnforceFunnelInvariant` leaves it as Terminal's only in-edge). Everything else
is probabilistic.

The mechanism that closes the gap is that **the player is not doing a random
walk** — the map is fully visible and typed (`MapBeaconStyleSO` per-type
sprites), so a player who wants to spend parts routes toward a Chopshop. The
technical requirement is therefore not "high modal-path probability" but **high
ancestor coverage**: the seeded node must be forward-reachable from as much of
the upstream frontier as possible, so a player at that depth can steer to it
from wherever they are without a backtrack.

That is a cheap, exact, RNG-free metric:

> `ancestorCount[v]` = number of beacons with a forward path to `v`, computed by
> one reverse-BFS per candidate over a predecessor table.

Cost: ~51 candidates × (V+E ≈ 205) ≈ 10k edge visits, once per successful
generation, against a Delaunay pass that already burns 3,000+ circumcircle tests
per beacon insertion. Free.

Selecting `argmax(ancestorCount)` per band, tie-break lowest index, also
eliminates the RNG surface entirely — no new salt, no seeded draw, and a test
contract that is a clean assertion rather than a statistical one.

**Concrete action.** Forced placement, one Chopshop per x-band, band winner =
`argmax(ancestorCount)` with lowest-index tie-break, excluding any candidate
graph-adjacent to an already-selected Chopshop.

### 2. Where it lives

**VERDICT: Inside `AssignBeaconTypes`, as a selection pre-pass feeding the
existing assignment loop. NOT a post-generation pass. The spur hazard you
flagged is real and confirmed.**

Three reasons it must be internal to `AssignBeaconTypes`:

1. **Single writer.** `BeaconData` is constructed once per index at `:1620`. A
   post-pass would have to replace already-built records — two places deciding
   beacon type, ADR-0011 #3.
2. **The adjacency ban would go stale.** Chopshop is already in
   `AdjacencyBannedTypes` (`:1451`). The ban only looks *backward* at
   lower-index assigned neighbours (`HasSameTypeNeighbour`, `:1627`). A forced
   Chopshop at index 40 would not prevent a weighted-rolled Chopshop at index 12
   that neighbours it — the forced type bypasses reroll, so the invariant
   silently breaks. Fix is one line: **pre-register the forced indices into
   `bannedByType[BeaconType.Chopshop]` before the assignment loop starts.** That
   makes the ban bidirectional and hard, at zero cost.
3. Positions and edges — everything selection needs — are already parameters of
   `AssignBeaconTypes` (`:1489-1494`). No signature change to `Generate`.

**Spur hazard — CONFIRMED REAL.** `AddSpurs` runs at `:359`, appending spur
positions at indices `[oldLen, oldLen+k)` and returning a grown array.
`AssignBeaconTypes` is called *after*, at line 362, on the grown array. Spur
indices are neither `0`, nor `1`, nor in `terminalSet`, so they fall through to
the weighted `else` branch at `:1573` — **spurs are typed from the normal pool
today, and a guaranteed Chopshop seeded without a guard would land on a dead-end
side branch.** With `_spurCount: 4` on 51 intermediates that is a ~7% chance per
seeded Chopshop of being silently off the main path.

The guard is available without a signature change: `TryPlaceUniformPoisson`
fills exactly `inputs.TargetBeaconCount` slots (`:514`), so
**`inputs.TargetBeaconCount` is the pre-spur count** and is already in scope.
Candidate range is `[2, inputs.TargetBeaconCount - 3]` — excludes Start,
LeftFunnel, RightFunnel, Terminal, and every spur.

**Cluster interaction.** `ApplyClusterAttraction` runs at line 294, well before
typing, so bands are computed on final post-pull positions. This matters: with
`_clusterCount: 2`, anchors land at x = 0.22 and 0.78 (`:1122-1130`) with
`ClusterPullRadius = 0.20`, which actively drains the inner edges of the middle
band toward the outer bands. Band 2 ([0.393, 0.607)) retains only its central
~0.16-wide slice — still ~13 intermediates at Biome 1 density, so not starved,
but the mechanism is real and the guard below covers the pathological case.

### 3. Authored vs. internal

**VERDICT: Count is authored on `BiomeDistributionSO`. Band positions are
DERIVED from the count. Do not author positions.**

The count is content — per-biome, designer-owned, and `technical-preferences.md`
forbids hardcoded gameplay values. It is also exactly parallel to `_spurCount` /
`_clusterCount` (`BiomeDistributionSO.cs:238, 249`).

Positions must be derived, for three reasons:

1. **Count and a positions array encode the same fact twice** — a designer can
   author `count: 3` with a 2-entry positions array. That is ADR-0011 #2, and
   policing it needs OnValidate machinery that exists only to defend a
   redundancy you chose to create.
2. **Equal partition of the intermediate X-band already lands on your target.**
   `XMin = 0.18`, `XMax = 0.82` (`:141-142`), span 0.64. K=3 gives band centers
   **0.287 / 0.500 / 0.713** — against your proposed 0.30 / 0.55 / 0.80. The
   third differs because 0.80 is inside the funnel reserve anyway. At 17.1
   beacons across the span, 0.213 normalized ≈ **5.8 beacons between stops** —
   exactly the rhythm you specified. No new constants needed.
3. The freeze memo's "prefer internal behaviour over new knobs" resolves cleanly
   on this split: the *count* is content (authored), the *band geometry* is
   behaviour (internal). Same line the Rest adjacency ban drew.

**Single int, not a min/max range.** "2–3" reads as designer uncertainty about
the number, not a request for per-run variance. A rhythm wants consistency;
randomising between 2 and 3 shop stops undermines the staged-spending intent. If
playtest later wants variance, the extension is `int` → `Vector2Int` on one
authored field with a capture. I accept that reversibility cost explicitly.

### 4. The existing Chopshop weight of 5

**VERDICT: Delete the entry from `_nonTerminalBeaconTypes` entirely. Do not zero
it, do not keep it.**

Keeping weight 5 contradicts the locked design outright. With 51 weighted-sampled
intermediates, weight 5 of 105 yields ~2.4 additional Chopshops on top of 3
guaranteed — **~5.4 per run against a stated target of 2–3.** Your own note is
correct: a guarantee plus residual weight is variance stacked on a floor, which
is the worst of both.

Zeroing rather than deleting is the wrong half-measure.
`BiomeGenerationInputsFactory.From` already strips `Weight <= 0` entries at the
SO→POCO boundary (`:36`), so a `Weight: 0` entry is dead data the runtime never
sees — a vestigial authored row, ADR-0011 #6 in asset form. Delete it.

Consequence worth naming: Chopshop count becomes **exactly deterministic** at
the authored value. Given the design intent is a reliable spend rhythm, that is
the correct property, not a loss.

Secondary benefit: remaining weights total **exactly 100**.

Note on spurs: with the weight gone, Chopshops can no longer appear on spurs at
all, since the guaranteed selector excludes spur indices by construction. Clean
outcome — no "pay fuel to detour to the shop" edge case to reason about.

### 5. Determinism

**VERDICT: Preserved, and strengthened — the proposed shape uses no RNG at all.
No new salt required.**

`argmax(ancestorCount)` with lowest-index tie-break is a pure function of
`(positions, edges)`, both of which are already deterministic from `runSeed`
(`:275`). No `System.Random` instance, therefore no `UnityEngine.Random` risk,
and nothing to add to the salt catalogue at `:111-124`.

**If** a later slice wants positional variety, the seed derivation hangs at
`new System.Random(runSeed ^ bandIndex ^ ChopshopSalt)` with
`ChopshopSalt = 0x4353` ('CS'). Do **not** declare that const now — an unused
salt const is exactly the vestigial value ADR-0011 forbids. Mention 0x4353 in
the selector's doc comment prose so a future slice doesn't collide.

**Save determinism: confirmed non-issue.** `NodeMapDto` (`:41`) persists the
full beacon graph payload rather than regenerating from `RunSeed`, and its own
doc comment cites exactly this scenario: *"any generator tweak would silently
mutate loaded runs."* In-flight saves unaffected, `SCHEMA_VERSION` stays 1.

### 6. Blast radius

**VERDICT: Zero red tests if `GuaranteedChopshopCount` defaults to 0 at the
record boundary. Two stale comment blocks and one drifting harness need
updating. No `MaxRetries` risk.**

*(Full test-by-test enumeration reproduced in §6 of this capture.)*

**`MaxRetries` risk: NONE.** The selector runs inside `AssignBeaconTypes` at
`:362` — after every retry-gated check. It cannot trigger a retry and cannot
over-constrain placement.

The one failure mode is an **empty band**. Ruling: **skip the band, place fewer,
do not fail the attempt** — mirroring the explicit `AddSpurs` policy at
`:1230-1234`. Same logic: this is type assignment, not a graph invariant, and
coupling it to geometry retries could burn all 100 attempts on a pathological
canvas.

**Storm-tuning impact: within noise.** Mean fuel cost per hop before =
(45·6 + 15·4 + 5·4 + 25·5 + 15·3)/105 = **4.95**. After = 500/100 = **5.00**.
On a 17-beacon walk that is well under 1% drift. **No storm retune required.**

### Three-lens self-audit (TD)

**Lens 1 — Codebase health.** ADR-0011 drift: clean, verified by grep. Deleting
the Chopshop weight row rather than zeroing it is the ADR-0011-correct disposal.
Single-responsibility: `AssignBeaconTypes` already owns "what type is beacon i";
a post-generation pass would create a second writer — rejected on that basis,
not on convenience. **Latent bug surfaced, not introduced:** the adjacency ban's
backward-only lookback means LeftFunnel's forced Combat is safe only because
Combat isn't in `AdjacencyBannedTypes`. Forcing a *banned* type is a new
situation the existing code does not handle. The one-line `bannedByType`
pre-seed is a hard requirement, not a nicety. Subscription lifecycle: N/A — pure
POCO in the `noEngineReferences: true` `WastelandRun.Run` assembly.
**Reference-image rule correctly does not fire:** that requirement applies to
placement-algorithm changes; this moves no position and adds/removes no edge —
it recolors existing nodes. Combined with the expired 5-slice window and the
post-freeze `_spurCount`/`_clusterCount` additions, **I confirm the freeze is
dead and does not gate this work.**

**Lens 2 — Optimization.** Cadence: once per successful generation. Cost:
predecessor table O(E) ≈ 150; reverse BFS per candidate ≈ 10k edge visits —
under 1% of generation cost against Delaunay's existing O(n²) inner loop.
Sub-millisecond, once per run. **I deliberately rejected both the cheaper and
the more expensive metric:** in-degree is cheaper but a poor routability proxy
on a near-uniform-degree Delaunay web; exact reach-probability under uniform
forward choice requires an X-sort plus float DP and answers the wrong question —
the player steers, they don't random-walk. Not caching, not pooling, not
memoizing: once-per-run at this cost does not earn it.

**Lens 3 — 1.0 survival.** `GuaranteedChopshopCount` is the 1.0 shape — Biome
2/3 will author different counts; that is the field's entire purpose. Derived
bands survive; if a biome ever wants asymmetric depth, the extension is an
internal band-curve function, not a signature change. **One named reversibility
cost:** `int` → `Vector2Int` if playtest demands variance — one field, one
capture. I judge variance actively *undesirable* here, so I am not pre-building
the range. The salt is reserved in prose, not in code. Downstream subscriber
check clean: `NodeMapDto` full-payload persistence means no save migration.
**Risk I am accepting and naming:** the empty-band graceful-skip is a silent
degradation path, mitigated by the exact-count test across 5 seeds, not by an
exception. If that test ever goes yellow, the band partition — not the retry
loop — is what needs revisiting.

**One verification item before merge, outside my read:** confirm
`BeaconSceneBinding` for Chopshop is in PrefabRoot mode (`_prefabRoots` entry
**and** the Mode 0→1 flip). Going from ~2.4 expected Chopshops to a guaranteed 3
raises the exposure of a missing flip from "sometimes" to "every run."

**Gate note:** this verdict rules on *shape*. The merge-time APPROVE
additionally requires an explicit EditMode-green attestation — compilation green
is not semantic green here, since three of the affected fixtures are statistical
rather than structural.

---

## 8. Independent verification of the TD's central claim

I did not take the "0.9–1.0 expected" figure on trust. Re-running my own Monte
Carlo on the real map, with 30 000 walks per configuration:

| Selection | Expected visits/run |
|---|---|
| Arbitrary node per band | 0.76 |
| `argmax(ancestorCount)` per band | 0.91 |

**Confirms the TD.** Ancestor-count selection improves the random-walk figure by
~20% but does not by itself reach the target — the justification rests on player
steering, which the routed-player model (3 of 3, 15 beacons, no fuel cost)
validates.

Ancestor counts on the sample map ranged 3 to 56 across 50 candidates, so the
metric discriminates meaningfully rather than returning near-uniform values.

Cut-vertex analysis independently confirmed the TD's chokepoint claim: exactly
two mandatory nodes, index 1 (x = 0.14) and index 53 (x = 0.86).

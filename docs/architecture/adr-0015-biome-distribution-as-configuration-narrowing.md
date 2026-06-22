# ADR-0015: Biome Distribution as Configuration Narrowing (ADR-0011 Scope-Narrowing Pattern)

## Status

Accepted (2026-06-15)

## Date

2026-06-15

## Last Verified

2026-06-21 (Block 2 amendment — strip-grammar dropped; FTL-style descriptor preserved; Block 1 generator landed Unity `ce3cc5d`)

## Decision Makers

User (BertanBerkol), Claude (technical-director session, 2026-06-15)

## Summary

Wasteland Run ships 1.0-canonical system shapes incrementally without
violating ADR-0011 (no bridges at done state) by narrowing scope via
**configuration data tables**, not via placeholder code paths. The
generator, runtime pipeline, and enum/schema are the full 1.0 shape from
day one; ScriptableObject distribution tables reference only the entries
that have shipped handlers. Adding new content later is a data edit, not a
code-path unfork. First application: the Slice 6 node-map biome-web
generator narrows biome 1 to `{Combat, Haven}` beacon types via
`Biome1Distribution.asset` while the full 7-type beacon enum (Combat,
EliteCombat, Merchant, Chopshop, Event, Rest, Haven) is real in code. The
canonical generator is FTL-style free-placed clustering — Poisson-disc
placement + Bowyer-Watson Delaunay triangulation + forward-bias edge
pruning — landed in Block 1 of the 2026-06-19 generator pivot.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core (architectural pattern, engine-agnostic) |
| **Knowledge Risk** | LOW — pattern uses standard ScriptableObject + enum + table-driven dispatch, all stable since Unity 2019 |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, ADR-0011, ADR-0013, ADR-0014, `design/gdd/node-map.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | None — pattern is engine-agnostic; only the consuming systems (BiomeDistributionSO, etc.) require engine verification |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0011 (no-bridges meta-rule — this ADR codifies one of its exception-#4 polymorphism patterns) |
| **Enables** | Slice 6 (canonical biome-web generator with narrowed biome-1 distribution), Fuel slice, Storm slice, every subsequent beacon-type handler slice, enemy roster expansion by biome, card pool by tier, merchant inventory tables, event encounter pools |
| **Blocks** | None — pattern lands alongside Slice 6 |
| **Ordering Note** | This ADR must be Accepted before Slice 6 Phase E (`BiomeWebGenerator` + `BiomeDistributionSO`) lands. |

## Context

### Problem Statement

ADR-0011 forbids bridges at done state, including stub returns (forbidden
pattern #6), bimodal paths (forbidden pattern #3), and transitional comments
(forbidden pattern #7). The user reinforced this on 2026-06-01 with an
explicit retraction of demo-forward development: build canonical 1.0 shape
directly, no throwaway scaffolding.

But shipping the entire game in one slice is not possible. Wasteland Run's
1.0 node-map per `design/gdd/node-map.md` has 7 beacon types, a storm
system, a Fuel economy, three biomes, and chassis-differentiated cadence
— each piece is its own vertical slice. The question is: how do we ship
the **canonical generator** in Slice 6 when only Combat encounters are
currently implemented, without violating ADR-0011 on what we ship?

Two failed answers we've already rejected:
1. Ship a prototype linear chain (`NodeMap.BuildLinearMilestone1`) and
   replace it later — TD rejected on 2026-06-15. Linear is scaffolding;
   the canonical FTL-style free-placed clustering does not exist in
   this shape.
2. Ship the canonical generator with `if (handler exists)` branches
   per beacon type — bimodal path (ADR-0011 #3) + stub returns (#6).

### Current State

Slice 6 closed 2026-06-19 — `BiomeWebGenerator`, `BiomeDistributionSO`,
and `Biome1Distribution.asset` shipped, with the linear/SingleCombat
scaffolds retired. Block 1 of the generator pivot (Unity `ce3cc5d`,
2026-06-20) replaced an interim strip-grid placement with the canonical
FTL-style algorithm: Poisson-disc placement + Bowyer-Watson Delaunay
triangulation + forward-bias edge pruning, all driven by the
`BiomeGenerationInputs` POCO. Block 2 (this amendment) threads the
`AllowBidirectional` traversal-policy flag through `NodeMap.FromBiomeGraph`
and renames the adjacency surface on the map view-models.

Beacon types currently emitted: `Combat`, `Haven`. Beacon types defined in
enum: `Start`, `Combat`, `EliteCombat`, `Merchant`, `Chopshop`, `Event`,
`Rest`, `Haven` (8 values — full set per this ADR's precondition). The
five unhandled types have no commit-pipeline handlers and no view-layer
presenters yet; each ships as its own canonical slice.

### Constraints

- ADR-0011: zero bridges, zero stubs, zero bimodal paths at done state.
- User retraction (2026-06-01): build canonical 1.0 shape directly.
- Slice scope budget: one vertical slice per week, not multi-week super-slices.
- `WastelandRun.Run` asmdef one-way arrow to `WastelandRun.Combat`; no
  reverse references; UI asmdef one-way arrow to both per ADR-0014.

### Requirements

- Canonical generator must be the full 1.0 shape from day one.
- Beacon-type enum must be complete (all 7 types defined).
- Each beacon type's handler can land in its own slice without re-touching
  the generator.
- Distribution table must be a data file (ScriptableObject), authored by
  designers, hot-swappable per biome.
- M2 must swap biome 1's distribution table for biomes-2-and-3 distribution
  tables with zero generator code changes.
- Tests must verify the canonical shape (FTL placement constraints,
  forward-path guarantee, etc.), not the narrowed content.

## Decision

Scope-narrowing for canonical 1.0 systems happens at the **data layer**, not
the code layer. The recipe is fixed:

1. **The enum / schema is complete.** All 7 beacon types must be real values
   in `BeaconType`. **Precondition for Slice 6 Phase E**: extend the current
   3-value enum (`{Start, Combat, Haven}`) to the full 8-value set
   (`{Start, Combat, EliteCombat, Merchant, Chopshop, Event, Rest, Haven}`)
   BEFORE the `BiomeDistributionSO` + generator land — single commit step,
   no consumers depend on enum cardinality. Same commit also rewrites the
   `BeaconType.cs` XML doc comment (currently cites ADR-0011 to justify the
   narrow enum; ADR-0015 inverts that reasoning). No `// TODO: add Merchant
   later` placeholders.
2. **The generator / runtime is canonical.** `BiomeWebGenerator` implements
   the full FTL placement algorithm per node-map GDD C1.1. It reads the
   distribution table; it does not know which types are "missing."
3. **The distribution table narrows by data.** `Biome1Distribution.asset`
   lists only Combat (weighted) + Haven (terminal). Merchant, Chopshop,
   EliteCombat, Event, Rest entries do not exist in the asset. The
   generator never emits them because the table never says to.
4. **Adding a new type is a data edit + a handler add.** When Merchant
   ships, the slice adds a new entry to `Biome1Distribution.asset` (or a
   later biome's table) and adds the commit-pipeline handler. The generator
   does not change. The enum does not change. No code path forks.
5. **The new type's handler lands as its own canonical implementation.**
   Not a stub. Not a "TODO." A real Merchant commit-pipeline branch with
   real reward semantics, validated by tests, shipped as a complete slice.

### Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│  BeaconType enum (Run/BeaconType.cs)                                │
│  { Start, Combat, EliteCombat, Merchant, Chopshop, Event, Rest,    │
│    Haven }                  ← 8 values post-Phase-E (was 3 values)  │
└─────────────────────────────────────────────────────────────────────┘
                                │
                                ▼ (referenced by)
┌─────────────────────────────────────────────────────────────────────┐
│  BiomeDistributionSO (Run/BiomeDistributionSO.cs)                   │
│  - DistributionEntry[] NonTerminal + TerminalBeaconType             │
│    (BeaconType + Weight, plus per-archetype combat weight pool)     │
│  - bool AllowBidirectional (lagging-dependency policy flag,         │
│    Block 2 — true until fuel/storm forward pressure ships)          │
└─────────────────────────────────────────────────────────────────────┘
                                │
                                ▼ (converted by factory to)
┌─────────────────────────────────────────────────────────────────────┐
│  BiomeGenerationInputs (Run/BiomeGenerationInputs.cs)               │
│  Engine-free POCO record per ADR-0002 noEngineReferences.           │
│  Carries the SO's narrowing data into the WastelandRun.Run asmdef.  │
└─────────────────────────────────────────────────────────────────────┘
                                │
                                ▼ (data input to)
┌─────────────────────────────────────────────────────────────────────┐
│  BiomeWebGenerator (Run/BiomeWebGenerator.cs)                       │
│  Poisson-disc placement + Bowyer-Watson Delaunay triangulation +    │
│  forward-bias edge pruning per node-map GDD C1.1.                   │
│  Reads only what the distribution table provides.                   │
│  Knows nothing about which types are "missing."                     │
└─────────────────────────────────────────────────────────────────────┘
                                │
                                ▼ (emits)
┌─────────────────────────────────────────────────────────────────────┐
│  NodeMap (Run/NodeMap.cs) — runtime graph                           │
│  Contains only beacon types that the distribution table specified.  │
│  No filtering, no skipping, no exceptions for unhandled types.      │
└─────────────────────────────────────────────────────────────────────┘
                                │
                                ▼ (committed via)
┌─────────────────────────────────────────────────────────────────────┐
│  Commit pipeline (RunSession / RunSceneHost)                        │
│  Dispatches on emitted beacon type. Every type the table can emit   │
│  has a real handler. Types the table does NOT emit do not appear    │
│  in the pipeline; no `else throw` branch needed.                    │
└─────────────────────────────────────────────────────────────────────┘
```

### Key Interfaces

```csharp
// Run/BiomeGenerationInputs.cs (POCO record, ADR-0002 noEngineReferences)
public sealed record BiomeGenerationInputs(
    IReadOnlyList<(BeaconType Type, int Weight)>          NonTerminalBeaconWeights,
    BeaconType                                            TerminalBeaconType,
    IReadOnlyList<(EnemyArchetypeId Id, int Weight)>      CombatArchetypeWeights,
    bool                                                  AllowBidirectional = false);

// Run/BiomeWebGenerator.cs (Block 1 — 2026-06-20)
public sealed class BiomeWebGenerator
{
    public BiomeGraph Generate(BiomeGenerationInputs inputs, int runSeed);
}

// Run/NodeMap.cs (Slice 6 + Block 2)
public sealed class NodeMap
{
    public static NodeMap FromBiomeGraph(
        BiomeGraph     graph,
        int            runSeed,
        BeaconType     terminalType,
        bool           allowBidirectional);   // Block 2
    // Advance accepts forward edges always; reverse edges only when
    // allowBidirectional is true.
}
```

### Implementation Guidelines

- **The distribution table is data, not code.** Do not branch on
  `distribution.Entries.Length` in the generator to enable / disable code
  paths. The generator runs the same algorithm regardless of how many
  entries the table has.
- **Do not validate "all 7 types must be present."** The whole point is that
  the table can narrow.
- **DO NOT validate terminal type at the generator level.** "Biome 1 emits
  only Combat + Haven" is a *content* assertion, not a *behavior* assertion.
  Express it as a per-biome content test (e.g.,
  `Biome1Distribution_emits_only_Combat_and_Haven`), not a generator guard.
  The generator stays content-agnostic.
- **DO validate "Combat / EliteCombat entries must carry a non-null
  `EnemyArchetypeId`"** in `BiomeDistributionSO.OnValidate` — content-level
  invariant per node-map GDD, enforced at author time so a designer cannot
  ship a Combat row without a roster.
- **Adding a new beacon-type handler is a separate slice.** That slice (a)
  adds the handler to the commit pipeline as canonical (no stub), (b)
  adds the new entry to whichever biome distribution tables want it,
  (c) ships tests for the new handler.
- **Tests cover the canonical shape, not the narrowed content.** Generator
  tests verify FTL placement constraints with any distribution table.
  Biome-1-specific tests verify "biome 1 emits only Combat + Haven" — that's
  a content assertion, not a behavior assertion.

## Alternatives Considered

### Alternative 1: Linear scaffold + replace later

- **Description**: Ship `NodeMap.BuildLinearMilestone1` (hardcoded 3-Combat
  linear chain) for Slice 6, replace with canonical generator in a later
  slice.
- **Pros**: Tiny slice; "make `PendingCardOffer` survive" ships immediately.
- **Cons**: Violates ADR-0011 (the linear factory is scaffolding with no
  surviving 1.0 form). Violates user's 2026-06-01 retraction. Creates
  exactly the throwaway prototype the user explicitly forbade.
- **Estimated Effort**: ~0.5 days for the slice itself, but creates debt.
- **Rejection Reason**: TD reversed prior 2026-06-13 approval after user
  caught the contradiction. Prior TD-me solved the local problem ("make
  `PendingCardOffer` survive") by reaching for the nearest helper without
  auditing against ADR-0011.

### Alternative 2: Canonical generator + runtime `if (handler exists)` branches

- **Description**: Ship the full FTL generator with all 7 beacon types
  emit-able, but the commit pipeline branches on whether a handler exists
  for the emitted type: `if (type == Merchant && !MerchantHandler.Available)
  throw NotImplementedException;`.
- **Pros**: Generator is canonical from day one.
- **Cons**: Bimodal path (ADR-0011 #3) at the commit pipeline. Stub return
  semantics (ADR-0011 #6) at every unhandled branch. Transitional comments
  (ADR-0011 #7) accumulate as TODO markers.
- **Estimated Effort**: Similar to chosen approach but with permanent debt.
- **Rejection Reason**: Direct ADR-0011 violation at multiple forbidden
  patterns simultaneously.

### Alternative 3: Build the entire game's content in one super-slice

- **Description**: Ship all 7 beacon-type handlers, Fuel, Storm, three
  biomes, and the generator in a single Slice 6.
- **Pros**: Zero scope narrowing needed.
- **Cons**: Multi-week slice. Each subsystem (Fuel, Storm, Merchant, etc.)
  is its own decision space — collapsing all decisions into one slice
  destroys the user's ability to course-correct between them. Memory
  ("Overall-Picture Thinking") warns against half-shipped systems that
  re-break.
- **Estimated Effort**: 4-6 weeks for one slice.
- **Rejection Reason**: Slice scope budget; user can't course-correct
  per-subsystem; risk of half-shipped second-half if scope blows up.

## Consequences

### Positive

- ADR-0011 stays clean — no stub returns, no bimodal paths, no transitional
  comments accumulating across slices.
- M2 biome expansion is a content edit (new distribution tables), not a
  generator refactor.
- Each beacon-type handler ships as a complete vertical slice with focused
  scope and testable success criteria.
- The pattern generalizes to enemy rosters per biome, card pools per tier,
  merchant inventories, event encounter pools — all the data-narrowing
  surfaces M2+ will need.
- Generator tests are content-agnostic; they validate the canonical algorithm
  once, then never change as content scales.

### Negative

- Slice 6 is larger than the linear-scaffold alternative would have been
  (~8-12 hrs vs ~0.5 days). TD considers this the correct trade.
- Designers need to author and maintain distribution-table ScriptableObjects;
  not as easy as inline magic numbers (but inline magic numbers are forbidden
  per `.claude/docs/technical-preferences.md`).
- Requires discipline at slice boundaries — every new beacon-type handler
  slice must ship its handler as canonical (no stubs), not as "for now just
  log a warning."

### Neutral

- Forces explicit content-vs-code separation. Each system using this pattern
  needs both an SO definition file and (separately) per-biome SO assets.
  More files; clearer ownership.

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| Future slice ships a beacon-type handler as a stub instead of canonical | MEDIUM | HIGH (would silently violate ADR-0011) | Pre-slice TD review enforces canonical-handler discipline; CI grep gates across Combat/Run/UI assemblies on `NotImplementedException`, `TODO: handler`, `Debug.LogWarning("not implemented`, and `throw new NotImplementedException` patterns |
| Designer ships a Combat / EliteCombat distribution entry with null `EnemyArchetypeId` | **HIGH** | HIGH (runtime null-ref at first commit) | `BiomeDistributionSO.OnValidate` rejects null `EnemyArchetypeId` on Combat/EliteCombat entries with a clear error; surfaces in inspector before the asset ships |
| Designer adds an unreviewed beacon type to a distribution table before its handler ships | MEDIUM | HIGH (runtime exception on commit) | `BiomeDistributionSO.OnValidate` checks distribution entries reference only handler-implemented types; editor-only assertion |
| Generator-only tests miss content-specific bugs (e.g., biome 1 emits Merchant by accident) | LOW | MEDIUM | Per-biome content assertion tests (separate from generator algorithm tests) verify each shipped biome's distribution emits only its allowed types |
| Pattern gets misapplied to systems that should NOT narrow by data (e.g., combat resolution) | LOW | LOW | This ADR explicitly scopes the pattern to **content-narrowing** systems (graph generation, roster selection, pool sampling). Mechanical / behavioral systems remain code-driven. |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU (frame time) | n/a | +0ms (generator runs once per run, off frame budget) | 16.6ms |
| Memory | n/a | +~2KB per BiomeDistributionSO asset | 2GB |
| Load Time | n/a | +~5ms per biome SO Resources-load at run start | n/a (gated by Awake) |
| Network (if applicable) | n/a | n/a | n/a |

No runtime performance cost. Pattern is data-loading at run-start only.

## Migration Plan

Pattern lands in Slice 6 Phase E alongside the new generator. No existing
system migrates to this pattern in this slice; future slices (enemy rosters,
card pools, merchant inventories) adopt it as their canonical shape.

1. Slice 6 Phase E lands `BiomeDistributionSO`, `BiomeWebGenerator`,
   `Biome1Distribution.asset`. Verify generator + distribution narrowing
   works end-to-end.
2. Future slice (Fuel economy) consumes biome distribution entries' Fuel
   cost fields if added.
3. Future slice (Merchant beacon) adds Merchant entry to biome distribution
   tables and Merchant commit-pipeline handler in the same slice.
4. M2 (Biome 2) authors `Biome2Distribution.asset` with biome-2 enemy
   roster + new beacon types. Zero generator code changes.
5. Slice 7 (Save & Persistence, ADR-0004) — `AllowBidirectional` joins the
   `RunStateDto` surface alongside `BiomeId + RunSeed + CurrentIndex +
   PathHistory[]` so save/load round-trips the traversal-policy flag with
   the rest of the run state.

**Rollback plan**: If the pattern proves brittle (e.g., distribution tables
become unmaintainable, or content drift creates per-biome edge cases that
should be code-driven), supersede this ADR with one that moves narrowing
back into code. The ScriptableObject assets are not load-bearing — they can
be inlined as code constants if needed. Generator interface stays the same.

## Validation Criteria

- [x] Slice 6 Phase E shipped `BiomeWebGenerator` + `BiomeDistributionSO` +
  `Biome1Distribution.asset` (2026-06-19). Block 1 (2026-06-20, Unity
  `ce3cc5d`) replaced the interim placement with Poisson-disc +
  Bowyer-Watson Delaunay + forward-bias pruning. Generator tests pass
  (determinism, anti-correlation across seeds, min-separation, forward-path
  guarantee, statistical angle-bias).
- [x] `BeaconType` enum is the full 8-value set (Start, Combat,
  EliteCombat, Merchant, Chopshop, Event, Rest, Haven). No `// TODO`
  markers. Doc comment cites ADR-0015.
- [x] `Biome1Distribution.asset` contains only Combat (non-terminal) and
  Haven (terminal). Generator emits only those types when run against
  this asset.
- [x] No `if (type == X) throw NotImplementedException` branches in the
  commit pipeline.
- [x] No `BuildLinearMilestone1`, no `SingleCombat`, no `Phase1Marker`
  surviving in code.
- [x] Block 2 (2026-06-21) — `AllowBidirectional` threaded through
  `BiomeGenerationInputs` → `NodeMap.FromBiomeGraph` → `NodeMap.Advance`
  with four EditMode invariant tests; map view-models renamed
  `IsReachable → IsAdjacentToCurrent`; ADR-0015 amended to drop
  strip-grammar wording while preserving the FTL-style descriptor.
- [ ] First future application (next beacon-type handler slice — likely
  Merchant) ships its handler as canonical, not as a stub.

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/node-map.md` | Node Map | C1.1 §7: "Beacon types: `{Combat, EliteCombat, Merchant, Chopshop, Event, Rest, Haven}`. Per-biome distribution is owned by the Node Encounter GDD (Node Map reads the distribution table; does not author it)." | `BiomeDistributionSO` is the distribution-table seam the GDD calls for. Generator consumes; designer authors per-biome assets. |
| `design/gdd/node-map.md` | Node Map | C1.1 §1-6: FTL beacon-web generation with 3 biomes, 18-22 beacons each, 80px min-sep, forward-biased connectivity, forward-path guarantee. | `BiomeWebGenerator` implements the canonical Poisson-disc + Bowyer-Watson Delaunay + forward-bias-pruning algorithm from day one, regardless of how narrow any biome's distribution table is. |
| (foundational) | (project-wide) | ADR-0011 forbidden patterns #3 (bimodal), #6 (stub returns), #7 (transitional comments) | Pattern routes scope narrowing entirely through data tables, bypassing all three forbidden-pattern surfaces. |

## Related

- **ADR-0011** (Accepted 2026-05-31) — Project-wide no-bridges meta-rule.
  This ADR codifies exception #4 (polymorphism via data tables) as a
  specific load-bearing pattern.
- **ADR-0013** (Accepted 2026-06-13) — Run-Scoped Card Collection +
  Reward-Source Composition. Sibling pattern: reward sources are composed
  via interface seam, not narrowed via data. The two patterns coexist —
  use composition for behavior-shaped seams, configuration narrowing for
  content-shaped seams.
- **ADR-0014** (Accepted 2026-06-13) — UI Toolkit as Primary Stack. The
  Slice 6 map view that renders biome-distribution output lands on UI Toolkit.
- **Slice 6 capture** — `production/polish-captures/2026-06-15-slice-6-runflow.md`
  (first concrete application).
- **Node Map GDD** — `design/gdd/node-map.md` C1.1 (canonical graph shape).

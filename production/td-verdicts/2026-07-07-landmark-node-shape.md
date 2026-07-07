# TD Verdict — Isolated Landmark Node Shape (2026-07-07)

## Context

Designer surfaced during Slice-E stage-3 HUD polish: rare, spatially isolated
"named place" beacons (e.g. abandoned airbase) that sit off to the side of the
main graph with restricted connectivity (typically 1 access edge + 1 exit
edge). Intent is world-flavour + spatial storytelling — the landmark FEELS
distant because it IS distant.

## Decision

**Green-light Option A (landmark overlay pass, data-driven). Defer
implementation to Biome 2 slice 1, with the airbase as the pilot landmark.**

Biome 1 ships with `_landmarks: []` (empty list). No code path lands ahead of
its first authored consumer.

## Shape (locked)

### Authoring

- New `LandmarkTemplateSO` in `WastelandRun.Run.Authoring`. Fields:
  - `_landmarkId : string` — save-key slug (e.g. `abandoned_airbase`)
  - `_rarity : float` — [0..1] probability per run
  - `_allowedXRange : Vector2` — normalized band, e.g. (0.60, 0.90)
  - `_allowedYRange : Vector2` — normalized band, e.g. (0.05, 0.20) for
    top-corner isolation
  - `_minDistanceFromAnyBeacon : float` — canvas px, e.g. 200
  - `_connectionInCount : int` — typically 1
  - `_connectionOutCount : int` — typically 1 (0 = terminal-shaped)
  - `_beaconType : BeaconType` — e.g. `Event` for the airbase
  - `_archetypeOverride : EnemyArchetypeId?` — set only if beaconType requires
- `BiomeDistributionSO._landmarks : List<LandmarkTemplateSO>` — Biome 1
  empty, Biome 2 authors the airbase.
- `BiomeGenerationInputs.Landmarks : IReadOnlyList<LandmarkInputs>` (POCO
  projection) — factory unpacks each SO to a POCO record at the boundary
  per ADR-0002.

### Generator pass

New pass in `BiomeWebGenerator.Generate` between Bowyer-Watson and edge
finalisation:

1. For each landmark template, roll RNG against `_rarity` using salt
   `0x4C4D` (`'LM'`) per ADR-0003.
2. If landed, sample position within X/Y bands with min-distance rejection
   against ALL existing beacons.
3. Emit beacon via shared `EmitBeaconAt(x, y, type, archetype)` private
   helper — cluster placement path calls this too so landmarks and
   cluster-beacons enter the graph through the same door (ADR-0011: no
   bimodal placement path).
4. Wire `_connectionInCount` forward edges from nearest-reachable neighbours
   with Δx > 0 into the landmark, and `_connectionOutCount` forward edges
   from the landmark to nearest neighbours ahead. Reuse existing
   forward-BFS-reach helper (no fork).

### Caps + determinism

- **Landmarks are exempt from the `lane × cluster × beacon ≤ 64` product
  cap.** Single-node, non-combinatorial. Add a separate `LandmarksMax=4`
  ceiling in `ValidateInputs`.
- Salt `0x4C4D` registered alongside `TopologySalt (0x4254)`,
  `BeaconTypeSalt (0x4249)`, `ArchetypeSalt (0x4541)` in the ADR-0003
  registry when landmark pass ships.

## Non-goals

- **Cluster-attach mode** — rejected (fights isolation semantic).
- **Special-case code branches per landmark name** — rejected (data-driven
  via SO list per ADR-0015).
- **Interior maps / sub-scenes for landmarks** — out of scope; landmarks
  are single beacons on the biome graph. Interior scenes can layer later
  without breaking this pass.

## Three-lens self-audit (from TD 2026-07-07)

- **Codebase health**: unified emit surface prevents ADR-0011 drift; landmark
  and cluster paths both call `EmitBeaconAt`. Reuse forward-BFS helper, no
  fork.
- **Optimization**: one-shot generator pass, rejection sampling against
  O(64) beacons. Tight loop for nearest-N — no LINQ scans.
- **1.0 survival**: named landmarks are a real 1.0 pattern (world-flavour).
  Building against zero authored content = infrastructure without consumer,
  trips `demo_forward_over_infrastructure`. Defer with pattern committed so
  Biome 2 planning can assume it.

## When

**Not now.** Slice E stage-3 HUD polish + Phase 2.5 parts-axis pivot are the
active workstreams. Land landmark pass as **Biome 2 slice 1**, with the
abandoned airbase as pilot landmark shipping in the same slice.

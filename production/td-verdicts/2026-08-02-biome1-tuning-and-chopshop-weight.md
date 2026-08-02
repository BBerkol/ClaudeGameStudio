---
date: 2026-08-02
topic: Biome 1 map-gen edge-distance tuning + Chopshop weight + adjacency-ban extension
files_touched:
  - Assets/Resources/Run/Biomes/Biome1Distribution.asset
  - Assets/Scripts/Run/BiomeWebGenerator.cs
adrs_at_risk: []
---

# TD Verdict — Biome 1 tuning: edge-length, Chopshop weight, Chopshop adjacency ban

## Context

Play-mode 2026-08-02 surfaced two Biome 1 map-gen issues:

1. **Long forward-edges from LeftFunnel** — user's 6th complaint on this axis. Screenshot shows the "YOU" node (LeftFunnel, index 1) with a forward edge stretching to a distant Event beacon in mid-canvas. Player will always pick the far node, skipping large map sections. Current tuning: `_maxEdgeLength: 1000`, `_reconnectRadius: 1000` on a 3840×1080 canvas. Prior tuning history 280/300 → 500/500 → 1000/1000 (last bump tracked canvas widening).
2. **Chopshop never spawns** — `_nonTerminalBeaconTypes` distribution is missing Type 4 (Chopshop). The workbench slice (commit 8556250) is complete but no beacon triggers it.

User also requested (2026-08-02): extend the same-type adjacency ban (currently Rest + Merchant) to include Chopshop, so shop-flavored stops don't clump.

## Root-cause analysis

**Problem 1.** LeftFunnel out-edges past the mandatory `(Start → LeftFunnel)` are unconstrained Delaunay edges subject only to `MaxEdgeLength`. LeftFunnel sits at X≈384–538 px; any Delaunay neighbour within 1000 px lands at X≈1384–1538 px (canvas midpoint). This is exactly what the screenshot shows — behavior is correct, cap is too loose.

**Problem 2.** `AssignBeaconTypes` samples from `_nonTerminalBeaconTypes` weighted pool. Type 4 has no entry → probability 0 → never spawns.

## Verdict

**ACCEPT** — three asset-value edits + one private-behavior code touch. All freeze-compliant (SO surface unchanged; asset values and internal HashSet are tuning, not new authored knobs).

### Values

- `_maxEdgeLength: 1000 → 550` — 14% of canvas / ~2× mean beacon spacing (~275 px at 55 beacons / 3840×1080). Halves current cap.
- `_reconnectRadius: 1000 → 450` — kept strictly < maxEdge so reconnect pass only revives short pruned edges, not medium-long jumpers.
- `_nonTerminalBeaconTypes` += `{ Type: 4, Weight: 5 }` — 5/105 ≈ 4.8% share → ~2-3 Chopshops per 55-beacon map (user chose rarer end of 2-4 target).
- `BiomeWebGenerator.AdjacencyBannedTypes` += `BeaconType.Chopshop,` — one-line HashSet extension. Private-behavior tightening; same category as the Rest+Merchant ban landed under (2026-07-01 verdict + 2026-07-30 Merchant extension).

### Why these knobs, not others

- **Diverge vs 1:1** — `MaxEdgeLength` is the hard visual "no jumping across the map" cap. `ReconnectRadius` is polish on stochastic pruning drops. Keeping `reconnect < maxEdge` preserves the code comment invariant that the re-add pass is "additive-only on close pairs".
- **Asset vs code cap on edge length** — a code-side "% canvas width" cap re-encodes a fact already expressible via `_maxEdgeLength + _canvasWidthPx`. Two sources of truth = ADR-0011 drift. Asset tuning stays canonical.
- **Merchant weight untouched** — Merchant (weight 15) and Chopshop (weight 5) are economically distinct (Merchant sells consumables/cards; Chopshop swaps parts). Similar-frequency-of-appearance is fine; adjacency ban now handles same-type clustering for both.

### Retry-loop risk

Low. `MaxRetries=100` with fresh topology RNG per attempt. `ForwardPathGuaranteeToTerminal` runs on the pre-reconnect edge set → independent of `_reconnectRadius`. At MinSep=90, mean spacing 275 px, the 550 px cap gives ~2× headroom for BFS. Funnel invariant unaffected (mandatory edges added post-cull). Spurs are additive polish (accept-fewer, never fail outer loop).

If post-drop editor shows `MaxRetries=100` exceptions, walk back to 650/500.

## Success criteria (play-mode verification)

1. 5 seeded runs: no LeftFunnel out-edge visually crosses canvas midpoint (X > 1920).
2. Editor console: zero `MaxRetries=100` exceptions across those 5 runs.
3. Chopshop appears 2-4 times per map across those 5 runs.

## Self-audit

- **Codebase health.** Clean. Asset-value tweaks + one-line HashSet extension. No ADR-0011 drift (no bridges, no bimodal paths). SO surface unchanged — respects 2026-07-07 freeze. Chopshop wiring is complete per BeaconSceneBinding flip (commit 8556250).
- **Optimization.** Zero perf risk. `EnforceMaxEdgeLengthCull` is O(edges) pre-existing; tighter cap removes work downstream. Adding fifth non-terminal type is O(1) in weighted roll. Adjacency ban extension is O(1) per assignment lookup.
- **1.0-shape survival.** Both values are canonical tuning knobs, not scaffolding. Chopshop weight is a first playtest anchor that will nudge with player data — that's normal balance authorship, not throwaway. No signature churn.

## Technical Director Review

**ACCEPT** — apply all four changes.

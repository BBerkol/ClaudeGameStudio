# Verdict — Sandstorm Chase + Fuel as Pacing Currency

**Date**: 2026-04-21
**Author**: creative-director (consulted via Task)
**Trigger**: User proposed FTL-style sandstorm chase + chassis-differentiated fuel burn as the node-map pacing spine. Verdict requested before `/design-system node-map` begins.
**Status**: GO WITH CONDITIONS. Pillar edits required before Node Map GDD pass.

---

## Proposal Summary

- A sandstorm advances from the left edge of the node map, consuming nodes 1-by-1 over discrete time (not real-time).
- Player navigates nodes with more routing freedom than pure-rightward FTL — choose branches, not just forward.
- **Fuel** is the per-move currency that gates routing. Scout burns less fuel per move → explores more; Heavy Truck burns more → forced toward shortest path.
- Storm + Fuel together form the map's pacing spine.

---

## Verdict: GO WITH CONDITIONS

This is the strongest directional alignment the map has had with the pillars. The sandstorm reframes the map as active pressure (not a level-select screen). Fuel finally has a distinct job. Chassis identity extends past combat into *routing*. Approved for design work.

---

## Pillar Alignment

| Pillar | Effect | Notes |
|---|---|---|
| **1. Vehicle as Character** | Amplifies | Chassis governs routing, not just combat. The car "decides" how much map you see. |
| **2. Chassis Identity** | Reinforces, with risk | Truck must feel *focused*, not *punished*. See Condition 4. |
| **3. Read to Win** | Reinforces *if legible* | Storm advance + fuel cost must be fully visible before node commit. See Condition 5. |
| **4. Scarcity with Agency** | Strongest gain | Fuel gets a real job. Scrap (build/repair), Fuel (routing), Energy (combat) become three distinct resource domains. |
| **5. Route Reflects Vehicle State** | Direct reinforcement | Chassis state and fuel state both shape viable routes. |

Tone/framing: **Storm = entropy.** The wasteland reclaiming the road behind you. Pressure, not a timer. Aligns with Mad Max road-as-protagonist framing already in the concept.

---

## Non-Negotiable Conditions

1. **Discrete time only.** Storm advances on node-commit (or defined tick events), never real-time. Protects anti-pillar "NOT a reflex game".
2. **Fuel owns routing only.** Fuel never leaks into combat economy. Combat stays on Energy. This is what makes the three-resource separation clean.
3. **Storm does NOT advance during combat.** Combat is a held breath. If the storm ticks while the player is in a fight, the game becomes a clock-pressure game and Pillar 3 / anti-reflex both crack.
4. **Heavy Truck survivability must be designed, not taxed.** Truck sees less of the map — its nodes must carry more weight. Options: higher reward density on critical-path nodes, Truck-specific shop/event weighting, or a small fuel-efficiency discount on elite paths. The fix is *value compensation*, not softening the fuel burn (which would dissolve Pillar 2).
5. **Legibility is mandatory.** Before a node commit, the player must see: current storm position, fuel cost of each option, what each route forecloses. If any of these are hidden, Pillar 3 is violated.

---

## Required Pillar Edits (BEFORE Node Map GDD)

Sequencing is load-bearing: the GDD must be written against the revised pillar text, not the current one.

### Pillar 4 rewrite
Replace the current Pillar 4 body with text that establishes three distinct resource domains:
- **Scrap** — build/repair economy (parts, install/scrap/repair)
- **Fuel** — routing pacing (map movement, storm avoidance)
- **Energy** — combat (per-turn card spending)

### New anti-pillar
Add to the Anti-Pillars list: **NOT a real-time map** — the storm advances on player action (node commits / defined tick events), never on wall clock. The map respects "NOT a reflex game" at the world layer the same way combat does at the encounter layer.

---

## North Star for Node Map GDD

> *"The map is a fuel-constrained route puzzle under the advancing entropy of the storm, where the chassis you chose has already decided how much of the map you get to see."*

Every Node Map GDD decision should be tested against this sentence.

---

## Downstream Dependencies to Flag in the Node Map GDD

- **Scrap Economy GDD** — must not share a pool with Fuel. Fuel acquisition sources must be distinct from Scrap sources to keep the three-domain separation legible.
- **Vehicle & Part GDD** — chassis fuel-burn stat becomes a new chassis-differentiator variable. Add to the chassis stat block (Scout low, Assault mid, Heavy Truck high).
- **Combat GDD** — non-interaction contract: nothing in combat advances the storm. Confirm no storm-tick side-effects leak into damage pipeline or end-of-turn hooks.
- **Loot & Reward GDD** — Truck-path node value compensation (Condition 4) requires reward-table awareness; flag as dependency.

---

## Open Questions Surfaced (for Node Map GDD to resolve)

1. Storm advance rate (nodes per tick, ticks per player-commit) — target feel: "pressure without panic".
2. Fuel acquisition — node rewards, chopshop, events? Must be legible and predictable enough to plan 2-3 nodes ahead.
3. What happens when a node is consumed *with the player still on it*? (Run-ender? Forced retreat? Damage?)
4. Does Haven approach have a storm behavior change (slowdown, acceleration, eye-of-the-storm beat)?
5. Fuel starting amount per chassis — tuning knob; must calibrate against average nodes-to-Haven count.

---

## Approval & Next Actions

- [x] Verdict received from creative-director
- [ ] Apply Pillar 4 rewrite to `design/gdd/game-concept.md`
- [ ] Add "NOT a real-time map" anti-pillar to `design/gdd/game-concept.md`
- [ ] Run `/design-system node-map` against revised pillars

# First Playable Milestone — Wasteland Run

**Target**: Sprint 3 end (Production weeks 5–6)
**Scheduled**: ~2026-06-22 (Sprint 3 close, 2-week sprints from 2026-05-25)
**Owner**: producer
**References**: `production/milestones/prototype-waiver.md` — Substitute Validation Path

---

## Purpose

The First Playable Milestone is the **validation substitute** for the prototype that was waived
under the γ decision (2026-04-22). Its purpose is identical to what a prototype would have
provided: confirm that the core loop *feels good* before further Feature-layer development
is committed.

The waiver explicitly states: "The validation question was not answered — it was deferred,
not retired." This milestone retires it.

---

## Scope: What Must Be Playable

A human must be able to play through the following end-to-end **without developer guidance**:

| Element | Requirement |
|---------|-------------|
| **Chassis** | Scout chassis, fully assembled with a starter deck |
| **Run structure** | 5-node run skeleton (not full biome variety — linear or minimal branching is acceptable) |
| **Combat** | At least 1 combat encounter against a non-placeholder enemy (intent telegraphed, subsystem portrait rendered, card play resolves correctly) |
| **Victory / defeat** | Combat ends with a win or loss outcome; post-combat screen shown |
| **Core loop** | Player can navigate from the start of a run to a combat resolution — the loop closes |

**Explicitly out of scope for this milestone**:
- Full 10-node run length
- Multiple biomes or enemy archetypes
- Merchant / Chopshop nodes
- Loot & Reward implementation
- Meta progression / Mastery
- Save & persistence (can be a stateless single-session build)
- Addressables (local asset loading acceptable)
- Polished art (placeholder sprites acceptable — Dual Portrait layout must be legible, not final)

---

## Validation Gate

### Quantitative gate

**≥ 3 playtest sessions required** before any Core-layer Feature work is committed. Internal
sessions are acceptable. Each session must be conducted with the playtester navigating
without guidance from the developer.

### Qualitative criteria (all must pass)

| # | Criterion | Source | How Verified |
|---|-----------|--------|--------------|
| 1 | A human played through the core loop (Scout chassis, 5-node run, ≥1 combat) without developer guidance | prototype-waiver.md condition 1 | Session observer notes |
| 2 | The game communicates what to do within the first 2 minutes of play | prototype-waiver.md condition 2 | Timestamped observation: player took a meaningful action ≤2min without being told |
| 3 | No critical fun-blocker bugs in the build | prototype-waiver.md condition 3 | QA triage prior to each session |
| 4 | The core mechanic (card play into vehicle combat) feels good to interact with | prototype-waiver.md condition 4 | Post-session interview: would player play again? Did the combat feel readable? |
| 5 | At least one playtester independently describes an experience matching the Player Fantasy sections of the Card Combat and V&P GDDs | prototype-waiver.md condition 5 (P3 + P4 validation) | Direct quote captured in playtest report — without prompting |

**Pillar validation targets**:
- **Pillar 3 (Read to Win)**: at least one playtester reads and correctly predicts an enemy intent outcome (FIZZLE, single-hit, position bonus) without being told the rule.
- **Pillar 4 (Scarcity with Agency)**: at least one playtester expresses a real-time Energy tradeoff decision ("I wanted to play this card but I didn't have enough").

---

## Gate Failure Consequence

If the First Playable gate fails — any qualitative criterion is not met across all 3 sessions —
a **Pillar Revision Sprint** precedes further Feature-layer development. This is the same
consequence as a failed prototype. No Feature-layer work may be committed until the gate
either passes or the sprint surfaces a specific, scoped design revision that is re-tested.

The Pillar Revision Sprint is time-boxed to one sprint (2 weeks). If the gate still fails
after revision, the Creative Director reviews the pillars directly.

---

## Playtest Report Requirements

Each session produces a report in `production/playtests/` covering:

1. **Session metadata**: date, playtester (anonymous ID acceptable), build version
2. **Observation log**: timestamped key events (first card played, first combat entered, first confusion moment, combat outcome)
3. **Pillar validation evidence**: direct quotes or paraphrased playtester statements relevant to Pillars 3 and 4
4. **Fun-blocker bugs**: any S1/S2 issues observed
5. **Session verdict**: PASS / FAIL against each of the 5 qualitative criteria

Minimum: 3 reports. Gate opens when all 3 are filed and majority (≥2 of 3) pass criterion 4 and criterion 5.

---

## Dependencies

| Dependency | Sprint | Status |
|------------|--------|--------|
| Vehicle POCO VP-001 (assembly + interfaces) | Sprint 1 | Ready for dev (S1-T6) |
| Vehicle POCO VP-002 (Vehicle class + IsDead) | Sprint 1–2 | Nice-to-have S1; Sprint 2 Day 1 fallback |
| Card System Story 004 (Addressables catalog) | Sprint 1 | In sprint (S1-T5) |
| Combat HUD — playable implementation | Sprint 2 | Unblocked by /ux-review |
| Enemy implementation (1 archetype) | Sprint 2 | W2 unblocked per systems-index |
| Node map run skeleton (5 nodes, linear) | Sprint 2–3 | |
| Scout starter deck in-game | Sprint 2 | `assets/data/cards/starter-decks.yaml` authored |

---

## Sign-Off Required

| Role | Required For |
|------|-------------|
| Producer | Gate open: ≥3 playtest reports filed |
| Creative Director | Gate pass: P3 + P4 pillar validation confirmed |
| User (design lead) | Final confirmation before Core-layer Feature work begins |

---

## Change Log

| Date | Change |
|------|--------|
| 2026-05-25 | Initial document. Sprint 3 target. Authored as T3 of Sprint 1, following /ux-review NEEDS REVISION pass on combat-hud.md and stage.txt flip to Production. |

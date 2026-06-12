# Prototype Milestone Waiver

**Decision ID**: γ (gamma)
**Date of Decision**: 2026-04-22
**Formally Documented**: 2026-05-25
**Documented By**: producer (gate-check skill)
**References**: `production/gate-checks/gate-production-2026-05-25.md` — Required Artifact #1

---

## Decision

The Pre-Production → Production phase gate requires at least one prototype in
`prototypes/` with a README. This requirement is **formally waived** for
Wasteland Run under the conditions stated below.

**Status**: Accepted — production may advance without a prototype artifact.

---

## Rationale

At the time of the γ decision (2026-04-22), the following conditions held:

1. **Architecture foundation was already solid.** Eight ADRs were either
   Accepted or Proposed, covering the core systems (vehicle, combat, cards,
   save, RNG, asset loading). The design risk of wiring systems together was
   lower than typical Pre-Production because the interfaces were already
   contract-locked.

2. **GDD coverage was complete.** All 10 MVP-tier GDDs were approved with
   full 8-section coverage. The "does the design make sense?" question that
   prototyping answers had already been answered by the design process.

3. **The vertical slice validation question is what matters, not the artifact.**
   The Pre-Production gate uses a prototype as a proxy for "have we validated
   the core loop feels good?" The γ decision acknowledged that this validation
   question was not answered — it was deferred, not retired.

4. **Solo developer context.** Building a throwaway prototype followed immediately
   by the real implementation of the same systems represents a significant
   duplication of effort at solo-dev scale. The Creative Director returned
   CONCERNS (not NOT READY) at the gate check, indicating the design foundation
   was solid enough to proceed with this caveat.

---

## Risk Accepted

By waiving the prototype requirement, the following risk is explicitly accepted:

| Risk | Pillar | Description |
|------|--------|-------------|
| Feel validation deferred | Pillar 3 — Read to Win | The "cards feel good to play" claim is unvalidated from play data. Static analysis of GDDs cannot confirm this. |
| Feel validation deferred | Pillar 4 — Scarcity with Agency | The "Scrap feels meaningfully scarce" claim is unvalidated. |
| Chassis Identity unvalidated | Pillar 2 — Chassis Identity | W-5 in risk register. Cannot be proven by static analysis. |

These risks are **not retired by this waiver**. They are deferred to the
First Playable Milestone.

---

## Substitute Validation Path

The prototype milestone is replaced by a **First Playable Milestone** with
mandatory playtest gates. This milestone is the validation mechanism that the
prototype requirement was intended to provide.

**Milestone document**: `production/milestones/first-playable.md`
**Target**: Sprint 3 (production weeks 5–6)

### Required at First Playable:

1. A human has played through the core loop (Scout chassis, 5-node run,
   1 combat encounter minimum) without developer guidance
2. The game communicates what to do within the first 2 minutes of play
3. No critical fun-blocker bugs in the first-playable build
4. The core mechanic (card play into vehicle combat) feels good to interact with
5. At least one playtester independently describes an experience matching
   the Player Fantasy sections of the Card Combat and V&P GDDs (P3 and P4
   validation)

**Gate**: ≥ 3 playtest sessions required before any Core-layer Feature work is
committed. If the first-playable gate fails, a pillar-revision sprint precedes
further Feature-layer development — the same consequence as a failed prototype.

---

## Director Panel at Gate Check (2026-05-25)

All four directors returned **CONCERNS** (not NOT READY). Relevant excerpts:

- **Creative Director**: "Pillars 3 and 4 (Read to Win, Scarcity with Agency)
  are unvalidated FEEL claims — must be confirmed by first-playable, not
  deferred to late Production. Conditional advance recommended."

- **Technical Director**: "Architecture and design foundation are solid; this
  is an execution validation gap, not a design problem."

- **Producer**: "Execution sequence for solo dev: vehicle-poco →
  vehicle-visual → save-persistence. Core ADR slate runs as parallel track."

- **Art Director**: "Character visual profiles gap does NOT apply — chassis
  sheets are the correct artifact for this vehicular game."

---

## Conditions for Production Advance

Production may begin with this waiver in place, subject to the following
conditions being tracked and enforced:

1. ✅ Control manifest authored (`docs/architecture/control-manifest.md`)
2. ✅ ADR-0008 accepted with memory-budget addendum
3. ✅ Sprint-01 written with first-playable milestone explicitly scheduled
4. ✅ `/ux-review design/ux/combat-hud.md` completed (Sprint 1 T2) — verdict: NEEDS REVISION, blocking issue resolved in-review. Report: `production/gate-checks/ux-review-combat-hud-2026-05-25.md`
5. ☐ First Playable Milestone reached by Sprint 3 end with ≥3 playtest sessions

Conditions 1–3 are met as of 2026-05-25. Conditions 4–5 are outstanding and
tracked in `production/sprints/sprint-01.md`.

---

## Sign-Off

| Role | Status | Date |
|------|--------|------|
| Creative Director | CONCERNS → Conditional Advance | 2026-05-25 (gate-check report) |
| Technical Director | CONCERNS → Conditional Advance | 2026-05-25 (gate-check report) |
| Producer | CONCERNS → Conditional Advance | 2026-05-25 (gate-check report) |
| Art Director | CONCERNS → Conditional Advance | 2026-05-25 (gate-check report) |
| User (design lead) | ✅ Accepted | 2026-05-25 |

# Gate Check: Systems Design → Technical Setup

**Date**: 2026-04-24
**Checked by**: `/gate-check` skill (review mode: `full`)
**Project**: Wasteland Run — 2D card roguelike, Unity 6.3 LTS, C#
**Predecessor gate**: `2026-04-24-pre-production.md` (FAIL — cross-GDD review was FAILING earlier today)
**Verdict**: **CONCERNS (PASSABLE)** — advance with conditions

---

## TL;DR

All 3 required artifacts present. All 6 quality checks passing. All 4 directors returned **CONCERNS** with explicit conditions attached as Technical Setup entry tasks (none returned NOT READY). The FAIL chain from `gdd-cross-review-2026-04-24.md` and `gdd-cross-review-2026-04-24-rerun.md` is closed — closure report `design/gdd/gdd-cross-review-2026-04-24-rerun2.md` is **CONCERNS (PASSABLE)**.

Stage advance: `production/stage.txt` = `Technical Setup`.

---

## Required Artifacts — 3/3 Present

| # | Artifact | Status |
|---|---|---|
| 1 | `design/gdd/systems-index.md` — 10 MVP systems enumerated, all Approved | ✓ |
| 2 | All 10 MVP-tier GDDs in `design/gdd/` | ✓ |
| 3 | Cross-GDD review report — `design/gdd/gdd-cross-review-2026-04-24-rerun2.md` (CONCERNS-PASSABLE) | ✓ |

**MVP GDDs verified present**: card-system.md, vehicle-and-part-system.md, save-persistence.md, status-effects.md, card-combat-system.md, scrap-economy.md, node-map.md, enemy-system.md, loot-reward.md, node-encounter.md.

**Individual design reviews**: 5 review logs in `design/gdd/reviews/` (card-combat-system, card-system, save-persistence, status-effects, vehicle-and-part-system). Remaining 5 reviews recorded inline in `systems-index.md` Next Steps with explicit "Approved YYYY-MM-DD" timestamps.

---

## Quality Checks — 6/6 Passing

| # | Check | Status |
|---|---|---|
| 1 | All MVP GDDs pass individual design review | ✓ |
| 2 | `/review-all-gdds` verdict ≠ FAIL | ✓ CONCERNS (PASSABLE) |
| 3 | All cross-GDD consistency issues resolved | ✓ 4/4 BLOCKERs + 6/6 CONCERNs closed |
| 4 | System dependencies bidirectionally consistent | ✓ verified in Phase 2 today |
| 5 | MVP priority tier defined | ✓ |
| 6 | No stale GDD references | ✓ verified in Phase 2 today |

Registry drift scan: zero drift across 44+ canonical constants in `design/registry/entities.yaml`.

---

## Director Panel Assessment

### Creative Director: CONCERNS

**Summary**: Pillar coverage holds across all 10 GDDs without tone/register conflict. Three-axis arbitration materially strengthens P2 (Chassis Identity) at the data layer entering Technical Setup. No core-promise drift.

**Conditions attached**:

- **CD-C1**: W-4 (Haven scrap sink) must be revisited before VS scope lock, not silently deferred post-MVP. Scarcity-with-agency loop has no terminal payoff in MVP build — players will feel the absence even if they can't name it.
- **CD-C2**: P2 runtime validation (W-5) must appear as a named gate in the VS playtest plan, not left implicit.
- **CD-C3**: Three-axis enum triad (SilhouetteClass / VisualFamily / ArchetypeFamily) must be treated as load-bearing during architecture — any ADR that collapses it back to a single field requires CD re-approval.

### Technical Director: CONCERNS

**Summary**: Five invariants (POCO separation, IVehicleView/IVehicleMutator split, passive serializer, 3-sub-model Card Combat, 3-axis enum DELEGATE) are unusually disciplined for pre-ADR state. Foundation ADRs may begin drafting now in order: **Card Combat state/event → Loot RNG/seeding → Save/Persistence** (lowest engine-risk first).

**Conditions attached**:

- **TD-C1**: Engine-validation spike must complete before any **rendering or UI ADR** is Accepted — URP Render Graph 2D sample + UI Toolkit 6.3 USS card-layout smoke test + New Input System combat action-map. Not a pre-condition for Save/Loot/Combat-state ADRs.
- **TD-C2**: Save ADR must explicitly resolve (a) schema-registry location (per-DTO attribute vs. central manifest) and (b) partial-load failure policy (what happens when one system's DTO fails to deserialize). GDDs intentionally left these open; ADR must not inherit them.
- **TD-C3**: ADR-0001's registry-backed citation pattern is the template for all Foundation ADRs, to preserve the zero-drift property.

### Producer: CONCERNS

**Summary**: Sequencing broadly sound, but two items listed as Technical Setup deliverables are actually design artifacts that should precede Foundation ADRs.

**Conditions attached**:

- **PR-C1**: Move `design/accessibility-requirements.md` and `design/ux/interaction-patterns.md` to **Week 1** of Technical Setup, **before** Foundation ADRs. Both inform Save ADR + event architecture + test framework + UI Toolkit USS structure.
- **PR-C2**: Scope Art Bible to **§1–4 Visual Identity Foundation only**; defer full production style sheets to Pre-Production.
- **PR-C3**: Add Save ADR explicitly as ADR-0004 in Technical Setup task list so it does not get dropped. Save ADR must precede any story that mutates persistent run state.
- **PR-C4**: Log W-1/W-2/W-4/W-5 + OQ-NE12 in `production/risk-register/` with explicit owners and trigger dates **before** closing this gate.

### Art Director: CONCERNS

**Summary**: Visual Identity Anchor + ADR-0001 are genuinely actionable — art bible §1–4 has real starting material. Three-axis enum resolution closes the biggest prior art-contract ambiguity. Two named visual gaps remain.

**Conditions attached**:

- **AD-C1**: Art Bible §3 (HUD Visual Language / State Color System) must define **rust-shimmer animation spec** (what it means in pixel-art terms, frame budget, and how it reads when haptic is absent for keyboard/mouse primary players) **before** the HUD UX spec is handed to implementation.
- **AD-C2**: Art Bible §3 must define the **colorblind-safe Ambush overlay non-color channel** (shape / border pattern / icon badge) before the Node Map UX spec is handed to implementation.

---

## Chain-of-Verification

5 challenge questions checked:

1. *Could any CONCERN elevate to a blocker?* No — all directors framed concerns as Technical Setup entry tasks, not pre-conditions for entry.
2. *Are concerns resolvable in the next phase?* Yes — accessibility doc ~1 day; engine-validation spike ~3–5 days; Save ADR decisions standard ADR work; Art Bible §3 specs scoped.
3. *Did I soften FAIL into CONCERNS?* No — artifact checklist 3/3, quality checklist 6/6, no director said NOT READY.
4. *Missed artifacts that could reveal blockers?* `docs/consistency-failures.md` absent (optional). Registry verified zero-drift. No hidden state.
5. *Do CONCERNS cumulatively blocker?* No — they form a coherent Week 1–3 plan for Technical Setup rather than a compounding problem.

**Verdict unchanged** after CoV: **CONCERNS (PASSABLE)**.

---

## Verdict: CONCERNS (PASSABLE) — advance with conditions

All required artifacts present, all quality checks passing, all 4 directors CONCERNS (no NOT READY). Conditions attach as named Week 1–3 tasks in the Technical Setup phase.

---

## Recommended Technical Setup Task Stack

Derived from director conditions. Consume these in order to satisfy PR-C4 (risk register) first, then design-tail items (PR-C1), then engine validation (TD-C1), then Foundation ADRs (TD-C2), then art foundation (AD-C1/C2).

### Week 1 — Design tail + risk register (precedes Foundation ADRs)

1. **PR-C4** — Populate `production/risk-register/` with W-1 (Truck reward loop), W-2 (Biome 3 Elite length), W-4 (Haven scrap sink), W-5 (Chassis Identity runtime validation), OQ-NE12 (accessibility gate). Each entry: owner + trigger date + resolution criteria.
2. **PR-C1(a)** — Write `design/accessibility-requirements.md` with tier commitment (Basic / Standard / Comprehensive / Exemplary).
3. **PR-C1(b)** — Initialize `design/ux/interaction-patterns.md` (pattern library seed — even minimal is acceptable).

### Week 1–2 — Engine validation spike (precedes rendering/UI ADRs)

4. **TD-C1** — Engine-validation spike:
   - URP Render Graph 2D sample (URP Compatibility Mode removed in 6.3; Render Graph API required).
   - UI Toolkit 6.3 USS card-layout smoke test (stricter USS parser).
   - New Input System action-map for combat (legacy Input Manager deprecated).

### Week 2–3 — Foundation ADRs (in TD-recommended order)

5. **ADR-0002** — Card Combat state/event architecture. Formalize CombatLoop / SubsystemState / PositionState three-sub-model contract, turn-state machine, event bus shape, intent resolution order. Uses the EnrageIntentCandidates / EnemyBrain surface from Enemy System.
6. **ADR-0003** — Loot RNG / seeding. Formalize `GenerateRewards(context, seed) → RewardOffer[]` reentrancy + determinism. Reinforce `System.Random` mandate (no UnityEngine.Random).
7. **ADR-0004** — Save & Persistence. Passive serializer + per-system DTOs + N=1 backup + per-system schema versioning. **Must resolve**: (a) schema-registry location (per-DTO attribute vs. central manifest); (b) partial-load failure policy (TD-C2).

### Week 3 — Visual Identity Foundation (precedes HUD/Map UX handoff)

8. **PR-C2 / AD baseline** — Art Bible §1–4 (Visual Identity Foundation only). Defer full production style sheets to Pre-Production.
9. **AD-C1** — Art Bible §3 rust-shimmer animation spec (pixel-art terms, frame budget, haptic-absent fallback).
10. **AD-C2** — Art Bible §3 colorblind-safe Ambush overlay non-color channel (shape/border/icon badge).

### Also required for Technical Setup → Pre-Production gate

11. Test framework initialization: NUnit via Unity Test Framework + `tests/unit/` + `tests/integration/` + CI workflow + one example test file.
12. `/architecture-review` run + report once Foundation ADRs are Accepted.
13. Master architecture document at `docs/architecture/architecture.md`.
14. Architecture traceability index at `docs/architecture/architecture-traceability.md`.
15. One UX spec started (e.g., main menu or core HUD — Combat HUD already drafted).

---

## Hard Constraints Preserved from Prior Gate Work

- **CD-C3**: Three-axis enum triad is load-bearing. No ADR may collapse SilhouetteClass / VisualFamily / ArchetypeFamily into a single field without CD re-approval.
- **TD-C3**: Registry-backed citation pattern (ADR-0001 template) is required for all Foundation ADRs.
- **PR-C3**: Save ADR must precede any story that mutates persistent run state.

---

## Stage Transition

`production/stage.txt` updated from `Systems Design` → `Technical Setup` on user approval of this gate report.

---

## Files Referenced

- `design/gdd/systems-index.md`
- `design/gdd/gdd-cross-review-2026-04-24-rerun2.md` (closure report)
- `design/gdd/gdd-cross-review-2026-04-24.md` (first FAIL)
- `design/gdd/gdd-cross-review-2026-04-24-rerun.md` (Stage 0 pre-fix snapshot)
- `design/registry/entities.yaml` (44+ canonical constants)
- `docs/architecture/adr-0001-visual-vehicle-part-system.md` (Accepted 2026-04-21)
- `docs/engine-reference/unity/VERSION.md` (Unity 6.3 LTS, 6 critical flags)
- `.claude/docs/technical-preferences.md`
- `production/gate-checks/2026-04-24-pre-production.md` (prior FAIL gate — superseded)

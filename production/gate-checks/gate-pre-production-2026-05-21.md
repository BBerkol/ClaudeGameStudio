# Gate Check: Technical Setup → Pre-Production

**Date**: 2026-05-21
**Checked by**: gate-check skill (full review mode — 4 directors)
**Verdict**: CONCERNS (Conditional PASS)
**Stage advanced**: Technical Setup → Pre-Production

---

## Required Artifacts: 13/13 present

- [x] Engine chosen — Unity 6.3 LTS
- [x] Technical preferences configured — naming conventions, performance budgets, forbidden patterns
- [x] Art bible (Sections 1–4+) — `design/art/art-bible.md`, 9+ sections, AD-ART-BIBLE APPROVED 2026-04-18
- [x] ≥3 ADRs covering Foundation-layer systems — 8 ADRs (ADR-0001 through ADR-0008)
- [x] Engine reference docs — `docs/engine-reference/unity/` (VERSION, breaking-changes, deprecated-apis, current-best-practices, PLUGINS)
- [x] Test framework initialized — `docs/test-strategy.md` (NUnit/Unity Test Framework, 136 tests, two-repo structure documented)
- [x] Test files in documented location — 136 EditMode tests in Wasteland Run repo (verified 2026-04-24)
- [x] CI workflow or documented deferral — deferred, trigger: "first sprint plan in production/sprints/"
- [x] Master architecture doc — `docs/architecture/architecture.md` (5 architectural principles, layered system map)
- [x] Architecture traceability index — `docs/architecture/architecture-traceability.md` (257 TRs indexed)
- [x] `/architecture-review` report — `docs/architecture/architecture-review-2026-04-25.md`
- [x] `design/accessibility-requirements.md` — Standard tier committed, 12 sections
- [x] `design/ux/interaction-patterns.md` — exists

---

## Quality Checks: 10/10 passing

- [x] Architecture covers rendering, input, state management (ADR-0001 visual/rendering, ADR-0002 state, ADR-0006 data authoring)
- [x] Technical preferences: naming conventions + performance budgets (60fps / 16.6ms / 200 draw calls / 2GB)
- [x] Accessibility tier defined (Standard — documented with rationale)
- [x] At least one screen's UX spec started (combat-hud, critical-state-feedback, armor-exposure, interaction-patterns)
- [x] ADRs have Engine Compatibility sections (confirmed ADR-0002; TD verified pattern across all ADRs)
- [x] ADRs have GDD Requirements Addressed sections
- [x] No ADR references deprecated APIs
- [x] HIGH RISK Unity 6.3 domains addressed — URP Compat Mode (RenderGraph committed in architecture.md §6), [SerializeField] fields-only (caught + fixed in ADR-0007 adversarial review), Box2D v3 (minimal 2D surface), Input System default (acknowledged)
- [x] Foundation layer traceability gaps = ZERO — Vehicle POCO closed ADR-0005; Card System closed ADR-0006; Save covered ADR-0004 (24/25 TRs)
- [x] ADR circular dependency check — clean DAG, no cycles (0002→None; 0003 derives 0002; 0004 depends 0002; 0005 depends 0001+0002; 0006 depends 0002+0005; 0007 depends 0005; 0008 depends 0007)

---

## Bonus: ADR-0007 Mechanics-Doc Condition Resolved

ADR-0007 (Frame-Driven Variable Slot System) was "Accepted (architecture surface only)" pending `design/gdd/vehicle-and-part-mechanics.md` approval. That approval occurred today (R8 APPROVED, 2026-05-21). ADR-0007 can now be marked fully Accepted — the condition is met.

---

## Director Panel Assessment

**Creative Director: CONCERNS**
Pillar fidelity is exceptional and verifiable across all 10 GDDs. Anti-pillars did real work. Scope discipline preserved — Chassis Identity deferral documented, not hidden.
- Concern 1: Pillar 2 (Chassis Identity) cannot be validated until Vertical Slice — accepted and documented (B3 scoping note).
- Concern 2: Pillar 1 (Vehicle as Character) felt weight unproven — granted-card lifecycle is mechanically correct but emotional quality requires a playtest, not a test suite.
- Concern 3: UX surface partial — Combat HUD UX spec must be authored before combat prototype code, or Pillar 3 information channel drifts.

**Technical Director: READY**
Foundation layer gaps truly zero. ADR-0008 Proposed is correct posture for Pre-Production entry — contract surface stable, no production code loads from catalog yet. All HIGH RISK Unity 6.3 changes explicitly addressed in architecture.md §6. Three pending Core ADRs correctly sequenced as Pre-Production slate (pure C#, LOW engine risk). CI deferral honest with trip-wire trigger. No prototype-killing technical risks identified.
- Tracked concern: ADR-0008 → Accepted before first art-loading story; CI trip-wire at first sprint plan; Control Manifest authoring; Status Effects ADR first; ADR-0007 full-Accepted (now resolved).

**Producer: CONCERNS**
Pre-Production entry justified by 10/10 GDD approval and clean Foundation architecture. No sprint plans or epics is expected — Pre-Production deliverables. Prototype milestone (Month 1–2) achievable for solo dev.
- Concern 1: 3 pending Core ADRs must be Sprint 1–2 deliverables, not as-needed.
- Concern 2: Save ADR scope needs clarification (new ADR vs ADR-0004 amendment).
- Concern 3: Balance Sim Runner needs Pre-Production spec + Month 2 proof-of-concept (full build at Month 5 alone is risky for a 10%-success-rate roguelike).

**Art Director: CONCERNS**
Art bible production-grade for Vertical Slice. Card art production and HUD implementation can begin immediately. Character profiles absent is appropriate. Combat HUD spec is binding-level pre-production specification.
- Concern 1: ADR-0008 Proposed creates a visual pipeline risk for chassis and vehicle-part sprites specifically.
- Concern 2: Chassis damage sprite scope underspecified — need per-chassis sprite production matrix before first chassis sprint.

---

## Blockers

None.

---

## Consolidated Concerns (12 items — address in Sprint 1–2)

### Architecture & ADR

1. **ADR-0008 → Accepted** before first chassis-art-loading story — requires TD sign-off + memory budget table + build-pipeline integration plan. [TD + AD]
2. **3 pending Core ADRs** (Status Effects, Scrap Economy, Node Map Commit) authored in Sprint 1–2, before implementation begins on those systems. Status Effects ADR first (tightest coupling to existing 136 tests). [PR + TD]
3. **Control Manifest** authored early Pre-Production — forbidden-token codification currently "pending" per architecture.md §2.2; determinism enforcement relies on it. [TD]
4. **Save ADR scope** clarified before Sprint 1 planning — new ADR (IL2CPP stripping, SynchronizationContext, etc.) or amendment to ADR-0004? [PR]
5. **CI** stood up the same sprint that first sprint plan lands in `production/sprints/` — this is the documented trigger, not optional. [TD]

### Production

6. **Prototype Milestone document** (`production/milestones/prototype.md`) with hard scope boundaries (3 systems max, named) — must exist before Sprint 1 begins. [PR]
7. **Balance Sim Runner**: spec authored during Pre-Production; single-system harness proof-of-concept at Month 2 as stretch goal; full runner by Month 5. A 10%-success-rate roguelike with ~150 cards cannot be hand-tuned in the 8-week window between Month 5 build and Month 8 closed beta. [PR]
8. **Card System GDD** — add 1-line dependency note: *"Part-granted card integration spec deferred to implementation — coordinate with Vehicle & Parts GDD before implementing granted-card lifecycle."* [PR]

### Visual (Before first chassis art sprint)

9. **Chassis sprite production matrix** — states × zones × variants per Scout + Assault, authored as companion to art-bible §5.1 before first chassis sprite is commissioned. [AD]

### Pre-Committed Creative Validation Gates (author into Sprint 1 plan)

10. **Month 1 paper test**: Paper-prototype Assault + Heavy Truck decks against 3–5 enemies. Pass condition: chassis architecture produces divergent strategies on paper. Fail = architectural flexibility gap before code is committed. [CD]
11. **Month 2–3 VS playtest primary criterion**: *"Does the tester verbally express loss when a part is destroyed?"* Pass = Pillar 1 delivering felt weight. Fail = return to design (not push forward). [CD]
12. **Sprint 1**: Combat HUD UX spec authored and approved before first line of combat HUD code is committed. [CD]

---

## Specialist Disagreements

None. TD returned READY while CD, PR, and AD returned CONCERNS — but TD's 5 tracked concerns are substantively consistent with the others. ADR-0008 was independently flagged by both TD and AD without coordination, confirming it as the most significant pre-production action item.

---

## Nice-to-Have

- Enemy encounter sequence pre-confirmed with game designer before enemy art production begins (routine alignment check, not a spec gap).
- ADR-0007 file header updated to reflect full Accepted status (mechanics-doc condition now met).

---

## Chain-of-Verification

5 challenge questions (CONCERNS draft) checked — verdict unchanged. No concern warranted escalation to blocker. No FAIL condition was softened. Director coverage is sufficient (TD directly read 7 files; CD read 7 files; AD read multiple art/UX/GDD files).

---

## Scope Signal

Pre-Production phase itself: **XL** — 3 pending Core ADRs, 3 pending Feature ADRs, first prototype build, first vertical slice, art production pipeline, 4 MVP UX specs to author, Balance Sim Runner architecture. Producer should plan capacity accordingly.

---

## Verdict: CONCERNS (Conditional PASS)

All 13 required artifacts present. All 10 quality checks passing. Foundation layer: zero gaps. No director returned NOT READY. Twelve concerns identified — all non-blocking, all addressable in Sprint 1–2.

**The project advances to Pre-Production.** The 12 concerns above constitute the Sprint 1–2 production agenda.

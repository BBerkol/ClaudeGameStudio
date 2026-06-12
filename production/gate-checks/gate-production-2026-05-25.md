# Gate Check: Pre-Production → Production

**Date**: 2026-05-25
**Checked by**: gate-check skill
**Review mode**: full (all four directors)
**Verdict**: FAIL (auto-trigger: Vertical Slice Validation)

---

## Required Artifacts: 9/15 present

| # | Artifact | Status |
|---|----------|--------|
| 1 | Prototype in `prototypes/` with README | ❌ MISSING — formally skipped (γ decision 2026-04-22) |
| 2 | First sprint plan in `production/sprints/` | ❌ MISSING |
| 3 | Art bible (all 9 sections) + AD sign-off | ✅ PASS — 9/9 sections, AD-ART-BIBLE passed 2026-04-18 |
| 4 | Character visual profiles | ✅ N/A — chassis sheets (§3.1, §5.1–5.4) serve this function for a vehicular game; AD confirms no gap |
| 5 | All MVP-tier GDDs complete | ✅ PASS — 10/10 approved |
| 6 | Master architecture document | ✅ PASS — exists (last updated 2026-04-25; stale — predates ADRs 0005–0008) |
| 7 | At least 3 Foundation-layer ADRs | ✅ PASS — 8 ADRs (6 Accepted, 1 Accepted arch-only, 1 Proposed) |
| 8 | Control manifest at `docs/architecture/control-manifest.md` | ❌ MISSING |
| 9 | Epics: Foundation AND Core layer | ⚠️ PARTIAL — 4 Foundation epics present; zero Core layer epics |
| 10 | Vertical Slice build: exists and playable | ❌ NOT BUILT |
| 11 | Vertical Slice: playtested (≥3 sessions) | ❌ ZERO playtests |
| 12 | Vertical Slice playtest report | ❌ MISSING — `production/playtests/` does not exist |
| 13 | UX specs: main menu, core HUD, pause menu | ⚠️ PARTIAL — combat-hud.md exists; no main menu, no pause menu |
| 14 | HUD design document | ✅ PASS — `design/ux/combat-hud.md` exists, authored |
| 15 | All key screen UX specs passed `/ux-review` | ❌ FAIL — combat-hud.md is "Ready for Review" but unreviewed; no UX review report exists |

---

## Quality Checks: 5/12 passing

| Check | Status |
|-------|--------|
| Core loop fun validated (playtest data) | ❌ NO playtests |
| UX specs cover all GDD UI Requirements | ⚠️ PARTIAL — HUD only |
| Interaction pattern library exists | ✅ PASS |
| Accessibility tier addressed in UX specs | ⚠️ PARTIAL — HUD has it; other screens missing |
| Sprint plan references story file paths | ❌ NO sprint plan |
| Architecture doc reflects all ADRs | ⚠️ STALE — architecture.md + traceability index predate ADRs 0005–0008 |
| ADRs have Engine Compatibility sections | ✅ PASS — confirmed by architecture-review 2026-04-25 |
| ADRs have ADR Dependencies sections | ✅ PASS |
| Core fantasy delivered (playtest evidence) | ❌ NO playtests |
| 3 of 4 Foundation epics have stories created | ❌ FAIL — vehicle-poco, vehicle-visual, save-persistence have EPIC.md only |
| Control manifest codifies forbidden-token / required-pattern rules | ❌ MISSING |
| ADR-0008 Accepted | ❌ Still PROPOSED |

---

## Vertical Slice Validation — AUTO-FAIL TRIGGER

> Per gate rules: if any item is NO, the verdict is automatically FAIL regardless of other checks.

| Item | Status |
|------|--------|
| A human has played through the core loop without developer guidance | ❌ NO — VS not built |
| The game communicates what to do within the first 2 minutes | ❌ UNKNOWN — no build exists |
| No critical fun-blocker bugs in VS build | ❌ UNKNOWN — no build exists |
| The core mechanic feels good to interact with | ❌ UNVALIDATED — no playtests |

**All four VS validation items unmet. Auto-FAIL triggered.**

---

## Director Panel Assessment

```
Creative Director:   CONCERNS
  Must-haves before Production: (1) Combat HUD /ux-review'd,
  (2) first-playable milestone scheduled in Sprint 1-2,
  (3) control manifest authored. Pillars 3 and 4 (Read to Win,
  Scarcity with Agency) are unvalidated FEEL claims — must be
  confirmed by first-playable, not deferred to late Production.
  Conditional advance recommended once items 1–3 land.

Technical Director:  CONCERNS
  4 Sprint-1 conditions: (1) author control manifest [HARD],
  (2) Accept ADR-0008 with deferred memory-budget addendum,
  (3) re-run /architecture-review to cover ADRs 0005–0008 and
  update stale architecture.md and traceability index,
  (4) stand up CI. Story 004 cannot honor its Manifest Version
  field without the control manifest. ADR-0007 header still
  reads "architecture surface only" — mechanics-doc R8 is
  approved; header must be updated.

Producer:            CONCERNS
  5 conditions: (1) sprint-01.md written, (2) vehicle-poco
  stories broken down, (3) Card System GDD part-granted-cards
  dependency note added, (4) Prototype Milestone waiver doc,
  (5) ADR-0008 on risk register. Execution sequence for solo
  dev: vehicle-poco → vehicle-visual (gated by ADR-0008) →
  save-persistence. Core ADR slate must run as parallel track.

Art Director:        CONCERNS
  5 concerns: (1) chassis sprite production matrix absent
  [blocks first art sprint], (2) combat HUD not /ux-review'd,
  (3) five UI screens have no UX specs (main menu, pause,
  part inspect, post-combat flow, map UI), (4) consolidated
  asset specifications document missing, (5) ADR-0007 header
  staleness (mechanics-doc approved — caveat should be removed).
  Character visual profiles gap does NOT apply — chassis
  sheets are the correct artifact for this vehicular game.
```

---

## Blockers

1. **Vertical Slice not built / no playtests** ← AUTO-FAIL TRIGGER
   Pillars 3 ("Read to Win") and 4 ("Scarcity with Agency") are FEEL pillars unvalidatable from GDDs or tests alone. The γ decision deferred this risk but did not retire it. Path forward: build a first-playable in early Production as a mandatory Sprint 1–2 milestone, run ≥3 sessions, revise before committing further Feature-layer work.

2. **Control manifest missing**
   Stories embed the manifest version; without it, all stories run on implicit rules. ADR-0003's forbidden-token CI grep exists only as verbal discipline. Determinism enforcement is load-bearing for this roguelike. Estimated: 2–4 hours to author.

3. **No sprint plan**
   Production without a sprint plan is Pre-Production with code. Must be the first artifact of Production.

4. **Combat HUD not `/ux-review`'d**
   Highest-leverage Pillar 3 delivery surface. Unreviewed layout density at minimum resolution (1280×720) could force expensive mid-sprint rework.

5. **ADR-0008 still Proposed**
   Blocks Story 004 (Card System) and all art-loading stories in `vehicle-visual-layer`. Story 004 already contains the text "ADR-0008 is Accepted for this use case" — a documentation contradiction with the actual ADR status.

---

## Recommendations

### Must-do before flipping stage.txt (estimated: 1–2 days)

- `/create-control-manifest` — codify Foundation layer rules and ADR-0003 forbidden-token list
- Accept ADR-0008 via `/architecture-decision` with lightweight memory-budget addendum (3-category EA table)
- Write `production/sprints/sprint-01.md` via `/sprint-plan`
- Write `production/milestones/prototype-waiver.md` documenting the γ decision formally

### Must-do Sprint 1

- Run `/architecture-review` covering ADRs 0005–0008 → update stale `architecture.md` + traceability index
- Run `/ux-review design/ux/combat-hud.md`
- Story breakdowns for `vehicle-poco-part-catalog` (at minimum)
- Schedule first-playable milestone explicitly (Sprint 1 target: week 4–6)
- Stand up CI (`game-ci/unity-test-runner@v4` per test-strategy.md deferred plan)
- Add 1-line part-granted cards dependency note to Card System GDD
- Update ADR-0007 header: remove "architecture surface only" caveat (mechanics-doc R8 now approved)

### Must-do before first art sprint

- Author chassis sprite production matrix (Scout + Assault: states × zones × variants)

### Must-do before Core-layer sprints

- Author Core ADRs: Status Effects, Scrap Economy, Node Map Commit (one per sprint, parallel track)

### Must-do before vehicle-visual-layer sprint

- Create stories for `vehicle-visual-layer` epic (post ADR-0008 acceptance)

---

## Verdict: FAIL

**Auto-FAIL trigger: Vertical Slice Validation block — all four items unmet.**
No playable build exists. Zero playtest sessions. Pillars 3 and 4 are unvalidated feel claims.

This verdict is **advisory** — the user may override with explicit acknowledgement. All four directors returned **CONCERNS**, not NOT READY. The architecture and design foundation are solid; this is an execution validation gap, not a design problem.

**Chain-of-Verification**: 5 questions checked — verdict unchanged (auto-trigger confirmed; no over-stated blockers found).

---

## Path to PASS

The blockers are execution gaps, not architectural problems. To re-run this gate as a CONCERNS → conditional PASS:

1. Author control manifest
2. Accept ADR-0008 with addendum
3. Write sprint-01.md with first-playable milestone explicitly scheduled
4. Formally document the γ prototype-skip decision
5. Run `/ux-review` on the combat HUD

Estimated time to clear all five: **1–2 focused working days.**

Once those are done, stage.txt may be updated to `Production` and Sprint 1 may begin.

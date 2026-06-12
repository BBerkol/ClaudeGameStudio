# Sprint 1 — 2026-05-25 to 2026-06-08

## Sprint Goal

**Primary**: Close path to Production gate PASS and create vehicle-poco-part-catalog stories.
**Stretch**: Land VP-001 interfaces + Card System S004 as first Foundation implementations.

## Capacity

- Sprint duration: 2 weeks (14 calendar days)
- Working days (solo dev, est. 5 days/week): 10 days
- Buffer (20%): 2 days
- **Available: 8 days**

---

## Sequencing Notes

**Day 1 priorities** — run these first to unblock the rest of the sprint:
- T2 (`/ux-review combat-hud`) — front-loaded because any UX CONCERNS verdict needs absorption time
- T4 (`/create-stories vehicle-poco-part-catalog`) — must complete Day 1 to unblock T6 (VP-001)

**Gate-flip sequence** — T1 → T2 → T3 must complete in order before `stage.txt` can change.

---

## Tasks

### Must Have (Critical Path) — 7.0 days estimated

| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|--------------|---------------------|
| T1 | Write `production/milestones/prototype-waiver.md` — formally document the γ 2026-04-22 prototype-skip decision | producer | 0.25 | — | File exists; γ decision documented with CD sign-off rationale and references to `production/gate-checks/gate-production-2026-05-25.md`; explicitly states first-playable milestone in Sprint 3 as the validation substitute |
| T2 | Run `/ux-review design/ux/combat-hud.md` *(front-load Day 1)* | ux-designer | 0.5 | — | Review report written in `production/gate-checks/` or `design/ux/`; APPROVED or NEEDS REVISION verdict recorded; combat-hud.md status updated |
| T3 | Flip `production/stage.txt` → `Production`; write `production/milestones/first-playable.md` | producer | 0.25 | T1, T2 | `stage.txt` reads `Production`; `first-playable.md` exists with Sprint 3 (week 5–6) target date; lists first-playable scope: core combat loop playable end-to-end with Scout chassis, 2 enemy types, 5-node run skeleton |
| T4 | Run `/create-stories vehicle-poco-part-catalog` *(Day 1, unblocks T6)* | lead-programmer | 0.5 | — | Story files created in `production/epics/vehicle-poco-part-catalog/`; VP-001 (assembly + interfaces) validated ready via `/story-readiness`; confirm with technical-director that ADR-0005 namespace/folder scaffolding is captured in VP-001 scope (no separate pre-req needed) |
| T5 | Implement Card System Story 004: ChassisMastery Addressables Catalog | unity-addressables-specialist | 2.5 | ADR-0008 Accepted ✓ | `production/epics/card-system/story-004-chassismastery-addressables-catalog.md` — all ACs passing including player-build catalog verification (not just Editor play-mode); IL2CPP link.xml entry confirmed; /story-done COMPLETE WITH NOTES or better |
| T6 | Vehicle POCO VP-001: `WastelandRun.Vehicle.asmdef` + core interfaces (`IVehicleView`, `IVehicleMutator`, `SlotKind`, `SlotInstance`, `IFrameLayout`, `AnchorPoint`, `SlotPosition`) | gameplay-programmer | 2.0 | T4 | TBD file from T4; assembly compiles with `noEngineReferences: true`; CI grep passes (zero `UnityEngine.*` in Vehicle assembly); *W-5 check*: interfaces leave room for Chassis Identity discrimination — no premature lock-in of chassis-neutral assumptions into IVehicleView signatures |
| T7 | Run `/architecture-review` covering ADRs 0005–0008 → update stale `architecture.md` + traceability index | technical-director | 1.0 | — | Architecture review report written in `docs/architecture/`; `architecture.md` reflects ADRs 0005–0008; traceability matrix has no Foundation-layer gaps; scope caveat: if review surfaces >2h of fixup work, capture findings and defer deep doc updates to Sprint 2 |

### Should Have — 1.6 days

| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|--------------|---------------------|
| T9 | Stand up CI: `game-ci/unity-test-runner@v4` GitHub Actions workflow | devops-engineer | 1.0 | — | `.github/workflows/tests.yml` exists and committed; test suite triggers on push to main; all existing tests pass in CI run; `docs/test-strategy.md` CI deferral note removed |
| T10 | ADR-0007 header: remove "architecture surface only" caveat (mechanics-doc R8 approved) | technical-director | 0.25 | — | ADR-0007 Status reads "Accepted" without qualifier; Revision History row added for 2026-05-25 |
| T11 | Card System GDD: add 1-line part-granted-cards → vehicle-and-part-mechanics dependency note | game-designer | 0.1 | — | `design/gdd/card-system.md` Dependencies section references `vehicle-and-part-mechanics.md` for granted-card lifecycle rules |
| T12 | Add ADR-0008 Addressables memory risk to risk register | producer | 0.25 | — | `production/risk-register/risk-register.md` has R-ADR0008 entry: leak-per-combat risk description, trigger = first chassis load implementation story, owner = unity-addressables-specialist, mitigation = explicit Release on combat teardown per ADR-0008 Runtime Discipline section |

### Nice to Have

| ID | Task | Agent/Owner | Est. Days | Notes |
|----|------|-------------|-----------|-------|
| T8 | Vehicle POCO VP-002: `Vehicle` class + `StructuralHp` death condition + `IsDead` backing field | gameplay-programmer | 2.0 | Defer to Sprint 2 if T6 runs long — VP-001 alone closes the assembly contract; VP-002 can land in Sprint 2 Day 1 |
| T13 | `/create-stories save-persistence` | lead-programmer | 0.5 | Nice to close out Foundation story coverage before Sprint 2 implementation |
| T14 | Vehicle POCO VP-003: `PartDefinitionSO` + `ChassisDefinitionSO` authoring SOs (Unity-facing) | unity-specialist | 2.0 | Depends on T4 story creation; only start if T6 + T8 both land and capacity remains |

---

## Carryover from Previous Sprint

*(Sprint 1 — no previous sprint; this is the first sprint of Production.)*

---

## Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| T5 Addressables first-time setup takes longer than 2.5d (catalog build, group config, IL2CPP) | MEDIUM | MEDIUM | Estimate raised from initial 1.5d for this reason. If >2.5d, cut scope to catalog structure only and defer player-build verification to Sprint 2 Day 1 |
| T6 VP-001 surfaces ADR-0007 mechanics-doc gaps (R8 not yet in ADR-0007 scope) | MEDIUM | HIGH | Complete T10 (ADR-0007 header update) before T6 begins if capacity allows; escalate gaps to technical-director immediately — do not implement around an unresolved ADR gap |
| T2 /ux-review returns CONCERNS on combat-hud; T3 gate-flip stalls | LOW | MEDIUM | Front-load T2 to Day 1. NEEDS REVISION verdict is acceptable for gate passage; only FAIL blocks T3 |
| T4 `/create-stories` produces 10+ stories, making VP-001 scope larger than 2.0d | LOW | LOW | Adjust T6 estimate after T4 completes Day 1; surface to producer if > 3.0d |
| T8 VP-002 bleeds into Must Have territory mid-sprint, consuming buffer | MEDIUM | MEDIUM | T8 is explicitly Nice to Have in this plan. If pulled in, treat the 2.0d estimate as a hard ceiling and cut rather than extend |

---

## Dependencies on External Factors

- Unity Editor required for Card System S004 (T5): catalog build, IL2CPP player build, Inspector validation — must be run in Unity project at `GameStudio\Madmax Rougelike\Wasteland Run\`
- GitHub Actions runner required for CI standup (T9)

---

## Definition of Done for Sprint 1

- [ ] `production/stage.txt` reads `Production`
- [ ] `production/milestones/prototype-waiver.md` exists
- [ ] `production/milestones/first-playable.md` exists with Sprint 3 target
- [ ] `/ux-review` report exists for `design/ux/combat-hud.md`
- [ ] `production/epics/vehicle-poco-part-catalog/` has story files
- [ ] Card System Story 004 closed via `/story-done`
- [ ] Vehicle POCO VP-001 closed via `/story-done`
- [ ] `/architecture-review` report exists covering ADRs 0005–0008
- [ ] All Logic stories have passing unit tests in `tests/`
- [ ] No S1 or S2 bugs in delivered features
- [ ] `production/sprint-status.yaml` reflects final story statuses

---

## PR-SPRINT Gate Notes

**Verdict**: CONCERNS → REALISTIC (after adjustments)

Producer adjustments applied:
- T5 re-estimated 1.5d → 2.5d (first Addressables story in project; build pipeline setup included)
- T7 re-estimated 0.5d → 1.0d (architecture.md + traceability index updates included)
- T8 (VP-002) moved from Should Have → Nice to Have; Sprint 2 target
- T4 → Day 1 sequencing note added
- W-5 Chassis Identity acceptance criterion added to T6
- Sprint goal reframed as Primary / Stretch
- T2 front-loaded to Day 1 for UX findings absorption time

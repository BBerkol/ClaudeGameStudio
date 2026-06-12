# UX Review: Combat HUD
**Date**: 2026-05-25
**Reviewer**: ux-review skill
**Document**: `design/ux/combat-hud.md`
**Template**: HUD Design (Phase 3B)
**Platform Target**: PC (Steam) — Keyboard/Mouse primary, Gamepad additive
**Accessibility Tier**: Standard (from `design/accessibility-requirements.md`)
**Sprint Task**: S1-T2

---

## Completeness: 8/8 (blocking issue resolved in-review)

| Section | Status |
|---------|--------|
| HUD Philosophy | ✅ Present — five governing tenets, anti-pillar, density cost acknowledgment |
| Information Architecture | ✅ Present — 43-channel inventory with GDD source bindings, OUT-OF-SCOPE table |
| Visual Budget | ✅ Added during review — max simultaneous elements, max screen area, red lines defined |
| Layout Zones | ✅ Present — pixel coordinates, zone map, safe-rectangle, z-order, ultrawide strategy |
| HUD Elements | ✅ Present — per-zone tables for all 9 zones |
| HUD States by Gameplay Context | ✅ Present — 5 combat phases (Setup/PlayerTurn/PlayerResolve/EnemyTurn/Ended) + Group B–D cascades |
| Platform Adaptation | ✅ Present — 16:9/16:10/21:9/32:9, DPI, HUD Scale 85–150% |
| Tuning Knobs | ✅ Present — HUD Scale, High Contrast, Reduced Motion, SR verbosity, Captions, Vibration, Cognitive Hints |

---

## Quality Checks

### Blocking Issues Resolved In-Review: 1
1. **Visual Budget** — Added §2 "Visual Budget" table defining max Decision-Critical channels (22), max Contextual channels (6 worst-case), max screen area (~79%), max total channels (43), and three red lines for escalation.

### Advisory Issues: 4

| # | Issue | Severity | Owner | Blocker Level |
|---|-------|----------|-------|---------------|
| A1 | **OQ-CH4: Gamepad pause binding** — Start = End Turn, Select = Accessibility overlay, Y = quick-focus. No gamepad pause home. Standard tier requires every core action to be gamepad-reachable. | HIGH | UX | P1 (pre-alpha) — escalate before VP-001 interfaces land |
| A2 | **systems-index.md row #14 stale** — showed "Not Started." Updated to "NEEDS REVISION — /ux-review 2026-05-25" during this review. | LOW | Producer | Fixed in-review |
| A3 | **OQ-CH18 save-ADR reference** — was "Tied to the pending Save ADR." ADR-0004 now Accepted. Updated OQ-CH18 to cite ADR-0004 and propose the non-blocking red-glyph indicator resolution. | LOW | Technical Director | P1 (needs TD sign-off on proposed behavior) |
| A4 | **Prototype carry-forward header** — referenced `prototypes/combat/REPORT.md` (path does not exist; prototype waived). Updated to note contracts are absorbed into §5. | LOW | — | Fixed in-review |

---

## GDD Alignment: ALIGNED

All 10 feeder GDDs covered in the 43-channel Information Architecture table. Every channel has a source binding pointing to a game-state system — no HUD-invented state. Scrap Economy and Loot & Reward correctly excluded with documented rationale (Pillar 4 three-domain separation).

## Accessibility: COMPLIANT — Standard tier met with one pending gap

- Color independence: 10-state redundancy table ✅
- Keyboard-only: 15 actions fully mapped, no chord interactions ✅
- Gamepad parity: 14/15 actions (pause binding unresolved — OQ-CH4) ⚠️
- Screen reader: ch42 Announce Stream, 4 verbosity tiers, on-demand read, grammar contract ✅
- Reduce Motion: 8-entry substitution table, zero shake, 3 Hz flash ceiling ✅
- WCAG 2.1 AA floor; AAA exception for armor cap-line ✅
- Text scaling: 16pt minimum, HUD Scale + OS DPI combined case documented ✅

## Pattern Library: CONSISTENT

All interactive elements reference library patterns (3.1 Card, 3.2 Status Effect Chip, 3.3 Intent Zone, 3.4 Tooltip, 3.5 Focus & Navigation). No ad-hoc interactions invented.

---

## Files Modified

| File | Change |
|------|--------|
| `design/ux/combat-hud.md` | Added Visual Budget table to §2; updated prototype header reference; updated OQ-CH18 to reference ADR-0004 |
| `design/gdd/systems-index.md` | Row #14 status updated from "Not Started" to NEEDS REVISION with /ux-review date |

---

## Verdict: **NEEDS REVISION → Blocking Issue Resolved In-Review**

The single blocking issue (Visual Budget missing) was resolved during this review session. All advisory items are documented, owner-assigned, and pre-alpha level — none block the Production gate.

**Gate passage**: This review satisfies Sprint 1 T2 acceptance criteria. The verdict of NEEDS REVISION with blocking issue resolved is acceptable per the sprint plan ("APPROVED or NEEDS REVISION accepted").

**Next actions before alpha**:
1. Resolve OQ-CH4 (gamepad pause binding) — UX owner, P1
2. TD sign-off on OQ-CH18 proposed behavior (non-blocking red-glyph error indicator) — P1
3. Art Director delivers color-blindness simulation proofs before Combat HUD enters alpha (OQ-CH19 resolution path)

**Next recommended skill**: `/team-ui` (when ready to begin visual design + implementation coordination for Combat HUD)

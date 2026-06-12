# Sprint Ticket — Combat HUD Refresh

> **Created**: 2026-05-18
> **Owner (when scheduled)**: ux-designer (primary) + art-director (visual) + accessibility-specialist (3-channel verification)
> **Status**: Not scheduled — named to close R5 recommended #2 ("name the refresh ticket; don't leave as 'deferred'")
> **Anchored to**: ADR-0007 (Frame-Driven Variable Slot System) Phase 4 outputs

---

## 1. Scope

`design/ux/combat-hud.md` was authored before ADR-0007 landed. Phase 4 of
the ADR-0007 handoff seeded three perceptual contracts that combat-hud.md
will need to absorb when the HUD next gets a refresh pass:

| Phase 4 deliverable | Combat HUD integration needed |
|---|---|
| `design/ux/critical-state-feedback.md` | Chassis vignette + HP-bar at-risk treatment + ambient audio layer wiring; per-side asymmetry (player stronger than enemy) |
| `design/ux/armor-exposure.md` | Per-slot break burst + redirect connector cue; persistent `× N` ExposureMultiplier badge on exposed armor slots (steady-state read) |
| `design/audio/amplified-redirect-sfx.md` | Additive amplified-redirect layer on the redirect-target hit; mix-priority ordering with CriticalState stinger |

In addition, the open questions tagged in each seed entry (OQ-CSF1..3,
OQ-AE1..3, OQ-AR1..3) are decisions the refresh pass should resolve.

## 2. Acceptance Criteria

- combat-hud.md absorbs the three seed entries (either by reference + targeted
  inserts, or by full inlining — author's choice during refresh).
- Per-slot ExposureMultiplier badge surface is specified (table row + zone
  placement in combat-hud.md §4 Layout Zones).
- 3-channel redundancy floor is verified for the new cues per
  `design/accessibility-requirements.md` §3 + §4.
- The 9 OQs across the three seed entries are resolved or explicitly deferred
  with named follow-up.
- A combat-hud.md change-log entry references this ticket.

## 3. Dependencies

- **Blocked-on**: nothing — the three seed entries are sufficient anchors for the refresh pass.
- **Blocks**: combat HUD implementation (ui-programmer cannot wire these cues until the refresh pass locks pixel-level treatment).

## 4. Tracking

This ticket lives at `production/sprint-tickets/combat-hud-refresh.md`. When
sprint planning picks it up, the producer assigns owners + sets a target
sprint. Until then it sits as a named-but-unscheduled artifact (the explicit
ask from R5 recommended #2 — not a perpetual "deferred").

## 5. References

- R5 review log: `design/gdd/reviews/vehicle-and-part-system-review-log.md` Review 5 entry, recommended #2.
- R6 handoff: `production/r5-handoff-adr-0007-amendment-scope.md` §Phase 4.
- Phase 4 seed entries (above).
- combat-hud.md: `design/ux/combat-hud.md` (target document).

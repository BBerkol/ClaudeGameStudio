---
name: ADR-0007 Variable Slot System UX Gaps
description: UX issues from adversarial reviews of V&P GDD post-ADR-0007 (Reviews 2, 3, 4). Review 4 completed 2026-05-18.
type: project
---

**Review history:** Reviews 2 and 3 (2026-05-18) closed all 7+10 blockers. Review 4 (2026-05-18) is the current re-review pass.

**Why:** ADR-0007 broke the 4-slot HUD assumption. combat-hud.md still hard-references 4-slot layout in at least 6 places. U1–U8 defer layout decisions to Combat HUD GDD without specifying them. Several UX perceptual gaps remain unresolved as deferred recommended items from Review 2.

**How to apply:** Before any HUD implementation work begins for variable-slot vehicles, the Combat HUD GDD must be revised. Do not let UI programmer begin Chassis Zone work against the stale 4-slot spec.

**Gaps still open after Review 4 (all carried from Review 2 deferred list #8, #9):**

1. CRITICAL (Review 4 NEW BLOCKER B1) — Armor breakthrough perceptual layer is unspecified. `OnArmorExposed` fires; plate-shatter SFX fires; R_ARM.5 sprite swap fires. But "MAY emphasize" for the RedirectsTo HUD highlight is not binding. No persistent indicator. No 3x multiplier surface. No screen-reader announce grammar for OnArmorExposed. Player cannot reliably know an exposed Armor slot routes amplified damage to hull_0 before the next hit lands.
2. CRITICAL (Review 4 NEW BLOCKER B2) — CriticalState UI rendering has no home. OnCriticalStateChanged event is spec'd and fires correctly (F-VP2 step h). But no GDD specifies what the subscriber does: no vignette/heartbeat audio channel in combat-hud.md, no V&P requirement for the visual, no Combat HUD channel for it. The Player Fantasy "maybe this run is over" beat has an event driver but no display contract.
3. CRITICAL (Review 4 NEW BLOCKER B3) — U6 offline deck-change notification is not updated for event-driven architecture. U6 still describes polling behavior ("lists the removed cards"). OnGrantedCardRemoved is now the driver per Review 3 B9 — but U6 is silent on this. The notification spec still reads like a pull model, not an event subscriber.
4. HIGH (Review 4 NEW BLOCKER B4) — U7 keyboard tab order for variable slot counts is unspecified. For 10-slot Dredge, no tab order is defined. "Left-to-right by FrameLayoutSO.Slots index" is the only reasonable interpretation but is not written.
5. HIGH (Review 4 REC-3) — U2 enemy slot reveal specifies showing Armor slot HP from turn 1, but does not specify how the redirect relationship is communicated. RedirectsToSlotId is data-model only; no arrow, badge, or label spec exists for "this Armor protects hull_0."
6. MEDIUM (Review 4 REC-5) — HudAnchor canonical coordinates not locked. GDD gives one example (0.50, 0.80) but no full table of expected anchor positions for small_frame slots. Risk of inconsistent art positioning across layouts.
7. ACCESSIBILITY (OPEN from Review 2) — U8 covers DamageState color independence but does not address Armor-exposed state specifically. The colorblind table in combat-hud.md §7 does not include the exposed state distinction.
8. ACCESSIBILITY (OPEN from Review 2) — Screen-reader announce grammar in combat-hud.md does not include OnArmorExposed event or redirect consequence.
9. STALE (Review 2 item 1, still open) — Combat HUD GDD still specifies "4-subsystem portrait" in 6+ locations. Must be updated before HUD implementation.
10. STALE (Review 2 item 2, still open) — No layout strategy specified for 8–10 slot counts in Combat HUD GDD. U2 punts; Combat HUD GDD has not resolved it.

---
name: V&P Mechanics GDD UX Gaps
description: UX gaps from adversarial reviews of vehicle-and-part-mechanics.md; updated 2026-05-21 after R6 re-review (Pass 5).
type: project
---

Adversarial review of `design/gdd/vehicle-and-part-mechanics.md` completed across five passes.

**Why:** GDD is in Draft (In Review). UX sign-off is a gate before Workshop inspector implementation.

**How to apply:** Do not allow UI programmer to begin Workshop inspector implementation until all BLOCKING findings are resolved. Track W2 re-review findings (R2 column) as separate items.

---

## Pass 1 Findings (2026-05-19) — original 10 gaps

Gaps 2 and 3 resolved in W2 (E.9 and E.10 added). Gaps 1, 4, 5, 6, 7, 8, 9, 10 remain open.

**Gap 1 — BLOCKER (C.3):** Offline slot Workshop tooltip reads "Destroyed. Repair for [cost] Scrap." — directly contradicts F-VPM3 (no Scrap repair exists). Fix: rewrite to describe actual repair paths.

**Gap 4 — HIGH (C.3/C.4):** "Scrap — 8 Scrap" label uses currency name as verb and noun simultaneously (Stroop interference). Suggested: "Salvage (+8 Scrap)." Flag to game-designer and writer.

**Gap 5 — HIGH (E.3):** Confirm dialog is referenced 3+ times but never fully specced. Missing: visual form, button layout, default focused button, keyboard dismiss key, gamepad dismiss, tab order, irreversibility signal.

**Gap 6 — HIGH (C.6):** "In sequence within the same frame" is self-contradictory. Fix: separate (a) atomic state commit, (b) same-Update-tick event dispatch, (c) animations span multiple frames.

**Gap 7 — HIGH (C.3):** "Repair option" at Workshop for non-Offline slots implies an interactive control that calls a non-existent verb. Fix: rename to HP display / condition readout.

**Gap 8 — MEDIUM (C.3/C.4/E.3):** Touch vocabulary ("tap to install," "tapping Remove," "tap-outside") on PC/Steam-primary platform.

**Gap 9 — MEDIUM (C.4):** Install flow preview depth unspecced for Slay the Spire audience (full card text, stat comparison format).

**Gap 10 — LOW (C.5):** No guidance on pre-gate "at-risk" warning states; defer to EA scope note needed.

---

## Pass 2 Findings (2026-05-20) — W2 re-review, 13 new/evolved findings

**Finding 1 — BLOCKING (C.3):** Gap 7 upgraded to BLOCKING. "Repair option" for Healthy/Degraded/Critical slots still present in C.3 line 93 after W2. If implemented, will wire a UI control to a non-existent Scrap-repair verb. Fix: replace "repair option" with informational HP/condition display.

**Finding 2 — BLOCKING (AC-VPM04):** AC tests inverted behavior — expects card to stay greyed in hand (old spec). Current C.2/C.5 spec moves card to Offline zone entirely; card is absent from hand and does not count toward hand size. If QA runs this AC against a correct implementation it will fail. Rewrite AC-VPM04 to match C.2/C.5.

**Finding 3 — BLOCKING (E.9):** "No playable cards — passing turn" is ambiguous with normal empty-hand state (Slay the Spire players have trained mental model: empty hand = reshuffle incoming). Banner text must communicate structural cause. Also: banner duration and dismissal behavior are unspecced — UI programmer has no timing anchor.

**Finding 4 — BLOCKING (E.3):** Confirm dialog "tap-outside" dismiss creates click-through hazard on KB+M (dismiss click propagates to underlying Workshop inspector). Input blocking behavior unspecced. Keyboard confirm key and dismiss key entirely absent. E.3 still covers only dismissal, not full dialog spec — third review pass with this gap open. ESCALATED TO BLOCKING.

**Finding 5 — BLOCKING (C.2/C.3):** "3 cards offline" HUD count has no placement spec. "HUD slot anchor" is undefined. UI programmer will invent placement; art director cannot spec visual treatment. Accessibility risk at small HUD sizes.

**Finding 6 — HIGH (C.3):** Offline slot tooltip omits node-event repair path (F-VPM3 lists it as valid). Tooltip also creates false confidence: tells player to use Repair cards without indicating whether they have any. Suggested copy: "[Part name] — Destroyed. Remove to free this slot. Repair via Repair cards in combat or at a Repair node."

**Finding 7 — HIGH (C.3):** Hidden install button on occupied slots teaches nothing. Remove-then-Install upgrade path is invisible. Greyed button with tooltip "Remove existing part to install a new one" is minimum acceptable spec. Better: "Replace" flow as single action.

**Finding 8 — HIGH (C.6):** Gap 6 still unresolved after W2. Third review pass. "In sequence within the same frame" still self-contradictory.

**Finding 9 — HIGH (C.3/C.4):** "Single-button confirm dialog" ambiguous: one button total (Confirm only, no labeled Cancel) vs. one primary CTA (Confirm + Cancel). Confirm-only violates Nielsen heuristic 5 for irreversible actions. Recommendation: two-button with Cancel as default keyboard focus (prevents accidental Enter-key confirmation).

**Finding 10 — ADVISORY (E.9):** No spec for repeated forced-pass turns. Per-turn banner is noisy; persistent HUD state ("Systems Offline" indicator) is a better pattern for a sustained condition.

**Finding 11 — ADVISORY (C.1):** DamageState table mixes HUD-layer and card-layer signals in the same cell without layer labeling. Ambiguous: does card wear texture appear in Workshop inspector or only in combat HUD?

**Finding 12 — ADVISORY (AC-VPM06):** Tests an unreachable state — Offline cards are absent from hand per C.5, so "player attempts to play the card" cannot occur. Replace with an AC testing Offline zone presence and HUD count display.

**Finding 13 — ADVISORY (C.3/C.4/E.3):** Gap 8 (touch verbs) still unresolved after W2. "Tapping" appears in C.3 and C.4 in addition to the previously-flagged "tap-outside" in E.3.

---

## Pass 3 Findings (2026-05-20) — Re-review for APPROVED status (7 questions scoped)

**VERDICT: NOT APPROVED.** R2 cited fixes are present but incomplete. 4 new blockers identified.

**B-1 — BLOCKING (C.4):** Offline-part stat delta compares against effective-zero (EffectiveArmorContribution=0 while Offline), not base stats. "Armor: 0 → 5" misleads player into reading a neutral swap as an upgrade. Fix: when current occupant is Offline, label the zero baseline explicitly ("Armor: 0 (Destroyed) → 5") or show Destroyed part's base stats in a separate column.

**B-2 — BLOCKING (C.4):** Zero-delta field omission (R2 cited fix — partially resolved, new failure mode). Spec now defines stat preview format, but omitting zero-delta fields hides stat identity. Slay the Spire audience expects full stat card, not a filtered delta. Fix: Option A — show zero-delta fields with "=" indicator. Option B — show all fields, highlight changes as a layer on top.

**B-3 — BLOCKING (C.3):** Healthy/Critical occupied slot tooltip has no affordance for Remove-to-upgrade path. Full-vehicle player sees zero install affordance anywhere. Minimum fix: add secondary tooltip line "Remove to swap for a different part." Better: "Replace" single-action flow.

**B-4 — BLOCKING (C.6/banner):** Forced-pass banner input-blocking behavior unspecified; repeated-banner loop unhandled (all-Offline scenario); "All systems offline" copy is vague about cause and player action path. Fix: specify input blocking; add persistent HUD indicator after first banner occurrence; rewrite copy to name cause and link to repair action.

**R-1 — RECOMMENDED (E.3):** Enter = Salvage inverts destructive-dialog keyboard convention. Enter must not be wired to destruction. Fix: remove Enter = Salvage, or make Cancel the default-focused element so Enter fires Cancel on open.

**R-2 — RECOMMENDED (C.3):** Offline Tray forward reference to hud.md is acceptable for deferral, but stub behavior during the interim period is unspecified. Add one sentence: "Until hud.md is authored, implement badge-only count on slot indicator; Tray is not built until hud.md signs off."

**R-3 — RECOMMENDED (E.3):** Confirm dialog body copy is tonally flat vs. "Vehicle as Character" pillar. Change "This part" to "[Part name]" in the loss line — zero scope cost, pillar alignment improvement.

---

## Pass 4 Findings (R5 re-review, 2026-05-20) — W6 applied

**VERDICT: NOT APPROVED.** 3 new/carried blockers, 6 highs.

**P4-BLOCK-1 — BLOCKING (C.4 step 2):** "Player taps Remove" — touch vocabulary, fourth pass. W6 fixed C.3 but not C.4 step 2. Fix: "selects." Author must run document-wide search for "tap" before R6.

**P4-BLOCK-2 — BLOCKING (C.3 slot indicator):** "Destroyed — repair to restore" tooltip implies Workshop has repair affordance. Workshop has none. New instance separate from prior Gap 1/Finding 6 which addressed the Workshop inspector tooltip. Fix: rewrite to name consequence not repair location.

**P4-BLOCK-3 — BLOCKING (E.3):** Click-outside dismiss has no input-capture spec; click-through to Workshop inspector is possible. Third pass with this gap. W6 fixed Enter-key and Cancel-focus but did not add propagation specification. Fix: add "The dismissing click is consumed and does not propagate to the inspector."

**P4-HIGH-1 — HIGH (C.4 stat preview):** "Armor: 12 = 12" uses "=" not "→"; deviates from genre convention (Slay the Spire, Hades, Balatro all use "→" for zero-delta). Ambiguous read as equality check. Fix: replace "=" with "→" or adopt B-2 Option B.

**P4-HIGH-2 — HIGH (C.3 HUD Tray):** Interim badge-only spec has no gate condition or milestone trigger. Tray may never be built. Escalated from ADVISORY (Pass 3 R-2). Fix: add one-line gate tying Tray to hud.md sign-off before Combat HUD enters implementation.

**P4-HIGH-3 — HIGH (E.9 / C.6):** 1.5s forced-pass input block is a magic number — not in Tuning Knobs, no source cited. Will be hardcoded by UI programmer. Fix: move `ForcedPassBannerDurationSecs` to Tuning Knobs (range 1.0–2.5s); add minimum-display-then-dismissable pattern.

**P4-HIGH-4 — HIGH (E.9):** No persistent HUD indicator for sustained all-Offline condition. Between banner appearances the HUD gives no "you are trapped" signal. Escalated from ADVISORY (Pass 2 Finding 10). Fix: specify a persistent status label that persists until at least one slot is repaired.

**P4-HIGH-5 — HIGH (C.3):** "Remove to swap for a different part" secondary tooltip is hover-only; gamepad players cannot trigger hover. Full-vehicle gamepad player sees zero install affordances anywhere. Fix: promote to always-visible sub-label in focused-slot inspector panel when slot is occupied.

**P4-HIGH-6 — HIGH (E.3):** Body copy "all cards it granted" omits Offline zone cards. Player cannot assess full loss scope. Fix: rewrite to surface Offline count: "all [N] cards it granted — including [M] currently removed from play." N count required as live data query at dialog-open.

---

## Blocking gate summary (as of 2026-05-20)

Do NOT allow Workshop inspector implementation until ALL of these are resolved:
- Pass 1: Gap 1, Gap 5, Gap 6, Gap 7
- Pass 2: Finding 1, Finding 2, Finding 3, Finding 4, Finding 5
- Pass 3: B-1, B-2, B-3, B-4
- Pass 4: P4-BLOCK-1, P4-BLOCK-2, P4-BLOCK-3

Draft cannot be signed off until all blockers above are resolved. Highs P4-HIGH-1 through P4-HIGH-6 must be resolved before Workshop inspector implementation begins.

---

## Pass 5 Findings (R6 re-review, 2026-05-21) — W7 applied

**VERDICT: NOT APPROVED.** W7 closed majority of R5 blockers but 6 blockers remain open.

**R6-Q2 — BLOCKING (C.3 Workshop tooltip):** W7 fixed the slot indicator tooltip (P4-BLOCK-2) by naming "Repair cards in combat" as recovery path, but this same copy now appears in the Workshop inspector tooltip — a context collapse. Workshop is non-combat; naming "Repair cards in combat" as an option while the player is at a Workshop reads as an unavailable action. Fix: split into two context-specific copy strings — one for combat HUD, one for Workshop inspector.

**R6-Q3 / P4-HIGH-5 — BLOCKING (C.3 Healthy/Critical hint):** "Remove to swap for a different part" copy is present but the spec does not confirm it is always-visible in the focused-slot inspector panel (vs. hover-only tooltip). Gamepad players cannot hover. P4-HIGH-5 not confirmed closed. Fix: explicitly state this is a persistent inline label in the focused-slot panel, not a hover tooltip.

**R6-Q4 — BLOCKING (C.3 Hidden install button):** Install button is still HIDDEN on occupied slots. This was B-3/Finding 7 core issue — never reversed. Hidden = "concept does not exist here." Greyed = "concept exists, blocked." Fix: change to greyed button with inline "Remove existing part to install a new one" tooltip.

**R6-Q6 / P4-HIGH-2 — BLOCKING (C.3 Offline Tray deferral):** Still reads "deferred to design/ux/hud.md (not yet authored)" with no gate condition or interim behavior written. P4-HIGH-2 not closed. Fix: add two sentences — interim badge-only behavior and gate condition tying Tray to hud.md sign-off before Combat HUD implementation.

**R6-Q7 — BLOCKING (E.9 relic/banner ordering):** Spec says relics fire before banner but does not specify all-Offline check is evaluated AFTER relic resolution. If a relic repairs a slot, banner fires anyway — passing the player's turn despite a now-valid game state. Fix: add "All-Offline check is evaluated after all on-start-of-turn triggers resolve. If at least one slot is online after trigger resolution, banner does not appear."

**R6-C1 / P4-BLOCK-3 — BLOCKING (E.3 click-through):** E.3 specifies click-outside=Cancel but does not state the click is consumed (non-propagating). W7 was supposed to fix this; sentence still absent. Fix: add "The dismissing click is consumed and does not propagate to the inspector."

**R6-Q1 — ADVISORY (C.3 HUD tooltip):** "restore via Repair cards in combat or a Repair node" — naming both options in combat is acceptable; order is correct (immediate action first). No action required.

**R6-Q5 — ADVISORY (C.4 Zero-delta stat lines):** Zero-delta display behavior should be explicitly written. Recommend: show zero-delta lines with grey styling and → symbol. Confirm this is written in C.4.

**R6-C2 / P4-HIGH-6 — HIGH (E.3 body copy):** "all cards it granted" still does not surface Offline zone card count. Player cannot assess full loss scope. Still open from R5.

---

## Blocking gate summary (as of 2026-05-21)

Open blockers for R6: R6-Q2, R6-Q3/P4-HIGH-5, R6-Q4, R6-Q6/P4-HIGH-2, R6-Q7, R6-C1/P4-BLOCK-3
Open high: R6-C2/P4-HIGH-6
Open advisory: R6-Q1 (no action), R6-Q5 (confirm spec text)

---

## Pass 6 Findings (R7 re-review, 2026-05-21) — W8 applied

**VERDICT: NOT APPROVED.** W8 closed 2 of 6 R6 blockers. 4 blockers remain open. 5 highs added.

**Confirmed closed this pass:**
- R6-C1/P4-BLOCK-3 — click-through prevention sentence added to E.3 (closed)
- R6-Q7 — relic-repair-prevents-forced-pass evaluation order added to E.9 (closed)

**R7-B — BLOCKING (R6-Q6/P4-HIGH-2 still open, C.3 HUD Offline Tray):** "Collapses to badge count when no cards are Offline" is ambiguous — does zero-state render a "0" badge or become invisible? Still no interim behavior spec and no gate condition tying Tray to hud.md sign-off. Fix: specify zero-state visibility (recommend invisible, not "0"), add interim badge-only behavior sentence, add gate condition.

**R7-F — BLOCKING (R6-Q2 still open, C.3 Workshop inspector tooltip):** W8 updated Chopshop references but did not split context-specific copy strings. Workshop inspector Offline tooltip still names "combat Repair cards" while player is at a non-combat node — a context collapse. Fix: two separate copy strings: (1) combat HUD: "Destroyed — restore via Repair cards or a Chopshop"; (2) Workshop inspector: "[Part name] — Destroyed. Remove to free this slot. Restore via Repair cards in combat or at a Chopshop."

**R7-G — BLOCKING (R6-Q3/P4-HIGH-5 still open, C.3 Healthy/Critical hint):** W8 did not touch the Healthy/Critical inspector hint. C.3 still says "secondary tooltip line" — hover-only. Gamepad players cannot hover. Fix: change "secondary tooltip line" to "always-visible secondary label in the focused-slot inspector panel."

**R7-H — BLOCKING (R6-Q4/B-3/Finding 7 still open, C.3 install button):** W8 did not change install button affordance on occupied slots. Still "hidden, not greyed." Fix: greyed button with inline "Remove existing part to install a new one" tooltip. Note: AC-VPM09 tests for hidden-button; if reversed to greyed, AC-VPM09 must be rewritten.

**R7-A — ADVISORY (C.4/C.3 install button state precedence):** Implicit reasoning that disabled state fires only on empty slots (not occupied) is not written. No implementer can derive this without reading both C.3 and C.4 together. Fix: one sentence in C.4 step 5: "The disabled state applies only when the slot is empty — occupied slots never reach this state because the install button is hidden per C.3."

**R7-C — HIGH (C.6 per-card Offline flash):** Flash spec says "each card's last screen position" but cards in draw pile or discard have no screen position in the hand area. Spec gives no guidance on flash behavior for non-hand cards. Fix: add clause — recommend Option A (flash only cards currently rendered in the hand area; zone-abstract cards produce no per-card flash; slot-level badge count is the zone-agnostic signal).

**R7-D — HIGH (E.9 banner/enemy-turn ordering):** Spec does not state whether the enemy's turn starts during or after the 1.5-second banner. "On damage received" relics "remain eligible during forced-pass turns" is ambiguous. Fix: add one sentence: "The enemy's turn begins after the 1.5-second banner completes — no enemy action resolves while the banner is displayed."

**R7-E — HIGH (E.3 gamepad focus indicator):** E.3 specifies B/Circle is default-focused on open but does not require a visible focus ring or highlight. Without a legible focus indicator, the default-focus spec is unenforceable. Fix: one sentence requiring clearly distinguishable focus state legible at 150–200cm viewing distance; defer visual treatment to UX spec.

**R7-J — HIGH (E.3 Remove dialog body copy):** "all cards it granted will be permanently removed" still does not surface Offline zone card count. Carried from R6-C2/P4-HIGH-6. Fix: "all [N] cards it granted — including [M] currently offline." N count requires live data query at dialog-open.

**R7-K — ADVISORY (C.4 zero-delta separator):** Zero-delta format is `Armor: 12 = 12` while changed stat is `Armor: 12 → 17` — two different separators in the same preview context. Fix: standardize to `→` throughout; style zero-delta lines grey to distinguish from changed lines.

---

## Blocking gate summary (as of 2026-05-21, after R7)

Open blockers: R7-B (Tray zero-state), R7-F (tooltip context collapse), R7-G (hover-only hint), R7-H (hidden install button)
Open highs: R7-C (Offline flash scope), R7-D (banner/enemy ordering), R7-E (focus indicator), R7-J (Remove dialog card count), R7-K (separator inconsistency)
Open advisory: R7-A (install state precedence implicit), R7-K (if not promoted)

---

## Pass 7 Findings (R8 re-review, 2026-05-21) — W9 applied

**VERDICT: NOT APPROVED.** W9 closed R7-B, R7-F, R7-G. R7-H deferred (advisory). 2 new blockers introduced by W9 fixes.

**Confirmed closed this pass:**
- R7-B — Tray zero-state=invisible, interim badge-only, gate condition all present in C.3. Closed.
- R7-F — Two context-specific copy strings now present in C.3 (Workshop vs. Combat HUD). Closed.
- R7-G — "Always-visible secondary label in focused-slot inspector panel" text present; "not a hover tooltip" explicitly stated. Closed.
- R7-D — "After the 1.5 seconds, the enemy's turn sequence begins normally." Present in E.9. Closed.

**R8-A — BLOCKING (C.3 Healthy/Critical hint):** W9 fix for R7-G introduced "focused" as the trigger condition for the always-visible secondary label, but "focused" is undefined. Does it mean D-pad traversal (transient focus ring), or confirmed slot selection (A/Cross pressed)? A programmer who implements D-pad hover as "focused" satisfies letter but breaks gamepad compliance intent. Fix: add one defining sentence — e.g. "For purposes of this label, 'focused' means the slot is the currently selected entry in the inspector panel (A/Cross on gamepad; slot clicked or Tab-navigated to on KB+M). D-pad traversal alone does not trigger the label."

**R8-B — BLOCKING (C.4 stat preview — multi-stat Offline case):** Offline stat preview rule gives one example: "Armor: 12 (Destroyed) → 17." Does not specify: (a) whether "(Destroyed)" labels each stat line or the block level when a part has multiple stats; (b) how to handle a stat present in the candidate but absent in the Offline part (absolute value or delta from zero?). Fix: extend the Offline rule with two sentences covering multi-stat layout and the "candidate-only stat" case.

**R8-C — RECOMMENDED (C.3 badge timing):** Badge disappears at data-layer instant (state transitions are instantaneous per C.1); Heavy repair VFX resolve over 0.5–2s (C.6). Badge vanishes before visual restoration is complete. Add one sentence: badge updates are data-driven, not animation-driven; view-layer may optionally delay badge fade for VFX sync, deferred to hud.md.

**R8-D — RECOMMENDED (E.3 gamepad button mapping):** A/Cross=confirm, B/Circle=cancel is assumed; no cross-project gamepad canon exists. Add cross-reference note: verify against design/ux/interaction-patterns.md when authored; update E.3 if that doc assigns these buttons differently.

**R8-E — RECOMMENDED (E.9 forced-pass banner):** (a) ForcedPassBannerDurationSecs still hardcoded in prose — not in Tuning Knobs table. P4-HIGH-3 has survived R4 through R8 without resolution; if implementation begins before this is added to Tuning Knobs it will be hardcoded permanently. (b) Queued input behavior during 1.5s block unspecified. (c) System pause menu (Esc) exemption status unspecified.

**R8-F — ADVISORY (C.3 Offline Workshop slot icon):** Wrench icon on non-repairable Offline slot sends pre-attentive repair affordance signal — contradicts copy ("no repair at this node") and F-VPM3. Flag to art-director; suggest replacing with damaged/offline icon (cracked gear, X-overlay).

**R8-G — ADVISORY (C.4 zero-delta separator):** R7-K still open. "=" separator for zero-delta vs. "→" for changed stat — inconsistency survives W9.

**R8-I — ADVISORY (E.9 "on damage received" relic ordering):** With enemy acting after banner, "on damage received" relics cannot fire during the input-blocked period — but the text says these relics "remain eligible during forced-pass turns," which reads as contradictory. Add one confirming sentence: these relics fire in the enemy-turn phase, after the banner has dismissed.

---

## Blocking gate summary (as of 2026-05-21, after R8)

Open blockers: R8-A ("focused" undefined — C.3), R8-B (multi-stat Offline preview — C.4), R7-H (hidden install button — deferred advisory)
Open recommended: R8-C (badge timing), R8-D (gamepad canon gap), R8-E (ForcedPassBannerDurationSecs not in Tuning Knobs — P4-HIGH-3 escalated)
Open advisory: R8-F (wrench icon affordance mismatch), R8-G (zero-delta separator, R7-K carried), R8-I (relic ordering language), R7-C (Offline flash scope — carried from R7, not re-examined in R8), R7-E (focus indicator — carried), R7-J (Remove dialog card count — carried), R7-A (install state precedence — carried)

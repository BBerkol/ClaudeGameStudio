# Review Log — vehicle-and-part-mechanics.md

## Revision Pass — 2026-05-21 — W9.1 Applied (R8 APPROVED — no re-review)
Scope signal: L
Workstreams applied: W9.1 (16 precision fixes — no design decisions reopened)
Blockers addressed: 16 | Advisory: 0 new
Summary: All 16 W9.1 bundled fixes applied per CD approval directive. Key fixes: C.1 clamp direction corrected — upward to `Ceiling(100/MaxHp)`, not downward (spec error caught by systems-designer and qa-lead); MaxHp=1 edge case added to C.1 (Ceiling(100/1)=100 conflicts with per-field max=99 — distinct rejection error now specified); AC-VPM-H pass condition corrected (N=50, not N≤48 — prior assertion was an impossible condition for MaxHp=2); ScrapRefundRate floor invariant note corrected (0.25→0.20 — cited wrong boundary value; Rare case at 0.20 added); entities.yaml `min: 0.20` added to ScrapRefundRate entry; audio active-Critical count owner specified in part-state model (IVehicleView property, not audio-manager shadow count); same-tick Critical+Destroyed edge case specified (net delta zero, loop does not start); Light-tier suppression clarified — applies to audio channel only (visual particle components always fire regardless of higher-tier audio); `CriticalLoopExitCrossfadeMs` tuning knob added (250ms placeholder, must be configurable field, TBD W3g); "focused" definition added to C.3 (traversal focus = D-pad/Tab focus ring, not confirmed selection); multi-stat Offline preview specified — block-level "(Destroyed)" label + "New from candidate:" subsection for absent-stat case; Pillar 5 cross-doc gate sentence added to Dependencies (implementation not gated on node-encounter.md update but Pillar 5 integration is); AC-VPM-A event source named (IVehicleEventBus per architecture §4.3) + forced-pass input rejection assertions added (card play/relic activation/TryEndTurn each tested separately); AC-VPM-G injectable IFrameClock contract replaces ±5ms tolerance assertion (deterministic, NUnit-compatible); AC-VPM-B affirmative assertion added (card X costs exactly BaseCost+CriticalEnergySurcharge before testing negative case); `ForcedPassBannerDurationSecs` added to Tuning Knobs (1.5s, inputs discarded not buffered, Esc exempt). Status header updated to Approved.
Prior verdict: R8 APPROVED (2026-05-21 — W9.1 applied same session)

---

## Review — 2026-05-21 — Verdict: APPROVED (R8)
Scope signal: L
Specialists: game-designer, systems-designer, economy-designer, ux-designer, qa-lead, audio-director, creative-director
Blocking items: 16 (all resolved as W9.1 bundled fixes — no re-review) | Recommended: 9 | Advisory: 7
Summary: R8 found no experience-layer concerns — only spec precision gaps. Three categories of blockers: (1) clamp math error in C.1 cross-field guard (direction backwards + AC-VPM-H impossible pass condition + MaxHp=1 edge case); (2) ACs missing precision — AC-VPM-G timing model ambiguous, AC-VPM-A event source unnamed + forced-pass rejection untested, AC-VPM-B affirmative assertion missing; (3) audio architecture gaps — active-Critical count owner unspecified, Light-tier suppression channel ambiguous, crossfade duration must be configurable field; (4) UX precision gaps introduced by W9 — "focused" undefined, multi-stat Offline preview unspecified. Creative-director verdict: "None of the R8 blockers change a single design decision. All are corrections to spec precision around decisions already made and correct. Revision iteration cost has crossed below the marginal cost of delaying prototype. APPROVED — apply 16 bundled fixes as W9.1, no re-review." CD spot-check items: clamp math (fixes 1–3) and 'focused' definition (fix 9).
Prior verdict resolved: Yes — R7 NEEDS REVISION (9 blockers) → R8 APPROVED (all R7 blockers closed in W9; R8 precision gaps resolved in W9.1 same session)

---

## Revision Pass — 2026-05-21 — W9 Applied (R8 re-review pending)
Scope signal: L
Workstreams applied: W9 (precision + economy + UX + QA hardening)
Blockers addressed: 9 | Advisory: 3 PVH added
Summary: All 9 R7 blockers addressed. Key fixes: C.1 cross-field OnValidate guard remediation changed from log-only to auto-clamp CriticalThresholdPct (saves SO in valid state; no designer acknowledgement needed; AC-VPM-H added); CriticalEnergySurcharge safe range capped 0–1 (was 0–2, removing untestable "validate against card pool" note); ScrapRefundRate lower bound added [0.20,0.99] (was [0.0,0.99] — lower bound protects Pillar 4 pivot agency; both bounds tested in AC-VPM-E); E.5 Rare-to-Rare pivot worked examples added (Scenario A = 98 Scrap net, Scenario B = 122 Scrap net — anchors Pillar 4 commitment claim); E.9 repair-card death spiral declared valid consequence state (non-slot repair sources not guaranteed; player strategy, not design guarantee); C.3 HUD Offline Tray zero-state=invisible + interim badge-only behavior + gate condition (Tray blocked until hud.md signed off); C.3 Workshop inspector Offline tooltip split into context-specific copy (Workshop copy removes "combat" reference from non-combat screen); C.3 Healthy/Critical inspector hint promoted from hover-only tooltip to always-visible secondary label (gamepad compliance); AC-VPM04 assertion (3) BLOCKED labeled explicitly; AC-VPM09 pending routing to unity-ui-specialist noted; AC-VPM-A repair-relic ordering assertion added; AC-VPM-E expanded to test both bounds; new ACs: AC-VPM-F (E.3 click-through), AC-VPM-G (Light-tier stagger), AC-VPM-H (cross-field auto-clamp); PVH-1/2/3 added (triage feel, Workshop register, early-trial forgivingness — playtest-resolved). Creative-director verdict: game-designer B1–B3 reclassified advisory (PVH); UX R7-H (hidden vs. greyed button) deferred to post-prototype UX ticket; audio W3g items advisory for GDD gate.
Prior verdict: R7 NEEDS REVISION (2026-05-21)

---

## Review — 2026-05-21 — Verdict: NEEDS REVISION (R7)
Scope signal: L
Specialists: game-designer, systems-designer, economy-designer, ux-designer, qa-lead, audio-director, creative-director
Blocking items: 9 | Recommended: 10+ | Advisory: tracked-but-not-blocking
Summary: W8 closed all 8 R6 blockers cleanly. R7 found 9 new blockers spanning four categories. Top findings: economy — session-state "54 Scrap" Rare pivot figure incorrect (actual 98–122 Scrap), GDD had no worked example; ScrapRefundRate had no lower bound (allowing economically catastrophic ScrapRefundRate=0); systems — C.1 cross-field guard specified log-only with no auto-clamp, leaving invalid SOs saveable; CriticalEnergySurcharge=2 validation was non-testable ("validate against card pool" with no owner/gate); game-designer — repair-card death spiral escape valve hand-waved ("may still exist"); UX — four R6 blockers survived W8 (Tray zero-state, Workshop tooltip context collapse, hover-only hint, hidden install button); QA — W8 mechanics had zero AC coverage. Creative-director adjudicated: game-designer B1–B3 reclassified as PVH (experience-layer questions for prototype); UX R7-H deferred post-prototype; audio W3g items advisory for GDD gate. "W9 should be the last revision. If R8 surfaces new experience-layer concerns instead of spec gaps, approve and move to prototype."
Prior verdict resolved: No (R6 NEEDS REVISION → R7 NEEDS REVISION — all 8 R6 blockers closed, 9 new precision/spec items)

---

## Revision Pass — 2026-05-21 — W8 Applied (R7 re-review pending)
Scope signal: L
Workstreams applied: W8 (precision hardening + cross-doc obligation)
Blockers addressed: 8 | Recommended: 4 applied
Summary: All 8 R6 blockers addressed. Key fixes: C.1 cross-field OnValidate guard added (Floor(MaxHp × CriticalThresholdPct / 100) ≥ 1 — catches MaxHp=1 + CriticalThresholdPct=99 combination that silently eliminates Critical window); Player Fantasy and F-VPM1 design-intent "45–55%" updated to "45–60%" (heavy_frame 7-slot installs approach 60% surcharge); C.3 slot indicator and Workshop inspector both updated "Repair node" → "Chopshop" (node-type specificity); C.6 Slot Destroyed table row inline sonic brief added (metallic impact, NOT salvage family, stinger 0.5–2s+tail, W3g for full contract); C.6 Light-tier stagger rule added (20–30ms per event from same trigger source — same-source simultaneous Light events stagger, cross-source may coincide); E.9 turn-start resolution order paragraph added (relics resolve before draw phase, re-evaluate all-Offline after relic resolution — repair-relic prevents forced-pass sub-case added); E.3 click-through prevention sentence added; Tuning Knobs CriticalEnergySurcharge "locks out" corrected to "reduces to single play." Advisory: AC-VPM04 BLOCKED set clarified (1a/1b split); AC-VPM-A repair-relic sub-case; AC-VPM-E CI gate promotion note. Cross-doc obligation: scrap-economy.md D.3 Repair verb "Degraded" → "Critical" with clarifying note (Degraded is visual-only, not a DamageState).
Prior verdict: R6 NEEDS REVISION (2026-05-21)

---

## Review — 2026-05-21 — Verdict: NEEDS REVISION (R6)
Scope signal: L
Specialists: game-designer, systems-designer, economy-designer, ux-designer, qa-lead, audio-director, creative-director
Blocking items: 8 | Recommended: 4
Summary: W7 was thorough and addressed all 11 R5 blockers. R6 found 8 new/previously-missed blockers, all precision-level. Top findings: systems-designer identified a critical-window collapse edge case (MaxHp=1 + CriticalThresholdPct=99 — individually valid per-field, combination makes Critical mechanically unreachable); Player Fantasy "45–55%" was factually wrong for heavy_frame (7 slots → ~60% last-install surcharge); C.3 used "a Repair node" instead of the specific node-type "Chopshop"; C.6 Slot Destroyed table row still contained the unfilled "positive sonic direction required in W3g" placeholder; audio-director flagged Light-tier exempt from 2-cap but no simultaneity upper bound or stagger spec; E.9 forced-pass check could race start-of-turn relics (ordering undefined); E.3 dialog missing click-through prevention. Cross-doc obligation confirmed: scrap-economy.md D.3 Repair verb still referenced "Degraded" state (non-existent mechanical state — visual-only). Creative-director verdict: all findings are precision issues; document architecture remains sound. W8 should close all items cleanly.
Prior verdict resolved: No (R5 NEEDS REVISION → R6 NEEDS REVISION — all prior blockers closed, 8 new precision items)

---

## Revision Pass — 2026-05-20 — W7 Applied (R6 re-review pending)
Scope signal: L
Workstreams applied: W7 (precision hardening + sibling-doc authority resolution)
Blockers addressed: 11 | Recommended: 3 applied
Summary: All 11 R5 blockers addressed. Key fixes: C.4 "taps Remove" (fifth appearance — corrected); C.3 Destroyed tooltip repair path corrected (node-type-agnostic phrasing); F-VPM1 float-cast notation added (matches F-VPM4 / AC-VPM-D pattern); F-VPM4 armor cap placeholder resolved (card-combat-system.md F-CC1 unbounded reference + provisional authoring guidance); C.6 Critical loop exit rule for Destroyed slots specified (loop terminates on zero active-Critical count by any combination of Healthy transitions and Destructions); C.6 same-tier cap scoped to Medium/Heavy — Light tier explicitly exempt (required for per-card Offline flash spec); AC-VPM-D MaxHp=0 guard assertion rewritten (tests guard path, not formula output — avoids C# NaN/max(0,NaN)=NaN trap); AC-VPM05 assertion (2) BLOCKED annotation; AC-VPM01 float-cast trap test added. Sibling-doc edit: scrap-economy.md D.6 tenure formula stripped and replaced with static redirect to vehicle-and-part-mechanics.md F-VPM2; D.1 TenureDecayRate/TenureMinMultiplier marked deprecated; D.10 formula summary updated; AC-SE7b/7c marked superseded; CombatsSurvived retrofit marked removed. ScrapRefundRate registered in entities.yaml with hard max 0.99 and OnValidate guidance.
Prior verdict: R5 NEEDS REVISION (2026-05-20)

---

## Review — 2026-05-20 — Verdict: NEEDS REVISION (R5)
Scope signal: L
Specialists: game-designer, systems-designer, economy-designer, ux-designer, qa-lead, audio-director, creative-director
Blocking items: 11 | Recommended: 6
Summary: W6 was substantive — Critical loop polyphony committed, Part scrapped SFX brief authored, ScrapRefundRate OnValidate guard documented, C.3/C.4 contradiction resolved as future-path stub. But 11 blockers remain, including a W6-introduced regression (same-tier cap conflicted with per-card Offline flash spec). Top issues: C.4 "taps Remove" survived W6's targeted fix (C.3 was fixed but C.4 was missed — fifth consecutive review appearance); C.3 Destroyed tooltip "repair to restore" is node-type-ambiguous; F-VPM1 float-cast notation missing (established in F-VPM4 but never added to F-VPM1); F-VPM4 armor cap placeholder `§[armor cap section]` never filled; C.6 Critical loop exit rule for Destroyed slots (loop never terminates if slots reach 0 HP instead of transitioning Healthy); W6-added same-tier cap conflicts with existing per-card Offline flash spec (Light tier fix: exempt). scrap-economy.md D.6 tenure formula still present — CD held firm: "this doc does not get APPROVED while its sibling contradicts it."
Prior verdict resolved: No (R4 NEEDS REVISION → R5 NEEDS REVISION — 8 blockers closed, 11 new/recurring)

---

## Revision Pass — 2026-05-20 — W6 Applied (R5 re-review pending)
Scope signal: L
Workstreams applied: W6 (full read-through pass)
Blockers addressed: 8 | Recommended: 5 applied
Summary: Player Fantasy refund framing corrected (proportional escalation is from install cost, not refund); C.3/C.4 install-over-occupied resolved as future-path stub (C.3/E.1 hide install button on occupied slots — no C.4 dialog yet); "tapping" → "selecting" throughout; ScrapRefundRate OnValidate guard documented (clamp [0.0, 0.99] on owning SO); Critical loop polyphony committed (single shared loop — one loop regardless of slot count; begins on first Critical-entry, ends when last Critical slot exits); Part scrapped SFX brief authored (Chopshop/salvage family; deliberate-loss register); E.3 default focus Cancel; forced-pass audio repeat-turn specified; simultaneous-rule same-tier cap (max 2 concurrent) + Critical-entry suppression (only first-entry onset fires); audio spec W3g brief updated; AC-VPM-E added.
Prior verdict: R4 NEEDS REVISION (2026-05-20)

---

## Review — 2026-05-20 — Verdict: NEEDS REVISION (R4)
Scope signal: L
Specialists: game-designer, systems-designer, economy-designer, ux-designer, qa-lead, audio-director, creative-director
Blocking items: 8 | Recommended: 8
Summary: W5 was real progress — structural skeleton sound, R3 blockers genuinely addressed. But 8 blockers remain, three of which are recurring across reviews. Top issues: ScrapRefundRate has no OnValidate clamp (invariant breaks silently at ≥ 1.0); C.3/C.4 install-over-occupied contradiction survived three review passes (hidden install button vs. live warning spec); scrap-economy.md D.6 still carries tenure formula creating authority split; Player Fantasy "proportionally larger at high fill" framing is factually wrong (refund is flat, not fill-sensitive). Audio director and creative-director agreed: Critical loop polyphony architecture and Part scrapped SFX sonic identity are design decisions for this doc, not W3g details — W3g should inherit a constrained problem. Creative-director discipline note: W6 author must run full read-through, not a delta pass.
Prior verdict resolved: No (R3 NEEDS REVISION → R4 NEEDS REVISION — 20 blockers closed, 8 new/recurring)

---

## Revision Pass — 2026-05-20 — W5 Applied (R4 re-review pending)
Scope signal: L
Workstreams applied: W5 (static refund + precision hardening)
Blockers addressed: 20 | Recommended: 3 applied
Summary: Tenure system eliminated by user design decision — F-VPM2 completely rewritten as static formula (`max(1, Floor(InstallBaseCost[rarity] × ScrapRefundRate))`; Common=4/Uncommon=10/Rare=20 Scrap). Turn-budget compression from multi-Critical slots documented as intentional design with energy-boost rare rewards as the designed relief valve. C.4 stat preview fully respecced: empty=absolute values; Healthy/Critical=delta with zero-delta fields shown as "= 12"; Offline baseline labeled "Armor: 12 (Destroyed) → 17". Occupied-slot upgrade affordance added ("Remove to swap for a different part"). C.6 simultaneous audio rule clarified (only highest-tier events play; lower-tier suppressed same frame). Critical loop exit trigger specified; loop audio gated to W3g. F-VPM4 ArmorContribution≥0 OnValidate guard added. E.5 net Scrap updated to 6/11 Scrap per static formula. E.9 input blocking fully specced (post-draw check, 1.5s block, no stacking, Enrage ticks during All-Offline). ACs rewritten: AC-VPM02 static formula assertions; AC-VPM07/C refund values corrected (4 Scrap); AC-VPM04 DrawPile fallback removed — marked BLOCKED pending DrawPileCount exposure; AC-VPM-D added for F-VPM4 boundary coverage. scrap-economy.md D.6 flagged as required reverse update.
Prior verdict: R3 NEEDS REVISION (2026-05-20)

---

## Review — 2026-05-20 — Verdict: NEEDS REVISION (R3)
Scope signal: L
Specialists: game-designer, systems-designer, economy-designer, ux-designer, qa-lead, audio-director, creative-director
Blocking items: 20 | Recommended: 3
Summary: W4 tightening was substantively correct but 20 finer-grain precision blockers remained. Critical findings: tenure formula ZeroCombatMultiplier cliff (0→1 combat = 3×–4.5× refund jump creates perverse timing incentive — resolved by user design decision: eliminate tenure, use static refund); turn-budget compression from multi-Critical undocumented (resolved: intentional, energy-boost rewards as relief valve); stat preview Offline baseline misleading ("Armor: 0 → 5" reads as upgrade when 0 = Destroyed effective, not true base); occupied slot shows no upgrade affordance; simultaneous audio rule self-contradicting; AC-VPM04 DrawPile fallback is false-negative loophole; Player Fantasy accurate-multiplier claim needed. Creative-director: "20 items are precision and completeness issues — architecturally the document is sound. W5 should close these. R4 should be a clean pass."
Prior verdict resolved: No (R2 NEEDS REVISION → R3 NEEDS REVISION — lateral on blockers, forward on precision)

---

## Revision Pass — 2026-05-20 — W4 Applied (re-review pending)
Scope signal: L
Workstreams applied: W4 (focused tightening)
Blockers addressed: 13 | Recommended: 5 applied
Summary: Arithmetic error corrected ("~12→9 Scrap net" in F-VPM2/E.5/Tuning Knobs); totalSlots and MaxHp divide-by-zero guards documented; E.9 forced-pass trigger expanded to include discard pile; external disable added as gate 1 in C.5 gate composition (with precedence); install stat preview specified as delta vs. current loadout; Player Fantasy rewritten to acknowledge all-Offline as slow death by design; Critical surcharge triage worked example added; HUD Offline Tray forward reference added to C.3; four ACs reworked (VPM04 DrawPileCount fallback, VPM05 Offline zone in setup, VPM09 programmatic assertion, VPM-A event-driven setup); VPM-B negative case added; VPM-C placeholder refund computed. Audio blockers (B14–B18) confirmed as W3g forward dependency — not blocking this doc.
Prior verdict: R2 NEEDS REVISION (2026-05-20)

---

## Review — 2026-05-20 — Verdict: NEEDS REVISION
Scope signal: L
Specialists: game-designer, systems-designer, economy-designer, ux-designer, qa-lead, audio-director, creative-director
Blocking items: 13 | Recommended: 9
Summary: W3 revision substantially improved precision but left 13 blockers: arithmetic error in "~12 Scrap net" claim (actual: 9); two formula divide-by-zero guards missing (F-VPM1/F-VPM4); E.9 forced-pass missed discard reshuffle; external disable absent from gate composition; install stat preview undefined; Player Fantasy called all-Offline "triage" when it is intentional slow death; Critical surcharge lacked a worked triage example; HUD Offline Tray not specified (CD adjudicated: W2 Offline zone stays but card identity must be visible in a Tray); four ACs needed structural rework. Audio blockers (W3g) confirmed as production gates but ruled non-blocking for this doc's approval. Creative-director verdict: "one revision away from approval — tightening, not rethinking."
Prior verdict resolved: No (R1 MAJOR REVISION NEEDED → R2 NEEDS REVISION — step-forward)

---

## Revision Pass — 2026-05-20 — W3 Applied (re-review pending)
Scope signal: L
Workstreams applied: W3 (spec hardening)
Blockers addressed: 15 | Audio pass (W3g) pending
Summary: Two-state collapse applied (Degraded row removed; DegradedThresholdPct = visual-only); ZeroCombatMultiplier=0.20 audition floor added to F-VPM2 and E.5; Offline zone specified as data structure (per-slot List<CardId> OfflineCards on SlotStateDTO, unordered, bounded, serialized per ADR-0004); F-VPM4 Armor contribution formula added; installedCount reconciled to total non-Empty slots matching scrap-economy.md D.2; C.1/C.5 non-compounding contradiction resolved (per-card language, worked example); C.3/F-VPM3 repair option contradiction resolved; C.6 stale pre-W2 rows replaced; all 9 ACs rewritten against registered constants; 3 new ACs added (AC-VPM-A all-Offline, AC-VPM-B non-compounding, AC-VPM-C structural-drift run-end). Remaining: C.6 audio pass (tier parameters, Critical loop contract, bus priority) deferred to W3g.
Prior verdict: R1 MAJOR REVISION NEEDED (2026-05-20); W1+W2 applied (2026-05-20)

---

## Revision Pass — 2026-05-20 — W1+W2 Applied (re-review pending)
Scope signal: L
Workstreams applied: W1 (editorial), W2 (creative)
Blockers addressed: ~17 | W3 pending: AC rework, audio spec, UX dialog
Summary: W1 transferred formula authority to scrap-economy.md (F-VPM1→D.2, F-VPM2→D.6), clarified Workshop/Chopshop as distinct node types (F-VPM3), collapsed Discard to Salvage, fixed "Select to install" vocabulary and Offline tooltip. W2 applied creative-director hybrid arc: Degraded = sensory only (visual wear on cards); Critical = +1 energy surcharge non-compounding; Offline = cards move to Offline zone (excluded from draw pool, shuffled back on repair); E.9 forced-pass spec for all-Offline state; E.10 empty install loot pool; CriticalEnergySurcharge tuning knob added. Float cast requirement added to C.1 threshold comparisons. W3 (AC rework, audio tier parameters + Critical loop contract, UX dialog spec) to follow after re-review.
Prior verdict: R1 MAJOR REVISION NEEDED (2026-05-20)

---

## Review — 2026-05-20 — Verdict: MAJOR REVISION NEEDED
Scope signal: L
Specialists: game-designer, systems-designer, economy-designer, ux-designer, qa-lead, audio-director, creative-director
Blocking items: 14 | Recommended: 11
Summary: Three structural fractures found — (1) F-VPM1/F-VPM2 are obsolete pre-R6 formulas that should be deleted and replaced with cross-references to scrap-economy.md D.2/D.6; F-VPM3 directly contradicts scrap-economy.md D.3's TryRepair verb; (2) Player Fantasy promises "you feel damage in the cards at Degraded" but C.1 delivers only an amber indicator — creative-director ruled: keep the fantasy, revise C.1 with a hybrid sensory-at-Degraded / bounded-cost-at-Critical / lifecycle-at-Offline pattern; (3) greyed Offline cards counting toward hand size installs the same death spiral the design explicitly avoided at Degraded/Critical, and the all-Offline zero-playable-card state is completely unspecified. All 9 ACs need structural rework (POCO/UI split, float cast, API specs) and 2 S2-level coverage gaps exist (vehicle death, structural schema-drift). C.6 audio section is not production-actionable (no tier parameters, Critical state loop contract unspecified, bus priority rules missing). Revision path: 3 sequential workstreams — W1 editorial cleanup (formula authority, Workshop/Chopshop, Discard), W2 creative (damage arc hybrid, Offline hand-size, all-Offline state), W3 spec hardening (ACs, audio params, UX dialog).
Prior verdict resolved: N/A — First review

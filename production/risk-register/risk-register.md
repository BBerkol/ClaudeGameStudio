# Risk Register — Wasteland Run

**Last Updated**: 2026-04-24
**Maintained By**: producer (escalate to creative-director for design/tone risks, technical-director for engineering risks)
**Review Cadence**: reviewed at every phase gate and at the start of every sprint plan

This register is the authoritative log of known project risks carried forward from design phase into Technical Setup / Pre-Production / Production. Each entry has an owner, a trigger date, resolution criteria, and a status.

Scope rules:
- **Design-phase watchpoints (W-*)** — surfaced by `/review-all-gdds` and not blocking for gate closure, but must be actively tracked so they are not silently dropped.
- **Open accessibility/compliance gates (OQ-*)** — questions from GDDs that require specialist review at a defined later milestone.
- Operational/engineering risks that emerge during implementation should be added as R-* entries with the same structure.

---

## Status Legend

| Status | Meaning |
|--------|---------|
| **Open** | Known risk, not yet mitigated; monitoring against trigger date |
| **Monitoring** | Active telemetry/observation in place; awaiting signal |
| **Mitigated** | Resolution criteria met; kept in register for post-ship traceability |
| **Accepted** | Explicit decision to ship with the risk; documented rationale |
| **Deferred** | Formally pushed to a later milestone with an owning artifact |

---

## Active Risks

| ID | Title | Category | Owner | Trigger Date | Status |
|----|-------|----------|-------|--------------|--------|
| W-1 | Truck chassis reward loop over-reward | Economy / Balance | economy-designer | Balance Pass (first full content integration sprint) | Open — Monitoring |
| W-2 | Biome 3 Elite combat length exceeds pacing window | Combat Pacing | systems-designer | Balance Pass (first full content integration sprint) | Open — Monitoring |
| W-4 | Haven scrap sink under-defined (endgame over-accumulation) | Economy / Scope | game-designer | Before VS scope lock (per CD-C1 gate condition) | Open — Scope Decision Pending |
| W-5 | Chassis Identity (P2) runtime validation | Design Pillar Validation | qa-lead + creative-director | VS playtest gate (named, per CD-C2 condition) | Open — Gate Pending |
| OQ-NE12 | HostileTiltDelta tell: accessibility review for KBM-only color-impaired players | Accessibility | accessibility-specialist | Before 1.0 ship (post-MVP, pre-1.0 milestone) | Open — Deferred (with plan) |

---

## W-1 — Truck Chassis Reward Loop Over-Reward

**Source**: `design/gdd/gdd-cross-review-2026-04-24-rerun2.md` §3b row W-1; echoed in §3c INFO-2.

### Summary

The Truck chassis applies `TruckRewardMultiplier = 1.5` on top of `BossFlatScrap`. The multiplicative stack is legal per formula (Scrap Economy GDD) and bounded to playable values, but over a full run the late-game Scrap surplus may exceed the design intent if this multiplier compounds with other chassis-favorable economies that surface at integration time.

### Why It's a Risk

- P4 "Scarcity with Agency" relies on Scrap staying meaningfully scarce through Biome 3. If the Truck's reward curve breaks that scarcity specifically for one chassis, P4 is partially negated for that subset of runs.
- Formula itself is correct; the concern is emergent interaction at Balance Pass when all content is integrated and real play data is available.
- Cannot be resolved by static analysis — requires live telemetry on boss reward distributions per chassis.

### Owner

**economy-designer** (primary), with I-2 BossFlatScrap × TruckRewardMultiplier telemetry bundled in the same investigation.

### Trigger Date

**Balance Pass** — the first full content-integration sprint during Production where all 3 biomes ship with live loot tables and all chassis are selectable end-to-end. Estimated: post-vertical-slice, mid-Production.

Concretely: when `/balance-check` is first run against a full end-to-end run build.

### Resolution Criteria

- Telemetry on at least 50 completed Truck runs across all 3 biomes shows the ratio `FinalRunScrap / ExpectedScrapCurve_Truck ≤ 1.15` (within 15% of design target).
- If the ratio exceeds 1.15: economy-designer proposes either (a) reduce `TruckRewardMultiplier` below 1.5, or (b) add a Truck-specific Scrap sink (e.g., heavier part repair costs).
- Resolution ratified by creative-director (P4 pillar owner) and landed in Scrap Economy GDD as a revision.

### Mitigations Already In Place

- W-1 is classified as **advisory, not blocking** in the cross-review — values are playable at nominal play patterns.
- Stage 1a arbitrations (three-axis triad) do not interact with W-1; no ADR dependency.

### Escalation Path

If Balance Pass telemetry shows `FinalRunScrap / Expected ≥ 1.30` (30%+ over curve), escalate to creative-director as a P4 pillar integrity concern rather than an economy tuning concern.

---

## W-2 — Biome 3 Elite Combat Length Exceeds Pacing Window

**Source**: `design/gdd/gdd-cross-review-2026-04-24-rerun2.md` §3b row W-2; echoed as INFO-2.

### Summary

Several Biome 3 Elite enemy archetypes cluster near the **upper bound** of the expected combat-length pacing window. The values are internally consistent and all formulas balance, but observed combat turn-counts against these Elites are likely to feel long relative to the rest of the Elite pool.

### Why It's a Risk

- Combat pacing is a feel concern, not a correctness concern — static analysis cannot distinguish "at the ceiling of the window" from "over the ceiling."
- Runs that bottleneck on a single long Elite fight near the end of Biome 3 risk breaking session flow (P3 "Tight Runs" implication).
- This is a curve-tuning concern, not a rules conflict — resolution lives in Balance Pass tuning, not in design revision.

### Owner

**systems-designer** (primary), with I-3 `EnrageTelegraphLeadTurns` per-archetype tuning bundled in the same investigation.

### Trigger Date

**Balance Pass** — same sprint as W-1. Concretely: when first full-biome playtests produce turn-count telemetry per Elite archetype.

### Resolution Criteria

- Turn-count telemetry across at least 20 Biome 3 Elite encounters shows **median turns-to-resolution ≤ upper-bound pacing target** (target defined in Card Combat GDD pacing spec, to be checked at Balance Pass time).
- For any archetype where median exceeds the upper bound: systems-designer proposes either (a) reduce Elite HP, (b) raise player Elite-phase damage expectation, or (c) accept with rationale if the specific archetype is intentionally a "boss-feeling" set piece.
- Resolution ratified via `/balance-check` and landed in Enemy GDD / Card Combat GDD as needed.

### Mitigations Already In Place

- W-2 is classified as **advisory, not blocking** in the cross-review.
- Per-archetype `EnrageTelegraphLeadTurns` tuning (I-3) is already identified as a complementary knob.

### Escalation Path

If multiple Biome 3 Elites exceed the upper bound **and** playtesters report "fights drag" verbatim, escalate to creative-director as a P3 pillar concern.

---

## W-4 — Haven Scrap Sink Under-Defined

**Source**: `design/gdd/gdd-cross-review-2026-04-24-rerun2.md` §3b row W-4. Creative-director gate condition CD-C1 explicitly requires this be revisited before VS scope lock.

### Summary

The Haven ending currently has no defined terminal Scrap sink. Without one, players may arrive at Haven with significant Scrap surplus that has no in-game use — breaking the scarcity-with-agency loop at the run's climactic moment.

### Why It's a Risk

- **P4 "Scarcity with Agency" has no terminal payoff in the MVP build.** Players will feel the absence of a Haven Scrap use even if they cannot name it.
- Deferring this silently to post-MVP converts a design gap into a pillar violation at the moment of highest player attention (the ending).
- CD-C1 gate condition: must be revisited **before VS scope lock**, not silently deferred.

### Owner

**game-designer** (primary), escalates to creative-director for P4 pillar ratification of whatever solution is chosen.

### Trigger Date

**Before VS scope lock.** Concretely: the VS scope-lock story is blocked until a Haven scrap-sink decision is recorded, either as an in-MVP mechanic or as an explicit, creative-director-signed scope deferral with a P4-mitigation rationale.

Estimated date: during Pre-Production, before the VS build is planned.

### Resolution Criteria

One of the following, ratified by creative-director:

1. **In-MVP design** — game-designer authors a Haven scrap-sink mechanic (cosmetic unlock, permanent chassis upgrade purchase, final-run memorial, etc.) in a Haven Ending GDD section or new mini-GDD. P4 terminal payoff restored.
2. **Scope-deferred with mitigation** — creative-director accepts the deferral on the explicit basis that the VS and MVP builds ship with a placeholder Scrap-cap affordance (e.g., Scrap converts to run-end score at fixed rate; excess Scrap is narratively "left behind") that gives player surplus *some* authored outcome, even if thin.
3. **Scope-cut** — Haven ending redesigned so Scrap is meaningfully consumed en route to Haven (not at arrival), removing the surplus by construction.

### Mitigations Already In Place

- None. This is the active open concern.

### Escalation Path

If game-designer and creative-director cannot converge on a solution before VS scope-lock, escalate to the user for scope-vs-pillar tradeoff decision. **Do not advance VS scope lock with this unresolved.**

---

## W-5 — Chassis Identity (P2) Runtime Validation

**Source**: `design/gdd/gdd-cross-review-2026-04-24-rerun2.md` §3b row W-5. Creative-director gate condition CD-C2 requires this appear as a **named gate** in the VS playtest plan, not left implicit.

### Summary

Pillar P2 "Chassis Identity" — the promise that each chassis feels meaningfully different to play — cannot be proven by static analysis of the GDDs. It is a playtest-gate claim. Static analysis has been strengthened by the three-axis arbitration (SilhouetteClass / VisualFamily / ArchetypeFamily) but formal validation still requires VS-gate work.

### Why It's a Risk

- A pillar that is only validated implicitly ("we'll know when we playtest") is a pillar that can silently fail. CD-C2 explicitly requires a **named gate**.
- VS playtests can be rationalized as "chassis felt different enough" without structured evidence, leaving P2 unvalidated through shipment.
- Three-axis triad (Stage 1a arbitration) materially mitigates the static-analysis risk but cannot substitute for empirical validation.

### Owner

**qa-lead** (playtest structure and evidence capture) + **creative-director** (P2 pillar pass/fail verdict).

### Trigger Date

**VS playtest gate.** Concretely: when the VS build enters playtesting, the VS playtest plan must explicitly include a "P2 Chassis Identity" validation section with per-chassis pass criteria.

### Resolution Criteria

- VS playtest plan contains a named section: "P2 Chassis Identity Validation Gate" with criteria such as:
  - At least 3 playtesters complete a partial run with each chassis under test.
  - ≥ 70% of playtesters, when asked "describe how [chassis A] felt different from [chassis B]," produce answers referencing at least 2 distinct gameplay dimensions (not just cosmetic differences).
  - Playtesters independently describe an experience that matches the Player Fantasy section of the chassis's GDD (per standard Pre-Production → Production gate acceptance criterion).
- Creative-director signs off on the validation result before the VS → Production gate is closed.
- If criteria not met: creative-director authorizes a chassis-identity iteration sprint *before* VS → Production gate closure.

### Mitigations Already In Place

- **Materially mitigated at the data layer** by Stage 1a three-axis arbitration — player `ChassisType` is decoupled from enemy `VisualFamily` / `ArchetypeFamily`, so chassis identity can drift along its own axis without being entangled with enemy variety.
- CD-C3 gate condition: any ADR that collapses the three-axis triad back to a single field requires CD re-approval. Architecturally protected.

### Escalation Path

If VS playtest P2 gate fails and an identity-iteration sprint produces no convergence: escalate to user for a pillar-refactor decision (rescope P2, or accept a weaker version).

---

## OQ-NE12 — HostileTiltDelta Tell: Accessibility Review for KBM-Only Color-Impaired Players

**Source**: `design/gdd/node-encounter.md` §K.5 "Accessibility Follow-up," line 1058.

### Summary

Node Encounter's `HostileTiltDelta` visual tell (which signals that a node has shifted toward hostile outcomes based on Frame state) uses a **three-channel** signal:
1. Rust-shimmer animation (visual)
2. Sub-200Hz audio undertone
3. Gamepad haptic feedback

For keyboard/mouse-only players, the haptic channel is unavailable. For players with color vision deficiency, the shimmer may be degraded. KBM-only players with visual impairment may perceive no channel.

### Why It's a Risk

- Accessibility is a **ship gate**, not a tuning concern. If a subset of players cannot perceive a game-significant tell, they are systematically disadvantaged.
- MVP scope explicitly defers formal accessibility-specialist review; this is tolerable only if tracked explicitly with a defined resolution milestone.
- Ships that omit this kind of tracking have historically been embarrassed by accessibility audits post-release.

### Owner

**accessibility-specialist** (primary — review and recommend remediation) + **game-designer** (implement recommended fallback channel) + **producer** (schedule the gate).

### Trigger Date

**Before 1.0 ship** (post-MVP, pre-1.0 milestone). Concretely: the accessibility review gate is scheduled as a named milestone in the post-MVP production plan, before any 1.0 release candidate.

### Resolution Criteria

One of the following, documented in the accessibility-specialist's review report:

1. **Three-channel tell is sufficient for MVP; add a fourth (non-color visual channel — shape, border, icon glyph) before 1.0.** Per AD-C2 gate condition on Art Bible §3 colorblind-safe Ambush overlay, this fourth channel is already being designed; OQ-NE12 resolution folds into AD-C2's delivery.
2. **Three-channel tell is insufficient even with AD-C2 fourth channel.** Accessibility-specialist proposes additional remediation (text cue, screen reader hook, configurable tell intensity). Production plan absorbs the work.
3. **Three-channel tell passes accessibility review as-is (unlikely).** Documented as accepted in the review report.

### Mitigations Already In Place

- Three-channel tell is already designed (I.4 of Node Encounter GDD).
- **AD-C2 Art Bible §3 retrofit** — colorblind-safe Ambush overlay with non-color visual channel (shape/border/icon) — is already a Week 1–2 Technical Setup task, which provides the mitigation path described in resolution option (1) above.
- OQ-NE12 is explicitly MVP-provisional: "ship MVP with three-channel tell; document KBM-only gap as known limitation."

### Escalation Path

If accessibility-specialist review (pre-1.0) returns "insufficient even with AD-C2" and remediation work would push the 1.0 release, escalate to user for release-slip-vs-ship-with-caveat decision. Default: **slip the release rather than ship a known accessibility gap.**

---

## Review History

| Date | Reviewer | Action |
|------|----------|--------|
| 2026-04-24 | producer (initial population) | Register created per PR-C4 gate condition. All 5 entries captured from 2026-04-24 gate-check + cross-review rerun2 + Node Encounter OQ list. |

---

## Review Triggers

Review this register:
- At the start of every sprint plan (producer confirms status and trigger-date changes).
- At every phase gate (producer surfaces active risks in the gate report).
- When any entry's trigger date is within 1 sprint (producer escalates to owner).
- When any new risk is identified during implementation (add as R-* entry).

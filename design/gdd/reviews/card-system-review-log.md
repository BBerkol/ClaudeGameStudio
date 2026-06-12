# Review Log: Card System

---

## Review — 2026-04-20 (Re-review #4) — Verdict: NEEDS REVISION → Revised → **APPROVED** (accepted by author, moving to production)

Scope signal: L
Specialists: game-designer, systems-designer, economy-designer, qa-lead, ux-designer, creative-director
Blocking items: 5 core issues found and resolved in session | Recommended: 10+ (advisory)
Prior verdict resolved: Yes — all 16 blockers from Re-review #3 confirmed resolved in GDD text.

Summary: Fourth clean re-review pass. All prior 16 blockers confirmed resolved. Five new core issues identified: (A) upgrade system phantom — `UpgradedVersion`/`IsUpgraded` fields in data contract with no mechanic defined; (B) primary-family bias algorithm underspecified — timing stated but not mechanism; (C) pity/F2/empty-pool three-way rules conflict — two contradictory outcomes for same input state; (D) keyword live-state UI entirely absent — Pillar 3 (Read to Win) undeliverable without live hand layer; (E) BaseDamage/DamageEffectSO.Amount desync — {damage} token and F1 used different sources with no tie-breaker. All 5 resolved in session. Creative-director verdict: NEEDS REVISION, all items tractable in one session.

Key decisions made:
- UpgradedVersion/IsUpgraded removed from EA data contract (post-EA RESERVED)
- Primary-family bias: slot-force algorithm (Slot 1 forced to primary-family pool, Slot 2 runs standard F2)
- Pity precedence: pity governs over F2 fallback; empty Rare pool → Scrap comp, counter resets to 0
- DamageEffectSO.Amount is the authoritative source for both {damage} token and F1; BaseDamage is convenience cache, must equal Amount, enforced at SO import
- Keyword State Visualization section added to UI Requirements (live hand layer for Retain/Ethereal/Innate/Exhaust)

### Blockers Resolved This Session

| Blocker | Fix Applied |
|---|---|
| Upgrade fields phantom in data contract | UpgradedVersion/IsUpgraded removed from EA CardDefinitionSO; RESERVED note added; OQ2 marked RESOLVED |
| Primary-family bias algorithm undefined | Slot-force algorithm specified (5 steps); PrimaryFamily field declared on ChassisMasteryDefinitionSO; degenerate-pool cases handled |
| Pity / F2 / empty-pool rules conflict | Precedence rule added (pity governs); empty-pool behavior specified (Scrap comp, counter resets); counter reset rules enumerated |
| Keyword live-state UI absent | Keyword State Visualization section added with per-keyword live hand state requirements |
| BaseDamage vs DamageEffectSO.Amount desync | DamageEffectSO.Amount declared authoritative; SO import fails if values differ; BaseDamage >= 1 enforced |
| F5 missing free valve exception | Cross-reference note added to F5 |
| F4 PurgeCount type confusion | Upper bound formula flagged as design-time only; EC7 named as runtime gate |
| FilterFamily empty-subset undefined | Silent-fail behavior added to DrawCardsEffectSO definition |
| F3 mastery coverage gaps | All 3 mastery tiers now tested; mastery 1-3 tolerance tightened to ±0.3pp / 100k draws |
| Missing ACs | Pity empty-pool path, pity/F2 independence, bias simulation, free purge valve, EC14, RequiresBehind playability, MerchantPrice parity all added |

---

## Review — 2026-04-20 (Re-review #3) — Verdict: NEEDS REVISION → Revised in session

Scope signal: L
Specialists: game-designer, systems-designer, economy-designer, qa-lead, creative-director (ux-designer failed — rate limit)
Blocking items: 16 found and resolved in session | Recommended: 12 (advisory)
Prior verdict resolved: Yes — all 19 blockers from 2026-04-20 (Re-review #2) confirmed resolved in GDD text.

Summary: Third clean re-review pass. All prior 26 blockers confirmed resolved. 16 new blockers identified across five domains: design gaps (skip incentive undocumented, Assault starter unspecified slots, Control stall not bounded, early-mastery identity mechanism absent, pity+Merchant interaction unspecified), data contract gaps (MerchantPrice field absent, PurgeCost = 0 exploit, F2/F4 missing variable tables), rules conflicts (pool exhaustion fallback rewrite needed, pity+empty-Rare-pool conflict, free purge valve seeding missing Save dependency), keyword gap (Innate stacking unbounded, breaks UI contract), and QA gaps (simulation ACs non-deterministic, Retain AC absent, EC1 ambiguity). Creative-director verdict: NEEDS REVISION, all items tractable in one session. All 16 resolved in session.

Key decisions made:
- Skip incentive is draw concentration (no mechanical skip reward; lean deck = higher draw reliability)
- Assault starter: max 1 Maneuver card (family ban to protect chassis identity gap)
- Control stall: forward dependency added — Card Combat GDD must specify Enrage-equivalent time-pressure mechanic
- Pity counter + Merchant: non-interaction is deliberate (Merchant Rare does not reset combat pity counter)
- Innate cap: max 3 per deck; enforced at offer/Merchant filter; opening hand = 3+4=7 (at compression threshold)
- MerchantPrice field added to CardDefinitionSO (default 0 = unset; pricing owned by Scrap Economy GDD)
- Pool exhaustion fallback rewritten: degrade tier → Scrap compensation; duplicates never offered
- Pity guarantee when Rare pool empty: converts to Scrap compensation
- Free purge valve: seeding spec added (runSeed + nodeIndex, persisted in Save); Save & Persistence added to Dependencies
- EC1 rewritten: Exhaust fires on play; Retain fires at turn end; not contradictory
- Retain AC added (was absent); EC15/EC16 added (Retain+Innate opening hand, Innate cap enforcement)
- Simulation ACs made deterministic (seed: 42 specified)
- Tuning Knobs contradiction resolved: min deck size removed from Tuning Knobs (per EC7 it is not a tuning knob)
- systems-index.md "pick-3" stale reference corrected to "2-card reward offer, pick 1 or skip"
- OQ7 partially resolved (MerchantPrice field added); OQ5 clarified (PurgeCost ≥ 1 now enforced at import)

### Blockers Resolved This Session

| Blocker | Fix Applied |
|---|---|
| Skip incentive absent | Draw concentration documented as the intended Pillar 4 mechanism |
| Assault starter 4 unspecified slots | Max 1 Maneuver card constraint added |
| Control stall not bounded | Enrage forward dependency added to Card Combat GDD |
| Early-mastery identity cadence | Mastery 1–3 primary-family reward bias added to Rarity section |
| Pity counter + Merchant unspecified | Deliberate non-interaction documented |
| MerchantPrice field absent | MerchantPrice: int added to CardDefinitionSO |
| F2/F4 missing variable tables | Variable tables with Symbol/Type/Range/Description added to both formulas |
| PurgeCost = 0 exploit | PurgeCost ≥ 1 enforced at SO import; range changed from 0–50 to 1–50 |
| F4 sequencing gap | Explicit sequencing constraint added: pool authoring blocked until Node Map GDD confirms CombatRewards |
| Pool exhaustion fallback broken | Rewritten: tier degradation → Scrap compensation; duplicates never offered; without-replacement maintained through Slot 2 |
| Pity + empty Rare pool conflict | Resolved: pity converts to Scrap compensation when Rare pool empty |
| Free purge valve seeding | Seeding spec added (runSeed + nodeIndex); Save & Persistence added to Dependencies |
| Innate stacking cap | Max 3 per deck; EC16 added; Innate cap AC added |
| Simulation ACs non-deterministic | seed: 42 added to all three simulation ACs |
| Retain AC absent + EC1 ambiguity | Retain AC added; EC1 rewritten to separate played vs. unplayed cases; EC15 added |
| systems-index "pick-3" | Corrected to "2-card reward offer, pick 1 or skip" |

---

## Review — 2026-04-20 — Verdict: MAJOR REVISION NEEDED → Revised in session

Scope signal: L
Specialists: game-designer, systems-designer, economy-designer, qa-lead, ux-designer, creative-director
Blocking items: 19 resolved in session | Recommended: 10 (several promoted to Open Questions or closed as resolved)
Prior verdict resolved: Partially — prior 7 blockers were resolved in the 2026-04-19 session, but an end-to-end doc pass revealed 5 of those items had not been fully propagated on-disk (notably ScrapSellValue rename incomplete), plus new specialist-level findings.

Summary: Structural scaffolding was sound but three categories of issues compounded: stale on-disk references (ScrapSellValue rename not propagated to Dependencies table), data-import validation gaps (PositionBonus no floor, EnergyCost no validation, no Control SO import rule), and pillar delivery mechanisms named but not specified (Chassis Identity fantasy had zero mechanical enforcement at the starter-deck level). The creative-director verdict identified Pillar 2 as aspirational prose — the "opening hand feels different" promise had no design constraints. Key design decisions made this session: max deck size removed entirely (natural balancing via draw variance); 33% free-purge valve per Chopshop visit; run pity counter for Rare drought (8-reward threshold); 3/3/1 copy limits with pool-exhaustion re-roll fallback; F3/OQ6 sampling resolved as without-replacement within a single offer; Control explicitly documented as a support family; starter deck Chassis Identity Constraints added; EC13/EC14 for Innate+Exhaust and Innate+Ethereal documented; full AC rewrite pass (F2 ±2σ, F3 pass band, Model B reward screen, EC3 code-review proxy, EC1/EC6/EC10/EC11 ACs added).

### Blockers Resolved This Session

| Blocker | Fix Applied |
|---|---|
| ScrapSellValue stale reference in Dependencies | Fixed to PurgeCost |
| PositionBonus no floor or upper bound | Added >= 0 SO import rule; ceiling guidance (0–8) added to F1 |
| EnergyCost < 0 not validated | Added >= 0 SO import rule |
| F3 vs OQ6 sampling contradiction | Resolved: without-replacement within single offer; F3 labeled as approximation |
| F2 pool exhaustion undefined | Pool Exhaustion Fallback clause added to F2 |
| Max deck size — removed | No maximum deck size; natural balancing through variance; Tuning Knobs updated |
| Free purge valve undefined | 33% seeded chance per Chopshop visit; added to Deck Composition Rules |
| OQ8 pity mechanic | Run pity counter: guaranteed Rare after 8 consecutive non-Rare combat rewards |
| OQ6 copy limits | Resolved: 3/3/1; added to Rarity and Deck Composition sections |
| Control "support family" undocumented | Explicit support-family statement added to Control family row |
| Starter deck Chassis Identity Constraint missing | Scout and Assault minimum family distributions specified in Deck Composition Rules |
| EC13 Innate+Exhaust missing | EC13 added: Innate fires until Exhaust removes card |
| EC14 Innate+Ethereal missing | EC14 added: permitted by design, use-in-opening pattern |
| StatusConditionSO missing from hierarchy | Added to EffectConditionSO hierarchy in Section 2 |
| Gamepad tooltip undefined | Gamepad tooltip behavioral requirement added to UI Requirements |
| EC7 floor rationale missing | Rationale added tying 10-card floor to Pillars 1 and 2 |
| AC rewrites (F2, F3, token contexts, reward screen, Control import, EC1, EC3, EC6, EC10, EC11) | Full AC section rewritten per qa-lead findings |
| OQ6 and OQ8 resolved | Marked RESOLVED in Open Questions table |

---

## Review — 2026-04-19 — Verdict: MAJOR REVISION NEEDED → Revised in session

Scope signal: L
Specialists: game-designer, systems-designer, economy-designer, qa-lead, ux-designer, creative-director
Blocking items: 7 resolved in session | Recommended: 8 (3 promoted to Open Questions OQ6–OQ8)
Prior verdict resolved: No — first review

Summary: The GDD was structurally complete (8/8 sections) but had two categories of serious issues. First, data contract ambiguities that would cause silent bugs: the {param} token binding was completely unspecified, BaseDamage and DamageEffectSO.Amount were contradictory sources of truth, and ScrapSellValue had opposite transaction directions in different sections. Second, pillar-serving gaps: Assault chassis had no per-subsystem targeting decision (violating Pillar 3 for 50% of EA chassis), Control family had no win-condition requirement (pure-stall decks could not end combat), and Innate keyword scope was ambiguous between "every combat" and "first combat only." All 7 blockers resolved in session. Card System is In Review pending a clean re-review session.

### Blockers Resolved

| Blocker | Fix Applied |
|---|---|
| B1: {param} token binding unspecified | Added token binding table with full vocabulary, indexed multi-effect tokens, and display context rule (static SO values in non-combat contexts) |
| B2: ScrapSellValue transaction direction contradiction | Renamed to PurgeCost, confirmed pay-to-purge model. F5 and all references updated. |
| B3: Innate keyword scope ambiguous | Clarified as every combat. Added note that Innate does not consume a draw slot. |
| B4: Assault chassis targeting shallowness | Added Focused Assault sub-category (single-slot targeting at ~70% damage vs. Broad). Assault family now has two targeting sub-types. |
| B5: Control family no win-condition requirement | Added rule: every Control card must include minimum 1 damage. Pure-disruption zero-damage cards are not permitted. |
| B6: Reward screen token resolution contract absent | Added display context rule to token binding spec: non-combat contexts resolve to baseline SO values only. |
| B7: Reward screen interaction model contradictory | Specified Model B (explicit Skip button required, 3 clickable actions). Added Rare-skip confirmation prompt. |

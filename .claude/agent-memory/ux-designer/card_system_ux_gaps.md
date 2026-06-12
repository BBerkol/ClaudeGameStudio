---
name: Card System UX Gaps
description: 32 open UX issues across four adversarial review passes of Card System GDD (2026-04-19 to 2026-04-20)
type: project
---

Adversarial UX review of `design/gdd/card-system.md` UI Requirements section. Four passes completed (2026-04-19 x2, 2026-04-20 x2). All gaps need resolution before a UX Spec can be authored for the combat screen and card reward screen.

## Pass 1 Gaps (structural review)

1. **Reward screen ambiguity** — "Cannot proceed without a choice" contradicts having a Skip option. Misclick risk on a high-stakes, irreversible screen is unaddressed.
2. **Hand size cap undefined** — Max before compression stated as 7, but no absolute cap exists. Compression behavior at 10+ cards not defined. Fitts's Law failure risk at high card counts.
3. **Tooltip position unspecified** — No render anchor defined. Obscuring hand or combat info is forbidden but no layout constraint given.
4. **Deck inspection locked out of combat** — "Outside of combat only." Slay the Spire allows in-combat deck inspection (genre expectation). Intentionality unclear.
5. ~~**Reward screen token resolution unspecified**~~ — **CLOSED** (Pass 4): GDD now explicitly states tokens resolve to baseline SO values on reward screen.
6. **Gamepad hand navigation undefined** — Fanned hand is hover-heavy. No alternative navigation pattern defined for gamepad.
7. **Chopshop purge scope ambiguous** — PARTIALLY RESOLVED by pass 2: discard pile IS included in the combined list. New sub-question: flat list loses deck/discard context (see pass 2, gap 4).
8. **Keyboard shortcuts entirely absent** — No hotkey design anywhere. Genre expectation (End Turn, Confirm, Cancel, Deck View) unmet.

## Pass 2 Gaps (adversarial review of explicit UI Requirements)

9. ~~**Gamepad tooltip trigger undefined**~~ — **CLOSED** (Pass 4): GDD Gamepad Requirement section now addresses this explicitly.
10. **Hand compression minimum width undefined** (RECOMMENDED) — No minimum exposed card width, no rule for what information is visible at compressed width, no overflow behavior. Extends gap 2.
11. **Rare-skip confirmation gamepad topology undefined** (ADVISORY) — Spec does not define whether gamepad can wrap from Skip back to Card A or if Skip is a terminal nav node.
12. **Flat purge list loses deck/discard context** (RECOMMENDED) — Players may want to purge a card just played (in discard) vs. one not yet seen. Flat combined list loses this signal.
13. **Deck cap reward screen behavior** — PARTIALLY CLOSED: GDD resolves this via "no maximum deck size" decision. But UI Requirements section doesn't state this — developer reading UI Requirements won't know. See Finding 22.
14. **Card-invalid vs. Offline slot visual ambiguity** (RECOMMENDED) — Two different suppression states (card cannot target here / slot is Offline) may share the same dimmed visual. Needs two distinct treatments.
15. **Positional lock indicator has no UX semantic contract** (ADVISORY) — Indicator is named but not described.
16. **No first-run keyword teaching hook** (RECOMMENDED) — Keyword comprehension is fully passive (hover-to-discover). No first-use highlight or forced teaching moment defined.

## Pass 3 Gaps

*(Pass 3 was the re-review pass that confirmed resolution of gaps 5 and 9 — no new findings recorded separately.)*

## Pass 4 Gaps (2026-04-20 — keyword state visibility and system cross-reference failures)

17. **Ethereal in-hand visual treatment absent** (BLOCKING) — No UI spec for how Ethereal cards are visually flagged as "expiring this turn" while in the player's hand. Badge alone is insufficient. Player trap without it.
18. **Innate in-hand visual treatment absent** (BLOCKING) — No UI spec for how Innate cards are visually distinguished in the opening hand. Player has no legible signal for why they have extra cards or why a card always appears.
19. **Escape key / reward screen dismissal: GDD body vs AC contradiction** (BLOCKING) — GDD body says "no passive close"; AC line 542 explicitly blocks Escape. These must agree. Blocking Escape on PC is a significant accessibility decision requiring explicit UX rationale in the UI Requirements section, not just in AC.
20. **Retain end-of-turn: no visual differentiation of retained cards** (BLOCKING) — No spec for how retained cards are visually differentiated from freshly drawn cards at the start of a new turn. Especially critical for Retain+Exhaust (EC1) interactions.
21. **Innate cap filter invisible — no player communication** (BLOCKING) — When 3 Innate cards are in deck, Innate cards are silently filtered from offers. No UI requirement defines how (or whether) this is communicated. Violates Pillar 3 (Read to Win).
22. **"No deck max" not noted in UI Requirements** (ADVISORY) — GDD resolves gap 13 via design decision, but the UI Requirements section doesn't reference it. Developer reading only UI Requirements won't know this constraint doesn't exist.
23. **Free-purge notification lives in Core Rules, not UI Requirements** (BLOCKING) — Line 174 contains explicit UI language ("Mechanics are in a good mood — first purge free") buried in Core Rules. UI Requirements section for Chopshop has no reference. Will be missed in implementation.
24. **Chopshop: current deck count not surfaced** (BLOCKING) — UI Requirements don't require displaying current deck count or proximity to floor. Breaks the Scrap/deck-size trade-off legibility. Player at 11 cards cannot tell they have one purge left.
25. **Gamepad tooltip dismiss behavior unspecified** (BLOCKING) — Gamepad Requirement specifies how tooltip is triggered but not how it is dismissed. No hover-off equivalent on gamepad. UI programmer will make arbitrary decision.
26. **Gamepad Chopshop list navigation undefined** (ADVISORY) — No spec for scroll behavior, list focus, or wrap-around for a 17–21 card scrollable list on gamepad.
27. **Chopshop per-card cost display after free purge is consumed** (BLOCKING) — No spec for how the per-card cost display and free-purge message update after the first (free) purge is used. Player will be confused about whether subsequent purges cost Scrap.
28. **Chopshop list sort order undefined** (ADVISORY) — Combined draw+discard list has no specified sort order. Affects how quickly a player can find and evaluate cards for purging.
29. **Pity counter: intentionally hidden vs. omission — not stated** (BLOCKING) — No UI requirement for pity counter visibility. GDD brief's own context question ("is it visible?") is unanswered. Must state explicitly: hidden by design OR spec the visibility treatment.
30. **Reward screen: Scrap compensation slot has no visual treatment** (ADVISORY) — F2 fallback awards Scrap when all tiers exhausted. Reward screen spec says "exactly 2 cards" — no treatment for Scrap-award slots.
31. **Pity-counter Scrap fallback has no reward screen treatment** (ADVISORY) — When pity fires but Rare pool is exhausted, player gets Scrap instead of an expected Rare. High emotional-valence moment with no UX spec.
32. **Primary family bias: hidden by design vs. omission — not stated** (ADVISORY) — Mastery 1-3 reward bias not surfaced. Must state explicitly in UI Requirements whether this is intentionally invisible (and why) or requires a disclosure element.

---

**Why:** These gaps will block the UI programmer and create rework if not resolved before `design/ux/combat-screen.md` and `design/ux/card-reward-screen.md` are authored. Gaps 17–21, 23–25, 27, 29 are BLOCKING and must be resolved before any UI implementation begins. A structural fix is also needed: the GDD needs a dedicated "Keyword State Visibility" subsection in UI Requirements covering Exhaust, Retain, Innate, and Ethereal live hand states.

**How to apply:** When authoring UX Specs for any card-related screen, resolve all open items first. Do not design around them — surface them to the user for explicit decisions. Closed gaps: 5, 9. All others remain open.

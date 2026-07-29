# Merchant UI Slice — Capture Before Destroy

**Date**: 2026-07-30
**Slice**: Merchant UI (dialogue-first entry + slide-open shop)
**Files at risk**: `Assets/UI/MerchantScreen.uxml` (full rewrite),
`Assets/UI/MerchantScreen.uss` (full rewrite),
`Assets/Scripts/CombatView/MerchantSceneController.cs` (rebind Q<>() +
entry↔shop state machine).

## Context

Merchant PrefabRoot infrastructure landed 2026-07-29 (commits `39e5a0e`
Unity, `b39aaa6` framework). Runtime + save + six-guard integration + 7
tests are on `main`. Placeholder UI was authored but renders pitch-black
at runtime — deferred to this slice which replaces UXML/USS wholesale
with the intended Darkest-Dungeon-style layout.

Design intent locked 2026-07-29 by user against
`C:\Users\berta\Desktop\merchant ref.png`. Full capture:
`C:\Users\berta\.claude\projects\C--ClaudeCreations-Madmax-Roguelike\memory\project_merchant_ui_layout_intent.md`.

## Placeholder UXML being destroyed

`Assets/UI/MerchantScreen.uxml` (56 lines, 2026-07-29). Elements:

- `#root.wr-merchant-root` — full-screen modal container (picking-mode Position, absorbs scrim clicks).
- `#panel.wr-merchant-panel.wr-panel` — 640px central panel, ember-bg background.
- `#header.wr-merchant-header` — horizontal row containing:
  - `#title.wr-merchant-title` — text "MERCHANT" (font-size 28, bold, ember-title color).
  - `#scrap-counter.wr-merchant-scrap` — text "0 SCRAP" (font-size 20, bold, ember-title color, right-aligned).
- `#offer-list.wr-merchant-offers` — vertical column of three rows:
  - `#offer-row-0.wr-merchant-offer` → `#offer-name-0` + `#offer-rarity-0` + `#offer-price-0` + `#offer-buy-0` (text "BUY").
  - `#offer-row-1.wr-merchant-offer` → same shape as row 0.
  - `#offer-row-2.wr-merchant-offer` → same shape as row 0.
- `#footer.wr-merchant-footer` — horizontal row containing:
  - `#convert-button.wr-merchant-convert.wr-button` — text "CONVERT SCRAP ⇄ FUEL" (font-size 14, bold, height 48).
  - `#leave-button.wr-merchant-leave.wr-button` — text "LEAVE →" (font-size 16, bold, height 48).

**Tokens referenced**: `--wr-color-ember-bg`, `--wr-color-ember-panel`,
`--wr-color-ember-panel-border`, `--wr-color-ember-title`,
`--wr-color-ember-body`, `--wr-radius-md`. (These stay in
`Tokens/tokens.colors.uss` — only consumers in MerchantScreen.uss are
being rewritten.)

## Placeholder USS being destroyed

`Assets/UI/MerchantScreen.uss` (173 lines, 2026-07-29). Local variables:

- `--wr-merchant-scrim: rgba(0, 0, 0, 0.65)` — full-screen scrim opacity.
- `--wr-merchant-row-bg: var(--wr-color-ember-panel)` — offer-row fill.
- `--wr-merchant-row-border: var(--wr-color-ember-panel-border)` — offer-row hairline.
- `--wr-merchant-rarity-common: #b8b8b8` — grey.
- `--wr-merchant-rarity-uncommon: #6ec4ff` — blue.
- `--wr-merchant-rarity-rare: #f2b56b` — orange (matches ember-title).
- `--wr-merchant-price: var(--wr-color-ember-title)` — orange.

Classes: `.wr-merchant-root` (full-screen absolute, center-column
layout), `.wr-merchant-panel` (640px wide central), `.wr-merchant-header`
(row space-between, bottom-border, margin-bottom 16px),
`.wr-merchant-title`, `.wr-merchant-scrap`, `.wr-merchant-offers` (flex
column), `.wr-merchant-offer` (row, all-4-sides bordered, radius-md,
padding 16px×12px, margin-bottom 12px), `.wr-merchant-offer.is-sold`
(opacity 0.4), `.wr-merchant-offer__name` (flex-grow 1, ember-body,
font-size 18 bold), `.wr-merchant-offer__rarity` (width 90, center,
class-modifiers `.is-uncommon` + `.is-rare` for colors),
`.wr-merchant-offer__price` (width 96, right, orange, font-size 18),
`.wr-merchant-offer__buy` (width 96, height 40),
`.wr-merchant-footer` (row space-between, top-border, margin-top 16
padding-top 16), `.wr-merchant-convert` (height 48, padding 20),
`.wr-merchant-leave` (height 48, padding 32).

## Placeholder controller Q<>() bindings being retargeted

`Assets/Scripts/CombatView/MerchantSceneController.cs` (293 lines,
2026-07-29). Cached VisualElement handles being retargeted to the new
element tree:

- `_root` ← Q<VisualElement>("root") — stays same.
- `_scrapCounter` ← Q<Label>("scrap-counter") — → `#scrap-value` + new `#fuel-value`.
- `_offerRows[i]` ← Q<VisualElement>($"offer-row-{i}") — → `#offer-slot-{0..11}` (12 slots).
- `_offerNames[i]` ← Q<Label>($"offer-name-{i}") — → per-slot child.
- `_offerRarity[i]` ← Q<Label>($"offer-rarity-{i}") — → per-slot child.
- `_offerPrices[i]` ← Q<Label>($"offer-price-{i}") — → per-slot child.
- `_offerBuys[i]` ← Q<Button>($"offer-buy-{i}") — → per-slot click handler.
- `_convertButton` ← Q<Button>("convert-button") — moves to right-panel resource footer.
- `_leaveButton` ← Q<Button>("leave-button") — retired; `#choice-3` label+handler-swap replaces it (Leave ↔ Back).

**Behavior preserved**: OnEnable Q<>() re-query per UIDocument SetActive
re-clone trap; OnEnable/OnDisable subscription lifecycle pairing for
locally-queried buttons; scrap-balance polled via RefreshScrapCounter
after mutations; TryPurchase → CommitMerchantPurchase → rebind.

**Behavior added**: entry↔shop state machine (`_isShopOpen` bool),
`OpenShop()` / `CloseShop()` toggling `.is-shop-open` on root with a
one-frame `schedule.Execute` deferral to bypass UI Toolkit's
display:none→flex transition-firing limitation; `#choice-3` label+handler
swap between "Leave" and "Back".

## Slice Shape (per TD + unity-ui-specialist verdicts)

**Single UXML file, class-toggle state machine.**

Entry state (root without `.is-shop-open`):
- Fullscreen `#illustration` (backdrop, sprite lagging dep).
- Right-anchored `#dialogue-panel` (34% width, diagonal-strip pattern from DialogueScene.uxml).
- Header: `#merchant-crest` + `#merchant-name` + resource strip (`#scrap-value` + `#fuel-value`).
- Body: `#body` flavor copy.
- Choices: `#choice-1` "1. Let's see what you have." / `#choice-2` "2. I want to sell." (disabled + "Coming soon" tooltip pre-Phase-2.5) / `#choice-3` "3. I'm not interested."
- Footer: `#convert-button` (persistent across both states).

Shop state (root with `.is-shop-open`):
- Illustration remains visible partially behind the incoming shop panel.
- Left `#shop-panel` (56% width, absolute position, translates from `-110% 0` → `0 0` on 220ms ease-out).
- Shop panel: `#tab-buy` / `#tab-sell` header + `#offer-grid` (4×3 = 12 slots authored; unused slots `display: none` at runtime).
- Right panel stays visible with `#choice-3` label swapped to "3. Back" and handler routed to `CloseShop()`.

## Technical Director Review

**Verdict**: `[TD-MERCHANT-UI: APPROVE]`

**Q1 — Sell button**: Ship visible-but-disabled with "Coming soon" hint.
Preserves 1.0 entry-state layout that trust-gate/escort variants will
inhabit. Fake empty-shop UX rips out at Phase 2.5; hiding foreclosures
the seam.

**Q2 — Convert scrap↔fuel**: Persistent right-panel footer strip across
both states. Convert is a *service the merchant provides*, not a
per-choice affordance. Doesn't pollute numbered choices (narrative-shaped
for trust-gate) and doesn't disappear on state toggle (ADR-0011 clean).

**Q3 — State toggle**: Single UXML with USS class toggle `is-shop-open`
on root. Two files doubles SetActive re-clone re-query surface + forces
controller to hold two VisualTreeAsset refs. `left: -100% → 0` USS
transition is compositor-cheap; SetActive re-clone trap gets *worse*
with two documents, not better.

**Q4 — Back button seam**: Approve Leave↔Back label+handler swap on
single `#choice-3` button. One delegate rebind, one focus target, label
carries current semantic. Zero ADR-0011 concern.

**Q5 — 1.0 survival + ADR-0011 audit**: No violations. Two-state design
is content-shaped (dialogue routing), not behavioral bimodal — ADR-0015
pattern applies. Watch: don't let "Coming soon" hint become a magic
string; serialize on controller so it becomes real merchant dialogue at
1.0.

**Three-lens self-audit**:

- **Health**: Single UXML avoids SetActive re-clone trap. OnEnable Q<>()
  stays flat. Confirm controller re-nulls VisualElement pointers in
  OnDisable per `feedback_uidocument_setactive_reclone`. Sweep new UXML
  for `--` in comments before commit per
  `feedback_uxml_no_double_dash_in_comments`. Subscription pairing:
  locally-queried buttons OnEnable/OnDisable; external publishers
  (MerchantVisit/purchase events) Bind/OnDestroy.
- **Optimization**: USS `translate` transition is compositor-cheap. 4×3
  grid = 12 elements. Runtime-built choices not needed (fixed 3 in entry,
  fixed 3 in shop). No per-frame alloc risk in a modal screen.
- **1.0 survival**: Entry-state shape survives to 1.0 — trust-gate
  dialogue slots into body, escort quest slots into numbered choices,
  suspicion variants read RunState in controller. Sell "Coming soon" is
  the only stopgap and flips to real at Phase 2.5 without USS/UXML
  churn. `is-shop-open` USS class IS the canonical 1.0 shape. Convert
  footer persists. No ripout risk.

**Ship it.** Files at risk match user's list; MerchantRoot.prefab +
runtime C# untouched per contract.

## unity-ui-specialist Structure Verdict

Single UXML with class toggle. Shop panel `position: absolute` with
`translate: -110% 0 → 0 0` (110% not 100% to cover edge bleed/shadow),
duration 220ms ease-out. UI Toolkit display:none→flex transition-firing
limitation → controller sets `display: flex` one frame before adding
`.is-shop-open` via `schedule.Execute`, letting layout pass establish
translated-off position before the class toggle fires the transition.

Right panel keeps DialogueScene's diagonal strip pattern verbatim
(right-anchored 34% width, rotated `-bg` sibling `rotate: 5deg`, upright
content on top with heavier left padding). Merchant-specific header
(crest + name + resource strip) sits inside content above scroll view.
No art assets required for this shape.

Choice-3 same-button swap: `_choice3Button.clicked -= HandleChoice3Clicked;
_choice3Label.text = _isShopOpen ? "3. Back" : "3. I'm not interested.";
_choice3Button.clicked += HandleChoice3Clicked;`. HandleChoice3Clicked
branches on `_isShopOpen`.

4×3 grid with 12 authored slots; unused slots per-slot `display: none`
at runtime. Phase 2.5 (parts axis) grows offer count without UXML
churn — just bind more slots and unhide.

Static UXML choice buttons (no runtime RebuildChoices). Merchant choices
are fixed 3 in entry + fixed 3 in shop; runtime building adds
complexity for zero benefit.

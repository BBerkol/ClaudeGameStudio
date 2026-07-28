# Dialogue Scene Layout Redesign — AWD-Style Fullscreen + Diagonal Strip

**Date:** 2026-07-28
**Scope:** UI rewrite of the shared Node Encounter dialogue modal (used by every Event payload today, future Merchant/Chopshop/Stranded chance-event tomorrow).
**Trigger:** User direction — the DDDA-2 two-panel split doesn't match the As-We-Descend visual reference they want. Fullscreen art + slanted right-side text strip is what ships.

---

## Files being rewritten

| Path | Kind | Delta |
|---|---|---|
| `Assets/UI/DialogueScene.uxml` | Rewrite | Two-panel split → fullscreen illustration bg + right-anchored panel with header + scroll body |
| `Assets/UI/DialogueScene.uss` | Rewrite | Amber panel styling → diagonal strip (rotated bg), 90% black transparent, borderless, per-button reward-pill class |
| `Assets/Scripts/Run/Authoring/DialogueSceneSO.cs` | Field add | New `_description` (short subtitle under title) |
| `Assets/Scripts/Run/Authoring/DialogueChoiceSO.cs` | Field add | New `_rewardIcon` (Sprite) + `_rewardCount` (int) for right-side pill |
| `Assets/Scripts/UI/DialogueSceneController.cs` | Behavior | Description binding, choice-button now has label + reward pill children, flip `.is-visible`→`.is-hidden` semantics so UI Builder preview works |

---

## Current-state snapshot (destroyed by this edit)

### `DialogueScene.uxml` before
Two-panel body: `#illustration-panel` (500×620) left, `#copy-panel` (~620×620) right, both children of a centered `#panel` (1120×620). Title + body + choice column stacked in the copy panel. Amber ember scrim behind.

### `DialogueScene.uss` before
- `.wr-dialogue-root` — full-screen, `display: none`, centers `.wr-dialogue-panel` (1120×620)
- `.wr-dialogue-panel` — amber bg + 2px border + md-radius
- `.wr-dialogue-illustration-panel` — 500px column, right border
- `.wr-dialogue-illustration` — 460×560 inset image, scale-to-fit
- `.wr-dialogue-copy-panel` — flex-grow, 32/36px padding, amber panel bg
- `.wr-dialogue-title` — 42px bold ember-title
- `.wr-dialogue-body` — 20px flex-grow ember-body
- `.wr-dialogue-choices` — column, stretch
- `.wr-dialogue-choice` — 52px tall, amber choice-bg, 2px border, hover state

### `DialogueSceneSO` before
Fields: `_illustration` (Sprite), `_title` (string), `_body` (TextArea 4-8), `_choices` (DialogueChoiceSO[]).

### `DialogueChoiceSO` before
Fields: `_label` (string), `_tooltip` (TextArea 2-4), `_lootContext` (LootContextTag), `_nextScene` (DialogueSceneSO).

---

## New target design

Per user direction 2026-07-28:

1. **Fullscreen background illustration** — the illustration image goes full-bleed behind everything (not confined to a 500px left panel).
2. **Right-anchored diagonal strip panel** — reaches full screen height (top to bottom), width ~34% of screen. Left edge is slanted (diagonal cut so the panel visually leans into the illustration).
3. **Panel header (fixed, non-scrolling)** — big title + description **preamble** (narrative setup describing what unfolded BEFORE the scene captures — e.g. "You crest the ridge and find a slave-driver whipping his men…").
4. **Panel body (scrollable)** — the **dialogue quote / moment itself** (e.g. `"Strap him down and get back on that line!"`) and the numbered choice buttons live in a `ScrollView` together, so "dialogue and interactive reactions paste into" the same scrolling region.
5. **Numbered choice buttons** — controller auto-prefixes labels with `1. `, `2. `, `3. ` (FTL/AWD convention; self-documents keyboard hotkeys).
6. **Reward pill on choice buttons** — right side of each choice button shows an icon + numeric count for the reward/cost it grants:
   - **Positive** count = gain — amber reward register (`[Tackle him] [🔩] 10`)
   - **Negative** count = cost — red cost register (`[Cut him loose] [❤] -10`)
   - **Zero** = show icon only (boolean rewards like 'unlock')
   - **Null icon** = no pill at all (decline / skip buttons)
7. **Panel background** — 90% opaque black (rgba(0,0,0,0.9)), **no border**.

---

## Design decisions being made (per intuit-before-clarify)

Landing these as stated assumptions rather than pre-asking — flag any that need to change:

1. **Diagonal implementation:** rotated background sibling (`rotate: 5deg`) sized larger than the visible container so the slant edge shows on the left; content sits upright on top. Chosen because UI Toolkit has no `clip-path` and rotating the whole panel would tilt the text. Angle 5° is subtle — can push to 7-8° if you want more lean.
2. **Description field on DialogueSceneSO:** short subtitle (≤120 chars) between title and body. Optional — empty string hides the element.
3. **RewardIcon + RewardCount on DialogueChoiceSO:** display-only metadata. Actual mechanical outcome still driven by the payload (`ScrapAmount` / `FuelAmount` / etc.). Non-null icon triggers the pill; count > 0 shows the number. This is a **display denormalization** — the designer authors these to match the payload today; the eventual clean-up unifies choice authoring as the source of truth (deferred, no consumer yet).
4. **Body copy moves into the scroll area** alongside choices — reads as "the dialogue pastes into" per your description. Description stays in the fixed header.
5. **UI Builder visibility:** flipping default USS state to visible (`display: flex` by default). Controller adds an `.is-hidden` class in `EnsureCached` so the runtime starts hidden until Bind removes the class. Fixes the "I see nothing inside DialogueScene" complaint when opening the UXML in UI Builder.
6. **Panel width:** 34% of screen. AWD-adjacent ratio; can retune.
7. **Configure() overloads:** adding the new SO fields via optional parameters (default null/empty) so the existing `NodeEncounterDataInitializer` calls keep compiling without touching them; explicit values authored in Unity Inspector or fed through the initializer follow-up.

---

## Three-lens self-audit (per `feedback_td_three_lens_self_audit`)

**Health:** UXML/USS rewrite is contained to one modal. The controller behavior change (is-hidden flip, description binding, reward-pill build) is additive. New SO fields default to empty/null so existing authored assets stay valid.

**Optimization:** The rotated background element adds one more `VisualElement` per modal (~negligible). ScrollView is a stock UI Toolkit control — no custom scroll math. Diagonal via rotate transform is GPU-cheap.

**1.0 survival:** RewardIcon/RewardCount as display-only metadata is a temporary shape — the correct end-state is that the choice authoring drives BOTH display and mechanical outcome (payload numbers go away). Deferred because no second consumer and unification would balloon this slice. Data shape (Sprite + int per choice) survives that unification; only the wiring changes. Data-flag lagging dep pattern per `feedback_data_flag_lagging_dependency`.

---

## Technical Director Review

*Self-review only — the change is visual-direction execution against a specific reference the user provided. No architectural pivot; no cross-system implications. TD spawn skipped per the "visual direction execution" carve-out. Three-lens audit above stands in.*

**Verdict:** Proceed.

---

## Acceptance walkthrough (after implementation)

- [ ] Open `Assets/UI/DialogueScene.uxml` in UI Builder — the full modal renders in the canvas (default `.is-hidden` not yet added).
- [ ] Enter PlayMode, hit an Event beacon — modal appears with the new layout, fullscreen illustration, diagonal strip on right.
- [ ] Resolve one Event → hit a second Event beacon → modal appears cleanly (regression check on `feedback_uidocument_setactive_reclone`).
- [ ] Author a reward pill by setting `_rewardIcon` + `_rewardCount` on a `DialogueChoiceSO` — the button renders with the pill on the right.
- [ ] Skip-style buttons (`_rewardIcon` null) render as label-only, no pill.
- [ ] Body text long enough to scroll — the header stays fixed, body + choices scroll together.

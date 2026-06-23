# TD Verdict — Slice 7a CardWidget P3/P4 Collision

**Date:** 2026-06-23
**Gate:** TD-ARCHITECTURE (sequencing call within Slice 7a scope)
**Verdict line:** `TD-ARCHITECTURE: APPROVE` — **Option α (DraftCardElement native UXML)**, with scope tightening below.

## Headline

Ship **α**. Picker is a screen-space draft surface; hand is the in-combat cast/target surface. They are **different problem domains** sharing only a visual lineage — that is ADR-0014's axis-aligned-hybrid pattern, not ADR-0011's bimodal-paths pattern. ADR-0014's P3-before-P4 sequencing **anticipates exactly this** and is internally consistent. β postpones cleanly but pays the same cost later. γ rejected — it is a textbook bridge.

## ADR-0011 reading (your Q1)

ADR-0011 bimodal-paths smell = "two parallel ways to author/solve **the same surface**." Picker offers (click-to-pick a static preview, 3 instances, screen-space draft, lifecycle = pick or skip) and hand (drag/cast/target/hover/cancel/slide-in, variable count, in-combat, lifecycle bound to `CombatLoop.Hand[i]`) are not the same surface. They are not parallel paths for one problem; they are two different problems with a shared visual ancestor. ADR-0014 §Decision lists `CardRewardPicker` and `Combat_HUD` as **separate rows** in its phase table — the ADR author treated them as separate surfaces, and that read holds.

**One caveat for ADR-0011 hygiene:** `DraftCardElement` USS pulls **the same design tokens** as the eventual hand card (`tokens.colors`, `tokens.typography`, `tokens.spacing`, `tokens.radii`). Visual lineage is enforced at the token layer, not by sharing a UXML root. When P4 lands the hand card, it inherits the same token references — no "two color systems" drift.

## Scope confirmation (your Q2)

`DraftCardElement` reproduces **only**: cost badge (top-left), name label, body sprite, info text (effect line). Click-to-pick + hover-lift (USS `:hover { translate: 0 -24px; transition: 120ms; }` — both trivial in USS). Target ~40-60 lines UXML + USS + ~80 lines C# controller.

**Explicitly out of scope for `DraftCardElement`:** drag, targeting service hook, slot-picker integration, `_castEngageLiftPx`/`_castCommitLiftPx` hysteresis, in-hand state polling, slide-in from off-screen, cancel-bop fade, playability tint. None of these are picker behaviors. If a future picker variant needs any of them, raise it then.

## Test pattern (your Q4)

Confirmed. Add `internal VisualElement Root => _root;` and a typed `internal Label SubtitleLabel => _root.Q<Label>("subtitle");` accessor. `InternalsVisibleTo("WastelandRun.CombatView.Tests")` in the asmdef if not already wired. The reflection-on-private-field pattern from the current test is the wrong primitive under UI Toolkit — Q-by-name is the canonical seam ADR-0014 §Implementation Guidelines >Tests calls out. Rewrite both tests to use the accessor; do not bridge the old reflection path forward.

## Capture-before-destroy (your Q5)

**Yes — pre-author it before drafting code.** Path: `production/polish-captures/2026-06-23-slice-7a-ui-toolkit-port.md`. Cover both surfaces in one file (they ship as one slice). Enumerate from source: every `static readonly Color`, every `*Px` const, every anchor offset, scrim alpha, subtitle string formats (`"+N SCRAP"`, `"Your run ends here."`, `"CHOOSE A CARD"`, `"SKIP →"`), button tints, hover lift values (`HoverLiftPx = 24f`, `LerpSpeed = 12f`), `CardWidget` cost/name/info colors (`CostTextColor 0.7451,0.3160,1,1` etc.) and font sizes. The capture exists so P4 can cross-reference what the hand needs to match.

## Verdict on the three options

- **α — APPROVE.** Ship it. Two surfaces, one token system, ADR-0011 clean.
- **β — Acceptable alternative but rejected.** Defers `CardRewardPicker` to bundle with P4. Cost: picker stays UGUI through M1, and P4 grows. Benefit: zero. Same end state, slower.
- **γ — REJECT.** Embedding UGUI under UI Toolkit is the bridge ADR-0011 forbids and the perf cost (per-frame `RuntimePanelUtils.ScreenToPanel`-equivalent overhead) violates ADR-0014's stated reason for the `Popups` exception.

## Success criteria (we'll know this was right if)

- `DraftCardElement.uxml` exists; `CardWidget.cs` is untouched.
- Both UI Toolkit controllers expose internal Q-by-name accessors; tests assert on `Label.text`, not reflection on TMP fields.
- Capture file at `production/polish-captures/2026-06-23-slice-7a-ui-toolkit-port.md` enumerates every authored value from `CardRewardPicker.cs`, `CombatOutcomeOverlay.cs`, and the cost/name/info color/font constants from `CardWidget.cs` that P4 will need to match.
- EditMode tests green before the gate closes (per `feedback_gate_check_requires_green_tests`).
- No new `Canvas` introduced; no `UnityEvent` anywhere in the new controllers.
- At P4, the hand-card UXML pulls the same tokens — no second color/spacing/radius vocabulary appears.

## Absolute paths referenced

- `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\Assets\Scripts\CombatView\CardRewardPicker.cs`
- `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\Assets\Scripts\CombatView\CombatOutcomeOverlay.cs`
- `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\Assets\Scripts\CombatView\CardWidget.cs`
- `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\Assets\Tests\EditMode\CombatView\CombatOutcomeOverlay_Test.cs`
- `C:\ClaudeCreations\Madmax Roguelike\docs\architecture\adr-0014-ui-toolkit-primary-stack-hybrid.md`
- `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\Assets\UI\Tokens\` (existing token sheets — `DraftCardElement` pulls from these)

---

*Filed by technical-director, 2026-06-23. Respects ADR-0014 (Accepted 2026-06-13), ADR-0011 (Accepted 2026-05-31), `feedback_capture_before_destroy_view_layer.md`, `feedback_gate_check_requires_green_tests.md`.*

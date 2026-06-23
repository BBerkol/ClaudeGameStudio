# 2026-06-23 — Slice 7a: UI Toolkit port of CombatOutcomeOverlay + CardRewardPicker

**Status:** Capture (pre-destroy)
**Slice:** 7a (ADR-0014 Phase 3)
**Surfaces destroyed:** `CombatOutcomeOverlay.cs` (UGUI MonoBehaviour),
`CardRewardPicker.cs` (UGUI MonoBehaviour), `CardOffer.cs` (UGUI hover/click
wrapper), `CombatOutcomeOverlay.prefab`, `CardRewardPicker.prefab`,
`CombatOutcomeOverlay_Test.cs` (reflection-on-private-field test pattern).
**Surfaces created:** `Assets/UI/CombatOutcomeOverlay.uxml/uss`,
`Assets/UI/CardRewardPicker.uxml/uss`, `Assets/UI/DraftCardElement.uxml/uss`,
`CombatOutcomeOverlayController.cs`, `CardRewardPickerController.cs`,
`DraftCardElement.cs` (custom UI Toolkit element), updated controller test on
`<Label>` element accessor.

## Why this destroys what it destroys

ADR-0014 (Accepted 2026-06-13) makes UI Toolkit the primary stack; UGUI is
retained only for the world-space `Popups` canvas. Phase 3 of the migration
ports `CardRewardPicker` + `CombatOutcomeOverlay` to UI Toolkit. Both are
shipped UGUI surfaces with authored prefabs and visual constants. The port
replaces the entire UGUI render tree — `Image`, `TMP_Text`, `Button`,
`IPointerClickHandler`, `RectTransform` — with UXML elements + USS classes
referenced from the token layer (`Assets/UI/Tokens/tokens.*.uss`).

ADR-0011 #4 (no parallel paths for the same surface) is honoured because
each surface gets ported wholesale in one commit — no UGUI-and-UI-Toolkit
side-by-side rendering of the same screen. The `CardWidget` UGUI prefab
stays in-place for the combat **hand** (P4 territory) — but the picker's 3
draft cards are rendered by a new `DraftCardElement` UXML/USS in the
picker's UI Toolkit subtree. Two visual representations of "a card" exist
during the P3 → P4 window, but in **different surfaces** (picker vs hand):
ADR-0014's axis-aligned hybrid (TD verdict
`production/td-verdicts/2026-06-23-slice-7a-cardwidget-collision.md`).

## Authored values destroyed (every constant, color, dimension, copy string)

### CombatOutcomeOverlay — `Assets/Scripts/CombatView/CombatOutcomeOverlay.cs`

**Colors (RGBA, normalized 0–1 unless noted):**

| Constant | Value | Used for |
|---|---|---|
| `VictoryColor` | `(0.55, 0.95, 0.55, 1.00)` | "VICTORY" title text + button colour-language match with `TurnPhaseWidget.BgVictory` |
| `DefeatColor` | `(1.00, 0.45, 0.45, 1.00)` | "DEFEAT" title text + button colour-language match with `TurnPhaseWidget.BgDefeat` |
| `ScrimColor` | `(0.00, 0.00, 0.00, 0.65)` | Full-screen tinted scrim |
| `ButtonBg` | `(0.30, 0.45, 0.65, 0.95)` | Primary button background |
| `ButtonLabel` | `(0.96, 0.96, 0.92, 1.00)` | Primary button label text |
| `SubtitleColor` | `(0.85, 0.85, 0.82, 0.90)` | Subtitle text ("+N SCRAP" / "Your run ends here.") |

**Dimensions (pixels, 1920×1080 reference):**

| Const | Value | Purpose |
|---|---|---|
| `TitleHeightPx` | 160 | Title element height |
| `SubtitleHeightPx` | 40 | Subtitle element height |
| `TitleFontPx` | 120 | Title font size |
| `SubtitleFontPx` | 22 | Subtitle font size |
| `ButtonWidthPx` | 320 | Primary button width |
| `ButtonHeightPx` | 76 | Primary button height |
| `ButtonFontPx` | 26 | Primary button label font size |
| `TitleAnchorYPx` | +120 | Title Y-offset from screen center (anchored mid) |
| `SubtitleAnchorYPx` | +30 | Subtitle Y-offset from screen center |
| `ButtonAnchorYPx` | -80 | Button Y-offset from screen center |
| Text element width | 900 | `sizeDelta.x` for title + subtitle |

**Copy strings (verbatim):**

| Path | String |
|---|---|
| Victory title | `VICTORY` |
| Defeat title | `DEFEAT` |
| Victory subtitle (dynamic) | `+{_scrapEarned} SCRAP` — int interpolated, 0 is legal per ADR-0013 |
| Defeat subtitle | `Your run ends here.` |
| Victory button label | `CONTINUE →` (with U+2192 right arrow) |
| Defeat button label | `RESET COMBAT` |

**Public C# API surface (must be preserved on the new controller):**

| Member | Signature | Caller |
|---|---|---|
| `Bind()` | `void` | `CombatHud.cs:633` |
| `BindCombat(CombatLoop loop)` | `void` | `CombatHud.cs:689` |
| `SetScrapEarned(int scrap)` | `void` (clamps <0 to 0) | `CombatController.DrainCombatToHost` line 250 area |
| `HideOverlay()` | `void` | `CardRewardPicker.HandleContinueRequested` |
| `OnContinueRequested` | `event Action` | `CardRewardPicker.Bind` line 104 subscribes |
| `OnRestartRequested` | `event Action` | `CombatHud.cs:635` subscribes (forwards to `RunSceneHost.RestartRun`) |

**Behavioural contracts (must be reproduced):**

- Hidden by default; only flips visible when `_activeLoop.Phase == CombatPhase.Ended`. Polling sits in `Update`. Comparison gate: re-stamp only on `(phase, winner)` flip.
- Scrim blocks pointer input on every other HUD widget by sitting last in the render tree with a raycast-receiving full-rect element (`PickingMode.Position` in UI Toolkit, `pointer-events: auto` USS).
- Hidden state keeps the GameObject ACTIVE (Update must keep ticking to notice the phase flip) — visibility achieved by zeroing scrim alpha + disabling pointer events + hiding child elements via class toggle, NOT `SetActive(false)`.
- `transform.SetAsLastSibling()` on every `ShowOutcome` — UI Toolkit equivalent: `BringToFront()` on the root.
- Primary click → branch on `_activeLoop.Winner`: `Player` invokes `_onContinueRequested`; otherwise `_onRestartRequested`.
- Clicks on the scrim background (anywhere outside the button) are absorbed (no-op `OnPointerClick`).

### CardRewardPicker — `Assets/Scripts/CombatView/CardRewardPicker.cs`

**Colors:**

| Constant | Value | Used for |
|---|---|---|
| `ScrimColor` | `(0.00, 0.00, 0.00, 0.78)` | Picker scrim (darker than overlay's `0.65` — picker is the "active" screen, deeper dim) |
| `TitleColor` | `(0.96, 0.92, 0.55, 1.00)` | "CHOOSE A CARD" title — gold/yellow |
| `SkipBg` | `(0.30, 0.30, 0.32, 0.92)` | Skip button background |
| `SkipLabel` | `(0.92, 0.92, 0.88, 1.00)` | Skip button label |

**Dimensions:**

| Const | Value | Purpose |
|---|---|---|
| `OffersToShow` | 3 | Number of draft cards rendered |
| `CardWidthPx` | 160 | Matches `Card.prefab` footprint — picker scales same physical card the player will hold |
| `CardGapPx` | 60 | Horizontal stride = width + gap |
| `HoverLiftPx` | 18 | Picker hover lift (NOT 24 — combat hand uses 24; picker is 18) |
| `LerpSpeed` | 12 | Per-second alpha-style lerp on hover-lift transitions |
| `TitleAnchorYPx` | +220 | Title Y-offset from screen center |
| `CardsAnchorYPx` | 0 | Cards row at screen center |
| `SkipAnchorYPx` | -240 | Skip button Y-offset from screen center |
| Title font | 56 | "CHOOSE A CARD" |
| Title size delta | 900 × 90 | Title element |
| Skip button size | 280 × 64 | |
| Skip font | 22 | |

**Copy:**

| Path | String |
|---|---|
| Title | `CHOOSE A CARD` |
| Skip button label | `SKIP →` (U+2192) |

**Public C# API surface (must be preserved):**

| Member | Signature | Caller |
|---|---|---|
| `Bind(RunSceneHost host, CombatOutcomeOverlay outcome)` | `void` | `CombatHud.cs:607` (after port: arg type becomes the new controller) |
| `OnPickResolved` | `event Action` | `CombatHud.cs:613` subscribes, forwards to `RunSceneHost.AdvanceToNextBeacon` |
| `OnOfferPicked(int choiceIndex)` | `void` (forwards `_host.Session.AcceptCardChoice(choiceIndex)`) | Internal to picker — called by `DraftCardElement` click handler |

**Behavioural contracts:**

- Hidden by default. Subscribed to `CombatOutcomeOverlay.OnContinueRequested`. When the event fires: hide the outcome overlay first (so scrims don't stack one frame), roll offers, bring picker to front, show.
- Reads `_host.State.PendingCardOffer.Choices` via the active `RunSession`. If null, logs warning and renders empty draft.
- Card prefab resolution priority: Inspector-wired `_cardPrefab` → fallback `Resources.Load<CardWidget>("Combat/Card")`. **After port: this fallback path retires** — `DraftCardElement` is a UXML custom element, no prefab resolution. Removes the `Assets/Resources/Combat/Card.prefab` self-load convenience but the resource was a P4-territory affordance, not the picker's.
- Skip → `_host.Session.SkipCardChoice()` → `_onPickResolved` → `CloseAndReset`.
- Card pick → `_host.Session.AcceptCardChoice(choiceIndex)` → `_onPickResolved` → `CloseAndReset`.
- Picker scrim absorbs all clicks outside cards/skip (matches overlay's pattern).
- `DestroyLegacyOffers` (current code, lines 149–163) walks live hierarchy on every Bind to clean pre-W7.24 authored children. **After port: this defensive sweep retires** — UI Toolkit panels rebuild from UXML on each Bind, no authored-children drift possible.

### CardOffer — `Assets/Scripts/CombatView/CardOffer.cs`

Full file destroyed. Functionality folds into `DraftCardElement` UXML + controller:
- Hover-lift (USS `:hover` transform translate + transition)
- Click → `_owner.OnOfferPicked(_choiceIndex)` (UI Toolkit `RegisterCallback<ClickEvent>`)
- `Bind(CardDefinition card)` — sets cost/name/info text on UXML labels, hides element when card is null
- Per-frame lerp toward base/hovered Y → replaced by USS `transition: translate 0.08s ease-out` (declarative)

### CombatOutcomeOverlay_Test.cs — `Assets/Tests/EditMode/CombatView/CombatOutcomeOverlay_Test.cs`

Current pattern: reflection on private `MonoBehaviour` field `_subtitle` (`TMP_Text`), then reflection on its `text` property. After port:
- Pattern switches to `internal Label SubtitleLabel => _root.Q<Label>("subtitle");` accessor on the controller (ADR-0014's documented test pattern, ADR section "Implementation Guidelines").
- All three existing test cases stay (`Victory_PositiveScrap_RendersFormattedScrapLine`, `Victory_ZeroScrap_RendersZeroLineCoherently`, `Defeat_SubtitleUnchanged_ScrapIgnored`).
- Test setup: instantiate the controller MonoBehaviour, drive it with a stub `UIDocument` whose `visualTreeAsset` is the production UXML. Or simpler: load the UXML directly via `AssetDatabase` in EditMode and pass the cloned root to the controller via `internal void BindRootForTest(VisualElement root)` accessor.

### Prefab assets destroyed

- `Assets/Prefabs/CombatView/CombatOutcomeOverlay.prefab` — authored with serialised `_scrim`/`_title`/`_subtitle`/`_buttonBg`/`_buttonLabel`/`_buttonGo`/`_primaryButton` references. Replaced by a `UIDocument` prefab wiring `Assets/UI/CombatOutcomeOverlay.uxml` and the shared `PanelSettings.asset`.
- `Assets/Prefabs/CombatView/CardRewardPicker.prefab` — authored with `_title`/`_offers`/`_scrim`/`_skipButton`/`_cardPrefab` references. Same shape replacement: `UIDocument` prefab + `Assets/UI/CardRewardPicker.uxml`.

`CombatHud.cs:282-283` `[SerializeField]` references retarget from `CombatOutcomeOverlay` / `CardRewardPicker` MonoBehaviours to the new controllers `CombatOutcomeOverlayController` / `CardRewardPickerController`. `CombatPrefabAuthor.cs` (the menu that re-authors `Combat.prefab`) must thread the new prefabs into the HUD's serialised slots — addressed in the same commit.

### CardWidget visual lineage (P4 reference — NOT destroyed in this slice)

`CardWidget.cs` UGUI surface stays in place for the combat hand (P4). The DraftCardElement UXML must visually match this UGUI surface at the token layer so P4's hand-card UXML can use the same tokens — visual lineage is enforced at the token vocabulary, not by sharing markup.

| Lineage value | Current UGUI source | New token (to author this slice) |
|---|---|---|
| Cost badge text color | `CostTextColor = (0.7451, 0.3160, 1.00, 1)` purple-magenta | `--wr-color-card-cost` (new in `tokens.colors.uss`) |
| Value badge text color | `ValueTextColor = (0.7451, 0.3160, 1.00, 1)` same purple | `--wr-color-card-value` (alias to `--wr-color-card-cost` for now) |
| Card name color | `NameTextColor = Color.black` | `--wr-color-card-name` |
| Info text color | `InfoTextColor = (0.18, 0.18, 0.22, 1)` dark grey-blue | `--wr-color-card-info` |
| Playable tint | `PlayableTint = (1, 1, 1, 1)` (sprite passthrough) | N/A — DraftCardElement is always "playable" in picker context |
| Card width × height | 160 × 270 (`Card.prefab` `sizeDelta`) | `--wr-size-card-width` / `--wr-size-card-height` (new in `tokens.spacing.uss`) |
| Body sprite | `Resources/Card_Images.psb` per-family layer (attack / defense / utility) — loaded via `CardWidget.GetFamilySprite` | DraftCardElement loads same PSB via `Resources.Load` in controller (no UXML asset reference because Resources path) |

Token authorship is **part of slice 7a** — token additions are net-additive, token consumers in the future (P4 hand UXML) inherit them.

## What survives

- `RunState.PendingCardOffer` shape (POCO record) — unchanged.
- `RunSession.AcceptCardChoice(int)` / `RunSession.SkipCardChoice()` APIs — unchanged.
- `CombatHud.cs` subscription topology — `_outcomeOverlay` and `_rewardPicker` retarget to controllers; runtime-construction fallback path retires (UIDocument prefab + PanelSettings replaces the dynamic GameObject build).
- `CombatController.DrainCombatToHost`'s `SetScrapEarned` call into the overlay — port preserves the method signature.
- `CombatPrefabAuthor.cs` wire-up block for these two surfaces — updated to thread new prefab references; not destroyed.
- The three existing EditMode test cases — assertions preserved, plumbing changes from reflection to typed `Q<Label>` accessor.
- `Assets/UI/Tokens/tokens.colors.uss` etc. — extended additively, not rewritten.
- `Assets/UI/controls.uss` and `PanelSettings.asset` — unchanged.
- `CardWidget.cs` and `Card.prefab` — untouched (P4 territory).

## Technical Director Review

**Verdict (2026-06-23, `production/td-verdicts/2026-06-23-slice-7a-cardwidget-collision.md`):**

> **TD-ARCHITECTURE: APPROVE — Option α (DraftCardElement native UXML).** Picker and hand are different problem domains sharing visual lineage at the token layer, not parallel paths for the same surface. ADR-0014's P3-before-P4 sequencing anticipates this and is internally consistent. β is acceptable but only postpones equivalent work; γ is a bridge — rejected.
>
> **Scope confirmed:** `DraftCardElement` reproduces cost badge, name label, body sprite, info text, plus click-to-pick and hover-lift via USS `:hover`. ~40-60 lines UXML/USS + ~80 line controller. Drag/targeting/hysteresis/playability-tint stay out.
>
> **Test pattern:** Add `internal VisualElement Root` and typed Q-by-name accessors (`internal Label SubtitleLabel => _root.Q<Label>("subtitle");`) per ADR-0014. Drop the reflection-on-private-field pattern entirely — don't bridge it forward.
>
> **Pre-author the capture:** Yes, write `production/polish-captures/2026-06-23-slice-7a-ui-toolkit-port.md` covering both surfaces (one slice = one capture) plus the `CardWidget` cost/name/info color and font constants — P4 needs that exact lineage for hand-card visual match.
>
> **Critical hygiene:** All token references go through `Assets/UI/Tokens/tokens.*.uss` — visual lineage with the future hand card is enforced at the token layer so no second color/spacing vocabulary appears at P4. EditMode tests must be green before gate close (per `feedback_gate_check_requires_green_tests`).

**Upstream TD verdict (slice-7 scope):** `production/td-verdicts/2026-06-23-slice-7-adr-0004-scope.md` — set the slice sequence 7a (this) → 7b (NodeMap shape pre-work) → 8 (canonical ADR-0004).

## Acceptance gate

- New tokens added to `Assets/UI/Tokens/tokens.colors.uss` and `tokens.spacing.uss` (card cost / value / name / info; card width / height).
- `Assets/UI/CombatOutcomeOverlay.uxml/uss`, `Assets/UI/CardRewardPicker.uxml/uss`, `Assets/UI/DraftCardElement.uxml/uss` authored, all referencing token stylesheets in their `<Style>` headers.
- Controllers `CombatOutcomeOverlayController`, `CardRewardPickerController`, `DraftCardElement` (custom element) authored in `WastelandRun.CombatView` (TBD — confirm asmdef in execution).
- `CombatHud.cs` SerializeField type swap (overlay + picker references retarget to controllers).
- Two old `.prefab`s replaced by `UIDocument`-based prefabs wired into `CombatPrefabAuthor.cs`.
- Three `.cs` files deleted: `CombatOutcomeOverlay.cs`, `CardRewardPicker.cs`, `CardOffer.cs`.
- `CombatOutcomeOverlay_Test.cs` rewritten to use typed accessor pattern; three existing assertions preserved.
- EditMode tests: at least 508/508/0/1 green (current baseline; may grow with new tests for picker controller).
- Grep gates: clean. No reflection-on-private-field test pattern reintroduced. No `UnityEvent` anywhere in the new controllers.
- Capture-before-destroy hook: this file's existence satisfies it on the `.cs` and `.prefab` paths above.

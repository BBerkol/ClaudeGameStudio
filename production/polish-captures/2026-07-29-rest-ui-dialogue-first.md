# Rest UI — Dialogue-First Overhaul (destroy capture)

**Date:** 2026-07-29
**Slice:** Rest UI rewrite (last old-shape narrative surface)
**Follows:** Merchant UI slice (66714bb) + PeekOverlayBinding extraction
**Precedes:** Chopshop Phase 2.5

## Files destroyed / rewritten

| File | Fate | GUID |
|------|------|------|
| `Assets/UI/RestPicker.uxml` | `git mv` → `RestScreen.uxml` (GUID preserved) | `00f3a1a1dc4746749b620436326df55d` |
| `Assets/UI/RestPicker.uss` | `git mv` → `RestScreen.uss` (GUID preserved) | `979409a868b91aa418e23f9cf40492d7` |
| `Assets/Scripts/CombatView/RestPickerController.cs` | `git mv` → `RestSceneController.cs` (GUID preserved) + class rename inside | `270fab6d139809743a3cbbef6861202d` |
| `Assets/Prefabs/BeaconRoots/RestRoot.prefab` | Edit lines 4898 (`m_Name`) + 4953 (`m_EditorClassIdentifier`) | `-` |
| `Assets/Editor/CombatPrefabAuthor.cs` | Edit lines 8727–8799 (path const + type ref + GameObject name + doc comments) | `-` |

Sibling doc-comment `<see cref>` / text sweep (5 files, 8 line-edits total —
per TD delta D1 no vestigial `RestPickerController` references may remain):

- `Assets/Scripts/CombatView/MerchantSceneController.cs:35`
- `Assets/Scripts/CombatView/EventModalHost.cs:151`
- `Assets/Scripts/CombatView/BeaconActivator.cs:396`
- `Assets/Scripts/CombatView/RunSceneOverlayHost.cs:26`
- `Assets/Scripts/CombatView/VehicleBarStack.cs:147, 231, 637, 754, 818`
- `Assets/Tests/PlayMode/CombatView/RunPrefabAuthoring_Test.cs:87`

New authoring surface added:

- `Assets/UI/RestScreen.uxml` — dialogue-first root, right dialogue panel
  (crest + name + resource strip + peek buttons + body + numbered choices),
  full-screen `#illustration` BG, peek overlay sibling at root level
- `Assets/UI/RestScreen.uss` — mirrors `MerchantScreen.uss` layout tokens;
  `background-image: resource("Rest BG")` on `#illustration`
- `Assets/Resources/Rest BG.png.meta` — Unity import (post-focus)

## Authored values enumerated pre-destroy

### `RestPicker.uxml` (35 lines)
- Root class `wr-rest-root` (picking-mode Ignore)
- `#button-rail` (3 buttons: `#repair-button "REPAIR"`, `#forge-button
  "FORGE CARD"`, `#upgrade-button "UPGRADE WEAPON"` — all width 240 ×
  height 64, wr-button)
- `#continue-button "CONTINUE →"` (width 280 × height 64, initially
  hidden via USS `display: none`)
- `#budget-bar` fixed 16 × 96px with inner `#budget-bar-fill` (percent
  height, drains bottom→top)

### `RestPicker.uss` (130 lines)
- `--wr-rest-scrim: rgba(0,0,0,0.30)`
- `--wr-rest-budget-bar-bg: rgba(20,20,20,0.92)`
- `--wr-rest-budget-bar-fill: #F5C24D` (warm welding-tool yellow)
- `--wr-rest-budget-bar-border: rgba(245,235,140,0.70)`
- `.wr-rest-root` absolute full-screen, padding-bottom: 80px, column
  flex, justify flex-end (rail sits above bottom padding)
- `.wr-rest-action:disabled { opacity: 0.5 }` +
  `:disabled:hover` neutralised (defensive against `.wr-button:hover`
  orange)
- `.wr-rest-continue.is-visible` reveal class
- `.wr-rest-budget-bar` position absolute left/top 0, translate-driven
  cursor follow
- `.wr-rest-budget-bar.is-visible` reveal class

All authored values MIGRATE — the budget bar visual + Repair drain
mode stay byte-identical; only the entry surface (bottom-rail →
dialogue-panel) changes.

### `RestPickerController.cs` (768 lines) — PRESERVED as-is

- All repair-mode logic (`EnterRepairMode`, `TickRepair`,
  `SubscribeRepairHover`, `TrySwapToWeldingCursor`, sparks,
  budget bar, hit-zone hover with `SetTargetHover`)
- Const budget = 20, SecondsPerTick = 1/3, BarOffset = (20, 12)
- Rest-scope excludes `SlotKind.Armor` + `SlotKind.Bodywork`
  (user directive 2026-06-30; memory `project_armor_not_subsystem`)
- Prefab-scoped dependency resolution via
  `GetComponentInParent<BeaconSceneBootstrap>` (Merchant-incident-
  proofed 2026-07-30)
- `WorldSpace` canvas worldCamera bind for `VehicleBarStack` +
  `HitZonesCanvas`
- Resume-into-resolved-Rest guard in `Start`

Only these methods rewrite:
- `OnEnable`: query new UXML slots (dialogue-panel, header, choice-1
  through choice-4, illustration, peek overlay slots via
  `PeekOverlayBinding.Bind`)
- `ShowPicker` → `ShowDialogueEntry`: show dialogue panel + numbered
  choices; disable choice-1 (Repair) when `!HasRepairableDamage`;
  disable choice-2/3 (Forge/Upgrade — Phase 2.5 gated); choice-4
  (Continue) always enabled
- Button wire: `_choice1.clicked += OnRepairClicked` → enters
  existing `EnterRepairMode`; `_choice4.clicked += OnContinueClicked`
  → existing resolve path
- Peek: `_peek.SetHandlers(HandleMapPeek, HandleDeckPeek)` — 1 line

### `RestRoot.prefab` (lines 4886–4957, `RestPicker` GameObject)

Serialized fields preserved on rename (identical field names in new
class):
- `_document` fileID pointer → survives
- `_weldingCursor: {guid: 0ebe9e1c72aa52d4398cd20528cb9a08}`
- `_weldingCursorHotspot: {x: 4, y: 4}`
- `_weldingSparks` fileID pointer

Edited:
- Line 4898: `m_Name: RestPicker` → `m_Name: RestScreen` (cosmetic
  hierarchy label — parallel to Merchant's `#dialogue-panel` naming)
- Line 4953: `m_EditorClassIdentifier: ...RestPickerController`
  → `...RestSceneController`

## Non-goals (deferred, ADR-0011 clean)

- **Slide-in left picker panel** — Repair keeps cursor-swap-inline
  pattern; Phase 2.5 will add `#forge-panel` / `#upgrade-panel` as
  purely additive UXML siblings (Merchant's `#shop-panel` precedent)
- **Forge / Upgrade functionality** — choice-2/3 remain disabled
  placeholders with `(soon)` suffix (TD delta D2: flag suffix for
  Phase 2.5 removal)
- **RepairSO tuning surface** — consts stay (SO scaffolding lands
  with Forge/Upgrade)
- **Welding cursor art change** — texture GUID preserved
- **Rest illustration variants** — single `Rest BG.png` for 1.0

## Final-game picture this serves

After this slice, ALL four narrative beacon types (Merchant, Event,
Rest, and — post-Phase 2.5 — Chopshop) read as the same fiction:
full-screen illustration, right dialogue panel, numbered choices,
Map/Deck peek in header. Rest players ALSO gain peek availability
that Pass 1 didn't ship. When Phase 2.5 wires Forge/Upgrade, both
plug in as additive UXML sections — no controller shape churn, no
UXML restructure.

## Technical Director Review

**APPROVE with two deltas (both applied):**

**D1 — sibling doc-comment sweep.** Enumerated above (5 files, 8
line-edits). No vestigial `RestPickerController` mention may remain
in the codebase post-commit — the class does not exist any more.

**D2 — "(soon)" copy is intentional stopgap.** Flagged in Phase 2.5
branch: replace `"2. Forge a new card.  (soon)"` +
`"3. Upgrade a weapon.  (soon)"` with `"2. Forge a new card."` /
`"3. Upgrade a weapon."` and wire real handlers.

### Q1 Shape — APPROVE dialogue-first-with-immediate-cursor-swap
Stub slide-in picker panel would be ADR-0011 drift (Phase 2.5 rips
it out to reshape for real content). Repair's cursor-swap-inline is
already shipping shape. Symmetry with Merchant is preserved without
symmetry theater — dialogue panel + illustration + peek reads
identical.

### Q2 Controller placement — APPROVE rename to `RestSceneController.cs`
Rename is cheap (one hard type ref in CombatPrefabAuthor.cs, one
prefab `m_EditorClassIdentifier` string; 5 doc-comment refs). Skipping
the rename to "avoid churn" is exactly the ADR-0011 "vestigial name"
trap — the class is no longer a picker.

### Q3 ADR-0011 audit — CLEAN if D1 holds
Old `RestPicker.uxml/.uss` deleted same commit (via rename, not
deprecated-and-kept). No adapter/bridge. No bimodal path. No
"transitional" comment. D1 sweep enforces zero vestigial name.

### Q4 Timing — APPROVE extract-first
Before Chopshop Phase 2.5. Rest overhaul deferred past Phase 2.5
would either ship Chopshop-only new shape (leaving Rest as the sole
old-shape holdout — memory `project_rest_ui_overhaul_pending`
directly warns) or implicitly port the old rail shape to Chopshop.

### Q5 Blocker — soft: verify `PeekOverlayBinding.cs.meta` lands
File was created 2026-07-29 without .meta (Unity not focused post-
Write). No serialized reference exists (POCO `readonly` field-init)
so class-name resolution survives, but check the meta lands with
this commit or the Merchant slice's PeekOverlayBinding wire has an
un-versioned dependency.

## Three-Lens Self-Audit

**Codebase Health** — ADR-0011 clean IF D1 holds; subscription
lifecycle preserved (repair-mode hover subs stay Bind/Unbind-scoped);
peek helper is now second-consumer-validated (extraction was timely,
not premature); no teardown race.

**Optimization** — Zero per-frame allocation added; UXML load
one-shot at `BeaconActivator` switch; peek overlay build on-demand
via `PeekContentBuilder`; USS reuses `DialogueScene.uss` classes
(no style-recomputation duplication).

**1.0-Shape Survival** — Dialogue-first shape is the 1.0 canonical
for every non-combat beacon; choice slots ARE the 1.0 shape (only
handler bodies are stubs — no restructure when Phase 2.5 wires);
peek is full 1.0 payload (Map + Deck).

## Success criteria

1. All four narrative beacons read as the same fiction shape.
2. Phase 2.5 Forge/Upgrade lands as additive UXML sibling + one
   handler pair — no dialogue-panel restructure, no controller
   shape churn, no rename.
3. `grep RestPicker` across `Assets/**/*.cs,uxml,uss,prefab` returns
   zero hits post-commit.
4. First Rest visit shows Map/Deck peek buttons in the header
   (Rest players gain peek availability Pass 1 lacked).
5. Repair-mode drain byte-identical to Pass 1 (same StartBudget=20,
   SecondsPerTick=1/3, hover subs, sparks, cursor swap, budget bar).
6. Tests green (EditMode + PlayMode).

# Capture — P5 canvas deletions (`Debug` + `Combat_HUD`) + registry gate

Date: 2026-09-17
Slice: ADR-0018 P5 completion (final pre-chopshop debt item)
Primary file at risk: `Assets/Prefabs/CombatView/CombatHud.prefab` (surgical
YAML — NOT a re-author; fileID reminting would drop the hand-wired
`_targetingReadoutPrefab` and churn every anchor)

## Why

The P5 per-canvas code audit found both canvases render nothing: `Debug` has
never had a child instantiated into it; `Combat_HUD`'s only content (the
BuffTooltip) is author-parented there and reparented to `Popups` inside
`CombatHud.Awake` before first render. The registry gate cannot ship while
they exist (allow-listing them would be an ADR-0011 bridge). Full audit +
ruling: `production/td-verdicts/2026-09-17-p5-canvas-deletions.md`.

## Authored values destroyed (verbatim from TD verdict §6)

### `Debug` — destroyed in full

- GameObject `&5484149036302222134`: `m_Name: Debug`, `m_IsActive: 1`,
  `m_Layer: 0`, child index 1 of the CombatHud root
- RectTransform `&1360351997342636089`: scale (0,0,0), anchors (0,0)/(0,0),
  anchoredPosition (0,0), sizeDelta (0,0), pivot (0,0),
  father `189484520493424105`
- Canvas `&3742852243510443251`: ScreenSpaceCamera, camera {fileID: 0},
  planeDistance 1, **sortingOrder 110**, overrideSorting 0
- CanvasScaler `&6972783002601490691`: ScaleWithScreenSize, 1920×1080,
  match 0.5, 100 ppu
- GraphicRaycaster `&3817684680613482677`: defaults
- `CombatHud._debugGroup` wire → `1360351997342636089` (prefab :258)
- Author source: `CombatPrefabAuthor.cs:2509-2531` + wire `:2631`
- `CombatPrefabStageHook` auto-hide membership (`"Debug"`) dies with it (D3)

### `Combat_HUD` — destroyed in full

- GameObject `&6552448530280038589`: `m_Name: Combat_HUD`, child index 0
- RectTransform `&3731869733579976421`: same zeroed layout as Debug's,
  children `[5428903171222390665]`
- Canvas `&4006782715438962496`: ScreenSpaceCamera, **sortingOrder 10**
- CanvasScaler `&8998072753662016966` + GraphicRaycaster `&90887311773905656`:
  identical settings to Debug's
- `CombatHud._canvas` wire → `4006782715438962496` (prefab :257)
- **BuffTooltip PrefabInstance `&2548849415120471836`: `m_TransformParent`
  changes `3731869733579976421` → `387827311396018076` (Popups RT), appended
  LAST in Popups' `m_Children`. EVERY other modification preserved verbatim** —
  pivot (0.5, 0), anchors (0.5, 0.5), sizeDelta (320, 110), anchoredPosition
  (0,0), localPosition zero, identity rotation, both `m_sharedMaterial`
  overrides, `m_Name: BuffTooltip`. If any of these change, the edit is wrong.
- Author source: `CombatPrefabAuthor.cs:2456-2474` + wire `:2630`

### Values that survive but lose their anchor (comments re-stated, not deleted)

- `CombatHudPanel`'s `UIDocument.sortingOrder = -10` — number unchanged;
  rationale re-anchored to the outcome overlay (0)
- The 1920×1080 / ScaleWithScreenSize / 0.5 scaler triple survives verbatim on
  `Popups`' CanvasScaler; `UIToolkitInitializer`'s comment referent moves there

## Mandated deltas (TD §3) shipping in the same commit

- **D1**: `CombatHud.BuildCanvas()` DELETED (else the runtime resurrects
  Combat_HUD every session, invisible to the static gate; pre-existing
  ADR-0011 #3)
- **D2**: `BuffTooltipWidget` stops caching the camera VALUE — caches the
  Canvas ref, reads `renderMode`/`worldCamera` at Show time; deletes
  `OnTransformParentChanged`. Without this, authoring the tooltip under Popups
  resurrects the 2026-09 silent-Show bug (null camera cached at Awake).
- **D3**: `CombatPrefabStageHook` drops `"Debug"`.
- **D4**: `PopupsGroup()` deleted; its LogError relocated into
  `EnsureScreenSpaceCameras` as the `_popupsGroup` null-guard's else.
- **D5** (TD rules delete; USER MAY OVERRULE): the tooltip's
  `SetAsLastSibling()` — no-op since all sibling pickers are UIDocuments whose
  order is `UIDocument.sortingOrder`, not hierarchy.

## The gate (TD §4)

`canvas_registry_gate` in `tools/ci/grep-gates.sh`: YAML half (glob all
prefabs+scenes, resolve every `!u!223` → owning GO name via `m_GameObject`,
allow-list of 7 names) + CODE half (AddComponent<Canvas> pinned to exactly
`CombatHud.BuildWorldCanvas` + `DamagePopupSpawner.cs:62`; every
`BuildWorldCanvas("Name"` literal must be allow-listed) + retired-name rows
(`new GameObject("Debug"`, `GameObject("Combat_HUD"`) + anti-vacuity (zero
canvases found ⇒ FAIL; allow-list >7 ⇒ FAIL citing the Amendment A cap).
Negative-tested in 5 steps incl. the nested-PrefabInstance and vacuous-code-
half cases.

## Zero-visual-delta claim

TRUE only with D2 (TD §7.3): the tooltip already lives under Popups at
runtime today — authoring it there directly changes nothing on screen,
provided the camera read is late-bound. One-look playtest owed: hover a buff
chip (tooltip appears, correctly positioned) + damage numbers still rise.

## Technical Director Review

**TD-ARCHITECTURE: APPROVE** — both canvases deleted, one commit, with
blocking deltas D1–D3 (D4 same-commit, D5 discretionary) and the two-half
gate per §4. Corrected premises recorded: `override_sorting_gate` anchors
only to MainBar.prefab (nothing re-points), and the author edit moves the
BuffTooltip Instantiate DOWN (after PartRewardPicker, parented to
popupsGroup) rather than moving Popups creation up — preserving today's
sibling order `[outcome, cardPicker, partPicker, tooltip]`. Full verdict +
three-lens self-audit: `production/td-verdicts/2026-09-17-p5-canvas-deletions.md`.

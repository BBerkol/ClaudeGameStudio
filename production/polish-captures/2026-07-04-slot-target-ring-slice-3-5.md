# Polish Capture — SlotTargetRing Slice 3.5 (HP text + icon/preview-arc retirement)

**Date:** 2026-07-04
**Slice:** SlotTargetRing HUD refactor Slice 3.5
**System:** `SlotTargetRing` widget (player-side per-slot HUD)
**Author:** Claude (Opus 4.7) with user oversight
**Pre-state reference:** Unity commit `24405da` (Slice 3), framework commit `c4f5ec1` (Slice 3 docs)

## What's being edited

Follow-up to Slice 3 (EnemyNumberBadge). User feedback from Slice 3 PlayMode smoke:

> *"Player's widgets will have numbers in the middle as well but as an addition they will have a ring around them that indicates it's the ally side. Yes I see +5 on repair and -5 when taking damage but the widget itself is very non-explanatory with an icon in the middle."*

Slice 3.5 makes the player ring self-explanatory by:
1. **Retiring** `_iconImage` + `SetIcon` + `_previewArcImage` + `_iconDiameterPx` from `SlotTargetRing`.
2. **Retiring** the icon-registry seam at `VehicleBarStack.BindRing` (registry SO stays — `IntentWidget` still consumes it).
3. **Adding** a centered HP text (`TMP_Text _hpText`) to the ring, live-populated by `Refresh()` alongside the existing damage-band fill.
4. **Preserving** everything else the ring does — damage-band color, HideRule state machine, hover halo, offline dim, palette binding.

## Pre-Slice-3.5 authored values (from `24405da`)

### `SlotTargetRing.prefab` — all serialized values on the root MB
- `_palette` — `CombatBarPalette` SO (guid `c612067c785d2ee43a5aa28c2e64e6d9`)
- `_fillImage`, `_outlineImage`, `_iconImage`, `_previewArcImage`, `_targetHoverOutlineImage` — nested Image refs
- `_outerDiameterPx = 40`
- `_outlineThicknessPx = 3`
- `_iconDiameterPx = 28`
- `_offlineDim = 0.4`
- `_hideRule = 3` (`HideOnFullUnlessAttackActive`)

### `SlotTargetRing.prefab` — nested children
| Child | Size | Sprite | Color | Enabled | Notes |
|---|---|---|---|---|---|
| **Fill** | 40−2·3=34, 34 | none | (set at runtime via `PaletteGreen/Yellow/Red`) | 1 | Palette-tinted band fill |
| **Outline** | 40, 40 | (authored ring sprite) | `SubBarMarkerRing` at runtime | 1 | Circle outline; smooth on player, jagged on enemy variants |
| **Icon** | 28, 28 | **null** (runtime push via `SetIcon`) | (1,1,1,1) | 1 | **RETIRE** — center glyph, kills the widget's self-explanatory quality per user |
| **PreviewArc** | 40, 40 | radial-fill sprite (guid `d078a655e73d4834b96ce730f75755cc`) | (1, 0.55, 0.1, 1) — orange | **0 (already disabled)** | **RETIRE** — dead code, disabled in `Awake()`; never re-enabled anywhere in the runtime |
| **TargetHoverOutline** | 40, 40 | (halo sprite) | `SubBarHoverOutline` at runtime | 1 (toggled) | Yellow halo on drag-cast target hover — **PRESERVE** |

### `VehicleBarStack.cs` — icon-registry seam
- Line 86: `[SerializeField] private SlotIconRegistry _slotIconRegistry;` — **RETIRE** (only consumer is line 658)
- Line 658-659: `Sprite icon = _slotIconRegistry != null ? _slotIconRegistry.GetIcon(slot.Kind) : null; ring.SetIcon(icon);` — **RETIRE** (target method gone)

### `CombatPrefabAuthor.cs` — author-time stamping
- Lines 1851-1867: Loads + stamps `_slotIconRegistry` on `VehicleBarStack` — **RETIRE** (field gone)
- `AuthorSlotTargetRing()` — creates + wires Icon + PreviewArc children — **RETIRE** those child seeds; **ADD** TMP text child seed
- **Preserve** loading of `SlotIconRegistryPath` for `IntentWidget` consumers elsewhere in the author (grep confirms IntentWidget authoring still needs it)

### `SlotTargetRingTests.cs`
- Lines 58-59: `InjectField(_ring, "_iconImage", _icon); InjectField(_ring, "_previewArcImage", _previewArc);` — **RETIRE** those injections
- Any test asserting on icon.enabled / SetIcon / preview-arc behavior — **RETIRE**
- **ADD** coverage: HP text follows `Refresh(currentHp, ...)`; text hidden when ring SetActive(false); text stays visible on broken slot (fill goes to bar-bg dim, number stays)

## What Slice 3.5 preserves

- **Damage-band fill** — `_fillImage.color` still set from `ColorForBand(ratio)` on green/yellow/red bands. Palette binding unchanged.
- **Outline** — palette-tinted `_outlineImage`, still swappable per prefab variant (smooth vs jagged).
- **Hover halo** — `_targetHoverOutlineImage` still flipped on by `SetTargetHover(true)` under yellow palette color.
- **HideRule state machine** — all 5 modes (`HideOnDestroyed`, `HideOnFullOrDestroyed`, `AlwaysHidden`, `HideOnFullUnlessAttackActive`, `AlwaysVisible`) preserved. Default stays `HideOnFullUnlessAttackActive`. See §Known follow-up below.
- **Offline dim** — `_offlineDim = 0.4f` still applied to fill+outline on `currentHp <= 0`.
- **Palette binding** — `CombatBarPalette` SO reference + fallback color set intact.
- **Geometry** — outer diameter + outline thickness unchanged. Fill sub-rect math unchanged.
- **Interaction seam** — `ICombatHoverTarget`, `SetTargetHover`, `SetInteractable`, `ClearHandlers`, `OnClicked`, `OnHover` all preserved.
- **VehicleBarStack.BindRing** flow — still per-slot bind, still SetHideRule, still per-frame Refresh loop, still adds ring to `_combatBars` for popup-anchor lookup.
- **`SlotIconRegistry` SO + `IntentWidget` consumer** — registry SO not deleted; only the ring-side seam retires.

## What Slice 3.5 changes destructively

**Fully retired code + prefab children:**
1. `SlotTargetRing.cs`:
   - Fields: `_iconImage`, `_previewArcImage`, `_iconDiameterPx`
   - Methods: `SetIcon(Sprite)`
   - Awake code: `_previewArcImage.enabled = false`, `_iconImage` defensive early-hide
   - `ApplyStaticGeometry`: icon + preview-arc size stamps
2. `SlotTargetRing.prefab`:
   - **Icon** child (RectTransform + CanvasRenderer + Image, 28×28, empty sprite) — deleted
   - **PreviewArc** child (RectTransform + CanvasRenderer + Image, 40×40, orange radial-fill sprite `d078a655…`) — deleted
3. `VehicleBarStack.cs`:
   - `_slotIconRegistry` field (line 86) — deleted
   - Registry lookup + `ring.SetIcon(icon)` call (lines 655-659) — deleted
4. `CombatPrefabAuthor.cs`:
   - Icon child seeding in `AuthorSlotTargetRing` — deleted
   - PreviewArc child seeding in `AuthorSlotTargetRing` — deleted
   - `_slotIconRegistry` stamping in `BuildVehicleHudAnchors` (lines 1851-1867) — deleted
5. `SlotTargetRingTests.cs`:
   - `_iconImage`, `_previewArcImage` field injections — deleted
   - Any test cases asserting on icon or preview-arc behavior — deleted
6. `PlayerVehicle.prefab` — `_slotIconRegistry` ref on `VehicleBarStack` cleared by re-author
7. `SlotTargetRing.prefab` — new `HpText` child added (TMP text, centered, ~22pt bold, color white)

**Additive code:**
- `SlotTargetRing.cs`:
   - `using TMPro;`
   - `[SerializeField] private TMP_Text _hpText;`
   - In `Refresh(int currentHp, int maxHp, DamageState state)`: after visibility check, write `_hpText.text = currentHp.ToString()` (memoized against `_lastHp` to skip redundant writes)
   - In `Awake()`: if `_hpText != null && _hpText.text.Length == 0`, seed with "0" so Prefab Mode preview reads as a real widget
   - `ApplyStaticGeometry`: no size stamp needed (TMP anchor stretch or fixed-size)

## Rollback path

`git revert <slice-3.5-commit>` fully rolls back:
- Deleted code returns; deleted prefab children return (Icon + PreviewArc re-instantiated by AuthorSlotTargetRing).
- `_slotIconRegistry` ref re-stamped on VehicleBarStack by re-running AuthorPlayerVehicle after revert.
- `SlotTargetRingTests` icon/preview-arc assertions restored.

No manual cleanup required. Re-authoring the ring prefab regenerates the child hierarchy.

## Technical Director Review

TD verdict from the pre-compaction session (banked in prior conversation; summarized here for the capture record):

**Q1 (Widget shape convergence — one widget or two?):** *TD verdict re-briefed after user caught that the first brief omitted damage-band fill, HideRule, hover halo, and palette binding.* Fresh verdict: **Option D — keep two categorically-different widgets**. Player ring keeps its full behavior toolkit (band + halo + HideRule + palette); enemy badge stays the simpler HP-on-backdrop widget. Only the icon + preview-arc + registry-seam retire from the ring. **ADR-0011 clean** — no bridges, no bimodal path; each vehicle prefab authors ONE widget shape under each anchor RT (Slice 3's `HasAnyBadge` probe already handles the picker).

**Q2 (Registry SO — delete or keep?):** Grep confirmed `IntentWidget` also consumes `SlotIconRegistry`. Registry SO **stays**; only the `VehicleBarStack._slotIconRegistry` field and its BindRing consumer retire. Zero orphan risk.

**Q3 (HP text color — always white, or drag-cast preview like the badge?):** Player ring's HP text **stays white always**. Player-side drag-cast targeting is against enemy slots, not player slots — there's no "projected damage to player from this card" fantasy in EA scope. Repair-preview on hover is possible follow-up scope (Delta B); NOT in Slice 3.5.

**Q4 (HideOnFullUnlessAttackActive interaction with new HP text):** Default HideRule will hide the entire ring at full HP, which now includes the new HP text. **Keep the current default.** The behavior is intentional: at full HP the player doesn't need a "20/20" reminder cluttering the vehicle. When damaged or when the player starts a drag, the ring (with number) shows. Surface as PlayMode smoke observation — if the user wants the number **always visible** on the player side, switch default to `AlwaysVisible` in a follow-up patch commit.

**Traps addressed:**
- **feedback_capture_before_destroy_view_layer** — this document enumerates every authored value being destroyed BEFORE the code edit.
- **feedback_aggressive_dead_code_cleanup** — `_slotIconRegistry` field + call site both retire together, not just the call site; author-time stamping goes too. Preview arc was already dead-code-disabled in prefab — this cleanup finally deletes it.
- **feedback_edit_prefab_visibility + feedback_executealways_asset_guard** — TMP text seeded with "0" in Awake so Prefab Mode preview reads as real widget; OnValidate guard preserved.
- **feedback_gdd_verb_signature_not_load_bearing** — no new interface; `Refresh(int currentHp, int maxHp, DamageState state)` signature UNCHANGED (HP already passed in); text just piggybacks on the existing call.
- **ADR-0014** — TMP text under UGUI Canvas is the same precedent as the enemy badge's `TextMeshProUGUI`. No new ADR exception.

## Test surface

- `Assets/Tests/EditMode/CombatView/SlotTargetRingTests.cs` — rewrite scope:
   - **Delete:** `_iconImage` / `_previewArcImage` field injections; icon/preview-arc assertions
   - **Add:**
     1. `Refresh_RendersHpText` — Refresh(currentHp=15, maxHp=20, Ok) → `_hpText.text == "15"`
     2. `Refresh_UpdatesHpTextOnChange` — Refresh(20 → 12) → text updates 20 → 12
     3. `Refresh_HpZero_TextShowsZero` — Refresh(0, 20, ...) → `_hpText.text == "0"` (still renders; broken visual carries via fill going bar-bg-dim)
     4. `Refresh_HideRuleHiddenAtFullHp` — HideRule=HideOnFullOrDestroyed + full HP → ring SetActive(false) → HP text hidden by parent SetActive
   - **Preserve:** all existing damage-band fill color assertions, HideRule assertions, halo assertions

## Acceptance criteria

- All EditMode tests pass (SlotTargetRingTests updated + full suite green).
- Slice 3.5 grep gate: no remaining `_iconImage` / `_previewArcImage` / `SetIcon` / `_slotIconRegistry` references in `Assets/Scripts/CombatView/` or `Assets/Tests/` (single `_slotIconRegistry` reference in `IntentWidget.cs` is EXPECTED — different consumer).
- PlayMode smoke:
   - Player ring on each non-armor slot shows centered HP number in white.
   - Number updates when player takes damage or repairs.
   - Damage-band fill color still transitions green → yellow → red per band.
   - Hover halo still fires on drag-cast target.
   - HideRule still hides at full HP unless attack active.
   - Enemy badges unchanged (regression check for Slice 3 baseline).
- Follow-up question surfaced to user post-smoke: *"Now that the ring shows a number, do you want the ring to stay visible at full HP too? Currently HideRule default hides it at full HP."*

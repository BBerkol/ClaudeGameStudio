# Combat UI Prefab Refactor — Inventory & Plan

**Branch**: `feature/combat-ui-prefab-refactor` (Unity repo) off `51bf40d`
**Source repo**: `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\`
**Started**: 2026-04-28
**Owner**: Claude + user (Q→O→D→D→A protocol)

This doc is the **contract** for the refactor. Every widget gets a row before any
code change. Each row lists what the widget builds today, what fields the refactor
exposes, and what hierarchy the prefab will have. No bulk edits — work proceeds
per-widget with a manual editor smoke per wave.

## Goals

- Convert 19 runtime-built combat widgets + the `CombatHud` composite into authored
  Unity prefabs with hierarchies that can be inspected, edited, and rearranged in
  the Editor without touching `.cs`.
- Author `CombatScene.unity` directly (not from CombatHud's programmatic build) so
  Canvas / EventSystem / camera layout are visible in the Hierarchy.
- Preserve the entire 240-test suite green throughout — no regressions.

## Non-goals

- Visual redesign. Tints, fonts, and layout match what `Awake()` builds today.
- New features. Reward picker, outcome overlay, run controls all stay functionally
  identical — only their construction site moves from runtime → asset.
- ScriptableObject expansion. The card / deck / vehicle / balance SOs landed in
  commit 1; this refactor does not add new authoring layers.

## Bind-or-build pattern

Every widget keeps its current `Awake()` build block as a **legacy fallback**.
The refactor adds `[SerializeField]` references for every visual child the widget
currently constructs. `Awake()` checks the reference; if null, it falls back to
the original build path. If wired (prefab is authored), it skips the build.

This means **the runtime keeps working at every step of the refactor**, even if
the prefab isn't authored yet for that widget. We can land a wave of `.cs` edits,
verify tests still pass, then author prefabs incrementally.

### Pattern, illustrated on EndTurnButton (pilot widget)

**Before:**

```csharp
private Image _background;
private TMP_Text _label;

private void Awake()
{
    _background = gameObject.AddComponent<Image>();
    _background.color = InactiveBg;
    _background.raycastTarget = true;

    GameObject labelGo = new GameObject("Label");
    // ... configures RectTransform + TextMeshProUGUI ...
    _label = labelGo.AddComponent<TextMeshProUGUI>();
    _label.text = "END TURN";
}
```

**After:**

```csharp
[SerializeField] private Image _background;
[SerializeField] private TMP_Text _label;

private void Awake()
{
    if (_background == null) BuildLegacy();
}

private void BuildLegacy()
{
    _background = gameObject.AddComponent<Image>();
    _background.color = InactiveBg;
    // ... rest unchanged ...
}
```

The legacy block is **moved verbatim** into `BuildLegacy()` — no behavioural
changes. A null-check on the most distinctive field decides which path runs.

**Field naming convention**: keep existing private field names. Just add
`[SerializeField]`. Avoid renames — they'd inflate the diff and risk subtle
typos. Inspector field labels can be polished later if desired (cheap and safe).

## Per-widget safety procedure

Per widget:

1. Read the current `.cs`.
2. Add `[SerializeField]` to the visual-child fields.
3. Move `Awake()` body into `BuildLegacy()`.
4. Replace `Awake()` body with the null-check guard.
5. Compile (Unity reload). Run **EditMode tests**. Must stay green.
6. (Per wave, not per widget) Manual editor smoke: open scene, hit Play, click
   through one combat. Console must be clean.

If any step fails, **stop**. Don't bulk-fix. Diagnose the single widget that broke
and either revert it or repair it before moving on.

## Wave plan & ordering

Order goes **simple-leaf → complex-leaf → composite → orchestrator**. Each wave
has at most ~5 widgets so a manual smoke test stays bounded.

| Wave | Scope | Widget count | Status |
|------|-------|--------------|--------|
| 1 | Simple HUD leaves: bg + 1-2 text children | 5 | Done — `1cd5ccb` + `dd95634` |
| 2A | Vehicle / card visuals fitting W1 pattern | 4 | Done — `952b48c` |
| 2B | Special-shape: SlotTarget shell + 3 skips | 1 prefab | Done — `36dbffc` |
| 3A | Overlays fitting W1 pattern | 4 | Done — `24a823c` |
| 3B | DamagePopupSpawner composite refactor + RunControlsWidget skip | 1 | Done — `b9bd8f6` |
| 4 | `CombatHud` orchestrator + `CombatScene.unity` (CombatSceneBlockout deferred here) | 1 | TBD before wave |

After Wave 4, prefabs are authored (one `.prefab` per widget), then
`CombatScene.unity` is authored as a YAML file referencing those prefabs as
nested instances.

## Wave 1 — simple HUD leaves (5 widgets)

All five share the pattern: one `Image` background, one or two `TMP_Text` children
in child GameObjects, `Awake()` builds everything, `Bind()` injects a controller
or getter, `Update()` polls and re-tints / re-texts.

### W1.1 — EndTurnButton (PILOT)

- **File**: `Assets/Scripts/CombatView/EndTurnButton.cs`
- **Fields to expose**: `_background` (Image), `_label` (TMP_Text)
- **Children built today**:
  - Self gets `Image` component (`InactiveBg` tint, raycastTarget=true)
  - `Label` GameObject — anchored full-rect, TextMeshProUGUI bold size 22, "END TURN"
- **Prefab hierarchy**:
  ```
  EndTurnButton (Image, EndTurnButton script, RectTransform)
    └─ Label (TextMeshProUGUI, RectTransform full-rect)
  ```
- **Bind contract**: `Bind(CombatController)`. Phase tints applied each frame.
- **Pilot deliverable**: refactor + author `.prefab` + manual click test before any
  other widget moves.

### W1.2 — EnergyOrbWidget

- **File**: `Assets/Scripts/CombatView/EnergyOrbWidget.cs`
- **Fields to expose**: `_background` (Image), `_text` (TMP_Text)
- **Children built today**:
  - Self gets `Image` with `SharedSprites.Circle()` sprite (procedural circle)
  - `Text` GameObject — full-rect TMP, size 38 bold, centered, shows `cur/max`
- **Procedural sprite note**: `SharedSprites.Circle()` builds a procedural texture
  on first access and caches it. Keep the line `_background.sprite =
  SharedSprites.Circle();` in `BuildLegacy()` (still runs when prefab missing) AND
  in a small `Awake()` step that runs **after** the null-check, because the
  authored prefab cannot serialize a procedural sprite — it must be assigned at
  runtime regardless.
- **Prefab hierarchy**:
  ```
  EnergyOrb (Image, EnergyOrbWidget script)
    └─ Text (TextMeshProUGUI, full-rect)
  ```
- **Bind contract**: `Bind(CombatController)`. Hides during Setup/Ended phases.

### W1.3 — PileCountWidget

- **File**: `Assets/Scripts/CombatView/PileCountWidget.cs`
- **Fields to expose**: `_background` (Image), `_labelText` (TMP_Text), `_countText`
  (TMP_Text). Keep `_countRect` derived from `_countText.transform` after assignment.
- **Children built today**:
  - Self `Image` (BgColor)
  - `Label` GameObject — top half (anchor 0,0.5 → 1,1), TMP size 12 bold
  - `Count` GameObject — bottom half (anchor 0,0 → 1,0.5), TMP size 22 bold
- **Animation state preserved**: `PlayDeltaAnimation`, scripted-roll, pulse curve
  all run on the existing `_countText` / `_countRect` references. No change.
- **Prefab hierarchy**:
  ```
  PileChip (Image, PileCountWidget script)
    ├─ Label (TextMeshProUGUI, top half)
    └─ Count (TextMeshProUGUI, bottom half)
  ```
- **Bind contract**: `Bind(string label, Func<int> countGetter)`. Two instances
  exist (Deck, Discard).
- **Reuse note**: instantiate the same prefab twice in scene; `CombatHud` calls
  `Bind` with the appropriate label + getter for each.

### W1.4 — TurnPhaseWidget

- **File**: `Assets/Scripts/CombatView/TurnPhaseWidget.cs`
- **Fields to expose**: `_background` (Image), `_turnText` (TMP_Text), `_phaseText`
  (TMP_Text)
- **Children built today**:
  - Self `Image` (BgNeutral)
  - `TurnText` — top half, TMP size 30 bold ("TURN N" / "READY" / "VICTORY")
  - `PhaseText` — bottom half, TMP size 18 normal ("your turn" / "enemy turn" / etc.)
- **Phase-driven background tints**: 5 colors switched in `Update()` based on
  `CombatPhase` + `CombatWinner`. No change — runs on the same `_background` ref.
- **Prefab hierarchy**:
  ```
  TurnPhaseBanner (Image, TurnPhaseWidget script)
    ├─ TurnText (TextMeshProUGUI, top half)
    └─ PhaseText (TextMeshProUGUI, bottom half)
  ```
- **Bind contract**: `Bind(CombatController)`.

### W1.5 — AmbushBannerWidget

- **File**: `Assets/Scripts/CombatView/AmbushBannerWidget.cs`
- **Fields to expose**: `_background` (Image), `_label` (TMP_Text)
- **Children built today**:
  - Self `Image` (BgColor — burnt orange)
  - `Label` — full-rect (12px / 4px inset), TMP size 14 bold, fixed text
    "[AMBUSH] — enemy struck before turn 1"
- **Visibility**: toggled via `_background.enabled` + `_label.enabled` (NOT
  `gameObject.SetActive`) so `Update()` keeps running across encounter changes.
  This stays the same after refactor.
- **Prefab hierarchy**:
  ```
  AmbushBanner (Image, AmbushBannerWidget script)
    └─ Label (TextMeshProUGUI, full-rect with 12/4 inset)
  ```
- **Bind contract**: `Bind(CombatController)`. Visible only when
  `Loop.Encounter == EncounterType.Ambush`.

## Wave 2 — vehicle / card visuals

Split into two batches based on shape:

| Sub-wave | Widgets | Status | Commit |
|----------|---------|--------|--------|
| 2A | FrameTarget, ResourceBar, Intent, Card | Done | `952b48c` |
| 2B | SlotTarget (shell-only) + 3 documented skips | Done | `<this commit>` |

`CombatSceneBlockout` deferred to Wave 4 (scene root, SpriteRenderer-based — fits scene authoring better than widget authoring).

### W2A — fits Wave 1 pattern cleanly (4 widgets — landed in `952b48c`)

Same shape as Wave 1: bg + N text/image children, all built in Awake. Bind-or-build refactor + `[SerializeField]` exposure straightforward.

| Widget | SerializeFields | Prefab path |
|--------|-----------------|-------------|
| FrameTargetWidget | `_selectionTint` (Image) | `FrameTarget.prefab` |
| ResourceBarWidget | `_background`, `_fillImage`, `_label` | `ResourceBar.prefab` |
| IntentWidget | `_background`, `_primaryText`, `_targetText` | `Intent.prefab` |
| CardWidget | `_background`, `_costText`, `_nameText`, `_infoText` | `Card.prefab` |

`ResourceBarWidget._fillRt` derived from `_fillImage.transform` every Awake — saves a SerializeField for a value that's recoverable. `CombatPrefabAuthor` got an `ActivateRecursively` helper (ResourceBar has a grandchild: FillArea > Fill).

### W2B — special-shape widgets

#### W2B.1 — SlotTargetWidget (shell-only refactor)

- **File**: `Assets/Scripts/CombatView/SlotTargetWidget.cs`
- **Fields exposed**: `_background` (Image), `_icon` (Image)
- **NOT exposed**: `_hpBar`, `_armorBar` — these are `ResourceBarWidget` instances built in `Bind()` (not Awake) because the slot kind isn't known until Bind, and only the Frame slot needs the second armor bar.
- **Why shell-only is correct**: the bar `AddComponent<ResourceBarWidget>()` calls hit ResourceBarWidget's `BuildLegacy` path. Refactoring `SlotTarget.BuildBar` to instantiate `ResourceBar.prefab` (via `PrefabUtility.InstantiatePrefab` or Resources) would be a pure build-time change with no runtime benefit — the legacy code path is fine.
- **Prefab hierarchy**:
  ```
  SlotTarget (Image, SlotTargetWidget script)
    └─ Icon (Image, anchored middle-LEFT, pivot left-center)
  ```
- **Bind contract**: `Bind(CombatController, Func<Vehicle>, SlotKind, BuffTooltipWidget?, string)`. Bars and tooltip wiring all happen here.

#### W2B.2 — BuffStripWidget — **PARTIAL REFACTOR DEFERRED → Wave 6 (full prefab refactor landed)**

- **File**: `Assets/Scripts/CombatView/BuffStripWidget.cs`
- **Original W2B rationale**: static `Spawn(...)` did `parent.gameObject.AddComponent<BuffStripWidget>()` — widget was added to an existing canvas RectTransform, never instantiated as a standalone GameObject. Per-badge `Rebuild()` is dynamic-by-design (signatures, counts, colours read from `StatusBadge`).
- **Wave 6 outcome**: full prefab refactor landed (per user direction "go with our new designer-friendly approach"). 6 consts → SerializeField (4 colours + 2 sizes); static `Spawn` removed; new `BuffStrip.prefab` asset; CombatHud now Instantiate's the prefab as a child of the runtime-built `BuffStripCanvas` (one per vehicle); `_buffStripPrefab` SerializeField on CombatHud carries the asset reference. Per-badge `Rebuild()` stays dynamic — only the lifecycle and tunable values changed.
- **Follower-registration fix**: strip is now a child of the canvas (was on the canvas GO itself pre-W6). CombatHud registers `_enemyBuffStrip.transform.parent` as the vehicle follower so world-space tracking still pins to the canvas root.

#### W2B.3 — CardFlightWidget — **PARTIAL REFACTOR DEFERRED → Wave 6 (full prefab refactor landed)**

- **File**: `Assets/Scripts/CombatView/CardFlightWidget.cs`
- **Original W2B rationale**: pure throwaway. `Spawn()` created a fresh GameObject, tweened 0.35s, `Destroy(gameObject)`. Building from scratch every spawn matched the lifecycle.
- **Wave 6 outcome**: full prefab refactor landed. 4 consts → SerializeField (durations + fade-curve + 2 colours); `_background` and `_nameText` now `[SerializeField]` and wired by the prefab; `Spawn(prefab, parent, from, to, name)` Instantiate's the asset and runs `Init` to set anchored position + name text + apply tunable colours. Lifecycle still spawn-tween-destroy; the only change is that the visual hierarchy comes from `CardFlight.prefab` instead of `new GameObject` + `AddComponent` chains.
- **Designer-tunable surface**: flight duration, fade onset, background colour, name colour. Author route mirrors the rest of the wave: temp GO → AddComponent → wire SerializeFields → SaveAsPrefabAsset.

#### W2B.4 — VehiclePositionAnimator — **PARTIAL REFACTOR LANDED IN WAVE 6**

- **File**: `Assets/Scripts/CombatView/VehiclePositionAnimator.cs`
- **Original W2B rationale**: pure behaviour driver, no UGUI children, no build path. Already flat.
- **Wave 6 outcome**: 2 consts → SerializeField (`_tweenDurationSec`, `_overtakeDipUnits`). No prefab needed (the script lives on the vehicle quad GameObject inside `CombatSceneBlockout.prefab`); designers tune the values in Prefab Mode on the blockout's vehicle quads.

## Wave 3 — overlays / popups

Split into two sub-waves based on shape:

| Sub-wave | Widgets | Status | Commit |
|----------|---------|--------|--------|
| 3A | DamagePopup, DebugStats, CombatLog, BuffTooltip | Done | `24a823c` |
| 3B | DamagePopupSpawner (composite) + RunControlsWidget (skip) | Done | `b9bd8f6` |

### W3A — fits Wave 1 pattern (4 widgets — landed in `24a823c`)

| Widget | SerializeFields | Prefab path | Notes |
|--------|-----------------|-------------|-------|
| DamagePopupWidget | `_label` (TMP_Text on root GO) | `DamagePopup.prefab` | Self-TMP — script + TextMeshProUGUI both on the same root GameObject. `Application.isPlaying` guards the pool-deactivate in Awake (otherwise editor authoring saves the prefab inactive → Instantiate returns inactive instance → Awake never fires → `_rect` null → Show NPE). |
| DebugStatsWidget | `_background`, `_text` | `DebugStats.prefab` | Standard pattern. |
| CombatLogWidget | `_background`, `_headerText`, `_bodyText` | `CombatLog.prefab` | Standard pattern. |
| BuffTooltipWidget | `_background`, `_header`, `_body` | `BuffTooltip.prefab` | Had no Awake before — `Spawn()` called `InitChildren()` manually. Refactor adds Awake that caches host canvas refs + bind-or-build + runtime-only HideAll. Spawn() now ends after `AddComponent` (4-line factory). |

### W3B — DamagePopupSpawner composite + RunControlsWidget skip (pending)

#### W3B.1 — DamagePopupSpawner (composite refactor)

- **File**: `Assets/Scripts/CombatView/DamagePopupSpawner.cs`
- **Why composite, not leaf**: builds its own ScreenSpaceOverlay Canvas (sortingOrder=30) + a fixed pool of 8 `DamagePopupWidget` instances. The widget instances themselves are now prefab-authorable (W3A landed); the spawner becomes a thin orchestrator that `Object.Instantiate(damagePopupPrefab, _canvasRect)` × 8.
- **Fields to expose**: `_canvas` (Canvas root — the sortingOrder=30 overlay), `_damagePopupPrefab` (DamagePopupWidget reference). Keep `_pool` derived (built in Awake from instantiating the prefab N times).
- **Bind-or-build branch**: prefab path instantiates `_damagePopupPrefab` 8× under `_canvas.transform`; legacy path runs the existing programmatic build (unchanged).
- **Action when wave starts**: re-read `DamagePopupSpawner.cs`, confirm sortingOrder + canvas mode, draft Author routine + prefab hierarchy spec, then refactor.

#### W3B.2 — RunControlsWidget — **SKIP PREFAB, no code refactor**

- **File**: `Assets/Scripts/CombatView/RunControlsWidget.cs`
- **Why skip**: debug-only panel built in `Bind()` (not Awake) because layout depends on runtime data — debug damage button reads `_controller.Balance.DebugDamageAmount`, and the encounter-pill row is built dynamically per `EncounterType` enum value. Authoring a prefab would require either freezing the enum-driven layout (locks it to today's values) or emitting placeholder children that get destroyed and rebuilt on Bind (defeats the purpose).
- **Why not refactor anyway**: this is debug UI — it doesn't ship in a release build, doesn't need designer-authoring, and the dynamic layout from `Bind` is the right shape for a debug tool that adapts to enum changes.
- **Action**: leave as-is. CombatHud Wave 4 keeps the existing programmatic instantiation.

## Wave 4 — CombatHud orchestrator (TBD before wave)

`CombatHud.cs` is currently 1606 lines and builds the entire screen
programmatically: Canvas, EventSystem, all child widgets, the reward picker, the
outcome overlay, the combat log panel, the debug stats toggle. The refactor
flattens this into a thin orchestrator:

- `[SerializeField]` references to every composed child widget (already authored
  in Waves 1-3).
- `Awake()` keeps a `BuildLegacy()` fallback that does the full programmatic
  build for any null reference. This is large but mechanical: existing helper
  methods inside `CombatHud` become the legacy build steps.
- `Bind(CombatController)` continues to fan out to every child's `Bind(...)`
  call. No change in dispatch.

**Action when wave starts**: re-read `CombatHud.cs` (1606 lines), enumerate every
field that gets assigned in `Awake()` / build helpers, and write W4 plan with the
full `[SerializeField]` list.

## Wave 6 — designer-editable closure pass

After Waves 1-5 landed (16 prefabs + SampleScene wired), three widgets were
deliberately skipped in W2B with the rationale "lifecycle doesn't fit the
prefab+Instantiate shape" (BuffStrip lived as `AddComponent` on a runtime canvas
GameObject; CardFlight built a fresh GameObject every spawn; VPA was a flat
behaviour driver). Wave 6 revisits them through a designer-friendly lens: even
when the *lifecycle* fits, the question is whether the *tunable surface* (size,
colours, durations) is exposed for designers to iterate without touching code.

The user's directive — **"from now on go with our new designer friendly
approach"** — locks in the full prefab+Instantiate path as the project default
for any future view-layer refactor where designer-tunable values exist. This
supersedes the earlier W2B "skip if lifecycle doesn't fit" heuristic.

| Sub-wave | Widget | Refactor shape | Rationale |
|----------|--------|----------------|-----------|
| W6.1 | VehiclePositionAnimator | const-lift only (2 fields) | No UGUI children, no spawn lifecycle — pure behaviour driver. Lives on a vehicle quad inside `CombatSceneBlockout.prefab`; designers tune in Prefab Mode. Full asset-prefab would be ceremony with no payoff. |
| W6.2 | BuffStripWidget | full prefab refactor | 6 SerializeFields (sizes + colours), removed static `Spawn()`, `Bind()` retained for closure injection. New `BuffStrip.prefab` (stretch-to-fill RectTransform + script, no children — icons built dynamically in `Rebuild`). CombatHud now `Instantiate(_buffStripPrefab, canvasGo.transform, false).Bind(...)`. **Topology change**: strip is now child-of-canvas instead of canvas-itself, so `RegisterFollower(side, _enemyBuffStrip.transform.parent)` (was `.transform`) — the canvas is the parent. |
| W6.3 | CardFlightWidget | full prefab refactor | 4 SerializeFields (durations + 2 colours), `_background` + `_nameText` now prefab-wired children. New `CardFlight.prefab` (110×160 bottom-center anchored, Image root + "Name" TMP child). New `Spawn(prefab, parent, from, to, name)` Instantiate's the asset; `Init` re-applies tunable colours so SerializeField is the source of truth. CombatHud holds `_cardFlightPrefab` and forwards through every spawn. |
| W6.4 | Plan doc | this update | Replace W2B.2/W2B.3/W2B.4 "skip" rationale rows with "Wave 6 outcome" rows; add this section + Decision log entry. |
| W6.5 | Smoke + commit | in-editor | Run `Tools > Wasteland Run > Author Combat Prefabs`, EditMode 240/240 green, Play SampleScene, walk one combat. Commit Wave 6 on `feature/combat-ui-prefab-refactor`. |

**Bootstrap-from-zero contract change**: pre-W6, `BuildLegacy()` could
`AddComponent` the widget onto a fresh runtime GameObject. After W6, BuffStrip
and CardFlight MUST be either inspector-wired (CombatHud.prefab carries the
ref) or Author-routine-wired. Both call sites in CombatHud now `LogError` and
fail-fast with actionable text directing to the Tools menu. Acceptable because
SampleScene already consumes `CombatHud.prefab` via W4C.

## Phase 6 — Author prefabs + CombatScene.unity (after Wave 4)

For each widget, author a `.prefab` directly as YAML at:
- `Assets/Prefabs/CombatView/EndTurnButton.prefab`
- `Assets/Prefabs/CombatView/EnergyOrb.prefab`
- ... (one per widget) ...
- `Assets/Prefabs/CombatView/CombatHud.prefab` (composite — references the others)

Then author `Assets/Scenes/Combat/CombatScene.unity` as YAML with:
- Main Camera
- EventSystem (standalone GameObject so the Canvas can reference it)
- A single `CombatHud` prefab instance under a Canvas
- The `CombatController` GameObject + `CombatSceneBlockout` instance for the
  chase-rail world geometry

YAML authoring is **direct file write**, not an editor script. We'll use the
`stripped` prefab variant pattern Unity emits — base prefab GUID + per-instance
overrides. Hand-authored YAML for fresh prefabs is straightforward; the only
risk is FileID + GUID collision, which we mitigate by authoring one prefab and
verifying it loads cleanly before authoring the next.

## Phase 7 — In-editor smoke test

After all prefabs + scene are authored:

1. Unity reload, watch console — must be clean (no missing-script warnings, no
   null refs from any widget's `Awake`).
2. Hit Play in `CombatScene.unity`.
3. Walk one full combat: ambush turn, plays a card, ends turn, takes enemy turn,
   triggers a Reposition, runs a Patch (auto-target verify), kills the enemy,
   sees the outcome overlay, dismisses.
4. Console clean throughout.

If smoke clean → squash-merge the branch back to `main`. (User decides
squash vs. preserve — discuss before merge.)

## Risks & mitigations

| Risk | Mitigation |
|------|-----------|
| A widget's prefab is wired but a child reference is missed → null ref at runtime | The bind-or-build guard checks the **most distinctive** field. If it's wired, all siblings should be too. Add asserts on the prefab path: `Debug.Assert(_label != null, "...")` per widget after Awake. |
| TMP `font` assignment depends on `TMP_Settings.instance` being present at runtime — prefab might serialize a stale font | Keep `if (TMP_Settings.instance != null) tmp.font = TMP_Settings.defaultFontAsset;` in BOTH BuildLegacy and the post-build Awake step (idempotent — no-op if font already set on prefab). |
| Procedural sprites (SharedSprites.Circle) can't be serialized into a prefab | Assign them in Awake() AFTER the null check — runtime always sets the procedural ref, prefab leaves the field empty. |
| `CombatHud` build order is significant (some widgets depend on others being constructed first) | The `BuildLegacy()` cascade preserves the existing order verbatim. Prefab path: every child is already built (it's a saved asset), so order doesn't matter. |
| Prefab YAML authoring breaks Unity import (bad GUID, wrong FileID) | Author one prefab, reload Unity, verify it imports cleanly + appears in Project window before authoring the next. |
| 240-test green stays a moving baseline (more tests added during the refactor) | Run tests **before** every wave commit. If count drifts, re-baseline before continuing. |

## Backout strategy

Every wave is one or more commits on `feature/combat-ui-prefab-refactor`. To
abandon the refactor:

```
git checkout main
git branch -D feature/combat-ui-prefab-refactor
```

Nothing on `main` changed — the three pre-refactor commits (0ffe8a4 / 1550426 /
51bf40d) are independent of this work.

## Decision log

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-04-28 | Branch off `51bf40d` (post-tire-art commit), not earlier | All pre-refactor work belongs on `main` so the branch starts from a clean baseline; tests already 240/240 green at this commit. |
| 2026-04-28 | Bind-or-build pattern (`if (_field == null) BuildLegacy();`) | Lets us refactor `.cs` and run tests independently of authoring the prefab. Failure mode is "still works programmatically", never "broken everywhere". |
| 2026-04-28 | Wave 1 = 5 simplest widgets, EndTurnButton as pilot | Pilot exercises the entire pipeline (refactor → prefab YAML → scene wire → manual click test) on the smallest possible surface before scaling. |
| 2026-04-28 | Keep procedural-sprite assignments in Awake unconditionally | Procedural sprites can't serialize into prefab YAML; assigning post-null-check is idempotent and safe. |
| 2026-04-29 | Default to full prefab+Instantiate path for view-layer refactors (Wave 6 directive) | User: "from now on go with our new designer friendly approach". Supersedes the W2B "skip if lifecycle doesn't fit" heuristic — exposing designer-tunable surface (sizes, colours, durations) wins over minimal-diff const-lift even when the lifecycle is throwaway/dynamic. Const-lift-only is the exception, reserved for cases where no asset prefab is meaningful (e.g., VehiclePositionAnimator, which lives inside another prefab). |

## Open questions

None — pilot-first plan covers the unknowns. Will re-evaluate after EndTurnButton
end-to-end completes.

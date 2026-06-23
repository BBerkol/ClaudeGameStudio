# MapView Authoring Surface — Designer-Editable Icons, Background, Path Style

**Date:** 2026-06-22
**System:** Run-scene MapView (UXML/USS + controller + line element)
**Trigger:** Block 3 eyeball-pass session. User played the closed Slice 6 + Block 1/2/3 generator loop, observed graph correctness, then surfaced an authoring-surface request: "the map prefab should be in editable form so i can change the icons and the background. if possible i would also like to be able to edit the path line we make in the inspector aswell." Hop-distance tuning deferred — the data flag (`_maxHopDistance: 280`) is already exposed for later iteration; today's slice is the missing *visual* authoring surface.

---

## The Final-Game Picture This Slice Serves

A complete Biome 1 ships per-beacon-type artwork (Combat = wreckage silhouette, Haven = banner, Start = arrow, etc.), a biome-distinctive background image on the map canvas, and a path-line whose stroke colour / width / dash rhythm matches the chosen art direction. **Each biome gets its own authored visual package** alongside its existing `BiomeDistributionSO` (Biome 2 = different palette, different icons, different background). Designers iterate the visual identity **in Inspector + USS**, not by recompiling.

ADR-0015 is the binding meta: scope narrows via the `BiomeDistributionSO` data table, not via code branches. Today's slice extends that table with a `MapTheme` field, treating per-biome map visuals as another instance of "configuration narrowing." That's the second concrete application of ADR-0015 (first was beacon-type subsetting; this is theme subsetting). No new pattern, no new abstraction — same shape applied to a sibling concern.

---

## What's Being Destroyed (Capture Surface)

Single destructive surface: **`BeaconNodeElement.cs`** removes the placeholder letter-text codepath that today renders the first character of the enum name inside each beacon (e.g. `'C'` for Combat, `'H'` for Haven). Lines being deleted, captured here verbatim:

```csharp
// CONSTRUCTOR (lines 71-73 of BeaconNodeElement.cs):
_typeLabel = new Label();
_typeLabel.AddToClassList("wr-beacon__label");
Add(_typeLabel);
```

```csharp
// Bind() method (lines 120-123):
// Type indicator: first character of the enum name.
// Start→'S', Combat→'C', Haven→'H', EliteCombat→'E', Merchant→'M', etc.
string typeName = vm.Type.ToString();
_typeLabel.text = typeName.Length > 0 ? typeName[0].ToString() : "?";
```

Also being deleted:
- Field declaration `private readonly Label _typeLabel;` (line 33)
- USS rules `.wr-beacon__label` (line 77-81 of MapView.uss) and per-variant overrides at lines 111-113, 131-133, 151-153, 163-165, 195-198 — all label-coloring overlays

**Not destroyed (preserved):**
- The `wr-beacon--type-*` class derivation in `BeaconNodeElement.Bind` (lines 93-107). Per-type tinting stays on the USS side; the sprite swap is layered on top.
- Every state class (`wr-beacon--current` / `--resolved` / `--reachable`).
- All click + hover behaviour.
- Every `ConnectionLineElement` rendering behaviour. Commit #3 only **lifts the constants out** — the Painter2D dashed-bezier walk is preserved exactly.

The letter-text was a **placeholder** by intent (per `BeaconNodeElement.cs` xmldoc, lines 6-22). No designer ever tuned a value here. The destructive cost is zero; the capture is required by the hook because the refactor touches a system-shape carrier and crosses the ≥50-line threshold across commits 2+3.

---

## Three-Commit Slice Plan

### Commit 1 (additive) — MapBeaconStyleSO + biome wiring

**Asmdef placement (corrected from initial draft):** `MapBeaconStyleSO` lives in `Assets/Scripts/Run/Authoring/` (namespace `WastelandRun.Run.Authoring`) — same assembly as `BiomeDistributionSO`. The original draft proposed `Assets/Scripts/UI/Authoring/` but that subdirectory + asmdef don't exist, and the dependency direction (`BiomeDistributionSO` references the theme SO) rules out a UI-owned placement without inverting the assembly arrow. Co-locating with `BiomeDistributionSO` is the natural place for a data table the biome owns.

- **New file:** `Assets/Scripts/Run/Authoring/MapBeaconStyleSO.cs`
  - `[CreateAssetMenu(menuName = "Wasteland Run/Run/Map Beacon Style")]`
  - `[SerializeField] private Sprite[] _beaconIconsByType = new Sprite[8];` — length pinned to `Enum.GetValues(typeof(BeaconType)).Length` (8). `OnValidate` resizes + warns if drift.
  - `[SerializeField] private Sprite _backgroundImage;`
  - Public read-only accessors: `Sprite BeaconIcon(BeaconType type)` (range-check by `(int)type`), `Sprite BackgroundImage`.
  - **No** per-type code switch. Index-by-enum-value is the canonical ADR-0015 narrowing shape.
- **Edit:** `Assets/Scripts/Run/Authoring/BiomeDistributionSO.cs`
  - Add `[SerializeField] private MapBeaconStyleSO _mapTheme;`.
  - Public accessor `MapBeaconStyleSO MapTheme => _mapTheme;` — nullable; controller falls back.
- **Edit:** `Assets/Scripts/UI/WastelandRun.UI.asmdef`
  - Add `"WastelandRun.Run.Authoring"` to the `references` array (one-line additive change). UI already references `WastelandRun.Run` POCO; this widens the same arrow to include the authoring sibling so `MapViewController` can hold a typed `MapBeaconStyleSO` parameter. No new bridge, no asmdef created.
- **Edit:** `Assets/Scripts/CombatView/RunSceneHost.cs`
  - Add public accessor `MapBeaconStyleSO ActiveMapTheme => _biomeDistribution != null ? _biomeDistribution.MapTheme : null;`. `CombatView` already references `Run.Authoring` so no asmdef change here.
- **Edit:** `Assets/Scripts/UI/MapViewController.cs`
  - `Bind(INodeMapView)` widens to `Bind(INodeMapView map, MapBeaconStyleSO theme = null)`. Cached on a private field so commit #2's per-beacon icon read can use it without re-threading.
  - `OnEnable` adds a `_canvas = root.Q<VisualElement>(className: "wr-map-canvas")` query.
  - New `ApplyCanvasBackground()` private method sets `_canvas.style.backgroundImage` from `_theme.BackgroundImage` (or clears it via `StyleKeyword.Null` if null).
  - **Fallback rule (lagging-dep):** if `theme` is null *or* `BackgroundImage` is null, the canvas keeps its USS `background-color` only. No broken render, no warning spam.
- **Edit:** `Assets/Scripts/UI/RunSceneOverlayHost.cs`
  - `_mapView.Bind(_host.Session.Controller)` becomes `_mapView.Bind(_host.Session.Controller, _host.ActiveMapTheme)`.
- **New asset:** `Assets/Resources/Run/Biomes/Biome1MapTheme.asset` paired with `Biome1Distribution.asset`. All 8 sprite slots + background slot left empty for now (placeholders to be authored after this slice; lagging-dep flag pattern per `feedback_data_flag_lagging_dependency.md`).
- **Edit:** `Biome1Distribution.asset` YAML — add the `_mapTheme` reference pointing at the new asset's GUID.
- **No edit:** `Assets/UI/MapView.uss` — `.wr-map-canvas` keeps its `background-color` token; controller layers `background-image` over it when a theme is bound. Unity stacks background-image over background-color natively.

### Commit 2 (DESTRUCTIVE — this capture covers it)

- **Edit:** `Assets/Scripts/UI/BeaconNodeElement.cs`
  - Delete `_typeLabel` field + construction + `Bind` letter-text code (lines 33, 71-73, 120-123).
  - Construction no longer adds any child — beacon root *is* the sprite host.
  - `Bind` calls a new parameter overload: `Bind(BeaconViewModel vm, Sprite icon)`. Sets `style.backgroundImage = new StyleBackground(icon)` when non-null; leaves style unchanged otherwise (USS tint visible underneath).
  - Existing `wr-beacon--type-*` class derivation **stays exactly as-is** — colour rules remain a USS concern. Sprite is a layer above colour.
- **Edit:** `Assets/Scripts/UI/MapViewController.cs.RebuildBeacons`
  - Passes resolved `Sprite icon = theme?.BeaconIcon(beacons[i].Type)` into each `BeaconNodeElement.Bind` call.
- **Edit:** `Assets/UI/MapView.uss`
  - Delete `.wr-beacon__label` rule + every per-variant `.wr-beacon__label` override (5 selectors).
  - Keep all `.wr-beacon--type-*` colour rules (the colour-tint layer survives — it's the fallback when icon is null and the foundation for the sprite when icon is present).

### Commit 3 (additive) — ConnectionLineElement USS custom properties

- **Edit:** `Assets/Scripts/UI/ConnectionLineElement.cs`
  - Replace `DefaultStroke` / `DashLengthPx` / `DashGapPx` / `StrokeWidthPx` constants with `resolvedStyle.GetCustomProperty` reads inside `OnGenerateVisualContent` against:
    - `--wr-map-connection-stroke` (Color)
    - `--wr-map-connection-width` (Length → px)
    - `--wr-map-connection-dash` (Length → px)
    - `--wr-map-connection-gap` (Length → px)
  - **Hard fallbacks** if the custom property is missing: same literal values today (0xe0/0xe0/0xe0, 2px, 8px, 6px). This is *not* a bridge — it's the failure mode of an unauthored token, which is permitted by the ADR-0011 #4 polymorphism-via-data shape (USS = data table).
  - `ArcLiftFraction` + `SampleCount` stay `const` — they're algorithmic shape, not styling. Lifting them would invite designer tuning of curve sample resolution, which is a footgun.
  - Drop the unused `Color strokeColor` constructor overload (no caller uses it; `MapViewController.cs:255` always uses the default).
- **Edit:** `Assets/UI/Tokens/tokens.colors.uss` (add):
  ```css
  --wr-map-connection-stroke: var(--wr-color-text);
  ```
- **New file** *(or extend existing)*: `Assets/UI/Tokens/tokens.map.uss` (small file dedicated to map-specific tokens — keeps `tokens.spacing.uss` clean):
  ```css
  --wr-map-connection-width: 2px;
  --wr-map-connection-dash: 8px;
  --wr-map-connection-gap: 6px;
  ```
- **Note in `ConnectionLineElement.cs` xmldoc:** Remove the "POLISH BACKLOG (Slice 6 closeout capture)" line (lines 25-26) — that backlog item is what this commit resolves.

---

## ADR Alignment Checklist

| ADR | Check | Status |
|---|---|---|
| **ADR-0011** (no bridges at done) | No bimodal path. USS-tint layer is the fallback, sprite is the layered authoring surface. Both paths exist as the **canonical** shape — same as how `wr-beacon--current` overrides `wr-beacon--type-combat` colour today. | ✅ |
| **ADR-0014** (UI Toolkit primary; UGUI for world-space popups only) | MapView stays UI Toolkit. No UGUI involvement. | ✅ |
| **ADR-0015** (configuration narrowing) | `MapBeaconStyleSO` is a data table indexed by `BeaconType` (no switch). `MapTheme` field on `BiomeDistributionSO` makes per-biome theme a narrowing of the same data shape. **Second concrete application of ADR-0015** (first was beacon-type subsetting). | ✅ |
| **ADR-0002** (POCO Run assembly, no engine refs) | `MapBeaconStyleSO` lives in `WastelandRun.Run.Authoring` (co-located with `BiomeDistributionSO`). UI assembly arrows into `Run.Authoring` to consume the typed reference — same direction as the existing UI → Run arrow, no inversion. POCO `WastelandRun.Run` assembly is untouched. | ✅ |

**Recommended (not blocking):** ADR-0015 amendment noting `MapTheme` as the second narrowing application. Defer to next session unless a clean amendment block is obvious — the principle itself is already in the ADR.

---

## Out of Scope (Deferred)

- **Hop-distance tuning.** `_maxHopDistance: 280` on `Biome1Distribution.asset` is already iterable in Inspector. Tune visually once the new beacon sprites land — the visual baseline shifts when sprite size ≠ current `--wr-space-xl` chip size.
- **ArcLift / SampleCount.** Algorithm shape, not authoring surface.
- **Per-beacon-instance overrides** (e.g. "this specific Haven gets a banner sprite, all others get the default icon"). Not in scope — the slice authors at the per-biome level. If a future story needs per-instance overrides, that's a `BeaconViewModel.IconOverride` field, not a change to today's data shape.
- **MapView.uxml topology changes.** Layers stay as-is. Background image applies as a style on `.wr-map-canvas`.
- **Animated stroke phase ("crawling ants")**, **hover glow**, **layered outline.** All listed in `ConnectionLineElement` xmldoc as polish backlog. None gated by today's slice — once USS custom properties drive the static values, animation work can layer on top via additional custom properties without re-shaping the element.

---

## Technical Director Review

**Verdict (2026-06-22):** Proceed with three-commit slice plan as scoped above.

### Categorical fit

`BeaconNodeElement` is a **visual representation** of a `BeaconViewModel`. Today's letter-text codepath is the *only* component that smells off-category — the element claims to be "the beacon chip" but is currently doing the placeholder labeller's job. Replacing it with a sprite restores categorical alignment (a beacon chip should *look like* a beacon, not display its own enum name). USS `wr-beacon--type-*` colour classes remain in-category (the chip *is* coloured by its type, regardless of whether a sprite is layered on top). ConnectionLineElement is in-category for its file purpose (paint a dashed bezier); the constants today live in code, but the moment a designer wants to iterate them, USS is the authoring surface that matches the file's category. Lift is in-category.

### Edit cadence

- **MapBeaconStyleSO:** edited per biome (≤8 biomes in 1.0 plan). Slow cadence, infrequent churn — SO is correct.
- **BiomeDistributionSO.MapTheme:** edited once per biome at creation. Same cadence as the existing distribution fields. Correct.
- **Sprite assignments inside MapBeaconStyleSO:** edited as art lands. Iteration cadence — Inspector-tuneable.
- **ConnectionLineElement constants:** edited maybe twice during the entire visual-direction pass, then frozen. USS custom properties are the right cadence (USS reload doesn't recompile).
- **ArcLift / SampleCount:** edited maybe never. Const is correct.

The cadence stack is coherent: const for never-touched, USS-var for occasionally-touched, SO for per-biome-authored, Inspector for art-iteration. No category collision.

### Per-biome scoping (ADR-0015 narrowing)

`BiomeDistributionSO.MapTheme` is the canonical narrowing surface. Biome 1 ships with its own `Biome1MapTheme.asset`; Biome 2 will ship with `Biome2MapTheme.asset`. **No** "DefaultTheme" SerializeField on the controller — the lagging-dep pattern says that when a biome's theme is missing or partially authored, the fallback is the existing USS-tinted chip (no sprite). This is the *same* shape as how `_combatArchetypes` is permitted to be empty on a biome that doesn't ship Combat beacons. The fallback is not a bridge — it's the canonical "this theme slot is unauthored" rendering.

### Destructive-cost audit

Zero. The letter-text path was a placeholder with no designer-authored values. The class names + state-modifier classes are preserved verbatim. Hover / click / pointer-enter contracts unchanged. View-model contract (`BeaconViewModel`) unchanged.

### Test coverage

The existing MapViewController EditMode coverage exercises the controller's beacon-element-creation path. Adding a sprite-injection step does not require new tests — the assertion surface (which beacons are visible, which are reachable, click routing) is unchanged. If commit #2 lands and existing tests stay green, the destructive cost is verified.

### Recommendation

Ship as **one slice, three commits**: (1) `MapBeaconStyleSO` + `BiomeDistributionSO.MapTheme` + biome 1 placeholder asset wiring, (2) `BeaconNodeElement` sprite rebuild (destructive — this capture file covers it), (3) `ConnectionLineElement` USS-vars + token additions.

Commit-by-commit lets each step land with a single concern. Commit 1 is purely additive and unblocks designer iteration on the SO even before sprites exist (Inspector array slot visible). Commit 2 is the only destructive step and is captured here. Commit 3 is additive + a documented refactor of compile-time constants into USS — zero risk surface.

---

## Approval

This capture awaits user approval before any code changes. After approval:

1. Commit 1 lands first (additive, low-risk). Tests stay green by definition.
2. Commit 2 lands (destructive — letter-text removed). Run EditMode tests.
3. Commit 3 lands (USS custom property lift). Run EditMode tests.
4. Push Unity to BBerkol/Wasteland-Run after commit 3.
5. Framework capture commit (this file) lands separately on framework `main` — local-only, per the 23-commit deferred-push policy.

# BeaconNode UXML Template Lift — Designer-Editable Beacon Prefab

**Date:** 2026-06-23
**System:** Run-scene MapView leaf element (`BeaconNodeElement` + new `BeaconNode.uxml`)
**Trigger:** Designer pair-session 2026-06-23 (UI Builder walkthrough on `MapView.uxml`). After learning UI Builder edits the screen-level prefab visually, the designer asked: *"can the beacons themselves be a prefab I can edit visually too, with data slots the code fills?"* TD verdict obtained pre-slice — approves principle with three sharpening pushbacks (template on controller not biome SO, single `#icon` slot only, no screen-identity SO extension). Full verdict embedded below under `## Technical Director Review`.

---

## The Final-Game Picture This Slice Serves

Every UI screen in Wasteland Run ships as a visual prefab a designer edits in UI Builder, with named slots the controller fills with runtime data. Per-biome SOs supply the *theme content* (sprites, colors, strings); the prefab supplies the *layout structure*. Designer never opens a `.cs` file to change a screen's look. The pattern extends cleanly across MerchantView, EventView, RestView, ChopshopView, HavenView, RunCompleteView card-pickers as each lands.

Today's slice completes that pattern at the **leaf-element level** for the map screen — `MapView.uxml` was already the screen-level prefab (named slots `#beacons-layer` / `#connections-layer` / `#map-title`); `BeaconNodeElement` was the only remaining UI structure built in C# rather than authored in UXML. Lifting it to a template + `#icon` slot closes that gap and establishes the convention every subsequent leaf-element slice will follow.

---

## What's Being Destroyed (Capture Surface)

### Pre-edit `BeaconNodeElement.cs` — constructor (lines 67-78)

```csharp
/// <summary>
/// Constructs the element, adds the <c>wr-beacon</c> base class, and
/// registers the click callback.
/// </summary>
public BeaconNodeElement()
{
    AddToClassList("wr-beacon");

    RegisterCallback<ClickEvent>(OnClick);
    RegisterCallback<PointerEnterEvent>(OnPointerEnter);
    RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
}
```

### Pre-edit `BeaconNodeElement.cs` — sprite injection inside `Bind` (lines 124-130)

```csharp
// Layer the per-biome sprite on top of the USS tint. Clearing to
// StyleKeyword.Null when icon is missing lets the pooled element
// recycle cleanly between Binds without retaining a prior sprite.
if (icon != null)
    style.backgroundImage = new StyleBackground(icon);
else
    style.backgroundImage = StyleKeyword.Null;
```

### Pre-edit USS rules that MUST continue to apply to the new template root (`MapView.uss`)

All 13 selectors stay live and must hit the new template root verbatim — verified by ensuring the UXML root carries the `wr-beacon` class:

- `.wr-beacon` (line 53) — base chip layout (absolute, 24x24, centered via translate, border, radius)
- `.wr-beacon--type-start` (81)
- `.wr-beacon--type-combat` (89)
- `.wr-beacon--type-elitecombat` (97)
- `.wr-beacon--type-merchant` (105)
- `.wr-beacon--type-chopshop` (113)
- `.wr-beacon--type-event` (121)
- `.wr-beacon--type-rest` (129)
- `.wr-beacon--type-haven` (137)
- `.wr-beacon--reachable` (147)
- `.wr-beacon--current` (160)
- `.wr-beacon--resolved` (174)
- `.wr-beacon:hover` (179)

**Not destroyed (preserved verbatim):**
- All three event delegates (`OnClicked`, `OnHoverEnter`, `OnHoverExit`) and their subscribers in `MapViewController`.
- All three event registrations (`ClickEvent`, `PointerEnterEvent`, `PointerLeaveEvent`) — they continue to register on the typed element wrapper, not on the template root.
- Pooled-element lifecycle: `MapViewController._beaconPool` + `OnDisable`'s unsubscribe sweep.
- `userData = vm.BeaconIndex` click-routing seam.
- `_isAdjacentToCurrent` click/hover gating.
- `style.left` / `style.top` percent positioning (still applied to the element wrapper, not the template).
- Every state class application in `Bind` (current / resolved / reachable / type-tint).

The destructive cost is **near-zero authored value**: the element's C# constructor today carries no designer-tuned values — only a class-list add and three event registrations. The capture exists because the slice crosses the ≥50-line threshold and touches a system-shape carrier.

---

## What Ships (Slice Contents)

### New file — `Assets/UI/BeaconNode.uxml`

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements" editor-extension-mode="False">
    <ui:VisualElement class="wr-beacon">
        <ui:VisualElement name="icon" class="wr-beacon__icon" />
    </ui:VisualElement>
</ui:UXML>
```

Minimal by design (TD recommendation): single `#icon` slot, no speculative children. `#frame` / `#state-indicator` / `#label` are deliberately omitted — every named slot is a controller obligation (Q lookup, null-guard, write path). New slots get added when authored content for them appears (lagging-dep pattern applied to UXML structure).

Designer can add unnamed decorative children to the UXML in UI Builder at will (frame chrome, hover glow ring, etc.) — controller never queries them so they remain pure visual ownership.

### New USS rule (`MapView.uss`)

```css
.wr-beacon__icon {
    position: absolute;
    left: 0; right: 0; top: 0; bottom: 0;
}
```

Fills the chip; the per-biome sprite goes onto this child's `backgroundImage` instead of the chip's own background, so the chip's USS tint stays visible as foundation.

### Edited file — `Assets/Scripts/UI/BeaconNodeElement.cs`

- New private field: `private readonly VisualElement _icon;`
- Constructor signature widens to accept `VisualTreeAsset template`:
  ```csharp
  public BeaconNodeElement(VisualTreeAsset template)
  {
      template.CloneTree(this);
      _icon = this.Q<VisualElement>("icon");
      if (_icon == null)
          Debug.LogError("[BeaconNodeElement] template missing #icon child — designer reparented or renamed it. Bind will be a no-op for icons.");

      RegisterCallback<ClickEvent>(OnClick);
      RegisterCallback<PointerEnterEvent>(OnPointerEnter);
      RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
  }
  ```
- `Bind`'s sprite-injection block writes to `_icon.style.backgroundImage` instead of `this.style.backgroundImage`. Null-clear path mirrors today's `StyleKeyword.Null` discipline so pooled elements recycle cleanly.
- Parameter-less constructor **removed**. There are zero callers other than `MapViewController` (verified by grep) — no migration shim needed, ADR-0011 clean.

### Edited file — `Assets/Scripts/UI/MapViewController.cs`

- New SerializeField: `[SerializeField] private VisualTreeAsset _beaconTemplate;`
- `RebuildBeacons` instantiation line changes from `new BeaconNodeElement()` to `new BeaconNodeElement(_beaconTemplate)`.
- One null-guard at the top of `RebuildBeacons` (or `Bind`): if `_beaconTemplate == null`, log warn + early-return so the missing-asset case fails loud (matches the existing `_beaconsLayer == null` guard).

### Inspector wire-up (one-time)

After the slice lands, designer must drag `Assets/UI/BeaconNode.uxml` onto the `_beaconTemplate` slot of the `MapViewController` component in the Combat prefab's `MapView` GameObject. CombatPrefabAuthor.cs will need a one-line update to bake this so future Author Combat Prefab runs survive a prefab rebuild. (Per `feedback_bake_designer_edits.md`.)

---

## Three-Pushback Compliance (TD Verdict → Implementation)

| Pushback | TD recommendation | Implementation |
|---|---|---|
| Template location | Controller SerializeField, NOT biome SO | ✅ `MapViewController._beaconTemplate` |
| Slot count | `#icon` only; no speculative `#frame` / `#state-indicator` / `#label` | ✅ `BeaconNode.uxml` ships single `#icon` slot |
| Screen-identity SO extension | DO NOT BUILD — `MapView.uxml` + tokens are already the authoring surface | ✅ no `MapBeaconStyleSO` field additions in this slice |

---

## ADR Alignment Checklist

| ADR | Check | Status |
|---|---|---|
| **ADR-0011** (no bridges at done) | Single source of truth for structure (`BeaconNode.uxml`); single source for theme content (`MapBeaconStyleSO`). No parallel storage, no bimodal paths. The parameterless ctor is removed cleanly (no compat shim). | ✅ |
| **ADR-0014** (UI Toolkit primary; UGUI for world-space popups only) | Completes the screen-level pattern at the leaf level. UI Toolkit only. | ✅ |
| **ADR-0015** (configuration narrowing) | Untouched. `MapBeaconStyleSO` continues to narrow icon **content** only; structural template is not narrowing data and correctly lives outside the biome SO. | ✅ |
| **ADR-0002** (POCO Run assembly, no engine refs) | Template ref lives in UI assembly on the controller; Run + Run.Authoring assemblies untouched. | ✅ |

---

## Out of Scope (Deferred per TD)

- **Screen-identity SO extension** (`CanvasColor` / `TitleText` / `TitleColor` / `OuterBackgroundColor`). DO NOT BUILD — would create bimodal authoring path with USS. The designer's screen-identity ask is resolved by the UI Builder coaching that preceded this slice.
- **`#frame` / `#state-indicator` / `#label` slots.** Add incrementally when authored content for them exists (lagging-dep pattern). `#label` is vestigial — letter-text was retired in `7363038`.
- **Template references on `MapBeaconStyleSO`.** Structure is project-wide, not per-biome. If a future biome legitimately needs a structurally different beacon, that's a `BeaconChipVariantSO` polymorphism decision then — not anticipated now.
- **Base class for UXML-template leaf elements.** TD: do not establish a base class until the third screen ships and real commonalities/differences are visible. Use file-naming convention only (`<Name>.uxml/.uss/Controller.cs`).
- **Migrating other elements** (ConnectionLineElement, etc.) to template form. Connection lines are Painter2D-painted; they don't fit the prefab model. Stay USS-driven.

---

## Test Plan (TD Pre-Slice Requirement)

- **Existing EditMode coverage** — `MapViewController.Bind` tests assert on behavior (which beacons visible, which reachable, click routing). No test reaches into `BeaconNodeElement` internals (grep confirmed zero hits in `Assets/Tests`). All existing tests stay green by construction.
- **New EditMode test** — `BeaconNodeElement_IconClearsOnNullRebind`:
  1. Construct a `BeaconNodeElement` with a minimal `VisualTreeAsset` synthesised at test-time (or load `BeaconNode.uxml` from Resources/test fixture path).
  2. `Bind(vm, someSprite)` → assert `_icon.resolvedStyle.backgroundImage` reflects the sprite.
  3. `Bind(vm, null)` → assert `_icon.resolvedStyle.backgroundImage` is the StyleKeyword.Null default.
  
  This pins the pooled-element-recycling contract that the lift mustn't regress.
- **Target**: EditMode 505 / 504 passed / 0 failed / 1 pre-existing skip.

---

## Unity 6.3 Spike (TD Risk Mitigation)

Before committing, verify in a 10-minute manual check that `[UxmlElement]` partial-class generation co-exists cleanly with `template.CloneTree(this)` in the ctor — Unity 6.3 introduced source-generator-driven UxmlElement codegen and there's a non-zero risk of ordering issues. If the spike reveals issues, the fallback is to drop the `[UxmlElement]` attribute (the element is only instantiated by the controller, not declared in any UXML, so the attribute is decorative at this point — removing it has zero functional cost).

---

## Technical Director Review

**Verdict (2026-06-23):** APPROVE the template-lift principle with three sharpening pushbacks. Ship as one narrowly-scoped slice.

### Is template + slot the right shape?

Yes — this is the Unity 6.3 idiom for designer-editable composite elements and how UI Builder is designed to be used. Alternatives rejected:
- *Per-screen ViewLayoutSO* (data-driven layout config) — wrong category. UXML *is* the layout config; an SO layer over it duplicates the authoring surface and violates ADR-0011 #2 (parallel storage).
- *Scriptable view configs* — same trap.
- *Keep C#-built, expose more `[UxmlElement]` attributes* — keeps designer locked out of structural decisions. Solves nothing.

The template + named-slot pattern has one job: let designers own structure (UXML) and theme content (SO), while the controller owns the binding lifecycle. That's the exact separation ADR-0014 implies but hasn't yet enforced at the leaf-element level. `MapView.uxml` already does this at the screen level — extending downward.

**Nuance**: `BeaconNodeElement` should remain a `[UxmlElement]` and continue to be instantiable in code via `new BeaconNodeElement(template)`. The constructor calls `template.CloneTree(this)` and `Q<>`-caches the named children. The element is **its own root** — do NOT have the controller instantiate the template into an anonymous `VisualElement`; the typed wrapper is what the controller pools and unsubscribes via `OnDisable`.

### Where does the template reference live?

**Project-wide `SerializeField` on `MapViewController` — NOT on `MapBeaconStyleSO`.**

- `MapBeaconStyleSO` claims to be **per-biome theme content**. Biome 1 has rusty-tin chips; Biome 2 has neon chips; the *layout of the chip* is the same.
- A `VisualTreeAsset` template claims to be **the chip's structure** — where the icon sits, what decoration surrounds it. Structure doesn't change between biomes.

Putting the template on the per-biome SO would force every biome to re-author (or duplicate) the chip structure. That's the inverse of ADR-0015's narrowing intent. ADR-0015 narrows *content* by biome, not *structure*. Structure is screen-shape, not biome-shape.

If (later) a biome legitimately needs a different chip structure, that's a separate `BeaconChipVariantSO` polymorphism decision then. Don't anticipate now — ADR-0015 explicitly says "do not anticipate narrowing surfaces."

### Categorical-fit on `BeaconNode.uxml`

The prefab claims to be: **"the visual shape of one beacon chip on the run map."** Audit of every proposed named child against that claim:

| Child | Aligns? | Notes |
|---|---|---|
| `#icon` | Yes | Per-type sprite layer. Core to chip identity. |
| `#frame` | **Smell** | If purely decorative, name is a lie (named slots imply controller binding). Don't ship. |
| `#state-indicator` | Conditional | Only if authored asset exists this slice. USS class-toggle handles state today. Don't ship. |
| `#label` | No | Letter-text retired in `7363038`. Vestigial slot — ADR-0011 #4 territory. |

**Minimal slot set v1: just `#icon`.** Every named slot is a controller obligation — don't pay that cost speculatively. UXML tree can hold unbounded unnamed decorative structure; UI Builder is happy to paint it, controller never touches it.

### Screen-level visual identity (black bg, grey canvas, "RUN MAP" title)

**Option (a) — `MapView.uxml` IS the prefab. Coach the designer.** Already executed pre-slice via UI Builder walkthrough.

DO NOT extend `MapBeaconStyleSO` with `CanvasColor` / `TitleText` / etc. (option b). Two reasons:
1. Bimodal authoring path (USS or SO; controller picks winner). ADR-0011 #2 + #3 fire.
2. Canvas color is *screen* identity, not biome identity. Biome 1 and Biome 2 are both still "the run map screen."

The per-biome `_backgroundImage` on `MapBeaconStyleSO` stays — it's biome-varying *content*, not screen identity. That's the right boundary.

### ADR-0015 fit on adding a `VisualTreeAsset` field

Confirmed: structural template ≠ narrowing data. ADR-0015's pattern is content-shaped (which subset emits). A `VisualTreeAsset` doesn't narrow a set; it specifies how to render. Different categories. Keeping the template on `MapViewController` keeps the two SOs in their proper lanes.

No ADR amendment needed.

### Slice shape & ordering

**One slice, scoped narrowly. Not two.**

In scope: new `BeaconNode.uxml` with `#icon` slot, `BeaconNodeElement.cs` ctor changes, `MapViewController.cs` template ref.

Out of scope: screen-identity SO extension (option b — categorical mistake), other slots (#frame / #state-indicator / #label — speculative), other screens (let pattern stabilize), base class (premature abstraction).

**Destructive cost audit**: near-zero authored value. Capture pre-existing ctor + Bind body, surviving USS rules, pooling/event-unsubscribe lifecycle contract. Get user approval, then ship.

**Test discipline**: existing EditMode tests assert behavior, not internals — grep confirmed zero `BeaconNodeElement` references in `Assets/Tests`. Add one new test: bind with icon, re-bind with null, assert backgroundImage cleared.

### Future-proofing

**Let the pattern emerge. Do NOT establish a base class now.** Six projected screens have different binding lifecycles, VM shapes, pooling needs. Forcing a base class today is anticipatory abstraction.

What to do as each ships:
1. Same naming convention: `<ScreenName>.uxml/.uss/Controller.cs` with kebab-case slot ids.
2. Document the pattern in ADR-0014 Phase B (or amendment) after the second screen ships — by then real commonalities/differences are visible.
3. Resist pre-emptive pooling. Different screens have different shapes.

After the third screen, lift a base class if obvious. Until then, three concrete controllers with consistent naming will be cleaner than one base class with three subclasses.

### Risks

| Risk | P | I | Mitigation |
|---|---|---|---|
| Pooled re-Bind leaks prior sprite | M | M | Existing null-clear via `StyleKeyword.Null` discipline applies to `_icon.style.backgroundImage`. Test pins this. |
| Designer reparents `#icon`, breaks `Q<>` | L | H | Controller's lookup logs Debug.LogError on null — fail-loud, not silent. |
| Unity 6.3 `[UxmlElement]` + template ctor ordering | L | M | 10-min spike before committing. Fallback: drop `[UxmlElement]` (zero functional cost — element is controller-instantiated, not UXML-declared anywhere). |
| Slice grows mid-flight (designer requests #frame) | M | L | Acceptable. Add slots incrementally as content lands — lagging-dep pattern applied to UXML structure. |

### ADR alignment summary

| ADR | Status |
|---|---|
| ADR-0011 (no bridges) | Clean. Single source for structure, single source for theme. No parallel storage, no bimodal paths. |
| ADR-0014 (UI Toolkit primary) | Reinforces. Leaf-element completion of screen-level pattern. |
| ADR-0015 (narrowing via SO) | Untouched. Template doesn't enter ADR-0015's domain. |
| ADR-0002 (no engine refs in Run POCO) | Untouched. |

### Edit cadence

Once-and-done structural lift. Post-slice cadence separation:
- **Daily** (designer): edit `BeaconNode.uxml` in UI Builder.
- **Per biome** (designer): edit `MapBeaconStyleSO` sprite array.
- **Never again** (programmer): touch the element ctor for structure reasons. Touch only when binding contract changes (new VM field, new event).

### Pre-slice capture requirements (met above)

- ✅ Pre-edit snapshot of `BeaconNodeElement.cs` constructor + Bind body.
- ✅ Pre-edit list of all `.wr-beacon*` USS rules (13 selectors).
- ✅ Minimal `BeaconNode.uxml` (single `#icon` slot).
- ✅ Confirmation pooling + event-unsubscribe lifecycle preserved verbatim.
- ✅ EditMode test plan including new "icon clears on null-rebind" test.

Ship it.

---

## Approval

This capture awaits user approval before any code changes. After approval:

1. 10-min Unity 6.3 `[UxmlElement]` + template ctor spike. Fail-fast if ordering issues.
2. Implementation (one commit):
   - New `BeaconNode.uxml` + `.meta`.
   - `BeaconNodeElement.cs` constructor + Bind edits.
   - `MapViewController.cs` SerializeField + instantiation thread-through.
   - `MapView.uss` adds `.wr-beacon__icon` rule.
   - New EditMode test `BeaconNodeElement_IconClearsOnNullRebind`.
3. Run EditMode batchmode → expect 505/504/0/1.
4. Run grep-gates → expect clean.
5. Designer drags `BeaconNode.uxml` onto `MapViewController._beaconTemplate` in the Combat prefab + saves; CombatPrefabAuthor.cs gets a one-line update to bake the reference.
6. Atomic commit.

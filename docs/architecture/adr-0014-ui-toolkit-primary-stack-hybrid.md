# ADR-0014: UI Toolkit as Primary Stack, UGUI Retained for World-Space Popups Only

## Status

Accepted (2026-06-13)

## Date

2026-06-13

## Last Verified

2026-06-13

## Decision Makers

User (BertanBerkol), Claude (technical-director-equivalent session)

## Summary

Wasteland Run adopts Unity UI Toolkit (UXML + USS + C# controllers) as
the primary stack for all screens, menus, HUD, the Slice 6 node-map
view, the Run Complete view, and every panel landed from this ADR
forward. UGUI is retained for **one** axis: the world-space `Popups`
canvas (damage numbers, status popups that anchor to live world
transforms). The split is axis-aligned — world-space vs screen-space —
not two ways to solve the same problem; it does not violate the
ADR-0011 bimodal-paths rule. A five-phase migration plan with a CI grep
gate sequences the transition without re-breaking shipped combat feel.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | UI |
| **Knowledge Risk** | HIGH — UI Toolkit production-ready for runtime UI is a Unity 6 claim; verified against Unity 6.3 docs 2026-06-13 |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, Unity 6.3 What's New, UI Toolkit runtime docs |
| **Post-Cutoff APIs Used** | UI Toolkit runtime panel system (`UIDocument`, `PanelSettings`, `RuntimePanelUtils`); USS hot-reload at runtime (6.3 capability); `ListView` data binding |
| **Verification Required** | (a) UXML hot-reload behavior in Play mode on 6.3 LTS, (b) gamepad focus routing across multiple `UIDocument` panels, (c) `RuntimePanelUtils.ScreenToPanel` per-frame cost vs UGUI WorldSpace Canvas at expected damage-number density |

> **Note**: Knowledge Risk is HIGH. If the project upgrades past Unity 6.3 LTS,
> re-validate UI Toolkit production-readiness claims and feature parity gaps
> documented in this ADR before continuing the migration plan.

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0002 (combat-system event pattern — forbidden-pattern rule extends to UI), ADR-0011 (no-bridges meta-rule — hybrid scope must remain axis-aligned, not bimodal) |
| **Enables** | Slice 6 (node-map view), every subsequent screen/menu/HUD panel in the run-loop, merchant, chopshop, event, and rest epics |
| **Blocks** | Slice 6 cannot start coding until Phase 1 (USS design tokens + base controls) lands |
| **Ordering Note** | Phase 1 of this ADR must land before any Slice 6 view code is authored. Phase 4 (Combat_HUD migration) must respect the capture-before-destroy protocol per `production/polish-captures/README.md`. |

## Context

### Problem Statement

Slice 6 of the Run Loop epic introduces the first screen-space UI surface
that is not part of combat: the node-map view. Earlier slices shipped
combat-only UI (`Combat_HUD`, `Popups`, `Debug`, `CardRewardPicker`,
`CombatOutcomeOverlay`) all in UGUI. M2 expansion adds Merchant, Chopshop,
Event, Rest, Title, Settings, Save/Load slot picker, and a branching node
map with variable rows, hover previews, and animated reveals.

The technical-director review on 2026-06-13 surfaced that picking UGUI for
Slice 6 means either (a) rewriting the entire screens layer in UI Toolkit
during the eventual migration (≈9–11 dev-days plus regression risk on
shipped combat feel), or (b) committing to UGUI through 1.0 and absorbing
the dynamic-layout cost on every M2 list/branching UI. Unity 6.3 LTS
positions UI Toolkit as the recommended stack for new runtime UI and
ships USS hot-reload at runtime, which UGUI cannot match.

The decision must land *before* Slice 6 code is authored, or Slice 6 ships
in a tech that the rest of the run-loop UI will then migrate away from.

### Current State

| Surface | Tech | Status |
|---------|------|--------|
| `Combat_HUD` (HP, armor, intent, energy, hand, deck/discard, hover) | UGUI Canvas (sortingOrder 10) | Shipped, Slice-of-combat tuned |
| `Popups` (damage numbers, status popups, world-anchored) | UGUI Canvas (sortingOrder 60) | Shipped |
| `Debug` (dev overlays) | UGUI Canvas (sortingOrder 110) | Shipped |
| `CardRewardPicker` | UGUI | Shipped Slice 5b |
| `CombatOutcomeOverlay` | UGUI | Shipped |
| Node-map view (Slice 6) | None — to author | Blocked on this ADR |
| Run Complete view (Slice 6) | None — to author | Blocked on this ADR |
| Merchant / Chopshop / Event / Rest views (M2+) | None — to author | Future |
| Title / Settings / Save-Load slot picker | None — to author | Future |

Zero UI Toolkit in the project today. Zero UXML, zero USS.

### Constraints

- **Performance budget:** 60fps target, 16.6ms frame, 200 draw calls. 2D
  card game is UI-heavy; UI cost matters and is visible in profile.
- **Combat feel must not regress.** Combat_HUD has been tuned via
  Prefab-Mode iteration; capture-before-destroy protocol applies to any
  migration of authored values.
- **Solo dev + AI assist.** Migration cost is real and lands on one
  person; phased plan with low-risk early phases is required.
- **No `UnityEvent` in combat systems** (ADR-0002 / technical-preferences).
  This ADR ports the rule forward to the UI layer: no `UnityEvent`
  *anywhere* under the new stack.
- **ADR-0011 no-bridges meta-rule.** Two parallel ways to author the same
  surface (UGUI panel and UI Toolkit panel for the same screen) is the
  textbook bimodal pattern forbidden by #4. The hybrid scope must be
  axis-aligned by problem domain, not "either-or per panel."

### Requirements

- All new screens, menus, HUD, map, and panel surfaces use UI Toolkit.
- World-space damage numbers and status popups remain on UGUI WorldSpace
  Canvas — the per-frame `RuntimePanelUtils.ScreenToPanel` conversion is
  materially more expensive than UGUI's direct WorldSpace Canvas and
  there is no offsetting benefit at the density expected.
- Existing UGUI surfaces (`Combat_HUD`, `CardRewardPicker`,
  `CombatOutcomeOverlay`) migrate to UI Toolkit on a scheduled plan, not
  in a rolling refactor.
- USS design tokens (typography, color palette, spacing scale, radii)
  are authored once and referenced by every UXML panel — no inline
  styling on production panels.
- A CI grep gate forbids `Canvas` components introduced outside the
  `Popups` canvas subtree once Phase 5 lands.

## Decision

UI Toolkit is the primary UI stack for Wasteland Run. UGUI is retained
**exclusively** for the world-space `Popups` canvas, where it stays
indefinitely because the world-anchoring problem is materially simpler in
UGUI and the alternative (per-frame `RuntimePanelUtils.ScreenToPanel`
conversion) is more expensive without compensating benefit.

### Architecture

```
                 ┌──────────────────────────────────────────┐
                 │           RunSceneHost (scene)            │
                 └──────────────────────────────────────────┘
                          │              │              │
            ┌─────────────┘              │              └───────────────┐
            ▼                            ▼                              ▼
   ┌─────────────────┐         ┌──────────────────┐         ┌─────────────────────┐
   │  Combat.prefab  │         │  MapView.prefab  │         │ RunCompleteView.pref│
   │                 │         │                  │         │                     │
   │ ┌─────────────┐ │         │  UIDocument      │         │  UIDocument         │
   │ │ Combat_HUD  │ │         │  + UXML + USS    │         │  + UXML + USS       │
   │ │ (UGUI → UI  │ │         │  + Controller    │         │  + Controller       │
   │ │  Toolkit P4)│ │         │  (Phase 2)       │         │  (Phase 2)          │
   │ └─────────────┘ │         └──────────────────┘         └─────────────────────┘
   │ ┌─────────────┐ │
   │ │  Popups     │ │  ← UGUI WorldSpace Canvas, kept forever
   │ │  (UGUI)     │ │     (axis-aligned exception — not bimodal)
   │ └─────────────┘ │
   │ ┌─────────────┐ │
   │ │  Debug      │ │  ← UGUI; dev-only, migrates with HUD in Phase 4
   │ └─────────────┘ │
   │ ┌─────────────┐ │
   │ │ CardReward  │ │  ← UGUI today; migrates Phase 3
   │ │   Picker    │ │
   │ └─────────────┘ │
   │ ┌─────────────┐ │
   │ │ CombatOut-  │ │  ← UGUI today; migrates Phase 3
   │ │ comeOverlay │ │
   │ └─────────────┘ │
   └─────────────────┘
```

### Key Interfaces

```csharp
// Every UI Toolkit panel under this ADR follows the same skeleton:
public sealed class MapViewController : MonoBehaviour
{
    [SerializeField] private UIDocument _document;
    [SerializeField] private PanelSettings _panelSettings; // shared
    private VisualElement _root;

    public event Action<int> NodePicked;        // outbound: C# event
    public event Action SkipRequested;

    private void OnEnable()
    {
        _root = _document.rootVisualElement;
        // Inbound: RegisterCallback<T>, never UnityEvent
        _root.Q<Button>("skip-btn").RegisterCallback<ClickEvent>(_ => SkipRequested?.Invoke());
    }

    public void Bind(IReadOnlyList<BeaconData> beacons, int currentIndex) { /* … */ }
}
```

### Binding Rules

1. **All new screens, menus, HUD, map, and panel surfaces are UXML + USS
   + C# controller.** No new UGUI `Canvas` may be added outside the
   `Popups` canvas subtree.
2. **No `UnityEvent` anywhere under this stack.** Inbound: UI Toolkit
   `RegisterCallback<T>`. Outbound: C# `Action` / `event` on the
   controller. This ports the ADR-0002 combat-systems forbidden-pattern
   rule forward to the UI layer.
3. **USS design tokens are the single source for typography, color,
   spacing, and radii.** Authored once in `Assets/UI/Tokens/` and
   referenced by every UXML panel. No magic colors or pixel sizes
   embedded in panel USS.
4. **One `PanelSettings` asset per render target.** All runtime
   `UIDocument` panels share it; bespoke panel settings require an
   exception note in the prefab's USS header comment.
5. **World-space UI stays UGUI.** Damage numbers, status popups, and any
   future world-anchored element belong in the `Popups` canvas subtree.
   `RuntimePanelUtils.ScreenToPanel` is not the right tool at the
   expected density.
6. **Editor-only tools are out of scope.** Custom Inspector previews,
   debug overlays in EditMode, EditorWindow panels — Editor UI is
   unaffected by this ADR.

### Implementation Guidelines

- **Phase boundaries are hard.** Do not migrate a Phase-N surface during
  Phase N-1 work. Each phase ships in its own commit with its own
  capture file when destructive.
- **Capture-before-destroy applies fully** to Phase 3 and Phase 4
  migrations. Every authored value (color, padding, font size, sprite
  reference, animation curve) is enumerated in a capture file before
  the UGUI panel is replaced. This is non-negotiable per the user
  memory `feedback_capture_before_destroy_view_layer.md`.
- **Tests:** UI Toolkit controllers expose their `VisualElement` root
  via an internal accessor for EditMode tests. Tests assert on data
  binding and event wiring, not visual layout (USS layout is verified
  by playtest screenshot per the test-evidence rubric).
- **Hot-reload:** USS hot-reload is enabled in Play mode for iteration
  speed. Production builds disable it (default).
- **Asmdef:** UI Toolkit controllers live in `WastelandRun.UI` (new
  asmdef) with one-way arrow `WastelandRun.UI → WastelandRun.Run` and
  `WastelandRun.UI → WastelandRun.Combat`. `WastelandRun.CombatView` gains
  a ref to `WastelandRun.UI` once the first Combat_HUD migration phase
  starts (Phase 4).

## Migration Plan

| Phase | Scope | Lands | Risk |
|-------|-------|-------|------|
| **P1** | USS design tokens + base controls (button, label, panel, scrollable list). `PanelSettings` asset authored. `WastelandRun.UI` asmdef created. | Before Slice 6 code | LOW — additive |
| **P2** | Slice 6 node-map view + Run Complete view authored UI Toolkit native. No UGUI map ever exists. | Slice 6 | MEDIUM — first real UI Toolkit work, learning curve |
| **P3** | Migrate `CardRewardPicker` + `CombatOutcomeOverlay` to UI Toolkit. Both are leaf surfaces with limited authored tuning. | M1 polish (post-Slice 6) | LOW–MEDIUM — capture-before-destroy required |
| **P4** | Migrate `Combat_HUD` (HP, armor, intent, energy, hand, deck/discard, hover) to UI Toolkit. Highest-risk migration; combat feel must not regress. | M1.5 dedicated migration slice | HIGH — exhaustive capture-before-destroy required; full playtest pass before merge |
| **P5** | CI grep gate: forbid `Canvas` components in any prefab outside the `Popups` canvas subtree (and `Debug`, retired in P4). Pattern matches ADR-0010 Phase 5 + ADR-0013 salt-uniqueness gate precedent. | Same commit as P4 close | LOW |

**Rollback plan:** Each phase ships in its own commit. If P2 reveals a
UI Toolkit blocker (gamepad routing gap, performance cliff at the
expected density, runtime panel bug), revert P2 + P1 in two commits and
file an ADR-0014 amendment with the specific finding. P3 and P4 inherit
the rollback shape per phase.

## Alternatives Considered

### Alternative 1: Stay UGUI for the entire project

- **Description:** Slice 6 authors the node-map in UGUI; M2's Merchant /
  Chopshop / Event / Rest / Title / Settings views all UGUI. UI Toolkit
  never enters the project.
- **Pros:** No learning curve. No migration cost. Combat_HUD never touched.
- **Cons:** M2 branching map and list-driven Merchant/Event views are
  multiple-times more code in UGUI (RectTransform + LayoutGroup +
  pooled prefabs + manual `LayoutRebuilder.ForceRebuildLayoutImmediate`
  vs USS flex + `ListView` binding). No USS hot-reload. Unity 6.3
  positions UI Toolkit as the recommended runtime stack — staying UGUI
  swims against the engine's own guidance for the entire 1.0 lifecycle.
- **Estimated Effort:** Zero now, ~3–5 dev-days extra per M2 list/branching panel.
- **Rejection Reason:** TD verdict — Unity 6.3 makes UI Toolkit the
  right primary stack; M2 UI surface inventory makes the long-run cost
  of staying UGUI larger than the migration cost of switching now.

### Alternative 2: Migrate everything to UI Toolkit before Slice 6

- **Description:** Pause Slice 6. Migrate `Combat_HUD`,
  `CardRewardPicker`, `CombatOutcomeOverlay`, `Debug` to UI Toolkit
  first. Then start Slice 6 with the entire project on one stack.
- **Pros:** Cleanest end-state. No phased plan to track.
- **Cons:** Combat_HUD migration is the highest-risk migration in the
  project — it's the most authored-value-dense view layer in the game.
  Doing it first, before any UI Toolkit experience accumulates, is the
  wrong sequencing. Combat feel regressions during the migration block
  Slice 6 indefinitely.
- **Estimated Effort:** ~9–11 dev-days upfront, Slice 6 blocked.
- **Rejection Reason:** Risk ordering wrong. Phase P2 (node-map) is the
  right place to learn the stack on a greenfield surface, before
  touching shipped combat code.

### Alternative 3 (CHOSEN): Hybrid — UI Toolkit primary, UGUI for world-space Popups

- **Description:** This ADR.
- **Pros:** Greenfield UI Toolkit work first (lowest risk). Combat_HUD
  migration scheduled for M1.5 dedicated slice with full
  capture-before-destroy. World-space damage numbers stay where they
  work best. ADR-0011 compliance maintained via axis-aligned split.
- **Cons:** Two stacks exist concurrently during the migration window
  (P2 → P4 close). The hybrid end-state itself has the `Popups`
  exception that requires a CI grep gate to remain disciplined.
- **Estimated Effort:** ~3–4 dev-days UI Toolkit ramp + P1; Slice 6
  grows from ~5 days UGUI to ~8–9 days UI Toolkit (one-time tax).
- **Acceptance Reason:** Right balance of risk sequencing, end-state
  cleanliness, and engine alignment.

## Consequences

### Positive

- M2's branching node-map, Merchant lists, Event choice screens, and
  Settings panels author in declarative UXML + USS rather than imperative
  RectTransform tuning. Code volume and bug surface both drop.
- USS hot-reload at runtime accelerates iteration on every panel after
  Phase 1. Combat-HUD-style "tune in Prefab Mode and bake" workflows
  apply to UI Toolkit panels at lower latency.
- One UI tech for all screen-space surfaces ports forward to a Steam
  Deck / handheld scaling story (UI Toolkit's flex layout adapts cleanly
  to varied resolutions; UGUI requires more anchoring math).
- ADR-0002 forbidden-pattern rule (`UnityEvent` ban) extends naturally
  to UI Toolkit via `RegisterCallback<T>` + C# `event`. The cross-system
  discipline holds.
- The `Popups` UGUI exception is explicit, scoped, and CI-enforced —
  it's a documented axis-aligned choice, not a quiet bridge.

### Negative

- Two stacks during the P2 → P4 window. Each new contributor (or
  resumed session) must understand which surface uses which tech. The
  CI gate (P5) locks the end state but does not help during migration.
- Combat_HUD migration (P4) is high-risk and high-effort. The combat
  feel regression risk is real and the capture-before-destroy protocol
  must be respected to the letter.
- World-space `Popups` retains UGUI patterns the rest of the project
  has retired. Anyone authoring a new world-anchored UI element must
  know to drop into UGUI explicitly.

### Neutral

- `WastelandRun.UI` asmdef joins the assembly graph. The one-way arrow
  to `Run` and `Combat` (no reverse references) preserves the layering
  discipline.
- USS design tokens introduce a new authoring surface
  (`Assets/UI/Tokens/`). This is additive, not destructive.

## Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Gamepad focus routing across multiple `UIDocument` panels is awkward in 6.3 | MEDIUM | MEDIUM | Gamepad is scoped "partial" in technical-preferences; focus routing limitations are tolerable for 1.0; revisit if gamepad scope upgrades to "primary" |
| TextMeshPro feature parity in UI Toolkit text isn't 1:1 with TMP in UGUI | LOW | LOW–MEDIUM | Phase 2 is greenfield (no TMP-feature dependency yet); Phase 4 captures every TMP feature in use before migration; gaps file an ADR-0014 amendment |
| Combat feel regression during P4 (Combat_HUD migration) | MEDIUM | HIGH | Exhaustive capture-before-destroy per `feedback_capture_before_destroy_view_layer.md`; dedicated M1.5 slice; full playtest pass before merge; rollback in a single commit if regression detected |
| USS hot-reload causes Editor instability on 6.3 | LOW | LOW | Disable hot-reload if observed; fall back to manual reload — does not block any phase |
| Screen reader integration weak in both UGUI and UI Toolkit (accessibility-specialist scope) | HIGH | MEDIUM | Out of scope for this ADR; tracked as accessibility-specialist follow-up; custom narration layer planned independently |
| Designer wants Scene-view direct manipulation for HUD layout tuning (only UGUI offers it) | MEDIUM | LOW | UI Builder gives visual editing back in 6.3; USS hot-reload at runtime closes the iteration gap for layout tuning |

## Performance Implications

| Metric | UGUI (today) | UI Toolkit (expected) | Budget |
|--------|--------------|------------------------|--------|
| Canvas rebuild cost on dirty element | Whole-canvas rebatch (mitigated by 3-sibling-canvas split) | Per-element dirty-rect repaint (single mesh per panel) | 60fps / 16.6ms |
| Per-frame layout cost (static panels) | Negligible | Negligible (USS layout computed once) | 60fps / 16.6ms |
| Per-frame layout cost (animated flex layouts: card hand drag, damage rain) | Mitigated by canvas-split + careful Transform animation | Non-trivial — USS animations and flex reflow can cost more than UGUI's direct Transform manipulation at high element count | 60fps / 16.6ms |
| World-space UI per-frame conversion | Direct (UGUI WorldSpace Canvas) | `RuntimePanelUtils.ScreenToPanel` per element per frame | 60fps / 16.6ms |

The world-space conversion cost is the load-bearing reason the `Popups`
canvas stays UGUI. At expected damage-number density (5–20 simultaneous
popups during burst combat), the per-frame conversion cost is enough to
prefer the simpler UGUI path.

Verification step before Phase 4 close: profile `Combat_HUD` in UI
Toolkit under burst-combat conditions and confirm no frame-time
regression vs the UGUI baseline.

## Validation Criteria

- [ ] Phase 1 lands: USS design tokens authored, `PanelSettings` asset
      created, `WastelandRun.UI` asmdef in the assembly graph, one base
      control compiled and previewable in UI Builder.
- [ ] Phase 2 lands: Slice 6 node-map view and Run Complete view
      authored UI Toolkit native, no UGUI fallback exists in the
      commit history for either surface.
- [ ] Phase 3 lands: `CardRewardPicker` and `CombatOutcomeOverlay`
      migrated, capture files in `production/polish-captures/` for both.
- [ ] Phase 4 lands: `Combat_HUD` migrated with exhaustive capture
      file; playtest pass confirms no combat-feel regression.
- [ ] Phase 5 lands: CI grep gate forbids new `Canvas` components
      outside the `Popups` canvas subtree; the gate fails the build
      when violated.
- [ ] No `UnityEvent` anywhere under the new UI stack at any phase.
- [ ] One `PanelSettings` asset shared by all runtime `UIDocument`
      panels; exceptions documented in panel USS headers.

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|--------------|--------|-------------|---------------------------|
| `design/gdd/node-map.md` | Map / Run-Loop | Branching node graph with variable rows, hover previews, animated reveals (M2+) | UI Toolkit's USS flex + `:hover` + `transition` properties express variable-row branching declaratively; UGUI would require manual anchoring math |
| `design/gdd/combat.md` | Combat HUD | HP/armor/intent/energy/hand/deck/discard surfaces must update reactively without per-frame layout cost | UI Toolkit retained-mode rendering scales to dense reactive HUDs better than UGUI's whole-canvas rebuilds |
| `design/gdd/accessibility.md` | Accessibility | Text scaling, dynamic font sizing, high-contrast modes | USS supports declarative font scaling and theming far more cleanly than UGUI's per-Component overrides; this ADR enables, does not solve, accessibility-specialist follow-up work |
| Foundational | UI Architecture | Single primary UI stack across screens, menus, HUD | This ADR is the foundational decision; subsequent UI ADRs amend it rather than re-decide it |

## Related

- **ADR-0002** (Card Combat: POCO state + event pattern) — forbidden-pattern
  rule (`UnityEvent` ban) ported forward to the UI layer.
- **ADR-0011** (No-bridges meta-rule) — axis-aligned hybrid scope
  compliance; the `Popups` UGUI exception is explicit, scoped, and
  CI-enforced rather than a bridge window.
- **ADR-0008** (Addressables) — UI assets (sprites, fonts) under the new
  stack continue to follow Addressables loading discipline.
- **Slice 6 capture file** (to be written, ADR-0014 Phase 2 trigger):
  `production/polish-captures/2026-06-14-slice-6-node-map-ui.md`.
- **Capture for this ADR**: `production/polish-captures/2026-06-13-adr-0014-ui-toolkit-primary-stack-hybrid.md`.
- **Unity 6.3 What's New** — UI Toolkit production-readiness, USS
  runtime hot-reload (verified 2026-06-13).
- **Unity 6.3 UI Toolkit runtime docs** — `UIDocument`, `PanelSettings`,
  `RuntimePanelUtils`, `ListView` binding.

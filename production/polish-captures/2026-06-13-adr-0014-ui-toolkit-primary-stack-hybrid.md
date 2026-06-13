# Capture — ADR-0014: UI Toolkit Primary Stack (Hybrid)

**Date:** 2026-06-13
**System slug:** `adr-0014-ui-toolkit-primary-stack-hybrid`
**ADR file:** `docs/architecture/adr-0014-ui-toolkit-primary-stack-hybrid.md`
**Trigger:** New Accepted ADR landing the UI-tech end-state decision
before any Slice 6 code is authored.

## What this ADR locks

Wasteland Run adopts **Unity UI Toolkit (UXML + USS + C# controller) as
the primary UI stack** for all screens, menus, HUD, the Slice 6
node-map view, the Run Complete view, and every panel from this ADR
forward. **UGUI is retained for one axis only**: the world-space
`Popups` canvas (damage numbers, status popups anchored to live world
transforms). The split is axis-aligned (world-space vs screen-space),
not bimodal — it does not violate ADR-0011 #4.

A **five-phase migration plan** sequences the transition:

- **P1** — USS design tokens + base controls + `PanelSettings` +
  `WastelandRun.UI` asmdef. **Lands before Slice 6 code.**
- **P2** — Slice 6 node-map view + Run Complete view authored UI
  Toolkit native. No UGUI map ever exists.
- **P3** — `CardRewardPicker` + `CombatOutcomeOverlay` migrate (M1 polish).
- **P4** — `Combat_HUD` migrates (M1.5 dedicated slice) with
  exhaustive capture-before-destroy.
- **P5** — CI grep gate forbids new `Canvas` components outside the
  `Popups` canvas subtree.

## Authored values being destroyed by this ADR landing

**None at this ADR commit.** This ADR is a forward-looking architectural
decision; the destructive edits land in Phases 3 and 4. Each phase will
ship its own capture file enumerating every authored value (color,
padding, font size, sprite reference, animation curve, anchoring,
sortingOrder, m_IsActive, m_Modifications) before any UGUI panel is
replaced.

**Capture-file commitments scheduled by this ADR:**

| Phase | Capture file path (planned) | Authored values to enumerate |
|-------|------------------------------|------------------------------|
| P3 | `production/polish-captures/<YYYY-MM-DD>-card-reward-picker-uitoolkit-migration.md` | Every authored value on `CardRewardPicker` UGUI prefab + child elements |
| P3 | `production/polish-captures/<YYYY-MM-DD>-combat-outcome-overlay-uitoolkit-migration.md` | Every authored value on `CombatOutcomeOverlay` UGUI prefab + child elements |
| P4 | `production/polish-captures/<YYYY-MM-DD>-combat-hud-uitoolkit-migration.md` | Exhaustive enumeration of `Combat_HUD` UGUI prefab — HP/armor bars, intent display, energy pips, hand layout, deck/discard piles, hover state, every TMP feature in use, every animation curve, every sprite reference |

## Files touched by this ADR commit

- **NEW:** `docs/architecture/adr-0014-ui-toolkit-primary-stack-hybrid.md` (~416 lines)
- **NEW:** `production/polish-captures/2026-06-13-adr-0014-ui-toolkit-primary-stack-hybrid.md` (this file)

No code touched. No prefab touched. No SO touched.

## Final-game picture this serves

Wasteland Run 1.0 ships with:

- One screen-space UI stack (UI Toolkit) handling: title screen,
  settings, save/load slot picker, Combat HUD, card reward picker,
  combat outcome overlay, node map, Run Complete screen, Merchant view,
  Chopshop view, Event view, Rest view, in-game pause menu.
- One world-space UI stack (UGUI WorldSpace Canvas) handling: damage
  numbers, status effect popups, any future world-anchored element.
- USS design tokens as the single source for typography, color,
  spacing, and radii across every screen-space panel.
- Zero `UnityEvent` anywhere — `RegisterCallback<T>` inbound, C# `event`
  outbound on every controller (ADR-0002 forbidden-pattern rule ported
  forward).
- CI grep gate forbidding new `Canvas` components outside the `Popups`
  subtree, enforcing the axis-aligned discipline at the build level.

M2 expansion (branching node-map, Merchant/Chopshop/Event/Rest panels)
drops in as additional UXML+USS+controller triples with zero rework of
the screen-space stack.

## Technical Director Review

**Verdict: ACCEPTED HYBRID (option c).** TD-ARCHITECTURE: CONCERNS on
the current UGUI-everywhere path; recommendation below resolves it.

### Performance (UGUI vs UI Toolkit)

UGUI's killer is the Canvas rebuild: any dirty element re-batches the
whole canvas. The project has already mitigated this correctly with 3
sibling canvases (HUD/Popups/Debug) — that's the standard UGUI escape
hatch. UI Toolkit uses a retained scene graph with dirty-rect repaints
and a single mesh per panel; for static-heavy HUDs with localized
animation (energy pip pulse, intent flicker) it wins easily. **But** UI
Toolkit's per-frame layout pass is non-trivial when many flex
containers reflow simultaneously (card hand drag, damage number rain).
For a UI-heavy card game, *both* can hit 60fps if authored well; UI
Toolkit has a higher ceiling and lower floor — it scales better but
punishes naive USS animations.

### Dynamic layout fit (M2 node-map)

This is where UI Toolkit clearly wins. Variable-row branching graphs,
fog-of-war reveals, hover tooltips — UGUI's RectTransform + LayoutGroup
forces you into either manual anchoring math or fragile nested
VerticalLayoutGroups with `LayoutRebuilder.ForceRebuildLayoutImmediate`
calls. USS flex + `:hover` pseudo-class + `transition` properties
express this in declarative CSS. For Merchant/Chopshop/Event views
(list-driven, data-bound), UI Toolkit's `ListView` + binding system is
multiple-times less code than UGUI ScrollRect + pooled item prefabs.

### Authoring workflow (solo dev)

UGUI is *faster for the first hour* (drag-and-drop, immediate visual).
UI Toolkit is faster after the first week — UI Builder gives you the
visual editing back, and USS hot-reloads at runtime in 6.3, which UGUI
cannot do. For a 20-year designer comfortable with structured
authoring, UXML/USS will click fast. **Caveat:** you'll lose Scene-view
direct manipulation, which matters for HUD spatial tuning.

### Migration cost — be specific

Realistic estimates (solo dev + AI assist):

- `Combat_HUD` rewrite: **4–6 days** (HP/armor bars, intent, energy,
  hand layout, deck/discard, hover) — biggest single risk
- `CardRewardPicker`: **1 day**
- `CombatOutcomeOverlay`: **0.5 day**
- Damage/status popup systems: **1.5 days** (world-space anchoring via
  `RuntimePanelUtils.ScreenToPanel` is the friction point)
- Re-tuning + bug fixes: **2 days**
- **Total deferred-migration cost: ~9–11 dev-days, plus regression risk
  on shipped combat feel.**

"Ship UI Toolkit now" cost: **3–4 days learning + ~5 days rebuilding
the 3 canvases**, but you avoid ever touching that code twice.

### Risk read (Unity 6.3 UI Toolkit gaps)

Production-ready claim is largely true, but real gaps for a 1.0 ship:

- **Gamepad routing** in runtime panels works but focus navigation
  between panels is awkward — manageable, you've already scoped
  gamepad as "partial."
- **World-space UI** (damage numbers tracking world positions)
  requires `RuntimePanelUtils` conversion each frame; UGUI's
  WorldSpace Canvas is simpler. **This is the one place UGUI is
  genuinely better.**
- **Screen reader integration** is weak in both stacks; you'd build a
  custom narration layer either way.
- **TextMeshPro features** (rich text gradient, complex shaders) — UI
  Toolkit's text rendering caught up in 6.3 but isn't 1:1.

### End-state recommendation

**(c) Hybrid with a hard split:**

- **UI Toolkit:** all screens, menus, Merchant, Chopshop, Event, Rest,
  Run Complete, Title, Settings, **node-map**, `CardRewardPicker`,
  `CombatOutcomeOverlay`, `Combat_HUD` (HP/armor/intent/energy/hand/
  deck/discard).
- **UGUI (keep):** world-space damage numbers + status popups only
  (the `Popups` canvas). They anchor to world transforms and are the
  one place UGUI is materially simpler.

This avoids the "rewrite Combat_HUD twice" trap while keeping the one
UGUI strength intact.

### ADR shape (if you accept)

**ADR-0014 — UI Toolkit as primary stack:**

- Binding rule: all *screens and HUD* are UI Toolkit (UXML + USS + C#
  controllers). No new UGUI Canvas except world-space `Popups`.
- No `UnityEvent` equivalent: use C# `Action`/`event` on controllers;
  `RegisterCallback<T>` for UI Toolkit events; ports the
  combat-systems forbidden-pattern rule forward.
- Migration phases: **(P1)** author USS design tokens + base controls
  before Slice 6, **(P2)** Slice 6 node-map ships UI Toolkit native,
  **(P3)** `CardRewardPicker` + `CombatOutcomeOverlay` migrate in the
  M1→M1.5 transition, **(P4)** `Combat_HUD` migrates in M1.5 with
  capture-before-destroy on every authored value, **(P5)** CI grep
  gate: no new `Canvas` components outside `Popups`.

**End-state verdict: (c) Hybrid — UI Toolkit for all screens/HUD, UGUI
retained only for world-space `Popups`, because Unity 6.3 makes UI
Toolkit the right primary stack but UGUI's WorldSpace Canvas is the one
feature genuinely worth preserving.**

## User decision

User accepted TD hybrid verdict on 2026-06-13 with explicit instruction
"accept TD hybrid, draft ADR-0014." This capture records the verdict
and the project-wide commitment; the ADR file at
`docs/architecture/adr-0014-ui-toolkit-primary-stack-hybrid.md`
formalises the binding rules, migration phases, and validation criteria.

# Interaction Pattern Library — Wasteland Run

> **Status**: Living document — patterns added as UX specs consume them
> **Last Updated**: 2026-04-25
> **Owner**: ux-designer (authoring); accessibility-specialist (accessibility floor review)

---

## 1. Purpose & How to Use

### What this document is

A **shared vocabulary** for UX specs. Each entry defines the *reusable contract*
for an interaction pattern — the events it fires, the keyboard behavior it
guarantees, the accessibility floor it meets — without dictating visual
treatment or spec-specific context.

This library is the abstraction layer. Concrete spec docs (e.g.,
`design/ux/combat-hud.md`) are authoritative for *their* context. When a spec
references a pattern by name, it inherits the pattern's contract and may
refine or extend it locally.

### When to reference a pattern

- Use a pattern name in a UX spec when the interaction matches the pattern's
  contract. Example: "Status Effect Chip pattern; see interaction-patterns.md §3.2"
- If a spec's interaction deviates from the pattern, the spec must call out the
  deviation explicitly ("extends Tooltip pattern with additional dwell behavior
  for X reason").
- Deviations that generalize to other contexts should be promoted to the library.

### When to add a new pattern

- A pattern is added when ≥2 UX specs need the same interaction shape, OR when
  an interaction is load-bearing enough to deserve a name even in one spec.
- Patterns start as stubs with "To be authored with [first consumer spec]" and
  are filled when the consumer spec is written.
- Adding a pattern is not blocking — stubs are fine. Unnamed ad-hoc interactions
  are what the library prevents.

### How this fits with other docs

- `design/accessibility-requirements.md` sets the accessibility floor every
  pattern must meet. Patterns cannot opt out of floor commitments.
- `design/art/art-bible.md` governs visual treatment. Patterns are agnostic to
  palette / typography / iconography — they specify *behavior*, not *look*.
- Engine-specific implementation (UI Toolkit widget choice, UGUI vs UIT,
  navigation system) is an ADR concern, not a pattern concern.

---

## 2. Pattern Catalog

Full entries in §3. Stub entries in §4.

| # | Pattern | Status | Primary consumer |
|---|---------|--------|------------------|
| 3.1 | Card | Full | `combat-hud.md` |
| 3.2 | Status Effect Chip | Full | `combat-hud.md` |
| 3.3 | Intent Zone | Full | `combat-hud.md` |
| 3.4 | Tooltip | Full | All specs |
| 3.5 | Focus & Navigation | Full | All specs |
| 4.1 | Button (Primary / Secondary / Destructive) | Stub | Main Menu / Settings |
| 4.2 | Modal Dialog | Stub | Save-exhaustion dialog, Settings confirmations |
| 4.3 | Settings Control (Slider / Toggle / Dropdown / Keybind Row) | Stub | Settings spec |
| 4.4 | Node Marker | Stub | Node Map spec |
| 4.5 | HostileTiltDelta Tell | Stub | Node Encounter spec |
| 4.6 | Toast / Notification | Stub | Multiple |
| 4.7 | Inspection Panel | Stub | Run log / deck inspector |
| 4.8 | Keybind Display (inline hint) | Stub | All specs |
| 4.9 | Resource Meter | Stub | `combat-hud.md` (authoritative), Map HUD |
| 4.10 | Card Zone (Deck / Hand / Discard) | Stub | `combat-hud.md` (authoritative) |

---

## 3. Full Pattern Entries

### 3.1 Card

**Purpose**: Display a playable card; allow inspection and play.

**When to use**: Any interaction where a card object is rendered for player
decision-making (hand, deck inspection, reward selection).

**Reusable contract**

| Aspect | Contract |
|--------|----------|
| Interactive states | Idle, Hover, Focus, Selected, Playing, Disabled, Fizzled |
| Input parity | Mouse click-to-play AND keyboard activate-to-play; any drag gesture must have a click/keyboard equivalent |
| Focusable | Yes. Tab order matches visual left-to-right in hand |
| Activation | Mouse click OR `Enter`/`Space` on focused card OR gamepad `A`/South |
| Cancel | `Esc` / gamepad `B`/East while a card is Selected (pre-resolve) |
| Fizzle state | Player-side fizzle throws `InvalidCardPlayException` — energy preserved, card stays in hand, visible rejection feedback (no state change penalty) |
| Accessibility hook | Accessible name = `"{CardName}, {Cost} energy, {KindLabel}, {ShortDescription}"`; tooltip provides full rules text |
| Color discipline | Card kind communicated by shape + icon, never color alone. Two orthogonal axes: **mechanical kind** (engine enum — Attack / Plate / Reposition / Repair) drives rules; **visual archetype** (art bible §3.4 — Precision / Assault / Plating / Control / Repair / Maneuver) drives shape. Mapping is defined in art bible §3.4 "Card Taxonomy: Mechanical Kind × Visual Archetype" table |
| Events fired | `OnCardFocused`, `OnCardSelected`, `OnCardPlayAttempted`, `OnCardPlayResolved`, `OnCardPlayFizzled` |

**Deviations allowed**: Reward-selection cards may use a different activation
(single-select commits rather than energy-spend play). Reward flow spec defines
the deviation.

**Not-a-card**: Status effect icons, intent zone contents, vehicle subsystems —
these are their own patterns even when card-sized.

**Authoritative spec**: `design/ux/combat-hud.md` §5 Hand Zone for combat-context
treatment. Reward-context treatment is pending reward-flow UX spec.

---

### 3.2 Status Effect Chip

**Purpose**: Communicate an active status effect's type, stack count, and remaining duration.

**When to use**: Anywhere an active status effect must be legible to the player
(player chassis, enemy chassis, tutorial overlays).

**Reusable contract**

| Aspect | Contract |
|--------|----------|
| Interactive states | Idle, Hover, Focus, Recently-Applied (highlight pulse), Recently-Expired (fade out) |
| Color independence | Status type communicated by icon shape + position, never color alone. Color is an accent only. |
| Stack indication | Stack count displayed numerically when > 1. Visual stack depth (layered icons) is decorative only — the number is authoritative. |
| Duration indication | Remaining turns displayed numerically OR as a radial/bar decrementer. If visual-only, a text equivalent is available via hover/focus. |
| Focusable | Yes. Keyboard focus order: player chips (left→right), then enemy chips (left→right) |
| Tooltip | Opens on hover (1.5s dwell default) OR on focus (immediate). See Tooltip §3.4. |
| Accessibility hook | `OnStatusApplied(target, status, duration, stacks)`, `OnStatusExpired(target, status)`, `OnStatusTickDamage(target, status, amount)` fire to the screen reader sink. Accessible name = `"{StatusName}, {Stacks} stacks, {Duration} turns remaining"` |
| No timing-critical reads | Player may inspect a chip as long as they want; combat does not advance while inspection is active (unless explicitly time-gated — see Timing & Pacing in combat-hud.md) |

**Authoritative spec for GDD hooks**: `design/gdd/status-effects.md` §Accessibility Requirements.

**Authoritative spec for combat visual**: `design/ux/combat-hud.md` §5.

---

### 3.3 Intent Zone

**Purpose**: Telegraph an enemy's upcoming action so the player can plan around it.

**When to use**: Any combat context where an enemy has a telegraphed turn-N action.

**Reusable contract**

| Aspect | Contract |
|--------|----------|
| Telegraph turn | Actions are visible at least 1 turn before they resolve (per `enemy-system.md`). Some archetypes telegraph longer; pattern respects archetype's telegraph window. |
| Retarget animation | When intent retargets mid-telegraph (e.g. after a Reposition card), the zone animates the target change with a distinguishable motion cue, not a silent swap. |
| Color independence | Intent verb (Attack / Repair / Buff / Reposition) communicated by icon + label, not color alone. |
| Focusable | Yes. Sits in keyboard focus order after player chassis, before enemy chassis. |
| Tooltip | Opens on hover/focus. Tooltip shows full intent breakdown: archetype, verb, target slot, value, unit, telegraph timestamp. |
| Accessibility hook | Accessible name format (from `design/gdd/enemy-system.md`): `"{ArchetypeDisplayName}: {IntentVerb} {SlotLabel} for {Value}{Unit}, telegraphed turn {TurnCount}"`. Updates on `OnIntentTelegraphed` and `OnIntentRetargeted`. |
| Reduce Motion | Retarget animation respects Reduce Motion: simplified to a flash + re-anchor instead of motion path. Information is preserved. |

**Authoritative spec**: `design/ux/combat-hud.md` §5 Zone 4.

---

### 3.4 Tooltip

**Purpose**: Provide on-demand detail for any element the player can inspect.

**When to use**: Any UI element with information that doesn't fit in its primary
visual (cards, status chips, buttons, settings rows, node markers).

**Reusable contract**

| Aspect | Contract |
|--------|----------|
| Activation — mouse | Hover dwell (default 1.5s; configurable 0.2s–3.0s in accessibility settings). |
| Activation — keyboard | Immediate on focus. Dwell setting does not apply to keyboard — focus implies intent. |
| Activation — gamepad | Immediate on focus (same as keyboard). Gamepad right-stick may also invoke a "long tooltip" per local spec. |
| Dismissal | Mouse: on hover-out. Keyboard/gamepad: on focus-out OR `Esc`. |
| Inspection time | Unlimited. No auto-dismiss timer. Game does not advance while tooltip is open in reactive contexts. |
| Hover-vs-focus parity | Any content available on mouse hover is available on keyboard focus. (Rule from `node-encounter.md`.) |
| Positioning | Tooltip anchors to the element's bounding box; flips to the opposite side when near a screen edge. Never covers the element it describes. |
| Content discipline | Max ~60 words for primary body; additional detail requires an Inspection Panel (§4.7), not a bigger tooltip. |
| Accessibility hook | Tooltip content is spoken by the screen reader as part of the focused element's accessible description. |
| Localization | Tooltip body is a localization string; no hardcoded text. |

**Non-tooltip surfaces**: Long-form detail (run log, deck composition, crafting
breakdown) uses Inspection Panel (§4.7), not a tooltip.

---

### 3.5 Focus & Navigation

**Purpose**: Guarantee every interactive element is reachable and visible via keyboard or gamepad.

**When to use**: Every UX spec. No exceptions. No hover-only interactions exist in this game.

**Reusable contract**

| Aspect | Contract |
|--------|----------|
| Focus visibility | The focused element is always distinguishable. Default treatment: 2px outline in the theme focus color + optional shape/scale change. Never communicated by color alone. |
| Focus order | Reads left-to-right, top-to-bottom within a zone. Zone-to-zone order is defined per spec. Tab advances; Shift+Tab retreats. |
| Gamepad focus | D-pad / left stick maps to directional nav. `A`/South activates; `B`/East cancels/back. |
| Focus trap — modals | Modal dialogs trap focus within the modal until dismissed. Tab cycles within the modal only. |
| Focus restoration | When a modal closes, focus returns to the element that opened it. When a screen unloads, focus lands on the target screen's "primary" element (defined per spec). |
| Skip affordances | Long repeating lists (deck viewer, inventory) support `Page Up`/`Page Down` or gamepad shoulder buttons to skip regions. |
| Initial focus on screen load | Every screen has a defined "primary focus" element. Never unfocused state on load. |
| Invisible elements | Elements hidden by layout (offscreen, behind scroll) are removed from the focus order until visible. |
| Disabled elements | Disabled elements are NOT focusable. If the player needs to know *why* it's disabled, surface that via an adjacent tooltip-equivalent, not by focusing the disabled element. |
| Accessibility hook | Every focusable element has an accessible name. Focus events can be consumed by the screen reader sink. |

**Remapping**: Navigation keys are remappable per the accessibility tier commitment (see `design/accessibility-requirements.md` §2).

**Authoritative spec for combat-context gamepad model**: `design/ux/combat-hud.md` §6 Gamepad navigation model.

---

## 4. Stub Pattern Entries

Each stub names the pattern, lists its anticipated consumers, and defers detail
to the first consumer spec.

### 4.1 Button (Primary / Secondary / Destructive)
- **Anticipated consumers**: Main Menu, Settings, Pause Menu, Save dialogs
- **To be authored with**: Main Menu UX spec
- **Known contracts already**: Keyboard-reachable; `Enter`/`Space`/gamepad-`A` activate; Destructive variant requires confirmation (modal dialog or hold-to-activate)

### 4.2 Modal Dialog
- **Anticipated consumers**: MasteryState save-exhaustion dialog (ADR-0004), Settings "unsaved changes", Quit confirmation, End Run confirmation
- **To be authored with**: first modal-consuming spec
- **Known contracts already**: Focus-trapped; focus restored to opener on dismiss; `Esc` dismisses non-blocking modals; blocking modals (save exhaustion) require explicit button press, no `Esc` dismissal

### 4.3 Settings Control (Slider / Toggle / Dropdown / Keybind Row)
- **Anticipated consumers**: Settings spec
- **To be authored with**: Settings UX spec
- **Known contracts already**: Slider = arrow keys ±1 step, Shift+arrow ±10, numeric display always shows current value. Keybind Row = "rebind" invokes a capture modal with conflict detection.

### 4.4 Node Marker
- **Anticipated consumers**: Node Map spec
- **To be authored with**: Node Map UX spec
- **Known contracts already** (from `node-map.md` I.5): Colorblind-safe state via opacity/pulse/outline; unique silhouettes per beacon type; accessible name = `"{EncounterType}, {ReachabilityState}, {FuelCost} fuel"`

### 4.5 HostileTiltDelta Tell
- **Anticipated consumers**: Node Encounter spec
- **To be authored with**: Node Encounter UX spec
- **Known contracts already** (from `node-encounter.md` I.4): Three-channel tell — visual shimmer + sub-200Hz audio + gamepad haptic (motorLevel 0.15, 1.5s loop). KBM-only fallback is two-channel (visual + audio). Pre-1.0 accessibility gate on file (OQ-NE12).

### 4.6 Toast / Notification
- **Anticipated consumers**: Multiple — save-success confirmation, rebind-collision warning, achievement unlock
- **To be authored with**: first toast-consuming spec
- **Known contracts already**: Non-blocking; never conveys information required to proceed; visible for a floor duration independent of animation; queueable; stackable with a cap.

### 4.7 Inspection Panel
- **Anticipated consumers**: Run log, deck inspector, Haven scrap ledger
- **To be authored with**: first inspection-consuming spec
- **Known contracts already**: Non-modal; player may keep open indefinitely; independent scroll; `Esc` or dedicated close button dismisses; focus restoration on dismiss.

### 4.8 Keybind Display (inline hint)
- **Anticipated consumers**: Every spec (inline hints like `[Space] End Turn`)
- **To be authored with**: Main Menu UX spec (first formal consumer)
- **Known contracts already**: Displays the currently-bound key from the Input System; updates live on rebind; uses platform-appropriate glyph on gamepad (`A` / Cross / etc.)

### 4.9 Resource Meter
- **Anticipated consumers**: Combat HUD (HP bars, energy pips, fuel gauge), Map HUD (fuel gauge, scrap count)
- **To be authored with**: already partially in `combat-hud.md` §5
- **Known contracts already**: Current value always numerically visible (not bar-only); color is accent, not primary channel; tick animation on change; respects Reduce Motion.

### 4.10 Card Zone (Deck / Hand / Discard)
- **Anticipated consumers**: Combat HUD (authoritative in `combat-hud.md`)
- **To be authored with**: already in `combat-hud.md` §5 Zone 6–7
- **Known contracts already**: Count badges on Deck and Discard; inspection available on click/focus; invariant `Deck + Hand + Discard == StartingSize` (from Unity backfill Step 4).

---

## 5. Gaps & Patterns Needed

- **Reward-flow patterns** — Post-combat reward selection (card choice, subsystem repair choice, loot grant). Pattern likely distinct from `Card §3.1` activation. Trigger: post-combat flow UX spec.
- **Haven-specific patterns** — Scrap ledger, part installation, repair dialog. Trigger: Haven UX spec (if in-MVP per W-4 resolution).
- **Tutorial / onboarding overlays** — First-run guidance. Likely blends Toast + Inspection Panel with a forced-focus variant. Trigger: onboarding spec.
- **Ambush cold-start** — Visual + audio + input-state transition when a run enters Ambush. Combat-hud.md §5 covers it locally; may need promotion if it generalizes to map ambushes.

---

## 6. Open Questions

- **OQ-PL1**: Should `Card §3.1` fizzle feedback be a standalone pattern when fizzle-capable elements beyond cards appear (e.g., enemy-side fizzle in `CombatLoop`)? Decide on promotion when second consumer lands.
- **OQ-PL2**: Should `Tooltip §3.4` and `Inspection Panel §4.7` share a content authoring model (so one piece of copy can render in either container)? Decision deferred to first inspection-consuming spec.
- **OQ-PL3**: `Keybind Display §4.8` — should gamepad glyphs match platform auto-detect (Xbox vs PS vs Switch Pro) or be a user-selectable override? Pre-1.0 decision.

---

## 7. Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-04-25 | Initial doc. Catalog of 15 patterns, 5 full entries, 10 stubs. | PR-C1b gate-check condition closure (Technical Setup phase). |
| 2026-04-25 | §3.1 Card "Color discipline" row rewritten to reference art bible §3.4 mapping table; explicit dual-axis framing (mechanical kind drives rules; visual archetype drives shape). | Reconciled card taxonomy conflict between engine `CardKind` enum (Attack/Plate/Reposition/Repair) and art bible archetype vocabulary (Precision/Assault/Plating/Control/Repair/Maneuver) after PR-C2 gap-check flagged them as orthogonal, not overlapping. |

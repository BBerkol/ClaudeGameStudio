# Accessibility Requirements — Wasteland Run

> **Status**: Accepted
> **Committed Tier**: Standard
> **Last Updated**: 2026-04-25
> **Owner**: accessibility-specialist (primary); ux-designer (interaction floors); qa-lead (test coverage)

---

## 1. Tier Commitment & Scope

### Committed Tier: Standard

Wasteland Run commits to the **Standard** accessibility tier for the 1.0 release.
This covers:

- Full keyboard remapping and keyboard-only playability
- Text scaling with a readable floor at target resolutions
- Colorblind-safe palette and "color never the sole information channel"
- Reduce Motion setting that gates non-essential animation
- Configurable tooltip dwell time
- Screen reader event hooks from all MVP systems (integration path pending — see Section 11)
- No timing-critical information reads
- Gamepad haptic as an additive channel, never a sole channel

### Explicitly In Scope
- Colorblind users (protanopia, deuteranopia, tritanopia)
- Low-vision users (text scaling, high contrast option)
- Motor-impaired users at a keyboard-only level (no multi-key combos required)
- Cognitively-loaded players (unlimited inspection time, no reaction-speed-gated reads)
- D/deaf or hard-of-hearing players (no information conveyed by audio alone)

### Explicitly Out of Scope for 1.0
- Full blind playability (requires turn-based navigation grammar that the real-time
  combat chase-rail layout does not support at MVP scope)
- Alternative-input hardware (switch, sip-and-puff, eye-gaze)
- Cognitive-load difficulty tiers (simplified encounter set)
- Dyslexia-specific fonts (standard readable font; OpenDyslexic toggle deferred)

Out-of-scope items may be revisited post-1.0. Document them as gaps, never as
"not supported" — the wording matters to the community.

---

## 2. Input Accessibility

### Requirements
- **Keyboard-only playable**: Every core interaction must be reachable via keyboard
  alone. No hover-only interactions. No multi-key chord requirements.
- **Full remapping**: Every keybind is rebindable at runtime via settings.
  Conflict detection surfaces rebind collisions inline.
- **Gamepad parity**: Every action reachable on gamepad. Gamepad is additive —
  it does not gate any feature (per `.claude/docs/technical-preferences.md`).
- **Focus visibility**: Keyboard focus is always visible on the focused element
  (outline or equivalent) and never ambiguous between two elements.
- **Focus restoration**: When a modal closes, focus returns to the element that
  opened it.

### Non-Requirements
- Hold-to-activate or hold-to-cancel patterns require a tap alternative.
- No timing-critical input sequences. Reaction-window inputs must have a
  configurable input buffer.

---

## 3. Visual Accessibility

### Colorblind-Safe Design
- **Rule**: Color is never the sole information channel. Every color-coded state
  must also carry a shape, icon, position, pattern, or text cue.
- **Palette**: The visual identity palette (see `design/art/art-bible.md`) is
  validated against protanopia/deuteranopia/tritanopia simulation.
- **Testing**: Dev-mode URP post-processing volume applies an LMS colorblind
  simulator to verify parity across all critical screens.

### Text Scaling
- **Minimum floor**: 16pt at 1080p (from `design/gdd/node-map.md` I.5).
- **Scaling range**: 100% (default) to 150% via Settings.
- **Layout resilience**: UI must accommodate 150% scale without overflow or truncation.
- **UI Toolkit binding**: USS `font-size` bound to a global setting; no hardcoded
  font sizes in USS stylesheets.

### Reduce Motion
- **Setting**: Global "Reduce Motion" toggle in Settings.
- **Effects gated when enabled**:
  - Storm parallax (`node-map.md`)
  - Screen-shake (non-essential feedback)
  - Parallax scrolling in the combat scene
  - Decorative particle systems on environmental props (ambient rust shimmer, drifting dust). Note: this refers to *decorative* rust shimmer, not the §H.3 information-bearing rust-shimmer tell — see "Effects NOT gated" below.
  - Legendary rarity pulse on cards/parts (replaced by static composite border per `design/art/art-bible.md` §4.7 — rarity differentiation preserved via value + composite double-border)
- **Effects NOT gated** (information-bearing motion stays):
  - Card animations that communicate play resolution
  - Intent zone telegraphs (these ARE the information)
  - Vehicle position swap (conveys turn order)
  - **§H.3 rust-shimmer tell** (HostileTiltDelta visual channel on Event beacons under hover/focus when `FrameState ∈ {Degraded, Offline}`). Under Reduce Motion this animation is alternative-rendered as a static frame at peak luminance — see `design/art/art-bible.md` §7.4 Rust-Shimmer Tell. The audio channel is unaffected.

### High Contrast
- A high-contrast UI setting is in scope for 1.0. Contrast ratio ≥ 7:1 for
  critical readable UI text (WCAG AAA for large text).

---

## 4. Audio Accessibility

- **Audio cue redundancy**: No game-critical information is conveyed by audio
  alone. Every audio cue has a visual equivalent.
  - Example: The HostileTiltDelta sub-200Hz cue is ONE of three channels (visual
    shimmer + audio + haptic). KBM-only players receive two of three. See
    `node-encounter.md` OQ-NE12.
- **Subtitles**: Any voice-over, narration, or diegetic speech must have
  subtitles on by default. Configurable background opacity (0–100%).
- **Mixer categories**: Separate Music / SFX / UI / Voice sliders in Settings.
- **Sub-bass separation**: Sub-200Hz cues occupy a dedicated audio bus so they
  can be attenuated without silencing SFX entirely.

---

## 5. Haptic & Motion

- **Gamepad haptic is additive only**: Never a sole information channel.
  KBM-only players are a supported first-class configuration.
- **Haptic intensity**: Configurable 0–100% scale in Settings, plus an off toggle.
- **KBM-only gap**: The HostileTiltDelta three-channel tell falls back to two
  channels (visual + audio) on KBM. This is documented in `node-encounter.md`
  and flagged for pre-1.0 remediation under OQ-NE12 (see Section 11).

---

## 6. Screen Reader Contracts

### Committed Event Surface

Every MVP system exposes a screen reader event hook. Integration to an actual
screen reader (NVDA/JAWS/VoiceOver) is pending ADR — see Section 11. Until the
ADR lands, systems fire events to a TTS-ready sink; the sink is a stub until
integration path is chosen.

### Per-System Hooks (locked by GDDs)

| System | Events / Format |
|--------|----------------|
| Status Effects (`design/gdd/status-effects.md`) | `OnStatusApplied(target, status, duration)`, `OnStatusExpired(target, status)`, `OnStatusTickDamage(target, status, amount)` |
| Node Map (`design/gdd/node-map.md` I.5) | Accessible name format: `"{EncounterType}, {ReachabilityState}, {FuelCost} fuel"` — e.g. `"Combat encounter, reachable, 2 fuel"` |
| Node Encounter (`design/gdd/node-encounter.md` I.4) | `OnHostileTiltChanged(deltaTier)` fires when visual shimmer changes tier |
| Enemy System (`design/gdd/enemy-system.md`) | Intent zone format: `"{ArchetypeDisplayName}: {IntentVerb} {SlotLabel} for {Value}{Unit}, telegraphed turn {TurnCount}"`. Updates on `OnIntentTelegraphed` and `OnIntentRetargeted`. |
| Vehicle & Part System (`design/gdd/vehicle-and-part-system.md` U8) | Tooltips keyboard-reachable; accessibility floors section per GDD |

### Format Discipline
- All accessible names use the localization string table — no hardcoded strings.
- Value + unit separation (`"15 HP"` not `"15HP"`) so screen readers pronounce
  units correctly.
- Numeric values spoken as numbers, not digits (`"twelve"` not `"one two"`).

---

## 7. Cognitive Accessibility

- **No timing-critical reads**: Every piece of game-critical information can be
  inspected for as long as the player needs. Turn timers, if any, are
  configurable or disableable.
- **Unlimited inspection time**: Hover / focus can hold a tooltip or inspection
  panel open indefinitely. No auto-dismiss on information surfaces.
- **Configurable tooltip dwell**: Tooltip appearance delay is configurable.
  Default 1.5s (from `status-effects.md`). Range 0.2s–3.0s in Settings.
- **Hover-focus parity**: Anything a mouse can reveal on hover, a keyboard can
  reveal on focus (`node-encounter.md` hover-vs-focus parity rule).
- **Consistent vocabulary**: Same term means the same thing across every system
  (enforced by the entity registry at `design/registry/entities.yaml`).

---

## 8. Per-System Accessibility Contracts (Index)

Each MVP GDD owns its own accessibility commitments. This doc is the umbrella;
the per-system sections are authoritative for system-specific rules.

| GDD | Section | Contract summary |
|-----|---------|------------------|
| `game-concept.md` | Accessibility philosophy | Color-as-information rule; colorblind mode test |
| `status-effects.md` | Accessibility Requirements | Color-independent icons, keyboard focus order, SR events, 1.5s tooltip dwell, no timing-critical reads |
| `node-map.md` | I.5 Accessibility Requirements | Colorblind beacon state (opacity/pulse/outline), unique beacon silhouettes, 16pt text floor, SR name format, keyboard-only playable, Reduce Motion for storm |
| `node-encounter.md` | I.4 + AC-NE18/19/20 | Three-channel HostileTiltDelta tell; hover-vs-focus parity; OQ-NE12 accessibility gate pre-1.0 |
| `enemy-system.md` | Intent accessibility hook | Intent zone string format; updates on telegraph + retarget events |
| `vehicle-and-part-system.md` | U8 Accessibility floors | Keyboard-reachable tooltips; floor rules per GDD |
| `card-combat-system.md` | (pending retrofit) | Card accessibility hooks queued via `/propagate-design-change` |

---

## 9. Testing & Validation

### Automated
- **Keyboard nav audit**: Unit test that every interactive UI element is reachable
  via Tab / Arrow keys and has a visible focus state.
- **USS font-size binding test**: Static analysis test that rejects USS files
  with hardcoded `font-size` values outside the theme variable.
- **No-color-only test**: Manual audit scaffolded by a dev-mode URP volume that
  desaturates the entire scene — every state must still be distinguishable.

### Manual (QA playtest gates)
- **Keyboard-only playthrough**: Full run with mouse unplugged. Regression gate.
- **Gamepad-only playthrough**: Full run with keyboard unplugged. Regression gate.
- **Colorblind simulator pass**: Every critical screen reviewed under protanopia,
  deuteranopia, and tritanopia simulation.
- **Reduce Motion regression**: Run with Reduce Motion on; verify no information
  loss and no unintended gated animations.
- **Text scaling regression**: Run at 150% text scale; verify no overflow or
  truncation on any critical UI.
- **Screen reader smoke test** (when integration lands): NVDA run through main
  menu, settings, one combat encounter, one node map transition.

### Tooling Notes
- Colorblind simulation is a URP custom post-processing volume (LMS matrix) with
  a dev toggle. ~50 lines of shader work; not in the asset-store dependency list.
- Screen reader integration path pending ADR (see Section 11).

---

## 10. Accessibility Settings UI

The Settings screen must expose, at minimum:

| Setting | Type | Default |
|---------|------|---------|
| Reduce Motion | toggle | off |
| Text Scale | slider 100–150% | 100% |
| High Contrast UI | toggle | off |
| Colorblind Preset | dropdown (None, Protanopia, Deuteranopia, Tritanopia) | None |
| Tooltip Dwell | slider 0.2s–3.0s | 1.5s |
| Subtitles | toggle | on |
| Subtitle Background Opacity | slider 0–100% | 75% |
| Haptic Intensity | slider 0–100% + off | 100% |
| Music / SFX / UI / Voice | sliders 0–100% | 100% |
| Key Rebinding | full remap menu | (defaults) |

Settings persist via the save system per ADR-0004 (MasteryState category — settings
are cross-run player preferences, never reset on run loss).

---

## 11. Open Gates & Pending Decisions

### Screen Reader Integration Path (ADR-pending)
**Status**: Event contracts locked by GDDs; integration path TBD.

**Options**:
- **A. UI Accessibility Plugin (UAP)** by MetalPopGames — Asset Store package
  (~$60). Bridges Unity UI to NVDA/JAWS/VoiceOver. Most common path.
- **B. Custom TTS pass** — Emit events to a TTS layer we build. More work,
  full control.
- **C. Defer integration to post-1.0** — Event surface is in place; actual
  TTS wiring lands in a post-launch patch. Documented as a known gap.

**Next action**: Run `/architecture-decision` when screen reader implementation
work is scheduled. Decision blocks full Standard tier compliance on the screen
reader axis.

### OQ-NE12: HostileTiltDelta Accessibility Review (pre-1.0 gate)
**Source**: `design/gdd/node-encounter.md` K.5.

**Status**: Provisional three-channel tell (visual shimmer + sub-200Hz audio +
gamepad haptic). KBM-only players receive two channels.

**Gate**: Formal accessibility review before 1.0 ship. Remediation may include
a fourth non-color / non-audio / non-haptic channel (e.g., reticle icon change),
folded into Art Bible §3 under AD-C2.

**Owner**: accessibility-specialist. **Trigger**: pre-1.0, post-MVP.

### AD-C2: Colorblind-Safe Ambush Overlay (Art Bible §3)
**Status**: CLOSED 2026-04-25.

**Requirement**: Ambush overlay must be distinguishable via non-color channel
(shape, pattern, motion) for colorblind players.

**Resolution**: Specified in `design/art/art-bible.md` §3.5 "Encounter Type Signal Geometry (Standard vs. Ambush)". Non-color channel = single top-left corner cut (4px at 45°) + chevron glyph prefix on labels / chevron anchor on text-free surfaces. Greyscale acceptance test included. The Combat HUD Ambush urgency tint (OQ-CH1) is now formally additive, not load-bearing — color may amplify the signal but cannot carry it alone.

---

## 12. Review Cadence

- **Per-sprint**: accessibility-specialist reviews any new interactive UI for
  keyboard reachability and no-color-only compliance before merge.
- **Per-milestone**: Full manual QA pass against Section 9 checklist.
- **Pre-1.0**: Formal accessibility review closes OQ-NE12 and any other open
  gates in Section 11.

---

## 13. Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-04-25 | Initial doc. Standard tier committed. Screen reader integration flagged as pending ADR. | PR-C1a gate-check condition closure (Technical Setup phase). |
| 2026-04-25 | §3.3 Reduce Motion — added Legendary rarity pulse to gated list with pointer to art-bible §4.7 fallback. | PR-C2 gap-check item G2 — QA traceability for the Legendary pulse Reduce Motion alternative. |
| 2026-04-25 | §3.3 Reduce Motion — disambiguated "rust shimmer" entries: decorative ambient rust shimmer remains gated; §H.3 information-bearing rust-shimmer tell is alternative-rendered (static frame at peak luminance) rather than gated. Cross-references art-bible §7.4 Rust-Shimmer Tell. | PR-C2 gap-check item AD-C1 — separating decorative motion from information-bearing motion that happens to share the same name. |
| 2026-04-25 | §11 AD-C2 — closed. Resolution: art-bible §3.5 "Encounter Type Signal Geometry" specifies non-color channel (single top-left corner cut + chevron glyph) with greyscale acceptance test. Combat HUD Ambush urgency tint reframed as additive, not load-bearing. | PR-C2 gap-check item AD-C2 — colorblind-safe Ambush overlay precondition for Map UX handoff. |

# Combat HUD

> **Status**: Ready for Review (all 8 sections authored 2026-04-24)
> **Author**: user + ux-designer
> **Last Updated**: 2026-04-24
> **Template**: HUD Design
> **Feeder GDDs**: card-combat-system, enemy-system, card-system, vehicle-and-part-system, status-effects, scrap-economy, loot-reward, save-persistence, node-encounter, node-map
> **Feeder prototype**: Carry-forward contracts from pre-production prototype sessions — absorbed into §5 Dynamic Behaviors (Turn-1 Plate wasted, Ambush cold-start, prior-frame flip tenet). Prototype milestone waived per `production/milestones/prototype-waiver.md`.

---

## HUD Philosophy

The Combat HUD is a **reading surface**, not an interface. Its job is to make every decision-critical piece of information visible at all times, with the math already resolved on behalf of the player. Combat in Wasteland Run is deliberate, turn-based, and luck-constrained — the HUD exists so the player can read *what's happening*, not compute it.

This stance grounds in three pillars and one anti-pillar from `design/gdd/game-concept.md`:

- **Pillar 3 (Read to Win)**: victory comes from reading the enemy, current build, and the options luck provides. Every HUD element exists to support a specific read.
- **Pillar 2 (Chassis Identity)**: both vehicles are rendered as 4-subsystem portraits, never as a single HP bar. The player reads the *opponent* the same way they read their *own vehicle* — by seeing which parts are hurt, destroyed, armored, or fizzling.
- **Pillar 4 (Energy scarcity)**: Energy is the combat resource. It lives in a dedicated, always-visible HUD element that never competes with map-layer resources (Scrap and Fuel are OUT OF SCOPE for this HUD).
- **Anti-pillar "NOT a reflex game"**: no HUD element ticks against wall-clock time, no element demands reaction inside a window. Every read is available for as long as the player needs it.

### The five governing tenets

1. **Reads before inputs.** Every pre-turn decision must be answerable from what is on screen *right now*. The player never needs to remember last turn's state, hover a tooltip to see a damage number, or do arithmetic to decide whether to play a card. Hover tooltips exist only to *elaborate* already-visible information (full card text, full status-effect description), never to gate decision-critical state.

2. **Dual vehicle portraits.** Both vehicles render as persistent 4-subsystem portraits (Weapon / Engine / Wheels / Frame), not as an aggregate HP bar. Each subsystem renders its HP, its `ALIVE | BROKEN` state, and any applicable modifier badges (FIZZLE, single-hit). Armor renders as an over-HP overlay with its cap-line visible. This symmetry is load-bearing: the player learns one reading pattern and applies it to both sides.

3. **Math made visible.** Every number that affects resolution is rendered, not computed. Intent previews show `PredictedDamage + (conditional PositionBonus)` as a literal preview — the bonus clause is drawn *only when the bonus is currently active*, so the player reads the actual effective number. Splash damage, FIZZLE, single-hit modifiers, and status-effect amplifications are all applied in the preview before the enemy acts. Card previews show post-resource cost and post-effect damage with the same contract.

4. **Prior-frame state flips.** When an enemy's subsystem goes BROKEN, any of its intent telegraphs affected by that break flip to their modified state (FIZZLE badge, single-hit badge, un-enraged pool) on the *prior frame* — before the intent resolves. The player reads "this hit is neutralised" one full frame before the damage would land, which is what makes the Symmetric Consequence Matrix legible rather than a surprise.

5. **No reflex gates.** The HUD contains zero real-time elements: no ticking timers, no shrinking input windows, no reaction-based states. Animations communicate state change (position swap, subsystem destruction, enrage trigger) but they do not impose a time budget. A player who looks away for thirty seconds returns to an unchanged HUD state.

### What this philosophy forbids

- **No hidden math.** No damage calculation happens off-screen. If the game's resolution will modify a number, the modification appears in the preview.
- **No decision-gated hovers.** Tooltips supplement; they never gate.
- **No phase-modal HUD.** The HUD does not morph between combat phases. Every decision-critical channel renders in every phase — only the *highlighted* element changes.
- **No off-HUD resources in combat.** Scrap and Fuel do not appear in the Combat HUD (they exist in the Map HUD and node-encounter overlays). The Combat HUD only renders the three in-combat resources: Energy, Armor, and Subsystem HP.
- **No reflex-based confirmation.** "Hold to confirm" and "tap during window" patterns are forbidden. All confirmations are discrete clicks / button presses with no time constraint.

### What this philosophy costs

The Reading Surface stance is screen-real-estate-heavy. Eight subsystem HPs + two armor overlays + two position indicators + intent preview zone + hand + energy + turn counter + EncounterType tag + any active status badges = a dense layout. Subsequent sections (Information Architecture, Layout Zones, HUD Elements) must solve the density problem without dropping any channel. This is a constraint, not an escape clause.

---

## Information Architecture

### Full Information Inventory

Every piece of state the Combat HUD must communicate, aggregated from the 10 feeder GDDs and the prototype carry-forward. Each row is a HUD channel.

| # | Channel | Source Binding | Source GDD | Urgency |
|---|---------|----------------|------------|---------|
| 1 | EncounterType tag (Standard / Ambush) | `CombatSetup.EncounterType` | Card Combat R15 | Decision-critical (Setup frame 1) |
| 2 | Turn counter | `CombatContext.TurnCount` | Card Combat S3 | Ambient |
| 3 | Combat phase highlight | `CombatLoop.Phase` (Setup / PlayerTurn / PlayerResolve / EnemyTurn / Ended) | Card Combat S1 | Ambient |
| 4 | End Turn button | Enabled iff `Phase == PlayerTurn` | Card Combat R2 | Decision-critical |
| 5 | Player hand (up to MaxHandSize cards) | `Card System.Hand` | Card System UI | Decision-critical |
| 6 | Hand count + deck / discard / exhausted counters | `Card System.DeckState` | Card System §States | Ambient |
| 7 | Energy pool (current / max) | `CombatContext.Energy / MaxEnergy` | Card Combat S4 | Decision-critical |
| 8 | Card tooltip (full) | hover / focus on a hand card | Card System UI | Contextual |
| 9 | Keyword-state indicator (Retain / Ethereal / Innate / Exhaust) per hand card | Card runtime flags | Card System UI | Contextual |
| 10 | Playable / unplayable card dim | `EnergyCost > Energy` or `PositionRequirement` fails | Card System UI | Decision-critical |
| 11 | Position indicator (Player) — `Ahead / Behind` | `PositionState.Position` (player) | Card Combat S2 | Decision-critical |
| 12 | Position indicator (Enemy) | `PositionState.Position` (enemy) | Card Combat S2 | Decision-critical |
| 13 | Player 4-subsystem portrait (Weapon / Engine / Mobility / Frame) | `VehicleState.Slots[s].DamageState` + `SlotStateChanged` | V&P + Card Combat R5 | Decision-critical |
| 14 | Enemy 4-subsystem portrait | same, enemy side | V&P + Card Combat R5 | Decision-critical |
| 15 | Per-slot `ALIVE / BROKEN` badge (both sides, 8 slots) | `DamageState == Offline` | Card Combat OQ-CC-NEW-3/4 matrix | Decision-critical |
| 16 | Per-slot PlatingStacks counter (non-Frame slots, both sides) | `StatusInstance(Plating).Stacks` | Status Effects | Ambient |
| 17 | Player `CurrentArmor / MaxArmor` bar with cap-line visible | `VehicleState.CurrentArmor` + `MaxArmor` | V&P + Status Effects | Decision-critical |
| 18 | Enemy `CurrentArmor / MaxArmor` bar with cap-line visible | `VehicleState.CurrentArmor` + `MaxArmor` | V&P + R4 | Decision-critical |
| 19 | Enemy Intent glyph (48×48 min) | `NextIntent.Type` → glyph lookup | Enemy System I.1 | Decision-critical |
| 20 | Enemy Intent target-slot label | `NextIntent.TargetSlot` | Enemy System I.1 | Decision-critical |
| 21 | Enemy Intent predicted-value readout — `PredictedDamage + (conditional PositionBonus)` | `NextIntent.PredictedDamage` + `PositionBonus` if active | Card Combat F-CC5 | Decision-critical |
| 22 | Intent FIZZLE badge (Weapon BROKEN → attack-family fizzles) | Derived: `enemy.Weapon.Offline && intent ∈ {Attack, Multi, DebuffAttack}` | Card Combat L1204 matrix | Decision-critical |
| 23 | Intent single-hit modifier badge (enemy Wheels BROKEN → Multi-Attack becomes single hit) | Derived: `enemy.Wheels.Offline && intent.Type == Multi` | Card Combat L1241 matrix | Decision-critical |
| 24 | Intent Plate-fizzle badge (enemy Engine BROKEN → Plate intents fizzle + un-enrage) | Derived: `enemy.Engine.Offline && intent.Type == Plate` | Card Combat L1241 matrix | Decision-critical |
| 25 | Synthetic RepositionIntent glyph (reposition, not attack) | `NextIntent.Type == Reposition` | Card Combat R18 | Contextual |
| 26 | Intent retarget animation (3-phase old → new) | `IEnemyEventSource.OnIntentRetargeted` | Enemy System I.2 | Event-driven |
| 27 | Enemy HP readout (numeric + bar) | `VehicleState.HullHP / MaxHullHP` | Enemy System I.1 | Ambient |
| 28 | Player HP readout (numeric + bar) | `VehicleState.HullHP / MaxHullHP` | V&P | Ambient |
| 29 | Enemy Name / Family plate | `EnemyDefinitionSO.DisplayName` + `ArchetypeFamily` + `BiomeIndex` | Enemy System I.1 | Ambient |
| 30 | Enrage indicator (active) — red rim + ENRAGE text | `OnEnrageActivated` event | Enemy System I.1 + Card Combat R7 | Decision-critical (once active) |
| 31 | Enrage warning (pre-activation) — "ENRAGE IN N" | `TurnsUntilEnrage <= EnrageTelegraphLeadTurns` | Card Combat R7 | Contextual (turns 6–7 under default tuning) |
| 32 | Status chip row (Player `ActiveStatuses`) | `IVehicleView.ActiveStatuses` | Status Effects | Decision-critical |
| 33 | Status chip row (Enemy `ActiveStatuses`) | `IVehicleView.ActiveStatuses` | Status Effects | Decision-critical |
| 34 | Per-chip inspection tooltip | hover / keyboard focus on chip | Status Effects | Contextual |
| 35 | Per-subsystem Corroded indicator on silhouette | `StatusInstance(Corroded).TargetSlot` | Status Effects | Decision-critical |
| 36 | Card-hover damage preview (post-Corroded, post-Plating, post-Armor) | `IStatusQuery.GetCorrodeBonusForAttack` + `DamageEffect.Amount` | Status Effects UI | Contextual |
| 37 | Targeting reticule / valid-slot highlight on enemy portrait when card selected | `ValidSubsystemTargets` on selected card | Card Combat R3 / Card System UI | Contextual |
| 38 | Damage-number flyout (source → target slot, per hit) | `DamageApplied` event | Card Combat F-CC1 | Event-driven |
| 39 | Slot destruction animation (Functional → Offline) | `SlotStateChanged(slot, _, Offline)` | V&P + Card Combat R5 | Event-driven |
| 40 | Position swap animation | `PositionChanged(old, new)` | Card Combat S2 | Event-driven |
| 41 | Save-in-progress indicator | `ISaveService.IsCommitInProgress` | Save & Persistence R2 | Contextual (ambient; cosmetic, does not gate play) |
| 42 | Screen-reader announce stream | turn transitions + status tick/expire + intent changes + slot destruction | Status Effects + Enemy System + accessibility | Always-on (non-visual) |
| 43 | NoOpIntent tell (intent glyph renders distinct from Defend / Attack) | `NextIntent.Type == NoOp` | Status Effects EC + Enemy System I.1 | Contextual |

**Total: 43 channels.** Every channel has an authoritative source binding; no HUD-invented state.

### Explicitly OUT of scope (not rendered in Combat HUD)

The following channels exist elsewhere in the game and MUST NOT appear in the Combat HUD:

| Excluded Channel | Rendered Where | Why excluded from Combat HUD |
|------------------|----------------|------------------------------|
| Scrap balance | Map HUD, Merchant UI, Chopshop UI | Pillar 4 — Scrap is a build-economy resource, not a combat resource. Showing it in combat blurs the three-domain separation. |
| Fuel balance | Map HUD | Pillar 4 — Fuel is a routing resource. Same rationale. |
| Run seed / deterministic diagnostic info | Debug overlay only | Player-facing HUD must not expose RNG seed. |
| Enemy AI brain internals (pool weights, intent candidate list) | Never | Information Hiding — exposing these defeats the Read to Win puzzle. |
| Post-combat reward preview | Post-Combat Flow screen (separate UX spec) | Different context; different information architecture. |
| Node map preview (next-node options) | Map HUD / Node Map UI | Different screen entirely. |
| Prototype's `-50% damage applied` state | Never (deprecated) | OQ-CC-NEW-3 resolved to Symmetric Fizzle — the `-50%` visual state is forbidden. |
| Specific MaxArmor numeric bands (5/8/15/10/20) as visual language | Never (balance-owned) | Those are prototype-authored tuning values, not canonical. |

### Categorization

The 43 channels organize along two primary axes: **Urgency** (how often is this read?) and **Spatial Owner** (who owns the real estate?).

#### By urgency

| Tier | Count | Definition | Rendering Contract |
|------|-------|------------|--------------------|
| **Decision-Critical** | 22 | Player reads this channel to decide *this turn's* play. | Always rendered, always legible, never occluded, fixed screen position, high-contrast. |
| **Ambient** | 7 | Player absorbs this channel over time; not read each turn. | Always rendered, can use secondary contrast, can be in peripheral zones. |
| **Contextual** | 11 | Appears on condition (Enrage warning, reposition intent, save indicator, tooltips, etc.). | Rendered only when condition is true, animates in/out, must not shift layout of Decision-Critical channels. |
| **Event-driven** | 3 | Animated feedback for state changes (damage flyout, slot destruction, position swap). | Transient. Must not gate input or lock the HUD. |

#### By spatial owner

| Zone | Channels | Notes |
|------|----------|-------|
| **Player-side** (left 45% per scene framing) | 5, 6, 7, 11, 13, 15, 16, 17, 28, 32, 35, 37 (target highlight returns here) | Mirrors Enemy-side structure for the Dual Portrait tenet |
| **Enemy-side** (right 45%) | 12, 14, 15, 16, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 29, 30, 31, 33, 34, 35, 43 | Intent Zone owns the top-right quadrant per Enemy System I.1 |
| **Neutral / cross-vehicle** (center, top, bottom bands) | 1, 2, 3, 4, 38, 40, 41, 42 | EncounterType tag (top band), turn counter (top band), End Turn (bottom-right), damage flyouts (mid-air), save indicator (corner) |
| **Overlay** (appears over other zones) | 8, 9, 10, 36 | Card tooltip + hover previews render above base layout |

### Visual Budget

| Metric | Value | Enforcement |
|--------|-------|-------------|
| **Max simultaneously-rendered Decision-Critical channels** | 22 (enumerated above) | Adding a new always-on channel requires removing or demoting an existing one. Escalate to UX before implementation. |
| **Max simultaneously-rendered Contextual channels** | 6 (worst-case: Enrage warning + RepositionIntent + targeting reticule + card-hover preview + save indicator + NoOpIntent tell) | Contextual channels must not shift Decision-Critical layout. If a new Contextual element cannot fit without occlusion, it is out of scope. |
| **Max screen area occupied by static HUD zones at 100% HUD Scale** | ~27% (player chassis) + ~27% (enemy chassis) + ~4% (intent zone) + ~13% (hand zone) + ~4% (energy/deck) + ~4% (end turn/save) = **~79% at max**. The remaining ~21% is Chase Rail background — not HUD. | HUD zones must not collectively exceed this. On ultrawide, the 16:9 safe rectangle is honored and letterbox is background-only. |
| **Max total channels rendered simultaneously (all tiers)** | 43 max (all channels active at once is the worst case: enrage active + status chips on both sides + targeting mode + save in-flight). | No additional channels may be added without a full Information Architecture re-review. |

**Red lines** (escalate to UX immediately if any is hit):
- A new always-on element is proposed.
- Any Decision-Critical channel would be occluded by a Contextual or Event-driven element.
- At 130% HUD Scale, a zone's contents overflow their zone boundary.

#### Derived architectural rules

1. **All 22 Decision-Critical channels must be simultaneously legible** without any player input. Meeting this is the central layout challenge for § Layout Zones.
2. **Enemy-side and Player-side render the same channel types** (tenet 2 — Dual Portraits). Asymmetry exists only in Intent Zone (enemy-only) and Hand/Energy (player-only).
3. **No Decision-Critical channel may be occluded** by a Contextual or Event-driven element. Card tooltips, damage flyouts, targeting reticules must render on overlay layers that do NOT obscure intent, position, HP, armor, or subsystem state.
4. **Intent Zone is the highest-z element** (Enemy System I.1 rule). Everything else renders behind it.

---

## Layout Zones

All placement is expressed in screen-relative terms using a 16:9 reference (1920×1080). Enemy-side zones preserve the spec locked by Enemy System I.1; Player-side zones mirror that structure for the Dual Portraits tenet.

### Zone Map

```
 ┌────────────────────────────────────────────────────────────────────┐
 │                           TOP BAND (8% h)                          │  → [1] EncounterType  [2] Turn Ctr  [3] Phase
 ├────────────────────────────────────────┬───────────────────────────┤
 │                                        │                           │
 │                                        │      [4] INTENT ZONE      │
 │                                        │      (top-right 20%×22%)  │
 │                                        │      — highest z-order —  │
 │        [5] PLAYER CHASSIS ZONE         │                           │
 │           (left 45% × 62% h)           │  ┌─────────────────────┐  │
 │                                        │  │ [6] ENEMY CHASSIS   │  │
 │         Name plate (above sprite)      │  │ ZONE (right 45%)    │  │
 │         Subsys portrait (W/E/M/F)      │  │                     │  │
 │         Position glyph (↑Ahead)        │  │   Name plate        │  │
 │         Armor bar + cap line           │  │   Subsys portrait   │  │
 │         HP readout                     │  │   Position glyph    │  │
 │         Status chip row                │  │   Armor + cap line  │  │
 │                                        │  │   HP readout        │  │
 │           CHASE RAIL BACKGROUND        │  │   Status chip row   │  │
 │           (parallax differential)      │  │   Enrage rim (cnd)  │  │
 │                                        │  └─────────────────────┘  │
 ├────────────────────────────────────────┴───────────────────────────┤
 │                                                                    │
 │               [7] HAND ZONE (bottom 22% h)                         │
 │            (fanned 5 cards, fills center 60% width)                │
 │                                                                    │
 ├─────────────────┬──────────────────────────────┬───────────────────┤
 │ [8] ENERGY +    │         (neutral)            │ [9] END TURN +    │
 │ DECK COUNTERS   │                              │ SAVE INDICATOR    │
 │ (bottom-left)   │                              │ (bottom-right)    │
 └─────────────────┴──────────────────────────────┴───────────────────┘
```

### Zone definitions

| # | Zone Name | Coordinates (1920×1080 ref) | Always Visible? | Contents (channel IDs from § Information Architecture) |
|---|-----------|------------------------------|-----------------|----------------------------------------------------------|
| 1 | **Top Band** | Full width × top 8% height (0,0 → 1920,86) | Yes | EncounterType tag (ch1, centered or left-of-center), Turn counter (ch2, centered), Phase highlight (ch3, subtle background tint on phase change) |
| 2 | **Player Chassis Zone** | Left 45% × top 70% (exclusive of top band) — (0, 86 → 864, 756) | Yes | Player subsystem portrait (ch13), per-slot ALIVE/BROKEN badge + PlatingStacks counter (ch15, ch16), Armor bar with cap-line (ch17), HP readout (ch28), Name plate (above sprite, thin strip), Position glyph (ch11, overlaid on sprite), Status chip row (ch32), Corroded indicators (ch35, on silhouette), Targeting reticule echo (ch37) |
| 3 | **Enemy Chassis Zone** | Right 45% × top 70% (exclusive of top band + intent zone) — (1056, 86 → 1920, 756), minus Intent Zone | Yes | **Identical channel set to Player Chassis Zone** (ch14, ch15, ch16, ch18, ch27, ch29, ch12, ch33, ch35), plus conditional Enrage rim + amber text (ch30), Enrage warning (ch31), NoOpIntent tell (ch43). Mirrors Player-side layout for Dual Portraits parity. |
| 4 | **Intent Zone** | Top-right corner; 20% width × 22% height; 24px inset — (1536, 110 → 1896, 334) | Yes (highest z-order) | Enemy Intent glyph (ch19, 48×48 min), target-slot label (ch20), predicted-value readout with F-CC5 `PredictedDamage + (conditional PositionBonus)` (ch21), FIZZLE badge (ch22), single-hit badge (ch23), Plate-fizzle badge (ch24), synthetic RepositionIntent glyph (ch25), intent retarget animation space (ch26) |
| 5 | **Chase Rail Background** | Full Player + Enemy Chassis Zone width (0, 86 → 1920, 756), behind all vehicle art | Yes | Parallax-scrolling wasteland background. Scroll speed differential communicates position: the vehicle that is **Ahead** gets a background scrolling slightly *faster past its sprite* (wind-in-hair effect); the vehicle that is **Behind** gets a background scrolling slightly *slower* (trailing). Speed differential is 10–15% around a shared base rate (art-direction tunable). Position swap animation reverses the differential over 500ms with an acceleration easing curve. |
| 6 | **Hand Zone** | Center 60% × bottom 22% (384, 864 → 1536, 1080) | Yes | Player hand (ch5, fanned, up to MaxHandSize), playable/unplayable dim (ch10), keyword-state indicator per card (ch9), card tooltip overlay (ch8, renders above Hand Zone on hover/focus), card-hover damage preview (ch36, renders in / adjacent to intent zone when a card is hovered) |
| 7 | **Energy + Deck Counters Zone** | Bottom-left corner; 20% width × 22% height (0, 864 → 384, 1080) | Yes | Energy pool (ch7, current/max, largest typography in zone), deck / discard / exhausted counters (ch6, smaller) |
| 8 | **End Turn + Save Indicator Zone** | Bottom-right corner; 20% width × 22% height (1536, 864 → 1920, 1080) | Yes | End Turn button (ch4, large, always-visible, disabled when `Phase != PlayerTurn`), Save-in-progress indicator (ch41, small icon, top-right of zone, faded when not saving) |
| 9 | **Overlay Layer** | Full screen, rendered above all other zones | Event-driven | Damage-number flyouts (ch38, animate from source slot → target slot → fade), slot destruction animation (ch39, expands from affected portrait), position swap animation (ch40, chase-rail differential reversal + position glyph flip), screen-reader stream (ch42, non-visual) |

### Position state rendering contract (chase-rail differential)

1. **Zones never move.** Player chassis stays left, enemy chassis stays right, for the entire combat — regardless of `PositionState`.
2. **Position glyph** on each vehicle's Chassis Zone (upper-right corner of sprite canvas) is a chevron: `↑` if that vehicle is Ahead, `↓` if Behind. Glyph is always rendered; the direction updates on `PositionChanged`.
3. **Chase-rail parallax differential** encodes position ambiently. The Chase Rail Background scroll speed varies per-vehicle half of the screen: the Ahead vehicle's background scrolls 10–15% faster than base; the Behind vehicle's scrolls 10–15% slower. The differential is visible without fixation — the player absorbs "who's pulling ahead" via peripheral motion.
4. **Position swap animation** (500ms, `PositionChanged` event):
   - Glyph flips on both vehicles (simultaneous).
   - Background parallax differential smoothly reverses over 500ms with an ease-in-out curve.
   - A one-shot wasteland motion effect (speed lines, dust plume) fires from each vehicle's trailing edge in the new direction — art-directed cue, not gameplay-critical.
   - No zone geometry changes. No HUD element moves.
5. **Ambush encounter cold-start** (EncounterType = Ambush at Setup): chase-rail differential boots at the "enemy Ahead" configuration from frame 1. The EncounterType tag in Top Band renders the Ambush motif on frame 1. Enemy Setup intent resolves before player's first turn — standard Intent Zone rendering, just fired one turn earlier than a Standard encounter.

### Symmetry & asymmetry rules

- **Symmetric** (identical channel set, identical layout): Chassis Zone contents (portrait, HP, armor bar, status chips, position glyph, Corroded indicators, slot ALIVE/BROKEN badges, PlatingStacks counters).
- **Asymmetric (enemy only)**: Intent Zone (no player-side equivalent because the player has no pre-committed intent — they play cards), Enrage rim + warning, NoOpIntent tell.
- **Asymmetric (player only)**: Hand Zone, Energy + Deck Counters Zone, End Turn button, targeting reticule echo (origin is player hand, target is enemy chassis).

### Z-order (back to front)

1. Chase Rail Background (parallax)
2. Vehicle sprites (player + enemy, same layer)
3. Chassis Zone sub-elements (name plate, subsystem portrait, armor bar, HP, status chips)
4. Hand Zone (cards)
5. Energy / End Turn / Counter zones
6. Top Band (EncounterType, turn counter)
7. **Intent Zone** (highest static z — Enemy System I.1 rule)
8. **Overlay Layer** (tooltips, hover previews, damage flyouts, destruction animations — above Intent Zone temporarily during animation, but must not occlude intent glyph; clip or offset if collision)
9. Screen-reader announce stream (non-visual, no z)

### Safe-zone / platform notes

Target platform: PC (Steam) at minimum 1280×720 up to 4K. Zones are defined as percentages, not pixels — scale uniformly. For 21:9 and 32:9 ultrawide, the Chase Rail Background extends into the letterbox zones but all HUD elements (zones 1, 4, 6, 7, 8) remain anchored at the 16:9 safe rectangle. Aspect ratios narrower than 16:9 are out of scope (PC-only, no mobile).

---

## HUD Elements

Per-element rendering contracts, organized by zone. Each element has a **channel ID** (from § Information Architecture), a **data source**, a **default state**, any **state variations** it can enter, and the **trigger** for each variation. Color tokens, exact typography, and final art treatment are owned by Art Direction — this section specifies behavioral contracts only.

### Zone 1 — Top Band

| Element | Ch | Default State | State Variations | Notes |
|---------|----|---------------|------------------|-------|
| **EncounterType Tag** | 1 | Renders the EncounterType motif on Setup frame 1. `Standard` → neutral-tone motif, left-aligned in top band. `Ambush` → urgency-tinted motif (warning amber or red accent per Art Direction), with a brief attention-pull animation on Setup frame 1 only (fade-in + subtle pulse, ≤600ms, then static). | None after Setup frame 1 — tag is immutable for the combat. | Must render BEFORE the enemy's Setup intent resolves in Ambush — priority layout. Boss nodes are locked to `Standard` per Node Map validator (AC-NM45c), so Boss combats never render Ambush tag. |
| **Turn Counter** | 2 | Integer "Turn N", centered in top band, increments on each `PlayerTurnStarted`. | Enrage warning overlay: when `TurnsUntilEnrage <= EnrageTelegraphLeadTurns` (default 2) AND `EnrageActive == false`, append "· ENRAGE IN N" in amber. When `EnrageActive == true`, append "· ENRAGE" in red. | Linked to § Information Architecture ch2 + ch31. The warning overlay is a text append, not a separate element, so it renders in the same read. |
| **Phase Highlight** | 3 | Subtle background tint on the entire top band, phase-specific. `Setup` → darker / muted. `PlayerTurn` → baseline. `PlayerResolve` → brief pulse (≤400ms) then back to baseline. `EnemyTurn` → enemy-side-colored tint. `Ended` → N/A (HUD fades to post-combat). | Cross-fades 200ms between phases. | Ambient channel — player absorbs phase via peripheral color, not by reading. |

### Zone 2 + 3 — Chassis Zones (Player Left / Enemy Right — identical contracts)

| Element | Ch | Default State | State Variations | Notes |
|---------|----|---------------|------------------|-------|
| **Subsystem Portrait** | 13, 14 | 4-slot silhouette layout per ADR-0001: Weapon, Engine, Mobility/Wheels, Frame visually represented with DamageState-appropriate art (`Functional` / `Degraded` / `Offline`). | `Functional` → full-color, clean silhouette. `Degraded` → desaturated, visible damage overlay. `Offline` → broken / smoking / bullet-holed treatment per ADR-0001. Transition on `SlotStateChanged` event (crossfade ≤300ms). | Layout is vertical stack flush to the outer edge of each Chassis Zone (per Enemy System I.1 rule). Slot order: Weapon top → Engine → Mobility → Frame bottom. Symmetric on both sides. |
| **Slot ALIVE/BROKEN Badge** | 15 | When `DamageState != Offline`, no badge — element reads as ALIVE via the portrait's clean silhouette. When `DamageState == Offline`, a discrete BROKEN badge renders on the slot (small icon — e.g., crossed-out chevron or fracture glyph — per Art). | Badge appears on `SlotStateChanged(_, _, Offline)`, persists until `SlotStateChanged(_, Offline, _)` fires (repair). | Binary state — no intermediate. The badge is a supplemental cue on top of the silhouette DamageState art; both render simultaneously. |
| **PlatingStacks Counter** | 16 | Small integer overlay on non-Frame slot icons (Weapon, Engine, Mobility). Rendered only when `StatusInstance(Plating).Stacks > 0` on that slot. | Incrementing animation (+1 tick) when stacks increase. Decrementing animation (−1 tick) when hit consumes plating. Fade-out when Stacks reaches 0. | Not shown on Frame slot — Frame uses Armor, not Plating, per status system rules. |
| **Armor Bar + Cap Line** | 17, 18 | Horizontal bar attached to the Frame slot, labeled with `CurrentArmor / MaxArmor` integer pair. Bar fills left-to-right proportional to `CurrentArmor`. **Cap line** is a visible tick mark at `MaxArmor` on the bar — distinct from the fill line — so the player reads "where the ceiling is" at a glance. Color-band distinct from HP (per Card Combat UI R9). | Fill animates on `OnCurrentArmorChanged`. Empty-state: at `CurrentArmor == 0`, bar renders empty at 40% opacity for legibility continuity. Cap line moves only on `MaxArmor` change (rare — part install / upgrade). | Prototype REPORT.md carry-forward: **cap line is the read-surface for Plate-now-or-save decisions**. Must be visible, not a hidden property of the bar. |
| **HP Readout** | 27, 28 | Numeric `HullHP / MaxHullHP` + horizontal bar fill. Derived from sum of slot HPs. | Red-tint pulse on damage taken (≤400ms). When `HullHP == 0`, bar renders empty; death cascade animates from the Frame slot. | Ambient channel — player checks it when strategizing, not each turn. |
| **Name Plate** | 29 | Thin strip above the chassis sprite, horizontally centered. Player-side shows chassis family + current loadout name (e.g., "Scout · Raider Build"). Enemy-side shows archetype name + biome tag (e.g., "Patch Rider · Raider · Biome 1"). | Static — set on combat Setup, does not update during combat. | Ambient. Player-side plate optional for MVP if loadout name is not available from the save layer — can fall back to just chassis class name. |
| **Position Glyph** | 11, 12 | Chevron overlay on the upper-right corner of the sprite canvas. `↑` when `PositionState.Position == Ahead`, `↓` when `Behind`. | Flip animation on `PositionChanged` event (simultaneous both vehicles, 200ms). | Rendered always — never absent. Pairs with Chase Rail Background differential for redundant communication. |
| **Status Chip Row** | 32, 33 | Horizontal row of small icons (chips) rendered below the portrait — one chip per entry in `ActiveStatuses`. Each chip shows: status icon, `Stacks` numeral (if Graduated type), small duration pips. | Chip enters on `StatusApplied`, exits on `StatusExpired`. Chip pulses on stack change. On hover / keyboard focus, chip triggers tooltip (ch34). | Identical chip-level information content on both vehicles (Status Effects Display Parity rule). Layout / density may differ per side if real estate dictates, but info content is symmetric. |
| **Corroded Slot Indicator** | 35 | When a `StatusInstance(Corroded)` targets a specific slot, a small "vulnerable" marker overlays that slot in the portrait (per Status Effects). Not a chip — a silhouette-level indicator. | Appears on Corroded apply, disappears on expire or slot repair. | Redundant-channel: also appears as a chip in Status Chip Row (ch32/33). Silhouette indicator is the *where* read; chip is the *what* read. |
| **Targeting Reticule Echo** | 37 | When player hovers a card in hand with `TargetType == EnemySubsystem`, a reticule overlays the enemy's `ValidSubsystemTargets`. Invalid slots visually dim. | Appears on card hover / gamepad select, disappears on card deselect or play. | Mirror-cue: if the hovered card targets a player-side slot (e.g., self-plate), reticule appears on player portrait instead. |
| **Enrage Rim + Text** | 30 | Enemy-side only. On `OnEnrageActivated`, a 2px red rim applies to the enemy sprite canvas and an amber "ENRAGE" text label renders in the upper-left corner of the Enemy Chassis Zone. Persistent once applied. | Un-enrage (Engine BROKEN) removes the rim and text with a brief de-emphasis animation; re-applies if enrage retriggers after Engine repair. | Per Enemy System I.1. Distinct visual identity — must not collide with targeting reticule or Status Chip Row. |
| **NoOpIntent Tell** | 43 | When `NextIntent.Type == NoOp` (Status Effect forced skip, e.g., enemy Stalled), the enemy chassis renders a muted overlay + the Intent Zone glyph shows a "—" with secondary-tone styling. | Appears on intent selection as NoOp, clears on next intent. | Consumers: Combat HUD + screen reader. Per Status Effects EC, NoOp must render distinctly from Defend / Attack — "nothing happens" must not be confused with "defending." |

### Zone 4 — Intent Zone

| Element | Ch | Default State | State Variations | Notes |
|---------|----|---------------|------------------|-------|
| **Intent Glyph** | 19 | 48×48px minimum icon; type-specific glyph (Attack / Multi / Defend / Debuff / Status / Reposition / NoOp). Lookup table authoritative per Enemy System I.2. | Retarget animation: 3-phase transition (fade out old → neutral pause → fade in new) per Enemy System I.2 `OnIntentRetargeted`. | Highest z-order. Must never be occluded by any other element. Rendering contract: one glyph, always visible during `PlayerTurn` and `EnemyTurn`. |
| **Target-Slot Label** | 20 | Monospace caps under glyph: "WEAPON" / "ENGINE" / "MOBILITY" / "FRAME" / "SELF" / "—". | Updates atomically with glyph on retarget. "—" for NoOp. | Single short word — scannable without fixation. |
| **Predicted-Value Readout (F-CC5 contract)** | 21 | Integer damage / stacks / armor-delta, adjacent to label. **F-CC5 format**: when position bonus is INACTIVE, renders the bare integer (e.g., `11`). When position bonus is ACTIVE, renders as `<base> + <bonus>` (e.g., `11 + 4`) where the `+ <bonus>` clause is drawn in an accent tone so the player reads the bonus separately. | On `PositionChanged` or intent retarget, the format flips between bare and bonus-clause at the same moment the glyph updates. | **Critical**: the bonus clause renders ONLY when the bonus is currently active. Do not show "11 + 0" when inactive — just show `11`. This is the prototype's "math made visible" contract. |
| **FIZZLE Badge** | 22 | When enemy Weapon is BROKEN AND intent ∈ {Attack, Multi, DebuffAttack}, a FIZZLE badge overlays the Intent Zone — the Predicted-Value readout dims + a "[FIZZLE]" label renders in the accent red/grey per Art. | Appears the frame enemy Weapon enters Offline and the next intent telegraphs; disappears on Weapon repair + intent reselection. | **Prior-frame flip** (tenet 4): the badge must render BEFORE the intent resolves, so the player reads "this is neutralised" before the damage would land. |
| **Single-Hit Modifier Badge** | 23 | When enemy Wheels is BROKEN AND intent.Type == Multi, a "[SINGLE-HIT]" badge overlays the Multi glyph. Predicted-Value recomputes to per-hit damage (MULTI 5×3 → renders as `5` with SINGLE-HIT badge). | Same as FIZZLE: prior-frame flip on Wheels break, revert on repair. | The damage shown is the effective single-hit value, not the pre-break multi total. Read: "this hits once for 5" not "this hits for 15." |
| **Plate-Fizzle Badge** | 24 | When enemy Engine is BROKEN AND intent.Type == Plate, a "[FIZZLE]" badge overlays the Plate glyph. Armor-gain readout shows `0` instead of the predicted gain. Enrage rim simultaneously drops if active (un-enrage). | Same prior-frame flip contract. | Compound visual with Enrage Rim — two elements change state on the same trigger (enemy Engine → Offline). Must animate in sync. |
| **Synthetic RepositionIntent Glyph** | 25 | Distinct reposition icon (chevron pair / swap arrow — per Art, NOT an attack glyph). Target-Slot Label renders "—" or "SELF." Predicted-Value shows `Position: Ahead` or `Position: Behind` (the target position the enemy will move to). | Appears when Card Combat R18 fires synthetic RepositionIntent; renders like any other intent in the Zone. | The glyph must be visually distinguishable from Attack glyphs — a repositioning enemy is "resetting their board state," not "committing violence." |

### Zone 5 — Chase Rail Background

Handled in § Layout Zones — no additional per-element spec. Parallax differential is an art + technical direction brief (tunable scroll speed factor), not a per-turn behavioral element.

### Zone 6 — Hand Zone

| Element | Ch | Default State | State Variations | Notes |
|---------|----|---------------|------------------|-------|
| **Card (Hand Card Face)** | 5 | Full card face with art, name, energy cost (top-left), cost color band, family tag, damage/effect numbers inline with description. Fanned layout across bottom-center 60% of screen. | Hovered: raises 40px, enlarges 110%, brightness +10%. Selected (targeting mode): persistent raised + rim-glow. Dimmed (unplayable, ch10): grey-scale overlay + lock icon if position-locked. Animating: draw / discard / exhaust / play paths per Card System. | Hover and select are mutually exclusive. Only one card selected at a time. |
| **Playable Dim** | 10 | Grey-scale + 40% opacity overlay on cards where `EnergyCost > CurrentEnergy` or `PositionRequirement` fails. For position-locked cards, additional lock-glyph icon overlay in the card's lower corner. | Dim fades in/out ≤150ms on energy-change or position-change events. | The dim state is DATA-DRIVEN — not animated. Player's next energy-paying card play updates all other cards' dim state in the same frame. |
| **Keyword-State Indicator** | 9 | Per-card runtime indicator for Retain / Ethereal / Innate / Exhaust keywords per Card System UI. Visual treatment per keyword (Retain: persistent rim; Ethereal: opacity pulse; Innate: entry animation; Exhaust: dissolve exit on play). | Retain rim appears on end-of-turn phase start. Ethereal opacity pulse begins on end-of-turn phase start. Innate entry animation fires only on combat Setup. Exhaust dissolve fires on play. | Art-direction ownership for final visual treatment per Card System UI; UX spec requires behavioral distinctness. |
| **Card Tooltip** | 8 | On hover (300ms dwell) or gamepad select, tooltip panel appears adjacent to the card (above for bottom-row cards, below for top if screen space allows). Contains `DisplayName`, `EnergyCost`, `Family`, `Rarity`, full `Description` with resolved tokens, `FlavorText` (if any), keyword badges with one-line glossary. | Fades in ≤150ms, persists while hover/focus holds, fades out ≤100ms on dismiss. | Mouse hover-delay is 300ms to prevent flicker on fan-scan. Gamepad select = immediate tooltip (no dwell). |
| **Card-Hover Damage Preview** | 36 | When hovering an attack card, a floating preview renders adjacent to the Intent Zone (NOT occluding it) showing the post-resolution damage breakdown: `DamageEffect.Amount + CorrodeBonus (if target has Corroded) − Plating − Armor = {final damage}`. Updates on re-hover to different target slot if card supports multiple targets. | Appears ≤200ms after card hover, disappears on card deselect. | Per Status Effects UI card-hover contract. This is the player's "commit check" before spending energy — must be accurate to the Damage Pipeline (F-CC1). |

### Zone 7 — Energy + Deck Counters

| Element | Ch | Default State | State Variations | Notes |
|---------|----|---------------|------------------|-------|
| **Energy Pool** | 7 | Large "N / M" typography (current / max energy). Decrements on card play; resets to max on `PlayerTurnStarted`. | Flash on increase (draw event granting energy). Red tint briefly on attempted over-spend (AC-CC5 rejection). | Largest element in this zone — Energy is the scarcity knob. |
| **Deck / Discard / Exhausted Counters** | 6 | Three small counters stacked vertically or horizontal row (Art-discretion for exact layout). Each displays integer count of cards in zone. | Click / button-press opens corresponding list view (deck browseable per Card System UI). Counts update on draw / discard / exhaust events. | Ambient channel. |

### Zone 8 — End Turn + Save Indicator

| Element | Ch | Default State | State Variations | Notes |
|---------|----|---------------|------------------|-------|
| **End Turn Button** | 4 | Large button, bottom-right corner of zone. Label "END TURN." Enabled iff `Phase == PlayerTurn`. | Disabled: greyed + inert, tooltip "Enemy resolving..." Hovered: highlight. Pressed: ≤100ms depress animation + transitions to `PlayerResolve`. | Always visible. Size: must be clickable without aim (large button). Accessibility: keyboard shortcut (Space or Enter) + gamepad Start button mapping. |
| **Save-in-Progress Indicator** | 41 | Small icon (disk / spinner glyph per Art), top-right of the zone. Default: rendered at low opacity (~30%) when idle. On `ISaveService.IsCommitInProgress == true`, icon pulses at full opacity. On commit complete, fades back to 30%. | Cosmetic — does not gate input. Save cannot interrupt play. | Per Save & Persistence R2. MUST NOT render an intrusive modal or block any combat input. Indicator exists only for diagnostic-feel. |

### Zone 9 — Overlay Layer

| Element | Ch | Default State | State Variations | Notes |
|---------|----|---------------|------------------|-------|
| **Damage-Number Flyout** | 38 | When `DamageApplied` fires, an integer flies out from the source (card play origin or enemy intent glyph) to the target slot. Arcs ≤500ms, fades on arrival. Color-coded: yellow = normal damage, red = subsystem-slot target, blue = armor absorbed. | Multiple flyouts can render simultaneously (card with multi-hit or AoE). | Per Card Combat UI §Readability: "every damage number must animate from source to target slot; no bulk HP tick-downs." |
| **Slot Destruction Animation** | 39 | When any slot transitions to `Offline`, a destruction burst expands from the affected slot (800ms — particles + silhouette flash + brief camera shake). The slot's ALIVE → BROKEN badge flip happens at the burst peak. | Destruction of Frame triggers combat-end cascade (longer animation, ≤2s, per Vehicle & Part §Death Cascade). | Non-interrupting — player can still read HUD during animation. Camera shake is subtle (≤3px max displacement). |
| **Position Swap Animation** | 40 | Per § Layout Zones position rendering contract: glyph flip + chase-rail parallax reversal + wasteland motion burst, ≤500ms total. | Triggered only by `PositionChanged`. Cannot interrupt or be skipped. | Does not gate input — player can queue next card during animation. |
| **Screen-Reader Announce Stream** | 42 | Non-visual element. Receives a stream of structured announcements: turn transitions ("Turn 5 begins, player turn"), status tick/expire ("Burning on enemy Engine: 3 stacks, 2 turns remaining"), intent changes ("Enemy telegraphs attack on player Frame, 11 damage"), slot destruction ("Player Wheels destroyed"). | Announcements prioritize decision-critical events; ambient events (Phase Highlight) not announced. | Per § Accessibility. Speak rate and verbosity are player-settable (see that section). |

---

## Dynamic Behaviors

### Scope & reading protocol

This section specifies what the HUD *does*, not what it *looks like*. Each entry is a HUD event contract — a single game-state signal that triggers a bounded HUD response with a stated duration, affected channels, z-order, and interruption rule. Events cluster into four groups:

- **A. Phase transitions** (five transitions across the combat loop)
- **B. Combat-state cascades** (game-state changes with multi-channel HUD consequences — slot destruction, intent flips, enrage, position, combat end)
- **C. Feedback events** (visible per-action confirmations — damage, armor, status, card play)
- **D. Out-of-turn events** (save commits, Ambush cold-start)

Every event contract must satisfy all five HUD Philosophy tenets — especially tenet 4 (prior-frame flips) and tenet 5 (no reflex gates).

---

### Group A — Phase transitions

The HUD does not morph between phases (per Philosophy). Phase changes only re-highlight which *already-present* elements are currently interactive. All transitions are `≤200ms` cross-fades on the Phase Highlight channel (ch3) unless noted.

| Event | Trigger | Channels affected | Timing | Notes |
|-------|---------|-------------------|--------|-------|
| **Setup → PlayerTurn** | `CombatLoop.Phase` changes on enemy first-intent draw complete | ch3 (phase highlight shifts to "Player Turn"), ch4 (End Turn enables), ch5 hand cards become playable, ch7 energy pool fill animation (~300ms). Hand is dealt *during* the fade: card-deal choreography fires from deck marker to each hand slot, ≤500ms. | 300–500ms composite | Player input accepted the frame ch4 enables. Hand-deal animation does not gate input — player can play a card mid-deal on a fully-dealt card. |
| **PlayerTurn → PlayerResolve** | Player clicks End Turn (ch4) OR last card resolves after End-Turn lock | ch4 (End Turn disables + shows "Enemy resolving..." tooltip), ch5 hand becomes inert (non-playable dim, ch10), ch3 (phase highlight shifts to enemy side) | 200ms | Any in-flight damage flyouts complete before resolve proceeds. Cards in hand are not discarded yet — discard fires in EnemyTurn → PlayerTurn transition. |
| **PlayerResolve → EnemyTurn** | All player-initiated effects resolved; game yields to enemy | ch19–ch27 intent zone: intent glyph *executes* (animates from intent position toward target slot), damage flyout fires (ch38), HP bars tick. Intent glyph fades on completion. | 500–1200ms depending on intent complexity (multi-hit AoE takes longer) | Enemy intent can fizzle mid-execution if a player end-of-turn effect breaks the enemy's Weapon — fizzle badge MUST be already rendered from prior-frame (tenet 4); the animation plays the fizzle outcome (no damage) rather than re-evaluating. |
| **EnemyTurn → PlayerTurn (next turn)** | Enemy intent complete + tick phase complete | ch2 (turn counter increments), ch16 PlatingStacks counters decrement if expiring, ch32/ch33 status chips tick (Burning damage applies, duration decrements, expired statuses fade out ≤300ms), next intent drawn into ch19–ch21. **Intent draw is eager**: the new intent renders the frame the enemy turn ends, before the next player-turn phase-highlight completes. | 400–600ms composite (tick phase first, then next-intent draw, then phase highlight) | Screen-reader announces the full tick batch in one debounced line: "Turn 6 begins. Burning on enemy Engine: 3 damage applied, 2 turns remaining. Player turn." |
| **Any → Ended** | Either vehicle's Frame transitions to `Offline` | Slot-destruction cascade fires on Frame (see Group B), all other HUD elements freeze for the duration of the destruction animation (≤2s), then fade globally. End-state screen (post-combat) takes over. | ≤2000ms (Frame destruction) + 400ms global fade | No input accepted during fade. This is the one HUD transition that intentionally holds the screen — Frame destruction is the combat's narrative climax and does not share focus. |

---

### Group B — Combat-state cascades

These are the load-bearing behaviors. Each involves multiple channels flipping together with a defined order. Every cascade satisfies tenet 4 (prior-frame): if the cascade is triggered by a *known* upcoming event (e.g., an enemy intent is about to fire), the HUD already reflects the cascaded state before the event animates.

#### B1. SlotBroken cascade

**Trigger**: Any `VehicleState.Slots[s].DamageState` transitions `Functional → Offline`.

**Channels affected** (in resolution order):

1. **ch15** — slot's ALIVE/BROKEN badge flips to BROKEN. Prior-frame rule: the flip happens on the frame the breaking hit *lands*, not after the destruction animation finishes. The animation (ch39) plays against an already-BROKEN state.
2. **ch39** — slot destruction animation fires (800ms burst + 3px camera shake).
3. **Side-specific consequence** per Symmetric Consequence Matrix (Card Combat L1237–L1254):
   - **Enemy Weapon BROKEN** → all standing enemy intents of type `Attack | Multi | DebuffAttack` immediately flip ch22 (FIZZLE badge) ON. Prior-frame: the flip happens simultaneously with ch15.
   - **Enemy Engine BROKEN** → ch24 (Plate-Fizzle badge) flips ON for any `Plate` intent. Additionally, ch34 (Enrage Rim) drops if lit (Engine controls enrage pool access). Ch2 turn counter's "ENRAGE IN N" overlay clears. All three flip simultaneously.
   - **Enemy Wheels BROKEN** → ch23 (single-hit badge) flips ON for any `Multi` intent; ch25 synthetic RepositionIntent glyph blocks (no further reposition possible). Flip is simultaneous.
   - **Enemy Frame BROKEN** → combat-end cascade (Group A: `Any → Ended`).
   - **Player Weapon BROKEN** → every hand card (ch5) of type `Attack | Multi` dims (ch10 playable dim flips OFF).
   - **Player Engine BROKEN** → every hand card of type `Plate` dims.
   - **Player Wheels BROKEN** → every hand card with a `PositionRequirement` or `Reposition` effect dims; active PositionBonus clauses in F-CC5 previews are stripped from ch21.
   - **Player Frame BROKEN** → combat-end cascade.
4. **ch38** — damage flyout of the breaking hit continues to fly from source to the now-BROKEN slot.
5. **ch42** — screen-reader announces: "[side] [slot] destroyed. [consequence summary: 'enemy attacks now fizzle' / 'enemy can no longer enrage' / etc.]"

**Timing**: Step 1 and Step 3 happen on the same frame (prior-frame rule). Steps 2 and 4 share the 800ms destruction window.

**Interruption rule**: Non-interrupting. Player can still read the HUD, queue hover tooltips, and (if phase is PlayerTurn) play cards during the destruction animation.

**Invariant**: No HUD state-flip in this cascade can render *only after* the destruction animation completes. Every consequence badge is visible from frame 1 of the animation.

#### B2. IntentFlip (mid-phase intent change)

**Trigger**: The enemy's drawn intent mutates within the current turn. Two causes:

- **Retarget** — the enemy's current intent targets a slot that was destroyed mid-turn (by the player). The intent retargets to the next-priority living slot per Enemy System targeting rules.
- **Brain re-evaluation** — an enemy brain (Smart/Adaptive) may swap its intent in response to a player move. Enemy System D.N defines which brains do this; Reactive and Fixed brains never retarget.

**Channels affected**:

1. **ch19/ch20/ch21** — intent glyph, target label, and predicted-value readout cross-fade (≤250ms) to the new intent's values.
2. **ch22/ch23/ch24** — modifier badges re-evaluate against the new intent (FIZZLE may appear or disappear).
3. **ch42** — screen-reader announces the flip: "Enemy intent changed. Now telegraphing [new intent] on [new target] for [new value] damage."

**Timing**: 250ms cross-fade. Not prior-frame (the flip *is* the event) — but the player has at least one more player-turn decision window before the enemy acts, so no reflex gate is created.

**Interruption rule**: Non-interrupting. A flip mid-cross-fade (rare: two consecutive player effects both retargeting) snaps to the latest state.

**Invariant**: The predicted-value readout MUST reflect the re-targeted slot's armor and status state, not the previous target's.

#### B3. PositionChanged

**Trigger**: `PositionState.Position` changes for either side (both sides always swap symmetrically per Card Combat R15). Causes: a Reposition card resolves; a synthetic RepositionIntent resolves; a BonusIfAhead/BonusIfBehind effect that changes position.

**Channels affected**:

1. **ch11/ch12** — both position glyphs (Player + Enemy) flip their chevron direction simultaneously. 200ms glyph animation.
2. **Zone 1 / Zone 2 / Zone 3** chase-rail parallax reverses direction. Differential scroll speeds smoothly cross-zero over 500ms (decelerate → pause → accelerate opposite direction). No hard cut.
3. **ch40** — wasteland motion burst overlay (brief dust / tire-spray particles at the parallax reversal peak, ≤400ms).
4. **ch21** — any F-CC5 predicted-value readouts affected by PositionBonus recompute and re-render. Bonus clauses appear/disappear from the preview simultaneously with the glyph flip (prior-frame: before the reposition animation plays out).
5. **ch5/ch10** — hand cards with `PositionRequirement` re-evaluate playability dim.
6. **ch42** — screen-reader announces: "Position changed. Player now [Ahead | Behind]."

**Timing**: 500ms total swap window. ch21 re-render is instant at swap start (prior-frame).

**Interruption rule**: Cannot be interrupted. A second PositionChanged queued during the 500ms is held until the first completes, then plays.

**Invariant**: The two position glyphs MUST mirror each other every frame (if player is Ahead, enemy is Behind). The chase-rail parallax direction MUST match the player's facing (Ahead → rail scrolls left; Behind → rail scrolls right).

#### B4. EnrageTriggered

**Trigger**: Enemy HP crosses the enrage threshold (per Enemy System G.2, default 40% MaxHP) AND enemy Engine is `Functional` (if Engine is Offline, enrage is suppressed).

**Channels affected**:

1. **ch34** — Enrage Rim lights (2px red outline on enemy portrait). 400ms pulse to grab attention, then steady red.
2. **ch2** — "ENRAGE IN N" overlay on turn counter clears (replaced by steady "ENRAGED" chip).
3. **ch19/ch20/ch21** — the *next* intent draw pulls from the enraged intent pool (Card Combat R14 `EnrageIntentCandidates`). The enraged intent draws at the next EnemyTurn → PlayerTurn transition (Group A), not mid-turn.
4. **ch42** — screen-reader announces: "Enemy enraged. Next intent will be drawn from enraged pool."

**Timing**: 400ms pulse. Enraged-pool intent draw occurs on the next turn transition (not immediate).

**Interruption rule**: Non-interrupting.

**Invariant**: If enemy Engine breaks *while* enraged, ch34 Enrage Rim drops and ch2 "ENRAGED" chip clears immediately (per B1 Engine BROKEN consequence). The enemy re-draws from the non-enraged pool at next turn transition.

#### B5. CombatEnded

**Trigger**: Either Frame transitions to Offline (Group A `Any → Ended`).

**Channels affected**: All HUD channels freeze in their current state. Frame destruction animation plays (ch39, extended ≤2s for Frame specifically). On completion, global HUD fade to 0% opacity over 400ms. Post-combat flow screen takes over.

**Interruption rule**: No input accepted. This is the sole HUD behavior that gates input, and it is justified because there is no further decision to make.

**Invariant**: The last visible state of the HUD before fade MUST be readable (no mid-animation freeze on a moving element). Any in-flight cross-fade snaps to its end state before the global fade begins.

---

### Group C — Feedback events

These communicate per-action confirmation. All are non-interrupting and never gate input.

#### C1. DamageApplied

**Trigger**: Damage resolves against a target (either side, any source).

**Channels affected**:

1. **ch38** — flyout integer animates from source (card play origin for player cards, intent glyph for enemy intents, status-chip for tick damage) to target slot. 500ms arc, yellow = HP damage, blue = Armor-absorbed, red = subsystem-killing hit (the hit that crossed 0).
2. **ch13/ch14** — affected subsystem HP bar ticks down over 200ms (smooth interpolation, no snap).
3. **ch17/ch18** — affected armor bar ticks down first if armor > 0 (absorbed), then HP.
4. **If hit is the killing hit** — triggers B1 SlotBroken cascade on the same frame.
5. **ch42** — screen-reader announces batched within a turn-resolve window: "[N] damage to [side] [slot]. [N] damage absorbed by armor." Batched announcements fire at end of resolve burst, not per-hit (to avoid spam on multi-hit cards).

**Timing**: 500ms flyout + 200ms bar tick (overlapping).

**Invariant**: The flyout integer MUST match the exact number the F-CC5 preview (ch21) showed. If they differ, the preview math is wrong — escalate as a bug.

#### C2. ArmorRefilled / PlatingApplied

**Trigger**: `Plate` effect resolves, or Plating stack applied.

**Channels affected**:

1. **ch17/ch18** — armor bar extends smoothly (200ms) up to new value, clamped at MaxArmor cap-line. If amount would exceed cap, the overflow visibly "bounces off" the cap-line (short 150ms visual — shows the player that over-cap Plate is wasted, per the "Turn-1 Plate wasted" open design question carried forward from the prototype).
2. **ch16** — PlatingStacks counter flashes the new stack count (150ms scale-up then settle).
3. **ch32/ch33** — Plating status chip appears/updates.
4. **ch42** — screen-reader announces: "[side] plated, [N] armor. [Capped at {MaxArmor}]."

**Timing**: 200ms bar + 150ms counter (parallel).

**Invariant**: The cap-line on ch17/ch18 MUST remain visible at all times — it is load-bearing for the "Plate now or save" decision.

#### C3. StatusApplied / StatusTicked / StatusExpired

**Trigger**: Status-effect system events.

**Channels affected**:

- **Applied** — ch32/ch33 status chip fades in (300ms). Stacks indicator renders. Screen-reader announces.
- **Ticked** — chip's per-tick animation (subtle pulse, ≤150ms). Any damage applies via C1 DamageApplied.
- **Expired** — chip fades out (300ms). If Corroded expires on a slot, ch37 Corroded Slot Indicator clears.

**Timing**: 150–300ms per sub-event.

**Invariant**: A status chip's stack count and remaining duration MUST be readable without hover (the hover tooltip only elaborates the rules text).

#### C4. CardPlayed

**Trigger**: Player plays a card (ch5).

**Channels affected**:

1. **ch5** — card animates out of hand toward the target (300ms arc). Hand reflows to close the gap (200ms slide).
2. **ch7** — energy pool ticks down by the card's cost (100ms).
3. **ch6** — discard pile counter increments (or exhaust counter, if `Exhaust` keyword).
4. Card effect resolves — may trigger C1, C2, C3, and any B-group cascades.
5. **ch42** — screen-reader announces: "Played [card name]. [N] energy remaining." Effect announcements batched separately.

**Timing**: 300ms arc + 200ms reflow.

**Invariant**: Hand reflow must complete before the next card-hover event fires, to prevent hover targeting the wrong card.

---

### Group D — Out-of-turn events

#### D1. SaveCommit

**Trigger**: `ISaveService.IsCommitInProgress` transitions `false → true`.

**Channels affected**: Only ch41 (Save-in-Progress Indicator). Pulses at full opacity for the commit duration, fades back to 30% on completion. No other channel affected.

**Timing**: Indicator state follows the commit duration (target ≤100ms per Save ADR). Minimum visible pulse is 200ms even if commit is faster, so the player reads it as intentional.

**Interruption rule**: None — save cannot interrupt combat or gate input.

#### D2. AmbushColdStart

**Trigger**: Combat opens with `CombatSetup.EncounterType == Ambush`.

**Channels affected**:

1. **Frame 1 of Setup** — ch1 EncounterType tag renders with Ambush urgency tint and ≤600ms pulse. The tag is visible BEFORE the enemy's Setup intent resolves (per prototype carry-forward #1).
2. **ch11/ch12** — position indicators render the Ambush default (player Behind, enemy Ahead) from frame 1.
3. **ch19/ch20/ch21** — the enemy's *first* intent is the Ambush Setup intent and renders immediately on frame 1 (already-drawn state, not an animated draw). Predicted-value readout is fully populated.
4. **Standard Setup → PlayerTurn transition** (Group A) fires after frame 1 reads are complete, not before.
5. **ch42** — screen-reader announces at open: "Ambush encounter. Enemy acts first. Player behind. [Enemy intent summary]."

**Timing**: All frame-1 state is simultaneous. The pulse is cosmetic; no input gate.

**Invariant**: Standard encounters MUST NOT show the Ambush tag — ch1 is an explicit binary signal. Boss-node encounters are locked to `Standard` per Node Map validator and will never trigger AmbushColdStart.

---

### Timing budget summary (single source of truth)

| Event | Budget |
|-------|--------|
| Intent flip (FIZZLE / single-hit / Plate-fizzle / retarget) | Prior-frame (0ms) or ≤250ms cross-fade |
| Position swap (full sequence) | 500ms |
| Slot destruction (non-Frame) | 800ms |
| Slot destruction (Frame / combat-end) | ≤2000ms |
| Damage flyout | 500ms |
| HP / Armor bar tick | 200ms |
| Plate overflow bounce | 150ms |
| Status chip fade (apply/expire) | 300ms |
| Enrage Rim pulse | 400ms |
| EncounterType tag pulse | ≤600ms |
| Phase highlight cross-fade | 200ms |
| Hand deal | ≤500ms |
| Card play arc | 300ms |
| Hand reflow | 200ms |
| Energy tick | 100ms |
| Save indicator pulse (min) | 200ms |
| Combat-end global fade | 400ms |

**Rule**: No element's animation duration may exceed its row. Art Director may specify shorter values; only lengthening requires UX re-review.

---

## Platform & Input Variants

### Target

- **Platform**: PC (Steam) only — no console, no mobile.
- **Input Methods**: Keyboard/Mouse (primary), Gamepad (partial — additive, never gating).
- **Touch**: Not supported. No touch-specific rendering path.
- **Reference resolution**: 1920×1080 (16:9).
- **Minimum supported**: 1280×720 (16:9). Lower resolutions are not a supported configuration.
- **Ultrawide**: 21:9 (2560×1080 / 3440×1440) and 32:9 (5120×1440) are common on Steam PC and MUST render cleanly — see Scaling subsection below.

### Input Philosophy (binding contract)

1. **KBM is source of truth.** Every HUD interaction is designed against keyboard + mouse first.
2. **Gamepad is a mirrored alternate.** Every KBM interaction has a gamepad equivalent. If a feature can only be triggered via mouse hover or mouse drag, the design is broken and must be revised — gamepad cannot be a second-class path.
3. **No hover-gated state.** Since gamepad has no hover, any state that would be communicated by "hover to reveal" is forbidden. Tooltips are hover-assisted on KBM and focus-assisted on gamepad — they only *elaborate* already-visible state (per Philosophy tenet 1).
4. **Every interactive element has keyboard focus support.** Focus is visible (2px outline + subtle scale-up, per Art Director's focus style). Tab order is deterministic and matches the gamepad navigation groups.
5. **No detection-dependent prompts.** The HUD does not change its icon set based on the last-used input (the "Steam Controller" auto-detect pattern is out of scope for MVP). Prompts render as descriptive text ("End Turn") with keybind glyphs rendered in the controls overlay only.

### Per-action input map

Every HUD action, its KBM binding, its gamepad binding, and its interaction contract.

| # | HUD Action | Element (§4) | KBM Binding | Gamepad Binding | Notes |
|---|------------|-------------|-------------|-----------------|-------|
| 1 | Hover a hand card | Zone 6 Card Face | Mouse over card | D-pad ←/→ to move focus (within Hand group) | On KBM: tooltip renders after 300ms hover dwell. On gamepad: tooltip renders immediately on focus. |
| 2 | Select a hand card for play | Zone 6 Card Face | Left-click once | A (south face button) | If card is non-targetable → plays immediately. If targetable → enters Target-Selection mode. |
| 3 | Cancel card targeting | Zone 6 (during target-select) | Right-click OR Esc | B (east face button) | Restores focus to the card, energy not consumed. |
| 4 | Target a slot (non-hand, during targeting) | Zone 2/3 Subsystem Portrait | Left-click on slot | A, with D-pad ←/→/↑/↓ to move targeting cursor among legal slots | Only legal targets are focusable (illegal targets render dimmed and skipped by focus cursor). |
| 5 | Hover enemy Intent glyph | Zone 4 Intent Glyph | Mouse over glyph | LB (Left Bumper) to enter Intent group, focus lands on glyph | Renders extended intent tooltip: source part, base + bonus decomposition, target slot highlight. |
| 6 | Hover a slot to see status detail | Zone 2/3 | Mouse over slot | Focus on slot (within Chassis group — see nav model) | Renders full status-chip elaboration (rules text of every active status on that slot). |
| 7 | Hover an individual status chip | Zone 2/3 Status Chip Row | Mouse over chip | Hold X while focused on the slot, D-pad ←/→ to select chip | Tooltip elaborates a single chip's rules (e.g., exact Burning damage formula). |
| 8 | Press End Turn | Zone 8 | Click End Turn OR Space OR Enter | Start button | Space/Enter is the keyboard shortcut. Start is used instead of Y because Y is reserved for other platforms' pause convention — see row 13. |
| 9 | Focus End Turn (quick-hop) | Zone 8 | Tab until End Turn focused | Y (north face button) | Quick-jump focus; does not activate. Second Y press (or A) activates. |
| 10 | Read current combat log / screen-reader verbosity | Settings overlay | F1 | Select / View button | Opens the Accessibility overlay (§7 Accessibility). |
| 11 | Cycle HUD focus group forward | All Zones | Tab | RB (Right Bumper) | Groups: Hand → Chassis (player portrait) → Chassis (enemy portrait) → Intent → End Turn → back to Hand. |
| 12 | Cycle HUD focus group backward | All Zones | Shift+Tab | LB (Left Bumper) | Reverse of row 11. |
| 13 | Pause menu | Overlay | Esc (when not targeting) | — (no binding — Esc equivalent is not mapped on gamepad since Start is End Turn) | On gamepad: player must use a dedicated unbound path — see Open Questions §8 "Pause binding on gamepad". |
| 14 | Dismiss tooltip | Any | Mouse off element | Release X (tooltip is hold-to-show on gamepad) | On KBM, tooltip dismisses on mouse-out. On gamepad, tooltip auto-shows on focus and additionally elaborates on X-hold. |
| 15 | Skip animation (quality-of-life) | Overlay Layer | Left-click OR Space during animation | A during animation | Skips any currently-playing ch38/ch39/ch40 animation to end-state. Does not skip phase transitions (Group A). Does not gate input (animations were already non-gating). |

**Invariants**:

- Every row above MUST have both a KBM and a gamepad binding (except row 13, which is an open question). No row may be "mouse only" or "keyboard only".
- Mouse dragging is not used anywhere. Card play is click-to-select, not drag-to-play (drag is harder to mirror on gamepad and is a common source of mistouches on KBM).
- No action requires both hands simultaneously on gamepad (no LB+RB combos). This is a baseline accessibility requirement — see §7.

### Gamepad navigation model

Focus is a single cursor that lives within one *group* at a time. Groups are large regions of the HUD. Within a group, D-pad moves focus; RB/LB switches group.

**Group list (cycle order)**:

1. **Hand** (default on PlayerTurn start) — D-pad ←/→ across cards in Zone 6. D-pad ↑ raises the focused card (equivalent of KBM hover raise) and reveals the tooltip.
2. **Chassis — Player** — D-pad selects among 4 subsystem slots in Zone 2. Focus outline wraps the focused slot.
3. **Chassis — Enemy** — D-pad selects among 4 subsystem slots in Zone 3.
4. **Intent** — D-pad has no travel (single element) but the group exists so the player can explicitly read the intent without mousing.
5. **End Turn** — D-pad has no travel. A button (or Start) activates.

**Group-cycling invariants**:

- RB always moves forward through the list. LB always moves backward. The cycle is circular (End Turn → Hand).
- The focus cursor remembers the last-focused element per group. When the player RBs out of Hand with card index 3 focused, then RBs back in later, focus returns to card index 3 (not the leftmost card).
- When the phase changes to PlayerResolve, the focus cursor is withheld (no focus outline visible) until phase returns to PlayerTurn. Hand group is re-selected on return.
- During Target-Selection mode (triggered by row 2 when the selected card is targetable), D-pad moves *across legal slot targets* regardless of group — the targeting mode temporarily overrides group cycling until A is pressed (confirm) or B is pressed (cancel).

### KBM-specific conventions

- **Tooltip dwell**: 300ms from mouse-enter to tooltip visible.
- **Drag threshold**: None used (no drag interactions).
- **Right-click**: Used only to cancel (row 3). Never used as a secondary action menu.
- **Scroll wheel**: Not bound. Reserved for future card-queue scrolling if hand exceeds visible width (out of scope for MVP).
- **Keyboard shortcuts for hand cards**: Number keys `1–9` select hand card by index and trigger play (card must be in focused Hand group on gamepad; on KBM, number keys work regardless of focus state). This is a power-user shortcut documented in the controls overlay.

### Gamepad-specific conventions

- **Deadzone**: Not applicable — no analog-stick input. D-pad only.
- **Vibration**: Optional and coarse. Enabled on: slot destruction (strong, 200ms), enrage trigger (medium, 400ms), player Frame near-death (<20% HP, subtle continuous on next PlayerTurn). Disableable via Accessibility overlay. Zero vibration on routine damage — vibration is reserved for threshold events only.
- **Face-button convention**: A = confirm / play, B = cancel, X = hold-to-elaborate (tooltip), Y = quick-jump focus. Matches Xbox controller convention, Steam Input handles PlayStation/Switch Pro remapping.

### Resolution & aspect-ratio scaling

Reference: 1920×1080 (16:9). All §3 Layout Zones coordinates are in this space.

#### Uniform 16:9 scaling (720p, 1080p, 1440p, 4K)

- All zones scale uniformly. Coordinates are defined in *normalized* 16:9 space and multiplied by the render resolution.
- Text is vector-rendered (TMPro SDF). Minimum point-equivalent size at 720p: 14pt. At 1080p: 16pt. At 1440p+: scales linearly but never above the authored size at 4K (i.e. stops growing past 200% authored).
- **Minimum supported resolution: 1280×720.** Below this, the HUD does not reflow — this is an explicitly-unsupported configuration.

#### 16:10 scaling (1920×1200 and similar)

- Zones are authored against 16:9 safe rectangle. On 16:10, the HUD extends zones vertically within the extra 120px by expanding the Chase-Rail background (Zone 1 extends upward, Zones 2/3 gain slight vertical breathing room). **No new HUD elements** appear in the extra space — it is ambient background extension only.

#### Ultrawide scaling (21:9, 32:9)

- The HUD renders against a **centered 16:9 safe rectangle**. Zones do not stretch horizontally.
- The remaining left/right letterbox is filled with the Chase-Rail background (Zone 1's wasteland parallax extends outward). This maintains Chassis Identity — the vehicles don't drift apart on ultrawide, which would break the chase-rail reading.
- HUD elements are never placed outside the 16:9 safe rectangle on ultrawide. This is a hard rule: a 32:9 player must see the *identical* combat HUD geometry as a 1920×1080 player.
- The background parallax extension on letterbox is *not* interactive and does not carry any HUD information.

#### DPI and text scaling

- The HUD respects the OS DPI scale (Windows 100%/125%/150%). At 125% DPI, all text renders at 125% of the authored size; at 150%, 150%. This can push some text beyond its layout box on 720p — documented as an edge case in §7 Accessibility.
- Independent of OS DPI, an in-game **HUD Scale slider** (85% / 100% / 115% / 130%) is available in Accessibility overlay (see §7). This scales only HUD elements, not the chase-rail background.

#### Orientation

- Landscape only. Portrait orientation is unsupported (PC Steam context; no portrait-oriented monitors targeted).

### Out of scope for §6

- Console-specific controller flows (Steam Deck gamepad is covered by the above gamepad spec since it is a PC Steam environment).
- Localized keybind prompts (English-only MVP; localization is deferred to a later spec).
- Custom keybind remapping UI — the *engine*-level remap support must exist (Unity Input System) but the settings screen is a separate UX spec.

---

## Accessibility

### Commitment: Reading-Surface Parity

Every decision-critical state in the Combat HUD must be available via **at least three independent channels**:

1. **Visual shape / position** — the element's geometry conveys the state (not color alone).
2. **Visual label / number** — a text readout or symbol conveys the state.
3. **Screen-reader announcement** — the state is in the Announce Stream (ch42) at a verbosity level the player has selected.

This is the floor. No decision-critical read may drop below three channels. Ambient reads (phase highlight, turn counter) may drop to two, but never one.

The commitment grounds in Philosophy:
- Tenet 1 (Reads before inputs) is an accessibility rule in disguise: the math is already visible, so nothing depends on a cognitive sprint at action time.
- Tenet 5 (No reflex gates) eliminates the single largest accessibility risk in action games. This commitment is already enforced by design, not added on top.

The cross-referenced **OQ-NE12 accessibility gate** (pre-1.0 `accessibility-specialist` review of the 3-channel HostileTiltDelta tell) is the project's precedent for 3-channel redundancy. The Combat HUD applies the same rule to every decision-critical channel.

### Color Independence

No HUD channel encodes state solely via color. Every color-encoded state has a redundant **shape / position / label** cue.

**Required color-independent distinctions** (Art Director owns the palette; this spec owns the requirements):

| State distinction | Color cue | Redundant non-color cue |
|-------------------|-----------|--------------------------|
| Subsystem `ALIVE` vs `BROKEN` (ch15) | Color shift on portrait | ALIVE/BROKEN text badge + portrait art swap to DamageState silhouette |
| Intent `will deal damage` vs `will fizzle` (ch22) | Glyph tint shift | FIZZLE text badge overlay on the intent glyph |
| Intent `Multi` vs `single-hit-modified` (ch23) | Tint + pip count | SINGLE HIT text badge + reduction of visible pip count (3 pips → 1 pip) |
| Plate intent `will apply` vs `will fizzle` (ch24) | Tint | PLATE-FIZZLE text badge |
| Damage type: HP / Armor-absorbed / Killing hit (ch38) | Flyout color (yellow / blue / red) | Flyout glyph shape differs: plain integer / shield overlay / burst-frame integer |
| Position `Ahead` vs `Behind` (ch11/ch12) | Glyph color | Chevron direction (← or →) + chase-rail parallax direction |
| Enrage active vs inactive (ch34) | Red rim | "ENRAGED" text chip + turn-counter overlay cleared |
| Playable vs unplayable card (ch10) | Dim (reduced saturation) | 30% opacity reduction + lock icon overlay + cursor-reject state on hover |
| Corroded slot (ch37) | Slot tint | Corrosion art decal on the slot portrait + Corroded chip in status row |
| Card keyword state (ch9) | Chip tint | Keyword icon + text label on the card frame |

**Palette requirements** (Art Director delivers):
- Protanopia, Deuteranopia, and Tritanopia simulations must each preserve all state distinctions above.
- WCAG 2.1 AA contrast minimum (4.5:1 for body text, 3:1 for UI components and graphical objects) against all HUD backgrounds including the chase-rail parallax (which is itself a dynamic background — contrast must hold across its full palette range).
- A **High-Contrast toggle** (Accessibility overlay) replaces palette with a high-contrast set that meets WCAG AAA (7:1 body, 4.5:1 graphical) at the cost of the chase-rail atmospheric look.

**Verification**: Art Director must produce color-blindness simulation proofs for every decision-critical state. UX re-reviews when proofs land.

### Keyboard-Only Path

Every interaction in §6 has a keyboard binding (mouse is never required). The Combat HUD is fully playable with keyboard alone.

**Additional keyboard-specific rules**:
- **Focus is always visible** while using keyboard input. If the player uses the mouse, the focus outline is suppressed to prevent visual clutter. On next Tab / Shift+Tab / number-key press, focus outline returns.
- **Focus trap during targeting**: during Target-Selection mode, focus cannot leave the set of legal target slots (Tab cycles only among legal targets, not through the whole HUD).
- **No keyboard chord interactions**: no binding requires holding two keys simultaneously (consistent with the no-LB+RB-combo rule for gamepad).

### Screen Reader Support

The **Announce Stream** (ch42) is the screen-reader surface. It emits structured announcements to the OS screen reader via standard accessibility APIs (Unity Input System + the UGUI Accessibility package, or equivalent).

**Verbosity tiers** (player-selectable in Accessibility overlay):

| Tier | What is announced |
|------|-------------------|
| **Off** | No announcements. HUD is played visually only. |
| **Minimal** | Phase transitions, combat start / end, slot destruction, position change. ~1 announcement per turn. |
| **Standard** (default on first run if system screen reader is detected) | Everything in Minimal + intent changes, damage applied (batched per resolve), status applied / expired, enrage. ~3–6 announcements per turn. |
| **Verbose** | Everything in Standard + every single card play, every tick, every focus change. Suited for screen-reader-primary play. ~15–25 announcements per turn. |

**Announcement contract**:
- Announcements use consistent grammar: `[Side] [Subsystem]: [event] [detail]`. Examples:
  - "Player Weapon: 8 damage taken. HP 17 of 25."
  - "Enemy Engine: destroyed. Enemy can no longer enrage."
  - "Enemy intent: attack, Player Frame, 11 damage including position bonus."
- Numeric values are always spoken in absolute terms, never in ambiguous qualifiers ("high" / "low"). The screen-reader player reads the same number the visual player reads.
- Announcements batch within a 300ms window to prevent TTS overlap. A multi-hit card announces once per target, not once per hit.
- The **screen-reader player can request an on-demand HUD read** via a dedicated keybind (default: `R`, rebindable). This reads the complete current HUD state in a fixed order (phase → turn → position → enemy intent → enemy subsystems → player subsystems → energy → hand).

**Focus echo**: When focus moves (via Tab or D-pad), the focused element auto-announces its full state at Standard or Verbose verbosity. At Minimal verbosity, focus moves are silent and the player must press the on-demand read key.

### Text & UI Scaling

**HUD Scale slider** (Accessibility overlay): 85% / 100% / 115% / 130% / 150%. Affects only HUD elements (§3 Zones). Does not affect chase-rail background.

- 85% is for small-monitor players who want more parallax visible.
- 130% and 150% are for readability. At 150% on 1080p, some zone boundaries shift to maintain non-overlap (the density costs predicted in §1 Philosophy bite hardest here).
- Text renders at its scaled size; layout boxes grow to fit. If scaled text would clip, the layout box expands (never truncates).

**OS DPI scale** (100% / 125% / 150%) multiplies on top of HUD Scale. A player at 150% OS DPI + 130% HUD Scale sees text at 195% of authored size — the layout spec must not break at this combined scale. If it does, the combat-hud spec is incomplete.

**Minimum body text size** at 100% HUD Scale / 100% DPI: 16pt at 1080p. Status-chip text and the smallest secondary readouts are at this floor. No smaller text exists anywhere on the HUD.

### Contrast

- WCAG 2.1 AA: 4.5:1 body / 3:1 graphical. This is the MVP floor.
- **High-Contrast toggle** provides AAA (7:1 / 4.5:1) at the cost of chase-rail atmosphere.
- The cap-line on armor bars (ch17/ch18) — a load-bearing read for the Plate-now-or-save decision — MUST be rendered at AAA contrast regardless of high-contrast toggle state. It is the single element that cannot afford ambiguity.

### Motion Sensitivity

A **Reduced Motion toggle** (Accessibility overlay, on by default if the OS reports `prefers-reduced-motion`) substitutes all motion with reduced variants:

| Default animation | Reduced-motion variant |
|-------------------|------------------------|
| Chase-rail parallax (continuous scroll) | Static background with subtle drift (~10% speed) |
| Position swap parallax reversal (500ms cross-zero) | Instant direction reversal (0ms), chevron flips hard |
| Slot destruction burst (800ms + 3px camera shake) | Flash + BROKEN badge appears (no shake, no particle burst) |
| Damage flyout arc (500ms) | Straight-line flyout, 200ms |
| Hand reflow after card play (200ms slide) | Instant (0ms) |
| Phase highlight cross-fade (200ms) | Instant highlight shift |
| Card play arc (300ms) | Instant consume |
| Enrage Rim pulse (400ms) | No pulse — steady rim only |

**Zero camera shake** under Reduced Motion. Zero rotational or rapid-scale motion. Flashing above 3 Hz is never used anywhere in the HUD even without Reduced Motion — this is a seizure-safety floor.

### Audio Accessibility

- **Captions**: All decision-relevant SFX (intent resolve "tell," slot destruction, enrage stinger, card play) have optional on-screen text captions (Accessibility overlay → Captions toggle). Captions appear in a dedicated strip between Zones 1 and 4 (top-center, below EncounterType tag).
- **No audio-only channel carries decision-critical information.** Every SFX has a redundant visual. A player with audio muted can win any combat the audio-playing player can win.
- **Low-frequency hearing loss accommodation**: the "tell" audio cues (intent resolve, enrage) include both a low-frequency thump AND a mid-high-frequency tonal element (per audio-director palette, TBD in Audio spec). This mirrors the OQ-NE12 HostileTiltDelta 3-channel approach.

### Vibration (Gamepad Only)

Disableable via Accessibility overlay. See §6 for the full vibration spec (intentionally coarse: three threshold events, not routine damage).

### Timing & Pacing

- **No reflex gates anywhere.** (Philosophy tenet 5, inherited.) No action has a time limit.
- The only non-instantaneous HUD behaviors are animations (§5), all of which are either non-gating (player can read through them) or occur during enemy resolve (player not acting).
- **No auto-advance** of any menu or tooltip. All dismissals are explicit.

### Input Remapping

Unity Input System supports rebinding at the engine level. The Accessibility overlay exposes:
- Rebind every keyboard key used by the Combat HUD.
- Rebind every gamepad button used by the Combat HUD (face buttons, shoulders, Start, Select).
- Swap-sticks option (reserved for future if analog stick use is added — out of scope for MVP's D-pad-only gamepad).
- Toggle hold vs. press for any "hold-to-X" binding (e.g., the X-hold-for-tooltip mapping in §6 row 14 can become a press-to-toggle).

### Cognitive Load Accommodations

- **HUD Hints toggle** (Accessibility overlay, default on during first 3 runs): renders subtle reminder tooltips on first-encountered elements (first EncounterType tag, first BROKEN badge, first FIZZLE, first enrage). Tooltips are dismissable and do not re-appear on the same run.
- **Full combat log overlay** (bound to L in KBM default, Back button on gamepad default — rebindable): displays a scrollable log of the last 20 combat events in plain text. The player can pause decision-making to re-read what happened. Does not gate input (per Philosophy).
- **No uninterruptible tutorials inside combat.** Any tutorial interjection that fires in combat must be dismissable with one keypress / button press and must not gate the End Turn button.

### Accessibility Overlay (F1 / Select-View)

The accessibility overlay is a modal settings panel surfaced by `F1` (KBM) or the Select/View button (gamepad). Non-gating on combat — opens as an overlay with a backdrop, does NOT pause-cascade the combat state (since combat is turn-based, there is nothing to pause). The player can adjust settings mid-combat and close to return to the same HUD state.

Settings grouped:
1. **Vision** — HUD Scale, High-Contrast toggle, Reduced Motion toggle
2. **Audio** — Master volume (inherits from general settings), Captions toggle
3. **Screen Reader** — Verbosity tier, On-Demand Read keybind, Focus-Echo toggle
4. **Input** — Full remap, Vibration toggle, Hold/Press override
5. **Cognitive** — HUD Hints toggle, Combat Log keybind, HUD Scale preview

### Pre-1.0 Accessibility Gate

This section is authored pre-implementation. Before any 1.0 release:

1. `accessibility-specialist` agent reviews the final implemented Combat HUD against this spec.
2. External playtesting with at least one player per disability category (color-vision, low-vision, motor, hearing, cognitive, screen-reader primary) is required. Feedback is a hard input to the 1.0 gate.
3. The OQ-NE12 HostileTiltDelta accessibility verdict (already pending on the Node Encounter GDD) runs in the same review pass.

### Out of scope for §7

- Full palette hex values (owned by Art Director).
- Screen-reader TTS voice selection (inherits OS default; no in-game voice override).
- Pronunciation dictionary for proper nouns (chassis names, status names) — deferred to Localization pass.
- Assist modes that change combat rules (e.g., "invulnerable mode") — this is a design-rules question, not a HUD question; belongs in a separate Assist Modes design pass if one is scoped.

---

## Open Questions

### Scope

This section catalogs every unresolved question surfaced while authoring §1–§7. Each entry has an **ID**, a **question**, an **owner** (who resolves it), a **blocker level** (what this blocks if unanswered), and an optional **reference** to the section that surfaced it.

Blocker levels:
- **P0 — Implementation blocker**: must be resolved before any Unity prototype/implementation work on this HUD begins.
- **P1 — Pre-alpha**: must be resolved before combat-HUD code enters alpha builds.
- **P2 — Pre-1.0**: must be resolved before the 1.0 release gate.
- **P3 — Tracked but not release-blocking**: captured for continuity; can slip.

### Questions

| ID | Question | Section | Owner | Blocker | Notes |
|----|----------|---------|-------|---------|-------|
| **OQ-CH1** | What is the Ambush urgency tint exact hex + pulse curve? | §4 Zone 1 / §5 D2 | Art Director | P1 | The pulse is ≤600ms per §5 D2; the *color* is art-direction. Needed before Unity prototype can visually distinguish Ambush from Standard. |
| **OQ-CH2** | What is the visual form of the FIZZLE / SINGLE HIT / PLATE-FIZZLE badges? | §4 Zone 4 / §7 Color Independence | Art Director + UX | P1 | Required: text + icon (both must be color-independent). Badge geometry must coexist with the intent glyph without occluding the damage readout (ch21). |
| **OQ-CH3** | How is "Plate overflow bounce" rendered? | §5 C2 / §7 | Art Director | P2 | The 150ms "bounces off cap-line" animation is conceptually important for the Plate-now-or-save read (connects to the Turn-1 Plate wasted open design question in project memory). Needs a concrete visual spec. |
| **OQ-CH4** | Pause binding on gamepad — where does pause live? | §6 row 13 | UX | P1 | Start is End Turn, Select/View is Accessibility overlay, Y is quick-focus. Pause needs a home: options are (a) LS click (L3), (b) long-press Start (300ms hold opens pause instead of End Turn), (c) a dedicated pause overlay accessed via Back button replacing Accessibility's current binding. Needs a UX decision backed by a short gamepad usability check. |
| **OQ-CH5** | How does the combat log overlay interact with the Accessibility overlay? Can both be open simultaneously? | §7 Cognitive + Overlay | UX | P2 | Both are modal overlays but serve different needs. Default proposal: open combat log closes Accessibility overlay (and vice versa). Alternative: stacked modals with Esc dismissing one layer at a time. |
| **OQ-CH6** | Does the "ENRAGE IN N" overlay on the turn counter show the exact turn count, or a qualitative threshold? | §4 Zone 1 Turn Counter | Game Design + UX | P1 | Current spec says "ENRAGE IN N". N = turns until the enemy crosses its enrage HP threshold at *current* damage pace? Or N = a fixed HP-based countdown regardless of player action? This affects how gameplay-critical the number is; if it's a prediction, it must update every turn. If it's fixed, it risks being misleading. |
| **OQ-CH7** | How does the HUD render an enemy whose archetype has *no* reposition capability (CanReposition=false)? | §4 Zone 2/3 + §5 B3 | UX + Game Design | P1 | Per Enemy System G.4 constraint 8, an enemy with CanReposition=false cannot satisfy its own PreferredPosition if the player locks the other position. Does the HUD render a "LOCKED POSITION" indicator on that enemy's Position Glyph? Surfaces a read the player currently can't make without domain knowledge. |
| **OQ-CH8** | Does the screen-reader Verbose tier announce card-hover damage previews? | §7 Screen Reader | Accessibility Specialist | P2 | Verbose tier announces every card play (~15–25 per turn). Does it also announce every hover / focus change mid-decision? Risk: TTS queue floods on power-user hand-cycling. Needs a screen-reader-primary playtester's input. |
| **OQ-CH9** | How does the HUD treat a card with MULTIPLE keyword states (e.g., Innate + Retain + Ethereal stacked)? | §4 Zone 6 Keyword-State Indicator | UX + Card System | P2 | Current spec implies one indicator per card. If a card can carry ≥2 stacked keywords, do indicators stack vertically on the card frame? Tile horizontally? Cycle? Card System GDD review needed. |
| **OQ-CH10** | What happens when the player's Hand exceeds the width of Zone 6? | §4 Zone 6 | UX | P2 | Card System sets MaxHandSize; if MVP MaxHandSize = 10 and the zone fits 7 at 1920×1080, the overflow needs a solution. Options: horizontal scroll (violates Philosophy tenet 1 if it hides state), fan-out (angular overlap), scale-down past a threshold. Pending a concrete MaxHandSize from Card System tuning. |
| **OQ-CH11** | Are damage flyouts (ch38) color-coded for splash-damage origin vs direct-hit origin? | §4 Zone 9 / §5 C1 | UX + Art Director | P3 | Current spec has three flyout types (HP/Armor/Killing). Splash damage is derived but not distinct. Adding a 4th type adds complexity; omitting means the player can't read that the hit was splash. QA playtest will tell us if the read matters. |
| **OQ-CH12** | Where does the "Save-in-Progress" indicator live on gamepad layout specifically? | §4 Zone 8 / §6 | UX | P3 | Current spec puts it in Zone 8 (top-right of End Turn area). On gamepad with focus on Y = End Turn quick-hop, is the indicator legible next to the highlighted button? Needs a sanity check during first Unity prototype. |
| **OQ-CH13** | Does the HUD render a "Next intent pool: ENRAGED" preview if enrage triggered this turn and the intent draws on the next transition? | §5 B4 | Game Design + UX | P2 | Currently: enrage triggers, ch2 shows "ENRAGED," and the next intent draws on the next EnemyTurn → PlayerTurn transition. Between trigger and draw, the player sees the enraged chip but not the *next* intent's enraged version. Should the HUD preview the draw-from-enraged-pool state mid-turn, or wait for the draw? Affects pacing of the Oh-shit moment. |
| **OQ-CH14** | What is the exact format of the "ALIVE / BROKEN" text badge for localization? | §4 Zone 2/3 / §7 Out of scope | Localization Lead + UX | P2 | "ALIVE" and "BROKEN" are English. Abbreviated glyphs (A / B) are more localization-friendly but lose readability. The full words are decision-critical so a shortened form could hurt. Needs a localization pass proposal. |
| **OQ-CH15** | How is "focus outline" visually differentiated from "target reticule echo" (ch36)? | §4 Zone 2/3 / §6 Focus | UX + Art Director | P1 | Both are outlines on a subsystem slot. Focus is a navigation state; Targeting Reticule Echo is a combat state (card is mid-targeting). Both can be present simultaneously if the player focuses a target slot while a card is active. Needs a visual protocol (e.g., focus = dashed 2px; target echo = solid 3px with glow). |
| **OQ-CH16** | Does "skip animation" (§6 row 15) skip the Frame-destruction end-cascade? | §5 B5 / §6 row 15 | UX | P2 | Combat-end is the only HUD behavior that gates input. Allowing skip could shorten the 2s destruction animation to instant, which might feel abrupt for an important narrative moment. Proposed default: combat-end cannot be skipped; all other animations can. Needs confirmation. |
| **OQ-CH17** | How does the HUD render during Node Encounter Ambush setup — is there a handoff animation from Node Map? | §5 D2 / Node Encounter / Node Map | UX + Game Design | P2 | Node Map § I hover surfaces EncounterType as an Ambush overlay on the node. When the player commits and transitions to Combat, is there a visual continuity (the Ambush silhouette channel carries through) or a hard cut to the Combat HUD's Ambush cold-start? Cross-spec question. |
| **OQ-CH18** | What is the HUD's response to a save-commit failure during combat (disk-full, IL2CPP stripping)? | §5 D1 / ADR-0004 (Accepted) | Technical Director | P1 | ADR-0004 establishes "save cannot gate input" and a per-category independent recovery chain. The behavioral gap: if `SaveSystem.CommitAsync` throws, does the save indicator silently show a red error glyph, or is the player informed? A mid-combat modal is forbidden (per §5 D1 contract). Recommended resolution: the indicator transitions to a red-glyph state for ≥2s, then fades — non-blocking, diagnostic only. Needs TD sign-off against ADR-0004 §recovery-chain. |
| **OQ-CH19** | Do color-vision simulation proofs need to ship before the Combat HUD enters alpha, or can they trail? | §7 Color Independence | Art Director + Accessibility Specialist | P2 | Formal pre-1.0 commitment, but if palette proofs slip, does alpha gate proceed? Proposed answer: alpha proceeds against a shape-redundancy baseline; palette proofs are a pre-1.0 gate. |
| **OQ-CH20** | Is there a first-run onboarding HUD overlay that introduces the dual-portrait read? | §7 Cognitive / §1 Philosophy | UX + Game Design | P2 | The "HUD Hints toggle" covers first-encountered elements, but the broader "both sides are portraits, not HP bars" read is a paradigm the player is learning. First-run tutorial overlay or pure discovery? Intersects with a tutorial design that does not yet exist. |

### Summary by blocker level

- **P0 (implementation blocker)**: none — the spec is sufficient to begin Unity prototyping.
- **P1 (pre-alpha)**: OQ-CH1, OQ-CH2, OQ-CH4, OQ-CH6, OQ-CH7, OQ-CH15, OQ-CH18 — **7 questions**.
- **P2 (pre-1.0)**: OQ-CH3, OQ-CH5, OQ-CH8, OQ-CH9, OQ-CH10, OQ-CH13, OQ-CH14, OQ-CH16, OQ-CH17, OQ-CH19, OQ-CH20 — **11 questions**.
- **P3 (tracked)**: OQ-CH11, OQ-CH12 — **2 questions**.

### Carry-forward obligations to other specs

Specs that must reference this Combat HUD spec when they are authored:

1. **Post-Combat Flow UX spec** — must handle the cross-fade FROM Combat HUD (§5 B5 ends with fade to post-combat) and inherit any state that was visible at combat-end.
2. **Map HUD / Node Map UX spec** — must inherit the Ambush handoff (OQ-CH17) and render the `EncounterType` hover surface consistently with how the Combat HUD renders it.
3. **Part Inspect UX spec** — must render the 4-subsystem portrait in a way that matches the in-combat portrait (visual identity persists across contexts).
4. **Audio spec** — must deliver the caption text strings for every decision-relevant SFX referenced in §7.
5. **Save ADR** — must ratify the "save cannot interrupt combat input" contract referenced in §5 D1.

### Dependencies flagged for other GDDs

- **Enemy System G.4 constraint 8** (CanReposition=false deadlock) is referenced by OQ-CH7. If Enemy System GDD revises that rule, re-open OQ-CH7.
- **Card System MaxHandSize** is referenced by OQ-CH10. Combat HUD's hand-overflow solution depends on the final value.
- **Card System keyword-stacking rules** (Innate / Retain / Ethereal / Exhaust) is referenced by OQ-CH9.
- **Node Encounter GDD OQ-NE12** (3-channel HostileTiltDelta accessibility gate) is precedent for §7 3-channel redundancy rule; resolving OQ-NE12 may affect §7 review process.

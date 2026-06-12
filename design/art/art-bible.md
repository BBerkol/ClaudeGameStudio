# Wasteland Run — Art Bible

*Maintained by: Art Director*
*Last updated: 2026-04-25*
*Status: Complete — AD-ART-BIBLE gate passed 2026-04-18*

> **Art Director Review (AD-ART-BIBLE)**: CONCERNS (resolved) 2026-04-18 — 8 concerns raised; all addressed: sprite master canvas system, semantic amber shifted to `#D08010`, Haven separated to ~5500K cool ambient, NPC section marked internal-authored-only, VFX visual language section added (5.6), chassis memory budget marked provisional.

### Revision Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-04-18 | Initial AD-ART-BIBLE sign-off. | Gate closure. |
| 2026-04-25 | §3.4 — added Plating Cards archetype (pentagon icon anchor, double-ruled header band, seamed edges, ember-orange forbidden); "five card-type shapes" → "six card-type shapes" (added pentagon); appended "Card Taxonomy: Mechanical Kind × Visual Archetype" subsection with mapping table. | PR-C2 gap-check exposed a dual-axis card taxonomy conflict between engine `CardKind` enum (Attack/Plate/Reposition/Repair) and existing archetype vocabulary (Precision/Assault/Control/Repair/Maneuver). Resolution: keep both as orthogonal axes — mechanical kind drives rules, visual archetype drives shape. Plate mechanical kind required its own visual archetype. |
| 2026-04-25 | §4.3 EMBER ORANGE entry — canonical meaning widened from "Promise/Direction/Haven" to "Direction/Restoration/Commitment" (three surfaces of one promise); permitted compound documented for selected Repair card. §3.5 HUD Shape Exception #1 — added compound case + greyscale-readability visual test criterion. §3.4 Repair Cards — added cross-reference to selection compound. | PR-C2 gap-check (item C2) flagged ember orange dual-role between Repair card edge and selected-card outline. Decision (Option A): accept the compound. The two rules are geometrically additive (selection outline outside card perimeter; Repair edge on card bottom), and the three ember roles already share a single underlying meaning ("this is where player intent lives"). Greyscale-readability test added so Repair identity cannot be obscured by selection state. |
| 2026-04-25 | §4.3 ASH WHITE entry — added "Non-Card Focus" application: 2px ash white outline as the keyboard focus indicator on non-card focusable UI (buttons, settings, menus, tooltips, dropdowns); text-contrast guard (≥4px padding from glyphs); mouse-hover differentiated to 1px inner highlight at 60% so input modality reads from the screen. §3.5 HUD Shape Exception #4 added — parallel-weight rule to the active card highlight. | PR-C2 gap-check (item G1) — non-card UI needed a focus color now that ember is locked to Direction/Restoration/Commitment. Decision (Option A): ash white 2px outline. Parallel weight to ember 2px card selection establishes a unified "2px outline = the live thing" register; color carries the kind of liveness (ember = card commitment, ash white = navigational presence). The two outlines never compose on the same element. |
| 2026-04-25 | §4.7 Legendary tier — added Reduce Motion fallback: pulse animation replaced by a static composite border (1px outer `#C87820` + 1px inner `#E09840`) when Reduce Motion is enabled. Rarity differentiation from Rare preserved via value + composite double-border (no other tier uses composite). Cross-listed in `design/accessibility-requirements.md` §3.3 gated-effects list. | PR-C2 gap-check (item G2) — Legendary pulse is the only animated rarity treatment; Reduce Motion users were silently losing the rarity signal under the existing spec. Static composite preserves visual identity without animation. |
| 2026-04-25 | §7.4 Animation Feel — added Rust-Shimmer Tell spec. Pixel-art interpretation (palette index cycling on rust-pixel set, no shader work), 6-frame asymmetric `[0,1,2,3,2,1]` cycle at 4fps = 1500ms loop, ~2% luminance variation matching Node Encounter §H.3. Haptic-absent fallback amplifies visual to 3% + audio to +4dB when no gamepad is connected. Reduce Motion alternative render = static frame 3 (peak luminance). Disambiguated from the "decorative rust shimmer" entry in accessibility-requirements.md §3.3. | PR-C2 gap-check / gate-check item AD-C1 — HUD UX implementation handoff requires the rust-shimmer animation defined in pixel-art terms with explicit frame budget and KBM/Reduce-Motion fallbacks. |
| 2026-04-25 | §3.5 — added "Encounter Type Signal Geometry (Standard vs. Ambush)" subsection. Non-color channel = single top-left 4px corner cut at 45° + chevron glyph (`>`) prefix on labels (or anchor on text-free surfaces), distinct from Maneuver Cards' two-corner symmetric cut. Greyscale acceptance test included. Combat HUD Ambush urgency tint (OQ-CH1) reframed as additive, not load-bearing. | PR-C2 gap-check / gate-check item AD-C2 — colorblind-safe Ambush overlay precondition for Map UX implementation handoff. Closes accessibility-requirements.md §11 AD-C2. |

---

## Section 1: Visual Identity Statement

### The One-Line Visual Rule

**Every element is treated as a found object — weathered, stamped, and worn, but legible as a silhouette from ten meters.**

This rule is the production filter. If an asset looks manufactured or pristine, it is wrong. If it cannot be read as a shape at small scale, it is wrong. Both conditions must be satisfied simultaneously — weathering never excuses illegibility, and legibility never licenses cleanliness.

---

### Supporting Visual Principles

#### Principle 1: TROPHY SILHOUETTE

**Definition:** Each chassis and each enemy vehicle has one non-negotiable geometric signature — a silhouette that reads as a distinct, nameable shape at 48×48 pixels. Scout is low-slung and angular, like a blade parallel to the road. Assault is a forward wedge, mass concentrated at the nose. Heavy Truck is a brutalist rectangle with bolted bulk at the shoulders — a thing that was never meant to be fast.

**Design test:** When the silhouette question is ambiguous — "should I add this armored cowling to the Scout?" — this principle says: does the addition preserve or compromise the blade shape at icon size? If it softens the angle into a blob, cut it.

**Pillar served:** Pillar 2 (Chassis Identity) — the silhouette IS the chassis's mechanical promise. A player who cannot tell Scout from Assault at a glance cannot internalize that their build decisions are chassis-native.

---

#### Principle 2: SCARCITY PALETTE

**Definition:** The base palette is iron oxide red-brown (~#8B3A2A), bleached sand (~#D4B896), ash white (~#E8E0D4), and tarnished steel grey (~#7A7872). Each biome gets exactly one high-chroma accent, applied sparingly to environmental detail, hazard indicators, and enemy faction markings — never to the player vehicle's primary read. Warm hues (red-orange range) signal danger, heat, and aggression. Cool hues (blue-grey range) signal scarcity, death, and distance. Haven's ember orange (~#E8630A) is the only hue in the game that carries warmth without threat — it is the one color that means "toward something." It appears on no enemy, no hazard, no card back.

**Design test:** When a biome's accent color is ambiguous — "should this toxic puddle read yellow-green or just grey-brown?" — remove the accent entirely and ask whether the environment still reads as a distinct biome. If yes, the accent was decoration and must be cut. The accent only earns its place by being the difference between recognizable and generic.

**Pillar served:** Pillar 4 (Scarcity with Agency) — the palette enforces the world's premise at the perceptual level before any UI text or mechanic communicates it. When everything looks like it costs something, decisions feel costly.

---

#### Principle 3: COLOR AS OPERATIONAL SIGNAL

**Definition:** In all combat UI, color is a system-state language, not decoration. Green means functional and contributing stats. Amber means degraded — at least one damage state applied, stats reduced. Red means critical or offline — subsystem not contributing. These three states are the only colors permitted on subsystem health indicators. No gradient, no style variation. Color redundancy encoding: green = lighter value + solid fill; amber = mid-value + hatched/dashed fill pattern; red = dark value + fractured/broken fill pattern. Shape and value always reinforce the hue signal — color is never the sole carrier of state information.

**Design test:** When adding a visual treatment to a subsystem indicator — "should a high-plating stack pulse orange for emphasis?" — does orange on a functional subsystem risk triggering the red-means-offline association? If yes, the treatment is prohibited regardless of aesthetics.

**Pillar served:** Pillar 3 (Read to Win) — a player cannot read the correct play if they cannot read the board state at a glance. The operational color language makes system health a pre-cognitive read, freeing working memory for the actual decision.

---

### Color Philosophy Statement

The world of Wasteland Run has been bleached, rusted, and stripped to its essential forms. The palette does not describe poverty as aesthetic flavor — it is the mechanical argument that resources are scarce, decisions are real, and nothing is replaced easily. The four base colors are the world's resting state. Biome accents are the world's one luxury per region. Haven's ember orange is the game's only promise. It appears on no enemy, no hazard, no card back. When the player sees it, they know what direction means something.

---

### What This Direction Explicitly Is Not

- **Not brown-grey noise**: Weathering must be applied with graphic precision — stamped edges, deliberate scratches following structural logic, not random texture overlay. Noise is not wear.
- **Not grimdark uniformity**: The palette has range. Ash white and bleached sand are present and visible. If every frame reads as dark, the scarcity palette has been misapplied — scarcity is about restraint, not darkness.
- **Not decorative complexity**: Parts have one material read: painted steel, bare metal, or structural plastic. A part that reads as three materials simultaneously has failed the found-object rule.
- **Not cinematic lens effects**: No lens flare, no chromatic aberration, no film grain applied globally. RUST ICON is about graphic objects in graphic space — a photograph aesthetic contradicts found-object legibility.

---

## Section 2: Mood & Atmosphere

### Design Intent

Each game state is a distinct emotional register. Atmosphere is the player's primary signal that context has shifted — not a re-skin of adjacent states. Differences between states are changes in contrast, energy, implied light source, and compositional grammar, not just palette swaps.

States are listed in escalating-stakes order. Read in sequence to confirm they form a legible emotional arc from open road to arrival.

---

### State 1: Map Navigation (The Open Road)

**Primary emotion:** Quiet vigilance — the player is making choices. The road belongs to them until they commit to a node.
**Lighting character:** Neutral-warm, diffuse overhead sun, mid-afternoon. ~5500K. Contrast: medium-flat. Wide shallow shadows. Horizon always visible.
**Atmospheric adjectives:** Exposed, parched, unhurried, surveilled, boundless.
**Energy level:** Measured.
**Mood-carrying visual element:** Route lines drawn as hand-scratched marks on a material surface — not a glowing digital map. The player's current position marker is a small, physically-weighted icon (a bolt head, a rivet) — not a pulsing waypoint.
**Production note:** Background shows distance as atmospheric haze — cooler, more desaturated towards horizon. UI layer at base palette values. No bloom. No lens effects.

---

### State 2: Active Combat (Turn-Based Card Fight)

**Primary emotion:** Predatory focus — hunting a solution under pressure. No ambient safety. Every turn narrows options.
**Lighting character:** Hard directional light from a low angle, implied late-afternoon sun from the right. ~3800K. Contrast: high. Deep cast shadows. Enemy vehicle reads as a mass against washed-out background.
**Atmospheric adjectives:** Fractured, airless, pressured, stripped, angular.
**Energy level:** Predatory.
**Mood-carrying visual element:** The enemy vehicle's shadow — long, hard-edged, thrown toward the player position. As enemy subsystems degrade, the shadow shortens (losing mass, losing threat volume).
**Production note:** Background desaturates ~30% relative to map navigation — the world contracts to the encounter. No motion blur. Hit reactions are fast, sharp, and immediate — no slow-mo, no exaggerated camera shake. The violence is functional, not cinematic.

---

### State 3: Post-Combat / Reward (Aftermath)

**Primary emotion:** Spent relief — not triumph. The fight is done. The player is sifting through what remains.
**Lighting character:** Diffuse, directionless — dust not yet settled. ~4800K. Contrast: flat. Evenly lit because the drama is over.
**Atmospheric adjectives:** Settled, dusty, quiet, catalogued, residual.
**Energy level:** Contemplative.
**Mood-carrying visual element:** Scrap and salvageable parts scattered at the base of the frame, lit flatly and without glamour. No glowing loot drops, no animated pickups — objects recovered from wreckage. Part condition communicated by surface wear visible at rest, not solely by UI indicators.
**Production note:** Transition from combat is a lighting temperature shift (amber-warm → neutral diffuse) — signals "danger passed" without UI text. Enemy vehicle stays in frame but visually dead: no shadow, hull listing.

---

### State 4: Merchant / Chopshop Node (Transactional Safety)

**Primary emotion:** Wary transaction — the player is spending something they may need later. The merchant is not a friend.
**Lighting character:** Interior-implied, warm artificial light (lantern/generator). ~2900K. Contrast: medium. Pools of warm light with cool shadow fill.
**Atmospheric adjectives:** Sheltered, close, calculated, opportunistic, oil-stained.
**Energy level:** Measured, with transactional undertone.
**Mood-carrying visual element:** A visible, physical light source (lantern hung from a hook, work light clamped to a shelf) that explains the warm temperature. Warmth is borrowed and will end when the transaction does. Haven's ember orange (~#E8630A) must NOT appear here — the warmth is incandescent yellow, not the game's promise color.
**Production note:** Background reads as enclosed space — walls, partial roof, work surface. Compression of space differentiates from map navigation. Items the player cannot currently afford shift to cool shadow fill.

---

### State 5: Unknown Node / Event (Tension of the Unknown)

**Primary emotion:** Suspended dread — not fear of something specific, but the physical sensation of not yet knowing.
**Lighting character:** Pre-dawn or overcast. ~6500K. Contrast: medium-high through value only — cool and high-contrast without red/amber danger hues. No clear directional shadow source.
**Atmospheric adjectives:** Liminal, still, overcast, anticipatory, charged.
**Energy level:** Predatory-adjacent — coiled, not yet released.
**Mood-carrying visual element:** One ambiguous element in frame — a silhouette on the road that isn't a vehicle, a dust signature that isn't from wind. Specific form resists immediate parsing at correct read distance. Everything else reads clearly; only the event's central subject is controlled-illegible.
**Production note:** This is the only state where mild illegibility is intentional and controlled. Resolution of that ambiguity is the event's first beat.

---

### State 6: Biome Transition / Boss Approach (Escalating Dread)

**Primary emotion:** Inevitable weight — something larger is ahead and cannot be avoided.
**Lighting character:** Hard back-light or rim-light from the direction of travel. ~3200K shifting toward red-orange as proximity increases. Contrast: high — maximum in the entire game. Player vehicle partially silhouetted.
**Atmospheric adjectives:** Imminent, silhouetted, furnace-lit, tectonic, inescapable.
**Energy level:** Elegiac — slow and absolute. The player has crossed a threshold.
**Mood-carrying visual element:** The player vehicle's silhouette against the transition light — TROPHY SILHOUETTE serving a narrative function. If the vehicle is heavily damaged, visible part-loss shows in the silhouette — gaps where armor was, asymmetry where a weapon mount is gone. *(Flag: requires part-loss to be visually represented in the chassis sprite.)*
**Production note:** Biome accent color reaches highest saturation here; all other colors pull toward base palette. Boss health UI does not appear until engagement begins.

---

### State 7: Haven Arrival (The Run's End)

**Primary emotion:** Earned stillness — not triumph, not relief. The specific feeling of having completed a circuit: you left, something happened, you returned changed.
**Lighting character:** Cool open-sky ambient, ~5500K — near-white, like late afternoon overcast after a long drive. Contrast: low. Long diffuse shadows. Ember orange (~#E8630A) at fullest presence in the entire game, appearing as fire and lantern accents against the cool ambient rather than dominating the light temperature.
**Atmospheric adjectives:** Cool-still, worn, recognized, circular, ember-lit.
**Energy level:** Elegiac — the elegy of having come back.
**Mood-carrying visual element:** Haven's light sources (ember orange fires, lanterns, lit structure) punch as hot, glowing accents against the cool near-white ambient. The contrast between the cool sky and the warm fire sources is the visual signal that makes Haven immediately distinct from all other states. Player vehicle parks in frame with all damage visible. The game does not clean it up. The chassis carries the run's history.
**Production note:** This is the only state where ember orange appears on environmental elements (not just UI). The cool ~5500K ambient creates unambiguous temperature separation from Chopshop (~2900K warm incandescent) — Haven reads cool-with-ember-fire, Chopshop reads warm-from-work-light. No testing required to separate them.

---

### State 8: Main Menu / Chassis Selection

**Primary emotion:** Considered appetite — the player is looking at a choice they are about to commit to. A preparation ritual.
**Lighting character:** Early morning or overcast pre-dawn. ~5000K. Contrast: medium. Flat, directionless — the world is not yet activated.
**Atmospheric adjectives:** Still, deliberate, pre-operative, expectant, unlaunched.
**Energy level:** Contemplative, forward-facing.
**Mood-carrying visual element:** Selected chassis in three-quarter view against neutral background — not a dramatic spotlight, not a hero pose. The vehicle is simply there, in real light, showing its silhouette and wear. It looks like it has been used before. Locked chassis appear in cooler, more desaturated values — possibilities, not promises.
**Production note:** Chassis selection transitions must preserve silhouette legibility throughout. No biome accent color on this screen — palette-neutral. Chassis name in ash white (~#E8E0D4) only.

---

### Cross-State Continuity Rules

1. **Ember orange (~#E8630A) appears at full ambient presence only in State 7 (Haven).** Partial presence permitted in States 4 and 8 only when clearly diegetic and single-source. Haven's ambient is cool (~5500K); ember orange appears there as fire/lantern accents against the cool light — not as the ambient temperature itself.
2. **The player vehicle is always visible in states 1–7 and always carries its current damage state.** Visual storytelling is cumulative.
3. **High contrast (States 2 and 6) is reserved for maximum-stakes moments.** Overuse destroys the signal. States 3, 4, and 7 should be noticeably flatter than State 2.
4. **Color temperature is the primary mood differentiator between states.** If two adjacent states share the same temperature, they will feel like variants of the same mood — check temperature first.
5. **No state uses lens effects, bloom overrides, or chromatic aberration.** URP post-process stack configured per-state for temperature and contrast only. Bloom disabled globally or held at minimum non-visible threshold.

---

## Section 3: Shape Language

> **Tuning note:** All numeric values in this section (ratios, pixel dimensions, angles, percentages) are starting targets, not immovable rules. They should be tested during production and adjusted based on what reads correctly on screen. When a value is revised during testing, update it here so the document stays current.

---

### Design Intent

Shape is the first signal the eye receives before color resolves, before text is readable, and before the player consciously processes what they are looking at. In Wasteland Run, shape carries two simultaneous obligations: it must serve the found-object visual rule (angular, assembled, non-manufactured) and it must serve legibility at combat distance (distinct silhouettes, clear figure-ground separation, readable hierarchy). Where these obligations conflict, legibility wins. A beautifully weathered chassis that reads as visual noise at 48×48px has failed at its primary job.

The geometric vocabulary of this game is built on one structural bias: **mass is earned, not given.** Everything angular earns its edges through implied function — a wedge deflects, a flat plane takes a hit, a beveled corner was filed down for clearance. Nothing is tapered for aesthetic elegance. This rule governs chassis design, environment geometry, UI frames, and card shapes equally.

---

### 3.1 Chassis Silhouette Philosophy

#### The 48×48 Silhouette Test

Before any chassis sprite is approved at any resolution, its silhouette must be tested at 48×48px (the icon-size minimum from the Visual Identity Statement). The test is binary: can a player who has never seen the game name the archetype from the silhouette alone? If the answer requires hedging — "I think that's the Scout?" — the silhouette has failed.

This is not a production shortcut. It is the primary design constraint that governs all part customization decisions. A part that looks correct at full resolution but dissolves the silhouette at icon size is the wrong part design.

---

#### Scout: The Blade Rule

**Geometric identity:** A horizontal slash. The Scout's silhouette is defined by its length-to-height ratio — minimum 3:1 width to height. The roofline drops at both ends, peaking slightly behind the midpoint (mass is in the engine block, not the nose). The front edge angles at 18–22 degrees from vertical, leaning forward. The rear tapers, but does not terminate in a point — the tail is cut at roughly 30 degrees, implying that it was sheared, not designed.

**Geometric rules (tuning targets):**
- No vertical element taller than 30% of total chassis height (cab structures, weapon mounts included)
- Maximum silhouette height at any point: 40px in the 48×48 test
- Front approach angle: 18–22 degrees off vertical (acute nose, not blunt)
- Rear departure: 25–35 degrees off vertical (tapered, not squared)
- Wheel arch protrusion: minimal — no more than 6% of chassis height above the roofline plane

**What makes Scout read as fast:** Not curves. Speed in this world is not streamlined — it is exposed, mechanical aggression. Scout reads as fast because it is low (close to road = reduced visual mass), because its longest dimension is horizontal (the eye moves laterally across it quickly), and because its leading edge is acute (directional vector is unambiguous — this object has a forward). The 3:1 ratio is the speed signal.

**Part customization rule:** Parts added to the Scout may extend its horizontal length (weapon mounts forward, armor plating rear) but must not add vertical mass. A roof-mounted weapon that brings total height above 40px in the silhouette test fails. The correct Scout weapon mount is slung beneath the chassis or flush to the hood — never towers. Armor plating on the Scout extends the horizontal form, or wraps the flanks below the wheel line. The blade shape must survive every legal build.

**Pillar anchor:** Pillar 2 (Chassis Identity) — Scout's precision-based card kit only makes intuitive sense if the chassis reads as a precision instrument. The blade silhouette is the player's first lesson in what Scout does.

---

#### Assault: The Wedge Rule

**Geometric identity:** A forward-leaning trapezoid. The Assault's mass is front-loaded — the nose is wide and blunt, carrying most of the visual weight. The silhouette widens from roof to ground (base wider than top by 15–25%). The front face is nearly vertical (5–10 degrees off vertical), implying that it meets resistance head-on rather than deflecting it. Rear roofline slopes down at 40–50 degrees, creating the wedge read.

**Geometric rules (tuning targets):**
- Width-to-height ratio: 2:1 to 2.2:1 (wider than Scout, shorter than Heavy)
- Front face verticality: 5–10 degrees off vertical (blunt approach, not acute)
- Roofline slope: 40–50 degrees from horizontal at the rear
- Silhouette base width is 15–25% wider than roofline width
- Wheel arch protrusion: moderate — up to 12% of chassis height, reinforcing the planted, wide-stance read

**What makes Assault read as adaptable:** The wedge reads as a shape that has been built up rather than carved down. Where Scout looks stripped, Assault looks equipped — surface is used. Weapon mounts on the Assault can occupy the roof without penalty because the silhouette is already mass-forward; a roof structure at 50% of chassis height still reads as subordinate to the dominant front-nose mass. The trapezoid shape creates implied visual stability — it reads as a thing that will not be moved sideways.

**Part customization rule:** Assault tolerates the widest range of part placements without silhouette distortion. Roof weapons permitted up to 50% of chassis height. Flank armor permitted on both sides simultaneously (cannot extend horizontally beyond 120% of base width or the wedge silhouette becomes a rectangle). The front face must remain visually dominant — no rear-heavy build can pass review if the rear silhouette mass exceeds the front by visual inspection at 48px.

**Pillar anchor:** Pillar 1 (Vehicle as Character) — Assault's part-loss impact is most legible because its parts are visually distributed across a larger surface area. Losing a flank armor plate on Assault is immediately readable in silhouette (the trapezoid becomes asymmetric). The shape works harder as a damage state communicator.

---

#### Heavy Truck: The Block Rule

**Geometric identity:** A weighted rectangle with bolted shoulders. The Heavy Truck's silhouette is 1.4:1 to 1.6:1 width-to-height — the only chassis where height approaches width. The corners are not rounded — they are 90 degrees or chamfered at 5–10 degrees maximum (one sheared edge, not a curve). The roofline is flat or very nearly flat (maximum 5-degree slope). Front face is full vertical. The silhouette implies that this object was cut from stock material, not designed.

**Geometric rules (tuning targets):**
- Width-to-height ratio: 1.4:1 to 1.6:1 (tallest relative silhouette in the game)
- Roofline: flat to 5-degree slope maximum — no wedge, no taper
- Corner treatment: 90-degree or chamfer only — no curves above 3px radius at 48px test size
- Front face: vertical, not angled — implies direct confrontation, not deflection
- Silhouette "bolted mass" rule: external add-ons (armor plates, weapon mounts, fuel tanks) must read as attached objects, not integrated forms. Each added part shows a visible seam or bracket line at full resolution.

**What makes Heavy Truck read as siege:** The rectangle reads as a thing that occupies space rather than moves through it. It has no directional vector in its silhouette — unlike Scout (acute nose points forward) and Assault (wedge implies direction), the Heavy Truck's rectangle shape is directionally ambiguous. This ambiguity is correct — it moves toward threats, it does not chase them. The "bolted mass" rule reinforces the found-object principle most aggressively on this chassis: the Heavy Truck looks assembled from components that once belonged to other things.

**Part customization rule:** Heavy Truck part additions must maintain or reinforce the blocky read. No part may introduce a curved surface longer than 8px at full resolution. Weapon mounts add to the shoulders (upper corners of the rectangle), reinforcing the broadness of the silhouette. Armor plating wraps the existing rectangle faces — it does not reshape the perimeter. A part that rounds any corner of the Heavy Truck silhouette fails review.

**Pillar anchor:** Pillar 5 (Route Reflects Vehicle State) — the Heavy Truck's silhouette communicates its constraints directly. Its visual mass explains why narrow routes read as threats. A player looking at the route map and looking at their Heavy Truck silhouette should feel the spatial incompatibility before reading any route width stat.

---

#### Cross-Chassis Silhouette Differentiation

At 48×48px, the three silhouettes must be distinguishable by:
1. Height-to-width ratio alone (Scout: horizontal slash / Assault: wide trapezoid / Heavy: near-square block)
2. Roofline character alone (Scout: low peaked, tapered ends / Assault: rear-sloping wedge / Heavy: flat roof)
3. Leading edge character alone (Scout: acute angle / Assault: blunt near-vertical / Heavy: full vertical)

No two chassis may share more than one of these three characteristics. If a proposed part configuration causes two chassis to share two or more characteristics, the configuration fails.

---

### 3.2 Enemy Vehicle Silhouette Rules

#### The Threat-Type Taxonomy

Enemy vehicles signal their threat type through three primary silhouette variables: **mass concentration** (where is the heavy part of the shape?), **protrusion pattern** (what extends beyond the chassis envelope?), and **surface regularity** (smooth stock shapes vs. heavily modified profiles).

Players must be able to learn enemy silhouette reading through play — this requires consistency. Each threat type maps to one silhouette grammar. The grammar never breaks. If an enemy is weapon-heavy, its silhouette is weapon-heavy, always.

---

#### Threat Type 1: Weapon-Heavy (Gunship, Artillery, Multi-Weapon)

**Silhouette grammar:** Protrusion-dominant. The chassis base is secondary in visual area to the weapons mounted on it. Weapon barrels extend beyond the chassis envelope in at least two directions simultaneously (forward + upward, or lateral + forward). The base chassis silhouette is identifiable but dwarfed.

**Geometric rule (tuning target):** Weapon protrusion extends minimum 30% of chassis width beyond the hull in at least one axis. Multiple protrusions create an asymmetric, thorny outline. The silhouette reads as "many vectors pointing outward" at 48px.

**Player learning cue:** "Lots of things sticking out means lots of guns." The thorny/spiky profile is the semantic universal for "many attack vectors." The first encounter with any weapon-heavy enemy type must provide enough distance to read this profile before engagement, so the player can identify and preemptively form a response.

**Shape contrast from player vehicles:** Player chassis have controlled protrusion (governed by the customization rules above). Weapon-heavy enemies violate those constraints — their protrusion is ungoverned. This visual contrast signals the asymmetry of the encounter: the enemy was built for attack, not survival.

---

#### Threat Type 2: Mobile (Fast Raider, Flanker, Pursuit)

**Silhouette grammar:** Scout-analogue, but visually corrupted. Mobile enemies use the horizontal blade read — low, wide, direction-forward — but with irregularity applied. The blade is not clean: panels are missing or offset, the roofline is notched, the nose is crumpled or asymmetrically armored. The speed signal is present but damaged.

**Geometric rule (tuning target):** Length-to-height ratio matches or exceeds Scout (3:1 or more). However, at least one silhouette element is visually irregular — an asymmetric notch, a panel gap, a non-parallel line in the roofline. The chassis reads as fast but improvised.

**Player learning cue:** "Low and long means it moves fast — the damage means it's reckless." Mobile enemies are a threat of positioning, not firepower. Their silhouette teaches the player that low profiles are high-mobility, so when the player sees this shape, their first response is "where will it be next turn, not how hard will it hit."

---

#### Threat Type 3: Armored (Shield, Tank, Brawler)

**Silhouette grammar:** Heavy Truck-analogue, but surface-interrupted. The block read is preserved and reinforced — same 1.4:1 to 1.6:1 ratio, same flat roofline rule. The distinction: armor plates on enemy armored vehicles show visible layering in silhouette. The perimeter of the shape is not a clean rectangle — it is a rectangle with additional rectangles bolted to its faces. The silhouette reads as "a rectangle that absorbed other rectangles."

**Geometric rule (tuning target):** Minimum two visible armor plate extensions add to the perimeter silhouette at full resolution. Each extension is a distinct rectangular or trapezoidal form — no fused, smooth reads. The total silhouette still reads as block-shaped at 48px; the layering detail is visible at 96px and above.

**Player learning cue:** "Blocky and thick means it absorbs damage — target the gaps between the plates." At 48px the silhouette reads the same as Heavy Truck (player learns: this is tough). At 96px the layered armor detail becomes visible (player learns: the plates are separate, implying they can be separately targeted or stripped). This two-resolution read is intentional and must be preserved in production.

---

#### Enemy Silhouette Learning Design

The three threat types must appear in the first three distinct encounters, in an order that supports learning:
1. First encounter: Mobile enemy (fast and dangerous, silhouette legible at long range given its horizontal profile)
2. Second encounter: Weapon-heavy enemy (the player recognizes a new silhouette grammar and correctly predicts it has more guns)
3. Third encounter: Armored enemy (the player applies block-read learning from their own Heavy Truck to correctly predict toughness)

This ordering is a design recommendation, not an art directive. *(Flag: confirm encounter sequence with game designer — if the ordering changes, verify that the revised learning sequence does not create a silhouette-reading gap.)*

---

### 3.3 Environment Geometry

#### The World's Geometric Bias

The wasteland world is built on one structural rule: **natural forms have been interrupted by constructed forms, and the constructed forms have begun to fail.** This means:

- Organic shapes (dunes, rock faces, dried riverbeds) are the baseline — soft, varied edge, not angular
- Human-made structures interrupt these organics with hard 90-degree and 45-degree angles
- But the human-made structures are failing — corners are eroded, beams sag, walls lean at 5–15 degrees off plumb

The result is a world where the dominant visual language is right angles that have lost their precision. Not curved-organic, not crisp-geometric — but geometric-degraded.

**Why this serves the themes:** Scarcity and exposure. The world built these structures and the world has been reclaiming them. The angular interruptions are evidence of prior human activity (things were made, things existed). Their degradation is evidence of the game's premise (the world is not maintained). The player's vehicle — the one coherent, maintained geometric form in the world — reads against this background as intentionally kept.

---

#### Biome Geometry Vocabularies

**Biome 1: Cracked Earth (Open Wastes)**
- Ground: shallow undulating dunes — curves with 150–300px arc radii at 1920px full scale. No sharp ground features.
- Structures: military/industrial ruins — corrugated metal walls, concrete slabs, fuel drum clusters. All surfaces at 90-degree or 45-degree angles to ground. Structural elements lean at 5–12 degrees off vertical (post-collapse angle).
- Hazard geometry: angular debris fields — shattered concrete, vehicle wrecks. Sharp edges, irregular polygons, nothing rounded. Hazard geometry must read as jagged at the biome zoom level.
- Horizon line: unobstructed — the player can see ahead. Exposure is the biome's defining feeling. Nothing blocks the horizon except distant structures at silhouette-only scale.

**Biome 2: Toxic Flats**
- Ground: hard flat surface interrupted by structural debris. Ground plane reads as a made thing (concrete, tarmac) that has fractured. Fracture lines at 30–60-degree angles to the route direction.
- Structures: dense, tall, partially collapsed. Vertical elements (columns, crane towers, silos) still present but truncated at irregular heights. No structure reaches a clean horizontal roofline — all are cut by collapse. Vertical:horizontal ratio 3:1 in structural forms (inverse of Cracked Earth).
- Claustrophobia geometry: structures crowd the route corridor to 60% of Cracked Earth's effective width. The player's vehicle silhouette reads larger relative to available space. This directly supports Pillar 5 (Route Reflects Vehicle State) — Heavy Truck players feel the narrowing most acutely.
- Hazard geometry: overhead collapse risks. Horizontal beams at 45-degree failure angles, debris cones (triangular ground shadows) marking fall zones.

**Biome 3: Ruins**
- Ground: perfectly flat, cracked grid pattern. Crack geometry is regular hexagonal or irregular polygonal — never organic curves. The flatness is the threat.
- Structures: none, or the remnants of structures reduced to footings — only the 90-degree corners of foundations remain. Negative space geometry — the shapes of absent structures.
- Sky geometry: the horizon line is visually dominant. All visual weight is at the top of the frame (sky, distance, unresolved horizon). Ground is empty. This creates maximum exposure feeling.
- Hazard geometry: flat and wide — chemical pools, wind barriers. Hazards are thin horizontal strips at ground level, not vertical elements. Hard to avoid laterally, easy to read in advance.

---

#### Biome Geometry Enforcement Rule

Every biome's geometry must pass a "compositional silhouette test": if all texture, color, and detail are removed and only the structural silhouettes remain, the biome must still be recognizable as distinct from the other biomes by shape composition alone. Cracked Earth: gentle undulation with angular interruptions. Toxic Flats: dense vertical truncated forms. Ruins: pure horizontal with geometric ground texture. This test is run by the art director at each biome's first complete rough-in.

---

### 3.4 Card UI Shape Grammar

> **Tuning note:** Card dimensions (200×280px), border widths, corner radii, and icon anchor sizes are starting values. Test at your target UI resolution before locking. If the grid or screen density requires adjustment, update the values here.

#### Cards as Found Objects

Cards in Wasteland Run are not digital cards, fantasy cards, or clean UI elements. They are **stamped field-order forms** — the kind of object that might exist in this world's military or wasteland economy as a physical document. They look like they were produced on a hand-press with a metal stamp, not typeset digitally.

**Base card dimensions:** 200×280px at UI resolution. Ratio 5:7 — taller than wide, legible as "document" rather than "tile." This is the production-standard starting size; all card art, text, and iconography must be specified relative to this frame.

**Corner treatment:** 3px radius at UI resolution. Near-square corners — the corner was not designed, it was cut with a guillotine blade that is slightly worn. No truly rounded corners (those read as manufactured/digital), no sharp 90-degree corners (those read as screen UI, not physical object).

**Border style:** Double-rule border. Outer rule: 2px, base palette (tarnished steel grey ~#7A7872). Inner rule: 1px, 4px inset from outer rule, same color at 70% opacity. The gap between rules is a deliberate dead zone — no content enters this zone. This creates the visual impression of a stamped metal frame, not a CSS border.

**Card back texture:** Hatched grid pattern, 45-degree angle, 4px grid spacing. This is not decorative — it reads as the back of a stencil plate. All card backs are identical (no card-type information on back — the player cannot pre-read hand order).

---

#### Card Type Shape Differentiation

Cards differentiate by three simultaneous shape signals: **header band shape**, **icon anchor shape**, and **edge treatment**.

**Precision Cards (targeted subsystem attacks, aimed shots)**
- Header band: horizontal rule at top, 16px tall, full card width. Rule is exact and unbroken — precision reads as controlled geometry.
- Icon anchor: circle, 24px diameter, centered on header band. Circle is the only curve in the card system; it signifies accuracy and point-targeting.
- Edge treatment: no edge modification. The card perimeter is the standard double-rule border with no additional elements. Precision is the absence of noise.
- At-a-glance read: "Circle on a ruled band, clean edges — this card does something specific to one target."

**Assault Cards (damage output, direct hits, area impact)**
- Header band: 16px band with a single 45-degree chevron notch cut from the right edge, 8px deep. The notch breaks the horizontal rule, implying forward force.
- Icon anchor: forward-pointing triangle (equilateral, 22px height), pointing toward the right edge of the card (toward the enemy). Triangle is the attack vector symbol.
- Edge treatment: right edge of the card has a 1px shadow line in rust color (~#8B3A2A), creating a directional visual weight toward the "attack direction."
- At-a-glance read: "Triangle on a notched band, weighted right — this card goes forward and hits something."

**Control Cards (positioning, debuffs, forced maneuvers on enemy)**
- Header band: 16px band with a horizontal slash extending 6px beyond the left card edge (a visual element that breaks the card boundary). Implies interference, disruption.
- Icon anchor: hexagon, 22px width. Hexagon is the constraint shape — it reads as a cage or boundary. No other card type uses hexagon.
- Edge treatment: left and right edges both carry 1px inset markers at the midpoint height — like binding brackets holding the card in place. The card looks like it has been clamped.
- At-a-glance read: "Hexagon on a band that breaks the border — this card constrains or repositions something."

**Repair Cards (hull restoration, subsystem recovery)**
- Header band: 16px band with a plus-sign mark (+) at center, 10px tall. The plus is the only header band symbol that is fully symmetric — implies restoration of balance.
- Icon anchor: rounded rectangle, 24×16px, 4px corner radius. The only icon anchor shape with a soft corner — softness implies restoration, not attack. This is a controlled exception to the no-curves rule.
- Edge treatment: bottom edge of the card has a 1px rule in ember orange (~#E8630A), 80% opacity. Ember orange is the game's only promise — Repair cards carry it because they are the only cards that restore what was lost. This is the only appearance of ember orange in the card system; it must not migrate to other card types.
- Selection compound: when a Repair card is selected, the 2px ember selection outline (§3.5 HUD Shape Exception #1) layers around the card perimeter, sitting *outside* the 1px Repair edge. The two rules do not overlap; the bottom of a selected Repair card reads as a slightly louder ember double-band. This compound is permitted (see §4.3 Ember Orange — Permitted Compound). Repair identity at greyscale must remain readable from shape + icon + header alone.
- At-a-glance read: "Plus-marked band, soft rectangle anchor, orange bottom edge — this card gives something back."

**Maneuver Cards (route adjustment, repositioning, speed changes)**
- Header band: 16px band with a diagonal slash from lower-left to upper-right corner of the band, dividing it into two triangles. The diagonal implies direction-change, not linear force.
- Icon anchor: diamond (square rotated 45 degrees), 20px per side. Diamond implies instability — the shape is balanced on a point, not flat-resting. Directional ambiguity is the maneuver card's semantic.
- Edge treatment: both top-left and bottom-right corners of the card have an additional 1px chamfer at 45 degrees, 6px long, cutting the card corners. The card itself is not square at these corners — it has been cut at an angle. This is the only card type with a modified perimeter shape.
- At-a-glance read: "Diagonal band, diamond anchor, cut corners — this card changes something about direction or position."

**Plating Cards (armor addition, subsystem fortification)**
- Header band: 16px band with two horizontal sub-rules (1px each, 2px apart, centered vertically in the band). The stacked rules imply layered plating bolted to a base. Distinct from the single-rule Precision band and the notched Assault band.
- Icon anchor: pentagon, point-up, 22px tall × 20px wide. Pentagon is the only five-sided anchor in the card system; its upward point reads as a shield edge deflecting a force from above. Matches the "bolted mass" silhouette rule from Heavy Truck (§3.1) — Plating reads as material *added*, not integrated.
- Edge treatment: left and right card edges each carry a 1px inset offset line at 25% height and 75% height — four short horizontal ticks per side, each 4px long, 1.5px inward from the outer rule. The ticks read as visible seams where plates have been bolted on. This is the only card type with inset edge ticks.
- Ember orange: FORBIDDEN on Plating cards. Ember orange is reserved for restoration semantics (Repair). Plating is addition, not recovery — the two must not share a color signal.
- At-a-glance read: "Pentagon on a double-ruled band, seamed edges, no ember — this card adds protection."

**Pillar anchor:** Pillar 1 (Vehicle as Character) — Plating cards are the player's active choice to add visible mass to the chassis. The pentagon anchor mirrors the player's ongoing "bolt things on" relationship with their vehicle.

---

#### Card Shape Learning Rule

The six card-type shapes (circle, triangle, hexagon, rounded rectangle, diamond, pentagon) must be introduced one at a time in the tutorial sequence. The first encounter with each type must be in a context where the player has time to read the card's static presentation before needing to play it. Shape reading is a skill the game teaches; it must sequence its curriculum.

---

#### Card Taxonomy: Mechanical Kind × Visual Archetype

Card classification has two orthogonal axes. The engine reads one; the artist reads the other.

- **Mechanical kind** (engine contract, Unity `CardKind` enum): What effect the card has on game state. Values: **Attack / Plate / Reposition / Repair.** This axis drives combat-loop logic and is the source of truth for rules questions.
- **Visual archetype** (art-bible shape grammar, this section): Which shape-language the card sprite uses. Values: **Precision / Assault / Plating / Control / Repair / Maneuver.** This axis drives sprite authoring and is the source of truth for visual questions.

**Permitted mappings:**

| Mechanical kind | Permitted visual archetype(s) | Selection rule |
|-----------------|-------------------------------|----------------|
| Attack | Precision OR Assault | Precision for single-subsystem targeted damage; Assault for general/AOE damage |
| Plate | Plating | 1:1 |
| Reposition | Maneuver OR Control | Maneuver when the player moves themselves; Control when the card forces an enemy to move |
| Repair | Repair | 1:1 |

**Cross-reference:** `design/ux/interaction-patterns.md` §3.1 Card mirrors this table. If either document changes, update both.

---

### 3.5 UI/HUD Shape Grammar

#### The HUD's Double Identity

The HUD operates as a **found-object layer** rather than a clean information overlay. It does not break the game's visual contract (everything is a physical object) — but it does impose a second constraint that world objects do not have: the HUD must be readable at combat speed, under visual stress, at any screen size. These two constraints (found-object aesthetic + instant legibility) are in direct tension.

The resolution: **the HUD uses the world's geometric vocabulary (angular, assembled, stamped) but holds to a stricter legibility budget than world objects.** World objects can be worn to the edge of readability; HUD elements cannot. A corroded road sign can be almost-legible. A subsystem health indicator must be always-legible.

---

#### HUD Frame Geometry

**Panel construction grammar:** All HUD panels are built from the same constructive logic — they look like they were cut from sheet metal and mounted to the screen's interior frame. Rules:

- Outer panel corners: 0-degree (sharp 90) on structural corners, 5–8px chamfer on user-facing corners (the corner you read from). The chamfer reads as "this edge was finished for contact; that edge was just cut."
- Panel borders: single-rule, 1.5px, tarnished steel grey (#7A7872). No shadow, no glow. The rule reads as a cut edge, not a design element.
- Panel fill: semi-transparent dark (base color at 75% opacity, #1A1814 at 80% opacity). Interior must pass 4.5:1 contrast ratio against all text and icon elements placed within.
- Panel separation: 4px minimum gap between adjacent HUD panels. Panels do not touch — they were mounted separately and the gap shows.

**Chassis status panel (left side, primary HUD zone):**
- Shape: L-bracket — a horizontal bar at top (8px tall) with a vertical leg extending down-left. This echoes the structural bracket language of the vehicle itself.
- Part-slot indicators within the panel: rectangular cells, 2px border, arranged in a vertical column. Damaged slots show a fracture mark (a single diagonal slash across the cell, 45 degrees, 1.5px in red #C0392B). Missing slots (part lost) show an empty cell with a dotted-border pattern — the slot exists but nothing fills it.

**Card hand panel (bottom, primary play zone):**
- Shape: horizontal tray — a flat rectangle with the top edge open (no top border rule on the hand tray itself — the cards sit in the tray, not inside a box). The tray has side end-caps (vertical 1.5px rules at left and right) but no lid.
- This makes the hand read as a physical tray from which the player selects, not a UI panel containing cards.

**Enemy status panel (right side, mirrored):**
- Same construction grammar as chassis status panel but mirrored. The mirroring is intentional — the player should be able to read both panels simultaneously as opposing poles. The enemy panel uses the same slot indicator grammar but cannot show a "missing slot" state (the player does not know what parts the enemy has lost until a specific card reveals it). *(Flag: confirm with game designer whether partial enemy damage state is intended information.)*

---

#### HUD Shape Exceptions

Three cases where the HUD explicitly breaks the found-object geometry to serve legibility:

1. **Active card highlight:** When a card in hand is selected, a 2px ember-orange (#E8630A) outline appears at the card perimeter. This is the only clean, unbroken geometric rule in the HUD. It must be immediately distinguishable from all other border treatments (all of which are grey-steel). The clean orange rule reads as "this is the live thing."

   **Compound case — selected Repair card:** A selected Repair card carries both the 2px outer selection outline (Commitment role) and the 1px inner ember bottom edge (Restoration role per §3.4). The two rules are geometrically additive: the selection outline sits *outside* the card perimeter; the Repair edge sits *on* the bottom of the card itself. They do not overlap. The bottom of a selected Repair card therefore reads as a slightly louder ember double-band, which aligns with player intent (a high-stakes commitment). Repair identity remains carried by header band (plus-mark), icon anchor (rounded rectangle), and back-of-card shape, none of which are obscured by the selection outline.

   **Visual test criterion (acceptance):** When a Repair card is selected, a player can still identify it as a Repair card without ambiguity from the shape language alone (greyscale render must pass — color removed, type still readable). If a greyscale render of a selected Repair card cannot be distinguished from any other selected card type by shape + icon + header alone, the test fails and the Repair edge treatment must be re-specified.

2. **Critical state alert (subsystem at 0 HP):** A fractured-fill pattern replaces the panel cell background (not an animation — a static fractured geometry, like a broken pane rendered as flat art). The fracture lines radiate from one corner of the cell at 25–35-degree angles. Three lines maximum; four reads as noise at combat speed.

3. **Turn counter:** A circular ring, 32px diameter, in the top-center HUD zone. This is the only full circle in the HUD. It is permitted because the turn counter must read as distinct from all rectangular panel geometry — the player must find it instantly without searching. The circle is the same tarnished steel grey; no color exception is needed.

4. **Non-card UI keyboard focus:** Any focusable non-card UI element (button, settings control, menu entry, focusable tooltip, dropdown) gets a 2px ash white (#E8E0D4) outline when keyboard focus lands on it. This is the only ash white perimeter rule in the game and is parallel in weight to the active card highlight (#1 above) — together they form a consistent "2px outline = the live thing" register across the entire interface, with the color carrying the *kind* of liveness: ember for card commitment, ash white for navigational presence. The two outlines never compose on the same element. See §4.3 ASH WHITE for the full color spec, the text-contrast guard, and the mouse-hover differentiation rule.

---

#### Encounter Type Signal Geometry (Standard vs. Ambush)

Encounter type is a binary signal that must read on three surfaces: the Map's node hover overlay (Node Map §I), the Combat HUD's EncounterType tag (Combat HUD ch1, §4 Zone 1, §5 D2), and any retroactive UI surfacing (post-combat summary). The signal carries via a non-color geometric mark so colorblind players and greyscale renders read it identically. (Closes accessibility requirement AD-C2.)

**Standard encounter (the absence-of-signal default):**
- Clean rectangular bounding edge on the encounter UI element (node hover container, EncounterType tag).
- No corner cut. No glyph prefix. Standard is the absence of decoration; it carries no positive shape mark.

**Ambush encounter (carries a positive shape mark on every surface):**
- **Single corner cut, top-left, 4px length at 45°.** This is intentionally distinct from Maneuver Cards (§3.4), which carry symmetric *two*-corner cuts (top-left + bottom-right). Ambush carries only the top-left cut. The asymmetric single cut reads as "the front of the form has been struck from ahead" — anchoring the spatial metaphor (Ambush always starts at `Position == Ahead` per Card Combat R15).
- **Chevron glyph prefix** (`>`, 8px wide × 6px tall, 1px stroke) on any text label carrying the Ambush signal (e.g., "Ambush Combat"). The chevron renders in the same color as the label — no color signal is loaded onto the chevron itself.
- For purely visual surfaces (node hover overlay without text), a single chevron glyph anchors the top-left interior of the bounding rectangle, 4px in from each edge, in tarnished steel grey `#7A7872`. Single chevron only; no animation, no pulse.
- The Combat HUD Ambush urgency tint (color hex + pulse curve, OQ-CH1 in `combat-hud.md`) is **additive** — it amplifies the Ambush signal but is never load-bearing. Removing the tint must never remove the Ambush signal.

**Greyscale acceptance test:** Render the Map and Combat HUD at full greyscale (color removed; values preserved). An Ambush element must remain distinguishable from a Standard element by corner cut + chevron alone. If greyscale removes the Ambush signal, the spec has failed.

**Cross-references:**
- `design/gdd/node-map.md` §I — hover preview surfaces this geometry on hostile-combat nodes.
- `design/ux/combat-hud.md` §4 Zone 1 + §5 D2 AmbushColdStart — EncounterType tag carries this geometry on frame 1.
- §3.4 Maneuver Cards — the two-corner symmetric Maneuver cut and the single-corner Ambush cut must remain visually distinct at icon size.
- `design/accessibility-requirements.md` §5 (Visual) and §11 AD-C2 — no-color-only compliance, AD-C2 retrofit closure.

---

### 3.6 Hero Shapes vs. Supporting Shapes

#### The Visual Hierarchy Contract

In every scene and screen, the player's eye must land in the correct order without being told where to look. The hierarchy is enforced through shape alone, before color is applied. This is tested by rendering scenes in greyscale and confirming that the hierarchy still reads correctly.

The correct landing order in every combat scene:

1. **Player vehicle** — largest coherent shape in the scene, lowest position in frame
2. **Enemy vehicle** — second-largest coherent shape, positioned to create vector toward player
3. **Active card** — isolated from hand, elevated slightly, brightest silhouette value
4. **Enemy status** — smaller than both vehicles, but higher in frame (scanning direction)
5. **Card hand** — present but recessed, read only when needed

---

#### The Hero Shape Rules

A shape is a hero shape if it must be found first. Hero shapes follow three rules:

**Rule 1: Hero shapes are closed.** The player vehicle, the enemy vehicle, and the active card are all fully closed silhouettes — no open edges, no bleed into background. Supporting shapes (HUD panels, environmental details, ground texture) are permitted to be open or implied. Closed vs. open is a pre-attentive signal; the eye resolves closed shapes first.

**Rule 2: Hero shapes are the largest shapes in their zone.** No supporting element in the same visual zone is permitted to be larger than the hero shape in that zone. A HUD panel that is larger than the player vehicle silhouette in any dimension is a failure state — the HUD has become visually dominant.

**Rule 3: Hero shapes carry the most irregular contour.** The player vehicle has the most complex perimeter in the scene — the most parts, the most angles, the most asymmetry from damage. This complexity is a pre-attentive signal: irregular contours draw the eye before smooth contours. World geometry and HUD panels must be simpler in perimeter complexity than the player vehicle. Environmental elements use the geometric-degraded vocabulary (angular but regular), which is deliberately simpler than the player vehicle's customized profile.

---

#### Supporting Shape Recession Rules

Supporting shapes (environment, HUD background panels, ground texture, UI decorative elements) must stay visually recessive through three simultaneous controls:

1. **Lower complexity:** Fewer distinct angles per element than the nearest hero shape
2. **Larger radius of curvature:** Environmental ground shapes use 150–300px arc radii — no sharp corners. This creates a smooth perimeter that recedes against the angular hero shapes.
3. **Grid alignment:** Environmental and HUD supporting shapes align to a 4px or 8px grid. Hero shapes are exempt from grid alignment — their irregular, part-assembled perimeters are permitted to fall off-grid. Off-grid shapes draw the eye; on-grid shapes recede.

**Pillar anchor:** Pillar 3 (Read to Win) — the player cannot make the correct play if they cannot find the relevant information at the correct moment. Visual hierarchy enforced through shape ensures that the player's scan path mirrors the decision order: vehicle states first, enemy state second, cards third.

---

## Section 4: Color System

> **Tuning note:** All hex values, percentage thresholds, and pixel measurements in this section are production starting points. They should be tested during development and adjusted based on what reads correctly on screen. When a value is revised during testing, update it here so the document stays current.

---

### 4.1 Primary Palette — Production Swatches

The base palette is the world at rest. These four colors appear in every game state, on every chassis, in every biome. They are never modified by theme — biome accents sit on top of them, they do not replace them.

**Swatch 1: Iron Oxide Red-Brown**
- Production hex: `#8B3A2A`
- Light variant (direct sun bounce): `#A8503C`
- Shadow variant (deep cast shadow): `#5C2218`
- Role: The dominant surface color of the world. Oxidized steel, clay earth, dried blood, corroded frame rails. Appears on structural chassis paint (base layer, pre-weathering), terrain ground planes, enemy vehicle primary hulls, and aged metal UI framing.
- Avoid on: health indicators, interactive UI elements. In UI contexts, this color appears only as inert border dressing and texture panels — never on anything a player should read as a state indicator.

**Swatch 2: Bleached Sand**
- Production hex: `#D4B896`
- Light variant (near-white highlight edge): `#EDD8BC`
- Shadow variant (warm mid-tone): `#B89A78`
- Role: Environmental fill — flat desert ground, dust, canvas-wrapped cargo, faded paint. Primary UI background tone for information panels and card backs. Use for: terrain fill, UI panel backgrounds, scrap piles, aged cloth/canvas, fog-of-war covered map areas.
- Avoid as: foreground color in combat UI — it reads as inactive or uncategorized.

**Swatch 3: Ash White**
- Production hex: `#E8E0D4`
- Light variant (bleached edge highlight): `#F5F0EC`
- Shadow variant (slightly cooler ash): `#D4CAC0`
- Role: The palette's near-white. Used for: highest-contrast text on dark surfaces, chassis name labels, silhouette edge highlights when vehicles cross a dark background, card text, UI labels. Pure white (#FFFFFF) is forbidden — it reads as a screen error or UI artifact in this art direction.
- Avoid as: decorative fill. Ash white is reserved for text legibility and edge highlight only.

**Swatch 4: Tarnished Steel Grey**
- Production hex: `#7A7872`
- Light variant (polished bare metal): `#9A9892`
- Shadow variant (deep tarnish): `#514F4A`
- Role: Bare metal, unpainted structural elements, exposed engine components, weapon barrels, bolt heads, hinges, mechanical connectors. Use for: secondary chassis surfaces (where paint has worn through), mechanical detail on parts, environmental metal objects, UI element outlines/dividers.
- Avoid as: background fill in UI — it reads as a disabled state.

---

### 4.2 Biome Accent System

Each biome has exactly one high-chroma accent. The accent earns its presence by making the biome recognizable without any text label. If removing the accent leaves the biome feeling generic, it is not doing enough work.

**Screen coverage rule (tuning target):** The biome accent must not exceed 15% of total screen surface area at any point during play. Measure this in the combat state.

**Forbidden surfaces for all accents:**
- Player vehicle primary paint
- Semantic state indicators (green/amber/red UI elements)
- Haven ember orange contexts
- Rarity tier indicators
- Card text and card backs
- Any functional UI element

---

**Biome 1: Cracked Earth — Brittle Amber**
- Production hex: `#C98A3A`
- Saturation target: ~65% HSL. Warm, dry, slightly burnt. Not fire-orange — dried-up amber, like old varnish or sun-baked resin.
- Approved surfaces: cracked earth terrain fracture lines, dried plant material, hazard markers for terrain hazards, biome-specific enemy faction markings (paint stripe or emblem only).

**Biome 2: Toxic Flats — Sickly Yellow-Green**
- Production hex: `#8AAB2C`
- Saturation target: ~72% HSL. The only cool-leaning accent in the first two biomes; the only hue that implies organic contamination rather than weathering.
- Approved surfaces: toxic pool fills, corroded metal, hazard zone markers, enemy faction markings.
- Colorblind flag: Yellow-green is problematic under deuteranopia/protanopia. Toxic hazards must carry a secondary cue (skull/warning icon, animated surface disturbance, or distinct value contrast).

**Biome 3: Ruins — Cold Concrete Blue**
- Production hex: `#4A6880`
- Saturation target: ~35% HSL. Desaturated blue-grey — not water, not sky, but the specific color of old concrete and shadowed rubble. Lives in shadow specifically: wall shadows, structural overhangs, cavities.
- Approved surfaces: shadow fill on ruin structures, crumbled concrete detail, structural debris, enemy faction markings, sky/horizon fill.

**Biome 4 (Post-launch): Haven Approach — Ember Orange**
- Production hex: `#E8630A`
- This is Haven's color. See Section 4.3 for restrictions.

---

### 4.3 Semantic Color Vocabulary

Semantic colors are the game's communication system. They are never used decoratively. A color that means "functional" in combat UI must never appear as a decorative trim color on a card back.

**GREEN — Functional / Contributing**
- Production hex: `#4A8C52` / Light variant: `#6AAE72`
- Application: Subsystem health indicators at full operating capacity. Stat bonuses being applied. Parts confirmed as installed and contributing. Repair card outcomes.
- Value + fill redundancy: Green = lighter overall value + solid fill. Even without the hue, a functional subsystem indicator has more visual weight (lighter, filled) than a degraded one.

**AMBER — Degraded / Reduced Capacity**
- Production hex: `#D08010` / Light variant: `#F0A030`
- Application: Subsystem health indicators when at least one damage state has been applied. Stats that are active but reduced. Parts that are damaged but still contributing.
- Value + fill redundancy: Amber = mid-value + hatched/dashed fill pattern. The fill pattern is required — it is the non-color signal that reinforces degradation.
- Note: Semantic amber has been shifted to `#D08010` (more saturated orange-yellow) to create hard separation from Biome 1's brittle amber (`#C98A3A`). Test both side by side in combat UI context to confirm separation holds.

**RED — Critical / Offline**
- Production hex: `#B03030` / Light variant: `#D04040`
- Application: Subsystem health indicators when offline (not contributing). Hull integrity critical threshold. Card effects that cause forced damage states.
- Value + fill redundancy: Red = dark value + fractured/broken fill pattern (a broken line or shattered-segment pattern — the fracture motif reinforces "broken"). Three fracture lines maximum; four reads as noise at combat speed.

**EMBER ORANGE — Direction / Restoration / Commitment**
- Production hex: `#E8630A`
- Application: Navigation toward Haven on the map. Haven UI elements. Haven arrival sequence. Repair card bottom edge (the game's only card-system use). Active card highlight outline in combat HUD. Multi-reward selection outline. Turn counter pulse on increment.
- **Unified meaning: "this is where the player's intent lives — progress toward a destination, recovery of what was lost, or commitment to an action about to resolve."** Every use of ember orange is one of three instances of this meaning:
  - **Direction** — Haven map icon, player position marker, Haven environment (Biome 4): the player is moving toward something that matters.
  - **Restoration** — Repair card bottom edge: the card returns what the run has taken.
  - **Commitment** — Active card highlight, multi-reward selection outline, turn counter pulse: the player has chosen, and the game is about to act on that choice.
- These three roles are not three different meanings — they are three surfaces of the same promise. No fourth meaning is permitted. Any new mechanic requesting ember orange must map to Direction, Restoration, or Commitment, or escalate to Art Director review.
- **Permitted compound:** A selected Repair card layers Commitment (2px outer outline) over Restoration (1px inner bottom edge). The compound is additive, not conflicting — see §3.5 HUD Shape Exceptions for the visual test criterion.

**ASH WHITE — Information / Neutral Text / Non-Card Focus**
- `#E8E0D4` (see primary palette)
- Application:
  - All primary UI text on dark surfaces. The neutral information color — it carries no semantic charge.
  - **Non-card UI keyboard focus indicator** — 2px outline at full value (#E8E0D4, 100% opacity) on any focusable non-card element (buttons, menu entries, settings controls, dropdowns, focusable tooltips). The focus rule matches the card selection rule (§3.5 HUD Shape Exception #1, ember 2px) in weight but uses ash white because non-card UI focus is *not* Commitment (committing a card to play) — it is **navigational presence**: "the keyboard cursor is here." Ember and ash-white 2px outlines never compose on the same element; cards always use ember (Commitment), non-card UI always uses ash white (presence).
- **Text-contrast guard:** When a focused element contains ash white text, the 2px ash white outline must be separated from any text glyph by ≥4px of inner padding so the outline reads as a frame, not as a text-adjacent rule.
- **Mouse hover ≠ keyboard focus.** Mouse hover on the same element uses a 1px ash white inner highlight (60% opacity) rather than the 2px outline. The 2px outline is reserved for keyboard focus so that input modality is readable from the screen.

**DEEP TARNISH — Absence / Destroyed**
- Production hex: `#1C1A18`
- Application: Parts fully destroyed. Shadow fills at maximum depth. Silhouette cutouts. Empty chassis slots.
- This is a warm dark — not pure black (#000000), which would read as a UI void or rendering error.

**NO OTHER SEMANTIC COLORS PERMITTED.** If a new mechanic requires a color signal, it must be assigned a meaning from this vocabulary or escalate to Art Director review.

---

### 4.4 UI vs. World Palette Boundary

**UI-ONLY COLORS** (never appear as world/environment colors):
- Semantic green `#4A8C52` — reserved for operational state indicators
- Semantic amber `#D08010` — reserved for degraded state (shifted from `#C8841A` for clear separation from Biome 1 brittle amber `#C98A3A`)
- Semantic red `#B03030` — reserved for critical/offline state
- Deep black `#1C1A18` as filled shapes (only applies in UI for empty slot indicators)

**WORLD-ONLY COLORS** (never appear in functional UI elements):
- Biome accents in their pure form. Exception: a biome accent may appear as a thin 1–2px decorative strip on the information panel frame to communicate current biome at a glance — this is the only permitted crossover.
- Iron oxide red-brown `#8B3A2A` as a filled UI shape — may appear as texture/material only, never as a state signal.

**SHARED COLORS** (appear in both layers, with different roles):
- Ash white `#E8E0D4` — world: edge highlights; UI: text
- Tarnished steel grey `#7A7872` — world: bare metal; UI: divider lines, outline borders
- Ember orange `#E8630A` — world: Haven environment (Biome 4 only); UI: Haven navigation, active card highlight, Repair card bottom edge
- Bleached sand `#D4B896` — world: terrain fill; UI: panel background tone

**The diagnostic question for any new asset:** "If a player sees this color, is there any scenario in which it creates a false state read?" If yes, the color is in the wrong layer.

---

### 4.5 Chassis Color Differentiation

The three chassis must read as different vehicles before shape recognition is complete. Color provides the first read; silhouette confirms it. All chassis primary paint is drawn exclusively from the base palette — no biome accents, no semantic signal colors, no ember orange.

**Scout — Cold Iron**
- Primary paint: `#6A7878` — desaturated cool grey-green, slightly blue-green cast. Temperature: cool.
- Worn surface: paint thins to reveal `#7A7872` (tarnished steel) at edges and impact points.
- Character read: somewhere cold and distant. The cool cast separates it from the warm-brown world, implying speed and range.
- Note: Scout `#6A7878` and Biome 3 cold concrete blue `#4A6880` are in the same family. In Biome 3, Scout may partially merge with shadowed backgrounds — the silhouette (blade shape, low profile) is the primary differentiator at that point.

**Assault — Burnt Iron**
- Primary paint: `#7A4A38` — warm, dark red-brown, deeper than world iron oxide, slightly purple-shifted. Temperature: hot-dark.
- Worn surface: paint thins to reveal `#5C4238` (deep aged metal) at edges.
- Character read: run hot, repeatedly. The deeper warm tone reads as heat damage and aggressive use.

**Heavy Truck — Raw Scale**
- Primary paint: `#58524A` — deepest value in the chassis range. Very dark warm grey, almost black but not quite. Temperature: neutral-dark.
- Worn surface: larger bare metal patches than other chassis — maintenance is secondary to function.
- Character read: was never elegant and was never meant to be.

**Chassis differentiation test:** Place Scout, Assault, and Heavy Truck at 48×48px icon size in greyscale. Scout should be the lightest value. Heavy Truck should be the darkest. Assault should sit mid-value with the warmest hue. If the value ladder is not clear in greyscale, chassis reads are not yet differentiated — adjust values before locking.

---

### 4.6 Colorblind Accessibility

Assume 8% of the player population has color vision deficiency. The following semantic pairs are at risk; the backup cue for each is mandatory.

**Risk 1: Green vs. Amber — Deuteranopia / Protanopia**
- Backup cue (primary): FILL PATTERN. Functional (green) = solid fill. Degraded (amber) = hatched/dashed fill. Fill patterns are implemented before the color layer.
- Backup cue (secondary): VALUE. Green is lighter than amber. Minimum 15% luminance difference required in greyscale.

**Risk 2: Amber vs. Red — Tritanopia**
- Backup cue (primary): FILL PATTERN. Degraded (amber) = hatched/dashed. Offline (red) = fractured/broken. The broken-line pattern must be visually distinct from the hatched pattern — test at screen resolution.
- Backup cue (secondary): ICON. All offline subsystem states show a small fracture-mark icon overlay on the indicator.

**Risk 3: Green vs. Red — Deuteranopia / Protanopia**
- Backup cue: FILL PATTERN (as above) covers this pair. Solid fill = functional; fractured fill = offline.
- Backup cue (secondary): VALUE. Minimum 25% luminance separation between functional and offline in greyscale.

**Risk 4: Toxic Yellow-Green Biome Accent vs. Semantic Green**
- Backup cue: CONTEXT BOUNDARY. Biome accents never appear inside the UI frame. Semantic colors never appear in world geometry. Spatial separation is the primary disambiguator.
- Backup cue (secondary): HAZARD ICON. All toxic hazard zones carry a skull or warning icon that is not color-dependent.

**Global rule:** Run the full game UI in a colorblind simulator (deuteranopia, protanopia, and tritanopia) before any build milestone. All three primary semantic states must be distinguishable under all three deficiency types using fill pattern alone.

---

### 4.7 Rarity Color System

Rarity color lives on the card border/frame and the part tooltip border only — not on the card background or the part sprite itself. This contains the rarity system to a specific UI region.

**Rarity Tier 1: Common**
- Production hex: `#7A7872` (tarnished steel grey — borrowed from base palette)
- Visual read: A common find has no extra color. Common is the absence of rarity signal.

**Rarity Tier 2: Uncommon**
- Production hex: `#3A6878` — desaturated cool blue-grey. Reads as intentional without being spectacular.
- Colorblind check: Reads as distinguishably lighter and cooler than steel grey under all color deficiencies.

**Rarity Tier 3: Rare**
- Production hex: `#8A5A1A` — deep amber-brown, richer than bleached sand, warm metalite quality. Like old brass or aged gold without shimmer.
- Does not overlap with semantic amber `#D08010` — rare amber is less orange-saturated, reading as aged metal, not a warning state.

**Rarity Tier 4: Legendary**
- Production hex: `#C87820` + edge glow variant `#E09840`
- Visual treatment: border shifts between the two variants in a subtle pulse — one full cycle over ~3 seconds. This is the only animated element in the loot UI. No other rarity tier animates.
- **Reduce Motion fallback:** When the player has Reduce Motion enabled (`design/accessibility-requirements.md` §3.3), the pulse is replaced by a **static composite border** — 1px outer rule at `#C87820` + 1px inner rule at `#E09840` — preserving the two-tone amber-gold register without animation. Rarity differentiation from Rare (`#8A5A1A`) is carried by value (Legendary is brighter) AND by the composite double-border (no other rarity tier uses a composite border). Legendary therefore remains the visually distinct top tier under both default rendering and Reduce Motion.
- Note: Legendary amber-gold `#C87820` must be tested against semantic amber `#D08010`. Differentiation is value (legendary is lighter, more gold-shifted) and context (legendary only on card/part borders; semantic amber only on subsystem indicators).

**Rarity color ladder (distinctiveness order):**
1. Legendary: `#C87820` (most prominent — only animated element)
2. Rare: `#8A5A1A`
3. Uncommon: `#3A6878`
4. Common: `#7A7872` (baseline world material — absence of rarity signal)

---

### Production Color Cheat Sheet

**World surface:** `#8B3A2A`, `#D4B896`, `#E8E0D4`, `#7A7872` and variants.
**Biome environment detail:** Single accent per biome. No accent on player vehicle.
**Subsystem functional:** `#4A8C52` + solid fill.
**Subsystem degraded:** `#D08010` + hatched fill.
**Subsystem offline:** `#B03030` + fractured fill.
**Haven elements (Biome 4 only) / Repair cards / Active card highlight:** `#E8630A`.
**Scout chassis:** `#6A7878`. **Assault chassis:** `#7A4A38`. **Heavy Truck:** `#58524A`.
**Rarity borders only:** Common `#7A7872`, Uncommon `#3A6878`, Rare `#8A5A1A`, Legendary `#C87820` (animated pulse).
**Text on dark surfaces:** `#E8E0D4` only. Never pure white `#FFFFFF`.
**Empty slots / destroyed parts:** `#1C1A18`.
**Accents capped at 15% screen area. Ember orange forbidden in Biomes 1–3 world elements. No semantic color used decoratively. Ever.**

---

## Section 5: Character Design Direction

> **Tuning note:** All proportions, pixel heights, damage thresholds, and screen-space percentages in this section are starting targets. Every value is subject to revision during playtesting. Flag changes against the three core principles (Trophy Silhouette, Scarcity Palette, Color as Operational Signal) before accepting adjustments.

---

### 5.1 Vehicle as Character — Visual Identity Across the Run

**Design intent:** The player vehicle is a visual diary — its surface condition at any moment tells the story of decisions made, damage survived, and resources spent or withheld.

#### Damage State Progression

Wear is **cumulative and non-resetting within a run.** Haven stops do not restore the chassis sprite. They may restore HP values, but the visual history remains until a full repair action is explicitly taken. This means a player who arrives at the final boss with a battered Scout has a visually different vehicle from one who conserved repairs. The run's cost must be readable on the hull.

Four damage milestone states govern chassis sprite selection. Each state is a distinct sprite layer or swap — not a tint or filter.

**State 0 — Operational (0% damage taken, or fully repaired at Haven)**
Clean factory lines on the base chassis color. No surface breaks. Panel seams are present but tight. Color sits at its fully saturated base value. No asymmetry. The vehicle looks like something someone still cares about.

**State 1 — Scratched (first combat contact through ~30% cumulative hull stress)**
Horizontal scratch marks appear along the forward-facing surfaces. Directional — angled toward the front to imply motion and incoming fire, not random. No missing geometry. Panel color is unchanged. The vehicle reads as used, not broken.

**State 2 — Dented (30%–60% cumulative hull stress)**
One or two panel faces show shallow deformation — a corner pushed inward, an edge that no longer sits flush. The chassis silhouette remains fully intact; deformation lives inside the bounding shape. Surface color begins to show bare-metal exposure at deformation points: a thin streak of `#7A7872` where paint or plating has cracked. Asymmetry begins here — deformation does not mirror across the chassis.

**State 3 — Breached (60%–85% cumulative hull stress)**
A panel is visibly compromised: one section shows an open gap, a hanging fragment, or a torn-away corner that breaks the chassis silhouette slightly (under 10% silhouette deviation from State 0). Bare metal and internal shadow are visible through the breach. Hatching fills on any status indicators for subsystems in this zone. Visible asymmetry now reads as structural, not decorative.

**State 4 — Critical (85%+ cumulative hull stress)**
Multiple panels breached. The chassis silhouette is noticeably irregular — up to 20% deviation from State 0 at its worst points. Darkened scorch marks (deep shadow tone `#2A2420`) appear around breach points. Internal framing is implied through gaps. The vehicle reads, without any UI, as something held together by will rather than engineering. Spark/smoke VFX may be applied here — delegated to technical-artist under VFX specs in Section 8.

*(Note: the 30/60/85% thresholds are starting targets to be validated against run pacing during playtesting. Confirm with game designer before authoring sprites to these breakpoints.)*

#### Part-Loss Visibility Rule

When a part is lost, the chassis sprite swaps to a variant that removes the part geometry and replaces it with evidence of absence:

- **Weapon parts lost:** The mounting point remains (bracket, bolts, torn cable) but the weapon geometry is absent. The mount scar is the visual — it communicates "something was here." Do not leave the slot visually clean or empty. Absence with evidence is legible; clean absence reads as a design error.
- **Armor/support parts lost:** The panel is gone. The layer beneath (internal framing or bare chassis surface) is exposed. Use `#58524A` raw-metal tone for exposed internals.

Part-loss variants are authored as separate sprite layer swaps, not as runtime deletion of sprite components. EA scope (Scout and Assault) requires at minimum two part-loss variants per chassis per subsystem zone.

**Cumulative wear reset rule:** Visual wear resets ONLY when a player spends a "Full Restore" resource at a Haven node. A standard Haven repair (HP restore) does not change the sprite state. The visual history is the point.

#### Haven Arrival Communication

When the player arrives at Haven, the current chassis sprite state is the run's report card. No dedicated damage summary screen is required — the vehicle communicates it. The Haven background (ember orange ambient) must provide enough contrast from the chassis to make damage states clearly readable. Ember orange must NOT appear on the damaged chassis sprite — it is environment-only.

**Design test:** If the chassis silhouette is still completely unbroken, it is State 1. State 2 begins at first deformation, not first breach.

---

### 5.2 NPC Visual Direction

**Design intent:** NPCs are environmental signals, not playable entities — they establish faction, tone, and transaction type through a single glance, then step aside for gameplay.

#### Presentation Format

NPCs are presented as **bust portraits in a sidebar panel** — face and upper torso, three-quarter angle (approximately 30° turn from forward-facing). Recommended portrait size: 180×220px at base resolution. NPCs are positioned in the information panel, not overlapping the vehicle display or route map. They do not occlude gameplay.

> **Production note — internal authored only.** NPC portrait character design (demographics, proportions, specific features) is not specified here at outsource level. These assets will be authored iteratively in-house with the art lead, tested for archetype legibility across variants, and locked through playtesting. Do not commission NPC portraits from external contractors without a separate character brief.

Background within the portrait frame is flat — the environmental color palette of the current biome node, darkened 30% to push the figure forward.

No full-figure character sprites are authored for EA. If an event calls for an environmental figure (implied presence), use an off-frame implication: a shadow, a hand, a piece of gear partially in frame. Full-figure NPC sprites are post-launch scope.

#### NPC Archetypes

**Merchant**
Visual register: pragmatic, resourced, self-protective. Clothing and equipment show signs of maintenance, not wealth. Use warm-neutral tones: base palette sand (`#D4B896`) and steel grey (`#7A7872`) dominate. One distinguishing object in frame — a worn satchel clasp, a mounted scope, a ledger — that reads as their identifier across multiple encounters. The Merchant should feel like a recurring face, not a generic vendor.

**Event Figure**
Visual register: ambiguous alignment. Tone shifts per event type but the portrait format is consistent. Silhouette uses found-object rules: worn wrapping, improvised gear, no military uniformity. Biome tones apply. No repeated asset across different event types; each event category gets one unique portrait per biome.

**Boss Operator**
Visual register: deliberate threat. Two visual signals required: scale (framing implies larger-than-usual presence) and singularity (one clear, memorable visual identifier — a fused visor, a specific scar pattern, non-standard equipment). Boss operators use slightly higher contrast than other NPCs — shadows are deeper, edges are sharper. Boss operators may carry a faction accent color in one deliberate element (see Section 5.3 for faction color rules). No other NPC type carries faction color in the portrait.

**EA scope:** 3 Boss Operators total (one per biome), each with a unique portrait and visual identifier.

#### NPC Silhouette Philosophy

NPCs follow the found-object rule in spirit but operate at a different register from vehicles. Vehicles are primarily geometric and machine-derived. NPCs are organic and improvised-textile-derived. The found-object quality in NPC design comes from material layering (wrapped fabrics over rigid elements, improvised fastenings) rather than chassis geometry. Silhouettes must be readable as distinct NPC types at portrait crop.

**Design test:** Cover everything below the collar. If the face and upper-shoulder silhouette alone communicate the NPC's archetype and general alignment register, the design is working. If the face reads as generic and you need the gear to communicate type, simplify the face or strengthen the upper silhouette.

---

### 5.3 Enemy Vehicle Visual Direction

**Design intent:** Enemy vehicles must communicate faction membership, threat tier, and behavioral archetype before any UI label appears — purely through shape, color deviation, and construction logic.

#### Enemy vs. Player Visual Differentiation

Player vehicles follow controlled construction logic: parts are mounted with evident purpose, damage states communicate history, the chassis has internal coherence even when worn. Enemy vehicles use a different construction logic: **enemy vehicles have visible external modification** that player vehicles do not carry.

External modification reads as: armor plating welded over existing geometry, improvised weaponry with rough mounting brackets, non-standard panel shapes that create jagged negative space in the silhouette.

Player vehicle chassis colors (cold iron, burnt iron, raw scale) are not used by enemies. Enemy factions have their own color allocation:

- **Raider faction:** Corroded copper `#8B6040` — warm, reddish, oxidized. Improvised, high chaos silhouette.
- **Salvager faction:** Matte ash `#6A6860` — near-neutral, dense. More structured than Raiders but still asymmetric.
- **Faction 3 (post-launch):** Reserved.

#### Faction Membership in Vehicle Design

Faction membership is communicated through two channels — both must be present:

1. **Chassis base color** (as defined above)
2. **One recurring structural motif** per faction — a shape pattern on every vehicle of that faction in a consistent location. For Raiders: stacked scrap shielding on the forward face. For Salvagers: cable bundles routed along the chassis spine. Must be visible at combat mid-field zoom. No faction icons or decals — the structural motif IS the faction signal.

#### Boss Vehicle Rules

Three required visual signals on every boss vehicle:

1. **Scale deviation:** Boss vehicles are authored at 130–150% of a standard enemy vehicle's pixel height at the same camera distance. Author the sprite at boss scale — do not runtime-scale a standard sprite.
2. **Silhouette complexity:** One non-repeating silhouette element — a protrusion, an elevated superstructure, an asymmetric mounted weapon — that breaks the chassis bounding box in a way standard enemies do not. Must read at 64×64px. This is the boss's visual signature.
3. **Color intensity:** Boss vehicles use the faction base tone at full saturation, not the slightly desaturated application standard enemies use. The boss vehicle looks like the faction's ideal expression.

**EA scope:** 3 distinct boss vehicles (one per biome). Each requires a unique silhouette signature authored at boss scale.

#### Enemy Damage State Visibility

Enemy damage states are visible at reduced fidelity — two states only:

- **Undamaged:** Full silhouette, base faction tone.
- **Damaged (below 50% HP):** One panel visibly breached, hatching on the breached zone, ~10–15% desaturation of chassis base tone. Communicates "this enemy is killable" without revealing precise HP.

Enemy damage sprites require two variants per chassis type, not four.

**Design test:** Cover the color and look only at the silhouette. Raiders should read as chaotic accumulation — vertical protrusions, uneven stacking. Salvagers should read as horizontal compression — dense, low-profile, cable-laced. If both read the same from silhouette alone, one design needs revision.

---

### 5.4 Expression and Pose Philosophy

**Design intent:** In the absence of faces, vehicle stance and asymmetry carry all emotional weight — orientation and structural integrity must do the work that posture and expression do in character-based games.

#### Vehicle Stance as Emotional Register

**Scout:** Undamaged, the 3:1 blade reads as coiled — a shape about to move. This is the Scout's confident register. As damage accumulates (front-facing surfaces deform first), the low blade begins to read as crouched rather than coiled. A State 3–4 Scout reads as exposed. No additional animation layer needed — the damage state sprite produces this.

**Assault:** The 2:1 trapezoid is mass-forward and blunt. Damage states must not make it read as small — the Assault should read as a damaged immovable object, not a shrinking one. Breach deformations are positioned toward the rear panels; the blunt forward face holds integrity longest.

#### Animation Philosophy

**EA stance: primarily static sprites, with a small defined set of micro-animations.**

Micro-animations in scope for EA:
- **Idle loop (player vehicle, Haven node only):** 2–3 frame exhaust or heat-shimmer at a defined emission point. Maximum amplitude: 2px.
- **Combat entry:** A single 1-frame offset sprite suggesting the vehicle lurching into position. Not a tweened animation.
- **Critical state (State 4 only):** Spark/smoke VFX at breach points — delegated to technical-artist. The sprite is static; life comes from particle effects.

No continuous idle loops for enemy vehicles in EA. No chassis tilt, bounce, or secondary motion.

#### Asymmetry as Storytelling

Part-loss and advanced damage states produce asymmetry the player reads narratively. A Scout that has lost its left-side weapon mount and is in State 3 on its forward panels tells a story without any caption. This emerges from the damage and part-loss sprite rules. The design discipline required is preservation: do not over-symmetrize damage sprites in the name of visual tidiness. The asymmetry is intentional.

**Design test:** Does the asymmetry follow a legible cause? If the missing panel is on the side that received the most lateral fire, the asymmetry is causal and reads correctly. If damage is distributed symmetrically as a default because it looks balanced, the damage sprite is wrong.

---

### 5.5 LOD and Camera Distance Philosophy

**Design intent:** Each zoom level is a contract — what detail the player can see determines what detail must be authored, and every authored detail must serve the level at which it is visible.

#### Zoom Level Definitions

**Level 1 — Map Icon (24×24px)**
Required: Chassis silhouette only. Trophy silhouette rule applies in full. Faction color or player chassis color applies. No surface detail, no damage state detail, no part geometry.
Damage states: Binary — intact icon vs. one "damaged" icon variant.
**Author separately at 24×24px — do not downscale the combat sprite.**

**Level 2 — Combat Mid-Field (master canvas system — see below)**
Required: Full chassis silhouette with internal panel geometry legible. All four damage states readable and distinct. Part-loss variants readable. Faction structural motif visible.
**This is the primary authored asset. Author it first.**

**Level 3 — Full Detail (128×128px)**
Context: Vehicle inspect screen or Haven arrival display.
**Status: Deferred to post-launch.** The combat sprite is self-sufficient for all in-run visual communication. The inspect screen adds significant art scope that is not justified for EA. Revisit when scope allows.

#### Master Combat Canvas System

All chassis are authored within a shared **256×128px master canvas**, anchored at the rear-wheel contact point (bottom-center). Vehicles are different sizes within this canvas — Scout is approximately half the pixel area of Heavy Truck. This size difference is intentional: the vehicle's visual footprint communicates its strategic weight before any stat is read.

| Chassis | Authored Size within Canvas | Ratio | EA Status |
|---|---|---|---|
| Scout | ~128×48px | 3:1 blade (low, long) | In scope |
| Assault | ~192×80px | 2.4:1 wedge (medium) | In scope |
| Heavy Truck | ~240×128px | ~2:1 block (fills canvas) | Post-launch |
| Enemy variants | Proportional to chassis analogue | — | In scope |
| Boss variants | +30–50% of chassis authored size | — | In scope |

All vehicles are placed on the same 256×128px canvas for runtime positional consistency. Empty canvas space around smaller vehicles is transparent. Animations (wheel rotation, suspension bounce) operate within the authored sprite bounds.

**48×48px silhouette test:** remains as the legibility gate. The test uses a thumbnail downscale of the combat sprite — not a separately authored asset. If the silhouette fails the test at thumbnail, fix the combat sprite.

#### Sprite Authoring Summary

| Zoom Level | Canvas / Authored Size | Damage States | Part-Loss Variants | EA Status |
|---|---|---|---|---|
| Map Icon | 24×24px (authored separately) | 2 (intact / damaged) | None | In scope |
| Combat Mid-Field | 256×128px canvas; per-chassis sizes above | 4 | Per subsystem zone | In scope — author first |
| Full Detail | 128×128px | 4 | Per subsystem zone | Deferred to post-launch |

#### Pixel Density Discipline

All vehicle sprites authored at 1:1 pixel ratio — no sub-pixel anti-aliasing, no fractional positioning. Chassis edges are clean pixel edges. Scratches and damage are pixel-precise. Scorch marks use stippling, not blur.

Color depth: All sprites authored in the defined palette plus a maximum of three intermediate mixing tones per sprite. Every color on a vehicle sprite must be either a named palette value or a documented derivation of one.

**Design test:** Can a player identify (a) the chassis type, (b) the damage state, and (c) one additional distinguishing piece of information (faction, part presence, weapon type) within two seconds of first seeing the combat sprite? If yes, detail level is correct.

---

### 5.6 VFX Visual Language

> **Production note:** This section defines the VFX vocabulary before any VFX work begins. All VFX must be authored within the constraints below — no exceptions for "it looked cool." The RUST ICON aesthetic is most easily violated by VFX, because particle systems and glow effects naturally drift toward contemporary game conventions (soft bloom, chromatic glow, screen-space distortion). None of those belong here.

---

#### 5.6.1 Absolute Prohibitions

The following are forbidden in all VFX across all game states:
- **Bloom or glow** — no soft, radiating light emission from any particle or sprite. Glow is a manufacture aesthetic; this world has no manufactured quality.
- **Chromatic aberration** — no lens fringing, no color channel separation.
- **Screen-space distortion** — no heat shimmer distortion, no lens refraction, no ripple effects.
- **Smooth opacity gradients** — no soft-edged, feathered particles. Particle sprites use hard pixel edges with point filter, consistent with all other sprites.
- **Particles outside the palette** — no VFX color may appear that is not a named palette value or a documented derivation. New hue introductions for VFX are not permitted.

---

#### 5.6.2 General VFX Palette Constraints

| VFX type | Permitted palette range | Prohibited |
|---|---|---|
| Dust / dirt | Bleached sand (`#D4B896`), ash white (`#E8E0D4`), tarnished steel grey (`#7A7872`) | Any saturated hue |
| Sparks / metal | Ash white (`#E8E0D4`), steel grey (`#7A7872`), iron oxide (`#8B3A2A`) at low count | Bright yellow, white glow |
| Fire / ember | Ember orange (`#E8630A`), iron oxide (`#8B3A2A`) — Haven and critical state only | Red glow, orange bloom |
| Impact flash | Ash white (`#E8E0D4`) — 2–3 frame burst maximum | Sustained glow, chromatic ring |
| Card play aura | Family hue at 50% opacity max, 1–2 frame flash only | Persistent glow, multiple hues |
| Legendary pulse | `#C87820` border animation (existing rarity system) — not a particle effect | Screen-wide flash, bloom |

---

#### 5.6.3 Particle Sprite Standards

- **Authored size**: 8×8px to 32×32px. No particle sprite exceeds 32×32px.
- **Filter mode**: Point — consistent with all other sprites.
- **Shape**: Hard pixel shapes (circle, oval, shard, square) — no feathered or soft-edge sprites.
- **Opacity curve**: Step function or fast linear falloff only. Smooth sigmoid opacity curves produce the soft-fade convention this aesthetic rejects.
- **Color palette compliance**: Each particle sprite authored in the defined palette. No gradients baked into particle sprites — use alpha channel only, with point filter, for a hard cutout read.

---

#### 5.6.4 Per-System VFX Specs

**Rear tire dust (combat, continuous)**
- Emission: continuous during vehicle motion on rail, both vehicles
- Direction: leftward, slight downward angle (~15° below horizontal)
- Sprite: 8×8px and 16×16px dust puffs, bleached sand and ash white
- Lifecycle: 0.4–0.8s, fast expand (linear scale ×1 → ×2), hard-step opacity fade (100% → 0% at 70% lifetime, no taper)
- Layer: behind both vehicles in scene depth order
- Intensity multiplier: ×2.5 during position swap animation (300ms burst)

**Impact / hit flash**
- Triggered by: card resolving damage on target vehicle
- Sprite: 2–4 pixel-shard sprites (4×4px each), ash white, radiating from impact zone on chassis
- Duration: 3 frames at 12fps (250ms total). Frame 1: all shards at origin. Frame 2: shards at max travel (8–12px from origin). Frame 3: shards at 60% opacity. Frame 4: none.
- No sustained glow after the flash frames.

**Sparks (critical damage state entry)**
- Triggered by: chassis crossing into State 3 (85% HP threshold)
- Sprite: 2×2px and 4×4px shard sprites, ash white and steel grey
- Emitter: pinned to breach-point zone on chassis (authored per-chassis, not generic)
- Emission rate: irregular burst every 1.5–3s (random interval), 4–8 particles per burst
- Lifecycle: 0.6s, arc trajectory with gravity (parabolic, leftward bias matching road direction)
- Continuous until state changes or run ends

**Card play highlight (card selection)**
- Trigger: card enters Selected state (lifted 4px from hand)
- Effect: family-hue tint at 40% opacity applied to card sprite for duration of hover. No particle emission. No glow. Removed immediately on deselect.
- This is a sprite tint, not a VFX particle effect.

**Legendary card reveal**
- Effect: the existing rarity border pulse (`#C87820` ↔ `#E09840`, 3s cycle) — no additional VFX.
- If a Legendary card enters the reward pool, the border pulse begins on reveal. No screen flash, no special emission.

**Position swap dust burst**
- Already specified in Section 7.1 (Section 7 combat layout): dust intensity ×2.5 during 300ms swap.
- VFX note: this is an emission rate multiplier on the existing rear-tire dust system — not a separate explosion effect.

---

#### 5.6.5 VFX States Where Effects Are Permitted

| Game state | Permitted VFX |
|---|---|
| Combat — idle | Rear tire dust (continuous) |
| Combat — card resolve | Impact flash, card play highlight |
| Combat — critical entry | Sparks (continuous from breach point) |
| Combat — position swap | Dust burst (×2.5 intensity, 300ms) |
| Combat — victory | No new VFX — preserve existing dust; chassis visible with all damage |
| Haven arrival | Ember fire/lantern flicker (diegetic environment, not particle — authored as animated sprite) |
| All other states | No VFX |

---

## Section 6: Environment Design Language

> **Tuning note:** All density values, resolution targets, and prop counts listed below are starting targets established before in-engine testing. Treat every number as a calibration stake, not a constraint. Revisit each value at first playable build and after each biome's first combat pass.

---

### Design Intent

The environment is not scenery. It is evidence. Every biome, every node, every prop is a legible record of what was built, what failed, and what survived. The player reads the world the same way they read a vehicle — looking for what still works.

---

### 6.1 Architectural Style and World History

**Design intent:** The built environment communicates a single coherent collapse — one industrial civilization at three stages of decomposition — so that every structure feels like it belongs to the same broken system.

#### The Civilization

One civilization built everything in this world. It was utilitarian, resource-extractive, and centralized. Its architecture was never meant to be beautiful — it was meant to process things: ore, fuel, labor, goods. Think Soviet-era industrial infrastructure filtered through mid-century American highway culture. Prefabricated components, standardized fastener patterns, modular expansion, corrugated metal cladding, poured concrete pad foundations. Nothing was built to last. Everything was built to be replaced, and replacement never came.

There are no ornamental structures, no monuments, no civic architecture. If a wall has a decorative quality, it is accidental — a color from a painted safety warning, a texture from aggregate in the concrete mix.

#### The Decay Timeline

The collapse was not a single event. It was a cascade that took approximately forty to fifty years. **Nothing is freshly abandoned, and nothing is ancient rubble.** The sweet spot is thirty to fifty years of uncontrolled decay — long enough for metal to have oxidized fully, long enough for paint to be gone except in sheltered recesses, but short enough that structural geometry is still recognizable. A wall is still a wall. A fuel tank is still a tank. A loading dock is still a loading dock.

This timeline rules out two failure modes: fresh abandonment (too clean, too hopeful) and geological ruin (too romantic, too abstracted). The decay is industrial decay — chemical, oxidative, gravitational, and thermal, not natural weathering.

#### Biome Architecture: Same System, Different Zone Function

All three EA biomes draw from the same civilization, each occupying a different operational role. Architecture reads differently because it served different purposes, not because different people built it.

**Cracked Earth** — Extraction and transit infrastructure. This zone moved raw materials. Expect: elevated pipe runs now collapsed at their joints, low prefab relay stations with roll-up doors, concrete pad foundations cracked by subsidence, chain-link perimeter remnants, road surface remnants with faded lane markings, low retaining walls. Structures are sparse, single-story, and wide. The open horizon is a design constraint — extraction infrastructure did not block sightlines.

**Toxic Flats** — Processing and storage. This zone refined or held something dangerous. Expect: vertical storage tanks at various heights and states of rupture, overhead pipe infrastructure connecting tanks, berm walls (earthwork containment), warning-color remnants on metal surfaces, condensed cluster arrangement. Structures are dense, vertical, and claustrophobic because the geometry of industrial processing is vertical and packed.

**Ruins** — Administrative and distribution. This zone coordinated and distributed. Expect: larger footprint slab foundations, regular column grids now without the structure they supported, loading dock platforms at a consistent height, collapsed roof planes as flat debris fields, faded signage on concrete faces. Structures are horizontal, wide-span, and absent — their geometry is defined by what is gone. Negative space is the primary architectural element.

#### Rule for Structural Plausibility

Every standing structure must pass the gravity-and-chemistry test: **can you explain why this part is still standing and why that part has failed?**

Metal fails at joints and thin sections. Concrete fails at tensile load points and foundation shifts. Glass is gone. Plastic is photodegraded. Wood is absent.

If a wall section is standing, the section adjacent to it that has collapsed should be the section with the longer unsupported span, the weaker connection point, or the greater exposure to thermal cycling. Random collapse is not plausible. Collapse follows load paths and material vulnerabilities.

**Design test:** When a prop arrangement feels arbitrary, ask: "What failed first, and did that failure cause the next failure?" If the failure sequence is unclear, revise until it is legible.

---

### 6.2 Texture Philosophy

**Design intent:** Every surface reads as one dominant material, handled with structural logic about how that material ages, so that the world feels physically consistent without requiring PBR rendering complexity.

#### Rendering Approach: Stylized with Material Logic

This game uses a **stylized approach with material-logical wear** — not photorealistic PBR, not flat cartoon. Target reference: hand-painted textures that follow physical rules about how materials actually fail.

PBR is rejected because: (1) specular highlights break flat 2D composition and compete with gameplay legibility; (2) it requires consistent lighting environments that conflict with the biome-specific lighting states in Section 2; (3) it invites material complexity that violates the one-dominant-material-read rule from Section 1.

Flat cartoon is rejected because: (1) it reads as intentional aesthetic softness that undercuts the depleted, purposeful tone; (2) it reduces silhouette legibility at small scale.

The correct approach: **hand-painted diffuse textures following physical wear logic, with hard edge shadows baked in.** Highlights are shape highlights (convex surface catches ambient), not specular. The light source in textures is consistent with the biome's locked lighting state — bake the light direction into the diffuse.

#### Material Read Budget: One Dominant, One Supporting

From Section 1: a part that reads as three materials simultaneously has failed the found-object rule.

- **Dominant read:** the primary structural material (corrugated steel, poured concrete, chain-link)
- **Supporting read:** a secondary material that is the result of the dominant material's failure or treatment (rust on the steel, staining on the concrete)
- **Decorative read:** any material added for visual interest without a functional or decay explanation — **forbidden**

A concrete wall with rebar visible at a crack is correct (one material, one failure mode). A concrete wall with rebar exposed plus spray paint plus a climbing vine plus moss is four reads — reduce to one dominant with one supporting.

#### Wear Application Rules

Wear follows the physics of the material and its environment — it is not a texture layer applied uniformly.

**Metal:**
- Rust flows downward from joints, bolt holes, and cut edges — never appears mid-surface without explanation
- Rust concentration is highest at water-collection points: horizontal ledges, upturned edges, flanges
- Paint fails from edges and corners first, then from areas of mechanical abrasion, then UV exposure on flat faces
- Impact deformation has a clear direction vector — there is a striker and a struck face

**Concrete:**
- Cracking follows tensile failure logic: cracks run from stress concentrators (corners, penetrations, joints) and propagate outward
- Spalling appears on faces exposed to repeated thermal cycling or moisture
- Staining runs vertical from cracks and from any embedded metal that has rusted through

**Application rule:** Start with the clean material. Add wear only where a physical process would deposit it. If you cannot name the physical process, do not add the wear mark.

**Design test:** When a wear mark feels decorative rather than structural, ask: "What physical process made this mark?" If no answer, remove the mark.

#### Tileable vs. Unique Textures

**Tileable:** Ground surfaces, sky gradients, background haze, structural surfaces on repeated prefab components. Tileable textures must have no directional wear.

**Unique:** Any prop seen at close range or with specific story function, any surface where tile seams would be visible at gameplay scale, all foreground layer props.

#### Resolution Targets (tuning targets)

| Asset Category | Base Resolution | Notes |
|---|---|---|
| Ground / terrain tile | 256×256px | Seamless tile, no directional wear baked in |
| Background structure | 256×512px | Silhouette-read priority |
| Midground prop (standard) | 128–256×128–256px | Most environment props |
| Foreground prop (node-defining) | 512×512px | Hazard markers, landmark structures |
| Map node icon | 32×32px | 1-bit clarity requirement at this size |
| Combat background panel | 1920×1080px split across 3 depth layers | Base 1080p; see resolution system note below |

**Resolution system note:** Base target is 1920×1080 (1080p). A UI Scale slider should be exposed in Settings — following the Slay the Spire approach of a fixed internal render resolution with a player-adjustable scale multiplier. In Unity, implement via `Canvas Scaler → Scale With Screen Size → Reference Resolution 1920×1080 → Match Width Or Height (0.5)`, with a UI Scale multiplier exposed in the Settings menu. This allows players to adjust to their display and preference without requiring a full responsive layout system.

---

### 6.3 Prop Density Rules

**Design intent:** Prop density is a biome-specific atmospheric argument — each biome communicates its former function through how much was left behind, and where.

#### Correct Visual Density Per Biome (tuning targets)

**Cracked Earth — Sparse.** Negative space is the dominant visual element. The horizon should be unobstructed for at least 60% of its width. Target: 3–5 distinct prop clusters per combat background panel, each cluster containing 2–4 props. Minimum 30% of the background panel should be empty ground or sky.

**Toxic Flats — Dense, vertical.** Vertical elements (tanks, pipe risers) break the horizon frequently. Target: 6–10 distinct vertical elements per combat background panel, clustered into 2–3 groups with minimal clear sky between groups.

**Ruins — Horizontal, fragmented.** Wide horizontal elements covering 40–60% of ground plane. Vertical props: maximum 2–3 per panel, each a deliberate choice. When a vertical element appears in Ruins, it reads as significant — use it sparingly for narrative props and node landmarks.

#### What Drives Density Differences

Three drivers, in priority order:

1. **Former zone function** — processing infrastructure is dense; extraction infrastructure is sparse
2. **Gameplay mood** — Toxic Flats claustrophobia heightens threat; Cracked Earth exposure heightens vulnerability; Ruins openness emphasizes loss
3. **Combat legibility** — density must never compete with the player or enemy vehicle silhouette; if adjustment is needed for legibility, legibility wins

#### Rule for Prop Placement

A prop feels world-placed when its position is explained by the same logic that would have placed it when the civilization was functional. A fuel relay station sits beside the road it served. A storage tank sits on a concrete pad, not on bare ground.

**Draft the functional explanation first, then check if it also serves composition.** If the functional explanation requires a compositionally awkward position, adjust composition around the prop — do not adjust the prop to serve composition.

**Design test:** For every prop, answer: "Where was this object when the civilization was active, and what has gravity and decay done to it since?" If the prop's current position cannot be reached from its functional origin by a plausible sequence of collapse or drift, reposition it.

#### Hazard Prop Visual Language

Hazard props communicate danger through three channels — all three must agree:

1. **Shape:** Hazard props use interrupted or broken silhouettes — jagged edges, asymmetric protrusions, collapsed angles. Non-hazard props have more resolved silhouettes.
2. **Color:** Operational signal system from Section 4. Red fill or red accent marks indicate active danger. In Cracked Earth, where iron oxide base tones are red-brown, hazard marking must be a distinct geometric element (a painted band, a stamped symbol), not a general reddening.
3. **Physical logic:** Hazard props show the evidence of what makes them dangerous — a contamination source shows a spread pattern flowing from it outward.

#### Foreground / Midground / Background Layer Rules

**Foreground layer:** Ground surface props only. No tall structures. Full color saturation — no atmospheric perspective.

**Midground layer:** Player vehicle, enemy vehicle, node-defining landmark props, active combat elements. No background-filler props intrude here. Full contrast and saturation.

**Background layer:** All architectural and environmental props. Apply 20–30% desaturation and a slight value shift toward the biome's sky color (atmospheric perspective). This ensures vehicles always read as the foreground subject.

**Rule:** If a background prop has higher visual contrast than the enemy vehicle silhouette, it must be adjusted — rescale, desaturate, or reposition.

**Design test:** Screenshot the combat scene in grayscale. The player vehicle and enemy vehicle should be the two highest-contrast elements. If any background prop outranks them, adjust it.

---

### 6.4 Environmental Storytelling Guidelines

**Design intent:** The environment is a silent record of decisions made under scarcity — each biome communicates a specific chapter of the collapse, readable without text, optional in depth but consistent in presence.

#### Visual Details That Tell the World's Story

The world communicates through evidence of process, not through set-dressing.

**Evidence of process:** a fuel tank with a blast-deformation pattern and soot spread on the ground downwind. A chain-link perimeter with one section torn outward and flattened. A road surface with deep parallel ruts where heavy repeated loading caused subsidence.

**Set-dressing (forbidden):** scattered bones (narrative cliché), dramatic lighting on a lone intact object (cinematic framing), rusted signage with legible warnings (too on-the-nose). These are forbidden because they substitute mood for evidence.

#### Story Beats Per Biome

**Cracked Earth:** The story is exhaustion. The extraction infrastructure was functional and standardized — it was not destroyed, it was simply abandoned mid-operation. Pipes are not blown apart; they are disconnected at joints and left. The roads show the density of the traffic that used them. The last portable or valuable things were removed. Fixed infrastructure was left.

**Toxic Flats:** The story is a mistake that compounded. Containment was attempted — berms, secondary walls, drainage channels. The containment failed at a specific point, not uniformly. Warning markings exist in quantity — whoever marked them knew the risk and continued anyway. The contamination has a visible spread pattern flowing from a specific origin point.

**Ruins:** The story is evacuation. Infrastructure was partially dismantled (salvageable components removed) but not demolished. The scale of the foundations indicates the scale of the original structure. The regular column grids suggest planning and intention — that the design now exists as isolated columns on empty slabs communicates the failure of planning at a systemic level.

#### Rule for Readable Narrative Detail

Two tiers:

**Mandatory legibility:** The player must be able to read the biome's basic story beat without any supporting text or UI. Test: show the biome screen to someone unfamiliar with the game and ask them to describe what happened here in one sentence. The answer should contain the correct core concept.

**Optional depth:** Secondary evidence — the specific failure point in the Toxic Flats containment, what the Ruins served — should be present for players who look, but its absence would not make the game lesser.

**Rule:** Design mandatory legibility first. Add optional depth into the density remaining after legibility is satisfied.

#### Route and Vehicle Suitability (Pillar 5)

Routes communicate vehicle suitability through terrain geometry, not through UI indicators. The visual vocabulary for suitability signals will be confirmed when the vehicle stats GDD is written (which will define the precise terrain-preference axes per chassis). Until then, draft vocabulary:

- **Favorable terrain for heavy/durable chassis:** Wide flat road surface, low obstacle density, ground texture shows compaction (hard-packed, smooth), wide route corridors.
- **Favorable terrain for fast/agile chassis:** Narrow corridors, higher obstacle density requiring agility, ground texture shows loose aggregate or sand.
- **Punishing terrain for any chassis:** Unstable ground surface (cracked slab over void, subsided road), high structural debris density. The route visually communicates that transiting it will cost something.

*(Flag: confirm the final chassis terrain-preference axes against the vehicle stats GDD before producing route suitability art assets.)*

**Design test:** A player with a specific chassis type should be able to look at two route options and make a reasonable guess about which favors their build based on environment art alone.

---

### 6.5 Node Visual Differentiation

**Design intent:** Each node type has a non-negotiable visual fingerprint at map-icon scale and a distinct environmental approach sequence, so the player knows what they are committing to before the UI label appears.

#### Node Legibility Rule

The player must be able to identify the node type from the map icon alone, at 32×32px, before hovering or selecting. During the approach sequence, the node type must be confirmed by environmental cues before any game state transition occurs.

**The commitment rule:** If the player cannot identify the node type before they commit to it, the visual design has failed.

#### Map Icon Specifications (32×32px)

**Combat Node:** Two opposing vehicle-like forms (simplified wedge shapes, facing each other). Tarnished steel grey (`#7A7872`) with a red operational signal mark. The directional tension of the two forms is the primary read.

**Merchant/Chopshop:** Single irregular structure form with an interior-implied opening (a building with an opening rather than an exterior wall). Warm amber tint to the icon body. The interior-implied opening is the differentiating silhouette element.

**Event/Unknown:** An intentionally unresolved geometric shape — a partial form that suggests a structure without completing it. 6500K cool grey-shifted color. The unresolved silhouette is the primary read.

**Biome Transition:** Two vertical elements with a gap between them (a gate or passage implied). Gradient fill from current biome accent color on left to destination biome accent color on right. The passage silhouette is unambiguous.

**Boss Node:** A single dominant mass — wider, heavier than the Combat node icons, centered. Maximum contrast: icon body at the darkest value in the map, backed by the lightest available value in the surround. Mass and contrast dominance signal significance. Boss nodes appear at a fixed position at the end of each biome — they are mandatory encounters, not player-selectable options.

**Haven:** A distributed cluster of multiple small form elements grouped together, suggesting settlement. Ember orange (`#E8630A`) as the color signal — the only icon on the map to use ember orange. This is the only correct use of ember orange in the map view.

#### Approach Sequence Environmental Cues

**Combat Node:** Background desaturates immediately (20–30% per Section 2). Enemy vehicle silhouette visible against the washed-out background. The desaturation is the first cue — even at the edge of the screen, the color shift signals combat before the enemy form is fully resolved.

**Merchant/Chopshop:** Interior-implied warmth appears at the edge of the frame — warm amber light spill from the direction of the node. The single-source warm light is the cue. No other node uses warm interior light.

**Event/Unknown:** One intentionally ambiguous element at the center of the frame. The overcast 6500K color state is the cue. No other node uses a center-framed ambiguous element.

**Biome Transition:** The ground surface shifts visibly in the approach frame — the current biome's ground texture begins giving way to the next biome's ground texture. The two-texture ground is the cue.

**Boss Node:** Hard back-light, maximum contrast. The Boss node occludes the light source, creating a silhouette-only read of the Boss environment. No other node produces back-lighting.

**Haven:** Ember orange is present before the structure is legible. Multiple ambient light sources — soft multi-directional ambient, no hard single-source shadow. Ember orange and the absence of hard single-source shadow are both cues.

#### Node Differentiation Design Test

View all six map icons at 32×32px simultaneously. Cover the UI labels. Ask someone unfamiliar with the game to group the icons by "what kind of place do you think this is?" The groupings should map to the correct node type categories without instruction.

If any two icons are grouped together incorrectly, revise the distinguishing silhouette element of the more ambiguous icon. Silhouette is always the fix — color alone is insufficient.

---

## Section 7: UI/HUD Visual Direction

> **Tuning note:** All size values, opacity levels, timing curves, and typographic scales in this section are starting targets. Every value should be validated against in-engine builds at 1080p before locking. All animation durations are tunable — adjust based on what reads clearly and feels responsive in the first playable build.

---

### The Combat Scene Layout

Before HUD specifications, the combat scene's fundamental visual grammar must be established — it governs how every panel, indicator, and animation decision below is positioned.

**Both vehicles are on a horizontal chase rail, facing the same direction (right).** This is not a face-off — it is a pursuit. The player vehicle occupies the left side of the screen (pos back / chasing); the enemy vehicle occupies the right side (pos front / being chased). Both vehicles are mid-frame vertically.

**Position state is communicated by physical position, not by a UI indicator.** Left = Behind. Right = Front. This is the most legible possible encoding — the player reads the chase geometry directly. When a position-change card is played, the two vehicles slide and swap positions along the rail. No additional position label or icon is needed; the rail IS the position system.

**The scene conveys constant motion through three layered systems:**
1. **Parallax background:** Blurred environment elements (cactus, terrain features, ground texture) drift leftward at different speeds per depth layer, creating the sensation of forward travel without the vehicles moving laterally.
2. **Vehicle idle animation loop:** Both vehicles have a subtle terrain-vibration loop — wheels rotating, suspension compressing and releasing at ~2–3px amplitude, chassis oscillating very slightly as if on rough ground. This is the only continuous loop on the combat screen.
3. **Rear dust/dirt particles:** Both vehicles continuously kick up a dust trail from their rear tires. The trail is the primary motion confirmation signal and the most distinctive visual marker of the chase. Particle direction and density should suggest speed.

**Wheel 2.5D treatment:** Despite the sprite being 2D, the wheels should read as rotating discs in perspective. Achieve this by authoring a simple rotation animation on the wheel sprite with a slight foreshortening (the wheel's vertical axis is slightly shorter than its horizontal, ~85% ratio). This creates a 2.5D illusion without requiring 3D rendering. The suspension bounce is a vertical oscillation of the wheel attachment point, not the whole chassis — the chassis floats slightly above the wheel travel.

**Position-swap animation:** When a position card is played, both vehicles slide simultaneously toward the center, cross, and settle into swapped positions. Duration: 300ms ease-in-out. The crossing moment is the dramatic beat — the two vehicles pass through the same center zone briefly. No collision effect; the crossing is a gameplay abstraction. Dust particles intensify during the slide.

---

### 7.1 Diegetic vs. Screen-Space HUD Philosophy

**Design intent:** The HUD reads as a field-assembled instrument cluster — physical gauges, stamped plates, and bolted trays jury-rigged to a vehicle cab, not a software interface rendered in void.

The HUD is **hybrid-diegetic**: every panel is authored as if it were a physical object clamped or welded to the player's cab. It is screen-space in technical implementation, but every design decision should be answerable with "where would this be mounted, and what material is it made of?"

**The three-layer construction model:**

| Layer | Material Metaphor | Visual Treatment |
|---|---|---|
| Panel body | Pressed steel tray or stamped sheet metal | Semi-transparent dark fill (#1A1814 at 80%), single-rule 1.5px border (#7A7872) |
| Panel surface | Stenciled markings, adhesive labels, scratched paint | Typography, iconography, status indicators as if printed onto the tray |
| Panel state | Indicator lights, cracked lenses, wire corrosion | Semantic color states (green/amber/red) + fill pattern redundancy |

**The found-object legibility rule:** The found-object aesthetic is applied **to the container, not the content.** Panels are weathered; text and numbers inside them are not. Rust, patina, and wear appear on borders, panel edges, and icon backgrounds only. Numbers, status indicators, card text, and energy pips are clean, sharp, and fully legible. The single exception is the Offline state fractured fill pattern — it degrades the readability of a subsystem indicator by design, signaling that this information is no longer trustworthy.

**When screen-space may break the found-object aesthetic:**
1. **Critical damage feedback:** A full-screen fractured-glass vignette (1–2 frames, not looping) when hull HP reaches the critical threshold.
2. **Turn-counter pulse:** The 32px circle turn counter briefly glows (#E8630A, 0.3s) on turn increment.
3. **Tooltip overlays:** Clean screen-space panels with no weathering — the found-object frame does not extend to instructional text.

**Design test:** Wireframe the combat HUD at 10cm wide. If you cannot tell which panels are vehicle status vs. card hand vs. enemy status from container shapes alone — before reading any text — the panel grammar has failed.

---

### 7.2 Typography Direction

**Design intent:** Text in this world was stamped, stenciled, or hand-scored by people who needed information to survive — it is direct, abbreviated, and built to be read at a glance.

**Font personality:** The typeface system evokes industrial stencil printing and military logistics labeling. Two-family system:

- **Display / Label family:** A condensed slab serif or geometric stencil face — dense letterforms, minimal stroke modulation, abbreviated spacing. The feeling of lettering on a vintage diesel engine control panel.
- **Data / Monospaced family:** A monospaced industrial face for all numeric values (HP numbers, energy counts, card costs). Monospaced numerals prevent layout jitter when values update frame to frame.

Both families must be licensed for commercial use before finalization. Test render all three text tiers in-engine before locking typeface choice.

**Text tier hierarchy (tuning targets at 1080p):**

| Tier | Role | Size | Weight | Case | Family |
|---|---|---|---|---|---|
| **T1 — Card Name / Screen Title** | Primary identification | 18–22px | Bold / Heavy | ALL CAPS | Display |
| **T2 — Stat Value / Energy / HP Number** | Operational data read mid-combat | 16–18px | Medium | Numerals, mixed case labels | Monospaced |
| **T3 — UI Label / Icon Caption** | Supporting context | 12–14px | Regular | SMALL CAPS or ALL CAPS | Display |
| **T4 — Tooltip / Keyword Definition** | Read at rest | 11–13px | Regular | Sentence case | Display |

T4 is the only tier that uses sentence case. All other tiers are uppercase — this world's signage does not whisper.

**Legibility constraints:** Minimum readable size at 1080p is T4 at 11px — nothing smaller anywhere in the UI. All text renders in ash white (#E8E0D4) on dark panel fills, minimum 4.5:1 contrast ratio. UI Scale multiplier exposed in settings (1×, 1.25×, 1.5×, 2×) — see Section 6 resolution system note.

**Design test:** Render the combat HUD at 1280×720. If T3 labels on subsystem panels are unreadable without squinting, size or weight is insufficient.

---

### 7.3 Iconography Style

**Design intent:** Every icon in this game is a stamped or engraved mark — the kind of symbol a machinist cuts into a control panel so a driver can read it through oil-smeared goggles at speed.

**Visual style:** Engraved / stamped outlines on a recessed dark chip.
- Single-weight outline, no fill graduation — consistent 2px stroke at 24px icon size
- No drop shadows, gradients, or glow on the icon mark itself
- Icon chip (#1A1814 at 90% opacity) is the stamped surface; the outline mark sits on top
- Semantic color applies to the outline, not the chip fill — chip stays dark at all states

**Icon families:**

| Family | Members | Minimum legible size |
|---|---|---|
| Subsystem icons | Engine, Mobility, Weapons, Support | 16px — distinct by silhouette |
| Card type icons | Precision, Assault, Control, Repair, Maneuver | 16px — highest-frequency icon in the game |
| Resource icons | Energy pip, Hull HP unit, Plating layer | 12px — energy pip is most-read icon in combat |
| Status effect icons | Stall, Suppress, Fortify, and additions from GDD | 16px — build using a reusable template chip |
| Node map icons | Combat, Haven, Merchant, Chopshop, Unknown, Boss | 24px floor |
| Rarity pip (list views) | Common, Uncommon, Rare, Legendary | 12px — Merchant/reward screens only |

**Common rarity treatment:** Common rarity uses no additional border color — it is the base card frame only. Common is the absence of a rarity signal, not a colored one. This resolves the tarnished steel grey overload risk: the structural border color (#7A7872) is never also a rarity signal.

**Silhouette-first rule:** Every icon must be readable by silhouette alone at its minimum legible size, rendered as a solid monochrome shape.

**Production rule for solo developer:** Author all icons in a master sheet at 48×48px, 1px inter-icon padding. Export pipeline scales to 24px and 16px. Icon chips are separate UI sprites applied programmatically — not baked into the icon sheet — so chip color and size can be adjusted without re-exporting.

**Minimum fill pattern dimensions (accessibility):** Subsystem indicator cells must be no smaller than 20×20px at 1080p. The hatched fill for degraded state uses 2px line weight with minimum 3px gap between lines. The fractured fill for offline state uses 3 fracture lines maximum radiating from one corner. Test all fill patterns at minimum cell size before locking.

**Design test:** All 5 card type icons, all 4 subsystem icons at 16px as filled black silhouettes. If any two are confusable, redesign the more complex one toward a simpler primary shape.

---

### 7.4 Animation Feel for UI Elements

**Design intent:** UI animation in this game is mechanical, not digital — the motion of levers, gauges, and stamp presses, not the easing curves of a mobile app.

**The wasteland animation doctrine:**

Permitted:
- Abrupt starts, mechanical ease-out — elements that arrive suddenly and settle with friction
- Linear motion with a single ease-off at end — no bounce, no spring, no anticipation
- Brief duration — 80–200ms for state changes; 200–350ms for panel entries; nothing exceeds 400ms unless it is a deliberate player-paused moment

Forbidden:
- Bounce / spring easing
- Fade-in from zero opacity — objects slide, stamp, or snap into view
- Continuous ambient animation on non-critical elements (Legendary rarity pulse §4.7 is the single decorative exception, plus the combat idle loop. The §H.3 rust-shimmer tell is information-bearing, not decorative — see Rust-Shimmer Tell below.)
- Screen shake as UI feedback
- Cinematic letterboxing or lens distortion

**Rust-Shimmer Tell (HostileTiltDelta visual channel, per Node Encounter §H.3):**

This animation is *information-bearing* — it is one of the three channels (visual / audio / haptic) that surface the HostileTiltDelta tell when the player hovers an Event beacon while `FrameState ∈ {Degraded, Offline}`. It is not decorative motion and is not gated by Reduce Motion in the same way ambient particles are; it has a specific Reduce Motion alternative render below.

- **Pixel-art interpretation:** A per-frame palette swap on the *rust-pixel set* of the affected map icon (currently 32×32px Event beacon). Non-rust pixels — silhouette outline, primary fill, glyph anchor — hold constant. The shimmer is achieved via palette index cycling, not via runtime noise sampling or per-pixel shader effects. Renders correctly as a flat 2D sprite animation with no GPU work and no shader pass.
- **Palette cycle:** 4 hues drawn from a 3-step value ladder around the icon's base rust hue (~`#8B3A2A`): one step darker, base, one step lighter, two steps lighter. Frame indexes cycle in an asymmetric "weathering wave" pattern: `[0, 1, 2, 3, 2, 1]` over 6 frames. Total luminance variation between frame 0 and frame 3: ~2% (matches Node Encounter §H.3 spec).
- **Frame budget:** 6-frame palette cycle at 4fps = 1500ms loop (6 × 250ms). Animation runs *only* while the Event beacon is the hover/focus target; deselection halts the cycle on the next frame and snaps back to frame 0. No frames are cached at rest. Memory cost: 6 × 4-byte palette LUT = 24 bytes per beacon. CPU cost: one palette swap per displayed frame (≤4 swaps per second per active beacon, single-beacon-at-a-time).
- **Haptic-absent fallback (KBM primary players, no gamepad connected):** The HostileTiltDelta tell is spec'd as a three-channel signal (visual + audio + haptic). KBM players have no haptic channel, so the remaining two channels are slightly amplified to compensate:
  - Visual: luminance variation lifts from ~2% to ~3% (palette ladder steps to one step darker, base, two steps lighter, three steps lighter — same 6-frame asymmetric pattern, wider amplitude).
  - Audio: low-frequency wind boost lifts from +3dB to +4dB below 200Hz (per Node Encounter §H.3).
  - Detection: `Gamepad.current == null` at the moment the beacon is selected. The fallback applies for the duration of that selection.
- **Reduce Motion alternative render:** When the player has Reduce Motion enabled (`design/accessibility-requirements.md` §3.3), the palette cycle is replaced by a *static rendering at frame 3* (the "peak weathering" state — the brightest frame of the cycle). The luminance amplitude advantage is preserved (3% under haptic-absent, 2% otherwise). The audio channel is unaffected — Reduce Motion gates motion, not audio. Net effect: Reduce Motion + KBM players still receive a static visual tell that is *stronger* than the default-mode animated tell, and an unmodified audio tell.
- **What the player perceives:** The Event icon under hover reads as "weathering faster than the rest of the map" — a subtle pulse of corrosion across the rust patches of the icon. It does not draw the eye like a warning telegraph; it reads as world-physics applied to the icon. This is intentional per Node Encounter §H.3 and Section B of that GDD ("HostileTiltDelta is physics, not punishment").
- **Cross-listing:** `design/accessibility-requirements.md` §3.3 Reduce Motion lists "decorative particle systems (rust shimmer, dust)" under gated effects — that entry refers to *ambient world rust-shimmer* on environmental props, which is decorative. The §H.3 information-bearing rust-shimmer tell on Event beacons is its own narrowly-scoped animation governed by this spec; its Reduce Motion behavior is alternative-render, not gating.

**Vehicle idle animation loop (combat screen only):**
- Wheel rotation: continuous loop at implied road speed — tune RPM to feel like ~80kph
- Suspension bounce: 2–3px vertical oscillation at the wheel attachment point, not the chassis. Period: ~0.4–0.6 seconds. Chassis follows with a 1-frame lag for secondary motion feel.
- Chassis micro-vibration: ±1px lateral and vertical jitter on a 3–5 frame random-offset cycle — subtle enough that players may not consciously notice it, but its absence should feel wrong.
- This is the only continuous loop on the combat screen for non-particle elements.

**Rear dust particles:**
- Both vehicles continuously emit a dust trail from rear wheel positions
- Particle direction: leftward (world-space), slightly downward, with random spread of ±15 degrees
- Particle lifetime: 0.4–0.8 seconds, fading to transparent. Scale: small (2–4px at spawn, growing to 6–10px at fade)
- Density communicates speed — during the position-swap animation, particle density increases 2× for the duration of the slide
- Particle color: bleached sand (#D4B896) to ash white (#E8E0D4), fading to transparent. No biome accent color in the particles — they are road dust, not environmental color.

**Parallax background motion:**
- Background layers drift leftward continuously during combat at different speeds per depth layer
- Layer 1 (furthest): slowest — distant terrain silhouette, barely perceptible
- Layer 2 (mid): moderate — structures, large rock formations
- Layer 3 (nearest / foreground detail): fastest — blurred cactus, low ground debris
- All layers are blurred in proportion to their implied distance — the nearest layer is the most blurred, creating motion-blur feel without post-processing
- Parallax motion pauses during card selection/play input window — the world holds while the player makes a decision — then resumes when the turn resolves

**Cards:**
- Hand entry: Cards slide up from below screen edge and fan into position. 30ms stagger per card. Entry curve: linear 70%, ease-out final 30%. Total: 200ms per card.
- Card focus (gamepad/keyboard navigation): 1px ash white (#E8E0D4) outline activates on focus. No lift. This is the tentative/browsing state.
- Card selection (committed, before play): 2px ember orange (#E8630A) outline replaces the ash white. Card lifts 8px upward over 80ms linear. This is the confirmed-selection state.
- Card play: Selected card slides from hand toward center-screen play zone over 150ms linear, disappears on frame 151 as effect resolves. No fanfare — the card is consumed.
- Card discard/cycle: Slides downward off-screen at 120ms linear.

**Subsystem state changes:**
- Color change: instantaneous on the frame the state resolves — no crossfade
- Fill pattern cut-in: 2 frames (33ms at 60fps) — a hard stamp
- Icon chip flash: 1-frame ash white flash on the chip at the moment of change, then immediate settle to new state color — the "indicator light flicker"
- Offline state: fractured fill cuts in at 2-frame rate; icon outline dims to 40% opacity over 400ms ease-out (indicator going dark)
- Recovery: fill cuts in at 2-frame rate; icon outline brightens to full opacity over 200ms ease-out

**Screen transitions (stamp-and-slide system):**
- Combat → Post-combat reward: Hand tray slides down (200ms linear) → enemy panel slides right (200ms, 50ms stagger) → reward panel stamps in from right (250ms ease-out)
- Post-combat → Map: Reward panel slides right (200ms) → map panel stamps in from bottom (250ms ease-out)
- Map → Combat: Map stamps down (200ms) → combat panels stamp in simultaneously from anchor edges (30ms stagger, 250ms ease-out each)
- Haven arrival: 350ms ease-out (slowest transition in the system) — the ember orange Haven icon holds 1 second before panel content appears
- All transitions complete in under 1 second total

**Design test:** Play five consecutive combat→reward→map transitions with a stopwatch. If any transition feels like it is delaying the player rather than punctuating a moment, cut 20% from every duration and retest.

---

### 7.5 Screen-by-Screen Visual Direction

**Design intent:** Each screen is a different physical space the player inhabits — not a different menu mode — and the visual language of that space communicates what kind of activity happens there.

#### Combat HUD

**Layout (from the confirmed combat scene sketch):**
- **Top center:** Turn counter (32px circle ring)
- **Top left:** Hull HP indicator (T1 numeral, resource icon) — this is the primary survival read; it wins the initial scan by positional priority
- **Top right:** Enemy hull HP indicator (mirrored treatment)
- **Left side panel:** Player subsystem states (4 subsystem indicators in L-bracket panel)
- **Right side panel:** Enemy subsystem states (mirrored panel)
- **Bottom center:** Card hand tray (open horizontal tray, 4 card slots)
- **Mid-screen, horizontal rail:** Both vehicles — player left, enemy right, both facing right

The card hand tray must never occlude the hull HP indicators. If vertical space is tight at 1080p, hull HP moves inside the player status panel (upper-left), not into the card zone.

**Energy display:** Three energy pips rendered as a distinct shape from subsystem indicator cells — a larger, non-rectangular form (circle or diamond, minimum 18px) in a dedicated zone near the card hand tray. Pip states: full (green, solid), spent (dark fill, steel grey border), not available (dark fill, no border). The three pips should chunk as a group, not read as three separate indicators.

**Position state:** Communicated entirely by vehicle position on the chase rail. No additional label or icon. Left vehicle = Behind (chasing). Right vehicle = Front (ahead). When position changes, vehicles slide and swap — the animation is the UI.

**Visual tone:** Active, tense, readable. The combat HUD is a cockpit — every element serves a function. The only ambient motion permitted is the vehicle idle loop, the parallax background, dust particles, and the Legendary card rarity pulse.

#### Map Screen

The route map is a **torn paper road atlas taped to a dashboard** — not a digital interface. Background reads as aged paper or cloth. Route lines are hand-drawn paths (rough, slightly irregular). Node icons are engraved stamp style (Section 7.3), placed as if pressed by a rubber stamp at each waypoint.

Player position marker: a small vehicle icon (simplified chassis shape), never a glowing dot. It is the only element on the map that uses ember orange (#E8630A).

Route line treatments:
- Traversed paths: tarnished steel grey (#7A7872)
- Available forward paths: bleached sand (#D4B896), slightly heavier line weight than traversed
- Locked/unavailable routes: ash white (#E8E0D4) at 30% opacity, dashed line
- Line weight difference and dash pattern provide redundant signals for colorblind players beyond color alone.

#### Chassis Selection

Each chassis presented at its largest display size in the game — full portrait or 3/4 view. Selected chassis is larger than unselected options (display rack, not carousel). Chassis stat comparison in a side panel using standard panel construction with T2 monospaced data typography.

Visual tone: deliberate, considered. Lighting direction: each chassis under a workbench lamp — clear, slightly harsh, no romantic atmosphere. The vehicle is a tool being evaluated.

#### Merchant / Chopshop

**Merchant:** Tailgate sale or pawn table — goods laid out flat, prices stamped beside them. Card items use the standard card frame; part items use a panel chip with part icon at T1 scale. No shop UI chrome.

**Chopshop:** More utilitarian — a workshop. Panel borders show more explicit scratching within the found-object rule. The chassis diagram for part installation is rendered in a technical-drawing style — wireframe or outline-only chassis with slot indicators. *(Flag: Chopshop chassis diagram is visually complex — confirm slot-indicator system design before finalizing this panel's art direction.)*

Haven's ember orange (#E8630A) does NOT appear in the Merchant or Chopshop. The warmth here is incandescent yellow-amber from a work light (2900K), not the game's promise color.

#### Post-Combat Reward

**Salvage is presented as a find** — something uncovered in the wreckage. The reward panel arrives with the 250ms stamp-in transition; each item appears with its icon at 48px, name in T1, relevant stat in T2, rarity border applied.

Multi-reward selection (choose one of three cards): three equal-width panels laid out horizontally. Selection state uses the 2px ember orange outline treatment — consistent with card selection everywhere in the game.

#### Main Menu

A static or very-slowly-shifting background depicting a wasteland road from behind a vehicle cab — the interior edge of a windscreen as a framing device, applying the found-object frame to the game's entry point itself. Menu options are a column of stamped panel labels mounted to the interior surface — not floating text.

The game title treatment: heavily stenciled, distressed, but fully legible at any screen size. This is a designation painted onto the side of a vehicle, not a brand logo.

Background motion rule: if dust particles or light shift are used, they must be subtle enough that a player who does not notice them still has a complete visual experience.

**Design test:** Screenshot the main menu. Crop out the menu options. Does the remaining background communicate the game's world and tone on its own? If not, the background composition is not carrying its weight.

---

## Section 8: Asset Standards

> **Tuning note**: Resolution tiers, atlas budgets, and layer counts are all configurable — they represent the production-ready defaults, not hard locks. Adjust during playtesting when visual fidelity vs. performance tradeoffs become measurable.

---

### 8.1 File Formats

| Asset Category | Format | Notes |
|---|---|---|
| Sprites (with alpha) | PNG | Source format; Unity imports and compresses to DXT5/BC3 at build time |
| Sprites (no alpha) | PNG | Unity compresses to DXT1/BC1 at build time |
| Fonts | TTF or OTF | Imported as TextMeshPro Font Assets; do not use Unity's legacy Text component |
| Audio | WAV (source), OGG (build) | Lossy OGG for all in-game audio; WAV preserved as source |
| UI layout | UXML + USS | UI Toolkit only — no UGUI Canvas for new screens |
| ScriptableObject data | `.asset` | All gameplay values (card stats, chassis specs, balance values) |
| Addressable catalogs | Generated by Unity | Do not hand-edit catalog files |

---

### 8.2 Naming Conventions

All asset filenames use `snake_case`. Prefixes identify asset type:

| Prefix | Type | Example |
|---|---|---|
| `spr_` | Sprite / texture | `spr_scout_chassis_state0.png` |
| `spr_card_` | Card face sprite | `spr_card_precision_blowout_shot.png` |
| `spr_ui_` | UI sprite / icon | `spr_ui_subsystem_engine.png` |
| `spr_bg_` | Background layer | `spr_bg_cracked_earth_far_a.png` |
| `spr_vfx_` | VFX sprite sheet | `spr_vfx_dust_rear_tire.png` |
| `anim_` | Animation clip | `anim_scout_idle_bounce.anim` |
| `mat_` | Material | `mat_scout_chassis.mat` |
| `sfx_` | Sound effect | `sfx_card_play_precision.wav` |
| `mus_` | Music track | `mus_combat_cracked_earth.ogg` |
| `so_` | ScriptableObject | `so_card_blowout_shot.asset` |
| `go_` | Prefab / GameObject | `go_combat_scene_controller.prefab` |

**Chassis damage state sprites** follow `spr_[chassis]_[subsystem]_state[N].png`:
- `spr_scout_chassis_state0.png` — pristine
- `spr_scout_chassis_state1.png` — light (30% threshold)
- `spr_scout_chassis_state2.png` — heavy (60% threshold)
- `spr_scout_chassis_state3.png` — critical (85% threshold)

**Card faces** follow `spr_card_[family]_[card-name].png` (family: `precision`, `assault`, `control`, `repair`, `maneuver`).

---

### 8.3 Texture Resolution Tiers

| Asset Type | Resolution | Notes |
|---|---|---|
| Vehicle chassis — master canvas | 256×128 px | Shared canvas for all chassis; individual sprites vary in size within it (see Section 5.5) |
| Vehicle chassis — Scout | ~128×48 px within canvas | Blade 3:1; roughly half the area of Heavy Truck |
| Vehicle chassis — Assault | ~192×80 px within canvas | Wedge 2.4:1; medium scale |
| Vehicle chassis — Heavy Truck | ~240×128 px within canvas | Block; post-launch |
| Vehicle chassis — damage overlays | Match per-chassis authored size | Per-subsystem layer composited at runtime |
| Card face — full art | 200×280 px | Locked. See Section 3 for rationale. |
| Card face — icon (hand thumbnail) | 64×90 px | Downscaled from full art; no separate asset |
| Subsystem icons (HUD) | 48×48 px | Minimum silhouette-legible size; matches TROPHY SILHOUETTE principle |
| Enemy faction icons / busts | 96×96 px | NPC encounter portraits; 128px for boss busts |
| Background layer — far A (tiling) | 512×512 px | Tiling sky/horizon composition A; loop-safe edges |
| Background layer — far B (tiling) | 512×512 px | Tiling sky/horizon composition B — distinct from A for depth variation |
| Background layer — mid (tiling) | 1024×512 px | Tiling mid-ground; slower parallax than far layers |
| Background layer — near (full-res) | 1920×1080 px | Non-tiling foreground; blurred cactus, debris; full art |
| UI chrome / borders | 9-slice safe zone minimum 8px | Card frame, HUD panels; see nine-slice note below |
| VFX sprite sheets | 128×128 px per frame | Dust, sparks; power-of-two for atlas packing |

**Nine-slice candidates**: Card frame border, HUD subsystem panel backgrounds, Haven shop panel, node map card popups. All nine-slice sprites must have a minimum 8px border zone on all four sides to survive slicing without artifact.

---

### 8.4 Texture Compression

| Platform | Format | Notes |
|---|---|---|
| PC (primary) | DXT5/BC3 (with alpha), DXT1/BC1 (no alpha) | Universally supported on Windows/Steam targets |
| PC — UI atlas | RGBA32 | Uncompressed for UI Toolkit sprites to prevent block artifact on fine text/icons |

**Filter mode: Point (no filtering) for all sprites** — hard pixel edges throughout, matching the RUST ICON hard-edged aesthetic. Do not override this on any individual sprite; the aesthetic depends on consistency.

Mipmap generation: disabled for all 2D sprites. Mipmaps introduce blurring at distance that contradicts the pixel-crisp intent. The parallax near layer (1920×1080) and card faces are never rendered at sub-pixel size in context.

---

### 8.5 Sprite Atlas Strategy (Sprite Atlas V2)

Unity 6.3 uses Sprite Atlas V2 by default. Do not downgrade to V1.

| Atlas | Contents | Rationale |
|---|---|---|
| `atlas_combat_hud` | Subsystem icons, position indicator, turn counter, energy pips | All visible every combat frame — minimize sprite draw calls |
| `atlas_cards_precision` | All Precision family card faces | Loaded per-family; unloaded when not in hand |
| `atlas_cards_assault` | All Assault family card faces | Same pattern |
| `atlas_cards_control` | All Control family card faces | Same pattern |
| `atlas_cards_repair` | All Repair family card faces | Same pattern |
| `atlas_cards_maneuver` | All Maneuver family card faces | Same pattern |
| `atlas_ui_chrome` | Card frames, rarity borders, HUD panel chrome, buttons | Static UI chrome shared across screens |
| `atlas_vfx_combat` | Dust, sparks, hit flash, Legendary pulse frames | All combat VFX in one atlas; loaded for combat, unloaded after |
| `atlas_nodes_[biome]` | Node icons, connection line art, biome map chrome | Per-biome atlas; loaded when entering biome map |
| `atlas_chassis_scout` | Scout chassis + all damage state layers | Loaded for runs featuring Scout |
| `atlas_chassis_assault` | Assault chassis + all damage state layers | Loaded for runs featuring Assault |

**Memory allocation target (4 layers × 3 biomes + HUD + chassis + VFX):**
- Far A tiling (512×512 × 3 biomes): ~2.25MB
- Far B tiling (512×512 × 3 biomes): ~2.25MB
- Mid tiling (1024×512 × 3 biomes): ~4.5MB
- Near full-res (1920×1080 × 3 biomes): ~18MB
- HUD + UI chrome atlases: ~8MB
- Card family atlases (5 × ~50 cards at 200×280): ~28MB
- Chassis atlases (2 chassis × 4 damage states + subsystem overlays): ~6MB *(provisional — pending ADR-[visual-part-system]; actual size depends on compositing strategy chosen)*
- VFX atlas: ~2MB
- **Total estimated uncompressed**: ~71MB. DXT5 compression yields roughly 4:1 → **~18MB in memory**. Acceptable within the 2GB ceiling. *(Chassis atlas line is provisional — see above.)*

Only the active biome's background layers are loaded at any time. Addressables handle streaming per biome transition.

---

### 8.6 Addressables Streaming Strategy

Card family atlases stream by the families present in the active deck — any family not represented in the current deck's card pool is not loaded. Background layer atlases stream by active biome.

```
Groups:
  combat-hud          — always loaded during combat
  cards-[family]      — loaded per-deck composition (lazy, per run start)
  bg-[biome]          — loaded per-biome transition, unloaded on exit
  chassis-[name]      — loaded at run start for the chosen chassis
  vfx-combat          — loaded at combat scene load
  ui-static           — always loaded (main menu, Haven, node map chrome)
```

**Exception handling note (Unity 6.2+ behavior change):** Addressables no longer throws a default exception on load failure — the operation completes with a `Failed` status. All `Addressables.LoadAssetAsync<T>` calls must check `OperationHandle.Status == AsyncOperationStatus.Succeeded` before accessing `.Result`. Do not assume a completed operation succeeded.

---

### 8.7 Animation Standards

**Vehicle idle animation — hybrid approach (frame-by-frame + shader-driven):**
- Frame-by-frame sprite animation: wheel rotation loop (8 frames at 12fps) + suspension bounce (4 frames, 0.4–0.6s period)
- Shader-driven micro-jitter: ±1px chassis displacement on a noise-driven offset in the sprite material shader — avoids the cost of per-frame unique sprites for micro-motion
- Rear dust particles: particle system, not sprite animation — continuous emission, leftward direction, 2-layer depth (near tire heavier, rear falloff lighter)
- 2.5D wheel illusion: wheel sprite rendered at ~85% vertical scale (foreshortened ellipse) with rotation loop

**Card flip / play animations:**
- Card selection lift: 4px Y offset over 80ms, eased out
- Card play: slide to target zone over 120ms + brief scale pop (1.0 → 1.08 → 1.0, 60ms)
- Discard: fade + 6px downward drift, 100ms
- Draw: slide up from deck position, 80ms

**Position swap (chase-rail):**
- Both vehicles slide across center in 300ms (ease in/out)
- Dust particle burst intensity increases during swap
- Swap resolves before next card input is accepted

**All animation curves:** Unity Animation Curve assets stored in `assets/data/animation-curves/`. Do not hardcode curve shape in MonoBehaviour code — all easing is data-driven.

---

### 8.8 LOD Philosophy

This is a 2D game — traditional 3D LOD does not apply. LOD rules govern **when to use which resolution tier** based on screen context, not camera distance:

| Context | Chassis sprite | Card art |
|---|---|---|
| Combat scene (primary) | Per-chassis authored size within 256×128 canvas | 200×280 full res (in-hand, selected) |
| Node map (thumbnail) | 24×24 icon (authored separately) | 64×90 thumbnail |
| Haven screen | 128×64 mid-res (Scout scale; Assault proportional) | 64×90 thumbnail |
| Victory / defeat screen | Per-chassis authored size within 256×128 canvas | — |

Enemy vehicles use the same chassis resolution as player vehicles — no LOD reduction for enemies. Enemy factions are distinguished by color marking and faction icon at HUD scale; the chassis silhouette is shared geometry with different paint/damage overlays.

---

### 8.9 Export Settings

**Sprites**: Export from Aseprite (or equivalent pixel editor) as PNG, no color profile embedding. Flat canvas only — no layer data in exported PNG. Unity's importer handles compression; do not pre-compress sprites before import.

**Fonts**: Export TTF from source. Import into Unity as TextMeshPro Font Asset via `Window > TextMeshPro > Font Asset Creator`. Do not use dynamic font atlas mode for any font used in card text — static atlas only, sized for the character set needed (Latin + numerals + basic punctuation).

**Audio**: Source as uncompressed WAV (44.1kHz, 16-bit). Unity AudioImporter compresses to OGG Vorbis at Quality 70 for SFX, Quality 80 for music. Do not pre-compress audio before import.

**ScriptableObjects**: All card definitions, chassis stats, and balance values authored in Unity Editor as `.asset` files via custom Editor tools — not hand-edited JSON or YAML. This ensures validation at authoring time, not runtime.

---

### 8.10 Vehicle Part System — ADR Required

> **PRODUCTION GATE**: Do not begin vehicle art production until the Visual Vehicle Part System ADR is written and approved. This ADR must define the compositing strategy (procedural overlays vs. key slot sprites vs. full damage-state sprites) and resolve the scope risk flagged by the Technical Director (360+ asset risk for 2 chassis × 4 subsystems × 4 damage states × visual variety). The ADR is a prerequisite for the chassis atlas memory budget in Section 8.5 to be considered final. Run `/architecture-decision` to author this ADR before any vehicle sprite work begins.

---

## Section 9: Reference Direction

### Purpose of This Section

Reference direction is not a license to imitate. Its function is to name specific techniques that have solved specific problems, assign each one a precise scope, and then draw an explicit boundary so that the reference informs without colonizing. Every reference listed here is cited for one technique. The moment a reference begins to describe the general look of Wasteland Run, it is being misread.

---

### Reference 1: Pacific Drive *(Ironwood Studios, 2024)*

**What to draw from: the visual language of incremental system failure.**

Pacific Drive communicates vehicle degradation through the accumulation of localized, specific damage marks — a cracked mirror, a door that no longer closes flush, windshield stress fractures originating from an impact point. Each mark is readable as a distinct event rather than a general "damaged" state. The visual argument is that the car records what happened to it, and the player reads that record before any UI element tells them anything.

In Wasteland Run, this principle governs the chassis damage state progression (Section 5.1). The technique to borrow: damage marks are authored as spatially specific sprite elements, each placed at the geometrically plausible point of impact rather than distributed uniformly. A forward-panel dent originates from the front. A lateral breach reads as lateral force. The vehicle's damage record is a causal history, not a texture treatment.

**What to explicitly avoid:** Pacific Drive's damage presentation occurs in first-person 3D, where camera proximity allows minute surface detail to carry meaning. Wasteland Run operates at 192×96px combat sprites. The Pacific Drive technique must be translated to pixel scale — which means each damage mark is authored as a small number of deliberate pixels that communicate direction and location, not fine-detail surface texture. Any attempt to achieve Pacific Drive's level of surface specificity at pixel scale will produce visual noise. The translation rule: legibility at pixel scale takes priority over surface fidelity. One well-placed five-pixel dent mark beats ten-pixel ambient crazing.

**Art bible principle served:** TROPHY SILHOUETTE (Section 1) and the Damage State Progression (Section 5.1). The silhouette must survive all four damage states. Pacific Drive's localized damage logic is exactly the mechanism that allows meaningful damage to accumulate without destroying the base chassis read.

---

### Reference 2: Slay the Spire *(MegaCrit Games, 2019)*

**What to draw from: the compositional grammar of a card as a scannable object at play speed.**

Slay the Spire established the spatial anatomy of a readable card — cost anchored to a fixed corner (top-left, high contrast), name in a consistently weighted band, art in a dedicated zone that does not compete with the keyword text zone, keywords themselves in a bottom register that the eye reaches last. The principle is that a player in mid-combat does not read a card; they scan it in a learned sequence that takes under one second. The card layout makes that sequence inevitable rather than negotiated.

In Wasteland Run, the card's scan sequence should be equally non-negotiable: energy cost first (top-left, highest contrast element on the card), card type shape-read second (the header band and icon anchor, identifiable before reading), effect text third (lower register, secondary read zone). The specific Slay the Spire technique to apply: position the cost in a region that is visually isolated from all other information. No competing element of equivalent contrast should share the cost's visual zone. The player should be able to read the cost without finding it.

**What to explicitly avoid:** Slay the Spire's card art is illustrative and fills the art zone with rendered scene-setting imagery. Wasteland Run cards are stamped field-order forms (Section 3.4) — the art zone carries a functional icon or a stripped schematic rendering of the card's subject, not illustrative scene art. Slay the Spire's chromatic range is also broader than Wasteland Run's permitted vocabulary; its cards introduce a wide range of hues for deck identity and rarity. Wasteland Run's palette is restrained to the base four colors plus family-specific hues (Section 3.4) and rarity border tones (Section 4.7). No chromatic expansion justified by this reference.

**Art bible principle served:** COLOR AS OPERATIONAL SIGNAL (Section 1, Principle 3) and Card Shape Grammar (Section 3.4). The scan-sequence logic from Slay the Spire is the implementation vehicle for the found-object card rules already established. The reference validates the structural decision; it does not dictate the visual surface.

---

### Reference 3: Papers, Please *(Lucas Pope, 2013)*

**What to draw from: stamped, official-document typography as the primary aesthetic register of a UI system.**

Papers, Please built an entire interaction language from bureaucratic document design — rubber stamps, official form typography, document-frame composition, the specific weight of a typeface chosen for reproduction on low-quality paper rather than for elegance. Every element of its UI looks like it was produced by an institution under resource pressure. The aesthetic consequence is that the player feels the world's logic in the act of reading, not just in the content of what they read.

Wasteland Run's card system, event text panels, and node information panels should feel like they were produced by the same kind of resource-pressured, bureaucratically organized civilization described in Section 6.1. The specific technique: all UI typography is set in a fixed-width or quasi-fixed-width face with a stamped, slightly-imprecise registration quality. Text panels carry faint rule lines or grid marks that imply a form template. The document is not pristine — registration is not pixel-perfect, ink density varies slightly across a panel. These imperfections are authored, not random.

**What to explicitly avoid:** Papers, Please operates in a monochrome and extremely low-color register that is deliberately oppressive as a tone-delivery mechanism. Wasteland Run is not attempting to recreate that affective weight. The base palette includes bleached sand and ash white as relatively warm, open tones — the document aesthetic should read as worn and found-object, not as institutional grey suffocation. The imprecision in registration is subtle. A card that looks like it was printed in a gulag is too far. A card that looks like it was stamped from a master plate by a traveling printer is correct.

**Art bible principle served:** The Visual Identity Statement (Section 1) — "treated as a found object — weathered, stamped, and worn." Papers, Please is the most direct reference for what "stamped" means as a production technique. Its typographic logic is the model for the industrial stamped-form aesthetic that Wasteland Run's card system and UI panels are built on.

---

### Reference 4: Darkest Dungeon *(Red Hook Studios, 2016)*

**What to draw from: value-first color composition that maintains mood legibility under maximum UI density.**

Darkest Dungeon's combat screens consistently maintain a strong, readable visual hierarchy despite placing six or more character portraits, multiple action bars, HP indicators, skill queues, and status effect icons on screen simultaneously. The mechanism is value structure: the background reads as a single mid-dark unified value, every UI element is anchored to either the very light or very dark extreme of the value range, and mid-values are avoided for anything the player needs to read at speed. The result is that critical information pops against its context without requiring high saturation to do so.

In Wasteland Run, the combat state (Section 2) places two vehicle silhouettes, a card hand, subsystem indicators, and a turn counter on screen simultaneously under adversarial conditions. The Darkest Dungeon value technique provides the structural solution: background at a unified mid-dark (the 30%-desaturated biome environment per Section 2), vehicle silhouettes at the highest-contrast range (bright edge highlights on dark chassis against mid-dark background), UI panels at a consistent dark-transparent layer, and semantic state indicators (green/amber/red) punching above the mid-value floor with full value contrast even before their hue is resolved. This is the mechanism that makes colorblind fill-pattern backup (Section 4.6) work — the value structure is the primary signal, the hue and pattern are confirmation.

**What to explicitly avoid:** Darkest Dungeon uses deep red-black shadows and gothic architectural framing as its primary atmospheric argument. Wasteland Run's shadow language uses strong contrast for predatory focus, not dread. The shadow in Wasteland Run has a specific direction and an implied physical light source — it is never atmospheric darkness for mood. The Darkest Dungeon value technique is borrowed at the structural level only; the emotional temperature of how those values are used is entirely different.

**Art bible principle served:** Hero Shapes vs. Supporting Shapes (Section 3.6) and the Combat state atmospheric spec (Section 2). The visual hierarchy contract — player vehicle first, enemy vehicle second, active card third — requires a value structure that enforces that ordering pre-attentively. Darkest Dungeon demonstrates that this hierarchy can be maintained at high UI density without making the screen feel cluttered.

---

### Reference 5: The Road *(John Hillcoat, 2009 film)*

**What to draw from: dry atmospheric graduation — how far distance reads as threat through value, not through added content.**

The 2009 film adaptation is one of the most precise executions of wasteland color temperature and atmospheric perspective in recent visual media. The specific technique: the horizon reads as a threat not because of what is on it, but because of how atmospheric value graduation (sky value, ground value, and mid-distance haze value) frames the space between the viewer and the horizon as a cost. The sky is never blue. It is a grey-white value that sits just lighter than the mid-distance grey. The ground sits just darker. The result is that the horizon feels far — not romantically far, but expensively far. Getting there requires something.

In Wasteland Run, the map navigation state (Section 2) and the biome background parallax system (Section 8.3, background layer resolution spec) are the application sites. The technique: atmospheric perspective is applied as a value graduation toward the sky, not as a color shift toward blue or cyan. The far background layers are desaturated and value-shifted toward ash white (`#E8E0D4`), not toward a blue haze. This produces distance that reads as dry and depleted rather than as the romantic blue-distance familiar from standard atmospheric perspective.

The Road also maintains one consistent rule across all exterior shots: the horizon line is always visible. In Wasteland Run, the Cracked Earth biome enforces this rule explicitly (Section 6) — the visible horizon communicates exposure, not freedom.

**What to explicitly avoid:** The Road's human subject matter and its specific narrative weight — the film earns its bleakness through character context that Wasteland Run does not have and does not require. The visual technique of desaturated grey-white atmospheric graduation is transferable; the emotional despair is not. Wasteland Run's scarcity palette carries weight, not hopelessness — the distinction is the presence of ember orange as a directional signal. The Road has no equivalent. Do not let this reference push the atmosphere toward nihilism; pull back when any scene begins to read as having no direction.

**Art bible principle served:** SCARCITY PALETTE (Section 1, Principle 2) and the Map Navigation state atmospheric spec (Section 2). The "exposed, parched, unhurried, surveilled, boundless" register for the map state is exactly the atmospheric tone this reference occupies. The specific technique of dry, ash-white atmospheric graduation over blue-haze graduation is the production implementation of "parched" as a color decision.

---

### Cross-Reference Application Rules

These references are additive and non-overlapping in their prescribed scope:

| Reference | Scope in Wasteland Run | Out of Scope |
|---|---|---|
| Pacific Drive | Localized damage-mark placement logic (sprites) | Surface material fidelity; 3D camera proximity detail |
| Slay the Spire | Card scan-sequence spatial grammar | Illustrative card art; chromatic card identity range |
| Papers, Please | Stamped document typography register | Monochrome oppression; institutional grey palette |
| Darkest Dungeon | Value-first UI hierarchy under high screen density | Gothic shadow aesthetics; horror emotional register |
| The Road | Dry atmospheric graduation (no blue-haze horizon) | Narrative despair; absent directional signal |

**Production test for reference misuse:** If a reviewer looking at a new asset could describe it as "that looks like it's from [reference]," the reference has been applied too broadly. The correct production result is an asset that, when analyzed, can be traced to a specific technique borrowed from a reference — but that reads, overall, as Wasteland Run.

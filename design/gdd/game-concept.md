# Game Concept: Wasteland Run

*Created: 2026-04-18*
*Status: Approved*

> **CD-PILLARS Review**: APPROVED (after revision) 2026-04-18
> **AD-CONCEPT-VISUAL Review**: APPROVED (direction selected) 2026-04-18
> **TD-FEASIBILITY Review**: CONCERNS (accepted) 2026-04-18
> **PR-SCOPE Review**: UNREALISTIC → revised to Option A (accepted) 2026-04-18

---

## Elevator Pitch

> A run-based vehicular roguelike where you build a scavenged car, fight through wasteland biomes using chassis-specific card combat, and chase a mythologized refuge called Haven. Your car is your character — every part you earn makes it more yours, and every battle threatens to take it apart.

---

## Core Identity

| Aspect | Detail |
| ---- | ---- |
| **Genre** | Vehicular roguelike / deckbuilder |
| **Platform** | PC (Steam) |
| **Target Audience** | Mid-core to hardcore roguelike players; Slay the Spire / FTL audience |
| **Player Count** | Single-player |
| **Session Length** | ~30 min (average failed run), ~60 min (successful run) |
| **Monetization** | Premium |
| **Estimated Scope** | Large (10–12 months to Early Access, solo dev) |
| **Comparable Titles** | Slay the Spire, FTL: Faster Than Light, Pacific Drive |

---

## Core Fantasy

Your car is your identity. Every part you scavenge, install, and fight to protect makes it more yours. When a part breaks, it hurts — not just mechanically, but emotionally. You're not piloting a game avatar; you're keeping something alive that only exists because of your choices. The wasteland strips everything down to what matters: the vehicle, the road, and the next encounter. Haven is out there. Whether you reach it depends entirely on how well you read the world in front of you.

---

## Unique Hook

Like FTL: Faster Than Light, AND ALSO your ship is a physical car you can watch fall apart — and it fights back with a deckbuilder. The vehicle isn't an abstracted stat block. It's a chassis with individually slottable parts, visible damage states, and a subsystem architecture that shapes both your deck and your combat identity. Losing your weapon mount isn't a number going down. It's watching the gun fall off your hood.

---

## Player Experience Analysis (MDA Framework)

### Target Aesthetics (What the player FEELS)

| Aesthetic | Priority | How We Deliver It |
| ---- | ---- | ---- |
| **Sensation** (sensory pleasure) | 5 | Card play feedback, part damage VFX, engine audio, RUST ICON visual style |
| **Fantasy** (make-believe, role-playing) | 2 | Vehicle-as-character identity, chassis archetypes, wasteland mythology |
| **Narrative** (drama, story arc) | 6 | Minimal — Haven as philosophical endpoint, not story-rich |
| **Challenge** (obstacle course, mastery) | 1 | Subsystem targeting depth, chassis identity, encounter reading, ~10% success rate |
| **Fellowship** (social connection) | N/A | Solo game |
| **Discovery** (exploration, secrets) | 3 | Chassis variety, card combinations, node events, biome enemy families |
| **Expression** (self-expression, creativity) | 4 | Build archetypes per chassis, deck evolution, part loadout identity |
| **Submission** (relaxation, comfort zone) | N/A | Not a relaxation game |

### Key Dynamics (Emergent player behaviors)

- Players will study enemy subsystem loadouts before committing to targeting priority each turn
- Players will develop chassis-specific mental models — Scout players think about position; Heavy Truck players think about endurance
- Players will agonize over part install/scrap decisions, especially when replacing a well-worn piece
- Players will learn biome enemy families over many runs and adjust routes accordingly
- Players will theorize about builds between sessions ("what if I had a flamethrower + Control deck?")

### Core Mechanics (Systems we build)

1. **Turn-based card combat** — hand of 4, 3 energy/turn, 5 card families (Precision, Assault, Control, Repair, Maneuver), position state (Behind/Ahead), subsystem targeting
2. **Modular vehicle build system** — chassis slots (structural, mobility, weapon, support) with individually tracked parts, damage states, and visual representation; parts feed both stats and deck
3. **Procedural horizontal node map** — 4 biomes, 6 node types (Unknown, Merchant, Treasure, Chopshop, Normal Encounter, Elite Encounter), seeded per run for determinism
4. **Chassis mastery meta progression** — XP earned each run feeds chassis-specific mastery track (10 levels); unlocks new parts, weapons, trinkets, deeper rarity pools, legendary structural sets
5. **Scrap economy** — Scrap Materials as primary currency; part install/scrap/repair decisions create persistent resource tension

---

## Player Motivation Profile

### Primary Psychological Needs Served

| Need | How This Game Satisfies It | Strength |
| ---- | ---- | ---- |
| **Autonomy** (freedom, meaningful choice) | Chassis selection, route decisions, part installs, card choices, subsystem targeting every turn | Core |
| **Competence** (mastery, skill growth) | Visible plating stacks show progress mid-fight; XP and mastery track show meta growth; encounter-reading skill develops across runs | Core |
| **Relatedness** (connection, belonging) | Minimal — single-player. Philosophical Haven loop creates connection to the journey rather than characters | Minimal |

### Player Type Appeal (Bartle Taxonomy)

- [x] **Achievers** — Chassis mastery tracks, Haven completion, legendary set collection
- [x] **Explorers** — Chassis variety, card combination discovery, Unknown node events, biome enemy variety
- [ ] **Socializers** — Not served (solo game)
- [ ] **Killers/Competitors** — Minimal; speedrun potential but not designed for it

### Flow State Design

- **Onboarding curve**: First run with Scout — lightest chassis, most forgiving, highest mobility. Simple starter deck (cost-1 cards dominant). No meta layer pressure on run 1.
- **Difficulty scaling**: Node map difficulty increases through biomes; enemy complexity and subsystem depth grow; elite/boss gates require strategic adaptation; mastery unlocks deepen the card pool over sessions
- **Feedback clarity**: Visible plating stacks on subsystem UI, HP bar, position indicator, XP counter post-combat, part damage states visible on vehicle silhouette
- **Recovery from failure**: ~30-min average run means fast restart; mastery XP carries forward on every run — even a failed run progresses the chassis track and feels productive

---

## Core Loop

### Moment-to-Moment (30 seconds)

Draw 4 cards from your chassis-shaped deck. Spend 3 energy. Choose: target an enemy subsystem with Precision to strip plating and work toward Offline; apply Assault pressure to Hull while wearing down Combat Armor; use Maneuver to shift position and set up follow-up plays; spend on Repair before a subsystem goes Offline; or use Control to disrupt the enemy's next turn. The enemy responds. Your parts may take damage. The primary tension on every turn: which enemy system to disable first changes everything — and the answer changes by enemy, by your current hand, and by what's already broken.

### Short-Term (5–15 minutes)

One combat encounter flows into a post-combat sequence: gain XP and Scrap, choose one card reward, review one found part (install replacing an existing part, or scrap it for materials), optionally repair damaged parts, return to the node map. The install/scrap decision is the encounter's emotional peak — every replacement is a small loss or a small gain. Then: choose the next node. An Unknown could be a windfall or a trap. An Elite is risk for better parts. A Chopshop could save the run. The "one more node" psychology lives here.

### Session-Level (30–120 minutes)

A full run navigates the node map across biomes, defeats biome gates (elites/bosses), and either dies or reaches Haven. Average failed run: ~30 minutes. Successful run: ~60 minutes. Natural stopping points occur at biome transitions and after boss encounters. The hook that makes players think about the game when not playing: the specific build they're running and what part they'd install next if they find it.

### Long-Term Progression

Chassis mastery tracks (10 levels per chassis, 1000 XP per level; average run grants 100–500 XP). Each level unlocks new parts, weapons, trinkets, deeper rarity pools, or legendary set availability. The mastery track IS the between-run upgrade economy — XP is the currency, levels are the purchase points. End reward: chance to find a legendary structural set for that chassis — rare, visually dramatic, individually strong, devastatingly powerful as a full set.

### Retention Hooks

- **Curiosity**: "What does an Engine-focused Scout build look like at mastery 7?" / "What's in the Unknown node before the biome boss?" / "What is Haven?"
- **Investment**: The specific part loadout of the current run — you've earned those pieces
- **Mastery**: Getting further into Biome 3, learning enemy targeting priorities, reading position threats
- **The loop**: Haven as philosophical endpoint — "everything happens in a pattern" — stimulates another run rather than resolving the journey

---

## Game Pillars

### Pillar 1: Vehicle as Character
Your car is your class, your build, and your emotional investment. Part loss should feel like losing a limb, not swapping a tool.

*Design test*: If a mechanic doesn't increase the player's attachment to their specific vehicle, it doesn't belong in the core loop. If a part can be replaced with zero emotional cost, the repair economy is too cheap.

### Pillar 2: Chassis Identity
Scout, Assault, and Heavy Truck play fundamentally differently at every layer — card rewards, part drops, and viable strategies must feel chassis-native. Each chassis must have at least one strategic archetype that is non-viable on the other two.

*Design test*: If a player can run the same strategy on all three chassis and achieve comparable success, Chassis Identity is failing. Playtesting target: every chassis has one build path that is mechanically impossible on the other two.

### Pillar 3: Read to Win
Victory comes from reading the enemy, your current build, and the options luck provides — not from brute force. Luck creates the run's variety; skill determines what you do with it.

*Design test*: When a player loses, they should be able to say "I misread what I had" or "I took the wrong gamble." Every combat encounter must have at least one decision the player could have made differently to improve the outcome, even in a losing run. If the player had no meaningful decisions, the encounter design is failing.

### Pillar 4: Scarcity with Agency
The game runs on three distinct resource domains, each with its own job and its own decision space: **Scrap** governs the build/repair economy (parts, install, scrap, repair); **Fuel** governs routing pacing (map movement under the advancing storm); **Energy** governs combat (per-turn card spending). Each domain must always feel tight, each must produce genuine hesitation, and the three must never collapse into a single pool — a tense combat turn must never feel like a travel cost, and a hard route decision must never feel like a repair decision.

*Design test*: If the correct resource decision is always obvious within a domain, the pillar is failing for that domain. If players stop noticing *which* resource they are spending — if Fuel, Scrap, and Energy start to feel interchangeable — the three-domain separation is failing. A well-designed resource moment produces genuine hesitation *within its own domain* (do I burn Fuel to explore or Fuel to escape the storm?), not across domains (do I spend combat Energy on a travel action?).

### Pillar 5: Route Reflects Vehicle State
The vehicle's current condition and build shape which routes are viable and which are too dangerous. The map is not a neutral backdrop — it interacts with what your car can and can't do right now.

*Design test*: If a player's vehicle damage and current loadout have no effect on route selection, this pillar is failing as a vehicle game pillar. A destroyed Mobility subsystem should change the calculus of taking an Elite encounter node.

### Anti-Pillars (What This Game Is NOT)

- **NOT story-driven**: We will not build rich narrative content, voiced characters, or branching dialogue. It would dilute the mastery focus and is unsustainable solo. Haven is a philosophical endpoint, not a narrative payoff.
- **NOT a reflex game**: We will not add real-time elements, QTEs, or reaction-based mechanics. It would undermine "Read to Win" and alienate the Slay the Spire audience.
- **NOT a real-time map**: The sandstorm that chases the player across the node map advances on player action (node commits and defined tick events), never on wall-clock time. The map respects "NOT a reflex game" at the world layer the same way combat respects it at the encounter layer. Storm pressure is *entropy the player can read*, not a timer the player must race.
- **NOT a power fantasy**: We will not make builds feel invincible or enemies feel trivial. Unchallenged power destroys the scarcity tension and the meaning of winning.
- **NOT a management sim**: We will not add persistent base-building, camp management, or complex between-run menus. The run is all you have. The car is all you are.
- **NOT a simulation**: Physics, fuel consumption, and damage are stylized for game feel, not realistic. Pacific Drive's simulation fidelity is not the model — FTL's abstraction is.

---

## Visual Identity Anchor

**Selected Direction: RUST ICON**

> **One-line visual rule**: Every element is treated as a found object — weathered, stamped, and worn, but legible as a silhouette from ten meters.

**Supporting visual principles:**

1. **Trophy silhouettes** — Each chassis has a distinct geometric signature readable at thumbnail size. Scout: low and angular. Assault: wedge. Heavy Truck: brutalist rectangle with bolted mass. The silhouette IS the chassis identity. *Design test*: Can a player identify their chassis from a 48×48 icon? If not, the silhouette isn't strong enough.

2. **Scarcity palette** — Base: iron oxide red-brown, bleached sand, ash white, tarnished steel grey. One high-chroma biome accent (cracked earth = brittle amber; toxic flats = sickly yellow-green; ruins = cold concrete blue; Haven approach = ember orange). Warm = danger/heat/aggression. Cool = scarcity/death/distance. The ember orange of Haven is the only color in the game that reads as refuge. *Design test*: Does removing the accent color make the biome feel generic? If not, the accent isn't doing enough work.

3. **Color as information (borrowed from SIGNAL DIAGRAM)** — Card combat UI uses system-state color language: green = functional, amber = degraded, red = critical/offline. A player reading a combat screen must be able to identify subsystem health from color alone without reading text. *Design test*: Can a colorblind-mode player still read system state? Ensure color is never the sole signal.

**Color philosophy summary**: The world has been bleached and rusted to its essential forms. The palette enforces the scarcity of the world. Every resource, part, and route feels precious because the visual vocabulary says "not much left." Haven's ember orange is earned — it's the first warm, welcoming color the player has seen in hours.

*Art Director (AD-CONCEPT-VISUAL) — Direction selected: RUST ICON. No blockers. Art bible to be authored before GDD writing begins.*

---

## Inspiration and References

| Reference | What We Take From It | What We Do Differently | Why It Matters |
| ---- | ---- | ---- | ---- |
| **Slay the Spire** | Card combat architecture, hand/energy economy, reward flow (card + relic after combat), run structure | Vehicle replaces character; subsystem targeting adds spatial targeting layer; chassis replaces class | Validates card roguelike audience size and mechanical depth ceiling |
| **FTL: Faster Than Light** | Subsystem management, route tension, permadeath philosophy, "ship is character" feeling | Physical visual vehicle replaces abstract ship grid; deckbuilder replaces direct firing controls | Validates subsystem mechanic and the emotional weight of ship loss |
| **Pacific Drive** | Vehicle-as-character emotional attachment, tactile part physicality, scavenging satisfaction | Turn-based card combat replaces real-time driving; roguelike run structure replaces open world | Validates that players form emotional bonds with vehicles they've built |
| **Mad Max (films)** | Wasteland mythology, vehicular scale, scrap culture, the road as protagonist | Roguelike structure; Haven as philosophical loop rather than Fury Road's linear destination | Aesthetic validation; tone and world framing |

**Non-game inspirations**: The road mythology of Mad Max: Fury Road — specifically the framing of the road as an eternal, indifferent force and the vehicle as the only sanctuary. The philosophical concept of eternal recurrence (Nietzsche) as the in-world explanation for the roguelike loop: Haven is real, but the road always begins again.

---

## Target Player Profile

| Attribute | Detail |
| ---- | ---- |
| **Age range** | 20–38 |
| **Gaming experience** | Mid-core to hardcore |
| **Time availability** | 30–60 min sessions; weekday evenings, longer weekends |
| **Platform preference** | PC (Steam) |
| **Current games they play** | Slay the Spire, FTL, Hades, Monster Train |
| **What they're looking for** | Tactical depth in a game that respects session time; a roguelike with strong thematic identity and meaningful build variety; something to theorycraft between sessions |
| **What would turn them away** | Randomness that overrides skill; real-time pressure; grindy progression without meaningful unlocks; sparse content at launch |

---

## Technical Considerations

| Consideration | Assessment |
| ---- | ---- |
| **Engine** | Unity LTS (pin immediately; do not use Tech Stream) |
| **Key Technical Challenges** | Card + subsystem state machine (complex but well-solved in genre); visual part system scope (see risk); save versioning discipline |
| **Art Style** | 2D stylized — RUST ICON direction (found-object weathered silhouettes, scrap-metal UI framing) |
| **Art Pipeline Complexity** | Medium — modular vehicle part system; part damage uses procedural overlays + reduced visible slot count (Option D: ~40–60 key assets vs. 360+ full-fidelity) |
| **Audio Needs** | Moderate — engine audio, card play feedback, combat hit feedback, biome ambient; no adaptive music system in MVP |
| **Networking** | None |
| **Content Volume (EA)** | ~150–200 cards across Scout + Assault; ~40–60 visible part assets; 3 biomes; ~20+ enemy types; 10+ trinkets; 6 node types |
| **Procedural Systems** | Node map (seeded per run, deterministic); loot table generation (weighted, chassis-biased); combat armor threshold generation |

---

## Risks and Open Questions

### Design Risks
- Turn feel with 3 energy and cost-1 dominated starter decks may be too shallow in early runs before the deck evolves
- Position mechanic (Behind/Ahead) may not feel meaningful enough without strong enemy variety that punishes/rewards position
- ~10% success rate may frustrate new players before mastery hooks engage; onboarding run must feel productive even in failure

### Technical Risks
- Visual vehicle part system scope — mitigated by Option D (procedural damage overlays + 5 key visible slots); requires formal ADR before any art production begins
- Card state machine debugging complexity — mitigate with in-game combat log and state inspector from day one; plain C# model separate from MonoBehaviours
- Save system versioning — mitigate with explicit version numbers, atomic writes, N=3 auto-backup from day one; single schema change without version bump can create launch review damage
- Card content volume (~200 cards needs art, balance, VFX, SFX) — plan production schedule explicitly; build balance tooling (headless sim runner) early

### Market Risks
- Card roguelike market is competitive (Slay the Spire, Monster Train, Cobalt Core) — vehicular identity is the differentiator; must be legible in store page thumbnail
- Small comparison set for vehicular roguelikes — less validation but clearer category ownership
- Solo dev shipping quality perception — mitigate with Early Access community building and public roadmap

### Scope Risks
- Heavy Truck (3rd chassis) and Biome 4 deferred to post-launch — must be positioned as roadmap content, not missing content, at EA launch
- Balance/tuning time consistently underestimated in card games — protect at least 6–8 weeks of dedicated balance time before EA
- Burnout risk in months 7–10 (content grind phase) — identify milestone checkpoints; scope permission to cut further if velocity drops

### Open Questions
*(Preserved from source GDD — to be resolved during prototype and playtesting)*
- Exact Combat Armor threshold conversion table (Overall Armor → Combat Armor pool)
- Exact Weld card variants and plating restoration values
- Which cards or effects can cause full part breakage (vs. Offline only)
- Final Ammo economy design (support builds without feeling taxing)
- Final part acquisition randomness calibration
- Exact legendary structural set bonuses per chassis
- Final biome enemy rosters and boss structure
- Final survival slot counts by chassis
- Exact boss encounter rules (post-playtesting)
- Shop inventory logic for set pieces vs. normal parts

---

## MVP Definition

**Core hypothesis**: Turn-based card combat with subsystem targeting and chassis-specific deck identity creates a 30-minute run loop worth repeating — where skill visibly improves outcomes across sessions.

**EA Scope (Option A — accepted after PR-SCOPE review):**
- Scout chassis (complete)
- Assault chassis (complete)
- 3 biomes with biome-specific enemy families and node distribution
- All 6 node types (Unknown, Merchant, Treasure, Chopshop, Normal Encounter, Elite Encounter)
- Full card families for Scout and Assault (~75–100 cards per chassis pool)
- Chassis mastery track (10 levels × 2 chassis) — full unlock progression
- Haven ending (philosophical loop — "everything happens in a pattern")
- Core UI: map view, combat view, post-combat flow, part inspect, mastery track

**Explicitly NOT in EA (post-launch roadmap):**
- Heavy Truck (3rd chassis) — highest scope-per-risk; ships as free update
- Biome 4 — ships as free update alongside or after Heavy Truck
- Legendary item sets — deferred to Full Vision
- Adaptive audio system
- Controller support (investigate at vertical slice)
- Full enemy roster depth per biome

### Scope Tiers

| Tier | Content | Features | Target |
| ---- | ---- | ---- | ---- |
| **Prototype** | Scout chassis, 1 biome, ~20 cards | Core combat loop, post-combat flow, no meta layer | Month 1–2 |
| **Vertical Slice** | Scout + Assault, 2 biomes, full card families | All node types, basic mastery meta layer | Month 3–5 |
| **EA / MVP** | Scout + Assault, 3 biomes, full mastery, Haven | All features listed above, EA-polish quality | Month 10–12 |
| **Full Vision** | + Heavy Truck, + Biome 4, legendary sets | Complete chassis roster, full biome set | Post-launch |

---

## Next Steps

- [ ] Run `/setup-engine` to configure Unity LTS and remove stale Godot reference files
- [ ] Run `/art-bible` — establish RUST ICON visual identity before any GDD system writing begins. Art bible gates asset production.
- [ ] Run `/design-review design/gdd/game-concept.md` to validate concept completeness
- [ ] Run `/map-systems` to decompose concept into individual systems with dependencies and priority tiers
- [ ] Run `/design-system` for each MVP system (combat, vehicle build, node map, meta progression, loot/economy)
- [ ] Run `/create-architecture` to produce the master architecture blueprint and Required ADR list
- [ ] Run `/architecture-decision` for each ADR — starting with visual part system scope (ADR required before any art production)
- [ ] Run `/gate-check` to validate readiness before committing to production
- [ ] Run `/prototype combat` to validate the card + subsystem state machine before full implementation
- [ ] Run `/playtest-report` after prototype to validate core hypothesis
- [ ] Run `/sprint-plan new` to plan the first sprint

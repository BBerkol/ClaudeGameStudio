# Technical Director — Build Order to MVP Vertical Slice

**Date:** 2026-06-08
**Author:** technical-director (consultative)
**Companion:** `production/td-inventory-2026-06-08.md`
**Strategic anchor:** `design/gdd/game-concept.md` §MVP Definition + §Pillars; `design/gdd/systems-index.md` §Dependency Map; ADR-0011 (no-bridges) + ADR-0012 (sum-of-parts armor).

**Rule of this document:** Every milestone ships a *playable end-to-end thing* AND realizes one or more accepted ADRs / GDD systems in canonical 1.0 shape. No bridges. No stopgap helpers. Placeholders for art/audio only. If a milestone requires a bridge to fit, the milestone is too big — split it.

**Reading the milestones:** Each one is small enough to ship in ~1–3 focused weeks of solo dev, and large enough to produce a visible run-loop step. They are ordered by hard dependency, not by "what's easiest next." The end of the sequence is *not* EA — it is the vertical slice the `game-concept.md` MVP scope table calls Vertical Slice (Scout + Assault, 2 biomes, full card families, basic meta layer). EA-scope (3 biomes, full mastery, Haven, ~150–200 cards) is *beyond* this document.

---

## Milestone 0 — Layer 3 cut closeout (in flight)

**End-to-end thing playable after this ships:** Combat view is unchanged at the player level — the same Slay-the-Spire baseline as today — but the card hand orchestration is in its canonical 5-component shape (HandModelObserver / HandEventQueue / HandSequencer / HandBeat / HandLayoutEngine) with EditMode determinism coverage. No "dangling work" inheritance into Milestone 1.

**Systems implemented this milestone:**
- *No new system.* Final phases of the in-progress card-hand animation Layer 3 cut (Phase 3b → 3c → 3d → 3e per `production/polish-captures/2026-06-05-card-hand-layer-3-orchestration-cut.md`). Phase 3a is already shipped and awaiting eye-check per `production/session-state/active.md`.

**What is still placeholder:** Vehicle silhouettes are squares. Card art is placeholder. SFX absent. (Unchanged from today.)

**Canonical 1.0 shape compliance:**
- ADR-0002 (POCO / event-driven combat) preserved.
- ADR-0011 (no-bridges) enforced: capture lists Layer 2 single-class `ProcessEventPipeline` / `DrainDiscardBurst` / `DrainOneDraw` / `ReassignWidgetsToCards` for **deletion**, not retention. Phase 3d is the cut.
- HandLayoutEngine as pure-function single source of truth for arc + Z-order satisfies ADR-0011's "single code path per operation" requirement.

**Explicitly NOT building this milestone:**
- Any new combat-view polish. The five remaining "Combat HUD spec'd-but-unbuilt" items (HP damage pulse, death cascade, chase-rail parallax differential, motion burst, HP/Armor bar visual redesign) are **deferred**. Re-surface only if Milestone 1+ exposes a feel-breaking gap.
- Renaming `BuildLegacy` / `WireLegacyRefs` / `BuildIconLegacy` view-layer methods (real ADR-0011 misnaming, but cosmetic — deferred to Milestone 6).
- The combat-demo backlog (B1/U1/D1/D2) is killed today — *do not harvest into Milestone 0.* Any of B1/U1's information that's useful gets carried as a memory or capture note; the work is not done as part of Milestone 0.

**Exit criteria:**
1. `HandPipeline_Determinism_Test.cs` and all existing EditMode tests pass (`dotnet test` / Unity Test Runner green).
2. Eye-check passes per the capture (single-play, draw-card, end-turn refill, drag-back cancel, hover lift, drag-cast crosshair).
3. Layer 2 single-class pipeline code is **deleted** from `CombatHud.cs` (per capture's DELETE rows).
4. Capture is amended to `Status: COMPLETE` and committed.

**Estimated effort:** 2–4 focused days (Phase 3a is done; 3b/3c/3d are mechanical relocations; 3e is one new EditMode test).

---

## Milestone 1 — Smallest playable run-loop slice (Scout, three nodes, save between)

**End-to-end thing playable after this ships:** Start a run as Scout. See a tiny seeded node map (run-start → 3 nodes → Haven). Each of the 3 middle nodes is a Combat encounter against a Biome-1 enemy. Win or die. Between nodes: a Post-Combat Flow that shows scrap gained, presents a 2-card-reward pick (or skip), repair prompt if any subsystem is damaged, return to map. After committing to the next node, save fires. Close the game. Reopen it. The run picks up where it left off. Reach Haven → end-of-run screen → start a new run.

This is the smallest possible thing that contains: a deterministic run, a node map, a save, a between-fight loot pick, and a run boundary. It is the keel.

**Systems implemented this milestone:**
- **Node Map System** (`design/gdd/node-map.md`) — minimal: 3 vertical strips, 1 beacon per strip, plus Haven. No branching. No storm (StormFrontX stays at `-StormStartOffset` for the whole milestone). No Fuel cost yet. Pure graph + commit pipeline + deterministic seed. Lays the canonical Node Map facade (`INodeMapView`, `INodeMapMutator`, `BeaconData` POCO) so subsequent milestones can extend without refactor.
- **Save & Persistence** (`design/gdd/save-persistence.md` + ADR-0004) — minimal viable canonical shape: `SaveManager` passive orchestrator, `RunStateDTO` + `NodeMapStateDTO` (with `SystemId` const + `SchemaVersion` const), atomic temp-then-rename writes on a background `Task`, `link.xml` IL2CPP preservation, Newtonsoft.Json. **MasteryState NOT in scope this milestone.** The asymmetric blocking-dialog logic is implemented but mastery save target stays a single empty DTO.
- **Loot & Reward System** (`design/gdd/loot-reward.md`) — pure-function `GenerateRewards(context, seed) → RewardOffer[]` for *card rewards only* this milestone. No part drops yet (no parts to drop). No Fuel grants (no Fuel system). Scrap payout per fight as a flat per-biome value (`BiomeBaseScrap[1] = 15`).
- **Scrap Economy System** (`design/gdd/scrap-economy.md`) — minimal: `IScrapEconomy` facade, `ScrapStateDTO`, `Add` / `TryDeduct` / `TryRepair(slotId, freeRepair: false)` only. **No Convert. No Merchant. No tenure refund. No FreeValveConsumedThisVisit.** Just the spine.
- **Vehicle & Part System retrofit (1)** — `VehicleDefinitionSO` redesigned away from the legacy 5-shape SlotSpec fields per `docs/architecture/no-bridges-audit-2026-05-31.md` row 2 Phase-1/2 addendum. One-shot migrator for `Vehicle_Scout.asset` (allowed exception per ADR-0011). `PartDefinitionSO` introduced per ADR-0012 even if only Scout's starter parts use it (~5 part SOs is enough to retire the `_armorHp` literal and validate sum-of-parts at runtime).
- **Card System** retrofit: `ICardRewardGenerator` + `CardDraft` introduced (referenced by Loot). The reward picker (`CardRewardPicker.cs`) loses its `_offers` / `DestroyLegacyOffers` dual path — single-path canonical shape.
- **Post-Combat Flow UI** (`design/gdd/post-combat-flow-ui` is unscoped today — UX spec is "Not Started" per systems-index row 16). **MUST be authored in this milestone** as a minimal UX spec scoped to "card reward pick + scrap display + repair button + continue" only. Per `.claude/docs/coordination-rules.md`, UX work routes to `ux-designer`; the UX spec gates the implementation.

**What is still placeholder:**
- Art: Scout placeholder square + 3 enemy placeholder squares (already exist). Node map nodes are colored circles. Card art is placeholder text.
- Audio: none.
- Content depth: 1 biome (1 only), 3 enemies (existing roster), ~20 cards total (sufficient for a starter deck + 5–10 reward candidates).

**Canonical 1.0 shape compliance:**
- **ADR-0002** preserved (combat POCO unchanged).
- **ADR-0003** (deterministic RNG) extended: Node Map + Loot derive their RNG via `RunSeed ^ stepIndex` per the rule; CI grep gate must extend to the new files.
- **ADR-0004** *implemented for the first time.* Save ADR (open per architecture.md §7) must land before save code is written — see Question 1.
- **ADR-0008** (Addressables) *not yet exercised this milestone* — art is still placeholder squares. Defer Addressables integration to Milestone 4.
- **ADR-0011** (no-bridges) enforced: VehicleDefinitionSO redesign retires the 5-shape legacy fields in one slice (Phase 1/2 of the audit addendum). One-shot migrator deleted in same commit.
- **ADR-0012** (sum-of-parts armor) realized: `PartDefinitionSO` is the authoring shape; `armor_0.MaxHp` is computed, not authored.
- New ADRs required *before* this milestone's code lands (per `docs/architecture/architecture.md` §7 pending slate):
  - **"Vehicle POCO Sub-Model + Part Catalog" ADR** — already mostly authored across ADR-0012 + the existing GDD; needs a formal ADR.
  - **"Node Map Commit Pipeline" ADR** — commit-pipeline atomicity, `BeaconType` → handler dispatch, save-block during `HandlerActive`.
  - **"Loot & Reward Generation Pipeline" ADR** — pure-function contract, deterministic seed, banded-linear DSBonus (deferred until Loot grows — minimal scope is just rarity-weighted card pull this milestone).
  - **Save ADR** (the open follow-up to ADR-0004 covering IL2CPP/dispatcher/timer choice).

**Explicitly NOT building this milestone:**
- Storm advance (`StormFrontX` stays static).
- Fuel system (no `IFuelContainer`, no `SpendFuel`).
- Branching node graph (no depth lanes, no lateral surcharge).
- Merchant / Chopshop / Event / Rest node types.
- Part drops as loot. Only card drops.
- Multi-fight per node (1 enemy per Combat node).
- Mastery / XP / chassis unlock.
- Assault chassis.
- Biome 2 / Biome 3.
- Status Effect "subsystem" rewrite (current in-loop resolution stays).
- Tenure refund / Free valve / Purge / Convert in Scrap Economy.
- HostileTiltDelta tell (deferred until Node Encounter ships in Milestone 3).
- Combat HUD polish items (deferred to Milestone 6 unless feel breaks).

**Estimated effort:** 4–8 weeks. By far the biggest milestone in the document. Worth it — it converts the codebase from "tech demo of one system" to "end-to-end run loop with multiple systems wired through the canonical seams."

**Validation we got this right:** After Milestone 1, the user can answer: *"can I play 3 fights, see rewards, save mid-run, reload, and finish a run?"* with yes. The shape of every subsequent milestone is "add one resource / one node type / one chassis / one biome" rather than "build a new spine."

---

## Milestone 2 — Fuel + branching map + storm

**End-to-end thing playable after this ships:** The Scout's node map grows from 3 forward beacons to a real branching graph per the Node Map GDD (5 vertical strips per biome, 4–6 beacons per strip, gate funnel at strip 5). Fuel cost per commit; Scout's `×0.8` multiplier active. Storm advances per `ChassisStormCadence[Scout]=3` after combat commits, eating beacons from behind. Insufficient-fuel run-end is reachable. Pillar 4 (Scarcity with Agency) and Pillar 5 (Route Reflects Vehicle State) are mechanically present.

**Systems implemented this milestone:**
- Node Map full graph generation + storm + Fuel (the parts deferred from Milestone 1).
- **Fuel container on Vehicle** — `IFuelContainer` / `IFuelMutator` per the V&P GDD retrofit R10 (`ContainerCap`, overflow semantics).
- Scrap Economy gains Convert (Scrap↔Fuel — per `convert_verb_future_events` memory and Node Encounter GDD's Event payload reuse).
- **Map UI (UX Spec)** authored and implemented (currently "Not Started" per systems-index row 17). Scope: branching graph display, route selection, storm visualization, Fuel display.

**What is still placeholder:** Art per Milestone 1 baseline. Storm visual = simple sweeping fill, no Perlin noise band. Map node icons = simple shapes color-coded by beacon type.

**Canonical 1.0 shape compliance:**
- **Node Map Commit Pipeline ADR** referenced.
- Pillar 4's three-domain separation (Scrap / Fuel / Energy) is enforced at code level by separate facades; Fuel is **never** mutated inside combat (Card Combat retrofit R13 — already in GDD).
- Pillar 5's named subsystem constraints (RC-M1 Mobility-offline-locks-elite, etc.) deferred until Elite Combat node type lands (Milestone 4).

**Explicitly NOT building this milestone:**
- Elite encounters (no Elite beacon type yet, only Combat beacons in Milestone 1 + branching graph in Milestone 2).
- Merchant / Chopshop / Event / Rest beacon types (Milestone 3).
- Mastery / XP.
- Assault chassis (Milestone 5).

**Estimated effort:** 2–4 weeks.

---

## Milestone 3 — Node Encounter handler dispatch + non-combat node types

**End-to-end thing playable after this ships:** The 5 non-Combat beacon types (Merchant, Chopshop, Event, Rest, plus Haven as the run-end) all dispatch through canonical `INodeEncounterHandler` handlers. The player can buy parts at Merchant, swap parts at Chopshop, hit Treasure / Ambush / Windfall / Convert events, repair at Rest, end a run at Haven. Pillar 1's emotional install/scrap decision (Vehicle as Character) is finally exercised: the player has to choose what part to keep when scrap is tight and the next node demands something specific.

**Systems implemented this milestone:**
- **Node Encounter System** (`design/gdd/node-encounter.md`) — full handler dispatch, BeaconType → handler mapping, asymmetric HostileTiltDelta vector, `OnHandlerBegin`/`OnHandlerEnd` observer events.
- **Part Inspect UI** (currently "Not Started" per systems-index row 15) — UX spec + implementation. Install / scrap / compare panel. Per V&P GDD R12 (`IPartCatalog.PreviewInstall`).
- Loot & Reward gains *part drops* (no longer card-only) and *Fuel grants* in Event beacons. Per-slot cooldown (`PartDropCooldownNodes=3`) implemented.
- Vehicle & Part GDD retrofit R11 — `InstalledPart.StatModifiers` + `CombatsSurvived` (tenure counter) — Scrap Economy tenure refund finally has its required input.
- Scrap Economy gains **Tenure refund** + **Free valve** + **Purge** (full F.5 / F.6 of the GDD).
- Save adds `NodeEncounterStateDTO` + `LootStateDTO`. Auto-save blocked while `HandlerActive`.

**What is still placeholder:** All art. Event payload visualizations are text-card style.

**Canonical 1.0 shape compliance:**
- **Node Encounter Handler Orchestration ADR** referenced.
- **Loot & Reward Generation Pipeline ADR** referenced (full DSBonus banded-linear math now live).
- **Scrap Economy Transaction Facade ADR** referenced.
- Pillar 1 (Vehicle as Character) mechanically present for the first time — install/scrap at Chopshop, repair at Rest, emotional cost of tenure refund.

**Explicitly NOT building this milestone:**
- Mastery / XP / chassis-unlock track.
- Assault chassis.
- Biome 2 / Biome 3 enemy rosters.
- Elite Combat encounter rules (Milestone 4).
- Boss combat (Milestone 4).
- HostileTiltDelta accessibility-channel work (deferred per OQ-NE12 — pre-1.0 accessibility review).

**Estimated effort:** 3–5 weeks.

---

## Milestone 4 — Elite + Boss combat, biome gate, full Biome-1 vertical slice

**End-to-end thing playable after this ships:** The Scout runs a full Biome 1 vertical slice — 18–22 beacons, all 6 node types, Elite encounters (Pillar 5 route constraints fire — offline Mobility locks Elite routes), a Biome-1 boss at the gate funnel. Win the boss → biome transition screen → run ends in Haven (placeholder Biome 2 stub). This is the first time *all six node types* are wired and Pillar 5 has a mechanical effect on routing.

**Systems implemented this milestone:**
- EliteCombat beacon type with HP / damage scalars (`EliteHPScalar`, `EliteDamageScalar`).
- Boss combat rules — `BeaconType.EliteCombat + isBoss=true` per Node Map graph-gen invariant (AC-NM45c).
- One Biome-1 boss enemy (data-authored — see Question 3 for which one).
- Pillar 5 route constraints (RC-M1 Mobility, RC-E1 Engine, RC-W1 Weapon, RC-F1 Frame) implemented in Node Map.
- Biome-1 enemy roster expansion: from 3 to 5–8 enemies (data-authored via Enemy GDD's `EnemyDefinitionSO` + `BrainRulesetSO` — see Question 2).
- Combat HUD: intent display contract per Enemy System P5 + Combat HUD UX (was "NEEDS REVISION"). Pillar 3 (Read to Win) has its information channel.

**What is still placeholder:** Biome 2 / Biome 3 are not implemented. End-of-Biome-1 dumps to a placeholder "You reached Haven (placeholder)" screen.

**Canonical 1.0 shape compliance:**
- **Enemy Data & Brain Contracts ADR** referenced.
- Pillar 3 (Read to Win) has full mechanical realization for the first time (intent display + Pillar 5 route constraints).

**Explicitly NOT building this milestone:**
- Mastery / XP carrying between runs.
- Assault chassis.
- Biome 2 / Biome 3 enemy data.
- Adaptive audio. SFX still minimal.
- Accessibility OQ-NE12 (deferred per the Node Encounter accessibility gate).

**Estimated effort:** 3–6 weeks (Enemy data authoring + boss design + Pillar 5 constraints are the long poles).

---

## Milestone 5 — Assault chassis + chassis-differentiated content

**End-to-end thing playable after this ships:** The player can pick Scout OR Assault at run start. Assault's chassis identity is mechanically distinct per Pillar 2 — different starter deck, different chassis multiplier (`×1.0` Fuel cost vs Scout's `×0.8`), different `ChassisStormCadence` (2 vs 3), different part loadout, different combat feel. Pillar 2 (Chassis Identity) can be design-tested for the first time.

**Systems implemented this milestone:**
- Assault chassis as second `IFrameLayout` archetype.
- Chassis-specific starter deck SO + reward pool bias.
- Chassis Identity System (Vertical Slice tier per `systems-index.md` row 11) — minimal data-driven shape per C6 ("data-driven for EA"): card pool biases + passive stat modifiers via declarative data contract. **No bespoke `IChassisAbility` hooks per C6.**
- Chassis-select screen (placeholder UI).

**What is still placeholder:** Assault vehicle art = placeholder square (visually distinct color from Scout). Audio absent.

**Canonical 1.0 shape compliance:**
- Pillar 2 (Chassis Identity) mechanically realized for the first time.
- C6 honored — chassis identity is data, not code-branched per chassis.

**Explicitly NOT building this milestone:**
- Heavy Truck (per `game-concept.md` MVP scope — post-launch).
- Mastery / XP.
- Biome 2 / Biome 3.

**Estimated effort:** 2–4 weeks.

---

## Milestone 6 — Combat View / HUD canonical cleanup + Combat HUD polish backlog

**End-to-end thing playable after this ships:** The view layer no longer carries `BuildLegacy` / `WireLegacyRefs` / `BuildIconLegacy` / `DestroyLegacyOffers` bimodal authored-vs-runtime branches. Single canonical path: prefab-authored, fail-fast if a serialized ref is missing. The previously-deferred Combat HUD polish items (HP damage pulse, death cascade, chase-rail parallax differential, motion burst, HP/Armor bar visual redesign) ship if they still feel needed.

**Systems implemented this milestone:**
- None new. Existing systems are brought to canonical 1.0 shape at the view layer.
- ADR-0011 audit row 11 (view-layer dual paths) closed.

**What is still placeholder:** All art still placeholder squares — that's a separate user-directed integration sprint.

**Canonical 1.0 shape compliance:**
- ADR-0011 fully honored at the view layer (last remaining bridge debt retired).

**Explicitly NOT building this milestone:**
- Mastery / XP / progression.
- Biome 2 / Biome 3.
- Asset integration.

**Estimated effort:** 1–3 weeks.

---

## Milestone 7 — Biome 2 + Biome 1 content depth

**End-to-end thing playable after this ships:** A full 2-biome run. Biome 2 has its own enemy family (5–8 archetypes), node distribution, palette accent. The gate funnel between Biome 1 and Biome 2 is real. Pillar 3 (Read to Win) gets tested across biomes — Biome 2 introduces new enemy intents the player has to learn.

**Systems implemented this milestone:**
- **Biome System** (Vertical Slice tier per `systems-index.md` row 12) — minimal: 2 biomes for VS, enemy family assignment, difficulty scaling curve, node distribution weights, accent palette rules.
- Biome-2 enemy roster (5–8 archetypes — data-authored via the Enemy SO authoring layer from Milestone 4).
- Card pool expansion to ~75–100 unique cards (sufficient for VS — full EA scope of ~150–200 is a later content sprint).

**What is still placeholder:** Biome 3 not implemented. Mastery still absent.

**Canonical 1.0 shape compliance:**
- The Vertical Slice line of `game-concept.md` MVP table is met (Scout + Assault, 2 biomes, full card families for both, all node types, basic mastery meta layer pending Milestone 8).

**Explicitly NOT building this milestone:**
- Biome 3 (EA-tier).
- Heavy Truck (post-launch).
- Mastery (next milestone).

**Estimated effort:** 4–6 weeks.

---

## Milestone 8 — Meta Progression layer (Vertical Slice gate)

**End-to-end thing playable after this ships:** XP earned each run feeds chassis-specific mastery track. Failed runs still grant XP. Mastery unlocks deepen the chassis card pool. The "even a failed run progresses" psychological loop is real. **This closes the Vertical Slice milestone of `game-concept.md`.**

**Systems implemented this milestone:**
- **Meta Progression System** (Vertical Slice tier per `systems-index.md` row 13) — chassis mastery tracks (10 levels × 2 chassis), XP per run, unlock economy.
- MasteryState save category fully enabled — asymmetric blocking-dialog policy (per ADR-0004) lands for the first time.
- **Meta UI (VS UX Spec)** authored and implemented (currently "Not Started" per systems-index row 18).

**Canonical 1.0 shape compliance:**
- `game-concept.md` Vertical Slice scope met.
- ADR-0004's RunState/MasteryState asymmetry is fully exercised.

**Explicitly NOT building this milestone:**
- EA-scope content depth (Biome 3, full card volume to ~150–200, legendary sets — all post-VS).
- Heavy Truck.

**Estimated effort:** 2–4 weeks.

---

## Beyond Milestone 8 (NOT part of this build order)

Per `game-concept.md` MVP table, the gap from Vertical Slice → EA / MVP is:
- Biome 3
- Full card families for both chassis (~75–100 cards per pool)
- Full chassis mastery unlock progression
- Haven ending (philosophical loop)
- Core UI EA-polish quality
- Audio system
- Asset integration sprints (art swaps from placeholder squares to RUST ICON style)
- Tooling: Balance Sim Runner (per `systems-index.md` Tooling Deliverables — Month 5)

These are content + polish sprints, not new system architecture. They sit downstream of this document.

---

## Questions for User

These are decisions I cannot resolve from existing GDDs, ADRs, or `game-concept.md`. Each one is load-bearing for the milestone listed.

### Q1 — Milestone 1 prerequisite — Save ADR scope

The architecture document (`docs/architecture/architecture.md` §7) and the systems-index "next steps" note an *open pending* Save ADR that owns IL2CPP stripping config, `SynchronizationContext` / dispatcher choice, periodic-flush timer mechanism, dirty-flag semantics, concurrent-write queue topology, Newtonsoft allocation budget, launch-recovery latency budget, `Application.quitting` timeout bound, ThreadLocal disposal, and the `File.Move`/`Flush(true)` IL2CPP verification (OQ4 + OQ5).

**Question:** Do you want this ADR authored as a single "Save Implementation" ADR before any save code is written in Milestone 1, OR do you want save code authored against the existing ADR-0004 + Save GDD and the implementation-detail decisions surfaced as one-off ADRs (Save-IL2CPP ADR, Save-Threading ADR, etc.) as they come up?

**Why this matters:** ADR-0004 left these as named open questions. ADR-0011 (no-bridges) means we cannot ship Milestone 1 with a save backend that gets replaced or bridged later. We need the implementation decisions made before code lands.

**My recommendation:** One Save Implementation ADR before Milestone 1, scoped tight (5 decisions max). Multiple small ADRs fragment the discoverable rule set.

### Q2 — Milestone 4 prerequisite — Enemy authoring layer shape

The Enemy GDD assumes designer-side data authoring via `EnemyDefinitionSO` + `BrainRulesetSO`. Today every enemy (Skimmer, Shepherd, Dredge) is C# code-authored via the `EnemyArchetypes` static factory + per-archetype `IFrameLayout` C# class + brain composition in code. Scaling from 3 → ~8 enemies in Biome 1 alone is fine in code (8 archetypes), but Biome 2 / 3 (15–24 enemies total) starts to hurt.

**Question:** When does the Enemy SO authoring layer land?
- **Option A:** Milestone 4 (as I've written it). All Biome 1 enemy roster expansion goes through the SO layer. ~5 new enemies authored as SOs alongside the existing 3 left in code (or migrated as a one-shot per ADR-0011 exception #1).
- **Option B:** Defer to Milestone 7 (Biome 2 expansion). Author Biome 1's 5–8 enemies in code (consistent with existing 3). Bite the SO authoring layer when content volume actually hurts.
- **Option C:** Now (folded into Milestone 1 as an Enemy retrofit prerequisite). Most expensive up-front but eliminates any chance of bridge debt accumulating.

**My recommendation:** Option A. Code-authoring 8 enemies works; code-authoring 24 enemies is a content bottleneck (per systems-index High-Risk Systems row). Cutoff at Biome 1 is a clean line. Option C front-loads pain without payoff at Milestone 1 (Milestone 1 only needs 3 enemies, all already exist).

### Q3 — Milestone 4 — Biome-1 boss

The Enemy GDD biome-1 roster includes Skimmer, Iron Shepherd, Dredge already implemented (3 enemies). The Biome-1 boss is not nailed down — the design memory `project_dredge_fight_design.md` calls out a deep Dredge fight design (Phase 1 taunt, Phase 2 javelin-spin-minigun chain, Cut-Chain card spawns on Javelin hit), which sounds boss-grade.

**Question:** Is the Dredge — promoted to boss-scale per its Phase 2 design — the Biome-1 boss? OR is the Dredge a regular Elite and a separate new boss design owns Biome 1?

**Why this matters:** Milestone 4 needs to know which one to build. If Dredge is the boss, the Dredge implementation slot in the milestone is the boss work. If Dredge is Elite, we need a new boss design slot before Milestone 4 can scope.

**My recommendation:** Punt to creative-director / game-designer. I can present trade-offs once the design intent is clear, but the boss-vs-Elite call is a design decision, not architecture.

### Q4 — Milestone 0 — Layer 3 cut closeout vs Milestone 1 parallel start

The Layer 3 cut (Milestone 0) is estimated at 2–4 focused days. Milestone 1 has a much bigger surface (4–8 weeks). 

**Question:** Do you want Milestone 0 finished cleanly **before** any Milestone 1 work begins (sequential — no context-switch), OR do you want Milestone 0 closed by another agent / a parallel evening pass while you start Milestone 1 design / Save ADR authoring?

**My recommendation:** Sequential. Solo dev. The Layer 3 cut has an eye-check protocol that needs your attention. The "no dangling work" rule you cited today is the reason. Finish 3a → 3e in one chain.

### Q5 — Milestone 6 timing

I placed the Combat View / HUD canonical cleanup at Milestone 6 (after Assault, before Biome 2). It could go anywhere from Milestone 2 onward — there is no hard dependency.

**Question:** Is Milestone 6's position correct, OR do you want the view-layer `BuildLegacy` cleanup brought forward (e.g. between Milestone 1 and Milestone 2) to retire that bridge debt sooner?

**My recommendation:** Hold at Milestone 6 — earlier than 6 means re-touching the same view files multiple times as new systems land. Doing it once after Assault is the cheapest pass. But the user-stated "no bridges anywhere" rule could justify earlier. Your call.

### Q6 — Tooling: Balance Sim Runner timing

`systems-index.md` Tooling Deliverables locks the Balance Sim Runner at "Month 5 (end of VS)" — i.e. Milestone 7 / 8 territory. The argument is that balancing 150–200 cards by hand is ~8 weeks of grind without it.

**Question:** Is the Balance Sim Runner still on the roadmap, and does it slot at Milestone 7 / 8 or earlier? It is *not* a player-visible feature, so it doesn't fit the milestone-shape of this document — but it is real infrastructure that the EA-content sprint will lean on.

**My recommendation:** It does not fit this build-order (which is end-to-end-thing-playable shaped). When the EA-content sprint begins (post Milestone 8), the Balance Sim Runner is the first infrastructure piece. Track it as a separate tooling sprint, not a milestone.

---

## Document validation criteria

We will know this build order was right if:

1. After Milestone 1, no system in the codebase carries a "for-now / transitional / bridge / Legacy" pattern that isn't covered by ADR-0011 §Allowed exceptions.
2. Each milestone closes one or more accepted ADRs from `architecture.md` §7 pending slate, not creates new ones (except the Save Implementation ADR before Milestone 1).
3. No milestone surfaces a feature that requires a bridge to ship. If one does, the milestone is split before any code lands.
4. The user can answer "did this milestone produce a thing I can play?" with yes after every single one.
5. The Vertical Slice line of `game-concept.md` MVP table is met at Milestone 8 — and the gap from there to EA is content/polish sprints, not architecture.

If any of these break, the technical-director comes back to revise this document before continuing.

# Sweep B — Biome-2/3 Slot-In Blockers

**Audit date:** 2026-07-04
**Scope:** Can biomes 2 & 3 be shipped as CONTENT-ONLY additions (SO assets, prefabs, JSON), or do they require CODE changes?
**Gate criterion:** Anything requiring code changes to add a new biome = 1.0 BLOCKER.
**Reference:** `production/audits/2026-07-04-1.0-vision-snapshot.md` §5 invariants (1) biome as table, (2) enemy data-driven, (3) save schema versioned, (4) beacon handlers canonical vertical slices, (5) no bridges, (6) determinism.

---

## Executive Summary

The generator + node-map + save-schema layer is genuinely ADR-0015 shape and would accept a `Biome2Distribution.asset` today with **zero code changes** — that pillar is solid. The blockers cluster in three other places:

1. **Enemy roster is hardcoded** — `EnemyArchetypeId` enum + `EnemyArchetypes` switch statement + one static class per archetype (`DuneSkimmer.cs`, `IronShepherd.cs`, `Dredge.cs`) with compile-time HP/damage/intent constants. Adding a biome-2 enemy today = write new C# file + edit enum + edit two switches. **Not ADR-0015 shape.** (**BLOCKER**)
2. **Chassis roster is 1-of-3** — Only `Vehicle_Scout.asset` and `SmallFrameLayout.cs` exist. Assault and Heavy Truck (both promised in the 1.0 vision snapshot §3) have zero code, zero assets, zero frame layouts. This is not a biome-slot-in issue, but the vision doc explicitly calls it a 1.0 gate: "The roster is closed." (**BLOCKER**)
3. **Only 2 of 6 beacon handlers exist** — Combat (via `SceneEncounterBuilder` + `CombatController`) and Rest (via `RestPickerController`). Merchant / Chopshop / Event / EliteCombat have no controllers, no session verbs, no data shapes. Haven is a terminal-index sentinel only (no dedicated controller — `RunController.CommitNextBeacon` latches `RunStatus.Victory` in-place). Vision doc §7 locked "all 6 beacon-type slices land pre-Haven-playtest." (**BLOCKER**)

Also flagged: reward pools (`MilestoneRewardPools.Milestone1()`) and starter deck (`RunDeck.Milestone1Starter()`) live as hardcoded factories keyed by biome/milestone rather than by chassis or biome. `RunSceneHost.BuildScout` fallback pins the player to Scout regardless of chassis choice. Mastery save schema DTO does not exist yet (only the `IMasteryStateSerializable` interface).

The good news: the ADR-0015 dispatch skeleton is already in the right places — `BiomeDistributionSO`, `BiomeSceneBindingSO`, `BeaconActivator`, and the `_combatBeaconArchetypes` array on `RunSceneHost` are all data-driven. The 1.0 fix isn't a rewrite; it's converting the compile-time islands (enemy archetypes, chassis, reward pools) to the same SO-driven pattern the map layer already uses.

---

## 1.0 BLOCKERS

_(Must fix before biomes 2/3 can land as content-only drops.)_

### B1. Enemy archetype system is compile-time coded (invariant #2 violation)

**Files:**
- `Assets/Scripts/Combat/Archetypes/EnemyArchetypeId.cs` — 3-value enum (DuneSkimmer, IronShepherd, Dredge)
- `Assets/Scripts/Combat/Archetypes/EnemyArchetypes.cs` — dispatcher with two exhaustive `switch` statements (`BuildVehicle`, `BuildBrain`) that throw on unknown id
- `Assets/Scripts/Combat/Archetypes/DuneSkimmer.cs` — static class, all HP/damage/intent stats as `public const int`
- `Assets/Scripts/Combat/Archetypes/IronShepherd.cs` — same shape
- `Assets/Scripts/Combat/Archetypes/Dredge.cs` — same shape

**Current shape:** Adding an enemy today requires (a) new enum entry, (b) new `.cs` file with a static class, (c) two edits to `EnemyArchetypes` switches. `SceneEncounterBuilder.Build` resolves the archetype through the `_combatBeaconArchetypes` prefab array (that part is data-driven), but the vehicle+brain construction under it hits the compile-time dispatch.

**Biome-2 scenario break:** Designer wants to ship a biome-2 enemy "Sand Serpent" as a new prefab + `.asset` — cannot. Code change required.

**Remediation:** Convert to ADR-0015 data-table shape. Options (present at TD gate before implementation):
- **B1a**: `EnemyArchetypeDefinitionSO` per archetype (SlotStats, IntentPool, BrainKind, EnemyArchetypeId stringly-typed id). `EnemyArchetypeBinder` already carries `_slotStats` — extend to reference the SO. Retire the enum + the 3 static classes.
- **B1b**: Lighter — keep the 3 static classes for compile-time balance visibility, but move `EnemyArchetypeId` to a `string` id or a hand-registered `Dictionary<string, IEnemyArchetypeFactory>`. Preserves grep-friendliness, breaks the enum wall.
- **B1c**: Table-shaped brain composition (intent definitions as SOs, brain as a mix of composable intent-selectors). Bigger lift but true 1.0 shape.

Recommend TD verdict on which shape wins before implementation.

**Related:** `BeaconType`-oblivious `_combatBeaconArchetypes` array in `RunSceneHost` is already the right dispatch skeleton — the missing piece is the vehicle/brain construction under it. `SceneEncounterBuilder.cs:71` also hardcodes `beacon.Type != BeaconType.Combat` — needs to accept `EliteCombat` too (currently `throw`), separate blocker (see B3).

---

### B2. Chassis roster is 1-of-3 (vision §3 violation)

**Files:**
- `Assets/Resources/combat/Vehicles/Vehicle_Scout.asset` — only shipping chassis
- `Assets/Scripts/Combat/Archetypes/SmallFrameLayout.cs` — only shipping player frame
- `Assets/Scripts/CombatView/RunSceneHost.cs:736` — `BuildScout` hardcoded fallback wires Scout parts inline (`scout_machinegun`, `scout_flamethrower`, `scout_engine`, `scout_wheels`, `scout_frame`, HP 10/10/24/10/55, armor 1/1/2/2/14=20)
- `Assets/Scripts/Run/RunDeck.cs:76` — `Milestone1Starter()` is a Scout-tuned deck (weapon_0=MG=3dmg, weapon_1=Flame=6dmg — Scout's weapon layout)

**Current shape:** `VehicleDefinitionSO` is architecturally generic (`_parts: List<PartSlot>`) but only one asset exists. No `AssaultFrameLayout` / `HeavyTruckFrameLayout` — but the `IFrameLayout` interface + `HaulerFrameLayout` / `TinyFrameLayout` / `DredgeFrameLayout` prove the polymorphism holds. Chassis-swap infrastructure is ~50% built.

**Biome-2 scenario break:** Not directly a biome slot-in issue. But the 1.0 vision snapshot §3 locks "three chassis at 1.0, roster is closed." The Chassis Identity pillar's test (§6, "three testers on the 3 chassis cannot converge on one dominant build") is untestable with only Scout.

**Remediation:**
- Ship `AssaultFrameLayout.cs` + `HeavyTruckFrameLayout.cs` (mirror `SmallFrameLayout` shape; different slot counts / positions per Chassis Identity pillar). Or FrameLayoutSO assets (`FrameLayout_Assault.asset` etc) — the SO route is already supported.
- Ship `Vehicle_Assault.asset` + `Vehicle_HeavyTruck.asset` with authored part loadouts.
- Convert `RunDeck.Milestone1Starter()` → `RunDeck.StarterFor(ChassisId)` (or a `ChassisStarterDeckSO` per chassis, referenced by the chassis SO). Scout, Assault, Heavy Truck each get their own starter deck.
- Retire the `BuildScout` inline fallback on `RunSceneHost` (its stated purpose is EditMode-test-without-assets — replace with a headless-test-only Resources check or gate the fallback behind an `#if UNITY_INCLUDE_TESTS`).
- Main Menu chassis picker UI (currently: no `MainMenu*.cs`, no `ChassisPicker*.cs`) — separate vision-doc §3 gate.

**Note:** Enemy frame layouts (`HaulerFrameLayout`, `TinyFrameLayout`, `DredgeFrameLayout`) are already sibling-shape; adding player frame layouts is a mirror lift. This is the smallest of the three blockers to close.

---

### B3. Only 2 of 6 beacon-type handlers exist (invariant #4 violation)

**Files that exist:**
- `Assets/Scripts/CombatView/SceneEncounterBuilder.cs` — Combat handler (throws on non-Combat)
- `Assets/Scripts/CombatView/CombatController.cs` — Combat runtime
- `Assets/Scripts/CombatView/RestPickerController.cs` — Rest handler
- `Assets/Scripts/CombatView/BeaconActivator.cs` — dispatch layer (canonical, biome-agnostic — good)
- `Assets/Scripts/Run/Authoring/BeaconSceneBindingSO.cs` — 7-type dispatch table (good)
- `Assets/Scenes/Beacons/Combat.unity` — scene exists
- `Assets/Scenes/Beacons/Haven.unity` — scene exists
- `Assets/Scenes/Beacons/Merchant.unity` — scene shell exists, empty
- `Assets/Scenes/Beacons/Event.unity` — scene shell exists, empty
- `Assets/Scenes/Beacons/Chopshop.unity` — scene shell exists, empty

**Files that DO NOT exist:**
- No `MerchantController.cs`, no `MerchantSession`, no merchant inventory SO
- No `ChopshopController.cs`, no chopshop offer generator
- No `EventController.cs`, no `EventDefinitionSO` for text events
- No `EliteCombatController.cs` (nor a modifier path in `SceneEncounterBuilder` for EliteCombat — hard-throws on non-Combat at `SceneEncounterBuilder.cs:71`)
- No `HavenController.cs` — Haven is currently handled by `RunController.CommitNextBeacon:247` (arriving on a terminal-type beacon latches `RunStatus.Victory` in-place). Vision §3 says Haven has "philosophical loop text, mastery XP applied, back to Main Menu" — none of that ships.

**Current shape:** `BeaconActivator` will happily route to any beacon type per its SO binding, but the roots on the other side are empty scenes / not-yet-authored prefabs.

**Biome-2 scenario break:** Any biome-2 distribution that emits Merchant/Chopshop/Event/EliteCombat/Rest emits a beacon that hard-fails presentation. Biome 1 dodges this today by narrowing to `{Combat, Haven}` only, so the audit gate becomes: **can biome 2 add a beacon type today without a code slice?** Answer: no. Every non-Combat non-Haven beacon type needs a controller + session verb + data shape before it can ship.

**Remediation:** Five canonical vertical slices (per ADR-0015 discipline — real handlers, not stubs):
- **Slice α (EliteCombat)**: Modifier layer on `SceneEncounterBuilder` (accept `EliteCombat` at `SceneEncounterBuilder.cs:71`, apply elite tuning: HP mult, extra intent, different reward table). Blocks: `FlatScrapRewardSource.Generate` and `FlatCardRewardSource.Generate` also hard-throw on `beacon.Type != BeaconType.Combat` — both need to accept EliteCombat (or a `IsCombatBeacon()` helper).
- **Slice β (Merchant)**: `MerchantController.cs` on `MerchantRoot.prefab`, `MerchantInventorySO` (biome-narrowing per ADR-0015), scrap-spend verb on `RunSession`. Retire `_biomeDistribution.CombatArchetypes` narrowing pattern → mirror for merchant inventory.
- **Slice γ (Chopshop)**: `ChopshopController.cs`, `ChopshopOfferSO` (parts pool, per-biome narrowing), part-install verb.
- **Slice δ (Event)**: `EventController.cs`, `EventDefinitionSO` (text + choice tree + effect list — biome-narrowing pool), scrap↔fuel convert seam wired up (currently reserved per `IScrapEconomy` xmldoc).
- **Slice ε (Haven)**: `HavenController.cs` on `HavenRoot.prefab`, mastery-XP-apply verb (currently missing — see B4), main-menu return.

Each slice is a real vertical (controller + SO + session verb + tests). None can be a stub per ADR-0011.

---

### B4. Mastery save schema DTO does not exist (invariant #3 violation)

**Files:**
- `Assets/Scripts/Save/IMasteryStateSerializable.cs` — interface exists
- `Assets/Scripts/Save/SaveSystem.Write.cs:44` — `_masteryRegistry` dictionary exists
- `Assets/Scripts/Save/Dtos/` — no `MasteryStateDto.cs`, no `MasteryTrackDto.cs`
- `Assets/Scripts/Save/Adapters/` — no `MasteryStateSerializable.cs`

**Current shape:** ADR-0004's mastery-side pipeline is scaffolding without a concrete DTO. The `IMasteryStateSerializable` interface, the registry dictionary, and the load/write paths all exist but nothing implements the interface — nothing to serialize.

**Biome-2 scenario break:** Biome-2 introduces mastery unlocks per the vision doc §3 mastery track. Adding a biome-2 unlock today would need to (a) create the `MasteryStateDto` from scratch AND (b) wire the first serializable AND (c) bump schema versions. That's three concurrent concerns — the exact "half-shipped system that re-breaks" pattern the memory `feedback_overall_picture_thinking` warns about.

**Remediation:** Ship the mastery DTO + adapter + registration at Slice ε (Haven), before biome-2. Vision doc §3 says "Per-chassis XP progression, level readouts, unlock tree visible" — so `MasteryStateDto` should carry a `Dictionary<ChassisId, ChassisMasteryTrackDto>` where each track has `Xp`, `Level`, and `UnlockedIds: List<string>`. Biome-2 unlocks then add to `UnlockedIds` — pure data addition, canonical ADR-0015 shape, `SchemaVersion` bump on the DTO if the shape changes.

---

### B5. Reward pools hardcoded per milestone, not per biome/chassis (invariant #1+#2 violation)

**Files:**
- `Assets/Scripts/Run/MilestoneRewardPools.cs:45` — `Milestone1()` returns a fixed 8-card list, hardcoded per-card `CardDefinition` construction
- `Assets/Scripts/Run/FlatCardRewardSource.cs:50` — hardcodes `MilestoneRewardPools.Milestone1()` — no way to vary pool by biome
- `Assets/Scripts/Run/FlatScrapRewardSource.cs:21` — hardcodes `ScrapPerCombat = 10` for all combats regardless of biome or archetype

**Current shape:** Both reward sources implement clean interfaces (`IRewardSource`, `ICardRewardSource`) — so ADR-0013's composition seam is real. But the flat sources are Milestone-1-scoped and both classes are their own DTD; there's no data-narrowing SO to swap per biome. `MilestoneRewardPools.cs:20` xmldoc explicitly acknowledges "Milestone-2 / Milestone-3 pools land as additional static methods on this same class" — that's the compile-time expansion trap.

**Biome-2 scenario break:** Biome-2 card pool is a code addition (new static method + new source class), not a data edit. Reward-per-combat scaling by biome (players expect harder biomes to pay more) is a code edit.

**Remediation:** Two options —
- **B5a**: Add `RewardPoolSO` (card list, weighted) and `ScrapRewardSO` (per-archetype or per-beacon-type payout). Reference from `BiomeDistributionSO` or from a new `BiomeRewardConfigSO`. `FlatScrapRewardSource` + `FlatCardRewardSource` become `SORewardSource` implementations that read the SO the host passes at construction. Retire `MilestoneRewardPools`.
- **B5b**: Keep the "content in code" precedent per `MilestoneRewardPools.cs:8` xmldoc ("balance edits and merges resolve as line diffs instead of asset-YAML conflicts") — but add per-biome routing at the source level. `BiomeDistributionSO.RewardPoolKey: string` picks which static method. Same problem (adding biome 2 = new method) but less code churn.

Recommend B5a — matches ADR-0015 discipline and closes the compile-time expansion door. B5b just delays the problem.

---

### B6. Reward sources hard-throw on non-Combat (compounds B3)

**Files:**
- `Assets/Scripts/Run/FlatScrapRewardSource.cs:26-28` — `throw` when `beacon.Type != BeaconType.Combat`
- `Assets/Scripts/Run/FlatCardRewardSource.cs:45-47` — same
- `Assets/Scripts/CombatView/SceneEncounterBuilder.cs:71-73` — same
- `Assets/Scripts/Run/RunSession.cs:125-127` — `EnterCombat` accepts `Combat` OR `EliteCombat` (good — one path already fixed)

**Current shape:** `RunSession.EnterCombat` was updated to accept EliteCombat, but the reward sources and the encounter builder still hard-throw on non-Combat. This means EliteCombat handling is partially threaded — the run-model side accepts it but every consumer downstream rejects it.

**Biome-2 scenario break:** Biome 1 sidesteps this by never emitting EliteCombat (per `Biome1Distribution.asset`), but the moment biome 2 or a later biome-1-slice adds EliteCombat to its distribution the whole reward + builder chain fails.

**Remediation:** Add a `BeaconTypeExtensions.IsCombatBeacon()` helper or change the guards to `!= Combat && != EliteCombat`. Trivial fix but flagged separately because the audit is about **grepping** for these hard-throws before biome 2 lands.

---

## 1.0 CLEANUP

_(Works today, would need cleanup during biome-2 slice.)_

### C1. `_playerVehicleAsset.BuildVehicle("Scout")` hardcoded name at RunSceneHost.cs:368, 442

Even when the future chassis picker feeds a non-Scout `VehicleDefinitionSO`, the display name is force-overridden to "Scout." Small string bug but will surface if any UI reads Vehicle.Name.

### C2. Biome-1 flavor in comments / const names

Grep hits for "Biome 1 roster" (`RunSceneHost.cs:72,77`, `SceneEncounterBuilder.cs:85`) are comments only — not load-bearing, but should retitle to "current-biome roster" or reference `BiomeDistributionSO.DisplayName` at build time for the error messages. Also `BiomeDistributionSO.cs:49,111,157` has "biome_1_wasteland" in tooltips as an example; keep as example, not directive.

### C3. `NodeMap.Milestone1CombatArchetypes` referenced in xmldoc but obsolete

`RunDeck.cs:73` xmldoc references `NodeMap.Milestone1CombatArchetypes` as a pattern precedent — was retired at Slice 6. Update xmldoc.

### C4. `_biomeDistribution` inspector slot on RunSceneHost — biome switch is a hard rebake

`RunSceneHost` takes exactly one `BiomeDistributionSO` today. Biome-2 slot-in works with a manual rebake of `Run.prefab` (swap the SO reference), but a run that spans biome-1 → biome-2 would need the host to swap the distribution mid-run. Vision §7 doesn't specify inter-biome transition mechanics; deferred to biome-2 slice. Non-blocker for the "can biome 2 ship as content-only" question — but a biome-transition mechanic is a code slice regardless.

### C5. `RunSceneHost.BuildScout` (line 736) — hardcoded chassis-in-code fallback

Explicitly labeled as a headless-test fallback ("no VehicleDefinitionSO is wired AND no Resources/Combat/Vehicles/Vehicle_Scout asset exists"). Kept in code so EditMode tests can build a runnable host. Gate behind `#if UNITY_INCLUDE_TESTS` or move to a test-fixtures asmdef to remove it from shipping code path. Post-biome-2 cleanup — doesn't block biome-2 addition.

---

## Slot-in confirmed OK

_(Already ADR-0015 shape — new biome = new asset only, no code change required.)_

### K1. `BiomeWebGenerator` is content-agnostic

`Assets/Scripts/Run/BiomeWebGenerator.cs` — reads a POCO `BiomeGenerationInputs` record. Zero references to biome id, biome number, or specific beacon types beyond what the inputs table says to sample. `ValidateInputs` only enforces schema-level invariants (positive weights, non-Combat terminal). Deterministic per ADR-0003 (`runSeed ^ stepIndex ^ salt`) — invariant #6 holds.

Adding `Biome2Distribution.asset` today produces a valid biome-2 graph with **zero generator code changes**. Verified: the SO doesn't know it's biome 1; the generator doesn't know either.

### K2. `BiomeDistributionSO` is authorable-per-biome

`Assets/Scripts/Run/Authoring/BiomeDistributionSO.cs` — every field is per-asset serialized. `BiomeId` (persistence key), `TerminalBeaconType` (next biome's entry vs run exit), `AllowBidirectional`, `MaxEdgeLength`, `ReconnectRadius`, `BeaconCountRange`, `MapTheme`. All narrowing lives here.

### K3. `BeaconActivator` dispatch is beacon-type-oblivious

`Assets/Scripts/CombatView/BeaconActivator.cs:238` — the dispatch switch is on `BeaconSceneEntry.Mode` (AdditiveScene vs PrefabRoot), not on `BeaconType`. Adding a beacon type = SO entry + prefab root wired via authoring, no activator edit. **The dispatch skeleton is textbook ADR-0015.** Downstream controllers are the missing piece (see B3).

### K4. Save schema is per-DTO independent (invariant #3)

`Assets/Scripts/Save/SaveSystem.Load.cs:260` — `SchemaMismatch` is per-entry, per-`SystemId`. Adding biome-2 unlocks bumps `SchemaVersion` on the affected DTO in isolation; other systems' saves remain valid. Per-category recovery chain works.

`NodeMapDto`, `RunSeedDto`, `RunDeckDto`, `VehicleStateDto` all use `SCHEMA_VERSION` constants + `IRunStateSerializable` adapters. Pattern is set — biome-2 additions plug in cleanly (once B4 lands the mastery DTO).

### K5. `RunController` seed derivation is biome-agnostic

`Assets/Scripts/Run/RunController.cs:150-186` — `DeriveCombatSeed`, `DeriveRewardSeed`, `DeriveCardOfferSeed` all use `RunSeed ^ stepIndex ^ salt`. No biome-id term. Invariant #6 (determinism holds across biomes) is architecturally guaranteed.

### K6. `PartDefinitionSO` (ADR-0012) is data-driven

`Assets/Scripts/CombatView/Data/PartDefinitionSO.cs` — designer-authored PartId, SlotKind, MaxHp, ArmorContribution, sprite ref. `Vehicle.InstallPart(SO overload)` reads the SO. Biome-2 parts = new `.asset` files, no code. This layer is done.

Enemy archetypes should follow this shape (see B1 remediation).

### K7. `BiomeSceneBindingSO` covers all 7 runtime beacon types

Even though only Combat + Rest + Haven have controllers (B3), the SO surface has entries for all 7 (Combat, EliteCombat, Merchant, Chopshop, Event, Rest, Haven) per `BeaconSceneBindingSO.cs:11-22`. Biome-2 slot-in of a new beacon type doesn't require editing the SO's shape — only adding the entry.

Combined with K3, the dispatch layer is fully biome-2-ready. The controllers are the blocker.

---

## Investigation Log

- Read vision snapshot §5 (6 invariants) → gated audit against these.
- Read ADR-0015 → confirmed pattern shape (enum full, generator canonical, table narrows).
- Verified `BiomeWebGenerator.cs`, `BiomeDistributionSO.cs`, `BeaconActivator.cs`, `BeaconSceneBindingSO.cs` — all canonical.
- Grepped `EnemyArchetypeId` / `BeaconType` switches → found compile-time enemy dispatch (B1) and hard-throws on non-Combat beacons (B3, B6).
- Grepped `Milestone1` / `BuildScout` → found reward pools (B5) and chassis fallback (C5).
- Glob'd `Assets/Resources/**/Vehicle_*.asset` → only Scout (B2).
- Glob'd `Assets/Scripts/**/{Rest,Haven,Merchant,Chopshop,Event}*.cs` → only Rest exists (B3).
- Grepped `MasteryStateDto` in Save/Dtos → interface exists, no DTO (B4).
- Confirmed `NodeMapDto`, `RunSeedDto`, `RunDeckDto`, `VehicleStateDto` all have `SCHEMA_VERSION` constants + adapters (K4).

---

## Verdict Summary

**Six 1.0 blockers** (B1 enemy code-shape, B2 chassis roster, B3 beacon handlers 4/6 missing, B4 mastery DTO missing, B5 reward pools code-scoped, B6 reward + builder hard-throw on non-Combat).

**Five cleanup items** (C1–C5) — cosmetic or test-only, not blocking biome-2.

**Seven confirmed-clean** (K1–K7) — generator, distribution SO, dispatch skeleton, save schema, seed derivation, part SO, beacon binding SO. This is the ADR-0015 pattern working as intended.

**Sequencing recommendation** — if the user wants to reach "biome 2 ships as content-only," the natural order is:
1. B6 (grep fix, ~1hr).
2. B3 five vertical slices (Elite / Merchant / Chopshop / Event / Haven).
3. B4 lands with the Haven slice (mastery XP apply verb needs the DTO).
4. B1 (enemy SO conversion) — big lift but data-only cascade.
5. B5 (reward SO conversion) — smaller lift, mirrors B1 shape.
6. B2 (chassis roster) — parallelizable with B1 since chassis and enemy pipelines don't touch.

Each of the above should be preceded by a TD-briefing slice on shape (in particular B1, B4, B5 have real shape decisions to make — recommend `AskUserQuestion` calls at those gates rather than the audit committing to a specific option).

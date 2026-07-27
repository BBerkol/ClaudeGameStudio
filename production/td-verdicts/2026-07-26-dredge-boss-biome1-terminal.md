---
date: 2026-07-26
system: run/beacon-type + biome-1 terminal
author: technical-director
verdict: ACCEPT (Fork A)
---

# TD Verdict — Dredge-as-Boss / Biome-1 Boss Terminal

## Context

User design pivot 2026-07-26: pull Dredge out of the biome-1 Combat pool and make
it the biome-1 capstone. Haven moves to biome-3 terminal (future). Biome-1's
last node becomes a Boss encounter that engages the Dredge fight; the beacon
costs 5 fuel to reach ("5 fuel to reach and also make the dredge fight engage
on the last node").

The pivot introduces the concept of a *biome-terminal boss encounter* that is
distinct from Haven (safe rest / refuel) and from EliteCombat (mid-run tough
combat with card+scrap rewards). It also introduces the concept of a run-scoped
`BiomeComplete` state that is not the same as "Haven reached".

## Files at Risk

- `Assets/Scripts/Run/BeaconType.cs` — enum extension
- `Assets/Scripts/Run/BeaconData.cs` — archetype-required guard extended to include `Boss`
- `Assets/Scripts/UI/BeaconCopy.cs` — parallel arrays (Titles/Descriptions/CurrentDescriptions)
- `Assets/Scripts/Run/Authoring/MapBeaconStyleSO.cs` — default icon arrays (length 8→9)
- `Assets/Scripts/Run/Authoring/BiomeDistributionSO.cs` — new `_bossEncounter` field
- `Assets/Scripts/Run/Authoring/BossEncounterSO.cs` — **new file**
- `Assets/Scripts/Run/BiomeGenerationInputs.cs` — new `BossArchetype` nullable field on the input record
- `Assets/Scripts/Run/Authoring/BiomeGenerationInputsFactory.cs` — plumb `_bossEncounter?.BossArchetype` through
- `Assets/Scripts/Run/BiomeWebGenerator.cs` — terminal-emit branch reads `BossArchetype`; validation requires it when terminal is Boss and rejects it otherwise
- `Assets/Scripts/CombatView/RunSceneHost.cs` — Boss branch in `HandleBeaconActivated` + `BeginCombatForCurrentBeacon`; `AdvanceToNextBeacon` defers `OnRunComplete` on combat-shaped terminals; `NotifyRewardClaimed` fires `OnRunComplete` when the terminal beacon just resolved
- `Assets/Scripts/Run/RunSession.cs` — `EnterCombat` gate uses `IsCombatBeacon()` predicate (accepts Boss)
- `Assets/Scripts/Run/BeaconTypeExtensions.cs` — `IsCombatBeacon` extended to include `Boss` (all beacons that fire a `CombatLoop`)
- `Assets/Scripts/UI/CardRewardPickerController.cs` — `HandleContinueRequested` early-out when no `PendingCardOffer` (defensive — Boss path today still latches an offer, but the guard removes the "empty draft" degraded surface)
- `Assets/Resources/Run/Biomes/Biome1Distribution.asset` — drop Dredge from Combat pool + wire `_bossEncounter`; EliteCombat fuel cost 12→8 (user tuning 2026-07-26)
- `Assets/Resources/Run/Biomes/Biome1MapTheme.asset` — Boss icon slot (index 8, null-fallback → USS chip)
- `Assets/Tests/EditMode/Run/Biome1Distribution_AssetSanity_Test.cs` — terminal assertion Haven → Boss
- `Assets/Tests/EditMode/Save/NodeMapDto_round_trip_test.cs` — Boss round-trip (new test)

## Boss Beacon Archetype Decision

`BeaconData` gets `Boss` added to the archetype-required set alongside Combat and
EliteCombat. Rationale: the beacon's `EnemyArchetype` is the persisted enemy id,
not re-rolled at combat-build time (matches Slice-2 TD verdict pattern). This
keeps save round-trip fully deterministic — if a future biome ships multiple
boss variants via a table, the generator picks at map-build time and the beacon
persists the choice. `BossEncounterSO` on the biome asset stays as the *pool*
seam (currently one boss per biome, later N via table).

## ADRs at Risk

- **ADR-0004** (Save & Persistence): `BeaconType` ordinal used in `NodeMapDto` node types. Append-only preserves the wire format; string-enum-convert preserves the wire keys. Save version bump NOT required (payload shape unchanged, only enum surface grows).
- **ADR-0011** (No Bridges): We MUST NOT ship a bimodal Haven-or-Boss code branch. Data-flag pattern (SO field null vs. set) is the ADR-0015 canonical scope-narrowing shape.
- **ADR-0015** (Configuration Narrowing): This is exactly the intended pattern — full enum, canonical generator, biome-specific SO controls what actually spawns. The Boss beacon type is real for the whole runtime; biome-1 asset chooses to emit it.

## Forks Considered

**Fork A — new `BeaconType.Boss` enum value + `BossEncounterSO` sibling seam.** ✅ RECOMMENDED
- Adds `Boss = 8` (append-only, ordinal-safe)
- Adds `BossEncounterSO` (holds boss `EnemyArchetypeId` + display-copy override)
- `BiomeDistributionSO` gets `[SerializeField] BossEncounterSO _bossEncounter` (null → Haven cap; non-null → Boss cap at terminal position)
- `BiomeWebGenerator` reads `_bossEncounter` and emits `Boss` at the terminal node when the field is present
- Save round-trip: `Boss` serializes as `"Boss"` in JSON (StringEnumConverter); ordinal 7 preserved for Haven

**Fork B — reuse `EliteCombat` + `IsTerminal` flag.** ❌ REJECTED
- Bimodal path (`if (EliteCombat && IsTerminal) → boss handling; else → normal elite`)
- Conflates two different design intents into one enum value
- Violates ADR-0011 forbidden pattern #3 (bimodal paths)

**Fork C — overload `Haven` as the boss-then-refuel node.** ❌ REJECTED
- Conflates "victory" with "safe rest" in save data + copy layer
- Would require Haven copy to be past-tense-context-aware, or two Haven variants (which is just Fork A wearing a hat)

## TD Verdict

**ACCEPT Fork A.** Ship in three commits:

**C1 — enum + parallel arrays + DTO round-trip test.** Append `Boss = 8` to `BeaconType`. Extend all enum-parallel structures (BeaconCopy Titles/Descriptions/CurrentDescriptions, MapBeaconStyleSO default sprite arrays). Add a NodeMapDto round-trip test asserting a `Boss`-typed node survives serialize → deserialize with type intact. No behaviour change yet — this is the shape-only commit.

**C2 — BossEncounterSO seam + generator + biome-1 asset wiring.** Create `BossEncounterSO` (single-purpose SO holding boss `EnemyArchetypeId` reference and optional copy override for the current-beacon popup). Add `_bossEncounter` field to `BiomeDistributionSO`. Extend `BiomeWebGenerator` to emit `Boss` at terminal position when `_bossEncounter` is present. Author `DredgeBoss.asset`. Update `Biome1Distribution.asset` — remove Dredge from `_combatArchetypes` (weights become 1:1 for Dune/Iron); wire `_bossEncounter` → `DredgeBoss.asset`; add Boss fuel cost = 5 at index 8. Update `Biome1Distribution_AssetSanity_Test` to assert terminal is Boss.

**C3 — encounter builder + BiomeComplete phase + victory placeholder.** Extend the encounter builder (RunSceneHost / SceneEncounterBuilder) with a `Boss` branch that reads the biome's `_bossEncounter` and builds the fight from its archetype. Add `RunPhase.BiomeComplete` distinct from Haven. On Boss defeat, transition to `BiomeComplete` and show a placeholder demo-complete/victory screen (biomes 2/3 aren't built — we're at end of the demo). Grep for any hard-coded Dredge references in enemy pools and remove.

## Non-Goals

- **Boss archetype tuning.** Dredge is already boss-tuned; no combat-model changes required.
- **Multiple boss variants per biome.** One boss per biome for now; the SO seam supports growth to a boss table later (ADR-0015 pattern — the biome asset would swap `_bossEncounter` for `_bossEncounterTable`).
- **Haven relocation to biome 3.** Biomes 2/3 aren't built. Haven stays as a valid enum value; no biome-1 emits it after this pivot; future biome-3 asset sets `_terminalBeaconType = Haven` and leaves `_bossEncounter = null`.
- **Boss-specific reward table.** Boss uses standard Combat reward for now; card offers, scrap, and part drops as if it were an EliteCombat. Boss reward specialization is a follow-up slice.

## Verification Bar

- All existing EditMode tests pass (Biome1Distribution asset sanity updated, fuel-cost array test remains).
- New NodeMapDto round-trip test: create a NodeMap with a `Boss` beacon, serialize, deserialize, assert `TerminalType == BeaconType.Boss` and beacon-list contains the Boss node with correct ordinal.
- Biome 1 playtest: run from Start → boss terminal, Dredge fight engages, victory transitions to placeholder demo-complete screen. No Haven beacon appears on biome-1 map.
- No hard-coded Dredge references in `_combatArchetypes` anywhere in the codebase.

## Signed Off By

technical-director, 2026-07-26

# 2026-07-28 — Biome 1 Event Distribution + Map Theme Icon Wiring

## Change summary

Node Encounter Event slice (2026-07-27) shipped code + payloads + prefabs but
never wired Event into the biome-1 non-terminal beacon pool. Generator emitted
only Combat + Rest. Also, the shared `Map Node Icons.psb` atlas ships three
Event sprite variants (Event1/2/3) but `Biome1MapTheme.asset` only referenced
one fileID and had empty alternates — every Event beacon on the map would
render identically.

Two config-shaped fixes:

1. **`Assets/Resources/Run/Biomes/Biome1Distribution.asset`** — added
   `Type: 5 (Event), Weight: 25` to `_nonTerminalBeaconTypes`, dropped Combat
   from 80 → 55, Rest unchanged at 20. Aligns to the GDD-called design
   percentages the existing sanity test already documented as targets.
2. **new `Assets/Editor/AuthorBiome1MapThemeIcons.cs`** — MenuItem that loads
   `Map Node Icons.psb` sprites by name, sets primary = Event1, alternates =
   [Event2, Event3] on `Biome1MapTheme.asset` via SerializedObject. Idempotent.

## Authored values being destroyed

- `Biome1Distribution.asset` `_nonTerminalBeaconTypes[0].Weight: 80` → `55`
  (Combat down-weight to make room for Event).
- `Biome1MapTheme.asset` `_beaconIconAlternates[5].Alternates: []` → `[Event2, Event3]`
  (was empty pool; primary at `_beaconIconsByType[5]` may be overwritten to
  Event1 if the currently-wired sprite isn't already Event1).

Existing Chopshop alternates (index 4) untouched. Existing primary icons for
Start/Combat/EliteCombat/Merchant/Chopshop/Rest/Haven/Boss untouched.

## Files touched

- `Assets/Resources/Run/Biomes/Biome1Distribution.asset` (1 array grew from
  2 entries to 3 entries; Combat weight edited).
- `Assets/Tests/EditMode/Run/Biome1Distribution_AssetSanity_Test.cs` (test
  name + assertions updated to Combat=55/Event=25/Rest=20).
- **new** `Assets/Editor/AuthorBiome1MapThemeIcons.cs` (~93 lines including
  doc comments; single MenuItem `Tools/Wasteland Run/Data/Author Biome1 Map
  Theme Icons`).

## Technical Director Review

**Verdict**: APPROVE (with one guard)

**(a) Standalone file — correct call.** `CombatPrefabAuthor.cs` is
Combat-prefab-shaped; a map-theme icon binder has zero categorical fit
there. ADR-0016 (category + edit cadence): different subject, different edit
rhythm, different file. Precedent: `AuthorEnemyArchetypePrefabs`,
`AuthorDune/Iron/Dredge` all live standalone. Fold-in would grow Combat
author's charter past its stated responsibility (composition smell-test
failure).

**(b) Name-lookup-by-string at atlas boundary — acceptable with one guard.**
PSB sprite names are authored strings the artist controls, same trust
boundary as SO field names. Fail loudly if any of Event1/Event2/Event3
aren't found (throw with the missing name), don't silently skip — otherwise
a rename ships an empty alternates array and Biome1 quietly loses variety.

**Self-audit:**
- *Health*: ADR-0011 clean (editor-only, exception #3 authoring surface); no
  bridges, no bimodal path. Config edit to Biome1Distribution is pure
  ADR-0015 narrowing table adjustment — model territory it was built for.
- *Optimization*: Editor MenuItem, zero runtime cost. N/A.
- *1.0 survival*: Author menu pattern is the shipping convention; Biome2/3
  will each get their own MapTheme.asset + parallel author menu (or the
  current one generalizes to `AuthorMapThemeIcons(BiomeMapTheme, string
  prefix)` when the second caller lands — don't pre-abstract).

Ship it.

## Guard applied per TD

- `AuthorBiome1MapThemeIcons.FindOrThrow` throws `InvalidOperationException`
  with the missing sprite name if any of Event1/2/3 aren't in the atlas.
  Runs before any SerializedObject mutation so a rename fails visibly instead
  of silently blanking alternates.

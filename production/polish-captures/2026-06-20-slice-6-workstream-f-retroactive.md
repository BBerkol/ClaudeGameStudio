# Polish Capture: Workstream F — Sibling Run.prefab Topology + Resources-Fallback Retirement (Retroactive)

**Date:** 2026-06-20
**System:** Run-scene topology (Unity prefabs, scene composition, asset-resolution policy)
**Status:** **RETROACTIVE.** The destructive edits already shipped in working-tree WIP between 2026-06-17 (Slice 6 closeout `21dca21`) and 2026-06-20. The capture-before-destroy hook would have blocked these edits at the time; this file documents the destruction retroactively so the commit can land legally per the protocol in `production/polish-captures/README.md` + `CLAUDE.md`.

**Trigger for retroactive authoring:** TD audit 2026-06-20 (pass 1 + pass 2) discovered uncaptured destructive work mixed with Workstream A (generator pivot Block 1) WIP in the same working tree. User confirmed the capture protocol was not skipped intentionally and asked TD to verdict the design retroactively (see `## Technical Director Review` below). Verdict: APPROVED with 2 pre-commit gates — both verified before this file was authored.

## What's being destroyed

A coherent post-Slice-6 designer-led pass that re-cuts the run-scene topology along categorical lines (Combat encounter vs. Run loop) per the sibling-not-nested + categorical-fit principles in memory `feedback_composition_smell_test.md` and `project_adr_0016_scope_expansion.md`. Three independent landmines were retired in the same pass:

1. **Cross-prefab GameObject SerializeField wire** (`RunSceneOverlayHost._combatHudRoot`) — an ADR-0011 bridge that required Combat.prefab to be visible-from-Run.prefab at author time.
2. **Three Resources-fallback paths** — `Vehicle_Scout`, `Biome1Distribution`, `CombatBalance_Default` were all loaded via `Resources.Load<T>` at `Awake` / `AutoLoadDataAssets` as a fallback when the SerializeField was null. The fallback was the bridge (per ADR-0011 #5); SerializeField is canonical.
3. **`RunControlsWidget` debug shell** — a pre-Slice-6 test harness that survived into the production prefab. Reset / encounter picker / debug-damage controls now live where they belong (RunCompleteViewController owns Restart, RunSceneHost Inspector owns encounter framing, debug damage was deleted outright).

### Affected paths (Workstream F)

**New files:**
- `Assets/Scripts/Run/RunOverlayEvents.cs` + meta — static-event POCO seam in `WastelandRun.Run` (noEngineReferences). Cross-asmdef mediator replacing the `_combatHudRoot` wire.
- `Assets/Prefabs/Run/Run.prefab` + meta — scene-root sibling of Combat.prefab. Owns RunSceneHost + RunSceneOverlayHost + MapView (UIDocument) + RunCompleteView (UIDocument).
- `Assets/Prefabs/Run.meta` — folder meta.

**Deleted files:**
- `Assets/Scripts/CombatView/RunControlsWidget.cs` + meta — 305 lines, debug shell.
- `Assets/Settings/Scenes/URP2DSceneTemplate.unity` + meta — 357 lines. GUID `2cda990e2423bbf4892e6590ba056729`. **TD-verified SAFE-TO-DROP** (zero references in `Assets/`, `ProjectSettings/`, `Packages/`; not in `EditorBuildSettings` or scene-template registry; stock Unity scene shell with default Lighting/RenderSettings only — no `RenderPipelineAsset` / `Renderer2DData` wiring).

**Modified C#:**
- `Assets/Scripts/CombatView/RunSceneHost.cs` (+68 net) — removed `DefaultPlayerVehicleResourcePath`, `DefaultBiomeDistributionResourcePath` consts and the `Awake` Resources.Load fallbacks; tooltip rewrites; BeginNewRun error message points at AuthorRun.
- `Assets/Scripts/UI/RunSceneOverlayHost.cs` (-51 net) — removed `_combatHudRoot` SerializeField + 3 `SetActive` toggles; replaced by `RunOverlayEvents.RaiseOverlayShown/Hidden` calls at HandleBeaconChanged / HandleCombatReady / HandleRunComplete. Docstring shortened.
- `Assets/Scripts/CombatView/Data/CombatBalanceSO.cs` (16 line delta) — removed `[Min(0)] [SerializeField] private int _debugDamageAmount = 10;` field + `public int DebugDamageAmount` accessor + `Configure(int, int, int debugDamageAmount)` parameter. Method signature narrowed.
- `Assets/Scripts/CombatView/SceneEncounterBuilder.cs` (2 lines) — comment update.
- `Assets/Editor/CombatPrefabAuthor.cs` (+317 net) — added `AuthorRun` + `AuthorRunMenu` + `AuthorCombatScene` menu items. Removed AuthorCombat's `_enemyArchetypePrefab` Dredge bake + `RunSceneHost._combatBeaconArchetypes[0]` Skimmer bake (both moved to AuthorRun with full 3-archetype roster). Removed CombatHud's `_runControls` SerializeField wire + W3B legacy-skip block. Added `_balanceAsset` bake on CombatController. Added 3 new `private const string` path consts: `RunPrefabRoot`, `RunPrefabPath`, `VehicleScoutAssetPath`, `BiomeDistributionAssetPath`, `CombatBalanceAssetPath`.
- `Assets/Editor/CombatDataInitializer.cs` (3 lines) — minor signature follow-up.

**Modified asmdef:**
- `Assets/Editor/WastelandRun.CombatView.Editor.asmdef` — added refs to `WastelandRun.Run.Authoring` and `WastelandRun.UI` (Editor asmdef expansion only; runtime arrows untouched). TD-verified not masking inversion.

**Modified prefabs / scenes:**
- `Assets/Prefabs/CombatView/Combat.prefab` — **net -545 lines.** Removed RunSceneHost + RunSceneOverlayHost + MapView + RunCompleteView components and children that moved to Run.prefab. Removed `_enemyArchetypePrefab` SerializeField wire on CombatController. Added `_balanceAsset` SerializeField wire. Combat.prefab now claims only combat-encounter scope: CombatController + SceneVisuals + LaneAxis + CombatHud + Popups + Debug canvases.
- `Assets/Prefabs/CombatView/CombatHud.prefab` — 1 line (minor follow-up).
- `Assets/Scenes/CombatScene.unity` — **net -191 lines.** Old single-root scene replaced by Combat.prefab + Run.prefab as scene-root siblings.

**Modified assets:**
- `Assets/Fonts/RussoOne SDF.asset` (+15) — **intentional TMP atlas regen** for better outline (user-confirmed 2026-06-20). KEEP.
- `Assets/Resources/combat/Balance/CombatBalance_Default.asset` (-1) — `_debugDamageAmount` field removed from SO definition; asset file loses the corresponding YAML line.

**Modified CI gates:**
- `tools/ci/grep-gates.sh` (+29 net) — 8 new grep gates:
  1. `\bRunControlsWidget\b` in `Assets/Scripts Assets/Tests`
  2. `\bRequestDebugDamageEnemy\b` in `Assets/Scripts Assets/Tests`
  3. `Resources\.Load.*EnemyArchetypes` in `Assets/Scripts`
  4. `Resources\.Load<VehicleDefinitionSO>` in `Assets/Scripts`
  5. `Resources\.Load<BiomeDistributionSO>` in `Assets/Scripts`
  6. `Resources\.Load<CombatBalanceSO>` in `Assets/Scripts`
  7. `\b_combatHudRoot\b` in `Assets/Scripts`
  8. `\bRequestReset\b` gate text updated (was: "widget now calls host directly"; now: "view now calls host directly")

### Authored values being destroyed (designer-facing)

Per `feedback_pre_author_capture_protocol.md` — every designer tweak baked into the prior topology, enumerated so re-authoring doesn't silently regress:

| # | Authored value | Prior location | Destination | Notes |
|---|----------------|----------------|-------------|-------|
| 1 | `_debugDamageAmount = 10` | `CombatBalanceSO` SerializeField | DELETED | Debug-shell artifact, no production consumer. Grep confirms zero remaining `_debugDamageAmount` / `DebugDamageAmount` references in `Assets/`. |
| 2 | `DefaultPlayerVehicleResourcePath = "Combat/Vehicles/Vehicle_Scout"` | `RunSceneHost` private const | DELETED | Bridge path. AuthorRun bakes `Vehicle_Scout.asset` into `_playerVehicleAsset` directly. |
| 3 | `DefaultBiomeDistributionResourcePath = "Run/Biomes/Biome1Distribution"` | `RunSceneHost` private const | DELETED | Bridge path. AuthorRun bakes `Biome1Distribution.asset` into `_biomeDistribution` directly. |
| 4 | CombatBalance `Resources.Load` fallback | `CombatController.AutoLoadDataAssets` | DELETED | AuthorCombat now bakes `CombatBalance_Default.asset` into `_balanceAsset`. |
| 5 | Enemy archetype `Resources.Load` fallback | `RunSceneHost.ResolveBinder` | DELETED | `_combatBeaconArchetypes[]` is now the only resolution path; throws if no element resolves. |
| 6 | `_combatHudRoot` cross-prefab GameObject ref | `RunSceneOverlayHost` SerializeField | DELETED | Wire replaced by `RunOverlayEvents` mediator. |
| 7 | AuthorCombat Dredge bake of `_enemyArchetypePrefab` | `CombatController` SerializeField | DELETED | `_enemyArchetypePrefab` field itself was retired Slice 6 Phase C (commit `233445e`). AuthorCombat's bake of it goes with the field. |
| 8 | AuthorCombat Skimmer bake of `_combatBeaconArchetypes[0]` | `RunSceneHost` array | MOVED to AuthorRun | AuthorRun now bakes the full 3-archetype Biome 1 roster (DuneSkimmer/IronShepherd/Dredge at 0/1/2). |
| 9 | CombatHud `_runControls` SerializeField + W3B legacy-skip block | `CombatPrefabAuthor.AuthorCombatHud` | DELETED | RunControlsWidget retirement removes the field and the wire. |
| 10 | `URP2DSceneTemplate.unity` (357 lines) | `Assets/Settings/Scenes/` | DELETED | Stock Unity residue per TD §1 — no references anywhere. |
| 11 | `RunControlsWidget.cs` (305 lines) | `Assets/Scripts/CombatView/` | DELETED | Successor mapping: Restart → `RunCompleteViewController`; encounter picker → `RunSceneHost._encounterSelection` Inspector field; debug damage → no successor (deleted per user confirmation 2026-06-20). |
| 12 | Old CombatScene single-root layout (~191 YAML lines) | `Assets/Scenes/CombatScene.unity` | RECUT | Replaced by AuthorCombatScene with Combat.prefab + Run.prefab as scene-root siblings. |
| 13 | Old Combat.prefab nested-host layout (~545 YAML lines) | `Assets/Prefabs/CombatView/Combat.prefab` | RECUT | RunSceneHost + RunSceneOverlayHost + MapView + RunCompleteView components moved to Run.prefab. |

### New canonical shapes

| Shape | Location | Purpose |
|-------|----------|---------|
| `RunOverlayEvents` static-event POCO | `Assets/Scripts/Run/RunOverlayEvents.cs` (WastelandRun.Run, noEngineReferences) | Cross-asmdef mediator: UI raises Shown/Hidden, CombatView subscribes. Replaces cross-prefab SerializeField. |
| `Run.prefab` | `Assets/Prefabs/Run/Run.prefab` | Scene-root sibling of Combat.prefab. Owns run-loop layer. |
| `CombatPrefabAuthor.AuthorRun` menu | `Tools > Wasteland Run > Author Run Prefab` | Idempotent (delete-and-recreate via SaveAsPrefabAsset). Asks overwrite confirmation when Run.prefab exists. Bakes 3-archetype roster + Vehicle_Scout + Biome1Distribution into RunSceneHost. |
| `CombatPrefabAuthor.AuthorCombatScene` menu | `Tools > Wasteland Run > Author Combat Scene` | Idempotent (strips existing roots by source-prefab GUID match, then InstantiatePrefab fresh). Places Combat + Run as scene-root siblings. |
| 8 new grep gates | `tools/ci/grep-gates.sh` | Pin retirement of debug-shell + 3 Resources fallbacks + _combatHudRoot wire. |

## Proposed change

Commit Workstream F as a single logical topology cut on Unity repo `main`. ADR-0011-clean: no half-state, no transitional comments, no parallel storage. Two pre-commit gates were verified before authoring this capture file (both pass — see TD §2 below).

**Commit body draft:**

```
feat(run-scene): workstream F — sibling Run.prefab + Resources-fallback retirement + RunOverlayEvents seam

Re-cuts the run-scene topology along categorical lines. Combat.prefab now
claims only combat-encounter scope (CombatController + SceneVisuals +
LaneAxis + CombatHud + Popups + Debug). Run.prefab is a new scene-root
sibling owning the run-loop layer (RunSceneHost + RunSceneOverlayHost +
MapView + RunCompleteView).

Replaces the cross-prefab _combatHudRoot SerializeField wire with a
RunOverlayEvents static-event POCO mediator in WastelandRun.Run
(noEngineReferences) — UI raises Shown/Hidden, CombatHud subscribes. Pairs
+= in OnEnable with -= in OnDisable (verified) so domain reload doesn't
leak listeners.

Retires three Resources.Load fallback paths (VehicleDefinitionSO,
BiomeDistributionSO, CombatBalanceSO) in favor of SerializeField + author-
time baking via new CombatPrefabAuthor.AuthorRun + reauthored AuthorCombat
+ new AuthorCombatScene menus. Per ADR-0011, the fallback was the bridge;
SerializeField is canonical; wire failures move from silent runtime warning
to loud throw at BeginNewRun.

Deletes RunControlsWidget.cs (305 lines, debug-shell artifact) and 8 new
grep gates pin it retired. Restart lives on RunCompleteViewController,
encounter picker lives on RunSceneHost._encounterSelection Inspector
field, debug damage is gone.

Deletes URP2DSceneTemplate.unity (357 lines, stock Unity residue, zero
references in Assets/ or ProjectSettings/).

Capture: production/polish-captures/2026-06-20-slice-6-workstream-f-retroactive.md
ADRs: 0011 (#1, #2, #5), 0014 (asmdef arrow unchanged), 0016 (categorical-fit).
```

## Technical Director Review

TD was consulted in two passes 2026-06-20. Pass 1 identified the workstream and recommended sequencing; pass 2 produced the design verdict after the user confirmed the protocol skip was unintentional and asked for a fresh design check.

### Pass 2 verdict (§2 — retroactive design)

**APPROVED with two pre-commit gate items — both verified.**

- **Sibling `Run.prefab` peer of `Combat.prefab`** — APPROVE. Consistent with `feedback_composition_smell_test.md` and ADR-0016 sibling-not-nested principle. Breaks the cross-prefab cycle entirely; does not relocate it. ADR-0014 unaffected (UI Toolkit primacy unchanged; Run.prefab is scene-topology peer, not a UI-stack choice).

- **`RunOverlayEvents` static-event POCO seam in `WastelandRun.Run`** — APPROVE with one concern. Pure C#, `noEngineReferences`, asmdef arrow stays `UI → Run` and `CombatView → Run` (both pre-existing). Textbook ADR-0011-clean asmdef-cycle break. **Concern**: static event in Edit Mode retains listeners across domain reloads. Mitigation: subscriber MUST pair `+=` with `-=` in `OnDisable`. **VERIFIED 2026-06-20**: `CombatHud.cs` lines 344-346 subscribe in OnEnable; lines 704-705 unsubscribe in OnDisable; line 346 re-checks `IsOverlayVisible` for late-subscribe correctness. Gate passed.

- **Three Resources-fallback retirements** — APPROVE. Resources was the bridge; SerializeField is canonical. Three grep gates pin this. ADR-0011 #5 explicitly forbids fallback-as-compat-overload. Wire failures move from silent warning → hard nullref / throw at Start — preferred per ADR-0011 (loud over wrong).

- **`_combatHudRoot` SerializeField removal** — APPROVE. Replaced by `RunOverlayEvents.RaiseOverlayShown/Hidden`. Cross-prefab GameObject SerializeFields are an ADR-0011 anti-pattern; the POCO seam replacement is net positive.

- **`CombatPrefabAuthor.AuthorRun` + +317 LoC, `_debugDamageAmount` removal** — APPROVE. Grep confirms zero remaining `_debugDamageAmount` / `DebugDamageAmount` consumers across `Assets/`. `Configure()` signature properly narrowed in the same diff. **AuthorRun idempotency VERIFIED 2026-06-20**: `AuthorRunMenu` asks overwrite confirmation when Run.prefab exists; `AuthorRun()` builds a fresh GameObject hierarchy, calls `PrefabUtility.SaveAsPrefabAsset(root, RunPrefabPath)` (overwrites at path), then `DestroyImmediate(root)`. Delete-and-recreate, not append. `AuthorCombatScene` strips existing Combat/Run roots by source-prefab GUID match before `InstantiatePrefab`. Gate passed.

- **`WastelandRun.CombatView.Editor.asmdef` adds Run / Run.Authoring / UI refs** — APPROVE. Editor asmdef expanding refs to drive the author tool is fine; runtime arrows untouched. Not masking inversion.

### Pass 2 verdict (§1 — URP2DSceneTemplate.unity)

**SAFE-TO-DROP.** File GUID `2cda990e2423bbf4892e6590ba056729`. Pre-deletion content is stock Unity scene YAML with default `OcclusionCullingSettings` / `RenderSettings` / `LightmapSettings` — no `RenderPipelineAsset` reference, no `Renderer2DData` reference, no Universal2DRenderer wiring. Grep across `Assets/`, `ProjectSettings/`, `Packages/` for both filename and GUID: zero hits. Pipeline plumbing lives elsewhere (untouched). User's instinct ("residue not in scenes folder") was correct.

### Pass 2 verdict (§3 — RunControlsWidget obsolescence)

**TRULY OBSOLETE.** Grep across `*.cs` / `*.unity` / `*.prefab` / `*.asmdef` / `*.uxml` for `RunControlsWidget`: zero hits. F4 debug-win hotkey lives in `CombatHud` + `CombatController.DebugWinCombat()` (independent). New grep gates pin obsolescence: `\bRunControlsWidget\b` and `\bRequestDebugDamageEnemy\b`.

**ADRs invoked:** 0011 (#1 one-shot migration, #2 parallel storage forbidden, #5 fallback-as-compat-overload forbidden), 0014 (asmdef arrow), 0016 (sibling-not-nested + categorical-fit).

---

## Out-of-scope (not destroyed by this capture)

- Workstream A files (`BiomeWebGenerator.cs`, `BiomeGenerationInputs.cs`, `BiomeGenerationInputsFactory.cs`, `BiomeDistributionSO.cs`, `Biome1Distribution.asset`) — bound by separate capture `2026-06-19-biome-generator-pivot-block-1.md`. Commit separately after this one lands.
- `Assets/Tests/EditMode/Run/BiomeWebGenerator_Test.cs` — bound by Block 1 capture (rewrite per -4/+7/2-rename plan). Separate commit.
- Block 2 (NodeMap.Advance bidirectional routing + MapView semantics rename + ADR-0015 amendment + save-load test) — scheduled within hard deadline **2026-06-30** per TD pass 2 §4 (extended from 2026-06-26 in original Block 1 capture; revert clause for Block 1 commit body uses the new date).

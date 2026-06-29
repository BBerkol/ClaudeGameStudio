# TD Verdict — Slice 10 scene-split execution

**Date:** 2026-06-27
**Trigger:** Slice 7 + 8 + 8a-8d + 9a + 9b all shipped; brief asks for fresh execution-shaped verdict per the 2026-06-17 prior.
**Status:** APPROVE with concrete shape. Cut shape recommendation: **Q10 (b) — Combat + Rest scenes only, ship the rest of the binding SO with empty entries.**

---

## Re-grounding

The 2026-06-17 prior holds. Three things have shifted on the surface that pull on detail but not on the verdict:

1. **Save bootstrap matured.** `SaveBootstrap` (sibling on Run.prefab, `[DefaultExecutionOrder(-100)]`) now drives `_host.Initialize(LoadResult, NodeMapDto, RunSeedDto, RunDeckDto, VehicleStateDto)`. The boot order under scene-split needs this component to land on **RunScene.unity** as a Run.prefab sibling and fire BEFORE any beacon scene loads — the host has to know "fresh vs resume" before the first beacon scene is asked for.
2. **CombatOutcomeOverlay + CardRewardPicker live on Combat.prefab, not Run.prefab.** The brief's Q6 had this backward. Both are children of `CombatHud.prefab` (which is nested in Combat.prefab) — they're `SerializeField`s on `CombatHud._outcomeOverlay` / `_rewardPicker`. They are combat-scoped today, instantiated at AuthorCombat time. Q6 reframes around that.
3. **The cross-prefab wire-up surface is now 7 SerializedObject writes.** `WireRestPickerCrossPrefab` writes 4 picker refs + 3 toggler refs at AuthorCombatScene time. This is a precise measurement of how much "wrong scene" wiring the scene-split deletes. The success metric (`RestScopeToggler.cs` deleted) is sharper now because the toggler is the *only* surface that needs cross-prefab refs.

The 2026-06-17 trigger condition ("after Slice 7, before biome 2") is met. The execution slice is in scope.

---

## Verdict per question

### Q1 — PlayerVehicle visual ownership

**Option (b): Per-scene instance, shared POCO.**

`RunSceneHost.Session.Player` is the canonical Vehicle POCO and lives on RunScene. Each beacon scene that wants to show the player vehicle instantiates `PlayerVehicle.prefab` into its own scene from a serialized `VehicleDefinitionSO` reference, then calls `VehicleVisual.Bind(session.Player)` in `BeaconSceneBootstrap.Awake` so the visual reads off the POCO. Combat.unity gets the chase-rail-rigged instance under `LaneAxis/`; Rest.unity gets a rest-posed instance under its own root (no chase rail, no parallax follower).

Why this preserves ADR-0011: the *visual* is not stored in two places — `VehicleVisual` is a pure projection of the POCO, and the POCO lives on RunScene. Each scene's instance is a *view* in the ADR-0014 sense, not parallel state. The two visuals never disagree because they're never alive simultaneously (additive beacon-scene swap unloads one before the other loads).

Why this serves the success metric: with Rest.unity owning its own player-vehicle instance, `RestScopeToggler.Show` no longer needs to reach across into Combat.prefab to flip enemy vehicle / Combat_HUD / EnemyBarStackCanvas off — those GameObjects are in a scene that isn't loaded during rest. The toggler vanishes because its job vanishes.

Why not (a) persistent-on-RunScene: cross-scene parenting would force the chase-rail follower in Combat.unity to track a transform in RunScene, which inverts ownership (Combat's rig becomes a passive observer of RunScene state — bimodal lifetime trap).

Why not (c) separate rest-posed vehicle prefab: bifurcates the chassis art pipeline — designer edits to PlayerVehicle.prefab don't propagate to the rest variant. ADR-0011 #1 (parallel storage) at the prefab level.

### Q2 — PlayerBarStackCanvas + Combat_HUD ownership

**Option (b): Both stay in Combat.unity; Rest.unity gets its own bar canvas (mirrors Q1.b).**

Combat_HUD is unambiguously combat-only — intent telegraph, hand, end-turn, energy orb, pile chips, buff strips. No question. Stays in Combat.unity.

PlayerBarStackCanvas is currently shared by combat + rest screens because the rest picker needs to render bars on it. Under scene-split, Rest.unity owns its own `RestBarStackCanvas` (mirror name to disambiguate from combat bar stack). `RestBarStackCanvas` is a child of Rest.unity's player vehicle instance. `RestPickerController` no longer reaches across into Combat.prefab — it grabs the local-scene bar stack via `GetComponentInChildren` on its scene's PlayerVehicle root in `Awake`.

Pattern: each beacon scene that needs to render vehicle bars owns its own bar canvas. Combat owns PlayerBarStackCanvas + EnemyBarStackCanvas. Rest owns RestBarStackCanvas. Merchant / Haven / etc. own whatever bar canvas they need (or none if their UI shape doesn't need it).

Why not (a) persistent on RunScene: same as Q1.a — inverts ownership, and the bar widget rebuild path on combat-rebuild (`CombatHud.OnCombatRebuilt`) now needs to know about a RunScene canvas. Scope leak.

Why not (c) per-scene rebuild from VehicleVisual lookup: that's the same model as (b) but framed as "build at runtime"; (b) gives the designer an authorable canvas asset per scene which matches the categorical-fit principle (each scene's HUD is part of the scene's claim).

### Q3 — Combat vs EliteCombat

**Same `Combat.unity`, parameterized by `BeaconData.EnemyArchetype`.**

Both routes through `RunSceneHost.BeginCombatForCurrentBeacon` and `SceneEncounterBuilder.Build` today — the only difference is which `EnemyArchetypeId` the beacon carries. Elite is not a separate scene category; it's an enemy-roster property of biome distribution. Splitting into `EliteCombat.unity` would mean two scene assets that author the same chase rail + parallax + HUD, which is ADR-0011 #1 parallel storage at the scene level.

The `BeaconSceneBindingSO` maps `BeaconType.Combat` AND `BeaconType.EliteCombat` to the same `Combat.unity` asset reference. The lookup is per-BeaconType but two entries can point at the same scene. ADR-0015 narrowing-by-data-table accommodates this naturally — `BeaconType.EliteCombat` is a "valid value, currently maps to the same place as Combat" which is precisely the configuration-narrowing pattern.

### Q4 — Authoring entrypoint shape

**Option (a) extended: `AuthorRunScene` + `AuthorCombatScene` + per-beacon scene authoring entry points, with a top-level `Author All Scenes` convenience.**

Concrete authoring menu structure post-slice-10:

- `Tools/Wasteland Run/Scenes/Author RunScene` — instantiates `Run.prefab` into `RunScene.unity` as the single scene root. No cross-prefab wiring (Run.prefab is self-contained).
- `Tools/Wasteland Run/Scenes/Author Combat Scene` — instantiates `Combat.prefab` + `BeaconSceneBootstrap` component on the scene root. NO cross-prefab refs (the toggler is deleted, the picker is moving to Rest.unity).
- `Tools/Wasteland Run/Scenes/Author Rest Scene` — instantiates a new lightweight `Rest.prefab` (or builds in-place) + `BeaconSceneBootstrap` + the rest-mode PlayerVehicle instance + RestBarStackCanvas + RestPicker UIDocument GameObject. No cross-prefab refs.
- `Tools/Wasteland Run/Scenes/Author Beacon Stub Scene (Haven|Merchant|Event|Chopshop)` — single placeholder UXML showing "$beaconType visited — click Continue" for the 4 deferred scenes. Same author menu shape so when each one gets designed, the menu fills out.
- `Tools/Wasteland Run/Scenes/Author All Scenes` — convenience: re-runs all of the above in dependency order (Run first, then each beacon).

`WireRestPickerCrossPrefab` is deleted entirely. The 7 SerializedObject writes vanish. RestPicker is now a child of Rest.unity and resolves its bar stack / vehicle rest pose / vehicle visual via `GetComponentInChildren` on its scene's PlayerVehicle root in `Awake` (single-scene resolution = no cross-prefab dance).

### Q5 — Boot order on fresh launch

**Explicit `host.LoadCurrentBeaconScene()` call from `SaveBootstrap.LoadAndInitialize` after `Initialize` resolves.**

Sequence:

1. Editor opens `RunScene.unity` (the persistent scene).
2. `SaveBootstrap.Awake` runs first (`[DefaultExecutionOrder(-100)]`) → `SaveSystem.Bind` + adapter registration.
3. `SaveBootstrap.Start` → `LoadRunState` → `host.Initialize(...)` which routes to either `BeginNewRun` or `BeginRunFromLoaded`. Both paths construct `RunController` + `RunSession` and fire `OnBeaconChanged`.
4. `SaveBootstrap.Start` THEN calls `_host.LoadCurrentBeaconScene()` synchronously (new method on RunSceneHost — reads `CurrentBeacon.Type`, looks up the scene asset in `BeaconSceneBindingSO`, calls `BeaconSceneLoader.LoadAdditive(...)`).
5. The beacon scene loads additively; its `BeaconSceneBootstrap.Awake` calls `FindAnyObjectByType<RunSceneHost>()` to resolve the host, then `Bind(host)` subscribes its scene-specific surfaces to host events.

Why explicit-call over "subscribe to `OnBeaconChanged`": the `Initialize` path FIRES `OnBeaconChanged` before any beacon scene listener exists (RunSceneOverlayHost is on Run.prefab and is already subscribed, but the BeaconSceneLoader needs an *explicit* first-frame load too — the steady-state `OnBeaconChanged` handler is for *subsequent* beacon transitions, not the bootstrap one). Making the first beacon load explicit avoids a "first beacon change fires before any handler is subscribed" race.

After bootstrap, subsequent `OnBeaconChanged` events drive `BeaconSceneLoader.LoadAdditive(newBeaconType)` + `UnloadAsync(oldBeaconType)` on the host or on a dedicated `BeaconSceneOrchestrator` component on RunScene. Implementation detail: prefer a dedicated `BeaconSceneOrchestrator` MonoBehaviour subscribed to `OnBeaconChanged`, sibling to RunSceneOverlayHost on Run.prefab — keeps RunSceneHost engine-free per ADR-0002/0003 style (no Unity scene APIs inside the run-loop owner).

### Q6 — Reward picker + CombatOutcomeOverlay location

**Stays in Combat.unity.** The brief's premise here was inverted by the current code (both are SerializeField references on CombatHud, which is part of Combat.prefab — they're authored as nested children of CombatHud.prefab, scope-bound to combat).

The flow is: combat-end → `CombatOutcomeOverlay` shows under CombatHud → player clicks Continue → `CardRewardPicker` shows under CombatHud → player picks → `host.NotifyRewardClaimed()` → `OnRewardClaimed` fires → `RunSceneOverlayHost.HandleRewardClaimed` re-shows the map view on RunScene.

The reward picker and outcome overlay BELONG to combat resolution. They render BEFORE combat scene unloads — they're combat scope. The map view re-show happens on RunScene because the map view is on RunScene.

Sequence under scene-split:
1. Player wins → CombatOutcomeOverlay shows (still in Combat.unity).
2. Player clicks Continue → CardRewardPicker shows (still in Combat.unity).
3. Player picks / skips → `host.NotifyRewardClaimed` → `OnRewardClaimed` → `BeaconSceneOrchestrator` unloads Combat.unity → map view on RunScene re-shows.

If we forced reward picker onto RunScene, we'd have to bridge "combat-end fired but map view not yet shown" via either a 3rd persistent UI document or a cross-scene event seam. Both add complexity for no gain. Keeping them combat-scoped is the categorical fit.

The outstanding ADR-0014 debt around these two (still UGUI? actually no — they're already UI Toolkit per the Slice 7a comment in CombatPrefabAuthor at line 3679) is unrelated to this slice. Don't fold it in.

### Q7 — Enemy archetype instantiation

**No `MoveGameObjectToScene` needed — Unity's `Object.Instantiate` parents into the active scene by default, and the active scene under additive load can be set via `SceneManager.SetActiveScene` when the beacon scene loads.**

`BeaconSceneOrchestrator` (the new component handling scene load/unload on `OnBeaconChanged`) calls `SceneManager.SetActiveScene(loadedBeaconScene)` after `LoadSceneAsync` completes and BEFORE firing the orchestrator's own "scene ready" event that triggers `BeginCombatForCurrentBeacon`. With Combat.unity as the active scene, every `Instantiate` inside `SceneEncounterBuilder.Build` (the enemy vehicle visual) and inside `CombatHud.BuildIntentTelegraph` parents into Combat.unity natively.

On combat-end (`OnRewardClaimed`), orchestrator switches active scene back to RunScene BEFORE `UnloadSceneAsync(Combat)` so any spawned popups / debug UI that survive into the next beacon scene's bootstrap default-parent to RunScene if needed.

Why explicit `SetActiveScene` instead of `MoveGameObjectToScene` per Instantiate: `MoveGameObjectToScene` is the right tool when you have a specific spawn that crosses scenes (e.g., a damage popup pooled on RunScene but raised by Combat). `SetActiveScene` is the right tool for "this scene is the default home for new GameObjects right now." We need the latter — every combat-side spawn is combat-scoped and should die with the scene.

### Q8 — BeaconSceneBindingSO shape

**Static mapping: one SO at `Assets/Data/BeaconScenes/BeaconSceneBinding.asset`, every biome shares it.**

`BeaconType` is biome-invariant (all 8 values per ADR-0015) and the *scene that handles* a given BeaconType is also biome-invariant — biome 2's Rest beacon still loads Rest.unity, just with potentially a different backdrop sprite which is a property of biome data, not scene topology.

Shape:

```csharp
[CreateAssetMenu]
public sealed class BeaconSceneBindingSO : ScriptableObject {
    [SerializeField] private List<BeaconSceneEntry> _entries;
    public string SceneAssetPathFor(BeaconType type) { /* linear scan, throw if not found */ }
    [Serializable] private struct BeaconSceneEntry {
        public BeaconType Type;
        public string ScenePath;   // Assets/Scenes/Combat.unity, etc.
    }
}
```

NOT folded into `BiomeDistributionSO` — biome distribution narrows *which beacon types appear*; scene binding narrows *how a beacon type is presented*. Different concerns, different cadences (biome 2 will fork distribution but reuse scene binding wholesale). ADR-0011 #4 (no vestigial duplication): one canonical binding SO.

For the deferred beacon types (Haven, Merchant, Event, Chopshop), the SO ships with entries pointing at the four stub scenes from Q10 — each stub scene is a real authored asset that just shows "$beaconType visited — Continue" until designed. NOT a null `ScenePath` field with runtime null-check (that's ADR-0011 #6 stub-return + bimodal-path).

Reference type: `string ScenePath` over `SceneAsset` reference — `SceneAsset` is editor-only; runtime uses scene path strings for `LoadSceneAsync`. CombatPrefabAuthor validates at author time that each path resolves.

### Q9 — RunOverlayEvents POCO mediator

**Survives, but its consumer set narrows.** Don't retire it yet.

Today RunOverlayEvents is the cross-prefab seam: Run.prefab's RunSceneOverlayHost raises Shown/Hidden when the map view opens/closes; Combat.prefab's CombatHud subscribes and SetActive(false)s its root.

Under scene-split:
- When map view shows, Combat.unity is loaded (we're between combats). CombatHud isn't visible because Combat.unity might not even be loaded yet — but if it IS loaded (combat just resolved, map shows immediately before scene unload), CombatHud still needs to fall silent.
- The cleaner case is "map view is shown while between beacon scenes" — Combat.unity isn't loaded, no subscriber.
- But there's still the "RunComplete view shows on top of an active beacon scene" case — RunCompleteView fires after a terminal beacon, and the player sees the run-summary panel laid over whatever beacon scene just resolved.

So: RunOverlayEvents survives but its subscriber count drops from 1-guaranteed to 0-or-1. Each beacon scene's `BeaconSceneBootstrap` subscribes its scene-local HUD (if any) to the events in `Awake` and unsubscribes in `OnDestroy`. The static event mediator pattern is preserved.

Alternative I considered and rejected: scene-unload itself as the hide signal. Doesn't work for RunComplete (overlay shows while beacon scene is still loaded) and breaks the "map view shows while Combat.unity is mid-unload" timing case. The explicit event signal is more robust.

### Q10 — Slice 10 cut shape

**Option (b): Combat + Rest scenes get authored, the 4 unstyled beacon types (Haven, Merchant, Event, Chopshop) ship as authored stub scenes.**

The success metric is `RestScopeToggler.cs` deleted. To delete it, both Combat.unity and Rest.unity must exist as separate scenes. Both already have content today (combat has all the combat surface; rest has the picker + vehicle rest pose + backdrop). Authoring both in Slice 10 is the smallest cut that hits the metric.

Critical: the 4 stub scenes also need to ship — not as TBD entries, but as real `Haven.unity` / `Merchant.unity` / `Event.unity` / `Chopshop.unity` files containing nothing but a `BeaconSceneBootstrap` + a UIDocument showing "$beaconType visited — Continue button". `BeaconSceneBindingSO` then has all 8 entries (Start is N/A, the other 7 all bind to real scenes). This is data-flag lagging-dep pattern (`feedback_data_flag_lagging_dependency`): the SO's value space is fully expressed today, and content fills in over time without re-touching the binding shape.

Why not (a) full design ALL six immediately: Haven / Merchant / Event / Chopshop don't have GDD-approved content. Authoring real content for them would be Combat-Hud-style work and is out of scope.

Why not (c) RunScene + Combat only, Rest stays as overlay on RunScene: doesn't delete RestScopeToggler. Misses the success metric. Half-shipped.

The Rest stub design: a UIDocument with one Label ("Resting…") + one Continue Button that fires `host.NotifyRestResolved()` — same shape as RestPickerController's empty-list path. For the 4 unstyled beacons: Continue button fires `host.AdvanceToNextBeacon(toIndex, HostAdvanceReason.PlayerChoice)` so they don't permanently soft-lock; in practice they're unreachable in biome 1 (Biome1Distribution only emits Combat + Haven), so the stubs only matter when biome 2 distribution adds Merchant/Event/Chopshop.

---

## Implementation shape

### New files (~6 new C# files + 7 scene assets + 1 SO)

**Code:**

1. `Assets/Scripts/Run/Authoring/BeaconSceneBindingSO.cs` (~70 lines) — the data-table SO. ADR-0015 narrowing pattern. Lives in `WastelandRun.Run.Authoring` asmdef (data-table SOs already live there).
2. `Assets/Scripts/CombatView/BeaconSceneOrchestrator.cs` (~120 lines) — MonoBehaviour on Run.prefab, sibling to RunSceneOverlayHost. Subscribes to `host.OnBeaconChanged`; calls `SceneManager.LoadSceneAsync(path, Additive)` + `UnloadSceneAsync(oldPath)` + `SetActiveScene` in the right order. Exposes `LoadCurrentBeaconSceneAsync()` for the boot-order explicit call from SaveBootstrap. Lives in CombatView (needs Unity scene APIs which Run.asmdef can't reference — Run.asmdef is `noEngineReferences: true`).
3. `Assets/Scripts/CombatView/BeaconSceneBootstrap.cs` (~80 lines) — MonoBehaviour on the root of each beacon scene. `Awake` calls `FindAnyObjectByType<RunSceneHost>(FindObjectsInactive.Include)` to resolve the host; exposes `Host` property; provides hooks for scene-specific Bind. Lives in CombatView.
4. `Assets/Scripts/Run/BeaconSceneSpec.cs` (~30 lines) — small POCO struct `(BeaconType, string ScenePath)` used by the SO. Lives in `WastelandRun.Run` so the SO can serialize a list of these and downstream consumers in CombatView can read them.

**Authoring (Editor):**

5. CombatPrefabAuthor.cs (modify, ~+400 lines, ~-150 lines net +250): new methods `AuthorRunScene`, `AuthorCombatScene` (renamed shape — no longer instantiates Run.prefab), `AuthorRestScene`, `AuthorBeaconStubScene(BeaconType)`, `AuthorAllScenes`. Delete `WireRestPickerCrossPrefab` (~60 lines). Rename old `AuthorCombatScene` → either delete or repurpose. Update Combat.prefab authoring to drop `RestVisuals` subtree (toggler + backdrop) — those move to Rest.unity's structure.

**Scene assets:**

6. `Assets/Scenes/RunScene.unity` — replaces `CombatScene.unity`. Contains Run.prefab as the single root, plus `BeaconSceneOrchestrator` component on the Run root.
7. `Assets/Scenes/Combat.unity` — Combat.prefab as root + `BeaconSceneBootstrap` component.
8. `Assets/Scenes/Rest.unity` — new shape. Root GameObject containing: PlayerVehicle.prefab instance (rest-posed, no chase rail follower component), backdrop SpriteRenderer (the placeholder dark quad), RestBarStackCanvas (child of player vehicle), RestPicker UIDocument GameObject, `BeaconSceneBootstrap` component.
9. `Assets/Scenes/Haven.unity` + `Merchant.unity` + `Event.unity` + `Chopshop.unity` — stub scenes. Each: empty root + UIDocument with placeholder UXML + `BeaconSceneBootstrap` + Continue button wired to `host.AdvanceToNextBeacon`.

**Data asset:**

10. `Assets/Data/BeaconScenes/BeaconSceneBinding.asset` — the SO instance with all 7 entries (Start excluded — it's not a runtime-visited beacon).

### Modified files

11. `Assets/Scripts/CombatView/RunSceneHost.cs` — no API changes. Optional: add `BeaconSceneBindingSO _sceneBinding` SerializeField + `CurrentBeaconScenePath` read-only property if BeaconSceneOrchestrator wants to read the binding through the host rather than holding its own reference. Either shape works; orchestrator-owns-binding is cleaner (single ownership).
12. `Assets/Scripts/CombatView/SaveBootstrap.cs` — `LoadAndInitialize` adds a final line after `_host.Initialize(...)`: `_beaconSceneOrchestrator.LoadCurrentBeaconSceneAsync()`. New SerializeField `_beaconSceneOrchestrator` wired by AuthorRunScene.
13. `Assets/Scripts/CombatView/RestPickerController.cs` — drop `_restScopeToggler` SerializeField + every call site. Drop `_playerBarStack` / `_vehicleRestPose` / `_playerVehicleVisual` cross-prefab wires; resolve them via `GetComponentInChildren<>` on the local scene's PlayerVehicle root in `Awake`. The picker becomes scene-local — no cross-prefab refs at all.
14. `Assets/Scripts/CombatView/RunSceneOverlayHost.cs` — drop `_restPicker` SerializeField + every call site. RestPicker no longer lives under Run.prefab; it's now on Rest.unity and resolves the host via FindAnyObjectByType in its own Bootstrap. RunSceneOverlayHost stays in charge of MapView / RunCompleteView only.
15. `Assets/Scripts/Run/BeaconData.cs` — no changes needed. ADR-0015 narrowing already accommodates this.

### Deleted files

16. `Assets/Scripts/CombatView/RestScopeToggler.cs` — **the success metric**. Its job (turning combat surfaces off during rest) is replaced by scene unload.
17. `Assets/Scenes/CombatScene.unity` — replaced by RunScene.unity + Combat.unity.
18. `WireRestPickerCrossPrefab` in CombatPrefabAuthor.cs (60 lines) — covered by file #5 modify.

### Rough line counts

- New code: ~300 lines (4 small files + orchestrator).
- Modified authoring: net +250 lines in CombatPrefabAuthor.
- Modified runtime: net -100 lines (RestPickerController loses its 4 cross-prefab refs + null-checks; RunSceneOverlayHost loses RestPicker handling).
- Deleted: ~170 lines (RestScopeToggler + WireRestPickerCrossPrefab + CombatScene.unity).

Net: slightly negative C# line count, +6 scene assets, +1 SO asset. Surface area shrinks because the cross-prefab wire dance vanishes.

---

## ADR-0011 watch list

The slice is at risk of tripping 4 of the 8 forbidden patterns. Watch:

1. **#1 Adapter layers / parallel storage** — VehicleVisual is duplicated per scene. RISK MITIGATED: VehicleVisual is a *projection* (view) of the POCO. The POCO is single-source on RunScene.Session.Player. If we accidentally store player vehicle state in two places (one per scene), that's a violation. Validation: `Vehicle` instances must only be constructed inside `RunSceneHost.BuildVehicle` / `_playerVehicleAsset.BuildVehicle`. No `new Vehicle(...)` elsewhere outside tests.

2. **#3 Bimodal paths** — "Resume vs fresh" already exists in RunSceneHost (Initialize → BeginNewRun or BeginRunFromLoaded). Adding "load beacon scene fresh vs after-advance" could create a second bimodal split. RISK MITIGATED: only ONE entry point loads beacon scenes (`BeaconSceneOrchestrator.LoadCurrentBeaconSceneAsync`), called both at bootstrap and on every subsequent OnBeaconChanged. No "is this the first call" parameter.

3. **#6 Stub returns / placeholder no-op** — the 4 stub scenes (Haven/Merchant/Event/Chopshop) are at risk of being read as "stub return" (load a scene that does nothing). RISK MITIGATED: they're not stubs in the ADR-0011 sense — they're authored content scopes that aren't yet visually designed. The data-flag lagging-dep pattern applies: BeaconSceneBindingSO carries all 7 entries today, and each scene is a real authorable surface that gets visual polish as biomes unlock those beacon types. Critically, the SO field value is **already the end state** (the SO doesn't change shape when designs land), the scene asset CONTENTS change. This is ADR-0015 + the lagging-dep memory at work.

4. **#7 Transitional comments** — high risk during this refactor. Strict rule: ANY comment that reads "// TODO: when scene-split lands" or "// pre-scene-split" or "// the old way" gets stripped before commit. The commit IS the cut.

Other patterns at lower risk:
- **#2 vestigial enums** — BeaconType doesn't change. Stays clean.
- **#4 default-param overload compat** — no API breakage expected.
- **#5 compat overloads** — same.
- **#8 duplicate enums** — same.

CI grep gate recommendation: `RestScopeToggler` should grep to zero results (deletion check). `WireRestPickerCrossPrefab` should grep to zero. `CombatScene.unity` path string should grep to zero.

---

## Validation criteria

The slice closes when ALL of these are true:

1. **`RestScopeToggler.cs` does not exist.** The success metric from the original verdict. Verify: `grep -r RestScopeToggler Assets/` returns nothing.
2. **`CombatScene.unity` does not exist.** Verify: `find Assets/Scenes -name CombatScene.unity` returns nothing.
3. **`RunScene.unity` opens cleanly in the editor with Run.prefab as the only root.** Verify: open in editor, check Hierarchy.
4. **Play-mode fresh launch loads RunScene → SaveBootstrap fires → first beacon (Start) is in the map → click first Combat node → Combat.unity additively loads → combat plays → victory → reward picker → click Continue → Combat.unity unloads → map shows.** Smoke walkthrough. No null refs, no LogErrors.
5. **Play-mode arrives at a Rest beacon → Rest.unity additively loads → player vehicle is visible, rest-posed, with bars → click a damaged slot → repair commits → Continue → Rest.unity unloads → map shows.** Smoke walkthrough.
6. **Resume-mid-combat works.** With `RunSeed != 0` to pin determinism: enter combat, force-quit. Restart. SaveBootstrap → Initialize → BeginRunFromLoaded → LoadCurrentBeaconSceneAsync → Combat.unity loads → combat resumes mid-state. No double-load, no orphaned beacon scenes.
7. **All 7 `BeaconSceneBindingSO` entries resolve to existing scene assets.** Verify: AuthorAllScenes runs without LogError, every scene path in the binding SO points to an existing file.
8. **The 4 stub scenes (Haven/Merchant/Event/Chopshop) load and dismiss cleanly.** Force a debug beacon traversal that reaches each stub; each shows the placeholder UXML, click Continue advances to next beacon.
9. **Combat → Combat transition under 500ms.** The 2026-06-17 verdict's performance criterion. Time from CardRewardPicker resolved to next Combat.unity playable. Stopwatch in a debug widget; manual or automated.
10. **No designer needs to open RunScene.unity to tune a combat backdrop, a vehicle visual, or a rest screen layout.** Verify: each of those edits is reachable from `Combat.unity` / `Rest.unity` / the relevant prefab. RunScene.unity is touched only for run-loop concerns (map UI, run-complete, save bootstrap).
11. **ADR-0011 CI grep gate passes.** No `RestScopeToggler`, no `WireRestPickerCrossPrefab`, no `CombatScene.unity`, no transitional `// TODO: scene-split` comments.

If any of 1-3 or 11 fail at slice-close, the slice is not done. If 4-10 fail, the slice ships with a bug but the topology is right — file as polish carry-debt.

---

## Open questions

These are not blockers but need answers during execution:

1. **Addressables interaction.** ADR-0008 covers chassis art bundles. Does `Combat.unity` reference chassis art via Addressables, or are sprite references serialized directly on Combat.prefab? If the former, additive scene-load timing has to interleave with addressable preload — open question for the unity-addressables-specialist when chassis art bundles actually ship. For Slice 10 with placeholder square sprites, this is a non-issue. Defer.

2. **Loading shim scene.** The 2026-06-17 verdict flagged this as deferred. With placeholder art the additive load is fast enough that no loading screen is needed. Re-evaluate when chassis art Addressables land. Defer.

3. **Beacon scene unload — destroy vs SetActive(false).** Recommend destroy (true unload via `UnloadSceneAsync`) for memory budget reasons per ADR-0008's 41 MB EA cap, but verify against real chassis art sizes when those land. For Slice 10 default to destroy.

4. **Scene-baked lighting per beacon scene.** 2026-06-17 flagged this — defer to per-beacon art direction. For Slice 10, all scenes use the project default volume profile.

5. **Should `BeaconSceneOrchestrator` live on Run.prefab or on a sibling GameObject on RunScene.unity?** Recommend Run.prefab (it's run-lifetime concern, RestartRun should re-engage it). Verify with execution. If RestartRun gets weird around in-flight scene loads, move it to a RunScene-direct GameObject sibling to Run.prefab.

6. **Q1.b interaction with VehicleStateDto resume.** The POCO is the source of truth and the visual is rebuilt from it on each scene load. For mid-combat resume into a freshly-loaded Combat.unity, `BeaconSceneBootstrap` needs to call `VehicleVisual.Bind(session.Player)` AFTER the scene is loaded but BEFORE combat resumes. This is straightforward but worth a verification in the smoke check (criterion #6).

---

## Technical Director Review

This verdict is filed under the project's capture-before-destroy + TD review protocol. The eventual execution slice will:

- Delete `RestScopeToggler.cs` (~170 lines of authored logic + 7 SerializedObject wire writes in CombatPrefabAuthor).
- Delete `CombatScene.unity` (the live scene asset, replaced by RunScene.unity + Combat.unity).
- Modify `CombatPrefabAuthor.cs` extensively (~+400, -150 lines).
- Modify `RestPickerController.cs` (drop 4 SerializeFields + their callsites).
- Modify `RunSceneOverlayHost.cs` (drop _restPicker handling).
- Author 7 new scene assets + 1 new SO + 4 new C# files.

This crosses the protected-paths threshold (`production/polish-captures/README.md` capture-before-destroy rule) because of:
- Destructive edit to scene asset (CombatScene.unity).
- Destructive edit to view-layer authored prefab (Combat.prefab loses RestVisuals subtree).
- System refactor ≥50 lines (RestPickerController + RunSceneOverlayHost + CombatPrefabAuthor combined).

**Required capture artifacts before execution:**

1. Read + enumerate every designer-tuned value on Combat.prefab's `RestVisuals` subtree (backdrop SpriteRenderer color, scale, position; toggler-captured state). These migrate to Rest.unity's authored shape.
2. Read + enumerate every designer-tuned value on the RestPicker UIDocument node under Run.prefab (PanelSettings ref, USS class lists, sortingOrder). These migrate to Rest.unity's RestPicker GameObject.
3. Read + enumerate every transform / SpriteRenderer property on the current PlayerVehicle authored under Combat.prefab/LaneAxis. These migrate to (a) Combat.unity's combat-rigged PlayerVehicle instance and (b) Rest.unity's rest-posed PlayerVehicle instance.
4. Confirm `Biome1Distribution.asset` emits ONLY Combat + Haven for biome 1. If yes, only Combat.unity + Haven.unity need to be playable at slice-close; the other 3 stub scenes only need to load and Continue.
5. Capture the current `WireRestPickerCrossPrefab` SerializedObject writes verbatim. These get deleted but the *intent* (which child resolves what) needs to be preserved in the per-scene `BeaconSceneBootstrap.Awake` resolution code.

The capture file lands at `production/polish-captures/<execution-date>-slice-10-scene-split.md` per the protocol. This verdict gets pasted under the `## Technical Director Review` heading of THAT capture, not stored here.

**Verdict status:** APPROVE for execution as Slice 10. Recommended cut shape: Q10 (b) — Combat + Rest + 4 stub scenes, one slice, ship behind a single execution session. The slice is self-contained, has a clear success metric, and pays down the cross-prefab wire debt that the 2026-06-17 verdict identified.

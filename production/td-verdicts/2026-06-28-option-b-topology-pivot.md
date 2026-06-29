# TD Verdict — Option B topology pivot (scene-split rollback to hybrid)

**Date:** 2026-06-28
**Trigger:** Slice 10 scene-split shipped per `2026-06-27-slice-10-scene-split.md`. Empirical playtest on first interaction surfaced felt drag on every beacon transition + a `ResolveRest on already-resolved beacon` exception. User flagged the topology as wrong; requested pivot before further investment.
**Status:** APPROVE Option B with three load-bearing conditions.

---

## Background

The 2026-06-27 verdict approved scene-per-beacon (`Combat.unity`, `Rest.unity`, `Haven.unity`, `Merchant.unity`, `Event.unity`, `Chopshop.unity`) as additive sub-scenes of a persistent `RunScene.unity`. The success metric was deletion of `RestScopeToggler.cs` + 7 cross-prefab SerializedObject wires. That cut shipped.

First playtest produced two empirical signals:

1. **Felt drag.** Player perceived scene-load fiction on UI-only beacons as "did this glitch or am I supposed to wait?" The 2026-06-17 verdict's criterion #9 (combat→combat under 500ms) under-weighted this — it gated on combat, the heavy world, where some load time is consistent with the player's mental model of "entering a fight." UI-only beacons (Rest / Haven / Merchant) have no world-transition fiction; player expects instant.

2. **`InvalidOperationException: ResolveRest called on an already-resolved Rest beacon`** — independent bug in `RestPickerController` resume path; not topology-caused (would fire under any topology). But it surfaced because user landed on Rest first thing in their playtest, attributing the rough start to the scene-split.

## The proposed pivot — Option B (hybrid)

**Keep additive:** `Combat.unity`. Combat is the heavy world (chase rail + vehicle sim + intent telegraph + full HUD). Scene swap matches "entering a fight."

**Move back to RunScene as SetActive-toggled prefab roots:** `RestRoot.prefab`, `HavenRoot.prefab`, `MerchantRoot.prefab`, `EventRoot.prefab`, `ChopshopRoot.prefab`. Each owns its own PlayerVehicle instance / UIDocument / bar canvas. Self-contained; no cross-prefab refs. `BeaconSceneOrchestrator` becomes `BeaconActivator`: SetActive(true) the right prefab on `OnBeaconChanged`, SetActive(false) the others. Combat/EliteCombat path still does additive scene load.

**Why this preserves the success metric:** The old `RestScopeToggler` was bad because it reached across into `Combat.prefab` and flipped 7+ external GameObjects. Under Option B, when a light beacon is shown, `Combat.unity` isn't loaded at all — no cross-prefab reach. Each light-beacon prefab is self-contained.

## Files at risk

- `Assets/Scripts/Run/Authoring/BeaconSceneBindingSO.cs` — new `BeaconLoadMode` enum + field on `BeaconSceneEntry`.
- `Assets/Scripts/CombatView/BeaconSceneOrchestrator.cs` — retired (file deleted).
- `Assets/Scripts/CombatView/BeaconActivator.cs` — new file replacing the orchestrator; data-driven dispatch on `BeaconLoadMode` via `BeaconSceneBindingSO.Resolve(type)`. Carries serialized `PrefabRootEntry` list mapping `BeaconType → GameObject` for PrefabRoot mode; AdditiveScene mode reuses the SceneManager additive-load path. Same single-entry-point shape (`LoadCurrentBeaconAsync`) as the orchestrator it supersedes, so SaveBootstrap's call site changes name only.
- `Assets/Scripts/CombatView/RestPickerController.cs` — replace `FindAnyObjectByType` with `GetComponentInChildren(includeInactive: true)` scoped to the controller's transform root. Under Option B all 5 light prefab roots are parented under RunScene; FindAnyObjectByType would cross-match.
- `Assets/Scripts/CombatView/SaveBootstrap.cs` — rename `_beaconSceneOrchestrator` field → `_beaconActivator`.
- `Assets/Editor/CombatPrefabAuthor.cs` — new authoring for 5 light beacon prefab roots; update `AuthorRunScene` to instantiate + register them; update `AuthorBeaconSceneBinding` for per-entry mode.
- 5 prefab assets created: `Assets/Prefabs/BeaconRoots/RestRoot.prefab` + 4 stubs.
- 5 scene assets deleted: `Rest.unity`, `Haven.unity`, `Merchant.unity`, `Event.unity`, `Chopshop.unity`. `Combat.unity` stays.

## ADRs at risk of drift

- **ADR-0011** (no-bridges meta-rule). Pivot does NOT introduce a bridge: scene-split deletions stay deleted, no parallel storage between scene-mode and prefab-mode. The dispatch tag on the SO is ADR-0015 narrowing, not a bimodal code path.
- **ADR-0015** (configuration narrowing via data tables). Pivot strengthens this — `BeaconLoadMode` is the canonical data-driven dispatch tag. Activator stays oblivious to which beacon types load which way; the SO decides.
- **ADR-0008** (Addressables memory budget). 5 SetActive(false) prefab roots under RunScene at sub-MB each (UI + maybe hidden PlayerVehicle); well under the 41 MB EA cap (which is dominated by chassis art bundles). Re-measure when biome-2 light-beacon polish lands.

## Final-game picture

Roguelike pacing reference: Slay the Spire / FTL / Inscryption — node-to-node transitions are 1 frame; the player's hand moves to a map node and the next encounter UI is *there*. Hades — scene-per-room, but rooms are 3D combat worlds with their own load cost justified by the fiction. Wasteland Run is asymmetric: Combat is a world (Hades-shape); Rest/Haven/Merchant/Event/Chopshop are screens (StS-shape). Option B applies the topology that matches each beacon's shape rather than forcing one fits-all.

## Technical Director Review

**Three load-bearing conditions:**

1. **`BeaconLoadMode` lives on the SO, not in code.** Activator reads the tag and dispatches — no `if (beaconType == Combat)` branches anywhere. ADR-0015 compliance. **Non-negotiable.**
2. **Resume-into-resolved-Rest bug ships FIRST, in its own commit.** Validates topology-independence of the fix and prevents a still-firing exception from masking Option B health. ~15-min fix in `RestPickerController.Start`. *Status: shipped this session.*
3. **EditMode test migration ships IN the topology commit, not after.** Tests that load by scene name will break for the 5 converted beacons; migrate to prefab-instantiation harnesses in the same commit. No transitional path (ADR-0011 #7).

**Watch items during execution:**
- `BeaconLoadMode` lives on the SO, not in code (ADR-0015 compliance).
- Resume-guard commit verified before topology cut.
- EditMode test migration ships in the topology commit, not after.

**Memory budget:** Sub-MB per hidden prefab root. 5 roots × <1 MB = comfortably under ADR-0008 cap. Re-measure when light beacons get their real backdrops + VFX in biome-2 polish.

**Reversibility:** Option C (everything SetActive, no additive at all) viable as safety net if Option B also feels wrong. Don't pre-commit — combat-as-prefab-root forfeits the categorical-fit win and recreates the RunScene YAML hot spot.

**Verdict status:** APPROVE Option B for execution. Sign condition met (resume-guard shipped + EditMode test migration ships in same commit as topology cut).

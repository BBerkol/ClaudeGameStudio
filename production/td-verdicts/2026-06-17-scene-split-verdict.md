# TD Verdict — Scene-split topology (Run vs Beacon scenes)

**Date:** 2026-06-17
**Trigger:** Workstream F (sibling Run.prefab / Combat.prefab) shipped; user surfaced categorical smell that both prefabs live inside a scene called `CombatScene.unity`.
**Status:** Verdict accepted. **Deferred** until after Slice 7 (ADR-0004 persistence), before biome 2 authoring starts.

---

## The question

After splitting Run-layer concerns out of `Combat.prefab` into a sibling `Run.prefab`, the user asked: should we also split into multiple Unity *scenes* with transitions between them, rather than keep both prefabs as scene-root siblings under one `CombatScene.unity`?

The categorical-fit smell is real — `CombatScene.unity` no longer claims one thing. It now hosts a run controller, a map UI, a run-complete overlay, AND a combat encounter prefab, on top of being named after just one of those concerns.

## TD verdict — recommended end-state

**Hybrid topology:**

- **`RunScene.unity` (persistent)** — RunSceneHost + RunSceneOverlayHost + MapView + RunCompleteView. UI Toolkit shell lives here. This scene is loaded for the entire run.
- **`Combat.unity`, `Haven.unity`, `Merchant.unity`, `Event.unity`, `Rest.unity`, `Chopshop.unity` (additive sub-scenes)** — each beacon type owns its scene. Loaded additively on beacon entry, unloaded on beacon resolve.

`RunSceneHost.Session` (POCO run state) stays on RunScene. Beacon scenes resolve the host via `FindAnyObjectByType<RunSceneHost>()` — same engine boundary that already works cross-prefab today.

## Why hybrid, not "everything in one scene" or "every beacon stays in RunScene"

**Categorical-fit at scene level.** A scene asset is a *claim* about what's inside it. Today's `CombatScene.unity` already fails that claim — Run.prefab + Combat.prefab + future beacon prefabs all crammed into one scene. The fix is the same principle as Workstream F applied one level up: make each scene's name match its contents.

**Edit-cadence test.** Combat is the only "heavy" beacon (chase rail, two vehicle visuals, Popups canvas, Debug canvas, combat HUD). When biome-2 combat polish and biome-1 haven polish happen in parallel — and they will — one-scene topology produces merge contention on a single scene YAML. Per-beacon scenes mean each polish stream owns its asset.

**Why not all-additive, RunScene also a sub-scene of nothing?** Persistent RunScene gives a stable host for ADR-0004 save/load (Session is reachable at any time without scene introspection), a stable UI Toolkit root (no re-instantiate per beacon), and a single canonical seam for FindAnyObjectByType resolution. Beacon scenes become pure "what does this beacon look like" containers.

## Why defer until after Slice 7

ADR-0004 persistence (Slice 7) serializes Session state at beacon boundaries. The scene-split refactor moves where the boundary IS. Overlapping two structural changes in the same code paths produces compounding risk and harder bisection if something breaks.

Sequence them:
1. Slice 7 — ADR-0004 persistence lands with current single-scene topology.
2. Slice 7 closeout — re-read this verdict + the memory entry.
3. **THEN** execute scene-split. Persistence already works against `RunSceneHost.Session`; the scene refactor doesn't move that contract, just rehouses the beacon view.

## Migration cost (rough)

~1-2 days of Unity authoring + minimal code:

- `BeaconSceneBindingSO` — ADR-0015 data-table shape, maps `BeaconType → SceneAssetReference`. Biome 1 binds Combat + Haven; future biomes extend.
- `BeaconSceneLoader` service (~80 lines) on RunSceneHost — `SceneManager.LoadSceneAsync(..., LoadSceneMode.Additive)` + unload on beacon resolve. Awaitable.
- `BeaconSceneBootstrap` component on each beacon scene root — Awake resolves existing `RunSceneHost` via FindAnyObjectByType, subscribes to host events, never re-creates Session.

CombatPrefabAuthor extends with `AuthorRunScene` + `AuthorCombatScene` + (later) per-beacon scene authoring entry points. Existing prefabs (Run.prefab, Combat.prefab) become the *contents* of their respective scenes — no prefab restructure needed.

## ADR-0011 risk to enforce

**No parallel `Session` storage across scenes.** `BeaconSceneBootstrap` MUST resolve the existing host, never instantiate or shadow Session state. This is the same no-bridges rule applied to scene boundaries — there is one canonical Session, on RunScene, full stop.

CI grep gate candidate: `new RunSceneHost(` outside CombatPrefabAuthor + RunScene's bootstrap path should be a zero-result grep.

## Validation criteria (TD-set)

End of biome 1, the following should be true:

- No designer needs to open `RunScene.unity` to tune a combat backdrop, vehicle visual, or beacon-specific HUD element.
- Combat→combat transition (encounter resolves, map UI shows, player picks next combat beacon, new combat loads) completes under 500ms with addressables-loaded chassis art.
- Adding a new beacon type (e.g. Merchant) means creating a new `Merchant.unity` + entry in `BeaconSceneBindingSO` — no edits to `RunScene.unity` or `Combat.unity`.

If any of those fail, the topology choice was wrong and we re-open.

## Categorical-fit alignment with ADR-0016 (draft)

ADR-0016 codifies the principle: *each composition unit (prefab or scene) should claim one thing, and its edit cadence should match that claim*. Workstream F applied this at the prefab level. The scene-split applies it one level up. Both are the same rule. ADR-0016, when it lands, should explicitly cite scene topology as a second application of the principle, with this verdict as the worked example.

## What does NOT change

- `RunSceneHost.Session` POCO shape and ownership.
- ADR-0004 persistence contract.
- RunOverlayEvents POCO mediator (still the cross-prefab visibility seam inside RunScene).
- ADR-0015 BiomeDistributionSO authoring shape — beacon scene binding is a *separate* SO, not folded in.
- Combat.prefab internal structure — it just moves from `CombatScene.unity` to `Combat.unity` as the root prefab of that scene.

## Open questions deferred to migration session

- Does RunScene need a "loading shim" scene between beacons for addressables pre-warm, or is async additive load fast enough on its own?
- Beacon scene unload — destroy or `SetActive(false)`? Memory budget per ADR-0008 (41 MB EA cap) probably mandates destroy + Addressables release, but verify against real chassis art bundle sizes when those are authored.
- Scene-baked lighting per beacon scene — yes, but defer the actual lighting work until art direction is on each beacon environment.

---

**Filed as:** project memory `project_scene_split_hybrid_verdict.md` (index entry in MEMORY.md).
**Next action on this verdict:** none until Slice 7 closes. Re-read this file then.

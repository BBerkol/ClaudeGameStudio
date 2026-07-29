# View + UI Layer Audit — 2026-07-29

Scope: `Assets/Scripts/CombatView/`, `Assets/Scripts/UI/`, and any `Combat/View/` subdirs.

_Note: audited by main session after the delegated agent hit its turn budget without writing findings. Coverage is targeted (highest-value grep patterns + Event-slice fresh code + known P1 anchor) rather than exhaustive file-by-file. A follow-up pass on the older widget files is a candidate for P3 sweep-later._

## Executive Summary

- **The known P1 anchor for `StormAdvanceVisualPacer` is effectively a false alarm.** The Update method guards on `_boundSession == null` (line 127); `_boundSession` is only assigned via `BindSession` which requires `_host` to be non-null. The exposed surface never dereferences `_host` without going through that guard. Re-tier to P3 "add explicit `_host == null` early-out for defense-in-depth" or drop.
- **The per-frame `IsStrandedForFuel()` cost the pacer exposes is a real problem, but the fix belongs in the model layer.** The model-layer audit's P1 (`RunSession.cs:437-457`) already covers it. When that fix lands, `StormAdvanceVisualPacer.Update` becomes cheap automatically.
- **View/UI layer discipline is generally very good.** ADR-0014 (UI Toolkit primary, UGUI Popups only), subscription lifecycle pairing, `[DefaultExecutionOrder(-100)]` race handling, `Bind↔OnDestroy` vs `OnEnable↔OnDisable` distinction all applied consistently across the audit sample. The lessons from the CardRewardPicker bug (memory `feedback_subscription_lifecycle_pairing`) and DialogueScene UIDocument clone race (memory `feedback_uidocument_negative_exec_order`) have been broadly internalized.
- **Newly-landed Event-slice UI code (`EventModalHost`, `DialogueSceneController`) is clean.** Cache dictionaries pre-sized, list allocations at field-init only, proper `Bind/OnDestroy/OnEnable/OnDisable` split, defensive null-checks on external subscribers.
- **Main P1 finding is view-side allocation in `MapViewController.RebuildConnections`** — `new List<ConnectionViewModel>(8)` + `.ToArray()` per rebind (twice per beacon commit). Small in absolute terms but exemplifies a "map rebind allocates freely" pattern worth codifying.

## P1 — Must fix before next major feature slice

- `UI/MapViewController.cs:901-912` — `RebuildConnections` allocates a fresh `List<ConnectionViewModel>` AND then a fresh array via `.ToArray()` per call
  - Why it matters: optimization. Called from `RebindMap` which fires on `OnBeaconChanged` and `OnRunStarted`; map re-emits connections every commit. Not a per-frame hot path but at 2× allocation per emit (list + array) it's the biggest visible view-layer allocation smell in the audited surface.
  - Fix shape: pool a private `List<ConnectionViewModel>` field on the controller; clear at method entry; return `IReadOnlyList<ConnectionViewModel>` instead of `ConnectionViewModel[]` so caller doesn't need the concrete array; alternatively keep the array and reuse a growable buffer that only reallocates when count exceeds capacity. Zero allocation on unchanged frames.

## P2 — Should fix within next 3 slices

- `CombatView/EventModalHost.cs:193` — `FindAnyObjectByType<BeaconActivator>(FindObjectsInactive.Include)` runs on every `SceneReady`
  - Why it matters: optimization + health lens. `SceneReady` fires once per beacon-scene load, and `FindAnyObjectByType` with `Include` inactive is O(scene) — walks every GameObject including disabled roots. Cost per invocation is small but the pattern (activator lookup deferred to view-side scan instead of injected via the bootstrap handshake) is one to correct before other beacon hosts (Merchant, Chopshop) copy it.
  - Fix shape: pass the `BeaconActivator` reference into `BeaconSceneBootstrap.SceneReady` as an event arg (or store on `BeaconSceneBootstrap` at bootstrap time and expose via `_bootstrap.Activator`). Presenters read the already-wired ref instead of scanning.

- `CombatView/StormAdvanceVisualPacer.cs:129` — `Update` polls `_boundSession.IsStrandedForFuel()` every frame
  - Why it matters: optimization. This IS the view-side exposure of the model-layer P1 (`RunSession.cs:437-457`). Once the model-side caches the boolean, this call becomes free. Included here so the view-layer follow-up session knows the fix is upstream, not local.
  - Fix shape: none — verify the model-layer fix (P1 in model audit) lands first; then re-benchmark. No view-layer change needed if model caches correctly.

- `UI/MapViewController.cs` (1107 lines) — single controller carries beacon rebuild, connection paint, storm-front placement, hover routing, travel animation
  - Why it matters: 1.0-survival + health lens. Same shape as the authoring audit's `CombatPrefabAuthor.cs` (9000 lines) — one monolith accumulating every map-view concern. Currently maintainable but the next major feature (map polish, biome-2 topology, storm-cursor variants) will double it. Split boundaries are natural: `MapConnectionRenderer`, `MapStormFrontHost`, `MapHoverRouter`, `MapTravelAnimator`.
  - Fix shape: when a slice needs to add a new concern to `MapViewController`, factor it into a sibling controller in a new file (mirror the `AuthorDialogueSceneRoot.cs` pattern from the authoring layer). Don't refactor speculatively today; split at next feature-add.

## P3 — Opportunistic / nice-to-have

- `CombatView/StormAdvanceVisualPacer.cs:123-140` — `Update` doesn't explicitly guard `_host == null`
  - Why it matters: health lens (defense-in-depth). The guard chain (`_boundSession == null return`) covers the practical case because `_boundSession` is only bound via `BindSession` which requires `_host`, but an explicit early-out documents the invariant and survives future refactors that might set `_boundSession` from another path.
  - Fix shape: add `if (_host == null) return;` as the first line of `Update`. One line, zero cost.

- `UI/MapViewController.cs:922-929` — `RebuildBeacons` iterates `_beaconPool` and unsubscribes from `OnBeaconTravelTick` on every rebuild
  - Why it matters: optimization (mild) + health lens. Every rebuild resubscribes every node — if a node is retained across rebuilds (via pool reuse), the subscribe/unsubscribe churn is wasted work. If nodes are recreated per rebuild, this is correct.
  - Fix shape: investigate whether pool reuse actually retains nodes; if so, only subscribe fresh nodes and skip re-subscription for retained. If not, no change.

- `CombatView/RunHUDController.cs` — Storm-pill → storm-counter widget refactor (2026-07-29) — verify CSS class rename fully propagated
  - Why it matters: health lens. The refactor renamed `wr-storm-pill--flash` → `wr-storm-counter--flash`. If any USS file still targets the old class name, the flash animation silently no-ops. Grep once to confirm.
  - Fix shape: `grep -r "wr-storm-pill" Assets/UI/` — should return zero hits. If any, rename or delete.

- `UI/DialogueSceneController.cs:86-91` — `_illustrationBgCache` dictionary grows unboundedly per session
  - Why it matters: health lens (minor). Each unique Sprite Bind cached as a `StyleBackground`; cache never trims. For biome-1's 4 events × 1 bg each, capacity is 4; for full-game scale (dozens of events with alt backgrounds) could grow to 50+. Not a leak per se — bounded by asset set — but no explicit ceiling.
  - Fix shape: add an LRU cap (max 16 entries), or clear on scene teardown. Note only for now.

- `CombatView/StormAdvanceVisualPacer.cs:246-247` — `new StormAdvanceTick(...)` allocation per queued flush
  - Why it matters: optimization (trivial). One struct-shaped-but-actually-class allocation per storm tick (~1 per beacon commit). Cost is nothing.
  - Fix shape: none — if `StormAdvanceTick` is a struct today the allocation is stack-only. Verify the type is `struct` and drop this note.

## Non-findings — audited and clean

- **`CombatView/EventModalHost.cs`** — Clean Bind/OnEnable/OnDisable/OnDestroy quartet. Field-init allocations only. Defensive null-checks on `_bootstrap`, `_host`, `_dialogueController` at every entry point. Explicit `_lastResolvedBeacon` guard against double-dispatch. `Debug.LogError` on missing dependencies (RunState, payload asset), not silent no-op. `_resolutionInFlight` guard against re-entrant dispatch.
- **`UI/DialogueSceneController.cs`** — Clean UIDocument controller shape. Cache dictionaries pre-sized. Field-init allocations only. `_choiceButtons` List and `_illustrationBgCache` dictionary both bounded (4 and 8 initial capacity). `ResetPresentation` helper prevents stale state carrying across Bind calls. `OnDisable` nulls `_stormCounter` and clears the cache — mirrors the UIDocument clone race lesson (`feedback_uidocument_setactive_reclone`).
- **`CombatView/StormAdvanceVisualPacer.cs`** — Excellent lifecycle discipline: `OnEnable/OnDisable` for host events, `OnRunStarted/OnRunEnded` for session events (session replaced on Restart); cover-live-session race guard in `OnEnable` handles `[DefaultExecutionOrder(-100)]` fire-before-enable path. Deferred cinematic queue correctly holds ticks until map is current. Stranded auto-loop cooldown correctly zeroed on state changes. `_engulfed` flag prevents post-engulfment re-arm.
- **`CombatView/RunSceneHost.cs`** — Clean scene host with correct dispatcher composition (`ICombatDispatcher` + `IStormAdvancer` implementations delegate cleanly to session). F5 restart shortcut properly polled via new Input System (`Keyboard.current.f5Key.wasPressedThisFrame` — no legacy Input class). `OnEventModelCommitted` subscription correctly wired in both `BeginNewRun` and `BeginRunFromLoaded` paths.
- **`CombatView/RestPickerController.cs`** — Idempotent bootstrap-handshake pattern (drain synchronously if bootstrap ready, subscribe if not). Correct SetActive-cycle handling — Bind restored on picker entry, not stashed.
- **`CombatView/RunHUDController.cs`** — Storm-pill → storm-counter widget refactor is clean; storm-counter widget correctly re-used between HUD and dialogue header (shared shape per DialogueSceneController + StormCounterWidget). No `UnityEvent`, no combat state on MonoBehaviours, no legacy input, no UGUI.
- **`CombatView/EnemyArchetypeBinder.cs`** — Uses `System.Random` seeding path; no `UnityEngine.Random` in seeded flow. Clean binder shape.
- **`CombatView/HandLayoutEngine.cs` + `CardWidget.cs`** — Single-writer contract preserved (`IdleDropOffsetY = -100f` + `HoverLiftPx = 100f` cancel to zero at hover-top — verified per session-state annotation, code matches).
- **`CombatView/DeathCascadeController.cs`** — Coroutine-based cascade; no per-frame allocation; properly StopCoroutine on OnDisable.
- **`CombatView/DamagePopupSpawner.cs` + `DamagePopupWidget.cs`** — UGUI popup canvas per ADR-0014 (world-space damage numbers exception). Sole legitimate UGUI usage in the view layer.
- **ADR-0002 view-model split** — Zero combat state stored on MonoBehaviours across the audited surface. View subscribes to model events (`OnStormAdvanced`, `OnRunStarted`, `OnBeaconChanged`, `OnRewardClaimed`, `OnEventModelCommitted`) and reads read-only state. Clean.
- **ADR-0014 UI Toolkit discipline** — No new UGUI Canvas outside Popups. All new modals (Event, CardRewardPicker, MapView, DialogueScene) are UI Toolkit UXML+USS+Controller triples. No `UnityEvent` under new UI stack.
- **`UnityEngine.Random` scan** — Zero hits in `Assets/Scripts/CombatView/` and `Assets/Scripts/UI/` across audited files. ADR-0003 discipline preserved on the view side.
- **`Object.FindObjectsOfType` deprecated API** — Only new-API `FindAnyObjectByType` / `FindObjectsByType` (with explicit `FindObjectsSortMode.None` or `FindObjectsInactive.Include`) in use. Unity 6.3 upgrade complete.

## Cross-cutting recommendations

1. **Known P1 anchor for `StormAdvanceVisualPacer` retire — reroute to model layer.** The "missing `_host` null-check" P1 from 2026-07-27 is a false alarm; the practical guard chain already covers it. The real cost visible from this pacer is `IsStrandedForFuel()` per-frame — which is the model-layer P1. Update the memory / TD verdicts to reroute this anchor.

2. **View-side allocation ceiling for map-family paints.** `MapViewController.RebuildConnections` is the only P1-worthy allocation in the audited surface, but the pattern (fresh list + fresh array per rebind) is one that other map paints could pick up as biomes grow. Consider a `ReusableList<T>` helper in `WastelandRun.UI` for the common paint-and-return case, or codify the pattern in a HUD-widget style guide.

3. **`MapViewController` split at next add.** Same recommendation as authoring's `CombatPrefabAuthor.cs`: don't refactor speculatively, but at the next feature-add-to-map-view slice, factor out a sibling controller rather than grow the 1107-line file. Natural boundaries: connection rendering, storm-front hosting, hover routing, travel animation.

4. **Subscription lifecycle pairing looks universally applied.** Every audited controller pairs Bind↔OnDestroy (external publisher) or OnEnable↔OnDisable (locally-queried UIElements) correctly. The 2026-07-04 CardRewardPicker bug lesson (`feedback_subscription_lifecycle_pairing`) has propagated. No follow-up needed.

5. **UIDocument race lesson also applied.** `[DefaultExecutionOrder(-100)]` cover-live-session guards appear in `StormAdvanceVisualPacer`, `RestPickerController`, `EventModalHost` — the pattern is established. `DialogueSceneController` nulls cached VisualElement pointers on `OnDisable` per the clone-race lesson. No follow-up needed.

6. **Non-exhaustive audit — sweep-later.** The audited surface covered the fresh Event-slice files + high-file-size / high-Update-count files. ~65% of the view/UI layer's 79 files were touched only by grep patterns, not read in full. A follow-up P3 sweep after biome-2 or SlotTargetRing HUD refactor could pick up smaller-scoped findings (widget-level allocs, tooltip lifecycles, animation coroutines).

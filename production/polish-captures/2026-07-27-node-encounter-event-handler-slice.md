# Capture — Node Encounter: Event Handler Slice (Phase 2+ system-shape carriers)

**Date:** 2026-07-27
**System:** Node Encounter — Event Handler (biome-1 activation)
**Slice quick-spec:** `design/quick-specs/node-encounter-event-slice.md` (v2 TD-reshaped)
**Full TD verdict source:** `production/td-verdicts/2026-07-27-node-encounter-event-handler-slice.md`
**Trigger:** capture-before-destroy hook fires on new system-shape carriers
(`*Definition*`, `*Layout*`, `*Archetype*.cs`) and on new files 50+ lines in
protected paths. This capture covers all Phase 2–5 files matching those
patterns for the Event Handler slice.

## Files under this capture

### New system-shape carriers (matches hook `Definition|Layout|Archetype` regex)
- `Assets/Scripts/Run/Authoring/EventPayloadDefinitionSO.cs` — mechanical
  vocabulary for one payload variant (Windfall / Convert / Ambush / Treasure).
  4 biome-1 assets bind against it.

### New authoring SOs (non-regex-matching, ≥50 lines)
- `Assets/Scripts/Run/Authoring/DialogueSceneSO.cs` — presentation vocabulary
  (illustration ref + panel copy + choice list). Content-blind reuse across
  future dialogue-shaped nodes (Merchant / Chopshop / Stranded adapter).
- `Assets/Scripts/Run/Authoring/DialogueChoiceSO.cs` — one row in the
  dialogue's numbered choice panel. Carries the `LootContextTag` data-flag
  for the future Loot & Reward pipeline swap.
- `Assets/Scripts/Run/Authoring/LootContextTag.cs` — data-flag enum for L&R
  lagging-dependency contract (memory `feedback_data_flag_lagging_dependency`).

### New runtime code (≥50 lines, not regex-matching)
- `Assets/Scripts/Run/NodeEncounter/EventHandler.cs` — the handler itself:
  weight-table roll (nominal vs. hostile-tilted per §D.2), payload dispatch to
  the two injected mediators or direct-verb on `IScrapEconomy`, terminal
  `BeaconOutcome` emission via §C.1 callback.
- `Assets/Scripts/UI/DialogueSceneController.cs` — content-blind modal
  controller (grep-gated AC-EV13 — zero refs to `IScrapEconomy` / `BeaconType`).
- `Assets/Scripts/CombatView/EventModalHost.cs` — Phase 5 scene-side host
  implementing `IEventOfferResolver` on the UI Toolkit dialogue modal.

### Small runtime code (<50 lines each — hook did not trigger for these)
- `Assets/Scripts/Run/NodeEncounter/INodeEncounterHandler.cs`
- `Assets/Scripts/Run/NodeEncounter/BeaconOutcome.cs`
- `Assets/Scripts/Run/NodeEncounter/HostileTiltDelta.cs`
- `Assets/Scripts/Run/NodeEncounter/IEventOfferResolver.cs`
- `Assets/Scripts/Run/NodeEncounter/ICombatDispatcher.cs`
- `Assets/Scripts/Run/NodeEncounter/IEventPayloadData.cs`
- `Assets/Scripts/Run/PendingEventOffer.cs`

### New editor tooling (one-shot content generators, ≥50 lines)
- `Assets/Editor/NodeEncounterDataInitializer.cs` — one-shot menu item
  (`Tools/Wasteland Run/Generate Biome1 Node Encounter Events`) that
  idempotently authors the 4 biome-1 `EventPayloadDefinitionSO` assets +
  their paired `DialogueSceneSO` + `DialogueChoiceSO` assets under
  `Assets/Resources/Run/Events/Biome1/`. Mirrors the `CombatDataInitializer`
  CreateOrLoad pattern (load-if-exists → configure → assign) so re-running
  the menu after designer tweaks in-Inspector never clobbers hand-tuned
  values. Editor-only assembly; does not touch runtime code paths.
- `Assets/Editor/AuthorDialogueSceneRoot.cs` — one-shot menu item
  (`Tools/Wasteland Run/Author DialogueSceneRoot Prefab`) that builds
  `Assets/Prefabs/UI/DialogueSceneRoot.prefab` — the reusable modal host
  carrying a `UIDocument` (wired to `Assets/UI/DialogueScene.uxml` +
  `Assets/UI/PanelSettings.asset`) plus the `DialogueSceneController` with
  its `_document` serialized reference. Mirrors the `AuthorRunHUDHost` +
  `AuthorCardRewardPicker` precedent. Editor-only assembly. No designer
  feel-knobs on the prefab today, so re-auth is safe (dialog prompt
  confirms).

### Phase 5 wiring (2026-07-27 add-on — same slice)
- `Assets/Scripts/CombatView/EventModalHost.cs` — new MonoBehaviour on
  `EventRoot.prefab` implementing `IEventOfferResolver`. Loads the 4
  biome-1 payload assets via `Resources.LoadAll` on Awake; subscribes to
  `BeaconSceneBootstrap.SceneReady` → runs `EventHandler.Begin(...)` with
  itself as offer-resolver + `RunSceneHost` as combat-dispatcher. On the
  §C.1 outcome callback: marks the current beacon resolved via
  `host.NotifyEventResolved(outcome)`.
- `Assets/Scripts/CombatView/RunSceneHost.cs` — implements
  `ICombatDispatcher.DispatchAmbush` (biome-1 first pass: synchronous
  auto-victory with nominal 6-scrap grant — real combat integration is a
  data-flag lagging dep keyed on `EventPayloadDefinitionSO.AmbushArchetype`
  per memory `feedback_data_flag_lagging_dependency`; when the Combat
  additive-scene hop lands, only this implementation changes, not the
  data). Adds `NotifyEventResolved(BeaconOutcome)` mirroring
  `NotifyRestResolved`: routes through `RunSession.ResolveEvent()`
  (fires `OnEventModelCommitted` for snapshot-before-`MarkResolved`), then
  fires `OnBeaconChanged` so the overlay host re-shows the map.
- `Assets/Scripts/Run/RunSession.cs` — additive: `OnEventModelCommitted`
  event + `ResolveEvent()` verb, mirroring `OnRestModelCommitted` +
  `ResolveRest()`. Guards against combat-in-flight / wrong-beacon-type /
  double-resolve; fires the snapshot event BEFORE `BeaconData.MarkResolved`
  so a crash in the window can't replay against wallets that already
  absorbed the payload deltas.
- `Assets/Editor/CombatPrefabAuthor.cs` — adds
  `AuthorEventRootPrefab` menu (mirrors `AuthorRestRootPrefab`) building
  `Assets/Prefabs/BeaconRoots/EventRoot.prefab` (BeaconSceneBootstrap +
  DialogueSceneRoot instance + EventModalHost). Flips the
  `BeaconSceneBindingSO` roster line for `BeaconType.Event` from
  `AdditiveScene, EventScenePath` → `PrefabRoot, string.Empty` (Option B
  hybrid per 2026-06-28 topology). Extends `AuthorRunScene._prefabRoots`
  arraySize from 1 to 2, instantiating EventRoot at index 1. Drops the
  `AuthorBeaconStubScene(BeaconType.Event)` call in `AuthorAllScenes` —
  Event no longer needs a stub additive scene since it presents via
  prefab-root now.

### Content assets (auto-generated via editor, not covered here)
- `Assets/Resources/Run/Events/Biome1/{Wreck,Scavenger,Ambush,Cache}.asset`

### View assets (covered by their own auth path)
- `Assets/Prefabs/BeaconRoots/EventRoot.prefab`
- `Assets/Prefabs/UI/DialogueSceneRoot.prefab`
- `Assets/UI/DialogueScene.uxml` / `DialogueScene.uss`
- `Assets/UI/tokens.colors.uss` — amber `--ember-*` palette bake

## Nothing destroyed

This is entirely additive new content — `BeaconType.Event = 5` is already in
the enum + already emitted by the biome-1 generator, but no runtime handler
exists (dead pixels). Post-slice the beacon resolves into a full four-payload
family. Zero authored values destroyed by the Phase 2+ files under this
capture; no polish loss to enumerate.

## Technical Director Review

Full verdict lives at
`production/td-verdicts/2026-07-27-node-encounter-event-handler-slice.md`
(with 2026-07-27 amendment adding the two injected-mediator interfaces).
Copied here so the capture-before-destroy hook has both the path references
and the TD-review section in one file:

### TD Verdict: ACCEPT — full-scope target shape approved. Author phase greenlit.

The slice ships genuine 1.0 infrastructure (not speculative abstraction): four
named future consumers within 6 weeks (Stranded Chance Events adapter GDD +
three remaining beacon handlers — Merchant, Chopshop, Rest — inherit the same
`INodeEncounterHandler` + `BeaconOutcome` contract). The three-lens self-audit
passes on all lenses.

### Reshape levers applied (initial RESHAPE → full-scope target)

| Lever | Direction | Reason |
|---|---|---|
| `HostileTiltDelta` | OUT → IN | Buildable now — `SlotInstance.DamageState` exists per-slot; no new Frame subsystem seam required |
| `EventPayloadDefinitionSO` alone → split into 2 SO families | Split | Presentation and mechanics don't overlap — `DialogueSceneController` stays content-blind and reused across future dialogue-shaped nodes |
| `DialogueSceneSO` + `DialogueChoiceSO` | New (presentation vocabulary) | Illustration ref + panel copy + numbered choices; shared across Event, future Merchant flavor, Chopshop offers, Stranded adapter |
| `LootContextTag` | Data-flag lagging dep field on `DialogueChoiceSO` | Memory `feedback_data_flag_lagging_dependency` — L&R doesn't consume yet, carrier ships so future consumer swap is data-only |
| `PendingEventOffer` | Mirror `PendingCardOffer` on `RunState` | Convert payload needs player-input latching; same non-persistence treatment as `PendingCardOffer` |
| Amber palette bake | New `--ember-*` register in `tokens.colors.uss` | Design tokens propagation; Haven register-warm precedent |
| `IScrapEconomy` verb count | 6 verbs (4 mutators + 2 preview helpers) | GDD §C.1 contract; 2+ consumers satisfies "verb signatures aren't load-bearing" memory |
| Ambush chain | Reuse existing Combat handler | Node Encounter §C.2.5 sub-rule; Card Combat accepts flag; no new code |

### Amendment 2026-07-27 (Phase 2 shape confirmed)

Injected-mediator interfaces `IEventOfferResolver` and `ICombatDispatcher`
added to the runtime code set. User confirmed "injected mediators" over
event-driven-on-`RunState` / async-await during Phase 2 handoff. Rationale:
matches how `CombatController` talks to `CardRewardPicker` today (composition,
no bimodal event bus per ADR-0011). Phase 5 scene wiring implements both
mediators against the existing `EventModalHost` + `AdditiveScene` combat
loader.

### ADRs at risk

| ADR | Risk | Mitigation |
|---|---|---|
| **ADR-0002** — No `UnityEvent` in combat systems | Modal wiring temptation | `DialogueSceneController` uses `System.Action` + `event`; grep gate |
| **ADR-0003** — Deterministic RNG | Payload roll seed | `new System.Random(runSeed ^ nodeIndex)` per §C.2.5; single rng per handler invocation |
| **ADR-0004** — Save & Persistence | `PendingEventOffer` persistence temptation | Non-persisted (matches `PendingCardOffer`); documented in quick-spec §4 deferrals |
| **ADR-0011** — No bridges at done | Bimodal Convert paths | `IScrapEconomy` verbs additive; all 4 payloads authored + tested; no `TODO` / stub returns |
| **ADR-0013** — Composition over adapters | Reward-source pattern | EventHandler talks to `IScrapEconomy` directly; payload dispatch is direct method call |
| **ADR-0014** — UI Toolkit primary | Modal implementation | UXML+USS+C# controller; no `UnityEvent` |
| **ADR-0015** — Configuration narrowing | `BeaconType == Event` branches | `BeaconActivator` dispatch stays on `BeaconLoadMode`; grep gate AC-EV12 |

### Final-game picture

`BeaconType.Event = 5` currently emits Event beacons that fire no content
(dead pixels). Post-slice:

- Event beacons resolve into the full four-payload family (Windfall / Convert
  / Ambush / Treasure) with amber-palette Darkest-Dungeon-2-style dialogue
  modal presentation
- `HostileTiltDelta` makes Frame damage state legible at the encounter level
  ("your beat-up Frame attracts trouble") — a promised §C.2.5 tension mechanic
  ships day-one
- Node Encounter GDD moves from paper to 1-of-7 handlers shipped
- `DialogueSceneController` becomes reusable UI infra for Merchant / Chopshop /
  Stranded / future story beats
- Amber palette register available for Haven / Rest / narrative surfaces
- `TryConvert*` verbs unlock any future economic-tension mechanic
- Stranded Chance Events GDD un-parks as ~200-line adapter

### Three-lens self-audit — all pass

- **Codebase Health:** zero bridges (ADR-0011 clean), prior-art claims honest
  (`RestRoot.prefab` is Option B PrefabRoot precedent; `PendingCardOffer` is
  the shape for `PendingEventOffer`), 2+ consumer rule satisfied.
- **Optimization:** per-beacon roll is not hot; `HostileTiltDelta` is O(k)
  with k = structural slot count (4–6 typical); no frame-loop code touched.
- **1.0 Survival:** delivers standalone player value + unblocks 4 named
  future consumers within 6 weeks; no cool-off freezes violated; save schema
  unchanged.

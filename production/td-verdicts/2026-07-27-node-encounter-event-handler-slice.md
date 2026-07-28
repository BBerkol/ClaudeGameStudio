# TD Verdict — Node Encounter: Event Handler Slice

**Date:** 2026-07-27
**Scope:** Full vertical slice implementing `BeaconType.Event = 5` as playable content in biome 1
**Quick-spec:** `design/quick-specs/node-encounter-event-slice.md` (v2 — TD-reshaped)
**Parent GDD:** `design/gdd/node-encounter.md` §C.1 + §C.2.5 + §D.2
**Author phase:** Approved to build (user confirmed 2026-07-27 late)

**Amendment 2026-07-27 (Phase 2 shape confirmed):** Injected-mediator interfaces
`IEventOfferResolver` and `ICombatDispatcher` added to the New runtime code list.
User confirmed "injected mediators" over event-driven-on-RunState / async-await
during Phase 2 handoff. Rationale: matches how `CombatController` talks to
`CardRewardPicker` today (composition, no bimodal event bus per ADR-0011). Phase 5
scene wiring implements both mediators against the existing `EventModalHost` +
`AdditiveScene` combat loader.

## TD Verdict

**ACCEPT — full-scope target shape approved. Author phase greenlit.**

The slice ships genuine 1.0 infrastructure (not speculative abstraction): four
named future consumers within 6 weeks (Stranded Chance Events adapter GDD +
three remaining beacon handlers — Merchant, Chopshop, Rest — inherit the same
`INodeEncounterHandler` + `BeaconOutcome` contract). The three-lens self-audit
passes on all lenses.

## Reshape summary (from initial RESHAPE verdict → full-scope target)

The initial (2026-07-27 morning) verdict on the parked Stranded Chance Events
GDD returned RESHAPE. That reshape triggered a strategic reorder: build the
Node Encounter Event Handler as the base infra first, then re-author Stranded
Events as a ~200-line adapter reusing the shared vocabulary. The parked GDD
capture at
`production/polish-captures/2026-07-27-stranded-chance-events-gdd-shape.md`
holds the reorder rationale.

After the reorder + Darkest-Dungeon-2 UI reference lock (user provided screenshot),
the target shape expanded to full-scope infrastructure per user directive
("id prefer to do the event system fully rather than a quick fix"):

| Reshape lever | Direction | Reason |
|---|---|---|
| `HostileTiltDelta` | Move from OUT → IN | Buildable now — `SlotInstance.DamageState` already exists per-slot; no new Frame subsystem seam required |
| `EventPayloadDefinitionSO` alone | Split into 2 SO families | Presentation and mechanics don't overlap — separating them lets `DialogueSceneController` stay content-blind and be reused across future dialogue-shaped nodes |
| `DialogueSceneSO` + `DialogueChoiceSO` | New — presentation vocabulary | Illustration ref + panel copy + numbered choices; shared across Event, future Merchant flavor, Chopshop offers, Stranded adapter |
| `DialogueSceneController` | Content-blind, zero refs to `IScrapEconomy`/`BeaconType` | Enables reuse; enforced by grep gate (AC-EV13) |
| `LootContextTag` | Data-flag lagging dep field on `DialogueChoiceSO` | Memory `feedback_data_flag_lagging_dependency` — L&R doesn't consume yet, carrier ships so future consumer swap is data-only |
| `PendingEventOffer` | Mirror `PendingCardOffer` on `RunState` | Convert payload needs player-input latching; same non-persistence treatment as `PendingCardOffer` this slice |
| Amber palette bake | New `--ember-*` register in `tokens.colors.uss` | Design tokens propagation for DD2-reference aesthetic; Haven register-warm precedent |
| `IScrapEconomy` verb count | 6 verbs (4 mutators + 2 preview helpers) | GDD §C.1 contract compliance; 2+ consumers (Event + downstream Stranded) satisfies the "verb signatures aren't load-bearing" memory rule |
| Ambush chain | Reuse existing Combat handler with `EncounterType = Ambush` | Node Encounter §C.2.5 Ambush sub-rule; Card Combat already accepts flag; no new code |

## Files at risk (this slice)

### New runtime code
- `Assets/Scripts/Run/NodeEncounter/INodeEncounterHandler.cs`
- `Assets/Scripts/Run/NodeEncounter/BeaconOutcome.cs`
- `Assets/Scripts/Run/NodeEncounter/EventHandler.cs`
- `Assets/Scripts/Run/NodeEncounter/HostileTiltDelta.cs`
- `Assets/Scripts/Run/NodeEncounter/IEventOfferResolver.cs` — injected mediator for Convert payload (Phase 2 amendment)
- `Assets/Scripts/Run/NodeEncounter/ICombatDispatcher.cs` — injected mediator for Ambush chain into existing Combat AdditiveScene (Phase 2 amendment)
- `Assets/Scripts/Run/NodeEncounter/IEventPayloadData.cs` — POCO contract mirroring the `IPartData` pattern so `EventHandler` (POCO) can consume `EventPayloadDefinitionSO` (Unity-bound) without importing the Authoring assembly (Phase 2 amendment)
- `Assets/Scripts/Run/PendingEventOffer.cs`

### New authoring / data
- `Assets/Scripts/Run/Authoring/EventPayloadDefinitionSO.cs`
- `Assets/Scripts/Run/Authoring/DialogueSceneSO.cs`
- `Assets/Scripts/Run/Authoring/DialogueChoiceSO.cs`
- `Assets/Scripts/Run/Authoring/LootContextTag.cs`

### New content assets
- `Assets/Resources/Run/Events/Biome1/Wreck.asset`
- `Assets/Resources/Run/Events/Biome1/Scavenger.asset`
- `Assets/Resources/Run/Events/Biome1/Ambush.asset`
- `Assets/Resources/Run/Events/Biome1/Cache.asset`

### New view / UI
- `Assets/Prefabs/BeaconRoots/EventRoot.prefab`
- `Assets/Prefabs/UI/DialogueSceneRoot.prefab`
- `Assets/Scripts/UI/DialogueSceneController.cs`
- `Assets/Scripts/CombatView/EventModalHost.cs`
- `Assets/UI/DialogueScene.uxml`
- `Assets/UI/DialogueScene.uss`

### New tests
- `Assets/Tests/EditMode/Run/NodeEncounter/EventHandler_Roll_Test.cs`
- `Assets/Tests/EditMode/Run/NodeEncounter/EventHandler_HostileTilt_Test.cs`
- `Assets/Tests/EditMode/Run/NodeEncounter/EventHandler_Ambush_Chain_Test.cs`
- `Assets/Tests/EditMode/Run/ScrapEconomy_Convert_Test.cs`
- `Assets/Tests/EditMode/Run/RunController_EventOffer_Test.cs`

### Modified
- `Assets/Scripts/Run/IScrapEconomy.cs` — 6 verbs (xmldoc + method additions)
- `Assets/Scripts/Run/ScrapEconomy.cs` — implementations
- `Assets/Scripts/Run/RunState.cs` — `PendingEventOffer` field
- `Assets/Scripts/Run/RunController.cs` — `ResolveEventOffer` + `SkipEventOffer` verbs
- `Assets/Scripts/CombatView/RunSceneHost.cs` — Event fan-out (mirrors Rest pattern)
- `Assets/Scripts/CombatView/BeaconActivator.cs` — EventRoot registration
- `Assets/Data/BeaconScenes/BeaconSceneBinding.asset` — Event entry
- `Assets/Editor/CombatPrefabAuthor.cs` — EventRoot authoring path
- `Assets/UI/tokens.colors.uss` — `--ember-*` amber palette

## ADRs at risk

| ADR | Risk | Mitigation |
|---|---|---|
| **ADR-0002** — No `UnityEvent` in combat systems | Modal wiring temptation | `DialogueSceneController` uses `System.Action` + `event` per ADR-0002 §3; grep gate |
| **ADR-0003** — Deterministic RNG | Payload roll seed | Use `new System.Random(runSeed ^ nodeIndex)` per §C.2.5 verbatim; single rng instance per handler invocation (§C.1 invariant 1) |
| **ADR-0004** — Save & Persistence | `PendingEventOffer` persistence temptation | Non-persisted this slice (matches `PendingCardOffer` today); documented in quick-spec §4 deferrals |
| **ADR-0011** — No bridges at done | Bimodal Convert paths / stub payloads | `IScrapEconomy` verbs additive (no bimodal); all 4 payloads authored + tested; no `TODO` / stub returns |
| **ADR-0013** — Composition over adapters | Reward-source pattern | EventHandler talks to `IScrapEconomy` directly, no adapter layer; payload dispatch is direct method call, not adapter chain |
| **ADR-0014** — UI Toolkit primary | Modal implementation | `DialogueScene.uxml` + `DialogueScene.uss`; `DialogueSceneController` C# controller; no `UnityEvent` per ADR-0002 port-forward |
| **ADR-0015** — Configuration narrowing | `BeaconType == Event` branches | `BeaconActivator` dispatch stays on `BeaconLoadMode` per Option B hybrid (2026-06-28); grep gate AC-EV12 |

## Final-game picture

`BeaconType.Event = 6` is currently live in the biome-1 generator emitting
Event beacons that fire no content when visited (dead pixels). Post-slice:

- Event beacons resolve into a full four-payload family (Windfall / Convert /
  Ambush / Treasure) with amber-palette Darkest-Dungeon-2-style dialogue
  modal presentation
- `HostileTiltDelta` makes Frame damage state legible at the encounter level
  ("your beat-up Frame attracts trouble") — a promised §C.2.5 tension mechanic
  ships day-one
- Node Encounter GDD moves from paper to 1-of-7 handlers shipped; remaining
  handlers inherit the contract
- `DialogueSceneController` becomes reusable UI infra for Merchant flavor,
  Chopshop offers, Stranded events, future story beats
- Amber palette register available for Haven / Rest / narrative surfaces
- `TryConvert*` verbs unlock any future economic-tension mechanic
- Stranded Chance Events GDD un-parks as ~200-line adapter

## Three-lens self-audit

**Lens 1 — Codebase Health:**
- Zero bridges (ADR-0011 clean): all four payloads authored; `IScrapEconomy` verbs additive; no bimodal branches; content-blind `DialogueSceneController` grep-gated
- Prior-art claim honest: `RestRoot.prefab` is a real precedent for the Option B PrefabRoot topology; `BeaconActivator` already dispatches on `BeaconLoadMode`; `PendingCardOffer` is the shape for `PendingEventOffer`
- No consumer count violations: `TryConvert*` has 2+ consumers within 6 weeks (EventHandler + Stranded adapter); `DialogueSceneController` has 4+ future consumers (Event / Merchant / Chopshop / Stranded)
- `LootContextTag` is data-flag lagging dep per memory `feedback_data_flag_lagging_dependency` — carrier field only, zero consumer branches this slice

**Lens 2 — Optimization:**
- Per-payload roll is not a hot path (fires once per beacon commit)
- `HostileTiltDelta` samples `Vehicle.GetSlotsByKind(SlotKind.Frame)` — existing O(k) call with k = Frame slot count (typically 4–6); no allocation on the roll path
- UI modal presentation uses UI Toolkit (ADR-0014) — GPU-efficient
- No frame-loop hot path touched

**Lens 3 — 1.0 Survival:**
- Delivers standalone player value: dead Event beacons become full payload family
- Unblocks 4 named future consumers within 6 weeks
- Amber palette bake is 1.0-shape correct (DesignTokens propagation register)
- No 5-slice cool-off freezes violated (Generator SO Surface Freeze from 2026-07-07 has expired 10 slices ago; not touching those files anyway)
- Save schema unchanged (matches `PendingCardOffer` non-persistence)

## Phased implementation (6 phases, ONE slice — internally phased)

1. **Foundation** — `INodeEncounterHandler`, `BeaconOutcome`, `IScrapEconomy` verbs, `PendingEventOffer`, `RunState` field, `RunController` verbs
2. **Handler + tilt** — `EventHandler`, `HostileTiltDelta`, `EventPayloadDefinitionSO` + tests (determinism, distribution, tilt)
3. **Content SOs** — `DialogueSceneSO`, `DialogueChoiceSO`, `LootContextTag`, 4 biome-1 event assets
4. **UI infra** — `DialogueSceneRoot.prefab`, `DialogueSceneController`, `DialogueScene.uxml/uss`, amber palette bake
5. **Wiring** — `EventRoot.prefab`, `EventModalHost`, `BeaconActivator` + `BeaconSceneBinding` + `CombatPrefabAuthor` + `RunSceneHost` fan-out
6. **Grep gates + acceptance** — verify AC-EV12/13/14/15 grep-cleanliness; run automated + manual AC-EV1..EV11; update session-state

## Follow-ups (not in this slice)

- **TD P1 fixes** (from 2026-07-27 health+opt audit — do before or during Event slice):
  - `NodeMap.ForwardEdgesFrom` `new List<int>()` per-call → precompute cached view
  - `IsStrandedForFuel` re-derives `PreviewSpend` per edge per frame → cache affordability bool, invalidate on `Advance/CreditFuel/Spend`
  - `StormAdvanceVisualPacer.Update` missing `_host` null-check → add guard
- **After Event slice ships:** re-author Stranded Chance Events GDD as adapter (parked capture references it)
- **BiomeDistributionSO → BiomeEventPoolsSO split** when Stranded event pool lands (TD P2 finding — new sibling SO, not another field on distribution)
- **Loot & Reward integration** for Treasure payload: swap 12-scrap stub → real L&R reward-offer flow keyed by `LootContextTag`
- **`TryRepair(freeRepair:true)`** retrofit lands with Rest handler slice

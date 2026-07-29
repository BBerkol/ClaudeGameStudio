# Model Layer Audit — 2026-07-29

Scope: Run/, Combat/ POCO, Persistence, IScrapEconomy + peers.

## Executive Summary
_[Fill in last based on findings below.]_

## P1 — Must fix before next major feature slice

- `Run/NodeMap.cs:199-205` — `ForwardEdgesFrom` allocates a fresh `List<int>` per call (P1 anchor confirmed still-present)
  - Why it matters: optimization. Called from `RunSession.IsStrandedForFuel`, `RunController.ForwardEdgesFrom`, `RunSceneOverlayHost` chip-affordability paint (view). At 60fps every hover/click/repaint on the map allocates and returns a fresh list. The xmldoc even flags "callers iterating frequently should cache" — that admission is the smell.
  - Fix shape: precompute per-node forward-edge `int[]` arrays once at ctor + on `Advance` (edges are immutable per-run), expose as `IReadOnlyList<int>`. Add a matching backward-edge cache when `AllowBidirectional=true` if callers ever need it. Zero allocation per call.

- `Run/RunSession.cs:437-457` — `IsStrandedForFuel` re-derives `PreviewSpend` per edge every call, with no invalidation seam (P1 anchor confirmed still-present)
  - Why it matters: optimization + 1.0-survival. Called from the pacer's wall-clock timer AND from every map-overlay repaint (per feedback trail). Each call: `ForwardEdgesFrom` (allocates), iterate frontier, `PreviewSpend` per dest (ceil + max), read `Fuel.Current`. Answer only changes on `Advance`/`CreditFuel`/`Spend`/`RefillPartial` — pure derived state.
  - Fix shape: cache boolean `IsStranded` on session, invalidate on the four fuel/beacon mutation seams (all currently on `RunSession.Advance` + reward paths). Recompute lazily on read after invalidation. Zero cost on unchanged frames.

- `Run/FuelState.cs:97-108` — `ComputeDrain` xmldoc says `max(1, ...)` and "single pip" but code enforces `max(2, ...)`
  - Why it matters: health. Doc drift — the doc contradicts the code and misleads future readers about the floor semantic. `Spend` xmldoc (line 47) also says "max(1, ceil(baseCost × chassisMultiplier))" — same drift in two places.
  - Fix shape: update both xmldocs to `max(2, ...)`. Confirm the "2" is the intended floor (project memory `project_storm_counter_sticker_drain_timer` locks sticker=drain=timer, suggesting 2 is intentional).


## P2 — Should fix within next 3 slices

- `Combat/Vehicle.cs:59-84,216-241` — Four computed getters (`MaxArmor`, `CurrentArmor`, `StructuralHp`, `StructuralMaxHp`) each iterate slot dictionaries on every read
  - Why it matters: optimization. Each getter walks either `_byKind[SlotKind.Armor]` (armor pair) or `_byId.Values` (structural pair) enumerating slot instances and summing fields. Callers include `MainBarWidget` / `HUD` widgets that poll `Player.StructuralHp` and `Player.CurrentArmor` on every frame during combat. Answer only changes on damage / repair / plate / install / restore — pure derived state. At 60fps with 2 vehicles × 4 getters that is ~480 dictionary enumerations per second where the underlying data is stable across most frames.
  - Fix shape: cache the four totals on `Vehicle` as private ints; invalidate (recompute) on the four mutation seams (`SlotInstance.SetHp` / `SetMaxHp` via a change callback, `InstallPart`, `RecomputeArmorPool`, `RestoreSlotState`, `PlateArmor`, `RepairSlot`). Getters become O(1) field reads. Or (lighter-touch): stash a `_dirty` bool that recomputes lazily on next read after mutation.
  - Risk: mutation seams currently thread through `SlotInstance` directly (Vehicle sets Hp via `slot.SetHp` in `PlateArmor`/`RepairSlot`, and `SetMaxHp` in `RecomputeArmorPool`). Would need a change-callback from SlotInstance up to Vehicle, or centralize slot mutations behind Vehicle-owned entrypoints. Reversible.

- `Run/NodeEncounter/EventHandler.cs:71-76` — Per-invocation state stored as instance fields, re-entrance risk if same handler instance is reused
  - Why it matters: health + 1.0-survival. `_pendingCallback`, `_pendingBeacon`, `_pendingRunSeed`, `_pendingEconomy`, `_pendingFuel` are all instance fields set at `Begin()` and read from presenter closures. The xmldoc acknowledges "if the same handler is re-invoked before its previous callback fires, the resolved-guard on the prior invocation still holds (callback closures reference the guard from their invocation)" — but `_pendingBeacon` etc. are NOT closure-captured, they're re-read from `this` when the closure fires. A second `Begin()` before the first modal closes would clobber `_pendingBeacon` under the first modal's still-open closure. Today `EventModalHost` builds a fresh `EventHandler` per invocation so this is dormant, but the shape invites future footgun.
  - Fix shape: either (a) enforce single-shot construction (throw on second `Begin`), or (b) capture all pending state in the closure at `Dispatch*` sites (via local variables) so no instance field is read from the callback. Option (b) is the 1.0-survival shape — makes the handler stateless-per-invocation and safe to reuse.

- `Run/NodeEncounter/EventHandler.cs:74` — `_pendingRunSeed` field is stored at `Begin` but only read from the Ambush closure (line 343)
  - Why it matters: health (dead-ish field). Field is stored on every `Begin` for every payload kind, but only Ambush's `DispatchAmbush` reads it — Windfall / Treasure / Convert never touch it. The seed WAS already consumed at line 146 (`new System.Random(runSeed ^ beacon.Index)`) for the payload roll. Ambush needs it a second time to hand off to combat. Not truly dead, but the field name suggests broader use than it has.
  - Fix shape: rename to `_pendingCombatSeed` or drop the field entirely and pass `runSeed` as a captured local into `DispatchAmbush`'s closure only. Pairs cleanly with the re-entrance fix above.

- `Run/RunState.cs` (via PendingCardOffer/PendingEventOffer non-persistence) — Mid-encounter Alt+F4 loses the offer, re-rolls on reload
  - Why it matters: 1.0-survival. `PendingCardOffer` and `PendingEventOffer` are POCO fields on `RunState` with no matching DTO. If the player Alt+F4s while a card-choice modal or event-modal is open, on reload the offer is gone — the game resumes without it. For card offers this is a mild frustration (player loses a choice they earned). For event offers the situation is worse: the beacon was resolved (or partially resolved) before the modal opened depending on the handler; a re-load might re-fire the event, or worse, silently drop it.
  - Fix shape: two options. (a) Persist both offers as new group-of-one DTOs (`PendingCardOfferDto`, `PendingEventOfferDto`) — ADR-0004 fully solves it. Cost: two more DTOs + adapters. (b) Block save-flush while any modal is open + snapshot at the presenter's Present/Resolve seams. Cheaper, but couples the save orchestrator to view lifecycle. Prefer (a) for 1.0 shape.
  - Note: today `EventModalHost` and `CardRewardPicker` are the only mid-run modals; verify no other pending-state fields have snuck in without persistence.

## P3 — Opportunistic / nice-to-have

- `Run/StormState.cs` — `PreviewAdvanceCounter` and `PeekNextCounter` intentionally duplicate the drain-math arithmetic in slightly different projections
  - Why it matters: ADR-0011 duplication watch. Two methods on the same class do overlapping arithmetic on the counter. Today acceptable — two callers, two projections (strip count vs next-value). Under ADR-0011's "share before the third caller lands" rule, extract a private helper the moment a third preview verb shows up (or a fourth changes the drain formula).
  - Fix shape: watch only, don't refactor today. Flag in the storm's file header if a third method is added.

- `Combat/CombatLoop.cs:598` — `ResolveEnemyIntent` allocates a fresh `List<IntentEffectOutcome>` per enemy turn
  - Why it matters: optimization (mild). One list per enemy turn is bounded — combat runs a handful of turns, enemy has 1-4 effects per intent. Total allocations per combat are trivial (~10-40). Only worth touching if the profiler ever surfaces it. Do not preallocate — the list is returned to the caller through `EnemyTurnResult.FromOutcomes`, so pool reuse would need lifetime discipline.
  - Fix shape: none today.

- `Combat/CombatLoop.cs:790,813` — `_hand.Insert(0, pulled)` is O(N) per insert
  - Why it matters: optimization (dormant). Hand size is bounded to `DefaultHandSize=5`, occasional Draw effects push to ~8. O(5) inserts per draw is a rounding error. Only becomes a smell if hand size ever exceeds ~20.
  - Fix shape: none today.

- `Run/BiomeWebGenerator.cs` — Multiple `List<>` / `HashSet<>` allocations per generation attempt loop
  - Why it matters: only invoked at run-start (once per new run), never in a per-frame path. Total allocation cost is ~one KB per run start. Not worth optimizing.
  - Fix shape: none.

- `Combat/Vehicle.cs:199-210` — `GetDamagedSlots` allocates a new `List<SlotInstance>` per call
  - Why it matters: optimization (mild). Called by Rest picker on modal open, not per-frame. Cost per call is trivial. Only worth touching if the profiler ever surfaces it.
  - Fix shape: none today.

- `Run/NodeEncounter/EventHandler.cs:157-188` — `RollKind` walks the weight table TWICE (once to compute total for the invariant check, once for cumulative)
  - Why it matters: optimization (mild). Table is 4 entries, invoked once per Event beacon (~5 per run). Trivial cost. The invariant assertion is a correctness net worth keeping.
  - Fix shape: none — the second pass exists to fail-fast on authoring bugs.

## Non-findings — audited and clean

- **Save layer (all DTOs + adapters + SaveSystem)** — Impressive ADR-0004 discipline. Every DTO has `SYSTEM_ID` + `SCHEMA_VERSION` consts; atomic temp-then-rename write path with 5-retry exponential backoff; per-category recovery chain (`.sav` → `.tmp` → `.bak`); partial-skip on per-DTO schema mismatch; `[ThreadStatic]` `IncrementalHash` pool for checksums; `DateParseHandling.None` to preserve checksum-round-trip for ISO timestamps. `CardEffectConverter` hand-coded dispatch (not reflection) per Slice 8d TD verdict. `SaveSystem.Write.cs` correctly guards Bind against double-call, uses `TaskCreationOptions.LongRunning` for the consumer, and coalesces intents. `SaveSystem.Load.cs` correctly gates schema-version at both envelope and per-entry levels. Clean.
- **`Run/RunController.cs`** — Façade over `RunState`; all mutations funnel through `RequireStarted` guard. New Event-slice methods (`ResolveEventOffer`, `SkipEventOffer`) fit cleanly. Salt constants (`0x4D41`, `0x4341`) match ADR-0003.
- **`Run/RunState.cs`** — Clean POCO holder. Exception: pending-offer fields flagged in P2 above.
- **`Run/StormState.cs`** — Clean spatial-cursor model. Duplication of arithmetic between preview verbs flagged in P3 (watch only).
- **`Run/RunDeck.cs`** — Clean POCO. Well-documented ADR-0013 discipline; sensible `AddCard` gate.
- **`Run/ScrapEconomy.cs` + `Run/IScrapEconomy.cs`** — Clean caller-agnostic verbs; convert verbs take `FuelState` at call time (ADR-0002 engine-free discipline). Non-negative-amount guards throughout.
- **`Run/PendingEventOffer.cs`** — Clean POCO record, `Direction`/`MaxInput`/`Rate`. Persistence flagged in P2 above.
- **`Run/NodeMap.cs`** — Structurally clean; only P1 finding is the `ForwardEdgesFrom` allocation.
- **`Run/NodeEncounter/*` interfaces (9 files)** — Clean one-verb POCO shape, well-documented ADR-0002/0011 discipline. All good gates.
- **`Combat/CombatLoop.cs`** — Solid turn orchestrator. Ambush pre-turn resolution, engine-offline DOT ticks, event ordering (RemoveAt before Resolve for correct draw indices) all documented and correct. `IndexOfInstance` uses `ReferenceEquals` explicitly per identity-binding rule.
- **`Combat/DamagePipeline.cs`** — Clean, no per-call allocations, well-documented event order (F-VP2 → R_ARM).
- **`Combat/Deck.cs`** — In-place Fisher-Yates, clean.
- **`Combat/Vehicle.cs`** — Solid ADR-0007/0012 shape (excluding P2 getter-recompute finding). `RestoreSlotState` correctly does NOT recompute armor pool (respects snapshot's depleted-buffer state).
- **Determinism** — Grep for `UnityEngine.Random` in `Assets/Scripts/{Run,Combat,Save}` returns zero hits (view-layer hits ignored). ADR-0003 discipline holds across the model layer.
- **ADR-0011 (no-bridges)** — No bridges / adapter layers / bimodal paths / transitional comments detected in the model layer. Doc comments consistently explain "why this shape" instead of "TODO remove after X".

## Cross-cutting recommendations

1. **Derived-state caching pattern.** Both P1 findings (`ForwardEdgesFrom`, `IsStrandedForFuel`) and one P2 finding (Vehicle computed getters) share the same shape: pure derived state is recomputed on every read, but only changes on a small set of mutation seams. Adopt a project-wide convention: **when a getter is called from a per-frame path AND its inputs are stable across most frames, cache the answer and invalidate on named mutation seams.** Codify it as a note in `technical-preferences.md` or a new "derived-state pattern" ADR. This pattern will recur — every future HUD widget bar that polls a computed property risks the same smell.

2. **Doc-code drift audit.** The `FuelState.ComputeDrain` finding (xmldoc says `max(1)`, code enforces `max(2)`) is a small instance of a broader risk: xmldocs across the model layer are dense and rules-heavy (which is good for onboarding) but drift from code as reversals land (e.g. the 2026-07-26 storm-counter-parity reversal). Recommend a one-time sweep before 1.0 — grep xmldoc for numeric literals and formulas, cross-check against code. Automate as a CI check if the pattern justifies it.

3. **Pending-offer persistence gap.** ADR-0004 has excellent coverage for run/deck/vehicle/fuel/storm/nodemap state, but `PendingCardOffer` and `PendingEventOffer` are load-bearing fields that don't have DTOs. This is a 1.0-survival concern: mid-modal Alt+F4 is a common player action. Either persist them or block save-flush while a modal is open. Prefer the former for ADR-0004 shape consistency.

4. **EventHandler re-entrance shape.** The `EventHandler` class stores per-invocation state on instance fields, which is fine today (fresh handler per invocation) but fragile. Two fixes: (a) capture pending state in closures at Dispatch* sites, or (b) throw on second `Begin` before Resolve. Pick one before more encounter handlers are authored to the same shape — else the pattern replicates.

5. **Watch: StormState arithmetic duplication.** `PreviewAdvanceCounter` + `PeekNextCounter` are the ADR-0011 "two-caller acceptable, three-caller extract" threshold. Add a check to the file header comment: "any new preview verb here MUST extract a shared drain helper first."

6. **Save layer as reference.** The Save layer's discipline (dotted-snake SystemIds, self-describing schema versions, atomic writes with checksum validation, per-DTO partial-skip, group-of-one vs resume-atomic membership) is exemplary. When future systems need persistence, the pattern is well-established and low-cost to extend. No changes recommended.

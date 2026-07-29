# Polish Capture: Merchant PrefabRoot Beacon Slice

**Date:** 2026-07-29
**System:** Merchant beacon (PrefabRoot per Option-B hybrid, ADR-0015) with card purchase + scrap↔fuel convert + save-persistent per-beacon visit history

**Affected paths (new files unless marked existing):**

Framework (design docs):
- `design/quick-specs/merchant-beacon-slice.md` (may be added; slice is scoped from `design/gdd/scrap-economy.md` §5 + `production/audits/2026-07-04-1.0-punch-list.md` P3.5β)

Unity project (`C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\`):
- `Assets/Scripts/Run/CardRarity.cs`
- `Assets/Scripts/Run/MerchantOfferEntry.cs`
- `Assets/Scripts/Run/MerchantOffer.cs`
- `Assets/Scripts/Run/MerchantVisit.cs`
- `Assets/Scripts/Run/MerchantOfferGenerator.cs`
- `Assets/Scripts/Run/RunState.cs` *(existing — add `MerchantVisits` non-null Dictionary, `PendingMerchantOffer`, `HasPendingMerchantVisit`)*
- `Assets/Scripts/Run/RunController.cs` *(existing — add `MerchantOfferSeedMix`, `CommitMerchantPurchase`, `ResolveMerchant`)*
- `Assets/Scripts/Run/RunSession.cs` *(existing — add `HasPendingEventOffer` + `HasPendingMerchantVisit` properties, guards in `Advance()` + `AutoAdvanceStrandedStorm()`)*
- `Assets/Scripts/Save/Adapters/MerchantVisitsSerializable.cs`
- `Assets/Scripts/Save/MerchantVisitEntry.cs`
- `Assets/Scripts/CombatView/LoadedRunSnapshot.cs` *(refactor payload struct)*
- `Assets/Scripts/CombatView/SaveBootstrap.cs` *(existing — build `LoadedRunSnapshot` once + register merchant adapter)*
- `Assets/Scripts/CombatView/RunSceneHost.cs` *(existing — `Initialize(LoadedRunSnapshot)` signature refactor + `NotifyMerchantResolved()` fan-out)*
- `Assets/Scripts/CombatView/MerchantSceneController.cs`
- `Assets/Scripts/CombatView/MerchantSceneHost.cs`
- `Assets/UI/Merchant/MerchantScreen.uxml`
- `Assets/UI/Merchant/MerchantScreen.uss`
- `Assets/Prefabs/BeaconRoots/MerchantRoot.prefab`
- `Assets/Resources/Run/Biomes/Biome1Distribution.asset` *(existing — Merchant weight 0 → 15; Combat 55 → 45, Rest 20 → 15, Event unchanged 25)*
- Tests: `MerchantOfferGenerator_Test.cs`, `RunController_MerchantPurchase_Test.cs`, `MerchantVisitsSerializable_Test.cs`, `MerchantSceneRootPrefabAuthoring_Test.cs`, `RunSession_Advance_Gates_Test.cs`, `RunSession_AutoAdvanceStrandedStorm_Gates_Test.cs`, `SaveBootstrap_LoadedRunSnapshot_Test.cs`

## Proposed change

Add the Merchant beacon (Biome1 pool) as a PrefabRoot per Option-B hybrid. Player enters → modal with 3 seeded card offers (all Common today; Uncommon/Rare land with parts axis) + scrap↔fuel convert. Buy deducts scrap via `IScrapEconomy.TrySpend`, adds card to `RunDeck`, marks sold bit on per-beacon `MerchantVisit` (readonly struct with `SoldMask` bitfield). Leave resolves the beacon with pre-`MarkResolved` snapshot (crash-window seam matching Rest). Storm auto-advance gated while modal open.

Companion refactor (in-slice): retire `RunSceneHost.Initialize` 9-arg positional signature → `readonly struct LoadedRunSnapshot` payload. Pays back on parts axis + Chopshop + boss adapters. Two-agent convergence in pass 6 flagged this as real code-health smell — this slice would make it worse without the refactor.

Companion fix (in-slice): `RunSession.Advance` + `AutoAdvanceStrandedStorm` add both `HasPendingEventOffer` (existing gap AP19) and `HasPendingMerchantVisit` guards. Six new guard checks total across the two methods — inseparable from the merchant work.

## Final-game picture this serves

- **Meta shape**: Merchant is 1 of 6 non-terminal beacon types shipping at 1.0 (P3.5β in `production/audits/2026-07-04-1.0-punch-list.md`). This is beacon-type slice #3 following Rest (2026-07-24) and Event (2026-07-28) — establishes the PrefabRoot precedent for Chopshop/Haven/EliteCombat/Boss follow-ons.
- **Parts axis (2026-07-04)**: `MerchantOfferEntry` as readonly struct with `CardRarity` field means `MerchantPartOffer` extends as sibling type when parts-axis lands. `OfferSeed` recorded on `MerchantOffer` mirrors `CardOffer.OfferSeed` for future pity/re-roll.
- **1.0 save infra**: `LoadedRunSnapshot` payload struct eliminates positional-args growth. Every future save adapter (parts inventory, chopshop history, boss defeat state) drops in as a struct field without seam churn.
- **1.0 guard shape**: The six-guard pattern (`HasPendingCardOffer` + `HasPendingEventOffer` + `HasPendingMerchantVisit` across `Advance` + `AutoAdvanceStrandedStorm`) is canonical — future pending-offer types (Chopshop, EliteReward) drop in as guard #7 without seam refactoring.
- **Storm as clock**: `HasPendingMerchantVisit` gate preserves the storm-as-clock design (`project_storm_counter_sticker_drain_timer`) — auto-advance never eats fuel while a modal-paced choice is open to the player.

## Authored values being destroyed

Nothing destroyed — this is a pure additive slice with one in-slice refactor. The refactor (`RunSceneHost.Initialize` 9-arg → `LoadedRunSnapshot`) preserves all values at the receiving side; the parameter shape changes but per-field application stays identical.

| Where | Value | Current | Replacement plan |
|---|---|---|---|
| `Biome1Distribution.asset` | Merchant weight | 0 (unreachable dead code) | 15 (Combat=45, Event=25, Rest=15, Merchant=15) |
| `RunSceneHost.Initialize` | 9-arg positional | `(runState, nodeMap, runSeed, runDeck, vehicleState, fuelState, stormState, pendingCardOffer, pendingEventOffer)` | Single `LoadedRunSnapshot` struct param carrying same 9 fields + `MerchantVisits` |
| `RunSession.Advance` | 1 guard | `HasPendingCardOffer` only | + `HasPendingEventOffer` + `HasPendingMerchantVisit` |
| `RunSession.AutoAdvanceStrandedStorm` | 1 guard | `HasPendingCardOffer` only | + `HasPendingEventOffer` + `HasPendingMerchantVisit` |

## Amendment history — 7 audit passes

Passes 1-4 (initial + 3rd TD pass): 10 BLOCKERS + 10 MODIFY folded.
Pass 5 (TD + debugger): 3 BLOCKERS (F1/AP15 SaveCategory infra; F2 vocabulary drift; AP14 null-safety) + 2 MODIFY (AP18 AdvanceReason grep; AP20 salt collision) + 1 COMPANION (AP19 Event guard).
Pass 6 (TD + debugger): 3 BLOCKERS (TD-A/AP22 converged Initialize signature; AP21 AutoAdvanceStrandedStorm gap) + 1 MODIFY (TD-B readonly struct rename) + 1 ADVISORY (TD-C snapshot ordering).
Pass 7 (TD + debugger): TD APPROVE with 2 non-blocking micro-notes (dictionary capacity hint — skip; verify empty-visits deserialize as non-null empty Dict — covered by test). Debugger completed verifications, no new findings surfaced.

**Total folded: 16 BLOCKERS + 13 MODIFY + 1 ADVISORY across 6 substantive passes.**

## Technical Director Review

**Verdict:** APPROVE
**Spawned at:** 2026-07-29 (7th pass — 4 prior TD passes precede this verdict)
**Agent transcript:**

> [TD-MERCHANT-P7]: APPROVE — SHIP AS FULLY AMENDED — 7TH PASS CLEAN (with two micro-notes, non-blocking)
>
> **Lens 1 — Codebase Health**
> - ADR-0011 clean: `CardRarity` omits `Legendary` (no vestigial values), `MerchantVisit` is canonical name (no `State` suffix drift), no bridges/parallel storage. Confirmed.
> - Subscription lifecycle: `MerchantSceneController` OnEnable/OnDisable for local + Bind/OnDestroy for external matches `feedback_uitoolkit_subscription_lifecycle`. Confirmed.
> - Snapshot ordering per AMEND-4 correctly distinguishes atomic-persistence (purchase) from crash-window-seam (Leave). XML doc fix prevents the exact confusion that triggered pass 6. Confirmed.
> - `LoadedRunSnapshot` as `readonly struct` collapses the 9-arg positional footgun and gives future slices a growth surface without signature churn (payload-struct pattern from three-lens rule). Confirmed.
> - Teardown: `MerchantVisit.WithSold` is a value-type return, no mutation races. Confirmed.
>
> **Lens 2 — Optimization**
> - `SoldMask` bitfield over Dictionary/HashSet: zero-alloc updates, cache-friendly serialization (single int). Correct choice for max-3 entries.
> - `MerchantVisitsSerializable` sorted-ascending write is O(n log n) on a max-of-visited-merchants collection — trivial. Confirmed.
> - Six guards in `Advance`/`AutoAdvanceStrandedStorm` are boolean short-circuits, no allocation. Confirmed.
> - No per-frame concerns — this is beacon-transition-cadence code.
>
> **Lens 3 — 1.0-Shape Survival**
> - `MerchantOfferEntry` as `readonly struct` with `CardRarity` field means the parts-axis expansion (per `project_parts_axis_in_1_0`) can extend to `MerchantPartOffer` as a sibling type without reshaping this one. Confirmed.
> - `OfferSeed` recorded on `MerchantOffer` mirrors `CardOffer.OfferSeed` for future pity/re-roll systems. Confirmed.
> - `MerchantOfferSeedMix = 0x4D52` ('MR') follows salt convention (`0x4341` 'CA' for cards). Confirmed.
> - Six-guard pattern is the canonical 1.0 shape — any future pending-offer type (Chopshop, EliteReward) drops in as guard #7 without refactoring the seam.
>
> **Two Non-Blocking Micro-Notes**
> 1. `MerchantVisits` Dictionary allocation on every fresh run: `new Dictionary<int, MerchantVisit>()` in ctor is fine; consider `Capacity: 4` hint if profiler ever flags it (won't). Skip.
> 2. AMEND-1 nullable check semantics: empty-visits case must serialize as non-null empty list (not null) to preserve "run started, no merchants visited yet" state distinctly from "resumed from pre-Merchant save." Test `MerchantVisitsSerializable_Test.cs` "empty→[]" covers this; confirm deserialize returns non-null empty dict.
>
> Ship it.

**TD reasoning summary:**
- Codebase health lens: all ADR-0011 disciplines held (no vestigial enum values, no `State`-suffix drift, no bridges); subscription lifecycle matches feedback rule; snapshot-ordering correction fixes the pass-6 confusion cleanly.
- Optimization lens: SoldMask bitfield + boolean guards + sorted-write on tiny collections — no per-frame concerns; beacon-transition cadence only.
- 1.0-survival lens: LoadedRunSnapshot pays back on parts axis + Chopshop + boss adapters; six-guard pattern is canonical shape for all future pending-offer types; MerchantOfferEntry structure extends to MerchantPartOffer as sibling without reshape.

## Debugger Adversarial Review (qa-lead 7th pass)

**Verdict:** No new blockers surfaced. Verifications on AMEND-1 (LoadedRunSnapshot refactor breaks no existing tests — RunSceneHost.Initialize call sites confined to SaveBootstrap), AMEND-2 (Event-offer guard doesn't break existing tests — no test currently expects Advance to succeed mid-event; fixture arrays already positionally cover Merchant at BeaconType ordinal 3), AMEND-3 (value-type-in-Dictionary semantics correct: `TryGetValue` returns defensive copy, `WithSold` computes on copy, assignment writes back), AMEND-4 (Rest pattern verified). Six prior debugger probes AP21-AP22 remained the last real findings; pass 7 checked their fixes and closed the loop.

## User approval

- Reviewed: 2026-07-29
- Approved by: bertanberkol
- Notes: User committed to shipping the fully amended spec after 7-pass audit. Findings from passes 1-6 all folded; pass 7 clean modulo two non-blocking micro-notes (both handled by planned test coverage).

## Implementation order

1. POCOs — `CardRarity.cs`, `MerchantOfferEntry.cs`, `MerchantOffer.cs`, `MerchantVisit.cs`
2. `RunState` mutations — non-null `MerchantVisits` Dict init in ctor; `PendingMerchantOffer` + `HasPendingMerchantVisit`
3. `RunController` verbs — `MerchantOfferSeedMix`, `CommitMerchantPurchase`, `ResolveMerchant` (per AMEND-4 ordering)
4. `RunSession` guards — `HasPendingEventOffer` + `HasPendingMerchantVisit` properties + 4 new guards across `Advance` + `AutoAdvanceStrandedStorm`
5. Save stack — `MerchantVisitEntry` DTO + `MerchantVisitsSerializable` + register via `RegisterRunStateSerializable`
6. `LoadedRunSnapshot` struct refactor — new file + `SaveBootstrap.LoadAndInitialize` builds struct + `RunSceneHost.Initialize(LoadedRunSnapshot)` signature swap; classify Merchant as standalone group-of-one; apply on BOTH resume + fresh-start branches
7. `MerchantOfferGenerator` — seeded, 3 non-dupe from `MilestoneRewardPools.Milestone1()`
8. Prefab + UI stack — `MerchantRoot.prefab` (sibling PrefabRoot), `MerchantScreen.uxml/.uss`, `MerchantSceneController` (lifecycle discipline), `MerchantSceneHost` (fan-out mirror of RestSceneHost)
9. Biome distribution edit — `Biome1Distribution.asset` weights rebalance
10. Test suite — 7 test files per amended spec
11. AP18 grep verify — `AdvanceReason.` in `Assets/Scripts/CombatView` (expected: zero switch consumers → skip enum extension)
12. AP20 salt verify — `0x4D52` collision check in NodeMap.cs + BiomeWebGenerator.cs (expected: clean per pass 5+6)

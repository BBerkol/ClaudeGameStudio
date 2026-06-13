# Polish Capture: Slice 5b — Reward-Stash Retirement (& Reward Pool Canonical Cut)

**Date:** 2026-06-13
**System:** Run-loop / Slice 5b — retires the transient 5a `_rewardCards`
bridge on `CombatController`, rewires `CardRewardPicker` to consume
`RunState.PendingCardOffer` via `RunSession.AcceptCardChoice` /
`SkipCardChoice`, and retires the now-vestigial `RewardPoolSO` chain in
the same atomic commit. Single ADR-0011 retirement, **strict no-op
mechanical content** — Heavy Burst keeps `1E / 5dmg`, Field Patch keeps
`1E`, Sustained Burn description restores "splash on Degraded" hint.

**Affected paths (Unity repo, uncommitted at time of capture):**

### Source — modified

- `Assets/Scripts/CombatView/CombatController.cs` — drops `_rewardCards`
  field, `AddCardToDeck`, `ClearRewardStash`, `RewardCards`, the
  `ResetCombat` sync loop (L251-252), `_rewardPoolAsset` SerializeField,
  `DefaultRewardPoolResourcePath` const, `RewardPool` accessor, the
  Resources auto-load for the reward pool, and the 5a→5b contract
  paragraph at L26-35 (the bridge it describes will no longer exist).
  Victory-branch `ResetCombat` gains a one-line `Debug.Log` noting the
  `SingleCombat` graph terminates at Haven (TD Lock C, NOT a `TODO`).
- `Assets/Scripts/CombatView/CardRewardPicker.cs` — drops inline
  `RewardPool[]` static factory array (8 entries, L36-54), drops
  `_controller.RewardPool` SO accessor path (L300-304), drops inline
  fallback shuffle (L306-321). `RollOffers()` now reads
  `_host.State.PendingCardOffer.Choices` and binds the first
  `OffersToShow` entries. `OnOfferPicked(card)` calls
  `_host.Session.AcceptCardChoice(index)`; `OnSkip()` calls
  `_host.Session.SkipCardChoice()`. `Bind` signature changes from
  `(CombatController, CombatOutcomeOverlay)` to
  `(RunSceneHost, CombatController, CombatOutcomeOverlay)` so the picker
  reaches `Session` directly without bouncing through the controller.
- `Assets/Scripts/CombatView/CombatOutcomeOverlay.cs` — drops the
  `_controller.ClearRewardStash()` call in the defeat branch (L279); on
  defeat-then-reset, `BeginNewRun` already replaces `RunState` wholesale,
  no separate clear is needed.
- `Assets/Scripts/CombatView/CombatHud.cs` — updates the picker
  `Bind(...)` call site to pass the resolved `RunSceneHost` reference
  alongside `CombatController` and `CombatOutcomeOverlay`.
- `Assets/Scripts/Run/FlatCardRewardSource.cs` — `Milestone1RewardPool()`
  body becomes one-line delegation to
  `MilestoneRewardPools.Milestone1()`; `OfferSize` const goes `2 → 3` so
  the canonical source matches the picker's `OffersToShow = 3`.
- `Assets/Editor/CombatDataInitializer.cs` — drops the Reward-Pool block
  (L67-81), drops the 5 reward-exclusive `cardLookup` entries
  (`heavyBurst`, `napalmStick`, `heavyPlate`, `fieldPatch`, `bigDraw` at
  L61-65), drops the `PoolsRoot` const + `EnsureFolder("Pools")` call,
  drops the `CreateOrLoadPool` helper (L152-153).
- `Assets/Editor/CanonicalCardData.cs` — drops the 5 "Reward-pool
  exclusives" entries (L64-74), re-points the L11-19 docstring at
  `MilestoneRewardPools.Milestone1()` (the "Same data as ... +
  CardRewardPicker.RewardPool — kept identical" line becomes truthful
  again after the bake).

### Source — new

- `Assets/Scripts/Run/MilestoneRewardPools.cs` (+ `.meta`) — static
  factory home for the 8-entry Milestone-1 reward pool. Same shape as
  `RunDeck.Milestone1Starter()`. Single grep target for Milestone-2/3
  pool additions.
- `Assets/Tests/EditMode/Run/MilestoneRewardPools_Test.cs` (+ `.meta`)
  — Lock D guard tests: `Milestone1_Has_Eight_Entries`,
  `Milestone1_Contains_Handbrake_And_Overtake_Clones`,
  `Milestone1_HeavyBurst_Costs_1E_Damages_5`,
  `Milestone1_FieldPatch_Costs_1E_Heals_8`,
  `Milestone1_SustainedBurn_DescriptionMentionsSplashOnDegraded`.

### Source — deleted

- `Assets/Scripts/CombatView/Data/RewardPoolSO.cs` (+ `.meta`,
  GUID `86f91902733b8c84db734cf989092872`)

### Assets — deleted

- `Assets/Resources/combat/Pools/RewardPool_Default.asset` (+ `.meta`,
  asset GUID `711fe69a638d47e4893800ee89fcb88f`)
- `Assets/Resources/combat/Cards/Card_HeavyBurst.asset` (+ `.meta`,
  asset GUID `3d0e2efcd2b479349b9481b9aaf43b4a`)
- `Assets/Resources/combat/Cards/Card_NapalmStick.asset` (+ `.meta`,
  asset GUID `f7c667474e9b1ee4796a92a239bced60`)
- `Assets/Resources/combat/Cards/Card_HeavyPlate.asset` (+ `.meta`,
  asset GUID `40c8d4b038975a4499b13331eaceb91f`)
- `Assets/Resources/combat/Cards/Card_FieldPatch.asset` (+ `.meta`,
  asset GUID `2ddd1e41dca0d9541965bdf5ed282a44`)
- `Assets/Resources/combat/Cards/Card_BigDraw.asset` (+ `.meta`,
  asset GUID `1a4dfa7a018def04f8bf9ea0113af14f`)
- `Assets/Resources/combat/Pools/` folder (+ `.meta`) — empty after
  `RewardPool_Default.asset` deletion; deleted to match the same-day
  `Decks/` folder retirement under Slice 5a.

## Proposed change

Retire the 5a transient stash bridge wholesale in the same commit as the
reward-pool canonical cut. The bridge was authorized in Slice 5a
explicitly as a one-slice carrier (`CombatController.cs:30-35`) so the
card-reward flow could keep working while the `RunSession.ExitCombat`
→ `PendingCardOffer` plumbing landed first. With that plumbing now live,
the bridge becomes the kind of "parallel storage" ADR-0011 forbids
(forbidden pattern #1: bridge window between old carrier and canonical
sink) and a textbook stub-returns risk (forbidden pattern #6 — picker
calls `AcceptCardChoice` against the canonical state, immediately
followed by a `BeginNewRun` that discards the result. The way around
that risk is **not** to keep the stash — it is to make the discard a
known terminal condition of the `SingleCombat` graph (Start → Combat →
Haven) and document it explicitly in code and capture).

The same commit retires `RewardPoolSO` + `RewardPool_Default.asset` + 5
reward-exclusive Card SOs. After picker rewiring, every one of those is
a vestigial-enum-of-one (ADR-0011 #4) — the SO has exactly one authored
instance, exactly one consumer (the picker, retiring), and no
per-instance tuning beyond the entry list. Same shape and reasoning as
the `StarterDeckSO` retirement approved 2026-06-13.

Per TD verdict (β + strict no-op + static factory): the 8-card P1
content (current player-facing reward pool) bakes verbatim into
`MilestoneRewardPools.Milestone1()`. **No mechanical retunes, no
description drops.** Handbrake and Overtake clones (deck-stacking via
reward) remain in pool; Heavy Burst stays `1E / 5dmg` (not
`Heavy MG Burst 2E / 6dmg`); Field Patch stays `1E` (not
`Field Repair 2E`); Sustained Burn description restores "splash on
Degraded" hint. The pre-5b P3 retunes that snuck in during
`FlatCardRewardSource` authoring revert. Only string renames without
tuning changes survive — those are cosmetic and already shipped as the
canonical Run-layer naming.

`OfferSize` on `FlatCardRewardSource` grows `2 → 3` so the canonical
source matches the picker's authored `OffersToShow = 3`. Shrinking the
picker is a UX regression; growing the source has no player-facing cost.

## Final-game picture this serves

ADR-0013's terminal shape: a single run grows a custom deck across many
encounters by accepting cards from per-combat `CardOffer`s. The deck
lives as a Run-layer POCO on `RunState.Deck`; offers are latched on
`RunState.PendingCardOffer` by `RunSession.ExitCombat` after victory,
resolved by `RunSession.AcceptCardChoice(int)` /
`RunSession.SkipCardChoice()`, and traversal between beacons is gated by
`RunSession.Advance(AdvanceReason)`. The node-map UI (Slice 6) will give
the player a map to walk; the per-encounter loop becomes
`Session.Advance → Session.EnterCombat → loop runs to Ended →
Session.ExitCombat → picker resolves the offer → Session.Advance`,
preserving `RunState.Deck` across the whole walk. At that terminal
shape there is no view-layer card stash, no SO-driven offer pool, and
no `_controller.RewardPool` accessor — the picker reads the same
`PendingCardOffer.Choices` that the canonical session populated.

**The wired-but-doesn't-persist condition** of Slice 5b → Slice 6: with
the current `NodeMap.SingleCombat` (Start → Combat → Haven), pressing
"Continue" after victory triggers `ResetCombat` which still calls
`BeginNewRun` (canonical reset, single code path). The
`AcceptCardChoice` call lands on the about-to-be-replaced `RunState`,
so the chosen card does not persist into the next combat. **This is
not a bridge** — `PendingCardOffer` is canonically consumed (Accept or
Skip clears it); no parallel storage exists; no stub-success path
returns. The deck loss is a product limitation of the single-combat
node graph, lifted in Slice 6 when `ResetCombat` is replaced by
`Session.Advance + Session.EnterCombat` against a multi-Combat
`NodeMap`. TD Lock C handles disclosure: commit message states this
explicitly, `active.md` STATUS block names it, `ResetCombat` victory
branch emits a one-line `Debug.Log` so QA sees it in player logs.

## Authored values being destroyed

### A. `CombatController` bridge surface (5 members + 1 sync loop + 4 SO-accessor members)

| Member | Line | Replacement |
|---|---|---|
| `_rewardCards` field | L75 | Gone; canonical sink is `_host.State.Deck` (already exists) |
| `AddCardToDeck(CardDefinition)` | L140 | `_host.Session.AcceptCardChoice(int)` |
| `ClearRewardStash()` | L150 | Gone; defeat reset path's `BeginNewRun` replaces `RunState` wholesale |
| `RewardCards { get; }` | L156 | Gone; no remaining consumer |
| `ResetCombat` sync loop | L251-252 | Gone; bridge target retired |
| `_rewardPoolAsset` field | L51 | Gone; canonical pool lives in `MilestoneRewardPools.Milestone1()` |
| `DefaultRewardPoolResourcePath` const | L57 | Gone; no Resources auto-load needed |
| `RewardPool { get; }` accessor | L163 | Gone; no remaining consumer (picker reads `PendingCardOffer.Choices`) |
| `AutoLoadDataAssets` reward-pool branch | L229 | Gone; one line, `_balanceAsset` line stays |

### B. `CardRewardPicker` bridge surface

| Member | Line | Replacement |
|---|---|---|
| Inline `static readonly Func<CardDefinition>[] RewardPool` array (8 entries) | L36-54 | Read `_host.State.PendingCardOffer.Choices` |
| `_controller.RewardPool` SO-accessor branch in `RollOffers` | L300-304 | Gone |
| Inline Fisher-Yates fallback in `RollOffers` | L306-321 | Gone (canonical offer is pre-rolled by `FlatCardRewardSource.Generate`) |
| `OnOfferPicked → _controller.AddCardToDeck` | L335 | `_host.Session.AcceptCardChoice(offerIndex)` |
| `OnSkip → CloseAndReset` (passive dismiss) | L339 | `_host.Session.SkipCardChoice()` then `CloseAndReset` |
| `Bind(CombatController, CombatOutcomeOverlay)` signature | L94 | `Bind(RunSceneHost, CombatController, CombatOutcomeOverlay)` — picker reads `Session` directly |
| `_controller` private field role | — | Stays — needed for `RequestReset` call on close (reset is a view-layer concern) |

### C. `CombatOutcomeOverlay` defeat-branch

| Member | Line | Replacement |
|---|---|---|
| `_controller.ClearRewardStash()` call | L279 | Gone; defeat-then-reset path's `BeginNewRun` replaces `RunState` wholesale, no separate clear needed |

### D. `RewardPoolSO` chain — vestigial-enum-of-one (ADR-0011 #4)

| Path | GUID | Notes |
|---|---|---|
| `Assets/Scripts/CombatView/Data/RewardPoolSO.cs` (+ `.meta`) | `86f91902733b8c84db734cf989092872` | Class deleted: `_pool` field, `Pool` accessor, `Roll(count, rng)` method, `SetPool` editor bootstrap. `[CreateAssetMenu]` attribute removed by deletion — no path to author a second pool via Inspector. |
| `Assets/Resources/combat/Pools/RewardPool_Default.asset` (+ `.meta`) | `711fe69a638d47e4893800ee89fcb88f` | Single authored instance. Inspector list of 8 `CardDefinitionSO` refs (5 reward-exclusives + 3 starter-clones). All content preserved in `MilestoneRewardPools.Milestone1()`. |
| `Assets/Resources/combat/Pools/` folder (+ `.meta`) | — | Empty after asset deletion; deleted to match same-day `Decks/` folder retirement under Slice 5a. |

### E. Reward-exclusive `CardDefinitionSO` assets (5 — only loaded by `RewardPool_Default.asset`)

Verified against `CanonicalCardData.cs:64-74` ("Reward-pool exclusives" section) and `RewardPool_Default.asset` `_pool[0..4]` GUIDs. None of these are referenced by `RunDeck.Milestone1Starter()` (the starter deck — Slice 5a verified) or by any prefab YAML / SO list outside `RewardPool_Default.asset`.

| Asset | GUID | Authored content (preserved in bake) |
|---|---|---|
| `Card_HeavyBurst.asset` | `3d0e2efcd2b479349b9481b9aaf43b4a` | "Heavy Burst", 1E, "Fire MG: 5 dmg.", `WeaponAttackEffect("weapon_0", 5)` |
| `Card_NapalmStick.asset` | `f7c667474e9b1ee4796a92a239bced60` | "Napalm Stick", 2E, "Fire Flame: 8 dmg, splash on Degraded.", `WeaponAttackEffect("weapon_1", 8)` |
| `Card_HeavyPlate.asset` | `40c8d4b038975a4499b13331eaceb91f` | "Heavy Plate", 1E, "+5 armor.", `PlateEffect(5)` |
| `Card_FieldPatch.asset` | `2ddd1e41dca0d9541965bdf5ed282a44` | "Field Patch", 1E, "Repair 8 HP to a damaged slot.", `RepairEffect(8)` |
| `Card_BigDraw.asset` | `1a4dfa7a018def04f8bf9ea0113af14f` | "Big Draw", 1E, "Draw 3 cards.", `DrawEffect(3)` |

### F. `CanonicalCardData.cs` "Reward-pool exclusives" (L64-74)

The 5 `Entry` records corresponding to the 5 SOs above. Deleted as a
group; the `CanonicalCardData.All` static array shrinks from 13 → 8
entries (the starter-deck 8 unique). Docstring at L11-19 re-points
from `CardRewardPicker.RewardPool` to
`MilestoneRewardPools.Milestone1()`.

### G. `CombatDataInitializer.cs` (L67-81 + helpers)

- Reward-Pool generation block (`CreateOrLoadPool("RewardPool_Default", ...)`)
- 5 `cardLookup` reward-exclusive variable declarations
  (`heavyBurst`, `napalmStick`, `heavyPlate`, `fieldPatch`, `bigDraw`)
- `PoolsRoot` const + `EnsureFolder(CombatRoot, "Pools")` line
- `CreateOrLoadPool` private static helper (L152-153)

## Content preservation table (TD Lock D — sign-off required before edit)

**This table is the contract.** Post-commit playtest must show the same 8 reward cards with the same name, cost, description, and effect as below. Each row is identical between the current player-facing pool (P1 inline in `CardRewardPicker`) and the new `MilestoneRewardPools.Milestone1()`. Strict no-op.

| # | Name | Cost | Description | Effect |
|---|---|---|---|---|
| 1 | Heavy Burst | 1E | Fire MG: 5 dmg. | `WeaponAttackEffect("weapon_0", damage: 5)` |
| 2 | Napalm Stick | 2E | Fire Flame: 8 dmg, splash on Degraded. | `WeaponAttackEffect("weapon_1", damage: 8)` |
| 3 | Heavy Plate | 1E | +5 armor. | `PlateEffect(armorGain: 5)` |
| 4 | Field Patch | 1E | Repair 8 HP to a damaged slot. | `RepairEffect(repairAmount: 8)` |
| 5 | Big Draw | 1E | Draw 3 cards. | `DrawEffect(count: 3)` |
| 6 | Handbrake | 2E | Reposition: drop Behind. | `RepositionToEffect(LanePosition.Behind)` |
| 7 | Overtake | 2E | Reposition: push Ahead. | `RepositionToEffect(LanePosition.Ahead)` |
| 8 | Flame Barrier | 1E | Buff: 50% reduction on next enemy attack. | `BuffEffect(BuffTag.FlameBarrier)` |

**Reverts from current `FlatCardRewardSource.Milestone1RewardPool()`** (which has `OfferSize = 2` and 6 entries):

| Change | From (P3) | To (strict no-op bake) | Reason |
|---|---|---|---|
| Rename + retune | `Heavy MG Burst 2E / 6dmg` | `Heavy Burst 1E / 5dmg` | Cost+1 / dmg+1 is a balance change masquerading as a refactor |
| Rename + retune | `Field Repair 2E` | `Field Patch 1E` | Cost+1 is a balance change masquerading as a refactor |
| Description drop | `"Fire Flame: 8 dmg."` (Sustained Burn) | `"Fire Flame: 8 dmg, splash on Degraded."` | UX bug — splash mechanic still triggers; player needs to know |
| Rename revert | `Sustained Burn` | `Napalm Stick` | Strict no-op against player-visible state (P1 inline / P2 SO) — picker bypasses P3 today via SO path, so the screen never showed "Sustained Burn". Lock D table is authoritative. |
| Rename revert | `Reinforce` | `Heavy Plate` | Same — Lock D row 3. |
| Rename revert | `Scout Pulse` | `Big Draw` | Same — Lock D row 5. |
| Pool expansion | 6 entries | 8 entries (add Handbrake, Overtake) | Restore deck-stacking reward option (P1 had it) |
| Display size | `OfferSize = 2` | `OfferSize = 3` | Match picker's `OffersToShow = 3` (shipped UX) |

> **Note on TD verdict 2 vs Lock D:** TD verdict 2 reasoned "renames-without-tuning acceptable in-slice" treating P3 as canonical. Lock D — the table the user signed — treats P1 (inline picker pool, which the player actually sees today via the SO path) as the contract. Lock D wins because the picker bypasses P3 entirely today (`_controller.RewardPool` SO path), so a true no-op against player-visible state restores the P1 names. The user-signed Lock D table is the binding contract.

## Adjacent risk (not destroyed, flagged for user)

- **`CardRewardPicker.prefab` YAML refs to `_cardPrefab` SerializeField**
  — stays wired; the picker still instantiates `Card.prefab` for each
  offer (visual layer is unchanged). No prefab edit required.
- **`Combat.prefab` references to `CombatController._rewardPoolAsset`
  and `RewardPool` accessor** — Inspector field on the prefab still
  references `RewardPool_Default.asset` via guid
  `711fe69a638d47e4893800ee89fcb88f`. On asset deletion, Unity's
  serializer drops the field on next prefab save. Non-blocking, will
  self-heal on next author pass — same pattern as Slice 5a's
  `_starterDeckAsset` orphan cleanup.
- **`CombatController.cs:30-35` 5a→5b contract paragraph** — describes a
  bridge that no longer exists post-5b. Delete the paragraph in the same
  commit. TD risk #5 (transitional comments — ADR-0011 #7).
- **`CombatHud.cs:600+` picker `Bind` call site** — picker `Bind`
  signature changes from `(controller, outcome)` to
  `(host, controller, outcome)`. `CombatHud` resolves `_host` the same
  way `CombatController` does (Inspector → `GetComponentInParent` →
  `FindAnyObjectByType`) so the picker's reach to `Session` doesn't
  require an extra public surface on `CombatController`. Caller-side
  change is one line.
- **`RunControlsWidget` debug-damage label** — reads
  `_controller.Balance.DebugDamageAmount`. Unrelated to reward flow;
  untouched.
- **`FlatCardRewardSource` `OfferSize = 2` test coverage** — verify
  whether existing `RunSession_CardReward_Test` asserts the 2-card shape;
  if so, those assertions update to `3` as part of the same edit.

## Technical Director Review

**Verdict 1 (Q1/Q2/Q3 scope):** RESHAPE (Q1=A reframed, Q2=A, Q3=canonical, single atomic commit)
**Verdict 2 (α/β/γ content drift):** RESHAPE (β + strict no-op + static factory + OfferSize=3)
**Spawned at:** 2026-06-13
**Agent:** technical-director

### TD Verdict 1 — Q1/Q2/Q3 scope (verbatim)

> **TD-CHANGE-IMPACT: RESHAPE**
>
> ## Rationale
>
> Your Q1/Q2/Q3 read is correct on the bridge surface. The reshape is on **Q1 framing**, not the choice itself.
>
> **Q1=A is correct, but not as "wired, awaiting traversal."** That framing is the half-system trap (`feedback_overall_picture_thinking`). A picker that calls `AcceptCardChoice` and then immediately gets its result trampled by `BeginNewRun` is not "in-progress traversal" — it is a **stub that returns success while doing nothing** (ADR-0011 #6: stub returns). One slice or ten slices, the user has been explicit: do not ship half-systems.
>
> **The reshape: make Q1=A honest by gating, not by waiting.** In 5b, `ResetCombat` after victory should call `Session.SkipCardChoice()` first (if a pending offer exists and the player chose to skip OR a continuation isn't implemented yet), then `BeginNewRun`. The picker's "Pick" path lands `AcceptCardChoice` and then ALSO falls through to `BeginNewRun` — but the commit message and `active.md` state the **known consequence**: "victory card choice does not persist across runs until Slice 6 wires `Session.Advance` + `EnterCombat`." That is not a bridge; it is a documented terminal condition for the current node graph (`SingleCombat` has nowhere to go after Haven).
>
> This is materially different from "wired, awaiting traversal" because the model state is consistent at every step — `PendingCardOffer` is consumed (Accept or Skip), `RunState` is reset cleanly, no parallel storage, no stub success. The deck loss is a **product limitation of the single-combat node graph**, not a code bridge.
>
> **Q2=A confirmed.** Retire `RewardPoolSO` in the same commit IF grep confirms zero other consumers. The `MilestoneRewardSource` is the canonical generator; a second SO-driven offer source is exactly ADR-0011 #1.
>
> **Q3 canonical confirmed.** Skip must be a model event. Passive UI dismiss leaves `PendingCardOffer` latched, which `Session.Advance` then blocks on — that is a latent bug, not a style preference.
>
> **Scope-split recommendation: single commit, no 5b-prime/double.** The three changes are one ADR-0011 retirement. Splitting them creates an intermediate state where either (a) offer source is canonical but landing is bridged, or (b) landing is canonical but offer source is bridged. Both are worse than the current 5a state. Land together.

### TD Verdict 2 — α/β/γ content drift (verbatim)

> **TD-ARCHITECTURE: RESHAPE**
>
> **Rationale (β with one correction):** You're right that α smuggles a balance regression under a refactor framing — Handbrake/Overtake stacking loss is real player-facing economy delta, not taste. ADR-0011 retirement of vestigial systems must be content-preserving by default; intentional content changes get their own slice with their own playtest gate. Bake P1/P2 content into the canonical Run-layer source, retire the SO chain wholesale, ship as a true no-op refactor. The Heavy Burst → Heavy MG Burst rename/retune and the Field Patch → Field Repair retune are also intentional-feeling design changes that snuck into P3 during canonical authoring — revert those to P1/P2 values in the bake. Renames-without-tuning (Reinforce, Scout Pulse, Sustained Burn) are acceptable in-slice since they're string-only and P3 already shipped them as the canonical names. The "Sustained Burn" description losing the "splash on Degraded" hint is a UX bug — restore it; the mechanic still triggers and the player needs to know.
>
> **Q2 — Pool home.** Separate static factory. `FlatCardRewardSource` is a *delivery mechanism* (implements `ICardRewardSource`); pool content is *data*. Put the bake in `Run/Rewards/MilestoneRewardPools.cs` as `public static IReadOnlyList<CardOffer> Milestone1()` and have `FlatCardRewardSource` read from it. This keeps the source class small, makes Milestone 2/3 pools obvious additions (one method each, same file), and gives you one grep target when balance changes land. Same shape as `RunDeck.Milestone1Starter()` precedent you already have.
>
> **Q3 — OfferSize.** Grow source to 3. Picker's `OffersToShow = 3` is the shipped player experience; shrinking to 2 is a UX regression. `OfferSize` on `FlatCardRewardSource` becomes 3 to match. If you want 2-offer pools later for tension/scarcity design, that's a Milestone 2+ knob, not a refactor side-effect.
>
> **Q4 — Designer tunability.** Acceptable to lose Inspector tuning for Milestone 1. My prior "single source of truth, tuning lives in code" verdict stands — you're a solo designer-coder, the round-trip cost of editing a static C# list vs. a SO Inspector is negligible, and the win is one grep target instead of "is the live value in the .asset or the .cs?". Caveat: when you onboard a second designer or hit Milestone 3 with 4+ pools, revisit.
>
> **Three locks unchanged from prior verdict, plus one addition:**
>
> - Lock A: single atomic commit (retire + bake + tests + SO deletes together)
> - Lock B: zero parallel offer sources after commit (grep gate on `RewardPoolSO` returns zero hits in `Assets/`)
> - Lock C: card-choice-doesn't-persist reality acknowledged in commit message ("Slice 6 wires `SelectedCardId` into `PendingDeckAdditions`")
> - **Lock D (new): content-preservation attestation in capture file** — user signs the 8-card P1↔bake side-by-side table before edit. This is the "we'll know it was right if" criterion: post-commit playtest of a Milestone 1 run shows the same 8 reward cards with the same costs/damage/heal values as today, and Handbrake/Overtake stacking still works.

### TD risk register (combined, both verdicts)

| # | Risk | Severity | Mitigation status (this slice) |
|---|---|---|---|
| 1 | `RewardPoolSO` has consumer outside picker | MED → LOW | Verification confirmed editor-only (Initializer creates; nothing else reads). Clean drop. |
| 2 | `BeginNewRun` after `AcceptCardChoice` silently discards accepted card → user perceives 5b as broken | HIGH | Three locks: (a) commit message states "card choice does not persist until Slice 6 — `SingleCombat` graph terminates at Haven post-victory"; (b) `active.md` STATUS block names the limitation; (c) one-line `Debug.Log` in `ResetCombat` victory branch noting the discard (NOT a `TODO` comment — ADR-0011 #7). |
| 3 | Inline `RewardPool[]` array has designer-tuned values | MED → ADDRESSED | Bake into `MilestoneRewardPools.Milestone1()` verbatim. 8-card side-by-side table above. User signs before edit. |
| 4 | `SkipCardChoice` semantics — does it clear `PendingCardOffer`? | MED → CLEAR | Verified: `RunController.cs:205-211` sets `_state.PendingCardOffer = null` directly. Clean. |
| 5 | Tests calling `AddCardToDeck` / `ClearRewardStash` / `RewardCards` | MED → CLEAR | Grep returned ZERO test consumers. Production-only retirement. |
| 6 | `CombatController.cs:30-35` 5a→5b contract comment becomes stale post-5b | LOW → ADDRESSED | Paragraph delete in same commit. ADR-0011 #7. |
| 7 | Bake fidelity drift (miscopy a damage value during 8-entry bake) | MED → MITIGATED | Side-by-side table above; `MilestoneRewardPools_Test` asserts count, presence-by-name, costs, healing, description hint. |
| 8 | Description string drift (e.g., lost Degraded hint) | LOW → ADDRESSED | Capture table includes `description` column verbatim; test asserts `Sustained Burn` description mentions "splash on Degraded". |
| 9 | 5 orphaned reward-exclusive Card SOs and 1 RewardPool asset | LOW → ADDRESSED | All listed in Section D + E with GUIDs; deleted atomically with SO class. Folder `Resources/combat/Pools/` deleted to match. |
| 10 | `CombatHud.cs` picker `Bind` call site stale after picker signature change | LOW → ADDRESSED | Single call-site update in same commit. |

### TD ruling on "Proceed to capture draft" gate

> **Proceed to capture draft with β + static factory home + OfferSize=3 + Heavy Burst/Field Patch reverted to P1 tuning + Sustained Burn description restored.** This is your call — if you want to ship the P3 retunes as intentional balance changes in this slice, say so and I'll re-verdict with that scope, but it needs to be explicit in the capture file under a "Balance Changes" heading separate from "Refactor."

**User decision on retune scope (recorded above invocation):** **(i) strict no-op refactor.** P3 retunes revert; renames-without-tuning kept; Sustained Burn description restored. No "Balance Changes" heading in this slice.

## User approval

- Reviewed: 2026-06-13
- Approved by: BertanBerkol
- Notes: Approved with Lock D signed — strict no-op refactor confirmed. Heavy Burst 1E/5, Field Patch 1E, Sustained Burn description restored, 8-entry pool, OfferSize=3. Renames kept (Sustained Burn, Reinforce, Scout Pulse). Single atomic commit per Lock A. Card-choice-doesn't-persist disclosure goes in commit message per Lock C.

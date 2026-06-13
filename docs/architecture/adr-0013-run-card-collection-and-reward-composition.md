# ADR-0013: Run-Scoped Card Collection + Reward-Source Composition

## Status

Accepted (2026-06-13)

## Date

2026-06-13

## Last Verified

2026-06-13

## Decision Makers

User (BertanBerkol), Claude (technical-director-equivalent session)

## Summary

Slice 4 of the Run Loop epic (Card-System epic kickoff) introduces the
run-scoped card collection (`RunDeck`) and the post-victory card-offer
pipeline. This ADR codifies four interlocking shape decisions that the
TD verdict locked on 2026-06-13:

1. **`RunDeck` is a new POCO in `WastelandRun.Run`** — not a reuse of
   `WastelandRun.Combat.Deck`. The Combat `Deck` models a single
   encounter's shuffle state (draw pile + discard + RNG); `RunDeck` models
   the player's evolving collection across encounters (no shuffle, no
   discard, no per-combat RNG). They share zero invariants and stay
   deliberately distinct types.
2. **`CombatReward` extends additively** to `(int Scrap, CardOffer Choices)`
   with a scrap-only convenience overload. Sealed positional record so future
   Milestone-2 extensions (loot bundles, lore drops) can land without
   breaking `IRewardSource` or `ICardRewardSource` signatures.
3. **Reward generation is composed at the session, not folded into a single
   `IRewardSource`.** Slice 3's `IRewardSource` rolls scrap; the new
   `ICardRewardSource` is a sibling interface that rolls card offers. The
   session calls both on player victory and assembles the dual-field
   `CombatReward`. Composing both via the same interface would force the
   scrap policy to carry pool/dedup state it doesn't want as M2 lands biome
   tables and pity logic.
4. **`Milestone1Starter` is a hardcoded factory on `RunDeck`**, mirroring the
   `NodeMap.Milestone1CombatArchetypes` pattern shipped in Slice 2. The
   prototype-waiver authority for the M1 deck shape is a single C# constant
   — ScriptableObject authoring lives in a later milestone.

Five cross-cutting structural locks accompany the four shape decisions:

- **`PendingCardOffer` lives on `RunState`, not on `RunSession`** — per
  ADR-0004's save-snapshot rule. A latched offer survives serialise/reload
  on a single source of truth.
- **The dupe-filter seam is at the source.** `ICardRewardSource.Generate`
  receives `IReadOnlyList<CardDefinition> currentDeck` so M2 implementations
  can apply dedup / pity logic without an interface change. M1's
  `FlatCardRewardSource` ignores it (trivially correct).
- **`CardOffer` records `OfferSeed`** — not just uses and discards it. M2
  pity counters and re-roll mechanics can derive secondary rolls off the
  same anchor without going back to `RunController.DeriveCardOfferSeed`
  at consumption time.
- **`RunController.DeriveCardOfferSeed`** adds salt `0x4341` (`'CA'` ASCII).
  Distinct from `CombatSeedMix` (`0x434F` `'CO'`), `RewardSeedMix`
  (`0x5257` `'RW'`), and the map salt (`0x4D41` `'MA'`). ADR-0003 formula:
  `RunSeed ^ stepIndex ^ 0x4341`. CI grep gate enforces salt uniqueness.
- **No bridges (ADR-0011).** No transitional helpers, no legacy
  `CombatReward.Scrap`-only path persisted past the slice, no parallel
  `Run.Deck` / `Combat.Deck` adapters. The shape shipped this slice is the
  1.0 shape.

## Engine Compatibility

| Field | Value |
|-------|-------|
| Unity Version | 6.3 LTS |
| Language | C# 9 |
| Affected Subsystems | `WastelandRun.Run` (RunDeck, CardOffer, ICardRewardSource, FlatCardRewardSource, CombatReward, RunState, RunController, RunSession), `WastelandRun.Combat` (StarterDecks retired), `WastelandRun.CombatView` (asmdef gains `WastelandRun.Run` ref) |

## Context

Slices 1–3 of the Run Loop epic shipped the run state spine
(RunController, RunState, NodeMap, ScrapEconomy), the inter-beacon driver
(RunSession + IEncounterBuilder), and the scrap reward seam (IRewardSource
+ FlatScrapRewardSource + CombatReward record). Slice 4 closes the loop by
landing the post-combat card pipeline: the player's deck grows across the
run, and each cleared Combat beacon offers two new cards drawn
deterministically from a Milestone-1 pool.

Four shape questions surfaced during Slice 4 planning, each with a sharp
answer once the TD review landed.

### Q1 — Where does the run-scoped deck live?

**Verdict: a new `RunDeck` POCO in `WastelandRun.Run`.**

Reusing `WastelandRun.Combat.Deck` would have collapsed two semantically
disjoint concerns into one type:

- `Combat.Deck` is a shuffle engine — it owns the draw pile, the discard
  pile, the per-encounter RNG state, and the reshuffle policy. Its
  invariants are encounter-scoped: a card moves from draw to hand to
  discard and back via shuffle.
- `RunDeck` is a collection — it owns the card list across the whole
  run. Its invariants are run-scoped: cards only get added (via
  `AddCard`); there is no draw, no discard, no shuffle, no RNG.

Forcing both into one type would require either teaching `Combat.Deck`
about a "reset to starter + extras" mode (cross-cutting state) or
teaching `RunDeck` consumers about draw/discard piles they don't use.
The dependency arrow (Run → Combat) means CombatLoop can keep
constructing its own `Combat.Deck` from a `List<CardDefinition>` handed
in by the view layer — no contamination.

### Q2 — How does `CombatReward` carry the card offer forward?

**Verdict: additive extension** —
`CombatReward(int Scrap, CardOffer Choices)`.

`CombatReward` is the runtime carrier from `IRewardSource` and the new
`ICardRewardSource` back to the session caller. Slice 3 deliberately
sealed the record with a positional constructor so Slice 4 could add the
`Choices` field without breaking the `IRewardSource.Generate` signature
or any consumer's pattern-match. `Choices` is nullable rather than a
sentinel-empty offer because the boss / final-beacon path
(`design/gdd/loot-reward.md`) substitutes flat 30 scrap for the card
offer, and a null carrier reads cleaner than a synthesised empty offer in
both UI and persistence.

A scrap-only convenience constructor (`new CombatReward(scrap)`) calls
into the dual-field overload with `Choices: null`, so existing scrap-pure
consumers (boss path, test fixtures) read naturally.

### Q3 — Does scrap and card generation share an interface?

**Verdict: compose — sibling `ICardRewardSource` seam.**

Considered alternatives:

- **(a) Sibling seam (accepted):** `IRewardSource` rolls scrap;
  `ICardRewardSource` rolls a `CardOffer`. The session calls both and
  assembles the dual-field `CombatReward`.
- (b) Fold into one: `IRewardSource.Generate` returns the full
  `CombatReward`. Rejected — couples scrap policy to card-pool state.
  M2 will introduce biome-routed card pools and pity counters; forcing
  the scrap policy to carry that ride-along makes both harder to evolve.

The compose-at-the-session pattern keeps each policy single-axis.
`FlatScrapRewardSource` ships flat-10; `FlatCardRewardSource` ships
deterministic-2-from-6. Either side can swap implementations without
touching the other.

### Q4 — Where does the Milestone-1 starter deck live?

**Verdict: hardcoded factory on `RunDeck.Milestone1Starter()`.**

Mirrors the `NodeMap.Milestone1CombatArchetypes` shape from Slice 2 — a
single C# constant in the canonical Run-layer assembly is the M1
authority. ScriptableObject authoring is a later milestone (the
`StarterDeckSO` path exists for the editor canonical-cards
initialisation, but the runtime fallback now reads
`RunDeck.Milestone1Starter()` instead of the retired
`StarterDecks.MakeBasic()`).

## Cross-Cutting Locks

### `PendingCardOffer` on `RunState`

When `RunSession.ExitCombat` lands a player-victory `CardOffer`, it both
returns it on `CombatReward.Choices` and latches it on
`RunState.PendingCardOffer`. The latch — not the in-memory session field
— is the source of truth. Per ADR-0004, save snapshots serialise
RunState; a mid-walk offer survives reload.

`RunSession.Advance` is gated: while `PendingCardOffer != null` (or
while a combat is in flight), Advance throws. The player resolves the
offer via `AcceptCardChoice(int)` or `SkipCardChoice()`, which adds the
chosen card (or doesn't) and clears the latch. Then Advance proceeds.

### Dupe-filter at the source seam

`ICardRewardSource.Generate(BeaconData beacon, int offerSeed,
IReadOnlyList<CardDefinition> currentDeck)`. The third argument is the
live `RunState.Deck.Cards` view, not a copy. M2 implementations can
apply pity (offering an unseen card if the player has 3+ duplicates of
the last roll), dedup (refusing to offer a card already in the deck), or
biome-routed pool filtering — all without an interface change.

`FlatCardRewardSource` accepts `currentDeck` but ignores it. The M1
contract is trivially correct: same `offerSeed` ⇒ same pair, regardless
of deck state.

### `CardOffer.OfferSeed` recorded, not consumed

`CardOffer` is `(IReadOnlyList<CardDefinition> Choices, int OfferSeed)`.
The seed that generated the offer rides forward on the record. M2 pity
counters that need to derive secondary rolls (e.g., "pity reroll uses
`OfferSeed ^ 1`") have the anchor without re-deriving from
`RunController.DeriveCardOfferSeed`.

### Card-offer salt: `0x4341` ('CA')

`RunController.CardOfferSeedMix = 0x4341`. ADR-0003 derivation:
`DeriveCardOfferSeed(stepIndex) => RunSeed ^ stepIndex ^ 0x4341`.

Salt table (all distinct):

| Salt | Hex | ASCII | Owner |
|------|-----|-------|-------|
| CombatSeedMix | `0x434F` | `'CO'` | RunController |
| RewardSeedMix | `0x5257` | `'RW'` | RunController |
| CardOfferSeedMix | `0x4341` | `'CA'` | RunController |
| Map salt | `0x4D41` | `'MA'` | NodeMap.MapSeed |

A combat-shuffle roll, a scrap-table roll, and a card-pool roll on the
same beacon never converge. The salt-uniqueness CI grep gate (added in
Slice 1) enforces this.

### No bridges (ADR-0011)

`StarterDecks.MakeBasic()` retired this slice. The combat-layer call
site is gone; the view-layer fallback (`CombatController` line ~274)
now reads `RunDeck.Milestone1Starter()`. The two
`StarterDeck_HandbrakeAndOvertake_CostTwoEnergy` /
`StarterDeck_IsThirteenCards` tests in `BuffAndDrawTests` migrated to
`RunDeck_Test` as `Milestone1Starter_*` cases. No deprecation period,
no parallel storage, no compat shim — the shape shipped this slice is
the 1.0 shape.

## Implementation Reference

Unity-side (Slice 4, 2026-06-13 on `BBerkol/Wasteland-Run`):

- `Assets/Scripts/Run/RunDeck.cs` (new) — POCO collection + `Milestone1Starter()`
- `Assets/Scripts/Run/CardOffer.cs` (new) — sealed record
- `Assets/Scripts/Run/ICardRewardSource.cs` (new) — seam interface
- `Assets/Scripts/Run/FlatCardRewardSource.cs` (new) — M1 default
- `Assets/Scripts/Run/CombatReward.cs` (extended) — `(int Scrap, CardOffer Choices)`
- `Assets/Scripts/Run/RunState.cs` (extended) — `Deck`, `PendingCardOffer`
- `Assets/Scripts/Run/RunController.cs` (extended) — `CardOfferSeedMix`, `DeriveCardOfferSeed`, `AcceptPendingCardChoice`, `SkipPendingCardChoice`
- `Assets/Scripts/Run/RunSession.cs` (refactored) — 4-arg ctor, dual-source latch on victory, `AcceptCardChoice` / `SkipCardChoice`
- `Assets/Scripts/Combat/StarterDecks.cs` (retired) — content migrated
- `Assets/Scripts/CombatView/CombatController.cs` — fallback reads `RunDeck.Milestone1Starter()`
- `Assets/Scripts/CombatView/WastelandRun.CombatView.asmdef` — adds `WastelandRun.Run`
- `Assets/Tests/EditMode/Run/RunDeck_Test.cs` (new) — 10 tests
- `Assets/Tests/EditMode/Run/RunSession_CardReward_Test.cs` (new) — 34 tests
- `Assets/Tests/EditMode/Run/RunSession_Test.cs` + `RunSession_Reward_Test.cs` (updated for 4-arg ctor + `SkipCardChoice` after victory loops)
- `Assets/Tests/EditMode/Combat/BuffAndDrawTests.cs` — two `StarterDeck_*` tests migrated to `RunDeck_Test.Milestone1Starter_*`
- `Assets/Tests/EditMode/Combat/DeckHandDiscardTests.cs` — one `StarterDecks.MakeBasic()` usage inlined as a 9-card distinct deck

EditMode test result: **442 total / 441 passed / 0 failed / 1 pre-existing skip.**

Capture file: `production/polish-captures/2026-06-13-adr-0013-run-card-collection-and-reward-composition.md`.

## Consequences

### Positive

- Card-pool policy and scrap-table policy can evolve on independent axes.
  M2 biome tables for scrap and M2 pity logic for cards don't have to
  share state or coordinate releases.
- `RunDeck` carries clean run-scoped invariants — no shuffle-state
  leakage, no encounter-scoped RNG. Save serialisation maps directly to
  the public surface (a `List<CardDefinition>` + a nullable
  `CardOffer`).
- The four ASCII salt constants make the salt table self-documenting in
  hex dumps. New salts have to land a CI gate alongside the constant.
- ADR-0011 no-bridges discipline holds — the slice ships the 1.0 shape
  with zero transitional helpers.

### Negative

- The 4-argument `RunSession` constructor grows with the system. A
  builder pattern may become attractive at slice 5 or 6 (when a
  `IModifierSource` or similar lands). Tracked but not built.
- `CardOffer` carrying `OfferSeed` reserves the anchor for M2 features
  that don't exist yet. ADR-0011 expressly allows this since the field
  IS shipped on day one — it's not a stub, it's a recorded fact.
- Both records (`CombatReward`, `CardOffer`) override every positional
  property to `{ get; }` to avoid Unity 6.3's missing
  `System.Runtime.CompilerServices.IsExternalInit`. This is a known
  Unity quirk, not an ADR-level decision — but it's why the records
  look slightly verbose.

### Risks Accepted

- M1 ships without dupe filtering. The Milestone-1 reward pool is six
  cards, drawing two distinct per offer; over a 3-combat run the player
  has at most 6 picks across the run, so duplicate offers are tolerable.
  M2 pity / dedup logic drops in at the `ICardRewardSource` seam.
- M1 card offers are skippable but not re-rollable. The intended M2
  feature (re-roll for scrap) requires the `OfferSeed` anchor that this
  ADR reserves but does not yet exercise.

## Supersedes / Amends

None. Builds on:

- ADR-0003 (deterministic RNG salting) — adds `CardOfferSeedMix`
- ADR-0004 (save & persistence) — `PendingCardOffer` on `RunState`
- ADR-0011 (no-bridges meta-rule) — first application to a brand-new
  cross-system carrier (`CombatReward.Choices` ships full, not stubbed)

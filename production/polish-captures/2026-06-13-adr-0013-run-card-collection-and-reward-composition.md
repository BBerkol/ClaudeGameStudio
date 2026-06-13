# Polish Capture: ADR-0013 Draft — Run-Scoped Card Collection + Reward-Source Composition

**Date:** 2026-06-13
**System:** Run-loop / Card-system epic kickoff — Slice 4 (RunDeck + CardOffer + ICardRewardSource + CombatReward.Choices additive + RunSession card-reward flow)
**Captures:** the four TD verdicts + five cross-cutting locks that already shipped this session as Unity-side code, before they are persisted as ADR-0013.

**Affected paths (this ADR write):**
- `docs/architecture/adr-0013-run-card-collection-and-reward-composition.md` — NEW Accepted ADR documenting the Slice 4 design verdicts the TD already locked on 2026-06-13.

**Affected paths (Slice 4 code already shipped on Unity repo `BBerkol/Wasteland-Run`, pre-commit at time of this capture):**
- `Assets/Scripts/Run/RunDeck.cs` — NEW POCO collection + `Milestone1Starter()` factory
- `Assets/Scripts/Run/CardOffer.cs` — NEW sealed record
- `Assets/Scripts/Run/ICardRewardSource.cs` — NEW seam
- `Assets/Scripts/Run/FlatCardRewardSource.cs` — NEW M1 default
- `Assets/Scripts/Run/CombatReward.cs` — extended to `(int Scrap, CardOffer Choices)` with scrap-only convenience ctor
- `Assets/Scripts/Run/RunState.cs` — gains `Deck` (ctor) + `PendingCardOffer` (internal set)
- `Assets/Scripts/Run/RunController.cs` — `CardOfferSeedMix = 0x4341`, `DeriveCardOfferSeed`, `AcceptPendingCardChoice`, `SkipPendingCardChoice`; `StartRun` wires the `RunDeck`
- `Assets/Scripts/Run/RunSession.cs` — 4-arg ctor (adds `ICardRewardSource`), dual-source latch on victory, `AcceptCardChoice` / `SkipCardChoice`, `HasPendingCardOffer`, `Advance` gated on pending offer
- `Assets/Scripts/Combat/StarterDecks.cs` — RETIRED
- `Assets/Scripts/CombatView/CombatController.cs` — fallback reads `RunDeck.Milestone1Starter()`
- `Assets/Scripts/CombatView/WastelandRun.CombatView.asmdef` — adds `WastelandRun.Run`
- `Assets/Editor/CombatDataInitializer.cs`, `Assets/Editor/CanonicalCardData.cs` — doc-comment refresh to point at `RunDeck.Milestone1Starter()` instead of the retired `StarterDecks.MakeBasic()`
- `Assets/Tests/EditMode/Run/RunDeck_Test.cs` — NEW (10 tests)
- `Assets/Tests/EditMode/Run/RunSession_CardReward_Test.cs` — NEW (34 tests)
- `Assets/Tests/EditMode/Run/RunSession_Test.cs` + `RunSession_Reward_Test.cs` — 4-arg ctor + `SkipCardChoice()` after victory loops
- `Assets/Tests/EditMode/Combat/BuffAndDrawTests.cs` — two `StarterDeck_*` tests removed (migrated to `RunDeck_Test`)
- `Assets/Tests/EditMode/Combat/DeckHandDiscardTests.cs` — one `StarterDecks.MakeBasic()` use inlined as a 9-card distinct deck

## Proposed change

Persist as ADR-0013 (Accepted, 2026-06-13) the four shape verdicts + five
cross-cutting locks that the TD reviewed and the user accepted before
Slice 4 code was authored. The code already exists and is green
(442/441/0/1); this capture exists to give the ADR write a recorded TD
verdict per the capture-before-destroy hook.

The ADR is **not destructive of authored values** in the conventional
sense — it documents shape decisions, not deletions of designer-tuned
numbers. The single authored-content removal it codifies
(`StarterDecks.MakeBasic()` retired in favour of
`RunDeck.Milestone1Starter()`) preserved the 13-card list verbatim;
nothing was numerically rebalanced.

## Final-game picture this serves

The run-loop epic's terminal shape: the player walks a node map, fights
deterministic combats, picks one of two new cards after each win, and
those picks compound into a custom deck that re-enters the next combat.
The four Q-decisions are the shape that scales without rework when M2
biome card pools and pity logic land:

- `RunDeck` ≠ `Combat.Deck` lets the shuffle engine stay encounter-local
  and the collection grow run-scoped.
- `CombatReward.(Scrap, Choices)` is additive so M2 loot bundles drop in
  without re-cutting the carrier.
- `IRewardSource` + sibling `ICardRewardSource` keeps scrap policy and
  card policy single-axis — neither blocks the other.
- `RunDeck.Milestone1Starter` mirrors `NodeMap.Milestone1CombatArchetypes`,
  so the M1 → M2 promotion path is "swap the constant for an SO," not
  "rebuild the loader."

## Authored values being destroyed

None. The 13-card Milestone-1 starter content migrated verbatim from
`StarterDecks.MakeBasic()` to `RunDeck.Milestone1Starter()`:

| Card | Count | Energy | Effect |
|---|---|---|---|
| BulletBarrage | 3 | 1 | WeaponAttack(weapon_0, 3 dmg) |
| FlameBurst | 2 | 2 | WeaponAttack(weapon_1, 6 dmg) |
| Weld | 3 | 1 | Plate(+3 armor) |
| Handbrake | 1 | 2 | RepositionTo(Behind) |
| Overtake | 1 | 2 | RepositionTo(Ahead) |
| Patch | 1 | 1 | Repair(5 HP) |
| Draw | 1 | 1 | Draw(2) |
| Flame Barrier | 1 | 1 | Buff(FlameBarrier) |

Six-card Milestone-1 reward pool (NEW content this slice — not a
destruction):

| Card | Energy | Effect |
|---|---|---|
| Heavy MG Burst | 2 | WeaponAttack(weapon_0, 6 dmg) |
| Sustained Burn | 2 | WeaponAttack(weapon_1, 8 dmg) |
| Reinforce | 1 | Plate(+5 armor) |
| Field Repair | 2 | Repair(8 HP) |
| Scout Pulse | 1 | Draw(3) |
| Flame Barrier | 1 | Buff(FlameBarrier) — duplicates a starter card by design (M1 has no dupe filter) |

Salt constants added / verified — all distinct, all `'CA'`/`'CO'`/`'RW'`/`'MA'`
ASCII-readable:

| Constant | Hex | Owner |
|---|---|---|
| `RunController.CombatSeedMix` | `0x434F` `'CO'` | Existing (Slice 2) |
| `RunController.RewardSeedMix` | `0x5257` `'RW'` | Existing (Slice 3) |
| `RunController.CardOfferSeedMix` | `0x4341` `'CA'` | NEW (Slice 4) |
| `NodeMap` map salt | `0x4D41` `'MA'` | Existing (Slice 1) |

## Technical Director Review

**Verdict: APPROVE** — accepted by user 2026-06-13 ("td opinion please yes" → review →
"accept verdicts. well do a commit run after"). All four shape questions answered
with the option that scales without rework when M2 biome card pools and pity
logic land. Five cross-cutting flags treated as locks on the implementation.

### Q1 — RunDeck location

**Verdict: (a) new POCO in `WastelandRun.Run`.** Reusing `Combat.Deck` would
collapse encounter-scoped shuffle state with run-scoped collection invariants.
`Combat.Deck` owns draw pile + discard + per-encounter RNG; `RunDeck` owns
collection growth across encounters with no shuffle, no discard, no RNG.
Forcing them into one type either pollutes `Combat.Deck` with a "reset to
starter + extras" mode or teaches `RunDeck` consumers about draw/discard piles
they don't use. Dependency arrow stays one-way (Run → Combat); `CombatLoop`
continues to construct its own `Combat.Deck` from a `List<CardDefinition>`
handed in by the view layer.

### Q2 — CombatReward shape

**Verdict: (a) additive extension** —
`CombatReward(int Scrap, CardOffer Choices)`. Slice 3 deliberately sealed
the record positionally to allow this additive growth without breaking
`IRewardSource.Generate`. `Choices` is nullable rather than a sentinel because
the boss / final-beacon path substitutes flat 30 scrap for the card offer per
`design/gdd/loot-reward.md`, and a null carrier reads cleaner than a synthesised
empty `CardOffer` in UI and persistence. Scrap-only convenience constructor
preserves clean reads on scrap-pure consumers and tests.

### Q3 — Reward generation composition

**Verdict: (a) sibling `ICardRewardSource` seam, NOT folded into `IRewardSource`.**
The session calls both on player victory and assembles the dual-field
`CombatReward`. Folding both into one interface forces the scrap policy to carry
card-pool state it doesn't want as M2 lands biome tables and pity logic.
Composing-at-the-session keeps each policy single-axis — `FlatScrapRewardSource`
ships flat-10, `FlatCardRewardSource` ships deterministic-2-from-6, either swaps
without touching the other.

### Q4 — Starter deck authoring

**Verdict: (c) hardcoded `RunDeck.Milestone1Starter()` factory.** Mirrors the
`NodeMap.Milestone1CombatArchetypes` shape from Slice 2. Single C# constant in
the canonical Run-layer assembly is the M1 authority. ScriptableObject authoring
is a later milestone (the editor `StarterDeckSO` path remains, but the runtime
fallback now reads `RunDeck.Milestone1Starter()` instead of the retired
`StarterDecks.MakeBasic()`).

### Cross-cutting lock #1 — `PendingCardOffer` on `RunState`, not `RunSession`

ADR-0004 save-snapshot rule: anything that must survive serialise/reload lives
on `RunState`. `RunSession` is in-memory orchestration; a mid-walk offer
between `ExitCombat` and `AcceptCardChoice` is exactly the kind of state save
must round-trip. Latching on `RunState.PendingCardOffer` (internal set, public
get) keeps the source of truth singular.

### Cross-cutting lock #2 — Dupe filter at the source seam

`ICardRewardSource.Generate(beacon, offerSeed, IReadOnlyList<CardDefinition>
currentDeck)`. Third argument is the live `RunState.Deck.Cards` view. M1
`FlatCardRewardSource` accepts and ignores it (trivially correct). M2
implementations can apply pity / dedup / biome-pool filtering without an
interface change.

### Cross-cutting lock #3 — `CardOffer.OfferSeed` recorded, not consumed

Record-as-data `(IReadOnlyList<CardDefinition> Choices, int OfferSeed)`. The
seed rides forward on the carrier so M2 pity counters can derive secondary
rolls (`OfferSeed ^ 1` for re-rolls, etc.) without going back to
`RunController.DeriveCardOfferSeed`. ADR-0011 expressly permits this since the
field IS shipped on day one — it's not a stub, it's a recorded fact.

### Cross-cutting lock #4 — Card-offer salt `0x4341` ('CA')

`RunController.CardOfferSeedMix = 0x4341`. ADR-0003 formula:
`DeriveCardOfferSeed(stepIndex) = RunSeed ^ stepIndex ^ 0x4341`. Distinct from
the three existing salts. A combat-shuffle roll, a scrap-table roll, and a
card-pool roll on the same beacon never converge by construction. Salt-uniqueness
CI grep gate (Slice 1) enforces this.

### Cross-cutting lock #5 — No bridges (ADR-0011) is non-negotiable

`StarterDecks.MakeBasic()` retired this slice (file deleted). View-layer
fallback reads `RunDeck.Milestone1Starter()`. Two starter-content tests in
`BuffAndDrawTests` migrated to `RunDeck_Test` as `Milestone1Starter_*` cases.
No deprecation period, no parallel storage, no compat shim. The shape shipped
this slice is the 1.0 shape.

### Unity 6.3 quirk note

Both records (`CombatReward`, `CardOffer`) override every positional property
to `{ get; }` to dodge Unity 6.3's missing
`System.Runtime.CompilerServices.IsExternalInit`. This is documented in the
ADR's "Negative" consequences — not an architecture decision, just a known
runtime constraint to record so future record additions don't trip the same
compile error.

### Test gate

Pre-write EditMode result: **442 total / 441 passed / 0 failed / 1 pre-existing
skip.** 44 new tests cover all four verdicts + five locks. Existing 398 Slice-3
green tests preserved; 2 Combat-layer starter-content tests migrated to
`RunDeck_Test` (same assertions, new home).

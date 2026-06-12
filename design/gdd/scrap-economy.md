# Scrap Economy System

> **Status**: In Design
> **Author**: user + game-designer + economy-designer + systems-designer
> **Last Updated**: 2026-04-22
> **Implements Pillar**: Pillar 4 (Scarcity with Agency — Scrap domain); Pillar 1 (Vehicle as Character — install cost scaling encodes part permanence; salvage refund authority now in vehicle-and-part-mechanics.md F-VPM2); indirect support for Pillar 3 (Read to Win).
> **Creative Director Review (CD-GDD-ALIGN)**: CONCERNS → REVISED 2026-04-22 (4 items: Section B re-voiced in wasteland register; D.6 Scrap refund gained tenure decay mechanic for Pillar 1 defense; Pillar 3 framing softened in Overview / H.4 / G.1d; I.5 Convert sub-screen + H.5/I.7 run summary gained named three-domain legibility).

## Overview

The Scrap Economy System is the authoritative owner of the **Scrap** resource domain — the run-scoped currency the player spends to build, repair, and edit their vehicle. It defines the run-state field (`CurrentScrap: int`), the transaction verbs that mutate it (`Install`, `Repair`, `Purge`, `Purchase`, `Scrap`, `Convert`), and the pricing formulas those verbs resolve. It is the **exclusive caller** of `IVehicleMutator.InstallPart` and `IVehicleMutator.RemovePart` for player-initiated actions: all install, replace, and scrap flows route through this system, whether triggered by a Chopshop, a Merchant offer, or a post-combat loot acceptance. Card-level economy (the `PurgeCost` and `MerchantPrice` fields defined on `CardDefinitionSO`) is resolved here: Card System provides the data, Scrap Economy performs the transaction.

The Scrap domain is one of three separate resource domains the game depends on (Scrap / Fuel / Energy — Pillar 4). This GDD enforces the **separation contract** declared by the Node Map GDD (AC-NM55): no shared pool or shared field exists between `CurrentScrap` and `CurrentFuel`, and any conversion between the two is performed only through named verbs (`ConvertScrapToFuel(int)`, `ConvertFuelToScrap(int)`) that log both sides. Without this system, the three-domain promise collapses into a single wallet and Pillar 4 fails by default. The system's player-facing surface is quiet but constant: every install is a Scrap decision, every repair is a Scrap decision, every Chopshop visit is a Scrap decision, and the running total the player carries is the cumulative ledger of every yes and every no since the run began — not the sole measure of a run's success (combat reads and route choices remain the skill layer, per Pillar 3), but the layer the player will return to between combats to take stock.

## Player Fantasy

Scrap is salvage — rust-stamped bolts and raw metal you pocket as the run takes you. You don't know exactly what you're holding; you know the weight of it. You know this many bolts gets you back on the road and that many doesn't, and every decision at a Chopshop is you dragging that weight against what the vehicle still needs. Forty-seven Scrap is enough for the install you've been planning, or for the repairs the last fight cost you, or for the purge you keep meaning to get to — never for all of it. The counter doesn't show what you have. It shows what's left after every yes and every no since the wasteland picked you up.

Fuel resets at stations. Energy resets each combat. Scrap only goes down, except when something dies and leaves pieces worth picking up. When the Rare pool runs dry and the pity counter hands you Scrap instead of a card, it lands like salvage should — not a reward, not a consolation, just another handful added to the pile before the pile gets smaller again. Every verb is named because spending is supposed to hurt a little. Every conversion is friction because a number this load-bearing can't be allowed to drift. You will count it between fights. You will recount it at the Chopshop. When the road goes quiet, you will pull the counter up just to confirm the number is still the number — the small ritual of a driver taking stock before the next long leg.

*Design anchor — Pillar 4 test satisfied by structure, not language: one pool + mutually exclusive spend verbs + always-visible running total = hesitation within the Scrap domain by construction. The hesitation is produced by the architecture (no shared wallet, named conversion friction) and the UI contract (running total always on-screen), not by rhetorical weight. If playtesters stop counting Scrap between decisions, this section has failed its design test regardless of how well it reads.*

## Detailed Design

### Core Rules

Scrap is a map-layer resource. It is mutated by six named verbs and read through a single facade. Combat code holds no reference to Scrap; this is a hard architectural boundary, not a runtime check.

#### The six verbs

| Verb | Available at | What it does | Cost source |
|------|--------------|--------------|-------------|
| **Install** | Chopshop (from offered parts); any node where the player accepts a post-combat part reward | Pays install cost → calls `IVehicleMutator.InstallPart(slot, part)`. If the target slot is occupied, auto-scraps the existing part as a single atomic step (no separate refund — see Scrap verb). | Formula (Section D) |
| **Repair** | Chopshop | Pays repair cost → calls `IVehicleMutator.Repair(slot, hp, canReviveOffline: true)`. Full restore only — no partial repairs. Any damage state (`Critical`, `Offline`) eligible at any Chopshop. Note: `Degraded` is a visual-only parameter in the vehicle damage model — it is not a mechanical `DamageState` (see vehicle-and-part-mechanics.md C.1). | Formula (Section D) |
| **Purge** | Chopshop | Pays `GlobalPurgeCost = 30` → removes one card from the run deck. Free-valve override sets cost to 0 for the first purge at a given Chopshop visit. | Flat constant |
| **Purchase** | Merchant | Pays `MerchantPrice(rarity)` → adds a card from the Merchant offer to the deck. | Formula (Section D) |
| **Scrap** | Chopshop | Removes an installed part (player-initiated standalone) → grants refund via `ScrapRefund(rarity)`. Frame slot may not be scrapped to Empty (V&P OQ-VP3). | Formula (Section D) |
| **Convert** | Chopshop **and** Merchant | Exchanges between Scrap and Fuel via `ConvertScrapToFuel(int)` / `ConvertFuelToScrap(int)`. Both sides logged (AC-NM55). Future: events may also call these verbs (forward note for Node Encounter GDD). | Formula (Section D) |

#### Non-negotiable rules

1. **No mid-combat Scrap transactions.** Combat UI must not show Scrap-affordance surfaces at all (hide, not disable). The Scrap counter remains visible read-only during combat as a status element.
2. **Deck floor at 10.** Purge is blocked when `DeckSize ≤ 10` (Card System EC7). Free-valve does not override this floor — a free purge that would drop the deck below 10 is still blocked.
3. **Frame slot may not be left Empty by player action.** The `Scrap` verb rejects the Frame slot when it would produce an Empty Frame. Install into Frame (which auto-scraps the existing Frame part) is valid because the slot is immediately re-occupied.
4. **All conversion is lossy, both directions.** See Section D for rates. UI must show the exact output value before the player confirms (no post-hoc surprises).
5. **Every Chopshop offers Install, Repair, Purge, Scrap, and Convert.** Inventory varies by node seed; the verb set does not. Merchants offer Purchase and Convert only.
6. **Free-valve is ~33% per Chopshop visit, seeded from `runSeed ^ nodeIndex`.** Deterministic across save reloads (not re-rolled). Applies to the first Purge only in that visit.
7. **All integer rounding applies against the player** (`Ceiling` on costs, `Floor` on refunds and conversion outputs) — consistent with V&P OQ-VP5.

#### `IScrapEconomy` facade

```csharp
// All methods map-layer only. Combat code must never reference this interface.
public interface IScrapEconomy
{
    // Read-side
    int  CurrentScrap         { get; }
    bool TransactionInFlight  { get; }  // true between Proposed and Logged phases

    // Spend verbs
    bool TryInstall     (IVehicleMutator v, IVehicleView vv, SlotType slot,
                         PartDefinitionSO part, string context, out TransactionResult r);
    bool TryRepair      (IVehicleMutator v, IVehicleView vv, SlotType slot,
                         string context, bool freeRepair, out TransactionResult r);
    bool TryPurge       (IDeckMutator d,    IDeckView dv,    CardDefinitionSO card,
                         bool freeValveActive, string context, out TransactionResult r);
    bool TryPurchase    (IDeckMutator d,    IMerchantOffer offer, CardDefinitionSO card,
                         string context, out TransactionResult r);
    bool TryScrapPart   (IVehicleMutator v, IVehicleView vv, SlotType slot,
                         string context, out TransactionResult r);

    // Convert verbs (AC-NM55 — only bridge between Scrap and Fuel domains)
    bool TryConvertScrapToFuel (IFuelSystem fs, int scrapAmount, string context,
                                out TransactionResult r);
    bool TryConvertFuelToScrap (IFuelSystem fs, int fuelAmount,  string context,
                                out TransactionResult r);

    // Grant (income — not a spend; no TransactionInFlight guard)
    void GrantScrap (int amount, string context);

    // Observable
    event Action<TransactionResult> OnTransactionCompleted;
}
```

```csharp
public readonly struct TransactionResult {
    public bool            Success    { get; }
    public TransactionVerb Verb       { get; }
    public FailureReason   Reason     { get; }   // None on success
    public int             ScrapDelta { get; }   // signed; 0 for free-valve purge
    public int             ScrapAfter { get; }
}

public enum FailureReason {
    None,
    InsufficientScrap,
    InvalidSlot,
    OverrideCollision,          // V&P OQ-VP2 — fail-closed
    FrameSlotWouldBeEmpty,      // V&P OQ-VP3
    PartNotInstalled,
    PartNotDamaged,
    ChassisIncompatible,
    SlotTypeMismatch,
    CardNotInDeck,
    DeckAtMinimumSize,          // Card System EC7
    CardNotInOffer,
    CardUnpriced,               // MerchantPrice == 0 (defensive — should be filtered upstream)
    TransactionInFlight,
    ConversionInsufficientSource
}

public enum TransactionVerb {
    Install, Repair, Purge, Purchase, ScrapPart,
    ConvertScrapToFuel, ConvertFuelToScrap, GrantScrap
}
```

### States and Transitions

#### `CurrentScrap` lifecycle

| Phase | Value | Owner |
|-------|-------|-------|
| Run start | `StartingScrap[chassis]` (Scout 60 / Assault 50 / Truck 40 — all `[TUNABLE]`, defaults in Section G) | `ScrapEconomyRunState` constructor |
| Live | Mutated only by named verbs | Verb Commit phase |
| Save | Serialized via `ScrapStateDTO` | Save & Persistence |
| Run end | Snapshotted for end-of-run summary; read-only thereafter | `ScrapEconomyRunState` teardown |
| Cross-run carry | None | — |

`CurrentScrap` is a plain `int` on a POCO run-state object, not a MonoBehaviour field (per project convention — combat and run state must be POCOs).

#### Transaction state machine

Every verb passes through four phases:

```
Proposed → Validated → Committed → Logged
```

| Phase | Enter | Success exit | Failure exit | Side effects |
|-------|-------|--------------|--------------|--------------|
| **Proposed** | Caller invokes `TryX(...)`; system sets `TransactionInFlight = true` | Validated begins | Immediate return with `FailureReason.TransactionInFlight` if a prior transaction is still live (reentrancy guard) | None |
| **Validated** | Synchronous precondition checks against current run state | Committed begins | `TransactionInFlight = false`; `TransactionResult.Failed(reason)` returned; no state mutated | None |
| **Committed** | All preconditions passed. This is the only phase that mutates `CurrentScrap`, the vehicle, or the deck. Mutations occur in a single contiguous block; if any downstream call throws, the Scrap mutation is rolled back and `Failed` is returned. | Logged begins | Rollback; `TransactionInFlight = false`; `TransactionResult.Failed(reason)` returned. Under correct precondition coverage this path should never activate in production. | `CurrentScrap` updated; `IVehicleMutator` / `IDeckMutator` / `IFuelSystem` called as applicable |
| **Logged** | Append `ScrapTransaction` record to in-memory log | `TransactionInFlight = false`; fires `OnTransactionCompleted`; returns `TransactionResult.Ok(...)` | N/A (terminal success) | Observable event emitted for UI subscribers |

**Atomicity contract:** compute-all → verify-all → mutate-all. No mutation before Commit. No event before Logged.

**Reentrancy rule:** `OnTransactionCompleted` fires *after* `TransactionInFlight` is cleared. Subscribers may call `GrantScrap` (non-blocking income) synchronously in the handler; subscribers may NOT call any `TryX` spend verb synchronously — queue for next frame.

**Concurrency:** single-threaded main-thread only. `TransactionInFlight` guards against double-click at the UI layer.

#### Verb-specific failure modes

**TryInstall** — (1) `TransactionInFlight` → fail; (2) insufficient Scrap; (3) `part.SlotType != slot` → `SlotTypeMismatch`; (4) `!part.CompatibleChassis.Contains(chassis)` → `ChassisIncompatible`; (5) installing this part would produce an Override collision on any stat (V&P OQ-VP2, fail-closed) → `OverrideCollision`. Auto-scrap-on-replace is handled internally — no caller-visible separate Scrap transaction.

**TryRepair** — (1) `TransactionInFlight`; (2) `DamageState == Empty` → `PartNotInstalled`; (3) `DamageState == Functional` → `PartNotDamaged`; (4) insufficient Scrap. All damage states (`Degraded`, `Offline`) are repairable at any Chopshop — **no tier gate in EA** (flagged in Section K as a post-launch consideration).

**TryPurge** — (1) `TransactionInFlight`; (2) `!deck.Contains(card)` → `CardNotInDeck`; (3) `deck.Count ≤ 10` → `DeckAtMinimumSize` (Card System EC7); (4) `!freeValveActive && CurrentScrap < 30` → `InsufficientScrap`. `ScrapDelta = freeValveActive ? 0 : -30`.

**TryPurchase** — (1) `TransactionInFlight`; (2) `!offer.Contains(card)` → `CardNotInOffer`; (3) `card.MerchantPrice == 0` → `CardUnpriced` (defensive — Node Encounter generation should filter these upstream; Scrap Economy rejects as last defense); (4) insufficient Scrap. No deck upper-bound check — matches Card System GDD (no maximum deck size).

**TryScrapPart** — (1) `TransactionInFlight`; (2) `DamageState == Empty` → `PartNotInstalled`; (3) `slot == Frame` → `FrameSlotWouldBeEmpty` (V&P OQ-VP3). No `InsufficientScrap` failure mode — this verb always grants Scrap.

**TryConvertScrapToFuel / TryConvertFuelToScrap** — (1) `TransactionInFlight`; (2) source resource balance < requested amount → `ConversionInsufficientSource`. Both mutations (source debit, target credit) occur inside a single Commit block. Both sides log independently; the Fuel-side log entry is owned by the Fuel system, the Scrap-side entry by this system.

#### Transaction log schema

```csharp
public record ScrapTransaction(
    int              TransactionId,   // monotonic, resets at run start
    TransactionVerb  Verb,
    int              ScrapDelta,      // signed; 0 for free-valve Purge
    int              ScrapAfter,      // balance after this transaction (corruption check)
    string           Context,         // "Chopshop[node:4]", "CombatReward[node:7]", etc.
    long             SequenceNumber   // monotonic counter (not wall-clock)
);
```

Field rationale: `Verb` + `Context` directly satisfy the Player Fantasy's "every transaction is named" claim. `ScrapAfter` is denormalized for post-load corruption detection (last-entry `ScrapAfter` must equal deserialized `CurrentScrap`). `SequenceNumber` replaces `DateTime.Now` because the game is deterministic and wall-clock would introduce platform variance across save/load.

The log is append-only, serialized with the run, discarded at run end. Expected size under 300 entries per run.

#### UI contract — mental-budget moments

The "decisions you have left" fantasy requires `CurrentScrap` to be visible at three recompute moments:

1. **Arriving at a Chopshop / Merchant.** All costs for all available verbs displayed on entry — no reveal-on-click.
2. **Picking a node at a branch.** HUD counter visible during map navigation (not just encounter screens).
3. **Accepting a reward with a choice.** Scrap / Card / Fuel options displayed simultaneously with values visible — never sequentially.

Scrap counter must update at the **moment of transaction confirmation**, not after any animation completes. Reward acceptance animates cosmetically; the number is already correct before the animation ends.

### Interactions with Other Systems

| System | Scrap Economy reads | Scrap Economy writes | Interface owner |
|--------|---------------------|----------------------|-----------------|
| **Vehicle & Part** | `IVehicleView.Slots[*].InstalledPart`, `.DamageState`, `.Hp / MaxHp`; `IVehicleView.ChassisId`; `IVehicleView.InstalledCount` for cost scaling | `IVehicleMutator.InstallPart(slot, part)`, `RemovePart(slot)`, `Repair(slot, hp, canReviveOffline)` — **exclusive player-initiated caller** | V&P |
| **Card System** | `CardDefinitionSO.Rarity` (for Merchant pricing); `CardDefinitionSO.MerchantPrice`. **Does NOT read `PurgeCost`** — Purge resolves at flat 30. `PurgeCost` field is removed from the SO (Card System GDD retrofit queued). | Deck mutation via `IDeckMutator` on Purge / Purchase — **exclusive player-initiated caller at map layer** | Card System |
| **Node Map** | Chopshop / Merchant node context (`nodeIndex`, `nodeType`) for `Context` string + free-valve seed derivation; `TruckRewardMultiplier ≥ 1.25` applied by Loot & Reward upstream of `GrantScrap` | None (Node Map is read-only from this system's view) | Node Map |
| **Fuel System** | `IFuelSystem.CurrentFuel` (read before Convert) | `IFuelSystem.AddFuel(int)`, `SpendFuel(int)` — called inside Convert Commit block; AC-NM55 separation enforced structurally (no shared field, named verbs only, both sides logged) | Fuel / Node Map |
| **Card Combat** | **Nothing.** | **Nothing.** | Non-interaction. `IScrapEconomy` is not injected into any Combat-namespace class. Any such import is a build violation. |
| **Save & Persistence** | `ScrapStateDTO` on load (`CurrentScrap`, `TransactionLog`, `runSeed`, `SequenceNumber`) | `ScrapStateDTO` on save | Save & Persistence owns `IRunSerializable`; Scrap Economy owns DTO schema. Save point always after `Logged` phase — no half-committed state reachable. |
| **Loot & Reward** *(undesigned — forward dep)* | None at this layer (receives post-multiplier amount) | `GrantScrap(amount, context)` called by Loot & Reward on reward acceptance; Loot & Reward must apply `TruckRewardMultiplier ≥ 1.25` before the call (AC-NM54) | Loot & Reward (undesigned) |
| **Node Encounter** *(undesigned — forward dep)* | None today | Future: event effects may call `TryConvertScrapToFuel` / `TryConvertFuelToScrap` directly. Verb signatures are caller-agnostic — any map-layer system can invoke. | Node Encounter (undesigned) |

#### Player-felt interaction flows

**Combat reward acceptance with choice.** When a combat node awards a variable reward (e.g., Scrap bundle vs. card offer vs. Fuel cache), all options are presented simultaneously with values visible. Accepting a card is declining Scrap — that opportunity cost is the economy working. If the reward is a pure Scrap bundle with no alternative, `GrantScrap` fires on reward-screen close; the counter animates to the new value, but the logical mutation has already applied.

**Pity-to-Scrap compensation.** When the Rare card pool is empty and the pity counter fires (counter owned and persisted by Loot & Reward per ADR-0006), compensation is awarded as **40 Scrap** (tunable as `PityScrapAward`). The amount sits deliberately between one Purge (30) and one Rare install (~50–65) — framed as salvage the wasteland owes the player when the Rare pool has nothing left to give (see Section B). Small enough that the player cannot immediately spend it into a Rare install; large enough that it's not a token gesture.

**Convert as controlled emergency.** Conversion rates (see Section D) are asymmetric and lossy in both directions. The Scrap → Fuel side is the more punishing ratio because running out of Fuel is more immediately run-ending than running out of Scrap; the direction the panicked player is more likely to reach for costs more per unit. The UI displays the exact output value before confirmation.

**Free-valve discovery.** Free-valve state is revealed on Chopshop entry via a distinct UI treatment (highlighted "FREE" badge on the Purge verb's cost display), **not pre-surfaced on the map icon**. Routing toward confirmed free-valve Chopshops would convert a surprise mechanic into an optimization; entry-reveal preserves the gift feeling.

**Haven terminal sink (forward dep).** A soft-cap pool can produce late-run hoarding (200+ Scrap at Haven). This is a Pillar-4 failure only if there is no terminal sink. **Haven Encounter GDD (undesigned) must provide a Scrap sink at run end** (tiered blessings / vault purchases / persistent upgrades priced ≥ 100 Scrap per tier). Flagged in Section F as a required forward dependency — Scrap Economy cannot be fully balanced end-to-end without it.

#### Retrofits and open items queued by this section

- **Card System GDD retrofit** (structural) — Remove `PurgeCost: int` field from `CardDefinitionSO`; simplify F5 to `PurgeTransactionCost = GlobalPurgeCost`; drop related ACs for per-card purge validation; collapse Tuning Knob row. Queued for after Scrap Economy GDD approval.
- **V&P GDD** — verify `IVehicleView.Slots[slot].InstalledPart` exposes `StatModifiers` read-only for Override-collision detection. If not, flag as a V&P retrofit.
- **Save & Persistence GDD** — register `ScrapStateDTO`; document save-point-after-Logged invariant.
- **Loot & Reward GDD** (undesigned) — must apply `TruckRewardMultiplier ≥ 1.25` before calling `GrantScrap`.
- **Node Encounter GDD** (undesigned) — event hooks may call Convert verbs (per user note 2026-04-22).
- **Haven Encounter GDD** (undesigned) — must provide terminal Scrap sink.

## Formulas

All Scrap values are integers. All costs round **up** against the player (`Ceiling`); all refunds and conversion outputs round **down** against the player (`Floor`). Rounding convention matches V&P OQ-VP5.

### D.1 — Constants

These are flat values, not formulas. Included here for cross-reference and registry registration.

| Constant | Default | Range [TUNABLE] | Source | Rationale |
|----------|---------|-----------------|--------|-----------|
| `GlobalPurgeCost` | 30 | 20–45 | Locked 2026-04-22 | Flat Purge cost. Replaces per-card `PurgeCost` field on `CardDefinitionSO` (Card System retrofit). Sits between one Repair (~15–25) and one Rare install (~50–65), making Purge a genuine competitor in the decision tier, not cheap enough to spam. |
| `StartingScrap[Scout]` | 60 | 40–80 | D.1 | Scout has lighter install costs and a slower reward curve; starts highest. Covers one Common install at the first Chopshop without running dry. |
| `StartingScrap[Assault]` | 50 | 35–70 | D.1 | Baseline. Can afford one Uncommon install at first Chopshop or two Commons. |
| `StartingScrap[Truck]` | 40 | 30–60 | D.1 | Truck starts lowest because `TruckRewardMultiplier ≥ 1.25` compensates on every combat reward downstream. |
| `PityScrapAward` | 40 | 25–60 | D.1 | Fires when Loot & Reward's pity counter (per ADR-0006) triggers on an empty Rare pool. Sits between one Purge (30) and one Rare install (~50–65) — framed as salvage the wasteland owes the player when the Rare pool has nothing left to give. Large enough to act on; small enough that it can't immediately buy a Rare. |
| `FreeValveProbability` | 0.33 | 0.20–0.50 | Card System Tuning Knobs | Seeded per Chopshop visit via `new System.Random(runSeed ^ nodeIndex).NextDouble() < FreeValveProbability`. Deterministic across reloads. |
| `ScrapRefundRate` | 0.40 | 0.20–0.50 | vehicle-and-part-mechanics.md F-VPM2 | Fraction of `InstallBaseCost` returned on standalone scrap. Authority moved to vehicle-and-part-mechanics.md F-VPM2 (W5 redesign — tenure system eliminated). Static formula; no tenure component. Hard max: 0.99 (OnValidate clamp on owning SO — see entities.yaml). Always a net loss vs. full install cost. |
| ~~`TenureDecayRate`~~ | ~~0.10~~ | ~~0.05–0.20~~ | **DEPRECATED (W5)** | Tenure system eliminated. Do not use. Was: per-combat-survived reduction in refund multiplier. |
| ~~`TenureMinMultiplier`~~ | ~~0.25~~ | ~~0.10–0.50~~ | **DEPRECATED (W5)** | Tenure system eliminated. Do not use. Was: floor on tenure decay multiplier. |

### D.2 — Install Cost

```
InstallCost(rarity, installedCount, totalSlots) = Ceiling( InstallBaseCost[rarity] × (1 + (installedCount / totalSlots) × kNorm) )
```

**Variables:**

| Name | Type | Domain | Source |
|------|------|--------|--------|
| `rarity` | enum | `{Common, Uncommon, Rare}` | `PartDefinitionSO.Rarity` |
| `installedCount` | int | 0 ≤ installedCount ≤ totalSlots | `IVehicleView.InstalledCount` (count of non-Empty slots) |
| `totalSlots` | int | 3–12 (EA range 4–10) | `IVehicleView.Slots.Count` (per FrameLayoutSO; see V&P R_FL.2) |
| `InstallBaseCost[Common]` | int | 10 [TUNABLE: 8–15] | D.2 |
| `InstallBaseCost[Uncommon]` | int | 25 [TUNABLE: 20–35] | D.2 |
| `InstallBaseCost[Rare]` | int | 50 [TUNABLE: 40–65] | D.2 |
| `kNorm` | float | 0.60 [TUNABLE: 0.40–1.00] | D.2 — full-vehicle surcharge cap (max multiplier is `1 + kNorm` when every slot is filled) |

**Worked examples** (normalized fill ratio = `installedCount / totalSlots`; multiplier is `1 + ratio × 0.60`):

| Chassis (`totalSlots`) | Rarity | Empty (`fill=0`) | Half-full (`fill=0.5`) | Full (`fill=1.0`) |
|--------|--------|-----------------|------------------------|-------------------|
| Scout / Hauler (4) | Common | `Ceiling(10 × 1.00) = 10` | `Ceiling(10 × 1.30) = 13` | `Ceiling(10 × 1.60) = 16` |
| Scout / Hauler (4) | Rare | `Ceiling(50 × 1.00) = 50` | `Ceiling(50 × 1.30) = 65` | `Ceiling(50 × 1.60) = 80` |
| Medium (5) | Rare | `Ceiling(50 × 1.00) = 50` | `Ceiling(50 × 1.30) = 65` | `Ceiling(50 × 1.60) = 80` |
| Heavy (7) | Rare | `Ceiling(50 × 1.00) = 50` | `Ceiling(50 × 1.30) = 65` (3 filled = ratio 0.43, multiplier 1.26 → `Ceiling(50 × 1.26) = 63`) | `Ceiling(50 × 1.60) = 80` (7 filled) |
| Dredge (10) | Rare | `Ceiling(50 × 1.00) = 50` | `Ceiling(50 × 1.30) = 65` (5 filled) | `Ceiling(50 × 1.60) = 80` (10 filled) |

**R6 amendment rationale (closes Blocker 4 — economy-designer + unity-specialist):** the pre-R6 formula used a raw `installedCount × k` term with k=0.15. This was defined for `installedCount ∈ 0..4` only — at the Dredge boss (10 slots) the raw formula produced a multiplier of `1 + 10 × 0.15 = 2.50`, charging ~125 Scrap for a Rare install (vs ~50 baseline) — a ~2.5× cost spike with no design intent. The fix normalizes the surcharge against the vehicle's total slot count so that a "full vehicle" always pays the same cap multiplier (`1 + kNorm`) regardless of chassis size. Each existing installed part adds a proportional surcharge on the next install — a half-full vehicle costs 30% more to modify than an empty one, a full vehicle costs 60% more. Domain valid for `totalSlots ∈ [3, 12]`. Scaling on the fill ratio (system-wide, not per-slot) reflects "vehicle complexity relative to its frame" rather than absolute slot count.

**Implementation note**: `totalSlots == 0` is forbidden by V&P R_FL.1 (every chassis declares ≥ 1 slot via FrameLayoutSO OnValidate). The formula's `totalSlots` denominator is therefore safe — no division-by-zero guard required at runtime. CI test asserts `InstallCost(Rare, totalSlots, totalSlots)` ≤ `Ceiling(InstallBaseCost[Rare] × (1 + kNorm))` for `totalSlots ∈ [3, 12]`.

### D.3 — Repair Cost

```
RepairCost(currentDamage, rarity) = Ceiling( currentDamage × RarityRepairRate[rarity] )
```

**Variables:**

| Name | Type | Domain | Source |
|------|------|--------|--------|
| `currentDamage` | int | `slot.MaxHp − slot.Hp`, ≥ 1 (zero-damage blocked at UI) | `IVehicleView.Slots[slot]` |
| `rarity` | enum | `{Common, Uncommon, Rare}` | `slot.InstalledPart.Rarity` |
| `RarityRepairRate[Common]` | float | 2.0 [TUNABLE: 1.5–3.0] | D.3 |
| `RarityRepairRate[Uncommon]` | float | 2.5 [TUNABLE: 2.0–4.0] | D.3 |
| `RarityRepairRate[Rare]` | float | 3.5 [TUNABLE: 2.5–5.0] | D.3 |

**Worked examples** (assumes typical `MaxHp` values from V&P; exact values owned by V&P balance pass):

| Part | MaxHp (assumed) | Damage taken | RepairCost |
|------|----------------|--------------|------------|
| Common Weapon | 8 | 4 | `Ceiling(4 × 2.0) = 8` |
| Common Weapon | 8 | 8 (Offline) | `Ceiling(8 × 2.0) = 16` |
| Uncommon Mobility | 10 | 5 | `Ceiling(5 × 2.5) = 13` |
| Rare Engine | 12 | 6 | `Ceiling(6 × 3.5) = 21` |
| Rare Engine | 12 | 12 (Offline) | `Ceiling(12 × 3.5) = 42` |

**Rationale:** Linear in damage (no thresholds, no diminishing returns) — keeps the math legible to the player. Rarity multiplier reflects the material cost of maintaining a higher-end part. A Rare slot fully Offline costs ~42 Scrap to restore: meaningful but not run-ending for a player with healthy reserves. Multiple simultaneously-damaged slots create compounding triage pressure, which is the design goal.

**Forward dep flag:** Actual `MaxHp` values per rarity tier are owned by V&P. These examples use plausible placeholders; formula outputs shift when V&P balance pass lands.

**Free-repair path (Rest handler retrofit 2026-04-23):** When `TryRepair` is called with `freeRepair == true` (invoked exclusively by Node Encounter's Rest handler per NE C.2.6), `RepairCost` evaluates to 0 and the `InsufficientScrap` failure path is unreachable. All other invariants hold — transaction state machine runs to `Logged`, `TransactionResult.ScrapDelta = 0`, observers fire normally. Rest handler MUST pass `freeRepair: true`; all other callers (Chopshop via map UI, etc.) MUST pass `freeRepair: false`. Audit validator: any `TransactionLog` entry with `verb = Repair` and `freeRepair = true` MUST carry origin `"RestHandler[node:X]"` — any other origin is an authoring error and logs a warning (caller invoked free-repair semantics without Rest-handler provenance).

### D.4 — Purge Cost

```
PurgeTransactionCost = IsFreeValveApplied ? 0 : GlobalPurgeCost
```

Not a formula — a flat constant with a single conditional override. `GlobalPurgeCost = 30` (see D.1). `IsFreeValveApplied` is computed per Chopshop visit (see D.8); consumed by the first Purge only.

### D.5 — Merchant Purchase Price

```
MerchantPrice(rarity) = BaseMerchantCost[rarity]
```

**Variables:**

| Name | Type | Domain | Source |
|------|------|--------|--------|
| `rarity` | enum | `{Common, Uncommon, Rare}` | `CardDefinitionSO.Rarity` |
| `BaseMerchantCost[Common]` | int | 15 [TUNABLE: 10–25] | D.5 |
| `BaseMerchantCost[Uncommon]` | int | 40 [TUNABLE: 30–55] | D.5 |
| `BaseMerchantCost[Rare]` | int | 75 [TUNABLE: 60–100] | D.5 |

Authored per-card on `CardDefinitionSO.MerchantPrice` using these tier values as the guide. The SO field is the runtime source; this formula is the authoring rule.

**Parity rule (OQ7 resolution):** `MerchantPrice` may not equal `GlobalPurgeCost` (30) for any card. With defaults above, this holds trivially — the rule only activates if tuning ever drops a card's price to exactly 30. SO import validator enforces this.

**Worked example:** A Rare card at 75 Scrap represents ~18–27% of a full run's Scrap income (~280–420 Scrap over 14 combat nodes). One Merchant Rare purchase plus one Rare install consumes ~140 Scrap — roughly half a run's budget. That's the intended tension floor for a Merchant visit.

### D.6 — Scrap Refund (player-initiated standalone scrap)

> **⚠ Authority redirect — W5 redesign (2026-05-20):** The tenure-based formula previously defined here has been eliminated. The authoritative formula is now in **vehicle-and-part-mechanics.md F-VPM2**. This section is retained as a read-through reference only; all formula changes must be made in F-VPM2.

```
ScrapRefund(rarity) = max( 1, Floor( InstallBaseCost[rarity] × ScrapRefundRate ) )
```

**Static values at default `ScrapRefundRate = 0.40`:**

| Rarity | `InstallBaseCost` | `ScrapRefund` |
|--------|-------------------|---------------|
| Common | 10 | `max(1, Floor(10 × 0.40)) = 4 Scrap` |
| Uncommon | 25 | `max(1, Floor(25 × 0.40)) = 10 Scrap` |
| Rare | 50 | `max(1, Floor(50 × 0.40)) = 20 Scrap` |

**Variables:**

| Name | Type | Domain | Source |
|------|------|--------|--------|
| `rarity` | enum | `{Common, Uncommon, Rare}` | `slot.InstalledPart.Rarity` |
| `InstallBaseCost[*]` | int | (see D.2) | D.2 |
| `ScrapRefundRate` | float | 0.40 [TUNABLE: 0.20–0.50; hard max 0.99 — see entities.yaml] | vehicle-and-part-mechanics.md F-VPM2 |

**Rationale.** Refund is flat per rarity tier — it does not encode install-timing, vehicle fill, or part age. Permanence is encoded on the cost side: a part installed at a nearly full vehicle costs more (F-VPM1 fill scaling), so removing it leaves a larger net loss — but that asymmetry comes from the elevated install cost, not a reduced refund. Scrapping is always a net loss; how much loss depends on when you installed, not how long you kept it.

Refund calculates against `InstallBaseCost`, **not** the scaled `InstallCost` paid. This closes the degenerate loop "install at high fill → scrap for base refund → profit." The fill scaling is a friction charge on the transaction, not recoverable investment.

**Replace-scrap exception:** When `TryInstall` internally auto-scraps a previously-installed part to make room, **no refund is issued**. The refund applies only to the standalone `TryScrapPart` verb. This prevents a micromanagement loop (scrap-first-for-refund → then-install) and keeps the replace flow clean.

### D.7 — Conversion Rates

Asymmetric-punitive. Both directions feel like losses; the Scrap → Fuel side hurts slightly more because running out of Fuel ends runs directly.

```
FuelOut  = Floor( ScrapIn / ScrapPerFuelRate )   // Scrap → Fuel
ScrapOut = Floor( FuelIn  / FuelPerScrapRate )   // Fuel → Scrap
```

**Variables:**

| Name | Type | Domain | Source |
|------|------|--------|--------|
| `ScrapIn` | int | ≥ `ScrapPerFuelRate` (minimum 1 Fuel output; UI blocks sub-rate amounts) | Caller |
| `FuelIn` | int | ≥ `FuelPerScrapRate` (minimum 1 Scrap output) | Caller |
| `ScrapPerFuelRate` | int | 4 [TUNABLE: 3–6] | D.7 — Scrap→Fuel rate |
| `FuelPerScrapRate` | int | 4 [TUNABLE: 3–6] | D.7 — Fuel→Scrap rate |

**Worked examples:**

| Direction | Input | Output | Net effect |
|-----------|-------|--------|------------|
| Scrap → Fuel | 4 S | 1 F | Emergency: "I need one more hop" |
| Scrap → Fuel | 20 S | 5 F | Mid-desperation: two-thirds of a purge's worth of Scrap for a short run to next station |
| Scrap → Fuel | 40 S | 10 F | Panic: nearly a Rare install sacrificed to reach any station |
| Fuel → Scrap | 4 F | 1 S | Negligible — almost never worth calling |
| Fuel → Scrap | 20 F | 5 S | Sacrificing routing buffer for ~one Repair's worth — clearly suboptimal |
| Fuel → Scrap | 40 F | 10 S | Catastrophic Fuel-dump for barely a Repair — never the right move |

**Rationale:** 1 Fuel is worth ~3–5 Scrap on the "open market" (a Fuel node provides 4–6 Fuel; a Combat node ~15–25 Scrap). The 4 F → 1 S rate prices Fuel-to-Scrap at ~25% of open-market value — a 75% haircut. Combined with Fuel's run-ending downside, Fuel-to-Scrap is categorically a mistake-mover, not a strategy. Scrap-to-Fuel at 4:1 is also bad economics but serves as a genuine emergency valve: 30 Scrap (one purge's budget) yields 7 Fuel — enough for a short recovery leg.

**AC-NM55 compliance:** Both directions emit independent log entries (Scrap-side logged by Scrap Economy; Fuel-side logged by Fuel System). No shared pool field. Both mutations occur inside the single `Committed` phase of the Convert verb.

### D.8 — Free-Valve Roll

```
IsFreeValveApplied = new System.Random(runSeed ^ nodeIndex).NextDouble() < FreeValveProbability
```

**Variables:**

| Name | Type | Domain | Source |
|------|------|--------|--------|
| `runSeed` | int | Run-level master seed | `ScrapStateDTO.runSeed` (set at run creation, serialized) |
| `nodeIndex` | int | Stable node identifier from Node Map generation | `NodeMap.Nodes[i].Index` |
| `FreeValveProbability` | float | 0.33 [TUNABLE: 0.20–0.50] | D.1 |

**Determinism:** XOR'ing `runSeed` with `nodeIndex` produces a per-(run, node) seed. Each Chopshop visit rolls once on entry; result is recomputed, not re-rolled, if the player saves and reloads mid-visit. `IsFreeValveApplied` is **not** serialized — it is re-derived from the serialized `runSeed` and `nodeIndex`.

**Scope:** Applies to the **first** Purge of that Chopshop visit only. Consumed after first use (free or paid — the free-valve "slot" burns on first purge regardless of whether the slot was used). Second and subsequent purges at the same visit cost `GlobalPurgeCost` (30).

**Worked example:** Over 10,000 simulated Chopshop entries at `FreeValveProbability = 0.33`, approximately 33% (± 2–3%) return `IsFreeValveApplied == true`. This is a Section J acceptance criterion (AC).

### D.9 — Income (from Loot & Reward) — forward dep

Incoming Scrap from combat rewards, beacons, and events is owned by the **Loot & Reward GDD** (undesigned). This GDD receives the final amount via `GrantScrap(amount, context)`. The multiplier contract:

```
GrantedAmount = BaseReward × (chassis == Truck ? TruckRewardMultiplier : 1.0)
```

Where `TruckRewardMultiplier ≥ 1.25` (locked in Node Map registry; floor constant, not tunable below this value). Loot & Reward applies the multiplier **before** calling `GrantScrap` — Scrap Economy does not know chassis type.

**Approximate run-level faucet** (informing sink targets): ~280–420 Scrap over 14 combat nodes from combat rewards alone. With Event and Merchant-conversion inflow, practical ceiling ≈ 450–550 Scrap total per run. Target end-state (Haven arrival): 20–60 Scrap for a typical spend pattern — enough for a final Uncommon install or two repairs, not enough to freely purchase-plus-install.

### D.10 — Formula summary table (quick reference)

| Formula | Inputs | Output | Default |
|---------|--------|--------|---------|
| `InstallCost` | rarity, installedCount, totalSlots | int Scrap cost | D.2 |
| `RepairCost` | damage, rarity | int Scrap cost | D.3 |
| `PurgeTransactionCost` | `IsFreeValveApplied` | int (0 or 30) | D.4 |
| `MerchantPrice` | rarity | int Scrap cost | D.5 (authored per-SO) |
| `ScrapRefund` | rarity | int Scrap return | vehicle-and-part-mechanics.md F-VPM2 (authority); D.6 (redirect) |
| `FuelOut` (Scrap→Fuel) | ScrapIn | int Fuel gain | D.7 |
| `ScrapOut` (Fuel→Scrap) | FuelIn | int Scrap gain | D.7 |
| `IsFreeValveApplied` | runSeed, nodeIndex | bool | D.8 |
| `GrantedAmount` (income) | baseReward, chassis | int Scrap gain | D.9 (Loot & Reward owns) |

## Edge Cases

Each edge case states the exact system response. Ordered by likelihood of encounter during normal play (EC-SE1 most common, EC-SE15 rarest).

### EC-SE1 — Commit-phase exception from downstream interface

**Situation:** A verb passes Validated. During `Committed`, `IVehicleMutator.InstallPart` (or any downstream mutator call) throws an exception — e.g., an internal V&P invariant violation surfaced only at mutation time.

**Resolution:** Roll back the Scrap mutation (`CurrentScrap` is restored to pre-transaction value), clear `TransactionInFlight`, append an audit entry with `Verb = <original verb>, ScrapDelta = 0, Context = "COMMIT_ROLLBACK: <caller context>"`. Return `TransactionResult.Failed(FailureReason.*)` with the reason mapped from the exception. Do not rethrow — the transaction failing cleanly is preferable to the map layer crashing. Under correct precondition coverage, this path should never fire in production; if it does, it indicates a missing Validated-phase check and must be logged at error severity for post-mortem.

### EC-SE2 — Save requested mid-transaction

**Situation:** The player triggers a save (auto-save or manual) while `TransactionInFlight == true`.

**Resolution:** Save & Persistence queries `IScrapEconomy.TransactionInFlight` before writing. If `true`, the save call blocks (no state written) until the current transaction reaches `Logged`. Since transactions are single-threaded, synchronous, and complete within a single frame, the block is imperceptible. This satisfies the save-point-after-Logged invariant — no half-committed state ever reaches disk. Save & Persistence GDD retrofit: document this precheck.

### EC-SE3 — Non-divisible Convert input

**Situation:** Player attempts `TryConvertScrapToFuel(7)` with `ScrapPerFuelRate = 4`.

**Resolution:** `FuelOut = Floor(7 / 4) = 1`. The player spends 7 Scrap and receives 1 Fuel; the house keeps the residual 3 Scrap. UI shows the output value (`→ 1 Fuel`) before confirmation — the player sees what they are getting before they pay. UI preview updates live as the input slider moves, so the player can always step to the exact 4-multiple (4 S → 1 F, 8 S → 2 F, etc.) if they want no residual. The non-divisible path is legal and logged; it is a user choice, not a bug.

### EC-SE4 — Free-valve triggered at deck floor

**Situation:** Player arrives at a Chopshop; free-valve rolls true; deck is already at `DeckSize == 10`.

**Resolution:** Purge is blocked by `DeckAtMinimumSize` regardless of free-valve state (per Core Rule 2). The free-valve roll is not unconsumed — it simply never applies because no Purge occurs. If the player later obtains another Purge opportunity after adding cards (e.g., at a later Chopshop), that visit gets its own independent free-valve roll. The original visit's roll is **not** carried forward. UI treatment: the FREE badge is shown on the Purge button, but clicking the button produces the standard deck-floor rejection message. The badge is not a promise; it's a state indicator.

### EC-SE5 — Double-click on verb button

**Situation:** Player clicks the "Install" button twice in rapid succession (network UI delay, double-click muscle memory, etc.).

**Resolution:** The first click enters `Proposed` and sets `TransactionInFlight = true`. The second click's `TryInstall` call immediately returns `FailureReason.TransactionInFlight` without touching state. Since transactions complete within a single frame, the guard window is ~16ms; a true double-click at 100–200ms inter-click delay will find the flag cleared and proceed as a second transaction. This is intentional — the flag guards against same-frame re-entry, not against the player deliberately performing two transactions back-to-back.

### EC-SE6 — Zero-Scrap Chopshop with all Functional slots

**Situation:** Player enters a Chopshop with `CurrentScrap == 0`, all four slots in `Functional` state, and a deck at exactly 10.

**Resolution:** Every verb fails its precondition. Install: `InsufficientScrap`. Repair: `PartNotDamaged`. Purge: `DeckAtMinimumSize`. Scrap: valid (always free) but player may choose not to. Convert Fuel→Scrap: valid if `CurrentFuel ≥ 4`. Player exits the Chopshop having done nothing. This is a **legal run state**, not an error. The Chopshop node does not gate exit on performing a transaction. Section I (UI) must ensure the exit/leave affordance is always visible regardless of which verbs are available.

### EC-SE7 — Pity award when would-have-been rarity was Common

**Situation:** The pity counter triggers on an empty pool. By the Card System pity spec (40-node pity window) — counter authored by Card System but written and persisted by Loot & Reward per ADR-0006 — the trigger is tied to Rare-pool exhaustion. The counter explicitly tracks "nodes since last Rare" (per Card System GDD F3). Consequently pity never fires for Common or Uncommon pools.

**Resolution:** `PityScrapAward = 40` flat, awarded via `GrantScrap(40, "Pity[node:X]")`. No rarity-sensitive variant. If the pity mechanic is ever generalized to other rarities post-EA, this constant may need rarity-keyed variants — flagged in Section K. For EA: single flat value, no branching.

### EC-SE8 — Fuel at cap during Scrap→Fuel Convert

**Situation:** Player calls `TryConvertScrapToFuel(20)` with `CurrentFuel` near the Fuel cap such that the converted Fuel would overflow.

**Resolution:** This GDD cannot unilaterally define overflow behavior — it is a **Fuel System contract question** flagged `[CONFIRM: Fuel System GDD]` in Section F. Three candidate behaviors:
- (a) `IFuelSystem.AddFuel` saturates at cap silently → Scrap paid in full, Fuel capped, residual lost to the house.
- (b) `IFuelSystem.AddFuel` rejects the overfill; Scrap Economy validates `CurrentFuel + FuelOut ≤ FuelCap` at Validated phase and fails with a new `FailureReason.FuelCapReached`.
- (c) Scrap Economy pre-clamps `FuelOut` to remaining capacity and scales `ScrapIn` accordingly (partial Convert).

**Working default pending Fuel GDD:** option (b) — fail at Validated with `FuelCapReached`. UI shows the max convertible amount as input-slider clamp based on `FuelCap - CurrentFuel`. This is the least surprising behavior (no silent Scrap loss) and the cheapest to implement defensively. Flagged in Section F as a Fuel System contract dependency.

### EC-SE9 — Fuel→Scrap leaving insufficient Fuel for next leg

**Situation:** Player converts enough Fuel to Scrap that the remaining Fuel cannot reach the next station. Run ends in fuel-starvation shortly after.

**Resolution:** Scrap Economy does NOT prevent this. Validating "does the player have enough Fuel for the next leg after this conversion?" requires knowledge of future routing and is Node Map's domain, not Scrap Economy's. The Convert verb's only Fuel-side check is `CurrentFuel ≥ fuelAmount` (non-negative result). Player choice respected; consequence real. UI may optionally show "Current Fuel: X / Nearest station: Y nodes away" during Convert confirmation as a courtesy — Section I (UI) owns that decision.

### EC-SE10 — Identical-part replace (same SO, same slot)

**Situation:** Player's Weapon slot is occupied by a Common Weapon "A". They install Common Weapon "A" again (same `CardDefinitionSO`).

**Resolution:** Treated as a normal replace. Auto-scraps the existing "A" (no refund, per Replace-scrap exception in D.6), installs the new "A", pays full `InstallCost`. The transaction is legal but economically pointless — the player's vehicle is identical pre- and post-transaction, minus 10 Scrap. UI **[CONFIRM: UX wording]** — add a subtle warning treatment on the install confirmation when `existingPart.Definition == newPart.Definition` (e.g., "This part is already installed. Install anyway?"). The warning does not block; it informs. Flagged for UX pass in Section I.

### EC-SE11 — Save/reload mid-Chopshop visit with free-valve already consumed

**Situation:** Player enters a Chopshop, uses the free-valve Purge, then saves. On reload, should the free-valve roll fire again?

**Resolution:** No. A per-visit flag `FreeValveConsumedThisVisit : bool` is added to the save DTO (`ScrapStateDTO`), keyed by `nodeIndex`. On visit entry: if `CurrentNodeIndex == savedNodeIndex && FreeValveConsumedThisVisit`, the free-valve slot is already burned and the purge cost is the standard 30. On next Chopshop entry (new `nodeIndex`), the flag resets and the new visit rolls its own free-valve per D.8. Save & Persistence retrofit: register `FreeValveConsumedThisVisit` as a visit-scoped field.

### EC-SE12 — Run-end reached with a very large transaction log

**Situation:** Exceptional run with 500+ transactions — e.g., heavy Chopshop use, repeated conversions.

**Resolution:** Transaction log is retained for end-of-run summary screen ("You spent X Scrap this run: Y% on Install, Z% on Repair..."), then discarded on run teardown (never carried between runs, never written to meta-progression). In-memory footprint: 500 × ~40 bytes = ~20 KB — negligible. No truncation needed. If log size ever becomes a concern, truncate oldest entries with a rolling buffer (post-EA consideration, Section K).

### EC-SE13 — Negative Scrap via hypothetical external bug

**Situation:** A bug in downstream code (e.g., a modded or corrupted save) causes `CurrentScrap` to appear as a negative value.

**Resolution:** The system maintains a hard invariant: `CurrentScrap >= 0`. All verbs' Validated phase checks `CurrentScrap >= cost`. If corruption produces a negative value, the next verb call fails all spend preconditions (the `>= 0` minimum holds) and the system asserts at debug-build level. Production build clamps silently to 0 and appends a `CORRUPTION_CLAMP` audit entry. The player's run is not crashed; the exploit surface is zero (a negative value cannot be leveraged into purchases because all checks are unsigned comparisons).

### EC-SE14 — Merchant offer with all cards at `MerchantPrice == 0`

**Situation:** Node Encounter generation produces a Merchant offer where every card has an unset or zero `MerchantPrice` — misauthoring, not gameplay.

**Resolution:** `TryPurchase` rejects each unpriced card with `FailureReason.CardUnpriced` (defensive — per Section C). UI shows the card greyed-out with "Not for sale" overlay. Player can leave the Merchant. A telemetry event `MerchantOfferAllUnpriced` fires at debug-build level to flag the authoring issue for balance pass. Node Encounter GDD must treat `MerchantPrice == 0` as a validation error at SO import, not a runtime possibility — this edge case exists solely as a last-line defense for authoring mistakes.

### EC-SE15 — Stale UI button after reward accept (race condition)

**Situation:** Player clicks "Accept Scrap Reward" on a combat reward screen. The reward resolves. The player then quickly clicks an already-closing dialog's ghost button (UI animation hasn't completed).

**Resolution:** Not an edge case in the domain — single-threaded, single-frame transactions mean the second click either lands before the first's `Logged` (blocked by `TransactionInFlight`) or after (idempotent — the reward button's handler is detached on success). No race condition exists at the Scrap Economy layer. If UI animation allows a visually-active ghost button, the UI layer must detach the handler on the first successful click; Section I (UI) owns the affordance lifecycle.

### Edge-case coverage summary

| Category | Covered |
|----------|---------|
| Failure-path atomicity | EC-SE1 (rollback), EC-SE13 (invariant) |
| Concurrency / reentrancy | EC-SE2 (save), EC-SE5 (double-click), EC-SE15 (UI race) |
| Interface contract gaps | EC-SE8 (Fuel cap — flagged), EC-SE9 (Fuel starvation post-Convert), EC-SE14 (unpriced Merchant) |
| Determinism / save-reload | EC-SE11 (free-valve persistence) |
| Pricing / refund degeneracies | EC-SE3 (Convert residual), EC-SE10 (identical replace) |
| Player-legal zero-op states | EC-SE6 (do-nothing Chopshop), EC-SE4 (free-valve at deck floor) |
| Scale / persistence | EC-SE7 (pity flat), EC-SE12 (log size) |

## Dependencies

### F.1 — Dependency summary

| Category | Count | Systems |
|----------|-------|---------|
| Upstream (approved, retrofit needed) | 3 | Card System, Vehicle & Part, Save & Persistence |
| Upstream (approved, clean contract) | 1 | Node Map |
| Peer (approved, non-interaction) | 1 | Card Combat |
| Downstream / peer (undesigned — forward dep) | 4 | Fuel System, Loot & Reward, Node Encounter, Haven Encounter |

### F.2 — Upstream: approved GDDs (retrofit required)

#### F.2a — Card System (`design/gdd/card-system.md`)

| Flow direction | What | Type |
|---|---|---|
| Scrap Economy reads | `CardDefinitionSO.Rarity`, `CardDefinitionSO.MerchantPrice` | Field read |
| Scrap Economy writes | Deck mutation via `IDeckMutator` on Purge / Purchase | **Exclusive player-initiated caller at map layer** |

**Retrofit required (queued post-approval of this GDD):**

1. Remove `PurgeCost: int` field from `CardDefinitionSO` (was per-rarity 5–50; superseded by flat `GlobalPurgeCost = 30`).
2. Simplify F5 (Purge resolution) to read: `PurgeTransactionCost = (freeValveActive ? 0 : GlobalPurgeCost)`. Remove the rarity-keyed branching.
3. Drop any AC that validates per-card purge pricing. If AC "Common purge < Uncommon purge < Rare purge monotonic" exists, delete.
4. Collapse the per-rarity `PurgeCost` Tuning Knob row into a single `GlobalPurgeCost` row.
5. Preserve OQ7 resolution (`MerchantPrice != GlobalPurgeCost` guard) — this rule survives retrofit.
6. Update Card System Dependencies section to list Scrap Economy as "deck mutator — exclusive player-initiated caller at map layer."

**Reciprocal acknowledgment:** Card System's Dependencies section must list Scrap Economy as both consumer (of `MerchantPrice`, `Rarity`) and mutator (of deck via Purge/Purchase).

#### F.2b — Vehicle & Part (`design/gdd/vehicle-and-part-system.md`)

| Flow direction | What | Type |
|---|---|---|
| Scrap Economy reads | `IVehicleView.Slots[*].{InstalledPart, DamageState, Hp, MaxHp}`, `.ChassisId`, `.InstalledCount`, `.Slots[*].InstalledPart.{StatModifiers, CombatsSurvived}` (Override collision + tenure refund) | Interface read |
| Scrap Economy writes | `IVehicleMutator.InstallPart(slot, part)`, `.RemovePart(slot)`, `.Repair(slot, hp, canReviveOffline: true)` | **Exclusive player-initiated caller** |

**Retrofit required:**

1. Verify `IVehicleView.Slots[slot].InstalledPart` exposes `StatModifiers` read-only (needed for Override-collision detection in `TryInstall` Validated phase, per V&P OQ-VP2 fail-closed resolution). If the exposed surface only returns the `PartDefinitionSO` reference without a flattened `StatModifiers` accessor, add `IReadOnlyList<StatModifier> StatModifiers { get; }` to the view-side.
2. `IVehicleMutator.InstallPart` contract must guarantee: "if target slot is non-Empty, auto-remove existing part atomically; no separate caller call to `RemovePart` required." Confirm V&P documents this atomicity. If not, it's a V&P documentation retrofit.
3. `IVehicleMutator.Repair(slot, hp, canReviveOffline)` — confirm `canReviveOffline=true` is the path that restores `Offline` → `Functional`. Scrap Economy always passes `true` (all Chopshop repairs revive).
4. V&P `InstalledCount` semantics: count of non-Empty slots (0–4). Document in V&P `IVehicleView` spec if not already.
5. **(New — tenure refund, D.6.)** Expose `int CombatsSurvived { get; }` on the per-instance installed-part record accessible via `IVehicleView.Slots[slot].InstalledPart.CombatsSurvived`. Semantics: increments by 1 at the close of every combat in which the part was installed (regardless of damage state outcome); resets to 0 on `InstallPart`; not inherited by a newly-installed part from any prior slot history. Domain: non-negative int. V&P owns the increment logic (fires on combat end, before any post-combat part-reward Install); Scrap Economy only reads.

**Reciprocal acknowledgment:** V&P must list Scrap Economy as the exclusive player-initiated caller of `InstallPart` / `RemovePart` / `Repair` (narrative/event-driven callers are out-of-scope for EA and will need separate design review if/when added). V&P must also list Scrap Economy as the sole documented consumer of `CombatsSurvived` for tenure-refund math (other systems may read it, but the value is designed against Scrap Economy D.6 — changing its increment semantics requires Scrap Economy re-review).

#### F.2c — Save & Persistence (`design/gdd/save-persistence.md`)

| Flow direction | What | Type |
|---|---|---|
| Scrap Economy writes | `ScrapStateDTO` on save | DTO serialization |
| Scrap Economy reads | `ScrapStateDTO` on load | DTO deserialization |
| Scrap Economy exposes | `TransactionInFlight` read property | Precheck hook |

**DTO schema owned by Scrap Economy:**

```csharp
public sealed class ScrapStateDTO {
    public int CurrentScrap;
    public int RunSeed;                      // master run seed (free-valve derivation)
    public long SequenceNumber;              // monotonic transaction counter
    public bool FreeValveConsumedThisVisit;  // visit-scoped flag (EC-SE11)
    public int CurrentVisitNodeIndex;        // for FreeValveConsumed scoping
    public List<ScrapTransactionDTO> TransactionLog;
}
```

**Retrofit required:**

1. Register `ScrapStateDTO` with the `IRunSerializable` registry.
2. Document save-point-after-Logged invariant: Save & Persistence must poll `IScrapEconomy.TransactionInFlight` before writing; if `true`, block/queue the save until cleared (EC-SE2).
3. Register `FreeValveConsumedThisVisit` as a visit-scoped field (reset on `nodeIndex` change).
4. Post-load corruption check: last-entry `TransactionLog[^1].ScrapAfter == CurrentScrap`. If mismatch, Save & Persistence emits a corruption warning.
5. Scrap Economy is **not** a `CombatSerializable` — pure map-layer persistence.

**Reciprocal acknowledgment:** Save & Persistence must list Scrap Economy in its DTO registry and mention the save-after-Logged invariant.

### F.3 — Upstream: approved GDDs (no retrofit)

#### F.3a — Node Map (`design/gdd/node-map.md`)

| Flow direction | What | Type |
|---|---|---|
| Scrap Economy reads | `nodeIndex`, `nodeType` (Chopshop / Merchant) | Context for `Context` field + free-valve seed |
| Scrap Economy reads | `TruckRewardMultiplier` constant (value ≥ 1.25) | Registry constant (applied by Loot & Reward, not here) |
| Scrap Economy writes | Nothing | — |

**Contract holds as-is.** Node Map already exposes `nodeIndex` and `nodeType`; `TruckRewardMultiplier` is in `design/registry/entities.yaml`. No retrofit.

**Separation contract reinforcement:** Scrap Economy satisfies AC-NM55 (no shared pool; Convert verbs are the only bridge). This GDD's Section C and D implement that contract; Node Map's AC-NM55 is the verification gate.

**Reciprocal acknowledgment:** Node Map's Dependencies section already mentions "Scrap Economy owns `CurrentScrap`" per the AC-NM55 language. No amendment needed.

### F.4 — Peer: approved GDDs (non-interaction contract)

#### F.4a — Card Combat (`design/gdd/card-combat.md`)

**The non-interaction.** Card Combat never reads and never writes Scrap / Fuel / any map-layer resource. Scrap Economy is never injected into any Combat-namespace class.

**Enforcement: build-time invariant.** This is not a convention — it is enforced at PR time:

1. A linter rule (added to `tools/ci/scrap-combat-isolation-check.cs` or equivalent) scans every `.cs` file for co-presence of `using Combat.*;` / `namespace Combat.*` **and** any reference to `IScrapEconomy`, `ScrapStateDTO`, or the `ScrapEconomy.*` namespace. Co-presence = build failure.
2. Assembly definition (`.asmdef`) boundary: the Combat assembly may not reference the Scrap Economy assembly. This is the primary structural guard; the linter is belt-and-suspenders.
3. Runtime assertion in `IScrapEconomy` implementation constructor: log and hard-assert if the call site is within a Combat-tagged scene. Debug-build only.

**Justification:** Pillar 4 (Scarcity with Agency) has AC-NM55 (separation contract) as its load-bearing structural test. A runtime-only enforcement means a single bad PR can silently degrade the invariant until playtest surfaces it. Build-time catches it at the author's screen.

**Reciprocal acknowledgment:** Card Combat GDD's F.3 non-interaction row already states "combat never touches `CurrentScrap`, `CurrentFuel`, `StormFrontX`, or `CombatCommitCounter`." Amend Card Combat F.3 to add the build-time invariant enforcement sentence (treated as a minor amendment, not a retrofit — the design decision is unchanged; only the enforcement language is added).

### F.5 — Downstream / peer: undesigned (forward dependencies)

These GDDs do not exist yet. Scrap Economy defines its contract surface now and flags which decisions must be answered before those GDDs ship.

#### F.5a — Fuel System (undesigned) — **hard blocker**

| Flow | What |
|---|---|
| Scrap Economy reads | `IFuelSystem.CurrentFuel`, `IFuelSystem.FuelCap` |
| Scrap Economy writes | `IFuelSystem.AddFuel(int)`, `IFuelSystem.SpendFuel(int)` |
| Scrap Economy provides | `IScrapEconomy.TryConvertFuelToScrap(IFuelSystem fs, int fuelAmount, ...)` |

**[CONFIRM: Fuel System GDD] — hard blocker.** Convert verb cannot ship to production until Fuel System GDD answers:

1. **Overflow behavior on `AddFuel` that exceeds `FuelCap`:** working default from EC-SE8 is option (b) — `TryConvertScrapToFuel` validates `CurrentFuel + FuelOut ≤ FuelCap` at Validated phase and fails with `FailureReason.FuelCapReached`. **This default is provisional.** Fuel System GDD owns the binding decision. If Fuel System specifies silent saturation or partial-Convert clamping, Scrap Economy must retrofit `TryConvertScrapToFuel` to match.
2. **Fuel log ownership on Convert:** Fuel System must emit its own log entry (to satisfy AC-NM55 "both sides log"). Scrap Economy logs the Scrap side; Fuel System logs the Fuel side. Both log entries reference the same `SequenceNumber` — Fuel System GDD must expose a way to propagate the Scrap Economy's sequence number into the Fuel log entry (or vice versa), so the paired entries are joinable in post-run analysis.
3. **Transactional rollback:** if `IFuelSystem.AddFuel` / `SpendFuel` throws during Convert's Commit phase, Scrap Economy rolls back the Scrap mutation (per EC-SE1). Fuel System must guarantee no partial Fuel mutation on throw (either the full delta applied or zero).

**Provisional working contract** (used until Fuel System GDD lands):

```csharp
public interface IFuelSystem {
    int CurrentFuel { get; }
    int FuelCap { get; }
    // Returns true on full success; false if capped/insufficient. Never throws under normal flow.
    bool AddFuel(int amount, out int actualAdded);
    bool SpendFuel(int amount);
}
```

#### F.5b — Loot & Reward (undesigned) — hard blocker for `GrantScrap` calls

| Flow | What |
|---|---|
| Loot & Reward writes | `IScrapEconomy.GrantScrap(int amount, string context)` |

**Contract requirements (flagged for Loot & Reward GDD):**

1. Loot & Reward must apply `TruckRewardMultiplier` (≥ 1.25, per AC-NM54 floor) **before** calling `GrantScrap`. Scrap Economy does not know chassis type; the multiplier is invisible downstream.
2. `context` string format: `"CombatReward[node:X]"`, `"BeaconReward[node:X]"`, `"EventReward[node:X]"`, `"Pity[node:X]"`. Loot & Reward GDD owns the exact string schema; Scrap Economy stores it unparsed.
3. Loot & Reward never calls any `TryX` spend verb. `GrantScrap` is a non-transactional one-way income call (no `TransactionInFlight` guard, no rollback semantics — reward acceptance is terminal).
4. Pity-to-Scrap award: Card System triggers pity; Loot & Reward resolves the payout (`GrantScrap(PityScrapAward, "Pity[node:X]")`). Scrap Economy is not aware of pity logic.

#### F.5c — Node Encounter (Approved 2026-04-23) — retrofit-complete

| Flow | What |
|---|---|
| Node Encounter writes | `IScrapEconomy.TryConvertScrapToFuel / TryConvertFuelToScrap` (EventConvert payload; rate computed caller-side at `EventConvertFavorableRate = 3`) |
| Node Encounter writes | `IScrapEconomy.GrantScrap` (Windfall Scrap payload = 12 flat; Treasure/salvage-fallback payloads routed via L&R) |
| Node Encounter writes | `IScrapEconomy.TryRepair(slot, context, freeRepair: true)` (Rest handler — zero-cost path) |

**Contract notes (finalized per NE Sections C/D/E):**

1. **EventConvert rate:** Event effects invoke `TryConvert*` verbs with caller-specified amounts. Node Encounter pre-computes the favorable rate `Floor(input / EventConvertFavorableRate)` (NE D.4, denominator = 3) before the call — the favorable denominator is NOT stored in Scrap Economy and is NOT exposed via a `rateOverride` parameter. Scrap Economy's `TryConvert*` verbs remain rate-agnostic in signature; the baseline `ScrapPerFuelRate / FuelPerScrapRate = 4` applies only when the caller uses unmodified raw inputs (Chopshop/Merchant path). One-direction-per-EventConvert is structurally enforced by Node Encounter (NE E.7 arbitrage-prevention) — Scrap Economy sees only a single `TryConvert*` call per Event outcome.
2. **Windfall Scrap branch:** 50% of Windfall payloads (NE D.5 seeded coin flip) grant `WindfallScrapGrant = 12` via `GrantScrap(12, "EventWindfall[node:X]")`. Auto-granted — no player verb; no transaction-in-flight semantics (income path).
3. **Rest handler free-repair:** Rest invokes `TryRepair(slot, "RestHandler[node:X]", freeRepair: true, out result)` once per damaged slot if player consents — zero-cost path bypasses Scrap cost calculation and the `InsufficientScrap` failure mode. Transaction-pipeline invariants (state machine → Logged, TransactionLog schema, observer fire) still hold. Audit validator: `freeRepair=true` origin MUST be `"RestHandler[node:X]"` (see D.3 free-repair path). `TrySpend` verb proposal from the former soft-forward-dep note is NO LONGER REQUIRED — Node Encounter never needs arbitrary Scrap-cost event outcomes in MVP scope (confirmed by NE C handler specs).
4. **HandlerActive / TransactionInFlight independence:** `HandlerActive` save-block is Save & Persistence's concern (NE E.2) and is orthogonal to Scrap Economy's `TransactionInFlight` guard. Save blocking during Chopshop-verb transactions (EC-SE2) is unchanged; Save blocking during handler lifecycles is layered above it.
5. **Merchant `CardUnpriced` edge case (EC-SE14):** Node Encounter's Merchant handler MUST validate `MerchantPrice > 0` at SO import time (per NE Merchant contract), not rely on Scrap Economy's runtime `CardUnpriced` defense as the primary gate. Runtime defense remains in place as a safety net.

#### F.5d — Haven Encounter (undesigned) — soft forward dep, Pillar 4 balance requirement

| Flow | What |
|---|---|
| Haven Encounter writes | `IScrapEconomy.TrySpend(int amount, string context, out TransactionResult)` (verb not yet defined — see note below) |

**Contract requirements (flagged for Haven Encounter GDD):**

1. **Terminal Scrap sink required.** Without a Haven sink, late-run Scrap has no use and Pillar 4 (Scarcity with Agency) fails for the run's final node cluster. Haven Encounter must provide tiered purchases (blessings / vault / persistent-upgrade tokens) priced ≥ 100 Scrap per tier floor, with at least 3 tiers so a full-hoarder player (300+ Scrap) can still spend meaningfully.
2. Purchases resolve via a new `TrySpend(int, string, out TransactionResult)` verb (not yet defined in this GDD's facade — will be added as an amendment after Haven Encounter GDD authors the tiered-purchase model). For now: this is an **API gap** flagged in Section K OQ.
3. Cross-run carry: Haven purchases are run-terminal; any cross-run progression produced by a Haven purchase is owned by the meta-progression system (undesigned).

### F.6 — Telemetry / tooling dependencies

| System | What |
|---|---|
| **Telemetry** (undesigned) | Receives `OnTransactionCompleted` events for aggregation. Specific events: `MerchantOfferAllUnpriced` (EC-SE14), `CORRUPTION_CLAMP` (EC-SE13), verb frequency histograms for balance pass. |
| **Run summary screen** (undesigned) | Reads transaction log at run end to produce spend-breakdown visualization. Log discarded on teardown. |

These are scoped out of EA but noted so the telemetry GDD (when written) can reference the event surface.

### F.7 — Dependency criticality matrix

| System | Status | Criticality | Blocks what |
|---|---|---|---|
| Card System retrofit | Queued | **High** | Cannot unify Purge pricing without it |
| V&P retrofit (`StatModifiers` exposure) | Queued | Medium | Override-collision check degrades to looser check without it |
| Save & Persistence retrofit | Queued | **High** | Save / load reliability + EC-SE11 correctness |
| Node Map | Clean | — | — |
| Card Combat (amendment only) | Queued | **High** (Pillar 4 structural) | AC-NM55 enforcement depends on isolation |
| Fuel System GDD | Undesigned | **Hard blocker** for Convert ship | Convert behavior cannot finalize |
| Loot & Reward GDD | Undesigned | **Hard blocker** for income | `GrantScrap` faucet undefined |
| Node Encounter GDD | Undesigned | Soft | Event-driven Convert is additive |
| Haven Encounter GDD | Undesigned | **Medium** (Pillar 4 balance) | No terminal sink = hoarding fails pillar |

## Tuning Knobs

All knobs are defined at Section D defaults; this section is the balance-pass surface. Ranges are "safe" in that values inside the range preserve the designed shape of the economy — values outside may break pillar tests or invariants.

### G.1 — First-class tuning knobs (numeric dials)

These may be adjusted freely during balance passes. Changing them shifts the *feel* of the economy without changing its *structure*.

#### G.1a — Early-run survival (first 1–3 combat nodes)

| Knob | Default | Safe range | Behavioral effect | Section |
|------|---------|-----------|-------------------|---------|
| `StartingScrap[Scout]` | 60 | 40–80 | Raise → Scout has more first-install options; lower → Scout is one repair away from broke on run 1 | D.1 |
| `StartingScrap[Assault]` | 50 | 35–70 | Raise → forgives bad rolls on first Chopshop; lower → first-Chopshop decisions feel terminal | D.1 |
| `StartingScrap[Truck]` | 40 | 30–60 | Raise → weakens "Truck suffers early, thrives late" identity; lower → Truck feels punishing before `TruckRewardMultiplier` compensates | D.1 |
| `InstallBaseCost[Common]` | 10 | 8–15 | Raise → first Common install feels expensive; lower → Commons feel disposable | D.2 |

**Tuning guidance:** If playtests show "run over by node 3" cluster pattern, raise `StartingScrap[chassis]` before raising anything downstream. If "too easy to survive node 1–3," lower `InstallBaseCost[Common]` before cutting starting Scrap — keeping starting money high but making the first purchase cost more preserves decision weight without forcing frugality.

#### G.1b — Mid-run friction (nodes 4–11)

| Knob | Default | Safe range | Behavioral effect | Section |
|------|---------|-----------|-------------------|---------|
| `InstallBaseCost[Uncommon]` | 25 | 20–35 | Raise → Uncommon installs become mid-run highlights; lower → Uncommons feel filler | D.2 |
| `InstallBaseCost[Rare]` | 50 | 40–65 | Raise → Rare install becomes a decisive run moment; lower → Rares feel routine | D.2 |
| `k` (install-count surcharge) | 0.15 | 0.10–0.25 | Raise → replacing parts in a built vehicle hurts more; lower → churn is painless | D.2 |
| `RarityRepairRate[Common]` | 2.0 | 1.5–3.0 | Raise → Common parts are cheap to lose + expensive to keep; lower → attrition is forgiven | D.3 |
| `RarityRepairRate[Uncommon]` | 2.5 | 2.0–4.0 | Raise → Uncommon triage pressure spikes; lower → mid-tier parts are tank-like | D.3 |
| `RarityRepairRate[Rare]` | 3.5 | 2.5–5.0 | Raise → Rare-Offline is catastrophic; lower → Rare parts weather damage freely | D.3 |

**Tuning guidance:** The intended mid-run feel is "every install displaces a repair or a purge." If playtests show "always afford both," raise `RarityRepairRate[*]` first (damage becomes more threatening), then `InstallBaseCost[Uncommon/Rare]`. If "always triaging, never installing," invert — lower repair rates before lowering install costs (repair-heavy runs still let players grow the vehicle).

#### G.1c — Emergency valves (conversion, purge, free-valve)

| Knob | Default | Safe range | Behavioral effect | Section |
|------|---------|-----------|-------------------|---------|
| `GlobalPurgeCost` | 30 | 20–45 | Raise → purge is a rare, deliberate act; lower → purge becomes deck-polish default | D.1 |
| `ScrapPerFuelRate` (Scrap → Fuel) | 4 | 3–6 | Raise → Scrap → Fuel is a last-resort panic button; lower → Fuel conversion becomes a strategy | D.7 |
| `FuelPerScrapRate` (Fuel → Scrap) | 4 | 3–6 | Raise → Fuel → Scrap is categorically a mistake-undo only; lower → players may game route to convert surplus Fuel | D.7 |
| `FreeValveProbability` | 0.33 | 0.20–0.50 | Raise → Chopshops feel gift-like; lower → free purge becomes a memorable event | D.1 |
| `ScrapRefundRate` | 0.40 | 0.25–0.60 | Raise → fresh-install scrap becomes a near-free undo; lower → even fresh mis-installs are punishing to reverse | D.1 |
| `TenureDecayRate` | 0.10 | 0.05–0.20 | Raise → veteran parts refund near-nothing (stronger Pillar 1 defense, weaker emergency-scrap option for veteran-loaded vehicles); lower → tenure barely matters and scrapping regresses toward "pay-in / pay-out" | D.1 |
| `TenureMinMultiplier` | 0.25 | 0.10–0.50 | Raise → floor on veteran refund rises (softens Pillar 1 defense); lower → veteran scrap refund approaches zero (strongest Pillar 1 defense) | D.1 |

**Tuning guidance:** If players consistently convert Fuel → Scrap mid-run, the rate is too generous (floor it at 4 or raise it). If players never convert Scrap → Fuel even when stranded, the rate is too brutal or Fuel is too abundant elsewhere — check Fuel System before moving `ScrapPerFuelRate`. If telemetry shows veteran-part scrap events clustering in low-Scrap runs (players sacrificing history as emergency valve), `TenureMinMultiplier` is the first knob to move — lower it and veteran scrap becomes less attractive, rising to `ScrapPerFuelRate` adjustment as a second-order fix.

#### G.1d — Pity & income

| Knob | Default | Safe range | Behavioral effect | Section |
|------|---------|-----------|-------------------|---------|
| `PityScrapAward` | 40 | 25–60 | Raise → pity feels like a real reward; lower → pity feels like a token consolation | D.1 |
| `BaseMerchantCost[Common]` | 15 | 10–25 | Raise → Merchant Commons are decisions; lower → Merchant Commons are impulse buys | D.5 |
| `BaseMerchantCost[Uncommon]` | 40 | 30–55 | Raise → mid-tier Merchant purchases displace one installed part; lower → too easy to deck-fill | D.5 |
| `BaseMerchantCost[Rare]` | 75 | 60–100 | Raise → a Merchant Rare is a defining run moment; lower → Rare purchases become routine | D.5 |

**Tuning guidance:** Pity award must sit `GlobalPurgeCost < PityScrapAward < InstallBaseCost[Rare]`. At defaults (30 < 40 < 50), holds. Raising `PityScrapAward` above `InstallBaseCost[Rare]` (50) breaks the Section B framing — pity would then fund exactly the category of purchase it's meant to stand in for, collapsing "salvage the wasteland owes you" back into "reward for bad luck."

### G.2 — Structural knobs (gated — changes require pillar re-review)

These are technically tunable, but moving them changes the *shape* of the economy, not just the feel. Don't move these during a balance pass without design-director gate.

| Knob | Current | Gate to move | Why it's gated |
|------|---------|--------------|----------------|
| `InstallBaseCost[rarity]` rarity scaling | 10 / 25 / 50 (2.5× and 5× ratios) | Creative-director / game-designer | Changes the decision-tier structure. Commons/Uncommons/Rares are currently 1:2.5:5. Flattening to 1:2:3 collapses rarity feel; widening to 1:3:7 makes Rares once-per-run. |
| Number of verbs at Chopshop | 5 (Install/Repair/Purge/Scrap/Convert) | Game-designer | Adding a new verb (e.g., "Refuel" separate from Convert) changes Chopshop's cognitive scope. Removing one degrades the "every Chopshop offers the full toolkit" rule. |
| `DeckSize` floor on Purge | 10 (Card System EC7) | Card System GDD | Moving this here is a Card System decision, not a Scrap Economy one. Scrap Economy consumes this value as read-only. |
| `TruckRewardMultiplier` floor | ≥ 1.25 (Node Map registry) | Creative-director + game-designer | Floor is locked by the Sandstorm Chase Pacing verdict (CD Condition 4). Moving below 1.25 regresses that verdict. |
| Free-valve applies to **first Purge only** | Rule-fixed | Game-designer | Applying it to "any purge this visit" changes the gift into a routine. Applying it to all visits dilutes the moment. |
| Convert verbs available at both Chopshop and Merchant | Current | Game-designer + Creative-director | Expanding to "any map node" bleeds Convert into the traversal layer. Contracting to "Chopshop only" removes Merchant's agency. |
| Verb atomicity (compute → verify → mutate) | Locked | Technical-director | Any change (e.g., allowing mid-transaction UI prompts) breaks rollback semantics (EC-SE1) and save-point invariants (EC-SE2). |

### G.3 — Forward-dep knobs (owned by other systems but impact this one)

These knobs live in other GDDs. Listed here because a balance designer tuning Scrap will feel these values immediately.

| Knob | Owner | Default | How it interacts with Scrap |
|------|-------|---------|------------------------------|
| `TruckRewardMultiplier` | Node Map / Loot & Reward | 1.25 (floor) | Multiplies `BaseReward` on combat / elite beacons when Truck. Raises effective Scrap faucet for Truck runs. |
| `FuelBurnMultiplier[chassis]` | V&P (pending) | Scout 0.8 / Assault 1.0 / Truck 1.3 | Higher burn → more Convert pressure mid-run → more Scrap sacrificed to Fuel. |
| `MaxHp[slot, rarity]` | V&P | TBD | Repair costs are linear in damage; changing MaxHp shifts repair cost ceilings. |
| `FuelCap` | Fuel System (undesigned) | TBD | Fuel cap determines Convert Scrap → Fuel headroom (EC-SE8). |
| Pity trigger window | Card System | 40 nodes since last Rare | Controls frequency of `GrantScrap(PityScrapAward)` calls. |
| `MerchantPrice` per card | Card System (authored per-SO) | Per D.5 tier values | Individual card pricing lives on the SO; tier defaults guide authoring but per-card overrides are allowed. |
| `BaseReward` per node type | Loot & Reward (undesigned) | TBD | Direct input to `GrantedAmount` formula in D.9. |

### G.4 — Non-tunable constants (rule-fixed)

These values are not tuning knobs. They are architectural invariants — changing them is equivalent to redesigning the system.

| Constant | Value | Why rule-fixed |
|----------|-------|-----------------|
| Number of chassis slots | 4 (Weapon / Engine / Mobility / Frame) | V&P architectural constant. `InstalledCount` domain is 0–4 by structure. |
| Rounding direction on costs | `Ceiling` | V&P OQ-VP5 convention. All costs round against the player. |
| Rounding direction on refunds / conversions | `Floor` | V&P OQ-VP5 convention. All gains round against the player. |
| Replace-scrap refund | 0 (no refund on `TryInstall` auto-replace) | Prevents install-scrap loop degeneracy (D.6). |
| Transaction state phases | 4 (Proposed / Validated / Committed / Logged) | Atomicity guarantee. No fewer = no rollback. |
| Frame slot may not be Empty by player action | Boolean invariant | V&P OQ-VP3 resolution. Frame=Empty is a run-ending V&P state, not a player-authored state. |
| `CurrentScrap >= 0` | Hard invariant | EC-SE13. Defensive assert; no negative-Scrap gameplay paths exist. |

### G.5 — Tuning-pass checklist

When running a balance pass on Scrap Economy, in order:

1. Run 30–50 test runs with telemetry capturing `OnTransactionCompleted` aggregates.
2. Check the histograms: which verbs fired how often? Typical healthy distribution (EA target) — Install ~20%, Repair ~25%, Purge ~10%, Purchase ~5%, Scrap ~3%, Convert ~5%, Grant (income) ~30%. Convert > 15% suggests Fuel is too scarce; Convert < 2% suggests Fuel is too abundant or rates too punishing.
3. Check end-of-run Scrap hoards: target 20–60 at Haven. >100 = Haven sink is too small or purchases are too rare; <10 = end-game too tight.
4. Tune one axis at a time. Don't move both `StartingScrap` and `InstallBaseCost` in the same pass.
5. After each tuning change, re-run AC-SE3 (Pillar 4 separation test) — a numeric tweak should never require structural re-verification, but surprises happen.

## Visual/Audio Requirements

Scrap Economy is a map-layer and UI-first system. It has no combat visuals and no environmental footprint. This section defines what art, audio, and UX teams must deliver to meet the Player Fantasy and Section C UI contract — not how they should achieve it.

### H.1 — Scrap counter (HUD — persistent)

**Requirement:** the current Scrap value must be visible at all times the player is outside combat. During combat it must be visible but styled as read-only.

| Context | Visual state | Notes |
|---------|--------------|-------|
| Map layer | Active, interactive-adjacent (tooltips, breakdown on hover) | Positioned top-left per project convention; visible across all map, Chopshop, Merchant, reward screens. |
| Combat | Desaturated / locked icon; value still readable | Signals "you cannot spend this right now." Not hidden — seeing your hoard *while* you fight is part of the Pillar 4 tension. |
| Post-combat reward screen | Active with animated delta | Shows the incoming `GrantScrap` amount (e.g., "+15") animating into the total. |
| Run summary screen | Final value displayed prominently with breakdown by verb | Part of end-of-run narrative. |

**Typography and legibility:**

- Scrap counter is the single most re-read number in the game (Section B). Font choice must tolerate 1- to 4-digit values without layout shift. Reserve space for up to 4 digits (run ceiling ~500–550 Scrap per Section D.9).
- Must remain legible at minimum display scale for accessibility (target: readable at 1080p without UI scaling).
- Colorblind-safe: the counter must not rely on color alone to convey state (desaturated during combat = visual hue change acceptable, but also apply an icon treatment or outline).

**Delta animation:**

- Gains animate up (number ticks upward over ~0.3s); losses animate down.
- Animation is cosmetic only; the logical value is already committed before the animation begins (Section C — "Scrap counter updates at the moment of transaction confirmation").
- Skippable: pressing the verb confirm button during an ongoing delta animation cancels the animation and snaps to final value.

### H.2 — Per-verb feedback matrix

Each verb has a distinct confirmation feedback. These are specification asks; exact treatment is UX's call.

| Verb | Visual feedback | Audio feedback | Duration target |
|------|-----------------|----------------|-----------------|
| **Install** | Part slot "fills" with part silhouette; Scrap delta animates down; brief highlight on the installed slot | Mechanical install stamp (satisfying, not triumphant) | 0.4–0.8s |
| **Repair** | Slot HP bar animates to full; damage indicator dissipates; Scrap delta animates down | Wrench/weld soundscape; brief, practical | 0.4–0.6s |
| **Purge** | Card animates out of deck view and disintegrates; Scrap delta (or "FREE" confirmation if free-valve) | Paper-tear-into-flame whoosh; quieter if free-valve | 0.5–0.8s |
| **Purchase** | Card animates into deck view; Scrap delta animates down | Transaction "ka-ching" — single solid tone, not coin-jangle (fits wasteland tone) | 0.4–0.6s |
| **Scrap** | Part silhouette disintegrates from slot; refund Scrap animates into counter | Grinding/dismantling sound; refund sound is deliberately muted (it's a net loss) | 0.5–0.8s |
| **Convert** | Animated "exchange" icon showing input → output; both values visible; both counters animate | Whirring/grinding (emphasizing lossy nature); pitch slightly off-key to feel unfair | 0.6–1.0s |

**Non-negotiables:**

1. **Failure feedback is distinct.** A failed `TryX` (insufficient Scrap, precondition violation) must produce a **negative** cue (buzzer or denial sound, red flash on the cost display) — never silence. Silent failures on a Scrap button would make the game feel broken, not punishing.
2. **No "coin" imagery or audio.** Scrap is industrial salvage, not currency. Sound design avoids coin jangle; icon design avoids coin silhouettes. Bolts, gears, ingots, raw metal chunks — the visual vocabulary is junkyard, not treasury.
3. **No victory sting on expensive purchases.** Buying a Rare card or installing a Rare part is a weighty decision, not a triumph. The audio feedback is "transaction completed" (neutral positive), not "fanfare." Rewards produce fanfare. Spends produce acknowledgment.

### H.3 — Free-valve reveal

The free-valve is one of the few "gift" moments in Scrap Economy and needs a distinctive audiovisual treatment.

| Trigger | Visual | Audio |
|---------|--------|-------|
| **On Chopshop entry with free-valve rolled true** | "FREE" badge appears on the Purge verb's cost display; subtle highlight animation (glow/pulse, not flashy) | Brief positive tone on Chopshop entry when free-valve is live — distinct from the standard Chopshop-entry ambience |
| **On first Purge at free-valve Chopshop** | Badge animates "consumed"; cost display transitions from "FREE" to standard 30 | Standard Purge confirmation audio with an additional subtle "resource saved" layered tone |
| **On second+ Purge at same visit** | Standard Purge treatment | Standard Purge audio |

**Critical:** the free-valve reveal happens **on Chopshop entry**, not on hovering the Purge button. The player should discover the gift as part of arriving, so the moment is about *the Chopshop* ("this one is generous"), not about *the decision* ("I should purge here").

**Not displayed on the map.** The free-valve state must not bleed into the node icon or tooltip at the map layer. Routing *toward* confirmed-free Chopshops would convert surprise into optimization.

### H.4 — Pity-to-Scrap reveal

When the pity counter fires (owned by Loot & Reward per ADR-0006) and hands Scrap instead of a Rare card:

| Visual | Audio |
|--------|-------|
| Reward screen shows the would-have-been card slot replaced with a "salvage token" treatment — framed as the wasteland paying out in scrap because the Rare pool has nothing left to give. | Dry, slightly deflated chord — "consolation that means something," not "jackpot." Distinct from regular `GrantScrap` audio. |

The pity moment is narratively load-bearing (Section B: the moment "lands like salvage should — not a reward, not a consolation"). Audio-director must match the tone — not a triumph, not a failure, just another handful of bolts the road left behind.

### H.5 — Transaction log / run summary

The transaction log is not player-facing during a run (it's internal audit). It becomes player-facing at run end.

**Run summary screen requirements:**

- Aggregate spend breakdown: bar chart or stacked visualization by verb (Install / Repair / Purge / Purchase / Scrap / Income). Convert is broken out separately — see "Domain crossings" below.
- **Three-domain layout (Pillar 4 retrospective legibility).** The Scrap section of the run summary is laid out alongside peer summary blocks for Fuel (owned by Fuel System) and Energy (owned by Card Combat). Three columns or three stacked blocks — UX call — each headed by its domain label: **Build Economy** (Scrap), **Route Economy** (Fuel), **Combat Economy** (Energy). The layout makes it visible at a glance that these three ledgers remained separate: each domain has its own totals, its own peak, its own spend breakdown. Scrap Economy owns only the Build Economy column; Fuel System and Card Combat own their respective columns. If either peer system is not yet wired up at run end (EA: Energy summary may be stub), show the column as empty with "—" rather than collapsing the layout.
- **Domain crossings section.** Convert transactions are grouped under a "Domain crossings" heading distinct from the routine-spend breakdown. Each entry: "Burned N Scrap for M Fuel at node K" (or reverse). The heading reinforces that these were architectural exceptions — moments where the player deliberately crossed a wall — rather than ordinary economy activity. If Convert count is zero, show "— no domain crossings this run" explicitly (legible confirmation that the domains stayed apart).
- Peak Scrap value reached during run.
- Largest single transaction.
- Free-valve count used.
- Pity awards received.

**Visual language:** industrial inventory aesthetic — clipboard / inventory-sheet / typewriter. Not a banking app. The three-domain layout reads as "three separate ledgers bolted to the same clipboard," not as a unified financial statement.

### H.6 — Audio mix priority

| Element | Priority tier | Notes |
|---------|---------------|-------|
| Failure cues | High | Must cut through ambient / music. Player needs to know immediately when a spend fails. |
| Transaction confirmations | Medium-high | Must feel crisp; should not be buried under music ducking. |
| Scrap counter delta tick | Low | Ambient; barely audible on small deltas, more pronounced on large gains. |
| Free-valve entry tone | Medium | One-shot per Chopshop entry; distinct but not attention-demanding. |
| Pity audio | Medium-high | Cuts through reward-screen ambience. |

### H.7 — Accessibility requirements

1. **Audio-independent feedback.** Every verb confirmation must also have a visual cue — the game must be playable without sound.
2. **Reduced motion option.** Scrap delta animation must respect system-level "reduce motion" preferences (fall back to instant snap on reduced-motion setting).
3. **High-contrast counter.** Scrap counter's foreground/background contrast must meet WCAG AA minimum (4.5:1 ratio).
4. **Text scaling support.** UI text for Scrap counter and cost displays must scale with a player-adjustable text size setting (target: 80%–150% scale range).
5. **Colorblind support.** Verb affordability states (affordable / too-expensive / unpriced) must not rely solely on color (green/red). Use icons (✓ / ✗ / —) or text treatments alongside color.

## UI Requirements

This section defines the interaction surface for Scrap Economy. UX owns visual layout and interaction specifics (authored via `/ux-design`); Section I defines the contract UX must satisfy.

### I.1 — HUD (persistent Scrap counter)

**Location:** top-left per project convention. Co-located with Fuel counter (adjacent, not combined — per AC-NM55 separation).

**Elements:**

| Element | Behavior |
|---------|----------|
| Numeric value | Current `CurrentScrap`. Always visible in map layer. Read-only, desaturated during combat. |
| Icon | Scrap-item icon (junkyard salvage aesthetic — not a coin). |
| Hover tooltip (mouse) / long-press (gamepad) | Shows running total's recent delta (e.g., "Last change: +15 from CombatReward[node:4], 2 nodes ago"). Optional — UX may descope if out of scope for EA. |
| Click / interact | No direct interaction — the counter is a status display. All Scrap actions happen at node-specific screens. |

**Layout constraints:**

- Must not overlap with any node-interaction UI. Chopshop / Merchant screens reserve the top-left HUD region as always-visible.
- Must not shift position when other HUD elements appear/disappear (e.g., appearing health-warning indicator should not reflow the Scrap counter).

### I.2 — Chopshop screen

**Entry state:**

- Scrap counter remains in HUD position.
- Vehicle diagram showing all 4 slots with current installed parts + damage states.
- Verb panel showing all 5 verbs available (Install / Repair / Purge / Scrap / Convert).
- Offered parts list (from the Chopshop's seeded inventory).
- Deck view (accessible via tab/toggle — not always-visible, to avoid overwhelming the initial screen).
- **Free-valve badge** on Purge if applicable (per H.3 reveal timing — shown on entry, not on verb hover).
- Leave / exit affordance always visible (EC-SE6: zero-Scrap Chopshop must still be exitable).

**Per-verb interaction flow:**

| Verb | Flow |
|------|------|
| **Install** | Player selects offered part → slot targeting prompt (or auto-targeted if part has single compatible slot) → confirmation dialog showing Scrap cost + "will auto-scrap existing [Part]" if replacing → confirm / cancel. |
| **Repair** | Player selects damaged slot → confirmation showing HP to restore + Scrap cost → confirm / cancel. Disabled/hidden if slot is Functional or Empty. |
| **Purge** | Player opens deck view (or deck is visible) → selects card to purge → confirmation showing cost (or "FREE" if free-valve) → confirm / cancel. Disabled if deck at size 10 (with tooltip "Cannot purge below 10 cards"). |
| **Scrap (part)** | Player selects installed slot (non-Frame) → confirmation showing refund amount → confirm / cancel. Frame slot is not targetable by this verb. |
| **Convert** | Opens Convert sub-screen (see I.5). |

**Affordability states (per verb button):**

| State | Visual | Interaction |
|-------|--------|-------------|
| Affordable | Standard styling | Clickable |
| Insufficient Scrap | Greyed + cost in red / strikethrough | Clickable (shows failure dialog "Insufficient Scrap") |
| Invalid precondition (e.g., Purge at deck=10) | Greyed with icon | Disabled + tooltip explains why |
| `FREE` (free-valve, Purge only) | Highlighted | Clickable |

**All costs visible without interaction.** Section C's mental-budget requirement: the player must see every cost on Chopshop entry, not reveal on click. The verb panel displays the cost for each verb at entry time.

### I.3 — Merchant screen

**Entry state:**

- Scrap counter remains in HUD.
- Offer grid: 3–5 cards from Merchant's seeded offer (Card System / Node Encounter own generation).
- Each card shows rarity, `MerchantPrice`, card text.
- Convert sub-screen accessible via dedicated button (I.5).
- Leave / exit always visible.

**Per-verb flow:**

| Verb | Flow |
|------|------|
| **Purchase** | Player clicks a card → confirmation showing `MerchantPrice` → confirm / cancel. |
| **Convert** | Opens Convert sub-screen (I.5). Available here as well as Chopshop (per Section C user decision). |

**Edge case EC-SE14 (all cards unpriced):** Each card shows "Not for sale" overlay; attempting to click produces a failure dialog "This card has no price — please report this as a bug." Exit affordance remains.

### I.4 — Reward acceptance screens

**Per Section C — mental-budget moments:** when a reward has a choice (Scrap bundle vs. card vs. Fuel cache), **all options are displayed simultaneously with values visible**. Never sequential.

**Requirements:**

1. All reward options on one screen, same z-level.
2. Values shown numerically (e.g., "+25 Scrap", "+6 Fuel", card offer with `MerchantPrice` shown as "Value: 40 Scrap" for mental framing).
3. Accepting one option animates the others fading out (confirming the opportunity cost).
4. Skip / decline all affordance (player can refuse the reward entirely — flagged as a design-team decision; if rejected, reward is forfeit).
5. **Pity award UI (H.4) uses this screen.** When pity fires, the reward screen shows the "salvage token" treatment in place of the would-have-been card slot.

### I.5 — Convert sub-screen

**Layout:**

- Two panels: Scrap → Fuel (left) and Fuel → Scrap (right). Tabs or side-by-side — UX call.
- Each panel has:
  - Current source resource value.
  - Input slider (amount to convert). Slider step is constrained to produce integer output.
  - Live preview of output (per D.7: `FuelOut = Floor(ScrapIn / 4)`).
  - Confirm button showing final delta ("Spend 20 Scrap → Gain 5 Fuel").

**Domain-crossing legibility (Pillar 4 player-felt moment — non-negotiable).**

The Convert sub-screen is the single in-run moment where the player is forced to consciously move value across the three-domain wall the architecture enforces. Section F.4 keeps Combat build-time-isolated from Scrap, but that isolation is invisible in play; Convert is where isolation becomes legible. The UI must surface the domain crossing explicitly, not just the numeric trade:

1. **Each panel is titled by domain, not by resource.** Left panel title: "Build Economy → Route Economy" (subtitle: "Scrap → Fuel"). Right panel title: "Route Economy → Build Economy" (subtitle: "Fuel → Scrap"). The domain name is the primary label; the resource name is secondary. This reinforces that the player is crossing a structural boundary, not just exchanging tokens.
2. **Confirm copy names the domains.** Confirm button text: "Burn 20 Build-economy Scrap for 5 Route-economy Fuel" (or similar tight variant — the word "Burn" is deliberate: conversion is destructive, lossy, and crosses a wall the system normally holds closed). UX may shorten for space, but the domain labels must appear.
3. **A between-domain divider is visually present.** A hard rule / hard edge runs between the source value and the output preview on each panel — not an arrow, not a flow line. The visual vocabulary is "this is being broken down and rebuilt," not "this is flowing from A to B." Audio pairs with this (H.2 Convert row already specifies whirring/grinding + slightly off-key pitch).
4. **The run-summary Convert line reinforces the framing** (see H.5 / I.7): Convert transactions are grouped under a "Domain crossings" heading distinct from Install / Repair / Purge / Scrap, so the player sees retrospectively that these were architectural exceptions, not routine spends.

This is the GDD's mandatory Pillar-4 player-felt moment. Players who never Convert in a run will not experience it directly, but the UI spec still holds: when Convert *does* appear, it must teach the three-domain structure in a single screen.

**Slider constraints:**

- Min: source rate (minimum 1 unit output) — below this, the slider is inactive.
- Max: caller's current source resource balance, OR clamped by destination headroom if `FailureReason.FuelCapReached` is the locked contract (pending Fuel System GDD — see F.5a). UI implementation must be flexible enough to accommodate either behavior.
- Step: 1 unit (not rate-aligned). Slider allows non-divisible amounts (EC-SE3); residual lost to the house is transparent in the preview.

**Before confirmation:**

- Display output preview as the primary value ("→ 5 Fuel") with the input cost as the secondary value ("-20 Scrap"). Domain labels frame both (per list item 2 above).
- If the conversion would reduce Fuel below a threshold that matters for routing (e.g., player doesn't have enough Fuel to reach next station), optionally surface a courtesy warning (EC-SE9) — this is a flag for UX to consider, not a requirement.

### I.6 — Combat UI (read-only)

During combat:

- Scrap counter remains in HUD, desaturated (per H.1).
- No Scrap-affordance buttons anywhere — not even disabled. Completely hidden (per Section C Rule 1).
- Fuel counter (by peer system) similarly handled per Fuel System GDD.

**Non-negotiable:** Combat UI must contain zero references to Scrap-modifying buttons. The presence of even a disabled Scrap button during combat degrades the "spending is a map-layer decision" mental model.

### I.7 — Run summary screen

End-of-run retrospective for Scrap spending. Implements the H.5 **three-domain layout** — the Scrap column ("Build Economy") sits alongside the Fuel ("Route Economy") and Energy ("Combat Economy") columns owned by their respective systems.

**Scrap column (Build Economy) contents:**

- Aggregate breakdown by verb (per H.5), excluding Convert (Convert lives in the shared "Domain crossings" row beneath the three columns).
- Peak Scrap value with node annotation ("Peak: 137 Scrap at node 9").
- Largest single transaction.
- Free-valve count used / total available.
- Pity awards received.
- Scrap remaining at run end (informs Haven purchases).

**Domain-crossings row (shared, beneath the three columns):**

- Convert transaction list (each direction + node + amount). If zero: "— no domain crossings this run" displayed explicitly.

**Interaction:** The three-column layout is the default retrospective view (the "economy" tab of the run summary). Other tabs (deck composition, damage taken, route map) are owned by other systems. This screen is read-only retrospective.

### I.8 — Input handling (keyboard / mouse / gamepad)

Per `technical-preferences.md`: Primary Input = Keyboard/Mouse; Gamepad support = partial (should not gate any feature); no touch.

| Input | Required support |
|-------|------------------|
| Mouse click | All verb interactions, slider drag, confirm/cancel dialogs |
| Keyboard | `Esc` cancels dialogs; tab navigates between verb buttons; `Enter` confirms focused action; arrow keys navigate lists (offered parts, cards) |
| Gamepad | Full parity with keyboard — every interaction reachable without a mouse, including Convert slider (d-pad increments by 1, shoulder buttons step by rate amount) |

**No hover-only interactions** — all hover states must be reachable via keyboard focus or gamepad selection.

### I.9 — Failure UX contracts

Per Section C — every `TryX` failure has a specific `FailureReason`. UI must map each to a user-readable message:

| FailureReason | User-facing message |
|---------------|---------------------|
| `InsufficientScrap` | "Not enough Scrap. (Need X, have Y.)" |
| `InvalidSlot` | "That slot can't accept this part." |
| `OverrideCollision` | "This part would conflict with another installed part's modifier." (V&P OQ-VP2 — fail-closed; wording is a placeholder, UX owns final language) |
| `FrameSlotWouldBeEmpty` | "You can't leave the Frame slot empty." |
| `PartNotInstalled` | "Nothing installed in that slot." |
| `PartNotDamaged` | "This part isn't damaged." |
| `ChassisIncompatible` | "Your chassis can't mount this part." |
| `SlotTypeMismatch` | "That part doesn't fit this slot." |
| `CardNotInDeck` | "Card isn't in your deck." (defensive — shouldn't be visible in practice) |
| `DeckAtMinimumSize` | "Can't purge — deck is at the minimum size of 10." |
| `CardNotInOffer` | "That card isn't available to buy." |
| `CardUnpriced` | "This card isn't for sale. (Please report this as a bug.)" |
| `TransactionInFlight` | Should never surface to UI — the button guard prevents it. If it does, generic "Please wait." |
| `ConversionInsufficientSource` | "Not enough [Scrap/Fuel] to convert." |

**Contract:** every failure produces (1) an audio denial cue, (2) a visual cue on the cost display (red flash / strikethrough), (3) a tooltip or dialog with the above text. Silent failures are forbidden.

### I.10 — Save / load UI touchpoints

- Save indicator must poll `IScrapEconomy.TransactionInFlight` and suppress the "saved" confirmation flash until the flag clears (EC-SE2).
- On load, the Scrap counter displays the loaded value before any verb-reachable UI becomes interactive.
- If post-load corruption is detected (last-entry `ScrapAfter` ≠ deserialized `CurrentScrap`, per F.2c), Save & Persistence surfaces the warning. Scrap Economy accepts the loaded value and continues.

### I.11 — Accessibility UI asks (reinforcement)

Mirrors H.7:

1. All UI states must be reachable without mouse (keyboard + gamepad).
2. All color-coded affordance states must also have icon/text indicators.
3. Text scaling must be supported (80–150%) without layout breakage.
4. Reduced motion: all animations must have instant-snap fallbacks.
5. Focus indicators must be clearly visible for keyboard navigation.

## Acceptance Criteria

Each AC is testable. Gate level:
- **L** = Logic (automated unit test, blocking)
- **I** = Integration (integration test or documented playtest, blocking)
- **V** = Visual/Feel (screenshot + lead sign-off, advisory)
- **U** = UI (manual walkthrough doc or interaction test, advisory)

### J.1 — Formula correctness

| ID | AC | Gate |
|----|----|------|
| AC-SE1 | `InstallCost(rarity, installedCount, totalSlots)` returns the values in the Section D.2 worked-example table for every cell across `totalSlots ∈ {4, 5, 7, 10}` × `rarity ∈ {Common, Rare}` × `fill ∈ {0.0, 0.5, 1.0}`. Test includes boundary `totalSlots=12` to confirm the formula's full domain. | L |
| AC-SE2 | `InstallCost` applies `Ceiling` rounding: `InstallCost(Common, 2, 4) == 13` (`10 × (1 + 0.5 × 0.60) = 13.0` ceil-no-op); `InstallCost(Uncommon, 2, 4) == 33` (`25 × 1.30 = 32.5` → 33); `InstallCost(Rare, 10, 10) == 80` (`50 × 1.60 = 80.0` — full-vehicle cap matches across chassis sizes). | L |
| AC-SE2b | `InstallCost(Rare, 10, 10) ≤ 80` AND `InstallCost(Rare, 7, 7) ≤ 80` AND `InstallCost(Rare, 4, 4) ≤ 80`: a fully-loaded vehicle pays the same surcharge cap regardless of chassis size. Pre-R6 raw-`installedCount` formula failed this AC at Dredge (`10 slots` → 125 Scrap); the normalized formula's ceiling is `Ceiling(InstallBaseCost[Rare] × (1 + kNorm)) = 80`. Closes R6 Blocker 4. | L |
| AC-SE3 | `RepairCost(damage, rarity)` matches Section D.3 worked examples for `(4, Common)`, `(8, Common)`, `(5, Uncommon)`, `(6, Rare)`, `(12, Rare)`. | L |
| AC-SE4 | `RepairCost` rejects `damage == 0` at the UI layer (button disabled); if called programmatically with 0, returns 0 with no transaction logged. | L |
| AC-SE5 | `PurgeTransactionCost == 0` when `freeValveActive == true`; `PurgeTransactionCost == 30` otherwise. Deck-size floor check (`DeckSize > 10`) is independent of free-valve state. | L |
| AC-SE6 | `MerchantPrice(rarity)` returns the Section D.5 defaults (15 / 40 / 75) when a card's SO does not override the tier default. | L |
| AC-SE7 | `ScrapRefund(rarity)` returns 4 / 10 / 20 for Common / Uncommon / Rare at `ScrapRefundRate = 0.40` (static formula — authority: vehicle-and-part-mechanics.md F-VPM2; no tenure component). | L |
| ~~AC-SE7b~~ | **SUPERSEDED (W5)** — Tenure system eliminated. `TenureMultiplier` and `combatsSurvived` no longer factor into ScrapRefund. AC-SE7 covers the static formula. | — |
| ~~AC-SE7c~~ | **SUPERSEDED (W5)** — `TenureMultiplier` formula eliminated. See AC-SE7. | — |
| AC-SE8 | Replace-scrap refund: `TryInstall` that replaces an existing part yields `ScrapRefund == 0` for the replaced part (per D.6 Replace-scrap exception — no refund on auto-replace). | L |
| AC-SE9 | `FuelOut(ScrapIn=7)` returns 1 when `ScrapPerFuelRate == 4` (Floor rounding). Residual 3 Scrap is spent, not refunded. | L |
| AC-SE10 | Both conversion verbs use `Floor`; `ScrapIn == 4 * N` produces exactly N Fuel with zero residual. | L |
| AC-SE11 | `IsFreeValveApplied` is deterministic per (`runSeed`, `nodeIndex`): same seed + node pair always produces the same boolean. | L |
| AC-SE12 | Over 10,000 simulated free-valve rolls with randomized seeds, ratio of true outcomes is `0.33 ± 0.03` at `FreeValveProbability = 0.33`. | L |

### J.2 — Transaction state machine

| ID | AC | Gate |
|----|----|------|
| AC-SE13 | All 6 verbs transition Proposed → Validated → Committed → Logged on happy path. `OnTransactionCompleted` fires once per successful transaction. | L |
| AC-SE14 | Validated-phase failure returns `TransactionResult.Failed` with appropriate `FailureReason`; no state mutation occurs (`CurrentScrap` unchanged; vehicle/deck unchanged). | L |
| AC-SE15 | Committed-phase exception rollback (EC-SE1): injected throw during `IVehicleMutator.InstallPart` inside Commit restores `CurrentScrap` to pre-transaction value, clears `TransactionInFlight`, appends `COMMIT_ROLLBACK` audit entry, returns failure. | L |
| AC-SE16 | `TransactionInFlight == true` between Proposed entry and Logged exit; `false` outside. | L |
| AC-SE17 | Re-entrant `TryX` call during `TransactionInFlight == true` returns `FailureReason.TransactionInFlight` without invoking Validated phase. | L |
| AC-SE18 | `OnTransactionCompleted` handler may call `GrantScrap` synchronously without causing reentrancy (flag is cleared before event fires); may NOT call any `TryX` spend verb synchronously (returns `TransactionInFlight` failure). | L |
| AC-SE19 | Log entry's `ScrapAfter` equals `CurrentScrap` immediately after `Logged` phase completes. | L |
| AC-SE20 | `SequenceNumber` monotonically increments by 1 per transaction within a run; resets to 0 on new run creation. | L |

### J.3 — Verb behavior (per-verb happy path + key failures)

| ID | AC | Gate |
|----|----|------|
| AC-SE21 | `TryInstall` happy path: part installed via `IVehicleMutator.InstallPart`, `CurrentScrap` decreases by `InstallCost`, log entry created with `Verb=Install`. | L |
| AC-SE22 | `TryInstall` with occupied target slot auto-replaces (existing part removed, new part installed, no separate `ScrapDelta` for replaced part). | L |
| AC-SE23 | `TryInstall` with `CurrentScrap < InstallCost` fails `InsufficientScrap`; `TryInstall` with `part.SlotType != slot` fails `SlotTypeMismatch`; `TryInstall` with incompatible chassis fails `ChassisIncompatible`. | L |
| AC-SE24 | `TryRepair` with `DamageState == Functional` fails `PartNotDamaged`; with `Empty` fails `PartNotInstalled`; with sufficient Scrap and `Offline` state, succeeds and restores to Functional via `canReviveOffline: true`. | L |
| AC-SE25 | `TryPurge` with `DeckSize == 10` fails `DeckAtMinimumSize` regardless of `freeValveActive` (AC-SE5 independence). | L |
| AC-SE26 | `TryPurge` with `freeValveActive == true` produces `ScrapDelta == 0`; subsequent purge at same visit produces `ScrapDelta == -30`. | L |
| AC-SE27 | `TryPurchase` with `card.MerchantPrice == 0` fails `CardUnpriced` (defensive). | L |
| AC-SE28 | `TryScrapPart` on Frame slot fails `FrameSlotWouldBeEmpty`. | L |
| AC-SE29 | `TryScrapPart` on Empty slot fails `PartNotInstalled`. | L |
| AC-SE30 | `TryConvertScrapToFuel(20)` with `ScrapPerFuelRate=4` produces `FuelOut=5`; `CurrentScrap` decreases by 20; `CurrentFuel` increases by 5; both sides log. | L |
| AC-SE31 | `TryConvertFuelToScrap(20)` with `FuelPerScrapRate=4` produces `ScrapOut=5`; both sides log. | L |
| AC-SE32 | `GrantScrap(25, "CombatReward[node:4]")` increases `CurrentScrap` by 25; log entry has `Verb=GrantScrap`; no `TransactionInFlight` guard triggers on concurrent `GrantScrap` calls (non-blocking income). | L |

### J.4 — Cross-system integration

| ID | AC | Gate |
|----|----|------|
| AC-SE33 | `TryInstall` is the sole player-initiated caller of `IVehicleMutator.InstallPart` at map layer (verified by call-site audit — no other production code path invokes `InstallPart`). | I |
| AC-SE34 | `TryRepair` is the sole map-layer caller of `IVehicleMutator.Repair`. | I |
| AC-SE35 | `TryPurge` / `TryPurchase` are the sole map-layer callers of `IDeckMutator.Remove` / `.Add`. | I |
| AC-SE36 | `GrantScrap` invoked with `TruckRewardMultiplier` applied externally: Loot & Reward integration test verifies that Truck chassis receives ≥ 1.25× the base-reward amount (AC-NM54 satisfaction test). | I |
| AC-SE37 | Save/load round-trip: save mid-run with `CurrentScrap=X`, `FreeValveConsumedThisVisit=true` at `nodeIndex=N`; reload; verify `CurrentScrap=X` and that visiting `nodeIndex=N` does not grant a fresh free-valve roll. | I |
| AC-SE38 | Post-load corruption check: last-entry `ScrapAfter` mismatch triggers warning; system continues with deserialized value. | I |

### J.5 — AC-NM55 separation contract (Pillar 4 structural)

| ID | AC | Gate |
|----|----|------|
| AC-SE39 | No shared field between `CurrentScrap` and `CurrentFuel` — a code audit verifies they live on separate POCOs with no common parent field. | I |
| AC-SE40 | Convert verbs are the sole path of Scrap ↔ Fuel transfer. A code-coverage test exercising all `IFuelSystem` call sites + all `IScrapEconomy` call sites confirms no non-Convert code path mutates both in the same call chain. | I |
| AC-SE41 | Both sides of a Convert verb emit independent log entries (Scrap log from Scrap Economy; Fuel log from Fuel System). Log entries are joinable by `SequenceNumber`. | I |
| AC-SE42 | **Build-time isolation invariant (F.4a):** attempting to reference `IScrapEconomy`, `ScrapStateDTO`, or the `ScrapEconomy.*` namespace from any file in the Combat assembly produces a build failure. Test: place a reference in a Combat file, verify CI fails. | I |
| AC-SE43 | Runtime debug assertion fires if `IScrapEconomy` is resolved inside a Combat-tagged scene (debug builds only). | I |

### J.6 — UI correctness

| ID | AC | Gate |
|----|----|------|
| AC-SE44 | Scrap counter is visible on all map-layer screens and is desaturated (read-only) during combat. | U |
| AC-SE45 | All 5 Chopshop verbs display cost at entry time (not reveal-on-click). | U |
| AC-SE46 | Insufficient-Scrap verb click produces the failure dialog with user-readable message (I.9 mapping); failure is accompanied by audio denial cue and visual red-flash on cost display. | U |
| AC-SE47 | Deck-at-10 state disables Purge button with tooltip "Cannot purge below 10 cards" regardless of free-valve state. | U |
| AC-SE48 | Free-valve badge appears on Chopshop entry (not on Purge hover); badge animates consumed after first Purge. | U |
| AC-SE49 | Convert sub-screen shows live output preview updating as the input slider moves; final confirmation shows both delta values. | U |
| AC-SE50 | Reward screens show all options simultaneously with numeric values; accepting one animates others fading out. | U |
| AC-SE51 | Keyboard-only navigation reaches every verb, every slider value, every confirm/cancel dialog (gamepad parity verified separately). | U |
| AC-SE52 | Text scaling (80–150%) does not cause Scrap counter or verb-cost display layout breakage. | U |

### J.7 — Edge case coverage (one AC per EC-SE entry)

| ID | AC | Covers | Gate |
|----|----|--------|------|
| AC-SE53 | Commit-phase exception triggers full rollback (covered by AC-SE15). | EC-SE1 | L |
| AC-SE54 | Save request during `TransactionInFlight` blocks until flag clears; no half-state on disk. | EC-SE2 | I |
| AC-SE55 | `TryConvertScrapToFuel(7)` with rate 4 produces `FuelOut=1`, `ScrapIn=7` (residual kept by house). | EC-SE3 | L |
| AC-SE56 | Free-valve at `DeckSize == 10` produces no Purge (blocked by floor); free-valve state is not carried to next visit. | EC-SE4 | L |
| AC-SE57 | Rapid double-click on verb button: second click within same frame fails `TransactionInFlight`; second click in later frame proceeds normally. | EC-SE5 | L |
| AC-SE58 | Zero-Scrap Chopshop with all-Functional slots and deck==10: exit-without-transacting is legal and reachable. | EC-SE6 | U |
| AC-SE59 | Pity award is flat 40 Scrap regardless of would-have-been card rarity. | EC-SE7 | L |
| AC-SE60 | [CONFIRM: Fuel System GDD] Convert overflow behavior matches locked contract (working default: fail at Validated with `FuelCapReached`). | EC-SE8 | I |
| AC-SE61 | Fuel→Scrap that reduces Fuel below next-leg threshold is not prevented by this system (player choice; Fuel starvation is Node Map's consequence). | EC-SE9 | I |
| AC-SE62 | Identical-part replace (same SO, same slot) is a legal transaction that pays full `InstallCost` with no refund; optional UX warning does not block. | EC-SE10 | L |
| AC-SE63 | `FreeValveConsumedThisVisit` persists through save/reload within the same `nodeIndex`. | EC-SE11 | I |
| AC-SE64 | Transaction log of 500+ entries serializes and deserializes without data loss; run-end teardown discards the log (not cross-run persistent). | EC-SE12 | I |
| AC-SE65 | Negative `CurrentScrap` state produced by any path is clamped to 0 with `CORRUPTION_CLAMP` log entry; all spend preconditions still fail (no exploit surface). | EC-SE13 | L |
| AC-SE66 | All-unpriced Merchant offer: each card shows "Not for sale"; exit affordance works; telemetry event `MerchantOfferAllUnpriced` fires in debug builds. | EC-SE14 | I |
| AC-SE67 | UI button handler on reward screen detaches after first successful click; ghost-click during closing animation produces no second transaction. | EC-SE15 | U |

### J.8 — Performance / non-functional

| ID | AC | Gate |
|----|----|------|
| AC-SE68 | All verb calls complete within a single frame at 60 FPS (< 16ms), including Logged phase. | L |
| AC-SE69 | Transaction log memory footprint under 50 KB for a worst-case 1,000-entry run (log is retained in-memory for run summary). | L |
| AC-SE70 | `IScrapEconomy` implementation is a POCO (no `MonoBehaviour` inheritance); verified by type check in unit test. | L |

## Open Questions

Open questions this GDD has explicitly deferred. Each is tagged with its **Trigger** (what would reopen the question), **Owner** (who resolves it), and **Impact** (what it blocks or degrades).

### K.1 — Native to this GDD

- **OQ-SE1 — `TrySpend(int, string, out TransactionResult)` verb not yet defined.** Events (Node Encounter) and Haven purchases both need a way to spend Scrap without coupling to a specific downstream verb. Currently this GDD's facade has no such verb — callers must use a specific `TryX` or compose via `GrantScrap(-N)` (which violates the invariant that `GrantScrap` is positive income).
  - **Trigger:** First Node Encounter event card or Haven tier purchase authored.
  - **Owner:** Game-designer + lead-programmer.
  - **Impact:** Haven Encounter GDD cannot finalize its purchase model without this verb; Node Encounter's "pay N Scrap to X" event template cannot be authored.
  - **Candidate resolution:** Add `TrySpend(int amount, string context, out TransactionResult)` to the facade as the 7th verb. Validated phase checks `CurrentScrap >= amount`; Committed phase debits. Single-purpose, context-string-driven. Low-risk addition; deferred only to avoid speculative API design before concrete callers exist.

- **OQ-SE2 — No repair-tier gate in EA.** Currently all damage states (`Degraded`, `Offline`) are repairable at any Chopshop (Section C, `TryRepair`). Post-launch telemetry may show that `Offline` parts are too easily recovered, trivializing the subsystem-strike tension.
  - **Trigger:** Post-EA playtest telemetry showing `Offline → Functional` repair frequency > 50% of all repairs.
  - **Owner:** Game-designer.
  - **Impact:** If triggered, this introduces a tier gate (e.g., `Offline` requires Tier-2 Chopshop, which requires authoring a Chopshop-tier system). Structural decision — not a simple knob flip.
  - **Candidate resolution:** Two-tier Chopshop model; or `Offline` repair surcharge (e.g., 1.5× `RepairCost`); or require a rare "repair kit" resource drop.

- **OQ-SE3 — Convert rate override for events.** Per user note 2026-04-22, future events may call `TryConvertScrapToFuel` / `TryConvertFuelToScrap` at non-standard rates (e.g., a generous merchant event: 2 S → 1 F). The GDD currently has no mechanism for this.
  - **Trigger:** First event card authored that requires a non-default Convert rate.
  - **Owner:** Game-designer + Node Encounter designer.
  - **Impact:** Without resolution, event designers must either (a) pre-compute delta and call `GrantScrap` / `SpendFuel` directly (bypassing the Convert verbs, which risks logging divergence and breaks the "named verbs only" AC-NM55 spirit); or (b) accept default rates for all events.
  - **Candidate resolution:** Add optional `(int? scrapPerFuelOverride, int? fuelPerScrapOverride)` parameters to the Convert verbs. Override values are range-validated (cannot exceed 1 or go below 10) to prevent authoring errors.

- **OQ-SE4 — Optional reward decline behavior.** Section I.4 mentions "Skip / decline all affordance" for reward screens but flags it as an open design decision. Should the player be able to refuse a reward entirely? If so, is the reward forfeit or deferred?
  - **Trigger:** First playtest where "I don't want any of these rewards" emerges as a user request.
  - **Owner:** Game-designer + Loot & Reward designer.
  - **Impact:** UI decision primarily; affects reward-screen flow. Low structural risk; defer until Loot & Reward authors the reward-screen spec.
  - **Candidate resolution:** Forfeit-only decline (no deferral) — the node is resolved whether or not the player takes the reward. Simplest model; avoids a "deferred rewards" system.

- **OQ-SE5 — Per-card `MerchantPrice` override policy.** D.5 uses tier defaults (15 / 40 / 75) but allows per-card overrides via `CardDefinitionSO.MerchantPrice`. What's the authoring guidance? How far can a card deviate from its tier before it becomes confusing?
  - **Trigger:** Card System balance pass — first set of merchant-priced cards authored.
  - **Owner:** Card System designer + game-designer.
  - **Impact:** Authoring consistency. If every card is ±50% from tier default, "Rare ≈ 75" stops being a useful mental model.
  - **Candidate resolution:** Tier defaults are a guide, not a floor/ceiling. Individual cards may deviate up to ±33% from the tier default (e.g., a Common may be priced 10–20; an Uncommon 27–53; a Rare 50–100). Beyond ±33%, SO import validator flags for design review.

- **OQ-SE6 — Pity-Scrap amount scaling with late-run income.** `PityScrapAward = 40` is a flat value. In a run with heavy Scrap income (e.g., Truck with `TruckRewardMultiplier`), 40 Scrap at node 12 is barely meaningful. In a poor run, 40 at node 3 is a massive gift.
  - **Trigger:** Post-EA telemetry showing pity awards perceived as trivial in late-run contexts (player feedback or low "felt impact" scores).
  - **Owner:** Game-designer.
  - **Impact:** If triggered, pity scales with run progress (`PityScrapAward = 40 + (nodeIndex × 2)`) or with current Scrap hoard (`max(40, CurrentScrap × 0.2)`) — either introduces a nonlinear reward.
  - **Candidate resolution:** Leave flat for EA; reconsider only if telemetry justifies it.

### K.2 — Cross-GDD hard dependencies (must resolve before ship)

- **OQ-SE7 — Fuel cap overflow behavior (EC-SE8).** Convert verb behavior when `CurrentFuel + FuelOut > FuelCap` is provisionally defined (fail at Validated with `FailureReason.FuelCapReached`), but this must be confirmed by Fuel System GDD.
  - **Trigger:** Fuel System GDD authored.
  - **Owner:** Fuel System designer.
  - **Impact:** Convert verb cannot ship to production with confidence until resolved. If Fuel System specifies silent saturation or partial-Convert clamping, this GDD's Section C, D, E, and I.5 must be retrofitted.

- **OQ-SE8 — Fuel-side log ownership for Convert (AC-SE41).** Both sides of a Convert transaction must log independently, and the log entries must be joinable by `SequenceNumber`. Fuel System GDD must accept the Scrap Economy's `SequenceNumber` (or vice versa) to maintain pair linkage.
  - **Trigger:** Fuel System GDD authored.
  - **Owner:** Fuel System designer + Scrap Economy (this GDD).
  - **Impact:** AC-SE41 cannot pass without a cross-system agreement on sequence propagation.

### K.3 — Forward dependencies (undesigned GDDs)

Summary of forward deps already documented in Section F; reprised here for centralized tracking:

- **OQ-SE9 — Haven Encounter GDD must provide terminal Scrap sink** (≥ 100 Scrap/tier × 3 tiers minimum). Without it, late-run hoarding breaks Pillar 4 agency. **Trigger:** Haven Encounter GDD authoring. **Owner:** Haven Encounter designer.
- **OQ-SE10 — Loot & Reward GDD must apply `TruckRewardMultiplier` externally before calling `GrantScrap`.** AC-NM54 floor is 1.25×. **Trigger:** Loot & Reward GDD authoring. **Owner:** Loot & Reward designer.
- **OQ-SE11 — Node Encounter GDD must validate `MerchantPrice > 0` at SO import** (not rely on EC-SE14 runtime defense). **Trigger:** Node Encounter GDD authoring. **Owner:** Node Encounter designer.
- **OQ-SE12 — Telemetry GDD must define the `OnTransactionCompleted` aggregation schema.** Section G.5's tuning-pass histogram targets assume a telemetry pipeline exists. **Trigger:** Telemetry system design (post-EA, likely). **Owner:** Analytics-engineer.

### K.4 — Queued retrofits to approved GDDs

All resolved decisions in this GDD, pending application to the target GDDs. These are not open questions — they are committed changes awaiting execution.

| Retrofit | Target GDD | Scope | Priority |
|----------|------------|-------|----------|
| Remove `PurgeCost` field; simplify F5; drop related ACs; collapse Tuning Knob row | Card System | Structural | **High** — blocks Section D.4 from being the authoritative purge rule |
| Verify / expose `IVehicleView.Slots[slot].InstalledPart.StatModifiers` read-only | V&P | Interface addition | Medium — Override-collision check in `TryInstall` degrades without it |
| ~~Expose `int CombatsSurvived { get; }` on `InstalledPart`~~ | ~~V&P~~ | ~~Interface + state~~ | **REMOVED (W5)** — D.6 tenure system eliminated; `CombatsSurvived` no longer consumed by Scrap Economy. AC-SE7b / AC-SE7c superseded. |
| Register `ScrapStateDTO`; document save-after-Logged invariant; register `FreeValveConsumedThisVisit` | Save & Persistence | DTO + invariant | **High** — blocks AC-SE37, AC-SE54, AC-SE63 |
| Add build-time invariant enforcement sentence to F.3 non-interaction row | Card Combat | Minor amendment | **High** — required for AC-SE42 to pass |

### K.5 — Post-EA revisit list (not blocking)

Items to revisit after EA launch based on telemetry or player feedback:

- Pity-Scrap scaling (OQ-SE6) — revisit if pity awards feel trivial late-run.
- Repair-tier gate (OQ-SE2) — revisit if `Offline → Functional` trivializes subsystem-strike tension.
- Transaction log retention window (Section E, EC-SE12) — revisit if run lengths grow and log size becomes a concern.
- Hover-tooltip recent-delta history (Section I.1) — revisit as accessibility/legibility signal once telemetry shows whether players actually use it.
- Haven terminal sink tier floor (OQ-SE9, ≥100 Scrap/tier) — revisit after first Haven balance pass; may need lower floor if typical run arrives with <200 Scrap.

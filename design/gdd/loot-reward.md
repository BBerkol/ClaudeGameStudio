# Loot & Reward System

> **Status**: In Design
> **Author**: BertanBerkol + /design-system orchestrator
> **Last Updated**: 2026-04-23
> **Implements Pillar**: Pillar 4 (Scarcity with Agency — PRIMARY); Pillar 1 (Vehicle as Character — secondary, part-drop cadence); Pillar 2 (Chassis Identity — secondary, chassis-biased pools); Pillar 3 (Read to Win — secondary, reward choice is a read, not a grind)

## Overview

The **Loot & Reward System** is Wasteland Run's deterministic reward-generation layer. It subscribes to `OnCombatEnded(CombatEndedPayload)` and to node-level reward triggers (beacon rewards, event outcomes), reads SO-authored reward tables keyed on biome, beacon type, and enemy archetype, and outputs a `RewardOffer` list that Post-Combat Flow UI presents to the player. It owns no state, no UI, no presentation — its surface is a single synchronous pure function, `GenerateRewards(context, seed) → RewardOffer[]`, scoped by TD-SYSTEM-BOUNDARY C5. Behind that function is the mechanical delivery of Pillar 4 (Scarcity with Agency): the cadence of Scrap inflow, Fuel inflow, card offers, and part drops that makes every install decision feel tight and every Chopshop visit feel earned.

For the player, the system is invisible but pervasive. It shapes the pacing of the run — how often parts drop, which cards appear at which mastery, when Scrap is generous and when it is lean. It honors the five hard contracts authored upstream: (1) `TruckRewardMultiplier ≥ 1.25` applied pre-`GrantScrap` so Scrap Economy stays chassis-blind; (2) the 2-card offer pipeline with mastery-gated rarity weights and slot-1 primary-family bias at Mastery 1-3; (3) the 8-offer Rare pity counter with `PityScrapAward = 40` fallback when the Rare pool is empty; (4) part drops as offers only (never direct mutation), honoring V&P's "no guaranteed slot replacement within any fixed node count" scarcity rule; (5) seeded determinism via `System.Random` derived from `RunSeed ^ nodeIndex`, so the same run replayed produces the same rewards. Card System, V&P, and Scrap Economy define *what* can be rewarded; Enemy System defines *what the player just did*; Loot & Reward is the function that turns those two inputs into the emotional peak of the short-term loop.

**Scope boundary.** This GDD defines drop-table *generation* only. Reward presentation, animation, ordering, player selection, and skip behavior live in Post-Combat Flow UI (`design/ux/post-combat.md`, undesigned). Reward inflow mutation (the call to `GrantScrap`, `InstallPart`, `AddCard`) is executed by L&R but authored/owned by each target system's mutator contract. L&R is the **pure function**; state lives elsewhere.

## Player Fantasy

**The road keeps its own books.** It gives in bent parts and scavenged scrap, it pays out Rares on a schedule only it knows, and when it has run dry of the thing you wanted it settles the debt in scrap and sends you on. The Loot & Reward fantasy is not about triumph — combat owns triumph, and the engine finally going quiet is where triumph lives. It is about the ten seconds after, when the dust hangs and the wasteland weighs what you've done and pays what it can spare. Two offers, never more. A part the road will let you look at, not keep. A Scrap count ticking up a number you've been watching since the last Chopshop. If you stayed alive long enough and read the world well enough, the road pays out in the thing you needed. If you didn't, it pays out in the thing it has.

**The emotional arc across a full play loop is suspicion → verification → trust.** Early runs, the drops feel random: a Rare you can't use, a card for a family you don't run, scrap when you wanted a part. By run three or four the rhythm surfaces: Rares come in a cadence, not a roulette; Commons pad the pool at low mastery so you don't feel starved; the pity scrap at combat 8 isn't a consolation prize, it's *what the road owed you for staying alive this long*. That is the fantasy L&R exists to deliver — the moment, somewhere around run three, when a player stops suspecting the game of being stingy and starts trusting that the wasteland keeps honest books. Every reward produced by this system should advance that trust, never erode it. A reward that feels arbitrary is a bug against the pillar. A reward that feels *earned through the shape of the run itself* — biome, mastery, combat history, what the payload says you just did — is the pillar working.

**What this pillar asks of every other system that touches rewards.** Scrap Economy anchors what Scrap *means* (one Common install, one-third of a Rare install, a half-repair on a Rare part); L&R's payouts must stay within that vocabulary or the meaning of Scrap collapses. Card System anchors what a card offer *is* (two cards, chassis-filtered, mastery-weighted); L&R delivers the draws without touching the weights. Enemy System anchors what the player just did (`DifficultyScore`, biome, elite); L&R reads that payload as the testimony of the combat and translates it into magnitude. Part drops honor V&P's scarcity contract — no guaranteed same-slot replacement within a fixed window — so a part that lands feels specific, not produced. The fantasy holds only if the whole chain agrees the road is keeping *one* ledger, not four independent ones.

## Detailed Design

### Core Rules

**C.1 Core Function Contract.** Loot & Reward exposes exactly one public entry point. The system is stateless beyond the persisted `LootStateDTO` (C.5) and owns no MonoBehaviours, no UI, no coroutines.

```csharp
public interface ILootRewardSystem
{
    RewardOffer[] GenerateRewards(RewardContext context, int seed);
}

public readonly struct RewardContext
{
    public int BiomeIndex;                    // 1..3
    public BeaconType Beacon;                 // Combat | Elite | Boss | Treasure | Event
    public ArchetypeFamily Family;            // Raider | Patcher | PitPacker | Elite | Boss (gameplay axis — see note below)
    public float DifficultyScore;             // 0..1, from Enemy System payload
    public ChassisId Chassis;                 // for TruckRewardMultiplier + card filter
    public int CardMastery;                   // 1..5, from Card System
    public int NodeIndex;                     // for cooldown decrement + seed mix
    public LootStateDTO State;                // mutable ref — RarePityCounter + cooldowns
}

public abstract record RewardOffer;
public sealed record ScrapGrantOffer(int Amount) : RewardOffer;
public sealed record FuelGrantOffer(int Amount) : RewardOffer;
public sealed record CardOfferPayload(CardDraft[] Draws) : RewardOffer;   // Card System fills
public sealed record PartDropPayload(PartDraft Part) : RewardOffer;
```

The function is **pure w.r.t. `(context, seed)`**: same inputs → same outputs, always. `System.Random(seed)` is the only RNG source. `UnityEngine.Random` is forbidden per `.claude/docs/technical-preferences.md`.

> **Enum axis disambiguation (BLOCKER-3 arbitration, 2026-04-24).** Three independent enum axes exist on `EnemyDefinitionSO`. They must not be conflated:
> - **`ArchetypeFamily`** (this GDD, gameplay axis) — `{Raider, Patcher, PitPacker, Elite, Boss}` — 5 members. Keys reward tables and part-drop pools. Owned here.
> - **`VisualFamily`** (Enemy System H, art axis) — `{Raider, Scavenger, Elite, Boss}` — 4 members. Drives silhouette family / per-family biome skinning. No gameplay effect. Owned by Enemy System.
> - **`SilhouetteClass`** (Enemy System H, size axis) — `{Small, Medium, Large, Boss}` — 4 members. Drives sprite-canvas sizing and the 20%-scale silhouette legibility test. No gameplay effect. Owned by Enemy System.
>
> Every `EnemyDefinitionSO` carries all three fields independently. L&R reads `ArchetypeFamily` only; it never inspects `VisualFamily` or `SilhouetteClass`.

**C.2 Reward Table Structure.** Rewards are authored as ScriptableObjects indexed on the three-axis key `(BiomeIndex, BeaconType, ArchetypeFamily)`. MVP set ≈ 60 SOs (3 biomes × 5 beacons × 4 primary families, pruned for impossible combinations).

```csharp
[CreateAssetMenu(menuName = "Wasteland Run/Reward Table")]
public sealed class RewardTableSO : ScriptableObject
{
    public int BiomeIndex;
    public BeaconType Beacon;
    public ArchetypeFamily Family;
    public RewardEntry[] Entries;
    public WeightModifier[] Modifiers;        // additive overlays
}

[System.Serializable]
public struct RewardEntry
{
    public RewardKind Kind;                   // ScrapGrant | FuelGrant | CardOffer | PartDropOffer
    public int BaseWeight;                    // sampling weight
    public SerializableCondition Gate;        // optional gate (e.g., "only if RarePityCounter >= 6")
}

[System.Serializable]
public struct WeightModifier
{
    public SerializableCondition Condition;   // e.g., "Chassis == Scout && CardMastery >= 4"
    public RewardKind TargetKind;
    public int Delta;                         // signed
}
```

Additive weight resolution:
```
EffectiveWeight(entry) = max(0, entry.BaseWeight
    + Σ modifier.Delta for each modifier
      where modifier.TargetKind == entry.Kind
        AND modifier.Condition(context) == true)
```

**C.3 Generation Pipeline.** `GenerateRewards` executes these 10 steps in order:

1. **Receive trigger** — called synchronously from Card Combat's `OnCombatEnded` OR from Node Encounter's reward trigger 📌 `C3-NE`.
2. **Seed RNG** — `var rng = new System.Random(seed);` where caller passes `RunSeed ^ nodeIndex`.
3. **Table lookup** — resolve `RewardTableSO` by `(Biome, Beacon, Family)`. If no match, fall back to `(Biome, Beacon, Wildcard)`. If still no match, emit `ScrapGrantOffer(BiomeBaseScrap[biome])` and log a content warning.
4. **Apply modifier overlays** — compute `EffectiveWeight` for every entry using additive formula in C.2.
5. **Normalize & sample** — pick 1-N entries (N depends on beacon; Combat=1 Scrap + 1 Card, Elite adds 1 Part roll, Boss substitutes 30 Scrap for card, Treasure=1 Scrap + 1 Fuel + optional Part, Event=Node-Encounter-specified 📌 `C3-NE`).
6. **Compute magnitude + chassis multiplier** — per C.4 sub-formulas, then multiply Scrap by `TruckRewardMultiplier` if `Chassis == Truck` (clamp 1.25).
7. **Pity & cooldown gates**:
   - If sampled entry is `CardOffer` AND `ICardRewardGenerator.Generate` returns `[]` (empty Rare-pool pity signal — Card System reports, L&R interprets) → substitute `ScrapGrantOffer(PityScrapAward=40)`.
   - If sampled entry is `PartDropOffer` AND `State.PartDropCooldown[slot] > 0` for chosen slot → reroll slot once, else substitute `ScrapGrantOffer(BiomeBaseScrap[biome] / 2)`.
8. **Card System delegation** — for CardOffer, call `ICardRewardGenerator.Generate(chassis, mastery, State.RarePityCounter, drawCount, currentDeckCardIds, rng)` and wrap result in `CardOfferPayload`. L&R passes the pity counter as input; Card System reads it to gate Rare draws but never mutates it. L&R never touches rarity weights or slot-1 bias — that is Card System's contract.
9. **Cooldown & pity state update** — decrement all `PartDropCooldown[*]` by 1, set `PartDropCooldown[droppedSlot] = PartDropCooldownNodes (3)` if a part dropped. **L&R writes `RarePityCounter`** based on the draw outcome: increment by 1 if no Rare was drawn this call (or empty pity-substitution fired), reset to 0 if any Rare appeared in the returned drafts. Card System is a read-only consumer of this value (per ADR-0006 Decision §"Pity counter authority"). 📌 `C5-SAVE`.
10. **Assemble `RewardOffer[]`** — emit in canonical order: `[Scrap, Fuel?, Card, Part?]`. Post-Combat Flow UI owns presentation order; L&R guarantees the bundle.

### States and Transitions

**C.4 Magnitude Formulas.**

**C.4.1 Scrap.**
```
BaseScrap(biome, beacon, DS) =
    BiomeBaseScrap[biome]
  + (beacon == Elite ? EliteScrapBonus : 0)
  + DSBonus(DS)

DSBonus(DS) =
    DS < DS_THRESHOLD ? DS_FLOOR_BONUS                                   // +4
    : DS_FLOOR_BONUS + (DS - DS_THRESHOLD) / (1 - DS_THRESHOLD)          // linear
                     * (DS_CEILING_BONUS - DS_FLOOR_BONUS)               // up to +12

FinalScrap = round(BaseScrap) * (Chassis == Truck ? TruckRewardMultiplier : 1.0)
```

Constants: `BiomeBaseScrap = [_, 15, 28, 42]`, `EliteScrapBonus = 18`, `DS_THRESHOLD = 0.40`, `DS_FLOOR_BONUS = 4`, `DS_CEILING_BONUS = 12`, `TruckRewardMultiplier = 1.25`.

Worked examples:
- **Biome-1 Normal combat, DS=0.128, Scout** → `15 + 0 + 4` = **19 Scrap**
- **Biome-3 Elite combat, DS=0.70, Truck** → `(42 + 18 + (4 + 0.5×8)) × 1.25` = `68 × 1.25` = **85 Scrap**

**C.4.2 Fuel** (PROVISIONAL — awaits Fuel System GDD 📌 `C4-FUEL`).
- Gated: `beacon ∈ {Treasure, Event}` only. Combat/Elite/Boss never emit Fuel.
- Magnitude: `FuelGrant = rng.Next(FuelGrantRange.Min, FuelGrantRange.Max + 1)`, default `[1, 3]`.
- Event-node-specific rewards overridden by Node Encounter's event payload 📌 `C4-NE`.

**C.4.3 Card offer.**
- Delegate fully: `CardOfferPayload.Draws = ICardRewardGenerator.Generate(chassis, mastery, State.RarePityCounter, drawCount, currentDeckCardIds, rng)`.
- **Boss-skip rule**: if `beacon == Boss`, L&R substitutes `ScrapGrantOffer(30)` in place of `CardOfferPayload`. Rationale: boss reward flow already includes the chassis-identity beat; a third card would bloat the decision surface.
- L&R writes the pity counter post-draw (step 9 above). Card System reads only.

**C.4.4 Part drop.**
- Sampled entry carries `SlotHint` (Engine | Hull | Mobility | Weapon | Armor | Wildcard) and `RarityHint` (Common | Uncommon | Rare | Wildcard). Slot vocabulary tracks V&P GDD `SlotKind` enum (ADR-0007): `Hull` replaces former `Frame`, `Mobility` replaces former `Tire`, `Armor` replaces former `Auxiliary`.
- When `Wildcard`, sample rarity from `PartRarityWeight = {Common: 60, Uncommon: 30, Rare: 10}`, sample slot uniformly.
- Before emit: check `State.PartDropCooldown[slot]`. If > 0: reroll slot once (uniform over slots with cooldown = 0). If all slots on cooldown: substitute half-biome-base Scrap and log.
- Emit `PartDropPayload(PartDraft)` — V&P owns `InstallPart` mutation 📌 retrofit flag R2 (V&P integration contract still queued; see F.3). Note: per BLOCKER-3 arbitration (2026-04-24), Enemy System's art-axis field is named `VisualFamily` (distinct from this GDD's gameplay-axis `ArchetypeFamily`) and `SilhouetteClass` is a third independent size axis — L&R reads only `ArchetypeFamily`.

### Interactions with Other Systems

**C.5 Persisted State.**

```csharp
[System.Serializable]
public sealed class LootStateDTO
{
    public int RarePityCounter;                          // L&R owns: writes per step 9, reads per step 7
    public Dictionary<SlotId, int> PartDropCooldown;     // L&R owns; decrements each node
}
```

- `LootStateDTO` is owned by Save & Persistence's passive-serializer 📌 `C5-SAVE` (per ADR-0004's "writer owns DTO field" principle and ADR-0006 Decision §"Pity counter authority").
- L&R writes both `PartDropCooldown` and `RarePityCounter`. Card System receives the pity value as a `Generate` input and is forbidden from mutating it.
- Cooldown decrements execute once per `GenerateRewards` call, regardless of whether a part dropped (so empty-combat nodes still age cooldowns). Pity counter increments/resets on every `CardOffer` resolution path (including the empty-pool pity-substitution branch — that branch counts as "no Rare drawn" and increments the counter).

**C.6 Interactions matrix.**

| Upstream / Downstream | Role | Data Flowing | Owner of Contract |
|---|---|---|---|
| Enemy System | upstream | `DifficultyScore`, `ArchetypeFamily`, `Beacon`, biome | Enemy System |
| Card System | downstream (delegation) | `ICardRewardGenerator.Generate(chassis, mastery, rng)` → `CardDraft[]`; empty-Rare pity signal | Card System |
| V&P (Vehicle & Parts) | downstream (offer only) | `PartDraft` (slot + rarity + id); V&P owns `InstallPart` mutation | V&P |
| Scrap Economy | downstream (offer only) | `int` Scrap amount; Scrap Economy owns `GrantScrap` | Scrap Economy |
| Fuel System | downstream (offer only, PROVISIONAL) | `int` Fuel amount | Fuel System 📌 `C6-FUEL` |
| Node Encounter | upstream (alt trigger) | Event-specific `RewardContext` with override payload | Node Encounter 📌 `C6-NE` |
| Save & Persistence | bidirectional | `LootStateDTO` read at run resume, written after each `GenerateRewards` | Save & Persistence 📌 `C6-SAVE` |
| Post-Combat Flow UI | downstream | `RewardOffer[]` bundle | Post-Combat Flow UI |

## Formulas

### D.1 Variable Glossary

| Variable | Type | Range | Source GDD | Notes |
|---|---|---|---|---|
| `biome` | int | 1–3 | Enemy System | 1=Sand Flats, 2=Junk Mesa, 3=Haven Approach |
| `beacon` | enum | `{Combat, Elite, Boss, Treasure, Event}` | Node Map | Beacon type that triggered reward generation |
| `family` | enum | `{Raider, Patcher, PitPacker, Elite, Boss}` | Enemy System | Third axis of `RewardTableSO` lookup key |
| `DS` | float | 0.0–1.0 | Enemy System D.5 | `DifficultyScore` from combat payload |
| `chassis` | enum | `{Scout, Assault, Truck}` | V&P | Determines TruckRewardMultiplier branch + card filter |
| `mastery` | int | 1–5 | Card System | Passed to `ICardRewardGenerator.Generate` |
| `nodeIndex` | int | 0+ | Node Map | Mixed into seed + used for cooldown decrement |
| `RunSeed` | int | any | Save & Persistence | Run-level deterministic seed |
| `seed` | int | any | Computed | `RunSeed ^ nodeIndex` |
| `RarePityCounter` | int | 0–8 | Card System (read-only to L&R) | Consecutive card offers without a Rare |
| `PartDropCooldown[slot]` | int | 0–`PartDropCooldownNodes` | L&R (LootStateDTO) | Nodes before slot re-eligible |
| `BiomeBaseScrap` | int[] | `[_, 15, 28, 42]` | L&R (C.4.1) | Floor Scrap, indexed by biome |
| `EliteScrapBonus` | int | 18 | L&R (C.4.1) | Additive when `beacon == Elite` |
| `DS_THRESHOLD` | float | 0.40 | L&R (C.4.1) | Below = floor only; at/above = linear scaling |
| `DS_FLOOR_BONUS` | int | 4 | L&R (C.4.1) | Flat DSBonus if DS < threshold |
| `DS_CEILING_BONUS` | int | 12 | L&R (C.4.1) | Maximum DSBonus at DS=1.0 |
| `TruckRewardMultiplier` | float | 1.25 | Node Map (registry) | Applied to Scrap before `GrantScrap` |
| `PityScrapAward` | int | 40 | Scrap Economy (registry) | Substituted on empty-Rare pity |
| `PartDropCooldownNodes` | int | 3 | L&R (C.4.4) | Cooldown duration after a slot drops |
| `PartRarityWeight` | int map | `{Common:60, Uncommon:30, Rare:10}` | L&R (C.4.4) | Unnormalized wildcard rarity weights |
| `FuelGrantRange` | int[2] | `[1, 3]` | L&R (C.4.2) | Uniform inclusive range |

### D.2 Scrap Magnitude Formula

**DSBonus (piecewise):**
```
DSBonus(DS) =
    4                                if DS < 0.40
    4 + ((DS - 0.40) / 0.60) * 8    if DS >= 0.40
```
Output range: `[4, 12]`, continuous.

**BaseScrap:**
```
BaseScrap(biome, beacon, DS) =
    BiomeBaseScrap[biome]
    + (beacon == Elite ? EliteScrapBonus : 0)
    + DSBonus(DS)
```

**FinalScrap:**
```
FinalScrap =
    Round(BaseScrap) * TruckRewardMultiplier   if chassis == Truck
    Round(BaseScrap)                           otherwise
```
Clamp `>= 1`. Banker's rounding (C# default).

**Envelope table — `[min, max]` Scrap per (biome × beacon × chassis):**

|  | Combat Non-Truck | Combat Truck | Elite Non-Truck | Elite Truck |
|---|---|---|---|---|
| Biome 1 | [19, 27] | [24, 34] | [37, 45] | [46, 56] |
| Biome 2 | [32, 40] | [40, 50] | [50, 58] | [63, 73] |
| Biome 3 | [46, 54] | [58, 68] | [64, 72] | [80, 90] |

Boss: flat 30. Treasure/Event: Combat formula (no EliteScrapBonus).
Absolute min = **19** (Biome-1 Combat non-Truck DS<0.40). Absolute max = **90** (Biome-3 Elite Truck DS=1.0).

### D.3 DSBonus Coarse Design Table

| DS Band | DSBonus | Computed | Design-Intent Label |
|---|---|---|---|
| [0.00, 0.40) | 4 | 4 (floor) | Easy / trivial — floor only, no scale |
| 0.40 | 4 | 4.0 | Threshold — first tick of linear region |
| 0.50 | ~5.3 | 4 + (0.10/0.60)×8 | Competent — enemy posed a real threat |
| 0.70 | 8 | 4 + (0.30/0.60)×8 | Challenging — high DPT or late Enrage |
| 0.85 | ~10 | 4 + (0.45/0.60)×8 | Brutal — near-maximum enemy profile |
| 1.00 | 12 | 4 + (0.60/0.60)×8 (ceiling) | Maximum — theoretical peak |

Intent: below-threshold band is intentionally wide (40% of DS space) so most early-biome weak-archetype combats pay the same floor — reward range stays legible. Top 15% (0.85–1.0) reserved for Elite + boss-tier.

### D.4 Fuel Grant Formula (PROVISIONAL)

📌 `C4-FUEL` — Fuel System GDD undesigned.

```
FuelGrant = rng.Next(FuelGrantRange.Min, FuelGrantRange.Max + 1)
          = rng.Next(1, 4)   // returns [1, 3] inclusive
```

**Beacon gate (hard rule, not provisional):** `beacon ∈ {Treasure, Event}` only — enforced at SO authoring time (Combat/Elite/Boss tables have no Fuel entry). **Event override:** Node Encounter payload may supply `FuelGrantOverride` that replaces the `rng.Next` draw entirely 📌 `C4-NE`.

### D.5 Part-Drop Sampling Formulas

**Rarity (wildcard):**
```
TotalRarityWeight = 100
roll = rng.Next(0, 100)
rarity = Common   if roll < 60
         Uncommon if roll < 90
         Rare     if roll >= 90
```
P(Common)=0.60, P(Uncommon)=0.30, P(Rare)=0.10. Expected rarity-weight per drop = 1.50.

**Slot (wildcard, uniform):**
```
eligibleSlots = [s for s in {Engine,Hull,Mobility,Weapon,Armor}
                 where PartDropCooldown[s] == 0]
slot = eligibleSlots[rng.Next(0, eligibleSlots.Count)]
```
P(any slot) = `1/|eligibleSlots|`. All-eligible: 0.20 each.

**Cooldown fallback:**
```
if eligibleSlots.Count == 0:
    emit ScrapGrantOffer(BiomeBaseScrap[biome] / 2)
    log("PartDrop: all slots on cooldown — fallback Scrap")
    return
```
Under default tuning (~17% of rewards being part offers, cooldown=3), expected fallback rate < 1%. Safety net, not a design-intended path.

### D.6 Additive WeightModifier Worked Example

**Setup:** Biome-2 Elite Raider table.

| Entry | Kind | BaseWeight |
|---|---|---|
| A | ScrapGrant | 40 |
| B | CardOffer | 50 |
| C | PartDropOffer | 20 |

**Modifiers:**
- M1: `Chassis==Scout && CardMastery>=4` → CardOffer `+15`
- M2: `DS >= 0.5` → PartDropOffer `+10`

**Context:** Scout, Mastery=4, DS=0.62. Both fire.

```
EffectiveWeight(ScrapGrant)    = max(0, 40 + 0)  = 40
EffectiveWeight(CardOffer)     = max(0, 50 + 15) = 65
EffectiveWeight(PartDropOffer) = max(0, 20 + 10) = 30
Total                                            = 135
```

| Entry | EffectiveWeight | P(after) | P(base) |
|---|---|---|---|
| ScrapGrant | 40 | 29.6% | 36.4% |
| CardOffer | 65 | 48.1% | 45.5% |
| PartDropOffer | 30 | 22.2% | 18.2% |

Intent: M1 (Scout + mastery) nudges cards up — chassis identity contract. M2 (high DS) nudges parts up — earned scarcity.

### D.7 Gold-Standard Trace — Patch Rider (Single `GenerateRewards`)

**Fixed inputs:**

| Input | Value |
|---|---|
| `RunSeed` | `0xCAFEBABE` |
| `nodeIndex` | `7` |
| `seed` | `0xCAFEBAB9` |
| `biome` | `1` |
| `beacon` | `Combat` |
| `family` | `Raider` |
| `DS` | `0.128` |
| `chassis` | `Scout` |
| `CardMastery` | `4` |
| `PartDropCooldown` | `{Engine:0, Hull:2, Mobility:0, Weapon:0, Armor:1}` |
| `RarePityCounter` | `3` |

**Step 1 — Trigger.** `OnCombatEnded` fires synchronously. Caller builds `RewardContext` from payload + run state. `GenerateRewards(context, 0xCAFEBAB9)` called.

**Step 2 — Seed.** `var rng = new System.Random(0xCAFEBAB9);`

**Step 3 — Table lookup.** Key `(Biome=1, Combat, Raider)` → `"Biome1_Combat_Raider"` SO. Biome-1 tables omit PartDropOffer (parts scarce in Sand Flats — design call):

| Kind | BaseWeight |
|---|---|
| ScrapGrant | 50 |
| CardOffer | 50 |

**Step 4 — Modifier overlays.** M1: `Chassis==Scout && CardMastery>=4` → CardOffer `+10`. Context satisfies.
```
EffectiveWeight(ScrapGrant) = 50
EffectiveWeight(CardOffer)  = 60
Total                       = 110
```

**Step 5 — Sample.** P(Scrap)=45.5%, P(Card)=54.5%. Roll `r1 ∈ [0,109]` → `72` → `>= 50` → **CardOffer primary**. Combat beacon emits Scrap + primary-roll per C.3 step 5, so both ScrapGrant and CardOffer proceed. *(Rolls illustrative — trace demonstrates pipeline flow, not byte-exact `System.Random` output.)*

**Step 6 — Magnitude.**
```
DSBonus(0.128): DS < 0.40 → DSBonus = 4
BaseScrap = 15 + 0 + 4 = 19
chassis == Scout → no multiplier
FinalScrap = 19
```

**Step 7 — Pity & cooldown.** `RarePityCounter=3 < 8` → no pity substitution. No PartDropOffer sampled → cooldown gate not reached.

**Step 8 — Card System delegation.** `ICardRewardGenerator.Generate(Scout, 4, rng)` returns `CardDraft[2]`: one Common Scout card (e.g., `Patch_Weld`), one Uncommon Scout card (e.g., `Scavenge_Run`). L&R wraps → `CardOfferPayload(draws)`.

**Step 9 — State update.** Decrement all cooldowns by 1:
```
Engine:   0 → 0   (floor)
Hull:     2 → 1
Mobility: 0 → 0
Weapon:   0 → 0
Armor:    1 → 0
```
No drop → no new cooldown set. `RarePityCounter` untouched (Card System owns writes). Persist `LootStateDTO` 📌 `C5-SAVE`.

**Step 10 — Assemble.**
```
RewardOffer[] = [
    ScrapGrantOffer(Amount: 19),
    CardOfferPayload(Draws: [Patch_Weld, Scavenge_Run])
]
```

**Result:** **19 Scrap + 2 Scout-filtered cards.** Post-Combat Flow UI animates Scrap first, then card-choice. Hull cooldown now 1, Armor now 0. `RarePityCounter` → 4 on Card System's side if neither draft was Rare.

## Edge Cases

The edge cases below cover every degenerate state the Loot & Reward system can enter. Each is fully specified so an engineer can implement a deterministic outcome and a QA engineer can verify it without ambiguity. L&R owns its fallback responses in full — no cross-system retrofit is required unless a new contract is implied on another GDD, flagged explicitly below.

Story type: **Logic**. Output location: `tests/unit/loot-reward/`. Gate level: **BLOCKING** for all automated tests listed in Detection fields.

### EC-LR1 — No Matching Table AND No Wildcard Fallback

**Condition:** Step 3 of `GenerateRewards` (see C.3 step 3) attempts a primary lookup on key `(BiomeIndex, Beacon, Family)` and finds no matching `RewardTableSO`. The wildcard lookup `(BiomeIndex, Beacon, Wildcard)` is then attempted and also finds no match.

**Response:** L&R emits exactly `[ScrapGrantOffer(BiomeBaseScrap[biome])]` — a single Scrap offer at the raw biome floor with no DSBonus, no multiplier, no card, no part. A content warning is logged: `"LootReward: no RewardTableSO found for ({BiomeIndex}, {Beacon}, {Family}) and no wildcard fallback — emitting floor Scrap only."` The pipeline does not throw. Step 9 and step 10 still execute on the floor-only result so `LootStateDTO` remains consistent.

**Salvage-fallback structural invariant (retrofit 2026-04-23):** `GenerateRewards` is contractually guaranteed to return a non-empty `RewardOffer[]` on every successful call — the floor-Scrap fallback specified above is the universal tail of every degenerate path (EC-LR1, EC-LR2, EC-LR3, EC-LR6, EC-LR7 all re-converge here or to half-floor Scrap). This guarantee is load-bearing for Node Encounter consumers: **Merchant, Chopshop, and Event-Treasure handlers depend on `RewardOffer[].Length >= 1` as a structural invariant** and are not required to handle empty-array cases in their commit paths. NE handlers that request rewards via L&R's canonical entry point will always receive at least one offer (minimally `[ScrapGrantOffer(BiomeBaseScrap[biome])]`); handlers that observe a truly empty array must treat it as a contract violation (exception, not fallback logic). The downgrade order for NE consumers is therefore: intended reward bundle → half-floor Scrap (EC-LR3/EC-LR7 paths) → floor Scrap (EC-LR1/EC-LR2/EC-LR6 paths) — never empty.

**Detection:** Unit test `LootReward_NoTable_NoWildcard_EmitsFloorScrap`. Additional contract test `LootReward_GenerateRewards_NeverReturnsEmpty` asserts that across a generated matrix of `(biome, beacon, family)` inputs — including all malformed/empty-table cases — the returned array length is always `>= 1`.

**Test:** Assert `result == [ScrapGrantOffer(BiomeBaseScrap[biome])]`, length 1, warning logged, no exception.

### EC-LR2 — All EffectiveWeights Zero After Modifier Overlays

**Condition:** A `RewardTableSO` is found. Step 4 applies modifiers; every entry's `EffectiveWeight = max(0, BaseWeight + ΣDeltas) == 0`. Total samplable weight is zero. AR-LR2 prohibits this via authoring but the engine must handle it.

**Response:** L&R emits `[ScrapGrantOffer(BiomeBaseScrap[biome])]` — same floor fallback as EC-LR1 — with a distinct warning: `"LootReward: all EffectiveWeights are zero in table '{tableName}' after modifier overlays — emitting floor Scrap. Verify WeightModifier Deltas against AR-LR2."` Cooldown decrements proceed normally.

**Detection:** Unit test `LootReward_AllWeightsZero_EmitsFloorScrap`.

**Test:** Assert floor Scrap emitted, AR-LR2 warning, cooldowns aged, no exception.

### EC-LR3 — FuelGrant Entry in a Non-Treasure/Non-Event Table (Authoring Error)

**Condition:** A `RewardTableSO` with `Beacon ∈ {Combat, Elite, Boss}` contains a `RewardEntry` with `Kind = FuelGrant`. AR-LR3 prohibits this; entry reaches runtime because SO validator was bypassed.

**Response:** At step 5, if the `FuelGrant` entry is sampled, L&R detects the beacon mismatch and logs: `"LootReward: FuelGrant entry sampled from a {Beacon} table — authoring violation (AR-LR3). Substituting ScrapGrantOffer(BiomeBaseScrap[biome] / 2)."` Fuel offer is replaced with half-floor Scrap. `IFuelSystem.GrantFuel` is never called. If the entry is NOT sampled, pipeline proceeds normally with no log.

**Detection:** Unit test `LootReward_FuelGrantInCombatTable_Substituted` + SO import validator test.

**Test:** Assert no `FuelGrantOffer` in output, half-floor Scrap, AR-LR3 error logged.

### EC-LR4 — LootStateDTO Missing on Run Resume

**Condition:** `context.State == null`. Occurs on new run start before Save wrote initial DTO, or on resume from pre-schema save file 📌 `C5-SAVE`.

**Response:** L&R detects null as first operation, constructs default DTO: `RarePityCounter = 0`, `PartDropCooldown = { all slots: 0 }`. Synthetic DTO used for the call, written at step 9 as normal. Warning: `"LootReward: LootStateDTO was null — initialized to defaults. Verify Save & Persistence wrote the initial DTO on run start."` Pipeline does not throw.

**Detection:** Unit test `LootReward_NullState_InitializesDefaults`.

**Test:** Assert no throw, default DTO constructed, warning logged, output non-empty.

### EC-LR5 — PartDropCooldown Contains a Key for a Removed Slot

**Condition:** `PartDropCooldown` deserialized from a save written when a slot existed that has since been removed from V&P's slot registry. Contains `{ ..., DeprecatedSlot: 2 }`.

**Response:** At step 9 decrement loop, L&R encounters unknown key. Skips decrement and logs: `"LootReward: PartDropCooldown contains unknown SlotId '{key}' — skipped. Save schema may be ahead of current build. Prune on next full save."` Unknown key left in dictionary (Save owns schema migration per 📌 `C5-SAVE`). Known slots decrement normally. Forward-compatible: if slot is re-added, stale cooldown resumes meaningful decrement.

**Detection:** Unit test `LootReward_StaleSlotKey_Skipped`.

**Test:** Assert known slots decremented, unknown key preserved, warning logged, no exception.

### EC-LR6 — ICardRewardGenerator.Generate Returns Null or Empty

**Condition:** At step 8, Card System returns `null` or empty `CardDraft[]`. Contract violation.

**Response:** L&R substitutes `ScrapGrantOffer(PityScrapAward=40)` — same value as empty-Rare pity path (step 7). Error logged: `"LootReward: ICardRewardGenerator.Generate returned null/empty for (chassis={chassis}, mastery={mastery}). Substituting PityScrapAward={PityScrapAward}. Investigate Card System."` `CardOfferPayload` dropped from bundle. `RarePityCounter` NOT incremented by L&R (Card System owns writes; L&R has no knowledge of whether generator failure should count against pity).

📌 `EC-LR6-card-null-contract` (*optional retrofit*) — Card System GDD to specify `Generate` never returns null/empty for valid inputs.

**Detection:** Unit tests `LootReward_CardGeneratorReturnsNull_SubstitutesPityScrap` and `LootReward_CardGeneratorReturnsEmpty_SubstitutesPityScrap`.

**Test:** Assert `ScrapGrantOffer(40)` in output, no card payload, error logged.

### EC-LR7 — V&P PartCatalog Has No Parts Matching Sampled (Slot, Rarity)

**Condition:** Step 5 samples `PartDropOffer`. L&R calls `IPartCatalog.GetParts(slot, rarity)` and receives empty collection.

**Response:** Substitute `ScrapGrantOffer(BiomeBaseScrap[biome] / 2)` — same vocabulary as all-slots-cooldown fallback in D.5. Log: `"LootReward: IPartCatalog returned no parts for (slot={slot}, rarity={rarity}). Substituting half-floor Scrap. Verify V&P catalog coverage against sampled rarity weights."` `PartDropCooldown[slot]` NOT set (no drop occurred) — cooldown for would-be slot is not penalized.

📌 `EC-LR7-vp-catalog-gap` (*optional retrofit*) — V&P GDD to document `IPartCatalog.GetParts` may return empty and L&R's fallback-to-Scrap response.

**Detection:** Unit test `LootReward_PartCatalogEmpty_SubstitutesHalfFloorScrap`.

**Test:** Assert half-floor Scrap substituted, no part payload, slot cooldown not set, warning logged.

### EC-LR8 — Save & Persistence Write Fails Mid-GenerateRewards

**Condition:** Step 9 persist call throws or returns failure (disk full, permissions, I/O timeout).

**Response:** L&R catches the exception at the persistence boundary. Does NOT re-throw — save failure must not prevent player from receiving reward. Assembled `RewardOffer[]` returned to caller as normal. In-memory `LootStateDTO` retains updated values for session duration. Critical error logged: `"LootReward: LootStateDTO persist failed — {exceptionMessage}. In-memory state intact; rewards delivered. Next save attempt will re-persist current state."` Error surfaced to UI error reporting layer (not Post-Combat Flow UI — separate error banner).

**Detection:** Unit test `LootReward_PersistFails_RewardsStillDelivered`.

**Test:** Assert no propagated exception, rewards non-null, in-memory DTO consistent, error logged.

### EC-LR9 — DifficultyScore at Exact Boundary Values (0.0 and 1.0)

**Condition:** `DifficultyScore` is exactly `0.0f` or `1.0f`. Boundary of DSBonus piecewise function.

**Response:** For `DS=0.0`: `0.0 < 0.40` → `DSBonus = 4`. For `DS=1.0`: `1.0 >= 0.40` → `DSBonus = 4 + (0.60/0.60)*8 = 12`. Denominator `(1 - DS_THRESHOLD) = 0.60` is a compile-time constant, never zero. No NaN, no infinity, no overflow. Matches D.2 envelope table (Biome-3 Elite Truck DS=1.0 = 90 Scrap).

**Detection:** Unit tests `LootReward_DSBonus_BoundaryZero_ReturnsFloor`, `LootReward_DSBonus_BoundaryOne_ReturnsCeiling`; integration tests `LootReward_FinalScrap_DS0_Biome1_Combat_Scout` (expect 19), `LootReward_FinalScrap_DS1_Biome3_Elite_Truck` (expect 90).

**Test:** Four tests matching D.2 envelope values; no NaN, no exception.

### EC-LR10 — Chassis Swap Mid-Run (Chopshop Scout-to-Truck)

**Condition:** Player swaps chassis at Chopshop mid-run. `context.Chassis` is now `Truck` for subsequent calls.

**Response:** `GenerateRewards` is pure — each call reads `context.Chassis` fresh. `TruckRewardMultiplier` applies if and only if `context.Chassis == Truck` at call time. Prior Scout rewards are immutable (L&R has no retroactive mutation path — Scrap Economy owns `GrantScrap`). First call after swap uses Truck multiplier; prior calls were correct as Scout. Card offers also shift: `Generate` receives `chassis=Truck` on next call. `LootStateDTO` has no chassis field — no DTO migration on swap.

**Detection:** Unit test `LootReward_ChassisSwap_TruckMultiplierAppliesNextCall`.

**Test:** Call Scout first (capture Scrap); call Truck with same other inputs; assert Scrap × 1.25 (rounded) on Truck call; assert `Generate` received correct chassis per call; no retroactive modification.

### Edge Case Coverage Summary

| EC # | Surface | Condition Summary | Response |
|---|---|---|---|
| EC-LR1 | Empty/malformed table | No SO match, no wildcard | Floor Scrap + warning |
| EC-LR2 | Empty/malformed table | All EffectiveWeights zero | Floor Scrap + warning |
| EC-LR3 | Empty/malformed table | FuelGrant in Combat/Elite/Boss | Half-floor Scrap substitution |
| EC-LR4 | State desync | LootStateDTO null | Default DTO + warning |
| EC-LR5 | State desync | PartDropCooldown unknown slot | Skip decrement + warning |
| EC-LR6 | Cross-system failure | Card returns null/empty | PityScrapAward(40) + error |
| EC-LR7 | Cross-system failure | PartCatalog empty for (slot,rarity) | Half-floor Scrap + cooldown skip |
| EC-LR8 | Cross-system failure | Save write fails | Deliver rewards + error + in-memory intact |
| EC-LR9 | Boundary state | DS = 0.0 or 1.0 | Matches D.2 envelope; no NaN |
| EC-LR10 | Boundary state | Chassis swap mid-run | Multiplier applies next call only |

### Authoring Rules

**AR-LR1 — Every Table Must Have At Least One Entry with BaseWeight > 0.**
Rule: Before modifiers apply, every `RewardTableSO` must contain at least one `RewardEntry` where `BaseWeight > 0`.
Why: Zero-only `BaseWeight` tables guarantee EC-LR2 under any modifier context. No valid production use.
Enforcement: SO import validator rejects on save with error: `"RewardTableSO '{name}': all BaseWeights are zero or negative — at least one entry must have BaseWeight > 0 (AR-LR1)."`

**AR-LR2 — WeightModifier.Delta Must Not Collapse All Entries to Zero.**
Rule: A negative `Delta` may suppress a reward type conditionally, but no combination of modifiers on a table may drive every entry's `EffectiveWeight` to zero across all reachable `RewardContext` values. Practical rule: a negative Delta's absolute magnitude should not exceed the entry's `BaseWeight` unless the intent is full conditional suppression AND the table satisfies AR-LR1 in that context via other entries.
Why: Unbounded negative Deltas produce silent probability collapses that reach players with no warning unless the SO validator catches them. Constraining magnitude keeps the system legible.
Enforcement: SO import validator computes worst-case effective weight per entry using statically-evaluable conditions. Runtime EC-LR2 fallback is the safety net.

**AR-LR3 — FuelGrant Entries Only in {Treasure, Event} Tables.**
Rule: No `RewardTableSO` with `Beacon ∈ {Combat, Elite, Boss}` may contain a `RewardEntry` with `Kind = FuelGrant`.
Why: Fuel is a traversal resource, not a combat reward. Mixing into combat violates Pillar 4 scarcity and would let the player refuel by fighting, reducing strategic tension. Beacon gate on Fuel is a hard rule (C.4.2).
Enforcement: SO import validator rejects with error: `"RewardTableSO '{name}': FuelGrant entry found in {Beacon} table — only Treasure and Event tables may contain Fuel rewards (AR-LR3)."` Runtime EC-LR3 is the fallback.

**AR-LR4 — RewardEntry.Gate Conditions Must Be Pure Functions of RewardContext.**
Rule: `SerializableCondition` implementations on `RewardEntry.Gate` must evaluate exclusively from `RewardContext` fields (including `State`). Forbidden reads: `UnityEngine.Random`, `DateTime`, `Time.time`, `Time.frameCount`, `PlayerPrefs`, any `MonoBehaviour` state, static mutable fields outside `RewardContext`.
Why: `GenerateRewards` is a pure function of `(context, seed)` (C.1 contract). External mutable state breaks seeded-run replay determinism.
Enforcement: (1) Code review gate for any new `SerializableCondition` subclass. (2) Unit test template: every subclass must have a test asserting `Evaluate(context)` is idempotent on repeat calls. (3) XML doc comment on the base class warning "must be pure functions of RewardContext (AR-LR4)."

**AR-LR5 — RewardTableSO.ArchetypeFamily Must Match the Family That Beacon Spawns.**
Rule: A table with `Family = Raider` must only register for beacons that spawn Raider enemies. The lookup key `(biome, Combat, Patcher)` must resolve to a Patcher table, never a Raider one.
Why: The three-axis key exists specifically to scope rewards to enemy profile. Mismatched pairings break the "the road reads what you did" fantasy — reward pool reflects a combat that did not happen.
Enforcement: Editor-time inspector cross-references Node Map / Enemy System's beacon-to-family registry — mismatch shows editor warning (not hard reject; registry may be incomplete during authoring). CI build-time validation pass raises mismatch as build warning for QA review before release.

## Dependencies

### F.1 — Upstream (Hard Dependencies)

Systems L&R reads from. Each row names the contract consumed and the authority GDD that owns it.

| System | Contract Consumed | Authority | Data Flow |
|---|---|---|---|
| Enemy System | `CombatEndedPayload` (`DifficultyScore`, `ArchetypeFamily`, biome, close-call signals) via `OnCombatEnded` event | Enemy System GDD | L&R subscribes; receives payload on combat end, packs into `RewardContext`. |
| Node Map | `BeaconType` enum; `biomeIndex`; `nodeIndex` | Node Map GDD | Read-only consumers. `biomeIndex` and `nodeIndex` pass through `RewardContext`; `BeaconType` is the second axis of the `RewardTableSO` key. |
| V&P | `ChassisId` enum; `IPartCatalog.GetParts(slot, rarity)`; `SlotId` enum | V&P GDD | `ChassisId` read from run state for multiplier + card-filter branches; `IPartCatalog` is L&R's source of truth for resolving a sampled `(slot, rarity)` into a concrete `PartDraft`. |
| Card System | `ICardRewardGenerator.Generate(chassis, mastery, rarePityCounter, drawCount, currentDeckCardIds, rng)` → `CardDraft[]` (empty-array signals empty-Rare-pool pity) | Card System GDD | L&R delegates card selection entirely. L&R passes the pity counter as input each call and writes back updates per step 9; Card System reads only. L&R never touches rarity weights or slot-1 bias. |
| Scrap Economy | `TruckRewardMultiplier = 1.25`; `PityScrapAward = 40`; Scrap vocabulary anchors (cost parities) | Scrap Economy GDD (registry) | Constants consumed from registry; applied by L&R before calling downstream `GrantScrap`. Parity rule `GlobalPurgeCost < PityScrapAward < InstallBaseCost.Rare` is Scrap Economy's to enforce; L&R must honor on output. |
| Save & Persistence | `LootStateDTO` serializer; run-start DTO initialization hook | Save & Persistence GDD (undesigned 📌 `C5-SAVE`) | L&R reads DTO on every `GenerateRewards` call, writes updated DTO at step 9. |

Hard contract: if any upstream interface changes shape, L&R must be re-verified against it. No in-house duplicate implementations exist — the Card System's pity logic, V&P's part catalog, Scrap Economy's Truck compensation all live outside L&R.

### F.2 — Downstream (Consumers)

Systems that read from L&R. Each row names the contract L&R exposes and the provisional default if the consumer is undesigned at L&R implementation time.

| System | Contract Exposed | Status | Provisional Default |
|---|---|---|---|
| Post-Combat Flow UI | `RewardOffer[]` bundle in canonical order `[Scrap, Fuel?, Card, Part?]` | Undesigned | L&R emits to no-op subscriber; rewards return from the function but are not presented. Logged for debugging. Bundle shape is a hard contract — changes require breaking-change notice. |
| Scrap Economy | `int` Scrap amount; L&R calls `IScrapEconomy.GrantScrap(amount, source=LootReward)` | Designed | L&R's multiplier-applied `FinalScrap` is handed to `GrantScrap`. Scrap Economy owns cap-clamp / overflow / audit. |
| V&P | `PartDraft` (slot + rarity + concrete id) via `PartDropPayload`; V&P owns `InstallPart` mutation | Designed (partial — art-contract retrofit queued) | L&R emits offer only; Post-Combat Flow UI orchestrates player selection, V&P performs install. |
| Card System | `CardOfferPayload(CardDraft[])` — already L&R-wrapped after delegation | Designed | L&R wraps the generator's output. Card System's `OnCardAdded` flow handles player selection consequences. |
| Fuel System | `int` Fuel amount via `FuelGrantOffer`; consumer calls `IFuelSystem.GrantFuel(amount)` | Undesigned 📌 `C4-FUEL` / `C6-FUEL` | Provisional: Fuel is gated to Treasure/Event beacons only. Magnitude `[1, 3]` uniform. Fuel System GDD must ratify on authoring. |
| Save & Persistence | `LootStateDTO` write signal after each `GenerateRewards` call | Undesigned 📌 `C5-SAVE` / `C6-SAVE` | Provisional: passive-serializer model. L&R mutates DTO in-place; persistence layer detects dirty-flag and writes on its own cadence. If persist fails mid-call, EC-LR8 fallback applies. |

### F.3 — Back-Reference Updates Required

Other GDDs must be amended to reference this GDD's contracts. Retrofits are queued post-Phase-5 and execute when the relevant GDD is next touched.

**Hard retrofits (existing GDDs):**

- **Enemy System GDD** — Confirm `CombatEndedPayload` contains `DifficultyScore`, `ArchetypeFamily`, `biomeIndex`, `BeaconType`, and close-call flags. No changes expected (Enemy System D.5 already specifies this); this is a verification pass during L&R implementation.
- **Card System GDD** — Add explicit `ICardRewardGenerator` interface section specifying: `Generate(chassis, mastery, rng) → CardDraft[]` signature; guarantee non-null non-empty return for valid inputs (closes 📌 `EC-LR6-card-null-contract`); Card System owns all `RarePityCounter` writes; L&R reads `RarePityCounter` via `LootStateDTO` but never writes. Confirm Boss-skip rule (Section C.4.3: L&R substitutes 30 Scrap for CardOffer at Boss beacons) is acknowledged as a Loot-side authorial call, not a Card System change.
- **V&P GDD** — Add `IPartCatalog.GetParts(slot, rarity)` method to V&P's public interface with explicit note that empty-return is a valid outcome (closes 📌 `EC-LR7-vp-catalog-gap`); L&R's half-floor Scrap fallback handles the gap. Per BLOCKER-3 arbitration (2026-04-24), Enemy System carries `VisualFamily` (art axis, 4 members), `ArchetypeFamily` (gameplay axis, 5 members — owned by this GDD), and `SilhouetteClass` (size axis, 4 members) as three independent fields; V&P does not need to mirror these enums (part drops key off `ArchetypeFamily` via L&R's `RewardContext` only).
- **Scrap Economy GDD** — Update `TruckRewardMultiplier` registry entry `referenced_by` list to include `design/gdd/loot-reward.md`. Same for `PityScrapAward`. L&R's application of both constants pre-`GrantScrap` is the integration path (Scrap Economy D.9 + AC-SE36 already anticipate this).
- **Node Map GDD** — Confirm `BeaconType` enum is owned by Node Map, consumed read-only by L&R via `RewardContext.Beacon`. L&R's three-axis `RewardTableSO` key makes `BeaconType` a first-class lookup field — any new `BeaconType` added to Node Map requires a new `RewardTableSO` authoring pass.
- **Node Encounter GDD** (retrofit-complete 2026-04-23) — Calls `GenerateRewards(context, seed)` at beacon-dispatch time for Merchant, Chopshop, and Event-Treasure handlers. `RewardContext` fields supply `FuelGrantOverride` / `ScrapGrantOverride` for event-specific payloads (closes 📌 `C3-NE`, `C4-NE`, `C6-NE`). **BeaconType axis reconciliation:** L&R's 5-value `BeaconType` `{Combat, Elite, Boss, Treasure, Event}` is a **reward-table lookup axis**, NOT a structural graph-type enum. Node Map's 7-value structural enum `{Combat, EliteCombat, Merchant, Chopshop, Event, Rest, Haven}` is authoritative for graph structure. Node Encounter routes between the two: `Boss` = `EliteCombat + isBoss flag` (applied at Strip-5 biome gates); `Treasure` = `Event + TreasurePayload` (one of several Event outcomes); Merchant/Chopshop/Rest/Haven are NE handler types and NEVER appear on L&R's axis. If L&R is ever called with `beacon == Merchant`, `Chopshop`, `Rest`, or `Haven` directly, that is an authoring/routing error — NE handlers for those beacon types either call L&R with a re-mapped `BeaconType` (Merchant/Chopshop → synthesize `Combat`-like reward context via wildcard) or do not call L&R at all (Rest, Haven).

**Soft retrofits (undesigned GDDs — to be satisfied on authoring):**

- **Fuel System GDD** — Must define `IFuelSystem.GrantFuel(amount, source)` mutator. Must ratify L&R's beacon gate: Fuel rewards only at Treasure/Event beacons; Combat/Elite/Boss never emit `FuelGrantOffer` (closes 📌 `C4-FUEL`, `C6-FUEL`). Must define `FuelGrantRange` default `[1, 3]` and its own safe range.
- **Save & Persistence GDD** — Must define `LootStateDTO` serializer (passive-serializer model); must initialize DTO to defaults on run start (empty `PartDropCooldown`, `RarePityCounter=0`) — closes EC-LR4 boundary case. Must handle schema migration when `SlotId` enum changes (closes EC-LR5). Must surface persist-failure error to the UI error banner layer (closes EC-LR8, no retroactive reward mutation).
- **Post-Combat Flow UI** — Must consume `RewardOffer[]` in canonical order. Must present Scrap before Card before Part (animation-level order). Must handle empty-Card case when L&R emits `[Scrap]` only (EC-LR1, EC-LR2). Must show the error banner on EC-LR8 persist-failure (copy/styling owned by UI).
- **Card System GDD** (optional, see EC-LR6) — Add `Generate` null/empty return prohibition as a hard contract.
- **V&P GDD** (optional, see EC-LR7) — Add `IPartCatalog.GetParts` empty-return documentation.

### F.4 — Data Asset Authoring

L&R authors the following ScriptableObject asset types. Paths are prescriptive.

| Asset Type | Path | Purpose | Cross-References |
|---|---|---|---|
| `RewardTableSO` | `assets/data/loot/tables/` | One per `(BiomeIndex, BeaconType, ArchetypeFamily)` combination plus wildcards. MVP set ≈ 60 SOs. Contains `Entries` (kind + base weight + gate) and `Modifiers` (conditional additive overlays). | References `RewardKind` enum (L&R), `ArchetypeFamily` (Enemy System), `BeaconType` (Node Map), `SerializableCondition` (L&R). |
| `WeightModifierLibrary` (optional) | `assets/data/loot/modifiers/` | Shared-modifier reference file when multiple tables apply the same modifier. Keeps author-time copy-paste under control. Not strictly required for MVP. | References `RewardKind`, `SerializableCondition`. |

No enemy-definition, part-definition, card-definition, or status-effect assets are authored here — those belong to Enemy System, V&P, Card System, and Status Effects respectively.

**SO Import Validator** (carry to Section J): The validator responsible for rejecting malformed `RewardTableSO` assets at import time (per AR-LR1, AR-LR2, AR-LR3, AR-LR5) lives alongside `assets/data/loot/tables/`. Implementation is an engineering concern owned by `unity-specialist` during the `/architecture-decision` pass for the loot data pipeline. Validator rules are enumerated in Section E's Authoring Rules and will be formalized as acceptance criteria in Section J.

## Tuning Knobs

Every tunable surface in the Loot & Reward system with default, safe range, and the gameplay axis it controls. Values live on `LootRewardConfig` (global) or on individual `RewardTableSO` assets (per-table). Changes outside safe ranges require a tuning memo. Consumed constants from other GDDs are listed in G.3 for visibility but are not L&R's to modify.

### G.1 — Global Constants (on `LootRewardConfig`)

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| `BiomeBaseScrap[1]` (Sand Flats) | 15 | 10–25 | Floor Scrap for Biome 1 rewards. Below 10 = early rewards feel hollow; above 25 = early Chopshop economy inflates. Scout/Assault/Truck start economies all calibrated against this anchor. |
| `BiomeBaseScrap[2]` (Junk Mesa) | 28 | 20–40 | Mid-biome floor. Must remain `> BiomeBaseScrap[1]` — monotonic biome progression is a hard rule. |
| `BiomeBaseScrap[3]` (Haven Approach) | 42 | 30–55 | Late-biome floor. Funds Rare card installs near Haven (Rare install cost = 50 Scrap per Scrap Economy). Below 30 = late runs cash-starved; above 55 = trivializes Haven economy. |
| `EliteScrapBonus` | 18 | 10–30 | Flat Elite-beacon bonus on top of `BiomeBaseScrap`. Below 10 = Elites feel under-rewarded relative to their DS; above 30 = Elites become the only economic path. |
| `BossFlatScrap` | 30 | 25–50 | Boss-beacon substitute for the card offer (Boss-skip rule, C.4.3). Must stay close to `PityScrapAward=40` but below `InstallBaseCost.Rare=50` — boss Scrap should feel like bonus funding, not a card replacement. |
| `DS_THRESHOLD` | 0.40 | 0.30–0.55 | Width of the "floor band" in the DSBonus piecewise function. Below 0.30 = DS reward noise at low-DS combats; above 0.55 = high-DS combats under-paid. The 0.40 anchor is justified by Patch Rider's 0.128 baseline sitting comfortably in the floor band. |
| `DS_FLOOR_BONUS` | 4 | 2–8 | Minimum DSBonus — what every "easy" combat pays. Must be `< DS_CEILING_BONUS`. Below 2 = easy combats feel unpaid; above 8 = easy/hard Scrap gap too narrow. |
| `DS_CEILING_BONUS` | 12 | 8–20 | Maximum DSBonus at DS=1.0. Must be `> DS_FLOOR_BONUS`. Below 8 = no reward gradient; above 20 = peak-DS combats overpay and destabilize the Biome-3 Elite Truck max (90 Scrap). |
| `PartDropCooldownNodes` | 3 | 2–5 | Nodes a slot is blocked after receiving a drop. Below 2 = same-slot re-drops feel too fast and break the V&P scarcity rule; above 5 = too many nodes with no eligible part slots, EC-LR7 fallback rate climbs. |
| `FuelGrantRange.Min` | 1 | 1–2 | Floor Fuel grant at Treasure/Event beacons. Below 1 = dead rewards; above 2 = traversal slack too generous. |
| `FuelGrantRange.Max` | 3 | 2–5 | Ceiling Fuel grant. Must be `>= FuelGrantRange.Min`. Above 5 = trivializes route planning. Awaits Fuel System GDD to lock relationship to Fuel consumption rates 📌 `C4-FUEL`. |
| `PartRarityWeight.Common` | 60 | 50–70 | Weight for Common rarity in wildcard rarity sampling. Sum of `Common + Uncommon + Rare` must = 100 (enforced by `LootRewardConfig` validator). |
| `PartRarityWeight.Uncommon` | 30 | 20–35 | Weight for Uncommon. |
| `PartRarityWeight.Rare` | 10 | 5–20 | Weight for Rare. Above 20 = Rares stop feeling rare; below 5 = Rare parts feel unattainable even with many combats. |
| `RareEmptyPoolPityScrap` | 40 | 30–50 | Same as `PityScrapAward` from registry — mirrored here as L&R's local read for pity-branch detection. Source of truth is Scrap Economy's registry entry. |

Weight constraint: `PartRarityWeight.Common + Uncommon + Rare == 100` (enforced by `LootRewardConfig` validator at SO import time). Changing any without offsetting invalidates D.5's rarity probability math.

### G.2 — Per-Table Tuning (on `RewardTableSO`)

Per-table surfaces are authored per SO. No single global safe-range — authorability is constrained by Authoring Rules AR-LR1–AR-LR5 and by editor-time validation against the global constraint `TotalEffectiveWeight > 0`.

| Knob | Type | Per-Entry Range | Affects |
|---|---|---|---|
| `Entries[].BaseWeight` | int | 1–999 | Sampling weight for this reward kind. Absolute value only matters relative to other entries in the same table. A weight of 50 against another 50 = 50/50 sampling; 50 against 10 = 83% / 17%. |
| `Entries[].Kind` | enum | `{ScrapGrant, FuelGrant, CardOffer, PartDropOffer}` | Which reward kind this entry yields. Fuel-gated to Treasure/Event beacons (AR-LR3). |
| `Entries[].SlotHint` (PartDropOffer only) | enum + Wildcard | `{Engine, Hull, Mobility, Weapon, Armor, Wildcard}` | Pre-selects slot; Wildcard triggers uniform slot sampling in D.5. Slot vocabulary mirrors V&P GDD `SlotKind` enum (ADR-0007). |
| `Entries[].RarityHint` (PartDropOffer only) | enum + Wildcard | `{Common, Uncommon, Rare, Wildcard}` | Pre-selects rarity; Wildcard triggers weighted rarity sampling per `PartRarityWeight`. |
| `Entries[].Gate` | `SerializableCondition` | — | Optional gate — entry participates in sampling only if Gate evaluates true on the `RewardContext`. Must be pure per AR-LR4. |
| `Modifiers[].Delta` | signed int | -999 to +999 (AR-LR2 caps practical magnitude) | Additive weight overlay applied to entries whose `TargetKind` matches. Delta constrained by AR-LR2: no single Delta should drive an entry's `EffectiveWeight` below zero across reachable contexts unless the intent is full conditional suppression AND AR-LR1 holds via other entries. |
| `Modifiers[].TargetKind` | enum | Same as `Entries[].Kind` | Which reward kind the Delta applies to. |
| `Modifiers[].Condition` | `SerializableCondition` | — | Runtime condition — Delta applies only when Condition evaluates true on the `RewardContext`. Pure per AR-LR4. |

### G.3 — Consumed Constants (Read-Only From Registry)

These values are tuned elsewhere but constrain L&R's behavior. Listed here so the L&R tuning team has a single view of everything that affects reward output.

| Constant | Value | Source GDD | Why L&R Cares |
|---|---|---|---|
| `TruckRewardMultiplier` | 1.25 (floor) | Node Map (registry) | Applied by L&R at step 6 of C.3 before `GrantScrap`. If Node Map lowers below 1.25, Truck run identity collapses — Node Map must never tune below floor. |
| `PityScrapAward` | 40 | Scrap Economy (registry) | L&R substitutes this value when Card System signals empty-Rare pity (C.3 step 7) and when `ICardRewardGenerator` returns null/empty (EC-LR6). Parity rule enforced upstream: `GlobalPurgeCost < PityScrapAward < InstallBaseCost.Rare`. |
| `DifficultyScore.*` weights | HP=0.30, DPT=0.40, IntentCount=0.15, EnrageSeverity=0.15 | Enemy System (registry) | DS weights determine the shape of incoming DS values — changes to these invalidate D.3's DSBonus band table. Enemy System must notify L&R if weights shift outside their safe ranges. |
| `BiomeHPScalar[1..3]` + `BiomeDamageScalar[1..3]` | Biome 1/2/3 scalars | Enemy System (registry) | Indirectly shape DS distribution across biomes. Their calibration constrains which DS values L&R actually sees per biome — changes here shift G.1's `BiomeBaseScrap` comfortable ranges. |

### G.4 — Telemetry Plan

L&R emits the following events for runtime tuning verification. Events are emitted to the analytics pipeline at step 10 of `GenerateRewards` (after `RewardOffer[]` is assembled, before return). Fields are suggestive; analytics-engineer pass after MVP may refine schema.

| Event | Emitted When | Key Fields | Tuning Use |
|---|---|---|---|
| `LootReward.OfferGenerated` | Every `GenerateRewards` call, before return | biome, beacon, family, DS, chassis, mastery, rewardKinds[], scrapAmount, fuelAmount?, partSlot?, partRarity? | Per-combat reward composition. Aggregate to verify biome × beacon averages match D.2 envelope table. |
| `LootReward.ScrapGranted` | `ScrapGrantOffer` emitted | amount, source=LootReward, biome, beacon, dsBucket | Scrap-flow histogram by (biome, beacon). Verifies banded DS curve in D.3 shape is realized in live play. |
| `LootReward.CardOfferDelegated` | `CardOfferPayload` emitted | chassis, mastery, rarePityCounter, drawCount, drawnRarities[] | Card-offer composition. Correlate with `RarePityCounter` progression to verify pity-counter dynamics. |
| `LootReward.PartDropOffered` | `PartDropPayload` emitted | slot, rarity, biome, beacon | Part drop distribution — confirms uniform slot sampling + `PartRarityWeight` ratios. |
| `LootReward.PityFallbackFired` | `PityScrapAward` substituted (step 7 or EC-LR6) | cause = {EmptyRarePool, CardGeneratorNull/Empty}, chassis, mastery | Pity trigger rate. Target cadence: ~once per 8 card offers for EmptyRarePool path per Section B fantasy. CardGeneratorNull/Empty should be ~0 (EC-LR6 is a bug surface). |
| `LootReward.PartDropCooldownFallback` | All-slots-cooldown fallback (D.5) | biome, beacon, slotCooldowns | Fallback rate. Target <1% per EC-LR7 analysis. Rising rate signals `PartDropCooldownNodes` is too long OR PartDropOffer table weights too high. |
| `LootReward.ContentWarning` | EC-LR1, EC-LR2, EC-LR3 fires | ec_code, tableName?, biome, beacon, family | Any emission is a content bug — target 0 per session. Alerts designers to table authoring gaps. |
| `LootReward.PersistFailed` | EC-LR8 fires | exceptionType, message | Save-failure rate. Any >0% rate is a platform issue escalation (disk, permissions). |

**Post-EA tuning triggers:**
- If `PityFallbackFired.EmptyRarePool` rate deviates from ~12% of card offers (one in 8) → tune Card System's Rare pool or `PartRarityWeight`, not L&R constants directly.
- If `PartDropCooldownFallback` rate > 2% → raise `PartDropCooldownNodes` ceiling to 4 OR reduce PartDropOffer weights in high-frequency tables.
- If `ScrapGranted.amount` average for Biome-1 Combat (non-Truck) falls outside [19, 23] across 1000+ sessions → verify DS distribution is not skewing high (Enemy System weight retune) or low (combat is too trivial).
- If `ContentWarning` emits > 0 times in a release candidate playtest → hold release; the missing `RewardTableSO` is a content bug.

## Visual/Audio Requirements

### H.0 — V&A Contract Overview

The Loot & Reward system is a pure function. It draws nothing and plays no sound. Section H is the **V&A contract L&R imposes on Post-Combat Flow UI**: for each `RewardKind` emitted in the canonical `[Scrap, Fuel?, Card, Part?]` bundle, H specifies the emotional envelope — visual in H.1, audio in H.2 — that Post-Combat Flow UI must deliver. Post-Combat Flow UI owns implementation; L&R owns the envelope. The load-bearing fantasy is Section B's "The Road Keeps Its Ledger" — the screen after combat is an accounting beat, not a celebration. Pillar 4 (Scarcity with Agency) lives or dies on the register discipline captured below.

📌 `H-postcombat-panel-spec` — Post-Combat Flow UI requires a panel template + audio bus contract (layout grid, z-order, bus routing, duck automation target) before implementing the envelopes below. Co-owned by art-director / audio-director / ux-designer / lead-programmer — not authored in this GDD.

---

### H.1 — Visual Requirements

#### H.1.1 — Per-RewardKind Visual Envelope

##### ScrapGrant

| Property | Spec |
|---|---|
| Register | Ledger accounting. Quiet, mechanical, expected. |
| Composition | Tarnished-steel scrap icon anchored left + numeric counter ticking from prior total to new total. No centered spotlight, no full-panel overlay. |
| Entry motion | Steady metronomic counter tick; ease-out decel at final value. No bounce, no elastic, no overshoot. |
| Hold | Counter settles; hold 0.4s before next element permitted. |
| Palette | Cold metallic grey / tarnished steel. Icon `#7A7872`. Numeral ash-white `#E8E0D4`. |
| Must NOT | Particle bursts. Screen-shake. Confetti. Full-panel flash. "You won" treatments. |

High-frequency event (fires every combat). Register must be neutral; 50 grants = 50 ledger entries, not 50 micro-celebrations.

##### FuelGrant (Treasure/Event beacons only)

| Property | Spec |
|---|---|
| Register | Scarce and specific. A particular thing was handed over. |
| Composition | Jerrycan/fuel-drum silhouette enters laterally (not top-down); integer amount appears beside icon after it settles. |
| Entry motion | Slower, heavier than Scrap — icon set down, not dropped. Numeral fades in after icon settles, not simultaneously. |
| Hold | 0.5s after settle before Card panel permitted. |
| Palette | Amber `#C98A3A`, categorically warmer than Scrap. Numeral matches Scrap ash-white — text layer reads unified. |
| Must NOT | Same motion/icon vocabulary as Scrap. Warm glow or emission — warmth lives in hue, not radiance. |

##### CardOfferPayload

| Property | Spec |
|---|---|
| Register | Deliberate. The player is being asked something. |
| Composition | Parchment card faces on canvas-toned panel, flat, centered, side by side. No elevation shadow, no slot-machine framing. |
| Entry motion | Short slide or fade to placed position — a placing gesture, not a reveal gesture. |
| Hold | Hard 0.6–0.8s beat after last card settles before interactive prompt activates. |
| Palette | Paper/parchment card face. Rarity = frame color + border weight only. Common: thin grey; Uncommon: medium amber; Rare: heavier cool steel-blue (not gold). No size/scale difference between rarities. |
| Must NOT | Card flip. Sparkle/shimmer/glow on faces. Scale pop on reveal. Slot-machine spin. Any "ta-da" treatment. |

##### PartDropPayload

| Property | Spec |
|---|---|
| Register | Weighted, mechanical, specific. |
| Composition | Three-beat silhouette-first reveal: (1) slot identity glyph in rarity-color outline (empty); (2) rarity frame fills in; (3) part silhouette resolves inside frame. Slot → rarity → identity. |
| Entry motion | Each beat eases in with ~0.3s gap; no bounce. Total entry ~1.0–1.2s. |
| Hold | 0.5s after part identity resolves. |
| Palette | Slot color follows combat-UI state vocabulary (Engine=amber, Hull=steel, Mobility=sand, Weapon=raider-red, Armor=verdigris). Rarity via border weight: Common 1px, Uncommon 2px, Rare 3px cooler tint. Part silhouette: dark against slot frame — legible as shape before label. |
| Must NOT | Triumphant fanfare motion. Scale pop. Particle burst. Any "congratulations" read. |

#### H.1.2 — Special Beats (Visual)

**Pity-fire (`RarePityCounter == 8`, empty Rare pool → PityScrapAward=40):**

- Scrap tally runs normal counter; after settle, **secondary stenciled "SETTLED" glyph** (or tally mark) fades in beside/beneath numeral.
- Glyph treatment: stamped/stenciled, not cleanly printed. Holds for normal Scrap hold duration.
- Visual weight increase lives only in the secondary glyph. Icon, counter motion, palette = standard Scrap.
- **Must NOT show:** greyed card slot, missing-card placeholder, apology indicator (X, crossed-out silhouette). The card slot does not exist in this sequence — Scrap beat plays as if it never did.

📌 `H-visual-pity-glyph` — stencil-style "SETTLED" glyph + in-world vocabulary requires asset pass; coordinate with writing/lore style guide.

**Boss-skip (Boss beacon → 30 Scrap substitutes for CardOffer):**

- Post-combat flow presents only the Scrap beat. Panel layout does not reserve space for a card panel. No muted silhouette, no placeholder.
- Scrap beat = standard Scrap (no special glyph, no secondary marker). Boss combat carries its own emotional weight upstream; reward beat is deliberately flat by contrast.
- **The absence is the design.** Players learn the pattern — Boss pays in Scrap. Taught through pattern, not explanation.
- **Must NOT include:** empty panel, lock icon, "reward: unavailable" label. Layout collapses cleanly.

**All-slots-cooldown fallback (PartDrop → Scrap(BiomeBaseScrap/2)):**

- Standard Scrap beat only, half-floor amount. Same visual treatment as any Scrap grant of that magnitude.
- Occurs <1% of reward events (D.5). Engineered safety net, not player-facing state.
- **Must NOT include:** greyed part silhouette, "part unavailable" placeholder, empty slot-frame.

#### H.1.3 — Pacing and Visual Rules

| Step | Element | Enters after | Hold before next |
|---|---|---|---|
| 1 | Scrap tally (always) | Combat resolution | 0.4s after settle |
| 2 | Fuel icon+amount (if present) | Scrap settles | 0.5s after settle |
| 3 | Card panel (if present) | Fuel settles (or Scrap) | 0.6–0.8s beat before interaction active |
| 4 | Part 3-beat reveal (if present) | Card panel settles | 0.5s after identity resolves |
| 5 | Interactive prompt | All reveals complete | — |

**Total envelope:** 3–5s first tick → interactive prompt. Fast combats trend to 3s floor; full bundles trend to 5s ceiling.

**Skip input:** fast-forwards cleanly (2×–3× pace) — no torn frames, no mid-reveal cuts. Hold beats collapse to zero on skip; motion completes.

**Palette discipline:** Scrap = cold steel; Fuel = amber; Card = paper/parchment on canvas panel; Part = dark silhouette on slot-color frame. Rarity via frame color/border weight only — no size scaling, no emissive glow. Panel background: neutral RUST ICON canvas tone (bleached sand, ash white). No per-kind background tint change.

**Motion discipline:** all transforms ease-out. No elastic, no bouncy overshoot, no spring physics.

#### H.1.4 — Visual Reference Anchors

| Reference | Take | Avoid |
|---|---|---|
| **FTL** inter-jump scrap tally | Counter ticks, nothing else moves; inter-scene minimalism | FTL font/iconography — use RUST ICON vocabulary |
| **Hades** boon altar | Object *placed*, not performed; deliberate centered; held beat before interaction | Saturation/Greek palette; god-tier shimmer |
| **Into the Breach** end-of-mission tally | Restrained accounting; line items resolve one at a time | Pixel scale — different resolution tier |
| **Hearthstone / Slay the Spire** card reveals | — | Card flip, sparkle on Rare, scale-pop on legendary. Too triumphant. |
| **MMO loot-drop bursts** | — | Any particle fire on reward reveal. Ledger entries, not celebrations. |

---

### H.2 — Audio Requirements

#### H.2.1 — Per-RewardKind Audio Envelope

##### ScrapGrant

| Attribute | Spec |
|---|---|
| Texture | Metallic percussive ticks — stamped steel, not coins |
| Duration | 0.6–1.0s tick loop; decays to silence |
| Decay | Short, dry; no sustained tail |
| Peak | -8 to -10 dB under combat SFX ceiling |
| Must NOT | Reward fanfare, coin-count sweep, pitch-rising sequence, orchestral |

High-frequency event. Register must be neutral; 50 ticks = 50 ledger entries, not 50 micro-celebrations.

##### FuelGrant

| Attribute | Spec |
|---|---|
| Texture | Warm, fluid-adjacent — hollow resonance with glug/pressurized-hiss quality |
| Duration | 0.6–0.9s single transient with modest tail |
| Decay | Warm fade |
| Peak | -7 to -9 dB (matches/slightly exceeds Scrap) |
| Must NOT | Coin sound, Scrap duplicate, triumphant sting, synth pad swell |

One delivery, not an accumulation. Distinction from Scrap = texture (metal vs. warmth, count vs. transfer), not volume.

##### CardOfferPayload

| Attribute | Spec |
|---|---|
| Texture | Cloth/paper-on-surface — soft impact, short natural resonance |
| Duration | 0.4–0.7s; silence after is the design |
| Decay | Mid tail — "a thing arrived," short enough not to linger |
| Peak | -8 to -10 dB (quietest non-silent cue) |
| Held beat | Perceptible silence before interactive prompt |
| Must NOT | Fanfare, pitch-rise sweep, orchestral strings, rarity-differentiated sting |

**No rarity-differentiated card cue.** Common/Rare use same cue — rarity is visible info; audio does not editorialize.

##### PartDropPayload

| Attribute | Spec |
|---|---|
| Texture | Mechanical thump — dense, weighted, single transient with metal/composite tail |
| Duration | 0.5–0.8s; single strike |
| Decay | Short mechanical ring (not reverb wash) |
| Peak | -4 to -6 dB (heaviest of the four; still under combat) |
| Must NOT | "Legendary loot" MMO drop, triumphant stinger, pitch-rise sting, synth brass |

Heaviest object-sound in the Post-Combat Flow screen; not the loudest moment in the session.

#### H.2.2 — Special Beats (Audio)

**Pity-fire:**

- Standard ScrapGrant tick plays — not replaced, not suppressed.
- Tick sequence is **heavier and slower** than standard — 2–3 ticks max, stamped-receipt cadence.
- Same texture family (metallic ledger-register); marginally more body — slightly larger object being stamped.
- **No card-reveal cue plays.** Space where card hush would have been is silent. No apology, no "unavailable," no minor-key variant.
- Total duration: 0.8–1.2s. Slower pace alone communicates weight.
- **Hardest cue to execute.** Standard game-audio vocabulary for substitution (pitch descent, minor intervals, muted success cues) does NOT apply. Pity-fire is a **guarantee being honored**, not a failure. Must sound like debt settled, not consolation.

📌 `H-audio-pitycue-register` — cross-reference Scrap Economy H.4 (pity-to-Scrap reveal) before final sound-design brief.

**Boss-skip:**

- ScrapGrant plays as normal ScrapGrant. Boss 30-Scrap within normal range; no audio distinction.
- **The silence where the card cue would have been is the design.** No muted stinger, no "unavailable" one-shot, no UI sound flagging absence.
- Player exits with Scrap + knowledge they beat a boss. Audio doesn't annotate what didn't arrive.

**All-slots-cooldown fallback:**

- ScrapGrant plays with standard register (no volume reduction for reduced amount).
- No "part unavailable" sound, no diminished Scrap cue.
- Expected rate <1%; audio system treats as invisible.

#### H.2.3 — Pacing and Mix Rules

**Cue ordering** (matches canonical `[Scrap, Fuel?, Card, Part?]`):

| Step | Cue | Resolution before next |
|---|---|---|
| 1 | Scrap tick loop | Fully decay to silence |
| 2 | FuelGrant (if present) | Fully decay before Card hush begins |
| 3 | CardOfferPayload hush | Reach held-beat (perceptible silence) before Part stinger |
| 4 | PartDropPayload thump (if present) | Clear all tails before interactive prompt |

**Total envelope:** 3–5s from first Scrap tick to interactive prompt.

**Skip ducking:** remaining audio **ducked, not cut** at -24 dB over 200ms. No mid-cue truncation, no pops, no voice-termination artifacts. Ducked tail exits cleanly while next screen state loads.

📌 `H-audio-postcombat-sfxbus` — Post-Combat Flow UI requires dedicated audio bus (send routing, duck automation target, voice ceiling). Bus contract must be authored before audio implementation — coordinate with lead-programmer + sound-designer.

**Mix budget:**

| Cue | Peak vs combat SFX ceiling |
|---|---|
| ScrapGrant | -8 to -10 dB |
| FuelGrant | -7 to -9 dB |
| CardOfferPayload | -8 to -10 dB (quietest non-silent) |
| PartDropPayload | -4 to -6 dB (heaviest; still under combat) |
| Pity-fire ScrapGrant | -6 to -8 dB (marginally heavier) |
| Interactive prompt | -6 dB (sits above all reward cues) |

Post-Combat Flow is calmer than combat — **quieter not louder**. No reward cue meets or exceeds combat-hit resolution peak.

#### H.2.4 — Audio Reference Anchors

| Reference | Element | Specificity |
|---|---|---|
| **FTL** | Inter-beacon scrap accumulation | Dry metallic tick-count register — accounting, not celebration. Target for ScrapGrant texture. |
| **Into the Breach** | End-of-mission tally beeps | Restrained, percussive, deliberate sequencing — tally register is legible without volume/orchestration. |
| **Hades** | Boon altar stingers | Specific, object-like transients with identifiable material. Model for PartDropPayload: one weighted impact, physically identifiable, not musical. |
| **FTL** | Inter-beacon ambient calm | Volume drops after combat; world settles. Macro model for Post-Combat Flow audio bed. |
| **Avoid: Hearthstone** | Card-reveal orchestral sweep | Announces power spike. CardOfferPayload must do the opposite — create space for decision. |
| **Avoid: Slay the Spire** | Card-reward flourish + rarity-differentiated sting | Rarity-sized sting model explicitly not the model here. |

## UI Requirements

### I.0 — Interactive Contract Overview

Section I is the Loot & Reward system's **interactive contract** on Post-Combat Flow UI. It does not specify layout, animation, or audio — those are H's domain. It specifies the input model, focus traversal, confirmation mechanics, and accessibility requirements that Post-Combat Flow UI must honor when presenting the `RewardOffer[]` bundle. Every MUST below is a testable obligation. Post-Combat Flow UI implements; L&R authors the contract.

---

### I.1 — UI Contract MUSTs

**I-LR-MUST-1: Keyboard/mouse baseline — all reward interactions are reachable by keyboard alone.**
Tab / arrow keys navigate between card options in `CardOfferPayload`. Enter confirms the focused selection. Escape or a dedicated Skip keybind (default: `Space`) advances through non-interactive reward beats and triggers continue on the final screen. Mouse click on a card or part panel is equivalent to keyboard confirm on the focused element; hover over a card or part panel is equivalent to focus — tooltip/preview activates identically. No information or action is accessible only via mouse hover.

**I-LR-MUST-2: Gamepad parity — all reward interactions are reachable by gamepad alone, with no hover-only states.**
D-pad left/right (or left stick) navigates card options in `CardOfferPayload`; South face button (A/Cross) confirms. East face button (B/Circle) declines a `PartDropOffer` or cancels a pending card selection before confirmation. South face button also serves as continue on non-interactive beats. No interaction required from a card or part panel is triggered by hover alone — every hover-equivalent state (focus ring, tooltip reveal, preview) is reachable by D-pad focus. Per `technical-preferences.md`, gamepad must not gate any feature.

**I-LR-MUST-3: Focus traversal — initial focus and post-confirm focus are deterministic per RewardKind.**
On screen entry, initial focus is assigned as follows: if `CardOfferPayload` is present, focus lands on the first (leftmost) card; if only `ScrapGrantOffer` or `FuelGrantOffer` are present (non-interactive bundle), focus lands on the continue button. After the player confirms a card selection, focus moves to the `PartDropOffer` accept/decline buttons if a part offer is present; otherwise focus moves to the continue button. After part accept or decline, focus moves to the continue button. Focus must never become stranded or return to a previously resolved panel.

**I-LR-MUST-4: Card selection mechanics — selection is a two-step commit; hover/focus reveals tooltip.**
Hovering or D-pad-focusing a card in `CardOfferPayload` renders that card in a focused/preview state and activates the card tooltip (name, type, cost, rarity frame, description text). A single click or confirm button press selects the card (highlighted, not yet committed). A second confirm press on the same selected card commits the choice and advances the flow. Pressing confirm on a different card re-selects without committing. This two-step model (select then confirm) prevents accidental picks on fast input. The commit action must be visually distinct from the select action.

📌 `I-card-tooltip-schema` — Card System must expose a tooltip data contract (`CardDraft` → display name, description, rarity, family, cost) consumable by Post-Combat Flow UI without coupling to Card System internals.

**I-LR-MUST-5: Part offer mechanics — accept/decline are both always available; current part in slot is shown for comparison.**
`PartDropOffer` presents the offered part (slot identity, rarity, part name + stat summary) alongside the current part occupying that slot on the player's vehicle, if any. If the slot is empty, a blank "— empty —" placeholder is shown. Decline is always available as a labeled action (keyboard: Escape or dedicated bind; gamepad: East face button). Accept installs the new part via V&P's `InstallPart` mutator and replaces the current part — this consequence must be communicated in the panel before the player commits ("Replaces: [current part name]" or equivalent). There is no forced take; the player may always decline.

📌 `I-part-compare-panel` — V&P must expose the current installed part's display data (name, rarity, stat summary) for the slot identified in `PartDropPayload` so Post-Combat Flow UI can render the comparison without holding V&P state itself.

**I-LR-MUST-6: Continue contract — every post-combat screen requires an explicit continue action; no auto-advance.**
Non-interactive bundles (Scrap-only, Boss-skip, pity-fire, all-slots-cooldown fallback) do not auto-dismiss. The continue button (keyboard: `Space` or `Enter`; gamepad: South face button) is always present and focused after all reveals complete per H.1.3's pacing. Skip input during the reveal sequence (H.1.3 fast-forward) accelerates presentation to the interactive-ready state but does not trigger continue — the player must press continue deliberately after all elements have settled. This ensures the player registers what was received before advancing.

**I-LR-MUST-7: Multi-offer ordering — when Card and Part offers are both present, Card resolves first, then Part, then continue.**
Post-Combat Flow UI presents interactive panels in the canonical order `[Card, Part]` matching H.1.3's pacing table. Card selection is resolved to a commit before the Part panel becomes interactive. The Part panel must not be focusable or activatable while a card selection is pending. After card commit, the Part panel activates with deterministic initial focus on the accept button. This sequential single-panel model prevents simultaneous multi-decision overload.

**I-LR-MUST-8: Input buffering and double-activation protection — a single commit input must not fire twice.**
The confirm action for card commit and part accept is consumed on the frame it is detected; the input system must not re-fire it on the next frame if the button is held. The Post-Combat Flow UI must disable the confirm button for a minimum of one rendered frame (≥ 16ms at 60fps) after a commit fires, before re-enabling for the next panel. This prevents a single fast button press from committing a card and simultaneously accepting a part, or advancing two screens on one physical press.

**I-LR-MUST-9: Accessibility — all reward content is announced to the accessibility tree; rarity is never encoded by color alone.**
On screen entry, a structured accessibility announcement fires listing all rewards in the bundle: `"Gained [N] Scrap. [Fuel: N units.] [Card offer: N options. Choose one or skip.] [Part offer: [slot] [rarity] part available. Accept or decline.]"` Each card in `CardOfferPayload` exposes its name, rarity, family, and description to the accessibility tree. Rarity is communicated by border weight + frame shape in addition to color (per H.1.1: Common = thin grey border, Uncommon = medium amber border, Rare = heavier cool-steel border with distinct shape) — removing color from any rarity display must not make rarities indistinguishable. All interactive buttons carry accessible labels: "Confirm card selection", "Accept part", "Decline part", "Continue".

**I-LR-MUST-10: Dev-build error surfacing — `ContentWarning` from EC-LR1/EC-LR2/EC-LR3 is shown in dev builds only; the player-facing screen is unaffected.**
When L&R emits a content warning (no matching `RewardTableSO`, all weights zero, or authoring violation), the player receives the fallback `ScrapGrantOffer` normally — the post-combat screen presents this as a standard Scrap beat with no error UI. In non-shipping (development) builds only, a non-blocking overlay banner displays the warning string (`"LootReward: [EC code] — [message]"`) for QA triage. The banner must not block any interactive element. In shipping builds the banner is compiled out; no player-facing error state exists for this condition because L&R always emits at minimum a floor Scrap offer.

---

### I.2 — Carry-Forward Flags and Downstream Targets

| Flag | Target System | Obligation |
|---|---|---|
| 📌 `I-card-tooltip-schema` | Card System | Define `CardDraft` tooltip data contract (name, description, rarity, family, cost) as a stable Post-Combat Flow UI-consumable interface. Required before Post-Combat Flow UI implements card panel. |
| 📌 `I-part-compare-panel` | V&P | Expose current installed part display data (name, rarity, stat summary) per `SlotId` as a Post-Combat Flow UI-readable interface. Required before part offer panel implements side-by-side comparison. |
| I-LR-MUST-1 through I-LR-MUST-10 | Post-Combat Flow UI | All MUSTs are L&R's interactive contract on the Post-Combat Flow UI spec (`design/ux/post-combat.md`). The UX spec must cite each MUST and provide implementation evidence. |
| I-LR-MUST-8 (input buffering) | `unity-ui-specialist` | Input debounce minimum (≥ 1 frame) must be verified at implementation — not enforceable at spec layer alone. Flag for implementation review. |
| I-LR-MUST-9 (a11y tree) | `unity-ui-specialist` | Unity UI Toolkit accessibility API bindings for all reward panel elements. Coordinate with accessibility requirements doc at `design/ux/accessibility-requirements.md`. |

## Acceptance Criteria

### J.0 — Acceptance Criteria Overview

Section J provides exhaustive testable coverage for every rule, formula, edge case, dependency contract, tuning constraint, and UI obligation established in Sections A–I of the Loot & Reward GDD. Coverage spans 55 ACs across 13 surfaces, distributed across three evidence tiers: unit tests (BLOCKING — Logic stories), integration tests or playtests (BLOCKING — Integration stories), and manual walkthrough / smoke checks (ADVISORY — UI, Config, and Visual stories). No rule established upstream is left without a corresponding verifiable AC.

---

### J.1 — Pure Function Determinism & Contract

**AC-LR1** — Given `GenerateRewards(context, seed)` is called twice with identical `RewardContext` values and the same seed integer. When the function returns. Then both calls return an array of the same length, same `RewardOffer` subtypes in the same positions, and the same magnitude fields — byte-for-byte equivalent output. No call to `UnityEngine.Random`, `Time.time`, `Time.frameCount`, or any static mutable field outside `RewardContext` occurs anywhere in the call path.
Unit test: `LootReward_SameInputsSameSeed_IdenticalOutput`. Verifies C.1.

**AC-LR2** — Given the caller computes the seed as `RunSeed ^ nodeIndex`. When `GenerateRewards` is called. Then the only `System.Random` instance constructed inside the function is `new System.Random(seed)` where `seed == RunSeed ^ nodeIndex`. No secondary `System.Random` is seeded from any other source; `UnityEngine.Random` is never invoked (forbidden per `technical-preferences.md`).
Unit test: `LootReward_SeedDerivation_OnlyXorSeed`. Verifies C.1, C.3 step 2.

**AC-LR3** — Given `GenerateRewards` completes. When the input `RewardTableSO`, `LootStateDTO.PartDropCooldown`, and `LootStateDTO.RarePityCounter` are inspected after the call. Then neither the `RewardTableSO` entries nor the `RarePityCounter` field have been mutated. The only mutation permitted is decrement of `PartDropCooldown` values and a potential increment of `PartDropCooldown[droppedSlot]` — both as specified in C.3 step 9.
Unit test: `LootReward_PureFunction_NoInputMutation`. Verifies C.1, C.3 step 9.

**AC-LR4** — Given a `RewardOffer[]` returned from any call to `GenerateRewards` for a Combat or Elite beacon. When the array is inspected. Then the canonical output order `[ScrapGrantOffer, FuelGrantOffer?, CardOfferPayload?, PartDropPayload?]` is always satisfied — Scrap is always first, no FuelGrantOffer appears in Combat or Elite results, Card (if present) precedes Part (if present). No other ordering is acceptable.
Unit test: `LootReward_CanonicalOutputOrder_AllBeaconTypes`. Verifies C.3 step 10.

**AC-LR5** — Given `GenerateRewards` is called a second time with identical inputs while the first call's stack frame is still on the call stack (hypothetical reentrancy via a callback triggered during generation). When the second call completes. Then it returns the same result as the first call would have returned for those same inputs. No shared mutable state between the two invocations exists; reentrancy does not corrupt either result.
Unit test: `LootReward_Reentrant_IdenticalResults`. Verifies C.1.

---

### J.2 — Scrap Formula & DSBonus Bands

**AC-LR6** — Given `biome=1`, `beacon=Combat`, `DS=0.0`, `chassis=Scout` (DS < DS_THRESHOLD). When `GenerateRewards` computes `FinalScrap`. Then `DSBonus = 4`, `BaseScrap = 15 + 0 + 4 = 19`, `FinalScrap = 19` (no multiplier). Result matches D.3 floor band and the D.7 gold-standard trace first-call Scrap value.
Unit test: `LootReward_Scrap_Biome1_Combat_DS0_Scout_Returns19`. Verifies C.4.1, D.2, D.3.

**AC-LR7** — Given `biome=3`, `beacon=Elite`, `DS=0.70`, `chassis=Truck`. When `GenerateRewards` computes `FinalScrap`. Then `DSBonus = 4 + (0.30/0.60) × 8 = 8`, `BaseScrap = 42 + 18 + 8 = 68`, `FinalScrap = round(68) × 1.25 = 85`. Result matches D.2 envelope max row (Biome-3 Elite Truck) and C.4.1 worked example.
Unit test: `LootReward_Scrap_Biome3_Elite_DS070_Truck_Returns85`. Verifies C.4.1, D.2.

**AC-LR8** — Given `DS < 0.40` for any biome and beacon combination. When `DSBonus` is computed. Then `DSBonus == 4` (DS_FLOOR_BONUS). The value is constant across the entire `[0.0, 0.40)` interval; no slope is applied within this band.
Unit test: `LootReward_DSBonus_FloorBand_DS039_Returns4`. Verifies D.2, D.3.

**AC-LR9** — Given `DS = 1.0`. When `DSBonus` is computed. Then `DSBonus = 4 + (0.60/0.60) × 8 = 12` (DS_CEILING_BONUS). The denominator `(1 - DS_THRESHOLD) = 0.60` is always non-zero; no NaN or infinity is produced at either boundary.
Unit test: `LootReward_DSBonus_CeilingBand_DS100_Returns12`. Verifies D.2, EC-LR9.

**AC-LR10** — Given `DS = 0.70`. When `DSBonus` is computed. Then `DSBonus = 4 + (0.30/0.60) × 8 = 8`. The piecewise-linear formula produces a continuous value at the exact interior point specified in D.3's design table.
Unit test: `LootReward_DSBonus_Interior_DS070_Returns8`. Verifies D.2, D.3.

**AC-LR11** — Given `chassis=Truck` and any `FinalScrap` magnitude. When the chassis multiplier is applied. Then `FinalScrap = round(BaseScrap) × TruckRewardMultiplier` where `TruckRewardMultiplier >= 1.25`. The multiplied and rounded value is passed to `IScrapEconomy.GrantScrap` — not the pre-multiplier `BaseScrap`. Scout and Assault chassis receive no multiplier (`× 1.0`).
Unit test: `LootReward_TruckMultiplier_AppliedBeforeGrantScrap`. Verifies C.4.1, G.3.

---

### J.3 — Fuel Grant Beacon Gating

**AC-LR12** — Given `beacon=Treasure`. When `GenerateRewards` assembles the `RewardOffer[]`. Then a `FuelGrantOffer` with `Amount` in `[FuelGrantRange.Min, FuelGrantRange.Max]` (default `[1, 3]` inclusive) may appear in the bundle. The amount is produced by `rng.Next(1, 4)` and is always an integer in the valid range.
Unit test: `LootReward_FuelGrant_TreasureBeacon_AmountInRange`. Verifies C.4.2, D.4.

**AC-LR13** — Given `beacon=Event`. When `GenerateRewards` assembles the `RewardOffer[]`. Then a `FuelGrantOffer` may appear with magnitude in `[FuelGrantRange.Min, FuelGrantRange.Max]`. An Event-node override payload (`FuelGrantOverride`) supplied in `RewardContext` replaces the `rng.Next` draw with the override value directly.
Unit test: `LootReward_FuelGrant_EventBeacon_AmountInRange_AndOverrideRespected`. Verifies C.4.2, D.4.

**AC-LR14** — Given `beacon ∈ {Combat, Elite, Boss}`. When `GenerateRewards` assembles the `RewardOffer[]` for any biome and any archetype family. Then no `FuelGrantOffer` is present in the result array. The beacon gate is absolute — no modifier, no context, no pity condition bypasses it.
Unit test: `LootReward_FuelGrant_CombatEliteBoss_NeverEmitted`. Verifies C.4.2, AR-LR3.

---

### J.4 — Card Offer Delegation & Pity

**AC-LR15** — Given a `CardOfferPayload` is included in the bundle. When the call to `ICardRewardGenerator.Generate` is inspected. Then L&R passes `(chassis, mastery, rarePityCounter, drawCount, currentDeckCardIds, rng)` — the exact six-argument canonical signature defined by Card System GDD `ICardRewardGenerator`. L&R does not compute rarity weights, does not consult pity thresholds internally, and wraps the returned `CardDraft[]` verbatim into `CardOfferPayload`. All draft-generation logic lives inside `ICardRewardGenerator`.
Unit test: `LootReward_CardDelegation_PassesCanonicalSixArgSignature`. Verifies C.4.3, C.6, F.1.

**AC-LR16** — Given `RarePityCounter` is read from `LootStateDTO` and its current value is below 8. When `GenerateRewards` processes a `CardOffer` sample. Then pity substitution does not fire; `CardOfferPayload` is emitted normally. `RarePityCounter` is incremented by 1 by L&R at step 9 if no Rare appeared in the returned drafts, or reset to 0 if at least one Rare appeared. Card System never writes this field (per ADR-0006 Decision §"Pity counter authority").
Unit test: `LootReward_PityCounter_BelowThreshold_NoSubstitution_LRWrites`. Verifies C.3 step 7 + step 9, C.5.

**AC-LR17** — Given `ICardRewardGenerator.Generate` signals the empty-Rare-pool pity condition (counter reaches 8 and Rare pool is exhausted). When `GenerateRewards` processes the step-7 pity branch. Then `CardOfferPayload` is dropped from the bundle and `ScrapGrantOffer(PityScrapAward=40)` is substituted in its place. Exactly one `ScrapGrantOffer` is present; no `CardOfferPayload` appears.
Unit test: `LootReward_PityFallback_EmptyRarePool_SubstitutesScrap40`. Verifies C.3 step 7.

**AC-LR18** — Given a pity substitution fires (either empty-Rare-pool or `ICardRewardGenerator` null/empty return per EC-LR6). When `LootStateDTO.RarePityCounter` is inspected after the call. Then L&R writes `RarePityCounter += 1` (the substitution branch counts as "no Rare drawn"). Card System is forbidden from writing this field — only L&R writes it (per ADR-0006 Decision §"Pity counter authority").
Unit test: `LootReward_PityFallback_LRIncrementsPityCounter`. Verifies C.5, C.3 step 7 + step 9.

**AC-LR19** — Given the pity path fires. When `LootReward.PityFallbackFired` telemetry is inspected. Then the event fires exactly once, `cause` field is `EmptyRarePool` (or `CardGeneratorNull/Empty` for EC-LR6 path), and `chassis` + `mastery` fields are populated.
Integration test: `LootReward_PityFired_TelemetryEventEmitted`. Verifies G.4, C.3 step 7.

**AC-LR20** — Given `beacon=Boss`. When `GenerateRewards` assembles the bundle. Then no `CardOfferPayload` is present — it is unconditionally replaced with `ScrapGrantOffer(BossFlatScrap=30)`. This substitution fires regardless of `RarePityCounter` value, card pool state, or chassis. The Boss-skip rule is a beacon-level gate, not a pity-level gate.
Unit test: `LootReward_BossBeacon_UnconditionalCardSkip_Returns30Scrap`. Verifies C.4.3, G.1.

---

### J.5 — Part Drop Cooldown & Sampling

**AC-LR21** — Given `PartDropOffer` is sampled and at least one slot has `PartDropCooldown == 0`. When the slot is selected. Then the emitted `PartDropPayload.Part.Slot` matches a slot whose cooldown was `0` at call time. A slot with `PartDropCooldown > 0` is never selected as the drop slot.
Unit test: `LootReward_PartDrop_OnlySelectsEligibleSlot`. Verifies C.4.4, D.5.

**AC-LR22** — Given a `PartDropPayload` is emitted for slot `s`. When `LootStateDTO.PartDropCooldown` is inspected after the call. Then `PartDropCooldown[s] == PartDropCooldownNodes` (default 3). All other slots that were on cooldown were decremented by 1 (floor 0); the emitting slot received the cooldown reset.
Unit test: `LootReward_PartDrop_SetsWinningSlotCooldown`. Verifies C.3 step 9, C.4.4.

**AC-LR23** — Given a node transition occurs (i.e., `GenerateRewards` is called for any `nodeIndex`). When the function completes. Then every slot's `PartDropCooldown` value has been decremented by 1 (floor 0), regardless of whether a part dropped. The decrement applies to all slots including slots that were already at 0.
Unit test: `LootReward_CooldownDecrements_EveryNodeCall`. Verifies C.3 step 9, C.5.

**AC-LR24** — Given all five slots (`Weapon`, `Engine`, `Mobility`, `Hull`, `Armor`) have `PartDropCooldown > 0`. When `PartDropOffer` is sampled. Then the cooldown fallback fires: `ScrapGrantOffer(BiomeBaseScrap[biome] / 2)` is substituted, no `PartDropPayload` is emitted, the `LootReward.PartDropCooldownFallback` telemetry event fires, and no slot's cooldown is modified by the failed drop attempt.
Unit test: `LootReward_PartDrop_AllSlotsOnCooldown_FallbackScrap`. Verifies C.4.4, D.5, G.4.

**AC-LR25** — Given a wildcard `RarityHint` on a sampled `PartDropOffer`. When rarity is sampled over 10,000 independent draws (each with a unique seed). Then the empirical distribution is `Common: 0.60 ± 0.02`, `Uncommon: 0.30 ± 0.02`, `Rare: 0.10 ± 0.02`, matching `PartRarityWeight = {Common:60, Uncommon:30, Rare:10}` from D.5.
Unit test: `LootReward_PartRarityWeights_Distribution10k`. Verifies D.5, G.1.

---

### J.6 — Weight Modifier Additive Overlays

**AC-LR26** — Given the D.6 worked example: Biome-2 Elite Raider table, `{ScrapGrant:40, CardOffer:50, PartDropOffer:20}`, modifiers M1 (`Scout && Mastery>=4` → CardOffer +15) and M2 (`DS>=0.5` → PartDropOffer +10), context Scout/Mastery=4/DS=0.62. When `EffectiveWeight` is computed for all three entries. Then results are `ScrapGrant=40`, `CardOffer=65`, `PartDropOffer=30`, total=135. Sampling probabilities match `{29.6%, 48.1%, 22.2%}` within ± 0.5%.
Unit test: `LootReward_WeightModifier_D6WorkedExample_MatchesSpec`. Verifies D.6, C.2.

**AC-LR27** — Given a modifier with a negative `Delta` that drives one entry's `EffectiveWeight` below zero. When `max(0, BaseWeight + ΣDeltas)` is computed. Then `EffectiveWeight` is clamped to `0` — never negative. A zero-weight entry does not participate in the sampling pool and does not contribute to the total denominator.
Unit test: `LootReward_WeightModifier_NegativeDeltaClampsToZero`. Verifies C.2, AR-LR2.

**AC-LR28** — Given a `SerializableCondition` gate on a modifier. When `GenerateRewards` is called twice with contexts that differ only in the condition field (e.g., `Chassis` switches from Scout to Truck). Then the condition's `Evaluate(context)` returns different results for the two contexts, and the resulting `EffectiveWeight` differs accordingly. No static mutable state is read inside condition evaluation — result is determined solely by the `RewardContext` argument.
Unit test: `LootReward_ModifierCondition_PureFunctionOfContext`. Verifies C.2, AR-LR4.

---

### J.7 — ContentWarning Fallback & Edge Cases

**AC-LR29** — Given `GenerateRewards` is called with a `(biome, beacon, family)` key that matches no `RewardTableSO` and no wildcard fallback table exists. When the pipeline executes C.3 step 3. Then the result is exactly `[ScrapGrantOffer(BiomeBaseScrap[biome])]` (length 1, no DSBonus, no multiplier), the content warning string `"LootReward: no RewardTableSO found for (...)"` is logged, `LootStateDTO` cooldowns are decremented normally, and no exception is thrown.
Unit test: `LootReward_NoTable_NoWildcard_EmitsFloorScrap`. Verifies EC-LR1.

**AC-LR30** — Given a `RewardTableSO` is found but every entry's `EffectiveWeight` is 0 after modifier overlays. When `GenerateRewards` proceeds to sampling. Then the result is `[ScrapGrantOffer(BiomeBaseScrap[biome])]` with the distinct AR-LR2 warning string, cooldowns are decremented, and no exception is thrown. The warning message is distinct from the EC-LR1 message so QA can differentiate the two content-bug classes.
Unit test: `LootReward_AllWeightsZero_EmitsFloorScrapWithARLR2Warning`. Verifies EC-LR2, AR-LR2.

**AC-LR31** — Given a `RewardTableSO` with `Beacon ∈ {Combat, Elite, Boss}` that contains a `FuelGrant` entry AND that entry is sampled at runtime (SO validator was bypassed). When step 5 detects the beacon mismatch. Then `FuelGrantOffer` is replaced with `ScrapGrantOffer(BiomeBaseScrap[biome] / 2)`, `IFuelSystem.GrantFuel` is never called, and the AR-LR3 error string is logged. If the entry is not sampled, pipeline proceeds without logging.
Unit test: `LootReward_FuelGrantInCombatTable_SubstitutedAtRuntime`. Verifies EC-LR3, AR-LR3.

**AC-LR32** — Given `context.State == null` (EC-LR4: null DTO on run start or pre-schema resume). When `GenerateRewards` is called. Then L&R initializes a default `LootStateDTO` (`RarePityCounter=0`, all slot cooldowns=0), uses it for the call, writes it at step 9, and logs the EC-LR4 warning. No exception is thrown; the output bundle is non-empty.
Unit test: `LootReward_NullState_InitializesDefaults`. Verifies EC-LR4.

**AC-LR33** — Given `LootStateDTO.PartDropCooldown` contains a `SlotId` key that no longer exists in V&P's slot registry (EC-LR5: stale schema key). When the step-9 decrement loop encounters the unknown key. Then the unknown key is skipped (no decrement, no exception), the EC-LR5 warning is logged, all known keys are decremented normally, and the unknown key is preserved in the dictionary for forward-compat schema resumption.
Unit test: `LootReward_StaleSlotKey_SkippedWithWarning`. Verifies EC-LR5.

**AC-LR34** — Given `ICardRewardGenerator.Generate` returns `null` or an empty `CardDraft[]` (EC-LR6: Card System contract violation OR legitimate empty-pool pity signal). When step 8 receives the return. Then `CardOfferPayload` is dropped, `ScrapGrantOffer(PityScrapAward=40)` is substituted, the EC-LR6 error string is logged for the null case (empty array is a valid pity signal, not an error), and L&R writes `RarePityCounter += 1` at step 9 (the substitution counts as "no Rare drawn").
Unit test: `LootReward_CardGeneratorReturnsNull_SubstitutesPityScrap` and `LootReward_CardGeneratorReturnsEmpty_SubstitutesPityScrap_IncrementsPity`. Verifies EC-LR6.

**AC-LR35** — Given `IPartCatalog.GetParts(slot, rarity)` returns an empty collection (EC-LR7: catalog gap). When step 5 resolves the sampled `PartDropOffer`. Then `ScrapGrantOffer(BiomeBaseScrap[biome] / 2)` is substituted, no `PartDropPayload` is emitted, `PartDropCooldown[slot]` is NOT set (no drop occurred, no cooldown penalty), and the EC-LR7 warning is logged.
Unit test: `LootReward_PartCatalogEmpty_SubstitutesHalfFloorScrap_NoCooldown`. Verifies EC-LR7.

**AC-LR36** — Given `context.State` contains a valid `LootStateDTO` and `GenerateRewards` completes successfully. When the player vehicle has no installed parts (brand-new run, all slots empty). When a `PartDropPayload` is included in the bundle. Then the `PartDropPayload` is emitted normally — empty slot state does not suppress part drops. Post-Combat Flow UI is responsible for rendering "— empty —" as the current-part comparison (per I-LR-MUST-5); L&R does not gate on installed part presence.
Unit test: `LootReward_PartDrop_EmptyVehicle_OfferStillGenerated`. Verifies C.4.4, I-LR-MUST-5.

---

### J.8 — Persistence & Save/Load Round-trip

**AC-LR37** — Given a `LootStateDTO` with `RarePityCounter=5` and `PartDropCooldown = {Engine:2, Hull:0, Mobility:1, Weapon:0, Armor:3}`. When the DTO is serialized and then deserialized. Then the deserialized values are byte-for-byte identical to the originals — no field is lost, truncated, or type-coerced. The round-trip preserves both the integer counter and the full dictionary.
Integration test or playtest: `LootReward_DTO_RoundTrip_PreservesAllFields`. Verifies C.5, F.1.

**AC-LR38** — Given the Save & Persistence layer throws or returns a failure signal during step 9 persist (EC-LR8: disk full, I/O error). When `GenerateRewards` catches the exception at the persistence boundary. Then the assembled `RewardOffer[]` is still returned to the caller, in-memory `LootStateDTO` retains the updated values, the `LootReward.PersistFailed` telemetry event fires with `exceptionType` and `message` fields populated, and no exception propagates to the caller.
Unit test: `LootReward_PersistFails_RewardsStillDelivered`. Verifies EC-LR8, G.4.

**AC-LR39** — Given a save file written by a future schema version that contains fields unknown to the current build. When `LootStateDTO` is deserialized. Then unknown fields are ignored without error, and the known fields (`RarePityCounter`, `PartDropCooldown`) are populated correctly from the file. The game does not crash; `GenerateRewards` runs normally on the partially-migrated DTO.
Integration test or playtest. Verifies F.1 (Save & Persistence soft retrofit), C.5.

---

### J.9 — Upstream/Downstream Dependency Contracts

**AC-LR40** — Given `OnCombatEnded(CombatEndedPayload)` fires from the Enemy System. When `GenerateRewards` is called with the resulting `RewardContext`. Then `context.DifficultyScore` equals `CombatEndedPayload.DifficultyScore` (Enemy System D.5 field), read-only — L&R never writes back to this field. Any discrepancy between the payload value and the value used inside `GenerateRewards` is a bug.
📌 Integration test (depends on Enemy System contract being honored): `LootReward_DifficultyScore_ConsumedFromPayload_ReadOnly`. Verifies F.1, C.3 step 1.

**AC-LR41** — Given `GenerateRewards` emits a `ScrapGrantOffer`. When `IScrapEconomy.GrantScrap(amount, source)` is called. Then the `amount` argument equals `FinalScrap` after `TruckRewardMultiplier` has been applied (not `BaseScrap` before multiplication). `GrantScrap` is called exactly once per `ScrapGrantOffer` in the bundle; it is not called for offers the player has not yet accepted (Post-Combat Flow UI owns the call sequencing after player selection).
📌 Integration test: `LootReward_GrantScrap_CalledWithMultipliedAmount`. Verifies F.2, C.4.1.

**AC-LR42** — Given `GenerateRewards` produces a `CardOfferPayload`. When `ICardRewardGenerator.Generate(chassis, mastery, rarePityCounter, drawCount, currentDeckCardIds, rng)` is called. Then the `rng` instance passed is the same `System.Random` seeded at step 2 — not a new instance — so card draws consume the same seeded sequence as the rest of the pipeline and do not introduce an independent random state. The other five arguments are sourced as documented in AC-LR15.
Unit test: `LootReward_CardDelegation_PassesSharedRngInstance`. Verifies C.3 step 8, C.4.3.

**AC-LR43** — Given a `PartDropPayload` is emitted and the player accepts the part via Post-Combat Flow UI. When `V&P.InstallPart(slot, part)` is called. Then it is called exactly once, only after player confirmation — never called unconditionally by `GenerateRewards` itself. L&R's role is offer generation only; `InstallPart` is the consumer's responsibility, not L&R's.
📌 Integration test (playtest acceptable): `LootReward_PartOffer_InstallCalledOnlyAfterConfirm`. Verifies C.4.4, F.2.

---

### J.10 — Tuning Knob Safe Ranges

**AC-LR44** — Given a `LootRewardConfig` SO is authored with any global constant outside its declared safe range (e.g., `BiomeBaseScrap[1] < 10` or `> 25`, `PartDropCooldownNodes < 2` or `> 5`). When the SO import validator runs. Then the asset is rejected with an error identifying the out-of-range field and its safe-range bounds. No out-of-range value reaches the runtime.
Unit test (validator): `LootRewardConfig_OutOfSafeRange_ValidatorRejects`. Verifies G.1.

**AC-LR45** — Given a `RewardTableSO` with any `Entries[].BaseWeight` value of `0` across all entries. When the SO import validator runs. Then import is rejected with the AR-LR1 error message: `"RewardTableSO '{name}': all BaseWeights are zero or negative — at least one entry must have BaseWeight > 0 (AR-LR1)."` A table with a mix of positive and zero weights passes, because at least one positive weight satisfies AR-LR1.
Unit test (validator): `RewardTableSO_AllZeroBaseWeights_ValidatorRejects`. Verifies AR-LR1, G.2.

**AC-LR46** — Given a `LootRewardConfig` is authored with `TruckRewardMultiplier < 1.25`. When the SO import validator runs. Then import is rejected. The 1.25 floor is a registry constraint (G.3); any value below it collapses the Truck chassis identity contract. A value of exactly `1.25` passes.
Unit test (validator): `LootRewardConfig_TruckMultiplierBelowFloor_ValidatorRejects`. Verifies G.3, C.4.1.

---

### J.11 — Telemetry Events Emitted

**AC-LR47** — Given `GenerateRewards` completes and returns the `RewardOffer[]` bundle. When `LootReward.OfferGenerated` telemetry is inspected. Then the event fires exactly once per call (not once per offer element in the bundle), and the payload includes `biome`, `beacon`, `family`, `DS`, `chassis`, `mastery`, `rewardKinds[]`, `scrapAmount`, and `fuelAmount?` as specified in G.4.
Integration test: `LootReward_Telemetry_OfferGenerated_FiresOncePerBundle`. Verifies G.4.

**AC-LR48** — Given a pity substitution fires (empty-Rare-pool path). When telemetry is inspected. Then `LootReward.PityFallbackFired` fires with `cause=EmptyRarePool`. Given the all-slots-cooldown fallback fires. When telemetry is inspected. Then `LootReward.PartDropCooldownFallback` fires with `slotCooldowns[]` populated. Given EC-LR1, EC-LR2, or EC-LR3 fires. Then `LootReward.ContentWarning` fires with the correct `ec_code` field. Each of these three events is distinct and mutually exclusive per invocation path.
Integration test: `LootReward_Telemetry_FallbackAndWarningEvents_CorrectEcCode`. Verifies G.4.

**AC-LR49** — Given `GenerateRewards` emits a `ScrapGrantOffer`. When `LootReward.ScrapGranted` telemetry fires. Then the payload includes `amount` (the post-multiplier `FinalScrap` value), `source=LootReward`, `biome`, `beacon`, and `dsBucket` (the DS band bucket as defined in D.3: `Floor`, `Competent`, `Challenging`, `Brutal`, `Maximum`). The `amount` field must never be `0` — floor clamping in D.2 guarantees `>= 1`.
Integration test: `LootReward_Telemetry_ScrapGranted_PayloadComplete`. Verifies G.4, D.2.

---

### J.12 — UI Integration Smoke (Delegates to Post-Combat Flow UI Spec)

**AC-LR50** — Given the Post-Combat Flow panel is entered after any reward bundle. When a screen reader or accessibility inspection tool is active. Then the structured announcement fires on panel entry listing all rewards in the bundle in the format specified in I-LR-MUST-9, before any interactive element is focused. All reward kinds (Scrap, Fuel, Card, Part) produce distinct announcement strings; the bundle announcement does not repeat after each individual element reveals.
Interaction test (ADVISORY). Verifies I-LR-MUST-9.

**AC-LR51** — Given a gamepad-only player (no keyboard, no mouse). When a full run is played through including at least one combat reward with a card offer, one combat reward with a part offer, one Treasure beacon, and one Boss beacon. Then all reward interactions are reachable and completable by D-pad and face button input alone — no hover-only state exists that is inaccessible to the gamepad user. No interaction requires mouse hover to reveal information or activate a control.
Playtest (ADVISORY). Verifies I-LR-MUST-2.

**AC-LR52** — Given the Post-Combat Flow panel presents both a `CardOfferPayload` and a `PartDropPayload` in the same bundle. When the player commits a card selection by pressing confirm twice on the same card. Then the confirm input is consumed on the commit frame and not re-fired on the next frame; the `PartDropPayload` accept button does not activate from the residual card-commit input. The minimum 16ms disable window (one rendered frame at 60fps) enforces this.
Interaction test (ADVISORY). Verifies I-LR-MUST-8.

**AC-LR53** — Given a dev build where L&R has emitted a content warning (EC-LR1, EC-LR2, or EC-LR3). When the Post-Combat Flow panel renders. Then a non-blocking overlay banner displays the warning string `"LootReward: [EC code] — [message]"` visible to QA but not blocking any interactive element. In a shipping build, the banner is absent — the player-facing screen presents only the fallback `ScrapGrantOffer` with no error UI.
Smoke check (ADVISORY). Verifies I-LR-MUST-10.

**AC-LR54** — Given a player on an empty-vehicle new run (all slots uninstalled) who receives a `PartDropPayload`. When the Post-Combat Flow part offer panel renders. Then the "current part" comparison column shows the `"— empty —"` placeholder text rather than a null-reference error, a blank panel, or a missing-asset indicator.
Smoke check (ADVISORY). Verifies I-LR-MUST-5, EC-LR edge case (brand-new run).

**AC-LR55** — Given the full post-combat reward sequence completes for a bundle containing `[ScrapGrantOffer, FuelGrantOffer, CardOfferPayload, PartDropPayload]` (a maximum bundle). When the total elapsed time from first Scrap tick to interactive-prompt-active state is measured. Then it falls within the 3–5s envelope specified in H.1.3 and H.2.3. Skip input at any point fast-forwards to the interactive-ready state without torn frames, mid-reveal cuts, or missed element reveals.
Playtest (ADVISORY). Verifies H.1.3, H.2.3.

---

### J.13 — Coverage Summary Table

| Surface | ACs | Evidence Tier | Gate Level |
|---|---|---|---|
| J.1 Pure Function Determinism & Contract | AC-LR1 – AC-LR5 | Unit (Logic) | BLOCKING |
| J.2 Scrap Formula & DSBonus Bands | AC-LR6 – AC-LR11 | Unit (Logic) | BLOCKING |
| J.3 Fuel Grant Beacon Gating | AC-LR12 – AC-LR14 | Unit (Logic) | BLOCKING |
| J.4 Card Offer Delegation & Pity | AC-LR15 – AC-LR20 | Unit + Integration (Logic/Integration) | BLOCKING |
| J.5 Part Drop Cooldown & Sampling | AC-LR21 – AC-LR25 | Unit (Logic) | BLOCKING |
| J.6 Weight Modifier Additive Overlays | AC-LR26 – AC-LR28 | Unit (Logic) | BLOCKING |
| J.7 ContentWarning Fallback & Edge Cases | AC-LR29 – AC-LR36 | Unit (Logic) | BLOCKING |
| J.8 Persistence & Save/Load Round-trip | AC-LR37 – AC-LR39 | Integration/Playtest | BLOCKING |
| J.9 Upstream/Downstream Dependency Contracts | AC-LR40 – AC-LR43 | Integration (📌 cross-system) | BLOCKING |
| J.10 Tuning Knob Safe Ranges | AC-LR44 – AC-LR46 | Unit — SO validator tests (Config) | ADVISORY |
| J.11 Telemetry Events Emitted | AC-LR47 – AC-LR49 | Integration | BLOCKING |
| J.12 UI Integration Smoke | AC-LR50 – AC-LR55 | Interaction test / Playtest / Smoke (UI, Visual) | ADVISORY |
| **Total** | **AC-LR1 – AC-LR55 (55 ACs)** | Unit: 38 · Integration: 11 · Playtest: 3 · Smoke: 3 | — |

**📌 Cross-system AC dependencies requiring upstream contract fulfillment before verification:**
- AC-LR40: Enemy System must confirm `CombatEndedPayload.DifficultyScore` field shape.
- AC-LR41: Scrap Economy must expose `IScrapEconomy.GrantScrap(amount, source)` interface.
- AC-LR43: V&P must expose `IPartCatalog.GetParts(slot, rarity)` and `InstallPart(slot, part)` interfaces (soft retrofit, F.3).
- AC-LR37/AC-LR39: Save & Persistence must implement `LootStateDTO` serializer (undesigned, 📌 `C5-SAVE`).

## Open Questions

### K.0 — Forward Open Questions Overview

Section K surfaces unresolved questions that emerged during the Loot & Reward design pass. The list is split into two tiers: **design-resolvable** questions answerable at next-phase GDD authoring or implementation (no runtime data needed), and **telemetry-gated** questions that require Early Access playtest data from the G.4 telemetry plan before they can be resolved. No upstream OQs were carried into this GDD; the list is net-new.

---

### K.1 — Design-Resolvable OQs

**OQ-LR1 — Event-node override payload schema.**
AC-LR13 and F.2 reference a `FuelGrantOverride` field on `RewardContext` that Event nodes may supply to replace the default `rng.Next(1, 4)` Fuel draw. The full Event-payload schema (which fields override which L&R defaults, how scrap overrides interact with DSBonus, whether an Event can override CardOffer or PartDrop) is not authored here — it is the Node Encounter GDD's ownership. L&R must publish a `RewardContextOverride` contract before Node Encounter is designed.
**Resolution path:** author during Node Encounter GDD (Row 10). Blocks: implementation of Event-beacon reward integration.
**Owner:** Node Encounter GDD + L&R (joint).

**OQ-LR2 — Pity counter persistence across runs.**
C.5 specifies `RarePityCounter` is persisted in `LootStateDTO` but does not specify whether the counter resets when a run ends (death, Haven victory) or carries across meta-runs. Section B's fantasy ("the road keeps its own books") could support either interpretation — a per-run ledger or a per-save ledger. Per-save ledger strengthens the "road remembers you" reading; per-run ledger is the simpler save-schema contract.
**Resolution path:** resolve during Meta Progression GDD (not yet designed) OR decide now as a standalone design call. Recommended: per-run reset to keep the pity guarantee bounded to a single ledger cycle. Per-save persistence risks compounding pity debt across many runs and diluting the "debt settled" moment.
**Owner:** L&R + Meta Progression (joint).

**OQ-LR3 — Wildcard PartDrop biome-specific rarity weights.**
D.5 locks `PartRarityWeight = {Common:60, Uncommon:30, Rare:10}` globally. Biome-3 may warrant a Rare-weighted pool to reinforce the "late-run, high-stakes" tone (Pillar 5: Route Reflects Vehicle State). A biome-keyed weight table is additive; it does not break the existing contract.
**Resolution path:** resolve during Biome-Specific Content GDD or via G.4 `PartDropOffered` telemetry bucket-by-biome at EA. Recommended: keep global for MVP; re-evaluate post-EA via OQ-LR7.
**Owner:** L&R + Biome design.

**OQ-LR4 — Card System not-yet-implemented (pre-alpha) fallback behavior.**
EC-LR6 and AC-LR34 handle the case where `ICardRewardGenerator.Generate` returns null/empty at runtime, but do not specify the pre-alpha stub behavior (i.e., when Card System is not wired up at all and `ICardRewardGenerator` is unbound). Options: (a) throw at service-resolution time so L&R fails loudly in development builds, (b) return an auto-pity `ScrapGrantOffer(40)` silently, (c) emit a placeholder "card offer (content pending)" visible in dev builds only.
**Resolution path:** resolve during L&R implementation (lead-programmer decision with input from Card System owner). Recommended: (a) fail loudly — L&R is not shipped standalone; if Card System is absent, the build is broken.
**Owner:** lead-programmer + Card System owner.

---

### K.2 — Telemetry-Gated OQs (Post-EA Resolution)

**OQ-LR5 — Is the 8-offer pity threshold right?**
C.3 step 7 and G.1 lock the pity threshold at 8 card offers. Section B's fantasy requires the player to experience **suspicion → verification → trust** — the threshold must be low enough that pity fires observably within a typical run, but high enough that it feels earned rather than routine. G.4 `PityFallbackFired` telemetry + player sentiment data (post-EA surveys, Discord feedback) are required to validate.
**Resolution path:** G.4 post-EA tuning trigger `PityFallbackFired` rate analysis. Re-tune to 6, 10, or 12 based on observed run-length distributions and player sentiment. Safe retune range: [6, 12].
**Owner:** economy-designer + community-manager (post-EA).

**OQ-LR6 — Is `TruckRewardMultiplier = 1.25` the correct floor for the Truck chassis identity?**
G.3 locks the multiplier floor at 1.25. The Truck chassis fantasy ("slow but richer") may need a stronger multiplier (1.30 – 1.40) if EA playtest shows Truck runs trail Scout/Assault in average Scrap-per-hour. Conversely, 1.25 may be too generous if Truck runs outpace the other chassis on total resources.
**Resolution path:** G.4 `ScrapGranted` telemetry bucketed by chassis; compare Scrap/hour across chassis at EA. Retune multiplier if chassis-parity gap exceeds ±15%. Safe retune range: [1.20, 1.40]. Any change below 1.20 requires re-reviewing Truck's cost-of-play (speed, slot count) elsewhere.
**Owner:** economy-designer.

**OQ-LR7 — DSBonus band shape: 2-band piecewise-linear vs. 3-band curve.**
D.2 / D.3 specify a 2-band piecewise-linear `DSBonus` (floor at DS < 0.40 with +4; linear ramp to +12 at DS = 1.00). A 3-band variant (floor + competent + brutal, with two inflection points) could better reward players who consistently punch above their weight at mid-high DS without over-rewarding max-DS edge cases. The 2-band shape is simpler to reason about and explain in playtest feedback.
**Resolution path:** G.4 `ScrapGranted.dsBucket` distribution at EA; if Brutal+Maximum buckets show compressed rewards (players don't feel the difference between DS=0.85 and DS=1.00), re-tune to 3 bands. Requires retune of D.2 formula and D.3 envelope table.
**Owner:** systems-designer + economy-designer.

**OQ-LR8 — Diegetic pity-counter visibility.**
Section B's fantasy requires pity to be **hidden until it fires** — the "suspicion → verification" arc depends on the player not knowing the exact threshold. A diegetic visualization (FTL-style "stored debt" icon counting up alongside Scrap) would increase predictability but could break the verification arc by converting suspicion into certainty. Post-EA player sentiment data is required to resolve: do players find the hidden counter frustrating, or does revealing it undermine the "debt settled" payoff?
**Resolution path:** EA playtest sentiment analysis + A/B test candidate (half of players see a diegetic counter, half do not). Metric: run-retention rate and "did pity feel earned?" survey score. Conservative default: keep hidden per B's locked framing until data contradicts.
**Owner:** creative-director + community-manager + analytics-engineer (post-EA).

---

### K.3 — OQ Summary Table

| OQ | Tier | Primary Owner | Blocks |
|---|---|---|---|
| OQ-LR1 Event-node override payload | Design-resolvable | Node Encounter GDD | Event-beacon reward integration |
| OQ-LR2 Pity counter persistence | Design-resolvable | Meta Progression GDD | LootStateDTO schema finalization |
| OQ-LR3 Biome-specific part rarity weights | Design-resolvable (or telemetry-deferrable) | L&R + Biome design | Biome-3 scarcity tone (post-EA can decide) |
| OQ-LR4 Pre-alpha Card System stub | Design-resolvable | lead-programmer | L&R implementation start |
| OQ-LR5 Pity threshold calibration | Telemetry-gated | economy-designer | Post-EA retune cycle 1 |
| OQ-LR6 TruckRewardMultiplier floor | Telemetry-gated | economy-designer | Post-EA chassis parity retune |
| OQ-LR7 DSBonus band shape | Telemetry-gated | systems-designer | Post-EA reward-curve retune |
| OQ-LR8 Diegetic pity-counter UI | Telemetry-gated | creative-director | Post-EA A/B test candidacy |

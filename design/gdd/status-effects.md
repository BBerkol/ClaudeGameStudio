# Status Effect System

> **Status**: In Design
> **Author**: Bertan Berkol + Claude Code agents
> **Last Updated**: 2026-04-20
> **Implements Pillar**: Pillar 3 (Read to Win), Pillar 2 (Chassis Identity)

## Overview

The Status Effect System owns the `StatusType` enum and all resolution logic for named status conditions in Wasteland Run. It defines what each status does, how long it persists, how stacks accumulate and are consumed, and what conditions trigger expiry. Card System triggers status application via `ApplyStatusEffectSO` but does not define behavior — Status Effect System is the sole authority on what `Stalled`, `Redirected`, and any future status type means at resolution time. Card Combat System queries status state via `StatusConditionSO.IsMet(context)` to gate conditional card effects.

The system is a pure data-and-logic layer with no scene presence: status state lives on the Vehicle POCO (as a dictionary or list of active `StatusInstance` records), and resolution is invoked by the Card Combat pipeline at defined tick points (turn start, turn end, card play). The EA status set consists of at minimum two types committed by the Card System GDD — `Stalled` and `Redirected` — plus any additional types required by card design. Status Effect System is a prerequisite for Card Combat System and Enemy System; neither can be designed without the `StatusType` enum and tick/expiry contract defined here.

## Player Fantasy

The enemy's engine coughs black smoke — **Stalled**, two turns. You've seen this before. A lesser driver would punch the accelerator and burn their hand on the damage cards. You're not that driver. You pull the Control card from your hand like a torque wrench, set the **Redirected** tag where it hurts, and watch the wasteland do the rest. Status effects are tells. Reading them is how you separate the living from the roadkill.

This system serves Pillar 3 (Read to Win). Every status condition is a piece of information the player reads before committing to an action — active statuses on the enemy's vehicle change which cards are optimal, which are wasteful, which are bait. A **Stalled** enemy turns a defensive hand into an offensive one. A **Redirected** enemy turns your most expensive damage card into a setup play you no longer need. The player who understands what each status does, and which cards gate their best effects behind status conditions, is the player who wins. Players who ignore the status display are burning energy they don't have.

The competence this creates is accumulative — it grows across runs, not just within them. The first time you see **Stalled** in the wasteland, you play through it. The tenth time, you play *around* it. The hundredth time, you don't let the enemy's next move matter at all. You were holding the damage cards for a kill. By then, you're holding them for a *verdict*.

## Detailed Design

### Core Rules

**R1 — Status Type Definitions (EA Set)**

The `StatusType` enum is owned by this system. Card System, Card Combat System, and Enemy System reference it but do not extend it.

| StatusType | Class | Effect |
|---|---|---|
| `Stalled` | Binary | Affected vehicle cannot play attack-family cards this turn. Support, Positioning, and Repair cards are unaffected. |
| `Redirected` | Binary | When the affected vehicle plays an attack card, the target subsystem is re-rolled randomly from all opposing subsystems at the moment of resolution. The intended target is discarded. |
| `Corroded` | Graduated | Designated subsystem takes `Stacks` bonus damage from each attack that targets it, for `Duration` turns. |
| `Burning` | Graduated | At the start of the affected vehicle's turn (before duration tick — fire-before-tick per R3), the vehicle's Frame subsystem takes `Stacks` damage. Burning ticks carry `DamageSource.Status` and bypass both Corrode bonus and Armor absorption — `CurrentArmor` is NOT consumed. See F2 and Card Combat F-CC1 DOT exception. |
| `Stunned` | Graduated | At the start of the affected vehicle's turn (before duration tick — fire-before-tick per R3), set `vehicle.StunnedSkipsRemaining = max(StunnedSkipsRemaining, Stacks)`. During the card-play phase, while `StunnedSkipsRemaining > 0` each card-play attempt is consumed without resolving: the selected card is discarded, no energy is spent (full refund), no `OnCardPlayed` fires, and `StunnedSkipsRemaining` decrements by 1. Once `StunnedSkipsRemaining == 0`, normal card-play resumes for the remainder of the turn. `StunnedSkipsRemaining` is reset to 0 at end of turn — does NOT carry over to subsequent turns. Stunned does not filter by `CardFamily` — it consumes any card-play attempt (contrast with Stalled, which suppresses only attack-family cards). |
| `Marked` | Binary | One-shot Frame-damage amplifier. While `Marked` is active on a vehicle, the next damage event that targets that vehicle's Frame slot — from any source (card attack, status DOT, environment) — has its incoming damage value increased by `MarkedBonus` (R5, default 3) before the F-CC1 damage pipeline runs. The `Marked` instance is then consumed (removed from `ActiveStatuses`) regardless of whether any of the bonus damage reaches Frame Hp (Armor absorbing the full bonus still consumes the marker — the marker is a one-shot, not a damage-gate). `RemainingDuration` ticks normally at turn-start (R3) and the marker expires by duration if not consumed before duration reaches 0. Marked has no `Stacks` field (Binary class — see R2). Marked does not fire at turn-start; it is a resolution-time amplifier consumed by the first Frame-targeted damage event. |

*"Attack-family cards" for Stalled purposes: cards whose `CardFamily` field is `Precision`, `Assault`, or `Maneuver`. Control and Repair family cards are not suppressed by Stalled — Control cards may still be played even though they contain a required minimum-damage effect (Card System rule). This definition is by `CardFamily` field, not by effect content.*

---

**R2 — StatusInstance Runtime Model**

Status state lives on the Vehicle POCO as a list of `StatusInstance` records. No status state resides on MonoBehaviours.

| Field | Type | Description |
|---|---|---|
| `StatusType` | `StatusType` enum | Which status this instance represents |
| `RemainingDuration` | `int` | Turns until expiry; decremented at start of the affected vehicle's turn |
| `Stacks` | `int` | Current stack count; consumed or accumulated per type's merge rule |
| `SourceVehicleId` | `string` | Identity of the vehicle that applied this status. Reserved for post-EA card effects; included at no authoring cost. |

`ApplyStatusEffectSO` input fields (`Duration`, `Stacks`) set the initial values; `RemainingDuration` and `Stacks` on the instance are mutable runtime state.

---

**R3 — Tick Point and Resolution Sequence**

Duration ticks at the **start of the affected vehicle's turn**, before any cards are played.

Per-turn resolution order for each vehicle's turn:

1. **Apply turn-start effects**: for each active `StatusInstance` on this vehicle, invoke the type's `OnTurnStart` handler:
   - `Stalled` → set `vehicle.AttackSuppressed = true` for this turn.
   - `Burning` → call `IVehicleMutator.ApplyDamage(SlotType.Frame, instance.Stacks, DamageSource.Status)`. Because `DamageSource == Status`, Card Combat F-CC1 step 1 (Corrode bonus) and step 2 (shield absorption — Armor on Frame path, Plating on non-Frame path) are both bypassed. Frame Hp decreases by `instance.Stacks` directly, floored at 0. `vehicle.CurrentArmor` is NOT consumed. `OnCurrentArmorChanged` does NOT fire.
   - `Stunned` → set `vehicle.StunnedSkipsRemaining = max(vehicle.StunnedSkipsRemaining, instance.Stacks)`. The counter governs card-play behavior during step 4: while `StunnedSkipsRemaining > 0`, each card-play attempt is consumed without resolving (card discarded, energy refunded, no `OnCardPlayed` fire), and the counter decrements. Counter resets to 0 at end of turn — Stunned skips do not bank across turns.
   - `Corroded`, `Redirected`, `Marked` → no turn-start effect (fire at damage-resolution time; Marked is consumed by the first Frame-targeted damage event during step 5 or by Burning's step 1 Frame damage call, whichever comes first this turn).
2. **Tick**: decrement `RemainingDuration` by 1 on all active `StatusInstance` records on this vehicle.
3. **Expire**: remove all instances where `RemainingDuration == 0`.
4. **Card play phase**: vehicle plays cards normally, subject to `AttackSuppressed` if `Stalled` is active.
5. **Apply resolution-time effects** (during card resolution, not at turn boundaries):
   - `Corroded` → when an attack targets the Corroded subsystem, add `instance.Stacks` to incoming `DamageOutput` before it reaches `IVehicleMutator`.
   - `Redirected` → when an attack is declared, re-roll target subsystem from the full opposing subsystem pool at that moment, substitute for the intended target.
   - `Marked` → when a damage event resolves against the vehicle's Frame slot, add `MarkedBonus` (R5, default 3) to the incoming damage value before F-CC1 runs, then remove the `Marked` instance from `ActiveStatuses`. Order with `Redirected`: `Redirected` resolves first (target substitution), then `Marked` evaluates against the final post-substitution target — if the resolved target is Frame, the Marked bonus fires and the marker is consumed; if the resolved target is not Frame, the marker is NOT consumed (it remains active until a Frame-targeted damage event occurs or its duration expires). Order with Burning's turn-start tick: a Frame-targeted Burning tick during step 1 consumes Marked before step 5 runs — Marked is one-shot regardless of source.

*Fire-before-tick order ensures `Duration` is a direct count of how many turns the effect applies. A `Duration=1` Burning fires exactly once on the affected vehicle's first turn, then expires. A `Duration=1` Stalled suppresses exactly one turn of attacks.*

Both vehicles tick their own statuses at the start of their own turn. Player goes first; enemy turn follows.

---

**R4 — Merge Rules (Re-application of Active Status)**

When `ApplyStatusEffectSO` fires for a `StatusType` already active on the target vehicle:

| Status Class | Merge Rule | Behavior |
|---|---|---|
| Binary (`Stalled`, `Redirected`, `Marked`) | **Refresh** | `RemainingDuration` is set to `max(RemainingDuration, incoming Duration)`. Duration can only increase; a weaker re-application never shortens an existing status. For `Marked`: a second application onto an already-active Marked instance refreshes duration only; the bonus does not stack (Binary class). |
| Graduated (`Corroded`, `Burning`, `Stunned`) | **Extend** | `Stacks` increases by the incoming `Stacks` value, capped at the per-type maximum (3). `RemainingDuration` resets to the incoming `Duration` value. |

*`Corroded` per-subsystem instances:* a second `Corroded` application naming a different subsystem creates a new independent instance on that slot — two Corroded instances on two different subsystems are permitted simultaneously. Re-application on the same subsystem uses the Extend merge rule.

---

**R5 — Duration and Stack Caps**

| Parameter | Cap | Reason |
|---|---|---|
| Maximum `RemainingDuration` (any type) | 3 turns | Prevents re-application chaining beyond tactical relevance. Refresh-model types cannot lock an enemy for more than 3 turns. |
| `MarkedBonus` (flat damage added to the consuming Frame-damage event) | 3 | Default value for the Marked-status amplifier. See Tuning Knobs row for safe range. |
| Maximum `Stacks` (graduated types) | 3 | Prevents explosive Corroded + burst-damage combinations before damage ceiling is validated against Card Combat System HP ranges. |
| Minimum `Duration` at application | 1 | Enforced at SO import — `Duration = 0` is a data authoring error and must not compile. |
| Minimum `Stacks` at application | 1 | Enforced at SO import — `Stacks = 0` on a `Graduated` type would produce a no-op Extend merge (duration reset, no damage potential added). |

---

**R6 — Enrage Counter Independence**

The Card Combat System's time-pressure mechanic (Enrage or equivalent escalation) **must tick at the combat-loop level**, not at any vehicle's `BeginTurn` call. The Status Effect resolution pipeline has no read or write access to the Enrage counter. `Stalled` on an enemy vehicle does not delay, pause, or influence the Enrage threshold.

*Forward dependency for Card Combat System GDD: "Enrage counter is a combat-scope counter incremented after both vehicles complete their turns. It is not part of any vehicle's per-turn resolution pipeline. Stalled does not pause it."*

### States and Transitions

A `StatusInstance` passes through three states during its lifetime:

| State | Description | Entry Condition | Exit Condition |
|---|---|---|---|
| **Inactive** | Not present on the vehicle. | Default state; after expiry. | `ApplyStatusEffectSO` fires with valid `Duration >= 1`. |
| **Active** | Present on the vehicle's status list; `RemainingDuration >= 1`. | Created by `ApplyStatusEffectSO`. | `RemainingDuration` ticks to 0 at the start of the affected vehicle's turn. |
| **Expired** | `RemainingDuration == 0` after a tick. Removed from the list immediately in the same tick pass. | Tick step reduces `RemainingDuration` to 0. | Immediately removed — there is no "expired but still present" state. |

**Merge transitions** (re-application while Active):

| Incoming Apply | Current State | Result |
|---|---|---|
| Binary type (Stalled/Redirected/Marked) | Inactive | New instance created → Active. |
| Binary type (Stalled/Redirected/Marked) | Active | Refresh: `RemainingDuration` replaced → remains Active. Marked bonus does not stack. |
| Graduated type (Corroded/Burning) | Inactive | New instance created → Active. |
| Graduated type (Corroded/Burning) | Active, same target slot | Extend: `Stacks` increased (capped at 3), `RemainingDuration` reset → remains Active. |
| `Corroded`, new target slot | Active on different slot | New independent instance created on new slot → both instances Active. |

**Vehicle status list lifecycle:**

```
Combat Start:    vehicle.ActiveStatuses = [] (empty)
During combat:   instances added/merged/ticked per R3–R4
Combat End:      vehicle.ActiveStatuses cleared entirely
```

Status effects do not persist between combat nodes. A vehicle that ends combat with `Burning 2` starts the next node's combat with a clean status list.

### Interactions with Other Systems

| System | Interface | Direction | Notes |
|---|---|---|---|
| **Card System** | `ApplyStatusEffectSO` passes `StatusType`, `Duration`, `Stacks` to trigger application. `StatusConditionSO` queries this system: "is `StatusType` X active on vehicle Y?" — answered from the vehicle's active status list. | Card System → Status Effects (apply + query) | Card System does not define what statuses do. `StatusType` enum must be available before any status-applying card SO can be authored. |
| **Card Combat System** | Invokes the Status Effect resolution pipeline at turn boundaries: `BeginTurn(vehicle)` triggers tick, expiry, and turn-start effects; card resolution triggers resolution-time effects (`Corroded`, `Redirected`). Card Combat System reads `vehicle.AttackSuppressed` (set by `Stalled`) to gate card play. Enrage counter is independent — not routed through this system. F-CC1 DOT exception is owned by Card Combat: when `DamageSource == Status` reaches the pipeline, step 1 (Corrode bonus) and step 2 (shield absorption — Armor on Frame path, Plating on non-Frame path) are both skipped. Burning relies on this exception. | Bidirectional: Card Combat drives timing; Status Effect drives vehicle state via `IVehicleMutator`. | Card Combat GDD must define: (1) the exact `BeginTurn` call site; (2) Enrage as a combat-scope counter decoupled from per-vehicle turn pipeline; (3) the F-CC1 DOT exception (Card Combat F-CC1 "DOT exception" section). |
| **Vehicle & Part System** | `Burning` and `Corroded` write to vehicle state via `IVehicleMutator` (damage intake). `StatusConditionSO` reads via `IVehicleView`. `vehicle.ActiveStatuses` list is a field on the Vehicle POCO, owned by Vehicle & Part System GDD. Burning requires 4-slot model (Weapon/Engine/Mobility/Frame) and `DamageSource.Status` discriminator to trigger F-CC1 DOT bypass. | Bidirectional: Status Effect reads vehicle state; mutators write it. | `IVehicleMutator.ApplyDamage(SlotType, int, DamageSource)` is the authoritative signature (V&P R9). Burning calls it with `(SlotType.Frame, Stacks, DamageSource.Status)`. Corroded does not call the mutator directly — it adds `Stacks` to an incoming attack's `DamageOutput` at resolution; the attack's own mutator call carries its own `DamageSource` (`Card` or otherwise). `CurrentArmor` is not read or written by this system. |
| **Enemy System** | Enemies apply statuses to the player vehicle using the same `ApplyStatusEffectSO` contract. Enemy AI decides when and which statuses to apply; Status Effect System defines what they do. Enemy System reads active statuses on player vehicle via `IVehicleView` for AI targeting decisions. **NoOpIntent render path (retrofit 2026-04-23):** when an enemy resolves a `NoOpIntent` sentinel (all-slots-Offline degenerate edge, malformed BrainRuleset, etc. — Enemy System C.11 + E), Status Effect pipeline hooks fire as follows: (a) turn-boundary ticks at `BeginTurn(vehicle)` — Burning damage, duration decrement, expiry — fire normally; status time advances regardless of whether the turn's intent resolves to NoOp; (b) resolution-time hooks (Corroded bonus, Redirected reroute, F-CC1 DOT exception) do NOT fire — there is no damage/effect chain to hook into; (c) `ApplyStatusEffectSO` calls embedded in a NoOp chain are absent by construction (NoOp has no payload, so no status application can be triggered). Render: status chips display unchanged; no special visual treatment for NoOp-intent enemy turns. Matches Enemy System C.11 ("Resolving receives NoOp and produces no effect") and Card Combat's `BeginTurn` turn-boundary ownership. | Enemy System → Status Effects (apply); bidirectional for queries. | Enemy System GDD must reference the `StatusType` enum and query interface. NoOpIntent interaction formalized via Enemy System's Resolving→no-effect contract — no special casing in this system. |
| **Save & Persistence System** *(indirect)* | No direct interface. Under the passive serializer pattern (systems-index Scoping Note C4), Save & Persistence has no reference to this system — it serializes whatever `VehicleStateDTO` Vehicle & Part System declares. Status state persists only because `ActiveStatuses` is a field on the Vehicle POCO, and serialization is handled by Vehicle & Part's DTO contract. Because statuses clear at combat end, `ActiveStatuses = []` is the expected serialized value at every valid save point. | Indirect: Status Effect → Vehicle & Part (field ownership) → Save & Persistence (generic serialization) | No special serialization logic required by this system. Requirement flows through Vehicle & Part GDD. |

## Formulas

### F1 — Corroded Bonus Damage

The bonus damage a `Corroded` status instance contributes to an attack targeting the affected subsystem.

```
CorrodeBonus = Stacks
FinalDamage  = DamageOutput + CorrodeBonus
```

**Variables:**

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Active stack count | `Stacks` | int | 1–3 | Stack count on the `Corroded` instance at the moment the attack resolves. Capped at 3 (R5). |
| Card damage output | `DamageOutput` | int | 1–20 | Resolved card damage before Corrode applies (BaseDamage + PositionBonus if applicable). Source: Card System F1. |
| Corrode bonus | `CorrodeBonus` | int | 1–3 | Flat bonus damage added to the incoming attack. Equals `Stacks`. |
| Final damage to subsystem | `FinalDamage` | int | 2–23 | Total damage passed to `IVehicleMutator` for this attack. |

**Output Range:** `CorrodeBonus` is 1–3. Combined `FinalDamage` is bounded by `DamageOutput` range + 3 maximum.

**Amplification reference (vs. typical mid-game Precision card ≈ 10 dmg):**

| Stacks | CorrodeBonus | Amplification |
|---|---|---|
| 1 | +1 | +10% |
| 2 | +2 | +20% |
| 3 | +3 | +30% |

Design target: max stacks amplifies a mid-game Precision card by 20–40%. At 3 stacks on a 10-damage card, +30% is within the target window.

**Example:** Scout Uncommon Precision card — `BaseDamage = 6`, `PositionBonus = 4` (condition met). Enemy Engine has `Corroded, Stacks = 2`.

```
DamageOutput = 6 + 4 = 10
CorrodeBonus = 2
FinalDamage  = 10 + 2 = 12
```

---

### F2 — Burning Damage

The damage a `Burning` status instance deals to the affected vehicle's Frame subsystem each tick, and the total across its duration.

Burning damage bypasses Armor entirely. Per Card Combat F-CC1 DOT exception: when the damage pipeline receives a tick with `DamageSource == Status`, step 1 (Corrode bonus) and step 2 (shield absorption — Armor on Frame path, Plating on non-Frame path) are both skipped. Frame Hp decreases directly; `CurrentArmor` is not consumed.

```
BurningDamage_per_tick = Stacks                    (direct Frame Hp loss — no Armor absorption)
TotalBurningDamage     = Stacks × Duration         (snapshot — see EC-S9 for merge caveat)
```

**Variables:**

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Stack count | `Stacks` | int | 1–3 | Active stack count on the `Burning` instance when the tick fires. |
| Duration | `Duration` | int | 1–3 | Number of turns the effect fires. Under the fire-before-tick resolution order (R3), Duration equals the exact number of tick-damage events. |
| Damage per tick | `BurningDamage_per_tick` | int | 1–3 | Frame HP lost per tick. |
| Total damage | `TotalBurningDamage` | int | 1–9 | Total Frame HP lost across all ticks. |

**Output Range:** 1 (Stacks=1, Duration=1) to 9 (Stacks=3, Duration=3).

**Total damage matrix:**

| | Duration = 1 | Duration = 2 | Duration = 3 |
|---|---|---|---|
| **Stacks = 1** | 1 | 2 | 3 |
| **Stacks = 2** | 2 | 4 | 6 |
| **Stacks = 3** | 3 | 6 | 9 |

**Frame HP pressure (estimated 20 HP baseline — Vehicle & Part System GDD defines authoritative values):**

| Scenario | Total Damage | % of 20 HP |
|---|---|---|
| Stacks=1, Duration=2 | 2 | 10% — background pressure |
| Stacks=2, Duration=2 | 4 | 20% — meaningful threat |
| Stacks=3, Duration=2 | 6 | 30% — urgent |
| Stacks=3, Duration=3 | 9 | 45% — critical; player must repair or push for a win |

Design target: `BurningDamage_per_tick` alone must not exceed 50% of Frame HP per affected turn. At max stacks (3 damage/turn vs. 20 HP baseline), this is 15% per turn — comfortably within the cap.

**Example:** Enemy applies `Burning, Stacks=2, Duration=3` to a player vehicle with `CurrentArmor = 5`, `MaxArmor = 10`. Over the next three player turns:

```
Turn 1: Burning fires → Frame takes 2 damage (Armor bypassed; CurrentArmor stays 5). Duration 3→2.
Turn 2: Burning fires → Frame takes 2 damage (Armor bypassed). Duration 2→1.
Turn 3: Burning fires → Frame takes 2 damage (Armor bypassed). Duration 1→0. Instance expires.
TotalBurningDamage = 2 × 3 = 6 (all 6 hit Frame Hp directly; CurrentArmor unchanged at 5)
```

### F3 — Marked Bonus Damage

The flat bonus added to the first Frame-targeted damage event a `Marked` vehicle receives while the marker is active.

```
MarkedBonus  = 3                                   (Binary, no Stacks — constant from R5)
FinalDamage  = IncomingDamage + MarkedBonus        (added before F-CC1 runs)
```

After `FinalDamage` is computed, the `Marked` instance is removed from `ActiveStatuses` regardless of how F-CC1 partitions the damage between Armor and Frame Hp.

**Variables:**

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Incoming damage value | `IncomingDamage` | int | 0–25 | Damage value declared by the source event (card `DamageOutput`, Burning tick `Stacks`, environment damage). |
| Marked bonus | `MarkedBonus` | int | 3 | Flat bonus added when the resolved target is Frame and Marked is active. Constant per R5 cap. |
| Final damage to Frame pipeline | `FinalDamage` | int | 3–28 | Value passed into F-CC1 step 0 for the Frame path. |

**Source-event matrix:**

| Source event | `IncomingDamage` example | Marked fires? | Notes |
|---|---|---|---|
| Card attack targeting Frame | `DamageOutput = 6` | Yes | F-CC1 Frame path then runs against `6 + 3 = 9`. |
| Card attack targeting non-Frame slot | `DamageOutput = 6` | No | Marker remains active; consumed by a later Frame event. |
| Burning tick (step 1) | `Stacks = 2` | Yes | Tick value `2` becomes `5`. F-CC1 still bypasses Armor (DOT exception). Marked is consumed. |
| Redirected re-roll lands on Frame | `DamageOutput = 6` | Yes | Marked fires AFTER Redirected substitution (R3 ordering). |
| Redirected re-roll lands on non-Frame | `DamageOutput = 6` | No | Marker remains. |

**Example:** Player vehicle has `Marked` (RemainingDuration=1). Enemy plays a Ram card with `DamageOutput = 10` targeting Frame. Player has `CurrentArmor = 5`.

```
IncomingDamage = 10
MarkedBonus    = 3
FinalDamage    = 10 + 3 = 13
F-CC1 Frame path: Armor 5 absorbs 5 → Frame Hp loses 8.
Marked is consumed (removed from ActiveStatuses).
```

## Edge Cases

- **EC-S1 — Stalled active; enemy has no attack-family cards in hand**: `Stalled` ticks down normally. Its effect is that `Precision`, `Assault`, and `Maneuver` cards cannot be played — if the enemy holds none, the status is present but has no observable effect. Duration still decrements at the start of the affected vehicle's turn. Stalled does not detect enemy intent and does not preserve itself.

- **EC-S2 — Redirected fires; all opposing subsystems are Offline**: The re-roll uses the full opposing 4-slot pool (Weapon / Engine / Mobility / Frame — per V&P 4-slot model) regardless of damage state. The attack resolves against a random slot. If the re-roll lands on Frame, the attack follows the F-CC1 Frame path (CurrentArmor absorbs first, then Frame Hp); if it lands on a non-Frame slot, it follows the non-Frame path (PlatingStacks absorbs first, then slot Hp). Redirected does not cancel when no live targets exist — it redirects unconditionally. Whether damage to an Offline subsystem has any effect is determined by the Card Combat System GDD. *Subsystem-strike cards (`DamageEffectSO.BypassPlating = true`) cannot target Frame (Card Combat OQ-CC-NEW-1 resolution); Redirected applied to a subsystem-strike attack still re-rolls across the 4-slot pool and may land on Frame, but the Frame re-roll falls outside the subsystem-strike targeting contract — resolution of this edge case is a Card System GDD forward dependency.*

- **EC-S3 — Multiple status types active simultaneously; resolution order**: Status effects resolve independently. Within Step 1 (turn-start effects), instances are processed in insertion order. Within Step 5 (resolution-time effects), `Redirected` resolves first (target substitution), then `Corroded` evaluates against the final post-substitution target. If `Redirected` moves the attack away from the Corroded subsystem, the Corrode bonus does not fire. If `Redirected` moves the attack onto the Corroded subsystem, the bonus fires.

- **EC-S4 — `Duration = 0` or `Stacks = 0` at SO import**: Both are data authoring errors. `ApplyStatusEffectSO` SO import validation must enforce `Duration >= 1` and `Stacks >= 1`. An SO that fails either check must not compile.

- **EC-S5 — Redirected random re-roll lands on the same subsystem the enemy was already targeting**: Not a bug. The pool is all opposing subsystems with no exclusion of the original target. The player cannot rely on Redirected guaranteeing a different subsystem.

- **EC-S6 — Status instance persists after the card that applied it is Exhausted**: A `StatusInstance` on the Vehicle POCO is independent of card lifecycle. Exhausting the applying card does not expire the status instance. Lifetime is governed by `RemainingDuration` only.

- **EC-S7 — Degenerate full-Stall strategy**: The `max RemainingDuration = 3` cap (R5) prevents re-application from pushing Stalled beyond 3 turns. The Card Combat System's time-pressure mechanic (Enrage or equivalent) is the second line of defense. Both must be active simultaneously. This GDD owns the cap; Card Combat System GDD owns the escalation.

- **EC-S8 — `Burning` fires on the same turn it expires (`RemainingDuration = 1` at turn start)**: Per the fire-before-tick resolution order (R3): Step 1 fires the Burning handler (Frame takes damage via `DamageSource.Status`, bypassing Armor), Step 2 decrements to 0, Step 3 removes the instance. The damage fires on the final turn. A tick-before-fire implementation would cause `Duration=1` Burning to deal zero damage silently — this must be asserted in implementation tests.

- **EC-S9 — `TotalBurningDamage = Stacks × Duration` is a snapshot formula**: After any Extend merge mid-duration, the formula no longer reflects remaining output. Any UI feature showing projected Burning damage must read `instance.Stacks × instance.RemainingDuration` from live state at display time.

- **EC-S10 — F1 `FinalDamage` is a single integer passed to `IVehicleMutator`**: `FinalDamage = DamageOutput + CorrodeBonus` must be computed and passed to `IVehicleMutator.TakeDamage` as a single call. Applying `DamageOutput` first, then `CorrodeBonus` in a second call is incorrect — the bonus is part of the attack, not a separate damage event.

- **EC-S11 — Refresh re-application with weaker incoming `Duration`**: If `Stalled` is active at `RemainingDuration = 3` and a new application fires with `Duration = 1`, result is `max(3, 1) = 3`. A weaker re-application never shortens an existing stall.

- **EC-S12 — Extend merge at Stacks cap; overflow discarded**: If `Burning` has `Stacks = 2` and incoming application has `Stacks = 2`, result is `min(2 + 2, 3) = 3`. One stack is silently discarded. Card designers should be aware that re-applying near the cap wastes stacks.

- **EC-S13 — Two `Corroded` instances expire in the same tick pass**: The expiry pass (R3 Step 3) collects all instances where `RemainingDuration == 0` in a first iteration, then removes them in a second pass. Never remove from a live list during a single iteration — index shifting will skip entries.

- **EC-S14 — `Corroded` check is per targeted slot, not per vehicle**: The bonus fires only when the attack specifically targets the Corroded subsystem. An attack targeting a different slot does not consult the Corroded instance. An implementation that checks "does the target vehicle have any Corroded instance?" would incorrectly apply the bonus to off-slot attacks.

- **EC-S15 — `Corroded` active on an Offline subsystem**: The instance does not auto-expire when its subsystem goes Offline. It continues to tick and fires if an attack targets that slot. Whether damage to an Offline subsystem has any effect is a Card Combat System GDD forward dependency. Corroded expiry is duration-only — never state-triggered.

- **EC-S16 — `Burning` active when vehicle has zero Frame Hp remaining but `CurrentArmor > 0`**: Under the new Armor-as-layer model, `CurrentArmor` protects Frame from card attacks (F-CC1 Frame path) but does NOT protect Frame from Burning ticks (DOT bypasses Armor). If the vehicle reaches Frame Hp = 0, combat end is triggered regardless of `CurrentArmor` value — Armor cannot absorb a killing DOT tick, and Armor alone cannot keep the Frame alive. Implementation implication: the vehicle-defeat check after a Burning tick must fire on Frame Hp, not on `CurrentArmor`. A player with high Armor and low Frame Hp remains vulnerable to Burning — this is the intended "Armor doesn't protect against internal threats" rule from the armor stress-test memo (section 3, R-1 mitigation).

- **EC-S17 — `Marked` consumed by Armor absorption**: If Armor absorbs the full Marked-amplified damage value (`IncomingDamage + 3 <= CurrentArmor` on the Frame path), the marker is still consumed. Marked is a one-shot amplifier on the damage *event*, not a damage-gate on Frame Hp. Design rationale: any other rule creates a stalemate where high-Armor targets render Marked permanent until expiry — undesirable for tempo and predictability. The marker burns on first Frame contact.

- **EC-S18 — `Marked` consumed by a Burning tick before any card resolves**: If `Marked` and `Burning` are both active on a vehicle at turn start, R3 step 1 fires Burning first. Burning's `IVehicleMutator.ApplyDamage(SlotType.Frame, ...)` is a Frame-targeted damage event, so Marked fires and is consumed during step 1. Any subsequent card-attack on Frame this turn does NOT receive the Marked bonus — the marker is one-shot, regardless of source.

- **EC-S19 — `Marked` expires before consumption**: If `Marked` is applied with `Duration = 1` and the target vehicle takes no Frame-targeted damage during that turn, R3 step 2 decrements `RemainingDuration` to 0 and step 3 removes the instance. The bonus is never paid out. This is intended — Marked rewards committing damage in a specific window.

- **EC-S20 — `Marked` applied to a vehicle with `Frame.Hp = 0`**: A Destroyed Frame triggers vehicle defeat before any further damage can be applied. Marked is moot in this state — combat is over. The instance may be created in the same tick as defeat but never fires; combat-end cleanup clears `ActiveStatuses`.

## Dependencies

### Systems This GDD Depends On

| System | What Status Effect System Needs | When Needed | Status |
|---|---|---|---|
| **Card System** | `CardFamily` enum (Stalled suppression scope: `Precision`, `Assault`, `Maneuver`); `ApplyStatusEffectSO` contract defines inbound parameters (`StatusType`, `Duration`, `Stacks`); `StatusConditionSO` defines the query interface this system must answer. | `StatusType` enum must be published before any status-applying card SO can be authored. `CardFamily` enum must be stable before R1's Stalled scope is implemented. | ✓ Approved |

### Systems That Depend On This GDD

| System | What They Need | Specific Interface | Status |
|---|---|---|---|
| **Card Combat System** | `StatusType` enum; `StatusInstance` runtime model; resolution pipeline call sites (`BeginTurn`, resolution-time hooks); `vehicle.AttackSuppressed` field set by `Stalled`; Enrage counter decoupling rule (must be a combat-scope counter, not routed through vehicle's per-turn pipeline). | Must integrate `BeginTurn(vehicle)` → fire/tick/expire sequence. Must define Enrage as independent of this system. | ✓ Approved (2026-04-21) |
| **Vehicle & Part System** | `vehicle.ActiveStatuses` list as a field on the Vehicle POCO. `IVehicleMutator.ApplyDamage(SlotType, int, DamageSource)` authoritative signature (V&P R9) — `DamageSource.Status` required for Burning. `IVehicleView` must expose a status query ("is `StatusType` X active on vehicle Y?"). | `ActiveStatuses: List<StatusInstance>` on Vehicle POCO. `ApplyDamage(SlotType slot, int amount, DamageSource source)` on `IVehicleMutator`. | ✓ Approved |
| **Enemy System** | `StatusType` enum; `ApplyStatusEffectSO` contract (enemies apply statuses via the same path as cards). `IVehicleView` status query for AI targeting decisions. `NoOpIntent` render path (see Interactions row above): no special casing required — `BeginTurn` ticks continue, resolution-time hooks no-op by absence of effect chain. | No separate enemy-side status API — enemies use the same `ApplyStatusEffectSO` contract. NoOpIntent contract honored via turn-boundary ownership (retrofit 2026-04-23). | ✓ Approved (2026-04-23) |

*Note on Save & Persistence*: Under the passive serializer pattern (systems-index Scoping Note C4), Save & Persistence does not directly depend on this system. Status persistence flows indirectly: Vehicle & Part System owns the Vehicle POCO and its `VehicleStateDTO`, which contains the `ActiveStatuses` field. Save & Persistence serializes the DTO without knowledge of its payload. All serialization requirements — that `StatusInstance` fields (including `SourceVehicleId`) survive round-trip, and that `ActiveStatuses = []` is the expected value at every valid save point — are requirements on the Vehicle & Part GDD's DTO contract, not on Save & Persistence.

### Bidirectional Consistency

Card System GDD lists this system in its Dependencies as: "`StatusType` enum — Card System's `ApplyStatusEffectSO` references it. Before any status-applying cards can be authored." Consistent with the upstream dependency above. ✓

## Tuning Knobs

| Knob | Location | Current Value | Safe Range | Gameplay Effect |
|---|---|---|---|---|
| Max `RemainingDuration` cap | Status Effect resolution constants | 3 turns | 2–4 | Lower = status effects are punchy but short; higher = Control decks can create longer lockout windows and stall risk increases. Must be re-evaluated alongside Card Combat System's Enrage turn threshold. |
| Max `Stacks` cap (Graduated types) | Status Effect resolution constants | 3 | 2–5 | Lower = Corroded and Burning are flat bonuses, low depth; higher = setup payoffs become explosive. At 5 max stacks with coefficient 1, `CorrodeBonus = 5` on a Rare Precision card deals 15+ damage — requires cross-check against Vehicle & Part System HP ranges before raising above 3. |
| `CorrodeBonus` per stack (F1 coefficient) | `StatusTypeDefinitionSO` — Corroded handler | 1 damage/stack | 1–2 | Changing to 2 shifts max Corrode bonus to +6 (+60% vs. 10-damage card). Revisit only after Vehicle HP ranges and enemy plating values are defined. |
| `BurningDamage` per stack (F2 coefficient) | `StatusTypeDefinitionSO` — Burning handler | 1 damage/stack | 1–3 | Changing to 2 makes max Burning 6/turn (30% of 20 HP Frame per turn). At 3 damage/stack, max Burning approaches lethal in 2–3 turns against a 20 HP Frame. Raise only after authoritative Frame HP is defined in Vehicle & Part System GDD. |
| `Burning` target subsystem | `StatusTypeDefinitionSO` — Burning handler | Frame (hardcoded for EA) | Frame only (EA) | Post-EA, Burning could target a specified slot. EA: Frame is the only target. Armor bypass is NOT a tuning knob — DOT bypass of Armor is a locked design decision (armor stress-test EC-3, Card Combat F-CC1 DOT exception); it must not be reverted to "Burning consumes Armor" without re-running the stress test. |
| Stalled suppressed card families | R1 definition | Precision, Assault, Maneuver | Any subset of the five families | Adding Control to the suppression set would make Stalled extremely powerful — enemies lose both damage and disruption simultaneously. Not recommended without playtesting evidence. |
| `MarkedBonus` flat damage | `StatusTypeDefinitionSO` — Marked handler (F3) | 3 damage | 2–5 | Lower = Marked is a soft tell; player can comfortably trade a turn to neutralize it via Repair. Higher = Marked becomes a high-priority "must answer this turn" tell — pushes Read-to-Win cadence faster. At 5 against a 20 HP Frame with 5 Armor, a Marked-amplified 10-damage attack deals 10 to Frame (50%). Raise only after V&P Frame HP ranges are confirmed and playtest data exists. |
| Enrage threshold (forward dependency) | Card Combat System GDD | Not yet defined | Card Combat GDD owns this | The Enrage turn count must be tuned in conjunction with the max Duration cap. If Enrage fires at turn 5 and max Duration is 3, Stalled can occupy 60% of the pre-Enrage window. Tune both together. |

## Visual/Audio Requirements

### Status Icons

Each status type has a distinct icon shape and color group. Color assignments follow art bible constraints: no semantic green/amber/red; Binary disruption types use tarnished steel grey; Graduated damage types use iron oxide.

| StatusType | Icon Shape | Color Group | Rationale |
|---|---|---|---|
| `Stalled` | Broken gear | Tarnished steel grey | Mechanical failure — suppressed drive train |
| `Redirected` | Forking arrow | Tarnished steel grey | Control disruption — trajectory diverted |
| `Corroded` | Acid drop | Iron oxide | Damage-over-time via corrosion — material degradation |
| `Burning` | Flame | Iron oxide | Damage-over-time via fire — heat damage to Frame |
| `Stunned` | Lightning bolt | Tarnished steel grey | Control disruption — card-play interruption |
| `Marked` | Crosshair / reticle | Tarnished steel grey | Targeting tell — next Frame hit is amplified |

Binary types share the steel grey group but are differentiated by icon shape. Graduated types share iron oxide but are differentiated by icon shape (acid drop vs. flame) — **no reliance on color alone for type discrimination**.

---

### Status Chip Layout

Each active `StatusInstance` renders as a status chip in the vehicle's status display area:

- **Icon**: 16×16px, placed at chip left.
- **Duration pips**: 3 × 4px square pips to the right of the icon. Filled pip = remaining turns. Outline-only pip = elapsed turns. (A `RemainingDuration = 2` instance shows 2 filled + 1 outline.)
- **Stacks numeral** (Graduated types only): 12px monospaced numeral in the upper-right corner of the chip. Renders in baseline chip color (iron oxide) at stacks 1–2; shifts to saturated iron oxide / near-white at stacks = cap (3) to signal the cap has been reached.
- **Chip size**: 40×20px target. Must remain legible at 1080p on standard PC monitor distance.

---

### VFX Events

| Event | Stalled | Redirected | Corroded | Burning | Stunned | Marked |
|---|---|---|---|---|---|---|
| **Application** | Gear-lock spark burst on affected vehicle | Arrow-split ripple on vehicle silhouette | Acid splatter on designated subsystem slot | Ignition flash on Frame slot | Lightning crackle on cab; brief screen-edge shake (low amplitude, 80ms) | Crosshair reticle locks onto Frame silhouette; subtle red rim pulse (one-shot pulse, NOT a loop) |
| **Active (idle)** | Faint gear-crack overlay on card play zone; attack-family cards dim | Subtle arrow shimmer on vehicle outline | Drip-pulsing overlay on Corroded slot | Low flame loop on Frame slot | Faint electric arc loop on cab (very low amplitude) | Static reticle outline on Frame slot — NO pulse loop; persistence is the tell |
| **Tick (turn start)** | — | — | — | Flame flare; Frame HP decrease number floats upward | Card-play attempt visibly consumed: card flies to discard with a "stutter" snap; energy refund spark on energy meter | — |
| **Consumption** | — | — | — | — | — | Reticle implodes into the Frame; +3 damage number floats upward in saturated iron oxide (distinct from regular damage numbers) |
| **Expiry** | Gear cracks dissolve | Arrow shimmer fades | Acid drop evaporates | Flame extinguishes with smoke wisp | Electric arc fades; "wake up" frame on cab | Reticle fades to outline-only then dissolves (un-consumed expiry — distinct from consumption flash) |

*Implementation note: "Active" idle effects are ambient loops — they must not read as action events. Keep amplitude low; player attention is for Tick and Expiry events.*

---

### Audio Events

| Event | Sound Character | Priority |
|---|---|---|
| Stalled — application | Metallic grinding lock-in, short (< 0.3s) | High |
| Stalled — expiry | Gear-release pop | Medium |
| Redirected — application | Whooshing fork / trajectory deflect | High |
| Corroded — tick (if any idle audio) | Optional: faint acid fizz loop, very low volume | Low |
| Burning — tick | Flame crackle burst, synced to damage number | High |
| Burning — expiry | Flame snuff + hiss | Medium |
| Stunned — application | Sharp electrical zap + brief metallic clang | High |
| Stunned — card-skip event | Stuttering snap (card flicked aside); low-volume electric crackle | Medium |
| Stunned — expiry | Electric pop + idle-engine recover | Low |
| Marked — application | Mechanical reticle-lock click (paired with a low rising tone, < 0.3s) | High |
| Marked — consumption | Cracking impact sting layered onto the consuming damage event (HIGH priority, must cut through Burning crackle if simultaneous) | High |
| Marked — un-consumed expiry | Reticle-release click (low volume) — distinct from consumption sting | Low |

*Stalled and Redirected have no turn-start tick sound — they are not DoT types and produce no per-turn audio event.*

---

> **Asset Spec flag**: Visual/Audio requirements are defined. After the art bible is approved, run `/asset-spec system:status-effects` to generate the full asset specification sheet for icon art, VFX, and audio SFX for this system.

## UI Requirements

### UI Surfaces That Consume Status Data

| Surface | What It Reads | Owning Spec |
|---|---|---|
| **Combat HUD** | Both vehicles' `ActiveStatuses` lists — renders one chip per `StatusInstance`, plus the per-subsystem Corroded indicator on the vehicle silhouette | Combat HUD UX Spec |
| **Card-hover preview** | Target vehicle's `ActiveStatuses` and target subsystem's `Corroded` instance (if any) — computes and displays final post-bonus damage | Combat HUD UX Spec |
| **Turn-transition announcement** | Status events fired during tick/expire (accessibility / screen reader) | Combat HUD UX Spec + accessibility-requirements.md |

This system provides the data contract. All layout, typography, color tokens, and animation timing belong to the Combat HUD UX Spec.

---

### Data Contract Exposed to UI

The UI layer reads status state via `IVehicleView`:

```
IVehicleView.ActiveStatuses : IReadOnlyList<StatusInstance>
```

Each `StatusInstance` exposes (per R2): `StatusType`, `RemainingDuration`, `Stacks`, `SourceVehicleId`, and — for `Corroded` only — a `TargetSlot` reference. No other fields are read by UI. UI never writes to status state.

Additional query surface (for card-hover damage preview):

```
IStatusQuery.GetCorrodeBonusForAttack(vehicle, targetSlot) : int
```

Returns `Stacks` if a `Corroded` instance exists on the target slot, else 0. This is the authoritative value the damage preview adds to `DamageOutput` to display the final projected damage.

---

### Chip Inspection — Interaction Contract

| Interaction | Trigger | Display |
|---|---|---|
| **Tooltip show** | Mouse hover over chip, OR chip receives keyboard focus via Tab navigation | Tooltip panel shows: status name, one-line rule text, current `Stacks`, `RemainingDuration`, and (for `Corroded`) the named target subsystem |
| **Tooltip hide** | Mouse leaves chip, OR focus moves to another element | Tooltip dismissed immediately |
| **No click action** | — | Chips are inspection-only; no click behavior. Clicking a chip must not open a modal, focus a card, or consume input. |

Hover-only interaction is forbidden (technical-preferences.md: "no hover-only interactions"). Every chip must be reachable and inspectable via keyboard focus traversal alone.

---

### Player vs. Enemy Display Parity

Player vehicle and enemy vehicle status displays render identical information: icon, `Stacks` numeral (if Graduated type), Duration pips, tooltip contents. This symmetry serves Pillar 3 (Read to Win) — enemy status state is the information the player uses to plan their turn. Hiding enemy Stacks count or Duration would degrade that read.

Chip density, placement, and layout may differ between player and enemy display areas (Combat HUD UX Spec decision), but per-chip information content must not.

---

### Card-Hover Damage Preview Integration

When the player hovers an attack card in hand, the combat preview must compute and display the **final damage** the attack will deal, including the `Corroded` bonus.

**Preview formula** (matches F1 exactly):

```
PreviewDamage = DamageOutput + GetCorrodeBonusForAttack(targetVehicle, targetSlot)
```

**Display rule:** The damage number shown in the preview is the final post-Corroded value — not the base `DamageOutput`. The player commits to the action with full knowledge of the final result.

**Redirected interaction:** If the target vehicle has an active `Redirected` instance, the card-hover preview must indicate the target will be re-rolled at resolution. The preview cannot predict the re-rolled target (random at resolution per R3). Display: show base `DamageOutput` with a "target re-roll" indicator; do not compute against the declared target's Corroded bonus because the declared target may not be the resolved target.

**Post-application preview latency:** After a status is applied (via card play or enemy action), the next card-hover preview must reflect the new state. No one-frame latency — the preview reads live `ActiveStatuses`.

---

### Accessibility Requirements

1. **Color-independent status type discrimination** — status types must be distinguishable without color. Icon shape alone must be sufficient (Visual/Audio spec: broken gear / forking arrow / acid drop / flame). The art bible constraint (no semantic green/amber/red) plus distinct icon shapes satisfies this. Chip tooltip must state the status name textually.
2. **Keyboard focus order** — every status chip must be reachable via Tab navigation from the combat play area. Default traversal order: player chips first (in application order), then enemy chips (in application order).
3. **Screen reader announcements** — status application, expiry, and tick events must produce announcements. Combat HUD UX Spec owns the exact phrasing; this GDD requires the event hooks:
   - `OnStatusApplied(vehicle, statusType, duration, stacks)`
   - `OnStatusExpired(vehicle, statusType)`
   - `OnStatusTickDamage(vehicle, statusType, damageAmount)` — fires only when a DoT tick deals damage
4. **Tooltip minimum dwell time** — tooltip contents must remain visible for at least 1.5 seconds once shown, even if the player briefly moves the cursor away (prevents flicker-dismiss at default accessibility settings). Combat HUD UX Spec may tune the exact duration.
5. **No timing-critical status reads** — player must never be required to read a status within a tight window to make a decision. Status state persists across the full turn; inspection time is unlimited.

---

> **Forward dependency on Combat HUD UX Spec:** Chip layout, palette, animation timing, and status list maximum width are owned by Combat HUD UX Spec. This GDD establishes the data contract (what is available to read) and the interaction contract (how players access details). Combat HUD UX Spec will consume both.

## Acceptance Criteria

### Core Rules

**AC-SE1 (R1 — Stalled suppresses attack-family cards):**
GIVEN a vehicle has an active `Stalled` instance with `RemainingDuration >= 1`, WHEN the card play phase begins for that vehicle, THEN any card whose `CardFamily` is `Precision`, `Assault`, or `Maneuver` cannot be played (the play action is blocked at the input layer, not silently discarded).

**AC-SE2 (R1 — Stalled does not suppress Control or Repair):**
GIVEN a vehicle has an active `Stalled` instance, WHEN the card play phase begins for that vehicle, THEN cards whose `CardFamily` is `Control` or `Repair` remain playable.

**AC-SE3 (R1 — Redirected re-rolls target at resolution):**
GIVEN a vehicle has an active `Redirected` instance, WHEN that vehicle plays an attack card declaring a specific opposing subsystem as its target, THEN the actual target subsystem used at resolution is selected at random from all opposing subsystems — the declared target is not used.

**AC-SE4 (R1 — Corroded adds stack bonus to attacks on the designated slot):**
GIVEN subsystem S on vehicle V has a `Corroded` instance with `Stacks = N`, WHEN an attack targets subsystem S on vehicle V and its card-resolved `DamageOutput` is D, THEN the damage received by subsystem S is exactly `D + N`.

**AC-SE5 (R1 — Burning deals damage at turn-start, targets Frame only, bypasses Armor):**
GIVEN vehicle V has an active `Burning` instance with `Stacks = N` and `CurrentArmor = A` (A > 0), WHEN vehicle V's turn begins, THEN (a) the vehicle's Frame Hp receives exactly N points of damage before any card is played, (b) `CurrentArmor` is unchanged (still A) after the tick, and (c) `OnCurrentArmorChanged` does NOT fire during the tick phase.

**AC-SE6 (R2 — StatusInstance fields are present on vehicle POCO):**
GIVEN `ApplyStatusEffectSO` fires on a vehicle with `StatusType = Stalled`, `Duration = 2`, `Stacks = 1`, WHEN the vehicle's active status list is inspected immediately after application, THEN a single `StatusInstance` is present with `StatusType = Stalled`, `RemainingDuration = 2`, `Stacks = 1`, and a non-null `SourceVehicleId` matching the applying vehicle's identifier.

**AC-SE7 (R3 — fire before tick: Burning fires before duration decrements):**
GIVEN vehicle V has `Burning` with `RemainingDuration = 1` and `Stacks = 2` at the start of its turn, WHEN that turn begins, THEN (a) Frame takes 2 damage first, (b) `RemainingDuration` decrements to 0, (c) the instance is removed — and the Frame damage is observed before the instance disappears.

**AC-SE8 (R3 — Stalled fires before tick):**
GIVEN vehicle V has `Stalled` with `RemainingDuration = 1`, WHEN that turn begins, THEN `vehicle.AttackSuppressed` is true during the card play phase, the instance is removed after the tick, and attack-family cards are blocked for exactly that turn — absent on the next turn.

**AC-SE9 (R3 — Corroded and Redirected have no turn-start effect):**
GIVEN vehicle V has both a `Corroded` instance and a `Redirected` instance, WHEN vehicle V's turn begins (turn-start phase only, before any card is played), THEN no damage is applied to any subsystem and no target re-roll occurs during the turn-start phase.

**AC-SE10 (R3 — Redirected resolves before Corroded; miss case):**
GIVEN vehicle V has `Redirected` active and subsystem S has `Corroded` with `Stacks = 2`, WHEN vehicle V plays an attack targeting S and `Redirected` re-rolls the target to a different subsystem T (T ≠ S), THEN subsystem T receives `DamageOutput` only (no Corrode bonus) and subsystem S receives 0 damage from this attack.

**AC-SE11 (R3 — Redirected resolves before Corroded; hit case):**
GIVEN vehicle V has `Redirected` active and subsystem T has `Corroded` with `Stacks = 2`, WHEN vehicle V plays an attack targeting S (S ≠ T) and `Redirected` re-rolls the target onto T, THEN subsystem T receives `DamageOutput + 2` damage — the Corrode bonus fires because T is the final resolved target.

**AC-SE12 (R3 — RemainingDuration decrements each turn):**
GIVEN a vehicle has `Stalled` with `RemainingDuration = 3`, WHEN that vehicle's turn completes three times, THEN: after turn 1 the instance has `RemainingDuration = 2`; after turn 2, `RemainingDuration = 1`; after turn 3, the instance is absent.

**AC-SE13 (R3 — Expiry removes instance; no "expired but present" state):**
GIVEN a vehicle has `Stalled` with `RemainingDuration = 1`, WHEN that vehicle's turn begins and the tick reduces `RemainingDuration` to 0, THEN the instance is not present in the vehicle's active status list after the expiry step completes.

**AC-SE14 (R4 — Binary merge: incoming weaker Duration does not shorten existing):**
GIVEN vehicle V has `Stalled` with `RemainingDuration = 3`, WHEN `ApplyStatusEffectSO` fires with `StatusType = Stalled` and `Duration = 1`, THEN the `Stalled` instance has `RemainingDuration = 3` (unchanged — `max(3, 1) = 3`).

**AC-SE15 (R4 — Binary merge: incoming stronger Duration refreshes up):**
GIVEN vehicle V has `Stalled` with `RemainingDuration = 1`, WHEN `ApplyStatusEffectSO` fires with `StatusType = Stalled` and `Duration = 3`, THEN the instance has `RemainingDuration = 3` and there is exactly one `Stalled` instance (no duplicate created).

**AC-SE16 (R4 — Graduated merge: Stacks add, Duration resets):**
GIVEN vehicle V has `Burning` with `Stacks = 1` and `RemainingDuration = 2`, WHEN `ApplyStatusEffectSO` fires with `StatusType = Burning`, `Stacks = 1`, `Duration = 3`, THEN the instance has `Stacks = 2` and `RemainingDuration = 3`, and there is exactly one `Burning` instance.

**AC-SE17 (R4 — Graduated Stacks cap at 3; overflow discarded):**
GIVEN vehicle V has `Burning` with `Stacks = 2`, WHEN `ApplyStatusEffectSO` fires with `StatusType = Burning` and `Stacks = 2`, THEN the `Burning` instance has `Stacks = 3` (not 4) — one stack is silently discarded and no error is raised.

**AC-SE18 (R4 — Corroded on different subsystem creates independent instance):**
GIVEN vehicle V has `Corroded` on subsystem A with `Stacks = 2`, WHEN `ApplyStatusEffectSO` fires with `StatusType = Corroded` targeting subsystem B, THEN vehicle V's active status list contains two `Corroded` instances: one on A with `Stacks = 2` (unchanged) and one on B with the new incoming Stacks value.

**AC-SE19 (R4 — Corroded on same subsystem uses Extend merge, not new instance):**
GIVEN vehicle V has `Corroded` on subsystem A with `Stacks = 1`, WHEN `ApplyStatusEffectSO` fires with `StatusType = Corroded` targeting subsystem A and `Stacks = 1`, THEN the active status list contains exactly one `Corroded` instance on A with `Stacks = 2`.

**AC-SE20 (R5 — RemainingDuration cap at 3):**
GIVEN vehicle V has `Stalled` with `RemainingDuration = 3`, WHEN `ApplyStatusEffectSO` fires with `StatusType = Stalled` and `Duration = 3`, THEN `RemainingDuration` is 3 — it cannot exceed the cap.

**AC-SE21 (R5 — SO import rejects Duration = 0):**
GIVEN an `ApplyStatusEffectSO` asset is authored with `Duration = 0`, WHEN the asset is imported into the project, THEN the import fails with a validation error — the SO does not load and cannot be referenced.

**AC-SE22 (R5 — SO import rejects Stacks = 0 on Graduated types):**
GIVEN an `ApplyStatusEffectSO` for a Graduated type (`Corroded` or `Burning`) is authored with `Stacks = 0`, WHEN the asset is imported, THEN the import fails with a validation error.

**AC-SE23 (R6 — Stalled does not affect Enrage counter):**
GIVEN the enemy vehicle has `Stalled` with `RemainingDuration = 3` and the Enrage counter is at value N before the enemy's turn, WHEN the enemy's turn begins and completes (card play blocked by Stalled), THEN the Enrage counter after both vehicles complete their turns has advanced by the standard combat-loop increment — it is not frozen, reduced, or skipped.

**AC-SE24 (R6 — Status resolution pipeline has no write access to Enrage counter):**
GIVEN `BeginTurn(vehicle)` is called for a Stalled vehicle, WHEN the status pipeline fires (steps 1–3), THEN the Enrage counter field on the combat-scope state object is identical before and after the `BeginTurn` call.

**AC-SE25 (R1 — Stalled checks CardFamily field, not effect content):**
GIVEN a `Control` family card has a mandatory minimum-damage `DamageEffectSO` in its effects list, AND vehicle V has `Stalled` active, WHEN the card play phase begins for vehicle V, THEN the Control card is playable — Stalled suppression is by `CardFamily`, not by effect content.

---

### Formulas

**AC-SE26 (F1 — CorrodeBonus equals Stacks exactly):**
GIVEN subsystem S has `Corroded` with `Stacks = 1`, WHEN an attack with `DamageOutput = 5` targets S, THEN S receives exactly 6 damage. Repeat: `Stacks = 2` → 7 damage. `Stacks = 3` → 8 damage. All three must pass.

**AC-SE27 (F1 — FinalDamage is a single TakeDamage call):**
GIVEN subsystem S has `Corroded` with `Stacks = 2` and an attack with `DamageOutput = 10` resolves, WHEN `TakeDamage` is invoked, THEN it is called exactly once with value 12 — two separate calls of 10 and 2 is a failing result (verified via test spy on `IVehicleMutator`).

**AC-SE28 (F2 — BurningDamage per tick equals Stacks; DamageSource.Status; Armor untouched):**
GIVEN vehicle V has `Burning` with `Stacks = 3` and `RemainingDuration = 2` and `CurrentArmor = 4`, WHEN vehicle V's turn begins, THEN `IVehicleMutator.ApplyDamage` is called exactly once with `(SlotType.Frame, 3, DamageSource.Status)` during the turn-start phase (not 6, not 2), Frame Hp decreases by 3, and `CurrentArmor` remains 4 after the call.

**AC-SE29 (F2 — TotalBurningDamage equals Stacks times initial Duration):**
GIVEN vehicle V has `Burning` with `Stacks = 2` and `Duration = 3` applied at start of turn T, WHEN turns T, T+1, and T+2 complete, THEN cumulative Frame damage from Burning ticks is exactly 6 (2 + 2 + 2).

**AC-SE30 (F2 — Burning targets Frame only, not other subsystems, and does not consume Armor):**
GIVEN vehicle V has `Burning` active and `CurrentArmor = A` (A >= 1), WHEN vehicle V's turn begins and Burning ticks, THEN only the Frame subsystem's Hp decreases — all non-Frame slot Hps and PlatingStacks are unchanged, and `vehicle.CurrentArmor` is unchanged (still A) after the tick.

---

### Edge Cases

**AC-SE31 (EC-S8 — Duration=1 Burning fires once then expires):**
GIVEN vehicle V has `Burning` applied with `Duration = 1` and `Stacks = 2`, WHEN vehicle V's next turn begins, THEN (a) Frame takes exactly 2 damage, (b) the `Burning` instance is absent after the expiry step, and (c) on vehicle V's following turn, no Burning damage occurs and no instance is present.

**AC-SE32 (EC-S8 — tick-before-fire is prohibited; Duration=1 must not produce zero damage):**
GIVEN vehicle V has `Burning` applied with `Duration = 1` and `Stacks = 1`, WHEN vehicle V's turn completes, THEN Frame has taken exactly 1 damage from the Burning tick — a result of 0 damage combined with an expired instance is a failing result (indicates incorrect tick-before-fire ordering).

**AC-SE33 (EC-S10 — FinalDamage single-call contract; Corrode is part of the attack):**
GIVEN subsystem S has `Corroded` with `Stacks = 1` and `DamageOutput = 7` resolves, WHEN `TakeDamage` is invoked, THEN (a) call value is 8 and (b) call count is 1 — two calls of 7 and 1 is a failing result.

**AC-SE34 (EC-S14 — Corroded bonus fires only for attacks targeting the Corroded slot):**
GIVEN subsystem A has `Corroded` with `Stacks = 2` and subsystem B has no status, WHEN an attack with `DamageOutput = 5` targets subsystem B, THEN B receives exactly 5 damage and A is unaffected by this attack.

**AC-SE35 (EC-S14 — Corroded check is per slot, not per vehicle):**
GIVEN subsystem A has `Corroded` with `Stacks = 2` and subsystem B has no Corroded instance, WHEN an attack targets subsystem B, THEN the Corroded instance on A is not consulted and B receives unmodified `DamageOutput` — applying the bonus to off-slot attacks is a failing result.

**AC-SE36 (EC-S13 — Two Corroded instances expiring simultaneously are both removed):**
GIVEN vehicle V has two `Corroded` instances on different subsystems, each with `RemainingDuration = 1`, WHEN vehicle V's turn begins and the tick step executes, THEN both `Corroded` instances are absent after the expiry step — neither is skipped due to list mutation during iteration.

**AC-SE37 (EC-S16 — Burning kills a vehicle through Armor):**
GIVEN vehicle V has `Frame.Hp = 1`, `CurrentArmor = 10`, `MaxArmor = 10`, and `Burning` with `Stacks = 2` active, WHEN vehicle V's turn begins, THEN the Burning tick reduces Frame Hp from 1 to 0 (floored), `CurrentArmor` remains 10, and the vehicle-defeat state-check triggers combat end — Armor does NOT prevent the kill. A result where `CurrentArmor` absorbs the tick or the vehicle survives because Armor > 0 is a failing result.

---

### Stunned (added 2026-05-19)

**AC-SE38 (R1 — Stunned consumes the first N card-play attempts of the turn):**
GIVEN vehicle V has `Stunned` with `Stacks = 2` applied at the end of the previous turn, WHEN vehicle V's turn begins, THEN (a) `vehicle.StunnedSkipsRemaining = 2` after step 1, (b) the first card-play attempt during the card-play phase is consumed without resolving (card discarded, energy fully refunded, `OnCardPlayed` does NOT fire, `StunnedSkipsRemaining` decrements to 1), (c) the second card-play attempt is consumed identically (counter decrements to 0), and (d) the third card-play attempt resolves normally.

**AC-SE39 (R1 — Stunned does not filter by CardFamily):**
GIVEN vehicle V has `Stunned` with `Stacks = 1` active, WHEN vehicle V plays any card (any `CardFamily`, including Repair, Control, Precision, Assault, Maneuver), THEN that card-play attempt is consumed identically — Stunned consumes the attempt regardless of family.

**AC-SE40 (R1 — Stunned skip refunds full energy cost):**
GIVEN vehicle V has `Stunned` with `Stacks = 1` and `CurrentEnergy = 5`, WHEN vehicle V plays a `Cost = 2` card and Stunned consumes the attempt, THEN `CurrentEnergy` remains 5 (no energy spent), the card is moved to discard, and `OnCardPlayed` does NOT fire.

**AC-SE41 (R1 — StunnedSkipsRemaining resets at end of turn; does not bank):**
GIVEN vehicle V has `Stunned` with `Stacks = 3` at turn start and plays only 1 card that turn (consumed by Stunned, counter ends turn at 2), WHEN vehicle V's turn ends, THEN `vehicle.StunnedSkipsRemaining` is reset to 0 — the 2 unused skips do NOT carry into the next turn.

**AC-SE42 (R4 — Stunned uses Extend merge):**
GIVEN vehicle V has `Stunned` with `Stacks = 1` and `RemainingDuration = 2`, WHEN `ApplyStatusEffectSO` fires with `StatusType = Stunned`, `Stacks = 1`, `Duration = 3`, THEN the instance has `Stacks = 2` and `RemainingDuration = 3` (Graduated Extend rule from R4).

---

### Marked (added 2026-05-19)

**AC-SE43 (R1 — Marked adds 3 flat damage to the first Frame-targeted event):**
GIVEN vehicle V has `Marked` (RemainingDuration >= 1) and `CurrentArmor = 0`, WHEN a card attack with `DamageOutput = 6` targets vehicle V's Frame, THEN Frame Hp decreases by exactly 9 (`6 + MarkedBonus(3)`) and the `Marked` instance is removed from `ActiveStatuses`.

**AC-SE44 (R1 — Marked is consumed even when Armor absorbs the full amplified hit):**
GIVEN vehicle V has `Marked` and `CurrentArmor = 15`, WHEN a card attack with `DamageOutput = 6` targets vehicle V's Frame, THEN (a) `FinalDamage = 9`, (b) Armor absorbs all 9 (CurrentArmor 15 → 6), (c) Frame Hp is unchanged, AND (d) the `Marked` instance is removed from `ActiveStatuses` — Armor absorbing the amplified hit still consumes the marker per EC-S17.

**AC-SE45 (R1 — Marked does NOT fire on non-Frame-targeted attacks):**
GIVEN vehicle V has `Marked` and subsystem Weapon has `PlatingStacks = 0`, WHEN a card attack with `DamageOutput = 6` targets the Weapon slot, THEN Weapon Hp decreases by 6 (no +3 bonus) and the `Marked` instance remains in `ActiveStatuses`.

**AC-SE46 (R1 — Marked is consumed by a Burning tick on the same turn):**
GIVEN vehicle V has `Marked` (RemainingDuration=2) and `Burning` (Stacks=2, RemainingDuration=2) at start of turn, WHEN vehicle V's turn begins, THEN R3 step 1 fires Burning first: Frame takes `2 + MarkedBonus(3) = 5` damage (DOT bypass of Armor still applies — F-CC1 step 2 is skipped), the `Marked` instance is removed, and any subsequent Frame-targeted card attack this turn does NOT receive the Marked bonus.

**AC-SE47 (R3 — Redirected resolves before Marked):**
GIVEN vehicle V has `Marked` and `Redirected` both active, WHEN vehicle V receives a card attack with declared target = Weapon and Redirected re-rolls onto Frame, THEN (a) Redirected substitutes target Frame first, (b) Marked then fires because the resolved target is Frame, (c) Frame receives `DamageOutput + 3`, and (d) the `Marked` instance is removed.

**AC-SE48 (R3 — Redirected re-roll lands on non-Frame; Marked does NOT fire):**
GIVEN vehicle V has `Marked` and `Redirected` both active, WHEN a card attack declared on Weapon is re-rolled to Engine by Redirected, THEN Engine receives unmodified `DamageOutput` and the `Marked` instance remains in `ActiveStatuses` (consumed only on Frame contact).

**AC-SE49 (R4 — Marked uses Binary Refresh merge; bonus does not stack):**
GIVEN vehicle V has `Marked` with `RemainingDuration = 1`, WHEN `ApplyStatusEffectSO` fires with `StatusType = Marked`, `Duration = 2`, THEN `RemainingDuration = max(1, 2) = 2`, there is exactly one `Marked` instance, and the next Frame-targeted hit receives exactly `+3` damage (NOT `+6`).

**AC-SE50 (EC-S19 — Marked expires un-consumed if no Frame damage occurs):**
GIVEN vehicle V has `Marked` with `RemainingDuration = 1` and receives no Frame-targeted damage during that turn, WHEN vehicle V's next turn begins, THEN the `Marked` instance is absent (expired at R3 tick) and no Marked bonus has been paid out.

## Open Questions

These questions remain unresolved at GDD sign-off. Each blocks a specific downstream story from being fully written and must be closed before the related story enters a sprint.

**OQ-SE1 — Enrage counter increment value is undefined.** AC-SE23 cannot specify the exact numeric assertion until the Card Combat System GDD defines the Enrage counter contract. Update AC-SE23 before the Enrage story enters sprint.

**OQ-SE2 — `IVehicleMutator.TakeDamage` DamageSource discriminator not yet specified.** ~~AC-SE28 and AC-SE30 verify correct HP changes but cannot assert correct `DamageSource` attribution until the Vehicle & Part System GDD defines that discriminator enum. Add a DamageSource-specific AC once it is defined.~~ **CLOSED (2026-04-21)** — V&P R9 defines `DamageSource { Card, Status, Environment }` and `IVehicleMutator.ApplyDamage(SlotType, int, DamageSource)` as the authoritative signature. AC-SE28 amended to assert the exact call signature. The `Status` value on the discriminator is the load-bearing flag that triggers Card Combat F-CC1's DOT exception (Armor bypass).

**OQ-SE3 — Redirected RNG contract unspecified.** AC-SE3 verifies Redirected fires at the correct step and uses the full pool. No AC verifies equal probability distribution per subsystem. If the Card Combat System GDD specifies a seeded random for resolution, add a distribution AC.

**OQ-SE4 — "All opposing subsystems" pool with Offline subsystems depends on Card Combat System GDD.** EC-S2 states Redirected uses the full pool regardless of Offline state, but "Offline" is not yet defined by Card Combat System. AC-SE3 cannot test the Offline-in-pool scenario until that definition exists.

---

## Armor Retrofit Closure Notes (2026-04-21)

This GDD was amended as part of the Armor-as-layer model change (armor stress-test memo `design/notes/armor-model-stress-test.md`, decisions locked 2026-04-21). The following changes and closures apply:

- **R1 Burning row** — amended to state DOT carries `DamageSource.Status` and bypasses Armor; explicit pointer to Card Combat F-CC1 DOT exception.
- **R3 step 1 Burning handler** — rewritten to call `IVehicleMutator.ApplyDamage(SlotType.Frame, Stacks, DamageSource.Status)` and spell out which F-CC1 steps are skipped (1 and 2) and which state is unchanged (`CurrentArmor`, `OnCurrentArmorChanged`).
- **F2 (Burning Damage)** — preamble added explaining Armor bypass at the formula level; example rewritten against a non-zero `CurrentArmor` to demonstrate that Armor is NOT consumed by DOT.
- **EC-S2 (Redirected pool)** — 4-slot pool including Frame; re-rolls that land on Frame follow the F-CC1 Frame path; subsystem-strike + Redirected edge case flagged as Card System forward dependency.
- **EC-S8 (Duration=1 Burning)** — amended to clarify DOT bypasses Armor at the fire step.
- **EC-S16 (new)** — Burning can kill through Armor; Armor does not protect Frame from DOT.
- **Tuning Knobs row for Burning** — Armor bypass flagged as NOT a tuning knob (locked design decision).
- **AC-SE5, AC-SE28, AC-SE30** — amended to assert Armor is untouched by Burning ticks (`CurrentArmor` unchanged; `OnCurrentArmorChanged` does NOT fire).
- **AC-SE37 (new)** — verifies Burning can defeat a vehicle with full Armor and low Frame Hp.
- **Dependencies → V&P row** — updated to reference `DamageSource.Status` and 4-slot model; clarifies `CurrentArmor` is not read or written by this system.
- **Dependencies → Card Combat row** — F-CC1 DOT exception called out as the Card Combat-owned contract Burning relies on.
- **OQ-SE2 closed** by V&P R9 + Card Combat F-CC1 fork.

Sections not amended by this retrofit: Overview, Player Fantasy, R2 StatusInstance model, R4 Merge rules, R5 caps, R6 Enrage decoupling, States and Transitions, F1 Corroded Damage, EC-S1/3/4/5/6/7/9/10/11/12/13/14/15, AC-SE1–4/6–27/29/31–36, Visual/Audio, UI Requirements.

**Forward dependencies opened by this retrofit (not owned here):**

- Card Combat System GDD owns the F-CC1 DOT exception text (already authored in the Card Combat retrofit, 2026-04-21).
- Vehicle & Part System GDD owns the `DamageSource` enum and `IVehicleMutator.ApplyDamage` signature (already authored in the V&P retrofit, 2026-04-21).
- Card System GDD must resolve the subsystem-strike + Redirected edge case (EC-S2) when subsystem-strike cards are authored.
- Card System GDD owns the `BypassPlating` flag on `DamageEffectSO` — Burning does NOT use this flag; its bypass is driven by `DamageSource.Status` alone.

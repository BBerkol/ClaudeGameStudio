# Stranded Chance Events GDD — Pre-Authoring TD Shape Review

**Date:** 2026-07-27
**Doc slated for author:** `design/gdd/stranded-chance-events.md`
**Type:** Pre-authoring shape review (new system GDD, not code)
**Trigger:** `capture-before-destroy.sh` block on new 55-line design doc

## Why this capture

Design decisions were locked in a design pass earlier today:
- **Role B (Meaningful Gamble)** — stranded stays terminal by default, player can
  buy back into the run with scrap.
- **Scrap-only** escape cost currency.
- **Random with drought floor** — per-strip roll, guaranteed floor if none fire.
- **Per-biome pool** via `BiomeDistributionSO.StrandedEventPool` (ADR-0015).
- **3 events for biome 1**: Scavenger (pure-choice), Wreck (pure-luck), Passing
  Convoy (cost/reward).

Before authoring the 8-section GDD, TD gate required to validate the *shape*
fits cleanly (roll owner, event vocabulary reuse, economy verbs, SO surface
freeze status).

## Values / decisions at risk

No existing authored values are being destroyed by this capture — this is a
*new* doc for a not-yet-built system. What's at risk is **committing to the
wrong shape in the GDD and paying to unwind it later** (Memory
`feedback_accept_structural_split_early` — declined-split on V&P GDD cost 2
wasted iterations).

Shape assumptions in the drafted Overview that TD flagged:

| Assumption in draft Overview | TD verdict |
|---|---|
| "Rolls fire from `StormAdvanceVisualPacer`'s existing per-strip queue" | ❌ Wrong owner — pacer is cadence-only, roll belongs in `RunSession.AutoAdvanceStrandedStorm` |
| "Uses existing `IScrapEconomy.TryConvert*` verbs" | ❌ Verb doesn't exist yet — Node Encounter GDD paper contract only. Use `TrySpend` + `FuelState.AddStrips` instead |
| "Ships biome 1 with three events" via new `StrandedEventSO` type | ⚠️ Would fork parallel event type from Node Encounter's `EventPayload`. Must share payload vocabulary or explicitly defer the merger |

## Technical Director Review

**Verdict: RESHAPE** (before authoring)

Shape is 80% right, two structural gaps must be resolved before spending hours
on an 8-section GDD.

**Lens 1 — Codebase Health:**
- Prior-art claim is false. `INodeEncounterHandler`, `BeaconOutcome`,
  `EventPayload`, and `TryConvertScrapToFuel/Fuel→Scrap` verbs exist **only in
  the Node Encounter GDD**, not in Unity code. `IScrapEconomy` today is
  `{Current, Add, TrySpend}` — no Convert verb, no handler pattern. Node
  Encounter is unbuilt. This GDD can't lean on it as an integration seam; it
  can only lean on the GDD paper contract, which is fine, but the doc must say
  "assumes Node Encounter contract lands first-or-together," not "reuses
  existing pattern."
- Fuel-drain scrap-purchase requires a new verb. `Pay X scrap → +1 strip of
  fuel` is exactly `TryConvertScrapToFuel` from Node Encounter. Cleanest for a
  self-contained slice: call `TrySpend` + a dedicated `AddFuelStrips(int)` on
  `FuelState`. Convert-verb story lands when Node Encounter proper ships.
- `StormAdvanceVisualPacer` is the wrong owner for the roll. The pacer's
  charter is *wall-clock cadence for a synchronous model tick* — it queues,
  defers, and animates. Rolling event triggers from inside the pacer
  bimodalizes it (visual pacer + gameplay dispatcher). Roll belongs in
  `RunSession.AutoAdvanceStrandedStorm` (session-side, alongside the strip
  advance), event UI presentation belongs on the host. ADR-0011 lens.

**Lens 2 — Optimization:** N/A at shape stage. Per-strip event roll is not a
hot path.

**Lens 3 — 1.0 Survival:**
- `StrandedEventPool` field is safe. 5-slice freeze expired (10 slices past
  2026-07-07); recent 2026-07-26 verdicts already extend the SO. Adding one
  array follows the ADR-0015 pattern cleanly.
- New `StrandedEventSO` vs. reuse: Node Encounter's `EncounterPayload` enum
  has `Windfall` / `Convert` / `Ambush` shapes overlapping the 3 events. **Do
  not fork a parallel event type.** Author `StrandedEventSO` as a *thin* SO
  that composes an existing (future) `EventPayloadDefinitionSO` shape — one
  payload vocabulary across NE + Stranded. Two systems on one payload =
  ADR-0011 clean. Two payload types = drift.
- 3-events-max bound is 1.0-shape correct.

**Required reshape before authoring (must be locked in "Dependencies"
section up front):**

1. **Roll owner** — Move roll-and-fire logic to
   `RunSession.AutoAdvanceStrandedStorm` (session-side); pacer stays
   cadence-only.
2. **Payload vocabulary** — Declare shared payload vocabulary with Node
   Encounter, or explicitly defer the merger with a follow-up ADR note. Do not
   silently ship parallel `StrandedEventSO`.
3. **Economy verbs** — Use `TrySpend` + `FuelState.AddStrips` for escape
   purchase, not the not-yet-built `TryConvertScrapToFuel`.

**Approve to author once those three are locked in the Dependencies section
up front.**

## Amended Overview (post-reshape)

Overview draft is being rewritten to reflect the three reshapes:

- Roll owner corrected: `RunSession.AutoAdvanceStrandedStorm` (not pacer)
- Escape verb corrected: `IScrapEconomy.TrySpend` + `FuelState.AddFuelStrips`
  (new primitive on FuelState, not the Node Encounter Convert verb)
- Event SO corrected: thin `StrandedEventSO` composing shared future
  `EventPayloadDefinitionSO`; Dependencies section explicitly notes NE
  contract must land first-or-together (or a follow-up ADR captures the
  parallel-payload-then-merge trajectory)

## Files about to be touched

- **NEW:** `design/gdd/stranded-chance-events.md` (this doc)
- **MODIFY:** `design/gdd/systems-index.md` (add Feature-tier entry)
- **MODIFY:** `design/gdd/node-encounter.md` + `design/gdd/scrap-economy.md`
  (bidirectional cross-links per design-docs.md rule)

Author phase (a later slice, not this session):
- `Assets/Scripts/Run/Authoring/BiomeDistributionSO.cs` — add
  `_strandedEventPool` array field
- `Assets/Scripts/Run/RunSession.cs` — extend `AutoAdvanceStrandedStorm` with
  per-strip roll
- `Assets/Scripts/Run/FuelState.cs` — new `AddFuelStrips(int)` primitive
- NEW: `Assets/Scripts/Run/Authoring/StrandedEventSO.cs` (thin composer)
- NEW: 3 event assets under `Assets/Resources/Run/Biomes/StrandedEvents/`

## Follow-up

**Reorder decision (2026-07-27, user):** Park this GDD; build the Node
Encounter Event-handler slice first, then re-author Stranded Chance Events
as a thin adapter over the shared infrastructure. Rationale: the 3 stranded
events map 1:1 onto Node Encounter §C.2.5 Event-handler payloads
(Windfall/Convert/Ambush/Treasure), and the shared `EventPayloadDefinitionSO`
+ Event modal UI + `TryConvert*` verbs delete ~70% of the Stranded Events
implementation surface. Additional win: `BeaconType.Event = 6` is currently
live in the generator but fires no content — the Event-handler slice makes
Event beacons real in biome 1 as a standalone deliverable.

Stranded Chance Events GDD was **never written** (hook blocked the initial
write; user approved parking before retry). Nothing to delete. This capture
becomes the historical record of the shape decision + reorder.

**Next artifact:** `design/quick-specs/node-encounter-event-slice.md`
scope-narrowing the Event handler + payload SO + `TryConvert*` verbs + biome-1
Event beacon activation.

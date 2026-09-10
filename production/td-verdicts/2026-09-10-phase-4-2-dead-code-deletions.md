# TD Verdict — Phase 4.2 Dead-Code Deletions

**Date:** 2026-09-10
**Slice:** remediation plan Phase 4.2, "Pure deletions (ADR-0011 residue)"
**Baseline at time of verdict:** EditMode 1281/1280/0/1, PlayMode 16/16
**Consulted:** `technical-director`, full batch brief with per-item measurements

## Why this file exists

The `td-review-required` hook blocks deletion of 5+ fallback/error-path lines in
non-test code without a same-day verdict. `VehicleVisual.cs` (Item 3) trips it.
This records an actual consultation held today — not a restatement.

## Files covered

`Assets/Scripts/CombatView/VehicleVisual.cs` · `Assets/Scripts/Combat/StatKind.cs`
· `Assets/Scripts/Combat/StatModifier.cs` · `Assets/Scripts/UI/PartOfferElement.cs`
· `Assets/Scripts/Run/NodeEncounter/BeaconOutcome.cs` ·
`Assets/Scripts/CombatView/MerchantSceneController.cs` ·
`Assets/Scripts/CombatView/RunSceneHost.cs`

## TD Verdict

**TD-CHANGE-IMPACT: CONCERNS** — not about the measurements, about the *plan
text*. Phase 4.2 is wrong in **five** places, and two of its items are not
deletions at all.

| Item | Plan says | Verdict |
|---|---|---|
| 1 `HostAdvanceReason` + `MapAdvanceReason` | Delete | **APPROVE** (already done) |
| 1b `AdvanceReason.CombatCleared` | *(silent)* | **DELETE — the field, not the value → Phase 5** |
| 2 `StatKind` / `StatModifier` | Delete | **DELETE both files** |
| 3 `VehicleVisualSlot` | "Collapse into `SlotKind`" | **DELETE enum AND `GetSlot`** — collapsing is an active mistake |
| 4 `BeaconOutcome` | "6 of 10 members dead" | **DELETE 2 members + `ZeroDelta`; KEEP the rest and ALL of `EncounterPayload`** |
| 5a Merchant no-op | Delete | **DELETE handler + 2 subscription lines only** |
| 5b MapView zero storm | "hardcoded zero storm values" | **Not a bug, not a pure deletion → own item** |

### The distinction doing the work

**Superseded** (named consumer shipped and chose a different input) → delete.
**Awaiting a consumer AND honestly populated** → keep.
**Awaiting a consumer AND impossible to populate** → delete; a field no code path
can ever fill is a stub, not scaffolding.

### Item 2 — reverses this session's earlier recommendation

I had recommended KEEP, on the strength of the file's own dated defense:
*"Kept rather than deleted because this IS the correct 1.0 shape and re-deriving
it later costs more than carrying it."* **That premise is false**, verified
directly: ADR-0017 line 233 reads

```
public enum StatKind { /* Phase 2.5A initial vocabulary — TBD in slice brief */ }
```

The ADR carries the *shape* (§163, §195, §197, §229-233, §249, §479-480), not the
vocabulary — deferred to a slice brief never written. `DodgeRate`/`CritRate`/
`ArmorRatingBonus` were invented at implementation time and the file itself calls
them provisional. Re-deriving costs minutes. Carrying has already cost once: the
file documents its own prior false comment describing a field that does not
exist. **ADR-0015 does not rescue it** — narrowing needs a working runtime to
narrow, and there is no carrier field, no aggregator, no `DamagePipeline` roll.

`ArmorRatingBonus` is additionally a naming hazard against the real, load-bearing
`SlotInstance.ArmorContribution` (ADR-0012).

**No ADR-0017 amendment** — the ADR already lists these as `NEW … (greenfield)`
files a future slice *creates*. Deleting a never-wired greenfield artifact
restores agreement with the ADR.

The 2026-08-05 Q3 verdict (no DMG / no dodge readout) is a **UI rule and
survives** — deleting the enum makes surfacing those numbers impossible, which
strengthens it.

### Item 3 — why "collapse into SlotKind" would make things worse

`GetSlot` has **zero callers**; `VehicleVisualSlot`'s 7 hits are all inside its
own file. The promised "runtime part-painter" **exists and bypasses this API**:
`VehiclePartTint` routes by canonical slotId strings (`_weaponSlotId` etc., set
by `CombatPrefabAuthor.cs:5094-5097`) and reaches renderers via `_visual.WeaponSlot`
/ `_visual.EngineSlot` directly. `GetSlot`'s own comment admits it: *"runtime
painters that route by SlotId should … instead of going through GetSlot."*

Collapsing into `SlotKind` would install an **enum-keyed** visual lookup on a
view layer that **ADR-0010 moved to `string slotId`** — manufacturing fresh
ADR-0010 drift. Current violations: #8, #6 ×2 (both `return null`s are
unreachable), #7.

**Hook note:** the deletion touches **no `[SerializeField]`**. `_weaponSlot`,
`_engineSlot`, `_frameSprite` all stay. **No prefab serialization change, no
re-author, no interaction with the active drift sentinel.**

### Item 4 — the plan's usage counts are methodologically wrong

`BeaconOutcome` has **zero production readers of any field** —
`RunSceneHost.NotifyEventResolved` never reads its parameter; every field read is
in `EventHandler_Dispatch_Test.cs`. The plan counted construction-site *writes*
and test asserts as consumption. Fix that method in the plan text; it will recur.

- **DELETE** `CardsOffered`, `PartOffered` — not merely unconsumed but **never
  populated**; all construction sites pass empty/null, and offers have a
  canonical home on `RunState` per ADR-0013/0004. ADR-0011 #2 + #6.
- **DELETE** `ZeroDelta` — zero callers; the plan missed it.
- **KEEP** `ScrapDelta`, `FuelDelta`, `PayloadType`, `WasCombatRewardClosed`,
  `RunTerminated` — honestly populated with real values, same justification as
  the 2026-09-09 `RunTerminated` ruling, which this extends rather than
  contradicts.
- **KEEP all 10 `EncounterPayload` members** — ordinals are load-bearing and
  track the GDD one-for-one for save/telemetry round-trip; `Rest`/`Merchant`/
  `Chopshop` beacons exist as PrefabRoots and simply do not route through the
  handler seam yet. **Unemitted ≠ dead.** This is where ADR-0015 genuinely
  applies. If `BeaconType.Haven` is retired, `EncounterPayload.Haven` goes in
  *that* slice.

### Items moved OUT of 4.2

- **1b → Phase 5.** The right deletion is `AdvanceReason` **entirely** plus
  `BeaconTransition.Reason`, not `CombatCleared` alone — a one-member enum
  threaded through four layers is a purer #4 violation than the two-member one.
  4-layer signature change across `INodeMapMutator`, `RunController`,
  `RunSession`, `NodeMap`, `RunSceneHost` + ctor + ~11 test sites.
  **`BeaconType.cs:41-46` claims `Reason` is "persisted on BeaconTransition so
  save snapshots … can key off the cause" — this is FALSE**, verified: no save
  DTO references it. A transitional comment that is also factually wrong is what
  let this survive prior audits.
- **5b → own item, sequenced against the storm-visual rewrite.** Not a bug:
  `PreviewedStormCursorBefore/After` have zero readers, so nothing displays
  zeros. The real defect is a **false-green test** — `BeaconTravelTick_Test.cs:28-29`
  asserts ctor propagation with synthetic values the sole production emitter
  never produces, so the suite structurally cannot catch the discrepancy. The
  fields also *cannot* be honestly populated: `StormState` exposes no
  normalized-X accessor. Deleting them is a **10→8 positional ctor change**
  across 4 files. `StormAdvanceStrips` stays — honestly populated.

## Corrections applied to the TD verdict itself

Verified against the repo rather than accepted:

- **`.Reason` is NOT zero-read in tests.** The TD stated the tests "never"
  read it. `RunController_HappyPath_Test.cs:229` asserts
  `Assert.AreEqual(AdvanceReason.Departure, t.Reason)`. Four of the five `.Reason`
  hits are the unrelated `VehicleMotionState.MotionReason`. The conclusion is
  unaffected — zero **production** readers, still a Phase 5 signature change.
- **`EncounterPayload` has 10 members, not the 9 in my brief** — I omitted
  `Haven = 9`. The TD caught it; KEEP ruling covers it.

## Approved 4.2 batch

1. `HostAdvanceReason` + `MapAdvanceReason` — done, commit as-is.
2. Delete `StatKind.cs` + `StatModifier.cs`; rewrite `PartOfferElement.cs:32`.
3. Delete `VehicleVisualSlot` + `GetSlot`.
4. Delete `BeaconOutcome.CardsOffered`, `.PartOffered`, `.ZeroDelta`.
5. Delete `MerchantSceneController` choice-2 handler + its subscribe/unsubscribe.

**Gate:** EditMode must return **1281/1280/0/1 unchanged**, plus
`grep -cE 'error CS' TestResults/editmode.log` — batchmode exit code lies, and
the `PartOfferElement` comment edit or the `StatModifier` deletion is the likely
place to strand a reference.

## Three-lens self-audit

Returned with the verdict. **Health:** every item grep-verified, not assumed; the
new finding is #7-with-a-false-claim at `BeaconType.cs:41-46`, worth a targeted
sweep since any xmldoc asserting "persisted"/"consumed by" may be lying.
Subscription pairing verified for Item 5a (`+=` at `:250` / `-=` at `:275`
removed together). **Optimization:** all items cold-path or zero-path; the
`BeaconTravelTick` payload shrink is real but negligible and explicitly not
claimed as justification. **1.0 survival:** the superseded-vs-awaiting
distinction was applied by measurement; the one accepted risk is that deleting
the cursor fields costs a re-add if the storm rewrite lands soon — deferred to
schedule knowledge rather than decided unilaterally. Flagged not fixed:
`BeaconTravelTick`'s 10-argument positional ctor is itself why dead fields
accumulate there; worth a Phase 5 line before the storm rewrite inherits a
12-arg ctor.

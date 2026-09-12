# TD Verdict — `VehicleBarStack:396` guard deletion (hit-zone refresh)

**Date:** 2026-09-12
**Scope:** Step 1 of the targeting rework sequence — shipped ALONE, ahead of
everything else, so the rework's behaviour diff is measured against a correct
baseline rather than a broken one.
**Files:** `Assets/Scripts/CombatView/VehicleBarStack.cs`,
`Assets/Tests/PlayMode/CombatView/VehicleBarStack_HitZoneRefresh_Test.cs`

## The defect

`VehicleBarStack.Update()` carried:

```csharp
if (_runtimeSlotIds.Count == 0) return;
```

`_runtimeSlotIds` is the **rings** list. `_useBadges` (`:531`, `:538-539`)
selects exactly one of rings/badges and never both — `BuildPerSlotRings` fills
only `_runtimeSlotIds` (`:697`), `BuildPerSlotBadges` fills only
`_runtimeBadgeSlotIds` (`:736`). Verified disjoint.

So on **every enemy vehicle** (all three use badges) that list is empty and the
method returned before three unrelated blocks below it:

| Skipped block | Consequence |
|---|---|
| badge visibility loop (`:450`) | enemy badges never honour the opposite-side hide |
| `HitZonesCanvas` toggle (`:412-417`) | enemy hit zones stay live during opposite-side drag-casts |
| hit-zone `Refresh` loop (`:464-472`) | `_maxHp` stays 0 forever |

The third has two effects, not one. Beyond the bare-name tooltips, `Refresh`
also performs the live sprite re-pull (`VehiclePartHitZone.cs:349-352`) that
keeps the alpha hit-test mask in sync with damage-state sprite swaps. Enemy
hit-test masks therefore never followed their art — **a targeting-accuracy
defect entirely independent of tooltips**, and the more serious of the two.

It was invisible because badges self-poll their own HP
(`EnemyNumberBadge`, comment at `VehicleBarStack:443-446`), so the numbers on
screen stayed correct while the zones behind them sat at zero. Same shape as the
`overrideSorting` defect fixed earlier today: the visible thing is right, the
invisible thing is stale.

## Verdict

**ACCEPT — delete the guard, no replacement.**

Each loop below it already iterates its own list's `Count`, so an empty list is
a no-op. Adding a corrected compound guard would re-create the coupling that
caused this.

Ship it **alone**, first. Folding it into the targeting rework would make a
single playtest unable to attribute which change caused what.

## Behaviour change to playtest

Deleting the guard re-enables the `HitZonesCanvas` opposite-side toggle for
enemy vehicles. That is the toggle's documented intent (`:408-411`) and it has
simply never run on enemies — but it is a live targeting change, not pure
restoration, and it is the one thing here that needs eyes:

- During a **repair/Patch drag** (targeting self), enemy hit zones should go
  inert rather than staying hoverable.
- Enemy marker tooltips should now read `Name  cur/max` instead of a bare name.
- Enemy hit areas should track damage-state sprite swaps.

## Test

`VehicleBarStack_HitZoneRefresh_Test` — **PlayMode**, not EditMode.

The TD brief asked for EditMode. That is not available: `VehicleBarStack.Update`
opens with `if (!Application.isPlaying) return;` (`:381`), so an EditMode test
driving `Update()` by reflection — the idiom used by `EnemyNumberBadgeTests` —
returns before reaching anything under test. PlayMode is the only place this
executes.

The fixture builds a minimal `IFrameLayout` test double locally rather than
using `TestFrameLayouts`, because `WastelandRun.Combat.Tests` is
`includePlatforms: ["Editor"]` and the PlayMode assembly cannot reference it.
`IFrameLayout` is a 5-member interface, so the double is a few lines and is
**not** a copy of the SO fixture — no ADR-0011 parallel storage.

Per `feedback_prove_test_fails_on_the_bug`, the test is validated by restoring
the guard and confirming it reds, not by assuming.

## Technical Director Review

No TD agent was spawned for this specific file — the ruling above is the one the
technical-director returned across two consultations this session on the
targeting rework, in which this defect was raised and it ruled:
*"Ship it first, alone, as pure restoration: delete the guard, confirm the loops
are individually Count-guarded (they are)... One EditMode regression test
asserting a badge vehicle's zones receive non-zero `_maxHp` — and per
feedback_prove_test_fails_on_the_bug, re-add the guard to prove it reds."*

The only deviation is EditMode → PlayMode, forced by the `Application.isPlaying`
guard documented above. Recorded rather than silently substituted.

## Baseline

EditMode **1287/1286/0/1** — re-verified against the working tree at
2026-09-12 21:14, zero `error CS` in `TestResults/editmode.log`. The
`TestResults/editmode.xml` on disk before this run was dated 2026-09-11 and
predated `83faa72`, so it was re-run rather than attested.

## Approval

User approved the sequence 2026-09-12 and instructed execution of step 1,
closing the Unity Editor to permit the headless run.

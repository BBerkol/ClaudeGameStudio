# Capture — `VehicleStateDto` joins `run.session_core` (Slice 3)

**Date:** 2026-08-27
**System:** ADR-0004 save recovery — resume-atomic group membership
**Epic:** Phase 2.5 Parts Axis · Garage re-cut slice 3
**Status:** AWAITING USER APPROVAL — no edits made

---

## 1. Why

`RunInventory` joined `run.session_core` at Phase 2.5C Slice A2
(`LoadedRunSnapshot.cs:60-66`). `VehicleStateDto` is still a standalone
group-of-one that falls back to chassis-fresh on absence
(`VehicleStateSerializable.cs:34-38`).

Equipping is swap-only: `Vehicle.SwapPart` (`Combat/Vehicle.cs:314`) paired with
`RunInventory.Remove` (`Run/RunInventory.cs:121`). An equip **moves** a
`PartInstance` from the inventory list onto a `SlotInstance`.

So on a vehicle-state skip: inventory rehydrates (it must — it is a group
member), vehicle rolls back to chassis-fresh, and a part that was **equipped**
exists in neither store. No exception, no log the player ever sees — the skip is
recorded in `LoadResult.skipped` for diagnostics only
(`SaveSystem.Load.cs:243-247`).

That is the ADR-0004 Slice 8d membership criterion, quoted verbatim in
`FuelStateDto.cs:15-17`: *"a member joins `run.session_core` if its
absence-with-others-present creates a silently-broken determinism or progression
invariant."*

The sharper framing, worth keeping: **group membership is not a property of a
DTO — it is a property of the transaction graph between DTOs.** `RunInventory`
and `VehicleState` are two halves of one conserved quantity. Once a verb moves
rows between them and the group already contains one half, they are one atomic
unit.

The existing rationale for keeping it standalone (`VehicleStateDto.cs:20-27`,
repeated at `RunSceneHost.cs:429-438`) — *"a missing vehicle DTO means restore
to chassis-fresh, a recoverable cosmetic loss"* — was **correct when written and
is now false**. Pre-parts the vehicle carried only HP. Post A1-A3 it carries
identity: `SlotSnapshotDto` persists `InstalledPartId`,
`InstalledPartDisplayName`, `MountDirection` (`VehicleStateDto.cs:139-142`).

---

## 2. What is destroyed

**No authored designer values.** No prefab, scene, SO, or tuned balance value is
touched. What is destroyed is a **behaviour** and four **documented claims**.

### 2a. Behaviour removed — the chassis-fresh fallback

`RunSceneHost.cs:666-667`:

```csharp
if (loadedVehicleState != null)
    loadedVehicleState.ApplyTo(player);
```

Post-move, `snapshot.LoadedVehicleState != null` is a gate precondition, so the
false limb is unreachable — a dead second shape guarded by a null check, i.e.
ADR-0011 #3 (bimodal path). `BeginRunFromLoaded`'s own xmldoc (`:620-626`) cites
exactly that pattern as the reason it exists as a sibling method rather than an
optional-parameter overload, so leaving it would make the method violate the
rule it was created to satisfy.

**Replaced by** the shape the four existing members already use: a top-of-method
`ArgumentNullException` alongside `loadedMap` / `loadedDeck` / `loadedInventory`
/ `loadedStormState` (`:653-660`), then an unconditional `ApplyTo`.

### 2b. Player-visible behaviour change — NAME IT

A save whose `vehicle_state` entry is **missing or schema-mismatched** currently
resumes with a healed vehicle and the run intact. After this change it
**regenerates the entire run** (map, seed, deck, inventory, storm).

This is the intended semantics of the group and matches the EA-mode
any-mismatch policy already accepted for the other five members
(`SaveSystem.Load.cs:211-216`). It is the correct trade — a wiped run beats a
silently deleted legendary part — but it is a real downgrade in recovery
leniency for one failure mode, and it should be a conscious choice, not a
side effect.

### 2c. Four doc blocks asserting the OLD membership

Under ADR-0011 #7, a comment describing a retired shape is a *transitional
comment* — a forbidden pattern, not a nit. All four must die in the same commit:

| File | Block |
|---|---|
| `Save/Dtos/VehicleStateDto.cs:14-28` | The `<b>Group membership</b>` block — states the criterion, then reaches the now-wrong conclusion. **Most dangerous of the four**: it is the file a future reader opens to learn the rule, and it currently teaches the rule *and* a worked example of applying it wrongly. |
| `Save/Adapters/VehicleStateSerializable.cs:34-38` | "this adapter is a standalone (group-of-one) RunState category — it does NOT join `run.session_core`" |
| `CombatView/LoadedRunSnapshot.cs:19-31` | Move `LoadedVehicleState` from the standalone paragraph to the group paragraph; "all five must be non-null" → six |
| `CombatView/RunSceneHost.cs:428-439` | The `loadedVehicleState` "independent of the session_core gate (Slice 9a Q1)" paragraph — deleted outright |

No "formerly standalone" phrasing. State the current rule only.

### 2d. Unrelated doc drift, fixed opportunistically

`CombatView/SaveBootstrap.cs:36-41` claims **six** standalone group-of-one
adapters and then names **five** — `PendingPartOfferSerializable` is registered
(`:151`) but missing from the enumeration. Found during the 2026-08-27 review.
The same block has to be edited for this slice anyway.

---

## 3. What is NOT changing

- **No `SCHEMA_VERSION` bump on any DTO.** Membership is a host-side load gate
  (`RunSceneHost.cs:480-484`), not a payload property — `SaveSystem.LoadCategory`
  dispatches entry-by-entry with no notion of groups (`SaveSystem.Load.cs:235`,
  `:260-264`). The wire shape is untouched.
- Nothing about `ExitCombat`, the resolution seams, or the 2026-08-27 ordering
  fix.
- No new adapter, no registration change in `SaveBootstrap.Bind`.

---

## 4. Proposed changeset

1. `RunSceneHost.cs:480-484` — add `&& snapshot.LoadedVehicleState != null` to
   the gate.
2. `RunSceneHost.cs:653-660` — add the `loadedVehicleState` null-throw to the
   guard block.
3. `RunSceneHost.cs:666-667` — replace the `if` with unconditional
   `loadedVehicleState.ApplyTo(player);`.
4. The four doc rewrites in §2c, plus the §2d count fix.
5. **New test** `Initialize_NullVehicleState_FallsBackToBeginNewRun` in
   `RunSceneHost_Resume_Test.cs` — plant a full envelope minus
   `run.vehicle_state`, assert `BeginNewRun` semantics (fresh map, not the
   loaded one). Without it the gate widening is untested and a future refactor
   silently un-widens it.
6. `LoadedRunSnapshot_Test.cs:136-138` — the xmldoc claiming "every standalone
   field" now miscounts. Adjust the comment; **keep the tests** — the payload
   class is a dumb carrier and must keep accepting nulls.

### 4b. TD Lens-1 recommendation — included

Move the six-term conjunction to a get-only
`LoadedRunSnapshot.HasCompleteSessionCore` property, so `Initialize` reads
`if (snapshot.HasCompleteSessionCore)`.

TD's reasoning: at six terms, a future seventh member added to
`LoadedRunSnapshot` but forgotten in the conjunction is a silent,
test-invisible bug **of exactly the class this slice is fixing**. Declaring
membership once, beside the fields, removes that. TD explicitly called this
"not scope creep" — `LoadedRunSnapshot.cs:34-39` already names itself the
growth surface.

---

## 5. Verification plan

- EditMode suite must stay green. Current baseline: **1154 / 1152 / 0 failed /
  2 skipped**, 0 `error CS`, at Unity `823a677`.
- Per-test diff against that baseline; the only expected delta is the one added
  test.
- Per memory `feedback-prove-test-fails-on-the-bug`: the new gate test must be
  **validated by reversion** — temporarily remove the `&& LoadedVehicleState !=
  null` term and confirm the test fails, then restore. A passing test here
  proves nothing until it is known to fail without the fix.

---

## Technical Director Review

> Verdict from the `technical-director` agent, 2026-08-27, Item 0 of a six-item
> adjudication. Reproduced verbatim.

**VERDICT: APPROVE — (a) correct under the Slice 8d criterion; (b) confirmed —
no bump, and a bump would be actively harmful; (c) confirmed — delete the
fallback branch, but the three doc blocks asserting the old membership must die
in the same commit.**

**(a) The criterion is met, and it is met on the progression limb, not the
cosmetic one.**

The criterion quoted at `Save/Dtos/FuelStateDto.cs:15-17` is *"a member joins
`run.session_core` if its absence-with-others-present creates a silently-broken
determinism or progression invariant."* The current rationale for keeping
VehicleState standalone is at `Save/Dtos/VehicleStateDto.cs:20-27` and repeated
at `CombatView/RunSceneHost.cs:429-438`: *"a missing vehicle DTO means 'restore
to chassis-fresh state,' which is a recoverable cosmetic loss (player sees a
healed vehicle when they expected damaged)."*

That reasoning was correct when it was written and is now false, and the thing
that falsified it is `RunInventory` joining the group at Slice A2
(`CombatView/LoadedRunSnapshot.cs:60-66`). Before parts, the vehicle carried
only *HP*, and HP loss is genuinely cosmetic-recoverable. After A1-A3, the
vehicle carries *identity*: `SlotSnapshotDto` persists `InstalledPartId`,
`InstalledPartDisplayName`, `MountDirection` (`VehicleStateDto.cs:139-142`).

The deletion is exact. Equipping is swap-only (`Combat/Vehicle.cs:314`
`SwapPart`, which requires an installed part and returns the displaced one)
paired with `RunInventory.Remove` (`Run/RunInventory.cs:121`). So an equip
transaction *moves* a `PartInstance` from the inventory list onto a
`SlotInstance`. If inventory rehydrates (it must — it is a group member) and
vehicle state does not, the equipped part is in neither store. No exception, no
log, no `LoadResult` entry the player ever sees: `SaveSystem.Load.cs:243-247`
records the skip in `skipped` for diagnostics only. That is textbook
*silently-broken progression invariant*. It qualifies.

Worth stating the sharper form for the capture: the move is not "VehicleState
got more important." It is that **`run.session_core` membership is not a
property of a DTO — it is a property of the transaction graph between DTOs.**
`RunInventory` and `VehicleState` are two halves of one conserved quantity. Once
a verb moves rows between them, they are one atomic unit, and the group already
contains one half. Same logic would apply in reverse if inventory were the
standalone one.

**(b) No `SCHEMA_VERSION` bump. Your claim is right, and the reason it is right
is stronger than "the wire shape is unchanged."**

Two independent confirmations:

1. *Mechanically, membership is not in the payload.* The gate is a host-side
   null-conjunction at `CombatView/RunSceneHost.cs:480-484`.
   `SaveSystem.LoadCategory` never sees it — it dispatches entry-by-entry,
   comparing `entryObj["schema_version"]` against `runHandler.SchemaVersion`
   (`SaveSystem.Load.cs:235`, `:260-264`) with no notion of groups. Membership
   is enforced strictly downstream of load. Nothing about the bytes changes.

2. *Bumping would be a self-inflicted wipe.* `VehicleStateDto.SCHEMA_VERSION` is
   already 2 (`VehicleStateDto.cs:67`), bumped at the A2/Q3 window.
   `SaveBootstrap.Bind` registers `_vehicleStateSerializable` unconditionally
   (`CombatView/SaveBootstrap.cs:127-128`, `:146`), and `VehicleStateDto.From`
   always emits (`VehicleStateDto.cs:99-109`) — there is no null-skip path.
   Therefore **every** save written since A2 carries a v2 `run.vehicle_state`
   entry. Bump to 3 and every one of them mismatches at
   `SaveSystem.Load.cs:260`, `LastLoaded` stays null, the newly-widened gate
   fails, and every in-flight save regenerates into a fresh run. The bump does
   not protect the transition — it *is* the destructive event you would be
   trying to avoid.

The residual behavior change is the correct one and should be named in the
capture: a save that predates A2 (or one whose vehicle entry is corrupt)
previously resumed with a healed vehicle; it will now regenerate the whole run.
That is the intended semantics of the group and per `SaveSystem.Load.cs:211-216`
the EA-mode any-mismatch policy already accepts it for the other five members.

**(c) Delete the branch. It is `ADR-0011 #3` (bimodal path), and it is the
*second* one in that method.**

Post-move, `snapshot.LoadedVehicleState != null` is a gate precondition, so
`RunSceneHost.cs:666-667`'s `if (loadedVehicleState != null)` is a branch whose
false limb is unreachable — a dead second shape guarded by a null-check, which
is precisely the pattern `BeginRunFromLoaded`'s own xmldoc at `:620-626` cites
as the reason it exists as a sibling method rather than an optional-parameter
overload. Leaving it would make the method violate the rule it was created to
satisfy.

Convert it to the shape the four existing members already use: a top-of-method
`ArgumentNullException` alongside `loadedMap` / `loadedDeck` / `loadedInventory`
/ `loadedStormState` (`RunSceneHost.cs:653-660`), then an unconditional
`loadedVehicleState.ApplyTo(player)`.

### TD three-lens self-audit

- **Codebase health.** ADR-0011 drift: grepped, and the drift is in *comments*,
  not code — three blocks listed above, all load-bearing prose.
  Single-responsibility: the gate stays where it belongs (`Initialize`, which
  already owns the resume-vs-fresh decision); no controller surface grows.
  Teardown races: none — `Initialize` runs once at boot, off
  `SaveBootstrap.LoadAndInitialize` (`SaveBootstrap.cs:161-201`), no
  subscription lifecycle involved. **One delta:** the gate is now a six-term
  hand-written conjunction. At six terms, a future seventh member added to
  `LoadedRunSnapshot` but forgotten in the conjunction is a silent,
  test-invisible bug of exactly the class this item is fixing. Recommend the
  conjunction move to a `LoadedRunSnapshot.HasCompleteSessionCore` get-only
  property on the payload class itself, so membership is declared once, next to
  the fields, and `Initialize` reads `if (snapshot.HasCompleteSessionCore)`.
  This is not scope creep — it is the same single-source fix as Item 5, and
  `LoadedRunSnapshot.cs:34-39` already names itself "the growth surface."
- **Optimization.** Confirmed, no delta. One extra null-check at boot;
  `ApplyTo` loops `Slots` once (`VehicleStateDto.cs:137-143`), unchanged in
  cost. No per-frame path touched.
- **1.0 survival.** The gate shape survives — it is the same shape that has
  absorbed StormState and RunInventory. `HasCompleteSessionCore` survives better
  than the inline conjunction. The **risk to name explicitly**: this move makes
  a corrupt or schema-skipped `run.vehicle_state` fatal to the whole run rather
  than cosmetic. At 1.0 with real players, that is a support-visible failure
  mode. It is the *correct* trade (a wiped run beats a silently deleted
  legendary), but it raises the stakes on `Vehicle.RestoreSlotState`'s tolerance
  for unknown `slotId` / unknown `partId` — a part deleted from the catalog
  between patches would now brick the run instead of losing a slot. That is out
  of scope here, but it is the next domino and belongs in the capture as a
  follow-on risk.

---

## 6. Independent corroboration (`unity-specialist`, 2026-08-27)

Asked separately whether anything in the resume path assumes
`LoadedVehicleState` may be null after the gate:

> Only one production consumer exists: `BeginRunFromLoaded`'s own
> `if (loadedVehicleState != null) loadedVehicleState.ApplyTo(player);`
> (`RunSceneHost.cs:666-667`) — which the change deletes. I found no other
> reader of `LoadedRunSnapshot.LoadedVehicleState` anywhere in the codebase, so
> no other call site breaks mechanically.

It independently raised both the null-throw and the doc-rewrite requirements,
and added: if the doc blocks are not touched in the same commit, the change
**creates a fresh instance of save-doc/code drift on day one**.

On `BeaconActivator.LoadCurrentBeaconAsync` (the untested resume-path
`IsResolved` read): no direct dependency — it never reads `VehicleStateDto` or
`LoadedRunSnapshot`, and only reads `_host.CurrentBeacon` after `Initialize` has
resolved to one branch. It will not break mechanically. But it flagged the
second-order effect independently of the TD:

> Moving VehicleState into the atomic group means a corrupt/missing *vehicle*
> entry alone now forces the **entire** session_core group to regenerate,
> trading today's narrow "heal the vehicle, keep the run" recovery for "lose the
> whole run" — while every other standalone member keeps the lenient
> per-category fallback. `StormState`'s prior join carried an explicit "silent
> divergence is worse than a fresh run" rationale
> (`StormStateSerializable.cs:19-24`); I found no equivalent rationale yet for
> Vehicle.

**That rationale is §1 of this capture** and should be written into
`VehicleStateDto`'s new doc block, mirroring `StormStateSerializable.cs:19-24`,
so the next reader finds the justification where they will look for it.

---

## 6b. What implementation actually cost vs what §2/§4 predicted

Recorded because the gap is informative, not to correct the record cosmetically.

### Doc blocks: predicted 4, actual 7

§2c listed four. A codebase-wide sweep (run BEFORE editing this time — the
2026-08-27 seam slice taught that lesson) found three more:
`RunSceneHost.cs:629`, `SaveBootstrap.cs:184`, `VehicleStateDto.cs:83`.

### Test fixtures: predicted 1 new test, actual 1 new + 5 envelopes + 1 deletion

This is the real surprise, and it is the strongest evidence FOR the
`HasCompleteSessionCore` property.

Adding a sixth member meant every hand-built "valid resume" envelope in the
suite was silently incomplete. Five needed the new entry:

| Fixture | Consequence if missed |
|---|---|
| `PlantResumeFixture` | 3 tests fail — resume never happens |
| `Resumed_RunDeck_Carries_Loaded_Cards_Verbatim` | Failed the run: expected 14 cards, got 13 (fresh starter deck) |
| `Fresh_Run_When_NodeMap_Schema_Skipped_...` | **Would have kept PASSING — for the wrong reason** |
| `Fresh_Run_When_RunDeck_Schema_Skipped_...` | **Would have kept PASSING — for the wrong reason** |
| `PlantResumeFixtureWithVehicleState` | already correct |

The two skip-lock tests are the dangerous ones: each regenerates the group on
purpose, so an absent vehicle entry still produces a green result while the test
silently stops exercising the skip it exists to lock. Only the deliberate sweep
caught them.

### One test DELETED, not rewritten

`Vehicle_Resumes_To_ChassisFresh_When_VehicleStateDto_Missing` asserted the
pre-join contract. Once `PlantResumeFixture` gained its vehicle entry, that test
would have **passed while constructing nothing it claimed to test** — its name
still advertising coverage of a case it no longer built. Replaced by
`Fresh_Run_When_VehicleState_Absent_SessionCoreMembershipLock`, which builds the
envelope explicitly.

That is the third instance today of a test whose name promised coverage its body
no longer provided (after the two `..._BeforeMarkResolved` ordering tests). Worth
treating as a codebase pattern rather than three coincidences: fixtures here are
shared and hand-built, so widening a contract quietly changes what old tests
actually construct.

### One addition beyond §4

`VehicleStateDto.SCHEMA_VERSION` gained an explicit do-not-bump warning. The TD's
reasoning — every save since A2 carries a v2 entry, so a bump invalidates all of
them and regenerates every in-flight run; *the bump IS the destructive event* —
existed only in this capture. It now sits on the constant someone would edit.

---

## 6c. Reversion probe — the gate lock is genuine

Per memory `feedback-prove-test-fails-on-the-bug`, the new test was validated by
temporarily removing `&& LoadedVehicleState != null` from
`HasCompleteSessionCore` and re-running the fixture.

`Fresh_Run_When_VehicleState_Absent_SessionCoreMembershipLock` **failed** —
and failed in a more useful way than expected:

```
System.ArgumentNullException : Value cannot be null.
Parameter name: loadedVehicleState
```

The gate let the null through and `BeginRunFromLoaded`'s null-throw (§4 item 2)
caught it. **The two-layer defence is doing real work, not ceremony.** Without
that throw a weakened gate would surface as a `NullReferenceException` deep in
`VehicleStateDto.ApplyTo`, or — worse — as a silent chassis-fresh resume, which
is the original defect reintroduced.

Restored immediately after; final suite re-verified.

### Verification result

**1154 total / 1152 passed / 0 failed / 2 skipped**, 0 `error CS`.
Diff against the `823a677` baseline: exactly one test added
(`Fresh_Run_When_VehicleState_Absent_SessionCoreMembershipLock`) and one removed
(`Vehicle_Resumes_To_ChassisFresh_When_VehicleStateDto_Missing`). Nothing else
moved.

---

## 7. Follow-on risk (NOT this slice)

Once vehicle state is run-fatal, `Vehicle.RestoreSlotState`'s tolerance for an
unknown `slotId` / unknown `partId` becomes load-bearing: a part deleted from
the catalog between patches would brick the run rather than lose a slot. Both
reviewers named this independently. Needs its own slice before any content
patch that removes an authored `PartDefinitionSO`.

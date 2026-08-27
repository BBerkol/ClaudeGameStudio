# Capture — Resolution-Seam Enqueue Ordering (all five beacon families)

**Date:** 2026-08-27
**System:** `RunSession` resolution seams + ADR-0004 autosave enqueue
**Trigger:** Confirmed reward-duplication defect (user repro, 2026-08-27)
**Status:** AWAITING USER APPROVAL — no edits made

---

## 1. The defect (CONFIRMED by user repro, not static analysis alone)

**Repro, performed by user 2026-08-27:**
1. Win a combat.
2. Claim the card reward. (Part pool is empty by design — card is the only reward.)
3. Quit while standing on the map, **without advancing to the next beacon**.
4. Relaunch.

**Observed:** the game resumes **at the start of the same combat**, and the
claimed card **is already in the deck**.

**Consequence:** unbounded duplication. Each refight/claim/quit cycle mints
another card. Scrap and fuel credited by `ExitCombat` are likewise retained
across the refight.

### Root cause

`RunSession.ResolveCombatRewards` (`RunSession.cs:979-980`):

```csharp
OnCombatModelCommitted?.Invoke();      // :979  — autosave snapshot taken HERE
_controller.MarkCombatBeaconCleared(); // :980  — beacon marked resolved AFTER
```

`SaveSystem.EnqueueRunStateWrite` captures the DTO set **synchronously on the
calling thread** (`SaveSystem.Write.cs:168` → `SnapshotRunRegistry`, `:262`),
not lazily at flush time. So the persisted snapshot contains:

- `run.run_deck` — **card present** (session_core member, correctly captured)
- `run.node_map` — combat beacon **`is_resolved: false`**

The two halves of one transaction land in different states. On resume,
`BeaconActivator.cs:215` (`Start || IsResolved` → `ClearAll()`) sees an
unresolved combat beacon, mounts the combat root, and the fight replays.

The state self-corrects **only** if the player advances first —
`AdvanceToNextBeacon` enqueues again (`RunSceneHost.cs:844`, `:849`), capturing
post-resolution state. This is why the 2026-08-27 15:00 playtest save shows
beacons 0/1/21 correctly resolved: that session advanced onward each time.

### Scope: all five seams share the shape

`RunSession.cs:893-895` documents the pre-`MarkResolved` fire order as shared
by `ResolveRest` / `ResolveEvent` / `ResolveMerchant` / `ResolveChopshop`.
Each mutates wallet/vehicle/inventory state inside its modal **before** the
seam runs, so each has the same window:

| Seam | Invoke | MarkResolved | Committed-before-latch state at risk |
|---|---|---|---|
| `ResolveRest` | `:813` | `:814` | repairs applied, fuel spent |
| `ResolveEvent` | `:851` | `:852` | scrap↔fuel conversion |
| `ResolveMerchant` | `:880` | `:881` | purchases, scrap spent |
| `ResolveChopshop` | `:915` | `:916` | part swaps, welding repairs |
| `ResolveCombatRewards` | `:979` | `:980` (via `MarkCombatBeaconCleared`) | **card/scrap/fuel — CONFIRMED** |

Only combat is confirmed by repro. The other four are the same code shape and
should be fixed in the same pass rather than left as four latent instances.

---

## 2. What is being destroyed / changed

**No authored designer values are destroyed.** No prefab, scene, SO, or tuned
balance value is touched. What is destroyed is a **documented rationale** and
a **statement ordering**.

### 2a. Rationale being deleted (`RunSession.cs:940-944`)

Current text, to be removed:

> Pre-MarkResolved fire order matches the four siblings — the snapshot
> captures state with the beacon still unresolved, so a crash in the gap
> replays the beacon rather than skipping it.

**Why it must go, not be softened:** this rationale is only sound when the
beacon's effects are *not yet committed*. At the resolution seam they always
are — `ExitCombat` (`RunSession.cs:774-777`) has already credited scrap, fuel,
and latched the offers before `ResolveCombatRewards` is reachable. So
"replays the beacon" is the harmful outcome and "skips it" is the safe one.
The comment documents the defect as a feature, which is how it survived review.

The sibling statement at `RunSession.cs:893-895` carries the same claim for the
other four seams and needs the same treatment.

### 2b. Statement ordering being changed (five sites, two lines each)

```
RunSession.cs:813-814   ResolveRest
RunSession.cs:851-852   ResolveEvent
RunSession.cs:880-881   ResolveMerchant
RunSession.cs:915-916   ResolveChopshop
RunSession.cs:979-980   ResolveCombatRewards
```

In each, `OnXxxModelCommitted?.Invoke()` moves to **after** the `MarkResolved`
call.

### 2c. What is NOT changing

- No `SCHEMA_VERSION` bump on any DTO. Wire shapes are untouched; only the
  *values* captured at snapshot time change (a `bool` that was wrong is now
  right).
- No change to `ExitCombat`'s credit ordering.
- No new enqueue sites. `EnqueueRunStateWrite` keeps its single call site
  (`RunSceneHost.cs:1174`).
- Existing saves are unaffected — a save already on disk with an unresolved
  beacon still resumes into the refight. **This fix prevents new occurrences;
  it does not repair saves already in the bad state.** Flagged as a known
  limitation, acceptable in EA.

---

## 3. Safety evidence gathered before proposing

The TD verdict flagged one behavioural risk: *"confirm no view subscriber to
`OnCombatModelCommitted` reads `IsResolved` expecting `false`."*

**Verified — the risk does not exist.** Every one of the five events has
exactly one subscriber, and every handler is a pure enqueue with no view logic:

| Event | Subscriber | Handler body |
|---|---|---|
| `OnRestModelCommitted` | `RunSceneHost.cs:589`, `:729` | `:1094-1097` → `EnqueueRunStateSnapshot();` |
| `OnEventModelCommitted` | `:590`, `:730` | `:1106-1109` → `EnqueueRunStateSnapshot();` |
| `OnMerchantModelCommitted` | `:591`, `:731` | `:1119-1122` → `EnqueueRunStateSnapshot();` |
| `OnChopshopModelCommitted` | `:592`, `:732` | `:1132-1135` → `EnqueueRunStateSnapshot();` |
| `OnCombatModelCommitted` | `:593`, `:733` | `:1153-1156` → `EnqueueRunStateSnapshot();` |

No subscriber observes beacon resolution state. The reorder is therefore
invisible to everything except the persisted snapshot — which is the thing
being fixed.

The offer-drainability concern that motivated the original ordering is already
enforced independently by the terminus assertion at `RunSession.cs:973-977`,
which throws if any offer is still latched when `ResolveCombatRewards` runs.
That guard is untouched.

---

## 4. Proposed changeset

1. **`RunSession.cs`** — swap the two statements at each of the five seams
   (`:813-814`, `:851-852`, `:880-881`, `:915-916`, `:979-980`).
2. **`RunSession.cs:940-944`** — replace the inverted rationale with the
   correct one: the snapshot must capture post-resolution state so a quit in
   the reward window cannot replay a beacon whose effects are already banked.
3. **`RunSession.cs:893-895`** — same rewrite for the sibling family statement.
4. **New test** in `RunSceneHost_EnqueuesWrite_Test.cs`:
   `PostCombatSnapshot_MarksBeaconResolved_SoResumeCannotRefight` — drive a
   combat to a win, `ExitCombat`, drain offers, `NotifyRewardClaimed`, drain
   the save queue, assert the persisted `run.node_map` entry for the combat
   beacon has `is_resolved == true` **and** the deck contains the claimed card.
   This test fails on today's code — that is its value.
5. **Second test:** `NotifyRewardClaimed_EnqueuesExactlyOnce` — the assertion
   originally mis-stated as "does not enqueue." It enqueues transitively via
   the seam by design; what must be locked is that it does so exactly once.

**Deliberately excluded from this changeset** (each is its own slice):
- `HasLiveEncounter` predicate extraction (4 call sites, view layer)
- `VehicleStateDto` → `run.session_core` membership move
- `BuildScout` deletion
- `PendingEventOffer` retirement

Rationale: this fix and the membership move both alter resume semantics.
Landing them together makes any regression ambiguous between two causes.

---

## Technical Director Review

> Verdict captured from the `technical-director` agent, 2026-08-27, Item 4 of a
> six-item adjudication. Reproduced verbatim in the portions bearing on this
> change.

**VERDICT: The test is worth adding — but the assertion as stated is factually
wrong, and chasing it surfaced a live save-corruption defect that outranks
every other item in this brief.**

**The assertion as stated cannot pass.** `NotifyRewardClaimed` *does* cause an
enqueue. `RunSceneHost.cs:923` calls `_session.ResolveCombatRewards()`, which
fires `OnCombatModelCommitted` at `RunSession.cs:979`, which is bound to
`HandleCombatModelCommitted` at `RunSceneHost.cs:593` / `:733`, which calls
`EnqueueRunStateSnapshot()` at `:1155`. A test asserting "does NOT enqueue"
fails on day one. The intent is right — *no direct enqueue in
`NotifyRewardClaimed`, the transitive one via the seam is the only one* — but
the stated reason does not support the stated assertion.

**Following that reason to its end found the real defect.** `RunSession.ExitCombat`
credits the wallet at `:774` (`Scrap.Add`) and `:775` (`Fuel.CreditFuel`),
latches both offers at `:776-777`, and correctly does **not** enqueue. Good.
The beacon is marked resolved much later, at `RunSession.cs:980`
(`MarkCombatBeaconCleared`) — which runs **after** `OnCombatModelCommitted?.Invoke()`
at `:979`. That ordering is deliberate and documented at `:940-944`: *"the
snapshot captures state with the beacon still unresolved, so a crash in the gap
replays the beacon rather than skipping it."*

That rationale is inverted, and here is why it matters. `SaveSystem.EnqueueRunStateWrite`
calls `SnapshotRunRegistry()` **synchronously on the caller thread**
(`SaveSystem.Write.cs:165-173`) — the DTO projection happens at enqueue time,
before `MarkCombatBeaconCleared` mutates anything. `NodeMapDto` persists
per-beacon resolution (`NodeMapDto.cs:148`, `:249-250`). And I traced every
`EnqueueRunStateSnapshot()` call site in `RunSceneHost` (`:602`, `:740`, `:844`,
`:849`, `:1096`, `:1108`, `:1121`, `:1134`, `:1155`) — **none of them fire after
`MarkCombatBeaconCleared`.**

Therefore the durable on-disk state after every won combat is: **scrap
credited, fuel credited, offers drained, beacon `is_resolved: false`** — and it
stays that way until the player's next `AdvanceToNextBeacon`. This is not a
crash window. It is the normal quit-from-the-map-screen path.

On resume, `BeaconActivator.cs:215` early-returns only for `Start` or resolved
beacons, so an unresolved Combat beacon **mounts the combat root and the player
refights it** — with the previous victory's scrap and fuel already in the
wallet. Every quit on the post-combat map screen is a repeatable reward
duplication, and the refight also re-rolls fresh card and part offers.

**This is a family-wide ordering defect, not a combat one.** `RunSession.cs:893-895`
states the pre-`MarkResolved` fire order is shared by `ResolveRest` /
`ResolveEvent` / `ResolveMerchant` / `ResolveChopshop`. Rest has the same shape:
per-tick repair drains mutate the vehicle and wallet inside the modal,
`ResolveRest` fires `OnRestModelCommitted` then marks resolved — so a quit after
a Rest persists "repairs applied, beacon unresolved," and the resume re-opens
Rest with a fresh repair budget. Same for Chopshop, whose mutations `:888-891`
explicitly happen inside the modal before the Leave seam.

The stated rationale — *"replays the beacon rather than skipping it"* — is only
safe when the beacon's effects are **not yet committed**. At the resolution seam
they always are. Replaying is the harmful outcome; skipping is the safe one. The
ordering that protects offer-drainability (the real, hard-won lesson at
`RunController.cs:320-335`) is a *different* concern and is already satisfied by
the terminus assertion at `RunSession.cs:973-977`, which guarantees no offer is
latched when the enqueue fires.

I want to be precise about confidence: I traced this statically and every link
is unambiguous, but I have not run the game. **Confirm with a two-minute manual
repro before acting:** win a combat, take both rewards, quit from the map
screen, relaunch — expect the same fight with the previous payout banked.

### Concrete action (TD)

**B. Fix the ordering — separate slice, own capture.** Move the enqueue to after
the resolution latch at all five seams. Lowest-risk shape: keep the
`OnXxxModelCommitted` events where they are (view subscribers may depend on
pre-resolve timing) and add the enqueue *after* `MarkResolved`. Cleanest shape:
move `OnCombatModelCommitted?.Invoke()` to after `_controller.MarkCombatBeaconCleared()`
at `RunSession.cs:979-980` and the equivalent at the other four. Either way,
`RunSession.cs:940-944` and `:893-995` must be rewritten — as they stand they
document the defect as a feature, which is how it survived review.

**C. Do not bundle B with the `session_core` move.** Both touch resume
semantics; debugging them together is how you lose a day.

### TD three-lens self-audit (Item 4)

- **Codebase health.** The five seams share an ordering contract stated in prose
  across five xmldocs and enforced nowhere — that is the single-source problem
  again. Once the ordering is fixed, one shared test that walks all five
  resolution verbs and asserts "persisted snapshot has `is_resolved == true`" is
  worth more than five per-verb tests. **Delta:** `MarkCombatBeaconCleared`
  throws on an already-resolved beacon (`RunController.cs:386-390`) with a
  message about *"a duplicate `NotifyRewardClaimed` subscriber"* — good, that
  stays valid under the reorder. Confirm no view subscriber to
  `OnCombatModelCommitted` reads `IsResolved` expecting `false`; that is the one
  behavioural risk in the reorder.
- **Optimization.** Confirmed, no delta. Reordering two statements.
  `SnapshotRunRegistry` cost is unchanged and already synchronous on the main
  thread at every enqueue — a real but pre-existing main-thread cost, worth a
  profiler look someday, explicitly not now.
- **1.0 survival.** Critical. This is a reward-duplication exploit and a
  progression-integrity defect; it does not survive to 1.0 in any form. The test
  shape survives — `ReadPayload()` + `is_resolved` assertion is the canonical way
  to test any future resolution seam, and every new beacon type should copy it.
  **Risk named:** the fix changes what a mid-chain crash does (replay → skip).
  For combat that is strictly right. For a future beacon type with a genuinely
  *resumable* multi-step interaction, skip would be wrong — such a type would
  need its own intermediate persistence rather than leaning on the unresolved
  flag. Say so in the rewritten xmldoc so the next author does not lean on the
  old, wrong rationale.

---

## 5. Post-capture addendum — evidence gathered after the TD verdict

Two of the TD's stated uncertainties have since been resolved empirically:

1. **"I have not run the game."** — User repro on 2026-08-27 **confirms** the
   defect. Refight occurs; claimed card is retained in the deck. The TD's
   severity framing ("every quit on the post-combat map screen") is accurate for
   the window between claiming and advancing; the 15:00 playtest save
   demonstrates the state self-corrects on advance, so steady-state saves are
   not corrupt.

2. **"Confirm no view subscriber reads `IsResolved` expecting `false`."** —
   Verified: all five events have exactly one subscriber each, every handler is
   a bare `EnqueueRunStateSnapshot()` call (see §3). The TD's "lowest-risk shape"
   hedge (keep events in place, add a separate enqueue) is therefore
   unnecessary — the **cleanest shape** (move the invoke) carries no additional
   risk and avoids adding a second enqueue path, which would itself be an
   ADR-0011 parallel-path smell.

One correction to the TD text, preserved above as written: it references
`:893-995` where `:893-895` is meant.

---

## 6. Post-implementation amendments (2026-08-27, after code review)

Reviewed by `unity-specialist` on the completed diff. Verdict:
**APPROVE-WITH-CHANGES**. Its findings corrected three claims made earlier in
this capture. Recorded here rather than silently edited above, so the record
shows what was believed at proposal time versus what proved true.

### 6a. Merchant severity was OVERSTATED in §1

§1's table lists Merchant as risking "purchases, scrap spent". That is wrong.
`PendingMerchantOffer` is **never persisted** — it is registered with no
`IRunStateSerializable`, carries no DTO, and is deterministically regenerated
on resume from `DeriveMerchantOfferSeed` (`RunSceneHost.cs:705-711`).
Purchased-entry state rides `MerchantVisits`, which persists independently of
the beacon's resolved flag.

So the Merchant instance of this bug was **"quit-resume re-shows the merchant
screen you thought you had left"** — annoying, not exploitable. Rest, Event and
Chopshop mutate persisted vehicle/wallet state directly and WERE exploitable
the same way Combat was (free repairs, double-convert, free part swaps/welds).
The fix is correct for all five regardless; only the severity claim was wrong.

### 6b. The fix ALSO closes a boss-replay defect — not noticed at proposal time

`RunController.MarkCombatBeaconCleared` (`:392-399`) sets `IsResolved = true`
**and**, for a terminal beacon, `_state.Status = RunStatus.Victory`. Both now
happen before the snapshot.

Pre-fix, a boss kill persisted `Status: Ongoing` + `is_resolved: false`. Quit
after claiming the boss reward but before any later snapshot, and resume
**replayed the boss fight**. Same defect class, terminal beacon, never
mentioned in the original analysis or the TD verdict.

`RunSceneHost.NotifyRewardClaimed`'s `IsRunComplete` branch (`:930-934`) is
unaffected either way — `RunController.IsRunComplete` (`:95`) is positional
(current beacon type == terminal type), independent of both `Status` and
`IsResolved`.

### 6c. Stale-doc blast radius was THREE TIMES larger than §4 stated

§4 proposed rewriting two doc blocks in `RunSession.cs`. The true count, after
a codebase-wide sweep prompted by the review, was **19 across four files**:

| File | Blocks | Notes |
|---|---|---|
| `RunSession.cs` | 9 | five event xmldocs + four method xmldocs |
| `RunSceneHost.cs` | 7 | six `HandleXxxModelCommitted` xmldocs + the `NotifyRewardClaimed` inline comment |
| `RunController.cs` | 2 | `ResolveCombat` inline comment + `CommitMerchantPurchase` xmldoc |
| `RunSession_ResolveRest_test.cs` | 1 | class-level summary |

The first sweep grepped only `RunSession.cs` and reported "zero stale docs
remaining" — true of that file, false of the codebase. The reviewer found three
of the remaining ten; a subsequent full-codebase grep found the other seven.

This matters beyond bookkeeping: every one of those blocks asserted the
inverted ordering as fact, and §2a of this capture identifies exactly that —
comments documenting the defect as a feature — as the mechanism by which the
original bug survived review. Codebase-wide grep for
`crash-window|pre-MarkResolved|snapshot-before-latch` now returns zero.

---

## 7. Test coverage as shipped

### 7a. The two corrected tests are PROXIES

`ResolveRest_FiresOnRestModelCommitted_AfterMarkResolved` and
`ResolveCombatRewards_FiresCommittedEvent_AfterMarkResolved` assert
`IsResolved` **in memory, inside the event handler, on the same call stack**.
Neither touches `SaveSystem` or reads a persisted payload. That is precisely
why their pre-correction versions passed for months while the on-disk record
was wrong — they asserted an in-memory invariant that was never false, while
the defect lived one layer down in what `SnapshotRunRegistry()` captured.

### 7b. The real regression lock, and proof it is not vacuous

`RunSceneHost_EnqueuesWrite_Test.RewardClaimSnapshot_PersistsResolvedBeacon_SoResumeCannotRefight`
reads the serialized `runstate.sav` payload and asserts `is_resolved == true`
on the combat beacon.

**Validated by deliberate reversion**: the combat seam was temporarily restored
to its buggy order and the test re-run. It **failed**, with
`Expected: True / But was: False`. Restored immediately after. A green test is
not evidence unless it is known to fail on the defect — this one is.

**Scope limit, stated honestly.** The test drives the seam WITHOUT a live
fight. `RunSession.EnterCombat` routes through `SceneEncounterBuilder`, which
requires enemy prefab binders wired into `RunSceneHost._combatBeaconArchetypes`
— prefab infrastructure that does not belong in an EditMode save test. The
first attempt did try a real combat and failed on exactly that
(`no EnemyArchetypeBinder resolved for archetype 'Dredge'`).

Skipping `ExitCombat` means no reward offer is ever latched, which
`ResolveCombatRewards` accepts. The seam under test is identical either way,
because the enqueue reads the beacon's resolved flag and nothing about the
fight.

**NOT covered:** that the claimed card persists alongside the resolved beacon —
the other half of the transaction whose disagreement defined the bug. That
needs the prefab-backed combat path and belongs with the PlayMode harness on
the tooling slice.

---

## 8. Carried forward — NOT fixed here

1. **`RunSceneHost.EnqueueRunStateSnapshot` (`:1170-1183`) swallows
   `InvalidOperationException` broadly** and always logs it as "SaveSystem not
   bound". But `VehicleStateSerializable.ToDto` (`:72-78`) throws that same
   type for its own invariant violations. Post-reorder this is sharper than it
   was: an exception there now fires AFTER `MarkCombatBeaconCleared` has
   flipped `IsResolved` and the wallet/deck mutations are live, so the snapshot
   never lands, disk keeps the previous (unresolved) record, and a quit
   reproduces this exact duplication defect via a different trigger.
   Pre-existing — the same catch guarded the same call before — but the reorder
   changes it from cosmetically harmless to a live vector. **Own slice.**

2. **`CardRewardPickerController.OnDisable` null-out is bundled into this
   changeset and is unrelated to it.** It fixes a UIDocument re-clone trap
   where a stale `_offerElements` array disarms the guard at `:247`, costing
   the run rather than the reward. Correct fix, verified suite-green, but it
   predates this capture and belongs in its own commit.

3. **`RunController_HappyPath_Test.cs:437/447` define
   `DriveCombatToPlayerVictory` / `DriveCombatToEnemyVictory` with a DIFFERENT
   signature** (take a `Vehicle`, construct the loop, return it) from the
   extracted `CombatDriver`. Deliberately left alone — folding them in would
   force the shared helper to take on vehicle construction. This leaves two
   different functions sharing a name in the same assembly, which is a naming
   trap for the next reader.

4. **No test drives a loss through the enemy actually attacking.**
   `CombatDriver.ToEnemyVictory` stages the loss by damaging the player on the
   player's own turn via `DamageSource.Environment`, preserved verbatim from
   the copies it replaced (it bypasses enemy-intent RNG to stay deterministic).
   Whether `EnemyIntentTests` / `CombatLoopLifecycleTests` cover a genuine
   enemy-driven kill was not confirmed. Predates this work.

5. **Saves already written in the bad state are not repaired.** This prevents
   new occurrences only. A save on disk holding a credited reward against an
   unresolved beacon will still refight once.

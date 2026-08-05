# Polish Capture: Combat Resolution Seam (defer MarkResolved to the reward-chain terminus)

**Date:** 2026-08-06
**System:** Combat beacon resolution / post-combat reward chain

**Affected paths:**

Production:
- `Assets/Scripts/Run/RunController.cs` — `ResolveCombat` loses the latch; new `MarkCombatBeaconCleared`; `RestorePendingOffers` comment rewrite
- `Assets/Scripts/Run/RunSession.cs` — new `OnCombatModelCommitted` event + `ResolveCombatRewards()`
- `Assets/Scripts/CombatView/RunSceneHost.cs` — subscribe the new event, new `HandleCombatModelCommitted`, `NotifyRewardClaimed` rewrite, dropped-offer warning rewrite

Tests:
- `Assets/Tests/EditMode/Run/RunController_HappyPath_Test.cs`
- `Assets/Tests/EditMode/Run/RunSession_Test.cs`
- `Assets/Tests/EditMode/Run/RunSession_Reward_Test.cs`
- `Assets/Tests/EditMode/Run/RunSession_CardReward_Test.cs`
- `Assets/Tests/EditMode/Run/RunSession_PartReward_Test.cs`
- `Assets/Tests/EditMode/Run/RunController_RestorePendingOffers_Guard_Test.cs`
- `Assets/Tests/EditMode/CombatView/RunSceneHost_Test.cs`

## Proposed change

Move `BeaconData.MarkResolved()` — **and the terminal-boss `RunStatus.Victory`
latch, atomically with it** — out of `RunController.ResolveCombat` and into a new
`RunSession.ResolveCombatRewards()` verb fired at the end of the post-combat
reward chain. The new verb fires a new `OnCombatModelCommitted` event (snapshot
trigger) *before* marking resolved, exactly as Rest / Event / Merchant /
Chopshop already do.

This makes combat the fifth instance of an established pattern rather than the
one exception. Combat is currently the only beacon type that latches resolved
inside the model verb that reads the fight result, and the only one whose
pending offer is **born** at resolution rather than dying at it.

## Final-game picture this serves

The parts axis is the last unbuilt pillar of the 1.0 loop, and slice 8 of the
garage re-cut (fill `PartRewardPool_Biome1.asset`, delete the empty-pool gate
test) is the moment it becomes player-reachable. Today a latched card/part offer
implies an unreachable resolver — `BeaconActivator` refuses to mount a resolved
beacon's root, and both pickers live inside `Combat.prefab`. Ship slice 8 on top
of that and a mid-reward save is a dead save file.

This change removes the defect rather than compensating for it, and as a
side-effect gives combat the post-combat autosave every other beacon type
already has. Combat is currently the only encounter whose outcome does not reach
disk until the *next* beacon move.

## Authored values being destroyed

| Where | Value | Current | Replacement plan |
|---|---|---|---|
| `RunController.ResolveCombat` | `current.MarkResolved()` on the Player branch | Latches at fight-end | Moves verbatim into `MarkCombatBeaconCleared` |
| `RunController.ResolveCombat` | `if (current.Type == TerminalType) _state.Status = RunStatus.Victory;` | Latches at fight-end | Moves **with** the above — see constraint below |
| `RunSceneHost.NotifyRewardClaimed` | body | Fires `OnRunComplete` or `OnRewardClaimed` | Calls `ResolveCombatRewards()` FIRST, then fans out |
| `RunController.RestorePendingOffers` | ~30 lines of scope/remedy commentary | Describes the defect being removed | Replaced with a ~6-line invariant statement |
| `RunSceneHost` dropped-offer warning | message text | Says "a save reached disk during the post-combat window… check for a new enqueue call site" | Becomes a corruption signal — after this change there legitimately IS a snapshot on that path |
| Test names/assertions | `ResolveCombat_PlayerWins_MarksCurrentBeaconResolved`, `ExitCombat_AfterPlayerVictory_MarksBeaconResolvedAndClearsInFlight` | Assert the old seam | Renamed/split — the old names encode the bug |

No designer-tuned values, prefab overrides, scene state, SO data or balance
numbers are touched. No prefab re-author required.

## LOAD-BEARING CONSTRAINTS

**1. `MarkResolved` and the Victory latch move TOGETHER, atomically.** They are
two halves of "the terminal fight is done". Splitting them would put the run in
`Victory` while the boss beacon is still unresolved — a state no guard
anticipates. `MarkCombatBeaconCleared` holds both.

**2. `MarkResolved` must land BEFORE `OnRewardClaimed` fires.** Three consumers
compute "is the map the current presentation?" from `IsResolved`:
`RunSceneOverlayHost.HandleBeaconChanged`, `RunHUDController.HandleBeaconChanged`,
`StormAdvanceVisualPacer.IsMapCurrent`. Firing the event against an unresolved
beacon makes all three conclude "still in an encounter" — map hidden, fuel pill
hidden, queued storm sweeps never flush. A black-screen hang with no exception,
strictly worse than the bug being fixed.

**3. The save enqueue goes at the `OnCombatModelCommitted` seam and NOWHERE
else.** `RunSession.ExitCombat` credits scrap and fuel *before* the terminus, so
a snapshot anywhere between the credit and the mark resumes into a refight
against an already-credited wallet — win again, credit again. This is the same
hazard `OnRestModelCommitted` / `OnEventModelCommitted` document. Do not add an
enqueue on victory, on `ExitCombat`, or in `NotifyRewardClaimed` outside the
event.

## Boss path — verified in source, not assumed

The terminal-boss branch was the case I was least willing to take on trust.
Verified:

1. `CombatOutcomeOverlayController.OnPrimaryClicked` branches **only** on
   `Winner == CombatWinner.Player` — there is no terminal/boss branch. A boss win
   shows the same CONTINUE button and fires the same `OnContinueRequested`, so
   the reward chain reaches the terminus on the boss exactly as elsewhere.
2. `NodeMap.IsRunComplete => _beacons[CurrentIndex].Type == TerminalType` —
   purely positional. The `OnRunComplete` handoff depends on neither flag being
   moved.
3. **Zero view/UI code reads `RunState.Status`.** The entire codebase has exactly
   two reads: `ResolveCombat`'s own guard and `CommitNextBeacon`'s. The first is
   upstream; the second is unreachable because `AdvanceToNextBeacon` early-returns
   on `IsRunComplete` regardless of Status. So Status sitting at `Ongoing`
   through the boss reward window is observable by nothing.
4. `RunStatus` is not persisted, so a quit mid-boss-reward resumes to a boss
   refight — identical to a mid-combat quit today, deterministic via the
   index-derived combat seed.

**New test required (no existing coverage):** win the terminal beacon → drive the
full chain → assert beacon resolved, `Status == Victory`, `OnRunComplete` fired
exactly once. The boss path's correctness currently rests on the outcome overlay
having no boss branch — true, but incidental and unpinned.

## Technical Director Review

**Verdict:** APPROVE (with the ordering, atomicity and enqueue-placement
constraints above treated as binding)
**Spawned at:** 2026-08-05, resumed 2026-08-06
**Agent transcript:** condensed to binding rulings; full reasoning in session
transcript.

**Q1 — Move it.** `MarkResolved` + Victory latch move into
`RunSession.ResolveCombatRewards()`, which fires `OnCombatModelCommitted` then
calls a new `internal RunController.MarkCombatBeaconCleared()`.
`BeaconData.MarkResolved` is `internal`, and the controller is the declared sole
mediator of node-map mutation, hence the split. Rejected folding it into
`NotifyRewardClaimed` directly (puts a model transition in the view layer and
skips the snapshot seam) and rejected an event with no verb (an event is not a
call site — something must own the guards).

`ResolveCombatRewards` guards mirror `ResolveChopshop`, plus one new:
`HasPendingCardOffer || HasPendingPartOffer` → **throw**. That is the
compile-free assertion that the terminus really is the terminus; a future picker
refactor that forgets to drain gets a loud throw at the seam instead of an
`Advance` failing three clicks later with no causal trail. Written as an explicit
conjunction rather than a loop so a fourth reward axis is a compile-visible edit.

**Q2 — Every exit still resolves, because only one ever did.** Enumerated: player
victory non-terminal (resolves at terminus); player victory terminal (same, plus
Victory — chain runs on the boss, verified); defeat (never marked resolved today,
must not start — run ends, `RunStatus` unpersisted, resume refights, which is
shipped behaviour); flee (does not exist); auto-resolve (does not exist —
`AutoAdvanceStrandedStorm` is a storm tick and early-returns on `_inFlight`); F4
debug win (flows through the victory path); F5 restart (discards the run);
non-combat terminal arrival (`CommitNextBeacon`, different beacon, untouched).

**New hazard accepted:** resolution now depends on the view chain completing. An
unwired `_partRewardPicker` escalates from "lose a reward" to "lose the run".
Mitigation: strengthen the existing LogError text and add a prefab-validation
assert so it cannot reach a build.

**Q3 — Blast radius:** 8 production edits, ~14 test items across 5 fixtures. The
mechanical pattern is `ExitCombat → [Skip*] → Advance` gaining one line. Every
production `IsResolved` consumer verified safe; the three ordering-dependent ones
are covered by constraint 2. Not APPROVE-able on compile-green — these are
semantic test changes and need an EditMode-green attestation.

**Q4 — Add the enqueue, at the seam only.** A mid-window save now resumes by
re-mounting the combat root and refighting the same deterministic fight, then
re-offering the same deterministic reward. That is what a mid-combat quit already
does. The nanosecond gap between `Invoke()` and `MarkResolved()` carries the same
double-credit exposure the four existing seams already ship with; inventing a
stronger guarantee for combat alone would re-create the asymmetry being removed.

**Q5 — Narrow the `RestorePendingOffers` guard, do not delete it.** Before the
cure it fires on 100% of restored card/part offers — bridge-shaped, a permanent
compensator for a design defect. After, it fires only on genuinely corrupt input
the writer can no longer produce, which is an integrity check and correct to
keep. Keep the `int` return and the host warning; rewrite both messages; delete
the ~30 lines of transitional commentary (ADR-0011 #7 — leaving them IS the
drift). Keep the guard symmetric across all three offer types.

**Correction to my brief, accepted:** `PendingEventOfferDto` is **not**
write-only. Event beacons resolve *at* drain time, so an event offer legitimately
coexists with an unresolved beacon and round-trips for real. Only card and part
are structurally write-only pre-cure.

**Q6 — Order: [this] → 3 → 4 → 5 → 8.** Hard: must precede slice 8, which is what
makes the bug player-reachable. Strong preference: precede slice 3, since both
edit overlapping lines of the same 90-line `BeginRunFromLoaded`.

**Three-lens self-audit (TD):** *Health* — removes ~30 lines of ADR-0011 #7
transitional comment on net; the new verb is the fifth instance of an existing
pattern, not a parallel path; explicitly ruled AGAINST extracting a shared
`ResolveBeacon` helper across the five (guards genuinely differ; three-line
bodies). Flagged that the four existing model-committed events have no explicit
unsubscribe — correct-by-lifetime since `_session` is replaced wholesale, but
worth the whole-game audit pass. *Optimization* — one delegate invocation and one
snapshot per combat, at beacon cadence; no allocation on the verb; identical cost
to four existing seams. *1.0* — parameterless verb on `RunSession` is the shape
the other four already have at 1.0; rejected a payload struct for
`OnCombatModelCommitted` because its four siblings carry none and asymmetry is
what we are removing.

## User approval
- Reviewed: 2026-08-06
- Approved by: bertanberkol — approved the consult, then explicitly asked for the
  boss path to be verified before implementation.
- Notes: boss path traced in source (see section above) and found covered; the
  atomicity constraint on the Victory latch and the boss-path test were added by
  that verification, not by the TD verdict.

# Capture — Terminal Save Lifecycle (clear-on-terminal)

**Date:** 2026-09-09
**System:** Save lifecycle · run termination
**Closes:** Remediation plan Defects A + C (§3.2, §3.3), merged
**Repos:** Unity (`Wasteland Run`) — production code + tests. No framework-repo code.

---

## Why this exists

`production/remediation-plan-2026-09-08.md` §3.2/§3.3 carry two player-facing
save defects. The user decided the shape on 2026-09-09: **clear-on-terminal**.
A run ends at the terminal *event* (boss death / defeat / storm engulf), not
when the end screen is dismissed. The save is invalidated there; completed runs
are never resumable.

This is not a destructive edit to authored content — no prefab, scene, SO or
designer-tuned value is touched. It crosses the capture threshold on the other
condition: a system refactor well past 50 lines touching four system-shape
carriers (`SaveSystem`, `RunSceneHost`, `RunSession`, `RunController`).

**Nothing authored is being destroyed. There are no designer values to bake.**

---

## §1 — Trace that opened the slice (§3.3, previously owed)

**Question:** does a snapshot land after a combat defeat — is this "free refight"
or "resume at full health"?

**Answer: free refight, at the exact pre-fight state.**

`RunController.cs:339-341` — `case CombatWinner.Enemy: _state.Status =
RunStatus.Defeat; return;`. No beacon resolved, no event fired, no snapshot. The
five `On*ModelCommitted` → `EnqueueRunStateSnapshot` bridges
(`RunSceneHost.cs:1237-1303`) are all resolution paths; `HandleCombatModelCommitted`
hangs off `RunSession.ResolveCombatRewards`, which defeat never reaches.

Last bytes on disk are therefore the **arrival snapshot** at that combat beacon
(`RunSceneHost.cs:987`), written before the fight began. Quitting after a loss
rolls back to beacon-unresolved, vehicle-undamaged, wallet-as-it-was.
**Permadeath is bypassable by Alt+F4.**

`RunStatus.Defeat` has **zero consumers** outside `RunController` — nothing in
the view reads it. The in-session paths are already safe: both defeat screens
route to `RunSceneHost.RestartRun` → `BeginNewRun` → snapshot, which overwrites
disk with the fresh run. The hole is only "player quits instead of clicking the
button."

---

## §2 — Five terminal fire sites, not two

The remediation plan named two. Verified by direct read, there are **five**:

| # | Cause | Model site | On disk at quit-time |
|---|---|---|---|
| 1 | Boss victory | `RunSession.cs:1013-1014` (`ResolveCombatRewards`) | boss beacon **resolved** |
| 2 | Combat defeat | `RunSession.cs:777` (`ExitCombat`, non-player winner) | pre-fight arrival |
| 3 | Engulf — advance | `RunSession.cs:470` | last beacon move |
| 4 | Engulf — stranded | `RunSession.cs:542` | last beacon move |
| 5 | Engulf — event cost | `RunSession.cs:596` (`AdvanceStormFromEvent`) | last beacon move |

Storm engulf (3/4/5) was absent from the plan entirely. User approved including
it 2026-09-09 — same defect, same root cause, and leaving it open would be a
`feedback_fix_one_sweep_all` violation.

Site 1 is **one frame earlier than `NotifyRewardClaimed`** (`RunSceneHost.cs:1075`),
which is where `OnRunComplete` fires. `MarkCombatBeaconCleared` latches
`RunStatus.Victory` (`RunController.cs:395-402`) and *then*
`OnCombatModelCommitted` fires the snapshot. Hooking the model site is better:
the snapshot the bridge just enqueued is dropped from `_pendingRunState` by the
clear before the consumer ever wakes, so the completed run never touches disk.
Cost: ~11 wasted `ToDto()` projections, once per run. Accepted over a
terminal-only branch inside `HandleCombatModelCommitted`, which would be
ADR-0011 #3 (bimodal path).

---

## §3 — H6: the slice as originally scoped CREATES a new resurrection bug

**This is the finding that reshaped the slice.** Verified end to end.

`RunSceneHost.NotifyEventResolved(BeaconOutcome outcome)` (`:1125-1132`)
**discards its `outcome` parameter entirely** and unconditionally calls
`_session.ResolveEvent()` → `OnEventModelCommitted` → `EnqueueRunStateSnapshot()`.
`BeaconOutcome.RunTerminated` (`BeaconOutcome.cs:55`) has **zero consumers**
anywhere — only its ctor, its property, and a doc reference.

`EventHandler` calls `_stormAdvancer.AdvanceStormFromEvent(...)` *before* the
terminal outcome callback. That routes to `RunSession.cs:596` — engulf site 5.
So:

> player picks an event choice with a storm cost → cursor catches them →
> the new clear fires and wipes the save → the event's terminal callback fires
> → `NotifyEventResolved` → `ResolveEvent` → `MarkResolved` +
> `EnqueueRunStateSnapshot` → **the whole run is written back to disk, with the
> event beacon now marked resolved.**

The run resurrects *and* the player skipped the encounter.

Today this path merely writes a redundant snapshot and is harmless. The moment
`ClearRunState` exists it becomes a live defect. **It is in the core slice, not
deferred.**

**Correction to the TD verdict:** the TD cited only the Ambush site
(`EventHandler.cs:346`). Verified — the identical chain exists at `:232`, `:260`
and `:319`. This is **every event choice carrying a storm cost**, not one path.
TD accepted the correction on second pass and confirmed it does **not** change
the fix: all four paths funnel through `Resolve` (`EventHandler.cs:366-373`) →
`ctx.Callback` → `EventModalHost.HandleBeaconOutcome` (`:243-256`) →
`_host.NotifyEventResolved(outcome)` (`:255`), which is the **only** call site
of that verb in the codebase (verified). The Ambush async hop happens *upstream*
of `Resolve`, so at the guard point all four shapes are identical.

### Why the guard sits at `NotifyEventResolved` and nowhere earlier

Ruled on TD second pass, with evidence rather than preference. `EventHandler` is
engine-free and *looks* like the more honest owner, but it is the wrong one — it
cannot see that a view teardown still has to run.

`HandleBeaconOutcome` does three things **before** it calls the host
(`EventModalHost.cs:243-256`, verified): clears `_resolutionInFlight`, stamps
`_lastResolvedBeacon`, and hides the dialogue controller. `_resolutionInFlight`
is the re-entry gate at `:208`. Intercept at `EventHandler.Resolve` and the
player is left staring at an un-hidden dialogue modal under the game-over
overlay, with the modal host permanently latched mid-resolution. `ctx.Resolved`
is worse — it is the double-fire defence (`:368-372`), and pre-setting it makes
`Resolve` throw rather than skip.

The correct division: **view teardown must complete, model resolution must not.**
`NotifyEventResolved` is exactly that boundary.

### `BeaconOutcome.RunTerminated` stays unconsumed

Do not consume it, and do not set it at the other three sites. The flag is
constructed *after* `AdvanceStormFromEvent` has already mutated the model, so
setting it at `:232`/`:260`/`:319` would mean each site re-reading `Status` into
a payload field — a mirrored predicate baked into a wire-adjacent struct, which
is the defect shape, not the fix. `Status` stays the single source of truth
(ADR-0011 #2).

Its honest 1.0 role is a *different* question — "this encounter's own outcome
killed the run" (lost the ambush) — which is defeat-screen/telemetry copy, not
lifecycle control. **It is always false in shipping content today**, because
Ambush is a synchronous auto-victory grant (`RunSceneHost.cs:1522-1530`).
Nothing may be inferred from it until the real AdditiveScene hop lands.

---

## §4 — The race, and why the two reviews disagreed

Two resurrection channels exist between the clear and the background write
consumer:

- **Channel A — the pending intent slot.** At boss victory,
  `EnqueueRunStateSnapshot` has already put a fresh intent in `_pendingRunState`
  (`SaveSystem.Write.cs:211`) moments before the terminal. Delete the files, and
  the consumer wakes afterwards and writes the completed run straight back.
  *Guaranteed on the boss path, not probabilistic.*
- **Channel B — the dequeued-but-unwritten intent.** `ProcessPending` (`:444-467`)
  dequeues and nulls the slot under `_pendingLock`, then calls `WriteWithRetry`
  **outside** it. A clear landing in that gap finds an empty slot, deletes the
  files, and the consumer then writes `.sav` back from the intent it already
  holds. Nulling the slot does not help.

**The disagreement.** `unity-specialist` proposed a narrow lock inside
`WriteWithRetry` around each individual `DoWrite` call, so the retry sleeps stay
outside the lock and the main thread cannot stall for the full ~7.75s budget.
`technical-director` proposed a wide lock in `ProcessPending` wrapping
dequeue + write.

**Resolved in favour of the wide lock.** The narrow lock does not close channel
B: the consumer can have dequeued the intent and not yet entered `DoWrite` when
the clear runs, and its next `DoWrite` then resurrects the file. The specialist's
stall objection is real and is answered by the TD's short-circuit rather than by
narrowing the lock.

Shape:

```csharp
private static readonly object _writeLock = new object();

ProcessPending():  lock (_writeLock) { <existing dequeue-under-_pendingLock + WriteWithRetry> }

ClearRunState():   Volatile.Write(ref _clearRequested, true);       // FIRST — before either lock
                   try {
                       lock (_writeLock) {                           // channel B (outer)
                           lock (_pendingLock) { _pendingRunState = null; }   // channel A
                           delete tmp, bak, live;
                       }
                   }
                   finally { Volatile.Write(ref _clearRequested, false); }
```

**`_pendingLock` nests INSIDE `_writeLock`, it is not taken sequentially before
it.** The sequential form the TD sketched avoids inversion (it never holds both)
but leaves a window between releasing `_pendingLock` and acquiring `_writeLock`,
and makes correctness depend on getting the two-step order exactly right
(null-then-delete, never delete-then-null). Nesting gives `ClearRunState` the
*same* acquisition order as `ProcessPending` — `_writeLock` → `_pendingLock`,
never the reverse — so no inversion exists anywhere and no ordering-dependent
window survives. Free: the null plus three deletes are microseconds.
(`unity-specialist`, second pass.)

**Put `lock (_writeLock)` inside `ProcessPending` itself**, wrapping the whole
existing body — not at each call site. Both `DrainPendingForTests` (`:248-252`)
and the consumer (`:406`) call `ProcessPending()` directly; wrapping once inside
the method is the only form a future third call site cannot forget. `DoWrite`
takes no locks of its own (verified in full), so this is the only critical
section that needs defining.

**The `_clearRequested` write must be the first statement, ahead of both
locks.** If it is set inside or after the lock acquisition, the main thread
blocks on `_writeLock` before the consumer's filter can ever observe the flag
and the mitigation does nothing. All five fire sites are main-thread, so this
stays consistent with the "caller-thread mutation only" convention at
`SaveSystem.Write.cs:39`. `ResetForTests` clears the flag alongside the existing
`_consumerFlushCount` reset (`:298`).

Lock ordering is `_writeLock → _pendingLock` in both paths (`ClearRunState`
releases `_pendingLock` before taking `_writeLock`), so no inversion. Nothing
inside `DoWrite` takes `_pendingLock`.

**Stall mitigation, in-slice:**

```csharp
catch (IOException) when (attempt < RetryDelaysMs.Length && !Volatile.Read(ref _clearRequested))
```

**The filter MUST be category-scoped.** `WriteWithRetry` is shared by both
categories — `ProcessPending` calls it for `run` then `mastery` in the same
invocation (`SaveSystem.Write.cs:456-465`, verified). An uncategorised
`!Volatile.Read(ref _clearRequested)` therefore lets a **RunState** clear
truncate a **MasteryState** retry, which is plausible whenever AV holds the
whole save directory rather than one file — and MasteryState is the ADR-0004
**blocking-policy** category. Worse, this slice's terminal events are exactly
when a mastery-XP write would also be in flight. So:

```csharp
catch (IOException) when (attempt < RetryDelaysMs.Length
    && !(intent.Category == SaveCategory.RunState && Volatile.Read(ref _clearRequested)))
```

Found by `unity-specialist` on adversarial second pass, **after** the TD's
APPROVE blessed the uncategorised form. Cheap fix; real cross-category coupling
bug as originally specified.

`_clearRequested` is `Volatile.Write`-set at the top of `ClearRunState`, cleared
in its `finally`. An exception *filter* is evaluated **before** the handler body,
so no further `Thread.Sleep` runs once a clear is pending. Worst-case main-thread
stall drops from ~7.75s to one already-started sleep (≤4s) plus one `DoWrite`.
The abandoned write throws to `RunConsumer`, which catches and raises
`SaveWriteFailed(Disk)` (`:404-416`) — correct telemetry; the write genuinely
failed and the file is about to be deleted anyway.

**Rejected: the queued delete-intent.** Both reviews independently rejected it.
`SaveSystem.Write.cs:393-395` and `RunSceneHost.cs:277-279` confirm **there is no
quit-time flush** — Alt+F4 is treated as suspend, and the background consumer is
the only path bytes take to disk. An async delete at the exact moment the player
is quitting is the same class of bug being closed, with a smaller window. The
delete must be durable before the call returns.

**Deletion order — `.tmp`, then `.bak`, then live `.sav`.** No ordering is
atomic: there is no cross-file transaction primitive on `ISaveStorage`, so a
process kill between two `Delete` syscalls always leaves some rung recoverable.
This order is the tie-break, because `LoadCategory` short-circuits at rung 1 —
while the live file is still present the chain never reaches tmp or bak, so no
intermediate state hands back a run through a rung the player would not expect.
The residual is equivalent to `DoWrite`'s own multi-step residual, which ADR-0004
Decision 3 already accepts as best-effort rather than a transaction log.

Two supporting reasons, added on TD second pass:

- **Every intermediate crash state surfaces the NEWEST surviving rung.** In
  steady state there is no `.tmp` — `DoWrite` step 6 renames it away
  (`SaveSystem.Write.cs:554`) — so the reverse order's common intermediate is
  `{.bak}` alone, handing back a *stale, earlier* snapshot. Resurrecting an
  older run is strictly more confusing than resurrecting the newest one.
- **Rung 2 is the only rung that mutates disk during a load** — it promotes the
  orphan by renaming `.tmp` over live (`SaveSystem.Load.cs:126`). Deleting
  `.tmp` first removes that mutation from every intermediate crash state.

---

## §5 — What gets built

### The clear verb

`SaveSystem.ClearRunState()` — `public static void`, no parameters, **synchronous
on the calling thread**, in `SaveSystem.Write.cs`.

- Owner is `SaveSystem` because the verb needs `_pendingLock`, `_pendingRunState`,
  `_storage` and mutual exclusion with `DoWrite`. A separate type would need all
  four handed to it — an adapter layer over a static, ADR-0011 #1.
- Category-specific **by name**, and that is load-bearing: **MasteryState must
  never be touched.** The whole point of ADR-0004's category split is that
  meta-progression outlives the run. No `SaveCategory` parameter (one legal
  argument is a stub-shaped generalisation) and no `ClearMasteryState` twin (no
  caller; unused symmetric surface is dead code).
- Goes through `_storage.Delete(...)` for all three paths. A direct
  `System.IO.File.Delete` would break under `InMemorySaveStorage` and would be a
  second parallel disk path alongside `ISaveStorage` — ADR-0011 parallel storage.
- `try/catch (IOException)` → `RaiseWriteFailed(new SaveWriteFailure(
  SaveCategory.RunState, SaveFailureStage.Disk, null, ex))` and return. RunState
  is non-blocking under ADR-0004; a locked file must not throw into the victory
  screen. Reuses `SaveFailureStage.Disk` — no new enum value; the semantic
  "the save layer could not make disk match intent" is identical.
- **No retry on delete, and that is a decision, not an omission.** `DoWrite` has
  a 5-retry budget because AV commonly holds save files open; `ClearRunState`
  deletes those same files and can hit identical contention. It still gets no
  retry: a multi-second freeze on the victory/death beat is worse than the
  failure it would be papering over, and ADR-0004 makes RunState non-blocking.
  Consequence, stated rather than hidden — **a failed delete means the
  resurrection bug survives for that run.** The load-path guard catches the
  victory case; defeat and engulf fall into the accepted residual below.
  Revisit if telemetry shows real delete failures.
- **The telemetry raise happens OUTSIDE `_writeLock`.** Capture the exception
  inside the lock, exit, then raise. Raising inside means an arbitrary
  subscriber runs while the clear holds the write lock, and a subscriber calling
  `DrainPendingForTests` would reenter (legal under `Monitor`, which is
  per-thread reentrant) and write a file mid-deletion.

**Companion refactor, in-slice:** the `(live, tmp, bak)` path triple is already
composed twice — `DoWrite` (`SaveSystem.Write.cs:492-495`) and `LoadCategory`
(`SaveSystem.Load.cs:96-99`). `ClearRunState` is the third. Extract
`private static (string live, string tmp, string bak) CategoryPaths(SaveCategory)`
and route all three through it. Three hand-composed copies of a `.bak` suffix
convention is how a clear ends up missing a rung later.

### The terminal signal

One model-side event, five fire sites, one host subscriber:

```csharp
public event System.Action<RunStatus> OnRunTerminated;   // RunSession
```

Payload is `RunStatus`, **not a new enum** — a `RunOutcome {Victory, Defeat,
StormEngulfed}` beside `RunStatus {Ongoing, Victory, Defeat}` is ADR-0011 #8,
duplicate enums, verbatim.

**`RunStatus` gains `Engulfed`.** Deliberate scope addition; it pays three times:
gives `OnRunTerminated` a payload with no new vocabulary; makes
`RunController`'s existing `Status != Ongoing` guards (`:306`, `:696`) actually
work for engulfment, which today they cannot; and gives the deferred mastery-XP
subscriber the outcome it needs without a signature change.

> **This reverses the 2026-07-06 TD Amendment A2 line preserved at
> `RunSession.cs:374-376`** — *"storm engulfment is a presentation event, not a
> terminal status."* Under clear-on-terminal that is no longer true: engulfment
> **is** terminal. Recorded here explicitly rather than left to drift.

Blast radius is contained: `RunStatus` has zero view consumers, and
`BeginNewRun`/`BeginRunFromLoaded` each construct a fresh `RunController`
(`RunSceneHost.cs:646`, `:810`) whose `StartRun` builds a `RunState` with
`Status = Ongoing` (`RunState.cs:120`) — Retry/F5 after an engulf is clean.

Adding `Engulfed` **defers** rather than makes the design call of whether storm
death counts as a defeat for mastery accounting. Folding engulf into `Defeat`
would make that call silently and irreversibly — by omission, in code, and
unrecoverably without a save migration. With `Engulfed` distinct, the future XP
subscriber must write an explicit `case Engulfed:`; it cannot silently inherit
`Defeat`'s rule.

TD second pass confirms this framing and rules it **contained enough to be a TD
decision — no game-designer sign-off is needed to implement.** But the deferral
is tracked, not implicit:

> **Open design question.** Owner: `game-designer`. Blocks the mastery-XP slice,
> **not** this one. *Does a storm-engulf loss award mastery XP identically to a
> combat defeat?*

**In scope for this slice: rewrite the `RunStatus.cs` xmldoc.** It is stale
independently of this work, on two counts, both verified:

1. *"Victory latches when the player commits the transition into the Haven
   beacon."* The actual latch is `arrived.Type == TerminalType &&
   !arrived.Type.IsCombatBeacon()` (`RunController.cs:717-721`) — Haven is one
   instance of a rule, not the rule — **and** there is a second latch the doc
   omits entirely, `MarkCombatBeaconCleared` (`:401-402`), which is the one that
   fires for the boss. Since Biome 1's terminal *is* the boss, the doc describes
   the only path that never runs.
2. *"Save/load and the Mastery system both key off the terminal value."* False on
   both halves: `RunStatus` is not persisted at all — that is Defect A's
   mechanism — and Mastery is unwired. This sentence is plausibly what seeded the
   wrong mental model in the remediation plan.

Correcting a false statement in a file the slice is already editing is in scope,
not doc churn.

`internal bool RunController.TryLatchEngulfed()` — returns false if
`Status != Ongoing`. This is hazard **H5**: victory is guarded
(`MarkCombatBeaconCleared` throws on an already-resolved beacon), defeat is
guarded (`_inFlight` + `ResolveCombat`'s status throw), but **engulfment is
completely unguarded in the model** — the only thing stopping repeat fires today
is `StormAdvanceVisualPacer._engulfed` (`:253`), a *view-side* latch. A view-side
latch as the sole stop is the pattern the storm-cursor pivot removed.
`RunSession.AutoAdvanceStrandedStorm` (`:527-532`) also gets
`if (_controller.State.Status != RunStatus.Ongoing) return;` — its current
`IsRunComplete` guard at `:532` is the positional one and does nothing for engulf.

### Host wiring

`HandleRunTerminated(RunStatus)` → `ClearRunStateForTerminal()`, the host-private
mirror of `EnqueueRunStateSnapshot` (`:1335-1353`). Its xmldoc carries the rule
that keeps this correct forever: **after a terminal, nothing may enqueue
RunState.**

Subscribed at **both** `RunSceneHost.cs:724` **and** `:871`. Missing one is the
signature bug of this slice: subscribe only in `BeginNewRun` and every *resumed*
run fails to clear. Both lists attach to a freshly-constructed `RunSession`
discarded on restart, so no `OnDestroy` detach — consistent with the existing
five, and it does not import the `OnEnable/OnDisable` pairing problem.

Do **not** reuse or near-name `RunSceneHost.OnRunEnded` (`:285`), which already
means "session torn down for restart" (**H7**).

`AdvanceToNextBeacon` (`:969-984`) — clear **instead of**, never in addition to
(**H3**), and *before* the invoke so a throwing subscriber cannot leave a
completed run alive:

```csharp
if (_controller.IsRunComplete)
{
    if (_controller.Current.Type.IsCombatBeacon())
    {
        OnBeaconChanged?.Invoke();
        EnqueueRunStateSnapshot();      // fight hasn't happened yet — legitimate
    }
    else
    {
        ClearRunStateForTerminal();     // Haven-style terminal
        OnRunComplete?.Invoke();
    }
    return;
}
```

The Haven branch is dead in shipping Biome 1 content (Haven unreachable; terminal
is Boss) but live in tests. Fixed anyway — two lines, and it is the 1.0 shape for
later biomes.

`NotifyEventResolved` gets the **H6** guard, and `RunSession.ResolveEvent` gets
the matching model-side invariant throw. Host early-return is the flow control;
the model throw catches the next caller who forgets. **Not** blanket-applied to
Rest / Merchant / Chopshop — they have no path to a mid-encounter terminal, so
guarding them is speculative. Trigger to revisit: any of them gains a storm cost.

### The load-path guard

Still needed even though the clear closes the root: the clear is synchronous
*after* `OnCombatModelCommitted` enqueued the boss-resolved snapshot. A hard
process kill (crash, power loss — not a quit) inside that window lands a
boss-resolved run on disk with nothing left to delete it. The guard is the
crash-window backstop, and it also covers already-completed saves on dev machines
from pre-fix builds.

```csharp
// NodeMap.cs, sibling to IsRunComplete (:101)
public bool IsTerminalCleared => IsRunComplete && Current.IsResolved;
```

```csharp
// LoadedRunSnapshot.cs
public NodeMap ResumeMap => _resumeMap ??= LoadedNodeMap?.ToNodeMap();
public bool CanResume => HasCompleteSessionCore && !ResumeMap.IsTerminalCleared;
```

`RunSceneHost.Initialize` (`:481`) becomes `if (snapshot.CanResume)`, and `:485`
passes `snapshot.ResumeMap`.

- **No `HasCompleteSessionCore` duplication** — `CanResume` composes it. The
  "one term, HERE and nowhere else" rule at `LoadedRunSnapshot.cs:119-127` is
  preserved; a seventh session_core member stays a one-line edit.
- **No fourth divergence.** The "map is current" triplet
  (`RunSceneOverlayHost`, `RunHUDController`, `StormAdvanceVisualPacer:269-276`)
  asks a different question. `CanResume` delegates its new half to exactly one
  declaration, on `NodeMap`, in the model, EditMode-testable.
- **The positional trap is structurally handled.** `NodeMap.IsRunComplete` is
  true on *arrival* at the boss, pre-fight (`NodeMap.cs:101`). The
  `&& Current.IsResolved` conjunct is the whole point: a crash mid-boss-fight
  leaves the beacon unresolved → `CanResume` true → legitimate resume preserved.
  **This is a required test.**

`IsTerminalCleared` is the *derivation* of `RunStatus.Victory` — of **both**
latch sites, the combat-terminal one at `RunController.cs:395-402` and the
non-combat-terminal one at `:717-721`. (The capture originally cited only the
first; a reader checking that range alone would conclude the predicate is
narrower than it is.) That is not ADR-0011 parallel storage — it is the
direct consequence of the plan's "do NOT persist RunStatus" ruling. Status is a
latched field for the *live* model; `IsTerminalCleared` is how the *load* path,
which by design has no Status on disk, asks the same question.

**Defeat and engulf deliberately get no load guard.** Neither is derivable from
the map. Accepted residuals, on the record: a defeat crash leaves the pre-fight
arrival snapshot (a refight after a genuine crash is acceptable — it is not the
Alt+F4 exploit); an engulf crash leaves a map with the storm cursor at or past
the player, which re-engulfs on the next tick. The only thing that could close
these is a persisted `RunStatus`, which the plan forbids. Honest state after a
crash beats a persisted derived field.

> **Forward note, so nobody reaches for the forbidden fix.** If telemetry ever
> shows real crashes in this window, the correct closure is an **append-only
> terminal marker in MasteryState**, never a `RunState` field. MasteryState is
> where run history and XP already have to live (see the step-5 constraint
> below), and it is the category that survives the clear by design.

### Step 5 — mastery XP: seam only, no XP code

Mastery XP is unwired end to end; `SaveSystem.Write.cs:157-158` states no
MasteryState DTOs ship yet. Writing "XP at the terminal" now means a MasteryState
DTO + adapter + registration + per-chassis mastery model + award formula — a
system, not a step in a lifecycle slice.

The plan's actual requirement, *"duplicate-award is structurally impossible,"* is
a property of the seam, and this slice delivers it in full: `OnRunTerminated`
fires from the model, not from a screen, so no screen re-entry can re-award;
`TryLatchEngulfed` + `MarkCombatBeaconCleared`'s already-resolved throw
(`RunController.cs:389-393`) + `ExitCombat`'s `_inFlight` guard mean the event
fires **at most once per run**; and the payload already carries the outcome the
award will branch on.

Explicitly forbidden in this slice: an empty `AwardMasteryXp()` (ADR-0011 #6,
stub return) and a `// TODO: award XP here` (#7, transitional comment).

Consequence worth recording: any post-boss reward the player claims is discarded
with the cleared save. That is correct under clear-on-terminal, and it means
**anything that must survive a run has to be written to MasteryState, not
RunState** — the constraint the XP slice inherits.

---

## §6 — Files and size

Production, ~155–180 lines across 8 files:

| File | Δ |
|---|---|
| `Assets/Scripts/Save/SaveSystem.Write.cs` | +45 (`ClearRunState`, `CategoryPaths`, `_writeLock`, retry short-circuit) |
| `Assets/Scripts/Save/SaveSystem.Load.cs` | ~0 net (route through `CategoryPaths`) |
| `Assets/Scripts/Run/RunStatus.cs` | +6 (`Engulfed` + doc) |
| `Assets/Scripts/Run/RunController.cs` | +15 (`TryLatchEngulfed`) |
| `Assets/Scripts/Run/RunSession.cs` | +30 (event, 5 fire sites, `ResolveEvent` guard, stranded guard) |
| `Assets/Scripts/Run/NodeMap.cs` | +8 (`IsTerminalCleared`) |
| `Assets/Scripts/CombatView/LoadedRunSnapshot.cs` | +20 (`ResumeMap`, `CanResume`) |
| `Assets/Scripts/CombatView/RunSceneHost.cs` | +30 (handler, 2 subscribes, advance branch, `NotifyEventResolved` guard, `Initialize` gate) |

Tests, ~250 lines. Minimum set — the TD will not sign off without these:

1. `ClearRunState` deletes all three rungs — assert `.bak` specifically (H1).
2. Clear drops a pending intent: enqueue → clear → `DrainPendingForTests` → file still absent (channel A).
3. Clear vs. in-flight write, exercised with the background consumer started (channel B).
4. `ClearRunState` leaves MasteryState files untouched.
5. `CanResume` false on a terminal-cleared map.
6. **`CanResume` TRUE on a map parked at the boss with the beacon unresolved** — the positional trap.
7. Engulf fires `OnRunTerminated` exactly once across repeated `AutoAdvanceStrandedStorm` calls (H5).
8. Storm-engulf-during-Event: terminal fires, `NotifyEventResolved` no-ops, disk stays empty (H6). **Prove it reds with the guard removed** or it is not evidence. — **PARTIAL, see below.**

### Test 8 — what shipped, and the gap that did not

**Model half: DONE and mutation-proven.**
`ResolveEvent_AfterStormEngulfedDuringTheEncounter_Throws` asserts both halves
of the defect — the throw, *and* that the beacon is left UNresolved (marking it
resolved is what credits the player a skipped encounter). Removing the guard
reds exactly this test and nothing else.

**Host half: NOT COVERED BY A TEST. Accepted, user decision 2026-09-10.**

`RunSceneHost.NotifyEventResolved`'s early-return and its disk consequence are
verified by inspection only. The reason is structural, not effort:

> `BiomeWebGenerator` hardcodes **`LeftFunnel` (the beacon immediately after the
> entry) as always Combat** since 2026-07-30 (`BiomeWebGenerator.cs:410-412`),
> regardless of the weighted non-terminal pool. The nearest reachable Event
> beacon is therefore **two hops** away, with a Combat beacon in between that
> must be fought and resolved. Covering this needs a full `CombatLoop` driven to
> victory inside a save-lifecycle fixture.

**Residual risk is low and worth stating precisely:** if the host guard were
deleted, the model invariant now **throws** instead of silently resurrecting the
run. The failure mode degrades from "the finished run comes back" to "an
exception at the game-over screen." That is exactly the flow-control-plus-
invariant pairing the TD specified, and the invariant is the half under test.

**A first attempt at this test was written, found to be VACUOUS, and deleted.**
It used the fixture's `BuildBiome1DistributionSO`, whose pool is Combat-only, so
the player could never stand on an Event beacon and `ResolveEvent` threw on its
beacon-TYPE check long before reaching the status check the test was named for.
It passed green in the guarded case for an entirely unrelated reason. Two
mutation passes exposed it: the first reddened on the wrong assertion, and
asking *why that assertion* rather than accepting the red is what surfaced it.

> **Trap for future tests.** `BuildBiome1DistributionSO` reads as though it
> mirrors shipping content — its comments say values "mirror the shipping
> Biome1Distribution.asset" — but its beacon pool is deliberately **Combat-only**
> for predictability, and `_guaranteedChopshopCount` is forced to 0. Any test
> assuming it can reach a Merchant, Rest, Chopshop or Event beacon through that
> fixture will pass for the wrong reason. Separately: the generated map's entry
> index is chosen by the generator — **it is not 0** — so read
> `NodeMap.CurrentIndex` rather than hardcoding.

Test-harness constraint from `unity-specialist`: EditMode tests asserting "clear
removed everything" must **bind without calling `StartBackgroundConsumer()`**,
and call `DrainPendingForTests()` before `ClearRunState()` when a prior write
must land first. Otherwise the existing `DrainPendingForTests`-races-the-consumer
trap decides the assertion by thread scheduling.

### `ResetForTests` can poison later tests — new hazard, must be fixed in-slice

Found by `unity-specialist` on adversarial second pass. Verified.

`_consumerCts.Cancel()` (`SaveSystem.Write.cs:282`) does **not** interrupt an
in-progress `Thread.Sleep(RetryDelaysMs[attempt])` (`:477`) — plain `Thread.Sleep`
does not observe cancellation tokens — and does not release a held lock.
`_consumerTask?.Wait(2000)` (`:283`) does not throw on timeout; it returns
`false`, **and that return value is never checked** (verified: the call sits
bare inside `try { } catch { }`). So if the consumer is mid-retry-storm holding
`_writeLock` when a test calls `ResetForTests()`, the join times out and the
method proceeds anyway — disposing the CTS, nulling `_storage`, clearing the
registries — **while an orphaned background thread still holds the static
`_writeLock`.**

The orphaned thread itself exits cleanly (`RunConsumer`'s `while
(!ct.IsCancellationRequested)` at `:379` fails at the next loop top), so this is
not a hang in that thread and there is no cyclic wait. The damage is **scope**:
before this slice an orphaned thread merely wrote to an isolated per-test temp
path a little longer — harmless. After it, that thread holds a static lock every
subsequent test's write, drain or clear needs. The next test's `SetUp` can block
until the *previous* test's orphan exits, surfacing as a flaky timeout in an
unrelated later test.

**This is created by the slice, not inherited.** Fix in-slice, both halves:
`ResetForTests` raises the bounded-abandon signal **before** its `Wait(2000)`
(so an abandoned retry storm gives up in ≤4s rather than running the full budget
against a 2s join), and it **checks the `Wait` return value and logs loudly** on
timeout so this cannot fail silently in CI.

**Behaviour change to expect, not a bug:** `DrainPendingForTests` →
`ProcessPending` now takes `_writeLock`, so it **serialises against the
background consumer** where previously both could be inside `ProcessPending`
simultaneously. That is strictly better — it removes a scheduling-decided
assertion — but it changes timing for any test that has both a started consumer
and a drain call. `SaveBootstrap_Test.cs:113-118` binds through the real
bootstrap, which does call `StartBackgroundConsumer`.

**Expect NOT to red:** EditMode tests that construct `RunSession` directly
without a host have no subscriber on `OnRunTerminated`, so `?.Invoke` no-ops and
no `SaveSystem` call occurs. The `RunSession_Fuel_Test` family does not need
binding and should not red on the event itself.

**Merge gate:** EditMode green with counts, Editor closed, plus
`grep -cE 'error CS' TestResults/editmode.log` (batchmode exit code lies).
Baseline **1264 / 1263 passed / 0 failed / 1 skipped**. Three most likely places
for an existing test to red: `RunStatus.Engulfed`, the `ResolveEvent` guard, and
the `_writeLock` serialisation above. "It compiles" is not "it passes."

**The merge gate is not waived by the TD's APPROVE.** Implementation may start;
the attestation is still required before merge, and test 8 must be proven to red
with the guard removed or it is not evidence.

---

## Technical Director Review

### Second pass (2026-09-09) — **APPROVE**

TD re-reviewed the resolved design in this file, including every point where the
resolution went against the TD or against `unity-specialist`. Verdict:
**TD-ARCHITECTURE: APPROVE. Implementation may proceed.** All three original
CONCERNS resolved: channel A + channel B closed by §4, H6 in the core slice per
§3, §6 carries the attestation as a *merge* gate.

Eight required capture edits were returned — all corrections and completions to
this text, none architectural, none blocking the start of code. **All eight are
applied above.** Summary of what the second pass changed or confirmed:

| Item | Outcome |
|---|---|
| H6 breadth correction (`:232`/`:260`/`:319`) | Accepted against the TD; does **not** change the fix — single call site verified |
| Guard placement at `NotifyEventResolved` | Ruled with evidence, not preference — earlier interception strands `_resolutionInFlight` |
| `RunTerminated` stays unconsumed | Confirmed; setting it at three more sites would be a mirrored predicate |
| Narrow-lock rejection (channel B) | Audited and confirmed correct |
| Exception-filter-before-sleep claim | Confirmed; ≤4s + one `DoWrite` worst case |
| Slice the sleeps further? | **No** — would duplicate the retry budget across two places |
| Reentrancy / deadlock sweep | None found; strict `_writeLock → _pendingLock` order, no cycle |
| Deletion order | Concur, plus two stronger supporting reasons (now in §4) |
| Both residuals | Signed off, with the MasteryState forward note added |
| `RunStatus.Engulfed` | TD call, no game-designer sign-off needed to implement; deferral now tracked |
| Capture misrepresentation | **None found.** One citation completed (`:717-721`) |

Three code-shape corrections the second pass produced, all folded in above:
`_clearRequested` must be written **before either lock** (the original sketch
omitted it and implementers copy sketches); the telemetry raise must happen
**outside** `_writeLock`; `ResetForTests` must clear `_clearRequested`.

### First pass — **CONCERNS**

Verdict: **CONCERNS** — direction approved, APPROVE withheld pending three
things, all fixable inside the slice: (1) the H2 mitigation as originally briefed
left **two** resurrection channels open, not one; (2) the slice as scoped
**created a new resurrection bug** on the storm-engulf-during-Event path;
(3) no EditMode-green attestation exists yet and this touches the save
orchestrator's threading model.

All three are addressed above: channel A + channel B are both closed by the
`_writeLock` + slot-null shape in §4; H6 is in the core slice per §3; the merge
gate in §6 is the attestation.

**TD corrections to the pre-review trace, all independently verified:**

- Five model-side terminal fire sites, not three — `RunSession.cs:596`
  (`AdvanceStormFromEvent`) was missed. Confirmed.
- Boss victory's true model terminal is `RunSession.cs:1013-1014`, one frame
  earlier than `NotifyRewardClaimed`. Confirmed.
- `RunSceneHost.OnRunEnded` (`:285`) already exists with a different meaning —
  do not near-name it. Confirmed.
- `HasCompleteSessionCore` has six members, not five. Confirmed.

**TD three-lens self-audit** (health / optimization / 1.0 survival) was returned
with the verdict. Health: no bridge, parallel storage, compat overload or
vestigial surface introduced; the third `(live, tmp, bak)` composition is
extracted rather than added to; a symmetric `ClearMasteryState` and a blanket
five-verb `RequireRunOngoing` were both refused for having zero and one real
callers. Optimization: every new signal is once-per-run, nothing per-frame; the
~11 wasted `ToDto()` projections on the boss path are named as an accepted trade
rather than hidden; the retry-budget-inside-the-lock stall is mitigated in-slice,
not deferred. 1.0 survival: `OnRunTerminated(RunStatus)` is the canonical seam
mastery XP, run-history telemetry, achievements and the victory-stats panel all
consume without a signature change — the seam ships, zero of its subscribers do.

**Reviewer disagreement, resolved against the TD's own framing where the
specialist was right and vice versa** — see §4. The wide lock is taken (TD) with
the stall objection answered by the short-circuit rather than by narrowing (the
specialist's narrow lock does not close channel B). The deletion order is the
specialist's (TD did not specify one).

**Corrections applied to the TD verdict itself:** H6 is not confined to the
Ambush site at `EventHandler.cs:346` — the identical chain exists at `:232`,
`:260` and `:319`, i.e. every event choice carrying a storm cost. Verified by
direct read.

---

---

## Unity Specialist Review (adversarial second pass)

Asked to refute the wide-lock resolution, since it overrode their own
recommendation.

**Channel B: conceded fully, without qualification.** Their own words — the
narrow lock "serialized the *disk operations* but not the *commitment point*."
The commitment happens at the dequeue (`SaveSystem.Write.cs:449`), the `lock`
block closes at `:451`, and `WriteWithRetry(run)` is not called until `:459`.
Between those, the consumer holds the intent as an uncontested local under **no
lock at all**. The wide lock stands.

**Filter semantics: confirmed**, with the bound tightened — a failing
`FileStream` construction throws immediately rather than blocking, so the
worst case is ≈4s + O(ms), not 4s + a slow write. One pre-existing exposure
named and explicitly *not* attributed to this slice: `OpenWrite` has no
independent timeout, so a wedged network/OneDrive path breaks the bound under
any lock design.

**Three findings that survived the TD's APPROVE** — all verified against the
repo before being written down here, all now folded into §4/§5/§6 above:

1. **Cross-category coupling in the retry filter** (`WriteWithRetry` is shared;
   a RunState clear would truncate a MasteryState retry — the blocking-policy
   category).
2. **`ResetForTests` orphan holding the static `_writeLock`**, poisoning later
   tests. Created by this slice, not inherited.
3. **`_pendingLock` must nest inside `_writeLock`**, not precede it, to remove
   an ordering-dependent window and match `ProcessPending`'s acquisition order.

**They also corrected their own earlier answer.** Their first pass implicitly
assumed Victory/Defeat gave the load guard a persisted backstop. They grepped
and retracted it: **zero `RunStatus` hits anywhere in `Assets/Scripts/Save`**
(independently verified — zero). `RunStatus` is not serialized for *any* of its
values; even the existing `Defeat` latch has never once reached disk. This
confirms rather than changes the residual framing above: the load guard relies
100% on map-derived state, by design.

**One hypothetical they raised and then ruled out by checking:** adding an enum
member risks nothing via exhaustiveness, because there is **no `switch` over
`RunStatus` anywhere** in the codebase — only direct `== RunStatus.Ongoing`
comparisons and assignments in `RunController.cs` / `RunState.cs`.

**Open engineering question they left on the table, deliberately not closed
here:** whether `RunStatus` should be persisted *at all* purely as a
crash-window backstop — a new DTO field plus a `SchemaVersion` bump under
ADR-0004. This slice's answer is **no**, on the plan's standing "do NOT persist
RunStatus" ruling and because clear-on-terminal's premise is "don't write a
doomed value, delete it." Recorded so the question is visibly answered rather
than overlooked.

---

## Approval

- [x] **User approval to implement — granted 2026-09-09.**

Agreed implementation shape: two stages with an EditMode checkpoint between
them, so the save-layer threading change is proven green before the model/host
changes land on top of it.

- **Stage 1 — save layer, self-contained.** `CategoryPaths`, `_writeLock` inside
  `ProcessPending`, category-scoped retry filter, `_clearRequested` +
  `ResetForTests` hardening, `ClearRunState`. Tests 1–4. Nothing outside
  `Assets/Scripts/Save` depends on it.
- **Stage 2 — model + host.** `RunStatus.Engulfed` + xmldoc rewrite,
  `TryLatchEngulfed`, `OnRunTerminated` + 5 fire sites, `ResolveEvent` +
  `NotifyEventResolved` guards, `IsTerminalCleared`, `ResumeMap`/`CanResume`,
  host handler + both subscribes, `AdvanceToNextBeacon` branch. Tests 5–8.

One commit per stage, so the threading change is bisectable alone. Test 8's
regression proof (revert the guard, confirm red, restore) runs after Stage 2.

### Stage 1 result — GREEN

**EditMode 1272 / 1271 passed / 0 failed / 1 skipped**, Editor closed,
`grep -cE 'error CS' TestResults/editmode.log` = **0**. Baseline was
1264/1263/0/1, so +8 tests and +8 passing — exactly the 8 new cases in
`SaveSystem_ClearRunState_test.cs`, no existing test disturbed. The
`_writeLock` serialisation did **not** red `SaveBootstrap_Test`, which was the
predicted risk.

Shipped in Stage 1: `_writeLock` (wrapping all of `ProcessPending`, placed
inside the method), `_clearRequested` with the category-scoped retry filter,
`CategoryPaths` (now the single declaration, routed from `DoWrite` **and**
`LoadCategory`), `ClearRunState`, and the `ResetForTests` abandon-signal +
join-timeout report.

**Three implementation notes worth keeping:**

- `UnityEngine.Debug` is **not available in this assembly** — `WastelandRun.Save`
  is `noEngineReferences: true`. The orphan-join report uses
  `Console.Error.WriteLine`, which Unity redirects into the same `editmode.log`
  the CI gate greps.
- The first version of the channel-B test was **vacuous**: it waited *until* the
  file was absent and then asserted absence. The file is absent the instant the
  loop ends, so the wait returned immediately and never gave an in-flight write
  its window. It now settles for a fixed period and *then* asserts.
- A duplicate `StubMasterySerializable` broke the first compile — one already
  exists in `Fixtures/EnvelopeFactory.cs`. `grep -rn "..." *.cs` does not
  recurse when a glob is supplied, which is why the first search missed it.

**Mutation-tested, not just green.** Commenting out
`lock (_pendingLock) { _pendingRunState = null; }` and re-running the file
produced **exactly one failure** — `ClearRunState_DropsPendingIntent_
SoALaterDrainCannotRewriteIt` — with the other seven still passing. The
channel-A test is non-vacuous and precisely targeted. Mutation reverted.

**Correction to a claim made earlier in this session.** The compile-failure run
was reported as "exit code 0" and called an instance of the batchmode-lies
trap. That was wrong: Unity exited **1** and `run-tests.ps1` correctly printed
`FAIL -- no results XML written`. The 0 came from piping the script through
`tail`, so the observed status was `tail`'s. Exit codes read through a pipe are
the pipe's — check `$LASTEXITCODE` or the script's own output instead. The
`error CS` grep stays regardless; the recorded trap is real, this run just
wasn't an instance of it.

### Stage 2 result — GREEN

**EditMode 1281 / 1280 passed / 0 failed / 1 skipped**, Editor closed,
`grep -cE 'error CS'` = **0**. Stage 1 was 1272/1271/0/1, so +9 — exactly the
nine cases in `RunSession_TerminalLifecycle_Test.cs`. Neither predicted risk
materialised: `RunStatus.Engulfed` and the `ResolveEvent` invariant reddened
nothing.

**Regression proof (model half):** removing the `ResolveEvent` status throw reds
`ResolveEvent_AfterStormEngulfedDuringTheEncounter_Throws` and only that test.
Restored.

**Fixture assumptions that were wrong and were caught by precondition asserts,
not by the real assertion silently passing** — four in this slice:

1. `.bak` only exists after a SECOND write; a single write cannot exercise rung 3.
2. `CommitNextBeacon` auto-resolves NON-combat terminals on arrival, so a
   Haven-terminal map can never sit "arrived but unresolved" — only a **Boss**
   terminal produces the positional-trap state.
3. `AdvanceStormFromEvent(int)` takes a **counter charge**, not strips; charging
   less than `CounterStart` moves the counter but not the cursor.
4. The generated map's **entry index is not 0**, and `LeftFunnel` is always
   Combat regardless of the weighted pool.

Writing the precondition assert is what converted each of these from a
false-green into a visible failure. None would have been caught by the primary
assertion.

**Tooling bug found and fixed in passing** (`tools/ci/run-tests.ps1`, failure
reporter): `($a -or $b -or '')`.Trim()` — `-or` is a boolean operator in
PowerShell, so the expression collapsed to `$true` and `.Trim()` threw
`[System.Boolean] does not contain a method named 'Trim'`. It crashed the
reporter on the only path that ever runs it: the one where a test has failed.
Latent since the script was written because the suite has been green. Fixed
with an explicit fallback chain — needed before Stage 2's test-8 regression
proof, which depends on reading failure output.

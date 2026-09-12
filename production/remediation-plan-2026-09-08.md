# Remediation Plan — 2026-09-08

Clean-slate sweep before resuming feature work. Every claim below was verified
by direct code read this session; agent findings that did not survive
verification are marked **CORRECTED**.

This is the ordered work list.

## Progress

| Item | State |
|---|---|
| **0.1 CI gap** | **DONE** 2026-09-09 — Unity `2ecbf62` (gates + workflow), `d55365d` (ADR-0016 containment gate) |
| **0.2 ADR-0016** | **DONE** 2026-09-09 — framework `4db7a0e`; TD RESHAPE applied, authoring rule cut |
| **0.3 ADR status hygiene** | **DONE** 2026-09-09 — 0005 + 0006 Superseded, 0008 flagged NOT INSTALLED, 0017 contradiction resolved, 0001 marked partial |
| **0.4 Dredge contradiction** | **DONE** 2026-09-09 — resolved in favour of permanent; stale test deleted |
| **1.1 Prefab drift** | **DONE** 2026-09-10 — investigated and **cleared as a FALSE POSITIVE**. Nothing baked because nothing drifted. Capture: `production/polish-captures/2026-09-10-prefab-drift-sentinel-cleared.md`. **Phase 4.1 unblocked** |
| **2.1 Bind harnesses** | **DONE** 2026-09-09 — Unity `a1689f9`; scope was 2 files, not 3 |
| **2.2 Controller tests** | **PARTIAL** 2026-09-09 — Unity `464704b`; RunHUDController covered, RunSceneOverlayHost deferred |
| **3.1 Projection wrap** | **DONE** 2026-09-09 — Unity `cdf03a3` (Defect B, both halves) |
| **3.2 + 3.3 Defects A + C** | **DONE** 2026-09-10 — Unity `80b05ac` (ClearRunState + threading), `56b7338` (clear-on-terminal wiring). Merged into ONE slice under the user's clear-on-terminal ruling; ~40% of §3.2 as written became unnecessary |
| **3.3 trace (owed)** | **DONE** — defeat writes NOTHING; last bytes are the pre-fight arrival snapshot, so a loss was a free refight. Permadeath was Alt+F4-bypassable |
| **4.2 Pure deletions** | **DONE** 2026-09-10 — Unity `f832359`, net −164 lines. Plan text was wrong in 5 places; corrected in §4.2 with the original preserved |
| **4.1 Beacon cleanup** | **DONE** 2026-09-11 — Unity `ca59184` (A: author + prose + roster), `0a4b450` (B: 4 scenes + 8 files + 4 build entries), `a8b769d` (C: A2 grep gates + A3 roster test). All three replacement acceptance criteria PASS. EditMode 1285/1284/0/1 (**new baseline**, +4 from A3), PlayMode 16/16 |
| **5.1 BuildLegacy cluster** | **DONE** 2026-09-11 — Unity `72ec787` (A) + `93b56ed` (D) + `4f2e873` (B) + `b18326b` (C), net −724 lines. **7 shapes, not 3**; the real justification is ADR-0011 **#2 parallel storage**, not #3. EditMode **1287**/1286/0/1 (**new baseline**, +2 from the wiring test), PlayMode 16/16. **Playtest PASSED 2026-09-12 — no regression**, proven by diff; it surfaced two *pre-existing* defects with a shared root cause, fixed in the same session (see **§5.1a**). Spun out new **§5.4** (`BuildScout` — relocate, not delete) |
| Phases 5.2–6 | not started. **Phase 4 is now CLOSED.** **Phase 5 gained two items from 4.2** — `AdvanceReason` + `BeaconTransition.Reason` (4-layer signature change), and `BeaconTravelTick`'s 10-arg positional ctor. **Phase 4.1 adds one:** `Assets/Scenes/CombatScene.unity` is a separate orphan — referenced only by a comment at `BeaconActivator.cs:84`, still in build settings, deliberately not bundled into 4.1 |

### Phase 4.1 outcome (2026-09-11)

Ran as specified. Nothing in the plan text needed correcting this time — the
2026-09-10 verdict had already absorbed every correction. Notes worth keeping:

- **The replacement acceptance criteria are the right ones and all three
  passed.** After Commit B, one `Author All Scenes` left `Assets/Scenes/Beacons/`
  holding only `Combat.unity` — no `Haven.unity`, tracked or untracked.
  `EditorBuildSettings.asset` and `BeaconSceneBinding.asset` both came back with
  an EMPTY diff, which is stronger than the "still 3 entries" the criteria asked
  for.
- **The pre-existing idempotence churn reproduced exactly**, minus the deleted
  file: 4 files (`ChopshopRoot.prefab`, `EventRoot.prefab`, `Beacons/Combat.unity`,
  `RunScene.unity`) where the 2026-09-10 experiment saw 5. The missing one is
  `Haven.unity`. Precise match — nothing new was introduced. Churn reverted; it
  is not part of any 4.1 commit.
- **A3 grew from 2 assertions to 4.** The verdict asked for the roster pairs and
  the Haven throw. Two more earn their place: Mode↔ScenePath consistency (this
  is what catches Commit B landing without Commit A — a roster entry aimed at a
  deleted scene), and non-terminal emitter coverage across every shipping
  `BiomeDistributionSO`. The latter encodes the verdict's own correction:
  terminals are EXCLUDED, because a terminal latches its status before the
  activator ever asks the SO to resolve.
- **Every new gate proven to red on its bug**, not assumed: Haven re-added → 3
  of 4 tests red; Rest entry removed → the 4th red; `AuthorBeaconStubScene` +
  a `STUB-<Type>-DESIGN-ME` literal re-added → both grep gates fired, exit 1.
- **EditMode baseline moves to 1285/1284/0/1.** The 1281 figure is now stale.
  The single skip is still `StormPacingTuner_Test` `[Explicit]`.

### Phase 3 corrections to this plan

**Two terminals became five.** The plan named boss-victory and combat-defeat.
Storm engulfment is the same defect at three more model sites — `RunSession.cs`
Advance, `AutoAdvanceStrandedStorm`, and `AdvanceStormFromEvent`. The last of
these was missed by the first TD pass too and found on verification.

**The boss terminal is not boss death.** A reward picker runs between the boss
dying and `OnRunComplete`; the true model terminal is `ResolveCombatRewards`,
one frame earlier than `NotifyRewardClaimed`.

**The slice would have CREATED a resurrection bug** if scoped as written — see
the capture's §3 (H6). `NotifyEventResolved` discards its `BeaconOutcome` and
re-snapshots unconditionally, so a storm-cost event choice that engulfs the
player mid-encounter would have written the finished run straight back to disk.
Harmless before `ClearRunState` existed; live defect the moment it did.

**"Do NOT persist RunStatus" held, and cost nothing.** The load path derives the
same answer from the map via `NodeMap.IsTerminalCleared`. Defeat and engulfment
are not map-derivable and deliberately get no load guard — accepted residual,
documented in the capture.

**§3.4's merge gate is met except for one named gap** (host half of the H6
test), accepted by the user 2026-09-10 with the reasoning recorded in the
capture. Do not read the gate as fully satisfied.

### Phase 2 corrections to this plan

**2.1 was two files, not three.** Only `RunSceneHost_Test` and
`RunSceneHost_SeedRetry_Test` instantiate the host unbound — established by
checking which tests actually `AddComponent<RunSceneHost>`, not by grepping the
class name. `PartRewardPicker_Test` was a hedged guess here ("and probably");
it has **zero** references to `SaveSystem` or the host and its asmdef does not
reference `WastelandRun.Save`, so it cannot be relying on the swallow.

**Bind through `SaveBootstrap.Bind`, not `SaveSystem.Bind`.** The plan said the
latter. `SaveBootstrap` is what `RunSceneHost_Resume_Test` already uses, and it
registers all **11** run-state serializables — which is the actual upgrade the
plan wanted. Binding `SaveSystem` directly would require duplicating that
11-entry registration list in test code, which drifts the day a 12th DTO lands.

**The inactive-GameObject pattern is mandatory.** `SaveBootstrap.Awake()` binds a
`DiskSaveStorage` at `Application.persistentDataPath` — on a live GameObject that
writes real save files from a unit test.

**2.2's coverage claim was wrong.** The plan says `RunSceneOverlayHost`,
`RunHUDController` and `BeaconActivator` have zero controller tests.
`BeaconActivator` is instantiated by two PlayMode tests
(`ChopshopBeaconRevisit_Test`, `ChopshopRepairMode_Test`). Only
`RunHUDController` and `RunSceneOverlayHost` were genuinely uncovered.

**`RunSceneOverlayHost` is still uncovered.** Its duplicated predicate sits at
`:126-130`. It is deferred because the predicate is only observable through a
live `MapViewController` (`Show`/`Hide`/`RunOverlayEvents.RaiseOverlayShown`),
and `MapViewController.Bind` warns-and-returns unless `_beaconsLayer` and
`_connectionsLayer` are populated — a materially larger fixture than
`RunHUDController`'s flat element list. Phase 3 refactors this call site anyway
and its merge gate specifies its own tests, so the coverage lands there.

### Phase 3 prerequisite discovered during 2.1

`RunSceneHost_EnqueuesWrite_Test.BeginNewRun_Without_Bound_Storage_Logs_Warning_Does_Not_Throw`
**explicitly asserts the swallow exists**, justifying itself in-comment by
"harnesses that don't run SaveBootstrap" — a premise 2.1 dissolved. Phase 3 must
replace it with the inverted assertion already specified in §3.4 item 4
(`Snapshot_WhenSaveSystemUnbound_Throws`). Deleting the catch without touching
this test turns it red.

Also confirmed for §3.1: the **projection half is synchronous**.
`SaveSystem.EnqueueRunStateWrite` calls `SnapshotRunRegistry()` (all 11 `ToDto`
calls) inline and only queues the byte-write for the background consumer. So
wrapping `SnapshotRunRegistry()` as §3.1 proposes does catch the projection
failures, and `DrainPendingForTests()` **races** the consumer that
`SaveBootstrap.Bind` starts — whichever side dequeues first wins. Tests that need
the file must wait on the file, not drain.

**Suite: 1263 / 1262 / 0 failed / 1 skipped** after Phase 2.

**Pushed** through 2026-09-09 (Unity `d55365d`, framework `1e083c1`). CI workflow
is live server-side. `UNITY_LICENSE` is unset, so the EditMode job skips with a
warning annotation — grep gates are the only server-side enforcement so far.

### 0.4 resolution — permanent, not temporary

Git history settled it. The `[Ignore]` was added **2026-06-05** (`47e8c92`)
citing a `TEMP` marker; that marker was removed and the comment rewritten to
*"Held at 0 — the boss reads through exposable pressure + hull damage; the frame
armor pool is not the pacing lever on this fight"* on **2026-07-05** (`43798a1`),
a month later. The test's stated reason points at something deleted after it was
written. `Dredge.FrameArmorHp = 0` is settled design.

`ApplyDamage_HullTarget_DrainsArmor0First` therefore encoded an abandoned design
and was **deleted**, not restored. Zero unique coverage lost —
`IronShepherdLayoutTests:75` and `SmallFrameLayoutTests:100` both cover
armor-drains-first live, plus 11 `AbsorbedByArmor` assertions in
`DamagePipeline_R_ARM_Tests.cs`.

**Verified by running the suite, not predicted: 1254 total / 1253 passed / 0
failed / 1 skipped** (80.6s wall, `TestResults/editmode.xml`). Skips went 2 → 1
exactly as the deletion implied; the remaining one is `StormPacingTuner_Test`'s
intentional `[Explicit]` balance sweep.

**The recorded baseline of 1244 was stale** — the suite has grown by 11 tests
since 2026-08-30 (`GarageBinding_Test.cs` among them, added in `38b2b45`). Any
future "suite at baseline" gate should use **1254 / 1253 / 0 / 1**.

Deletion confirmed against the result XML rather than assumed: the surviving
`ApplyDamage_HullTarget_DrainsArmor0First` is class-qualified to
`SmallFrameLayoutTests`, not `DredgeLayoutTests`, which kept its other 19 cases.

**Found during 0.1–0.4, not yet fixed:**

- `validate-commit.sh:163-175` picks Python via `command -v`, which succeeds on
  the Windows Store alias stub while execution fails — so the JSON check would
  **false-block** any commit touching `assets/data/*.json`. Same family as the
  `jq` finding. Dormant (no such files today), one-line fix.
- **4 of 4** post-2026-05-31 ADRs lack the titled "ADR-0011 compliance"
  paragraph that `adr-0011:108` mandates and `:219` lists as a validation
  criterion. Corrected from an earlier "3 of 4" — ADR-0014's single grep hit is
  an **inline phrase inside an Alternatives bullet** (`:296`), not the mandated
  Decision-section paragraph. 0013, 0015 and 0017 have nothing. ADR-0016 ships
  with a correct one. **Deliberately left unfixed:** writing four retroactive
  compliance paragraphs is exactly the doc churn the project prefers to avoid,
  and none of the four ADRs is actually non-compliant in substance — only
  undocumented. Do it if `/architecture-review` starts failing on it.

---

## Severity summary

| # | Item | Severity | Risk to fix |
|---|---|---|---|
| A | Resume-after-completed-run presents a live map | **Player-facing save loss** | Medium |
| B | Serializer failure silently skips the save | **Silent save loss** | Low |
| C | No save-invalidation API — defeat resurrects the run | **Player-facing** | Unknown (untraced) |
| D | Prefab drift sentinel blocks all re-authoring | Blocks other work | Low |
| E | CI gates exist but only fire on Unity-repo commits | False assurance | None (additive) |
| F | ADR doc drift — 0016 phantom, 0005/0006 stale, 0017 self-contradictory | Misleads future work | None (docs) |
| G | Dead beacon scenes + vestigial code | Cruft | Low |
| H | `BuildLegacy` cluster — ADR-0011 violated in 11 files | Structural debt | Medium |
| I | ADR-0014 P4/P5 unfinished — the 9k author problem | Structural debt | High |
| J | Chopshop steps 5–6 | Feature work | Medium |

---

## Phase 0 — Zero runtime risk. Do these first.

Nothing in Phase 0 can break the game. All of it makes later phases verifiable.

### 0.1 — Close the CI gap

**CORRECTED from the audit.** `tools/ci/grep-gates.sh` (189 lines) and
`tools/ci/run-tests.ps1` both exist **in the Unity repo**, and
`.claude/hooks/validate-commit.sh:119-121` does invoke the gate script,
resolving `REPO_ROOT` to the repo being committed. That hook was added
2026-08-27 specifically to fix the "unrun gates" problem.

The real gaps:

- The **framework repo has no `tools/ci/`**, so every framework-repo commit
  prints `[gates] no tools/ci/grep-gates.sh … grep gates NOT run` and passes.
- There is **no GitHub Actions workflow in either repo** — verified, no
  `.github/workflows` anywhere. Enforcement is local-commit-hook only. A clone,
  a CI runner, or a commit made outside the hook has no gate.
- Three ADR-claimed gates are **absent from `grep-gates.sh`**:
  - ADR-0003 nondeterminism tokens (`UnityEngine.Random`, `DateTime.Now`,
    `Guid.NewGuid`, `Random.Shared`, `Time.time`)
  - ADR-0010 Phase 5 (`LegacySlotKind`, `LegacyKindBridge`, `IsLegacyMode`)
  - ADR-0013 salt uniqueness
  - ADR-0014 P5 Canvas-outside-Popups — **leave this one out until P4 lands**;
    the Canvases it forbids are still legitimately in use.

ADR-0004's uniqueness check is real but is an EditMode test
(`SchemaRegistry_Unique_test.cs`), not a gate — fine, it just needs CI to run it.

**Work:** add the three gate lines; add a GitHub Actions workflow calling
`grep-gates.sh` + `run-tests.ps1`. Highest value-per-hour item on this list —
it converts work already done into actual enforcement.

### 0.2 — Write ADR-0016

Does not exist. `git log --all --diff-filter=A` shows it was never added. Yet
it is cited **5 times inside Accepted ADR-0017**, including its `Depends On`
row, and ~10 production docs reference it.

Subject: *"prefabs are organized by conceptual category and edit cadence, not
by dependency convenience."* A complete four-section outline already exists at
`~/.claude/projects/.../memory/project_adr_0016_scope_expansion.md`.

**Urgency:** `production/td-verdicts/2026-07-29-whole-game-health-opt-audit.md:262`
proposes recycling number 0016 for an unrelated "Derived-State Caching Pattern."
Claim the number before that collides.

### 0.3 — ADR status hygiene

- **ADR-0005** (`IPartCatalog`, `IVehicleView`, `IVehicleMutator`,
  `ChassisDefinitionSO`): zero hits each. Functionally superseded by
  0007→0010→0012 but never marked. → **Mark Superseded. Do not implement.**
- **ADR-0006**: 11 of 12 named symbols absent; no `WastelandRun.Cards` asmdef.
  What shipped instead — the `CardEffect` POCO hierarchy plus ADR-0013's
  `ICardRewardSource` — is a *better* shape. → **Mark Superseded.**
- **ADR-0017**: header says `Accepted (2026-07-05)`, line 52 says it flips to
  Accepted only when the ADR-0012 amendment is Accepted. That amendment **does**
  exist (`adr-0012…md:275-374`) and A2 is executed in code
  (`Vehicle.cs:418-419`, `armorContribution` has no default). So Accepted is
  defensible — **the doc just contradicts itself and needs a cleanup.**
- **ADR-0008 Addressables**: package **not installed** (verified, zero hits in
  `Packages/manifest.json`), zero usage, `scriptingBackend` empty → still Mono,
  IL2CPP smoke test never done. Project loads via `Assets/Resources/`.
  → **Re-label as deferred-future so nobody plans against it.**
- **ADR-0001**: data half landed (`DamageState`, `ApplyDamage(slotId,…)`);
  visual half unbuilt (zero `MaterialPropertyBlock`, no project `.shadergraph`).
  → Note as partial; gates vehicle art, not the current axis.

### 0.4 — Resolve the Dredge contradiction

`DredgeLayoutTests.cs:86` is `[Ignore]`d citing *"FrameArmorHp temporarily 0 for
playtest (Dredge.cs:91-94 TEMP)"*. But `Dredge.cs:89-91` now reads *"Held at 0 —
the boss reads through exposable pressure + hull damage"*, i.e. permanent, and
there is no `TEMP` marker anywhere in the file. One of the two is lying about
design intent. Decide, then either delete the test or restore it.

---

## Phase 1 — Unblock authoring

### 1.1 — Bake the prefab drift

Sentinel active since 2026-08-30 on **Combat, PlayerVehicle, MainBar,
HudAnchors**. Until cleared, any re-author destroys designer edits.

**This is a hard prerequisite for Phase 4's acceptance test**, which requires
running `Author All Scenes` twice and diffing. Do not sequence the beacon
cleanup before this.

Protocol: read each prefab YAML, diff against what `CombatPrefabAuthor.cs`
generates, bake every divergence, write a capture, get approval, then clear
the sentinel.

---

## Phase 2 — Test infrastructure

Build this **before** touching save behaviour, so the fix's green light means
something. The project has already been burned once by tests that passed while
the on-disk record was wrong.

**The good news, verified:** infrastructure is in better shape than the backlog
claims. A PlayMode asmdef and 5 PlayMode tests exist (incl.
`PostCombatRewardChainAuthoring_Test.cs`); `RunSceneHost_Resume_Test.cs` is 587
lines and already plants fixture envelopes on `InMemorySaveStorage`; the Save
suite pairs `_round_trip` + `_wire_format` per DTO, i.e. it already asserts the
serialized payload. This is extension, not greenfield.

**TD ruling: the debug/warp tooling slice does NOT need to land first.**
Blocking a save-integrity fix on developer tooling inverts the priority — the
defect ships to players, the tooling ships to us. Build tooling later, when it
helps *find* the next defect of this class.

### 2.1 — Bind the three unbound harnesses

`RunSceneHost_Test.cs`, `RunSceneHost_SeedRetry_Test.cs`, and probably
`EditMode/UI/PartRewardPicker_Test.cs` rely on the exception swallow that
Phase 3 deletes. Add `SaveSystem.Bind(new InMemorySaveStorage())` in SetUp +
`ResetForTests()` in TearDown.

Free upgrade: those three go from "never touch the serializer" to "project all
11 DTOs on every host transition" — direct coverage against the
`VehicleStateSerializable.ToDto` invariant class.

### 2.2 — Controller tests for the resume path

9 of 11 view controllers have **zero** test files. The three that matter here
are exactly the resume-path trio: `RunSceneOverlayHost`, `RunHUDController`,
`BeaconActivator`. Start with these; the other six are lower priority.

---

## Phase 3 — Save integrity

Ship **B then A inside one slice** — same file, same funnel, same failure
domain. B must land first *within* the commit, because binding the harnesses is
what makes A's tests actually project the DTOs.

### 3.1 — Defect B: the silent swallow

`RunSceneHost.EnqueueRunStateSnapshot()` (`:1317`) catches
`InvalidOperationException` and reports it as *"SaveSystem not bound"* — but
`VehicleStateSerializable.ToDto` throws that same type for its own invariants.
A real serializer failure is mislabelled and **the write silently does not
happen**.

**Do NOT use `SaveSystem.IsBound`** (it exists at `SaveSystem.Write.cs:93`) —
its own doc comment says it is for prefab-validation tests and explicitly *not*
for runtime branching.

Three-part fix:
1. Delete the `catch` from the funnel entirely. Bind the three harnesses (2.1).
2. In `SaveSystem.EnqueueRunStateWrite`, wrap only `SnapshotRunRegistry()` and
   rethrow a save-owned `SaveProjectionException` carrying `SystemId` + inner.
   `EnsureBound()` keeps throwing plain `InvalidOperationException`. **The two
   conditions become distinguishable by type, not by message string.**
3. Funnel catches `SaveProjectionException` only → `LogError` + raise a
   `SaveWriteFailed` telemetry event. Run continues (ADR-0004 non-blocking).

`SaveSystem` already has this exact telemetry pattern (`SaveTempCorrupted`,
`SaveBackupRecoveryRotationWindow`, cleared in `ResetForTests`).

Payload shape must be `readonly struct SaveWriteFailure { SaveCategory Category;
string SystemId; Exception Inner; }` — **not** `Action<string>`. MasteryState
hooks the same seam under ADR-0004's *blocking-dialog* policy and needs
`Category` to branch.

> ### ⛔ 3.2 AS WRITTEN BELOW IS WRONG — see TD verdict 2026-09-09
>
> `production/td-verdicts/2026-09-09-save-integrity-phase-3.md` returned AMEND.
> The blocking finding, verified before acting:
>
> **`NodeMap.IsRunComplete` is POSITIONAL** —
> `_beacons[CurrentIndex].Type == TerminalType` (`NodeMap.cs:101`). It is true the
> instant the cursor *lands on* the boss beacon, **before the fight**.
> `AdvanceToNextBeacon:969-983` already knows this and fires only
> `OnBeaconChanged` for a combat terminal, commenting *"OnRunComplete deferred to
> NotifyRewardClaimed."*
>
> So `if (_controller.IsRunComplete) OnRunComplete?.Invoke();` — the fix written
> below — **shows the victory screen for a boss the player never fought**, and
> awards XP for it once the stats panel lands. Strictly worse than Defect A.
>
> Correct predicate is **terminal AND resolved**, which equals `RunStatus.Victory`
> because the latch rides atomically with `MarkResolved` on both terminal shapes
> (`RunController.cs:395-403` and `:716-720`), and `is_resolved` **is** persisted
> (`NodeMapDto.cs:249-250`) — so it reconstructs without persisting `RunStatus`.
> Add `IsRunOver => IsRunComplete && Current.IsResolved`; keep `IsRunComplete` for
> the positional callers that legitimately want it.
>
> Two more blocking amendments: the fan-out may fire into an **empty delegate**
> (`RunSceneOverlayHost` subscribes in `OnEnable`, `SaveBootstrap` is
> `[DefaultExecutionOrder(-100)]`) — probe before writing; and there are **FOUR**
> copies of the map-current predicate, not three — the fourth is
> `StormAdvanceVisualPacer.cs:269-276`.
>
> **3.2 is HELD pending a user decision** — see "The Shape fork" below.

### 3.2 — Defect A: resume-after-completion

Verified chain: boss dies → snapshot written with cursor on resolved terminal →
player quits at victory screen → `BeginRunFromLoaded` fires `OnBeaconChanged`
unconditionally (zero `IsRunComplete` refs in the method) →
`RunSceneOverlayHost.HandleBeaconChanged` sees `IsResolved` → **shows the map**
and then unconditionally **hides `RunCompleteView`** → `RunStatus` is not
persisted so it resets to `Ongoing` → player clicks a beacon →
`AdvanceToNextBeacon` throws.

**TD correction to the obvious fix.** Do *not* mirror `AdvanceToNextBeacon`'s
early `return`. That path can skip `OnBeaconChanged` because a previous fire
already put subscribers in a coherent state; on a cold boot there is no previous
fire. Skipping produces a third state neither path has been in — notably
`RunHUDController._mapIsCurrent` defaults `true` and `ApplyRootVisibility` never
runs, so the fuel/storm/scrap HUD paints over the victory screen.

Correct shape — no early return, order load-bearing:

```csharp
OnRunStarted?.Invoke();
OnBeaconChanged?.Invoke();            // bind subscribers against loaded cursor
if (_controller.IsRunComplete)
    OnRunComplete?.Invoke();          // terminal overlay takes the screen last
EnqueueRunStateSnapshot();
```

**Reject the `SaveBootstrap.cs:222` guard.** `ActivateFor` already early-outs on
`IsResolved`; adding `!IsRunComplete` at the call site *skips the `ClearAll()`*
and adds a fourth copy of a mirrored predicate. Instead extend the predicate at
its owner: `if (Start || IsResolved || _host.IsRunComplete) { ClearAll(); return; }`
— which also closes the Haven-style-terminal hole where `IsResolved` is false.

**Prerequisite refactor:** the "map is current" predicate is now written **three
times** (`RunSceneOverlayHost`, `RunHUDController:367`, `BeaconActivator`), each
commenting that it mirrors the others. Extract
`BeaconPresentation.IsMapCurrent(BeaconData, bool isRunComplete)` and route all
three through it *in this slice* — otherwise the fix creates a fourth divergence.

**Do NOT persist `RunStatus`.** ADR-0011 parallel storage for a derived value,
and it pre-empts the correct terminal-lifecycle shape.

**Mandatory, not follow-up:** port the `GameOverViewController:87-90` lazy
element resolution into `RunCompleteViewController`. Verified: the sibling was
hardened after the UIDocument panel-clone race burned it; `RunCompleteViewController:91-96`
still caches eagerly and `Bind` warn-returns at `:46-50`. This fix moves its
`Bind` from mid-session to **frame 0 of boot** — exactly where that race bites.
Without the port, `Show()` succeeds, `Bind()` silently returns, and the victory
panel appears with a **placeholder seed and an empty deck list**, with EditMode
green. This is the single most likely way we ship green-and-wrong.

**Recheck when mastery XP lands:** `OnRunComplete` has exactly one subscriber
today and it is pure view — idempotent, no persistence, no XP, no analytics. The
day it awards XP, firing it from `BeginRunFromLoaded` becomes a duplicate-award
bug on every relaunch. Leave a comment at the fire site naming that constraint.

### 3.3 — Defect C: no save invalidation (needs a trace first)

**Verified: zero hits** for `ClearRunState` / `DeleteRunState` / `ClearSave` /
`InvalidateRun` across `Assets/Scripts`. `RunStatus.Defeat` is latched in memory
at `RunController.cs:340` and never persisted. So **quitting after a combat
defeat and relaunching resurrects the run.**

Untraced: whether a snapshot lands after the loss. That determines whether this
is "free refight of the fight you lost" or "resume at full health." Trace
`CombatController.cs:274` through to the snapshot funnel before scoping.

Same family and root cause as A: terminal states have no persistence lifecycle.
Clear-on-terminal is the 1.0 shape for both.

### 3.4 — Merge gate for Phase 3

1. EditMode `Resume_OnCompletedRun_FiresRunComplete_AfterBeaconChanged` — assert
   an **ordered list** of fire events (`["RunStarted","BeaconChanged","RunComplete"]`),
   not two counters; counters pass on the wrong shape too.
2. EditMode `Resume_OnOngoingRun_DoesNotFireRunComplete` — **the anti-false-green
   control.** Without it, an unconditional fire passes test 1.
3. EditMode `Snapshot_WhenProjectionThrows_RaisesSaveWriteFailed_AndWritesNothing`
   — assert **`InMemorySaveStorage` contents**, not the log, not the pending intent.
4. EditMode `Snapshot_WhenSaveSystemUnbound_Throws` — locks the swallow out.
5. PlayMode `Resume_OnCompletedRun_PaintsVictoryPanelWithLoadedSeed` — assert
   `Q<Label>("seed-value").text` equals the fixture seed. This is the one that
   catches the panel-clone race.
6. **Revert-proof:** back out the fan-out change, confirm test 1 goes red and
   test 2 stays green, paste the failure output into the capture.
7. One screenshot: kill boss → quit at victory → relaunch → correct seed + full
   deck list.
8. Suite at baseline **1244 / 1242 / 0 failed / 2 skipped** plus exactly the
   added tests.

**Open UX decision:** the run HUD (fuel/storm/scrap) will be visible on the
resumed victory screen under the corrected fix. Either accept it as real 1.0
state — arguably good — or occlude it. Decide explicitly; do not let it land as
"close enough."

---

## Phase 4 — Dead weight

### 4.1 — Beacon cleanup — **DONE 2026-09-11, Unity `ca59184` + `0a4b450` + `a8b769d`**

> Shipped as three commits, not two: A (author + prose + roster), B (deletions),
> C (the A2 grep gates + A3 roster test, which the verdict called non-optional).
> Outcome notes are in the Progress section above.

Verified state: the Option B rollback is 4/5 done. Merchant/Chopshop/Event/Rest
are PrefabRoot and wired. **Haven is still AdditiveScene pointing at a
`STUB-Haven-DESIGN-ME` scene** — and Haven is emitted by nothing
(`Biome1Distribution.asset`: non-terminal roster is Combat 45 / Event 25 /
Merchant 15 / Rest 15; terminal is Boss). It has no placement path *and* no
presentation path.

**TD verdict: delete Haven's roster entry.** Not a `HavenRoot.prefab` (a prefab
presenting nothing is ADR-0011 #6), not a `BeaconLoadMode.None` third value
(vestigial the day Haven gets content). `Resolve` then throws — which is the
desired **loud** failure, caught and logged at `BeaconActivator:238`, versus
today's silent blank stub load. Haven's entry ships in the slice that builds
Haven's screen.

**Commit A — author + prose (disarms resurrection):**
- `CombatPrefabAuthor.cs`: roster line 8339, `AuthorBeaconStubScene(Haven)` call
  at 8295, the `AuthorBeaconStubScene` method (9291-9320, it calls
  `EnsureSceneInBuildSettings` so it recreates scene *and* build entry),
  MenuItems 9283-9286, consts 8239-8241.
- 15 prose sites now false (ADR-0011 #7): `CombatPrefabAuthor.cs` blocks
  8225-8233 / 8296-8298 / 8331-8335 / 8358 / 9275-9289 and the completion log;
  `BeaconSceneBindingSO.cs:16-22` (the sentence that would make someone build
  the wrong thing later) plus 119 and 137-140; `BeaconActivator.cs:23/28/68`;
  `BeaconSceneBootstrap.cs:8`.
- Regenerate the SO via `Author Beacon Scene Binding`. **Do not hand-edit the
  YAML** — `AuthorBeaconSceneBinding` rewrites the whole roster on every call
  and is invoked from both `AuthorAllScenes` and `AuthorChopshopSlice`.

**Commit B — pure deletion:**
- `Haven.unity`, `Merchant.unity`, `Event.unity`, `Chopshop.unity` + `.meta`
- 4 `EditorBuildSettings` entries (7 total today; keep RunScene, CombatScene,
  Beacons/Combat). `EnsureSceneInBuildSettings` only ever *adds* — this stays
  removed only because Commit A removed the caller.

**Order is not optional.** Delete first and the next author run silently
recreates everything.

**~~Acceptance test:~~ run `Author All Scenes` twice back to back — `git diff`
must be empty.** ⛔ **THIS IS UNACHIEVABLE AND ALWAYS WAS. Proven by experiment
2026-09-10** on a scratch branch against an unmodified tree:

| Run | Against | Result |
|---|---|---|
| 1 | committed clean tree | **5 files churned** (2 prefabs, 3 scenes) |
| 2 | run 1's output | **3 files churned** — scenes only, 283 ins / 283 del |

Both runs exit 0, zero errors, zero semantic change. **Prefabs settle after one
run; scenes never settle** — `AuthorRunScene` does `NewScene(Single)` +
`new GameObject(...)`, minting fresh `m_LocalIdentfierInFile` values and
re-serialising in a different order every invocation.

**Generalise it: any acceptance criterion in this project based on diffing
author output against committed SCENES is unachievable. Do not write another.**

**Replacement criteria** — headless, and they test what 4.1 actually claims.
After Commit B, run `Author All Scenes` **once**:

1. `git status --porcelain Assets/Scenes/Beacons/` shows no `Haven.unity` —
   the resurrection-immunity the commit order buys.
2. `EditorBuildSettings.asset` still has exactly **3** entries.
3. `git diff` on `BeaconSceneBinding.asset` is empty.

Any other churn is the pre-existing idempotence problem — file it, don't let it
block 4.1.

**Prerequisite Phase 1.1 is DONE** (sentinel was a false positive).

**Also grep before shipping:** `LoadScene(` with an integer argument — removing
build entries renumbers the list. Expected zero hits; paste the result.

**Do not bundle:** `Assets/Scenes/CombatScene.unity` is a separate orphan
(referenced only in a comment at `BeaconActivator.cs:84`, but in build settings).
Different question, own follow-up.

### 4.2 — Pure deletions (ADR-0011 residue) — **DONE 2026-09-10, Unity `f832359`**

> **This section as originally written was WRONG IN FIVE PLACES.** Corrected
> text below; the original claims are struck through so the errors stay legible.
> Verdict: `production/td-verdicts/2026-09-10-phase-4-2-dead-code-deletions.md`.
>
> **The methodological error to not repeat:** the original counted
> construction-site *writes* and *test asserts* as consumption. `BeaconOutcome`
> has **zero production readers of any field** —
> `RunSceneHost.NotifyEventResolved` never reads its parameter. Grep for reads
> excluding the declaring file and the test file before calling anything "live".
>
> **The distinction that decided every item:** *superseded* (the named consumer
> shipped and chose a different input) → delete. *Awaiting a consumer AND
> honestly populated* → keep. *Awaiting a consumer AND impossible to populate*
> → delete — a field no code path can ever fill is a stub, not scaffolding.

**Shipped:** `HostAdvanceReason` + `MapAdvanceReason`; `StatKind.cs` **and
`StatModifier.cs`** (the original named only the first — it does not compile
alone); `VehicleVisualSlot` **and `GetSlot`**; `BeaconOutcome.CardsOffered`,
`.PartOffered` **and the zero-caller `.ZeroDelta`**; `MerchantSceneController`'s
choice-2 handler + its subscribe/unsubscribe pair.

**Kept, against the original text:** all **10** `EncounterPayload` members —
ordinals are load-bearing (they track the GDD one-for-one so save/telemetry
round-trips without a mapping table) and Rest/Merchant/Chopshop beacons exist as
PrefabRoots, they just do not route through the handler seam yet. **Unemitted ≠
dead**; this is where ADR-0015 genuinely applies. Also kept:
`ScrapDelta`/`FuelDelta`/`PayloadType`/`WasCombatRewardClosed`/`RunTerminated`,
honestly populated and awaiting the defeat-summary consumer.

**Moved OUT of 4.2** (neither is a pure deletion):

- **`AdvanceReason` + `BeaconTransition.Reason` → Phase 5.** Zero *production*
  readers; all three consumers named in its xmldoc shipped using other inputs.
  The correct cut is the whole enum **plus** the field — a one-member enum
  threaded through four layers is a worse #4 violation than the two-member one.
  4-layer signature change across `INodeMapMutator`, `RunController`,
  `RunSession`, `NodeMap`, `RunSceneHost`, the ctor and ~11 test sites.
  **`BeaconType.cs:41-46` claims `Reason` is "persisted on `BeaconTransition`" —
  verified FALSE.** A transitional comment that is also factually wrong is what
  let this survive prior audits; grep-verify any xmldoc asserting "persisted" or
  "consumed by" before trusting it.
- **`BeaconTravelTick` cursor fields — DONE 2026-09-10, Unity `d2a7633`.**
  Shipped ahead of the storm rewrite rather than sequenced behind it: the
  false-green was live, and the rewrite is better off shaping its own payload
  than inheriting a placeholder. Ctor 10→8; `StormAdvanceStrips` kept.
  Original framing below, preserved because it was wrong in an instructive way.

  ~~→ own item, sequenced against the storm-visual single-writer rewrite.~~ ~~"hardcoded zero storm values"~~ — it is
  **not a bug** (`PreviewedStormCursorBefore/After` have zero readers, so nothing
  displays zeros) and **not a pure deletion** (10→8 positional ctor across 4
  files). The real defect is a **false-green test**:
  `BeaconTravelTick_Test.cs:28-29` asserts ctor propagation using synthetic
  values the sole production emitter never produces, so the suite structurally
  cannot catch the discrepancy. The fields also cannot be honestly populated —
  `StormState` exposes no normalized-X accessor. `StormAdvanceStrips` stays.

<details>
<summary>Original text (superseded — kept for audit trail)</summary>

All verified, all zero-consumer:

- **`MapAdvanceReason` + `HostAdvanceReason`** (`RunSceneHost.cs:24`, `:1339-1349`):
  all three switch arms `return AdvanceReason.Departure`. Only `PlayerChoice` is
  ever passed; `CombatVictory` and `RewardClaimed` have zero call sites. This is
  `LegacyKindBridge`'s exact shape — forbidden #1, #4, #6, #8. Delete the enum
  and the mapper; pass `AdvanceReason` directly.
- **`StatKind`** (`Combat/StatKind.cs`): 3 total hits project-wide, all
  declaration or a comment explaining it's unused. Delete.
- **`VehicleVisualSlot`** (`VehicleVisual.cs:251-257`): duplicates `SlotKind`;
  `:194` is a stub return. Collapse into `SlotKind`.
- Smaller: `BeaconOutcome.cs:11-23` (6 of 10 members dead),
  `MerchantSceneController.cs:477-483` (empty no-op handler),
  `MapViewController.cs:360-368` (hardcoded zero storm values).

Sequence 4.2 **after** Phase 3 — `MapAdvanceReason` lives in `RunSceneHost.cs`,
the same file the save fix edits. Do not have both in flight.

</details>

---

## Phase 5 — Structural debt

### 5.1 — The `BuildLegacy` cluster — **DONE 2026-09-11**

> Unity `72ec787` (A) · `93b56ed` (D) · `4f2e873` (B) · `b18326b` (C).
> Net −724 lines. EditMode **1287**/1286/0/1 (new baseline, +2 from the wiring
> test), PlayMode 16/16, grep-gates clean, zero prefab churn.
> **Blocking playtest still OWED** — see below.
>
> **The plan's sizing was wrong and so was its reasoning.** Not "25 occurrences
> across 11 files" — **seven distinct shapes**, three of which neither the plan
> nor the initial scout found (`BuffStripWidget.BuildIcon`/`WireLegacyRefs`,
> `CombatHud.BuildDamagePopups`, `EnsureCrosshair`), plus a seventh public
> factory (`BuffTooltipWidget.Spawn`). Two of the three were *public API* —
> deleting the bodies without them would have converted ADR-0011 #3 into #6,
> leaving the violation count unchanged.
>
> **The real justification is #2, not #3.** `CombatPrefabAuthor` stated the
> invariant outright: author literals and `BuildLegacy` literals were maintained
> to "produce numerically identical UI" — two copies of one visual spec, i.e.
> **parallel storage**. That, not "P4 deletes them anyway", is why this belonged
> before P4: P4's capture had to read every authored value twice and reconcile,
> and the second copy had already drifted.
>
> **Commit order changed during execution.** D ran before B: `ResourceBarWidget`
> still carried a `BuildLegacy` body, so B's grep gate would have shipped with an
> exclusion — and an exclusion inside a gate is itself a bridge.
>
> **`RunSceneHost.BuildScout` is NOT this shape** — it is the EditMode suite's
> vehicle factory (20 fixtures depend on the unwired condition;
> `ScoutFallback_PartIdParity_Test` reflects in to pin it). It needs relocating,
> not deleting → **new §5.4**.

#### 5.1 findings worth keeping

- **A phantom citation seeded a whole family of defects.** `AuthorBuffStrip`'s
  log-and-continue was justified as "matches the precedent in `BuildSlotColumn`".
  **No such method exists anywhere in `Assets/`** — the only hit is the citation.
  The same shape had been copied to eight further guards in `AuthorRun`. A
  dangling citation manufactures authority; treat all nine as one lineage.
- **The right severity axis is "does the degradation announce itself?", not
  appearance-vs-behaviour.** A null vehicle asset is loud (warning + unwinnable
  deck). A wrong `sortingOrder` is silent: the game-over overlay renders *below*
  MapView, leaving a possibly-invisible modal eating input — the
  pending-offer-gate failure class, which reads as a hang.
- **Abort before the first write.** The first draft of the fix aborted *after*
  `AuthorBeaconSceneBinding()` had already rewritten `BeaconSceneBinding.asset`
  (the Phase 4.1 acceptance asset) and after an empty PartRewardPool could be
  created. `AuthorRun` is now ordered reads-then-guards-then-abort-then-writes.
  Not having a side effect beats disclosing one.
- **Hand-audit orphans; never ref-count them.** `EnergyOrbWidget`'s
  `FullBgColor`/`FullTextColor` are used inside the deleted body *and* in
  `Update`. A ref-count sweep would have deleted live code.
- **Unused `using`s emit no warning.** `BuffStripWidget` kept dead `TMPro` /
  `UnityEngine.UI` imports through a fully green suite.
- **Two comments were wrong in the opposite direction** — live code described as
  legacy. `PileChip.prefab` carries no Button or CanvasGroup at all, so
  `EnsureClickable`'s AddComponent branches are the live path. That is *not*
  ADR-0011 #3: one path, not two.
- **Verify orphan claims by script GUID, not by name.** A name grep for
  `ResourceBarWidget` also matched `SlotTargetRing.prefab`, which carries its own
  unrelated `_fillImage` under a different GUID.

#### OWED — blocking playtest (TD raised this above the standards table's ADVISORY tier for UI)

One combat exercising every deleted fallback's live counterpart: cards render ·
pile chips count, roll, and **open on click** · energy readout tints at 0 ·
turn banner recolours per phase · damage numbers rise · End Turn retints ·
**status effect applied and its icon hovered** (the highest-risk row — the only
fallback that was genuinely reachable) · victory/defeat banner. Watch the
console: an unwired ref now logs a named error instead of self-healing. The
`[AMBUSH]` banner needs `RunSceneHost.EncounterSelection` set and may be skipped
— record it as untested rather than passed.

### 5.1a — Playtest outcome + the `overrideSorting` defect — **DONE 2026-09-12**

**The playtest PASSED.** Neither defect it found is a 5.1 regression, and that
was proven by diff rather than assumed:

- **Cards.** The deleted `handCanvas.overrideSorting = true; sortingOrder = 25;`
  lived inside the `if (_cards.Length == 0)` bootstrap branch, whose own comment
  read *"Legacy fallback path only — the authored Combat.prefab carries this by
  construction."* On the authored path it never ran, before or after.
- **Tooltip.** The deleted `BuffTooltipWidget.Spawn` sat behind
  `if (_buffTooltip == null)`; that field is a required row in
  `CombatWidgetPrefabWiring_Test`, so the branch was unreachable. The surviving
  `else if` reparent branch is byte-identical pre- and post-5.1.

Rows that passed live: pile chip opens on click, energy readout reddens at 0,
damage numbers appear. Status-effect *application* could not be exercised —
no card grants one yet — so the buff row was reached via an existing
FlameBarrier instead. `[AMBUSH]` remains **UNTESTED**.

#### The defect it did surface — one cause, two symptoms

`Canvas.overrideSorting` was assigned while the GameObject was **inactive**, and
does not persist; `sortingOrder`, written in the same block, does. That
asymmetry is what made it invisible — the prefab looked right.

| Site | Author code | Was on disk |
|---|---|---|
| CardHand (`CombatPrefabAuthor.cs:3609`→`:3625`) | `SetActive(false)` then `overrideSorting = true`, `sortingOrder = 25` | `m_OverrideSorting: 0`, `m_SortingOrder: 25` |
| BuffStripCanvas (`:1094`→`:1104`) | `SetActive(false)` then `overrideSorting = true`, `sortingOrder = 22` | `m_OverrideSorting: 0`, `m_SortingOrder: 22` |

**Zero prefabs project-wide carried `m_OverrideSorting: 1`.** A nested Canvas
ignores its `sortingOrder` unless the flag is on, so:

- CardHand (verified nested under `Combat_HUD`, parent GO `6552448530280038589`)
  rendered at **10** and fell behind the vehicle `HitZonesCanvas` at **15**.
- BuffStripCanvas lost the chip raycast to the parent `BarStackCanvas`, so the
  HP bar absorbed the pointer, `BuffIconWidget.OnPointerEnter` never fired, and
  `BuffTooltipWidget.Show` was never called. The tooltip widget itself is sound.

**Why it survived two months.** `PatchCardHandCanvasMenu` (`:3372`) is a
2026-07-06 hotfix written for exactly this, which logs
`"Patched CardHand … overrideSorting=true"` on success and left the prefab
untouched. Meanwhile `CombatHud.cs:1467` asserted *"the overrideSorting=25
nested canvas"* as fact. Same phantom-citation family §5.1 itself flagged.

#### Fix shipped

1. Two surgical YAML flips, `m_OverrideSorting: 0 → 1` — `CombatHud.prefab`
   Canvas `&3615108685937019552`, `MainBar.prefab` Canvas `&577952370169777220`.
   Exactly 2 changed lines, zero fileID churn. Chosen over the existing patch
   menu deliberately: that menu ends in `SaveAsPrefabAsset`, a whole-prefab
   rewrite, and `project_author_scenes_never_idempotent` records what those
   round-trips do. Both canvases already carried a `GraphicRaycaster`, so
   enabling the flag cannot orphan input.
2. Author reordered at both sites — Canvas configured while the GO is still
   active, `SetActive(false)` moved after it and before any child lands.
3. `override_sorting_gate` in `tools/ci/grep-gates.sh` — a **positive**
   assertion (fires on absence, unlike `gate()`), anchored by Canvas fileID so a
   reminted id fails loudly instead of passing vacuously. Both failure modes
   negative-tested: flag flipped to 0 → fires with the right message; anchor
   renamed → fires "not found". This is the load-bearing enforcement, since
   `UNITY_LICENSE` is unset and the EditMode job skips in CI.

Capture: `production/polish-captures/2026-09-12-overridesorting-restoration.md`.

**Owed:** a confirming playtest — cards should now sit above the target rings,
and hovering a buff icon should produce a tooltip. Both are one-look checks.

**Follow-up not taken (needs a decision):** `PatchCardHandCanvasMenu` is now
redundant — the author does it correctly and the prefab is fixed. It is a menu
that historically no-opped while logging success. Deleting it is the
`feedback_aggressive_dead_code_cleanup` call; it was left alone because it sits
outside the approved scope of this slice.

#### Original plan text (superseded, kept for the record)

### ~~5.1 — The `BuildLegacy` cluster (do before ADR-0014 P4)~~

**25 occurrences across 11 files** in runtime `Assets/Scripts` (not editor, so
ADR-0011 exception #3 does not apply). Every combat widget carries
`if (serializedRef == null) BuildLegacy();` — a literal old-way/new-way selector,
ADR-0011 forbidden #3 in textbook form. `EndTurnButton.cs:29-33` documents the
doctrine outright.

Files: `AmbushBannerWidget`, `EndTurnButton`, `DamagePopupWidget`, `CardWidget`,
`BuffTooltipWidget`, `EnergyOrbWidget`, `PileCountWidget`, `ResourceBarWidget`,
`TurnPhaseWidget`, `DamagePopupSpawner:49-50`, `CombatSceneBlockout`. Plus
`CombatHud.cs:1503-1532` for the card hand.

**Do this before P4** — P4 has to delete these anyway, and removing them first
makes the high-risk migration materially smaller.

### 5.2 — ADR-0014 P4 + P5

The Combat_HUD migration and the CI Canvas gate. This is the root cause of the
9,348-line `CombatPrefabAuthor.cs` problem: 391 `Vector3/Vector2` literals, 286
RectTransform layout ops, 84 `Color` literals — ~760 lines of *visual appearance
expressed as code* against ~420 of structure and wiring. 231 UGUI hits vs 56
UIDocument.

The ADR itself rates P4 HIGH risk, scopes it as a dedicated M1.5 slice, and
requires exhaustive capture-before-destroy plus a full playtest before merge. It
also already names *"Combat-HUD-style 'tune in Prefab Mode and bake' workflows"*
as a cost of not finishing.

Every surface that migrates stops appearing in the author script — UXML+USS is
text, diffable, designer-editable, with nothing to overwrite it. 18 UXML / 22
USS / 3,801 USS lines already prove the pattern works here.

Two of the four currently-drifting prefabs (**MainBar**, **HudAnchors**) are P4
scope — this retires them permanently.

**Cheap stopgap worth doing regardless (~1 day):** a bake-assist tool that diffs
a live prefab against what the author would generate and emits the C# deltas.
Removes hand-transcription, which is where errors actually enter.

**Explicitly rejected:** inverting authority so prefabs become source of truth
and the author becomes a validator. Reproducible-from-scratch authoring is
load-bearing here, and prefab YAML merge conflicts trade a visible problem for
an invisible one.

### 5.3 — `VehicleBodyworkAnchors`

Zero hits. ADR-0017 declares it a Phase 2.5 commitment with `Blocks: All of
Phase 2.5`. Any bodywork or chassis-2/3 work stalls here. `SlotKind.Bodywork`
exists (`SlotKind.cs:64`, handled at `DamagePipeline.cs:116`) but zero slots are
authored, which is why the garage BODYWORK panel is empty.

### 5.5 — Targeting UX rework (NEW, 2026-09-12) — design SETTLED, step 1 SHIPPED

Came out of the Phase 5.1 playtest. Owner-settled after two technical-director
consultations and two ux-designer reviews. **Every claim below was verified
against code; the reviews' two biggest assertions did not survive and are
recorded as such.**

#### The shape — three surfaces, three jobs, no overlap

- **Idle** — vehicle art is INERT (no raycast, no tooltip). Rings are the
  primary info surface; hovering a RING gives full part info. Bars, badges and
  buff strips behave as today.
- **Targeting** — part art becomes targetable; hovering it highlights the paired
  ring (already wired, `VehicleBarStack:578`). Rings stay minimal: number +
  colour only.
- **Player intent panel, above the player vehicle** — mirrors the enemy
  `IntentWidget`. **Not merely a targeting readout.** Lifecycle: a "formulating"
  icon during the player's turn while they decide → target readout (name +
  hp/maxHp + projected damage) while a card is dragged over a target → the
  **decided intent** once committed. The player-side counterpart to enemy
  intent. That lifecycle also closes the discoverability gap, because the panel
  exists *before* the drag rather than appearing only once you commit.

#### Forward spec (owner, 2026-09-12) — not yet buildable

Status effects on attacks do not exist in the game yet. When they land:

- The **player intent panel** is where a pending attack's status effects are
  described — numbers, text and colour. The panel was chosen partly because it
  has room to grow this way; a 40px ring never would.
- Once a status is **applied** to a part, its icon shows **adjacent to that
  part's ring** — beside the disc, not inside it.

This gives the ring a defined growth path and keeps the disc itself at number +
colour permanently.

#### Rejected / banked

- **Arc + glyph + hatch ring redesign** (ux-designer) — **banked**, not used.
  Owner wants simplicity.
- **Crosshair carrying the projected number** — **rejected**. Carries no maxHp
  and the owner dislikes it visually. One surface, not two.
- **Whole-board "runway lights"** for valid targets — **dropped**, premised on a
  bug that does not exist (below).
- **Hiding the ring while its part is hovered** — rejected: ring and hit zone
  are non-coincident rects, so the swap guarantees a seam where neither contains
  the cursor, and it removes the readout at the moment of interest.

**Accepted cost, stated twice by the owner:** the panel is not in the eye-travel
path — you aim at the enemy while the readout sits above your own vehicle. It
lands on **Attack**, the frequent path; Repair gets it free because the readout
sits above the vehicle being repaired. Do not re-raise without playtest evidence.

#### Review claims that did NOT survive verification

- **"Targeting is pixel-perfect and the rework threatens it."** False. Drag
  resolution is `RectTransformUtility.RectangleContainsScreenPoint` against a
  bounding rect (`CombatHud.cs:1259`; confirmed again by the comment at
  `VehicleBarStack:591-595`). The alpha-silhouette mask only ever governed the
  IDLE raycast — the path being switched off.
- **"A repair card with nothing broken silently accepts nothing"** — ranked the
  ux-designer's #1 must-fix, twice. Unreachable.
  `RepairEffect.GetStructuralFailureReason` (`CardEffect.cs:201-204`) returns
  `"no subsystem is offline"`, which greys the card, and `CardWidget.cs:505/533`
  early-return on `!IsPlayable`, so the drag cannot begin. **The real residual is
  tiny:** the model already computes that reason string and the view never shows
  it. Surface it on the dimmed card.

#### Adjacent findings (own slices)

- **`HideOnFullUnlessAttackActive` is effectively dead.** `ResolveHideRule`
  (`:892-897`) only returns `AlwaysVisible` or `HideOnFullOrDestroyed`, yet that
  case is the serialized default (`SlotTargetRing.cs:85`), is implemented
  (`:194`), is tested, and five `VehicleBarStack` comments describe it as live.
  ADR-0011 vestigial-enum question.
- **ADR-0014's P5 gate cannot ship as written.** It forbids `Canvas` outside the
  `Popups` subtree; four ship outside it today — `IntentCanvas` (5),
  `HitZonesCanvas` (15), `BuffStripCanvas` (22), `CardHand` (25). TD ruling: the
  ADR named one *instance* where it meant the *axis* (it already says the split
  is "axis-aligned — world-space vs screen-space"). Amend to a **world-space
  vehicle-anchored canvas category** with a membership predicate and an exit
  criterion. Members: `Popups`, `HitZonesCanvas`, `IntentCanvas`,
  `TargetingReadoutCanvas` — nothing else. `BarStackCanvas` / `BuffStripCanvas` /
  `CardHand` are screen-space-equivalent and stay P4 scope.

#### Build order (TD-approved)

| # | Step | State |
|---|---|---|
| 1 | `VehicleBarStack:396` guard deletion + PlayMode regression test | **DONE + PLAYTESTED 2026-09-12** — Unity `f8b1df6`. Verdict `td-verdicts/2026-09-12-vehiclebarstack-396-hitzone-refresh.md`. All three criteria confirmed on screen: enemy zones go inert during a repair drag · enemy markers read `Name  cur/max` · enemy hit areas track damage-state sprite swaps |
| 2 | `zonesLive` single-writer consolidation of the `HitZonesCanvas` toggle | not started — behaviour-neutral in idle, survives P4 verbatim |
| 3 | ADR-0014 amendment — the canvas category above | not started, gates step 4's timing |
| 4 | Player intent panel, world-space UGUI, **its own sorting order** (NOT `IntentCanvas`'s 5, which sits under `HitZonesCanvas` at 15) | not started |
| 5 | **P4** — flip gate to targeting-only, delete zone tooltip plumbing (~60 lines + dead `\|\| _combatTooltip != null` at `:934` + reversed-Q1 prose in 3 files), idle info → ring hover, land `SlotReadout`, retire badge self-poll | not started |
| 6 | **P4 close** — P5 predicate gate written against the amended category | not started |

Step 4 reads `AttackStateController.IncomingDamage` + `DamagePipeline.PreviewDamage`
**directly**, not via `SlotReadout` — the panel is a singleton showing one slot
while `SlotReadout` is a per-slot broadcast, so routing it through would invert
the data flow. `LateUpdate` poll, no new event surface. Hang refresh off the
existing transition gate at `CombatHud:1198` and **hold-and-dim** on gaps; a
naive per-frame rebuild flickers as the cursor crosses part seams.

**Step 5 commit-boundary requirement:** the gate flip and ring-hover idle info
must land in the SAME commit, or idle per-part info goes dark mid-slice.

**Expected-but-not-yet-true until step 5, so nobody re-reports it as a defect:**
idle hover over the vehicle art STILL shows tooltips today. That is the current
shipped behaviour and it is deliberately untouched by steps 1–4. Confirmed
observed 2026-09-12 after step 1; not a regression, not a miss. The art goes
inert only when the gate flips, and the gate flips only alongside ring-hover
info — precisely so per-part information is never homeless for a commit.

### 5.4 — `BuildScout` / `_playerVehicleAsset` fallback retirement (NEW, from 5.1)

Found during 5.1 and deliberately **excluded** from it. `RunSceneHost.BuildScout`
looks like the `BuildLegacy` shape and is not: it is **the EditMode suite's
vehicle factory living in production code**. `RunSceneHost.cs:667-670` records
that **20 fixtures** depend on the unwired condition, and
`ScoutFallback_PartIdParity_Test:157-168` reflects into it and asserts it exists —
a test added 2026-08-05 to pin a real `scout_machinegun` / `scout_machine_gun`
divergence. There is also a prior TD ruling at `VehicleSwapPartTests.cs:151`.

**The distinction that matters:** `BuildScout` is a second construction path that
is *tested, pinned, and proven at parity*. The `BuildLegacy` copies had no test,
no gate, and nothing forcing them to track the author script. A maintained second
path is not the same object as an unmaintained one.

**So the remedy is relocation, not deletion** — move the factory to a
test-visible seam or an SO the fixtures load, then delete the three real value
divergences:

| Site | Diverges on |
|---|---|
| `RunSceneHost.cs:653-656` | `ChassisCards` vs `RunDeck.Milestone1Starter()` |
| `RunSceneHost.cs:1488-1489` | `TankCapacity` vs `SCOUT_TANK_CAPACITY_FALLBACK = 35` |
| `RunSceneHost.cs:1528-1529` | `FuelBurnMultiplier` vs `SCOUT_FUEL_BURN_MULTIPLIER_FALLBACK = 0.7f` |

Note `:669-686` is **not** a fourth: both arms only log (`LogError` vs
`LogWarning`) and `StartRun` runs identically — it is a severity selector on a
diagnostic, not a behavioural branch.

**Containment already shipped in 5.1 Commit A:** `AuthorRun` now aborts rather
than saving `Run.prefab` with `_playerVehicleAsset` unwired, and
`CombatWidgetPrefabWiring_Test` has no row for it yet — **add
`(Run.prefab, RunSceneHost, _playerVehicleAsset)`** when this slice starts, so
the fallback cannot silently go live in the interim.

Not P4 scope, so no ordering pressure. Sequence independently.

---

## Phase 6 — Chopshop (the active feature axis)

Steps 1–4 are **DONE and pushed** (Unity `38b2b45`), playtest passed.

### 6.1 — Step 5 prerequisites (all verified missing)

| Needed | Status |
|---|---|
| `CardDefinition.SourceSlotId` + `RunDeck.RemoveBySourceSlot` + DTO v2 | **DONE** |
| `SlotInstance.ClearPart()` uninstall verb | **zero hits — does not exist** |
| `RequireSlotMutable` extraction | **does not exist** — only `RequireInstallable` |
| `WeaponAttackEffect.LaunchSlotId` rebind on mount | **NOT DONE** — `CardDefinition.cs:103` still says "will". Capture §10c: *"do not ship equip without it"* |
| Deep copy of effects | Not done (shared-effects note, §10b) |

### 6.2 — ADR amendments owed (were due before step 3; steps 3 and 4 shipped without them)

Per capture §10g:
- **ADR-0012** — uninstall as a first-class armor-pool mutation path;
  clamp-on-shrink as policy for gesture-driven removal. Removes the "every
  combat slot stays occupied" premise.
- **ADR-0013** — larger: `RunDeck` gains a removal verb, cards gain provenance.
  The composition contract is no longer append-only.
- **ADR-0004** — `RunDeckDto` SCHEMA_VERSION 1 → 2, registry updated.
- **ADR-0011** — no amendment needed; the `LaunchSlotId` fix is an *application*
  of #2.

### 6.3 — Step 5 (install / uninstall)

Inline row icons, drag as the shortcut. The read surface was built for this:
`GarageHandlers` is a `readonly struct` so it extends; `RefreshGarage` is the
single rebuild path; `#card-preview` is already the floating drag-ghost layer;
tiles carry `InstanceId` because duplicates are not stacked.

**One design decision before implementing drag:** dropping onto the CAR cannot
work through the current seam — the centre gap is `Ignore` so UGUI hit zones get
the click, but once UI Toolkit captures the pointer the GraphicRaycaster never
sees the release. Either restrict drops to slot rows, or hand-roll a
screen→world test on `PointerUp` against `VehicleVisual.CollectHitZones`.

**TD success criterion:** if step 5 lands touching `GarageViewModel`,
`GarageHandlers` and `RefreshGarage` **additively** — no new UI class, no
picking-mode change, no UXML restructure — the read surface was right.

### 6.4 — Step 6 (buy / sell) — BLOCKED

Blocked on a **pricing design decision**, not on build work. `PartRarity` does
not exist (needs a field plus a schema bump on `RunInventoryDto` **and**
`PendingPartOfferDto`).

Prerequisites the capture names: repair-mode extraction out of the controller
before a third mode lands, and `ApplySurface(Entry|Garage|Vendor)`.

### 6.5 — Chopshop content gaps (not code)

- No `Assets/Resources/PartIcons/` — `PartIconAtlas` is wired; create the folder,
  drop sprites named by `PartId`, no code change.
- BODYWORK panel empty until a layout authors a `SlotKind.Bodywork` slot (5.3).

### 6.6 — Chopshop traps worth not rediscovering

- **`ChopshopRoot.prefab` must not be RE-authored** — corrected 2026-09-09. The
  earlier wording ("edited by surgical YAML, never authored by the tool") was
  wrong: `CombatPrefabAuthor.AuthorChopshopRootPrefab` does own the prefab and
  writes it via `SaveAsPrefabAsset` (`:9127`), and `AuthorAllScenes` calls it
  (`:8273`). The real trap is narrower and worse. The author calls
  `ActivateRecursively(root)` immediately before saving (`:9126`), so the prefab
  is written **active**, while `RunScene.unity:663-666` carries an
  `m_IsActive: 0` override targeting `fileID: 8814606828562375546`. Re-authoring
  reissues the GameObject fileIDs, orphaning that override — the ChopshopRoot
  instance then stays active and the chopshop boots over the run map.
  Authoring it the *first* time is fine; re-running it is what breaks.
- `RecomputeArmorPool` no-ops until `FillArmorPool` has run — a fixture that
  skips it looks like it proves something and doesn't.
- `UnbindWorkbench` does not hide the bars; the `HudAnchors` container must be
  `SetActive(false)`.

---

## Smaller items, logged

- `VehiclePartHitZone.SetTargetHover` calls `EnsureTargetHoverOutline()`
  unconditionally (`:364`) — **builds** an outline in order to hide it
  (GameObject reparent + sprite from a 1024×1024 mask), and a hard Unity error
  if the zone is mid-destroy. One-line guard: early-out when
  `!hovered && _targetHoverOutline == null`. Also `OnDisable` does not
  unsubscribe hover — only `OnDestroy` does.
- Storm single-writer rewrite (queued TD slice).
- Storm cosmetic docking gap; `StormFrontElement` 30 Hz repaint while the map is
  hidden — gate on visibility.
- `RunSceneHost_ChopshopResume_test.cs` — cheap regression lock on the Leave
  double-resolve fix; copy the `RunSceneHost_RestResume_test` shape.
- Fuel economy pass — sim says fuel, not storm, binds a 55-beacon map.
- `RunSceneOverlayHost` / `BeaconActivator` subscribe to an **external**
  publisher (`_host`) in `OnEnable`/`OnDisable`. Project rule says Bind↔OnDestroy
  for external publishers. Dormant — neither object is `SetActive`-cycled. Do not
  fix speculatively; do not let anyone add a `SetActive` toggle without revisiting.

---

## Playtest debt

- The read-only garage has never been on screen (steps 1–4 playtest covered the
  entry, not every layout number).
- Composed deck (13 cards) and the new offer text are proven by test only.
- Unconfirmed on screen: whether the part-offer row shows granted-card text
  ("4x BulletBarrage") or the old category label.

---

## What is genuinely clean

Worth stating so it doesn't get re-audited:

- Both repos committed and pushed; working tree clean.
- Zero open bug files.
- Only **3** TODO markers across 261 source files — the data-flag-instead-of-TODO
  discipline is holding.
- 103 test files; **1** skipped test (the Dredge one at 0.4).
- Zero `NotImplementedException`, zero `[Obsolete]` project-wide.
- ADR-0002, 0004, 0012, 0013, 0015 fully executed. 0007 and 0009 correctly
  superseded by 0010.
- The `LegacySlotKind` retirement is real: zero hits for all three retired
  symbols, 81 `slotId` sites.
- Determinism discipline holds in code — the only live nondeterminism hit is
  `SaveSystem.cs:121` (`DateTime.UtcNow` for a save timestamp, outside seeded
  systems, benign).
- The SO data layer is genuinely designer-friendly: 79 `[Tooltip]` across 82
  `[SerializeField]`, `OnValidate` guardrails, and both data initializers guard
  on existence so re-running preserves designer edits.

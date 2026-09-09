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
| Phases 1–6 | not started |

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

### 4.1 — Beacon cleanup (two commits, A then B)

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

**Acceptance test:** run `Author All Scenes` twice back to back — `git diff` must
be empty. Requires Phase 1.1 done first.

**Also grep before shipping:** `LoadScene(` with an integer argument — removing
build entries renumbers the list. Expected zero hits; paste the result.

**Do not bundle:** `Assets/Scenes/CombatScene.unity` is a separate orphan
(referenced only in a comment at `BeaconActivator.cs:84`, but in build settings).
Different question, own follow-up.

### 4.2 — Pure deletions (ADR-0011 residue)

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

---

## Phase 5 — Structural debt

### 5.1 — The `BuildLegacy` cluster (do before ADR-0014 P4)

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

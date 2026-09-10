# TD Verdict — Phase 4.1 Beacon Cleanup

**Date:** 2026-09-10
**Status:** verdict obtained, **NO CODE WRITTEN**. Slice not started.
**Baseline:** EditMode 1281/1280/0/1, PlayMode 16/16.

> **Hook note:** `td-review-required.sh` is date-keyed. When 4.1 is picked up on a
> later date this file will not satisfy it — restate it under that date, as was
> done for the terminal-lifecycle slice. The *reasoning* below does not expire.

## Files covered

`Assets/Editor/CombatPrefabAuthor.cs` · `Assets/Scripts/Run/Authoring/BeaconSceneBindingSO.cs`
· `Assets/Scripts/Run/Authoring/BiomeDistributionSO.cs` · `Assets/Scripts/CombatView/BeaconActivator.cs`
· `Assets/Data/BeaconScenes/BeaconSceneBinding.asset` · `ProjectSettings/EditorBuildSettings.asset`

## TD Verdict

**TD-CHANGE-IMPACT: APPROVE WITH AMENDMENTS.** Two-commit shape and order are
correct. Order is load-bearing and the mechanism was verified, not assumed:
`AuthorBeaconStubScene` calls `EnsureSceneInBuildSettings` (`:9317`), which only
ever ADDS (`:9326`). Delete-then-author resurrects scene *and* build entry.

### Verified facts (re-measured, plan confirmed)

- Binding SO: Haven is the lone non-combat `AdditiveScene`; **`Merchant.unity` /
  `Event.unity` / `Chopshop.unity` are referenced by nothing** — `Event.unity`
  not even by a const.
- `Haven.unity` root GameObject is literally `STUB-Haven-DESIGN-ME`.
- `Biome1Distribution`: Combat 45 / Merchant 15 / Event 25 / Rest 15, terminal
  Boss. **Haven emitted by nothing.**
- `LoadScene(` with an integer argument: **zero hits.** One runtime load site,
  string-path keyed (`BeaconActivator.cs:338`). Renumbering safe.
- `EditorBuildSettings`: exactly 7 entries. Author-script line numbers accurate.
- `AuthorAllScenes` (`:8256`) has **no `DisplayDialog` guard** — headless-callable.

### THE CORRECTION THAT MATTERS — a Haven terminal never reaches `Resolve`

My briefing argued the throw from `Resolve` was the desired loud failure.
**Wrong**, and verified directly:

- `RunController.CommitNextBeacon:749-753` — on arrival at a NON-combat terminal
  it calls `MarkResolved()` and latches `RunStatus.Victory` **synchronously**.
- `BeaconActivator.ActivateFor:215` short-circuits on `current.IsResolved` →
  `ClearAll(); return;` — **before** the `Resolve` call at `:233`.

So the Haven binding entry is **already unreachable via the terminal path
today**. Deleting it changes nothing about terminal behaviour. The only route to
`Resolve(Haven)` is a NON-terminal Haven, which requires putting Haven in
`_nonTerminalBeaconTypes`.

**Consequence: do NOT flip `BiomeDistributionSO._terminalBeaconType`'s default
from Haven to Boss.** There is no throw to avoid, and flipping costs a
`BossEncounter is null` validation warning on every freshly-created SO
(`BiomeDistributionSO.cs:474-479`) plus re-auditing 11 construction sites in
`BiomeDistributionSO_FuelCosts_Test` that rely on the default.

### Haven's live mechanics — KEEP, untouched

`RunSession.cs:510-514` (refill + storm reset), `:692-696` (preview), `:784`
(stranded gate), `MapViewController.cs:348/369/381`,
`RunSceneOverlayHost.cs:252/431/475`. All read `BeaconData.Type` off the model;
**none consults the binding SO**. This slice removes presentation binding only.
That is the ADR-0015 lagging-dependency shape, not an ADR-0011 bridge.

**Logged, NOT fixed here:** `BeaconType.cs:14-18` says generators/reward
sources/view bindings must never `switch` on these values — yet there are eight
`== BeaconType.Haven` branches. Canonical fix is a per-type arrival-effect
column on `BiomeDistributionSO`, sibling to `BeaconFuelCosts`. Own slice, own
save/preview blast radius. Natural co-traveller with the storm-visual rewrite.

### Test blast radius — nothing can red

Zero references to `BeaconSceneBindingSO` / `BeaconSceneEntry` /
`BeaconLoadMode` anywhere in EditMode. The 47 EditMode files mentioning Haven
are model-level and never activate a beacon. PlayMode: only
`ChopshopBeaconRevisit_Test:155-158` and `ChopshopRepairMode_Test:641-644` load
the asset, and both assert only that **Chopshop** resolves to PrefabRoot.

**The one real execution hazard:** regenerate the SO with **`Author Beacon
Scene Binding` ONLY** (`:8308`), never `Author All Scenes` — the latter
re-authors `ChopshopRoot.prefab` and reissues its fileIDs, dragging churn into
Commit A and risking the documented chopshop-boots-over-the-map state if the run
aborts mid-way.

## Amendments beyond the plan

- **A1** — `BeaconSceneBindingSO.cs:62-65` tells a future dev to fix a missing
  binding by re-authoring. After this slice that is **false advice** and it is
  load-bearing for the whole rationale. Rewrite to state the new contract: a
  type without an entry has no presentation *by design*; adding one is a slice
  that ships the screen.
- **A2** — add a `tools/ci/grep-gates.sh` gate on `AuthorBeaconStubScene`
  (ADR-0011 exception #5). Without it, resurrection-immunity depends on commit
  order holding forever.
- **A3** — add a roster-shape EditMode test. After this the SO no longer covers
  every `BeaconType` and **nothing** enforces the weakened contract. Assert the
  7 `(Type, Mode)` pairs and that `Resolve(Haven)` throws.
- **A4** — **all three** consts go: `ChopshopScenePath` (`:8241`) is already
  dead residue, its only mention a comment at `:8331-8335`.
- **A5** — scope the prose sweep by grep, not by the plan's "15 sites". There
  are 139 "Haven" mentions across 30 files; most stay true (they describe
  mechanics). Sweep only presentation-binding prose:
  `grep -rn "Haven" --include=*.cs Assets/Scripts Assets/Editor | grep -iE "stub|AdditiveScene|PrefabRoot|scene path|\.unity|binding"`
- **A6** — do NOT edit `production/polish-captures/*`, `td-verdicts/*` or
  `audits/*`. They are timestamped records; correcting them destroys the trail.
- **Also false, plan misses:** `BiomeDistributionSO.cs:63-65` ("For Biome 1,
  this is Haven (run exit)") and `:79-82`. Biome 1 has been Boss since
  2026-07-26.

### What does NOT cascade — checked to prevent over-deletion

`EnsureSceneInBuildSettings` stays (called at `:8569`, `:8783`).
`BeaconScenesRoot` (`:8236`) stays (used by `AuthorCombatScene` at `:8769`).
`BeaconSceneBootstrap` stays — root component on `Combat.unity` and all four
`*Root.prefab` roots.

---

## THE ACCEPTANCE TEST IS UNACHIEVABLE — PROVEN BY EXPERIMENT

The plan gates 4.1 on *"run `Author All Scenes` twice back to back — `git diff`
must be empty."* The TD predicted this could not pass. **Two runs on a scratch
branch against an unmodified tree proved it:**

| Run | Against | Result |
|---|---|---|
| 1 | committed clean tree | **5 files churned** — `ChopshopRoot.prefab`, `EventRoot.prefab`, `Beacons/Combat.unity`, `Beacons/Haven.unity`, `RunScene.unity` |
| 2 | run 1's output | **3 files churned** — scenes only, 283 insertions / 283 deletions |

Both runs: exit 0, **zero errors**, zero semantic change.

**Prefabs settle after one run. Scenes NEVER settle.** `AuthorRunScene` does
`NewScene(Single)` + `new GameObject(...)`, so every invocation mints fresh
`m_LocalIdentfierInFile` values and re-serialises objects in a different order —
`STUB-Haven-DESIGN-ME` and `BeaconSceneBootstrap` swap positions between runs.

**This is a property of the authoring pipeline and has nothing to do with 4.1.**
Generalise it: **any acceptance criterion in this project based on diffing
author output against committed scenes is unachievable.** Do not write another.

### Replacement acceptance criteria (headless, and they test the actual claim)

After Commit B, run `Author All Scenes` **once** and assert:

1. `git status --porcelain Assets/Scenes/Beacons/` shows no `Haven.unity` —
   nothing untracked, nothing restored. *(This is the resurrection-immunity the
   commit order buys — the thing 4.1 actually claims.)*
2. `EditorBuildSettings.asset` still has exactly **3** entries.
3. `git diff Assets/Data/BeaconScenes/BeaconSceneBinding.asset` is empty.

Any other churn that run produces is the pre-existing idempotence problem —
note it, file it, do not let it block 4.1.

## Recommended sequence when resumed

0. ~~Scratch-branch idempotence experiment~~ — **DONE 2026-09-10, see above.**
1. **Commit A** — author + prose. Regenerate with `Author Beacon Scene Binding`
   **only**. Includes A1, A4, A5 and the two `BiomeDistributionSO` prose fixes.
2. **Commit B** — 4 scenes + 4 `.meta` + 4 build entries.
3. Full EditMode + PlayMode. Expect 1281/1280/0/1 and 16/16, plus
   `grep -cE 'error CS' TestResults/editmode.log`.
4. `Author All Scenes` ×1 → the three replacement criteria above.
5. **Commit C** (separate) — A2 grep gate + A3 roster test.

**Success looks like:** a year from now the Haven-screen slice adds one roster
line and one prefab and touches nothing else, and no author run has silently
recreated a stub scene in the interim.

## Three-lens self-audit

Returned with the verdict. **Health:** `ChopshopScenePath` found as live residue;
the eight `== BeaconType.Haven` branches contradict `BeaconType.cs:14-18` and are
logged with a named fix shape rather than left to become the next audit finding.
**Optimization:** confirmed no delta and explicitly declined to claim one —
`Resolve` is a linear scan over ≤8 entries once per beacon transition; removing
4 build entries trims the player build but the scenes were never loaded, so no
load-time benefit may be claimed. **1.0 survival:** deleting the entry is the
1.0 shape — the Haven slice ships `HavenRoot.prefab` + its roster line together.
Flagged risk: the SO's contract silently weakens from "every non-Start type is
bound" to "bound types are bound", and nothing enforces the weaker form — A3 is
the mitigation and is **non-optional**, not nice-to-have.

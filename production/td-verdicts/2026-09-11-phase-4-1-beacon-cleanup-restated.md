# TD Verdict — Phase 4.1 Beacon Cleanup (restated for 2026-09-11)

**Date:** 2026-09-11
**Original verdict:** `production/td-verdicts/2026-09-10-phase-4-1-beacon-cleanup.md`
**Status:** execution begins today. The original is a timestamped record and is
NOT edited (amendment A6).

> **Why this file exists:** `td-review-required.sh` is date-keyed — it demands a
> verdict dated *today*. The reasoning in the 2026-09-10 verdict does not expire;
> this file restates it verbatim in substance so the gate resolves against the
> real review rather than a rubber stamp.

## Files covered

`CombatPrefabAuthor.cs` · `BeaconSceneBindingSO.cs` · `BiomeDistributionSO.cs`
· `BeaconActivator.cs` · `BeaconSceneBootstrap.cs`
· `BeaconSceneBinding.asset` · `EditorBuildSettings.asset`
· `Haven.unity` · `Merchant.unity` · `Event.unity` · `Chopshop.unity`
· `grep-gates.sh` · `BeaconSceneBinding_Roster_Test.cs`

## TD Verdict

**TD-CHANGE-IMPACT: APPROVE WITH AMENDMENTS** (carried forward unchanged from
2026-09-10). Delete Haven's roster entry rather than authoring a
`HavenRoot.prefab` that presents nothing (ADR-0011 #6) or adding a
`BeaconLoadMode.None` third value (vestigial the day Haven gets content).
Haven's entry ships in the slice that builds Haven's screen.

**Two-commit order is load-bearing and was verified, not assumed:**
`AuthorBeaconStubScene` calls `EnsureSceneInBuildSettings` (`:9317`), which only
ever ADDS (`:9326`). Delete-then-author resurrects scene *and* build entry.
Commit A (remove the caller) must land before Commit B (delete the files).

### Verified facts — re-measured TODAY against the live tree

| Claim | Re-verified 2026-09-11 |
|---|---|
| `LoadScene(` with an integer argument | **zero hits repo-wide** — in fact zero `LoadScene(` of any form; the only load site is `LoadSceneAsync` |
| `EditorBuildSettings` entry count | **7** — RunScene, CombatScene, Beacons/{Combat, Haven, Merchant, Event, Chopshop} |
| Haven is `AdditiveScene` → `HavenScenePath` | confirmed, `CombatPrefabAuthor.cs:8339` |
| `Merchant.unity` / `Event.unity` / `Chopshop.unity` referenced by nothing | confirmed — `MerchantScenePath` survives only to feed the stub authoring path; `ChopshopScenePath` is referenced by nothing but a comment |
| A5 prose sweep scope | **15 lines across 5 files** (grep in the original verdict, re-run today) |

### The correction that matters — a Haven terminal never reaches `Resolve`

`RunController.CommitNextBeacon:749-753` marks resolved and latches
`RunStatus.Victory` synchronously on arrival at a non-combat terminal;
`BeaconActivator.ActivateFor:215` then short-circuits on `IsResolved` →
`ClearAll(); return;` **before** the `Resolve` call at `:233`. The Haven binding
entry is already unreachable via the terminal path. Deleting it changes nothing
about terminal behaviour.

**Therefore: do NOT flip `BiomeDistributionSO._terminalBeaconType`'s default**
from Haven to Boss. There is no throw to avoid, and flipping costs a
`BossEncounter is null` validation warning on every freshly-created SO plus a
re-audit of 11 construction sites in `BiomeDistributionSO_FuelCosts_Test`.

### Haven's live mechanics — KEEP, untouched

`RunSession.cs:510-514` (refill + storm reset), `:692-696` (preview), `:784`
(stranded gate), `MapViewController.cs:348/369/381`,
`RunSceneOverlayHost.cs:252/431/475`. All read `BeaconData.Type` off the model;
none consults the binding SO. **This slice removes presentation binding only** —
the ADR-0015 lagging-dependency shape, not an ADR-0011 bridge.

**Logged, NOT fixed here:** `BeaconType.cs:14-18` forbids `switch`ing on these
values, yet eight `== BeaconType.Haven` branches exist. Canonical fix is a
per-type arrival-effect column on `BiomeDistributionSO`. Own slice, own
save/preview blast radius.

## Amendments (all carried forward, all in scope)

- **A1** — `BeaconSceneBindingSO.cs:62-65` tells a future dev to fix a missing
  binding by re-authoring. After this slice that is false advice and it is
  load-bearing for the whole rationale. Rewrite to the new contract: a type
  without an entry has no presentation *by design*.
- **A2** — add a `tools/ci/grep-gates.sh` gate on `AuthorBeaconStubScene`
  (ADR-0011 exception #5). Without it, resurrection-immunity depends on commit
  order holding forever.
- **A3** — add a roster-shape EditMode test. **Non-optional.** The SO's contract
  silently weakens from "every non-Start type is bound" to "bound types are
  bound", and nothing else enforces the weaker form. Assert the 7 `(Type, Mode)`
  pairs and that `Resolve(Haven)` throws.
- **A4** — all three scene-path consts go, `ChopshopScenePath` included.
- **A5** — scope the prose sweep by grep, not by the plan's "15 sites".
- **A6** — do NOT edit `polish-captures/*`, `td-verdicts/*` or `audits/*`.
- **Also false, plan misses:** `BiomeDistributionSO.cs:63-65` and `:79-82` still
  claim Biome 1 caps at Haven. It has been Boss since 2026-07-26.

### What does NOT cascade — checked to prevent over-deletion

`EnsureSceneInBuildSettings` stays (called at `:8569`, `:8783`).
`BeaconScenesRoot` stays (used by `AuthorCombatScene`). `BeaconSceneBootstrap`
stays — root component on `Combat.unity` and all four `*Root.prefab` roots.

### The one real execution hazard

Regenerate the SO with **`Author Beacon Scene Binding` ONLY**, never `Author All
Scenes` — the latter re-authors `ChopshopRoot.prefab` and reissues its fileIDs,
dragging churn into Commit A and risking the documented
chopshop-boots-over-the-map state if the run aborts mid-way.

## Acceptance criteria (the plan's original test is DISPROVEN — see 2026-09-10)

Diffing author output against committed scenes is unachievable in this project;
`AuthorRunScene` mints fresh `m_LocalIdentfierInFile` values every invocation.
After Commit B, run `Author All Scenes` **once** and assert:

1. `git status --porcelain Assets/Scenes/Beacons/` shows no `Haven.unity`.
2. `EditorBuildSettings.asset` still has exactly **3** entries.
3. `git diff Assets/Data/BeaconScenes/BeaconSceneBinding.asset` is empty.

Any other churn is the pre-existing idempotence problem — note it, do not let it
block 4.1.

## Technical Director Review

Verdict as above: **APPROVE WITH AMENDMENTS**, A1–A6 all in scope, A3
non-negotiable.

### Three-lens self-audit

**Health:** `ChopshopScenePath` is live residue and goes with the rest; the eight
`== BeaconType.Haven` branches contradict `BeaconType.cs:14-18` and are logged
with a named fix shape rather than left to become the next audit finding.
**Optimization:** no delta, and none is claimed. `Resolve` is a linear scan over
≤8 entries once per beacon transition; removing 4 build entries trims the player
build but those scenes were never loaded, so no load-time benefit exists.
**1.0 survival:** deleting the entry *is* the 1.0 shape — the Haven slice ships
`HavenRoot.prefab` and its roster line together. Success looks like: a year from
now the Haven-screen slice adds one roster line and one prefab and touches
nothing else, and no author run has silently recreated a stub scene in between.

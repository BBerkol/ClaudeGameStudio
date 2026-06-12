# Test Strategy

> **Status**: Active
> **Last Updated**: 2026-04-25

## Repo split — read this first

Wasteland Run uses a two-repo structure:

| Repo | Purpose | Path |
|------|---------|------|
| **Madmax Roguelike** (this repo) | Design docs, architecture, production management | `C:\ClaudeCreations\Madmax Roguelike\` |
| **Wasteland Run** | Unity 6.3 LTS project with all buildable code + tests | `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\` (remote: BBerkol) |

Test code lives in the Wasteland Run repo because Unity Test Framework requires Unity assemblies. This repo holds only the strategy document and points to it.

## Framework

- **Unity Test Framework** (NUnit-based, ships with Unity 6.3 LTS)
- **Test types in use**: EditMode (logic/unit tests in pure C#)
- **PlayMode** (runtime/integration): not yet authored — added when first runtime system needs coverage

## Test location

`Wasteland Run/Assets/Tests/EditMode/Combat/`

| File | Coverage |
|---|---|
| `AmbushSetupTests.cs` | EncounterType.Ambush setup intent resolution |
| `CardPlayTests.cs` | Card play loop (energy, hand, target validation) |
| `CombatLoopLifecycleTests.cs` | Combat lifecycle (start, turn, end) |
| `CombatLoopSetupTests.cs` | Combat initialization |
| `DamagePipelineTests.cs` | Damage application + armor + Frame splash |
| `DeckHandDiscardTests.cs` | Deck/Hand/Discard invariant (`Deck+Hand+Discard == StartingSize`) |
| `EncounterRulesTests.cs` | Encounter rule resolution |
| `EncounterTypeTests.cs` | EncounterType axis (Standard / Ambush) |
| `EnemyIntentTests.cs` | Enemy intent resolution |
| `EnemySelfRepairTests.cs` | SelfRepairBrain priority order + repair resolution |
| `IntentPoolTests.cs` | Pool-filter on position axis (R17) |
| `PositionAxisTests.cs` | Position axis (Ahead/Behind) + position swap |
| `RepairCardTests.cs` | Repair card resolution + ungated escape hatch |
| `SplashDamageTests.cs` | `floor(damage/2)` Frame splash on non-Frame hits |
| `SymmetricFizzleTests.cs` | OQ-CC-NEW-3/4 symmetric fizzle (player throws, enemy fizzles) |
| `VehicleArmorTests.cs` | Armor model + cap line |
| `VehicleTests.cs` | Vehicle state + subsystem state machine |

**Current count: 136 passing EditMode tests** (last verified 2026-04-24 post Unity backfill).

## Assembly setup

`WastelandRun.Combat.Tests.asmdef`:
- Platform: Editor-only
- References: `WastelandRun.Combat`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, `nunit.framework.dll`
- `defineConstraints`: `UNITY_INCLUDE_TESTS` (excluded from player builds)
- `autoReferenced: false` (test assembly never referenced by gameplay code)

## How to run

### Locally (developer loop)

1. Open the Wasteland Run project in Unity Editor
2. Window → General → Test Runner
3. Switch to **EditMode** tab → Run All

### Headless (used for CI when added)

```
Unity.exe -batchmode -nographics -projectPath "<wasteland-run-path>" \
  -runTests -testPlatform EditMode -testResults <output.xml>
```

## CI

**Status: deferred to first production sprint.**

Rationale: CI's value is catching regressions from changes the committer didn't notice. Currently the project is solo, in Technical Setup, with no parallel work streams. Tests are run locally before commits and have not silently broken. Setting up `game-ci/unity-test-runner@v4` requires a Unity license activation flow (ULF generation + GitHub secrets) that is best done when the cost of *not* having CI starts to bite — i.e., once feature work begins on multiple stories simultaneously.

**Trigger to enable CI:** First sprint plan committed in `production/sprints/`.

**Planned CI shape (when added):**

- Location: `Wasteland Run/.github/workflows/tests.yml` (CI lives where the code lives)
- Action: `game-ci/unity-test-runner@v4`
- Trigger: Push to main + PR to main
- License: ULF stored as `UNITY_LICENSE` GitHub secret in BBerkol/Wasteland Run repo
- Reporting: NUnit XML uploaded as workflow artifact

## Coverage strategy

Per `.claude/docs/coding-standards.md`, story-type test gating:

| Story type | Required evidence | Gate level |
|---|---|---|
| Logic (formulas, AI, state machines) | Unit test in `EditMode/` | BLOCKING |
| Integration (multi-system) | Integration test (PlayMode when added) OR documented playtest | BLOCKING |
| Visual/Feel | Screenshot + lead sign-off | ADVISORY |
| UI | Manual walkthrough doc OR interaction test | ADVISORY |
| Config/Data | Smoke check pass | ADVISORY |

Logic stories must have automated coverage before marking Done. Visual/Feel stories use sign-off docs in `production/qa/evidence/` instead.

## Forbidden in tests

Per `.claude/docs/coding-standards.md`:

- **No `UnityEngine.Random`** — use `System.Random` with explicit seed (deterministic)
- **No external I/O** — no file system, no network, no DB calls
- **No order dependence** — each test sets up and tears down its own state
- **No magic numbers in fixtures** (exception: boundary value tests where the number IS the point)

## Open items

- **PlayMode test pass** — added when first runtime system (e.g., HUD or save flow) needs integration coverage
- **CI workflow** — added at sprint 1 trigger (see CI section above)
- **Coverage minimum** — `.claude/docs/technical-preferences.md` lists this as `[TO BE CONFIGURED — set at architecture phase]`. Decide during `/architecture-review`.

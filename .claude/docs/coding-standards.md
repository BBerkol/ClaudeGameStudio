# Coding Standards

- All game code must include doc comments on public APIs
- Every system must have a corresponding architecture decision record in `docs/architecture/`
- Gameplay values must be data-driven (external config), never hardcoded
- All public methods must be unit-testable (dependency injection over singletons)
- Commits must reference the relevant design document or task ID
- **Commit messages**: Use Conventional Commits format — `feat:`, `fix:`, `chore:`, `docs:`, `test:`, `refactor:`. Reference the story or task ID in the body (e.g., `Story: EPIC-001-S02`).
- **Verification-driven development**: Write tests first when adding gameplay systems.
  For UI changes, verify with screenshots. Compare expected output to actual output
  before marking work complete. Every implementation should have a way to prove it works.

# Design Document Standards

- All design docs use Markdown
- Each mechanic has a dedicated document in `design/gdd/`
- Documents must include these 8 required sections:
  1. **Overview** -- one-paragraph summary
  2. **Player Fantasy** -- intended feeling and experience
  3. **Detailed Rules** -- unambiguous mechanics
  4. **Formulas** -- all math defined with variables
  5. **Edge Cases** -- unusual situations handled
  6. **Dependencies** -- other systems listed
  7. **Tuning Knobs** -- configurable values identified
  8. **Acceptance Criteria** -- testable success conditions
- Balance values must link to their source formula or rationale

# Testing Standards

## Test Evidence by Story Type

All stories must have appropriate test evidence before they can be marked Done:

| Story Type | Required Evidence | Location | Gate Level |
|---|---|---|---|
| **Logic** (formulas, AI, state machines) | Automated unit test — must pass | `tests/unit/[system]/` | BLOCKING |
| **Integration** (multi-system) | Integration test OR documented playtest | `tests/integration/[system]/` | BLOCKING |
| **Visual/Feel** (animation, VFX, feel) | Screenshot + lead sign-off | `production/qa/evidence/` | ADVISORY |
| **UI** (menus, HUD, screens) | Manual walkthrough doc OR interaction test | `production/qa/evidence/` | ADVISORY |
| **Config/Data** (balance tuning) | Smoke check pass | `production/qa/smoke-[date].md` | ADVISORY |

## Automated Test Rules

- **Naming**: `[system]_[feature]_test.[ext]` for files; `test_[scenario]_[expected]` for functions
- **Determinism**: Tests must produce the same result every run — no random seeds, no time-dependent assertions
- **Isolation**: Each test sets up and tears down its own state; tests must not depend on execution order
- **No hardcoded data**: Test fixtures use constant files or factory functions, not inline magic numbers
  (exception: boundary value tests where the exact number IS the point)
- **Independence**: Unit tests do not call external APIs, databases, or file I/O — use dependency injection

## What NOT to Automate

- Visual fidelity (shader output, VFX appearance, animation curves)
- "Feel" qualities (input responsiveness, perceived weight, timing)
- Platform-specific rendering (test on target hardware, not headlessly)
- Full gameplay sessions (covered by playtesting, not automation)

## CI/CD Rules

- Automated test suite runs on every push to main and every PR
- No merge if tests fail — tests are a blocking gate in CI
- Never disable or skip failing tests to make CI pass — fix the underlying issue
- Engine-specific CI commands:
  - **Godot**: `godot --headless --script tests/gdunit4_runner.gd`
  - **Unity**: `game-ci/unity-test-runner@v4` (GitHub Actions)
  - **Unreal**: headless runner with `-nullrhi` flag

# Coroutine & One-Shot-Event Lifecycle (ruled 2026-09-16/17, from the P4a postmortem)

Runtime facts these rules rest on, both PINNED BY TEST on Unity 6000.3.13f1
(`CardHandElement_AnimationLifecycle_Test`):

- Deactivating a coroutine's host GameObject kills it WITHOUT disposing the
  iterator — pending `finally` blocks DO NOT run.
- `yield return someIterator()` executes the nested body IN THE SAME step —
  "lazy iterator = next frame" is false (`StartCoroutine` drills through).

## Rules

1. **Blocking-flag coroutines use try/finally with a single named settle.**
   Any coroutine that raises a flag other code blocks on (`IsAnimating`,
   a stored `Coroutine` handle gating re-entry, a routine handle gating a
   request path) must release that flag in a `finally` that calls ONE named
   settle method — the coroutine's only terminal path. Never duplicate the
   settle in the normal tail (two exit paths drift; ADR-0011). `try/catch`
   around a `yield` does not compile — `try/finally` only.
2. **try/finally is half the remedy.** It covers exceptions and explicit
   `StopCoroutine`. It does NOT cover host deactivation (see pinned fact) or
   a live-but-wedged tween. Every such coroutine also needs a stoppable
   handle and an explicit abort (`AbortAnimation` pattern: StopCoroutine +
   invoke the registered settle if the flag survived), and every teardown
   path (OnDisable, sequencer Stop, timeout branches) must route through it.
   Adopt both halves together or neither.
3. **One-shot event registrations validate before unregistering.** A
   callback that unregisters itself on first fire must first verify its
   payload is usable (e.g. `GeometryChangedEvent` on a parent can fire
   before children resolve — NaN/zero geometry). Invalid → keep listening;
   provide a later-anchor fallback (first-use) plus a loud diagnostic if
   even that fails. Silent one-shot failure is permanent and invisible.
4. **Feel values are serialized; integrity budgets are derived constants.**
   Designer-facing feel numbers live in `[SerializeField]` (and must stay in
   lockstep with any prefab-baked copy — guard with a drift test). Pipeline
   timeout/wedge budgets are `const`, DERIVED from the animation's own
   declared durations, owned by the code that owns the durations — never a
   second hand-authored number in another assembly.
5. **Freeze view elements the model has disowned.** Between a model mutation
   and the beat that claims its view elements (≥1 coroutine step), per-frame
   Ticks run against the mutated model. An element whose model item has left
   the collection must hold position, not recompute layout.

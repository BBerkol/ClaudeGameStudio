# ADR-0003: Deterministic RNG Discipline for Loot & Reward (and all seeded run systems)

## Status

Accepted

## Date

2026-04-24

## Last Verified

2026-04-24

## Decision Makers

- User (creative/design lead) — approved 2026-04-24
- technical-director (architectural review) — approved 2026-04-24
- unity-specialist (engine-idiom review) — approved 2026-04-24

## Summary

All seeded run systems (Loot & Reward, Node Map generation, any future reproducible subsystem) derive scoped `System.Random` instances per call from `RunSeed ^ stepIndex`, with the RNG passed down by reference through the call graph. Non-deterministic sources (`UnityEngine.Random`, `Time.*`, `DateTime.Now`, `Guid.NewGuid`, `Random.Shared`, static mutable fields) are explicitly forbidden inside these functions and will be enforced by CI grep.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.13f1) |
| **Domain** | Core / Scripting |
| **Knowledge Risk** | LOW — pure C# standard library (`System.Random`), no post-cutoff APIs |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/breaking-changes.md`, `.claude/docs/technical-preferences.md`, `design/gdd/loot-reward.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | None — `System.Random` behavior is stable C# standard library |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0002 — established the `System.Random` + explicit-seed pattern for one system; this ADR generalizes it |
| **Enables** | ADR-0004 Save & Persistence (defines what RunSeed state must persist), any future Node Map generation ADR (same seeding discipline applies) |
| **Blocks** | Loot & Reward production epic stories (AC-LR1 through AC-LR5 require this ADR Accepted); Node Map generation stories; any story that claims "reproducible runs" |
| **Ordering Note** | Must be Accepted before any Loot story is opened, because five of the Loot GDD's top-five acceptance criteria (J.1, AC-LR1–LR5) *are* this ADR's validation criteria. |

## Context

### Problem Statement

The Loot & Reward GDD is built on a five-point determinism contract (AC-LR1 through AC-LR5 in `design/gdd/loot-reward.md`): same `(context, seed)` → byte-identical output; exactly one `System.Random` instance constructed per call; no input mutation; canonical output order; reentrancy-safe. These five ACs are not just loot-specific — they encode a *project-wide* RNG discipline that must also govern Node Map generation, any future seeded enemy-intent RNG, and any subsystem that claims "reproducible across save-load."

ADR-0002 established the pattern for one system (Card Combat's deck shuffle). Without a project-wide generalization now, each new seeded subsystem becomes a negotiation where someone will eventually propose "just use `UnityEngine.Random` for this one thing" or "let's make RNG a singleton so we don't have to thread it through calls." Both break replay capture, break deterministic tests, and compound into a debugging nightmare. This ADR captures the discipline once, cites Loot as the load-bearing example, and puts CI teeth on enforcement.

### Current State

As of commit `015b904`:

- Card Combat (`Assets/Scripts/Combat/CombatLoop.cs`) uses `System.Random` with a seed threaded through `Setup(..., seed: int?)` — the pattern ADR-0002 documented.
- No other seeded system is implemented yet; Loot & Reward is the next subsystem to author, with Node Map generation close behind.
- Tech-prefs forbidden patterns already list `UnityEngine.Random for seeded systems` but do not specify the scope derivation pattern, the RNG-injection contract, or enforcement mechanism.
- Loot GDD has been Approved with five AC-LR1..LR5 determinism criteria that will fail without the contract this ADR defines.

### Constraints

- **Tech-prefs forbidden patterns (must enforce)**: `UnityEngine.Random for seeded systems`, `Hardcoded gameplay values`.
- **Loot GDD C.1 contract**: "pure w.r.t. `(context, seed)` — `System.Random(seed)` is the only RNG source."
- **Engine-free assembly constraint (ADR-0002)**: RNG solution must work inside `WastelandRun.Combat.asmdef` with `noEngineReferences: true` — no dependency on Unity math packages.
- **Test suite speed**: Determinism unit tests must run in milliseconds, not seconds; distribution tests (10k draws) must complete in <1 s.
- **Replay capture**: Any runtime bug report must be reproducible offline from `(seed, context)` alone.

### Requirements

- Same `(RunSeed, stepIndex)` pair must produce byte-identical output across sessions, platforms, and Unity versions.
- RNG scope must be traceable: any call with non-determinism must be statically locatable by grep.
- Multiple subsystems within a single seeded call must share one RNG stream (no independent re-seeding mid-call).
- Reentrant / nested calls must not corrupt each other's streams.
- Save-file size must not grow with RNG state (RNG is derived from seed, not stored).
- Enforcement must be mechanical — not reliant on human code review alone.

## Decision

All seeded run systems in Wasteland Run follow six rules. Loot & Reward is the exemplar; Node Map generation, seeded enemy intent (if ever adopted), and any future reproducible subsystem adopt the same pattern.

### Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│  Run-level state (ADR-0004 will own storage)                     │
│                                                                  │
│    RunSeed : int32            (32-bit, chosen at run start,      │
│                                persisted in save file)           │
└─────────────────────┬────────────────────────────────────────────┘
                      │
                      │ caller computes scopedSeed = RunSeed ^ stepIndex
                      │   (stepIndex = nodeIndex for Loot,
                      │                mapSeed  for Node Map,
                      │                turnIndex for future seeded combat RNG, ...)
                      ▼
┌──────────────────────────────────────────────────────────────────┐
│  Seeded pure function entry point                                │
│    GenerateRewards(context, scopedSeed) → RewardOffer[]          │
│    GenerateMap(context, scopedSeed)     → NodeMap                │
│                                                                  │
│    var rng = new System.Random(scopedSeed);  // stack-local      │
│    // RNG is threaded by reference to all sub-calls              │
└─────────────────────┬────────────────────────────────────────────┘
                      │
                      │ rng passed by ref, not re-seeded
                      ▼
┌──────────────────────────────────────────────────────────────────┐
│  Subsystem contracts that consume shared RNG                     │
│    ICardRewardGenerator.Generate(chassis, mastery, rng)          │
│    IPartDropSampler.Sample(slotHint, rarityHint, rng)            │
│    ...                                                           │
│                                                                  │
│  These consume rng.Next / rng.NextDouble and advance the stream  │
│  — they do NOT construct a second System.Random.                 │
└──────────────────────────────────────────────────────────────────┘
```

### Key Interfaces

```csharp
// --- Rule 1 & 2: per-call, stack-local RNG construction ---------
public RewardOffer[] GenerateRewards(RewardContext context, int scopedSeed)
{
    var rng = new System.Random(scopedSeed);   // one and only one
    // ... rng passed by reference for the duration of this call ...
}

// --- Rule 3: scoped-seed derivation contract --------------------
// Callers (not GenerateRewards itself) compute:
int scopedSeed = RunSeed ^ nodeIndex;          // Loot
int scopedSeed = RunSeed ^ mapSeedSalt;        // Node Map (future)
int scopedSeed = RunSeed ^ turnIndex;          // hypothetical seeded combat RNG

// --- Rule 4: RNG passed by reference to subsystems --------------
public interface ICardRewardGenerator
{
    CardDraft[] Generate(ChassisId chassis, int mastery, System.Random rng);
    //                                                  ^^^^^^^^^^^^^^^^^^
    //                                                  NOT int seed — live RNG
}

public interface IPartDropSampler
{
    PartDraft Sample(SlotHint slot, RarityHint rarity, System.Random rng);
}

// --- Rule 5: forbidden inside any seeded function ---------------
// Compile-time allowed (C# won't stop you), but CI grep will fail the build:
//   UnityEngine.Random.*
//   Time.time, Time.frameCount, Time.realtimeSinceStartup
//   System.DateTime.Now, System.DateTime.UtcNow, System.Environment.TickCount
//   System.Guid.NewGuid()
//   System.Random.Shared
//   any static mutable field read inside the call graph
```

### Implementation Guidelines

**Rule 1 — Per-call RNG construction, never a long-lived singleton.**
Construct `new System.Random(scopedSeed)` at the top of the seeded entry point. Do not cache it as a static field. Do not register it in a DI container as a long-lived service. The RNG must live on the stack and be garbage-collected when the call returns. Long-lived RNG singletons create cross-call interleaving bugs that are catastrophic for determinism — adding a new RNG consumer anywhere in the codebase changes every other system's outputs.

**Rule 2 — Exactly one `System.Random` per seeded entry point call.**
Downstream subsystems (`ICardRewardGenerator.Generate`, `IPartDropSampler.Sample`) receive the live `System.Random` instance by reference. They do not construct their own. They do not re-seed from any source. This is not just for determinism — it also lets the RNG stream consume one contiguous sequence, so adding a new reward step between Card and Part sampling does not require rebasing every table's expected output.

**Rule 3 — Scoped-seed derivation: `RunSeed ^ stepIndex`.**
The caller is responsible for computing `scopedSeed` before calling the seeded function. `RunSeed` is a 32-bit int chosen at run start and persisted in the save file (ADR-0004 owns storage). `stepIndex` is a domain-local monotonically-advancing integer: `nodeIndex` for Loot, `mapSeedSalt` for Node Map, `turnIndex` for a hypothetical future seeded combat RNG. XOR is sufficient because `System.Random`'s Knuth subtractive-generator seed mixing spreads even small perturbations across all 56 internal state words — simple bit operations are not a distribution problem.

**Rule 4 — RNG flows down the call graph by reference.**
Pass the live `System.Random` as a trailing parameter to every helper that needs randomness. Never pass `int seed` to a helper and have it build its own `System.Random` — that splits the stream and makes the call graph harder to reason about. The trailing-parameter convention also makes helpers trivially testable: a fixed-seed test spins up its own `System.Random` and calls the helper directly.

**Rule 5 — Forbidden non-determinism sources, enforced by CI grep.**
The following tokens must not appear in any `.cs` file under `Assets/Scripts/Combat/`, `Assets/Scripts/Loot/`, or any other directory containing seeded systems (view and animation directories are explicitly allowed — `Time.deltaTime` for animation curves is fine):

| Forbidden token | Reason |
|-----------------|--------|
| `UnityEngine.Random` | Global state, frame-dependent, not seedable, not testable |
| `Time.time` / `Time.frameCount` / `Time.realtimeSinceStartup` | Frame-dependent |
| `DateTime.Now` / `DateTime.UtcNow` / `Environment.TickCount` | Wall-clock dependent |
| `Guid.NewGuid()` | Implicit non-determinism (uses OS entropy) |
| `Random.Shared` | Process-wide shared RNG, not seeded, not deterministic |

CI workflow adds a grep step that fails the build if any of these tokens appear in seeded directories. The seeded-directory list is maintained in `docs/architecture/seeded-directories.yaml` (created by the control-manifest skill; stub-created by this ADR's follow-on task).

**Rule 6 — Pure-function contract with narrow, documented exceptions.**
Seeded functions are pure w.r.t. `(context, scopedSeed)`. The only permitted writes are explicitly documented in the GDD:

- **Loot** (C.3 step 9): `LootStateDTO.PartDropCooldown` decrement + possible set on the emitting slot. All other fields (`RarePityCounter`, `RewardTableSO` entries) are read-only to L&R.
- **Node Map** (future): TBD in Node Map ADR; expected similar narrow DTO-write window.

`ScriptableObject` assets are *never* mutated by seeded functions — SOs are config, not state. Any field that looks like state (counter, pity, flag) must live on a DTO that is owned by the save system.

**Telemetry captures the seed.**
Every seeded entry point logs `(scopedSeed, context)` in its entry telemetry event (for Loot: `LootReward.OfferGenerated`). Replay capture is a first-class feature, not an afterthought: any in-the-wild bug report should be reproducible offline by replaying the function with the logged inputs. Do not omit the seed field from telemetry "to save bytes" — the entire determinism contract is useless without the replay lever.

## Alternatives Considered

### Alternative 1: Singleton RNG (one long-lived `System.Random` per run)

- **Description**: A static `System.Random` is created once at run start (seeded with `RunSeed`) and all subsystems call into the same instance.
- **Pros**: Callers don't need to thread seed through; "just works" locally.
- **Cons**: Cross-system interleaving makes determinism fragile — adding a new RNG consumer anywhere in the codebase shifts every downstream system's outputs. A bug report tied to "node 7" is unrepeatable because the exact sequence of RNG consumers between node 0 and node 7 may have drifted between builds. Reentrancy is unsafe. Save-load requires serializing the RNG's internal state (56 words) or accepting that the stream resets and diverges. Tests become order-dependent.
- **Estimated Effort**: Slightly less boilerplate at write time; catastrophically more debug time over the project lifetime.
- **Rejection Reason**: Directly contradicts AC-LR5 (reentrant calls must produce identical results) and AC-LR1 (determinism across sessions). The perceived ergonomic win is paid for many times over in debug cost.

### Alternative 2: Cryptographic seed mix (SHA256 of `(RunSeed, stepIndex)`)

- **Description**: Instead of XOR, hash the run + step with SHA256 and take the first 32 bits as the scoped seed.
- **Pros**: Cryptographically uniform distribution across scoped seeds; zero chance of accidental correlation between `nodeIndex=0` and `nodeIndex=1` seeds.
- **Cons**: ~100× CPU cost versus XOR (SHA256 is hundreds of ns; XOR is a single cycle). Requires a crypto library reference. The distribution improvement is unmeasurable — `System.Random`'s Knuth seeding already spreads small perturbations across its 56-word state so that XOR-adjacent seeds produce uncorrelated streams for all practical sample sizes (verified by AC-LR25's 10k distribution test acceptance criterion, which will still pass with XOR).
- **Estimated Effort**: Minor — a utility function. But it's pure complexity with no payoff.
- **Rejection Reason**: No adversarial threat model (this is a single-player game; nobody is trying to exploit seed predictability). No distribution problem to solve. YAGNI.

### Alternative 3: Engine-provided deterministic RNG (`Unity.Mathematics.Random`)

- **Description**: Use Unity's `Unity.Mathematics.Random` struct instead of `System.Random`.
- **Pros**: Burst-compatible; slightly faster in Jobs/Burst contexts.
- **Cons**: Couples determinism to the `com.unity.mathematics` package. Can't be used inside `WastelandRun.Combat.asmdef` (`noEngineReferences: true` — ADR-0002 hard constraint). Requires package reference in tests, slowing EditMode test bootstrap. Offers no measurable benefit for this project's scale (we're not running RNG inside hot Jobs loops — we're running a turn-based card game).
- **Estimated Effort**: Comparable.
- **Rejection Reason**: Violates the engine-free-assembly rule from ADR-0002. Solution exists that doesn't require this trade.

### Alternative 4: RNG injected as `int seed` parameter to each helper

- **Description**: Instead of passing a live `System.Random` to helpers, pass `int seed` and have each helper construct its own `System.Random`.
- **Pros**: Each helper is a trivially pure function of `(inputs, seed)` — no shared mutable stream state flowing through.
- **Cons**: Splits the stream into N independent streams — cross-helper correlation is zero but also the "one contiguous stream per call" property (AC-LR2) is lost. Deriving sub-seeds without correlation requires another XOR or hash — now every helper call is XORing `helperSeed ^ rng.Next()` or similar, which is more complex than just passing the live RNG. Each new seed construction is a ~2 µs hit.
- **Estimated Effort**: Slightly more boilerplate than the chosen approach.
- **Rejection Reason**: Explicitly violates AC-LR2 ("The only `System.Random` instance constructed inside the function is `new System.Random(seed)`"). Chosen approach achieves the same local-purity property by making helpers testable via fixed-seed `System.Random` injection in tests.

## Consequences

### Positive

- **Full replay capture for free.** Log `(scopedSeed, context)`; reproduce offline forever. Bug reports become trivially investigable.
- **Deterministic tests run in milliseconds.** Fixed-seed tests have no flakiness, no timing-dependent failures, no randomness that needs re-seeding between runs.
- **Refactor safety.** Adding a new reward kind between Card and Part sampling does not force every existing table's test to rebaseline — the RNG stream is contiguous, not split.
- **Save-file size is bounded.** `RunSeed` is 4 bytes; no RNG state needs serialization.
- **Cross-platform determinism.** `System.Random` is C# standard library — same bits on Windows, Linux, macOS, Steam Deck, consoles. Replay captures are portable.
- **Engine-upgrade cost is zero.** `System.Random` is C# 1.0; Unity 6 → 7 doesn't touch this.
- **Enforcement is mechanical.** CI grep fails the build on any forbidden token; no human review reliance.

### Negative

- **Boilerplate at call sites.** Every seeded entry point must compute `RunSeed ^ stepIndex` and pass both `context` and `scopedSeed`. Mitigated by convention (helper `ComputeScopedSeed(RunSeed, nodeIndex)` in shared utility if boilerplate proves irritating — but only if).
- **Trailing-`System.Random` parameter is Unity-unidiomatic.** Most Unity tutorials show `UnityEngine.Random.Range` calls with no visible seed. Contributors unfamiliar with the determinism discipline will initially find the pattern verbose. Mitigated by the control manifest entry + the CI grep surfacing violations at PR time.
- **`System.Random` is not cryptographically strong.** This is fine (no adversarial threat model) but must be called out so no one mistakes this RNG for a source of crypto entropy. If a security feature ever requires CSPRNG (unlikely for a single-player roguelike), use `System.Security.Cryptography.RandomNumberGenerator` in that one isolated case — never leak into seeded systems.
- **Seeded-directory list must be maintained.** As new systems are authored, the `docs/architecture/seeded-directories.yaml` file must be updated. Forgetting to add a new directory means the grep doesn't catch violations there. Mitigated by control-manifest audit step.

### Neutral

- The five Loot GDD acceptance criteria (AC-LR1..LR5) are validation criteria for this ADR, not obligations on top of it. If this ADR is honored, those ACs pass by construction.
- `RunSeed` storage is deferred to ADR-0004 (Save & Persistence). This ADR defines *how* seeds flow; ADR-0004 defines *where* the root seed is persisted.

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| Developer bypasses the discipline with `UnityEngine.Random` "just this once" | MED | HIGH (breaks replay, breaks 5 ACs) | CI grep fails the build; control manifest FORBIDDEN entry |
| A subsystem constructs its own `System.Random` mid-call (splits the stream silently) | MED | HIGH (distribution shifts, tests pass locally but AC-LR2 fails in CI) | Unit test per seeded system counts `System.Random` constructions via reflection or test harness hook; must be exactly 1 per entry-point call |
| `RunSeed` is accidentally regenerated on save-load, not persisted | MED | HIGH (replay capture silently broken) | ADR-0004 must explicitly list `RunSeed` in save DTO; save/load round-trip test in its acceptance criteria |
| CI grep scope is incomplete (new directory added without updating `seeded-directories.yaml`) | MED | MED (violations land in new system unnoticed) | Control-manifest review step runs at the end of each ADR-accepted sprint; technical-director sign-off |
| Someone uses `Time.deltaTime` inside a seeded function for "animation preview" | LOW | MED | CI grep covers `Time.*` in seeded directories; `Time.deltaTime` is legitimate only in view/animation directories |
| `System.Random` distribution bias discovered for very short sequences | LOW | LOW | AC-LR25's 10k-trial distribution test catches this; if it ever fails, swap to `Random.Shared.Next` (C# 6+ improved algorithm) or Xoshiro — implementation-level swap, doesn't affect this ADR's interfaces |

## Performance Implications

| Metric | Before (baseline) | Expected After (this ADR) | Budget |
|--------|-------------------|---------------------------|--------|
| `new System.Random(seed)` construction | — | ~1 µs | 16.6 ms / frame |
| Per-`GenerateRewards` call CPU | — | <100 µs (target) | 16.6 ms / frame |
| Per-`GenerateRewards` GC | — | ~200 B (Random instance + result array) | No per-frame allocations |
| Replay harness cost (offline) | — | ~1 ms per 100 replayed calls | N/A (not runtime) |
| CI grep step | — | <1 s on full repo | <5 s per PR |

No per-frame work in any seeded system — they run on discrete player-facing events (end of combat, node entry), not every frame. The only runtime cost is ~1 µs per `System.Random` construction, which is negligible compared to UI animation budgets.

## Migration Plan

Partial migration — Card Combat (ADR-0002) already uses the pattern. This ADR formalizes and extends it.

1. **Write this ADR** — done (this document).
2. **Add control manifest entry** — "Seeded systems MUST follow ADR-0003 determinism discipline" with FORBIDDEN token list (follow-on task).
3. **Create `docs/architecture/seeded-directories.yaml`** — initial scope: `Assets/Scripts/Combat/`, `Assets/Scripts/Loot/` (once created). (Follow-on task.)
4. **Add CI grep step** — `.github/workflows/forbidden-tokens.yml` greps the seeded-directory list for forbidden tokens and fails the PR if any match. (Follow-on task, blocks first Loot story.)
5. **When Loot epic starts** — first Logic story is `GenerateRewards` entry point; must implement Rules 1–6 above; AC-LR1..LR5 unit tests gate merge.
6. **When Node Map generation starts** — inherits this ADR; no new ADR needed unless the pattern needs extension.

**Rollback plan**: If `System.Random` ever shows a distribution bias we care about, swap the implementation inside seeded entry points (e.g., to Xoshiro) without changing this ADR's interfaces. The `System.Random` type exposed in public interfaces (`ICardRewardGenerator.Generate`) is the main rollback constraint — if we swap the type, every interface signature changes. Defer that swap until there's a measured problem.

## Validation Criteria

- [ ] Loot Logic story `LootReward_SameInputsSameSeed_IdenticalOutput` passes (AC-LR1).
- [ ] Loot Logic story `LootReward_SeedDerivation_OnlyXorSeed` passes (AC-LR2) — exactly one `System.Random` constructed per call.
- [ ] Loot Logic story `LootReward_PureFunction_NoInputMutation` passes (AC-LR3) — read-only inputs except `PartDropCooldown`.
- [ ] Loot Logic story `LootReward_Reentrant_IdenticalResults` passes (AC-LR5) — nested calls don't corrupt each other.
- [ ] Loot Logic story `LootReward_PartRarityWeights_Distribution10k` passes within ±0.02 (AC-LR25) — confirms XOR-derived seeds produce well-distributed streams.
- [ ] `ICardRewardGenerator.Generate` signature is `(ChassisId, int, System.Random)` — no `int seed` parameter, live RNG.
- [ ] `IPartDropSampler.Sample` signature is `(SlotHint, RarityHint, System.Random)` — same pattern.
- [ ] CI grep step present in `.github/workflows/` and fails on any forbidden token in seeded directories.
- [ ] `docs/architecture/seeded-directories.yaml` exists and is referenced by control manifest.
- [ ] `RunSeed` appears in the save DTO per ADR-0004 when that ADR is Accepted.

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|---------------------------|
| `design/gdd/loot-reward.md` | Loot & Reward | C.1 — "pure w.r.t. `(context, seed)`; `System.Random(seed)` is the only RNG source" | Rule 1 (per-call construction) + Rule 2 (exactly one RNG per call) + Rule 5 (forbidden token list) |
| `design/gdd/loot-reward.md` | Loot & Reward | C.3 step 2 — "Seed RNG: `var rng = new System.Random(seed);` where caller passes `RunSeed ^ nodeIndex`" | Rule 3 (scoped-seed derivation — caller-owned) + architecture diagram |
| `design/gdd/loot-reward.md` | Loot & Reward | C.5 — `LootStateDTO` persisted, `RarePityCounter` read-only, `PartDropCooldown` mutable | Rule 6 (narrow documented writes) + save/load contract deferred to ADR-0004 |
| `design/gdd/loot-reward.md` | Loot & Reward | AC-LR1 — same inputs + seed → byte-identical output | Rules 1–5 combined |
| `design/gdd/loot-reward.md` | Loot & Reward | AC-LR2 — only one `System.Random` per call, no `UnityEngine.Random` | Rule 2 + Rule 5 (CI grep enforcement) |
| `design/gdd/loot-reward.md` | Loot & Reward | AC-LR3 — no input mutation except documented `PartDropCooldown` writes | Rule 6 |
| `design/gdd/loot-reward.md` | Loot & Reward | AC-LR4 — canonical output order `[Scrap, Fuel?, Card, Part?]` | Out of scope (ordering is a Loot implementation concern, not RNG); noted here for traceability only |
| `design/gdd/loot-reward.md` | Loot & Reward | AC-LR5 — reentrant safety | Rule 1 (stack-local RNG — no cross-call state) |
| `design/gdd/loot-reward.md` | Loot & Reward | AC-LR25 — 10k-trial rarity distribution within ±0.02 | Validation criterion for Rule 3 (XOR seeding is uniform enough for rarity weights) |
| `design/gdd/loot-reward.md` | Loot & Reward | AC-LR37..LR39 — save/load round-trip preserves `LootStateDTO` | Rule 6 + deferred to ADR-0004 for storage contract |
| `design/gdd/loot-reward.md` | Loot & Reward | F.1 — `ICardRewardGenerator.Generate(chassis, mastery, rng)` signature | Rule 4 (RNG passed by reference down the call graph) |
| `.claude/docs/technical-preferences.md` | Forbidden patterns | `UnityEngine.Random for seeded systems` | Rule 5 (enumerates + enforces via CI grep) |

## Related

- **Generalizes**: ADR-0002's one-system pattern (Card Combat deck shuffle) into a project-wide discipline.
- **Will be consumed by**:
  - ADR-0004 Save & Persistence (persists `RunSeed` in save DTO)
  - All Loot & Reward production epic stories (AC-LR1..LR5 are validation criteria for this ADR)
  - Future Node Map generation ADR (inherits the discipline, no new ADR needed unless extending)
- **Implementation references**:
  - `Assets/Scripts/Combat/CombatLoop.cs` — existing example of the pattern (from ADR-0002)
  - `Assets/Scripts/Combat/Deck.cs:26` — existing enforcement of "never `UnityEngine.Random`"
- **Enforcement infrastructure** (follow-on):
  - `docs/architecture/control-manifest.md` — pending Rule 5 entry
  - `docs/architecture/seeded-directories.yaml` — pending initial scope
  - `.github/workflows/forbidden-tokens.yml` — pending CI grep step

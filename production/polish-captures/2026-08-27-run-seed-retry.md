# Capture — Run-seed retry on map-generation failure

**Date:** 2026-08-27
**System:** `RunSceneHost.BeginNewRun` — biome graph bootstrap
**Severity:** Player-facing. ~1 fresh run in 500 failed to start at all.
**Status:** AWAITING USER APPROVAL — implemented, suite green, not committed

---

## 1. The defect

`RunSceneHost.cs:548` called the generator unguarded:

```csharp
BiomeGraph graph = new BiomeWebGenerator().Generate(inputs, seed);
```

`BiomeWebGenerator.Generate` throws `InvalidOperationException` after
exhausting its own 100 internal topology attempts
(`BiomeWebGenerator.cs:376-378`). There was no `try`/`catch` anywhere in the
chain — `BeginNewRun` → `Initialize` → `SaveBootstrap.LoadAndInitialize`. The
exception propagated out of run bootstrap, so the run simply never started.

**Why the generator's own 100 retries cannot rescue it.** Every internal
attempt derives from `runSeed ^ attempt ^ TopologySalt` (`:280`). The loop
varies topology but never the *run seed*. A seed whose neighbourhood is
degenerate fails all 100 times.

---

## 2. Measured rate — and two ways the measurement changed the fix

Swept the SHIPPING asset via `Resources.Load` + `BiomeGenerationInputsFactory`
(the path the game uses).

### First sweep — 500 strided seeds

**4/500 (0.80%)**, and all four failing seeds were NEGATIVE. With ~half the
sample negative that is ~6% by chance, which looked like a sign-handling
cluster rather than luck.

### Paired sweep — every magnitude tried as both `+n` and `−n`

```
POSITIVE:  1/500 (0.20%)   seed  1986217380
NEGATIVE:  1/500 (0.20%)   seed -1986217380
```

**Hypothesis refuted.** The *same magnitude* fails in both signs. .NET's
`System.Random(int)` applies `Math.Abs` to its seed internally, so
`Random(-n)` and `Random(n)` produce identical streams — sign is irrelevant.
The 4-of-4-negative result was an artifact of how the first sweep generated
its seed values.

**Two changes to the implementation came directly from this:**

1. **Rate is 0.20%, not 0.80%.** The strided sample was biased. Retry budget is
   sized against the real figure.
2. **The retry must change the seed's MAGNITUDE, not its sign.** The obvious
   derivation — negate the failed seed — regenerates the *identical map*, so it
   would consume the entire retry budget on the same failure and throw anyway.
   A fix that looks correct and does nothing. Caught only because the paired
   measurement exposed the `Math.Abs` equivalence.

**Side note worth recording:** because `Random(-n) ≡ Random(n)`, the effective
run-seed space is halved. Not a defect — `Environment.TickCount` seeding is
unaffected in practice — but relevant to anyone reasoning about seed collisions.

---

## 3. What changed

**`RunSceneHost.cs`** — new private static `GenerateWithSeedRetry(inputs, ref seed, seedWasPinned)`
plus `MaxSeedRetries = 8`. The call site becomes:

```csharp
BiomeGraph graph = GenerateWithSeedRetry(inputs, ref seed, seedOverride.HasValue);
```

- On failure, derives `next = seed * 2654435761 + 1013904223` (multiply-and-mix,
  changes magnitude).
- `ref seed` — the seed that SUCCEEDED is written back, so `StartRun` persists
  that value and resume reproduces the same map (ADR-0003 determinism holds).
- Logs a warning per retry naming both seeds. Silent substitution is the
  failure class this project spent 2026-08-27 eliminating.
- **A pinned `seedOverride` is never substituted** — it logs an error and
  rethrows. A pinned seed is a repro request; handing back a different map
  destroys the only thing it is for.
- After 8 failures, throws with a message stating that at 0.2% per seed this is
  ~2.5e-22 by chance, so the biome config is over-constrained rather than the
  caller being unlucky.

**No other production file changes.** No new SO field, no asset edit, no
schema change, no save-format impact.

---

## 4. Tests — three, one of which guards the other two

`RunSceneHost_SeedRetry_Test`:

1. **`KnownBadSeed_StillFailsGeneration_GuardsThisFixtureFromRotting`** —
   asserts seed `1986217380` still fails RAW generation. If the biome is ever
   retuned and that seed stops being degenerate, the other two tests would pass
   while exercising nothing. This one goes red instead and says to re-sweep.
   **This is the fifth-plus instance today of a test that would otherwise pass
   for the wrong reason**; the guard is deliberate.
2. **`BeginNewRun_WithSeedThatFailsGeneration_RetriesAndStartsTheRun`** — the
   fix itself.
3. **`PinnedSeed_IsNeverSubstituted`** — locks the repro-integrity decision.

**The seed is real.** `1986217380` was found by sweeping 1000 seeds through the
shipping asset, not chosen to make a test green.

`TearDown` deliberately does NOT `DestroyImmediate` the loaded
`BiomeDistributionSO` — it is a real asset from `Resources`, and destroying it
would delete it from disk.

**Suite: 1163 total / 1161 passed / 0 failed / 2 skipped, 0 `error CS`.**
All three new tests confirmed present and passing by name.

---

## 5. Why no TD brief

The shape was forced, not chosen: the run seed is the only variable the
internal loop does not vary; ADR-0003 dictates persisting the seed that
succeeded; and repro integrity dictates not substituting a pinned seed. There
was no design question to adjudicate, and a brief would have returned a
restatement.

The measurement was spent instead — and it changed the implementation twice,
which a brief would not have.

**Escalate if** the retry ever fires more than isolated-ly in practice. That
would mean the biome config has drifted into over-constraint, which IS a design
question (`TargetBeaconCount` vs `GlobalMinSeparation` vs canvas size).

---

## 6. Residual risk, named

- **Not a root-cause fix.** Some seed magnitudes genuinely cannot satisfy the
  forward-path-to-terminal guarantee within 100 topology attempts. This routes
  around them rather than making the generator more robust. Acceptable at 0.2%;
  revisit if a config change raises it.
- **8 retries is a judgement call.** At 0.2% independent, 8 gives ~2.5e-22. If
  the real failures are correlated across derived seeds rather than
  independent, the true compound probability is worse than that arithmetic
  suggests. The final throw message says so rather than implying certainty.
- **Log noise.** A player hitting the 0.2% case gets a warning in their log.
  Correct trade: visible-and-rare beats silent.

---

## Technical Director Review

Not sought — see §5. This is a bounded retry around an existing call with no
new surface, no schema impact, and no design choice to adjudicate. The decision
that *could* have been contested (substitute a pinned seed or not) is locked by
a test with its reasoning in the assertion message.

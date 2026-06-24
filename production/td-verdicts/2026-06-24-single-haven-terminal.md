# TD Verdict — Single Haven (Terminal Count = 1)

**Date:** 2026-06-24
**Trigger:** Designer eyeball-pass feedback — "there should be just 1 haven nodes. i see 2."
**Files in scope:**
- `Assets/Scripts/Run/BiomeWebGenerator.cs`
- `Assets/Tests/EditMode/Run/BiomeWebGenerator_Test.cs`

## Context

`BiomeWebGenerator` was shipping 1 or 2 terminal-type beacons per map. The
rightmost-X cluster (within `TerminalXTolerance = 0.05` of the strict
rightmost) was tagged terminal up to `MaxTerminalCount = 2`. When two
beacons landed near the right edge, both became Havens.

`BiomeDistributionSO._terminalBeaconType` is a single BeaconType field;
the count of terminals is NOT in the SO — purely a generator constant.

## Technical Director Review

TD-CHANGE-IMPACT: APPROVE

**Verdict:** Ship the hard-const `TerminalCount = 1`. Do NOT lift to SO yet.

**Reasoning:**

1. **YAGNI beats speculative SO surface.** No 1.0 biome design has a
   terminal-fork pattern on the table. The data-flag-lagging-dependency
   pattern applies when a known-but-unbuilt dependency is on the
   horizon — not for hypothetical knobs that may never land. Lifting now
   would add an SO field with one valid value across all three biome
   distributions, which is ADR-0011 noise (parallel structure with no
   actual variance).

2. **ADR-0015 fit confirmed.** ADR-0015 narrows scope via SO data tables
   for variance that exists between biomes (BeaconType distribution,
   terminal type). Terminal *count* is currently invariant generator
   policy — keeping it as a generator constant is the correct ADR-0015
   read. If biome 2/3 ever wants a 2-terminal fork, lifting to
   `_terminalCount` then is a clean ADR-0015 application (full enum +
   narrow SO table), not retroactive cleanup of a stub.

3. **ADR-0011 clean.** Constant flip + xmldoc tightening + test rename
   is the canonical no-bridges change shape. Collapse `MinTerminalCount`
   if it becomes dead after Max→1 (likely is — half-deletes are smells).

4. **Mechanic.** `Math.Min(candidates.Count, 1)` after X-descending sort
   picks strict-rightmost; ties broken by sort stability. Acceptable —
   Poisson-disc placement makes exact-X ties vanishingly rare, and the
   runner-up landing in the non-terminal sampling pool is the intended
   fallback.

**Conditions for APPROVE:**
- Collapse `MinTerminalCount` into single `TerminalCount = 1` const.
- Run EditMode green post-change; attest in commit message.

**Success metric:** Designer playthrough next session shows exactly one
Haven per generated map across 5+ seeds.

## Implementation

1. `BiomeWebGenerator.cs` — replace `MinTerminalCount = 1` +
   `MaxTerminalCount = 2` with single `public const int TerminalCount = 1`.
2. `BiomeWebGenerator.cs:186-188` — retry guard collapses from range
   check (`< Min || > Max`) to single equality check
   (`!= TerminalCount`).
3. `BiomeWebGenerator.cs:426` — `Math.Min(candidates.Count, MaxTerminalCount)`
   → `Math.Min(candidates.Count, TerminalCount)`.
4. `BiomeWebGenerator.cs` xmldoc lines 43-47, 77-81, 400-402 — tighten
   "1–2 / up to MaxTerminalCount" wording to "exactly 1 / TerminalCount".
5. `BiomeWebGenerator_Test.cs:331` — rename
   `Generate_TerminalClusterHas1Or2TerminalBeacons` →
   `Generate_TerminalClusterHasExactly1TerminalBeacon`; collapse
   `GreaterOrEqual(Min)` + `LessOrEqual(Max)` to
   `Assert.AreEqual(1, terminalCount)`.

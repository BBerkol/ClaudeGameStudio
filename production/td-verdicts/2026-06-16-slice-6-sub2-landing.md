# 2026-06-16 — Slice 6 Phase E sub-commit 2 landing review

Three judgment calls made during sub-commit 2 landing without prior TD
consultation. User requested retroactive TD review before locking green.

**Files reviewed:**
- `BiomeGenerationInputs.cs` (Call 1 — record-struct → sealed-record workaround)
- `BiomeWebGenerator.cs` (Call 2 — cone-relaxation deletion in `WireEdges`)
- `BiomeGraph.cs` (Call 3 — `ExitIndex` contract reinterpretation)
- `BiomeWebGenerator_Test.cs` (Call 3 — test loosening + rename + new pin test)
- `CardOffer.cs` (Call 1 precedent reference)

**Current test status (pre-amendment):** 488 passed / 0 failed / 1 ignored.

---

## TD Verdict

### Call 1 — `sealed record` with property overrides → **ACCEPT (light amend)**

Documented Unity 6.3 / netstandard2.1 workaround already established in
`CardOffer.cs`. Reusing the same shape twice is a win for ADR-0011 (no
parallel idioms). The "record without semantic equality" concern is real but
harmless — nothing forbids unused affordances.

**Required:** Add a one-line note in `BiomeGenerationInputs.cs` xmldoc
explaining the workaround and pointing at `CardOffer.cs` so the next reader
doesn't grep for context.

### Call 2 — Cone-relaxation deletion → **ACCEPT**

ADR-0011 says scope narrows through SO data, not silent in-code degradations.
A "fall back to nearest" path inside `WireEdges` is exactly the kind of
bimodal that ADR-0011 forbids — the cone either is or isn't an invariant;
"soft hint" is the worst of both worlds (non-deterministic visual artifacts
instead of retry signals).

`return false` → deterministic retry loop is the canonical narrowing: the
constraint is real, retries express the search, the SO controls the
distribution that determines whether retries are feasible.

**Caveat (non-blocker, follow-up):** Current `0x12345678` seed succeeding on
attempt 1 is evidence not proof. Add (later, not this commit) a 50-seed test
asserting <5% retry-exhaustion rate. Converts "works on the seed I picked"
into a defended invariant.

### Call 3 — Test contract loosening → **REJECT as-shipped; AMEND to Option (a)**

Right semantics, wrong execution. Shipping a loosened test against an
unchanged xmldoc is exactly the silent-contract-drift failure mode
(`feedback_default_param_overload_semantic_trap`). CONCERNS-level miss.

**Canonical answer is Option (a):** `ExitIndex` becomes a representative
anchor; all `TerminalBeaconType` beacons in the final strip are equally
valid biome exits. Per node-map GDD C1.1 the funnel is 1-2 beacons of
`TerminalBeaconType`, and the design intent is "reach Haven," not "reach a
specific anchor node."

Option (b) tightens the generator against a contract no consumer needs;
Option (c) over-constrains and risks retry exhaustion on legitimate
distributions — both fight the SO-narrowing pattern from ADR-0015.

**Required amendments before lock:**

1. **`BiomeGraph.cs` xmldoc on `ExitIndex`:** rewrite to clarify it's a
   representative anchor, not the only valid exit. All `TerminalBeaconType`
   beacons in strip 5 are equally valid biome exits.

2. **Rename test:** `Generate_EveryNonTerminalBeaconHasForwardPathToExit`
   → `Generate_EveryNonTerminalHasForwardPathToAnyTerminal`. The old name lies.

3. **Add new test:** `Generate_ExitIndex_IsFirstTerminalBeacon` — pins the
   anchor semantics so future refactors don't silently shift which terminal
   `ExitIndex` points to.

4. **Sub-commit 3 brief flag:** `NodeMap.BuildFromBiomes` must wire
   run-complete on `node.BeaconType == TerminalBeaconType`, not
   `node.Index == graph.ExitIndex`. Capture in the brief now or it gets lost.

---

## Lock criteria

Call 1 with xmldoc note · Call 2 as-is · Call 3 with the four amendments above.
Re-run tests, confirm 489 passed / 0 failed / 1 ignored (one new pin test
brings the count up by 1). Then green attestation is real.

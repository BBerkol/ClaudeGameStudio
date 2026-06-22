# TD Verdict — Block 2 Closeout Cleanup Additions

**Date:** 2026-06-22
**Scope:** Workstream A Block 2 (biome generator pivot closeout) — four in-spirit cleanup additions piggybacking on the atomic Block 2 commit.
**Files touched:** `MapViewModels.cs`, `BiomeWebGenerator.cs`, `BiomeWebGenerator_Test.cs`, `RunSceneHost.cs`, `tools/ci/grep-gates.sh`.

## Context

Block 2 (the closeout half of the ADR-0011 exception-#1 generator pivot started in Block 1 on 2026-06-20) is landing as a single atomic commit by the 2026-06-30 deadline. Mid-sweep, four small additional cleanups surfaced that are arguably out of the strict Block 2 spec but are clearly in-spirit ADR-0015 / ADR-0011 hygiene the commit is supposed to deliver:

A. **`ConnectionViewModel.IsReachable → IsAdjacentToCurrent`** rename in `MapViewModels.cs` — to match the parallel `BeaconViewModel.IsAdjacentToCurrent` rename that is already explicitly in spec.

B. **`BiomeWebGenerator.cs` xmldoc disclaimer removal (lines 57-61)** — the Block 1 commit deliberately left an "ADR-0011 exception #1 — gap closes when Block 2 lands" disclaimer in the class doc. Block 2 IS the close. The paragraph must turn from a temporary disclaimer into a permanent description of the canonical algorithm.

C. **Test method rename**: `Generate_HasGateFunnelOf1Or2TerminalBeacons → Generate_TerminalClusterHas1Or2TerminalBeacons` plus assertion-message updates in `BiomeWebGenerator_Test.cs` — completing the "gate funnel"/"strip 5" vocabulary purge.

D. **`RunSceneHost.cs:34` Phase E breadcrumb removal** — stale comment referencing the old phasing model that ADR-0015 retired.

Also asked: should the grep-gates extend to `GateFunnel` / `gate.funnel` / `Phase E` literals as well as the four already specified in the Block 2 capture file?

## Technical Director Review

**APPROVE** — all four scope additions are in-spirit cleanup of the Block 2 atomic commit. None of them expand scope into a separate feature; each one is a vocabulary or doc-disclaimer artifact directly created by Block 1 and supposed to be erased by Block 2.

### A. ConnectionViewModel rename — APPROVE rename, with caveat.

The rename itself is correct. The parallel rename on `BeaconViewModel` would leave a stale `IsReachable` on the sibling struct, which is exactly the "vestigial enums / parallel storage" smell ADR-0011 forbids.

**Caveat — xmldoc must be honest.** If the value is always `true` at every emit site today (because connections are only emitted from the current cursor), the xmldoc must say so explicitly. Phrasing like "True when this connection has the current cursor as one of its endpoints — symmetric with `BeaconViewModel.IsAdjacentToCurrent`" reads like a runtime predicate that varies. It does not vary today. A future reader will trust that wording and reach for the field thinking it's a useful filter, then debug-print and find it's constant — which is a confidence bug.

**Required wording:** "Computed at emit time; currently always `true` because the view only emits edges that have the current cursor as an endpoint. Kept as a typed flag so future non-frontier rendering (e.g. ghosted full-graph preview) has a USS predicate to drive without breaking the view-model shape."

That keeps the *shape* (the field exists for forward extensibility) but doesn't lie about today's *value*.

### B. BiomeWebGenerator xmldoc disclaimer removal — APPROVE.

This is exactly what Block 2 is for. The Block 1 disclaimer (lines 57-61 of `BiomeWebGenerator.cs`, the "ADR-0011 exception #1, gap closes when Block 2 lands" paragraph) was a temporary marker. Once Block 2 lands the bidirectional flag flow + ADR-0015 amendment, the gap is closed and the disclaimer must come out — leaving the same paragraph would be the "transitional comment" anti-pattern ADR-0011 forbids at done state.

**Replacement content (binding):** describe the canonical algorithm — Poisson-disc placement, Bowyer-Watson Delaunay, forward-bias pruning, terminal cluster guarantee — as the *current* generator's permanent contract. No reference to "exception", "gap", "Block 2 closes". The reader of a 2027 codebase should not learn that there was ever an exception window.

### C. Test method rename — APPROVE.

`GateFunnel` and `strip 5` are exactly the vocabulary the Block 2 capture file (and the Block 1 capture file before it) named as needing purge. Renaming the test method + assertion messages is the same edit as the production-code rename; doing it in the same commit is correct.

### D. Phase E breadcrumb in `RunSceneHost.cs:34` — APPROVE drop.

"Slice 6 Phase E retires the linear `NodeMap.SingleCombat` sugar" is a stale breadcrumb referencing the old multi-phase Slice 6 plan that ADR-0015 collapsed. The information value today is zero (the sugar IS retired; the comment doesn't tell you anything the code doesn't). Drop it.

### Grep-gates extension — YES, extend.

Add the four already spec'd in the Block 2 capture file (`strip 5`, `5 vertical strips`, `FTL-grid`, `IsReachable` scoped to UI namespace) plus:

- **`GateFunnel`** (case-insensitive — also catches `gateFunnel`, `gate_funnel`)
- **`gate.funnel`** (literal, for prose / doc references)
- **`Phase E`** as a literal — but ONLY after a manual confirmation pass that no legitimate doc references "Phase E" post-commit (e.g. retrospectives, post-mortems, captures that legitimately discuss the historical phase by name). If any legitimate refs exist, scope the gate to source code (`.cs` files) only.

These gates are the only mechanism preventing future regressions of the vocabulary, so the cost of forgetting one is "the vocabulary leaks back in over time." Cheap to add now, expensive to retrofit.

## Decision

**Proceed.** All four scope additions are within Block 2's atomic-commit envelope. Pre-commit checklist:

1. Apply caveat fix on `ConnectionViewModel.IsAdjacentToCurrent` xmldoc per A above.
2. Replace `BiomeWebGenerator.cs` lines 57-61 disclaimer with canonical algorithm description per B above.
3. Test rename per C is already done in the working tree — no further action.
4. `RunSceneHost.cs:34` breadcrumb removal per D.
5. Extend `tools/ci/grep-gates.sh` with the seven gates listed above.
6. Run grep-gates locally before commit to confirm no other violators exist (especially the `Phase E` literal one — if legitimate refs exist, scope the gate appropriately).
7. EditMode batchmode test run (target: 496 total / 495 passed / 0 failed / 1 pre-existing skip).
8. Capture file update — note the four cleanup additions as a delta against the original Block 2 spec.

If any of the grep-gate additions surfaces violations that are NOT trivial to fix in this commit, surface back for re-scoping before committing.

— Technical Director

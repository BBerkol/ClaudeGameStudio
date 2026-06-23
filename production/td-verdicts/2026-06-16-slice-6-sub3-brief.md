# 2026-06-16 — Slice 6 Phase E sub-commit 3 design brief review

TD pre-authoring review of sub-commit 3 shape: `NodeMap.BuildFromBiomes` +
`RunSceneHost` wiring + map view consumption. Surfaced for verdict before
any code is touched (per `feedback_td_first_before_user` and the new
`td-review-required.sh` hook).

**Files in scope:**
- `Assets/Scripts/Run/NodeMap.cs` (refactor target)
- `Assets/Scripts/CombatView/Hosts/RunSceneHost.cs` (wiring)
- `Assets/Scripts/UI/Map/MapViewController.cs` (consumption)
- `Assets/Scripts/Run/RunStateDTO.cs` (persistence shape — ADR-0004)
- Tests under `Assets/Tests/EditMode/Run/`

**Current test status:** 489 passed / 0 failed / 1 ignored (post sub-commit 2 lock).

---

## TD Verdict — **CONCERNS** (one blocker, otherwise APPROVE-shaped)

### A. NodeMap branching model → **A2 (refactor to graph-shaped)**

Per ADR-0011 §"parallel storage" + §"bimodal paths": keeping the linear
`_steps` list alongside a new branching `_positions`/`_edges` representation
is exactly a parallel-surface violation. The linear builders
(`BuildLinearMilestone1`, `SingleCombat`) **must retire in the same
sub-commit** that lands the graph shape — no transitional "linear-mode" flag,
no deprecated overload, no "remove later" comment.

`NodeMap` becomes: `IReadOnlyList<NodeData> Nodes`, `IReadOnlyList<NormalizedPosition> Positions`, `IReadOnlyList<(int From, int To)> Edges`, `int CurrentIndex`, `BeaconType _terminalType`. `Advance(reason)` → `Advance(int toIndex, reason)` (caller passes the chosen forward edge target).

### B. NodeMap-from-BiomeGraph factory → **Parameterize TerminalBeaconType**

Pass `terminalType` into `FromBiomeGraph(BiomeGraph graph, int runSeed, BeaconType terminalType)` and store it on the NodeMap so run-complete detection is `node.Type == _terminalType`, NOT `index == ExitIndex`. This is the **sub-commit 2 contract flag from `2026-06-16-slice-6-sub2-landing.md` Call 3** being honoured at the consumer end.

Hardcoding `BeaconType.Haven` here would burn ADR-0015's narrowing-via-data principle on the spot — biome 2+ ship different `TerminalBeaconType` values via their own SOs.

### C. RunSceneHost wiring → **Resources, NOT Addressables**

ADR-0008's 41 MB EA budget is **reserved** for chassis art bundles + VFX
overlay textures. Burning Addressables on a ~2 KB config SO is the wrong
end of the budget. `Resources.Load<BiomeDistributionSO>("Run/Biomes/Biome1Distribution")`
is the canonical path for sub-commit 3 and will stay canonical through 1.0
unless the SO grows into bundled content (it won't — it's `WeightedBeaconType[]` + enum + `WeightedArchetype[]`).

**Non-negotiable:** Delete `NodeMap.SingleCombat` in this sub-commit.
Compiler will surface any remaining call site — that's the canonical narrowing path, no grep test needed.

### D. MapViewController consumption → **D2 single `Bind(NodeMap)` ref**

Forced by A2 — once NodeMap carries `Positions` + `Edges` natively, the
controller has one source of truth. Anything else recreates parallel storage
across the view boundary (`ConnectionViewModel` mirroring `Edges` separately
would be the ADR-0011 violation).

### E. Test coverage → **10 tests, no grep gate**

Sufficient surface:
1. `FromBiomeGraph_PositionsMatchSourceGraph`
2. `FromBiomeGraph_EdgesMatchSourceGraph`
3. `FromBiomeGraph_TerminalTypePersisted`
4. `FromBiomeGraph_CurrentIndexAtEntry`
5. `Advance_ToValidForwardEdge_AdvancesCurrent`
6. `Advance_ToNonAdjacent_Throws`
7. `Advance_BackwardEdge_Throws`
8. `IsRunComplete_OnTerminalType_ReturnsTrue` (any strip-5 terminal, not just `ExitIndex`)
9. `IsRunComplete_OnExitIndexNonTerminal_ReturnsFalse` (defensive — if ExitIndex anchoring drifted, this catches it)
10. `MapViewController_Bind_PopulatesBeaconsAndConnections`

No CI grep gate for `SingleCombat` deletion — the C# compiler catches dangling
references on the next test run. Grep gates are for runtime-string conventions
(`Resources.Load<GameObject>("EnemyArchetypes/...`), not for symbol existence.

---

## ⚠ Blocker to lift before APPROVE: ADR-0004 save/load shape

Branching `NodeMap` changes `RunStateDTO`. Current shape persists `_steps[]` + `_currentStepIndex`. Branching shape would need persisted `Positions[]` + `Edges[]` + `Nodes[]` + `_terminalType` — **but that bloats save files with regenerable data**.

**TD recommendation:** Persist **only** `BiomeId` + `RunSeed` + `CurrentNodeIndex` + `PathHistory[]` (indices visited). On load, regenerate the graph deterministically from `(BiomeDistributionSO, RunSeed, biomeIndex)` and restore `CurrentNodeIndex` + history. Generator determinism (ADR-0003) makes this safe — same seed always produces same graph.

**SchemaVersion bump required** on `RunStateDTO` per ADR-0004 §"distributed schema registry". Migration path: new field set + drop linear `_steps[]` field; no backwards-compatible reader (M1 is pre-EA, no shipped saves to migrate).

**Why this is a blocker, not a follow-up:** Authoring sub-commit 3 without
deciding the persistence shape means `RunStateDTO` either (a) lags behind the
NodeMap refactor and breaks save/load silently, or (b) gets thrashed in a
follow-up commit that violates the "in-same-sub-commit" no-bridges discipline.

---

## Cross-ADR drift flags (non-blockers, monitor)

- **ADR-0002 (engine-free Run.asmdef):** `NodeMap` already POCO; refactor stays POCO. ✓
- **ADR-0003 (deterministic RNG):** `FromBiomeGraph` does not generate — only adapts. No new RNG seeding inside NodeMap. ✓
- **ADR-0013 (run-scoped card collection):** `RunDeck` independent of NodeMap shape change. ✓
- **ADR-0014 (UI Toolkit primary):** `MapViewController` already UI Toolkit. ✓
- **ADR-0015 (distribution narrowing):** Honoured by parameterized `terminalType`. ✓

---

## Lock criteria

1. User approves ADR-0004 persist-regenerate-on-load approach (`BiomeId` + `RunSeed` + `CurrentNodeIndex` + `PathHistory[]`; SchemaVersion bump).
2. Sub-commit 3 lands all of: NodeMap A2 refactor, `FromBiomeGraph` factory, `SingleCombat` + `BuildLinearMilestone1` deletion, RunSceneHost Resources wiring, MapViewController `Bind(NodeMap)`, 10 tests, `RunStateDTO` shape change + SchemaVersion bump.
3. Test count target: 499 passed / 0 failed / 1 ignored (+10 new tests).
4. No transitional comments, no `// TODO retire`, no parallel surfaces.

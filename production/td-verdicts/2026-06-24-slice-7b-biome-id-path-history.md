# Slice 7b — BiomeId on SO + PathHistory[] on NodeMap

Date: 2026-06-24
Files touched:
- `Assets/Scripts/Run/Authoring/BiomeDistributionSO.cs`
- `Assets/Scripts/Run/NodeMap.cs`
- `Assets/Scripts/CombatView/RunSceneHost.cs`
- 6 EditMode test fixtures (`RunSession_Test`, `RunSession_Reward_Test`, `RunSession_CardReward_Test`, `NodeMap_AllowBidirectional_Test`, `RunController_HappyPath_Test`, `SceneEncounterBuilder_Test`)
- `Assets/Resources/Run/Biomes/Biome1Distribution.asset`
- 3 new EditMode tests (`NodeMap_PathHistory_Test`, `NodeMap_BiomeId_Test`, `BiomeDistributionSO_BiomeId_Test`)

ADRs at risk: 0004 (Save & Persistence), 0011 (no bridges), 0015 (configuration narrowing), 0003 (deterministic RNG).

## Technical Director Review

**Verdict: APPROVE with one AMEND delta.**

**Shape: APPROVE.** Pure-additive, ADR-0011 clean, ADR-0015 placement consistent, ADR-0004 promise honored without contract change. No RNG surface touched.

**Q1 PathHistory semantic — APPROVE pre-mutation push of `from.Index`.**
Invariant `PathHistory.Concat(CurrentIndex) == full trace` is the right one. Start beacon enters PathHistory on first Advance (correct — Start IS visited); Terminal never enters (correct — Advance never fires from terminal, so Terminal appears only as `CurrentIndex` at run end). Post-mutation push of `to.Index` would invert the invariant and force every reader to special-case "is the current beacon already in history?" — that's the bridge smell.

**Q2 Test fixture BiomeId — AMEND: use `"test_biome"` literal, not `""`.**
Empty-string-as-"untyped" is exactly the bimodal path ADR-0011 forbids: production code would eventually grow `if (string.IsNullOrEmpty(BiomeId))` branches to handle the "untyped" case, and that's a vestigial enum value in string clothing. `"test_biome"` is a real, declared value — tests assert against a known slug, prod asserts against `"biome_1_wasteland"`, the field's contract is "always a real slug." Clean.

Corollary: NodeMap ctor should treat `BiomeId` as required (non-null, non-empty) — throw `ArgumentException` if empty. That converts the ADR-0011 discipline from convention to enforced invariant, and the OnValidate on the SO catches it at author time before it ever reaches the ctor.

**Q3 OnValidate severity — APPROVE warn.**
`LogWarning` matches existing OnValidate cadence on this SO and Unity surfaces it in the Inspector with the yellow triangle. `LogError` is reserved for "this asset will crash at runtime" — and with the ctor throw from Q2 amendment, the asset *will* crash at runtime if shipped empty, so arguably LogError is defensible. But warn is consistent with the SO's existing style, and the ctor throw is the real safety net. Keep warn.

**Success criteria:** Slice 8 RunState persistence can serialize `NodeMap` round-trip from `(BiomeId, RunSeed, CurrentIndex, PathHistory)` with no schema additions.

## Applied deltas (vs original brief)

- AMEND Q2: test fixtures pass `"test_biome"`, NodeMap ctor throws `ArgumentException` on null/empty `biomeId`.

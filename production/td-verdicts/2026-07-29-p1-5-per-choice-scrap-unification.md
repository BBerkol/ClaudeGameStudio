# TD Verdict — P1-5: Retire `_perChoiceScrapReward` Parallel Storage

**Date:** 2026-07-29
**Source audit:** `production/td-verdicts/2026-07-29-whole-game-health-opt-audit.md` §3 P1-5
**Files touched:** `IDialogueChoiceData.cs`, `DialogueSceneController.cs`,
`EventModalHost.cs`, `EventHandler.cs`, `IEventPayloadData.cs`,
`EventPayloadDefinitionSO.cs`, `NodeEncounterDataInitializer.cs`,
`EventHandler_Dispatch_Test.cs`

---

## Problem

Three parallel spellings of the same number (choice reward count):

1. `DialogueChoiceSO._rewardCount` — the authored per-choice number, already the
   display source for the outcome stinger.
2. `EventPayloadDefinitionSO._perChoiceScrapReward[]` — index-parallel array on
   the payload SO.
3. Initializer call-sites spelling the number twice (once on the choice, once on
   the payload array).

ADR-0011 forbidden pattern #2 (parallel storage). If either surface drifts,
`EventHandler` grants the wrong amount silently.

---

## TD Verdict

**ACCEPT — retire `_perChoiceScrapReward` parallel storage.**

Pick `DialogueChoiceSO.RewardCount` (already authored, already drives the UI
stinger) as the single source of truth. Propagate it through `IDialogueChoiceData`
so the engine-free `EventHandler` can read it without touching Unity types.

**Accepted shape:**

- `IDialogueChoiceData` gains `int RewardCount { get; }` — projecting from the
  terminal `DialogueChoiceSO.RewardCount` at click time.
- `DialogueSceneController.DialogueChoiceProjection` stores and forwards
  `RewardCount` from the `DialogueChoiceSO` at click time.
- `EventModalHost.SyntheticChoice.RewardCount = 0` (error-recovery path has no
  reward).
- `EventHandler.ReadPerChoiceScrap` reads `choice.RewardCount` instead of
  indexing `payload.PerChoiceScrapReward`. Non-zero `RewardCount` overrides the
  scalar fallback; zero falls through to the scalar (Windfall single-choice
  scrapAmount stays as fallback per ADR-0015 narrowing).
- `IEventPayloadData.PerChoiceScrapReward` removed. `EventPayloadDefinitionSO`
  field + property + `Configure()` parameter removed.
- Initializer drops `perChoiceScrapReward` args from all 4 calls. Wreck's
  `scrapAmount: 40` stays (Windfall scalar fallback). Cache relies solely on
  `Choice_Cache_Pry.rewardCount = 60` and `Choice_Cache_Blast.rewardCount = 24`.
- Test `FakePayload` drops `PerChoiceScrapReward`. `FakeChoice` gains
  `RewardCount`. Per-choice scrap tests drive via `FakePresenter.AutoRewardCount`
  (or `FakeChoice` constructor).

**ADR alignment:** ADR-0011 no-bridges (eliminates parallel storage).
ADR-0002 engine-free POCO: `IDialogueChoiceData` must not import Unity types;
`int RewardCount` is a plain int — clean.

**Risk:** Low. The `RewardCount` field is already authored on every shipped
`DialogueChoiceSO`; values are identical to the now-deleted array entries.

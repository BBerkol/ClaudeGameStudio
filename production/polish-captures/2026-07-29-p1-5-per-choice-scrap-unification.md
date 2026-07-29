# Polish Capture — P1-5: Retire `_perChoiceScrapReward` Parallel Storage

**Date:** 2026-07-29
**TD Verdict file:** `production/td-verdicts/2026-07-29-p1-5-per-choice-scrap-unification.md`
**Source audit:** `production/td-verdicts/2026-07-29-whole-game-health-opt-audit.md` §3 P1-5

## Files Touched

- `Assets/Scripts/Run/NodeEncounter/IDialogueChoiceData.cs` — add `RewardCount`
- `Assets/Scripts/UI/DialogueSceneController.cs` — `DialogueChoiceProjection` gains `RewardCount`
- `Assets/Scripts/CombatView/EventModalHost.cs` — `SyntheticChoice.RewardCount = 0`
- `Assets/Scripts/Run/NodeEncounter/EventHandler.cs` — `ReadPerChoiceScrap` reads `choice.RewardCount`
- `Assets/Scripts/Run/NodeEncounter/IEventPayloadData.cs` — remove `PerChoiceScrapReward`
- `Assets/Scripts/Run/Authoring/EventPayloadDefinitionSO.cs` — remove field, property, Configure param
- `Assets/Editor/NodeEncounterDataInitializer.cs` — drop `perChoiceScrapReward` args
- `Assets/Tests/EditMode/Run/NodeEncounter/EventHandler_Dispatch_Test.cs` — migrate fakes

## Authored Values Being Destroyed

### `EventPayloadDefinitionSO.cs`
- `[SerializeField] private int[] _perChoiceScrapReward = new int[0]` — serialized field removed.
  All live authored assets (`Event_Wreck`, `Event_Cache`) carry this array in their `.asset` YAML.
  After field removal Unity will silently drop the orphaned serialized data on next import.
  **Values preserved:** the identical numbers already live on the choice SOs:
  - `Event_Wreck._perChoiceScrapReward[0] = 40` → `Choice_Wreck_Take._rewardCount = 40` (already authored)
  - `Event_Cache._perChoiceScrapReward = [60, 24]` → `Choice_Cache_Pry._rewardCount = 60`,
    `Choice_Cache_Blast._rewardCount = 24` (already authored)

- `public IReadOnlyList<int> PerChoiceScrapReward` property — removed from public API.
- `Configure()` parameter `int[] perChoiceScrapReward = null` — removed.

### `IEventPayloadData.cs`
- `IReadOnlyList<int> PerChoiceScrapReward { get; }` interface member — removed.

### `NodeEncounterDataInitializer.cs`
- `BuildWreck`: `perChoiceScrapReward: new[] { 40 }` arg — removed. `scrapAmount: 40` stays.
- `BuildCache`: `perChoiceScrapReward: new[] { 60, 24 }` arg — removed. Values live on choice SOs.

## Technical Director Review

**Source:** `production/td-verdicts/2026-07-29-p1-5-per-choice-scrap-unification.md`

**Verdict: ACCEPT**

Pick `DialogueChoiceSO.RewardCount` (already authored, already drives the UI stinger) as the
single source of truth. The parallel `_perChoiceScrapReward` array on `EventPayloadDefinitionSO`
is an ADR-0011 forbidden pattern #2 (parallel storage). No authored value is lost — all
numbers are already present on the choice SOs in identical form. The `scrapAmount` scalar
on Windfall payload stays as the fallback for single-choice events where `RewardCount == 0`.

Risk: Low. Behaviour is preserved identically.

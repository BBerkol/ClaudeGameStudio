# Authoring + Data Layer Audit — 2026-07-29

Scope: */Authoring/**, Editor/, top-level *SO.cs.

## Executive Summary

Authoring + data layer is in **good structural shape** with one live P1 concern
concentrated in the freshly-landed 2026-07-27 Event slice. Pre-existing SOs
(BiomeDistributionSO, FrameLayoutSO, VehicleDefinitionSO, PartDefinitionSO,
MapBeaconStyleSO, BeaconSceneBindingSO) all ship OnValidate guardrails and
follow ADR-0011 no-bridges and ADR-0015 narrowing patterns cleanly. Editor
tooling has established a solid one-file-per-major-target convention
(`AuthorDialogueSceneRoot`, `AuthorRunHUDHost`, `AuthorBiome1MapThemeIcons`),
which the still-monolithic 9000-line `CombatPrefabAuthor.cs` predates.

**4 P1 findings** — all clustered around the Event slice: missing OnValidate
on `EventPayloadDefinitionSO`, missing OnValidate on `DialogueSceneSO`,
parallel-storage risk on `DialogueChoiceSO._rewardCount` vs the payload's
scrap fields, and **triplicate scrap-number authoring** in
`NodeEncounterDataInitializer` where the same number is spelled three times per
event (Wreck: 40 in three places; Cache: 60/24 in three places). This is a
health issue today and a scaling trap when reward types expand past scrap.

**9 P2 findings** — mostly quality-of-life: `Configure(...)` 9-optional-param
overload trap on the new SO, missing invariant checks on
`VehicleDefinitionSO.OnValidate` (null layout, duplicate SlotId, kind mismatch,
missing structural part), missing clamp on `BiomeDistributionSO._targetBeaconCount>40`,
missing partId presence check on `PartDefinitionSO`, and a note to stop growing
the 9000-line `CombatPrefabAuthor.cs`.

**7 P3 findings** — including a stale one-shot migrator (`CardAssetMigrator.cs`)
overdue for deletion since 2026-06-02, and a possible memory drift on
`BeaconSceneBindingSO`'s hybrid roster.

**Nothing broken, nothing shipping-blocking**, but the P1 cluster is worth
resolving before the next Event-slice extension (Merchant, Chopshop) because
those slices will likely re-use the same DialogueSceneSO/DialogueChoiceSO
carriers and inherit the parallel-storage risk if it isn't unified now.


## P1 — Must fix before next major feature slice

- `EventPayloadDefinitionSO.cs:1-171` — **No `OnValidate` guardrails on invariants**
  - Why it matters: Health lens. The SO carries several fields with hard invariants that the runtime relies on: `_convertRate` must be > 0 (comment says "Rate <= 0 is authoring error; EventHandler asserts"), `_convertMaxInput` must be >= 0, `_scrapAmount`/`_fuelAmount` must be >= 0, `_ambushArchetype` must be non-empty when `_kind == Ambush`, `_dialogue` must be non-null. Also both per-choice arrays should either be length 0 or match the paired `DialogueSceneSO.Choices` length. Today invariant errors only surface at runtime as EventHandler asserts. All neighbor SOs (`BiomeDistributionSO`, `MapBeaconStyleSO`, `BeaconSceneBindingSO`, `FrameLayoutSO`) ship `OnValidate` — this one is an outlier.
  - Fix shape: Add `#if UNITY_EDITOR OnValidate` that clamps numeric fields, warns on `_kind==Ambush && string.IsNullOrWhiteSpace(_ambushArchetype)`, warns on `_dialogue==null`, and warns when per-choice arrays are non-empty but length != `_dialogue.Choices.Count`.

- `DialogueChoiceSO.cs:1-152` — **No `OnValidate`; `_rewardCount` display/mechanics divergence is designer-discipline only**
  - Why it matters: Health + 1.0-survival lens. The comment on `_rewardCount` (line 89-98) admits: "Display-only — the actual granted/deducted amount lives on the payload SO today; designer discipline keeps them in sync until the choice-as-source-of-truth unification lands." This is a **parallel-storage smell** (ADR-0011 forbidden #2) waiting to bite — Wreck ships `rewardCount:40` on the choice AND `scrapAmount:40` on the payload AND `perChoiceScrapReward:[40]` on the payload — three fields for the same number. On the Cache event, the choices carry 60/24 and the payload's `perChoiceScrapReward` also carries [60, 24]. Duplication is *already* live in `NodeEncounterDataInitializer.cs:143, 285`.
  - Fix shape: Two options. (A) Declare `_rewardCount` on the choice as the source of truth and delete `perChoiceScrapReward` from the payload (the presenter reads choices, the mechanical resolver reads choices too — the payload keeps only `scrapAmount` as a fallback for single-choice Windfall). (B) Keep payload as source of truth and mark the choice's `_rewardCount` as `[HideInInspector]` bound from the payload at Bind time so designers can't drift the numbers. Recommendation: A — the outcome-reveal pass already treats the choice as the semantic unit ("this choice grants X"). Track as follow-up rather than block the slice, but log the parallel-storage risk explicitly.

- `DialogueSceneSO.cs:78` — **Empty `_choices` list has no `OnValidate` warning; runtime assert is the only guard**
  - Why it matters: Health lens. Comment on the field says "Empty list is authoring error (asserted at Bind)." — but the SO ships without any editor-time surfacing. Author-time discovery is much cheaper than runtime discovery.
  - Fix shape: Add `OnValidate` that warns when `_choices == null || _choices.Length == 0`, and additionally warns on null entries inside the array (a null `DialogueChoiceSO` in the middle of a two-choice list would surface as an NPE at Bind).

- `NodeEncounterDataInitializer.cs:143, 193, 229, 285` — **Payload/choice scrap number duplication, single edit-point drift risk**
  - Why it matters: 1.0-survival + health lens. `Event_Wreck` ships `scrapAmount:40, perChoiceScrapReward:[40]` **and** the paired `Choice_Wreck_Take` ships `rewardCount:40`. Cache similarly ships `perChoiceScrapReward:[60,24]` AND choices carry `rewardCount:60/24`. A designer editing the choice's outcome copy will drift one of these numbers and the display/mechanics will silently mismatch. This is a **triplicate-authoring-surface** anti-pattern — the initializer literally spells the number three times per event.
  - Fix shape: Depends on which unification path you pick under `DialogueChoiceSO.cs:1-152` finding. Whichever surface wins as source of truth, delete the other two writes from `NodeEncounterDataInitializer` — single spelling per number.


## P2 — Should fix within next 3 slices

- `EventPayloadDefinitionSO.cs:148-170` — **`Configure(...)` overload with 9 optional parameters is a default-param semantic trap**
  - Why it matters: Health lens (memory item `feedback_default_param_overload_semantic_trap`). `Configure` has 9 params, 7 of them optional with defaults. Every existing call site relies on named args (`kind:`, `dialogue:`, `scrapAmount:`, etc.), which is good discipline — but the shape invites drift. When `_perChoiceStormCost` was added, existing callers silently gained `perChoiceStormCost = new int[0]` default which for Wreck/Scavenger/Ambush *is what they wanted*, but for Cache the initializer had to be edited too. Any future field addition with a "safe" default (0 / empty / null) can silently zero out fields on existing callers who don't opt in. Additionally the "always-defaults" for Ambush archetype `"raider_scout"` on Wreck/Scavenger/Cache assets is misleading (dead data written to disk).
  - Fix shape: Split into per-kind explicit builder methods (`ConfigureWindfall(dialogue, scrap, fuel, perChoiceStorm)`, `ConfigureConvert(dialogue, direction, rate, maxInput, perChoiceStorm)`, `ConfigureAmbush(dialogue, archetype, perChoiceStorm)`, `ConfigureTreasure(dialogue, perChoiceStorm, perChoiceScrap)`). Each sets only the fields load-bearing for that kind. This mirrors the payload semantics — a Windfall SO carrying `_ambushArchetype="raider_scout"` on disk is a lie.

- `NodeEncounterDataInitializer.cs:87-104` — **Menu doesn't wire the four generated payloads into any registry — silent runtime "no payload with Kind=X" throws depend on external wiring**
  - Why it matters: 1.0-survival lens. `EventHandler.FindPayload` throws `"no payload asset with Kind=... in the configured biome pool"` if not all 4 kinds are present. The initializer generates them under `Assets/Resources/Run/Events/Biome1/` and someone else (probably `EventModalHost` reading Resources lazily) picks them up. But there's no assert-at-menu-time check that all 4 kinds are covered — if the designer edits `_kind` on Cache to Windfall, the biome silently loses Treasure coverage.
  - Fix shape: After Build, load all `EventPayloadDefinitionSO` under `Assets/Resources/Run/Events/Biome1/`, verify every `EventPayloadKind` is present exactly once, log an error if not. Optional stretch: also verify `_dialogue` refs are wired and `_dialogue.Choices` matches `_perChoiceStormCost.Length` when non-empty.

- `BiomeDistributionSO.cs:308-313` — **`_targetBeaconCount > 40` OnValidate warns but doesn't clamp**
  - Why it matters: Health lens. Every other clamped numeric field on this SO both warns AND clamps. This one only warns, leaving the runtime to throw. Since the comment on the field (line 118) says "Hard cap of 40 enforced by the generator", the SO should self-clamp to match — designer sees the field value fix itself in the Inspector, no runtime surprise.
  - Fix shape: Add `_targetBeaconCount = 40;` inside the `> 40` warning branch, mirroring the `< 4` clamp above.

- `VehicleDefinitionSO.cs:84-88` — **`OnValidate` misses several invariants: null `_layout`, part `SlotId` collisions, part-kind vs slot-kind mismatch, missing structural part**
  - Why it matters: Health lens. Runtime `BuildVehicle` throws or produces broken vehicles on: `_layout==null`, two `PartSlot` entries with the same `SlotId`, a `PartDefinitionSO.SlotKind` that doesn't match the layout slot's `Kind`, structural slot with no part assigned. All of these are catchable at author time.
  - Fix shape: Extend `OnValidate` with a foreach over `_parts` that (a) warns if `_layout==null`, (b) tracks a seen-set of SlotIds and warns on duplicates, (c) resolves each entry's SlotId against `_layout.Slots` and warns if `entry.Part.SlotKind != slot.Kind`, (d) warns if any layout slot marked `IsStructural` has no matching Part entry.

- `PartDefinitionSO.cs:41-42` — **`_partId` has no uniqueness guard and no OnValidate**
  - Why it matters: Health lens. Comment on the field says "Must be unique across all `PartDefinitionSO` assets." — but nothing enforces it. Save/load will collide silently on duplicate `_partId` since the persistence layer keys on it.
  - Fix shape: Add `OnValidate` warn on empty `_partId` (matches BiomeDistributionSO's BiomeId pattern, line 291). Cross-asset uniqueness requires an asset-postprocessor or menu validator; scope P3, but the empty-check is a P2 quick win.

- `StormEngulfmentSO.cs:20-28` — **Single-field SO has no OnValidate but the `[Range(0.25, 10)]` attribute already clamps**
  - Why it matters: Health lens (minor). The Range attribute clamps via Inspector but not via Configure/reflection paths — if a future editor script writes a non-clamped value, it will stick. Not urgent since no such caller exists today.
  - Fix shape: When touching this SO next time, add `OnValidate` mirror-clamp. Purely defensive; leave alone if the SO stays single-field.

- `DialogueSceneSO.cs:78, 84` — **`Choices` property returns `IReadOnlyList<DialogueChoiceSO>` by wrapping the array — allocation is trivial but the array itself is exposed via a struct wrap Unity may re-cast**
  - Why it matters: Optimization lens (low). `_choices` is a `DialogueChoiceSO[]` cast to `IReadOnlyList<DialogueChoiceSO>` — cheap (no allocation), but callers can still cast back to the concrete array. Not a leak today (no caller mutates), but no defensive copy.
  - Fix shape: Not worth changing right now; note only if you refactor the SO surface.

- `BiomeGenerationInputsFactory.cs:33-45` — **`From(distribution)` allocates two `List<>` per call; cheap for map-gen but if it fires per-frame it would matter**
  - Why it matters: Optimization lens. Today called once per biome enter — negligible. If a future slice moves distribution unpacking into a per-beacon or per-frame path, this becomes GC pressure.
  - Fix shape: Not urgent; add a note if you ever cache-warm the POCO record.

- `AuthorBiome1MapThemeIcons.cs:47` — **`AssetDatabase.LoadAllAssetsAtPath` + `OfType<Sprite>().ToArray()` on every menu invocation loads whole .psb**
  - Why it matters: Optimization lens (edit-time only, minor). Menu-invoked, low frequency; loading .psb reimport-graph on demand is fine.
  - Fix shape: No action.

- `CombatPrefabAuthor.cs` (9000 lines) — **Single monolithic Editor file carrying 30+ author menus is a maintenance liability**
  - Why it matters: Health + 1.0-survival lens. 9000-line editor file. `AuthorDialogueSceneRoot.cs` and `AuthorRunHUDHost.cs` and `AuthorBiome1MapThemeIcons.cs` have already been split out — the pattern is established. Continued growth of `CombatPrefabAuthor.cs` will make Event/Merchant/Chopshop authoring hard to find.
  - Fix shape: When adding a new prefab author (Merchant, Chopshop coming), spin it out as a sibling `AuthorMerchantRoot.cs` / `AuthorChopshopRoot.cs` instead of appending to `CombatPrefabAuthor.cs`. Existing Event author code (`AuthorEventRootPrefab` at line 8715) is a candidate to split when it next needs edits — small, self-contained, natural boundary.


## P3 — Opportunistic / nice-to-have

- `MapBeaconStyleSO.cs:46, 53` — **Sprite array default lengths hardcoded to `9`**
  - Why it matters: Health lens (minor). `_beaconIconsByType = new Sprite[9]` and `_beaconIconAlternates = new SpritePool[9]`. If `BeaconType` enum grows past 9, `OnValidate` catches it and resizes — but the field initializer is a magic number.
  - Fix shape: `new Sprite[Enum.GetValues(typeof(BeaconType)).Length]` — impossible in a field initializer without reflection at runtime, so leave as-is; the OnValidate resize is the actual guard. Note only.

- `NodeEncounterDataInitializer.cs:301-319` — **`CreateOrRefresh` helper uses `LoadAssetAtPath` before `SaveAssets`; ordering okay but not documented**
  - Why it matters: Health lens (minor). Order of AssetDatabase operations after CreateAsset can matter (memory item `unity_batchmode_no_quit` is adjacent). Today Build() calls `SaveAssets + Refresh` at the end (line 99-100) and each `CreateAsset` inside `CreateOrRefresh` doesn't need an interim flush. Good.
  - Fix shape: No action; note only.

- `AuthorRunHUDHost.cs:105`, `AuthorDialogueSceneRoot.cs:94` — **`EnsureFolder` implementations are duplicated across authoring scripts**
  - Why it matters: Health lens (minor). Same helper appears in `NodeEncounterDataInitializer.cs:290`, `AuthorRunHUDHost.cs:113`, `AuthorDialogueSceneRoot.cs:94`, `CombatPrefabAuthor.cs` (various). Not ADR-0011 forbidden (utility duplication, not bridge), but a `AuthorHelpers.EnsureFolder(string path)` would DRY.
  - Fix shape: When the next author script is added, factor `EnsureFolder` + `EnsureFolder(parent, leaf)` into a shared internal static.

- `CombatBalanceSO.cs:8-38` — **Only holds `_enemyRepairAmount` + `_enemyPlateAmount` today; the fee-brains still hardcode most values**
  - Why it matters: 1.0-survival lens. The comment (line 10-15) says "Centralizes combat numeric tunables that previously lived as `const int` magic numbers across CombatController and the brains." — but only 2 tunables live here today. This SO is under-populated relative to its stated ambition.
  - Fix shape: Not urgent. When designers hit tuning pain on a specific hardcoded number, move that number to this SO rather than adding a new one. Note it as a landing zone.

- `CardAssetMigrator.cs` — **One-shot migrator marked for deletion in Stage K but still in tree**
  - Why it matters: Health lens (ADR-0011 exception #1 — one-shot migrators). File xmldoc line 29-31 says "this file is deleted in Stage K once the 13 assets have been migrated and committed." ADR-0010 landed 2026-06-02. Assets are migrated. This file is dead code today.
  - Fix shape: Confirm the ADR-0010 migration is complete (memory says it is), then delete `CardAssetMigrator.cs` + its `.meta`. Verify no CI grep gate references it first.

- `RussoOneFontSetup.cs:60-63` — **Delete-then-recreate on re-run**
  - Why it matters: 1.0-survival lens. Same GUID-churn pattern as the vehicle authors (memory `project_vehicle_author_guid_churn`). Font asset GUID changes on every re-run. If anything references `RussoOne SDF.asset` by GUID (probably nothing — TMP references live via `TMP_Settings.defaultFontAsset`), those refs break.
  - Fix shape: Not urgent — the TMP settings wire is re-established at the end of the same run (line 108-115). Note only.

- `BeaconSceneBindingSO.cs:143` — **`BeaconLoadMode` enum has two values (`AdditiveScene`, `PrefabRoot`) that were the 2026-06-28 hybrid rollback**
  - Why it matters: 1.0-survival lens. Memory `project_scene_split_hybrid_verdict` confirms Combat + EliteCombat + Boss stay AdditiveScene; Rest + Haven + Merchant + Event + Chopshop go PrefabRoot. Today the roster in `CombatPrefabAuthor.AuthorBeaconSceneBinding` (line 8387-8397) has Haven + Merchant + Chopshop STILL on AdditiveScene, only Rest + Event on PrefabRoot. That contradicts the memory. Either the memory is stale, or the migration is incomplete.
  - Fix shape: Verify whether Haven/Merchant/Chopshop are meant to be PrefabRoot per the hybrid verdict or still stubbed on AdditiveScene. If PrefabRoot, plan the migration; if AdditiveScene, update the memory.


## Non-findings — audited and clean

- `BiomeDistributionSO.cs` OnValidate — solid guards on every numeric invariant except the missing `>40` clamp (P2 above); ADR-0015 narrowing pattern preserved cleanly.
- `BeaconSceneBindingSO.cs` OnValidate — duplicate-entry warning, `Start` warning, `AdditiveScene`+empty-path warning, `PrefabRoot`+non-empty-path warning all present.
- `FrameLayoutSO.cs` OnValidate — comprehensive R_FL.1 rule enforcement (SlotId uniqueness, structural presence, HP boundaries, Armor redirect chain 3a-3e, ExposureMultiplier finite/positive, MaxHpOverride).
- `MapBeaconStyleSO.cs` OnValidate — resizes both parallel arrays to match `BeaconType.Length` and preserves existing sprite slots.
- `BossEncounterSO.cs` — minimal shape, no runtime invariants worth guarding beyond what `BiomeDistributionSO.OnValidate` already asserts (terminal-is-boss ↔ encounter-non-null pairing).
- `LootContextTag.cs` — data-flag lagging-dep pattern properly applied; enum ordinals load-bearing, documented.
- `IEventPayloadData.cs` / `IDialogueChoiceData.cs` — POCO interface split from SO is clean; engine-free per ADR-0002; no bridge/adapter code.
- `BiomeGenerationInputsFactory.cs` — clean SO→POCO unpack boundary, weight ≤ 0 filter applied, boss archetype nullability handled.
- `CardDefinitionSO.cs` — `[SerializeReference]` polymorphism per ADR-0010 correctly retiring per-kind discriminator fields; ToRuntime copies effects to avoid shared mutable graph.
- `PartDefinitionSO.cs` shape — ADR-0012 sum-of-parts armor correctly modelled; `Min(0)` on `_armorContribution`, `Min(1)` on `_maxHp`.
- `VehicleDefinitionSO.cs` fuel fields — `_tankCapacity` and `_fuelBurnMultiplier` shape matches V3 locked shape; per-chassis authoring surface clean.
- `CombatBalanceSO.cs` — no CI-forbidden patterns; `Min(1)` on repair, `Min(0)` on plate.
- `CanonicalCardData.cs` — proper factory pattern for effect graphs (each call gets fresh instances); single source of truth shared by initializer + migrator.
- `CombatDataInitializer.cs` — idempotent create-or-load semantics; preserves designer edits on re-run; scoped to fresh-checkout bootstrap.
- `AuthorDialogueSceneRoot.cs` — clean UIDocument prefab authoring; explicit re-author confirmation dialog; explicit missing-asset error surfaces.
- `AuthorRunHUDHost.cs` — same shape as DialogueSceneRoot; correct SerializedObject wire; docstring correctly notes `[DefaultExecutionOrder(-100)]` Awake ordering.
- `AuthorBiome1MapThemeIcons.cs` — proper "resolve sprites FIRST, mutate SO SECOND" ordering; explicit throw on missing sprite name (2026-07-28 guard).
- `UIToolkitInitializer.cs` — properly idempotent (loads existing, no-op returns); correct SerializedObject wire pattern.
- `CombatPrefabStageHook.cs` — Popups/Debug auto-hide with proper Save/After-save re-toggle; no state leaks.
- `RussoOneFontSetup.cs` — persists material + atlas texture as sub-assets to survive domain reload (documented gotcha handled).
- `NodeEncounterDataInitializer.cs` — two-mode Generate/Refresh split correctly preserves designer tweaks by default; overwrite dialog is explicit.
- ADR-0011 no-bridges scan — no `if (isLegacy)` branches, no parallel storage on shipped SOs, no adapter layers found in authoring code. Bridge references in comments are all documented exceptions (one-shot migrator, or historical context notes).
- Unity 6.3 `[SerializeField]` on properties/methods — grep confirmed no violations across authoring layer.
- `Object.FindObjectsOfType` deprecated API — grep confirmed no usages across authoring layer or Scripts/.

## Cross-cutting recommendations

**Top pattern — missing `OnValidate` on newly-landed SOs.** Every SO added before 2026-07 ships with `OnValidate` guardrails; the three SOs added in the 2026-07-27 Event slice (`EventPayloadDefinitionSO`, `DialogueSceneSO`, `DialogueChoiceSO`) all ship without any. Adding `OnValidate` to authoring SOs should be treated as ship-blocking discipline (comparable to writing a docstring on the class).

**Top pattern — parallel-storage risk on choice/payload split.** The 2026-07-28 Route D per-choice cost/reward addition created a spot where the same scrap number lives on three surfaces (choice `_rewardCount` display, payload `_scrapAmount` fallback, payload `_perChoiceScrapReward` per-choice). Unification is worth doing this slice or next — waiting until faction/reward-kind expansion lands makes it harder, not easier. See P1 finding on `DialogueChoiceSO.cs:1-152`.

**Top pattern — `Configure(...)` optional-parameter builder methods.** `EventPayloadDefinitionSO.Configure` (9 optional params) and `VehicleDefinitionSO.Configure` (5 required — safer) show the shape divergence. Prefer per-kind explicit builders on any SO whose fields split by discriminator; keeps callers honest about which fields are load-bearing.

**Deferred cleanup — `CardAssetMigrator.cs` awaiting deletion since 2026-06-02.** ADR-0010 is complete; the migrator is a documented one-shot marked for deletion in "Stage K." Do the delete on the next non-slice housekeeping day.

**Verify — `BeaconSceneBindingSO` roster vs memory `project_scene_split_hybrid_verdict`.** Roster today has Haven/Merchant/Chopshop on AdditiveScene while memory says PrefabRoot. Someone owns reconciling this — either the migration is planned but not landed, or the memory needs an update.

**Author-menu discipline going forward.** Do not add new authoring routines to `CombatPrefabAuthor.cs` (9000 lines). New patterns like `AuthorDialogueSceneRoot.cs`, `AuthorRunHUDHost.cs`, `AuthorBiome1MapThemeIcons.cs` are the model — one file per major prefab/asset target.


## Cross-cutting recommendations
_[Fill in at end.]_

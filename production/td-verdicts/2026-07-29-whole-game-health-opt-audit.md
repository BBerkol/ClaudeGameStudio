# Whole-Game Health + Optimization Audit — 2026-07-29

Synthesis of three parallel scoped audits after Node Encounter Event slice ship
(framework `66a8e0f`; Unity `43a438e / 47c00e4 / 04d02e0`). Sources:

- `production/td-verdicts/2026-07-29-audit-model-layer.md`
- `production/td-verdicts/2026-07-29-audit-view-ui-layer.md`
- `production/td-verdicts/2026-07-29-audit-authoring-data-layer.md`

Review-only pass. No code changes. Verdict + tiered fix ordering.

---

## 1. Executive Summary

Codebase is in **good structural shape** post-Event-slice. All three lenses
(health, optimization, 1.0-survival) come back clean on the load-bearing
infrastructure — save layer is exemplary, ADR-0002 view/model split holds
universally, ADR-0014 UI Toolkit discipline is broadly internalized, ADR-0003
determinism scan is clean across Run/Combat/Save/View/UI, and ADR-0011
no-bridges scan finds no live drift outside one overdue one-shot migrator.

**Surprises:**
- The 2026-07-27 P1 anchor on `StormAdvanceVisualPacer` (missing `_host`
  null-check) is a **false alarm** — the guard chain already covers it. Retire.
- The Event slice, freshly landed, is the only cluster where P1s bunch: three
  SOs shipped without `OnValidate` (`EventPayloadDefinitionSO`,
  `DialogueSceneSO`, `DialogueChoiceSO`) and the initializer triplicates every
  scrap number (three surfaces per event reward). This is repairable this
  slice, and doing so is much cheaper than after Merchant / Chopshop copy the
  same shape.
- The two remaining pre-existing P1 anchors (`NodeMap.ForwardEdgesFrom`
  allocation, `RunSession.IsStrandedForFuel` per-frame) are both derived-state
  caching problems and share a common fix pattern that's worth codifying.
- **Total live P1 count: 6.** One (view-side) has a strong dependency on model
  P1 landing first, so real fix count is 5 discrete pieces of work.

---

## 2. False Alarms Retired

- **`StormAdvanceVisualPacer` missing `_host` null-check** (2026-07-27 anchor).
  View/UI audit determined the practical guard chain (`_boundSession == null`
  return at `Update:127`, with `_boundSession` only ever bound via
  `BindSession` which requires `_host` non-null) already covers the failure
  mode. Retire from P1. Kept in P3 as one-line defense-in-depth.

- **`StormAdvanceTick` allocation per queued flush** (view/UI P3-05).
  Confirmed `readonly struct` per 2026-07-24 and 2026-07-25 storm-cursor
  verdicts. Stack-only, zero heap allocation. Drop the note entirely.

- **`StormEngulfmentSO` needing `OnValidate` today** (authoring P2 mid-list).
  Only a single `[Range(0.25, 10)]` float; Inspector clamping is the only
  writer today. Purely defensive, tiered down to opportunistic.

No cross-audit contradictions detected between the three layers.

---

## 3. P1 Fix Order — Ship This Session

Order below reflects dependency ordering + lens weight. Fix (a) unblocks (d); fixes (b)/(c)/(e) are independent.

### P1-1 — `Run/NodeMap.cs:199-205` — precompute forward-edge arrays

- **Fix shape:** Compute per-node `int[]` at ctor + on `Advance` (edges
  immutable per-run). Expose as `IReadOnlyList<int>`. Delete per-call
  `new List<int>`.
- **Lenses:** optimization primary; health secondary (xmldoc admits the smell).
- **Dependencies:** None. Unblocks P1-4 (fewer allocs to cache around).
- **Route:** `unity-specialist` (pure POCO refactor, framework-shape parallel).

### P1-2 — `Run/RunSession.cs:437-457` — cache `IsStranded` boolean

- **Fix shape:** Add `bool _isStrandedCached` + dirty flag. Invalidate on the
  four mutation seams: `Advance`, fuel `CreditFuel`, `Spend`, `RefillPartial`.
  Lazy recompute on read after invalidation. Zero cost on unchanged frames.
- **Lenses:** optimization primary; 1.0-survival secondary (view-side pacer
  polls this per-frame, and future HUD affordances will too).
- **Dependencies:** Benefits from P1-1 landing first (recompute becomes free
  of list-alloc). Not strictly blocked.
- **Route:** `unity-specialist`.

### P1-3 — `UI/MapViewController.cs:901-912` — pool RebuildConnections buffer

- **Fix shape:** Pool a private `List<ConnectionViewModel>` field on the
  controller; clear at method entry; return `IReadOnlyList<ConnectionViewModel>`
  instead of `.ToArray()`. Zero alloc on unchanged frames.
- **Lenses:** optimization primary; 1.0-survival secondary (biome-2 topology
  will multiply rebind frequency).
- **Dependencies:** None. Independent of model P1s.
- **Route:** `unity-ui-specialist`.

### P1-4 — `EventPayloadDefinitionSO` + `DialogueSceneSO` + `DialogueChoiceSO` — add `OnValidate`

- **Fix shape:** Three sibling `#if UNITY_EDITOR OnValidate` methods. On
  `EventPayloadDefinitionSO`: clamp `_convertRate > 0`, `_convertMaxInput >= 0`,
  `_scrapAmount/_fuelAmount >= 0`; warn on `_kind==Ambush &&
  IsNullOrWhiteSpace(_ambushArchetype)`; warn on `_dialogue==null`; warn when
  per-choice arrays non-empty but length != `_dialogue.Choices.Count`. On
  `DialogueSceneSO`: warn on empty `_choices` and on null entries. On
  `DialogueChoiceSO`: warn on empty `_id`.
- **Lenses:** health primary. This closes the outlier pattern — every neighbor
  SO ships `OnValidate`, these three don't.
- **Dependencies:** None. Independent, three-file scope.
- **Route:** `unity-specialist`.

### P1-5 — `NodeEncounterDataInitializer.cs:143, 285` — unify scrap-reward parallel storage

- **Fix shape:** Pick the choice's `_rewardCount` as the source of truth per
  authoring audit recommendation A. Delete `perChoiceScrapReward` from the
  payload. Payload keeps `_scrapAmount` only as a Windfall single-choice
  fallback. Update initializer to spell the number once per event. Update the
  presenter/resolver to read from the choice.
- **Lenses:** 1.0-survival primary (ADR-0011 forbidden pattern #2 — parallel
  storage); health secondary (single edit-point removes the drift class).
- **Dependencies:** Bundle with P1-4 — both touch the Event-slice SO family;
  same code review scope; blocks P1-4's OnValidate from having to work around
  the divergent surfaces. Fix P1-5 first, then P1-4's per-choice-array
  invariant becomes simpler.
- **Route:** `unity-specialist`.

**Suggested ship order:** P1-5 → P1-4 → P1-1 → P1-2 → P1-3.
P1-5 and P1-4 are one bundle (Event-slice SO tidy-up).
P1-1 and P1-2 are one bundle (derived-state caching pattern in Run/).
P1-3 is standalone view/UI.

---

## 4. P2 Defer List — Schedule Window

Grouped by theme.

### Theme A — Derived-state caching (revisit: opportunistic, whenever a HUD widget adds a new computed poll)

- `Combat/Vehicle.cs:59-84,216-241` — 4 computed getters (`MaxArmor`,
  `CurrentArmor`, `StructuralHp`, `StructuralMaxHp`) iterate slot dictionaries
  per read; caching requires SlotInstance → Vehicle change callback plumbing
  or a `_dirty` bool.
- **Revisit window:** next slice that adds a per-frame HUD poll of any of
  these four, OR before shipping to prevent future regressions from
  MainBarWidget-shaped widgets. Not urgent — 480 dict enumerations/sec is
  survivable at 60fps.

### Theme B — OnValidate coverage on pre-Event SOs (revisit: next opportunistic authoring pass)

- `VehicleDefinitionSO.cs:84-88` — missing null `_layout`, duplicate SlotId,
  kind-mismatch, missing structural part invariants.
- `PartDefinitionSO.cs:41-42` — missing empty-`_partId` guard.
- `BiomeDistributionSO.cs:308-313` — `_targetBeaconCount > 40` should clamp,
  currently only warns. **Note: frozen-SO exception** — this SO is under the
  5-slice cool-off freeze (`project_generator_so_surface_freeze`). This is a
  one-line clamp in existing OnValidate, no surface change, arguably falls
  within freeze scope; hold pending user decision.
- **Revisit window:** next slice that touches any of these SOs for real work;
  or a dedicated "SO validation hardening" 30-min opportunistic pass.

### Theme C — Parallel-storage / adjacent shape hygiene (revisit: before Merchant/Chopshop authoring)

- `EventPayloadDefinitionSO.Configure(...)` 9-optional-param overload —
  default-param semantic trap; split into per-kind explicit builders.
- `NodeEncounterDataInitializer.cs:87-104` — no assert that all four
  `EventPayloadKind` values are covered in the generated pool.
- **Revisit window:** must land before Merchant/Chopshop authoring starts, so
  the pattern doesn't replicate. Concretely: next slice that authors a
  non-Event beacon SO family.

### Theme D — Monolith split (revisit: at next feature-add to each)

- `Editor/CombatPrefabAuthor.cs` (9000 lines) — split at next new prefab
  author target (Merchant, Chopshop coming). Do NOT refactor speculatively.
- `UI/MapViewController.cs` (1107 lines) — split at next add of a new map-view
  concern (biome-2 topology, storm-cursor variant, hover polish). Natural
  boundaries pre-identified: `MapConnectionRenderer`, `MapStormFrontHost`,
  `MapHoverRouter`, `MapTravelAnimator`.
- **Revisit window:** at next feature-add, factor out sibling controller
  instead of appending. No dedicated split slice needed.

### Theme E — Offer-persistence gap (revisit: pre-1.0 polish, definite target)

- `Run/RunState.cs` `PendingCardOffer` + `PendingEventOffer` — no matching
  DTOs; mid-modal Alt+F4 loses the offer, re-load either drops or re-fires.
- **Fix shape:** two new group-of-one DTOs (`PendingCardOfferDto`,
  `PendingEventOfferDto`) mirroring existing ADR-0004 shape. Cost is
  ~2 hours; the save layer's pattern is exemplary and low-cost to extend.
- **Revisit window:** before shipping — this is a real 1.0-survival gap.
  Suggested: bundle into a "save layer completeness" polish slice with any
  other pending-offer fields discovered along the way. Not this session.

### Theme F — EventHandler re-entrance shape (revisit: next Event-slice extension)

- `Run/NodeEncounter/EventHandler.cs:71-76` — per-invocation state on instance
  fields; today safe (fresh handler per invocation) but fragile.
- **Fix shape:** capture pending state in closures at `Dispatch*` sites (local
  vars), so no instance field is read from callbacks. Alternative: throw on
  second `Begin` before `Resolve`.
- **Revisit window:** must land before a second encounter handler is authored
  to the same shape (Merchant handler is likely next). Bundle with P1-4/P1-5.

### Theme G — `_pendingRunSeed` naming (opportunistic)

- `Run/NodeEncounter/EventHandler.cs:74` — field named `_pendingRunSeed` but
  only Ambush reads it. Rename to `_pendingCombatSeed` or drop entirely by
  capturing as local in DispatchAmbush closure. Pairs with Theme F.

### Theme H — `EventModalHost.cs:193` `FindAnyObjectByType` on SceneReady (revisit: next beacon-host slice)

- Pattern of "activator lookup deferred to view-side scan" — pass
  `BeaconActivator` reference into `SceneReady` event args instead. Before
  Merchant/Chopshop hosts copy it.

---

## 5. P3 Opportunistic

- `Run/StormState.cs` — arithmetic duplication between `PreviewAdvanceCounter`
  and `PeekNextCounter` (2-caller ADR-0011 threshold). Watch; extract on 3rd.
- `Combat/CombatLoop.cs:598` — `ResolveEnemyIntent` list alloc per enemy turn
  (~10-40 per combat). Non-issue.
- `Combat/CombatLoop.cs:790,813` — `_hand.Insert(0, pulled)` O(N). Non-issue
  at hand size ≤ 5.
- `Combat/Vehicle.cs:199-210` — `GetDamagedSlots` alloc per call. Rest picker
  only, not per-frame.
- `Run/NodeEncounter/EventHandler.cs:157-188` — `RollKind` walks weight table
  twice; second pass is a correctness net.
- `Run/BiomeWebGenerator.cs` — per-run-start alloc. Non-issue.
- `CombatView/StormAdvanceVisualPacer.cs:123-140` — add explicit
  `_host == null` early-out (defense-in-depth, one line).
- `UI/MapViewController.cs:922-929` — investigate whether beacon pool retains
  nodes across rebuilds; if so, skip re-subscription for retained.
- `CombatView/RunHUDController.cs` — verify USS class rename
  `wr-storm-pill` → `wr-storm-counter` propagated (grep zero-hit check).
- `UI/DialogueSceneController.cs:86-91` — `_illustrationBgCache` unbounded;
  add LRU cap (16) or clear on scene teardown.
- `MapBeaconStyleSO.cs:46,53` — hardcoded sprite array length `9`; OnValidate
  resize is the actual guard.
- `Editor/CardAssetMigrator.cs` — **overdue for deletion since 2026-06-02**.
  See §7 ADR-0011 drift.
- `Editor/RussoOneFontSetup.cs` — delete-then-recreate GUID churn; TMP wire
  re-established same run.
- `BeaconSceneBindingSO.cs:143` roster contradiction — see §7 for the actual
  action.
- `CombatBalanceSO.cs:8-38` — under-populated relative to stated ambition;
  landing zone for future tuning moves.
- Duplicated `EnsureFolder` helper across authoring scripts — DRY on next add.
- `FuelState.ComputeDrain` xmldoc says `max(1, ...)` but code enforces
  `max(2, ...)` — **borderline P2**, doc drift; one-line xmldoc fix. Cheap
  enough to bundle with P1-1/P1-2 model work if convenient.

---

## 6. Cross-Cutting Patterns Worth Codifying

Consolidated + deduplicated from the three audits' 6+6+5 cross-cutting sections:

### Pattern 1 — Derived-state caching (ADR candidate)
Getters called from per-frame paths whose inputs are stable across most
frames should cache the answer and invalidate on named mutation seams. Recurs
across `ForwardEdgesFrom`, `IsStrandedForFuel`, `Vehicle` computed getters,
and will recur for every future HUD-widget poll. **Recommend:** codify as a
short note in `technical-preferences.md` OR draft a dedicated
`ADR-0016 Derived-State Caching Pattern`. Prefer the ADR if we want CI grep
teeth (e.g., flag `public int Get*()` methods on POCO that call `foreach`).

### Pattern 2 — OnValidate as ship-blocking authoring discipline
Every SO added pre-2026-07 ships `OnValidate`; three Event-slice SOs don't.
**Recommend:** memory entry — `feedback_onvalidate_ship_blocking` — "New
authoring SO ships with `OnValidate` guardrails on every invariant the runtime
asserts. Treat missing OnValidate the same as missing docstring."

### Pattern 3 — Parallel-storage smell on shared-number authoring
Choice `_rewardCount` + payload `_scrapAmount` + payload `_perChoiceScrapReward`
= three spellings of the same number. Single-source-of-truth authoring surface
required. Same shape will bite Merchant (price on offer vs price on receipt vs
price on tooltip) and Chopshop (part cost on offer vs part cost on inventory
line). **Recommend:** call out in the Merchant/Chopshop TD brief when it
lands; consider a memory entry — `feedback_reward_number_single_spelling`.

### Pattern 4 — `Configure(...)` optional-parameter builder trap
Aligns with existing memory `feedback_default_param_overload_semantic_trap`.
Extension: for SOs where fields split by discriminator (like
`EventPayloadKind`), prefer per-kind explicit builder methods over
`Configure(kind, ..., 8 optional params...)`. **Recommend:** extend the
existing memory rather than a new one — add a "SO Configure()" bullet.

### Pattern 5 — Author-menu file discipline
`AuthorDialogueSceneRoot.cs`, `AuthorRunHUDHost.cs`,
`AuthorBiome1MapThemeIcons.cs` are the model. New authoring routines go in
sibling files, not appended to `CombatPrefabAuthor.cs`. **Recommend:**
memory entry — `feedback_authoring_file_per_target`.

### Pattern 6 — Subscription lifecycle discipline (universally applied)
Every audited controller correctly pairs Bind↔OnDestroy (external publisher)
vs OnEnable↔OnDisable (locally-queried UIElements). The 2026-07-04
CardRewardPicker lesson has propagated. **No follow-up needed** — existing
memories `feedback_subscription_lifecycle_pairing` and
`feedback_uitoolkit_subscription_lifecycle` are load-bearing and healthy.

### Pattern 7 — UIDocument race handling (universally applied)
`[DefaultExecutionOrder(-100)]` cover-live-session guards appear across every
UIDocument controller. `DialogueSceneController` nulls cached VisualElement
pointers on `OnDisable`. **No follow-up needed** — existing memories
`feedback_uidocument_negative_exec_order` and
`feedback_uidocument_setactive_reclone` are load-bearing and healthy.

### Pattern 8 — Save-layer as reference pattern
Save-layer discipline (dotted-snake SystemIds, self-describing SCHEMA_VERSION,
atomic writes with checksum validation, per-DTO partial-skip, group-of-one vs
resume-atomic membership) is exemplary. When future systems need persistence
(pending-offer DTOs, mastery state, unlocks), extend the pattern verbatim.
**No follow-up needed.**

### Pattern 9 — Doc-code drift sweep pre-1.0
`FuelState.ComputeDrain` xmldoc drift (`max(1)` vs code `max(2)`) is a small
instance of a broader risk — xmldocs across model layer are dense and
rules-heavy, and drift when reversals land (e.g., 2026-07-26 storm counter
parity reversal). **Recommend:** schedule a one-time xmldoc sweep before 1.0.
Grep xmldoc numeric literals / formulas, cross-check against code.
Automate as CI grep check if drift instances are numerous enough.

---

## 7. ADR-0011 Drift Status

Per-subsystem one-liner:

| Subsystem | Status | Note |
|---|---|---|
| Save layer | **clean** | Exemplary discipline; reference pattern. |
| `Run/` POCO (Session, State, NodeMap, Storm, Fuel, RunDeck, ScrapEconomy) | **clean** | Zero bridges detected. Pending-offer persistence gap is a coverage gap, not a bridge. |
| `Combat/` POCO (CombatLoop, Vehicle, Deck, DamagePipeline) | **clean** | ADR-0007/0012 shape holds. |
| `Run/NodeEncounter/` interfaces (9 files) | **clean** | Well-documented ADR-0002/0011 discipline. |
| `View/UI` layer | **clean** | ADR-0014 primary-stack discipline holds. Popups is documented UGUI exception. |
| Authoring SOs (pre-Event-slice) | **clean** | All ship OnValidate; ADR-0015 narrowing preserved. |
| Authoring SOs (Event-slice) | **needs-work** | Parallel-storage on choice/payload (P1-5); missing OnValidates (P1-4). Fixable this session. |
| `Editor/CardAssetMigrator.cs` | **debt-tolerated, overdue** | One-shot migrator per ADR-0011 exception #1; ADR-0010 complete since 2026-06-02, migrator should have been deleted then. **Action: schedule delete on next non-slice housekeeping day.** Verify no CI grep gate references it first. |
| `BeaconSceneBindingSO` roster | **needs-reconciliation** | Memory `project_scene_split_hybrid_verdict` says Rest/Haven/Merchant/Event/Chopshop all PrefabRoot; roster today has Rest+Event PrefabRoot but Haven/Merchant/Chopshop still AdditiveScene. Either memory is stale OR migration is incomplete. **Action:** cheap-verify (5 min) — confirm which is current truth, then either update memory or plan the migration. Not this session unless trivial. |

---

## 8. What This Pass Did NOT Cover

**Non-exhaustive audit disclaimer.** The three parallel audits targeted highest-value grep patterns + fresh Event-slice code + known pre-2026-07-27 anchors, not exhaustive file-by-file coverage.

Specifically deferred / lightly-touched:

- **View/UI layer:** ~65% of the 79 files touched by grep only, not read in
  full. Fresh Event-slice files (`EventModalHost`, `DialogueSceneController`)
  + high-Update-count files (`StormAdvanceVisualPacer`, `MapViewController`)
  + known P1 anchor got full reads. Older widgets, tooltip lifecycles,
  animation coroutines: not audited.
- **Combat layer:** `CombatController` view/facade layer, `EnemyIntent`
  telegraph presenters, deck-animation choreography: not audited in depth.
- **Enemy AI / brains:** `EnemyBrain*` files: not audited.
- **Testing infrastructure:** `Assets/Tests/` not swept; assumed green per
  latest EditMode run (no explicit verification for this audit).
- **Shader / VFX layer:** not in scope for this pass.
- **Scene/prefab YAML:** not audited (would require Editor read of `.unity` /
  `.prefab` YAML for override drift).
- **Framework repo `src/`:** off-limits per memory `project_adr_0010_complete`
  (4-slot/variable-N trap); not audited.

Follow-up P3 sweeps (biome-2 or SlotTargetRing HUD refactor timings) could
pick up smaller-scoped findings in the deferred areas.

---

## Ship Signal

- **P1 count:** 5 discrete pieces of work (grouped as 2 bundles + 1 standalone).
- **Top-priority fix:** Event-slice SO parallel-storage cleanup (P1-5)
  bundled with the three missing `OnValidate`s (P1-4) — must land before
  Merchant/Chopshop authoring copies the same shape.
- **No shipping blockers.** All P1s are hardening, not correctness.
- **No cross-audit contradictions.**

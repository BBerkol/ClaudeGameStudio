# TD Verdict — Structural Debt Cleanup Batch (Items 1–3 + ADR-0018 P4b ruling)

**Date:** 2026-09-17
**Author:** technical-director
**Source of truth:** `production/remediation-plan-2026-09-08.md` §4.2 (lines 611–622),
§5.4 (lines 1049–1084), Progress line 26, §5.2 (lines 837–864, 977)
**Code repo:** `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\`
**Governing docs:** ADR-0011 (no bridges), ADR-0018 (UGUI retention registry),
`production/postmortems/2026-09-17-p4a-migration-regressions.md` (P1/P4/P6)

Every claim below was verified by direct code read this session. Where the plan
or the brief was wrong, it is marked **CORRECTED** with the evidence.

---

## TD Verdict

| Item | Verdict |
|---|---|
| **1 — `AdvanceReason` + `BeaconTransition.Reason` deletion** | **APPROVE** — with the deletion order in §1.3 and the test-assertion replacement in §1.4 |
| **2 — `BuildScout` / `_playerVehicleAsset` fallback retirement** | **APPROVE WITH RESHAPE** — the brief's two candidate homes are both wrong; the factory does not relocate, it **dies** and the fixtures load the shipping `Vehicle_Scout.asset` from `Resources`. §2 |
| **3 — `Assets/Scenes/CombatScene.unity` orphan** | **APPROVE** — cleanest item in the batch; three-file change, zero risk surface. §3 |
| **P4b ruling** | **NEITHER named option. RE-DERIVE the ADR-0018 category in place (Amendment A), seat `HudAnchors` under the corrected invariant, and do NOT migrate it in P4.** §4 |
| **P5 close in this batch** | **NO** — blocked on a fresh canvas audit; ADR-0018's registry is already proven wrong on one row. §5 |

All three cleanup items may ship in one batch. The P4b ruling is a document
change only and ships alongside. **Order: Item 3 → Item 1 → Item 2 → P4b
amendment.** Rationale in §6.

---

## 1. Item 1 — `AdvanceReason` + `BeaconTransition.Reason`

### 1.1 What the plan claimed, and what is actually true

| Plan claim | Verified |
|---|---|
| Zero *production* readers of `.Reason` | **TRUE.** `\.Reason\b` across `Assets/` returns exactly **one** `BeaconTransition` hit: `RunController_HappyPath_Test.cs:229`. The other four hits are `VehicleMotionState.MotionReason` in `VehicleRestPose_show_restoresOnHide_test.cs` — a different type, unrelated. |
| `BeaconType.cs:41-46` claims `Reason` is "Persisted on `BeaconTransition`" and that is FALSE | **TRUE — and worse than stated.** `BeaconTransition` is a `readonly struct` that is never serialized; the word "persisted" in that xmldoc means "carried on the struct", but a reader naturally reads it as save-persistence. It also names three consumers ("save snapshots, map UI animation, and Node Encounter dispatch") that do not read the field. |
| 4-layer signature change | **TRUE**, and the layer list is right. |
| ~11 test sites | **CORRECTED — 141 occurrences across 27 files** (`AdvanceReason|\.Reason` count). The *edit* is mechanical (drop one argument), but the blast radius is 22 test files, not 11 sites. Size the slice accordingly. |

### 1.2 Save / DTO contact — the question you asked, answered

**Zero contact. Verified two ways:**

1. `grep -n "Reason" Assets/Scripts/Save/**` → **no matches** across the entire
   `WastelandRun.Save` assembly (`Dtos/`, `Adapters/`, `SaveSystem*.cs`,
   `Envelope.cs`, `WriteIntent.cs`).
2. The 11-file `BeaconTransition` grep contains **no file under
   `Assets/Scripts/Save`** and no test under `Assets/Tests/EditMode/Save`.

`BeaconTransition` is a transient return value consumed inside
`RunSession.Advance` (fuel/storm/merchant resolution) and discarded. Nothing
round-trips it. **No schema bump, no `SCHEMA_VERSION` change, no registry edit.**
This is a pure compile-time deletion with no save-migration tail.

### 1.3 Deletion order (constraint — follow exactly)

The cut must go **outside-in**, so the compiler is your worklist at every step
and you never have a half-threaded signature that still builds.

1. **Delete the only reader first.** Remove the assertion line
   `RunController_HappyPath_Test.cs:229`. Do this as its own hunk so the diff
   shows the reader dying before the field does. (Replacement assertion: §1.4.)
2. **Drop the argument at the outermost call sites** — production first:
   `Assets/Scripts/CombatView/RunSceneOverlayHost.cs` (`:289`, `:337`, `:345`).
3. **Drop the parameter from the host:**
   `Assets/Scripts/CombatView/RunSceneHost.cs` `:961` signature **and the
   `<param name="reason">` xmldoc block at `:955-956`**, which currently says
   *"Carried as telemetry signal; the host does not branch internally on this
   value."* There is no telemetry consumer — deleting the parameter without
   deleting that sentence leaves a false claim behind, which is the exact
   failure mode that let this survive prior audits.
4. **Drop the parameter through the model layers, in this order:**
   `Assets/Scripts/Run/RunSession.cs` `:477` → `Assets/Scripts/Run/RunController.cs`
   `:725` → `Assets/Scripts/Run/INodeMapMutator.cs` `:22` → 
   `Assets/Scripts/Run/NodeMap.cs` `:267`. Interface **after** its implementer
   so you never have an unimplemented member mid-edit.
5. **Delete the field + ctor parameter:** `Assets/Scripts/Run/BeaconTransition.cs`
   — the `Reason` property `:17`, the ctor parameter `:31`, the assignment `:38`.
   Ctor goes 6 args → 5. **Also revise the struct's own xmldoc `:3-10`**, which
   names the same three phantom consumers.
6. **Delete the enum last:** `Assets/Scripts/Run/BeaconType.cs` `:40-52` — the
   whole `AdvanceReason` declaration *and* its xmldoc. `BeaconType` itself is
   untouched; only the second type in that file dies.
7. **Sweep the test call sites.** All are `X(n, AdvanceReason.Departure)` →
   `X(n)`. Files: `RunController_HappyPath_Test.cs`, `RunSession_Test.cs`,
   `RunSession_CardReward_Test.cs`, `RunSession_Fuel_Test.cs`,
   `RunSession_PartReward_Test.cs`, `RunSession_PreviewBeaconArrival_Test.cs`,
   `RunSession_ResolveRest_test.cs`, `RunSession_Reward_Test.cs`,
   `RunSession_TerminalLifecycle_Test.cs`, `RunSession_Advance_Gates_Test.cs`,
   `NodeMap_AllowBidirectional_Test.cs`, `NodeMap_BiomeIdPathHistory_Test.cs`,
   `RunSceneHost_Test.cs`, `RunSceneHost_EnqueuesWrite_Test.cs`,
   `RunHUDController_MapVisibility_Test.cs`, `SceneEncounterBuilder_Test.cs`,
   and the two PlayMode files `ChopshopBeaconRevisit_Test.cs`,
   `ChopshopRepairMode_Test.cs`.

**Do not** introduce a default-parameter overload to soften step 4. That is
ADR-0011 forbidden #5 and, per memory `feedback_default_param_overload_semantic_trap`,
it preserves source compatibility while hiding whether anything still depends on
the old shape. Break every call site in the same commit.

### 1.4 What the tests should assert afterward

`RunController_HappyPath_Test.cs:229` is the only assertion that dies. **Do not
replace it with nothing, and do not replace it with a new field assertion.**
Replace it with an assertion on a field that is actually load-bearing and
currently unasserted at that site:

```
Assert.AreEqual(1, t.StepIndex);   // ADR-0003 seed-mix index
```

Reason: `StepIndex` is the RNG mix term per ADR-0003 and the struct's xmldoc
`:19-24` says it diverges from `ToIndex` on a branching map. Today
`NodeMap.Advance` sets `stepIndex: to.Index` — i.e. they are the same value, and
nothing pins that this is deliberate. Asserting `StepIndex` at the one site that
was already inspecting the struct converts a dying assertion into a live one on
the field that will actually break when M2 branching lands.

**Test-count expectation:** net **0** new tests, **0** deleted tests (the
assertion line moves, the `[Test]` stays). EditMode baseline should return
**1299/1298/0/1 unchanged**. If the count moves, something else changed —
investigate before committing.

### 1.5 Residual risk

Low. The one thing to watch: `AdvanceReason.CombatCleared` appears **only in
tests** (`RunController_HappyPath_Test.cs:215/321/377/383/389/398`,
`RunSession_Test.cs:289`) — always inside `Assert.Throws` blocks where the throw
comes from the *index*, not the reason. Confirm during the sweep that no test's
expected exception message references the reason value; if one does, that test
was asserting on the wrong cause and should be tightened, not mechanically
rewritten.

---

## 2. Item 2 — `BuildScout` / `_playerVehicleAsset` fallback

### 2.1 The brief's framing does not survive contact with the code

Four corrections, each load-bearing on where the factory goes.

**CORRECTED (a) — `BuildScout` is not "the EditMode suite's vehicle factory."**
The EditMode suite does not call it. Twelve test files declare their **own
private static `BuildScout`**, copy-pasted, never calling the production one:
`VehicleStateSerializable_test.cs:25`, `MerchantVisitsSerializable_Test.cs:47`,
`PendingEventOfferSerializable_test.cs:39`, `RunDeckSerializable_test.cs:41`,
`RunSeedSerializable_test.cs:40`, `PendingCardOfferSerializable_test.cs:44`,
`RunInventorySerializable_test.cs:40`, `PendingPartOfferSerializable_test.cs:39`,
`LoadedRunSnapshot_Test.cs:52`, `RunSession_ResolveRest_test.cs:25`,
`VehicleGetDamagedSlotsTests.cs:18`, `VehicleRestoreSlotStateTests.cs:23`.
The *only* caller of `RunSceneHost.BuildScout` is the reflection probe in
`ScoutFallback_PartIdParity_Test.cs:157-168`. **Thirteen construction paths for
one vehicle** — that is ADR-0011 #2 (parallel storage) at a scale the plan did
not see, and it is the actual defect. Relocating the production copy to a
thirteenth home fixes nothing.

**CORRECTED (b) — `Vehicle_Scout.asset` already exists at a `Resources` path.**
`Assets/Resources/combat/Vehicles/Vehicle_Scout.asset`. It is loadable by
`Resources.Load<VehicleDefinitionSO>("combat/Vehicles/Vehicle_Scout")` from
EditMode tests, PlayMode tests, and shipping builds alike. The brief's two
candidate homes (test-asmdef static helper vs. a *new* SO fixture) both build
something that already exists.

**CORRECTED (c) — `BuildScout`'s own comment is false.** `RunSceneHost.cs:1632-1636`
says the fallback fires "when no `VehicleDefinitionSO` is wired **and no
`Resources/Combat/Vehicles/Vehicle_Scout` asset exists**." `RunSceneHost` has no
`Resources.Load` path anywhere — the only two construction sites are `:625-627`
and `:797-799`, both a bare null-check ternary. Same failure class as Item 1's
`BeaconType.cs:41-46`: an xmldoc asserting a mechanism that was never built.

**CORRECTED (d) — the asmdef constraint is real, and it eliminates one of the
two candidate homes.** `WastelandRun.Combat.Tests.asmdef` (home of
`TestFrameLayouts.cs` and `TestVehicleFactory.cs` under
`Assets/Tests/EditMode/Combat/Helpers/`) declares `"includePlatforms": ["Editor"]`.
`WastelandRun.CombatView.PlayMode.Tests.asmdef` declares `"includePlatforms": []`
and does **not** reference `WastelandRun.Combat.Tests`. A platform-unconstrained
assembly cannot reference an Editor-constrained one, so a
`Combat.Tests` static helper **cannot serve `ChopshopBeaconRevisit_Test.cs` or
`ChopshopRepairMode_Test.cs`** — and both of those `AddComponent<RunSceneHost>()`
and therefore ride the unwired fallback today. The brief's instinct here was
correct; the reason is the platform constraint, not the `Assets/Editor` folder.

**CORRECTED (e) — "20 fixtures" is 10 files.** `AddComponent<RunSceneHost>` appears
in exactly ten: `RunHUDController_MapVisibility_Test.cs`,
`RunSceneHost_EnqueuesWrite_Test.cs`, `RunSceneHost_RestResume_test.cs`,
`RunSceneHost_Resume_Test.cs`, `RunSceneHost_SeedRetry_Test.cs`,
`RunSceneHost_Test.cs`, `SaveBootstrap_Test.cs`,
`SaveRegistry_Completeness_Test.cs` (EditMode) plus
`ChopshopBeaconRevisit_Test.cs`, `ChopshopRepairMode_Test.cs` (PlayMode). The
"20" is presumably test *methods*; either way, ten files to edit.

### 2.2 The reshaped remedy — the factory dies, it does not move

**Production keeps exactly one construction path.** Delete the fallback entirely
and let the fixtures wire the shipping asset, using the injection mechanism the
fixtures **already use**.

Both suites already reflect private serialized fields into `RunSceneHost`:
`RunSceneHost_Test.cs:232-235` does `typeof(RunSceneHost).GetField("_biomeDistribution", ...)
.SetValue(host, so)`, and `ChopshopBeaconRevisit_Test.cs:187` calls
`InjectField(_host, "_biomeDistribution", _biomeSo)`. Adding one line per fixture —

```
InjectField(_host, "_playerVehicleAsset",
    Resources.Load<VehicleDefinitionSO>("combat/Vehicles/Vehicle_Scout"));
```

— costs ten lines total, uses a proven mechanism in both assemblies, needs no new
asmdef reference, and makes every fixture exercise the **shipping data** rather
than a code-shaped shadow of it (memory `feedback_exercise_the_real_shape`).

This is strictly better than relocation on every axis that matters:

- The three value divergences (`ChassisCards` `:653-656`, `TankCapacity`
  `:1488-1490`, `FuelBurnMultiplier` `:1528-1530`) do not need deleting
  one-by-one — they **cannot exist**, because there is no second source.
- `ScoutFallback_PartIdParity_Test.cs` deletes wholesale. It pins parity between
  two paths; with one path there is nothing to diverge. Deleting a test is
  normally a smell — here it is the proof the slice worked.
- The 12 duplicated test-local `BuildScout` copies get a consolidation target for
  the first time. **Do not consolidate them in this slice** (see §2.4).

### 2.3 Concrete file-by-file scope

**Production deletions — `Assets/Scripts/CombatView/RunSceneHost.cs`:**

| Lines | What |
|---|---|
| `:49-53` | `_playerVehicleAsset` tooltip — rewrite; drop the "BuildScout fallback fires only if…" sentence. Field itself **stays** (it is the one path). |
| `:625-627` | Ternary → `_playerVehicleAsset.BuildVehicle("Scout")` |
| `:653-656` | Ternary → `_playerVehicleAsset.ChassisCards` |
| `:660-687` | Collapse the split-severity deck warning to the single `LogError` arm. The `else` arm exists only to excuse the fallback. (The plan is right that `:669-686` is not a fourth divergence — both arms only log and `StartRun` runs identically — but with the fallback gone the `else` is unreachable.) |
| `:797-799` | Ternary → `_playerVehicleAsset.BuildVehicle("Scout")` |
| `:1465-1470` | Delete `SCOUT_TANK_CAPACITY_FALLBACK` + `SCOUT_FUEL_BURN_MULTIPLIER_FALLBACK` and the comment block |
| `:1488-1490` | Ternary → `_playerVehicleAsset.TankCapacity` |
| `:1523` / `:1528-1530` | Ternary → `_playerVehicleAsset.FuelBurnMultiplier`; the xmldoc at `:1523` references `BuildScout` — rewrite |
| `:1533-1570` | `_cachedSmallFrame` + `GetSmallFrameLayout()`. **Constraint:** grep `GetSmallFrameLayout` for a second caller before deleting. If `BuildScout` is the only one, both die, and the comment at `:1536-1538` ("Lives here rather than in a shared test helper because BuildScout is production code — sharing with TestFrameLayouts would recreate the very production/test bridge Phase 2.5A retires") dies with them. That comment's premise dissolves the moment `BuildScout` stops being production code. |
| `:1632-1657` | `BuildScout` itself, comment included |

**Add a null guard.** `BeginNewRun` must **throw** on a null `_playerVehicleAsset`,
matching the existing `_biomeDistribution` contract (`RunSceneHost.cs:55-60`
tooltip: *"BeginNewRun throws if null"*). Without the fallback, an unwired asset
must be loud and immediate, not a `NullReferenceException` three frames later.

**Test edits:**

- Delete `Assets/Tests/EditMode/CombatView/ScoutFallback_PartIdParity_Test.cs`.
- Add the `_playerVehicleAsset` injection line to the ten fixture files listed in
  §2.1(e).
- `Assets/Tests/EditMode/Combat/VehicleSwapPartTests.cs:151` — the comment
  *"(closes the BuildScout player-fallback hole — TD 2026-08-04)"* references a
  hole that no longer exists. Update the prose; **do not** delete the test.
- `Assets/Tests/EditMode/CombatView/RunSceneHost_Resume_Test.cs:358` and `:492`
  carry `BuildScout`-mirroring comments with line references that will rot —
  update.
- `Assets/Tests/PlayMode/CombatView/ChopshopBeaconRevisit_Test.cs:404` and
  `ChopshopRepairMode_Test.cs:897` ("Mirrors `RunSceneHost.BuildScout`'s part
  shape") — update to reference `Vehicle_Scout.asset`.
- `Assets/Tests/EditMode/Combat/SmallFrameLayoutTests.cs:15` ("Mirror
  `CombatController.BuildScout` numbers") — already stale (that type does not own
  a `BuildScout`); fix while you are here.
- `Assets/Scripts/Combat/Vehicle.cs:410` and `Assets/Scripts/Run/RunDeck.cs:186`
  both cite `BuildScout` in xmldoc — update.
- `Assets/Editor/CombatDataInitializer.cs:15` ("Default values mirror … the
  BuildScout helper") — update.
- `Assets/Editor/CombatPrefabAuthor.cs:6603-6606` — the `AuthorRun` abort message
  says the run "would silently fall back to BuildScout, whose deck cannot win."
  After this slice it would throw, not fall back. Rewrite the message. **Keep the
  abort** — it is the containment that 5.1 Commit A shipped.

**Wiring test (plan §5.4's own ask):** add the row
`(Run.prefab, RunSceneHost, _playerVehicleAsset)` to
`Assets/Tests/EditMode/CombatView/CombatWidgetPrefabWiring_Test.cs`. Verified
present in the shipping prefab: `Assets/Prefabs/Run/Run.prefab:128` carries
`_playerVehicleAsset: {fileID: 11400000, guid: bd2b36318ec4cb9428abb3687d3b810d}`.
**This row must land in the same commit as the deletion**, not before and not
after — it is the only thing standing between an unwired prefab and a hard throw
at `BeginNewRun`.

### 2.4 Explicitly out of scope — do not do these in this slice

- **Consolidating the 12 test-local `BuildScout` copies.** Real debt, but it
  spans four assemblies, two of which cannot reference a shared Editor-only
  helper (§2.1(d)), so the consolidation is not one refactor — it is a
  helper-per-assembly design question. Spinning it into this slice turns a
  bounded deletion into an open-ended one. Log it as **§5.4b** in the plan.
- **The `InjectField` duplication** between the EditMode and PlayMode fixtures.
  Two callers, structurally forced apart by the platform constraint. Under
  ADR-0011 this is *not* the "share before the third caller" case — there is no
  legal shared home. Leave it.

### 2.5 Test evidence required before this closes

- EditMode green, with the count stated: expect **1298** (baseline 1299 minus the
  deleted `ScoutFallback_PartIdParity_Test`; confirm that file contributed
  exactly one `[Test]` before accepting the number).
- PlayMode **17/17** — the two Chopshop fixtures are the ones most likely to
  break, since they move from a code-built vehicle to the authored asset. If a
  Chopshop test reds on part HP or slot identity, that is **the authored asset
  disagreeing with the old fallback** — a real finding, not a test to patch.
  Report it before changing either side.
- Per memory `feedback_gate_check_requires_green_tests`: compilation green is not
  semantic green. Paste the counts.

---

## 3. Item 3 — `Assets/Scenes/CombatScene.unity`

**APPROVE.** Every claim verified; nothing to correct.

- The scene's guid `8c9cfa26abfee488c85f1582747f6a02` is referenced in exactly
  **two** places repo-wide: its own `Assets/Scenes/CombatScene.unity.meta:2` and
  `ProjectSettings/EditorBuildSettings.asset:13`.
- **No build-index dependency exists.** `grep -rn "buildIndex|GetSceneByBuildIndex"`
  across `Assets/` returns **zero hits**, so removing the middle entry of three
  cannot shift anything that is read. (This was the one real risk in the item.)
- **Nothing re-adds it.** `CombatPrefabAuthor.cs:6913` defines
  `CombatScenePath = "Assets/Scenes/Beacons/Combat.unity"` — a *different* file —
  and `EnsureSceneInBuildSettings` at `:7454` is called with that path. The
  orphan is not on any author path.
- The only prose reference is `Assets/Scripts/CombatView/BeaconActivator.cs:84`,
  inside the `_lastActivatedBeacon` comment: *"Combat A → Combat B share
  `CombatScene.unity`."* The statement's *logic* is correct and still
  load-bearing — only the filename is wrong. **Fix it to `Beacons/Combat.unity`;
  do not delete the comment**, it documents the Bug-6 activation-tracker
  rationale.

**Files touched:** `Assets/Scenes/CombatScene.unity` (delete),
`Assets/Scenes/CombatScene.unity.meta` (delete),
`ProjectSettings/EditorBuildSettings.asset` (remove the middle `m_Scenes` entry),
`Assets/Scripts/CombatView/BeaconActivator.cs` (comment `:84`).

**Acceptance:** after deletion, run `Author All Scenes` and confirm
`EditorBuildSettings.asset` comes back with **two** entries and an otherwise
empty diff — the same stronger-than-asked criterion Phase 4.1 used.

---

## 4. P4b ruling — neither migrate-with-spike nor registry entry

### 4.1 A finding that reframes the question

**ADR-0018's registry contains a factual error, on the same class of claim that
sank ADR-0014.** The `Popups` row reads: *"Exit criterion: Damage numbers stop
tracking world transforms…"* — but **damage numbers do not render on `Popups`.**

- `DamagePopupSpawner` builds and owns its own canvas: `DamagePopupSpawner.cs:58-72`
  creates `DamagePopupCanvas`, `RenderMode.ScreenSpaceOverlay`, `sortingOrder 30`,
  and instantiates the 8-widget pool into it (`:83-92`). The prefab
  `Assets/Prefabs/CombatView/DamagePopupSpawner.prefab:15/49/61` confirms the
  serialized copy: name `DamagePopupCanvas`, `m_RenderMode: 0`,
  `m_SortingOrder: 30`.
- `CombatHud._popups` **is** that spawner; `PopupOnEnemySlot` / `PopupOnPlayerSlot`
  (`CombatHud.cs:722-733`) route straight to `_popups.Show(anchor, …)`.
- What actually lives on the `Popups` canvas (order 60) is the **buff tooltip** —
  `CombatHud.cs:1114-1121` (`PopupsGroup()`) and `BuffTooltipWidget.cs:65`.

So ADR-0018 §4's own open question — *"whether damage numbers actually render
[on `DamagePopupCanvas`] or on `Popups` was not established by this audit"* — is
**now established: they render on `DamagePopupCanvas`.** That canvas is
transient, per-event, and positioned from a live world anchor
(`DamagePopupSpawner.Show` converts the anchor's world centre → screen → local;
`DamagePopupWidget.cs:66/74/88` then self-animates from the captured start
position). **It passes ADR-0018's invariant cleanly and is a member on its
merits.**

**Consequence for the cap arithmetic in your brief:** the registry is not 4/5
with one slot free. Once `DamagePopupCanvas` takes the seat it has earned, it is
**5/5 with zero headroom**, and admitting `HudAnchors` makes it **6 — which
*exceeds* the stated cap and triggers the ADR's own re-derivation clause.** The
question "can `HudAnchors` take the last slot" has no valid answer; the slot is
already spoken for.

### 4.2 Why `HudAnchors` must not simply be admitted

ADR-0018 §1 names `HudAnchors`, MainBar and the rings **by name as failing the
invariant** — *"persistent HUD state with an authored layout, which is precisely
why P4 wants them in USS."* Admitting a surface the document explicitly excludes,
on cost grounds, converts a semantic category into an escape hatch. The ADR
predicted this exact move and pre-rejected it: *"Granting membership to an
unexamined canvas is how registries rot."*

### 4.3 Why `HudAnchors` must not migrate in P4 either

The argument is **not** performance, and deliberately so — ADR-0018 §3 withdrew
the performance rationale as never-measured, and I will not reinstate an
unmeasured one. Bounded cost analysis, for completeness and as the record:

> ~8–10 anchored elements per vehicle (MainBar + `weapon_0/1/2`, `engine_0`,
> `mobility_0`, `slot_exposable_1/2` per `CombatPrefabAuthor.cs:1119-1152`) × 2
> vehicles on screen ≈ **16–20 elements**. Per element per frame:
> `WorldToScreenPoint` (one matrix multiply) + `RuntimePanelUtils.ScreenToPanel`
> + two inline `style.left/top` writes. The arithmetic is negligible (single-digit
> µs for the whole set). **The real cost is that inline position writes dirty
> layout**, so a currently-native-batched canvas cost becomes a C#-side layout +
> repaint pass every frame the vehicles move — which is most of combat, including
> the position-swap animation. That is very likely *affordable*. It is also not
> the deciding factor, and a spike that measured it would answer the wrong
> question.

The deciding factor is **authoring**, and it cuts the opposite way from ADR-0014's
general argument:

- Anchor placements are **hand-placed in Prefab Mode against the live vehicle
  sprite**, per-archetype, then baked into `spec.AnchorPositions`
  (`CombatPrefabAuthor.cs:1121-1152`, `SeedHudAnchor` `:1233-1290`,
  `ResolvePos` override table). `VehicleHudAnchors.cs:14-22` records *why*: the
  prior UV-as-numeric-proxy scheme "was correct math but couldn't accommodate
  per-prefab visual placement when chassis proportions diverge — Dredge in
  particular drifted."
- **Migration does not delete that data; it relocates it into a format with no
  direct-manipulation affordance.** UI Toolkit has no scene graph, so per-slot
  offsets must become a per-archetype numeric table converted world→panel each
  frame. The designer loses eyeball-against-the-sprite placement and regains the
  numeric-table workflow that `VehicleHudAnchors` was *created to escape*.
- That cost multiplies across the 1.0 roadmap: player + three enemy archetypes
  today, ADR-0017 chassis 2/3, plus the parts axis's visual part swaps
  (memory `project_parts_axis_in_1_0`). It is a direct violation of
  `feedback_designer_friendly_default`.

Layer on the postmortem's own gates and the case closes:

- **P6 (mechanism novelty).** P4b is the first *per-frame* world→screen→panel
  conversion, the first migration of a **designer-hand-placed** layout, and it
  reintroduces the y-flip convention that produced D2/D4. Three first-occurrence
  mechanisms in one slice, on the largest remaining surface.
- **P1 (convention pair table + rendered check).** The conversion axis list here
  is longer than slice 6's: world-unit origin, y-sign (`ScreenToPanel` is
  y-DOWN), per-vehicle local scale (`SeedHudAnchor` stamps `localScale 0.01`),
  canvas reference resolution, pivot. Every one is a D4 candidate.
- **The critical path.** P4b unlocks nothing for chopshop. The user's stated goal
  is clearing pre-chopshop debt; P4b is the one item in this batch that would
  *create* debt-shaped risk rather than retire it.

### 4.4 The ruling

> **`HudAnchors` does not migrate in P4. The ADR-0018 category is re-derived in
> place as Amendment A, under which `HudAnchors` is a member on its merits rather
> than as an exception, and P4 closes on P4a scope.**

Re-derivation, not extension, is what the ADR itself prescribes once the cap is
exceeded. Doing it *now* — four days after the ADR landed, while the canvas audit
is fresh in hand — is far cheaper than doing it after a sixth member forces it.
It is one amendment section on an existing document, not a new ADR
(memory `feedback_prefer_code_over_doc_churn`).

**The corrected invariant** (drop `transient`, which was descriptive of the first
four members and never causal; name the mechanism that actually resists USS):

> UGUI is retained for canvases whose element **position or hit-shape is derived
> from a live scene transform or sprite** — scene-graph parenting, per-frame
> world-transform following, or sprite-alpha hit testing. UI Toolkit has no scene
> graph and no sprite-alpha hit test; these surfaces would have to re-implement
> one in C#, which is a reimplementation, not a migration.

Under it, all six members qualify by the same mechanism, and `Combat_HUD`,
`Debug`, `CardHand` and `BuffStripCanvas` still fail it — the category retains
its teeth.

**Amendment A must contain, at minimum:**

1. **Correct the `Popups` row.** Its member is the **buff tooltip**, not damage
   numbers. New exit criterion: *"The buff tooltip stops positioning from a
   hovered widget's world rect (`BuffTooltipWidget.cs:172-174`) — i.e. tooltip
   anchoring moves into panel space."*
2. **Seat `DamagePopupCanvas`** with the exit criterion previously misfiled under
   `Popups`: *"Damage numbers stop projecting from a live world anchor, or
   `RuntimePanelUtils.ScreenToPanel` is measured acceptable at the 8-popup pool's
   peak spawn density."*
3. **Seat `HudAnchors`** (MainBar, per-slot rings, enemy badges and the nested
   `BuffStripCanvas` count as its children, **not** as separate members — state
   this explicitly or the count rots).
4. **Restate the invariant** per above, and say in one line that `transient` was
   dropped because it was descriptive, not causal.
5. **Reset the cap with a new trigger.** Six members, re-derive at **>7**. State
   the new threshold explicitly; a cap silently carried forward is worse than no
   cap.
6. **Record the audit correction** in the Consequences section: this is the
   *second* canvas-identity error in the ADR-0014/0018 lineage, and it was found
   by a code read, not by the YAML audit — so ADR-0018's Validation Criteria
   ("the canvas inventory table matches a fresh YAML read") is necessary but
   **not sufficient**. YAML tells you a canvas exists; only code tells you what
   renders into it. Add that sentence.

### 4.5 `HudAnchors` exit criterion — drafted text

> **`HudAnchors` (incl. MainBar, per-slot `SlotTargetRing`s, `EnemyNumberBadge`s,
> nested `BuffStripCanvas`)** — migrates when **both** hold:
>
> **(1) Authoring parity.** Per-archetype anchor placement can be authored
> against the live vehicle sprite with the same direct-manipulation affordance
> Prefab Mode provides today (`VehicleHudAnchors._entries` +
> `CombatPrefabAuthor.SeedHudAnchor`'s `AnchorPositions` bake). A blind numeric
> table does not satisfy this — it is the exact workflow
> `VehicleHudAnchors.cs:14-22` was built to retire after Dredge drifted under it.
>
> **(2) Conversion cost bounded by a real measurement.** A **single** follower
> component owns the world→panel conversion for a whole vehicle (one conversion
> per vehicle per frame, fanned out to elements by cached local offsets — not one
> conversion per element), measured at **≤0.3 ms/frame for 2 vehicles × 12
> elements at 1080p** on the reference machine, with the layout-invalidation cost
> of the inline position writes included in the measurement, not just the math.
>
> **Ordering is not symmetric.** Until (1) exists, (2) is irrelevant and must not
> be spiked: a passing performance number would otherwise be used to justify
> trading away a designer capability. Re-open this row when a UI Toolkit
> sprite-relative placement workflow exists, not when someone has spare cycles to
> profile.

### 4.6 Validation — "we will know this was right if"

- The next three vehicle-authoring passes (chassis 2, chassis 3, or any parts-axis
  visual swap) place anchors in Prefab Mode without touching a numeric table. If
  designers end up hand-editing `AnchorPositions` numbers anyway, criterion (1) is
  already effectively lost and the row should be re-opened on different grounds.
- No P4a-class defect (value-transported, render-wrong) appears in the combat HUD
  after P4 closes — because the surface with the most convention crossings never
  crossed them.
- If a future slice needs a *seventh* registry member, that is the signal the
  re-derived invariant is also wrong. Re-derive again; do not extend.

---

## 5. ADR-0018 P5 — what it checks, and whether it closes here

**Under this ruling, P5 concretely checks:** no `Canvas` component exists in any
prefab under `Assets/Prefabs/` or any scene under `Assets/Scenes/` whose
GameObject name is outside the enumerated registry — **`Popups`,
`DamagePopupCanvas`, `HitZonesCanvas`, `IntentCanvas`, `TargetingReadoutCanvas`,
`HudAnchors`** — implemented as a YAML grep in
`tools/ci/grep-gates.sh` (Unity repo; memory `project_ci_enforcement_reality` —
there is no GitHub Actions runner, the commit hook is the enforcement), with the
six names as an explicit literal list and a comment cross-referencing ADR-0018
Amendment A §2. It is a **registry check, not a predicate** — the gate must say
so in a comment, or a future reader will try to extend it by reasoning.

One refinement the ruling makes possible: because `HudAnchors` is now a member,
the gate no longer has to special-case the largest surface in the project, which
is precisely the failure mode ADR-0018 §"Why the obvious replacement predicates
also fail" identified for the vehicle-parentage predicate. The registry approach
survives; the reason it survives is different from what ADR-0018 assumed.

**Can P5 close in the same batch? NO.** Two blockers, both concrete:

1. **A fresh canvas audit is a hard prerequisite, and it is now overdue twice
   over.** ADR-0018's own Validation Criteria say *"Re-verify before P4 opens —
   this document's predecessor was wrong precisely because nobody did."* P4a then
   landed seven slices (through `ea98e8c`) that deleted UGUI widgets and their
   canvases, and §4.1 above proves the existing table is already wrong on one row.
   Writing a gate against a stale, known-defective inventory is how the
   ADR-0014 P5 gate became unshippable on day one. **Re-read every `Canvas` in
   `Assets/Prefabs` + `Assets/Scenes` from YAML, and for each one confirm from
   *code* what renders into it** — the §4.1 error was invisible to a YAML-only
   read.
2. **Non-registry canvases plausibly still ship.** `Combat_HUD` (order 10),
   `Debug` (110), `CardHand` (25) and `BuffStripCanvas` (22) are all P4 scope and
   referenced by live code comments (`CombatHud.cs:73-74`, `:140-142`). P4a
   migrated some of these; which ones is exactly what the audit must establish. A
   gate written today would red on surfaces that are legitimately mid-migration.

**Sequencing:** P5 is a **two-step slice of its own** — (a) fresh audit, code-read
per canvas, reconciled into Amendment A's table; (b) the grep gate, proven to red
on a deliberately-added out-of-registry `Canvas` before it is accepted (the
Phase 4.1 standard: every new gate proven to red on its bug, not assumed). Ship
it as the closing act **after** this batch, not inside it.

---

## 6. Batch sequencing and risk

**Order: Item 3 → Item 1 → Item 2 → P4b amendment.**

- **Item 3 first** — smallest, independent, and it touches
  `EditorBuildSettings.asset`, which nothing else in the batch goes near. Landing
  it alone keeps that asset's diff unambiguous.
- **Item 1 second** — pure signature deletion, no prefab contact, no save
  contact. It touches `RunSceneHost.cs` (`:955-961`) but in a region far from
  Item 2's edits.
- **Item 2 third, and alone in its commit.** It is the only item with prefab
  contact (`Run.prefab` wiring test), the only one that can surface a real
  data disagreement between the authored asset and the retired fallback, and the
  only one whose test count moves. Do not co-mingle.
- **P4b amendment last** — document-only, zero code, no ordering pressure.

**Do not bundle Items 1 and 2 into one commit** even though both edit
`RunSceneHost.cs`. If the Chopshop PlayMode fixtures red under Item 2, a bundled
commit makes the bisect ambiguous.

**Playtest debt:** Items 1 and 3 are behaviour-neutral by construction (a deleted
unread field; a deleted unreferenced scene) — **no playtest owed**. Item 2
changes which vehicle the game builds in the fixture path only; production
already ran the SO path via the wired `Run.prefab`, so production behaviour is
unchanged. **One first-look pass owed** under postmortem P4 anyway: start a run,
confirm the Scout's five parts show correct names/HP on the rings and that the
opening hand contains an attack card. ≤2 minutes, and it is the only thing that
would catch an authored-asset/fallback disagreement that the tests happened not
to assert.

---

## 7. Three-lens self-audit

**Lens 1 — Codebase health.**
ADR-0011 drift, grepped not assumed: Item 1 is a textbook **#4 vestigial enum**
(two members, one ever passed in production) plus a **#7 transitional comment
that is factually false** (`BeaconType.cs:41-46`). Item 2 is **#2 parallel
storage** at a scale the plan missed — 13 construction paths for one vehicle,
enumerated in §2.1(a) — plus a second false-mechanism comment
(`RunSceneHost.cs:1632-1636` describing a `Resources.Load` path that does not
exist). Two independent instances of the same defect class in one batch is not a
coincidence; it is the pattern memory
`feedback_never_relay_negative_existence_claims` warns about, inverted — a
*positive* existence claim in an xmldoc, never verified. **Recommend a standing
rule, not a one-off fix:** any xmldoc asserting "persisted", "consumed by",
"falls back when X exists", or naming downstream consumers gets grep-verified at
review time. Both of this batch's items were found only because the plan told us
to distrust exactly that phrasing.
Single-responsibility: Item 2 *shrinks* `RunSceneHost`'s charter (it stops being
a vehicle factory) — the right direction for a 9k-line-adjacent class.
Duplication vs. premature abstraction: the 12 test-local `BuildScout` copies are
past the "share before the third caller" line, but their shared home is blocked
by a platform-constraint boundary (§2.1(d)) — correctly deferred to §5.4b, not
forced.
Subscription lifecycle: **confirmed, no delta.** No item touches Bind/OnDestroy
or OnEnable/OnDisable pairs.
Teardown races: **confirmed, no delta** for Items 1 and 3. Item 2's one exposure
is `BeginNewRun` now throwing on a null asset instead of falling back — name the
guard explicitly and place it at the top of `BeginNewRun`, before any
`_controller` allocation, so a throw cannot leave a half-built session behind.

**Lens 2 — Optimization.**
Items 1 and 3 are deletions; zero runtime delta. Item 2 removes two ternary
null-checks from the per-run construction path (not a hot path — do not claim
this as a win). One allocation note worth recording: `GetSmallFrameLayout()`
lazily creates a `HideFlags.HideAndDontSave` `ScriptableObject` that is **never
released**, cached in a `static` field for the process lifetime. Deleting it
retires a small permanent leak in the Editor — worth one line in the commit
message, not worth a separate item.
Cadence: nothing in this batch adds an event or callback.
**Speculative over-optimization avoided, and this is the important one:** §4.3
deliberately declines to spike P4b's per-frame conversion cost. Measuring it
would produce a number that answers a question the decision does not turn on, and
per the postmortem's Class D, a measurement with an unstated frame of reference
outranks a correct estimate in the room. The exit criterion in §4.5 sequences the
measurement **behind** the workflow condition precisely so that cannot happen.

**Lens 3 — 1.0-shape survival.**
Item 1: deleting a field with no readers cannot cost signature churn later; if
M2 storm-forced advances genuinely need a cause, they will need a payload shaped
by *their* consumers, not a two-member enum guessed at in M1. Per memory
`demo_forward_over_infrastructure`, the 1.0 shape is "add it when a consumer
exists." The replacement `StepIndex` assertion (§1.4) is the one that survives —
it pins the ADR-0003 seed-mix term that M2 branching will actually change.
Item 2: `_playerVehicleAsset` **is** the 1.0 shape and stays; what dies is
scaffolding. The `CombatWidgetPrefabWiring_Test` row is permanent infrastructure
with a growing consumer list, not a stopgap.
Item 3: deletion, nothing to survive.
P4b: this is where the lens earns its keep. **Migrating `HudAnchors` now would
lock in the 1.0-hostile shape** — a numeric anchor table converted per frame,
authored blind, re-derived for every future chassis and every visual part swap on
the parts axis. Keeping it in UGUI keeps the direct-manipulation workflow that
`VehicleHudAnchors` was purpose-built to restore. The registry entry is not a
stopgap wearing a hat: its exit criterion (§4.5) is falsifiable, ordered, and
names the condition under which the decision genuinely reverses.
The one stopgap risk I am accepting and flagging: **Amendment A's re-derived
invariant is itself a hypothesis.** If a seventh member appears, the category is
wrong again and must be re-derived a third time — which is why §4.4(5) demands a
new explicit threshold rather than silently carrying the old one forward.

---

## 8. Constraints summary (the checklist)

1. Item 1: delete the reader (`RunController_HappyPath_Test.cs:229`) **before**
   the field; outside-in order per §1.3; **no default-param overload**; replace
   the dying assertion with `StepIndex`; revise all three false xmldocs
   (`BeaconType.cs:40-52`, `BeaconTransition.cs:3-10`, `INodeMapMutator.cs:3-9`,
   `RunSceneHost.cs:955-956`). EditMode count must stay **1299**.
2. Item 2: the factory **dies**, it does not relocate; fixtures load
   `Resources.Load<VehicleDefinitionSO>("combat/Vehicles/Vehicle_Scout")` via the
   existing `InjectField` mechanism in both suites; `BeginNewRun` throws on null;
   the `CombatWidgetPrefabWiring_Test` row lands in the **same commit**; verify
   `GetSmallFrameLayout` has no second caller before deleting it; a Chopshop
   PlayMode red is a **finding to report**, not a test to patch.
3. Item 3: confirm `EditorBuildSettings.asset` returns to two entries with an
   otherwise-empty diff after `Author All Scenes`; fix, do not delete,
   `BeaconActivator.cs:84`.
4. P4b: no migration, no bare registry admission — **re-derive as Amendment A**
   with all six items in §4.4; the `HudAnchors` exit criterion is ordered
   (workflow gates cost, not the reverse).
5. P5: does **not** close in this batch. Fresh canvas audit, code-read per
   canvas, then a gate proven to red on a deliberately-added out-of-registry
   `Canvas`.
6. Evidence: state EditMode/PlayMode counts explicitly in the commit or capture;
   run the ≤2-minute first look after Item 2 per postmortem P4.

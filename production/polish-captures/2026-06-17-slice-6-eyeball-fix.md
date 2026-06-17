# Polish Capture: Slice 6 Eyeball Fix — Overlay Wire-Up + Debug-Shell Retirement + HUD Bar Diagnosis

**Date:** 2026-06-17
**System:** Run Loop — Slice 6 runtime closeout (post-merge eyeball fix)

**Why this exists:** Slice 6 merged 2026-06-17 (commits `1eb603b` + `c0cda1f` on `origin/main`) as EditMode-green + grep-green. The closeout TD verdict listed "Manual run-through: start → combat → win → map shows → click beacon → ..." as success criterion #4 but attestation requirements (TQ4) only covered EditMode tests + `_loop` null-coalesce checks. The walkthrough was never run before merge. First playtest 2026-06-17 (same day) surfaced **three independent symptoms in `Combat.prefab`**:

1. **No map at boot.** MapView never appears; user dropped straight into combat.
2. **Combat HUD bars (HP / Armor / Scrap / Fuel / Energy) all missing.** Card energy badges render correctly; vehicle-level HUD does not.
3. **Debug shell (`RunControlsWidget`) visible in production.** `Reset Combat` / encounter picker / `DAMAGE ENEMY 10` controls bottom-left of the screen.

Grep dig found `_host: {fileID: 0}` in `Combat.prefab` — at least one `RunSceneOverlayHost` SerializeField ref is unset. The closeout shipped the C# but never finished authoring the prefab wiring the C# depends on.

## Proposed change — three workstreams, one session

### Workstream A — Wire `RunSceneOverlayHost` refs in `Combat.prefab`

The component has four `[SerializeField]` refs. Only `_host` has a fallback (`GetComponentInParent` in `OnEnable`); the other three silently no-op their Show/Hide calls when null. All four must be wired:

| Ref | Target | Fallback if null |
|---|---|---|
| `_host` (`RunSceneHost`) | The `RunSceneHost` MonoBehaviour on the prefab root (or wherever it lives) | `GetComponentInParent<RunSceneHost>()` in `OnEnable` |
| `_mapView` (`MapViewController`) | The MapView's UIDocument-bearing GameObject | None — no fallback. Null → no map at boot. |
| `_runCompleteView` (`RunCompleteViewController`) | The RunComplete's UIDocument-bearing GameObject | None — null → no RunComplete view at terminal beacon. |
| `_combatHudRoot` (`GameObject`) | The Combat_HUD canvas root GameObject (not the `CombatHud` component) | None — null → HUD doesn't toggle on `OnCombatReady` / off on `OnBeaconChanged`. |

**No destructive edit here.** Wiring fills in unset slots.

### Workstream B — Retire `RunControlsWidget` (debug-shell delete)

TD verdict (2026-06-17 mid-eyeball): "Delete it — bridges-at-done." The widget mixes pure-debug controls (`Reset Combat` button now redundant with RunComplete's restart, `DAMAGE ENEMY 10` debug damage) with one debug-shell tuning knob (encounter picker — Standard/Ambush toggle). The encounter type is canonically authored per beacon by `BiomeDistributionSO` once that path is wired; the runtime picker exists because the production path didn't ship encounter-type-per-beacon yet. Same pattern as the retired `PickFirstForwardEdge` (ADR-0011 transitional state).

**Wider scope than initial:** grep finds RunControlsWidget tendrils across 6 files. Full delete surface:

| File | Change | Destroys |
|---|---|---|
| `Assets/Scripts/CombatView/RunControlsWidget.cs` | DELETE file | The entire widget — Reset button, encounter picker pills, debug-damage button, winner banner |
| `Assets/Scripts/CombatView/RunControlsWidget.cs.meta` | DELETE file | Unity GUID |
| `Assets/Scripts/CombatView/CombatHud.cs:293` | DELETE field | `[SerializeField] private RunControlsWidget _runControls;` |
| `Assets/Scripts/CombatView/CombatHud.cs:~801` | DELETE line | `_runControls = go.AddComponent<RunControlsWidget>();` — programmatic instantiation that puts the widget into the runtime scene every boot |
| `Assets/Scripts/CombatView/CombatHud.cs:154` | EDIT xmldoc | Reference to "DAMAGE ENEMY 10 button RunControlsWidget appends below the [...]" — adjust adjacent comment |
| `Assets/Scripts/CombatView/CombatController.cs:21, 110, 124, 128` | EDIT xmldoc | Class summary + remarks referencing RunControlsWidget; trim to surviving consumers only |
| `Assets/Scripts/CombatView/CombatController.cs` (`RequestDebugDamageEnemy`) | DELETE method | Public debug method called only by the widget |
| `Assets/Scripts/CombatView/CombatController.cs` (`EncounterSelection` property) | KEEP (used by RunSceneHost via SerializeField path) | — |
| `Assets/Scripts/CombatView/Data/CombatBalanceSO.cs:8` (xmldoc) | EDIT xmldoc | "designers tune these via RunControlsWidget" reference |
| `Assets/Scripts/CombatView/Data/CombatBalanceSO.cs` (`DebugDamageAmount` field) | DELETE field | Only consumer is the widget; dies with it |
| `Assets/Scripts/CombatView/RunSceneHost.cs:89` (Tooltip text on `_encounterSelection`) | EDIT Tooltip | "Designer-tweakable from RunControlsWidget at runtime" → "Set in Inspector before Play; takes effect on the next BeginNewRun." |
| `Assets/Editor/CombatPrefabAuthor.cs:3033` (+ surrounding logic) | EDIT author script | Strip widget creation/wiring from the prefab author flow |
| `Assets/Prefabs/CombatView/Combat.prefab` | EDIT — re-author or hand-strip | Remove any RunControlsWidget GameObject that ended up in the YAML (likely none, since CombatHud creates it at runtime — confirm via grep) |

### Workstream C — Diagnose missing HUD bars

TD hypothesis: regression from CombatController → pure-presenter refactor (Slice 6 commit B `c0cda1f`). State now plumbed through `HandleCombatReady(loop)` but bar widgets in `CombatHud` may still be listening to the pre-refactor path. Card-level binding (energy cost badges) works; vehicle-level binding (HP, Armor) doesn't.

**Investigation only at this capture stage.** Diagnosis may surface:
- (a) `CombatHud` bar-binding code that references a deleted CombatController method/field → reroute through new presenter path
- (b) Prefab regression where bar widget GameObjects were de-parented or `m_IsActive: 0` set during an earlier prefab author pass → re-author or hand-fix
- (c) Some other shape we don't yet see

**If (a):** code change to `CombatHud.cs` only; no further capture needed beyond noting the rewire in this file.
**If (b):** capture file gets a `Workstream C — authored values destroyed` table appended before any prefab edit.
**If (c):** stop, re-consult TD before touching anything.

## Final-game picture this serves

Closes Slice 6 at runtime, not just at compile/test time. The end-state is the same as the closeout aimed for: **a `Combat.prefab` a designer can open in Unity, see clearly named slots, drag asset refs into them, hit Play, and walk a full run from start to Haven with HUD bars rendering correctly.** Designer-friendly prefab is load-bearing per user direction 2026-06-17: *"clean prefab system that a person can easily understand and edit"*.

Workstream B is the same ADR-0011 ratchet as the closeout's `PickFirstForwardEdge` deletion: debug-shell scaffolding does not survive into the production prefab. The encounter type stays designer-tunable via the `RunSceneHost._encounterSelection` SerializeField on the prefab root — the canonical 1.0 authoring path. Runtime mid-play tweaking goes; that's an iteration-speed concession we paid for during pre-Slice-6 build that no longer earns its keep.

Workstream A unblocks the entire run loop visually. Workstream C closes the second-order regression. Together they let M1 reward-pool tuning + biome-2 distribution authoring + RunComplete polish proceed on a runtime-verified base instead of stacking on a broken one.

## Authored values being destroyed

Already enumerated in Workstream B table above. Summary:

- `RunControlsWidget.cs` (303 lines) — the entire widget script
- `RunControlsWidget` field on `CombatHud` + programmatic instantiation line
- `CombatController.RequestDebugDamageEnemy(int)` method
- `CombatBalanceSO.DebugDamageAmount` field (designer-tunable balance value)
- Xmldoc references in `CombatController`, `CombatHud`, `CombatBalanceSO`, `RunSceneHost`
- `CombatPrefabAuthor` widget creation/wiring logic

No prefab YAML values to capture in Workstream A (filling unset refs). Workstream C deferred to in-flight diagnosis — capture amendment if prefab edit needed.

## Technical Director Review

**Verdict:** APPROVE (TD verdict captured 2026-06-17, full TD response in session log under `TD-EYEBALL-VERDICT: CONCERNS` heading)

**TD reasoning summary:**

*Q1 — Path 1 (capture-and-keep-eyeballing) vs Path 2 (fix-prefab-first).* Path 2 with scope expanded. Three independent symptoms in the same prefab; the diagnosis surface is small enough (one prefab, four refs, one widget, one HUD canvas check) that investigation and fix collapse into one ~30-60 min Unity inspector session. A separate TD-investigation gate would add ceremony without new information.

*Q2 — What this says about Slice 6 closeout.* Treat Slice 6 as **shipped-code, runtime-unverified**. EditMode-green + grep-green covered the C# delta but not the authored-asset delta the C# depends on. The closeout's TD attestation list (TQ4) was honest about what it attested but the success criteria included a manual walkthrough that was never run. This warrants an **amendment to TD attestation discipline**, not a new ADR-0011 entry: when a slice's success criteria include "manual walkthrough" or "scene-runtime", APPROVE verdicts must explicitly mark those criteria as either (a) attested with screenshot/recording, or (b) deferred with a named follow-up. EditMode-green + grep-green alone is insufficient when the slice ships authored-asset wiring. TD will save this as a feedback memory after user sign-off.

*Q3 — Debug shell visibility (`RunControlsWidget` in production).* Same fix-session, not a follow-up. The widget IS the ADR-0011 smell — debug-shell scaffolding bleeding into canonical 1.0 shape. User's `feedback_demo_forward_over_infrastructure.md` is explicit: build canonical 1.0 directly, no throwaway scaffolding. Slice 6's "reuse CombatScene" design implicitly inherited that scene's test harness; closeout should have either gated the widget off via a debug flag or deleted it. **Delete it — bridges-at-done.**

*Q4 — Missing HUD bars: independent or shared root with overlay wire-up?* Most likely **independent**. `_combatHudRoot` controls Show/Hide on the canvas root, not bar visibility inside it; null `_combatHudRoot` does not suppress bars. Card energy badges rendering on cards but bars missing over vehicles suggests card-level binding works but vehicle-level HUD binding does not — could be a prefab-author regression from the Slice 6 CombatController → pure-presenter refactor (state plumbed through `HandleCombatReady` but HUD widgets still listening to the old path). Cheap shared-root sanity check first: (a) is the Combat_HUD canvas GameObject active at runtime, (b) are the bar widget GameObjects active under it, (c) are their data-binding refs wired.

*Q5 — Attestation for the fix.* **All three:**
1. Grep `Combat.prefab` YAML for all four overlay refs — confirm no `fileID: 0` on any.
2. Re-run EditMode tests — hold at 486 / 0 / 1 (catch any prefab-load test regression).
3. **Manual scene walkthrough screenshot** covering: map at boot → beacon click → combat with HUD bars visible → win → map → terminal → RunComplete → restart. The walkthrough screenshot is the load-bearing one — exactly the success criterion #4 the closeout missed. Don't skip it again.

**Designer-friendly addendum (user direction, captured here):** the resulting `Combat.prefab` must be **a clean prefab a designer can open in Unity, understand the slot layout, and tweak without reading code**. This is the load-bearing UX criterion alongside the TD attestation list. Workstream A's wiring must use clear ref names + the existing Tooltip text in `RunSceneOverlayHost`. Workstream B's delete must not leave dead xmldoc references that confuse a future Inspector-reading designer. Workstream C's diagnosis must not paper over the regression with a private wire — if the bar binding lives in the prefab inspector, the fix lives there too.

## User approval

- Reviewed: 2026-06-17
- Approved by: User 2026-06-17 — *"approve"* — covers original scope (A + B + C) + TD attestation-discipline feedback memory.
- Notes: Capture written first per `capture-before-destroy` hook; execution begins on explicit "approved" confirmation. **Superseded by Amendment 2026-06-17 below — see Workstream A retirement notice.**

---

## Amendment 2026-06-17 — Workstream D added, Workstream A retired

**Trigger:** Pre-execution investigation re-grepped `Combat.prefab` for the three Slice 6 UI overlay script GUIDs and confirmed all three are zero hits:

| Script | GUID | Hits in Combat.prefab |
|---|---|---|
| `RunSceneOverlayHost` | `8059e3b1176919447979f902874da35a` | **0** |
| `MapViewController` | `428e64131b6826c439ea5fe88b4907e0` | **0** |
| `RunCompleteViewController` | `2f4a886e2d15e01499613f6e320fa274` | **0** |

The Slice 6 closeout (commits `1eb603b` + `c0cda1f`, merged 2026-06-17 09:30 to `origin/main`) shipped the C# delta but **never edited the prefab**. The `_host: {fileID: 0}` grep finding the original capture leaned on was on `CombatController` (line above `_balanceAsset`), not on `RunSceneOverlayHost`. RunSceneOverlayHost is not in the prefab at all.

Additional stale-prefab smell surfaced: RunSceneHost is in Combat.prefab (line 336) but its serialized field is `_enemyArchetypePrefab: {fileID: ..., guid: 83248814cca49dd41a616d4ddcb461f8, type: 3}` (line 340) — a retired field. The current C# class has `_combatBeaconArchetypes` array (line 76 of `RunSceneHost.cs`); the prefab YAML still carries the orphaned old field. The closeout-shipped author-code at lines 7165-7195 was meant to bake `_combatBeaconArchetypes[0]` but the prefab was never re-authored, so the wire is stale.

### Workstream A — RETIRED

Original Workstream A ("wire 4 refs in Inspector") cannot solve the problem because the components those refs would point to do not exist in the prefab. Hand-adding the components and the wires once would also break on the next `Tools > Wasteland Run > Author Combat Prefab` run because the tool rebuilds the prefab tree from scratch (line 6879 `new GameObject("Combat")`) and `CombatPreservePaths` (lines 7433-7444) is a small whitelist that does not include Slice 6 additions. `CapturePreservedNodes` only preserves Transform/RectTransform + SpriteRenderer/Image sprite & color — component references and arbitrary SerializeField values are not preserved across re-authors. Hand-authoring = ticking time bomb.

**Workstream A is replaced by Workstream D.** The user runs the author tool; the wiring happens automatically.

### Workstream D — Extend `CombatPrefabAuthor.cs` to author the Slice 6 UI overlay layer

The canonical authoring tool gains responsibility for the Slice 6 additions. Per ADR-0016 direction (to be drafted next session) — *the tool IS the authoring surface*; hand-author drift is forbidden as it creates two sources of truth (ADR-0011 #3 bimodal-path violation).

**New code in `CombatPrefabAuthor.cs` (additive, no destruction of existing author logic):**

| New step in `AuthorCombat` | Creates | Wires |
|---|---|---|
| Add `RunSceneOverlayHost` component on prefab root | (component only — no new GameObject; sibling to RunSceneHost on root) | After children exist: `_host` ← root's RunSceneHost; `_mapView` ← MapView child's MapViewController; `_runCompleteView` ← RunCompleteView child's RunCompleteViewController; `_combatHudRoot` ← existing Combat_HUD canvas GameObject |
| Add `MapView` GameObject as child of root | UIDocument component + MapViewController component | UIDocument's `sourceAsset` ← `MapView.uxml` (GUID `aba917630cb4ea14398a0caccbef806f`); MapViewController's `_document` ← sibling UIDocument; `_mapViewAsset` ← `MapView.uxml` |
| Add `RunCompleteView` GameObject as child of root | UIDocument component + RunCompleteViewController component | UIDocument's `sourceAsset` ← `RunComplete.uxml` (GUID `38c99bb26f27b4849a96fd2af4c75e92`); RunCompleteViewController's `_document` ← sibling UIDocument; `_runCompleteAsset` ← `RunComplete.uxml` |

**Author code shape:** uses the existing `SerializedObject` + `FindProperty` pattern (precedent at lines 7165-7195 — `_combatBeaconArchetypes[0]` baking). No new patterns introduced.

**Authored values destroyed by Workstream D:** none. The components do not yet exist in the prefab; this is pure creation.

### Workstream B — file edit table re-targeted to canonical author flow

Original Workstream B table is unchanged except for `CombatPrefabAuthor.cs:3033` — that line reference was speculative ("strip widget creation/wiring from the prefab author flow"). The accurate target: grep `CombatPrefabAuthor.cs` for `RunControlsWidget` references and strip them. If none found, the author tool never created the widget (CombatHud creates it at runtime via `Awake`); the only fix surface for the author tool is the new Workstream D code.

### Workstream C — investigation deferred to post-re-author

TD verdict: "may not auto-resolve, but plausible it does." After Workstream D + B ship and user runs the author tool, re-check whether HP/Armor bars now render. The stale-prefab pattern that orphaned `_enemyArchetypePrefab` may also have orphaned `_enemyBarStack` / `_playerBarStack` SerializeFields on `CombatHud`. If bars render: Workstream C resolves with the author run. If bars still missing: separate diagnosis pass, escalate as a new finding.

### Order of operations (executing now)

1. ✅ Read `MapViewController.cs` + `RunCompleteViewController.cs` (TD step 0) — confirmed SerializeField shapes: each has `_document` (UIDocument) + asset field (`_mapViewAsset` / `_runCompleteAsset`).
2. ⏳ Extend `CombatPrefabAuthor.cs` — Workstream D code (add-then-delete sequencing per TD Q4).
3. ⏳ Delete `RunControlsWidget.cs` + `.cs.meta` + strip from `CombatHud`, `CombatController`, `CombatBalanceSO.DebugDamageAmount`, `RunSceneHost` Tooltip + author tool — Workstream B.
4. ⏳ User runs `Tools > Wasteland Run > Author Combat Prefab`.
5. ⏳ Stale-field grep on Combat.prefab (TD step 5.5): `grep -E "^\s+_[a-zA-Z]+:" Combat.prefab | sort -u` — log orphan finds as follow-up TD-RISK; don't fix in this session.
6. ⏳ Workstream C check — do HP/Armor bars now render?
7. ⏳ Attestation block: (a) prefab-YAML grep for all 3 script GUIDs (must be ≥1 each), (b) prefab-YAML grep for `{fileID: 0}` on all 4 RunSceneOverlayHost SerializeFields (must be 0 hits), (c) EditMode tests green (486/0/1), (d) walkthrough screenshot map → click → combat with bars → win → map → terminal → RunComplete → restart.
8. ⏳ Save `feedback_prefab_deliverable_attestation.md` memory.
9. ⏳ Commit.

### Risk flags surfaced and accepted by user 2026-06-17 *"go for it"*

1. Time creep: +60-90 min vs hand-author.
2. Workstream A retired — wiring now atomic via author tool, no Inspector hand-work.
3. Workstream C may not auto-resolve — diagnose after re-author.
4. `link.xml` IL2CPP coverage for UI controllers — future ADR, non-blocking.
5. Memory write pending sign-off on rule shape.

### Technical Director Review — Amendment

**Verdict:** APPROVE Option B (extend author tool). Full TD verdict captured this session, summary:

- **Q1 (Option A vs B):** Option B. CapturePreservedNodes only saves Transform + sprite refs; component wires would die on every author run = ticking time bomb = ADR-0011 bimodal violation.
- **Q2 (capture amendment shape):** Addendum to existing file (this block). New capture would fragment audit trail.
- **Q3 (attestation rule):** New separate memory `feedback_prefab_deliverable_attestation.md`. Rule: prefab-edit deliverables MUST grep prefab YAML for script GUIDs + zero-fileID-0 on wired SerializeFields. EditMode-green + code-grep-green ≠ semantic-green when deliverable is a prefab edit.
- **Q4 (single edit pass to author file):** Approve, sequence add-then-delete (D before B in the file) for cleaner failure isolation.
- **Q5 (order of operations):** Approve with two additions — step 0 (read view controllers) ✅ done, step 5.5 (stale-field grep) added above.

### Follow-up — broader pre-Slice-6 audit (user direction 2026-06-17)

User direction mid-execution: *"not just slice 6 we need to check everything prior that we havent checked please take note of it"*. The `_enemyArchetypePrefab` discovery (RunSceneHost serialized data carrying a retired field) is a single visible example of a broader pattern — prior slices may have shipped C# refactors whose prefab YAML never got re-authored to match. Slice 6 surfaced one; there may be more across `Combat.prefab`, `CombatHud.prefab`, `PlayerVehicle.prefab`, `EnemyVehicle.prefab`, `Dredge.prefab`, `MainBar.prefab`, `VehicleBarStack.prefab`, etc.

**Scope this session does NOT cover:** the broader audit. This session ships the Slice 6 fix + the stale-field-grep on `Combat.prefab` only. The broader audit deserves its own session with a structured pass per prefab.

**Action item recorded for next session (after Slice 6 eyeball fix lands):**

> **Pre-Slice-6 stale-prefab audit.** Walk every prefab under `Assets/Prefabs/`, grep YAML for serialized fields whose source C# class no longer declares them. Output: a list of orphaned fields per prefab + per-line TD-RISK assessment (cosmetic vs gameplay-critical). For gameplay-critical orphans, re-author or hand-strip. The pattern memory should be: any time a C# field is renamed/retired, the author tool for the prefab carrying it must run in the same commit, OR a stale-field follow-up ticket must be opened.

This action item will be promoted to a session-state entry after this session commits, so next-session pickup includes it.

---

## Amendment 2026-06-17 (mid-eyeball, continued) — Workstream E: archetype roster bake + Resources fallback retirement

### Symptom

After Workstreams B + D landed, author tool ran clean, map rendered for the first time. User clicked a Combat beacon and got:

```
SceneEncounterBuilder.Build: no EnemyArchetypeBinder resolved for archetype 'Dredge'.
Wire the matching prefab in RunSceneHost._combatBeaconArchetypes or provide it
under Resources/EnemyArchetypes/Dredge.
```

Cascading `RunController.CommitNextBeacon: called while current beacon (index=1, type=Combat) is unresolved` on every subsequent click — the failed combat-start left the beacon stuck "unresolved" and every retry re-cascaded.

### Root cause

`Biome1Distribution.asset` emits three archetypes at equal weight: DuneSkimmer (Id=0), IronShepherd (Id=1), Dredge (Id=2). The author tool's Phase C block (added at Slice 6 close) baked only `_combatBeaconArchetypes[0] = DuneSkimmer`. When the run rolled Dredge or IronShepherd, `RunSceneHost.ResolveBinder` walked the 1-element array, missed, fell through to `Resources.Load<GameObject>("EnemyArchetypes/{id}")` — but `Assets/Resources/EnemyArchetypes/` does not exist. Returned null → SceneEncounterBuilder threw.

This is a classic two-canonical-path violation: array + Resources fallback both purport to resolve the binder, only one was wired, and neither was *required* to be the single source of truth.

### TD verdict (mid-session)

Recommendation: **Option A (bake all 3 into the array) + delete the Resources fallback in the same change**. Reasoning:

- The bimodal-at-done violation is created by the *fallback*, not the array. Collapsing to one canonical path resolves it.
- Option C (move roster onto `BiomeDistributionSO`) is the cleanest ADR-0015 read but drags Combat types into Run assembly; cross-assembly refactor not in scope for an eyeball-fix.
- ADR-0015 narrows *what biome 1 emits* (the SO weights); scene-side prefab binding is a different axis correctly owned by `RunSceneHost`.
- Option B (Resources-only) loses the per-scene Inspector affordance.

Verdict CONCERNS upgraded to APPROVE after grep confirmed zero external callers of the Resources path (4 hits, all local to `RunSceneHost.ResolveBinder` doc + tooltip + call + `SceneEncounterBuilder` error string).

### Files touched

1. **`Assets/Editor/CombatPrefabAuthor.cs`** — replaced the 1-slot bake with a roster-driven 3-slot bake. Roster is an explicit `(path, EnemyArchetypeId)[]` array so the pairing with `Biome1Distribution.asset` `_combatArchetypes` stays legible. Per-element missing-prefab warnings preserved with precise id naming.

2. **`Assets/Scripts/CombatView/RunSceneHost.cs`** — `ResolveBinder` now returns null directly when no array match is found; `Resources.Load` fallback deleted. Tooltip on `_combatBeaconArchetypes` retuned ("single canonical path (ADR-0011)"). Header changed from "Skimmer at index 0" → "Biome 1 roster".

3. **`Assets/Scripts/CombatView/SceneEncounterBuilder.cs`** — error-message string updated: dropped the "or provide it under Resources/EnemyArchetypes/{id}" clause; added "CombatPrefabAuthor bakes the Biome 1 roster at indices 0..2" so designers hitting this error know exactly where to look.

4. **`tools/ci/grep-gates.sh`** — new gate `Resources\.Load.*EnemyArchetypes` (Assets/Scripts) asserts zero hits. Gates clean after change.

### Out of scope — backlog items recorded

- **RunController robustness fix**: a beacon whose combat fails to build during `SceneEncounterBuilder.Build` currently sticks "unresolved" and every subsequent click cascades `CommitNextBeacon called while ... unresolved`. The right fix is for failed builds to reset the beacon to pickable (or set a clear error state) rather than poison the controller. Track as `runcontroller-failed-build-recovery` backlog item.

- **Map polish pass**: the run map renders correctly but reads as a debug shell — no visible current-beacon indicator, no reachable-edge highlight, no hover state, no title/seed legend. TD verdict: continue eyeball walkthrough first; batch map polish after the full walkthrough surfaces all readability issues, design once. Backlog item: `slice-6-map-polish` — current-beacon indicator + reachable-edge highlight + axis/legend + hover state.

### Attestation (Workstream E)

- Gates: clean (`[grep-gates] all gates clean`).
- Tests: pending re-run after E lands (target: hold 486 / 0 / 1).
- Walkthrough: pending — user to re-author + Play + report.

---

## In-Flight Task Snapshot 2026-06-17 — recorded before Workstream F (composition smell-fix)

User stop-the-line direction 2026-06-17: *"all of the above then after save the current tasks we needed to do because we need to adress this right now. clean up the smell then continue."* The "smell" is the categorical mismatch — `RunSceneHost` + `MapView` + `RunCompleteView` + `RunSceneOverlayHost` all living inside `Combat.prefab` (a combat composite). User wants the prefab graph re-shaped to siblings BEFORE resuming the eyeball walkthrough. The items below are paused-in-place; they resume after Workstream F lands.

### Paused walkthrough backlog

| # | Item | Source | Status at pause |
|---|---|---|---|
| 1 | **Re-run author + walkthrough after Workstream E** (steps 6–7 of Amendment 1's order of operations) | This file, Amendment 1 §"Order of operations" | Not started — was the next user-side action |
| 2 | **Workstream C — diagnose missing HUD bars** | This file, §"Workstream C" + TD Q4 | Deferred to post-re-author check; never re-checked after Workstream E |
| 3 | **Attestation block** (prefab YAML grep for 3 script GUIDs ≥1 each + 0 fileID:0 on overlay refs + EditMode tests + walkthrough screenshot) | This file, Amendment 1 §"Order of operations" step 7 | Not started |
| 4 | **Save `feedback_prefab_deliverable_attestation.md` memory** | TD Q3 Amendment 1 | Not started |
| 5 | **Stale-field grep on Combat.prefab** (`grep -E "^\s+_[a-zA-Z]+:" Combat.prefab | sort -u` → log orphan finds) | TD step 5.5, Amendment 1 | Not started |
| 6 | **Broader pre-Slice-6 stale-prefab audit** (next-session ticket) | This file, §"Follow-up — broader pre-Slice-6 audit" | Next session, gated on Workstream F closing |
| 7 | **RunController failed-build recovery** (`runcontroller-failed-build-recovery`) | This file, §"Out of scope — backlog items recorded" (Workstream E) | Backlog, not in this session |
| 8 | **Slice 6 map polish pass** (`slice-6-map-polish`) | This file, §"Out of scope — backlog items recorded" (Workstream E) | Backlog, batch after full walkthrough |
| 9 | **Commit Workstreams B + D + E + F** | This file, Amendment 1 §"Order of operations" step 9 | Held until F lands + attestation runs |

### Hand-off promise

After Workstream F (composition smell-fix) lands and the user confirms the prefab shape is right, **resume Item 1** — re-run the author tool, then walk items 2 → 3 → 4 → 5 → 9 in order. Items 6 → 8 stay in the backlog memory list for next-session pickup (already covered by `project_next_session_eyeball.md` + this file's existing "Follow-up" block; the session-state STATUS update at commit time will surface them).

---

## Workstream F — Prefab Split Capture (categorical smell-fix)

**Trigger:** User stop-the-line direction 2026-06-17: composition smell — `RunSceneHost` (run-loop driver) + `RunSceneOverlayHost` + `MapView` + `RunCompleteView` are all living inside `Combat.prefab` (a combat composite). Categorical mismatch — these are run-layer concerns, not combat-encounter concerns. User caught the smell on the first eyeball pass; TD missed it during Slice 6 close; I extended it via Workstream D earlier this session. Three memory files saved this session lock the prevention rule in (`feedback_composition_smell_test.md`, `feedback_td_briefing_discipline.md`, `project_adr_0016_scope_expansion.md`).

**Decisions approved by user 2026-06-17:**
1. Split shape (Run-layer extract) — approved.
2. Cross-prefab wire = Option 2 (event seam via POCO mediator in `WastelandRun.Run`) — approved over Options 1/3/4.
3. Programmatic scene re-wire via new `Author Combat Scene` menu — approved (TD's required change over hand-edit).
4. Capture-amendment-first sequence — approved.

**Goal end-state:** `Combat.prefab` claims "this is the combat encounter scene" — every component inside aligns with that claim. `Run.prefab` claims "this is the run loop + run-overlay UI" — every component inside aligns with that claim. The two are top-level siblings in `CombatScene.unity`. The OverlayHost no longer holds a `_combatHudRoot` SerializeField — it raises events on a POCO mediator in `WastelandRun.Run`; `CombatHud` subscribes from the Combat assembly.

### Authored values destroyed — every field captured before destruction

All `fileID` values below were read out of `Combat.prefab` (rev as of 2026-06-17 mid-session, lines noted inline). After Workstream F lands, these values must reappear under the new prefab's component graph with no loss. The author tool produces them deterministically from code, but this capture is the ground-truth checksum.

#### 1. `MapView` GameObject (Combat.prefab line 250, fileID `2500120273223189421`) — MOVES OUT

| Field | Current value | Post-split target |
|---|---|---|
| `m_Name` | `MapView` | unchanged, parented under Run.prefab root |
| `m_IsActive` | `1` | unchanged |
| Transform (`&91758720011340859`) `m_LocalPosition` | `(0, 0, 0)` | unchanged |
| Transform `m_LocalScale` | `(1, 1, 1)` | unchanged |
| Transform `m_Father` | `7380098574025556086` (Combat root) | Run.prefab root transform |
| Transform `m_Children` | `[]` | unchanged |
| UIDocument (`&7269377025468449081`) `m_PanelSettings` | `{guid: 41f96296f400e0245a37aa4e9f1de5f8, type: 2}` (`Assets/UI/PanelSettings.asset`) | unchanged |
| UIDocument `sourceAsset` | `{fileID: 9197481963319205126, guid: aba917630cb4ea14398a0caccbef806f, type: 3}` (`MapView.uxml`) | unchanged |
| UIDocument `m_SortingOrder / m_Position / m_WorldSpaceSizeMode / Width / Height` | `0 / 0 / 1 / 1920 / 1080` | unchanged |
| MapViewController (`&7202693912299954274`) Script GUID | `428e64131b6826c439ea5fe88b4907e0` | unchanged |
| MapViewController `_document` | `{fileID: 7269377025468449081}` (sibling UIDocument) | new internal fileID, same logical wire |
| MapViewController `_mapViewAsset` | `{guid: aba917630cb4ea14398a0caccbef806f, type: 3}` | unchanged |

#### 2. `RunCompleteView` GameObject (Combat.prefab line 436, fileID `5766879158815974971`) — MOVES OUT

| Field | Current value | Post-split target |
|---|---|---|
| `m_Name` | `RunCompleteView` | unchanged, parented under Run.prefab root |
| `m_IsActive` | `1` | unchanged |
| Transform (`&2357003498049811637`) `m_LocalPosition` | `(0, 0, 0)` | unchanged |
| Transform `m_LocalScale` | `(1, 1, 1)` | unchanged |
| Transform `m_Father` | `7380098574025556086` (Combat root) | Run.prefab root transform |
| UIDocument (`&1987353586991441785`) `m_PanelSettings` | `{guid: 41f96296f400e0245a37aa4e9f1de5f8, type: 2}` | unchanged |
| UIDocument `sourceAsset` | `{fileID: 9197481963319205126, guid: 38c99bb26f27b4849a96fd2af4c75e92, type: 3}` (`RunComplete.uxml`) | unchanged |
| UIDocument `m_SortingOrder / WorldSpaceSizeMode / Width / Height` | `0 / 1 / 1920 / 1080` | unchanged |
| RunCompleteViewController (`&6742965567436946798`) Script GUID | `2f4a886e2d15e01499613f6e320fa274` | unchanged |
| RunCompleteViewController `_document` | `{fileID: 1987353586991441785}` (sibling UIDocument) | new internal fileID, same logical wire |
| RunCompleteViewController `_runCompleteAsset` | `{guid: 38c99bb26f27b4849a96fd2af4c75e92, type: 3}` | unchanged |

#### 3. `RunSceneHost` MonoBehaviour (Combat.prefab line 400, fileID `3359085387489826617`) — MOVES OUT (component on Combat root → component on Run root)

| Field | Current value | Post-split target | Note |
|---|---|---|---|
| Script GUID | `8e879c63b977c6146a51fcc26ea24ad7` | unchanged | |
| `_playerVehicleAsset` | **`{fileID: 0}` ← UNWIRED** | **TD-RISK flagged below** | already a stale-prefab smell, not introduced by this refactor |
| `_biomeDistribution` | **`{fileID: 0}` ← UNWIRED** | **TD-RISK flagged below** | same — pre-existing smell |
| `_combatBeaconArchetypes[0]` | `{guid: 0616e48eb8f292f4abe3dc9e2511c2a5, type: 3}` (DuneSkimmer) | unchanged (Workstream E baked roster) | |
| `_combatBeaconArchetypes[1]` | `{guid: 348efc5257369ab4787a95fe4a33ad3b, type: 3}` (IronShepherd) | unchanged | |
| `_combatBeaconArchetypes[2]` | `{guid: 83248814cca49dd41a616d4ddcb461f8, type: 3}` (Dredge) | unchanged | |
| `_runSeed` | `0` | unchanged | |
| `_encounterSelection` | `0` (Standard) | unchanged | |

#### 4. `RunSceneOverlayHost` MonoBehaviour (Combat.prefab line 420, fileID `4150825144439847729`) — MOVES OUT, FIELD SET CHANGES

| Field | Current value | Post-split target |
|---|---|---|
| Script GUID | `8059e3b1176919447979f902874da35a` | unchanged |
| `_host` | `{fileID: 3359085387489826617}` → RunSceneHost (same prefab) | new internal fileID → RunSceneHost (now sibling component in Run.prefab) |
| `_mapView` | `{fileID: 7202693912299954274}` → MapViewController (same prefab) | new internal fileID → MapViewController (now child in Run.prefab) |
| `_runCompleteView` | `{fileID: 6742965567436946798}` → RunCompleteViewController (same prefab) | new internal fileID → RunCompleteViewController (now child in Run.prefab) |
| **`_combatHudRoot`** | **`{fileID: 6435744754285889646}` → stripped GameObject from CombatHud.prefab instance (guid `24c92595efa3ac04dbee290067b22303`)** | **FIELD REMOVED — replaced by Option 2 event seam (POCO mediator `RunOverlayEvents` in `WastelandRun.Run`)** |

#### 5. Combat root Transform children list (Combat.prefab line 352, fileID `7380098574025556086`)

Current 5 children:
1. `{fileID: 1031150932009002631}` → SceneVisuals (or similar — **stays**)
2. `{fileID: 4964221122556935786}` → LaneAxis (**stays**)
3. `{fileID: 275866856508630958}` → CombatHud instance (**stays**)
4. `{fileID: 91758720011340859}` → **MapView Transform — MOVES OUT**
5. `{fileID: 2357003498049811637}` → **RunCompleteView Transform — MOVES OUT**

Post-split: 3 children remain (SceneVisuals + LaneAxis + CombatHud instance). Categorical claim of `Combat.prefab` is now coherent.

#### 6. Combat root component list (Combat.prefab line 327)

Current 5 components on root:
1. `{fileID: 7380098574025556086}` → Transform (stays)
2. `{fileID: 248011968795595011}` → CombatController (stays — combat presenter)
3. `{fileID: 7622689839050374138}` → CombatSceneBlockout (stays — backdrop/vehicle wiring)
4. `{fileID: 3359085387489826617}` → **RunSceneHost — MOVES OUT**
5. `{fileID: 4150825144439847729}` → **RunSceneOverlayHost — MOVES OUT**

Post-split: 3 components remain (Transform + CombatController + CombatSceneBlockout). Combat.prefab is a clean combat composite.

#### 7. `CombatScene.unity` wiring

Currently: a single prefab instance with `m_SourcePrefab: {guid: 4ee0cdd0c4ea8e442863de089215c801, type: 3}` (Combat.prefab) at scene line 587.

Post-split: scene root holds **two** sibling prefab instances:
- Combat.prefab (existing instance, `guid: 4ee0cdd0c4ea8e442863de089215c801`)
- Run.prefab (NEW instance, GUID assigned at first `Author Run Prefab` run)

Authored programmatically via new `Tools > Wasteland Run > Author Combat Scene` menu — no hand-edit drift.

### New code surface created by Workstream F

#### A. `RunOverlayEvents` POCO mediator (new file, `WastelandRun.Run` assembly)

```csharp
namespace WastelandRun.Run
{
    public static class RunOverlayEvents
    {
        public static event Action OnMapShown;
        public static event Action OnMapHidden;
        public static bool IsMapVisible { get; private set; }

        internal static void RaiseMapShown() { IsMapVisible = true; OnMapShown?.Invoke(); }
        internal static void RaiseMapHidden() { IsMapVisible = false; OnMapHidden?.Invoke(); }
    }
}
```

- `internal` raise methods → only Run assembly publishes (via OverlayHost path).
- `WastelandRun.UI` (OverlayHost) calls raise via assembly InternalsVisibleTo OR via a small public wrapper — TBD at implementation, surface decision deferred to coding step.
- `WastelandRun.Combat` (CombatHud) subscribes to public events.

#### B. `CombatHud` subscription (existing file, ~6 lines added)

`OnEnable`: subscribe to `OnMapShown` → hide canvas; subscribe to `OnMapHidden` → show canvas. Read `RunOverlayEvents.IsMapVisible` at subscription time to handle cold-start (CombatHud awakes before any event has fired).
`OnDisable`: unsubscribe.

#### C. `RunSceneOverlayHost` modification (existing file)

Replace `_combatHudRoot.SetActive(...)` calls with `RunOverlayEvents.RaiseMapShown()` / `RaiseMapHidden()`. Delete `_combatHudRoot` SerializeField + xmldoc entry.

#### D. `CombatPrefabAuthor.cs` modifications

- **Strip from existing `AuthorCombat`:** RunSceneHost component creation (lines ~6967-7080 area — exact lines confirmed at execution), RunSceneOverlayHost component creation, MapView child + UIDocument + MapViewController wiring (Workstream D code), RunCompleteView child + UIDocument + RunCompleteViewController wiring (Workstream D code). Remove `_combatHudRoot` wire entirely.
- **New `AuthorRun` method + menu** `Tools > Wasteland Run > Author Run Prefab`:
  - Creates `Assets/Prefabs/Run/Run.prefab` (new folder).
  - Adds RunSceneHost + RunSceneOverlayHost on root. Bakes the 3-archetype roster (Workstream E pattern preserved).
  - Adds MapView child + UIDocument (PanelSettings + MapView.uxml) + MapViewController.
  - Adds RunCompleteView child + UIDocument (PanelSettings + RunComplete.uxml) + RunCompleteViewController.
  - Wires OverlayHost `_host` → RunSceneHost (same root), `_mapView` → MapViewController, `_runCompleteView` → RunCompleteViewController. No `_combatHudRoot` wire (field deleted).
- **New `AuthorCombatScene` method + menu** `Tools > Wasteland Run > Author Combat Scene`:
  - Loads `CombatScene.unity`.
  - Ensures both Combat.prefab and Run.prefab are present as scene-root siblings.
  - If either is missing, instantiates it via `PrefabUtility.InstantiatePrefab` and saves the scene.

#### E. Start-order safety in `RunSceneHost.BeginNewRun` (TD risk #1)

`CombatController.Start():205` currently calls `RunSceneHost.BeginNewRun` directly. Cross-prefab Awake ordering is undefined; with sibling prefabs the call may land before RunSceneHost is fully initialized. Mitigation: `BeginNewRun` defers to the first frame via a one-shot coroutine OR via a `Ready` flag the caller polls. Implementation choice deferred to coding step but the safety must land in the same commit as the split.

### Pre-existing smells flagged (NOT introduced by Workstream F)

These are not "destroyed" by this workstream — they're already broken in the current prefab and will travel with the components to Run.prefab. Surfaced here so the user sees them and decides whether to fix in this session or push to the broader pre-Slice-6 audit (Item 6 in the in-flight snapshot).

1. **`RunSceneHost._playerVehicleAsset = {fileID: 0}`** — UNWIRED. RunSceneHost depends on this to instantiate the player vehicle at run start. Current state: either RunSceneHost falls back to some other path (Resources? scene search?) or the system has never actually built the player vehicle from this field. Same stale-prefab class as the retired `_enemyArchetypePrefab`.
2. **`RunSceneHost._biomeDistribution = {fileID: 0}`** — UNWIRED. RunSceneHost depends on this for the biome-1 distribution SO (Workstream E ships `Biome1Distribution.asset` but the wire is missing). Same class as #1.

**Recommendation:** the new `AuthorRun` method should bake both of these at creation. **Canonical paths verified 2026-06-17 against the existing Resources fallbacks in `RunSceneHost.cs:94-95`:**

- `_playerVehicleAsset` (type `VehicleDefinitionSO`, NOT a prefab) ← `Assets/Resources/Combat/Vehicles/Vehicle_Scout.asset`
- `_biomeDistribution` (type `BiomeDistributionSO`) ← `Assets/Resources/Run/Biomes/Biome1Distribution.asset`

Both load via `AssetDatabase.LoadAssetAtPath` (editor-only) in the author tool, matching the rest of the tool's pattern. If either canonical asset is missing, `AuthorRun` logs a precise warning naming the missing path. Pattern matches Workstream E's roster bake.

**Dead-code follow-up (Item 6 audit, NOT this commit):** `RunSceneHost.Awake` at lines 190-193 has Resources.Load fallbacks for both fields. Once `AuthorRun` bakes the serialized values, those fallbacks become dead code (serialized wins over fallback). Per TD verdict 2026-06-17 — *don't delete in this commit, log as Item-6 follow-up*. The fallbacks are tagged for retirement when the broader pre-Slice-6 audit runs.

### Attestation requirements (TD-listed, all four required before declaring clean)

1. **`Combat.prefab` grep → zero matches** for these four script GUIDs:
   - `8e879c63b977c6146a51fcc26ea24ad7` (RunSceneHost)
   - `8059e3b1176919447979f902874da35a` (RunSceneOverlayHost)
   - `428e64131b6826c439ea5fe88b4907e0` (MapViewController)
   - `2f4a886e2d15e01499613f6e320fa274` (RunCompleteViewController)
2. **`Run.prefab` grep → exactly one match each** for the same four GUIDs.
3. **`CombatScene.unity` grep → `m_SourcePrefab` refs to both prefab GUIDs** as scene-root siblings (Combat.prefab `4ee0cdd0c4ea8e442863de089215c801` + Run.prefab GUID assigned at creation).
4. **Full EditMode test run green** — paste output. Per `feedback_demo_forward_over_infrastructure.md`: tests fix forward to canonical APIs, no shims. Per `feedback_gate_check_requires_green_tests`: compile-green ≠ semantic-green.

### Risks accepted

1. Start-order coupling — mitigated in §E above (`BeginNewRun` defers).
2. EditMode tests that reference Combat.prefab paths to the moved components — grep + fix forward, no retention shims.
3. Pre-Slice-6 broader audit — gets *easier* on the post-split shape (TD verdict).
4. `Popups` canvas stays in Combat.prefab (ADR-0014 world-space exception — damage numbers are combat-scoped).
5. `link.xml` IL2CPP coverage for UI controllers — future ADR, non-blocking for this workstream.
6. Pre-existing unwired SerializeFields (`_playerVehicleAsset`, `_biomeDistribution`) — flagged in §"Pre-existing smells" above; recommendation to fix in same commit; final call to user.

### Technical Director Review — Workstream F

**Verdict:** APPROVE with changes (TD verdict captured this session under heading `[TD-ARCHITECTURE: APPROVE with changes]`).

**Required changes (incorporated above):**
1. Programmatic scene re-wire via `Author Combat Scene` menu — incorporated §D.
2. Option 2 event seam = POCO mediator in `WastelandRun.Run`, NOT on OverlayHost type — incorporated §A (asmdef cycle avoided).
3. `BeginNewRun` start-order safety — incorporated §E.
4. EditMode test re-point (fix forward, no shims) — incorporated under Attestation #4.
5. CombatHud defaults hidden in the prefab and shows on first `OnMapHidden` (or `IsMapVisible` snapshot at OnEnable) — incorporated §B.

**Capture file decision:** AMEND existing file (this section), do not create a new dated capture. Workstream F is the destructive landing of the same in-flight smell-fix.

### User approval required BEFORE destructive edit

- Reviewed: 2026-06-17 (this session).
- Awaiting user sign-off on: (a) the destroy-list checksum above, (b) the §"Pre-existing smells" recommendation (fix `_playerVehicleAsset` + `_biomeDistribution` in same commit, yes/no), (c) execution start.

---

## Polish backlog — map view (post Workstream F eyeball pass, 2026-06-17 PM)

User eyeballed the run map and flagged it as unreadable. Frontier-only + dashed + slight bezier curvature shipped as the v1 readability fix (TD verdict: `production/td-verdicts/2026-06-17-map-connection-line-element.md`). Items below are tracked deferred polish so the seams don't drift.

- **Hardcoded stroke color in `ConnectionLineElement.DefaultStroke`** — currently `#d97a3a` matching `--wr-color-accent`. Promote to a USS custom property (`--wr-connection-stroke`) and read via `customStyle.TryGetValue` so the visual-style swap stays a one-file token edit. TD note 1 from the 2026-06-17 verdict.
- **Beacon-type icons** — replace the type-initial label (`C` / `H` / `S`) inside `BeaconNodeElement` with sprite/SVG art per `BeaconType`. Polish backlog item, blocked on art direction.
- **Hover popout + white outline on beacons** — user-requested. Likely a USS `:hover` rule + a transform/scale + outline stroke via a second `generateVisualContent` pass or layered VisualElement.
- **Animated dash phase ("crawling ants")** — Painter2D foundation already in place; offset the cursor walk in `OnGenerateVisualContent` by `Time.unscaledTime * speed` and call `MarkDirtyRepaint` on a schedule. Polish backlog.
- **Per-beacon-type stroke color** — color-code edges by destination type (Combat / Haven / Merchant / ...) so the planning glance carries type information. Optional, may clash with the type-icon polish above.

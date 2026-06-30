# Polish Capture: VehicleBarStack Collapse (Slice 2.6 Phase 1c)

**Date:** 2026-06-30
**System:** Combat HUD overlay topology — collapse VehicleBarStack.prefab + LaneAxis canvases into per-vehicle authoring
**Status:** Draft — awaiting user approval before any destructive edit lands

## Proposed change

Phase 1 Option A (subsystem bars/markers as authored nested children of
`Anchor_<slotId>` on each vehicle's `VehicleHudAnchors`) has already landed in
C#. This capture covers the **adjacent collapse** the user escalated to: get
rid of `VehicleBarStack.prefab` entirely and let MainBar + BuffStrip ride on
the vehicle prefab alongside the per-slot widgets.

After the cut, the overlay topology is:

```
PlayerVehicle.prefab
  └─ HudAnchors (Canvas WorldSpace + GraphicRaycaster + VehicleHudAnchors)
      ├─ Anchor_weapon_0 (RT) → SubsystemBar + SubsystemMarker (nested)
      ├─ ... (one per slot)
      ├─ MainBar (nested, was MainBarSlot under VehicleBarStack.prefab)
      │   └─ BuffStripCanvas → BuffStrip
      └─ VehicleBarStack (component only — Update loop, hover/click, drag-cast)
```

The component still owns the runtime loop (Update, hover/click wiring,
RebuildForCurrentVehicle, BindForCombat/BindForRest, drag-cast surface). It
just no longer carries its own canvas hierarchy — the vehicle prefab is the
canvas owner.

## Final-game picture this serves

ADR-0011 forbids parallel storage / bimodal paths. `VehicleBarStack.prefab` as
a sibling canvas under `LaneAxis` was a Slice-2.6 transitional shape — once
per-slot bars moved onto `VehicleHudAnchors`, the bar-stack prefab kept only
MainBar + BuffStripCanvas + drag-cast plane, and decorative children
(SubsystemArea/BottomImage) that no longer mean anything. Collapsing it
removes the last sibling canvas and makes the vehicle prefab the single owner
of its HUD overlay — same axis as the per-slot bars. Designer hand-places
MainBar like every other overlay element. Enemy archetype swap (Scout→Brute
mid-run) inherits the right overlay topology natively because the overlay
travels on the vehicle root.

The component itself stays — its public surface (BindForCombat, BindForRest,
RebuildForCurrentVehicle, hover/click events) is the load-bearing part. Only
the prefab shell goes.

## Authored values being destroyed

### Prefab: `Assets/Prefabs/CombatView/VehicleBarStack.prefab`
- Entire prefab asset deleted.
- Stale serialized refs that bind to this prefab's children (`_markersParent`,
  `_barsParent`, `_markerTemplate`, `_barTemplate`) — already orphans post
  Phase 1, deleted here.
- Decorative children with no consumer: `Bars` (orphan), `Markers` (orphan),
  `SubsystemArea/BottomImage` (decorative).
- `MainBarSlot` child — replaced by a `MainBar` child on each vehicle prefab's
  `HudAnchors` container, hand-placed by designer in Prefab Mode.

### Combat.prefab — LaneAxis canvases

- `LaneAxis/EnemyBarStackCanvas` — entire GameObject deleted.
- `LaneAxis/PlayerBarStackCanvas` — entire GameObject deleted.
- 4 propertyPath overrides on the nested CombatHud (`_enemyBarStack`,
  `_playerBarStack`, `_enemyBuffStrip`, `_playerBuffStrip`) — deleted; refs
  resolve at runtime via `HandleCombatRebuilt`.

### RestRoot.prefab

- Second `AuthorBarStackCanvas` call site in `AuthorRestRootPrefab`
  (CombatPrefabAuthor.cs:7820) — deleted. RestRoot inherits the new topology
  for free because `RestPickerController.Awake` already runtime-resolves the
  bar stack via `scope.GetComponentInChildren<VehicleBarStack>(includeInactive:
  true)` (line 172) and the bar stack now lives under the PlayerVehicle
  scoped beneath RestRoot. RestRoot keeps `PlayerBarStackCanvas` GameObject
  removed — no replacement needed.

### CombatScene.unity

- propertyPath `_enemyBarStackCanvas` (line 143) + `_playerBarStack` (line
  484) — investigated as part of scene cleanup pass; default to fileID 0 (refs
  resolve runtime).

### CombatHud.cs

- `_enemyBarStack`, `_playerBarStack`, `_enemyBuffStrip`, `_playerBuffStrip` —
  4 SerializeFields deleted; replaced by runtime resolution in new
  `ResolveHudOverlayRefs()` method called from `HandleCombatRebuilt`.
- `BuildEnemyBarStack` + `BuildPlayerBarStack` (lines 1872-1900) — deleted
  entirely. They register `_*BarStack.transform.parent` (=BarStackCanvas) as
  a `VehiclePositionAnimator` follower; after the cut, the vehicle root IS
  the mount-child and the mount IS the animator's drive target, so any
  registration would double-drive lane swap motion.

### VehicleBarStack.cs

- `_mainBarPrefab` (line 73) — deleted; MainBar now authored on vehicle prefab.
- `_mainBarSlot` (line 74) — deleted; resolved via `_hudAnchors.MainBar`.
- `_side` SerializeField (line 95) — deleted; replaced by `Side side` as a
  positional parameter on `BindForCombat(...)` (Amendment 3 supersedes the
  initial `SetSide(Side)` proposal). The component never carries serialized
  identity; `_side` is set in the same call that reads it (BuildPerSlotBars
  via Bind), eliminating timing-order risk entirely.
- `EnsureRuntimeMainBar` (lines 838-868) — body simplified to single-line
  `_runtimeMainBar = _hudAnchors != null ? _hudAnchors.MainBar : null;`
  per Amendment 4. Zero `GetComponentInChildren` walks; zero fallback
  `Instantiate` paths. The dual-path bridge (Finding 2) is deleted.

### VehicleHudAnchors.cs

- ADD `MainBarWidget _mainBar` SerializeField + public `MainBarWidget MainBar
  { get; }` accessor (Amendment 4 selected Shape B — direct widget reference,
  not generic RectTransform). The widget is hand-wired at author time by the
  new `SeedMainBarAnchor` helper (Amendment 7), so runtime resolution is a
  single field read with zero `GetComponentInChildren` walks.

### CombatPrefabAuthor.cs

- `AuthorBarStackCanvas` method (definition at line 7081) — deleted.
- Both call sites:
  - Lines 6930-6931 (AuthorCombat — for Combat.prefab LaneAxis canvases) — deleted.
  - Line 7820 (AuthorRestRootPrefab) — deleted.
- SerializedObject wiring of `_enemyBarStack`/`_playerBarStack`/`_enemyBuffStrip`/
  `_playerBuffStrip` (lines 6972-6980) — deleted.
- BuffStripWidget resolution via `enemyBarStack.GetComponentInChildren` (lines
  6940-6948) — deleted.
- `CombatPreservePaths` entries at lines 7287-7288 (`LaneAxis/
  EnemyBarStackCanvas`, `LaneAxis/PlayerBarStackCanvas`) — deleted.
- Author logic that nests `MainBar.prefab` under each vehicle's
  `HudAnchors/MainBar` slot — ADDED (idempotent by source-prefab root name,
  same pattern as Phase 1 SubsystemBar/SubsystemMarker nesting).

## TD-surfaced risk hotspots — investigation results

### Risk 1: cold-boot null
- **TD prescription:** name a `ResolveHudOverlayRefs()` method, log
  `Debug.LogError` if the vehicle visual exists but `VehicleBarStack`
  component is missing.
- **Status:** Method-name + loud-fail plan locked. No surprises.

### Risk 2: archetype swap closure ghost
- **TD prescription:** confirm cleanup of prior-archetype hover/click handlers
  migrates cleanly from `RebuildForCurrentVehicle.ClearHandlers` to vehicle
  teardown via `DestroyImmediate`.
- **Investigation:** `CombatHud.SpawnVisualUnderMount` (CombatHud.cs:1848-
  1855) already destroys all prior mount children before instantiating the
  new archetype's visual. Closure ghosting is structurally impossible — the
  GameObject carrying the handlers is gone before any handler can re-fire.
- **Status:** CLEARED. Cleaner than the original RebuildForCurrentVehicle
  path because the lifetime tracks the prefab instance, not a runtime list.

### Risk 3: Rest hand-off
- **TD prescription:** confirm `RestPickerController` still resolves the bar
  stack under the new topology.
- **Investigation:** `RestPickerController.Awake:172` already calls
  `scope.GetComponentInChildren<VehicleBarStack>(includeInactive: true)` with
  `scope = transform.root`. After the collapse, the bar stack still resides
  under `transform.root` (PlayerVehicle under RestRoot), and the
  GetComponentInChildren walk crosses prefab boundaries — it finds the new
  location for free.
- **Status:** CLEARED. ZERO changes to RestPickerController.

### Risk 4: lane swap follower
- **TD prescription:** confirm bars travel with vehicle during lane swap.
- **Investigation:** Pre-cut, `BuildEnemyBarStack`/`BuildPlayerBarStack`
  register `_*BarStack.transform.parent` as a `VehiclePositionAnimator`
  follower (CombatHud.cs:1872-1900). Post-cut, the bar stack lives
  *under* the vehicle root, and the vehicle root IS the mount-child that
  the animator drives. Following automatically. Any explicit register call
  would double-drive.
- **Status:** CLEARED. Delete BuildEnemyBarStack + BuildPlayerBarStack
  entirely. Less code, not more.

## Safety-pass amendments (2026-06-30 second TD review)

Pre-execution second-pass TD review (verdict appended below) surfaced **seven
concrete Commit-1 must-haves** that override the original Commit-1 list. These
amendments are all blocking — no code touches until each is folded into the
plan.

### Amendment 1 — Canvas sortingOrder retier (Finding 1)

OLD `BarStackCanvas` (WorldSpace) sat at sortingOrder 4 — BELOW every other
canvas including Popups (60), HitZones (100), Debug (110). NEW HudAnchors was
seeded at 120 in Phase 1 (chosen to beat HitZones at 100 for Prefab-Mode
visibility), which would draw bars OVER outcome overlay (Popups 60) and Debug
widgets (110). That is a real visual regression.

Retier to:

| Canvas | RenderMode | Old | New |
|---|---|---|---|
| HitZonesCanvas (per vehicle) | WorldSpace | 100 | **15** |
| HudAnchors (per vehicle) | WorldSpace | 120 | **20** |
| BuffStrip (per vehicle, inside MainBar) | WorldSpace | 5 | **22** |

Resulting ordering keeps HudAnchors above HitZones (bars draw over hit zones),
BuffStrip above HudAnchors (buff icons draw over HP bars), and the whole
per-vehicle WorldSpace stack under Popups (60) and Debug (110) — outcome
overlay covers bars again, debug widgets stay on top.

Touch sites (verified by grep):
- `CombatPrefabAuthor.cs:1524` (HitZones, 100 → 15)
- `CombatPrefabAuthor.cs:1748` (HudAnchors, 120 → 20)
- `CombatPrefabAuthor.cs:1105` (BuffStripCanvas inside `AuthorMainBar`,
  5 → 22; `overrideSorting = true` already set at line 1104 so the
  retier honours intent)

### Amendment 2 — Canvas resolution via `GetComponentInParent<Canvas>` walk

`CombatHud.AssignWorldCamera`, `AssignBuffStripWorldCamera`, and
`RestPickerController` canvas resolution currently assume specific transform
paths. Post-collapse the canvases ride on different transforms (per-vehicle,
not per-side under LaneAxis). Switch all three to a `GetComponentInParent<
Canvas>` walk from the resolved widget — no hardcoded parent assumptions.

Touch sites (verified by grep):
- `CombatHud.cs:528-534` (`AssignBuffStripWorldCamera` walks
  `strip.transform.parent.GetComponent<Canvas>()` — switch to
  `strip.GetComponentInParent<Canvas>(includeInactive: true)`)
- `CombatHud.cs:536-545` (`AssignWorldCamera(VehicleBarStack, Camera)` walks
  `stack.transform.parent.GetComponent<Canvas>()` — switch to
  `stack.GetComponentInParent<Canvas>(includeInactive: true)`)
- `RestPickerController.cs:185-186` (same pattern, same fix)

### Amendment 3 — `Side` as `BindForCombat` parameter (Finding 4)

**REJECT** the proposed `SetSide(Side)` out-of-band setter. Add `Side side` as
a positional parameter to `VehicleBarStack.BindForCombat(...)`. Two call sites
in CombatHud (enemy + player), no test fixtures. Update both lines in
Commit 1; delete `_side` SerializeField; delete the SerializedObject stamping
block in CombatPrefabAuthor.cs:7148-7156 (already in Commit 2 cleanup).

This eliminates the timing-order concern entirely — `_side` is set in the
same call that uses it (BuildPerSlotBars reads it after Bind assigns it).

### Amendment 4 — `MainBarWidget MainBar { get; }` accessor shape (Finding 3)

VehicleHudAnchors exposes `MainBarWidget MainBar { get; }` (not the generic
`RectTransform`). Populated at author time by a new `SeedMainBarAnchor` helper
in CombatPrefabAuthor.cs that mirrors `SeedHudAnchor` — instantiates
MainBar.prefab under the HudAnchors container and writes the
`MainBarWidget` reference into `VehicleHudAnchors._mainBar` via
SerializedObject (idempotent: existing references survive re-author).

`VehicleBarStack.EnsureRuntimeMainBar` simplifies to:
```csharp
_runtimeMainBar = _hudAnchors != null ? _hudAnchors.MainBar : null;
```
No `GetComponentInChildren` walk; no fallback Instantiate path (Finding 2
bridge deleted).

### Amendment 5 — Lock `HandleCombatRebuilt` sequence with comment block (Finding 5)

Wrap the new sequence in a comment block that documents the load-bearing
ordering:

```csharp
// CombatHud:HandleCombatRebuilt sequence is fixed — DO NOT reorder.
// SpawnVehicleVisuals() must run first to destroy prior archetype + instantiate
// new vehicle prefabs (carrying VehicleBarStack + VehicleHudAnchors).
// ResolveHudOverlayRefs() then re-discovers _enemy/_playerBarStack +
// _enemy/_playerBuffStrip on the freshly spawned visuals. BindForCombat
// happens last because it reads the now-resolved Side via the BindForCombat
// parameter (Amendment 3) and the now-bound MainBar via _hudAnchors.MainBar
// (Amendment 4). Subscription teardown rides DestroyImmediate of the prior
// vehicle GameObject (Risk 2 cleared).
SpawnVehicleVisuals();
ResolveHudOverlayRefs();
// ...BindForCombat for both sides
```

### Amendment 6 — BuffStrip resolution + log-error in `ResolveHudOverlayRefs`

The new `ResolveHudOverlayRefs()` method also runtime-resolves BuffStripWidget
on each side (the SerializeField was deleted alongside BarStack refs).
Resolution: walk from the resolved VehicleBarStack to its MainBar to the
nested BuffStripWidget. Log error with side + archetype name if absent:

```csharp
[CombatHud] Vehicle visual '{archetypeName}' (side {side}) is missing a
VehicleBarStack component on its root — re-author this vehicle prefab via
'Author Player/Enemy Vehicle Prefab'. HUD overlay will be missing this combat.
```

### Amendment 7 — Author code idempotently wires MainBarWidget reference

`CombatPrefabAuthor.cs` `BuildVehicleHudAnchors` invokes the new
`SeedMainBarAnchor` helper which:
1. Creates-or-reuses a `MainBar` child RectTransform under the HudAnchors
   container.
2. Instantiates MainBar.prefab beneath it (idempotent by source-prefab root
   name, matching the SubsystemBar/SubsystemMarker pattern at lines 1803-
   1810).
3. Writes the resulting `MainBarWidget` reference into
   `VehicleHudAnchors._mainBar` via SerializedObject — survives re-authoring
   without losing designer tweaks to the bar's localPosition/localScale.

## Phasing plan (two commits) — AMENDED (final-gate re-split)

### PRECONDITION (final-gate Section D)

**DO NOT re-run any vehicle-prefab Author menu before Commit 1 lands.**

`CombatPrefabAuthor.cs:1748` already writes `HudAnchors` at sortingOrder=120
in the current working tree (Phase 1). Amendment 1's retier (120 → 20) ships
with Commit 1's structural move. If a vehicle-prefab Author menu runs in the
interim, fresh prefab YAML carries 120 and Finding 1's regression (bars over
Popups/Debug) activates between the two commits. Hold all Author menu
invocations until Commit 1's step 11 re-author block.

### Commit 1: structural move + author-code seam updates (atomic)

Commit 1 owns ALL deletions whose absence would crash the step-11 re-author
pass. SerializedObject wiring against deleted SerializeFields is the canonical
failure mode (FindProperty returns null → NPE on objectReferenceValue set);
deletion ships with the SerializeField deletion, not a follow-up commit.

1. Apply **Amendment 1** sortingOrder retier on three canvas author sites.
2. Apply **Amendment 2** canvas-walk in CombatHud + RestPickerController.
3. Add `MainBarWidget _mainBar` field + `MainBar` accessor to
   `VehicleHudAnchors.cs` (**Amendment 4**).
4. Add `SeedMainBarAnchor` helper in CombatPrefabAuthor.cs and call it from
   `BuildVehicleHudAnchors` (**Amendment 7**).
5. On `VehicleBarStack.cs`: add `Side side` parameter to `BindForCombat`;
   delete `_side` SerializeField; delete `_mainBarPrefab`, `_mainBarSlot`
   SerializeFields; rewrite `EnsureRuntimeMainBar` to single-line
   `_hudAnchors.MainBar` resolution (**Amendments 3 + 4**).
6. On `CombatHud.cs`: delete 4 SerializeFields; add `ResolveHudOverlayRefs()`
   (resolves BarStack + BuffStrip, log-errors with side + archetype name);
   call it between `SpawnVehicleVisuals` and `BindForCombat` with locked
   comment block (**Amendments 5 + 6**); update both `BindForCombat` call
   sites to pass `Side.Player`/`Side.Enemy`.
7. Delete `BuildEnemyBarStack` + `BuildPlayerBarStack` from CombatHud.
8. Delete `VehicleBarStack.prefab` asset.
9. Delete `LaneAxis/EnemyBarStackCanvas` + `LaneAxis/PlayerBarStackCanvas`
   GameObjects from Combat.prefab (also dropping the 4 nested CombatHud
   property overrides).
10. Delete second `AuthorBarStackCanvas` call at line 7820 (RestRoot).
11. **Author-code seam deletions** (final-gate Section F — promoted from
    Commit 2 because each one targets the four CombatHud SerializeFields
    deleted in step 6; leaving them for Commit 2 means step-11 re-author
    NPEs on `FindProperty` returning null):
    - Delete `AuthorBarStackCanvas` call sites at
      `CombatPrefabAuthor.cs:6930-6931` (AuthorCombat — Combat.prefab
      LaneAxis canvases).
    - Delete BuffStripWidget resolution at
      `CombatPrefabAuthor.cs:6940-6948` (resolves against deleted locals
      from the `AuthorBarStackCanvas` returns at 6930-6931).
    - Delete SerializedObject wiring at
      `CombatPrefabAuthor.cs:6972-6980`
      (`hudSo.FindProperty("_enemyBarStack")...` etc. — NPEs because the
      SerializeFields no longer exist).
    - Delete LogError at `CombatPrefabAuthor.cs:6989` mentioning the
      deleted fields by name.
12. Re-author 4 vehicle prefabs + Combat.prefab + RestRoot.prefab.

### Commit 2: pure dead-code + text cleanup (no functional impact)

After Commit 1's structural cut, Commit 2 is mechanical removal of method
bodies and prose that no longer have any callers or referents. Nothing in
Commit 2 affects re-author correctness — it could even ship as part of
Commit 1, but stays separate so review can isolate architectural change from
text/code-graveyard cleanup.

1. Delete `AuthorBarStackCanvas` method definition
   (`CombatPrefabAuthor.cs:7081-7159`) — body becomes unreachable after
   Commit 1 step 11 removes all call sites.
2. Delete `CombatPreservePaths` entries at
   `CombatPrefabAuthor.cs:7287-7288` (`LaneAxis/EnemyBarStackCanvas`,
   `LaneAxis/PlayerBarStackCanvas` — GameObjects already deleted in
   Commit 1 step 9).
3. Purge orphan comments referencing `BarStackCanvas` /
   `VehicleBarStack.prefab` / `MainBarSlot`. Confirmed touch-site
   (final-gate Section B): `BuffStripWidget.cs:54-57` carries a stale
   prose block referencing the deleted `BarStackCanvas`. Grep
   `Assets/Scripts/` for the three tokens and prune every hit.

## Validation criteria (Play Mode walkthrough)

After Commit 1, before merge:

1. **First-combat cold boot** — Both bar stacks + MainBar render on initial
   combat entry. `Debug.LogError` from `ResolveHudOverlayRefs()` does NOT
   fire.
2. **Archetype swap (Scout → Brute mid-run)** — New archetype's bars/MainBar
   render; no ghost-fire clicks land on the prior archetype's invisible
   handlers (Risk 2 closed structurally; verify in-engine anyway).
3. **Combat → Rest hand-off** — Enter Rest beacon; `RestPickerController`
   resolves bar stack; HP bars + part cards interact as before. No null refs.
4. **Lane swap animation** — Trigger position swap (any card that swaps lane
   roles); bars travel smoothly with each vehicle; no double-drive judder
   (Risk 4 closed).
5. **Sort-order ocular check** (final-gate Section E) — trigger
   CombatOutcomeOverlay (victory/defeat); confirm the overlay covers the
   per-vehicle HP/MainBar/BuffStrip stack (Popups 60 > HudAnchors 20 / BuffStrip
   22). Open Debug overlay (e.g. F-key debug stack); confirm debug widgets
   render OVER the HP bars (Debug 110 > HudAnchors 20). HitZones must render
   UNDER HudAnchors (15 < 20 → bars draw over their own hit zone).

Grep checks (must return zero hits in `Assets/Scripts/` after Commit 2; the
first two also zero after Commit 1):
- `BarStackCanvas`
- `VehicleBarStack.prefab`
- `_mainBarPrefab`
- `_mainBarSlot`
- `\b_side\b` (within `VehicleBarStack.cs` specifically — Amendment 3
  retires the SerializeField; project-wide hits in other files are
  acceptable noise)

## Technical Director Review

**Verdict:** APPROVE with three amendments + four risk hotspots
**Spawned at:** 2026-06-30 (mid-session, ahead of this capture)

**TD reasoning summary:**

- The component (Update loop, hover/click wiring, drag-cast surface,
  Bind/Rebuild API) is load-bearing — keep it. Only the prefab shell + sibling
  canvas under LaneAxis are the bridge artifacts. ADR-0011 clean.
- Per-vehicle authoring of MainBar matches the Phase 1 axis (per-slot
  widgets already authored per-vehicle). One axis, not two. No bimodal storage.
- Enemy archetype swap inherits the right overlay topology natively because
  the overlay travels on the vehicle root. Solves a class of subscriber-order
  race bugs (Bug #17, #22 in the project log) by construction.
- Amendments (initial — superseded where noted by the second-pass review
  below):
  1. ~~Replace `_side` SerializeField with `SetSide(Side)` runtime call~~ —
     **SUPERSEDED by Amendment 3:** `Side side` is a parameter on
     `BindForCombat`, not an out-of-band setter. Same goal (eliminate
     serialized identity), better shape (no timing-order risk).
  2. Add explicit `ResolveHudOverlayRefs()` method on CombatHud — don't bury
     the runtime resolve in `HandleCombatRebuilt`'s body. **Retained.**
  3. Two commits, not one: structural move atomic; author cleanup separate so
     review can isolate the architectural change from the author-script
     churn. **Retained.**
- Four risk hotspots: cold-boot null, archetype swap closure ghost, Rest
  hand-off, lane swap follower. (All four investigated above; all four
  cleared.)

### Second-pass review — health/safety/optimization (2026-06-30)

**Verdict:** CONCERNS → conditional APPROVE after the seven Commit-1
amendments above are folded in.

User-requested second pass after the four risk hotspots cleared, asking TD to
stress-test the plan for perf/GC/lifecycle/sort-order/raycaster concerns
beyond the primary verdict.

**Findings surfaced:**

1. **Sort-order regression** — pre-cut bars at sortingOrder 4 (under
   everything); post-cut HudAnchors at 120 was set in Phase 1 to beat
   HitZones (100) for Prefab-Mode visibility, which inverts the OLD ordering
   versus Popups (60) and Debug (110). Real visual regression — outcome
   overlay would no longer cover bars; debug widgets would be occluded.
   **Fix:** Amendment 1 (HitZones 100→15, HudAnchors 120→20, BuffStrip 5→22).
2. **EnsureRuntimeMainBar dual-path** — current fallback `Instantiate(_main
   BarPrefab)` is the ADR-0011 bridge candidate. **Fix:** Amendment 4
   collapses to single-line `_hudAnchors.MainBar` resolution; zero fallback
   paths remain.
3. **MainBar accessor return type** — Shape B selected
   (`MainBarWidget MainBar { get; }`). Saves GetComponentInChildren walks at
   the cost of coupling VehicleHudAnchors to MainBarWidget — acceptable
   because MainBarWidget IS the per-vehicle HUD's single anchor for the
   structural-slot bar. **Fix:** Amendment 4.
4. **`SetSide` rejected** — out-of-band setter creates timing-order risk.
   **Fix:** Amendment 3 — `Side side` parameter on `BindForCombat`. Two call
   sites + zero test fixtures = safe refactor.
5. **HandleCombatRebuilt sequence is load-bearing** — Spawn → Resolve → Bind
   ordering is the contract; future maintainers must not reorder. **Fix:**
   Amendment 5 — locked comment block above the sequence.
6. **Placement (Finding 6)** — VehicleBarStack component sits on the
   HudAnchors layer (Shape B), sharing the WorldSpace Canvas + GraphicRay
   caster + nested per-slot widgets + MainBar. One transform root, one
   Canvas, one logical owner of the per-vehicle HUD overlay.
7. **Cold-fail log text (Finding 7)** — confirmed exact text including side
   + archetype name. **Fix:** Amendment 6.

**Net effect on phasing:**

Commit 1 grows by three concrete fixes (sortOrder, canvas-walk, BindForCombat
parameter refactor) but loses the SetSide method addition — net wash. Commit 2
unchanged.

**Blockers cleared:** all seven amendments are self-contained, fit in Commit 1,
and need no further TD consultation.

## User approval

- Drafted: 2026-06-30
- Second-pass amendments: 2026-06-30
- Approved by: _pending_
- Notes: User directives during this session — "id actually like mainbar to
  undergo this operation aswell if its not risky" → "cant we just get rid of
  vehiclebarstack entirely and move the mainbar and the buffstrip with the
  subsystem bars?" → "yea get TD involved so we do this clean and precise."
  → "we need to clear all risks." → "can you and TD please do 1 health safety
  and optimization pass according to these results before we continue."

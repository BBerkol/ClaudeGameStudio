# TD Verdict — ADR-0018 P5: `Debug` + `Combat_HUD` canvas deletions, then the registry gate

Date: 2026-09-17
Gate: TD-ARCHITECTURE (final ADR-0018 phase)
ADR: `docs/architecture/adr-0018-ugui-retention-transient-world-anchored-annotations.md` (Amendment A registry)

## TD Verdict

**TD-ARCHITECTURE: APPROVE — with three mandated deltas (D1/D2/D3), one corrected
premise, and one corrected edit shape.**

Delete **both** canvases. Neither is deferrable and neither is allow-listable:
allow-listing a canvas that renders nothing is ADR-0011 forbidden pattern #6
(stub/vestigial retention) dressed as a registry row, and it would make the gate
assert the opposite of what the ADR says the category is. Defer is worse than
either — the gate is the last P5 deliverable and there is no second window.

The slice is APPROVED **conditional on the green-test attestation in §7 being
produced with actual runner output**, not an expectation.

---

## 0. Corrected premise (load-bearing — the brief got this wrong)

> "`override_sorting_gate` is anchored to a Canvas fileID in this very prefab
> (which one? verify — if it anchors to a canvas being deleted, the gate must be
> re-pointed in the same commit)"

**It does not.** `tools/ci/grep-gates.sh:441-443` carries exactly one row:

```
"Assets/Prefabs/CombatView/MainBar.prefab|577952370169777220|BuffStripCanvas (sortingOrder 22 ...)"
```

The CardHand row was deleted 2026-09-13 and no CombatHud.prefab fileID has been
anchored since. **Neither deletion touches `override_sorting_gate`; nothing needs
re-pointing.**

The surgical-YAML-over-re-author house rule still stands, but on its own two
merits, not this one:
1. `project_author_scenes_never_idempotent` — an author run reminds fileIDs
   across the whole prefab, producing a diff nobody can review.
2. `CombatWidgetPrefabWiring_Test.cs:106-111` records that
   `CombatHud._targetingReadoutPrefab` is a **hand-wired YAML edit** that a full
   `AuthorCombatHud` run would silently drop. Re-authoring CombatHud.prefab today
   costs that wire.

**Ruling: surgical YAML edits to `CombatHud.prefab`. No `AuthorCombatHud` run in
this slice.** The author-tool edits (§3) are for the *next* run, whenever that is.

---

## 1. Finding 1 — `Debug` canvas: DELETE

Verified. GO `&5484149036302222134`, Canvas `&3742852243510443251`,
ScreenSpaceCamera/110. `m_Children: []` — zero children in YAML. The only
`_debugGroup` consumer in the entire repo is the null-guarded sweep at
`CombatHud.cs:428`. Nothing instantiates into it. Fully dead.

No behavioural risk. The deletion is pure subtraction.

---

## 2. Finding 2 — `Combat_HUD` canvas: DELETE, with a corrected edit shape

Verified end to end:

- `CombatPrefabAuthor.cs:2502` parents the BuffTooltip PrefabInstance under
  `hudCanvasGo.transform` — confirmed in YAML: PrefabInstance
  `&2548849415120471836`, `m_TransformParent: {fileID: 3731869733579976421}`
  (= Combat_HUD's RectTransform).
- `CombatHud.cs:1130-1136` reparents it to `Popups` inside `BuildBuffTooltip()`,
  called from `Awake` at `:291` — before any paint. Confirmed.
- Combat_HUD's RectTransform `m_Children` contains exactly one entry
  (`5428903171222390665` = the tooltip). After the re-home it is empty.
- Neither the Canvas nor any descendant carries a `Graphic`. Confirmed zero
  rendered pixels in any played frame.

### 2.1 CORRECTION to the proposed author edit (ordering question, answered)

The brief asked whether re-homing `:2502` to `Popups` requires moving the Popups
creation block (`:2533-2554`) above it. **No — and moving it would be the wrong
fix.**

If Popups creation moves up and the tooltip Instantiate stays at `:2502`, the
tooltip becomes Popups **child index 0**, because the outcome overlay / card
picker / part picker are instantiated at `:2561-2589`. Today's runtime order is
`[outcome, cardPicker, partPicker, tooltip]` — the tooltip is **last**, forced by
`SetAsLastSibling()` at `CombatHud.cs:1139`. Moving Popups up inverts that.

**Correct edit: leave the Popups creation block where it is (`:2538`) and move
the BuffTooltip `InstantiatePrefab` statement DOWN**, to immediately after the
`PartRewardPicker` block (after `:2589`), parented to `popupsGroup`. This
resolves the ordering dependency *and* reproduces `SetAsLastSibling()` in the
authored asset, which is what makes the zero-visual-delta claim true (§7.3). The
`buffTooltip` local is consumed at `:2649`, well after — no other constraint.

Matching YAML surgery: **append** `- {fileID: 5428903171222390665}` to the Popups
RectTransform's `m_Children` (currently 3 entries), i.e. last.

### 2.2 `_canvas` consumers — full enumeration (answers the brief's ask)

Three, all in `CombatHud.cs`, plus one YAML wire:

| Site | Disposition |
|---|---|
| `:137` `[SerializeField] private Canvas _canvas;` | **DELETE field** |
| `:427` `ApplyScreenSpaceCamera(_canvas, mainCam);` | **DELETE call** |
| `:278` `BuildCanvas();` + `:1238-1258` method body | **DELETE both** — see D1 |
| `CombatHud.prefab:257` `_canvas: {fileID: 4006782715438962496}` | **DELETE line** |
| `CombatPrefabAuthor.cs:2630` `so.FindProperty("_canvas")...= hudCanvas;` | **DELETE line** |

`:139-141`'s comment block ("Debug/Popups copy scaler settings from `_canvas`…",
"Combat_HUD=10") describes a three-canvas arrangement that becomes a one-canvas
arrangement. Rewrite, do not leave — a comment describing a deleted structure is
ADR-0011 forbidden pattern #7.

Note: `_canvas` on `DamagePopupSpawner.cs:32` and `_canvasRect` on
`BuffTooltipWidget.cs:42` are unrelated identifiers. Do not sweep by name.

---

## 3. Mandated deltas

### D1 — `BuildCanvas()` must be DELETED, not null-guarded. **BLOCKING.**

`CombatHud.cs:1238-1258` reads `if (_canvas != null) return;` and otherwise
`new GameObject("Combat_HUD")` with a Canvas at sortingOrder 10. Once the
authored canvas is deleted, `_canvas` is null at every `Awake`, so **this method
re-creates a `Combat_HUD` canvas at runtime in every single play session.** The
registry gate is a static YAML/source scan and would never see it — P5 would ship
green while the canvas it deleted is alive on screen.

This is also pre-existing ADR-0011 forbidden pattern #3 (bimodal authored/runtime
path), so deleting it is owed regardless.

Delete: the `BuildCanvas()` call at `:278` and the method at `:1238-1258`.

### D2 — the tooltip's host-camera cache becomes ordering-dependent. **BLOCKING.**

This is the only real regression vector in the slice, and it is invisible to YAML
review.

`CombatHud.cs:279-286` carries an explicit comment recording that
`EnsureScreenSpaceCameras()` was **moved earlier in `Awake`** precisely so the
Popups canvas has its `worldCamera` bound *before* `BuildBuffTooltip()` reparents
the tooltip — because the reparent fires
`BuffTooltipWidget.OnTransformParentChanged` → `CacheHostCanvas()`
(`BuffTooltipWidget.cs:68-79`), which snapshots `hostCanvas.worldCamera` into
`_hostCanvasCamera`. Without the ordering, `Show()` failed silently.

Author the tooltip under Popups and **the reparent never happens**, so
`CacheHostCanvas()` only ever runs from `BuffTooltipWidget.Awake` (`:52-55`). At
that moment `Popups`' `m_Camera` is `{fileID: 0}` (verified in YAML — the author
cannot reference a scene camera) and child-vs-parent `Awake` order is not
guaranteed. `_hostCanvasCamera` caches **null**, and
`ScreenPointToLocalPointInRectangle` at `:172` against a ScreenSpaceCamera canvas
with a null camera mispositions or drops the tooltip. The 2026-09 bug returns,
and this time nothing reparents to paper over it.

**Mandated fix — stop caching the camera *value*.** Cache the `Canvas` reference
once in `Awake`; read `renderMode` / `worldCamera` off it at `Show` time:

- Replace field `_hostCanvasCamera` (+ `_hostCanvasIsOverlay`) with a single
  `private Canvas _hostCanvas;`
- `CacheHostCanvas()` keeps `_canvasRect` and `_hostCanvas`; both are stable for
  the object's whole life once the reparent is gone.
- In `ShowKeyed` (`:172`), compute locally:
  `Camera cam = (_hostCanvas != null && _hostCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _hostCanvas.worldCamera : null;`
- **Delete `OnTransformParentChanged` (`:68`)** — with the reparent gone it is
  vestigial (ADR-0011 #4). Verified: nothing else reparents this widget.

Camera binding is *inherently* late (per-scene `Camera.main`). Caching its value
at `Awake` was always the latent defect; the reparent was accidental insulation.
`Show` fires on hover-enter, not per frame, so two field reads off a cached ref
cost nothing (Lens 2 below).

**Rejected alternative:** have `CombatHud` call a new public
`_buffTooltip.RefreshHostCanvas()` after `EnsureScreenSpaceCameras()`. It works,
but it preserves a cross-class temporal contract for no benefit and is
bridge-shaped — a second method whose only job is to undo a stale cache.

### D3 — `CombatPrefabStageHook.AutoHideChildren` must drop `"Debug"`. **BLOCKING.**

`CombatPrefabStageHook.cs:31` is `{ "Popups", "Debug" }`. After the deletion
`FindRecursive` returns null and the entry silently no-ops — a dead name pointing
at a deleted GameObject. Change to `{ "Popups" }` in the same commit. The class
xmldoc (`:7-21`) names Debug four times and must be rewritten with it.

### D4 — orphaned `PopupsGroup()` + the alarm it carried. **Non-blocking, same commit.**

`PopupsGroup()` (`CombatHud.cs:1269-1277`) has exactly one caller:
`BuildBuffTooltip():1121`. Removing the reparent orphans it.

Ruling:
- **Delete `PopupsGroup()`.**
- **Relocate its LogError** into `EnsureScreenSpaceCameras()` as an `else` on the
  `_popupsGroup` null-guard at `:429`, reworded to the now-true fault: *an
  unwired `_popupsGroup` means the tooltip's host canvas never gets its camera
  bound, so hover descriptions position incorrectly.* Do not simply drop the
  alarm — with D2 in place, an unwired `_popupsGroup` is still a real runtime
  fault, just at a different site.
- `_popupsGroup` itself survives (consumer at `:429`, RequiredWiring row at
  `CombatWidgetPrefabWiring_Test.cs:116`).
- **Keep `BuildBuffTooltip()`** reduced to its `_buffTooltip == null` LogError —
  that alarm catches a scene-instance override the EditMode test cannot see.
  Rewrite its xmldoc (`:1113-1118`) to say the tooltip is *authored* under
  Popups, and why order 60 still matters.

### D5 — `SetAsLastSibling()` at `:1139`. **Discretionary — user may overrule.**

I rule **delete it**. Its stated reason ("so any later-authored popup can't paint
over an active hover") is false for all three current siblings: they are
UIDocuments whose draw order is `UIDocument.sortingOrder`, not hierarchy —
`CombatPrefabAuthor.cs:2573-2585` states this explicitly. The tooltip is the only
UGUI renderer under Popups, so its sibling index affects nothing. Keeping it
leaves a no-op call plus a comment asserting something untrue.

Cost of overruling me: one no-op call per Awake, and the comment must still be
rewritten to state the real reason. If a second **UGUI** popup ever lands under
Popups, that slice re-derives the ordering — and will have to anyway.

---

## 4. The registry gate — APPROVE the name-anchored design, with one structural addition

The drafted allow-list is **exactly correct**. Independently re-derived by
resolving every `!u!223` block to its owning GameObject via `m_GameObject` across
the whole repo:

| Canvas name | Instances | Where |
|---|---|---|
| `Popups` (60) | 1 | CombatHud.prefab |
| `DamagePopupCanvas` (30) | 1 | DamagePopupSpawner.prefab + runtime fallback |
| `BuffStripCanvas` (22, overrideSorting) | 1 | MainBar.prefab (child of HudAnchors) |
| `HudAnchors` (20) | 4 | PlayerVehicle + Dredge + DuneSkimmer + IronShepherd |
| `HitZonesCanvas` (15) | 4 | same four vehicle prefabs |
| `IntentCanvas` (5) | runtime | `CombatHud.cs:1159` |
| `TargetingReadoutCanvas` (30) | runtime | `CombatHud.cs:1200` |
| ~~`Combat_HUD` (10)~~ | 1 | **deleted by this slice** |
| ~~`Debug` (110)~~ | 1 | **deleted by this slice** |

12 YAML instances across 7 files; 7 allow-listed names post-deletion. 6 registry
members + `BuffStripCanvas` seated as a child per Amendment A.

**Count reconciliation:** the audit reported "13 instances / 8 distinct names."
My sweep finds 12 YAML + 2 runtime-only names = 9 distinct. The *allow-list* is
identical either way, so this does not change the gate — but reconcile the number
before it is quoted in the ADR, because "8" is what a future reader will check
against.

### 4.1 ADDITION — the gate needs a CODE half. **BLOCKING.**

A YAML-only gate is blind to `IntentCanvas`, `TargetingReadoutCanvas` and the
`DamagePopupCanvas` runtime path — i.e. blind to a third of its own registry —
and blind to exactly the D1 resurrection failure. Two halves:

**(a) YAML half.** Glob `Assets/**/*.prefab` and `Assets/**/*.unity` (**glob, never
a pinned file list** — see negative test 3). For every `--- !u!223 &<id>` block,
read `m_GameObject: {fileID: X}` and resolve `X` → `m_Name` **within the same
file**. Never use YAML block adjacency; Unity does not guarantee block order.
Fire on any name not in the allow-list.

**(b) CODE half.** In `Assets/Scripts/**`, `AddComponent<Canvas>()` must appear at
exactly two pinned sites after D1: `CombatHud.cs` (inside `BuildWorldCanvas`) and
`DamagePopupSpawner.cs:62`. Fire on any third site. Additionally, every
`BuildWorldCanvas("<Name>"` string literal must be in the allow-list. Do **not**
gate `Assets/Editor/CombatPrefabAuthor.cs`'s four construction sites — their
output is YAML and is already covered by (a); gating them would fire on every
legitimate author edit.

**(c) Retired-name gate.** Two rows through the script's existing `gate` helper:
`new GameObject("Debug"` and `GameObject("Combat_HUD"`. Narrow literals — bare
`"Debug"` would match every `Debug.Log`.

### 4.2 Anti-vacuity, per the script's own header

- Zero canvases found across the repo ⇒ hard FAIL ("the parser or the glob is
  broken"), not a silent pass. Same discipline as `containment_gate`'s
  missing-file branch.
- Allow-list length > 7 ⇒ hard FAIL with *"allow-list exceeds the ADR-0018
  Amendment A cap — re-derive the category before seating a member."* The gate
  polices its own growth; adding an 8th entry should cost a conversation, not a
  one-line edit.

### 4.3 Negative-test procedure — APPROVE the 4 steps, plus a mandatory 5th

1. Add a `!u!223` Canvas on a new GO named `Bogus` in a scratch prefab → fires,
   naming `Bogus`. (YAML half is live)
2. Rename `HitZonesCanvas` → `HitZones` in one vehicle prefab → fires. (name
   resolution goes through `m_GameObject`, not adjacency)
3. **Nested-PrefabInstance blind spot.** Add a Canvas to the BuffTooltip *source*
   prefab (guid `ce207c7e503074940bfd16e0826d5431`, nested inside
   CombatHud.prefab) → gate fires **against that source prefab's own file**, and
   must NOT be expected to fire on CombatHud.prefab. A nested canvas never
   materialises as a `!u!223` block in the host — it appears only as
   `m_Modifications`. The test's real assertion is therefore *"the gate globs all
   prefab files"*: 7 files carry canvases today, and a pinned list goes stale the
   day an 8th prefab lands.
4. Delete one allow-list entry → fires on a shipping canvas. (list is
   load-bearing)
5. **NEW, mandatory — code half.** (a) Add `AddComponent<Canvas>()` to any third
   file under `Assets/Scripts/` → fires. (b) Change `BuildWorldCanvas("IntentCanvas"`
   → `BuildWorldCanvas("Intent"` → fires. Without step 5 the code half can ship
   vacuous, which is the precise failure the script's header exists to prevent —
   and, since `grep-gates.sh` is the only server-side enforcement (no CI runner,
   `UNITY_LICENSE` unset), a vacuous half is indistinguishable from a clean repo.

---

## 5. Files to be touched

Unity repo — `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\`:

1. `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\Assets\Prefabs\CombatView\CombatHud.prefab` — YAML surgery (§6.2)
2. `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\Assets\Editor\CombatPrefabAuthor.cs` — delete `:2456-2474` (Combat_HUD creation), delete `:2509-2531` (Debug creation), delete `:2630-2631` (`_canvas` + `_debugGroup` wires), move the BuffTooltip `InstantiatePrefab` from `:2502` to after `:2589` re-parented to `popupsGroup`, rewrite the `:2446-2455` three-canvas comment
3. `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\Assets\Editor\CombatPrefabStageHook.cs` — D3 (`:31` + xmldoc `:7-21`)
4. `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\Assets\Scripts\CombatView\CombatHud.cs` — delete `_canvas` `:137`, `_debugGroup` `:146`, `BuildCanvas()` call `:278` + body `:1238-1258`, sweep lines `:427-428`, reparent `:1130-1136`, `SetAsLastSibling` `:1139` (D5), `PopupsGroup()` `:1269-1277` (D4); rewrite comments `:139-141`, `:279-286`, `:416-418`, `:1113-1118`, `:1260-1268`; add the relocated `_popupsGroup` alarm at `:429`
5. `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\Assets\Scripts\CombatView\BuffTooltipWidget.cs` — D2 (`:42-47` fields, `:52-55` Awake, `:63-79` cache + delete `OnTransformParentChanged`, `:169-172` Show)
6. `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\Assets\Scripts\CombatView\RunHUDHost.cs` — `:41` `[Tooltip]` string says "below Combat_HUD 10". This is **designer-facing Inspector text naming a canvas that will not exist**; re-anchor to the outcome overlay (sortingOrder 0)
7. `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\Assets\Editor\UIToolkitInitializer.cs` — `:10` xmldoc anchors PanelSettings scaling to "Combat_HUD Canvas scaling (1920x1080 / ScaleWithScreenSize / …)". Those values survive verbatim on `Popups`' CanvasScaler; re-anchor the comment there
8. `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\Assets\Scripts\UI\CombatHudPanelController.cs` — `:11` xmldoc "replacement for the UGUI Combat_HUD canvas". Keep the lineage sentence (it is true history) but past-tense it, and re-state the `sortingOrder -10` rationale against the outcome overlay (0), which is the surviving relationship
9. `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\tools\ci\grep-gates.sh` — add `canvas_registry_gate()` per §4 + its `canvas_registry_gate` invocation before the `fail` check
10. `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\Assets\Tests\EditMode\CombatView\CombatWidgetPrefabWiring_Test.cs` — **NO EDIT REQUIRED, verified.** `_canvas` and `_debugGroup` appear in neither `RequiredWiring` nor `KnownUnwired`, and the fixture does not enumerate all serialized fields. Named here so the capture records that it was checked rather than assumed

Docs repo — `C:\ClaudeCreations\Madmax Roguelike\`:

11. `C:\ClaudeCreations\Madmax Roguelike\docs\architecture\adr-0018-ugui-retention-transient-world-anchored-annotations.md` — P5 row → DONE, gate name + allow-list recorded, and a Consequences line: *the category audit found two canvases that were registry-shaped but rendered nothing; the gate is what stops a third*
12. `C:\ClaudeCreations\Madmax Roguelike\production\remediation-plan-2026-09-08.md` — P5 row → DONE with baselines
13. `C:\ClaudeCreations\Madmax Roguelike\production\polish-captures\2026-09-17-p5-canvas-deletions.md` — the capture, quoting §6 below

---

## 6. Authored values destroyed (quote this block into the capture)

### 6.1 `Debug` — destroyed in full

- GameObject `&5484149036302222134`: `m_Name: Debug`, `m_IsActive: 1`, `m_Layer: 0`, child index **1** of the CombatHud root
- RectTransform `&1360351997342636089`: `m_LocalScale (0,0,0)`, `m_AnchorMin (0,0)`, `m_AnchorMax (0,0)`, `m_AnchoredPosition (0,0)`, `m_SizeDelta (0,0)`, `m_Pivot (0,0)`, `m_Father 189484520493424105`
- Canvas `&3742852243510443251`: `m_RenderMode: 1` (ScreenSpaceCamera), `m_Camera: {fileID: 0}`, `m_PlaneDistance: 1`, **`m_SortingOrder: 110`**, `m_OverrideSorting: 0`, `m_PixelPerfect: 0`, `m_TargetDisplay: 0`, `m_ReceivesEvents: 1`
- CanvasScaler `&6972783002601490691`: `m_UiScaleMode: 1`, `m_ReferenceResolution: 1920x1080`, `m_ScreenMatchMode: 0`, `m_MatchWidthOrHeight: 0.5`, `m_ReferencePixelsPerUnit: 100`
- GraphicRaycaster `&3817684680613482677`: `m_IgnoreReversedGraphics: 1`, `m_BlockingObjects: 0`, `m_BlockingMask: 4294967295`
- `CombatHud._debugGroup` wire → `1360351997342636089` (prefab `:258`)
- Author-tool source of the above: `CombatPrefabAuthor.cs:2509-2531` + wire `:2631`
- `CombatPrefabStageHook` auto-hide membership (`"Debug"`) — the designer
  affordance that hid this canvas in Prefab Mode dies with it

### 6.2 `Combat_HUD` — destroyed in full

- GameObject `&6552448530280038589`: `m_Name: Combat_HUD`, `m_IsActive: 1`, child index **0** of the CombatHud root
- RectTransform `&3731869733579976421`: `m_LocalScale (0,0,0)`, `m_AnchorMin (0,0)`, `m_AnchorMax (0,0)`, `m_AnchoredPosition (0,0)`, `m_SizeDelta (0,0)`, `m_Pivot (0,0)`, `m_Father 189484520493424105`, `m_Children: [5428903171222390665]`
- Canvas `&4006782715438962496`: `m_RenderMode: 1`, `m_Camera: {fileID: 0}`, `m_PlaneDistance: 1`, **`m_SortingOrder: 10`**, `m_OverrideSorting: 0`
- CanvasScaler `&8998072753662016966`: identical settings to Debug's (1920x1080 / mode 1 / match 0.5 / 100 ppu)
- GraphicRaycaster `&90887311773905656`: identical settings to Debug's
- `CombatHud._canvas` wire → `4006782715438962496` (prefab `:257`)
- BuffTooltip PrefabInstance `&2548849415120471836`: **`m_TransformParent` changes `3731869733579976421` → `387827311396018076`.** Every other modification is PRESERVED verbatim — `m_Pivot (0.5, 0)`, `m_AnchorMin/Max (0.5, 0.5)`, `m_SizeDelta (320, 110)`, `m_AnchoredPosition (0,0)`, `m_LocalPosition (0,0,0)`, identity rotation, both `m_sharedMaterial` overrides, `m_Name: BuffTooltip`. **If any of these change, the edit is wrong.**
- Author-tool source: `CombatPrefabAuthor.cs:2456-2474` + wire `:2630`

### 6.3 Values that survive but lose their anchor (comment-only, do not delete)

- `CombatHudPanel`'s `UIDocument.sortingOrder = -10` was chosen relative to
  Combat_HUD (10). It remains correct — it still orders the HUD panel below the
  outcome overlay (0), which is the real surviving relationship — but its
  *rationale* must be re-stated in those terms in files 6/7/8. The number does
  not change.
- The 1920x1080 / ScaleWithScreenSize / 0.5 scaler triple survives verbatim on
  `Popups`' CanvasScaler. `UIToolkitInitializer`'s PanelSettings match stays
  valid; only the comment's referent moves.

---

## 7. Sequencing, tests, and the one-look playtest

### 7.1 One commit, not a split

Any split leaves an intermediate state that is *worse* than either endpoint:
deletions without D1 ship a runtime canvas resurrection; the gate without the
deletions fires red on every commit. And the commit hook runs `grep-gates.sh` —
a red intermediate blocks the second half of its own slice.

Order within the commit:
1. Write the capture (`production/polish-captures/2026-09-17-p5-canvas-deletions.md`), user-approved, **before** any edit — hook requirement, date-keyed to today.
2. Code edits: D1, D2, D3, D4, D5 + author-tool edits (files 2-8).
3. YAML surgery on `CombatHud.prefab` (file 1), per §6.2's preserve-list.
4. `canvas_registry_gate` + allow-list (file 9).
5. Negative-test all 5 steps from §4.3, reverting each probe.
6. Run the suites.
7. Docs (files 11-13).

### 7.2 Green-test attestation — APPROVE is conditional on this

- Baseline to match **exactly**: EditMode **1297 / 1296 passed / 0 failed / 1 skipped**, PlayMode **19 / 19**. No test is added, deleted or renamed by this slice, so any movement in these numbers means something broke — stop and diagnose rather than re-baseline.
- `grep-cE 'error CS' TestResults/editmode.log` must be **0**. Batchmode exit code lies about compile failures; do not trust it.
- `tools/ci/grep-gates.sh` must print `[grep-gates] all gates clean`.
- Paste the actual runner tail into the capture. "Expected green" is not green.

### 7.3 Zero-visual-delta reasoning — CONFIRMED, but only with D2

The reasoning holds, and the proof is specific:

- Runtime parent today is already `Popups` (`CombatHud.cs:1135`, in `Awake` at `:291`, before first paint). Authored parent becomes the same RectTransform `&387827311396018076`.
- Sibling index matches (**last**), because §2.1 moves the author's Instantiate call after the part picker — reproducing `SetAsLastSibling()`.
- Host Canvas, `sortingOrder 60`, scaler settings, and every tooltip RectTransform override are byte-identical (§6.2 preserve-list).
- `Debug` and `Combat_HUD` render zero pixels: no `Graphic` anywhere beneath either, and `Combat_HUD` has no children left after the re-home.

**The only behavioural difference is *when* the widget resolves its host camera —
which is exactly D2. Without D2 the zero-delta claim is FALSE**, and it fails in
the worst possible way: silently, only on hover, only at runtime, invisible to
every automated check in this slice.

### 7.4 The one-look playtest

One combat. Hover a buff badge on **each** vehicle's buff strip, then hover a
badge while the cursor sits over a `SlotTargetRing`.

That single sequence exercises all three things the slice can break: the tooltip
renders at all (authored parent correct), it positions at the cursor (D2 camera
resolution), and it still paints above `HitZonesCanvas` 15 / `HudAnchors` 20
(Popups 60 unchanged — the original reason the tooltip lives there). Nothing else
in this slice has a visible surface.

Also confirm in the Console: no `[CombatHud] _popupsGroup is not wired` (the D4
relocated alarm) and no NRE at `Awake`.

---

## 8. Three-lens self-audit

### Lens 1 — Codebase health

- **ADR-0011 drift, grepped not assumed.** Three live instances found, all folded
  into this slice: `BuildCanvas()` bimodal authored/runtime path (#3, D1);
  `OnTransformParentChanged` going vestigial (#4, D2); `AutoHideChildren` dead
  name (#4, D3). Plus a fourth **left out of scope and flagged**:
  `DamagePopupSpawner.cs:40` `if (_canvas == null) BuildCanvas();` is the
  identical bimodal shape against an authored canvas in
  `DamagePopupSpawner.prefab`. Same defect class, different prefab — it does not
  block P5 (the canvas there is allow-listed and does render), but it is now the
  last known instance of this pattern and should be the first line of the next
  cleanup slice. Naming it so it does not get rediscovered in six months.
- **Subscription lifecycle.** Untouched. No Bind/OnEnable pairs are added or
  removed; `RunOverlayEvents` (`:269-270`, Awake↔OnDestroy) is not in scope.
  Confirmed clean.
- **Single-responsibility.** `CombatHud` gets strictly smaller: one field, one
  builder, one accessor, one reparent branch removed. `BuffTooltipWidget` takes
  on resolving its own camera — correct owner, since it is the only consumer.
- **Duplication vs premature abstraction.** No new helper. The camera-resolution
  expression has exactly one call site (`ShowKeyed`); inlining it is correct
  under ADR-0011 until a second caller exists.
- **Teardown races.** D2 *removes* one: today a `SetParent` during teardown would
  fire `OnTransformParentChanged` → `CacheHostCanvas` on a widget mid-destroy.
  With the callback gone that path cannot exist. The `_hostCanvas` ref is a plain
  Unity object ref; `Show` after destroy is already impossible (the widget is the
  event target). `EditorApplication.delayCall`'s `if (this == null) return` guard
  at `:459` is untouched and still covers the OnValidate path.

### Lens 2 — Optimization

- **Cadence.** `Show` fires on hover-enter, not per frame. Moving two field reads
  (`renderMode`, `worldCamera`) from Awake to Show is free at that cadence, and
  the `GetComponentInParent` walk the original comment was avoiding stays
  avoided — `_hostCanvas` is still cached once.
- **Allocation.** Zero per invocation. No closures, no coroutines, no boxing
  introduced. Deleting two Canvas + two CanvasScaler + two GraphicRaycaster
  components removes two canvas batches from the UGUI update loop, so this is net
  negative cost, not neutral.
- **Cache/style recomputation.** None — no USS, no layout invalidation, no
  `Label.text`.
- **Not over-optimizing.** Explicitly declining to add a dirty-flag or an event
  for camera rebinding. Two field reads per hover is the correct amount of
  machinery; anything more would be speculative.

### Lens 3 — 1.0-shape survival

- **Does the shape survive?** Yes. "The tooltip is authored under the canvas it
  renders on" is the terminal state — there is no follow-on slice that reshapes
  it. The reparent was the transitional thing, and it dies here.
- **Downstream subscribers.** Nothing subscribes to canvas identity; the gate
  consumes *names*, which is why the allow-list is the durable artifact rather
  than any fileID. No payload struct in play.
- **Stopgap visuals.** None approved. No placeholder, no temp USS class, no
  affordance that a later slice rips out.
- **Signature churn risk.** The one signature change is
  `BuffTooltipWidget`'s private cache fields — private, single-consumer, no
  external surface. `Show`/`ShowKeyed`/`Hide` are unchanged.
- **Risk I am accepting, stated plainly:** the gate is name-anchored, so renaming
  an allow-listed canvas *and* its allow-list entry in one commit passes. That is
  deliberate — a fileID anchor would break on every legitimate re-author, which
  is precisely how the CardHand row died. Name-anchoring plus negative test 4
  (delete an entry → must fire) is the right trade. It is also the reason the
  allow-list cap check in §4.2 matters: the gate cannot stop a rename, so it must
  at least make *growth* loud.
- **Edge behavior deferred cleanly?** Yes — D5 defers Popups sibling ordering to
  whenever a second UGUI popup lands there, and that slice will have to derive it
  from scratch anyway. No bad default is locked in: "tooltip is the last child"
  is authored into the asset, so the default is the current behavior.

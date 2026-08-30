# Capture — repair moves into the garage (§9 step 4)

**Date:** 2026-08-30
**System:** `ChopshopWorkbenchController` repair mode + vehicle visibility + the
Chopshop entry rail
**Status:** AWAITING USER APPROVAL — nothing edited yet
**Build order:** step 4 of
`production/polish-captures/2026-08-29-chopshop-bay-garage.md` §9
**TD verdict:** ACCEPT-WITH-CONDITIONS — pasted in full below

---

## 1. What this slice does

Three director asks from the 2026-08-30 playtest, which turn out to be one
change:

1. The vehicle must NOT be visible on the chopshop entry screen.
2. The vehicle must be re-centred — it currently reads bottom-left-of-centre.
3. A Repair control moves off the entry rail into the garage.

Ask 1 **forces** ask 3. Welding hit-tests the world-space vehicle sprite
(`SubscribeRepairHover` → `VehicleVisual.CollectHitZones`), so leaving
`choice-1` on the entry rail would open a weld mode with an invisible target.

The end state: the chopshop entry is pure vendor and dialogue; the garage is the
single surface where the vehicle is seen and worked on.

## 2. Director decisions

### 2a. Vendor panel is HIDDEN while the garage is open

Asked 2026-08-30. The TD recommended *visible but inert*; the director chose
**hidden**. Recorded as final, with the costs the TD named:

- **The garage loses the scrap / fuel readout.** Step 6 (buy/sell) will have to
  build its own, since it is a window over the entry screen and will want the
  wallet on screen. Logged as a step-6 cost, accepted.
- **"Middle of the screen" needs restating.** See §2b.

**One thing the hidden option makes simpler, which the TD's recommendation did
not get for free.** TD condition C1 was "the entry rail must stop being
interactive while the garage is open" — because today it is fully live
underneath (Finding A). Hiding `#dialogue-panel` outright satisfies C1 by
construction: no `.is-inert` class, no `SetEnabled(false)` on
`#dialogue-panel-content`, no separate guard against the Map/Deck peek buttons
staying clickable during a weld (TD Finding J becomes moot). **C1 collapses into
the visibility change.** That is a real reduction in moving parts.

### 2b. Where "the middle of the screen" actually is

With the vendor panel hidden, the only remaining occluder on the horizontal axis
is the garage's own systems panel at `width: 34%` (left). The free gap becomes
x ∈ [0.34, 1.0].

**Screen centre (0.5) sits inside that gap.** So world x ≈ **0** satisfies the
director's literal words AND clears the panel — provided the chassis is narrow
enough. Run camera is orthographic, size 5 → world width 17.78 at 16:9, so the
clearance budget is:

```
half-width < (0.50 - 0.34) x 17.78 = 2.84 world units
i.e. total chassis width < 5.69 world units
```

Vertically the gap runs from under the floating title to above the 168px storage
band, so its centre sits slightly above true centre → world y ≈ **+0.5**.

**Starting value `(0, +0.5, 0)`. This is a starting value, not the shipped one.**
Nobody can compute the final number in review because the chassis pivot is not
its visual centre — it is tuned against the live screen and then baked back into
`CombatPrefabAuthor.cs` (§5 step 9).

`_restScale` is currently **1.0**, inherited from `PlayerVehicle.prefab` with no
ChopshopRoot override — 25% larger than combat's authored 0.8. If the chassis
overflows the 5.69 budget, scale comes down, **as a ChopshopRoot override**,
never as an edit to `PlayerVehicle.prefab` (shared with the Combat scene).

## 2c. AMENDMENT 2026-08-30 — what the garage must hide is TWO things, not one

Playtest (user image #3) shows two surfaces bleeding over the open garage. The
capture originally named only the dialogue panel.

**i. `#header-actions` — the Map / Deck peek buttons.** Authored in
`ChopshopScreen.uxml:54-62`, inside `#dialogue-panel`. Not new: the garage's
opaque header bar covered them until the 2026-08-30 layout pass removed that
background at the director's request. Hiding `#dialogue-panel` takes them with
it, so this is already covered by §2a — but it is visible until that lands, and
it is the concrete form of TD Finding J.

**ii. The RunHUD — fuel gauge and scrap counter.** `RunHUD.uxml` is a
**separate UIDocument with its own sorting order**, so it paints above the
chopshop AND the garage. That is why it renders on top of the opaque systems
panel rather than behind it. `#modal-container` picking modes are irrelevant
here — this is a different panel, not a sibling element.

**Evidence it is stale, not merely mis-ordered:** the HUD reads `35/35` fuel
while the chopshop resource strip reads `15 L`. A HUD that had hidden and
re-shown correctly would agree with the model. `35/35` is a full tank — boot
values never updated.

**Hypothesis, NOT confirmed.** `RunHUDController` hides its root in
`HandleBeaconChanged` on the same `mapIsCurrent` predicate the map uses
(`current == null || Start || IsResolved`), and a Chopshop being visited is
unresolved, so it should hide. At `RunHUDController.cs:247` the subscribe path
early-returns when `_host` is null, commented *"scene not loaded yet; OnEnable
will retry"* — but `OnEnable` fires once for an object that never deactivates.
Lose that race and the HUD never subscribes, never hides, and keeps boot-time
text for the whole run. Fits the symptoms including the intermittency (the
2026-08-30 image #1 garage had no leak). **What would confirm it:** whether the
leak correlates with resuming a save versus starting a fresh run.

**Ruling for this slice:** the garage hides the RunHUD **explicitly on open and
restores it on close**, rather than trusting a run-scope predicate to have
already done it. Reasons: (a) it is correct regardless of which hypothesis is
right; (b) the garage is a fullscreen surface and owning its own occlusion is
the same discipline the dialogue-panel hide follows; (c) it does not require
diagnosing a race in a run-scope component from inside a beacon-scope one.

**Do NOT "fix" the predicate as part of this slice.** If the race is real it is
a run-scope defect affecting every PrefabRoot beacon — Rest, Merchant, Event —
not just the Chopshop, and it deserves its own slice with its own repro. Log it;
do not widen this one. Add T8: opening the garage hides the RunHUD root, closing
it restores it.

## 3. Authored values being destroyed

Required by the capture-before-destroy protocol. Every value enumerated here was
read from the file, not recalled.

### 3a. Prefab override — `ChopshopRoot.prefab:5060-5066`

```yaml
- target: {fileID: 2223125358721315140, guid: cfa4ef50e76f4ef48b8bf1d6ba40c4e8, type: 3}
  propertyPath: _restLocalPosition.x
  value: -2
- target: {fileID: 2223125358721315140, guid: cfa4ef50e76f4ef48b8bf1d6ba40c4e8, type: 3}
  propertyPath: _restLocalPosition.y
  value: -2.5
```

**Who authored `-2 / -2.5` and why:** to park the chassis clear of the
right-hand dialogue panel on the ENTRY screen (2026-08-01 workbench shape, Shape
C). The vehicle no longer appears on the entry screen, so nothing pins it any
more. Grep-verified: `_restLocalPosition` has exactly one consumer
(`VehicleRestPose.Show`), and `VehicleRestPose` has exactly one production
consumer (`ChopshopWorkbenchController`).

Mirror value in source: `Assets/Editor/CombatPrefabAuthor.cs:8998` —
`restPoseSo.FindProperty("_restLocalPosition").vector3Value = new Vector3(-2f, -2.5f, 0f);`

`_restScale`: effective **1.0**, inherited, no ChopshopRoot override today.

### 3b. Entry rail — `ChopshopScreen.uxml:108-117`

Deleted outright (TD Ruling 5 — a disabled repair affordance left on the rail is
the vestigial pattern ADR-0011 forbids):

```xml
<!-- Choice 1: Repair (live this slice — welding
     cursor + hover-drain + budget bar, recovered
     verbatim from git commit b26fc77 pre-strip). -->
<ui:Button name="choice-1"
           class="wr-chopshop-choice wr-chopshop-choice--repair"
           picking-mode="Position">
    <ui:Label name="choice-1-label"
              class="wr-chopshop-choice-label"
              text="1. Weld armor and parts back together." />
</ui:Button>
```

Remaining four renumber. New rail:

| Old | New |
|---|---|
| ~~1. Weld armor and parts back together.~~ | *(deleted)* |
| 2. Forge a new card.  (soon) | **1.** Forge a new card.  (soon) |
| 3. Upgrade a weapon.  (soon) | **2.** Upgrade a weapon.  (soon) |
| 4. Look over the vehicle. | **3.** Look over the vehicle. |
| 5. Get back on the road. | **4.** Get back on the road. |

**Renumber risk, stated out loud:** this assumes Forge and Upgrade actually
ship. If either is cut we renumber again. Cheap, accepted.

### 3c. Controller consts — `ChopshopWorkbenchController.cs:98-102`

```csharp
private const string ChoiceRepairEntryLabel     = "1. Weld armor and parts back together.";
private const string ChoiceRepairActiveLabel    = "1. Welding...";
private const string ChoiceRepairDepletedLabel  = "1. Welding budget depleted.";
private const string ChoiceLeaveEntryLabel      = "5. Get back on the road.";
private const string ChoiceLeaveBackLabel       = "5. Back.";
```

`ChoiceLeaveBackLabel` is deleted outright — the two-mode Leave button retires.
The three repair labels survive as garage-toggle copy with the "1. " prefix
dropped. `ChoiceLeaveEntryLabel` renumbers to "4. ".

### 3d. Dead branch — `ChopshopWorkbenchController.cs:596-601`

```csharp
if (_inRepair)
{
    ExitRepairMode();
    ShowDialogueEntry();
    return;
}
```

Unreachable once the rail is hidden while the garage is open. ADR-0011 #6.

### 3e. Test assertions

Every deletion from `ChopshopRepairMode_Test.cs` (committed `22d15a6`) is
enumerated with its rationale in TD Ruling 5 below. **Net: zero assertions
carrying a live invariant are deleted.** Everything removed is label-swap copy
for a mechanism that stops existing.

### 3f. Retired prose

The 2026-08-01 "vehicle composed BESIDE the right dialogue panel (locked
constraint, dialogue stays visible during repair)" is duplicated in six places.
One fact in six copies is itself the ADR-0011 #2 defect. After this slice the
rationale lives **only** here; the other five point at it. A surviving
"locked constraint" comment is the transitional-comment pattern ADR-0011 #7
forbids.

## 4. Verified independently (not taken on the TD's word)

| Claim | Verified |
|---|---|
| `_restLocalPosition` `-2` / `-2.5` override | ✅ `ChopshopRoot.prefab:5060-5066` |
| `choice-1` copy + block | ✅ `ChopshopScreen.uxml:108-117` |
| `#budget-bar` paints UNDER the garage panels | ✅ line 172 vs `#modal-container` line 194 |
| Entry rail live under the open garage | ✅ `#dialogue-panel` line 41, `position:absolute; right:0`; visible in the 2026-08-30 playtest screenshot |
| RunScene `m_IsActive` override would be orphaned by a re-author | ✅ `RunScene.unity:659-666` — `propertyPath: m_IsActive` / `value: 0` against the ChopshopRoot GUID |
| `VehicleRestPose` has one production consumer | ✅ grep: `ChopshopWorkbenchController` only |
| No uncommitted prefab edits | ✅ `git status` clean except the two garage UI files |

**A note on the last row.** The drift sentinel lists `Combat`, `PlayerVehicle`,
`MainBar`, `HudAnchors`, all flagged in one batch at 2026-08-30T19:00:19.
`git status` is clean and `PlayerVehicle.prefab` has not been touched since
2026-07-04. That rules out *uncommitted* edits; it does not rule out an old
Prefab Mode tweak committed without baking. **It does not block this slice** —
the plan touches none of those three prefabs and uses surgical YAML on
ChopshopRoot rather than a re-author.

## 5. Implementation order

Adapted from the TD's, with step 0 resolved and C1 folded into the hide.

0. ~~Director answers the dialogue-panel question~~ — **DONE: hidden (§2a).**
1. `VehicleRestPose` — apply the endpoint immediately when
   `_lerpDurationSec <= 0` instead of deferring to `Update`; add **T7**.
   Closes the one-frame flash on `SetActive(true)` → `Show()`.
2. *(SHOULD)* Extract `ChopshopVehicleStage` — pure move, no behaviour change.
3. `GarageScreen.uxml` / `.uss` — `#btn-garage-repair` above Back in
   `#garage-nav` (the `column-reverse` was authored for this).
   `GarageBinding` gains `SetRepairHandler` + narrow label/enabled setters —
   **do not expose the `Button`.** Add **T5**.
4. `ChopshopScreen.uxml` / `.uss` — delete `choice-1`, renumber, move
   `#budget-bar` to sit after `#modal-container`. Picking modes UNCHANGED.
5. `ChopshopWorkbenchController` — `EnterGarage` / `ExitGarage`; hide
   `#dialogue-panel` on garage open; vehicle `SetActive` + `RestPose.Show/Hide`
   + `BindForWorkbench` / `UnbindWorkbench` moved out of `ShowDialogueEntry`;
   repair rehomed; `_inRepair` branch and `ChoiceLeaveBackLabel` deleted; all
   three repair-exit paths return to GARAGE state not entry state;
   garage rebuild on repair exit; `UnsubscribeAllHover` in `OnDisable`;
   budget-bar clamps; occlusion guard.
6. Rewrite `ChopshopRepairMode_Test` per Ruling 5; add **T1–T4**.
   **Same commit as 5** — the invariants are never uncovered.
7. Prefab — surgical YAML on the two `_restLocalPosition` scalars (+ a
   `_restScale` entry if needed) AND `CombatPrefabAuthor.cs:8998` in the same
   commit; hoist the literal into a named constant; add **T6**.
8. Sweep the six copies of the retired locked-constraint prose.
9. Playtest — tune pose and scale against the live screen, bake back into
   `CombatPrefabAuthor.cs`, re-run T6.

## 5b. Files at risk

**Unity project — `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\`**

Edited:

| File | Change |
|---|---|
| `Assets\UI\ChopshopScreen.uxml` | delete `choice-1`, renumber, reorder `#budget-bar` after `#modal-container` |
| `Assets\UI\ChopshopScreen.uss` | retired-constraint header comment |
| `Assets\UI\GarageScreen.uxml` | `#btn-garage-repair` in `#garage-nav` |
| `Assets\UI\GarageScreen.uss` | repair-button styling, active / depleted states |
| `Assets\Scripts\UI\GarageBinding.cs` | `SetRepairHandler` + narrow label / enabled setters |
| `Assets\Scripts\CombatView\ChopshopWorkbenchController.cs` | the bulk — see §5 step 5 |
| `Assets\Scripts\CombatView\VehicleRestPose.cs` | zero-duration immediate apply |
| `Assets\Editor\CombatPrefabAuthor.cs` | line ~8998 value, hoisted constant, comment block ~8925 |
| `Assets\Prefabs\BeaconRoots\ChopshopRoot.prefab` | **surgical YAML only** — `m_Modifications` at line 5060 |

New:

| File | Purpose |
|---|---|
| `Assets\Scripts\CombatView\ChopshopVehicleStage.cs` | plain host-owned class collecting the vehicle touchpoints (`VehicleVisual` + `VehicleRestPose` + `VehicleBarStack`) behind `Show()` / `Hide()`. Lives in `CombatView` because `WastelandRun.UI` cannot reference it — see TD Ruling 4. |

Tests:

| File | Change |
|---|---|
| `Assets\Tests\PlayMode\CombatView\ChopshopRepairMode_Test.cs` | rewritten per Ruling 5, plus T1–T4, T8 |
| `Assets\Tests\EditMode\UI\GarageBinding_Test.cs` | T5 |
| `Assets\Tests\EditMode\CombatView\ChopshopRootPrefabWiring_Test.cs` | T6, and the `choice-4` comment |
| `Assets\Tests\EditMode\CombatView\VehicleRestPose_show_restoresOnHide_test.cs` | T7 |

**Explicitly NOT to be touched:**

- `Assets\Prefabs\CombatView\PlayerVehicle.prefab` — shared with the Combat
  scene; any scale change is a ChopshopRoot override, never an edit here
- `Assets\Scenes\RunScene.unity` — do NOT re-author ChopshopRoot; the
  `m_IsActive` override at `:659-666` would be orphaned
- `Assets\Scripts\Run\GarageViewModel.cs`, `WeldRepairModel`, any DTO — no model
  change, no schema bump
- `Assets\Scripts\CombatView\RunHUDController.cs` — **the racy subscribe path and
  the `mapIsCurrent` predicate are NOT touched.** §2c's suspected defect is
  run-scope and gets its own slice. This file does gain ONE additive verb,
  `SetSurfaceOccluded(bool)`, plus an `_surfaceOccluded` flag that root
  visibility is ANDed against — the garage needs some way to assert occlusion,
  and a public verb on the owner is correct where reaching into another
  component's UIDocument from the Chopshop would not be. No existing branch
  changes meaning.

**Framework repo — `C:\ClaudeCreations\Madmax Roguelike\`**

- `production\polish-captures\2026-08-30-chopshop-repair-into-garage.md` — this file
- `production\polish-captures\2026-08-29-chopshop-bay-garage.md` — §9 step 4 status
- `production\td-verdicts\2026-08-01-chopshop-workbench-shape.md` — one-line
  "constraint superseded" pointer; **do not rewrite the history**

## 6. Required new coverage

- **T1** vehicle hidden on chopshop arrival, visible after the garage opens
- **T2** entry rail not interactive while the garage is open (satisfied by the
  hide; assert it anyway — it is the two-Back-buttons guard)
- **T3** repair exit returns to the garage, not the entry
- **T4** garage list HP text refreshes after a weld
- **T5** `#btn-garage-repair` resolves in the shipping `GarageScreen.uxml`
- **T6** `_restLocalPosition` anti-drift: shipped prefab matches the authoring
  constant
- **T7** `VehicleRestPose.Show` with zero duration applies without an `Update`

**Honest limit, carried forward:** `Button.clicked` still cannot be raised from
these assemblies (no `Unity.InputSystem` reference), so the new Repair button's
DISPATCH is playtest-owned, exactly as `btn-garage-back` already is. The tests
prove what happens once a handler runs; they would pass with the button unwired.

## 7. Deliberately NOT in this slice

Install / uninstall gestures and drag (step 5) · buy/sell (step 6, still blocked
on pricing) · a repair cost / "rods" currency (parked, §2a of the garage
capture — repair stays free until designed) ·
`VehiclePartHitZone.SetTargetHover` building an outline in order to hide it
(TD Finding G — logged as debt, deliberately not fixed here).

---

## Technical Director Review

Received 2026-08-30. Verdict: **ACCEPT-WITH-CONDITIONS**. Reproduced below.
Where the director's §2a choice (vendor panel hidden) diverges from the TD's
recommendation, the divergence and its consequences are recorded in §2a/§2b
above rather than by editing the verdict text.

### Conditions

- **C1** — the entry rail must stop being interactive while the garage is open.
  It is live today (Finding A). Without this, step 4 ships two Back buttons.
  *(Satisfied by construction under the hidden option — see §2a.)*
- **C2** — the vehicle must fit INSIDE the free gap with no overlap onto any
  `Position` panel. `_restScale` becomes a hit-test constraint, not a look
  choice.
- **C3** — `ChopshopRepairMode_Test` must be rewritten in the SAME commit as
  the move, not after. The two invariants it guards (cancel-does-not-resolve,
  hover subscribe/unsubscribe symmetry) must never be uncovered for a commit.

### Ruling 1 — the locked constraint

The 2026-08-01 constraint is three claims with different owners. "Repair stays
on the entry rail" was retired by the director 2026-08-29 (capture §2a).
"Vehicle composed beside the dialogue panel" was retired by the director
2026-08-30. "Dialogue stays visible during repair" was never decided by anyone —
it goes back to the director. *(Answered: hidden.)*

### Ruling 2 — centring

Centre on the GAP, one pose, never a per-mode pose. A vehicle that slides when
you enter repair is a moving target for a hit-test-driven mechanic, and a second
authored Vector3 is parallel storage of one fact. Hiding the vehicle on entry
fully removes the constraint that forced `(-2, -2.5)`.

### Ruling 3 — hit-testing

**Do NOT flip panels to `Ignore` during repair.** It is a bimodal path
(ADR-0011 #3) on the most load-bearing property in the screen; it kills the
garage's own interactivity during repair (no scrolling, no Back, and at step 5
no install icons); and it is one more thing that can be got wrong silently.
`#modal-container` stays `Ignore`, panels stay `Position`, `#root` is NEVER
`Position` — ruling 2 of the 2026-08-30 verdict stands unamended.

Instead: fit the vehicle inside the gap (C2), and ship a cheap occlusion guard
so the failure is loud. Once per `EnterRepairMode`, project each damaged slot's
hit-zone rect centre to screen and call `panel.Pick(pos)`; non-null means a UI
element is eating that zone → `Debug.LogWarning` naming the slot. ~15 lines,
`#if UNITY_EDITOR || DEVELOPMENT_BUILD`, once per mode entry, never per frame.
Any chassis under a `Position` panel is **unweldable with no feedback
whatsoever** — the cursor is over it, the outline never lights, and the player
concludes the mechanic is broken.

### Ruling 4 — ownership is forced by the asmdefs, not chosen

`WastelandRun.UI` does not reference `CombatView`; `CombatView` references
`UI`. `GarageBinding` is in `UI`; `VehicleRestPose` and `VehicleBarStack` are in
`CombatView`. Giving `GarageBinding` the vehicle needs a `UI → CombatView`
reference, which Unity rejects as circular. **The controller owns vehicle
visibility.** `GarageBinding` gets `SetRepairHandler(Action)` — the same shape
as the `SetBackHandler(Action)` it already has.

**"Hidden" must be `SetActive(false)` on the `PlayerVehicle` root, NOT
`RestPose.Hide()`.** `Hide()` lerps back to the captured original, which is
`localPosition (0,0,0)` — dead centre and fully visible. `VehicleBarStack` sits
under the vehicle root, so sprites, bars, rings and hit zones all go dark with
one switch. Verified safe: `VehiclePartHitZone.OnEnable/OnDisable` are
`#if UNITY_EDITOR` and early-out on `Application.isPlaying`; `VehicleBarStack`
has no enable/disable lifecycle; child `activeSelf` flags survive a parent
toggle.

`VehicleRestPose.Hide()` and `VehicleBarStack.UnbindWorkbench()` both have zero
production callers today. Wire both on garage close — do not delete them; this
slice is what they were written for.

**SHOULD: extract `ChopshopVehicleStage`** — a plain class in `CombatView` owned
by the controller the way `GarageBinding` and `PeekOverlayBinding` are. The
controller is ~900 lines already carrying repair drain, cursor swap, sparks,
peek, garage and beacon resolution. Trigger if declined now: extract before step
5, which adds a sixth vehicle touchpoint.

### Ruling 5 — the two-mode Leave button is dead; test disposition

`choice-1` deleted, rail renumbered. The `_inRepair` branch in `OnLeaveClicked`
and `ChoiceLeaveBackLabel` are dead and must be deleted.

**The garage Repair button becomes the toggle** (label carries the mode).
**Garage Back keeps exactly one meaning and is `SetEnabled(false)` during
repair.** Right-click cancel stays. Why not a two-mode Back: steps 5 and 6 add
an install flow and a buy/sell window; overloading Back now compounds at every
future mode. A single-meaning Back is what makes a modal stack legible.

**Critical corollary:** every repair exit path must return to GARAGE state.
Today `TickRepair`'s `ShouldExitRepair`, the right-click branch in `Update`, and
`OnLeaveClicked` all call `ShowDialogueEntry()`, which calls `_garage.Hide()` —
after the move that would slam the garage shut when the budget runs dry. All
three become `ExitRepairMode(); RefreshGarage();`.

Assertion-by-assertion disposition:

- `RepairChoice_IsEnabledAndLabelled_...` — REWRITE onto `#btn-garage-repair`.
  Drop the `choice-5-label` assertion. Keep the budget-bar-hidden assertion.
- `RepairChoice_IsDisabled_...` — REWRITE onto the garage button; invariant
  unchanged and load-bearing.
- `EnteringRepair_ShowsBudgetBar_SwapsBothLabels_AndClosesTheGarage` — REWRITE,
  rename to `...KeepsTheGarageOpen`. **INVERT** the `modal-container` assertion.
  Delete the `choice-5-label` and `choice-1`-disabled assertions. Rewrite
  `choice-4`-disabled as "garage Back disabled during repair".
- `EnteringRepair_SubscribesHoverOnEveryDamagedSlotWithAHitZone` — REWRITE
  minimally (enter via the garage). **The count assertion of 2 survives
  verbatim.** Highest-value test in the file.
- `BackDuringRepair_...` — REWRITE, rename to `CancellingRepair_ReturnsToTheGarage_...`.
  **Keep verbatim:** the `activeSelf` beacon-not-resolved assertion, the
  budget-bar-hidden assertion, and `ReadHoverSubCount() == 0`. The branch those
  guarded is being deleted, but the INVARIANTS are still live and now guarded by
  different code — deleting them because the implementation moved would be the
  wrong read. Add: `modal-container` still not hidden after cancel.

### Ruling 6 — surgical YAML, never re-author

Edit the two `value:` scalars, plus a `_restScale` entry if the scale changes.
Update `CombatPrefabAuthor.cs:8998` in the SAME commit.

**Why re-authoring is rejected, concretely:** `AuthorChopshopRootPrefab` builds
a fresh `GameObject` and calls `SaveAsPrefabAsset`, regenerating every local
fileID. `RunScene.unity` carries a `PrefabInstance` for ChopshopRoot whose
`m_Modifications` include **`m_IsActive: 0`**. Regenerating orphans those
overrides and ChopshopRoot boots ACTIVE in RunScene — the chopshop screen
renders over the run map from frame one. `BeaconActivator`'s defensive
`SetActive(false)` sweep masks this at runtime, which makes it worse, not
better: the corruption survives in the scene asset unnoticed. This is
`feedback_nested_prefab_topology_orphans_overrides` with a named victim.

**Anti-drift lock:** hoist the literal at `CombatPrefabAuthor.cs:8998` into a
named constant and assert the shipped prefab matches it (T6). Without this the
surgical YAML and the author routine are two copies of one fact that can
silently diverge — the defect ADR-0011 #2 exists to stop.

### Ruling 7 — required coverage

T1–T7 as listed in §6 above.

**Gate:** this ACCEPT is conditional on an explicit EditMode-green attestation
with counts (baseline 1244 / 1242 passed / 0 failed / 2 skipped) PLUS the
PlayMode chopshop suite green, pasted in the message that claims it.
Compilation green is not semantic green, and this slice rewrites the only
behaviour lock on the Chopshop's only mechanic.

### Findings not asked about

- **A. The entry rail is LIVE under the garage today.** Ships in the read-only
  slice. The 2026-08-30 verdict's claim that the garage "covers the dialogue
  panel" was carried forward from aspiration and was never true of the shipped
  UXML. *(This is C1, and the hidden option resolves it.)*
- **B. `#budget-bar` paints UNDER the garage panels** — it is a sibling sitting
  before `#modal-container` in document order. Move it after. One-line.
- **C. The budget bar can leave the screen** —
  `barY = panelPos.y - BudgetBarHeight - BarOffsetY` goes negative near the top,
  and the re-centred vehicle sits higher than the old pose. Add two
  `Mathf.Clamp` calls.
- **D. The garage list will show stale HP after a weld.** `GarageBinding.Show`
  rebuilds from the snapshot taken at open time. **Do not rebuild per drain
  tick** — that is N element allocations at 60fps for a value that changes ~3
  times a second. Rebuild on repair EXIT; live feedback already rides the
  polling `VehicleBarStack`.
- **E. `BindForWorkbench` is called on every `ShowDialogueEntry`** with
  `UnbindWorkbench` never called. Not a leak, but a meaningless cadence. The
  `EnterGarage`/`ExitGarage` pair makes it one-shot.
- **F. `OnDisable` does not call `UnsubscribeAllHover`** — only `ExitRepairMode`
  and `OnDestroy` do. With the vehicle now `SetActive`-toggled underneath a mode
  holding closures over its hit zones, add it. Two lines.
- **G. `VehiclePartHitZone.SetTargetHover(false)` calls
  `EnsureTargetHoverOutline()` unconditionally** — hiding an outline that was
  never shown BUILDS it. Debt; do not fix in this slice.
- **H. Stale memory.** `project_garage_is_chopshop_modal` says the garage should
  be a MonoBehaviour named `GarageViewController`. The shipped implementation is
  `GarageBinding`, a plain helper, per the deliberate 2026-08-30 amendment;
  `GarageViewController.cs` does not exist. Correct the memory.
- **I. Step 6 tripwire.** Buy/sell is a THIRD surface with a third free gap and
  its own vehicle-visibility question. `ChopshopVehicleStage` is what keeps that
  from becoming a fourth authored Vector3.
- **J. If the vendor panel stays visible, disable `#dialogue-panel-content`**,
  not just the choice column — `#header-actions` carries the Map/Deck peek
  buttons. *(Moot under the hidden option.)*

### Three-lens self-audit

**Codebase health.** The slice REMOVES three drift sites (dead `_inRepair`
branch, vestigial `choice-1`, six-way duplicated prose) and gives two dead API
halves their first callers. Drift introduced: `GarageBinding` gains a second
`Action` setter. At two this is duplication, not a pattern — **at the third
(step 5's install/uninstall) collapse to a single `readonly struct
GarageHandlers`.** Subscription lifecycle: `#btn-garage-repair` is
locally-queried, so its handler pairs with `Bind`/`Unbind` from
`OnEnable`/`OnDisable` — not Bind/OnDestroy, which is the external-publisher
pattern and would leak across the deactivated window. Honest weak point: the
controller is already carrying six view responsibilities and this adds a
seventh; the asmdef forbids moving it to `UI`. `ChopshopVehicleStage` is the
mitigation, recommended rather than deferred.

**Optimization.** New per-frame work is **zero**. `UpdateBudgetBarPosition`
already ran per frame; two clamps are free and it still writes only
`style.translate` (transform-only repaint). Vehicle `SetActive` fires on a
click. The occlusion guard fires once per repair entry, dev builds only. The one
real trap is D — per-tick garage rebuild is explicitly forbidden. Not asking for
a diffing refresh in `GarageBinding.Show`: a garage open is a click, and a stale
row surviving a diff is the worse failure.

**1.0-shape survival.** `#garage-nav` `column-reverse` with Repair above Back is
the 1.0 stack; buy/sell adds siblings without touching it.
`SetRepairHandler(Action)` is correctly parameterless — no data crosses that
seam now or at 1.0. The single-pose decision survives because the garage layout
is single. Named risks: the rail renumber assumes Forge and Upgrade ship; the
hidden-panel option costs the garage its scrap/fuel readout and step 6 must
rebuild one; buy/sell is a third surface. No throwaway scaffolding — every
element added is the shipping shape.

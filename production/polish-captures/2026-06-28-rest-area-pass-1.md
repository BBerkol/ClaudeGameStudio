# 2026-06-28 — Rest Area Pass 1 (Repair-only, three-button shell)

**Status:** Awaiting user approval before destructive edits begin.

## Context

The Option B topology pivot (also 2026-06-28) just shipped: `RestRoot.prefab`
is a SetActive-toggled prefab root under a scene-level `BeaconActivator` in
`RunScene.unity`. The current Rest UX is a 2-day-old placeholder:

- **Title** "REST POINT" + subtitle "REPAIR ONE SUBSYSTEM"
- Empty-state label "Nothing to repair." (hidden by default)
- Continue button (hidden by default, revealed after pick or on empty-list)
- Damaged-slot picks happen via `VehiclePartHitZone` clicks on the player
  vehicle — no in-picker candidate list
- Free full-slot repair on click; one pick per Rest

User locked the **final-game Rest vision** today:

> Rest is a one-pick beacon. Player chooses exactly ONE of three actions:
> **Repair** (welding-cursor + hold-LMB-to-heal, shared 20 HP budget at
> 3 HP/s, distributed across slots), **Forge Weapon Card** (rarity-rolled
> choice of 3), or **Upgrade Weapon** (rest-site rarity gated, weapons max
> 3 levels). Three buttons centered at bottom on entry; pick one and that
> mode runs; Continue appears when the mode finishes.

User-locked Pass 1 constraints:

1. **Fixed 20 HP budget** — no rarity scaling yet.
2. **Disabled buttons** for Forge + Upgrade — visible-but-inert with tooltip.
3. **One-pick beacon** — once Repair is clicked, Forge/Upgrade are out of
   reach this Rest.
4. **Continue appears when the picked mode's operation completes.**

Pass 2 (Forge) gated on weapon-card pools (not built). Pass 3 (Upgrade)
gated on weapon-upgrade system + rest-site rarity (neither built).

## Files being destructively reshaped

### `Assets/UI/RestPicker.uxml`
**Entire structure thrown out.** Current elements being destroyed:
- `#root` (`.wr-rest-root`)
- `#title` (`.wr-rest-title`) — text "REST POINT", color `#F5EB8C`
- `#subtitle` (`.wr-rest-subtitle`) — text "REPAIR ONE SUBSYSTEM", color `#C8C8B4`
- `#empty-state` (`.wr-rest-empty`) — text "Nothing to repair.", color `#9A9A8C`
- `#dismiss-button` (`.wr-rest-dismiss`) — text "CONTINUE →", bg `rgba(77,77,82,0.92)`

Pass 1 UXML will hold: three-button rail (`#repair-button`, `#forge-button`,
`#upgrade-button`) + a hidden `#continue-button` revealed at mode completion.
Title/subtitle copy: TBD per user pass; current "REST POINT" likely survives
as page title.

### `Assets/UI/RestPicker.uss`
**All rule blocks thrown out.** Pre-authored values being destroyed:
- `:root` custom-prop palette: `--wr-rest-scrim` (0.78 black scrim),
  `--wr-rest-title`, `--wr-rest-subtitle`, `--wr-rest-empty-text`,
  `--wr-rest-dismiss-bg`, `--wr-rest-dismiss-text`
- `.wr-rest-root` — absolute fullscreen, scrim bg, flex column centered
- `.wr-rest-title` — 900×70, 48px bold, color from --wr-rest-title
- `.wr-rest-subtitle` — 900×30, 18px regular, color from --wr-rest-subtitle
- `.wr-rest-empty` — 560×40, 22px, `display: none` + `.is-visible` reveal
- `.wr-rest-dismiss` — 280×64, 22px bold, radius from `--wr-radius-md`,
  `display: none` + `.is-visible` reveal

Pass 1 USS gets a fresh palette + three-button rail layout. No designer
color/spacing tuning has happened on the current values — palette is
authored from the controls token defaults, 2 days old.

### `Assets/Scripts/CombatView/RestPickerController.cs`
**Entire logic reshape + relocation.** The whole click-to-repair-slot
flow is retired:
- Auto-show on `Start` — survives, reshapes (now opens three-button rail)
- `VehiclePartHitZone.OnClicked` subscription for slot pick — retired
- `RestPickerViewModel.Commit(slotId)` call path — retired (replaced by
  budget-drain model that doesn't atomically commit a single slot)
- Empty-list short-circuit — retired (Pass 1 has no concept of
  "no damaged slots" since 20 HP is the budget, player can leave it on
  the table)
- Resume-into-resolved-Rest guard in `Start()` — survives, identical role

**Categorical-fit relocation (TD verdict):** Controller moves from
`Assets/Scripts/CombatView/` to `Assets/Scripts/UI/` under
`WastelandRun.UI.asmdef`, matching the sibling screen controllers
(`CardRewardPickerController`, `CombatOutcomeOverlayController`,
`MapViewController`, `RunCompleteViewController`). Current placement is
an ADR-0014 boundary smudge.

### `Assets/Scripts/Run/RestPickerViewModel.cs`
**Retired or reshaped to budget-drain model.** Pre-authored shape being
destroyed:
- `RestRepairCandidate` row struct (SlotId / DisplayName / Hp / MaxHp /
  DamageDelta) — retired; Pass 1 doesn't enumerate candidates
- `Candidates` projection from `Vehicle.GetDamagedSlots()` — retired
- `IsEmpty` — retired (Pass 1 has no empty path)
- `Commit(string slotId)` — retired (replaced by budget-drain)
- `Dismiss()` — likely survives in spirit as the "Continue" verb that
  calls `RunSession.ResolveRest(null)`
- `ResolveDisplayName(SlotInstance)` — retired (no per-slot row labels)

Open: does the VM survive in any form, or does the Pass 1 controller
talk directly to `RunSession` / `Vehicle`? Likely a thin VM survives
holding the 20 HP budget state + per-slot HP getter for the bar paint.

### `Assets/Prefabs/BeaconRoots/RestRoot.prefab`
Gains a worldspace `ParticleSystem` child for welding sparks (per TD Q3).
The PlayerVehicle composition + `BeaconSceneBootstrap` root + RestPicker
UIDocument structure all survive. Welding-tool cursor texture asset ref
likely lands on the controller serialize fields.

## Technical Director Review

TD-ARCHITECTURE: APPROVE WITH CONCERNS

**Pass 1 Rest scope — locked architecture, with five constraints attached.**

**Q1. Three-button shell vs. one-button-now.** Three-button shell. ADR-0011
#6 ("stub returns") targets *code* that lies about behavior; a disabled UI
button with a "Coming soon" tooltip is honest about its state — that is the
UX pattern, not the smell. Two reshapes of the bottom rail in 8 weeks is the
larger risk. **Constraint:** the disabled buttons must read
`SetEnabled(false)` + tooltip; do *not* wire `clicked` handlers that throw
or no-op (that would be the ADR-0011 violation).

**Q2. "Picked-action" state location.** Transient on the controller (option
a). The pick is a within-screen modal state, not a model fact —
`RunSession.ResolveRest` is the only model-relevant write, and that already
fires once at Continue. A `RestAction` field on `RunState` would invite the
Q4 persistence question we explicitly reject below. View-only state stays
view-only.

**Q3. Welding sparks rendering.** Worldspace `ParticleSystem` on a child of
`RestRoot.prefab`, position-driven each frame from
`Camera.main.ScreenToWorldPoint(Input.mousePosition)`. ADR-0014 hybrid line
is clean: UI Toolkit owns the three-button shell + Continue + HUD bars; the
vehicle and its FX are already worldspace (VehicleVisual, WheelDustEmission,
DeathCascadeController prior art). Sparks are conceptually a worldspace
effect happening *at* the vehicle, not a UI overlay. **Constraint:** the
spark prefab is parented under `RestRoot`, not under a UI VisualElement;
no `UIElements`-painted particles.

**Q4. 20 HP budget persistence.** No persistence. In-flight budget lives on
the controller; close-app mid-Repair = next launch reopens the Rest screen
with budget full. Roguelike commit-on-continue convention, no new `RunState`
field, no ADR-0004 schema bump for an EA Pass 1 mechanic. The user's
intuition here is correct. **Save fires only on `RunSession.ResolveRest` at
Continue**, same as today.

**Q5. Forward-compat for Pass 2/3.** Bake a minimal seam, no more. Ship
`enum RestAction { Repair }` (single-valued today) + a single
`IRestActionMode` interface (`Enter()`, `Exit()`, `event Action Completed`)
implemented by a `RepairMode` class on the controller. Three-button click
handlers route to a mode-by-enum lookup. Pass 2 adds `Forge` to the enum +
a `ForgeMode` sibling; the shell rail does not move. This is ADR-0015-shaped
(data/composition narrowing the action space) not ADR-0011 stub territory —
the interface has one real implementation, not zero. **Do not** pre-author
`ForgeMode`/`UpgradeMode` empty classes; those land with their passes.

**Q6. Capture-before-destroy footprint.** Light. Capture must list: (a)
`RestPicker.uxml` element IDs + class names being deleted (`#title`,
`#subtitle`, `#empty-state`, `#dismiss-button`, all `wr-rest-*` classes),
(b) `RestPicker.uss` rule blocks being thrown out, (c) the
`RestPickerViewModel.Commit(string slotId)` / `Dismiss()` shape being
retired (Pass 1 shifts to a budget-drain model — VM either dies or
reshapes), (d) the per-hit-zone subscription closures and
`RestRepairCandidate` row struct. Two days of placeholder, no designer
color/spacing tuning yet — under 200 lines of capture.

**Categorical mismatch I want flagged in the capture:** `RestPickerController.cs`
currently lives at `Assets/Scripts/CombatView/RestPickerController.cs`, but
it consumes `RestPicker.uxml` and binds against `RestPickerViewModel` — it's
a UI screen controller, not a combat-view component. `WastelandRun.UI.asmdef`
already hosts the sibling screen controllers (`CardRewardPickerController`,
`CombatOutcomeOverlayController`, `MapViewController`,
`RunCompleteViewController`). Pass 1's reshape is the right window to
relocate it to `Assets/Scripts/UI/RestPickerController.cs` under the
`WastelandRun.UI` asmdef. The current placement is the only ADR-0014
boundary smudge in the brief.

**Risk register additions:**
- **R-Pass2-shell:** If Forge UX needs more than three buttons at the bottom
  edge (e.g. a card-row carousel), the three-button rail will reshape.
  Acceptable — that's a Pass 2 design call, not a Pass 1 architecture
  failure.
- **R-cursor-leak:** `Cursor.SetCursor(weldingTex, ...)` must reset on
  `OnDisable` AND `OnDestroy` AND mode-exit. Cursor state is global Unity
  state; a leak across beacon teardown will paint the map screen with a
  welding cursor. Add a TearDown test if practical.

**Success criteria** ("we'll know this was right if"):
- Pass 2 lands and the three-button rail does not move pixels.
- `RestAction` enum + `IRestActionMode` seam absorbs `ForgeMode` without
  touching `RepairMode`.
- Zero `RunState` schema bumps for Pass 1.
- The controller relocation to `WastelandRun.UI` does not require any
  `using WastelandRun.CombatView` imports (it should already only depend
  on `WastelandRun.Run` + `UnityEngine.UIElements` + the hit-zone interface).

Files at risk:
- `Assets/Scripts/CombatView/RestPickerController.cs` (relocate + reshape)
- `Assets/Scripts/Run/RestPickerViewModel.cs` (retire or reshape)
- `Assets/Scripts/Run/RunSession.cs` (`ResolveRest` API reshape — see Amendment §3)
- `Assets/UI/RestPicker.uxml`
- `Assets/UI/RestPicker.uss`
- `Assets/Prefabs/BeaconRoots/RestRoot.prefab` (gains ParticleSystem child
  + welding cursor texture ref)

## Amendment — 2026-06-28 post-capture re-check

User-driven re-check after the initial capture caught five material gaps
plus one new user spec. Locked decisions inline; pending TD one final
health/optimization pass on the amended picture.

### A1. Surviving CombatView deps are load-bearing (capture was silent)

The following CombatView types **survive Pass 1 unchanged** and are
load-bearing for the Repair UX — capture must declare them, not only
list what dies:

- `VehicleBarStack.BindForRest(targetGetter, visual)` — live HP bar paint
  over parked vehicle for Frame + all subsystem slots. Refreshes as
  `Vehicle.RepairSlot` mutates per drain tick. Without this the player
  has no visual feedback that draining is working.
- `VehicleRestPose.Show()` — pose snap on Repair-mode enter.
- `VehicleVisual.CollectHitZones(slotId, scratch)` — used to wire hover
  targets per damaged slot.
- `BeaconSceneBootstrap.Host.Session` — resolves `RunSession` (no change).

Consequence: `RestPickerController.cs` **stays in
`Assets/Scripts/CombatView/`** (not relocated to `Scripts/UI/`). TD's
relocation success criterion assumed bars + rest pose moved elsewhere;
they don't. Sibling UI controllers
(`CardRewardPickerController` / `CombatOutcomeOverlayController` /
`MapViewController` / `RunCompleteViewController`) are confirmed
pure-UI (zero `WastelandRun.CombatView` imports) — Rest is categorically
different because the screen IS the vehicle. The ADR-0014 smudge TD
flagged is real but the fix isn't this slice.

### A2. Continue-reveal timing — locked: budget-to-zero (option c)

User decision: **Continue button appears only when the 20 HP budget
reaches 0.** No "drain 0 and continue" escape hatch. The player commits
to spending the full budget when they pick Repair.

New spec attached: a **vertical budget bar** rendered attached to the
welding-tool cursor. Drains from full → empty as the budget spends.
When the bar hits 0, Continue reveals.

**Rendering decision (TD: confirm).** UI Toolkit `VisualElement` with
`PickingMode.Ignore`, absolute-positioned and updated each frame from
`Mouse.current.position.ReadValue()` (new Input System per
`technical-preferences.md`). Sits in the picker UIDocument tree above
the three-button rail layer. Cursor texture itself stays a static
welding icon via `Cursor.SetCursor`; the bar is a **separate**
follow-mouse element, not baked into the cursor texture (cursor
textures are immutable; dynamic content has to live elsewhere).

Forge / Upgrade buttons are still disabled in Pass 1 — they don't reach
the budget-bar path.

### A3. `RunSession.ResolveRest(string slotId)` — drop the param

Current `RunSession.cs:204` applies an *atomic full-slot heal* at
commit time (`player.RepairSlot(slotId, slot.MaxHp - slot.Hp)`) and
throws on `null` when damaged slots remain. Pass 1's drain model fires
`Vehicle.RepairSlot` *per tick during the mode*, so at Continue no
further repair fires.

**Reshape:** drop the `slotId` parameter entirely. Body collapses to:
```csharp
public void ResolveRest()
{
    if (_inFlight != null) throw …;
    BeaconData current = _controller.Current;
    if (current.Type != BeaconType.Rest) throw …;
    if (current.IsResolved) throw …;
    OnRestModelCommitted?.Invoke();
    current.MarkResolved();
}
```
This is the bigger ADR-0011 cleanup — no `slotId == null` dismiss
branch, no "damaged-slot projection contract" tied to the verb, no
forced full-heal-at-commit semantics. Consumers (controller +
`RunSceneOverlayHost` xmldoc) update accordingly.

### A4. `RestRepairCandidate` partially survives — capture said fully retired

Pass 1 still needs damaged-slot enumeration to subscribe the right hit
zones (`VehicleVisual.CollectHitZones(slotId, scratch)` requires the
slotId upfront). Two options:

- **Slim the struct:** drop `DisplayName` / `MaxHp` / `DamageDelta` (no
  row UI), keep `SlotId` + `Hp`. Then drop the VM entirely (controller
  reads `Vehicle.GetDamagedSlots()` directly + tracks budget locally).
- **Drop the struct entirely:** controller works directly off
  `IReadOnlyList<SlotInstance>` from `Vehicle.GetDamagedSlots()`. No
  intermediate POCO.

**Decision:** drop the struct entirely. Controller reads
`GetDamagedSlots()` directly. `RestPickerViewModel` retires (no
budget-state value the model would persist; budget is transient
controller state per TD Q2/Q4).

### A5. `VehiclePartHitZone.OnHover` newly subscribed (capture noted OnClicked retiring, not OnHover wiring)

`VehiclePartHitZone` already publishes `event Action<bool, Vector2>
OnHover` (`VehiclePartHitZone.cs:158`). Pass 1 reshape on the
subscription side:

- **Dies:** `OnClicked → Commit(slot)` subscription (capture noted)
- **New wire:** `OnHover(entered, _) → trackHoveredSlot` per damaged
  hit zone. Controller per-frame loop: if
  `Mouse.current.leftButton.isPressed && _hoveredSlot != null` and the
  slot is still in `GetDamagedSlots()` and budget > 0, call
  `Vehicle.RepairSlot(_hoveredSlot, 1)` throttled to 3 HP/s (one tick
  per ~333 ms accumulator).

Per-tick HP cost is decremented from the controller-local 20 HP
budget. When the slot reaches MaxHp it drops from the damaged set;
hover continues but drain no-ops on that slot. Player must redirect
hover to a still-damaged slot to keep spending. Budget reaches 0 →
Continue reveals.

### A6. Welding cursor + bar — implementation notes

- Welding cursor texture: serialize as `Texture2D` field on the
  controller. Asset path target: `Assets/Art/UI/Cursors/welding.png`
  (does not yet exist — author with placeholder per
  `feedback_placeholder_squares_for_enemy_iteration` if no art exists).
- Cursor hot-spot: tool tip position in the texture (Vector2 serialized
  field, default `(0, 0)`).
- Cursor swap fires on Repair-mode `Enter()`; reset on `Exit()` AND
  `OnDisable` AND `OnDestroy` (R-cursor-leak risk register entry from
  initial TD verdict).
- Budget bar element: child of the picker UIDocument root, USS class
  `wr-rest-budget-bar`, absolute-positioned, `PickingMode.Ignore`,
  bound to a `wr-rest-budget-bar__fill` inner VisualElement whose
  `style.height` scales with `budget / 20f`.

### Non-material (note only)

- UI Toolkit `.tooltip` shows in editor only at runtime — Forge /
  Upgrade disabled-button tooltips will not render to the player in
  Pass 1. Acceptable for placeholder pass; runtime tooltip widget is a
  future polish task.
- `OnRestModelCommitted` xmldoc on `RunSession.cs:64` references
  "vehicle mutation (or empty-list dismiss)" — text updates to reflect
  the new shape (mutations already happened during the mode; this is
  the latch).
- `RunSceneOverlayHost.cs:27` xmldoc references the "unresolved-Rest
  map-suppression guard" — survives unchanged.

## TD Final Health + Optimization Pass — 2026-06-28

TD-MANIFEST: APPROVE WITH ONE FIX BEFORE BUILD + Pass 1.5 polish flagged.

### FIX BEFORE BUILD (1 item)

**F1. Per-frame allocs in the drain hot path.**
`Vehicle.GetDamagedSlots()` at `Vehicle.cs:199` allocates a fresh
`List<SlotInstance>` every call. The per-tick "is hovered slot still
damaged?" check would leak GC over the 20-second drain. **Fix:** controller
checks the hovered slot directly via `Vehicle.GetSlotById(_hoveredSlot)` +
`slot.HasPart && slot.Hp < slot.MaxHp && slot.Hp > 0`. Reserve
`GetDamagedSlots()` for the once-per-Show enumeration (hit-zone wiring),
where the alloc is amortized. Zero alloc in the steady state.

### Pass 1.5 polish (DO NOT BLOCK)

**P1. Budget-bar follow-mouse: `style.translate` over `style.left/top`.**
Functionally equivalent in 6.3 but `style.translate = new
StyleTranslate(new Translate(x, y, 0))` bypasses the layout pass and hits
only the transform pass. ~5× cheaper per frame. Set bar's `position:
absolute; left: 0; top: 0;` in USS once, then mutate translate each
frame. Not a build-blocker.

**P2. `IRestSceneAdapter` extraction to a shared assembly.** Could
properly relocate `RestPickerController.cs` to `WastelandRun.UI` by
extracting a thin adapter interface (Bind bars / Show pose / collect
hit zones) into a shared lower assembly. Real cleanup; **Pass 1.5
scope**, not Pass 1. The ADR-0014 smudge stays documented + deferred.

### APPROVE as-amended (5 items)

**A2-confirm. Throttle accumulator pattern.** Canonical: controller's
`Update()` runs `_acc += Time.deltaTime; while (_acc >= 0.333f) {
Vehicle.RepairSlot(_hoveredSlot, 1); _acc -= 0.333f; }`. Use `while`
not `if` so a frame stall doesn't lose ticks. `WastelandRun.Combat`
stays Time-free (`RepairSlot(string, int)` is engine-free, no leak).

**A3-confirm. `OnHover` fires per pointer event, not per frame.**
Publisher invokes `OnHover` inside `OnPointerEnter` (`:515`) and
`OnPointerExit` (`:528`) only — UGUI fires those on transition. One
enter + one exit per hover cycle. No alloc guard needed beyond a
`string _hoveredSlot` ref.

**A5-confirm. Cursor leak defenses under "drain-to-zero" rule.** The
locked rule closes the deliberate-bail window. Remaining abnormal
exits: `OnApplicationFocus(false)`, `OnApplicationPause(true)`, scene
reload mid-drain, future pause-menu `Time.timeScale = 0`.
**Mitigation:** reset cursor in `OnDisable` + `OnDestroy` +
`OnApplicationFocus(false)`. Repair mode's `Exit()` already resets on
the happy path (budget-zero → Exit → Continue click). Three hooks
cover every abnormal vector.

**A7-confirm. `ResolveRest` param drop — ADR-0011 clean.** Production
consumers per grep: `RestPickerViewModel.cs:98,105` (both die with the
VM). Test consumers: `RunSession_ResolveRest_test.cs` (assertions
asserted the atomic-heal semantic that no longer exists — **migrate
tests to new shape**, do NOT keep a `string slotId = null` default-param
to preserve compile). Update `RunSession.cs:64` xmldoc to remove
"vehicle mutation" phrasing. `RestPickerViewModel_test.cs` deletes with
the VM. No production code drift.

**A8-confirm. VM retirement — ADR-0014 clean.** ADR-0014's "controller
binds against VM" is load-bearing only when there is view-state worth
materializing (carousels, sort orders, formatted strings). Pass 1 has
none — budget is transient controller-local int. Reading
`Vehicle.GetDamagedSlots()` directly at Show-time for hit-zone wiring
is canonical. Keeping a hollow VM would be the ADR-0011 #2 ("parallel
storage") violation. Memory `feedback_gdd_verb_signature_not_load_bearing`
applies: no interface seam without ≥2 concrete consumers.

### Test surface impact (new from this pass)

- `Assets/Tests/EditMode/Run/RunSession_ResolveRest_test.cs` — semantic
  reshape; assertions migrate from "atomic full-slot heal" to
  "`OnRestModelCommitted` fires + beacon resolved, no Vehicle mutation
  from the verb itself."
- `Assets/Tests/EditMode/Run/RestPickerViewModel_test.cs` — delete with
  the VM.
- New (optional, polish): cursor-leak TearDown test (Pass 1.5 risk
  register item from initial verdict).

### Final files at risk (consolidated)

- `Assets/Scripts/CombatView/RestPickerController.cs` (reshape, stays
  in CombatView)
- `Assets/Scripts/Run/RestPickerViewModel.cs` (retire)
- `Assets/Scripts/Run/RunSession.cs:64,204` (xmldoc + `ResolveRest` API
  reshape)
- `Assets/Tests/EditMode/Run/RunSession_ResolveRest_test.cs` (semantic
  reshape)
- `Assets/Tests/EditMode/Run/RestPickerViewModel_test.cs` (delete)
- `Assets/UI/RestPicker.uxml`, `Assets/UI/RestPicker.uss`
- `Assets/Prefabs/BeaconRoots/RestRoot.prefab` (ParticleSystem child +
  cursor texture ref + budget-bar element optionally authored here or
  pure-UXML)
- `Assets/Art/UI/Cursors/welding.png` (new, placeholder OK)

# Capture — Chopshop Bay garage screen

**Date:** 2026-08-29
**System:** New UI Toolkit garage modal in `ChopshopScreen`'s `#modal-container`
**Status:** DESIGN AGREED — read-only slice pending approval to build

---

## 0. Files touched (read-only slice)

| File | Change |
|---|---|
| `Assets/UI/ChopshopScreen.uxml` | `#modal-container` `picking-mode` `Position` → **`Ignore`** (see §4) |
| `Assets/UI/GarageScreen.uxml` + `.uss` | NEW — the garage tree mounted into the socket |
| `Assets/UI/PartStorageTile.uxml` + `.uss` | NEW — compact drag-source tile |
| `Assets/UI/PartSlotRow.uxml` + `.uss` | NEW — wide collapsible slot row |
| `Assets/Scripts/UI/PartStorageTileElement.cs` | NEW |
| `Assets/Scripts/UI/PartSlotRowElement.cs` | NEW |
| `Assets/Scripts/UI/PartTextFormatter.cs` | NEW — static helper extracted from `PartOfferElement` |
| `Assets/Scripts/UI/PartOfferElement.cs` | delegates its stat/card text to the extracted helper |
| `Assets/Scripts/CombatView/GarageViewModel.cs` | NEW — POCO state, host-owned |
| `Assets/Scripts/CombatView/ChopshopWorkbenchController.cs` | mounts/unmounts the garage; `choice-4` enabled |

**ADRs at risk:** ADR-0014 (UI Toolkit primary — this is native UXML/USS, no
`UnityEvent`). ADR-0011 (two new elements rather than one stretched across three
jobs — see §5). ADR-0002 (engine-free Combat/Run — the garage reads through
`IPartData`, never a concrete SO). **ADR-0012 and the 2026-08-04 swap-only lock
are affected by the EMPTY-SLOT decision, which is NOT in this slice — see §7.**

## 1. Origin

Designed against the director's reference mockup
(`~/Desktop/referances/chopshop ref.png`, v2). **Layout adopted; information
architecture corrected** — the mockup is stat-driven and predates weapons
granting cards (`f12148b`, same day).

## 2. Shape

Full-screen modal mounted in the existing `#modal-container` socket, with a
**transparent centre** so the world-space vehicle remains the subject:

- **Left — INSTALLED SYSTEMS:** collapsible rows, WEAPONS + VITAL SYSTEMS.
- **Right — BODYWORK:** collapsible rows for optional slots.
- **Centre:** the live vehicle with its existing `SlotTargetRing` callout pins.
- **Under the vehicle:** AP meter.
- **Bottom — PARTS STORAGE:** horizontal strip of owned parts with stack counts
  and ALL / WEAPONS / BODYWORK / VITAL tabs.
- **Bottom-right:** Chopshop Bay plate + BACK.

**Row naming (director):** ONE name. The collapsible header IS the equipped
part's name ("Worn Machine Gun") — not slot-type-then-part-name, which read the
weapon's identity twice.

## 3. What the mockup assumes that we do not have

| Mockup | Reality |
|---|---|
| `SPEED` stat + meter | **No Speed stat exists anywhere in the codebase.** |
| `DODGE 10%` + `DODGE 5%` on parts | `StatKind.DodgeRate` is orphaned — no carrier, no aggregator, no `DamagePipeline` consumption. Its own doc forbids UI until all four exist. |
| `DMG 8` on Hunter MG | **Parts carry no damage.** Damage lives on cards. `PartRewardPicker_Test` asserts exactly four labels specifically to block a DMG/DODGE readout (TD verdict 2026-08-05 Q3). |
| Part icon illustrations | None authored. `PartIconAtlas` resolves null; tiles render USS-tinted. |
| 12,450 scrap / 320 L fuel | Uncalibrated — our economy is tens of scrap, 35 L tank. |
| BODYWORK with 6 populated rows | `SlotKind.Bodywork` has **zero authored slots**; layout-driven, so the group is ABSENT, not empty. |

**Director decisions on these:** DODGE hidden until the stats session builds it
properly; **granted cards occupy the expanded row where `DMG` sits in the
mockup** — same position, same weight, real data.

## 4. The picking-mode correction — I got this wrong twice

Originally proposed flipping `#modal-container` to `Ignore`. **Withdrew it** on
seeing the reference, reasoning that a transparent centre made it unnecessary.
**The withdrawal was wrong.**

UI Toolkit hit-testing is **rect-based, not alpha-based**: a full-screen
`Position` element swallows every pointer event across its whole rect whether or
not anything is painted there. So the change IS needed —
**`#modal-container` → `Ignore`, each edge panel individually `Position`.**

That is not a novel pattern; it is exactly what `#root` does three elements
above in the same file (`Ignore` root with `Position` children, so world-space
UGUI vehicle hit zones receive clicks through the gaps). Free, proven, in-file.

**Standing warning that still applies:** never flip `#root` itself to
`Position` — it silently kills repair-mode hover.

## 5. Do NOT reuse `PartOfferElement`

I had planned to. `unity-ui-specialist` argued against it and is right: it is
identity-bound to a fixed `choiceIndex` with an `OnPicked(int)` event,
click-only, and card-width. A storage tile needs drag-source behaviour and
inventory-instance identity; a slot row needs wide layout and a drop-target
role. One class across three jobs is the shape ADR-0011 flags.

**Reuse the FORMATTING, not the class** — extract `BuildArmorText` /
`BuildInfoText` / `BuildGrantedCardText` into a static `PartTextFormatter` so
all three surfaces render a part identically. (Those formatters were written
today in `b7df709`; this pulls them out.)

## 6. UI Toolkit facts worth not rediscovering

- **No built-in drag-and-drop for this shape.** `ListView`'s reorder handle is
  same-container-only. Roll it: `PointerDownEvent` + ~6px slop before committing
  to a drag (below slop the event bubbles so `ClickEvent` still fires for
  preview); above slop `StopPropagation` + `CapturePointer`.
- **Do not reparent the dragged tile.** Spawn a floating ghost, absolute,
  `picking-mode="Ignore"` so it does not shadow its own drop target, parented at
  the modal's top level so the strip's `ScrollView` cannot clip it.
- **Resolve drop targets with `panel.Pick(position)`, NOT `elementUnderPointer`**
   — the ghost sits on top of the pointer.
- **A horizontal-only `ScrollView` ignores the mouse wheel.** There is no
  vertical scroller for the wheel to drive, so the strip is a silent no-op
  unless `WheelEvent` is intercepted and `delta.y` mapped onto `scrollOffset.x`.
- **No scroll-vs-drag ambiguity to solve** — keyboard/mouse primary, no touch,
  and a horizontal `ScrollView` has no click-drag-to-pan for non-touch input.
- **Roll collapsible rows; do not use `Foldout`.** Its `unity-toggle` chrome is
  Editor-styled and expensive to strip to the diegetic look. A `Button` header +
  `.is-hidden` content matches the convention used four times already in
  `ChopshopScreen.uxml`, with identical keyboard/gamepad focus behaviour.
- **One host controller**, view decomposed into owned helper classes mirroring
  `PeekOverlayBinding`; state in a single `GarageViewModel` POCO. Panels render,
  they do not hold state (project rule: no combat/run state on MonoBehaviours).
- **Inherited from `8-DONE`:** the garage mounts inside a Chopshop, so it
  inherits the `OnBeaconActivated` refresh cadence, which fires SYNCHRONOUSLY
  inside `SetActive(true)`. **Anything it displays must be committed to the
  model BEFORE the mount**, not during the frame.

## 7. The empty-slot decision — OUT OF SCOPE HERE, under TD review

Director decided **any slot may be emptied**, with a warning (not a block) on
leaving with a fillable gap. That reverses the 2026-08-04 swap-only lock
("nothing ever dissolves"). It is the director's call and is not re-litigated.

**Nothing in the read-only slice depends on it** — greyed empty rows are pure
display, and a bare `Vehicle` has empty slots before install, so they are
testable without any model change.

Named consequences, sent to `technical-director` 2026-08-29:
`UninstallPart` does not exist · armor pool shrinks on removal (clamp
semantics?) · **uninstalling a weapon must pull its cards from `RunDeck`, and a
rehydrated `PartData` carries an EMPTY card list, so a post-resume uninstall
would remove ZERO cards and orphan them** · which copies to remove when two
identical weapons are installed · a weaponless vehicle cannot win, and the only
guard is an advisory toast.

## 8. UX findings adopted

From `ux-designer`, 2026-08-29:

- **Card comparison is the screen's whole purpose and the mockup cannot do it.**
  Clicking a tile shows an AP delta — secondary information for a weapon. Fix:
  clicking a storage tile **auto-expands the compatible installed row and
  renders the candidate's cards adjacent**, same format and sort. AP delta
  becomes the secondary readout under the card diff.
- **This means storage tiles need their own expand-in-place**, which neither
  mockup has. Argued as read-only scope, not follow-up: otherwise the comparison
  problem ships unsolved and gets bolted on later as a modal that reads worse.
- **Cut the standalone click→AP-delta mode.** Three disclosure states would
  otherwise compete (click-for-delta, expand-for-detail, drag-for-action); for a
  weapon the stat delta and card diff are the same decision.
- **Empty rows differ by category.** Weapon/vital empty is a threat — warning
  glyph + an explicit "0 cards from this slot" line. Bodywork empty is
  bookkeeping — flat grey, no glyph.
- **Exit warning:** fire ONLY when a weapon/vital slot is empty AND a compatible
  spare is held. Toast at BACK with "Equip now" / "Leave anyway", never a
  blocking dialog. Never for bodywork — warning about a gap the player cannot
  fill trains them to dismiss the banner.
- **Drag must not be the only path** (keyboard/gamepad reachability). Explicit
  buttons are the mechanism; drag is a shortcut layered on top. Applies to both
  install and uninstall.

**One UX claim I do not fully accept.** It argued drag alone cannot express
*which* weapon slot receives a universal-mount part, since the Scout has two.
True if dragging onto the vehicle; not true if dragging onto a specific list
row, which the mockup implies. Treated as an argument for **row-targeted drag**,
not as proof that drag needs a button behind it — the accessibility argument
already carries that on its own.

## 9. Build order

1. **`SourceSlotId` on `CardDefinition`** — see §10. Small, and it is the
   prerequisite for BOTH the garage's headline feature and uninstall.
2. **Read-only garage** — layout, both list panels, storage strip with tabs,
   collapsible rows, expanded rows showing granted cards, **and storage-tile
   expand so comparison works in-layout** (§8). No install gesture.
3. **Install / uninstall** — buttons first, drag as the shortcut, per §10.

### Drag spike — PROPOSED THEN DROPPED

Originally step 1: a throwaway two-element spike proving pointer-capture, ghost
and slop-click. **Dropped 2026-08-29.** The read-only slice has no install
gesture, so a spike now de-risks step 3 before step 2 exists — and it is
throwaway scaffolding, which `feedback_demo_forward_over_infrastructure`
forbids. When drag lands, real tiles and rows exist to drag it against and the
work happens in place. The durable value was never the spike; it was
`unity-ui-specialist`'s findings, which are written down in §6.

---

## 10. TD verdict on the empty-slot decision — received 2026-08-29

**CONCERNS**, confined to the card-removal seam. Everything else cleared.

### 10a. The catalog is the WRONG fix — and I built the thing that breaks it

I proposed a `PartId → PartDefinitionSO` catalog to recover a rehydrated part's
cards (recorded in `PartData.GrantedCards`' xmldoc and §7 above). **TD rejected
it, correctly.**

`PartDefinitionSO.GrantedCards` **mints fresh `CardDefinition` instances on
every access** — deliberate, documented, and locked by
`GrantedCards_MintFreshInstancesPerAccess`, all written by me the same day. So
even in a fresh session with the SO reference intact, re-reading at uninstall
time yields objects reference-unequal to what sits in `RunDeck`. A catalog hands
back non-matching instances; you fall back to structural comparison, which
cannot distinguish two identical weapons.

**The property that protects card independence is the property that makes
removal-by-reference impossible.** Worth remembering as a shape: a deliberate
freshness guarantee forecloses identity-based lookup later.

### 10b. Correct seam — stamp at grant time, not removal time

`CardDefinition` gains `string SourceSlotId` (null = chassis card or reward
pick; never removable by uninstall). `RunDeck.ComposeStarting` and the install
verb set it from the slot being filled. Uninstall becomes
`RunDeck.RemoveBySourceSlot(slotId)` — a pure model verb in `WastelandRun.Run`,
engine-free, **behaving identically before and after resume because the field
rides the wire**. No catalog, no view-side seam, no concrete-type check.

This also answers "which copies" without the per-card instance tag the QA pass
proposed: **slot granularity IS gesture granularity** — one slot, one part, one
card set.

Cost: `CardDefinitionDto` gains a field; `RunDeckDto.SCHEMA_VERSION` 1 → 2.
Under ADR-0004's any-mismatch policy that regenerates `run.session_core`, so
in-flight saves lose their run. Acceptable — already on a fresh-run footing.

### 10c. LIVE DEFECT found — verified in the assets

```
Card_BulletBarrage.asset:  LaunchSlotId: weapon_0
Card_FlameBurst.asset:     LaunchSlotId: weapon_1
```

**`LaunchSlotId` is authored into the card asset, not derived from the mounting
slot.** Install `Part_Scout_MachineGun` into `weapon_1` and its cards still fire
from `weapon_0`.

Same two-copies-of-one-fact defect (ADR-0011 #2) the granted-cards slice fixed
this afternoon, **surviving one level down**. Hidden today by swap-only plus a
fixed loadout; live the instant equip ships. Fix alongside the stamp: when a
card is stamped with `SourceSlotId`, rebind its `WeaponAttackEffect.LaunchSlotId`
to the same slot. The authored value becomes a designer default the mount
overrides. **Do not ship equip without it.**

### 10d. Uninstall shape (for step 3)

**Clamp silently, no new event, no refusal** — verified:
`SlotInstance.SetMaxHp` (`:175-180`) already drops `Hp` to the new ceiling, so
losing a contributing part to COMBAT DAMAGE already shrinks the buffer
silently. Gesture-driven removal must not invent a second policy.

Order: guards → capture `outgoing` BEFORE clearing → `SlotInstance.ClearPart()`
(a sibling verb, not a flag on `InstallPart`) → `RecomputeArmorPool()` → return
`outgoing`. **`CorrodedStacks` resets** — stacks belong to the part, not the
socket.

Guards live in the MODEL, not the UI: `RequireInstallable` already rejects
`SlotKind.Armor` model-side, and install/uninstall must reject the same slot set
or the garage can produce a vehicle it cannot rebuild. Extract
`RequireSlotMutable(slot)` before a third caller appears.

Hull/frame not emptiable and `armor_0` not user-facing: **both confirmed.**

### 10e. Unwinnable-run floor that is not a block

Advisory-only is defensible. Move the predicate to `RunDeck.HasAttackCard`
(currently run-start only in `RunSceneHost`). Then: a persistent, non-modal
"no weapons" state on the run-map vehicle panel, and **combat entry with no
attack card becomes a flee-only encounter, not a lockout.**

> "A weaponless vehicle is not unwinnable, it's *unfightable*."

No floor in the model — the model should not know what "winnable" means.

### 10f. Tension in the verdict, NOT smoothed over

TD says the read-only garage is "fully independent, nothing gates it", then says
to source installed parts' cards from **the deck's `SourceSlotId` grouping** —
which does not exist yet. Both cannot be true.

Resolution: **land `SourceSlotId` first** (step 1). The read-only slice could
instead group by the existing `LaunchSlotId`, which works today, but that is the
very field 10c corrects — building the headline feature on it would be
knowingly temporary.

**Binding constraint either way:** the garage must NEVER read
`SlotInstance.InstalledPart.GrantedCards`. That is empty after every resume.
Inventory rows read their live `IPartData`; installed slots read the deck.

### 10g. ADRs needing amendment (before step 3)

- **ADR-0012** — uninstall is a first-class armor-pool mutation path;
  clamp-on-shrink is policy for gesture-driven removal, not only damage-driven.
  Removes the "every combat slot stays occupied" premise.
- **ADR-0013** — larger: `RunDeck` gains a removal verb and cards gain
  provenance. The composition contract is no longer append-only.
- **ADR-0004** — `RunDeckDto` SCHEMA_VERSION 1 → 2, registry updated.
- **ADR-0011** — no amendment. The `LaunchSlotId` fix is an APPLICATION of #2,
  not an exception to it.

### 10h. Flagged for 1.0, not now

`SourceSlotId == null` meaning "not removable" is an implicit convention. If a
second removal axis ever appears (relic-granted, curse, chassis-mastery cards),
it becomes a nullable-overload problem. Acceptable now; revisit on the second
axis.

---

## Technical Director Review

> Full verdict recorded in §10 above (`technical-director`, 2026-08-29), scoped
> to the empty-slot model consequences. Ruling: **CONCERNS** on the card-removal
> seam only; the catalog shape rejected in favour of grant-time stamping.

**Prior TD rulings carried into this design:**

- **2026-08-05 Q3 (stat surface):** no DMG and no DODGE readouts on part
  surfaces without model backing. Binding — it is why the mockup's `DMG 8` and
  `DODGE 5%` are not built, and why `PartRewardPicker_Test` asserts an exact
  label count.
- **2026-08-01 (Shape C workbench):** the vehicle is composed BESIDE the
  dialogue panel and the dialogue stays visible during repair. The garage covers
  the dialogue panel only while open, and restores it on BACK.
- **ADR-0014:** UI Toolkit is primary for all screens; no `UnityEvent`.

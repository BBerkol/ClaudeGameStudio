# TD Verdict — read-only garage surface

**Date:** 2026-08-30
**Scope:** Step 3 of the build order in
`production/polish-captures/2026-08-29-chopshop-bay-garage.md` §9 — the
read-only garage. No install gesture, no repair move.

**Verdict:** ACCEPT, carrying forward the rulings already given for this design.

---

## Provenance — these rulings are not new

This slice was reviewed before implementation began. The design, the element
shape, and the data-sourcing constraint all come from passes recorded in the
garage capture; this file exists to name the concrete files against those
rulings.

- **`ux-designer`, 2026-08-29** (capture §8) — comparison is the screen's whole
  purpose; storage tiles need their own expand-in-place; empty rows differ by
  category; drag must never be the only path.
- **`unity-ui-specialist`, 2026-08-29** (capture §6) — do NOT stretch
  `PartOfferElement`; reuse the FORMATTING, not the class; roll collapsible rows
  rather than `Foldout`; one host controller with the view split into
  `PeekOverlayBinding`-style owned helpers; state in a single POCO.
- **`technical-director`, 2026-08-29** (capture §10f) — the binding data rule
  below.
- **Director, 2026-08-29/30** (capture §2-SETTLED, §2e) — three surfaces, the
  garage opens fullscreen from the entry rail, install/uninstall are inline row
  icons, so expand-in-place is a READING affordance only.

## Files

| File | Change |
|---|---|
| `Assets/Scripts/UI/PartTextFormatter.cs` | NEW — static, extracted from `PartOfferElement` |
| `Assets/Scripts/UI/PartOfferElement.cs` | delegates its stat/card text to the extracted helper |
| `Assets/Scripts/Run/GarageViewModel.cs` | NEW — POCO state, engine-free |
| `Assets/Scripts/UI/PartSlotRowElement.cs` | NEW — wide collapsible slot row |
| `Assets/Scripts/UI/PartStorageTileElement.cs` | NEW — compact storage tile |
| `Assets/UI/GarageScreen.uxml` + `.uss` | NEW |
| `Assets/UI/PartSlotRow.uxml`, `PartStorageTile.uxml` (+ `.uss`) | NEW |
| `Assets/Scripts/UI/GarageBinding.cs` | NEW — owns the modal: clones the screen, fills it, shows/hides |
| `Assets/UI/ChopshopScreen.uxml` | `#modal-container` `picking-mode` → `Ignore` |
| `Assets/Scripts/CombatView/ChopshopWorkbenchController.cs` | mounts/unmounts the garage; `choice-4` enabled |
| `Assets/Editor/CombatPrefabAuthor.cs` | authors the three template refs onto the controller |
| `Assets/Prefabs/BeaconRoots/ChopshopRoot.prefab` | ADDITIVE: three new `objectReference` lines |
| `Assets/Tests/EditMode/Run/GarageViewModel_Test.cs` | NEW — 12 tests |
| `Assets/Tests/EditMode/UI/GarageBinding_Test.cs` | NEW — 13 tests against the SHIPPING UXML |
| `Assets/Tests/EditMode/UI/WastelandRun.UI.Tests.asmdef` | + `WastelandRun.Combat.Tests` reference |

**Prefab handling.** The three new `[SerializeField]` template refs are added to
`ChopshopRoot.prefab` by **surgical YAML insert**, not by re-running
`Author Chopshop Root Prefab`. The authoring routine is updated in the same
commit so future regenerations carry the wiring, but regenerating *now* would
rebuild the prefab from scratch and silently discard any Prefab Mode tuning
that has not been baked back into `CombatPrefabAuthor.cs`. The insert is purely
additive — three `objectReference` lines on an existing MonoBehaviour block, no
existing value read or overwritten — so it destroys nothing and needs no
capture beyond this note.

**Amendment, 2026-08-30 (during implementation).** `GarageBinding.cs` was added
to this table after the fact. The original list had
`ChopshopWorkbenchController` mounting the garage itself, which contradicts the
`unity-ui-specialist` ruling this verdict already carries — *one host controller,
the view split into `PeekOverlayBinding`-style owned helpers*. The controller
already owns repair mode, the resource strip, the choice column, the welding
cursor and the peek overlay; absorbing clone/fill/show/hide would have made the
garage its third view responsibility. `GarageBinding` is the same shape as the
`PeekOverlayBinding` sitting beside it in `Assets/Scripts/UI/` — a plain class
the host owns, bound in `OnEnable` and unbound in `OnDisable`. No new
MonoBehaviour, no new scene object, no change to any other ruling below.

**ADRs:** ADR-0014 (UI Toolkit primary, no `UnityEvent`). ADR-0011 (two new
elements rather than one stretched across three jobs; formatting shared, class
not). ADR-0002 (`GarageViewModel` lives in the engine-free `WastelandRun.Run`
and reads `IPartData` / `RunDeck` / `RunInventory` — no Unity types, no
`PartDefinitionSO`). No DTO change, no schema bump.

## Binding rulings

**1. The garage must NEVER read `SlotInstance.InstalledPart.GrantedCards`.**
That list is empty after every resume — a rehydrated part is engine-free
`PartData` built from `PartId` and display name, with no card list on the wire.
An installed weapon's cards come from the DECK, grouped by
`CardDefinition.SourceSlotId` (shipped `3562c4e`). Inventory rows read their own
live `IPartData`, which is the authored SO and does carry cards. Getting this
wrong produces a garage that is correct in the editor and blank for players
after a save — the exact class the `PartIconAtlas` rule already guards against.

**2. `picking-mode` on `#modal-container` goes to `Ignore`, panels get
`Position`.** UI Toolkit hit-tests by rect, not alpha, so a full-screen
`Position` element swallows the centre whether or not anything is painted there.
This mirrors `#root` three elements above it in the same file. Load-bearing
rather than cosmetic: repair moves into this screen at step 4 and welding needs
the world-space vehicle clickable through the gap.
**Never flip `#root` itself to `Position`** — that silently kills repair hover.

**3. State lives in one `GarageViewModel` POCO the host owns.** Panels render;
they hold nothing. Matches the project rule against combat/run state on
MonoBehaviours, and it is what makes the read surface testable in EditMode
without a scene.

**4. Anything the garage displays must be committed to the model BEFORE the
mount.** It inherits the `OnBeaconActivated` cadence, which fires synchronously
inside `SetActive(true)`. This is what broke the first draft of
`ChopshopBeaconRevisit_Test`.

## Deliberately NOT in this slice

Install and uninstall gestures · the repair move · drag of any kind · buy/sell
(blocked on a pricing decision) · bodywork rows (`SlotKind.Bodywork` has zero
authored slots, so the group is absent, not empty).

---

## Technical Director Review

ACCEPT. The rulings above are carried forward from the 2026-08-29 passes
recorded in the garage capture, restated here against the concrete file list so
the surface is reviewed before it exists rather than after. Ruling 1 is the one
that silently produces a shipped-but-broken screen; the others fail loudly.

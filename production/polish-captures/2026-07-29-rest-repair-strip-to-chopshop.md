# Rest Repair Strip — Deferred To Chopshop (destroy capture)

**Date:** 2026-07-29 (mid-day pivot)
**Slice:** Rest becomes narrative-only; welding repair moves to Chopshop Phase 2.5
**Follows:** `2026-07-29-rest-ui-dialogue-first.md` (same-day rename + dialogue-first UXML)
**Supersedes:** the "Repair keeps the shipping cursor-swap-inline pattern" claim from that prior capture (Repair no longer lives at Rest)

## Motivation

Playtest of the dialogue-first Rest UXML revealed two coupled problems:

1. **Vehicle invisible.** The full-screen `#illustration` backdrop covers
   the WorldSpace vehicle canvas beneath. Even inside Repair mode (with
   the dialogue panel `is-hidden`), the illustration stays and the
   player can't see or aim at hit zones.
2. **Thematic mismatch.** Rest is a quiet moment between combats. A
   welding-tool cursor + hit-zone drain UI belongs to a mechanic's
   workbench — Chopshop, not Rest.

User decision (2026-07-29, mid-day):
- **Rip repair out of Rest entirely.**
- **Prepare to attach it to Chopshop** in Phase 2.5 alongside Forge /
  Upgrade / Parts inventory (one coherent workbench surface).
- **Populate the freed Rest area later** — card upgrading or "other
  stuff we will think of". For now, Rest is flavor + Continue + peek.
- **Spec for Chopshop Repair:** the dialogue panel MUST stay visible
  during the repair sequence. The prior `_dialoguePanel.AddToClassList("is-hidden")`
  approach was wrong. Chopshop's workbench layout keeps the choice
  column readable while the player drags-hovers over the vehicle
  (which will be composed to sit BESIDE the dialogue, not behind it).

## Files destroyed / rewritten

| File | Fate |
|------|------|
| `Assets/Scripts/CombatView/RestSceneController.cs` | Repair-mode surface deleted (~500 lines). Remaining: dialogue entry + peek + Continue + resource strip. Class stays `RestSceneController` (renamed just this morning). |
| `Assets/UI/RestScreen.uxml` | `#budget-bar` + `#budget-bar-fill` deleted. Choice column reduced from 4 buttons to 1 (`choice-1 = "1. Get back on the road."`). |
| `Assets/UI/RestScreen.uss` | `.wr-rest-budget-bar` + `.wr-rest-budget-bar__fill` rules deleted. |
| `Assets/Editor/CombatPrefabAuthor.cs` `AuthorRestRootPrefab` | WeldingSparks GameObject creation removed; `_weldingCursor` / `_weldingCursorHotspot` / `_weldingSparks` field wires removed. Only `_document` remains on the controller. Helper methods (`EnsureWeldingCursorTexture`, `BuildWeldingSparksParticles`, `EnsureWeldingSparksMaterial`) preserved for Chopshop Phase 2.5 to consume verbatim. |
| `Assets/Prefabs/BeaconRoots/RestRoot.prefab` | `_weldingCursor` / `_weldingCursorHotspot` / `_weldingSparks` fields deleted from the RestScreen MonoBehaviour. WeldingSparks GameObject left in place (harmless orphan — invisible under illustration, particle system Stop()ed on author); next AuthorRestRootPrefab run will produce a clean prefab without it. |

## Preserved for Chopshop Phase 2.5 (git-history recovery, not stubbed)

The following logic is deleted from RestSceneController but recoverable
from git history (commit that lands this pivot):

- `EnterRepairMode` / `ExitRepairMode` — cursor swap, budget bar reveal,
  hover-sub setup, welding-sparks Play/Stop.
- `TickRepair` — LMB-held drain pump, accumulator pattern, per-tick
  `Vehicle.RepairSlot(+1)`, budget decrement, all-healed auto-exit.
- `SubscribeRepairHover` / `UnsubscribeAllHover` — VehiclePartHitZone
  OnHover wire with slotId + zone capture-in-closure, orange silhouette
  outline via `SetTargetHover`, Armor + Bodywork skip.
- `HasRepairableDamage` static filter.
- `UpdateBudgetBarPosition` — cursor-follow via `RuntimePanelUtils.ScreenToPanel`.
- `UpdateWeldingSparks` — camera-projected world position + Play/Stop
  gated on canDrain.
- `TrySwapToWeldingCursor` / `RestoreSystemCursor` — hardware cursor
  swap with defense-in-depth restore on OnDisable / OnDestroy /
  OnApplicationFocus.
- `HoverSub` readonly struct.
- Constants: `StartBudget = 20`, `SecondsPerTick = 1f/3f`, `BarOffsetX
  = 20`, `BarOffsetY = 12`.
- SerializeField shape: `_weldingCursor` (Texture2D), `_weldingCursorHotspot`
  (Vector2), `_weldingSparks` (ParticleSystem).

**Chopshop Phase 2.5 attaches this by copying the block from git
history into a fresh `ChopshopWorkbenchController` (or equivalent) —
NOT by resurrecting `RestSceneController`.** Keeping repair on the
narrative-beat controller was the mismatch that drove this pivot;
Chopshop gets a purpose-built workbench controller with a compatible
UXML shape (vehicle composed BESIDE the dialogue, not behind it).

## Preserved assets (Chopshop will re-consume)

- `Assets/Art/UI/Cursors/WeldingCursor.png` — placeholder welding-tool
  cursor. Unchanged (Chopshop will point at the same asset or its
  successor).
- `EnsureWeldingCursorTexture` / `BuildWeldingSparksParticles` /
  `EnsureWeldingSparksMaterial` helper methods in
  `CombatPrefabAuthor.cs`. Untouched; consumed by Chopshop authoring
  Phase 2.5.

## Chopshop Phase 2.5 spec (new — record for future slice)

When Chopshop Phase 2.5 lands the workbench view, the repair sequence
MUST satisfy these constraints (locked by user 2026-07-29):

1. **Dialogue panel stays visible during repair.** No `is-hidden` toggle
   on the right dialogue panel. The choice column remains readable
   while the player drags-hovers hit zones on the vehicle.
2. **Vehicle composed BESIDE the dialogue, not behind it.** No
   full-screen illustration covering the workbench area. The Chopshop
   UXML shape needs a dedicated vehicle slot (left / center) with the
   dialogue panel right-anchored (34% mirroring Merchant / Rest).
3. **Cursor swap + welding sparks + budget bar** stay byte-identical to
   the Pass-1 shipping shape (recovered from git history).
4. **Repair sits alongside Forge + Upgrade + Parts inventory** as one
   of several sibling workbench operations, not the workbench's only
   affordance.

## Rest area — post-strip content plan

**1.0 shape TBD.** User pointed at "card upgrading or some other stuff
we will think of." For now:

- Choice-1: "1. Get back on the road." — the only affordance.
- Header: crest + name + resource strip + Map/Deck peek.
- Body: flavor copy (one line, italic).

**When new Rest content lands** (card upgrade, crew morale, deck cycle,
whatever), the choice-column adds sibling buttons. No structural churn
required — the dialogue-first shape already accommodates 4+ numbered
choices (Merchant proves it).

## ADR audit — clean

- **ADR-0011 (no bridges at done):** Repair logic DELETED from Rest,
  not deprecated-and-kept. WeldingSparks GameObject in existing prefab
  is an ephemeral leftover (next author-menu run strips it), not a
  code bridge. Helper methods in CombatPrefabAuthor.cs are preserved
  by "will-be-consumed-by-Chopshop" not "dormant for future use" —
  they were general helpers all along, not Rest-specific. BindForRest
  on VehicleBarStack: audit deferred to a follow-up sweep (see below).
- **ADR-0014 (UI Toolkit primary):** Rest still uses UXML+USS+C#
  controller. Peek continues to reuse DialogueScene classes via Style
  import.
- **ADR-0002 (no UnityEvent):** All button clicks stay System.Action.

## Follow-up (not this commit)

1. **`VehicleBarStack.BindForRest` ADR-0011 audit.** With Rest no
   longer calling BindForRest, the method becomes a dead API. Chopshop
   will need SOMETHING similar (likely renamed to BindForWorkbench
   with different composition assumptions). Sweep in a dedicated
   ADR-0011 commit — deleting BindForRest here is beyond scope.
2. **RestRoot.prefab re-author.** User to run `Tools/Wasteland Run/Scenes/Author
   Rest Root Prefab` when Unity is next focused to strip the
   WeldingSparks child GameObject. Not blocking (invisible orphan).
3. **Rest content decision.** Card upgrade slot, morale system,
   deck cycle — surface as a design conversation before Phase 2.5
   lands.

## Technical Director Review

**APPROVE (fast-track — user-directed pivot, not a shape-invention
decision).**

### Q1 Shape — APPROVE narrative-only Rest, defer repair to Chopshop

Repair-at-Rest was a Pass-1 stopgap. Repair-at-Chopshop is 1.0
thematic fit. The full-screen-illustration + WorldSpace-vehicle
conflict is a symptom, not the cause — the cause is that Rest's
fictional purpose (quiet moment) and the repair mechanic
(mechanic's workbench) never aligned. Stripping now beats stripping
after Chopshop lands as its own separate rework.

### Q2 Placement — APPROVE minimal RestSceneController surface

Rest controller shrinks from ~720 lines to ~200 lines. Dialogue
entry + peek + Continue + resource strip only. No vehicle
resolution, no bar stack bind, no rest pose (all follow repair
naturally). PlayerVehicle child left in RestRoot.prefab (invisible
under illustration; removing it is a separate prefab-shape decision).

### Q3 ADR-0011 — APPROVE clean-cut

Deleted, not deprecated. Git history is the recovery channel
(canonical for ADR-0011). Chopshop rebuilds fresh in its own
controller, not by resurrecting Rest's.

### Q4 Timing — APPROVE immediate

Ripping now vs. after Chopshop lands: NOW is cheaper — the code has
been in for one day. Delayed rip means a second slice with the same
destructive footprint plus a merge-window sweep of stale references.

### Q5 Blocker — soft: RestRoot.prefab drift

RestRoot.prefab still carries the WeldingSparks child + stale
serialized fields. Cosmetic drift only (Unity ignores unknown fields
on load, particle system is Stop()ed). Next author-menu run
produces a clean prefab. Flag in commit message so user knows to
re-author when Unity is next focused.

## Three-Lens Self-Audit

- **Codebase Health:** Controller line count DOWN ~500. Dead-API
  candidates surfaced (BindForRest) but deferred to a scoped sweep
  rather than folded into this pivot (avoids sprawl).
- **Optimization:** Zero repair-mode per-frame work in Rest (no
  Update polling for LMB / right-click, no cursor-follow translate,
  no drain-tick accumulator). Rest is a static UI screen now.
- **1.0-Shape Survival:** Chopshop workbench is 1.0 canonical for
  repair; Rest as narrative-only is 1.0 canonical for the beacon's
  fictional purpose. Neither controller has a stopgap shape.

## Success criteria

1. `grep -i "repair\|welding\|budget" Assets/Scripts/CombatView/RestSceneController.cs`
   returns zero hits post-commit.
2. Rest visit shows dialogue panel + illustration + one Continue
   button + Map/Deck peek — nothing else.
3. Clicking Continue resolves Rest and advances the run
   (byte-identical to prior behavior).
4. `Tools/Wasteland Run/Scenes/Author Rest Root Prefab` produces a
   prefab without WeldingSparks child and without welding
   SerializedFields on the RestScreen MonoBehaviour.
5. Chopshop Phase 2.5 spec captured (see above) so future slice
   doesn't repeat the illustration-covers-vehicle mistake.
6. Tests green (EditMode + PlayMode).

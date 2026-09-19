# TD Verdict — Granted-Card Rehydration: Part Catalog + DTO Normalization (B′)

**Date:** 2026-09-19
**Verdict:** **AMEND** — Option **B′**: resolve `PartId → IPartData` at the
**load boundary**, and normalize `PartDataDto` down to a reference.
**Option A (widen the wire) REJECTED.**
**Provenance:** FRESH `technical-director` consultation, commissioned after c2
logged this as a blocking prerequisite of the equip gesture.

**Files at risk:** `Assets/Scripts/Combat/IPartCatalog.cs` (NEW),
`Assets/Scripts/CombatView/PartCatalog.cs` (NEW),
`Assets/Scripts/Save/Dtos/PartDataDto.cs`,
`Assets/Scripts/Save/Dtos/RunInventoryDto.cs`,
`Assets/Scripts/Save/Dtos/PendingPartOfferDto.cs`,
`Assets/Scripts/Save/Dtos/SlotSnapshotDto.cs`,
`Assets/Scripts/Save/Dtos/VehicleStateDto.cs`,
`Assets/Scripts/Combat/PartData.cs`, `Assets/Scripts/Combat/Vehicle.cs`,
`Assets/Scripts/CombatView/RunSceneHost.cs`

**ADRs:** ADR-0012 (amendment — reverses the deferred-catalog non-goal),
ADR-0013 (note), ADR-0004 (3 schema bumps), ADR-0011 (#2, exception #4)

## Technical Director Review

### It corrected the brief — my own cost model for B was FALSE

I briefed that a catalog costs "an engine dependency at a layer that is
currently engine-free." Wrong, and the correction is what flips the decision.

`WastelandRun.Run.asmdef` has `"noEngineReferences": true` and **already holds
live `PartDefinitionSO` instances at runtime**: `RunSceneHost.cs:1483` passes
`_partRewardPool.Parts` (a `List<PartDefinitionSO>`) into the Run-assembly type
`FlatPartRewardSource` through `IReadOnlyList<IPartData>` covariance, and
`RunSceneHost.cs:1473-1483`'s own xmldoc names the trick: *"the Run assembly
never sees the ScriptableObject."*

A catalog therefore costs **zero** engine dependency. `IPartCatalog` is an
engine-free interface in Combat; the implementation lives in CombatView beside
`BuildPartRewardSource`. ADR-0011 exception #4, already shipped, already
load-bearing.

**Direct precedent already in the repo:** `PartIconAtlas.cs:16-26` resolves
`PartId → Sprite` for the *stated* reason that "an installed part rehydrates as
an engine-free `PartData` POCO after a save resume — it has no SO reference and
never will." Sprite is a facet of part-definition data; granted cards are the
same facet class. **The project already took this decision once — in the view
layer, not the model.**

### `PartData.cs:44-57` is a correct ruling applied to the wrong direction

The 2026-08-29 catalog rejection says a catalog "returns non-matching instances
and forces structural comparison, which cannot distinguish two identical
weapons." That argument is about **identity matching for removal** and it is
sound — stamping stays the removal answer.

It has **zero force against install**, which needs only "what cards does this
PartId grant," and *wants* freshly-minted instances so each deck card is
independently removable. The comment's own header says "Do NOT try to recover
the list at removal time" — it never ruled on install. It is a category error,
and it is currently the strongest-looking blocker to the right answer. Rewrite
it in the same commit.

### Option C (recompose at the install site) is the problem statement, not an option

- **Before a resume:** the inventory row holds the SO
  (`FlatPartRewardSource` → `_partRewardPool.Parts` → `RunController.cs:645`).
- **After a resume:** it does not — `RunSceneHost.cs:481` →
  `RunInventoryDto.cs:49 new PartInstance(Part.ToPartData(), InstanceId)` →
  `PartDataDto.cs:72-76`.

C only works if something restores fidelity first, which is A or B. The real
third option is **C′ = restore fidelity at the load boundary** — B at the right
seam. That is B′.

### THREE live defects, already shipping, that B′ closes and A does not

**D-a — Chopshop storage tiles go blank after a resume.**
`PartStorageTileElement.cs:177` takes the headline from
`PartTextFormatter.DamageRange(row.Data.GrantedCards)`; `:211` calls
`BuildCardChips(row.Data.GrantedCards)`. Post-resume `row.Data` is a `PartData`,
so every carried weapon shows **durability instead of its damage range and zero
card chips**. Reachable today with no new code. `GarageViewModel.cs:86-91` and
`:306-309` assert this is safe — true pre-resume, false post-resume; `:114-124`
documents the hazard for the *installed* side then walks into it on *storage*.

**D-b — Restored part offers name no cards.** `PartOfferElement.cs:178` →
`PartTextFormatter.cs:125 InfoLine(..., CardSummary(part.GrantedCards))`, and
`PendingPartOfferDto.cs:113-114` rehydrates choices via `ToPartData()`. The
offer is *deliberately* persisted rather than regenerated
(`PendingPartOfferDto.cs:24-30`), so this is the designed path.

**D-c — Stat staleness.** `PartDataDto.ToPartData()` rebuilds `MaxHp` /
`ArmorContribution` / `MountDirection` / `DisplayName` from **save-time** values.
A designer retune never reaches a part in an in-flight save, and a stale
`ArmorContribution` silently mis-sizes `armor_0` via sum-of-parts (ADR-0012).

**D-c is what decides A vs B.** `PartDataDto` is already a denormalized copy of
an authored asset, and it is already drifting. A makes that copy bigger and adds
polymorphic effect trees to it. **B′ deletes the copy.**

### Why A is rejected

1. **A needs three DTOs, not one.** `SlotSnapshotDto` does not embed
   `PartDataDto`. `RunSceneHost.cs:777` builds the vehicle from the SO, then
   `:783 loadedVehicleState.ApplyTo(player)` overwrites every slot and
   `SlotInstance.cs:294-300` replaces the SO record with a fresh `PartData` —
   the resume destroys the live records one line after creating them. So
   uninstall-after-resume hands c2's `UninstalledPart` a degraded record and
   **re-install grants zero cards**. A must widen `SlotSnapshotDto` too. Card
   definitions end up triplicated on the wire.
2. **A freezes content for in-flight runs.** A `BulletBarrage` balance patch
   would not reach a resumed player's inventory. For an EA roguelike shipping
   balance patches this compounds every patch, invisibly.
3. **A destroys the definition/instance boundary right before parts grow
   instance state.** `RunInventoryDto.cs:90-94` already predicts the next bump
   ("the `PartRarity` field when rarities land"). Under B′ rarity/wear/mods land
   next to `part_id`, visibly distinct from definition data. Under A they land
   indistinguishable from six copied definition fields. **That is the
   maintainability failure this decision is actually about.**
4. **The `CardDefinitionDto` precedent does not transfer.** A deck card is
   instance state carrying `SourceSlotId` provenance minted at grant time — it
   must ride the wire. A part's granted-card list is pure definition, a function
   of `PartId`. Same-looking data, different ontological class.

### The slice — one slice, two commits

**P1a — catalog + inventory/offer resolution. UNBLOCKS c3's install half, and
closes D-a, D-b, D-c on its own.** (~8 files)

1. `IPartCatalog` in `WastelandRun.Combat`, engine-free:
   `bool TryGet(string partId, out IPartData part)`.
2. `PartCatalog` in CombatView: `Resources.LoadAll<PartDefinitionSO>("VehicleParts")`
   → dictionary, cached per domain reload. All 8 assets already live in
   `Assets/Resources/VehicleParts/` — **the folder is the registry**, so no new
   authored asset and no drift-by-omission. Mirrors `PartIconAtlas.cs:113`.
3. **`PartDataDto` collapses to `{ part_id }`.** Keep the class (it is the
   extension point rarity/wear will use); delete `display_name`, `slot_kind`,
   `max_hp`, `armor_contribution`, `mount_direction`. `ToPartData()` becomes
   `Resolve(IPartCatalog)` returning the SO itself.
4. `RunInventoryDto.ToRunInventory(IPartCatalog)`,
   `PendingPartOfferDto.ToPartOffer(IPartCatalog)`; bump both `SCHEMA_VERSION`.
5. Wire at `RunSceneHost.cs:481` and `:804`.
6. **Unresolvable id:** offer → collapse to no-offer (reuses the mixed-null rule,
   `PendingPartOfferDto.cs:110`); inventory row → drop with `Debug.LogError`.
   **Never throw** — `RunSceneHost.Initialize` runs outside the `JsonException`
   recovery chain, the same constraint `SlotInstance.cs:279-284` documents.
7. Rewrite `PartData.cs:30-68`. `PartData` survives as the identity-less
   synthetic record for the enemy/test int path (`Vehicle.cs:541-549`), where
   `GrantedCards => Array.Empty` is *honestly* correct.

**P1b — vehicle-slot resolution. UNBLOCKS uninstall→re-install round-trip.** (~6 files)

8. `Vehicle.RestoreSlotState` — replace the three identity params (`partId`,
   `displayName`, `mountDirection`) with one resolved `IPartData installedPart`.
   The signature **narrows**; the SO-vs-POCO choice leaves Combat entirely.
9. `SlotSnapshotDto` sheds `installed_part_display_name` + `mount_direction`,
   keeps `installed_part_id`. Safe: `SlotInstance.cs:82-83` defines both as
   derived, and the only view readers (`TargetingReadoutWidget.cs:283`,
   `VehicleBarStack.cs:1148`) go through the derived property.
10. Bump `VehicleStateDto.SCHEMA_VERSION`.
11. **Unresolvable id on a slot → pass `null`**, producing
    `HasPart && InstalledPart == null`, which **c2 already throws on loudly**
    (`Vehicle.cs:492`). No new failure mode; the guard just shipped is the
    terminal.

### Migration: NONE. No one-shot migrator.

The bumps do it. `SaveSystem.Load.cs:284-287` records a `SchemaMismatch` and
`continue`s. `RunInventoryDto` and `VehicleStateDto` are both `run.session_core`
members, so `HasCompleteSessionCore` goes false and `RunSceneHost` regenerates
the group — the documented EA all-or-none policy (`SaveBootstrap.cs:176-186`,
`:244-248`). `PendingPartOfferDto` is group-of-one and collapses to no-offer.
ADR-0011 exception #2 working as designed.

**Land P1a and P1b in the same session** — split across two playtest builds, a
tester eats two run losses instead of one.

### Schema-bump mechanics

The registry is distributed: the constant lives on each DTO class and
`SaveSystem.Load.cs:272` reads it off the registered adapter. **No central
version table** — a many-file, one-line-each change across `RunInventoryDto`,
`PendingPartOfferDto`, `VehicleStateDto`. The CI uniqueness gate is on
`SystemId`, not version, so it is untouched. Test envelopes
(`RunSceneHost_Resume_Test`, `RunSceneHost_RestResume_test`) reference
`X.SCHEMA_VERSION` **by constant, never by literal** — bumps are transparent to
existing fixtures.

### c3: unchanged. Do not reorder, do not narrow.

P1a → P1b → c3 as scoped. Shipping c3's uninstall half alone was correctly
rejected: "parts come off but don't go back on" is an ADR-0011 bimodal path of
the worst kind, because the missing half is invisible until the player tries it.
With P1a+P1b in front, c3's resume round-trip is satisfiable for the first time.

### Defects this would WAKE: one watch item, zero wakes

Grepped, not reasoned. The three real defects are already awake.

- `InstalledPart.MaxHp` / `.ArmorContribution` — **zero hits repo-wide.** Every
  readout uses `slot.MaxHp` / `slot.ArmorContribution`
  (`GarageViewModel.cs:250-252`). No wake.
- `InstalledPart.SlotKind` — today `SlotInstance.cs:297` forces it to the slot's
  `Kind`; under B′ it becomes the authored kind. Only comparison site is
  `Vehicle.cs:375 RequireInstallable`, which *enforces* equality on every
  install path. No wake.
- **WATCH (allocation, not correctness):** `PartDefinitionSO.cs:88-103` mints
  fresh `CardDefinition` instances on **every** `GrantedCards` access.
  Post-resume those reads are currently free (empty array); after P1a they
  become real allocations, and `GarageViewModel.cs:311`,
  `PartStorageTileElement.cs:177` and `:211` each hit the property
  independently — **3 mints per storage row per garage open.** Bounded by
  inventory size, on a player gesture, not per-frame. Ship as-is; if garage-open
  profiles hot, collapse the three reads into one.

### Three-lens self-audit (TD's own, unprompted)

**Health.** No existing bridge/parallel storage for parts. The drift B′ *could*
introduce is keeping denormalized stat fields alongside catalog resolution — two
sources of truth for `MaxHp`. **That is why P1a deletes them in the same
commit; resolving cards from the catalog while reading stats from the wire would
be a split-brain record, worse than either option alone. Non-negotiable.** No
new MonoBehaviour/events, so no lifecycle delta. Catalog construction belongs in
`RunSceneHost` beside `BuildPartRewardSource` — one field, one method, no
manager. `PartCatalog` and `PartIconAtlas` will both be
LoadAll-into-dictionary — two callers, extract nothing yet. Mirror
`PartIconAtlas`'s domain-reload reset guard so a stale map cannot be served.

**Optimization.** Built once per domain reload, read at 3 resume sites; not
per-frame. One 8-element array + one dictionary. B′ **reduces** load-time
allocation vs A, which would deserialize N `CardDefinitionDto` + polymorphic
effect trees per inventory row per load. **ADR-0008 flag (record, do not act):**
`Resources.LoadAll` eagerly loads every part's sprite ref — nothing at 8 parts,
a real line item against the 41 MB EA budget at 1.0 part count;
`PartDefinitionSO.cs:30-34` already carries the Addressables migration note.

**1.0 survival.** `PartDataDto = { part_id }` **is** the 1.0 shape and survives
the predicted `PartRarity` bump cleanly. `IPartCatalog.TryGet` survives; the
`Resources`-backed impl is knowingly throwaway when parts move to Addressables —
interface durable, impl replaceable. Downstream subscribers (workbench-shop,
parts-mastery) both need `PartId → definition` and are served with no signature
growth.

**Audit deltas — mandatory, not optional:**

- **(a) DOC SWEEP is part of P1a's done state.** Five blocks assert in prose
  that no catalog exists or ever will: `PartData.cs:14-18` and `:44-57`,
  `PartDataDto.cs:22-31`, `GarageViewModel.cs:86-91` / `:114-124` / `:306-309`,
  `RunDeckSourceSlot_Test.cs:10-32`, `PartIconAtlas.cs:16-26`. All false the
  moment P1a lands. Duplicated rationale prose left standing **is itself the
  defect** — the `PartData.cs` comment is living proof, since it is why this
  hole survived three slices.
- **(b) Add `PartCatalog_Completeness_Test` (EditMode).** Every
  `PartDefinitionSO` asset resolves; every part referenced by
  `Vehicle_Scout.asset._parts` and each `PartRewardPool_*.asset` is present.
  This is what keeps the unresolvable-id branch unreachable in shipped builds.
- **(c) SEEN-FAILING FIRST.** Write the resume round-trip against current `main`
  and watch it fail on empty `GrantedCards` before a line of P1a exists. Same
  for stat staleness (retune a part SO's `MaxHp`, resume, assert the *authored*
  value).
- **(d) CONTENT RULE for the ADR amendment: a shipped `PartId` is never
  deleted.** Retire a part by removing it from the pools, not from the folder.
  Normalization makes save recoverability depend on content resolution; this is
  the rule that keeps that dependency safe.

**Documentation:** an **ADR-0012 amendment**, not a new ADR — it reverses
exactly one clause (the deferred catalog non-goal) and leaves sum-of-parts armor
untouched. Plus a one-paragraph ADR-0013 note: stamping remains the **removal**
mechanism, the catalog serves **install/display only**; they are complementary,
and writing that down is what stops someone "simplifying" one into the other.

**Success criteria:** (1) c3's resume round-trip passes with no card data on the
wire; (2) a designer retune of a part SO reaches an in-flight save with no code
change; (3) when `PartRarity` lands it is a one-field addition and nobody has to
ask which fields are authoritative.

## Independent verification by me

| Claim | Site | Result |
|---|---|---|
| `Run.asmdef` is engine-free yet holds live SOs via covariance | `WastelandRun.Run.asmdef` + `RunSceneHost.cs:1483` | CONFIRMED — my brief's cost model was wrong |
| `PartIconAtlas` already resolves `PartId → Sprite` for this exact reason | `PartIconAtlas.cs:16-26` | CONFIRMED, verbatim rationale |
| All part assets live in one Resources folder | `Assets/Resources/VehicleParts/` | CONFIRMED — 8 assets |
| D-a is live and player-visible | `PartStorageTileElement.cs:177`, `:211` | CONFIRMED |
| D-b path | `PartTextFormatter.cs:125` | CONFIRMED |
| `PartData.GrantedCards` hardcoded empty | `PartData.cs:68` | CONFIRMED |
| No catalog exists anywhere | repo-wide grep | CONFIRMED — 0 hits |

## User Ruling (2026-09-19)

**B′ as specced. APPROVED.** P1a then P1b in the same session, seen-failing
first, then c3 as scoped. Accepted alongside: in-flight saves regenerate (no
migrator), and the "a shipped PartId is never deleted" content rule.

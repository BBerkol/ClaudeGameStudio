# TD Verdict — Phase 2.5C Parts Axis, Slices A1→A3 (2026-08-04)

> **Provenance:** This file captures the 10-verdict TD synthesis produced in
> the 2026-08-04 planning session (recorded in
> `production/session-state/active.md` §3 and memory
> `project_parts_axis_a1_seams`). Per that session's explicit instruction —
> "Do NOT re-brief the technical-director on A1; the 10-verdict synthesis
> covers the whole A1→A3 chain" — this is a capture of the standing verdict,
> not a fresh consult. Re-consult fires only if scope shifts
> mid-implementation.

## Scope

Parts become a third reward axis (alongside scrap + cards), in three slices:

- **A1 — RunInventory POCO (model only).** `PartInstance.cs` (new,
  `readonly record struct` holding `IPartData`), `RunInventory.cs` (new,
  `List<PartInstance>` + add/remove/query + per-run InstanceId counter),
  `Vehicle.SwapPart` seam on `Vehicle.cs` (damage-ratio-preserving install,
  ceiling rounding, reuses `RecomputeArmorPool()` per ADR-0012).
- **A2 — save adapter.** `RunInventoryDto.cs` (new, carries
  `PartInstanceDto` rows + `next_instance_id`) + `RunInventorySerializable.cs`
  (new adapter) joining the ADR-0004 `run.session_core` resume-atomic group
  (same membership rationale as `RunDeckDto` — seed-locked timeline must not
  outrun dropped loot); `RunState.cs` gains ctor-constructed `Inventory`
  (fresh inventory is ALWAYS empty — chassis-fresh scrap parts are authored
  on the vehicle SO, so no `StartRun` parameter; a required param would be
  constant ceremony at the fresh site, and TD 2026-06-25 Q2 keeps StartRun
  single-shape with resume branching in the host); resume flows through an
  internal `RunController.RestoreInventory` verb (exact precedent:
  `RestorePendingOffers`); `PartData.cs` (new, engine-free `IPartData`
  class in Combat) as the rehydration carrier — sprite resolution stays
  view-side by PartId. `SaveBootstrap.cs` registers the adapter;
  `LoadedRunSnapshot.cs` gains the session_core member; `RunSceneHost.cs`
  Initialize gate + `BeginRunFromLoaded` grow the fifth member.
- **A3 — reward-source composition.** `IPartRewardSource.cs` (new sibling
  seam, ADR-0013 pattern), `PartOffer.cs` (new, mirrors `CardOffer` incl.
  recorded OfferSeed for M2 pity), `FlatPartRewardSource.cs` (new,
  ctor-injected pool), `PartRewardPoolSO.cs` (new SO in CombatView/Data),
  `CombatReward.cs` extends additively with a `PartOffer Parts` field,
  `RunState.cs` gains `PendingPartOffer`, `RunController.cs` gains
  `PartOfferSeedMix = 0x5054` ('PT') + `DerivePartOfferSeed` +
  `AcceptPendingPartChoice`/`SkipPendingPartChoice`, `RunSession.cs`
  `ExitCombat` composes the third source + accept/skip pass-throughs.
  Tests: `RunInventory_Test.cs`, `VehicleSwapPartTests.cs`,
  `RunInventoryDto_*_test.cs`, `PartRewardSource_Test.cs`,
  `RunControllerPartOffer_Test.cs` (new).

## Locked decisions (planning session — do not re-open)

1. Reward cadence: **every combat**.
2. **No** auto-scrap of Commons — manual sell only ("nothing dissolves").
3. Swap damage-ratio rounding: **ceiling** —
   `newHp = ceil(newMaxHp × oldHp / oldMaxHp)`. Prevents swap-to-repair;
   Rest/Repair stay the only healing verbs.
4. A3 is a **sibling `IPartRewardSource`** — NOT folded into `IRewardSource`,
   NOT a 3-tuple. Mirrors `ICardRewardSource` (ADR-0013).
5. `PartInstance` holds **`IPartData`, not `PartDefinitionSO`** — ADR-0002
   engine-free assembly; the SO already implements the interface.
6. `SwapPart` is its **own method** — not an `InstallPart` overload, no
   bool/optional-param variant (ADR-0011 forbids bimodal paths).
7. `PartRarity`, chassis-fit rules beyond existing SlotKind/MountDirection
   checks, UI, and the part catalog are **out of scope** for A1→A3.

## ADRs at risk

- **ADR-0002** (engine-free combat model) — held: `IPartData` throughout;
  SO types never enter Run/Combat assemblies. CLEAN.
- **ADR-0003** (deterministic RNG) — new salt `0x5054` ('PT') verified
  distinct from 0x4341/0x5257/0x434F/0x4D41/0x4D52. Per-call
  `System.Random(offerSeed)`. CLEAN.
- **ADR-0004** (save) — RunInventoryDto joins `run.session_core`;
  SystemId `run.run_inventory`, SCHEMA_VERSION 1. CLEAN.
- **ADR-0011** (no bridges) — no compat overloads, no bimodal paths;
  required StartRun param, not a default. CLEAN.
- **ADR-0012** (sum-of-parts armor) — SwapPart routes through
  `RecomputeArmorPool()`; armor_0 buffer slot is swap-forbidden (derived
  state, never authored/installed directly). CLEAN.
- **ADR-0013** (reward-source composition) — sibling seam + additive
  `CombatReward` field + recorded offer seed. This IS the pattern's second
  application. CLEAN.

## Final-game picture

1.0 ships the full parts progression axis (`project_parts_axis_in_1_0`):
every combat drops parts, RunInventory feeds the Tarkov-style equip UI
(2.5E), Chopshop buys/sells inventory rows, Codex discovery keys off drops.
A1→A3 is the model + persistence + reward spine all of that mounts on; the
seams (sibling reward source, IPartData carrier, InstanceId row identity)
are the 1.0 shapes, not scaffolding.

## TD Verdict

**GO** (captured from the 2026-08-04 planning-session 10-verdict synthesis;
sequence strictly A1 → A2 → A3; EditMode tests per slice; no commits without
user instruction).

## Amendment package — fresh TD consult 2026-08-04 (user-adopted in full)

Q1/Q2/Q3 below correspond to the three findings surfaced after A1/A2 landed.
Full verdict text lives in-conversation; user adopted all amendments and
answered the open designer question: **swap-only — un-equip-to-empty is NOT
legal; every combat slot stays occupied; no `UninstallPart` verb ever.**

- **Q1 ACCEPT as built** + guard: `RunController.RestoreInventory` throws
  unless the current inventory is still empty (protects 2.5E's held
  reference). Scheduled (not now): `VehicleStateDto` joins `run.session_core`
  at the first slice where a non-authored part can occupy a slot.
- **Q2 ACCEPT (a)**: `RequireInstallable(slot, part)` single helper on
  `Vehicle.cs` — Armor-slot rejection (both IPartData verbs, closing the
  install-onto-armor_0 hole) + `part.SlotKind == slot.Kind` gate +
  MountDirection gate. Int overload stays ungated (no kind data on the wire)
  with an explicit xmldoc sentence: input-completeness asymmetry, not
  validation-policy asymmetry.
- **Q3 AMEND (a)**: `SlotInstance.cs` gains `InstalledPart` (`IPartData`);
  `InstalledPartId`/`InstalledPartDisplayName` become derived properties (no
  duplicate storage); all three write paths produce the record — caller's
  instance on the IPartData verbs, synthesized `PartData` on the int overload
  when `partId != null` (closes the BuildScout player-fallback hole) and on
  `RestoreFromSnapshot`. `SwapPart` returns the outgoing `IPartData`.
  `SlotSnapshotDto.cs` gains nullable `mount_direction`
  (StringEnumConverter, not Required); `VehicleStateDto.cs` SCHEMA_VERSION
  1→2 (sole bump — free this window since A2 already regenerates all saves);
  `Vehicle.RestoreSlotState` gains `SlotPosition? mountDirection` param;
  corrupt row (id without direction) → null record, never throw (load path
  only catches JsonException).
- **A3 pre-flight decided**: `PendingPartOfferDto.cs` +
  `PendingPartOfferSerializable.cs` ship as a persisted group-of-one (NOT
  session_core; regenerate-at-resume rejected — pool is dupe-filtered against
  mutable inventory). Shared `PartDataDto.cs` extracted now (second
  full-surface consumer; `RunInventoryDto` unreleased so `PartInstanceDto`
  restructures to `instance_id` + nested `part` with no version bump).
  `RestorePendingOffers` widens to `(card, event, part)` — no fourth verb.
  `AcceptPendingPartChoice` only ever adds to `RunInventory` — never
  auto-equips (install locked in Combat, open on RunMap). Empty/absent
  part pool = "this biome drops no parts yet" (ADR-0015 data narrowing) —
  `Generate` may return null and `ExitCombat` latches nothing, keeping the
  game playable between 2.5C and the 2.5E picker UI.

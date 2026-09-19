# TD Verdict — Chopshop Step 5, Model Layer (install / uninstall)

**Date:** 2026-09-19
**Slice:** Phase 6, chopshop step 5 — model verbs behind the equip gesture
**Files at risk:** `Assets/Scripts/Combat/CardDefinition.cs`,
`Assets/Scripts/CombatView/Data/CardDefinitionSO.cs`,
`Assets/Scripts/Combat/Vehicle.cs`, `Assets/Scripts/Combat/SlotInstance.cs`,
`Assets/Scripts/Run/RunController.cs`,
`Assets/Tests/EditMode/Run/RunDeckComposition_Test.cs`,
`Assets/Tests/EditMode/Run/RunDeckSourceSlot_Test.cs`
**ADRs at risk of drift:** ADR-0012 (part data + sum-of-parts armor),
ADR-0013 (run-scoped card collection), ADR-0004 (save/persistence),
ADR-0011 (no bridges at done)

## Provenance of this file — READ FIRST

The technical-director and unity-ui-specialist consultations for this slice were
run **earlier on 2026-09-19**. That session was closed accidentally before the
verdict was written to disk, so what follows is **transcribed from the verdict
record I wrote into `production/session-state/active.md` at session close**
(entry `2026-09-19 (session close): STEP 5 DECISION PACKAGE`), not a fresh
consultation and not a verbatim agent transcript. It is a faithful summary of
rulings that were obtained, and **every blocking claim below was independently
verified by me in source** — both at the time and again on re-open (see
Verification section). Nothing here is reconstructed from assumption.

If a verbatim fresh verdict is wanted instead, re-spawn `technical-director`
with this file as the brief and replace this section.

## Technical Director Review

**Verdict: AMEND** — proceed, with the amendments below. Three of them reverse
what the proposing brief claimed.

### 1. REVERSAL — a uniform structural gate BREAKS BOOT

The brief claimed hull installs all route through the ungated `int` overload, so
`RequireInstallable` could safely reject structural slots. **True for enemies
only.** Verified chain: `Vehicle_Scout.asset:26` lists `hull_0` in `_parts` →
`VehicleDefinitionSO.cs:110` installs it through the **gated `IPartData`
overload** → `SmallFrame.asset:48-51` marks `hull_0` `IsStructural: 1`. Gating
structural inside `RequireInstallable` therefore throws inside `BuildVehicle`
and **the player vehicle cannot be constructed at all.**

**Ruling:** `RequireSlotMutable` is **UNINSTALL-ONLY**. `RequireInstallable` is
left unchanged.

### 2. The Armor clause is DUPLICATED by decision, not by accident

Install rejects Armor because armor capacity is *derived* (sum-of-parts,
ADR-0012). Uninstall rejects Armor because there is *no outgoing record* for a
derived pool. Same condition, two unrelated reasons, two distinct messages.
Extracting a shared guard would fuse two independent rules that are free to
diverge later. This ruling is explicitly flagged as **the one most worth user
challenge** — see User Rulings below; it was upheld.

### 3. TWO BLOCKING DEFECTS found in the existing model (both verified)

**(a) ARMOR LEAK.** `Vehicle.ComputeArmorSum:467` skips only
`DamageState.Offline`. An emptied slot is `Empty`, **not** `Offline`, so it keeps
paying its `ArmorContribution` forever — and survives save/resume, because
`RestoreSlotState` deliberately does not recompute. `ClearPart` must zero
`MaxHp` / `Hp` / `ArmorContribution` / `CorrodedStacks` and null `InstalledPart`,
not merely flip `HasPart`.

**(b) SILENT PART DISSOLUTION.** `RestoreFromSnapshot` nulls `InstalledPart` on a
corrupt row, and its comment promises un-equip "fails loudly" — nothing actually
fails, because un-equip is unreachable today. The moment the gesture ships,
`UninstallPart` returns null, the part leaves the vehicle and never reaches
inventory. Post-resume only, and invisible. `UninstallPart` must **throw** on
`(HasPart && InstalledPart == null)` **before any mutation**.

### 4. REPLACE, DON'T COPY — `CardEffect.Clone()` is REJECTED

`WithSourceSlot` (`CardDefinition.cs:108-113`) already allocates a fresh
`CardEffect[]` and copies elements by reference. The fix is to **replace** each
`WeaponAttackEffect` with `new WeaponAttackEffect(sourceSlotId, e.Damage)` and
pass every other effect through. Nothing is ever mutated, so no general deep copy
is required.

`CardEffect.Clone()` is rejected on a confirmed structural fact:
`WastelandRun.Combat.asmdef` has `"references": []`, so Combat can **never** reach
the existing `CardEffectDto` reconstruction (7 subtypes). The duplication would be
permanent and **unfixable by construction** — ADR-0011 #2 in its purest form.

A `ToRuntime` deep copy is also rejected: it allocates per access, and
`GrantedCards` mints fresh instances on **every** read, including every
`RefreshGarage`.

The rebind belongs **inside** `WithSourceSlot` (Combat assembly) so that
provenance and launch physically cannot diverge.
**INVARIANT: the launch slot IS the granting slot.**

### 5. DOC CHURN: three amendments collapse to one, non-gating

- **ADR-0012 needs nothing.** `:167` already names `UninstallPart`; `:36`, `:43`,
  `:216` already specify install/uninstall. Step 5 is the ADR catching up to
  itself, not the reverse.
- **ADR-0004 needs nothing, and there is NO SCHEMA BUMP** — `has_part` is already
  `Required.Always` (`SlotSnapshotDto.cs:82`).
- The "nothing ever dissolves" premise blamed on ADR-0012 **is not in the ADR** —
  it is a stale xmldoc on `Vehicle.SwapPart`.
- **One consolidated ADR-0013 amendment only. Non-gating.**

### 6. View layer — THE CAR IS NOT A DROP TARGET (option A)

Clicks resolve by **alpha-mask silhouette** (`VehiclePartHitZone.cs:85-89`,
`_alphaHitThreshold 0.1`). The recorded option B hit-tests `CollectHitZones`
**bounding rects** — two different answers for the same pixel, plus a second copy
of the combat tier-ordering rule that `VehicleBarStack.cs:155` deliberately leaves
unwired in workbench mode.

If B is ever revived: `EventSystem.RaycastAll`, **never**
`RectangleContainsScreenPoint`, and exactly **one** y-flip at the CombatView
boundary (mirror `CombatHud.cs:973`).

Drag defers to slice 5b. Inline icons are fully usable alone, on one condition:
the icon handler must invoke the **same host command** a future drop would
(`OnInstallRequested` / `OnUninstallRequested`), so drag arrives as a second
producer of an existing command rather than a bridge.

### 7. Build order

`c1` ships **independently** and fixes a live combat-correctness defect whether or
not equip ever ships.

- **c1** — rebind + non-aliasing in `WithSourceSlot`; invert the planted tripwire
  at `RunDeckComposition_Test.cs:124` (`AreSame` → `AreNotSame`); correct the
  false `ToRuntime` xmldoc; stale-comment sweep in the same commit.
- **c2** — `SlotInstance.ClearPart` + `Vehicle.UninstallPart` +
  `RequireSlotMutable`.
- **c3** — `RunController` install/uninstall orchestrator (**not**
  `GarageViewModel` — that is a read projection) + resume round-trip test.

**SEEN-FAILING REQUIRED** on all three: the rebind test validated by reverting to
the reference copy; the armor test by removing the `ArmorContribution` zeroing;
the corrupt-row guard by feeding a snapshot with `hasPart: true, partId: ""`.

### 8. Out of scope — logged, do not widen

Card-reward attack cards (`RunController.cs:520`, `:603`) enter the deck
**unstamped**, keeping their authored `LaunchSlotId`. Once a slot can be emptied,
such a card is permanently dead (`IsSlotOnline:755` is false for `!HasPart`) — it
greys forever and can never be un-greyed. It **fails safe**, and the deck-reward
axis owns it. **Do NOT widen step 5.**

## User Rulings (2026-09-19, on re-open)

1. **Build order** — c1 ships alone first, as its own commit, with the tripwire
   inversion as the diff's centre of gravity. APPROVED.
2. **Armor clause** — duplicate per TD, two distinct messages. TD ruling UPHELD
   against its own invitation to challenge.

## Verification performed by me on re-open (2026-09-19)

Re-confirmed in source before touching anything, after the session loss:

| Claim | Status |
|---|---|
| `CardDefinition.cs:110-111` copies effects **by reference** | CONFIRMED |
| `CardDefinition.cs:99-105` xmldoc names the future rebind + the deep-copy trap | CONFIRMED |
| `RunDeckComposition_Test.cs:124` planted tripwire `Assert.AreSame(source.Effects[0], composed[0].Effects[0])` | CONFIRMED |
| `WeaponAttackEffect(string launchSlotId, int damage)` ctor exists, throws on empty | CONFIRMED (`CardEffect.cs:105-111`) |
| `CardDefinitionSO.cs:45-47` false hot-edit xmldoc | CONFIRMED |
| `WithSourceSlot` callers: 1 production (`RunDeck.cs:160`) + 4 test | CONFIRMED |
| `ClearPart` / `RequireSlotMutable` still 0 hits | CONFIRMED |

## Scope of THIS commit (c1)

1. `WithSourceSlot` rebinds `WeaponAttackEffect` to `sourceSlotId`; all other
   effects pass through by reference. Xmldoc rewritten to state the invariant.
2. Invert the tripwire at `RunDeckComposition_Test.cs:124`; strengthen
   `RunDeckSourceSlot_Test.WithSourceSlot_DoesNotMutateTheOriginal` to assert the
   rebind and the source's untouched `LaunchSlotId`.
3. Correct the false `ToRuntime` xmldoc (`CardDefinitionSO.cs:45-47`) — the claim
   that a hot-edit "won't retro-mutate live combat state" is false, and false in
   builds too, since the SO is process-global. **That false claim is the license
   for the defect.**
4. Stale-comment sweep, same commit: `Vehicle.SwapPart` xmldoc,
   `RestoreFromSnapshot` comment, `Compose_SkipsEmptySlots` comment.

No behaviour outside deck composition changes. No ADR gating. No schema bump.

# TD Verdict — Chopshop Step 5 c2 (ClearPart / UninstallPart / RequireSlotMutable)

**Date:** 2026-09-19
**Verdict:** **AMEND**
**Provenance:** FRESH consultation. Unlike its sibling
`2026-09-19-chopshop-step5-model.md` (a transcription — see that file's
provenance section), this verdict was obtained from a live `technical-director`
read commissioned specifically to CHALLENGE the transcription rather than defer
to it. It was given the three c2 rulings as claims, not as authority.

**Files at risk:** `Assets/Scripts/Combat/Vehicle.cs`,
`Assets/Scripts/Combat/SlotInstance.cs`,
`Assets/Scripts/Combat/UninstalledPart.cs` (NEW),
`Assets/Scripts/Save/Dtos/VehicleStateDto.cs` (stale doc only),
`Assets/Tests/EditMode/Combat/VehicleSwapPartTests.cs`,
`Assets/Tests/EditMode/Combat/VehicleRestoreSlotStateTests.cs`

**ADRs:** ADR-0011 (#2 duplicate knowledge, #4 vestigial), ADR-0012
(sum-of-parts armor), ADR-0004 (save/persistence)

## Technical Director Review

### Ruling 1 — uninstall-only structural gate: CONFIRMED, and understated

The boot-break chain holds link by link: `Vehicle_Scout.asset:26` lists `hull_0`
in `_parts` → `VehicleDefinitionSO.cs:110` installs through the **gated**
`IPartData` overload (no branch, no int fallback) → `SmallFrame.asset:48-51`
marks `hull_0` `IsStructural: 1`.

**On "is the real smell that structural parts are in `_parts` at all?" — No.**
`VehicleDefinitionSO.OnValidate:178-189` *requires* an entry for every structural
non-Armor slot and logs "vehicle is born dead (structural slot Offline at build)"
when one is missing. Structural-in-`_parts` is an enforced authoring invariant,
not drift. Removing it would leave hull HP un-authored — a worse shape.

**AMEND — the transcription justified the uninstall-side structural rejection
only by symmetry / "no outgoing record". Two harder reasons exist:**

- `Vehicle.StructuralHp:216-225` sums `Hp` over structural slots with **no
  `HasPart` guard**. `ClearPart` zeroing `Hp` on `hull_0` makes `IsDead` true
  immediately (`:494`). Un-equipping your hull kills you on the spot.
- `CombatLoop.RequireFrameInstalled:899-907` throws `InvalidCombatantException`
  on a vehicle with no installed structural slot — the next beacon is
  unenterable.

Put those in the `RequireSlotMutable` message. "No outgoing record" is the
weakest of the three and the only one a future editor could argue away.

### Ruling 2 — duplicated Armor clause: outcome UPHELD, **rationale REJECTED**

The recorded rationale ("two independent rules free to diverge later") is
fictional. Both clauses test `slot.Kind == SlotKind.Armor` and both derive from
ONE fact: `armor_0` is a derived buffer, not a part socket (ADR-0012 Decision 3,
implemented at `Vehicle.cs:453-482` + `SlotInstance.InitializeAsBuffer:190-200`).
Install-allowed-on-Armor destroys sum-of-parts derivation; uninstall-allowed-on-
Armor removes a buffer no part occupies. Neither is reachable without rewriting
ADR-0012 — and that moves both clauses together. There is no world where one
moves alone.

**The outcome survives on different grounds:** extraction costs more than it
buys. A shared `RequireNotArmorBuffer(slot, verb)` reintroduces the branch as a
parameter and trades two specific messages for one generic one. Two literal `if`s
over one enum value is not ADR-0011 #2 — #2 targets parallel storage of STATE,
not two readers of the same immutable layout field. `SlotKind.Armor` is stored
once, in the layout.

**AMEND — add the structural tie the duplication lacks.** The risk is not the two
`if`s; it is that nothing links them, so an edit to one is invisible to the
other. The tie is a test, not an abstraction: one EditMode method asserting BOTH
verbs reject `armor_0`. Grep `SlotKind.Armor` and you find the pair.

### Ruling 3 — both defects CONFIRMED, both need amendment

**(a) Armor leak — `ClearPart` zeroing is NECESSARY BUT NOT SUFFICIENT.**
`ComputeArmorSum:473-482` skips only `Offline`; `SlotInstance.DamageState:156`
returns `Empty` when `!HasPart`. Survives save/resume at two sites the
transcription did not cite: `SlotSnapshotDto.From:97` projects
`ArmorContribution` verbatim and `RestoreFromSnapshot:227` writes it back
verbatim (clamped only at `< 0`); `RestoreSlotState:707-716` never recomputes.

**Also add `if (!inst.HasPart) continue;` to `ComputeArmorSum`**, because:

1. `SlotInstance`'s own class doc (`:16-22`) assigns the decision to the
   aggregator — "whether the contribution counts is decided by the aggregator
   (Offline slots excluded)". The aggregator knows one of the two
   non-contributing states and is blind to the other. Completing it is its
   charter, not a second copy.
2. **`ClearPart` cannot cover the restore path at all** — restore bypasses it. A
   `hasPart:false, armor_contribution:N` row round-trips into a live leak no
   writer-side fix can reach.
3. Zero regression risk: the only writer of a non-zero `ArmorContribution` is
   `SlotInstance.InstallPart:141`, which always sets `HasPart = true`.

Keep the `ClearPart` zeroing too — it keeps the WIRE clean. Writer normalizes,
reader enforces. That is a normalize/validate pair, not duplication.

**(b) Silent dissolution — confirmed; today it would be a TORN TRANSACTION, not
silence.** `PartInstance`'s ctor throws on null data (`PartInstance.cs:37-39`),
so `RunInventory.Add(null)` throws AFTER the slot was already cleared: part gone,
slot empty, misleading exception. Throw-before-mutation is raised from "should"
to **must**.

**AMEND — the message must not claim a single cause.**
`Vehicle.InstallPart(string,int,int,string,string):424-434` leaves
`InstalledPart` null whenever `partId` is null — every enemy archetype and most
test fixtures. `(HasPart && InstalledPart == null)` is therefore ALSO the
identity-less int-install state. Name both origins.

**c3 contract point, decided now:** `GarageViewModel.BuildSlotRow:237-254` renders
such a slot as occupied, so the button WILL be live and the throw WILL fire on a
real click. Do NOT add a `CanUninstall` predicate — that is a second copy of the
gate. c3 catches into a player-facing refusal. One gate, one catch.

### MISS 1 (blocking, decides c2's signature) — un-equip → re-equip is a FREE FULL REPAIR

`SlotInstance.InstallPart:138-142` sets `Hp = maxHp`. `SwapPart` throws on an
empty slot (`:328-331`), so the only re-equip path into a slot c2 just emptied is
`InstallPart(string, IPartData)` — which full-heals. `RunInventory` cannot carry
the damage: `PartInstance` holds only `Data` + `InstanceId`
(`PartInstance.cs:30-45`), `PartInstanceDto` only `instance_id` + `part`
(`RunInventoryDto.cs:18-24`). `CorrodedStacks` is zeroed by `ClearPart` and never
restored.

This defeats the rule `SwapPart` exists to enforce (`Vehicle.cs:274-280`, locked
2026-07-05): *"Ratio preservation closes the swap-to-repair exploit — Rest and
Repair stay the only healing verbs."* c2+c3 reopens it by a different door, in
the screen that sells paid Welding Repair.

**Why it lands on c2:** c2 chooses the return type. Bare `IPartData` makes the
later fix a signature change across every caller plus a `RunInventoryDto` schema
bump. Ship the 1.0 shape now — a `readonly struct UninstalledPart { Data, Hp,
CorrodedStacks }` captured BEFORE `ClearPart`. c3 may ignore `Hp`/`CorrodedStacks`
for now: unread DATA on a payload is not an ADR-0011 stub return (which forbids
unreachable PATHS).

The rule question itself — "is free repair acceptable for 1.0?" — is a design
call, not TD's.

### MISS 2 (log; blocks the INSTALL verb, not c2) — post-resume install grants zero cards

`PartData.GrantedCards` is hardcoded empty (`PartData.cs:66`) and `PartDataDto`
(`:25-47`) carries no card list, so EVERY rehydrated part grants nothing. The
REMOVAL direction is safe (`RunDeck.RemoveBySourceSlot:88-98` keys on
`SourceSlotId`, which rides the wire via `CardDefinitionDto:59`). The INSTALL
direction is not: equip any part after a resume and the deck gains zero cards — a
weapon that fires nothing, permanently, invisibly.

`PartData.cs:38-42` names the WRONG direction as the risk ("swapping a weapon OUT
after a resume would remove zero cards"); that one was fixed by the stamping seam.
The live hole is IN. Fix belongs in the save DTO or an install-time
recomposition. **Blocking prerequisite of the equip gesture.**

### MISS 3 (log, non-gating) — mid-combat clear leaves a dead hit zone

Not reachable (Garage is a Chopshop-beacon modal), and `Vehicle` cannot see
`CombatLoop`, so the phase gate belongs to c3's orchestrator, not c2. If it ever
fires: `VehicleBarStack.BindHitZone:961-976` binds zones once and `SetActive(false)`s
only for `!HasPart` AT BIND TIME, so a live zone stays aimed at an emptied slot;
`IsSlotAttackable` then rejects at play. Cosmetic, no corruption.

### MISS 4 (pre-existing, out of scope) — `MaxHpOverride` is vestigial

`SlotDefinition.MaxHpOverride:43` is validated in `FrameLayoutSO.OnValidate:54-160`
and read by NOTHING at runtime. `SmallFrame.asset:59` authors `armor_0:
MaxHpOverride: 20`, a number that does nothing since ADR-0012 made armor derived.
ADR-0011 #4. Debt register.

### Interaction sweep (answers to the specific asks)

- **Readers of `HasPart`:** ~25 sites, all read-and-skip, none mutate, all degrade
  to "empty" correctly. The only readers with NO `HasPart` guard are
  `StructuralHp`/`StructuralMaxHp` (`Vehicle.cs:216-241`) and
  `MaxArmor`/`CurrentArmor` (`:59-84`) — all four made safe by the structural and
  Armor rejection clauses. A second independent reason both clauses are
  load-bearing.
- **Assumes `InstalledPart` non-null when `HasPart`:** nothing in production.
  `RunDeck.cs:150` and `GarageViewModel.cs:238` both null-guard. `UninstallPart`
  would be the first — which is why it must throw.
- **Other `ComputeArmorSum` callers:** exactly two, both in `Vehicle` —
  `RecomputeArmorPool:457`, `FillArmorPool:470`. `RecomputeArmorPool` has four
  call sites (`InstallPart:269`, `SwapPart:346`, `RepairSlot:682`,
  `DamagePipeline:197,208`). None assumes an emptied slot contributes.
- **Can the save path persist a half-cleared slot:** **YES** —
  `SlotSnapshotDto.From:97` → `RestoreFromSnapshot:227`, verbatim both ways, no
  cross-field validation on either side. Strongest argument for the aggregator
  guard.

### Mutation ordering (ordering matters at Vehicle level, not inside ClearPart)

`Vehicle.UninstallPart(string slotId)`:

1. null `slotId` → `ArgumentNullException`; unknown id → `ArgumentException`
   (mirror `SwapPart:324-327`).
2. `RequireSlotMutable(slot)` — ALL throws here, before any write. Clause order:
   **Armor → structural → (HasPart && InstalledPart == null) → !HasPart.** Order
   is for message quality: a structural slot with a corrupt record should report
   *structural*, the permanent rule, not the transient one.
3. **Capture the payload** (`InstalledPart`, `Hp`, `CorrodedStacks`) BEFORE step 4
   nulls them.
4. `slot.ClearPart();`
5. `RecomputeArmorPool();` — MUST follow 4. It reads `ComputeArmorSum` over live
   slot state; running it first re-derives the pre-clear sum and the contribution
   never drops.
6. return the payload.

Inside `ClearPart`, field order is irrelevant, but **all six writes live in one
method** (`HasPart=false, MaxHp=0, Hp=0, ArmorContribution=0, CorrodedStacks=0,
InstalledPart=null`), mirroring the ctor (`:100-105`). Never a public two-step: a
`DamageState` read between a `HasPart` flip and the zeroing would see `Empty` over
residual `MaxHp`. Mark `internal` like `RestoreFromSnapshot:220`, same stated
reason. `RequireSlotMutable` is `private static`, mirroring `RequireInstallable:367`.

### Test probes — five, not two

1. **Armor leak, writer side** (planned). Install contributing part → `UninstallPart`
   → `MaxArmor` drops. *Seen-failing: remove the `ArmorContribution` zeroing.*
2. **Corrupt-row guard** (planned). Restore `hasPart:true, partId:""` → `UninstallPart`
   throws **and the slot is UNCHANGED afterward**. A bare `Assert.Throws` does not
   test throw-before-mutation.
3. **Armor leak, READER side — NEW.** Restore `armorContribution:5, hasPart:false`
   → `MaxArmor` excludes it. Fails today AND fails with the `ClearPart` fix alone,
   because restore never routes through `ClearPart`. Justifies the
   `ComputeArmorSum` amendment. Home: `VehicleRestoreSlotStateTests.cs`.
4. **Paired Armor rejection — NEW.** `Armor_IsRejectedBy_BothInstallAndUninstall`,
   one method, both asserts. This is the tie standing in for the rejected
   extraction. **Passes on arrival — it is a tie, not a regression probe.**
5. **Identity-less int install refuses un-equip — NEW.** Build via
   `TestVehicleFactory.MakeFullCombatant` (int path, `partId: null`) →
   `UninstallPart("weapon_0")` throws. Pins the guard's SECOND origin and stops
   someone "fixing" the throw into a null return when an enemy fixture trips it.

Seen-failing attestation required for 1, 2, 3, 5 individually.

**Gate condition:** no APPROVE-to-merge until EditMode is attested green BY NAME
AND COUNT post-change (baseline 1299/1298/0/1skip) with all five added.
Compilation-green is not semantic-green.

### Three-lens self-audit (TD's own, unprompted)

**Health.** `UninstallPart`/`ClearPart`/`RequireSlotMutable` grepped — zero hits
repo-wide, no bridge introduced. Two pre-existing drifts surfaced and deliberately
NOT folded in: `MaxHpOverride` (MISS 4) and the stale swap-only claim at
`VehicleStateDto.cs:27-28` — same stale-doc family c1 swept, missed because that
sweep scoped to Combat. **Sweep it in c2's commit.** `Vehicle` is the right owner
(already owns Install/Swap/Repair/RestoreSlotState, only type that can reach
`armor_0`). No teardown races: pure POCO, no coroutines, no Unity lifecycle — the
subscription-lifecycle rules do not apply to c2. They re-enter at c3.

**Optimization.** `ComputeArmorSum` is O(N) over ~6 slots on every damage tick;
one bool check inside an existing loop is free, no new allocation. `readonly
struct` returned by value is the zero-alloc payload choice. Explicitly NOT caching
the armor sum — recompute-on-mutation is cheap and correct, and a cache would be
exactly the parallel storage ADR-0011 #2 forbids.

**1.0 survival.** The payload struct is the whole point of MISS 1; bare
`IPartData` is throwaway scaffolding. Anticipated subscribers: c3's orchestrator
(`Data`), the repair-economy fix (`Hp`, `CorrodedStacks`), parts progression
(extends without touching callers). No stopgaps in c2 — no placeholder USS, no
temp affordance, no transitional path. The one thing a later slice may reshape is
the corrupt-row exception MESSAGE, once the deferred catalog slice makes that case
recoverable; cheap, no signature impact.

**Audit delta:** the audit moved `UninstalledPart` from nice-to-have to the single
reason this is AMEND rather than ACCEPT. If c2 ships bare `IPartData`, the
free-repair fix acquires a signature change it does not need — the one place this
verdict would lock in a bad default.

## Independent verification by me (not taken on faith)

TD claims are evidence, not authority. Re-checked in source before implementing:

| Claim | Site | Result |
|---|---|---|
| Re-equip full-heals (`Hp = maxHp`) | `SlotInstance.cs:138-141` | CONFIRMED |
| `SwapPart` exists to close the swap-to-repair exploit | `Vehicle.cs:273-279` | CONFIRMED (locked 2026-07-05) |
| `PartInstance` carries only `Data` + `InstanceId` | `PartInstance.cs:31-32` | CONFIRMED |
| `StructuralHp` has no `HasPart` guard | `Vehicle.cs:217-225` | CONFIRMED |
| `PartData.GrantedCards` hardcoded empty | `PartData.cs:66` | CONFIRMED |
| Int-install leaves `InstalledPart` null when `partId` null | `Vehicle.cs:427-430` | CONFIRMED — its own xmldoc says so |
| Structural-in-`_parts` is an enforced invariant | `VehicleDefinitionSO.OnValidate:178-189` | CONFIRMED |

## User Rulings (2026-09-19)

1. **Return shape** — `UninstalledPart` readonly struct. APPROVED.
2. **Free repair via un-equip/re-equip** — DEFER. Ship the payload so damage
   survives the uninstall; c3 may ignore it for now. The hole stays unreachable
   until the gesture lands, and closing it later is a consumer-side change only.
   Logged as a blocking prerequisite of the equip gesture.
3. Earlier same-day: Armor clause stays duplicated (upheld); c1 ships alone
   (done — Unity `7e8eb0c`).

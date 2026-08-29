# Capture — Weapons grant their own cards

**Date:** 2026-08-29
**System:** `IPartData` / `PartDefinitionSO` / `VehicleDefinitionSO` / `RunDeck` composition
**Status:** IMPLEMENTED + VERIFIED 2026-08-29 — see §8.

---

## 0. Files touched

| File | Change |
|---|---|
| `Assets/Scripts/Combat/IPartData.cs` | + `IReadOnlyList<CardDefinition> GrantedCards` |
| `Assets/Scripts/Combat/PartData.cs` | implements `GrantedCards` as empty — see §5 |
| `Assets/Scripts/Combat/CardDefinition.cs` | read-only (composition consumes it) |
| `Assets/Scripts/CombatView/Data/PartDefinitionSO.cs` | + `_grantedCards` (`List<CardDefinitionSO>`) + `Configure` overload |
| `Assets/Scripts/CombatView/Data/VehicleDefinitionSO.cs` | + `_chassisCards` (`List<CardDefinitionSO>`) + `ChassisCards` |
| `Assets/Scripts/Run/RunDeck.cs` | + `ComposeStarting(...)`; `Milestone1Starter()` narrows to the chassis set |
| `Assets/Scripts/CombatView/RunSceneHost.cs` | `BeginNewRun` composes instead of hardcoding |
| `Assets/Editor/CombatDataInitializer.cs` | always-run pass wiring part→cards and chassis→cards |
| `Assets/Tests/EditMode/Run/RunDeckComposition_Test.cs` | NEW |

**ADRs at risk:** ADR-0011 (this REMOVES a #2 parallel-storage violation).
ADR-0002 (`GrantedCards` is `CardDefinition`, which lives in the engine-free
Combat assembly — no Unity type crosses). ADR-0015 (composition is driven by SO
data tables, not code branches). ADR-0013 (`RunDeck` stays the run-scoped
collection; `Combat.Deck` shuffle engine untouched). **No DTO change, no schema
bump.**

## 1. Why — this deletes a duplication, it does not add a system

`RunDeck.Milestone1Starter()` hardcodes:

- 3× **BulletBarrage** → `WeaponAttackEffect("weapon_0")` → the slot `BuildScout`
  and `Vehicle_Scout.asset` fill with **`scout_machine_gun`**
- 2× **FlameBurst** → `WeaponAttackEffect("weapon_1")` → **`scout_flamethrower`**

**The starter deck already mirrors the installed weapons.** It is duplicated by
hand, in a different assembly, with nothing keeping the two in step — change the
Scout's weapon and the deck silently keeps firing the old one's cards. Same
parallel-storage shape as the card-playability predicate fixed earlier today
(`2026-08-29-card-playability-predicate-unification.md`).

After this change, a weapon's cards exist in exactly one place: the part asset.

## 2. The split (director, 2026-08-29)

| Source | Cards |
|---|---|
| `scout_machine_gun` | 3× BulletBarrage |
| `scout_flamethrower` | 2× FlameBurst + **1× Flame Barrier** |
| chassis (`Vehicle_Scout`) | 3× Weld, Patch, Draw, Handbrake, Overtake |

3 + 3 + 7 = **13**, identical to today's `Milestone1Starter()`.

Flame Barrier is weapon-granted by explicit director call — swapping the
flamethrower out takes it with you.

## 3. Data already exists

All 8 card SOs are authored (`Card_BulletBarrage`, `Card_FlameBurst`,
`Card_FlameBarrier`, `Card_Weld`, `Card_Patch`, `Card_Draw`, `Card_Handbrake`,
`Card_Overtake`) and all 5 Scout part SOs. `VehicleDefinitionSO.BuildVehicle`
already installs via the `IPartData` overload, and `_playerVehicleAsset` is
wired by the author — **the SO path is the live production path**; `BuildScout`
is the headless fallback only. So this is filling two lists, not creating a
pipeline.

## 4. Sizing — counted before committing (feedback_count_real_consumers)

**61 references to `Milestone1Starter` across 33 files.** Breakdown:

- **28** are `new RunDeck(RunDeck.Milestone1Starter())` fixture noise — keep
  compiling, just get a smaller deck.
- **4** count assertions compare against `Milestone1Starter().Count` — they are
  SELF-RELATIVE and survive the narrowing.
- **~6 test files** drive combat off that deck and genuinely need attack cards;
  they move to the composed deck.
- `RunDeck_Test` asserts BulletBarrage→`weapon_0` / FlameBurst→`weapon_1`. Those
  assertions **relocate** to a part-asset test — which is where they belonged.

## 5. Named gap — rehydrated parts carry NO cards

After a resume, `SlotInstance.InstalledPart` rehydrates as engine-free
`PartData` built from `PartId` + display name; the SO reference is gone and the
wire format carries no card list. `PartData.GrantedCards` therefore returns
**empty**, and that is correct-by-construction rather than a stub: a rehydrated
part genuinely has no card list today.

**This slice is unaffected** — the deck is composed once at run start and
persisted thereafter via `RunDeckDto`, so nothing re-derives it on resume.

**Slice 5 (equip verb) MUST solve it**, or swapping a weapon after a resume
removes zero cards. The fix that fits the existing rules is a
`PartId → PartDefinitionSO` catalog, mirroring what `PartIconAtlas` already
does for sprites, and honouring the standing rule to key off `PartId` and never
off the concrete type.

## 6. Two steps, one slice

**A.** Add the field, wire the assets, and assert *composed == `Milestone1Starter()`
exactly*. Zero call-site churn; proves the authored data is right.

**B.** Flip `RunSceneHost` to compose, narrow `Milestone1Starter` to the chassis
set, move the ~6 combat-driving test files.

**A is NOT an acceptable stopping point.** The project has already been burned
by this: `ScoutFallback_PartIdParity_Test` is recorded as having
*"institutionalised the duplication rather than removing it."* A exists only to
de-risk B, and B lands in the same slice.

## 7. Authoring caveat

`CombatDataInitializer.Configure` fires **only for freshly-created assets**, so
re-running the menu will not add card lists to the existing `Part_Scout_*`
assets. The wiring pass must therefore be **always-run and write only the new
field**, leaving every designer-tuned value alone. Menu:
`Tools > Wasteland Run > Generate Default Combat Data`.

---

## 8. Outcome — verified 2026-08-29

**EditMode 1184 / 1182 / 0 failed / 2 skipped** (baseline 1172; +12 = 7
composition tests + 5 asset tests). **PlayMode 3/3.** 0 `error CS`.

Assets authored headlessly via
`Unity.exe -batchmode -executeMethod WastelandRun.CombatView.Editor.CombatDataInitializer.GenerateAll`
— **the namespace is `WastelandRun.CombatView.Editor`, not `WastelandRun.EditorTools`**
(first attempt failed on the guessed namespace). Wiring confirmed by reading the
asset YAML: MachineGun carries the same card GUID three times, Flamethrower
carries 2+1, Vehicle_Scout carries 7.

`ComposedScoutDeck_IsThirteenCards_MatchingThePreChangeStarter` is the claim the
whole slice rests on and it passes: 7 chassis + 3 + 3 = **13**, with per-name
counts matching the old hardcoded list exactly. A fresh run is dealt the same
opening deck it was before the refactor.

### 8a. Sizing was wrong in the safe direction

Predicted ~6 combat-driving test files would break. **Two tests broke, total.**
The combat-driving fixtures pass explicit `CardDefinition`s to `PlayCard` rather
than drawing from the starter, so a smaller starter never reached them. Worth
remembering before quoting churn estimates from call-site counts again: 61
references, 2 breakages.

### 8b. A fixture went silently vacuous — caught, not shipped

`RunDeck_Test.Milestone1Starter_AttackCards_BindLayoutSlotIds` walked the deck
asserting *"if you find BulletBarrage, it binds weapon_0"*. Once the weapon
cards moved out, that loop found nothing and **the test passed while asserting
nothing** — it was not in the 2-failure list precisely because it had stopped
testing. Exactly the class the ADR-0007 five-envelope-fixture lesson warns
about.

Replaced with two live assertions:
- `Milestone1Starter_ContainsNoWeaponCards` — the invariant that actually
  belongs to the chassis list.
- `PartAssetGrantedCards_Test` — the slot-binding claim relocated onto the
  shipped assets, where the cards now live.

**Generalisable:** when data moves between two places, a fixture that iterates
the OLD place with a conditional assertion does not fail — it goes quiet. Grep
for `if (…) Assert` shapes in any test that reads a collection you just emptied.

### 8c. Consumer count missed test-only implementers

The `IPartData` implementer grep was scoped to `Scripts/`, so five test-local
`FakePart` doubles were missed and surfaced as compile errors in waves (the
compiler reports per-assembly). Swept repo-wide on the second pass.
**Interface-member additions must grep `Tests/` too.**

### 8c-bis. Review pass — run AFTER commit, which was the wrong order (`3d168bc`)

`feedback_three_review_gates` puts adversarial diff review BEFORE commit. It was
skipped on `f12148b` and run only when the user asked "do we need a pass?".
**It found three things, one suite-breaking — so the gate earns its place.**

1. **The "fresh instance" test was tautological.** It handed the fake part two
   already-distinct `CardDefinition`s and asserted they were distinct. Blind to
   the only real failure mode: `PartDefinitionSO` listing the SAME asset three
   times and returning one shared object. **Shipped inside the very commit whose
   message warns about vacuous fixtures** — writing the lesson down is not the
   same as applying it. Replaced by a composition test (concatenates, does not
   clone) plus `GrantedCards_MintFreshInstancesPerAccess` against the shipped
   asset.
2. **An attack-less deck is an unwinnable run and nothing checked for it.** The
   first guard errored unconditionally and **failed 20 EditMode tests**, because
   those run the BuildScout fallback where an attack-less deck is the expected
   headless state. Severity is now split: wired asset + no attacks → LogError
   (authoring drift); unwired → LogWarning (known fallback). An error on a
   normal state trains people to ignore the channel.
3. **Dead defensiveness** — `slot?.` in `ComposeStarting` cannot be false
   (`GetSlotById` throws; the ctor instantiates every layout slot). ADR-0011.

Final: **EditMode 1185 / 1183 / 0 / 2**, PlayMode 3/3.

### 8d. Deltas from the plan

- `Milestone1Starter()` **kept its name**. Renaming would have churned 61 call
  sites for no behavioural gain; its doc now states plainly that it is the
  chassis set and the code fallback, with an explicit "do not re-add weapon
  cards here".
- Engine / Wheels / Frame get an **explicit empty** `SetGrantedCards()` rather
  than being left unset, so an empty list reads as authored intent rather than
  an authoring omission.

---

## Technical Director Review

> Carried forward from the 2026-08-29 four-agent pass, whose ADR-0011 ruling
> applies directly: a rule stored in two hand-maintained places is forbidden
> pattern #2, and the fix is to give it one home rather than to add a test that
> keeps the copies honest.

The same reasoning that condemned the duplicated playability predicate condemns
the duplicated weapon-card list, and for the same reason: **nothing keeps the
copies in step, and the failure is silent.** A weapon whose cards change in the
part asset while `Milestone1Starter` keeps the old list produces a deck that
fires a weapon the vehicle no longer carries — green suite, wrong game.

Adopted rulings:
- Composition belongs in the **model** (`RunDeck.ComposeStarting`), engine-free
  and directly testable, not in `RunSceneHost` where only a PlayMode test could
  reach it.
- `GrantedCards` is typed `IReadOnlyList<CardDefinition>`, not
  `CardDefinitionSO[]`, so `IPartData` stays engine-free per ADR-0002.
- The parity-test-instead-of-fix pattern is explicitly rejected (§6).

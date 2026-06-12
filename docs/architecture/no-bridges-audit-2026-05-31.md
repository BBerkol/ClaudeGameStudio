# No-Bridges Architectural Audit — 2026-05-31

**Authority:** ADR-0011 (No-Bridges Architectural Rule, Project-Wide, Accepted 2026-05-31).

**Scope:** Both repos — framework (`C:\ClaudeCreations\Madmax Roguelike\`) and Unity project (`C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\`). Every accepted ADR (ADR-0001 through ADR-0008) is reviewed.

**Method:** Forbidden-pattern grep sweep across both repos for the 8 patterns in ADR-0011 §Forbidden patterns at done state, then per-system severity assessment.

**Output format per system:** Severity · Findings · Recommended retirement slice (or N/A).

## Severity Rubric

| Severity | Definition |
|----------|------------|
| **BLOCKING** | Parallel storage of live gameplay state OR adapter surface >50 callers. Cannot declare any milestone done without retirement. B1-class drift risk. |
| **HIGH** | Bridge surface present, <50 callers, OR ADR contradiction (an accepted ADR's design is not yet honoured and any future implementation will create parallel paths). |
| **MEDIUM** | Bimodal branches with <50 callers, or one-off bridge fields, or transitional comments without underlying code violation. |
| **LOW** | Stale comments, vestigial enum members with no live readers, single-file cleanup. |
| **N/A** | Either system not implemented yet (re-audit when it lands) or already on a single clean path. |

## Audit summary

| # | System | ADR | Severity | Retirement |
|---|--------|-----|----------|------------|
| 1 | Combat slot system (slotId vs LegacySlotKind) | ADR-0007 + ADR-0009 + Amendment | **BLOCKING** | ADR-0010 (plan parked) |
| 2 | VehicleDefinitionSO legacy SlotSpec fields | ADR-0001 (proposed) | **BLOCKING** | Add to ADR-0010 Phase 1/2 scope |
| 3 | Card system | ADR-0002 | **MEDIUM (provisional)** | Dedicated deep-audit pass before declared done |
| 4 | Save & persistence | ADR-0004 | N/A | Not implemented yet — re-audit when implemented |
| 5 | Deterministic RNG | ADR-0003 | **LOW** | `DamagePopupWidget.cs:77` cosmetic `Random.Range` cleanup (optional) |
| 6 | Addressables — Resources fallback contradiction | ADR-0008 | **HIGH** | When ADR-0008 implementation begins: retire `Resources.Load` for in-scope assets in the same slice (no parallel runtime paths) |
| 7 | UI framework (UGUI vs UI Toolkit) | none | **LOW** | Confirm UGUI-only is intentional; if UI Toolkit is later adopted, that adoption must retire UGUI in scope |
| 8 | Audio system | none | N/A | Not implemented yet |
| 9 | Input handling | none | N/A | New Input System only |
| 10 | Vehicle variant chain | none | N/A | Plan abandoned 2026-05-13 (memory `project_vehicle_variant_chain.md`) |
| 11 | Framework engine-free POCO `src/` | none | N/A | Clean — `UnityEvent` only appears in a forbid-doc comment (`IVehicleView.cs:125`) |

---

## 1. Combat slot system (slotId vs LegacySlotKind)

**Severity:** BLOCKING

**ADRs:** ADR-0007 (Accepted 2026-05-19, modern slot vocabulary), ADR-0009 (Accepted 2026-05-28, phased bridge migration), ADR-0009 Amendment (2026-05-30, bridge classified permanent — architectural posture **reversed by ADR-0011**).

**Forbidden patterns present:**

| ADR-0011 pattern | Evidence | Surface |
|---|---|---|
| Adapter layer | `SlotDefinition.LegacyKindBridge` field, archetypes assign `legacyKindBridge:` | 26 assignments / 7 files (archetype layouts) |
| Parallel storage | `Vehicle._maxArmor` + `Vehicle._currentArmor` legacy pool AND `SlotKind.Armor` slot HP | `Vehicle.cs` legacy fields + `armor_0` per-archetype |
| Bimodal code paths | `IsLayoutMode` branches in `Vehicle`, `CombatLoop`, `DamagePipeline`, layouts | 20 occurrences / 8 files |
| Vestigial enum | `LegacySlotKind.cs` (MachineGun/Flamethrower/Engine/Wheels/Frame/Exposable1/Exposable2/Javelin) | 1,096 occurrences / 72 files (drift +15 since 2026-05-31 morning survey) |
| Backwards-compat ctor | `Vehicle(string name)` legacy ctor + `Vehicle(string name, IFrameLayout layout)` modern | `Vehicle.cs:1` legacy ctor + ~44 test sites still construct legacy |
| Stub return | `Slot.ArmorContribution => _bridge != null ? 0 : _armorContribution` | `Slot.cs:90` |
| "Transitional"/"bridge" comments | Pervasive in archetype layouts, `LegacySlotKind.cs`, `Slot.cs`, `SlotDefinition.cs`, `CombatController.cs` | 15+ files |
| Duplicate enums for same axis | `LegacySlotKind` vs `SlotKind` both describe slot identity | 1 file vs 1 file |

**Concrete harm already observed:** B1 bug — `MainBarWidget` displayed 0 armor for Dredge because the widget read `Vehicle.EffectiveMaxArmor` (legacy pool) while the actual 60hp lived on `armor_0.Hp`. Direct symptom of parallel storage drift.

**Retirement scope:** Owned by **ADR-0010** (combat slot retirement). Six-phase plan is on disk at `production/adr-0010-phase-plan.md`, parked pending this audit + ADR-0011 landing. Both preconditions now satisfied; ADR-0010 is unblocked.

**Action:** None this audit. ADR-0010 Phase 0 begins as task #18 once #17 closes.

---

## 2. VehicleDefinitionSO legacy SlotSpec fields  *(NEW FINDING)*

**Severity:** BLOCKING

**ADRs:** ADR-0001 (Visual vehicle part system — Proposed, gates vehicle art production).

**Location:** `Assets/Scripts/CombatView/Data/VehicleDefinitionSO.cs:23-77`.

**Forbidden patterns present:**

| ADR-0011 pattern | Evidence |
|---|---|
| Parallel storage of vehicle definition | Five hardcoded `SlotSpec` fields keyed by the legacy 5-shape vocabulary: `_machineGun`, `_flamethrower`, `_engine`, `_wheels`, `_frame` (lines 39-43) |
| Adapter (data-side) | `BuildVehicle()` maps these legacy-named SO fields onto modern slotIds (`weapon_front`, `weapon_back`, `engine_0`, `mobility_0`, `hull_0`) at lines 57-61 |
| Stub field | `SlotSpec.ArmorContribution` is serialized but documented "IGNORED at build time" (line 32, line 14-19) |
| "Transitional"/"for now" comments | Lines 29-31, 14-19 |

**Bridged data on disk:** `Assets/Resources/combat/Vehicles/Vehicle_Scout.asset` carries `ArmorContribution` values across 5 slots — these are dead bytes in the serialized asset, kept only so deserialization doesn't break.

**Concrete harm:** Designer-facing Inspector still presents the legacy 5-shape vocabulary. Any future enemy or new vehicle archetype that doesn't match the 5-shape gets shoehorned into the SO or skips it entirely (which is why archetype enemies use `IFrameLayout` directly instead of `VehicleDefinitionSO` — already a bridge symptom).

**Retirement scope:** Belongs to **ADR-0010 Phase 1 / Phase 2** (add to scope as an amendment). Recommended add to parked plan:

> **Phase 1 addendum** — VehicleDefinitionSO redesign: replace hardcoded 5-shape SlotSpec fields with a `slotId → MaxHp` list shape OR fold authoring into `IFrameLayout`-driven SO. Drop `ArmorContribution` field outright. Designer Inspector binds dynamically over `IFrameLayout.Slots`.
>
> **Phase 2 addendum** — One-shot `Vehicle_Scout.asset` migrator: read legacy 5-field shape, write new shape, delete migrator in same slice (per ADR-0011 §Allowed exception #1).

**Action:** Amend `production/adr-0010-phase-plan.md` to fold VehicleDefinitionSO into Phase 1/2 scope before Phase 0 (ADR-0010 authoring) begins.

---

## 3. Card system (ADR-0002)

**Severity:** MEDIUM (provisional — full deep-audit not yet performed)

**ADRs:** ADR-0002 (Accepted — POCO state model, engine-free assembly, deterministic seeding, exception-based validation).

**Provisional findings from grep sweep:**

- `CardDefinitionSO`, `CardPlayResult`, `CardDefinition`, `IntentPool`, `WeightModifier`, `EnemyIntent`, `EnemyTurnResult` all reference `LegacySlotKind` — this is **inherited from the combat slot bridge**, not a separate card-system bridge. Retiring under ADR-0010 sweeps this.
- `Assets/Resources/combat/...` has card SO data on disk (`CardWidget.cs` loads `Resources.LoadAll<Sprite>`, `CardRewardPicker.cs` loads `Resources.Load<CardWidget>`). Bridges to Addressables once ADR-0008 implementation begins — covered under finding #6.

**Possible deeper bridges not yet inspected:**
- Card targeting vocabulary (CardTarget enum, `SlotKind` filters in card definitions) — needs deeper read to confirm single-vocabulary at done.
- Card effect SO shapes — ADR-0002 mentions DamageEffectSO authority; potential historical effect-SO variants?
- DamagePipeline single path vs split for damage-from-card vs damage-from-intent.

**Retirement scope:** Schedule **dedicated card-system deep audit** after ADR-0010 closes (combat-slot retirement removes the noise floor and lets card-specific bridges surface clearly). Track as a new task post-ADR-0010 demolition.

**Decision (locked 2026-05-31):** Deferred until after ADR-0010 closes. Confirmed.

**Action:** None now. New task opens when ADR-0010 closes.

---

## 4. Save & Persistence (ADR-0004)

**Severity:** N/A — not implemented in the Unity project

**ADRs:** ADR-0004 (Accepted — passive orchestrator, per-system DTOs, distributed schema registry, atomic temp-then-rename writes, asymmetric exhaustion policy).

**Findings:**

- Zero `Save*.cs` or `Persist*.cs` files under `Assets/Scripts/`.
- Zero `SchemaVersion`, `saveVersion`, `SaveVersion` references in code.
- ADR-0004 is Accepted but the design is not yet wired into the Unity project.

**ADR-0011 implications when save system lands:**
- Save DTO shapes must each carry their own `SchemaVersion` constant (per ADR-0004 distributed registry) — version fields are an explicit ADR-0011 exception (§Allowed #2), so this is clean by construction.
- Save-format migrations must run as **one-shot migrators at load time** (ADR-0011 §Allowed #1), not as parallel runtime readers.
- Forbidden: shipping the save system with an "old format" branch and a "new format" branch.

**Action:** No retirement work. Re-audit this section when the first save DTO ships.

---

## 5. Deterministic RNG (ADR-0003)

**Severity:** LOW

**ADRs:** ADR-0003 (Accepted — per-call `System.Random` from `RunSeed ^ stepIndex`, live RNG passed by reference, forbidden non-determinism tokens enforced by CI grep).

**Findings:**

- `Deck.cs:35` comment confirms ADR-0003 compliance: "Fisher-Yates in place. Uses the provided RNG — never UnityEngine.Random." ✓
- `DamagePopupWidget.cs:77` uses `Random.Range(-MaxJitterPx, MaxJitterPx)` for cosmetic damage-popup horizontal jitter. This is view-layer cosmetic, not seeded gameplay state.
- Zero other `UnityEngine.Random` / `Random.Range` / `Random.value` usage found.

**ADR-0011 read:** Cosmetic RNG in the view layer is not a bridge — there is no parallel deterministic-RNG path for popup positioning, and damage-popup placement does not need to be deterministic. **No violation, but flag for cleanup consideration**: if the project ever adds a deterministic replay or screenshot-stable test mode, this becomes a violation.

**Action:** None now. Consider switching to a non-determinism token-prefixed comment (e.g., `// COSMETIC_RNG: not seeded — replay-stable popup positioning is out of scope`) to make the intent explicit. **Optional cleanup, not a retirement slice.**

---

## 6. Addressables vs Resources — ADR-0008 contradiction

**Severity:** HIGH

**ADRs:** ADR-0008 (Accepted 2026-05-25 — Addressables for chassis art lazy-load + VFX-combat overlay textures, 41 MB EA cap, local-only groups, async-only API rule, release-on-teardown unload contract).

**Findings:**

- **Zero** `Addressables.` / `AssetReference` references in code.
- Eleven `Resources.Load` / `Resources.LoadAll` call sites:
  - `CombatController.cs:197-200` — StarterDeckSO, VehicleDefinitionSO, CombatBalanceSO, RewardPoolSO loaded synchronously at Awake.
  - `CardWidget.cs:113` — `Resources.LoadAll<Sprite>` for card art.
  - `CardRewardPicker.cs:198` — `Resources.Load<CardWidget>` for card prefab.
  - `CombatPrefabAuthor.cs:2334,5284` — editor-time sprite load (allowed as editor authoring per ADR-0011 §Allowed #3).

**ADR-0011 implication:** ADR-0008 mandates Addressables for chassis art + VFX overlays specifically. **At ADR-0008's done state, those asset categories must NOT have a parallel `Resources.Load` path.** The current codebase has zero Addressables — when ADR-0008 implementation begins, the migration slice must convert the in-scope asset categories to Addressables AND delete the corresponding `Resources.Load` calls in the same slice. **No parallel runtime asset-loading paths allowed.**

**Out-of-scope-for-ADR-0008 loaders (StarterDeckSO, CombatBalanceSO, RewardPoolSO):** Per ADR-0008's stated scope (chassis art + VFX overlays), these may legitimately stay on `Resources.Load` if they're not in the Addressables scope. **Decision needed at ADR-0008 implementation time**: either (a) widen ADR-0008 scope to all SO data and migrate everything, or (b) explicitly carve these out as Resources-permanent in an ADR-0008 amendment with rationale.

**Retirement scope:** Schedule **ADR-0008 implementation slice** — when it begins, the in-scope assets migrate to Addressables in one pass, the corresponding `Resources.Load` sites are deleted, and a CI grep gate is added forbidding `Resources.Load` for those asset directories.

**Decision (locked 2026-05-31):** Leave dormant until user finishes creating chassis art + VFX overlay assets. Implementation slice triggers when the first in-scope asset is added; testing fills in the assets at that point. No sprint item until then.

**Action:** None now. Re-flag when first chassis art asset lands.

---

## 7. UI framework (UGUI vs UI Toolkit)

**Severity:** LOW — **DECIDED 2026-05-31**

**Findings:**

- 28 files use `using UnityEngine.UI;` (UGUI Canvas + Image + Button + RectTransform).
- Zero `.uxml` and zero `.uss` files anywhere in the project.
- Unity 6.3 LTS recommends UI Toolkit for new projects (per `docs/engine-reference/unity/VERSION.md`).

**ADR-0011 read:** Single UI system in use. **No bridge violation today.**

**Decision (locked 2026-05-31):** UGUI-only through release. User requirement: "everything in prefab designer friendly form everything connected and working." UI Toolkit's UXML/USS authoring is not prefab-based, which conflicts with the [Bake Designer Edits] / [Pre-Author Capture Protocol] workflow. No UI Toolkit ADR will be opened. Any future UI Toolkit adoption requires explicit user re-decision under ADR-0011.

**Action:** None. Decision locked.

---

## 8. Audio system

**Severity:** N/A

**Findings:** Zero `AudioSource`, `AudioClip`, `AudioMixer`, `AudioListener` references in `Assets/Scripts/`. Audio not yet implemented.

**ADR-0011 implication for audio implementation:** When audio lands, it must ship on a single playback path (single AudioSource pool, single mixer chain). Forbidden: shipping with a legacy `AudioSource.PlayClipAtPoint` path AND a modern pooled-source path AND an event-channel path all simultaneously.

**Action:** No retirement work. Re-audit when audio implementation begins; flag to `audio-director` agent at that point.

---

## 9. Input handling

**Severity:** N/A

**Findings:**

- `Assets/InputSystem_Actions.inputactions` exists (new Input System action map set up).
- Zero `UnityEngine.Input.` / `Input.GetKey` / `Input.GetButton` / `Input.GetAxis` / `Input.GetMouseButton` references in code.

**Action:** Clean. No bridge. No action required.

---

## 10. Vehicle variant chain

**Severity:** N/A

**Findings:** Plan abandoned 2026-05-13 per memory `project_vehicle_variant_chain.md` — Player and Enemy prefabs are now flat-copy independent (no variant link). The "Vehicle_Base sibling-chain" bridge candidate dissolved before becoming code debt.

**Action:** None.

---

## 11. Framework engine-free POCO `src/`

**Severity:** N/A

**Findings:**

- `LegacySlotKind` / `LegacyKindBridge` — zero references in `C:\ClaudeCreations\Madmax Roguelike\src\`. The combat slot bridge is entirely Unity-side.
- `UnityEvent` — 1 reference at `src/WastelandRun.Vehicle/IVehicleView.cs:125`, which is a doc comment **forbidding** UnityEvent in combat (citing technical-preferences). Not a usage.
- The framework `src/` carries the new engine-free POCO foundation (commit `4a6e5f9`). Clean by construction.

**Cross-repo question — DECIDED 2026-05-31 (B3):** Framework `src/WastelandRun.*` (commit `4a6e5f9`) is **reverted** — the engine-free POCO foundation was built around an obsolete 4-slot compile-time invariant (per its `SlotType` enum), which is incompatible with V1 Stage A's variable-N Dredge (9 slots including weapon_2 Javelin + slot_exposable_1/2). The framework work predates the variable-N pivot and cannot represent the locked V1 Stage A spec. Reverting framework `src/` leaves the Unity-side `Assets/Scripts/Combat/` as the canonical combat domain. **ADR-0010 reverts to its originally parked six-phase in-place retirement plan**, amended only to fold in the VehicleDefinitionSO finding (see §2).

**Action:** Delete framework `src/WastelandRun.Vehicle/`, `src/WastelandRun.Cards/`, `src/WastelandRun.Enemies/`, `src/WastelandRun.Gameplay/`, `src/WastelandRun.ScrapEconomy/` and the corresponding tests under `tests/unit/gameplay/`. Specific deletion list surfaces with the framework-revert task; see §Cross-cutting / Framework revert below.

---

## Cross-cutting findings

### Drift since 2026-05-31 morning survey

The combat slot bridge surface grew by 15 occurrences (1,081 → 1,096) and the bridge field reference count grew by ? (was 39 across 10 files; this audit measured 26 across 7 files for assignments specifically, so the metrics aren't directly comparable). **Conclusion:** Drift creep continues. The ADR-0010 phase plan's expectation of `~1,081 / 72` is approximate; Phase 0 should re-count immediately before locking phase boundaries, per memory `feedback_count_real_consumers.md`.

### CI grep gates to add (per ADR-0011 §Validation Criteria)

When each retirement slice closes, add a build-failing grep job:

| Slice | Token | Target path |
|-------|-------|-------------|
| ADR-0010 Phase 5 | `LegacySlotKind` | `Assets/Scripts/**/*.cs` |
| ADR-0010 Phase 5 | `LegacyKindBridge` | `Assets/Scripts/**/*.cs` |
| ADR-0010 Phase 5 | `IsLegacyMode\|IsLayoutMode` | `Assets/Scripts/**/*.cs` |
| ADR-0010 Phase 5 | `_maxArmor\|_currentArmor` | `Assets/Scripts/**/*.cs` |
| ADR-0010 Phase 5 | `EffectiveMaxArmor\|EffectiveCurrentArmor` | `Assets/Scripts/**/*.cs` |
| ADR-0008 implementation | `Resources\.Load.*ChassisArt\|Resources\.Load.*VfxOverlay` | `Assets/Scripts/**/*.cs` |

Gates live in `tools/ci/` (path TBD). Each slice's PR adds its own gate.

### ADR amendments required after each retirement

| Existing ADR | Amendment trigger | Action |
|--------------|-------------------|--------|
| ADR-0009 | ADR-0010 Phase 6 close | Mark superseded by ADR-0010 |
| ADR-0009 Amendment | ADR-0010 Phase 6 close | Mark architectural posture reversed by ADR-0011; conclusion superseded |
| ADR-0007 | ADR-0010 Phase 6 close | Annotation: bridge tolerance lifted; vocabulary is now single |
| ADR-0001 | ADR-0010 amended Phase 1/2 close | Annotation: VehicleDefinitionSO shape migrated; ADR-0001 design is now the only vehicle-part vocabulary |
| ADR-0008 | ADR-0008 implementation slice close | Annotation: Resources path retired for ADR-0008-scope assets |

### Per-system retirement order (proposed by gameplay-impact severity, not code volume)

1. **ADR-0010** — combat slot system (BLOCKING, B1 already happened). In flight.
2. **VehicleDefinitionSO redesign** — folded into ADR-0010 Phase 1/2.
3. **ADR-0008 implementation slice** — schedule alongside or after ADR-0010 demolition (depends on whether chassis art work for biome-1 is needed before ADR-0010 closes).
4. **Card system deep audit + retirement (if findings warrant)** — after ADR-0010 Phase 6.
5. **Save/persistence implementation** — when first DTO ships (must include schema-version field by construction).
6. **Audio implementation** — when audio begins (single-path constraint).
7. **UGUI vs UI Toolkit decision** — only if UI Toolkit is later adopted.

## Punch-list closure criteria

This audit document is closed when:

- [ ] ADR-0010 Phase 6 complete; CI gates for combat slot tokens green.
- [ ] VehicleDefinitionSO redesign complete (amended into ADR-0010 Phase 1/2 scope).
- [ ] Card system deep audit performed; either marked clean or follow-up retirement scheduled.
- [ ] ADR-0008 implementation slice scheduled or carved out via amendment with user rationale.
- [ ] UGUI-only-through-release decision confirmed with user.
- [ ] Save/persistence + audio sections re-audited when their systems land.

When all six are checked, this document is renamed `no-bridges-audit-2026-05-31-closed.md` and archived; a successor audit is opened.

## Memory hooks

- `project_no_bridges_at_done.md` — directive verbatim.
- `project_adr_0010_parked.md` — combat slot retirement plan pointer.
- `production/adr-0010-phase-plan.md` — full six-phase plan (NEEDS AMENDMENT — add VehicleDefinitionSO redesign to Phase 1/2 per finding #2).

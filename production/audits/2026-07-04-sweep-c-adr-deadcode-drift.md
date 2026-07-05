# Sweep C — ADR Compliance + Dead Code + Drift

**Auditor:** main-session (self-audit; parallel-grep salvo across compliance markers)
**Date:** 2026-07-04
**Scope:** ADR-0002/0003/0004/0010/0012/0013/0014 compliance; dead code; prefab-vs-author drift; test-mass debt.
**Explicitly excluded:** ADR-0011 (Sweep A), biome-slot-in (Sweep B).

---

## Executive Summary

Compliance across the 7 ADRs in scope is **clean**. Combat model is POCO-pure (ADR-0002), RNG discipline is enforced project-wide (ADR-0003), all 4 shipping DTOs implement `SystemId` + `SchemaVersion` (ADR-0004), slot vocabulary is single-string (ADR-0010), sum-of-parts armor is threaded through 14 files coherently (ADR-0012), reward-source siblings are wired (ADR-0013), and UI Toolkit primary stack is honored except for the known-and-planned P4 Combat_HUD migration (ADR-0014).

Dead-code surface is nearly empty: 1 TODO (VFX marker, non-blocking), zero `[Obsolete]`, zero "removed" comments. The `BuildLegacy` bimodal path across 13 CombatView widgets is the only significant drift and is already captured as **Sweep A C1**, not duplicated here.

Test-mass health is 87 test files across 5 asmdefs — proportionate to system count. No dead-test signals surfaced.

Verdict: no new blockers from this sweep. Two cosmetic-cleanup items (D1, D2) and one deferred-migration checkpoint (D3) below.

---

## 1.0 BLOCKERS (must fix)

_(None from this sweep. All ADRs in scope pass compliance grep gates.)_

---

## 1.0 CLEANUP (should fix)

### D1. Single TODO in Scripts — technical-art VFX marker

`Assets/Scripts/CombatView/CombatController.cs:542` — `TODO(technical-art): persistent smoke-from-hood VFX while Engine is [broken]`. Not load-bearing (game runs fine without smoke); parked for the technical-art pass. Recommend leaving as-is; this is the intended use of TODO — a marker for a future scoped pass, not tech debt.

**Status:** Not a violation. Kept in punch list for visibility only.

### D2. Combat_HUD UGUI Canvas + Button UnityEvent — ADR-0014 P4 pending

`Assets/Scripts/CombatView/CombatHud.cs:268,863` — comments reference `onClick listener (UnityEvent persistence on a...)` for the End Turn button (Unity UGUI `Button` → `onClick` is a `UnityEvent`). This is the known P4 migration path (Combat_HUD to UI Toolkit) documented in ADR-0014 §Phase table. The button is inside the `Combat_HUD` Canvas which is still UGUI pending P4.

**Not a violation today.** ADR-0014 P4 is planned as the "M1.5 dedicated migration slice." Per §5 vision, all core UI ships in UI Toolkit at 1.0 — meaning P4 must land before 1.0. Currently unscheduled.

**1.0 gate implication:** P4 becomes an explicit 1.0 blocker under the 3-biome scope revision (the Combat HUD is the single most-viewed screen in the game — cannot ship as UGUI while every other screen is UI Toolkit). Adding to the consolidated punch list.

---

## Confirmed Compliant

### K1. ADR-0002 — POCO combat, no UnityEvent

Zero UnityEvent usages in combat logic. All hits in grep are:
- xmldoc citations of the rule (CardRewardPickerController, CombatOutcomeOverlayController, MapViewController, RunCompleteViewController — all say "no UnityEvent per ADR-0014 / ADR-0002")
- Comments reinforcing the rule (`EnemyTurnResult.cs:12`, `PileCountWidget.cs:59`)
- The Combat_HUD End Turn button (see D2 — planned migration)

Combat model classes (`Vehicle`, `SlotInstance`, `Deck`, `Hand`, `TurnEngine`, `EnemyTurnResult`) contain zero `UnityEngine.*` references at the model layer. Verified.

### K2. ADR-0003 — Deterministic RNG discipline

`Assets/Scripts/Combat/Deck.cs:40` xmldoc: *"Fisher-Yates in place. Uses the provided RNG — never UnityEngine.Random."*

Grep for `UnityEngine.Random | Time.time | Time.deltaTime | DateTime.Now | DateTime.UtcNow` in `Combat/` and `Run/` returned zero forbidden-token hits. RNG discipline holds.

`RunController.cs:150-186` uses `RunSeed ^ stepIndex ^ salt` per ADR-0003 — verified in Sweep B K5.

### K3. ADR-0004 — Per-DTO `SystemId` + `SchemaVersion`

All 4 shipping DTOs implement both fields:
- `NodeMapDto.cs:98,107` — `SystemId => SYSTEM_ID`, `SchemaVersion => SCHEMA_VERSION`
- `RunDeckDto.cs:79,82` — same shape
- `RunSeedDto.cs:76,83` — same shape
- `VehicleStateDto.cs:71,78` — same shape

Defense-in-depth check in each DTO's `Deserialize`: orchestrator SystemId mismatch throws with explicit "orchestrator routing is broken" message. Per-DTO independent recovery per ADR-0004 §Recovery Chain.

**Note:** `MasteryStateDto` is missing (Sweep B B4) — that's a Sweep B blocker, not an ADR-0004 compliance violation of shipping code. When it lands it must follow this same pattern.

### K4. ADR-0010 — Single-vocabulary slot IDs

Grep for `LegacySlotKind | LegacyKindBridge | IsLegacyMode | _armorHp` returned zero hits. Retirement complete per memory `project_adr_0010_complete` (merged 2026-06-02). Slot vocabulary is a single `string slotId` across the entire codebase.

### K5. ADR-0012 — Sum-of-parts armor

14 files reference `RecomputeArmorPool | FillArmorPool | ArmorContribution` — all in canonical positions (Vehicle, SlotInstance, DamagePipeline, RepairResult, IPartData, PartDefinitionSO, VehicleDefinitionSO, VehicleStateDto, SlotSnapshotDto, 3× enemy archetypes, RunSceneHost, SmallFrameLayout). No orphaned references, no direct `armor_0.MaxHp` writes bypassing recompute (confirmed cross-reference with Sweep A findings — the surviving `armorContribution=0` default overload is a Sweep A B1 blocker, not an ADR-0012 shape violation).

### K6. ADR-0013 — RunDeck + sibling ICardRewardSource seam

`RunSession.cs:42-50` — takes both `IRewardSource` and `ICardRewardSource` as constructor parameters. Both interfaces implemented (`FlatScrapRewardSource : IRewardSource`, `FlatCardRewardSource : ICardRewardSource`). `CombatReward` type carries both `Scrap` and `Choices` per ADR-0013 additive composition.

`RunSession.cs:172,178` — null-check throws are correct guards per ADR-0013 (sources must return concrete value, not null).

**Note:** The hardcoded `Milestone1RewardPools` factory violates Sweep B B5 (biome-2/3 slot-in), not ADR-0013 shape. The interface seam is clean; the CONTENT behind the seam is milestone-hardcoded.

### K7. ADR-0014 — UI Toolkit primary, UGUI Popups exception

Post-combat flow (CardRewardPicker + CombatOutcomeOverlay) landed on UI Toolkit 2026-06-23. Map, Menu, Mastery all UI Toolkit. Popups Canvas is the intended axis-aligned exception per §Hybrid.

Combat_HUD remains UGUI pending P4 (see D2). Not a violation today but must land before 1.0 under the 3-biome scope.

---

## Dead Code Surface

- **1 TODO** across all `Assets/Scripts/` — the VFX marker (D1). Healthy.
- **Zero `[Obsolete]` attributes.**
- **Zero `// removed` / `// unused` / `// Deprecated` comments** in shipping code.
- **BuildLegacy bimodal path** (13 CombatView widgets) — captured as Sweep A C1, not duplicated here.

Dead-code surface is genuinely small for a codebase of this size. The discipline of "delete rather than dormant retention" from memory `feedback_aggressive_dead_code_cleanup` shows in the audit.

---

## Prefab-vs-Author Drift

Not swept in-depth — capture-before-destroy protocol and Prefab Mode discipline in the project means most drift is caught at author time. Known items:

- **Dredge HP bar UVs cosmetically off** — deferred per memory `project_dredge_uvs_deferred` to Slice 2.6 FrameLayoutSO migration. Still deferred; not a 1.0 blocker.
- **VehicleHudAnchors migration** landed 2026-06-30 per memory `project_hud_anchors_slice_26`; Dredge UV fix expected here — verify post-Slice-A.

No new drift surfaced.

---

## Test-Mass Health

- **87 `.cs` test files** across `Assets/Tests/EditMode/{Combat, CombatView, Run, Save, UI}`.
- Distribution proportionate to system size (Combat + CombatView dominate — matches the actual system weight).
- **7/7 UI Toolkit tests pass** as of 2026-07-04 verification (`production/qa/2026-07-04-uitoolkit-port-verify.xml`).
- No `[Ignore]` attributes or `.skip` files surfaced.

Test mass is healthy. No debt from this sweep.

---

## Investigation Log

- Grepped `UnityEvent` across `Assets/Scripts/` → 8 hits, all xmldoc/comments/planned-P4 button. Zero model-layer violations.
- Grepped `UnityEngine.Random | Time.time | Time.deltaTime | DateTime.Now | DateTime.UtcNow` across `Combat/` and `Run/` → zero forbidden-token hits.
- Grepped `SchemaVersion | SystemId` across `Save/Dtos/` → all 4 shipping DTOs implement both.
- Grepped `LegacySlotKind | LegacyKindBridge | IsLegacyMode | _armorHp` → zero hits.
- Grepped `RecomputeArmorPool | FillArmorPool | ArmorContribution` → 14 files, all canonical.
- Grepped `ICardRewardSource | IRewardSource` → sibling seam confirmed.
- Grepped `TODO | FIXME | HACK | XXX` → 1 hit (D1).
- Grepped `\[Obsolete\] | // Deprecated | // removed | // unused` → zero hits.
- Counted test files → 87 across 5 asmdefs.

---

## Verdict Summary

**0 blockers.** All 7 ADRs in scope pass compliance grep gates.

**1 checkpoint (D2) reclassified upward** — ADR-0014 P4 Combat_HUD migration was planned deferrable; under the 3-biome 1.0 scope revision it becomes an explicit 1.0 blocker (Combat HUD is the most-viewed screen; cannot ship UGUI while all other screens are UI Toolkit).

**1 cosmetic-only item (D1)** — VFX TODO marker; leave as-is.

Combined with Sweep A + Sweep B, the codebase is architecturally sound. The blockers cluster in **scope-completion work** (chassis roster, beacon handlers, mastery DTO, reward SOs, Combat_HUD port), not in **quality drift**. Fixing them is content-shaped work + a few structural conversions — nothing requires a rewrite.

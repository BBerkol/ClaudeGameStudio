# Vehicle & Part System — Review Log

## Review 1: 2026-04-21 (Light-Touch)

**Verdict**: APPROVED

**Mode**: Light-touch (single-pass, no multi-specialist gate). User directive 2026-04-21 to accelerate path to Unity prototype. Full director/specialist gating paused until a GDD introduces novel risk. ADR-0001 (Accepted 2026-04-21) covers the visual/technical risk surface for this system, so light-touch is appropriate.

### Completeness

All 8 required sections present (Overview, Player Fantasy, Detailed Design, Formulas, Edge Cases, Dependencies, Tuning Knobs, Acceptance Criteria). Bonus sections: Visual/Audio Requirements, UI Requirements, Open Questions. Detailed Design is split into Core Rules (R1–R9), States and Transitions, Emotional Attachment Mechanic (P4), Chassis Architecture (B2), and Interactions with Other Systems — matches the project's convention used by Card System and Status Effects.

### Cross-GDD Consistency

- **Card System GDD**: `CardFamily` values (Precision, Assault, Maneuver) referenced in B2 PrimaryFamilies match Card System exactly. `CardDefinitionSO` referenced in R7 (GrantedCards) matches ownership in Card System.
- **Status Effects GDD**: `ActiveStatuses: List<StatusInstance>` lives on the Vehicle POCO as Status Effects mandates. OQ-SE2 (`DamageSource` discriminator) closed by R9's `enum DamageSource { Card, Status, Environment }`.
- **Save & Persistence GDD**: Vehicle POCO is a leaf field on `RunState`; fully serializable, no MonoBehaviour dependency (R1).
- **ADR-0001**: Slot taxonomy, `IVehicleView` / `IVehicleMutator` signatures, and `SpriteKey` Addressables contract honored verbatim. Visual/Audio section cites ADR-0001 as authoritative rather than re-deriving.

### Internal Consistency

- R3 death check (`IsDead = Frame.DamageState == Offline`) consistent with AC-VP9/VP10 and F-VP4.
- R5 install auto-scraps previous part, matching session decision and EC-VP7.
- R6 Chopshop-only player-initiated scrap matches design decision locked 2026-04-21.
- R7 GrantedCards rarity counts (1/2/3/3) tested by AC-VP16–VP18.
- F-VP1 DegradedThreshold formula produces values consistent with AC-VP5 (Scout Frame: 50% × 16 = 8) and AC-VP6 (Hp=9 → Functional).
- F-VP2 plating-first ordering + Corrode-before-plating (reasserted from Status Effects F1) tested by AC-VP20, AC-VP27.
- State machine transition table (States and Transitions) exhaustive: all 4 states × valid transitions covered, including Offline→Degraded revival via `canReviveOffline` flag (AC-VP24).

### Formula Quality

F-VP1 through F-VP4 all have variables, ranges, examples, and design-target commentary. F-VP2 includes two worked examples (plating absorption and Functional→Offline one-hit). F-VP3 composition order explicit (`(base + add) × mult`), Override precedence documented, example calculation given.

### Acceptance Criteria

35 GIVEN/WHEN/THEN ACs. All testable. AC-VP34 (M2 emotional attachment) testable via seeded unit test. AC-VP35 (M3 scarcity) flagged as playtest-only per coding standards. All other ACs automatable.

### P4 Mandatory Compliance

Systems-index Scoping Note P4 requires a named, specified emotional-attachment mechanic. Delivered via the Emotional Attachment Mechanic (P4) sub-section with three mechanisms (M1 visible damage, M2 part-granted cards, M3 part scarcity) plus a design-test gate (two yes/no questions the mechanic must pass). Anti-features explicitly listed to prevent scope creep toward simulation-depth tracking.

### Open Questions (Carried Forward)

- **OQ-VP1**: Mastery track schema (`MasteryUnlocks` on ChassisDefinitionSO) — resolves when Meta Progression GDD begins.
- **OQ-VP2**: Override collision handling at runtime install — resolves in Scrap Economy GDD (strong recommendation: UI prevents install).
- **OQ-VP3**: All-slots-Empty + Frame=Empty death ruling — resolves in Scrap Economy and/or Card Combat GDD (leaning: Scrap Economy enforces Frame-replacement).
- **OQ-VP4**: Mid-resolution deck zone mutation semantics — resolves in Card Combat GDD (needs stack definition).
- **OQ-VP5**: Stat round/floor convention for int consumers — resolves when first int-consuming stat is implemented.

### Closed Open Questions

- **Status Effects OQ-SE2**: `DamageSource` discriminator. Closed by R9's `enum DamageSource { Card, Status, Environment }`. Status Effects review log entry may be updated to reference Vehicle & Part R9.

### Forward Dependencies (to Propagate)

1. **Card System GDD** must acknowledge part-granted cards as a deck source + per-instance provenance tracking (EC-VP11, R7). Propagate via `/propagate-design-change` or during Card Combat GDD.
2. **Meta Progression GDD** (future) — `MasteryUnlocks` field on `ChassisDefinitionSO`.
3. **Scrap Economy GDD** (future) — `InstallPart` / `RemovePart` exclusivity; Chopshop-only scrap enforcement; Override-collision handling.

### Non-Blocking Observations

- EC-VP19 (save-with-dead-Frame) is conditional on Save & Persistence's decision about save points during defeat sequence. If saves are disallowed during defeat (recommended), the EC cannot occur — no change required now.
- Visual/Audio and UI Requirements are light by design, citing ADR-0001 and deferring to Combat HUD / Audio GDDs for full spec. Acceptable for a light-touch MVP GDD.
- B2.5 mastery hook deferred to OQ-VP1 rather than authored now — prevents premature schema lock.

### Files Updated by This Review

- `design/gdd/systems-index.md` — Row 2 (Vehicle & Part System) → Approved; Progress Tracker 4/10 MVP; Next Steps updated.
- `production/session-state/active.md` — next milestone set to Card Combat GDD.
- `design/gdd/reviews/vehicle-and-part-system-review-log.md` — this file.

---

## Review 2: 2026-05-18 (Full Multi-Specialist — Post ADR-0007 Revision)

**Verdict**: MAJOR REVISION NEEDED
**Scope signal**: XL
**Specialists**: game-designer, systems-designer, qa-lead, ux-designer, ui-programmer, audio-director, economy-designer, unity-specialist, creative-director (senior synthesis)
**Blocking items**: 7 | **Recommended**: 10 | **Specialist disagreements**: 0 (convergence across all 8 specialists)
**Prior verdict resolved**: No — light-touch APPROVED (2026-04-21) is superseded by ADR-0007 revision findings.
**User decision**: Defer revision to separate session.

### Summary

Eight specialists converged on a single thesis: the architecture is sound, but the contract surface is unfinished. ADR-0007 (Frame-Driven Variable Slot System) was authored as a parallel draft to the V&P GDD revision and the two were never reconciled, producing three documented contradictions (overflow amplification policy, floor vs. ceil rounding for `redirected_amount`, layout naming `small_frame` vs. `small_frame_player`). Independently, the R9 interface is missing two events (`OnSlotHpChanged`, `OnVehicleDied`) that are already cited in EC-VP20. The engine-free assembly boundary leaks `PartDefinitionSO` in two locations (R9 events and R12 `IPartCatalog`). Combat HUD GDD is stale (6+ hardcoded 4-slot references). The 3× Armor exposure multiplier is mechanically present but has no perceptual layer — players cannot read the breakthrough moment.

### Blocking Items (7)

1. **GDD ↔ ADR-0007 reconciliation** [systems-designer + qa-lead + unity-specialist] — Three documented contradictions: overflow amplification policy (GDD R_ARM.2 vs ADR Decision 6), `floor` vs `ceil` for `redirected_amount`, layout naming (`small_frame` vs `small_frame_player`), and `FrameLayoutId` field type (string vs direct SO reference). ADR-0007 should be amended first and frozen; GDD revised to match.
2. **R9 interface contract gap** [ui-programmer + qa-lead + unity-specialist] — `OnSlotHpChanged` and `OnVehicleDied` referenced in EC-VP20 but not declared in `IVehicleView`. Plus `TickStatuses` / `RemoveStatus` missing per ADR-0007. Cannot implement HUD or death sequence.
3. **Engine-boundary leak in two locations** [unity-specialist] — R9 events (`OnPartInstalled`/`Removed`) and R12 `IPartCatalog` return `PartDefinitionSO` into `WastelandRun.Vehicle.asmdef` which has `noEngineReferences: true`. Violates ADR-0005. Swap to `string PartId`.
4. **`IsDead` getter-vs-event ordering race** [systems-designer] — `IsDead` is derived from `StructuralHp == 0` so it returns true the moment Hp hits 0, but EC-VP20 orders `OnVehicleDied()` as step 6/last. Subscribers reading `IsDead` early will double-trigger defeat sequence.
5. **`DegradedThreshold` floor-to-zero** [systems-designer] — `floor(pct × MaxHp / 100)` produces 0 at low boundary inputs. Degraded band becomes unreachable; slot jumps Functional → Offline silently. Add `max(1, …)` clamp.
6. **Float-to-int overflow in amplified damage** [systems-designer] — `(int)(amount × ExposureMultiplier)` wraps to `int.MinValue` at large inputs. Needs explicit `Math.Min(…, int.MaxValue)` clamp before cast.
7. **Armor with null `MaxHpOverride` and no chassis default** [systems-designer + unity-specialist] — `KeyNotFoundException` or Hp=0 birth. Dredge currently TBD on both Armor plates. `OnValidate` rule required.

### Recommended Revisions (10 — important but not blocking)

8. **Armor breakthrough perceptual layer** [game-designer + ux-designer + audio-director] — 3× exposure multiplier is mechanically present but invisible. Required: `ExposureMultiplier` as displayed stat (REC-1), distinct exposure cascade vs ordinary Offline (R3), differentiated SFX for amplified hits (MISS-5), persistent Armor→Hull redirect indicator (R4), screen-reader announce grammar (A2).
9. **combat-hud.md must be updated for ADR-0007** [ui-programmer + ux-designer] before any HUD implementation — 6+ hardcoded 4-slot references including dead `CurrentArmor`/`MaxArmor` bindings.
10. **Scrap Economy `InstallCost` formula** [economy-designer] — `installedCount` domain hardcoded 0–4. Will produce prohibitive pricing at 7–10 slots. Normalize as `installedCount / totalSlots` or cap.
11. **Loot & Reward D.5 sampling vocabulary stale** [economy-designer] — enumerates deleted slot kinds `{Engine, Frame, Tire, Weapon, Auxiliary}` (Frame→Hull renamed; Tire/Auxiliary don't exist in `SlotKind`). Replace with live query against `IVehicleView.GetSlotsByKind()`.
12. **`MountDirection` produces unspendable loot** [economy-designer + game-designer] — `IPartCatalog.GetParts` has no `SlotPosition` parameter; post-EA position-locked weapons will be offered to incompatible layouts.
13. **11 ACs require testability rewrites** [qa-lead] — AC-VP2, AC-VP3, AC-VP7, AC-VP12, AC-VP20, AC-VP27, AC-VP33b, AC-VP34, AC-VP36, AC-VP38, AC-VP39, AC-VP40. Plus 14 coverage gaps including `FrameLayoutSO.OnValidate`, `MountDirection → PositionRequirement` derivation, and `OnSlotHpChanged` firing.
14. **Audio spec completion** [audio-director] — mix bus assignment, polyphony cap (10-slot Dredge stress case), card-removal cue, plate-shatter timing anchor, amplified-redirect SFX layer.
15. **Non-structural slot loss has no death-proximity signal** [game-designer] — consider `CriticalState` computed property as near-death indicator (Player Fantasy "maybe this run is over" has no mechanical hook below Hull death).
16. **Part-granted card economy ceiling** [economy-designer] — 5 slots × 3 Rare = 15 granted cards; deck can balloon past Card System coherence math thresholds.
17. **`HudAnchor` field** [ux-designer] referenced in ADR-0007 summary but missing from GDD R_FL.1 `SlotDefinition` contract.

### Nice-to-Have (not tracked as blocking/recommended)

- Layout strategy for 8–10 slot HUD (dense row variant)
- Aggregate armor readout for multi-Armor vehicles
- Progressive disclosure tooltip on chassis swap
- Information hierarchy in degraded-heavy states (row background tints)
- DOT-on-exposed-Armor "no-op" surfaced in tooltip
- Tenure-bonus discovery as designed mastery moment

### Senior Verdict — Creative Director

> "Eight specialists, zero clean APPROVEs, and at least three documented contradictions between the GDD and the very ADR it implements. The architectural redesign is sound; the contract surface is unfinished and self-contradictory in ways that would silently corrupt implementation. ADR-0007 was the right call — every critical issue is a *contract* problem, not a *concept* problem. Process miss: ADR and GDD were authored in parallel without reconciliation. Do not revise in-session — defer to a focused reconciliation pass (ADR amendment, R9 rewrite, boundary fixes, stale-doc triage), then re-review."

### Process Note

ADR-0007 and V&P GDD were authored as parallel drafts and never reconciled against each other before review. Recommendation: ADR-0007 should be amended first and frozen, then the GDD revised to match. This protocol miss is informational, not a defect to fix mid-session.

### Files Updated by This Review

- `design/gdd/systems-index.md` — Row 2 status Approved (2026-04-21) → NEEDS REVISION (2026-05-18); header Last Updated bumped.
- `design/gdd/reviews/vehicle-and-part-system-review-log.md` — this entry.
- `production/session-state/active.md` — W0 cleanup item 3 marked complete with deferred-revision note.

---

## Review 2 Revision Pass: 2026-05-18 (Same-Day In-Session)

**Outcome**: All 7 Review 2 blockers closed via 19 surgical edits.

User pushed back on creative-director's "defer to separate session" recommendation and chose to revise in-session. Pattern A boundary fix applied (SlotInstance/Mutator/Catalog use `IPartData`; events use `string PartId`). `IsDead` converted to backing field with atomic write-before-event in F-VP2 Step 4(f-g). `DegradedThreshold` gained `max(1, …)` clamp in F-VP1. `SafeAmplify` helper introduced in R_ARM.2 + F-VP2 (clamps amplified amount to `int.MaxValue` before int cast). `FrameLayoutSO.OnValidate` now rejects Armor slot with null/<1 `MaxHpOverride` (hard import gate). `HudAnchor Vector2` field added to `SlotDefinition`. 10 new ACs authored (AC-VP43..AC-VP52). 8 of 10 recommended items deferred (#8 Armor perceptual layer, #9 combat-hud.md update, #10 InstallCost normalization, #11 Loot D.5 vocab, #12 MountDirection unspendable-loot guard, #13 11 ACs rewrite + 14 coverage gaps, #14 audio spec completion, #16 part-granted card economy ceiling).

Re-review recommended in fresh session before Approved status.

---

## Review 3: 2026-05-18 (Same-Day Re-Review of Revision Pass)

**Verdict**: MAJOR REVISION NEEDED
**Scope signal**: L
**Specialists**: game-designer, systems-designer, qa-lead, ux-designer, unity-specialist, creative-director (senior synthesis)
**Blocking items**: 10 | **Recommended**: 2 trivial (#15 CriticalState AC, #17 HudAnchor field surfaced from Review 2's recommended list)
**Specialist disagreements**: 0 (full convergence on top blockers)
**Prior verdict resolved**: Partial — Review 2 blockers were closed but the revision introduced new contract gaps and surfaced new contradictions between V&P GDD and ADR-0007 that Review 2 did not flag.
**User decision**: Revise immediately in-session (declined defer-to-fresh-session recommendation).

### Summary

Six specialists converged on a single thesis: Review 2's revision was structurally correct but introduced a new GDD↔ADR-0007 drift pattern. The revision pass applied edits to the GDD without back-propagating to ADR-0007, so three documented contradictions remained: `IsDead` is still declared as a derived getter in ADR-0007 Decision 4 (GDD R3 says backing field); `HudAnchor` in ADR-0007 Key Interfaces is still `UnityEngine.Vector2` (violates engine-free `WastelandRun.Vehicle.asmdef` per ADR-0005); `IFrameLayout` interface is missing from GDD R_FL despite being the ADR-0007 contract surface. Plus the engine-free boundary was incomplete (`SlotDefinition` lives in `WastelandRun.Gameplay.Vehicle` but should be in `WastelandRun.Vehicle`); `F-VP2` step ordering allowed `OnVehicleDied` to fire before `IsDead` was atomically written; `OnCriticalStateChanged` and `OnGrantedCardRemoved` events were missing from `IVehicleView`; `SafeAmplify` did not guard against NaN/Infinity inputs.

### Blocking Items (10 — All Closed Same-Day)

1. **ADR-0007 IsDead must be backing field** [systems-designer + unity-specialist] — Decision 4 still declared `public bool IsDead => StructuralHp == 0;`. Closed: amended to `IsDead { get; private set; }` with backing-field semantics noted; GDD R3 + F-VP2 Step 4(f-g) reference the atomic write pattern.
2. **HudAnchor must be engine-free POCO, not Vector2** [unity-specialist + ux-designer] — ADR-0007 Key Interfaces and GDD R_FL.1 both declared `Vector2 HudAnchor` inside the engine-free assembly, violating ADR-0005. Closed: introduced `AnchorPoint` POCO struct with `IsFinite`/`IsInUnitRect` helpers; field type changed throughout; CI grep gate added (AC-VP52) to prevent regression.
3. **IFrameLayout interface must be declared in GDD R_FL** [unity-specialist + qa-lead] — GDD R_FL.1 only mentioned `FrameLayoutSO` (engine-bearing). Closed: GDD R_FL.1 rewritten to declare `IFrameLayout` (engine-free interface) implemented by `FrameLayoutSO`; vehicle construction iterates `IFrameLayout.Slots`.
4. **OnCriticalStateChanged must fire after OnVehicleDied** [game-designer + ux-designer] — F-VP2 Step 4 had no Critical state transition event. Closed: added step (h) `if vehicle.CriticalState != wasCritical: Fire OnCriticalStateChanged(vehicle.CriticalState)` after OnVehicleDied; AC-VP51b added; `wasCritical` snapshot at step start.
5. **F-VP1 OnValidate must reject zero-width Functional band** [systems-designer + qa-lead] — `DegradedThreshold` could equal `MaxHp` at edge cases. Closed: explicit "HP boundaries reject zero-width band" rule added as OnValidate rule #6; AC-VP48f written.
6. **SafeAmplify must guard against NaN/Infinity** [systems-designer] — Multiplier corruption via debug write or deserialization bug could amplify to nonsense values. Closed: NaN/Infinity guard + `multiplier ≤ 0` guard added, both fall back to unamplified `amount` with warning log; AC-VP50b written.
7. **OnValidate must reject Armor as RedirectsToSlotId target** [unity-specialist + game-designer] — Armor → Armor redirect chains would create infinite-loop exposure cascades. Closed: AC-VP48c explicitly forbids `RedirectsToSlotId` pointing to another Armor slot.
8. **AC-VP48 must split into 6 sub-ACs covering all OnValidate rules** [qa-lead] — Original AC-VP48 was a single compound AC, not independently testable. Closed: AC-VP48a (Unique SlotIds), AC-VP48b (≥1 structural), AC-VP48c (Armor RedirectsToSlotId), AC-VP48d (ExposureMultiplier finite/positive), AC-VP48e (HudAnchor finite/unit-rect), AC-VP48f (HP boundaries reject zero-width band).
9. **OnGrantedCardRemoved event missing for U6 Offline deck change** [ui-programmer + game-designer] — When granted cards are removed on slot Offline, no event fires so HUD can't update card displays. Closed: added `OnGrantedCardRemoved(slotId, IReadOnlyList<string> grantedCardIds)` to IVehicleView events; F-VP2 step (d′) fires the event after card removal.
10. **L&R D.5 stale slot vocab + Scrap Economy D.6 CombatsSurvived ownership** [economy-designer] — L&R D.5 + 8 referenced lines still used deleted enum `{Engine, Frame, Tire, Weapon, Auxiliary}`; Scrap Economy D.6 did not document V&P as the single writer of `CombatsSurvived`. Closed: loot-reward.md updated to `{Engine, Hull, Mobility, Weapon, Armor, Wildcard}` across all references with a tracking note; scrap-economy.md D.6 amended with explicit "read-only consumer" ownership block; V&P R11 was already updated in same session.

### Recommended Items Closed (2 trivial — surfaced from Review 2's recommended list)

- **#15 — `CriticalState` AC reconciliation** — AC-VP51 contradicted R9's canonical definition (had additional "AND ≥1 structural still Functional" conjunct, which would have made `CriticalState=false` in the most critical case: every structural slot Degraded but none yet dead). Reconciled to match R9: any structural Degraded/Offline = Critical. AC-VP51 rewritten; AC-VP51b added for transition test.
- **#17 — `HudAnchor` field missing from GDD R_FL.1** — Reflected in ADR-0007 summary but missing from GDD `SlotDefinition` contract. Closed: added to `SlotDefinition` (R_FL.1) and copied onto `SlotInstance` (R3) at construction time. Combat HUD subscribers read `HudAnchor` from `SlotInstance` directly without a layout round-trip.

### Recommended Items Still Deferred (8 — to be carried into Review 4)

- #8 Armor breakthrough perceptual layer (ExposureMultiplier displayed stat, distinct exposure cascade vs Offline, differentiated SFX, persistent Armor→Hull redirect indicator, screen-reader announce grammar)
- #9 combat-hud.md must be updated for ADR-0007 (6+ hardcoded 4-slot references + dead `CurrentArmor`/`MaxArmor` bindings)
- #10 Scrap Economy `InstallCost` formula normalization (`installedCount / totalSlots` cap)
- #11 Loot & Reward D.5 sampling vocabulary fully reconciled with live `IVehicleView.GetSlotsByKind()` query (only the enum vocab was fixed in Review 3; the live-query refactor is the deeper fix)
- #12 `MountDirection` unspendable-loot guard (add `SlotPosition` parameter to `IPartCatalog.GetParts`)
- #13 11 AC testability rewrites + 14 coverage gaps including `FrameLayoutSO.OnValidate`, `MountDirection → PositionRequirement` derivation, `OnSlotHpChanged` firing
- #14 Audio spec completion (mix bus assignment, polyphony cap, card-removal cue, plate-shatter timing anchor, amplified-redirect SFX layer)
- #16 Part-granted card economy ceiling (5 slots × 3 Rare = 15 granted cards may exceed Card System coherence math thresholds)

### Senior Verdict — Creative Director

> "Review 2's revision was structurally correct but introduced a new GDD↔ADR-0007 drift pattern: edits were applied to one document without back-propagating to the other. Three contradictions remained — IsDead derived-getter, HudAnchor Vector2 in engine-free assembly, IFrameLayout interface missing — every one of them a contract-shape issue, not a concept issue. Resolution requires editing both docs in lock-step. Recommend deferring to a fresh session; revision-in-session is high-risk for context exhaustion mid-flight."

User overrode the defer recommendation and elected to revise in-session. All 10 blockers + 2 trivial recommended closed via lock-step edits to V&P GDD + ADR-0007 + loot-reward.md + scrap-economy.md. Two design decisions resolved via user adjudication:
- **AC-VP51 reconciliation**: R9 wins (any structural Degraded/Offline = Critical, no conjunct).
- **CombatsSurvived ownership**: V&P is the single authoritative writer; Scrap Economy is read-only.

### Files Updated by This Review

- `design/gdd/vehicle-and-part-system.md` — R3 (`SlotInstance` HudAnchor field + CombatsSurvived ownership note), R9 (events block: `OnGrantedCardRemoved` + `OnCriticalStateChanged`), R11 (CombatsSurvived single-writer block), R_FL.1 (IFrameLayout interface, AnchorPoint type, 6 OnValidate rules), R_ARM.2 (SafeAmplify NaN/Inf guard), F-VP1 (zero-width band rejection), F-VP2 Step 4 (atomic ordering, `wasCritical` snapshot, step d′ OnGrantedCardRemoved, step h OnCriticalStateChanged), Acceptance Criteria (AC-VP48 split into a-f, AC-VP50b, AC-VP51 reconciled, AC-VP51b added, AC-VP52 updated to AnchorPoint + CI gate), Tuning Knobs (HudAnchor row → AnchorPoint), Testing notes (full AC-VP43..AC-VP52 + sub-AC list).
- `docs/architecture/adr-0007-frame-driven-variable-slot-system.md` — Decision 2 (SlotDefinition moved to WastelandRun.Vehicle, AnchorPoint POCO added, MaxHpOverride documented, 6 OnValidate rules), Decision 3 (prose updated for engine-free SlotDefinition), Decision 4 (IsDead backing field), Key Interfaces (AnchorPoint declaration, HudAnchor type swap, MaxHpOverride, CriticalState getter, OnCriticalStateChanged + OnGrantedCardRemoved events).
- `design/gdd/loot-reward.md` — D.5 + worked example + AC-LR24 + AC-LR37 + palette spec: all stale `{Engine, Frame, Tire, Weapon, Auxiliary}` references replaced with `{Engine, Hull, Mobility, Weapon, Armor}`; explanatory mapping note added.
- `design/gdd/scrap-economy.md` — D.6 amended with explicit "read-only consumer" ownership block for CombatsSurvived.
- `design/gdd/systems-index.md` — Row 2 status NEEDS REVISION → REVISED — Awaiting Re-Review; header Last Updated bumped.
- `design/gdd/reviews/vehicle-and-part-system-review-log.md` — this entry.

---

## Review 4: 2026-05-18 (Same-Day Re-Review of Review 3 Revision Pass)

**Verdict**: NEEDS REVISION
**Scope signal**: M
**Specialists**: game-designer, systems-designer, qa-lead, ux-designer, ui-programmer, audio-director, economy-designer, unity-specialist, creative-director (senior synthesis)
**Blocking items**: 5 | **Recommended**: 10 (1 closed in-session: R1 MAY→MUST; 9 deferred)
**Specialist disagreements**: 2 (qa-lead vs creative-director on AC-rewrite blocker promotion; ui-programmer vs creative-director on combat-hud.md gating — both resolved per creative-director split)
**Prior verdict resolved**: Partial — Review 3 blockers were closed but lock-step in-session edits introduced 5 new spec-precision drift points across V&P GDD ↔ ADR-0007 ↔ Scrap Economy.
**User decision**: Revise immediately in-session (declined creative-director's "strong recommendation: fresh session"); R5 fresh-session re-review queued post-`/clear`.

### Summary

Eight specialists converged: the architecture is no longer in doubt — R2 and R3 closed the structural issues (assembly boundaries, event ordering primitives, damage redirection math, SafeAmplify hardening all hold). What remained was **spec-precision debt**: 5 places where the GDD prose and the ADR prose drifted out of lock-step during the R3 in-session revision, plus one cross-doc contradiction with Scrap Economy. Creative-director's verbatim verdict: "This is mechanical alignment work, not creative work."

### Blocking Items (5 — All Closed Same-Day)

1. **Canonical event table + ADR-0007 Decision 11 lock-step + wasCritical pre-Step-2 timing** [creative-director + game-designer + systems-designer + ui-programmer] — F-VP2 Step 4's (a→h) ordering was not mirrored in ADR-0007 Decision 11 (still pre-R3 simplified pseudocode); `wasCritical` was captured at Step 4 start, racing the Armor → redirect recursion's mid-step CriticalState transitions. **Closed:** `wasCritical` moved to F-VP2 Step 0 (pre-Step-2, pre-recursion) so each ApplyDamage call captures its own pre-state; new "Canonical Event Order Table" added in V&P F-VP2 as single source of truth (cited by ADR-0007 + combat-hud.md); ADR-0007 Decision 11 rewritten to defer to V&P + 7 locked invariants (CI lock-step compare against V&P "Ordering contract (locked)" block).
2. **F-VP2 Step 4(d) engine-boundary regression on `slot.InstalledPart.GrantedCards.Select(c => c.CardId)`** [unity-specialist + game-designer] — Pseudocode implied an engine-bearing object collection across the engine-free `WastelandRun.Vehicle` boundary (ADR-0005 `noEngineReferences:true`). **Closed:** new "`IPartData.GrantedCards` engine-free type" paragraph added in V&P R4 IPartData boundary contract — typed `IReadOnlyList<IGrantedCardData>` where `IGrantedCardData` is an engine-free struct/interface in `WastelandRun.Vehicle` carrying `CardId (string)` + baked `PositionRequirement`. ADR-0007 Decision 11 invariant #7 mirrors. No `CardDefinitionSO` reference crosses the boundary.
3. **DegradedThresholdPct ownership + IFrameLayout interface gap** [systems-designer + game-designer] — R_FL.1 declared `IFrameLayout` interface but did not specify whether `DegradedThresholdPct` lived on the interface or the SO; line 113 still described it as a chassis-level knob on `ChassisDefinitionSO`. **Closed:** user adjudicated to interface-level fixed default 50 (no per-frame override for EA). Added `DegradedThresholdPct (int, 1..99)` to `IFrameLayout` in both V&P R_FL.1 and ADR-0007 Key Interfaces; `FrameLayoutSO` implements the property as a `[SerializeField]` per-layout authoring surface (all 6 EA layouts ship 50); line 113 description corrected.
4. **CriticalState UX delivery stub** [creative-director + ux-designer + ui-programmer] — `OnCriticalStateChanged` event existed (R3 closure) but V&P did not specify what the HUD MUST do on receipt; combat-hud.md still stale on the 4-slot model. **Closed:** new contract-only MUST-verb stub added in V&P UI Requirements — "HUD MUST display a critical-state indicator within 100ms of `OnCriticalStateChanged(isCritical=true)`, MUST clear within 100ms on `isCritical=false`." Visual treatment (vignette geometry, color, pulse cadence) and audio-layer driving deferred to combat-hud.md refresh ticket (separate follow-up, not a V&P gate per creative-director's split with ui-programmer).
5. **Cross-doc contradiction — Offline tenure accrual** [economy-designer] — Scrap Economy D.6 line 379 ("regardless of damage state — an Offline part still 'survived'") directly contradicted V&P R11 ("Offline slots do NOT accrue tenure"). V&P R11 is the single authoritative writer per R3 closure. **Closed:** scrap-economy.md D.6 tenure-source paragraph rewritten to align with V&P R11; supersession note added; AC-SE7b/c math unaffected (only semantics tightened — `combatsSurvived` is "combats where slot was non-Offline at end," not "combats fought").

### Recommended Items (1 Closed In-Session, 9 Deferred)

**Closed in-session:**
- **R1 — "MAY emphasize" → "MUST emphasize"** [ux-designer] — V&P UI Requirements `OnArmorExposed` line tightened. The exposure mechanic's perceptual layer is the core UX payload; optional treatment defeats the design intent.

**Deferred to follow-up tickets (carried forward to R5 + downstream):**
- R2 F-VP1 explicit zero-MaxHp edge case (R_FL.1 rule 6 already rejects at import; defensive prose only)
- R3 11 R2-era ACs in prose phrasing — qa-lead flagged as blocker, creative-director downgraded to recommended ("quality issue, not correctness")
- R4 Audio cue bindings for OnArmorExposed / OnSlotDamageStateChanged→Degraded / OnCriticalStateChanged / OnVehicleDied (Audio GDD ownership)
- R5 AC-VP48 sub-AC (d) explicit editor-time behavior (red console error vs silent clamp)
- R6 Combat HUD anchor wrap rule for 4–10 slot layouts (combat-hud.md refresh ticket)
- R7 ScrapReward.PerCombat soft-cap cross-link (Scrap Economy)
- R8 OnSlotDamageStateChanged idempotency note on identical-state writes (covered implicitly by Canonical Event Order Table footnote)
- R9 SafeAmplify warning log channel specification (ADR-0005 forbids `Debug.LogWarning` in engine-free assembly)
- R10 AC-VP48 sub-AC (f) NaN/Inf MaxHp rejection sub-case

### Specialist Disagreements (Resolved Per Creative-Director Split)

- **qa-lead vs creative-director** on R3 (11 R2-era ACs): qa-lead promoted to **blocker**; creative-director downgraded to **recommended** ("rewriting 11 R2-era ACs is a quality issue, not a correctness issue"). Resolution: deferred to follow-up batch.
- **ui-programmer vs creative-director** on combat-hud.md staleness: ui-programmer marked as V&P blocker B5; creative-director half-agreed — stub the CriticalState UX in V&P (closes V&P-side gate, becomes Blocker #4) and defer combat-hud.md full refresh as a follow-up ticket against ADR-0007's variable-slot model.

### Senior Verdict — Creative Director

> "The architecture is no longer in doubt. R2 and R3 closed the structural issues — assembly boundaries, event ordering primitives, damage redirection math, and the SafeAmplify hardening all hold up. What remains is **spec-precision debt**: 5 places where the GDD's prose and the ADR's prose drifted out of lock-step during the R3 in-session revision, plus one cross-doc contradiction with Scrap Economy. This is mechanical alignment work, not creative work. Scope is M (medium), 4–8 hours focused. **Strong recommendation: fresh session.** R4's findings are a focused punch list; opening a clean context with the blocker list and editing in lock-step will produce a cleaner result than another in-session round on top of an already-long R3 session." Gate: `[GATE-CD-GDD-ALIGN]: CONCERNS`

User overrode the defer recommendation and elected to revise in-session (third override across R2/R3/R4 sequence). All 5 blockers + 1 recommended closed via lock-step edits to V&P GDD + ADR-0007 + scrap-economy.md. Three design decisions resolved via user adjudication:
- **DegradedThresholdPct ownership**: interface-level constant default 50 (no per-frame override for EA; future balance can promote to SO-level if needed).
- **wasCritical snapshot timing**: pre-Step-2 (each recursive ApplyDamage captures its own pre-state).
- **CriticalState UX stub**: contract-only with MUST verb + 100ms latency budget; specific visual treatment deferred to combat-hud.md authorship.

### Files Updated by This Review

- `design/gdd/vehicle-and-part-system.md` — Line 113 (DegradedThreshold owner: interface, not chassis), R_FL.1 (IFrameLayout gained `DegradedThresholdPct`; FrameLayoutSO implements), R4 IPartData boundary (new "`IPartData.GrantedCards` engine-free type" paragraph declaring `IReadOnlyList<IGrantedCardData>`), F-VP2 Step 0 (added `wasCritical` pre-Step-2 capture), F-VP2 Step 4 (removed redundant wasCritical capture; added subscriber-rules clarification), F-VP2 (new "Canonical Event Order Table" with 10-row table + 3 subscriber rules, single source of truth for ADR-0007 + combat-hud.md), UI Requirements (R1 MAY→MUST on OnArmorExposed; new OnCriticalStateChanged HUD contract stub with 100ms latency).
- `docs/architecture/adr-0007-frame-driven-variable-slot-system.md` — Decision 11 rewritten (replaces simplified pseudocode with 7 locked invariants deferring canonical pipeline to V&P F-VP2; CI lock-step compare specified), Key Interfaces (`IFrameLayout` gained `DegradedThresholdPct { get; }` property).
- `design/gdd/scrap-economy.md` — D.6 tenure-source paragraph (Offline accrual contradiction with V&P R11 resolved in V&P's favor; supersession note added).
- `design/gdd/systems-index.md` — Row 2 status REVISED → Awaiting Re-Review (R4 → R5); header Last Updated bumped.
- `design/gdd/reviews/vehicle-and-part-system-review-log.md` — this entry.

---

## Review — 2026-05-18 — Verdict: MAJOR REVISION NEEDED (Review 5)

Scope signal: L (in a clean session with the right strategy); XL if attempted in-session again.
Specialists: game-designer, systems-designer, qa-lead, ux-designer, ui-programmer, audio-director, economy-designer, unity-specialist, creative-director (senior).
Blocking items: 21 | Recommended: 8 (some deferred from R4 reframed; 3 audio items relocated to audio implementation spec)
Prior verdict resolved: R4 (NEEDS REVISION) — **partial**. R4's closures held for the items addressed, but new structural surface area was opened during the revision pass (the recurring R2→R3→R4→R5 drift pattern).

### Summary

Re-review of R4 in-session revision pass on top of R3 in-session revision pass. Eight specialists + creative-director converged on three structural problems: (1) **F-VP2 event-coverage holes** — Armor INTACT branch silently swallows Steps 3/4 events on the armor slot itself; recursive Hull resolution mis-snapshots `wasCritical`; repair path doesn't fire `OnSlotHpChanged`; plating buffer and status tick changes emit no events. (2) **V&P↔ADR-0007 drift, again** — `ActiveStatuses`, `GetStatModifier`, `AnchorPoint` referenced in GDD but missing from ADR-0007 Key Interfaces; `AnchorPoint` struct lacks `[System.Serializable]` (Unity 6.3 will silently fail to serialize HudAnchor). (3) **Two competing sources of truth for event sequencing** — R4 added the Canonical Event Order Table but did not prune EC-VP20's prose, exactly the normalization-without-cleanup anti-pattern that produced earlier drift.

Creative-director's diagnosis: **the document has outgrown in-session revision.** Every revision pass closes some blockers and introduces new drift. The reviewer (in prior rounds, same conversation) is making local fixes without the full document model in fresh attention. R3 → R4 → R5 in one extended conversation means context-fatigue is now a structural factor.

### Blocking Items (21)

**Event-contract structural bugs (4):**
1. **[systems-designer]** F-VP2 Armor INTACT branch silently swallows events on the armor slot itself when armor absorbs sub-lethal damage — Steps 3/4 skipped, so `OnSlotDamageStateChanged`, `OnArmorExposed`, `OnCriticalStateChanged` never fire.
2. **[game-designer]** Armor recursion mis-snapshots `wasCritical` — outer-call snapshot leaks into recursive Hull resolution, double-firing `OnCriticalStateChanged`. (Distinct bug from #1; see Disagreement 2.)
3. **[ui-programmer]** Repair path (F-VP3) does not fire `OnSlotHpChanged` — hp can change without view-layer notification.
4. **[game-designer]** EC-VP20 still describes event sequencing in prose, creating a second source of truth competing with F-VP2 Canonical Event Order Table.

**View-layer event-contract holes (4):**
5. **[ui-programmer]** No `OnPlatingChanged` event when overflow plating buffer changes.
6. **[ui-programmer]** Status stack-count changes (`TickStatuses`) emit no event.
7. **[ui-programmer]** `OnGrantedCardRemoved` requires card-ID → CardDefinitionSO resolution but no `ICardCatalog` is declared in `IVehicleView` dependencies.
8. **[game-designer]** `OnGrantedCardRemoved` has no delivery contract — does it remove cards-in-hand or only deck?

**R4 closure drift / AC coverage (3):**
9. **[systems-designer]** F-VP1 DegradedThreshold formula still references `ChassisDef.DegradedThresholdPct` (R4 moved authority to FrameLayoutSO but didn't propagate).
10. **[systems-designer]** AC-VP44 (atomic event order) omits `OnGrantedCardRemoved` from required event list.
11. **[game-designer]** Worked recursion example missing from F-VP2 table.

**ADR-0007 drift (3):**
12. **[unity-specialist]** ADR-0007 Key Interfaces omits `ActiveStatuses` and `GetStatModifier(string statId)`.
13. **[unity-specialist]** `AnchorPoint` POCO not re-declared in ADR-0007 Key Interfaces.
14. **[unity-specialist]** `AnchorPoint` struct lacks `[System.Serializable]` — silently breaks Unity inspector serialization under Unity 6.3 fields-only `[SerializeField]` rule.

**AC layer (4):**
15. **[qa-lead]** 9 of 11 R2-era ACs still untestable (R4 downgraded to recommended; R5 creative-director reverses — see Disagreement 1).
16. **[qa-lead]** AC-VP35 (R_ARM exposure multiplier) not falsifiable — no input/output table.
17. **[qa-lead]** AC-VP50b (SafeAmplify NaN/Inf) doesn't specify NaN injection method.
18. **[qa-lead]** No AC covers recursive `wasCritical` correctness; no AC verifies `IGrantedCardData` assembly placement.

**UX / sensory contract (3):**
19. **[ux-designer]** `CriticalState` has no perceptual UI contract — no glow, vignette, audio, HUD treatment for near-death readback.
20. **[ux-designer]** `OnArmorExposed` event has no visual vocabulary defined.
21. **[audio-director]** Amplified-redirect damage (Armor→Hull 3×) has no distinct SFX layer — undermines "exposed = scarier" Player Fantasy.

### Recommended (8)

1. **[ux-designer]** HudAnchor collision policy (downgraded from blocking).
2. **[ux-designer]** combat-hud.md refresh — named follow-on ticket (downgraded from blocking).
3. **[audio-director]** Polyphony cap on `OnSlotDamageStateChanged` audio (relocated to audio implementation spec).
4. **[audio-director]** Plate-shatter + OnSlotHpChanged same-frame mix/ducking rule (relocated).
5. **[audio-director]** Death stinger vs OnVehicleDied music transition overlap (relocated).
6. **[economy-designer]** Granted cards × Card System MaxCopiesInDeck — escalate to Card System GDD owner.
7. Prune EC-VP20 to a pointer at F-VP2 table.
8. Add CI grep gate for V&P↔ADR-0007 interface drift (the recurring pattern).

### Specialist Disagreements (Adjudicated by Creative-Director)

**Disagreement 1 — qa-lead vs R4 creative-director on 9 untestable ACs:**
- qa-lead R5 position: blocking, because an untestable AC is not an AC by definition.
- R4 creative-director: downgraded to recommended.
- **R5 creative-director ruling:** qa-lead is correct on principle. R4's downgrade was a procedural error. **Hybrid resolution:** split each AC into (a) a concrete testable AC with input/output table, OR (b) relocate to a "Design Test / Pillar Alignment" section where qualitative criteria are allowed. Leaving them in the AC section in untestable form is NOT allowed.

**Disagreement 2 — Are systems-designer F1 and game-designer F2 the same bug?**
- **R5 creative-director ruling:** Two distinct bugs sharing one code path. F1 = coverage bug (under-emission, INTACT branch skips Steps 3/4). F2 = correctness bug (over-emission with stale `wasCritical` on recursion). Both need explicit fixes; one fix will not address the other.

### Senior Verdict — Creative Director

> "This is not a verdict on the design — the design is sound. It is a verdict on the document's *integrity*. There are 3 structural bugs in the canonical event order, 4 unobservable-state holes in the view-layer contract, 2 sources of truth competing for event-order authority, 9 still-untestable ACs, and recurring ADR drift that no in-session pass has yet closed durably. R4 did real work but R5 has re-opened enough surface area that another in-session pass will almost certainly repeat the R2→R3→R4 pattern.
>
> **Pattern observation:** Every revision pass closes some blockers and introduces new V&P↔ADR-0007 drift. The document has outgrown in-session revision. R3→R4→R5 in one extended conversation means context-fatigue is now a factor in *why* drift keeps appearing.
>
> **Strategic recommendation:** Stop here. Do not attempt R6 in this conversation. Switch from 'revise the GDD' to 'amend the ADR + add an event-contract appendix' so the ADR becomes the single source of truth for interfaces. Open a fresh session. Sequence fixes: ADR amendment first → event-order appendix → AC rewrite → UX contracts last."

Gate: `[GATE-CD-SYNTHESIS]: MAJOR REVISION NEEDED`

### User Decision

User accepted creative-director's stop-and-switch strategy. R6 deferred to a fresh session with the ADR-first sequence plan. R5 findings logged here as handoff. ADR-0007 amendment scope doc to be drafted at `production/r5-handoff-adr-0007-amendment-scope.md` for next-session pickup.

### Files Updated by This Review

- `design/gdd/systems-index.md` — Row 2 status REVISED (R4) → NEEDS REVISION (R5, 2026-05-18); header updated.
- `production/r5-handoff-adr-0007-amendment-scope.md` — new file; ADR-0007 amendment scope for the next-session R6 attempt.
- `design/gdd/reviews/vehicle-and-part-system-review-log.md` — this entry.
- `production/session-state/active.md` — updated to reflect R5 verdict + stop-and-switch decision.

No edits to `design/gdd/vehicle-and-part-system.md` or `docs/architecture/adr-0007-frame-driven-variable-slot-system.md` in this session — those are deferred to the next-session structural pass per creative-director's recommendation.

---

## Review — 2026-05-19 — Verdict: MAJOR REVISION NEEDED (Review 6)

Scope signal: L — one revision pass crossing V&P GDD + ADR-0007 + Card System GDD across 4 edit clusters.
Specialists: game-designer, systems-designer, ux-designer, ui-programmer, audio-director, economy-designer, unity-specialist, creative-director (senior).
Blocking items: 8 | Recommended: ~25 (across all specialists) | Specialist disagreements: 1 (resolved per creative-director)
Prior verdict resolved: R5 (MAJOR REVISION NEEDED) — **partial closure**. The 21 R5 blockers were closed at the spec layer across Phases 1-4 of the stop-and-switch handoff (Phase 1: ADR-0007 Decisions 12-16 + Key Interfaces; Phase 2: V&P GDD cleanup + ADR-0006 amendment; Phase 3: AC layer cleanup + 7 new ACs; Phase 4: 3 UX/audio seed entries + 1 sprint ticket). However, R6 surfaced 8 new structural blockers — the R5 ledger closed, but the revision pass introduced fresh drift.

### Summary

Six adversarial specialists + senior creative-director rendered verdict on the post-Phase-4 state. R6 was expected to be **APPROVED** (or **NEEDS REVISION** with minor items only). Instead, 8 blocking issues emerged, clustered around four edit groups:

1. **U6 staleness / Decision 16 propagation gap** — four-specialist convergence (game-designer, systems-designer, ux-designer, ui-programmer). U6 (line 1691) + AC-VP21 THEN clause + stale line 545 all describe hard-removal of granted cards on Offline, directly contradicting Decision 16's soft-disable semantics. This is the priority blocker.

2. **ADR-0007 spec-layer compile/serialization bugs** — unity-specialist found two compile/serialization errors in the ADR Key Interfaces block authored in Phase 1: `SlotDefinition` declared with `{ get; }` property syntax (violates ADR's own line-40 fields-only rule and Unity 6.3 `[SerializeField]` rule — silent serialization data loss); `AnchorPoint.IsInUnitRect { get; }` declared with no body (will not compile in C#).

3. **Economy formula and ceiling gaps** — economy-designer found InstallCost formula defined for `installedCount ∈ 0..4` only; at Heavy (7 slots) and Dredge (10 slots) the formula produces ~125 Scrap for a Rare install vs ~50 baseline — economy collapses on the chassis variable-slot was created to enable. Separately, Dredge 10 slots × 3 cards = 30 granted + 10 starter = 40-card deck; Card System GDD does not publish MaxDeckSize.

4. **Lifecycle and audio same-frame contracts unowned** — ui-programmer found HUD event subscription/unsubscription lifecycle unspecified across V&P GDD, ADR-0007, AND combat-hud.md. With 12 events on engine-free POCO Vehicle, this is a guaranteed Vehicle leak on scene reload. audio-director found three same-frame death-cue ordering contradictions: OnVehicleDied fires before OnArmorExposed and OnCriticalStateChanged(true); plate-shatter cue fires after `IsDead == true`; CriticalState entry stinger fires on a dead vehicle. No arbiter rule exists.

Creative-director's diagnosis: "Phases 1-4 closed the R5 surface but introduced new drift on two axes: Decision 16 landed in ADR but did not propagate to U6/AC-VP21/line 545; ADR-0007 Key Interfaces was authored with property syntax despite ADR's own line-40 rule. R5 closure was real but partial."

### Blocking Items (8)

1. **[game-designer + systems-designer + ux-designer + ui-programmer B2 — convergence]** **U6 (line 1691) + AC-VP21 THEN clause + stale line 545** all describe hard-removal of granted cards on Offline — directly contradicts Decision 16 soft-disable. Priority blocker. Rewrite to match Decision 16: cards stay in deck/hand/discard on Offline, dimmed via `SourceSlotId` playability gate; hard removal only on scrap or external-source termination.

2. **[unity-specialist B1]** **ADR-0007 `SlotDefinition` (lines 770-779) uses property-backed members** — will not serialize under Unity 6.3 `[SerializeField]` fields-only rule. Contradicts ADR's own Engine Compatibility note (line 40). Compile-clean but silent data loss in FrameLayoutSO inspector. **Spec-layer fix in ADR-0007, not V&P.**

3. **[unity-specialist B2]** **ADR-0007 `AnchorPoint.IsInUnitRect { get; }`** declared with no body — will not compile in C#. Spec-layer fix in ADR-0007.

4. **[economy-designer BLOCK-1]** **InstallCost formula domain breaks on Heavy (7 slots) and Dredge (10 slots)** — formula defined for `installedCount ∈ 0..4`; at 10 slots Rare install costs ~125 Scrap vs ~50 baseline. R5 normalization recommendation unimplemented. **Blocks ADR-0007 Decisions 12-16 from being economically coherent.**

5. **[audio-director B1/B2/B3]** **Same-frame death cue ordering produces three audio contradictions** — OnVehicleDied fires BEFORE OnArmorExposed and OnCriticalStateChanged(true) with no arbiter; plate-shatter cue fires AFTER `IsDead == true` with no suppression rule; CriticalState entry stinger fires on a dead vehicle. V&P audio table (line 1605-1614) has no ducking rule.

6. **[ui-programmer B1]** **HUD event subscription lifecycle completely unspecified** across V&P GDD, ADR-0007, AND combat-hud.md. 12 events on engine-free POCO Vehicle + `MonoBehaviour` HUD without documented unsubscribe contract = guaranteed Vehicle leak on scene reload. No owner doc assigned.

7. **[economy-designer BLOCK-2]** **Granted-card deck ceiling unconstrained at layout scale** — Dredge 10 slots × 3 cards = 30 granted + 10 starter = 40-card deck. Card System GDD does not publish MaxDeckSize. R5 deferred item #16 still open.

8. **[ux-designer + audio-director A1]** **U8 accessibility floor incomplete** — DamageState (Functional/Degraded/Offline) has no colorblind palette beyond numeric HP. 6 of 7 P0/P1 audio cues have no caption coverage spec. `accessibility-requirements.md` §4 forbids audio-only decision-critical channels.

### Recommended Revisions (selected — full list in specialist outputs)

- **[ui-programmer B3]** combat-hud.md channels 17/18 reference removed `VehicleState.CurrentArmor/MaxArmor` — won't compile against `IVehicleView`. Owned by combat-hud-refresh ticket (Phase 4), but V&P currently defers to a stale dep.
- **[ui-programmer B4]** HudAnchor collision policy + 10-slot Dredge scroll threshold unspecified. V&P defers to Combat HUD GDD; Combat HUD GDD has no such section.
- **[systems-designer]** DegradedThresholdPct range incoherence (1..100 vs 1..99 across docs).
- **[systems-designer]** CombatsSurvived idempotency guard missing (R11 doesn't guard against double-fire on scene reload — also flagged by unity-specialist DET-1).
- **[systems-designer]** F-VP3 empty-product identity not stated.
- **[ui-programmer G1]** 100ms SLA (line 1601) not verifiable — replace wall-clock with frame-relative contract.
- **[ui-programmer G4]** Exposed Armor slot tooltip content unspecified (`× N` badge).
- **[unity-specialist DET-2/3]** `GetParts` return order unspecified; `DamageContext` allocation strategy unspecified.
- **[unity-specialist SER-1/2/3]** `VehicleStateDTO.SchemaVersion` constant not declared in GDD or ADR; AC-VP33b migration spec confuses `VehicleStateDTO` V1 vs `SlotStateDTO` V1; `ArmorContribution` IL2CPP `link.xml` preservation not addressed.
- **[unity-specialist BOUND-1/2/3, ADV-3]** Assembly placement for `ICardCatalog` impl, `VehicleStatePreview`, `CardPositionRequirement` (cycle risk); `DamageContext` referenced extensively but never formally declared.
- **[unity-specialist ADV-1]** AC-VP54 CI tool unimplemented — boundary invariant unenforced until tooling sprint ships.
- **[audio-director R1-R7]** Polyphony floor guarantee, bus assignments, sub-revival repair cue coverage, `OnCriticalStateChanged(false)` recovery stinger row, mutual CriticalState dual stinger, break stinger spatial anchor, dual-Armor breakthrough clipping all underspecified.
- **[economy-designer REC-1 through REC-5, GAP-1 through GAP-5]** Scrap-rotate exploit analysis missing; fuel overflow caller-optional; stat modifier stacking uncapped; CombatsSurvived tenure loss intent unstated; Armor parts in catalog have no drop path; SlotPosition filter missing from `GetParts`; L&R D.5 slot enumeration static; ArmorContribution deprecation has no removal target; Truck cross-balance unmodeled.

### Specialist Disagreement (Adjudicated)

**Is combat-hud.md staleness a V&P GDD blocker or a separate ticket?**
- **[ui-programmer]** Treats as a V&P blocker (channels 17/18 reference removed surface).
- **[creative-director adjudication]** V&P blocker BY REFERENCE, not by ownership. The combat-hud-refresh ticket (Phase 4) is the right home for the fix, but V&P currently ships a contract that points at a stale dependency. Holding V&P until the refresh ticket lands is cleaner than promoting with a broken downstream pointer.

### Senior Verdict — Creative Director

> "Phases 1-4 closed the R5 surface but introduced new drift on two axes. Decision 16 landed in ADR but did not propagate to U6/AC-VP21/line 545; ADR-0007 Key Interfaces was authored with property syntax despite ADR's own line-40 rule. The convergence point (Decision 16) means most blockers cluster around two edits — one V&P pass and one ADR-0007 amendment. **Verdict stays MAJOR REVISION NEEDED** because the scope crosses three documents and includes compile-blocking spec errors, not minor item polish."

Gate: `[GATE-CD-GDD-ALIGN]: REJECT`

### Edit Clusters for R7 Entry

- **Cluster A (U6/AC-VP21/line 545/U8 caption table/U8 palette):** mechanical text fixes — ~1 session. Closes Blockers 1, 5 (partial), 8.
- **Cluster B (ADR-0007 amendment):** struct syntax, AnchorPoint body, SchemaVersion constant, link.xml note, assembly homes, DamageContext declaration. ~1 session. Closes Blockers 2, 3 + spec-layer recommended items.
- **Cluster C (InstallCost + MaxDeckSize):** formula renormalization + Card System GDD MaxDeckSize publish. Needs game-designer + economy-designer review. ~1 session. Closes Blockers 4, 7.
- **Cluster D (event lifecycle + audio ordering):** assign owner doc, write contract. ~0.5 session. Closes Blockers 5 (rest), 6.

### Validation Criteria for R7 Entry

- U6 + AC-VP21 + line 545 match Decision 16 verbatim (soft-disable model).
- ADR-0007 Key Interfaces compile (SlotDefinition fields, AnchorPoint properties have bodies).
- InstallCost formula validates at installedCount=10 (worked example).
- Audio U8 specifies same-frame death-cue ordering and `IsDead` suppression rules.
- Event subscription contract has a named owner doc.

### User Decision

User accepted creative-director's defer-to-fresh-session recommendation (first time across R2/R3/R4/R5/R6 sequence that the user has NOT overridden the defer recommendation). R7 to be attempted in a fresh session with the four edit clusters tackled in sequence — likely Cluster B (ADR-0007) first since spec-layer fixes unblock implementation.

### Files Updated by This Review

- `design/gdd/systems-index.md` — Row 2 status NEEDS REVISION (R5) → NEEDS REVISION (R6); header Last Updated bumped to 2026-05-19.
- `design/gdd/reviews/vehicle-and-part-system-review-log.md` — this entry.

No edits to `design/gdd/vehicle-and-part-system.md`, `docs/architecture/adr-0007-frame-driven-variable-slot-system.md`, or any Phase 4 seed entries in this session — all deferred to the R7 fresh-session structural pass.

---

## Review 7: 2026-05-19 (Full — Adversarial Specialist + Senior Synthesis)

**Verdict**: MAJOR REVISION NEEDED — **STRUCTURAL SPLIT INVOKED**

**Scope signal**: XL — multi-cluster structural revision requiring 2 new GDD files, ADR-0007 contract amendment, frame-ordering convention authoring, ~32 findings across 8 domains, plus cross-doc orphan repairs in `scrap-economy.md` and `accessibility-requirements.md`.

**Specialists consulted**: game-designer, systems-designer, qa-lead, ux-designer, ui-programmer, audio-director, economy-designer, unity-specialist, creative-director (senior synthesis).

**Blocking items**: 27 across 8 specialist domains (4 P0 definitional contradictions, 4 P0 same-frame ordering holes, 4 P0 cross-document orphans, 4 P1 formula boundary holes, 4 P1 acceptance criteria defects, 7 P1 UX/UI specification holes).
**Recommended**: 2 (AnchorPoint.IsFinite doc debt, MaxDeckSize post-EA guard).

### Session Context

R7 revision pass completed all 4 R6 clusters (B: ADR-0007 spec fixes; A: V&P U6/AC-VP21/U8; C: InstallCost renorm + MaxDeckSize; D: lifecycle + audio death-frame ordering). Pre-review sanity sweep this session uncovered and patched 6 additional stale Cluster A references (lines 493, 721, 732–733, 734, 748, AC-VP24). All 8 R6 blockers verified closed before adversarial review.

### Adversarial Specialist Findings (Top 5 synthesized across all 8)

1. **SlotDefinition dual canonical declaration** [unity-specialist BLOCKER-1] — Lines 144–165 declare immutable POCO; lines 771–781 Key Interfaces declare mutable public-field struct (R7 Cluster B Unity 6.3 [SerializeField] fix). Two contradictory contracts in same document. Highest severity finding.
2. **Same-frame ordering ungoverned across 4 domains** [game-designer B2, ux-designer B2, audio-director B1/B3, ui-programmer B3] — Plate→0 + Hull→0; OnArmorExposed VFX + OnPlatingChanged VFX; OnGrantedCardRemoved subscriber order; plate-shatter pre-roll vs. vehicle-death cue. Four independent specialists found the same class of hole — signals missing global convention.
3. **IsPlayable field referenced by 3 ACs, never declared** [qa-lead 1+4 cascade] — AC-VP21, AC-VP24, AC-VP44d dangle on a missing model field.
4. **Cross-document orphan references in 3 directions** [economy-designer BLOCK-1, ux-designer B4, audio-director B4, systems-designer B5] — BaseSalvageValue/TenureBonus not in scrap-economy.md vocabulary; accessibility-requirements.md §6 doesn't exist; OnSlotHpChanged not in V&P audio table; CriticalState formula referenced but never declared.
5. **Formula boundary + dominant-strategy holes** [systems-designer B1–B5, economy-designer BLOCK-2] — DegradedThresholdPct=100 undefined; DefaultSlotMaxHp=0 div-by-zero; SafeAmplify=0 silent rewrite; installedCount > totalSlots unclamped; InstallCost renormalization creates "install commons last" as dominant strategy.

### Creative-Director Senior Synthesis

**Drift-pattern read**: R5→R6→R7 is not iterative convergence — it is stable oscillation around an unstable artifact. Three signals confirm this is structural, not content:
- Finding count is increasing, not decreasing (8 → 32).
- New blockers appear in sections that R7 revisions didn't directly touch (silent invariant breakage from coupling).
- Same defect class recurs (R5 "U-row drift" → R6 "U6 wording" → R7 "U6 wording, different flavor").

**Strategic recommendation**: Structural split per R5 precedent, executed in a fresh session — do NOT continue in-session revision. Split into:
- `design/gdd/vehicle-and-part-architecture.md` (~600 lines, code-facing) — slot model (single canonical SlotDefinition), AnchorPoint, chassis schema, F-VP1/F-VP2/F-VP3 with declared boundary domains, validation gates, **frame-ordering convention as top-level section**, data-authoring rules. **ADR-0007 Accepted gates on this doc only.**
- `design/gdd/vehicle-and-part-mechanics.md` (~800 lines, design-facing) — damage states, soft-disable lifecycle, HUD UX, accessibility hooks, audio cues, economy integration, granted-card lifecycle. Iterates independently.

**Producer math**: 7 iterations in 5 weeks on a single doc with no convergence trajectory. One fresh-session split (~1 session) costs less than R8 (~1 session + strong prior R9 will be needed). Architecture doc unblocks ADR-0007 → W2 wave on faster trajectory than monolith-R8.

### User Decision

User accepted creative-director's structural-split recommendation. Next session opens with V&P GDD split execution (after /clear). This is the second consecutive session where the user has accepted the defer/split recommendation (first was R6→R7 defer to fresh session).

### Files Updated by This Review

- `design/gdd/systems-index.md` — Row 2 status NEEDS REVISION (R6) → MAJOR REVISION NEEDED (R7) + SPLIT PENDING; header Last Updated rewritten with R7 split-pending summary; design doc cell suffixed `→ SPLIT`.
- `design/gdd/reviews/vehicle-and-part-system-review-log.md` — this entry.
- `production/session-state/active.md` — pending update to capture R7 split decision and fresh-session pickup plan.

No edits to `design/gdd/vehicle-and-part-system.md` or `docs/architecture/adr-0007-frame-driven-variable-slot-system.md` in this Phase 4 step — split execution deferred to the next fresh session.

### Forward Pickup (Fresh Session)

1. `/clear` to discard the R7 review session context.
2. Read `production/session-state/active.md` and this Review 7 entry to recover.
3. Author `design/gdd/vehicle-and-part-architecture.md` skeleton first (8 required sections + frame-ordering convention as top-level), then fill section by section with approval between sections (per `.claude/rules/design-docs.md` incremental authoring rule).
4. Author `design/gdd/vehicle-and-part-mechanics.md` skeleton next; mark as "Draft — gates on architecture-doc Approved."
5. `/design-review design/gdd/vehicle-and-part-architecture.md` once complete. On APPROVED → ADR-0007 Proposed → Accepted → W2 unblocks.
6. Mechanics doc iterates in parallel without blocking W2.
7. Original `vehicle-and-part-system.md` retained as historical artifact; do not edit or revive.


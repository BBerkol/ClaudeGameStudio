# R5 Handoff — ADR-0007 Amendment Scope (Next-Session Pickup)

**Date**: 2026-05-18
**Source**: V&P GDD Review 5 (`design/gdd/reviews/vehicle-and-part-system-review-log.md`)
**Verdict closed**: MAJOR REVISION NEEDED (21 blockers)
**Strategy**: Stop-and-switch — ADR-first structural pass instead of another in-session GDD revision

---

## Why This File Exists

R2 → R3 → R4 → R5 produced a recurring pattern: every in-session revision closes blockers and introduces new V&P↔ADR-0007 drift. Creative-director's diagnosis is that the document has outgrown conversational patching. This handoff captures the R5 findings in a sequence-ordered scope so the next session can pick up from a fresh context without re-deriving the punch list.

**Open the next session with `/clear`, then read in this order:**
1. This file (scope + sequence)
2. `design/gdd/reviews/vehicle-and-part-system-review-log.md` Review 5 entry (full findings)
3. `docs/architecture/adr-0007-frame-driven-variable-slot-system.md` (current state)
4. `design/gdd/vehicle-and-part-system.md` (current state)

Do NOT attempt all four phases in one session. Each phase below is its own session-sized unit of work.

---

## Strategy Reframe

**Old approach** (R2-R5): GDD is the source of truth for interfaces; ADR is a parallel doc that must be kept in sync. **Result**: every GDD edit re-opens ADR drift.

**New approach** (R6+): **ADR-0007 is the single source of truth for the interface layer** (events, key interfaces, POCOs, `[Serializable]` declarations, engine-boundary contracts). The GDD *references* the ADR rather than restating it. Sections that currently restate ADR contracts in the GDD get pruned to "see ADR-0007 §X."

This makes drift mechanically impossible — there is only one place to edit.

---

## Sequenced Work Plan

### Phase 1 — ADR-0007 Amendment (do this FIRST, in its own session)

**Scope**: Make ADR-0007 the complete, authoritative source for the interface layer. Once Phase 1 lands, the GDD edits in Phase 2 become a series of *deletions* (replacing GDD restatements with ADR pointers), not new authoring.

**Tasks**:

1. **Add missing interfaces to ADR-0007 Key Interfaces section:**
   - `ActiveStatuses` property on `IVehicleView` (referenced in F-VP2 step (e), R9 — missing from ADR)
   - `GetStatModifier(string statId)` method on `IVehicleView` (referenced in F-VP2, R9 — missing from ADR)
   - `OnPlatingChanged` event on `IVehicleView` (new — addresses R5 blocker #5)
   - `OnStatusStackChanged` event on `IVehicleView` (new — addresses R5 blocker #6)
   - `ICardCatalog` declaration as a dependency the view layer requires for `OnGrantedCardRemoved` resolution (R5 blocker #7)
   - `IGrantedCardData` engine-free type explicitly placed in `WastelandRun.Vehicle` assembly with assembly-placement clause (R5 blocker #18)

2. **Re-declare AnchorPoint POCO in ADR-0007 Key Interfaces** (R5 blocker #13):
   ```csharp
   [System.Serializable]   // CRITICAL: Unity 6.3 fields-only [SerializeField] rule
   public struct AnchorPoint {
       public float X;     // unit rect [0,1]
       public float Y;     // unit rect [0,1]
   }
   ```
   Add a `[System.Serializable]` annotation requirement note: without it, `HudAnchor` on `SlotDefinition` silently fails to serialize in the Unity inspector under Unity 6.3 (R5 blocker #14).

3. **Add Decision 12 — Repair-path event emission:**
   - Repair path MUST fire `OnSlotHpChanged` (R5 blocker #3)
   - Spec'd the same way as damage path: emit on every hp delta, including +deltas

4. **Add Decision 13 — Canonical Event Order Single Source of Truth:**
   - Declare that the F-VP2 Canonical Event Order Table is the canonical sequencing spec
   - Add CI lock-step requirement: ADR Decision 11 invariants and V&P F-VP2 table must match exactly (CI grep gate)
   - Note: this is the cleanup that will let us prune EC-VP20's prose in Phase 2 (R5 blocker #4)

5. **Add Decision 14 — Armor INTACT branch event emission (R5 blocker #1):**
   - When Armor absorbs damage but `Hp > 0`, the armor slot itself MUST run Steps 3/4
   - Provide worked example: armor slot transitions `Functional → Degraded` while still absorbing — must fire `OnSlotDamageStateChanged` AND `OnCriticalStateChanged` if applicable
   - This is a structural correction, not a polish item — the canonical event-order spec is currently incomplete

6. **Add Decision 15 — Recursive wasCritical snapshot (R5 blocker #2):**
   - `wasCritical` MUST be captured at the top of each `ApplyDamageToSlot` invocation (including recursive calls)
   - Worked example showing armor break → Hull redirect path with two distinct `wasCritical` snapshots
   - Distinct from Decision 14 — that's about under-emission on the INTACT path; this is about over-emission on the recursive path

7. **Add Revision History entry** at the top of ADR-0007 documenting Phase 1 amendment (date, decisions 12-15 added, interfaces added, AnchorPoint annotation).

**Phase 1 exit criteria:**
- ADR-0007 is the complete spec for the V&P interface layer
- All R5 blockers #1-7, #11-14 are addressed *within ADR-0007 alone* (no GDD edits yet)
- ADR Revision History entry written
- CI lock-step grep gate spec'd

**Estimated effort**: 1 focused session (S–M).

---

### Phase 2 — V&P GDD Cleanup (do this SECOND, separate session)

**Scope**: Prune GDD restatements; replace with ADR pointers. This is *deletion* work, not authoring.

**Tasks**:

1. **Prune R9 Key Interfaces in V&P GDD** — replace `IVehicleView` / `IVehicleMutator` declarations with: "See ADR-0007 Key Interfaces §X for the canonical interface declaration. The GDD references members by name only."

2. **Prune EC-VP20 prose** (R5 blocker #4) — replace with: "See F-VP2 Canonical Event Order Table for the authoritative event sequencing. Subscriber rules are documented in the table footnotes."

3. **Fix F-VP1 DegradedThreshold formula** (R5 blocker #9) — `ChassisDef.DegradedThresholdPct` → `frameLayout.DegradedThresholdPct`. Propagate R4 closure.

4. **Update F-VP2 Canonical Event Order Table:**
   - Add Step row for Armor INTACT branch with explicit Steps 3/4 emission on armor slot (per ADR Decision 14)
   - Add worked recursion example showing two `wasCritical` snapshots (per ADR Decision 15, R5 blocker #11)
   - Add Step row for repair path emitting `OnSlotHpChanged` (per ADR Decision 12)

5. **Update AC-VP44** — add `OnGrantedCardRemoved` to the required event list (R5 blocker #10).

6. **Define `OnGrantedCardRemoved` delivery contract** (R5 blocker #8) — does it remove cards-in-hand or only deck? Single sentence answer. (Open question: this requires a user design decision.)

**Phase 2 exit criteria:**
- GDD no longer restates ADR-0007 interfaces — only references them
- F-VP2 table reflects ADR Decisions 12-15
- F-VP1 closure drift fixed
- AC-VP44 coverage hole closed
- `OnGrantedCardRemoved` delivery scope locked

**Estimated effort**: 1 session (S).

**User decision required before this phase starts**: `OnGrantedCardRemoved` cards-in-hand scope.

---

### Phase 3 — AC Rewrite (do this THIRD, separate session)

**Scope**: Address qa-lead's 9 R2-era untestable ACs per creative-director's hybrid ruling.

**Tasks**:

1. **Audit the 9 R2-era ACs** flagged by qa-lead. For each:
   - Option A: rewrite as concrete testable AC with input/output table
   - Option B: relocate to a new "Design Test / Pillar Alignment" section where qualitative criteria are allowed
   - Option C (forbidden): leave in AC section in untestable form

2. **Add new ACs** for R5-surfaced coverage gaps:
   - AC for recursive `wasCritical` correctness (R5 blocker #18 part 1)
   - AC for `IGrantedCardData` assembly placement (R5 blocker #18 part 2)
   - AC for repair path `OnSlotHpChanged` firing (covers R5 blocker #3 via test)
   - AC for `OnPlatingChanged` firing (covers R5 blocker #5 via test)
   - AC for `OnStatusStackChanged` firing (covers R5 blocker #6 via test)
   - AC for Armor INTACT branch event emission on the armor slot (covers R5 blocker #1 via test)
   - AC for Armor recursion fresh wasCritical snapshot (covers R5 blocker #2 via test)
   - AC for AC-VP35 R_ARM exposure multiplier with concrete input/output table (R5 blocker #16)
   - AC for AC-VP50b SafeAmplify NaN/Inf with specified injection method (R5 blocker #17)

3. **Create "Design Test / Pillar Alignment" section** in the GDD if it doesn't exist, as the destination for relocated qualitative criteria.

**Phase 3 exit criteria:**
- Zero untestable ACs in the Acceptance Criteria section
- All R5-surfaced coverage gaps closed via new ACs
- Qualitative criteria relocated, not deleted (Pillar Alignment preserved)

**Estimated effort**: 1 session (M).

---

### Phase 4 — UX/Sensory Contract Authoring (do this FOURTH, separate session — depends on Phase 1)

**Scope**: Close the perceptual contract for CriticalState, OnArmorExposed, and amplified-redirect damage. **These do NOT live in V&P GDD** — they seed entries elsewhere.

**Tasks**:

1. **Seed entry in `design/ux/`** for CriticalState perceptual contract (R5 blocker #19):
   - Specify glow / vignette / audio cue / HUD treatment for near-death state
   - Tie to `OnCriticalStateChanged` event
   - Location: new file `design/ux/critical-state-feedback.md` OR section in existing combat-hud authorship doc

2. **Seed entry in `design/ux/`** for OnArmorExposed visual vocabulary (R5 blocker #20):
   - "Armor just broke and you are now exposed" visual specification
   - Tie to `OnArmorExposed` event firing

3. **Seed audio spec entry** for amplified-redirect SFX layer (R5 blocker #21):
   - Distinct audio signature for Armor→Hull at 3× multiplier
   - Location: audio implementation spec (when it exists) OR `design/audio/amplified-redirect-sfx.md`

4. **Create combat-hud.md refresh ticket** (R5 recommended #2) — name it explicitly, don't leave as "deferred." Track as a sprint item, not a perpetual recommendation.

**Phase 4 exit criteria:**
- UX entries seeded for CriticalState and OnArmorExposed
- Audio entry seeded for amplified-redirect
- Named combat-hud refresh ticket exists

**Estimated effort**: 1 session (M).

---

## Cross-Doc Items (Not in V&P Scope — Escalations)

These came up in R5 but belong elsewhere. Track but do not fold into V&P phases:

- **[economy-designer]** Granted cards × Card System `MaxCopiesInDeck` rule — escalate to Card System GDD owner. Question: do granted cards count toward the deck copy limit? V&P emits the event; Card System owns the rule.

---

## Re-Review Strategy

After all 4 phases land (in separate sessions, with `/clear` between each), run:

```
/design-review design/gdd/vehicle-and-part-system.md
```

**This will be Review 6.** Expectation: APPROVED or NEEDS REVISION with minor items only — the structural rework should close all 21 R5 blockers if the phase sequence is followed.

**If R6 still returns MAJOR REVISION NEEDED**, that is a signal that the GDD itself may need structural reorganization (e.g., split into V&P-Architecture and V&P-Mechanics docs). Don't attempt R7 in-session.

---

## Optional: CI Grep Gate

Add a CI check for V&P↔ADR-0007 drift (R5 recommended #8):
- Grep V&P GDD for interface declarations (`interface I*`, event signatures, struct decls)
- Grep ADR-0007 for the same
- Diff — if any V&P interface declaration doesn't exist in ADR-0007, fail CI
- This is the structural prevention for the recurring R2-R5 drift pattern

Spec this as part of Phase 1 or defer to a tooling sprint.

---

## Summary for Next-Session Pickup

**Open new session. `/clear` first.**

**First command**: Read this file in full.
**Second command**: Read `design/gdd/reviews/vehicle-and-part-system-review-log.md` Review 5.
**Third command**: Confirm scope with user, then begin Phase 1 (ADR-0007 amendment) in its own session.

Do NOT attempt Phases 1+2 in the same session. Do NOT skip phases. The whole point of the strategy switch is *fresh context per phase*.

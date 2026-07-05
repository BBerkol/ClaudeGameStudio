---
date: 2026-07-04
verdict: KEEP-AND-CLOSE (port already landed 2026-06-23; treat this as closeout, not a decision)
question: Do we port CombatOutcomeOverlay + CardRewardPicker from UGUI to UI Toolkit for 1.0?
scope_context: production/audits/2026-07-04-1.0-vision-snapshot.md
adrs_touched: ADR-0014 (status line update), ADR-0011 (verified clean)
supersedes_memory: project_post_combat_flow_exists_ugui.md (STALE — port already landed)
---

# TD Verdict — UI Toolkit Post-Combat Port

## Verdict

**KEEP-AND-CLOSE.** The port already shipped on 2026-06-23. This consult is a
book-keeping slice: update ADR-0014 status line, delete two stale doc-refs,
purge the stale memory note. Do NOT slot ahead of Slice A — 30-min cleanup,
not a slice.

## Evidence the port shipped

- `Assets/Prefabs/CombatView/CardRewardPicker.prefab` (2026-06-23) and
  `CombatOutcomeOverlay.prefab` (2026-06-23) are **UIDocument-only** —
  three components each: GameObject / Transform / UIDocument /
  `WastelandRun.UI.*Controller`. Zero Canvas, zero RectTransform, zero
  Graphic Raycaster.
- `Assets/UI/CardRewardPicker.uxml` + `.uss` and
  `Assets/UI/CombatOutcomeOverlay.uxml` + `.uss` — with ADR-0014 design-token
  imports (`Assets/UI/Tokens/tokens.*.uss` and `controls.uss`).
- `CombatHud._outcomeOverlay` / `_rewardPicker` are typed to
  `WastelandRun.UI.CombatOutcomeOverlayController` /
  `CardRewardPickerController`. The UIDocument controllers ARE the live path
  — there is no runtime fallback to a UGUI implementation.
- EditMode tests exist for both controllers
  (`Tests/EditMode/UI/CardRewardPickerController_Test.cs` +
  `CombatOutcomeOverlayController_Test.cs`). Port carries test coverage.
- No live UGUI post-combat code found. The two occurrences of the word
  "legacy" in the controllers are xmldoc comments naming the retired UGUI
  class they replaced ("mirrors the legacy CombatView.CombatOutcomeOverlay
  so CombatHud's wiring carries over"), not a live bimodal branch.

## Why the note said "outstanding debt"

The memory note `project_post_combat_flow_exists_ugui` predates the
2026-06-23 landing and was never retired. This consult flushed it out —
purge on close.

## ADR alignment (verified)

- **ADR-0014** — Compliant. All ADR-0014-primary surfaces (HUD, Map, Menu,
  Mastery, Options, Overlay, Picker) now ship on UI Toolkit. UGUI is scoped
  to world-space Popups only.
- **ADR-0011** (no bridges) — Compliant. No adapter layer, no parallel
  UGUI/UI-Toolkit storage, no bimodal path, no compat overload, no stub
  return. The two `<see cref="…"/>` xmldoc references pointing at retired
  class names (`CardWidget.cs:281,291`, `PilePopupWidget.cs:41`) are
  broken doc links, not bridges — the runtime code path is single.
- **ADR-0002** (no UnityEvent in combat) — Compliant. Controllers use
  `event Action` for `OnContinueRequested` / `OnRestartRequested` /
  `OnPickResolved`.

## Closeout tasks (before Slice A — ~30 min total)

1. **Purge stale memory note.** Delete `project_post_combat_flow_exists_ugui.md`
   from user memory and drop its `MEMORY.md` line. It is actively
   wrong and will mislead a future TD brief.
2. **Update ADR-0014 status line.** Migration table entry "P3 picker+overlay"
   → mark Landed (2026-06-23). Reference prefab GUIDs so future auditors
   can grep.
3. **Repoint two stale xmldoc references** (5-min sweep):
   - `Assets/Scripts/CombatView/CardWidget.cs:281,291` — retarget
     `<see cref="CardRewardPicker"/>` to `CardRewardPickerController`.
   - `Assets/Scripts/CombatView/PilePopupWidget.cs:41` — retarget
     `<see cref="CombatOutcomeOverlay"/>` to
     `CombatOutcomeOverlayController`.

None of these are slice-worthy — bundle into the next housekeeping commit
or fold into the codebase-health audit response.

## Risks

- **Silent audit regression.** If ADR-0014 status line is not updated,
  the 2026-07-04 codebase-health audit will re-flag the port as
  outstanding — false positive that costs a re-consult.
- **Memory drift into future consults.** Every hour the
  `post_combat_flow_exists_ugui` note stays in memory is another hour
  of risk that a future TD/audit reads it and briefs off stale state.
  Highest-value 30 seconds in this slice is the memory purge.
- **Test evidence.** Verdict assumes `CardRewardPickerController_Test` +
  `CombatOutcomeOverlayController_Test` are EditMode-green. If they are red,
  the port isn't actually done and this verdict flips to "finish the port
  first" — but that is still not a decision, it's execution.

## What this verdict is NOT

- Not a re-decision on UI Toolkit vs UGUI. That was ADR-0014.
- Not a schedule shift. Nothing here blocks or accelerates Slice A.
- Not a scope change. 1.0 shape is unchanged.

## Success criteria (we'll know this was right if)

- The codebase-health audit does NOT flag CombatOutcomeOverlay /
  CardRewardPicker as ADR-0014 debt.
- No future TD brief references `project_post_combat_flow_exists_ugui`.
- ADR-0014's migration table has P3 marked Landed with a date + prefab
  GUID reference.
- The audit finds zero live UGUI code paths outside the world-space
  Popups exception.

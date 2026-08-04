---
date: 2026-08-02
system: Combat HUD — DebugStats + CombatLog dropdown removal
files_touched:
  - Assets/Scripts/CombatView/CombatHud.cs
  - Assets/Scripts/CombatView/CombatController.cs
  - Assets/Editor/CombatPrefabAuthor.cs
  - Assets/Scripts/CombatView/DebugStatsWidget.cs (DELETE)
  - Assets/Scripts/CombatView/CombatLogWidget.cs (DELETE)
  - Assets/Prefabs/CombatView/DebugStats.prefab (DELETE)
  - Assets/Prefabs/CombatView/CombatLog.prefab (DELETE)
  - Assets/Prefabs/CombatView/CombatHud.prefab (re-authored)
adrs_at_risk: []
predecessor_commit: 3ec0886
---

# DebugStats + CombatLog dropdown removal — 2026-08-02

## Overview

User: "want the stats and the log dropdowns to go away please, i keep
collapsing them loosing me time. dont need them anymore."

Both widgets are top-left designer debug overlays introduced during
early combat scaffolding. They're pure debug UI (per-slot Hp/Corroded/
DamageState dump + rolling event log), never a shipping feature.
User surface them by clicking a toggle each session — friction with
zero payoff since the same info now lives on the vehicle HUD (bars,
buff strip, floating popups, damage numbers).

TD verdict ACCEPT-WITH-CONDITIONS: extend removal from the widgets
themselves to also cover the `CombatController._log` buffer + all 28
`AddLog(...)` call sites, since `CombatLogWidget` is the sole consumer
per grep of `.Log`, `_log`, `controller.Log`. Leaving the buffer behind
after the widget dies is exactly the ADR-0011 parallel-storage
drift pattern.

## Pre-change authored values being replaced

Nothing designer-tuned is destroyed. All values removed are debug UI
chrome (font sizes, panel widths, background colors) hardcoded in
`DebugStatsWidget.cs` / `CombatLogWidget.cs` / `CombatHud.cs`. No SO
assets touched. No save-schema surface (`_log` is a runtime
`readonly List<string>`, never serialized).

## Changes applied

### 1. `Assets/Scripts/CombatView/CombatHud.cs`

- Delete constants (lines 124–147): `TopLeftMarginPx`,
  `TopLeftPanelWidthPx`, `TopLeftStackGapPx`, `DebugStatsHeightPx`,
  `DebugStatsToggleHeightPx`, `DebugStatsToggleGapPx`,
  `CombatLogHeightPx`, `CombatLogToggleHeightPx`,
  `CombatLogToggleGapPx`.
- Delete serialized fields (lines 258–274): `_combatLog`,
  `_combatLogPanel`, `_combatLogPanelRt`, `_combatLogToggleRt`,
  `_combatLogToggleLabel`, `_combatLogToggleButton`,
  `_debugStatsToggleButton`, `_debugStatsPanel`,
  `_debugStatsToggleLabel`.
- Delete methods (lines 811–977): `BuildDebugStats`, `ToggleDebugStats`,
  `BuildCombatLog`, `ToggleCombatLog`, `RepositionLogStack`.
- Delete Awake call sites (lines 384–388): `BuildDebugStats()` and
  `BuildCombatLog()` invocations + surrounding comment.

### 2. `Assets/Scripts/CombatView/CombatController.cs`

Per TD:
- Delete field `_maxLogLines` (line 46).
- Delete field `_log` (line 71).
- Delete property `Log => _log` + xmldoc (lines 177–180).
- Delete method `AddLog(string)` (lines 742–746).
- Delete `_log.Clear();` (line 226).
- Delete all 28 `AddLog(...)` call sites: lines 131, 227, 228, 229,
  338, 343, 348, 357, 367, 372, 401, 406, 531, 558, 648, 650, 658,
  664, 668, 676, 683, 704, 716, 722, 727, 732, 736. (Plus the
  `SpawnEngineDotPopupAndLog` internal AddLog at 558.)
- Update xmldoc (lines 24–27): drop the `CombatLogWidget` /
  `DebugStatsWidget` references from the "Combat state is surfaced
  through the canvas widgets in CombatHud" paragraph.

### 3. `Assets/Editor/CombatPrefabAuthor.cs`

- Delete method `AuthorDebugStats` (lines 2635–2679) — orphan (no
  callers), safe standalone delete.
- Delete method `AuthorCombatLog` (lines 2681–2730) — orphan (no
  callers), safe standalone delete.
- Delete helper `BuildCombatLogLine` (lines 2732–2770) — sole caller
  was `AuthorCombatLog`.
- Delete constants inside `AuthorCombatHud` (lines 3563–3568):
  `DebugStatsHeightPx`, `DebugStatsToggleHeightPx`,
  `DebugStatsToggleGapPx`, `CombatLogHeightPx`,
  `CombatLogToggleHeightPx`, `CombatLogToggleGapPx`.
- Also drop `TopLeftMarginPx`, `TopLeftPanelWidthPx`,
  `TopLeftStackGapPx` (lines 3560–3562) — only consumed by the two
  deleted blocks.
- Delete `LoadPrefab("DebugStats")` + `LoadPrefab("CombatLog")` calls
  (lines 3580–3581).
- Delete inline authoring block for DebugStatsToggle + panel
  instantiate (lines 3825–3869).
- Delete inline authoring block for CombatLogToggle + panel
  instantiate + `CombatLogWidget` GetComponent (lines 3871–3917).
- Delete `CombatLogWidget combatLog = ...` var + any subsequent
  wire-up under `AuthorCombatHud` (line 3917 area).

### 4. Deletes

- `Assets/Scripts/CombatView/DebugStatsWidget.cs` (+ .meta)
- `Assets/Scripts/CombatView/CombatLogWidget.cs` (+ .meta)
- `Assets/Prefabs/CombatView/DebugStats.prefab` (+ .meta)
- `Assets/Prefabs/CombatView/CombatLog.prefab` (+ .meta)

### 5. `Assets/Prefabs/CombatView/CombatHud.prefab`

Contains authored child GameObjects for both panels + toggles.
Cleanest path: user re-runs `Tools/Wasteland Run/Author Combat HUD
Prefab` after this commit lands. The author's inline blocks (§3) are
gone so the re-author emits a HUD prefab without either widget. Unity
will drop orphan SerializeField refs with a benign warning; no manual
YAML editing required.

## Play-mode verification

Post-commit user should:
- Boot into a Combat encounter.
- Confirm top-left corner shows no STATS / LOG toggles or panels.
- Confirm combat gameplay unaffected (bars, hand, End Turn, floating
  popups, buff strip all still render).
- Confirm no console errors from `CombatController` / `CombatHud`.

## Excluded from commit

Nothing. Full removal in one commit per user's converge-before-switch
preference.

## Technical Director Review

**ACCEPT-WITH-CONDITIONS** — extend removal to include
`CombatController._log`, `_maxLogLines`, `Log` property, `AddLog`
method + all 28 `AddLog(...)` call sites (all applied above).

- **CombatController.Log consumer report:** sole consumer is
  `CombatLogWidget` (verified at `CombatHud.cs:945`, the only external
  `_controller.Log` binding site — inside `BuildCombatLog` which is
  being deleted). Zero test/replay/save/telemetry readers. Buffer +
  writers can go with the widget.
- **ADR-0011:** compliant. Net deletion, no bridges/stubs/parallel
  storage introduced. The only way to violate ADR-0011 here would be to
  leave `_log` + `Log` + `AddLog` behind "in case a future widget wants
  them" — extended removal above avoids that trap.
- **ADR-0004 save-schema:** clean. `_log` is runtime-only, never
  serialized to `RunState`/`CombatState` DTOs. Zero
  `SchemaVersion` bump needed.
- **Orphan authoring surface:** the two `[SerializeField]` fields
  `_maxLogLines` on `CombatController` + all nine on `CombatHud` are
  dead-authoring surface if kept; extended removal above deletes them.
- **CombatPrefabAuthor scope:** two full menu methods to remove
  (`AuthorDebugStats` line 2637, `AuthorCombatLog` line 2683) plus the
  inline authoring in `AuthorCombatHud`. All confirmed as sole callers
  / consumers.
- **CombatHud.prefab YAML:** orphaned SerializeField refs resolve
  cleanly on the next `Author Combat HUD Prefab` re-run.
- **Three-Lens Self-Audit:** no delta beyond above — deletion improves
  codebase health under ADR-0011, no per-frame regression (removes
  Update tick on both widgets + AddLog call in every combat event), no
  1.0-signature risk.

## Follow-ups

- User re-runs `Tools/Wasteland Run/Author Combat HUD Prefab` after
  this commit lands to bake the cleaned-up HUD.
- If a future debug telemetry surface is needed, ADR-0011 says build
  fresh — do NOT resurrect `_log` scaffolding.

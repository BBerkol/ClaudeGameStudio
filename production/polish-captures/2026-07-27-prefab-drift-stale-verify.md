# Prefab Drift Bake — Dredge + Combat (STALE SENTINEL, no bake required)

**Date:** 2026-07-27
**Sentinel entries cleared:** `Dredge`, `Combat`
**Flag timestamps (both):** 2026-07-27T00:24:27+03:00
**Verdict:** Both entries are false positives from user chatter during the
2026-07-26/27 dredge-boss bundle discussion (C1/C2/C3). No actual prefab
drift exists in the working tree; both prefabs are already baked into
`Assets/Editor/CombatPrefabAuthor.cs`.

## Why the sentinel fired

`.claude/hooks/pre-author-bake-required.sh` is a UserPromptSubmit heuristic:
it scans the user's prompt text for edit-disclosure patterns paired with a
known vehicle name. During the dredge-boss bundle work the user's prompts
naturally referenced "Dredge" and "Combat" (both as vehicle name AND as
`BeaconType.Combat` / `Combat.prefab`), tripping the pattern. The hook flags
independently of actual git diff state.

Once flagged, the sentinel persists until manually cleared (per hook design
— the reminder fires on every prompt until `rm production/session-state/prefab-drift-pending.json`
or a targeted edit of the JSON).

## Verification — Dredge

**Working tree:** `git status --short Assets/Prefabs/Enemies/Dredge.prefab`
returns empty. No unstaged edits.

**Commit history since last bake commit (`78696fa`, 2026-06-30):**

| Commit | Title | CombatPrefabAuthor.cs also modified? |
|---|---|---|
| `d85feb3` | drift bake v2 + bar resize + author-flow hardening | Yes (+284 lines) |
| `a0b4d6d` | SlotTargetRing Slice 2b — icon wiring + enemy strip | Yes (+459 lines) |
| `24405da` | SlotTargetRing Slice 3 — EnemyNumberBadge + Delta A | Yes (+175 lines) |

Every touch of `Dredge.prefab` after the last bake commit was accompanied
by an author-code update. The most recent commit (`24405da`) was explicitly
"additive slice — no destructive edit to authored content" per its own
commit body.

**Spot-check of `AuthorDredge()` at `CombatPrefabAuthor.cs:4957`:**
All designer-tuned values documented in the v2 bake header comment are
present in current source:

- `Empty` placeholder: pos (0.9, -0.139, 0), scale (0.21, 0.21, 0.21), color (0.09, 0.075, 0.067, 1) ✓
- `WeaponSlot3SpriteName = "Javelin"` ✓
- `HasWheelsMiddleHitZone = true` at (-3.1048, -0.6912) ✓
- Anchor positions: `mobility_0` (-2.14, -0.10), `engine_0` (3.25, 0.24), `slot_exposable_2` (-0.17, 0.29) ✓
- `SkipPerSlotRings = true, UseEnemyBadges = true` (Slice 3 asymmetry, memory `project_hud_widget_asymmetry`) ✓

## Verification — Combat

**Working tree:** `git status --short Assets/Prefabs/CombatView/Combat.prefab`
returns empty.

**Commit history since last bake commit (`78696fa`):**

Zero commits touch `Combat.prefab` after `78696fa`. The prior capture
`production/polish-captures/2026-07-24-prefab-drift-Combat.md` verified
that the CardHand Canvas z-order drift was already baked into
`AuthorCombatHud()` at that point. Nothing has changed the prefab since.

## Action taken

1. This capture written (documenting the stale-sentinel verification).
2. `production/session-state/prefab-drift-pending.json` deleted (both
   entries were the only pending vehicles; sentinel file removed entirely).

## Technical Director Review

Skipped — no code changes made, only capture-doc authoring + sentinel
clearance. No system-shape decisions to review.

## Follow-up

None required. Both prefabs remain in the "authored + committed clean"
state established by their most recent bake commits.

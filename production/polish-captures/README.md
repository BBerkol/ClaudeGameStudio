# Polish Captures — Capture-Before-Destroy Protocol

This directory holds capture files for destructive edits to authored content.
The `capture-before-destroy.sh` PreToolUse hook gates Edit/Write/MultiEdit on
protected paths and refuses to let Claude proceed without one of these files
in place for today's date.

## Why this exists

Multiple times across the project, view-layer refactors squashed accumulated
designer polish (per-side anchor overrides, tooltip behaviors, color tints,
m_IsActive states, etc.) because Claude went straight to "delete and rebuild"
without first capturing what was authored. Memory rules alone proved
insufficient — they got ignored. The hook is the enforcement.

The hook also enforces **mandatory technical-director review** for any
system refactor on protected paths or new system code (≥50 lines). TD
holds the final-game picture and judges proposed changes against it
*before* code is touched.

## Protected paths (the hook blocks destructive edits here)

| Pattern | Why protected |
|---|---|
| `Assets/Prefabs/**/*.prefab` | Authored prefabs — accumulated overrides |
| `Assets/Scenes/**/*.unity` | Scenes |
| `Assets/Editor/**/*.cs` | Author scripts (e.g. CombatPrefabAuthor) |
| `Assets/Resources/**` | Designer-tuned SOs/prefabs |
| `Assets/Scripts/**/*(Definition\|Layout\|Archetype)*.cs` | System-shape carriers |
| `design/(gdd\|registry\|art\|audio\|ux\|narrative)/**` | Locked design docs |
| `docs/architecture/adr-*.md` | Accepted ADRs |

Always allowed (never blocked):
- `production/polish-captures/**` (this dir)
- `production/session-state/**`
- `.claude/**`

## When the hook triggers

The hook fires on `Edit | Write | MultiEdit`. It blocks when the path is
protected AND the change crosses a destructive/refactor threshold:

- **Edit:** `old_string` is 200+ chars longer than `new_string`, OR `old_string`
  removes a `--- !u!` GameObject YAML block that `new_string` doesn't have
- **MultiEdit:** any MultiEdit on a protected path triggers review
  (aggregate damage is hard to bound — conservative default)
- **Write (overwrite):** existing file shrinks by 500+ chars
- **Write (new file):** 50+ lines in a protected zone — TD review on new
  system code per user directive

## Capture file format

Filename: `YYYY-MM-DD-<system-slug>.md`
Example: `2026-06-04-vehiclebarstack.md`

Required structure (the hook checks the bolded items):

```markdown
# Polish Capture: <System Name>

**Date:** YYYY-MM-DD
**System:** <human-readable name>
**Affected paths:**
- `Assets/Prefabs/CombatView/VehicleBarStack.prefab`   ← path basename MUST appear in file
- `Assets/Scripts/CombatView/VehicleBarStack.cs`
- `Assets/Editor/CombatPrefabAuthor.cs`

## Proposed change
<What is being done and why, in 2-4 sentences>

## Final-game picture this serves
<How this change supports the WHOLE game shape — vehicles roster,
biomes plan, HUD polish needs across the project, etc.>

## Authored values being destroyed

| Where | Value | Current | Replacement plan |
|---|---|---|---|
| VehicleBarStack.prefab | Marker_Engine anchoredPosition | (12, -34) | Spawned at HudAnchor UV (0.5, 0.58) |
| ApplyPerSideWidgetOverrides | EnemyFlamethrower_Bar.x | -156.0 | Per-chassis override list, see below |
| ... | ... | ... | ... |

## Technical Director Review     ← REQUIRED HEADING — hook checks for this

**Verdict:** APPROVE | RESHAPE | VETO
**Spawned at:** YYYY-MM-DD HH:MM
**Agent transcript:** (paste TD agent's response here)

**TD reasoning summary:**
- <Point 1 from TD>
- <Point 2 from TD>
- <Structural guidance if RESHAPE>

## User approval
- Reviewed: YYYY-MM-DD
- Approved by: <user>
- Notes: ...
```

## Hook bypass logic (per-system, not per-day)

Today's date gets a capture file → the hook checks every protected edit
against EVERY today-dated file. Bypass requires **BOTH** of these in the same
file:

1. The path basename appears somewhere in the capture file (substring match)
2. A `## Technical Director Review` heading exists in the file

So one capture covers one system. Touching a second system the same day
requires a second capture. This is intentional — per-system bypass prevents
"I already wrote a capture today" from becoming a blanket pass.

## The protocol Claude must follow

1. Spawn `technical-director` agent with:
   - Current state of system + accumulated polish
   - Proposed change + the why
   - List of files about to be touched
   - Final-game picture this serves
2. Wait for TD verdict: APPROVE / RESHAPE / VETO
3. Write capture file at `production/polish-captures/<date>-<system>.md`
   - Reference every path that will be touched (basename in body)
   - Paste TD response under `## Technical Director Review`
   - Enumerate every authored value about to be destroyed
4. Surface the capture to the user, get explicit approval
5. Retry the destructive edit — hook will now allow it

If TD verdict is RESHAPE, redesign the change with TD's guidance and
re-run the protocol. If VETO, do not proceed.

## Mandatory TD review thresholds (Claude self-enforces; hook backstops)

| Change scope | TD review required? |
|---|---|
| Edit ≥200 chars to protected path | YES (hook enforces) |
| Write ≥500 char overwrite of protected path | YES (hook enforces) |
| New file ≥50 lines in protected path | YES (hook enforces) |
| Any system refactor crossing ≥3 files | YES (self-enforced — hook can't see multi-file) |
| Any system refactor of ≥100 LOC delta | YES (self-enforced) |
| New system from scratch ≥50 LOC | YES (self-enforced) |

## ADR-0011 carve-out

ADR-0011 says "no bridges at done state." The polish-capture protocol is
**not** a bridge. It is preparation. The capture file is throwaway — once
the destructive edit lands and the polish is preserved in the new code,
the capture has done its job. It does not become a permanent compatibility
layer or vestigial code path. ADR-0011 explicitly carves out one-shot
migrators; this is the same shape.

#!/bin/bash
# Claude Code PreToolUse hook: Validates git commit commands
# Receives JSON on stdin with tool_input.command
# Exit 0 = allow, Exit 2 = block (stderr shown to Claude)
#
# Input schema (PreToolUse for Bash):
# { "tool_name": "Bash", "tool_input": { "command": "git commit -m ..." } }

INPUT=$(cat)

# Parse command -- use jq if available, fall back to grep
if command -v jq >/dev/null 2>&1; then
    COMMAND=$(echo "$INPUT" | jq -r '.tool_input.command // empty')
else
    # jq is NOT installed in this environment (verified 2026-08-27), so this
    # fallback is the live path, not a safety net. The original pattern
    # ("[^"]*") stopped at the first ESCAPED quote, truncating any command
    # containing a quoted path — which is every `cd "<project>" && git commit`.
    # Take everything to the end of the JSON object instead, then unescape.
    COMMAND=$(echo "$INPUT" \
        | sed 's/.*"command"[[:space:]]*:[[:space:]]*"//; s/"[[:space:]]*}[[:space:]]*}[[:space:]]*$//' \
        | sed 's/\\"/"/g')
fi

# FAIL LOUD-OPEN. If the command could not be extracted at all, say so and
# allow — do NOT silently skip.
#
# Deliberately different from capture-before-destroy.sh / td-review-required.sh,
# which fail CLOSED. Those guard irreversible loss of authored content, where a
# false block costs a minute and a bypass costs a prefab. This one guards commit
# QUALITY: blocking every commit because a parser hiccupped would halt the
# project, and a hook that halts everything gets deleted rather than repaired.
# The banner is the point — silence is what let this hook sit dead for the
# project's entire history (2026-08-27).
if [ -z "$COMMAND" ]; then
    echo "[validate-commit] PARSER FAILED — could not extract the command from hook input." >&2
    echo "                  Commit ALLOWED unchecked. CI grep gates did NOT run." >&2
    exit 0
fi

# Only process git commit commands.
#
# Anchored '^git commit' until 2026-08-27, which meant this hook had NEVER
# fired: commits in this project are issued as `cd "<project>" && git commit`,
# so the anchor rejected every single one. Match the verb wherever it appears
# after a shell separator instead.
# Strip quoted spans BEFORE matching, so a command that merely MENTIONS the
# verb inside a string is not mistaken for the verb itself. Verified 2026-08-27:
#   git commit -m "fix where git commit failed"   -> match   (is a commit)
#   cd "C:/x y/z" && git commit -m "msg"          -> match   (is a commit)
#   echo "run && git commit later" > notes.txt    -> ignore  (was a FALSE BLOCK)
# Over-blocking is as harmful as never firing: a hook that stops unrelated
# commands gets disabled rather than fixed.
COMMAND_UNQUOTED=$(echo "$COMMAND" | sed 's/"[^"]*"//g; s/'"'"'[^'"'"']*'"'"'//g')
if ! echo "$COMMAND_UNQUOTED" | grep -qE '(^|&&|;|\|)[[:space:]]*git[[:space:]]+commit'; then
    exit 0
fi

# Resolve WHICH repo this commit targets before doing anything else.
#
# The hook runs with the SESSION's cwd, which in a multi-repo setup is usually
# not the repo being committed to. Commits are issued as
# `cd "<project>" && git commit ...`, so the cd target is authoritative.
#
# Two real bugs on first write (both 2026-08-27, both found by running the hook
# the way it is actually invoked rather than the way it was tested):
#   1. Gate lookup resolved from cwd → gate script not found → SILENT skip.
#   2. This staged-files check ran in the wrong repo → found nothing staged →
#      early exit 0 → the gate block below was never reached at all.
# Both recreated the false assurance this mechanism exists to remove.
TARGET_DIR=$(echo "$COMMAND" | grep -oE 'cd[[:space:]]+"[^"]+"' | head -1 \
    | sed 's/^cd[[:space:]]*"//; s/"$//')
if [ -z "$TARGET_DIR" ]; then
    TARGET_DIR=$(echo "$COMMAND" | grep -oE "cd[[:space:]]+'[^']+'" | head -1 \
        | sed "s/^cd[[:space:]]*'//; s/'$//")
fi
[ -z "$TARGET_DIR" ] && TARGET_DIR="."

# Get staged files FROM THE TARGET REPO
STAGED=$(git -C "$TARGET_DIR" diff --cached --name-only 2>/dev/null)
if [ -z "$STAGED" ]; then
    exit 0
fi

WARNINGS=""

# ---------------------------------------------------------------------------
# BLOCKING: project CI grep gates
#
# Added 2026-08-27. Before this, tools/ci/grep-gates.sh existed with 35 gates
# and NOTHING invoked it — it appeared only in settings.local.json as a
# permission allowlist entry and in capture files as prose ("run grep-gates →
# expect clean"). ADR-0011 meanwhile claims "CI-enforceable — each retirement
# slice adds a grep gate that fails the build if the retired marker
# reappears." There was no build. That claim was itself an instance of the
# drift class the gates exist to prevent.
#
# Unrun gates are worse than no gates: they produce documented false
# assurance. This makes them fire.
#
# Deliberately generic — no hardcoded project path. Any repo that ships
# tools/ci/grep-gates.sh gets it enforced; repos that don't are unaffected.
# The script self-locates relative to its own path and exits non-zero when a
# gate fires.
# ---------------------------------------------------------------------------
# Resolve the repo being committed to. NOT from cwd alone: the hook runs with
# the SESSION's cwd, which for a multi-repo setup is usually not the repo the
# commit targets. Commits are issued as `cd "<project>" && git commit ...`, so
# the cd target is the authoritative signal when present.
#
# This was a real bug on first write (2026-08-27): the hook resolved from cwd,
# found no gate script in the framework repo, and skipped SILENTLY — recreating
# the false assurance it was written to remove. Caught by running the hook from
# the actual session cwd instead of the one used while testing it.
REPO_ROOT=$(git -C "$TARGET_DIR" rev-parse --show-toplevel 2>/dev/null)
[ -z "$REPO_ROOT" ] && REPO_ROOT=$(git rev-parse --show-toplevel 2>/dev/null)

GATE_SCRIPT=""
if [ -n "$REPO_ROOT" ] && [ -f "$REPO_ROOT/tools/ci/grep-gates.sh" ]; then
    GATE_SCRIPT="$REPO_ROOT/tools/ci/grep-gates.sh"
fi

# Visible skip, never silent. A gate that quietly does not run is worse than no
# gate — that is the failure this whole mechanism exists to correct, and it is
# how ADR-0011's "CI-enforceable" claim went stale unnoticed for months.
if [ -z "$GATE_SCRIPT" ]; then
    echo "[gates] no tools/ci/grep-gates.sh under '${REPO_ROOT:-<no repo resolved>}' — grep gates NOT run for this commit." >&2
fi

if [ -n "$GATE_SCRIPT" ]; then
    GATE_OUTPUT=$(bash "$GATE_SCRIPT" 2>&1)
    if [ $? -ne 0 ]; then
        echo "BLOCKED: CI grep gates fired — commit refused." >&2
        echo "$GATE_OUTPUT" >&2
        echo "" >&2
        echo "A gate fires when a deliberately-retired symbol reappears in live code." >&2
        echo "Either the reintroduction is a mistake, or the retirement is genuinely" >&2
        echo "being reversed — in which case delete the gate in the same commit and" >&2
        echo "say why. Do not work around it." >&2
        exit 2
    fi
fi

# Check design documents for required sections
DESIGN_FILES=$(echo "$STAGED" | grep -E '^design/gdd/')
if [ -n "$DESIGN_FILES" ]; then
    while IFS= read -r file; do
        if [[ "$file" == *.md ]] && [ -f "$file" ]; then
            for section in "Overview" "Player Fantasy" "Detailed" "Formulas" "Edge Cases" "Dependencies" "Tuning Knobs" "Acceptance Criteria"; do
                if ! grep -qi "$section" "$file"; then
                    WARNINGS="$WARNINGS\nDESIGN: $file missing required section: $section"
                fi
            done
        fi
    done <<< "$DESIGN_FILES"
fi

# Validate JSON data files -- block invalid JSON
DATA_FILES=$(echo "$STAGED" | grep -E '^assets/data/.*\.json$')
if [ -n "$DATA_FILES" ]; then
    # Find a working Python command
    PYTHON_CMD=""
    for cmd in python python3 py; do
        if command -v "$cmd" >/dev/null 2>&1; then
            PYTHON_CMD="$cmd"
            break
        fi
    done

    while IFS= read -r file; do
        if [ -f "$file" ]; then
            if [ -n "$PYTHON_CMD" ]; then
                if ! "$PYTHON_CMD" -m json.tool "$file" > /dev/null 2>&1; then
                    echo "BLOCKED: $file is not valid JSON" >&2
                    exit 2
                fi
            else
                echo "WARNING: Cannot validate JSON (python not found): $file" >&2
            fi
        fi
    done <<< "$DATA_FILES"
fi

# Check for hardcoded gameplay values in gameplay code
# Uses grep -E (POSIX extended) instead of grep -P (Perl) for cross-platform compatibility
#
# Path set widened 2026-08-27. This previously matched ONLY '^src/gameplay/',
# a framework-template path that the Unity project never uses — so this check
# had never fired on a single real file. A hook inspecting paths that do not
# exist provides false assurance, which is worse than no hook.
CODE_FILES=$(echo "$STAGED" | grep -E '^(src/gameplay/|Assets/Scripts/)')
if [ -n "$CODE_FILES" ]; then
    while IFS= read -r file; do
        if [ -f "$file" ]; then
            if grep -nE '(damage|health|speed|rate|chance|cost|duration)[[:space:]]*[:=][[:space:]]*[0-9]+' "$file" 2>/dev/null; then
                WARNINGS="$WARNINGS\nCODE: $file may contain hardcoded gameplay values. Use data files."
            fi
        fi
    done <<< "$CODE_FILES"
fi

# Check for TODO/FIXME without assignee -- uses grep -E instead of grep -P
# Same 2026-08-27 widening as above: '^src/' never matched in the Unity project.
SRC_FILES=$(echo "$STAGED" | grep -E '^(src/|Assets/Scripts/|Assets/Tests/)')
if [ -n "$SRC_FILES" ]; then
    while IFS= read -r file; do
        if [ -f "$file" ]; then
            if grep -nE '(TODO|FIXME|HACK)[^(]' "$file" 2>/dev/null; then
                WARNINGS="$WARNINGS\nSTYLE: $file has TODO/FIXME without owner tag. Use TODO(name) format."
            fi
        fi
    done <<< "$SRC_FILES"
fi

# Print warnings (non-blocking) and allow commit
if [ -n "$WARNINGS" ]; then
    echo -e "=== Commit Validation Warnings ===$WARNINGS\n================================" >&2
fi

exit 0

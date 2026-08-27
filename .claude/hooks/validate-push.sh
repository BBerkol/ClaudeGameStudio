#!/bin/bash
# Claude Code PreToolUse hook: Validates git push commands
# Warns on pushes to protected branches
# Exit 0 = allow, Exit 2 = block
#
# Input schema (PreToolUse for Bash):
# { "tool_name": "Bash", "tool_input": { "command": "git push origin main" } }

INPUT=$(cat)

# Parse command -- use jq if available, fall back to grep
if command -v jq >/dev/null 2>&1; then
    COMMAND=$(echo "$INPUT" | jq -r '.tool_input.command // empty')
else
    # jq is NOT installed here (verified 2026-08-27) — this fallback is the
    # live path. The original pattern ("[^"]*") stopped at the first ESCAPED
    # quote, truncating any command containing a quoted path.
    COMMAND=$(echo "$INPUT" \
        | sed 's/.*"command"[[:space:]]*:[[:space:]]*"//; s/"[[:space:]]*}[[:space:]]*}[[:space:]]*$//' \
        | sed 's/\\"/"/g')
fi

# FAIL LOUD-OPEN — quality guard, not a destruction guard. See the same block
# in validate-commit.sh for why this differs from capture-before-destroy.sh.
if [ -z "$COMMAND" ]; then
    echo "[validate-push] PARSER FAILED — could not extract the command from hook input." >&2
    echo "                Push ALLOWED unchecked. Branch + test-evidence check did NOT run." >&2
    exit 0
fi

# Only process git push commands.
#
# Anchored '^git push' until 2026-08-27 — same defect as validate-commit.sh.
# Pushes are issued as `cd "<project>" && git push`, so the anchor rejected
# every one and this hook had never fired.
# Strip quoted spans before matching — same false-positive fix as
# validate-commit.sh (2026-08-27). Without it, any command mentioning
# "git push" inside a string would be treated as a push.
COMMAND_UNQUOTED=$(echo "$COMMAND" | sed 's/"[^"]*"//g; s/'"'"'[^'"'"']*'"'"'//g')
if ! echo "$COMMAND_UNQUOTED" | grep -qE '(^|&&|;|\|)[[:space:]]*git[[:space:]]+push'; then
    exit 0
fi

# Resolve the repo being pushed FROM. The hook runs with the SESSION's cwd,
# which in this multi-repo setup is usually not the repo being pushed — so
# CURRENT_BRANCH was reporting the framework repo's branch regardless of which
# project the push targeted.
TARGET_DIR=$(echo "$COMMAND" | grep -oE 'cd[[:space:]]+"[^"]+"' | head -1 \
    | sed 's/^cd[[:space:]]*"//; s/"$//')
if [ -z "$TARGET_DIR" ]; then
    TARGET_DIR=$(echo "$COMMAND" | grep -oE "cd[[:space:]]+'[^']+'" | head -1 \
        | sed "s/^cd[[:space:]]*'//; s/'$//")
fi
[ -z "$TARGET_DIR" ] && TARGET_DIR="."

CURRENT_BRANCH=$(git -C "$TARGET_DIR" rev-parse --abbrev-ref HEAD 2>/dev/null)
[ -z "$CURRENT_BRANCH" ] && CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD 2>/dev/null)
MATCHED_BRANCH=""

# Check if pushing to a protected branch
for branch in develop main master; do
    if [ "$CURRENT_BRANCH" = "$branch" ]; then
        MATCHED_BRANCH="$branch"
        break
    fi
    # Also check if pushing to a protected branch explicitly (quote branch name for safety)
    if echo "$COMMAND" | grep -qE "[[:space:]]${branch}([[:space:]]|$)"; then
        MATCHED_BRANCH="$branch"
        break
    fi
done

if [ -n "$MATCHED_BRANCH" ]; then
    echo "Push to protected branch '$MATCHED_BRANCH' detected." >&2

    # ------------------------------------------------------------------
    # Report ACTUAL test evidence instead of reminding someone to have it.
    #
    # Rewritten 2026-08-27. This previously printed "Reminder: Ensure build
    # passes, unit tests pass" and carried a commented-out `exit 2` block.
    # A prose reminder verifies nothing, and commented-out code is vestigial
    # under ADR-0011. Worse, batchmode's exit code is unreliable — a run can
    # report success while writing no results file at all (observed
    # 2026-08-27: exit 0 with six `error CS` lines and no XML). So the only
    # trustworthy signals are the results XML itself and its failed count.
    #
    # Non-blocking by design: this repo is trunk-based, pushes to main are
    # normal, and a hook that blocks the standard workflow gets disabled
    # rather than obeyed. The job here is to make the evidence impossible to
    # skip past, not to gate.
    # ------------------------------------------------------------------
    # Resolve from TARGET_DIR, not cwd — the results XML lives in the repo
    # being pushed, which is usually not the session's cwd.
    REPO_ROOT=$(git -C "$TARGET_DIR" rev-parse --show-toplevel 2>/dev/null)
    [ -z "$REPO_ROOT" ] && REPO_ROOT=$(git rev-parse --show-toplevel 2>/dev/null)

    # -print0/-0 and a while-read, NOT `| xargs`: both repo roots contain a
    # space ("Madmax Roguelike" / "Madmax Rougelike"), and bare xargs would
    # split every path into non-existent fragments. Same class as the
    # capture-before-destroy word-split defect found earlier today.
    LATEST_XML=""
    while IFS= read -r -d '' f; do
        if grep -q '<test-run' "$f" 2>/dev/null; then
            LATEST_XML="$f"
            break
        fi
    done < <(find "$REPO_ROOT" -maxdepth 2 -name '*.xml' -newermt '-6 hours' -print0 2>/dev/null)

    if [ -z "$LATEST_XML" ]; then
        echo "  TEST EVIDENCE: none found (no results XML in the last 6 hours)." >&2
        echo "  If you ran the suite elsewhere, fine — but nothing here proves it." >&2
    else
        SUMMARY=$(head -c 400 "$LATEST_XML" | grep -o 'total="[0-9]*"[^>]*failed="[0-9]*"' | head -1)
        FAILED=$(echo "$SUMMARY" | grep -o 'failed="[0-9]*"' | grep -o '[0-9]*')
        echo "  TEST EVIDENCE: $(basename "$LATEST_XML") — $SUMMARY" >&2
        if [ "${FAILED:-1}" != "0" ]; then
            echo "  ^^ FAILURES PRESENT in the most recent run." >&2
        fi
    fi
fi

exit 0

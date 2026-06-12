#!/bin/bash
# Claude Code PreToolUse hook: Gates destructive edits to authored content.
#
# Forces capture-before-destroy + mandatory technical-director review.
# See production/polish-captures/README.md for the protocol.
#
# Exit behavior:
#   exit 0 = allow tool call (path not protected, change not destructive, OR
#            current-date capture file exists with TD review section AND
#            references the path being touched)
#   exit 2 = BLOCK tool call (destructive edit to protected path without capture)
#
# Triggers: Edit | Write | MultiEdit  (PreToolUse)
#
# Protected zones (substring match on file_path):
#   - Assets/Prefabs/**/*.prefab           (game prefabs)
#   - Assets/Scenes/**/*.unity             (scenes)
#   - Assets/Editor/**/*.cs                (author scripts)
#   - Assets/Resources/**                  (designer-tuned SO/prefab)
#   - Assets/Scripts/**/*(Definition|Layout|Archetype)*.cs
#                                          (system-shape carriers)
#   - design/(gdd|registry|art|audio|ux|narrative)/**
#   - docs/architecture/adr-*.md           (accepted ADRs)
#
# Always allowed (capture/session/hook infrastructure):
#   - production/polish-captures/**
#   - production/session-state/**
#   - .claude/**
#
# Destructive thresholds:
#   - Edit:      old_string longer than new_string by 200+ chars
#   - Write:     existing file shrinks by 500+ chars after overwrite
#   - Write:     new file 50+ lines in protected path (counts as new system code)
#   - MultiEdit: any single edit hits Edit threshold OR aggregate loss 500+

INPUT=$(cat)

# Parse JSON. Prefer jq; fall back to Python (always available on Windows
# Git Bash via py.exe or python). Without one of these we can't measure
# old/new lengths and the hook would silently no-op — so we hard-require it.
TOOL_NAME=""
FILE_PATH=""
OLD_LEN=0
NEW_LEN=0
CONTENT_LEN=0

# Parser preference: jq → node → fail-open with warning. jq is the cleanest;
# node is universally present in this environment (Git Bash for Windows has
# no real Python — the python/python3 commands resolve to Microsoft Store
# stub shims that error out). awk could parse JSON but escape handling is
# treacherous, so we don't.

parse_with_jq() {
    TOOL_NAME=$(echo "$INPUT" | jq -r '.tool_name // empty')
    FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')
    OLD_LEN=$(echo "$INPUT" | jq -r '.tool_input.old_string // "" | length')
    NEW_LEN=$(echo "$INPUT" | jq -r '.tool_input.new_string // "" | length')
    CONTENT_LEN=$(echo "$INPUT" | jq -r '.tool_input.content // "" | length')
    CONTENT_LINES=$(echo "$INPUT" | jq -r '.tool_input.content // "" | split("\n") | length')
    MULTI_LOSS=$(echo "$INPUT" | jq -r '[.tool_input.edits[]? | (.old_string // "" | length) - (.new_string // "" | length)] | add // 0')
    MULTI_MAX_EDIT_LOSS=$(echo "$INPUT" | jq -r '[.tool_input.edits[]? | (.old_string // "" | length) - (.new_string // "" | length)] | max // 0')
}

parse_with_node() {
    PARSED=$(node -e '
        let raw = "";
        process.stdin.on("data", c => raw += c);
        process.stdin.on("end", () => {
            let d; try { d = JSON.parse(raw); } catch (e) { process.exit(0); }
            const tn = d.tool_name || "";
            const ti = d.tool_input || {};
            const fp = ti.file_path || "";
            const os = ti.old_string || "";
            const ns = ti.new_string || "";
            const ct = ti.content || "";
            const edits = ti.edits || [];
            const losses = edits.map(e => (e.old_string || "").length - (e.new_string || "").length);
            const ml = losses.reduce((a,b) => a+b, 0);
            const mx = losses.length ? Math.max(...losses) : 0;
            const lines = ct ? (ct.split("\n").length) : 0;
            console.log(tn);
            console.log(fp);
            console.log(os.length);
            console.log(ns.length);
            console.log(ct.length);
            console.log(lines);
            console.log(ml);
            console.log(mx);
        });
    ' <<< "$INPUT")
    TOOL_NAME=$(echo "$PARSED" | sed -n '1p')
    FILE_PATH=$(echo "$PARSED" | sed -n '2p')
    OLD_LEN=$(echo "$PARSED" | sed -n '3p')
    NEW_LEN=$(echo "$PARSED" | sed -n '4p')
    CONTENT_LEN=$(echo "$PARSED" | sed -n '5p')
    CONTENT_LINES=$(echo "$PARSED" | sed -n '6p')
    MULTI_LOSS=$(echo "$PARSED" | sed -n '7p')
    MULTI_MAX_EDIT_LOSS=$(echo "$PARSED" | sed -n '8p')
}

if command -v jq >/dev/null 2>&1; then
    parse_with_jq
elif command -v node >/dev/null 2>&1; then
    parse_with_node
else
    echo "⚠ capture-before-destroy: no jq or node available; hook disabled" >&2
    exit 0
fi

# Defaults if parsing returned nothing
OLD_LEN=${OLD_LEN:-0}
NEW_LEN=${NEW_LEN:-0}
CONTENT_LEN=${CONTENT_LEN:-0}
CONTENT_LINES=${CONTENT_LINES:-0}
MULTI_LOSS=${MULTI_LOSS:-0}
MULTI_MAX_EDIT_LOSS=${MULTI_MAX_EDIT_LOSS:-0}

# Only act on Edit / Write / MultiEdit
case "$TOOL_NAME" in
    Edit|Write|MultiEdit) ;;
    *) exit 0 ;;
esac

# No file path → not our problem
[ -z "$FILE_PATH" ] && exit 0

# Normalize path separators (Windows backslash → forward slash)
NORM_PATH=$(echo "$FILE_PATH" | sed 's|\\|/|g')

# ----- ALWAYS ALLOWED ZONES -----
case "$NORM_PATH" in
    *production/polish-captures/*) exit 0 ;;
    *production/session-state/*)   exit 0 ;;
    */.claude/*)                   exit 0 ;;
esac

# ----- PROTECTED ZONE DETECTION -----
PROTECTED=false
PROTECT_REASON=""

if echo "$NORM_PATH" | grep -qE 'Assets/Prefabs/.*\.prefab$'; then
    PROTECTED=true; PROTECT_REASON="authored prefab"
elif echo "$NORM_PATH" | grep -qE 'Assets/Scenes/.*\.unity$'; then
    PROTECTED=true; PROTECT_REASON="scene"
elif echo "$NORM_PATH" | grep -qE 'Assets/Editor/.*\.cs$'; then
    PROTECTED=true; PROTECT_REASON="editor/author script"
elif echo "$NORM_PATH" | grep -qE 'Assets/Resources/'; then
    PROTECTED=true; PROTECT_REASON="designer-tuned resource"
elif echo "$NORM_PATH" | grep -qE 'Assets/Scripts/.*(Definition|Layout|Archetype).*\.cs$'; then
    PROTECTED=true; PROTECT_REASON="system-shape carrier"
elif echo "$NORM_PATH" | grep -qE 'design/(gdd|registry|art|audio|ux|narrative)/'; then
    PROTECTED=true; PROTECT_REASON="design document"
elif echo "$NORM_PATH" | grep -qE 'docs/architecture/adr-.*\.md$'; then
    PROTECTED=true; PROTECT_REASON="accepted ADR"
fi

[ "$PROTECTED" = "false" ] && exit 0

# ----- DESTRUCTIVE / SYSTEM-REFACTOR HEURISTIC -----
NEEDS_REVIEW=false
REVIEW_REASON=""

case "$TOOL_NAME" in
    Edit)
        DELTA=$((OLD_LEN - NEW_LEN))
        if [ "$DELTA" -ge 200 ]; then
            NEEDS_REVIEW=true
            REVIEW_REASON="Edit removes $DELTA chars from $PROTECT_REASON"
        fi
        ;;
    MultiEdit)
        # Any MultiEdit on protected path with non-trivial loss triggers
        # review. The 200-char threshold matches single-Edit behavior.
        if [ "$MULTI_MAX_EDIT_LOSS" -ge 200 ] || [ "$MULTI_LOSS" -ge 500 ]; then
            NEEDS_REVIEW=true
            REVIEW_REASON="MultiEdit on $PROTECT_REASON — max single-edit loss $MULTI_MAX_EDIT_LOSS chars, total $MULTI_LOSS"
        fi
        ;;
    Write)
        if [ -f "$FILE_PATH" ]; then
            EXISTING_LEN=$(wc -c < "$FILE_PATH" 2>/dev/null || echo 0)
            DELTA=$((EXISTING_LEN - CONTENT_LEN))
            if [ "$DELTA" -ge 500 ]; then
                NEEDS_REVIEW=true
                REVIEW_REASON="Write overwrites $PROTECT_REASON, shrinks file by $DELTA chars"
            fi
        else
            # New file in protected zone — TD review required for any
            # new system code at 50+ lines (user directive 2026-06-04).
            if [ "$CONTENT_LINES" -ge 50 ]; then
                NEEDS_REVIEW=true
                REVIEW_REASON="New $PROTECT_REASON ($CONTENT_LINES lines) — TD review required for new system code"
            fi
        fi
        ;;
esac

[ "$NEEDS_REVIEW" = "false" ] && exit 0

# ----- CAPTURE FILE CHECK -----
TODAY=$(date +%Y-%m-%d)
CAPTURE_DIR="production/polish-captures"
PATH_BASENAME=$(basename "$NORM_PATH")

if [ ! -d "$CAPTURE_DIR" ]; then
    BLOCK_MSG="No capture directory ($CAPTURE_DIR). Run protocol first."
    BLOCK=true
else
    # Find any capture file dated today
    TODAY_FILES=$(find "$CAPTURE_DIR" -maxdepth 1 -type f -name "${TODAY}-*.md" 2>/dev/null)
    if [ -z "$TODAY_FILES" ]; then
        BLOCK_MSG="No capture file for today ($TODAY) in $CAPTURE_DIR/"
        BLOCK=true
    else
        # At least one today file exists. Check each for path reference + TD review section.
        MATCH_FOUND=false
        FAILED_FILES=""
        for cap in $TODAY_FILES; do
            HAS_PATH=false
            HAS_TD=false
            if grep -qF "$PATH_BASENAME" "$cap" 2>/dev/null; then
                HAS_PATH=true
            fi
            if grep -qE '^## *Technical Director Review' "$cap" 2>/dev/null; then
                HAS_TD=true
            fi
            if [ "$HAS_PATH" = "true" ] && [ "$HAS_TD" = "true" ]; then
                MATCH_FOUND=true
                break
            else
                MISSING=""
                [ "$HAS_PATH" = "false" ] && MISSING="$MISSING no-path-ref"
                [ "$HAS_TD" = "false" ]   && MISSING="$MISSING no-td-review"
                FAILED_FILES="$FAILED_FILES\n     $(basename "$cap") (missing:$MISSING)"
            fi
        done
        if [ "$MATCH_FOUND" = "true" ]; then
            BLOCK=false
        else
            BLOCK_MSG="No capture file dated $TODAY both (a) references '$PATH_BASENAME' AND (b) contains '## Technical Director Review' section.\n   Files checked:$FAILED_FILES"
            BLOCK=true
        fi
    fi
fi

if [ "$BLOCK" = "true" ]; then
    cat >&2 <<EOF
🛑 BLOCKED: Destructive edit to protected file without capture+TD review

   File:    $NORM_PATH
   Reason:  $REVIEW_REASON
   Block:   $(echo -e "$BLOCK_MSG")

   PROTOCOL (production/polish-captures/README.md):
   1. Spawn technical-director agent with:
        - Current state of system + accumulated polish
        - Proposed change + the why
        - List of files about to be touched
        - Final-game picture this serves
   2. Get TD verdict: APPROVE / RESHAPE / VETO
   3. Write capture file: production/polish-captures/${TODAY}-<system-slug>.md
        - Reference path '$PATH_BASENAME' somewhere in the file
        - Include section heading: '## Technical Director Review'
        - Paste TD's verdict + reasoning under that heading
        - Enumerate every authored value about to be destroyed
   4. Get user approval on the capture
   5. Retry the edit

   ADR-0011 "no bridges at done state" does NOT exempt the capture phase.
   Capture is preparation, not a bridge.
EOF
    exit 2
fi

exit 0

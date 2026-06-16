#!/bin/bash
# Claude Code PreToolUse hook: gates new-feature work + contract changes
# behind a mandatory technical-director consultation.
#
# Distinct from capture-before-destroy.sh (which gates DESTRUCTIVE edits to
# authored content). This hook gates FORWARD-LOOKING decisions where TD
# input shapes the design, not just records the cost of destruction:
#   - New systems / new public types
#   - New SO authoring shapes ([CreateAssetMenu])
#   - New assemblies (.asmdef)
#   - Public-API contract / xmldoc changes on Run/ and Combat/ core types
#   - Fallback / error-path deletions in non-test code
#
# Exit behavior:
#   exit 0 = allow (not a trigger, OR today's TD verdict exists with markers)
#   exit 2 = BLOCK (trigger fired without a same-day TD verdict)
#
# Verdict locations checked (either satisfies the gate):
#   production/td-verdicts/<YYYY-MM-DD>-*.md
#   production/polish-captures/<YYYY-MM-DD>-*.md   (reuse destructive-op captures)
#
# A verdict file passes the gate when it both:
#   (a) references the touched file's basename
#   (b) contains a heading matching: ## TD Verdict | ## Technical Director Review

INPUT=$(cat)

# ---------- JSON parsing (jq → node fallback, matches capture-before-destroy.sh) ----------

TOOL_NAME=""
FILE_PATH=""
OLD_STR=""
NEW_STR=""
CONTENT=""

parse_with_jq() {
    TOOL_NAME=$(echo "$INPUT" | jq -r '.tool_name // empty')
    FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')
    OLD_STR=$(echo "$INPUT" | jq -r '.tool_input.old_string // empty')
    NEW_STR=$(echo "$INPUT" | jq -r '.tool_input.new_string // empty')
    CONTENT=$(echo "$INPUT" | jq -r '.tool_input.content // empty')
}

parse_with_node() {
    PARSED=$(node -e '
        let raw = "";
        process.stdin.on("data", c => raw += c);
        process.stdin.on("end", () => {
            let d; try { d = JSON.parse(raw); } catch (e) { process.exit(0); }
            const ti = d.tool_input || {};
            const out = {
                tn: d.tool_name || "",
                fp: ti.file_path || "",
                os: ti.old_string || "",
                ns: ti.new_string || "",
                ct: ti.content || ""
            };
            // Newline-delimited base64 to survive shell escaping
            const enc = s => Buffer.from(s, "utf8").toString("base64");
            console.log(out.tn);
            console.log(out.fp);
            console.log(enc(out.os));
            console.log(enc(out.ns));
            console.log(enc(out.ct));
        });
    ' <<< "$INPUT")
    TOOL_NAME=$(echo "$PARSED" | sed -n '1p')
    FILE_PATH=$(echo "$PARSED" | sed -n '2p')
    OLD_STR=$(echo "$PARSED" | sed -n '3p' | base64 -d 2>/dev/null)
    NEW_STR=$(echo "$PARSED" | sed -n '4p' | base64 -d 2>/dev/null)
    CONTENT=$(echo "$PARSED" | sed -n '5p' | base64 -d 2>/dev/null)
}

if command -v jq >/dev/null 2>&1; then
    parse_with_jq
elif command -v node >/dev/null 2>&1; then
    parse_with_node
else
    echo "⚠ td-review-required: no jq or node available; hook disabled" >&2
    exit 0
fi

# Only act on Edit / Write / MultiEdit
case "$TOOL_NAME" in
    Edit|Write|MultiEdit) ;;
    *) exit 0 ;;
esac

[ -z "$FILE_PATH" ] && exit 0

# Normalize path separators (Windows backslash → forward slash)
NORM_PATH=$(echo "$FILE_PATH" | sed 's|\\|/|g')

# ---------- ALWAYS-ALLOWED ZONES ----------
case "$NORM_PATH" in
    *production/*)           exit 0 ;;
    */.claude/*)             exit 0 ;;
    *.meta)                  exit 0 ;;
esac

# ---------- TRIGGER DETECTION ----------
TRIGGER=false
TRIGGER_REASON=""

# Patterns we look for to identify a "new public type" or contract surface
NEW_TYPE_REGEX='^[[:space:]]*public[[:space:]]+(sealed[[:space:]]+)?(class|interface|abstract[[:space:]]+class|sealed[[:space:]]+record|record|enum)[[:space:]]+[A-Z]'
CREATE_ASSET_MENU_REGEX='\[CreateAssetMenu'
FALLBACK_BLOCK_REGEX='(if[[:space:]]*\(|return|throw|catch[[:space:]]*\()'

is_run_or_combat_core() {
    # Core paths whose xmldoc changes are contract-shaped
    case "$1" in
        *Assets/Scripts/Run/*.cs)     return 0 ;;
        *Assets/Scripts/Combat/*.cs)  return 0 ;;
    esac
    return 1
}

is_test_path() {
    case "$1" in
        *Assets/Tests/*) return 0 ;;
        *_Test.cs)       return 0 ;;
    esac
    return 1
}

count_deleted_fallback_lines() {
    # Counts deleted lines in OLD_STR that look like fallback / error-path
    # constructs. Compares OLD_STR vs NEW_STR by gathering OLD-only lines.
    local old="$1"
    local new="$2"
    # Quick path: if NEW_STR contains all of OLD_STR's relevant lines, zero deletion
    local count=0
    while IFS= read -r line; do
        if echo "$line" | grep -qE "$FALLBACK_BLOCK_REGEX"; then
            # Was this exact line preserved in NEW_STR?
            if ! echo "$new" | grep -qFx "$line"; then
                count=$((count + 1))
            fi
        fi
    done <<< "$old"
    echo "$count"
}

# ----- New file in Assets/Scripts/ via Write -----
if [ "$TOOL_NAME" = "Write" ] && [ ! -f "$FILE_PATH" ]; then
    case "$NORM_PATH" in
        *Assets/Scripts/*.cs)
            if echo "$CONTENT" | grep -qE "$NEW_TYPE_REGEX"; then
                TRIGGER=true
                TRIGGER_REASON="New .cs file declaring a public type — new system surface"
            fi
            if echo "$CONTENT" | grep -qE "$CREATE_ASSET_MENU_REGEX"; then
                TRIGGER=true
                TRIGGER_REASON="New ScriptableObject authoring shape ([CreateAssetMenu])"
            fi
            ;;
        *.asmdef)
            TRIGGER=true
            TRIGGER_REASON="New assembly definition (.asmdef) — structural boundary change"
            ;;
    esac
fi

# ----- Edit that adds a new public type to an existing file -----
if [ "$TRIGGER" = "false" ] && [ "$TOOL_NAME" = "Edit" ]; then
    case "$NORM_PATH" in
        *Assets/Scripts/*.cs)
            # New string declares a public type that the old string did not
            if echo "$NEW_STR" | grep -qE "$NEW_TYPE_REGEX"; then
                if ! echo "$OLD_STR" | grep -qE "$NEW_TYPE_REGEX"; then
                    TRIGGER=true
                    TRIGGER_REASON="Edit introduces a new public type declaration"
                fi
            fi
            # New CreateAssetMenu attribute appears
            if [ "$TRIGGER" = "false" ] && echo "$NEW_STR" | grep -qE "$CREATE_ASSET_MENU_REGEX"; then
                if ! echo "$OLD_STR" | grep -qE "$CREATE_ASSET_MENU_REGEX"; then
                    TRIGGER=true
                    TRIGGER_REASON="Edit adds a [CreateAssetMenu] attribute (new SO shape)"
                fi
            fi
            ;;
    esac
fi

# ----- Edit to xmldoc on Run/ or Combat/ core file -----
if [ "$TRIGGER" = "false" ] && [ "$TOOL_NAME" = "Edit" ]; then
    if is_run_or_combat_core "$NORM_PATH" && ! is_test_path "$NORM_PATH"; then
        # xmldoc lines start with '///' — heuristic: OLD or NEW contains such lines
        # AND OLD differs from NEW in xmldoc content (not just code)
        OLD_XMLDOC=$(echo "$OLD_STR" | grep -E '^[[:space:]]*///' | tr -d '[:space:]')
        NEW_XMLDOC=$(echo "$NEW_STR" | grep -E '^[[:space:]]*///' | tr -d '[:space:]')
        if [ -n "$OLD_XMLDOC$NEW_XMLDOC" ] && [ "$OLD_XMLDOC" != "$NEW_XMLDOC" ]; then
            TRIGGER=true
            TRIGGER_REASON="xmldoc change on Run/ or Combat/ core type — contract drift risk"
        fi
    fi
fi

# ----- Edit that deletes fallback / error-path lines (non-test code) -----
if [ "$TRIGGER" = "false" ] && [ "$TOOL_NAME" = "Edit" ]; then
    if ! is_test_path "$NORM_PATH"; then
        case "$NORM_PATH" in
            *Assets/Scripts/*.cs)
                DELETED=$(count_deleted_fallback_lines "$OLD_STR" "$NEW_STR")
                if [ "${DELETED:-0}" -ge 5 ]; then
                    TRIGGER=true
                    TRIGGER_REASON="Edit deletes $DELETED fallback / error-path lines in non-test code"
                fi
                ;;
        esac
    fi
fi

[ "$TRIGGER" = "false" ] && exit 0

# ---------- VERDICT FILE CHECK ----------
TODAY=$(date +%Y-%m-%d)
PATH_BASENAME=$(basename "$NORM_PATH")

check_verdict_dir() {
    local dir="$1"
    [ -d "$dir" ] || return 1
    local files
    files=$(find "$dir" -maxdepth 1 -type f -name "${TODAY}-*.md" 2>/dev/null)
    [ -z "$files" ] && return 1
    local file
    for file in $files; do
        local has_path=false
        local has_td=false
        if grep -qF "$PATH_BASENAME" "$file" 2>/dev/null; then
            has_path=true
        fi
        if grep -qE '^## *(TD Verdict|Technical Director Review)' "$file" 2>/dev/null; then
            has_td=true
        fi
        if [ "$has_path" = "true" ] && [ "$has_td" = "true" ]; then
            return 0
        fi
    done
    return 1
}

if check_verdict_dir "production/td-verdicts" \
   || check_verdict_dir "production/polish-captures"; then
    exit 0
fi

# ---------- BLOCK ----------
cat >&2 <<EOF
🛑 BLOCKED: Forward-looking change requires technical-director consultation

   File:    $NORM_PATH
   Reason:  $TRIGGER_REASON

   No verdict file dated $TODAY in either:
     production/td-verdicts/${TODAY}-*.md   (preferred for new-feature work)
     production/polish-captures/${TODAY}-*.md  (combined w/ destructive op)

   A passing verdict file must:
     (a) reference '$PATH_BASENAME' somewhere in the body
     (b) contain a section heading: '## TD Verdict' or '## Technical Director Review'

   PROTOCOL:
   1. Spawn technical-director agent with:
        - What you're about to build / change + the why
        - Files that will be touched (this one + others)
        - ADRs at risk of drift
        - Final-game picture the change serves
   2. Get verdict: ACCEPT / AMEND / REJECT
   3. Write the verdict file at production/td-verdicts/${TODAY}-<topic-slug>.md
   4. Retry the edit

   This hook fires on: new public types, new SOs, new asmdefs, xmldoc changes
   on Run/+Combat/ core files, and deletion of 5+ fallback/error-path lines
   in non-test code. See the hook source for the full trigger list.
EOF
exit 2

#!/bin/bash
# Claude Code UserPromptSubmit hook: enforce designer-edit bake-back before
# Claude can suggest re-running any Author* menu.
#
# Problem this solves
# -------------------
# CombatPrefabAuthor.cs regenerates vehicle prefabs from source. When a designer
# tunes a prefab in Unity Prefab Mode (positions, scales, sprites, m_IsActive),
# those tweaks live ONLY in the prefab YAML. Re-running `Author X Prefab`
# rebuilds from author source and silently wipes those edits.
#
# Memory rules feedback_bake_designer_edits and feedback_pre_author_capture_protocol
# already require Claude to bake drift into CombatPrefabAuthor.cs BEFORE suggesting
# re-author — but memory rules drift; hooks don't.
#
# How this hook works
# -------------------
# Fires on every UserPromptSubmit. Reads the user's message:
#   1. If it contains an edit-disclosure pattern + a known vehicle name,
#      adds that vehicle to a sentinel file with timestamp.
#   2. If the sentinel file has ANY pending vehicles (from this turn or prior),
#      emits a <system-reminder> block listing them with the mandatory
#      bake-first protocol.
#
# The reminder fires on EVERY user prompt until the sentinel is cleared.
# Clearing the sentinel is manual:
#   - Delete the file:  rm production/session-state/prefab-drift-pending.json
#   - Or remove a specific vehicle by editing the JSON.
#
# Exit behavior
# -------------
#   exit 0 = always (this hook never blocks; it informs)
#   stdout = if non-empty, Claude Code injects it as additional context for
#            this turn (per UserPromptSubmit hook contract)
#
# Input schema (UserPromptSubmit, via stdin):
#   { "session_id": "...", "hook_event_name": "UserPromptSubmit",
#     "prompt": "<user message>", "cwd": "...", ... }

set -u

INPUT=$(cat)
SENTINEL="production/session-state/prefab-drift-pending.json"

# Known vehicle / prefab names the bake protocol applies to. Add new ones here
# as they're introduced. Case-insensitive match.
VEHICLES=(
    "DuneSkimmer"
    "Dredge"
    "IronShepherd"
    "Brute"
    "Stinger"
    "PlayerVehicle"
    "EnemyVehicle"
    "MainBar"
    "BuffStrip"
    "HudAnchors"
    "Combat"
    "RestRoot"
    "SubsystemBar"
    "SubsystemMarker"
)

# ---------- parse user prompt out of JSON ----------
PROMPT=""
if command -v jq >/dev/null 2>&1; then
    PROMPT=$(echo "$INPUT" | jq -r '.prompt // empty' 2>/dev/null)
elif command -v node >/dev/null 2>&1; then
    PROMPT=$(node -e '
        let raw = "";
        process.stdin.on("data", c => raw += c);
        process.stdin.on("end", () => {
            try { const d = JSON.parse(raw); process.stdout.write(d.prompt || ""); }
            catch (e) { process.exit(0); }
        });
    ' <<< "$INPUT" 2>/dev/null)
else
    # No parser → hook can't read prompt. Still emit reminder from existing
    # sentinel so Claude is informed about pending drift on every turn.
    PROMPT=""
fi

# Lowercase the prompt for matching
PROMPT_LC=$(echo "$PROMPT" | tr '[:upper:]' '[:lower:]')

# ---------- detect edit-disclosure patterns ----------
# Matches phrases like "i edited X", "I'm done with X", "I tuned X", "I redid X",
# "I authored X", "I changed X in prefab mode", etc. Liberal on purpose — false
# positives just amplify attention, false negatives lose designer work.
EDIT_VERB_RE='\b(i|i'\''m|im|i am|i have|i just|i did)\b.{0,40}\b(edit|edited|editing|tune|tuned|tuning|author|authored|authoring|tweak|tweaked|change|changed|modify|modified|adjust|adjusted|move|moved|place|placed|position|positioned|redo|redid|redoing|set up|setting up|fix|fixed|fixing|work|working|done with|done editing)\b'
EDIT_CONTEXT_RE='\b(in prefab mode|prefab mode|in (the )?editor|in unity|in the unity editor|designer (tuning|tweak|edit)|in the prefab|hand-placed|hand placed|by hand)\b'

FOUND_EDIT_VERB=0
FOUND_EDIT_CONTEXT=0

if [ -n "$PROMPT_LC" ]; then
    if echo "$PROMPT_LC" | grep -qE "$EDIT_VERB_RE" 2>/dev/null; then
        FOUND_EDIT_VERB=1
    fi
    if echo "$PROMPT_LC" | grep -qE "$EDIT_CONTEXT_RE" 2>/dev/null; then
        FOUND_EDIT_CONTEXT=1
    fi
fi

# Match vehicle names in the prompt (case-insensitive)
MATCHED_VEHICLES=()
for v in "${VEHICLES[@]}"; do
    v_lc=$(echo "$v" | tr '[:upper:]' '[:lower:]')
    if echo "$PROMPT_LC" | grep -qE "\b${v_lc}\b" 2>/dev/null; then
        MATCHED_VEHICLES+=("$v")
    fi
done

# Decide whether THIS prompt flags new drift:
#   - explicit edit verb + at least one vehicle name → flag those vehicles
#   - OR explicit "in prefab mode" / "in editor" context + a vehicle → flag
#   - OR ANY vehicle name AND a "i" subject AND an editing context word, even
#     if patterns above missed (defensive fallback)
NEW_FLAGS=()
if [ ${#MATCHED_VEHICLES[@]} -gt 0 ]; then
    if [ "$FOUND_EDIT_VERB" = "1" ] || [ "$FOUND_EDIT_CONTEXT" = "1" ]; then
        NEW_FLAGS=("${MATCHED_VEHICLES[@]}")
    fi
fi

# ---------- read existing sentinel ----------
mkdir -p "$(dirname "$SENTINEL")" 2>/dev/null

EXISTING_PENDING=""
if [ -f "$SENTINEL" ]; then
    if command -v jq >/dev/null 2>&1; then
        EXISTING_PENDING=$(jq -r '.pending // [] | .[]' "$SENTINEL" 2>/dev/null)
    elif command -v node >/dev/null 2>&1; then
        EXISTING_PENDING=$(node -e '
            try {
                const d = JSON.parse(require("fs").readFileSync(process.argv[1], "utf8"));
                (d.pending || []).forEach(v => console.log(v));
            } catch (e) {}
        ' "$SENTINEL" 2>/dev/null)
    fi
fi

# ---------- merge new flags into pending set ----------
ALL_PENDING=()
declare -A SEEN=()

# Existing entries first (preserve insertion order)
while IFS= read -r v; do
    [ -z "$v" ] && continue
    if [ -z "${SEEN[$v]:-}" ]; then
        SEEN[$v]=1
        ALL_PENDING+=("$v")
    fi
done <<< "$EXISTING_PENDING"

# Then new flags
for v in "${NEW_FLAGS[@]}"; do
    if [ -z "${SEEN[$v]:-}" ]; then
        SEEN[$v]=1
        ALL_PENDING+=("$v")
    fi
done

# ---------- write sentinel if changed ----------
if [ ${#NEW_FLAGS[@]} -gt 0 ]; then
    TS=$(date -Iseconds 2>/dev/null || date "+%Y-%m-%dT%H:%M:%S")

    # Build JSON manually (portable across jq/node/neither)
    JSON='{"version":1,"pending":['
    FIRST=1
    for v in "${ALL_PENDING[@]}"; do
        if [ "$FIRST" = "1" ]; then FIRST=0; else JSON="$JSON,"; fi
        JSON="$JSON\"$v\""
    done
    JSON="$JSON],\"lastFlagged\":{"
    FIRST=1
    for v in "${ALL_PENDING[@]}"; do
        if [ "$FIRST" = "1" ]; then FIRST=0; else JSON="$JSON,"; fi
        # New flags get the new TS; existing ones keep what was there.
        # For simplicity (and because portability of preserving prior TS is
        # painful in bash), restamp existing-AND-new alike with current TS.
        # The TS is informational only — the SET membership is what matters.
        JSON="$JSON\"$v\":\"$TS\""
    done
    JSON="$JSON}}"

    echo "$JSON" > "$SENTINEL"
fi

# ---------- emit reminder if sentinel non-empty ----------
if [ ${#ALL_PENDING[@]} -eq 0 ]; then
    exit 0
fi

# Build a human-readable list
LIST=""
for v in "${ALL_PENDING[@]}"; do
    LIST="$LIST  - $v"$'\n'
done

cat <<EOF
<system-reminder>
PREFAB DRIFT PENDING — these vehicles have designer-side edits that have NOT
been baked into CombatPrefabAuthor.cs source:

$LIST
Before ANY response that recommends "re-run", "re-author", "Author X Prefab",
or any Unity menu that regenerates one of these prefabs, you MUST:

  1. Read the affected .prefab YAML (full file or targeted ranges).
  2. Diff against the values CombatPrefabAuthor.cs would generate.
  3. Bake every divergence (localPosition, localScale, m_IsActive, sprite refs,
     color, layer order, etc.) into CombatPrefabAuthor.cs as new const / seed /
     per-archetype Author* array entry.
  4. Write a capture at production/polish-captures/\$(date +%Y-%m-%d)-prefab-drift-<vehicle>.md
     listing every value baked.
  5. Show the user the diff + ask explicit approval BEFORE suggesting the
     re-author menu invocation.

If you have already completed steps 1–5 for a vehicle in this list and want
to clear it, run:
    rm production/session-state/prefab-drift-pending.json
or edit that file to remove the specific vehicle from the "pending" array.

This reminder fires on every user prompt until the sentinel is cleared.
Reference: memory rules feedback_bake_designer_edits +
feedback_pre_author_capture_protocol.
</system-reminder>
EOF

exit 0

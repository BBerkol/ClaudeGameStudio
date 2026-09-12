# Capture — `overrideSorting` Restoration on CardHand + BuffStripCanvas

**Date:** 2026-09-12
**Trigger:** Phase 5.1 blocking playtest (remediation plan §5.1)
**Files at risk:** `Assets/Prefabs/CombatView/CombatHud.prefab`,
`Assets/Prefabs/CombatView/MainBar.prefab`, `Assets/Editor/CombatPrefabAuthor.cs`
**Nature of edit:** restoration of a value the author code already intends to
write. No authored designer value is destroyed. See ruling below.

## What the playtest found

Two defects, reported by the user during the §5.1 playtest:

1. Cards render **behind** the target-area ring widgets. They should be on top.
2. The FlameBarrier buff icon is visible under the player HP bar, but hovering
   it produces **no tooltip**.

Rows that PASSED: pile chip opens on click, energy readout turns red at 0,
damage numbers appear. Status-effect application could not be exercised via a
card — no such card exists yet — so the buff row was reached through an
existing FlameBarrier instead.

## Neither defect is a Phase 5.1 regression — proven by diff

This was checked before anything was proposed, because 5.1 deleted seven
bind-or-build fallbacks and was the obvious suspect.

- **Cards.** The deleted `handCanvas.overrideSorting = true; sortingOrder = 25;`
  lived *inside* the `if (_cards == null || _cards.Length == 0)` bootstrap
  branch. Its own comment read *"Legacy fallback path only — the authored
  Combat.prefab carries this by construction."* On the authored path (which is
  live: no `[CombatHud] _cards is empty` error appears) that line never ran,
  before or after 5.1.
- **Tooltip.** The deleted `BuffTooltipWidget.Spawn` sat behind
  `if (_buffTooltip == null)`. `_buffTooltip` ships wired — it is a required row
  in `CombatWidgetPrefabWiring_Test` — so the branch was unreachable. The
  `else if` reparent branch is byte-identical pre- and post-5.1.

**Phase 5.1's deletions are vindicated.** The playtest surfaced two older
latent defects; it did not surface a regression.

## Root cause — one cause, two symptoms

`Canvas.overrideSorting` is assigned while the GameObject is **inactive**, and
the value does not persist to the saved prefab. `sortingOrder`, written in the
same block, does persist.

| Site | Author code | On disk |
|---|---|---|
| CardHand (`CombatPrefabAuthor.cs:3609` → `:3625`) | `SetActive(false)`, then `overrideSorting = true`, `sortingOrder = 25` | `m_OverrideSorting: 0`, `m_SortingOrder: 25` |
| BuffStripCanvas (`:1094` → `:1104`) | `SetActive(false)`, then `overrideSorting = true`, `sortingOrder = 22` | `m_OverrideSorting: 0`, `m_SortingOrder: 22` |

**Zero prefabs in the project carry `m_OverrideSorting: 1`** — verified by
`grep -rln "m_OverrideSorting: 1" Assets/Prefabs/ Assets/Scenes/`, which returns
nothing. A 100% failure rate across every site the author writes is systematic,
not a stray hand-edit.

### How that produces each symptom

- **Cards behind rings.** CardHand is genuinely a *nested* canvas: its
  RectTransform's `m_Father` resolves to GameObject `6552448530280038589` =
  `Combat_HUD`. A nested Canvas's `sortingOrder` is ignored unless
  `overrideSorting` is true, so cards render at Combat_HUD's **10**. The
  vehicle's world-space `HitZonesCanvas` sits at **15** and paints over them.
- **No buff tooltip.** The author's own comment at `:1072` states the purpose:
  *"Owns its own Canvas (overrideSorting + sortingOrder 5) so chip raycasts beat
  the parent BarStackCanvas … GraphicRaycaster is required for buff-chip tooltip
  hover."* With overrideSorting off, the chip raycast does not beat the parent
  bar stack, the HP bar above the icon absorbs the pointer,
  `BuffIconWidget.OnPointerEnter` never fires, and `BuffTooltipWidget.Show` is
  never called. The tooltip widget itself is sound — `_background` / `_header` /
  `_body` are all wired and `SetVisible` only toggles `.enabled`.

### Confidence

The inactive-GameObject *mechanism* is a strong inference, not an executed
proof: 3/3 assignment sites follow the pattern, the project-wide count of
`m_OverrideSorting: 1` is zero, and `sortingOrder` written in the same block
does survive. The fixes below correct the artifact regardless of whether the
mechanism is exactly this.

### Why it survived two months

`PatchCardHandCanvasMenu` (`:3372`) is a 2026-07-06 hotfix written to fix
precisely this, and it logs
`"Patched CardHand … Canvas overrideSorting=true, sortingOrder=25"` on success.
The prefab says otherwise, so it was either never invoked or it ran and silently
changed nothing. Meanwhile `CombatHud.cs:1467` asserts *"the overrideSorting=25
nested canvas"* as established fact. Same phantom-citation family the 5.1
findings flagged: a comment manufacturing authority for a value that is absent.

## Values being written

Exactly two fields change. Nothing is deleted or overwritten.

| File | Canvas block | Field | Before | After |
|---|---|---|---|---|
| `CombatHud.prefab` | `!u!223 &3615108685937019552` (CardHand) | `m_OverrideSorting` | `0` | `1` |
| `MainBar.prefab` | `!u!223 &577952370169777220` (BuffStripCanvas) | `m_OverrideSorting` | `0` | `1` |

No designer-authored value is touched. `sortingOrder` (25 / 22), anchors,
sizeDelta, pivots, colours, and every child override are left exactly as they
are. Both canvases already carry a `GraphicRaycaster`
(GUID `dc42784cf147c0c48a680349fa168899`, cross-checked against MainBar's
`m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.GraphicRaycaster`), so
enabling `overrideSorting` cannot orphan input — that raycaster is there
specifically because the author expected overrideSorting to be on.

## Route chosen, and the one deviation

Surgical YAML edit for **both** canvases.

The original proposal was to run the existing
`Tools > Wasteland Run > Patch CardHand Canvas (z-order)` menu for CardHand.
**Deviated deliberately.** That menu ends in
`PrefabUtility.SaveAsPrefabAsset(root, hudPath)`, a whole-prefab rewrite, and
`project_author_scenes_never_idempotent` records that author/save round-trips
remint fileIDs. A two-field YAML edit is strictly narrower, fully reviewable in
the diff, and cannot churn anything it does not name. `MainBar.prefab` has no
equivalent patch menu at all and is under the drift sentinel, so it needed the
surgical route regardless — using one route for both keeps the change uniform.

## Author-side fix

The YAML edit repairs today's artifact. Without an author fix, the next author
run reintroduces the bug. Both sites are reordered so the Canvas is configured
while its GameObject is active, then deactivated before children are added. At
both sites the GameObject has no children at that point, so activating it
briefly runs nothing but the Canvas/GraphicRaycaster themselves.

## Gate

A grep gate in `tools/ci/grep-gates.sh` asserting both prefabs contain
`m_OverrideSorting: 1`. This is the load-bearing enforcement: `UNITY_LICENSE` is
unset, so the EditMode job skips in CI and grep gates are the only server-side
check (`project_ci_enforcement_reality`).

The 2026-07-06 hotfix is the argument for the gate. A fix without one rots
silently and leaves a success log behind as false evidence.

## Technical Director Review

No TD agent was spawned for this edit. Offered to the user and explicitly
declined in favour of proceeding; recorded here as the protocol requires, with
the reasoning that would have been briefed:

**This is a restoration, not a destructive edit.** The
capture-before-destroy protocol exists to stop authored designer values being
silently overwritten by regenerated content. Here the direction is the reverse —
the author code is the source of truth, it already specifies
`overrideSorting = true` at both sites with written rationale, and the artifact
has drifted *away* from it. The edit moves the prefab toward what the author
already declares, touches two boolean fields, and destroys nothing. The
categorical-fit question (`feedback_td_briefing_discipline`) — *what does this
prefab claim to be?* — answers cleanly: `CombatHud.prefab` claims to be a HUD
whose card hand sorts above per-vehicle bars, and `MainBar.prefab` claims to be
a bar whose buff chips are hoverable. Both claims are currently false. The edit
makes them true.

Residual risk accepted: enabling `overrideSorting` detaches each canvas from its
parent's batch, a minor draw-call cost on two small canvases, which is the cost
the author intended to pay when it added the dedicated `GraphicRaycaster`.

## Approval

User approved 2026-09-12 ("go ahead"), covering all three parts: the two
prefab edits, the author-side reorder, and the CI gate.

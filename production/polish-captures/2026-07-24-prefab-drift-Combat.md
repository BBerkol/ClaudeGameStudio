# Prefab Drift Bake — Combat (CombatHud.prefab CardHand Canvas)

**Date:** 2026-07-24
**Sentinel entry:** `Combat` (flagged 2026-07-06T14:50:58+03:00)
**Actual drifted file:** `Assets/Prefabs/CombatView/CombatHud.prefab` (nested inside Combat.prefab by asset ref)
**Combat.prefab itself:** clean, no working-tree drift (last bake commit `78696fa`)

## What drifted

Designer added 2 components to `CardHand` GameObject inside `CombatHud.prefab` on 2026-07-06 to fix card z-order (cards now render above per-vehicle bars at HudAnchors sortingOrder 20 / BuffStrip 22, below Popups at 60):

| Component | Field | Value |
|---|---|---|
| `Canvas` | `m_OverrideSorting` | 1 (true) |
| `Canvas` | `m_SortingOrder` | 25 |
| `Canvas` | `m_RenderMode` | 2 (nested canvas — value driven by Unity; not authored) |
| `Canvas` | `m_PlaneDistance` | 100 (Unity default) |
| `GraphicRaycaster` | `m_IgnoreReversedGraphics` | 1 (Unity default) |
| `GraphicRaycaster` | `m_BlockingObjects` | 0 (None — Unity default) |
| `GraphicRaycaster` | `m_BlockingMask.m_Bits` | 4294967295 (Everything — Unity default) |

## Bake status

**Bake is ALREADY DONE in the working tree** (uncommitted). No new code edits required. Two code paths in `Assets/Editor/CombatPrefabAuthor.cs`:

### Path 1 — canonical author path
`AuthorCombatHud()` at line ~3670:

```csharp
Canvas handCanvas = handGo.AddComponent<Canvas>();
handCanvas.overrideSorting = true;
handCanvas.sortingOrder = 25;
handGo.AddComponent<GraphicRaycaster>();
```

Rationale (from source comment): "cards render above per-vehicle bars (HudAnchors 20 / BuffStrip 22) without dragging the rest of Combat_HUD (EndTurn, EnergyOrb, IntentCanvas, TurnPhaseBanner) along with it. Sits below Popups (60) so damage numbers still land on top. Own GraphicRaycaster because overrideSorting cuts the child raycaster group off the parent's raycast batch."

### Path 2 — targeted patch menu (idempotent hotfix)
`PatchCardHandCanvasMenu()` at line ~3413, menu item `Tools/Wasteland Run/Patch CardHand Canvas (z-order)`:

```csharp
Canvas canvas = handGo.GetComponent<Canvas>();
if (canvas == null)
    canvas = handGo.AddComponent<Canvas>();
canvas.overrideSorting = true;
canvas.sortingOrder = 25;
if (handGo.GetComponent<GraphicRaycaster>() == null)
    handGo.AddComponent<GraphicRaycaster>();
```

Comment tags this as `2026-07-06 hotfix` — matches sentinel flag date. Explicit design: patch-only menu so rebuilding the whole HUD doesn't reset the 3 sibling-canvas designer tweaks (Combat_HUD/Popups/Debug sortingOrder 10/60/110 per `project_combat_scene_architecture`).

## Value-match verification

| Field | Prefab YAML | Author code sets | Match |
|---|---|---|---|
| `Canvas.overrideSorting` | 1 | `true` | ✓ |
| `Canvas.sortingOrder` | 25 | `25` | ✓ |
| `GraphicRaycaster` component | present | `AddComponent<GraphicRaycaster>()` | ✓ |
| `GraphicRaycaster` fields | Unity defaults | Unity defaults (no field writes) | ✓ |

Re-running `Tools/Wasteland Run/Author Combat HUD Prefab` OR `Tools/Wasteland Run/Patch CardHand Canvas (z-order)` will regenerate this drift correctly and idempotently.

## Also in the CombatPrefabAuthor.cs working tree (unrelated, do NOT commit under this bake)

Slice E RunHUDHost.prefab integration in `AuthorAllScenes` + `AuthorRunScene`:
- `AuthorRunHUDHost.AuthorRunHUDHostPrefab()` call added before flush-to-disk
- `InstantiatePrefab(runHudHostPrefab, scene)` inside `AuthorRunScene` as scene-root sibling
- Log message updates

These are Slice E work-in-progress, separately queued. Not part of the CardHand Canvas drift bake.

## Next steps (require user approval)

1. Clear sentinel: `rm production/session-state/prefab-drift-pending.json` (or edit "pending" array to drop "Combat")
2. Optional: commit the CardHand Canvas bake portion of `CombatPrefabAuthor.cs` separately from the Slice E RunHUDHost work — or bundle if Slice E commit is imminent

## Technical Director Review

Skipped for this bake — no code changes required (bake was already present in working tree; this capture is documenting the already-done state before clearing the sentinel). If user wants a TD verdict on the sibling-canvas z-order design retroactively, note that `project_combat_scene_architecture` memory already sets the 10/60/110 layering contract that this drift extends coherently (25 slots between HudAnchors 20 and Popups 60).

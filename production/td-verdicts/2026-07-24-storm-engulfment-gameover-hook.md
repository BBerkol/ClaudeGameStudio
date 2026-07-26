# TD Verdict — Storm-Engulfment Game-Over Hook (V2 wiring slice)

**Date:** 2026-07-24
**System:** Out-of-fuel V2 game-over screen — subscription + view wiring
**Files at risk:**
- `Assets/UI/GameOverView.uxml` (already exists, uncommitted; drafted 2026-07-06 pre-pivot — layout still valid)
- `Assets/UI/GameOverView.uss` (new)
- `Assets/UI/GameOverPanelSettings.asset` (new — dedicated sortingOrder 100)
- `Assets/Scripts/UI/GameOverViewController.cs` (new — mirrors `RunCompleteViewController`)
- `Assets/Scripts/CombatView/RunSceneOverlayHost.cs` (subscription + handler)
- `Assets/Editor/CombatPrefabAuthor.cs` (mount block in `AuthorRun`)
- Run.prefab, RunScene.unity (re-authored consequence — Combat.prefab untouched)

## Context

The 2026-07-06 verdict (`2026-07-06-out-of-fuel-gameover.md`) shipped the
V1 shape (`OnRunStranded` → immediate game-over). User pivoted same day to
V2 (stranded fires `OnAutoStormBegan`; ticker advances a storm cursor; when
the cursor engulfs the player's beacon it fires `OnStormEngulfed` — that's
the actual game-over trigger). The V2 model+ticker+visual work landed
(commits `81088de`, `e0a1461`, `153b911`) but the view+wiring never did:
`Assets/UI/GameOverView.uxml` was drafted pre-pivot and left untracked;
`GameOverViewController.cs` was never created; `RunSceneOverlayHost` has
zero references to `OnStormEngulfed`.

Grep verified today: `RaiseStormEngulfed()` fires at
`StormEngulfmentController.cs:144`, but nowhere in the codebase does
`OnStormEngulfed +=` appear. The event fires into the void — game
silently halts on the storm ticker without a game-over screen.

## Technical Director Review

**Verdict: ACCEPT-WITH-AMENDMENTS.** Delegated to the 2026-07-06 verdict's
2026-07-24 V2 Amendment section for the full self-audit and answers to
the six briefing questions. Summary of the applied AMENDs:

- **A3** — Defer view-show by one frame (`StartCoroutine` + `yield return null`)
  because `RaiseStormEngulfed` fires mid-storm-coroutine before the terminal-
  tick paint settles.
- **A4** — Dedicated `GameOverPanelSettings.asset` with `sortingOrder = 100`
  so the terminal overlay paints deterministically above MapView +
  RunCompleteView (which both share the default `PanelSettings` at
  sortingOrder 0). Terminal-tier convention matches HUD canvas ordering
  (10/60/110).
- **A5** — Doc-surface cleanup: retire references to the pre-pivot
  `OnRunStranded` event; V2 renamed it to `OnAutoStormBegan`. Game-over
  trigger is `OnStormEngulfed`.

**Question answers (see amendment for full rationale):**

1. Controller lives at `Assets/Scripts/UI/GameOverViewController.cs`
   (mirrors `RunCompleteViewController`). Keeps ADR-0014 UI asmdef arrow
   intact.
2. Subscription owns on `RunSceneOverlayHost` (not a sibling
   `GameOverHost`) — game-over is a one-shot terminal overlay swap, not a
   per-tick paint pipe like `StormMapVisualHost`.
3. `HandleStormEngulfed` calls `_mapView.HideStormFront()` first, then
   `_mapView.Hide()`, then shows the game-over panel. Placeholder arc under
   a scrim reads as a bug, not doom.
4. RETRY wires to `RunSceneHost.RestartRun()` — same primitive as F5 debug
   and CombatOutcomeOverlay defeat. Becomes the 4th caller.
5. See A3.
6. See A4.

**Three-lens self-audit:** health / optimization / 1.0-survival — all
green. Full audit paragraphs in the amendment section of the paired
2026-07-06 verdict.

## Cross-reference

Full self-audit + question rationale + amendment history:
`production/td-verdicts/2026-07-06-out-of-fuel-gameover.md`, section
`## 2026-07-24 V2 Amendment — Storm-Engulfment Trigger Wiring`.

## ADR alignment

- **ADR-0011** (no bridges) — clean. `OnRunStranded → OnAutoStormBegan`
  rename already landed; this slice adds `OnStormEngulfed` subscription
  additively. No vestigial event fossils.
- **ADR-0014** (UI Toolkit primary) — new controller uses UIDocument +
  UXML + USS. No UnityEvent. Action-delegate event surface matches
  `RunCompleteViewController`.
- **ADR-0015** (data-flag lagging-dep) — RETURN TO MENU button ships
  `SetEnabled(false)` cleanly; wires up when main-menu scene ships.
  Mastery Progression bar ships as an unwired placeholder; Bind overload
  lands when MasteryState arrives.

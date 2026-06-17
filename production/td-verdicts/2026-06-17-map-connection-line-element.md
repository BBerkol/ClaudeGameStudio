# TD Verdict — ConnectionLineElement (map frontier-edge rendering)

**Date:** 2026-06-17
**Trigger:** Slice 6 eyeball pass surfaced unreadable map (all 30 graph edges rendered every frame, only one orange line findable) + click crash (BeaconNodeElement fires on non-reachable nodes → NodeMap.Advance throws). User specified frontier-only + dashed + slight curvature for v1; polish backlog (icons, hover popout, white outline) explicit but deferred.
**Status:** **ACCEPT** with two non-blocking notes.

---

## TD Verdict

**ACCEPT.**

Painter2D on a sealed `VisualElement` subclass is the canonical UI Toolkit primitive for runtime custom 2D draw in Unity 6.3. The considered alternatives confirm it:

- **(a) Dash-as-mini-VisualElements** — hierarchy bloat (~30-40 leaves per redraw across ~5 frontier edges). Stalls the polish backlog: animated dash phase, layered outline strokes, hover glow all want a single paint callback, not 40 child layout passes.
- **(b) SVG asset per edge** — data-driven mass for what is fundamentally a procedural primitive parameterized by two normalized endpoints.
- **(c) Keep old straight-line helper + filter to frontier** — drops the user-given dashed/curvature requirement. Non-starter.

ADR alignment clean:

- **ADR-0014 (UI Toolkit primary stack)** — Painter2D is in-stack. New element lives in `WastelandRun.UI` asmdef, no arrow violation.
- **ADR-0011 (no bridges at done)** — old `BuildConnectionLine` helper deleted in the same edit. USS `.wr-connection` + `.wr-connection--reachable` rules stripped in the same commit. No bimodal rendering path, no transitional comment.

## Files in scope

- **NEW:** `Assets/Scripts/UI/ConnectionLineElement.cs` (~120 lines, sealed class)
- **EDIT:** `Assets/Scripts/UI/MapViewController.cs` (frontier filter in `BuildConnectionViewModels`, drop `BuildConnectionLine`, instantiate `ConnectionLineElement` in `RebuildConnections`)
- **EDIT:** `Assets/UI/MapView.uss` (strip `.wr-connection*` rules)
- **EDIT (companion):** `Assets/Scripts/UI/BeaconNodeElement.cs` — click gate on `IsReachable` (4 lines, fixes the crash)

## AMEND-class notes (non-blocking)

1. **Hardcoded `#d97a3a` stroke color** — fine as forward-looking seam, but file the TODO as a polish-backlog line in the Slice 6 closeout capture so it doesn't drift. ADR-0011 tolerates this only if tracked, not if invisible.

2. **`MarkDirtyRepaint` on endpoint changes** — confirm `generateVisualContent` re-fires when endpoints change. *Design response: not applicable in current shape — `_fromNorm` and `_toNorm` are `readonly`, set only in the constructor. The controller clears the connections layer and re-adds elements on every `Bind`, so endpoint mutation is impossible. Element creation auto-dirties the paint callback; contentRect resize auto-invalidates layout. If a future polish task adds mutable endpoint setters, that setter must call `MarkDirtyRepaint()` and this verdict needs an amendment.*

## Final-game picture this serves

The map is the run's strategic decision surface. Player reads "where can I go" in one glance. Polish backlog hangs off this primitive cleanly:

- Dashed phase animation → "crawling ants" flow toward beacon
- Color → beacon-type hint (combat / haven / merchant)
- Layered strokes → hover white outline + glow

None of that requires a rewrite of the primitive — just additional draw calls inside `OnGenerateVisualContent` or a constructor parameter for stroke color.

## Followup

- Hardcoded color TODO → tracked in Slice 6 closeout capture polish-backlog section.
- Painter2D pattern is reusable for any future custom 2D draw on the UI Toolkit stack (combat HUD overlays, reward popouts, etc.).

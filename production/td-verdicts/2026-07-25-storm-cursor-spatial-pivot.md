# TD Verdict — Storm Cursor Spatial Pivot (V3.1)

**Date:** 2026-07-25
**System:** Storm cursor (out-of-fuel game-over path)
**Author:** Technical Director
**Status:** APPROVED — Shape B (float NormalizedX cursor)
**Predecessor:** `2026-07-24-storm-persistent-cursor.md` (V3 shipped a discrete beacon-index cursor; this verdict pivots it to a spatial-X cursor before that shape lands on main)

## TD Verdict

**Ship Shape B.** The V3 cursor semantics (`CursorIndex : int` = "Nth-placed beacon") are the defect, not the visual. The user's mental model — "storm advances a consistent pixel-delta per strip" — is not expressible under any integer-index shape because `BiomeWebGenerator` places beacons via uniform Poisson-disc scatter (verified: `TryPlaceUniformPoisson` at line 188, no X-sort before `AssignBeaconTypes` at line 236), so index++ produces a spatially-arbitrary sweep. Shape A (sort by X at gen time) partially fixes the visual but doesn't give fixed-pixel strips and touches the frozen generator surface. Shape C is a bimodal path (integer model + spatial mapping table) — ADR-0011 #3 trap. Shape D violates the 5-slice freeze.

**Uncommitted state is our friend here.** The V3 shape lives on disk unpushed. Rewriting cursor semantics before landing is a same-slice pivot, not a migration. One atomic commit, no bridges.

## Answers

**Q1 — Shape.** B. Rationale above. Generator freeze (2026-07-07) is respected: `BiomeWebGenerator` is untouched. The change is entirely downstream in `StormState` + view.

**Q2 — Engulfment predicate home.** **Session-side with an injected `Func<int, float> beaconXProvider`.** The predicate `Storm.CursorX >= beaconX(Player.CurrentIndex)` is model-authoritative game-over — it must not depend on view lifecycle. `RunSceneHost.BeginNewRun` already knows `NodeMap` (positions); pass `i => nodeMap.Beacons[i].NormalizedPosition.x` into `RunSession` ctor alongside `_havenFuelRefillPercent`. This preserves the "model fires game-over synchronously in `Advance`" contract from the V3 verdict (Q6). The alternative (view-side predicate) would mean two subscribers can disagree about "is the player engulfed right now" — silent-progression-invariant break.

**Q3 — Save-rehydration for the 2026-07-25 in-flight save.** **Bump `SchemaVersion` from 1 → 2 in `StormStateDto`; the ADR-0004 EA-mode policy triggers a group-cascade regenerate.** Under `run.session_core` group membership (from V3 verdict), a schema mismatch on `StormStateDto` causes the whole session_core group (NodeMap + RunSeed + RunDeck + Storm) to fall through to fresh-run. This is the intended behavior — a mid-run int→float cursor with no numeric compat (index 3 ≠ X=3.0) can only round-trip through save-invalidation. Document in changelog: "2026-07-25 in-progress runs will restart from Depart at next launch." User has one save affected; acceptable cost given zero shipping runs exist.

**Q4 — Default `StormAdvancePerStripNormalized`.** **0.05f**, exposed on `BiomeDistributionSO` as `_stormAdvancePerStrip` (float, `OnValidate` clamp `(0f, 0.2f]`). Matches user's stated 50px-per-strip target on a 1000px map, and preserves ADR-0015 lagging-dep pattern (Biome 2 tunes without code change). Rename `_initialStormBeaconIndex` (added in V3) → `_initialStormCursorX` (float, default `-0.15f` = 3 strips of grace runway at 0.05/strip, `OnValidate` clamp `[-1.0f, 0f]`).

**Q5 — Three-lens self-audit.**

*Lens 1 (Codebase Health):* ADR-0011 clean — uncommitted V3 shape rewritten in place, zero bridges. `MapViewController.StormFrontX(int)` (line 430) rewrites to `StormFrontX(float cursorX) => cursorX * mapWidth`, drops the negative-cursor extrapolation branch (redundant when cursor is native normalized-X). `FlipSwallowedThrough(int)` (line 921) rewrites to `FlipSwallowedThroughX(float)` scanning `_beaconPositions[i].x <= cursorX`. `BeaconTravelTick.PreviewedStormCursorBefore/After` (Amendment E1) becomes `float` — same rename shape. Subscription lifecycle (Bind/OnDestroy for `OnStormAdvanced`) preserved from V3 — no regression. No dual-cursor risk: `_beaconPositions` is view-cached from `NodeMap`, session's injected provider reads the same source.

*Lens 2 (Optimization):* `StormAdvanceTick` payload grows from 3 int → 3 float (12 bytes). Still `readonly struct`, no boxing. `Advance` mutation stays scalar. `StormFrontX` becomes O(1) identity (was O(1) array-index + extrapolation branch) — net cheaper. `FlipSwallowedThroughX` still O(beacons) scan, unchanged cost. No per-frame allocation. Confirmed, no delta.

*Lens 3 (1.0-Shape Survival):* `float CursorX` is the canonical 1.0 shape — every future storm concern (retreat mutators, spike mechanics, per-tile visual rendering) reads spatial-X natively. Integer cursor was throwaway scaffolding disguised as canonical (the predecessor verdict got this one wrong; V3 built the wrong 1.0 shape). Payload extension seam preserved — `readonly struct StormAdvanceTick(float FromX, float ToX, float PlayerX)` grows cleanly for future fields (e.g., `int StripsThisTick` if a HUD forecaster needs it). Cluster-lane biomes (2026-07-07 generator) work natively — arc apex is always at a spatial X, no per-lane resolution logic needed. **Delta:** `beaconXProvider` injected via ctor is a new session dependency. If NodeMap regenerates mid-run (it doesn't today, but a future "reroll biome" mechanic might), provider must be re-injected. Document in `RunSession` xmldoc.

## Migration Manifest

Same-slice pivot, one atomic commit. All V3-uncommitted files touched:

**Modified:** `StormState.cs` (int→float cursor, `Advance(int strips, float perStrip)` OR `Advance(float delta)`), `StormStateDto.cs` (SchemaVersion 1→2, fields → float), `StormStateSerializable.cs` (JSON shape follows DTO), `StormState_Test.cs` (float assertions), `StormStateDto_round_trip_test.cs` (float round-trip), `RunSession.cs` (ctor gains `Func<int, float>` provider; `Advance` uses `Storm.Advance(strips * _stormAdvancePerStrip)` + `Storm.CursorX >= _beaconXProvider(playerIndex)`), `RunSceneHost.cs` (inject provider), `BiomeDistributionSO.cs` (`_initialStormBeaconIndex` → `_initialStormCursorX`, add `_stormAdvancePerStrip`), `MapViewController.cs` (`StormFrontX(float)`, `FlipSwallowedThroughX(float)`, `PlayStormAdvance(float, float, float)`), `StormMapVisualHost.cs` (float payload), `BeaconTravelTick.cs` (Amendment E1 fields → float), `StormAdvanceTick.cs` (int→float on all three fields, xmldoc rewrite).

**Untouched:** `BiomeWebGenerator.cs` (freeze respected), `FuelState.cs` (strip count still int, computed inside `Spend`), `StormAdvanceVisualPacer.cs` (paces off event — payload change transparent).

## Success Criteria

1. Storm apex advances ~50px per non-Haven commit on default 1000px map (0.05 normalized × 1000 = 50).
2. Swallowed beacons are spatially left-of-arc — no random-looking chips ahead of the front.
3. Player visually ahead of arc is not engulfed; player visually behind is (predicate matches visual).
4. Save from V3 shape triggers group-cascade regenerate on load (single user save affected, acceptable).
5. EditMode green including updated `StormState_Test` float variants and `StormStateDto_round_trip_test` v2.

If (1) is off, retune SO — not code (ADR-0015). If (3) fails, `beaconXProvider` is wired wrong — grep `RunSceneHost.BeginNewRun` for the injection site.

---

**Your call.** Same-slice pivot cost is small (uncommitted V3 rewrites in place), the alternative (ship V3 int-cursor + retrofit later) burns another verdict cycle and locks another slice of subscribers onto the wrong shape.

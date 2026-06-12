# Combat Demo Backlog

**Goal**: Polished, playable combat demo. 1 player vehicle + 3 enemies (Dune Skimmer, Iron Shepherd, Dredge) fully populated with parts and subsystems, ready to swap art assets. State must be solid enough that when combat gets hooked into the larger run/map/meta layers later, no rework is needed.

**Demo flow**: Player fights 3 enemies sequentially, picks a reward between fights. Reward picker (`CardRewardPicker.cs`) already exists — needs wiring.

**Process rules** (locked 2026-05-30):
- Living list. Add items as discovered. Don't gate work on backlog completeness.
- Not shipping. Don't optimize for store/cert/EA. Optimize for "feels right and is wired clean."
- Regular spaghettification audits. After every 2–3 items, sweep for dead code, redundant abstractions, or copy-paste that's grown legs.
- Slice 2.7 (LegacySlotKind retirement) **deferred indefinitely** — bridge is paid debt, see ADR-0009 amendment.

---

## Bugs (active)

### B1 — Dredge armor bar displays 0 instead of 60
**Symptom**: Dredge spawns in Play Mode showing 0 armor on its main bar, despite `armor_0` slot installed at 60hp.
**Root cause**: `MainBarWidget.cs:232` reads `target.MaxArmor` / `target.CurrentArmor` — the legacy armor pool, which is 0 in layout mode (Dredge). The 60hp lives on `armor_0.Hp`, where the widget doesn't look.
**Fix**: Add `Vehicle.EffectiveMaxArmor` / `Vehicle.EffectiveCurrentArmor` properties — sum across `SlotKind.Armor` slot instances in layout mode, fall back to legacy `MaxArmor`/`CurrentArmor` in legacy mode. `MainBarWidget` reads those two. `DebugStatsWidget.cs:88` likewise.
**Scope**: 1 production file (Vehicle.cs +2 props), 2 view files (MainBarWidget, DebugStatsWidget), 1 test file (new tests for the props in both modes).
**Risk**: low — player is legacy mode so player armor untouched; layout-mode enemies (Shepherd unarmored, Dredge 60-armor) get correct display.

## Missing UI / UX (active)

### U1 — Enemy name on top of main bar
**Symptom**: Main bar widget doesn't display `Vehicle.Name` above the HP/armor bars. Player can't read which enemy they're fighting from the bar block alone.
**Fix**: Add name label to `MainBarWidget` (or its parent `VehicleBarStack`), anchored above the HP bar. Use existing TMP font / palette.
**Scope**: 1 view file edit + prefab re-author (`CombatPrefabAuthor.cs`).
**Risk**: low — purely additive; need to bake designer prefab tweaks per "Pre-Author Capture Protocol" memory before re-running author.

## Demo wiring (deferred until bugs/UI cleared)

### D1 — 3-enemy sequence runner
- Player vs Skimmer → reward pick → Player vs Shepherd → reward pick → Player vs Dredge → end-screen
- Reward picker (`CardRewardPicker.cs`) already exists. Needs encounter chaining + post-victory hand-off.
- Player vehicle state (hand, deck, hp, armor) must persist between fights.

### D2 — Combat HUD polish items (from memory `combat_hud_backlog.md`)
Spec'd-but-unbuilt:
- HP damage pulse + death cascade
- Chase-rail parallax differential
- Motion burst
- HP/Armor bar visual redesign (user-flagged)

## Cosmetic — deferred to FrameLayoutSO migration

These items require touching `IFrameLayout` POCO singletons (`SmallFrameLayout.cs`, `DredgeFrameLayout.cs`, etc.) to retune `SlotDefinition.HudAnchor` UVs. Per TD verdict 2026-06-09, hold all UV tuning until the slice-2.6 `FrameLayoutSO` migration so it lands in one pass — no bake step, no offset-map shim.

### C1 — Dredge HP/Armor bar HudAnchor UVs not hand-tuned against final sprite
**Status**: Math/projection working (chassis `localBounds` × `localToWorldMatrix` sampled at `BindForCombat`, UV→world→canvas chain verified in Play Mode). Positions deterministic but visually off because Dredge's `HudAnchor` UVs in `DredgeFrameLayout.cs` are first-pass guesses, not hand-tuned against the boss sprite.
**Fix-when**: During `FrameLayoutSO` slice-2.6 migration — tune UVs in Inspector against the live sprite, save SO asset.
**Do not**: Tune in `DredgeFrameLayout.cs` now (would re-do during migration) or build offset-map / bake shim (PR to delete later).

## Cleanup / spaghettification audits (running)

Cadence: every 2–3 backlog items, run a sweep. Targets:
- Dead helpers introduced during the ADR-0009 migration that no longer have consumers (precedent: `WeightModifiers.ZeroIfArmorFull()` deletion in 2.5c-2)
- Copy-paste between archetypes (3 enemies, watch for duplicated brain wiring)
- View layer reading legacy pools where layout-mode aggregates would be cleaner
- ADR drift — code that's evolved past the doc

---

## Resolved

_(none yet)_

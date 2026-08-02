---
date: 2026-08-02
system: Chopshop workbench (Phase 2.5) — post-slice bug fixes + Biome 1 map-gen tuning
files_touched:
  - Assets/UI/ChopshopScreen.uxml
  - Assets/UI/ChopshopScreen.uss
  - Assets/Editor/CombatPrefabAuthor.cs
  - Assets/Prefabs/BeaconRoots/ChopshopRoot.prefab
  - Assets/Resources/Run/Biomes/Biome1Distribution.asset
  - Assets/Scripts/Run/BiomeWebGenerator.cs
adrs_at_risk: []
predecessor_commit: 8556250
---

# Chopshop bug fixes + Biome 1 tuning — 2026-08-02

## Overview

Chopshop workbench slice landed as commit `8556250` (2026-08-02) but wasn't
play-verified end-to-end. Play test surfaced two bugs (both shared root
cause) and two adjacent map-gen concerns. This capture bundles all four
resolutions into one commit per user direction.

## Bugs fixed

### Bug 1 — Vehicle not visible in Chopshop scene

- **Symptom:** Chopshop scene showed only the UXML panel + a flat scrim
  background where the parked vehicle should be.
- **Root cause:** `ChopshopScreen.uxml` inherited the post-repair-strip
  `RestScreen` pattern — an opaque `<illustration>` `VisualElement`
  rendered on a Screen Space Overlay UIDocument, which always paints on
  top of the world-space scene camera. The vehicle SpriteRenderers under
  `ChopshopRoot.prefab` were rendering correctly; the UI was covering
  them.

### Bug 2 — Weld repair not firing (cursor didn't trigger sparks/drain)

- **Symptom:** Clicking `1. Weld armor and parts back together.` opened
  repair mode (cursor swap + budget bar shown), but hovering the vehicle
  never fired `VehiclePartHitZone` events → no sparks, no drain.
- **Root cause:** Root UXML element had `picking-mode="Position"`, so
  the entire UI Toolkit surface intercepted every click / hover — the
  underlying UGUI colliders on the parked vehicle never received input.

## Pre-change authored values being replaced

Nothing designer-tuned is destroyed by this bundle. The changes:

- **UXML `<illustration>` element** — deleted. Was placeholder from the
  post-strip RestScreen copy, never held authored per-Chopshop art.
- **USS `.wr-chopshop-illustration` rule** — deleted along with the
  element; `background-image: resource("Chopshop BG")` cited the same
  sprite that now renders as a world-space SpriteRenderer (see below).
- **USS root `background-color`** — moved from opaque
  `var(--wr-color-ember-scrim)` token → `rgba(0, 0, 0, 0.30)` light scrim.
  Matches the pre-strip `RestPicker.uss` value at git `b26fc77^`.
- **UXML root `picking-mode`** — flipped `"Position"` → `"Ignore"`
  with a load-bearing comment covering: (a) why the flip is required
  (VehiclePartHitZone passthrough), (b) why the backdrop is world-space
  (Screen Space Overlay can't render behind world geometry), and (c) how
  to add a future modal without re-breaking hover (child element with
  `picking-mode="Position"`, NOT a root flip).

## Changes applied

### 1. UI Toolkit surface — passthrough + light scrim

- `Assets/UI/ChopshopScreen.uxml` — root `picking-mode="Position"` →
  `"Ignore"`; deleted the `<illustration>` element; added the load-
  bearing comment (documents future-modal-blocker guidance from TD).
- `Assets/UI/ChopshopScreen.uss` — root `background-color` to
  `rgba(0, 0, 0, 0.30)`; deleted `.wr-chopshop-illustration` rule.
- Pattern source: pre-strip `RestPicker.uss` / `.uxml` at git
  `b26fc77^` (verified before edits — pre-strip design deliberately
  used the picking-Ignore + no-illustration + light-scrim combination
  for exactly this passthrough case).

### 2. World-space garage backdrop

- `Assets/Editor/CombatPrefabAuthor.cs :: AuthorChopshopRootPrefab` —
  spawns a `Backdrop` SpriteRenderer child of `ChopshopRoot` at
  `Vector3.zero`, rendering the `Chopshop BG_0` sprite at
  `sortingOrder: -100`.
- Sprite loaded via `AssetDatabase.LoadAllAssetsAtPath` and filtered to
  `Sprite` — `LoadAssetAtPath<Sprite>` returns null for
  `spriteMode = Multiple` assets, only the master `Texture2D` comes
  back. Enumeration is required; this file only has one sub-sprite so
  the first hit wins.
- Camera math: `1920 × 1080 @ 100 PPU = 19.2 × 10.8` world units;
  orthographic-size-5 camera viewport is `17.78 × 10`; centering at
  (0, 0) fully covers the visible frame. `sortingOrder: -100` sits
  below the vehicle Frame layer (0) and behind-plane parts (-1).
- `ChopshopRoot.prefab` re-authored to bake the new child.

### 3. Vehicle rest-pose override (nested-instance polymorphism)

- `Assets/Editor/CombatPrefabAuthor.cs :: AuthorChopshopRootPrefab` —
  overrides the nested `VehicleRestPose._restLocalPosition` to
  `(-2f, -2.5f, 0f)` so the vehicle parks in the garage lower-left
  quadrant, clear of the right dialogue panel and reading over the
  backdrop's floor plane.
- Override lives on the nested instance ONLY. `PlayerVehicle.prefab`'s
  default `_restLocalPosition = Vector3.zero` is preserved for any
  future beacon that wants dead-center parking (Rest, if it ever
  re-adopts the pattern, etc.).
- Position tuned by-eye against a user-provided screenshot; user
  approved as "ok for now" (may iterate later via Prefab Mode → I
  re-bake the author when they land a final value, per
  `feedback_bake_designer_edits.md`).
- `ChopshopRoot.prefab` re-authored to bake the override.

### 4. Biome 1 map-gen tuning (separate TD verdict — see below)

- `Assets/Resources/Run/Biomes/Biome1Distribution.asset` —
  `_maxEdgeLength: 1000 → 550`, `_reconnectRadius: 1000 → 450`,
  added `{ Type: 4 (Chopshop), Weight: 5 }` to non-terminal pool
  (~4.8% share → 2-3 Chopshops per 55-beacon map).
- `Assets/Scripts/Run/BiomeWebGenerator.cs :: AdjacencyBannedTypes`
  += `BeaconType.Chopshop` (same-type clustering ban, matches
  Rest + Merchant precedent).
- Bundled here per user's "commit them together once Chopshop is
  completely done" instruction. TD verdict for this half lives at
  `production/td-verdicts/2026-08-02-biome1-tuning-and-chopshop-weight.md`.

## Play-mode verification

- Vehicle visible over garage BG. ✓
- Weld choice opens repair mode; cursor hovers vehicle parts; budget
  bar drains; sparks fire. ✓
- Vehicle rest position: user-approved "ok for now". ✓
- Biome map-gen tuning: separate verification per TD verdict AC (5
  seeded runs — user to confirm post-commit).

## Excluded from commit

- `Assets/Prefabs/CombatView/Combat.prefab` — 3 `m_Camera: null`
  property overrides from opening the prefab in the editor. Unity
  noise, not a functional change; excluded.

## Technical Director Review

### Bug fix bundle (this capture)

**ACCEPT** — apply all four changes.

- **ADR-0011 no-bridges:** Clean. Backdrop-as-sibling-SpriteRenderer is
  the established pattern (world-space BG under beacon root,
  sortOrder -100). Not a bridge — it's the canonical Chopshop
  composition. `_restLocalPosition` override on nested instance is
  per-beacon polymorphism (ADR-0011 exception #4), not parallel
  storage. Prefab default stays `Vector3.zero`.
- **Generator SO freeze:** Zero risk. This bundle touches
  `Assets/UI/ChopshopScreen.{uxml,uss}`, `CombatPrefabAuthor.cs`,
  `ChopshopRoot.prefab`. No BiomeDistributionSO / BiomeGenerationInputs
  / BiomeWebGenerator SURFACE touched (biome half is asset values +
  private HashSet extension — cleared under separate verdict).
- **Load-bearing comment:** Good defensive move. The
  `picking-mode="Ignore"` reason (VehiclePartHitZone passthrough) +
  future-modal-blocker guidance is exactly the kind of invariant that
  would get "cleaned up" in a future refactor without it.

**Three-Lens Self-Audit**

- **Codebase Health:** Zero ADR-0011 drift. `AuthorChopshopRootPrefab`
  growing (backdrop spawn + rest-pose override) is on-charter —
  author-time composition is exactly its job. Rule-of-3 preemption:
  `LoadAllAssetsAtPath` + sortOrder-`-100` backdrop pattern now exists
  in Chopshop only; if a third beacon adds a backdrop, extract
  `AuthorBeaconBackdrop(root, spriteName, sortOrder)` helper before
  the third caller lands.
- **Optimization:** Editor-only cost is a single sprite load. Runtime:
  one extra sprite draw call at sortOrder -100 (batches with URP 2D
  renderer). Well below the 200 draw call budget. `picking-mode="Ignore"`
  is cheaper than Position (skips hit-testing subtree). Net win.
- **1.0-Shape Survival:** Backdrop-as-SpriteRenderer + picking-Ignore
  root are the 1.0 shape for any workbench-over-vehicle beacon.
  `(-2, -2.5)` rest-pose override is data-only; iterate freely without
  re-verdict. If Merchant / future Chopshop-Boss ever needs a
  workbench-over-vehicle variant, this is the pattern to copy.

**Follow-ups (non-blocking):**
- If a third beacon author needs a backdrop → extract
  `AuthorBeaconBackdrop` helper (rule-of-3).
- Rest-pose tuning is data-only; iterate freely.

### Biome tuning bundle

Verdict: **ACCEPT** — see full analysis at
`production/td-verdicts/2026-08-02-biome1-tuning-and-chopshop-weight.md`.
Success criteria: 5 seeded runs with no LeftFunnel edge crossing
midpoint (X > 1920), zero `MaxRetries=100` exceptions, 2-4 Chopshops
per map.

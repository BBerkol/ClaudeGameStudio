# TD Verdict — MapBeaconStyleSO Icon Alternates (2026-07-07)

## Context

User has 3 different Event beacon icon sprites and wants the run map to pick
one at random per Event beacon, deterministically across save/resume. Same
mechanism should generalise to any beacon type in any biome as a pure data
edit.

## Proposed change

**File:** `Assets/Scripts/Run/Authoring/MapBeaconStyleSO.cs`

Additive sibling table + new overload:

- New `[Serializable] public struct SpritePool { public Sprite[] Alternates; }`
- New `[SerializeField] private SpritePool[] _beaconIconAlternates = new SpritePool[8];`
- New public method:
  `Sprite BeaconIcon(BeaconType type, int variantKey)`
  — returns Knuth-hash deterministic pick from
  `{primary} ∪ _beaconIconAlternates[i].Alternates`, skipping null primary.
- Existing `Sprite BeaconIcon(BeaconType type)` kept unchanged (seedless entry
  for editor tooling / debug UI).
- `OnValidate` extended to also resize `_beaconIconAlternates` to
  `Enum.GetValues(typeof(BeaconType)).Length` with the same warning-log pattern
  used for `_beaconIconsByType`.

**Consumer:** `Assets/Scripts/UI/MapViewController.cs` — the single call site
in `RebuildBeacons` gains the `beacons[i].BeaconIndex` argument (stable
per-run ordinal on `BeaconViewModel`).

## Technical Director Review

### ADR sanity checks

**ADR-0015 (biome distribution narrowing) — CONFIRMED.** The sibling
`_beaconIconAlternates` table is indexed by `(int)BeaconType`, same shape as
`_beaconIconsByType`. No `switch` on the enum, no code path that assumes
specific types exist. This is the same narrowing exemplar, extended
additively. Biomes 2+ author variety as a pure data edit — canonical
ADR-0015.

**ADR-0011 (no bridges) — CONFIRMED, not a bridge.** Parallel storage
requires two representations of *the same fact*. Here `_beaconIconsByType[i]`
= baseline (required visual identity) and `_beaconIconAlternates[i]` =
variety pool (optional). Disjoint semantics, both live in the final game,
neither replaces the other. The overload `BeaconIcon(BeaconType)` is a valid
seedless entry — not a legacy bridge (ADR-0011 exception category: separate
API contract, not a compat shim).

**ADR-0003 (deterministic RNG) — OUT OF SCOPE, confirmed.** Knuth hash on
`BeaconIndex` is view-layer sampling of a *presentation* choice, not a
gameplay outcome. It touches no `RunState`, no combat resolution, no reward.
Save/resume determinism is achieved via the stable ordinal, not RNG state.
Do not touch this with `System.Random`.

### Three-lens self-audit

- **Health:** One call site (`MapViewController.cs`, `RebuildBeacons` inside
  the beacon loop), one signature update. No ADR-0011 drift risk.
  `SpritePool` struct with a single `Sprite[] Alternates` field is
  right-sized; do not add fields speculatively (weights, tags) — that's
  future signature churn without a consumer.
- **Optimization:** Called once per beacon at map bind, not per-frame. Knuth
  hash + one modulo + one branch is free. No delta.
- **1.0 survival:** Signature `BeaconIcon(BeaconType, int variantKey)` is the
  canonical 1.0 shape. Payload is a primitive, so future needs (e.g.,
  per-beacon variant memoization) can layer without breaking callers.

### Two amendments (folded into this same edit)

1. **`OnValidate` resizes `_beaconIconAlternates`** to
   `Enum.GetValues(typeof(BeaconType)).Length` with the same warning-log
   pattern used for `_beaconIconsByType`.
2. **Parameter name is `variantKey`, not `seed`.** `seed` implies RNG state
   (ADR-0003 territory); `variantKey` names the actual role — a stable
   ordinal keying a deterministic pool lookup. Prevents future readers from
   misreading this as an RNG seam.

### Verdict

**ACCEPT.**

## Files touched

- `Assets/Scripts/Run/Authoring/MapBeaconStyleSO.cs` — sibling alternates
  array + new `BeaconIcon(BeaconType, int variantKey)` overload +
  `OnValidate` extension.
- `Assets/Scripts/UI/MapViewController.cs` — `RebuildBeacons` call-site
  signature update passing `beacons[i].BeaconIndex` as the variant key.

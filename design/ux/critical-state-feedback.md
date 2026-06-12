# Critical State Feedback — UX Seed Entry

> **Status**: Seed (Phase 4 R5 #19 closure — to be expanded with combat-hud refresh)
> **Last Updated**: 2026-05-18
> **Owner**: ux-designer (authoring); art-director (palette/motion review); accessibility-specialist (3-channel verification)
> **Authoritative event source**: ADR-0007 §Decision 14 + §Decision 15 + §Key Interfaces (`OnCriticalStateChanged`)

---

## 1. Purpose

Specify what the player perceives when `vehicle.CriticalState` flips, on either
side of the chase. This document is the perceptual contract for the event; it
does not redefine when the event fires (ADR-0007 owns that) and does not dictate
exact pixel treatment (combat-hud refresh owns that).

This entry exists because Review 5 flagged that the CriticalState transition had
no UX contract — the event was specified mechanically (one structural slot
Offline, or `IsDead == true`) but the player-perceptible signal was undefined.

## 2. Anchored Event Contract

| Event | Signature | Fires |
|---|---|---|
| `OnCriticalStateChanged` | `Action<bool>` (new `isCritical`) | At most once per top-level `ApplyDamage` / `Repair` invocation, on the transition that actually flips `vehicle.CriticalState` (ADR-0007 Decision 15) |

**False → True trigger** (entering Critical): the per-call evaluation in
F-VP2 Step 4(h) determines `CriticalState_now != wasCriticalLocal`, where
`wasCriticalLocal` is the per-invocation Step 0 snapshot (ADR-0007 Decision 15).
Concretely: any structural slot transitioning to Offline while the vehicle was
not previously Critical fires `OnCriticalStateChanged(true)`.

**True → False trigger** (exiting Critical): the same Step 4(h) gate, but on a
Repair path that lifts the last Offline structural slot back to
Functional/Degraded (ADR-0007 Decision 12 emission ordering).

The UX layer must treat the two transitions as a paired enter/exit cue —
losing the recovery cue silently breaks the "you are no longer one hit from
death" read.

## 3. Perceptual Contract (3-Channel)

Per `design/accessibility-requirements.md` §3 and `design/ux/combat-hud.md` §10
(every decision-critical channel renders across ≥3 independent channels), the
CriticalState transition is **decision-critical** — the player must read it
without inspection. The three channels are:

| Channel | False → True (entering Critical) | True → False (exiting Critical) |
|---|---|---|
| **Visual — chassis** | Persistent vignette/edge glow around the affected vehicle's chassis silhouette while `CriticalState == true`. Color is an accent only; shape/motion is the primary cue (interaction-patterns.md §3.2 color-independence rule). | Vignette fades over a fixed window; chassis returns to baseline silhouette treatment. |
| **Visual — HP bar** | `CurrentHullHp` bar gains a persistent "at-risk" treatment (per-slot Offline badges already exist per combat-hud.md table row 15; this layer composites a chassis-level danger frame around the HP block). | Treatment removes synchronously with the chassis vignette. |
| **Audio** | One-shot stinger on the False → True frame; ambient layer (low rumble or filter sweep — sound-designer authoritative spec lives in audio implementation when it exists) holds while Critical. | One-shot recovery stinger on True → False; ambient layer cross-fades out. |

**Floor commitments** (must be met by any candidate visual / audio treatment):

- Color is never the sole channel. (`design/accessibility-requirements.md` §3.)
- Reduce Motion compliant: vignette pulsing is allowed but must be replaceable
  with a static treatment when Reduce Motion is on. The static treatment must
  still be unmistakable from the non-Critical baseline.
- No timing-critical read. The CriticalState read holds as long as the state
  is true; the player may pause inspection indefinitely.
- Screen reader hook: a string is emitted on each transition. Format:
  `"{VehicleLabel} entered critical condition"` / `"{VehicleLabel} recovered from critical condition"`. The string is a localization key; English text is illustrative.

## 4. Side Distinction (Player vs Enemy)

The HUD distinguishes player-CriticalState from enemy-CriticalState by side
(player on left rail, enemy on right rail per combat-hud.md zone layout).
Both sides use the same channel set but the player-side treatment is the
stronger one — a player one hit from death has more reason to read it than
an enemy near a guaranteed kill.

Specifically:
- **Player side**: vignette opacity higher; ambient audio layer is present.
- **Enemy side**: vignette opacity lower; ambient layer is absent (one-shot
  stingers still fire). Rationale: the enemy CriticalState is informational
  ("press the advantage"), not a survival warning, so it does not need the
  persistent ambient channel.

Both sides honor the screen-reader hook regardless of side asymmetry.

## 5. Composition with Other Cues

The Critical state composites *over* other per-slot cues, not under them:

| Lower layer | This layer | Conflict resolution |
|---|---|---|
| Per-slot Offline badge (combat-hud.md table row 15) | Chassis vignette | Composite — vignette frames the silhouette; per-slot badges remain readable inside the vignette. |
| `OnArmorExposed` impact cue (`armor-exposure.md`) | Chassis vignette transition | Armor exposure may co-occur with the False → True flip (the same frame an armor break overflows to Hull crossing structural). Both cues fire; the chassis vignette settles after the armor-exposure one-shot. |
| Player position cue (Ahead/Behind) | Chassis vignette | Independent — no conflict. |

When `OnVehicleDied` fires the CriticalState ambient layer must stop (death
overrides the at-risk channel). The death sequence is owned by combat-hud.md
§Player Death Sequence; this doc only commits to "stop the ambient" at the
death frame.

## 6. Failure Modes (Watch For)

- **Missed entry cue**: if the chassis vignette fades in instead of snapping,
  the player can read the at-risk state late. Default: snap-on, fade-out.
- **Recovery cue not paired**: if exit is silent, the player keeps reading
  Critical for several seconds after repair. Both transitions must have a
  parity-matched signal.
- **CriticalState false-positive at run start**: the event must NOT fire on
  combat enter even when the vehicle starts with a structural slot Offline
  (this is a pre-existing state, not a transition). Decision 15 snapshot
  discipline already prevents this at the model layer; UX must subscribe
  *after* the initial combat-setup snapshot so it does not animate on first
  render. Owner: combat HUD wire-up.

## 7. Open Questions

- **OQ-CSF1**: Should the player-side ambient audio layer duck card-play SFX
  while active? Defer to audio-director spec when it lands.
- **OQ-CSF2**: When both vehicles are simultaneously Critical (mutual one-hit
  state), does the player-side cue suppress the enemy-side stinger to avoid
  channel collision? Decide during combat-hud refresh.
- **OQ-CSF3**: Does the recovery stinger fire if the player Repairs while
  *not* Critical (i.e., from Degraded to Functional, no CriticalState flip)?
  Answer per the event contract: **no**, the recovery stinger is bound to
  `OnCriticalStateChanged(false)`, not to repair generally. Documented for
  clarity.

## 8. References

- ADR-0007 §Decision 14, §Decision 15 — event firing contract.
- ADR-0007 §Decision 12 — repair-path event emission (the True → False trigger).
- `design/gdd/vehicle-and-part-system.md` F-VP2 Canonical Event Order Table —
  authoritative event sequencing.
- `design/ux/interaction-patterns.md` §3.5 (Focus & Navigation),
  §3.2 (Status Effect Chip — color-independence pattern).
- `design/accessibility-requirements.md` §3 (visual), §4 (audio).
- `design/ux/combat-hud.md` §10 (3-channel redundancy floor).

## 9. Change Log

| Date | Change | Reason |
|---|---|---|
| 2026-05-18 | Initial seed entry. | Phase 4 R5 #19 closure — CriticalState UX contract anchored to ADR-0007 Decision 14/15. |

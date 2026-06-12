# Armor Exposure Visual Vocabulary — UX Seed Entry

> **Status**: Seed (Phase 4 R5 #20 closure — to be expanded with combat-hud refresh)
> **Last Updated**: 2026-05-18
> **Owner**: ux-designer (authoring); art-director (palette + motion review); sound-designer (paired SFX — see `design/audio/amplified-redirect-sfx.md`)
> **Authoritative event source**: ADR-0007 §Decision 14 + §Key Interfaces (`OnArmorExposed`)

---

## 1. Purpose

Specify what the player perceives at the frame where an Armor slot transitions
to exposed (the "armor just broke and the underlying slot is now redirect-fed
at `× ExposureMultiplier`" moment). This is the per-slot perceptual layer; the
chassis-level Critical layer is in `critical-state-feedback.md`. The audio
counterpart is `design/audio/amplified-redirect-sfx.md`.

This entry exists because Review 5 flagged that `OnArmorExposed` had no UX
contract — the event was specified mechanically (Decision 14 step 4(e)) but
the visual signal at the breakthrough moment was undefined.

## 2. Anchored Event Contract

| Event | Signature | Fires |
|---|---|---|
| `OnArmorExposed` | `Action<string armorSlotId, string redirectsToSlotId>` | Once on the transition where an Armor slot goes from `Hp > 0` to `Hp == 0` — the breakthrough frame (ADR-0007 Decision 14 step 4(e); F-VP2 Step 4(e) row). |

**Does NOT fire** on:
- A hit that lands on an already-exposed armor (Hp was already 0). The
  redirect-to-Hull damage still resolves and the amplified-redirect SFX
  layer (audio doc) still plays, but the *visual transition* cue is a
  one-shot on the break frame only.
- A hit that absorbs but does not break (`Hp > 0` after absorption). The
  armor's own Damage State transition cues (Functional → Degraded) fire
  via `OnSlotDamageStateChanged` per Decision 14 — those are a separate
  per-slot channel, NOT the exposure cue.
- A Repair that lifts the armor back from Hp 0 (no inverse cue; the
  un-exposure transition is folded into the standard slot-state recovery
  channel via `OnSlotDamageStateChanged`).

The visual vocabulary here is therefore strictly the "armor broke this
frame" cue, not the steady-state "armor is exposed" rendering. The
steady-state rendering is owned by V&P GDD §R_ARM.5 Visual Contract
(`DamageState == Offline` → exposed wound sprite).

## 3. Perceptual Contract (3-Channel)

Per `design/accessibility-requirements.md` §3 (visual) + the 3-channel
redundancy floor in `design/ux/combat-hud.md` §10, the breakthrough moment
is **decision-critical** — the player must read it to understand that
subsequent hits on this slot now flow into the redirect target at the
amplifier multiplier (per V&P §R_ARM.2).

| Channel | Cue |
|---|---|
| **Visual — slot** | One-shot break burst at the armor slot's anchor point on the vehicle silhouette: shape-driven (cracks expanding, plate spalling). Color is an accent only. After the burst settles, the slot's steady-state sprite is the V&P §R_ARM.5 exposed wound sprite. |
| **Visual — link** | Brief directional connector cue drawn from the armor slot's anchor to the `redirectsToSlotId` slot's anchor — communicates "future hits here now feed there." Connector is shown once on the break frame and fades. |
| **Audio** | Paired SFX in `design/audio/amplified-redirect-sfx.md` — break stinger fires synchronously with the visual burst. Audio is additive; the visual cue is sufficient alone for D/deaf players (per `accessibility-requirements.md` §4 "no audio-only channel carries decision-critical information"). |

**Floor commitments**:

- **No color-alone signaling.** Shape (crack/spall motion) is primary; color
  is an accent (interaction-patterns.md §3.2 rule).
- **Reduce Motion compliant.** The crack-and-spall animation must have a
  static equivalent (a single frame replacement to the exposed wound sprite
  plus a brief flash) when Reduce Motion is on. The connector line is
  drawn but does not animate under Reduce Motion — it appears, holds for
  a fixed window, then disappears.
- **No timing-critical read.** The exposed wound sprite is the steady
  state and the player may read it indefinitely after the burst settles.
- **Screen reader hook**: emits `"{VehicleLabel} {ArmorSlotLabel} exposed; damage now redirects to {RedirectsToSlotLabel} amplified"` on the event frame. (Localization key; English illustrative.)

## 4. Per-Slot Treatment (Both Armor Slots, Both Vehicles)

The vocabulary applies uniformly to all Armor slots. Concretely for the EA
roster (V&P §R_ARM.1 + chassis SOs):

- **Player Scout chassis** — has no Armor slots; this doc is informational only on the player side until a chassis with Armor unlocks.
- **Player chassis with Armor (post-EA)** — apply the cue at the chassis-relative anchor for each Armor slot the chassis defines.
- **Enemy chassis with Armor (Dredge, future)** — same treatment, mirrored to the enemy side of the chase rail. Dredge has `armor_chest` and `armor_back` (V&P §R_FL chassis SO author note); each fires independently.

The cue does NOT scale with `ExposureMultiplier`. A higher multiplier does
not produce a louder/brighter break — the multiplier is communicated by
the persistent `× N` badge on the exposed slot (combat-hud refresh ticket
owns the badge surface). Rationale: amplified-redirect intensity is a
*continuous* read; the exposure event is a *discrete* transition. The
two should not be conflated into one channel.

## 5. Composition with Other Cues

The exposure cue may co-occur with:

| Co-occurring cue | Resolution |
|---|---|
| Critical state flip (`OnCriticalStateChanged(true)`) on the same frame — common when the recursive Hull recursion crosses the structural threshold (ADR-0007 Decision 15 worked example) | Both cues fire. Exposure burst plays at the armor anchor; CriticalState vignette settles around the chassis. Layer order: burst on top, vignette under. |
| `OnVehicleDied` on the same frame — possible if the recursive Hull hit also kills | Exposure burst still plays (it is the breakthrough cue, not survival-conditional); death sequence supersedes the chassis treatment immediately after. |
| Status effect application on the same frame (e.g., a Corroded application that broke the armor) | The status chip update is its own channel (combat-hud.md table row 32/33). Both fire independently. |

## 6. Failure Modes (Watch For)

- **Slot anchor drift**: if the chassis prefab moves slot anchor points,
  the burst can play at a stale screen position. Anchor lookup must be
  per-frame, not cached at event time.
- **Connector pointing offscreen**: if `redirectsToSlotId` has an anchor
  outside the visible chassis silhouette (shouldn't happen for the EA
  roster, but guard for future chassis), the connector clips to the
  silhouette edge with an outward-arrow tip rather than disappearing.
- **Already-exposed regression**: re-firing the exposure cue on every hit
  to an already-exposed slot would be an event-contract violation (see §2
  "Does NOT fire on already-exposed"). Regression coverage is owned by
  V&P AC-VP58 + AC-VP59b.

## 7. Open Questions

- **OQ-AE1**: Should the connector cue persist for the steady-state exposed
  read (faint line always visible while exposed), or only flash on the
  break frame? Default for now: flash on break only. Decide during
  combat-hud refresh.
- **OQ-AE2**: Per-slot break shake — does the chassis silhouette get a
  brief positional shake on break? Strong "feel" cue, but may collide
  with Reduce Motion. Defer to art-director motion pass.
- **OQ-AE3**: When both Dredge armor slots break on the same combat-turn
  resolution (rare but possible with multi-hit cards), do both bursts
  play simultaneously, or sequenced? Default: simultaneous (independent
  events). Re-evaluate after first multi-armor playtest.

## 8. References

- ADR-0007 §Decision 14 (Armor INTACT branch + breakthrough emission) — event firing contract.
- ADR-0007 §Decision 15 (recursive snapshot + idempotency) — guarantees `OnArmorExposed` fires before the recursive Hull-redirect call's events, on the same top-level `ApplyDamage` invocation.
- `design/gdd/vehicle-and-part-system.md` §R_ARM.2 (damage behavior), §R_ARM.5 (visual contract — steady-state armor sprites).
- `design/audio/amplified-redirect-sfx.md` — paired audio cue (this doc commits to a synchronized firing frame).
- `design/ux/interaction-patterns.md` §3.2 (color-independence), §3.5 (Focus & Navigation).
- `design/accessibility-requirements.md` §3 (visual), §4 (audio).
- `design/ux/combat-hud.md` §10 (3-channel redundancy floor).
- `design/ux/critical-state-feedback.md` — chassis-level Critical layer (composites with this slot-level cue).

## 9. Change Log

| Date | Change | Reason |
|---|---|---|
| 2026-05-18 | Initial seed entry. | Phase 4 R5 #20 closure — `OnArmorExposed` UX contract anchored to ADR-0007 Decision 14. |

# Amplified-Redirect SFX Layer — Audio Seed Entry

> **Status**: Seed (Phase 4 R5 #21 closure — to be expanded with audio implementation spec when it exists)
> **Last Updated**: 2026-05-18
> **Owner**: sound-designer (authoring); audio-director (mix balance); accessibility-specialist (audio-floor verification)
> **Authoritative event source**: ADR-0007 §Decision 14 + V&P §R_ARM.2 (`SafeAmplify` + recursive redirect `ApplyDamage` call)

This is the first entry in `design/audio/`. The directory is intentionally seeded
in this Phase rather than at audio-system bring-up because the redirect-amplify
moment is the first SFX surface whose contract is locked at the model layer (ADR-0007
Decision 14). Future audio specs (combat ambient bed, card-play SFX, status SFX)
will join this directory when their owning systems land.

---

## 1. Purpose

Specify the SFX layer that distinguishes the **amplified-redirect impact** —
damage that flows from an Armor slot to its `RedirectsTo` target at
`× ExposureMultiplier` — from a normal slot hit. The player must be able to
hear that a hit was *amplified*, not just landed.

This is decision-critical context: the player who reads "I hit them for 18"
when they expected to hit for 6 needs to know the damage went through an
exposed armor and got multiplied — otherwise the math feels random.

This entry does not specify the **break-frame** stinger that pairs with
`OnArmorExposed`; that one-shot is the audio sibling of
`design/ux/armor-exposure.md` and is referenced from §4 of this doc.

## 2. Anchored Trigger Contract

The amplified-redirect SFX layer fires every time the recursive `ApplyDamage`
call inside the Armor branch resolves a non-zero redirected_amount on the
`RedirectsTo` target. Concretely, per V&P §R_ARM.2 pseudocode:

| Path | Condition | SFX layer fires? |
|---|---|---|
| Armor INTACT, no overflow | `armor_consumed = amount; overflow = 0` | **No** — armor absorbed cleanly, no redirect happened. |
| Armor INTACT, breakthrough with overflow | `slot.Hp → 0; overflow > 0; redirected = SafeAmplify(overflow, mult)` | **Yes** — the inner recursive `ApplyDamage(redirectsTo, redirected, source)` runs. SFX layer plays. |
| Armor exposed (Hp already 0) | `redirected = SafeAmplify(amount, mult); ApplyDamage(redirectsTo, redirected, source)` | **Yes** — full redirect. SFX layer plays. |
| `redirected_amount == 0` (degenerate) | `SafeAmplify` returned 0 (e.g., `amount=0`) | **No** — no impact to sonify. |
| `SafeAmplify` fallback path (NaN/Inf/≤0 multiplier) | Falls back to identity (delivers `amount` unamplified — V&P §R_ARM.2 + AC-VP50b) | **No** — by design, the fallback is unamplified, so this is a normal slot hit, not an amplified impact. The SFX layer must NOT fire in the fallback path, to keep the audio contract honest. |

**Anchor frame**: SFX fires on the recursive `ApplyDamage` inner call's impact
resolution — same frame as the inner call's Step 3 (`OnSlotHpChanged` on the
redirect target). This places the amplified-redirect cue *after* any
`OnArmorExposed` break-frame stinger (§4 composition) and synchronously with
the redirect-target HP tween.

## 3. Sonic Contract

This is a seed-level direction, not the final mix spec. Sound-designer will
expand into a full sample / synthesis spec when the audio system lands.

| Aspect | Direction |
|---|---|
| **Identifiability** | The redirect-amplify layer must be **distinguishable from a normal slot hit within ~200ms** of impact. The player should not have to think "wait, was that amplified?" |
| **Layering** | This is an additive layer, not a replacement. The underlying slot-hit SFX (the redirect target's normal hit sound) still plays. The amplified-redirect layer composites *over* it. |
| **Intensity scaling** | The layer's intensity scales with the `ExposureMultiplier` value (higher mult → more aggressive layer). Specific scaling curve (linear vs perceptual log) is deferred to mix authoring. Floor: a 1.0× multiplier (theoretical edge case — not in EA roster) produces no layer; a 3.0× multiplier (Dredge default) produces the canonical layer. |
| **Damage-magnitude vs multiplier** | The layer is keyed to the **multiplier**, not the absolute damage delivered. A 5-damage hit amplified to 15 (3.0×) and a 50-damage hit amplified to 150 (3.0×) get the same layer character — both are "3× amplified." The damage *magnitude* is communicated by the slot-hit base layer; the *amplification* is communicated by this redirect layer. |
| **Spatialization** | Layer plays at the **redirect target** screen position (the slot taking the damage), not the armor source position. Rationale: the impact is on the redirect target; the armor is the *cause*, not the receiver. The break-frame stinger (§4) plays at the armor source — the two locations together communicate the redirect direction. |

## 4. Composition with Other Audio Cues

The full audio sequence for an Armor breakthrough hit is layered:

1. **Frame N — armor break**: `OnArmorExposed` break stinger (paired with
   `design/ux/armor-exposure.md` visual burst). Plays at armor source position.
2. **Frame N (same frame) — recursive impact**: Slot-hit base layer (the
   redirect target's normal hit sound) + the amplified-redirect layer
   (this doc). Plays at redirect-target position.
3. **Frame N (same frame) — CriticalState transition (conditional)**: If the
   recursive Hull hit crossed CriticalState (ADR-0007 Decision 15 worked
   example), the CriticalState entry stinger plays. Plays as a chassis-level
   non-spatialized layer (`design/ux/critical-state-feedback.md` §3).

Mix priority (loudest → quietest), when all three co-occur:
1. CriticalState entry stinger (highest — survival-critical read).
2. Amplified-redirect layer (medium — math-critical read).
3. Break stinger (lower — visual is already strong on the break frame).
4. Slot-hit base layer (lowest — substrate).

For a hit on **already-exposed armor** (no break this frame), only items 2
and (conditional) 3 fire. The break stinger does not re-fire.

## 5. Accessibility Floor

Per `design/accessibility-requirements.md` §4:

- **No audio-only decision-critical read.** The amplified-redirect layer
  communicates a decision-critical fact (damage was amplified), but the same
  fact is conveyed visually by the damage flyout magnitude (when authored)
  and by the persistent `× N` exposure-multiplier badge on the exposed slot
  (combat-hud refresh ticket owns the badge). A player with audio muted
  reads the multiplied damage from the flyout + badge alone.
- **No timing-critical audio**. The cue is one-shot; the player does not
  need to "catch" it to play correctly.
- **Subtitles / captions**: a closed-caption track entry for the
  amplified-redirect layer is required (text like
  `[amplified redirect — × N damage]` localized). Caption fires on the
  same frame as the SFX. This is the explicit redundancy that closes the
  audio-floor commitment.
- **Volume mix**: the layer is on the SFX bus (not Music, not Voice), and
  honors the SFX volume slider. No standalone bus for this layer.

## 6. Failure Modes (Watch For)

- **Layer fires on `SafeAmplify` fallback**: would be a contract violation
  — fallback is unamplified, so the layer must NOT fire (§2 row 5). The
  hook must be placed inside the redirect branch after the amplification
  guard succeeds, not before.
- **Layer fires twice on simultaneous breakthroughs**: A multi-hit card
  hitting two Armor slots in the same resolution would fire the layer
  twice. This is correct (two amplified impacts = two cues), but the mix
  must duck the second layer slightly so they don't sum into clipping.
  Defer to mix authoring.
- **Spatialization drift**: redirect target moves mid-frame (positional
  cards play before damage resolution). Anchor must be sampled at impact
  frame, not pre-card-play.

## 7. Open Questions

- **OQ-AR1**: Should the amplified-redirect layer have an enemy-vs-player
  asymmetry (different timbre when the player is *receiving* an amplified
  hit vs *dealing* one)? Player feedback favors clarity over realism here;
  asymmetric direction is the leading candidate. Decide pre-1.0 with
  audio-director.
- **OQ-AR2**: Does the layer pitch-shift with `ExposureMultiplier` value
  (e.g., higher mult → higher pitch), or is intensity the only varying
  dimension? Author both, A/B in mix sessions.
- **OQ-AR3**: For the (currently theoretical) 1.0× edge case — the layer
  is suppressed (§3 "no layer at 1.0×") — but should the break stinger
  still fire? Yes (per §4 composition; break is a separate trigger), but
  document for the implementation pass.

## 8. References

- ADR-0007 §Decision 14 (Armor INTACT branch + breakthrough emission).
- `design/gdd/vehicle-and-part-system.md` §R_ARM.2 (`SafeAmplify` formula
  + recursive redirect), §AC-VP50b (NaN/Inf fallback contract).
- `design/ux/armor-exposure.md` — paired visual cue (synchronized firing).
- `design/ux/critical-state-feedback.md` — chassis-level CriticalState
  audio layer (composites with this slot-level cue).
- `design/accessibility-requirements.md` §4 (audio floor: no audio-only
  reads, subtitles, volume mix).

## 9. Change Log

| Date | Change | Reason |
|---|---|---|
| 2026-05-18 | Initial seed entry. First entry in `design/audio/`. | Phase 4 R5 #21 closure — amplified-redirect SFX layer anchored to ADR-0007 Decision 14 + V&P §R_ARM.2. |

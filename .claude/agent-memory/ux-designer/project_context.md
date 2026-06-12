---
name: Project Context
description: Wasteland Run — 2D vehicular card roguelike, PC/Steam, KB+M primary, partial gamepad, Slay the Spire audience
type: project
---

**Wasteland Run** is a 2D vehicular card roguelike built in Unity 6.3 LTS, targeting PC/Steam (EA ~Month 10-12 from 2026-04-18 start).

Key UX constraints:
- Keyboard/Mouse primary input. Gamepad is additive — no feature may be gated on gamepad.
- No touch support.
- Target audience: mid-core to hardcore roguelike players (Slay the Spire/FTL familiarity assumed). Keyboard shortcuts, deck inspection during combat, and precise card targeting are expected genre norms.
- Two chassis at EA: Scout (mobility/precision) and Assault (aggression/ramming).
- Combat layout: chase rail, both vehicles face right, left=back/right=front.
- Map layout: left=run start, right=Haven; diamond/hex node grid, rightward movement only.

**Why:** Solo dev with 10-12 month EA timeline. UX must be spec'd carefully before UI implementation begins — no iteration budget for fundamental re-designs post-build.

**How to apply:** Frame UX recommendations against the Slay the Spire baseline. When players expect a convention (deck inspection during combat, keyboard shortcuts, skip as a valid choice), deviating from it requires explicit justification in the GDD.

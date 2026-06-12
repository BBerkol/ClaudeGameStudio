---
name: Project Audio Context
description: Wasteland Run sonic identity, first audio doc location, and audio system status
type: project
---

Game: Wasteland Run — PC card roguelike (Unity 6.3 LTS, URP 2D, Steam).

Sonic identity: "RUST ICON" — found-object weathered aesthetic, FTL-style abstraction. NOT Pacific Drive simulation realism. Sparse, industrial, post-apocalyptic palette.

First audio doc seeded: `design/audio/amplified-redirect-sfx.md` (2026-05-18). Anchored to ADR-0007 Decision 14 + V&P §R_ARM.2. This is the only authored audio spec in the project as of 2026-05-20. All other audio specs (combat ambient bed, card-play SFX, status SFX) are deferred until owning systems land.

Audio system status: No audio middleware chosen yet. No bus hierarchy, no LUFS targets, no naming convention file authored. The audio directory exists but is effectively empty.

**Why:** Audio is a late-stage concern for this team; design systems are still being locked.
**How to apply:** Do not assume any audio infrastructure exists. Every spec written must be self-contained and not reference engine-level audio tooling until middleware is chosen.

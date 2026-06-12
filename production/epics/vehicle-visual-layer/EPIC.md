# Epic: Vehicle Visual Layer

> **Layer**: Foundation
> **GDD**: design/gdd/vehicle-and-part-architecture.md (visual sections) + design/gdd/vehicle-and-part-mechanics.md (critical/audio feedback sections)
> **Architecture Module**: `WastelandRun.CombatView` — URP Sprite Lit Shader Graph materials, MaterialPropertyBlock + `[PerRendererData]`, damage overlay system, Addressables chassis/part art bundle loading
> **Status**: Ready (⚠️ ADR-0008 Proposed — see below)
> **Stories**: Not yet created — run `/create-stories vehicle-visual-layer`

## Overview

Implements the `WastelandRun.CombatView` vehicle rendering layer — the visual expression of "Vehicle as Character." The chassis body and four part slots render via URP Sprite Lit Shader Graph materials using `MaterialPropertyBlock` with `[PerRendererData]`-declared properties for per-renderer state (damage tint, grime/wear overlay, critical pulse), ensuring zero material instance proliferation under URP's SRP Batcher. Damage state transitions (Functional → Degraded → Offline) drive shader property changes through the `IVehicleView` subscription loop, never by polling. Chassis and part art are loaded at runtime via Addressables using the `chassis-[name]` and `part-[id]` label scheme (ADR-0008), with a `ChassisArtBundle` asset reference on `ChassisDefinitionSO` per ADR-0007 §3.3. The critical-state audio loop (`CriticalLoopExitCrossfadeMs` configurable) and the part-offline visual suppression behavior (visual always fires; audio channel only for Light-tier suppression) are implemented here as `IVehicleEventBus` subscribers. This epic delivers the emotional feedback layer — the shader-driven wear, the part-death cascade, the visual read that makes a damaged vehicle feel like a wounded character.

> ⚠️ **ADR-0008 is Proposed** (pending technical-director sign-off). Stories that depend solely on ADR-0001 (shader, MaterialPropertyBlock, damage states) can proceed immediately. Stories requiring the Addressables art bundle loading pipeline should be marked **Blocked — awaiting ADR-0008 acceptance**. Track against `production/risk-register/`.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0001: Visual Vehicle Part System | URP Sprite Lit Shader Graph for parts; `MaterialPropertyBlock` + `[PerRendererData]`; damage state overlay via shader; Addressables for chassis/part art assets; no material instance proliferation | HIGH |
| ADR-0008: Addressables Runtime Asset Loading | `AssetReferenceT<ChassisArtBundle>` on `ChassisDefinitionSO`; `chassis-[name]` / `part-[id]` label scheme; memory budget rules; lazy-load on encounter start; release on scene unload | HIGH |

## GDD Requirements

No registry TR-IDs are assigned to the visual layer — all 25 TR-vehicle entries cover the gameplay data model (Epic 2a). This epic's acceptance criteria derive from:

- **ADR-0001 Validation Criteria**: `[PerRendererData]` property isolation verified (no cross-renderer bleed); damage overlay shader renders correct state per DamageState enum; no material instance creation under Play Mode profiler
- **ADR-0007 §W3g**: `CriticalLoopExitCrossfadeMs` tuning knob implemented and configurable; active-Critical audio channel count ownership specified in part-state model
- **GDD vehicle-and-part-mechanics.md**: Critical state visual pulse; Degraded visual tell; Offline visual suppression (visual always fires, audio Light-tier only); multi-stat Offline preview "(Destroyed)" block label; `ForcedPassBannerDurationSecs` (1.5s, inputs discarded, Esc exempt)
- **ADR-0008 Validation Criteria**: Chassis art bundle loads within frame budget; zero leaks on scene reload; fallback on missing key does not crash

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- Visual/Feel stories have screenshot evidence + lead sign-off in `production/qa/evidence/`
- ADR-0001 Validation Criterion 2: Prototype confirms `[PerRendererData]` shader property isolation (no cross-renderer bleed on two simultaneous vehicles)
- URP Render Graph API used for any custom render passes — `ScriptableRenderPass.Execute` pattern is absent from this codebase
- `MaterialPropertyBlock` path verified under Unity Profiler: zero `new Material()` calls during DamageState transitions
- Addressables chassis+part bundle loads in Play Mode without errors; memory release on scene unload verified
- Critical-state audio loop crossfade fires on Frame-slot critical entry and exit; `CriticalLoopExitCrossfadeMs` is tunable without code change
- Part-offline visual suppression and audio-channel-only Light-tier suppression verified against GDD spec
- `ForcedPassBannerDurationSecs` (1.5s) renders correctly; inputs discarded during banner; Esc exempt

> ⚠️ Stories covering Addressables art bundle loading remain **Blocked** until ADR-0008 status transitions to Accepted. All other stories in this epic are unblocked.

## Next Step

Run `/create-stories vehicle-visual-layer` to break this epic into implementable stories.

# ADR-0008: Unity Addressables for Runtime Asset Loading

## Status

**Accepted (2026-05-25) — NOT INSTALLED. Deferred-future; do not plan against
it.** Re-labelled 2026-09-09.

> **Nothing in this ADR has been adopted.** Verified 2026-09-09:
>
> - The Addressables **package is not installed** — zero hits for
>   `addressables` in `Packages/manifest.json`.
> - **Zero runtime usage** — no file under `Assets/Scripts` references
>   `UnityEngine.AddressableAssets`.
> - `scriptingBackend` is unset in `ProjectSettings.asset`, i.e. still **Mono**,
>   so the **IL2CPP smoke test this ADR makes a shipping precondition has never
>   been run**.
>
> The project loads assets through `Assets/Resources/` instead. That is a real
> divergence from this ADR, not an oversight in the loading code.
>
> The technical decision is still considered sound and the ADR is kept for when
> asset volume justifies it — but "Accepted" was being read as "this is how the
> project loads assets," which is false. Any story or estimate that assumes
> Addressables is available must first budget the package install, the group
> setup, and the IL2CPP smoke test.
>
> Consequence for ADR-0007 §3.3: `AssetReferenceT<ChassisArtBundle>` is **not**
> unblocked in practice. The sentence below claiming otherwise was true of the
> decision and false of the tree.
>
> Re-labelled during the 2026-09-08 clean-slate audit
> (`production/remediation-plan-2026-09-08.md` §0.3).

Original acceptance note (2026-05-25): technical-director sign-off granted;
memory budget table and build-pipeline integration plan added as part of the
Accepted transition. All three acceptance conditions stated in the original
Proposed draft were met at that time.

## Date

2026-05-19

## Decision Makers

- User (creative/design lead) — locked B1 (approve) during V&P
  Architecture R1 close 2026-05-19
- creative-director — recommended B1 in R1 senior synthesis with caveat
  that technical-director sign-off is the proper ADR-class paper trail
- technical-director — pending sign-off for Accepted transition
- unity-specialist — flagged the dependency as unapproved during R1
  adversarial review (BLOCKER); endorses Addressables for the chassis
  art use case once approved

## Context

### Problem Statement

ADR-0007 (Frame-Driven Variable Slot System) §3.3 declares
`AssetReferenceT<ChassisArtBundle>` on the `ChassisDefinitionSO`
authoring asset to lazy-load chassis art bundles at vehicle construction
time. Addressables was not in the project's Allowed Libraries list at
the time §3.3 was authored. V&P Architecture R1 adversarial review
flagged this as a hard blocker: a contract doc cannot bind an unapproved
dependency. The decision space:

- **B1**: Approve Addressables. Add to Allowed Libraries. Keep
  lazy-load contract on `ChassisDefinitionSO`.
- **B2**: Replace `AssetReferenceT<T>` with direct ScriptableObject
  reference or resources path string. Surrender lazy-load.

User locked **B1** during R1 close on creative-director recommendation.

### Forces

- **For B1 (approve)**: Lazy-load is the right pattern for chassis art —
  the player only loads the art for the chassis they pick at run start
  and any chassis they encounter as enemies in the current biome.
  Eager-loading all chassis art means a worst-case ~30-40MB texture
  budget at scene start vs. ~8-12MB with lazy-load. The 2GB memory
  ceiling per `technical-preferences.md` is generous, but UI/VFX/audio
  expansion across EA will compete for that headroom.
- **For B1 (approve)**: `unity-addressables-specialist` is already
  documented in `technical-preferences.md` Engine Specialists routing
  table as the agent to invoke "when asset management systems are
  built." The team has implicitly signposted Addressables as expected;
  this ADR makes it explicit.
- **Against B1**: Addressables adds a non-trivial build pipeline
  surface — content catalogs, asset groups, build profiles, remote
  content delivery configuration. Even the local-only use case carries
  a learning curve and CI overhead.
- **For B2 (replace)**: Direct references are simpler, fail-fast at
  load time (no async catalog lookup), and remove a build pipeline
  dependency. For a 2-3 chassis EA scope, eager-loading 30-40MB of art
  is well within budget.
- **Against B2**: Surrenders the architectural option of lazy-loading
  in EA → 1.0 → post-launch expansion. If chassis count grows to 5-8
  with biome-themed variants, eager-load grows linearly. Late-stage
  migration to Addressables is more expensive than starting there.

## Decision

**Approve Unity Addressables for runtime asset reference and lazy-load
within the following scope:**

1. **Chassis art bundle loading** per ADR-0007 §3.3 — primary use case.
2. **Vehicle-part art pipeline** per ADR-0001 — forward use case
   (visual part overlays, damage state textures). Specific contract to
   be locked when ADR-0001 implementation work begins.
3. **No remote content delivery in EA** — all addressable groups build
   into the local game install. Remote CDN is a post-launch decision
   requiring a separate ADR.
4. **Memory budget rule (pending)** — total Addressables-managed
   texture memory shall not exceed 40% of the 2GB ceiling at any frame.
   Specific budget allocation per asset category to be authored when
   the part art pipeline lands.

### Memory Budget (EA)

| Category | Addressables Group | EA Cap | Notes |
|---|---|---|---|
| Chassis art | `chassis-[name]` | 12 MB | 1 player + 1 enemy chassis loaded per combat session; unloaded at combat teardown |
| Combat VFX / overlays | `vfx-combat` | 1 MB | 2 shared overlay textures; combat-scoped lifetime per ADR-0001 |
| Part art (post-EA) | TBD | 28 MB reserved | ADR-0001 forward use; not built in EA; headroom reserved |

Total managed at EA scope: ~41 MB = ~2% of 2 GB ceiling. Within the 40% soft cap.

**Enforcement**: Verified at each milestone build via Unity Memory Profiler snapshot (`Window → Analysis → Memory Profiler`). On breach: escalate to technical-director; raise cap only via amended ADR.

### Build Pipeline (EA)

- **Groups**: `chassis-scout`, `chassis-assault`, `vfx-combat` — local build, no remote CDN
- **Build mode**: Packed Assets (local) — no content catalog hosted remotely
- **Catalog rebuild**: automatic on Unity build; no manual CI hook required at EA scope (revisit when catalog grows non-trivial)
- **Owner**: `unity-addressables-specialist` owns group authoring, catalog config, and validates Unity 6.3 Addressables API before first implementation story
- **Remote CDN**: deferred to post-launch; requires a separate ADR

### Runtime Discipline & Verification

**Async API rule**: Use `LoadAssetsAsync` + `await` on a load-screen barrier only. **Never** call `WaitForCompletion` on the gameplay thread — it blocks the render thread and will manifest as hitches on the combat-start transition. All Addressables load calls are `async/await` only.

**Unload contract**: Every `Addressables.LoadAssetAsync` handle must be released via `Addressables.Release(handle)` at the appropriate lifecycle boundary:
- Chassis art handles: released at combat teardown by the combat-session orchestrator (`CombatSceneController` or equivalent)
- Overlay texture handles: released at combat teardown by `CombatSceneController` (held for full combat duration per ADR-0001)
- `handle.IsValid()` check in `finally` block before release (per ADR-0001 load pattern and Unity 6.2+ behavior)

Leaking a handle per combat will compound across 15-30 combats in a single roguelike session. This is a known Addressables foot-gun. Ownership must be explicit at implementation time — no implicit disposal.

**IL2CPP smoke test** (Verification Required #4): Before any Addressables-loaded code ships in a build candidate, `unity-addressables-specialist` must run a standalone IL2CPP build that loads one `ChassisArtBundle` via `AssetReferenceT<T>` and logs success. This validates that ADR-0005 `link.xml` preservation covers the new Addressables surface and that IL2CPP stripping has not silently dropped the loaded types. Failure mode: bundle loads in Editor but returns null in IL2CPP — not caught by EditMode tests.

V&P Architecture R2 may reference this ADR as Proposed without blocking
APPROVED, since the dependency contract surface (the
`AssetReferenceT<T>` field shape) is stable across Proposed/Accepted.
Code that actually loads from the catalog cannot ship until this ADR
reaches Accepted.

## Consequences

### Positive

- ADR-0007 §3.3 unblocks immediately; V&P Architecture R2 can proceed.
- Lazy-load preserved as the chassis art pattern through EA → 1.0.
- `unity-addressables-specialist` routing is now real (was speculative).

### Negative

- Adds Addressables build pipeline surface to CI. Local builds may take
  longer once the catalog is non-trivial.
- IL2CPP `link.xml` discipline expands — Addressables references must
  be preserved per ADR-0005 patterns.
- Cold-start latency for chassis selection screen may add 50-200ms vs.
  eager-loaded baseline. To be measured at first benchmark.

### Neutral

- `unity-addressables-specialist` becomes a recurring routing target;
  expect 1-2 reviews per asset-pipeline ADR going forward.

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0005 (assembly split + IL2CPP `link.xml` discipline) |
| **Required By** | ADR-0007 (chassis art lazy-load contract surface) |
| **Amends** | None |
| **Supersedes** | None |
| **Forward-references** | ADR-0001 (vehicle-part art pipeline forward use case) |

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.13f1) |
| **Domain** | Asset pipeline / Runtime asset loading |
| **Knowledge Risk** | MEDIUM — Addressables has had multiple breaking-change waves across Unity 6.x; the 6.3 LTS surface is stable but distinct from pre-6.0 patterns. `unity-addressables-specialist` must validate the catalog/group/profile config against 6.3 docs before Accepted. |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `.claude/docs/technical-preferences.md`, ADR-0001, ADR-0005, ADR-0007 |
| **Post-Cutoff APIs Used** | `AssetReferenceT<T>` (generic typed reference, stable in 6.x); content catalog API (potentially breaking — to verify). |
| **Verification Required** | (1) `unity-addressables-specialist` validates 6.3 surface. (2) Memory budget table authored. (3) Build-pipeline integration plan landed. (4) IL2CPP standalone smoke test loads one `ChassisArtBundle` via `AssetReferenceT<T>`. |

## GDD Requirements Addressed

- ADR-0007 §3.3 — chassis art bundle loading (primary use case).
- ADR-0001 visual part system — forward use case (deferred).

## Open Questions Resolved (2026-05-25)

All three open questions from the Proposed draft are resolved as part of the
Accepted transition:

1. **Local-only or remote-capable groups in EA?** — **Local-only.** All
   Addressables groups build into the local game install for EA. Remote CDN
   requires a separate ADR.
2. **Catalog rebuild trigger** — **Automatic on Unity build** at EA scope.
   No separate CI hook. Revisit when catalog grows non-trivial (e.g., Heavy
   Truck chassis added post-EA or part art pipeline lands).
3. **Async API discipline** — **`LoadAssetAsync` + `await` only; no
   `WaitForCompletion` on the gameplay thread.** See Runtime Discipline &
   Verification section above.

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-05-19 | user (locked) + claude (drafted) | **Initial Proposed.** Captures V&P Architecture R1 Phase A2 = B1 decision. Awaits technical-director sign-off + memory budget + build-pipeline integration plan before transition to Accepted. |
| 2026-05-25 | technical-director (sign-off) + claude (addendum) | **Accepted transition.** Added memory budget table (3-category EA: chassis art 12 MB / vfx-combat 1 MB / part art 28 MB reserved), build-pipeline EA plan, Runtime Discipline & Verification section (async API rule, unload contract, IL2CPP smoke test). Resolved all 3 open questions. TD-ADR CONCERNS applied before write. |

# Vehicle & Part Architecture — Review Log

This doc was split from `vehicle-and-part-system.md` after R7 verdict
MAJOR REVISION NEEDED (2026-05-19) invoked the creative-director's
R5-precedent structural-split recommendation. This log starts at R1 on
the new architecture-doc surface.

The monolith V&P GDD review history (R1–R7) is preserved at
`vehicle-and-part-system-review-log.md` and remains the canonical
historical artifact for the pre-split design space.

---

## Review 1 — 2026-05-19 — Verdict: MAJOR REVISION NEEDED

Scope signal: **L (Large)** — 5-8 days of focused authoring, gated by 1
ADR-class decision (engine-free vs Unity serialization) before revision
can start.

Specialists: unity-specialist, systems-designer, game-designer,
economy-designer, qa-lead, creative-director (senior synthesizer).

Blocking items: **7** | Recommended: **11** | Specialist disagreements
resolved: **3**

### Summary

The split is the right idea; the execution did not honor the strategic
intent. Architecture doc came in at 1,739 lines vs ~600 target — it
absorbed every R7 concern (IsPlayable ~70 lines, audio composition
contract, 13 edge cases, 5 tuning subsections, late §5.4) and reproduced
the monolith's surface area in a smaller file with the same review
pressure. §5.4 is the smoking gun — it ships knowing one load-bearing
validation gate is stale ("added in next revision pass") and AC-VPA24
vacuous-tests that missing gate.

**Creative-director senior verdict**: no second structural split
warranted. One disciplined revision pass, anchored on 3 Phase A decisions,
gets this to APPROVED. ADR-0007 cannot transition Proposed → Accepted
until this doc reaches APPROVED.

### Blocking Items (7)

1. **§3.1 contradiction: engine-free POCO vs `[SerializeField]`**
   [unity-specialist, creative-director] — `WastelandRun.Vehicle` declared
   `noEngineReferences: true` per ADR-0005 while `SlotDefinition` carries
   `[SerializeField]`. Mutually exclusive. **Phase A1 decision required.**
2. **§3.1 `int?` / `bool?` with `[SerializeField]`** [unity-specialist] —
   Unity does not serialize `Nullable<T>`. Replace with
   `HasMaxHpOverride + MaxHpOverride` discriminated pattern.
3. **§3.3 `AssetReferenceT<ChassisArtBundle>` binds unapproved
   dependency** [unity-specialist, creative-director] — Addressables not
   in `technical-preferences.md` Allowed Libraries. **Phase A2 decision
   required.**
4. **§5.1 vs §5.4 contradictory state models** [systems-designer,
   game-designer, creative-director] — §5.1: 3-state, §5.4: 4-state.
   Reconcile to single 4-state canonical (§5.4 wins).
5. **§3.5 knowingly stale — missing `CriticalThresholdPct ∈ [1,
   DegradedThresholdPct - 1]` validation gate** [systems-designer] — §5.4
   says "added in next revision pass." AC-VPA24 vacuous-tests it.
6. **§4.4 audio table missing Critical state contract** [game-designer]
   — Critical declared as state in §5.4 but no audio event entry. Player
   won't hear the most dangerous transition.
7. **§3.4 ScrapSlot can scrap structural slots** [unity-specialist] — No
   `!slot.IsStructural` guard. Player can scrap their own Hull.

### Recommended Revisions (11)

8. **§5.3 IEEE 754 cross-platform claim too broad** — scope to "IL2CPP
   x64; ARM64 not validated."
9. **§3.5 `OnValidate` doesn't block import** — switch to
   `AssetPostprocessor` or downgrade claim.
10. **§5.3 SafeAmplify chained `Inf`** [systems-designer] — add
    post-multiply finite check.
11. **§5.2 / event-handler idempotency guard** [systems-designer] —
    F-VP2 clamp hides upstream double-fire bugs.
12. **§5.2 BLOCK-2 close is aspirational** [economy-designer] —
    mechanics doc unwritten. Mark close as "pending mechanics-doc."
13. **§6.13 ReentrancyException kills run** [game-designer,
    creative-director] — roguelike-hostile. **Phase A3 decision required.**
14. **§3.4 IsPlayable UI affordance unspecified** [game-designer] —
    anchor-reference to mechanics doc minimum.
15. **§6.4-6.5 schema-drift scrap-refund undefined** [economy-designer]
    — define or explicit deferral.
16. **AC verb glossary** [qa-lead] — define
    "rejects/throws/logs/no-op" once at §9 head.
17. **AC splits** [qa-lead] — AC-VPA24 → 24a..i; AC-VPA20 → 20a/b/c;
    AC-VPA34 → 34a..d; AC-VPA08 rewrite; AC-VPA17b add zero-installed.
18. **AC-VPA25/26 vacuous-pass risk** [qa-lead] — mark as "verified
    when sister doc authored" or downgrade to advisory.

### Specialist Disagreements (resolved)

- **qa-lead**: "no AC for §6.13 reentrancy" — **AC-VPA31 already covers
  this.** Dropped.
- **economy-designer**: "MaxDeckSize no floor" — **line 1502 declares
  `[16, 256]`.** Dropped.
- **game-designer**: §6.2 1-Hp slot feel-cliff as BLOCKER vs
  **creative-director** downgrades to recommended. §6.2 explicitly defers
  to mechanics doc; architecture deferral correct. **Downgraded.**

### Phase A Decisions Locked (2026-05-19)

These three ADR-class decisions are required before Phase B revision can
start. All locked per creative-director recommendation and user approval.

1. **Phase A1: Engine boundary = A2 (POCO + Authoring DTO split)**.
   Keep `noEngineReferences: true` on `WastelandRun.Vehicle`.
   `SlotDefinition` becomes pure POCO; new `SlotDefinitionAuthoring`
   (Unity-side, `[SerializeField]`) converts to POCO at load. Preserves
   ADR-0005's unit-test promise. Costs ~80 lines in §3.1 (authoring/
   runtime split table).
2. **Phase A2: Addressables = B1 (approve)**. Addressables added to
   `technical-preferences.md` Allowed Libraries 2026-05-19. ADR-0008
   stub opened (Proposed) for technical-director sign-off paper trail.
3. **Phase A3: Reentrancy policy = C2 (safe-state finish)**. Bus catches
   `VehicleReentrancyException` at publication boundary, logs, finishes
   frame in safe state. Run continues. Preserves no-mid-tick-reentrancy
   contract without weaponizing it against the player.

### Phase B Revision Plan (5 working days, fresh session)

Pickup with `/clear` then read `production/session-state/active.md`.
Revision order:

- **Day 1 — Structural fixes**: Phase A1 implementation (§3.1
  POCO+Authoring DTO split); discriminated `Has*` pattern; Phase A2
  `AssetReferenceT<T>` confirmation; §3.4 `!slot.IsStructural` guard;
  Phase A3 `OnReentrancyDetected` safe-state path in §6.13.
- **Day 2 — State-model reconciliation**: §5.1 refactored to F-VP1 only,
  pointing at §5.4 as canonical 4-state model; §4.4 Critical entry
  added; §3.5 absorbs the `CriticalThresholdPct ∈ [1,
  DegradedThresholdPct-1]` gate (no more "added in next revision pass").
- **Day 3 — Formula tightening**: §5.3 IEEE 754 claim scoped to IL2CPP
  x64; §5.3 post-multiply finite check; idempotency guard at
  `OnPartUninstalled`.
- **Day 4 — AC splits and verbs**: §9 verb glossary; AC-VPA24/20/34
  splits; AC-VPA08 rewrite; AC-VPA17b add; AC-VPA25/26 status.
- **Day 5 — Economy and dependency hygiene**: §3.4 mechanics-doc
  anchor; §5.2 BLOCK-2 explicit-pending language; §6.4-6.5 schema-drift
  refund clause or deferral; §7 `[REVERSE PENDING]` policy reaffirmed.

### Re-review Approach

Focused re-review on changed sections only (§3.1, §3.3, §3.4, §3.5,
§4.4, §5.1, §5.3, §5.4, §6.13, §9). A focused re-review on these
sections should pass in one round. **Expected R2 verdict: APPROVED.**

Prior verdict resolved: First review of this doc (split from monolith
V&P GDD post-R7). Monolith review history at
`vehicle-and-part-system-review-log.md`.

---

## Phase B Revision — 2026-05-19 — COMPLETE (all 18 R1 findings closed)

Phase B revision ran in a single working day across 5 sub-day passes
(Days 1–5). Doc grew 1,739 → 2,191 lines (+452). AC count 35 → 50.

**Closure summary by day:**

- **Day 1 — Structural fixes**: Blockers #1 (A1), #2, #3 (A2), #7
  closed via §3.1 POCO+DTO split, discriminated `Has*` pattern,
  ADR-0008 stub, §3.4 ScrapSlot structural guard, §6.10 bus-side
  safe-state catch (A3). Recommendation #13 (A3) closed.
- **Day 2 — State-model reconciliation**: Blockers #4, #5, #6 closed
  via §5.1 collapse to F-VP1, §5.4 canonical 4-state declaration,
  §3.5 `CriticalThresholdPct` gate, §3.3 `FrameLayoutSO`
  `CriticalThresholdPct` field, §4.4 Critical audio entry +
  Offline→Destroyed vocabulary sweep.
- **Day 3 — Formula tightening**: Recommendations #8, #10, #11
  closed via §5.3 IL2CPP x64 scope narrowing, §5.3 post-multiply
  finite check + §6.12 cross-link, §5.2 upstream idempotency
  requirement + `SlotAlreadyOccupiedException` + `InstalledCountClamped`
  telemetry.
- **Day 4 — AC splits and verbs**: Recommendations #9, #16, #17, #18
  closed via §9.0 verb glossary + named-exception catalog, AC-VPA08
  rewrite, AC-VPA17 → 17a/b, AC-VPA20 → 20a..d, AC-VPA24 → 24a..i,
  AC-VPA34 → 34a..d, AC-VPA25/26 status notes, §3.5 OnValidate
  two-layer enforcement clarification.
- **Day 5 — Economy and dependency hygiene**: Recommendations #12,
  #14, #15 closed. §5.2 BLOCK-2 reworded to "architecture surface
  only" + ADR-0007 status note pre-staged for Accepted (architecture
  surface) transition; §3.4 IsPlayable UI affordance mechanics-doc
  anchor added with `[REVERSE PENDING]` marker; §6.4 schema-drift
  refund deferred to mechanics-doc with `[REVERSE PENDING]` marker
  (option (b) per user lock); §7.4 policy reaffirmed —
  `[REVERSE PENDING — <reason>]` annotations mandatory at link sites,
  unmarked gaps block APPROVED.

**`[REVERSE PENDING]` markers planted in Phase B** (all point to
mechanics-doc, unwritten):

- §5.2 — `InstallCost` formula
- §3.4 — `IsPlayable == false` UI affordance
- §6.4 — Scrap refund on slot drop during schema drift
- §7.1 — ADR-0007 "Consequences" reverse-link (resolves on
  ADR-0007 Accepted)

Producer-backlog sweep of these markers is post-R2 per §7.4 policy.

**ADR-0007 status note**: pre-staged for the Proposed → Accepted
(architecture surface only) transition that fires on R2 APPROVED.
Full **Accepted across W0 scope** still requires mechanics-doc
approval (downstream from W2).

**Next**: R2 focused-section re-review on the 13 Phase B sections
only (§3.1, §3.3, §3.4, §3.5, §4.4, §5.1, §5.2, §5.3, §5.4, §6.10,
§6.12, §7, §9). Expected verdict: **APPROVED**.

---

## Review 2 — 2026-05-19 — Verdict: NEEDS REVISION

Scope signal: **M (Medium)** — surgical revision pass, single working
session (no fresh Phase A decisions needed; all findings are
section-local).

Specialists: qa-lead, unity-specialist (narrow specialist pass
selected by user instead of full 5-agent spawn; R2 surface area did
not justify spawning game-designer / economy-designer /
creative-director — all R1 structural decisions still hold).

Blocking items: **5** | Recommended: **9** | Specialist
disagreements resolved: **0**

### Summary

R2 focused-section re-review (10 of 13 Phase B sections analysed)
caught two carry-over bugs the Phase B sweep missed and three
fresh contract-surface issues that landed during Phase B itself:

- **B1/B2 (carry-overs)**: Phase A1 POCO + Authoring DTO split was
  applied to `SlotDefinition` but the AC layer (AC-VPA02/03) still
  asserted `[SerializeField]` on the POCO, and `AnchorPoint` (§3.2)
  never got the same treatment. Same class of bug, two surfaces.
- **B3 (Phase B introduction)**: §3.5 two-layer enforcement (added
  Day 2) claimed AssetPostprocessor throw blocks import. Unity 6.x
  Accelerator-cache and parallel-import workers don't formally
  guarantee that — defence in depth required.
- **B4/B5 (Phase B introduction)**: Two ACs landed during Phase B
  that bundled multiple guarantees into single ACs or omitted
  inner-exception preservation. Surgical splits + assertion adds.

No structural-split needed; no Phase A2 decisions needed. R2 is a
disciplined cleanup pass — 14 surgical edits, all section-local.

### Blocking Items (5)

1. **§9.1 AC-VPA02/03 carry-over from Phase A1 POCO + DTO split**
   [qa-lead] — Phase A1 declared `SlotDefinition` engine-free POCO,
   moved `[SerializeField]` onto new `SlotDefinitionAuthoring` DTO,
   but AC-VPA02 still asserted "all fields carry `[SerializeField]`"
   on the POCO. Split to AC-VPA02a (POCO zero Unity attrs) +
   AC-VPA02b (DTO has `[SerializeField]`). Same fix for AC-VPA03.
2. **§3.2 `AnchorPoint` did not receive Phase A1 POCO treatment**
   [unity-specialist] — Same `noEngineReferences: true` assembly as
   `SlotDefinition`, but `X`/`Y` fields still carry `[SerializeField]`.
   Strip them; document why `[System.Serializable]` is safe
   inside the engine-free assembly (System namespace, not UnityEngine).
3. **§3.5 AssetPostprocessor throw overstated as rollback guarantee**
   [unity-specialist] — Unity 6.x does not formally guarantee that a
   postprocessor throw evicts the failing asset from the import cache:
   Accelerator cache or parallel-import worker can leave a stale prior
   version resident. Either guarantee rollback (technically infeasible)
   or add a runtime guard. Belt-and-braces fix selected: soften §3.5 +
   add `FrameLayoutSO.IsValidated` runtime flag + §3.3 construction
   guard that refuses to construct against unvalidated assets.
4. **§9.6 AC-VPA31 bundled three Phase A3 guarantees into one AC**
   [qa-lead] — Phase A3 reentrancy policy specifies three independent
   guarantees: (a) telemetry emission, (b) Phase 3 completion, (c)
   run-continuation. AC-VPA31 collapsed all three into one. Split to
   31a/31b/31c so a single failure doesn't mask the others.
5. **§9.2 AC-VPA09 missing inner-exception preservation assertion**
   [qa-lead] — AC asserts `VehicleSubscriberException` is logged but
   doesn't verify the original throw is preserved via `InnerException`.
   A no-op fix that silently swallows exceptions and just logs the
   wrapper class would pass AC-VPA09 as written. Add explicit
   `caughtException.InnerException is …` assertion.

### Recommended Revisions (9)

6. **§7.2 missing ADR-0008 dependency row** [unity-specialist] — §3.3
   binds `AssetReferenceT<ChassisArtBundle>` per Phase A2 / ADR-0008
   approval, but §7.2 doesn't list ADR-0008. Add row with
   `[REVERSE PENDING — ADR-0008 currently Proposed]` annotation.
7. **§7.1 path drift `design/ux/hud.md` → `design/ux/combat-hud.md`**
   [qa-lead] — Dependency table references a path that doesn't exist
   in repo; correct file is `design/ux/combat-hud.md`.
8. **§9.7 AC-VPA34c "non-blocking" should read "ADVISORY"** [qa-lead]
   — §9.0 verb glossary added in Day 4 defines "ADVISORY" precisely;
   "non-blocking" pre-dates that glossary and now reads as informal.
   Align to glossary vocabulary.
9. **§9.7 AC-VPA34c Mono Editor Play Mode exclusion clause missing**
   [unity-specialist] — Mono JIT does not guarantee the same IEEE 754
   rounding contract as IL2CPP x64; in-Editor Play Mode tests of
   SafeAmplify determinism would produce false fails on Mono. Add
   explicit exclusion clause.
10. **§9.4 AC-VPA17b redundant "no telemetry is emitted" qualifier**
    [qa-lead] — §5.2 idempotency contract already specifies the no-op
    semantics; restating "no telemetry" in AC-VPA17b muddies
    AC-VPA17a's separate clamp-telemetry contract. Remove the tail.
11. **§9.6 §6.7/6.8/6.9 → §9.5 AC-VPA24f..i cross-reference missing**
    [qa-lead] — A reader scanning §9.6 for §6.7/6.8/6.9 coverage finds
    nothing because those edge cases are covered by AC-VPA24f..i
    in §9.5 (Phase B AC split). Add a cross-ref note after AC-VPA33.
12. **§3.5 stale-discriminator warning text not specified** [qa-lead]
    — The two "advisory only" Warning rules
    (`HasMaxHpOverride == false ⇒ MaxHpOverride ignored`, same for
    `HasStructuralOverride`) don't specify the exact warning message
    text. Designers, log search, and telemetry payloads should share
    one canonical string. Specify it.
13. **§3.3 `SpriteBundle` async-load mode note missing** [unity-specialist]
    — `AssetReferenceT<ChassisArtBundle>.LoadAssetAsync` is async-only;
    §3.3 construction pipeline doesn't say so explicitly. A reader
    could try to dereference `SpriteBundle` synchronously during
    construction and silently get a not-loaded handle. Add the
    async-only note.
14. **§9.4 AC-VPA22b `MaxHp = 1` threshold-collapse coverage missing**
    [qa-lead] — Existing AC-VPA22 covers the `MaxHp = 3` collapse, but
    the further `MaxHp = 1` case (both thresholds floor to 1, equal
    to `MaxHp`, Healthy band empty by construction) has no AC. §6.2
    covers the design rationale; §9 needs a matching AC.

### Advisory (separate from R1 numbering)

- **§9.7 AC-VPA35 production `.asmdef` glob bound missing** [qa-lead]
  — Static-analysis grep should be scoped to production runtime
  assemblies, not the whole repo (tests legitimately reference
  `System.DateTime`, `Stopwatch` for harness timing). Specify the
  glob bound:
  `src/runtime/**/WastelandRun.Vehicle*.asmdef` + closure; exclude
  `tests/**` and editor-only asmdefs.

### Specialist Disagreements (resolved)

- None. The narrow specialist pass (qa-lead + unity-specialist) did
  not generate disagreements — the two specialists' findings were
  complementary (qa-lead caught AC-layer issues, unity-specialist
  caught engine-boundary issues).

### Phase A Decisions

None required. All R1 Phase A decisions still hold; R2 findings are
section-local cleanups, not structural decisions.

### Closure summary (R2 revision pass)

R2 revision ran in a single in-session pass. 14 surgical edits
applied:

- **B1 (§9.1)**: AC-VPA02 → 02a/02b; AC-VPA03 → 03a/03b
- **B2 (§3.2)**: AnchorPoint POCO; engine-free comment block
- **B3 (§3.3 + §3.5)**: Async-load note; IsValidated runtime field
  + construction guard; §3.5 softening with defence-in-depth language
- **B4 (§9.6)**: AC-VPA31 → 31a/31b/31c
- **B5 (§9.2)**: AC-VPA09 inner-exception assertion
- **R6 (§7.2)**: ADR-0008 dependency row
- **R7 (§7.1)**: Path drift to combat-hud.md
- **R8 + R9 (§9.7)**: AC-VPA34c "non-blocking" → "ADVISORY"; Mono
  editor Play Mode exclusion clause
- **R10 (§9.4)**: AC-VPA17b telemetry qualifier removed
- **R11 (§9.6)**: AC-VPA33 cross-ref to §9.5 24f..i
- **R12 (§3.5)**: Stale-discriminator warning text
- **R13 (§3.3)**: SpriteBundle async-load mode note
- **R14 (§9.4)**: AC-VPA22b `MaxHp = 1` collapse
- **advisory (§9.7)**: AC-VPA35 production-asmdef-glob bound

Doc growth: 2,191 → 2,328 (+137 lines). AC count: 50 → 57 (+7 from
the 02a/02b/03a/03b/22b/31a/31b/31c splits, net of AC-VPA02 and
AC-VPA31 retiring as bare references).

**Next**: R3 focused-section re-review on the 10 R2-touched sections
only (§3.2, §3.3, §3.5, §7.1, §7.2, §9.1, §9.2, §9.4, §9.6, §9.7).
Expected verdict: **APPROVED** — no new structural surface
introduced; all edits surgical and section-local.

Prior verdict resolved: R1 MAJOR REVISION NEEDED (Phase B closure
2026-05-19, all 18 R1 findings closed).

---

## Review 3 — 2026-05-19 — Verdict: NEEDS REVISION

Scope signal: **S (Small)** — 8 blockers, all section-local prose/AC
edits; no Phase A decisions required. Single-session revision pass.

Specialists: qa-lead, unity-specialist (narrow pass — consistent with
R2 approach; full 5-agent spawn not warranted for focused re-review).

Blocking items: **8** | Recommended: **4** | Advisories: **7**

### Summary

R3 was scoped as a rubber-stamp pass on 14 surgical R2 fixes. Two
clusters flipped the verdict:

**Cluster 1 — §9.1 carry-overs (B1, B2):** The B1 R2 split introduced
a naming error (`ToRuntime()` vs `ToPoco()`) and a hedged type-kind
assertion into AC-VPA02b/03b — the ACs most directly verifying the
Phase A1 POCO boundary. Both are testability failures introduced by the
R2 revision itself.

**Cluster 2 — §3.3/§3.5 technical defects (B6, B7, B8):** The
unity-specialist surfaced three interconnected defects in the
`IsValidated` mechanism and postprocessor contract. The async-only
claim was factually wrong (`WaitForCompletion()` exists). `IsValidated`
resets on every domain reload, making the runtime guard ineffective in
Play Mode without a prior reimport. The postprocessor throw does not
abort the Unity 6.x import pipeline — the "blocking gate" claim was
incorrect.

No Phase A decisions required. All 8 blockers were section-local prose
or AC edits resolved in a single revision session.

### Blocking Items (8)

1. **§9.1 AC-VPA02b/03b: `ToRuntime()` vs `ToPoco()` naming mismatch**
   [qa-lead] — B1 R2 split introduced wrong method name. Fixed: replaced
   every `ToRuntime()` with `ToPoco()` in both ACs.
2. **§9.1 AC-VPA02b: hedged type-kind "ValueType (or class)"**
   [qa-lead] — Untestable assertion. Fixed: removed hedge; committed to
   `ValueType` (§3.1 declares `SlotDefinitionAuthoring` as `struct`).
3. **§9.2 AC-VPA05: slot indices unspecified; ordering assertion
   non-deterministic** [qa-lead] — Phase 2 commit order is layout-index
   order; event-name ordering depends on which slot has the lower index.
   Fixed: added "plate at index 0, hull at index 1" to setup; reframed
   assertion in layout-index-order terms.
4. **§9.6 AC-VPA31b: vacuously satisfiable with one subscriber**
   [qa-lead] — "Remaining subscribers execute" is vacuous if only one
   subscriber registered. Fixed: added 3-subscriber setup to AC-VPA31a
   (well-behaved #1/#3, reentrant #2); AC-VPA31b now has concrete
   non-vacuous coverage.
5. **§9.7 AC-VPA34c: CI enforcement of IL2CPP vs Mono gate level
   unspecified** [qa-lead] — AC is BLOCKER on IL2CPP but ADVISORY on
   Mono without CI enforcement spec. Fixed: added build-target assertion
   step requirement; Mono-backend results are advisory only.
6. **§3.3 async-only claim factually incorrect** [unity-specialist] —
   "`LoadAssetAsync` is the only resolution path; no synchronous variant
   exists" is wrong — `WaitForCompletion()` exists. Fixed: reframed as
   frame-budget prohibition, not API absence.
7. **§3.3 `IsValidated` resets on every domain reload** [unity-specialist]
   — `[System.NonSerialized]` fields initialize to `false` on domain
   reload (Play Mode entry). Postprocessor only re-sets on import, not
   on reload. Fixed: scoped `IsValidated` as editor-only mid-session
   defense; documented domain-reload limitation; noted
   `IPreprocessBuildWithReport` as the build-time blocking gate.
8. **§3.5 postprocessor throw does not abort Unity 6.x import**
   [unity-specialist] — Exceptions from `OnPostprocessAllAssets` are
   caught by Unity's import pipeline. Import continues; "blocking gate"
   claim was incorrect. Fixed: renamed layer to "editor-session
   observability layer"; corrected throw behavior; added planned
   `IPreprocessBuildWithReport` hardening step as build-time gate.

### Recommended Revisions (4)

9.  **§9.2 AC-VPA06** [qa-lead] — "phase 2c (VFX)" label; use
    `SubscriberPhase.VFX`. Verify `OnArmorExposed` in §4 event catalog.
10. **§9.4 AC-VPA22b** [qa-lead] — Add assertion that `state ==
    Degraded` is unreachable at `MaxHp = 1`.
11. **§9.7 AC-VPA35** [qa-lead] — `includePlatforms`-only exclusion
    heuristic fragile; add named exclusion list or enforce convention.
12. **§3.3 `ChassisDefinitionSO` fields** [unity-specialist] —
    `[SerializeField] public` pattern violates project coding standards;
    use `[field: SerializeField] public ... { get; private set; }`.

### Phase A Decisions

None required. B7 resolved as scope-documentation (Option C); B8
resolved as role-reframe. No new contract surface introduced.

### Closure summary (R3 revision pass)

R3 revision ran in a single in-session pass. 9 surgical edits applied
across 5 sections:

- **B6 (§3.3)**: Async-only claim reframed as frame-budget prohibition
- **B7 (§3.3)**: IsValidated comment scoped; step 1 guard scoped;
  domain-reload limitation documented
- **B8 (§3.5)**: Postprocessor role reframed; throw behavior corrected;
  IPreprocessBuildWithReport planned hardening noted
- **B1 (§9.1)**: ToRuntime() → ToPoco() in AC-VPA02b + AC-VPA03b
- **B2 (§9.1)**: Removed "or class" hedge from AC-VPA02b type-kind
- **B3 (§9.2)**: Slot indices added to AC-VPA05; ordering reframed
- **B4 (§9.6)**: 3-subscriber setup added to AC-VPA31a
- **B5 (§9.7)**: CI enforcement clause added to AC-VPA34c
- **Header**: last-updated updated to "2026-05-19 (R3 revision pass)"

**Next**: R4 focused-section re-review on the 6 R3-touched sections:
§3.3, §3.5, §9.1, §9.2, §9.6, §9.7. §3.2, §7.1, §7.2 unchanged from
R2; no R3 edits applied, no re-review needed.

Prior verdict resolved: R2 NEEDS REVISION (R3 revision pass 2026-05-19,
all 8 R3 findings closed).

---

## Review 4 — 2026-05-19 — Verdict: APPROVED (accepted as-is after R4 revision pass)

Scope signal: **S (Small)** — 4 blockers, all section-local; resolved in a single revision session.

Specialists: unity-specialist, qa-lead, game-designer, systems-designer, creative-director (senior synthesizer). Full 5-agent spawn (first full spawn since R1; focused scope warranted full pass).

Blocking items: **4** | Recommended: **10** | Advisories: **9**

### Summary

R4 was expected to reach APPROVED after the R3 surgical revision pass closed all 8 prior blockers. Two defect clusters surfaced instead, preventing approval until the revision pass:

**Cluster 1 — `IsValidated` session lifecycle (B1, triple-corroborated):** The `FrameLayoutSO.IsValidated` guard in §3.3 step 1 throws `VehicleConstructionException` on every domain reload (every Play Mode entry), making it either always-firing on valid assets or dead code. The doc documented the domain-reload limitation but did not reconcile it with the guard's runtime behavior. Resolution: guard scoped `[Editor only]` with `!Application.isEditor` skip in shipped builds; exception renamed `LayoutNotValidatedThisSessionException` (distinct from data-corrupt errors); shipped-build skip and reimport instructions added; new exception added to §9.0 catalog; §6.10 telemetry fields named (`OffendingSubscriberType`, `OriginatingEventName`, `RejectedMutatorSignature`).

**Cluster 2 — `[SerializeField] public` on SOs (B2):** R3 recommendation #12 not actioned. `ChassisDefinitionSO` and `FrameLayoutSO` used `[SerializeField] public` fields on `sealed class : ScriptableObject`, contradicting the doc's own read-only runtime contract and project coding standards. Converted to `[field: SerializeField] public T Prop { get; private set; }` throughout; `IsValidated` kept as a plain public field with justification comment (needs external writes from `AssetPostprocessor`); explanatory comment distinguishes `SlotDefinitionAuthoring` as the intentional `[SerializeField]` exception.

**Cluster 3 — AC-VPA31 reentrancy (B3, B4):** AC-VPA31a named `VehicleEvent.ReentrancyDetected` but §6.10 uses `VehicleEvent.ReentrancyBlocked` — naming inconsistency. Setup omitted concrete vehicle state, damage amounts, and telemetry field names. AC-VPA31b was not falsifiable: "given the same setup as AC-VPA31a" was ambiguous between independent setup and chained state; "internally consistent" had no observable for Phase 3 completion; no assertion prevented ghost-kill (reentrant damage partially applied without events, Phase 3 computing death from unseen damage). Resolution: AC-VPA31a fully respecified with setup/action/expected format, corrected event name, and named fields; AC-VPA31b fully rewritten with independent setup, `hpBeforeAction` baseline, three explicit assertions (subscriber #3 executes, `slot.CurrentHp == 5` confirming zero reentrant effect, Phase 3 state consistent and `OnVehicleDied` not fired).

**Creative-director senior verdict**: Structure is sound; all 40+ prior blockers held; R4 is a focused fix pass not a structural redo. ADR-0007 transitions Proposed → Accepted (architecture surface only). Mechanics-doc is downstream; W2 (EnemyDefinitionSO + BrainRulesetSO) unblocks.

### Blocking Items (4)

1. **§3.3/§3.5 `IsValidated` session lifecycle** [unity-specialist + game-designer + systems-designer] — construction guard always fires on domain reload; guard scoped to `[Editor only]`; `LayoutNotValidatedThisSessionException` introduced; shipped-build skip documented.
2. **§3.3 `[SerializeField] public` on ScriptableObject fields** [systems-designer; unity-specialist] — R3 rec #12 not actioned; converted to `[field: SerializeField]` properties throughout.
3. **§9.6 AC-VPA31a telemetry field names unspecified** [qa-lead] — event name corrected `ReentrancyDetected` → `ReentrancyBlocked`; setup concretised; `OffendingSubscriberType` and `OriginatingEventName` named.
4. **§9.6 AC-VPA31b not falsifiable** [qa-lead + game-designer + systems-designer] — full rewrite: independent setup, zero-reentrant-effect assertion, Phase 3 observable.

### R4 Revision Closure (all 4 blockers resolved in-session)

Doc growth: ~2,460 → ~2,495 lines (+~35).

**ADR-0007 status transition**: Proposed → **Accepted (architecture surface only)**. Full Accepted across W0 scope still requires mechanics-doc approval (downstream from W2).

Prior verdict resolved: R3 NEEDS REVISION (R4 revision pass 2026-05-19, all 4 R4 findings closed).

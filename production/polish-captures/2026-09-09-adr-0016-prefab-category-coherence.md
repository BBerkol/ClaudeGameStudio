# Capture — ADR-0016 Prefab Category Coherence (new ADR)

**Date:** 2026-09-08
**System:** Architecture documentation — `docs/architecture/adr-0016-prefab-category-coherence.md`
**Driver:** `production/remediation-plan-2026-09-08.md` §0.2
**Change class:** New Accepted ADR (~340 lines). No runtime code, no prefab, no scene.

## What is being destroyed

**Nothing.** This is a net-new file at a path that has never existed
(`git log --all --diff-filter=A` shows no ADR-0016 was ever added). No authored
values, no designer tunes, no serialized data are at risk.

The capture-before-destroy hook fired on the *size* threshold ("new system code
≥50 lines"), not on a destructive edit. This capture exists to satisfy the
protocol and to record the TD verdict, which is the part with real content.

## Why now

1. **Accepted ADR-0017 depends on a document that does not exist.** It cites
   ADR-0016 five times — lines 41, 49 (`Depends On` row), 269, 334, 460.
2. **Number collision risk.** `production/td-verdicts/2026-07-29-whole-game-health-opt-audit.md:262`
   proposes recycling 0016 for an unrelated "Derived-State Caching Pattern."
3. **~10 production docs** already reference ADR-0016 as governing practice.

## Final-game picture this serves

Every future "where does this new thing live?" question — the next beacon type,
the pause menu, reward inventory, boss cutscenes — gets answered by citing a
principle instead of by precedent-matching whatever the last slice did. The
absence of this document is what allowed the run-loop driver to accrete inside
the combat composite across two slices.

## Facts verified against current code before drafting (2026-09-08)

| Claim | Result |
|---|---|
| `Run.prefab` / `Combat.prefab` separately scoped | CONFIRMED — `Assets/Prefabs/Run/`, `Assets/Prefabs/CombatView/` |
| Cross-prefab containment is zero both directions | CONFIRMED — Combat GUID `4ee0cdd0…` in `Run.prefab` = 0; Run GUID `87507ab0…` in `Combat.prefab` = 0 |
| `VehicleHudAnchors` is a sibling component | CONFIRMED — `VehicleHudAnchors.cs:41`, resolved via `visual.GetComponent<>()` at `VehicleBarStack.cs:216/251/351` |
| `VehicleBodyworkAnchors` unbuilt | CONFIRMED — zero hits under `Assets/` |
| ADR-0011 mandates a titled "ADR-0011 compliance" paragraph | CONFIRMED — `adr-0011:108` and validation checklist `:219` |
| Prior ADRs comply with that mandate | **FAILED** — 0013 = 0, 0014 = 1, 0015 = 0, 0017 = 0. Three of four skip it. |
| `ChopshopRoot.prefab` is surgical-YAML-only (per memory) | **REFUTED** — `AuthorChopshopRootPrefab()` called from `AuthorAllScenes` at `CombatPrefabAuthor.cs:8273`, writes via `SaveAsPrefabAsset` at `:9127`. The tool owns it. The memory's real point is narrower: *re-running* it orphans the `m_IsActive` override in `RunScene.unity`. |
| `Assets/Scenes/CombatScene.unity` duplicate exists | CONFIRMED — 18,016 bytes (Jul 26) alongside `Beacons/Combat.unity` 7,225 bytes (Jul 28) |
| 2026-06-17 verdict asks for scene topology as an application | CONFIRMED — verdict line 69, verbatim: *"ADR-0016, when it lands, should explicitly cite scene topology as a second application of the principle, with this verdict as the worked example."* |

## Technical Director Review

**Verdict: RESHAPE** (technical-director, 2026-09-08, Opus)

TD read ADR-0011, ADR-0014, ADR-0017 in full plus the 2026-06-17 and 2026-07-29
verdicts, and independently confirmed the gap, the `Run.prefab` contents
(`MapView`:15, `Run`:92, `RunCompleteView`:207, `GameOverView`:277), zero
cross-references to the Combat.prefab GUID, `CombatPrefabAuthor.cs` at 9,348
lines, and `VehicleBodyworkAnchors` unbuilt.

### Primary finding — cut the authoring-surface application

> **Do not ship "the tool IS the authoring surface" in this ADR.**
>
> **1. It fails the ADR's own smell test.** The scope boundary says 0016
> governs composition (where a thing lives) only. Authoring authority is not
> composition — it is *who is allowed to write the bytes*. Would someone who
> opened ADR-0016 to answer "where does this component live?" expect to find a
> rule prohibiting hand-YAML edits? No. That is the accretion pattern the ADR
> exists to name, committed inside the ADR that names it. Path-dependence —
> "0016 was originally scoped as authoring-surface, so the authoring rule should
> also live here" — is the exact reasoning that put `RunSceneHost` in
> `Combat.prefab`.
>
> **2. The stated carve-out is factually wrong.** `CombatPrefabAuthor.cs:8247`
> declares `ChopshopRootPrefabPath` and `AuthorAllScenes()` authors it. Shipping
> an Accepted ADR whose exemption list contradicts the code is worse than
> shipping no ADR — 0016's entire existence is a lesson about documents that
> don't match reality. Related: "the tool" is not singular — four author entry
> points write prefabs (`CombatPrefabAuthor`, `AuthorRunHUDHost`,
> `AuthorDialogueSceneRoot`, `AuthorBiome1MapThemeIcons`).
>
> **3. It entrenches what you are actively migrating away from.** The
> remediation plan calls the 9,348-line author *the root cause*; P4 permanently
> retires two of the four currently-drifting prefabs. The rule's scope shrinks
> by design. Accepted today = amendment owed at P4. That is throwaway
> scaffolding in document form.
>
> **4. The durable part is already covered.** What is load-bearing is
> single-writer per authored artifact — ADR-0011 forbidden pattern #2 (parallel
> storage) in authoring form. It does not need a new ADR.
>
> **Replacement:** one out-of-scope sentence pointing at ADR-0011 #2 and
> ADR-0014 P4. If the operational rule needs a home, that home is
> `docs/architecture/control-manifest.md` — per `docs/CLAUDE.md` it is the
> versioned "flat programmer rules sheet," designed to change as reality
> changes. An ADR is not.

### Full delta list

| # | Delta | Why |
|---|---|---|
| 1 | Cut Application 2 (tool-is-authoring-surface); replace with an out-of-scope sentence | Not composition; carve-out contradicts `CombatPrefabAuthor.cs:8247`; scope shrinks at P4 |
| 2 | New Application 2 = scene-vs-prefab topology, citing the 2026-06-17 verdict as worked example | Requested at verdict line 69; it is the level the next question arrives at |
| 3 | `Date: 2026-06-17`; Status `Accepted (2026-06-17 — document authored 2026-09-08)`; one-line note in ADR-0017 Related | Makes 0017's `Depends On` chronologically valid without a novel status token |
| 4 | Category primary, cadence tiebreaker-only, with two stated conditions | Two co-equal axes with no precedence is an exploitable hole |
| 5 | Add containment grep gate (Run.prefab ↔ Combat.prefab GUID, baseline 0); reframe rejection as invariant-gated / judgment-review-time | A mechanizable subset exists; §0.1 is already touching that file |
| 6 | Add the titled "ADR-0011 compliance" paragraph | `adr-0011:108`/`:219` mandate it; 3 of 4 prior ADRs skipped it |
| 7 | Add "resolve once at bind, cache; never per-frame" to the cross-seam wiring rule | Otherwise the ADR licenses a per-frame `FindAnyObjectByType` |
| 8 | Make the smell test a required TD-brief field | The 2026-06-17 failure was an unasked question, not an unknown rule |
| 9 | Resolve `CombatScene.unity` vs `Beacons/Combat.unity` before writing | Live category collision in the tree on authoring day |
| 10 | Replace strawman Alternative 2 with "case-by-case TD judgment, no codified principle" | That is the actual rejected status quo, with a documented incident |
| 11 | Demote duplicated rationale in `adr-0017:332` + the 2026-06-17 verdict to "see ADR-0016" pointers | Four copies of the same reasoning will drift |
| 12 | One-line Related note closing the 0016 number collision | Closes it in the record, not just by arrival order |

### TD self-audit (three lenses)

- **Codebase health** — surfaced the ADR-0011 compliance-paragraph drift (3 of 4
  prior ADRs non-compliant) and the four-way duplication of the smell-test
  rationale. Both folded into deltas 6 and 11.
- **Optimization** — zero runtime cost; the real cost is the wiring rule, which
  is unbounded as drafted. Delta 7 bounds it. Cutting Application 2 *reduces*
  cost (no exemption list, no amendment owed at P4).
- **1.0 survival** — category-primary, Application 1 and Application 3 all
  survive unchanged. Application 2 as drafted does **not** survive P4. Proposed
  scene-vs-prefab replacement does.

**TD success criterion:** *"the next new beacon type gets a scene-vs-PrefabRoot
decision made by citing 0016 rather than by precedent-matching whatever the last
beacon did; and ADR-0014 P4 lands without needing to amend 0016."*

## Disposition of the 12 deltas

**Applied in the ADR as written:** 1, 2, 3 (0016 side), 4, 6, 7, 8, 10, 12.

**Modified — delta 9.** TD asked to resolve the `CombatScene.unity` duplicate
before writing. Declined as a blocker, because
`production/remediation-plan-2026-09-08.md` §4.1 explicitly rules it out of the
current slice: *"Do not bundle: `Assets/Scenes/CombatScene.unity` is a separate
orphan… Different question, own follow-up."* Resolving it means a scene deletion
plus a build-settings edit, which is Phase 4 work with its own acceptance test.
**Instead the ADR names it as a known open violation with a pointer to §4.1.**
An ADR that admits its own first unresolved violation is honest; one that
silently omits it is the failure mode 0016 exists to prevent.

**Deferred with reason — delta 11.** Demoting the duplicated rationale requires
editing Accepted ADR-0017 and a historical TD verdict. Rewriting a historical
verdict is not appropriate — it is a dated record of what was said. The
ADR-0017 edit is worth doing and is logged as follow-up alongside delta 3's
one-line Related note; both are protected-path edits deserving their own pass.

**Deferred to a follow-up commit — delta 5.** The containment gate belongs in
`tools/ci/grep-gates.sh` in the **Unity** repo; this ADR is in the framework
repo. Baseline verified 0 in both directions, so it will land green.

## Files touched by this change

- `docs/architecture/adr-0016-prefab-category-coherence.md` — **new file**
- This capture — **new file**

No prefabs, no scenes, no ScriptableObjects, no runtime code.

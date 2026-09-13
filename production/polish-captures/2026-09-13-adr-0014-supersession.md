# Capture — ADR-0014 supersession by ADR-0018

**Date:** 2026-09-13
**System:** UI stack policy — which canvases stay UGUI
**New file:** `docs/architecture/adr-0018-ugui-retention-transient-world-anchored-annotations.md`
**Modified:** `docs/architecture/adr-0014-ui-toolkit-primary-stack-hybrid.md`
(Status pointer only — **body not edited**)

## What is being destroyed

No authored values, no prefab data, no code. What is being retired is the
**authority** of six statements in ADR-0014, each enumerated below with the
verification that killed it. ADR-0014's body is preserved verbatim; only its
Status gains a supersession pointer.

| # | Statement being retired | Where | Verified reality (2026-09-13) |
|---|---|---|---|
| 1 | UGUI is retained for "the **world-space** `Popups` canvas" | title, `:24-25`, `:152` | `Popups` is `m_RenderMode: 1` — ScreenSpaceCamera. `CombatHud.ApplyScreenSpaceCamera` (`:520-526`) forces it there every HUD build |
| 2 | The exception is justified by avoiding `RuntimePanelUtils.ScreenToPanel` versus "UGUI's direct WorldSpace Canvas" | `:113-115` | Not a WorldSpace canvas; `DamagePopupWidget` already does that conversion and performs fine. Self-refuting for its own example |
| 3 | "No new UGUI `Canvas` may be added outside the `Popups` canvas subtree" | `:196-197` | 13 of 14 shipping canvases are outside it |
| 4 | Current State enumerates the UGUI surfaces | `:77-91` | Omits `HudAnchors` ×4, `HitZonesCanvas` ×4, `CardHand`, `BuffStripCanvas`, `DamagePopupCanvas` |
| 5 | "Zero UI Toolkit in the project today. Zero UXML, zero USS." | `:91` | 18 `.uxml`, 22 `.uss`, 19 scripts referencing `UIDocument` |
| 6 | P5 gate: forbid `Canvas` outside the `Popups` subtree | `:247` | Would fire on 13 canvases the day it shipped |

**Preserved unchanged** and carried into ADR-0018: UI Toolkit as the primary
stack, the P1–P5 phase structure, P3's landed migration detail, and the
rollback-per-phase shape.

## How the audit was done

Every `Canvas` component in `Assets/Prefabs` and `Assets/Scenes` was read
straight from YAML — 14 across 7 files — resolving each to its GameObject name
with its real `m_RenderMode`, `m_OverrideSorting` and `m_SortingOrder`, then
diffed against the document. Two runtime-built canvases (`IntentCanvas`,
`TargetingReadoutCanvas`) were added from `CombatHud.BuildWorldCanvas` call
sites. The full inventory is reproduced in ADR-0018's Context section.

This is the step the TD made ADR-0018 conditional on, on the grounds that it had
found two additional false-adjacent facts within ten minutes and concluded the
inventory had never been checked against the prefabs at all. That was correct:
the audit found **four** more beyond the original `Popups` finding.

## Technical Director Review

The technical-director was consulted on this specific question during the
2026-09-12 session and returned a verdict. Quoted where load-bearing:

> **REJECT (the category as scoped) — AMEND to a narrower one.** *"Your finding
> is confirmed and it is worse than stated."*

On the two obvious replacement predicates, both of which it disproved:

> *"`CardHand` serializes `m_RenderMode: 2` … nested under `Combat_HUD`, so it
> renders screen-space and the value is inert — but a grep for `m_RenderMode: 2`
> catches it. … `PlayerVehicle.prefab` carries **two** WorldSpace canvases:
> `HitZonesCanvas` and **`HudAnchors`** … `HudAnchors` hosts MainBar and the
> rings — the single largest P4 surface. So the category does not merely
> mis-exclude `Popups`. It mis-includes the ring stack."*

On why no predicate is offered:

> *"A gate must key on something checkable. The three real members share no
> checkable field… Any predicate tight enough to exclude `HudAnchors` and
> `CardHand` degenerates into a name allowlist — and a name allowlist is the
> exclusion-as-bridge I objected to. **P5's gate must not be written against
> this category.** … Call it a registry, not a predicate. That is honest about
> being an allowlist instead of dressing one up as a rule."*

On the artifact:

> *"An append-only amendment on top of a document whose title, summary,
> decision, diagram and consequences all assert a false fact leaves six wrong
> statements in force and one correction buried at the bottom. **Write ADR-0018
> … superseding ADR-0014.** … Mark ADR-0014 Superseded by ADR-0018 with a
> one-line pointer — do not edit its body."*

On the withdrawn rationale:

> *"The stated rationale is unevidenced and, for its named example,
> self-refuting. What actually justifies the exception is ergonomic, not
> performance."*

**Conditions imposed and honoured in ADR-0018:** the membership predicate and a
per-member exit criterion are stated; the registry is closed at four members
with a stated re-derivation threshold of five; `TargetingReadoutCanvas` retains
membership and therefore stays pre-P4.

**Deviation recorded:** the TD listed `Popups`, `HitZonesCanvas`, `IntentCanvas`
and `TargetingReadoutCanvas` as members. The audit surfaced a fifth candidate it
had not seen — `DamagePopupCanvas` (ScreenSpaceOverlay, order 30, in
`DamagePopupSpawner.prefab`). It is **not** granted membership by default;
ADR-0018 records it as P4 scope with an open question, because whether damage
numbers actually render there or on `Popups` was not established and the answer
decides its classification. Granting membership to an unexamined canvas is how
registries rot.

## Risk accepted

A semantic invariant cannot be mechanically enforced, so the registry needs
human review whenever a member is added. ADR-0018 names this in Consequences
rather than leaving it to be discovered, and caps the registry at five members
before the category must be re-derived.

## Approval

User approved proceeding with step 3 (ADR-0018 + the gating canvas-inventory
re-verification) on 2026-09-13.

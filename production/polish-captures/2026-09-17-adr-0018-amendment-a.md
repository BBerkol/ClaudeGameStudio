# Capture — ADR-0018 Amendment A (category re-derivation)

Date: 2026-09-17
File: `docs/architecture/adr-0018-ugui-retention-transient-world-anchored-annotations.md`

## What is being replaced

The "Open questions carried into P4" section's two question bodies (both now
answered) are compressed to their resolutions, with the original prose
preserved in git history:

1. **HudAnchors question** (3 paragraphs): asked whether P4 migrates
   world-anchored positioning or HudAnchors joins the registry "with numbers
   attached". Resolved by Amendment A: seated in the registry under the
   re-derived invariant; the deciding argument is authoring parity, not
   performance, and the exit criterion (A.3) is ordered so authoring parity
   gates any cost spike.
2. **DamagePopupCanvas question** (1 paragraph): asked whether damage numbers
   render there or on `Popups`. Resolved by code read: `DamagePopupSpawner.cs`
   builds `DamagePopupCanvas` (ScreenSpaceOverlay, sortingOrder 30) and damage
   numbers render THERE; `Popups` (order 60) hosts the buff tooltip. Registry
   rows corrected in A.2.

The Migration Plan P4 row's original scope text is retained inline with
strikethrough (record style), status → CLOSED 2026-09-17 on P4a scope. No
other authored content in the ADR is altered; the §2 registry table remains
in place with A.2 as its superseding table.

## Why

ADR-0018's registry was factually wrong on the `Popups` row — the same
canvas-identity error class that sank ADR-0014 — and the fifth registry seat
was already spoken for, so seating `HudAnchors` exceeds the 5-member cap and
triggers the ADR's own re-derivation clause. Full reasoning:
`production/td-verdicts/2026-09-17-structural-debt-cleanup.md` §4.

## Technical Director Review

**Ruling (verbatim from the 2026-09-17 structural-debt verdict §4.4):**

> `HudAnchors` does not migrate in P4. The ADR-0018 category is re-derived in
> place as Amendment A, under which `HudAnchors` is a member on its merits
> rather than as an exception, and P4 closes on P4a scope.

Amendment content mandated by the TD (§4.4 "must contain, at minimum"):
correct the `Popups` row (member is the buff tooltip); seat
`DamagePopupCanvas` with the exit criterion previously misfiled under
`Popups`; seat `HudAnchors` with children counted as children (MainBar,
rings, badges, nested BuffStripCanvas — not separate members); restate the
invariant with `transient` dropped as descriptive-not-causal; reset the cap
(six members, re-derive at >7); record in Consequences that this is the
second canvas-identity error in the lineage and that YAML audits are
necessary but not sufficient. All six points are implemented in Amendment A
verbatim from the verdict's drafted text, including the A.3 exit criterion.

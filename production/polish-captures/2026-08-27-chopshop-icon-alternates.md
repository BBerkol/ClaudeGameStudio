# Capture — Chopshop map icon renders as Event (Biome1MapTheme)

**Date:** 2026-08-27
**System:** `MapBeaconStyleSO` icon variants — `Biome1MapTheme.asset`
**Severity:** Player-facing. Two-thirds of Chopshops are invisible as Chopshops.
**Status:** AWAITING USER APPROVAL — no edits made

---

## 1. The defect

`Biome1MapTheme.asset` `_beaconIconAlternates` row **[4] Chopshop** contains
Event's two sprites, byte-identical to row **[5] Event**:

```
  [4] Chopshop     -4081809818625799, 5021469096146529386
  [5] Event        -4081809818625799, 5021469096146529386   ← identical
```

Every other row is empty. This is the only instance — verified by dumping all
nine rows.

`MapBeaconStyleSO.BeaconIcon(type, variantKey)` (`:80-100`) picks
deterministically from `{primary} ∪ alternates[type]`:

```csharp
uint pick = ((uint)variantKey * 2654435761u) % (uint)total;
```

For Chopshop `total = 3` (1 primary + 2 alternates), so **only 1 in 3
Chopshops draws its real icon**. The other two draw Event sprites and read as
Event beacons on the map.

Measured across indices 0-58: **19/59 (32%)** resolve to `pick == 0`.

---

## 2. Player-facing impact, and how it was found

The user reported seeing 0-2 Chopshops per run across ~7 runs and never 3,
while the save file for those same runs contained exactly 3.

Predicted distribution if each of 3 shops is independently 1-in-3 visible:

| Visible | Predicted | User observed (n=7) |
|---|---|---|
| 0 | 30% | 1 run |
| 1 | 44% | 3 runs |
| 2 | 22% | 3 runs |
| 3 | 4% | 0 runs |

The shapes match. The user also reported the icon *was* distinguishable when
present — consistent with the 1-in-3 that renders correctly.

**Falsifiable prediction on the save at 22:42** (`run_seed 1236003781`), which
contains Chopshops at indices 2, 20, 23:

| Beacon | `pick` | Renders as |
|---|---|---|
| idx 2 — 37% across, 80% up | 1 | **Event** |
| idx 20 — 58% across, 50% up | 2 | **Event** |
| idx 23 — 80% across, 21% up | 0 | **Chopshop** ✓ |

So exactly one visible on that map, at the far right.

---

## 3. Diagnostic cost — recorded because the misdirection is the lesson

Roughly two hours went into the wrong layer. The chain:

1. User reports too few Chopshops → suspected generation.
2. Save file shows 3 → suspected reachability.
3. Routed-player model gives 3-of-3 → suspected the routing assumption.
4. Suspected band starvation from cluster attraction (TD had flagged it).
5. Built a 60-seed diagnostic. **First attempt hand-rolled "shipping-like"
   inputs and exhausted `MaxRetries=100`** — proving the hand-rolled config was
   not the shipping one, while writing a diagnostic about exactly that hazard.
6. Rebuilt using `Resources.Load` + `BiomeGenerationInputsFactory`. Result:
   **59/60 seeds place exactly 3, evenly spread at x ≈ 0.38 / 0.59 / 0.80.**
   Generator vindicated.
7. Only then traced the render path → `BeaconIcon` → the alternates table.

**The tell that was walked past:** the user said early on that the icon *was*
distinguishable. If two-thirds of a type render as a *different* type, that is
precisely what a player reports. The report contained the answer at step 1.

**Rule extracted:** when the persisted model and the screen disagree, the fault
is between them — walk the render path BEFORE re-litigating the model. The save
file was correct at every step; four hours of model analysis could not have
found a view-layer table.

---

## 4. What is destroyed

**One authored value: `_beaconIconAlternates[4].Alternates` — 2 sprite refs.**

```yaml
  - Alternates:                                   # [4] Chopshop
    - {fileID: -4081809818625799, guid: 6379bcf1aafc4dc4c87dc465cec7cf72, type: 3}
    - {fileID: 5021469096146529386, guid: 6379bcf1aafc4dc4c87dc465cec7cf72, type: 3}
```

becomes

```yaml
  - Alternates: []                                # [4] Chopshop
```

**Provenance:** not a deliberate art choice. These are Event's variants
duplicated into the Chopshop row — a copy-paste from the adjacent row. There is
no Chopshop variant art in the atlas for them to have come from.

**Nothing else changes.** Explicitly preserved:
- All 9 `_beaconIconsByType` primaries, including Chopshop's own
  `1943721698433863534`
- `[5] Event` keeps both alternates — Event variety is intentional and unaffected
- All other alternates rows stay empty
- `_backgroundImage`

**Consequence:** Chopshop always draws its primary icon. It loses visual
variety it never legitimately had, and gains being identifiable — which is
required, because Chopshop is the ONLY place parts can be spent.

---

## 5. Two adjacent observations — NOT changed, flagged for a designer call

Found while dumping the table. Both may be intentional; neither is touched here.

- **`[0] Start` and `[1] Combat` share primary `3851722254969420692`.** A Start
  beacon and a Combat beacon draw identically. Start is born resolved and sits
  at the far left, so context probably disambiguates it — but if it does not,
  it is the same class of defect as this one.
- **`[2] EliteCombat` and `[8] Boss` share `-2954226323514003695`.** Plausibly
  deliberate (boss reuses elite art), and Boss is terminal so position
  disambiguates.

Raising these rather than fixing them: they are art-direction calls, and unlike
the Chopshop row there is no evidence they are accidental.

---

## 6. Verification plan

- EditMode suite green. Baseline **1158 / 1156 / 0 failed / 2 skipped** at
  Unity `cbedd2f`.
- Recompute `pick` for the current save's Chopshops (idx 2, 20, 23) — all three
  must resolve to the Chopshop primary once alternates are empty, because
  `total == 1` short-circuits at `MapBeaconStyleSO.cs:91`.
- **User confirmation is the real gate:** load the 22:42 save and confirm three
  Chopshop icons at 37%/80%-up, 58%/50%-up and 80%/21%-up.

### Regression lock

A test asserting that no two beacon types share an icon in the shipping theme —
`BeaconIcon(type, k)` for every type across many `k` must never return a sprite
that is another type's primary. That would have caught this at authoring time,
and it generalises to the two observations in §5.

Per memory `feedback-prove-test-fails-on-the-bug`, it gets validated by
reverting the asset row and confirming the test fails.

---

## Technical Director Review

Not applicable — this is a two-value asset correction, not a system change. No
code is modified; `MapBeaconStyleSO.BeaconIcon` behaves correctly and needs no
change (with one authored entry, `total == 1` returns the primary directly).

The regression-lock test in §6 is new test surface, not a system refactor.

**Escalate to a TD brief if** the fix is instead to author genuine Chopshop
variant art — that is content work with an art-pipeline dependency, and a
different decision from removing wrong data.

# Capture — The BuildLegacy Cluster (remediation Phase 5.1)

**Date:** 2026-09-11
**System:** CombatView widget construction — bind-or-build fallbacks
**Status:** AWAITING USER APPROVAL. No code written.

## The headline

The plan sized this as "25 occurrences across 11 files". The scout found 9
widgets in 3 shapes. **The TD found 7 shapes and one live dependency**, and
refuted the scout on five points. Sizing below is the TD's, verified.

## Files at risk

`Assets/Scripts/CombatView/` — `AmbushBannerWidget` · `BuffTooltipWidget` ·
`CardWidget` · `DamagePopupWidget` · `EndTurnButton` · `EnergyOrbWidget` ·
`PileCountWidget` · `TurnPhaseWidget` · `BuffStripWidget` · `BuffIconWidget` ·
`DamagePopupSpawner` · `CombatHud` · `CombatSceneBlockout` (comment only) ·
`ResourceBarWidget` (Commit D)
Plus `Assets/Editor/CombatPrefabAuthor.cs` · `tools/ci/grep-gates.sh` ·
new `Assets/Tests/EditMode/CombatView/CombatWidgetPrefabWiring_Test.cs`

## The final-game picture this serves

`CombatPrefabAuthor.cs:23-24` states the invariant outright: the author script's
literals and each widget's `BuildLegacy` literals are maintained so the prefab
and the runtime fallback "produce numerically identical UI." That is **two
copies of the same visual specification** — ADR-0011 forbidden **#2, parallel
storage**, which neither the plan nor the scout brief classified it as.

ADR-0014 P4 requires exhaustive capture-before-destroy of every authored visual
value. Right now that capture must read every value **twice** and reconcile.
This slice halves the capture surface and removes the risk of capturing from the
stale copy — and `EndTurnButton.cs:29-33` proves the second copy has already
drifted in prose. Nobody has ever checked the numbers.

## Every authored value being destroyed

**None.** This is the unusual part and it is load-bearing: the shipping prefabs
already contain the children `BuildLegacy` would build. Deleting the methods
mutates **no serialized data**, so this slice requires **no author run** and must
produce **zero prefab churn**. Any `.prefab` diff means something went wrong.

*(This is why the prefab-drift sentinel does not gate this slice — nothing here
recommends or requires a re-author.)*

What is destroyed is ~350 lines of unreachable C#: 8 `BuildLegacy` bodies, 2
public compat methods, 4 host-side selectors, and the orphaned private helpers
and color constants they alone used.

## The seven shapes

| # | Shape | Site | Found by |
|---|---|---|---|
| 1 | 8 × `if (ref == null) BuildLegacy();` | the 8 widgets | scout |
| 2 | `BuildPoolFromPrefab` / `BuildPoolLegacy` | `DamagePopupSpawner.cs:49-50` | scout |
| 3 | `BuildCardHand` legacy branch | `CombatHud.cs:1503-1545` | scout (range wrong, TD corrected) |
| 4 | `BuildIcon` fork + `WireLegacyRefs` | `BuffStripWidget.cs:133-177`, `BuffIconWidget.cs:72-79` | **TD** |
| 5 | `BuildDamagePopups` | `CombatHud.cs:980-983` | **TD** |
| 6 | `EnsureCrosshair` else-branch | `CombatHud.cs:1274-1291` | **TD** |
| 7 | `BuffTooltipWidget.Spawn` public factory | `BuffTooltipWidget.cs:61-82` | **TD** |

Shapes 4 and 7 are **public API**. Deleting the `BuildLegacy` bodies without
them converts forbidden #3 (bimodal path) into forbidden #6 (stub return) — net
violation count unchanged.

## Dead-at-runtime: proven, with one exception

Verified independently by scout and TD, six ways: source prefabs all wire the
guarded field (positive control — 10 prefabs carry `_background`, all non-zero) ·
**zero** nulling overrides in any prefab or scene (positive control: the same
grep matches `_scrollSpeed` ×5, so it would have matched) · no widget lives in a
scene · no runtime `AddComponent` outside the author script · no
`Resources`/Addressables path loads a widget prefab · no `[ExecuteAlways]` on any
affected type · no test `AddComponent`s any of them.

### THE ONE EXCEPTION — R1, and it is the reason this needed a TD

`CombatPrefabAuthor.cs:2800-2810`. If `BuffIcon.prefab` is absent when
`AuthorBuffStrip` runs, it **logs and continues**, saving `BuffStrip.prefab` with
`_buffIconPrefab` null. The comment says so outright: log+continue exists *"so
BuildLegacy keeps the strip functional."*

- **Today:** wrong author order → ugly-but-working buff icons.
- **After a blind deletion:** wrong author order → `NullReferenceException` in
  `BuffStripWidget.BuildIcon`, on the first status effect of the first combat.

Amendment 1 converts it to a hard abort. **Non-optional.**

## Technical Director Review

**TD-CHANGE-IMPACT: APPROVE WITH AMENDMENTS.** Full verdict in the session
transcript; the load-bearing content is reproduced here.

### Refutations of the scout brief — all five independently re-verified

- **R1** — the `AuthorBuffStrip` log+continue above. Verified verbatim.
- **R2** — six shapes, not three. `BuildIconLegacy` / `WireLegacyRefs` /
  `BuildDamagePopups` / `EnsureCrosshair` all confirmed present.
- **R3** — `BuffTooltipWidget.Spawn` is a 7th site and is `public static`.
  Confirmed.
- **R4** — `ResourceBarWidget` is not a `BuildLegacy` site, it is an **orphan
  class**. Confirmed and extended: its GUID appears **only in its own `.meta`**
  across all of `Assets/` (the TD swept only `Prefabs/` + `Scenes/`), and
  `AuthorResourceBar` is `private static` with **no `[MenuItem]` and no caller**.
  Dead three layers deep. This closes the TD's own open item.
- **R5** — scout missed `_intent: {fileID: 0}` on `CombatHud.prefab:364`.
  Confirmed. Matters for Amendment 4: a naive "every ref non-null" test reds on
  day one, gets weakened, and dies.
- **R6** — the orphan cascade must be hand-audited, NOT ref-counted.
  `EnergyOrbWidget`'s `FullBgColor`/`FullTextColor` are used inside the body
  **and** in `Update` — a ref-count sweep would delete live code. Unused private
  *fields* emit CS0414; unused private *methods* emit nothing, so
  `BuildText`/`BuildLine` sit silently. Do not rely on the compiler.
  **Closed since:** `CardWidget.BuildText`'s four call sites (`:198/211/221/234`)
  are all inside `BuildLegacy` (`:190-242`) — it is orphaned. TD left this open.
- **R7** — `BuildCardHand`'s legacy branch is `:1503-1545`, not the scout's
  `1501-1540`. Cutting the wrong brace deletes controller binding.

### Ordering: correct, but the plan's stated reason is wrong

The plan says "P4 has to delete these anyway, so doing it first makes P4
smaller." **False for Commit A** — P4 deletes the widget classes wholesale; a
74-line file is no harder to delete than a 52-line one. Net saving: zero.

The real reason is the ADR-0011 #2 parallel-storage framing above. The plan's
reason **is** straightforwardly true for Commit C: `CombatHud.cs` is 1,810 lines
and P4 *rewrites* it, so removing ~90 lines of bimodal boot logic from the
rewrite's input is a genuine simplification.

The scout's counter-hypothesis — "deleting the reference implementation makes P4
harder" — is wrong. The author script is the reference implementation P4
migrates from; `BuildLegacy` is the untested, ungated second copy that has
already drifted. A silently-drifted second copy is worse than none, because P4's
capture might read it and believe it.

### Amendments

1. **NON-OPTIONAL** — `AuthorBuffStrip` log+continue → hard abort before
   `SaveAsPrefabAsset`. Rewrite the message (the "will fall back to legacy"
   sentence becomes a lie). Check the cited `BuildSlotColumn` precedent for the
   same shape.
2. **NON-OPTIONAL** — delete `BuffTooltipWidget.Spawn` and
   `BuffIconWidget.WireLegacyRefs` in the same commit as their bodies.
3. **NON-OPTIONAL** — hand-audit the orphan cascade per R6. Build must be
   CS0414-clean in `CombatView/`.
4. **NON-OPTIONAL** — add `CombatWidgetPrefabWiring_Test.cs`, **table-driven**
   over `(prefabPath, componentType, fieldName)`. Reuse
   `PostCombatRewardChainAuthoring_Test`'s dormant-instantiate + reflected-read
   pattern. Assert at the level the ref lives. **Allow-list `_intent` and
   `_crosshairPrefab`** with the reason in a comment. Failure message must name
   the fix. `OnValidate` rejected as the guard: editor-only, can't throw,
   doesn't run headlessly.
5. **NON-OPTIONAL** — grep gates on `BuildLegacy` / `BuildIconLegacy` /
   `BuildPoolLegacy` / `WireLegacyRefs`, scoped to `Assets/Scripts/`. Note the
   gate's `///` filter will NOT catch `CombatSceneBlockout.cs:27` — scrub by hand.
6. **OPTIONAL (recommended, Commit D)** — delete `ResourceBarWidget` whole. Both
   TD prerequisites now discharged (see R4).
7. **NON-OPTIONAL (process)** — this file.
8. **OPTIONAL** — record the #2 parallel-storage reclassification in plan §5.1.
9. **NON-OPTIONAL — replaces and generalises Amendment 1.** No author method may
   save an asset with a required reference left unwired. Applies to **five**
   sites in `CombatPrefabAuthor.cs`: `:2800-2810` (BuffIcon), `:7945` (GameOverView
   .uxml), `:7947` (PanelSettings), `:7949` (GameOverPanelSettings), `:7974`
   (Vehicle_Scout). Each becomes `LogError` + `return` **before**
   `SaveAsPrefabAsset` / `ApplyModifiedPropertiesWithoutUndo`. Rewrite every
   message — all promise a runtime fallback; after Commit A one promise is false
   and the rest are unwanted. **Delete the `BuildSlotColumn` citation outright,
   do not reword it.** Per the codify-principle-then-applications preference,
   state the rule ONCE at the top of the file and have the five sites reference
   it rather than repeating the rationale five times.
10. **NON-OPTIONAL — scope constraint on Amendment 5.** Gate **only**
    `BuildLegacy|BuildIconLegacy|BuildPoolLegacy|WireLegacyRefs`, scoped to
    `Assets/Scripts/`. Do **NOT** generalise to `Legacy` or `Fallback`:
    `BuildScout` must survive until §5.4, and `RunSceneHost.cs:1465-1466,
    1533-1536` plus `ScoutFallback_PartIdParity_Test` are full of those words. A
    generic pattern reds the suite and teaches everyone to weaken the gate —
    which is the failure mode the `BuildSlotColumn` comment demonstrates at the
    doc level.
11. **NON-OPTIONAL — free win.** Add `(Run.prefab, RunSceneHost,
    _playerVehicleAsset)` as a row in Amendment 4's table. Converts the §5.4
    deferral's safety from "is it wired?" into a headless assertion, so the
    fallback cannot silently go live before the retirement slice.
12. **NON-OPTIONAL — Phase 4.1 miss, found during this slice.**
    `CombatPrefabAuthor.cs:7953-7954` claims the binding SO carries "all 7
    runtime-visited BeaconType entries (Start excluded)". There are **8**
    non-Start types and 7 are bound — the comment asserts an exhaustiveness that
    Phase 4.1 deliberately retired. Yesterday's A5 prose sweep missed it because
    the grep was keyed on "Haven"/stub/binding tokens and this line contains
    none. Fix in Commit A's prose pass.

## Follow-up verdict (same TD, second round) — `BuildScout`

### The scout's "8th shape" claim: RETRACTED, and the TD concurred

`BuildScout` is **not** the widget cluster's shape. It is the EditMode suite's
vehicle factory living in production code: `RunSceneHost.cs:667-670` records that
**20 fixtures** depend on the unwired condition, and
`ScoutFallback_PartIdParity_Test:157-168` reflects in and asserts the method
exists. It cannot be deleted — it has to be **relocated**.

**A correction to the scout, verified:** the claim that `:664`/`:683` "branch
downstream on whether the fallback is active" is **wrong**. Those lines are a
**severity selector on a diagnostic** — both arms only log (`LogError` vs
`LogWarning`) and `StartRun` runs identically either way. The genuine value
divergences are three, all verified, and the scout named none of them:

| Site | Diverges on |
|---|---|
| `RunSceneHost.cs:653-656` | `ChassisCards` vs `RunDeck.Milestone1Starter()` |
| `RunSceneHost.cs:1488-1489` | `TankCapacity` vs `SCOUT_TANK_CAPACITY_FALLBACK = 35` |
| `RunSceneHost.cs:1528-1529` | `FuelBurnMultiplier` vs `SCOUT_FUEL_BURN_MULTIPLIER_FALLBACK = 0.7f` |

### Ruling: defer to a named **§5.4**, contain now

Out of 5.1. Four reasons: different violation category (relocation, not
demolition) · "fix one, sweep all" is discharged by the *sweep*, and Commit A
wakes nothing in `RunSceneHost` · folding it in would **destroy 5.1's acceptance
signal**, since a green suite can no longer prove the deletions were safe if the
suite was edited in the same breath · no ordering pressure, `RunSceneHost` is not
P4 scope.

**Take the containment half now** (Amendment 9's `:7974` hard abort) — three
lines, no runtime change, prevents the wired→unwired transition ever being
authored. Record §5.4 by name in the plan; an unnamed "we noticed this" is
exactly how `BuildSlotColumn` happened.

### `:7949` is the most dangerous of the five, and the scout's discriminator was wrong

Not appearance-vs-behaviour. The right discriminator is **does the degradation
announce itself?**

| Site | Degradation | Announces itself? |
|---|---|---|
| `:7974` Vehicle_Scout | Wrong deck, tank, burn rate | **Yes** — warning + unwinnable deck |
| `:2803` BuffIcon | Post-Commit-A: NRE on first status effect | Yes, at the worst moment |
| `:7949` GameOverPanelSettings | Overlay renders **below** MapView | **No** |

A wrong `sortingOrder` is silent: the game-over overlay renders behind the map,
leaving a possibly-invisible modal eating input — the pending-offer-gate failure
class, a state that reads as a hang rather than an error. And TD 2026-07-24
AMEND A4 makes `sortingOrder = 100` a **requirement**, so the author tool is
knowingly shipping a violation of an accepted amendment and logging about it.

### The `BuildSlotColumn` citation manufactured authority

A dangling citation is worse than none: `:2804` is *why* the log+continue looked
sanctioned, and very likely why the shape was copied to the other four sites.
Treat all five as **one lineage descending from a claim that was never true** —
which reframes the remedy from "patch five call sites" to "the pattern was never
justified." That is what Amendment 9 encodes.

### Commit shape: unchanged. Commit D promoted OPTIONAL → RECOMMENDED

Both TD prerequisites were discharged by the scout's sweep (zero project-wide
references; `AuthorResourceBar` has no `[MenuItem]` and no caller). A→B→C
ordering unchanged and reinforced — Amendment 9's containment belongs in Commit
A, which already touches `CombatPrefabAuthor.cs`, before anything can author a
null.

### Three-lens self-audit (TD, returned unprompted)

**Health:** six shapes not three, plus a 7th public-API stub; two would have
survived a name-based sweep and silently converted #3 into #6. One real
duplication delta — `BuildText`/`BuildLine` recur in five widgets, but do **not**
extract a helper: all but `CardWidget`'s are being deleted. Deletion, not
abstraction. **Optimization:** no meaningful delta, and none claimed — 9 null
checks once per instantiation. One small real win collapsing the per-rebuild
branch in `BuffStripWidget.BuildIcon`; call it correctness, not performance. Do
not speculatively restructure `CombatHud` boot order in C. **1.0 survival:**
deletions survive trivially. **Commit C partially does not** — those ~90 lines
are lines P4 would remove anyway; its value is shrinking P4's input, and it
should not be scored as durable 1.0 work. The durable artifact is Amendment 4's
test, which is why it must be a **table** — P4 deletes half the rows and adds UI
Toolkit rows, and a table absorbs that as data edits. The `_crosshairPrefab`
allow-list entry is genuinely temporary (gone at Commit C) and must say so; the
`_intent` entry is **permanent** and encodes a real architectural fact.

## Commit shape — four commits, split by blast radius not by name

| Commit | Scope |
|---|---|
| **A** | 8 widget bodies + `Spawn` + `WireLegacyRefs` + `BuildIcon` fork + orphan cascade + stale comments + Amendment 1 |
| **B** | The wiring guard — Amendment 4 test + Amendment 5 gates |
| **C** | `CombatHud` host selectors: `BuildDamagePopups`, `EnsureCrosshair`, `BuildCardHand`, `EnsureSiblingCanvas`/`EnsurePopupsGroup` |
| **D** | `ResourceBarWidget` orphan removal (optional, deferrable, order-free) |

**Order is load-bearing:** B must fall between A and C — C is the first change
that could plausibly *introduce* an unwired ref (it retires `_crosshairPrefab`),
so the guard must already be in place, and it is proven red-on-bug against A's
surface where the answer is known. A must precede C so the host's bimodal
bootstrap is edited after its leaves are single-path. D is order-free.

## Acceptance criteria (headless; none diffs author output against scenes)

1. `grep -rnE "BuildLegacy|BuildIconLegacy|BuildPoolLegacy|WireLegacyRefs" Assets/Scripts/` → zero.
2. `bash tools/ci/grep-gates.sh` exits 0 clean.
3. **Gate proven red:** re-add a `BuildLegacy` stub → exits 1 and names it. Revert.
4. `grep -cE 'error CS' TestResults/editmode.log` → 0.
5. Zero **CS0414** in `CombatView/`.
6. EditMode `1285 + N` / `1284 + N` / 0 failed / 1 skipped.
7. **New test proven red:** null `Card.prefab` `_background` → test fails with
   the "re-run Author…" message; restore → green.
8. **`git status` shows no `.prefab` / `.unity` churn.** Any prefab diff means
   the author tool ran and the slice's premise slipped.
9. PlayMode 16/16.
10. **BLOCKING playtest** (TD raised this above the standards table's ADVISORY
    tier for UI, because the slice's whole premise is runtime behaviour): one
    combat exercising every deleted fallback's live counterpart — status effect
    applied, buff icon hovered, attack card played, turn ended, run resolved.
    No NRE in the log.

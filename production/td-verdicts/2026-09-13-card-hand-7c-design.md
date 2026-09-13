# TD Verdict — ADR-0018 P4a slice 7c: card hand design

**Date:** 2026-09-13
**Topic:** Migrating the card hand to UI Toolkit — where the pipeline lives,
coroutines, z-order, `DraftCardElement` duplication, panel placement, slice size
**Reviewers:** `technical-director` (agentId `a4cb6c9bf388e0051`) +
`unity-ui-specialist` (agentId `a16f6be8268479967`), dispatched in parallel
before any code was written

**Files touched:** `Assets/Scripts/Combat/CardDefinition.cs`,
`Assets/Scripts/CombatView/CardWidget.cs`,
`Assets/Scripts/CombatView/HandLayoutEngine.cs`,
`Assets/Scripts/CombatView/HandBeat.cs`,
`Assets/Scripts/CombatView/HandSequencer.cs`,
`Assets/Scripts/CombatView/CombatHud.cs`,
`Assets/Scripts/UI/CombatHudPanelController.cs`,
`Assets/Scripts/UI/DraftCardElement.cs`, `Assets/UI/DraftCardElement.uss`,
`Assets/UI/Tokens/tokens.spacing.uss`, `Assets/UI/CombatHudPanel.uxml` + `.uss`

## Technical Director Review

**Verdict: AMEND.** Direction right — option (A) move the pipeline, one panel,
keep coroutines — but 7c must *fix* an invariant it was planning to preserve, and
one verified defect would have silently destroyed a designer bake.

### THE HAZARD — verified, and worse than reported

`Assets/UI/Tokens/tokens.spacing.uss:12-15`:

```css
/* Card footprint — shared by DraftCardElement (slice 7a) and the future
   hand card UXML (ADR-0014 P4). Matches the live Card.prefab sizeDelta. */
--wr-size-card-width:  160px;
--wr-size-card-height: 270px;
```

`Card.prefab` is **208 × 351** (`CombatPrefabAuthor.cs:1852`). **The comment
asserts a match that does not exist.** Those are the *pre-scale* values from
before the 2026-06-04 designer bake (*"scaled 1.3x from authored 160×270 to
208×351 per designer request (1.5x was too large)"*).

Had 7c consumed these tokens as documented, the hand card would have silently
shrunk 23%, every percentage-anchored band would have re-laid out against a
different box, and **there would have been no compile error**.

**Resolution: split the tokens**, `--wr-size-card-*` (draft, 160×270) and
`--wr-size-handcard-*` (hand, 208×351). This preserves both surfaces exactly as
they render today. Unifying them would be a visual design pass, not a migration,
and is the user's call to make separately. Three cards on an empty overlay and
eight across a live combat have different screen budgets — forcing one number is
how the 1.3× bake got lost in the first place. ADR-0015 configuration narrowing,
not duplication.

### Verified drift #2 — invariant Z3 is already false

`HandLayoutEngine.cs:12-13` claims *"Single writer of `SetSiblingIndex` for hand
widgets (invariant Z3)"*. `CardWidget.cs:555` calls `transform.SetAsLastSibling()`
**raw**, bypassing the engine, on every drag begin.

(The TD counted three writers; verified as two, one raw — `CardWidget.cs:379`
calls *through* the engine and is a caller, not a second writer. The substantive
finding is unaffected: the single-writer claim is false.)

7c1 makes it true for the first time by routing the drag-begin raise through one
owner.

### Verified drift #3 — three more false claims around `DraftCardElement`

- `DraftCardElement.cs:35` — `<see cref="WastelandRun.CombatView.CardOffer"/>`.
  **No such type.** Every `CardOffer` in the repo is `WastelandRun.Run.CardOffer`,
  the reward record. Identical phantom to the one 7a deleted from `RenderStatic`;
  that sweep was single-file.
- `DraftCardElement.uss:11` — repeats the same phantom.
- `DraftCardElement.uss:25` — *"HoverLiftPx 18 — picker hover lift (NOT 24 —
  combat hand uses 24)."* Self-contradictory in one line, and **both numbers are
  wrong**: the hand uses **100** (`CardWidget.cs:46`), which is load-bearing
  because it exactly cancels `IdleDropOffsetY = -100`. A designer "matching the
  hand" off this line breaks the arc.

### Q1 — Pipeline lives in `WastelandRun.UI`, but `HandLayoutEngine` splits first

Option (B) (leave the pipeline in CombatView naming the new UI type) is
**disqualified by a shipped-bug mechanism, not taste**: `CombatHud.HandleOverlayShown`
calls `gameObject.SetActive(false)` and the panel is its child, so every overlay
cycle re-clones the `UIDocument` tree. Under (B), `CombatHud._cards` would hold
`VisualElement` references *across* that cycle — the hand is dead after the first
reward picker. That is `feedback_uidocument_setactive_reclone` verbatim.

`HandLayoutEngine` sheds three methods before travelling: `ReassignWidgetsToCards`
/ `ApplyZOrder` / `ApplyZOrderForWidget` are **element operations wearing a math
class's name** and belong on the hand view. `ComputeSlotTransform` ×2 and
`IndexOfCard` are pure math and move intact. Moving all five together would
relocate the circular dependency rather than remove it.

### Q2 — Coroutines stay, hosted on `CombatHudPanelController`

USS transitions cannot carry `DrawOne`: `AnimateFromDeck` recomputes its target
every frame from live `Hand.Count` ("the hand makes room" as later cards land),
and a transition has a fixed endpoint. The controller already has `Update()` and
already drives per-frame animation (`TickCrosshair`, `PileChipView.Tick`).

**Do not narrow `MonoBehaviour runner`** to the controller type — it genuinely
only needs `StartCoroutine`, and the existing null/`isActiveAndEnabled` guards are
correct. Change the instance, not the signature.

**Latent defect to close:** Unity silently kills coroutines on a disabled
MonoBehaviour but `HandSequencer._coroutine` stays non-null, and `EnsureRunning`
early-returns on non-null. Add `Stop()` to the controller's `OnDisable` or a
mid-combat overlay leaves the hand permanently deaf.

### Q4 — Unify the text, keep the elements separate

**Unify:** `BuildInfoText` is behaviourally identical in both
(`DraftCardElement.cs:180-203` vs `CardWidget.cs:416-451`, differing only in
`""` vs `string.Empty`). Pure function of `CardDefinition`, no loop — it moves to
`CardDefinition`, exactly as 7b moved `RequiresTarget` for the same reason. The
drift risk is documented precedent, not hypothesis: `CardWidget.cs:706-717`
records a hand-rolled copy of the playability rule drifting and shipping
Weld-lit-while-Engine-offline.

`BuildValueText` splits: the branch *order* (attack > plate > repair > draw) is
shared and is what would silently miss a new effect type, so
`CardDefinition.BaseValueText` is shared; the projection colouring is combat-only
and its absence in the draft is deliberately reasoned.

**Do NOT unify the elements.** The draft card is a flex-row child with a USS
`:hover` and zero positioning code. The hand card is absolutely positioned on a
rotated arc, pointer-captured, z-reordered, pooled across 8 slots,
coroutine-animated. A unified element needs a mode flag gating positioning, drag
and hover — **ADR-0011 forbidden pattern #3 (bimodal path), created by the
unification.** Shared lineage belongs in USS tokens.

### Q5 — One tree, hand layer between the chips and the crosshair

The crosshair is already in this tree and must paint over the hand during a cast
(`HideVisual` exists for exactly that); in one tree that is "the crosshair is a
later child", for free. `ShowPilePopup` calls `BringToFront()` so nothing built
later paints over it — a separate hand document defeats that. And one tree means
the pointer arrives already in panel space, which is what slice 6 predicted would
retire its `ScreenToPanel` hop.

**Requirement this creates:** the UGUI hand's GameObjects survive a `SetActive`
cycle; migrated elements do not — they are destroyed and re-cloned, losing every
`_currentCard` binding. `CombatHud.CatchUpToHand()` already exists and is already
correct ("a state-recovery snap, not a draw beat") and **must be called from the
controller's `OnEnable` after the re-query**. Miss it and the symptom is "return
from a reward overlay, hand is blank."

### Q6 — 7c is too big. Split.

**7c1 — prepare.** Zero visual change, compile-enforced, playable throughout:
z-order single-writer made true; `BuildInfoText` → `CardDefinition`;
`BaseValueText` shared; the four false claims fixed; card-size tokens split.

**7c2 — migrate.** One risk class: `CardHandElement` + UXML + USS, pipeline move,
drag rework, `OnEnable` catch-up, `OnDisable` `Stop()`, re-homed `IsHandAnimating`
and chip anchors.

**Explicitly rejected: an `IHandCard` seam in 7c1.** Every member already matches
implicitly so it would be cheap — but it has an expiry date (one implementer,
same assembly, after 7c2), making it slice-4-callback-shaped rather than
`ICardCastService`-shaped. Retype `CardWidget[]` → `CardHandElement[]` in 7c2
instead.

### On deferring the hand entirely

Considered seriously, rejected. ADR-0018 §2 names `CardHand` as P4 scope and
explicitly *not* a registry member, so deferring forces either an unshippable P5
or a registry admission whose honest exit criterion is "we didn't want to" — the
rot the ADR exists to prevent, burning one of five slots. And the cast gesture is
currently split across two stacks: all-UGUI and all-UIT are both stable, the
split is not, and stopping here freezes it.

**But the designer-workflow concern gets a real answer, as an acceptance
condition of 7c2:** every one of the constants in the capture's inventory lands
either in USS or as a documented field on the controller, **with its rationale
comment carried verbatim** — and the two coupled pairs
(`HoverLiftPx`↔`IdleDropOffsetY`, engage 120↔commit 200) land adjacent in one
file with the "move together" note intact.

## UI specialist findings (agentId `a16f6be8268479967`)

Substantive content sound. **One citation fabricated** —
`CombatHudPanelController.cs:1081-1097 ResolveLayersIfPossible` does not exist
(the file is 1016 lines, no such method). The advice it supported (re-query on
`OnEnable`, never cache across a re-clone) is correct and is what the file does;
the evidence was invented. Other spot-checked citations verified real.

- **A SECOND Y-flip, distinct from the pointer one already on the seam.**
  `HandLayoutEngine.ComputeSlotTransform` emits Y-**up** (UGUI: `yArc`,
  `IdleDropOffsetY -100`, `HoverLiftPx +100`); `style.top` / `translate.y` are
  Y-**down**. Fixing the pointer flip and missing this one is a silent mirrored
  arc.
- `style.translate`/`rotate` are composited (repaint only); `left`/`top`
  invalidate layout. Use `left`/`top` on reflow, `translate` per frame.
- `hierarchy.Insert(index, element)` is the `SetSiblingIndex` equivalent.
- Drag: follow `MapViewController.cs:1129-1175` — capture on threshold, not on
  down; `PointerCaptureOutEvent` is the single source of truth for "drag ended,
  possibly forcibly", covering window-focus-loss which UGUI's `IEndDragHandler`
  handled implicitly.
- `_suppressNextClick` **disappears** rather than ports: a plain `VisualElement`
  has no `Clickable`, so no `ClickEvent` is synthesized to suppress.
- `RuntimePanelUtils.PanelToScreen` is the exact inverse of `ScreenToPanel`.
  Never hand-roll the flip.
- `visibility`, not `display:none`, for hiding mid-drag (capture orphaning +
  `resolvedStyle` zeroing).
- Guard per-frame `Label.text` writes behind an equality check, and move
  `GetFamilySprite`'s per-frame sprite write to `AssignCard`.

Flagged by the specialist as **unverified, do not treat as fact**:
absolute-position reordering being layout-free in this build, `usageHints`
availability in 6.3, `IPointerEvent.originalMousePosition`'s space, whether
`display:none` releases pointer capture, `experimental.animation` stability.

## My own finding, which removes one of those risks

The specialist warned that `BuildValueText` emits a TMP tag
`<color=#5DFB5D>{projected}</color>` whose UI Toolkit support is unverified, and
the repo has **zero** rich-text prior art in any UIT label (all `<b>` hits are XML
doc comments).

The tag wraps the **entire** returned string — it is a whole-element colour.
That is exactly the `is-damage` / `is-blue` / `is-dim` USS-class pattern already
shipped in `PilePopupRowElement` in slice 5. **7c2 drops rich text entirely:**
three classes (none / up / down), no unverified dependency, and it matches an
established in-project pattern instead of introducing a second one.

## Three-lens self-audit

Both agents self-audited; the material points are folded above. The one I am
carrying forward as a standing risk rather than a solved item:
`CombatHudPanelController` is ~1016 lines and 7c2 adds the hand pool, the z-order
writer and a coroutine host. If it exceeds ~1400 lines after 7c2, extract a
`CardHandView` sub-object the way `PileChipView` was extracted — the house
pattern — rather than letting it sprawl.

**We'll know this was right if:** after 7c2,
`grep -n "SetSiblingIndex\|BringToFront\|SendToBack" Assets/Scripts/UI/` shows
writes from exactly one file; the hand renders at **208×351**, not 160×270;
returning from the part-reward picker mid-run shows a populated hand; and
`ICardCastService.cs` is unchanged since 7b.

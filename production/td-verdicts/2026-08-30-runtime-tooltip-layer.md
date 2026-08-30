# TD Verdict — runtime tooltip layer

**Date:** 2026-08-30
**Scope:** Extract the garage's `TooltipEvent` handler into a shared
`Assets/Scripts/UI/TooltipLayer.cs`, and wire the four screens whose tooltips
have never rendered.
**Verdict:** ACCEPT-WITH-CONDITIONS

---

## 1. Why this exists at all

UI Toolkit's `VisualElement.tooltip` is drawn by the **Editor's** tooltip
system. A runtime panel renders nothing for it. This project carries **20
`tooltip=` attributes across five UXML files and had ZERO `TooltipEvent`
handlers**, so no tooltip in this game has ever been visible to a player —
including the "(soon)" hints that are the only thing telling someone a greyed
choice is not broken.

`TooltipEvent` does fire at runtime. A listener was built inside `GarageBinding`
and shipped in Unity `0d44f15`.

## 2. THE BLOCKING FINDING — the shipped implementation is half broken

`GarageBinding.HandleTooltip` reads the tooltip off the picked element:

```csharp
string text = (evt.target as VisualElement)?.tooltip;
```

`evt.target` is the DEEPEST element under the cursor. That works only when the
tooltip owner has no pickable children.

**Verified against the shipping UXML, not taken on the TD's word:**

| Owner | Shape | Works today? |
|---|---|---|
| `btn-garage-back`, `btn-garage-repair` | self-closing `<ui:Button … />` | ✅ target IS the owner |
| `#tile-mount`, `#tile-hp`, `#tile-armor` | leaf `Label`s carrying their own tooltip | ✅ |
| `choice-1` … `choice-3` (`ChopshopScreen.uxml:126-152`) | Button **wrapping** `choice-N-label` | ❌ the Label is picked, has no tooltip |
| Rest / Merchant "(soon)" choices | same wrapping shape | ❌ |
| Storage-tile **card chips** | `chip.tooltip` on a container wrapping two Labels | ❌ |

So the two cases demonstrated as working are exactly the two that happen to be
childless, and **every "Coming soon" tooltip — the highest-value content — is
silently dead.** A playtest of the garage buttons passes over it.

**Ruling: resolve by ANCESTOR WALK from `evt.target` upward**, taking the anchor
rect from the OWNING element rather than `evt.rect`. That also drops a
dependence on `evt.rect` being populated during trickle-down, which is
engine-internal behaviour.

**Do not switch to bubble-up or to per-element registration.** The engine's own
bubble-up handler calls `StopImmediatePropagation()` when it finds a tooltip, so
a root bubble-up listener never fires. And the "(soon)" buttons are
`SetEnabled(false)`; a root **trickle-down** listener still runs because the root
is enabled, whereas per-element registration on a disabled button would not.
Disabled-button tooltips are most of the payload.

## 3. Rulings

**R1 — New public type CONFIRMED; `PeekOverlayBinding` correctly rejected.**
Three independent reasons: peek is content-BLIND with six named slots while
tooltips are content-GENERIC with none; the Chopshop's layer must serve the
GARAGE MODAL, which peek has no relationship to; and future consumers
(`MapView`, `RunHUD`, the ADR-0014 P4 combat HUD) have no peek overlay at all.
Also rejected, so they are not re-raised: a MonoBehaviour on the beacon root
(fails the composition smell-test, buys nothing serialisable) and a scene-wide
singleton (each beacon PrefabRoot owns its own `UIDocument` and therefore its own
panel — a cross-panel tooltip cannot position itself; per-screen is a constraint,
not a preference).

Public surface is `Bind` / `Unbind` / `Hide` only. `Show(text, rect)` stays
private so the payload shape is not locked.

**R2 — Lifecycle: `Bind` in OnEnable, `Unbind` in OnDisable, and `Unbind` must
`UnregisterCallback` AND `RemoveFromHierarchy`.**
These are `BeaconActivator` SetActive-toggled roots and the `UIDocument`
re-clones its tree per cycle. Bind/OnDestroy instead would work on visit 1 and
silently stop for the rest of the run — the failure a single playtest passes.
`GarageBinding.Unbind` today nulls `_screenRoot` but never unregisters; it
survives only because the tree is garbage anyway. **That pattern must not
propagate.**

`DialogueSceneController` is the exception: bind inside its `EnsureCached()`,
not `OnEnable`, or it binds against a tree that has not been cloned yet.

**R3 — Estimate-based placement REJECTED. Measure.**
Not perf — correctness. `TooltipHeightEstimate = 44f` decides the flip-above
branch, and the longest string wraps to 2-3 lines against `max-width: 260px`, so
the flip case is misplaced by up to two lines exactly where it flips: the bottom
of the screen, where the storage strip and the choice rail live. The clamps do
not absorb that, they cause it. Place relative to the layer's PARENT
(`parent.WorldToLocal`), not the panel.

**R4 — Create the layer in CODE; do not add `#tooltip-layer` to any UXML, and
delete it from `GarageScreen.uxml`.**
A missing UXML element is a `Q<>` returning null — a SILENT no-op on one screen,
and five hand-maintained copies is five chances at it. The element is invisible
and entirely code-positioned, so a designer opening it in UI Builder learns
nothing. The designable surface is `.wr-tooltip`, which stays in USS. Add to
`docRoot` rather than `#root` so it paints above `#modal-container`,
`#budget-bar` and `#peek-overlay` without depending on child ordering.

Consequence taken deliberately: one Chopshop-level layer serves the chopshop
chrome, the garage modal and the peek overlay. Both containers are full-panel
absolute, so world coordinates line up.

**R5 — ADR-0011 gate.** `GarageBinding`'s copy dies in the SAME commit.
`grep -rn "RegisterCallback<TooltipEvent>" Assets/` must return exactly ONE hit
afterwards.

## 4. Findings not asked about

- **Hide when the pointer leaves the panel.** `TooltipEvent` fires on hovered-
  element CHANGE; exiting the panel produces no new element, no event, and the
  tooltip stays painted. Register `PointerLeaveEvent` on `docRoot`. **This bug
  exists in the garage today.**
- **`BuffTooltipWidget`** is a second, UGUI tooltip implementation. Not drift
  today (different stack; ADR-0014 permits UGUI in combat until P4) but record
  `TooltipLayer` as its convergence target so P4 does not produce a third.
- **`.is-hidden { display: none }` is duplicated** across the screen stylesheets.
  Pre-existing; explicitly NOT this slice.
- **Non-goal guard:** add no new `tooltip=` attributes here. Twenty exist; make
  those render; stop.
- **Fact correction to the brief:** `DialogueSceneController` lives in
  `WastelandRun.UI`, not `CombatView`. Three of the four hosts are CombatView.

## 5. Implementation order

1. `TooltipLayer.cs` — ancestor walk, owner-anchored rect, parent-relative
   placement, measured size, `PointerLeaveEvent` hide, `Unbind` that
   unregisters + removes from hierarchy.
2. Move `.wr-tooltip` from `GarageScreen.uss` to `controls.uss` (already loaded
   by all four screens — verified).
3. `TooltipLayer_Test.cs` BEFORE wiring hosts. EditMode cannot dispatch a real
   `TooltipEvent` without a live panel, so expose resolution as
   `internal static bool TryResolveTooltip(...)` and test against CLONED
   SHIPPING UXML, the doctrine `GarageBinding_Test` already follows. Required
   assertion: resolving from `#choice-1-label` yields
   `"Coming soon (Parts axis)"`. Per `feedback_prove_test_fails_on_the_bug`,
   swap the walk back to `from.tooltip` and confirm it REDS before keeping it.
4. Wire `ChopshopWorkbenchController`; delete `GarageBinding`'s copy in the same
   commit; run the R5 grep.
5. Wire `MerchantSceneController` and `RestSceneController`.
6. Wire `DialogueSceneController` — bind inside `EnsureCached()`.
7. Playtest. The case that proves it: **hover a greyed "(soon)" choice** — the
   one that fails today. Then move the cursor off the game view and confirm the
   tooltip clears.

## 6. Files at risk

**Unity** — `C:\ClaudeCreations\GameStudio\Madmax Rougelike\Wasteland Run\`

New:
- `Assets\Scripts\UI\TooltipLayer.cs`
- `Assets\Tests\EditMode\UI\TooltipLayer_Test.cs`

Edited:
- `Assets\Scripts\UI\GarageBinding.cs` — delete `_tooltip`, the
  `RegisterCallback<TooltipEvent>`, `HandleTooltip`, both estimate consts, the
  `Q<Label>("tooltip-layer")`, the Unbind null
- `Assets\UI\GarageScreen.uxml` — delete the `#tooltip-layer` Label
- `Assets\UI\GarageScreen.uss` — delete `.wr-tooltip`
- `Assets\UI\controls.uss` — add `.wr-tooltip`
- `Assets\Scripts\CombatView\ChopshopWorkbenchController.cs`
- `Assets\Scripts\CombatView\MerchantSceneController.cs`
- `Assets\Scripts\CombatView\RestSceneController.cs`
- `Assets\Scripts\UI\DialogueSceneController.cs`

**NOT edited** (the payoff of R4): `ChopshopScreen.uxml`, `MerchantScreen.uxml`,
`RestScreen.uxml`, `DialogueScene.uxml`.

**Framework:** this file.

## 7. Success criteria

(a) Hovering a disabled "(soon)" choice on Chopshop, Rest and Merchant shows
text — the case that fails today. (b) The R5 grep returns exactly one hit.
(c) Tooltips still render on the SECOND and THIRD beacon visit of a run, not
just the first. (d) The next screen that wants tooltips adopts it with one field
and two lines, no signature change.

---

## Technical Director Review

ACCEPT-WITH-CONDITIONS, conditional on the ancestor walk (§2), the
unregister-on-Unbind lifecycle (R2), measured placement (R3), and
`GarageBinding`'s copy dying in the same commit (R5).

**Three-lens self-audit.**

*Codebase health.* The slice removes a duplication that was about to become
five copies, and gives `TooltipLayer` a real owner rather than a bolt-on.
`ChopshopWorkbenchController` gains a third owned binding but no new
responsibility — same shape as `_peek` and `_garage`. The lifecycle defect in
the current `Unbind` is named so it does not propagate. Extraction at five call
sites is late, not premature.

*Optimization.* No new per-frame work; `TooltipEvent` fires on hovered-element
change, and an `Update` poll is explicitly forbidden. The ancestor walk is a
`for` over ~5 parents with no LINQ, closure or boxing. One layer per `Bind`, not
per hover — a hover is a human gesture, the same reasoning the card preview
already follows. No per-element cache: the walk is cheaper than the dictionary
that would replace it.

*1.0 survival.* `Bind`/`Unbind`/`Hide` survives unchanged; `MapView` beacon
hovers are the obvious next consumer. Named risk: 1.0 will likely want RICH
tooltips (title + body, keyword highlight, cost icon) and a `Label` can only
hold plain text — but `Show` is private, so a later slice swaps the internal
`Label` for a container behind the same seam with no public change. The
"(soon)" strings are throwaway content that dies when Forge and Upgrade ship;
that argues FOR the layer, which must outlive its highest-value current content.

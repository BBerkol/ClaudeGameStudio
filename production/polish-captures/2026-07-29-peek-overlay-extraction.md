# Peek Overlay Extraction — Capture

**Date:** 2026-07-30
**System:** UI / dialogue-first narrative surfaces
**Slice:** Refactor (extraction) — pre-Rest-overhaul cleanup
**Scope:** ~180 lines rewritten across 2 controllers + new ~90-line shared helper

## Motivation

`MerchantSceneController.cs` line 583 carries an in-code TD directive from the
2026-07-30 merchant slice review:

> "TD review 2026-07-30 flagged this as the third inline copy — extract to a
> shared PeekOverlayController BEFORE Rest peek buttons land."

Rest UI overhaul is queued immediately after this slice (memory
`project_rest_ui_overhaul_pending.md`). Extracting the peek plumbing NOW means
Rest inherits the clean helper for free; extracting AFTER Rest lands would ship
a third inline copy just to delete it in the same commit — the exact ADR-0011
"third caller drift" trap.

## Duplication being destroyed

The Show/Hide/Wire/Unwire plumbing for the `.wr-dialogue-peek-*` overlay
appears verbatim in two controllers:

| Piece | DialogueSceneController | MerchantSceneController |
|-------|-------------------------|-------------------------|
| 6 VisualElement/Button field decls | lines 76-81 | lines 128-133 |
| `PeekOverlayIsHiddenClass` const | line 55 | line 127 |
| 6 `Q<>()` cache queries | lines 394-399 | lines 198-203 |
| Button wire/unwire | lines 426-450 | lines 259-262 / 291-293 |
| `ShowPeek(string, VisualElement)` | lines 206-218 | lines 603-611 |
| `HidePeek()` | lines 225-230 | lines 613-618 |
| Start-hidden + clear-on-teardown | lines 421-423 / 272-275 | lines 275-279 / 302-305 |

Content half (`PeekContentBuilder.BuildMap` / `BuildDeck`) is already extracted
in `Assets/Scripts/CombatView/PeekContentBuilder.cs` — only Show/Hide plumbing
is duplicated.

## Extracted target

New file: `Assets/Scripts/UI/PeekOverlayBinding.cs` (namespace `WastelandRun.UI`).
Pure C# helper (not `MonoBehaviour`) — peek elements live inside each host's
UIDocument tree, so a second MB would demand designer prefab-authoring for
zero payoff. Composition smell-test: a designer would NOT expect a
`PeekOverlay` MB on the beacon root.

Asmdef arrow confirmed clean: `WastelandRun.CombatView.asmdef` line 9 already
references `WastelandRun.UI`, so `MerchantSceneController` (in
`WastelandRun.CombatView`) can consume `WastelandRun.UI.PeekOverlayBinding`.

### Public surface

```csharp
public sealed class PeekOverlayBinding
{
    public bool IsVisible { get; }
    public bool IsAvailable { get; }        // all 6 UXML slots resolved

    public void Bind(VisualElement docRoot); // Q<>() 6 slots + wire clicks + start-hidden
    public void SetHandlers(Action onMap, Action onDeck); // stores + SetEnabled refresh
    public void Show(string title, VisualElement content);
    public void Hide();
    public void Unbind();                    // unwire + null all + clear callbacks
}
```

### TD MODIFY deltas applied

1. **Enabled-state includes IsAvailable:** `SetEnabled` for peek buttons must
   be `(_btnMap != null && onMap != null)` — not just `(onMap != null)` — so a
   degraded-UXML host (declined `hasPeek`) leaves buttons disabled even after
   `SetHandlers`. Also called at the tail of `Bind` so degraded mode is
   disabled-by-default before any `SetHandlers` fires.
2. **Lifecycle contract:** doc-comment on `Bind` explicitly notes the
   SetActive-cycle re-clone contract (per memory
   `feedback_uidocument_setactive_reclone`) — host must call `Unbind` in
   OnDisable and `Bind` in OnEnable.

## Post-refactor call sites

**DialogueSceneController.cs** (~ -75 lines):
- Delete: 6 peek fields, 2 Action fields, `PeekOverlayIsHiddenClass` const,
  `WirePeekButtons` / `UnwirePeekButtons` / `RefreshPeekButtonEnabledStates`,
  3 click handlers, `IsPeekVisible` custom property body
- Keep public verbs `ShowPeek(string, VisualElement)` / `HidePeek()` /
  `IsPeekVisible` as thin forwards (called externally by `EventModalHost`)
- Add `private readonly PeekOverlayBinding _peek = new();`
- `Bind` becomes: `_peek.SetHandlers(onMapPeekRequested, onDeckPeekRequested);`
- `EnsureCached` gets `_peek.Bind(docRoot);`
- `OnDisable` gets `_peek.Unbind();`

**MerchantSceneController.cs** (~ -75 lines):
- Delete: 6 peek fields, `PeekOverlayIsHiddenClass` const, inline `ShowPeek` /
  `HidePeek` copies, `HandlePeekCloseClicked`, 6 peek `Q<>()` calls, conditional
  wire block, peek null-cleanup block, stale line-583 TD comment
- Add `private readonly PeekOverlayBinding _peek = new();`
- OnEnable: `_peek.Bind(docRoot); if (!_peek.IsAvailable) LogWarning(...);
  _peek.SetHandlers(HandleMapPeek, HandleDeckPeek);`
- OnDisable: `_peek.Unbind();`
- Rename `HandleMapPeekClicked` → `HandleMapPeek` (semantic: builder, not
  UI-event-handler now) — inline body still queries `RunState` + calls
  `_peek.Show("Map", PeekContentBuilder.BuildMap(state))`

## What is NOT changing

- `PeekContentBuilder.cs` — untouched (content half already shared)
- `EventModalHost.cs` — untouched (already consumes the DialogueSceneController
  seam correctly)
- `MerchantScreen.uxml` / `DialogueScene.uxml` — untouched (element IDs
  preserved: `#btn-peek-map` / `#btn-peek-deck` / `#btn-peek-close` /
  `#peek-overlay` / `#peek-title` / `#peek-content`)
- `.wr-dialogue-peek-*` USS classes — untouched
- Public verbs `dialogueController.ShowPeek(...)` / `HidePeek` — preserved for
  `EventModalHost` back-compat

## No-bridges audit (ADR-0011)

- No adapter layer
- No `IsLegacyPeekMode` bimodal flag
- No parallel storage (helper OWNS the 6 fields, host does not shadow them)
- No transitional `// TODO extract this` comments left behind
- Line-583 stale TD comment DELETED in the same commit
- Both consumers converge in the same commit

`IsAvailable` bool is NOT a bridge — it's a real runtime state (UXML declined
to load) matching the pattern in `feedback_data_flag_lagging_dependency`.

## Test plan

- No new tests (extraction is a shape refactor; existing PlayMode flow
  covers)
- Sanity: EditMode test suite green (`run-tests.ps1` — Unity Editor must be
  closed)
- Manual PlayMode: enter Merchant node → click Map peek button → verify
  overlay opens with map content → close → click Deck peek → verify deck
  content → close → click Leave → beacon exits cleanly. Repeat for Event
  node (which goes through DialogueSceneController).

## Technical Director Review

**Verdict: APPROVE with two MODIFY deltas (both applied above).**

### Q1 — Shape: APPROVE (pure C# helper, not MonoBehaviour)

Pure C# helper is correct. The peek UXML lives *inside* each host's
UIDocument tree — it's a subtree of an existing VisualElement, not a
scene-graph child. A MonoBehaviour would require prefab authoring, an
inspector surface, and lifecycle coupling to GameObject enable/disable — all
of which fight the actual ownership (the host's UIDocument owns the tree
lifetime, the helper only owns the wiring). Composition-smell-test (per
memory): a designer would *not* expect a `PeekOverlay` MB on the beacon
root — they'd expect the peek buttons to be part of the dialogue surface
UXML they're already editing. Pure helper is the honest shape.

### Q2 — Placement: MODIFY → `WastelandRun.UI` at `Assets/Scripts/UI/PeekOverlayBinding.cs` (asmdef arrow verified CombatView → UI)

Reason: `PeekOverlayBinding` is a pure UI Toolkit wiring helper with zero
domain knowledge (no `RunState`, no `PeekContentBuilder` dependency — it
takes `Action` handlers and `VisualElement` content). That belongs in the UI
asmdef. `CombatView.asmdef` line 9 confirms the arrow: CombatView already
references WastelandRun.UI, so MerchantSceneController can consume the
helper without a cycle.

### Q3 — ADR-0011 no-bridges check: APPROVE (clean)

Straight extraction, both hosts converge in the same commit, no adapter
layer, no bimodal path, no `IsLegacyPeekMode` flag, no transitional comment.
The `hasPeek`/`IsAvailable` graceful-degrade bool is *not* a bridge — it's a
real runtime state (UXML declined to load). One smell to name explicitly:
**do not** leave the inline `ShowPeek`/`HidePeek` bodies in either host as
"compat forwards" — delete them. Thin forwards in Dialogue are okay only if
callers outside the host currently invoke `dialogueController.ShowPeek(...)`
— `EventModalHost` uses the Bind-callback seam, but we grep before deleting
to confirm no external call sites.

### Q4 — Scope boundary: APPROVE extract-first

Extract now, before Rest peek buttons wire. Three reasons:

1. Rest UI overhaul is queued next and reuses DialogueSceneController — Rest
   inherits the clean helper for free the moment it lands.
2. Line-583 inline TD comment already committed to this — deferring past
   Rest would leave that comment stale, which is itself an ADR-0011
   transitional-comment smell.
3. Two concrete consumers is enough under ADR-0011. Waiting for a third to
   "prove" the abstraction is exactly the anti-pattern this rule prevents.

### Q5 — Blocker: NONE

Two MODIFY deltas listed above. Proceed.

### Self-Audit (three lenses)

- **Codebase Health:** No ADR-0011 drift introduced. Subscription-lifecycle
  risk named and mitigated via `Bind`/`Unbind` doc-comment contract.
  Duplication-vs-abstraction: two existing consumers + one imminent (Rest) =
  correct extraction moment per ADR-0011.
- **Optimization:** Zero per-frame cost — UI event wiring. No allocation per
  invocation (handlers stored as fields, not closures per-click). USS class
  toggle is one style recompute per Show/Hide (already paid pre-refactor).
- **1.0-Shape Survival:** Signature is the 1.0 shape — content-blind helper,
  action seams, IsAvailable fallback. Downstream consumers hook via
  `SetHandlers` without signature growth. No stopgap, no throwaway.

### Success criteria

- Rest peek buttons wire in one line
  (`_peek.SetHandlers(HandleMapPeek, HandleDeckPeek)`) with zero copied
  plumbing
- Zero re-emergence of `ShowPeek`/`HidePeek` inline bodies in any host
- Line-583 stale TD comment deleted in same commit
- EditMode tests green (attested in commit message)

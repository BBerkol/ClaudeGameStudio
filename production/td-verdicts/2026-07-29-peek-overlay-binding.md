# TD Verdict — PeekOverlayBinding Extraction

**Date:** 2026-07-29
**Slice:** Peek overlay plumbing extraction (pre-Rest-UI-overhaul cleanup)
**New file:** `Assets/Scripts/UI/PeekOverlayBinding.cs`
**Companion capture:** `production/polish-captures/2026-07-29-peek-overlay-extraction.md`

## Change under review

Introduce `WastelandRun.UI.PeekOverlayBinding` — a pure C# helper (not
`MonoBehaviour`) that owns the Map/Deck peek overlay wiring (6 UXML slots,
button click plumbing, Show/Hide verbs, host-callback seams). Two existing
hosts (`DialogueSceneController`, `MerchantSceneController`) currently
duplicate this plumbing verbatim; both converge on the new helper in the
same commit. `MerchantSceneController.cs` line 583 carries an in-code TD
directive to do this extraction BEFORE Rest peek buttons wire.

## Files touched

- **New:** `Assets/Scripts/UI/PeekOverlayBinding.cs` (~180 lines with doc
  comments)
- **Modified:** `Assets/Scripts/UI/DialogueSceneController.cs` (~ -75 lines)
- **Modified:** `Assets/Scripts/CombatView/MerchantSceneController.cs`
  (~ -75 lines)

Untouched: `Assets/Scripts/CombatView/PeekContentBuilder.cs`,
`Assets/Scripts/CombatView/EventModalHost.cs`, `MerchantScreen.uxml`,
`DialogueScene.uxml`, `.wr-dialogue-peek-*` USS classes.

## ADRs at risk of drift — audited clean

- **ADR-0011 (no bridges at done):** no adapter, no bimodal path, no
  `IsLegacyPeekMode` flag, no transitional comment. `IsAvailable` bool is a
  real runtime state (UXML declined to load), not a bridge — matches
  memory `feedback_data_flag_lagging_dependency`. Line-583 stale TD
  comment deleted in same commit.
- **ADR-0014 (UI Toolkit as primary stack):** helper stays in
  `WastelandRun.UI`, content-blind (no `RunState` / `Combat` dep). Asmdef
  arrow confirmed: `WastelandRun.CombatView.asmdef` line 9 already
  references `WastelandRun.UI`, so MerchantSceneController can consume the
  helper without a cycle. No `UnityEvent`.
- **ADR-0002 (POCO/events over UnityEvent):** helper uses `System.Action`
  seams throughout.

## Final-game picture this serves

Rest UI overhaul is queued next and reuses `DialogueSceneController`.
Extracting NOW means Rest inherits the clean helper for free; extracting
AFTER Rest lands would ship a third inline copy just to delete it. Also
unblocks the future Chopshop dialogue-first shape (Phase 2.5) — Chopshop
will consume the same helper. Long-term, ANY dialogue-first beacon that
needs a strategy peek plugs in with one `_peek.SetHandlers(map, deck)`
line + one `_peek.Bind(docRoot)` line — zero copied plumbing.

## TD Verdict

**APPROVE with two MODIFY deltas (both applied to the ship-file):**

1. **Enabled-state includes IsAvailable:** peek button `SetEnabled` must be
   `(_btnMap != null && _onMapRequested != null)` — not just
   `(_onMapRequested != null)` — so a degraded-UXML host leaves buttons
   disabled even after `SetHandlers`. `Bind` also calls
   `RefreshButtonEnabledStates` at its tail so degraded mode starts
   disabled-by-default before any `SetHandlers` fires.
2. **Lifecycle contract:** `Bind`'s xmldoc explicitly notes the SetActive
   re-clone contract per memory `feedback_uidocument_setactive_reclone` —
   host must call `Unbind` in OnDisable and `Bind` in OnEnable.

### Q1 Shape — APPROVE (pure C# helper, not MonoBehaviour)

The peek UXML lives *inside* each host's UIDocument tree — a subtree of an
existing VisualElement, not a scene-graph child. A MonoBehaviour would
require prefab authoring, an inspector surface, and lifecycle coupling to
GameObject enable/disable — all of which fight the actual ownership.
Composition smell-test (memory `feedback_composition_smell_test`): a
designer would NOT expect a `PeekOverlay` MB on the beacon root.

### Q2 Placement — APPROVE (`Assets/Scripts/UI/PeekOverlayBinding.cs`)

Content-blind helper with zero domain deps → belongs in UI asmdef.
Asmdef arrow verified.

### Q3 ADR-0011 — APPROVE (clean)

Two consumers, one commit, no bridge artifacts. Public verbs
`DialogueSceneController.ShowPeek/HidePeek/IsPeekVisible` retained as thin
forwards because `EventModalHost.cs:353,360` calls them externally — a
grep confirmed no other callers, so `HidePeek`/`IsPeekVisible` could be
inlined, but keeping the trio symmetric preserves the documented public
API surface and costs nothing.

### Q4 Timing — APPROVE extract-first

Before Rest UI overhaul lands. Rationale in the capture file.

### Q5 Blocker — NONE

## Self-Audit (three lenses)

- **Codebase Health:** No ADR-0011 drift. Subscription-lifecycle risk named
  and mitigated via Bind/Unbind doc contract. Two-caller extraction under
  ADR-0011 is timely (not premature).
- **Optimization:** Zero per-frame cost — UI event wiring only. No
  per-invocation allocation (handlers stored as fields, not per-click
  closures). USS class toggle already paid pre-refactor.
- **1.0-Shape Survival:** Signature is 1.0-final — content-blind,
  action-seam, IsAvailable degrade path. Downstream consumers hook via
  `SetHandlers` with no signature growth. No stopgap.

## Success criteria

- Rest peek buttons wire in ONE line when Rest lands
- Zero inline `ShowPeek`/`HidePeek` bodies remain in any host after the
  extraction commit
- Line-583 stale TD comment deleted in same commit
- EditMode tests green (attested in commit message)

# TD Verdict — RestSceneController Rename + Dialogue-First Rewrite

**Date:** 2026-07-29
**Slice:** Rest UI dialogue-first overhaul (last old-shape narrative surface)
**Companion capture:** `production/polish-captures/2026-07-29-rest-ui-dialogue-first.md`
**Files touched:**
- Rename: `Assets/Scripts/CombatView/RestPickerController.cs` → `RestSceneController.cs` (class + file + .meta pair)
- Rename: `Assets/UI/RestPicker.uxml/uss` → `RestScreen.uxml/uss` (files + .meta pairs; GUIDs preserved)
- Rewrite: `Assets/UI/RestScreen.uxml` body (~130 lines new)
- Rewrite: `Assets/UI/RestScreen.uss` body (~180 lines new)
- Rewrite: entry surface of `RestSceneController.cs` (OnEnable UXML query, ShowDialogueEntry, choice button wire, PeekOverlayBinding wire)
- Preserved verbatim: repair-mode logic in the same controller (EnterRepairMode / TickRepair / hover subs / weld cursor / sparks / budget bar drain)
- Edit: `Assets/Prefabs/BeaconRoots/RestRoot.prefab` lines 4898 + 4953
- Edit: `Assets/Editor/CombatPrefabAuthor.cs` lines 8727–8799 (path + type + name + comments)
- Sweep: 5 sibling files (see D1 delta)

## TD Verdict — APPROVE with two deltas (both applied)

### D1 — Sibling doc-comment sweep

No vestigial `RestPickerController` mention may remain in the codebase
post-commit — the class does not exist any more. Sweep required in
these 5 files (8 line-edits total):

- `Assets/Scripts/CombatView/MerchantSceneController.cs:35` —
  `<see cref="RestPickerController"/>` → `<see cref="RestSceneController"/>`
- `Assets/Scripts/CombatView/EventModalHost.cs:151` —
  text "RestPickerController." → "RestSceneController."
- `Assets/Scripts/CombatView/BeaconActivator.cs:396` —
  text "RestPickerController" → "RestSceneController"
- `Assets/Scripts/CombatView/RunSceneOverlayHost.cs:26` —
  `<see cref="RestPickerController"/>` → `<see cref="RestSceneController"/>`
- `Assets/Scripts/CombatView/VehicleBarStack.cs:147, 231, 637, 754, 818` —
  5 text refs
- `Assets/Tests/PlayMode/CombatView/RunPrefabAuthoring_Test.cs:87` —
  text "RestPicker lives on RestRoot.prefab" → "RestScreen lives on RestRoot.prefab"

### D2 — "(soon)" copy is intentional stopgap

Choice-2 (`"2. Forge a new card.  (soon)"`) + choice-3
(`"3. Upgrade a weapon.  (soon)"`) are Phase-2.5-gated placeholders.
The "(soon)" suffix WILL be ripped out when Phase 2.5 lands. Not
ADR-0011 drift (the label suffix is text-only, not structural), but
flag it in the Phase 2.5 branch commit so reviewer catches the copy.

## ADRs at risk of drift — audited clean

- **ADR-0011 (no bridges at done):** Old files renamed (not
  deprecated-and-kept); no adapter; no bimodal path; no
  transitional comment; D1 sweep enforces no vestigial name.
- **ADR-0014 (UI Toolkit as primary stack):** New RestScreen.uxml
  reuses `DialogueScene.uss` for peek classes (no duplication);
  content-blind `PeekOverlayBinding` seam consumed via one-line
  `SetHandlers`.
- **ADR-0002 (no UnityEvent):** All button clicks via `System.Action`
  seams (unchanged from Pass 1).
- **Slot-vocabulary + armor-not-subsystem discipline:** Rest scope
  still excludes `SlotKind.Armor` + `SlotKind.Bodywork` (memory
  `project_armor_not_subsystem` — user directive 2026-06-30). No
  drift.

## Final-game picture this serves

After this slice, ALL four narrative beacon types (Merchant, Event,
Rest, and — post-Phase 2.5 — Chopshop) read as the same fiction:
full-screen illustration, right dialogue panel, numbered choices,
Map/Deck peek in header. Rest players ALSO gain peek availability
that Pass 1 didn't ship. When Phase 2.5 wires Forge/Upgrade, both
plug in as additive UXML sections + one handler pair each — no
controller shape churn, no UXML restructure, no rename.

## Q-Ladder verdicts

### Q1 Shape — APPROVE (dialogue-first-with-immediate-cursor-swap)

Adding a stub slide-in picker panel now would be ADR-0011 drift.
Repair's cursor-swap-inline is the shipping shape and works. When
Phase 2.5 lands Forge/Upgrade, the `#shop-panel`-equivalent slot is
a purely additive UXML sibling of `#dialogue-panel`, identical to
how Merchant grew it.

### Q2 Placement — APPROVE (rename to `RestSceneController.cs`)

Parallel to `MerchantSceneController`. One hard type ref
(CombatPrefabAuthor.cs:8773), one prefab `m_EditorClassIdentifier`
string, 8 doc-comment mentions — all fixed in same commit. Skipping
the rename to "avoid churn" is the ADR-0011 vestigial-name trap.

### Q3 ADR-0011 — APPROVE (clean if D1 holds)

Two files renamed via `git mv` (history preserved). No parallel
paths. D1 sweep enforces zero vestigial name.

### Q4 Timing — APPROVE (extract-first)

Before Chopshop Phase 2.5. Deferring past Phase 2.5 would leave
Rest as the sole old-shape narrative surface (memory
`project_rest_ui_overhaul_pending` directly warns).

### Q5 Blocker — soft

Verify `PeekOverlayBinding.cs.meta` lands with this commit. File
was created 2026-07-29 without .meta (Unity not focused post-Write).
No serialized reference exists (POCO field-init) so class-name
resolution survives, but the meta should land or the Merchant slice's
PeekOverlayBinding wire has an un-versioned dependency.

## Three-Lens Self-Audit

- **Codebase Health:** ADR-0011 clean iff D1 sweep holds;
  subscription lifecycle preserved (repair-mode hover subs stay
  Bind/Unbind-scoped); peek helper is now second-consumer-validated;
  no teardown race introduced; single-responsibility charter now
  matches actual work (`Scene` not `Picker`).
- **Optimization:** Zero per-frame alloc added; UXML load one-shot
  at BeaconActivator switch; peek overlay build is on-demand via
  `PeekContentBuilder`; USS reuses `DialogueScene.uss` (no duplicate
  style rules).
- **1.0-Shape Survival:** Dialogue-first shape IS 1.0 canonical
  for non-combat beacons; choice-slot shape IS 1.0 (only handler
  bodies are stubs — no restructure when Phase 2.5 wires); peek
  is full 1.0 payload (Map + Deck).

## Success criteria

1. All four narrative beacons read as the same fiction shape.
2. Phase 2.5 Forge/Upgrade lands as additive UXML sibling + one
   handler pair each — no dialogue-panel restructure, no controller
   shape churn, no rename.
3. `grep RestPicker` across `Assets/**/*.{cs,uxml,uss,prefab}` returns
   zero hits post-commit.
4. First Rest visit shows Map/Deck peek buttons in the header.
5. Repair-mode drain byte-identical to Pass 1 (same StartBudget=20,
   SecondsPerTick=1/3, hover subs, sparks, cursor swap, budget bar).
6. Tests green (EditMode + PlayMode) — attested in commit message.

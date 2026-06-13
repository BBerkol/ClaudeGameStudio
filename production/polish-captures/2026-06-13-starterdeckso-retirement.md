# Polish Capture: StarterDeckSO Retirement (Slice 5a)

**Date:** 2026-06-13
**System:** Run-loop / Slice 5a — replacement of the prototype-era
`StarterDeckSO` ScriptableObject carrier with the canonical Run-layer
POCO factory `RunDeck.Milestone1Starter()` introduced by ADR-0013.

**Affected paths (Unity repo, uncommitted at time of capture):**

- `Assets/Scripts/CombatView/Data/StarterDeckSO.cs` — DELETED (+ `.meta`,
  GUID `9e72dfdc977051740a0353c2427b00b2`)
- `Assets/Resources/combat/Decks/StarterDeck_Basic.asset` — DELETED
  (+ `.meta`, asset GUID `d3206ea4d1892014ea0ce070d728dcf9`)
- `Assets/Editor/CombatDataInitializer.cs` — MODIFIED (Starter-Deck
  generation block + `DecksRoot` const + `CreateOrLoadDeck` helper +
  `EnsureFolder("Decks")` line removed; 5 unused `cardLookup[...]`
  locals pruned; docstring re-pointed at `RunDeck.Milestone1Starter()`)
- `Assets/Scripts/CombatView/CombatController.cs` — MODIFIED (drops
  `_starterDeckAsset` SerializeField, `DefaultStarterDeckResourcePath`
  constant, and the Resources fallback path; deck now arrives via
  `_host.State.Deck` after `RunSceneHost.BeginNewRun()`)

## Proposed change

Retire the `StarterDeckSO` carrier and its single authored instance
`StarterDeck_Basic.asset` in the same commit that brings up
`RunSceneHost`. The 13-card Milestone-1 starter content survives
verbatim in `RunDeck.Milestone1Starter()` — the same cards, the same
counts, the same energy costs, the same effects — and is exercised by
the 10-test `RunDeck_Test` suite that already shipped with Slice 4.
The slice does not rebalance, rename, or re-cost any card.

The carrier change closes a vestigial-enum-of-one path (ADR-0011
forbidden pattern #4): the SO had exactly one authored asset, exactly
one serialized field (`_entries`), and no per-instance tuning beyond
the entry list. Keeping it alongside `RunDeck.Milestone1Starter()`
even for a single commit would create a bridge window (ADR-0011
forbidden pattern #1) and tempt future code to pick whichever deck
source was closest to hand.

## Final-game picture this serves

ADR-0013's terminal shape: a single run grows a custom deck across
many encounters by accepting cards from per-combat `CardOffer`s. The
deck lives as a Run-layer POCO on `RunState.Deck`, constructed at
`RunController.StartRun(Vehicle, runSeed, NodeMap)` from
`RunDeck.Milestone1Starter()` and grown thereafter via
`AcceptPendingCardChoice`. The node-map UI (Slice 6) will give the
player a map to walk; the bootstrap responsibility moves fully into
`RunSceneHost.BeginNewRun()`. At that terminal shape there is no
second authored starter deck, no UI for picking one, and no biome
swap that points at a different SO — so the SO carrier's lifetime
ends with this slice. The CombatDataInitializer's Cards / Pools /
Vehicles / Balance generators stay on for the same reason the deck
generator leaves: those have multiple authored instances and
per-instance tuning. Decks have exactly one, by design.

## Authored values being destroyed

The `StarterDeck_Basic.asset` instance contained 8 entries totaling
13 cards. All card GUIDs verified against
`Assets/Resources/Combat/Cards/Card_*.asset.meta`:

| Card | Asset GUID | Count | Replacement |
|---|---|---|---|
| BulletBarrage | `ee0e4410121fa0d4d9bcce006ccd7dc3` | 3 | `RunDeck.Milestone1Starter()` row, identical content |
| FlameBurst | `4f823bce5c41549409b1791a7c7c9f9d` | 2 | `RunDeck.Milestone1Starter()` row, identical content |
| Weld | `51f348d338d334141b0d81fda457b056` | 3 | `RunDeck.Milestone1Starter()` row, identical content |
| Handbrake | `7c38c6fe079e7be4397f81efcfd052e8` | 1 | `RunDeck.Milestone1Starter()` row, identical content |
| Overtake | `f4dda9caee7c8e0418fa20a7647d6ca4` | 1 | `RunDeck.Milestone1Starter()` row, identical content |
| Patch | `58da3a3eb2aafd847ad656a184dde981` | 1 | `RunDeck.Milestone1Starter()` row, identical content |
| Draw | `69954143ae54eb042b24193e2068d6f1` | 1 | `RunDeck.Milestone1Starter()` row, identical content |
| FlameBarrier | `5d490b4f79c81c6429157e88086e55a4` | 1 | `RunDeck.Milestone1Starter()` row, identical content |

No designer overrides outside the entry list. No inspector tweaks
elsewhere — the SO has only `[SerializeField] List<Entry> _entries`
plus the `[CreateAssetMenu]` attribute (which is the point: there
must be no path back to authoring a second starter deck via the
inspector).

`StarterDeckSO.cs` source itself (60 lines): one `Entry` inner class
(`CardDefinitionSO card`, `[Min(1)] int count`, two-arg ctor), one
`ToCards()` expansion method that constructs fresh POCO copies per
card via `CardDefinitionSO.ToRuntime()`, one `internal SetEntries()`
editor bootstrap helper. All capability re-homed: the same expansion
to fresh `CardDefinition` POCOs now happens in `RunDeck.Cards` /
`RunDeck.Milestone1Starter()`; the editor bootstrap path
(`CombatDataInitializer.GenerateAll()`) no longer needs to author
this asset at all.

## Adjacent risk (not destroyed, flagged for user)

- `Combat.prefab` retains orphan YAML lines from
  `CombatController`'s removed SerializeFields: `_starterDeckAsset`
  (line 300), `_playerVehicleAsset` (301), `_enemyArchetypePrefab`
  (304), `_deckSeed` (299). Unity's serializer drops missing fields
  on next prefab save; non-blocking, will self-heal on next author
  pass.
- `Combat.prefab` has **no** `RunSceneHost` component. With
  `CombatController._host` unwired and the lookup chain (Inspector →
  `GetComponentInParent` → `FindAnyObjectByType`) returning null,
  Play mode would NPE at `ResetCombat()`. Slice 5a is not closed
  until this is wired in-editor.

## Technical Director Review

**Verdict:** APPROVE (with contingency)
**Spawned at:** 2026-06-13
**Agent:** technical-director

**TD verdict (verbatim):**

> **TD-CAPTURE: APPROVE**
>
> **Rationale**
>
> This is a textbook ADR-0011 cleanup, not a destructive refactor.
> The SO carrier had one authored instance, one serialized field
> (`_entries`), and zero per-instance tuning beyond the entry list —
> that is the dictionary definition of "vestigial enum-of-one"
> (forbidden pattern #4). Retiring it in the same slice that
> introduces `RunSceneHost` + `RunDeck.Milestone1Starter()` is
> correct: it prevents the SO from existing in parallel with the
> canonical POCO factory even for one commit, which would otherwise
> create a bridge window (forbidden pattern #1) and tempt future code
> paths to read from whichever carrier was closer to hand.
>
> **Content preservation verified by capture**
>
> The 13-card list (BulletBarrage×3, FlameBurst×2, Weld×3,
> Handbrake×1, Overtake×1, Patch×1, Draw×1, FlameBarrier×1 = 13) is
> enumerated in the capture and survives verbatim in
> `RunDeck.Milestone1Starter()` under `RunDeck_Test` coverage (10/10
> green). No designer overrides are being orphaned. The
> `[CreateAssetMenu]` attribute is gone, which is the point — there
> must be no path back to authoring a second starter deck via the
> inspector.
>
> **Risk register**
>
> - *Prefab YAML orphans* (`_starterDeckAsset`, `_deckSeed`,
>   `_playerVehicleAsset`, `_enemyArchetypePrefab`): acceptable.
>   Unity's serializer drops missing fields on next save. Flagged to
>   user; not blocking.
> - *Missing `RunSceneHost` component on `Combat.prefab`*: this is
>   the one real footgun. The slice is not "done" until the prefab
>   is authored and Play mode boots through
>   `RunSceneHost.BeginNewRun()`. Verdict is contingent on this
>   being addressed before the slice closes — not before commit,
>   but before the slice is marked complete in the sprint board.
> - *EditMode 467/466/0/1*: matches the gate-check standard. Green
>   attestation accepted.
>
> **No reshape required**
>
> The four-file scope is minimally sufficient: SO class + asset +
> initializer block + CombatController fallback path. Anything less
> would leave a bridge; anything more (e.g., touching the other
> CombatDataInitializer generators) would violate the per-system
> carrier-vs-instance distinction the capture correctly articulates
> — Cards/Pools/Vehicles/Balance have multiple authored instances
> and stay SO-backed. That asymmetry is the right read.
>
> **Verdict**: APPROVE. Proceed to commit on user approval of the
> capture. Confirm `RunSceneHost` is added to `Combat.prefab`
> before closing Slice 5a.

**TD reasoning summary:**
- ADR-0011 cleanup, not a destructive refactor — the SO was a
  vestigial-enum-of-one (one instance, one field).
- 13-card content preserved verbatim in `RunDeck.Milestone1Starter()`,
  covered by `RunDeck_Test` (10/10 green).
- Same-commit retirement is required: keeping the SO and the POCO
  in parallel for even one commit creates a bridge window.
- Prefab YAML orphans are acceptable (Unity self-heals on save).
- **One contingency:** `RunSceneHost` must be added to
  `Combat.prefab` before Slice 5a is marked complete in the sprint
  board. Not blocking for commit, blocking for slice closeout.
- Four-file scope is minimally sufficient — anything less keeps a
  bridge; anything more (touching Cards/Pools/Vehicles/Balance
  generators) would violate the carrier-vs-instance distinction.

## User approval

- Reviewed: 2026-06-13
- Approved by: BertanBerkol
- Notes: Approved. RunSceneHost Combat.prefab wiring tracked as separate
  pre-closeout item per TD contingency.

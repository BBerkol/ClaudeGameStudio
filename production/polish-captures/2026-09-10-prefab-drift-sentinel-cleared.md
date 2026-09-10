# Capture — Prefab Drift Sentinel Investigated and Cleared

**Date:** 2026-09-10
**System:** prefab authoring · drift sentinel
**Closes:** remediation plan Phase 1.1
**Outcome:** sentinel cleared as a **false positive**. No prefab was modified at
any point in this investigation.

---

## The claim under investigation

`production/session-state/prefab-drift-pending.json` flagged four entries on
**2026-08-30T19:00:19+03:00**:

```json
{"version":1,"pending":["Combat","PlayerVehicle","MainBar","HudAnchors"]}
```

Per protocol this blocked **all** re-authoring, and it is a hard prerequisite
for Phase 4.1, whose acceptance test runs `Author All Scenes` twice and diffs.

**Nothing was baked, because there was nothing to bake.** Four independent
lines of evidence, below.

---

## Evidence 1 — the hook flags PROMPT TEXT, not prefab content

`.claude/hooks/pre-author-bake-required.sh` never opens a `.prefab`. It
regex-matches the **user's prompt** for an edit verb or an edit-context phrase
next to a vehicle name:

```
EDIT_CONTEXT_RE='\b(in prefab mode|prefab mode|in (the )?editor|in unity|
in the unity editor|designer (tuning|tweak|edit)|in the prefab|hand-placed|
hand placed|by hand)\b'
```

It is a **disclosure detector**, not a drift detector — and its own comments
record a prior false positive:

> *"verified 2026-08-29 that 'i am working on combat cards' re-flagged the
> sentinel. Because the hook re-flags on every matching prompt, that made the
> sentinel permanently sticky."*

That is the same failure mode one day before this sentinel fired.

## Evidence 2 — git says no prefab has changed since 2026-08-05

| Prefab | Last commit |
|---|---|
| `Combat.prefab` | `ed4ce30` **2026-08-05** — *"re-author Combat.prefab to author-script output"* |
| `MainBar.prefab` | `d85feb3` **2026-06-30** |
| `PlayerVehicle.prefab` | `a0b4d6d` **2026-07-03** |

All three predate the 2026-08-30 flag. `Combat.prefab`'s last commit is
literally a re-author **to** author-script output — i.e. it was deliberately
brought INTO alignment, the opposite of drift.

## Evidence 3 — the working tree is clean

`git status --porcelain` over `Assets/Prefabs/` returns nothing. So there are no
unsaved designer edits sitting on disk either. If a designer had tuned a prefab
in Prefab Mode and Unity had written it, git would show it.

## Evidence 4 — `HudAnchors` is not a prefab at all

The pending list names `HudAnchors`. **`HudAnchors.prefab` does not exist.**
`HudAnchors` is a *container GameObject* built by
`BuildVehicleHudAnchors`/`SeedHudAnchor` and carrying a `VehicleHudAnchors`
component inside other prefabs (`CombatPrefabAuthor.cs:1833-1841`). A list built
from file inspection could not contain it; a list built from prompt text could.

---

## What was actually run, and what it proved

The user chose the definitive test: run the authors headlessly against a clean
tree, diff, restore. Pre-test state recorded for verifiable restoration —
HEAD `a968cc8`, tree clean, and MD5s:

```
ebeb811b06560e5901d6f3dfcce48fd9  Combat.prefab
e6a3b1304544e11b208086744a00a02a  PlayerVehicle.prefab
7fbbacc873fb9d4633523be2fd6f2ae1  MainBar.prefab
```

`Unity.exe -batchmode -nographics -executeMethod
WastelandRun.CombatView.Editor.CombatPrefabAuthor.AuthorMainBarMenu`

**The author refused to run, by design:**

```
DisplayDialog: Overwrite MainBar.prefab? … already exists.
Re-authoring will RESET designer tweaks (pixelsPerHp, anchor presets,
ap/hp/bg colors, sibling overlays) back to author defaults.
[CombatPrefabAuthor] AuthorMainBar cancelled — MainBar.prefab preserved.
```

`EditorUtility.DisplayDialog` returns `false` in batchmode, so the guard
auto-declines. **The safety dialog worked exactly as intended.** Post-run MD5s
were byte-identical to the table above and the tree stayed clean.

### Two operational findings worth keeping

- **`-executeMethod` needs `-quit`.** Without it Unity sat resident for 45
  minutes after the method returned, with no further log writes. Distinct from
  the `-runTests` rule, where `-quit` makes tests silently skip. Diagnostic
  signature of the hang: `Unity.exe` alive at flat memory + log mtime frozen.
- **The menu wrappers are not headless-callable, and that is deliberate.**
  `AuthorMainBar()` is `private`; only the dialog-guarded `…Menu()` wrappers are
  public for MainBar. `AuthorCombat()` and `AuthorPlayerVehicle()` are public
  and *would* bypass the guard — **do not** call them to satisfy a diagnostic.

---

## Ruling

**Cleared as a false positive.** The sentinel recorded that a prompt *sounded
like* a prefab edit, not that one occurred.

**Residual risk, stated plainly:** if a designer edit was made in Prefab Mode and
never saved to disk, it was already lost before this investigation began — no
trace exists in git or the working tree, and clearing the sentinel neither
causes nor conceals that. Nothing in the evidence suggests it happened.

**Not changed:** the hook itself stays as-is. It is deliberately liberal
("false positives are cheap, a destroyed designer tweak is not") and that trade
is correct. This capture exists so the next person to hit a sticky sentinel can
check the four evidence lines above in minutes instead of re-deriving them.

**Phase 4.1 is unblocked** — its acceptance test (`Author All Scenes` twice,
empty diff) may now proceed. Note it will hit the same batchmode dialog
behaviour, so it needs a human at the Editor or a `-quit`-bearing invocation of
a non-guarded entry point.

## Approval

- [x] **User approval to clear on the evidence — granted 2026-09-10.**
      Option chosen over (a) re-running the authors in the Editor by hand and
      (b) bypassing the guard dialog headlessly.

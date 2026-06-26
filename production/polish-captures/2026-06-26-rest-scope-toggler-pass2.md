# 2026-06-26 — RestScopeToggler pass 2 (TRIMMED — bar-spawn refactor only; MainBar + IntentCanvas deferred to Slice 10 scene-split)

Slice 9b third-pass closeout. Pass 1 (sibling capture `2026-06-26-rest-scope-toggler.md`) shipped the 5-fix bundle (rename + 3 new refs + lerp snap + AlwaysVisible enum + AuthorCombatScene wire). Second PlayMode trial surfaces 4 residual gaps. **Trimmed to Fix #1 + defer #4** after second TD consultation flagged whack-a-mole risk and reopened the scene-split window from 2026-06-17.

## Trigger (PlayMode observation, post pass-1)

Same Rest beacon scenario after pass 1 landed. Backdrop swap works. SceneVisuals subtree hidden. Enemy vehicle hidden. EnemyBarStackCanvas hidden. Combat_HUD canvas SetActive(false) (cards / end-turn / banners gone). Vehicle snaps to centre. Picker scrim renders with "Nothing to repair / CONTINUE →". But:

1. **Per-slot bars STILL invisible on the rest-posed vehicle**. Markers (small squares on the chassis) ARE visible. Bars are spawned but each one carries its combat-mode `HideOnFullUnlessAttackActive` rule — at full HP idle they self-hide. **→ FIX (Option B refactor).**
2. **Player MainBar (top-left 20/20 HP + 55/55 AP plate)** still renders. PlayerBarStackCanvas was kept on intentionally (parents the rest-mode per-slot bars); MainBar lives nested under it and was never told to hide. **→ DEFERRED to Slice 10 scene-split.**
3. **Enemy intent telegraph ("16 ATTACK", top-right)** still renders. `IntentCanvas` is **runtime-built** by `CombatHud.BuildIntentTelegraph` (world-space Canvas, sortingOrder 5) parented to `_blockout.LaneAxis` — NOT a child of Combat_HUD. Our SetActive(false) on Combat_HUD doesn't reach it. **→ DEFERRED to Slice 10 scene-split.**
4. **HandBeat DiscardBurst warning** logs once at rest transition. Pre-inactive event drain; one-shot per transition; not state corruption. **→ DEFERRED (one-shot drain log, not a leak).**

## Why trimmed — second TD verdict (2026-06-26, after pass-2 draft)

User flagged "are we tangling things up?" after seeing pass-2 plan add a 6th serialized ref + runtime Find for IntentCanvas. Second TD consultation reopened the scope question.

> **TD-ARCHITECTURE: CONCERNS**
>
> The user's instinct is correct. Pause pass-2. The pattern is whack-a-mole, and we are inside the scene-split window I called on 2026-06-17. But A→C is not the right cut today either — closing Slice 9b on a half-built scene refactor is its own trap.
>
> **Recommendation: hybrid A-now / C-next.**
>
> **Land a *trimmed* pass-2 today** — fixes #1 (VehicleBarStack Option B refactor) and #4 (HandBeat defer). Those are correct under any topology — the bar-spawn vs bar-behavior split is canonical, not topology-coupled.
>
> **Drop fixes #2 and #3 from pass-2.** MainBar hiding and IntentCanvas runtime-Find are the exact surfaces that will dissolve under scene-split. Adding a 6th ref + a literal-name Find this afternoon is work we throw away in two weeks. Worse, the IntentCanvas Find normalizes "RestScopeToggler reaches into LaneAxis by string" as a pattern — once landed, pass-3/4 inherit it.
>
> **Accept the cosmetic gap.** Slice 9b ships with player MainBar + IntentCanvas still rendering during Rest. Ugly but not broken — Rest picker scrim is on top, vehicle re-poses, picker resolves cleanly. Note the visual debt explicitly in the Slice 9b closeout.
>
> **Open Slice 10 as scene-split execution** per my 2026-06-17 verdict. Conditions are now met: Slice 7 (ADR-0004) closed, biome 2 not started, RestScopeToggler at exactly the size where its category-coherence starts to crack (your "6 refs vs 7" question is the symptom). Combat.unity owns combat chrome; Rest.unity owns the rest screen; the toggler stops existing because the *scene boundary* is the seam.
>
> **Why not A-only:** Pass-3 lands within one PlayMode trial of pass-2 — Popups canvas is the next surface (outcome banner draws over rest scrim). At 7 refs the toggler is an inventory not a component.
>
> **Why not C-only:** Scene-split is 1-2 days authoring. Closing 9b today on partial scene work compounds risk per my own deferral logic (overlapping structural changes, harder bisection).
>
> **Pushback on the original framing:** Option D (reparent IntentCanvas under HUD) is unsafe — breaks VehiclePositionAnimator follower math, confirm before ever proposing again. Option B (camera culling layers) doesn't apply — UGUI canvases ignore culling layers, so it can't be the single seam.
>
> **Success metric:** Slice 10 closes with RestScopeToggler.cs deleted entirely. If it survives the refactor, the topology was wrong and we re-open.

## Values being destroyed (trimmed to Fix #1 only)

### `Assets/Scripts/CombatView/VehicleBarStack.cs`

Restructure `TryBuildRestWidgets` (line 757) per Option B — keep `_runtimeBuilt` as the spawn gate, lift the rest-mode behavior out of the gate:

```csharp
private void TryBuildRestWidgets()
{
    Vehicle target = _restTargetGetter?.Invoke();
    if (target == null) return;

    if (!_runtimeBuilt)
    {
        _structuralSlotId = target.GetStructuralSlotId();
        EnsureRuntimeMainBar();
        if (_runtimeMainBar != null)
        {
            _runtimeMainBar.Bind(target);
            _runtimeMainBar.SetShowName(false);
        }
        BuildPerSlotBars(target, tooltip: null, tooltipKeyPrefix: "rest");
        _runtimeBuilt = true;
    }
    else
    {
        // Bars built by prior combat path — re-Bind MainBar with rest
        // target identity (in case the bound vehicle ref drifted).
        if (_runtimeMainBar != null)
        {
            _runtimeMainBar.Bind(target);
            _runtimeMainBar.SetShowName(false);
        }
    }

    // Unconditional rest behavior — applies to bars regardless of who
    // spawned them. Idempotent re-paint; not a re-spawn.
    for (int i = 0; i < _runtimeBars.Count; i++)
    {
        if (_runtimeBars[i] != null)
            _runtimeBars[i].SetHideRule(SubsystemBar.HideRule.AlwaysVisible);
    }
    for (int i = 0; i < _runtimeSlotIds.Count; i++)
    {
        SubsystemBar bar = _runtimeBars[i];
        if (bar == null) continue;
        string slotId = _runtimeSlotIds[i];
        SlotInstance slot = target.GetSlotById(slotId);
        bar.Refresh(slot.Hp, slot.MaxHp, slot.DamageState);
    }
}
```

No new fields. No new methods. The spawn / behavior axes are split inline — combat ticks Refresh every frame via `Update()`, so it never needs the rest path's post-step. This is topology-independent — correct under both the current single-scene shape and the future scene-split shape.

## Files NOT touched (trimmed)

- `RestScopeToggler.cs` — no new SerializeField, no runtime Find. Stays at 5 refs from pass 1.
- `CombatPrefabAuthor.cs` — no `WireRestPickerCrossPrefab` edit (no new ref to wire).
- `RestScopeToggler_show_togglesCombatScope_test.cs` — no extension (no new ref to assert).
- `Combat.prefab` / `Run.prefab` — no edit.
- `CombatHud.cs` / `BuildIntentTelegraph` — left alone (deferred to scene-split).
- `HandBeat.cs` — deferred (one-shot drain log).

## Re-author sequence after edits land

None required. The `VehicleBarStack` edit is a pure runtime behavior change — no prefab dependency, no SerializeField shape change, no AuthorXxx menu re-run.

## Cosmetic debt carried into Slice 9b closeout

Two visual gaps survive the trimmed bundle. Documented in the Slice 9b closeout note so the next session sees them:

- **Player HP/AP plate (top-left)** continues to render during Rest. Picker scrim sits over it visually; no functional break. Will disappear when Slice 10 scene-split moves combat chrome into a separate scene that's unloaded during Rest.
- **Enemy intent telegraph (top-right)** continues to render during Rest. Same fix path — runtime-built canvas dies with its host scene on unload.

## Slice 10 — scene-split execution (next slice)

Conditions met per TD's 2026-06-17 verdict (`production/td-verdicts/2026-06-17-scene-split-verdict.md`):
- Slice 7 (ADR-0004) closed
- Biome 2 not started
- RestScopeToggler at the size where category coherence starts cracking

Scope (preview, not yet scoped): persistent `RunScene` + additive beacon sub-scenes (Combat.unity, Rest.unity). Beacon dispatch unloads the prior sub-scene and loads the new one. RestScopeToggler.cs is deleted entirely as the scene boundary becomes the on/off seam.

## Sign-off

Trimmed plan approved by user 2026-06-26. Pass-2 capture replaces the prior draft (which is preserved as commit history). Pass-2 ships Fix #1 only; deferrals + debt are documented above.

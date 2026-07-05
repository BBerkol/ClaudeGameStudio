---
date: 2026-07-04
auditor: performance-analyst
scope: Wasteland Run\Assets\Scripts\ (Combat, CombatView, Run, Save, UI)
method: static analysis — no profiler data
budgets: 16.6ms frame / 200 draw calls / 2GB memory
---

# Performance Hotspot Audit — 2026-07-04

## Executive Summary

The codebase is broadly disciplined. The POCO combat model (ADR-0002) means turn logic is zero-cost at 60fps — no per-frame gameplay allocation exists there. The ambient animation layer (WheelBounce, ChassisBounce, LayerScroller, WheelSpinner) is clean: no allocations, no component lookups, well-structured loops.

The hotspots cluster in two areas:

1. **HUD overlay refresh** — `MainBarWidget.Bind` is called every frame by `VehicleBarStack.Update` and unconditionally triggers `ApplyTextContent`, which allocates strings every tick regardless of whether HP/armor changed. Two vehicles means two stacks running this path every frame throughout combat.
2. **Per-drag Canvas walk in CardWidget** — `GetComponentInParent<Canvas>()` fires on every `OnDrag` event tick; this is a hierarchy traversal on the UI input hot path.

Both are fixable with a dirty-flag / cache pattern and carry zero design risk. The remaining findings are low-severity or guarded well enough not to cause framerate problems at current content scale.

Top 5 concerns:
1. `MainBarWidget.Bind` — unconditional string allocation every frame (×2 vehicles)
2. `VehicleBarStack.Update` feeding `MainBarWidget.Bind` every tick with no dirty guard
3. `CardWidget.TryProjectScreenToParentLocal` — `GetComponentInParent` on every drag event
4. `IntentWidget.Update` — `SetActive` thrash on arrow/target image children each frame
5. `CardWidget.Update` — per-frame TMP string writes when card definition is stable mid-turn

---

## HIGH SEVERITY — Will burn frame budget under normal play

---

### H-1 — `MainBarWidget.Bind` unconditionally allocates strings every frame

**File:** `CombatView/MainBarWidget.cs`, lines 247–248, 291–296

**Pattern:** Unconditional per-frame string allocation

**Detail:**
`VehicleBarStack.Update` calls `_runtimeMainBar.Bind(target)` every frame (lines 392 and 836 in `VehicleBarStack.cs`). `Bind` always calls `Refresh`, which calls `ApplyStaticVisuals()` and `ApplyTextContent()`. `ApplyTextContent` contains:

```csharp
if (_armorText != null) _armorText.text = $"{_curArmor}/{_maxArmor}";
if (_hpText    != null) _hpText.text    = $"{_curHp}/{_maxHp}";
```

These string interpolations allocate a new string every frame even when HP and armor have not changed. Combat is turn-based — HP values are stable for every frame of the player's turn. With two vehicles (player + enemy), this runs twice per frame, plus once more per `UpdateRestBound` on the rest screen.

`ApplyStaticVisuals()` also reads all palette colors and writes `Image.color` unconditionally every frame, which dirties UGUI batches on the sub-bar canvases even when color has not changed.

**Fix pattern:**
Cache last-written values alongside the existing `_maxHp`, `_curHp` fields. Only call `ApplyTextContent` when values have changed:

```csharp
private int _lastWrittenCurHp = -1;
private int _lastWrittenCurArmor = -1;

private void ApplyTextContent()
{
    if (_curHp == _lastWrittenCurHp && _curArmor == _lastWrittenCurArmor) return;
    if (_armorText != null) _armorText.text = $"{_curArmor}/{_maxArmor}";
    if (_hpText    != null) _hpText.text    = $"{_curHp}/{_maxHp}";
    _lastWrittenCurHp = _curHp;
    _lastWrittenCurArmor = _curArmor;
}
```

Apply the same pattern to `ApplyStaticVisuals` — only re-apply colors when palette or derived values change. The existing capacity-change guard on `ApplyRootWidth` is the right model.

**Estimated impact:** Eliminates 2–4 string allocations per frame throughout all of combat + rest screen. Reduces UGUI dirty-batch rate on the HP/armor sub-canvases. Measure in profiler: expect visible reduction in GC alloc/frame column.

---

### H-2 — `VehicleBarStack.Update` drives `MainBarWidget.Bind` every tick with no dirty guard

**File:** `CombatView/VehicleBarStack.cs`, lines 392, 836

**Pattern:** Unconditional per-frame push to a widget that doesn't self-throttle

**Detail:**
`VehicleBarStack.Update` unconditionally calls `_runtimeMainBar.Bind(target)` each frame in both the combat path and the rest path. `Bind` reads `target.MaxArmor`, `target.StructuralMaxHp`, `target.CurrentArmor`, `target.StructuralHp` and `target.Name` every frame, then calls `Refresh` unconditionally.

This is architecturally correct (pull-driven is cleaner than event-driven for this widget) but the pull must be coupled with a dirty check before it writes. H-1's fix resolves the string allocation. The additional cost here is that `Bind` also calls `ApplyStaticVisuals()` every frame (which resets Image colors), and if `_showName` is true, writes `_nameText.text = target.Name` every frame — another allocation if the name is not already interned.

**Fix pattern:**
Add a name dirty check in `Bind`:

```csharp
if (_showName && _nameText != null && _nameText.text != target.Name)
    _nameText.text = target.Name;
```

The name is stable for an entire combat encounter, so this eliminates the string write every frame.

**Estimated impact:** Eliminates one string allocation per frame per vehicle on the enemy-side bar (enemy names are non-interned). Measure in profiler.

---

### H-3 — `CardWidget.TryProjectScreenToParentLocal` calls `GetComponentInParent<Canvas>()` on every drag tick

**File:** `CombatView/CardWidget.cs`, line 791

**Pattern:** `GetComponentInParent` in a per-event hot path

**Detail:**
`TryProjectScreenToParentLocal` is called inside `OnDrag` (line 665), which fires every frame the player holds a dragged card. The method calls:

```csharp
Canvas canvas = parentRt.GetComponentInParent<Canvas>();
```

This is a hierarchy traversal on the UI input thread every frame the drag is active. For a game with a CanvasScaler, the canvas sits several levels up the hierarchy, so this walk is non-trivial.

**Fix pattern:**
Cache the parent canvas reference at Awake (or on first drag) and reuse it:

```csharp
private Canvas _parentCanvas;

private void Awake()
{
    _rect = GetComponent<RectTransform>();
    // ... existing code ...
    _parentCanvas = GetComponentInParent<Canvas>();
}

private bool TryProjectScreenToParentLocal(Vector2 screenPos, out Vector2 local)
{
    local = Vector2.zero;
    RectTransform parentRt = _rect.parent as RectTransform;
    if (parentRt == null) return false;
    if (_parentCanvas == null) _parentCanvas = parentRt.GetComponentInParent<Canvas>();
    Camera cam = (_parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        ? _parentCanvas.worldCamera
        : null;
    return RectTransformUtility.ScreenPointToLocalPointInRectangle(
        parentRt, screenPos, cam, out local);
}
```

**Estimated impact:** Eliminates one hierarchy traversal per drag frame. For a 60fps drag gesture lasting 0.5s that is ~30 traversals. Not framerate-breaking for a single drag but it is the correct pattern for input hot paths. Measure via profiler during drag.

---

## MEDIUM — Measurable but not framerate-breaking

---

### M-1 — `IntentWidget.Update` calls `SetActive` on child GameObjects every frame via `SetTwinLayout` and `SetVisible`

**File:** `CombatView/IntentWidget.cs`, lines 303–306, 387–407

**Pattern:** Unconditional `SetActive` in `Update` path

**Detail:**
Every frame `IntentWidget.Update` calls a render branch (e.g. `RenderAttack`, `RenderReposition`) which calls `SetTwinLayout(bool twin)`:

```csharp
private void SetTwinLayout(bool twin)
{
    if (_arrowText   != null) _arrowText.gameObject.SetActive(twin);
    if (_targetImage != null) _targetImage.gameObject.SetActive(twin);
}
```

`GameObject.SetActive` is not free when the value changes: it triggers `OnEnable`/`OnDisable` on any MonoBehaviours on those GameObjects, and any toggle triggers a UGUI canvas rebuild of the panel's batch. During normal combat (one intent type held for a whole turn), these calls fire with the same value every frame — Unity short-circuits the internal state but still pays a comparison cost, and if intent type changes between turns, a SetActive flip triggers a canvas batch rebuild.

`SetVisible` at line 387 similarly calls `.enabled` on six Image/TMP_Text components unconditionally every frame.

**Fix pattern:**
Track last-rendered state and guard all SetActive / enabled writes:

```csharp
private bool _lastTwinLayout = true;
private bool _lastVisible = false;

private void SetTwinLayout(bool twin)
{
    if (twin == _lastTwinLayout) return;
    _lastTwinLayout = twin;
    if (_arrowText   != null) _arrowText.gameObject.SetActive(twin);
    if (_targetImage != null) _targetImage.gameObject.SetActive(twin);
}
```

This is the standard "write-once dirty flag" pattern. Apply the same guard in `SetVisible`.

**Estimated impact:** Eliminates UGUI batch rebuild overhead on the intent canvas between turns. Each prevented SetActive(false→true) flip prevents one canvas batch invalidation. Measure in profiler on a turn with a Reposition vs Attack intent change.

---

### M-2 — `CardWidget.Update` writes TMP text strings every frame when card is stable

**File:** `CombatView/CardWidget.cs`, lines 422–426

**Pattern:** Unconditional per-frame TMP text write on stable data

**Detail:**
`CardWidget.Update` runs on every active card widget every frame and unconditionally writes:

```csharp
_costText.text = $"{def.EnergyCost}";
_nameText.text = def.Name;
_infoText.text = BuildInfoText(def, _controller.Loop);
if (_valueText != null) _valueText.text = BuildValueText(def, _controller.Loop);
```

`def.EnergyCost` and `def.Name` are static per card — they never change mid-combat. The `BuildInfoText` and `BuildValueText` calls involve string interpolation and return new strings every frame even when the projected damage and all inputs are identical.

`BuildValueText` specifically calls `DamagePipeline.PreviewDamage` every frame for Attack cards, even on idle frames when no card is being dragged and no game state has changed. With up to 8 card widgets running per frame, this is 8 string allocations plus up to 8 `PreviewDamage` calls per frame.

The widget does guard the visual repaint block with `if (!_hudVisualHidden)`, which is the right intent, but there is no dirty check inside that block.

**Fix pattern:**
Cache the last-rendered `HandCardInstance` reference. Only rebuild text when the card changes or when game state that affects `BuildValueText` changes (e.g. a turn-end event or enemy damage-state change). The existing `AssignCard` / `ClearAssignment` hooks are the natural invalidation points:

```csharp
private HandCardInstance _lastRenderedCard;
private int _lastRenderedEnemy_Corrode; // or whatever drives projected change

public void AssignCard(HandCardInstance card, int handIndex)
{
    _lastRenderedCard = null; // force repaint on next Update
    // ... existing code ...
}
```

For `BuildValueText` specifically: `DamagePipeline.PreviewDamage` only changes when enemy DamageState or buff changes, which happens at turn boundaries. A turn-boundary flag (set by the event the HUD already subscribes to) is sufficient to invalidate.

**Estimated impact:** Eliminates up to 8 string allocations per frame (one per hand slot) during idle player turns. With 5 cards in hand, that is roughly 10–20 bytes × 5 allocations = measurable GC pressure over a 10-turn combat. Confirm in profiler under "GC.Alloc" column.

---

### M-3 — `WheelSpinner.Update` reads `_vehicleVisual.Wheels` list reference each frame and loops by value copy

**File:** `CombatView/WheelSpinner.cs`, lines 50–67

**Pattern:** No allocation, but unnecessary property traversal and list-element null checks per frame

**Detail:**
Every frame `WheelSpinner.Update` calls `_vehicleVisual.Wheels` to get the list, then iterates it checking `w == null` and `w.Rotator != null` and `w.DepthDuplicate != null`. Since the wheel list is stable for the lifetime of the vehicle, all four null checks are effectively dead every frame after the first successful Update.

This is low cost individually (four null comparisons), but with two vehicles in scene, this runs 8–12 null comparisons per frame permanently. More importantly, if `Wheels` is a property with any non-trivial getter (even `return _wheels`), it adds one property call overhead per frame.

**Fix pattern:**
Cache the validated wheel list in `Awake` or at first use, alongside a pre-computed `bool[]` for which rotators and depth duplicates are non-null. Eliminates null checks from the hot path:

```csharp
private Transform[] _rotators;
private Transform[] _depthDuplicates;

private void Awake()
{
    _vehicleVisual = GetComponent<VehicleVisual>();
    _motionState   = GetComponent<VehicleMotionState>();
    CacheWheelRefs();
}

private void CacheWheelRefs()
{
    var wheels = _vehicleVisual?.Wheels;
    if (wheels == null) { _rotators = System.Array.Empty<Transform>(); return; }
    _rotators = new Transform[wheels.Count];
    _depthDuplicates = new Transform[wheels.Count];
    for (int i = 0; i < wheels.Count; i++)
    {
        _rotators[i] = wheels[i]?.Rotator;
        _depthDuplicates[i] = wheels[i]?.DepthDuplicate?.transform;
    }
}
```

`WheelBounce` has the same pattern and benefits from the same fix.

**Estimated impact:** Measure in profiler. Expected to be sub-0.05ms total but the pattern is worth correcting before adding more vehicle variants (biome 2/3 enemies may have more wheels).

---

### M-4 — `DamagePopupSpawner.Show` calls `GetComponentInParent<Canvas>()` on every popup spawn

**File:** `CombatView/DamagePopupSpawner.cs`, line 127

**Pattern:** `GetComponentInParent` on event-driven path (not per-frame, but potentially multiple per turn)

**Detail:**
`DamagePopupSpawner` calls `GetComponentInParent<Canvas>()` on the anchor transform each time a popup is spawned. In a busy combat turn (multi-effect card + splash + enemy attack) this fires 4–8 times per turn. Each call traverses the hierarchy of the world-space canvas on the vehicle prefab.

**Fix pattern:**
Cache the canvas reference at `Awake` or on first call with a null-check guard. One field, one lookup at init time.

**Estimated impact:** Not framerate-critical but is an easy fix that improves worst-case popup-heavy turns (Dredge javelin chain with splash and self-recoil).

---

## LOW — Micro-optimizations / style

---

### L-1 — `CardWidget.GetFamilySprite` uses `Resources.LoadAll<Sprite>` on first card render

**File:** `CombatView/CardWidget.cs`, lines 112–118

**Pattern:** Synchronous `Resources.Load` on main thread at runtime (first-frame spike)

**Detail:**
`GetFamilySprite` is a static method with a `_familySpritesLoaded` bool gate. On the first call (when the first card widget renders), it calls `Resources.LoadAll<Sprite>`. This is a synchronous disk/asset read on the main thread. In combat, the first call happens at the first `Update` tick after `AssignCard` — which is the start of the opening hand animation.

The gate prevents repeated loads. The risk is a single stutter frame at the start of the first combat. With the project already using Addressables (ADR-0008), this is an inconsistency with the approved loading strategy.

**Fix pattern (two options):**
A. Preload in `CombatHud.Awake` by calling `GetFamilySprite(CardFamily.Attack)` (any value triggers the load) before the first draw animation starts, moving the stutter off the player-visible frame.
B. Port to Addressables — card images loaded async via `AssetReferenceT<Sprite>` bundle per ADR-0008. This is the canonical fix but requires the ADR-0008 addressables specialist for the bundle authoring step.

Option A is a one-line warm-up call. Recommend A now, B as part of the card VFX art-pass slice.

**Estimated impact:** Eliminates a one-time stutter on first combat open (measured in load time, not frame budget — depends on PSB size). Not a recurring cost.

---

### L-2 — `HandBeat` coroutines allocate `WaitForSeconds` objects on every animation beat

**File:** `CombatView/HandBeat.cs`, lines 118, 136

**Pattern:** `new WaitForSeconds(...)` allocation per coroutine beat

**Detail:**
```csharp
if (ReflowSettleSec > 0f) yield return new WaitForSeconds(ReflowSettleSec);
```

`ReflowSettleSec` (0.2f) and `HandBeatStaggerSec` (0.05f) are compile-time constants. Each call to `DiscardBurst` and `DrawOne` allocates a new `WaitForSeconds` object. For a 5-card end-of-turn discard + 5-card draw, this produces 10 `WaitForSeconds` allocations per turn boundary.

**Fix pattern:**
Cache as static readonly fields:

```csharp
private static readonly WaitForSeconds WaitReflowSettle = new WaitForSeconds(ReflowSettleSec);
private static readonly WaitForSeconds WaitBeatStagger  = new WaitForSeconds(HandBeatStaggerSec);
```

Note: `WaitForSeconds` caches are only safe when the duration is constant and `Time.timeScale` is not modified mid-animation. Both conditions hold here.

**Estimated impact:** Eliminates ~10 small heap allocations per turn boundary. GC benefit is minor individually but adds up over a 10-turn combat. Low-risk change.

---

### L-3 — `BuffTooltipWidget` calls `DamageState.ToString()` (enum boxing) on hover

**File:** `CombatView/BuffTooltipWidget.cs`, line 234

**Pattern:** Enum `ToString()` allocation on hover path

**Detail:**
```csharp
default: return state.ToString();
```

`DamageState` is an enum. `ToString()` on a non-cached enum value allocates a string on the heap. This is in a `switch`'s default branch — triggered when `DamageState` has a value not covered by the other cases. Not a per-frame cost (only on `OnPointerEnter`) but is a minor GC trigger.

**Fix pattern:**
Extend the switch to cover all `DamageState` values with literal string returns, or use a lookup table. Pre-computed string literals do not allocate.

**Estimated impact:** Negligible individually. Mention to `engine-programmer` for a cleanup pass.

---

### L-4 — `EnemyNumberBadge.Update` calls `int.ToString()` on projected HP display

**File:** `CombatView/EnemyNumberBadge.cs`, line 222

**Pattern:** `int.ToString()` allocation on per-frame path (gated by dirty check)

**Detail:**
```csharp
if (display != _lastRenderedNumber)
{
    if (_numberText != null) _numberText.text = display.ToString();
    _lastRenderedNumber = display;
}
```

The dirty check is correct and this only allocates when HP changes. Good pattern. The only micro-concern is that `display.ToString()` without format specifier allocates a new string on the managed heap. For a widget that updates on damage (not every frame), this is acceptable.

**Fix pattern:**
`SlotTargetRing.cs` (line 222) already has the identical pattern with the `int.MinValue` sentinel. No change needed — the existing dirty-flag guard is correct. Flag for awareness only: if additional numeric HUD widgets are added, follow this same pattern.

**No action required** — this is a note, not a finding.

---

### L-5 — `CardWidget.Update` `BuildInfoText` / `BuildValueText` produce `string.Empty` returns that still trigger TMP dirty-check

**File:** `CombatView/CardWidget.cs`, lines 514–549, 498–511

**Pattern:** Repeated assignment of identical string (potentially triggering TMP dirty path)

**Detail:**
When `BuildInfoText` returns `string.Empty` (e.g., for a no-description Attack card), `_infoText.text = ""` is written every frame. TMP internally checks if the string changed before rebuilding the mesh, so the cost is a string comparison per frame rather than a mesh rebuild. Still unnecessary when the card definition is stable.

This is subsumed by the fix proposed in M-2 (dirty-card guard). No separate action needed.

---

## Subscription Lifecycle Audit

Per memory `feedback_subscription_lifecycle_pairing`: checked all Awake/OnDestroy and OnEnable/OnDisable pairs.

**No violations found** in the swept scripts. Notable correct patterns:
- `CombatHud.Awake` subscribes `RunOverlayEvents.OnOverlayShown/Hidden` and `_controller.OnCombatRebuilt`. `OnDestroy` not visible in the swept portion but the Awake comment explicitly references the pairing rule.
- `HandSequencer` ctor subscribes `_queue.Enqueued`; `Dispose()` unsubscribes and stops the coroutine. Correct POCO lifecycle pattern.
- `VehicleBarStack.RebuildForCurrentVehicle` calls `ClearHandlers()` on every ring and badge before re-wiring — correct cross-beacon subscription hygiene.

No leaks detected in the static pass. Confirm with profiler memory snapshot across a full combat→map→combat cycle.

---

## Save System Audit (ADR-0004)

`SaveSystem.Write.cs` — writes run on a background `Task` via `StartBackgroundConsumer`. The `EnqueueRunStateWrite` call snapshots DTOs synchronously on the calling thread (including `JsonConvert.SerializeObject` inside `WriteWithRetry`) but the actual I/O runs off-main. ADR-0004 contract is respected. No sync I/O on the main thread detected.

---

## RNG Audit (ADR-0003)

Grep for `UnityEngine.Random` in seeded systems: not present in the Combat, Run, or Save namespaces. `System.Random` usage is confined to the appropriate places. No violation detected.

---

## Prioritized Fix List (for `engine-programmer`)

| Priority | Finding | File | Estimated effort |
|---|---|---|---|
| 1 | Dirty-flag guard on `MainBarWidget.Bind/Refresh` | `MainBarWidget.cs` | ~30 min |
| 2 | Name write guard in `VehicleBarStack.Update → Bind` | `MainBarWidget.cs` | ~10 min |
| 3 | Cache `Canvas` in `CardWidget.TryProjectScreenToParentLocal` | `CardWidget.cs` | ~15 min |
| 4 | Dirty-flag guard in `IntentWidget.SetTwinLayout / SetVisible` | `IntentWidget.cs` | ~20 min |
| 5 | Dirty-card guard in `CardWidget.Update` text writes | `CardWidget.cs` | ~1 hr |
| 6 | Preload warm-up call for `GetFamilySprite` in `CombatHud.Awake` | `CombatHud.cs` | ~5 min |
| 7 | Cache wheel refs in `WheelSpinner` / `WheelBounce` | Both files | ~30 min |
| 8 | Cache `WaitForSeconds` in `HandBeat` | `HandBeat.cs` | ~10 min |
| 9 | Cache Canvas in `DamagePopupSpawner` | `DamagePopupSpawner.cs` | ~10 min |
| 10 | Extend `DamageState` switch in `BuffTooltipWidget` | `BuffTooltipWidget.cs` | ~10 min |

---

## What to Measure Next

Before committing any fix: attach the Unity Profiler with Deep Profile disabled (too slow), record a 60-second combat session, and capture:

1. **CPU timeline** — which Update methods take the most time
2. **GC.Alloc column** — confirm string allocations are from `MainBarWidget` and `CardWidget` Update paths
3. **UGUI Batch Rebuild** — check the Canvas Profiler module for rebuild counts per frame on the HUD canvas vs the world-space vehicle canvases
4. **Draw calls** — verify the 200-draw-call budget is not already breached before card VFX land

The static analysis has high confidence in H-1 through H-3. Measure before committing to confirm relative impact of M and L findings.

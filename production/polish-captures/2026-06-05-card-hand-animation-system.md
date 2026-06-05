# Polish Capture: Card Hand Animation System

**Date:** 2026-06-05
**System:** Card combat view layer — widget binding, hand reflow, draw/discard animation orchestration

**Affected paths:**
- `Assets/Scripts/Combat/CombatLoop.cs` — model change: `DrawForEffect` and `DrawHand` switch from `_hand.Add` (append-right) to `_hand.Insert(0, ...)` (prepend-left); `CardDrawn` event fires with index 0
- `Assets/Scripts/CombatView/CardWidget.cs` — Update() loses `hand[_handIndex]` per-frame content rebind; `_currentCard` becomes source of truth; `_handIndex` is reassigned by CombatHud on hand mutation
- `Assets/Scripts/CombatView/CombatHud.cs` — adds event queue + `ProcessEventPipeline()` coroutine + `ReassignWidgetsToCards()` pass; routes CardDiscarded/CardDrawn through the queue instead of immediate-fire handlers
- (Reference, not edited) `Assets/Scripts/Combat/Archetypes/DredgeFrameLayout.cs` — Dredge bar HudAnchor values, unrelated regression noted in session-state but NOT part of this refactor

## Proposed change

Three interdependent pieces ship together as one canonical 1.0 cut. (1) CardWidget binds to **card identity** instead of slot index — `_currentCard` is the truth, `_handIndex` is derived from `hand.IndexOf(_currentCard)` and reassigned on hand mutation. Existing widgets visibly slide between slots instead of swapping content. (2) CombatHud queues model events (CardDiscarded, CardDrawn) and drains them through a **single sequenced coroutine pipeline**: discard → reassign widgets → reflow settle → next draw → reassign → settle → repeat. No parallel-fire chaos. (3) CombatLoop's `DrawForEffect` and `DrawHand` switch to **`_hand.Insert(0, ...)` semantics** so newly drawn cards land at the LEFT side of hand (deck-chip side), existing cards shift right. Per `project_drawn_cards_land_left.md`.

## Final-game picture this serves

This is the canonical Slay-the-Spire baseline that the 1.0 game ships with. Cards visibly *move* between deck, hand, and discard — never teleport, never content-swap. Multi-draw sequences read as discrete arrivals the player can count ("one... two... three"). Left-arrival from the deck chip matches visual intuition: cards flow deck → hand → discard, left to right. The same pipeline serves every draw path the game needs: Draw card play (1–3 cards), EndEnemyTurn full refill (up to 5 cards via staggered burst), Buff-triggered draws. It preserves drag-cast targeting + handoff, drag-back cancel, hover lift, right-click cancel (separate bug, not in scope), and the end-of-turn hand dump. Per `feedback_demo_forward_over_infrastructure.md` — this is the shape we ship with, not scaffolding.

## Authored values being destroyed (or at risk during refactor)

Every numeric constant and behavior gate that's been tuned over multiple sessions. Each entry below must survive into the new code at the exact value listed; if any change is intentional during refactor, note it in the implementation PR.

### CardWidget.cs constants

| Where | Value | Current | Replacement plan |
|---|---|---|---|
| CardWidget.cs:38 | `PlayableTint` | `new Color(1f, 1f, 1f, 1f)` | Preserved verbatim |
| CardWidget.cs:39 | `UnplayableTint` | `new Color(0.55f, 0.55f, 0.55f, 1f)` | Preserved verbatim |
| CardWidget.cs:40 | `HiddenColor` | `new Color(1f, 1f, 1f, 0f)` | Preserved verbatim — used for vacant slots |
| CardWidget.cs:48 | `HoverLiftPx` | `24f` | Preserved verbatim |
| CardWidget.cs:49 | `LerpSpeed` | `12f` | Preserved verbatim — controls slot-to-slot glide AND hover lift |
| CardWidget.cs:54 | `SlideInStartX` | `-1200f` | Preserved verbatim — off-screen-left snap point for first occupy of a vacant slot |

### CombatHud.cs constants

| Where | Value | Current | Replacement plan |
|---|---|---|---|
| CombatHud.cs:26 | `HandCapacity` | `5` | Preserved verbatim — widget pool size |
| CombatHud.cs:27 | `CardSpacingPx` | `180f` | Preserved verbatim — horizontal spacing input to arc layout |
| CombatHud.cs:36 | `CardArcHeightPx` | `35f` | Preserved verbatim — parabolic Y peak at arc center |
| CombatHud.cs:37 | `CardArcRotationDeg` | `3f` | Preserved verbatim — edge-card rotation tilt |

### Arc layout formula (CombatHud.ComputeSlotTransform, lines 1086–1093)

| Aspect | Current | Replacement plan |
|---|---|---|
| `t` parameter | `(handCount == 1) ? 0f : (slotIndex / (float)(handCount - 1)) * 2f - 1f` | Preserved — handles 1-card edge case |
| `xPos` | `t * (CardSpacingPx * (handCount - 1) / 2f)` | Preserved — centered around 0 regardless of count |
| `yArc` | `CardArcHeightPx * (1f - t * t)` | Preserved — parabolic |
| `rotZ` | `-t * CardArcRotationDeg` | Preserved — left cards tilt right, right cards tilt left |

### Behavioral contracts that must survive

| Behavior | Current path | Replacement plan |
|---|---|---|
| Slide-in on first occupy | Update() line 403–407: `!_wasVisible` → snap to `(SlideInStartX, _basePosition.y)`, set `_wasVisible=true` | New path on first widget→card assignment in ReassignWidgetsToCards |
| Hover lift on playable | Update() line 417–419: target = `_basePosition + (0, HoverLiftPx)` when hovered + playable | Preserved — Update still owns position lerp, hover check independent of binding |
| Drag-cast HideVisual handoff | `_hudVisualHidden` flag (line 366), HideVisual line 685, ShowVisual line 705 | Preserved — independent of binding model, must remain real-time bypass of pipeline |
| Drag-back cancel slide | `_slidingBackFromTop` flag + alpha walk toward 1 each frame | Preserved — independent of pipeline |
| IsAnimating gate | Update() line 338: `if (IsAnimating) return;` — coroutine owns position/rotation/alpha during AnimateToDiscard / AnimateFromDeck | Preserved — pipeline coroutines still set IsAnimating; Update still bails |
| AnimateToDiscard target | DiscardChip world position (line 962, snapRight: true) | Preserved — pipeline calls same coroutine, just sequenced |
| AnimateFromDeck source | DeckChip world position (snapRight: false) | Preserved — pipeline calls same coroutine, just sequenced |
| Vacant slot paint | Update() line 343–355: HiddenColor + empty text + `_wasVisible=false` + snap to `_basePosition` | Replacement: vacant state set by ReassignWidgetsToCards when no card maps to widget |
| Card sprite swap per family | Update() line 373: `_background.sprite = GetFamilySprite(_currentCard.Family)` | Preserved — paint logic moves to identity-binding entry point (Bind / Reassign) rather than per-frame Update |
| Cost / Name / Info / Value text | Update() lines 376–379 | Preserved — paint logic moves to identity-binding entry point |
| PaintForAnimation (no playability dim) | Line 780–793: shared by AnimateFromDeck + AnimateToDiscard | Preserved |
| End-of-AnimateToDiscard hidden state | Line 826–833: HiddenColor + empty text + alpha=1 reset | Preserved; reassign path takes over from there |

### Model contract (CombatLoop)

| Contract | Current | Replacement plan |
|---|---|---|
| `DrawForEffect` insert position | `_hand.Add(pulled)` at line 664 → CardDrawn fires with `_hand.Count - 1` (right end) | `_hand.Insert(0, pulled)` → CardDrawn fires with index `0` (left end). Other draws inserted same way push prior draws right by one. |
| `DrawHand` insert position | `_hand.Add(c)` at line 686 → CardDrawn fires with `_hand.Count - 1` | Same change as DrawForEffect — `Insert(0, c)` + CardDrawn(c, 0) |
| `PlayCard` event ordering | Layer 1 already applied: CardDiscarded → RemoveAt → Resolve → Discard.Add (lines 333–355) | Preserved — Layer 1 stays in |
| Hand iteration semantics | Hand was effectively oldest-first (Add to right). | Flipping to newest-first (Insert at 0). Consumer audit: zero callers of `_hand[0]`, `hand[0]`, `_hand.First()`, `hand.First()`, `_hand.RemoveAt(0)` found across all C# code. Safe. Document new semantic in CombatLoop near `_hand` field declaration. |

### Risks the implementation must explicitly mitigate (per TD R1–R5)

| Risk | Mitigation contract |
|---|---|
| Pipeline stall on missing widget/exception | Every `yield return AnimateX()` wrapped with timeout = `max(LerpDuration * 2f, 1.5f)`; log warning on timeout, continue draining |
| Cascading effect event ordering | CardDiscarded fires BEFORE CardDrawn in `PlayCard` — already true per Layer 1; lock in code, not implicit |
| Mid-flight reassign collision | ReassignWidgetsToCards skips widgets where `IsAnimating == true`; in-flight ones settle into their reassigned slot on completion. Re-read target every frame in AnimateFromDeck/ToDiscard, don't cache at start |
| Polish loss | This capture is the safety net. Every constant and behavior above must survive |
| Determinism | Headless test: fixed RunSeed + fixed play sequence → same final hand state across 3 runs. Required before merge |

### TD-mandated quality gates

| Gate | Requirement |
|---|---|
| Old slot-bind code | DELETED, not commented out, not flagged. Per ADR-0011 |
| Reflow settle time | Serialized field on CombatHud (`[SerializeField] float _reflowSettleSeconds = 0.2f`), not a const |
| Pipeline class location | Inside CombatHud. No new orchestrator class unless pipeline grows past ~150 LOC (revisit threshold) |
| End-of-turn DiscardHand | Staggered burst, 50ms between launches, await last to settle. Comment inline as the one exception to event-by-event sequencing |
| Determinism smoke test | One headless EditMode test, fixed seed, fixed play sequence, asserts final hand. Required in same PR |

## Technical Director Review

**Verdict:** APPROVE
**Spawned at:** 2026-06-05
**Agent transcript:** (full TD response below, verbatim)

**TD reasoning summary:**

- Root-cause diagnosis is correct. Slot-indexed binding with per-frame `hand[_handIndex]` rebind is the canonical "content swap" anti-pattern for hand-of-cards UIs; every shipped card game uses card-identity binding. Parallel-fire animation against a live model is the canonical "retarget mid-flight" anti-pattern; fix is universally a sequenced pipeline with settle beats.
- Three pieces are correctly identified as interdependent — any single piece in isolation worsens the symptom.
- Simplicity: ~300 LOC across 3 files, no new classes. Minimum architectural change that solves root cause.
- Performance: neutral-to-positive. Update loses a `List.IndexOf` paint per frame, gains cheap reference read. Pipeline yields when idle. Reflow settle 0.2s well within card-game feel budget.
- Pillar alignment: serves readable card flow (StS baseline), preserves designer intent via the capture, satisfies ADR-0011 no-bridges (clean cut, old code deleted), serves canonical 1.0 shape per `feedback_demo_forward_over_infrastructure.md`.

**TD answers to architectural questions:**

- **Q1 — event queue location:** Single class on CombatHud. CombatHud already owns widget refs, ComputeSlotTransform, and CardDiscarded/CardDrawn subscriptions. Extracting an orchestrator adds coupling for no separation gain. Caveat: revisit if pipeline grows past ~150 LOC.
- **Q2 — reflow settle:** Fixed delay, serialized field. Designer's pacing knob. Epsilon-watch couples pipeline to lerp speed, hard to reason about. StS uses fixed pacing for the same reason.
- **Q3 — EndTurn DiscardHand:** Coordinated burst with 50ms staggered launches. Pure sequential = 2.25s dead time (players will mash through). Pure simultaneous loses the cascade visual. StS pattern. Only place in pipeline that violates strict event-by-event sequencing — comment inline.
- **Q4 — hand iteration order:** Grep audit ran. Zero callers of `_hand[0]`, `hand[0]`, `_hand.First()`, `hand.First()`, `_hand.RemoveAt(0)`. Safe to flip semantics. Document new "newest at 0" semantic at field declaration.

**TD-mandated conditions on approval (all 6 satisfied or owned by implementation):**

1. Polish capture file MUST be written before code touches — **this document satisfies that condition**.
2. Grep audit before model edit — **done, clean**.
3. No bridges — old slot-bind code DELETED, not gated.
4. Determinism smoke test in implementation PR.
5. Single class, no orchestrator extraction unless pipeline > 150 LOC.
6. Reflow settle time as serialized field, not const.

**TD-defined success criteria:**

1. Playing any card: only that card animates to discard. No other widget moves except via reflow lerp to new slot positions.
2. Playing a Draw card: discard plays out, hand reflows, then drawn cards fly in from deck chip *one at a time, left-side occupation*, each visible as a discrete arrival.
3. EndEnemyTurn refill of 5 cards: staggered burst arrives from deck chip, reads as a single coherent "drawing a hand" beat.
4. Drag-cancel still slides back with alpha fade. Hover lift still works. Drag-cast crosshair handoff still works.
5. Zero "widget content swap" visual events across a 10-turn combat session.
6. Frame time during pipeline animations stays under 16.6ms.
7. Determinism: same RunSeed + same play sequence → same final hand state across 3 runs.

**TD risks called out (R1–R5):**

- **R1 Pipeline starvation:** every awaited animation wrapped with timeout; log + continue on timeout
- **R2 Event ordering during cascading effects:** lock CardDiscarded-then-CardDrawn explicitly, not implicit
- **R3 Mid-flight reassign collision:** ReassignWidgetsToCards skips IsAnimating widgets; in-flight animations re-read target every frame
- **R4 Polish loss:** this capture file is the safety net
- **R5 Determinism:** headless seed test required before merge

**Full TD response (verbatim):**

> TD-ARCHITECTURE: APPROVE
>
> **APPROVE the three-piece refactor as proposed.** The root-cause diagnosis is correct, the three pieces are correctly identified as interdependent (any single piece in isolation worsens the symptom), and the proposed shape matches the canonical 1.0 picture per `feedback_demo_forward_over_infrastructure.md`. Proceed to polish-capture, then implementation.
>
> This is exactly the kind of system where incremental seam-patching produces compounding debt — the symptom shape (teleporting content, retargeting mid-flight, scattered draws) is a direct emission of the slot-indexed-binding + parallel-fire combination, and no amount of `_wasVisible` flag tuning will solve it. The architectural cut is the right move.
>
> [Reasoning, Q1–Q4 answers, R1–R5 risks, success criteria, and 6 conditions captured in the structured sections above]

## User approval

- Reviewed: 2026-06-05
- Approved by: bertanberkol@gmail.com
- Notes: Approved for full implementation (Layer 2a–2e + polish audit). All 6 TD conditions binding.

---

## Implementation order (post-approval)

1. **Layer 2a — Card-identity binding (CardWidget + CombatHud minimal)**: change Update() to stop reading `hand[_handIndex]`; introduce `ReassignWidgetsToCards()` called from CombatHud event handlers. Validate hand still works on plays before adding pipeline.
2. **Layer 2b — Event pipeline (CombatHud)**: route CardDiscarded/CardDrawn through queue + `ProcessEventPipeline()` coroutine. Add `_reflowSettleSeconds` serialized field.
3. **Layer 2c — Staggered EndTurn burst**: special-case `EndTurnDiscardAll` in pipeline with 50ms launch stagger.
4. **Layer 2d — Model prepend**: `DrawForEffect` and `DrawHand` switch to `_hand.Insert(0, ...)` + CardDrawn(card, 0). Document semantic in CombatLoop.
5. **Layer 2e — Determinism smoke test**: EditMode test with fixed seed + fixed play sequence, asserts final hand.
6. **Polish audit**: walk every row in the "authored values being destroyed" table, verify the new code preserves it.

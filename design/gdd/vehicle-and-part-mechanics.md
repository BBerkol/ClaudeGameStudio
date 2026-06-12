# Vehicle & Part System — Mechanics

> **Status**: Approved (R8 APPROVED; W9.1 bundled fixes applied — no re-review required)
> **Author**: Bertan Berkol + Claude Code agents
> **Last Updated**: 2026-05-21 (W9 — C.1 cross-field OnValidate guard remediation: auto-clamp CriticalThresholdPct (not log-only); Tuning Knobs CriticalEnergySurcharge safe range 0–2→0–1; Tuning Knobs ScrapRefundRate lower bound [0.0,0.99]→[0.20,0.99]; E.5 Rare-to-Rare pivot worked examples (98/122 Scrap); E.9 repair-card death spiral = valid consequence state declared; C.3 HUD Offline Tray: zero-state=invisible + interim badge-only + gate condition; C.3 Workshop inspector Offline tooltip split (combat-HUD vs Workshop copy); C.3 Healthy/Critical hint: "secondary tooltip line"→"always-visible secondary label in focused-slot inspector panel"; AC-VPM04 assertion (3) BLOCKED label added; AC-VPM09 pending element identifier noted; AC-VPM-A sub-case event-log ordering assertion added; AC-VPM-E lower-bound test added; new ACs: AC-VPM-F click-through, AC-VPM-G Light-tier stagger, AC-VPM-H cross-field auto-clamp; PVH-1/PVH-2/PVH-3 added)
> **Implements Pillar**: Pillar 1 (Vehicle as Character), Pillar 4 (Scarcity with Agency), Pillar 5 (Route Reflects Vehicle State)
> **Architecture Contract**: design/gdd/vehicle-and-part-architecture.md (Approved — R4)
> **ADR**: ADR-0007 (Accepted — architecture surface only)

---

## Overview

The Vehicle & Part Mechanics document defines the experiential layer of the vehicle system — the rules the player sees, feels, and decides within. Where the architecture document (`vehicle-and-part-architecture.md`) defines the data contracts and event model, this document defines the meaning those contracts produce for the player: what a Critical slot costs you, how a Destroyed slot shrinks your hand by removing cards from the draw pool entirely, how the install economy creates genuine trade-offs as your vehicle fills up, and what happens to your run when a schema migration drops a slot mid-campaign. This document owns: the three damage states and their mechanical effects; the slot soft-disable and hard-removal rules; the IsPlayable visual affordance vocabulary; the install/remove/salvage economy (including the InstallCost formula and the static salvage-refund formula); the granted-card lifecycle; the Armor contribution scaling formula; and the event-level audio/visual feedback specification. It is the primary design reference for combat HUD, the vehicle inspector screen, and any run event that mutates a vehicle's slot state.

---

## Player Fantasy

Your vehicle is a body, and its parts are organs. When a slot reaches Critical, every card it grants costs one extra energy to play. That is the triage decision: repair now, or pay deeper and hope you survive another turn? When a part is Destroyed, the cards it granted leave your draw pool entirely — not just greyed out in hand, but absent from the deck you're building from. You didn't lose a stat point; you lost a tool, and the hand available to you is now genuinely smaller.

The install economy is not a friction layer — it is a commitment system. As your vehicle fills up, each additional part costs more than the last — not because of a specific slot type, but because a more complete vehicle is harder to modify. The cost of your next install scales with how complete your vehicle already is: a half-full 5-slot chassis costs about 24% more per install than an empty one; as the vehicle approaches full, each install costs up to 45–60% more than the base price (exact percentage depends on frame size). The vehicle you build over a run is a series of bets: each part you add is an escalating investment in the configuration you're committing to. A player who installs recklessly and then wants to pivot mid-run pays for that indecision in Scrap. Salvaging a part always returns the same flat amount for its rarity tier — Common returns 4 Scrap regardless of when you installed it or how full your vehicle was. The install cost rises with vehicle fill; the refund does not. A part installed at a full vehicle cost more to put in — removing it recovers the same flat 4 Scrap, leaving a larger absolute net loss. Early installs are cheap mistakes; late installs are expensive ones. The commitment is priced into the install, not the refund.

If every slot is Destroyed simultaneously — all parts Offline at once — there are no cards left to draw. Your turn is forcibly skipped while the enemy acts. The game does not end instantly: a relic, a repair card from an external source, or some other intervention may still exist. If nothing does, the enemy reduces your hull to zero over successive turns. All-Offline is the vehicle dying — not a puzzle to solve, but a consequence to read coming. It is by design that you see the end before it arrives.

Every interaction with the vehicle's slot state should land with physical weight. The sound of a Destroyed slot. The muted absence of a card that was in your deck a moment ago. The quiet relief of a repair that brings a slot back from Critical. These are not UI notifications — they are events in a fight for survival that the player is invested in because the vehicle is theirs.

---

## Detailed Rules

### C.1 Damage State Model

Each slot instance has a `DamageState` computed from `CurrentHp / MaxHp` against the Critical threshold authored on `FrameLayoutSO` (`CriticalThresholdPct`, default 20). States are read-only derived values — they are never stored, only computed on read.

| State | Condition | Mechanical Effect | Player-Facing Signal |
|---|---|---|---|
| `Healthy` | `(float)CurrentHp / MaxHp > CriticalThresholdPct / 100f` | None | Slot indicator full-color |
| `Critical` | `0 < (float)CurrentHp / MaxHp ≤ CriticalThresholdPct / 100f` | **+1 energy surcharge** — cards from this slot cost 1 additional energy to play; non-compounding per card (see below) | Slot indicator red, pulsing; part sprite shows heavy damage; affected cards show red energy-cost badge |
| `Destroyed` | `CurrentHp == 0` | Slot enters Offline state (see C.2); granted cards moved to Offline zone (see C.5) | Slot indicator dark/cracked; granted cards absent from hand/deck/discard; HUD shows Offline card count |

**Visual wear:** `FrameLayoutSO` retains a `DegradedThresholdPct` (default 50) as a **visual-only** parameter. When `(float)CurrentHp / MaxHp` crosses below this threshold, the part sprite transitions to a worn visual state. This is a purely cosmetic signal — it does not correspond to a separate `DamageState` and carries no mechanical effect. It can be tuned independently of `CriticalThresholdPct`.

**Non-compounding surcharge (per card):** Each card from a Critical slot costs `BaseCost + CriticalEnergySurcharge` energy to play. The surcharge is applied independently per card, keyed to that card's source slot state at play time. **Worked example:** A player has 3 energy and two cards in hand — card A from Critical slot X, card B from Critical slot Y (both with `BaseCost = 1`, `CriticalEnergySurcharge = 1`). Playing card A costs 2 energy; playing card B also costs 2 energy — a total of 4 energy to play both. "Non-compounding" means: the per-card surcharge is always +1 regardless of how many Critical slots exist simultaneously — having three Critical slots never makes any single card cost +3.

**Turn-budget compression (intentional design):** The per-card surcharge does not compound per card, but the aggregate effect on a turn with multiple Critical slots is real and by design. A player with four Critical slots whose entire hand costs 2 energy per card cannot play more than one card on a 3-energy budget. This is the intended crisis state: multiple failing systems compress the turn into a forced triage. It does not trigger the All-Offline forced-pass path (cards are still in hand) — it is a soft pressure that demands the player act differently. The game includes rare rewards that increase the per-turn energy budget beyond 3; a player who reaches a Critical-heavy state while holding an energy-boost relic or high-energy-grant card has a meaningful decision. A player who does not has a meaningful consequence to read coming.

**Triage example:** Player has 3 energy. Hand contains card A (BaseCost = 2, source slot Critical — total cost 3 energy) and card B (BaseCost = 1, source slot Healthy — total cost 1 energy). Without the surcharge, both cards together cost 3 energy and both can be played. With the Critical surcharge: card A alone costs all 3 energy, leaving nothing for card B. Playing card B first (1 energy spent, 2 remaining) still leaves card A unaffordable — it needs 3. Either choice plays only one card instead of two. The Critical slot has collapsed a two-card turn into a forced one-card decision; that is the triage.

**Float cast requirement:** `CurrentHp` and `MaxHp` are `int` fields. All threshold comparisons must cast to `float` before dividing — integer division returns `0` for all non-full-health values, incorrectly placing all damaged slots into the Destroyed state. The notation `(float)CurrentHp / MaxHp` is mandatory in implementation.

**Structural slots at Destroyed:** A structural slot (`IsStructural = true`) contributes `0` to `StructuralHp`. When the sum of all structural slot `CurrentHp` reaches `0`, the vehicle dies (see architecture §6.3). This is the only state transition with a run-ending consequence.

**State transitions are instantaneous.** There is no animation delay between a damage event resolving and `DamageState` updating. Visual feedback (C.6) plays on the state change event — the state itself is always current.

**Runtime threshold guard:** `FrameLayoutSO.OnValidate` enforces `CriticalThresholdPct ∈ [1, 99]` at SO import. At runtime, if a `CriticalThresholdPct` of 0 or negative is encountered (via save migration, mod, or bug bypassing the SO importer), the DamageState predicate must log an error and treat the threshold as `1`. A threshold of `0` makes Critical structurally unreachable — the condition `0 < ratio ≤ 0` is always false — which is a silent mechanical failure.

**Cross-field OnValidate guard:** `FrameLayoutSO.OnValidate` must also enforce `Floor(MaxHp × CriticalThresholdPct / 100) ≥ 1`. This guard catches the edge case where `MaxHp = 1` and `CriticalThresholdPct = 99` are each individually valid — both pass the per-field range checks — but the combination makes Critical mechanically equivalent to Destroyed: the only HP value that would qualify as Critical (`ratio ≤ 0.99`) is `CurrentHp = 0`, which is the Destroyed condition. Any slot at `CurrentHp = 1` evaluates as `1.00 > 0.99` → Healthy, then immediately Destroyed at `CurrentHp = 0` with no Critical window. **Remediation (auto-clamp):** When the guard fires, `FrameLayoutSO.OnValidate` must clamp `CriticalThresholdPct` upward to `Ceiling(100 / MaxHp)` and log a warning naming the adjusted value: `"CriticalThresholdPct clamped from [original] to [clamped] to preserve a valid Critical window (MaxHp=[MaxHp])."` The SO is saved in the clamped state; no manual designer correction is required. This mirrors the per-field clamp behavior of the single-field guards. See AC-VPM-H for the test verifying this path.

**MaxHp = 1 edge case:** If `MaxHp = 1`, `Ceiling(100 / MaxHp) = 100`, which conflicts with the per-field guard capping `CriticalThresholdPct` at 99. No valid `CriticalThresholdPct` satisfies both guards simultaneously — this configuration has no reachable Critical window. `FrameLayoutSO.OnValidate` must raise a distinct error: `"MaxHp=1 does not support a Critical window — set MaxHp ≥ 2."` No clamp is attempted; the field values are left unchanged pending designer correction.

### C.2 Soft-Disable Lifecycle (Offline)

A slot enters **Offline** when `CurrentHp == 0` (i.e., `DamageState == Destroyed`). Offline is not a separate stored flag — it is the direct consequence of the Destroyed state.

**What Offline means:**
- The slot no longer generates cards. New cards from this part's generation logic are not added to the draw pool for the remainder of the encounter or run node.
- All cards with `SourceSlotId` matching this slot are moved to the **Offline zone** (see data structure below). They do not count toward hand size. They are not drawn and cannot be played. The HUD slot anchor shows an Offline card count badge for the part (e.g., *"3 cards offline"*).
- The slot's HUD anchor goes dark. The part sprite renders in a destroyed visual state (cracked, unlit, or absent depending on art direction).
- The part itself remains installed. It is not ejected, lost, or replaced by Offline. The install slot is occupied.

**What survives Offline:**
- The part's identity (`PartId`, `SlotKind`, all `PartDefinitionSO` fields) is preserved.
- `CurrentHp` is `0` but `MaxHp` is unchanged — repair targets the full `MaxHp` range.
- Armor contribution from this slot drops to `0` for as long as it remains Offline (see F-VPM4).

**Restoration (Repair):**
- Any `IVehicleMutator.Repair(slot, amount)` call that raises `CurrentHp` above `0` immediately ends the Offline state.
- On the first point of Hp restored, the slot transitions: `DamageState` re-derives to Critical/Healthy based on the new `CurrentHp`. All cards in the Offline zone for this slot are **shuffled back into the draw pile** (not returned to hand — they re-enter the deck at random positions). The HUD indicator reactivates.
- Restoration is instantaneous at the data layer; visual feedback (C.6) plays on the `OnSlotHpChanged` event.
- The part resumes card generation at the next card-grant opportunity (start of next encounter turn or node card grant, depending on the card-generation trigger owned by Card Combat System).

**Offline is not destruction of the part.** The player does not lose loot, does not lose deck content permanently, and does not lose the install slot. Permanent loss only occurs on explicit Scrap (see C.4).

**Offline zone data structure:**
- **Structure:** Per-slot. Each slot tracks its own Offline zone as a `List<CardId> OfflineCards` on the slot's state DTO. One list per slot — not a global pool.
- **Order:** Unordered. Cards are shuffled into the draw pile randomly on repair regardless of insertion order.
- **Maximum size:** Bounded by the slot's granted-card count. Cannot overflow.
- **Serialization:** Stored as `OfflineCards: List<CardId>` on each `SlotStateDTO` per ADR-0004. Save & Persistence must include this field in the slot DTO schema. This is a required `SlotStateDTO` field (see reverse dependencies).

### C.3 IsPlayable Affordances

`IsPlayable` is a derived boolean on a slot (see architecture §3.4): a slot is playable when it is installed, has a part, `CurrentHp > 0`, and no external disabling condition is active. This section defines how the UI communicates each non-playable cause to the player.

**Slot indicator states (HUD):**

| Cause | Slot Indicator | Tooltip / Error Text |
|---|---|---|
| `Healthy` / `Critical` | Full-color / red-pulsing | — (no error state) |
| `Destroyed` (Offline) | Dark, cracked overlay | "Destroyed — restore via Repair cards in combat or a Chopshop" |
| No part installed | Empty socket art | "Empty — install a part at a Workshop" |
| External disable (e.g. enemy debuff) | Dimmed with status icon | "[Status name] — [duration] turns remaining" |

**Offline cards:** When a slot enters Offline, all cards from that slot are moved to the Offline zone — they are absent from hand, deck, and discard and do not count toward hand size (see C.2, C.5). The HUD slot indicator displays an Offline card count badge overlaid directly on the slot's HUD anchor icon (a number with a lock icon, e.g., "3"). The badge appears when the slot enters Offline and disappears when the slot is repaired and the `OfflineCards` list is empty.

**HUD Offline Tray (required forward dependency):** The HUD must include an Offline Tray — a collapsed strip outside the hand area showing the icons of all cards currently in any slot's Offline zone, labeled with their source slot badge. The Tray provides card-identity visibility (which specific cards are absent) without including those cards in hand math. **Zero-state:** The Tray is **invisible** when no cards are Offline — it does not render a "0" badge or empty container; it appears only when at least one card is in any slot's Offline zone. **Interim behavior (until `design/ux/hud.md` is authored):** Implement badge-only count on the slot indicator (a number with lock icon, e.g., "3") as the sole Offline signal; the Tray strip itself is not built until `hud.md` signs off. **Gate condition:** The Tray may not be implemented before `design/ux/hud.md` is authored and approved — strip positioning, card-icon layout, and collapse animation are UX spec decisions. Badge-only is the implementation floor for Combat HUD. Full UX spec: `design/ux/hud.md` (to be authored).

**Externally disabled cards** (source slot NOT Offline — enemy debuff, relic interaction): the card remains in its current zone (hand/deck/discard), renders greyed with a status icon, and shows an inline error on play attempt: *"[Status name] — [turns remaining]."* This is a separate mechanism from Offline and does not use the Offline zone.

**Install affordances (Workshop):**

- An empty slot shows a pulsing socket indicator and the label *"Empty slot — select to install"* in the vehicle inspector.
- An Offline slot (part present, `CurrentHp == 0`) shows the part name in red with a wrench icon. **Copy is context-specific — two separate strings:** (1) **Workshop inspector** (non-combat node): *"[Part name] — Destroyed. Remove to free this slot. Restore via Repair cards in combat or at a Chopshop."* (2) **Combat HUD slot indicator tooltip**: *"Destroyed — restore via Repair cards or a Chopshop."* The Workshop copy avoids naming "combat Repair cards" while the player is at a non-combat node.
- A Healthy/Critical slot shows the part name with its current `DamageState` color and an **always-visible secondary label in the focused-slot inspector panel**: *"Remove to swap for a different part."* This label is not a hover tooltip — it renders as a persistent sub-label whenever the slot is focused. **"Focused" definition:** focused means the slot is currently under the inspector's traversal focus ring (D-pad/Tab navigation), not confirmed selection (A/Cross or click). The label updates as focus moves — it is visible during navigation before the player commits to opening a slot. This ensures gamepad players (who cannot hover) have access to the upgrade path affordance.
- Attempting to install a part into an already-occupied slot (any state) is not possible via UI — the install button is hidden, not greyed. The slot must be freed via the Remove flow first.

**Remove affordance (inspector label):**
- **Remove** — selecting Remove opens a confirm dialog (see E.3). Available only at Workshop nodes. See E.3 for the full dialog spec including keyboard shortcuts and dismiss gestures.

**Position-requirement affordance (cards):** Cards with `PositionRequirement == RequiresAhead` or `RequiresBehind` dim when the condition is not met and show inline: *"Requires enemy [ahead / behind]."* This is a Card Combat System responsibility referenced here for completeness — the mechanic is owned by card-combat.md.

### C.4 Install, Remove, and Salvage

**Install flow:**

1. Player selects an empty slot in the vehicle inspector at a Workshop node.
2. The inspector shows available parts from the current node's loot pool (parts found this run, offered as rewards, or available for purchase).
3. Each part displays: `SlotKind` compatibility indicator, stat preview, InstallCost in Scrap (see scrap-economy.md D.2), and the cards it grants.

   **Stat preview rules:**
   - If the destination slot is **empty**: show absolute values for all applicable stats.
   - If the destination slot is **occupied and Healthy/Critical**: show delta vs. the installed part's **base stat values** (from `PartDefinitionSO`, not effective values scaled by HP). Example: `Armor: 12 → 17`. Show all stats — including stats with zero delta (display as `Armor: 12 = 12`). Omitting zero-delta stats hides whether the new part has that stat at all.
   - If the destination slot is **occupied and Offline** (`CurrentHp = 0`): compare against the installed part's **base stat values** (not effective values, which would all be 0 or near-0). Label the Offline state explicitly with a **block-level "(Destroyed)" label at the top of the preview panel** — not repeated per stat line — making the destroyed state unmistakable before any stat is read. **Multi-stat display:** delta arrows apply normally per stat (e.g., `Armor: 12 → 17` / `MaxHp: 8 → 5`). **Absent-stat handling:** stats present in the candidate part but absent in the Offline part (i.e., the Offline part has no value for that stat) are **omitted from the delta column** and listed instead under a separate **"New from candidate:"** subsection without delta arrows (e.g., `SpeedBonus: 3`). This prevents the UI from showing `SpeedBonus: 0 → 3`, which would misrepresent an addition as an upgrade over a zero baseline.
   - Stats not applicable to the part's `SlotKind` are omitted.
4. Player confirms install. `InstallCost` Scrap is deducted. The part is installed; granted cards are added to the deck immediately (shuffled into the draw pile).
5. If the player cannot afford `InstallCost`, the install button is disabled with inline text: *"Requires [cost] Scrap."*
6. Install is only available at **Workshop nodes**. It is not available mid-combat.

**InstallCost trigger:** `InstallCost` is computed at install time via scrap-economy.md D.2. Inputs supplied by this system: `rarity` (from the part's `PartDefinitionSO`), `installedCount` (total non-Empty slots across all SlotKinds at the moment of install, not counting the slot being filled — see F-VPM1), and `totalSlots` (from the active `FrameLayoutSO`).

**Remove / Salvage flow:**

1. Player selects an occupied slot in the vehicle inspector.
2. Player selects **Remove**. A confirm dialog appears (see E.3): *"Salvage [Part name] — Irreversible."*
3. Player confirms. The part is destroyed, `SalvageAmount` Scrap is awarded (see scrap-economy.md D.6 via F-VPM2). The slot is freed. Granted cards hard-removed from all zones (deck, hand, discard, and Offline zone if applicable — see C.5).
4. There is no part inventory — the part is gone. Remove is only available at **Workshop nodes**.

**Install-over-occupied-slot (future path — not currently accessible):** The install button is hidden on occupied slots per C.3/E.1 — there is no UI path to trigger this scenario in the current design. This block is retained as a stub for a potential future "Replace" single-action flow. If a Replace flow is implemented: (a) no Scrap refund is issued for the replaced part, and (b) the confirm dialog must display *"The installed [Part name] will be destroyed. You will receive no Scrap for it."* Players who want the salvage refund must use the Remove flow above before installing a replacement.

**Salvage amount:** Computed by scrap-economy.md D.6 `ScrapRefund(rarity)` (see F-VPM2). The refund is always a flat percentage of the rarity base cost — always a loss relative to install cost — removal is a build correction, not a resource rotation. The inspector displays the exact Salvage amount before the player confirms.

**Schema-drift removal (automatic):**

When a save is loaded and the `FrameLayoutSO` no longer contains a `SlotId` that the save references, the missing slot is automatically dropped (see architecture §6.4–6.5). The player is notified on load: *"[N] slot(s) were removed because your vehicle's frame was updated. You received [X] Scrap."* The refund is computed by scrap-economy.md D.6 (same static formula as manual Salvage — `ScrapRefund(rarity)`, see F-VPM2). Schema drift is rare but must never silently destroy player progress — the notification and refund are non-negotiable.

### C.5 Granted-Card Lifecycle

Parts grant cards to the player's deck on install. The lifecycle of those cards tracks the slot's state through two regimes: **soft-disable** (reversible) and **hard-remove** (permanent).

**Soft-disable (Slot Offline):**

When a slot enters Offline (`CurrentHp == 0`), all cards with `SourceSlotId` matching that slot are moved to the **Offline zone** — a separate non-drawable pool outside of hand, deck, and discard (see C.2 for data structure). Cards in the Offline zone do not count toward hand size, are not drawn, and cannot be played. The move is immediate and atomic on the `OnSlotHpChanged` event (hp → 0).

When the slot is repaired (`CurrentHp > 0`), all cards in the Offline zone for that slot are **shuffled back into the draw pile**. They are not restored to their prior zone — regardless of whether they were in hand, deck, or discard when the slot went Offline, they re-enter as deck cards at random positions.

**Hard-remove triggers (permanent):**

Cards are hard-removed from deck, hand, discard, and Offline zone atomically in two cases only:

1. **Salvage** — the player explicitly salvages the part (via Remove flow, C.4). All cards with matching `SourceSlotId` are swept from all zones simultaneously, including the Offline zone. This is a deliberate player action with a confirm dialog.
2. **External-source termination** — a game event explicitly ends a non-slot grant (e.g., the Dredge Javelin tether is cut between nodes, ending the chain of Javelin cards it granted). The `IVehicleMutator.HardRemoveCards(sourceSlotId, cardIds)` call sweeps the specified cards from all zones. `SourceSlotId` is nullable — non-slot grants use `null` and provide an explicit `cardIds` list.

Hard-remove is silent at the data layer but always paired with a node-level narrative beat or confirm dialog at the call site. The architecture guarantees atomicity — a hard-remove either completes across all zones or throws; it never leaves partial state.

**Energy surcharge (Critical):** Cards from a Critical slot cost `BaseCost + CriticalEnergySurcharge` energy to play. `CriticalEnergySurcharge` defaults to 1. The surcharge is applied independently per card keyed to the card's source slot DamageState. If two cards from two different Critical slots are played in a turn, each card pays +1 — "non-compounding" means the per-card surcharge is always +`CriticalEnergySurcharge` regardless of how many Critical slots exist simultaneously. A card is never charged more than one surcharge regardless of how many of its source-slot's siblings are also Critical. A card with `BaseCost = 0` and a Critical source slot costs `CriticalEnergySurcharge` energy to play (default: 1).

**Gate composition:** A granted card is playable only when ALL of the following are true, evaluated in this order:
1. **External disable gate** — no active external disable condition (`ExternalDisableStatus == null`). If an external disable is present, the card remains in hand but is greyed; the displayed error is `[Status name] — [turns remaining]` (see C.3). External disable takes display priority over position and energy errors.
2. **Offline zone gate** — card is in hand, deck, or discard (not in the Offline zone). Cards in the Offline zone are absent from hand; there is no play attempt to reject.
3. **Position requirement gate** — position requirement is met, or requirement is `None`/`BonusIf*` (positional gate — advisory for Bonus variants).
4. **Energy gate** — player has sufficient energy, including any Critical surcharge.

The source-slot Offline check does not appear as a gate error — Offline cards are absent from hand, so there is no play attempt to reject.

### C.6 Audio and Visual Feedback

This section specifies the feedback events and their required signal tier. Actual SFX, music cues, and animation curves are authored by the Audio Director and Technical Artist against these specs.

**Signal tiers:**
- **Heavy** — screen-space impact, distinct SFX, held animation. Reserved for high-stakes state changes.
- **Medium** — localised VFX + SFX, brief animation. Standard feedback for meaningful events.
- **Light** — subtle SFX or particle, no screen shake. Used for informational state changes.
- **Silent** — data-layer event only; no player-facing feedback.

| Event | Trigger | Tier | Notes |
|---|---|---|---|
| Slot takes damage (non-Destroying) | `OnSlotHpChanged` (hp decreases, was >0) | Medium | Part sprite flashes damage tint; slot indicator updates |
| Part visual wear (DegradedThresholdPct crossed) | `OnSlotHpChanged` (ratio crosses DegradedThresholdPct) | Silent | Part sprite transitions to worn visual state; no SFX, no indicator color change; purely cosmetic (not a DamageState) |
| Slot enters Critical | `OnCriticalStateChanged` (→ Critical) | Medium | Slot indicator begins pulsing red; ambient warning audio loop begins on `OnCriticalStateChanged (→ Critical)`. **Polyphony architecture (committed): Single shared loop.** One loop plays regardless of how many slots are simultaneously Critical — the loop begins on the first Critical-entry event and ends when the last Critical slot exits. Slot count is not communicated through separate loop instances. **Loop exit:** loop fades on `OnCriticalStateChanged (→ Healthy)` or `OnSlotHpChanged (hp → 0)` for the last remaining Critical slot. If a Critical slot is Destroyed while the loop is playing, the loop continues until all other Critical slots also exit. **Destroyed slot exit rule:** A Destroyed slot is removed from the active-Critical count at the moment of destruction (`hp → 0`), even though it does not emit `OnCriticalStateChanged → Healthy`. The loop terminates when active-Critical count reaches zero by any combination of Healthy transitions and Destructions. **Per-entry SFX behavior:** the audio onset stinger (if any) fires once per loop-start — on the first Critical-entry event only. Subsequent Critical entries while the loop is active trigger no additional SFX; the shared loop is the sole audio representation of the Critical state. This applies to simultaneous multi-slot Critical entries and to subsequent single-slot entries while the loop runs. **Do not implement the audio loop until W3g specifies the exit crossfade and Destroyed-while-looping behavior.** The single-shared-loop polyphony architecture is committed here; W3g inherits a constrained brief, not a blank slate. The trigger event itself may be implemented now. **`OnCriticalStateChanged` is a first-class event** on the `IVehicleEventBus` (see architecture §4.3) — it is not derived at the call site from `OnSlotHpChanged`. Verify its presence in the architecture event table before implementing. |
| Slot Destroyed (enters Offline) | `OnSlotHpChanged` (hp → 0) | Heavy | Part destruction VFX; SFX distinct from damage hit: metallic impact character, NOT in the salvage/scrapping sonic family (see Part scrapped row); duration class stinger 0.5–2s with tail; full cue contract (exit behavior, mix priority) authored in W3g — see audio spec note; Offline card count badge appears on HUD anchor |
| Slot repaired (Offline → any) | `OnSlotHpChanged` (0 → >0) | Heavy | Part restore VFX; relief audio cue (positive sonic direction required in W3g); cards shuffled into draw pile (cards were absent from hand — no in-hand un-grey animation); Offline count badge disappears |
| Slot repaired (non-Offline) | `OnSlotHpChanged` (hp increases, was >0) | Light | Slot indicator brightens; subtle repair SFX |
| Part installed | Install confirmed | Medium | Part snap-in animation; install SFX; cards added to deck notification |
| Part scrapped | Remove confirmed | Medium | Salvage SFX — scraping metal clatter, brief rattle (< 0.75s), resourceful and transactional in character. Draws from the Chopshop/salvage sonic family, **not** the combat Destroyed family. Communicates a deliberate player reclamation action, not catastrophic loss. Must be sonically distinct from Slot Destroyed — they must not share destruction vocabulary. Exact asset spec: W3g. Cards swept from all zones (hand/deck/discard/Offline) atomically — no visible sweep animation for cards in deck or Offline zone. |
| All systems offline (forced-pass) | `OnForcedPassBegin` | Medium | "All systems offline — turn passed" banner displayed for 1.5 seconds (non-dismissable); audio cue distinct from combat damage (spec TBD W3g). **Repeat-turn behavior:** the audio cue fires on every forced-pass turn — it is not suppressed after the first occurrence. Banner text and audio are identical on each repeat. |
| Vehicle death (`StructuralHp == 0`) | `OnVehicleDied` | Heavy | Owned by Card Combat System — referenced here for completeness |
| Schema-drift slot drop (on load) | Load-time notification | Medium | Notification banner on map screen; audio cue to draw player attention to banner (spec TBD W3g) |
| Cards absent from hand (slot enters Offline) | Slot enters Offline | Light | Brief flash/particle at each card's last screen position before the card disappears from hand; one flash per card removed; no text. Provides visible per-card feedback that specific cards were swept. Offline count badge on HUD reinforces the slot-level signal. |
| Cards return to draw pile (slot repaired from Offline) | Slot repairs from Offline | Light | Subtle chime on slot indicator reactivation; no per-card animation (cards enter draw pile, not hand) |

**Audio spec note (W3g):** C.6 requires a dedicated audio production pass before implementation. Required W3g deliverables: (1) Critical state loop contract (exit crossfade duration, Destroyed-while-looping behavior — polyphony architecture is committed as single shared loop); (2) Heavy-tier cue sonic direction: for Slot Destroyed — metallic impact, distinct from part-scrapped vocabulary; for Slot repaired from Offline — relief/restoration character, brief lift. Both cues must align with RUST ICON palette (found-object, weathered, physically tactile). Duration class: stinger 0.5–2s with tail. (3) Medium-tier audio for forced-pass (consequence notification, not impact) and schema-drift events; (4) polyphony behavior for multi-slot simultaneous Destroy events (same-frame, same tier — cap at 2 per simultaneous-feedback rule); (5) W3g authoring order: commit Slot Destroyed SFX first; then commission Part scrapped SFX from the Chopshop/salvage sonic family (not the Destroyed family — brief rattle, < 0.75s, resourceful). Full audio contract to be specified in `vehicle-and-part-audio.md` (W3g, pending audio-director sign-off). **Mechanics implementation is not blocked by the audio spec** — event triggers, tier classifications, polyphony architecture, and the simultaneous feedback rule in this table are sufficient for programming work to proceed.

**Tier rationale — forced-pass is Medium, not Heavy:** Forced-pass is assigned Medium because it is a consequence notification, not a new impact event. The Heavy events (Slot Destroyed, Slot repaired from Offline) carry the emotional weight of the state transitions that cause all-Offline. Forced-pass fires after those Heavy cues have already landed — it is the banner reading out the result, not the event itself. Treating it as Heavy would double the dramatic peak on an already-catastrophic sequence.

**Simultaneous feedback rule:** When a single `ApplyDamage` call triggers multiple state transitions (e.g., a hit that takes a slot from Healthy to Destroyed in one step), visual effects sequence: damage flash → destruction VFX → Offline badge appears. For audio: when multiple events fire within the same frame, only events at the **highest triggered tier** play; lower-tier events in the same frame are suppressed. Example: a Healthy → Destroyed transition triggers Medium (damage) + Heavy (Destroy) — only the Heavy SFX plays. The damage hit audio is suppressed because it is subsumed by the more severe event. This prevents double-triggering without requiring a per-event priority queue. **Same-tier simultaneity (Medium and Heavy only):** when multiple events at the **Medium or Heavy** tier fire in the same frame, a maximum of 2 instances play concurrently; remaining instances are suppressed. First-wins ordering (earliest event in the frame's event sequence plays). **Light-tier events are exempt from this cap** — Light SFX are quiet, brief, and decorative; simultaneous Light events may all play without polyphony management. This exemption is required for the per-card Offline flash (one flash per card), which can produce more than 2 simultaneous Light events for slots with large granted-card counts. **Light-tier stagger:** when multiple Light events from the same trigger source fire simultaneously (e.g., all per-card Offline flashes from a single slot's Offline transition), stagger onset by 20–30ms per event — first event at 0ms, second at 20–30ms, third at 40–60ms, and so on. This prevents perceptual smearing while preserving the "one flash per card" information density. Events from different trigger sources in the same frame may coincide freely. **Critical-entry suppression (intentional):** if Slot A enters Critical (Medium) and Slot B is Destroyed (Heavy) in the same frame, the Critical-entry cue is suppressed — only the Heavy Destroyed SFX plays. This is intentional: the Destroyed event carries the moment; the Critical surcharge communicates itself through gameplay on subsequent turns.

**Active-Critical count ownership:** The active-Critical slot count is maintained as a read property on the part-state model layer (`IVehicleView.ActiveCriticalCount` or equivalent), not derived by the audio manager. The audio manager subscribes to `OnCriticalStateChanged` and `OnSlotHpChanged` and queries this property — it does not maintain its own shadow count. **Same-tick Critical+Destroyed edge case:** If a slot enters Critical and is Destroyed in the same event tick (e.g., `hp=1` takes 1 damage — Critical entry and Destruction are simultaneous), both the increment and decrement fire sequentially (net delta zero). The audio loop does not start in this case — the slot was never "stably Critical."

**Tier-priority suppression channel:** "Tier-priority suppression" in the simultaneous feedback rule applies to the **audio channel only**. Visual components of Light-tier events (particle flashes, indicator updates) fire regardless of higher-tier audio events in the same frame. Example: when a Heavy Slot Destroyed and Light per-card flash events fire simultaneously, the Heavy SFX plays and the Light audio is suppressed — but the per-card visual flashes still render. This ensures card-identity information (which cards left) is never lost at the exact moment it is most needed.

**Feedback is view-layer only.** No feedback event may modify game state or delay game-state resolution.

---

## Formulas

### F-VPM1: InstallCost Scaling

> **Authority: scrap-economy.md D.2.** This document does not redeclare the formula. See `design/gdd/scrap-economy.md §D.2` for the full specification:
> ```
> InstallCost(rarity, installedCount, totalSlots) =
>     Ceiling(InstallBaseCost[rarity] × (1 + (installedCount / totalSlots) × kNorm))
> ```
>
> This system provides two inputs to that formula:
> - `installedCount` — **total count of non-Empty slots across all SlotKinds** on the vehicle at the moment of install (not counting the slot being filled). This is `IVehicleView.InstalledCount` per scrap-economy.md D.2's variable table. The surcharge reflects total vehicle fill complexity, not same-kind depth.
> - `totalSlots` — total slot count on the active `FrameLayoutSO`.
>
> All cost constants (`InstallBaseCost` per rarity tier, `kNorm = 0.60`) and tuning ownership live in scrap-economy.md D.2 and `design/registry/entities.yaml`.

**Design intent:** As the vehicle fills, each install costs more. A half-full vehicle with a 5-slot chassis (`installedCount = 2`) costs `Ceiling(BaseCost × 1.24)` at default `kNorm`; a half-full 4-slot chassis (`installedCount = 2`) costs `Ceiling(BaseCost × 1.30)`. The last install on a 5-slot chassis (`installedCount = 4`) costs `Ceiling(BaseCost × 1.48)` — approaching but never reaching the theoretical `× 1.60` ceiling (which would require `installedCount == totalSlots`, an impossible state during an install transaction). In practice the last-install surcharge across EA chassis is approximately 45–60% depending on frame size. Both diverse builds and depth builds face the same fill-ratio penalty at equivalent vehicle fill levels — the formula measures overall vehicle complexity, not same-kind stacking depth. Cost pressure is fill-ratio relative, not absolute: a large-frame vehicle with 6/12 slots filled faces the same cost pressure as a small-frame vehicle with 3/6 slots filled.

**Float cast required:** `installedCount` and `totalSlots` are `int` fields. The ratio must be computed as `(float)installedCount / totalSlots` — identical to the requirement in F-VPM4 and the DamageState predicate. In C#, `int / int` returns `0` for all cases where `installedCount < totalSlots`, silently zeroing fill scaling with no error. The correct notation `(float)installedCount / totalSlots` is mandatory in implementation.

**Precondition guard:** `FrameLayoutSO.OnValidate` must enforce `totalSlots ≥ 1`. A frame with `totalSlots = 0` is a content authoring error — the formula performs division by `totalSlots` and crashes at runtime. The formula must not be invoked with `totalSlots = 0`.

### F-VPM2: Scrap-on-Remove (Salvage Refund)

> **Authority: scrap-economy.md D.6.** This document declares the formula; scrap-economy.md D.6 must reference this definition, not redefine it.
>
> ```
> ScrapRefund(rarity) = max(1, Floor(InstallBaseCost[rarity] × ScrapRefundRate))
> ```
> Constant: `ScrapRefundRate = 0.40` (from scrap-economy.md D.1).

**Static refund values at default `ScrapRefundRate = 0.40`:**

| Rarity | InstallBaseCost | ScrapRefund |
|---|---|---|
| Common | 10 | `max(1, Floor(10 × 0.40)) = 4 Scrap` |
| Uncommon | 25 | `max(1, Floor(25 × 0.40)) = 10 Scrap` |
| Rare | 50 | `max(1, Floor(50 × 0.40)) = 20 Scrap` |

The refund is the same regardless of when the part was installed, how many combats it survived, or what the current vehicle fill ratio is. This makes the cost of a pivot legible: a player always knows what they will get back for a given rarity tier.

**"Removal is always a loss" invariant:** `ScrapRefund` is always less than `InstallCost`. `InstallCost` is always `≥ InstallBaseCost[rarity]` (it can only increase with vehicle fill — see F-VPM1). `ScrapRefund` is `max(1, Floor(BaseCost × 0.40)) ≤ BaseCost × 0.40 < BaseCost ≤ InstallCost`. The invariant holds at all valid tuning values of `ScrapRefundRate < 1.0`.

**What this system provides:** The part's `rarity` (from `PartDefinitionSO`) is passed to the formula. The result is the Scrap amount displayed in the Remove confirm dialog.

**Schema-drift:** The same static formula applies to automatic slot drops on save-load (architecture §6.4–6.5).

### F-VPM3: Repair at Workshop

Repair is **not a Scrap-cost transaction at Workshop nodes.** The Workshop screen (install/remove only) has no repair affordance. Slot Hp is restored exclusively through:

- **Repair cards** played during combat (e.g., `RepairSubsystemEffectSO`-bearing cards).
- **Node events** that explicitly restore Hp as a reward or encounter outcome.
- **Run relics** or part effects that trigger `IVehicleMutator.Repair()` under specific conditions.
- **Chopshop nodes** — `TryRepair` at a Chopshop is a Scrap-cost transaction (scrap-economy.md D.3). The Chopshop is a distinct node type from the Workshop.

No Scrap-repair formula is defined here because Workshop offers no repair affordance.

> **Workshop vs. Chopshop:** These are distinct node types. Workshop = install/remove only (this document's scope). Chopshop = Scrap-denominated repair + purge + purchase + convert (scrap-economy.md D.3). The node-type catalog must confirm these are authored as separate node entries.

### F-VPM4: Armor Contribution Scaling

```
EffectiveArmorContribution(slot) = max(0, Floor(ArmorContribution × (float)CurrentHp / MaxHp))
```

**Variables:**

| Name | Type | Domain | Source |
|---|---|---|---|
| `ArmorContribution` | int | ≥ 0; 0 for non-armored parts | `PartDefinitionSO.ArmorContribution` |
| `CurrentHp` | int | 0 ≤ CurrentHp ≤ MaxHp | Slot state |
| `MaxHp` | int | ≥ 1 | `PartDefinitionSO.MaxHp` |

**Output range:** 0 (at `CurrentHp = 0`) to `ArmorContribution` (at `CurrentHp = MaxHp`). **Rounding:** Floor — consistent with the "round against the player" convention. **Float cast required** — same constraint as DamageState predicate.

**Application:** Applies to all slots with `ArmorContribution > 0`. Slots with `ArmorContribution = 0` return 0 regardless of HP. This formula fires on every read of the vehicle's effective Armor total (total effective Armor = sum of `EffectiveArmorContribution` across all slots).

**Precondition guards:**
- `PartDefinitionSO.OnValidate` must enforce `MaxHp ≥ 1`. If `MaxHp = 0` is encountered at runtime (schema migration edge case), do **not** rely on `max(0, NaN)` — in C#, `float` division by zero produces `NaN`, and `Math.Max(0f, NaN)` returns `NaN`, not `0`. The implementation must guard: `if (MaxHp == 0) return 0;` before the division.
- `PartDefinitionSO.OnValidate` must also enforce `ArmorContribution ≥ 0`. A negative value is a content authoring error — the `max(0, ...)` wrapper silently clamps it at runtime with no error, preventing detection at SO import time.

**Total vehicle armor cap:** Total effective Armor = sum of `EffectiveArmorContribution` across all slots, stored as `vehicle.MaxArmor` in the Card Combat system (`card-combat-system.md F-CC1` variable table). As of this writing, `vehicle.MaxArmor` is listed in card-combat-system.md as "0..unbounded (sum of ArmorContribution)" — no explicit cap is yet authored. Until card-combat-system.md authors an armor cap, `PartDefinitionSO.OnValidate` should warn when `ArmorContribution` exceeds a provisional authoring limit (cross-reference with the card-combat designer before shipping armor-heavy parts). This document does not redeclare the cap; see card-combat-system.md F-CC1 as the authoritative source.

**At Destroyed (Offline):** `CurrentHp = 0` → `EffectiveArmorContribution = 0`. This formula is the mechanical specification of the prose claim in C.2 ("Armor contribution drops to 0 for as long as it remains Offline").

---

## Edge Cases

**E.1 — Install into an Offline slot:** Player attempts to install a part into a slot where the current part is Destroyed (`CurrentHp == 0`). Not permitted — an occupied slot (regardless of state) must be Removed before a new part can be installed. The install UI hides the install option on occupied slots; the inspector shows only the Remove option.

**E.2 — All structural slots Offline simultaneously:** If a single `ApplyDamage` call would reduce multiple structural slots to `0` in one event sequence (e.g., an area damage effect), vehicle death (`OnVehicleDied`) fires once after all slot transitions complete — not once per structural slot. Death is evaluated after Phase 3 of the full damage sequence (per architecture §6.3).

**E.3 — Remove confirm dialog spec:**
The Remove confirm dialog is the only irreversible player-initiated action in this system.
- **Dialog title:** *"Salvage [Part name] — Irreversible"*
- **Body copy:** *"You will receive [X] Scrap. This part and all cards it granted will be permanently removed."*
- **Buttons:** **Salvage** (confirm) | **Cancel** (dismiss without action)
- **KB+M (primary input):** Escape = Cancel; clicking anywhere outside the modal = Cancel. Enter key is **not** mapped to Salvage — Cancel is the default-focused button on dialog open, preventing accidental Enter-key confirmation of a destructive action. To confirm, the player must click the Salvage button explicitly.
- **Gamepad:** A/Cross = Salvage; B/Circle = Cancel. B/Circle is default-focused on open.
- **Dialog state:** Stateless — opening, focusing, or cancelling the dialog creates no partial state. The part remains installed on any cancel path.
- **Click-through prevention:** Clicking outside the modal (the Cancel path via outside-click) **consumes the click** — it dismisses the dialog only and does not interact with any element in the vehicle inspector behind the modal. The click that closes the dialog is not forwarded to the underlying UI layer.

**E.4 — Schema-drift removes a structural slot:** If the dropped slot was structural, the vehicle's `StructuralHp` recalculates on load. If the remaining structural slots have `CurrentHp == 0` after the drop (edge case: save in a very damaged state), the run ends immediately on load with the schema-drift notification shown first, then the run-end screen. The player cannot be dropped into a dead vehicle mid-run.

**E.5 — InstalledCount changes between install and remove:** `InstalledCount` is computed at install time and the cost paid is locked at that moment. If the player later removes a part (of any SlotKind), the total `InstalledCount` decreases by one — the fill ratio for future installs decreases accordingly. The salvage refund is always the static value for the part's rarity (F-VPM2) regardless of when the part is removed. Same-node install-and-salvage (empty vehicle, Common part) costs 6 Scrap net (`10 install − 4 refund = 6`). Same-node install-and-salvage on a full vehicle (where the install cost was elevated by the fill-ratio) costs proportionally more: e.g. at `installedCount = 4` on a 5-slot frame, install cost is `Ceiling(10 × 1.48) = 15 Scrap`; refund is still 4 Scrap; net cost is 11 Scrap. The commitment is priced into the install, not the refund.

**Rare-to-Rare pivot cost (commitment in practice):** The commitment mechanic bites hardest on Rare parts mid-to-late run. Two scenarios on a 5-slot chassis (`totalSlots = 5`, `ScrapRefundRate = 0.40`):
- **Scenario A — early install, late pivot:** Rare installed at `installedCount = 0` costs `Ceiling(50 × 1.00) = 50 Scrap`. Salvaging returns `max(1, Floor(50 × 0.40)) = 20 Scrap`; `installedCount` drops to 3. Reinstalling a Rare at `installedCount = 3` costs `Ceiling(50 × (1 + 3/5 × 0.60)) = Ceiling(50 × 1.36) = 68 Scrap`. **Net pivot cost: 50 − 20 + 68 = 98 Scrap.**
- **Scenario B — full-vehicle pivot:** Rare installed at `installedCount = 4` costs `Ceiling(50 × 1.48) = 74 Scrap`. Salvaging returns 20 Scrap; `installedCount` drops to 3. Reinstalling at `installedCount = 3` costs 68 Scrap. **Net pivot cost: 74 − 20 + 68 = 122 Scrap.**

At Biome 2 rates (`BiomeBaseScrap.Biome2 = 28 Scrap` per combat before DSBonus), a 98–122 Scrap Rare pivot represents 3.5–4.4 full combat cycles — the intended cost of changing build identity mid-run. Early Rare installs are cheaper to pivot from (Scenario A); full-vehicle Rare pivots are extremely costly (Scenario B). Both represent meaningful, irreversible commitment.

**E.6 — Hard-remove during combat:** Hard-remove (`IVehicleMutator.HardRemoveCards`) is not triggered by in-combat events in EA. The only hard-remove triggers are player-initiated scrap (Workshop, out of combat) and external-source termination (run events between nodes — e.g., Dredge Javelin chain-cut is a between-nodes event, not mid-combat). If a future mechanic requires in-combat hard-remove, it must resolve before Phase 2 of the triggering event to avoid mid-bus deck mutation (architecture §6.10 reentrancy rules apply).

**E.7 — Repair card played on a non-Offline slot already at MaxHp:** `IVehicleMutator.Repair(slot, amount)` clamps to `MaxHp`. Overheal is silently absorbed — no excess carries over, no error is thrown. The Repair card is moved to the discard pile (consumed normally).

**E.8 — Part with no granted cards scrapped:** Hard-remove on a part that granted zero cards is a no-op at the deck layer. No card-sweep animation plays. The part destruction feedback (C.6 Medium tier) still plays.

**E.9 — All-Offline forced-pass turn:** All slot instances are Offline simultaneously, leaving no cards in any zone that contributes to the draw pile. At the start of the player's turn, **the forced-pass check runs after the draw phase** — the standard draw phase executes first (attempting to draw from draw pile; reshuffling discard to draw pile if empty); if after the draw phase hand, draw pile, and discard pile all contain zero drawable cards, the forced-pass path triggers. This ordering ensures that a repair that occurred at end of enemy turn (restoring cards to draw pile) is correctly reflected before the check runs.

**Turn-start resolution order:** Start-of-turn relics resolve **before** the draw phase. A relic that repairs a slot on turn start restores that slot's `OfflineCards` to the draw pile before the draw attempt — and therefore before the forced-pass check. The all-Offline condition must be re-evaluated after all start-of-turn relic effects resolve and the draw phase completes. If a relic resolves the all-Offline condition (e.g., auto-repair restores one slot), the forced-pass path does not trigger for that turn.

When forced-pass triggers: the game shows a forced-pass banner *"All systems offline — turn passed."* The banner displays for 1.5 seconds. **Input blocking during banner:** all player inputs are blocked for the full 1.5 seconds — card play, menu open, relic activation, and any other player-initiated action. "On start of turn" relics that fire automatically (not player-activated) may fire before the banner appears, per normal turn-start relic resolution order. After the 1.5 seconds, the enemy's turn sequence begins normally. **Repeated all-Offline turns:** if the all-Offline condition persists across multiple turns, subsequent forced-pass banners display the same text; they do not stack or escalate. The turn-counter advances normally during forced-pass turns (Enrage clock ticks).

The player's turn ends without a card being played. The enemy acts normally. Escape requires a non-slot repair source. Without one, the enemy reduces `StructuralHp` to `0` via the normal vehicle-death path. All-Offline is intentionally a slow death, not an instant run-end, to preserve relic interactions. Note: relics with "on start of turn" or "on damage received" trigger hooks remain eligible during forced-pass turns; relics with "on card played" hooks do not fire.

**Repair-card death spiral — valid consequence state:** If the slot sourcing the player's Repair cards enters Offline, those cards enter the Offline zone and are inaccessible during the Offline state. Non-slot repair sources (Chopshop nodes, node-event repair rewards, repair relics) are **not guaranteed** per run segment — acquiring them is player strategy, not a design guarantee. A run where all Repair-card-granting slots go Offline and the player holds no non-slot repair source is a valid run-end consequence state. This is the correct reading: the player had opportunities to invest in repair access and did not. The consequence is readable in advance — a player watching their Repair-granting slot enter Critical has a full turn to play their Repair cards before the slot becomes Offline.

**E.10 — Empty install loot pool:** Player opens the vehicle inspector at a Workshop node. No parts are available in the node's loot pool. The install UI shows an empty-state panel: *"No parts available at this Workshop."* The install button is absent. The player may still Remove existing parts.

---

## Dependencies

| System | Dependency | What this doc consumes |
|---|---|---|
| `design/gdd/vehicle-and-part-architecture.md` | **Contract surface (required)** | `SlotDefinition` (§3.1), `FrameLayoutSO` thresholds `CriticalThresholdPct`, `DegradedThresholdPct` (§3.2), `IsPlayable` contract (§3.4), validation gates (§3.5), phase model (§4.1), `IVehicleEventBus` event sequence (§4.3), F-VP1/2/3 (§5.1–5.3), `DamageState` predicate (§5.4), `IVehicleMutator.Repair`/`HardRemoveCards` (§6), reentrancy rules (§6.10) |
| `design/gdd/card-system.md` | `CardDefinitionSO`, `SourceSlotId` field, `PositionRequirement` enum, granted-card deck mechanics | Gate composition (C.5) and soft-disable visual treatment (C.3) depend on card data contract |
| `design/gdd/card-combat-system.md` | Card generation timing (start-of-turn grant), `RepairSubsystemEffectSO` resolution | C.2 repair restoration timing; C.5 source-slot gate evaluation point |
| `design/gdd/scrap-economy.md` | Scrap as the install/remove currency; authoritative install formula (D.2); authoritative salvage refund formula (D.6); Chopshop TryRepair verb (D.3) | F-VPM1 cross-references D.2; F-VPM2 cross-references D.6. F-VPM3 Workshop scope is distinct from D.3 Chopshop. |
| `design/gdd/save-persistence.md` | Schema-drift hook (§6.4–6.5 of architecture); `SlotId` as persistence key; `SlotStateDTO.OfflineCards` field | F-VPM2 schema-drift refund; E.4 structural-slot drift edge case; Offline zone serialization (C.2) |

**Registry dependency:** `ScrapRefundRate` from scrap-economy.md D.1 is the only constant from F-VPM2 that requires registry registration (if not already present). The tenure-related constants (`TenureDecayRate`, `TenureMinMultiplier`, `ZeroCombatMultiplier`) were removed from this document in W5 — do not register them. Update any existing registry entries or pending-registration notes for those constants.

**combatsSurvived ownership (Pillar 1 tracking only):** `combatsSurvived` is no longer used in the Salvage Refund formula (F-VPM2). It is retained as a Pillar 1 emotional-attachment tracker on `InstalledPart` — the count of combats a specific part has survived contributes to player attachment (see architecture §3 for the `CombatsSurvived` property on `InstalledPart`). The system that owns the increment is `card-combat-system.md`: it increments `CombatsSurvived` on the combat-win event, after the combat result is resolved and before the post-combat flow begins. It resets to 0 on install and on salvage. No atomicity constraints apply beyond the existing combat-win event sequence (incrementing `CombatsSurvived` is idempotent per combat and has no effect on any formula).

**Required reverse dependency update — scrap-economy.md D.6:** With the W5 static-refund change, the tenure-based formula in scrap-economy.md D.6 is now superseded by the static formula declared here. `scrap-economy.md §D.6` must be updated to: (a) remove the `TenureMultiplier` formula, (b) replace `ScrapRefund(rarity, combatsSurvived)` with `ScrapRefund(rarity) = max(1, Floor(InstallBaseCost[rarity] × ScrapRefundRate))`, and (c) reference this document as the declarative source. **This document is the authority; D.6 references it.** This update is a blocking prerequisite for scrap-economy.md's own next review.

**Pillar 5 dead zone — named cross-doc obligation:** `HostileTiltDelta` in `node-encounter.md` references "Frame subsystem is Degraded/Offline." With the two-state collapse in this document (Degraded = visual-only, no mechanical state), the tilt trigger as written can only fire at Offline (0 HP) — not at Critical (1–20% HP). This creates a gap where a player with all slots at 5% HP (Critical, energy surcharges active) receives zero world-level response from the Node Encounter system. Updating the tilt trigger to reference "Critical/Offline" is a **required cross-doc update owned by node-encounter.md** that must be completed before the vehicle damage model is considered fully integrated with Pillar 5 (Route Reflects Vehicle State). This is a named obligation, not a soft note. **Implementation gate note:** implementation of this GDD is not gated on node-encounter.md's `HostileTiltDelta` update, but the integrated Pillar 5 trigger path is not valid until that update ships.

**Total armor cap cross-reference:** The per-slot `EffectiveArmorContribution` formula is defined here (F-VPM4), but the cap on total vehicle armor is owned by `card-combat-system.md`. Both systems must agree: this doc defers to card-combat-system.md for the cap value and references it as the authoritative source.

**Reverse dependencies** (systems that must list this doc in their own Dependencies section):

| System | What they consume from this doc |
|---|---|
| `design/gdd/card-combat-system.md` | Source-slot playability gate (C.5); repair event timing (C.2); feedback event table (C.6) |
| `design/gdd/save-persistence.md` | Schema-drift refund formula (F-VPM2); structural-slot drift edge case (E.4); `SlotStateDTO.OfflineCards` field (C.2) |
| UX specs (`design/ux/`) | IsPlayable affordance vocabulary (C.3); slot indicator states; confirm dialog spec (E.3) |

---

## Tuning Knobs

| Knob | Default | Safe Range | Gameplay Aspect |
|---|---|---|---|
| `CriticalThresholdPct` | 20 | 10–35 | Hp% at which a slot shows red pulsing and triggers the energy surcharge. Below 10 gives almost no warning before Destroyed. Authored per `FrameLayoutSO`. Must be ≥ 1 — `FrameLayoutSO.OnValidate` enforces this; DamageState predicate must assert at runtime. |
| `DegradedThresholdPct` | 50 | 30–70 | **Visual-only.** Hp% at which the part sprite shows worn/damaged art. No mechanical effect — not a DamageState. Tunable independently of `CriticalThresholdPct`. |
| `CriticalEnergySurcharge` | 1 | 0–1 | Additional energy required to play a card from a Critical slot. At 0: Critical is sensory-only (no mechanical pressure). At 1 (default): each Critical-sourced card costs +1 energy. Safe range capped at 1 pending card-pool authoring — value of 2 is reserved and requires AC-VPM-F validation (which cannot be satisfied until the full granted-card pool is authored and reviewed for 0-cost cards from Critical-eligible slots). Applied per card, not as a global per-turn cap. |
| `ForcedPassBannerDurationSecs` | 1.5 | 1.0–3.0 | Duration in seconds of the "All systems offline — turn passed" banner. Inputs during this window are **discarded** (not buffered) — card play, relic activation, and manual turn-end are all rejected. System pause menu (Esc) is exempt from the input block. |
| `CriticalLoopExitCrossfadeMs` | 250 | TBD W3g | Duration in milliseconds of the Critical ambient loop fade-out when the active-Critical count reaches zero. Placeholder value: 250ms. **Must be exposed as a configurable field — not a compile-time constant.** Final value authored in W3g. |

**Formula floor invariant (F-VPM2):** The `max(1, Floor(...))` wrapper guarantees `ScrapRefund ≥ 1` at all tuning values of `ScrapRefundRate`. At minimum `ScrapRefundRate = 0.20` (the enforced lower bound): `max(1, Floor(10 × 0.20)) = max(1, 2) = 2 Scrap` for Common; `max(1, Floor(50 × 0.20)) = max(1, 10) = 10 Scrap` for Rare — the floor does not activate for any rarity at the minimum bound. At extreme low `ScrapRefundRate = 0.05` (below the enforced minimum, for reference only): `max(1, Floor(10 × 0.05)) = max(1, 0) = 1 Scrap` — floor activates. Invariant holds.

**Install and salvage economy knobs** (`InstallBaseCost` per rarity tier, `kNorm`, `ScrapRefundRate`) are owned by `scrap-economy.md`. See `design/registry/entities.yaml` for registered default values.

**Critical constraint — ScrapRefundRate:** Must remain `< 1.0` to preserve the "removal is always a loss" invariant, and must remain `≥ 0.20` to preserve meaningful pivot agency. The SO or data asset that owns this constant must enforce `ScrapRefundRate ∈ [0.20, 0.99]` via `OnValidate`, and the registry entry in `entities.yaml` must include both `min: 0.20` and `max: 0.99` attributes. **Upper-bound rationale:** `ScrapRefundRate = 1.0` produces `ScrapRefund(Common) = 10 Scrap = InstallBaseCost.Common` — the "removal is always a loss" invariant breaks silently. **Lower-bound rationale:** `ScrapRefundRate ≤ 0.0` produces `ScrapRefund = 1 Scrap` for all rarities regardless of install cost — a Rare at full vehicle (74 Scrap installed) returns 1 Scrap, making mid-run pivots economically unrecoverable and eliminating Pillar 4 agency without any runtime signal or invariant violation. See AC-VPM-E for the tests that verify both bounds.

---

## Acceptance Criteria

> All ACs cite registered constants from `design/registry/entities.yaml` or named formula sections in scrap-economy.md D.2/D.6. No hardcoded values appear without a registry citation. `ScrapRefundRate` from scrap-economy.md D.1 must be registered in entities.yaml before ACs citing it are test-ready. Tenure-related constants (`TenureDecayRate`, `TenureMinMultiplier`, `ZeroCombatMultiplier`) were removed from this document in W5 — do not register them.

**AC-VPM01 — InstallCost formula correctness.**
- Setup: `small_frame` vehicle (`totalSlots = 5`, per entities.yaml `FrameLayoutId.small_frame`). All slots empty (`installedCount = 0`). `rarity = Common` (`InstallBaseCost.Common = 10`, `kNorm = 0.60` per scrap-economy.md D.2).
- Action: compute `InstallCost` for sequential installs.
- Expected:
  - 1st install (`installedCount = 0`): `Ceiling(10 × (1 + (0/5) × 0.60)) = 10 Scrap`
  - 2nd install (`installedCount = 1`): `Ceiling(10 × (1 + (1/5) × 0.60)) = Ceiling(10 × 1.12) = 12 Scrap`
  - 3rd install (`installedCount = 2`): `Ceiling(10 × (1 + (2/5) × 0.60)) = Ceiling(10 × 1.24) = 13 Scrap`
- After installing the 2nd part (of any SlotKind) and removing it: `installedCount` decreases by 1. The cost for the next install (`installedCount = 1`) = 12 Scrap (not 13). `installedCount` counts all non-Empty slots, not same-kind only.
- **Float-cast trap (must pass):** `installedCount` and `totalSlots` are `int` fields; integer division silently returns 0 when `installedCount < totalSlots`, zeroing fill scaling with no error or warning. Assert that `InstallCost(Common, installedCount=1, totalSlots=5)` returns **12 Scrap**. A return of 10 Scrap indicates the implementation used `int / int` division. The fix is `(float)installedCount / totalSlots` — identical to the requirement in F-VPM1 and F-VPM4.

**AC-VPM02 — Salvage refund formula correctness (static).**
- Setup: `ScrapRefundRate = 0.40` (from scrap-economy.md D.1 / entities.yaml).
- Expected per rarity:
  - `rarity = Common` (`InstallBaseCost = 10`): `ScrapRefund = max(1, Floor(10 × 0.40)) = 4 Scrap`.
  - `rarity = Uncommon` (`InstallBaseCost = 25`): `ScrapRefund = max(1, Floor(25 × 0.40)) = 10 Scrap`.
  - `rarity = Rare` (`InstallBaseCost = 50`): `ScrapRefund = max(1, Floor(50 × 0.40)) = 20 Scrap`.
- For each case: confirm dialog displays the correct Scrap amount. Scrap balance increases by that amount after confirm. Result is identical regardless of `combatsSurvived` value (0, 5, or 99).
- Verify "removal is always a loss": `ScrapRefund < InstallBaseCost[rarity]` at `ScrapRefundRate = 0.40`.

**AC-VPM03 — Damage state boundary conditions.**
- Setup: `MaxHp = 10`, `CriticalThresholdPct = 20`.

| `CurrentHp` | `(float)CurrentHp / MaxHp` | Expected State |
|---|---|---|
| 10 | 1.00 | Healthy |
| 6 | 0.60 | Healthy |
| 3 | 0.30 | Healthy |
| 2 | 0.20 | Critical (boundary — ≤ threshold) |
| 1 | 0.10 | Critical |
| 0 | 0.00 | Destroyed |

- **Float-cast trap test:** `MaxHp = 2, CurrentHp = 1`, `CriticalThresholdPct = 20`. Float: `1f/2 = 0.50 > 0.20` → **Healthy**. Integer division (incorrect): `1/2 = 0` → would evaluate as Destroyed. Expected: Healthy. This case specifically isolates the float-cast requirement — a wrong result here means integer division is being used.

**AC-VPM04 — Soft-disable (Offline) and restore.**
- Setup: a slot with three granted cards: one in hand (card A), one in draw pile (card B), one in discard (card C). `CurrentHp = 1`. Record hand count (N_hand), draw pile count (N_deck), discard count (N_discard).
- Action: apply 1 damage → `CurrentHp = 0`.
- Expected: (1a) card A absent from hand; (1b) card C absent from discard; (2) hand count = N_hand − 1; **(3) BLOCKED — draw pile count = N_deck − 1 requires DrawPileCount exposure (see BLOCKED note below);** (4) discard count = N_discard − 1; (5) HUD Offline count badge on slot shows "3"; (6) cards are not drawable.
- Action: repair 1 Hp → `CurrentHp = 1`.
- Expected: (8) hand count unchanged (cards return to draw pile, not hand); (9) HUD Offline count badge disappears; (10) `DamageState` re-derives to Critical or Healthy based on new `CurrentHp`.
- **⚠ BLOCKED — assertions (1c) and (7) require DrawPileCount:** Assertion (1c) — card B absent from draw pile after Offline — and assertion (7) — draw pile count = (N_deck − 1) + 3 after repair — require `DrawPileCount` (or equivalent property exposing the card draw pile size) on the card system's state contract. These assertions **must not be proxied via `slot.OfflineCards.Count`** — the proxy only proves the Offline zone was written correctly, not that cards were actually removed from the draw pile. A broken draw-pile removal passes the proxy test, masking the bug. Until `DrawPileCount` is available as a prerequisite story delivery, assert only (1a), (1b), (2), (4), (5), (6), (8), (9), (10). Move assertions (1c) and (7) to the BLOCKED set.

**AC-VPM05 — Hard-remove zone coverage.**
- Setup: a part with **four** granted cards: card A in hand, card B in draw pile, card C in discard, and card D in the Offline zone for this slot (`slot.OfflineCards = [cardD.CardId]` — drive the slot to `CurrentHp = 0` via `IVehicleMutator.ApplyDamage` before setup to populate the Offline zone via the event path). Record zone counts (N_hand, N_deck, N_discard). Player confirms Remove (Salvage confirmed via E.3 dialog).
- Expected: (1) hand: N_hand − 1 (card A absent); (2) draw pile: N_deck − 1 (card B absent); (3) discard: N_discard − 1 (card C absent); (4) `slot.OfflineCards.Count == 0` (card D swept from Offline zone); (5) no card with the removed part's `SourceSlotId` present in any zone (hand, draw pile, discard, or Offline zone) after removal.
- **⚠ BLOCKED — assertion (2) requires DrawPileCount:** Assertion (2) requires `DrawPileCount` (or equivalent property exposing the card draw pile size) on the card system's state contract — the same prerequisite as AC-VPM04 assertions (3) and (7). Do NOT proxy assertion (2) via `slot.OfflineCards.Count` — that tests Offline zone writes, not draw-pile removal correctness. A broken draw-pile removal passes the proxy test. Until `DrawPileCount` is exposed as a prerequisite story delivery, assert only (1), (3), (4), (5).

**AC-VPM06 — Gate composition priority (externally disabled card).**
- Setup: a card in hand that has an active enemy-debuff external disable (`ExternalDisableStatus = "Jammed"`, `turnsRemaining = 2`), AND `PositionRequirement = RequiresAhead` (condition not met), AND player has 0 energy. The card's source slot is Healthy (not Offline).
- Action: player attempts to play the card.
- Expected: the card displays the external-disable status icon and tooltip: "Jammed — 2 turns remaining." The card is greyed in hand (remains in hand — external disables do not trigger Offline zone). The positional and energy gates are not the displayed error, but the card is still unplayable.

**AC-VPM07 — Schema-drift notification.**
- Setup: save with `SlotId = "weapon_01"` containing a `rarity = Common` part. One card from `weapon_01` is in the Offline zone at time of save (`SlotStateDTO.OfflineCards` for `weapon_01` contains one `CardId`). Load after removing `"weapon_01"` from `FrameLayoutSO`. Expected refund: `ScrapRefund(Common) = max(1, Floor(10 × 0.40)) = 4 Scrap` (static formula — `combatsSurvived` has no effect).
- Expected: (1) load-time notification: *"1 slot was removed because your vehicle's frame was updated. You received 4 Scrap."*; (2) Scrap balance +4; (3) slot absent from vehicle inspector; (4) all cards from that slot hard-removed from all zones (hand, deck, discard); (5) Offline zone for the dropped slot: `slot.OfflineCards.Count == 0` — the Offline zone card was swept atomically with the slot drop.

**AC-VPM08 — Overheal clamp.**
- Setup: slot at `CurrentHp = MaxHp = 10`.
- Action: `IVehicleMutator.Repair(slot, 5)`.
- Expected: `CurrentHp` remains 10. No exception thrown. Repair card moved to discard pile.

**AC-VPM09 — Install blocked when slot occupied (hidden, not greyed).**
- Setup: a slot with any part installed (any `DamageState`).
- Expected: (1) no install button element is rendered in the slot inspector UI — it is structurally absent, not greyed or disabled; (2) only the Remove option is rendered. Verify via UI hierarchy assertion: query the rendered component tree for the occupied slot inspector and confirm no element with an install affordance role (element bound to an install action or carrying install vocabulary) is present. Absence must be verified programmatically — visual inspection is not a sufficient gate for a blocking correctness requirement.
- **Pending routing — implementation blocked:** Before this AC can be automated, route to `unity-ui-specialist` to confirm the install affordance element's USS class name or UXML element type in the actual implementation. The assertion "no element with install affordance role present" is not implementable without this identifier. Mark this AC as *pending specification* until the identifier is confirmed and recorded here.

**AC-VPM-A — All-Offline forced-pass turn.**
- Setup: drive all vehicle slots to `CurrentHp = 0` via `IVehicleMutator.ApplyDamage(slot, damage)` calls — do **not** set `CurrentHp = 0` directly via test fixture. Offline zone population (`OfflineCards` on each `SlotStateDTO`) occurs via the `OnSlotHpChanged` event path; bypassing the event bus leaves `OfflineCards` unpopulated, causing hand/draw pile to be empty for the wrong structural reason and preventing `OnForcedPassBegin` from triggering correctly. After all slots are Destroyed: hand empty, draw pile empty, discard empty of drawable cards, all cards in their respective Offline zones.
- Action: game evaluates start-of-player-turn.
- Expected: (1) forced-pass banner: *"All systems offline — turn passed"*; (2) banner persists 1.5 seconds (non-dismissable); (3) player turn ends with no card played; (4) enemy acts normally; (5) turn counter advances; (6) state recurs on the next turn if all slots remain Offline.
- **Sub-case — repair relic prevents forced-pass:** Setup as above (all slots Offline). Equip a relic with "on start of turn: repair one slot for 1 Hp." Advance to the next player turn.
  - Expected: relic fires before the draw phase; one slot repairs; that slot's `OfflineCards` shuffle into the draw pile; draw phase draws from the now-non-empty draw pile; forced-pass check finds ≥1 drawable card and does NOT trigger; `OnForcedPassBegin` is never raised; the player's turn proceeds normally with at least one card in hand.
  - **Ordering assertion (required):** Subscribe to `IVehicleEventBus` (the named first-class event bus per architecture §4.3 — verify `OnSlotRepaired` and `OnDrawPhaseBegin` are both first-class events on this bus before implementing). The event log must record `OnSlotRepaired` (from the relic) **before** `OnDrawPhaseBegin`. If the log shows `OnDrawPhaseBegin` before `OnSlotRepaired`, the implementation violates the E.9 resolution order — the forced-pass not triggering would be a lucky coincidence, not a correct implementation. This ordering assertion is what distinguishes a correct implementation from one that happens to produce the right outcome by accident. **Test infrastructure:** use `IVehicleEventBus.Subscribe<T>` in test setup to capture event order into a local list; assert the list sequence after turn evaluation completes.

**Forced-pass input rejection assertions (required):** During the 1.5-second forced-pass block, the following input attempts must each be rejected: (a) a card play attempt raises `OnInputRejected(reason: ForcedPass)` or equivalent rejection event; (b) a relic activation attempt raises `OnInputRejected(reason: ForcedPass)`; (c) `ITurnController.TryEndTurn()` returns `false` (manual turn-end is non-responsive). These assertions confirm the input block is active — the banner display alone does not verify blocking correctness.

**AC-VPM-B — Critical surcharge — non-compounding across slots.**
- Setup: two slots both at `DamageState == Critical`. Slot A grants card X (`BaseCost = 1`). Slot B grants card Y (`BaseCost = 1`). `CriticalEnergySurcharge = 1`.
- **AC-VPM-B.1 (affirmative assertion):** Card X costs exactly `BaseCost + CriticalEnergySurcharge = 2` energy. Card Y costs exactly `BaseCost + CriticalEnergySurcharge = 2` energy. Assert both values are exactly 2 before testing the negative case.
- Expected: card X costs 2 energy; card Y costs 2 energy. Playing both = 4 energy total. Card Y does NOT cost 3 energy (no compounding from Slot A also being Critical).
- Sub-case (`CriticalEnergySurcharge = 2`): card X costs 3, card Y costs 3. Playing both = 6 energy. Neither card is double-charged.
- **Negative case (verify non-compounding):** Card Y must NOT cost `BaseCost + 2` (i.e., 3 energy) in the base case where two slots are simultaneously Critical. The correct cost is exactly `BaseCost + CriticalEnergySurcharge = 1 + 1 = 2`. If card Y costs 3 energy, the implementation is incorrectly applying `N_critical_slots × CriticalEnergySurcharge` instead of the constant per-card surcharge — this is the specific compounding failure mode this AC must catch.

**AC-VPM-D — Armor contribution formula correctness (F-VPM4).**
- Setup: slot with `ArmorContribution = 10`, `MaxHp = 10`.

| `CurrentHp` | Expected `EffectiveArmorContribution` | Rationale |
|---|---|---|
| 10 | `max(0, Floor(10 × 10/10)) = 10` | Full HP = full contribution |
| 5 | `max(0, Floor(10 × 5/10)) = 5` | Mid-range; Floor rounds down |
| 3 | `max(0, Floor(10 × 3/10)) = max(0, Floor(3.0)) = 3` | Float cast required |
| 1 | `max(0, Floor(10 × 1/10)) = max(0, Floor(1.0)) = 1` | Minimum non-zero |
| 0 | `max(0, Floor(10 × 0/10)) = 0` | Destroyed → zero armor |

- **Float-cast test:** `MaxHp = 3, CurrentHp = 1, ArmorContribution = 10`. Float: `Floor(10 × 1f/3) = Floor(3.33) = 3`. Integer division (incorrect): `Floor(10 × 0) = 0`. Expected: 3.
- **MaxHp = 0 guard test:** `MaxHp = 0`. The `if (MaxHp == 0) return 0` guard must fire before the division is reached. **Assert that the guard path is reached and returns 0** — do not assert `max(0, Floor(ArmorContribution × 0f/0f)) = 0`, as `0f/0f = NaN` in C# and `Math.Max(0f, NaN) = NaN`, not `0`. A naive implementation without the explicit guard produces `NaN`, not `0`, and this AC must catch that failure. No exception thrown.
- **ArmorContribution = 0 test:** `ArmorContribution = 0`, any `CurrentHp`. Expected: `EffectiveArmorContribution = 0`.

**AC-VPM-C — Schema-drift on structural slot causes run-end.**
- Setup: save with two structural slots — S1 (`SlotId = "hull_01"`, `IsStructural = true`, `CurrentHp = 0`, `rarity = Common`) and S2 (`SlotId = "hull_02"`, `IsStructural = true`, `CurrentHp = 0`). Load after removing `"hull_01"` from `FrameLayoutSO`. Expected refund for S1: `ScrapRefund(Common) = max(1, Floor(10 × 0.40)) = 4 Scrap` (static formula).
- Expected: (1) schema-drift notification shown first: *"1 slot was removed because your vehicle's frame was updated. You received 4 Scrap."*; (2) after notification, the run-end screen is shown (remaining StructuralHp = 0 — S2 is also at `CurrentHp = 0`); (3) player is not placed into map or combat in a dead-vehicle state.

**AC-VPM-E — ScrapRefundRate OnValidate guard (both bounds).**
- Setup: identify the ScriptableObject or data asset that owns `ScrapRefundRate`. Test both bounds separately:
  - **Upper bound:** Attempt to set `ScrapRefundRate = 1.0` in the Unity Inspector and save.
  - **Lower bound:** Attempt to set `ScrapRefundRate = 0.10` (below the 0.20 floor) in the Unity Inspector and save.
- Expected (upper bound): (1) `OnValidate` fires and logs an error (e.g., *"ScrapRefundRate must be ≤ 0.99 — value clamped to 0.99"*); (2) asset not persisted with `ScrapRefundRate ≥ 1.0` (value clamped to 0.99); (3) no exception thrown.
- Expected (lower bound): (1) `OnValidate` fires and logs an error (e.g., *"ScrapRefundRate must be ≥ 0.20 — value clamped to 0.20"*); (2) asset not persisted with `ScrapRefundRate < 0.20` (value clamped to 0.20); (3) no exception thrown.
- **Why the upper bound matters:** `ScrapRefundRate = 1.0` produces `max(1, Floor(10 × 1.0)) = 10 Scrap` for Common — equal to `InstallBaseCost.Common`. The "removal is always a loss" invariant fails silently.
- **Why the lower bound matters:** `ScrapRefundRate ≤ 0.0` produces `ScrapRefund = 1 Scrap` for all rarities regardless of install cost — a Rare at full vehicle (74 Scrap installed) returns 1 Scrap, eliminating Pillar 4 pivot agency without any runtime signal.
- **CI gate (blocking):** This AC must be promoted to a blocking CI gate. Both bound tests must run in CI under the `economy-invariants` category. Register as required tests before the first balance tuning pass.

**AC-VPM-F — E.3 Remove confirm dialog click-through prevention.**
- Setup: open the Remove confirm dialog for an occupied slot in the Workshop inspector. Ensure an interactive element in the inspector (e.g., another slot's Remove button) is positioned behind the modal's outside-click dismiss area.
- Action: click outside the modal on a position that overlaps the behind-modal interactive element (the Cancel-via-outside-click path).
- Expected: (1) the dialog is dismissed (Cancel fires); (2) the underlying inspector element is **not** activated — the outside-click is consumed by the modal and does not propagate to the inspector layer; (3) vehicle state is unchanged; (4) no unintended Remove action triggers on the slot behind the modal.
- **Why this matters:** E.3 specifies "the dismissing click is consumed and does not interact with any element in the vehicle inspector behind the modal." Without this AC, an implementation that passes dismiss-path tests but forwards the click could trigger an accidental second Remove action.

**AC-VPM-G — C.6 Light-tier stagger for same-source simultaneous events.**
- Setup: configure a slot with **4 granted cards** (A, B, C, D), all in hand. Drive the slot to `CurrentHp = 0` via a single `IVehicleMutator.ApplyDamage` call — a single Offline event producing 4 simultaneous Light-tier per-card flash events from the same trigger source.
- Expected: (1) 4 Light-tier flash events fire — one per card; (2) onset timestamps staggered from `OnSlotHpChanged (hp → 0)` dispatch using an **injected deterministic `IFrameClock`**: card A fires at tick 0 (0ms), card B at tick 1 (20ms pinned), card C at tick 2 (40ms), card D at tick 3 (60ms); expected windows — B: [20ms, 30ms]; C: [40ms, 60ms]; D: [60ms, 90ms] — measured from trigger dispatch, not from preceding card's actual onset; (3) no two flashes from this trigger source share the same clock tick.
- **Test infrastructure:** Inject `IFrameClock` (or equivalent test-clock interface) into the stagger system before this AC can be automated. The stagger system must consume `IFrameClock` for onset scheduling — not `Time.deltaTime` or platform time — so deterministic tick advances produce verifiable assertions in NUnit edit-mode tests.
- **Note:** Cross-source events in the same frame may coincide freely — this AC tests same-source stagger only.

**AC-VPM-H — C.1 cross-field OnValidate guard auto-clamp.**
- Setup: in the Unity Inspector, set `MaxHp = 2` and `CriticalThresholdPct = 49` on a `FrameLayoutSO`. Save the asset. **Precondition:** `MaxHp = 2 ≥ 2` — this test exercises the upward clamp path. For `MaxHp = 1`, a distinct rejection error fires (see C.1 MaxHp=1 edge case) — test that path separately.
- Expected: (1) `FrameLayoutSO.OnValidate` fires; (2) a warning is logged: *"CriticalThresholdPct clamped from 49 to 50 to preserve a valid Critical window (MaxHp=2)"*; (3) the SO is saved with `CriticalThresholdPct = 50` (`Ceiling(100 / MaxHp) = Ceiling(100 / 2) = 50`); (4) no exception thrown.
- **The specific failure mode this catches:** MaxHp=2, CriticalThresholdPct=49 passes all per-field range checks but produces `Floor(2 × 49 / 100) = 0` — eliminating the Critical window. The clamp is **upward** (to `Ceiling(100 / MaxHp) = 50`), not downward — decrementing from 49 would never satisfy the guard for MaxHp=2 and would loop to 1 without resolving.

---

## Prototype Validation Hypotheses (PVH)

These are experience-layer design claims that cannot be verified by unit tests or document review — they require playtest data. Add each to the Prototype playtest protocol. They do not block implementation but must be evaluated before the vertical slice milestone.

**PVH-1 — Critical surcharge produces triage decisions, not a flat tax.**
- Hypothesis: In a session where a single slot is Critical for 2+ consecutive turns, a majority of playtest participants report feeling genuine repair pressure — a decision between paying the surcharge and spending a turn on repair — not just an increased cost they absorb and ignore.
- Threshold: ≥60% of participants in a structured playtest session describe the Critical state as a "decision moment" unprompted.
- Owner: game-designer. Timing: first vertical slice prototype with a Repair card in the starting deck.
- **What failure looks like:** Participants describe the Critical state as "a cost I pay and keep going." Remediation: review Repair card energy cost vs. surcharge savings per turn; consider adjusting `CriticalEnergySurcharge` or Repair card `BaseCost`.

**PVH-2 — Workshop install flow does not break the "vehicle as body" emotional register.**
- Hypothesis: The transactional Workshop screen (Scrap costs, install buttons, stat previews) is experienced as planning — the strategic layer — not as shopping that contradicts the visceral "vehicle as body" feeling established in combat.
- Threshold: ≤20% of participants in post-session debrief describe the Workshop as "shopping" or "feeling disconnected from my vehicle" unprompted.
- Owner: game-designer + ux-designer. Timing: first playtest session including a Workshop node.
- **What failure looks like:** Participants use commercial language ("price," "store," "buying a part") unprompted when describing the Workshop. Remediation: revisit Workshop copy register; consider framing install confirmation as integration/grafting rather than purchase.

**PVH-3 — Early Common install-and-salvage cost is appropriately forgiving.**
- Hypothesis: A 6 Scrap net loss on an early-run Common trial install (empty vehicle, `installedCount = 0`) is experienced as a real but acceptable cost — not trivially free, not so punitive it deters valid experimentation.
- Threshold: Playtest participants occasionally trial Common installs early-run and describe the 6 Scrap cost as "real but manageable" rather than "free" or "too cheap to matter."
- Owner: game-designer. Timing: first prototype session with `StartingScrap` values set.
- **What failure looks like:** Participants systematically trial multiple Common parts before committing, describing the net loss as "irrelevant." Remediation: review `StartingScrap` values; consider whether early experimentation is in fact desirable (in which case the design intent note in E.5 should reflect this explicitly).

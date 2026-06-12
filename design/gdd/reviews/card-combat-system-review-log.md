# Card Combat System — Review Log

## Review 1: 2026-04-21 (Light-Touch)

**Verdict**: APPROVED

**Mode**: Light-touch (single-pass, no multi-specialist gate). User directive 2026-04-21 to accelerate path to Unity prototype. Card Combat introduces no new technical risk surface beyond what ADR-0001 already covers — damage overlays, event-driven view updates, and C# event architecture are all inherited constraints. Light-touch is appropriate.

### Completeness

All 8 required sections present (Overview, Player Fantasy, Detailed Design, Formulas, Edge Cases, Dependencies, Tuning Knobs, Acceptance Criteria). Bonus sections: Visual/Audio Requirements, UI Requirements, Open Questions. Detailed Design is split into Core Rules (R1–R12), States and Transitions (S1–S5), and Interactions with Other Systems — matches convention from Card System, Status Effects, and Vehicle & Part.

### C1 Scoping Compliance (Three Sub-Models)

R1 explicitly names all three sub-models with ownership boundaries:

- **`CombatLoop`** (this GDD) — turn orchestrator, holds `TurnCount`, phase state, energy, event bus. Owns **no** subsystem HP.
- **`SubsystemState`** (V&P GDD, lives on `Vehicle.Slots[]`) — per-slot HP/plating/status/DamageState. Read through `IVehicleView`, written through `IVehicleMutator`.
- **`PositionState`** (this GDD) — small named component on `Vehicle` holding `Position: { Behind, Ahead }`.

R1 contains an explicit non-aggregation clause: `CombatLoop` does not contain `SubsystemState` or `PositionState`. AC-CC32 enforces this at code review. Systems-index C1 scoping note satisfied.

### Cross-GDD Consistency

- **Card System**: Card play pipeline (R3) honors `CardDefinitionSO` contract. Effect-list order preserved per Card System EC10 (effects do not short-circuit on Offline targets). `CardResolutionContext` structure matches what Card System effect implementations consume. Deck reshuffle behavior (EC-CC14) honors Card System EC5.
- **Status Effects**: Fire-before-tick order (R6) honors Status Effects R3. Enrage decoupling (R7) implemented at `CombatLoop` level per Status Effects R6. Redirected RNG (R8) defined. Stalled + Enrage interaction (EC-CC8) prevents stall-break exploit.
- **Vehicle & Part**: Damage pipeline (R5 / F-CC1) reasserts V&P F-VP2 Corrode-before-plating-before-Hp ordering. `DamageSource { Card, Status, Environment }` enum used from V&P R9. Frame=Empty rejection (R11) enforces V&P R3 Frame-death rule. Stat floor convention (R12) is project-wide.
- **Save & Persistence**: RNG seed formula `runSeed ^ combatNodeIndex` uses locked inputs from S&P. Combat does not support mid-combat saves in EA (consistent with V&P EC-VP19 conditional).
- **ADR-0001**: Event-driven view layer honored — all state changes emit events (`SlotStateChanged`, `PositionChanged`, `StatusApplied`, `CardPlayed`, etc.). No MonoBehaviour Update() polls combat state. Zero `UnityEvent` usage (AC-CC30 grep enforcement).

### Open Question Closures

This GDD closes **six** open questions — all six targeted as objectives:

| OQ | Closed By | Summary |
|---|---|---|
| **Status Effects OQ-SE1** (Enrage counter defaults) | R7 | `EnrageTurn=8`, `EnrageInitialBonus=+2`, `EnrageEscalation=+1/turn`, `EnrageTelegraphLeadTurns=2` |
| **Status Effects OQ-SE3** (Redirected RNG distribution) | R8 + F-CC3 | Uniform over non-Offline slots using seeded `System.Random` |
| **Status Effects OQ-SE4** (Offline status semantics) | EC-CC9 | Statuses on Offline slots fire and tick normally; mechanically no-op but kept live for revive-resume |
| **V&P OQ-VP3** (Frame=Empty death ruling) | R11 | `CombatLoop.Setup` throws `InvalidCombatantException`; Frame non-removable mid-combat |
| **V&P OQ-VP4** (mid-resolution deck zone mutation) | R10 | Mutations are immediate; in-flight effect lists run on their start-state order; no re-indexing |
| **V&P OQ-VP5** (stat round/floor convention) | R12 | `(int)Math.Floor(FloatStatValue)` — project-wide, preemptive |

### Internal Consistency

- R2 lifecycle (Setup → PlayerTurn → PlayerResolve → EnemyTurn → (loop) or Ended) matches S1 transition table exhaustively.
- R3 8-step card play pipeline matches AC-CC5 through AC-CC8 test coverage.
- R5 damage pipeline matches F-CC1 composite formula and AC-CC9/10/11.
- R7 Enrage matches F-CC2 formula; AC-CC12–16 cover inactive/active/escalation/telegraph/Stalled interaction cases.
- R8 Redirected matches F-CC3 algorithm; AC-CC17–19 cover uniform distribution, offline-exclusion, and bit-for-bit determinism.
- R6 status tick fire-before-tick matches Status Effects R3 and AC-CC20–22.
- S1 death-check semantics consistent: death cuts phase immediately after R5 step 4, skipping PlayerResolve if enemy dies on player turn (EC-CC13, AC-CC28).
- Enrage docs consistent: EC-CC7 (dead enemy doesn't Enrage), EC-CC8 (Stalled doesn't waste Enrage turn).

### Formula Quality

F-CC1 through F-CC4 all have variables, types, ranges, sources, and worked examples.

- F-CC1 includes a full worked example (Scout Weapon: Corroded 2 + Plating 3 + 6-dmg card → state transition).
- F-CC2 includes inactive (turn 7), active (turn 10) examples.
- F-CC3 includes determinism guarantee text + 4-slot distribution example.
- F-CC4 includes three draw-count scenarios (opening, retained, hand overflow).

### Acceptance Criteria

34 ACs total (32 numbered + 2 playtest-only). All automated ACs testable under NUnit via Unity Test Framework with deterministic seeded RNG. AC-CC17 Redirected test uses `new System.Random(seed: 42)` with 10,000 iterations at 20% ± 1pp per slot — statistically sound. AC-CC24 asserts bit-for-bit determinism across identical seed+input replays. AC-CC30 is a `grep` test enforcing no-`UnityEvent` in Card Combat. AC-CC31/32 are code-review gates enforcing the three-sub-model separation and `IVehicleView`/`IVehicleMutator` boundary.

### Tuning Knobs

All 10 knobs on `CombatRulesSO` with current value + safe range + gameplay effect. Explicit "Not Tuning Knobs (Design Constraints)" list prevents accidental dial-turning on locked pillar contracts (turn order, fire-before-tick, Corrode-before-plating, three-sub-model split, DamageSource enum, Enrage exclusion from player).

### Open Questions (Carried Forward)

- **OQ-CC1** — Enemy retargeting policy per-archetype. EA default: retarget to Frame. Resolves in Enemy System GDD.
- **OQ-CC2** — Combat HUD event animation specifics (timing curves, intent-icon pulse rate, Enrage visual intensity gradient). Resolves in Combat HUD UX Spec.
- **OQ-CC3** — Mid-combat save point policy (loot/reward interstitial save-lock TBD). Resolves in Save & Persistence GDD (minor retrofit).
- **OQ-CC4** — Combat RNG stream isolation (single stream vs per-domain). Forward-deferred until first replay/debug tooling milestone. Not blocking for EA.

### Forward Dependencies (to Propagate)

1. **Status Effects GDD** — add references to Card Combat R7 (Enrage), R8 (Redirected RNG), EC-CC9 (Offline status semantics) so OQ-SE1/SE3/SE4 closures are traceable from that GDD.
2. **Vehicle & Part GDD** — add references to Card Combat R10 (mid-resolution mutation), R11 (Frame-empty rejection), R12 (stat floor convention) so OQ-VP3/VP4/VP5 closures are traceable.
3. **Card System GDD** — per-instance card provenance tracking for part-granted cards (V&P R7, EC-VP11) still needs explicit contract. Card Combat R10 honors the mechanic but the Card System data contract is still a Forward Dependency — fold into Card Combat implementation or address via `/propagate-design-change`.
4. **Enemy System GDD** (future) — `IEnemyBrain` interface, `EnemyIntent` struct, R4 telegraph contract, R6 `OnInvalidTarget` retargeting hook, per-enemy `EnrageTurn` override field.
5. **Combat HUD UX Spec** (future) — event-subscription catalog (all 9 named events) and readability requirements.
6. **Scrap Economy GDD** (future) — mid-combat part-scrap mechanic (if added) must honor R10 in-flight mutation contract.

### Non-Blocking Observations

- Part-granted cards propagation (Forward Dep #3) still outstanding as of this approval. R10 (mid-resolution mutation) honors the mechanic at the Combat layer; the per-instance provenance tracking contract belongs in Card System data. Recommended next step: fold into Card Combat implementation work or run `/propagate-design-change` before `/prototype combat`.
- OQ-CC4 (RNG stream isolation) is genuinely forward-deferrable — a single stream is correct for EA. Flagged here so a future replay-tooling task can find it.
- Visual/Audio and UI Requirements are light by design, citing ADR-0001, Card System, and future Combat HUD GDD as authoritative sources rather than re-deriving. Acceptable for a light-touch MVP GDD.
- EC-CC11 (Stalled duration interaction) contains inline clarification of Stalled's skip-flag semantics — this is technically Status Effects territory but the cross-system interaction is documented here for implementor clarity.

### Files Updated by This Review

- `design/gdd/systems-index.md` — Row 5 (Card Combat System) → Approved; Progress Tracker 5/10 MVP; Next Steps updated.
- `production/session-state/active.md` — next milestone set to `/prototype combat`.
- `design/gdd/reviews/card-combat-system-review-log.md` — this file.

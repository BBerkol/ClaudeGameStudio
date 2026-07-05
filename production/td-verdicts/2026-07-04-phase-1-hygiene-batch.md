---
date: 2026-07-04
topic: Phase 1 hygiene batch — vocabulary cleanup, no behavior change
files_touched:
  - Assets/Scripts/Combat/SlotInstance.cs
  - Assets/Scripts/CombatView/VehicleBarStack.cs
  - Assets/Scripts/Combat/CardEffectOutcome.cs
  - Assets/Scripts/Combat/DamagePipeline.cs
  - Assets/Scripts/CombatView/SaveBootstrap.cs
  - Assets/Scripts/Combat/Archetypes/Dredge.cs  (P1.2 addendum)
adrs_referenced:
  - ADR-0004 (save schema)
  - ADR-0010 (slot single vocabulary)
  - ADR-0011 (no bridges)
  - ADR-0012 (sum-of-parts armor)
verdict: APPROVE
source_audit: production/audits/2026-07-04-1.0-punch-list.md §Phase 1
---

# TD Verdict — Phase 1 Hygiene Batch

## Context

Batch of Phase 1 hygiene-only edits from the 1.0 consolidated punch list:

1. `SlotInstance.cs` (lines 80-88) — xmldoc rewrite on `InstallPart(int maxHp, int armorContribution = 0, ...)`. Reframes the `= 0` default from "legacy / test paths" to the designed common case (weapons/engine/mobility). Signature unchanged.
2. `SlotInstance.cs` (line 108) — xmldoc on `DegradedThreshold`. Drops the "differs from the legacy 50%-of-MaxHp rule" reference (slot-retirement drift per ADR-0010).
3. `VehicleBarStack.cs` (line 983) — comment "or tests that pass bare ints" — same reframing as #1.
4. `CardEffectOutcome.cs` (line 43) — collapses ArmorGained xmldoc "legacy mode vs layout mode" fork to single description.
5. `DamagePipeline.cs` (lines 5, 45-49, 94) — removes "Frame pool vs R_ARM slot" legacy-fork narrative; describes the ADR-0012 sum-of-parts armor slot path as the single reality.
6. `SaveBootstrap.cs` — rename 4 private fields `_xxxAdapter` → `_xxxSerializable` for accuracy against the DTO type names (`NodeMapSerializable`, `RunSeedSerializable`, `RunDeckSerializable`, `VehicleStateSerializable`).

## Technical Director Review

**TD-CHANGE-IMPACT: APPROVE**

This batch is exactly what it claims to be — five files of vocabulary cleanup that reduce ADR drift without touching runtime behavior. Verdict landed against the two memory-flagged traps:

**Gate-check green attestation.** Doc-only edits + symbol renames on private fields cannot break EditMode semantics. `_nodeMapAdapter → _nodeMapSerializable` is a compile-time rename on private state — Unity serialization keys off field name, but SaveBootstrap DTOs are Newtonsoft-serialized per ADR-0004, not Unity-serialized, so no save-file migration risk. Confirm the fields aren't `[SerializeField]` before landing; if any are, they need `[FormerlySerializedAs]` or a bake pass. Assuming they're plain private (standard for SaveBootstrap orchestrator per ADR-0004), attestation stands: recompile-green ≡ semantically-green here.

**Default-param overload trap check (item 1).** The `InstallPart` `armorContribution = 0` xmldoc rewrite is the exact scenario my memory flags — but here you're moving in the *safe* direction: reframing the default as the designed common case rather than a compat path. No signature change, no call-site behavior shift, no recompute-path implication. This is aligning docs with ADR-0012 reality (weapons/engine/mobility legitimately contribute 0), not smuggling a semantic reinterpretation. `Vehicle.cs:264` already frames it correctly, so you're eliminating asymmetric documentation, not introducing new meaning.

**DamagePipeline (item 5)** is the highest-leverage entry — that file's "Frame pool vs R_ARM slot" fork narrative is the single most misleading legacy-residue passage I've seen flagged this quarter. Killing it is pure win.

Land the batch. One commit, `docs:` prefix, reference ADR-0010/0011/0012/0004 in the body.

## Follow-ups

- Before landing SaveBootstrap rename: verify the 4 fields are plain private (not `[SerializeField]`). If they carry the attribute, add `[FormerlySerializedAs("_xxxAdapter")]` in the same commit.
- No new ADR needed; this is drift-reduction against existing accepted ADRs.

## P1.2 addendum — Dredge FrameArmorHp

**Verdict: APPROVE (option C, per user 2026-07-04).** User surfaced three options for restoring `Dredge.cs:96 FrameArmorHp` (currently 0 with a TEMP 2026-06-02 restore-to-60 comment): A restore to 60, B restore to lower interim value, C leave at 0 + drop the TEMP framing. User chose C.

**Xmldoc edit:** collapses the TEMP paragraph into an authored-shipping-value framing — the boss's pacing lever is exposable pressure + hull damage, not the frame armor pool. Removes the "restore to 60" note (design intent supersedes it). No mechanical change; the constant stays 0.

**ADR impact:** none. ADR-0012 sum-of-parts armor stays fully consistent — a 0 FrameArmorHp is legitimate for enemies per memory `feedback_unified_boss_armor_pool` (single-number armor for enemies/bosses can be zero when the boss's tension model routes elsewhere).

**Balance responsibility:** if playtesting on the Cut Chain / Javelin phase reveals the fight lacks armor-drain-as-tension-lever, this is where to revisit — but as a balance follow-up, not as unfinished tech debt.


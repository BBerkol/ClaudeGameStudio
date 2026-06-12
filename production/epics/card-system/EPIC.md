# Epic: Card System

> **Layer**: Foundation
> **GDD**: design/gdd/card-system.md
> **Architecture Module**: `WastelandRun.Cards` (engine-free POCO) + `WastelandRun.Gameplay` (SO authoring types, AddressablesCardCatalog)
> **Status**: Ready
> **Stories**: 10 stories created

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | [Assembly Core Contracts](story-001-assembly-core-contracts.md) | Logic | Ready | ADR-0006 |
| 002 | [CardEffectSO Hierarchy & Validators](story-002-cardeffectso-hierarchy-validators.md) | Logic | Ready | ADR-0006 |
| 003 | [CardDefinitionSO & TokenResolver](story-003-carddefinitionso-tokenresolver.md) | Logic | Ready | ADR-0006 |
| 004 | [ChassisMasteryDefinitionSO & AddressablesCardCatalog](story-004-chassismastery-addressables-catalog.md) | Integration | Ready | ADR-0006 |
| 005 | [RewardDrawAlgorithm — Pool Selection & Determinism](story-005-reward-draw-algorithm-pool-determinism.md) | Logic | Ready | ADR-0006 + ADR-0003 |
| 006 | [RewardDrawAlgorithm — Pity, Innate Cap & Purge Valve](story-006-pity-innate-cap-purge-valve.md) | Logic | Ready | ADR-0006 |
| 007 | [Card States & Deck Lifecycle](story-007-card-states-deck-lifecycle.md) | Logic | Ready | ADR-0006 |
| 008 | [CardSystemDTO & Save Integration](story-008-cardsystemdto-save-integration.md) | Integration | Ready | ADR-0006 + ADR-0004 |
| 009 | [Deck Composition Rules & Edge Cases](story-009-deck-composition-edge-cases.md) | Logic | Ready | ADR-0006 |
| 010 | [Starter Deck Content Authoring](story-010-starter-deck-content-authoring.md) | Config/Data | Ready | ADR-0006 |

## Overview

Implements the entire Card data model and reward pipeline. The engine-free `WastelandRun.Cards` assembly defines the runtime POCO surface: `ICardData`, `ICardEffect`, `ICardEffectCondition`, `CardDraft`, `ICardRewardGenerator`, and `TokenResolver`. The Unity-side `WastelandRun.Gameplay` assembly holds the authoring types: `CardDefinitionSO`, eight sealed `CardEffectSO` subclasses (Damage, RestoreArmor, ApplyStatus, Draw, GainEnergy, Reposition, Exhaust, and conditional variants), three `EffectConditionSO` subclasses (Position, SlotState, Status), and `AddressablesCardCatalog` which loads all card assets via a single `card-definitions` Addressables label partitioned by `ChassisPool`. The `RewardDrawAlgorithm` composes rarity weights, pity counter, primary-family bias (Slot 1), and copy-limit fallback into a single deterministic draw that consumes a caller-owned `System.Random` per ADR-0003. Description tokens resolve at draft-generation and combat-display time through `TokenResolver` — never pre-baked. Persistence follows ADR-0004 via `CardSystemDTO` (deck, discard, exhausted as `List<string>` of CardId, plus `RarePityCounter` and `CardCopyCounts`). This epic delivers the vocabulary layer that every combat, economy, and loot system references.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0006: Card System Data Authoring | Three-assembly split (Cards POCO / Gameplay SOs / Combat reference); Addressables `card-definitions` label; `RewardDrawAlgorithm` with pity + bias + copy-limit; `CardSystemDTO` sealed record | MEDIUM |
| ADR-0004: Save & Persistence Architecture | Passive-orchestrator pattern; `CardSystemDTO` declared with `SystemId = "card-system"`, `SchemaVersion = 1`; background Task atomic writes | MEDIUM |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-card-001 | `CardDefinitionSO` is the single authority for card metadata including slot, effects, rarity, and cost | ADR-0006 ✅ |
| TR-card-002 | `CardId` format `[chassis]_[family]_[sequence]` is stable post-ship and used for save/serialization | ADR-0006 ✅ |
| TR-card-003 | Card description strings use templated `{param}` tokens that resolve at display time from effect SO fields | ADR-0006 ✅ |
| TR-card-004 | `CardEffectSO` hierarchy defines Effect, Condition, and Effect parameters; effects fire in list order per `CardResolutionContext` | ADR-0006 ✅ |
| TR-card-005 | `DamageEffectSO.BypassPlating` flag enables subsystem-strike archetype; bypasses non-Frame Plating in Card Combat F-CC1 step 2 | ADR-0006 ✅ |
| TR-card-006 | `RestoreArmorEffectSO` applies vehicle-level Armor via `IVehicleMutator.AddArmor(int)`, capped at MaxArmor | ADR-0006 ✅ |
| TR-card-007 | Card families (Precision, Assault, Control, Repair, Maneuver) define mechanical role; family taxonomy consistent across chassis | ADR-0006 ✅ |
| TR-card-008 | Control family cards must include at least one `DamageEffectSO` with `Amount >= 1` to prevent pure disruption | ADR-0006 ✅ |
| TR-card-009 | Card keywords (Exhaust, Retain, Innate, Ethereal) are enum flags; Ethereal and Retain mutually exclusive | ADR-0006 ✅ |
| TR-card-010 | Innate keyword caps at 3 cards per deck; cards filtered from offers above cap threshold | ADR-0006 ✅ |
| TR-card-011 | Starter deck is 10 cards per chassis, fixed list per `ChassisDefinitionSO`, with family distribution constraints | ADR-0006 ✅ |
| TR-card-012 | Card rarity weights by mastery stored on `ChassisMasteryDefinitionSO`; must sum to 100 per tier | ADR-0006 ✅ |
| TR-card-013 | Rarity pool exhaustion fallbacks to next-lower tier (Rare→Uncommon→Common) when copy limits reached | ADR-0006 ✅ |
| TR-card-014 | Pity counter tracks 8 consecutive combat offers without Rare; 9th offer guaranteed Rare or Scrap compensation | ADR-0006 ✅ |
| TR-card-015 | Primary-family bias slots Slot 1 from primary pool (Mastery 1–3 only); Slot 2 runs standard F2 | ADR-0006 ✅ |
| TR-card-016 | Copy limits enforced: 3 max per Common/Uncommon, 1 max per Rare; no duplicates in single 2-card offer | ADR-0006 ✅ |
| TR-card-017 | Card states (In Deck, In Hand, In Discard, Exhausted) define location; Discard reshuffles when deck empty | ADR-0006 ✅ |
| TR-card-018 | Card purge at Chopshop costs flat `GlobalPurgeCost = 30` Scrap; deck cannot drop below 10 cards | ADR-0006 ✅ |
| TR-card-019 | Free purge valve rolls 33% per Chopshop visit based on `runSeed + nodeIndex`; persists mid-visit | ADR-0006 ✅ |
| TR-card-020 | Damage output formula: `BaseDamage + (PositionBonus * PositionConditionMet)`; range 1–20 EA | ADR-0006 ✅ |
| TR-card-021 | `ICardRewardGenerator.Generate()` interface provides `CardDraft[]` with resolved tokens; no SO reads by L&R | ADR-0006 ✅ |
| TR-card-022 | Card effect resolution reads vehicle state only via `IVehicleView`; mutates only via `IVehicleMutator` | ADR-0006 ✅ |
| TR-card-023 | `EffectConditionSO` hierarchy (Position, SlotState, Status) gates effect application per condition evaluation | ADR-0006 ✅ |
| TR-card-024 | `DrawCardsEffectSO` with empty family-filtered deck silently produces nothing; no error or fallback | ADR-0006 ✅ |
| TR-card-025 | SO import validates `BaseDamage == DamageEffectSO.Amount` and enforces `BaseDamage >= 1` on damage cards | ADR-0006 ✅ |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/card-system.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- `WastelandRun.Cards` assembly compiles with `noEngineReferences: true` verified by CI
- `AddressablesCardCatalog` loads `card-definitions` label in Unity Editor and Play Mode
- `RewardDrawAlgorithm` passes determinism regression: 10,000-sample seed replay produces byte-identical output
- `CardSystemDTO` round-trips through `SaveManager` with no data loss (deck + discard + exhausted + pity counter)
- SO import validators reject malformed `CardDefinitionSO` and `CardEffectSO` assets

## Next Step

Run `/create-stories card-system` to break this epic into implementable stories.

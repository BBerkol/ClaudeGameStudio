// WastelandRun.Tests.Gameplay — test-support.cs
// Shared fixtures for combat-loop integration tests: a minimal ICardData record and
// a player chassis builder that mirrors the Enemies test fixture.

using System;
using System.Collections.Generic;
using WastelandRun.Cards;
using WastelandRun.Enemies;
using WastelandRun.Vehicle;

namespace WastelandRun.Tests.Gameplay
{
    /// <summary>
    /// Inline <see cref="ICardData"/> implementation used to author fixture decks
    /// without dragging the Unity SerializedObject pipeline into the test runner.
    /// </summary>
    public sealed class FixtureCard : ICardData
    {
        public string CardId { get; init; } = "fixture";
        public string DisplayName { get; init; } = "Fixture";
        public string DescriptionTemplate { get; init; } = "";
        public string FlavorText { get; init; } = "";
        public string CardArtKey { get; init; } = "";
        public ChassisType ChassisPool { get; init; } = ChassisType.Scout;
        public CardFamily Family { get; init; } = CardFamily.Assault;
        public CardRarity Rarity { get; init; } = CardRarity.Common;
        public bool IsStarterCard { get; init; } = true;
        public int EnergyCost { get; init; } = 1;
        public int MerchantPrice { get; init; } = 0;
        public CardTargetType TargetType { get; init; } = CardTargetType.EnemySubsystem;
        public IReadOnlyList<SlotType> ValidSubsystemTargets { get; init; } = Array.Empty<SlotType>();
        public WastelandRun.Cards.PositionRequirement PositionRequirement { get; init; } = WastelandRun.Cards.PositionRequirement.None;
        public CardKeyword Keywords { get; init; } = CardKeyword.None;
        public IReadOnlyList<ICardEffect> Effects { get; init; } = Array.Empty<ICardEffect>();
        public int BaseDamage { get; init; } = 0;
        public string SourceSlotId { get; init; } = null;

        /// <summary>Convenience: simple damage card targeting an arbitrary subsystem.</summary>
        public static FixtureCard Damage(string id, int cost, int baseDamage) => new FixtureCard
        {
            CardId      = id,
            DisplayName = id,
            EnergyCost  = cost,
            BaseDamage  = baseDamage,
            TargetType  = CardTargetType.EnemySubsystem,
            Effects     = new ICardEffect[]
            {
                new DamageEffect(Amount: 0, PositionBonus: 0, BypassPlating: false, Conditions: Array.Empty<ICardEffectCondition>()),
            },
        };
    }

    /// <summary>Builds the same Scout chassis used by the Enemies fixture so test damage maths line up.</summary>
    public static class TestPlayerFixtures
    {
        public const string PartIdWeapon   = "test_player_weapon";
        public const string PartIdEngine   = "test_player_engine";
        public const string PartIdMobility = "test_player_mobility";
        public const string PartIdFrame    = "test_player_frame";

        public static IChassisData BuildScoutChassis(int frameArmor = 4)
        {
            var weapon   = new FixturePart(PartIdWeapon,   SlotType.Weapon,   ChassisType.Scout, maxPlating: 2);
            var engine   = new FixturePart(PartIdEngine,   SlotType.Engine,   ChassisType.Scout, maxPlating: 2);
            var mobility = new FixturePart(PartIdMobility, SlotType.Mobility, ChassisType.Scout, maxPlating: 2);
            var frame    = new FixturePart(PartIdFrame,    SlotType.Frame,    ChassisType.Scout,
                                            maxPlating: 0, armorContribution: frameArmor);

            var slotMaxHp = new Dictionary<SlotType, int>
            {
                { SlotType.Weapon,    8 },
                { SlotType.Engine,    8 },
                { SlotType.Mobility, 10 },
                { SlotType.Frame,    20 },
            };

            var starterParts = new Dictionary<SlotType, IPartData>
            {
                { SlotType.Weapon,   weapon   },
                { SlotType.Engine,   engine   },
                { SlotType.Mobility, mobility },
                { SlotType.Frame,    frame    },
            };

            return new FixtureChassis(ChassisType.Scout, slotMaxHp, starterParts);
        }

        public static WastelandRun.Vehicle.Vehicle SpawnPlayer(int frameArmor = 4) =>
            VehicleFactory.FromChassis(BuildScoutChassis(frameArmor));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WastelandRun.Cards;
using WastelandRun.Gameplay.Cards;
using WastelandRun.Vehicle;

// EditMode test — CardDefinitionSO reflection (AC-1a) requires Unity assemblies to be loaded.
namespace WastelandRun.Tests.Unit.CardSystem
{
    [TestFixture]
    public class TokenResolverTest
    {
        private TokenResolver _resolver;

        [SetUp]
        public void SetUp() => _resolver = new TokenResolver();

        // ─────────────────────────────────────────────────────────────────────────
        // AC-1a: CardDefinitionSO serialized field declaration (reflection)
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        [TestCase("_cardId",             typeof(string))]
        [TestCase("_displayName",        typeof(string))]
        [TestCase("_descriptionTemplate",typeof(string))]
        [TestCase("_family",             typeof(CardFamily))]
        [TestCase("_rarity",             typeof(CardRarity))]
        [TestCase("_chassisPool",        typeof(ChassisType))]
        [TestCase("_isStarterCard",      typeof(bool))]
        [TestCase("_energyCost",         typeof(int))]
        [TestCase("_targetType",         typeof(CardTargetType))]
        [TestCase("_keywords",           typeof(CardKeyword))]
        [TestCase("_effectSOs",          typeof(CardEffectSO[]))]
        public void test_CardDefinitionSO_RequiredSerializedField_ExistsWithCorrectType(
            string fieldName, Type expectedType)
        {
            // Arrange
            var type = typeof(CardDefinitionSO);

            // Act
            var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);

            // Assert
            Assert.IsNotNull(field,
                $"CardDefinitionSO is missing required field '{fieldName}'");
            Assert.AreEqual(expectedType, field!.FieldType,
                $"Field '{fieldName}' has type {field.FieldType.Name} but expected {expectedType.Name}");
            Assert.IsNotNull(field.GetCustomAttribute<SerializeField>(),
                $"Field '{fieldName}' is missing [SerializeField] attribute");
        }

        [Test]
        public void test_CardDefinitionSO_SerializedFieldCount_AtLeastElevenRequired()
        {
            // Arrange — guards against a required field being renamed and replaced
            var type = typeof(CardDefinitionSO);
            var requiredNames = new[]
            {
                "_cardId", "_displayName", "_descriptionTemplate", "_family", "_rarity",
                "_chassisPool", "_isStarterCard", "_energyCost", "_targetType", "_keywords", "_effectSOs"
            };

            // Act
            var allSerializedFields = type
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.GetCustomAttribute<SerializeField>() != null)
                .Select(f => f.Name)
                .ToHashSet();

            // Assert — all 11 required names must be present in the serialized set
            foreach (var name in requiredNames)
                Assert.IsTrue(allSerializedFields.Contains(name),
                    $"Required [SerializeField] field '{name}' not found among serialized fields");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-2: All 10 standard tokens resolve to correct values
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_TokenResolver_AllTenStandardTokens_ResolveToCorrectValues()
        {
            // Arrange
            var card = new MockCardData(
                descriptionTemplate: "{damage} {bonus} {heal} {plating} {armor} {draws} {energy} {stacks} {duration} {cost}",
                energyCost: 3,
                effects: new List<ICardEffect>
                {
                    new DamageEffect(Amount: 5, PositionBonus: 3, BypassPlating: false,
                        Conditions: Array.Empty<ICardEffectCondition>()),
                    new RepairSubsystemEffect(HpRestored: 4, CanReviveOffline: false),
                    new RestorePlatingEffect(Stacks: 2, TargetSlot: SlotType.Frame),
                    new RestoreArmorEffect(Amount: 6),
                    new DrawCardsEffect(Count: 2, FamilyFilter: null),
                    new GainEnergyEffect(Amount: 1),
                    new ApplyStatusEffect(Status: StatusType.Burning, Stacks: 3, Duration: 2, TargetSlot: null),
                });

            // Act
            string result = _resolver.Resolve(card);

            // Assert — space-separated order matches template
            Assert.AreEqual("5 3 4 2 6 2 1 3 2 3", result);
        }

        [Test]
        public void test_TokenResolver_CardMissingMatchingEffect_MissingTokenResolvesToQuestionMark()
        {
            // Arrange — only DamageEffect; {heal} has no matching effect
            var card = new MockCardData(
                descriptionTemplate: "Deal {damage} — restore {heal}",
                energyCost: 2,
                effects: new List<ICardEffect>
                {
                    new DamageEffect(Amount: 7, PositionBonus: 0, BypassPlating: false,
                        Conditions: Array.Empty<ICardEffectCondition>()),
                });

            // Act
            string result = _resolver.Resolve(card);

            // Assert
            Assert.AreEqual("Deal 7 — restore ?", result);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-3: Indexed token resolution
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_TokenResolver_IndexedDamageTokens_ResolveToDifferentEffectAmounts()
        {
            // Arrange — two DamageEffects: 5 and 8
            var card = new MockCardData(
                descriptionTemplate: "Deal {damage.1} then {damage.2}",
                energyCost: 4,
                effects: new List<ICardEffect>
                {
                    new DamageEffect(Amount: 5, PositionBonus: 0, BypassPlating: false,
                        Conditions: Array.Empty<ICardEffectCondition>()),
                    new DamageEffect(Amount: 8, PositionBonus: 0, BypassPlating: false,
                        Conditions: Array.Empty<ICardEffectCondition>()),
                });

            // Act
            string result = _resolver.Resolve(card);

            // Assert
            Assert.AreEqual("Deal 5 then 8", result);
        }

        [Test]
        public void test_TokenResolver_UnindexedDamageToken_TreatedAsIndexOne()
        {
            // Arrange — {damage} with no index is treated as {damage.1}
            var card = new MockCardData(
                descriptionTemplate: "{damage}",
                energyCost: 1,
                effects: new List<ICardEffect>
                {
                    new DamageEffect(Amount: 5, PositionBonus: 0, BypassPlating: false,
                        Conditions: Array.Empty<ICardEffectCondition>()),
                    new DamageEffect(Amount: 8, PositionBonus: 0, BypassPlating: false,
                        Conditions: Array.Empty<ICardEffectCondition>()),
                });

            // Act
            string result = _resolver.Resolve(card);

            // Assert — resolves to first DamageEffect only
            Assert.AreEqual("5", result);
        }

        [Test]
        public void test_TokenResolver_OutOfBoundsIndexedToken_ResolvesToQuestionMark()
        {
            // Arrange — two DamageEffects; {damage.3} is beyond range
            var card = new MockCardData(
                descriptionTemplate: "{damage.3}",
                energyCost: 1,
                effects: new List<ICardEffect>
                {
                    new DamageEffect(Amount: 5, PositionBonus: 0, BypassPlating: false,
                        Conditions: Array.Empty<ICardEffectCondition>()),
                    new DamageEffect(Amount: 8, PositionBonus: 0, BypassPlating: false,
                        Conditions: Array.Empty<ICardEffectCondition>()),
                });

            // Act
            string result = _resolver.Resolve(card);

            // Assert
            Assert.AreEqual("?", result);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-4: Unknown token renders as "?"
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_TokenResolver_UnknownToken_ResolvesToQuestionMark()
        {
            // Arrange
            var card = new MockCardData(
                descriptionTemplate: "Deal {dmg} damage",
                energyCost: 2,
                effects: new List<ICardEffect>());

            // Act
            string result = _resolver.Resolve(card);

            // Assert
            Assert.AreEqual("Deal ? damage", result);
        }

        [Test]
        public void test_TokenResolver_MultipleUnknownTokens_EachIndependentlyResolvesToQuestionMark()
        {
            // Arrange
            var card = new MockCardData(
                descriptionTemplate: "{abc} and {xyz}",
                energyCost: 1,
                effects: new List<ICardEffect>());

            // Act
            string result = _resolver.Resolve(card);

            // Assert
            Assert.AreEqual("? and ?", result);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-5: DamageEffect.Amount=0 — defensive posture (resolver does not throw)
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_TokenResolver_DamageEffectAmountZero_ResolvesToZeroNotQuestionMark()
        {
            // Arrange — Amount=0 is rejected by OnValidate at SO import; test is resolver-defensive only
            var card = new MockCardData(
                descriptionTemplate: "Deal {damage} damage",
                energyCost: 0,
                effects: new List<ICardEffect>
                {
                    new DamageEffect(Amount: 0, PositionBonus: 0, BypassPlating: false,
                        Conditions: Array.Empty<ICardEffectCondition>()),
                });

            // Act
            string result = _resolver.Resolve(card);

            // Assert — "0", not "?", not null, no throw
            Assert.AreEqual("Deal 0 damage", result);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AC-6: F1 formula boundary values
        //       DamageOutput = BaseDamage + (PositionBonus × positionConditionMet)
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        [TestCase(1,  0, false,  1, TestName = "BaseDamage1_NoBonus_CondFalse_Yields1")]
        [TestCase(1,  0, true,   1, TestName = "BaseDamage1_NoBonus_CondTrue_Yields1")]
        [TestCase(12, 8, true,  20, TestName = "BaseDamage12_Bonus8_CondTrue_Yields20")]
        [TestCase(12, 8, false, 12, TestName = "BaseDamage12_Bonus8_CondFalse_Yields12")]
        public void test_DamageEffect_ComputeOutput_F1FormulaBoundaryValues(
            int baseDamage, int positionBonus, bool positionConditionMet, int expected)
        {
            // Act
            int output = DamageEffect.ComputeOutput(baseDamage, positionBonus, positionConditionMet);

            // Assert
            Assert.AreEqual(expected, output,
                $"F1: ComputeOutput({baseDamage}, {positionBonus}, {positionConditionMet}) expected {expected}");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Null guard
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void test_TokenResolver_NullCardData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _resolver.Resolve(null!));
        }

        [Test]
        public void test_TokenResolver_RepeatedTokenInTemplate_AllOccurrencesReplaced()
        {
            // Arrange — same token appears twice; regex Replace must substitute all occurrences
            var card = new MockCardData(
                descriptionTemplate: "Deal {damage} and {damage} again",
                energyCost: 1,
                effects: new List<ICardEffect>
                {
                    new DamageEffect(Amount: 5, PositionBonus: 0, BypassPlating: false,
                        Conditions: Array.Empty<ICardEffectCondition>()),
                });

            // Act
            string result = _resolver.Resolve(card);

            // Assert — both occurrences resolved, not just the first
            Assert.AreEqual("Deal 5 and 5 again", result);
        }

        [Test]
        public void test_TokenResolver_EmptyDescriptionTemplate_ReturnsEmptyString()
        {
            // Arrange
            var card = new MockCardData(
                descriptionTemplate: "",
                energyCost: 1,
                effects: new List<ICardEffect>());

            // Act + Assert — no throw, empty result
            Assert.AreEqual("", _resolver.Resolve(card));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Test double
    // ─────────────────────────────────────────────────────────────────────────────

    internal sealed class MockCardData : ICardData
    {
        private readonly List<ICardEffect> _effects;

        public MockCardData(string descriptionTemplate, int energyCost, List<ICardEffect> effects)
        {
            DescriptionTemplate = descriptionTemplate;
            EnergyCost = energyCost;
            _effects = effects;
        }

        public string CardId => "mock_test_001";
        public string DisplayName => "Mock Card";
        public string DescriptionTemplate { get; }
        public string FlavorText => string.Empty;
        public string CardArtKey => string.Empty;
        public ChassisType ChassisPool => default;
        public CardFamily Family => default;
        public CardRarity Rarity => default;
        public bool IsStarterCard => false;
        public int EnergyCost { get; }
        public int MerchantPrice => 0;
        public CardTargetType TargetType => default;
        public IReadOnlyList<SlotType> ValidSubsystemTargets => Array.Empty<SlotType>();
        public PositionRequirement PositionRequirement => default;
        public CardKeyword Keywords => default;
        public IReadOnlyList<ICardEffect> Effects => _effects;
        public int BaseDamage => 0;
        public string? SourceSlotId => null;
    }
}

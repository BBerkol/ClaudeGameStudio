using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WastelandRun.Cards;
using WastelandRun.Vehicle;

namespace WastelandRun.Tests.Unit.CardSystem
{
    [TestFixture]
    public class AssemblyContractsTests
    {
        // -------------------------------------------------------------------------
        // AC-1: Assembly has no UnityEngine references
        // -------------------------------------------------------------------------

        [Test]
        public void test_CardAssembly_HasNoUnityEngineReferences()
        {
            var assembly = Assembly.GetAssembly(typeof(ICardData));
            var referenced = assembly!.GetReferencedAssemblies();
            foreach (var name in referenced)
                Assert.IsFalse(name.Name!.Contains("UnityEngine"),
                    $"WastelandRun.Cards must not reference UnityEngine — found: {name.Name}");
        }

        // -------------------------------------------------------------------------
        // AC-2: ICardData exposes exactly 18 required members
        // -------------------------------------------------------------------------

        [Test]
        public void test_ICardData_ExposesExactly18RequiredMembers()
        {
            // Compile-time verification: assigning a concrete mock via the interface
            // and accessing all 18 members proves the interface exposes them.
            ICardData card = new MockCardData();

            // Access all 18 members through the interface reference
            _ = card.CardId;
            _ = card.DisplayName;
            _ = card.DescriptionTemplate;
            _ = card.FlavorText;
            _ = card.CardArtKey;
            _ = card.ChassisPool;
            _ = card.Family;
            _ = card.Rarity;
            _ = card.IsStarterCard;
            _ = card.EnergyCost;
            _ = card.MerchantPrice;
            _ = card.TargetType;
            _ = card.ValidSubsystemTargets;
            _ = card.PositionRequirement;
            _ = card.Keywords;
            _ = card.Effects;
            _ = card.BaseDamage;
            _ = card.SourceSlotId;

            // Reflection: assert exactly 18 members on the interface
            var members = typeof(ICardData).GetProperties(BindingFlags.Instance | BindingFlags.Public);
            Assert.AreEqual(18, members.Length,
                $"ICardData must expose exactly 18 properties, found {members.Length}: {string.Join(", ", members.Select(m => m.Name))}");
        }

        // -------------------------------------------------------------------------
        // AC-3: ICardEffect is pure marker — zero methods
        // -------------------------------------------------------------------------

        [Test]
        public void test_ICardEffect_GetMethods_ReturnsEmpty()
        {
            var methods = typeof(ICardEffect).GetMethods();
            Assert.AreEqual(0, methods.Length,
                "ICardEffect must have zero methods — it is a pure marker interface");
        }

        [Test]
        public void test_ICardEffect_GetMethods_WithInstancePublicFlags_ReturnsEmpty()
        {
            var methods = typeof(ICardEffect).GetMethods(BindingFlags.Instance | BindingFlags.Public);
            Assert.AreEqual(0, methods.Length,
                "ICardEffect must have zero instance public methods");
        }

        // -------------------------------------------------------------------------
        // AC-4: DamageEffect carries Conditions
        // -------------------------------------------------------------------------

        [Test]
        public void test_DamageEffect_WithTwoConditions_ConditionsCountIsTwo()
        {
            var conditions = new ICardEffectCondition[]
            {
                new PositionCondition(PositionRequirement.BonusIfAhead),
                new PositionCondition(PositionRequirement.RequiresBehind)
            };
            var effect = new DamageEffect(5, 2, false, conditions);

            Assert.AreEqual(2, effect.Conditions.Count);
            Assert.AreSame(conditions[0], effect.Conditions[0]);
            Assert.AreSame(conditions[1], effect.Conditions[1]);
        }

        [Test]
        public void test_DamageEffect_WithNoConditions_ConditionsCountIsZero_NotNull()
        {
            var effect = new DamageEffect(3, 0, false, Array.Empty<ICardEffectCondition>());
            Assert.IsNotNull(effect.Conditions);
            Assert.AreEqual(0, effect.Conditions.Count);
        }

        // -------------------------------------------------------------------------
        // AC-5a: ICardEffectCondition is pure marker — zero methods and properties
        // -------------------------------------------------------------------------

        [Test]
        public void test_ICardEffectCondition_GetMethods_ReturnsEmpty()
        {
            var methods = typeof(ICardEffectCondition)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public);
            Assert.AreEqual(0, methods.Length,
                "ICardEffectCondition must have zero instance public methods — no IsMet()");
        }

        [Test]
        public void test_ICardEffectCondition_GetProperties_ReturnsEmpty()
        {
            var props = typeof(ICardEffectCondition).GetProperties();
            Assert.AreEqual(0, props.Length,
                "ICardEffectCondition must have zero properties");
        }

        [Test]
        public void test_PositionCondition_ImplementsICardEffectCondition()
        {
            Assert.IsTrue(typeof(PositionCondition).IsAssignableTo(typeof(ICardEffectCondition)));
        }

        [Test]
        public void test_SlotStateCondition_ImplementsICardEffectCondition()
        {
            Assert.IsTrue(typeof(SlotStateCondition).IsAssignableTo(typeof(ICardEffectCondition)));
        }

        [Test]
        public void test_StatusCondition_ImplementsICardEffectCondition()
        {
            Assert.IsTrue(typeof(StatusCondition).IsAssignableTo(typeof(ICardEffectCondition)));
        }

        // -------------------------------------------------------------------------
        // AC-5b: CardRarity enum has exactly 4 values
        // -------------------------------------------------------------------------

        [Test]
        public void test_CardRarity_HasExactlyFourValues()
        {
            var values = Enum.GetValues(typeof(CardRarity));
            Assert.AreEqual(4, values.Length,
                "CardRarity must have exactly 4 values (Common, Uncommon, Rare, Legendary)");
        }

        [Test]
        public void test_CardRarity_ContainsCommonUncommonRareLegendary()
        {
            var names = Enum.GetNames(typeof(CardRarity));
            Assert.Contains("Common", names);
            Assert.Contains("Uncommon", names);
            Assert.Contains("Rare", names);
            Assert.Contains("Legendary", names);
        }

        // -------------------------------------------------------------------------
        // AC-5c: Legendary excluded from reward draws
        // RewardDrawAlgorithm is implemented in Story 005.
        // This test documents the contract at the interface level.
        // -------------------------------------------------------------------------

        [Test]
        [Ignore("Story-005: RewardDrawAlgorithm not yet implemented — activate when Story 005 is Done")]
        public void test_Legendary_NeverSelectedIn10000Draws_Seed42()
        {
            // When RewardDrawAlgorithm exists:
            // var catalog = new LegendaryOnlyCatalogStub();
            // var weights = new UniformWeightsStub();
            // var resolver = new TokenResolver();
            // var algorithm = new RewardDrawAlgorithm(catalog, weights, resolver);
            // var rng = new System.Random(42);
            // int legendaryCount = 0;
            // for (int i = 0; i < 10_000; i++)
            // {
            //     var drafts = algorithm.Generate(ChassisType.Scout, 1, 0, 1, Array.Empty<string>(), rng);
            //     legendaryCount += drafts.Count(d => d.Rarity == CardRarity.Legendary);
            // }
            // Assert.AreEqual(0, legendaryCount, "Legendary must never be selected in EA reward draws");
        }

        // -------------------------------------------------------------------------
        // AC-6: ICardRewardGenerator.Generate exact signature
        // -------------------------------------------------------------------------

        [Test]
        public void test_ICardRewardGenerator_Generate_HasCorrectSignature()
        {
            // Compile-time test: a lambda conforming to the exact signature can be assigned
            // to a local variable of the interface's delegate shape.
            // The key assertion is that System.Random rng is in the signature, NOT int seed.
            Func<ChassisType, int, int, int, IReadOnlyList<string>, System.Random, CardDraft[]> conforming =
                (chassis, mastery, pity, draws, deck, rng) => Array.Empty<CardDraft>();

            var method = typeof(ICardRewardGenerator).GetMethod("Generate");
            Assert.IsNotNull(method, "ICardRewardGenerator must declare Generate");

            var parameters = method!.GetParameters();
            Assert.AreEqual(6, parameters.Length, "Generate must have exactly 6 parameters");
            Assert.AreEqual(typeof(System.Random), parameters[5].ParameterType,
                "6th parameter must be System.Random (not int seed)");
            Assert.AreEqual(typeof(CardDraft[]), method.ReturnType,
                "Generate must return CardDraft[]");
        }

        // -------------------------------------------------------------------------
        // AC-7: CardDraft has exactly 10 public non-static fields/properties
        // -------------------------------------------------------------------------

        [Test]
        public void test_CardDraft_HasExactlyTenPublicNonStaticMembers()
        {
            // EqualityContract is a protected (not public) synthesized property on sealed records —
            // it does not appear in a BindingFlags.Public query, so the count is exactly 10.
            var props = typeof(CardDraft)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.Name != "EqualityContract")
                .ToArray();
            Assert.AreEqual(10, props.Length,
                $"CardDraft must have exactly 10 public instance properties, found {props.Length}: {string.Join(", ", props.Select(p => p.Name))}");
        }

        [Test]
        public void test_CardDraft_AllTenFieldsMatchExpectedNameAndType()
        {
            // Instantiate a CardDraft and verify both name and type — count-only is insufficient.
            var draft = new CardDraft
            {
                CardId        = "scout_precision_001",
                DisplayName   = "Test",
                RulesText     = "Deal {damage}",
                Family        = CardFamily.Precision,
                Rarity        = CardRarity.Common,
                EnergyCost    = 1,
                CardArtKey    = "art_key",
                KeywordBadges = Array.Empty<string>(),
                MerchantPrice = null,
                SelectionHash = 42
            };

            Assert.AreEqual("scout_precision_001", draft.CardId);
            Assert.AreEqual("Test", draft.DisplayName);
            Assert.AreEqual("Deal {damage}", draft.RulesText);
            Assert.AreEqual(CardFamily.Precision, draft.Family);
            Assert.AreEqual(CardRarity.Common, draft.Rarity);
            Assert.AreEqual(1, draft.EnergyCost);
            Assert.AreEqual("art_key", draft.CardArtKey);
            Assert.IsNotNull(draft.KeywordBadges);
            Assert.IsNull(draft.MerchantPrice);
            Assert.AreEqual(42, draft.SelectionHash);

            // Verify the types via reflection
            var byName = typeof(CardDraft)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .ToDictionary(p => p.Name);

            Assert.AreEqual(typeof(string),                   byName["CardId"].PropertyType);
            Assert.AreEqual(typeof(string),                   byName["DisplayName"].PropertyType);
            Assert.AreEqual(typeof(string),                   byName["RulesText"].PropertyType);
            Assert.AreEqual(typeof(CardFamily),               byName["Family"].PropertyType);
            Assert.AreEqual(typeof(CardRarity),               byName["Rarity"].PropertyType);
            Assert.AreEqual(typeof(int),                      byName["EnergyCost"].PropertyType);
            Assert.AreEqual(typeof(string),                   byName["CardArtKey"].PropertyType);
            Assert.AreEqual(typeof(IReadOnlyList<string>),    byName["KeywordBadges"].PropertyType);
            Assert.AreEqual(typeof(int?),                     byName["MerchantPrice"].PropertyType);
            Assert.AreEqual(typeof(int),                      byName["SelectionHash"].PropertyType);
        }

        // -------------------------------------------------------------------------
        // AC-8: TokenResolver resolves all 10 standard tokens
        // -------------------------------------------------------------------------

        private readonly TokenResolver _resolver = new TokenResolver();

        [Test]
        public void test_TokenResolver_Resolve_DamageToken_ReturnsFirstDamageEffectAmount()
        {
            var card = new MockCardData
            {
                DescriptionTemplate = "Deal {damage} damage.",
                Effects = new ICardEffect[] { new DamageEffect(7, 0, false, Array.Empty<ICardEffectCondition>()) }
            };
            Assert.AreEqual("Deal 7 damage.", _resolver.Resolve(card));
        }

        [Test]
        public void test_TokenResolver_Resolve_BonusToken_ReturnsFirstDamageEffectPositionBonus()
        {
            var card = new MockCardData
            {
                DescriptionTemplate = "+{bonus} if ahead.",
                Effects = new ICardEffect[] { new DamageEffect(5, 3, false, Array.Empty<ICardEffectCondition>()) }
            };
            Assert.AreEqual("+3 if ahead.", _resolver.Resolve(card));
        }

        [Test]
        public void test_TokenResolver_Resolve_HealToken_ReturnsFirstRepairSubsystemEffectHpRestored()
        {
            var card = new MockCardData
            {
                DescriptionTemplate = "Restore {heal} HP.",
                Effects = new ICardEffect[] { new RepairSubsystemEffect(4, false) }
            };
            Assert.AreEqual("Restore 4 HP.", _resolver.Resolve(card));
        }

        [Test]
        public void test_TokenResolver_Resolve_PlatingToken_ReturnsFirstRestorePlatingEffectStacks()
        {
            var card = new MockCardData
            {
                DescriptionTemplate = "Add {plating} Plating.",
                Effects = new ICardEffect[] { new RestorePlatingEffect(2, SlotType.Engine) }
            };
            Assert.AreEqual("Add 2 Plating.", _resolver.Resolve(card));
        }

        [Test]
        public void test_TokenResolver_Resolve_ArmorToken_ReturnsFirstRestoreArmorEffectAmount()
        {
            var card = new MockCardData
            {
                DescriptionTemplate = "Gain {armor} Armor.",
                Effects = new ICardEffect[] { new RestoreArmorEffect(3) }
            };
            Assert.AreEqual("Gain 3 Armor.", _resolver.Resolve(card));
        }

        [Test]
        public void test_TokenResolver_Resolve_DrawsToken_ReturnsFirstDrawCardsEffectCount()
        {
            var card = new MockCardData
            {
                DescriptionTemplate = "Draw {draws} cards.",
                Effects = new ICardEffect[] { new DrawCardsEffect(2, null) }
            };
            Assert.AreEqual("Draw 2 cards.", _resolver.Resolve(card));
        }

        [Test]
        public void test_TokenResolver_Resolve_EnergyToken_ReturnsFirstGainEnergyEffectAmount()
        {
            var card = new MockCardData
            {
                DescriptionTemplate = "Gain {energy} Energy.",
                Effects = new ICardEffect[] { new GainEnergyEffect(1) }
            };
            Assert.AreEqual("Gain 1 Energy.", _resolver.Resolve(card));
        }

        [Test]
        public void test_TokenResolver_Resolve_StacksToken_ReturnsFirstApplyStatusEffectStacks()
        {
            var card = new MockCardData
            {
                DescriptionTemplate = "Apply {stacks} Burning.",
                Effects = new ICardEffect[] { new ApplyStatusEffect(StatusType.Burning, 3, 2, null) }
            };
            Assert.AreEqual("Apply 3 Burning.", _resolver.Resolve(card));
        }

        [Test]
        public void test_TokenResolver_Resolve_DurationToken_ReturnsFirstApplyStatusEffectDuration()
        {
            var card = new MockCardData
            {
                DescriptionTemplate = "Burning for {duration} turns.",
                Effects = new ICardEffect[] { new ApplyStatusEffect(StatusType.Burning, 3, 2, null) }
            };
            Assert.AreEqual("Burning for 2 turns.", _resolver.Resolve(card));
        }

        [Test]
        public void test_TokenResolver_Resolve_CostToken_ReturnsEnergyCostAsDecimalString()
        {
            var card = new MockCardData
            {
                DescriptionTemplate = "Costs {cost} Energy.",
                EnergyCost = 2
            };
            Assert.AreEqual("Costs 2 Energy.", _resolver.Resolve(card));
        }

        [Test]
        public void test_TokenResolver_Resolve_UnknownToken_ReturnsQuestionMark()
        {
            var card = new MockCardData
            {
                DescriptionTemplate = "Does {unknown} thing.",
                Effects = Array.Empty<ICardEffect>()
            };
            Assert.AreEqual("Does ? thing.", _resolver.Resolve(card));
        }

        [Test]
        public void test_TokenResolver_Resolve_NoThrow_OnNullOrMissingEffect()
        {
            var card = new MockCardData
            {
                DescriptionTemplate = "Deal {damage} and heal {heal}.",
                Effects = Array.Empty<ICardEffect>()
            };
            string result = null!;
            Assert.DoesNotThrow(() => result = _resolver.Resolve(card));
            Assert.AreEqual("Deal ? and heal ?.", result);
        }

        // -------------------------------------------------------------------------
        // AC-9: TokenResolver indexed tokens
        // -------------------------------------------------------------------------

        [Test]
        public void test_TokenResolver_IndexedDamage1_ReturnsFirstAmount_WhenTwoEffects()
        {
            var card = new MockCardData
            {
                DescriptionTemplate = "{damage.1} then {damage.2}.",
                Effects = new ICardEffect[]
                {
                    new DamageEffect(5, 0, false, Array.Empty<ICardEffectCondition>()),
                    new DamageEffect(8, 0, false, Array.Empty<ICardEffectCondition>())
                }
            };
            Assert.AreEqual("5 then 8.", _resolver.Resolve(card));
        }

        [Test]
        public void test_TokenResolver_IndexedDamage2_ReturnsSecondAmount_WhenTwoEffects()
        {
            var card = new MockCardData
            {
                DescriptionTemplate = "{damage.2}",
                Effects = new ICardEffect[]
                {
                    new DamageEffect(5, 0, false, Array.Empty<ICardEffectCondition>()),
                    new DamageEffect(8, 0, false, Array.Empty<ICardEffectCondition>())
                }
            };
            Assert.AreEqual("8", _resolver.Resolve(card));
        }

        [Test]
        public void test_TokenResolver_IndexedDamage2_ReturnsQuestionMark_WhenOnlyOneEffect()
        {
            var card = new MockCardData
            {
                DescriptionTemplate = "{damage.2}",
                Effects = new ICardEffect[]
                {
                    new DamageEffect(5, 0, false, Array.Empty<ICardEffectCondition>())
                }
            };
            Assert.AreEqual("?", _resolver.Resolve(card));
        }

        [Test]
        public void test_TokenResolver_IndexedDamage1_ReturnsQuestionMark_WhenNoDamageEffect()
        {
            var card = new MockCardData
            {
                DescriptionTemplate = "{damage.1}",
                Effects = new ICardEffect[] { new GainEnergyEffect(1) }
            };
            Assert.AreEqual("?", _resolver.Resolve(card));
        }

        // -------------------------------------------------------------------------
        // AC-11: Sealed condition records exist and hold correct field types
        // -------------------------------------------------------------------------

        [Test]
        public void test_PositionCondition_ExistsAndHoldsPositionRequirementField()
        {
            var condition = new PositionCondition(PositionRequirement.BonusIfAhead);
            Assert.AreEqual(PositionRequirement.BonusIfAhead, condition.Required);
            Assert.IsInstanceOf<ICardEffectCondition>(condition);
        }

        [Test]
        public void test_SlotStateCondition_ExistsAndHoldsSlotTypeAndDamageStateFields()
        {
            var condition = new SlotStateCondition(SlotType.Engine, DamageState.Degraded);
            Assert.AreEqual(SlotType.Engine, condition.Slot);
            Assert.AreEqual(DamageState.Degraded, condition.RequiredState);
            Assert.IsInstanceOf<ICardEffectCondition>(condition);
        }

        [Test]
        public void test_StatusCondition_ExistsAndHoldsStatusTypeAndPresentFields()
        {
            var condition = new StatusCondition(StatusType.Burning, true);
            Assert.AreEqual(StatusType.Burning, condition.Status);
            Assert.IsTrue(condition.Present);
            Assert.IsInstanceOf<ICardEffectCondition>(condition);
        }

        // -------------------------------------------------------------------------
        // AC-12: CardSystemDTO shape
        // -------------------------------------------------------------------------

        [Test]
        public void test_CardSystemDTO_SystemId_EqualsCardSystem()
        {
            Assert.AreEqual("card-system", CardSystemDTO.SystemId);
        }

        [Test]
        public void test_CardSystemDTO_SchemaVersion_EqualsOne()
        {
            Assert.AreEqual(1, CardSystemDTO.SchemaVersion);
        }

        [Test]
        public void test_CardSystemDTO_HasExactlyFourDataFields()
        {
            // Only instance properties count as data fields; constants are excluded.
            var dataProps = typeof(CardSystemDTO)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .ToArray();
            Assert.AreEqual(4, dataProps.Length,
                $"CardSystemDTO must have exactly 4 data fields, found {dataProps.Length}: {string.Join(", ", dataProps.Select(p => p.Name))}");

            var names = dataProps.Select(p => p.Name).ToHashSet();
            Assert.IsTrue(names.Contains("Deck"));
            Assert.IsTrue(names.Contains("Discard"));
            Assert.IsTrue(names.Contains("Exhausted"));
            Assert.IsTrue(names.Contains("CardCopyCounts"));
        }
    }

    // -------------------------------------------------------------------------
    // Test fixture — mock ICardData with configurable Effects and EnergyCost
    // -------------------------------------------------------------------------

    internal sealed class MockCardData : ICardData
    {
        public string CardId { get; init; } = "scout_precision_001";
        public string DisplayName { get; init; } = "Test Card";
        public string DescriptionTemplate { get; set; } = "";
        public string FlavorText { get; init; } = "";
        public string CardArtKey { get; init; } = "";
        public ChassisType ChassisPool { get; init; } = ChassisType.Scout;
        public CardFamily Family { get; init; } = CardFamily.Precision;
        public CardRarity Rarity { get; init; } = CardRarity.Common;
        public bool IsStarterCard { get; init; } = false;
        public int EnergyCost { get; set; } = 1;
        public int MerchantPrice { get; init; } = 0;
        public CardTargetType TargetType { get; init; } = CardTargetType.EnemySubsystem;
        public IReadOnlyList<SlotType> ValidSubsystemTargets { get; init; } = Array.Empty<SlotType>();
        public PositionRequirement PositionRequirement { get; init; } = PositionRequirement.None;
        public CardKeyword Keywords { get; init; } = CardKeyword.None;
        public IReadOnlyList<ICardEffect> Effects { get; set; } = Array.Empty<ICardEffect>();
        public int BaseDamage { get; init; } = 0;
        public string? SourceSlotId { get; init; } = null;
    }
}

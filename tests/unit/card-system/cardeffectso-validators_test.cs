using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WastelandRun.Cards;
using WastelandRun.Gameplay.Cards;
using WastelandRun.Vehicle;

// EditMode test — UNITY_EDITOR is defined; all #if UNITY_EDITOR validator code is compiled and callable.
namespace WastelandRun.Tests.Unit.CardSystem
{
    [TestFixture]
    public class CardEffectSoValidatorsTest
    {
        // ── helpers ─────────────────────────────────────────────────────────────

        private static T CreateSO<T>() where T : ScriptableObject =>
            ScriptableObject.CreateInstance<T>();

        private static void SetField(object target, string fieldName, object value) =>
            target.GetType()
                  .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                  .SetValue(target, value);

        private static void CallOnValidate(object target) =>
            target.GetType()
                  .GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance)
                  .Invoke(target, null);

        private static void DestroyAfterTest(Object obj) =>
            Object.DestroyImmediate(obj);

        // ── AC-7: DamageEffectSO.PositionBonus validation ────────────────────

        [Test]
        public void test_DamageEffectSO_PositionBonusNegative_LogsError()
        {
            var so = CreateSO<DamageEffectSO>();
            SetField(so, "_positionBonus", -1);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("PositionBonus"));
            CallOnValidate(so);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_DamageEffectSO_PositionBonusZero_NoError()
        {
            var so = CreateSO<DamageEffectSO>();
            SetField(so, "_positionBonus", 0);
            CallOnValidate(so);
            LogAssert.NoUnexpectedReceived();
            DestroyAfterTest(so);
        }

        [Test]
        public void test_DamageEffectSO_PositionBonusPositive_NoError()
        {
            var so = CreateSO<DamageEffectSO>();
            SetField(so, "_positionBonus", 5);
            CallOnValidate(so);
            LogAssert.NoUnexpectedReceived();
            DestroyAfterTest(so);
        }

        // ── AC-10: RestoreArmorEffectSO.Amount validation ────────────────────

        [Test]
        public void test_RestoreArmorEffectSO_AmountZero_LogsError()
        {
            var so = CreateSO<RestoreArmorEffectSO>();
            SetField(so, "_amount", 0);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Amount must be >= 1"));
            CallOnValidate(so);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_RestoreArmorEffectSO_AmountNegative_LogsError()
        {
            var so = CreateSO<RestoreArmorEffectSO>();
            SetField(so, "_amount", -3);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Amount must be >= 1"));
            CallOnValidate(so);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_RestoreArmorEffectSO_AmountOne_NoError()
        {
            var so = CreateSO<RestoreArmorEffectSO>();
            SetField(so, "_amount", 1);
            CallOnValidate(so);
            LogAssert.NoUnexpectedReceived();
            DestroyAfterTest(so);
        }

        // ── AC-15: ChassisMasteryDefinitionSO weight validation ─────────────

        // BindingFlags.NonPublic only — GetNestedType ignores Instance/Static flags.
        private static System.Type GetMasteryTierType() =>
            typeof(ChassisMasteryDefinitionSO).GetNestedType("MasteryTier", BindingFlags.NonPublic);

        private static object MakeTier(System.Type tierType, int min, int max, int common, int uncommon, int rare)
        {
            var tier = System.Activator.CreateInstance(tierType);
            tierType.GetField("MasteryMin").SetValue(tier, min);
            tierType.GetField("MasteryMax").SetValue(tier, max);
            tierType.GetField("WeightCommon").SetValue(tier, common);
            tierType.GetField("WeightUncommon").SetValue(tier, uncommon);
            tierType.GetField("WeightRare").SetValue(tier, rare);
            return tier;
        }

        [Test]
        public void test_ChassisMastery_WeightsDontSum100_LogsError()
        {
            var so = CreateSO<ChassisMasteryDefinitionSO>();
            var tierType = GetMasteryTierType();
            var tier = MakeTier(tierType, 1, 3, 85, 14, 2); // sums to 101
            var tiersArr = System.Array.CreateInstance(tierType, 1);
            tiersArr.SetValue(tier, 0);
            SetField(so, "_tiers", tiersArr);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("sum to 101"));
            CallOnValidate(so);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_ChassisMastery_NegativeWeight_LogsError()
        {
            var so = CreateSO<ChassisMasteryDefinitionSO>();
            var tierType = GetMasteryTierType();
            var tier = MakeTier(tierType, 1, 3, -10, 110, 0); // sums to 100 but negative
            var tiersArr = System.Array.CreateInstance(tierType, 1);
            tiersArr.SetValue(tier, 0);
            SetField(so, "_tiers", tiersArr);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("negative weight"));
            CallOnValidate(so);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_ChassisMastery_NonContiguousTiers_LogsError()
        {
            var so = CreateSO<ChassisMasteryDefinitionSO>();
            var tierType = GetMasteryTierType();
            var tier0 = MakeTier(tierType, 1, 3, 85, 14, 1);
            var tier1 = MakeTier(tierType, 5, 7, 70, 25, 5); // gap — should start at 4
            var tiersArr = System.Array.CreateInstance(tierType, 2);
            tiersArr.SetValue(tier0, 0);
            tiersArr.SetValue(tier1, 1);
            SetField(so, "_tiers", tiersArr);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("not contiguous"));
            CallOnValidate(so);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_ChassisMastery_ValidWeights_NoError()
        {
            var so = CreateSO<ChassisMasteryDefinitionSO>();
            var tierType = GetMasteryTierType();
            var tier = MakeTier(tierType, 1, 5, 85, 14, 1); // sums to 100
            var tiersArr = System.Array.CreateInstance(tierType, 1);
            tiersArr.SetValue(tier, 0);
            SetField(so, "_tiers", tiersArr);
            CallOnValidate(so);
            LogAssert.NoUnexpectedReceived();
            DestroyAfterTest(so);
        }

        // ── CardDefinitionSO validators ──────────────────────────────────────

        private CardDefinitionSO MakeValidCard()
        {
            var so = CreateSO<CardDefinitionSO>();
            SetField(so, "_cardId", "scout_precision_007");
            SetField(so, "_energyCost", 1);
            SetField(so, "_merchantPrice", 0);
            SetField(so, "_keywords", CardKeyword.None);
            SetField(so, "_family", CardFamily.Precision);
            SetField(so, "_effectSOs", new CardEffectSO[0]);
            SetField(so, "_validSubsystemTargets", new SlotType[0]);
            SetField(so, "_targetType", CardTargetType.NoTarget);
            SetField(so, "_baseDamage", 0);
            return so;
        }

        // ── AC-17: CardId format ─────────────────────────────────────────────

        [Test]
        public void test_CardId_Valid_NoError()
        {
            var so = MakeValidCard();
            SetField(so, "_cardId", "scout_precision_007");
            CallOnValidate(so);
            LogAssert.NoUnexpectedReceived();
            DestroyAfterTest(so);
        }

        [Test]
        public void test_CardId_ZeroPadded000_NoError()
        {
            var so = MakeValidCard();
            SetField(so, "_cardId", "scout_precision_000");
            CallOnValidate(so);
            LogAssert.NoUnexpectedReceived();
            DestroyAfterTest(so);
        }

        [Test]
        public void test_CardId_ZeroPadded999_NoError()
        {
            var so = MakeValidCard();
            SetField(so, "_cardId", "scout_precision_999");
            CallOnValidate(so);
            LogAssert.NoUnexpectedReceived();
            DestroyAfterTest(so);
        }

        [Test]
        public void test_CardId_Uppercase_LogsError()
        {
            var so = MakeValidCard();
            SetField(so, "_cardId", "Scout_precision_007");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("CardId"));
            CallOnValidate(so);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_CardId_NonPaddedDigits_LogsError()
        {
            var so = MakeValidCard();
            SetField(so, "_cardId", "scout_precision_7");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("CardId"));
            CallOnValidate(so);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_CardId_Hyphens_LogsError()
        {
            var so = MakeValidCard();
            SetField(so, "_cardId", "scout-precision-007");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("CardId"));
            CallOnValidate(so);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_CardId_EmptySequence_LogsError()
        {
            var so = MakeValidCard();
            SetField(so, "_cardId", "scout_precision_");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("CardId"));
            CallOnValidate(so);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_CardId_EmptyString_LogsError()
        {
            var so = MakeValidCard();
            SetField(so, "_cardId", "");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("CardId"));
            CallOnValidate(so);
            DestroyAfterTest(so);
        }

        // ── AC-6: EnergyCost < 0 ────────────────────────────────────────────

        [Test]
        public void test_EnergyCostNegative_LogsError()
        {
            var so = MakeValidCard();
            SetField(so, "_energyCost", -1);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("EnergyCost"));
            CallOnValidate(so);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_EnergyCostZero_NoError()
        {
            var so = MakeValidCard();
            SetField(so, "_energyCost", 0);
            CallOnValidate(so);
            LogAssert.NoUnexpectedReceived();
            DestroyAfterTest(so);
        }

        // ── AC-16: MerchantPrice parity ──────────────────────────────────────

        [Test]
        public void test_MerchantPrice30_LogsError()
        {
            var so = MakeValidCard();
            SetField(so, "_merchantPrice", 30);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("MerchantPrice"));
            CallOnValidate(so);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_MerchantPrice0_NoError()
        {
            var so = MakeValidCard();
            SetField(so, "_merchantPrice", 0);
            CallOnValidate(so);
            LogAssert.NoUnexpectedReceived();
            DestroyAfterTest(so);
        }

        [Test]
        public void test_MerchantPrice29_NoError()
        {
            var so = MakeValidCard();
            SetField(so, "_merchantPrice", 29);
            CallOnValidate(so);
            LogAssert.NoUnexpectedReceived();
            DestroyAfterTest(so);
        }

        [Test]
        public void test_MerchantPrice31_NoError()
        {
            var so = MakeValidCard();
            SetField(so, "_merchantPrice", 31);
            CallOnValidate(so);
            LogAssert.NoUnexpectedReceived();
            DestroyAfterTest(so);
        }

        // ── AC-5: Ethereal + Retain mutual exclusion ─────────────────────────

        [Test]
        public void test_EtherealAndRetain_LogsError()
        {
            var so = MakeValidCard();
            SetField(so, "_keywords", CardKeyword.Ethereal | CardKeyword.Retain);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Ethereal.*Retain|Retain.*Ethereal"));
            CallOnValidate(so);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_InnateAndEthereal_NoError()
        {
            var so = MakeValidCard();
            SetField(so, "_keywords", CardKeyword.Innate | CardKeyword.Ethereal);
            CallOnValidate(so);
            LogAssert.NoUnexpectedReceived();
            DestroyAfterTest(so);
        }

        [Test]
        public void test_InnateAndExhaust_NoError()
        {
            var so = MakeValidCard();
            SetField(so, "_keywords", CardKeyword.Innate | CardKeyword.Exhaust);
            CallOnValidate(so);
            LogAssert.NoUnexpectedReceived();
            DestroyAfterTest(so);
        }

        [Test]
        public void test_ExhaustAndRetain_NoError()
        {
            var so = MakeValidCard();
            SetField(so, "_keywords", CardKeyword.Exhaust | CardKeyword.Retain);
            CallOnValidate(so);
            LogAssert.NoUnexpectedReceived();
            DestroyAfterTest(so);
        }

        // ── AC-11: Control-family must-include-damage ─────────────────────────

        [Test]
        public void test_ControlFamily_NoDamageEffect_LogsError()
        {
            var so = MakeValidCard();
            SetField(so, "_family", CardFamily.Control);
            var armorEffect = CreateSO<RestoreArmorEffectSO>();
            SetField(armorEffect, "_amount", 3);
            SetField(so, "_effectSOs", new CardEffectSO[] { armorEffect });
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Control-family"));
            CallOnValidate(so);
            DestroyAfterTest(armorEffect);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_ControlFamily_WithDamageEffectAmountZero_LogsError()
        {
            var so = MakeValidCard();
            SetField(so, "_family", CardFamily.Control);
            var dmg = CreateSO<DamageEffectSO>();
            SetField(dmg, "_amount", 0);
            SetField(so, "_effectSOs", new CardEffectSO[] { dmg });
            // Amount=0 fails Control-family check; BaseDamage=0 also fails BaseDamage >= 1 check.
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Control-family"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("BaseDamage must be >= 1"));
            CallOnValidate(so);
            DestroyAfterTest(dmg);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_ControlFamily_WithValidDamageEffect_NoControlError()
        {
            var so = MakeValidCard();
            SetField(so, "_family", CardFamily.Control);
            var dmg = CreateSO<DamageEffectSO>();
            SetField(dmg, "_amount", 2);
            SetField(so, "_effectSOs", new CardEffectSO[] { dmg });
            SetField(so, "_baseDamage", 2);
            SetField(so, "_targetType", CardTargetType.EnemySubsystem);
            SetField(so, "_validSubsystemTargets", new SlotType[] { SlotType.Weapon });
            CallOnValidate(so);
            LogAssert.NoUnexpectedReceived();
            DestroyAfterTest(dmg);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_RepairFamily_NoDamageEffect_NoControlError()
        {
            var so = MakeValidCard();
            SetField(so, "_family", CardFamily.Repair);
            SetField(so, "_effectSOs", new CardEffectSO[0]);
            CallOnValidate(so);
            LogAssert.NoUnexpectedReceived();
            DestroyAfterTest(so);
        }

        // ── AC-12/13/14: BypassPlating three independent rules ────────────────

        private DamageEffectSO MakeBypassDamage()
        {
            var dmg = CreateSO<DamageEffectSO>();
            SetField(dmg, "_amount", 3);
            SetField(dmg, "_bypassPlating", true);
            return dmg;
        }

        [Test]
        public void test_BypassPlating_FrameInTargets_LogsError()
        {
            var so = MakeValidCard();
            var dmg = MakeBypassDamage();
            SetField(so, "_effectSOs", new CardEffectSO[] { dmg });
            SetField(so, "_baseDamage", 3);
            SetField(so, "_targetType", CardTargetType.EnemySubsystem);
            SetField(so, "_validSubsystemTargets", new SlotType[] { SlotType.Frame });
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Frame"));
            CallOnValidate(so);
            DestroyAfterTest(dmg);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_BypassPlating_AllEnemySubsystems_LogsError()
        {
            var so = MakeValidCard();
            var dmg = MakeBypassDamage();
            SetField(so, "_effectSOs", new CardEffectSO[] { dmg });
            SetField(so, "_baseDamage", 3);
            SetField(so, "_targetType", CardTargetType.AllEnemySubsystems);
            SetField(so, "_validSubsystemTargets", new SlotType[] { SlotType.Weapon });
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("AllEnemySubsystems"));
            CallOnValidate(so);
            DestroyAfterTest(dmg);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_BypassPlating_EmptyTargets_LogsError()
        {
            var so = MakeValidCard();
            var dmg = MakeBypassDamage();
            SetField(so, "_effectSOs", new CardEffectSO[] { dmg });
            SetField(so, "_baseDamage", 3);
            SetField(so, "_targetType", CardTargetType.EnemySubsystem);
            SetField(so, "_validSubsystemTargets", new SlotType[0]); // empty = all slots
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("explicit non-Frame"));
            CallOnValidate(so);
            DestroyAfterTest(dmg);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_BypassPlating_ValidExplicitTargets_NoError()
        {
            var so = MakeValidCard();
            var dmg = MakeBypassDamage();
            SetField(so, "_effectSOs", new CardEffectSO[] { dmg });
            SetField(so, "_baseDamage", 3);
            SetField(so, "_targetType", CardTargetType.EnemySubsystem);
            SetField(so, "_validSubsystemTargets", new SlotType[] { SlotType.Weapon, SlotType.Engine });
            CallOnValidate(so);
            LogAssert.NoUnexpectedReceived();
            DestroyAfterTest(dmg);
            DestroyAfterTest(so);
        }

        // ── AC-8/9: BaseDamage consistency ────────────────────────────────────

        [Test]
        public void test_BaseDamageMismatch_LogsError()
        {
            var so = MakeValidCard();
            var dmg = CreateSO<DamageEffectSO>();
            SetField(dmg, "_amount", 5);
            SetField(so, "_effectSOs", new CardEffectSO[] { dmg });
            SetField(so, "_baseDamage", 4); // mismatch
            SetField(so, "_targetType", CardTargetType.EnemySubsystem);
            SetField(so, "_validSubsystemTargets", new SlotType[] { SlotType.Weapon });
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("does not match"));
            CallOnValidate(so);
            DestroyAfterTest(dmg);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_BaseDamageMatch_NoError()
        {
            var so = MakeValidCard();
            var dmg = CreateSO<DamageEffectSO>();
            SetField(dmg, "_amount", 5);
            SetField(so, "_effectSOs", new CardEffectSO[] { dmg });
            SetField(so, "_baseDamage", 5);
            SetField(so, "_targetType", CardTargetType.EnemySubsystem);
            SetField(so, "_validSubsystemTargets", new SlotType[] { SlotType.Weapon });
            CallOnValidate(so);
            LogAssert.NoUnexpectedReceived();
            DestroyAfterTest(dmg);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_BaseDamageLessThanOne_LogsError()
        {
            var so = MakeValidCard();
            var dmg = CreateSO<DamageEffectSO>();
            SetField(dmg, "_amount", 0);
            SetField(so, "_effectSOs", new CardEffectSO[] { dmg });
            SetField(so, "_baseDamage", 0);
            SetField(so, "_targetType", CardTargetType.EnemySubsystem);
            SetField(so, "_validSubsystemTargets", new SlotType[] { SlotType.Weapon });
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("BaseDamage must be >= 1"));
            CallOnValidate(so);
            DestroyAfterTest(dmg);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_NoDamageEffect_BaseDamageIgnored_NoError()
        {
            var so = MakeValidCard();
            SetField(so, "_effectSOs", new CardEffectSO[0]);
            SetField(so, "_baseDamage", 0);
            CallOnValidate(so);
            LogAssert.NoUnexpectedReceived();
            DestroyAfterTest(so);
        }

        // ── AC-19: EffectConditionSO ToRuntime() projection ──────────────────

        [Test]
        public void test_PositionConditionSO_ToRuntime_ReturnsPositionCondition()
        {
            var so = CreateSO<PositionConditionSO>();
            SetField(so, "_required", PositionRequirement.RequiresAhead);
            var result = so.ToRuntime();
            Assert.IsInstanceOf<PositionCondition>(result);
            Assert.AreEqual(PositionRequirement.RequiresAhead, ((PositionCondition)result).Required);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_SlotStateConditionSO_ToRuntime_ReturnsSlotStateCondition()
        {
            var so = CreateSO<SlotStateConditionSO>();
            SetField(so, "_slot", SlotType.Weapon);
            SetField(so, "_requiredState", DamageState.Degraded);
            var result = so.ToRuntime();
            Assert.IsInstanceOf<SlotStateCondition>(result);
            var condition = (SlotStateCondition)result;
            Assert.AreEqual(SlotType.Weapon, condition.Slot);
            Assert.AreEqual(DamageState.Degraded, condition.RequiredState);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_StatusConditionSO_ToRuntime_ReturnsStatusCondition()
        {
            var so = CreateSO<StatusConditionSO>();
            SetField(so, "_status", StatusType.Burning);
            SetField(so, "_present", true);
            var result = so.ToRuntime();
            Assert.IsInstanceOf<StatusCondition>(result);
            var condition = (StatusCondition)result;
            Assert.AreEqual(StatusType.Burning, condition.Status);
            Assert.IsTrue(condition.Present);
            DestroyAfterTest(so);
        }

        // ── ToRuntime() sanity checks for all 8 effect SOs ───────────────────

        [Test]
        public void test_DamageEffectSO_ToRuntime_ReturnsDamageEffect()
        {
            var so = CreateSO<DamageEffectSO>();
            SetField(so, "_amount", 4);
            SetField(so, "_positionBonus", 2);
            SetField(so, "_bypassPlating", false);
            var result = so.ToRuntime();
            Assert.IsInstanceOf<DamageEffect>(result);
            var effect = (DamageEffect)result;
            Assert.AreEqual(4, effect.Amount);
            Assert.AreEqual(2, effect.PositionBonus);
            Assert.IsFalse(effect.BypassPlating);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_RestorePlatingEffectSO_ToRuntime_ReturnsRestorePlatingEffect()
        {
            var so = CreateSO<RestorePlatingEffectSO>();
            SetField(so, "_stacks", 2);
            SetField(so, "_targetSlot", SlotType.Engine);
            var result = so.ToRuntime();
            Assert.IsInstanceOf<RestorePlatingEffect>(result);
            var effect = (RestorePlatingEffect)result;
            Assert.AreEqual(2, effect.Stacks);
            Assert.AreEqual(SlotType.Engine, effect.TargetSlot);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_RestoreArmorEffectSO_ToRuntime_ReturnsRestoreArmorEffect()
        {
            var so = CreateSO<RestoreArmorEffectSO>();
            SetField(so, "_amount", 3);
            var result = so.ToRuntime();
            Assert.IsInstanceOf<RestoreArmorEffect>(result);
            Assert.AreEqual(3, ((RestoreArmorEffect)result).Amount);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_DrawCardsEffectSO_ToRuntime_NullFilter_ReturnsNullFamilyFilter()
        {
            var so = CreateSO<DrawCardsEffectSO>();
            SetField(so, "_count", 2);
            SetField(so, "_hasFamilyFilter", false);
            var result = so.ToRuntime();
            Assert.IsInstanceOf<DrawCardsEffect>(result);
            var effect = (DrawCardsEffect)result;
            Assert.AreEqual(2, effect.Count);
            Assert.IsNull(effect.FamilyFilter);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_DrawCardsEffectSO_ToRuntime_WithFilter_ReturnsFamilyFilter()
        {
            var so = CreateSO<DrawCardsEffectSO>();
            SetField(so, "_count", 1);
            SetField(so, "_hasFamilyFilter", true);
            SetField(so, "_familyFilter", CardFamily.Repair);
            var result = so.ToRuntime();
            Assert.AreEqual(CardFamily.Repair, ((DrawCardsEffect)result).FamilyFilter);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_GainEnergyEffectSO_ToRuntime_ReturnsGainEnergyEffect()
        {
            var so = CreateSO<GainEnergyEffectSO>();
            SetField(so, "_amount", 2);
            var result = so.ToRuntime();
            Assert.IsInstanceOf<GainEnergyEffect>(result);
            Assert.AreEqual(2, ((GainEnergyEffect)result).Amount);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_ShiftPositionEffectSO_ToRuntime_ReturnsShiftPositionEffect()
        {
            var so = CreateSO<ShiftPositionEffectSO>();
            SetField(so, "_direction", -1);
            var result = so.ToRuntime();
            Assert.IsInstanceOf<ShiftPositionEffect>(result);
            Assert.AreEqual(-1, ((ShiftPositionEffect)result).Direction);
            DestroyAfterTest(so);
        }

        [Test]
        public void test_RepairSubsystemEffectSO_ToRuntime_ReturnsRepairSubsystemEffect()
        {
            var so = CreateSO<RepairSubsystemEffectSO>();
            SetField(so, "_hpRestored", 10);
            SetField(so, "_canReviveOffline", true);
            var result = so.ToRuntime();
            Assert.IsInstanceOf<RepairSubsystemEffect>(result);
            var effect = (RepairSubsystemEffect)result;
            Assert.AreEqual(10, effect.HpRestored);
            Assert.IsTrue(effect.CanReviveOffline);
            DestroyAfterTest(so);
        }

        // ── AC-11 edge case: two DamageEffectSOs, only one Amount >= 1 ─────────

        [Test]
        public void test_ControlFamily_TwoDamageEffects_OnlyOneValid_NoControlError()
        {
            // AC-11: Control-family rule requires at least one DamageEffectSO with Amount >= 1.
            // A second DamageEffectSO with Amount = 0 must not cause a false negative.
            var so = MakeValidCard();
            SetField(so, "_family", CardFamily.Control);
            var dmgValid = CreateSO<DamageEffectSO>();
            SetField(dmgValid, "_amount", 3);
            var dmgInvalid = CreateSO<DamageEffectSO>();
            SetField(dmgInvalid, "_amount", 0);
            SetField(so, "_effectSOs", new CardEffectSO[] { dmgValid, dmgInvalid });
            SetField(so, "_baseDamage", 3);
            SetField(so, "_targetType", CardTargetType.EnemySubsystem);
            SetField(so, "_validSubsystemTargets", new SlotType[] { SlotType.Weapon });
            CallOnValidate(so);
            LogAssert.NoUnexpectedReceived();
            DestroyAfterTest(dmgValid);
            DestroyAfterTest(dmgInvalid);
            DestroyAfterTest(so);
        }

        // ── AC-16 cross-field: MerchantPrice=30 + EnergyCost=0 ───────────────

        [Test]
        public void test_MerchantPrice30_EnergyCost0_LogsMerchantPriceError()
        {
            // AC-16: MerchantPrice == 30 is forbidden regardless of EnergyCost value.
            var so = MakeValidCard();
            SetField(so, "_merchantPrice", 30);
            SetField(so, "_energyCost", 0);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("MerchantPrice"));
            CallOnValidate(so);
            DestroyAfterTest(so);
        }
    }
}

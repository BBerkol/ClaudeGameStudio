using System.Collections.Generic;

namespace WastelandRun.Cards
{
    /// <summary>Deals damage to a target subsystem. BypassPlating skips Plating mitigation on non-Frame slots. Conditions gate amount application.</summary>
    public sealed record DamageEffect(
        int Amount,
        int PositionBonus,
        bool BypassPlating,
        IReadOnlyList<ICardEffectCondition> Conditions) : ICardEffect
    {
        /// <summary>F1 formula: DamageOutput = BaseDamage + (PositionBonus × positionConditionMet).</summary>
        public static int ComputeOutput(int baseDamage, int positionBonus, bool positionConditionMet)
            => baseDamage + (positionConditionMet ? positionBonus : 0);
    }
}

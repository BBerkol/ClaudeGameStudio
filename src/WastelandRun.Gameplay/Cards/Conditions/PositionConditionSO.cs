using UnityEngine;
using WastelandRun.Cards;

namespace WastelandRun.Gameplay.Cards
{
    /// <summary>
    /// Authoring SO for the Position effect condition. Projects to <see cref="PositionCondition"/> —
    /// gates the parent effect on the vehicle's current position relative to the enemy.
    /// </summary>
    [CreateAssetMenu(menuName = "Wasteland/Card/Conditions/Position")]
    public sealed class PositionConditionSO : EffectConditionSO
    {
        [SerializeField] private PositionRequirement _required;

        public override ICardEffectCondition ToRuntime() => new PositionCondition(_required);
    }
}

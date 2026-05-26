using UnityEngine;
using WastelandRun.Cards;
using WastelandRun.Vehicle;

namespace WastelandRun.Gameplay.Cards
{
    /// <summary>
    /// Authoring SO for the SlotState effect condition. Projects to <see cref="SlotStateCondition"/> —
    /// gates the parent effect on a specific slot's <see cref="DamageState"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Wasteland/Card/Conditions/SlotState")]
    public sealed class SlotStateConditionSO : EffectConditionSO
    {
        [SerializeField] private SlotType _slot;
        [SerializeField] private DamageState _requiredState;

        public override ICardEffectCondition ToRuntime() => new SlotStateCondition(_slot, _requiredState);
    }
}

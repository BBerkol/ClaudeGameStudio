using UnityEngine;
using WastelandRun.Cards;
using WastelandRun.Vehicle;

namespace WastelandRun.Gameplay.Cards
{
    /// <summary>
    /// Authoring SO for the Status effect condition. Projects to <see cref="StatusCondition"/> —
    /// gates the parent effect on the presence or absence of a <see cref="StatusType"/> on the target vehicle.
    /// </summary>
    [CreateAssetMenu(menuName = "Wasteland/Card/Conditions/Status")]
    public sealed class StatusConditionSO : EffectConditionSO
    {
        [SerializeField] private StatusType _status;
        [SerializeField] private bool _present;

        public override ICardEffectCondition ToRuntime() => new StatusCondition(_status, _present);
    }
}

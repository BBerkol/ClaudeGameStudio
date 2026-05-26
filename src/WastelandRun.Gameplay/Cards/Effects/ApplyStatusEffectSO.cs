using UnityEngine;
using WastelandRun.Cards;
using WastelandRun.Vehicle;

namespace WastelandRun.Gameplay.Cards
{
    /// <summary>
    /// Authoring SO for the ApplyStatus card effect. Projects to <see cref="ApplyStatusEffect"/> —
    /// applies a status with stacks and duration, optionally targeting a specific slot.
    /// Unity cannot serialize <c>Nullable&lt;SlotType&gt;</c>, so TargetSlot uses a bool-shadow pattern
    /// (_hasTargetSlot + _targetSlot).
    /// </summary>
    [CreateAssetMenu(menuName = "Wasteland/Card/Effects/ApplyStatus")]
    public sealed class ApplyStatusEffectSO : CardEffectSO
    {
        [SerializeField] private StatusType _status;
        [SerializeField] private int _stacks;
        [SerializeField] private int _duration;
        /// <summary>When false, TargetSlot is ignored and the runtime record receives null.</summary>
        [SerializeField] private bool _hasTargetSlot;
        [SerializeField] private SlotType _targetSlot;

        public override ICardEffect ToRuntime() =>
            new ApplyStatusEffect(_status, _stacks, _duration,
                                  _hasTargetSlot ? _targetSlot : (SlotType?)null);
    }
}

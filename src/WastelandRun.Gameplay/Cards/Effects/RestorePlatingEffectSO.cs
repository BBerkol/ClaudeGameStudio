using UnityEngine;
using WastelandRun.Cards;
using WastelandRun.Vehicle;

namespace WastelandRun.Gameplay.Cards
{
    /// <summary>
    /// Authoring SO for the RestorePlating card effect. Projects to <see cref="RestorePlatingEffect"/> —
    /// adds PlatingStacks to a specific subsystem slot.
    /// </summary>
    [CreateAssetMenu(menuName = "Wasteland/Card/Effects/RestorePlating")]
    public sealed class RestorePlatingEffectSO : CardEffectSO
    {
        [SerializeField] private int _stacks;
        [SerializeField] private SlotType _targetSlot;

        public override ICardEffect ToRuntime() => new RestorePlatingEffect(_stacks, _targetSlot);
    }
}

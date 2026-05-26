using UnityEngine;
using WastelandRun.Cards;

namespace WastelandRun.Gameplay.Cards
{
    /// <summary>
    /// Authoring SO for the RepairSubsystem card effect. Projects to <see cref="RepairSubsystemEffect"/> —
    /// restores HP to a subsystem slot, optionally reviving an Offline slot when CanReviveOffline is true.
    /// </summary>
    [CreateAssetMenu(menuName = "Wasteland/Card/Effects/RepairSubsystem")]
    public sealed class RepairSubsystemEffectSO : CardEffectSO
    {
        [SerializeField] private int _hpRestored;
        [SerializeField] private bool _canReviveOffline;

        public override ICardEffect ToRuntime() => new RepairSubsystemEffect(_hpRestored, _canReviveOffline);
    }
}

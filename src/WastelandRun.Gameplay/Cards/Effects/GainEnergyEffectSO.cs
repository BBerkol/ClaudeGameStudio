using UnityEngine;
using WastelandRun.Cards;

namespace WastelandRun.Gameplay.Cards
{
    /// <summary>
    /// Authoring SO for the GainEnergy card effect. Projects to <see cref="GainEnergyEffect"/> —
    /// grants energy during the player's turn.
    /// </summary>
    [CreateAssetMenu(menuName = "Wasteland/Card/Effects/GainEnergy")]
    public sealed class GainEnergyEffectSO : CardEffectSO
    {
        [SerializeField] private int _amount;

        public override ICardEffect ToRuntime() => new GainEnergyEffect(_amount);
    }
}

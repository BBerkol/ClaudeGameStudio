using UnityEngine;
using WastelandRun.Cards;

namespace WastelandRun.Gameplay.Cards
{
    /// <summary>
    /// Authoring SO for the ShiftPosition card effect. Projects to <see cref="ShiftPositionEffect"/> —
    /// changes vehicle position on the combat rail. Direction: +1 moves ahead, -1 moves behind.
    /// </summary>
    [CreateAssetMenu(menuName = "Wasteland/Card/Effects/ShiftPosition")]
    public sealed class ShiftPositionEffectSO : CardEffectSO
    {
        /// <summary>+1 moves ahead, -1 moves behind.</summary>
        [SerializeField] private int _direction;

        public override ICardEffect ToRuntime() => new ShiftPositionEffect(_direction);
    }
}

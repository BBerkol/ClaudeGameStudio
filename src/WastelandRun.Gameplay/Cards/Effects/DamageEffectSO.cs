using UnityEngine;
using WastelandRun.Cards;

namespace WastelandRun.Gameplay.Cards
{
    /// <summary>
    /// Authoring SO for the Damage card effect. Projects to <see cref="DamageEffect"/> — deals damage
    /// to a target subsystem with optional position bonus and Plating bypass (TR-card-005).
    /// OnValidate rejects PositionBonus &lt; 0.
    /// </summary>
    [CreateAssetMenu(menuName = "Wasteland/Card/Effects/Damage")]
    public sealed class DamageEffectSO : CardEffectSO
    {
        [SerializeField] private int _amount;
        [SerializeField] private int _positionBonus;
        [SerializeField] private bool _bypassPlating;
        [SerializeField] private EffectConditionSO[] _conditions;

        /// <summary>Base damage dealt before position bonus. Must be >= 1 on any card using this effect.</summary>
        public int Amount        => _amount;
        /// <summary>Bonus damage applied when the card's PositionRequirement is met. Must be >= 0.</summary>
        public int PositionBonus => _positionBonus;
        /// <summary>When true, bypasses non-Frame Plating in Card Combat F-CC1 step 2. Cannot target Frame slots.</summary>
        public bool BypassPlating => _bypassPlating;

        public override ICardEffect ToRuntime() =>
            new DamageEffect(_amount, _positionBonus, _bypassPlating,
                             EffectConditionProjection.Project(_conditions));

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_positionBonus < 0)
                Debug.LogError($"[{name}] DamageEffectSO.PositionBonus must be >= 0 (got {_positionBonus}).", this);
        }
#endif
    }
}

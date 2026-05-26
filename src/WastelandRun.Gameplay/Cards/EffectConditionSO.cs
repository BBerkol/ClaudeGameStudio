using UnityEngine;
using WastelandRun.Cards;

namespace WastelandRun.Gameplay.Cards
{
    /// <summary>Abstract base for all card effect condition ScriptableObjects. Projects to the engine-free ICardEffectCondition record via ToRuntime().</summary>
    public abstract class EffectConditionSO : ScriptableObject
    {
        public abstract ICardEffectCondition ToRuntime();
    }
}

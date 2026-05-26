using UnityEngine;
using WastelandRun.Cards;

namespace WastelandRun.Gameplay.Cards
{
    /// <summary>Abstract base for all card effect ScriptableObjects. Each sealed subclass serializes effect parameters and projects to the engine-free ICardEffect record via ToRuntime().</summary>
    public abstract class CardEffectSO : ScriptableObject
    {
        public abstract ICardEffect ToRuntime();
    }
}

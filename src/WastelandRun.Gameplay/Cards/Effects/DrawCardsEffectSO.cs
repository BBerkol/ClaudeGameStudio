using UnityEngine;
using WastelandRun.Cards;

namespace WastelandRun.Gameplay.Cards
{
    /// <summary>
    /// Authoring SO for the DrawCards card effect. Projects to <see cref="DrawCardsEffect"/> —
    /// draws cards with an optional family filter. Unity cannot serialize <c>Nullable&lt;CardFamily&gt;</c>,
    /// so FamilyFilter uses a bool-shadow pattern (_hasFamilyFilter + _familyFilter).
    /// </summary>
    [CreateAssetMenu(menuName = "Wasteland/Card/Effects/DrawCards")]
    public sealed class DrawCardsEffectSO : CardEffectSO
    {
        [SerializeField] private int _count;
        /// <summary>When false, FamilyFilter is ignored and the runtime record receives null.</summary>
        [SerializeField] private bool _hasFamilyFilter;
        [SerializeField] private CardFamily _familyFilter;

        public override ICardEffect ToRuntime() =>
            new DrawCardsEffect(_count, _hasFamilyFilter ? _familyFilter : (CardFamily?)null);
    }
}

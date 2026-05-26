// WastelandRun.Gameplay — Combat/CardResolutionContext.cs
// Bundles the state, the card being played, and the target slot a card resolver needs.

using WastelandRun.Cards;
using WastelandRun.Vehicle;

namespace WastelandRun.Gameplay.Combat
{
    /// <summary>
    /// Per-play context passed to <see cref="CardEffectResolver.Resolve"/>.
    /// Engine-free; constructed by <see cref="CombatLoop"/> at play time.
    /// </summary>
    public sealed class CardResolutionContext
    {
        public CombatState State { get; }
        public ICardData   Card  { get; }

        /// <summary>
        /// Concrete slot the card resolves against.
        ///   - EnemySubsystem cards: the slot the player selected.
        ///   - Self / NoTarget cards: a sentinel (Frame) — effects that need a slot use their own override.
        /// </summary>
        public SlotType TargetSlot { get; }

        public CardResolutionContext(CombatState state, ICardData card, SlotType targetSlot)
        {
            State      = state;
            Card       = card;
            TargetSlot = targetSlot;
        }
    }
}

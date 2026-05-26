// WastelandRun.Gameplay — Combat/CardEffectResolver.cs
// Dispatches the 8 ICardEffect record types onto the F-VP2 vehicle pipeline.
// Marker-interface dispatch via switch (per ADR-0006 — "dispatch lives in WastelandRun.Combat").

using WastelandRun.Cards;
using WastelandRun.Vehicle;

namespace WastelandRun.Gameplay.Combat
{
    /// <summary>
    /// Resolves a single <see cref="ICardEffect"/> against a <see cref="CardResolutionContext"/>.
    /// Stateless — instantiate or call directly. Switch dispatch keeps the marker-interface
    /// contract from leaking into <see cref="ICardEffect"/> itself.
    ///
    /// Short-circuit: after every effect, callers must check <see cref="CombatState.IsEnded"/>
    /// and stop processing the card's remaining effects (card-combat-system.md R3 step 6).
    /// </summary>
    public static class CardEffectResolver
    {
        /// <summary>
        /// Applies <paramref name="effect"/> to the context. Returns true if combat is still in
        /// progress after the effect resolves; false if either Frame went Offline (caller should stop).
        /// </summary>
        public static bool Resolve(ICardEffect effect, CardResolutionContext ctx)
        {
            switch (effect)
            {
                case DamageEffect dmg:           ApplyDamage(dmg, ctx); break;
                case ApplyStatusEffect status:   ApplyStatus(status, ctx); break;
                case RestoreArmorEffect armor:   ((IVehicleMutator)ctx.State.Player).AddArmor(armor.Amount); break;
                case RestorePlatingEffect pl:    ((IVehicleMutator)ctx.State.Player).AddPlating(pl.TargetSlot, pl.Stacks); break;
                case RepairSubsystemEffect rep:  ((IVehicleMutator)ctx.State.Player).Repair(ctx.TargetSlot, rep.HpRestored, rep.CanReviveOffline); break;
                case GainEnergyEffect gain:      ctx.State.Energy += gain.Amount; break;
                case DrawCardsEffect draw:       DrawCards(draw, ctx); break;
                case ShiftPositionEffect shift:  ApplyShift(shift, ctx); break;
            }

            UpdateOutcome(ctx.State);
            return !ctx.State.IsEnded;
        }

        // -----------------------------------------------------------------
        // Effect handlers
        // -----------------------------------------------------------------

        private static void ApplyDamage(DamageEffect effect, CardResolutionContext ctx)
        {
            // F1: DamageOutput = BaseDamage + (PositionBonus × positionConditionMet).
            // BaseDamage on the card; PositionBonus and the condition live on the effect.
            bool conditionMet = MatchesPositionBonus(ctx.Card.PositionRequirement, ctx.State.PlayerPosition);
            int amount = DamageEffect.ComputeOutput(ctx.Card.BaseDamage, effect.PositionBonus, conditionMet);
            if (amount <= 0) return;

            ((IVehicleMutator)ctx.State.Enemy).ApplyDamage(ctx.TargetSlot, amount, DamageSource.Card);
            // DamageEffect.Conditions and BypassPlating are deferred until the v2 card resolver — fixture
            // cards in v1 never set them, so leaving them unhandled keeps the dispatcher honest.
        }

        private static void ApplyStatus(ApplyStatusEffect effect, CardResolutionContext ctx)
        {
            // Status effects on cards target the enemy unless the card is self-targeted.
            // For v1 we route by CardTargetType — anything but Self/NoTarget lands on the enemy.
            IVehicleMutator target = ctx.Card.TargetType == CardTargetType.Self
                ? (IVehicleMutator)ctx.State.Player
                : (IVehicleMutator)ctx.State.Enemy;

            SlotType? slot = effect.TargetSlot ?? (ctx.Card.TargetType == CardTargetType.EnemySubsystem
                ? ctx.TargetSlot
                : (SlotType?)null);

            target.ApplyStatus(effect.Status, effect.Duration, effect.Stacks, slot);
        }

        private static void DrawCards(DrawCardsEffect effect, CardResolutionContext ctx)
        {
            // FamilyFilter is ignored for v1 (fixture cards don't use it). EC5: empty deck → silently noop.
            for (int i = 0; i < effect.Count; i++)
            {
                if (ctx.State.Deck.Hand.Count >= ctx.State.Rules.MaxHandSize) break;
                if (ctx.State.Deck.DrawOne(ctx.State.Rng) == null) break;
            }
        }

        private static void ApplyShift(ShiftPositionEffect effect, CardResolutionContext ctx)
        {
            // +1 = move ahead, -1 = move behind. The chase rail has only two slots, so
            // any positive direction snaps to Ahead and any negative to Behind.
            if (effect.Direction > 0)      ctx.State.PlayerPosition = PositionState.Ahead;
            else if (effect.Direction < 0) ctx.State.PlayerPosition = PositionState.Behind;
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        /// <summary>
        /// True when the card's BonusIf* position condition matches the player's current position.
        /// RequiresAhead/RequiresBehind never grant a damage bonus (they gate play, not damage —
        /// CardPlayValidator handles them). None always returns true so the position-bonus stays
        /// off unless explicitly opted into.
        /// </summary>
        private static bool MatchesPositionBonus(PositionRequirement req, PositionState pos) => req switch
        {
            PositionRequirement.BonusIfAhead  => pos == PositionState.Ahead,
            PositionRequirement.BonusIfBehind => pos == PositionState.Behind,
            _ => false
        };

        /// <summary>
        /// Polls both vehicles for Frame-Offline (IsDead) and flips Outcome accordingly.
        /// Idempotent — once set, will not regress.
        /// </summary>
        internal static void UpdateOutcome(CombatState state)
        {
            if (state.IsEnded) return;
            if (state.Enemy.IsDead)  state.Outcome = CombatOutcome.PlayerWon;
            else if (state.Player.IsDead) state.Outcome = CombatOutcome.EnemyWon;
        }
    }
}

// WastelandRun.Gameplay — Combat/CombatLoop.cs
// Engine-free driver of the 5-phase combat turn loop (card-combat-system.md R2).
// Owns CombatState and dispatches card play + enemy intent resolution.

using System;
using System.Collections.Generic;
using WastelandRun.Cards;
using WastelandRun.Enemies;
using WastelandRun.Vehicle;

namespace WastelandRun.Gameplay.Combat
{
    /// <summary>
    /// Drives an encounter from setup through resolution. Stateful — holds a single
    /// <see cref="CombatState"/> for the active encounter.
    ///
    /// Phase order per card-combat-system.md R2:
    ///   Setup → PlayerTurn → PlayerResolve → EnemyTurn → Resolution.
    ///
    /// Determinism: all RNG flows through <c>State.Rng</c>, constructed from the seed
    /// passed to <see cref="Setup"/> (per ADR-0003).
    /// </summary>
    public sealed class CombatLoop
    {
        private readonly CardPlayValidator _validator = new CardPlayValidator();

        public CombatState State { get; private set; }

        // -----------------------------------------------------------------
        // Setup
        // -----------------------------------------------------------------

        /// <summary>
        /// Builds a fresh <see cref="CombatState"/>, shuffles the starter deck, draws
        /// the opening hand, and telegraphs the enemy's intent for turn 1.
        /// </summary>
        public CombatState Setup(
            Vehicle.Vehicle player,
            Vehicle.Vehicle enemy,
            IReadOnlyList<ICardData> starterDeck,
            IEnemyBrain brain,
            int seed,
            CombatRules rules = null,
            PositionState startingPosition = PositionState.Ahead)
        {
            if (player == null)      throw new ArgumentNullException(nameof(player));
            if (enemy == null)       throw new ArgumentNullException(nameof(enemy));
            if (starterDeck == null) throw new ArgumentNullException(nameof(starterDeck));
            if (brain == null)       throw new ArgumentNullException(nameof(brain));

            rules ??= CombatRules.Default;
            var rng  = new Random(seed);
            var deck = new DeckStateManager();

            // Pre-shuffle the starter deck — DeckStateManager.DrawOne pulls index 0,
            // so the shuffle order *is* the draw order until the deck is reshuffled.
            var shuffled = ShuffleCopy(starterDeck, rng);
            deck.Load(shuffled, Array.Empty<ICardData>(), Array.Empty<ICardData>());

            State = new CombatState(player, enemy, deck, brain, rules, rng, startingPosition)
            {
                TurnIndex = 1,
                Energy    = rules.StartingEnergy,
            };

            // Innate cards go to hand first, then we top up to HandSizePerTurn.
            deck.DrawInnateCards();
            DrawToHandSize();

            // Telegraph the intent that will fire on turn 1's EnemyTurn (R4).
            State.NextEnemyIntent = State.Brain.ChooseIntent(BuildBrainContext(), State.Rng);

            // Defensive: starter conditions could already be terminal (Frame=0 chassis).
            CardEffectResolver.UpdateOutcome(State);

            return State;
        }

        // -----------------------------------------------------------------
        // PlayerTurn — play a card
        // -----------------------------------------------------------------

        /// <summary>
        /// Attempts to play <paramref name="card"/> targeting <paramref name="targetSlot"/>.
        /// Returns <see cref="CardPlayResult.Ok"/> on success; one of the failure codes
        /// otherwise. State is only mutated on success.
        ///
        /// Pipeline per card-combat-system.md R3:
        ///   1. Energy gate → 2. Position gate → 3. Target gate → 4. In-hand gate →
        ///   5. Pay energy + move zones → 6. Resolve effects with end-on-IsEnded short-circuit.
        /// </summary>
        public CardPlayResult PlayCard(ICardData card, SlotType targetSlot)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            if (State == null) throw new InvalidOperationException("CombatLoop.PlayCard called before Setup.");
            if (State.IsEnded) return CardPlayResult.Ok; // combat already over — silent no-op

            // 1. Energy
            if (card.EnergyCost > State.Energy) return CardPlayResult.InsufficientEnergy;

            // 2. Position (RequiresAhead / RequiresBehind only — BonusIf* never gate)
            CardPlayResult positionResult = _validator.CheckPositionRequirement(card, State.PlayerPosition);
            if (positionResult != CardPlayResult.Ok) return positionResult;

            // 3. Target validity (EnemySubsystem cards only)
            if (card.TargetType == CardTargetType.EnemySubsystem)
            {
                IReadOnlyList<SlotType> validTargets = _validator.ResolveValidTargets(card, AllSlots);
                if (!Contains(validTargets, targetSlot)) return CardPlayResult.InvalidTarget;
            }

            // 4. In-hand check
            if (!Contains(State.Deck.Hand, card)) return CardPlayResult.NotInHand;

            // 5. Pay energy; move card to discard/exhausted before effects run so DrawCards
            //    can pull from a clean deck and zone bookkeeping is correct mid-effect.
            State.Energy -= card.EnergyCost;
            State.Deck.PlayCard(card);

            // 6. Resolve effects with R3 step-6 short-circuit on IsEnded.
            var ctx = new CardResolutionContext(State, card, targetSlot);
            foreach (ICardEffect effect in card.Effects)
            {
                bool keepGoing = CardEffectResolver.Resolve(effect, ctx);
                if (!keepGoing) break;
            }

            return CardPlayResult.Ok;
        }

        // -----------------------------------------------------------------
        // EndPlayerTurn — runs PlayerResolve → EnemyTurn → Resolution
        // -----------------------------------------------------------------

        /// <summary>
        /// Resolves the remaining three phases (PlayerResolve, EnemyTurn, Resolution),
        /// then advances the loop to the next PlayerTurn (drawing cards, resetting energy,
        /// telegraphing the next intent).
        /// Idempotent once combat has ended.
        /// </summary>
        public void EndPlayerTurn()
        {
            if (State == null) throw new InvalidOperationException("CombatLoop.EndPlayerTurn called before Setup.");
            if (State.IsEnded) return;

            // PlayerResolve — tick statuses on the player (DOT fire-before-tick deferred per ADR-0007).
            ((IVehicleMutator)State.Player).TickStatuses();
            State.Deck.EndTurn();
            CardEffectResolver.UpdateOutcome(State);
            if (State.IsEnded) return;

            // EnemyTurn — resolve the telegraphed intent (if any).
            PickedIntent picked = State.NextEnemyIntent;
            if (picked != null)
            {
                var resolveCtx = new EnemyResolveContext(
                    self:               State.Enemy,
                    target:             State.Player,
                    resolvedTargetSlot: picked.ResolvedTargetSlot,
                    enrageBonus:        picked.EnrageBonus,
                    turnIndex:          State.TurnIndex,
                    enemyPosition:      State.EnemyPosition);
                picked.Intent.Resolve(resolveCtx);
            }
            CardEffectResolver.UpdateOutcome(State);
            if (State.IsEnded) return;

            // Resolution — tick enemy statuses, advance turn, refresh energy & hand,
            // and telegraph the next intent.
            ((IVehicleMutator)State.Enemy).TickStatuses();
            CardEffectResolver.UpdateOutcome(State);
            if (State.IsEnded) return;

            State.TurnIndex++;
            State.Energy = State.Rules.EnergyPerTurn;
            State.Deck.DrawInnateCards();
            DrawToHandSize();

            State.NextEnemyIntent = State.Brain.ChooseIntent(BuildBrainContext(), State.Rng);
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static readonly SlotType[] AllSlots =
        {
            SlotType.Weapon, SlotType.Engine, SlotType.Mobility, SlotType.Frame
        };

        private void DrawToHandSize()
        {
            int target = State.Rules.HandSizePerTurn;
            while (State.Deck.Hand.Count < target)
            {
                if (State.Deck.DrawOne(State.Rng) == null) break;
                if (State.Deck.Hand.Count >= State.Rules.MaxHandSize) break;
            }
        }

        private EnemyBrainContext BuildBrainContext() =>
            new EnemyBrainContext(State.Enemy, State.Player, State.EnemyPosition, State.TurnIndex);

        // Fisher-Yates copy — leaves the caller's deck list untouched.
        private static List<ICardData> ShuffleCopy(IReadOnlyList<ICardData> source, Random rng)
        {
            var copy = new List<ICardData>(source.Count);
            for (int i = 0; i < source.Count; i++) copy.Add(source[i]);
            for (int i = copy.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }
            return copy;
        }

        private static bool Contains<T>(IReadOnlyList<T> list, T value) where T : class
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], value)) return true;
            }
            return false;
        }

        private static bool Contains(IReadOnlyList<SlotType> list, SlotType value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value) return true;
            }
            return false;
        }
    }
}

// WastelandRun.Enemies — EnemyAI.cs
// Core enemy AI framework: position, brain context, intent specs, retarget policy, brain.
// Engine-free; relies only on WastelandRun.Vehicle interfaces.
// Authority: design/gdd/biome-1-enemy-roster.md + enemy-system.md.

using System;
using System.Collections.Generic;
using WastelandRun.Vehicle;

namespace WastelandRun.Enemies
{
    /// <summary>
    /// Combat positional axis on the chase rail.
    /// The enemy is either Ahead of the player or Behind. Position is tracked external
    /// to Vehicle (the chase rail is combat-scope state, not vehicle-scope).
    /// </summary>
    public enum CombatPosition
    {
        Ahead,
        Behind
    }

    /// <summary>
    /// Position gating for an intent.
    ///   None          — intent fires regardless of position.
    ///   RequiresAhead — intent only available when enemy is Ahead of the player.
    ///   RequiresBehind — intent only available when enemy is Behind the player.
    /// Filtered intents drop to weight 0 in the pool (still considered for telegraph display).
    /// </summary>
    public enum PositionRequirement
    {
        None,
        RequiresAhead,
        RequiresBehind
    }

    /// <summary>
    /// Snapshot supplied to weight modifiers and intent resolution. Read-only on both vehicles.
    /// The brain consults this when computing per-turn weights.
    /// </summary>
    public sealed class EnemyBrainContext
    {
        public IVehicleView Self { get; }
        public IVehicleView Target { get; }
        public CombatPosition EnemyPosition { get; }
        public int TurnIndex { get; }

        public EnemyBrainContext(
            IVehicleView self,
            IVehicleView target,
            CombatPosition enemyPosition,
            int turnIndex)
        {
            Self          = self;
            Target        = target;
            EnemyPosition = enemyPosition;
            TurnIndex     = turnIndex;
        }
    }

    /// <summary>
    /// Context passed to <see cref="IEnemyIntent.Resolve"/> when the intent fires.
    /// Carries the post-retarget slot, the current enrage bonus, and the live mutators.
    /// </summary>
    public sealed class EnemyResolveContext
    {
        public IVehicleMutator Self { get; }
        public IVehicleMutator Target { get; }
        public SlotType ResolvedTargetSlot { get; }
        public int EnrageBonus { get; }
        public int TurnIndex { get; }
        public CombatPosition EnemyPosition { get; }

        public EnemyResolveContext(
            IVehicleMutator self,
            IVehicleMutator target,
            SlotType resolvedTargetSlot,
            int enrageBonus,
            int turnIndex,
            CombatPosition enemyPosition)
        {
            Self               = self;
            Target             = target;
            ResolvedTargetSlot = resolvedTargetSlot;
            EnrageBonus        = enrageBonus;
            TurnIndex          = turnIndex;
            EnemyPosition      = enemyPosition;
        }
    }

    /// <summary>
    /// A weight modifier: given context, multiply the intent's base weight by the returned float.
    /// Returning 0 effectively removes the intent from the active pool for this turn.
    /// </summary>
    public delegate float WeightModifier(EnemyBrainContext ctx);

    /// <summary>
    /// Static, immutable definition of one intent slot in a brain's pool.
    /// Holds: base weight, position requirement, modifiers, declared target slot, and a builder
    /// that produces the live <see cref="IEnemyIntent"/> when this spec is chosen.
    /// </summary>
    public sealed class IntentSpec
    {
        public string Name { get; }
        public int BaseWeight { get; }
        public PositionRequirement PositionReq { get; }
        public SlotType DeclaredTargetSlot { get; }
        public IReadOnlyList<WeightModifier> Modifiers { get; }
        public Func<IEnemyIntent> Builder { get; }

        public IntentSpec(
            string name,
            int baseWeight,
            SlotType declaredTargetSlot,
            Func<IEnemyIntent> builder,
            PositionRequirement positionReq = PositionRequirement.None,
            IReadOnlyList<WeightModifier> modifiers = null)
        {
            Name               = name;
            BaseWeight         = baseWeight;
            DeclaredTargetSlot = declaredTargetSlot;
            Builder            = builder;
            PositionReq        = positionReq;
            Modifiers          = modifiers ?? Array.Empty<WeightModifier>();
        }

        /// <summary>
        /// Effective weight after position gating and weight-modifier multiplication.
        /// </summary>
        public float ComputeEffectiveWeight(EnemyBrainContext ctx)
        {
            if (PositionReq == PositionRequirement.RequiresAhead && ctx.EnemyPosition != CombatPosition.Ahead)
                return 0f;
            if (PositionReq == PositionRequirement.RequiresBehind && ctx.EnemyPosition != CombatPosition.Behind)
                return 0f;

            float w = BaseWeight;
            foreach (WeightModifier mod in Modifiers)
                w *= mod(ctx);
            return w;
        }
    }

    /// <summary>
    /// Resolves which slot on the target an intent actually hits when its declared slot is offline.
    ///   FixedSlot   — always hit the configured slot. If offline, hit it anyway (overflow damage is moot).
    ///   PriorityList — try each slot in order; pick the first non-Offline/non-Empty.
    ///                  If all priority slots are offline, fall through to the declared slot.
    /// Per `enemy-system.md` G.4 and the biome-1 stat blocks.
    /// </summary>
    public sealed class RetargetPolicy
    {
        public enum Kind { FixedSlot, PriorityList }

        public Kind PolicyKind { get; }
        public SlotType FixedSlot { get; }
        public IReadOnlyList<SlotType> Priority { get; }

        private RetargetPolicy(Kind kind, SlotType fixedSlot, IReadOnlyList<SlotType> priority)
        {
            PolicyKind = kind;
            FixedSlot  = fixedSlot;
            Priority   = priority ?? Array.Empty<SlotType>();
        }

        public static RetargetPolicy Fixed(SlotType slot) =>
            new RetargetPolicy(Kind.FixedSlot, slot, null);

        public static RetargetPolicy PriorityListOf(params SlotType[] order) =>
            new RetargetPolicy(Kind.PriorityList, default, order);

        /// <summary>
        /// Returns the slot the intent will actually hit on the target.
        /// Default behaviour is to honour the intent's declared slot; the policy overrides
        /// when that slot is offline (or when the policy itself selects a slot).
        /// </summary>
        public SlotType ResolveTarget(IVehicleView target, SlotType declared)
        {
            if (PolicyKind == Kind.FixedSlot)
            {
                return FixedSlot;
            }

            // PriorityList: pick first non-Offline, non-Empty slot in priority order.
            foreach (SlotType slot in Priority)
            {
                DamageState s = target.GetDamageState(slot);
                if (s != DamageState.Offline && s != DamageState.Empty)
                    return slot;
            }
            // All priority slots gone — fall back to declared slot.
            return declared;
        }
    }

    /// <summary>
    /// Enrage configuration. EnrageBonus is added flat to outgoing damage on
    /// the enrage activation turn and grows linearly each turn thereafter.
    ///   EnrageBonus = (TurnIndex &lt; EnrageTurn) ? 0
    ///               : BaseBonus + (TurnIndex - EnrageTurn) * Escalation
    /// Defaults per card-combat-system.md R7: EnrageTurn=8, BaseBonus=+2, Escalation=+1/turn.
    /// </summary>
    public sealed class EnrageConfig
    {
        public int EnrageTurn { get; }
        public int BaseBonus { get; }
        public int Escalation { get; }

        public EnrageConfig(int enrageTurn = 8, int baseBonus = 2, int escalation = 1)
        {
            EnrageTurn = enrageTurn;
            BaseBonus  = baseBonus;
            Escalation = escalation;
        }

        public int ComputeBonus(int turnIndex)
        {
            if (turnIndex < EnrageTurn) return 0;
            int turnsIntoEnrage = turnIndex - EnrageTurn;
            return BaseBonus + turnsIntoEnrage * Escalation;
        }
    }

    /// <summary>
    /// Holds a single intent specs pool. Multi-phase enemies (Dredge) carry one of these
    /// per phase and swap which pool the brain uses when the phase trigger fires.
    /// </summary>
    public sealed class IntentPool
    {
        public IReadOnlyList<IntentSpec> Specs { get; }

        public IntentPool(IReadOnlyList<IntentSpec> specs)
        {
            Specs = specs ?? Array.Empty<IntentSpec>();
        }
    }

    /// <summary>
    /// Picks an intent for the upcoming turn based on context and rolls one via the brain's RNG.
    /// </summary>
    public interface IEnemyBrain
    {
        /// <summary>
        /// Reads context, computes effective weights, rolls the RNG, and returns the chosen
        /// intent + the resolved target slot for telegraph display + the enrage bonus.
        /// Returns null if no intent has positive weight (all gated out).
        /// </summary>
        PickedIntent ChooseIntent(EnemyBrainContext ctx, Random rng);
    }

    /// <summary>
    /// Result of <see cref="IEnemyBrain.ChooseIntent"/>: the live intent, the slot it will hit
    /// on the target after retargeting, and the enrage bonus to apply.
    /// </summary>
    public sealed class PickedIntent
    {
        public IEnemyIntent Intent { get; }
        public IntentSpec SourceSpec { get; }
        public SlotType ResolvedTargetSlot { get; }
        public int EnrageBonus { get; }

        public PickedIntent(IEnemyIntent intent, IntentSpec sourceSpec, SlotType resolvedTargetSlot, int enrageBonus)
        {
            Intent             = intent;
            SourceSpec         = sourceSpec;
            ResolvedTargetSlot = resolvedTargetSlot;
            EnrageBonus        = enrageBonus;
        }
    }

    /// <summary>
    /// Concrete brain. Stateless other than the active pool reference — phase swaps happen by
    /// caller assigning a new <see cref="ActivePool"/> when a phase trigger fires (e.g., Dredge HP ≤ 60%).
    /// </summary>
    public sealed class EnemyBrain : IEnemyBrain
    {
        public IntentPool ActivePool { get; set; }
        public RetargetPolicy Retarget { get; }
        public EnrageConfig Enrage { get; }

        public EnemyBrain(IntentPool initialPool, RetargetPolicy retarget, EnrageConfig enrage = null)
        {
            ActivePool = initialPool ?? throw new ArgumentNullException(nameof(initialPool));
            Retarget   = retarget   ?? throw new ArgumentNullException(nameof(retarget));
            Enrage     = enrage     ?? new EnrageConfig();
        }

        public PickedIntent ChooseIntent(EnemyBrainContext ctx, Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            // Compute effective weights.
            IReadOnlyList<IntentSpec> specs = ActivePool.Specs;
            float[] weights = new float[specs.Count];
            float total = 0f;
            for (int i = 0; i < specs.Count; i++)
            {
                float w = specs[i].ComputeEffectiveWeight(ctx);
                weights[i] = w;
                total     += w;
            }

            if (total <= 0f) return null;

            // CDF roll.
            double roll = rng.NextDouble() * total;
            double acc = 0d;
            IntentSpec chosen = specs[specs.Count - 1]; // fallback to last (covers float drift at the top end)
            for (int i = 0; i < specs.Count; i++)
            {
                acc += weights[i];
                if (roll < acc)
                {
                    chosen = specs[i];
                    break;
                }
            }

            SlotType resolved = Retarget.ResolveTarget(ctx.Target, chosen.DeclaredTargetSlot);
            int bonus = Enrage.ComputeBonus(ctx.TurnIndex);

            return new PickedIntent(chosen.Builder(), chosen, resolved, bonus);
        }
    }

    /// <summary>
    /// Reusable weight-modifier helpers used by the three archetype intent pools.
    /// These mirror the modifier strings in the GDD stat blocks (e.g., "player.Frame.state==Damaged → ×2.0").
    /// </summary>
    public static class WeightModifiers
    {
        /// <summary>×<paramref name="multiplier"/> when the target's given slot is Degraded.</summary>
        public static WeightModifier TargetSlotDegraded(SlotType slot, float multiplier) =>
            ctx => ctx.Target.GetDamageState(slot) == DamageState.Degraded ? multiplier : 1f;

        /// <summary>Returns 0 (intent dropped) when the target's given slot is Offline.</summary>
        public static WeightModifier ZeroIfTargetSlotOffline(SlotType slot) =>
            ctx => ctx.Target.GetDamageState(slot) == DamageState.Offline ? 0f : 1f;

        /// <summary>Returns 0 when ALL target slots are Offline (i.e., the player is already dead).</summary>
        public static WeightModifier ZeroIfAllTargetSlotsOffline() => ctx =>
        {
            bool anyAlive = false;
            foreach (SlotType s in new[] { SlotType.Weapon, SlotType.Engine, SlotType.Mobility, SlotType.Frame })
            {
                DamageState ds = ctx.Target.GetDamageState(s);
                if (ds != DamageState.Offline && ds != DamageState.Empty) { anyAlive = true; break; }
            }
            return anyAlive ? 1f : 0f;
        };

        /// <summary>Returns 0 when the SELF (the enemy itself) has the given slot Offline.</summary>
        public static WeightModifier ZeroIfSelfSlotOffline(SlotType slot) =>
            ctx => ctx.Self.GetDamageState(slot) == DamageState.Offline ? 0f : 1f;

        /// <summary>Returns 0 when the enemy is already Ahead — used by Tailgate (no-op if already ahead).</summary>
        public static WeightModifier ZeroIfEnemyAhead() =>
            ctx => ctx.EnemyPosition == CombatPosition.Ahead ? 0f : 1f;

        /// <summary>Returns 0 when the enemy is already at max armor — used by Reinforce.</summary>
        public static WeightModifier ZeroIfSelfArmorFull() =>
            ctx => ctx.Self.CurrentArmor >= ctx.Self.MaxArmor ? 0f : 1f;
    }
}

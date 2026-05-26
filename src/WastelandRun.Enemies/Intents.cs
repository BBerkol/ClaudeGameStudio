// WastelandRun.Enemies — Intents.cs
// Concrete IEnemyIntent implementations covering every intent in biome-1-enemy-roster.md.
// Each intent is a thin record over the data needed to resolve it against the target.

using System;
using WastelandRun.Vehicle;

namespace WastelandRun.Enemies
{
    /// <summary>
    /// A live intent: knows how to mutate the target (or self) when its turn comes.
    /// Built by an <see cref="IntentSpec.Builder"/> when the brain picks the spec.
    /// </summary>
    public interface IEnemyIntent
    {
        string Name { get; }
        void Resolve(EnemyResolveContext ctx);
    }

    /// <summary>
    /// Damage intent: hit the resolved target slot for (BaseDamage + EnrageBonus + bonusFromCallers).
    /// Routes through IVehicleMutator.ApplyDamage with DamageSource.Card.
    /// Used by: Ram, Scatter Shot, Sweep, Bulldoze, Flank.
    /// </summary>
    public sealed class DamageIntent : IEnemyIntent
    {
        public string Name { get; }
        public int BaseDamage { get; }
        public DamageSource Source { get; }

        public DamageIntent(string name, int baseDamage, DamageSource source = DamageSource.Card)
        {
            Name       = name;
            BaseDamage = baseDamage;
            Source     = source;
        }

        public void Resolve(EnemyResolveContext ctx)
        {
            int dmg = BaseDamage + ctx.EnrageBonus;
            if (dmg <= 0) return;
            ctx.Target.ApplyDamage(ctx.ResolvedTargetSlot, dmg, Source);
        }
    }

    /// <summary>
    /// Apply a status to a specific slot (or vehicle-level).
    /// Used by: Armor Rend (Corroded on player Engine), Taunt (Marked on player Frame).
    /// </summary>
    public sealed class StatusIntent : IEnemyIntent
    {
        public string Name { get; }
        public StatusType Status { get; }
        public int Duration { get; }
        public int Stacks { get; }
        public SlotType? OverrideTargetSlot { get; }

        public StatusIntent(string name, StatusType status, int duration, int stacks, SlotType? overrideTargetSlot = null)
        {
            Name               = name;
            Status             = status;
            Duration           = duration;
            Stacks             = stacks;
            OverrideTargetSlot = overrideTargetSlot;
        }

        public void Resolve(EnemyResolveContext ctx)
        {
            SlotType? slot = OverrideTargetSlot ?? ctx.ResolvedTargetSlot;
            ctx.Target.ApplyStatus(Status, Duration, Stacks, slot);
        }
    }

    /// <summary>
    /// Self-defence: add armor to self. Used by Iron Shepherd's Reinforce.
    /// </summary>
    public sealed class DefendIntent : IEnemyIntent
    {
        public string Name { get; }
        public int ArmorGain { get; }

        public DefendIntent(string name, int armorGain)
        {
            Name      = name;
            ArmorGain = armorGain;
        }

        public void Resolve(EnemyResolveContext ctx)
        {
            ctx.Self.AddArmor(ArmorGain);
        }
    }

    /// <summary>
    /// No-op utility (e.g., Dune Skimmer's Tailgate — pure position shift, not modelled at the
    /// vehicle-state layer). Domain-tests can assert it was chosen but it produces no state delta
    /// on either vehicle. Position shift is owned by the combat-position controller (out of scope).
    /// </summary>
    public sealed class UtilityIntent : IEnemyIntent
    {
        public string Name { get; }

        public UtilityIntent(string name) { Name = name; }

        public void Resolve(EnemyResolveContext ctx) { /* domain no-op */ }
    }

    /// <summary>
    /// Composite intent: resolves multiple child intents in sequence.
    /// Used by: Shred (Damage Weapon + Damage Frame), Javelin Hook (Damage Frame + Apply Stunned).
    /// All child intents see the same EnemyResolveContext — caller is responsible for
    /// crafting children whose declared targets / status overrides line up.
    /// </summary>
    public sealed class CompositeIntent : IEnemyIntent
    {
        public string Name { get; }
        public IEnemyIntent[] Children { get; }

        public CompositeIntent(string name, params IEnemyIntent[] children)
        {
            Name     = name;
            Children = children ?? Array.Empty<IEnemyIntent>();
        }

        public void Resolve(EnemyResolveContext ctx)
        {
            foreach (IEnemyIntent child in Children)
                child.Resolve(ctx);
        }
    }

    /// <summary>
    /// Damage that explicitly targets a fixed slot, ignoring the brain's retarget policy.
    /// Used inside CompositeIntent when one component must hit a slot different from the
    /// resolved target (e.g., Shred deals damage to both Weapon and Frame in the same turn).
    /// </summary>
    public sealed class FixedSlotDamageIntent : IEnemyIntent
    {
        public string Name { get; }
        public SlotType FixedSlot { get; }
        public int BaseDamage { get; }
        public DamageSource Source { get; }
        public bool AddEnrageBonus { get; }

        public FixedSlotDamageIntent(
            string name,
            SlotType fixedSlot,
            int baseDamage,
            DamageSource source = DamageSource.Card,
            bool addEnrageBonus = false)
        {
            Name           = name;
            FixedSlot      = fixedSlot;
            BaseDamage     = baseDamage;
            Source         = source;
            AddEnrageBonus = addEnrageBonus;
        }

        public void Resolve(EnemyResolveContext ctx)
        {
            int dmg = BaseDamage + (AddEnrageBonus ? ctx.EnrageBonus : 0);
            if (dmg <= 0) return;
            ctx.Target.ApplyDamage(FixedSlot, dmg, Source);
        }
    }
}

// WastelandRun.Gameplay — Combat/CombatRules.cs
// Tuning constants for the engine-free combat turn loop.
// All values traceable to design/gdd/card-combat-system.md §R9 (Defaults).

namespace WastelandRun.Gameplay.Combat
{
    /// <summary>
    /// Combat-loop tuning defaults per card-combat-system.md R9.
    /// Immutable record so callers can spin up alternates for tuning passes / scripted
    /// encounters without mutating shared state.
    /// </summary>
    public sealed record CombatRules(
        int HandSizePerTurn   = 4,
        int StartingEnergy    = 3,
        int EnergyPerTurn     = 3,
        int MaxHandSize       = 10)
    {
        /// <summary>GDD R9 defaults — Hand 4, Energy 3, MaxHand 10.</summary>
        public static readonly CombatRules Default = new CombatRules();
    }
}

// WastelandRun.Gameplay — Combat/CombatOutcome.cs
// Terminal result of a combat encounter. Inspected by the run loop / UI after each turn tick.

namespace WastelandRun.Gameplay.Combat
{
    /// <summary>
    /// Outcome of a combat encounter as observed by the run-layer caller.
    /// Set by <see cref="CombatLoop"/> after damage resolution any time
    /// either vehicle's <see cref="WastelandRun.Vehicle.IVehicleView.IsDead"/> flips.
    /// </summary>
    public enum CombatOutcome
    {
        /// <summary>Both vehicles alive; combat continues.</summary>
        InProgress,

        /// <summary>Enemy vehicle's Frame dropped to Offline — encounter resolved as a player victory.</summary>
        PlayerWon,

        /// <summary>Player vehicle's Frame dropped to Offline — encounter resolved as a loss.</summary>
        EnemyWon
    }
}

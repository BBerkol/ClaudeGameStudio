// WastelandRun.Vehicle — StatModifier.cs
// Immutable value type representing a single stat modifier contribution from a part.
// Authority: ADR-0005 §Key Interfaces / StatModifier

namespace WastelandRun.Vehicle
{
    /// <summary>
    /// A single stat modifier contributed by an installed part.
    /// Composed by <see cref="StatComposer.Compose"/> following the pipeline:
    ///   result = (base + ΣAdd) × ΠMultiply
    ///   Override short-circuits and returns its value directly (only one Override per stat allowed).
    ///
    /// This is a readonly record struct — zero-allocation, value-equality, stack-allocated.
    /// ADR-0005 §Key Interfaces.
    ///
    /// Usage example:
    ///   var mod = new StatModifier(StatType.CardBaseDamageOut, StatOperation.Multiply, 1.25f);
    ///   float composed = StatComposer.Compose(baseDamage, new[] { mod });
    /// </summary>
    public readonly record struct StatModifier(
        StatType TargetStat,
        StatOperation Operation,
        float Value);
}

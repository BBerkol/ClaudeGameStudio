// WastelandRun.Vehicle — StatComposer.cs
// Pure, static stat composition function.
// Hot-path safe: no allocation, no state, deterministic.
// Authority: ADR-0005 R5 / §Key Interfaces / StatComposer

using System.Collections.Generic;

namespace WastelandRun.Vehicle
{
    /// <summary>
    /// Stateless, allocation-free stat modifier composition.
    ///
    /// Composition pipeline (ADR-0005 R5 / V&amp;P GDD R8):
    ///   1. Collect all Add modifiers: add = Σ mod.Value where mod.Operation == Add
    ///   2. Collect all Multiply modifiers: mult = Π mod.Value where mod.Operation == Multiply
    ///   3. If any Override modifier exists: return its Value (only one Override per stat allowed).
    ///   4. Otherwise: return (baseValue + add) × mult
    ///
    /// Multiple Override modifiers on the same stat throw <see cref="MultipleOverrideException"/>.
    /// This is an editor-time authoring error; the EditorBuildPreprocessor gate catches it
    /// before it reaches players. The runtime check is defense-in-depth.
    ///
    /// Performance: O(n) where n = number of modifiers. No allocation.
    /// Budget: &lt; 0.02 ms for 4 parts × 4 modifiers each = 16 ops. ADR-0005 R12.
    ///
    /// Unit test golden value (ADR-0005 §Validation Criteria / AC-VP28):
    ///   StatComposer.Compose(3f, [+1 Add, ×1.25 Multiply]) == 5.0f
    ///
    /// Usage example:
    ///   float energy = StatComposer.Compose(chassis.MaxEnergyBase, allModifiers
    ///       .Where(m => m.TargetStat == StatType.MaxEnergyBase));
    /// </summary>
    public static class StatComposer
    {
        /// <summary>
        /// Composes a base value with a sequence of stat modifiers.
        /// </summary>
        /// <param name="baseValue">The consumer-defined base value (e.g., chassis.MaxEnergyBase).</param>
        /// <param name="modifiers">All modifiers to apply, from all installed parts.</param>
        /// <returns>
        /// The Override value if exactly one Override modifier is present;
        /// otherwise (baseValue + ΣAdd) × ΠMultiply.
        /// Returns <paramref name="baseValue"/> unchanged if <paramref name="modifiers"/> is empty.
        /// </returns>
        /// <exception cref="MultipleOverrideException">
        /// Thrown when more than one Override modifier targets the same stat.
        /// This is an authoring error — the EditorBuildPreprocessor gate catches it first.
        /// </exception>
        public static float Compose(float baseValue, IEnumerable<StatModifier> modifiers)
        {
            float add         = 0f;
            float mult        = 1f;
            float overrideVal = 0f;
            int   overrideCount = 0;
            StatType overrideStat = default;

            foreach (StatModifier mod in modifiers)
            {
                switch (mod.Operation)
                {
                    case StatOperation.Add:
                        add += mod.Value;
                        break;

                    case StatOperation.Multiply:
                        mult *= mod.Value;
                        break;

                    case StatOperation.Override:
                        overrideCount++;
                        overrideVal  = mod.Value;
                        overrideStat = mod.TargetStat;
                        if (overrideCount > 1)
                        {
                            throw new MultipleOverrideException(overrideStat);
                        }
                        break;
                }
            }

            return overrideCount == 1
                ? overrideVal
                : (baseValue + add) * mult;
        }
    }
}

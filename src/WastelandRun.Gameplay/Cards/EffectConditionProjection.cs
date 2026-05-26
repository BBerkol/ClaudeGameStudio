using System;
using System.Collections.Generic;
using WastelandRun.Cards;

namespace WastelandRun.Gameplay.Cards
{
    /// <summary>
    /// Projects an array of EffectConditionSO assets to their runtime ICardEffectCondition records.
    /// Preserved in link.xml to prevent IL2CPP managed-stripping — this static helper is called
    /// by every *EffectSO.ToRuntime() that holds gating conditions.
    /// </summary>
    internal static class EffectConditionProjection
    {
        internal static IReadOnlyList<ICardEffectCondition> Project(EffectConditionSO[] sources)
        {
            if (sources == null || sources.Length == 0)
                return Array.Empty<ICardEffectCondition>();

            // Null entries are a designer error caught by OnValidate on the parent CardDefinitionSO;
            // filter here to prevent NullReferenceException in combat resolution.
            var result = new List<ICardEffectCondition>(sources.Length);
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null)
                    result.Add(sources[i].ToRuntime());
            }
            return result;
        }
    }
}

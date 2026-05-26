using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WastelandRun.Cards
{
    /// <summary>
    /// Resolves {token} placeholders in card DescriptionTemplate against live ICardData field values.
    /// Stateless — safe to share across all five display contexts (combat hand, reward, Merchant, Chopshop, deck view).
    /// Unknown or out-of-bounds tokens resolve to "?" without throwing.
    /// </summary>
    public sealed class TokenResolver
    {
        // Matches {token} and {token.N} patterns
        private static readonly Regex _tokenPattern = new Regex(
            @"\{(?<name>[a-z]+)(?:\.(?<index>\d+))?\}",
            RegexOptions.Compiled);

        /// <summary>Returns card.DescriptionTemplate with all {token} placeholders replaced by their resolved values.</summary>
        public string Resolve(ICardData card)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));

            return _tokenPattern.Replace(card.DescriptionTemplate, match =>
            {
                var name = match.Groups["name"].Value;
                var indexGroup = match.Groups["index"];
                bool hasIndex = indexGroup.Success;
                // TryParse guards against overflow on malformed templates like {damage.999999999999}.
                // Unindexed {damage} defaults to 1 — same as {damage.1} per GDD token spec.
                int index = (hasIndex && int.TryParse(indexGroup.Value, out int parsedIndex)) ? parsedIndex : 1;

                return name switch
                {
                    "damage"   => ResolveIndexed<DamageEffect>(card.Effects, index, e => e.Amount),
                    "bonus"    => ResolveFirst<DamageEffect>(card.Effects, e => e.PositionBonus),
                    "heal"     => ResolveFirst<RepairSubsystemEffect>(card.Effects, e => e.HpRestored),
                    "plating"  => ResolveFirst<RestorePlatingEffect>(card.Effects, e => e.Stacks),
                    "armor"    => ResolveFirst<RestoreArmorEffect>(card.Effects, e => e.Amount),
                    "draws"    => ResolveFirst<DrawCardsEffect>(card.Effects, e => e.Count),
                    "energy"   => ResolveFirst<GainEnergyEffect>(card.Effects, e => e.Amount),
                    "stacks"   => ResolveFirst<ApplyStatusEffect>(card.Effects, e => e.Stacks),
                    "duration" => ResolveFirst<ApplyStatusEffect>(card.Effects, e => e.Duration),
                    "cost"     => card.EnergyCost.ToString(),
                    _          => "?"
                };
            });
        }

        private static string ResolveFirst<T>(IReadOnlyList<ICardEffect> effects, Func<T, int> selector)
            where T : ICardEffect
        {
            foreach (var effect in effects)
            {
                if (effect is T typed) return selector(typed).ToString();
            }
            return "?";
        }

        // 1-indexed: {damage.1} = first DamageEffect, {damage.2} = second, etc.
        private static string ResolveIndexed<T>(IReadOnlyList<ICardEffect> effects, int oneBasedIndex, Func<T, int> selector)
            where T : ICardEffect
        {
            if (oneBasedIndex < 1) return "?";
            int count = 0;
            foreach (var effect in effects)
            {
                if (effect is T typed)
                {
                    count++;
                    if (count == oneBasedIndex) return selector(typed).ToString();
                }
            }
            return "?";
        }
    }
}

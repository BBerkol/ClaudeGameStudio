using System.Collections.Generic;
using WastelandRun.Vehicle;

namespace WastelandRun.Cards
{
    /// <summary>Generates a deterministic card reward offer. Caller constructs and owns the System.Random instance per ADR-0003 — never pass a seed integer.</summary>
    public interface ICardRewardGenerator
    {
        /// <summary>
        /// Returns exactly drawCount drafts, or an empty array when the pity-empty case fires
        /// (rarePityCounter >= 8 AND Rare pool is empty — Loot &amp; Reward awards Scrap compensation).
        /// Never returns a partial array.
        /// </summary>
        CardDraft[] Generate(
            ChassisType chassis,
            int mastery,
            int rarePityCounter,
            int drawCount,
            IReadOnlyList<string> currentDeckCardIds,
            System.Random rng);
    }
}

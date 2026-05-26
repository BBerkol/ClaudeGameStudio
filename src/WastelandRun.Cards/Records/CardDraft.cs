using System.Collections.Generic;

namespace WastelandRun.Cards
{
    /// <summary>Immutable card offer presented to the player at a reward node. RulesText is pre-resolved by TokenResolver — the UI renders it verbatim.</summary>
    public sealed record CardDraft
    {
        public string CardId { get; init; }
        public string DisplayName { get; init; }
        public string RulesText { get; init; }
        public CardFamily Family { get; init; }
        public CardRarity Rarity { get; init; }
        public int EnergyCost { get; init; }
        public string CardArtKey { get; init; }
        public IReadOnlyList<string> KeywordBadges { get; init; }
        /// <summary>Null at reward screens; populated when displaying cards in the Merchant.</summary>
        public int? MerchantPrice { get; init; }
        /// <summary>Deterministic hash of CardId + chassis + mastery + slotIndex for telemetry deduplication.</summary>
        public int SelectionHash { get; init; }
    }
}

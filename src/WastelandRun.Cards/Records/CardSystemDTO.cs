using System.Collections.Generic;
using Newtonsoft.Json;

namespace WastelandRun.Cards
{
    /// <summary>
    /// Serializable snapshot of the player's card system state for save/load (ADR-0004).
    /// RarePityCounter is NOT stored here — Loot &amp; Reward owns it in LootStateDTO.
    /// </summary>
    public sealed record CardSystemDTO
    {
        public const string SystemId      = "card-system";
        public const int    SchemaVersion = 1;

        /// <summary>
        /// Explicit parameterless constructor required for Newtonsoft.Json deserialization of
        /// init-only properties. Guards against positional-record refactor removing the
        /// synthesized parameterless constructor, which would cause silent null-field deserialization
        /// under IL2CPP with link.xml stripping.
        /// </summary>
        [JsonConstructor]
        public CardSystemDTO() { }

        public List<string>            Deck           { get; init; }
        public List<string>            Discard        { get; init; }
        public List<string>            Exhausted      { get; init; }
        /// <summary>CardId → total count across all zones. Derived but persisted for O(1) offer-filter checks. Auto-corrected from zone lists on load if mismatch detected.</summary>
        public Dictionary<string, int> CardCopyCounts { get; init; }
    }
}

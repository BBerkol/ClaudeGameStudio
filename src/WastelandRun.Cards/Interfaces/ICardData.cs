using System.Collections.Generic;
using WastelandRun.Vehicle;

namespace WastelandRun.Cards
{
    /// <summary>Runtime read-only view of a card. Implemented by CardDefinitionSO projection in WastelandRun.Gameplay.</summary>
    public interface ICardData
    {
        string CardId { get; }
        string DisplayName { get; }
        string DescriptionTemplate { get; }
        string FlavorText { get; }
        string CardArtKey { get; }
        ChassisType ChassisPool { get; }
        CardFamily Family { get; }
        CardRarity Rarity { get; }
        bool IsStarterCard { get; }
        int EnergyCost { get; }
        int MerchantPrice { get; }
        CardTargetType TargetType { get; }
        IReadOnlyList<SlotType> ValidSubsystemTargets { get; }
        PositionRequirement PositionRequirement { get; }
        CardKeyword Keywords { get; }
        IReadOnlyList<ICardEffect> Effects { get; }
        int BaseDamage { get; }
        /// <summary>Non-null for part-derived granted cards; null for starter-deck and reward-granted cards. Runtime-stamped — not serialized in CardSystemDTO.</summary>
        string? SourceSlotId { get; }
    }
}

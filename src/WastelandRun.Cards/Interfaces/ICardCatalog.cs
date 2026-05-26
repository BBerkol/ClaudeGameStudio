using System.Collections.Generic;
using WastelandRun.Vehicle;

namespace WastelandRun.Cards
{
    /// <summary>Runtime lookup for card data by identity or chassis. Implemented by AddressablesCardCatalog in WastelandRun.Gameplay.</summary>
    public interface ICardCatalog
    {
        /// <summary>Returns the card definition for the given ID.</summary>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">
        /// Thrown when no card with <paramref name="cardId"/> exists in the catalog.
        /// Callers that tolerate missing IDs must catch this exception.
        /// </exception>
        ICardData GetById(string cardId);
        IReadOnlyList<ICardData> GetByChassis(ChassisType chassis);
        IReadOnlyList<ICardData> GetByChassisAndRarity(ChassisType chassis, CardRarity rarity);
    }
}

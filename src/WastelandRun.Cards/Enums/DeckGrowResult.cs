namespace WastelandRun.Cards
{
    /// <summary>
    /// Result of a deck grow or purge operation. Returned rather than thrown so the
    /// UI layer can read the outcome and display appropriate inline feedback.
    /// ADR-0006 §"Deck Composition Rules".
    /// </summary>
    public enum DeckGrowResult
    {
        /// <summary>The card was added to (or removed from) the deck successfully.</summary>
        Success,

        /// <summary>Purge rejected: deck is already at the minimum allowed size (10).</summary>
        MinimumSizeReached,

        /// <summary>Grow rejected: deck is already at the maximum allowed size (60).</summary>
        CapacityExceeded
    }
}

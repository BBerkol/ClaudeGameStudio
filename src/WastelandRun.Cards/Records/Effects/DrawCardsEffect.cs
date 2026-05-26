namespace WastelandRun.Cards
{
    /// <summary>Draws cards from the deck. FamilyFilter null means draw any family. An empty filtered deck silently produces nothing (EC5).</summary>
    public sealed record DrawCardsEffect(int Count, CardFamily? FamilyFilter) : ICardEffect;
}

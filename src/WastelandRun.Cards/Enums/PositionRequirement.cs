namespace WastelandRun.Cards
{
    /// <summary>Chase-rail position constraint or bonus condition for a card or effect.</summary>
    public enum PositionRequirement
    {
        None,
        RequiresAhead,
        RequiresBehind,
        BonusIfAhead,
        BonusIfBehind
    }
}

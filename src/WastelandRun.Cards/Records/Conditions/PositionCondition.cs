namespace WastelandRun.Cards
{
    public sealed record PositionCondition(PositionRequirement Required) : ICardEffectCondition;
}

using WastelandRun.Vehicle;

namespace WastelandRun.Cards
{
    public sealed record StatusCondition(StatusType Status, bool Present) : ICardEffectCondition;
}

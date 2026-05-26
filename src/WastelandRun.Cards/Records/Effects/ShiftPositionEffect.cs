namespace WastelandRun.Cards
{
    /// <summary>Shifts the vehicle's chase-rail position. +1 = move ahead, -1 = move behind.</summary>
    public sealed record ShiftPositionEffect(int Direction) : ICardEffect;
}

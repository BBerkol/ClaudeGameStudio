namespace WastelandRun.Cards
{
    public sealed record RepairSubsystemEffect(int HpRestored, bool CanReviveOffline) : ICardEffect;
}

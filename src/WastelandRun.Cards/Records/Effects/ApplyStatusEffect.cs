using WastelandRun.Vehicle;

namespace WastelandRun.Cards
{
    /// <summary>Applies a status effect to a target slot or the vehicle. Null TargetSlot means vehicle-level application.</summary>
    public sealed record ApplyStatusEffect(StatusType Status, int Stacks, int Duration, SlotType? TargetSlot) : ICardEffect;
}

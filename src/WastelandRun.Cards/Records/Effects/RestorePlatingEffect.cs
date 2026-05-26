using WastelandRun.Vehicle;

namespace WastelandRun.Cards
{
    public sealed record RestorePlatingEffect(int Stacks, SlotType TargetSlot) : ICardEffect;
}

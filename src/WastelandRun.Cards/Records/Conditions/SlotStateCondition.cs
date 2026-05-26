using WastelandRun.Vehicle;

namespace WastelandRun.Cards
{
    public sealed record SlotStateCondition(SlotType Slot, DamageState RequiredState) : ICardEffectCondition;
}

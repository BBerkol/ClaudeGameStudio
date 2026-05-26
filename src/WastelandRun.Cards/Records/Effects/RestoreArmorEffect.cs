namespace WastelandRun.Cards
{
    /// <summary>Restores vehicle-level Armor via IVehicleMutator.AddArmor. Self-target only; capped at MaxArmor.</summary>
    public sealed record RestoreArmorEffect(int Amount) : ICardEffect;
}

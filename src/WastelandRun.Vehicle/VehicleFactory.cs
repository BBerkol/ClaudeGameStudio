// WastelandRun.Vehicle — VehicleFactory.cs
// Single entry-point for constructing a Vehicle from an IChassisData.
// Vehicle's ctor is internal — external callers (Gameplay, Enemies, tests) come through here.
// Authority: ADR-0005 §Key Interfaces (factory pattern referenced in Vehicle.cs).

using System.Collections.Generic;

namespace WastelandRun.Vehicle
{
    /// <summary>
    /// Builds a fully-initialised <see cref="Vehicle"/> from chassis data.
    ///
    /// Sequence:
    ///   1. Allocate one SlotState per SlotType using the chassis DegradedThreshold.
    ///   2. Construct the Vehicle with the per-slot MaxHp map from the chassis.
    ///   3. Install each starter part — this sets PartId / MaxHp / Hp and caches stat modifiers + armor.
    ///
    /// Both player and enemy chassis use this path. Determinism: no RNG required here
    /// (deck shuffling lives in the card system, not in Vehicle construction).
    /// </summary>
    public static class VehicleFactory
    {
        public static Vehicle FromChassis(IChassisData chassis)
        {
            var slots = new Dictionary<SlotType, SlotState>(4);
            foreach (SlotType slotType in SlotTypes.All)
            {
                slots[slotType] = new SlotState(slotType, chassis.DegradedThresholdPct);
            }

            var chassisMaxHpMap = new Dictionary<SlotType, int>(chassis.SlotMaxHp);
            var vehicle = new Vehicle(chassis.Chassis, slots, chassisMaxHpMap);

            // Install starter parts. Vehicle.InstallPart populates the SlotState caches.
            foreach (SlotType slotType in SlotTypes.All)
            {
                if (chassis.StarterParts.TryGetValue(slotType, out IPartData part) && part != null)
                {
                    vehicle.InstallPart(slotType, part);
                }
            }

            // Starter armor — fill CurrentArmor to MaxArmor at spawn. Combat rules elsewhere
            // dictate per-combat reset (V&P GDD line 34); the factory just sets the spawn baseline.
            vehicle.AddArmor(vehicle.MaxArmor);

            return vehicle;
        }
    }
}

// WastelandRun.Vehicle — VehicleStatePreview.cs
// Pure read projection returned by IPartCatalog.PreviewInstall.
// Used by the compare-panel UI to display stat deltas without mutating vehicle state.
// Authority: ADR-0005 R10 / §Key Interfaces / IPartCatalog

using System.Collections.Generic;

namespace WastelandRun.Vehicle
{
    /// <summary>
    /// Immutable snapshot of projected vehicle stats after a hypothetical part install.
    /// Produced by <see cref="IPartCatalog.PreviewInstall"/> — pure read, no mutation.
    ///
    /// Usage example:
    ///   VehicleStatePreview preview = catalog.PreviewInstall(view, SlotType.Engine, candidatePart);
    ///   int deltaArmor = preview.ProjectedMaxArmor - view.MaxArmor;
    ///   // Display deltaArmor in the compare panel UI.
    /// </summary>
    public sealed class VehicleStatePreview
    {
        /// <summary>Slot the candidate part would be installed into.</summary>
        public SlotType TargetSlot { get; }

        /// <summary>The part data that was evaluated.</summary>
        public IPartData Candidate { get; }

        /// <summary>Existing part in the slot that would be auto-scrapped, or null if slot was empty.</summary>
        public IPartData Displaced { get; }

        /// <summary>Projected MaxArmor after install (Σ ArmorContribution across resulting installed parts).</summary>
        public int ProjectedMaxArmor { get; }

        /// <summary>
        /// Projected composed stat values after install.
        /// Key is <see cref="StatType"/>; value is the result of
        /// <see cref="StatComposer.Compose"/> using the chassis base + all part modifiers
        /// (existing parts minus displaced, plus candidate).
        /// </summary>
        public IReadOnlyDictionary<StatType, float> ProjectedStats { get; }

        /// <summary>
        /// Card IDs that would be added to the deck pile (from the candidate part).
        /// </summary>
        public IReadOnlyList<string> CardsAdded { get; }

        /// <summary>
        /// Card IDs that would be removed from all deck zones (from the displaced part, if any).
        /// </summary>
        public IReadOnlyList<string> CardsRemoved { get; }

        internal VehicleStatePreview(
            SlotType targetSlot,
            IPartData candidate,
            IPartData displaced,
            int projectedMaxArmor,
            IReadOnlyDictionary<StatType, float> projectedStats,
            IReadOnlyList<string> cardsAdded,
            IReadOnlyList<string> cardsRemoved)
        {
            TargetSlot         = targetSlot;
            Candidate          = candidate;
            Displaced          = displaced;
            ProjectedMaxArmor  = projectedMaxArmor;
            ProjectedStats     = projectedStats;
            CardsAdded         = cardsAdded;
            CardsRemoved       = cardsRemoved;
        }
    }
}

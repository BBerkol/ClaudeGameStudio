using System.Collections.Generic;
using WastelandRun.Vehicle;

namespace WastelandRun.Cards
{
    /// <summary>Per-chassis rarity weights by mastery tier. Implemented by ChassisMasteryDefinitionSO in WastelandRun.Gameplay.</summary>
    public interface IMasteryWeights
    {
        /// <summary>Rarity weights for a chassis at the given mastery level. Values sum to 100.</summary>
        (int Common, int Uncommon, int Rare) GetWeights(ChassisType chassis, int mastery);

        /// <summary>Primary card families for slot-1 bias at Mastery 1–3.</summary>
        IReadOnlyList<CardFamily> GetPrimaryFamilies(ChassisType chassis);
    }
}

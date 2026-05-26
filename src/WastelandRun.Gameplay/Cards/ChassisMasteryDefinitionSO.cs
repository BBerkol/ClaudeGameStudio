using System;
using System.Collections.Generic;
using UnityEngine;
using WastelandRun.Cards;
using WastelandRun.Vehicle;

namespace WastelandRun.Gameplay.Cards
{
    /// <summary>
    /// Stores per-chassis rarity weights and primary-family bias for all mastery tiers (TR-card-012).
    /// Implements IMasteryWeights so RewardDrawAlgorithm can consume it without a Unity reference.
    /// OnValidate enforces: weights sum to 100; no negative weights; tier ranges are contiguous.
    /// </summary>
    [CreateAssetMenu(menuName = "Wasteland/Card/ChassisMastery")]
    public sealed class ChassisMasteryDefinitionSO : ScriptableObject, IMasteryWeights
    {
        [Serializable]
        private struct MasteryTier
        {
            public int MasteryMin;
            public int MasteryMax;
            public int WeightCommon;
            public int WeightUncommon;
            public int WeightRare;
        }

        [SerializeField] private ChassisType _chassis;
        [SerializeField] private CardFamily[] _primaryFamilies;
        [SerializeField] private MasteryTier[] _tiers;

        public (int Common, int Uncommon, int Rare) GetWeights(ChassisType chassis, int mastery)
        {
            if (_tiers == null) return (85, 14, 1);
            foreach (var tier in _tiers)
                if (mastery >= tier.MasteryMin && mastery <= tier.MasteryMax)
                    return (tier.WeightCommon, tier.WeightUncommon, tier.WeightRare);
            return (85, 14, 1);
        }

        public IReadOnlyList<CardFamily> GetPrimaryFamilies(ChassisType chassis) =>
            _primaryFamilies ?? Array.Empty<CardFamily>();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_tiers == null || _tiers.Length == 0) return;

            for (int i = 0; i < _tiers.Length; i++)
            {
                var tier = _tiers[i];
                int sum = tier.WeightCommon + tier.WeightUncommon + tier.WeightRare;

                if (sum != 100)
                    Debug.LogError(
                        $"[{name}] Tier {i} (Mastery {tier.MasteryMin}-{tier.MasteryMax}) " +
                        $"weights sum to {sum}, must be 100 " +
                        $"(Common={tier.WeightCommon} Uncommon={tier.WeightUncommon} Rare={tier.WeightRare}).", this);

                if (tier.WeightCommon < 0 || tier.WeightUncommon < 0 || tier.WeightRare < 0)
                    Debug.LogError(
                        $"[{name}] Tier {i} (Mastery {tier.MasteryMin}-{tier.MasteryMax}) " +
                        "contains a negative weight — all weights must be >= 0.", this);
            }

            // Check tier ranges are contiguous (sorted by MasteryMin)
            for (int i = 1; i < _tiers.Length; i++)
            {
                if (_tiers[i].MasteryMin != _tiers[i - 1].MasteryMax + 1)
                    Debug.LogError(
                        $"[{name}] Tier ranges are not contiguous: " +
                        $"tier {i - 1} ends at Mastery {_tiers[i - 1].MasteryMax} but " +
                        $"tier {i} starts at Mastery {_tiers[i].MasteryMin}.", this);
            }
        }
#endif
    }
}

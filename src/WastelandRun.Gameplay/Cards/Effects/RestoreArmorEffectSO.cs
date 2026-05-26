using UnityEngine;
using WastelandRun.Cards;

namespace WastelandRun.Gameplay.Cards
{
    /// <summary>
    /// Authoring SO for the RestoreArmor card effect. Projects to <see cref="RestoreArmorEffect"/> —
    /// adds vehicle-level Armor up to MaxArmor via IVehicleMutator.AddArmor (TR-card-006).
    /// Target is implicit Self — no per-slot targeting field. OnValidate rejects Amount &lt; 1.
    /// </summary>
    [CreateAssetMenu(menuName = "Wasteland/Card/Effects/RestoreArmor")]
    public sealed class RestoreArmorEffectSO : CardEffectSO
    {
        [SerializeField] private int _amount;

        public int Amount => _amount;

        public override ICardEffect ToRuntime() => new RestoreArmorEffect(_amount);

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_amount < 1)
                Debug.LogError($"[{name}] RestoreArmorEffectSO.Amount must be >= 1 (got {_amount}).", this);
        }
#endif
    }
}

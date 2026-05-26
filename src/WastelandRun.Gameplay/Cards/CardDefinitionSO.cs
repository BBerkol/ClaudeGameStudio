using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using WastelandRun.Cards;
using WastelandRun.Vehicle;

namespace WastelandRun.Gameplay.Cards
{
    /// <summary>
    /// Single authority for all card metadata (TR-card-001). Implements ICardData — the engine-free
    /// runtime surface consumed by combat, rewards, UI, and save systems. OnValidate enforces all
    /// GDD data-contract rules (TR-card-002, TR-card-005, TR-card-008, TR-card-009, TR-card-025)
    /// at asset-save time in the Editor, never at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Wasteland/Card/Definition")]
    public sealed class CardDefinitionSO : ScriptableObject, ICardData
    {
        [SerializeField] private string _cardId;
        [SerializeField] private string _displayName;
        [SerializeField] private string _descriptionTemplate;
        [SerializeField] private string _flavorText;
        [SerializeField] private string _cardArtKey;
        [SerializeField] private ChassisType _chassisPool;
        [SerializeField] private CardFamily _family;
        [SerializeField] private CardRarity _rarity;
        [SerializeField] private bool _isStarterCard;
        [SerializeField] private int _energyCost;
        [SerializeField] private int _merchantPrice;
        [SerializeField] private CardTargetType _targetType;
        [SerializeField] private SlotType[] _validSubsystemTargets;
        [SerializeField] private PositionRequirement _positionRequirement;
        [SerializeField] private CardKeyword _keywords;
        [SerializeField] private CardEffectSO[] _effectSOs;
        /// <summary>Must equal the Amount of the first DamageEffectSO in Effects. 0 if no DamageEffectSO.</summary>
        [SerializeField] private int _baseDamage;

        // ICardData — runtime-only; not serialized per ADR-0007 Decision 16.
        public string? SourceSlotId => null;

        public string CardId                => _cardId;
        public string DisplayName           => _displayName;
        public string DescriptionTemplate   => _descriptionTemplate;
        public string FlavorText            => _flavorText;
        public string CardArtKey            => _cardArtKey;
        public ChassisType ChassisPool      => _chassisPool;
        public CardFamily Family            => _family;
        public CardRarity Rarity            => _rarity;
        public bool IsStarterCard           => _isStarterCard;
        public int EnergyCost               => _energyCost;
        public int MerchantPrice            => _merchantPrice;
        public CardTargetType TargetType    => _targetType;
        public PositionRequirement PositionRequirement => _positionRequirement;
        public CardKeyword Keywords         => _keywords;
        public int BaseDamage               => _baseDamage;

        public IReadOnlyList<SlotType> ValidSubsystemTargets =>
            _validSubsystemTargets ?? System.Array.Empty<SlotType>();

        public IReadOnlyList<ICardEffect> Effects =>
            _effectsCache ??= ProjectEffects();

        private IReadOnlyList<ICardEffect> _effectsCache;

        private IReadOnlyList<ICardEffect> ProjectEffects()
        {
            if (_effectSOs == null || _effectSOs.Length == 0)
                return System.Array.Empty<ICardEffect>();
            // Null entries are a designer error caught by OnValidate (ValidateNullEffects);
            // filter here to prevent NullReferenceException in combat resolution.
            var result = new System.Collections.Generic.List<ICardEffect>(_effectSOs.Length);
            for (int i = 0; i < _effectSOs.Length; i++)
            {
                if (_effectSOs[i] != null)
                    result.Add(_effectSOs[i].ToRuntime());
            }
            return result;
        }

#if UNITY_EDITOR
        private static readonly Regex _cardIdPattern =
            new Regex(@"^[a-z]+_[a-z]+_[0-9]{3}$", RegexOptions.Compiled);

        private void OnValidate()
        {
            // Invalidate projection cache before any read so editor edits to referenced
            // EffectConditionSOs round-trip through ToRuntime on the next access.
            _effectsCache = null;

            ValidateCardId();
            ValidateEnergyCost();
            ValidateMerchantPrice();
            ValidateKeywords();
            ValidateNullEffects();
            ValidateControlFamily();
            ValidateBypassPlating();
            ValidateBaseDamage();
        }

        private void ValidateNullEffects()
        {
            if (_effectSOs == null) return;
            for (int i = 0; i < _effectSOs.Length; i++)
            {
                if (_effectSOs[i] == null)
                    Debug.LogError(
                        $"[{name}] _effectSOs[{i}] is null — assign or remove the empty slot.", this);
            }
        }

        private void ValidateCardId()
        {
            if (string.IsNullOrEmpty(_cardId) || !_cardIdPattern.IsMatch(_cardId))
                Debug.LogError(
                    $"[{name}] CardId '{_cardId}' does not match required pattern ^[a-z]+_[a-z]+_[0-9]{{3}}$. " +
                    "Format: [chassis]_[family]_[seq] with exactly 3 zero-padded digits.", this);
        }

        private void ValidateEnergyCost()
        {
            if (_energyCost < 0)
                Debug.LogError($"[{name}] EnergyCost must be >= 0 (got {_energyCost}).", this);
        }

        private void ValidateMerchantPrice()
        {
            if (_merchantPrice == 30)
                Debug.LogError(
                    $"[{name}] MerchantPrice == 30 conflicts with GlobalPurgeCost (30). " +
                    "Use a different price. MerchantPrice = 0 means unlisted.", this);
        }

        private void ValidateKeywords()
        {
            bool hasEthereal = (_keywords & CardKeyword.Ethereal) != 0;
            bool hasRetain   = (_keywords & CardKeyword.Retain)   != 0;
            if (hasEthereal && hasRetain)
                Debug.LogError(
                    $"[{name}] Keywords contain both Ethereal and Retain — mutually exclusive (GDD EC2).", this);
        }

        private void ValidateControlFamily()
        {
            if (_family != CardFamily.Control) return;
            if (_effectSOs == null) { ReportControlFamilyError(); return; }

            foreach (var so in _effectSOs)
            {
                if (so is DamageEffectSO dmg && dmg.Amount >= 1)
                    return; // found qualifying damage effect
            }
            ReportControlFamilyError();
        }

        private void ReportControlFamilyError() =>
            Debug.LogError(
                $"[{name}] Control-family card must contain at least one DamageEffectSO with Amount >= 1 (TR-card-008).", this);

        private void ValidateBypassPlating()
        {
            if (_effectSOs == null) return;

            bool hasBypass = false;
            foreach (var so in _effectSOs)
            {
                if (so is DamageEffectSO dmg && dmg.BypassPlating)
                {
                    hasBypass = true;
                    break;
                }
            }
            if (!hasBypass) return;

            // Rule 1: BypassPlating + Frame in ValidSubsystemTargets
            if (_validSubsystemTargets != null)
            {
                foreach (var slot in _validSubsystemTargets)
                {
                    if (slot == SlotType.Frame)
                    {
                        Debug.LogError(
                            $"[{name}] BypassPlating cannot target SlotType.Frame (subsystem-strike only).", this);
                        break;
                    }
                }
            }

            // Rule 2: BypassPlating + AllEnemySubsystems
            if (_targetType == CardTargetType.AllEnemySubsystems)
                Debug.LogError(
                    $"[{name}] BypassPlating cannot be used with TargetType.AllEnemySubsystems.", this);

            // Rule 3: BypassPlating + empty ValidSubsystemTargets (empty = all slots, which includes Frame)
            if (_validSubsystemTargets == null || _validSubsystemTargets.Length == 0)
                Debug.LogError(
                    $"[{name}] BypassPlating requires explicit non-Frame ValidSubsystemTargets. " +
                    "Empty array means all slots (including Frame).", this);
        }

        private void ValidateBaseDamage()
        {
            if (_effectSOs == null) return;

            foreach (var so in _effectSOs)
            {
                if (so is DamageEffectSO dmg)
                {
                    // BaseDamage must be >= 1 on any card with a DamageEffectSO
                    if (_baseDamage < 1)
                        Debug.LogError(
                            $"[{name}] BaseDamage must be >= 1 on damage cards (got {_baseDamage}).", this);

                    // BaseDamage must equal the first DamageEffectSO's Amount
                    if (_baseDamage != dmg.Amount)
                        Debug.LogError(
                            $"[{name}] BaseDamage ({_baseDamage}) does not match " +
                            $"DamageEffectSO '{dmg.name}'.Amount ({dmg.Amount}) — keep these in sync (TR-card-025).", this);
                    return; // validate against first DamageEffectSO only
                }
            }
        }
#endif
    }
}

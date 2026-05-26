namespace WastelandRun.Cards
{
    /// <summary>Card rarity tier. Drives reward pool weights and copy limits. Legendary is reserved post-EA and never selected by RewardDrawAlgorithm.</summary>
    public enum CardRarity
    {
        Common,
        Uncommon,
        Rare,
        Legendary
    }
}

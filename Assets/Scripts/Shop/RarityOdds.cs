using UnityEngine;

/// <summary>
/// How often each tier turns up in the shop, as relative weights (2026-10-30).
/// Read ONLY by ShopRoller.PickByRarity. On the mode config, beside the pools
/// it's rolled against, so tuning the odds is one Inspector block and a mode
/// can have its own.
///
/// Weights, not percentages: tuning one tier doesn't mean re-tuning the rest.
/// The defaults happen to add to 100, so they read as percentages anyway.
/// </summary>
[System.Serializable]
public class RarityOdds
{
    [Tooltip("🚧 0: trash never comes from the shop, only from events (not built).")]
    [Min(0)] public int trash = 0;
    [Min(0)] public int common = 60;
    [Min(0)] public int uncommon = 27;
    [Min(0)] public int rare = 10;
    [Min(0)] public int legendary = 3;

    public int WeightOf(Rarity rarity) => rarity switch
    {
        Rarity.Common => common,
        Rarity.Uncommon => uncommon,
        Rarity.Rare => rare,
        Rarity.Legendary => legendary,
        Rarity.Trash => trash,
        _ => 0,
    };
}

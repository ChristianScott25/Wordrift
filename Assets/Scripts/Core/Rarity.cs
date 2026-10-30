/// <summary>
/// How rare something for sale is (2026-10-30). Every kind of thing the shop
/// sells carries one — bookmarks, consumables, checkouts, tile upgrades and the
/// tiles themselves — and the shop rolls a TIER first, then a thing from that
/// tier (ShopRoller.PickByRarity), so adding a sixth common doesn't make commons
/// more common.
///
/// ⚠️ THE NUMBERS ARE WRITTEN DOWN ON PURPOSE AND ARE SAVED IN EVERY ASSET.
/// Unity stores an enum as its number, so reordering these would quietly re-rank
/// every asset in the project. Common is 0 so that an asset nobody ranked — a
/// brand-new item — is Common and works straight away; were Trash 0, every
/// unranked thing would silently become trash. New tiers go on the END.
///
/// Trash is deliberately EMPTY for now: it will only ever come from events,
/// never the shop (RarityOdds.trash is 0). Cursed is NOT a tier — it's a
/// separate flag, and anything of any rarity can be cursed.
/// </summary>
public enum Rarity
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    Legendary = 3,
    Trash = 4,
}

public static class RarityText
{
    /// <summary>The word the player sees — "RARE". The one place it's written.</summary>
    public static string Label(Rarity rarity) => rarity switch
    {
        Rarity.Uncommon => "UNCOMMON",
        Rarity.Rare => "RARE",
        Rarity.Legendary => "LEGENDARY",
        Rarity.Trash => "TRASH",
        _ => "COMMON",
    };

    /// <summary>The word the player sees for the cursed flag.</summary>
    public const string Cursed = "CURSED";
}

using UnityEngine;

/// <summary>
/// 🚧 Gold Leaf (2026-10-29): puts a modifier (a 2L) on the tile it's dropped on
/// FOR THE REST OF THE RUN — a shop tile upgrade you get to aim.
///
/// It upgrades the tile's ORIGIN, the bag tile it was dealt as, which the save
/// already writes by value — so the badge is still there next round and after
/// a resume. The board tile gets the badge too so you see it at once.
///
/// Refused, the same way the shop's upgrade row would never offer it, on: a
/// tile playing as something else this round (an Ink Blot or a rewritten
/// letter — the bag tile underneath isn't what you're looking at), a wild, a
/// tile already holding the mode's maximum, and a 0-point tile when the
/// modifier would do nothing there (TileModifier.WorksOnZeroPointTile).
/// </summary>
[CreateAssetMenu(fileName = "Consumable_GoldLeaf", menuName = "Word Crush/Consumable/Gild Tile")]
public class GildTileConsumable : Consumable
{
    [Tooltip("What it puts on the tile. Must be in the mode's tileModifiers pool, " +
             "or a save can't bring it back.")]
    public TileModifier modifier;

    public override ConsumableTarget Targets => ConsumableTarget.BoardTile;

    // ChangesBoard stays true: it changes a tile's worth, not where tiles are,
    // but clearing the selection makes the word row re-read the new badge.
    public override string PowerText =>
        $"Drop it on a tile to give it a {(modifier != null ? modifier.badgeLabel : "modifier")} " +
        "for the rest of the run.";

    public override bool Use(ConsumableUse use)
    {
        var tile = use?.Tile;
        if (modifier == null || tile == null || tile.IsRewritten) return false;

        var spec = tile.Origin;
        if (spec == null || spec.IsWild || spec.Retired) return false;
        if (spec.baseScore == 0 && !modifier.WorksOnZeroPointTile) return false;

        int limit = use.Session != null && use.Session.Config != null ? use.Session.Config.maxModifiersPerTile : 0;
        if (!spec.AddModifier(modifier, limit)) return false;

        tile.AddModifier(modifier);
        return true;
    }
}

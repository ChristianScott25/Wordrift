using UnityEngine;

/// <summary>
/// Turns the tile it's dropped on into a wild for as long as that tile stays on
/// the board. The bag never hears about it: the tile goes back to being itself
/// the moment it leaves (played, discarded, cleared, redrawn) and next round it
/// is dealt as what it always was.
///
/// A PLAIN wild — 0 points, no badges — even on an upgraded tile. His call,
/// 2026-10-08: a wild that keeps its 3W is exactly what the shop refuses to sell.
///
/// All of the work is Board.MakeWild → Tile.BecomeWild. Refused (and so not
/// spent) on a tile that's already a wild.
/// </summary>
[CreateAssetMenu(fileName = "Consumable_InkBlot", menuName = "Word Crush/Consumable/Wild Tile")]
public class WildTileConsumable : Consumable
{
    public override ConsumableTarget Targets => ConsumableTarget.BoardTile;

    public override string PowerText =>
        "Drop it on a tile to make it a wild until it leaves the board. " +
        "It's worth 0 and loses its badges. Your bag isn't changed.";

    public override bool Use(ConsumableUse use) =>
        use != null && use.Board != null && use.Board.MakeWild(use.Tile);
}

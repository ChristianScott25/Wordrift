using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🚧 Shredder (2026-10-29): takes the tile it's dropped on out of your bag FOR
/// GOOD — thinning the bag so the tiles you want come round more often.
///
/// ⚠️ THE TILE IS RETIRED, NOT REMOVED. A tile's identity is its index in
/// RunState.TileBag and a saved round is a list of those indices, so deleting
/// one would re-point every index after it. RunState.RetireTile flags it
/// (TileSpec.Retired); RunState.ActiveTiles leaves it out of every deal, count
/// and shop roll from then on. Its Origin is what's retired, so shredding an
/// Ink-Blotted tile shreds the tile underneath.
///
/// The tile also leaves the board now (Board.RemoveTiles, like Eraser). Refused
/// once the bag is down to `minimumBagSize`, so a board can always be dealt.
/// </summary>
[CreateAssetMenu(fileName = "Consumable_Shredder", menuName = "Word Crush/Consumable/Shred Tile")]
public class ShredTileConsumable : Consumable
{
    [Tooltip("Refuses to shred below this many tiles left in the bag.")]
    [Min(1)] public int minimumBagSize = 40;

    public override ConsumableTarget Targets => ConsumableTarget.BoardTile;

    public override string PowerText =>
        "Drop it on a tile to remove that tile from your bag for good. " +
        $"Won't take your bag below {minimumBagSize} tiles.";

    public override bool Use(ConsumableUse use)
    {
        var board = use?.Board;
        var run = use?.Run;
        var tile = use?.Tile;
        if (board == null || run == null || tile == null || board.Busy || board.Resolving) return false;
        if (tile.Origin == null || run.ActiveTileCount <= minimumBagSize) return false;

        if (!run.RetireTile(tile.Origin)) return false;
        board.RemoveTiles(new List<Tile> { tile }, rippleFrom: use.Cell);
        return true;
    }
}

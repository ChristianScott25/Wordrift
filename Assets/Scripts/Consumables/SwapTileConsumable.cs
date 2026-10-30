using UnityEngine;

/// <summary>
/// 🚧 Transpose (2026-10-29): swaps the tile it's dropped on with the tile
/// directly above it — or below it, on the top row. Board.SwapTiles does the
/// move; nothing is added or removed. Refused when neither neighbour has a tile.
/// </summary>
[CreateAssetMenu(fileName = "Consumable_Transpose", menuName = "Word Crush/Consumable/Swap Tile")]
public class SwapTileConsumable : Consumable
{
    public override ConsumableTarget Targets => ConsumableTarget.BoardTile;

    public override string PowerText =>
        "Drop it on a tile to swap it with the tile above it (or below it, if there's none above).";

    public override bool Use(ConsumableUse use)
    {
        var board = use?.Board;
        if (board == null || use.Tile == null) return false;

        // Up is +y: the board deals and falls bottom to top.
        var cell = use.Tile.Cell;
        if (!board.Tiles.TryGetValue(cell + Vector2Int.up, out var other) || other == null)
            board.Tiles.TryGetValue(cell + Vector2Int.down, out other);

        if (other == null) return false;

        // Two plain tiles of the same letter: the swap would change nothing you
        // could see, so it isn't spent.
        if (use.Tile.Face == other.Face && use.Tile.Modifiers.Count == 0 && other.Modifiers.Count == 0)
            return false;

        return board.SwapTiles(use.Tile, other);
    }
}

using UnityEngine;

/// <summary>
/// Clears every tile in one row, or one column, of the board — dropped on a
/// tile to pick which. Strikethrough (row) and Bookworm (column) are two assets
/// of this one class; it's named for the power, like every consumable.
///
/// It is a discard you don't pay discards for (his call, 2026-10-08 — the item
/// is the cost): the tiles are gone for the round, the board falls and refills
/// from the bag, and the discard counter doesn't move. All of that is
/// Board.RemoveTiles, the same call DISCARD makes.
///
/// It ripples: the tile it was dropped on spins away first, then its
/// neighbours on both sides, out to the edges (Board.RemoveTiles).
///
/// A round the clear empties is ended by GameSession.Update's ordinary
/// round-over check once the board has settled, same as a discard's.
/// </summary>
[CreateAssetMenu(fileName = "Consumable_Strikethrough", menuName = "Word Crush/Consumable/Line Clear")]
public class LineClearConsumable : Consumable
{
    [Tooltip("On: clears the ROW the item is dropped on. Off: the COLUMN.")]
    public bool horizontal = true;

    public override ConsumableTarget Targets => ConsumableTarget.BoardTile;

    public override string PowerText => horizontal
        ? "Drop it on a tile to clear that whole row. Doesn't use your discards."
        : "Drop it on a tile to clear that whole column. Doesn't use your discards.";

    public override bool Use(ConsumableUse use)
    {
        var board = use?.Board;
        if (board == null || use.Tile == null || board.Busy || board.Resolving) return false;

        var line = board.TilesInLine(use.Cell, horizontal);
        if (line.Count == 0) return false;

        board.RemoveTiles(line, rippleFrom: use.Cell);
        return true;
    }
}

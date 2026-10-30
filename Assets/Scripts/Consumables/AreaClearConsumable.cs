using UnityEngine;

/// <summary>
/// 🚧 Clears the tiles around the one it's dropped on (2026-10-29): Eraser
/// (radius 0 — just that tile) and Coffee Ring (radius 1 — the 3x3 around it)
/// are two assets of this class.
///
/// The same deal as LineClearConsumable: it's Board.RemoveTiles, the call
/// DISCARD makes, so the tiles are gone for the round and the board refills —
/// but your discard allowance is untouched.
/// </summary>
[CreateAssetMenu(fileName = "Consumable_Eraser", menuName = "Word Crush/Consumable/Area Clear")]
public class AreaClearConsumable : Consumable
{
    [Tooltip("0 = only the tile it's dropped on; 1 = the 3x3 around it; and so on.")]
    [Min(0)] public int radius = 0;

    public override ConsumableTarget Targets => ConsumableTarget.BoardTile;

    public override string PowerText => radius <= 0
        ? "Drop it on a tile to remove just that tile. Doesn't use your discards."
        : $"Drop it on a tile to clear the {radius * 2 + 1}x{radius * 2 + 1} square around it. Doesn't use your discards.";

    public override bool Use(ConsumableUse use)
    {
        var board = use?.Board;
        if (board == null || use.Tile == null || board.Busy || board.Resolving) return false;

        var area = board.TilesAround(use.Cell, radius);
        if (area.Count == 0) return false;

        board.RemoveTiles(area, rippleFrom: use.Cell);
        return true;
    }
}

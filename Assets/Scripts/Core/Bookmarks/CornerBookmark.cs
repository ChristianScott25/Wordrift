using UnityEngine;

/// <summary>
/// Flat POINTS when any tile of the word sits on one of the board's corners.
///
/// Corners are the board's outer box (Board.CellBounds), so a corner the
/// Dilapidated has closed just can't be used that round.
/// </summary>
[CreateAssetMenu(fileName = "DogEar", menuName = "Word Crush/Bookmark/Corner")]
public class CornerBookmark : Bookmark
{
    [Min(0)] public int bonusPoints = 15;

    public override void OnWordScored(ScoringContext ctx)
    {
        if (ctx.Tiles == null) return;

        foreach (var tile in ctx.Tiles)
        {
            if (tile == null || !ctx.IsCorner(tile.Cell)) continue;
            ctx.AddPoints(bonusPoints, displayName);
            return;
        }
    }
}

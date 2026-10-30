using UnityEngine;

/// <summary>
/// Adds to the MULT when no tile of the word touches the board's outer edge —
/// on a 5x5, the word lives entirely in the middle 3x3.
/// </summary>
[CreateAssetMenu(fileName = "Centerpiece", menuName = "Word Crush/Bookmark/No Edge")]
public class NoEdgeBookmark : Bookmark
{
    [Min(0f)] public float multBonus = 4f;

    public override void OnWordScored(ScoringContext ctx)
    {
        // ⚠️ Without a board, IsEdge says false for everything — which would
        // read as "never touched the edge" and fire on every word.
        if (ctx.Tiles == null || ctx.Tiles.Count == 0) return;
        if (ctx.BoardBounds.width <= 0 || ctx.BoardBounds.height <= 0) return;

        foreach (var tile in ctx.Tiles)
            if (tile == null || ctx.IsEdge(tile.Cell)) return;

        ctx.AddMult(multBonus, displayName);
    }
}

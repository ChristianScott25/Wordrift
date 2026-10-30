using UnityEngine;

/// <summary>
/// Multiplies the MULT when every tile of the word sits on the board's outer
/// edge — a word that walks the frame. Centerpiece's opposite.
/// </summary>
[CreateAssetMenu(fileName = "GiltEdge", menuName = "Word Crush/Bookmark/Edge Only")]
public class EdgeOnlyBookmark : Bookmark
{
    [Min(1f)] public float multiplier = 2f;

    public override void OnWordScored(ScoringContext ctx)
    {
        if (ctx.Tiles == null || ctx.Tiles.Count == 0) return;

        foreach (var tile in ctx.Tiles)
            if (tile == null || !ctx.IsEdge(tile.Cell)) return;

        ctx.MultiplyMult(multiplier, displayName);
    }
}

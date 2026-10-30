using UnityEngine;

/// <summary>
/// Multiplies the MULT when the word has a tile in EVERY column of the board
/// (or every row, with <see cref="rows"/> ticked). On a 5x5 that's a word of
/// at least five tiles weaving all the way across.
///
/// One class, two assets — Columnist and Ruled Paper — the same way the two
/// line-clear items share LineClearConsumable.
/// </summary>
[CreateAssetMenu(fileName = "Columnist", menuName = "Word Crush/Bookmark/Every Column or Row")]
public class EveryLineBookmark : Bookmark
{
    [Tooltip("Off = every COLUMN; on = every ROW.")]
    public bool rows;

    [Min(1f)] public float multiplier = 3f;

    public override void OnWordScored(ScoringContext ctx)
    {
        if (ctx.Tiles == null) return;

        var box = ctx.BoardBounds;
        int start = rows ? box.yMin : box.xMin;
        int count = rows ? box.height : box.width;

        // A bit per line. 31 is far past any board this game has, and guarding
        // it keeps the shift from aliasing (C# masks the shift count).
        if (count <= 0 || count > 31) return;

        int seen = 0;
        foreach (var tile in ctx.Tiles)
        {
            if (tile == null) continue;
            int line = (rows ? tile.Cell.y : tile.Cell.x) - start;
            if (line >= 0 && line < count) seen |= 1 << line;
        }

        if (seen != (1 << count) - 1) return;
        ctx.MultiplyMult(multiplier, displayName);
    }
}

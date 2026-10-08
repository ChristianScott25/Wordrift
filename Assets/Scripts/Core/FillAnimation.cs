using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decides WHEN each new tile starts falling into the board — never WHICH tile
/// it is or where it goes.
///
/// Separate from IRefillPolicy (which cells get filled) and IGravityRule (where
/// survivors land) for the same reason those two are separate from each other:
/// each is one question. A new look for a refill is one small class here.
///
/// ⚠️ ANIMATION NEVER CHANGES WHAT IS DRAWN. The board spawns the tiles — and so
/// draws them from the bag — in exactly the order it always has, and only then
/// asks this for delays. A pattern that wanted tiles DRAWN in its own order
/// would move every seed and every saved bag; that's why it gets cells and
/// hands back times, and never touches a tile.
/// </summary>
public interface ITileFillAnimation
{
    /// <summary>
    /// Fills `into[i]` with how many seconds cell `cells[i]` waits before its
    /// tile starts to fall. `into` is at least as long as `cells`.
    /// </summary>
    void Delays(IReadOnlyList<Vector2Int> cells, float[] into);
}

/// <summary>
/// One tile at a time: the bottom row left to right, then the next row up.
/// The opening board of every round, Restart, and RedrawConsumable — the fills
/// big enough to be worth watching. His call, 2026-10-08.
/// </summary>
public class DealRowByRow : ITileFillAnimation
{
    private readonly float step;

    public DealRowByRow(float step) => this.step = Mathf.Max(0f, step);

    public void Delays(IReadOnlyList<Vector2Int> cells, float[] into)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            // Rank = how many cells come before this one in deal order. n² over a
            // 25-cell board is nothing, and it needs no sorted copy.
            int rank = 0;
            for (int j = 0; j < cells.Count; j++)
                if (DealsBefore(cells[j], cells[i])) rank++;
            into[i] = rank * step;
        }
    }

    private static bool DealsBefore(Vector2Int a, Vector2Int b) =>
        a.y < b.y || (a.y == b.y && a.x < b.x);
}

/// <summary>
/// Column by column, left to right; a column's tiles drop together. Every
/// everyday refill — after a word, a discard or a cleared line — because a
/// full deal after every word would add about a second to every play.
/// Only the columns actually getting tiles are ranked, so a refill in one
/// column drops at once.
/// </summary>
public class SweepColumns : ITileFillAnimation
{
    private readonly float step;

    public SweepColumns(float step) => this.step = Mathf.Max(0f, step);

    public void Delays(IReadOnlyList<Vector2Int> cells, float[] into)
    {
        // The filling columns, left to right; a cell's rank is its column's place.
        var columns = new List<int>();
        foreach (var cell in cells)
            if (!columns.Contains(cell.x)) columns.Add(cell.x);
        columns.Sort();

        for (int i = 0; i < cells.Count; i++)
            into[i] = columns.IndexOf(cells[i].x) * step;
    }
}

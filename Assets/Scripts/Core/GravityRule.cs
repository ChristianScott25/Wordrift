using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Where one surviving tile ends up after gravity. To may equal From.</summary>
public struct TileMove
{
    public Vector2Int From;
    public Vector2Int To;
}

/// <summary>The full result of one gravity pass.</summary>
public struct GravityPlan
{
    /// <summary>One entry for EVERY surviving tile, even the ones that don't move.</summary>
    public List<TileMove> Moves;

    /// <summary>
    /// Cells with no tile once everything has fallen. Whether these actually
    /// get new tiles is not gravity's call — see IRefillPolicy.
    /// </summary>
    public List<Vector2Int> Empties;
}

/// <summary>
/// Decides how tiles fall. Isolated behind this interface because non-rectangular
/// boards raise real questions later (do tiles fall through holes? diagonally
/// into gaps?) and we'll want to swap the answer without touching Board.
/// </summary>
public interface IGravityRule
{
    GravityPlan Plan(IEnumerable<Vector2Int> cells, ICollection<Vector2Int> occupied);
}

/// <summary>
/// Straight-down gravity: within each column, surviving tiles compact to the
/// lowest cells and the gaps open at the top. On a board with holes, tiles
/// currently fall past them to the lowest empty cell in that column.
/// </summary>
public class ColumnGravity : IGravityRule
{
    public GravityPlan Plan(IEnumerable<Vector2Int> cells, ICollection<Vector2Int> occupied)
    {
        var plan = new GravityPlan
        {
            Moves = new List<TileMove>(),
            Empties = new List<Vector2Int>(),
        };

        foreach (var column in cells.GroupBy(c => c.x))
        {
            var columnCells = column.OrderBy(c => c.y).ToList();
            var survivors = columnCells.Where(occupied.Contains).ToList();

            for (int i = 0; i < survivors.Count; i++)
                plan.Moves.Add(new TileMove { From = survivors[i], To = columnCells[i] });

            for (int i = survivors.Count; i < columnCells.Count; i++)
                plan.Empties.Add(columnCells[i]);
        }

        return plan;
    }
}

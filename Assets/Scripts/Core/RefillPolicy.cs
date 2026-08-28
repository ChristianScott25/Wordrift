using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decides which empty cells get a fresh tile.
///
/// This is deliberately separate from IGravityRule: gravity says where the
/// surviving tiles end up, this says whether the holes they left get filled.
/// Match-3 style modes want every hole filled immediately; a mode that feeds
/// tiles in on its own clock wants none of them filled.
/// </summary>
public interface IRefillPolicy
{
    /// <summary>
    /// Given every cell that has no tile, return the ones to fill right now.
    /// Returning nothing is valid — the board is allowed to have gaps.
    /// </summary>
    IEnumerable<Vector2Int> CellsToFill(IReadOnlyList<Vector2Int> empties);
}

/// <summary>
/// Top the board up completely, so no cell is ever empty once things settle.
/// The default, and what Rogue Demo plays on — until its bag runs dry, when
/// there is simply nothing to top up with.
/// </summary>
public class FillEveryCell : IRefillPolicy
{
    public IEnumerable<Vector2Int> CellsToFill(IReadOnlyList<Vector2Int> empties) => empties;
}

/// <summary>
/// Never fill anything. Cleared cells stay empty and the stack just compacts,
/// which leaves the board's population entirely up to whoever is dropping
/// tiles in — that's the mode.
///
/// Nothing uses this since Overflow mode was cut. Kept because it's half of
/// what a mode owning its own board population needs, and it costs nothing.
/// </summary>
public class NeverRefill : IRefillPolicy
{
    private static readonly Vector2Int[] Nothing = new Vector2Int[0];

    public IEnumerable<Vector2Int> CellsToFill(IReadOnlyList<Vector2Int> empties) => Nothing;
}

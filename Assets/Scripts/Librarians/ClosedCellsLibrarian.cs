using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// THE DILAPIDATED. A few spaces on the board simply aren't there.
///
/// The cells are drawn when the round is set up and stay shut for all of it:
/// nothing ever spawns in one, nothing can be chained through one, and a word
/// has to route around it. On a board this small three holes change every shape
/// the player is used to reading.
///
/// WHAT HAPPENS WHEN TILES FALL is the interesting half, and it costs nothing:
/// a closed cell is subtracted from the board's shape, so the column it was in
/// has one fewer slot and ColumnGravity compacts into what's left. Tiles above
/// the hole fall straight PAST it to fill the space below, and stop above it
/// once that space is full. The hole never fills.
///
/// It's a CHOOSING librarian, like The Censor, and plays by the same rules: the
/// draw happens once, in Apply, from a stream keyed to the round — so a round
/// re-entered after a save comes back with the same holes and there is nothing
/// to write to disk. It differs in where the choice lands: The Censor writes a
/// letter into RoundRules.Note, this fills in RoundRules.ClosedCells, which the
/// mode hands to the board before the board is built.
///
/// The banner doesn't say WHICH cells, so there's no PowerFor override — unlike
/// a banned letter, the player can see these.
/// </summary>
[CreateAssetMenu(fileName = "Librarian_Dilapidated",
                 menuName = "Word Crush/Librarian/Closed Cells")]
public class ClosedCellsLibrarian : Librarian
{
    [Tooltip("How many spaces are closed for the round. Drawn from the board's " +
             "own cells, subject to the neighbour rule below.")]
    [Min(1)] public int closedCells = 3;

    [Tooltip("The fewest open neighbours every remaining space must still have. " +
             "2 is what stops the holes from walling a space off — three of them " +
             "around a corner would otherwise strand the tile sitting in it, " +
             "where it could never be played or discarded. 0 turns the rule off " +
             "and lets the holes land anywhere.")]
    [Min(0)] public int minNeighbours = 2;

    public override string PowerText =>
        closedCells == 1
            ? "One space on the board is closed."
            : $"{closedCells} spaces on the board are closed.";

    public override void Apply(RoundRules rules)
    {
        var board = rules.BoardCells;
        if (rules.Rng == null || board == null || board.Count == 0) return;

        var open = new HashSet<Vector2Int>(board);

        // Never every cell. A board with nothing left on it isn't a hard round,
        // it's a round that can't be played at all.
        int wanted = Mathf.Min(closedCells, open.Count - 1);

        for (int i = 0; i < wanted; i++)
        {
            // Rebuilt each time round rather than filtered once, because closing
            // one space changes which OTHER spaces are still safe to close.
            //
            // Built by walking `board` — an ordered list — and testing membership,
            // NOT by iterating `open`: a HashSet's enumeration order isn't
            // guaranteed stable across runtimes, and a draw whose candidate order
            // could differ between the editor and the device would quietly stop
            // being reproducible from the seed.
            var candidates = new List<Vector2Int>();
            foreach (var cell in board)
                if (open.Contains(cell) && SafeToClose(cell, open)) candidates.Add(cell);

            // Nowhere left that doesn't strand something. Closing fewer spaces is
            // the right answer — it's still the round the banner describes, just
            // gentler — but it's worth a line in the log, because a board shape
            // that can't take three holes is a tuning problem, not a bad roll.
            if (candidates.Count == 0)
            {
                Debug.LogWarning(
                    $"{Title} could only close {i} of {wanted} spaces on this board " +
                    $"while leaving every space {minNeighbours} neighbours.", this);
                break;
            }

            var pick = candidates[rules.Rng.Range(0, candidates.Count)];
            open.Remove(pick);
            rules.ClosedCells.Add(pick);
        }
    }

    /// <summary>
    /// Would closing this space leave every space next to it with neighbours
    /// enough? Checking only the neighbours IS checking the whole board: closing
    /// a cell can't change anyone else's count, and the board satisfies the rule
    /// before each closure by induction — it starts out satisfying it, and no
    /// closure that would break it is ever accepted.
    ///
    /// Adjacency is Board.AreAdjacent, the same call the chain uses, so "next to"
    /// means the same thing here as it does to the player's finger. It counts
    /// diagonals — if diagonal chaining is ever turned off, this has to follow it.
    /// </summary>
    private bool SafeToClose(Vector2Int cell, HashSet<Vector2Int> open)
    {
        if (minNeighbours <= 0) return true;

        foreach (var other in open)
        {
            if (other == cell || !Board.AreAdjacent(other, cell)) continue;
            if (NeighboursAfterClosing(other, open, cell) < minNeighbours) return false;
        }
        return true;
    }

    /// <summary>Open spaces next to this one, pretending `closing` is already shut.</summary>
    private static int NeighboursAfterClosing(
        Vector2Int cell, HashSet<Vector2Int> open, Vector2Int closing)
    {
        int count = 0;
        foreach (var other in open)
            if (other != closing && Board.AreAdjacent(cell, other)) count++;
        return count;
    }
}

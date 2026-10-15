using System.Collections.Generic;
using System.Text;

/// <summary>
/// One word the board can spell: the tiles in order, and the word they make.
/// For a wild or a choice tile, `Word` holds the letter the search tried there.
/// </summary>
public class FoundWord
{
    public List<Tile> Path;
    public string Word;
}

/// <summary>
/// Finds every word the board can spell starting on one tile — the Cheat Sheet's
/// search. Walks outward through adjacent tiles (diagonals included, each tile
/// once), and drops a path the moment no dictionary word starts with what it
/// spells, which is what keeps a 5x5 board to a few thousand steps.
///
/// It only FINDS. Which word is best is GameSession's question, because that
/// needs the scorer and the round's rules, and Core may not reference Modes.
///
/// ⚠️ DETERMINISTIC AND RNG-FREE. Neighbours are visited in a fixed order (by
/// x, then y, off each tile's cell — never by enumerating the board's
/// Dictionary, whose order isn't stable across runtimes) and a wild tries its
/// letters a to z. So the same board always gives the same list in the same
/// order, and nothing here can shift a seed.
///
/// Tiles spell what they spell: a "ch" tile adds both letters, a wild tries all
/// 26 and a choice tile only its own options. Length is counted in LETTERS, the
/// same as everywhere else.
/// </summary>
public static class WordFinder
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz";

    public static List<FoundWord> From(Board board, Tile start, WordValidator validator,
                                       int minLetters)
    {
        var found = new List<FoundWord>();
        if (board == null || start == null || validator == null) return found;

        var path = new List<Tile>();
        var spelled = new StringBuilder();
        Step(board, start, validator, minLetters, path, spelled, found);
        return found;
    }

    private static void Step(Board board, Tile tile, WordValidator validator, int minLetters,
                             List<Tile> path, StringBuilder spelled, List<FoundWord> found)
    {
        if (tile == null || !tile.IsSettled || path.Contains(tile)) return;

        foreach (string piece in PiecesOf(tile))
        {
            int mark = spelled.Length;
            spelled.Append(piece);
            string soFar = spelled.ToString();

            if (validator.HasPrefix(soFar))
            {
                path.Add(tile);

                if (soFar.Length >= minLetters && validator.Contains(soFar))
                    found.Add(new FoundWord { Path = new List<Tile>(path), Word = soFar });

                foreach (var next in NeighboursOf(board, tile))
                    Step(board, next, validator, minLetters, path, spelled, found);

                path.RemoveAt(path.Count - 1);
            }

            spelled.Length = mark;
        }
    }

    /// <summary>What this tile can put into the word, one option at a time.</summary>
    private static IEnumerable<string> PiecesOf(Tile tile)
    {
        if (tile.Spec != null && tile.Spec.IsWild)
        {
            foreach (char c in Alphabet) yield return c.ToString();
            yield break;
        }

        if (!string.IsNullOrEmpty(tile.Options))
        {
            foreach (char c in tile.Options) yield return c.ToString();
            yield break;
        }

        if (!string.IsNullOrEmpty(tile.Letters))
            yield return tile.Letters.ToLowerInvariant();
    }

    /// <summary>The tiles around this one, in a fixed order.</summary>
    private static IEnumerable<Tile> NeighboursOf(Board board, Tile tile)
    {
        var tiles = board.Tiles;
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0) continue;
            var cell = new UnityEngine.Vector2Int(tile.Cell.x + dx, tile.Cell.y + dy);
            if (tiles.TryGetValue(cell, out var next) && next != null) yield return next;
        }
    }
}

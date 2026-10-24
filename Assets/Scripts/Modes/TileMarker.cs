using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps a librarian's MARKED tiles on the board: the start-on / end-on tiles
/// and the infected ones. Owned by the mode, because it is ROUND STATE — which
/// tiles are marked depends on what the player has played — and a librarian is
/// a read-only recipe that can't hold any. The librarian only says how many and
/// what kind (RoundRules.MarkCount / MarkKind); this does the rest.
///
/// It tops the marks back up to the count WHENEVER the board is at rest and
/// short — after the opening deal, after a word's refill, after a discard or an
/// item clears a marked tile. A mark belongs to the Tile object (Tile.Mark), so
/// a tile that falls or is shuffled keeps it, and one that leaves the board
/// simply drops out of the list on the next Tick.
///
/// ⚠️ DETERMINISTIC FROM THE SEED. Candidates come from Board.OccupiedInOrder,
/// never from Board.Tiles (a Dictionary, whose order isn't stable across
/// runtimes), and every pick is one draw off the round's own MarkStream, whose
/// position is saved. Picking happens only when a top-up is due, so the
/// per-frame Tick allocates nothing.
/// </summary>
public class TileMarker
{
    private readonly TileMark kind;
    private readonly int count;
    private readonly string note;
    private readonly List<Tile> marked = new();

    // Reused between picks so a top-up allocates as little as it can.
    private readonly List<Tile> candidates = new();
    private readonly HashSet<string> markedFaces = new();

    // Cells handed back by a restore, applied once the board exists.
    private readonly List<Vector2Int> pendingCells = new();

    public Rng Rng { get; private set; }

    // A top-up that ran out of tiles to mark remembers the board it gave up on,
    // so a nearly-empty board isn't re-scanned (and re-allocated) every frame.
    // -1 = not stalled.
    private int stalledTileCount = -1;

    public TileMarker(TileMark kind, int count, string note, Rng rng)
    {
        this.kind = kind;
        this.count = Mathf.Max(0, count);
        this.note = note ?? "";
        Rng = rng;
    }

    /// <summary>Whether this round marks anything at all.</summary>
    public bool Active => kind != TileMark.None && count > 0;

    /// <summary>
    /// Every frame. Drops marks whose tile has left the board, then — only with
    /// the board at rest — tops back up to the count.
    /// </summary>
    public void Tick(Board board)
    {
        if (!Active || board == null) return;

        Prune(board);
        if (marked.Count >= count || board.Busy || board.Resolving) return;
        if (board.Tiles.Count == stalledTileCount) return;
        TopUp(board);
    }

    /// <summary>
    /// A tile still counts only while the board's map holds IT at its cell. A
    /// played tile is released, a discarded or bombed one removed, a redrawn
    /// one replaced — every way out of the board fails this one test.
    /// </summary>
    private void Prune(Board board)
    {
        for (int i = marked.Count - 1; i >= 0; i--)
        {
            var tile = marked[i];
            if (tile != null && board.Tiles.TryGetValue(tile.Cell, out var there) && there == tile)
                continue;
            marked.RemoveAt(i);
            stalledTileCount = -1;   // the board changed under the marks
        }
    }

    /// <summary>
    /// Marks tiles until the count is met or nothing is left to mark. Each pick
    /// tries hardest for a letter that isn't already marked, then for any tile
    /// that isn't a wild (a wild start tile fits every word), then for anything.
    /// </summary>
    private void TopUp(Board board)
    {
        markedFaces.Clear();
        foreach (var tile in marked) markedFaces.Add(tile.Face);

        stalledTileCount = -1;
        var cells = board.OccupiedInOrder();
        while (marked.Count < count)
        {
            var pick = Pick(board, cells, freshLetter: true, allowWild: false)
                    ?? Pick(board, cells, freshLetter: false, allowWild: false)
                    ?? Pick(board, cells, freshLetter: false, allowWild: true);
            if (pick == null)
            {
                // Every tile on the board is already marked. Try again only
                // once the board holds a different number of tiles.
                stalledTileCount = board.Tiles.Count;
                return;
            }

            Mark(pick);
        }
    }

    private Tile Pick(Board board, List<Vector2Int> cells, bool freshLetter, bool allowWild)
    {
        candidates.Clear();
        foreach (var cell in cells)
        {
            if (!board.Tiles.TryGetValue(cell, out var tile) || tile == null) continue;
            if (tile.Mark != TileMark.None) continue;
            if (!allowWild && tile.Spec != null && tile.Spec.IsWild) continue;
            if (freshLetter && markedFaces.Contains(tile.Face)) continue;
            candidates.Add(tile);
        }

        if (candidates.Count == 0) return null;
        // No stream (no run) still marks, just not reproducibly — the first one.
        return candidates[Rng == null ? 0 : Rng.Range(0, candidates.Count)];
    }

    private void Mark(Tile tile)
    {
        tile.SetMark(kind, note);
        marked.Add(tile);
        markedFaces.Add(tile.Face);
    }

    /// <summary>The marked cells, for the round's save.</summary>
    public void Capture(List<int> intoX, List<int> intoY)
    {
        foreach (var tile in marked)
        {
            if (tile == null) continue;
            intoX.Add(tile.Cell.x);
            intoY.Add(tile.Cell.y);
        }
    }

    /// <summary>
    /// A save's marks and stream position. The cells are held until the board
    /// has been restored (ApplyRestored) — the board doesn't exist yet here.
    /// </summary>
    public void Restore(List<int> cellX, List<int> cellY, Rng rng)
    {
        Rng = rng;
        pendingCells.Clear();
        if (cellX == null || cellY == null) return;
        int n = Mathf.Min(cellX.Count, cellY.Count);
        for (int i = 0; i < n; i++) pendingCells.Add(new Vector2Int(cellX[i], cellY[i]));
    }

    /// <summary>Marks the restored board's tiles at the saved cells.</summary>
    public void ApplyRestored(Board board)
    {
        if (board == null) return;
        foreach (var cell in pendingCells)
            if (marked.Count < count && board.Tiles.TryGetValue(cell, out var tile) &&
                tile != null && tile.Mark == TileMark.None)
                Mark(tile);
        pendingCells.Clear();
    }
}

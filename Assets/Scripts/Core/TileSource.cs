using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where the board gets the next tile.
///
/// Split out for the same reason as IGravityRule and IRefillPolicy: the board
/// shouldn't have to care whether tiles are endless. An arcade mode wants an
/// infinite weighted draw (EndlessTiles). A roguelike round wants a sack it can
/// empty (TileSack) — and when that sack is empty the board simply stops
/// producing tiles, leaving the cells it would have filled alone.
///
/// A mode installs its own source in GameMode.Attach, which runs before
/// Board.Build. That matters: the opening fill is drawn through this too.
/// </summary>
public interface ITileSource
{
    /// <summary>
    /// Restores the source to its starting state. The board calls this before
    /// every full fill — Build and ResetBoard both — so a finite source comes
    /// back whole at the start of each round without the mode remembering to.
    /// </summary>
    void Reset();

    /// <summary>
    /// The next tile, or false when there is nothing left to draw. False is a
    /// normal outcome, not an error: the board just doesn't spawn that tile.
    /// </summary>
    bool TryDraw(out TileSpec tile);

    /// <summary>How many tiles are left, or -1 when the source is endless.</summary>
    int Remaining { get; }
}

/// <summary>
/// The default: draw plain tiles from a LetterSet's spawn weights forever.
/// What Moves mode plays on, and what Board falls back to when no mode
/// installed anything.
/// </summary>
public class EndlessTiles : ITileSource
{
    private readonly LetterSet letters;

    public EndlessTiles(LetterSet letters) => this.letters = letters;

    public int Remaining => -1;

    public void Reset() { }

    public bool TryDraw(out TileSpec tile)
    {
        tile = null;
        var entry = letters == null ? null : letters.Draw();
        if (entry == null) return false;
        tile = LetterSet.CreateSpec(entry);
        return true;
    }
}

/// <summary>
/// A finite sack of tiles, drawn WITHOUT replacement — once the E's are gone
/// there are no more E's this round.
///
/// The sack drains a COPY of the stock it was given, and Reset refills the copy
/// from the stock. Two things follow. The full sack returns at the start of
/// every round — playing tiles never shrinks the run. And the stock is a live
/// view of the run's tiles (see RunState.Sack), so a tile a shop adds mid-run
/// is simply there on the next Reset, with no re-wiring.
///
/// Running dry is the point, not a failure. The board's opening fill is paid
/// for out of the sack like everything else, so a 25-cell board has already
/// spent a quarter of a 98-tile sack before the first word.
/// </summary>
public class TileSack : ITileSource
{
    private readonly IReadOnlyList<TileSpec> stock;
    private readonly List<TileSpec> remaining = new();

    public TileSack(IReadOnlyList<TileSpec> stock)
    {
        this.stock = stock;
        Reset();
    }

    public int Remaining => remaining.Count;

    /// <summary>Tiles in a full sack — the denominator for anything showing how much is left.</summary>
    public int Capacity { get; private set; }

    public void Reset()
    {
        remaining.Clear();
        if (stock != null) remaining.AddRange(stock);
        Capacity = remaining.Count;
        if (Capacity == 0)
            Debug.LogError("TileSack was given an empty stock, so no tiles will spawn.");
    }

    /// <summary>
    /// Pulls one tile out at random and keeps it out for the round.
    ///
    /// Drawing at random beats shuffling once up front: Reset stays free, and
    /// tiles can be added mid-round (a shop, a bookmark) without re-shuffling.
    /// </summary>
    public bool TryDraw(out TileSpec tile)
    {
        tile = null;
        if (remaining.Count == 0) return false;

        int index = Random.Range(0, remaining.Count);
        tile = remaining[index];

        // Swap-remove: position in the sack means nothing, and RemoveAt would
        // shift up to 97 entries down on every single tile.
        remaining[index] = remaining[remaining.Count - 1];
        remaining.RemoveAt(remaining.Count - 1);
        return true;
    }
}

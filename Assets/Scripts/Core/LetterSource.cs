using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where the board gets the letter for the next tile.
///
/// Split out for the same reason as IGravityRule and IRefillPolicy: the board
/// shouldn't have to care whether letters are endless. An arcade mode wants an
/// infinite weighted draw (EndlessLetters). A roguelike round wants a bag it can
/// empty (TileBag) — and when that bag is empty the board simply stops producing
/// tiles, leaving the cells it would have filled alone.
///
/// A mode installs its own source in GameMode.Attach, which runs before
/// Board.Build. That matters: the opening fill is drawn through this too.
/// </summary>
public interface ILetterSource
{
    /// <summary>
    /// Restores the source to its starting state. The board calls this before
    /// every full fill — Build and ResetBoard both — so a finite source comes
    /// back whole at the start of each round without the mode remembering to.
    /// </summary>
    void Reset();

    /// <summary>
    /// The next letter, or false when there is nothing left to draw. False is a
    /// normal outcome, not an error: the board just doesn't spawn that tile.
    /// </summary>
    bool TryDraw(out char letter);

    /// <summary>How many letters are left, or -1 when the source is endless.</summary>
    int Remaining { get; }
}

/// <summary>
/// The default: draw from a LetterSet's spawn weights forever. What Moves mode
/// plays on, and what Board falls back to when no mode installed anything.
/// </summary>
public class EndlessLetters : ILetterSource
{
    private readonly LetterSet letters;

    public EndlessLetters(LetterSet letters) => this.letters = letters;

    public int Remaining => -1;

    public void Reset() { }

    public bool TryDraw(out char letter)
    {
        letter = 'e';
        if (letters == null) return false;
        letter = letters.Draw();
        return true;
    }
}

/// <summary>
/// A finite bag of letter tiles, drawn from WITHOUT replacement — once the E's
/// are gone there are no more E's.
///
/// Built by reading a LetterSet's spawn weights as tile COUNTS, which is exactly
/// what they already are in LetterSet_Scrabble: nine A's, twelve E's, one Q,
/// 98 tiles in all. (Scrabble's other two are blanks, and this game has no
/// concept of a blank.) So the bag and the arcade modes' letter distribution
/// stay the same asset — rebalancing one rebalances both.
///
/// Running dry is the point, not a failure. The board's opening fill is paid for
/// out of the bag like everything else, so a 50-cell board has already spent
/// half of it before the first word.
/// </summary>
public class TileBag : ILetterSource
{
    private readonly LetterSet letters;
    private readonly int copies;
    private readonly List<char> remaining = new();

    /// <param name="copies">
    /// How many full copies of the distribution to pour in. 1 = a single
    /// Scrabble bag. The knob to turn when a round runs out of tiles too early.
    /// </param>
    public TileBag(LetterSet letters, int copies = 1)
    {
        this.letters = letters;
        this.copies = Mathf.Max(1, copies);
        Reset();
    }

    public int Remaining => remaining.Count;

    /// <summary>Tiles in a full bag — the denominator for anything showing how much is left.</summary>
    public int Capacity { get; private set; }

    public void Reset()
    {
        remaining.Clear();
        Capacity = 0;

        if (letters == null)
        {
            Debug.LogError("TileBag has no LetterSet, so it is empty and no tiles will spawn.");
            return;
        }

        foreach (var entry in letters.Entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.letter)) continue;
            char c = char.ToLowerInvariant(entry.letter[0]);
            int count = Mathf.Max(0, entry.weight) * copies;
            for (int i = 0; i < count; i++) remaining.Add(c);
        }

        Capacity = remaining.Count;
        if (Capacity == 0)
            Debug.LogError($"LetterSet '{letters.name}' has no positive weights, so the bag is empty.");
    }

    /// <summary>
    /// Pulls one tile out at random and keeps it out.
    ///
    /// Drawing at random beats shuffling once up front: Reset stays free, and
    /// tiles can be added mid-round (a shop, a relic) without re-shuffling.
    /// </summary>
    public bool TryDraw(out char letter)
    {
        letter = 'e';
        if (remaining.Count == 0) return false;

        int index = Random.Range(0, remaining.Count);
        letter = remaining[index];

        // Swap-remove: position in the bag means nothing, and RemoveAt would
        // shift up to 97 entries down on every single tile.
        remaining[index] = remaining[remaining.Count - 1];
        remaining.RemoveAt(remaining.Count - 1);
        return true;
    }
}

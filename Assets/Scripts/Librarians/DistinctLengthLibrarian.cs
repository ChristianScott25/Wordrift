using UnityEngine;

/// <summary>
/// THE CATALOGUER. One word per length, and no two words alike.
///
/// It reads the round's played words rather than counting anything of its own,
/// which is what keeps it stateless — and means it survives a save for free,
/// since the words played this round are already in the snapshot. Play a
/// four-letter word and every other four is closed for the round.
///
/// The pressure is that it tightens as you go, and it tightens from BOTH ends:
/// the easy short words run out first, and the board has to keep producing
/// lengths you haven't used.
/// </summary>
[CreateAssetMenu(fileName = "Librarian_DistinctLength",
                 menuName = "Word Crush/Librarian/Distinct Word Lengths")]
public class DistinctLengthLibrarian : Librarian
{
    public override string PowerText =>
        "Every word must be a different length from every word before it.";

    public override string Refuse(WordCheck check)
    {
        if (check.WordsThisRound == null) return null;

        foreach (var played in check.WordsThisRound)
            if (played != null && played.Length == check.Length)
                return $"Already played a {check.Length}-letter word.";

        return null;
    }
}

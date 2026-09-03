using UnityEngine;

/// <summary>
/// The first word you play sets a length, and every word after it has to match.
/// Play a three and the whole round is threes.
///
/// The exact inverse of DistinctLengthLibrarian, and stateless for the same
/// reason: it never has to remember which length was chosen, because under its
/// own rule every word already played has that length — so ANY of them answers
/// the question. That matters more than it looks. WordCheck.WordsThisRound is a
/// SET and has no order, so "the first word" isn't a thing this could read even
/// if it wanted to; "every word so far agrees" is, and it's the same rule.
///
/// 🎯 The choice is the first word, and it's made before you know what the board
/// will give you later — take the long word and you've promised to keep finding
/// long words.
/// </summary>
[CreateAssetMenu(fileName = "Librarian_Conformist",
                 menuName = "Word Crush/Librarian/Locked Length")]
public class LockedLengthLibrarian : Librarian
{
    public override string PowerText =>
        "Your first word sets the length. Every word after it must be the same length.";

    public override string Refuse(WordCheck check)
    {
        if (check.WordsThisRound == null) return null;

        foreach (var played in check.WordsThisRound)
        {
            if (played == null || played.Length == check.Length) continue;
            return $"Locked to {played.Length}-letter words this round.";
        }

        return null;
    }
}

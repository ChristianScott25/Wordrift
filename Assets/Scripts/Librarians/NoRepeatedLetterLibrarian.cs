using UnityEngine;

/// <summary>
/// No word may use the same letter twice. Within ONE word — a letter refused
/// here is free again in the next word, so this narrows every word without
/// spending anything.
///
/// Stateless and it doesn't even read the round: the whole rule is in the word
/// in front of it, which makes it the cheapest librarian there is.
///
/// 🎯 It takes out the words you reach for without thinking — plurals ending in
/// a letter already used, doubles like LETTER and BOOKS — so the board reads
/// differently rather than merely worse.
/// </summary>
[CreateAssetMenu(fileName = "Librarian_Abridged",
                 menuName = "Word Crush/Librarian/No Repeated Letter")]
public class NoRepeatedLetterLibrarian : Librarian
{
    public override string PowerText => "No word may use the same letter twice.";

    public override string Refuse(WordCheck check)
    {
        string word = check.Word;
        if (string.IsNullOrEmpty(word)) return null;

        // A nested scan rather than a HashSet: words are a handful of characters
        // and this runs on every selection change, so the allocation would cost
        // more than the comparisons do.
        for (int i = 0; i < word.Length; i++)
            for (int j = i + 1; j < word.Length; j++)
                if (word[i] == word[j])
                    return $"No repeated letters — {char.ToUpperInvariant(word[i])} twice.";

        return null;
    }
}

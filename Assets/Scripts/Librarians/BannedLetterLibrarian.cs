using UnityEngine;

/// <summary>
/// One letter is struck out for the round, and which one is rolled when the
/// round begins.
///
/// THE ONLY LIBRARIAN THAT CHOOSES SOMETHING, and the reason RoundRules carries
/// a stream and a note. The draw happens once, in Apply, from a stream keyed to
/// the round — so re-entering the round after a save re-derives exactly the same
/// letter with nothing written to disk. Drawing anywhere else (in Refuse, say)
/// would give a different answer on every keystroke.
///
/// It draws from RoundRules.LetterPool, which holds one entry per TILE in the
/// run's bag. So the roll is weighted by how common the letter actually is: a
/// banned Z would be a shrug, and this is much more likely to take an E.
///
/// The banner has to say WHICH letter, which is what PowerFor is for — PowerText
/// alone can only describe the rule in the abstract, and a boss whose rule you
/// can't read is just a bad round.
/// </summary>
[CreateAssetMenu(fileName = "Librarian_Censor",
                 menuName = "Word Crush/Librarian/Banned Letter")]
public class BannedLetterLibrarian : Librarian
{
    /// <summary>Only ever seen if the round somehow had no letters to draw from.</summary>
    public override string PowerText => "One letter is banned for the round.";

    public override string PowerFor(string note) =>
        string.IsNullOrEmpty(note)
            ? PowerText
            : $"The letter {note.ToUpperInvariant()} may not be used.";

    public override void Apply(RoundRules rules)
    {
        var pool = rules.LetterPool;
        if (rules.Rng == null || pool == null || pool.Count == 0) return;

        rules.Note = pool[rules.Rng.Range(0, pool.Count)].ToString();
    }

    public override string Refuse(WordCheck check)
    {
        if (string.IsNullOrEmpty(check.Note) || string.IsNullOrEmpty(check.Word)) return null;

        char banned = char.ToLowerInvariant(check.Note[0]);
        foreach (char letter in check.Word)
            if (char.ToLowerInvariant(letter) == banned)
                return $"{char.ToUpperInvariant(banned)} is banned this round.";

        return null;
    }
}

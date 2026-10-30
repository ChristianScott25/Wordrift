using UnityEngine;

/// <summary>
/// Adds to the MULT when the word ends with the same letters as the word
/// before it — CAT, HAT, BAT — and pays MORE the longer the streak runs.
///
/// The streak is worked out from the round's word list every time, never
/// counted on the asset: a bookmark is a read-only recipe, and a list the
/// session already saves means a resumed round keeps its streak for free.
///
/// The same word twice is NOT a rhyme, or Deja Vu spam would build a streak
/// with no skill in it. A word that doesn't rhyme breaks the chain, and the
/// list starts fresh every round, so the streak does too.
/// </summary>
[CreateAssetMenu(fileName = "Verse", menuName = "Word Crush/Bookmark/Rhyme")]
public class RhymeBookmark : Bookmark
{
    [Tooltip("How many letters at the end must match.")]
    [Min(1)] public int rhymeLetters = 2;

    [Tooltip("Mult per rhyming link in the streak: the 2nd word in a row gets " +
             "this, the 3rd twice this, and so on.")]
    [Min(0f)] public float multPerLink = 2f;

    public override void OnWordScored(ScoringContext ctx)
    {
        int links = 0;
        string later = ctx.Word;
        for (int back = 1; ; back++)
        {
            string earlier = ctx.WordBefore(back);
            if (!Rhymes(later, earlier)) break;
            links++;
            later = earlier;
        }

        if (links == 0) return;
        ctx.AddMult(links * multPerLink, displayName);
    }

    private bool Rhymes(string a, string b)
    {
        if (a == null || b == null || a == b) return false;
        if (a.Length < rhymeLetters || b.Length < rhymeLetters) return false;
        return string.CompareOrdinal(a, a.Length - rhymeLetters,
                                     b, b.Length - rhymeLetters, rhymeLetters) == 0;
    }
}

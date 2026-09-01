using UnityEngine;

/// <summary>
/// Multiplies the MULT when the word starts and ends with the same letter.
///
/// The multiplicative one of the three, because the condition is rare and hard
/// to engineer — and because a x2 landing after another bookmark's +Mult is
/// worth far more than before it, which is the ordering lesson this one teaches.
/// </summary>
[CreateAssetMenu(fileName = "Bookend", menuName = "Word Crush/Bookmark/Bookend")]
public class BookendBookmark : Bookmark
{
    [Min(1f)] public float multiplier = 2f;

    public override void OnWordScored(ScoringContext ctx)
    {
        string word = ctx.Word;
        if (string.IsNullOrEmpty(word) || word.Length < 2) return;
        if (word[0] != word[word.Length - 1]) return;

        ctx.MultiplyMult(multiplier, displayName);
    }
}

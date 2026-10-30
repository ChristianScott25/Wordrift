using UnityEngine;

/// <summary>
/// Multiplies the MULT when the word reads the same backwards — POP, LEVEL.
///
/// Multiplicative because the condition is rare: the board has to hand you a
/// mirror, and most of what it deals isn't one.
/// </summary>
[CreateAssetMenu(fileName = "LookingGlass", menuName = "Word Crush/Bookmark/Palindrome")]
public class PalindromeBookmark : Bookmark
{
    [Min(1f)] public float multiplier = 3f;

    public override void OnWordScored(ScoringContext ctx)
    {
        string word = ctx.Word;
        if (string.IsNullOrEmpty(word) || word.Length < 2) return;

        for (int i = 0, j = word.Length - 1; i < j; i++, j--)
            if (word[i] != word[j]) return;

        ctx.MultiplyMult(multiplier, displayName);
    }
}

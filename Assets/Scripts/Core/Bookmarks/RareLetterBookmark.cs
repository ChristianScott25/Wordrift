using UnityEngine;

/// <summary>
/// Multiplies the MULT when the word holds two or more rare letters, and
/// multiplies harder for every one past that.
///
/// Letters are counted in the SPELLED word, so a QU tile counts its Q and a
/// wild that became a Z counts as a Z. Repeats count too — two Zs is two.
///
/// 🎯 Strong on purpose: the 104-tile bag holds about one each of J, Q, X and
/// Z, so two in one word usually means the shop sold you some.
/// </summary>
[CreateAssetMenu(fileName = "Antiquarian", menuName = "Word Crush/Bookmark/Rare Letters")]
public class RareLetterBookmark : Bookmark
{
    [Tooltip("Letters that count as rare, lowercase.")]
    public string rareLetters = "jkqvxz";

    [Tooltip("How many rare letters the word needs before this fires.")]
    [Min(1)] public int minimumRare = 2;

    [Tooltip("x Mult at exactly the minimum.")]
    [Min(1f)] public float baseMultiplier = 2f;

    [Tooltip("Added to the multiplier for each rare letter past the minimum.")]
    [Min(0f)] public float extraPerLetter = 1f;

    public override void OnWordScored(ScoringContext ctx)
    {
        string word = ctx.Word;
        if (string.IsNullOrEmpty(word) || string.IsNullOrEmpty(rareLetters)) return;

        int rare = 0;
        foreach (char c in word)
            if (rareLetters.IndexOf(char.ToLowerInvariant(c)) >= 0) rare++;

        if (rare < minimumRare) return;
        ctx.MultiplyMult(baseMultiplier + (rare - minimumRare) * extraPerLetter, displayName);
    }
}

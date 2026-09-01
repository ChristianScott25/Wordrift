using UnityEngine;

/// <summary>
/// Adds to the MULT when the word holds more vowels than consonants. Y is NOT a
/// vowel here — it counts on the consonant side, so YOYO is 2 v 2 and doesn't
/// fire.
///
/// Additive rather than multiplicative on purpose: the condition is easy to hit
/// on short words, so a flat +Mult keeps it honest where a x2 would dominate.
/// It's also what gives the run something for a later x Mult bookmark to
/// multiply, which is where slot order starts to pay.
/// </summary>
[CreateAssetMenu(fileName = "VowelFanatic", menuName = "Word Crush/Bookmark/Vowel Fanatic")]
public class VowelFanaticBookmark : Bookmark
{
    [Min(0f)] public float multiplierBonus = 4f;

    [Tooltip("Letters that count as vowels. Y is deliberately absent.")]
    public string vowels = "aeiou";

    public override void OnWordScored(ScoringContext ctx)
    {
        string word = ctx.Word;
        if (string.IsNullOrEmpty(word)) return;

        int vowelCount = 0;
        foreach (char c in word)
            if (vowels.IndexOf(char.ToLowerInvariant(c)) >= 0) vowelCount++;

        // Strictly more, not "at least as many" — a 50/50 word shouldn't pay.
        if (vowelCount * 2 <= word.Length) return;

        ctx.AddMult(multiplierBonus, displayName);
    }
}

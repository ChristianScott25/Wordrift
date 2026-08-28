using UnityEngine;

/// <summary>
/// Doubles the word when it holds more vowels than consonants. Y is NOT a vowel
/// here — it counts on the consonant side, so YOYO is 2 v 2 and doesn't fire.
/// </summary>
[CreateAssetMenu(fileName = "VowelFanatic", menuName = "Word Crush/Bookmark/Vowel Fanatic")]
public class VowelFanaticBookmark : Bookmark
{
    [Min(1f)] public float multiplier = 2f;

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

        ctx.Mult *= multiplier;
    }
}

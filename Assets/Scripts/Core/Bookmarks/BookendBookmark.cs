using UnityEngine;

/// <summary>Doubles the word if it starts and ends with the same letter.</summary>
[CreateAssetMenu(fileName = "Bookend", menuName = "Word Crush/Bookmark/Bookend")]
public class BookendBookmark : Bookmark
{
    [Min(1f)] public float multiplier = 2f;

    public override void OnWordScored(ScoringContext ctx)
    {
        string word = ctx.Word;
        if (string.IsNullOrEmpty(word) || word.Length < 2) return;
        if (word[0] != word[word.Length - 1]) return;

        ctx.Mult *= multiplier;
    }
}

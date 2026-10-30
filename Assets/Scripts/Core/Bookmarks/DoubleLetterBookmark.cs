using UnityEngine;

/// <summary>
/// Adds to the MULT when the same letter appears twice side by side — the TT
/// in LETTER. Roughly Spine's opposite: Spine pays for no repeats at all.
///
/// Additive because doubles are common enough in English that a x Mult here
/// would fire on most long words.
/// </summary>
[CreateAssetMenu(fileName = "DoubleSpaced", menuName = "Word Crush/Bookmark/Double Letter")]
public class DoubleLetterBookmark : Bookmark
{
    [Min(0f)] public float multBonus = 4f;

    public override void OnWordScored(ScoringContext ctx)
    {
        string word = ctx.Word;
        if (string.IsNullOrEmpty(word)) return;

        for (int i = 1; i < word.Length; i++)
        {
            if (word[i] != word[i - 1]) continue;
            ctx.AddMult(multBonus, displayName);
            return;
        }
    }
}

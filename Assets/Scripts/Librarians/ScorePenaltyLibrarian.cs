using UnityEngine;

/// <summary>
/// Takes a cut of every word: both halves of the score, reduced by a percentage,
/// after every bookmark has had its say.
///
/// AFTER the bookmarks on purpose. It taxes what you built rather than what you
/// started with, so a run with good bookmarks loses more in absolute terms and
/// the same amount proportionally — which is the point of a percentage.
///
/// The first librarian to use the scoring hook, and the reason it exists. Note
/// that nothing here is previewed: the live POINTS x MULT readout shows the word
/// untaxed, and the cut lands as its own named beat in the walk-through after
/// ENTER. That's the same bargain bookmarks make.
///
/// 🎯 It changes nothing about which words are legal, so unlike every other
/// librarian you play the round exactly as you would have — and simply come up
/// short.
/// </summary>
[CreateAssetMenu(fileName = "Librarian_Critic",
                 menuName = "Word Crush/Librarian/Score Penalty")]
public class ScorePenaltyLibrarian : Librarian
{
    [Tooltip("How much of every word is taken away. 0.25 = a quarter off both " +
             "Points and Mult.")]
    [Range(0f, 0.9f)] public float penalty = 0.25f;

    [Tooltip("Neither number is ever reduced below this. 1 keeps a word worth " +
             "something however hard the cut is — and keeps Mult from collapsing " +
             "a score to nothing.")]
    [Min(0f)] public float floor = 1f;

    public override string PowerText =>
        $"Every word loses {Mathf.RoundToInt(penalty * 100f)}% of its Points and Mult.";

    public override void Score(ScoringContext ctx)
    {
        string who = Title;

        // Points is a whole number and the cut is rounded UP — a 10-point word
        // at 25% keeps 8, not 7. Kinder by half a point, and it means the number
        // shown never has a hidden fraction behind it.
        int keptPoints = Mathf.Max(
            Mathf.CeilToInt(floor),
            Mathf.CeilToInt(ctx.Points * (1f - penalty)));

        // AddPoints, not a direct write, so the walk-through can say who did it.
        // The delta is what's recorded, which is why it's computed rather than
        // assigned.
        if (keptPoints < ctx.Points) ctx.AddPoints(keptPoints - ctx.Points, who);

        // Mult is fractional by nature — the length curve deals in 1.5s — so it
        // is NOT rounded, only floored. Rounding it up to whole numbers would
        // quietly flatten the curve this is supposed to be taxing.
        if (ctx.Mult <= 0f) return;   // nothing to take, and no dividing by it

        float keptMult = Mathf.Max(floor, ctx.Mult * (1f - penalty));
        if (keptMult < ctx.Mult) ctx.MultiplyMult(keptMult / ctx.Mult, who);
    }
}

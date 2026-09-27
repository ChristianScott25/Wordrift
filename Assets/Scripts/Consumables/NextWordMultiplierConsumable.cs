using UnityEngine;

/// <summary>
/// The next word you play is worth more — after everything else has been counted.
///
/// ⚠️ IT IS AN IScoreRule ITSELF, exactly as a Librarian is. That is the whole
/// design: ScoreCalculator.Evaluate already ends with `roundRule?.Score(ctx)`,
/// so there is no new stage, no change to Evaluate, and RogueDemoMode simply
/// composes this with whatever librarian the round has.
///
/// Three things fall out of that, and all three are the reason:
///
///  - IT REALLY IS "AFTER EVERYTHING ELSE". The final score is Points x Mult, so
///    multiplying Mult multiplies the total, wherever in the chain it lands.
///  - IT SHOWS IN THE SCORE WALK-THROUGH FOR FREE. MultiplyMult records a
///    ScoreStep and ScoreTallyWidget replays every step, so the player watches a
///    "DOUBLER  x2 MULT" beat land after the bookmarks. Writing ctx.Mult
///    directly would score correctly and be invisible.
///  - IT IS SAFE UNDER SPECULATIVE EVALUATION. GameSession.ResolveSelection
///    calls Evaluate once PER CANDIDATE WORD when the chain holds a wild, so
///    Score may run many times for one word. It only writes into the context it
///    was handed, so that's harmless. ⚠️ THE CHARGE IS SPENT IN
///    RogueDemoMode.OnWordAccepted, which fires once, at the end of the tally —
///    never in here. A rule that consumed itself would burn on a word the player
///    never played.
///
/// And it can't change which letter a wild becomes: multiplying every candidate
/// by the same number leaves their order exactly as it was.
/// </summary>
[CreateAssetMenu(fileName = "Consumable_Doubler", menuName = "Word Crush/Consumable/Next Word Multiplier")]
public class NextWordMultiplierConsumable : Consumable, IScoreRule
{
    [Tooltip("What the next word's score is multiplied by. It lands on MULT, so " +
             "it multiplies the whole word — Points x Mult — not one half of it.")]
    [Min(1f)] public float multiplier = 2f;

    public override string PowerText =>
        $"x{ScoringContext.Trim(multiplier)} on the next word you play, " +
        "after everything else has been counted.";

    /// <summary>
    /// Arming is the whole effect: the round holds onto this until a word is
    /// accepted. False when there's no round to arm (between rounds, or on a
    /// mode that has no such notion), and the item is then not spent.
    /// </summary>
    public override bool Use(ConsumableUse use) =>
        use != null && use.Session != null && use.Session.ArmForNextWord(this);

    /// <summary>Runs after the bookmarks and the librarian. See the class comment.</summary>
    public void Score(ScoringContext ctx) => ctx?.MultiplyMult(multiplier, Title);
}

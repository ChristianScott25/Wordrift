/// <summary>
/// Something that gets a turn at a word's score, after the bookmarks and before
/// the total is taken. One slot, one method.
///
/// It exists so that ScoreCalculator can hand the round's own rule a turn
/// without Core learning what a librarian is — the same trick GameMode.Bookmarks
/// plays for a run. Anything that wants to intervene in scoring implements this
/// and gets handed the ScoringContext; it does NOT get a new stage in Evaluate.
///
/// Go through ctx.AddPoints / AddMult / MultiplyMult like a bookmark does, so
/// what you did shows up in the walk-through by name. Touching the fields
/// directly still scores correctly and vanishes from the readout.
/// </summary>
public interface IScoreRule
{
    void Score(ScoringContext ctx);
}

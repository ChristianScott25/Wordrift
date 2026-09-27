using System.Collections.Generic;

/// <summary>
/// Several scoring rules as one, run in the order they were added.
///
/// It exists because a round can now have more than one turn at a word's score —
/// the librarian, plus whatever consumables the player armed — and
/// ScoreCalculator.Evaluate takes exactly one IScoreRule. Composing here rather
/// than widening Evaluate keeps the scoring pipeline's shape: tiles, then
/// bookmarks, then the round, then the mode's multiplier.
///
/// ⚠️ IT IS REFILLED, NOT REBUILT. A mode holds one of these for its lifetime and
/// calls Clear/Add when what's active changes, because the thing that asks for it
/// is read on the way into Evaluate — which happens once per candidate word while
/// a finger is moving. A `new` here would allocate on that path.
///
/// ⚠️ ASK IsEmpty BEFORE HANDING IT OUT. GameSession decides whether a word needs
/// the expensive per-candidate wild resolution by testing whether the mode has a
/// score rule at all, so an empty composite that still isn't null would put every
/// selection frame on the slow path.
///
/// Like everything else at this stage it must only write into the ScoringContext
/// it is handed: Evaluate is called speculatively, once per candidate word.
/// </summary>
public sealed class CompositeScoreRule : IScoreRule
{
    private readonly List<IScoreRule> rules = new();

    public bool IsEmpty => rules.Count == 0;

    public int Count => rules.Count;

    public void Clear() => rules.Clear();

    /// <summary>Adds a rule, ignoring null so callers can pass an optional one straight in.</summary>
    public void Add(IScoreRule rule)
    {
        if (rule != null) rules.Add(rule);
    }

    public void Score(ScoringContext ctx)
    {
        // Indexed, not foreach: this runs once per candidate word on a chain
        // holding a wild, and a List enumerator is a struct that still costs
        // something to set up each time.
        for (int i = 0; i < rules.Count; i++) rules[i].Score(ctx);
    }
}

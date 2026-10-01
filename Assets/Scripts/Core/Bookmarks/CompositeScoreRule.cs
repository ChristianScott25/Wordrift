using System.Collections.Generic;

/// <summary>
/// Several scoring rules as one, run in the order they were added.
///
/// It exists because a round can have more than one turn at a word's score —
/// the librarian, plus whatever consumables the player armed — and
/// ScoreCalculator.Evaluate takes exactly one IScoreRule. Composing here rather
/// than widening Evaluate keeps the scoring pipeline's shape: tiles, then
/// bookmarks, then the round, then the mode's multiplier.
///
/// ⚠️ EACH RULE IS ADDED WITH WHAT KIND OF THING IT IS, and that is not
/// decoration. The walk-through shakes whatever produced a beat, and from in here
/// a librarian and an armed Doubler are both just an IScoreRule — only the mode
/// adding them knows which is which. The tag is stamped onto the context before
/// each one runs, so the step comes out pointing at the right thing on screen.
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

    // Parallel to `rules`, never a field on the rule itself: the same Consumable
    // asset can be armed twice and a Librarian is a shared authored asset, so
    // "what kind of thing is this, here, now" belongs to the list and not to the
    // object in it.
    private readonly List<ScoreActor> kinds = new();

    public bool IsEmpty => rules.Count == 0;

    public int Count => rules.Count;

    public void Clear()
    {
        rules.Clear();
        kinds.Clear();
    }

    /// <summary>
    /// Adds a rule, ignoring null so callers can pass an optional one straight in.
    /// </summary>
    /// <param name="kind">
    /// What the walk-through should shake when this one scores. The caller is the
    /// only thing that knows — see the warning on the class.
    /// </param>
    public void Add(IScoreRule rule, ScoreActor kind)
    {
        if (rule == null) return;
        rules.Add(rule);
        kinds.Add(kind);
    }

    public void Score(ScoringContext ctx)
    {
        // Indexed, not foreach: this runs once per candidate word on a chain
        // holding a wild, and a List enumerator is a struct that still costs
        // something to set up each time.
        for (int i = 0; i < rules.Count; i++)
        {
            ctx.Acting(kinds[i], rules[i]);
            rules[i].Score(ctx);
        }
    }
}

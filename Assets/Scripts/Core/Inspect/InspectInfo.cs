using System.Collections.Generic;

/// <summary>
/// What an info box says about one thing: a name, a sentence or two of what it
/// does, and its short qualities.
///
/// ⚠️ IT IS REFILLED, NEVER REBUILT. Inspector keeps one of these and hands the
/// same instance to whatever is being read, so no bundle and no list is made per
/// tap — the same bargain ModeStatus takes with its chip list. (A Describe that
/// builds a SENTENCE still allocates that sentence; TileSpec's does. That is one
/// string on a deliberate touch, not a per-frame cost, and it is the reason this
/// must never be moved onto a path that runs while a finger is moving.)
///
/// ⚠️ WIDEN THIS RATHER THAN ADDING A SECOND HOOK. IInspectable.Describe is the
/// only way anything says what it is, so a box that later wants an icon, a
/// rarity colour or a "you own 2" line grows a field here — exactly the deal
/// RoundRules, WordCheck, ScoringContext, RunPerks and ConsumableUse all take.
/// </summary>
public sealed class InspectInfo
{
    /// <summary>The name, drawn in the box's title bar.</summary>
    public string Title;

    /// <summary>What it does, in the player's words.</summary>
    public string Body;

    /// <summary>
    /// The short qualities drawn as chips along the bottom — "3 PTS", "2L",
    /// "WILD". Kept separate from Body because they are the part a player scans
    /// rather than reads, and because they are what will eventually be coloured.
    /// </summary>
    public readonly List<string> Tags = new();

    /// <summary>
    /// Extra lines under the description, drawn smaller and quieter — the small
    /// print a player wants only once they've read the rule ("Marked tiles can be
    /// discarded"). Keep each one a short sentence.
    /// </summary>
    public readonly List<string> Notes = new();

    /// <summary>
    /// One line about why this can't be used RIGHT NOW ("The Shusher won't allow
    /// items this round"), drawn last in a warning colour. Empty when there's
    /// nothing wrong — which is nearly always.
    /// </summary>
    public string Warning;

    /// <summary>Empties it, ready to be filled again. Keeps the lists' capacity.</summary>
    public void Clear()
    {
        Title = "";
        Body = "";
        Warning = "";
        Tags.Clear();
        Notes.Clear();
    }

    /// <summary>Adds a small-print line. Blank ones are dropped.</summary>
    public void Note(string note)
    {
        if (!string.IsNullOrWhiteSpace(note)) Notes.Add(note);
    }

    /// <summary>
    /// Adds a quality chip. Blank ones are dropped rather than drawn as an empty
    /// box, so a describer can offer one unconditionally.
    /// </summary>
    public void Tag(string tag)
    {
        if (!string.IsNullOrWhiteSpace(tag)) Tags.Add(tag);
    }
}

using UnityEngine;

/// <summary>
/// One round that plays by a different rule. Every few rounds a run meets one,
/// it warps something for that round only, and it pays extra for the trouble.
///
/// A librarian is an AUTHORED RECIPE, exactly like a Bookmark or a TileModifier:
/// read-only at runtime, no per-round state on it. Anything a librarian would
/// want to remember about the round in progress it reads from what it's handed
/// (see WordCheck) — that's what keeps two rounds against the same librarian
/// from bleeding into each other, and what keeps it out of the save file.
///
/// Two hooks, and they are the two MOMENTS a round has:
///
///   Apply(RoundRules)     — before the round starts. Change the allowances,
///                            and make any choice the round needs (rules.Rng).
///   Refuse(WordCheck)     — while the player is choosing. Rule words out.
///   Score(ScoringContext) — after the bookmarks. Change what a word is worth.
///
/// Those are the three moments a round has, and they are all of them. The next
/// lever a librarian wants is a FIELD on RoundRules or WordCheck, not a fourth
/// hook for the other librarians to ignore.
///
/// THE NAME IS A COSTUME. "Librarian" is what these are called today and it may
/// well become exams, or critics, or something else. Nothing user-facing is
/// hardcoded: the noun comes from RogueDemoModeConfig.librarianLabel, and each
/// character's name from displayName below. A rename is this file, its three
/// subclasses, and one Inspector string — not a hunt through the UI.
/// </summary>
public abstract class Librarian : ScriptableObject, IScoreRule
{
    [Tooltip("What this one is CALLED — 'The Grandiloquent'. Shown on the HUD for " +
             "the whole round. Free to change without touching what it does.")]
    public string displayName = "";

    [TextArea]
    [Tooltip("Optional. Overrides the description this librarian writes for itself. " +
             "Leave it empty and the HUD shows the one it derives from this asset's " +
             "own numbers (and from whatever the round rolled, for one that " +
             "chooses) — which is what stops the text going stale when you retune.")]
    public string powerOverride = "";

    /// <summary>
    /// What this librarian does, in the player's words, derived from its own
    /// settings. Derived rather than authored so that turning a number in the
    /// Inspector can't leave the HUD describing the old rule — the failure that
    /// a second, hand-written copy of a rule always eventually produces.
    /// </summary>
    public abstract string PowerText { get; }

    /// <summary>
    /// The same description, for a librarian whose rule isn't known until the
    /// round rolls it — "The letter E is banned" rather than "a letter is
    /// banned". The note is whatever Apply wrote into RoundRules.Note.
    ///
    /// Overriding this instead of PowerText is what keeps the variable rule and
    /// the fixed ones the same kind of thing; a librarian that doesn't choose
    /// anything never sees the note.
    /// </summary>
    public virtual string PowerFor(string note) => PowerText;

    /// <summary>
    /// What the HUD shows: the author's wording if there is one, else its own,
    /// told what this round chose. A method rather than a property because the
    /// answer depends on the round — the banner is built once in Begin.
    /// </summary>
    public string Power(string note) =>
        string.IsNullOrWhiteSpace(powerOverride) ? PowerFor(note) : powerOverride;

    /// <summary>The name to show, falling back to the asset's file name.</summary>
    public string Title => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

    /// <summary>
    /// A turn at the round's allowances, before the round starts. The mode fills
    /// RoundRules in from its config first, so a librarian is always editing the
    /// round that WOULD have been played.
    /// </summary>
    public virtual void Apply(RoundRules rules) { }

    /// <summary>
    /// Why this word can't be played, or null when it can — the default, which
    /// is what makes a librarian that only touches the allowances a one-method
    /// class.
    ///
    /// The string is shown to the player verbatim, so write it as a reason and
    /// not as an error. It's only ever asked about a word the dictionary has
    /// already accepted, so there's no need to guard against nonsense.
    /// </summary>
    public virtual string Refuse(WordCheck check) => null;

    /// <summary>
    /// A turn at the score, after every bookmark has had one. Empty by default,
    /// which is what makes a librarian that only rules words out a one-method
    /// class.
    ///
    /// Go through ctx.AddPoints / MultiplyMult rather than the fields, so the
    /// walk-through can name you — a librarian that quietly halves a score and
    /// doesn't appear in the readout is indistinguishable from a bug.
    /// </summary>
    public virtual void Score(ScoringContext ctx) { }
}

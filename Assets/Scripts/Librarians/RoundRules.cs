/// <summary>
/// The round's allowances, on their way from the mode config to the round, with
/// a stop at the librarian. The mode fills this in, hands it over, and reads
/// back whatever comes out — so a librarian never has to know where a number
/// came from, and the mode never has to ask whether there is a librarian.
///
/// Mutable and deliberately small. Widen it when a librarian needs a lever the
/// round already has (the score target, the board's refill policy), rather than
/// adding a second hook: one bundle means the next librarian is an asset, not a
/// change to every signature.
///
/// Nothing here is saved. It's rebuilt from the config and the round's
/// librarian in Begin, both of which the save already records — see
/// RunState.Librarian.
/// </summary>
public class RoundRules
{
    /// <summary>Words the player gets. RogueDemoModeConfig.moves by default.</summary>
    public int Moves;

    /// <summary>Tiles the player may throw away, in TILES not uses.</summary>
    public int Discards;

    /// <summary>
    /// What the round's score target is multiplied by. A FACTOR rather than a
    /// target, so a librarian never has to know what the run's curve says this
    /// round is worth — "three times whatever it would have been" keeps working
    /// on round 2 and on round 20.
    ///
    /// Multiply into it rather than assigning, so two of these could ever stack.
    /// </summary>
    public float TargetMultiplier = 1f;

    /// <summary>
    /// The round's own random stream, for a librarian that has to CHOOSE
    /// something — which letter to ban, which cell to close. Draw from it in
    /// Apply and only in Apply: it's keyed to the round, so a round re-entered
    /// after a save rebuilds exactly the same choice with nothing written to
    /// disk. Drawing anywhere else would give a different answer each time.
    /// </summary>
    public Rng Rng;

    /// <summary>
    /// The round's letters, ONE ENTRY PER TILE in the run's bag — so drawing
    /// from it uniformly is really drawing weighted by how common a letter
    /// actually is in this run. That's the point: a banned Z is a shrug, and a
    /// banned E is a boss.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<char> LetterPool;

    /// <summary>
    /// What the librarian decided, in whatever form suits it — the banned letter,
    /// today. Written in Apply, read back by the mode, and handed to both halves
    /// of the librarian that need it: PowerFor (so the banner says WHICH letter)
    /// and WordCheck.Note (so Refuse can enforce it).
    ///
    /// A string rather than a typed field because the alternative is one field
    /// per librarian on a class every librarian shares. Not saved — see Rng.
    /// </summary>
    public string Note = "";

    /// <summary>
    /// What clearing the round pays, as a multiple of the usual payout. The mode
    /// sets this to the config's librarian rate before handing the rules over, so
    /// a librarian that wants its own rate simply overwrites it — which is the
    /// seam for "this one is harder, so it pays more" later.
    /// </summary>
    public float PayoutMultiplier = 1f;
}

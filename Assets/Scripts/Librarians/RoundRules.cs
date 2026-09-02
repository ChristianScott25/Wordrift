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
    /// What clearing the round pays, as a multiple of the usual payout. The mode
    /// sets this to the config's librarian rate before handing the rules over, so
    /// a librarian that wants its own rate simply overwrites it — which is the
    /// seam for "this one is harder, so it pays more" later.
    /// </summary>
    public float PayoutMultiplier = 1f;
}

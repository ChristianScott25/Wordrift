using UnityEngine;

/// <summary>
/// The first roguelike round: reach a score target within a fixed number of
/// words, playing off a finite bag of letters.
///
/// The bag is the new idea. Every tile the board spawns comes out of it and
/// never goes back — the opening fill included — so a 50-cell board has already
/// spent half a Scrabble bag before the first word is played. When the bag runs
/// dry the board stops refilling and the round plays out on whatever is still
/// sitting there, which is what turns spending a rare letter into a decision.
///
/// The mode doesn't drive the board at all: no clock, no drip. It installs the
/// bag, then counts. Everything else is the shared loop.
/// </summary>
public class RogueDemoMode : GameMode
{
    private readonly RogueDemoModeConfig config;
    private TileBag bag;

    private int movesLeft;
    private int scored;

    public RogueDemoMode(RogueDemoModeConfig config) => this.config = config;

    public override void Attach(Board board)
    {
        base.Attach(board);

        // Attach is the last moment before the opening fill, and that fill draws
        // from the bag like everything else — so the bag has to exist by now.
        // The board resets it before every fill; we never touch it again.
        bag = new TileBag(config.letterSet, config.bagCopies);
        board.Letters = bag;
    }

    public override void Begin()
    {
        movesLeft = config.moves;
        scored = 0;
    }

    public override void OnWordAccepted(WordResult result)
    {
        movesLeft--;

        // The session owns the real score. This mode keeps its own running total
        // because GameMode has no handle on the session, and a target the mode
        // can't see is a target it can't judge.
        scored += result.Points;
    }

    public override void OnWordRejected(WordResult result)
    {
        if (config.rejectedWordsCostMoves) movesLeft--;
    }

    private bool TargetReached => scored >= config.targetScore;

    /// <summary>
    /// Nothing left to play with: the bag is empty, so no more tiles are coming,
    /// and what's on the board can't even reach the minimum word length.
    ///
    /// Without this the round can lock. Moves only tick down when a word is
    /// submitted, so a player who runs the bag dry and clears the board keeps
    /// their remaining moves forever with no way to spend them. Waiting out
    /// Resolving just lets the last clear finish before the panel appears.
    /// </summary>
    private bool OutOfTiles =>
        board != null && !board.Resolving &&
        bag != null && bag.Remaining == 0 &&
        board.TileCount < config.minWordLength;

    public override bool IsRoundOver =>
        movesLeft <= 0 || OutOfTiles || (config.endOnTargetReached && TargetReached);

    /// <summary>
    /// This mode can be passed or failed, so it says which. Reading TargetReached
    /// rather than remembering a flag means running out of moves ON the target
    /// still counts as a win.
    /// </summary>
    public override string Outcome =>
        TargetReached ? "TARGET REACHED" :
        OutOfTiles ? "OUT OF TILES" : "OUT OF MOVES";

    public override ModeStatus Status => new ModeStatus
    {
        Label = "MOVES",
        Value = Mathf.Max(0, movesLeft).ToString(),
        Fraction = config.moves > 0 ? (float)movesLeft / config.moves : 0f,
        Urgent = movesLeft <= config.urgentMoves && !TargetReached,

        // Target and bag share one string because the HUD has exactly one spare
        // slot. Giving each its own readout is the next HUD job, not this one.
        Goal = $"{scored} / {config.targetScore}   BAG {(bag != null ? bag.Remaining : 0)}",
    };
}

using UnityEngine;

/// <summary>
/// The roguelike round: reach the run's current score target within a fixed
/// number of words, playing off the run's finite bag of tiles.
///
/// The mode is one round's rules; the RUN lives in RunState. The mode finds
/// the run in Attach (starting one when there isn't one), plays a round
/// against its target, and in End either sends the session on to the shop
/// (cleared) or ends the run (failed). The bag belongs to the run — this
/// mode just drains a copy of it for the round.
///
/// The mode doesn't drive the board at all: no clock, no drip. It installs the
/// bag, then counts moves. Everything else is the shared loop.
/// </summary>
public class RogueDemoMode : GameMode
{
    private readonly RogueDemoModeConfig config;
    private RunState run;
    private TileBag bag;

    // The bag's stream, kept rather than just handed over: saving a round has to
    // record how far into it the round had drawn, and restoring one has to wind
    // a fresh one forward to match.
    private Rng bagRng;
    private int movesLeft;
    private int discardsLeft;

    // Built once per round: bookmarks can only change in the shop, and Status is
    // rebuilt every frame — no reason to re-join the same string 60 times a second.
    private string bookmarkLine = "";

    public RogueDemoMode(RogueDemoModeConfig config) => this.config = config;

    public override void Attach(GameSession session, Board board)
    {
        base.Attach(session, board);

        // Playing this mode with no run in progress — scene opened directly,
        // fresh from the menu, or restarting after a loss — starts one.
        // Mid-run, the existing run carries the round number and the bag.
        run = RunState.Current;
        if (run == null || run.Template != config) run = RunState.StartNew(config);

        // Attach is the last moment before the opening fill, and the fill
        // draws from the bag like everything else — so it has to exist by
        // now. The board resets it before every fill, which refills the copy
        // from the run's tiles: the full bag returns each round, and anything
        // a shop added is simply in it.
        // The bag draws from the run's seed, keyed to this round — so a seed
        // deals the same tiles, and this round's deal doesn't depend on how
        // many tiles were drawn in the rounds before it.
        bagRng = run.StreamFor(RunState.BagStream);
        bag = new TileBag(run.TileBag, bagRng);
        board.TileSource = bag;
    }

    public override void Begin()
    {
        movesLeft = config.moves;
        discardsLeft = config.discardsPerRound;
        bookmarkLine = BuildBookmarkLine();
    }

    /// <summary>
    /// A fresh allowance every round, never carried over — see Begin. Spending
    /// it costs no move on purpose: the move budget is for words, and a discard
    /// is what you do when the board won't give you one.
    /// </summary>
    public override int DiscardsLeft => discardsLeft;

    public override void OnTilesDiscarded(int tileCount) =>
        discardsLeft = Mathf.Max(0, discardsLeft - tileCount);

    /// <summary>The run's bookmarks, in the order they'll get to score.</summary>
    public override System.Collections.Generic.IReadOnlyList<BookmarkSpec> Bookmarks =>
        run?.Bookmarks;

    private string BuildBookmarkLine()
    {
        if (run == null || run.Bookmarks.Count == 0) return "";

        var names = new System.Text.StringBuilder();
        foreach (var owned in run.Bookmarks)
        {
            if (names.Length > 0) names.Append("  ·  ");
            names.Append(owned.Name.ToUpperInvariant());
        }
        return names.ToString();
    }

    /// <summary>
    /// The move counter, the discard allowance, and how far into the bag this
    /// round has already got. The bag is saved as INDICES into the run's tiles
    /// (see RunState.TileIndex) — a tile's identity is which entry of the bag it
    /// is, so that's the only thing that survives a save meaningfully.
    /// </summary>
    public override void CaptureRound(RoundSnapshot into)
    {
        into.movesLeft = movesLeft;
        into.discardsLeft = discardsLeft;
        into.bagDraws = bagRng == null ? 0 : bagRng.Draws;

        if (run == null || bag == null) return;

        // Order is part of the answer, not just membership: TryDraw indexes into
        // the bag, so the same stream over a reordered bag deals differently.
        var index = run.TileIndex();
        foreach (var tile in bag.RemainingTiles)
            if (index.TryGetValue(tile, out int i)) into.bagRemaining.Add(i);
    }

    public override void RestoreRound(RoundSnapshot from)
    {
        movesLeft = from.movesLeft;
        discardsLeft = from.discardsLeft;

        if (run == null || bag == null) return;

        // Board.Build has already dealt an opening hand out of this bag; the
        // saved round's draw replaces it wholesale. Tiles in neither the restored
        // bag nor the restored board are the ones already played or discarded —
        // gone for this round, back next round, exactly as if they'd just been played.
        var tiles = new System.Collections.Generic.List<TileSpec>(from.bagRemaining.Count);
        foreach (int i in from.bagRemaining)
        {
            var tile = run.TileAt(i);
            if (tile != null) tiles.Add(tile);
        }

        // A fresh stream starts over, and Board.Build has just spent an arbitrary
        // number of its draws on an opening hand that's about to be thrown away.
        // Winding a new one forward to the saved position is what makes the tile
        // that falls next the tile that WOULD have fallen next.
        bagRng = run.StreamFor(RunState.BagStream);
        bagRng.Skip(from.bagDraws);
        bag.RestoreRemaining(tiles, bagRng);
    }

    public override void OnWordAccepted(WordResult result) => movesLeft--;

    public override void OnWordRejected(WordResult result)
    {
        if (config.rejectedWordsCostMoves) movesLeft--;
    }

    // The session owns the score, the run owns the target; this just compares.
    private bool TargetReached => session.Score >= run.TargetScore;

    /// <summary>
    /// Nothing left to play with: the bag is empty, so no more tiles are
    /// coming, and what's on the board can't even reach the minimum word
    /// length.
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

    public override void End()
    {
        if (TargetReached)
        {
            // Paid before the shop loads, because the shop reads the balance in
            // Start. movesLeft is already decremented for the winning word, so
            // clearing on move 6 of 20 correctly banks 14 unused moves.
            run.AddMoney(config.RewardFor(session.Score, Mathf.Max(0, movesLeft)));

            // Cleared: the run continues in the shop, and the panel is skipped.
            // The shop advances the round when the player leaves it, so it can
            // still talk about the round that was just cleared.
            session.ContinueTo(config.shopSceneName);
        }
        else
        {
            // The run died with this round. Ending it now is what makes the
            // panel's PLAY AGAIN start over: Restart re-attaches, finds no run,
            // and builds a fresh one at round 1 with a stock bag.
            RunState.End();
        }
    }

    /// <summary>
    /// This mode can be passed or failed, so it says which. Reading
    /// TargetReached rather than remembering a flag means running out of moves
    /// ON the target still counts as a win.
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

        // Round, target, bag and money share one string because the HUD has
        // exactly one spare slot, and it's now four readouts wide — "R2" not
        // "ROUND 2" so it still fits. Money can't change mid-round; it's here
        // so the player can plan the next shop. A real multi-readout HUD is
        // overdue, but it's a HUD job: wire StatusWidget.goalLabel.
        Goal = $"R{run.Round}   {session.Score} / {run.TargetScore}   " +
               $"BAG {bag.Remaining}   ${run.Money}",

        // Whatever bookmarks the run is carrying. Drawn on its own HUD line, or
        // dropped entirely if the widget has no label for it.
        Extra = bookmarkLine,
    };
}

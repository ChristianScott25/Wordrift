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

    /// <summary>
    /// The round's rule-warper, or null on an ordinary round. The RUN decides
    /// which one and when (see RunState.PickLibrarian) — the mode only plays the
    /// round it's given, which is what keeps "every third round" a run-level
    /// rule that a boss-round ordering change won't have to chase through here.
    /// </summary>
    private Librarian librarian;

    // This round's allowances after the librarian has had its say. Kept rather
    // than read back into loose fields because End needs the payout rate off it.
    private RoundRules rules = new RoundRules();

    // Built once per round: bookmarks can only change in the shop, and Status is
    // rebuilt every frame — no reason to re-join the same string 60 times a second.
    private string bookmarkLine = "";

    // Same again for the librarian's name and power, which are fixed for the round.
    private string librarianBanner = "";

    // What this round's librarian chose for itself, if it chooses anything — the
    // banned letter, today. Rebuilt in Begin from a round-keyed stream, so it is
    // the same choice every time this round is entered and there is nothing to
    // save. Stamped onto every WordCheck on the way past.
    private string librarianNote = "";

    // What THIS round asks for: the run's target through the librarian's factor.
    // Held rather than recomputed so the HUD and the win check read one number.
    //
    // Starts at MaxValue, not 0, so a round that somehow reached Update without
    // Begin fails safe — an unset 0 would make "score >= target" true on frame
    // one and clear the round before a word was played.
    private int roundTarget = int.MaxValue;

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
        librarian = run?.Librarian;

        // The round the config describes, offered to the librarian, then read
        // back. A librarian never sees a raw config value and the mode never
        // asks whether there is a librarian — the only branch is the payout
        // rate, which has to be seeded before Apply so a librarian can overrule it.
        rules = new RoundRules
        {
            Moves = config.moves,
            Discards = config.discardsPerRound,
            PayoutMultiplier = librarian != null ? config.librarianPayoutMultiplier : 1f,

            // Taken fresh and drawn from only inside Apply. It's keyed to the
            // round, so re-entering the round after a save re-derives the same
            // choice — which is why a librarian's pick needs no save support.
            Rng = run?.StreamFor(RunState.LibrarianRoundStream),
            LetterPool = BuildLetterPool(),
        };
        librarian?.Apply(rules);
        librarianNote = rules.Note ?? "";

        movesLeft = rules.Moves;
        discardsLeft = rules.Discards;

        // Computed once, here, for the same reason the payout rate is read off
        // the rules in End: the librarian has had its say and nothing after this
        // point should be asking the run for a raw target.
        roundTarget = run == null
            ? int.MaxValue
            : ScoreLimits.Clamp((double)run.TargetScore * rules.TargetMultiplier);
        bookmarkLine = BuildBookmarkLine();
        librarianBanner = BuildLibrarianBanner();
    }

    /// <summary>
    /// The round's extra word rule, if it has one. Straight through to the
    /// librarian — the mode adds nothing of its own, so a round with none is a
    /// null check and not a code path.
    /// </summary>
    public override string Refuse(WordCheck check)
    {
        if (librarian == null) return null;

        // WordCheck is a struct, so this stamps the mode's copy and nothing
        // else. It's how the round's choice reaches the librarian without
        // GameSession — which builds the check — having to know one exists.
        check.Note = librarianNote;
        return librarian.Refuse(check);
    }

    /// <summary>
    /// The round's turn at the score, after every bookmark. The librarian itself
    /// — most of them don't override Score, and an unoverridden one costs a
    /// virtual call per word.
    /// </summary>
    public override IScoreRule ScoreRule => librarian == null ? null : librarian;

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

    /// <summary>
    /// "LIBRARIAN — THE GRANDILOQUENT" over what it does. Both halves are
    /// authored (the noun on the config, the name and power on the asset), so
    /// renaming librarians to exams never touches this method.
    /// </summary>
    private string BuildLibrarianBanner()
    {
        if (librarian == null) return "";

        string label = string.IsNullOrWhiteSpace(config.librarianLabel)
            ? librarian.Title.ToUpperInvariant()
            : $"{config.librarianLabel.ToUpperInvariant()} — {librarian.Title.ToUpperInvariant()}";

        string power = librarian.Power(librarianNote);
        return string.IsNullOrWhiteSpace(power) ? label : $"{label}\n<size=80%>{power}</size>";
    }

    /// <summary>
    /// Every letter in the run's bag, one entry per TILE — so a librarian
    /// drawing from it uniformly is really drawing weighted by how common the
    /// letter is in this particular run. Built from the run's bag rather than
    /// the letter catalog because the bag is what the player actually owns, and
    /// the shop has been editing it.
    /// </summary>
    private System.Collections.Generic.IReadOnlyList<char> BuildLetterPool()
    {
        var letters = new System.Collections.Generic.List<char>();
        if (run == null) return letters;

        foreach (var tile in run.TileBag)
            if (tile != null) letters.Add(tile.Letter);
        return letters;
    }

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

    // The session owns the score, the run owns the CURVE; roundTarget is what
    // this round actually asks for once the librarian has had its say.
    private bool TargetReached => session.Score >= roundTarget;

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
            run.AddMoney(config.RewardFor(session.Score, Mathf.Max(0, movesLeft),
                                          rules.PayoutMultiplier));

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
        Goal = $"R{run.Round}   {session.Score} / {roundTarget}   " +
               $"BAG {bag.Remaining}   ${run.Money}",

        // Whatever bookmarks the run is carrying. Drawn on its own HUD line, or
        // dropped entirely if the widget has no label for it.
        Extra = bookmarkLine,

        // Empty on an ordinary round, so the line simply isn't there.
        Banner = librarianBanner,
    };
}

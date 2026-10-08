using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs one round. Owns the loop every mode shares — select, validate, score,
/// demolish, refill — and delegates anything mode-specific to a GameMode.
///
/// Selecting tiles and COMMITTING to them are separate steps: ChainController
/// only ever reports a selection, and SubmitSelection / DiscardSelection are
/// what act on it. Both are public because the HUD's buttons call them.
///
/// It deliberately does NOT know what a timer is. Add modes, not branches.
/// </summary>
public class GameSession : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private Board board;
    [SerializeField] private ChainController chainController;
    [SerializeField] private Camera sceneCamera;

    [Header("Content")]
    [Tooltip("The imported lexicon. Plain text, one lowercase word per line, " +
             "'#' starts a comment. Kept exactly as imported — swap this whole " +
             "asset to change dictionaries, and put additions in the extra list.")]
    [SerializeField] private TextAsset wordList;

    [Tooltip("Words the imported list is missing, hand-edited and alphabetical. " +
             "Optional: with none assigned the game plays on the imported list " +
             "alone. Unioned with it, so a word in either one counts.")]
    [SerializeField] private TextAsset extraWordList;

    [Tooltip("Words the game refuses even though the imported list has them — " +
             "the slurs a 2006 Scrabble lexicon still carries. Applied LAST, " +
             "so a word here is refused whatever the other two lists say.")]
    [SerializeField] private TextAsset blockedWordList;

    [Tooltip("Used when this scene is played directly. The main menu overrides it.")]
    [SerializeField] private ModeConfig fallbackMode;

    [Tooltip("Divides the screen into bands and frames the camera onto the " +
             "board's. Wired by Word Crush/Set Up Game Layout.")]
    [SerializeField] private GameLayout layout;

    public ModeConfig Config { get; private set; }
    public int Score { get; private set; }
    public bool IsPlaying { get; private set; }

    private GameMode mode;
    private WordValidator validator;
    private ScoreCalculator scorer;

    private int wordsFound;
    private string bestWord = "";
    private int bestWordPoints;

    // True while a scored word is being walked through on the HUD — its tiles
    // flying up and the board refilling underneath. Input is off; the round
    // must not end mid-tally, and no save may be written.
    private bool tallying;

    // Every word accepted this round. Bookmarks read it to spot a repeat, so it
    // has to be cleared per round and written AFTER the word has been scored.
    private readonly HashSet<string> wordsThisRound = new();

    private void Awake()
    {
        Config = ModeSelection.Take() ?? fallbackMode;
        if (Config == null)
        {
            Debug.LogError("GameSession has no mode config assigned.", this);
            enabled = false;
            return;
        }

        if (sceneCamera == null) sceneCamera = Camera.main;

        validator = new WordValidator(wordList, extraWordList, blockedWordList);
        scorer = new ScoreCalculator(Config);
        mode = Config.CreateMode();

        // Attach first: the mode may swap the board's refill, gravity, or tile
        // source, and Build performs the opening fill through whatever is installed.
        mode.Attach(this, board);
        board.Build(Config.boardShape, Config.letterSet,
                    Config.tileSkins, Config.letterFont);

        // ⚠️ AFTER Build, BEFORE any widget's Start. Resolve reads BoardSize, so
        // it cannot run before the board exists; and every widget places itself
        // inside a band, so it must run before they do. Awake is the one window
        // that satisfies both — every OnEnable and every Start come after it.
        EnsureLayout();
        if (layout != null) layout.Resolve();

        chainController.Init(board, sceneCamera);
        chainController.ChainChanged += OnChainChanged;
        chainController.ChainSubmitted += OnChainSubmitted;
    }

    /// <summary>
    /// The bookmark row lets the player reorder mid-round, and that order is the
    /// scoring order — so it has to reach the save file, or a resumed run comes
    /// back playing by an order the player didn't choose.
    ///
    /// RequestSave rather than SaveRun: a drag can land at any moment, including
    /// while a cleared word's tiles are still falling, and the whole point of the
    /// queue is that the file is never written with the board mid-collapse.
    /// </summary>
    private void OnEnable() => RunState.Changed += RequestSave;

    private void OnDisable() => RunState.Changed -= RequestSave;

    private void OnDestroy()
    {
        if (chainController == null) return;
        chainController.ChainChanged -= OnChainChanged;
        chainController.ChainSubmitted -= OnChainSubmitted;
    }

    // Start (not Awake) so every HUD widget has subscribed before the first events fire.
    private void Start() => StartRound();

    public void StartRound()
    {
        leavingScene = false;
        tallying = false;
        saveDirty = false;
        Score = 0;
        wordsFound = 0;
        bestWord = "";
        bestWordPoints = 0;
        wordsThisRound.Clear();

        mode.Begin();

        // A round that was interrupted comes back exactly as it was, on top of
        // the fresh one Begin just set up. It has to run here rather than in
        // Awake: Begin hands out this round's allowances, so restoring before it
        // would be overwritten by it.
        RestoreSavedRound();

        IsPlaying = true;
        chainController.InputEnabled = true;

        // Every widget hears the restored numbers on its first frame, because
        // these fire after the restore rather than before it.
        GameEvents.RaiseRoundStarted();
        GameEvents.RaiseScoreChanged(Score);
        GameEvents.RaiseStatusChanged(mode.Status);
        RaiseSelection();

        SaveRun();
    }

    /// <summary>
    /// Puts an interrupted round back: the bag first, then the board, then the
    /// session's own bookkeeping. Does nothing at all for a normal round start,
    /// which is every start but the one right after CONTINUE.
    ///
    /// The board has already been built and filled by now, out of the bag. That
    /// opening hand is simply thrown away — a couple of dozen tiles instantiated
    /// and destroyed once per resume, in exchange for Board.Build not needing to
    /// know that resuming exists.
    /// </summary>
    private void RestoreSavedRound()
    {
        var saved = RunState.TakePendingRound();
        if (saved == null || !saved.captured) return;

        var run = RunState.Current;
        if (run == null) return;

        // The bag before the board: the mode refills the draw from the save, and
        // Board.Restore then places tiles without drawing anything at all.
        mode.RestoreRound(saved);

        var layout = new Dictionary<Vector2Int, TileSpec>(saved.boardTile.Count);
        int cells = Mathf.Min(saved.boardTile.Count,
                              Mathf.Min(saved.boardCellX.Count, saved.boardCellY.Count));
        for (int i = 0; i < cells; i++)
        {
            var spec = run.TileAt(saved.boardTile[i]);
            if (spec != null) layout[new Vector2Int(saved.boardCellX[i], saved.boardCellY[i])] = spec;
        }
        board.Restore(layout);

        // Clamped on the way in as well as on the way up: an older save could
        // hold a score written before the overflow was fixed, and resuming a
        // negative one would keep it negative for the rest of the round.
        Score = ScoreLimits.Clamp((long)saved.score);
        wordsFound = saved.wordsFound;
        bestWord = saved.bestWord ?? "";
        bestWordPoints = saved.bestWordPoints;
        foreach (var word in saved.wordsThisRound) wordsThisRound.Add(word);
    }

    /// <summary>Replays the same mode without reloading the scene.</summary>
    public void Restart()
    {
        // A fresh rule object every time, so Restart works exactly like a scene
        // load: Attach runs again and no round state can leak between plays.
        // For a run that just died this is also what starts the NEW run —
        // Attach finds RunState.Current empty and builds one from scratch.
        mode = Config.CreateMode();
        mode.Attach(this, board);
        board.ResetBoard();
        StartRound();
    }

    private void Update()
    {
        if (!IsPlaying) return;

        mode.Tick(Time.deltaTime);
        GameEvents.RaiseStatusChanged(mode.Status);

        FlushSaveWhenSettled();

        // Not while a word is still being tallied — the move is already spent,
        // so this would otherwise cut the last word's score off mid-count.
        if (!tallying && mode.IsRoundOver) EndRound();
    }

    private void EndRound()
    {
        IsPlaying = false;

        // Drop any queued save. The round is over, so what happens next owns the
        // save file: a cleared round is the shop's to write, and a failed one
        // ends the run, which deletes it. A stale in-round snapshot landing after
        // either would undo them.
        saveDirty = false;

        chainController.InputEnabled = false;
        chainController.CancelChain();

        // The mode may claim the ending (advance the run, head for the shop).
        // If it did, the next scene IS the ending — no game-over panel.
        mode.End();
        if (leavingScene) return;

        GameEvents.RaiseRoundEnded(new RoundSummary
        {
            Score = Score,
            WordsFound = wordsFound,
            BestWord = bestWord,
            BestWordPoints = bestWordPoints,
            Headline = mode.Outcome,
        });
    }

    private void OnChainChanged(IReadOnlyList<Tile> chain) => RaiseSelection();

    /// <summary>
    /// Publishes what's selected and what may be done with it. The dictionary
    /// and the mode are both consulted HERE, once — the buttons only obey the
    /// answer, so a rule change can't leave a button offering something the
    /// session would refuse.
    /// </summary>
    private void RaiseSelection()
    {
        var chain = chainController.Selection;

        // Resolve first: with a wild or a choice tile in the chain the word isn't
        // decided until the dictionary, the round's rule and the scorer have all
        // had a say. Without one this is exactly the two questions it always was.
        var resolved = ResolveSelection(chain);
        ShowResolvedLetters(chain, resolved);

        bool isWord = resolved.IsWord;
        string refused = resolved.Refused;

        GameEvents.RaiseSelectionChanged(new SelectionState
        {
            Word = resolved.Word,
            TileCount = chain.Count,

            // Borrowed for the duration of the callback — see SelectionState.
            // The word row draws copies of these so its tiles carry the same
            // faces, score corners and badges the board's are carrying.
            Tiles = chain,
            CanSubmit = IsPlaying && isWord && refused == null,
            RefusedReason = refused,
            CanDiscard = IsPlaying && mode.CanDiscard(chain.Count),
            DiscardsLeft = mode.DiscardsLeft,

            // ⚠️ The word's LENGTH and nothing else — NOT what pressing PLAY
            // pays. The tiles arriving is the walk-through's whole job, so
            // counting them here would give the ending away first. It is the
            // same call the walk starts from, so the numbers don't jump.
            Preview = scorer.Opening(chain),
        });
    }

    /// <summary>
    /// Finds the layout, and stands one up if the scene hasn't been set up yet.
    ///
    /// ⚠️ Without this a missing GameLayout doesn't read as a missing GameLayout.
    /// The camera never gets framed and every widget keeps whatever position it
    /// was last authored at, so the screen looks like a layout that was built
    /// wrong rather than one that was never built. GameLayout carries its own
    /// band table and finds its own board and camera, so standing one up gets
    /// the bands and the framing right; what stays missing is the widgets the
    /// editor script creates and wires, which is a much smaller and much more
    /// legible hole.
    /// </summary>
    private void EnsureLayout()
    {
        if (layout == null) layout = FindFirstObjectByType<GameLayout>(FindObjectsInactive.Include);
        if (layout != null) return;

        var canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null)
        {
            Debug.LogError("GameSession found no Canvas and no GameLayout — the board " +
                           "cannot be framed. Run Word Crush/Set Up Game Layout.", this);
            return;
        }

        layout = canvas.gameObject.AddComponent<GameLayout>();
        Debug.LogWarning("No GameLayout in the scene, so one was created at runtime. The " +
                         "bands and the camera will be right, but the widgets that only " +
                         "the editor script builds (round header, tile bag, items, system " +
                         "buttons) are missing and the rest are unwired. " +
                         "Run Word Crush/Set Up Game Layout.", this);
    }

    /// <summary>
    /// Plays the selected tiles as a word. Wired to the ENTER button — the only
    /// way a word is submitted now that lifting the finger doesn't do it.
    /// </summary>
    public void SubmitSelection()
    {
        if (!IsPlaying) return;
        chainController.Submit();
    }

    /// <summary>
    /// Throws the selected tiles off the board without scoring them. They are
    /// spent exactly like played tiles: gone for this round, back in the bag
    /// next round, and never returned to the draw mid-round.
    ///
    /// Costs no move — the allowance it spends is the mode's, not the move
    /// counter's.
    /// </summary>
    public void DiscardSelection()
    {
        if (!IsPlaying) return;

        // Ask the mode, not the button: the button may be a frame stale, and
        // this is the call site that actually takes the allowance.
        int count = chainController.Selection.Count;
        if (!mode.CanDiscard(count)) return;

        var discarded = chainController.TakeSelection();
        mode.OnTilesDiscarded(discarded.Count);
        board.RemoveTiles(discarded);

        RaiseSelection();
        GameEvents.RaiseStatusChanged(mode.Status);
        RequestSave();

        // Discarding can empty a bag-limited board, so the round may be over.
        // OutOfTiles waits out the resolve, so this normally lands in Update.
        if (mode.IsRoundOver) EndRound();
    }

    // ---- Consumables --------------------------------------------------------

    /// <summary>
    /// Whether an item could be spent right now. THE one place that question is
    /// answered — the items area obeys it and refuses to pick an item up at all,
    /// exactly as the PLAY button obeys SelectionState.CanSubmit rather than
    /// re-deriving it.
    ///
    /// The three refusals are the same three that stop a save being written: a
    /// board mid-fall or mid-tally is one whose tile map is about to change under
    /// whatever the item does to it.
    /// </summary>
    public bool CanUseConsumable => IsAtRest;

    /// <summary>
    /// The round is live and nothing is moving: no score walk, no fall, no
    /// clear waiting to compact. The same three conditions that gate a save,
    /// which is why both spending an item and leaving mid-round ask it.
    ///
    /// ⚠️ !mode.IsRoundOver as well as IsPlaying: the round is over the moment
    /// the mode says so, but IsPlaying only flips when Update next calls
    /// EndRound. A UI tap handled before GameSession.Update in that frame would
    /// otherwise see a finished round as live — and LeaveToMenu would save it.
    /// </summary>
    public bool IsAtRest =>
        IsPlaying && mode != null && !mode.IsRoundOver &&
        !tallying && board != null && !board.Busy && !board.Resolving;

    /// <summary>
    /// What this round has left to draw — for the bag view, which is the only
    /// thing that shows it. Null before the mode has a bag, and null for a mode
    /// that has none.
    ///
    /// A pass-through rather than a widget reaching for Board.TileSource and
    /// casting it: which SOURCE a round draws from is the mode's business, and
    /// the UI knowing it happens to be a TileBag would be the UI knowing one
    /// mode's implementation.
    ///
    /// ⚠️ LIVE, NOT A SNAPSHOT — see GameMode.RemainingTiles. Take what you need
    /// and let go of it.
    /// </summary>
    public IReadOnlyList<TileSpec> RemainingTiles => mode?.RemainingTiles;

    /// <summary>
    /// Spends the item in a slot. THE ONE PLACE AN ITEM LEAVES THE RUN.
    ///
    /// ⚠️ IT SPENDS ONLY IF Use RETURNED TRUE. That is the whole contract with
    /// Consumable.Use, and it is what will make a future pick-a-target flow safe:
    /// a player who changes their mind is just a Use that never ran.
    ///
    /// Returns whether the item was actually spent, so the caller knows whether
    /// to close its panel.
    /// </summary>
    /// <param name="screenPoint">Where the item was dropped. An item is played by
    /// dragging it onto the board, so a drop anywhere else is a change of mind
    /// and must cost nothing.</param>
    public bool UseConsumable(int index, Vector2 screenPoint)
    {
        if (!CanUseConsumable) return false;

        var run = RunState.Current;
        if (run == null || index < 0 || index >= run.Consumables.Count) return false;

        var consumable = run.Consumables[index];
        if (consumable == null) return false;

        // Where it landed. An item that names a target needs a TILE under the
        // finger; one that doesn't just needs to be somewhere on the board.
        //
        // Both refusals happen BEFORE anything is cleared or spent, which is
        // what makes "drag it out and change your mind" free — the same
        // guarantee Use returning false gives, arriving one step earlier.
        Tile target = null;
        if (consumable.Targets == ConsumableTarget.BoardTile)
        {
            target = TileAtScreen(screenPoint);
            if (target == null) return false;
        }
        else if (!IsOverBoard(screenPoint))
        {
            return false;
        }

        // Before the item runs, not after — and it has to be, because a shuffle
        // moves the tiles out from under the chain synchronously. Leaving it
        // would keep the selected tiles tinted, no longer next to each other,
        // and spelling something else.
        //
        // The cost is that an item which then REFUSES has cleared the selection
        // for nothing. That is only reachable on a board with fewer than two
        // tiles (Board.Shuffle rules out every other refusal), which is a board
        // with no selection worth keeping.
        chainController.CancelChain();

        bool used = consumable.Use(new ConsumableUse
        {
            Run = run,
            Session = this,
            Board = board,
            Rng = mode.ConsumableRng,
            Tile = target,
            Cell = target == null ? default : target.Cell,
        });

        if (!used) { RaiseSelection(); return false; }

        // Raises RunState.Changed, which queues the save — the board may be
        // sliding, and FlushSaveWhenSettled holds it until it isn't.
        run.SpendConsumable(index);

        RaiseSelection();
        GameEvents.RaiseStatusChanged(mode.Status);
        return true;
    }

    /// <summary>
    /// Is this screen point over the board?
    ///
    /// The drop zone for an item that targets nothing in particular. It lives
    /// here rather than on the widget because the widget has no camera and no
    /// board, and giving it either would be a second place that knows how to
    /// turn a finger into a place on the board — which is exactly what
    /// ChainController already is.
    ///
    /// Deliberately the board's OWN rectangle with no slack: a generous margin
    /// would make a drop just outside it play the item, and an item is something
    /// the player paid for.
    /// </summary>
    public bool IsOverBoard(Vector2 screenPoint)
    {
        if (board == null || sceneCamera == null) return false;

        Vector3 world = sceneCamera.ScreenToWorldPoint(screenPoint);
        Vector2 size = board.BoardSize;
        Vector2 centre = board.BoardCenter;

        return Mathf.Abs(world.x - centre.x) <= size.x * 0.5f &&
               Mathf.Abs(world.y - centre.y) <= size.y * 0.5f;
    }

    /// <summary>
    /// The tile under a screen point, or null. Board.TileAt refuses tiles that
    /// aren't settled, so an item cannot be dropped onto one mid-slide.
    /// </summary>
    public Tile TileAtScreen(Vector2 screenPoint)
    {
        if (board == null || sceneCamera == null) return null;

        Vector3 world = sceneCamera.ScreenToWorldPoint(screenPoint);
        world.z = 0f;
        return board.TileAt(world);
    }

    /// <summary>
    /// Hands a consumable to the round to hold until a word is accepted. Pure
    /// pass-through: the round owns armed state because it dies with the round.
    /// </summary>
    public bool ArmForNextWord(Consumable consumable) =>
        mode != null && mode.ArmForNextWord(consumable);

    private void OnChainSubmitted(IReadOnlyList<Tile> chain)
    {
        if (!IsPlaying || chain.Count == 0) return;

        // Resolved again rather than carried over from the preview. Safe, and
        // deliberately so: the rule draws no randomness and wordsThisRound only
        // grows AFTER Evaluate, so the same chain resolves to the same word — a
        // cached answer could go stale, this one cannot disagree.
        var resolved = ResolveSelection(chain);
        string word = resolved.Word;

        // Unreachable through the ENTER button, which disables itself on an
        // invalid word — kept because the submit path is public and the rule
        // that a bad word costs nothing shouldn't live only in a button.
        if (!resolved.IsWord)
        {
            var rejected = ScoreCalculator.Rejected(word, chain.Count);
            foreach (var tile in chain) tile.FlashInvalid();
            mode.OnWordRejected(rejected);
            GameEvents.RaiseWordSubmitted(rejected);
            GameEvents.RaiseStatusChanged(mode.Status);
            RaiseSelection();
            if (mode.IsRoundOver) EndRound();
            return;
        }

        // Refused by the round's own rule rather than by the dictionary. Like
        // the branch above this is unreachable through ENTER, which disables
        // itself — but unlike it, nothing is charged for trying: a librarian
        // says a word CAN'T be played, which isn't the same as playing a bad one.
        if (resolved.Refused != null)
        {
            foreach (var tile in chain) tile.FlashInvalid();
            RaiseSelection();
            return;
        }

        // The mode supplies the scoring hooks; the session doesn't know what
        // they are. wordsThisRound is passed BEFORE this word joins it.
        var result = scorer.Evaluate(chain, word, wordsThisRound, mode.Bookmarks, mode.ScoreRule);

        // End-of-round bookkeeping only — none of this is on screen, so it can
        // land immediately. Anything the HUD SHOWS (the score, the mode's
        // resource) is applied at the end of the walk-through instead, so the
        // readouts all move together rather than the score lagging the moves.
        wordsFound++;
        wordsThisRound.Add(word);
        if (result.Points > bestWordPoints)
        {
            bestWordPoints = result.Points;
            bestWord = word;
        }

        // ⚠️ Forget the played tiles' resolved faces BEFORE the empty selection
        // that follows this submit lands: ShowResolvedLetters would put every
        // wild back to "*" — mid-flight, so a wild would fly up as a star and
        // land on a twin reading "C". They never return to the board, so there
        // is nothing to restore.
        resolvedShowing.Clear();

        StartCoroutine(ScoreThenClear(chain, result));
    }

    /// <summary>
    /// Lifts the word off the board, then plays the score out: the word's
    /// length, then every tile, then the bookmarks, the round and the mode, one
    /// beat at a time.
    ///
    /// The tiles are RELEASED the moment the word is played (2026-10-07, his
    /// call) — they fly up into the word row and the board collapses and
    /// refills underneath the count, so the next word can be planned while the
    /// numbers run. Input stays off until the end, and the save still waits for
    /// !tallying, so nothing can act on the new tiles early. The refill draws
    /// exactly what it always did, in the same order — only sooner.
    ///
    /// ⚠️ THIS IS THE WALK-THROUGH'S ONLY CLOCK. It used to hand the HUD a
    /// duration and wait that long while the HUD ran its own timer, and the two
    /// could not stay in step — see ScoreTallyTiming. The widgets are now pure
    /// listeners: they draw and shake whatever beat they're handed, and this
    /// decides when. Everything the player SEES still lands at the end, so the
    /// score and the move counter move together.
    /// </summary>
    private IEnumerator ScoreThenClear(IReadOnlyList<Tile> chain, WordResult result)
    {
        // Input is off and the round can't end for the duration, so deferring
        // the word's effects can't be observed or exploited — nothing can be
        // submitted twice, and IsRoundOver is suppressed in Update.
        tallying = true;
        chainController.InputEnabled = false;

        // The opening frame: both numbers at the word's length, nothing counted.
        GameEvents.RaiseWordSubmitted(result);

        // Off the board, still alive: the word row flies them up, and the board
        // refills behind them. Then wait for every one to LAND — asked of the
        // tiles, never timed here, so this stays the walk's only clock. With
        // nothing listening nothing flies, every tile reads settled, and the
        // count starts at once.
        board.ReleaseTiles(chain);
        GameEvents.RaiseTilesLaunched(chain);

        // Anything nobody launched is hidden rather than left drawn on top of
        // the tiles now falling into its cell. (The board still owns it, and
        // destroys it at the end with the rest.)
        for (int i = 0; i < chain.Count; i++)
            if (chain[i] != null && chain[i].IsSettled) chain[i].gameObject.SetActive(false);

        yield return new WaitUntil(() => AllLanded(chain));

        yield return new WaitForSeconds(ScoreTallyTiming.StepAt(0));

        int beats = result.StepCount;
        for (int i = 0; i < beats; i++)
        {
            GameEvents.RaiseScoreBeat(result.Steps[i]);

            // The LAST beat is held by Finish below instead, or the end of every
            // word would be two pauses back to back.
            if (i < beats - 1) yield return new WaitForSeconds(ScoreTallyTiming.StepAt(i + 1));
        }

        // Paid even when nothing fired, so the final numbers are readable
        // instead of vanishing the instant the last beat lands.
        yield return new WaitForSeconds(ScoreTallyTiming.Finish());
        GameEvents.RaiseScoreWalkEnded();

        Score = ScoreLimits.Clamp((long)Score + result.Points);
        mode.OnWordAccepted(result);
        GameEvents.RaiseScoreChanged(Score);
        board.DisposeReleased();

        tallying = false;
        if (IsPlaying) chainController.InputEnabled = true;

        GameEvents.RaiseStatusChanged(mode.Status);
        RaiseSelection();
        RequestSave();

        if (mode.IsRoundOver) EndRound();
    }

    private static bool AllLanded(IReadOnlyList<Tile> chain)
    {
        for (int i = 0; i < chain.Count; i++)
            if (chain[i] != null && !chain[i].IsSettled) return false;
        return true;
    }

    // ---- Saving -------------------------------------------------------------
    //
    // The run saves itself in the background — after every word, after every
    // discard, and whenever the app goes away. The player is never asked.

    // A save is owed but hasn't been written yet, because the board is still
    // moving. See FlushSaveWhenSettled.
    private bool saveDirty;

    /// <summary>
    /// Asks for a save at the next safe moment. NOT an immediate write: a word's
    /// tiles are removed the instant it scores and the stack only compacts
    /// settleDelay later, so for that window the columns hold holes gravity would
    /// never have left. A snapshot taken there restores a board with permanent
    /// gaps in it — a bug whose symptom points nowhere near its cause.
    /// </summary>
    private void RequestSave() => saveDirty = true;

    /// <summary>
    /// Writes an owed save once nothing is moving. Being killed during the fall
    /// therefore costs the last word: the previous save still stands, and it's
    /// consistent. Better than a save that isn't.
    /// </summary>
    private void FlushSaveWhenSettled()
    {
        if (!saveDirty || tallying || board.Busy || board.Resolving) return;
        saveDirty = false;
        SaveRun();
    }

    /// <summary>Writes the run and this round out, right now.</summary>
    private void SaveRun()
    {
        var run = RunState.Current;
        if (run == null) return;

        var data = run.Capture(SaveLocation.Game);
        data.roundState = CaptureRound(run);
        RunSave.Write(data);
    }

    private RoundSnapshot CaptureRound(RunState run)
    {
        var snapshot = new RoundSnapshot
        {
            captured = true,
            score = Score,
            wordsFound = wordsFound,
            bestWord = bestWord,
            bestWordPoints = bestWordPoints,
        };
        snapshot.wordsThisRound.AddRange(wordsThisRound);

        // The board as bag indices — a tile's identity is which entry of the
        // run's bag it is, so that's what has to be written down.
        var index = run.TileIndex();
        foreach (var placed in board.Tiles)
        {
            if (placed.Value == null || placed.Value.Spec == null) continue;
            if (!index.TryGetValue(placed.Value.Spec, out int i)) continue;
            snapshot.boardCellX.Add(placed.Key.x);
            snapshot.boardCellY.Add(placed.Key.y);
            snapshot.boardTile.Add(i);
        }

        // The mode adds whatever the RULES own — moves, discards, the bag.
        mode.CaptureRound(snapshot);
        return snapshot;
    }

    /// <summary>
    /// The app is going away. On iOS this is the last callback that reliably
    /// runs, so an owed save is written here rather than waiting for a frame
    /// that may never come. If the board is mid-fall it's left alone — the
    /// previous save is consistent, and a torn one wouldn't be.
    /// </summary>
    private void OnApplicationPause(bool paused)
    {
        if (paused) FlushSaveWhenSettled();
    }

    private void OnApplicationQuit() => FlushSaveWhenSettled();

    private bool leavingScene;

    /// <summary>
    /// Leaves this round for another scene after a short beat — long enough for
    /// the last word's demolition to land. For a mode whose round flows
    /// somewhere other than the game-over panel; calling this skips the panel.
    /// </summary>
    public void ContinueTo(string sceneName, float delay = 1f)
    {
        leavingScene = true;
        StartCoroutine(LoadAfterBeat(sceneName, delay));
    }

    /// <summary>
    /// Leaves the round for the menu WITHOUT ending the run — the pause
    /// screen's MAIN MENU. The run is saved on the way out, so CONTINUE picks it
    /// up exactly here. Returns false, and does nothing, unless IsAtRest.
    ///
    /// ⚠️ REFUSES MID-WORD ON PURPOSE. A save must never be taken with the board
    /// moving (see FlushSaveWhenSettled), and a word still scoring may be the
    /// one that ends the round and sends the run to the shop — leaving then
    /// would race that. Waiting a second or two for the board to settle is the
    /// whole cost.
    /// </summary>
    public bool LeaveToMenu(string sceneName)
    {
        if (!IsAtRest || leavingScene) return false;

        // Unconditionally, not only when a save is owed: the board is at rest,
        // so this is always a safe snapshot, and it can't leave the file behind
        // a change that forgot to call RequestSave.
        saveDirty = false;
        SaveRun();

        leavingScene = true;
        SceneManager.LoadScene(sceneName);
        return true;
    }

    private IEnumerator LoadAfterBeat(string sceneName, float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneName);
    }

    private bool IsValidWord(string word) =>
        word.Length >= Config.minWordLength && validator.Contains(word);

    // ---- Undecided tiles ----------------------------------------------------
    //
    // Two kinds of tile arrive without a letter of their own, and both spell "*":
    // a WILD, which becomes any of the 26, and a CHOICE tile ("a/e/i"), which
    // becomes one of the handful its catalog row names. Deciding which needs the
    // dictionary, the round's rule AND the scorer, so it can't live in
    // ChainController.WordOf — Scripts/Core may not reference Scripts/Modes.
    // WordOf stays dumb and assembles the word with its "*"s still in it; this is
    // where that word is turned into the one the player gets.
    //
    // The ONLY difference between the two kinds down here is which letters the
    // dictionary is allowed to try — SlotOptionsFor below — so everything else
    // (refusing, scoring, the tiebreak, the face flip) is written once.
    //
    // ⚠️ NOTHING HERE MAY DRAW FROM Rng. This runs on every selection change
    // while a finger is moving, and one draw would shift every roll after it and
    // make the run's seed meaningless.

    /// <summary>What a selection really spells, once its wilds have been decided.</summary>
    private readonly struct Resolved
    {
        /// <summary>The word to show, score and record — or the raw "*" one when nothing fits.</summary>
        public readonly string Word;

        public readonly bool IsWord;

        /// <summary>The round's reason for refusing it, or null.</summary>
        public readonly string Refused;

        public Resolved(string word, bool isWord = false, string refused = null)
        {
            Word = word;
            IsWord = isWord;
            Refused = refused;
        }
    }

    /// <summary>
    /// Picks the best letter for every undecided tile in the chain, by the rule:
    /// of the words this chain could spell, throw away the ones the round
    /// refuses, and take whichever of the rest scores highest.
    ///
    /// Two kinds of tile arrive undecided and both spell "*": a WILD, which may
    /// become any of the 26, and a CHOICE tile, which may become one of the two
    /// or three its catalog row names. The only difference between them is the
    /// letters handed to the dictionary, which is why one method covers both.
    ///
    /// When the round refuses ALL of them it still returns one, with the reason —
    /// so the player sees a real word in red and is told why, rather than being
    /// shown "*" and left to guess whether it was even a word.
    /// </summary>
    private Resolved ResolveSelection(IReadOnlyList<Tile> chain)
    {
        string raw = ChainController.WordOf(chain);
        if (chain.Count == 0) return new Resolved(raw);

        // Nothing undecided: exactly what this did before wilds existed, at
        // exactly the same cost. Every word of every run that hasn't bought one
        // comes through here, so it must stay a single dictionary probe. A choice
        // tile spells "*" as well, so it needs no test of its own.
        if (raw.IndexOf(TileSpec.WildSpelling, System.StringComparison.Ordinal) < 0)
        {
            bool plain = IsValidWord(raw);
            return new Resolved(raw, plain, plain ? mode.Refuse(CheckFor(chain, raw)) : null);
        }

        // Too short to be a word at all, so there is nothing to resolve against
        // and the "*" stays on screen. This is what makes a wild read as a wild
        // until the selection is actually long enough to mean something.
        if (raw.Length < Config.minWordLength) return new Resolved(raw);

        var candidates = validator.Matches(raw, SlotOptionsFor(chain, raw.Length));
        if (candidates.Count == 0) return new Resolved(raw);

        // Only the FIRST refused candidate is ever needed, so nothing collects
        // the rest: a word that can't be played has no score worth comparing.
        string firstRefused = null, firstReason = null;
        List<string> allowed = null;

        for (int i = 0; i < candidates.Count; i++)
        {
            string reason = mode.Refuse(CheckFor(chain, candidates[i]));

            if (reason != null)
            {
                if (firstRefused == null)
                {
                    firstRefused = candidates[i];
                    firstReason = reason;
                }
                continue;
            }

            // Nothing can tell the allowed candidates apart, so the first one
            // already IS the answer — stop rather than asking the round about
            // hundreds of words whose scores are all going to be equal. "***"
            // matches every three-letter word in the dictionary, so this is the
            // difference between one Refuse call and a thousand.
            if (!ScoreSeparatesWords) return new Resolved(candidates[i], true);

            (allowed ??= new List<string>()).Add(candidates[i]);
        }

        if (allowed != null) return new Resolved(BestOf(chain, allowed), true);

        // Everything fits the dictionary and nothing fits the round. Show the
        // first one with its reason, so the player gets a real word in red and
        // an explanation rather than a star and a guess about whether it was
        // even a word. firstReason is that exact word's reason, since both were
        // taken together.
        return new Resolved(firstRefused, true, firstReason);
    }

    // What each position of the word is allowed to be, or null when every
    // undecided tile in the chain is a plain wild and so may be anything.
    //
    // Reused rather than rebuilt: this runs on every selection change while a
    // finger is moving, and a fresh list per frame is garbage for nothing.
    private readonly List<string> slotOptions = new();

    /// <summary>
    /// The letters each position of the word may take, for WordValidator.Matches —
    /// how a choice tile says it is a wildcard over three letters rather than 26.
    ///
    /// Returns NULL when the chain holds no choice tile, which is every chain a
    /// run without one ever makes: Matches then takes its original unrestricted
    /// path with no per-letter check at all.
    ///
    /// ⚠️ Walks by Letters.Length, not one entry per tile — a "ch" tile eats two
    /// positions of the word. Same walk as ShowResolvedLetters, and for the same
    /// reason: a tile's spelling is how many characters it is answerable for.
    /// </summary>
    private IReadOnlyList<string> SlotOptionsFor(IReadOnlyList<Tile> chain, int length)
    {
        bool anyChoice = false;

        slotOptions.Clear();
        for (int i = 0; i < length; i++) slotOptions.Add(null);

        int at = 0;
        for (int i = 0; i < chain.Count && at < length; i++)
        {
            var tile = chain[i];
            if (tile == null) continue;

            // A choice tile spells exactly one "*", so its options belong to the
            // one position it occupies. Everything else leaves its positions
            // null, which Matches reads as "any letter" — correct for a plain
            // wild and irrelevant for a fixed letter, which isn't a slot at all.
            //
            // tile.Options, not tile.Spec.Options: the Tile stamped it in Init,
            // and asking the Spec here would allocate a string per tile per frame.
            if (tile.Options.Length > 0)
            {
                slotOptions[at] = tile.Options;
                anyChoice = true;
            }

            at += tile.Letters.Length;
        }

        return anyChoice ? slotOptions : null;
    }

    /// <summary>
    /// Can anything actually tell two candidate words apart?
    ///
    /// ⚠️ Usually NOT, and that's the normal case rather than an edge case. The
    /// candidates are all spelled by the SAME tiles in the SAME order — only the
    /// letters they resolve to differ — so the tile walk returns identical Points
    /// AND Mult for every one of them. Only a bookmark that reads the letters
    /// (Bookend, Spine, Vowel Fanatic, Deja Vu) or a librarian that scores can
    /// separate them, and a run owns neither until it buys one. When this is
    /// false the alphabetical tiebreak IS the answer, which is why it's worth
    /// asking before doing any work at all.
    ///
    /// That argument rests on two things, and both are worth stating because the
    /// second one moved recently:
    ///
    ///  - A tile is worth the same whichever letter it becomes — true of a wild
    ///    (always 0) and of a choice tile (its catalog row stamps one baseScore
    ///    for the whole group).
    ///  - The word's LENGTH is the same for every candidate, which it is because
    ///    WordValidator.Matches only ever returns words the pattern's own length.
    ///    Length became the opening POINTS on 2026-09-30, so it is now part of
    ///    the score rather than just the multiplier — the shortcut survives that,
    ///    but it is one step closer to the edge than it was.
    ///
    /// ⚠️ A future tile that scored the letter it RESOLVED to would break this,
    /// and quietly: it would take the alphabetically first word rather than the
    /// highest-scoring one. ModeConfig.pointsPerLetter becoming a per-LETTER
    /// value rather than a per-letter-COUNT one is exactly that change wearing a
    /// different hat.
    /// </summary>
    private bool ScoreSeparatesWords =>
        (mode.Bookmarks != null && mode.Bookmarks.Count > 0) || mode.ScoreRule != null;

    /// <summary>
    /// The highest-scoring of several words the same tiles could spell. Ties keep
    /// the earlier one, and Matches hands them over alphabetically — so a tie is
    /// broken the same way every time rather than by whatever order a set happened
    /// to enumerate in.
    ///
    /// Only ever reached when ScoreSeparatesWords is true; the caller answers the
    /// tie case itself, without collecting a list to pick from.
    /// </summary>
    private string BestOf(IReadOnlyList<Tile> chain, List<string> candidates)
    {
        if (candidates.Count == 1) return candidates[0];

        string best = candidates[0];
        int bestPoints = -1;

        for (int i = 0; i < candidates.Count; i++)
        {
            // Safe to run speculatively: Evaluate builds a fresh ScoringContext
            // and every bookmark and librarian only ever writes into that one.
            // ⚠️ A bookmark that DID something — paid money, touched the run —
            // would fire once per candidate here. They must stay declarative.
            //
            // ⚠️ recording: false is not an optimisation to drop. This runs on
            // every frame of a drag, once per candidate — three wildcards is
            // hundreds of them — and only Points is read. Recording would build
            // a step per tile per candidate, each costing a few strings.
            int points = scorer.Evaluate(chain, candidates[i], wordsThisRound,
                                         mode.Bookmarks, mode.ScoreRule,
                                         recording: false).Points;

            // Strictly greater, so the first of equal answers wins.
            if (points > bestPoints)
            {
                bestPoints = points;
                best = candidates[i];
            }
        }

        return best;
    }

    // Tiles currently showing a letter that isn't theirs — wilds and choice
    // tiles alike. Kept here rather than asked of the board, so putting them back
    // costs nothing and doesn't need Board to grow an enumerator.
    private readonly List<Tile> resolvedShowing = new();

    /// <summary>
    /// Puts the resolved letter on the face of every undecided tile in the chain
    /// — a wild or a choice tile — and takes it back off the ones that have left
    /// it. A choice tile goes back to reading "A/E/I", a wild back to "*".
    ///
    /// ⚠️ Walks by Letters.Length, not one character per tile — a "ch" tile eats
    /// two characters of the word. Resolving never changes the word's LENGTH (each
    /// "*" becomes exactly one letter, and a choice tile spells exactly one "*"),
    /// which is what lets the raw chain and the resolved string be walked together.
    /// </summary>
    private void ShowResolvedLetters(IReadOnlyList<Tile> chain, Resolved resolved)
    {
        // Cleared first and unconditionally: a tile that left the selection has
        // to go back to "*" even when the new selection resolves to nothing.
        // Null-checked because a tile played a moment ago has been destroyed.
        for (int i = 0; i < resolvedShowing.Count; i++)
            if (resolvedShowing[i] != null) resolvedShowing[i].ShowLetters(null);
        resolvedShowing.Clear();

        if (!resolved.IsWord || resolved.Word == null) return;

        int at = 0;
        for (int i = 0; i < chain.Count && at < resolved.Word.Length; i++)
        {
            var tile = chain[i];
            if (tile == null) continue;

            int span = tile.Letters.Length;
            if (at + span > resolved.Word.Length) break;

            if (tile.IsUndecided)
            {
                tile.ShowLetters(resolved.Word.Substring(at, span));
                resolvedShowing.Add(tile);
            }

            at += span;
        }
    }

    /// <summary>
    /// A word plus the round around it, for the mode to judge. wordsThisRound is
    /// handed over as it stands, which is BEFORE the word being checked joins it
    /// — the same guarantee ScoringContext makes, and for the same reason: a
    /// rule about what you've already played must not count the thing you're
    /// asking about.
    /// </summary>
    private WordCheck CheckFor(IReadOnlyList<Tile> chain, string word) => new WordCheck
    {
        Word = word,
        Tiles = chain,
        WordsThisRound = wordsThisRound,
    };
}

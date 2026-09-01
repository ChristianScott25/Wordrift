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
    [Tooltip("Plain text, one lowercase word per line. Swap this to change dictionaries.")]
    [SerializeField] private TextAsset wordList;

    [Tooltip("Used when this scene is played directly. The main menu overrides it.")]
    [SerializeField] private ModeConfig fallbackMode;

    [Header("Camera framing")]
    [SerializeField] private float paddingAbove = 1.6f;
    [SerializeField] private float paddingSides = 0.4f;
    [SerializeField] private float verticalOffset = 0.5f;

    public ModeConfig Config { get; private set; }
    public int Score { get; private set; }
    public bool IsPlaying { get; private set; }

    private GameMode mode;
    private WordValidator validator;
    private ScoreCalculator scorer;

    private int wordsFound;
    private string bestWord = "";
    private int bestWordPoints;

    // True while a scored word is being walked through on the HUD. The board
    // hasn't cleared yet and input is off; the round must not end mid-tally.
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

        validator = new WordValidator(wordList);
        scorer = new ScoreCalculator(Config);
        mode = Config.CreateMode();

        // Attach first: the mode may swap the board's refill, gravity, or tile
        // source, and Build performs the opening fill through whatever is installed.
        mode.Attach(this, board);
        board.Build(Config.boardShape, Config.letterSet,
                    Config.tileSkins, Config.letterFont);
        FrameBoard();

        chainController.Init(board, sceneCamera);
        chainController.ChainChanged += OnChainChanged;
        chainController.ChainSubmitted += OnChainSubmitted;
    }

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

        Score = saved.score;
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
        string word = ChainController.WordOf(chain);

        GameEvents.RaiseSelectionChanged(new SelectionState
        {
            Word = word,
            TileCount = chain.Count,
            CanSubmit = IsPlaying && chain.Count > 0 && IsValidWord(word),
            CanDiscard = IsPlaying && mode.CanDiscard(chain.Count),
            DiscardsLeft = mode.DiscardsLeft,

            // The same first stage the real score uses, so the preview can't
            // drift from what pressing ENTER actually pays.
            Preview = scorer.Base(chain),
        });
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

    private void OnChainSubmitted(IReadOnlyList<Tile> chain)
    {
        if (!IsPlaying || chain.Count == 0) return;

        string word = ChainController.WordOf(chain);

        // Unreachable through the ENTER button, which disables itself on an
        // invalid word — kept because the submit path is public and the rule
        // that a bad word costs nothing shouldn't live only in a button.
        if (!IsValidWord(word))
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

        // The mode supplies the scoring hooks; the session doesn't know what
        // they are. wordsThisRound is passed BEFORE this word joins it.
        var result = scorer.Evaluate(chain, word, wordsThisRound, mode.Bookmarks);

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

        StartCoroutine(ScoreThenClear(chain, result));
    }

    /// <summary>
    /// Plays the score out before the board reacts, so nothing moves under the
    /// numbers. The wait is the HUD's tally: one beat per bookmark that fired,
    /// which means a run with no bookmarks waits for nothing at all.
    /// </summary>
    private IEnumerator ScoreThenClear(IReadOnlyList<Tile> chain, WordResult result)
    {
        // Input is off and the round can't end for the duration, so deferring
        // the word's effects can't be observed or exploited — nothing can be
        // submitted twice, and IsRoundOver is suppressed in Update.
        tallying = true;
        chainController.InputEnabled = false;

        GameEvents.RaiseWordSubmitted(result);
        yield return new WaitForSeconds(ScoreTallyTiming.For(result.StepCount));

        Score += result.Points;
        mode.OnWordAccepted(result);
        GameEvents.RaiseScoreChanged(Score);
        board.RemoveTiles(chain);

        tallying = false;
        if (IsPlaying) chainController.InputEnabled = true;

        GameEvents.RaiseStatusChanged(mode.Status);
        RaiseSelection();
        RequestSave();

        if (mode.IsRoundOver) EndRound();
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

    private IEnumerator LoadAfterBeat(string sceneName, float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneName);
    }

    private bool IsValidWord(string word) =>
        word.Length >= Config.minWordLength && validator.Contains(word);

    /// <summary>Fits the board in view with room above it for the HUD.</summary>
    private void FrameBoard()
    {
        if (sceneCamera == null) return;
        sceneCamera.orthographic = true;

        float halfHeight = board.BoardSize.y / 2f + paddingAbove;
        float halfWidth = board.BoardSize.x / 2f + paddingSides;
        sceneCamera.orthographicSize = Mathf.Max(halfHeight, halfWidth / sceneCamera.aspect);
        sceneCamera.transform.position = new Vector3(
            board.BoardCenter.x,
            board.BoardCenter.y + verticalOffset,
            sceneCamera.transform.position.z);
    }
}

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
        Score = 0;
        wordsFound = 0;
        bestWord = "";
        bestWordPoints = 0;
        wordsThisRound.Clear();

        mode.Begin();
        IsPlaying = true;
        chainController.InputEnabled = true;

        GameEvents.RaiseRoundStarted();
        GameEvents.RaiseScoreChanged(Score);
        GameEvents.RaiseStatusChanged(mode.Status);
        RaiseSelection();
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

        // Not while a word is still being tallied — the move is already spent,
        // so this would otherwise cut the last word's score off mid-count.
        if (!tallying && mode.IsRoundOver) EndRound();
    }

    private void EndRound()
    {
        IsPlaying = false;
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

        if (mode.IsRoundOver) EndRound();
    }

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

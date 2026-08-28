using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs one round. Owns the loop every mode shares — drag, validate, score,
/// demolish, refill — and delegates anything mode-specific to a GameMode.
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
        GameEvents.RaiseChainChanged("", false);
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

        if (mode.IsRoundOver) EndRound();
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

    private void OnChainChanged(IReadOnlyList<Tile> chain)
    {
        string word = ChainController.WordOf(chain);
        GameEvents.RaiseChainChanged(word, IsValidWord(word));
    }

    private void OnChainSubmitted(IReadOnlyList<Tile> chain)
    {
        GameEvents.RaiseChainChanged("", false);
        if (!IsPlaying || chain.Count == 0) return;

        string word = ChainController.WordOf(chain);

        if (!IsValidWord(word))
        {
            var rejected = ScoreCalculator.Rejected(word, chain.Count);
            foreach (var tile in chain) tile.FlashInvalid();
            mode.OnWordRejected(rejected);
            GameEvents.RaiseWordSubmitted(rejected);
            GameEvents.RaiseStatusChanged(mode.Status);
            if (mode.IsRoundOver) EndRound();
            return;
        }

        // The mode supplies the scoring hooks; the session doesn't know what
        // they are. wordsThisRound is passed BEFORE this word joins it.
        var result = scorer.Evaluate(chain, word, wordsThisRound, mode.Bookmarks);
        Score += result.Points;
        wordsFound++;
        wordsThisRound.Add(word);
        if (result.Points > bestWordPoints)
        {
            bestWordPoints = result.Points;
            bestWord = word;
        }

        mode.OnWordAccepted(result);
        board.RemoveTiles(chain);

        GameEvents.RaiseScoreChanged(Score);
        GameEvents.RaiseWordSubmitted(result);
        GameEvents.RaiseStatusChanged(mode.Status);

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

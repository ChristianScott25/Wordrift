using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ties everything together: builds the board / input / HUD at startup,
/// runs the round timer, validates submitted chains, and scores them.
/// </summary>
public class GameManager : MonoBehaviour
{
    // --- Demo configuration ---
    private const int GridWidth = 5;
    private const int GridHeight = 10;
    private const float RoundSeconds = 60f;
    private const int MinWordLength = 3;

    private Board board;
    private ChainController input;
    private GameHUD hud;
    private WordDictionary dictionary;

    private float timeLeft;
    private int score;
    private bool playing;

    private void Start()
    {
        dictionary = new WordDictionary();
        Debug.Log($"Dictionary loaded: {dictionary.Count} words.");

        var boardGo = new GameObject("Board");
        board = boardGo.AddComponent<Board>();
        board.Build(new RectangleShape(GridWidth, GridHeight));

        SetUpCamera();

        input = new GameObject("ChainController").AddComponent<ChainController>();
        input.Init(this, board, Camera.main);

        hud = new GameObject("HUD").AddComponent<GameHUD>();
        hud.Init(this);

        StartRound();
    }

    private void SetUpCamera()
    {
        var cam = Camera.main;
        cam.orthographic = true;
        cam.backgroundColor = new Color(0.09f, 0.16f, 0.22f);

        // Fit the board with room above it for the HUD.
        Vector2 center = board.BoardCenter;
        float halfH = board.BoardSize.y / 2f + 1.6f;
        float halfW = board.BoardSize.x / 2f + 0.4f;
        cam.orthographicSize = Mathf.Max(halfH, halfW / cam.aspect);
        cam.transform.position = new Vector3(center.x, center.y + 0.5f, -10f);
    }

    private void StartRound()
    {
        score = 0;
        timeLeft = RoundSeconds;
        playing = true;
        input.InputEnabled = true;
        hud.SetScore(0);
        hud.SetTimer(timeLeft);
        hud.SetCurrentWord("", false);
        hud.ShowWordResult("", 0, false);
        hud.HideGameOver();
    }

    private void Update()
    {
        if (!playing) return;
        timeLeft -= Time.deltaTime;
        hud.SetTimer(timeLeft);
        if (timeLeft <= 0f) EndRound();
    }

    private void EndRound()
    {
        playing = false;
        input.InputEnabled = false;
        input.CancelChain();
        hud.ShowGameOver(score);
    }

    public void RestartGame()
    {
        board.ResetBoard();
        StartRound();
    }

    /// <summary>Called while dragging so the HUD can preview the word.</summary>
    public void OnChainChanged(string word)
    {
        hud.SetCurrentWord(word, word.Length >= MinWordLength && dictionary.Contains(word));
    }

    /// <summary>Called when the player releases their finger.</summary>
    public void SubmitChain(List<Tile> chain, string word)
    {
        hud.SetCurrentWord("", false);
        if (!playing || chain.Count == 0) return;

        if (word.Length >= MinWordLength && dictionary.Contains(word))
        {
            int points = LetterBag.WordScore(word);
            score += points;
            hud.SetScore(score);
            hud.ShowWordResult(word, points, true);
            board.RemoveTiles(chain);
        }
        else
        {
            hud.ShowWordResult(word, 0, false);
            foreach (var tile in chain) tile.FlashInvalid();
        }
    }
}

/// <summary>
/// Spawns the GameManager automatically whenever the Timed Mode scene is
/// entered (at startup or via the main menu), so the scene needs no manual setup.
/// </summary>
public static class Bootstrap
{
    private const string GameSceneName = "Timed Mode";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        TrySpawn(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, _) => TrySpawn(scene);
    }

    private static void TrySpawn(UnityEngine.SceneManagement.Scene scene)
    {
        if (scene.name == GameSceneName && Object.FindFirstObjectByType<GameManager>() == null)
            new GameObject("GameManager").AddComponent<GameManager>();
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>End-of-round screen. Its buttons call PlayAgain / BackToMenu.</summary>
public class GameOverPanel : MonoBehaviour
{
    [Tooltip("The visuals to show/hide. Must NOT be this object — deactivating " +
             "ourselves would stop us hearing the round-ended event.")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text scoreLabel;
    [SerializeField] private TMP_Text detailLabel;
    [SerializeField] private GameSession session;

    [Header("Text")]
    [SerializeField] private string title = "ROUND OVER";
    [SerializeField] private string menuSceneName = "Main Menu";

    private void Awake()
    {
        if (root == gameObject)
        {
            Debug.LogError("GameOverPanel's 'root' must be a child object, not itself.", this);
            root = null;
        }
        Show(false);
    }

    private void Show(bool visible)
    {
        if (root != null) root.SetActive(visible);
    }

    private void OnEnable()
    {
        GameEvents.RoundEnded += OnRoundEnded;
        GameEvents.RoundStarted += OnRoundStarted;
    }

    private void OnDisable()
    {
        GameEvents.RoundEnded -= OnRoundEnded;
        GameEvents.RoundStarted -= OnRoundStarted;
    }

    private void OnRoundStarted() => Show(false);

    private void OnRoundEnded(RoundSummary summary)
    {
        if (titleLabel != null) titleLabel.text = title;
        if (scoreLabel != null) scoreLabel.text = summary.Score.ToString();
        if (detailLabel != null)
        {
            detailLabel.text = summary.WordsFound > 0
                ? $"{summary.WordsFound} words   •   best: {summary.BestWord.ToUpperInvariant()} (+{summary.BestWordPoints})"
                : "No words found";
        }
        Show(true);
    }

    public void PlayAgain()
    {
        if (session != null) session.Restart();
    }

    public void BackToMenu() => SceneManager.LoadScene(menuSceneName);
}

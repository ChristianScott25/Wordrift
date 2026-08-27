using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The between-rounds screen of a run. A stub today: it says what was cleared
/// and what's next, and its one button starts the next round. This is where
/// buying tiles, bookmarks, and sack upgrades will live — and everything sold
/// here edits RunState.Current, never an authored asset.
/// </summary>
public class ShopScreen : MonoBehaviour
{
    [SerializeField] private TMP_Text headline;
    [SerializeField] private TMP_Text detail;
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string menuSceneName = "Main Menu";

    private void Start()
    {
        var run = RunState.Current;
        if (run == null)
        {
            // The scene was opened on its own — nothing cleared, nowhere to go.
            Debug.LogWarning("Shop opened with no run in progress — returning to the menu.");
            SceneManager.LoadScene(menuSceneName);
            return;
        }

        if (headline != null) headline.text = $"ROUND {run.Round} CLEARED";
        if (detail != null)
            detail.text = $"NEXT TARGET   {run.Template.TargetForRound(run.Round + 1)}";
    }

    /// <summary>Wired to the Continue button. Starts the next round of the run.</summary>
    public void Continue()
    {
        var run = RunState.Current;
        if (run == null)
        {
            SceneManager.LoadScene(menuSceneName);
            return;
        }

        run.AdvanceRound();
        ModeSelection.Select(run.Template);
        SceneManager.LoadScene(gameSceneName);
    }
}

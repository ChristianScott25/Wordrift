using System.Collections.Generic;
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

        var granted = GrantPlaceholderUpgrades(run, 3);

        if (detail != null)
        {
            detail.text = $"NEXT TARGET   {run.Template.TargetForRound(run.Round + 1)}";
            if (granted.Count > 0)
                detail.text += $"\n<size=60%>FREE UPGRADES   {string.Join("   ", granted)}</size>";
        }
    }

    // ------------------------------------------------------------------------
    // TEMPORARY — delete when the real shop exists. This is a placeholder so
    // run-persistent tile upgrades can be seen working end to end before
    // there's anything to buy: every shop visit gilds three random sack tiles
    // with a random modifier from the mode's upgrade pool, free.
    // ------------------------------------------------------------------------
    private static List<string> GrantPlaceholderUpgrades(RunState run, int count)
    {
        var granted = new List<string>();
        if (run.Sack.Count == 0 || run.Template.tileModifiers == null) return granted;

        var pool = new List<TileModifier>();
        foreach (var modifier in run.Template.tileModifiers)
            if (modifier != null) pool.Add(modifier);
        if (pool.Count == 0) return granted;

        for (int i = 0; i < count; i++)
        {
            var tile = run.Sack[Random.Range(0, run.Sack.Count)];
            var modifier = pool[Random.Range(0, pool.Count)];
            tile.AddModifier(modifier);
            granted.Add($"{tile.letters.ToUpperInvariant()}+{modifier.badgeLabel}");
        }
        return granted;
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

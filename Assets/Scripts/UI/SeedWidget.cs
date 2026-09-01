using TMPro;
using UnityEngine;

/// <summary>
/// Prints the run's seed, quietly, in a corner. Deliberately unobtrusive — it's
/// there so a board can be reported and reproduced ("seed 4K7PQW2M, round 3"),
/// not because a player needs to think about it.
///
/// Scene-agnostic: it reads RunState directly and draws nothing when there's no
/// run, so the same prefab works in the Game scene and the Shop.
/// </summary>
public class SeedWidget : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    [Tooltip("Printed before the code. Blank for just the code.")]
    [SerializeField] private string prefix = "";

    private void OnEnable()
    {
        // The round can change under it (the shop advances it), so refresh on
        // every enable rather than only once.
        Refresh();
        GameEvents.RoundStarted += Refresh;
    }

    private void OnDisable() => GameEvents.RoundStarted -= Refresh;

    private void Refresh()
    {
        if (label == null) return;

        var run = RunState.Current;
        label.text = run == null ? "" : $"{prefix}{run.SeedCode}";
    }
}

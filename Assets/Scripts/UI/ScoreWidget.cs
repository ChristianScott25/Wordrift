using TMPro;
using UnityEngine;

/// <summary>Displays the running score. Drop the prefab anywhere on the HUD.</summary>
public class ScoreWidget : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private string format = "{0}";

    private void OnEnable() => GameEvents.ScoreChanged += OnScoreChanged;
    private void OnDisable() => GameEvents.ScoreChanged -= OnScoreChanged;

    private void OnScoreChanged(int score)
    {
        if (label != null) label.text = string.Format(format, score);
    }
}

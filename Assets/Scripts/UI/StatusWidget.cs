using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows whatever resource the current mode is spending — seconds in Timed
/// Mode, moves in Moves Mode. One widget covers every mode, so adding a mode
/// needs no new HUD work.
/// </summary>
public class StatusWidget : MonoBehaviour
{
    [SerializeField] private TMP_Text valueLabel;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private Image progressBar;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color urgentColor = new Color(1f, 0.4f, 0.4f);

    private void OnEnable() => GameEvents.StatusChanged += OnStatusChanged;
    private void OnDisable() => GameEvents.StatusChanged -= OnStatusChanged;

    private void OnStatusChanged(ModeStatus status)
    {
        var color = status.Urgent ? urgentColor : normalColor;

        if (valueLabel != null)
        {
            valueLabel.text = status.Value;
            valueLabel.color = color;
        }
        if (nameLabel != null) nameLabel.text = status.Label;
        if (progressBar != null)
        {
            progressBar.fillAmount = Mathf.Clamp01(status.Fraction);
            progressBar.color = color;
        }
    }
}

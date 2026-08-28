using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows whatever resource the current mode is spending — moves in Rogue Demo,
/// seconds in a timed mode if one ever comes back. One widget covers every
/// mode, so adding a mode needs no new HUD work.
/// </summary>
public class StatusWidget : MonoBehaviour
{
    [SerializeField] private TMP_Text valueLabel;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private Image progressBar;

    [Tooltip("Optional second readout, for a mode that is chasing a target as well " +
             "as spending a resource. Leave empty and it rides along with the name " +
             "label instead, so a mode that sets one is readable with no wiring.")]
    [SerializeField] private TMP_Text goalLabel;

    [Tooltip("Optional third readout — a standing line of text the mode wants shown, " +
             "the run's bookmarks today. Nothing rides along if this is empty: it's " +
             "extra colour, not a number, so dropping it costs the player nothing.")]
    [SerializeField] private TMP_Text extraLabel;

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
        bool goalHasOwnSlot = goalLabel != null;
        if (goalHasOwnSlot) goalLabel.text = status.Goal;

        if (nameLabel != null)
        {
            // No dedicated slot wired up yet: rather than drop the goal on the
            // floor, show it next to the resource name. Assigning goalLabel in
            // the prefab moves it out again — nothing else changes.
            //
            // Sized down because the name label is 400px wide with auto-sizing
            // off and wrapping on: at full size the goal would wrap onto a
            // second line and overlap the score underneath it.
            nameLabel.text = goalHasOwnSlot || string.IsNullOrEmpty(status.Goal)
                ? status.Label
                : $"{status.Label}   <size=60%>{status.Goal}</size>";
        }
        if (extraLabel != null) extraLabel.text = status.Extra;

        if (progressBar != null)
        {
            progressBar.fillAmount = Mathf.Clamp01(status.Fraction);
            progressBar.color = color;
        }
    }
}

using TMPro;
using UnityEngine;

/// <summary>
/// Live preview of the selected word; turns green once it's valid, and says why
/// when a real word still can't be played.
/// </summary>
public class CurrentWordWidget : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    [Tooltip("Optional. Where a librarian's refusal is written — 'Too short: 5 " +
             "letters or longer'. Leave it empty and the reason rides along under " +
             "the word itself, so the message is never lost for want of wiring.")]
    [SerializeField] private TMP_Text reasonLabel;

    [SerializeField] private Color pendingColor = Color.white;
    [SerializeField] private Color validColor = new Color(0.4f, 1f, 0.4f);

    [Tooltip("A real word this round won't take. Distinct from the pending colour " +
             "on purpose: the difference between 'not a word yet' and 'not allowed' " +
             "is the whole point of the message.")]
    [SerializeField] private Color refusedColor = new Color(1f, 0.65f, 0.25f);

    [SerializeField] private bool uppercase = true;

    private void OnEnable() => GameEvents.SelectionChanged += OnSelectionChanged;
    private void OnDisable() => GameEvents.SelectionChanged -= OnSelectionChanged;

    private void OnSelectionChanged(SelectionState selection)
    {
        if (label == null) return;

        string word = selection.Word ?? "";
        if (uppercase) word = word.ToUpperInvariant();

        // A decision the session already made, never re-derived here — the same
        // rule CanSubmit follows. A widget that worked out for itself why a word
        // was refused would eventually disagree with the round that refused it.
        string reason = selection.RefusedReason ?? "";
        bool refused = reason.Length > 0;

        if (reasonLabel != null)
        {
            label.text = word;
            reasonLabel.text = reason;
        }
        else
        {
            // No slot wired: show it under the word rather than dropping it.
            // Sized down so the word stays the thing being read.
            label.text = refused ? $"{word}\n<size=55%>{reason}</size>" : word;
        }

        label.color = selection.CanSubmit ? validColor
                    : refused ? refusedColor
                    : pendingColor;
    }
}

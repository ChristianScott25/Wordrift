using TMPro;
using UnityEngine;

/// <summary>Live preview of the selected word; turns green once it's valid.</summary>
public class CurrentWordWidget : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color pendingColor = Color.white;
    [SerializeField] private Color validColor = new Color(0.4f, 1f, 0.4f);
    [SerializeField] private bool uppercase = true;

    private void OnEnable() => GameEvents.SelectionChanged += OnSelectionChanged;
    private void OnDisable() => GameEvents.SelectionChanged -= OnSelectionChanged;

    private void OnSelectionChanged(SelectionState selection)
    {
        if (label == null) return;
        string word = selection.Word ?? "";
        label.text = uppercase ? word.ToUpperInvariant() : word;

        // CanSubmit, not "is it in the dictionary" — the session already
        // decided, and re-deriving it here is how a preview starts lying.
        label.color = selection.CanSubmit ? validColor : pendingColor;
    }
}

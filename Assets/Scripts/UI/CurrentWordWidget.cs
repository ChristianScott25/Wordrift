using TMPro;
using UnityEngine;

/// <summary>Live preview of the word being dragged; turns green once it's valid.</summary>
public class CurrentWordWidget : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color pendingColor = Color.white;
    [SerializeField] private Color validColor = new Color(0.4f, 1f, 0.4f);
    [SerializeField] private bool uppercase = true;

    private void OnEnable() => GameEvents.ChainChanged += OnChainChanged;
    private void OnDisable() => GameEvents.ChainChanged -= OnChainChanged;

    private void OnChainChanged(string word, bool valid)
    {
        if (label == null) return;
        label.text = uppercase ? word.ToUpperInvariant() : word;
        label.color = valid ? validColor : pendingColor;
    }
}

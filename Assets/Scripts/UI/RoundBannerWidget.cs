using TMPro;
using UnityEngine;

/// <summary>
/// Announces what makes this round different — the librarian's name over its
/// rule — for as long as the round lasts.
///
/// Its own widget rather than a fourth line on StatusWidget, for a reason worth
/// keeping: StatusWidget is pinned to the top-left corner and everything inside
/// it is positioned relative to that corner, so a line living there can only
/// ever sit under the move counter — which is exactly where the selected word is
/// drawn. This anchors wherever it likes.
///
/// It still reads ModeStatus.Banner, so the channel from the rules to the HUD is
/// unchanged and a mode still needs no new HUD work to use it.
/// </summary>
public class RoundBannerWidget : MonoBehaviour
{
    [Tooltip("The line itself. Hidden on a round with nothing to announce, which " +
             "is why it must be a CHILD and never this object — see OnEnable.")]
    [SerializeField] private TMP_Text label;

    private void OnEnable()
    {
        // This object stays active whatever happens: it's the listener, and a
        // listener that switches itself off never hears the event that would
        // switch it back on. Only the child label is hidden.
        if (label != null && label.gameObject == gameObject)
        {
            Debug.LogError("RoundBannerWidget's label is the widget itself — hiding it would " +
                           "switch off its own listener. Point it at a child label.", this);
            label = null;
        }

        GameEvents.StatusChanged += OnStatusChanged;
    }

    private void OnDisable() => GameEvents.StatusChanged -= OnStatusChanged;

    private void OnStatusChanged(ModeStatus status)
    {
        if (label == null) return;

        string banner = status.Banner ?? "";
        bool announcing = banner.Length > 0;

        // Hidden rather than blanked, so an empty banner leaves no gap.
        if (label.gameObject.activeSelf != announcing) label.gameObject.SetActive(announcing);
        if (announcing) label.text = banner;
    }
}

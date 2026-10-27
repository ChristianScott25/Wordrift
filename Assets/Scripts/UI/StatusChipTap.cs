using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A tap on a resource-strip chip opens the info box with what the chip means.
/// The chips are icons with no word on them (2026-10-27), so this is how the
/// player finds out a quill is their moves.
///
/// StatusWidget adds it to every chip it builds and writes Title/About each
/// frame along with the number. It reads them only on the tap, the same way
/// RoundBannerWidget answers for the header. The chip's plate has to be a
/// raycast target for the tap to reach this. That's safe because the strip sits
/// above the board, so it can't block a press that starts on a tile.
/// </summary>
public class StatusChipTap : MonoBehaviour, IPointerClickHandler
{
    public string Title;
    public string About;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(Title) && string.IsNullOrEmpty(About)) return;
        Inspector.ShowText(Title, About, Inspector.ScreenRectOf((RectTransform)transform));
    }
}

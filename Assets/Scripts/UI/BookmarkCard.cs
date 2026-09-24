using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One bookmark, drawn as a card the player can drag.
///
/// It owns no state worth the name: which bookmark it shows and where it sits
/// are both told to it by BookmarkRowWidget, which is the thing that knows what
/// the run is carrying and in what order. All this does is look like a card and
/// report a finger.
///
/// The three drag interfaces are the FIRST in this project. They cost nothing to
/// use — the Game and Shop scenes already carry a GraphicRaycaster and an
/// InputSystemUIInputModule, which routes mouse and touch through them alike.
///
/// ⚠️ The background Image must stay a raycast target. It is what makes the card
/// draggable at all, and it's also what keeps ChainController's hands off the
/// board underneath: that guard is EventSystem.IsPointerOverGameObject() on the
/// press frame, and a card with no raycast target is invisible to it.
/// </summary>
public class BookmarkCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("The card's body. Uses the tile sprite, so a bookmark looks like " +
             "something off the board rather than a new kind of object.")]
    [SerializeField] private Image body;

    [Tooltip("The bookmark's name, over the body.")]
    [SerializeField] private TMP_Text label;

    private BookmarkRowWidget owner;

    /// <summary>This card's RectTransform, fetched once.</summary>
    public RectTransform Rect { get; private set; }

    private void Awake() => Rect = (RectTransform)transform;

    /// <summary>
    /// Everything the card is. Called on every refresh rather than once at
    /// creation, because a card is REUSED: reordering hands card 0 a different
    /// bookmark rather than destroying and rebuilding the row.
    /// </summary>
    public void Bind(BookmarkRowWidget owner, string name, Sprite sprite, Color bodyColor, Color textColor)
    {
        this.owner = owner;
        if (Rect == null) Rect = (RectTransform)transform;

        if (body != null)
        {
            // Only override the authored sprite when there is one to use, the
            // same bargain TileSkin takes everywhere else: a null field means
            // "keep what this was authored with", not "go blank".
            if (sprite != null) body.sprite = sprite;
            body.color = bodyColor;

            // Belt and braces. A card that isn't a raycast target can't be
            // dragged AND stops shielding the board from the same press.
            body.raycastTarget = true;
        }

        if (label != null)
        {
            label.text = name == null ? "" : name.ToUpperInvariant();
            label.color = textColor;
        }
    }

    public void OnBeginDrag(PointerEventData eventData) => owner?.BeginCardDrag(this);

    public void OnDrag(PointerEventData eventData) => owner?.DragCard(this, eventData);

    public void OnEndDrag(PointerEventData eventData) => owner?.EndCardDrag(this);
}

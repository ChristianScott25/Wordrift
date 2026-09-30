using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One slot of the items box — either an item you're carrying, or an empty
/// space where one would go.
///
/// It is to ConsumablesAreaWidget exactly what BookmarkCard is to
/// BookmarkRowWidget: it owns nothing, it is told what to show and which slot it
/// is, and it reports a finger.
///
/// ⚠️ A TAP READS, A DRAG ONTO THE BOARD SPENDS. Those are two different
/// gestures on purpose and they replaced a tap-then-confirm panel on
/// 2026-09-29. Dragging is what a targeted item will need anyway — the tile
/// under the finger is the target — so building it now means there is one way
/// to play an item rather than two, and a drag is deliberate enough that it
/// needs no confirmation step. Reading is a plain tap here and press-and-hold on
/// the BOARD, because a tap there already means "add this letter".
///
/// ⚠️ A filled slot is a raycast target and an empty one is not. An empty slot
/// that swallowed a tap would be worse than one that ignores it, and it would
/// also shield the board from a press for no reason (ChainController's guard is
/// EventSystem.IsPointerOverGameObject on the press frame).
///
/// ⚠️ THE TAP AND THE DRAG MUST NOT BOTH FIRE. Unity still delivers a click on
/// release when the finger comes back over the slot it started on, so a drag
/// that ended where it began would also open the info box. `dragged` rules that
/// out — the same guard BookmarkCard carries.
/// </summary>
public class ConsumableSlotView : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerClickHandler
{
    [Tooltip("The slot's body. A filled one borrows the tile sprite, so an item " +
             "reads as something you could put on the board rather than a new " +
             "kind of object.")]
    [SerializeField] private Image body;

    [Tooltip("The item's name, over the body. Blank when the slot is empty.")]
    [SerializeField] private TMP_Text label;

    [SerializeField] private Button button;

    private ConsumablesAreaWidget owner;
    private int slot = -1;

    // Did this gesture turn into a drag? See the class comment.
    private bool dragged;

    /// <summary>Which slot of the box this is.</summary>
    public int Slot => slot;

    /// <summary>This slot's RectTransform, fetched once.</summary>
    public RectTransform Rect { get; private set; }

    /// <summary>What this slot is showing, or null when it's empty.</summary>
    public Consumable Consumable { get; private set; }

    private void Awake()
    {
        Rect = (RectTransform)transform;
        EnsureParts();
    }

    /// <summary>
    /// Builds a slot. Made in code rather than authored as a prefab because how
    /// many there are is the RUN's business, not the scene's — the same reason
    /// StatusWidget builds its chips and BookmarkRowWidget its cards.
    /// </summary>
    public static ConsumableSlotView Create(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var view = go.AddComponent<ConsumableSlotView>();
        view.Rect = (RectTransform)go.transform;
        view.EnsureParts();
        return view;
    }

    /// <summary>
    /// Fills in whatever wasn't wired. Idempotent, and safe to call before or
    /// after Awake — Create runs it on an object whose Awake has already fired,
    /// since AddComponent is immediate.
    /// </summary>
    private void EnsureParts()
    {
        if (Rect == null) Rect = (RectTransform)transform;

        if (body == null) body = GetComponent<Image>();
        if (body == null) body = gameObject.AddComponent<Image>();

        if (button == null) button = GetComponent<Button>();
        if (button == null) button = gameObject.AddComponent<Button>();
        button.targetGraphic = body;

        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        if (label == null)
        {
            var text = new GameObject("Label", typeof(RectTransform));
            text.transform.SetParent(transform, false);

            var rect = (RectTransform)text.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // A new TMP_Text takes whatever font is TMP's DEFAULT at the moment
            // it is created, which Word Crush/Create Font Asset has already set
            // to the game's one typeface. That is the whole reason it is set.
            var tmp = text.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 8f;
            tmp.fontSizeMax = 24f;
            tmp.textWrappingMode = TextWrappingModes.Normal;

            // The label must not eat the tap — the body is what's clickable.
            tmp.raycastTarget = false;

            label = tmp;
        }

        // ⚠️ THE BUTTON IS HERE FOR ITS COLOURS, NOT ITS CLICK. Reading and
        // spending are both handled by this class's own pointer handlers, which
        // is what lets a drag suppress the tap; Button.onClick has no idea a
        // drag happened and would fire anyway. Any listener left on it from an
        // older scene would re-open that hole, so it is cleared.
        button.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// Everything this slot is. Called on every refresh rather than once at
    /// creation, because a slot is REUSED — spending item 0 hands slot 0 the
    /// item that was in slot 1 rather than rebuilding the box.
    /// </summary>
    public void Bind(ConsumablesAreaWidget owner, int slot, Consumable consumable,
                     Sprite filledSprite, Color filledColor,
                     Sprite emptySprite, Color emptyColor, Color textColor)
    {
        this.owner = owner;
        this.slot = slot;
        Consumable = consumable;

        if (Rect == null) Rect = (RectTransform)transform;

        bool filled = consumable != null;

        var bodyColor = filled ? filledColor : emptyColor;

        if (body != null)
        {
            // A null sprite means "keep what this was authored with", the same
            // bargain TileSkin takes — never "go blank".
            var sprite = filled ? filledSprite : emptySprite;
            if (sprite != null) body.sprite = sprite;

            body.color = bodyColor;
            body.preserveAspect = true;

            // An empty slot is NOT a raycast target: a tap it swallowed would do
            // nothing AND would shield the board from the same press, since
            // ChainController's guard is EventSystem.IsPointerOverGameObject.
            body.raycastTarget = filled;
        }

        if (label != null)
        {
            label.text = filled ? consumable.Title.ToUpperInvariant() : "";
            label.color = textColor;
        }

        if (button != null)
        {
            // ⚠️ THE COLOUR HAS TO GO THROUGH THE ColorBlock, not just onto the
            // Image. A Button with ColorTint transition OVERWRITES its target
            // graphic's colour with the state colour — so setting body.color and
            // stopping there would paint the slot and then watch Unity paint
            // over it with plain white on the very next state transition.
            var colors = button.colors;
            colors.normalColor = bodyColor;
            colors.selectedColor = bodyColor;
            colors.highlightedColor = Lift(bodyColor, 0.10f);
            colors.pressedColor = Lift(bodyColor, -0.12f);

            // An empty slot is a disabled button that still has to look like an
            // empty slot rather than like a greyed-out one.
            colors.disabledColor = bodyColor;
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            button.interactable = filled;
        }

        name = filled ? $"Item {slot} - {consumable.name}" : $"Item {slot} - empty";
    }

    /// <summary>Every gesture starts as a tap until it moves.</summary>
    public void OnPointerDown(PointerEventData eventData) => dragged = false;

    /// <summary>A tap, not a drag: say what this item does. Never spends it.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (dragged || Consumable == null || owner == null) return;
        owner.Read(slot);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragged = true;
        if (Consumable == null || owner == null) return;
        owner.BeginItemDrag(this, eventData);
    }

    public void OnDrag(PointerEventData eventData) => owner?.DragItem(this, eventData);

    public void OnEndDrag(PointerEventData eventData) => owner?.EndItemDrag(this, eventData);

    private static Color Lift(Color color, float by) => new Color(
        Mathf.Clamp01(color.r + by), Mathf.Clamp01(color.g + by),
        Mathf.Clamp01(color.b + by), color.a);
}

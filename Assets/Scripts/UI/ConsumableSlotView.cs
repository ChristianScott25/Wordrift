using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One slot of the items box — either an item you're carrying, or an empty
/// space where one would go.
///
/// It is to ConsumablesAreaWidget exactly what BookmarkCard is to
/// BookmarkRowWidget: it owns nothing, it is told what to show and which slot it
/// is, and it reports a tap. Splitting it out costs nothing today and is what
/// makes the next step cheap — a consumable you DRAG onto a tile is three
/// interfaces added to this class, the same three BookmarkCard already carries
/// and which need no wiring in either scene.
///
/// ⚠️ A filled slot is a raycast target and an empty one is not. An empty slot
/// that swallowed a tap would be worse than one that ignores it, and it would
/// also shield the board from a press for no reason (ChainController's guard is
/// EventSystem.IsPointerOverGameObject on the press frame).
/// </summary>
public class ConsumableSlotView : MonoBehaviour
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

        // A persistent listener pointing at a scene object does not survive
        // being saved into a prefab, so it is added here — the same reason
        // WordActionsWidget and BagButtonWidget add their own. Removed first so
        // a re-run (or a wired prefab that already has it) can't double up.
        button.onClick.RemoveListener(OnPressed);
        button.onClick.AddListener(OnPressed);
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

    private void OnPressed()
    {
        if (Consumable == null || owner == null) return;
        owner.Select(slot);
    }

    private static Color Lift(Color color, float by) => new Color(
        Mathf.Clamp01(color.r + by), Mathf.Clamp01(color.g + by),
        Mathf.Clamp01(color.b + by), color.a);
}

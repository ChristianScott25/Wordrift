using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The resource strip — the row of small readouts under the round header:
/// money, moves, discards, round.
///
/// It draws whatever chips the mode publishes, in the order it publishes them,
/// and knows nothing about what any of them mean. That is the whole point: this
/// used to render ONE resource plus a pre-formatted string the mode had crammed
/// three more readouts into ("R2   60 / 120   BAG 12   $25"), because the HUD
/// had exactly one spare slot. A mode now says how many readouts it has.
///
/// ⚠️ The chip views are built at runtime, like BookmarkRowWidget's cards and
/// for the same reason: the COUNT is the mode's to decide, so there is nothing
/// sensible to author in a prefab.
/// </summary>
public class StatusWidget : MonoBehaviour
{
    [Header("Chip look")]
    [Tooltip("The chip's background — the Medium Button art, 9-sliced so only its " +
             "flat middle stretches and the corners stay crisp at any chip shape.")]
    [SerializeField] private Sprite plateSprite;

    [Tooltip("How chunky the plate's corners are. Bigger = bigger corners. Same " +
             "knob as the info box's Border Scale.")]
    [Min(0.01f)][SerializeField] private float plateBorderScale = 2.5f;


    [SerializeField] private Color chipColor = new Color(0f, 0f, 0f, 0.35f);
    [SerializeField] private Color labelColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] private Color valueColor = Color.white;

    [Tooltip("Value colour when the mode marks a chip urgent — low on moves.")]
    [SerializeField] private Color urgentColor = new Color(1f, 0.45f, 0.45f);

    [Header("Icons — drawn in place of a chip's label. Empty = the label.")]
    [SerializeField] private Sprite moneyIcon;
    [SerializeField] private Sprite movesIcon;
    [SerializeField] private Sprite discardsIcon;
    [SerializeField] private Sprite roundIcon;

    [Tooltip("How much of a chip's width the icon (or the label standing in for " +
             "it) gets, on the left. The number takes the rest.")]
    [Range(0.15f, 0.6f)][SerializeField] private float iconShare = 0.36f;

    [Tooltip("Space between the chip's edge and what's in it, in canvas units — " +
             "enough to clear the plate's frame.")]
    [Min(0f)][SerializeField] private float chipPadding = 10f;

    [Header("Spacing, in canvas units")]
    [SerializeField] private float chipGap = 10f;

    [SerializeField] private float labelSize = 22f;
    [SerializeField] private float valueSize = 40f;

    /// <summary>
    /// One chip's three parts, kept together so a refresh is a field write and
    /// never a GetComponentInChildren.
    /// </summary>
    private class ChipView
    {
        public RectTransform Rect;
        public TMP_Text Label;
        public TMP_Text Value;
        public Image Icon;
        public StatusChipTap Tap;
    }

    private readonly List<ChipView> views = new();
    private RectTransform self;
    private int shown;

    private void Awake() => self = (RectTransform)transform;

    private void OnEnable()
    {
        GameEvents.StatusChanged += OnStatusChanged;
        GameLayout.Changed += PlaceSelf;
    }

    private void OnDisable()
    {
        GameEvents.StatusChanged -= OnStatusChanged;
        GameLayout.Changed -= PlaceSelf;
    }

    // ⚠️ Start, not OnEnable: every OnEnable runs before any Start, and the
    // layout is resolved between the two. Placing in OnEnable would read a band
    // that doesn't exist yet. Same rule for every widget that sits in a band.
    private void Start() => PlaceSelf();

    private void PlaceSelf()
    {
        GameLayout.Attach(self, LayoutBand.ResourceStrip);
        LayOut();
    }

    private void OnStatusChanged(ModeStatus status)
    {
        var chips = status.Chips;
        int count = chips == null ? 0 : chips.Count;

        // Grow the pool if this mode publishes more than we've seen; never
        // shrink it. Chips are cheap and a round's strip doesn't change size,
        // so churning them would be work for nothing.
        while (views.Count < count) views.Add(BuildChip());

        for (int i = 0; i < views.Count; i++)
        {
            bool used = i < count;
            if (views[i].Rect.gameObject.activeSelf != used)
                views[i].Rect.gameObject.SetActive(used);
            if (used) Draw(views[i], chips[i]);
        }

        if (shown != count)
        {
            shown = count;
            LayOut();
        }
    }

    private void Draw(ChipView view, StatusChip chip)
    {
        var color = chip.Urgent ? urgentColor : valueColor;

        // Field writes only — what a tap will say, read when it happens.
        if (view.Tap != null)
        {
            view.Tap.Title = chip.Title;
            view.Tap.About = chip.About;
        }

        // Runs every frame, so the two SetActives only fire on a change.
        var icon = IconFor(chip.Icon);
        bool showIcon = icon != null && view.Icon != null;
        if (showIcon)
        {
            if (view.Icon.sprite != icon) view.Icon.sprite = icon;
            if (!view.Icon.gameObject.activeSelf) view.Icon.gameObject.SetActive(true);
        }
        else if (view.Icon != null && view.Icon.gameObject.activeSelf)
        {
            view.Icon.gameObject.SetActive(false);
        }

        if (view.Label != null)
        {
            if (view.Label.gameObject.activeSelf == showIcon)
                view.Label.gameObject.SetActive(!showIcon);
            view.Label.text = chip.Label;
        }
        if (view.Value != null)
        {
            view.Value.text = chip.Value;
            view.Value.color = color;
        }
    }

    private Sprite IconFor(StatusIcon kind) => kind switch
    {
        StatusIcon.Money => moneyIcon,
        StatusIcon.Moves => movesIcon,
        StatusIcon.Discards => discardsIcon,
        StatusIcon.Round => roundIcon,
        _ => null,
    };

    /// <summary>
    /// Spreads the visible chips evenly across the band.
    ///
    /// Even shares rather than fit-to-content: the strip is read at a glance and
    /// a chip that moves because a number gained a digit is harder to find than
    /// one that never moves.
    /// </summary>
    private void LayOut()
    {
        if (self == null || shown <= 0) return;

        float width = self.rect.width;
        if (width <= 0f) return;

        float pitch = (width - chipGap * (shown - 1)) / shown;
        if (pitch <= 0f) return;

        for (int i = 0; i < shown && i < views.Count; i++)
        {
            var rect = views[i].Rect;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = new Vector2(i * (pitch + chipGap), 0f);
            rect.offsetMax = new Vector2(i * (pitch + chipGap) + pitch, 0f);
        }
    }

    private ChipView BuildChip()
    {
        var go = new GameObject($"Chip {views.Count}", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);

        var background = go.GetComponent<Image>();
        background.sprite = plateSprite;
        background.color = chipColor;

        // A raycast target so a tap reaches StatusChipTap. The strip is above
        // the board, so this can't stop a word starting on a tile.
        background.raycastTarget = true;
        var tap = go.AddComponent<StatusChipTap>();

        // Sliced: the chip is tall and narrow and the art is 2:1, so a plain
        // stretch would smear the corners and thicken the side outlines.
        if (plateSprite != null)
        {
            background.type = Image.Type.Sliced;
            background.fillCenter = true;
            background.pixelsPerUnitMultiplier = 1f / plateBorderScale;
        }

        // SIDE BY SIDE, not stacked (2026-10-27): the strip is too short for a
        // label over a number, which is what squashed "MOVES" into the frame.
        // The icon sits on the left, and a chip with no icon yet puts its label
        // there instead, small and auto-sized, so it still says what it is.
        // The number takes the rest. Padding clears the plate's frame.
        var label = MakeText(rect, "Label", labelSize, labelColor, TextAlignmentOptions.Center);
        label.enableAutoSizing = true;
        label.fontSizeMin = 8f;
        label.fontSizeMax = labelSize;
        Place(label.rectTransform, 0f, iconShare, chipPadding, 0f);

        var value = MakeText(rect, "Value", valueSize, valueColor, TextAlignmentOptions.Center);
        value.enableAutoSizing = true;
        value.fontSizeMin = 12f;
        value.fontSizeMax = valueSize;
        Place(value.rectTransform, iconShare, 1f, 4f, chipPadding);

        // Off until a chip asks for it.
        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var iconRect = (RectTransform)iconGo.transform;
        iconRect.SetParent(rect, false);
        Place(iconRect, 0f, iconShare, chipPadding, 0f);
        var icon = iconGo.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        iconGo.SetActive(false);

        return new ChipView
        {
            Rect = rect,
            Label = label,
            Value = value,
            Icon = icon,
            Tap = tap,
        };
    }

    /// <summary>
    /// Pins a child to a horizontal slice (xMin..xMax) of its chip, inset by
    /// `left` and `right` at the sides and by chipPadding top and bottom.
    /// </summary>
    private void Place(RectTransform child, float xMin, float xMax, float left, float right)
    {
        child.anchorMin = new Vector2(xMin, 0f);
        child.anchorMax = new Vector2(xMax, 1f);
        child.offsetMin = new Vector2(left, chipPadding);
        child.offsetMax = new Vector2(-right, -chipPadding);
    }

    private static TMP_Text MakeText(RectTransform parent, string name, float size,
                                     Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }
}

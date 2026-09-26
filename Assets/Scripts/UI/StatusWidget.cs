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
    [SerializeField] private Sprite chipSprite;

    [SerializeField] private Color chipColor = new Color(0f, 0f, 0f, 0.35f);
    [SerializeField] private Color labelColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] private Color valueColor = Color.white;

    [Tooltip("Value colour when the mode marks a chip urgent — low on moves.")]
    [SerializeField] private Color urgentColor = new Color(1f, 0.45f, 0.45f);

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
        public Image Bar;
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

        if (view.Label != null) view.Label.text = chip.Label;
        if (view.Value != null)
        {
            view.Value.text = chip.Value;
            view.Value.color = color;
        }

        // A negative fraction means "this chip is a number, not a gauge" — money
        // and the round number have no full to be a fraction of.
        if (view.Bar != null)
        {
            bool hasBar = chip.Fraction >= 0f;
            if (view.Bar.gameObject.activeSelf != hasBar)
                view.Bar.gameObject.SetActive(hasBar);
            if (hasBar)
            {
                view.Bar.fillAmount = Mathf.Clamp01(chip.Fraction);
                view.Bar.color = color;
            }
        }
    }

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
        background.sprite = chipSprite;
        background.color = chipColor;
        background.raycastTarget = false;

        var label = MakeText(rect, "Label", labelSize, labelColor, TextAlignmentOptions.Top);
        label.rectTransform.anchorMin = new Vector2(0f, 0.52f);
        label.rectTransform.anchorMax = new Vector2(1f, 1f);
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;

        var value = MakeText(rect, "Value", valueSize, valueColor, TextAlignmentOptions.Bottom);
        value.rectTransform.anchorMin = new Vector2(0f, 0f);
        value.rectTransform.anchorMax = new Vector2(1f, 0.55f);
        value.rectTransform.offsetMin = Vector2.zero;
        value.rectTransform.offsetMax = Vector2.zero;

        // A thin gauge along the bottom edge, hidden unless the chip has a
        // fraction. Placeholder treatment — it's here so "running out" reads
        // without having to parse "4/20".
        var barGo = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        var barRect = (RectTransform)barGo.transform;
        barRect.SetParent(rect, false);
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.offsetMin = new Vector2(6f, 0f);
        barRect.offsetMax = new Vector2(-6f, 5f);

        var bar = barGo.GetComponent<Image>();
        bar.sprite = chipSprite;
        bar.type = Image.Type.Filled;
        bar.fillMethod = Image.FillMethod.Horizontal;
        bar.raycastTarget = false;

        return new ChipView
        {
            Rect = rect,
            Label = label,
            Value = value,
            Bar = bar,
        };
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

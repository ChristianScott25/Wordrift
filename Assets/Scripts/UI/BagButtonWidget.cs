using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The tile bag — how many tiles are left to draw this round, out of how many
/// the run's bag holds.
///
/// 🚧 It is a BUTTON that does nothing yet. Tapping it should open the bag and
/// show what's still in it, which is a real feature and not this one's job. It
/// is a button now rather than later so it looks tappable from the day it
/// appears — a readout that silently becomes interactive is a feature nobody
/// finds.
/// </summary>
public class BagButtonWidget : MonoBehaviour
{
    [Header("Slots")]
    [SerializeField] private TMP_Text valueLabel;

    [SerializeField] private TMP_Text captionLabel;

    [SerializeField] private Button button;

    [Header("Look")]
    [SerializeField] private string caption = "TILES";

    [Tooltip("Value colour once the bag is nearly out — the round is ending.")]
    [SerializeField] private Color lowColor = new Color(1f, 0.6f, 0.35f);

    [SerializeField] private Color normalColor = Color.white;

    [Tooltip("Fraction of the bag left below which the readout goes warm.")]
    [Range(0f, 1f)][SerializeField] private float lowFraction = 0.15f;

    [Header("Place in band")]
    [Range(0f, 1f)][SerializeField] private float bandXMin = 0.62f;

    [Range(0f, 1f)][SerializeField] private float bandXMax = 0.79f;

    private RectTransform self;

    private void Awake()
    {
        self = (RectTransform)transform;

        // Wired here rather than in the prefab: a persistent listener pointing at
        // a scene object does not survive being saved into a prefab, which is the
        // same reason WordActionsWidget adds its own.
        if (button != null) button.onClick.AddListener(OnPressed);
    }

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

    // Start, not OnEnable — the layout resolves between the two. See GameLayout.
    private void Start()
    {
        PlaceSelf();
        if (captionLabel != null) captionLabel.text = caption;
    }

    private void PlaceSelf() =>
        GameLayout.Attach(self, LayoutBand.RoundHeader, bandXMin, bandXMax);

    private void OnStatusChanged(ModeStatus status)
    {
        if (valueLabel == null) return;

        valueLabel.text = status.BagTotal > 0
            ? $"{status.BagRemaining}/{status.BagTotal}"
            : status.BagRemaining.ToString();

        bool low = status.BagTotal > 0 &&
                   status.BagRemaining <= status.BagTotal * lowFraction;
        valueLabel.color = low ? lowColor : normalColor;
    }

    /// <summary>🚧 Nothing to open yet. Announced so the button isn't silent.</summary>
    private void OnPressed() =>
        Debug.Log("Tile bag: contents view isn't built yet.");
}

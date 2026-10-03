using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The tile bag — how many tiles are left to draw this round, out of how many
/// the run's bag holds.
///
/// Tapping it opens BagViewWidget, which shows what's still in there. This one
/// is only the readout and the hit area; what the bag CONTAINS is the panel's
/// business, and where the tiles come from is the mode's.
/// </summary>
public class BagButtonWidget : MonoBehaviour
{
    [Header("Slots")]
    [SerializeField] private TMP_Text valueLabel;

    [Tooltip("🚧 Optional, and deliberately left unwired: the bag art carries the " +
             "meaning, so a \"TILES\" caption is a second answer to the same " +
             "question. Wire a label here to put it back.")]
    [SerializeField] private TMP_Text captionLabel;

    [SerializeField] private Button button;

    [Tooltip("The panel this opens. A plain reference rather than a bus: there " +
             "is exactly one caller and one listener, both built by the same " +
             "editor script in the same scene.")]
    [SerializeField] private BagViewWidget bagView;

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

    /// <summary>
    /// Opens the bag. Logs rather than failing silently if the panel was never
    /// wired — the reference is written by BagViewSetup, and an unwired button
    /// looks exactly like the stub this replaced.
    /// </summary>
    private void OnPressed()
    {
        if (bagView == null)
        {
            Debug.LogError("The tile bag button has no view to open. Run " +
                           "Word Crush > Set Up Game Layout.", this);
            return;
        }

        bagView.Open();
    }
}

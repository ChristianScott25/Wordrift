using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 🚧 The info and pause buttons at the left of the button band.
///
/// ⚠️ SEPARATE FROM WordActionsWidget ON PURPOSE, even though the drawing puts
/// all four buttons in one row. PLAY and DISCARD are gated by the selection —
/// they go dead when the word isn't playable. These two never are. Sharing a
/// widget would mean a selection event deciding whether you can pause,
/// which is the kind of coupling that only shows up as a bug months later.
///
/// Pause opens the pause screen (PauseViewWidget); it was a settings button
/// until 2026-10-06. Info doesn't open anything yet — it's meant to become a
/// run-info panel (what your bookmarks do, the librarian's rule, the seed) and
/// logs until then rather than doing nothing silently.
/// </summary>
public class SystemButtonsWidget : MonoBehaviour
{
    [Header("Slots")]
    [SerializeField] private Button infoButton;

    [FormerlySerializedAs("settingsButton")]
    [SerializeField] private Button pauseButton;

    [Tooltip("What the pause button opens. Wired by Word Crush > Set Up Game Layout.")]
    [SerializeField] private PauseViewWidget pauseView;

    [Header("Place in band")]
    [Range(0f, 1f)][SerializeField] private float bandXMin = 0f;

    [Range(0f, 1f)][SerializeField] private float bandXMax = 0.28f;

    private RectTransform self;

    private void Awake()
    {
        self = (RectTransform)transform;

        // Added here rather than in the prefab: a persistent listener pointing at
        // a scene object doesn't survive being saved into one.
        if (infoButton != null) infoButton.onClick.AddListener(OnInfo);
        if (pauseButton != null) pauseButton.onClick.AddListener(OnPause);
    }

    private void OnEnable() => GameLayout.Changed += PlaceSelf;

    private void OnDisable() => GameLayout.Changed -= PlaceSelf;

    // Start, not OnEnable — the layout resolves between the two. See GameLayout.
    private void Start() => PlaceSelf();

    private void PlaceSelf()
    {
        // Info is the left END of the row, so its art goes flush left — that's
        // what lines it up with the board's left edge.
        if (GameLayout.Attach(self, LayoutBand.Buttons, bandXMin, bandXMax) && infoButton != null)
            GameLayout.HugEdge((RectTransform)infoButton.transform, toLeft: true);
    }

    private void OnInfo() => Debug.Log("Run info: the panel isn't built yet.");

    private void OnPause()
    {
        if (pauseView == null)
        {
            Debug.LogError("The pause button has no pause view to open. " +
                           "Run Word Crush > Set Up Game Layout.", this);
            return;
        }

        pauseView.Open();
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 🚧 The info and settings buttons at the left of the button band.
///
/// ⚠️ SEPARATE FROM WordActionsWidget ON PURPOSE, even though the drawing puts
/// all four buttons in one row. PLAY and DISCARD are gated by the selection —
/// they go dead when the word isn't playable. These two never are. Sharing a
/// widget would mean a selection event deciding whether you can open settings,
/// which is the kind of coupling that only shows up as a bug months later.
///
/// Neither opens anything yet. Info is meant to become a run-info panel — what
/// your bookmarks do, the librarian's rule, the seed — and settings a settings
/// panel. Both log until then rather than doing nothing silently.
/// </summary>
public class SystemButtonsWidget : MonoBehaviour
{
    [Header("Slots")]
    [SerializeField] private Button infoButton;

    [SerializeField] private Button settingsButton;

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
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);
    }

    private void OnEnable() => GameLayout.Changed += PlaceSelf;

    private void OnDisable() => GameLayout.Changed -= PlaceSelf;

    // Start, not OnEnable — the layout resolves between the two. See GameLayout.
    private void Start() => PlaceSelf();

    private void PlaceSelf() =>
        GameLayout.Attach(self, LayoutBand.Buttons, bandXMin, bandXMax);

    private void OnInfo() => Debug.Log("Run info: the panel isn't built yet.");

    private void OnSettings() => Debug.Log("Settings: the panel isn't built yet.");
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The two things you can do with a selection: play it, or throw it away.
///
/// ⚠️ IT NO LONGER COMES AND GOES. It used to appear only with a selection,
/// which suited a widget floating over the board and does not suit one that owns
/// a band: an empty band reads as a layout bug, and buttons that appear under
/// your thumb mid-drag are buttons you press by accident. Both sit there greyed
/// out instead, which is also how the discard allowance stays readable.
///
/// It is the ONLY way a word gets submitted — lifting your finger off a drag
/// plays nothing. Both
/// buttons are pure obedience: every enable/disable decision arrives inside a
/// SelectionState that the session already worked out, so this widget never
/// consults the dictionary or the discard rule itself.
/// </summary>
public class WordActionsWidget : MonoBehaviour
{
    [SerializeField] private GameSession session;

    [Header("Play")]
    [SerializeField] private Button submitButton;
    [SerializeField] private TMP_Text submitLabel;

    [Tooltip("What the submit button says.")]
    [SerializeField] private string submitText = "PLAY";

    [Header("Discard")]
    [SerializeField] private Button discardButton;
    [SerializeField] private TMP_Text discardLabel;

    [Header("Look")]
    [Tooltip("Faded onto a button that's visible but refusing — an invalid word, " +
             "or a discard bigger than the allowance left.")]
    [SerializeField] private Color disabledTint = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color enabledTint = Color.white;

    [Header("Place in band")]
    [Range(0f, 1f)][SerializeField] private float bandXMin = 0.3f;

    [Range(0f, 1f)][SerializeField] private float bandXMax = 1f;

    private void Awake()
    {
        // The session is a scene object, so a prefab can't carry the reference —
        // it's wired when the widget is placed, and found here if that was missed.
        if (session == null) session = FindFirstObjectByType<GameSession>();
        if (session == null)
            Debug.LogError("WordActionsWidget found no GameSession — the buttons will do nothing.", this);

        // Listeners in code rather than persistent ones on the prefab: the
        // target is a scene object, and a persistent listener to one doesn't
        // survive being saved into a prefab.
        if (submitButton != null) submitButton.onClick.AddListener(OnSubmit);
        if (discardButton != null) discardButton.onClick.AddListener(OnDiscard);
    }

    private void OnEnable()
    {
        GameLayout.Changed += PlaceSelf;
        GameEvents.SelectionChanged += OnSelectionChanged;
        GameEvents.RoundEnded += OnRoundEnded;
    }

    private void OnDisable()
    {
        GameLayout.Changed -= PlaceSelf;
        GameEvents.SelectionChanged -= OnSelectionChanged;
        GameEvents.RoundEnded -= OnRoundEnded;
    }

    // Start, not OnEnable — the layout resolves between the two. See GameLayout.
    private void Start() => PlaceSelf();

    private void PlaceSelf() =>
        GameLayout.Attach((RectTransform)transform, LayoutBand.Buttons, bandXMin, bandXMax);

    private void OnSubmit()
    {
        if (session != null) session.SubmitSelection();
    }

    private void OnDiscard()
    {
        if (session != null) session.DiscardSelection();
    }

    // The round is over: both buttons go dead, but they stay on screen. There is
    // nothing behind them to reveal, and the band would otherwise empty out at
    // exactly the moment the game-over panel wants a settled screen behind it.
    private void OnRoundEnded(RoundSummary summary) => SetIdle();

    private void OnSelectionChanged(SelectionState selection)
    {
        if (selection.IsEmpty)
        {
            SetIdle();
            return;
        }

        SetButton(submitButton, submitLabel, selection.CanSubmit, submitText);

        // The count is on the button because it's the number that decides
        // whether the press will work — "DISCARD 3" against "2 LEFT" is the
        // whole explanation for why it's greyed out.
        SetButton(discardButton, discardLabel, selection.CanDiscard,
                  $"DISCARD {selection.TileCount}" +
                  $"   <size=60%>{selection.DiscardsLeft} LEFT</size>");
    }

    /// <summary>Nothing selected: both buttons present, both refusing.</summary>
    private void SetIdle()
    {
        SetButton(submitButton, submitLabel, false, submitText);
        SetButton(discardButton, discardLabel, false, "DISCARD");
    }

    private void SetButton(Button button, TMP_Text label, bool usable, string text)
    {
        if (button != null) button.interactable = usable;
        if (label == null) return;
        label.text = text;
        label.color = usable ? enabledTint : disabledTint;
    }
}

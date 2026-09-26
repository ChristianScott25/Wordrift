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

    [Tooltip("How big the discard button's second line is, as a percentage of " +
             "the first. It is the allowance, not the action, so it reads under it.")]
    [Range(40, 100)][SerializeField] private int secondLinePercent = 62;

    [Header("Discard")]
    [SerializeField] private Button discardButton;
    [SerializeField] private TMP_Text discardLabel;

    [Header("Place in band")]
    [Range(0f, 1f)][SerializeField] private float bandXMin = 0.3f;

    [Range(0f, 1f)][SerializeField] private float bandXMax = 1f;

    private int discardsLeft;

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
        // Remembered so the button can still say what the allowance is once the
        // round is over and there is no selection left to read it from.
        discardsLeft = selection.DiscardsLeft;

        bool empty = selection.IsEmpty;
        SetButton(submitButton, submitLabel, !empty && selection.CanSubmit, submitText);

        // The count is on the button because it's the number that decides
        // whether the press will work — "DISCARD 3" over "2 LEFT" is the whole
        // explanation for why it's greyed out.
        //
        // ⚠️ TWO LINES, not one. On one line the allowance ran off the end of
        // the button, which is also why the label auto-sizes: "DISCARD 12" over
        // "0 LEFT" is a lot wider than "PLAY".
        SetButton(discardButton, discardLabel, !empty && selection.CanDiscard,
                  DiscardText(empty ? 0 : selection.TileCount));
    }

    private string DiscardText(int tiles)
    {
        string action = tiles > 0 ? $"DISCARD {tiles}" : "DISCARD";
        return $"{action}\n<size={secondLinePercent}%>{discardsLeft} LEFT</size>";
    }

    /// <summary>Nothing left to act on: both buttons present, both refusing.</summary>
    private void SetIdle()
    {
        SetButton(submitButton, submitLabel, false, submitText);
        SetButton(discardButton, discardLabel, false, DiscardText(0));
    }

    /// <summary>
    /// ⚠️ SETS `interactable` AND THE TEXT, AND DELIBERATELY NOT THE COLOUR.
    ///
    /// The button's look comes from its own ColorBlock — green or red when it
    /// will act, grey when it won't — which Unity applies to the plate the moment
    /// `interactable` changes. This used to fade the LABEL instead, from when the
    /// button was a flat rectangle with nothing else to say it was dead. Doing
    /// both would fight: a greyed plate under a faded label is twice as washed
    /// out as either was meant to be.
    /// </summary>
    private static void SetButton(Button button, TMP_Text label, bool usable, string text)
    {
        if (button != null) button.interactable = usable;
        if (label != null) label.text = text;
    }
}

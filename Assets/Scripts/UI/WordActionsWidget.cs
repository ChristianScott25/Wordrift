using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The two things you can do with a selection: play it, or throw it away.
///
/// It appears only when tiles are selected, and it is the ONLY way a word gets
/// submitted — lifting your finger off a drag no longer plays anything. Both
/// buttons are pure obedience: every enable/disable decision arrives inside a
/// SelectionState that the session already worked out, so this widget never
/// consults the dictionary or the discard rule itself.
/// </summary>
public class WordActionsWidget : MonoBehaviour
{
    [Tooltip("The visuals to show/hide with the selection. Must NOT be this " +
             "object — deactivating ourselves would stop us hearing events.")]
    [SerializeField] private GameObject root;

    [SerializeField] private GameSession session;

    [Header("Enter")]
    [SerializeField] private Button submitButton;
    [SerializeField] private TMP_Text submitLabel;

    [Header("Discard")]
    [SerializeField] private Button discardButton;
    [SerializeField] private TMP_Text discardLabel;

    [Header("Look")]
    [Tooltip("Faded onto a button that's visible but refusing — an invalid word, " +
             "or a discard bigger than the allowance left.")]
    [SerializeField] private Color disabledTint = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color enabledTint = Color.white;

    private void Awake()
    {
        if (root == gameObject)
        {
            Debug.LogError("WordActionsWidget's 'root' must be a child object, not itself.", this);
            root = null;
        }

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

        Show(false);
    }

    private void OnEnable()
    {
        GameEvents.SelectionChanged += OnSelectionChanged;
        GameEvents.RoundEnded += OnRoundEnded;
    }

    private void OnDisable()
    {
        GameEvents.SelectionChanged -= OnSelectionChanged;
        GameEvents.RoundEnded -= OnRoundEnded;
    }

    private void OnSubmit()
    {
        if (session != null) session.SubmitSelection();
    }

    private void OnDiscard()
    {
        if (session != null) session.DiscardSelection();
    }

    private void OnRoundEnded(RoundSummary summary) => Show(false);

    private void OnSelectionChanged(SelectionState selection)
    {
        Show(!selection.IsEmpty);
        if (selection.IsEmpty) return;

        SetButton(submitButton, submitLabel, selection.CanSubmit, "ENTER");

        // The count is on the button because it's the number that decides
        // whether the press will work — "DISCARD 3" against "2 LEFT" is the
        // whole explanation for why it's greyed out.
        SetButton(discardButton, discardLabel, selection.CanDiscard,
                  $"DISCARD {selection.TileCount}" +
                  $"   <size=60%>{selection.DiscardsLeft} LEFT</size>");
    }

    private void Show(bool visible)
    {
        if (root != null) root.SetActive(visible);
    }

    private void SetButton(Button button, TMP_Text label, bool usable, string text)
    {
        if (button != null) button.interactable = usable;
        if (label == null) return;
        label.text = text;
        label.color = usable ? enabledTint : disabledTint;
    }
}

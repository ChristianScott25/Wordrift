using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 🚧 The letter picker (2026-10-29): 26 buttons over the board, for an item that
/// AsksForLetter (Correction Fluid). GameSession.UseConsumable opens it with a
/// callback; tapping a letter closes it and calls back, which is when the item
/// actually runs and is spent. Closing it any other way spends nothing.
///
/// Built by Word Crush > Set Up Game Layout (LetterPickerSetup) on the pause
/// view's borrowed shell. The letter buttons are made HERE, at runtime, from one
/// dressed template the setup leaves in the grid — 26 scene objects for one
/// look would be 26 things to keep in step.
///
/// ⚠️ THE BACKDROP IS A FULL-SCREEN RAYCAST TARGET, exactly as in the pause and
/// bag views, and it's right here for the same reason: while it's up the board
/// can't be touched, so the tile the item was dropped on can't move. THE ROOT
/// MUST BE OFF WHEN THIS IS CLOSED — nothing else protects the board.
/// </summary>
public class LetterPickerWidget : MonoBehaviour
{
    [Tooltip("The visuals to show/hide. Must NOT be this object — deactivating " +
             "ourselves would stop us hearing the events that close us.")]
    [SerializeField] private GameObject root;

    [SerializeField] private GameSession session;

    [SerializeField] private Button closeButton;

    [Tooltip("Where the letter buttons go. A GridLayoutGroup lays them out.")]
    [SerializeField] private RectTransform grid;

    [Tooltip("One dressed button, kept switched off, that every letter is copied from.")]
    [SerializeField] private Button letterTemplate;

    [Tooltip("The letters offered, in order.")]
    [SerializeField] private string letters = "abcdefghijklmnopqrstuvwxyz";

    private Action<string> onPick;
    private bool built;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        if (root == gameObject)
        {
            Debug.LogError("LetterPickerWidget's 'root' must be a child object, not itself.", this);
            root = null;
        }

        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (letterTemplate != null) letterTemplate.gameObject.SetActive(false);
        if (root != null) root.SetActive(false);
    }

    private void OnEnable()
    {
        GameEvents.RoundStarted += Close;
        GameEvents.RoundEnded += OnRoundEnded;
    }

    private void OnDisable()
    {
        GameEvents.RoundStarted -= Close;
        GameEvents.RoundEnded -= OnRoundEnded;
        Close();
    }

    private void OnRoundEnded(RoundSummary summary) => Close();

    private void Update()
    {
        // The round stopped under us (see PauseViewWidget.Update for why
        // RoundEnded alone isn't enough).
        if (IsOpen && session != null && !session.IsPlaying) Close();
    }

    /// <summary>
    /// Shows the letters. `pick` runs with the chosen letter, lowercase, after
    /// the picker has closed. False when there's nothing to show.
    /// </summary>
    public bool Open(Action<string> pick)
    {
        if (root == null || pick == null) return false;
        BuildButtons();
        if (!built) return false;

        onPick = pick;
        root.SetActive(true);
        ModalViews.Set(this, true);

        // The layout bands are appended at runtime and sibling order is draw
        // order — same as the bag and pause views.
        transform.SetAsLastSibling();
        return true;
    }

    /// <summary>Puts it away. A change of mind: nothing is called back.</summary>
    public void Close()
    {
        onPick = null;
        if (root != null) root.SetActive(false);
        ModalViews.Set(this, false);
    }

    private void Choose(string letter)
    {
        var pick = onPick;
        Close();
        pick?.Invoke(letter);
    }

    private void BuildButtons()
    {
        if (built) return;
        if (grid == null || letterTemplate == null)
        {
            Debug.LogError("The letter picker isn't wired. Run Word Crush > Set Up Game Layout.", this);
            return;
        }

        foreach (char c in letters)
        {
            string letter = char.ToLowerInvariant(c).ToString();
            var button = Instantiate(letterTemplate, grid);
            button.gameObject.name = $"Letter {letter.ToUpperInvariant()}";
            button.gameObject.SetActive(true);

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = letter.ToUpperInvariant();

            button.onClick.AddListener(() => Choose(letter));
        }
        built = true;
    }
}

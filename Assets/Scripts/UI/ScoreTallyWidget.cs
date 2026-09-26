using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// The scoring readout: POINTS x MULT, live while you select and then walked
/// through a step at a time once you commit.
///
/// Before ENTER it shows only the BASE — what the tiles are worth times the
/// word's length multiplier. Bookmarks are deliberately absent from that: seeing
/// them land afterwards is the payoff, and previewing them would just hand the
/// player the answer.
///
/// After ENTER it replays WordResult.Steps. A run owning no bookmarks produces
/// no steps, so early rounds resolve instantly and the flourish grows only as
/// the player earns things worth watching.
///
/// Both this and GameSession read ScoreTallyTiming, which is the whole reason
/// that class exists: the session waits exactly as long as this animates, and
/// two Inspector copies of the same numbers would eventually disagree.
///
/// ⚠️ IT NEVER HIDES. It used to disappear whenever nothing was selected, which
/// was right when it floated in the middle of the screen and wrong now that it
/// owns a band: a box that empties out leaves a hole, and a box that comes and
/// goes makes the bands around it look like they moved. It rests at 0 x 0
/// instead.
/// </summary>
public class ScoreTallyWidget : MonoBehaviour
{
    [Tooltip("The visuals to show/hide. Must NOT be this object — deactivating " +
             "ourselves would stop us hearing events.")]
    [SerializeField] private GameObject root;

    [SerializeField] private TMP_Text pointsLabel;
    [SerializeField] private TMP_Text multLabel;

    [Tooltip("The product underneath — what this word is actually worth.")]
    [SerializeField] private TMP_Text totalLabel;

    [Tooltip("Names the bookmark firing during the walk-through. Blank the rest of the time.")]
    [SerializeField] private TMP_Text stepLabel;

    [Tooltip("Where the multiplier comes from — \"5 LETTERS\". Without it that " +
             "number is magic; Balatro names the poker hand over its own for the " +
             "same reason.")]
    [SerializeField] private TMP_Text lengthLabel;

    [Header("Place in band")]
    [Range(0f, 1f)][SerializeField] private float bandXMin = 0f;

    [Range(0f, 1f)][SerializeField] private float bandXMax = 1f;

    [Header("Look")]
    [SerializeField] private Color restingColor = Color.white;

    [Tooltip("Flashed on whichever number a step just changed.")]
    [SerializeField] private Color hitColor = new Color(1f, 0.85f, 0.3f);

    private Coroutine tally;

    private void Awake()
    {
        if (root == gameObject)
        {
            Debug.LogError("ScoreTallyWidget's 'root' must be a child object, not itself.", this);
            root = null;
        }
        // The box is permanent now, so this runs once and only guards against a
        // prefab whose root was saved switched off.
        if (root != null && !root.activeSelf) root.SetActive(true);
        DrawResting();
    }

    private void OnEnable()
    {
        GameLayout.Changed += PlaceSelf;
        GameEvents.SelectionChanged += OnSelectionChanged;
        GameEvents.WordSubmitted += OnWordSubmitted;
        GameEvents.RoundStarted += OnRoundStarted;
        GameEvents.RoundEnded += OnRoundEnded;
    }

    private void OnDisable()
    {
        GameLayout.Changed -= PlaceSelf;
        GameEvents.SelectionChanged -= OnSelectionChanged;
        GameEvents.WordSubmitted -= OnWordSubmitted;
        GameEvents.RoundStarted -= OnRoundStarted;
        GameEvents.RoundEnded -= OnRoundEnded;
    }

    // Start, not OnEnable — the layout resolves between the two. See GameLayout.
    private void Start() => PlaceSelf();

    private void PlaceSelf() =>
        GameLayout.Attach((RectTransform)transform, LayoutBand.Score, bandXMin, bandXMax);

    private void OnSelectionChanged(SelectionState selection)
    {
        // A tally in progress owns the display until it's finished — the
        // selection empties the moment ENTER is pressed, and letting that
        // blank the numbers would wipe the score mid-count.
        if (tally != null) return;

        if (selection.IsEmpty)
        {
            DrawResting();
            return;
        }

        Draw(selection.Preview.Points, selection.Preview.Mult, "");
        DrawLength(selection.Word);
    }

    private void OnWordSubmitted(WordResult result)
    {
        if (!result.Accepted) return;
        if (tally != null) StopCoroutine(tally);
        tally = StartCoroutine(Walk(result));
    }

    private void OnRoundStarted() => Clear();

    private void OnRoundEnded(RoundSummary summary) => Clear();

    private void Clear()
    {
        if (tally != null) StopCoroutine(tally);
        tally = null;
        DrawResting();
    }

    /// <summary>The box with nothing selected: zeros, not a blank.</summary>
    private void DrawResting()
    {
        Draw(0, 0f, "");
        DrawLength("");
    }

    /// <summary>
    /// Says where the multiplier came from. Counted in LETTERS, not tiles — a
    /// "ch" tile is two, which is the same distinction ScoreCalculator draws and
    /// the same one two bookmarks once got wrong.
    /// </summary>
    private void DrawLength(string word)
    {
        if (lengthLabel == null) return;

        int letters = string.IsNullOrEmpty(word) ? 0 : word.Length;
        lengthLabel.text = letters == 0 ? "" : $"{letters} LETTER{(letters == 1 ? "" : "S")}";
    }

    /// <summary>
    /// Steps the two numbers from their base to their final values, pausing on
    /// each bookmark. Each ScoreStep already carries the totals AFTER it, so
    /// this only has to display them — no scoring logic lives here.
    /// </summary>
    private IEnumerator Walk(WordResult result)
    {
        Draw(result.Base.Points, result.Base.Mult, "");
        DrawLength(result.Word);

        if (result.HasSteps)
        {
            foreach (var step in result.Steps)
            {
                yield return new WaitForSeconds(ScoreTallyTiming.StepSeconds);
                Draw(step.Points, step.Mult, $"{step.Source}   {step.Detail}");
                Flash(step.Side == ScoreSide.Points ? pointsLabel : multLabel);
            }
        }

        // Held even when nothing fired, so the numbers are readable instead of
        // vanishing the instant ENTER is pressed.
        yield return new WaitForSeconds(ScoreTallyTiming.FinishSeconds);

        // Clear tally BEFORE resting: the session raises an empty selection at
        // the same moment, and whichever of the two lands first must reach the
        // same place. Both rest, so the order can't matter.
        tally = null;
        DrawResting();
    }

    private void Draw(int points, float mult, string step)
    {
        if (pointsLabel != null)
        {
            pointsLabel.text = points.ToString();
            pointsLabel.color = restingColor;
        }
        if (multLabel != null)
        {
            // Bare number — the separator label between the two IS the "x".
            multLabel.text = ScoringContext.Trim(mult);
            multLabel.color = restingColor;
        }
        // Saturated the same way ScoreCalculator.Evaluate is, and for the same
        // reason: this is the number the player WATCHES, so a wrapped one here
        // would show the negative score even when the awarded one was fine.
        if (totalLabel != null) totalLabel.text = ScoreLimits.Clamp((double)points * mult).ToString();
        if (stepLabel != null) stepLabel.text = step;
    }

    private void Flash(TMP_Text label)
    {
        if (label != null) label.color = hitColor;
    }

}

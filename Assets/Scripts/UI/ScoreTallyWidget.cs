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
/// It hides itself the moment the walk-through ends — the same moment the board
/// clears — so a scored word never lingers over the next selection.
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
        Show(false);
    }

    private void OnEnable()
    {
        GameEvents.SelectionChanged += OnSelectionChanged;
        GameEvents.WordSubmitted += OnWordSubmitted;
        GameEvents.RoundStarted += OnRoundStarted;
        GameEvents.RoundEnded += OnRoundEnded;
    }

    private void OnDisable()
    {
        GameEvents.SelectionChanged -= OnSelectionChanged;
        GameEvents.WordSubmitted -= OnWordSubmitted;
        GameEvents.RoundStarted -= OnRoundStarted;
        GameEvents.RoundEnded -= OnRoundEnded;
    }

    private void OnSelectionChanged(SelectionState selection)
    {
        // A tally in progress owns the display until it's finished — the
        // selection empties the moment ENTER is pressed, and letting that
        // blank the numbers would wipe the score mid-count.
        if (tally != null) return;

        if (selection.IsEmpty)
        {
            Show(false);
            return;
        }

        Show(true);
        Draw(selection.Preview.Points, selection.Preview.Mult, "");
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
        Show(false);
    }

    /// <summary>
    /// Steps the two numbers from their base to their final values, pausing on
    /// each bookmark. Each ScoreStep already carries the totals AFTER it, so
    /// this only has to display them — no scoring logic lives here.
    /// </summary>
    private IEnumerator Walk(WordResult result)
    {
        Show(true);
        Draw(result.Base.Points, result.Base.Mult, "");

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

        // Clear tally BEFORE hiding: the session raises an empty selection at
        // the same moment, and whichever of the two lands first must reach the
        // same place. Both hide, so the order can't matter.
        tally = null;
        Show(false);
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
        if (totalLabel != null) totalLabel.text = Mathf.RoundToInt(points * mult).ToString();
        if (stepLabel != null) stepLabel.text = step;
    }

    private void Flash(TMP_Text label)
    {
        if (label != null) label.color = hitColor;
    }

    private void Show(bool visible)
    {
        if (root != null) root.SetActive(visible);
    }
}

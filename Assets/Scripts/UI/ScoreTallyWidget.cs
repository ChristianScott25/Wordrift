using TMPro;
using UnityEngine;

/// <summary>
/// The scoring readout: POINTS x MULT, live while you select and then walked
/// through a beat at a time once you commit.
///
/// Before PLAY it shows what the word's LENGTH is worth and nothing else — not
/// the tiles, not the bookmarks. Watching those arrive is what PLAY is for, and
/// a readout that had already added them up would hand the player the answer
/// before the show ran.
///
/// After PLAY it opens on exactly that same pair (WordResult.Opening, off the
/// same ScoreCalculator.Opening) so nothing jumps, and then draws each beat it
/// is handed as the tiles and everything after them climb on top.
///
/// ⚠️ IT DOES NOT OWN A CLOCK, AND MUST NOT GROW ONE. GameSession.ScoreThenClear
/// steps the beats and raises them; this draws whatever it is given. The two used
/// to run separate timers off a shared constant and could not stay in step — see
/// ScoreTallyTiming for what that cost.
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

    [Tooltip("Names whatever is firing during the walk-through. Blank the rest of the time.")]
    [SerializeField] private TMP_Text stepLabel;

    [Tooltip("Where BOTH numbers come from — \"5 LETTERS\". Without it they are " +
             "magic; Balatro names the poker hand over its own for the same reason.")]
    [SerializeField] private TMP_Text lengthLabel;

    [Header("Place in band")]
    [Range(0f, 1f)][SerializeField] private float bandXMin = 0f;

    [Range(0f, 1f)][SerializeField] private float bandXMax = 1f;

    [Header("Look")]
    [Tooltip("The left-hand number, and every floating number that moved it.")]
    [SerializeField] private Color pointsColor = new Color(0.13f, 0.26f, 0.55f, 1f);

    [Tooltip("The right-hand number, and every floating number that moved it.")]
    [SerializeField] private Color multColor = new Color(0.60f, 0.13f, 0.16f, 1f);

    [Tooltip("Flashed on whichever number a beat just changed.")]
    [SerializeField] private Color hitColor = new Color(1f, 0.85f, 0.3f);

    /// <summary>
    /// A walk is in progress and owns the display.
    ///
    /// ⚠️ THIS IS WHAT STOPS THE SCORE BLANKING THE INSTANT PLAY IS PRESSED.
    /// ChainController.Submit raises the word and then immediately raises an
    /// EMPTY selection, and acting on that would wipe the numbers before the
    /// first beat landed.
    /// </summary>
    private bool walking;

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
        GameEvents.ScoreBeat += OnScoreBeat;
        GameEvents.ScoreWalkEnded += OnWalkEnded;
        GameEvents.RoundStarted += OnRoundStarted;
        GameEvents.RoundEnded += OnRoundEnded;
    }

    private void OnDisable()
    {
        GameLayout.Changed -= PlaceSelf;
        GameEvents.SelectionChanged -= OnSelectionChanged;
        GameEvents.WordSubmitted -= OnWordSubmitted;
        GameEvents.ScoreBeat -= OnScoreBeat;
        GameEvents.ScoreWalkEnded -= OnWalkEnded;
        GameEvents.RoundStarted -= OnRoundStarted;
        GameEvents.RoundEnded -= OnRoundEnded;

        // Or a re-enabled widget would think a walk were still running and
        // ignore every selection from then on.
        walking = false;
    }

    // Start, not OnEnable — the layout resolves between the two. See GameLayout.
    private void Start() => PlaceSelf();

    private void PlaceSelf() =>
        GameLayout.Attach((RectTransform)transform, LayoutBand.Score, bandXMin, bandXMax);

    private void OnSelectionChanged(SelectionState selection)
    {
        // A walk in progress owns the display until it's finished — see `walking`.
        if (walking) return;

        if (selection.IsEmpty)
        {
            DrawResting();
            return;
        }

        // The LENGTH only — see the class note. The tiles are the show.
        Draw(selection.Preview.Points, selection.Preview.Mult, "");
        DrawLength(selection.Word);
    }

    /// <summary>
    /// The opening frame: both numbers at the word's LENGTH, nothing counted yet.
    /// Every tile then climbs on top of it, one beat at a time.
    /// </summary>
    private void OnWordSubmitted(WordResult result)
    {
        if (!result.Accepted) return;

        walking = true;
        Draw(result.Opening.Points, result.Opening.Mult, "");
        DrawLength(result.Word);
    }

    /// <summary>
    /// One beat. Each step already carries the totals AFTER it, so this only has
    /// to display them — no scoring logic lives here, and no timing either.
    /// </summary>
    private void OnScoreBeat(ScoreStep step)
    {
        Draw(step.Points, step.Mult, $"{step.Source}   {step.Detail}");
        Flash(step.Side == ScoreSide.Points ? pointsLabel : multLabel);

        // Only the beats with nothing on screen behind them. Everything else is
        // popped by the widget that owns the thing — it's the only one that knows
        // where that thing is. See ScorePop.
        if (step.Kind == ScoreActor.Mode || step.Kind == ScoreActor.None)
            ScorePop.Show(step.Amount, Inspector.ScreenRectOf((RectTransform)transform), step.Side);
    }

    private void OnWalkEnded()
    {
        walking = false;
        DrawResting();
    }

    private void OnRoundStarted() => Clear();

    private void OnRoundEnded(RoundSummary summary) => Clear();

    private void Clear()
    {
        walking = false;
        DrawResting();
    }

    /// <summary>The box with nothing selected: zeros, not a blank.</summary>
    private void DrawResting()
    {
        Draw(0, 0f, "");
        DrawLength("");
    }

    /// <summary>
    /// Says where the two numbers came from. Counted in LETTERS, not tiles — a
    /// "ch" tile is two, which is the same distinction ScoreCalculator draws and
    /// the same one two bookmarks once got wrong.
    ///
    /// It earns its place twice over now that length is the base POINTS as well
    /// as the multiplier: this one number is where the whole walk starts.
    /// </summary>
    private void DrawLength(string word)
    {
        if (lengthLabel == null) return;

        int letters = string.IsNullOrEmpty(word) ? 0 : word.Length;
        lengthLabel.text = letters == 0 ? "" : $"{letters} LETTER{(letters == 1 ? "" : "S")}";
    }

    private void Draw(int points, float mult, string step)
    {
        if (pointsLabel != null)
        {
            pointsLabel.text = points.ToString();
            pointsLabel.color = pointsColor;
        }
        if (multLabel != null)
        {
            // Bare number — the separator label between the two IS the "x".
            multLabel.text = ScoringContext.Trim(mult);
            multLabel.color = multColor;
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

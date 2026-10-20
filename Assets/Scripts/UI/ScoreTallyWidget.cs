using TMPro;
using UnityEngine;

/// <summary>
/// The scoring readout: a bar of three labelled columns, POINTS x MULT = TOTAL,
/// live while you select and then walked through a beat at a time once you
/// commit. The total is worked out on every draw, so it climbs with the two
/// numbers during the walk.
///
/// Three labels in fixed COLUMNS, each number centred under its caption
/// (2026-10-19, off the mock-up). That replaced one rich-text line, which had
/// replaced five labels at fixed offsets because the gaps between them opened
/// and closed as a 3 became a 30. Columns don't have that problem the way
/// offsets did: each number is centred in a slot sized for the biggest it gets,
/// and auto-sizes down inside it. The colours are the same pointsColor /
/// multColor fields the floating numbers use.
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
///
/// No caption naming each beat ("BOOKEND   x2 MULT") since 2026-10-08, his call:
/// the numbers floating off whatever fired already say it.
/// </summary>
public class ScoreTallyWidget : MonoBehaviour
{
    [Tooltip("The visuals to show/hide. Must NOT be this object — deactivating " +
             "ourselves would stop us hearing events.")]
    [SerializeField] private GameObject root;

    [Tooltip("The POINTS number.")]
    [SerializeField] private TMP_Text pointsLabel;

    [Tooltip("The MULT number.")]
    [SerializeField] private TMP_Text multLabel;

    [Tooltip("The TOTAL number.")]
    [SerializeField] private TMP_Text totalLabel;

    [Header("Place in band")]
    [Range(0f, 1f)][SerializeField] private float bandXMin = 0f;

    [Range(0f, 1f)][SerializeField] private float bandXMax = 1f;

    [Header("Look")]
    [Tooltip("The left-hand number, and every floating number that moved it.")]
    [SerializeField] private Color pointsColor = new Color(0.30f, 0.58f, 1f, 1f);

    [Tooltip("The right-hand number, and every floating number that moved it.")]
    [SerializeField] private Color multColor = new Color(1f, 0.33f, 0.30f, 1f);

    [Tooltip("Flashed on whichever number a beat just changed.")]
    [SerializeField] private Color hitColor = new Color(1f, 0.85f, 0.3f);

    [Tooltip("The total.")]
    [SerializeField] private Color totalColor = Color.white;

    // What's on the line now. Draw is called on every frame of a drag, so the
    // string is only rebuilt when one of these actually changed.
    private int shownPoints = -1;
    private float shownMult = -1f;
    private int shownHit = -1;
    private int points;
    private float mult;
    private int hit;   // 0 none, 1 points, 2 mult — which number a beat just moved


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

        // Loud, because the failure is silent otherwise: Render returns early
        // and the bar just sits there blank. Happens on a prefab built before
        // the three-column bar (2026-10-19) until the setup is re-run.
        if (pointsLabel == null || multLabel == null || totalLabel == null)
            Debug.LogError("ScoreTallyWidget is missing a number label, so the score bar " +
                           "will stay blank. Run Word Crush > Set Up Game Layout.", this);
        DrawResting();
    }

    // So retuning a colour in the Inspector shows on the next draw.
    private void OnValidate() => shownHit = -1;

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
        Draw(selection.Preview.Points, selection.Preview.Mult);
    }

    /// <summary>
    /// The opening frame: both numbers at the word's LENGTH, nothing counted yet.
    /// Every tile then climbs on top of it, one beat at a time.
    /// </summary>
    private void OnWordSubmitted(WordResult result)
    {
        if (!result.Accepted) return;

        walking = true;
        Draw(result.Opening.Points, result.Opening.Mult);
    }

    /// <summary>
    /// One beat. Each step already carries the totals AFTER it, so this only has
    /// to display them — no scoring logic lives here, and no timing either.
    /// </summary>
    private void OnScoreBeat(ScoreStep step)
    {
        Draw(step.Points, step.Mult);
        Flash(step.Side);

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
    private void DrawResting() => Draw(0, 0f);

    private void Draw(int points, float mult)
    {
        this.points = points;
        this.mult = mult;
        hit = 0;
        Render();
    }

    /// <summary>
    /// The number a beat just moved turns the hit colour until the next draw.
    /// </summary>
    private void Flash(ScoreSide side)
    {
        hit = side == ScoreSide.Points ? 1 : 2;
        Render();
    }

    private void Render()
    {
        if (pointsLabel == null || multLabel == null || totalLabel == null) return;
        if (points == shownPoints && Mathf.Approximately(mult, shownMult) && hit == shownHit) return;

        bool numbersMoved = points != shownPoints || !Mathf.Approximately(mult, shownMult);
        shownPoints = points;
        shownMult = mult;
        shownHit = hit;

        // Colours on every change (the flash moves without the numbers moving);
        // text only when a number did, because setting TMP text re-lays it out.
        pointsLabel.color = hit == 1 ? hitColor : pointsColor;
        multLabel.color = hit == 2 ? hitColor : multColor;
        totalLabel.color = totalColor;
        if (!numbersMoved) return;

        // Saturated the same way ScoreCalculator.Evaluate is, and for the same
        // reason: this is the number the player WATCHES, so a wrapped one here
        // would show the negative score even when the awarded one was fine.
        long total = ScoreLimits.Clamp((double)points * mult);

        pointsLabel.text = points.ToString();
        multLabel.text = "x" + ScoringContext.Trim(mult);
        totalLabel.text = total.ToString();
    }
}

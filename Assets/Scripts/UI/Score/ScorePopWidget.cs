using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The floating numbers: a pooled set of labels that rise off whatever just
/// scored and fade out. The one listener on ScorePop.
///
/// ⚠️ ONE POOL, ON THE CANVAS, FOR BOTH RENDER SPACES. The word row's tiles are
/// world-space sprites and the bookmark cards are canvas UI, and a Screen Space -
/// Overlay canvas draws over every world sprite unconditionally — so a
/// world-space label would simply vanish behind the HUD whenever it mattered.
/// Everything is drawn here instead, placed from a SCREEN rectangle, which both
/// kinds of caller can already produce: Inspector.ScreenRectOf has an overload
/// for a RectTransform and one for a world Bounds plus a camera.
///
/// ⚠️ NOTHING HERE MAY BE A RAYCAST TARGET. These float over the board, and
/// ChainController refuses to start a word whenever IsPointerOverGameObject is
/// true on the press frame — so one raycast target in here stops word selection
/// working AT ALL, not degraded, dead, with a symptom that points nowhere near a
/// decorative label. Same standing rule as the info box.
///
/// ⚠️ ITS ROOT MUST NOT LIVE IN A LAYOUT BAND. GameLayout.Attach stretches
/// whatever it is given to fill the band and resets its scale; this sits directly
/// under the canvas, like the info box, and re-asserts itself as the last sibling
/// because the band containers are created at runtime and would otherwise draw
/// over it.
/// </summary>
public class ScorePopWidget : MonoBehaviour
{
    [Tooltip("How big the floating number is drawn, in canvas units.")]
    [SerializeField] private float fontSize = 46f;

    [Tooltip("How far it rises over its life, in canvas units.")]
    [SerializeField] private float rise = 80f;

    [Tooltip("How far above the thing it starts, in canvas units — clear of the " +
             "tile or card rather than printed across its top edge.")]
    [SerializeField] private float gap = 10f;

    [Tooltip("How long one number is on screen. Longer than a beat on purpose: " +
             "two or three overlapping is what a good word should look like.")]
    [SerializeField] private float seconds = 0.6f;

    [Tooltip("Fraction of the life spent fading. The rest is at full strength.")]
    [Range(0.1f, 1f)][SerializeField] private float fadeFraction = 0.45f;

    [Tooltip("A number that moved POINTS. Matches the left-hand readout.")]
    [SerializeField] private Color pointsColor = new Color(0.13f, 0.26f, 0.55f, 1f);

    [Tooltip("A number that moved MULT. Matches the right-hand readout.")]
    [SerializeField] private Color multColor = new Color(0.60f, 0.13f, 0.16f, 1f);

    private readonly List<Pop> pops = new();
    private Canvas canvas;

    private sealed class Pop
    {
        public RectTransform Rect;
        public TMP_Text Label;
        public Vector3 From;
        public float Elapsed;
        public bool Live;
    }

    private void Awake() => canvas = GetComponentInParent<Canvas>();

    private void OnEnable()
    {
        ScorePop.Requested += Show;

        // ⚠️ The ROUND boundaries only, deliberately NOT ScoreWalkEnded. These
        // expire on their own, and the last beat's number is still rising when
        // the walk ends — clearing there would snap it out of existence just as
        // the player looked at it. The round events are here because a round
        // torn down mid-walk would otherwise leave numbers hanging over the next
        // one, which nothing else would ever clean up.
        GameEvents.RoundStarted += ClearAll;
        GameEvents.RoundEnded += OnRoundEnded;
    }

    private void OnDisable()
    {
        ScorePop.Requested -= Show;
        GameEvents.RoundStarted -= ClearAll;
        GameEvents.RoundEnded -= OnRoundEnded;
        ClearAll();
    }

    private void OnRoundEnded(RoundSummary summary) => ClearAll();

    private void Show(string text, Rect screenRect, ScoreSide side)
    {
        // The band containers are built at runtime in GameSession.Awake, so a
        // scene-authored object ends up behind them. Cheap to re-assert, and the
        // info box has already hidden itself by the time a word is being scored.
        transform.SetAsLastSibling();

        var pop = Take();

        pop.Label.text = text;
        pop.Label.color = side == ScoreSide.Points ? pointsColor : multColor;
        pop.Label.fontSize = fontSize;
        pop.Label.alpha = 1f;

        // ⚠️ Screen pixels straight into `position`, NOT anchoredPosition. Both
        // canvases in this game are Screen Space - Overlay, which is precisely
        // what makes a canvas rect's world space the same as screen space —
        // anchoredPosition would need dividing by the scale factor first.
        float scale = canvas == null || canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;
        pop.From = new Vector3(screenRect.center.x, screenRect.yMax + gap * scale, 0f);

        pop.Rect.position = pop.From;
        pop.Elapsed = 0f;
        pop.Live = true;
        pop.Rect.gameObject.SetActive(true);
    }

    private void Update()
    {
        float life = Mathf.Max(0.01f, seconds);
        float scale = canvas == null || canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;

        for (int i = 0; i < pops.Count; i++)
        {
            var pop = pops[i];
            if (!pop.Live) continue;

            pop.Elapsed += Time.deltaTime;
            float t = pop.Elapsed / life;

            if (t >= 1f)
            {
                Retire(pop);
                continue;
            }

            pop.Rect.position = pop.From + new Vector3(0f, rise * scale * t, 0f);

            // Full strength first, then out. A number that started fading
            // immediately reads as half-finished rather than as emphasis.
            float fade = Mathf.Clamp01((t - (1f - fadeFraction)) / fadeFraction);
            pop.Label.alpha = 1f - fade;
        }
    }

    private Pop Take()
    {
        for (int i = 0; i < pops.Count; i++)
            if (!pops[i].Live) return pops[i];

        var made = Make($"Pop {pops.Count}");
        pops.Add(made);
        return made;
    }

    private Pop Make(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0f);          // grows upward off the thing
        rect.sizeDelta = new Vector2(400f, 80f);

        // A new TMP_Text takes whatever font is TMP's DEFAULT when it is created,
        // which Word Crush/Create Font Asset has already set to the game's one
        // typeface. Same reason the info box's chips need no font field.
        var label = go.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Bottom;
        label.fontStyle = FontStyles.Bold;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;                 // see the class warning

        go.SetActive(false);
        return new Pop { Rect = rect, Label = label };
    }

    private static void Retire(Pop pop)
    {
        pop.Live = false;
        pop.Rect.gameObject.SetActive(false);
    }

    /// <summary>
    /// Takes every number off the screen at once. The walk ending calls it, and
    /// so does a round starting or ending — a round torn down mid-walk would
    /// otherwise leave the last word's numbers hanging over the next one.
    /// </summary>
    public void ClearAll()
    {
        for (int i = 0; i < pops.Count; i++)
            if (pops[i].Live) Retire(pops[i]);
    }
}

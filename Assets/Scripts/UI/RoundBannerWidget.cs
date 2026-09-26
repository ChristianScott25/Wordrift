using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The round header — the box in the top-left: who you're up against, what they
/// do, their portrait, and where the round stands.
///
/// It used to be a single line of text under the action buttons announcing the
/// librarian. The banded layout gave it a permanent box of its own, which is why
/// the one string the mode used to build ("LIBRARIAN — THE CENSOR" glued to its
/// power with size markup) is now two fields: the name goes in the title bar and
/// the power in the body under it, and a single label can't draw two places.
///
/// ⚠️ THE BOX IS ALWAYS THE SAME SIZE. On a round with no librarian the name and
/// power come back empty and the placeholder shows instead — the box does not
/// shrink. A header that changed height would move every band under it, so the
/// board would jump between rounds.
/// </summary>
public class RoundBannerWidget : MonoBehaviour
{
    [Header("Slots")]
    [Tooltip("The title bar — the librarian's name, or the round when there isn't one.")]
    [SerializeField] private TMP_Text titleLabel;

    [Tooltip("What this round's librarian does. Blank on an ordinary round.")]
    [SerializeField] private TMP_Text powerLabel;

    [Tooltip("Where the round stands: score over target.")]
    [SerializeField] private TMP_Text scoreLabel;

    [Tooltip("The librarian's portrait. 🚧 A flat colour block until there's art.")]
    [SerializeField] private Image avatar;

    [Header("Look")]
    [Tooltip("How faded the portrait is on a round with NO librarian. The art is " +
             "drawn as it is meant to look, so a librarian round shows it at full " +
             "strength and this only dims the empty case.")]
    // ⚠️ A NEW FIELD ON PURPOSE. This replaced two serialized Colors that were
    // authored for a flat placeholder block — a beige tint and a 12% white. Real
    // art needs neither, but the old VALUES are already saved in the scene, and
    // changing a C# default cannot reach a field that is already serialized. A
    // new NAME has no saved key, so it takes its initializer.
    [Range(0f, 1f)][SerializeField] private float emptyAvatarAlpha = 0.45f;

    [Tooltip("Score colour once the round's target has been reached.")]
    [SerializeField] private Color clearedColor = new Color(0.5f, 1f, 0.5f);

    [SerializeField] private Color normalColor = Color.white;

    [Tooltip("What the title bar says on a round with no librarian.")]
    [SerializeField] private string plainRoundTitle = "ROUND";

    [Header("Place in band")]
    [Tooltip("Left and right edges as a fraction of the header band, which this " +
             "shares with the tile bag and the consumables area.")]
    [Range(0f, 1f)][SerializeField] private float bandXMin = 0f;

    [Range(0f, 1f)][SerializeField] private float bandXMax = 0.6f;

    private RectTransform self;

    private void Awake() => self = (RectTransform)transform;

    private void OnEnable()
    {
        GameEvents.StatusChanged += OnStatusChanged;
        GameLayout.Changed += PlaceSelf;
    }

    private void OnDisable()
    {
        GameEvents.StatusChanged -= OnStatusChanged;
        GameLayout.Changed -= PlaceSelf;
    }

    // Start, not OnEnable — the layout resolves between the two. See GameLayout.
    private void Start() => PlaceSelf();

    private void PlaceSelf() =>
        GameLayout.Attach(self, LayoutBand.RoundHeader, bandXMin, bandXMax);

    /// <summary>
    /// Everything here comes off ModeStatus, including the score — deliberately,
    /// rather than also listening to ScoreChanged. Two sources for one number is
    /// how a header ends up disagreeing with itself for a frame.
    /// </summary>
    private void OnStatusChanged(ModeStatus status)
    {
        bool hasLibrarian = status.HasLibrarian;

        if (titleLabel != null)
            titleLabel.text = hasLibrarian
                ? status.LibrarianName
                : $"{plainRoundTitle} {status.Round}";

        if (powerLabel != null)
            powerLabel.text = hasLibrarian ? status.LibrarianPower : "";

        if (avatar != null)
            avatar.color = hasLibrarian
                ? Color.white
                : new Color(1f, 1f, 1f, emptyAvatarAlpha);

        if (scoreLabel != null)
        {
            scoreLabel.text = $"{status.Score} / {status.Target}";

            // Reached is worth calling out: it's the moment the round stops being
            // about survival and starts being about banking more.
            scoreLabel.color = status.Target > 0 && status.Score >= status.Target
                ? clearedColor
                : normalColor;
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 🚧 The pause screen, opened by the pause button. First version: a close
/// button and MAIN MENU, on the tile bag view's borrowed art.
///
/// ⚠️ THE BACKDROP IS A FULL-SCREEN RAYCAST TARGET, EXACTLY AS IN BagViewWidget,
/// and for the same reason it's correct here: while it's up, ChainController
/// returns on every press frame, so the board can't be touched and the
/// selection survives untouched. So THE ROOT MUST BE OFF WHEN THIS IS CLOSED —
/// nothing else protects the board.
///
/// ⚠️ NOT A Time.timeScale PAUSE, AND IT MUST NOT BECOME ONE — a zero timescale
/// would strand a score walk mid-count forever (see BagViewWidget). A word that
/// was already scoring when this opened finishes underneath it, which is why
/// MAIN MENU waits for GameSession.IsAtRest.
///
/// MAIN MENU KEEPS THE RUN: GameSession.LeaveToMenu saves on the way out and
/// the menu's CONTINUE resumes it. His call, 2026-10-06.
/// </summary>
public class PauseViewWidget : MonoBehaviour
{
    [Tooltip("The visuals to show/hide. Must NOT be this object — deactivating " +
             "ourselves would stop us hearing the events that close us.")]
    [SerializeField] private GameObject root;

    [SerializeField] private GameSession session;

    [SerializeField] private Button closeButton;

    [SerializeField] private Button menuButton;

    [SerializeField] private string menuSceneName = "Main Menu";

    private void Awake()
    {
        if (root == gameObject)
        {
            Debug.LogError("PauseViewWidget's 'root' must be a child object, not itself.", this);
            root = null;
        }

        // Added here rather than in the prefab: a persistent listener pointing
        // at a scene object doesn't survive being saved into one.
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (menuButton != null) menuButton.onClick.AddListener(GoToMenu);

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
    }

    private void OnRoundEnded(RoundSummary summary) => Close();

    private void Update()
    {
        if (root == null || !root.activeSelf) return;

        // ⚠️ The round stopped under us. RoundEnded is NOT enough: a cleared
        // round heads for the shop through ContinueTo, which returns from
        // EndRound before RoundEnded is raised — so a pause opened while the
        // winning word was scoring would otherwise sit there, dead, until the
        // scene swapped out from under it.
        if (session != null && !session.IsPlaying)
        {
            Close();
            return;
        }

        // Greyed while a word is still scoring or the board is still falling —
        // LeaveToMenu would refuse anyway, and a dead button that looks alive
        // reads as broken. Polled because "the board just settled" has no event.
        if (menuButton != null)
            menuButton.interactable = session != null && session.IsAtRest;
    }

    public void Open()
    {
        if (root == null) return;

        // No live round, nothing to pause: after a game over (the pause button
        // is still reachable behind the panel) or on the way to the shop.
        if (session != null && !session.IsPlaying) return;

        if (session == null)
            Debug.LogError("The pause view has no GameSession, so MAIN MENU can't save the run. " +
                           "Run Word Crush > Set Up Game Layout.", this);

        root.SetActive(true);

        // ⚠️ Same as the bag view: the layout bands are appended at runtime,
        // and sibling order is draw order, so without this the panel draws
        // UNDER the HUD.
        transform.SetAsLastSibling();
    }

    /// <summary>Puts it away, and any info box left open under it.</summary>
    public void Close()
    {
        if (root != null) root.SetActive(false);
        Inspector.Hide();
    }

    private void GoToMenu()
    {
        if (session == null) return;
        session.LeaveToMenu(menuSceneName);
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menu actions. Every mode loads the same game scene — only the config differs.
///
/// Wiring a new mode button: drag this object into the button's OnClick, pick
/// MainMenu -> PlayMode (ModeConfig), then drop the mode asset in the slot.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string shopSceneName = "Shop";

    [Tooltip("Shown only when there's a run worth resuming. Hidden otherwise, so " +
             "the menu says nothing about a save that isn't there.")]
    [SerializeField] private Button continueButton;

    [Tooltip("Every mode a save could name. A save records which config it was " +
             "played on by asset name, and this is what that name is looked up " +
             "in — object references don't survive a scene load, and this is the " +
             "one screen that has to resolve one from a string. Word Crush > Set " +
             "Up Main Menu fills it in.")]
    [SerializeField] private ModeConfig[] modes;

    // The save this menu found on enable, kept so Continue doesn't read the disk
    // a second time and risk disagreeing with the button it just showed.
    private RunSaveData saved;

    private void OnEnable()
    {
        saved = RunSave.Read();
        if (continueButton != null)
            continueButton.gameObject.SetActive(ResumableRun() != null);
    }

    /// <summary>Buttons can pass any mode asset directly.</summary>
    public void PlayMode(ModeConfig config) => Play(config);

    /// <summary>
    /// Picks the run back up wherever it was left — mid-round or mid-shop. Wired
    /// to the CONTINUE button, which only exists when this can succeed.
    /// </summary>
    public void Continue()
    {
        var template = ResumableRun();
        if (template == null)
        {
            // The save went away or stopped fitting between enable and the tap.
            Debug.LogWarning("There's no run to continue.");
            if (continueButton != null) continueButton.gameObject.SetActive(false);
            return;
        }

        if (RunState.Resume(template, saved) == null) return;

        ModeSelection.Select(template);
        SceneManager.LoadScene(saved.location == SaveLocation.Shop ? shopSceneName : gameSceneName);
    }

    public void Quit() => Application.Quit();

    /// <summary>
    /// The config the save names, or null when there's nothing to resume. Null
    /// covers a missing save, a mode that isn't in the list, and a mode whose
    /// numbers have been retuned since — all of which mean the same thing here.
    /// </summary>
    private RogueDemoModeConfig ResumableRun()
    {
        if (saved == null || modes == null) return null;

        // Only a run-shaped mode can hold a run — a future arcade mode never
        // writes a save, so it can never be named by one. RunState answers
        // whether the save still FITS, rather than this re-deriving the rule:
        // a CONTINUE button offering something Resume would refuse is exactly
        // the bug that split would cause.
        foreach (var mode in modes)
            if (mode is RogueDemoModeConfig rogue && RunState.CanResume(rogue, saved)) return rogue;

        return null;
    }

    private void Play(ModeConfig config)
    {
        if (config == null)
        {
            Debug.LogError("No mode config set on this button — assign one in the Inspector.", this);
            return;
        }
        // NEW RUN always starts fresh, and says so on the button. Ending the run
        // is also what deletes the save, so an abandoned one can't come back.
        RunState.End();
        ModeSelection.Select(config);
        SceneManager.LoadScene(gameSceneName);
    }
}

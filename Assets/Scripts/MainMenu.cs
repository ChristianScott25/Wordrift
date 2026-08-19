using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Menu actions. Every mode loads the same game scene — only the config differs.
///
/// Wiring a new mode button: drag this object into the button's OnClick, pick
/// MainMenu -> PlayMode (ModeConfig), then drop the mode asset in the slot.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "Game";

    [Tooltip("Used by PlayTimedMode() for a button with no config argument.")]
    [SerializeField] private ModeConfig timedMode;

    /// <summary>Buttons can pass any mode asset directly.</summary>
    public void PlayMode(ModeConfig config) => Play(config);

    /// <summary>Convenience for the default Timed Mode button.</summary>
    public void PlayTimedMode() => Play(timedMode);

    public void Quit() => Application.Quit();

    private void Play(ModeConfig config)
    {
        if (config == null)
        {
            Debug.LogError("No mode config set on this button — assign one in the Inspector.", this);
            return;
        }
        ModeSelection.Select(config);
        SceneManager.LoadScene(gameSceneName);
    }
}

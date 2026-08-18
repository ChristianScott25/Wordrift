using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Main menu actions. Add this component to any object in the Main Menu
/// scene and wire buttons to these methods via the Button's OnClick list.
/// </summary>
public class MainMenu : MonoBehaviour
{
    public void PlayTimedMode()
    {
        SceneManager.LoadScene("Timed Mode");
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gives the main menu a CONTINUE button, and renames PLAY to NEW RUN so the
/// destructive one says what it does.
///
/// Also fills in MainMenu.modes with every mode config in the project — a save
/// records which config it was played on by asset NAME, and this list is what
/// that name gets looked up in. Object references don't survive a scene load, so
/// there has to be one somewhere; this is the screen that needs it.
///
/// Safe to re-run: it creates the button only when it's missing, matches the
/// existing button's look rather than imposing one, and sets absolute positions
/// so running it twice can't walk the layout down the screen.
/// </summary>
public static class MainMenuSetup
{
    private const string ScenePath = "Assets/Scenes/Main Menu.unity";
    private const string ContinueName = "Continue Button";

    // Absolute, not relative: a re-run must land the buttons in the same place.
    private static readonly Vector2 ContinueAt = new Vector2(0f, 70f);
    private static readonly Vector2 PlayAt = new Vector2(0f, -70f);

    [MenuItem("Word Crush/Set Up Main Menu")]
    public static void SetUp()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Run();
    }

    internal static void Run()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            Debug.LogError($"No scene at {ScenePath}.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var menu = Object.FindFirstObjectByType<MainMenu>(FindObjectsInactive.Include);
        if (menu == null)
        {
            Debug.LogError($"{ScenePath} has no MainMenu component.");
            return;
        }

        var play = FindPlayButton(menu);
        if (play == null)
        {
            Debug.LogError("Couldn't find the menu's play button — nothing to sit CONTINUE above.");
            return;
        }

        Relabel(play, "NEW RUN");
        Place(play.gameObject, PlayAt);

        var resume = EnsureContinueButton(menu, play);
        WordCrushSetup.SetRef(menu, "continueButton", resume);
        WireModes(menu);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Main menu ready: CONTINUE added, PLAY relabelled NEW RUN.");
    }

    /// <summary>
    /// The button that starts a run: whichever one isn't ours. Found by
    /// elimination rather than by name, since the scene's play button was
    /// authored by hand and could be called anything.
    /// </summary>
    private static Button FindPlayButton(MainMenu menu)
    {
        foreach (var button in menu.GetComponentsInChildren<Button>(true))
            if (button.name != ContinueName) return button;
        return null;
    }

    private static Button EnsureContinueButton(MainMenu menu, Button play)
    {
        var button = FindContinueButton(menu) ?? BuildContinueButton(play);

        Place(button.gameObject, ContinueAt);
        Relabel(button, "CONTINUE");

        // Re-wired whenever it's missing, not only on the run that creates the
        // button — a button that exists but does nothing is exactly the silent
        // failure a re-runnable setup script is supposed to fix. A persistent
        // listener onto a scene object is fine HERE; the caveat about them not
        // surviving is about saving one into a PREFAB.
        if (button.onClick.GetPersistentEventCount() == 0)
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(button.onClick, menu.Continue);

        return button;
    }

    private static Button FindContinueButton(MainMenu menu)
    {
        foreach (var button in menu.GetComponentsInChildren<Button>(true))
            if (button.name == ContinueName) return button;
        return null;
    }

    private static Button BuildContinueButton(Button play)
    {
        // Copy the play button's look rather than picking colours here: the menu
        // was styled by hand, and a second button in different colours would be
        // an obvious regression the moment that styling changes.
        var playImage = play.GetComponent<Image>();
        var playLabel = play.GetComponentInChildren<TMP_Text>(true);

        var made = WordCrushSetup.MakeButton(
            play.transform.parent, ContinueName, "CONTINUE", ContinueAt,
            playImage != null ? playImage.color : Color.white,
            playLabel != null ? playLabel.color : Color.black);

        var playRect = play.GetComponent<RectTransform>();
        var madeRect = made.GetComponent<RectTransform>();
        madeRect.sizeDelta = playRect.sizeDelta;
        if (playLabel != null)
        {
            var label = made.GetComponentInChildren<TMP_Text>(true);
            label.font = playLabel.font;
            label.fontSize = playLabel.fontSize;
        }

        // Above the play button in the hierarchy as well as on screen, so tab
        // order and the inspector read the way the screen does.
        made.transform.SetSiblingIndex(play.transform.GetSiblingIndex());
        return made;
    }

    private static void Relabel(Button button, string text)
    {
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = text;
    }

    private static void Place(GameObject go, Vector2 position)
    {
        var rect = go.GetComponent<RectTransform>();
        if (rect != null) rect.anchoredPosition = position;
    }

    /// <summary>
    /// Every ModeConfig in the project, so a save naming one by asset name can
    /// find it. Rewritten in full on each run — this is a discovered list, not a
    /// tuned one, so there's nothing here to preserve.
    /// </summary>
    private static void WireModes(MainMenu menu)
    {
        var found = new List<ModeConfig>();
        foreach (var guid in AssetDatabase.FindAssets("t:ModeConfig"))
        {
            var config = AssetDatabase.LoadAssetAtPath<ModeConfig>(AssetDatabase.GUIDToAssetPath(guid));
            if (config != null) found.Add(config);
        }

        var so = new SerializedObject(menu);
        var modes = so.FindProperty("modes");
        if (modes == null)
        {
            Debug.LogError("MainMenu has no serialized 'modes' field.", menu);
            return;
        }

        modes.arraySize = found.Count;
        for (int i = 0; i < found.Count; i++)
            modes.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log($"Main menu can resume {found.Count} mode config(s).");
    }
}

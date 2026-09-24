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

    /// <summary>🚧 TEMPORARY — see the block at the bottom of this file.</summary>
    private const string UnlimitedName = "Unlimited Money Button";

    // Absolute, not relative: a re-run must land the buttons in the same place.
    // The menu's buttons are 300x100, so a pitch of 140 leaves a 40px gap between
    // them. Keep any new one on that pitch.
    private static readonly Vector2 ContinueAt = new Vector2(0f, 70f);
    private static readonly Vector2 PlayAt = new Vector2(0f, -70f);
    private static readonly Vector2 UnlimitedAt = new Vector2(0f, -210f);

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
        bool testMode = EnsureUnlimitedButton(menu, play);
        WireModes(menu);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Main menu ready: CONTINUE added, PLAY relabelled NEW RUN." +
                  (testMode ? " 🚧 UNLIMITED MONEY added below it." : ""));
    }

    /// <summary>
    /// The button that starts a run: whichever one isn't ours. Still found by
    /// elimination rather than by name, since the scene's play button was authored
    /// by hand and could be called anything.
    ///
    /// ⚠️ EVERY BUTTON THIS SCRIPT MAKES HAS TO BE NAMED IN <see cref="IsOurs"/>.
    /// Miss one and this returns it, and the caller then relabels it NEW RUN and
    /// moves it onto the play button — two buttons stacked in one place, both saying
    /// the same thing, one of them starting the wrong mode. That is exactly what
    /// adding the unlimited-money button would have done.
    /// </summary>
    private static Button FindPlayButton(MainMenu menu)
    {
        foreach (var button in menu.GetComponentsInChildren<Button>(true))
            if (!IsOurs(button.name)) return button;
        return null;
    }

    /// <summary>A button this script generated, as opposed to the hand-authored one.</summary>
    private static bool IsOurs(string name) => name == ContinueName || name == UnlimitedName;

    private static Button FindByName(MainMenu menu, string name)
    {
        foreach (var button in menu.GetComponentsInChildren<Button>(true))
            if (button.name == name) return button;
        return null;
    }

    private static Button EnsureContinueButton(MainMenu menu, Button play)
    {
        var button = FindByName(menu, ContinueName) ?? BuildCopyOfPlayButton(
            play, ContinueName, "CONTINUE", ContinueAt, play.transform.GetSiblingIndex());

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


    /// <summary>
    /// A new button matching the hand-authored play button's look. Copying it
    /// rather than picking colours here is deliberate: the menu was styled by hand,
    /// and a generated button in different colours would be an obvious regression
    /// the moment that styling changes.
    /// </summary>
    private static Button BuildCopyOfPlayButton(
        Button play, string name, string label, Vector2 at, int siblingIndex)
    {
        var playImage = play.GetComponent<Image>();
        var playLabel = play.GetComponentInChildren<TMP_Text>(true);

        var made = WordCrushSetup.MakeButton(
            play.transform.parent, name, label, at,
            playImage != null ? playImage.color : Color.white,
            playLabel != null ? playLabel.color : Color.black);

        var playRect = play.GetComponent<RectTransform>();
        var madeRect = made.GetComponent<RectTransform>();
        madeRect.sizeDelta = playRect.sizeDelta;
        if (playLabel != null)
        {
            var text = made.GetComponentInChildren<TMP_Text>(true);
            text.font = playLabel.font;
            text.fontSize = playLabel.fontSize;
        }

        // Placed in the hierarchy the way it sits on screen, so tab order and the
        // inspector read top-to-bottom like the menu does. The caller decides, since
        // CONTINUE goes above the play button and the test mode's button goes below.
        made.transform.SetSiblingIndex(siblingIndex);
        return made;
    }

    // ---- 🚧 TEMPORARY: the unlimited-money test mode's button ------------------
    //
    // A THIRD BUTTON, BELOW NEW RUN, THAT STARTS THE TEST MODE. Unlike the Librarian
    // Lab — which lives in Assets/Editor and so physically cannot ship — this writes a
    // real button into the real menu scene, so it CAN reach a build. That was a
    // deliberate call: it's meant to be startable from the menu like any other mode.
    //
    // Deleting it is this block, the two constants at the top, the call in Run(), the
    // button in Main Menu.unity, Editor/UnlimitedMoneyModeSetup.cs, the asset, and
    // RogueDemoModeConfig.unlimitedMoney with its readers in RunState.
    //
    // ⚠️ UnlimitedName must stay listed in IsOurs, or FindPlayButton starts returning
    // THIS button and relabelling it NEW RUN.

    /// <summary>
    /// Adds (or re-wires) the test mode's button. Silently does nothing when the
    /// asset isn't there, so this menu item still works on a project where
    /// 🚧 Create Unlimited Money Mode has never been run.
    /// </summary>
    private static bool EnsureUnlimitedButton(MainMenu menu, Button play)
    {
        var config = AssetDatabase.LoadAssetAtPath<RogueDemoModeConfig>(
            UnlimitedMoneyModeSetup.AssetPath);

        if (config == null)
        {
            Debug.Log("No Mode_RogueDemo_Unlimited.asset, so no test-mode button. " +
                      "Run Word Crush > 🚧 Create Unlimited Money Mode first if you want one.");
            return false;
        }

        // +1: below the play button, matching where it sits on screen. CONTINUE has
        // already been inserted above it by now, so this index is the settled one.
        var button = FindByName(menu, UnlimitedName) ?? BuildCopyOfPlayButton(
            play, UnlimitedName, "UNLIMITED MONEY", UnlimitedAt,
            play.transform.GetSiblingIndex() + 1);

        Place(button.gameObject, UnlimitedAt);
        Relabel(button, "UNLIMITED MONEY");

        // Rewired from scratch every run rather than only when empty, unlike
        // CONTINUE: this listener carries the mode ASSET as its argument, and a
        // button still pointing at a stale one would start the wrong mode while
        // looking perfectly correct. Cheap to redo, expensive to debug.
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, i);

        UnityEditor.Events.UnityEventTools.AddObjectPersistentListener<ModeConfig>(
            button.onClick, menu.PlayMode, config);

        // ⚠️ AND THEN FIX THE TYPE IT JUST SAVED. AddObjectPersistentListener
        // ignores T and stores the ARGUMENT'S RUNTIME TYPE (it calls argument.GetType()
        // internally), so spelling out <ModeConfig> above buys nothing: the call is
        // written as taking a "RogueDemoModeConfig". PlayMode takes a ModeConfig, and
        // the hand-authored NEW RUN button stores "ModeConfig, Assembly-CSharp".
        // Whether that mismatch still resolves comes down to reflection binder rules —
        // precisely the kind of thing that can behave one way in the Mono editor and
        // another in an IL2CPP iOS build, which is a bug you'd only meet on device.
        // Make the two buttons identical instead of finding out.
        SetObjectArgumentType(button, typeof(ModeConfig));
        return true;
    }

    /// <summary>
    /// Rewrites the saved object-argument type on every persistent call of a button,
    /// so it names the handler's PARAMETER type rather than the argument's runtime
    /// type. See the caller for why that isn't the same thing.
    /// </summary>
    private static void SetObjectArgumentType(Button button, System.Type parameterType)
    {
        // "ModeConfig, Assembly-CSharp" — full name plus the SHORT assembly name, which
        // is the form Unity serializes. AssemblyQualifiedName drags a version, culture
        // and public key token along and does not match what the Inspector writes.
        string typeName = $"{parameterType.FullName}, {parameterType.Assembly.GetName().Name}";

        var so = new SerializedObject(button);
        var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        if (calls == null)
        {
            Debug.LogError("Button.onClick has no m_PersistentCalls.m_Calls — Unity changed the " +
                           "serialized layout, so the test-mode button's argument type is unfixed " +
                           "and the button may do nothing.", button);
            return;
        }

        for (int i = 0; i < calls.arraySize; i++)
        {
            var stored = calls.GetArrayElementAtIndex(i)
                .FindPropertyRelative("m_Arguments.m_ObjectArgumentAssemblyTypeName");
            if (stored != null) stored.stringValue = typeName;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
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

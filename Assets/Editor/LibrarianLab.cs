using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 🚧 TEMPORARY — A TEST TOOL, NOT A GAME MODE.
///
/// Librarian rounds only come round every third round, and which one turns up is
/// drawn from the seed — so seeing a particular boss means playing until it shows
/// up. This forces one onto EVERY round, so a boss can be looked at in ten
/// seconds instead of ten minutes.
///
/// IT CANNOT REACH A BUILD. Everything here is in Assets/Editor, which Unity
/// never compiles into a player. The only trace of it in game code is
/// RunState.LibrarianOverride — a static delegate that is null in a build,
/// because this file is the only thing that ever assigns it.
///
/// Deleting the lab is deleting this file, that field, and the block in
/// RunState.PickLibrarian that reads it. Nothing else knows it exists.
///
/// The choice lives in SessionState, which survives entering play mode (and the
/// domain reload that comes with it) but is forgotten when the editor closes —
/// so a forced librarian can't quietly outlive the session that asked for it.
/// </summary>
public class LibrarianLab : EditorWindow
{
    private const string ForcedKey = "Wordrift.LibrarianLab.Forced";
    private const string GameScenePath = "Assets/Scenes/Game.unity";

    private Vector2 scroll;

    [MenuItem("Word Crush/Librarian Lab")]
    public static void Open() =>
        GetWindow<LibrarianLab>("Librarian Lab").minSize = new Vector2(340f, 260f);

    /// <summary>
    /// Installs the override on every domain reload, which includes the one that
    /// happens on the way into play mode — so it is in place well before
    /// GameSession.Awake asks the run for its librarian.
    /// </summary>
    [InitializeOnLoadMethod]
    private static void Install() => RunState.LibrarianOverride = Resolve;

    /// <summary>
    /// The librarian to force, or null for "play normally". Looked up in the
    /// mode's own pool first, so the lab plays the same asset the run would; a
    /// librarian that isn't in the pool yet is loaded from the project instead,
    /// which is what makes it possible to test one before wiring it up.
    /// </summary>
    private static Librarian Resolve(RogueDemoModeConfig config)
    {
        string name = SessionState.GetString(ForcedKey, "");
        if (string.IsNullOrEmpty(name)) return null;

        if (config?.librarians != null)
            foreach (var librarian in config.librarians)
                if (librarian != null && librarian.name == name) return librarian;

        foreach (var found in All())
            if (found.name == name) return found;

        return null;
    }

    private static List<Librarian> All()
    {
        var found = new List<Librarian>();
        foreach (string guid in AssetDatabase.FindAssets("t:Librarian"))
        {
            var librarian = AssetDatabase.LoadAssetAtPath<Librarian>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (librarian != null) found.Add(librarian);
        }
        found.Sort((a, b) => string.CompareOrdinal(a.Title, b.Title));
        return found;
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Test tool. Forces one librarian onto EVERY round so you can play a " +
            "boss without waiting for round 3. Editor only — it can't reach a build.",
            MessageType.None);

        string forced = SessionState.GetString(ForcedKey, "");
        EditorGUILayout.LabelField(
            "Now forcing",
            string.IsNullOrEmpty(forced) ? "nothing — rounds play normally" : forced,
            EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(forced)))
            if (GUILayout.Button("Stop forcing — play normally"))
            {
                SessionState.SetString(ForcedKey, "");
                Debug.Log("Librarian Lab: rounds are back to normal.");
            }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "PLAY opens the Game scene and presses play, which starts a FRESH run " +
            "at round 1 — so it overwrites whatever run was saved.",
            MessageType.Warning);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var librarian in All())
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(librarian.Title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(librarian.PowerText, EditorStyles.wordWrappedMiniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Play")) Play(librarian);
                    if (GUILayout.Button("Select", GUILayout.Width(60f)))
                        Selection.activeObject = librarian;
                }
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        if (GUILayout.Button("Delete the saved run"))
        {
            RunSave.Delete();
            Debug.Log("Librarian Lab: saved run deleted.");
        }

        // The label reads off SessionState, which another window (or the play
        // mode change below) can move underneath it.
        Repaint();
    }

    private void Play(Librarian librarian)
    {
        SessionState.SetString(ForcedKey, librarian.name);

        // Already running: the round in progress was set up before the choice was
        // made, so it can't retroactively have this librarian. Say so rather than
        // bouncing play mode underneath them.
        if (EditorApplication.isPlaying)
        {
            Debug.Log($"Librarian Lab: forcing {librarian.Title}. It applies to the " +
                      "NEXT round — restart play, or lose the round and press PLAY AGAIN.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
        {
            Debug.LogError($"No scene at {GameScenePath}.");
            return;
        }

        // Straight into the Game scene rather than the menu: opened on its own it
        // falls back to its own mode config and starts a fresh run at round 1,
        // which is exactly the round we want the forced librarian on.
        EditorSceneManager.OpenScene(GameScenePath);
        EditorApplication.isPlaying = true;
    }
}

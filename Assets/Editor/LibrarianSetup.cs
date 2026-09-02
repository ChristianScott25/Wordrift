using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the librarian assets and puts them in every rogue mode's pool,
/// because ScriptableObjects can't be authored from the command line.
///
/// Idempotent, and it touches even less than BookmarkSetup does: the only field
/// it writes on an existing asset is the display NAME. Everything a librarian
/// does is a number on the asset with a sensible default in the class, and a
/// librarian writes its own description from those numbers (Librarian.PowerText)
/// — so there is nothing here that could undo tuning, and no wording to keep in
/// step with a value.
///
/// Adding a fourth: subclass Librarian, then either Create -> Word Crush ->
/// Librarian -> ... in the project window, or add a line below and re-run this.
/// </summary>
public static class LibrarianSetup
{
    private const string LibrarianFolder = "Assets/GameData/Librarians";

    [MenuItem("Word Crush/Create Librarian Assets")]
    public static void CreateLibrarians() => Build();

    internal static List<Librarian> Build()
    {
        if (!AssetDatabase.IsValidFolder(LibrarianFolder))
            AssetDatabase.CreateFolder("Assets/GameData", "Librarians");

        var librarians = new List<Librarian>();

        var grandiloquent = CreateOrLoad<MinimumLengthLibrarian>("Librarian_Grandiloquent");
        Name(grandiloquent, "The Grandiloquent");
        librarians.Add(grandiloquent);

        var cataloguer = CreateOrLoad<DistinctLengthLibrarian>("Librarian_Cataloguer");
        Name(cataloguer, "The Cataloguer");
        librarians.Add(cataloguer);

        var redactor = CreateOrLoad<DiscardLimitLibrarian>("Librarian_Redactor");
        Name(redactor, "The Redactor");
        librarians.Add(redactor);

        int added = AttachToModes(librarians);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Librarians ready in {LibrarianFolder}; {added} added to mode configs.");
        return librarians;
    }

    /// <summary>
    /// The name and nothing else. What each one DOES is left entirely alone, so
    /// re-running this can never walk back a number you turned.
    /// </summary>
    private static void Name(Librarian asset, string displayName)
    {
        asset.displayName = displayName;
        EditorUtility.SetDirty(asset);
    }

    private static T CreateOrLoad<T>(string name) where T : ScriptableObject
    {
        string path = $"{LibrarianFolder}/{name}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    /// <summary>
    /// Adds any librarian a rogue mode config doesn't already list. Only the
    /// rogue config has a pool — a librarian is a rule about a RUN, and a mode
    /// without runs has nowhere to put one.
    /// </summary>
    private static int AttachToModes(List<Librarian> librarians)
    {
        int added = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:RogueDemoModeConfig"))
        {
            var mode = AssetDatabase.LoadAssetAtPath<RogueDemoModeConfig>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (mode == null) continue;

            bool changed = false;
            foreach (var librarian in librarians)
            {
                if (mode.librarians.Contains(librarian)) continue;
                mode.librarians.Add(librarian);
                changed = true;
                added++;
            }
            if (changed) EditorUtility.SetDirty(mode);
        }
        return added;
    }
}

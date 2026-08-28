using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the bookmark assets and puts them in every mode's bookmark pool,
/// because ScriptableObjects can't be authored from the command line.
///
/// Idempotent, and it splits the fields two ways for the same reason
/// TileModifierSetup does: what a bookmark IS (its name and description) is
/// refreshed every run, while its PRICE is a balance number, so a price is
/// written only when the asset's is still 0.
///
/// Adding a fourth is a code + Inspector job — subclass Bookmark, then
/// Create -> Word Crush -> Bookmark -> ..., and add it to a mode's list.
/// </summary>
public static class BookmarkSetup
{
    private const string BookmarkFolder = "Assets/GameData/Bookmarks";

    [MenuItem("Word Crush/Create Bookmark Assets")]
    public static void CreateBookmarks() => Build();

    internal static List<Bookmark> Build()
    {
        if (!AssetDatabase.IsValidFolder(BookmarkFolder))
            AssetDatabase.CreateFolder("Assets/GameData", "Bookmarks");

        var bookmarks = new List<Bookmark>();

        var bookend = CreateOrLoad<BookendBookmark>("Bookend");
        bookend.multiplier = 2f;
        Describe(bookend, "Bookend", "Doubles the word if it starts and ends with the same letter.", price: 12);
        bookmarks.Add(bookend);

        var dejaVu = CreateOrLoad<DejaVuBookmark>("DejaVu");
        dejaVu.bonusPoints = 10;
        Describe(dejaVu, "Deja Vu", "+10 points for a word you already spelled this round.", price: 10);
        bookmarks.Add(dejaVu);

        var vowels = CreateOrLoad<VowelFanaticBookmark>("VowelFanatic");
        vowels.multiplier = 2f;
        vowels.vowels = "aeiou";
        Describe(vowels, "Vowel Fanatic", "Doubles the word if it has more vowels than consonants. Y is a consonant.", price: 14);
        bookmarks.Add(vowels);

        int added = AttachToModes(bookmarks);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Bookmarks ready in {BookmarkFolder}; {added} added to mode configs.");
        return bookmarks;
    }

    private static void Describe(Bookmark asset, string displayName, string description, int price)
    {
        asset.displayName = displayName;
        asset.description = description;

        // Seed only, never re-seed: an unpriced bookmark (0) gets the default,
        // and anything already priced is left as tuned.
        if (asset.price == 0) asset.price = price;

        EditorUtility.SetDirty(asset);
    }

    private static T CreateOrLoad<T>(string name) where T : ScriptableObject
    {
        string path = $"{BookmarkFolder}/{name}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    /// <summary>Adds any bookmark a mode config doesn't already list.</summary>
    private static int AttachToModes(List<Bookmark> bookmarks)
    {
        int added = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:ModeConfig"))
        {
            var mode = AssetDatabase.LoadAssetAtPath<ModeConfig>(AssetDatabase.GUIDToAssetPath(guid));
            if (mode == null) continue;

            bool changed = false;
            foreach (var bookmark in bookmarks)
            {
                if (mode.bookmarks.Contains(bookmark)) continue;
                mode.bookmarks.Add(bookmark);
                changed = true;
                added++;
            }
            if (changed) EditorUtility.SetDirty(mode);
        }
        return added;
    }
}

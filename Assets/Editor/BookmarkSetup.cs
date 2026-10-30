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
/// written only when the asset's is still 0. Card art, likewise, only fills an
/// empty slot.
///
/// Adding another is a code + Inspector job — subclass Bookmark, then
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
        Describe(bookend, "Bookend", "x2 Mult if the word starts and ends with the same letter.", price: 12);
        Art(bookend, "bookmark_book");
        bookmarks.Add(bookend);

        var dejaVu = CreateOrLoad<DejaVuBookmark>("DejaVu");
        dejaVu.bonusPoints = 10;
        Describe(dejaVu, "Deja Vu", "+10 Points for a word you already spelled this round.", price: 10);
        Art(dejaVu, "bookmark_witch");
        bookmarks.Add(dejaVu);

        var vowels = CreateOrLoad<VowelFanaticBookmark>("VowelFanatic");
        vowels.multiplierBonus = 4f;
        vowels.vowels = "aeiou";
        Describe(vowels, "Vowel Fanatic", "+4 Mult if the word has more vowels than consonants. Y is a consonant.", price: 14);
        Art(vowels, "bookmark_sun");
        bookmarks.Add(vowels);

        var spine = CreateOrLoad<SpineBookmark>("Spine");
        spine.multiplier = 2f;
        Describe(spine, "Spine", "x2 Mult if no letter appears twice in the word.", price: 16);
        Art(spine, "bookmark_tree");
        bookmarks.Add(spine);

        // Marginalia and Shorthand are deliberately opposed — one pays for long
        // words, the other only for the shortest legal one. Owning both is close
        // to owning neither, which is why they share a pool.
        var marginalia = CreateOrLoad<MarginaliaBookmark>("Marginalia");
        marginalia.multPerExtraLetter = 1;
        Describe(marginalia, "Marginalia", "+1 Mult for every letter past the minimum word length.", price: 13);
        Art(marginalia, "bookmark_fox");
        bookmarks.Add(marginalia);

        var shorthand = CreateOrLoad<ShorthandBookmark>("Shorthand");
        shorthand.multBonus = 4;
        Describe(shorthand, "Shorthand", "+4 Mult on a word of exactly the minimum length.", price: 13);
        Art(shorthand, "bookmark_ribbon");
        bookmarks.Add(shorthand);

        // ---- 2026-10-29: eleven more, so the Bookshop has something to sell.
        // 🚧 They REUSE the six AI pictures for now (his call, 2026-10-29), so two
        // bookmarks can share a card face until each gets its own.

        var lookingGlass = CreateOrLoad<PalindromeBookmark>("LookingGlass");
        lookingGlass.multiplier = 3f;
        Describe(lookingGlass, "Looking Glass", "x3 Mult if the word reads the same backwards, like POP or LEVEL.", price: 16);
        Art(lookingGlass, "bookmark_witch");
        bookmarks.Add(lookingGlass);

        var doubleSpaced = CreateOrLoad<DoubleLetterBookmark>("DoubleSpaced");
        doubleSpaced.multBonus = 4f;
        Describe(doubleSpaced, "Double Spaced", "+4 Mult if the same letter appears twice in a row, like the TT in LETTER.", price: 12);
        Art(doubleSpaced, "bookmark_ribbon");
        bookmarks.Add(doubleSpaced);

        var verse = CreateOrLoad<RhymeBookmark>("Verse");
        verse.rhymeLetters = 2;
        verse.multPerLink = 2f;
        Describe(verse, "Verse", "+2 Mult if the word ends in the same 2 letters as your last word (not the same word). " +
                                 "Keep the rhyme going for +2 more each word. Resets each round.", price: 14);
        Art(verse, "bookmark_sun");
        bookmarks.Add(verse);

        var sequel = CreateOrLoad<ChainBookmark>("Sequel");
        sequel.bonusPoints = 20;
        Describe(sequel, "Sequel", "+20 Points if the word starts with the letter your last word ended on. Resets each round.", price: 10);
        Art(sequel, "bookmark_book");
        bookmarks.Add(sequel);

        var antiquarian = CreateOrLoad<RareLetterBookmark>("Antiquarian");
        antiquarian.rareLetters = "jkqvxz";
        antiquarian.minimumRare = 2;
        antiquarian.baseMultiplier = 2f;
        antiquarian.extraPerLetter = 1f;
        Describe(antiquarian, "Antiquarian", "x2 Mult if the word has 2 rare letters (J K Q V X Z), and +1x for each one past that.", price: 15);
        Art(antiquarian, "bookmark_fox");
        bookmarks.Add(antiquarian);

        var goldenRatio = CreateOrLoad<FibonacciBookmark>("GoldenRatio");
        goldenRatio.lengths = new[] { 3, 5, 8 };
        goldenRatio.multiplier = 5f;
        Describe(goldenRatio, "Golden Ratio", "x5 Mult on an 8-letter word, if your last two words this round were 3 letters and then 5.", price: 18);
        Art(goldenRatio, "bookmark_tree");
        bookmarks.Add(goldenRatio);

        var dogEar = CreateOrLoad<CornerBookmark>("DogEar");
        dogEar.bonusPoints = 15;
        Describe(dogEar, "Dog-Ear", "+15 Points if the word uses a corner tile.", price: 10);
        Art(dogEar, "bookmark_book");
        bookmarks.Add(dogEar);

        var centerpiece = CreateOrLoad<NoEdgeBookmark>("Centerpiece");
        centerpiece.multBonus = 4f;
        Describe(centerpiece, "Centerpiece", "+4 Mult if no tile in the word touches the edge of the board.", price: 13);
        Art(centerpiece, "bookmark_sun");
        bookmarks.Add(centerpiece);

        var giltEdge = CreateOrLoad<EdgeOnlyBookmark>("GiltEdge");
        giltEdge.multiplier = 2f;
        Describe(giltEdge, "Gilt Edge", "x2 Mult if every tile in the word is on the edge of the board.", price: 14);
        Art(giltEdge, "bookmark_ribbon");
        bookmarks.Add(giltEdge);

        var columnist = CreateOrLoad<EveryLineBookmark>("Columnist");
        columnist.rows = false;
        columnist.multiplier = 3f;
        Describe(columnist, "Columnist", "x3 Mult if the word uses a tile in every column.", price: 16);
        Art(columnist, "bookmark_tree");
        bookmarks.Add(columnist);

        var ruledPaper = CreateOrLoad<EveryLineBookmark>("RuledPaper");
        ruledPaper.rows = true;
        ruledPaper.multiplier = 3f;
        Describe(ruledPaper, "Ruled Paper", "x3 Mult if the word uses a tile in every row.", price: 16);
        Art(ruledPaper, "bookmark_fox");
        bookmarks.Add(ruledPaper);

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

    /// <summary>
    /// 🚧 TEMPORARY AI ART. Fills an EMPTY card-art slot only, so a picture
    /// swapped by hand sticks. Which bookmark got which picture was a guess
    /// (2026-10-27) — none of them were drawn for a particular bookmark.
    /// </summary>
    private static void Art(Bookmark asset, string fileName)
    {
        if (asset.cardArt != null) return;
        asset.cardArt = WordCrushSetup.LoadTempArt(fileName);
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

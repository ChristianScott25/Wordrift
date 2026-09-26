using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Turns the project's `.ttf` into the ONE TMP font asset every label reads —
/// Word Crush/Create Font Asset.
///
/// This exists because the Font Asset Creator is an editor WINDOW: the settings
/// that make a pixel font come out sharp have to be typed into a dialog, which
/// means they're only ever as reproducible as whoever typed them last. Here they
/// are written down, and re-running gives the same asset every time.
///
/// 🚧 **THE FONT IS A PLACEHOLDER AND WILL BE REPLACED.** `Pixel Font.ttf` is
/// third-party and would need its creator attributed, which Christian doesn't
/// want to carry — he intends to draw a replacement. Nothing here is specific to
/// it: point `SourcePath` at a new `.ttf`, re-run, and every label in the game
/// changes, because they all read the one asset this builds.
///
/// ⚠️ SDF, NOT A BITMAP ATLAS, AND THAT IS DELIBERATE. A true pixel font wants
/// to be rendered as a bitmap — crisp, but only at whole multiples of its design
/// size. This game's canvas scales continuously (1080 wide, matched to width),
/// so text almost never lands on a whole multiple and a bitmap atlas would blur
/// and shimmer at exactly the sizes actually used. SDF scales cleanly to any
/// size; with pixel letterforms it still reads as pixel art. Getting true
/// crispness instead would mean locking the canvas to integer scale factors,
/// which fights the band layout and would letterbox most phones.
/// </summary>
public static class FontSetup
{
    private const string SourcePath = "Assets/Fonts/Pixel Font.ttf";
    private const string AssetPath = "Assets/Fonts/Pixel Font SDF.asset";

    // Big enough that the SDF gradient has room to be sharp, small enough that
    // the atlas stays one page for ASCII. Padding is the usual ~1/8 of it;
    // too little and neighbouring glyphs bleed into each other's gradients.
    private const int SamplingPointSize = 72;
    private const int Padding = 8;
    private const int AtlasSize = 1024;

    [MenuItem("Word Crush/Create Font Asset")]
    public static void SetUp()
    {
        var asset = Build();
        if (asset == null) return;

        int applied = ApplyEverywhere(asset);

        EditorUtility.DisplayDialog("Word Crush",
            $"Font asset built from {SourcePath}.\n\n" +
            $"Set as the TMP default, on both mode configs' letterFont, and " +
            $"applied to {applied} existing labels.\n\n" +
            "Run Word Crush/Set Up Game Layout afterwards if you change the HUD.",
            "OK");
    }

    /// <summary>
    /// Creates (or rebuilds) the font asset.
    ///
    /// ⚠️ The material and the atlas texture are SUB-ASSETS of the font asset and
    /// have to be added by hand. TMP_FontAsset.CreateFontAsset makes them in
    /// memory but does not file them anywhere — leave that out and the asset
    /// saves with a null material, which draws nothing and looks like a broken
    /// font rather than a missing step.
    /// </summary>
    private static TMP_FontAsset Build()
    {
        var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
        if (source == null)
        {
            Debug.LogError($"No font at {SourcePath}. Drop a .ttf there first.");
            return null;
        }

        // Rebuilt from scratch rather than topped up: the sampling size and
        // padding are baked into the atlas, so a re-run that kept the old one
        // would silently keep the old settings too.
        AssetDatabase.DeleteAsset(AssetPath);

        var asset = TMP_FontAsset.CreateFontAsset(
            source, SamplingPointSize, Padding, GlyphRenderMode.SDFAA,
            AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic,
            enableMultiAtlasSupport: true);

        if (asset == null)
        {
            Debug.LogError($"TMP could not build a font asset from {SourcePath}.");
            return null;
        }

        asset.name = "Pixel Font SDF";
        AssetDatabase.CreateAsset(asset, AssetPath);

        if (asset.material != null)
        {
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
        }

        if (asset.atlasTextures != null && asset.atlasTextures.Length > 0)
        {
            asset.atlasTextures[0].name = asset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        }

        // Baked now rather than on demand. The atlas is DYNAMIC, so a glyph
        // nobody pre-rendered still appears the first time it's needed — but
        // that first time would be a hitch mid-round, and every character this
        // game shows is known in advance.
        asset.TryAddCharacters(PrintableAscii());

        // ⚠️ Zero softness is what keeps SDF looking like pixel art rather than
        // like a blurred bitmap. The default leaves a soft edge that reads as
        // out-of-focus next to hard-edged sprite work.
        if (asset.material != null && asset.material.HasProperty(ShaderUtilities.ID_OutlineSoftness))
            asset.material.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetPath);

        Debug.Log($"Built {AssetPath} at {SamplingPointSize}pt SDF.");
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
    }

    private static string PrintableAscii()
    {
        var text = new StringBuilder();
        for (char c = ' '; c <= '~'; c++) text.Append(c);

        // The handful this game shows that ASCII doesn't cover: the unlimited
        // money marker, the em dash in a librarian's title, the multiply sign in
        // the score box, and the wild tile's own face.
        text.Append("∞—×∗");
        return text.ToString();
    }

    /// <summary>
    /// Points everything that draws text at the new asset.
    ///
    /// ⚠️ SETTING THE TMP DEFAULT IS NOT ENOUGH, and that is the trap. A
    /// TMP_Text serializes whichever font asset was default WHEN IT WAS CREATED,
    /// so every label already in the project keeps LiberationSans and only new
    /// ones pick this up. The default is set for the labels that don't exist yet;
    /// the existing ones have to be walked and rewritten.
    /// </summary>
    private static int ApplyEverywhere(TMP_FontAsset asset)
    {
        SetTmpDefault(asset);
        SetModeConfigFonts(asset);

        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            count += ApplyToPrefab(AssetDatabase.GUIDToAssetPath(guid), asset);

        // Game LAST so the editor is left on the scene you were working in, not
        // on whichever one happened to be at the end of the list.
        foreach (string path in new[] { "Assets/Scenes/Shop.unity",
                                        "Assets/Scenes/Main Menu.unity",
                                        "Assets/Scenes/Game.unity" })
            count += ApplyToScene(path, asset);

        AssetDatabase.SaveAssets();
        return count;
    }

    private static void SetTmpDefault(TMP_FontAsset asset)
    {
        var settings = Resources.Load<TMP_Settings>("TMP Settings");
        if (settings == null)
        {
            Debug.LogWarning("No TMP Settings asset — new labels will keep the old default.");
            return;
        }

        var so = new SerializedObject(settings);
        var property = so.FindProperty("m_defaultFontAsset");
        if (property == null) return;

        property.objectReferenceValue = asset;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }

    /// <summary>
    /// The board's tiles get it through the mode config, which is the seam that
    /// already existed for exactly this — TileLook.LetterFont. Setting it here
    /// means the tiles and the HUD can't drift onto different typefaces.
    /// </summary>
    private static void SetModeConfigFonts(TMP_FontAsset asset)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ModeConfig"))
        {
            var config = AssetDatabase.LoadAssetAtPath<ModeConfig>(AssetDatabase.GUIDToAssetPath(guid));
            if (config == null) continue;

            var so = new SerializedObject(config);
            var property = so.FindProperty("letterFont");
            if (property == null) continue;

            property.objectReferenceValue = asset;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }
    }

    private static int ApplyToPrefab(string path, TMP_FontAsset asset)
    {
        var contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            int count = Apply(contents.GetComponentsInChildren<TMP_Text>(true), asset);
            if (count > 0) PrefabUtility.SaveAsPrefabAsset(contents, path);
            return count;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static int ApplyToScene(string path, TMP_FontAsset asset)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(path) == null) return 0;

        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            path, UnityEditor.SceneManagement.OpenSceneMode.Single);

        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
            count += Apply(root.GetComponentsInChildren<TMP_Text>(true), asset);

        if (count > 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
        return count;
    }

    private static int Apply(TMP_Text[] labels, TMP_FontAsset asset)
    {
        int count = 0;
        foreach (var label in labels)
        {
            if (label == null || label.font == asset) continue;
            label.font = asset;
            EditorUtility.SetDirty(label);
            count++;
        }
        return count;
    }
}

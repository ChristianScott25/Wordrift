using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the default TileSkin asset and puts it on every mode config, because
/// a ScriptableObject can't be authored from the command line.
///
/// Idempotent: it won't duplicate the skin, won't add it to a mode twice, and
/// won't overwrite colors on a skin that already exists. Touches no scene.
///
/// Adding a second look later is a normal Inspector job — right-click in
/// Assets/GameData/Skins, Create -> Word Crush -> Tile Skin, then add it to a
/// mode's Tile Skins list. Weights decide how often each one turns up.
/// </summary>
public static class TileSkinSetup
{
    private const string SkinFolder = "Assets/GameData/Skins";
    private const string AssetPath = SkinFolder + "/TileSkin_White.asset";
    private const string BaseSpritePath = "Assets/Sprites/Tile - white.png";

    [MenuItem("Word Crush/Create Tile Skin Asset")]
    public static void CreateTileSkin() => Build();

    internal static TileSkin Build()
    {
        if (!AssetDatabase.IsValidFolder(SkinFolder))
            AssetDatabase.CreateFolder("Assets/GameData", "Skins");

        var skin = AssetDatabase.LoadAssetAtPath<TileSkin>(AssetPath);
        bool isNew = skin == null;

        if (isNew)
        {
            skin = ScriptableObject.CreateInstance<TileSkin>();
            AssetDatabase.CreateAsset(skin, AssetPath);
            skin.displayName = "White";
        }

        // Sprites imported as "Multiple" live as sub-assets, so load them all.
        var sprite = AssetDatabase.LoadAllAssetsAtPath(BaseSpritePath).OfType<Sprite>().FirstOrDefault();
        if (sprite != null) skin.baseSprite = sprite;
        else Debug.LogWarning($"No sprite found at {BaseSpritePath} — assign Base Sprite by hand.");

        EditorUtility.SetDirty(skin);

        int added = AttachToModes(skin);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"{(isNew ? "Created" : "Refreshed")} {AssetPath}; added to {added} mode config(s).");
        Selection.activeObject = skin;
        return skin;
    }

    /// <summary>Adds the skin to any mode config that doesn't already list it.</summary>
    private static int AttachToModes(TileSkin skin)
    {
        int added = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:ModeConfig"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mode = AssetDatabase.LoadAssetAtPath<ModeConfig>(path);
            if (mode == null || mode.tileSkins.Contains(skin)) continue;

            mode.tileSkins.Add(skin);
            EditorUtility.SetDirty(mode);
            added++;
        }
        return added;
    }
}

using UnityEditor;
using UnityEngine;

/// <summary>
/// 🚧 TEMPORARY — builds the unlimited-money TEST MODE's asset.
///
/// The shop is the least-exercised part of the game, and seeing all of it means
/// grinding rounds for money — clearing round 1 pays about $19, which is one
/// bookmark. This mode is Rogue Demo with nothing ever too expensive, so the whole
/// shelf can be played through in one visit.
///
/// THE ASSET IS A COPY, NOT A SECOND SET OF NUMBERS. That's the entire point of
/// this script existing rather than the asset being authored by hand: a test mode
/// whose round targets or tile bag had drifted from the real one would be testing
/// a game that doesn't exist. Every field is copied wholesale and then exactly two
/// are overwritten, so a field added to RogueDemoModeConfig next month is carried
/// across the day it's added — the same bargain ModeConfig.Fingerprint takes by
/// building itself out of JsonUtility instead of a hand-written list.
///
/// ⚠️ It is NOT automatic. Re-tune Mode_RogueDemo and the copy keeps the old
/// numbers until this is run again. Running it again is the whole fix.
///
/// UNLIKE THE LIBRARIAN LAB, THIS CAN REACH A BUILD. The lab is editor-only and
/// physically cannot ship; this writes a real asset that a real main-menu button
/// points at. Deleting the test mode is four things:
///   1. Assets/GameData/Mode_RogueDemo_Unlimited.asset
///   2. this file
///   3. RogueDemoModeConfig.unlimitedMoney, RunState.UnlimitedMoney, and the two
///      branches that read it (MoneyText collapses back to $"${Money}")
///   4. the 🚧 block in MainMenuSetup, and the button itself in Main Menu.unity
/// </summary>
public static class UnlimitedMoneyModeSetup
{
    private const string DataFolder = "Assets/GameData";
    private const string SourcePath = DataFolder + "/Mode_RogueDemo.asset";

    /// <summary>
    /// ⚠️ MainMenuSetup builds its button against this exact path and name, and
    /// RunState.CanResume matches a save by asset NAME — so the file name here is
    /// load-bearing in two places, not just a label.
    /// </summary>
    internal const string AssetName = "Mode_RogueDemo_Unlimited";
    internal const string AssetPath = DataFolder + "/" + AssetName + ".asset";

    [MenuItem("Word Crush/🚧 Create Unlimited Money Mode")]
    public static void CreateUnlimitedMoneyMode()
    {
        if (Build() != null)
            Debug.Log("Now run Word Crush > Set Up Main Menu to add its button to the menu.");
    }

    /// <summary>
    /// Creates or re-syncs the asset. Returns null and says why if it can't.
    /// </summary>
    internal static RogueDemoModeConfig Build()
    {
        var real = AssetDatabase.LoadAssetAtPath<RogueDemoModeConfig>(SourcePath);
        if (real == null)
        {
            Debug.LogError($"No {SourcePath} to copy. Run Word Crush > Create Rogue Demo Mode Asset first.");
            return null;
        }

        // The real mode must never have this ticked — every save would be playing
        // with unlimited money and nothing on screen would say so except the $∞.
        if (real.unlimitedMoney)
            Debug.LogError($"{SourcePath} has Unlimited Money TICKED. That's the real mode — " +
                           "untick it in the Inspector. Only the copy should ever have it on.", real);

        var copy = AssetDatabase.LoadAssetAtPath<RogueDemoModeConfig>(AssetPath);
        bool isNew = copy == null;

        if (isNew)
        {
            copy = ScriptableObject.CreateInstance<RogueDemoModeConfig>();
            AssetDatabase.CreateAsset(copy, AssetPath);
        }

        // Every serialized field in one call, subclass fields included. A hand
        // written list is the thing that goes stale; this can't.
        EditorUtility.CopySerialized(real, copy);

        // ⚠️ CopySerialized copies m_Name TOO, so the copy would answer to
        // "Mode_RogueDemo" — and RunState.CanResume matches a save by comparing
        // Template.name against the saved modeConfigName. Both modes would then
        // claim the same save file, and which one won would come down to the order
        // MainMenu.modes happened to be filled in. Put the name back.
        copy.name = AssetName;

        copy.displayName = "Rogue Demo — Unlimited Money";
        copy.unlimitedMoney = true;

        EditorUtility.SetDirty(copy);
        AssetDatabase.SaveAssets();

        // Reimport so the name settles from the file rather than from whatever was
        // left in memory, then read it back — this is the one field that would fail
        // silently, so it's worth proving rather than assuming.
        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        var reloaded = AssetDatabase.LoadAssetAtPath<RogueDemoModeConfig>(AssetPath);
        if (reloaded == null || reloaded.name != AssetName)
            Debug.LogError($"{AssetPath} came back named '{reloaded?.name}', not '{AssetName}'. " +
                           "Saves would be resolved onto the wrong mode — fix the asset's name by hand.");

        Debug.Log(isNew
            ? $"Created {AssetPath} — a copy of Mode_RogueDemo with unlimited money. 🚧 TEST MODE."
            : $"Re-synced {AssetPath} from Mode_RogueDemo. It now matches the real mode's numbers.");

        Selection.activeObject = reloaded != null ? reloaded : copy;
        return reloaded != null ? reloaded : copy;
    }
}

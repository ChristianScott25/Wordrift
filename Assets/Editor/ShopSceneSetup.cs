using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Creates the Shop scene — the stub screen between the rounds of a run — and
/// registers it in Build Settings, because a scene can't be authored from a CLI.
///
/// Safe to re-run: an existing Shop scene is left completely alone (delete the
/// file first to regenerate it) and only Build Settings are refreshed. Unlike
/// the Game scene there's no second wiring pass here — everything ShopScreen
/// points at is an object in the same scene, and those survive the save; it
/// was ASSET references that didn't.
/// </summary>
public static class ShopSceneSetup
{
    internal const string ScenePath = "Assets/Scenes/Shop.unity";

    private static readonly Color BackgroundColor = new Color(0.09f, 0.16f, 0.22f);
    private static readonly Color AccentColor = new Color(1f, 0.75f, 0.1f);

    [MenuItem("Word Crush/Create Shop Scene")]
    public static void Create()
    {
        if (File.Exists(ScenePath))
        {
            Debug.Log($"{ScenePath} already exists — left alone, Build Settings refreshed. " +
                      "Delete the file first if you want it regenerated.");
            RegisterInBuild();
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // The 2D renderer doesn't need the default light.
        var light = Object.FindFirstObjectByType<Light>();
        if (light != null) Object.DestroyImmediate(light.gameObject);

        var cam = Camera.main;
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BackgroundColor;

        var canvasGo = new GameObject("Shop Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        var screenGo = WordCrushSetup.NewUI("ShopScreen", typeof(ShopScreen));
        screenGo.transform.SetParent(canvasGo.transform, false);
        WordCrushSetup.Stretch(screenGo);
        var shop = screenGo.GetComponent<ShopScreen>();

        var title = WordCrushSetup.MakeText(screenGo.transform, "Title", 64, TextAlignmentOptions.Center);
        WordCrushSetup.Anchor(title.gameObject, new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(1000f, 100f));
        title.text = "SHOP";
        title.color = new Color(1f, 1f, 1f, 0.5f);

        var headline = WordCrushSetup.MakeText(screenGo.transform, "Headline", 100, TextAlignmentOptions.Center);
        WordCrushSetup.Anchor(headline.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0f, 280f), new Vector2(1000f, 140f));
        headline.text = "ROUND 1 CLEARED";
        headline.color = AccentColor;

        var detail = WordCrushSetup.MakeText(screenGo.transform, "Detail", 56, TextAlignmentOptions.Center);
        WordCrushSetup.Anchor(detail.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0f, 130f), new Vector2(1000f, 90f));
        detail.text = "NEXT TARGET   180";

        var hint = WordCrushSetup.MakeText(screenGo.transform, "Hint", 40, TextAlignmentOptions.Center);
        WordCrushSetup.Anchor(hint.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(1000f, 80f));
        hint.text = "nothing for sale yet";
        hint.fontStyle = FontStyles.Italic;
        hint.color = new Color(1f, 1f, 1f, 0.35f);

        var button = WordCrushSetup.MakeButton(screenGo.transform, "ContinueButton", "CONTINUE",
            new Vector2(0f, -280f), AccentColor, Color.black);
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(button.onClick, shop.Continue);

        WordCrushSetup.SetRef(shop, "headline", headline);
        WordCrushSetup.SetRef(shop, "detail", detail);

        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        RegisterInBuild();

        Debug.Log($"Created {ScenePath} and added it to Build Settings.");
    }

    /// <summary>
    /// Appends the Shop scene to Build Settings without disturbing what's
    /// already registered — WordCrushSetup.RegisterScenes owns the ordering.
    /// </summary>
    private static void RegisterInBuild()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}

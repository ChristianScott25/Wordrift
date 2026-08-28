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
/// Creates the Shop scene — the screen between the rounds of a run — and
/// registers it in Build Settings, because a scene can't be authored from a CLI.
///
/// Re-runnable, and it TOPS UP an existing shop: anything the screen needs and
/// doesn't have (the money readout, the buy rows) is added, and the layout of
/// the pieces it owns is set. It never deletes a child, never touches text,
/// colour, or font, and never re-wires a button that already has its listener —
/// so the rule of thumb is: positions are the generator's, styling is yours.
///
/// Unlike the Game scene there's no second wiring pass here — everything
/// ShopScreen points at is an object in the same scene, and those survive the
/// save; it was ASSET references that didn't.
/// </summary>
public static class ShopSceneSetup
{
    internal const string ScenePath = "Assets/Scenes/Shop.unity";

    private static readonly Color BackgroundColor = new Color(0.09f, 0.16f, 0.22f);
    private static readonly Color AccentColor = new Color(1f, 0.75f, 0.1f);
    private static readonly Color OfferColor = new Color(1f, 1f, 1f, 0.14f);

    /// <summary>How many buy rows the shop lays out. The stock itself is temporary.</summary>
    private const int OfferRows = 4;

    [MenuItem("Word Crush/Create Shop Scene")]
    public static void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        bool existed = File.Exists(ScenePath);
        var scene = existed
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : BuildNewScene();

        var shop = Object.FindFirstObjectByType<ShopScreen>();
        if (shop == null)
        {
            Debug.LogError($"{ScenePath} has no ShopScreen — delete the scene file and re-run to regenerate it.");
            return;
        }

        int added = EnsureContents(shop);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        RegisterInBuild();

        Debug.Log(existed
            ? $"{ScenePath} updated — {added} element(s) added, existing ones left alone."
            : $"Created {ScenePath} and added it to Build Settings.");
    }

    /// <summary>The parts that are the same every time: camera, canvas, titles, Continue.</summary>
    private static UnityEngine.SceneManagement.Scene BuildNewScene()
    {
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

        var title = WordCrushSetup.MakeText(screenGo.transform, "Title", 64, TextAlignmentOptions.Center);
        WordCrushSetup.Anchor(title.gameObject, new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(1000f, 100f));
        title.text = "SHOP";
        title.color = new Color(1f, 1f, 1f, 0.5f);

        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        return scene;
    }

    /// <summary>
    /// Adds whatever the screen is missing and lays out what it owns. Returns
    /// how many objects it had to create, so the log can say whether a re-run
    /// actually did anything.
    /// </summary>
    private static int EnsureContents(ShopScreen shop)
    {
        var root = shop.transform;
        int added = 0;

        var headline = Ensure(root, "Headline", 100, ref added, t =>
        {
            t.text = "ROUND 1 CLEARED";
            t.color = AccentColor;
        });
        WordCrushSetup.Anchor(headline.gameObject, Center, new Vector2(0f, 640f), new Vector2(1000f, 140f));

        var detail = Ensure(root, "Detail", 52, ref added, t => t.text = "NEXT TARGET   90");
        WordCrushSetup.Anchor(detail.gameObject, Center, new Vector2(0f, 530f), new Vector2(1000f, 80f));

        var money = Ensure(root, "Money", 76, ref added, t =>
        {
            t.text = "$0";
            t.color = AccentColor;
        });
        WordCrushSetup.Anchor(money.gameObject, Center, new Vector2(0f, 410f), new Vector2(1000f, 110f));

        // The old stub's "nothing for sale yet" line is now a lie. Deleting a
        // child isn't this script's job, so it's parked under the button and —
        // only if it still says exactly that — given something true to say.
        var hint = root.Find("Hint");
        if (hint != null)
        {
            WordCrushSetup.Anchor(hint.gameObject, Center, new Vector2(0f, -520f), new Vector2(1000f, 60f));
            var hintText = hint.GetComponent<TMP_Text>();
            if (hintText != null && hintText.text == "nothing for sale yet")
                hintText.text = "buying the same upgrade again costs more";
        }

        var buttons = new Button[OfferRows];
        var labels = new TMP_Text[OfferRows];
        for (int i = 0; i < OfferRows; i++)
        {
            string name = $"Offer{i}";
            var existing = root.Find(name);
            if (existing == null)
            {
                var made = WordCrushSetup.MakeButton(root, name, "—", Vector2.zero, OfferColor, Color.white);
                // Only ever wired at creation: adding the listener again on a
                // re-run would buy twice per click.
                int index = i;
                UnityEditor.Events.UnityEventTools.AddIntPersistentListener(
                    made.onClick, shop.Buy, index);
                existing = made.transform;
                added++;
            }

            buttons[i] = existing.GetComponent<Button>();
            labels[i] = existing.GetComponentInChildren<TMP_Text>();
            WordCrushSetup.Anchor(existing.gameObject, Center,
                new Vector2(0f, 280f - i * 130f), new Vector2(640f, 110f));
        }

        var continueButton = root.Find("ContinueButton");
        if (continueButton == null)
        {
            var made = WordCrushSetup.MakeButton(root, "ContinueButton", "CONTINUE",
                Vector2.zero, AccentColor, Color.black);
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(made.onClick, shop.Continue);
            continueButton = made.transform;
            added++;
        }
        WordCrushSetup.Anchor(continueButton.gameObject, Center, new Vector2(0f, -400f), new Vector2(560f, 140f));

        WordCrushSetup.SetRef(shop, "headline", headline);
        WordCrushSetup.SetRef(shop, "detail", detail);
        WordCrushSetup.SetRef(shop, "moneyLabel", money);
        WireRows(shop, buttons, labels);

        return added;
    }

    private static Vector2 Center => new Vector2(0.5f, 0.5f);

    /// <summary>Finds a text child by name, or makes one and styles it the first time.</summary>
    private static TMP_Text Ensure(Transform root, string name, float size,
                                   ref int added, System.Action<TMP_Text> style)
    {
        var existing = root.Find(name);
        if (existing != null) return existing.GetComponent<TMP_Text>();

        var text = WordCrushSetup.MakeText(root, name, size, TextAlignmentOptions.Center);
        style(text);
        added++;
        return text;
    }

    /// <summary>
    /// Fills ShopScreen.rows — an array of a nested serializable pair, which
    /// SetRef can't reach, so this walks the SerializedProperty itself.
    /// </summary>
    private static void WireRows(ShopScreen shop, Button[] buttons, TMP_Text[] labels)
    {
        var so = new SerializedObject(shop);
        var rows = so.FindProperty("rows");
        if (rows == null)
        {
            Debug.LogError("No serialized field 'rows' on ShopScreen.", shop);
            return;
        }

        rows.arraySize = buttons.Length;
        for (int i = 0; i < buttons.Length; i++)
        {
            var element = rows.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("button").objectReferenceValue = buttons[i];
            element.FindPropertyRelative("label").objectReferenceValue = labels[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(shop);
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

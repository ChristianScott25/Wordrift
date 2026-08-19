using System.Collections.Generic;
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
/// Generates the project's assets, prefabs, and the Game scene from code, so
/// the setup is reproducible and reviewable instead of hand-wired.
///
/// Run it from the menu: Word Crush -> Rebuild Game Scene &amp; Assets.
/// After it runs, everything is a normal asset you can edit by hand — this is
/// a starting point, not a thing you have to keep re-running.
/// </summary>
public static class WordCrushSetup
{
    private const string DataFolder = "Assets/GameData";
    private const string ModifierFolder = "Assets/GameData/Modifiers";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string HudPrefabFolder = "Assets/Prefabs/Hud";
    private const string ScenePath = "Assets/Scenes/Game.unity";
    private const string MenuScenePath = "Assets/Scenes/Main Menu.unity";
    private const string LetterSpriteFolder = "Assets/Resources/Letters";

    private static readonly Color BackgroundColor = new Color(0.09f, 0.16f, 0.22f);
    private static readonly Color AccentColor = new Color(1f, 0.75f, 0.1f);

    [MenuItem("Word Crush/Rebuild Game Scene && Assets")]
    public static void Rebuild()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder(DataFolder);
        EnsureFolder(ModifierFolder);
        EnsureFolder(PrefabFolder);
        EnsureFolder(HudPrefabFolder);
        EnsureFolder("Assets/Scenes");

        var letterSet = BuildLetterSet();
        var modifiers = BuildModifiers();
        var shape = BuildBoardShape();
        BuildTimedMode(shape, letterSet, modifiers);
        BuildMovesMode(shape, letterSet, modifiers);
        BuildTilePrefab();
        BuildHudPrefabs();

        // Flush and re-import BEFORE the scene references any of this. Assets
        // created earlier in the same run aren't reliably assignable until
        // they've been through an import, and the assignment fails silently.
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        BuildGameScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        WireSceneAssets();
        RegisterScenes();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Word Crush: rebuilt GameData assets, prefabs, and Assets/Scenes/Game.unity.");
        EditorUtility.DisplayDialog("Word Crush",
            "Rebuilt:\n\n" +
            "• Assets/GameData (letter set, board shape, mode configs, modifiers)\n" +
            "• Assets/Prefabs (Tile + HUD widgets)\n" +
            "• Assets/Scenes/Game.unity\n\n" +
            "Open Game.unity and press Play, or start from Main Menu.", "OK");
    }

    /// <summary>
    /// Re-points the existing Game scene at the generated assets, without
    /// regenerating anything. Use this if the board comes up empty.
    /// </summary>
    [MenuItem("Word Crush/Repair Scene References")]
    public static void Repair()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        WireSceneAssets();
    }

    /// <summary>
    /// Re-reads every sprite in the letters folder into the LetterSet. Run this
    /// after dropping in replacement art so you don't re-drag 26 slots.
    /// </summary>
    [MenuItem("Word Crush/Rescan Letter Sprites")]
    public static void RescanSprites()
    {
        var letterSet = AssetDatabase.LoadAssetAtPath<LetterSet>($"{DataFolder}/LetterSet_Scrabble.asset");
        if (letterSet == null)
        {
            Debug.LogError("No LetterSet found — run 'Rebuild Game Scene & Assets' first.");
            return;
        }

        int found = 0;
        var so = new SerializedObject(letterSet);
        var entries = so.FindProperty("entries");
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            string letter = entry.FindPropertyRelative("letter").stringValue;
            var sprite = FindLetterSprite(letter);
            if (sprite != null)
            {
                entry.FindPropertyRelative("sprite").objectReferenceValue = sprite;
                found++;
            }
        }
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log($"Word Crush: matched {found} letter sprites from {LetterSpriteFolder}.");
    }

    // ------------------------------------------------------------------ data

    // letter, points, spawn weight (Scrabble values and tile-bag counts)
    private static readonly (string letter, int points, int weight)[] ScrabbleLetters =
    {
        ("a", 1, 9), ("b", 3, 2), ("c", 3, 2), ("d", 2, 4), ("e", 1, 12),
        ("f", 4, 2), ("g", 2, 3), ("h", 4, 2), ("i", 1, 9), ("j", 8, 1),
        ("k", 5, 1), ("l", 1, 4), ("m", 3, 2), ("n", 1, 6), ("o", 1, 8),
        ("p", 3, 2), ("q", 10, 1), ("r", 1, 6), ("s", 1, 4), ("t", 1, 6),
        ("u", 1, 4), ("v", 4, 2), ("w", 4, 2), ("x", 8, 1), ("y", 4, 2),
        ("z", 10, 1),
    };

    private static LetterSet BuildLetterSet()
    {
        var asset = CreateOrLoad<LetterSet>($"{DataFolder}/LetterSet_Scrabble.asset");

        var so = new SerializedObject(asset);
        var entries = so.FindProperty("entries");
        entries.arraySize = ScrabbleLetters.Length;
        for (int i = 0; i < ScrabbleLetters.Length; i++)
        {
            var (letter, points, weight) = ScrabbleLetters[i];
            var entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("letter").stringValue = letter;
            entry.FindPropertyRelative("points").intValue = points;
            entry.FindPropertyRelative("weight").intValue = weight;
            entry.FindPropertyRelative("sprite").objectReferenceValue = FindLetterSprite(letter);
        }
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static Sprite FindLetterSprite(string letter)
    {
        if (string.IsNullOrEmpty(letter)) return null;
        foreach (string extension in new[] { "png", "jpg", "psd", "asset" })
        {
            string path = $"{LetterSpriteFolder}/{letter}.{extension}";
            if (!File.Exists(path)) continue;

            // Sprites imported as "Multiple" live as sub-assets.
            var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprite != null) return sprite;
        }
        return null;
    }

    private static List<TileModifier> BuildModifiers()
    {
        var doubleLetter = CreateOrLoad<LetterMultiplierModifier>($"{ModifierFolder}/DoubleLetter.asset");
        doubleLetter.multiplier = 2;
        doubleLetter.tint = new Color(0.6f, 0.85f, 1f);
        doubleLetter.spawnChance = 0f;   // off until you want special tiles
        EditorUtility.SetDirty(doubleLetter);

        var tripleWord = CreateOrLoad<WordMultiplierModifier>($"{ModifierFolder}/TripleWord.asset");
        tripleWord.multiplier = 3;
        tripleWord.tint = new Color(1f, 0.6f, 0.8f);
        tripleWord.spawnChance = 0f;
        EditorUtility.SetDirty(tripleWord);

        return new List<TileModifier> { doubleLetter, tripleWord };
    }

    private static RectangleBoardShape BuildBoardShape()
    {
        var shape = CreateOrLoad<RectangleBoardShape>($"{DataFolder}/Board_5x10.asset");
        shape.width = 5;
        shape.height = 10;
        EditorUtility.SetDirty(shape);
        return shape;
    }

    private static TimedModeConfig BuildTimedMode(BoardShapeAsset shape, LetterSet letters, List<TileModifier> modifiers)
    {
        var mode = CreateOrLoad<TimedModeConfig>($"{DataFolder}/Mode_Timed.asset");
        mode.displayName = "Timed Mode";
        mode.boardShape = shape;
        mode.letterSet = letters;
        mode.minWordLength = 3;
        mode.roundSeconds = 60f;
        mode.secondsPerWord = 0f;
        mode.secondsPerExtraLetter = 0f;
        mode.tileModifiers = new List<TileModifier>(modifiers);
        EditorUtility.SetDirty(mode);
        return mode;
    }

    private static MovesModeConfig BuildMovesMode(BoardShapeAsset shape, LetterSet letters, List<TileModifier> modifiers)
    {
        var mode = CreateOrLoad<MovesModeConfig>($"{DataFolder}/Mode_Moves.asset");
        mode.displayName = "Moves Mode";
        mode.boardShape = shape;
        mode.letterSet = letters;
        mode.minWordLength = 3;
        mode.moves = 20;
        mode.tileModifiers = new List<TileModifier>(modifiers);
        EditorUtility.SetDirty(mode);
        return mode;
    }

    // --------------------------------------------------------------- prefabs

    private static Tile BuildTilePrefab()
    {
        string path = $"{PrefabFolder}/Tile.prefab";

        var root = new GameObject("Tile", typeof(SpriteRenderer), typeof(Tile));
        var letterRenderer = root.GetComponent<SpriteRenderer>();
        letterRenderer.sortingOrder = 1;

        var badge = new GameObject("Badge", typeof(SpriteRenderer));
        badge.transform.SetParent(root.transform, false);
        var badgeRenderer = badge.GetComponent<SpriteRenderer>();
        badgeRenderer.sortingOrder = 0;
        badgeRenderer.enabled = false;

        var tile = root.GetComponent<Tile>();
        SetRef(tile, "letterRenderer", letterRenderer);
        SetRef(tile, "badgeRenderer", badgeRenderer);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<Tile>();
    }

    private class HudPrefabs
    {
        public GameObject Status;
        public GameObject Score;
        public GameObject CurrentWord;
        public GameObject WordResult;
        public GameObject GameOver;
    }

    private static HudPrefabs BuildHudPrefabs() => new HudPrefabs
    {
        Status = BuildStatusWidget(),
        Score = BuildScoreWidget(),
        CurrentWord = BuildCurrentWordWidget(),
        WordResult = BuildWordResultWidget(),
        GameOver = BuildGameOverPanel(),
    };

    private static GameObject BuildStatusWidget()
    {
        var root = NewUI("StatusWidget", typeof(StatusWidget));
        Anchor(root, new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(400f, 140f));

        var name = MakeText(root.transform, "Label", 40, TextAlignmentOptions.TopLeft);
        Anchor(name.gameObject, new Vector2(0f, 1f), Vector2.zero, new Vector2(400f, 50f));
        name.color = new Color(1f, 1f, 1f, 0.6f);

        var value = MakeText(root.transform, "Value", 80, TextAlignmentOptions.TopLeft);
        Anchor(value.gameObject, new Vector2(0f, 1f), new Vector2(0f, -46f), new Vector2(400f, 100f));

        var widget = root.GetComponent<StatusWidget>();
        SetRef(widget, "nameLabel", name);
        SetRef(widget, "valueLabel", value);
        return SavePrefab(root, $"{HudPrefabFolder}/StatusWidget.prefab");
    }

    private static GameObject BuildScoreWidget()
    {
        var root = NewUI("ScoreWidget", typeof(ScoreWidget));
        Anchor(root, new Vector2(1f, 1f), new Vector2(-40f, -30f), new Vector2(400f, 140f));

        var caption = MakeText(root.transform, "Label", 40, TextAlignmentOptions.TopRight);
        Anchor(caption.gameObject, new Vector2(1f, 1f), Vector2.zero, new Vector2(400f, 50f));
        caption.text = "SCORE";
        caption.color = new Color(1f, 1f, 1f, 0.6f);

        var value = MakeText(root.transform, "Value", 80, TextAlignmentOptions.TopRight);
        Anchor(value.gameObject, new Vector2(1f, 1f), new Vector2(0f, -46f), new Vector2(400f, 100f));
        value.text = "0";

        SetRef(root.GetComponent<ScoreWidget>(), "label", value);
        return SavePrefab(root, $"{HudPrefabFolder}/ScoreWidget.prefab");
    }

    private static GameObject BuildCurrentWordWidget()
    {
        var root = NewUI("CurrentWordWidget", typeof(CurrentWordWidget));
        Anchor(root, new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(1000f, 120f));

        var label = MakeText(root.transform, "Word", 90, TextAlignmentOptions.Top);
        Anchor(label.gameObject, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1000f, 120f));

        SetRef(root.GetComponent<CurrentWordWidget>(), "label", label);
        return SavePrefab(root, $"{HudPrefabFolder}/CurrentWordWidget.prefab");
    }

    private static GameObject BuildWordResultWidget()
    {
        var root = NewUI("WordResultWidget", typeof(WordResultWidget));
        Anchor(root, new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(1000f, 80f));

        var label = MakeText(root.transform, "Result", 50, TextAlignmentOptions.Top);
        Anchor(label.gameObject, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1000f, 80f));

        SetRef(root.GetComponent<WordResultWidget>(), "label", label);
        return SavePrefab(root, $"{HudPrefabFolder}/WordResultWidget.prefab");
    }

    private static GameObject BuildGameOverPanel()
    {
        // The listener lives on the outer object, which stays active; only the
        // inner "Panel" is shown and hidden.
        var outer = NewUI("GameOverPanel", typeof(GameOverPanel));
        Stretch(outer);

        var root = NewUI("Panel", typeof(Image));
        root.transform.SetParent(outer.transform, false);
        Stretch(root);
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);

        var title = MakeText(root.transform, "Title", 100, TextAlignmentOptions.Center);
        Anchor(title.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0f, 320f), new Vector2(1000f, 140f));
        title.text = "ROUND OVER";

        var score = MakeText(root.transform, "Score", 160, TextAlignmentOptions.Center);
        Anchor(score.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1000f, 200f));
        score.color = AccentColor;

        var detail = MakeText(root.transform, "Detail", 44, TextAlignmentOptions.Center);
        Anchor(detail.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1000f, 80f));
        detail.color = new Color(1f, 1f, 1f, 0.7f);

        var panel = outer.GetComponent<GameOverPanel>();
        var again = MakeButton(root.transform, "PlayAgainButton", "PLAY AGAIN",
            new Vector2(0f, -160f), AccentColor, Color.black);
        var menu = MakeButton(root.transform, "MenuButton", "MAIN MENU",
            new Vector2(0f, -330f), new Color(1f, 1f, 1f, 0.15f), Color.white);

        // UnityEvent wiring that survives serialization.
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(
            again.onClick, panel.PlayAgain);
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(
            menu.onClick, panel.BackToMenu);

        SetRef(panel, "root", root);
        SetRef(panel, "titleLabel", title);
        SetRef(panel, "scoreLabel", score);
        SetRef(panel, "detailLabel", detail);

        return SavePrefab(outer, $"{HudPrefabFolder}/GameOverPanel.prefab");
    }

    // ----------------------------------------------------------------- scene

    private static void BuildGameScene()
    {
        // Create the scene FIRST. Asset references resolved before this point
        // don't survive being written into the new scene, so everything the
        // scene points at is loaded after this line (or in WireSceneAssets).
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var hud = new HudPrefabs
        {
            Status = Require<GameObject>($"{HudPrefabFolder}/StatusWidget.prefab"),
            Score = Require<GameObject>($"{HudPrefabFolder}/ScoreWidget.prefab"),
            CurrentWord = Require<GameObject>($"{HudPrefabFolder}/CurrentWordWidget.prefab"),
            WordResult = Require<GameObject>($"{HudPrefabFolder}/WordResultWidget.prefab"),
            GameOver = Require<GameObject>($"{HudPrefabFolder}/GameOverPanel.prefab"),
        };

        if (hud.Status == null || hud.Score == null || hud.CurrentWord == null ||
            hud.WordResult == null || hud.GameOver == null)
        {
            Debug.LogError("Word Crush: aborting scene build — HUD prefabs are missing (see errors above).");
            return;
        }

        // The 2D renderer doesn't need the default light.
        var light = Object.FindFirstObjectByType<Light>();
        if (light != null) Object.DestroyImmediate(light.gameObject);

        var cam = Camera.main;
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BackgroundColor;
        cam.transform.position = new Vector3(0f, 0f, -10f);

        // tilePrefab is assigned later, in WireSceneAssets.
        var boardGo = new GameObject("Board", typeof(Board));
        var board = boardGo.GetComponent<Board>();

        var chainGo = new GameObject("ChainController", typeof(LineRenderer), typeof(ChainController));
        var line = chainGo.GetComponent<LineRenderer>();
        line.material = GetOrCreateLineMaterial();
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.sortingOrder = 5;
        line.useWorldSpace = true;
        line.positionCount = 0;
        var chain = chainGo.GetComponent<ChainController>();
        SetRef(chain, "line", line);

        // HUD
        var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        InstantiateInto(hud.Status, canvasGo.transform);
        InstantiateInto(hud.Score, canvasGo.transform);
        InstantiateInto(hud.CurrentWord, canvasGo.transform);
        InstantiateInto(hud.WordResult, canvasGo.transform);
        var gameOver = InstantiateInto(hud.GameOver, canvasGo.transform);

        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var sessionGo = new GameObject("GameSession", typeof(GameSession));
        var session = sessionGo.GetComponent<GameSession>();
        SetRef(session, "board", board);
        SetRef(session, "chainController", chain);
        SetRef(session, "sceneCamera", cam);
        SetRef(gameOver.GetComponent<GameOverPanel>(), "session", session);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    /// <summary>
    /// Second pass: re-open the saved scene and point it at the generated
    /// assets. Done separately because assignments made while the scene is
    /// being constructed don't survive the save.
    /// </summary>
    private static void WireSceneAssets()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var board = Object.FindFirstObjectByType<Board>();
        var session = Object.FindFirstObjectByType<GameSession>();
        var chain = Object.FindFirstObjectByType<ChainController>();
        if (board == null || session == null || chain == null)
        {
            Debug.LogError("Word Crush: generated scene is missing Board / GameSession / ChainController.");
            return;
        }

        var tilePrefab = Require<GameObject>($"{PrefabFolder}/Tile.prefab");
        if (tilePrefab != null) SetRef(board, "tilePrefab", tilePrefab.GetComponent<Tile>());
        SetRef(session, "fallbackMode", Require<TimedModeConfig>($"{DataFolder}/Mode_Timed.asset"));
        SetRef(session, "wordList", Require<TextAsset>("Assets/Resources/wordlist.txt"));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        VerifyScene(board, session, chain);
    }

    /// <summary>
    /// Re-reads the saved scene's key references. Catches the case where an
    /// assignment looked fine in memory but didn't survive serialization.
    /// </summary>
    private static void VerifyScene(Board board, GameSession session, ChainController chain)
    {
        var missing = new List<string>();
        Check(board, "tilePrefab", missing);
        Check(session, "board", missing);
        Check(session, "chainController", missing);
        Check(session, "sceneCamera", missing);
        Check(session, "wordList", missing);
        Check(session, "fallbackMode", missing);
        Check(chain, "line", missing);

        if (missing.Count == 0)
        {
            Debug.Log("Word Crush: scene references verified.");
            return;
        }
        Debug.LogError("Word Crush: these references are still unset — " + string.Join(", ", missing));
    }

    private static void Check(Object target, string field, List<string> missing)
    {
        var property = new SerializedObject(target).FindProperty(field);
        if (property == null || property.objectReferenceValue == null)
            missing.Add($"{target.GetType().Name}.{field}");
    }

    private static Material GetOrCreateLineMaterial()
    {
        string path = $"{DataFolder}/ChainLine.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;

        var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        material = new Material(shader) { color = AccentColor };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void RegisterScenes()
    {
        var wanted = new List<string>();
        if (File.Exists(MenuScenePath)) wanted.Add(MenuScenePath);
        wanted.Add(ScenePath);

        EditorBuildSettings.scenes = wanted
            .Select(path => new EditorBuildSettingsScene(path, true))
            .ToArray();
    }

    // --------------------------------------------------------------- helpers

    private static GameObject NewUI(string name, params System.Type[] components)
    {
        var go = new GameObject(name, components);
        if (go.GetComponent<RectTransform>() == null) go.AddComponent<RectTransform>();
        return go;
    }

    private static void Anchor(GameObject go, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;
    }

    private static void Stretch(GameObject go)
    {
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI MakeText(Transform parent, string name, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        text.text = "";
        return text;
    }

    private static Button MakeButton(Transform parent, string name, string label, Vector2 offset, Color background, Color textColor)
    {
        var go = NewUI(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Anchor(go, new Vector2(0.5f, 0.5f), offset, new Vector2(560f, 140f));
        go.GetComponent<Image>().color = background;

        var text = MakeText(go.transform, "Label", 56, TextAlignmentOptions.Center);
        Stretch(text.gameObject);
        text.text = label;
        text.color = textColor;

        return go.GetComponent<Button>();
    }

    private static GameObject SavePrefab(GameObject go, string path)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static GameObject InstantiateInto(GameObject prefab, Transform parent)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(parent, false);
        return instance;
    }

    /// <summary>
    /// Assigns a serialized reference and verifies it actually stuck. Silent
    /// failures here are how you end up with an empty board and no error.
    /// </summary>
    private static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogError($"No serialized field '{field}' on {target.GetType().Name}.", target);
            return;
        }
        if (value == null)
        {
            Debug.LogError($"Nothing to assign to '{field}' on {target.GetType().Name} — the asset is missing.", target);
            return;
        }

        property.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (PrefabUtility.IsPartOfPrefabInstance(target))
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        EditorUtility.SetDirty(target);

        so.Update();
        if (so.FindProperty(field).objectReferenceValue != value)
            Debug.LogError($"Failed to assign '{field}' on {target.GetType().Name} — reference did not persist.", target);
    }

    /// <summary>Loads an asset by path, complaining loudly if it isn't there.</summary>
    private static T Require<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            Debug.LogError($"Expected a {typeof(T).Name} at {path} but found none.");
        return asset;
    }

    private static T CreateOrLoad<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}

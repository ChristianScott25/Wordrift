using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Puts a BoardBackground on the Board in the Game scene and points it at the
/// square sprite. Needed because adding a component and assigning a reference in
/// a scene can't be done from the command line.
///
/// Idempotent: it won't add a second component, and it only assigns the sprite
/// when the slot is empty, so a swapped sprite or a re-tuned colour survives.
/// The board colour, border width and draw order are all Inspector fields on the
/// component — this command never touches them after creation.
/// </summary>
public static class BoardBackgroundSetup
{
    private const string ScenePath = "Assets/Scenes/Game.unity";
    private const string SquareSpritePath = "Assets/Sprites/White Square.png";

    [MenuItem("Word Crush/Set Up Board Background")]
    public static void SetUpFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SetUp();
    }

    internal static void SetUp()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var board = Object.FindFirstObjectByType<Board>();
        if (board == null)
        {
            Debug.LogError($"No Board in {ScenePath} — run 'Rebuild Game Scene & Assets' first.");
            return;
        }

        var background = board.GetComponent<BoardBackground>();
        bool isNew = background == null;
        if (isNew) background = board.gameObject.AddComponent<BoardBackground>();

        var serialized = new SerializedObject(background);
        var sprite = serialized.FindProperty("cellSprite");
        if (sprite == null)
        {
            Debug.LogError("BoardBackground has no 'cellSprite' field — did the scripts compile?");
            return;
        }

        // Only fill an empty slot: a sprite swapped by hand should stick.
        if (sprite.objectReferenceValue == null)
        {
            var square = AssetDatabase.LoadAllAssetsAtPath(SquareSpritePath).OfType<Sprite>().FirstOrDefault();
            if (square == null)
            {
                Debug.LogError($"No sprite found at {SquareSpritePath}.");
                return;
            }

            sprite.objectReferenceValue = square;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            serialized.Update();
            if (serialized.FindProperty("cellSprite").objectReferenceValue != square)
            {
                Debug.LogError("Failed to assign 'cellSprite' — reference did not persist.");
                return;
            }
        }

        EditorUtility.SetDirty(background);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log(isNew
            ? $"Added BoardBackground to the Board in {ScenePath}. Colour, border width and draw order are on the component."
            : $"BoardBackground already on the Board in {ScenePath} — left its settings alone.");
    }
}

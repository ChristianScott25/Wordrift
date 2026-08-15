using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Builds the whole UI in code (no scene setup needed): timer, score,
/// current-word preview, and a game-over panel with a restart button.
/// Uses the built-in legacy Text so no TMP essentials import is required.
/// </summary>
public class GameHUD : MonoBehaviour
{
    private Text timerText;
    private Text scoreText;
    private Text wordText;
    private Text lastWordText;
    private GameObject gameOverPanel;
    private Text finalScoreText;
    private Font font;

    public void Init(GameManager game)
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.transform.SetParent(transform);
        }

        var root = canvasGo.transform;

        timerText = MakeText(root, "Timer", new Vector2(0, 1), new Vector2(40, -30), TextAnchor.UpperLeft, 70);
        scoreText = MakeText(root, "Score", new Vector2(1, 1), new Vector2(-40, -30), TextAnchor.UpperRight, 70);
        wordText = MakeText(root, "CurrentWord", new Vector2(0.5f, 1), new Vector2(0, -140), TextAnchor.UpperCenter, 90);
        lastWordText = MakeText(root, "LastWord", new Vector2(0.5f, 1), new Vector2(0, -250), TextAnchor.UpperCenter, 50);
        lastWordText.color = new Color(1f, 1f, 1f, 0.7f);

        BuildGameOverPanel(root, game);
        gameOverPanel.SetActive(false);
    }

    private Text MakeText(Transform parent, string name, Vector2 anchor, Vector2 offset, TextAnchor align, int size)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = offset;
        rect.sizeDelta = new Vector2(900, 120);
        var text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        text.fontStyle = FontStyle.Bold;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        return text;
    }

    private void BuildGameOverPanel(Transform parent, GameManager game)
    {
        gameOverPanel = new GameObject("GameOverPanel", typeof(Image));
        gameOverPanel.transform.SetParent(parent, false);
        var rect = gameOverPanel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        gameOverPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);

        var title = MakeText(gameOverPanel.transform, "Title", new Vector2(0.5f, 0.5f), new Vector2(0, 250), TextAnchor.MiddleCenter, 110);
        title.text = "TIME'S UP!";

        finalScoreText = MakeText(gameOverPanel.transform, "FinalScore", new Vector2(0.5f, 0.5f), new Vector2(0, 80), TextAnchor.MiddleCenter, 80);

        var buttonGo = new GameObject("RestartButton", typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(gameOverPanel.transform, false);
        var buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0, -150);
        buttonRect.sizeDelta = new Vector2(500, 150);
        buttonGo.GetComponent<Image>().color = new Color(1f, 0.75f, 0.1f);
        buttonGo.GetComponent<Button>().onClick.AddListener(game.RestartGame);

        var label = MakeText(buttonGo.transform, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, TextAnchor.MiddleCenter, 65);
        label.text = "PLAY AGAIN";
        label.color = Color.black;
    }

    public void SetTimer(float secondsLeft)
    {
        int s = Mathf.CeilToInt(Mathf.Max(0f, secondsLeft));
        timerText.text = $"{s / 60}:{s % 60:00}";
        timerText.color = s <= 10 ? new Color(1f, 0.4f, 0.4f) : Color.white;
    }

    public void SetScore(int score) => scoreText.text = score.ToString();

    /// <summary>Shows the word being built; green when it's already valid.</summary>
    public void SetCurrentWord(string word, bool isValid)
    {
        wordText.text = word.ToUpperInvariant();
        wordText.color = isValid ? new Color(0.4f, 1f, 0.4f) : Color.white;
    }

    public void ShowWordResult(string word, int points, bool valid)
    {
        lastWordText.text = valid ? $"{word.ToUpperInvariant()}  +{points}" : "";
    }

    public void ShowGameOver(int finalScore)
    {
        finalScoreText.text = $"Score: {finalScore}";
        gameOverPanel.SetActive(true);
    }

    public void HideGameOver() => gameOverPanel.SetActive(false);
}

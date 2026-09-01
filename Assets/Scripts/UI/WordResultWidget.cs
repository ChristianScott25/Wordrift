using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>Flashes the last scored word and its points, then fades out.</summary>
public class WordResultWidget : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private float holdSeconds = 1.2f;
    [SerializeField] private float fadeSeconds = 0.4f;
    [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.75f);

    private Coroutine routine;

    private void OnEnable() => GameEvents.WordSubmitted += OnWordSubmitted;
    private void OnDisable() => GameEvents.WordSubmitted -= OnWordSubmitted;

    private void OnWordSubmitted(WordResult result)
    {
        if (label == null || !result.Accepted) return;

        // Just the word and what it paid. The MECHANISM — which multiplier,
        // which bookmark — belongs to ScoreTallyWidget; this is the flourish
        // that says the total landed.
        label.text = $"{result.Word.ToUpperInvariant()}  +{result.Points}";

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        label.color = color;
        yield return new WaitForSeconds(holdSeconds);

        for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
        {
            var faded = color;
            faded.a = Mathf.Lerp(color.a, 0f, t / fadeSeconds);
            label.color = faded;
            yield return null;
        }

        label.text = "";
        routine = null;
    }
}

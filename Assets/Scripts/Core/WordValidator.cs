using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Answers "is this a word?". Backed by a plain text file, one lowercase word
/// per line. Swap the TextAsset to swap dictionaries.
/// </summary>
public class WordValidator
{
    private readonly HashSet<string> words = new();

    public int Count => words.Count;

    public WordValidator(TextAsset source)
    {
        if (source == null)
        {
            Debug.LogError("No word list assigned — every word will be rejected.");
            return;
        }

        foreach (var line in source.text.Split('\n'))
        {
            var word = line.Trim().ToLowerInvariant();
            if (word.Length > 0) words.Add(word);
        }
    }

    public bool Contains(string word) =>
        !string.IsNullOrEmpty(word) && words.Contains(word.ToLowerInvariant());
}

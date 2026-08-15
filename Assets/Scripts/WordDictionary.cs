using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads the valid-word list from Assets/Resources/wordlist.txt
/// (plain text, one lowercase word per line). Replace that file to
/// swap in a different dictionary.
/// </summary>
public class WordDictionary
{
    private readonly HashSet<string> words = new();

    public int Count => words.Count;

    public WordDictionary()
    {
        var asset = Resources.Load<TextAsset>("wordlist");
        if (asset == null)
        {
            Debug.LogError("wordlist.txt not found in a Resources folder — all words will be rejected.");
            return;
        }

        foreach (var line in asset.text.Split('\n'))
        {
            var word = line.Trim().ToLowerInvariant();
            if (word.Length > 0) words.Add(word);
        }
    }

    public bool Contains(string word) =>
        !string.IsNullOrEmpty(word) && words.Contains(word.ToLowerInvariant());
}

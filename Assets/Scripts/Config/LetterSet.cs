using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The alphabet: which letters exist, what they score, and how often they spawn.
/// Everything is editable in the Inspector, so rebalancing the letter
/// distribution needs no code changes.
///
/// Deliberately no art: letters are drawn as text on a TileSkin, so the look of
/// a tile and the rules of the alphabet vary independently.
///
/// Duplicate this asset to make variants (an "easy" set with more vowels, a
/// themed set with different art) and point a ModeConfig at it.
/// </summary>
[CreateAssetMenu(fileName = "LetterSet", menuName = "Word Crush/Letter Set")]
public class LetterSet : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("A single letter. Lowercase.")]
        public string letter = "a";

        [Tooltip("Points this letter is worth.")]
        public int points = 1;

        [Tooltip("Relative spawn frequency. Higher = appears more often.")]
        public int weight = 1;
    }

    [SerializeField] private List<Entry> entries = new();

    public IReadOnlyList<Entry> Entries => entries;

    private Dictionary<char, Entry> lookup;
    private int totalWeight;

    private void EnsureBuilt()
    {
        if (lookup != null) return;
        lookup = new Dictionary<char, Entry>();
        totalWeight = 0;
        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.letter)) continue;
            char c = char.ToLowerInvariant(entry.letter[0]);
            lookup[c] = entry;
            totalWeight += Mathf.Max(0, entry.weight);
        }
        if (totalWeight <= 0)
            Debug.LogError($"LetterSet '{name}' has no letters with a positive weight.", this);
    }

    /// <summary>Call after editing entries at runtime so caches rebuild.</summary>
    public void Invalidate() => lookup = null;

    public int PointsFor(char letter)
    {
        EnsureBuilt();
        return lookup.TryGetValue(char.ToLowerInvariant(letter), out var entry) ? entry.points : 0;
    }

    /// <summary>Draws a random letter using the weighted distribution.</summary>
    public char Draw()
    {
        EnsureBuilt();
        int roll = Random.Range(0, Mathf.Max(1, totalWeight));
        foreach (var entry in entries)
        {
            roll -= Mathf.Max(0, entry.weight);
            if (roll < 0 && !string.IsNullOrEmpty(entry.letter))
                return char.ToLowerInvariant(entry.letter[0]);
        }
        return 'e';
    }
}

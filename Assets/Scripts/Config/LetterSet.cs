using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The tile catalog: everything a tile can spell — single letters today, "qu"
/// or "ie" someday — with each one's base values and spawn frequency. This is
/// THE list a new TileSpec is stamped from (CreateSpec); grow Entry when tiles
/// need more base values than score, so every spec picks them up in one place.
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
        [Tooltip("What a tile of this kind spells. Lowercase. Usually one letter; " +
                 "multi-letter entries (qu, ie) are allowed but not playable yet.")]
        public string letter = "a";

        // Entry is where per-letter base values live. Add future ones (base
        // money? base whatever) here, and stamp them in CreateSpec below.
        [Tooltip("Base score a tile of this kind starts with.")]
        public int points = 1;

        [Tooltip("Relative spawn frequency — and, read as a count, how many go " +
                 "into a run's sack. 0 = in the catalog but never spawns naturally.")]
        public int weight = 1;
    }

    [SerializeField] private List<Entry> entries = new();

    public IReadOnlyList<Entry> Entries => entries;

    private Dictionary<string, Entry> lookup;
    private int totalWeight;

    private void EnsureBuilt()
    {
        if (lookup != null) return;
        // Keyed by the full string, so "q" and "qu" are different catalog rows.
        lookup = new Dictionary<string, Entry>();
        totalWeight = 0;
        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.letter)) continue;
            lookup[entry.letter.ToLowerInvariant()] = entry;
            totalWeight += Mathf.Max(0, entry.weight);
        }
        if (totalWeight <= 0)
            Debug.LogError($"LetterSet '{name}' has no letters with a positive weight.", this);
    }

    /// <summary>Call after editing entries at runtime so caches rebuild.</summary>
    public void Invalidate() => lookup = null;

    /// <summary>The catalog row for what a tile spells, or null if unlisted.</summary>
    public Entry EntryFor(string letters)
    {
        if (string.IsNullOrEmpty(letters)) return null;
        EnsureBuilt();
        return lookup.TryGetValue(letters.ToLowerInvariant(), out var entry) ? entry : null;
    }

    /// <summary>
    /// Makes a new tile of the given kind, stamped with the catalog's base
    /// values. THE way a TileSpec is born: when Entry grows new base values,
    /// stamping them here hands them to every spec in the game at once.
    /// </summary>
    public TileSpec CreateSpec(string letters)
    {
        var entry = EntryFor(letters);
        if (entry == null)
        {
            Debug.LogError($"LetterSet '{name}' has no entry for '{letters}' — spec created worth 0.", this);
            return new TileSpec { letters = letters, baseScore = 0 };
        }
        return CreateSpec(entry);
    }

    /// <summary>Same stamp, for callers already holding the row (see RunState).</summary>
    public static TileSpec CreateSpec(Entry entry) => new TileSpec
    {
        letters = entry.letter,
        baseScore = entry.points,
    };

    /// <summary>Draws a random catalog row using the weighted distribution.</summary>
    public Entry Draw()
    {
        EnsureBuilt();
        int roll = Random.Range(0, Mathf.Max(1, totalWeight));
        foreach (var entry in entries)
        {
            roll -= Mathf.Max(0, entry.weight);
            if (roll < 0 && !string.IsNullOrEmpty(entry.letter))
                return entry;
        }
        return null;
    }
}

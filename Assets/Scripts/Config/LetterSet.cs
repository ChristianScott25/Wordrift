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

        [Tooltip("Relative spawn frequency — and the share of a run's tile bag " +
                 "this letter gets. 0 = in the catalog but never appears.")]
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

    /// <summary>
    /// A bag of exactly <paramref name="targetCount"/> tiles, sharing them out
    /// across the catalog in proportion to weight, with a floor of ONE of every
    /// letter that has any weight at all.
    ///
    /// Weight is a ratio here, not a count — which is what lets the bag be
    /// resized to any number without re-authoring the catalog, and what a
    /// "bigger bag" upgrade will turn. Scrabble's own 98 falls out of this
    /// unchanged, because at that size the ratios already ARE the counts.
    ///
    /// The floor is what costs the distribution its accuracy: a letter with
    /// weight 1 can't halve, so at 52 tiles J K Q X Z take five where their
    /// share says two and a half, and the common letters make up the
    /// difference. That's the trade the floor buys — never opening a run
    /// unable to spell a Q word at all.
    /// </summary>
    public List<TileSpec> BuildTileBag(int targetCount)
    {
        EnsureBuilt();

        var bag = new List<TileSpec>();
        if (totalWeight <= 0) return bag;

        // Only letters that can appear at all take part; a weight-0 catalog row
        // is listed, not stocked.
        var stocked = new List<Entry>();
        foreach (var entry in entries)
            if (entry != null && !string.IsNullOrEmpty(entry.letter) && entry.weight > 0)
                stocked.Add(entry);
        if (stocked.Count == 0) return bag;

        if (targetCount < stocked.Count)
        {
            Debug.LogWarning($"LetterSet '{name}': a bag of {targetCount} can't hold one of each " +
                             $"of its {stocked.Count} letters — using {stocked.Count}.", this);
            targetCount = stocked.Count;
        }

        // Largest remainder: floor everyone (never below one), then hand the
        // leftovers to whoever the flooring shortchanged most. Deterministic —
        // ties go to the earlier catalog row — so the same set always builds
        // the same bag.
        var ideal = new float[stocked.Count];
        var counts = new int[stocked.Count];
        int placed = 0;
        for (int i = 0; i < stocked.Count; i++)
        {
            ideal[i] = stocked[i].weight * (float)targetCount / totalWeight;
            counts[i] = Mathf.Max(1, Mathf.FloorToInt(ideal[i]));
            placed += counts[i];
        }

        while (placed < targetCount)
        {
            int best = -1;
            for (int i = 0; i < stocked.Count; i++)
                if (best < 0 || ideal[i] - counts[i] > ideal[best] - counts[best]) best = i;
            counts[best]++;
            placed++;
        }

        // Only reachable when the floor pushed the bag over target: take back
        // from whoever is furthest over, but never below the floor of one.
        while (placed > targetCount)
        {
            int worst = -1;
            for (int i = 0; i < stocked.Count; i++)
                if (counts[i] > 1 && (worst < 0 || ideal[i] - counts[i] < ideal[worst] - counts[worst]))
                    worst = i;
            if (worst < 0) break;   // everyone is at one; the floor wins
            counts[worst]--;
            placed--;
        }

        for (int i = 0; i < stocked.Count; i++)
            for (int n = 0; n < counts[i]; n++)
                bag.Add(CreateSpec(stocked[i]));

        return bag;
    }

    /// <summary>
    /// Draws a random catalog row using the weighted distribution. The generator
    /// is passed in rather than taken from a global, so a run's draws belong to
    /// its own seed and nothing else can perturb them.
    /// </summary>
    public Entry Draw(Rng rng)
    {
        EnsureBuilt();
        if (rng == null)
        {
            // A programming error, not a config one — every source builds its
            // own generator. Silence here would look like an empty letter set.
            Debug.LogError($"LetterSet '{name}' was asked to draw with no Rng.", this);
            return null;
        }

        int roll = rng.Range(0, Mathf.Max(1, totalWeight));
        foreach (var entry in entries)
        {
            roll -= Mathf.Max(0, entry.weight);
            if (roll < 0 && !string.IsNullOrEmpty(entry.letter))
                return entry;
        }
        return null;
    }
}

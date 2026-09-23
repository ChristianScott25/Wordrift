using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Answers "is this a word?". Backed by plain text files, one lowercase word
/// per line, with '#' starting a comment.
///
/// There are THREE lists on purpose. The main one is an imported lexicon kept
/// exactly as it arrived, so it can be re-imported or swapped for a different
/// one wholesale; the extra one is ours, hand-edited, and holds the words the
/// import is missing. Merging them into a single file would work and would also
/// bury every deliberate addition in a 178,000-line diff — which is the same
/// reason authored assets and run state are kept apart everywhere else here.
///
/// The third list SUBTRACTS. The imported lexicon is a 2006 Scrabble list, which
/// predates the 2020 removal of slurs from tournament lists and would otherwise
/// score them; the blocklist takes them back out. It is applied LAST, after both
/// additions, so a blocked word is refused no matter what any other list says —
/// "blocked wins" is the one ordering rule here, and the reason it isn't just a
/// third union.
///
/// Subtracting rather than editing the import is what keeps the removals
/// reviewable: they are 327 lines in their own file instead of an invisible
/// difference from a lexicon nobody can diff against.
///
/// The first two are unioned, so which of them a word came from is not a thing
/// the game can ask. Nothing downstream should ever need to.
/// </summary>
public class WordValidator
{
    private readonly HashSet<string> words = new();

    // Words grouped by length, for Matches. BUILT LAZILY, on the first pattern
    // that actually needs it — three or more wildcards, which takes three wild
    // tiles adjacent on the board and so may never happen in a whole run.
    // Building it eagerly cost a pass over 178,000 words and ~2MB in Awake, and
    // GameSession builds a validator on EVERY Game scene load, so that was a
    // few milliseconds and a couple of megabytes per round bought for nothing.
    private Dictionary<int, List<string>> byLength;

    // For the one summary line the constructor logs, so the two list passes
    // don't each announce a total that the next one then changes.
    private int addedFromExtra;
    private int blockedCount;

    public int Count => words.Count;

    /// <summary>
    /// Builds the dictionary. `extra` is optional — with none assigned the game
    /// plays on the imported list alone, which is a complete dictionary and not
    /// a broken state, so this only logs an error for a missing MAIN list.
    /// </summary>
    public WordValidator(TextAsset source, TextAsset extra = null,
                         TextAsset blocked = null)
    {
        if (source == null)
        {
            // Nothing below is worth doing or reporting: the additions and the
            // blocklist are both edits to a dictionary that doesn't exist, and a
            // summary line under this error would read like a working load.
            Debug.LogError("No word list assigned — every word will be rejected.");
            return;
        }

        AddAll(source);

        if (extra != null) AddExtra(extra);

        // Last, and after the additions on purpose: this is a veto, not an
        // opinion. Removing before adding would let the extra list put a
        // blocked word back without anyone meaning to.
        if (blocked != null) RemoveBlocked(blocked);

        Debug.Log($"Dictionary: {Count} words " +
                  $"(+{addedFromExtra} added, -{blockedCount} blocked).");
    }

    /// <summary>
    /// Takes the blocked words out again. Reports how many lines did nothing,
    /// which is the useful signal: a line that matches no word usually means a
    /// typo, or an inflection that was guessed rather than checked — and a
    /// blocklist entry that silently misses is the failure mode that matters
    /// here, since the word stays playable and nothing says so.
    /// </summary>
    private void RemoveBlocked(TextAsset blocked)
    {
        int removed = 0, inert = 0;

        foreach (var line in blocked.text.Split('\n'))
        {
            var word = Clean(line);
            if (word.Length == 0) continue;

            if (words.Remove(word)) removed++;
            else inert++;
        }

        blockedCount = removed;
        if (inert > 0)
            Debug.LogWarning(
                $"{blocked.name}: {inert} entr(ies) matched no word in the " +
                "dictionary — check for typos.", blocked);
    }

    private void AddAll(TextAsset source)
    {
        foreach (var line in source.text.Split('\n'))
        {
            var word = Clean(line);
            if (word.Length > 0) words.Add(word);
        }
    }

    /// <summary>
    /// Adds the hand-edited list, and reports anything on it the main list
    /// already had. A redundant entry is harmless to play but it's also a lie
    /// about why the line is there — the file's whole job is to be the list of
    /// words the import is MISSING, and one nobody can trust gets re-checked by
    /// hand forever. Cheap to answer here, since both lists are being read anyway.
    /// </summary>
    private void AddExtra(TextAsset extra)
    {
        int added = 0;
        var redundant = new List<string>();

        foreach (var line in extra.text.Split('\n'))
        {
            var word = Clean(line);
            if (word.Length == 0) continue;

            if (words.Add(word)) added++;
            else redundant.Add(word);
        }

        addedFromExtra = added;
        if (redundant.Count == 0) return;

        // Named, not just counted: the point is to be able to delete the lines.
        var named = new StringBuilder();
        for (int i = 0; i < redundant.Count && i < 12; i++)
        {
            if (named.Length > 0) named.Append(", ");
            named.Append(redundant[i]);
        }
        if (redundant.Count > 12) named.Append($", +{redundant.Count - 12} more");

        // "the dictionary" and not "the main list": by now `words` also holds the
        // earlier lines of THIS file, so a word listed twice here lands in
        // `redundant` too — and naming the wrong file is worse than saying less.
        Debug.LogWarning(
            $"{extra.name} has {redundant.Count} redundant word(s) — already in " +
            $"the dictionary, so the line can go: {named}", extra);
    }

    /// <summary>
    /// One line to one word, or "" for a line that holds none. '#' starts a
    /// comment: no word contains one, so there's nothing ambiguous to strip.
    /// </summary>
    private static string Clean(string line)
    {
        int comment = line.IndexOf('#');
        if (comment >= 0) line = line.Substring(0, comment);
        return line.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Groups the dictionary by word length, the first time something asks for
    /// it. Lazy on purpose — see the field. Safe to defer because it reads
    /// `words` as it stands, and by the time anything can call Matches the
    /// constructor has finished adding AND blocking.
    /// </summary>
    private void IndexByLength()
    {
        byLength = new Dictionary<int, List<string>>();

        foreach (var word in words)
        {
            if (!byLength.TryGetValue(word.Length, out var bucket))
                byLength[word.Length] = bucket = new List<string>();
            bucket.Add(word);
        }

        // Sorted here, once, so Matches never has to: its callers break ties
        // alphabetically, and a HashSet has no order to inherit.
        foreach (var bucket in byLength.Values) bucket.Sort(System.StringComparer.Ordinal);
    }

    public bool Contains(string word) =>
        !string.IsNullOrEmpty(word) && words.Contains(word.ToLowerInvariant());

    /// <summary>
    /// Every word in the dictionary matching a pattern, where TileSpec.WildSpelling
    /// ('*') stands for any single letter — what a chain holding wild tiles could
    /// spell. Always ALPHABETICAL, so a caller choosing between equally good
    /// answers can just take the first and get the same one every time.
    ///
    /// Two strategies, because neither wins everywhere. Substituting every letter
    /// is 26^n probes, which is unbeatable at one or two wildcards and hopeless at
    /// five (11.8 million, a visible freeze — and a run that buys enough wilds can
    /// reach it). Scanning only the words of the right length is bounded no matter
    /// how many wildcards there are: 1,013 three-letter words, 8,888 five-letter
    /// ones. The crossover sits between two wildcards (676 probes) and three
    /// (17,576), so that's where it switches.
    /// </summary>
    public List<string> Matches(string pattern)
    {
        var found = new List<string>();
        if (string.IsNullOrEmpty(pattern)) return found;

        pattern = pattern.ToLowerInvariant();

        int wildcards = 0;
        foreach (char c in pattern) if (c == Wildcard) wildcards++;

        if (wildcards == 0)
        {
            if (words.Contains(pattern)) found.Add(pattern);
            return found;
        }

        if (wildcards > SubstitutionLimit)
        {
            ScanByLength(pattern, found);
            return found;
        }

        var slots = new int[wildcards];
        for (int i = 0, at = 0; i < pattern.Length; i++)
            if (pattern[i] == Wildcard) slots[at++] = i;

        Substitute(new System.Text.StringBuilder(pattern), slots, 0, found);
        return found;
    }

    // Taken from TileSpec rather than written again: a second copy would leave
    // this search quietly matching nothing the day the character changed.
    private static readonly char Wildcard = TileSpec.WildSpelling[0];

    /// <summary>
    /// Wildcards up to which substituting beats scanning. Three is already the
    /// losing side of the crossover, so this is 2.
    /// </summary>
    private const int SubstitutionLimit = 2;

    /// <summary>
    /// Fills in the wildcards left to right, a-z at each, so the words come out
    /// already sorted and nothing has to sort them. Recursive rather than nested
    /// loops because the number of wildcards isn't known until runtime; the depth
    /// is bounded by SubstitutionLimit.
    ///
    /// ONE buffer for the whole walk, and the slots are worked out up front
    /// rather than re-found each level — so the only thing allocated per probe
    /// is the candidate string itself, and nothing has to undo a slot on the way
    /// back up (every level overwrites its own position on every iteration).
    /// This runs on every selection change, so the garbage is worth caring about.
    /// </summary>
    private void Substitute(System.Text.StringBuilder buffer, int[] slots, int slot,
                            List<string> found)
    {
        if (slot == slots.Length)
        {
            var candidate = buffer.ToString();
            if (words.Contains(candidate)) found.Add(candidate);
            return;
        }

        for (char c = 'a'; c <= 'z'; c++)
        {
            buffer[slots[slot]] = c;
            Substitute(buffer, slots, slot + 1, found);
        }
    }

    /// <summary>
    /// Walks only the words of the pattern's length. Already alphabetical, since
    /// IndexByLength sorted the buckets.
    /// </summary>
    private void ScanByLength(string pattern, List<string> found)
    {
        if (byLength == null) IndexByLength();
        if (!byLength.TryGetValue(pattern.Length, out var bucket)) return;

        foreach (var word in bucket)
        {
            bool fits = true;
            for (int i = 0; i < pattern.Length; i++)
            {
                if (pattern[i] == Wildcard || pattern[i] == word[i]) continue;
                fits = false;
                break;
            }
            if (fits) found.Add(word);
        }
    }
}

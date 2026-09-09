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

    public bool Contains(string word) =>
        !string.IsNullOrEmpty(word) && words.Contains(word.ToLowerInvariant());
}

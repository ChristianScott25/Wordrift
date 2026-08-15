using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Letter point values and spawn frequencies, both based on Scrabble.
/// Rare letters (Z, Q, X...) appear less often and score more.
/// </summary>
public static class LetterBag
{
    private static readonly Dictionary<char, int> Points = new()
    {
        ['a'] = 1, ['b'] = 3, ['c'] = 3, ['d'] = 2, ['e'] = 1, ['f'] = 4,
        ['g'] = 2, ['h'] = 4, ['i'] = 1, ['j'] = 8, ['k'] = 5, ['l'] = 1,
        ['m'] = 3, ['n'] = 1, ['o'] = 1, ['p'] = 3, ['q'] = 10, ['r'] = 1,
        ['s'] = 1, ['t'] = 1, ['u'] = 1, ['v'] = 4, ['w'] = 4, ['x'] = 8,
        ['y'] = 4, ['z'] = 10,
    };

    // Scrabble tile-bag counts (blanks excluded) used as spawn weights.
    private static readonly Dictionary<char, int> Weights = new()
    {
        ['a'] = 9, ['b'] = 2, ['c'] = 2, ['d'] = 4, ['e'] = 12, ['f'] = 2,
        ['g'] = 3, ['h'] = 2, ['i'] = 9, ['j'] = 1, ['k'] = 1, ['l'] = 4,
        ['m'] = 2, ['n'] = 6, ['o'] = 8, ['p'] = 2, ['q'] = 1, ['r'] = 6,
        ['s'] = 4, ['t'] = 6, ['u'] = 4, ['v'] = 2, ['w'] = 2, ['x'] = 1,
        ['y'] = 2, ['z'] = 1,
    };

    private static readonly int TotalWeight = ComputeTotalWeight();

    private static int ComputeTotalWeight()
    {
        int total = 0;
        foreach (int w in Weights.Values) total += w;
        return total;
    }

    public static int PointsFor(char letter) => Points[char.ToLowerInvariant(letter)];

    public static int WordScore(string word)
    {
        int score = 0;
        foreach (char c in word) score += PointsFor(c);
        return score;
    }

    /// <summary>Draws a random letter using the weighted distribution.</summary>
    public static char Draw()
    {
        int roll = Random.Range(0, TotalWeight);
        foreach (var pair in Weights)
        {
            roll -= pair.Value;
            if (roll < 0) return pair.Key;
        }
        return 'e'; // unreachable
    }
}

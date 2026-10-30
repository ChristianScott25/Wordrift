using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🚧 Vowel Shower (2026-10-29): turns a few consonants on the board into vowels
/// for the round. Each tile goes through Board.Rewrite — the same trick as Ink
/// Blot — so the bag never hears about it, and it's saved as the round's
/// boardFace.
///
/// Only plain one-letter consonant tiles are candidates: not a wild, a choice
/// tile, a pair like CH, or a tile an item already rewrote. Which tiles and which
/// vowels come off the round's item stream, walked in OccupiedInOrder order, so
/// a seed always picks the same ones. Refused (before any draw) when there's
/// no candidate.
/// </summary>
[CreateAssetMenu(fileName = "Consumable_VowelShower", menuName = "Word Crush/Consumable/Random Vowels")]
public class RandomVowelsConsumable : Consumable
{
    [Min(1)] public int count = 3;

    [Tooltip("The vowels a tile may become, lowercase.")]
    public string vowels = "aeiou";

    public override string PowerText =>
        $"Turns {count} random consonant{(count == 1 ? "" : "s")} on the board into vowels, for this round.";

    public override bool Use(ConsumableUse use)
    {
        var board = use?.Board;
        if (board == null || use.Rng == null || string.IsNullOrEmpty(vowels)) return false;
        if (board.Busy || board.Resolving) return false;

        var candidates = new List<Tile>();
        foreach (var cell in board.OccupiedInOrder())
        {
            if (!board.Tiles.TryGetValue(cell, out var tile) || tile == null || tile.IsRewritten) continue;
            string letters = tile.Spec?.letters;
            if (letters == null || letters.Length != 1) continue;
            char c = letters[0];
            if (!char.IsLetter(c) || vowels.IndexOf(c) >= 0) continue;
            candidates.Add(tile);
        }
        if (candidates.Count == 0) return false;

        int changed = 0;
        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            int pick = use.Rng.Range(0, candidates.Count);
            var tile = candidates[pick];
            candidates.RemoveAt(pick);

            string vowel = vowels[use.Rng.Range(0, vowels.Length)].ToString();
            if (board.Rewrite(tile, vowel)) changed++;
        }
        return changed > 0;
    }
}

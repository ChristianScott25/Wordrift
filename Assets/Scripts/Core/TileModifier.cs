using UnityEngine;

/// <summary>
/// A special property attached to a tile — double letter, triple word, and
/// whatever comes later. ScoreCalculator walks these when scoring a chain,
/// and Tile uses the visuals to make them readable on the board.
///
/// To add one: subclass, override the bits you care about, create the asset.
/// </summary>
public abstract class TileModifier : ScriptableObject
{
    [Header("Visuals")]
    [Tooltip("Tint applied to the tile so the player can see it's special.")]
    public Color tint = Color.white;

    [Tooltip("Optional badge sprite drawn behind the letter.")]
    public Sprite badge;

    [Header("Spawning")]
    [Tooltip("Chance (0-1) that a newly spawned tile gets this modifier.")]
    [Range(0f, 1f)] public float spawnChance = 0f;

    /// <summary>Applied to this tile's own letter value.</summary>
    public virtual int ModifyLetterScore(int points) => points;

    /// <summary>Multiplies the score of the whole word this tile is part of.</summary>
    public virtual int WordMultiplier => 1;
}

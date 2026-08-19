using UnityEngine;

/// <summary>Double / triple word score.</summary>
[CreateAssetMenu(fileName = "WordMultiplier", menuName = "Word Crush/Tile Modifier/Word Multiplier")]
public class WordMultiplierModifier : TileModifier
{
    [Min(1)] public int multiplier = 2;

    public override int WordMultiplier => multiplier;
}

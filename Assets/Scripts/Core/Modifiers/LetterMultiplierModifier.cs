using UnityEngine;

/// <summary>Double / triple letter score.</summary>
[CreateAssetMenu(fileName = "LetterMultiplier", menuName = "Word Crush/Tile Modifier/Letter Multiplier")]
public class LetterMultiplierModifier : TileModifier
{
    [Min(1)] public int multiplier = 2;

    public override long ModifyLetterScore(long points) => points * multiplier;
}

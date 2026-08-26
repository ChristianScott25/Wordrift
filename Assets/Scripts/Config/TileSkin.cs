using UnityEngine;

/// <summary>
/// One tile look: the base art plus the colors the letter and score are drawn
/// in on top of it. A skin fully describes how a tile reads, so a dark skin can
/// carry light text without anything else in the project knowing.
///
/// Letters are text now, not art, which is what makes this cheap: a new look is
/// one asset and one sprite, not 26 sprites. Modes hold a list of these, so
/// several looks can be in play on the same board at once — see
/// ModeConfig.tileSkins and Board.PickSkin.
/// </summary>
[CreateAssetMenu(fileName = "TileSkin", menuName = "Word Crush/Tile Skin")]
public class TileSkin : ScriptableObject
{
    [Tooltip("Shown wherever skins get listed to the player. Not used in play yet.")]
    public string displayName = "Tile";

    [Tooltip("The tile body. Tinted at runtime for selection and modifiers, so " +
             "a white or light sprite has the most range.")]
    public Sprite baseSprite;

    [Tooltip("Color of the letter drawn on this tile.")]
    public Color letterColor = new Color(0.16f, 0.17f, 0.23f, 1f);

    [Tooltip("Color of the score in the corner.")]
    public Color scoreColor = new Color(0.16f, 0.17f, 0.23f, 1f);

    [Tooltip("Circle drawn behind a multiplier badge. Tinted per modifier, so " +
             "keep it white. Swap this to change the badge treatment for every " +
             "modifier at once. Empty = the label draws with no circle behind it.")]
    public Sprite badgeSprite;

    [Tooltip("Relative chance of a spawning tile taking this skin, when a mode " +
             "lists more than one. Same idea as a letter's spawn weight.")]
    [Min(0)] public int weight = 1;
}

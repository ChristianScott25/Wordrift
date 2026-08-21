using TMPro;

/// <summary>
/// How one tile should look, as two independent axes:
///
///   Skin       — the body art and the colors drawn on it
///   LetterFont — the typeface the letter is set in
///
/// Separate on purpose. Changing the font shouldn't mean touching the art, and
/// a new tile skin shouldn't drag a typeface along with it. Either field may be
/// null, which means "keep whatever the Tile prefab was authored with".
///
/// This is the seam to widen when tiles gain more looks — a badge style, a
/// demolish effect — rather than growing Tile.Init's parameter list again.
/// </summary>
public struct TileLook
{
    public TileSkin Skin;
    public TMP_FontAsset LetterFont;
}

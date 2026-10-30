using UnityEngine;

/// <summary>
/// 🚧 What each rarity LOOKS like (2026-10-30): one colour per tier, plus the
/// cursed colour. The one place they live, so the info box, both shops'
/// description panels and the shelf rows can't disagree about what "rare" looks like.
///
/// A code constant, like InspectBoxSetup's look values, because it's read in two
/// scenes. The colours are deep enough to carry WHITE text (the info box's
/// chips) and to read as ink on the cream parchment (the Bookshop). The ordinary
/// shop is a dark panel, where deep colours vanish, so text there asks for the
/// `onDark` version — the same colour lifted toward white. First guesses.
/// </summary>
public static class RarityStyle
{
    public static readonly Color Common = new Color32(0x66, 0x66, 0x66, 0xFF);
    public static readonly Color Uncommon = new Color32(0x2E, 0x7D, 0x32, 0xFF);
    public static readonly Color Rare = new Color32(0x1F, 0x5F, 0xBF, 0xFF);
    public static readonly Color Legendary = new Color32(0xB0, 0x56, 0x00, 0xFF);
    public static readonly Color Trash = new Color32(0x7A, 0x5C, 0x3A, 0xFF);
    public static readonly Color Cursed = new Color32(0x9C, 0x2A, 0x7A, 0xFF);

    /// <summary>How far toward white a colour is lifted to read on a dark panel.</summary>
    private const float DarkLift = 0.45f;

    public static Color ColorOf(Rarity rarity) => rarity switch
    {
        Rarity.Uncommon => Uncommon,
        Rarity.Rare => Rare,
        Rarity.Legendary => Legendary,
        Rarity.Trash => Trash,
        _ => Common,
    };

    /// <summary>Text wrapped in a colour tag, for a TMP label with rich text on.</summary>
    public static string Paint(string text, Color color, bool onDark = false) =>
        $"<color=#{ColorUtility.ToHtmlStringRGB(onDark ? Color.Lerp(color, Color.white, DarkLift) : color)}>{text}</color>";

    /// <summary>
    /// "RARE · CURSED", coloured — the label line a description panel puts above
    /// its body. Empty when there's nothing to say.
    /// </summary>
    public static string Line(Rarity? rarity, bool cursed, bool onDark = false)
    {
        string line = rarity.HasValue ? Paint(RarityText.Label(rarity.Value), ColorOf(rarity.Value), onDark) : "";
        if (cursed)
            line += (line.Length > 0 ? "  ·  " : "") + Paint(RarityText.Cursed, Cursed, onDark);
        return line;
    }

    /// <summary>A description with its label line on top — or the description alone.</summary>
    public static string WithLine(string body, Rarity? rarity, bool cursed, bool onDark = false)
    {
        string line = Line(rarity, cursed, onDark);
        return line.Length == 0 ? body : line + "\n" + body;
    }

    /// <summary>
    /// A name for a shelf row, coloured by its tier. Common is left in the
    /// label's own colour: it's the default, and colouring it would make every
    /// row a colour and none of them stand out.
    /// </summary>
    public static string Name(string name, Rarity? rarity, bool onDark = false)
    {
        if (!rarity.HasValue || rarity.Value == Rarity.Common) return name;
        return Paint(name, ColorOf(rarity.Value), onDark);
    }
}

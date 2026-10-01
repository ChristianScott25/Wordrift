using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds THE INFO BOX — the little card that says what the thing you just
/// touched is.
///
/// It lives in its own file rather than inside GameLayoutSetup because BOTH
/// scenes need one. The bookmark row is in the Game scene and the Shop, and a
/// tap on a card raises Inspector either way; a box in only one of them would
/// make the same gesture work in one scene and silently do nothing in the other.
///
/// ⚠️ A SIBLING ON THE CANVAS, NOT A CHILD OF ANY WIDGET. It has to reach every
/// corner of the screen — it tucks under a bookmark near the bottom and under a
/// tile in the middle — and GameLayout.Attach would otherwise trap it inside
/// whichever band its owner lives in.
///
/// ⚠️ NOTHING IN IT IS A RAYCAST TARGET, and that is not decoration. In the Game
/// scene it floats over the board, and ChainController refuses to start a word
/// whenever EventSystem.IsPointerOverGameObject() is true on the press frame —
/// so one raycast target here stops word selection working at all, which looks
/// nothing like a problem with a decorative panel. InspectBoxWidget re-asserts
/// it at runtime; this is the other half.
///
/// ⚠️ ITS SIZE COMES FROM ITS CONTENTS. The box has to know how tall it came out
/// before it can tuck under anything, so the layout groups and the
/// ContentSizeFitter are rewritten on every run. So is everything else about how
/// it looks — unlike GameLayoutSetup.Text, this script preserves NO hand-tuning,
/// and the look block below says why.
/// </summary>
public static class InspectBoxSetup
{
    private const string BoxName = "Info Box";

    /// <summary>
    /// The plate the box is drawn on — a cream rounded rectangle with a black
    /// outline, 200x100, used 9-SLICED so one sprite covers every size.
    ///
    /// ⚠️ There is a "Mini Item Text Box" beside it in the same folder and it is
    /// NOT used, on purpose. Its corner curve and its outline are pixel for pixel
    /// the same as this one's — the only difference is how much flat middle was
    /// drawn, and the flat middle is exactly the part 9-slice generates. One
    /// sprite at two sizes is two things to keep in step for no gain.
    /// </summary>
    private const string BoxSpritePath = "Assets/Sprites/Gameplay UI/Main Item Text Box.png";

    /// <summary>
    /// How far in from each edge the sprite stops being a corner and starts being
    /// a stretchable edge, in sprite pixels.
    ///
    /// ⚠️ THIS IS STAMPED ONTO THE TEXTURE'S IMPORT SETTINGS, not just read. The
    /// border lives on the importer, which means it is normally set by hand in
    /// the Sprite Editor — and a value set by hand is a value that can quietly
    /// differ from what the code was written against, with a symptom (slightly
    /// wrong corners) nobody would trace back to an import setting. Same argument
    /// as BoxWidth below.
    ///
    /// 8 because the art's corner curve is finished by pixel 7 and its outline is
    /// 3 thick, so 8 contains the whole corner with a pixel to spare. Redraw the
    /// corner bigger and this has to grow with it.
    /// </summary>
    private const int SpriteBorder = 8;

    /// <summary>
    /// THE BOX'S WHOLE LOOK, AND ALL OF IT IS WRITTEN ON EVERY RUN.
    ///
    /// ⚠️ This used to be split — the width in code, the colours and font sizes
    /// left on the object so hand-tuning survived a re-run, the same split
    /// GameLayoutSetup.Text draws. That split is WRONG HERE and was wrong twice
    /// over. There is a box in each of TWO scenes, so a value tuned on one of
    /// them is a value the other one doesn't have: the look drifts apart, which
    /// is the exact thing one shared box exists to prevent. And every change so
    /// far has had to be forced past the guard anyway — once when the art landed
    /// and every colour had been picked for a dark panel, once when the text grew.
    /// A guard you route around every time is not protecting anything.
    ///
    /// So the Inspector holds nothing worth keeping. Drag borderScale on the
    /// object to FIND a value by eye if you like — OnValidate redraws it live —
    /// then put the number here, where both scenes read it.
    /// </summary>
    private const float BoxWidth = 560f;

    // Breathing room inside the plate. It has to clear the DRAWN corner, not just
    // the text: at the default border scale the corner is about 20 canvas units
    // across, so anything under about 28 sits the first letter on the curve.
    private const int CardPadding = 40;
    private const float RowSpacing = 14f;

    private const float TitleSize = 42f;
    private const float BodySize = 32f;
    private const float ChipSize = 26f;

    /// <summary>
    /// How chunky the drawn border and corners come out, shared by the plate and
    /// the chips. Higher is chunkier. Lives on the widget rather than on the
    /// Image because the CHIPS are built at runtime and need it too.
    /// </summary>
    private const float BorderScale = 2.5f;

    // The art is cream with a black outline, so the text is dark. The chips are
    // tinted, which works because a multiply over cream gives a coloured fill and
    // leaves the black outline black.
    private static readonly Color TitleColor = new Color(0.10f, 0.09f, 0.12f, 1f);
    private static readonly Color BodyColor = new Color(0.27f, 0.25f, 0.29f, 1f);
    private static readonly Color ChipColor = new Color(0.47f, 0.60f, 0.82f, 1f);
    private static readonly Color ChipTextColor = Color.white;

    /// <summary>
    /// Makes or refreshes the box on a canvas. Returns the widget, or null if
    /// there was no canvas to put it on.
    /// </summary>
    public static InspectBoxWidget Build(Canvas canvas)
    {
        if (canvas == null) return null;

        var boxSprite = BoxSprite();

        var widget = Object.FindFirstObjectByType<InspectBoxWidget>(FindObjectsInactive.Include);
        GameObject root;

        if (widget != null && !PrefabUtility.IsPartOfPrefabInstance(widget.gameObject))
        {
            root = widget.gameObject;
        }
        else
        {
            root = new GameObject(BoxName, typeof(RectTransform), typeof(InspectBoxWidget));
            root.transform.SetParent(canvas.transform, false);
            widget = root.GetComponent<InspectBoxWidget>();
        }

        var rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;

        // Hangs DOWN from its top edge, which is what "tucked under the thing
        // you touched" means once the box can be any height.
        rect.pivot = new Vector2(0.5f, 1f);

        // Width is a decision; height is measured from the contents.
        //
        // ⚠️ This used to be guarded by `sizeDelta.x <= 1f` — "only set it on a
        // box that is new". That guard NEVER FIRED: a fresh RectTransform
        // defaults to 100x100, not 0x0, so every box came out 100 units wide and
        // wrapped its description to about four characters. Kept as a comment
        // because the same guard shape is correct for colours and font sizes
        // below, and the difference is only that they start out unset.
        rect.sizeDelta = new Vector2(BoxWidth, rect.sizeDelta.y);

        Column(root, padding: 0, spacing: 0f);

        var card = Slot(root.transform, "Card");
        var plate = card.GetComponent<Image>() ?? card.AddComponent<Image>();

        plate.sprite = boxSprite;
        plate.raycastTarget = false;
        plate.color = Color.white;      // let the art's own cream through

        // Sliced, NOT Simple, and deliberately not through WordCrushSetup.SetSprite:
        // that one sets preserveAspect, which is right for art with words drawn
        // into it and meaningless here. Stretching is the entire point.
        plate.type = Image.Type.Sliced;
        plate.fillCenter = true;
        plate.preserveAspect = false;

        Column(card, padding: CardPadding, spacing: RowSpacing);

        var titleText = Label(Slot(card.transform, "Title"), TitleSize,
                              TextAlignmentOptions.Left, TitleColor);

        var bodyText = Label(Slot(card.transform, "Body"), BodySize,
                             TextAlignmentOptions.TopLeft, BodyColor);
        bodyText.textWrappingMode = TextWrappingModes.Normal;

        var tags = Slot(card.transform, "Tags");
        var row = tags.GetComponent<HorizontalLayoutGroup>() ?? tags.AddComponent<HorizontalLayoutGroup>();
        row.spacing = RowSpacing;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        Fit(tags);

        WordCrushSetup.SetRef(widget, "root", card);
        WordCrushSetup.SetRef(widget, "titleLabel", titleText);
        WordCrushSetup.SetRef(widget, "bodyLabel", bodyText);
        WordCrushSetup.SetRef(widget, "tagRow", (RectTransform)tags.transform);
        WordCrushSetup.SetRef(widget, "chipSprite", boxSprite);

        // The chips are built at runtime, so their look has to be handed to the
        // widget rather than built here. Same numbers as the card above.
        WordCrushSetup.SetColor(widget, "chipColor", ChipColor);
        WordCrushSetup.SetColor(widget, "chipTextColor", ChipTextColor);
        WordCrushSetup.SetFloat(widget, "chipFontSize", ChipSize);
        WordCrushSetup.SetFloat(widget, "borderScale", BorderScale);

        // The card and the chips are the same sprite at the same corner size, and
        // the widget is what applies it to both — see ApplySliceSettings.
        widget.ApplySliceSettings();

        // Drawn over everything, whatever order the widgets were made in. The
        // widget raises it again on every open, because GameLayout adds its band
        // containers to this same canvas AT RUNTIME and so ends up on top.
        root.transform.SetAsLastSibling();

        // Closed. A box left switched on in the saved scene is the first thing
        // you would see on opening it.
        card.SetActive(false);

        EditorUtility.SetDirty(widget);
        return widget;
    }

    /// <summary>
    /// Loads the plate sprite, having first made sure the texture is imported the
    /// way a 9-sliced pixel-art sprite has to be.
    ///
    /// ⚠️ THE IMPORT SETTINGS ARE WRITTEN HERE RATHER THAN LEFT TO THE INSPECTOR,
    /// and the border is only one of four things that were wrong on the file as
    /// drawn. The texture arrived as Sprite Mode MULTIPLE, which makes the sprite
    /// a SUB-asset — LoadAssetAtPath&lt;Sprite&gt; returns null for one of those, so
    /// the box would have come out with no plate at all and nothing to say why.
    /// Bilinear filtering and compression are the other two: both quietly smear a
    /// 3-pixel black outline, which is most of what this art IS.
    /// </summary>
    private static Sprite BoxSprite()
    {
        StampSpriteImport(BoxSpritePath, SpriteBorder);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BoxSpritePath);
        if (sprite == null)
            Debug.LogError($"No sprite at {BoxSpritePath} — the info box will have no plate.");
        return sprite;
    }

    /// <summary>
    /// Makes a texture importable as one 9-sliceable pixel-art sprite. Re-imports
    /// only when something actually had to change, because SaveAndReimport is not
    /// free and this runs on every layout setup.
    /// </summary>
    private static void StampSpriteImport(string path, int border)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;

        bool changed = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        // Multiple makes the sprite a sub-asset and LoadAssetAtPath<Sprite> finds
        // nothing. This is the one that would have cost an afternoon.
        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;   // bilinear smears the outline
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        var wanted = new Vector4(border, border, border, border);
        if (importer.spriteBorder != wanted)
        {
            importer.spriteBorder = wanted;
            changed = true;
        }

        var platform = importer.GetDefaultPlatformTextureSettings();
        if (platform.textureCompression != TextureImporterCompression.Uncompressed)
        {
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(platform);
            changed = true;
        }

        if (changed) importer.SaveAndReimport();
    }

    /// <summary>
    /// Stacks a slot's children top to bottom and lets it grow to fit them.
    ///
    /// Horizontal fit is deliberately Unconstrained: the box's WIDTH is a
    /// decision, not a consequence of how long the longest word in a description
    /// happens to be.
    /// </summary>
    private static void Column(GameObject slot, int padding, float spacing)
    {
        var group = slot.GetComponent<VerticalLayoutGroup>() ?? slot.AddComponent<VerticalLayoutGroup>();
        group.padding = new RectOffset(padding, padding, padding, padding);
        group.spacing = spacing;
        group.childAlignment = TextAnchor.UpperLeft;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = false;
        Fit(slot);
    }

    private static void Fit(GameObject slot)
    {
        var fitter = slot.GetComponent<ContentSizeFitter>() ?? slot.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    /// <summary>
    /// A child of a layout group. Its anchors are NOT set here on purpose — the
    /// group overwrites them every time it lays out, so anything written here
    /// would be a lie about who decides where this sits.
    /// </summary>
    private static GameObject Slot(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    /// <summary>
    /// EVERYTHING here is applied every run — see the look block at the top for
    /// why this one doesn't preserve hand-tuning the way GameLayoutSetup.Text does.
    /// </summary>
    private static TMP_Text Label(GameObject slot, float size, TextAlignmentOptions align,
                                  Color color)
    {
        var text = slot.GetComponent<TMP_Text>();
        if (text == null)
        {
            // A new TMP_Text takes whatever font is TMP's DEFAULT when it is
            // created, which Word Crush/Create Font Asset has already set to the
            // game's one typeface. That is the whole reason that default is set.
            text = slot.AddComponent<TextMeshProUGUI>();
            text.fontStyle = FontStyles.Bold;
            text.text = "";
        }

        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.raycastTarget = false;
        return text;
    }
}

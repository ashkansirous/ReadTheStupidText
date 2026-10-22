namespace DinoReader.Core;

/// <summary>What the OCR read from one page picture: its lines with their boxes (Slice 78). An empty reading is a page with no
/// text, which is kept as a picture-only page.</summary>
public sealed record PageReading(IReadOnlyList<PageLine> Lines)
{
    public string Text => ImportedText.PageText(Lines.Select(l => l.Text));

    public bool HasText => Lines.Count > 0;
}

/// <summary>
/// One page in the review wizard (Slice 78): its picture as picked, the rotation and crop applied to it, what the OCR read
/// from the result, and the text the grown-up typed over it. A page without a <see cref="SourceFile"/> is text only.
/// Changing the picture (rotate, crop) throws away the reading, which described the old picture, but keeps the typed text,
/// so <see cref="NeedsOcr"/> is true for exactly the pages that changed and a fix the grown-up made is not lost.
/// </summary>
public sealed record PageDraft(
    string Id,
    string? SourceFile,
    int RotationQuarterTurns = 0,
    NormalizedRect? Crop = null,
    PageReading? Ocr = null,
    string? EditedText = null,
    string? TransformedFile = null)
{
    private const int FullTurn = 4;

    /// <summary>A page for a new picture.</summary>
    public static PageDraft ForPicture(string sourceFile, PageReading? ocr = null) => new(NewId(), sourceFile, Ocr: ocr);

    /// <summary>A text-only page. Line endings are made LF, the only kind the story code knows.</summary>
    public static PageDraft ForText(string text) => new(NewId(), null, EditedText: LineBreaks.ToLf(text));

    /// <summary>The page of an existing story. <paramref name="pictureFile"/> is the resolved path of its picture, or null
    /// when it has none (or the file is gone); its recognised lines are kept, so nothing is read again until it changes.</summary>
    public static PageDraft FromStory(StoryPage page, string? pictureFile)
    {
        if (pictureFile is null)
        {
            return ForText(page.Text);
        }

        var ocr = new PageReading(page.Lines ?? []);
        return new PageDraft(NewId(), pictureFile, Ocr: ocr, EditedText: page.Text == ocr.Text ? null : page.Text);
    }

    public bool HasPicture => SourceFile is not null;

    /// <summary>The page text: the grown-up's text if any, else what the OCR read.</summary>
    public string Text => EditedText ?? Ocr?.Text ?? string.Empty;

    public bool NeedsOcr => SourceFile is not null && Ocr is null;

    public bool NeedsTransform => RotationQuarterTurns != 0 || Crop is not null;

    /// <summary>The file that shows the page now: the transformed copy, or the source when nothing was applied. Null while a
    /// rotation or crop has not been written yet.</summary>
    public string? PictureFile => NeedsTransform ? TransformedFile : SourceFile;

    /// <summary>Turns the picture clockwise by <paramref name="quarterTurns"/> (negative for anticlockwise); the crop turns with it.</summary>
    public PageDraft Rotated(int quarterTurns) => HasPicture && quarterTurns % FullTurn != 0
        ? Changed(Wrap(RotationQuarterTurns + quarterTurns), CropAfterTurn(Crop, quarterTurns))
        : this;

    /// <summary>The rotation and crop in the editor's result, both at once.</summary>
    public PageDraft WithTransform(int quarterTurns, NormalizedRect? crop) => HasPicture
        ? Changed(Wrap(quarterTurns), crop)
        : this;

    /// <summary>Whether <paramref name="other"/> shows the same picture as this page: same page, source, rotation and crop.
    /// A reading made for one is stale for the other.</summary>
    public bool SamePictureAs(PageDraft other) =>
        Id == other.Id && SourceFile == other.SourceFile && RotationQuarterTurns == other.RotationQuarterTurns && Crop == other.Crop;

    public PageDraft WithOcr(PageReading ocr) => this with { Ocr = ocr };

    public PageDraft WithText(string text) => this with { EditedText = LineBreaks.ToLf(text) };

    public PageDraft WithTransformedFile(string? file) => this with { TransformedFile = file };

    private PageDraft Changed(int turns, NormalizedRect? crop) =>
        this with { RotationQuarterTurns = turns, Crop = crop, Ocr = null, TransformedFile = null };

    /// <summary>A crop rectangle after the picture is turned clockwise by <paramref name="quarterTurns"/>: the rectangle
    /// stays on the same part of the picture.</summary>
    public static NormalizedRect? CropAfterTurn(NormalizedRect? crop, int quarterTurns)
    {
        if (crop is not { } rect)
        {
            return null;
        }

        for (int i = Wrap(quarterTurns); i > 0; i--)
        {
            rect = new NormalizedRect(1 - rect.Bottom, rect.X, rect.Height, rect.Width);
        }

        return rect;
    }

    private static int Wrap(int turns) => ((turns % FullTurn) + FullTurn) % FullTurn;

    private static string NewId() => Guid.NewGuid().ToString("N");
}

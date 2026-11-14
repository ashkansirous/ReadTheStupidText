namespace DinoReader.Core;

/// <summary>Where the page text editor puts the page's picture: above the field (held upright), in a column on the left, as a small thumbnail on the left (keyboard up), or nowhere (the page has none).</summary>
public enum EditorPicture
{
    None,
    Above,
    Left,
    Thumbnail,
}

/// <summary>The editor's shape for one window: where the picture goes and how wide its column is in dp (0 unless it is on the left).</summary>
public readonly record struct EditorShape(EditorPicture Picture, double PictureWidth);

/// <summary>The pure rules of the page text editor held sideways (Slice 232 part C, mock Batch 44 frame 120).</summary>
public static class EditorLayout
{
    /// <summary>The thumbnail's width when the keyboard is up.</summary>
    public const double ThumbnailWidth = 64;

    /// <summary>
    /// The shape for a window. The picture is a thumbnail exactly when the window is held sideways on a phone (short side under <see cref="LandscapeLayout.TabletShortSide"/>) and the text
    /// editor has focus (the keyboard is up, so the field keeps its lines); otherwise it is the left column (sideways), above the field (upright) or absent (no picture).
    /// </summary>
    public static EditorShape For(double width, double height, bool hasPicture, bool editorFocused)
    {
        if (!hasPicture)
        {
            return new EditorShape(EditorPicture.None, 0);
        }

        if (!LandscapeLayout.IsLandscape(width, height))
        {
            return new EditorShape(EditorPicture.Above, 0);
        }

        bool phone = LandscapeLayout.SizeOf(width, height) == DeviceSize.Phone;
        return phone && editorFocused
            ?new EditorShape(EditorPicture.Thumbnail, ThumbnailWidth)
            : new EditorShape(EditorPicture.Left, LandscapeLayout.PicturePaneWidth(width, height, 0));
    }
}

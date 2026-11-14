namespace DinoReader.Core;

/// <summary>
/// The geometry of the reorder mode's grid (Slice 264): tiles of one size in rows of <paramref name="Columns"/>, so the page can place each book and find the spot a finger is over.
/// Pure arithmetic, no views. Never mirrored: column 0 is always on the left.
/// </summary>
public readonly record struct ShelfOrderGrid(int Columns, double TileWidth, double TileHeight, double GapX, double GapY, double Left, double Top)
{
    /// <summary>The narrowest a tile may be, in dp, when the margins leave no room.</summary>
    public const double MinTileWidth = 1;

    /// <summary>The top-left corner of the tile at <paramref name="index"/>.</summary>
    public (double X, double Y) Origin(int index) =>
        (Left + ((index % Columns) * (TileWidth + GapX)), Top + ((index / Columns) * (TileHeight + GapY)));

    /// <summary>The height of the whole grid for <paramref name="count"/> tiles, to the bottom of the last row.</summary>
    public double ContentHeight(int count) =>
        count <= 0 ? Top : Top + (((count + Columns - 1) / Columns) * (TileHeight + GapY)) - GapY;

    /// <summary>The slot (0 to count - 1) nearest to a point: the column and row under it, clamped to the grid and to the last book.</summary>
    public int SlotAt(double x, double y, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        int column = Math.Clamp((int)Math.Floor((x - Left) / (TileWidth + GapX)), 0, Columns - 1);
        int rows = (count + Columns - 1) / Columns;
        int row = Math.Clamp((int)Math.Floor((y - Top) / (TileHeight + GapY)), 0, rows - 1);
        return Math.Min((row * Columns) + column, count - 1);
    }

    /// <summary>A grid that fills <paramref name="width"/> with <paramref name="columns"/> equal tiles between <paramref name="sidePadding"/> margins; a tile is a 3:4 cover plus a title strip of <paramref name="titleHeight"/>.</summary>
    public static ShelfOrderGrid Fit(double width, int columns, double sidePadding, double gapX, double gapY, double titleHeight, double titleSpacing) =>
        Fit(width, columns, sidePadding, sidePadding, gapX, gapY, titleHeight, titleSpacing);

    /// <summary>The same with a different left and right margin (Slice 319: the shelf list beside a rail has 16 on the left and 26 on the right).</summary>
    public static ShelfOrderGrid Fit(double width, int columns, double leftPadding, double rightPadding, double gapX, double gapY, double titleHeight, double titleSpacing) =>
        Fit(width, columns, leftPadding, rightPadding, gapY, gapX, gapY, titleHeight, titleSpacing);

    /// <summary>The same with its own top inset (Slice 319: beside the rail the shelf list starts 14 down, not one gap down).</summary>
    public static ShelfOrderGrid Fit(double width, int columns, double leftPadding, double rightPadding, double topPadding, double gapX, double gapY, double titleHeight, double titleSpacing)
    {
        int cols = Math.Max(1, columns);
        double fitted = ShelfOrderLayoutRules.WholeDp((width - leftPadding - rightPadding - ((cols - 1) * gapX)) / cols); // whole dp: a fractional width makes WinUI's measure and arrange disagree by 1
        double tile = Math.Max(MinTileWidth, fitted); // margins wider than the width must not give a negative WidthRequest
        double cover = tile / LandscapeLayout.DefaultPictureAspect;
        double spare = width - leftPadding - rightPadding - ((cols - 1) * gapX) - (cols * tile);
        double left = leftPadding + Math.Floor(Math.Max(0, spare) / 2); // the flooring leaves a remainder: half goes to each side, in whole dp
        return new ShelfOrderGrid(cols, tile, cover + titleSpacing + titleHeight, gapX, gapY, left, topPadding);
    }
}

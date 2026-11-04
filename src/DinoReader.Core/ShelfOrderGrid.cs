namespace DinoReader.Core;

/// <summary>
/// The geometry of the reorder mode's grid (Slice 264): tiles of one size in rows of <paramref name="Columns"/>, so the page can place each book and find the spot a finger is over.
/// Pure arithmetic, no views. Never mirrored: column 0 is always on the left.
/// </summary>
public readonly record struct ShelfOrderGrid(int Columns, double TileWidth, double TileHeight, double GapX, double GapY, double Left, double Top)
{
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
    public static ShelfOrderGrid Fit(double width, int columns, double sidePadding, double gapX, double gapY, double titleHeight, double titleSpacing)
    {
        int cols = Math.Max(1, columns);
        double tile = Math.Max(1, (width - (2 * sidePadding) - ((cols - 1) * gapX)) / cols);
        double cover = tile / LandscapeLayout.DefaultPictureAspect;
        return new ShelfOrderGrid(cols, tile, cover + titleSpacing + titleHeight, gapX, gapY, sidePadding, gapY);
    }
}

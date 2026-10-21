namespace ReadTheStupidText.Application.Images;

/// <summary>
/// Turns the detector's probability map into text boxes (the DB post-processing of PaddleOCR,
/// without OpenCV): binarize at <see cref="BinaryThreshold"/>, label 4-connected regions, drop
/// weak or tiny ones, and unclip each region's rectangle (the detector predicts a shrunken
/// kernel). Boxes are axis-aligned, so steeply rotated text is merged or missed; the document
/// scanner hands us deskewed pages. Coordinates are scaled back to the original image.
/// </summary>
public static class DbPostProcessor
{
    public const float BinaryThreshold = 0.3f;
    public const float BoxThreshold = 0.6f;
    public const float UnclipRatio = 1.5f;
    private const int MinRegionSide = 3;
    private const int MinBoxSide = 5;

    public static IReadOnlyList<OcrBox> Boxes(float[] probabilities, int mapWidth, int mapHeight, float scaleX, float scaleY, int imageWidth, int imageHeight)
    {
        var boxes = new List<OcrBox>();
        foreach (Region region in Regions(probabilities, mapWidth, mapHeight))
        {
            if (region.Width < MinRegionSide || region.Height < MinRegionSide || region.MeanScore < BoxThreshold)
            {
                continue;
            }

            OcrBox box = ToImage(Unclip(region, mapWidth, mapHeight), scaleX, scaleY, imageWidth, imageHeight);
            if (box.Width >= MinBoxSide && box.Height >= MinBoxSide)
            {
                boxes.Add(box);
            }
        }

        return boxes;
    }

    private static OcrBox Unclip(Region region, int mapWidth, int mapHeight)
    {
        float width = region.Width;
        float height = region.Height;
        float distance = width * height * UnclipRatio / (2 * (width + height));
        return new OcrBox(
            Math.Max(0, region.MinX - distance),
            Math.Max(0, region.MinY - distance),
            Math.Min(mapWidth, region.MaxX + 1 + distance),
            Math.Min(mapHeight, region.MaxY + 1 + distance));
    }

    private static OcrBox ToImage(OcrBox box, float scaleX, float scaleY, int imageWidth, int imageHeight) =>
        new(
            Math.Clamp(box.Left / scaleX, 0, imageWidth),
            Math.Clamp(box.Top / scaleY, 0, imageHeight),
            Math.Clamp(box.Right / scaleX, 0, imageWidth),
            Math.Clamp(box.Bottom / scaleY, 0, imageHeight));

    private static IEnumerable<Region> Regions(float[] probabilities, int width, int height)
    {
        bool[] visited = new bool[width * height];
        var stack = new Stack<int>();
        for (int start = 0; start < visited.Length; start++)
        {
            if (visited[start] || probabilities[start] <= BinaryThreshold)
            {
                continue;
            }

            yield return Flood(probabilities, width, height, start, visited, stack);
        }
    }

    private static Region Flood(float[] probabilities, int width, int height, int start, bool[] visited, Stack<int> stack)
    {
        var region = new Region(start % width, start / width);
        visited[start] = true;
        stack.Push(start);
        while (stack.Count > 0)
        {
            int index = stack.Pop();
            region.Add(index % width, index / width, probabilities[index]);
            PushNeighbours(probabilities, width, height, index, visited, stack);
        }

        return region;
    }

    private static void PushNeighbours(float[] probabilities, int width, int height, int index, bool[] visited, Stack<int> stack)
    {
        int x = index % width;
        int y = index / width;
        TryPush(probabilities, visited, stack, x > 0, index - 1);
        TryPush(probabilities, visited, stack, x < width - 1, index + 1);
        TryPush(probabilities, visited, stack, y > 0, index - width);
        TryPush(probabilities, visited, stack, y < height - 1, index + width);
    }

    private static void TryPush(float[] probabilities, bool[] visited, Stack<int> stack, bool inside, int index)
    {
        if (inside && !visited[index] && probabilities[index] > BinaryThreshold)
        {
            visited[index] = true;
            stack.Push(index);
        }
    }

    private sealed class Region
    {
        private float _scoreSum;
        private int _count;

        public Region(int x, int y)
        {
            MinX = MaxX = x;
            MinY = MaxY = y;
        }

        public int MinX { get; private set; }
        public int MaxX { get; private set; }
        public int MinY { get; private set; }
        public int MaxY { get; private set; }
        public int Width => MaxX - MinX + 1;
        public int Height => MaxY - MinY + 1;
        public float MeanScore => _scoreSum / _count;

        public void Add(int x, int y, float score)
        {
            MinX = Math.Min(MinX, x);
            MaxX = Math.Max(MaxX, x);
            MinY = Math.Min(MinY, y);
            MaxY = Math.Max(MaxY, y);
            _scoreSum += score;
            _count++;
        }
    }
}

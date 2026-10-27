using System.Text.Encodings.Web;
using System.Text.Json;
using DinoReader.Core;

namespace DinoReader.BookBuilder;

/// <summary>
/// Builds the files of each free store story (Slice 102) from its <c>content/dino/&lt;stem&gt;</c> folder (Slice 157) and the checked-in translations:
/// <c>artifacts/store/&lt;id&gt;/{meta.json, cover.jpg, story-v&lt;n&gt;.dino}</c>, the layout the Worker serves (Slice 92). The <c>build</c> command writes them;
/// the <c>publish-store</c> job (or <c>tools/upload-store-story.ps1</c>) puts a folder up to R2. Voices are cast the way the app does, through
/// <see cref="StarterBuilder.Build"/>.
/// </summary>
internal static class StoreBuilder
{
    public const string MetaFile = "meta.json";
    public const string CoverFile = "cover.jpg";
    public const string CoverThumbFile = "cover-thumb.jpg";
    public const string StoreLanguage = "en";

    private const int WordsPerMinute = 110;

    private static readonly JsonSerializerOptions MetaOptions = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string PackageFileName(int version) => $"story-v{version}.dino";

    /// <summary>Where <c>build</c> writes the store folders; <c>--out</c> overrides it.</summary>
    public static string? OutputFolder { get; set; }

    public static string StoreFolder() => OutputFolder ?? Path.Combine(StarterBuilder.RepoRoot(), "artifacts", "store");

    /// <summary>The store entries of the four bundled starters (Decision 128), by package file name: the same ids as the bundled files, so a deleted starter can be downloaded again.</summary>
    public static IReadOnlyDictionary<string, StoreEntry> StarterEntries =>
        Books.Starters.ToDictionary(b => b.FileName, b => new StoreEntry(b.Info.Title, b.Info.Id, b.Info.Colour, b.Info.Credits, b.Info.AgeMin, b.Info.AgeMax, b.Info.Author, b.Info.Added));

    /// <summary><c>content/dino/&lt;stem&gt;</c>: the checked-in <c>pictures/1..N.jpg</c> and <c>cover.jpg</c> of a book (Slice 119).</summary>
    public static string StoryArtFolder(string stem) => Books.Folder(stem).Dir;

    /// <summary>The story as it is packed: cast voices, <c>TranslateToMyLanguage</c> on and every checked-in translation merged in.</summary>
    public static Story Build(StoreStorySpec spec) =>
        StarterBuilder.WithTranslations(
            StarterBuilder.WithPictures(StarterBuilder.Build(spec.Id, spec.Title, spec.Text, spec.ColourIndex), PictureFilesOf(spec)), spec.Stem);

    private static IReadOnlyList<string> PictureFilesOf(StoreStorySpec spec) =>
        StarterBuilder.PictureFilesIn(Books.Folder(spec.Stem).PicturesPath);

    internal static async Task WriteStoryAsync(StoreStorySpec spec, FileStoryLibrary library)
    {
        Story story = Build(spec);
        await library.AddAsync(story);
        IReadOnlyList<string> pictures = PictureFilesOf(spec);
        StarterBuilder.CopyPictures(story, pictures, library);
        string? coverSource = StarterBuilder.ExistingFile(Path.Combine(StoryArtFolder(spec.Stem), "cover.jpg"));
        await WritePackageAsync(await StarterBuilder.WithPictureCoverAsync(story, coverSource, library), spec, library, Path.Combine(StoryArtFolder(spec.Stem), CoverThumbFile));
    }

    internal static async Task WritePackageAsync(Story story, StoreStorySpec spec, FileStoryLibrary library, string thumbnailPath)
    {
        string folder = Path.Combine(StoreFolder(), spec.Id);
        Directory.CreateDirectory(folder);
        await using (FileStream output = File.Create(Path.Combine(folder, PackageFileName(spec.Version))))
        {
            await StoryPackageWriter.WriteAsync(story, library, output, includeRecordings: false, version: spec.Version); // a store upload never carries a parent recording (Slice 111)
        }

        await File.WriteAllBytesAsync(Path.Combine(folder, CoverFile), CoverBytes(story, spec, library, thumbnailPath));
        await File.WriteAllTextAsync(Path.Combine(folder, MetaFile), MetaJson(spec, story));
    }

    /// <summary>The store list's cover (a row per story on a metered phone, so small): the checked-in <c>cover-thumb.jpg</c> (288x384, written by
    /// <c>tools/prepare-story-pictures.ps1</c>) when the story has a picture cover, else the generated colour cover. The full cover stays inside the package.
    /// A picture cover with no thumbnail file fails the build with a clear message.</summary>
    public static byte[] CoverBytes(Story story, StoreStorySpec spec, FileStoryLibrary library, string? thumbnailPath = null)
    {
        if (story.CoverStyle != CoverStyle.Picture || story.CoverFile is null)
        {
            return StoreCover.Render(spec.Title, CoverPalette.ColourAt(spec.ColourIndex));
        }

        string path = StarterBuilder.ExistingFile(thumbnailPath ?? string.Empty)
            ?? throw new InvalidOperationException($"The store story '{spec.Title}' has a picture cover but no {CoverThumbFile}; run tools/prepare-story-pictures.ps1 to make it.");
        return File.ReadAllBytes(path);
    }

    /// <summary>The <c>meta.json</c> the Worker checks (<c>worker/stories.ts</c> <c>parseMeta</c>): the page count and the reading time come from the text.</summary>
    public static string MetaJson(StoreStorySpec spec, Story story) =>
        JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["schemaVersion"] = StoryPackageManifest.CurrentSchemaVersion,
            ["title"] = spec.Title,
            ["language"] = StoreLanguage,
            ["version"] = spec.Version,
            ["pageCount"] = story.Pages.Count,
            ["minutes"] = Minutes(story.Text),
            ["added"] = spec.Added,
            ["credits"] = spec.Credits,
            ["ageMin"] = spec.AgeMin,
            ["ageMax"] = spec.AgeMax,
            ["author"] = spec.Author,
            ["titles"] = TitlesOf(story),
        }, MetaOptions);

    /// <summary>Slice 159: the book's title in every other language (<see cref="ShippedTitles"/>); the store shows the chosen language's.</summary>
    public static SortedDictionary<string, string> TitlesOf(Story story) =>
        new(ShippedTitles.AllFor(story.Title).ToDictionary(t => t.Key, t => t.Value), StringComparer.Ordinal);

    /// <summary>Reading time in whole minutes at a child's pace, never below one.</summary>
    public static int Minutes(string text)
    {
        int words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Max(1, (int)Math.Ceiling(words / (double)WordsPerMinute));
    }
}

/// <summary>The store metadata of a bundled starter that is also a store story (Decision 128).</summary>
internal sealed record StoreEntry(string Title, string Id, int ColourIndex, string Credits, int AgeMin, int AgeMax, string Author, string Added)
{
    /// <summary>The spec the store builder writes it under: the story's own text and the starter's content version (<see cref="StarterBuilder.StarterVersions"/>).</summary>
    public StoreStorySpec ToSpec(Story story, int version) =>
        new(Id, Title, string.Empty, story.Text, ColourIndex, Credits, AgeMin, AgeMax, Author, [], version, Added);
}

/// <summary>
/// The store list's cover (Slice 102): the generated colour cover, drawn from the title in a built-in pixel font on the story's colour,
/// and encoded as a baseline JPEG by <see cref="JpegWriter"/>. The test project has no imaging library, and a plain pixel font
/// keeps the covers deterministic on every OS.
/// </summary>
internal static class StoreCover
{
    public const int Width = 288;
    public const int Height = 384;

    private const int Scale = 4;
    private const int GlyphWidth = 5;
    private const int GlyphHeight = 7;
    private const int GlyphGap = 1;
    private const int MaxCharsPerLine = 10;
    private const int LineGap = 3;
    private const byte White = 255;
    private const byte Neutral = 128;

    // A to Z, seven rows of five cells each, rows separated by "/".
    private static readonly string[] Glyphs =
    [
        ".###./#...#/#...#/#####/#...#/#...#/#...#", "####./#...#/#...#/####./#...#/#...#/####.",
        ".####/#..../#..../#..../#..../#..../.####", "####./#...#/#...#/#...#/#...#/#...#/####.",
        "#####/#..../#..../####./#..../#..../#####", "#####/#..../#..../####./#..../#..../#....",
        ".####/#..../#..../#..##/#...#/#...#/.###.", "#...#/#...#/#...#/#####/#...#/#...#/#...#",
        "#####/..#../..#../..#../..#../..#../#####", "..###/...#./...#./...#./...#./#..#./.##..",
        "#...#/#..#./#.#../##.../#.#../#..#./#...#", "#..../#..../#..../#..../#..../#..../#####",
        "#...#/##.##/#.#.#/#.#.#/#...#/#...#/#...#", "#...#/##..#/#.#.#/#..##/#...#/#...#/#...#",
        ".###./#...#/#...#/#...#/#...#/#...#/.###.", "####./#...#/#...#/####./#..../#..../#....",
        ".###./#...#/#...#/#...#/#.#.#/#..#./.##.#", "####./#...#/#...#/####./#.#../#..#./#...#",
        ".####/#..../#..../.###./....#/....#/####.", "#####/..#../..#../..#../..#../..#../..#..",
        "#...#/#...#/#...#/#...#/#...#/#...#/.###.", "#...#/#...#/#...#/#...#/#...#/.#.#./..#..",
        "#...#/#...#/#...#/#.#.#/#.#.#/##.##/#...#", "#...#/#...#/.#.#./..#../.#.#./#...#/#...#",
        "#...#/#...#/.#.#./..#../..#../..#../..#..", "#####/....#/...#./..#../.#.../#..../#####",
    ];

    // The one punctuation mark a title may carry ("X Marks the Treasure!").
    private const string Exclamation = "..#../..#../..#../..#../..#../...../..#..";

    /// <summary>The JPEG of a cover: <paramref name="title"/> in white capitals, wrapped and centred, on <paramref name="colourHex"/> (<c>#RRGGBB</c>).</summary>
    public static byte[] Render(string title, string colourHex)
    {
        byte red = Convert.ToByte(colourHex.Substring(1, 2), 16);
        byte green = Convert.ToByte(colourHex.Substring(3, 2), 16);
        byte blue = Convert.ToByte(colourHex.Substring(5, 2), 16);
        (byte y, byte cb, byte cr) background = JpegWriter.ToYCbCr(red, green, blue);
        bool[,] ink = Typeset(title);
        byte[][] planes = [new byte[Width * Height], new byte[Width * Height], new byte[Width * Height]];
        for (int row = 0; row < Height; row++)
        {
            for (int col = 0; col < Width; col++)
            {
                (byte y, byte cb, byte cr) pixel = ink[col, row] ? (White, Neutral, Neutral) : background;
                int at = row * Width + col;
                (planes[0][at], planes[1][at], planes[2][at]) = pixel;
            }
        }

        return JpegWriter.Encode(planes, Width, Height);
    }

    /// <summary>The lines the title wraps to: words stay whole, at most <see cref="MaxCharsPerLine"/> letters wide.</summary>
    public static IReadOnlyList<string> Wrap(string title)
    {
        var lines = new List<string>();
        string current = string.Empty;
        foreach (string word in title.ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.Length > 0 && current.Length + 1 + word.Length > MaxCharsPerLine)
            {
                lines.Add(current);
                current = string.Empty;
            }

            current = current.Length == 0 ? word : current + " " + word;
        }

        lines.Add(current);
        return lines;
    }

    private static bool[,] Typeset(string title)
    {
        var ink = new bool[Width, Height];
        IReadOnlyList<string> lines = Wrap(title);
        int lineHeight = GlyphHeight * Scale;
        int top = (Height - (lines.Count * lineHeight + (lines.Count - 1) * LineGap * Scale)) / 2;
        for (int i = 0; i < lines.Count; i++)
        {
            DrawLine(ink, lines[i], top + i * (lineHeight + LineGap * Scale));
        }

        return ink;
    }

    private static void DrawLine(bool[,] ink, string line, int top)
    {
        int advance = (GlyphWidth + GlyphGap) * Scale;
        int left = (Width - (line.Length * advance - GlyphGap * Scale)) / 2;
        for (int i = 0; i < line.Length; i++)
        {
            string? glyph = line[i] switch
            {
                >= 'A' and <= 'Z' => Glyphs[line[i] - 'A'],
                '!' => Exclamation,
                _ => null,
            };
            if (glyph is not null)
            {
                DrawGlyph(ink, glyph.Split('/'), left + i * advance, top);
            }
        }
    }

    private static void DrawGlyph(bool[,] ink, string[] rows, int left, int top)
    {
        for (int row = 0; row < GlyphHeight; row++)
        {
            for (int col = 0; col < GlyphWidth; col++)
            {
                if (rows[row][col] == '#')
                {
                    Fill(ink, left + col * Scale, top + row * Scale);
                }
            }
        }
    }

    private static void Fill(bool[,] ink, int x, int y)
    {
        for (int dy = 0; dy < Scale; dy++)
        {
            for (int dx = 0; dx < Scale; dx++)
            {
                ink[x + dx, y + dy] = true;
            }
        }
    }
}

/// <summary>
/// A small baseline JPEG encoder (4:4:4, uniform quantiser, one custom Huffman table pair), enough for a flat cover. The Huffman tables
/// are valid DHT tables with fixed-length codes (4 bits for DC, 8 for AC), so no standard-table data is needed.
/// </summary>
internal static class JpegWriter
{
    private const int Block = 8;
    private const int Quantiser = 2;
    private const int DcCodeBits = 4;
    private const int AcCodeBits = 8;
    private const int DcSymbolCount = 12;
    private const int ZeroRunLimit = 15;
    private const int ZeroRunLength = 16;
    private const byte EndOfBlock = 0x00;
    private const byte ZeroRunSymbol = 0xF0;

    private static readonly int[] ZigZag =
    [
        0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5, 12, 19, 26, 33, 40, 48, 41, 34, 27, 20, 13, 6, 7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51, 58, 59, 52, 45, 38, 31, 39, 46, 53, 60, 61, 54, 47, 55, 62, 63,
    ];

    // The AC symbols in code order: end of block, 16 zeros, then every (run, size) pair; a symbol's code is its index.
    private static readonly byte[] AcSymbols =
        [EndOfBlock, ZeroRunSymbol, .. Enumerable.Range(0, 16).SelectMany(run => Enumerable.Range(1, 10).Select(size => (byte)((run << 4) | size)))];

    private static readonly double[,] CosTable = BuildCosines();

    public static (byte Y, byte Cb, byte Cr) ToYCbCr(byte r, byte g, byte b) =>
    (
        Clamp(0.299 * r + 0.587 * g + 0.114 * b),
        Clamp(128 - 0.168736 * r - 0.331264 * g + 0.5 * b),
        Clamp(128 + 0.5 * r - 0.418688 * g - 0.081312 * b)
    );

    /// <summary>Encodes three planes (Y, Cb, Cr, row-major) whose sides are multiples of eight.</summary>
    public static byte[] Encode(byte[][] planes, int width, int height)
    {
        using var output = new MemoryStream();
        WriteHeaders(output, width, height);
        WriteScan(output, planes, width, height);
        output.Write([0xFF, 0xD9]);
        return output.ToArray();
    }

    private static double[,] BuildCosines()
    {
        var table = new double[Block, Block];
        for (int x = 0; x < Block; x++)
        {
            for (int u = 0; u < Block; u++)
            {
                table[x, u] = Math.Cos((2 * x + 1) * u * Math.PI / 16);
            }
        }

        return table;
    }

    private static byte Clamp(double value) => (byte)Math.Clamp((int)Math.Round(value), 0, 255);

    private static void WriteSegment(Stream output, byte marker, byte[] body)
    {
        int length = body.Length + 2;
        output.Write([0xFF, marker, (byte)(length >> 8), (byte)length]);
        output.Write(body);
    }

    private static void WriteHeaders(Stream output, int width, int height)
    {
        output.Write([0xFF, 0xD8]);
        WriteSegment(output, 0xE0, [(byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0, 1, 0, 1, 0, 0]);
        WriteSegment(output, 0xDB, [0, .. Enumerable.Repeat((byte)Quantiser, 64)]);
        WriteSegment(output, 0xC0, [8, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 3, 1, 0x11, 0, 2, 0x11, 0, 3, 0x11, 0]);
        WriteSegment(output, 0xC4, DhtBody(0x00, DcCodeBits, Enumerable.Range(0, DcSymbolCount).Select(i => (byte)i).ToArray()));
        WriteSegment(output, 0xC4, DhtBody(0x10, AcCodeBits, AcSymbols));
        WriteSegment(output, 0xDA, [3, 1, 0x00, 2, 0x00, 3, 0x00, 0, 63, 0]);
    }

    private static byte[] DhtBody(byte tableClassAndId, int codeBits, byte[] symbols)
    {
        var counts = new byte[16];
        counts[codeBits - 1] = (byte)symbols.Length;
        return [tableClassAndId, .. counts, .. symbols];
    }

    private static void WriteScan(Stream output, byte[][] planes, int width, int height)
    {
        var bits = new BitWriter(output);
        var previousDc = new int[3];
        for (int by = 0; by < height; by += Block)
        {
            for (int bx = 0; bx < width; bx += Block)
            {
                for (int c = 0; c < 3; c++)
                {
                    previousDc[c] = EncodeBlock(bits, Quantise(planes[c], width, bx, by), previousDc[c]);
                }
            }
        }

        bits.Flush();
    }

    // The 64 quantised coefficients of one block, in zigzag order.
    private static int[] Quantise(byte[] plane, int width, int bx, int by)
    {
        var samples = new double[Block, Block];
        for (int y = 0; y < Block; y++)
        {
            for (int x = 0; x < Block; x++)
            {
                samples[y, x] = plane[(by + y) * width + bx + x] - 128;
            }
        }

        var natural = new int[Block * Block];
        for (int v = 0; v < Block; v++)
        {
            for (int u = 0; u < Block; u++)
            {
                natural[v * Block + u] = (int)Math.Round(Coefficient(samples, u, v) / Quantiser);
            }
        }

        return ZigZag.Select(i => natural[i]).ToArray();
    }

    private static double Coefficient(double[,] samples, int u, int v)
    {
        double sum = 0;
        for (int y = 0; y < Block; y++)
        {
            for (int x = 0; x < Block; x++)
            {
                sum += samples[y, x] * CosTable[x, u] * CosTable[y, v];
            }
        }

        return 0.25 * Weight(u) * Weight(v) * sum;
    }

    private static double Weight(int index) => index == 0 ? Math.Sqrt(0.5) : 1;

    private static int EncodeBlock(BitWriter bits, int[] zigzag, int previousDc)
    {
        int difference = zigzag[0] - previousDc;
        int size = SizeOf(difference);
        bits.Write(size, DcCodeBits);
        bits.Write(Magnitude(difference, size), size);
        int run = 0;
        for (int i = 1; i < zigzag.Length; i++)
        {
            if (zigzag[i] == 0)
            {
                run++;
                continue;
            }

            for (; run > ZeroRunLimit; run -= ZeroRunLength)
            {
                bits.Write(Array.IndexOf(AcSymbols, ZeroRunSymbol), AcCodeBits);
            }

            int acSize = SizeOf(zigzag[i]);
            bits.Write(Array.IndexOf(AcSymbols, (byte)((run << 4) | acSize)), AcCodeBits);
            bits.Write(Magnitude(zigzag[i], acSize), acSize);
            run = 0;
        }

        if (run > 0)
        {
            bits.Write(Array.IndexOf(AcSymbols, EndOfBlock), AcCodeBits);
        }

        return zigzag[0];
    }

    private static int SizeOf(int value) => value == 0 ? 0 : 32 - System.Numerics.BitOperations.LeadingZeroCount((uint)Math.Abs(value));

    private static int Magnitude(int value, int size) => value >= 0 ? value : value + (1 << size) - 1;

    private sealed class BitWriter(Stream output)
    {
        private int _buffer;
        private int _count;

        public void Write(int value, int length)
        {
            for (int i = length - 1; i >= 0; i--)
            {
                _buffer = (_buffer << 1) | ((value >> i) & 1);
                if (++_count == 8)
                {
                    Emit();
                }
            }
        }

        public void Flush()
        {
            while (_count != 0)
            {
                Write(1, 1);
            }
        }

        private void Emit()
        {
            output.WriteByte((byte)_buffer);
            if (_buffer == 0xFF)
            {
                output.WriteByte(0);
            }

            (_buffer, _count) = (0, 0);
        }
    }
}
